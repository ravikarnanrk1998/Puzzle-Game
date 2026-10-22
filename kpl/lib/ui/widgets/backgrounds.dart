import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/theme.dart';

/// Slowly drifting colour blobs + a neon perspective floor grid.
class AuroraBackground extends StatefulWidget {
  const AuroraBackground({super.key, this.colors, this.grid = true});

  final List<Color>? colors;
  final bool grid;

  @override
  State<AuroraBackground> createState() => _AuroraBackgroundState();
}

class _AuroraBackgroundState extends State<AuroraBackground> with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: const Duration(seconds: 24))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: CustomPaint(
        painter: _AuroraPainter(
          _c,
          widget.colors ?? const [AppColors.violet, AppColors.cyan, AppColors.pink, AppColors.goldDeep],
          widget.grid,
        ),
        child: const SizedBox.expand(),
      ),
    );
  }
}

class _AuroraPainter extends CustomPainter {
  _AuroraPainter(this.anim, this.colors, this.grid) : super(repaint: anim);

  final Animation<double> anim;
  final List<Color> colors;
  final bool grid;

  @override
  void paint(Canvas canvas, Size size) {
    final t = anim.value * 2 * math.pi;
    final rect = Offset.zero & size;
    canvas.drawRect(
      rect,
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFF0E1446), Color(0xFF070A22), Color(0xFF03040B)],
          stops: [0, 0.55, 1],
        ).createShader(rect),
    );

    final blobs = <(double, double, double)>[
      (0.2 + 0.12 * math.sin(t), 0.16 + 0.08 * math.cos(t * 1.3), 0.78),
      (0.86 + 0.1 * math.cos(t * 0.8), 0.32 + 0.1 * math.sin(t * 1.1), 0.66),
      (0.5 + 0.2 * math.sin(t * 0.6 + 1), 0.78 + 0.07 * math.cos(t * 0.9), 0.82),
      (0.08 + 0.08 * math.cos(t * 1.4 + 2), 0.6 + 0.1 * math.sin(t * 0.7), 0.56),
    ];
    for (var i = 0; i < blobs.length; i++) {
      final b = blobs[i];
      final c = colors[i % colors.length];
      final center = Offset(b.$1 * size.width, b.$2 * size.height);
      final r = b.$3 * size.width;
      canvas.drawCircle(
        center,
        r,
        Paint()
          ..shader = RadialGradient(colors: [c.o(0.30), c.o(0.0)])
              .createShader(Rect.fromCircle(center: center, radius: r)),
      );
    }

    if (grid) {
      final horizon = size.height * 0.7;
      final cx = size.width / 2;
      final line = Paint()
        ..color = AppColors.cyan.o(0.07)
        ..strokeWidth = 1;
      for (var i = -14; i <= 14; i++) {
        canvas.drawLine(
          Offset(cx + i * 7, horizon),
          Offset(cx + i * size.width * 0.32, size.height),
          line,
        );
      }
      final shift = (anim.value * 12) % 1.0;
      for (var k = 0; k < 12; k++) {
        final f = (k + shift) / 12;
        final y = horizon + (size.height - horizon) * f * f;
        line.color = AppColors.cyan.o(0.02 + 0.08 * f);
        canvas.drawLine(Offset(0, y), Offset(size.width, y), line);
      }
      final glowRect = Rect.fromLTWH(0, horizon - 50, size.width, 100);
      canvas.drawRect(
        glowRect,
        Paint()
          ..shader = LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [AppColors.violet.o(0), AppColors.violet.o(0.13), AppColors.violet.o(0)],
          ).createShader(glowRect),
      );
    }

    canvas.drawRect(
      rect,
      Paint()
        ..shader = RadialGradient(
          colors: [Colors.transparent, Colors.black.o(0.55)],
          stops: const [0.55, 1.0],
        ).createShader(rect),
    );
  }

  @override
  bool shouldRepaint(covariant _AuroraPainter old) => old.colors != colors || old.grid != grid;
}
