// ignore_for_file: avoid_print
import 'package:flutter_test/flutter_test.dart';
import 'package:kpl/core/league.dart';
import 'package:kpl/core/rng.dart';
import 'package:kpl/data/players_db.dart';
import 'package:kpl/data/teams.dart';
import 'package:kpl/engine/auction.dart';
import 'package:kpl/engine/career.dart';
import 'package:kpl/engine/delivery.dart';
import 'package:kpl/engine/match_engine.dart';
import 'package:kpl/engine/season.dart';
import 'package:kpl/models/franchise.dart';
import 'package:kpl/models/player.dart';
import 'package:shared_preferences/shared_preferences.dart';

Franchise _squad2026(String id) {
  final f = Franchise(teamById(id));
  for (final c in Career.preAuctionSquads()[id]!) {
    f.squad.add(c);
  }
  f.autoLineup();
  return f;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  SharedPreferences.setMockInitialValues({});

  test('player database is complete and consistent', () {
    final db = PlayerDb.instance;
    expect(db.all.length, 285);
    for (final t in kTeams) {
      expect(db.squad2026(t.id).length, 25, reason: '${t.id} 2026 squad');
      expect(db.squad2026(t.id).where((p) => p.overseas).length, lessThanOrEqualTo(8), reason: t.id);
    }
    final names = db.all.map((p) => p.name).toSet();
    expect(names.length, db.all.length, reason: 'duplicate names');
    for (final tr in kTrades2027) {
      expect(db.byName(tr.player), isNotNull, reason: tr.player);
    }
    final pre = Career.preAuctionSquads();
    expect(pre['DC']!.any((c) => c.player.name == 'Rishabh Pant'), isTrue);
    expect(pre['LSG']!.any((c) => c.player.name == 'Kuldeep Yadav'), isTrue);
    expect(pre['SRH']!.any((c) => c.player.name == 'T Natarajan'), isTrue);
    for (final e in pre.entries) {
      expect(e.value.length, 25, reason: e.key);
    }
    final top = List.of(db.all)..sort((a, b) => b.overall.compareTo(a.overall));
    print('Top 12 overall: ${top.take(12).map((p) => '${p.name} ${p.overall}').join(', ')}');
  });

  test('match engine produces realistic T20 scores', () {
    var total1 = 0, total2 = 0, wk1 = 0, sixes = 0, chases = 0, superOvers = 0;
    const n = 400;
    final ids = kTeams.map((t) => t.id).toList();
    final squads = {for (final id in ids) id: _squad2026(id)};
    for (var i = 0; i < n; i++) {
      final h = ids[i % 10], a = ids[(i * 3 + 1) % 10 == i % 10 ? (i + 1) % 10 : (i * 3 + 1) % 10];
      final m = MatchEngine(
        home: TeamSheet.fromFranchise(squads[h]!),
        away: TeamSheet.fromFranchise(squads[a]!),
        venue: teamById(h),
        seed: i * 7919,
      )..simulateToEnd();
      expect(m.finished, isTrue);
      total1 += m.innings[0].runs;
      wk1 += m.innings[0].wickets;
      total2 += m.innings[1].runs;
      sixes += m.innings[0].bat.values.fold(0, (s, c) => s + c.sixes);
      if (m.outcome!.winnerId == m.innings[1].batting.team.id) chases++;
      if (m.outcome!.superOver) superOvers++;
      // Bowling quotas respected.
      for (final inn in m.innings.take(2)) {
        for (final b in inn.bowl.values) {
          expect(b.balls, lessThanOrEqualTo(24));
        }
        expect(inn.legalBalls, lessThanOrEqualTo(120));
      }
    }
    final avg1 = total1 / n, avg2 = total2 / n;
    print('Avg 1st inns: ${avg1.toStringAsFixed(1)}/${(wk1 / n).toStringAsFixed(1)}  '
        'avg 2nd: ${avg2.toStringAsFixed(1)}  sixes/inns: ${(sixes / n).toStringAsFixed(1)}  '
        'chase win%: ${(chases * 100 / n).toStringAsFixed(0)}  super overs: $superOvers');
    expect(avg1, inInclusiveRange(165, 205));
    expect(wk1 / n, inInclusiveRange(5.0, 8.5));
  });

  test('season schedule has 70 games, 14 per team, 7 at home', () {
    final s = Season.generate(2027, Rng(42));
    expect(s.league.length, 70);
    for (final t in kTeams) {
      final mine = s.league.where((f) => f.involves(t.id)).toList();
      expect(mine.length, 14, reason: t.id);
      expect(mine.where((f) => f.home == t.id).length, 7, reason: '${t.id} home games');
    }
    expect(s.league.first.home, 'RCB');
  });

  test('mega auction fills every squad legally', () {
    final c = Career.instance;
    c.startNew(mode: CareerMode.mega, teamId: 'CSK');
    final userKeep = c.preAuctionContracts('CSK').map((x) => x.player).toList()
      ..sort((a, b) => b.overall.compareTo(a.overall));
    c.confirmMegaRetention(userKeep.take(3).map((p) => p.id).toList());
    final a = c.auction!;
    print('Retentions: ${c.franchises.values.map((f) => '${f.id}:${f.size}(${money(f.purse)})').join('  ')}');
    print('Lots: ${a.lots.length}');
    var guard = 0;
    while (!a.finished && guard++ < 2000) {
      a.resolveInstantly();
      a.advance();
      if (a.lot != null) a.openLot();
    }
    c.finishAuction();
    for (final f in c.franchises.values) {
      expect(f.size, inInclusiveRange(League.minSquad, League.maxSquad), reason: f.id);
      expect(f.overseasCount, lessThanOrEqualTo(League.maxOverseas), reason: f.id);
      expect(f.purse, greaterThanOrEqualTo(0), reason: f.id);
      expect(lineupProblem(f.xiPlayers), isNull, reason: f.id);
    }
    final sold = a.lots.where((l) => l.status == LotStatus.sold).toList()
      ..sort((x, y) => y.price.compareTo(x.price));
    print('Sold: ${sold.length}. Top buys: ${sold.take(10).map((l) => '${l.player.name}->${l.soldTo} ${money(l.price)}${l.viaRtm ? ' (RTM)' : ''}').join(', ')}');
    print('Squads: ${c.franchises.values.map((f) => '${f.id} ${f.size}p ${f.overseasCount}os str ${f.strength} left ${money(f.purse)}').join(' | ')}');
  });

  test('full season runs to a champion', () {
    final c = Career.instance;
    final s = c.season!;
    var guard = 0;
    while (s.nextFixture != null && guard++ < 100) {
      c.simulateFixture(s.nextFixture!);
      s.advancePlayoffs();
    }
    expect(s.leagueDone, isTrue);
    expect(s.playoffs.length, 4);
    expect(s.champion, isNotNull);
    final table = s.standings();
    print('Table: ${table.map((t) => '${t.teamId} ${t.pts} (${t.nrr.toStringAsFixed(2)})').join(', ')}');
    final orange = s.leaders((x) => x.runs.toDouble(), n: 3);
    final purple = s.leaders((x) => x.wickets.toDouble(), n: 3);
    print('Champion: ${s.champion}  Orange: ${orange.map((e) => '${PlayerDb.instance.byId(e.key).name} ${e.value.runs}').join(', ')}');
    print('Purple: ${purple.map((e) => '${PlayerDb.instance.byId(e.key).name} ${e.value.wickets}').join(', ')}');
    final json = s.toJson();
    final back = Season.fromJson(json);
    expect(back.fixtures.length, s.fixtures.length);
    expect(back.champion, s.champion);
  });

  test('user batting resolution behaves sensibly', () {
    final f1 = _squad2026('RCB'), f2 = _squad2026('MI');
    var perfectRuns = 0, lateRuns = 0, perfectOuts = 0, lateOuts = 0;
    const n = 600;
    for (var i = 0; i < n; i++) {
      final m = MatchEngine(home: TeamSheet.fromFranchise(f1), away: TeamSheet.fromFranchise(f2), venue: teamById('RCB'), seed: i);
      m.setToss('RCB', true);
      m.autoBowlerIfNeeded();
      final plan = planAiDelivery(m.rng, m.bowler, 1);
      final good = m.resolveUserShot(plan, const ShotInput(dir: ShotDir.straight, timingMs: 5));
      final bad = m.resolveUserShot(plan, const ShotInput(dir: ShotDir.straight, timingMs: 230));
      perfectRuns += good.runs;
      lateRuns += bad.runs;
      if (good.wicket) perfectOuts++;
      if (bad.wicket) lateOuts++;
    }
    print('perfect: ${(perfectRuns / n).toStringAsFixed(2)} r/b, out ${(perfectOuts * 100 / n).toStringAsFixed(1)}%   '
        'late: ${(lateRuns / n).toStringAsFixed(2)} r/b, out ${(lateOuts * 100 / n).toStringAsFixed(1)}%');
    expect(perfectRuns, greaterThan(lateRuns));
    expect(perfectOuts, lessThan(lateOuts));
  });

  test('user bowling resolves for any aim, accuracy and variation', () {
    final f1 = _squad2026('MI'), f2 = _squad2026('CSK');
    final rng = Rng(7);
    for (var i = 0; i < 400; i++) {
      final m = MatchEngine(home: TeamSheet.fromFranchise(f1), away: TeamSheet.fromFranchise(f2), venue: teamById('CSK'), seed: i);
      m.setToss('MI', true);
      m.autoBowlerIfNeeded();
      final aimY = 8.12 + rng.next() * 11.6, aimX = -1.3 + rng.next() * 2.6;
      final type = DeliveryType.values[rng.nextInt(DeliveryType.values.length)];
      final line = BallLine.values[rng.nextInt(BallLine.values.length)];
      final plan = planUserDelivery(m.rng, m.bowler, type, line, rng.next(), m.current.phase,
          aimY: aimY, aimX: aimX, swing: rng.chance(0.5) ? 0.27 : null);
      final r = m.resolveUserBowling(plan, [DeliveryType.yorker]);
      m.apply(r);
    }
  });

  test('mini auction flow works', () {
    final c = Career.instance;
    c.startNew(mode: CareerMode.mini, teamId: 'MI');
    final mine = c.preAuctionContracts('MI');
    final release = mine.where((x) => x.player.overall < 66).map((x) => x.player.id).toSet();
    c.confirmMiniReleases(release);
    final a = c.auction!;
    print('Mini pool lots: ${a.lots.length}; purses: ${c.franchises.values.map((f) => '${f.id} ${money(f.purse)} (${f.size})').join(', ')}');
    var guard = 0;
    while (!a.finished && guard++ < 2000) {
      a.resolveInstantly();
      a.advance();
      if (a.lot != null) a.openLot();
    }
    c.finishAuction();
    for (final f in c.franchises.values) {
      expect(f.size, inInclusiveRange(League.minSquad, League.maxSquad), reason: f.id);
      expect(f.purse, greaterThanOrEqualTo(0), reason: f.id);
    }
    final sold = a.lots.where((l) => l.status == LotStatus.sold).toList()..sort((x, y) => y.price.compareTo(x.price));
    print('Mini top buys: ${sold.take(8).map((l) => '${l.player.name}->${l.soldTo} ${money(l.price)}').join(', ')}');
  });

  test('roles parse correctly', () {
    final db = PlayerDb.instance;
    expect(db.byName('Jasprit Bumrah')!.role, Role.bowl);
    expect(db.byName('Rishabh Pant')!.leftBat, isTrue);
    expect(db.byName('Kuldeep Yadav')!.bowlStyle, BowlStyle.lws);
    expect(db.byName('Quinton de Kock')!.surname, 'DE KOCK');
    expect(db.byName('MS Dhoni')!.shortName, 'MS Dhoni');
    expect(db.byName('Virat Kohli')!.jersey, 18);
  });
}
