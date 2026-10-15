import 'dart:math';

import 'package:flutter/material.dart';

import '../shop/catalog.dart';

// The bike's fixed shapes, built once instead of every frame.
final Path _body = Path()
  ..moveTo(0, -30)
  ..quadraticBezierTo(11, -24, 11, -8)
  ..lineTo(9, 18)
  ..quadraticBezierTo(0, 26, -9, 18)
  ..lineTo(-11, -8)
  ..quadraticBezierTo(-11, -24, 0, -30)
  ..close();

final Path _windshield = Path()
  ..moveTo(-7, -22)
  ..quadraticBezierTo(0, -29, 7, -22)
  ..lineTo(5, -17)
  ..lineTo(-5, -17)
  ..close();

const Rect _glowArea = Rect.fromLTWH(-34, -34, 68, 68);

/// Draws a top-down motorbike facing up, centred on the origin
/// (about 30 x 62 units). Shared by the game, the shop and the app icon.
void paintBike(Canvas canvas, BikeSkin skin,
    {bool boosting = false, double time = 0}) {
  final color = skin.color;
  final dark = Color.lerp(color, Colors.black, 0.45)!;
  final light = skin.accent;

  if (skin.glow) {
    // A soft oval halo. A gradient gives the same look as a blur filter at
    // a fraction of the cost, which matters as it is drawn every frame.
    final pulse = 0.55 + 0.25 * sin(time * 5);
    canvas.save();
    canvas.scale(1, 78 / 46);
    canvas.drawCircle(
      Offset.zero,
      34,
      Paint()
        ..shader = RadialGradient(
          colors: [
            skin.accent.withValues(alpha: pulse),
            skin.accent.withValues(alpha: pulse * 0.5),
            skin.accent.withValues(alpha: 0),
          ],
          stops: const [0.35, 0.65, 1],
        ).createShader(_glowArea),
    );
    canvas.restore();
  }

  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: const Offset(5, 7), width: 26, height: 62),
      const Radius.circular(13),
    ),
    Paint()..color = const Color(0x55000000),
  );

  if (boosting) {
    final len = 26 + sin(time * 60) * 6;
    canvas.drawPath(
      Path()
        ..moveTo(-6, 26)
        ..quadraticBezierTo(0, 26 + len * 1.3, 6, 26)
        ..close(),
      Paint()..color = const Color(0xFFFF6D00),
    );
    canvas.drawPath(
      Path()
        ..moveTo(-3, 26)
        ..quadraticBezierTo(0, 26 + len * 0.8, 3, 26)
        ..close(),
      Paint()..color = const Color(0xFFFFF176),
    );
  }

  final tyre = Paint()..color = const Color(0xFF1A1A1A);
  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: const Offset(0, 20), width: 11, height: 22),
      const Radius.circular(5),
    ),
    tyre,
  );
  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: const Offset(0, -22), width: 10, height: 20),
      const Radius.circular(5),
    ),
    tyre,
  );

  canvas.drawPath(_body, Paint()..color = color);
  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: const Offset(0, -10), width: 4, height: 30),
      const Radius.circular(2),
    ),
    Paint()..color = light,
  );
  canvas.drawPath(_windshield, Paint()..color = const Color(0xCC90CAF9));

  canvas.drawLine(
    const Offset(-15, -13),
    const Offset(15, -13),
    Paint()
      ..color = const Color(0xFF424242)
      ..strokeWidth = 3.5
      ..strokeCap = StrokeCap.round,
  );

  // Rider: arms, body and helmet.
  final arm = Paint()
    ..color = dark
    ..strokeWidth = 5
    ..strokeCap = StrokeCap.round;
  canvas.drawLine(const Offset(-8, 0), const Offset(-14, -12), arm);
  canvas.drawLine(const Offset(8, 0), const Offset(14, -12), arm);
  canvas.drawOval(
    Rect.fromCenter(center: const Offset(0, 4), width: 20, height: 24),
    Paint()..color = dark,
  );
  canvas.drawCircle(const Offset(0, -3), 8.5, Paint()..color = light);
  canvas.drawArc(
    Rect.fromCircle(center: const Offset(0, -3), radius: 8.5),
    pi * 1.2,
    pi * 0.6,
    false,
    Paint()
      ..color = const Color(0xFF212121)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3.5,
  );

  canvas.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromCenter(center: const Offset(0, 24), width: 8, height: 3),
      const Radius.circular(1.5),
    ),
    Paint()..color = const Color(0xFFFF1744),
  );
}
