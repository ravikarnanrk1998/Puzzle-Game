import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../core/theme.dart';
import 'camera.dart';

// ===================================================================== pose
//
// Character-local space: x = the player's right, y = the way they face,
// z = up, origin on the ground between the feet (metres, 1.8 m athlete).
// Poses describe the pelvis/spine/head orientation plus hand and foot
// targets; arms and legs are solved with two-bone IK, so every pose keeps
// correct limb lengths and blends naturally.

enum BatHold { none, both, right }

class Pose {
  const Pose({
    this.hip = const Vec3(0, 0, 0.96),
    this.pelvisYaw = 0,
    this.pelvisLean = 0,
    this.pelvisSide = 0,
    this.spineYaw = 0,
    this.spineLean = 0,
    this.spineSide = 0,
    this.headYaw = 0,
    this.headPitch = 0,
    this.lHand = const Vec3(-0.25, 0.04, 0.84),
    this.rHand = const Vec3(0.25, 0.04, 0.84),
    this.lElbow = const Vec3(-0.5, -1, -0.3),
    this.rElbow = const Vec3(0.5, -1, -0.3),
    this.lFoot = const Vec3(-0.12, 0, 0.08),
    this.rFoot = const Vec3(0.12, 0, 0.08),
    this.lToe = const Vec3(-0.12, 1, 0),
    this.rToe = const Vec3(0.12, 1, 0),
    this.lKnee = const Vec3(-0.1, 1, 0),
    this.rKnee = const Vec3(0.1, 1, 0),
    this.hold = BatHold.none,
    this.grip = const Vec3(0, 0.26, 0.84),
    this.bat = const Vec3(0.05, 0.12, -1),
    this.topLeft = true,
  });

  final Vec3 hip;
  final double pelvisYaw, pelvisLean, pelvisSide;
  final double spineYaw, spineLean, spineSide;
  final double headYaw, headPitch;
  final Vec3 lHand, rHand, lElbow, rElbow;
  final Vec3 lFoot, rFoot, lToe, rToe, lKnee, rKnee;
  final BatHold hold;

  /// Top hand position on the handle (two-handed) and bat direction.
  final Vec3 grip;
  final Vec3 bat;

  /// Which hand is at the top of the handle (left for right-handers).
  final bool topLeft;

  Pose copyWith({Vec3? hip, BatHold? hold, Vec3? bat, Vec3? rHand, Vec3? lHand, double? headYaw, double? headPitch}) => Pose(
        hip: hip ?? this.hip,
        pelvisYaw: pelvisYaw,
        pelvisLean: pelvisLean,
        pelvisSide: pelvisSide,
        spineYaw: spineYaw,
        spineLean: spineLean,
        spineSide: spineSide,
        headYaw: headYaw ?? this.headYaw,
        headPitch: headPitch ?? this.headPitch,
        lHand: lHand ?? this.lHand,
        rHand: rHand ?? this.rHand,
        lElbow: lElbow,
        rElbow: rElbow,
        lFoot: lFoot,
        rFoot: rFoot,
        lToe: lToe,
        rToe: rToe,
        lKnee: lKnee,
        rKnee: rKnee,
        hold: hold ?? this.hold,
        grip: grip,
        bat: bat ?? this.bat,
        topLeft: topLeft,
      );

  static double _l(double a, double b, double t) => a + (b - a) * t;
  static Vec3 _v(Vec3 a, Vec3 b, double t) => Vec3.lerp(a, b, t);

  static Pose lerp(Pose a, Pose b, double t) {
    if (t <= 0) return a;
    if (t >= 1) return b;
    final hold = a.hold == b.hold ? a.hold : (t < 0.5 ? a.hold : b.hold);
    return Pose(
      hip: _v(a.hip, b.hip, t),
      pelvisYaw: _l(a.pelvisYaw, b.pelvisYaw, t),
      pelvisLean: _l(a.pelvisLean, b.pelvisLean, t),
      pelvisSide: _l(a.pelvisSide, b.pelvisSide, t),
      spineYaw: _l(a.spineYaw, b.spineYaw, t),
      spineLean: _l(a.spineLean, b.spineLean, t),
      spineSide: _l(a.spineSide, b.spineSide, t),
      headYaw: _l(a.headYaw, b.headYaw, t),
      headPitch: _l(a.headPitch, b.headPitch, t),
      lHand: _v(a.lHand, b.lHand, t),
      rHand: _v(a.rHand, b.rHand, t),
      lElbow: _v(a.lElbow, b.lElbow, t),
      rElbow: _v(a.rElbow, b.rElbow, t),
      lFoot: _v(a.lFoot, b.lFoot, t),
      rFoot: _v(a.rFoot, b.rFoot, t),
      lToe: _v(a.lToe, b.lToe, t),
      rToe: _v(a.rToe, b.rToe, t),
      lKnee: _v(a.lKnee, b.lKnee, t),
      rKnee: _v(a.rKnee, b.rKnee, t),
      hold: hold,
      grip: _v(a.grip, b.grip, t),
      bat: _v(a.bat.normalized, b.bat.normalized, t),
      topLeft: t < 0.5 ? a.topLeft : b.topLeft,
    );
  }

  static Vec3 _m(Vec3 v) => Vec3(-v.x, v.y, v.z);

  /// Left-right mirror (left-handed batters, left-arm bowlers).
  Pose mirrored() => Pose(
        hip: _m(hip),
        pelvisYaw: -pelvisYaw,
        pelvisLean: pelvisLean,
        pelvisSide: -pelvisSide,
        spineYaw: -spineYaw,
        spineLean: spineLean,
        spineSide: -spineSide,
        headYaw: -headYaw,
        headPitch: headPitch,
        lHand: _m(rHand),
        rHand: _m(lHand),
        lElbow: _m(rElbow),
        rElbow: _m(lElbow),
        lFoot: _m(rFoot),
        rFoot: _m(lFoot),
        lToe: _m(rToe),
        rToe: _m(lToe),
        lKnee: _m(rKnee),
        rKnee: _m(lKnee),
        hold: hold,
        grip: _m(grip),
        bat: _m(bat),
        topLeft: !topLeft,
      );

  Pose lifted(double dz) => copyWith(hip: Vec3(hip.x, hip.y, hip.z + dz));
}

/// Plays a list of (time, pose) keyframes with smooth interpolation.
Pose samplePoses(List<(double, Pose)> keys, double t) {
  if (t <= keys.first.$1) return keys.first.$2;
  for (var i = 0; i < keys.length - 1; i++) {
    final a = keys[i], b = keys[i + 1];
    if (t <= b.$1) {
      final u = (t - a.$1) / math.max(1e-6, b.$1 - a.$1);
      final e = u * u * (3 - 2 * u);
      return Pose.lerp(a.$2, b.$2, e);
    }
  }
  return keys.last.$2;
}

// --------------------------------------------------------------- batting
// Right-handed batter. Local +y faces the off side, local -x points at the
// bowler, so the left shoulder leads.

const _bElbL = Vec3(-0.6, 0.5, -0.4);
const _bElbR = Vec3(0.5, 0.3, -0.8);

const pBatStance = Pose(
  hip: Vec3(0.0, -0.02, 0.89),
  pelvisLean: 0.12,
  spineLean: 0.22,
  spineYaw: 0.12,
  headYaw: 1.25,
  headPitch: 0.18,
  lFoot: Vec3(-0.24, 0.03, 0.08),
  rFoot: Vec3(0.22, 0.0, 0.08),
  lToe: Vec3(-0.15, 1, 0),
  rToe: Vec3(0.25, 1, 0),
  lKnee: Vec3(-0.3, 1, 0),
  rKnee: Vec3(0.3, 1, 0),
  hold: BatHold.both,
  grip: Vec3(0.04, 0.27, 0.84),
  bat: Vec3(0.08, 0.1, -1),
  lElbow: _bElbL,
  rElbow: _bElbR,
);

const pBatBacklift = Pose(
  hip: Vec3(0.02, -0.02, 0.87),
  pelvisLean: 0.12,
  spineLean: 0.2,
  spineYaw: -0.12,
  headYaw: 1.3,
  headPitch: 0.15,
  lFoot: Vec3(-0.27, 0.04, 0.08),
  rFoot: Vec3(0.22, 0.0, 0.08),
  lToe: Vec3(-0.15, 1, 0),
  rToe: Vec3(0.25, 1, 0),
  lKnee: Vec3(-0.3, 1, 0),
  rKnee: Vec3(0.3, 1, 0),
  hold: BatHold.both,
  grip: Vec3(0.2, 0.14, 1.1),
  bat: Vec3(0.55, -0.25, 0.8),
  lElbow: Vec3(-0.2, 0.6, -0.5),
  rElbow: Vec3(0.8, 0.2, -0.6),
);

const pDriveContact = Pose(
  hip: Vec3(-0.3, 0.02, 0.8),
  pelvisYaw: 0.2,
  pelvisLean: 0.18,
  pelvisSide: -0.12,
  spineLean: 0.38,
  spineYaw: 0.15,
  spineSide: -0.18,
  headYaw: 1.35,
  headPitch: 0.45,
  lFoot: Vec3(-0.68, 0.1, 0.08),
  rFoot: Vec3(0.24, -0.02, 0.1),
  lToe: Vec3(-0.6, 0.8, 0),
  rToe: Vec3(0.35, 1, -0.2),
  lKnee: Vec3(-1, 0.5, 0),
  rKnee: Vec3(0.2, 1, -0.3),
  hold: BatHold.both,
  grip: Vec3(-0.42, 0.25, 0.74),
  bat: Vec3(-0.3, 0.12, -0.95),
  lElbow: Vec3(-0.5, 0.8, 0.0),
  rElbow: Vec3(0.3, 0.6, -0.6),
);

const pDriveFollow = Pose(
  hip: Vec3(-0.3, 0.02, 0.82),
  pelvisYaw: 0.5,
  pelvisLean: 0.12,
  pelvisSide: -0.1,
  spineLean: 0.18,
  spineYaw: 0.4,
  spineSide: -0.1,
  headYaw: 1.0,
  headPitch: 0.1,
  lFoot: Vec3(-0.68, 0.1, 0.08),
  rFoot: Vec3(0.22, -0.02, 0.12),
  lToe: Vec3(-0.6, 0.8, 0),
  rToe: Vec3(0.35, 1, -0.3),
  lKnee: Vec3(-1, 0.5, 0),
  rKnee: Vec3(0.2, 1, -0.3),
  hold: BatHold.both,
  grip: Vec3(-0.32, 0.05, 1.55),
  bat: Vec3(0.35, -0.45, 0.82),
  lElbow: Vec3(-0.8, 0.3, 0.4),
  rElbow: Vec3(0.2, 0.9, 0.0),
);

const pLoftFollow = Pose(
  hip: Vec3(-0.2, 0.0, 0.86),
  pelvisYaw: 0.55,
  pelvisLean: 0.02,
  spineLean: -0.12,
  spineYaw: 0.35,
  headYaw: 0.9,
  headPitch: -0.35,
  lFoot: Vec3(-0.55, 0.1, 0.08),
  rFoot: Vec3(0.24, -0.02, 0.12),
  lToe: Vec3(-0.6, 0.8, 0),
  rToe: Vec3(0.35, 1, -0.3),
  lKnee: Vec3(-1, 0.5, 0),
  rKnee: Vec3(0.2, 1, -0.3),
  hold: BatHold.both,
  grip: Vec3(-0.15, 0.12, 1.78),
  bat: Vec3(0.3, -0.25, 0.92),
  lElbow: Vec3(-0.8, 0.2, 0.5),
  rElbow: Vec3(0.6, 0.6, 0.3),
);

const pPullContact = Pose(
  hip: Vec3(0.12, -0.02, 0.88),
  pelvisYaw: 0.75,
  pelvisLean: 0.05,
  spineYaw: 0.5,
  spineLean: 0.05,
  spineSide: 0.12,
  headYaw: 0.55,
  headPitch: 0.05,
  lFoot: Vec3(-0.26, 0.14, 0.08),
  rFoot: Vec3(0.26, -0.08, 0.08),
  lToe: Vec3(-0.8, 0.6, 0),
  rToe: Vec3(0.2, 1, 0),
  lKnee: Vec3(-0.8, 0.6, 0),
  rKnee: Vec3(0.2, 1, 0),
  hold: BatHold.both,
  grip: Vec3(-0.05, 0.32, 1.18),
  bat: Vec3(-0.5, 0.85, 0.12),
  lElbow: Vec3(-0.3, 0.2, -1),
  rElbow: Vec3(0.6, 0.2, -0.8),
);

const pPullFollow = Pose(
  hip: Vec3(0.12, -0.02, 0.88),
  pelvisYaw: 1.25,
  pelvisLean: 0.05,
  spineYaw: 0.75,
  spineSide: 0.1,
  headYaw: 0.2,
  headPitch: 0.0,
  lFoot: Vec3(-0.26, 0.14, 0.1),
  rFoot: Vec3(0.26, -0.08, 0.08),
  lToe: Vec3(-0.8, 0.4, 0),
  rToe: Vec3(-0.2, 1, 0),
  lKnee: Vec3(-0.8, 0.4, 0),
  rKnee: Vec3(-0.2, 1, 0),
  hold: BatHold.both,
  grip: Vec3(-0.25, -0.12, 1.42),
  bat: Vec3(0.45, -0.85, 0.3),
  lElbow: Vec3(-0.9, 0.0, 0.2),
  rElbow: Vec3(0.2, 0.6, -0.6),
);

const pCutContact = Pose(
  hip: Vec3(0.08, 0.05, 0.86),
  pelvisYaw: -0.1,
  pelvisLean: 0.15,
  spineLean: 0.25,
  spineYaw: -0.15,
  spineSide: 0.08,
  headYaw: 0.9,
  headPitch: 0.35,
  lFoot: Vec3(-0.22, 0.05, 0.08),
  rFoot: Vec3(0.18, 0.32, 0.08),
  lToe: Vec3(-0.2, 1, 0),
  rToe: Vec3(-0.3, 1, 0),
  lKnee: Vec3(-0.3, 1, 0),
  rKnee: Vec3(0.0, 1, 0),
  hold: BatHold.both,
  grip: Vec3(-0.02, 0.42, 1.08),
  bat: Vec3(-0.25, 0.85, -0.45),
  lElbow: Vec3(-0.7, 0.2, -0.5),
  rElbow: Vec3(0.5, 0.3, -0.8),
);

const pCutFollow = Pose(
  hip: Vec3(0.08, 0.05, 0.86),
  pelvisYaw: 0.05,
  pelvisLean: 0.15,
  spineLean: 0.25,
  spineYaw: 0.15,
  spineSide: 0.05,
  headYaw: 0.8,
  headPitch: 0.3,
  lFoot: Vec3(-0.22, 0.05, 0.08),
  rFoot: Vec3(0.18, 0.32, 0.08),
  lToe: Vec3(-0.2, 1, 0),
  rToe: Vec3(-0.3, 1, 0),
  lKnee: Vec3(-0.3, 1, 0),
  rKnee: Vec3(0.0, 1, 0),
  hold: BatHold.both,
  grip: Vec3(-0.28, 0.36, 0.92),
  bat: Vec3(-0.75, 0.35, -0.55),
  lElbow: Vec3(-0.8, 0.0, -0.4),
  rElbow: Vec3(0.3, 0.6, -0.7),
);

const pDefend = Pose(
  hip: Vec3(-0.22, 0.02, 0.84),
  pelvisYaw: 0.1,
  pelvisLean: 0.18,
  pelvisSide: -0.08,
  spineLean: 0.42,
  spineYaw: 0.1,
  spineSide: -0.15,
  headYaw: 1.4,
  headPitch: 0.5,
  lFoot: Vec3(-0.52, 0.1, 0.08),
  rFoot: Vec3(0.22, 0.0, 0.08),
  lToe: Vec3(-0.5, 0.9, 0),
  rToe: Vec3(0.3, 1, 0),
  lKnee: Vec3(-1, 0.5, 0),
  rKnee: Vec3(0.3, 1, 0),
  hold: BatHold.both,
  grip: Vec3(-0.36, 0.3, 0.82),
  bat: Vec3(-0.22, 0.05, -0.97),
  lElbow: Vec3(-0.4, 0.9, 0.2),
  rElbow: Vec3(0.4, 0.6, -0.5),
);

const pSweep = Pose(
  hip: Vec3(-0.02, 0.05, 0.56),
  pelvisYaw: 0.25,
  pelvisLean: 0.25,
  spineLean: 0.45,
  spineYaw: 0.2,
  spineSide: -0.1,
  headYaw: 1.3,
  headPitch: 0.45,
  lFoot: Vec3(-0.6, 0.18, 0.08),
  rFoot: Vec3(0.38, -0.28, 0.12),
  lToe: Vec3(-0.7, 0.7, 0),
  rToe: Vec3(0.3, -0.4, -0.85),
  lKnee: Vec3(-1, 0.6, 0.3),
  rKnee: Vec3(0.0, 1, -1.5),
  hold: BatHold.both,
  grip: Vec3(-0.25, 0.48, 0.55),
  bat: Vec3(-0.85, 0.4, -0.3),
  lElbow: Vec3(-0.6, 0.3, -0.6),
  rElbow: Vec3(0.5, 0.5, -0.6),
);

const pLeave = Pose(
  hip: Vec3(0.0, -0.02, 0.9),
  pelvisLean: 0.08,
  spineLean: 0.1,
  spineYaw: -0.1,
  headYaw: 1.2,
  headPitch: 0.1,
  lFoot: Vec3(-0.26, 0.04, 0.08),
  rFoot: Vec3(0.22, 0.0, 0.08),
  lToe: Vec3(-0.15, 1, 0),
  rToe: Vec3(0.25, 1, 0),
  hold: BatHold.both,
  grip: Vec3(0.05, 0.12, 1.55),
  bat: Vec3(0.35, -0.3, 0.88),
  lElbow: Vec3(-0.6, 0.4, 0.2),
  rElbow: Vec3(0.8, 0.3, -0.2),
);

// --------------------------------------------------------------- bowling
// Right-arm bowler running towards +y.

const pRunA = Pose(
  hip: Vec3(0, 0, 0.94),
  pelvisLean: 0.12,
  spineLean: 0.12,
  spineYaw: 0.12,
  lFoot: Vec3(-0.1, 0.42, 0.14),
  rFoot: Vec3(0.11, -0.42, 0.3),
  lToe: Vec3(0, 1, 0.1),
  rToe: Vec3(0, 0.5, -0.86),
  lKnee: Vec3(0, 1, 0.2),
  rKnee: Vec3(0, 1, -0.3),
  lHand: Vec3(-0.24, -0.26, 0.98),
  rHand: Vec3(0.2, 0.32, 1.12),
  lElbow: Vec3(-0.3, -1, 0.2),
  rElbow: Vec3(0.3, -1, -0.6),
);

const pRunB = Pose(
  hip: Vec3(0, 0, 0.94),
  pelvisLean: 0.12,
  spineLean: 0.12,
  spineYaw: -0.12,
  lFoot: Vec3(-0.11, -0.42, 0.3),
  rFoot: Vec3(0.1, 0.42, 0.14),
  lToe: Vec3(0, 0.5, -0.86),
  rToe: Vec3(0, 1, 0.1),
  lKnee: Vec3(0, 1, -0.3),
  rKnee: Vec3(0, 1, 0.2),
  lHand: Vec3(-0.2, 0.32, 1.12),
  rHand: Vec3(0.24, -0.26, 0.98),
  lElbow: Vec3(-0.3, -1, -0.6),
  rElbow: Vec3(0.3, -1, 0.2),
);

const pGather = Pose(
  hip: Vec3(0, 0.05, 1.18),
  pelvisYaw: -0.9,
  pelvisLean: -0.05,
  spineYaw: -0.4,
  spineLean: -0.08,
  headYaw: 1.1,
  headPitch: 0.05,
  lFoot: Vec3(-0.08, 0.3, 0.5),
  rFoot: Vec3(0.1, -0.2, 0.36),
  lToe: Vec3(0, 1, -0.3),
  rToe: Vec3(0.4, 0.4, -0.8),
  lKnee: Vec3(-0.2, 1, 0.5),
  rKnee: Vec3(0, 1, -0.2),
  lHand: Vec3(-0.12, 0.5, 2.15),
  rHand: Vec3(0.2, -0.05, 1.3),
  lElbow: Vec3(-0.6, 0.2, 0.3),
  rElbow: Vec3(0.8, -0.3, -0.5),
);

const pLoad = Pose(
  hip: Vec3(0, 0.0, 0.92),
  pelvisYaw: -1.2,
  pelvisLean: -0.1,
  spineYaw: -0.3,
  spineLean: -0.22,
  spineSide: 0.12,
  headYaw: 1.35,
  headPitch: 0.0,
  lFoot: Vec3(-0.05, 0.62, 0.32),
  rFoot: Vec3(0.06, -0.1, 0.08),
  lToe: Vec3(0, 1, 0.1),
  rToe: Vec3(1, 0.2, 0),
  lKnee: Vec3(0, 1, 0.4),
  rKnee: Vec3(0.6, 0.6, 0),
  lHand: Vec3(-0.08, 0.72, 1.95),
  rHand: Vec3(0.12, -0.62, 1.15),
  lElbow: Vec3(-0.8, 0.2, 0.2),
  rElbow: Vec3(0.6, -0.2, -0.8),
);

const pRelease = Pose(
  hip: Vec3(0, 0.38, 0.9),
  pelvisYaw: -0.25,
  pelvisLean: 0.15,
  spineYaw: 0.05,
  spineLean: 0.25,
  spineSide: -0.12,
  headYaw: 0.1,
  headPitch: 0.1,
  lFoot: Vec3(-0.08, 0.88, 0.08),
  rFoot: Vec3(0.1, -0.3, 0.2),
  lToe: Vec3(0, 1, 0),
  rToe: Vec3(0.6, 0.5, -0.6),
  lKnee: Vec3(0, 1, 0.1),
  rKnee: Vec3(0.3, 1, -0.3),
  lHand: Vec3(-0.34, 0.0, 1.0),
  rHand: Vec3(0.1, 0.42, 2.28),
  lElbow: Vec3(-1, -0.4, -0.2),
  rElbow: Vec3(0.6, -0.5, 0.0),
);

const pFollow = Pose(
  hip: Vec3(-0.02, 0.72, 0.88),
  pelvisYaw: 0.4,
  pelvisLean: 0.22,
  spineYaw: 0.3,
  spineLean: 0.42,
  spineSide: -0.08,
  headYaw: -0.2,
  headPitch: -0.25,
  lFoot: Vec3(-0.08, 0.95, 0.08),
  rFoot: Vec3(0.12, 0.5, 0.32),
  lToe: Vec3(0, 1, 0),
  rToe: Vec3(0, 1, -0.5),
  lKnee: Vec3(0, 1, 0.1),
  rKnee: Vec3(0, 1, 0.3),
  lHand: Vec3(-0.38, 0.35, 1.05),
  rHand: Vec3(-0.3, 1.08, 0.7),
  lElbow: Vec3(-1, -0.5, 0.0),
  rElbow: Vec3(0.6, 0.2, 0.4),
);

// -------------------------------------------------------- fielding etc.

const pKeeperCrouch = Pose(
  hip: Vec3(0, -0.08, 0.5),
  pelvisLean: 0.35,
  spineLean: 0.45,
  headPitch: -0.35,
  lFoot: Vec3(-0.3, 0.06, 0.08),
  rFoot: Vec3(0.3, 0.06, 0.08),
  lToe: Vec3(-0.35, 1, 0),
  rToe: Vec3(0.35, 1, 0),
  lKnee: Vec3(-0.8, 1, 0),
  rKnee: Vec3(0.8, 1, 0),
  lHand: Vec3(-0.07, 0.42, 0.42),
  rHand: Vec3(0.07, 0.42, 0.42),
  lElbow: Vec3(-1, 0.2, -0.3),
  rElbow: Vec3(1, 0.2, -0.3),
);

const pKeeperUp = Pose(
  hip: Vec3(0, -0.02, 0.86),
  pelvisLean: 0.15,
  spineLean: 0.2,
  lFoot: Vec3(-0.28, 0.06, 0.08),
  rFoot: Vec3(0.28, 0.06, 0.08),
  lToe: Vec3(-0.35, 1, 0),
  rToe: Vec3(0.35, 1, 0),
  lKnee: Vec3(-0.6, 1, 0),
  rKnee: Vec3(0.6, 1, 0),
  lHand: Vec3(-0.09, 0.42, 1.22),
  rHand: Vec3(0.09, 0.42, 1.22),
  lElbow: Vec3(-1, 0, -0.5),
  rElbow: Vec3(1, 0, -0.5),
);

const pSlip = Pose(
  hip: Vec3(0, -0.04, 0.72),
  pelvisLean: 0.3,
  spineLean: 0.4,
  headPitch: -0.3,
  lFoot: Vec3(-0.28, 0.04, 0.08),
  rFoot: Vec3(0.28, 0.04, 0.08),
  lToe: Vec3(-0.25, 1, 0),
  rToe: Vec3(0.25, 1, 0),
  lKnee: Vec3(-0.5, 1, 0),
  rKnee: Vec3(0.5, 1, 0),
  lHand: Vec3(-0.12, 0.46, 0.6),
  rHand: Vec3(0.12, 0.46, 0.6),
  lElbow: Vec3(-1, 0, -0.4),
  rElbow: Vec3(1, 0, -0.4),
);

const pFieldReady = Pose(
  hip: Vec3(0, -0.02, 0.86),
  pelvisLean: 0.18,
  spineLean: 0.28,
  headPitch: -0.2,
  lFoot: Vec3(-0.22, 0.04, 0.08),
  rFoot: Vec3(0.22, 0.04, 0.08),
  lToe: Vec3(-0.2, 1, 0),
  rToe: Vec3(0.2, 1, 0),
  lKnee: Vec3(-0.4, 1, 0),
  rKnee: Vec3(0.4, 1, 0),
  lHand: Vec3(-0.17, 0.38, 0.66),
  rHand: Vec3(0.17, 0.38, 0.66),
  lElbow: Vec3(-0.6, -0.6, 0),
  rElbow: Vec3(0.6, -0.6, 0),
);

const pCatch = Pose(
  hip: Vec3(0, 0, 0.95),
  spineLean: -0.08,
  headPitch: -0.5,
  lFoot: Vec3(-0.16, 0.04, 0.08),
  rFoot: Vec3(0.16, 0.0, 0.08),
  lHand: Vec3(-0.08, 0.28, 2.0),
  rHand: Vec3(0.08, 0.28, 2.0),
  lElbow: Vec3(-1, 0, 0),
  rElbow: Vec3(1, 0, 0),
);

const pThrow = Pose(
  hip: Vec3(0, 0, 0.9),
  pelvisYaw: -0.9,
  spineYaw: -0.4,
  spineLean: -0.05,
  headYaw: 1.0,
  lFoot: Vec3(-0.12, 0.45, 0.08),
  rFoot: Vec3(0.15, -0.38, 0.08),
  lToe: Vec3(0, 1, 0),
  rToe: Vec3(1, 0.3, 0),
  lKnee: Vec3(0, 1, 0),
  rKnee: Vec3(0.5, 0.6, 0),
  lHand: Vec3(-0.15, 0.85, 1.5),
  rHand: Vec3(0.35, -0.55, 1.75),
  lElbow: Vec3(-0.6, 0.2, -0.6),
  rElbow: Vec3(0.6, -0.4, -0.2),
);

const pStand = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.13, 0.02, 0.08),
  rFoot: Vec3(0.13, 0.0, 0.08),
  lHand: Vec3(-0.24, 0.06, 0.84),
  rHand: Vec3(0.24, 0.06, 0.84),
);

/// Non-striker: bat grounded in the right hand.
const pNonStriker = Pose(
  hip: Vec3(0, 0, 0.95),
  spineLean: 0.05,
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.04, 0.08),
  lHand: Vec3(-0.24, 0.05, 0.86),
  rHand: Vec3(0.26, 0.24, 0.88),
  rElbow: Vec3(0.6, -0.6, -0.4),
  hold: BatHold.right,
  bat: Vec3(0.1, 0.38, -0.92),
);

const pUmpire = Pose(
  hip: Vec3(0, 0, 0.96),
  spineLean: 0.08,
  headPitch: 0.05,
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.02, 0.08),
  lHand: Vec3(-0.06, -0.2, 0.96),
  rHand: Vec3(0.06, -0.2, 0.98),
  lElbow: Vec3(-1, -0.3, -0.2),
  rElbow: Vec3(1, -0.3, -0.2),
);

const pUmpOut = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.02, 0.08),
  lHand: Vec3(-0.24, 0.06, 0.84),
  rHand: Vec3(0.16, 0.2, 2.18),
  rElbow: Vec3(1, 0, 0),
);

const pUmpSix = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.02, 0.08),
  lHand: Vec3(-0.3, 0.08, 2.15),
  rHand: Vec3(0.3, 0.08, 2.15),
  lElbow: Vec3(-1, 0, 0),
  rElbow: Vec3(1, 0, 0),
);

const pUmpFour = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.02, 0.08),
  lHand: Vec3(-0.24, 0.06, 0.84),
  rHand: Vec3(0.78, 0.25, 1.32),
  rElbow: Vec3(0.3, -1, 0),
);

const pUmpWide = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.16, 0.02, 0.08),
  rFoot: Vec3(0.16, 0.02, 0.08),
  lHand: Vec3(-0.8, 0.05, 1.42),
  rHand: Vec3(0.8, 0.05, 1.42),
  lElbow: Vec3(0, -1, 0),
  rElbow: Vec3(0, -1, 0),
);

const pUmpNoBall = Pose(
  hip: Vec3(0, 0, 0.96),
  lFoot: Vec3(-0.14, 0.02, 0.08),
  rFoot: Vec3(0.14, 0.02, 0.08),
  lHand: Vec3(-0.24, 0.06, 0.84),
  rHand: Vec3(0.8, 0.08, 1.42),
  rElbow: Vec3(0, -1, 0),
);

// ===================================================================== look

enum HeadGear { helmet, cap, hat, none }

const kSkinTones = [
  Color(0xFF8A5A3B),
  Color(0xFF6E4630),
  Color(0xFFA0704C),
  Color(0xFF5A3A26),
  Color(0xFFB98563),
  Color(0xFFC79A74),
];

class FigureLook {
  const FigureLook({
    required this.shirt,
    required this.trim,
    required this.trousers,
    this.skin = const Color(0xFF8A5A3B),
    this.hair = const Color(0xFF17110D),
    this.headGear = HeadGear.cap,
    this.helmet,
    this.pads = false,
    this.gloves = false,
    this.keeperGloves = false,
    this.bat = false,
    this.name,
    this.number,
  });

  final Color shirt;
  final Color trim;
  final Color trousers;
  final Color skin;
  final Color hair;
  final HeadGear headGear;
  final Color? helmet;
  final bool pads;
  final bool gloves;
  final bool keeperGloves;
  final bool bat;
  final String? name;
  final int? number;

  FigureLook withSkin(Color s) => FigureLook(
        shirt: shirt,
        trim: trim,
        trousers: trousers,
        skin: s,
        hair: hair,
        headGear: headGear,
        helmet: helmet,
        pads: pads,
        gloves: gloves,
        keeperGloves: keeperGloves,
        bat: bat,
        name: name,
        number: number,
      );
}

Color skinFor(String seed) => kSkinTones[seed.hashCode.abs() % kSkinTones.length];

Color _trousersFor(Color shirt) => shirt.luma > 0.55 ? shirt.darken(0.12) : shirt.darken(0.18);

FigureLook batterLook(Color shirt, Color trim, {String? name, int? number, Color? skin}) => FigureLook(
      shirt: shirt,
      trim: trim,
      trousers: _trousersFor(shirt),
      skin: skin ?? const Color(0xFF8A5A3B),
      headGear: HeadGear.helmet,
      helmet: shirt.luma > 0.55 ? const Color(0xFF1C2A5A) : shirt.darken(0.15),
      pads: true,
      gloves: true,
      bat: true,
      name: name,
      number: number,
    );

FigureLook fielderLook(Color shirt, Color trim, {String? name, int? number, Color? skin}) => FigureLook(
      shirt: shirt,
      trim: trim,
      trousers: _trousersFor(shirt),
      skin: skin ?? const Color(0xFF8A5A3B),
      name: name,
      number: number,
    );

FigureLook keeperLook(Color shirt, Color trim, {Color? skin}) => FigureLook(
      shirt: shirt,
      trim: trim,
      trousers: _trousersFor(shirt),
      skin: skin ?? const Color(0xFF8A5A3B),
      headGear: HeadGear.helmet,
      helmet: shirt.luma > 0.55 ? const Color(0xFF1C2A5A) : shirt.darken(0.15),
      pads: true,
      keeperGloves: true,
    );

const umpireLook = FigureLook(
  shirt: Color(0xFF243A6E),
  trim: Color(0xFFE53935),
  trousers: Color(0xFF14161D),
  headGear: HeadGear.hat,
);

// ================================================================== actor

class Actor {
  Actor({
    required this.foot,
    required this.facing,
    required this.pose,
    required this.look,
    this.mirror = false,
    this.scale = 1,
  });

  final Vec3 foot;

  /// Direction the player faces: 0 = +y (towards the striker), pi/2 = +x.
  final double facing;
  final Pose pose;
  final FigureLook look;
  final bool mirror;
  final double scale;
}

// =============================================================== skeleton

Vec3 _rot(Vec3 v, Vec3 k, double a) {
  final c = math.cos(a), s = math.sin(a);
  final kv = k.dot(v);
  final cx = k.y * v.z - k.z * v.y, cy = k.z * v.x - k.x * v.z, cz = k.x * v.y - k.y * v.x;
  return Vec3(v.x * c + cx * s + k.x * kv * (1 - c), v.y * c + cy * s + k.y * kv * (1 - c), v.z * c + cz * s + k.z * kv * (1 - c));
}

/// Two-bone IK: returns (middle joint, end joint).
(Vec3, Vec3) _ik(Vec3 root, Vec3 target, double a, double b, Vec3 pole) {
  final d = target - root;
  var dist = d.length;
  final minD = (a - b).abs() + 1e-3, maxD = a + b - 1e-3;
  if (dist < minD) dist = minD;
  if (dist > maxD) dist = maxD;
  final dir = d.length < 1e-6 ? const Vec3(0, 0, -1) : d.normalized;
  final x = (a * a - b * b + dist * dist) / (2 * dist);
  final h = math.sqrt(math.max(0, a * a - x * x));
  var pp = pole - dir * pole.dot(dir);
  if (pp.length < 1e-6) {
    pp = dir.cross(const Vec3(0, 0, 1));
    if (pp.length < 1e-6) pp = dir.cross(const Vec3(1, 0, 0));
  }
  pp = pp.normalized;
  return (root + dir * x + pp * h, root + dir * dist);
}

class Skeleton {
  late Vec3 pelvis, neck, head, lSho, rSho, lElb, rElb, lHand, rHand;
  late Vec3 lHip, rHip, lKnee, rKnee, lAnkle, rAnkle, lToe, rToe;
  late Vec3 pR, pF, pU, cR, cF, cU, hR, hF, hU;
  late Vec3 fwd;
  bool hasBat = false;
  late Vec3 batTop, batTip, batFace;
  double s = 1;

  void solve(Pose p, Vec3 origin, double facing, double scale, {bool mirror = false}) {
    final pose = mirror ? p.mirrored() : p;
    s = scale;
    final fw = Vec3(math.sin(facing), math.cos(facing), 0);
    final rt = Vec3(math.cos(facing), -math.sin(facing), 0);
    const up = Vec3(0, 0, 1);
    fwd = fw;
    Vec3 dir(Vec3 v) => rt * v.x + fw * v.y + up * v.z;
    Vec3 at(Vec3 v) => origin + dir(v) * scale;

    // Pelvis frame.
    pF = _rot(fw, up, pose.pelvisYaw);
    pR = _rot(rt, up, pose.pelvisYaw);
    pU = up;
    pU = _rot(pU, pR, -pose.pelvisLean);
    pF = _rot(pF, pR, -pose.pelvisLean);
    pU = _rot(pU, pF, pose.pelvisSide);
    pR = _rot(pR, pF, pose.pelvisSide);
    // Chest frame.
    cF = _rot(pF, pU, pose.spineYaw);
    cR = _rot(pR, pU, pose.spineYaw);
    cU = pU;
    cU = _rot(cU, cR, -pose.spineLean);
    cF = _rot(cF, cR, -pose.spineLean);
    cU = _rot(cU, cF, pose.spineSide);
    cR = _rot(cR, cF, pose.spineSide);
    // Head frame.
    hF = _rot(cF, cU, pose.headYaw);
    hR = _rot(cR, cU, pose.headYaw);
    hU = cU;
    hU = _rot(hU, hR, -pose.headPitch);
    hF = _rot(hF, hR, -pose.headPitch);

    pelvis = at(pose.hip);
    neck = pelvis + cU * (0.5 * s);
    head = neck + hU * (0.15 * s) + hF * (0.025 * s);
    lSho = neck + cR * (-0.19 * s) + cU * (-0.05 * s);
    rSho = neck + cR * (0.19 * s) + cU * (-0.05 * s);
    lHip = pelvis + pR * (-0.1 * s) + pU * (-0.05 * s);
    rHip = pelvis + pR * (0.1 * s) + pU * (-0.05 * s);

    Vec3 lTarget = at(pose.lHand), rTarget = at(pose.rHand);
    hasBat = pose.hold != BatHold.none;
    if (pose.hold == BatHold.both) {
      final bd = dir(pose.bat).normalized;
      final top = at(pose.grip);
      final low = top + bd * (0.105 * s);
      if (pose.topLeft) {
        lTarget = top;
        rTarget = low;
      } else {
        rTarget = top;
        lTarget = low;
      }
      batTop = top - bd * (0.07 * s);
      batTip = batTop + bd * (0.86 * s);
    }
    final la = _ik(lSho, lTarget, 0.29 * s, 0.31 * s, dir(pose.lElbow));
    final ra = _ik(rSho, rTarget, 0.29 * s, 0.31 * s, dir(pose.rElbow));
    lElb = la.$1;
    lHand = la.$2;
    rElb = ra.$1;
    rHand = ra.$2;
    if (pose.hold == BatHold.right) {
      final bd = dir(pose.bat).normalized;
      batTop = rHand - bd * (0.08 * s);
      batTip = batTop + bd * (0.86 * s);
    }
    if (hasBat) {
      final bd = (batTip - batTop).normalized;
      final toBowler = dir(Vec3(pose.topLeft ? -1 : 1, 0.15, 0));
      var face = toBowler - bd * toBowler.dot(bd);
      if (face.length < 1e-4) face = bd.cross(const Vec3(0, 0, 1));
      batFace = face.normalized;
    }

    final ll = _ik(lHip, at(pose.lFoot), 0.44 * s, 0.43 * s, dir(pose.lKnee));
    final rl = _ik(rHip, at(pose.rFoot), 0.44 * s, 0.43 * s, dir(pose.rKnee));
    lKnee = ll.$1;
    lAnkle = ll.$2;
    rKnee = rl.$1;
    rAnkle = rl.$2;
    lToe = lAnkle + dir(pose.lToe).normalized * (0.17 * s);
    rToe = rAnkle + dir(pose.rToe).normalized * (0.17 * s);
  }

  /// Bones as (from, to, radius) for shadows and the far-away LOD.
  List<(Vec3, Vec3, double)> bones() => [
        (pelvis, neck, 0.16 * s),
        (neck, head, 0.1 * s),
        (lSho, lElb, 0.055 * s),
        (lElb, lHand, 0.045 * s),
        (rSho, rElb, 0.055 * s),
        (rElb, rHand, 0.045 * s),
        (lHip, lKnee, 0.085 * s),
        (lKnee, lAnkle, 0.065 * s),
        (rHip, rKnee, 0.085 * s),
        (rKnee, rAnkle, 0.065 * s),
        (lAnkle, lToe, 0.05 * s),
        (rAnkle, rToe, 0.05 * s),
        if (hasBat) (batTop, batTip, 0.045 * s),
      ];
}

// ================================================================ renderer

class _Buf {
  Float64List v = Float64List(6 * 2048);
  Int32List c = Int32List(2048);
  Int32List t = Int32List(3 * 4096);
  int nv = 0, nt = 0;

  void clear() {
    nv = 0;
    nt = 0;
  }

  int vert(double x, double y, double z, double nx, double ny, double nz, int color) {
    if (nv >= c.length) {
      final nv2 = Float64List(v.length * 2)..setAll(0, v);
      final nc2 = Int32List(c.length * 2)..setAll(0, c);
      v = nv2;
      c = nc2;
    }
    final o = nv * 6;
    v[o] = x;
    v[o + 1] = y;
    v[o + 2] = z;
    v[o + 3] = nx;
    v[o + 4] = ny;
    v[o + 5] = nz;
    c[nv] = color;
    return nv++;
  }

  void tri(int a, int b, int d) {
    if (nt * 3 + 3 > t.length) {
      t = Int32List(t.length * 2)..setAll(0, t);
    }
    final o = nt * 3;
    t[o] = a;
    t[o + 1] = b;
    t[o + 2] = d;
    nt++;
  }
}

/// Lighting rig for the characters.
class CharacterLight {
  const CharacterLight({required this.key, required this.ambient, required this.diffuse, required this.keyTint, required this.ambTint, required this.rim, required this.rimTint, required this.spec});

  final Vec3 key;
  final double ambient, diffuse, rim, spec;
  final Color keyTint, ambTint, rimTint;

  static final day = CharacterLight(
    key: const Vec3(0.42, -0.55, 0.72).normalized,
    ambient: 0.5,
    diffuse: 0.72,
    keyTint: const Color(0xFFFFF4E2),
    ambTint: const Color(0xFFD4E2FF),
    rim: 0.12,
    rimTint: const Color(0xFFFFFFFF),
    spec: 0.18,
  );

  static final night = CharacterLight(
    key: const Vec3(-0.25, -0.45, 0.86).normalized,
    ambient: 0.44,
    diffuse: 0.7,
    keyTint: const Color(0xFFFFF8EE),
    ambTint: const Color(0xFFB8C8FF),
    rim: 0.42,
    rimTint: const Color(0xFFBFE3FF),
    spec: 0.22,
  );
}

class CharacterRenderer {
  final _b = _Buf();
  final _sk = Skeleton();
  Float32List _pos = Float32List(6 * 4096);
  Int32List _col = Int32List(3 * 4096);
  Float64List _sx = Float64List(4096), _sy = Float64List(4096), _sd = Float64List(4096);
  final List<int> _order = [];
  Float64List _td = Float64List(4096);

  CharacterLight light = CharacterLight.day;

  static const _pad = Color(0xFFF4F4F1);
  static const _shoe = Color(0xFFF7F7F7);
  static const _sole = Color(0xFF2B2B30);
  static const _wood = Color(0xFFE7CF95);
  static const _grip = Color(0xFF1C1C24);

  final List<Skeleton> _pool = [];
  final Map<Actor, Skeleton> _solved = {};

  Skeleton skeleton(Actor a) {
    final hit = _solved[a];
    if (hit != null) return hit;
    _sk.solve(a.pose, a.foot, a.facing, a.scale, mirror: a.mirror);
    return _sk;
  }

  /// Solves every actor's skeleton once for this frame (shadows + bodies).
  void prepare(List<Actor> actors) {
    _solved.clear();
    while (_pool.length < actors.length) {
      _pool.add(Skeleton());
    }
    for (var i = 0; i < actors.length; i++) {
      final a = actors[i];
      _pool[i].solve(a.pose, a.foot, a.facing, a.scale, mirror: a.mirror);
      _solved[a] = _pool[i];
    }
  }

  /// Soft sun (or floodlight) shadows for [actors], drawn in one layer.
  void drawShadows(Canvas canvas, Camera3D cam, List<Actor> actors, {required bool day}) {
    final dirs = day ? [light.key] : const [Vec3(0.55, 0.45, 0.7), Vec3(-0.55, 0.45, 0.7), Vec3(0.5, -0.5, 0.7), Vec3(-0.5, -0.5, 0.7)];
    canvas.saveLayer(null, Paint()..color = Colors.black.o(day ? 0.38 : 0.2));
    final p = Paint()
      ..strokeCap = StrokeCap.round
      ..color = Colors.black;
    for (final a in actors) {
      if (cam.depth(a.foot) < 1.5) continue;
      final sc = cam.scaleAt(a.foot);
      if (sc < 1.5) continue;
      final sk = skeleton(a);
      // Distant players: just a contact shadow.
      if (sc > 6) {
        for (final d0 in dirs) {
          final d = d0.normalized;
          Vec3 g(Vec3 v) => Vec3(v.x - d.x * v.z / d.z, v.y - d.y * v.z / d.z, 0.01);
          for (final (a0, b0, r) in sk.bones()) {
            final ga = g(a0), gb = g(b0);
            if (cam.depth(ga) < 0.8 || cam.depth(gb) < 0.8) continue;
            p.strokeWidth = math.max(1.0, r * 2 * cam.scaleAt(ga));
            canvas.drawLine(cam.project(ga), cam.project(gb), p);
          }
        }
      }
      canvas.drawOval(Rect.fromCenter(center: cam.project(a.foot), width: 0.75 * sc * a.scale, height: 0.22 * sc * a.scale), p);
    }
    canvas.restore();
  }

  void draw(Canvas canvas, Camera3D cam, Actor a) {
    final s = cam.scaleAt(a.foot.withZ(0.9)) * a.scale;
    if (s < 2) return;
    final sk = skeleton(a);
    if (s < 9) {
      _drawFar(canvas, cam, sk, a.look, s);
      return;
    }
    final n = s < 20 ? 4 : (s < 50 ? 6 : 9);
    _b.clear();
    _build(sk, a.look, n, s);
    _render(canvas, cam);
    if (s > 32) _decals(canvas, cam, sk, a.look, s);
  }

  // ---------------------------------------------------------- geometry

  void _drawFar(Canvas c, Camera3D cam, Skeleton sk, FigureLook look, double s) {
    final p = Paint()
      ..strokeCap = StrokeCap.round
      ..strokeWidth = math.max(1.1, 0.16 * s);
    void seg(Vec3 a, Vec3 b, Color col, double w) {
      if (cam.depth(a) < 0.6 || cam.depth(b) < 0.6) return;
      p
        ..color = col
        ..strokeWidth = math.max(1.0, w * s);
      c.drawLine(cam.project(a), cam.project(b), p);
    }

    final legs = look.pads ? _pad : look.trousers;
    seg(sk.lHip, sk.lKnee, look.trousers, 0.15);
    seg(sk.rHip, sk.rKnee, look.trousers, 0.15);
    seg(sk.lKnee, sk.lAnkle, legs, 0.12);
    seg(sk.rKnee, sk.rAnkle, legs, 0.12);
    seg(sk.pelvis, sk.neck, look.shirt, 0.3);
    seg(sk.lSho, sk.lHand, look.shirt.darken(0.1), 0.09);
    seg(sk.rSho, sk.rHand, look.shirt.darken(0.1), 0.09);
    if (sk.hasBat) seg(sk.batTop, sk.batTip, _wood, 0.07);
    final hp = cam.project(sk.head);
    c.drawCircle(hp, math.max(1.1, 0.12 * s), Paint()..color = look.headGear == HeadGear.helmet ? (look.helmet ?? look.shirt) : (look.headGear == HeadGear.hat ? Colors.white : look.skin));
  }

  void _tube(Vec3 a, Vec3 b, double ra, double rb, Color ca, Color cb, int n, {bool capA = true, bool capB = true}) {
    final ax0 = b - a;
    final len = ax0.length;
    if (len < 1e-5) return;
    final ax = ax0 * (1 / len);
    final ux0 = ax.z.abs() < 0.9 ? ax.cross(const Vec3(0, 0, 1)) : ax.cross(const Vec3(1, 0, 0));
    final u = ux0.normalized;
    final v = ax.cross(u);
    final ia = _b.nv;
    final ica = ca.toARGB32(), icb = cb.toARGB32();
    for (var k = 0; k < n; k++) {
      final th = k / n * math.pi * 2;
      final cs = math.cos(th), sn = math.sin(th);
      final dx = u.x * cs + v.x * sn, dy = u.y * cs + v.y * sn, dz = u.z * cs + v.z * sn;
      _b.vert(a.x + dx * ra, a.y + dy * ra, a.z + dz * ra, dx, dy, dz, ica);
    }
    final ib = _b.nv;
    for (var k = 0; k < n; k++) {
      final th = k / n * math.pi * 2;
      final cs = math.cos(th), sn = math.sin(th);
      final dx = u.x * cs + v.x * sn, dy = u.y * cs + v.y * sn, dz = u.z * cs + v.z * sn;
      _b.vert(b.x + dx * rb, b.y + dy * rb, b.z + dz * rb, dx, dy, dz, icb);
    }
    for (var k = 0; k < n; k++) {
      final k1 = (k + 1) % n;
      _b.tri(ia + k, ia + k1, ib + k1);
      _b.tri(ia + k, ib + k1, ib + k);
    }
    void cap(Vec3 c, double r, double sign, int ring, int col) {
      if (n <= 6) {
        // Low detail: a single pole vertex rounds the end well enough.
        final pole = _b.vert(c.x + ax.x * sign * r * 0.8, c.y + ax.y * sign * r * 0.8, c.z + ax.z * sign * r * 0.8, ax.x * sign, ax.y * sign, ax.z * sign, col);
        for (var k = 0; k < n; k++) {
          _b.tri(ring + k, ring + (k + 1) % n, pole);
        }
        return;
      }
      final ic = _b.nv;
      for (var k = 0; k < n; k++) {
        final th = k / n * math.pi * 2;
        final cs = math.cos(th), sn = math.sin(th);
        final dx = u.x * cs + v.x * sn, dy = u.y * cs + v.y * sn, dz = u.z * cs + v.z * sn;
        final nx = dx * 0.8 + ax.x * sign * 0.6, ny = dy * 0.8 + ax.y * sign * 0.6, nz = dz * 0.8 + ax.z * sign * 0.6;
        _b.vert(c.x + dx * r * 0.8 + ax.x * sign * r * 0.55, c.y + dy * r * 0.8 + ax.y * sign * r * 0.55, c.z + dz * r * 0.8 + ax.z * sign * r * 0.55, nx, ny, nz, col);
      }
      final pole = _b.vert(c.x + ax.x * sign * r, c.y + ax.y * sign * r, c.z + ax.z * sign * r, ax.x * sign, ax.y * sign, ax.z * sign, col);
      for (var k = 0; k < n; k++) {
        final k1 = (k + 1) % n;
        _b.tri(ring + k, ring + k1, ic + k1);
        _b.tri(ring + k, ic + k1, ic + k);
        _b.tri(ic + k, ic + k1, pole);
      }
    }

    if (capA) cap(a, ra, -1, ia, ica);
    if (capB) cap(b, rb, 1, ib, icb);
  }

  /// Ellipsoid with semi-axes [ex], [ey], [ez] (orthogonal vectors).
  void _ellipsoid(Vec3 c, Vec3 ex, Vec3 ey, Vec3 ez, int nu, int nv, Color Function(double lat, double lon) color, {double latMin = -math.pi / 2, double latMax = math.pi / 2}) {
    final lx = ex.dot(ex), ly = ey.dot(ey), lz = ez.dot(ez);
    final start = _b.nv;
    for (var i = 0; i <= nv; i++) {
      final lat = latMin + (latMax - latMin) * i / nv;
      final cl = math.cos(lat), sl = math.sin(lat);
      for (var j = 0; j < nu; j++) {
        final lon = j / nu * math.pi * 2;
        final co = math.cos(lon), so = math.sin(lon);
        final px = c.x + ex.x * cl * co + ey.x * cl * so + ez.x * sl;
        final py = c.y + ex.y * cl * co + ey.y * cl * so + ez.y * sl;
        final pz = c.z + ex.z * cl * co + ey.z * cl * so + ez.z * sl;
        var nx = ex.x * cl * co / lx + ey.x * cl * so / ly + ez.x * sl / lz;
        var ny = ex.y * cl * co / lx + ey.y * cl * so / ly + ez.y * sl / lz;
        var nz = ex.z * cl * co / lx + ey.z * cl * so / ly + ez.z * sl / lz;
        final nl = math.sqrt(nx * nx + ny * ny + nz * nz);
        nx /= nl;
        ny /= nl;
        nz /= nl;
        _b.vert(px, py, pz, nx, ny, nz, color(lat, lon).toARGB32());
      }
    }
    for (var i = 0; i < nv; i++) {
      for (var j = 0; j < nu; j++) {
        final j1 = (j + 1) % nu;
        final a = start + i * nu + j, b = start + i * nu + j1, c2 = start + (i + 1) * nu + j1, d = start + (i + 1) * nu + j;
        _b.tri(a, b, c2);
        _b.tri(a, c2, d);
      }
    }
  }

  /// Lofted elliptical sections (torso, pads).
  void _loft(List<(Vec3, Vec3, Vec3, double, double)> sections, int n, Color Function(int section, double angle) color, {bool capBottom = true, bool capTop = true}) {
    final starts = <int>[];
    for (var si = 0; si < sections.length; si++) {
      final (c, r, f, w, d) = sections[si];
      starts.add(_b.nv);
      for (var k = 0; k < n; k++) {
        final th = k / n * math.pi * 2;
        final cs = math.cos(th), sn = math.sin(th);
        final px = c.x + r.x * cs * w + f.x * sn * d, py = c.y + r.y * cs * w + f.y * sn * d, pz = c.z + r.z * cs * w + f.z * sn * d;
        var nx = r.x * cs / w + f.x * sn / d, ny = r.y * cs / w + f.y * sn / d, nz = r.z * cs / w + f.z * sn / d;
        final nl = math.sqrt(nx * nx + ny * ny + nz * nz);
        nx /= nl;
        ny /= nl;
        nz /= nl;
        _b.vert(px, py, pz, nx, ny, nz, color(si, th).toARGB32());
      }
    }
    for (var si = 0; si < sections.length - 1; si++) {
      final a = starts[si], b = starts[si + 1];
      for (var k = 0; k < n; k++) {
        final k1 = (k + 1) % n;
        _b.tri(a + k, a + k1, b + k1);
        _b.tri(a + k, b + k1, b + k);
      }
    }
    void cap(int si, double sign) {
      final (c, r, f, _, _) = sections[si];
      final up = r.cross(f).normalized * sign;
      final pole = _b.vert(c.x, c.y, c.z, up.x, up.y, up.z, color(si, 0).toARGB32());
      final a = starts[si];
      for (var k = 0; k < n; k++) {
        _b.tri(a + k, a + (k + 1) % n, pole);
      }
    }

    if (capBottom) cap(0, -1);
    if (capTop) cap(sections.length - 1, 1);
  }

  void _box(Vec3 c, Vec3 ax, Vec3 ay, Vec3 az, List<Color> faceCols) {
    final faces = <(Vec3, Vec3, Vec3)>[
      (ax, ay, az),
      (-ax, -ay, az),
      (ay, -ax, az),
      (-ay, ax, az),
      (az, ay, -ax),
      (-az, ay, ax),
    ];
    for (var fi = 0; fi < 6; fi++) {
      final (n, u, v) = faces[fi];
      final nn = n.normalized;
      final col = faceCols[fi % faceCols.length].toARGB32();
      final base = c + n;
      final i0 = _b.vert(base.x - u.x - v.x, base.y - u.y - v.y, base.z - u.z - v.z, nn.x, nn.y, nn.z, col);
      final i1 = _b.vert(base.x + u.x - v.x, base.y + u.y - v.y, base.z + u.z - v.z, nn.x, nn.y, nn.z, col);
      final i2 = _b.vert(base.x + u.x + v.x, base.y + u.y + v.y, base.z + u.z + v.z, nn.x, nn.y, nn.z, col);
      final i3 = _b.vert(base.x - u.x + v.x, base.y - u.y + v.y, base.z - u.z + v.z, nn.x, nn.y, nn.z, col);
      _b.tri(i0, i1, i2);
      _b.tri(i0, i2, i3);
    }
  }

  void _build(Skeleton k, FigureLook look, int n, double px) {
    final s = k.s;
    final detail = n >= 7;
    final nh = n + 2;

    // Legs.
    for (final (hip, knee, ankle, toe) in [(k.lHip, k.lKnee, k.lAnkle, k.lToe), (k.rHip, k.rKnee, k.rAnkle, k.rToe)]) {
      _tube(hip, knee, 0.1 * s, 0.07 * s, look.trousers, look.trousers.darken(0.04), n);
      _tube(knee, ankle, 0.068 * s, 0.047 * s, look.trousers.darken(0.04), look.trousers.darken(0.08), n, capA: false);
      final shin = ankle - knee;
      final footDir = (toe - ankle).normalized;
      var front = footDir - shin.normalized * footDir.dot(shin.normalized);
      front = front.length < 1e-4 ? k.fwd : front.normalized;
      if (look.pads) {
        final side = shin.normalized.cross(front).normalized;
        final top = knee + front * (0.045 * s) - shin * 0.12;
        final mid = knee + shin * 0.45 + front * (0.04 * s);
        final bot = ankle + front * (0.035 * s) - shin * 0.06;
        _loft([
          (top, side, front, 0.078 * s, 0.05 * s),
          (mid, side, front, 0.075 * s, 0.055 * s),
          (bot, side, front, 0.065 * s, 0.05 * s),
        ], n, (si, th) => (math.cos(th * 3)).abs() > 0.9 ? _pad.darken(0.1) : _pad);
        _ellipsoid(knee + front * (0.06 * s), side * (0.07 * s), front * (0.045 * s), shin.normalized * (-0.06 * s), n, detail ? 4 : 3, (lat, lon) => _pad);
      }
      // Shoe.
      final sc = ankle + footDir * (0.07 * s) - const Vec3(0, 0, 1) * (0.03 * s);
      final side = footDir.cross(const Vec3(0, 0, 1)).normalized;
      _ellipsoid(sc, footDir * (0.13 * s), side * (0.052 * s), const Vec3(0, 0, 1) * (0.05 * s), n, detail ? 4 : 3, (lat, lon) => lat < -0.35 ? _sole : _shoe);
    }

    // Pelvis + torso.
    _ellipsoid(k.pelvis - k.pU * (0.03 * s), k.pR * (0.165 * s), k.pF * (0.11 * s), k.pU * (0.12 * s), n + 2, detail ? 4 : 3, (lat, lon) => look.trousers);
    final waist = k.pelvis + k.pU * (0.06 * s);
    final belly = k.pelvis + k.cU * (0.2 * s);
    final chest = k.pelvis + k.cU * (0.35 * s);
    final shoulders = k.neck - k.cU * (0.07 * s);
    final collar = k.neck + k.cU * (0.005 * s);
    final shirtTop = look.shirt.lighten(0.06);
    _loft([
      (waist, k.pR, k.pF, 0.168 * s, 0.115 * s),
      (belly, k.cR, k.cF, 0.166 * s, 0.112 * s),
      (chest, k.cR, k.cF, 0.205 * s, 0.13 * s),
      (shoulders, k.cR, k.cF, 0.22 * s, 0.12 * s),
      (collar, k.cR, k.cF, 0.078 * s, 0.072 * s),
    ], nh + 2, (si, th) {
      if (si == 4) return look.trim;
      if (si == 0) return look.shirt.darken(0.08);
      return si >= 3 ? shirtTop : look.shirt;
    }, capTop: false);

    // Neck + head.
    _tube(k.neck, k.head - k.hU * (0.07 * s), 0.052 * s, 0.048 * s, look.skin.darken(0.08), look.skin, n, capA: false);
    final hx = k.hR * (0.096 * s), hy = k.hF * (0.108 * s), hz = k.hU * (0.118 * s);
    _ellipsoid(k.head, hx, hy, hz, nh + 2, detail ? 7 : 4, (lat, lon) {
      // Hair on the top and back of the head.
      final front = math.sin(lon);
      if (lat > 0.45 || (lat > -0.2 && front < -0.35)) return look.hair;
      return look.skin;
    });
    switch (look.headGear) {
      case HeadGear.helmet:
        final hc = look.helmet ?? look.shirt.darken(0.3);
        _ellipsoid(k.head + k.hU * (0.02 * s) - k.hF * (0.005 * s), k.hR * (0.122 * s), k.hF * (0.135 * s), k.hU * (0.13 * s), nh + 2, detail ? 6 : 4,
            (lat, lon) => math.sin(lon) < -0.2 && lat < 0.2 ? hc.darken(0.15) : hc,
            latMin: -0.25);
        // Peak.
        _box(k.head + k.hF * (0.13 * s) + k.hU * (0.035 * s), k.hR * (0.085 * s), k.hF * (0.045 * s), k.hU * (0.012 * s), [hc.darken(0.2)]);
        if (detail) {
          // Face grille.
          final g = const Color(0xFFCBD2DC);
          for (final dz in [-0.035, -0.075, -0.115]) {
            final cc = k.head + k.hF * (0.125 * s) + k.hU * (dz * s);
            _tube(cc - k.hR * (0.085 * s) - k.hF * (0.03 * s), cc - k.hR * (0.03 * s), 0.006 * s, 0.006 * s, g, g, 3, capA: false, capB: false);
            _tube(cc - k.hR * (0.03 * s), cc + k.hR * (0.03 * s), 0.006 * s, 0.006 * s, g, g, 3, capA: false, capB: false);
            _tube(cc + k.hR * (0.03 * s), cc + k.hR * (0.085 * s) - k.hF * (0.03 * s), 0.006 * s, 0.006 * s, g, g, 3, capA: false, capB: false);
          }
          final top = k.head + k.hF * (0.128 * s) + k.hU * (0.0 * s), bot = k.head + k.hF * (0.12 * s) - k.hU * (0.13 * s);
          _tube(top, bot, 0.006 * s, 0.006 * s, g, g, 3, capA: false, capB: false);
        }
      case HeadGear.cap:
        final cc = look.shirt.darken(0.1);
        _ellipsoid(k.head + k.hU * (0.025 * s), k.hR * (0.104 * s), k.hF * (0.116 * s), k.hU * (0.11 * s), nh + 2, detail ? 4 : 3, (lat, lon) => cc, latMin: 0.05);
        _box(k.head + k.hF * (0.15 * s) + k.hU * (0.035 * s), k.hR * (0.075 * s), k.hF * (0.06 * s), k.hU * (0.008 * s), [cc.darken(0.15)]);
      case HeadGear.hat:
        const hat = Color(0xFFF3F1EA);
        _ellipsoid(k.head + k.hU * (0.05 * s), k.hR * (0.2 * s), k.hF * (0.2 * s), k.hU * (0.012 * s), nh + 2, 2, (lat, lon) => hat.darken(0.05));
        _ellipsoid(k.head + k.hU * (0.05 * s), k.hR * (0.105 * s), k.hF * (0.11 * s), k.hU * (0.1 * s), nh, 3, (lat, lon) => hat, latMin: 0);
      case HeadGear.none:
        break;
    }

    // Arms.
    final gloveCol = look.keeperGloves ? const Color(0xFFF1EEE4) : const Color(0xFFF7F7F7);
    for (final (sho, elb, hand) in [(k.lSho, k.lElb, k.lHand), (k.rSho, k.rElb, k.rHand)]) {
      _ellipsoid(sho, k.cR * (0.082 * s), k.cF * (0.076 * s), k.cU * (0.076 * s), n, 3, (lat, lon) => look.shirt);
      final sleeve = Vec3.lerp(sho, elb, 0.6);
      final hem = Vec3.lerp(sho, elb, 0.66);
      _tube(sho, sleeve, 0.072 * s, 0.064 * s, look.shirt, look.shirt.darken(0.05), n, capA: false, capB: false);
      _tube(sleeve, hem, 0.064 * s, 0.062 * s, look.trim, look.trim, n, capA: false, capB: false);
      _tube(hem, elb, 0.054 * s, 0.049 * s, look.skin, look.skin, n, capA: false);
      final wrist = Vec3.lerp(elb, hand, 0.84);
      _tube(elb, wrist, 0.049 * s, 0.036 * s, look.skin, look.skin.darken(0.05), n, capA: false, capB: false);
      if (look.gloves || look.keeperGloves) {
        final r = (look.keeperGloves ? 0.075 : 0.052) * s;
        _ellipsoid(hand, Vec3(r, 0, 0), Vec3(0, r, 0), Vec3(0, 0, r * 1.1), n, 3, (lat, lon) => gloveCol);
      } else {
        final r = 0.04 * s;
        _ellipsoid(hand, Vec3(r, 0, 0), Vec3(0, r, 0), Vec3(0, 0, r * 1.15), n, 3, (lat, lon) => look.skin);
      }
    }

    // Bat.
    if (k.hasBat) {
      final bd = (k.batTip - k.batTop).normalized;
      final side = bd.cross(k.batFace).normalized;
      final handleEnd = k.batTop + bd * (0.3 * s);
      _tube(k.batTop, handleEnd, 0.018 * s, 0.017 * s, _grip, _grip, math.min(n, 6));
      final bc = handleEnd + bd * (0.28 * s);
      _box(bc, bd * (0.28 * s), side * (0.054 * s), k.batFace * (0.022 * s), [
        _wood.darken(0.1),
        _wood.darken(0.1),
        _wood,
        const Color(0xFFC9A766),
        _wood,
        _wood,
      ]);
      if (detail) {
        // Sticker band on the face.
        _box(bc - bd * (0.12 * s) + k.batFace * (0.023 * s), bd * (0.06 * s), side * (0.05 * s), k.batFace * (0.002 * s), [AppColors.red]);
      }
    }
  }

  // ---------------------------------------------------------- render

  void _ensure(int nv, int nt) {
    if (_sx.length < nv) {
      _sx = Float64List(nv * 2);
      _sy = Float64List(nv * 2);
      _sd = Float64List(nv * 2);
    }
    if (_td.length < nt) _td = Float64List(nt * 2);
    if (_pos.length < nt * 6) {
      _pos = Float32List(nt * 12);
      _col = Int32List(nt * 6);
    }
  }

  void _render(Canvas canvas, Camera3D cam) {
    final nv = _b.nv, nt = _b.nt;
    _ensure(nv, nt);
    final v = _b.v;
    final tmp = Float64List(2);
    // Project and light every vertex.
    final l = light;
    final lkx = l.key.x, lky = l.key.y, lkz = l.key.z;
    final amb = l.ambient, dif = l.diffuse, rimK = l.rim, specK = l.spec;
    final kr = l.keyTint.r * dif, kg = l.keyTint.g * dif, kb = l.keyTint.b * dif;
    final ar = l.ambTint.r * amb, ag = l.ambTint.g * amb, ab = l.ambTint.b * amb;
    final rr = l.rimTint.r * rimK, rg = l.rimTint.g * rimK, rb = l.rimTint.b * rimK;
    final cpx = cam.pos.x, cpy = cam.pos.y, cpz = cam.pos.z;
    final cols = _b.c;
    // The view direction barely changes across one figure: use its centre.
    var vx = cpx - v[0], vy = cpy - v[1], vz = cpz - v[2];
    final vl = math.sqrt(vx * vx + vy * vy + vz * vz);
    vx /= vl;
    vy /= vl;
    vz /= vl;
    var hx = lkx + vx, hy = lky + vy, hz = lkz + vz;
    final hl = math.sqrt(hx * hx + hy * hy + hz * hz);
    hx /= hl;
    hy /= hl;
    hz /= hl;
    for (var i = 0; i < nv; i++) {
      final o = i * 6;
      _sd[i] = cam.projectInto(v[o], v[o + 1], v[o + 2], tmp, 0);
      _sx[i] = tmp[0];
      _sy[i] = tmp[1];
      final nx = v[o + 3], ny = v[o + 4], nz = v[o + 5];
      final ndl = nx * lkx + ny * lky + nz * lkz;
      final diff = ndl > 0 ? ndl : 0.0;
      final ndv = nx * vx + ny * vy + nz * vz;
      final f = ndv < 0 ? 1.0 : 1 - ndv;
      final rim = f * f * f;
      // Blinn specular (ndh^24 by repeated squaring).
      final ndh = nx * hx + ny * hy + nz * hz;
      var spec = 0.0;
      if (ndh > 0) {
        final p2 = ndh * ndh, p4 = p2 * p2, p8 = p4 * p4, p16 = p8 * p8;
        spec = p16 * p8 * specK * 255;
      }
      final c = cols[i];
      final br = ((c >> 16) & 0xFF).toDouble(), bg = ((c >> 8) & 0xFF).toDouble(), bb = (c & 0xFF).toDouble();
      var r = br * (ar + kr * diff) + rim * rr * 255 + spec;
      var g = bg * (ag + kg * diff) + rim * rg * 255 + spec;
      var b = bb * (ab + kb * diff) + rim * rb * 255 + spec;
      r = r > 255 ? 255 : r;
      g = g > 255 ? 255 : g;
      b = b > 255 ? 255 : b;
      cols[i] = 0xFF000000 | (r.toInt() << 16) | (g.toInt() << 8) | b.toInt();
    }
    final cp = cam.pos;
    // Cull back faces and sort triangles far to near.
    final t = _b.t;
    _order.clear();
    for (var i = 0; i < nt; i++) {
      final a = t[i * 3], b = t[i * 3 + 1], c = t[i * 3 + 2];
      if (_sd[a] < 0.3 || _sd[b] < 0.3 || _sd[c] < 0.3) continue;
      // Screen-space winding is unreliable for mixed meshes; use normals.
      final oa = a * 6, ob = b * 6, oc = c * 6;
      final mx = (v[oa] + v[ob] + v[oc]) / 3, my = (v[oa + 1] + v[ob + 1] + v[oc + 1]) / 3, mz = (v[oa + 2] + v[ob + 2] + v[oc + 2]) / 3;
      final nx = v[oa + 3] + v[ob + 3] + v[oc + 3], ny = v[oa + 4] + v[ob + 4] + v[oc + 4], nz = v[oa + 5] + v[ob + 5] + v[oc + 5];
      if (nx * (mx - cp.x) + ny * (my - cp.y) + nz * (mz - cp.z) > 0) continue;
      _td[i] = _sd[a] + _sd[b] + _sd[c];
      _order.add(i);
    }
    final td = _td;
    _order.sort((x, y) => td[y].compareTo(td[x]));
    var k = 0;
    for (final i in _order) {
      for (var j = 0; j < 3; j++) {
        final vi = t[i * 3 + j];
        _pos[k * 2] = _sx[vi];
        _pos[k * 2 + 1] = _sy[vi];
        _col[k] = cols[vi];
        k++;
      }
    }
    if (k == 0) return;
    final vert = ui.Vertices.raw(ui.VertexMode.triangles, Float32List.sublistView(_pos, 0, k * 2), colors: Int32List.sublistView(_col, 0, k));
    canvas.drawVertices(vert, BlendMode.dst, Paint());
    vert.dispose();
  }

  /// Name and number on the back of the shirt.
  void _decals(Canvas canvas, Camera3D cam, Skeleton k, FigureLook look, double px) {
    if (look.number == null) return;
    final back = k.pelvis + k.cU * (0.34 * k.s) - k.cF * (0.12 * k.s);
    final toCam = (cam.pos - back).normalized;
    if ((-k.cF).dot(toCam) < 0.35) return;
    final r = -k.cR; // seen from behind, the player's left is our right
    final up = k.cU;
    if (cam.depth(back) < 1) return;
    final o = cam.project(back);
    final ox = cam.project(back + r * 0.01) - o;
    final oy = cam.project(back - up * 0.01) - o;
    final m = Float64List(16)
      ..[0] = ox.dx
      ..[1] = ox.dy
      ..[4] = oy.dx
      ..[5] = oy.dy
      ..[10] = 1
      ..[12] = o.dx
      ..[13] = o.dy
      ..[15] = 1;
    canvas.save();
    canvas.transform(m);
    final textCol = look.trim.luma < 0.25 || (look.trim.r - look.shirt.r).abs() + (look.trim.g - look.shirt.g).abs() + (look.trim.b - look.shirt.b).abs() < 0.3 ? Colors.white : look.trim;
    final name = _text(look.name ?? '', T.display(6.5, color: textCol, weight: FontWeight.w700, spacing: 0.4));
    name.paint(canvas, Offset(-name.width / 2, -14));
    final num = _text('${look.number}', T.display(17, color: textCol, weight: FontWeight.w700, height: 1));
    num.paint(canvas, Offset(-num.width / 2, -6));
    canvas.restore();
  }

  static final Map<String, TextPainter> _texts = {};

  TextPainter _text(String s, TextStyle style) {
    final key = '$s|${style.fontSize}|${style.color?.toARGB32()}';
    final hit = _texts[key];
    if (hit != null) return hit;
    if (_texts.length > 200) {
      for (final t in _texts.values) {
        t.dispose();
      }
      _texts.clear();
    }
    return _texts[key] = TextPainter(text: TextSpan(text: s, style: style), textDirection: TextDirection.ltr)..layout();
  }
}
