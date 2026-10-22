import 'dart:math' as math;

import '../core/league.dart';
import '../core/rng.dart';
import '../data/teams.dart';
import '../models/franchise.dart';
import '../models/player.dart';
import 'delivery.dart';

enum ExtraType { none, wide, noBall, legBye, bye }

enum Dismissal { bowled, caught, caughtBehind, lbw, runOut, stumped }

extension DismissalX on Dismissal {
  String get label => const [
        'BOWLED',
        'CAUGHT',
        'CAUGHT BEHIND',
        'LBW',
        'RUN OUT',
        'STUMPED',
      ][index];
}

class BallResult {
  const BallResult({
    this.runs = 0,
    this.extra = ExtraType.none,
    this.extraRuns = 0,
    this.wicket = false,
    this.dismissal,
    this.fielderId,
    this.outBatterId,
    this.angle,
    this.lofted = false,
    this.timing,
    this.edge = false,
    this.beaten = false,
    this.defended = false,
  });

  /// Runs credited to the batter.
  final int runs;
  final ExtraType extra;

  /// Extras conceded on this ball (wide = 1 + any runs, no-ball = 1, byes...).
  final int extraRuns;
  final bool wicket;
  final Dismissal? dismissal;
  final int? fielderId;
  final int? outBatterId;

  /// Field angle of the shot in degrees (batter-relative, + = off side).
  final double? angle;
  final bool lofted;
  final TimingGrade? timing;
  final bool edge;
  final bool beaten;
  final bool defended;

  bool get legal => extra != ExtraType.wide && extra != ExtraType.noBall;
  int get total => runs + extraRuns;
  bool get isFour => runs == 4;
  bool get isSix => runs == 6;

  String get chip {
    if (wicket) return 'W';
    switch (extra) {
      case ExtraType.wide:
        return extraRuns > 1 ? '${extraRuns}wd' : 'wd';
      case ExtraType.noBall:
        return runs > 0 ? '${runs}nb' : 'nb';
      case ExtraType.legBye:
        return '${extraRuns}lb';
      case ExtraType.bye:
        return '${extraRuns}b';
      case ExtraType.none:
        return runs == 0 ? '•' : '$runs';
    }
  }
}

/// Probability table for one ball: wicket, then runs 0,1,2,3,4,6.
class OutcomeDist {
  OutcomeDist(this.w, this.d0, this.d1, this.d2, this.d3, this.d4, this.d6);

  double w, d0, d1, d2, d3, d4, d6;

  void normalize() {
    final s = w + d0 + d1 + d2 + d3 + d4 + d6;
    if (s <= 0) {
      d0 = 1;
      return;
    }
    w /= s;
    d0 /= s;
    d1 /= s;
    d2 /= s;
    d3 /= s;
    d4 /= s;
    d6 /= s;
  }

  /// Applies batter-vs-bowler skill: positive favours the batter.
  void scaleSkill(double skill) {
    w *= math.exp(-0.35 * skill);
    d4 *= math.exp(0.22 * skill);
    d6 *= math.exp(0.28 * skill);
    normalize();
  }

  /// Returns -1 for a wicket, otherwise runs scored.
  int sample(Rng rng) {
    var x = rng.next() * (w + d0 + d1 + d2 + d3 + d4 + d6);
    if ((x -= w) < 0) return -1;
    if ((x -= d0) < 0) return 0;
    if ((x -= d1) < 0) return 1;
    if ((x -= d2) < 0) return 2;
    if ((x -= d3) < 0) return 3;
    if ((x -= d4) < 0) return 4;
    return 6;
  }
}

class TeamSheet {
  TeamSheet({
    required this.team,
    required this.order,
    required this.captainId,
    required this.keeperId,
  });

  factory TeamSheet.fromFranchise(Franchise f) {
    f.ensureLineup();
    return TeamSheet(
      team: f.info,
      order: f.xiPlayers,
      captainId: f.captainId!,
      keeperId: f.keeperId!,
    );
  }

  final TeamInfo team;
  final List<Player> order;
  final int captainId;
  final int keeperId;

  Player byId(int id) => order.firstWhere((p) => p.id == id);

  TeamSheet withOrder(List<Player> newOrder) =>
      TeamSheet(team: team, order: newOrder, captainId: captainId, keeperId: keeperId);
}

class BatCard {
  BatCard(this.playerId, this.position);

  final int playerId;
  final int position;
  int runs = 0, balls = 0, fours = 0, sixes = 0;
  bool out = false;
  Dismissal? how;
  int? bowlerId;
  int? fielderId;

  double get strikeRate => balls == 0 ? 0 : runs * 100 / balls;
}

class BowlCard {
  BowlCard(this.playerId);

  final int playerId;
  int balls = 0, runs = 0, wickets = 0, maidens = 0, dots = 0;
  int wides = 0, noBalls = 0, fours = 0, sixes = 0;

  double get economy => balls == 0 ? 0 : runs * 6 / balls;
  String get figures => '${oversText(balls)}-$maidens-$runs-$wickets';
}

class Fow {
  const Fow(this.score, this.wicket, this.balls, this.playerId);
  final int score, wicket, balls, playerId;
}

class BallLog {
  BallLog({
    required this.over,
    required this.result,
    required this.bowlerId,
    required this.batterId,
    required this.score,
    required this.wickets,
  });

  final int over;
  final BallResult result;
  final int bowlerId, batterId, score, wickets;
}

class Innings {
  Innings({
    required this.batting,
    required this.bowling,
    required this.maxOvers,
    this.target,
    int? maxWickets,
    this.superOver = false,
  }) : maxWickets = maxWickets ?? math.min(10, batting.order.length - 1) {
    striker = batting.order[0].id;
    nonStriker = batting.order[1].id;
    bat[striker] = BatCard(striker, 1);
    bat[nonStriker] = BatCard(nonStriker, 2);
  }

  final TeamSheet batting, bowling;
  final int maxOvers;
  final int? target;
  final int maxWickets;
  final bool superOver;

  int runs = 0, wickets = 0, legalBalls = 0;
  int wides = 0, noBalls = 0, legByes = 0, byes = 0;
  final Map<int, BatCard> bat = {};
  final Map<int, BowlCard> bowl = {};
  final List<int> bowlerOrder = [];
  final List<BallLog> log = [];
  final List<int> overRuns = [];
  final List<Fow> fow = [];
  late int striker;
  late int nonStriker;
  int nextIndex = 2;
  int? bowlerId;
  int? lastOverBowler;
  bool freeHit = false;
  int overAcc = 0;
  int overBowlerAcc = 0;
  int partnershipRuns = 0, partnershipBalls = 0;

  int get maxBalls => maxOvers * 6;
  int get ballsLeft => maxBalls - legalBalls;
  int get extras => wides + noBalls + legByes + byes;
  bool get allOut => wickets >= maxWickets;
  bool get complete => allOut || legalBalls >= maxBalls || (target != null && runs >= target!);
  int get overIndex => legalBalls ~/ 6;
  int get ballInOver => legalBalls % 6;
  double get runRate => legalBalls == 0 ? 0 : runs * 6 / legalBalls;
  int? get runsNeeded => target == null ? null : math.max(0, target! - runs);
  double? get requiredRate =>
      target == null || ballsLeft <= 0 ? null : runsNeeded! * 6 / ballsLeft;
  String get scoreText => '$runs/$wickets';
  String get overs => oversText(legalBalls);
  int get maxOversPerBowler => superOver ? 1 : math.max(1, (maxOvers / 5).ceil());

  /// 0 = powerplay, 1 = middle overs, 2 = death.
  int get phase {
    if (superOver) return 2;
    final ppEnd = math.max(1, (maxOvers * 0.3).round());
    final deathStart = maxOvers - math.max(1, (maxOvers * 0.2).round());
    if (overIndex < ppEnd) return 0;
    if (overIndex >= deathStart) return 2;
    return 1;
  }

  /// Balls of the over in progress (or the last completed over).
  List<BallLog> get recentOver {
    if (log.isEmpty) return const [];
    final over = (ballInOver == 0 && legalBalls > 0) ? overIndex - 1 : overIndex;
    return log.where((b) => b.over == over).toList();
  }

  int oversBowledBy(int id) => (bowl[id]?.balls ?? 0) ~/ 6;
}

class MatchOutcome {
  MatchOutcome({
    required this.winnerId,
    required this.text,
    required this.potmId,
    this.tie = false,
    this.superOver = false,
    this.marginRuns,
    this.marginWickets,
  });

  final String? winnerId;
  final String text;
  final int potmId;
  final bool tie;
  final bool superOver;
  final int? marginRuns;
  final int? marginWickets;
}

class MatchEngine {
  MatchEngine({
    required this.home,
    required this.away,
    required this.venue,
    required int seed,
    this.overs = League.t20Overs,
    this.difficulty = Difficulty.normal,
  }) : rng = Rng(seed);

  final TeamSheet home, away;
  final TeamInfo venue;
  final int overs;
  final Rng rng;
  final Difficulty difficulty;

  String? tossWinnerId;
  bool tossElectedBat = true;
  late TeamSheet battingFirst;
  late TeamSheet bowlingFirst;
  final List<Innings> innings = [];
  MatchOutcome? outcome;
  int superOvers = 0;

  bool get started => innings.isNotEmpty;
  bool get finished => outcome != null;
  Innings get current => innings.last;
  bool get inSuperOver => innings.length > 2;

  TeamSheet sheetOf(String teamId) => home.team.id == teamId ? home : away;

  // ------------------------------------------------------------- toss

  void setToss(String winnerId, bool electBat) {
    tossWinnerId = winnerId;
    tossElectedBat = electBat;
    final winner = sheetOf(winnerId);
    final loser = identical(winner, home) ? away : home;
    battingFirst = electBat ? winner : loser;
    bowlingFirst = electBat ? loser : winner;
    innings
      ..clear()
      ..add(Innings(batting: battingFirst, bowling: bowlingFirst, maxOvers: overs));
  }

  bool aiElectBat() => rng.chance(venue.batFactor > 1.05 ? 0.3 : 0.5);

  void autoToss() {
    final w = rng.chance(0.5) ? home : away;
    setToss(w.team.id, aiElectBat());
  }

  String get tossText {
    final w = sheetOf(tossWinnerId!).team;
    return '${w.name} won the toss and chose to ${tossElectedBat ? 'bat' : 'bowl'}';
  }

  // ---------------------------------------------------------- bowling

  bool get needBowler => !finished && current.bowlerId == null;

  List<Player> eligibleBowlers() {
    final inn = current;
    return inn.bowling.order.where((p) {
      if (p.id == inn.bowling.keeperId) return false;
      if (inn.oversBowledBy(p.id) >= inn.maxOversPerBowler) return false;
      if (p.id == inn.lastOverBowler) return false;
      return true;
    }).toList();
  }

  Player suggestBowler() {
    final inn = current;
    final cands = eligibleBowlers();
    final phase = inn.phase;
    final oversLeft = inn.maxOvers - inn.overIndex;
    Player? best;
    var bestScore = -1e9;
    for (final p in cands) {
      final left = inn.maxOversPerBowler - inn.oversBowledBy(p.id);
      var s = p.bowlStyle == BowlStyle.none ? -60.0 : p.bowl.toDouble();
      if (p.bowl < 50) s -= 25;
      if (phase == 0) s += p.isPace ? 5 : -3;
      if (phase == 1) s += p.isSpin ? 7 : -1;
      if (phase == 2) s += p.isPace ? 4 + (p.bowl - 75) * 0.4 : -6;
      if (phase == 1 && p.isPace && p.bowl >= 80 && left <= 2 && oversLeft > 4) s -= 9;
      s += left * 1.5;
      s += rng.range(-2.5, 2.5);
      if (s > bestScore) {
        bestScore = s;
        best = p;
      }
    }
    return best ?? inn.bowling.order.firstWhere((p) => p.id != inn.lastOverBowler);
  }

  void setBowler(int id) {
    final inn = current;
    inn.bowlerId = id;
    if (!inn.bowl.containsKey(id)) {
      inn.bowl[id] = BowlCard(id);
      inn.bowlerOrder.add(id);
    }
  }

  void autoBowlerIfNeeded() {
    if (needBowler) setBowler(suggestBowler().id);
  }

  Player get striker => current.batting.byId(current.striker);
  Player get nonStriker => current.batting.byId(current.nonStriker);
  Player get bowler => current.bowling.byId(current.bowlerId!);

  // ------------------------------------------------------ outcome model

  OutcomeDist _baseDist(Player batter, Player bowler, Innings inn, {double skillShift = 0}) {
    final bowlSkill = bowler.bowlStyle == BowlStyle.none ? 25 : bowler.bowl;
    var skill = (batter.bat - bowlSkill) / 22.0 + skillShift;
    final card = inn.bat[batter.id]!;
    if (card.balls > 12) skill += 0.12;
    if (card.balls > 25) skill += 0.08;
    if (card.balls < 4) skill -= 0.12;
    if (bowler.isSpin) skill -= (venue.spinFactor - 1) * 1.2;

    var agg = 0.22 + batter.power / 100 * 0.55;
    final phase = inn.phase;
    if (phase == 0) agg += 0.06;
    if (phase == 2) agg += 0.2;
    if (inn.target != null && inn.ballsLeft > 0) {
      final rrr = inn.runsNeeded! * 6 / inn.ballsLeft;
      agg += ((rrr - 8.5) / 8).clamp(-0.06, 0.45);
    }
    final wkLeft = inn.maxWickets - inn.wickets;
    if (wkLeft <= 3 && inn.ballsLeft > 18) agg -= 0.12;
    agg = agg.clamp(0.05, 1.05);
    final a = agg - 0.5;

    var w = 0.047 * math.exp(-0.36 * skill + 0.95 * a);
    var d6 = 0.044 * math.exp(0.4 * skill + 1.3 * a) * (0.55 + batter.power / 110);
    var d4 = 0.133 * math.exp(0.3 * skill + 0.6 * a);
    const d3 = 0.006;
    final d2 = 0.066 * math.exp(0.08 * skill);
    var d1 = 0.36 * math.exp(0.05 * skill - 0.3 * a);
    var d0 = 0.35 * math.exp(-0.28 * skill - 0.45 * a);
    switch (phase) {
      case 0:
        d4 *= 1.14;
        d1 *= 0.95;
      case 1:
        d4 *= 0.9;
        d6 *= 0.93;
        d1 *= 1.08;
        w *= 0.95;
      default:
        d6 *= 1.15;
        w *= 1.1;
    }
    d4 *= venue.batFactor;
    d6 *= venue.batFactor * venue.batFactor;
    d0 /= venue.batFactor;
    return OutcomeDist(w, d0, d1, d2, d3, d4, d6)..normalize();
  }

  Dismissal _pickDismissal(Player bowler) {
    final spin = bowler.isSpin;
    return rng.weighted({
      Dismissal.caught: 0.5,
      Dismissal.caughtBehind: spin ? 0.07 : 0.14,
      Dismissal.bowled: 0.17,
      Dismissal.lbw: spin ? 0.15 : 0.11,
      Dismissal.runOut: 0.05,
      Dismissal.stumped: spin ? 0.06 : 0.0,
    });
  }

  int _randomFielder({bool allowBowler = true}) {
    final inn = current;
    final opts = inn.bowling.order
        .where((p) => p.id != inn.bowling.keeperId && (allowBowler || p.id != inn.bowlerId))
        .toList();
    return rng.pick(opts).id;
  }

  double _angleForRuns(int runs) {
    switch (runs) {
      case 6:
        return rng.range(-115, 45);
      case 4:
        return rng.chance(0.55) ? rng.range(20, 165) : rng.range(-165, -20);
      case 0:
        return rng.range(-60, 60);
      default:
        return rng.range(-135, 135);
    }
  }

  double _angleForDismissal(Dismissal d) {
    switch (d) {
      case Dismissal.caughtBehind:
      case Dismissal.stumped:
        return rng.range(165, 180);
      case Dismissal.caught:
        return rng.range(-120, 70);
      default:
        return rng.range(-30, 30);
    }
  }

  BallResult _wicket(Dismissal d, {bool noBall = false, double? angle, bool lofted = false, TimingGrade? timing, bool edge = false}) {
    final inn = current;
    int? fielder;
    var outId = inn.striker;
    var runs = 0;
    switch (d) {
      case Dismissal.caught:
        fielder = rng.chance(0.06) ? inn.bowlerId : _randomFielder(allowBowler: false);
      case Dismissal.caughtBehind:
      case Dismissal.stumped:
        fielder = inn.bowling.keeperId;
      case Dismissal.runOut:
        fielder = _randomFielder();
        if (rng.chance(0.35)) outId = inn.nonStriker;
        runs = rng.chance(0.4) ? 1 : 0;
      default:
        break;
    }
    return BallResult(
      runs: runs,
      wicket: true,
      dismissal: d,
      fielderId: fielder,
      outBatterId: outId,
      extra: noBall ? ExtraType.noBall : ExtraType.none,
      extraRuns: noBall ? 1 : 0,
      angle: angle ?? _angleForDismissal(d),
      lofted: lofted || d == Dismissal.caught,
      timing: timing,
      edge: edge || d == Dismissal.caughtBehind,
    );
  }

  /// Fully simulated delivery (AI batting vs AI bowling).
  BallResult simulateBall() {
    final inn = current;
    final batter = striker;
    final bwl = bowler;
    final pace = bwl.isPace;
    final wideP = (0.022 + (pace ? 0.012 : 0.004) + (inn.phase == 2 ? 0.008 : 0) - (bwl.bowl - 75) * 0.0005)
        .clamp(0.008, 0.06);
    if (rng.chance(wideP)) {
      final extra = rng.chance(0.04) ? 5 : (rng.chance(0.05) ? 2 : 1);
      return BallResult(extra: ExtraType.wide, extraRuns: extra, beaten: true);
    }
    final noBall = rng.chance(pace ? 0.005 : 0.0015);
    final dist = _baseDist(batter, bwl, inn);
    return _finishSample(dist, bwl, noBall);
  }

  BallResult _finishSample(OutcomeDist dist, Player bwl, bool noBall, {DeliveryPlan? plan}) {
    final inn = current;
    var code = dist.sample(rng);
    if (code == -1 && (inn.freeHit || noBall)) {
      if (rng.chance(0.06)) return _wicket(Dismissal.runOut, noBall: noBall);
      code = 0;
    }
    if (code == -1) {
      var d = _pickDismissal(bwl);
      if (plan != null) {
        final stumps = plan.hitsStumps(leftHandBatter: striker.leftBat);
        if ((d == Dismissal.bowled || d == Dismissal.lbw) && !stumps) d = Dismissal.caughtBehind;
        if (plan.type == DeliveryType.bouncer && d != Dismissal.runOut) d = Dismissal.caught;
      }
      return _wicket(d);
    }
    if (!noBall && code == 0) {
      if (rng.chance(0.03)) return const BallResult(extra: ExtraType.legBye, extraRuns: 1);
      if (rng.chance(0.004)) return BallResult(extra: ExtraType.bye, extraRuns: rng.chance(0.3) ? 4 : 1);
    }
    return BallResult(
      runs: code,
      extra: noBall ? ExtraType.noBall : ExtraType.none,
      extraRuns: noBall ? 1 : 0,
      angle: _angleForRuns(code),
      lofted: code == 6 || (code == 4 && rng.chance(0.2)),
      defended: code == 0 && rng.chance(0.6),
      beaten: code == 0 && rng.chance(0.25),
    );
  }

  // ------------------------------------------- user batting resolution

  static const _suit = <List<double>>[
    // straight, cover, point, thirdMan, midWicket, squareLeg, fineLeg
    [0.35, 0.05, -0.5, 0.1, 0.05, -0.45, 0.25], // yorker
    [1.0, 0.9, 0.25, -0.2, 0.65, 0.0, -0.1], // full
    [0.45, 0.45, 0.35, 0.35, 0.25, 0.05, 0.2], // good length
    [0.0, 0.1, 0.7, 0.55, 0.6, 0.75, 0.5], // short
    [-0.9, -0.7, 0.15, 0.5, 0.25, 0.7, 0.85], // bouncer
    [1.0, 1.0, 0.65, 0.1, 1.0, 0.7, 0.3], // full toss
  ];

  static const _lineAdj = <List<double>>[
    [-0.25, 0.15, 0.25, 0.2, -0.55, -0.55, -0.5], // wide off
    [0.0, 0.1, 0.1, 0.05, -0.15, -0.25, -0.2], // off
    [0.15, -0.05, -0.2, -0.15, 0.15, 0.05, 0.0], // middle
    [-0.1, -0.4, -0.5, -0.4, 0.25, 0.3, 0.35], // leg
  ];

  BallLine _effectiveLine(DeliveryPlan d, Player batter) {
    final dir = batter.leftBat ? -1.0 : 1.0;
    final lat = d.line.legward + (d.swing + d.turn) * dir;
    if (lat < -0.45) return BallLine.wideOff;
    if (lat < -0.06) return BallLine.off;
    if (lat < 0.07) return BallLine.middle;
    return BallLine.leg;
  }

  double shotSuitability(DeliveryPlan d, ShotDir dir, Player batter) {
    final line = _effectiveLine(d, batter);
    var s = _suit[d.type.lengthRow][dir.index] + _lineAdj[line.index][dir.index];
    if (d.spin && (dir == ShotDir.fineLeg || dir == ShotDir.squareLeg) &&
        (d.type == DeliveryType.stock || d.type == DeliveryType.flighted)) {
      s += 0.35;
    }
    return s.clamp(-1.2, 1.3);
  }

  OutcomeDist _groundDist(TimingGrade g, double q) {
    switch (g) {
      case TimingGrade.perfect:
        if (q >= 0.6) return OutcomeDist(0.01, 0.05, 0.17, 0.14, 0.03, 0.58, 0.02);
        if (q >= 0.25) return OutcomeDist(0.02, 0.12, 0.33, 0.15, 0.02, 0.35, 0.01);
        if (q >= -0.15) return OutcomeDist(0.04, 0.27, 0.42, 0.1, 0.01, 0.16, 0);
        return OutcomeDist(0.12, 0.45, 0.3, 0.05, 0, 0.08, 0);
      case TimingGrade.good:
        if (q >= 0.6) return OutcomeDist(0.02, 0.1, 0.3, 0.16, 0.02, 0.4, 0);
        if (q >= 0.25) return OutcomeDist(0.03, 0.2, 0.42, 0.13, 0.01, 0.21, 0);
        if (q >= -0.15) return OutcomeDist(0.06, 0.36, 0.4, 0.07, 0, 0.11, 0);
        return OutcomeDist(0.15, 0.5, 0.26, 0.03, 0, 0.06, 0);
      case TimingGrade.early:
      case TimingGrade.late:
        return q < 0
            ? OutcomeDist(0.16, 0.42, 0.3, 0.04, 0, 0.08, 0)
            : OutcomeDist(0.1, 0.42, 0.33, 0.05, 0, 0.1, 0);
      case TimingGrade.veryEarly:
      case TimingGrade.veryLate:
        return OutcomeDist(0.24, 0.56, 0.12, 0, 0, 0.08, 0);
      case TimingGrade.miss:
        return OutcomeDist(0.05, 0.95, 0, 0, 0, 0, 0);
    }
  }

  OutcomeDist _loftDist(TimingGrade g, double q) {
    switch (g) {
      case TimingGrade.perfect:
        if (q >= 0.5) return OutcomeDist(0.04, 0, 0.05, 0.04, 0, 0.14, 0.73);
        if (q >= 0.15) return OutcomeDist(0.1, 0, 0.1, 0.06, 0, 0.2, 0.54);
        if (q >= -0.2) return OutcomeDist(0.2, 0.05, 0.15, 0.05, 0, 0.2, 0.35);
        return OutcomeDist(0.32, 0.15, 0.2, 0, 0, 0.15, 0.18);
      case TimingGrade.good:
        if (q >= 0.5) return OutcomeDist(0.1, 0, 0.1, 0.08, 0, 0.25, 0.47);
        if (q >= 0.15) return OutcomeDist(0.16, 0.05, 0.18, 0.08, 0, 0.23, 0.3);
        return OutcomeDist(0.3, 0.15, 0.22, 0.05, 0, 0.15, 0.13);
      case TimingGrade.early:
      case TimingGrade.late:
        return OutcomeDist(0.36, 0.2, 0.24, 0.05, 0, 0.1, 0.05);
      case TimingGrade.veryEarly:
      case TimingGrade.veryLate:
        return OutcomeDist(0.45, 0.4, 0.1, 0, 0, 0.05, 0);
      case TimingGrade.miss:
        return OutcomeDist(0.05, 0.95, 0, 0, 0, 0, 0);
    }
  }

  /// Outcome of the user's shot against an AI delivery.
  BallResult resolveUserShot(DeliveryPlan d, ShotInput s) {
    final inn = current;
    final batter = striker;
    final bwl = bowler;
    if (d.wide) {
      return const BallResult(extra: ExtraType.wide, extraRuns: 1, beaten: true);
    }
    final grade = s.timingMs == null ? null : gradeTiming(s.timingMs!, difficulty.windows);
    final skill = (batter.bat - bwl.bowl) / 22.0;
    final stumps = d.hitsStumps(leftHandBatter: batter.leftBat);
    final free = inn.freeHit || d.noBall;

    late OutcomeDist dist;
    var beaten = false;
    var defended = false;
    var edge = false;
    if (s.leave || grade == TimingGrade.miss) {
      beaten = true;
      dist = stumps ? OutcomeDist(0.68, 0.32, 0, 0, 0, 0, 0) : OutcomeDist(0.03, 0.97, 0, 0, 0, 0, 0);
    } else if (s.defend) {
      defended = true;
      switch (grade!) {
        case TimingGrade.perfect:
        case TimingGrade.good:
          dist = OutcomeDist(0, 0.86, 0.14, 0, 0, 0, 0);
        case TimingGrade.early:
        case TimingGrade.late:
          dist = OutcomeDist(0.05, 0.8, 0.12, 0, 0, 0.03, 0);
          edge = true;
        default:
          dist = OutcomeDist(stumps ? 0.2 : 0.08, 0.75, 0.05, 0, 0, 0, 0);
          edge = true;
      }
    } else {
      var q = shotSuitability(d, s.dir!, batter) + skill * 0.15;
      if (d.type == DeliveryType.variation && grade != TimingGrade.perfect) q -= 0.35;
      if (d.type == DeliveryType.slower && (grade == TimingGrade.early || grade == TimingGrade.veryEarly)) q -= 0.15;
      dist = s.loft ? _loftDist(grade!, q) : _groundDist(grade!, q);
      edge = grade != TimingGrade.perfect && grade != TimingGrade.good;
    }
    dist.scaleSkill(skill);
    var code = dist.sample(rng);
    if (code == -1 && free) {
      code = 0;
    }

    // Shot angle for the animation.
    double angle;
    if (s.dir != null) {
      final g = grade!;
      final noise = g == TimingGrade.perfect ? 8.0 : (g == TimingGrade.good ? 15.0 : 26.0);
      var a = s.dir!.angle + rng.gauss() * noise;
      if (g == TimingGrade.early || g == TimingGrade.veryEarly) a -= 18;
      if (g == TimingGrade.late || g == TimingGrade.veryLate) a += 18;
      if (edge && code != 6 && rng.chance(0.5)) a = rng.chance(0.5) ? rng.range(140, 170) : rng.range(-170, -140);
      angle = a;
    } else {
      angle = rng.range(-35, 35);
    }

    if (code == -1) {
      Dismissal dm;
      if (beaten && stumps) {
        dm = rng.chance(0.55) ? Dismissal.bowled : Dismissal.lbw;
      } else if (beaten) {
        dm = (d.spin && rng.chance(0.5)) ? Dismissal.stumped : Dismissal.caughtBehind;
      } else if (defended) {
        dm = stumps && rng.chance(0.5) ? Dismissal.bowled : Dismissal.caughtBehind;
      } else if (s.loft) {
        dm = rng.chance(0.9) ? Dismissal.caught : Dismissal.caughtBehind;
      } else if (edge) {
        dm = stumps && rng.chance(0.25)
            ? Dismissal.bowled
            : (rng.chance(0.6) ? Dismissal.caughtBehind : Dismissal.caught);
      } else {
        dm = rng.chance(0.92) ? Dismissal.caught : Dismissal.runOut;
      }
      return _wicket(dm, noBall: d.noBall, angle: angle, lofted: s.loft, timing: grade, edge: edge);
    }
    return BallResult(
      runs: code,
      extra: d.noBall ? ExtraType.noBall : ExtraType.none,
      extraRuns: d.noBall ? 1 : 0,
      angle: angle,
      lofted: s.loft || code == 6,
      timing: grade,
      edge: edge,
      beaten: beaten,
      defended: defended,
    );
  }

  // ------------------------------------------ user bowling resolution

  double deliveryQuality(DeliveryPlan d, List<DeliveryType> recent) {
    final phase = current.phase;
    var q = switch (d.type) {
      DeliveryType.yorker => 0.55 + (phase == 2 ? 0.2 : 0),
      DeliveryType.wideYorker => 0.5 + (phase == 2 ? 0.15 : 0),
      DeliveryType.full => (phase == 0 && !d.spin) ? 0.15 : -0.15,
      DeliveryType.goodLength => 0.35 + (phase == 0 ? 0.1 : 0),
      DeliveryType.short => phase == 1 ? 0.05 : 0.0,
      DeliveryType.bouncer => 0.25,
      DeliveryType.slower => 0.4 + (phase == 2 ? 0.1 : 0),
      DeliveryType.flighted => 0.15,
      DeliveryType.stock => 0.3,
      DeliveryType.quicker => 0.3 + (phase == 2 ? 0.1 : 0),
      DeliveryType.variation => 0.5,
      DeliveryType.fullToss => -0.85,
    };
    final reps = recent.take(3).where((t) => t == d.type).length;
    q -= reps * 0.17;
    q = q * (0.35 + 0.65 * d.accuracy) + (d.accuracy - 0.6) * 0.45;
    if (!d.spin && d.swing.abs() > 0.15) q += 0.08;
    if (d.spin) q += (venue.spinFactor - 1) * 0.8;
    return q.clamp(-1.0, 1.0);
  }

  /// Outcome of the user's delivery against the AI batter.
  BallResult resolveUserBowling(DeliveryPlan d, List<DeliveryType> recent) {
    if (d.wide) {
      return BallResult(extra: ExtraType.wide, extraRuns: rng.chance(0.05) ? 5 : 1, beaten: true);
    }
    final bwl = bowler;
    final q = deliveryQuality(d, recent);
    final dist = _baseDist(striker, bwl, current, skillShift: -q * 1.15);
    switch (d.type) {
      case DeliveryType.yorker:
      case DeliveryType.wideYorker:
        dist.d6 *= 0.35;
        dist.d4 *= 0.7;
        dist.w *= 1.1;
      case DeliveryType.bouncer:
        dist.w *= 1.25;
        dist.d6 *= 1.2;
        dist.d0 *= 1.15;
        dist.d1 *= 0.8;
      case DeliveryType.fullToss:
        dist.d4 *= 1.7;
        dist.d6 *= 1.8;
        dist.w *= 0.5;
      case DeliveryType.slower:
        dist.w *= 1.15;
      case DeliveryType.flighted:
        dist.d6 *= 1.3;
        dist.w *= 1.2;
      default:
        break;
    }
    dist.normalize();
    return _finishSample(dist, bwl, d.noBall, plan: d);
  }

  // ------------------------------------------------------ apply a ball

  void _swap(Innings inn) {
    final t = inn.striker;
    inn.striker = inn.nonStriker;
    inn.nonStriker = t;
  }

  void apply(BallResult r) {
    if (finished) return;
    final inn = current;
    final bowlerId = inn.bowlerId!;
    final bc = inn.bowl.putIfAbsent(bowlerId, () {
      inn.bowlerOrder.add(bowlerId);
      return BowlCard(bowlerId);
    });
    final sc = inn.bat[inn.striker]!;
    final overBefore = inn.overIndex;
    final wasFreeHit = inn.freeHit;
    final batterId = inn.striker;

    switch (r.extra) {
      case ExtraType.wide:
        inn.wides += r.extraRuns;
        inn.runs += r.extraRuns;
        bc.runs += r.extraRuns;
        bc.wides++;
        inn.overAcc += r.extraRuns;
        inn.overBowlerAcc += r.extraRuns;
        final ran = r.extraRuns == 5 ? 0 : r.extraRuns - 1;
        if (ran.isOdd) _swap(inn);
      case ExtraType.noBall:
        inn.noBalls += 1;
        inn.runs += 1 + r.runs;
        bc.runs += 1 + r.runs;
        bc.noBalls++;
        sc.runs += r.runs;
        sc.balls += 1;
        if (r.runs == 4) {
          sc.fours++;
          bc.fours++;
        }
        if (r.runs == 6) {
          sc.sixes++;
          bc.sixes++;
        }
        inn.overAcc += 1 + r.runs;
        inn.overBowlerAcc += 1 + r.runs;
        if (r.runs.isOdd && !r.wicket) _swap(inn);
      case ExtraType.legBye:
      case ExtraType.bye:
        if (r.extra == ExtraType.legBye) {
          inn.legByes += r.extraRuns;
        } else {
          inn.byes += r.extraRuns;
        }
        inn.runs += r.extraRuns;
        sc.balls += 1;
        inn.overAcc += r.extraRuns;
        if (r.extraRuns.isOdd) _swap(inn);
      case ExtraType.none:
        inn.runs += r.runs;
        sc.runs += r.runs;
        sc.balls += 1;
        bc.runs += r.runs;
        if (r.runs == 4) {
          sc.fours++;
          bc.fours++;
        }
        if (r.runs == 6) {
          sc.sixes++;
          bc.sixes++;
        }
        inn.overAcc += r.runs;
        inn.overBowlerAcc += r.runs;
        if (r.runs.isOdd && !r.wicket) _swap(inn);
    }
    inn.partnershipRuns += r.total;
    if (r.legal) inn.partnershipBalls++;

    if (r.wicket) {
      final outId = r.outBatterId ?? inn.striker;
      final oc = inn.bat[outId]!;
      oc.out = true;
      oc.how = r.dismissal;
      oc.bowlerId = r.dismissal == Dismissal.runOut ? null : bowlerId;
      oc.fielderId = r.fielderId;
      inn.wickets++;
      if (r.dismissal != Dismissal.runOut) bc.wickets++;
      inn.fow.add(Fow(inn.runs, inn.wickets, inn.legalBalls + (r.legal ? 1 : 0), outId));
      inn.partnershipRuns = 0;
      inn.partnershipBalls = 0;
      if (!inn.allOut && inn.nextIndex < inn.batting.order.length) {
        final nb = inn.batting.order[inn.nextIndex++];
        inn.bat[nb.id] = BatCard(nb.id, inn.nextIndex);
        if (outId == inn.striker) {
          inn.striker = nb.id;
        } else {
          inn.nonStriker = nb.id;
        }
        final caught = r.dismissal == Dismissal.caught || r.dismissal == Dismissal.caughtBehind;
        if (caught && inn.striker != nb.id) _swap(inn);
      }
    }

    if (r.legal) {
      inn.legalBalls++;
      bc.balls++;
      if ((r.extra == ExtraType.none && r.runs == 0) ||
          r.extra == ExtraType.legBye ||
          r.extra == ExtraType.bye) {
        bc.dots++;
      }
      inn.freeHit = false;
    }
    if (r.extra == ExtraType.noBall) {
      inn.freeHit = true;
    } else if (r.extra == ExtraType.wide && wasFreeHit) {
      inn.freeHit = true;
    }

    inn.log.add(BallLog(
      over: overBefore,
      result: r,
      bowlerId: bowlerId,
      batterId: batterId,
      score: inn.runs,
      wickets: inn.wickets,
    ));

    if (r.legal && inn.legalBalls % 6 == 0) {
      inn.overRuns.add(inn.overAcc);
      if (inn.overBowlerAcc == 0) bc.maidens++;
      inn.overAcc = 0;
      inn.overBowlerAcc = 0;
      inn.lastOverBowler = bowlerId;
      inn.bowlerId = null;
      if (!inn.complete) _swap(inn);
    }

    if (inn.complete) _endInnings();
  }

  // ------------------------------------------------------ innings flow

  TeamSheet _superOverSheet(TeamSheet s) {
    final order = List.of(s.order)
      ..sort((a, b) => (b.bat * 0.8 + b.power * 0.2).compareTo(a.bat * 0.8 + a.power * 0.2));
    return s.withOrder(order);
  }

  void _endInnings() {
    final inn = current;
    if (inn.legalBalls % 6 != 0 || inn.overAcc > 0) {
      inn.overRuns.add(inn.overAcc);
      inn.overAcc = 0;
    }
    if (innings.length == 1) {
      innings.add(Innings(
        batting: bowlingFirst,
        bowling: battingFirst,
        maxOvers: overs,
        target: inn.runs + 1,
      ));
      return;
    }
    if (innings.length == 2) {
      final i1 = innings[0], i2 = innings[1];
      if (i2.runs >= i2.target!) {
        final wk = i2.maxWickets - i2.wickets;
        _finish(i2.batting, '${i2.batting.team.name} won by $wk wicket${wk == 1 ? '' : 's'}', wickets: wk);
      } else if (i2.runs == i2.target! - 1) {
        _startSuperOver(i2);
      } else {
        final rn = i1.runs - i2.runs;
        _finish(i1.batting, '${i1.batting.team.name} won by $rn run${rn == 1 ? '' : 's'}', runs: rn);
      }
      return;
    }
    // Super over innings.
    final soIndex = innings.length - 2; // 1-based count of super-over innings
    if (soIndex.isOdd) {
      innings.add(Innings(
        batting: _superOverSheet(inn.bowling),
        bowling: inn.batting,
        maxOvers: 1,
        maxWickets: 2,
        target: inn.runs + 1,
        superOver: true,
      ));
    } else {
      final so1 = innings[innings.length - 2], so2 = inn;
      if (so2.runs >= so2.target!) {
        _finish(so2.batting, '${so2.batting.team.name} won the Super Over', superOver: true);
      } else if (so2.runs == so2.target! - 1 && superOvers < 4) {
        _startSuperOver(so2);
      } else if (so2.runs == so2.target! - 1) {
        // Still level after several super overs: most boundaries in the match.
        int boundaries(TeamSheet t) => innings
            .where((i) => identical(i.batting.team, t.team))
            .fold(0, (s, i) => s + i.bat.values.fold(0, (a, c) => a + c.fours + c.sixes));
        final w = boundaries(so1.batting) >= boundaries(so2.batting) ? so1.batting : so2.batting;
        _finish(w, '${w.team.name} won on boundary count', superOver: true);
      } else {
        _finish(so1.batting, '${so1.batting.team.name} won the Super Over', superOver: true);
      }
    }
  }

  void _startSuperOver(Innings last) {
    superOvers++;
    innings.add(Innings(
      batting: _superOverSheet(last.batting),
      bowling: last.bowling,
      maxOvers: 1,
      maxWickets: 2,
      superOver: true,
    ));
  }

  void _finish(TeamSheet winner, String text, {int? runs, int? wickets, bool superOver = false}) {
    outcome = MatchOutcome(
      winnerId: winner.team.id,
      text: text,
      potmId: _potm(winner.team.id),
      tie: superOver,
      superOver: superOver,
      marginRuns: runs,
      marginWickets: wickets,
    );
  }

  int _potm(String winnerId) {
    final score = <int, double>{};
    void add(int id, double v) => score[id] = (score[id] ?? 0) + v;
    for (final inn in innings.take(2)) {
      for (final c in inn.bat.values) {
        var s = c.runs + c.fours * 1.0 + c.sixes * 2.0;
        if (c.balls >= 10) s += (c.strikeRate - 135) * c.balls / 120;
        add(c.playerId, s);
        if (c.out && c.fielderId != null) {
          add(c.fielderId!, c.how == Dismissal.runOut ? 6 : 7);
        }
      }
      for (final b in inn.bowl.values) {
        var s = b.wickets * 24.0 + b.dots * 1.0;
        if (b.balls > 0) s -= (b.economy - 8.5) * (b.balls / 6) * 2.5;
        add(b.playerId, s);
      }
    }
    final winners = sheetOf(winnerId).order.map((p) => p.id).toSet();
    var bestId = sheetOf(winnerId).order.first.id;
    var best = -1e9;
    score.forEach((id, v) {
      final adj = winners.contains(id) ? v * 1.2 + 10 : v;
      if (adj > best) {
        best = adj;
        bestId = id;
      }
    });
    return bestId;
  }

  // ------------------------------------------------- fast simulation

  void simulateBallAuto() {
    if (finished) return;
    autoBowlerIfNeeded();
    apply(simulateBall());
  }

  void simulateOver() {
    if (finished) return;
    final inn = current;
    final startOver = inn.overIndex;
    while (!finished && identical(current, inn) && inn.overIndex == startOver) {
      simulateBallAuto();
    }
  }

  void simulateInnings() {
    if (finished) return;
    final inn = current;
    while (!finished && identical(current, inn)) {
      simulateBallAuto();
    }
  }

  void simulateToEnd() {
    if (!started) autoToss();
    var guard = 0;
    while (!finished && guard++ < 5000) {
      simulateBallAuto();
    }
  }

  Player playerById(int id) {
    for (final p in home.order) {
      if (p.id == id) return p;
    }
    return away.order.firstWhere((p) => p.id == id);
  }
}
