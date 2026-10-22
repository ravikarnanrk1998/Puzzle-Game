import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart';

/// Paints [text] so that [anchor] (in -1..1 alignment space) sits at [at].
Size paintText(
  Canvas canvas,
  String text,
  Offset at,
  TextStyle style, {
  Alignment anchor = Alignment.center,
  double maxWidth = 2000,
  TextAlign align = TextAlign.center,
}) {
  final tp = TextPainter(
    text: TextSpan(text: text, style: style),
    textDirection: TextDirection.ltr,
    textAlign: align,
    maxLines: 1,
  )..layout(maxWidth: maxWidth);
  final dx = at.dx - tp.width * (anchor.x + 1) / 2;
  final dy = at.dy - tp.height * (anchor.y + 1) / 2;
  tp.paint(canvas, Offset(dx, dy));
  final s = Size(tp.width, tp.height);
  tp.dispose();
  return s;
}

Path starPath(Offset c, double r, {double inner = 0.45, int points = 5}) {
  final p = Path();
  for (var i = 0; i < points * 2; i++) {
    final rad = i.isEven ? r : r * inner;
    final a = -math.pi / 2 + i * math.pi / points;
    final pt = Offset(c.dx + rad * math.cos(a), c.dy + rad * math.sin(a));
    if (i == 0) {
      p.moveTo(pt.dx, pt.dy);
    } else {
      p.lineTo(pt.dx, pt.dy);
    }
  }
  return p..close();
}

/// Scale a path about a point without deprecated Matrix4 helpers.
Path scalePath(Path path, double sx, double sy, Offset about) {
  final m = Float64List(16);
  m[0] = sx;
  m[5] = sy;
  m[10] = 1;
  m[15] = 1;
  m[12] = about.dx * (1 - sx);
  m[13] = about.dy * (1 - sy);
  return path.transform(m);
}

Offset lerpO(Offset a, Offset b, double t) => Offset(a.dx + (b.dx - a.dx) * t, a.dy + (b.dy - a.dy) * t);

double lerpD(double a, double b, double t) => a + (b - a) * t;

double clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

double easeOut(double t) => 1 - math.pow(1 - clamp01(t), 3).toDouble();

double easeInOut(double t) {
  final x = clamp01(t);
  return x < 0.5 ? 4 * x * x * x : 1 - math.pow(-2 * x + 2, 3) / 2;
}
