import 'dart:math' as math;

import 'package:flutter/painting.dart';

/// World space: metres. x = lateral (+x = right-hander's leg side), y = along
/// the pitch from the bowler's stumps (y = 0) to the striker's stumps
/// (y = 20.12), z = height.
class Vec3 {
  const Vec3(this.x, this.y, this.z);

  static const zero = Vec3(0, 0, 0);

  final double x, y, z;

  Vec3 operator +(Vec3 o) => Vec3(x + o.x, y + o.y, z + o.z);
  Vec3 operator -(Vec3 o) => Vec3(x - o.x, y - o.y, z - o.z);
  Vec3 operator *(double s) => Vec3(x * s, y * s, z * s);
  Vec3 operator -() => Vec3(-x, -y, -z);
  Vec3 withZ(double nz) => Vec3(x, y, nz);
  Vec3 get ground => Vec3(x, y, 0);
  double get length => math.sqrt(x * x + y * y + z * z);
  double distTo(Vec3 o) => (this - o).length;
  double dot(Vec3 o) => x * o.x + y * o.y + z * o.z;
  Vec3 cross(Vec3 o) => Vec3(y * o.z - z * o.y, z * o.x - x * o.z, x * o.y - y * o.x);
  Vec3 get normalized {
    final l = length;
    return l < 1e-9 ? const Vec3(0, 0, 1) : Vec3(x / l, y / l, z / l);
  }

  static Vec3 lerp(Vec3 a, Vec3 b, double t) =>
      Vec3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
}

const kPitchLength = 20.12;
const kStrikerCrease = 18.9;
const kBowlerCrease = 1.22;
const kPitchMid = 10.06;

/// Pinhole camera with yaw (around z) and pitch (downward tilt).
class Camera3D {
  Camera3D({required this.pos, required this.focal, required this.cx, required this.cy, this.pitch = 0, this.yaw = 0})
      : _fx = math.sin(yaw) * math.cos(pitch),
        _fy = math.cos(yaw) * math.cos(pitch),
        _fz = -math.sin(pitch),
        _rx = math.cos(yaw),
        _ry = -math.sin(yaw),
        _ux = math.sin(yaw) * math.sin(pitch),
        _uy = math.cos(yaw) * math.sin(pitch),
        _uz = math.cos(pitch);

  factory Camera3D.lookAt(Vec3 pos, Vec3 target, {required double focal, required double cx, required double cy}) {
    final dx = target.x - pos.x, dy = target.y - pos.y, dz = target.z - pos.z;
    final h = math.sqrt(dx * dx + dy * dy);
    return Camera3D(pos: pos, focal: focal, cx: cx, cy: cy, yaw: math.atan2(dx, dy), pitch: math.atan2(-dz, h));
  }

  final Vec3 pos;
  final double focal;
  final double cx, cy;
  final double pitch;
  final double yaw;
  final double _fx, _fy, _fz, _rx, _ry, _ux, _uy, _uz;

  /// Distance along the viewing direction.
  double depth(Vec3 p) => (p.x - pos.x) * _fx + (p.y - pos.y) * _fy + (p.z - pos.z) * _fz;
  double depthXYZ(double x, double y, double z) => (x - pos.x) * _fx + (y - pos.y) * _fy + (z - pos.z) * _fz;

  bool visible(Vec3 p) => depth(p) > 0.5;

  Offset project(Vec3 p) {
    final dx = p.x - pos.x, dy = p.y - pos.y, dz = p.z - pos.z;
    final d = math.max(0.05, dx * _fx + dy * _fy + dz * _fz);
    final xc = dx * _rx + dy * _ry;
    final up = dx * _ux + dy * _uy + dz * _uz;
    return Offset(cx + focal * xc / d, cy - focal * up / d);
  }

  /// Projects into [out] at [i]*2 and returns the depth.
  double projectInto(double x, double y, double z, List<double> out, int i) {
    final dx = x - pos.x, dy = y - pos.y, dz = z - pos.z;
    final d = dx * _fx + dy * _fy + dz * _fz;
    final dd = d < 0.05 ? 0.05 : d;
    out[i * 2] = cx + focal * (dx * _rx + dy * _ry) / dd;
    out[i * 2 + 1] = cy - focal * (dx * _ux + dy * _uy + dz * _uz) / dd;
    return d;
  }

  /// Pixels per metre at the depth of [p].
  double scaleAt(Vec3 p) => focal / math.max(0.05, depth(p));

  /// World point on the ground (z = 0) under a screen position.
  Vec3? groundAt(Offset s) {
    final dx = (s.dx - cx) / focal, dy = (cy - s.dy) / focal;
    final dirX = _fx + _rx * dx + _ux * dy;
    final dirY = _fy + _ry * dx + _uy * dy;
    final dirZ = _fz + _uz * dy;
    if (dirZ >= -1e-4) return null;
    final t = -pos.z / dirZ;
    return Vec3(pos.x + dirX * t, pos.y + dirY * t, 0);
  }

  /// True when the camera looks roughly from the bowler's end towards the striker.
  bool get fromBowlerEnd => _fy > 0;

  Camera3D shaken(double amount, double time) {
    if (amount <= 0) return this;
    return Camera3D(
      pos: pos,
      focal: focal,
      cx: cx + math.sin(time * 61) * amount * 6,
      cy: cy + math.cos(time * 53) * amount * 5,
      pitch: pitch,
      yaw: yaw,
    );
  }

  static double _angleLerp(double a, double b, double t) {
    var d = b - a;
    while (d > math.pi) {
      d -= 2 * math.pi;
    }
    while (d < -math.pi) {
      d += 2 * math.pi;
    }
    return a + d * t;
  }

  static Camera3D lerp(Camera3D a, Camera3D b, double t) => Camera3D(
        pos: Vec3.lerp(a.pos, b.pos, t),
        focal: a.focal + (b.focal - a.focal) * t,
        cx: a.cx + (b.cx - a.cx) * t,
        cy: a.cy + (b.cy - a.cy) * t,
        pitch: a.pitch + (b.pitch - a.pitch) * t,
        yaw: _angleLerp(a.yaw, b.yaw, t),
      );
}

/// Camera rigs used by the game.
class Rigs {
  /// Behind the bowler's end, low third-person angle with the stands, roof
  /// and sky in shot (user bowling / AI batting).
  static Vec3 bowlingPos = const Vec3(0, -8.6, 4.6);
  static Vec3 bowlingLook = const Vec3(0, 21.5, -0.6);

  static Camera3D bowling(Size s) => Camera3D.lookAt(
        bowlingPos,
        bowlingLook,
        focal: _focal(s, 1.07),
        cx: s.width / 2,
        cy: s.height * 0.5,
      );

  /// Close-up of the pitch while the bowler drags the target.
  static Camera3D aim(Size s) => Camera3D.lookAt(
        const Vec3(0, -5.5, 9.5),
        const Vec3(0, 15.6, 0),
        focal: _focal(s, 2.15),
        cx: s.width / 2,
        cy: s.height * 0.52,
      );

  /// Behind the striker, over the shoulder (user batting).
  static Vec3 battingPos(bool leftHanded) => Vec3(leftHanded ? 1.25 : -1.25, 27.2, 3.4);
  static Vec3 battingLook(bool leftHanded) => Vec3(leftHanded ? 0.3 : -0.3, 2.0, 0.6);

  static Camera3D batting(Size s, {required bool leftHanded}) => Camera3D.lookAt(
        battingPos(leftHanded),
        battingLook(leftHanded),
        focal: _focal(s, 1.3),
        cx: s.width / 2,
        cy: s.height * 0.47,
      );

  /// Orbiting high shot for menus.
  static Camera3D orbit(Size s, double angle, {double radius = 62, double height = 17}) => Camera3D.lookAt(
        Vec3(math.sin(angle) * radius, kPitchMid - math.cos(angle) * radius, height),
        const Vec3(0, kPitchMid, 5),
        focal: s.width > s.height ? s.height * 1.45 : s.width * 1.05,
        cx: s.width / 2,
        cy: s.height * (s.width > s.height ? 0.45 : 0.4),
      );

  /// Focal length scaled for the short screen side (landscape gameplay).
  static double _focal(Size s, double k) => math.min(s.height, s.width * 0.6) * k;

  static double focalFor(Size s, double k) => _focal(s, k);
}
