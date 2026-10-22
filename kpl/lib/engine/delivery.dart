import '../core/rng.dart';
import '../models/player.dart';

enum DeliveryType {
  yorker,
  wideYorker,
  full,
  goodLength,
  short,
  bouncer,
  slower,
  flighted,
  stock,
  quicker,
  variation,
  fullToss,
}

const kPaceDeliveries = [
  DeliveryType.yorker,
  DeliveryType.full,
  DeliveryType.goodLength,
  DeliveryType.short,
  DeliveryType.bouncer,
  DeliveryType.slower,
  DeliveryType.wideYorker,
];

const kSpinDeliveries = [
  DeliveryType.flighted,
  DeliveryType.stock,
  DeliveryType.quicker,
  DeliveryType.variation,
];

extension DeliveryTypeX on DeliveryType {
  String get label => const [
        'Yorker',
        'Wide yorker',
        'Full',
        'Good length',
        'Short',
        'Bouncer',
        'Slower ball',
        'Flighted',
        'Stock ball',
        'Quicker',
        'Variation',
        'Full toss',
      ][index];

  String get short => const [
        'YORKER',
        'WIDE YKR',
        'FULL',
        'GOOD',
        'SHORT',
        'BOUNCER',
        'SLOWER',
        'FLIGHT',
        'STOCK',
        'QUICKER',
        'MYSTERY',
        'FULL TOSS',
      ][index];

  bool get isSpinType => index >= DeliveryType.flighted.index && index <= DeliveryType.variation.index;

  /// Metres in front of the batter's stumps where the ball pitches.
  /// 0 means it does not bounce before reaching the batter (full toss).
  double get pitchFromStumps => const [
        1.3, 1.4, 3.2, 6.2, 8.4, 10.8, 5.2, 3.2, 4.8, 5.8, 4.6, 0.0,
      ][index];

  /// Height (m) of the ball when it reaches the batter.
  double get arriveHeight => const [
        0.1, 0.12, 0.38, 0.78, 1.15, 1.8, 0.62, 0.42, 0.6, 0.55, 0.6, 1.0,
      ][index];

  /// Length category used by the shot-suitability tables.
  int get lengthRow {
    switch (this) {
      case DeliveryType.yorker:
      case DeliveryType.wideYorker:
        return 0;
      case DeliveryType.full:
      case DeliveryType.flighted:
        return 1;
      case DeliveryType.goodLength:
      case DeliveryType.stock:
      case DeliveryType.variation:
      case DeliveryType.slower:
        return 2;
      case DeliveryType.short:
      case DeliveryType.quicker:
        return 3;
      case DeliveryType.bouncer:
        return 4;
      case DeliveryType.fullToss:
        return 5;
    }
  }
}

enum BallLine { wideOff, off, middle, leg }

extension BallLineX on BallLine {
  String get label => const ['Wide off', 'Off stump', 'Middle', 'Leg stump'][index];
  String get short => const ['WIDE OFF', 'OFF', 'MIDDLE', 'LEG'][index];

  /// Lateral offset (m) relative to middle stump towards the batter's leg side.
  double get legward => const [-0.78, -0.12, 0.0, 0.14][index];
}

/// A planned delivery - drives both the outcome model and the 3D animation.
class DeliveryPlan {
  DeliveryPlan({
    required this.type,
    required this.line,
    required this.speed,
    required this.swing,
    required this.turn,
    required this.accuracy,
    required this.leftArm,
    required this.spin,
    this.wide = false,
    this.noBall = false,
    this.pitchY,
    this.lineX,
  });

  /// Exact pitching point / lateral line (world metres) when the bowler aimed
  /// with the drag-and-drop target; null for AI deliveries.
  final double? pitchY;
  final double? lineX;

  final DeliveryType type;
  final BallLine line;

  /// km/h.
  final double speed;

  /// Lateral drift in the air (world metres, +x = right-hander's leg side).
  final double swing;

  /// Lateral deviation after pitching (world metres).
  final double turn;

  /// 0..1 execution accuracy.
  final double accuracy;
  final bool leftArm;
  final bool spin;
  final bool wide;
  final bool noBall;

  bool get bounces => type != DeliveryType.fullToss;

  String describe() {
    final parts = <String>['${speed.round()} km/h', type.label];
    if (!spin && swing.abs() > 0.12) parts.add(swing > 0 ? 'Inswing' : 'Outswing');
    if (spin && turn.abs() > 0.12) parts.add('Turning');
    if (wide) parts.add('WIDE');
    if (noBall) parts.add('NO BALL');
    return parts.join(' • ');
  }

  /// Would this delivery go on to hit the stumps (for bowled / LBW)?
  bool hitsStumps({required bool leftHandBatter}) {
    if (wide) return false;
    if (type == DeliveryType.bouncer || type == DeliveryType.short) return false;
    if (type == DeliveryType.wideYorker) return false;
    // Effective lateral position at the stumps, batter-relative (+ = leg).
    final dir = leftHandBatter ? -1.0 : 1.0;
    final lat = line.legward + (swing + turn) * dir;
    return lat > -0.16 && lat < 0.16;
  }
}

enum ShotDir { straight, cover, point, thirdMan, midWicket, squareLeg, fineLeg }

extension ShotDirX on ShotDir {
  String get label => const [
        'STRAIGHT',
        'COVER',
        'POINT',
        'THIRD MAN',
        'MID-WKT',
        'SQ LEG',
        'FINE LEG',
      ][index];

  /// Field angle in degrees: 0 = straight back past the bowler,
  /// +90 = square on the off side, -90 = square on the leg side.
  double get angle => const [0.0, 52.0, 95.0, 145.0, -52.0, -95.0, -150.0][index];

  bool get offSide => angle > 0;
}

enum TimingGrade { perfect, good, early, late, veryEarly, veryLate, miss }

extension TimingGradeX on TimingGrade {
  String get label => const [
        'PERFECT',
        'GOOD',
        'EARLY',
        'LATE',
        'TOO EARLY',
        'TOO LATE',
        'MISSED',
      ][index];
}

class ShotInput {
  const ShotInput({this.dir, this.loft = false, this.defend = false, this.timingMs});

  final ShotDir? dir;
  final bool loft;
  final bool defend;

  /// Signed timing error in ms (negative = early). Null when no shot played.
  final double? timingMs;

  bool get leave => dir == null && !defend;
}

class TimingWindows {
  const TimingWindows(this.perfect, this.good, this.ok, this.poor);
  final double perfect, good, ok, poor;
}

TimingGrade gradeTiming(double errMs, TimingWindows w) {
  final a = errMs.abs();
  if (a <= w.perfect) return TimingGrade.perfect;
  if (a <= w.good) return TimingGrade.good;
  if (a <= w.ok) return errMs < 0 ? TimingGrade.early : TimingGrade.late;
  if (a <= w.poor) return errMs < 0 ? TimingGrade.veryEarly : TimingGrade.veryLate;
  return TimingGrade.miss;
}

enum Difficulty { easy, normal, hard }

extension DifficultyX on Difficulty {
  String get label => const ['EASY', 'NORMAL', 'HARD'][index];

  /// Real ball flight time is multiplied by this so humans can react.
  double get slowMo => const [2.7, 2.2, 1.8][index];
  TimingWindows get windows => const [
        TimingWindows(75, 150, 240, 340),
        TimingWindows(58, 115, 195, 285),
        TimingWindows(42, 88, 150, 230),
      ][index];

  /// AI bowling accuracy offset.
  double get aiBowlBonus => const [-0.08, 0.0, 0.06][index];
}

/// Picks an AI delivery for [bowler] given the match phase (0 pp, 1 middle, 2 death).
DeliveryPlan planAiDelivery(Rng rng, Player bowler, int phase, {double spinFactor = 1.0, double accuracyBonus = 0}) {
  final spin = bowler.isSpin;
  DeliveryType type;
  if (spin) {
    final mystery = bowler.name.contains('Varun') ||
        bowler.name.contains('Narine') ||
        bowler.name.contains('Rashid') ||
        bowler.name.contains('Noor') ||
        bowler.name.contains('Digvesh') ||
        bowler.name.contains('Ghazanfar');
    type = rng.weighted({
      DeliveryType.stock: 0.4,
      DeliveryType.flighted: phase == 2 ? 0.1 : 0.22,
      DeliveryType.quicker: phase == 2 ? 0.34 : 0.22,
      DeliveryType.variation: mystery ? 0.26 : 0.12,
    });
  } else {
    switch (phase) {
      case 0:
        type = rng.weighted({
          DeliveryType.goodLength: 0.4,
          DeliveryType.full: 0.24,
          DeliveryType.short: 0.12,
          DeliveryType.bouncer: 0.06,
          DeliveryType.yorker: 0.07,
          DeliveryType.slower: 0.11,
        });
      case 1:
        type = rng.weighted({
          DeliveryType.goodLength: 0.34,
          DeliveryType.short: 0.16,
          DeliveryType.full: 0.14,
          DeliveryType.slower: 0.16,
          DeliveryType.bouncer: 0.08,
          DeliveryType.yorker: 0.12,
        });
      default:
        type = rng.weighted({
          DeliveryType.yorker: 0.34,
          DeliveryType.wideYorker: 0.15,
          DeliveryType.slower: 0.2,
          DeliveryType.full: 0.08,
          DeliveryType.goodLength: 0.1,
          DeliveryType.bouncer: 0.13,
        });
    }
  }
  var line = type == DeliveryType.wideYorker
      ? BallLine.wideOff
      : rng.weighted({
          BallLine.off: 0.46,
          BallLine.middle: 0.26,
          BallLine.wideOff: 0.14,
          BallLine.leg: 0.14,
        });
  final accuracy = (0.6 + bowler.bowl / 260 + accuracyBonus + rng.gauss() * 0.08).clamp(0.2, 1.0);
  // Execution errors: missed yorkers become full tosses etc.
  if (accuracy < 0.55) {
    if (type == DeliveryType.yorker || type == DeliveryType.wideYorker || type == DeliveryType.slower) {
      type = rng.chance(0.6) ? DeliveryType.fullToss : DeliveryType.full;
    } else if (type == DeliveryType.goodLength || type == DeliveryType.stock) {
      type = rng.chance(0.5) ? DeliveryType.full : DeliveryType.short;
    }
  }
  final wide = rng.chance(type == DeliveryType.wideYorker ? 0.12 : (spin ? 0.015 : 0.03));
  if (wide && line != BallLine.wideOff) line = rng.chance(0.5) ? BallLine.wideOff : BallLine.leg;
  final noBall = !spin && rng.chance(0.006);
  return _build(rng, bowler, type, line, accuracy, wide, noBall, phase, spinFactor);
}

/// Builds the user's chosen delivery with the accuracy from the meter.
/// [aimY]/[aimX] are the dropped target on the pitch; [swing] forces swing.
DeliveryPlan planUserDelivery(
  Rng rng,
  Player bowler,
  DeliveryType type,
  BallLine line,
  double accuracy,
  int phase, {
  double spinFactor = 1.0,
  double? aimY,
  double? aimX,
  double? swing,
}) {
  var t = type;
  var l = line;
  var wide = false;
  var noBall = false;
  if (accuracy < 0.15) {
    if (rng.chance(0.55)) {
      wide = true;
      l = rng.chance(0.5) ? BallLine.wideOff : BallLine.leg;
    } else {
      t = DeliveryType.fullToss;
      noBall = !bowler.isSpin && rng.chance(0.4);
    }
  } else if (accuracy < 0.4) {
    if (t == DeliveryType.yorker || t == DeliveryType.wideYorker) {
      t = rng.chance(0.55) ? DeliveryType.fullToss : DeliveryType.full;
    } else if (t == DeliveryType.bouncer) {
      if (rng.chance(0.35)) {
        wide = true;
      } else {
        t = DeliveryType.short;
      }
    } else if (t == DeliveryType.goodLength || t == DeliveryType.stock || t == DeliveryType.slower) {
      t = rng.chance(0.5) ? DeliveryType.full : DeliveryType.short;
    } else if (t == DeliveryType.flighted) {
      t = DeliveryType.fullToss;
    }
  } else if (t == DeliveryType.wideYorker && accuracy < 0.6 && rng.chance(0.3)) {
    wide = true;
  }
  final built = _build(rng, bowler, t, l, accuracy, wide, noBall, phase, spinFactor);
  if (aimY == null && swing == null) return built;
  // Execution error scatters the ball around the dropped target.
  final err = 1 - accuracy;
  final py = aimY == null || t == DeliveryType.fullToss ? null : aimY + rng.gauss() * err * 1.1;
  final px = aimX == null || wide ? null : aimX + rng.gauss() * err * 0.22;
  return DeliveryPlan(
    type: built.type,
    line: built.line,
    speed: built.speed,
    swing: swing ?? built.swing,
    turn: built.turn,
    accuracy: accuracy,
    leftArm: built.leftArm,
    spin: built.spin,
    wide: built.wide,
    noBall: built.noBall,
    pitchY: py,
    lineX: px,
  );
}

DeliveryPlan _build(Rng rng, Player bowler, DeliveryType type, BallLine line, double accuracy, bool wide, bool noBall, int phase, double spinFactor) {
  final spin = bowler.isSpin;
  var speed = bowler.bowlStyle.baseSpeed + rng.gauss() * 3;
  if (type == DeliveryType.slower) speed *= 0.8;
  if (type == DeliveryType.quicker) speed *= 1.08;
  if (type == DeliveryType.flighted) speed *= 0.92;
  if (type == DeliveryType.bouncer) speed += 3;
  var swing = 0.0;
  var turn = 0.0;
  if (spin) {
    var t = bowler.bowlStyle.stockTurn * rng.range(0.12, 0.42) * spinFactor;
    if (type == DeliveryType.variation) t = -t * 0.9;
    if (type == DeliveryType.quicker) t *= 0.45;
    if (type == DeliveryType.flighted) t *= 1.2;
    turn = t;
  } else {
    final swingAmt = phase == 0 ? rng.range(0.05, 0.32) : rng.range(0.0, 0.14);
    swing = (rng.chance(0.5) ? 1 : -1) * swingAmt;
    turn = rng.gauss() * 0.05; // seam
  }
  return DeliveryPlan(
    type: type,
    line: line,
    speed: speed,
    swing: swing,
    turn: turn,
    accuracy: accuracy,
    leftArm: bowler.bowlStyle.leftArm,
    spin: spin,
    wide: wide,
    noBall: noBall,
  );
}
