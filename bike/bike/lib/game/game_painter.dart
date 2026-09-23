import 'dart:math';

import 'package:flutter/material.dart';

import 'bike_art.dart';
import 'bike_game.dart';

/// Draws the whole game world with plain canvas shapes (no image assets).
class GamePainter extends CustomPainter {
  GamePainter(this.game) : super(repaint: game);

  final BikeGame game;

  static final Map<int, TextPainter> _icons = {};

  @override
  void paint(Canvas canvas, Size size) {
    final g = game;
    canvas.save();
    canvas.scale(g.scale);
    canvas.translate(
      g.offsetX + sin(g.time * 93) * g.shake,
      cos(g.time * 71) * g.shake,
    );

    _drawGround(canvas);
    _drawScenery(canvas);
    _drawRoad(canvas);
    _drawPickups(canvas);
    for (final v in g.vehicles) {
      _drawVehicle(canvas, v);
    }
    _drawParticles(canvas);
    _drawPlayer(canvas);
    if (g.boosting) _drawSpeedLines(canvas);
    _drawTexts(canvas);
    _drawBanner(canvas);
    canvas.restore();

    // Darken the top edge so the HUD stays readable.
    final top = Rect.fromLTWH(0, 0, size.width, 110);
    canvas.drawRect(
      top,
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0x88000000), Color(0x00000000)],
        ).createShader(top),
    );
  }

  void _drawGround(Canvas canvas) {
    final g = game;
    final left = -g.offsetX - 30;
    final width = g.viewWidth + 60;
    canvas.drawRect(Rect.fromLTWH(left, -30, width, g.viewHeight + 60),
        Paint()..color = const Color(0xFF66BB6A));
    final stripe = Paint()..color = const Color(0xFF5CAF60);
    for (var y = -160.0 + g.scroll % 160; y < g.viewHeight + 30; y += 160) {
      canvas.drawRect(Rect.fromLTWH(left, y, width, 80), stripe);
    }
  }

  void _drawScenery(Canvas canvas) {
    final shadow = Paint()..color = const Color(0x33000000);
    for (final s in game.scenery) {
      final c = Offset(s.x, s.y);
      switch (s.kind) {
        case SceneryKind.tree:
          final r = 22 * s.scale;
          canvas.drawCircle(c + const Offset(6, 8), r, shadow);
          canvas.drawCircle(c, r, Paint()..color = const Color(0xFF2E7D32));
          canvas.drawCircle(c + Offset(-r * 0.2, -r * 0.2), r * 0.7,
              Paint()..color = const Color(0xFF388E3C));
          canvas.drawCircle(c + Offset(-r * 0.35, -r * 0.35), r * 0.3,
              Paint()..color = const Color(0xFF4CAF50));
        case SceneryKind.bush:
          final r = 9 * s.scale;
          final parts = [Offset(-r, 0), Offset(r, 0), Offset(0, -r * 0.8)];
          for (final o in parts) {
            canvas.drawCircle(c + o + const Offset(3, 4), r, shadow);
          }
          for (final o in parts) {
            canvas.drawCircle(
                c + o, r, Paint()..color = const Color(0xFF558B2F));
            canvas.drawCircle(c + o + Offset(-r * 0.3, -r * 0.3), r * 0.45,
                Paint()..color = const Color(0xFF7CB342));
          }
        case SceneryKind.flowers:
          const colors = [
            Color(0xFFFF80AB),
            Color(0xFFFFFF8D),
            Colors.white,
            Color(0xFFEA80FC),
          ];
          for (var i = 0; i < 6; i++) {
            final a = i * 1.1 + s.x;
            canvas.drawCircle(
              c + Offset(cos(a) * 12 * s.scale, sin(a) * 9 * s.scale),
              3,
              Paint()..color = colors[i % colors.length],
            );
          }
      }
    }
  }

  void _drawRoad(Canvas canvas) {
    final g = game;
    const l = BikeGame.roadLeft;
    const r = BikeGame.roadRight;
    final h = g.viewHeight + 60;
    final dashStart = -80.0 + g.scroll % 80;

    // Red/white curbs.
    final white = Paint()..color = Colors.white;
    final red = Paint()..color = const Color(0xFFE53935);
    canvas.drawRect(Rect.fromLTWH(l - 14, -30, 14, h), white);
    canvas.drawRect(Rect.fromLTWH(r, -30, 14, h), white);
    for (var y = dashStart; y < g.viewHeight + 30; y += 80) {
      canvas.drawRect(Rect.fromLTWH(l - 14, y, 14, 40), red);
      canvas.drawRect(Rect.fromLTWH(r, y, 14, 40), red);
    }

    // Asphalt with faint tyre tracks.
    canvas.drawRect(Rect.fromLTWH(l, -30, BikeGame.roadWidth, h),
        Paint()..color = const Color(0xFF3D4049));
    final track = Paint()..color = const Color(0x14000000);
    for (var i = 0; i < BikeGame.laneCount; i++) {
      final cx = BikeGame.laneCenter(i);
      canvas.drawRect(Rect.fromLTWH(cx - 14, -30, 8, h), track);
      canvas.drawRect(Rect.fromLTWH(cx + 6, -30, 8, h), track);
    }

    final edge = Paint()..color = const Color(0xDDFFFFFF);
    canvas.drawRect(Rect.fromLTWH(l + 5, -30, 4, h), edge);
    canvas.drawRect(Rect.fromLTWH(r - 9, -30, 4, h), edge);

    final dash = Paint()..color = const Color(0xE6FFFFFF);
    for (var i = 1; i < BikeGame.laneCount; i++) {
      final x = l + BikeGame.laneWidth * i;
      for (var y = dashStart; y < g.viewHeight + 30; y += 80) {
        canvas.drawRRect(
          RRect.fromRectAndRadius(
              Rect.fromLTWH(x - 2.5, y, 5, 42), const Radius.circular(2)),
          dash,
        );
      }
    }
  }

  void _drawPickups(Canvas canvas) {
    final g = game;
    for (final p in g.pickups) {
      final c = Offset(BikeGame.laneCenter(p.lane), p.y);
      if (p.kind == PickupKind.coin) {
        // Squash horizontally to fake a spinning coin.
        final spin = cos(g.time * 5 + p.y * 0.03).abs();
        final w = 22 * (0.25 + 0.75 * spin);
        canvas.drawOval(
            Rect.fromCenter(
                center: c + const Offset(3, 4), width: w, height: 22),
            Paint()..color = const Color(0x33000000));
        canvas.drawOval(Rect.fromCenter(center: c, width: w, height: 22),
            Paint()..color = const Color(0xFFFFA000));
        canvas.drawOval(
            Rect.fromCenter(center: c, width: w * 0.72, height: 16),
            Paint()..color = const Color(0xFFFFD54F));
        canvas.drawOval(
            Rect.fromCenter(
                center: c + Offset(-w * 0.12, -4),
                width: w * 0.25,
                height: 5),
            Paint()..color = const Color(0xCCFFFFFF));
        continue;
      }
      final (Color color, IconData icon) = switch (p.kind) {
        PickupKind.nitro => (const Color(0xFF2979FF), Icons.bolt),
        PickupKind.shield => (const Color(0xFF00BFA5), Icons.shield),
        _ => (const Color(0xFFFF4081), Icons.favorite),
      };
      final pulse = 1 + sin(g.time * 6) * 0.08;
      canvas.drawCircle(
          c, 27 * pulse, Paint()..color = color.withValues(alpha: 0.25));
      canvas.drawCircle(c, 19, Paint()..color = Colors.white);
      canvas.drawCircle(c, 16, Paint()..color = color);
      final tp = _icon(icon);
      tp.paint(canvas, c - Offset(tp.width / 2, tp.height / 2));
    }
  }

  void _drawVehicle(Canvas canvas, Vehicle v) {
    final w = v.width;
    final h = v.height;
    canvas.save();
    canvas.translate(BikeGame.laneCenter(v.x), v.y);
    final steer = (v.lane.toDouble() - v.x).clamp(-1.0, 1.0);
    if (steer != 0) canvas.rotate(steer * 0.2);

    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(center: const Offset(5, 7), width: w, height: h),
        const Radius.circular(12),
      ),
      Paint()..color = const Color(0x44000000),
    );
    if (v.kind == VehicleKind.truck) {
      _drawTruck(canvas, v, w, h);
    } else {
      _drawCar(canvas, v, w, h);
    }

    if (v.indicating && (game.time * 6).floor().isEven) {
      final x = v.signalDir.toDouble() * (w / 2 - 3);
      final glow = Paint()..color = const Color(0x55FFAB00);
      final amber = Paint()..color = const Color(0xFFFFAB00);
      for (final y in [-h / 2 + 5, h / 2 - 5]) {
        canvas.drawCircle(Offset(x, y), 10, glow);
        canvas.drawCircle(Offset(x, y), 4.5, amber);
      }
    }
    canvas.restore();
  }

  void _drawCar(Canvas canvas, Vehicle v, double w, double h) {
    final glass = Paint()..color = const Color(0xFF263545);
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromCenter(center: Offset.zero, width: w, height: h),
        const Radius.circular(12),
      ),
      Paint()..color = v.color,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(w / 2 - 7, -h / 2 + 10, 5, h - 20),
          const Radius.circular(3)),
      Paint()..color = const Color(0x22000000),
    );
    final mirror = Paint()..color = Color.lerp(v.color, Colors.black, 0.3)!;
    canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(-w / 2 - 4, -h * 0.2, 6, 7),
            const Radius.circular(2)),
        mirror);
    canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(w / 2 - 2, -h * 0.2, 6, 7),
            const Radius.circular(2)),
        mirror);
    // Windshield, roof and rear window. The car faces up the screen.
    canvas.drawRRect(
      RRect.fromRectAndCorners(
        Rect.fromLTWH(-w * 0.38, -h * 0.27, w * 0.76, h * 0.17),
        topLeft: const Radius.circular(8),
        topRight: const Radius.circular(8),
        bottomLeft: const Radius.circular(3),
        bottomRight: const Radius.circular(3),
      ),
      glass,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
          Rect.fromLTWH(-w * 0.36, -h * 0.08, w * 0.72, h * 0.27),
          const Radius.circular(6)),
      Paint()..color = Color.lerp(v.color, Colors.white, 0.2)!,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
          Rect.fromLTWH(-w * 0.34, h * 0.21, w * 0.68, h * 0.11),
          const Radius.circular(4)),
      glass,
    );
    _drawLights(canvas, w, h);
  }

  void _drawTruck(Canvas canvas, Vehicle v, double w, double h) {
    final cabH = h * 0.24;
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(-w / 2 + 2, -h / 2, w - 4, cabH),
          const Radius.circular(9)),
      Paint()..color = v.color,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
          Rect.fromLTWH(-w * 0.38, -h / 2 + 5, w * 0.76, cabH * 0.35),
          const Radius.circular(4)),
      Paint()..color = const Color(0xFF263545),
    );
    final top = -h / 2 + cabH + 4;
    final trailer = Rect.fromLTWH(-w / 2, top, w, h / 2 - top);
    canvas.drawRRect(RRect.fromRectAndRadius(trailer, const Radius.circular(4)),
        Paint()..color = const Color(0xFFECEFF1));
    final rib = Paint()
      ..color = const Color(0xFFCFD8DC)
      ..strokeWidth = 2;
    for (var i = 1; i < 6; i++) {
      final y = top + trailer.height * i / 6;
      canvas.drawLine(Offset(-w / 2 + 4, y), Offset(w / 2 - 4, y), rib);
    }
    canvas.drawRect(Rect.fromLTWH(-w / 2, top + trailer.height * 0.45, w, 8),
        Paint()..color = v.color);
    _drawLights(canvas, w, h);
  }

  void _drawLights(Canvas canvas, double w, double h) {
    final head = Paint()..color = const Color(0xFFFFF9C4);
    final tail = Paint()..color = const Color(0xFFD50000);
    for (final sx in [-1.0, 1.0]) {
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
              center: Offset(sx * (w / 2 - 8), -h / 2 + 4),
              width: 10,
              height: 5),
          const Radius.circular(2),
        ),
        head,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
              center: Offset(sx * (w / 2 - 8), h / 2 - 3.5),
              width: 10,
              height: 4),
          const Radius.circular(2),
        ),
        tail,
      );
    }
  }

  void _drawPlayer(Canvas canvas) {
    final g = game;
    canvas.save();
    canvas.translate(g.playerX, g.playerY);
    if (g.shieldTimer > 0) {
      final fading = g.shieldTimer < 2 && (g.time * 10).floor().isOdd;
      if (!fading) {
        final r = 44 + sin(g.time * 8) * 2;
        canvas.drawCircle(
            Offset.zero, r, Paint()..color = const Color(0x3300E5FF));
        canvas.drawCircle(
          Offset.zero,
          r,
          Paint()
            ..color = const Color(0xCC18FFFF)
            ..style = PaintingStyle.stroke
            ..strokeWidth = 3,
        );
      }
    }
    canvas.rotate(g.tilt + g.crashSpin);
    final blink = g.invincibleTimer > 0 && (g.time * 14).floor().isOdd;
    if (!blink) paintBike(canvas, g.bike, boosting: g.boosting, time: g.time);
    canvas.restore();
  }

  void _drawParticles(Canvas canvas) {
    for (final p in game.particles) {
      final t = p.progress.clamp(0.0, 1.0);
      canvas.drawCircle(
        Offset(p.x, p.y),
        p.size * (0.4 + 0.6 * t),
        Paint()..color = p.color.withValues(alpha: t),
      );
    }
  }

  void _drawSpeedLines(Canvas canvas) {
    final g = game;
    final paint = Paint()
      ..color = const Color(0x66FFFFFF)
      ..strokeWidth = 2;
    const span = BikeGame.roadWidth + 60;
    for (var i = 0; i < 18; i++) {
      final x = BikeGame.roadLeft - 30 + (i * 97.3) % span;
      final y = (g.time * 1800 + i * 211) % (g.viewHeight + 200) - 100;
      canvas.drawLine(Offset(x, y), Offset(x, y + 70), paint);
    }
  }

  void _drawTexts(Canvas canvas) {
    for (final t in game.texts) {
      final age = t.maxLife - t.life;
      final pop = age < 0.15 ? 0.5 + age / 0.15 * 0.5 : 1.0;
      final alpha = min(1.0, t.life / t.maxLife * 2);
      _text(canvas, t.text, Offset(t.x, t.y), t.size, t.color,
          alpha: alpha, scale: pop);
    }
  }

  void _drawBanner(Canvas canvas) {
    final t = game.levelBannerTimer;
    if (t <= 0) return;
    final age = 2.2 - t;
    final s = age < 0.25
        ? 0.4 + age / 0.25 * 0.8
        : (age < 0.4 ? 1.2 - (age - 0.25) / 0.15 * 0.2 : 1.0);
    final alpha = t < 0.4 ? t / 0.4 : 1.0;
    final c = Offset(BikeGame.worldWidth / 2, game.viewHeight * 0.3);
    _text(canvas, 'LEVEL ${game.level}', c, 46, const Color(0xFFFFEB3B),
        alpha: alpha, scale: s);
    _text(canvas, 'SPEED UP!  +100', c + const Offset(0, 44), 20,
        Colors.white,
        alpha: alpha);
  }

  void _text(Canvas canvas, String s, Offset center, double size, Color color,
      {double alpha = 1, double scale = 1}) {
    final tp = TextPainter(
      text: TextSpan(
        text: s,
        style: TextStyle(
          fontFamily: 'Roboto',
          fontSize: size,
          fontWeight: FontWeight.w900,
          fontStyle: FontStyle.italic,
          color: color.withValues(alpha: alpha),
          shadows: [
            Shadow(
              color: Colors.black.withValues(alpha: 0.6 * alpha),
              blurRadius: 6,
              offset: const Offset(2, 2),
            ),
          ],
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    canvas.save();
    canvas.translate(center.dx, center.dy);
    canvas.scale(scale);
    tp.paint(canvas, Offset(-tp.width / 2, -tp.height / 2));
    canvas.restore();
    tp.dispose();
  }

  TextPainter _icon(IconData icon) {
    return _icons.putIfAbsent(
      icon.codePoint,
      () => TextPainter(
        text: TextSpan(
          text: String.fromCharCode(icon.codePoint),
          style: TextStyle(
            fontFamily: icon.fontFamily,
            package: icon.fontPackage,
            fontSize: 22,
            color: Colors.white,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout(),
    );
  }

  @override
  bool shouldRepaint(GamePainter oldDelegate) => oldDelegate.game != game;
}
