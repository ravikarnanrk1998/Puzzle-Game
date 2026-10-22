import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../core/theme.dart';
import 'camera.dart';

/// Renders the ground (sky, grass, pitch, creases, rope) with the GPU shader
/// in `shaders/ground.frag`, or a simpler polygon version if shaders are
/// unavailable on the device.
class GroundRenderer {
  static ui.FragmentProgram? _program;
  static Future<void>? _loading;

  /// Multiplier applied to FlutterFragCoord (kept configurable for engines
  /// that report device pixels).
  static double fragScale = 1.0;

  static Future<void> load() => _loading ??= _load();

  static Future<void> _load() async {
    try {
      _program = await ui.FragmentProgram.fromAsset('shaders/ground.frag');
    } catch (e) {
      debugPrint('Ground shader unavailable, using fallback: $e');
    }
  }

  ui.FragmentShader? _shader;

  void dispose() {
    _shader?.dispose();
    _shader = null;
  }

  void paint(Canvas canvas, Size size, Camera3D cam, {required double rx, required double ry, required Color team, double time = 0, bool day = false}) {
    final program = _program;
    if (program == null) {
      _fallback(canvas, size, cam, rx, ry, team, day);
      return;
    }
    final s = _shader ??= program.fragmentShader();
    var i = 0;
    void f(double v) => s.setFloat(i++, v);
    f(size.width);
    f(size.height);
    f(cam.pos.x);
    f(cam.pos.y);
    f(cam.pos.z);
    f(cam.yaw);
    f(cam.pitch);
    f(cam.focal);
    f(cam.cx);
    f(cam.cy);
    f(rx);
    f(ry);
    f(time);
    f(team.r);
    f(team.g);
    f(team.b);
    f(fragScale);
    f(day ? 1 : 0);
    canvas.drawRect(Offset.zero & size, Paint()..shader = s);
  }

  void _fallback(Canvas c, Size size, Camera3D cam, double rx, double ry, Color team, bool day) {
    c.drawRect(Offset.zero & size, Paint()..color = day ? const Color(0xFF8DB6E8) : const Color(0xFF070A1E));
    final pts = <Offset>[];
    for (var k = 0; k <= 180; k++) {
      final phi = k / 180 * math.pi * 2;
      final p = Vec3(math.cos(phi) * rx, kPitchMid + math.sin(phi) * ry, 0);
      if (cam.depth(p) > 1) pts.add(cam.project(p));
    }
    if (pts.length > 2) {
      final path = Path()..addPolygon(pts, true);
      c.drawPath(path, Paint()..color = const Color(0xFF2A7F37));
    } else {
      c.drawRect(Offset.zero & size, Paint()..color = const Color(0xFF2A7F37));
    }
    final q = [const Vec3(-1.52, -2.5, 0), const Vec3(1.52, -2.5, 0), const Vec3(1.52, 22.6, 0), const Vec3(-1.52, 22.6, 0)];
    if (q.every((v) => cam.depth(v) > 0.5)) {
      c.drawPath(Path()..addPolygon(q.map(cam.project).toList(), true), Paint()..color = const Color(0xFFC8AE80));
    }
    final white = Paint()
      ..color = Colors.white.o(0.9)
      ..strokeWidth = 1.5;
    for (final y in [kBowlerCrease, kStrikerCrease]) {
      final a = Vec3(-1.83, y, 0), b = Vec3(1.83, y, 0);
      if (cam.depth(a) > 0.5) c.drawLine(cam.project(a), cam.project(b), white);
    }
  }
}
