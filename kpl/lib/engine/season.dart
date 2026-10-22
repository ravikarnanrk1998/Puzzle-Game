import 'dart:math' as math;

import '../core/rng.dart';
import '../data/teams.dart';
import 'match_engine.dart';

enum FixtureKind { league, qualifier1, eliminator, qualifier2, finalMatch }

extension FixtureKindX on FixtureKind {
  String get label => const ['League', 'Qualifier 1', 'Eliminator', 'Qualifier 2', 'FINAL'][index];
  bool get isPlayoff => this != FixtureKind.league;
}

class MatchSummary {
  MatchSummary({
    required this.firstBat,
    required this.r1,
    required this.w1,
    required this.b1,
    required this.r2,
    required this.w2,
    required this.b2,
    required this.winner,
    required this.text,
    required this.potmId,
    this.superOver = false,
    this.byUser = false,
  });

  final String firstBat;
  final int r1, w1, b1, r2, w2, b2;
  final String? winner;
  final String text;
  final int potmId;
  final bool superOver;
  final bool byUser;

  Map<String, dynamic> toJson() => {
        'f': firstBat,
        's': [r1, w1, b1, r2, w2, b2],
        'w': winner,
        't': text,
        'p': potmId,
        'so': superOver,
        'u': byUser,
      };

  static MatchSummary fromJson(Map<String, dynamic> j) {
    final s = (j['s'] as List).cast<int>();
    return MatchSummary(
      firstBat: j['f'] as String,
      r1: s[0],
      w1: s[1],
      b1: s[2],
      r2: s[3],
      w2: s[4],
      b2: s[5],
      winner: j['w'] as String?,
      text: j['t'] as String,
      potmId: j['p'] as int,
      superOver: j['so'] as bool? ?? false,
      byUser: j['u'] as bool? ?? false,
    );
  }
}

class Fixture {
  Fixture({
    required this.no,
    required this.home,
    required this.away,
    required this.venueId,
    this.kind = FixtureKind.league,
    this.result,
  });

  final int no;
  String home;
  String away;
  final String venueId;
  final FixtureKind kind;
  MatchSummary? result;

  bool involves(String id) => home == id || away == id;
  String opponentOf(String id) => home == id ? away : home;
  bool get played => result != null;
  String get title => kind == FixtureKind.league ? 'Match $no' : kind.label;

  /// Weekend double-headers: roughly one league game in six starts at 3:30 PM.
  bool get afternoon => kind == FixtureKind.league && no % 6 == 3;
  String get startTime => afternoon ? '3:30 PM IST' : '7:30 PM IST';

  Map<String, dynamic> toJson() => {
        'n': no,
        'h': home,
        'a': away,
        'v': venueId,
        'k': kind.index,
        'r': result?.toJson(),
      };

  static Fixture fromJson(Map<String, dynamic> j) => Fixture(
        no: j['n'] as int,
        home: j['h'] as String,
        away: j['a'] as String,
        venueId: j['v'] as String,
        kind: FixtureKind.values[j['k'] as int],
        result: j['r'] == null ? null : MatchSummary.fromJson(j['r'] as Map<String, dynamic>),
      );
}

class Standing {
  Standing(this.teamId);

  final String teamId;
  int p = 0, w = 0, l = 0, nr = 0;
  int runsFor = 0, ballsFaced = 0, runsAgainst = 0, ballsBowled = 0;
  final List<String> form = [];

  int get pts => w * 2 + nr;
  double get nrr {
    if (ballsFaced == 0 || ballsBowled == 0) return 0;
    return runsFor * 6 / ballsFaced - runsAgainst * 6 / ballsBowled;
  }
}

class PlayerSeasonStats {
  int matches = 0, innings = 0, runs = 0, balls = 0, fours = 0, sixes = 0;
  int notOuts = 0, high = 0, fifties = 0, hundreds = 0;
  bool highNotOut = false;
  int wickets = 0, ballsBowled = 0, runsConceded = 0, dots = 0;
  int bestW = 0, bestR = 0;
  int catches = 0, stumpings = 0, potm = 0;

  double get average {
    final outs = innings - notOuts;
    return outs <= 0 ? runs.toDouble() : runs / outs;
  }

  double get strikeRate => balls == 0 ? 0 : runs * 100 / balls;
  double get economy => ballsBowled == 0 ? 0 : runsConceded * 6 / ballsBowled;

  /// Official-style MVP points.
  double get mvp => wickets * 3.5 + dots * 1.0 + fours * 2.5 + sixes * 3.5 + (catches + stumpings) * 2.5;

  List<int> toJson() => [
        matches, innings, runs, balls, fours, sixes, notOuts, high, fifties, hundreds,
        highNotOut ? 1 : 0, wickets, ballsBowled, runsConceded, dots, bestW, bestR,
        catches, stumpings, potm,
      ];

  static PlayerSeasonStats fromJson(List<int> v) {
    final s = PlayerSeasonStats();
    s.matches = v[0];
    s.innings = v[1];
    s.runs = v[2];
    s.balls = v[3];
    s.fours = v[4];
    s.sixes = v[5];
    s.notOuts = v[6];
    s.high = v[7];
    s.fifties = v[8];
    s.hundreds = v[9];
    s.highNotOut = v[10] == 1;
    s.wickets = v[11];
    s.ballsBowled = v[12];
    s.runsConceded = v[13];
    s.dots = v[14];
    s.bestW = v[15];
    s.bestR = v[16];
    s.catches = v[17];
    s.stumpings = v[18];
    s.potm = v[19];
    return s;
  }
}

class Season {
  Season({required this.year, required this.fixtures, Map<int, PlayerSeasonStats>? stats})
      : stats = stats ?? {};

  final int year;
  final List<Fixture> fixtures;
  final Map<int, PlayerSeasonStats> stats;
  String? champion;
  String? runnerUp;

  List<Fixture> get league => fixtures.where((f) => f.kind == FixtureKind.league).toList();
  List<Fixture> get playoffs => fixtures.where((f) => f.kind.isPlayoff).toList();
  bool get leagueDone => league.every((f) => f.played);
  bool get finished => champion != null;

  Fixture? get nextFixture {
    for (final f in fixtures) {
      if (!f.played) return f;
    }
    return null;
  }

  Fixture? nextFixtureFor(String teamId) {
    for (final f in fixtures) {
      if (!f.played && f.involves(teamId)) return f;
    }
    return null;
  }

  // ------------------------------------------------------- schedule

  static Season generate(int year, Rng rng, {String openerHome = 'RCB'}) {
    final a = kTeams.where((t) => t.group == 'A').map((t) => t.id).toList();
    final b = kTeams.where((t) => t.group == 'B').map((t) => t.id).toList();
    final pairs = <List<String>>[];
    for (final g in [a, b]) {
      for (var i = 0; i < g.length; i++) {
        for (var j = i + 1; j < g.length; j++) {
          pairs.add([g[i], g[j]]);
          pairs.add([g[j], g[i]]);
        }
      }
    }
    for (var i = 0; i < 5; i++) {
      // Designated cross-group rival: played twice.
      pairs.add([a[i], b[i]]);
      pairs.add([b[i], a[i]]);
      for (var j = 0; j < 5; j++) {
        if (j == i) continue;
        final homeA = ((j - i) % 5 + 5) % 5 <= 2;
        pairs.add(homeA ? [a[i], b[j]] : [b[j], a[i]]);
      }
    }
    rng.shuffle(pairs);
    final ordered = <List<String>>[];
    final remaining = List.of(pairs);
    final openerIdx = remaining.indexWhere((p) => p[0] == openerHome);
    ordered.add(remaining.removeAt(openerIdx < 0 ? 0 : openerIdx));
    while (remaining.isNotEmpty) {
      bool clash(List<String> p, int back) {
        if (ordered.length < back) return false;
        final q = ordered[ordered.length - back];
        return p.contains(q[0]) || p.contains(q[1]);
      }

      var k = remaining.indexWhere((p) => !clash(p, 1) && !clash(p, 2));
      if (k < 0) k = remaining.indexWhere((p) => !clash(p, 1));
      if (k < 0) k = 0;
      ordered.add(remaining.removeAt(k));
    }
    final fixtures = <Fixture>[
      for (var i = 0; i < ordered.length; i++)
        Fixture(no: i + 1, home: ordered[i][0], away: ordered[i][1], venueId: ordered[i][0]),
    ];
    return Season(year: year, fixtures: fixtures);
  }

  // ------------------------------------------------------- standings

  List<Standing> standings() {
    final map = {for (final t in kTeams) t.id: Standing(t.id)};
    for (final f in league) {
      final r = f.result;
      if (r == null) continue;
      final first = r.firstBat;
      final second = f.opponentOf(first);
      final s1 = map[first]!, s2 = map[second]!;
      s1.p++;
      s2.p++;
      if (r.winner == null) {
        s1.nr++;
        s2.nr++;
        s1.form.add('N');
        s2.form.add('N');
        continue;
      }
      if (r.winner == first) {
        s1.w++;
        s2.l++;
        s1.form.add('W');
        s2.form.add('L');
      } else {
        s2.w++;
        s1.l++;
        s2.form.add('W');
        s1.form.add('L');
      }
      final balls1 = r.w1 >= 10 ? 120 : r.b1;
      final balls2 = r.w2 >= 10 ? 120 : r.b2;
      s1.runsFor += r.r1;
      s1.ballsFaced += balls1;
      s1.runsAgainst += r.r2;
      s1.ballsBowled += balls2;
      s2.runsFor += r.r2;
      s2.ballsFaced += balls2;
      s2.runsAgainst += r.r1;
      s2.ballsBowled += balls1;
    }
    final list = map.values.toList()
      ..sort((x, y) {
        final c = y.pts.compareTo(x.pts);
        if (c != 0) return c;
        final n = y.nrr.compareTo(x.nrr);
        if (n != 0) return n;
        return y.w.compareTo(x.w);
      });
    return list;
  }

  int positionOf(String teamId) => standings().indexWhere((s) => s.teamId == teamId) + 1;

  // --------------------------------------------------------- playoffs

  Fixture? _po(FixtureKind k) {
    for (final f in fixtures) {
      if (f.kind == k) return f;
    }
    return null;
  }

  String _winner(Fixture f) => f.result!.winner!;
  String _loser(Fixture f) => f.opponentOf(f.result!.winner!);

  /// Adds playoff fixtures as soon as their participants are known.
  void advancePlayoffs() {
    if (!leagueDone) return;
    final n = fixtures.length;
    if (_po(FixtureKind.qualifier1) == null) {
      final top = standings().take(4).map((s) => s.teamId).toList();
      fixtures.add(Fixture(no: n + 1, home: top[0], away: top[1], venueId: top[0], kind: FixtureKind.qualifier1));
      fixtures.add(Fixture(no: n + 2, home: top[2], away: top[3], venueId: top[2], kind: FixtureKind.eliminator));
      return;
    }
    final q1 = _po(FixtureKind.qualifier1)!, el = _po(FixtureKind.eliminator)!;
    if (_po(FixtureKind.qualifier2) == null) {
      if (q1.played && el.played) {
        fixtures.add(Fixture(no: n + 1, home: _loser(q1), away: _winner(el), venueId: 'KKR', kind: FixtureKind.qualifier2));
      }
      return;
    }
    final q2 = _po(FixtureKind.qualifier2)!;
    if (_po(FixtureKind.finalMatch) == null) {
      if (q2.played) {
        fixtures.add(Fixture(no: n + 1, home: _winner(q1), away: _winner(q2), venueId: 'GT', kind: FixtureKind.finalMatch));
      }
      return;
    }
    final fin = _po(FixtureKind.finalMatch)!;
    if (fin.played && champion == null) {
      champion = _winner(fin);
      runnerUp = _loser(fin);
    }
  }

  /// Teams still alive in the playoffs (for the bracket UI).
  bool isEliminated(String teamId) {
    if (!leagueDone) return false;
    final top4 = standings().take(4).map((s) => s.teamId).toSet();
    if (!top4.contains(teamId)) return true;
    for (final f in playoffs) {
      if (!f.played || !f.involves(teamId)) continue;
      if (_loser(f) == teamId && f.kind != FixtureKind.qualifier1) return true;
    }
    return false;
  }

  // ------------------------------------------------------------ stats

  PlayerSeasonStats statsOf(int id) => stats.putIfAbsent(id, PlayerSeasonStats.new);

  void record(Fixture f, MatchEngine m, {bool byUser = false}) {
    final i1 = m.innings[0];
    final i2 = m.innings.length > 1 ? m.innings[1] : null;
    f.result = MatchSummary(
      firstBat: i1.batting.team.id,
      r1: i1.runs,
      w1: i1.wickets,
      b1: i1.legalBalls,
      r2: i2?.runs ?? 0,
      w2: i2?.wickets ?? 0,
      b2: i2?.legalBalls ?? 0,
      winner: m.outcome?.winnerId,
      text: m.outcome?.text ?? 'No result',
      potmId: m.outcome?.potmId ?? i1.batting.order.first.id,
      superOver: m.outcome?.superOver ?? false,
      byUser: byUser,
    );
    for (final p in [...m.home.order, ...m.away.order]) {
      statsOf(p.id).matches++;
    }
    for (final inn in m.innings.take(2)) {
      for (final c in inn.bat.values) {
        final s = statsOf(c.playerId);
        s.innings++;
        s.runs += c.runs;
        s.balls += c.balls;
        s.fours += c.fours;
        s.sixes += c.sixes;
        if (!c.out) s.notOuts++;
        if (c.runs > s.high || (c.runs == s.high && !c.out)) {
          s.high = c.runs;
          s.highNotOut = !c.out;
        }
        if (c.runs >= 100) {
          s.hundreds++;
        } else if (c.runs >= 50) {
          s.fifties++;
        }
        if (c.out && c.fielderId != null) {
          final fs = statsOf(c.fielderId!);
          if (c.how == Dismissal.stumped) {
            fs.stumpings++;
          } else if (c.how == Dismissal.caught || c.how == Dismissal.caughtBehind) {
            fs.catches++;
          }
        }
      }
      for (final b in inn.bowl.values) {
        final s = statsOf(b.playerId);
        s.wickets += b.wickets;
        s.ballsBowled += b.balls;
        s.runsConceded += b.runs;
        s.dots += b.dots;
        if (b.wickets > s.bestW || (b.wickets == s.bestW && b.runs < s.bestR) || (s.bestW == 0 && s.bestR == 0)) {
          s.bestW = b.wickets;
          s.bestR = b.runs;
        }
      }
    }
    if (m.outcome != null) statsOf(m.outcome!.potmId).potm++;
    advancePlayoffs();
  }

  List<MapEntry<int, PlayerSeasonStats>> leaders(double Function(PlayerSeasonStats s) key, {int n = 10}) {
    final list = stats.entries.where((e) => key(e.value) > 0).toList()
      ..sort((a, b) => key(b.value).compareTo(key(a.value)));
    return list.take(math.max(0, n)).toList();
  }

  Map<String, dynamic> toJson() => {
        'y': year,
        'f': fixtures.map((f) => f.toJson()).toList(),
        's': {for (final e in stats.entries) '${e.key}': e.value.toJson()},
        'c': champion,
        'ru': runnerUp,
      };

  static Season fromJson(Map<String, dynamic> j) {
    final s = Season(
      year: j['y'] as int,
      fixtures: (j['f'] as List).map((e) => Fixture.fromJson(e as Map<String, dynamic>)).toList(),
      stats: {
        for (final e in (j['s'] as Map<String, dynamic>).entries)
          int.parse(e.key): PlayerSeasonStats.fromJson((e.value as List).cast<int>()),
      },
    );
    s.champion = j['c'] as String?;
    s.runnerUp = j['ru'] as String?;
    return s;
  }
}
