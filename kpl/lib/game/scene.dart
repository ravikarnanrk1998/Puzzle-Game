import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../core/theme.dart';
import '../data/teams.dart';
import '../ui/widgets/paint_utils.dart';
import 'camera.dart';
import 'character.dart';
import 'ground.dart';
import 'stadium_mesh.dart';

/// Everything the painter needs for one frame (mutated by the controller).
class SceneState {
  Camera3D Function(Size size)? camFn;
  final List<Actor> actors = [];
  Vec3? ball;
  final List<Vec3> trail = [];
  Vec3? marker;
  double markerAlpha = 0;
  Color markerColor = AppColors.cyan;
  Vec3? aim;
  bool pitchMap = false;
  bool pitchMapLabels = true;
  bool leftBatter = false;
  double farStumpsHit = -1;
  double nearStumpsHit = -1;
  Color stumpsColor = AppColors.gold;
  double hype = 0.2;
  double time = 0;
  double flash = 0;
  double? ring;
  Vec3 ringAt = const Vec3(0, 19.05, 0.7);
  final List<(Offset, double)> swipe = [];
  bool hideNearActors = false;
  ScreenEvent screen = ScreenEvent.none;
}

class ScenePainter extends CustomPainter {
  ScenePainter({required this.mesh, required this.ground, required this.s, required this.team, required Listenable repaint})
      : super(repaint: repaint);

  final StadiumMesh mesh;
  final GroundRenderer ground;
  final SceneState s;
  final TeamInfo team;

  static final _people = CharacterRenderer();

  @override
  void paint(Canvas canvas, Size size) {
    final cam = (s.camFn ?? (sz) => Rigs.bowling(sz))(size);
    ground.paint(canvas, size, cam, rx: mesh.rx, ry: mesh.ry, team: team.primary, time: s.time, day: mesh.day);
    mesh.paint(canvas, size, cam, time: s.time, hype: s.hype, event: s.screen);

    if (s.pitchMap) _pitchMap(canvas, cam);
    if (s.aim != null) _target(canvas, cam, s.aim!, AppColors.gold, 1, pulse: true);
    if (s.marker != null && s.markerAlpha > 0) _target(canvas, cam, s.marker!, s.markerColor, s.markerAlpha);

    _people.light = mesh.day ? CharacterLight.day : CharacterLight.night;
    final visible = [
      for (final a in s.actors)
        if (cam.depth(a.foot) >= 1.2 && !(s.hideNearActors && cam.depth(a.foot) < 6)) a,
    ];
    _people.prepare(visible);
    _people.drawShadows(canvas, cam, visible, day: mesh.day);

    final items = <(double, VoidCallback)>[];
    for (final a in visible) {
      items.add((cam.depth(a.foot.withZ(0.9)), () => _people.draw(canvas, cam, a)));
    }
    items.add((cam.depth(const Vec3(0, kPitchLength, 0.3)), () => _stumps(canvas, cam, kPitchLength, s.farStumpsHit)));
    items.add((cam.depth(const Vec3(0, 0, 0.3)), () => _stumps(canvas, cam, 0, s.nearStumpsHit)));
    final ball = s.ball;
    if (ball != null) items.add((cam.depth(ball) - 0.05, () => _ball(canvas, cam, ball)));
    items.sort((a, b) => b.$1.compareTo(a.$1));
    for (final it in items) {
      if (it.$1 > 0.8) it.$2();
    }

    final ring = s.ring;
    if (ring != null && cam.depth(s.ringAt) > 1) {
      final p = cam.project(s.ringAt);
      final sc = cam.scaleAt(s.ringAt);
      final base = 0.3 * sc;
      final r = base + ring.clamp(0.0, 1.0) * 1.7 * sc;
      final good = ring < 0.12;
      final col = good ? AppColors.green : Color.lerp(AppColors.gold, AppColors.cyan, ring.clamp(0.0, 1.0))!;
      canvas.drawCircle(p, base, Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5
        ..color = Colors.white.o(0.5));
      canvas.drawCircle(p, r, Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = good ? 4 : 2.6
        ..color = col.o(0.95));
      canvas.drawCircle(p, r, Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 9
        ..color = col.o(0.22)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 5));
    }

    _swipe(canvas);

    final rect = Offset.zero & size;
    canvas.drawRect(
      rect,
      Paint()
        ..shader = RadialGradient(
          radius: 0.9,
          colors: [Colors.transparent, Colors.black.o(0.42)],
          stops: const [0.62, 1.0],
        ).createShader(rect),
    );
    if (s.flash > 0) canvas.drawRect(rect, Paint()..color = Colors.white.o(s.flash.clamp(0.0, 1.0)));
  }

  void _pitchMap(Canvas c, Camera3D cam) {
    const zones = <(double, double, String, Color)>[
      (0.0, 2.0, 'YORKER', Color(0xFFFF4D5E)),
      (2.0, 4.5, 'FULL', Color(0xFFFFB020)),
      (4.5, 7.2, 'GOOD', Color(0xFF2EE59D)),
      (7.2, 9.6, 'SHORT', Color(0xFF38E8FF)),
      (9.6, 12.0, 'BOUNCER', Color(0xFFB57BFF)),
    ];
    for (final z in zones) {
      final y0 = kPitchLength - z.$2, y1 = kPitchLength - z.$1;
      final q = [Vec3(-1.45, y0, 0.01), Vec3(1.45, y0, 0.01), Vec3(1.45, y1, 0.01), Vec3(-1.45, y1, 0.01)];
      if (q.any((v) => cam.depth(v) < 1)) continue;
      final pts = q.map(cam.project).toList();
      c.drawPath(Path()..addPolygon(pts, true), Paint()..color = z.$4.o(0.2));
      c.drawLine(pts[0], pts[1], Paint()
        ..color = z.$4.o(0.7)
        ..strokeWidth = 1.2);
      final mid = cam.project(Vec3(1.95, (y0 + y1) / 2, 0.01));
      final sc = cam.scaleAt(Vec3(0, (y0 + y1) / 2, 0));
      if (sc > 10 && s.pitchMapLabels) {
        paintText(
          c,
          z.$3,
          mid,
          T.display(math.min(20, math.max(13, sc * 0.6)), color: z.$4, weight: FontWeight.w700, shadows: const [Shadow(color: Colors.black, blurRadius: 4)]),
          anchor: Alignment.centerLeft,
        );
      }
    }
  }

  void _target(Canvas c, Camera3D cam, Vec3 at, Color color, double alpha, {bool pulse = false}) {
    if (cam.depth(at) < 1) return;
    final p = cam.project(at);
    final sc = cam.scaleAt(at);
    final k = pulse ? 1 + 0.12 * math.sin(s.time * 6) : 1.0;
    final rect = Rect.fromCenter(center: p, width: 0.62 * sc * k, height: 0.62 * sc * k * _squash(cam, at));
    c.drawOval(rect.inflate(5), Paint()
      ..color = color.o(0.3 * alpha)
      ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6));
    c.drawOval(rect, Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.4
      ..color = color.o(alpha));
    c.drawOval(rect.deflate(rect.width * 0.3), Paint()..color = color.o(0.55 * alpha));
    if (pulse) {
      final cross = Paint()
        ..color = color.o(0.9)
        ..strokeWidth = 1.4;
      c.drawLine(Offset(p.dx - rect.width * 0.8, p.dy), Offset(p.dx + rect.width * 0.8, p.dy), cross);
      c.drawLine(Offset(p.dx, p.dy - rect.height * 1.2), Offset(p.dx, p.dy + rect.height * 1.2), cross);
    }
  }

  double _squash(Camera3D cam, Vec3 at) {
    final a = cam.project(at), b = cam.project(Vec3(at.x, at.y + 0.3, 0));
    final w = cam.scaleAt(at) * 0.3;
    return ((a - b).distance / math.max(1, w)).clamp(0.15, 1.0);
  }

  void _ball(Canvas c, Camera3D cam, Vec3 b) {
    final shadow = b.ground;
    if (cam.depth(shadow) > 1) {
      final sp = cam.project(shadow);
      final ss = cam.scaleAt(shadow);
      final a = (0.55 * (1 - math.min(1.0, b.z / 6))).clamp(0.06, 0.55);
      c.drawOval(Rect.fromCenter(center: sp, width: 0.18 * ss, height: 0.07 * ss), Paint()..color = Colors.black.o(a));
    }
    final trail = s.trail;
    for (var i = 0; i < trail.length - 1; i++) {
      final t0 = trail[i], t1 = trail[i + 1];
      if (cam.depth(t0) < 1 || cam.depth(t1) < 1) continue;
      final k = (i + 1) / trail.length;
      c.drawLine(cam.project(t0), cam.project(t1), Paint()
        ..color = Colors.white.o(0.32 * k)
        ..strokeWidth = math.max(1.5, 0.06 * cam.scaleAt(t1)) * k
        ..strokeCap = StrokeCap.round);
    }
    final p = cam.project(b);
    final r = math.max(2.8, 0.04 * cam.scaleAt(b) * 1.9);
    c.drawCircle(p, r * 2.6, Paint()
      ..color = Colors.white.o(0.25)
      ..maskFilter = MaskFilter.blur(BlurStyle.normal, r * 1.2));
    c.drawCircle(
      p,
      r,
      Paint()
        ..shader = RadialGradient(
          center: const Alignment(-0.35, -0.4),
          colors: const [Colors.white, Color(0xFFE9EDF4), Color(0xFFA9B2C2)],
          stops: const [0, 0.6, 1],
        ).createShader(Rect.fromCircle(center: p, radius: r)),
    );
  }

  void _stumps(Canvas c, Camera3D cam, double y, double hit) {
    const xs = [-0.114, 0.0, 0.114];
    if (cam.depth(Vec3(0, y, 0)) < 1) return;
    final sc = cam.scaleAt(Vec3(0, y, 0));
    final lit = hit >= 0 && hit < 1.6 && ((hit * 8).floor().isEven);
    final wood = Paint()
      ..strokeCap = StrokeCap.round
      ..strokeWidth = math.max(1.6, 0.038 * sc);
    for (var i = 0; i < 3; i++) {
      final base = Vec3(xs[i], y, 0);
      var top = Vec3(xs[i], y, 0.71);
      if (hit >= 0) {
        final t = math.min(1.0, hit / 0.35);
        final e = 1 - (1 - t) * (1 - t);
        final side = (i - 1) * 0.5 + 0.15;
        final away = y > 10 ? 0.62 : -0.62;
        top = Vec3(xs[i] + math.sin(side) * 0.55 * e, y + away * e, 0.71 - 0.42 * e);
      }
      final pb = cam.project(base), pt = cam.project(top);
      if (lit) {
        c.drawLine(pb, pt, Paint()
          ..color = const Color(0xFFFF3B30).o(0.6)
          ..strokeWidth = wood.strokeWidth * 3
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 4));
      }
      wood.color = lit ? const Color(0xFFFF6B5E) : const Color(0xFFF3F1EC);
      c.drawLine(pb, pt, wood);
      wood.color = (lit ? const Color(0xFFFFD166) : s.stumpsColor).o(0.9);
      c.drawLine(Offset.lerp(pb, pt, 0.55)!, Offset.lerp(pb, pt, 0.78)!, wood);
    }
    final bail = Paint()
      ..strokeCap = StrokeCap.round
      ..strokeWidth = math.max(1.2, 0.025 * sc)
      ..color = lit ? const Color(0xFFFFE066) : const Color(0xFFF6F3E8);
    for (var b = 0; b < 2; b++) {
      var a = Vec3(xs[b], y, 0.725), z = Vec3(xs[b + 1], y, 0.725);
      if (hit >= 0) {
        final t = math.min(hit, 1.0);
        final up = 3.2 * t - 4.9 * t * t;
        final dx = (b == 0 ? -1 : 1) * 0.9 * t;
        final dy = (y > 10 ? 1.1 : -1.1) * t;
        a = Vec3(a.x + dx, y + dy, math.max(0, 0.725 + up));
        z = Vec3(z.x + dx + 0.1 * math.sin(t * 9), y + dy + 0.08 * math.cos(t * 9), math.max(0, 0.725 + up + 0.06));
      }
      if (cam.depth(a) < 1) continue;
      if (lit) {
        c.drawLine(cam.project(a), cam.project(z), Paint()
          ..color = const Color(0xFFFFD60A).o(0.7)
          ..strokeWidth = bail.strokeWidth * 4
          ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 5));
      }
      c.drawLine(cam.project(a), cam.project(z), bail);
    }
  }

  void _swipe(Canvas c) {
    final pts = s.swipe;
    if (pts.length < 2) return;
    for (var i = 0; i < pts.length - 1; i++) {
      final age = s.time - pts[i].$2;
      final a = (1 - age / 0.45).clamp(0.0, 1.0);
      if (a <= 0) continue;
      final w = 3 + 9 * a * (i / pts.length);
      c.drawLine(pts[i].$1, pts[i + 1].$1, Paint()
        ..color = AppColors.cyan.o(0.35 * a)
        ..strokeWidth = w * 2.2
        ..strokeCap = StrokeCap.round
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6));
      c.drawLine(pts[i].$1, pts[i + 1].$1, Paint()
        ..color = Colors.white.o(0.9 * a)
        ..strokeWidth = w * 0.5
        ..strokeCap = StrokeCap.round);
    }
  }

  @override
  bool shouldRepaint(covariant ScenePainter old) => true;
}
