import 'dart:math' as math;

import '../core/rng.dart';
import '../engine/delivery.dart';
import '../engine/match_engine.dart';
import 'camera.dart';

/// 3D flight of a delivery from release to the batter (and beyond).
class DeliveryPath {
  DeliveryPath({required this.plan, required this.batterLeft, required double slowMo, required this.keeperY}) {
    final spin = plan.spin;
    final hand = batterLeft ? -1.0 : 1.0;
    release = Vec3(plan.leftArm ? 0.34 : -0.34, 1.3, spin ? 2.05 : 2.3);
    finalX = (plan.lineX ?? plan.line.legward * hand) + plan.swing + plan.turn;
    if (plan.wide) finalX = plan.line == BallLine.leg ? 0.95 * hand : -1.05 * hand;
    arriveY = 19.05;
    arriveZ = plan.type.arriveHeight;
    final v = math.max(15.0, plan.speed / 3.6);
    final f = slowMo * (spin ? 0.82 : 1.0);
    if (plan.bounces) {
      bounceY = (plan.pitchY ?? (kPitchLength - plan.type.pitchFromStumps)).clamp(5.0, 19.2);
      final aimX = finalX - plan.turn;
      final u = (bounceY - release.y) / (arriveY - release.y);
      bounceX = release.x + (aimX - release.x) * u;
      tBounce = (bounceY - release.y) / v * f;
      tArrive = tBounce + (arriveY - bounceY) / (v * 0.86) * f;
    } else {
      bounceY = arriveY;
      bounceX = finalX;
      tBounce = (arriveY - release.y) / v * f;
      tArrive = tBounce;
    }
    arc = spin ? 0.95 : 0.32;
    tAfter = 0.32 * f + 0.25;
  }

  final DeliveryPlan plan;
  final bool batterLeft;
  final double keeperY;
  late final Vec3 release;
  late final double finalX;
  late final double arriveY;
  late final double arriveZ;
  late final double bounceY;
  late final double bounceX;
  late final double tBounce;
  late final double tArrive;
  late final double tAfter;
  late final double arc;

  bool get bounces => plan.bounces;

  Vec3 at(double t) {
    if (t <= tBounce) {
      final u = (t / tBounce).clamp(0.0, 1.0);
      final x = release.x + (bounceX - release.x) * u - plan.swing * 0.45 * u * (1 - u);
      final y = release.y + (bounceY - release.y) * u;
      final endZ = bounces ? 0.0 : arriveZ;
      final z = release.z + (endZ - release.z) * u + arc * u * (1 - u) * (bounces ? 1 : 0.6);
      return Vec3(x, y, math.max(0, z));
    }
    if (t <= tArrive) {
      final v = ((t - tBounce) / (tArrive - tBounce)).clamp(0.0, 1.0);
      final x = bounceX + (finalX - bounceX) * v;
      final y = bounceY + (arriveY - bounceY) * v;
      final z = arriveZ * (2 * v - v * v);
      return Vec3(x, y, z);
    }
    // Past the batter, carrying to the keeper.
    final w = ((t - tArrive) / tAfter).clamp(0.0, 1.0);
    final x = finalX + (finalX - bounceX) * w * 0.35;
    final y = arriveY + (keeperY - arriveY) * w;
    final z = arriveZ + (0.75 - arriveZ) * w;
    return Vec3(x, y, z);
  }

  /// Position when the ball would hit the stumps (for bowled animations).
  double get tStumps => tArrive + 0.16;
}

/// Trajectory after the bat (world space).
class ShotPath {
  ShotPath({required this.from, required this.to, required this.apex, required this.duration, this.bounceAt, this.rollTo});

  final Vec3 from;
  final Vec3 to;
  final double apex;
  final double duration;

  /// Fraction of the flight at which the ball lands and starts rolling.
  final double? bounceAt;
  final Vec3? rollTo;

  Vec3 at(double t) {
    final u = (t / duration).clamp(0.0, 1.0);
    if (bounceAt != null && rollTo != null && u > bounceAt!) {
      final v = (u - bounceAt!) / (1 - bounceAt!);
      final e = 1 - (1 - v) * (1 - v);
      final hop = 0.9 * math.sin(v * math.pi * 2).abs() * (1 - v);
      return Vec3(to.x + (rollTo!.x - to.x) * e, to.y + (rollTo!.y - to.y) * e, hop);
    }
    final w = bounceAt == null ? u : u / bounceAt!;
    final x = from.x + (to.x - from.x) * w;
    final y = from.y + (to.y - from.y) * w;
    final base = from.z + (to.z - from.z) * w;
    final z = base + apex * 4 * w * (1 - w);
    return Vec3(x, y, math.max(0, z));
  }

  Vec3 get end => rollTo ?? to;
}

// ----------------------------------------------------------------- field

/// Fielding position by field angle (deg, + = off side) and distance from
/// the striker's stumps.
Vec3 fieldPos(double angleDeg, double dist, {required bool leftBatter}) {
  final a = angleDeg * math.pi / 180;
  final sx = leftBatter ? 1.0 : -1.0;
  return Vec3(sx * math.sin(a) * dist, kPitchLength - math.cos(a) * dist, 0);
}

class FieldSetting {
  FieldSetting(this.positions, this.keeper, this.slips);
  final List<Vec3> positions;
  final Vec3 keeper;
  final List<Vec3> slips;
}

FieldSetting fieldFor({required int phase, required bool spin, required bool leftBatter, double boundary = 66}) {
  final deep = boundary - 4;
  Vec3 p(double a, double d) => fieldPos(a, d, leftBatter: leftBatter);
  final keeper = spin ? p(180, 1.2) : p(180, 13.5);
  final slips = <Vec3>[];
  final list = <Vec3>[];
  if (phase == 0) {
    if (!spin) {
      slips.add(p(166, 15));
      list.add(slips.first);
    }
    list.addAll([p(95, 24), p(58, 26), p(20, 25), p(-22, 25), p(-60, 26), p(-95, 22), p(-152, deep), p(140, deep * 0.95)]);
    if (spin) list.add(p(-40, 18));
  } else if (phase == 1) {
    list.addAll([p(95, 24), p(56, 26), p(18, 25), p(-60, 26), p(-95, deep), p(-55, deep), p(-12, deep), p(12, deep), p(100, deep)]);
  } else {
    list.addAll([p(98, 24), p(55, 27), p(-62, 27), p(-140, 24), p(-12, deep), p(12, deep), p(-58, deep), p(-95, deep), p(100, deep)]);
  }
  return FieldSetting(list.take(9).toList(), keeper, slips);
}

int nearestFielder(List<Vec3> f, Vec3 target) {
  var best = 0;
  var bd = double.infinity;
  for (var i = 0; i < f.length; i++) {
    final dx = f[i].x - target.x, dy = f[i].y - target.y;
    final d = dx * dx + dy * dy;
    if (d < bd) {
      bd = d;
      best = i;
    }
  }
  return best;
}

/// Everything the aerial view needs to animate one shot.
class ShotPlan {
  ShotPlan({
    required this.path,
    required this.fielder,
    required this.fielderTarget,
    required this.runs,
    required this.caught,
    required this.six,
    required this.four,
    required this.runOut,
  });

  final ShotPath path;
  final int? fielder;
  final Vec3? fielderTarget;
  final int runs;
  final bool caught;
  final bool six;
  final bool four;
  final bool runOut;

  double get duration => path.duration;
}

ShotPlan planShot(BallResult r, FieldSetting field, {required bool leftBatter, required double boundary, required Rng rng}) {
  final a = (r.angle ?? 0) * math.pi / 180;
  final sx = leftBatter ? 1.0 : -1.0;
  final dir = Vec3(sx * math.sin(a), -math.cos(a), 0);
  final start = Vec3(leftBatter ? -0.2 : 0.2, 19.2, 0.7);
  Vec3 along(double d, [double z = 0]) => Vec3(start.x + dir.x * d, start.y + dir.y * d, z);
  // Elliptical boundary distance in this direction, measured from centre.
  final rope = boundary + 2;

  if (r.isSix) {
    final d = rope + rng.range(6, 24);
    return ShotPlan(
      path: ShotPath(from: start, to: along(d, 6), apex: rng.range(19, 31), duration: 2.3),
      fielder: null,
      fielderTarget: null,
      runs: 6,
      caught: false,
      six: true,
      four: false,
      runOut: false,
    );
  }
  if (r.isFour && !r.wicket) {
    final lofted = r.lofted || rng.chance(0.3);
    final d = rope + 1.5;
    final idx = nearestFielder(field.positions, along(d * 0.85));
    final path = lofted
        ? ShotPath(from: start, to: along(d * 0.72), apex: rng.range(6, 11), duration: 1.9, bounceAt: 0.62, rollTo: along(d))
        : ShotPath(from: start, to: along(d * 0.15), apex: 0.4, duration: 1.75, bounceAt: 0.08, rollTo: along(d));
    return ShotPlan(
      path: path,
      fielder: idx,
      fielderTarget: along(d * 0.93),
      runs: 4,
      caught: false,
      six: false,
      four: true,
      runOut: false,
    );
  }
  if (r.wicket && (r.dismissal == Dismissal.caught)) {
    final idx = nearestFielder(field.positions, along(rng.range(22, boundary - 2)));
    final f = field.positions[idx];
    final catchAt = Vec3(f.x + rng.range(-2.5, 2.5), f.y + rng.range(-2.5, 2.5), 1.4);
    final dist = math.sqrt(math.pow(catchAt.x - start.x, 2) + math.pow(catchAt.y - start.y, 2));
    return ShotPlan(
      path: ShotPath(from: start, to: catchAt, apex: dist > 35 ? rng.range(16, 26) : rng.range(6, 14), duration: 1.5 + dist / 60),
      fielder: idx,
      fielderTarget: catchAt.ground,
      runs: 0,
      caught: true,
      six: false,
      four: false,
      runOut: false,
    );
  }
  if (r.wicket && r.dismissal == Dismissal.runOut) {
    final d = rng.range(16, 30);
    final idx = nearestFielder(field.positions, along(d));
    final target = along(d);
    return ShotPlan(
      path: ShotPath(from: start, to: target, apex: 0.4, duration: 1.1),
      fielder: idx,
      fielderTarget: target,
      runs: r.runs,
      caught: false,
      six: false,
      four: false,
      runOut: true,
    );
  }
  final runs = r.runs + (r.extra == ExtraType.legBye || r.extra == ExtraType.bye ? r.extraRuns : 0);
  final d = switch (runs) {
    0 => r.defended ? rng.range(3, 9) : rng.range(12, 22),
    1 => rng.range(15, 28),
    2 => rng.range(38, 56),
    _ => rng.range(56, boundary - 4),
  };
  final target = along(d);
  final idx = nearestFielder(field.positions, target);
  final lofted = r.lofted && runs >= 2;
  return ShotPlan(
    path: ShotPath(
      from: start,
      to: lofted ? along(d * 0.8) : along(d * 0.2),
      apex: lofted ? rng.range(5, 9) : 0.35,
      duration: 0.9 + d / 30,
      bounceAt: lofted ? 0.7 : 0.15,
      rollTo: target,
    ),
    fielder: idx,
    fielderTarget: target,
    runs: runs,
    caught: false,
    six: false,
    four: false,
    runOut: false,
  );
}
