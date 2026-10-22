import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../models/player.dart';
import 'paint_utils.dart';

/// Procedurally drawn franchise crest (shield + sash + title stars).
class TeamCrest extends StatelessWidget {
  const TeamCrest(this.team, {super.key, this.size = 48, this.glow = false});

  final TeamInfo team;
  final double size;
  final bool glow;

  @override
  Widget build(BuildContext context) {
    final crest = CustomPaint(size: Size(size, size * 1.12), painter: CrestPainter(team));
    if (!glow) return crest;
    return DecoratedBox(
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        boxShadow: [BoxShadow(color: team.glow.o(0.5), blurRadius: size * 0.5, spreadRadius: -size * 0.08)],
      ),
      child: crest,
    );
  }
}

class CrestPainter extends CustomPainter {
  CrestPainter(this.team);

  final TeamInfo team;

  static Path shield(Size s) {
    final w = s.width, h = s.height;
    return Path()
      ..moveTo(w * 0.5, 0)
      ..quadraticBezierTo(w * 0.74, h * 0.075, w * 0.98, h * 0.08)
      ..lineTo(w * 0.94, h * 0.56)
      ..quadraticBezierTo(w * 0.88, h * 0.85, w * 0.5, h)
      ..quadraticBezierTo(w * 0.12, h * 0.85, w * 0.06, h * 0.56)
      ..lineTo(w * 0.02, h * 0.08)
      ..quadraticBezierTo(w * 0.26, h * 0.075, w * 0.5, 0)
      ..close();
  }

  Color get _rim => team.secondary.luma < 0.2 ? AppColors.gold : team.secondary;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final path = shield(size);
    final rect = Offset.zero & size;
    final p = team.primary;
    canvas.drawShadow(path, Colors.black, w * 0.05, false);
    canvas.drawPath(
      path,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [p.lighten(0.24), p, p.darken(0.42)],
        ).createShader(rect),
    );
    canvas.save();
    canvas.clipPath(path);
    final band = Path()
      ..moveTo(-w * 0.1, h * 0.66)
      ..lineTo(w * 1.1, h * 0.38)
      ..lineTo(w * 1.1, h * 0.5)
      ..lineTo(-w * 0.1, h * 0.78)
      ..close();
    canvas.drawPath(band, Paint()..color = _rim.o(0.8));
    canvas.drawPath(band.shift(Offset(0, h * 0.14)), Paint()..color = _rim.o(0.18));
    final gloss = Rect.fromLTWH(0, 0, w, h * 0.48);
    canvas.drawRect(
      gloss,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Colors.white.o(0.34), Colors.white.o(0)],
        ).createShader(gloss),
    );
    canvas.restore();

    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = w * 0.055
        ..color = _rim,
    );
    canvas.drawPath(
      scalePath(path, 0.84, 0.84, Offset(w * 0.5, h * 0.52)),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = w * 0.014
        ..color = Colors.white.o(0.42),
    );

    final n = team.titles;
    for (var i = 0; i < n; i++) {
      final c = Offset(w * 0.5 + (i - (n - 1) / 2) * w * 0.12, h * 0.21);
      canvas.drawPath(starPath(c, w * 0.048), Paint()..color = AppColors.goldLight);
    }

    final id = team.id;
    final fs = id.length >= 4 ? w * 0.27 : (id.length == 3 ? w * 0.34 : w * 0.42);
    paintText(
      canvas,
      id,
      Offset(w * 0.5, h * 0.55),
      T.display(
        fs,
        color: Colors.white,
        weight: FontWeight.w700,
        spacing: 0.5,
        height: 1,
        shadows: [Shadow(color: Colors.black.o(0.6), blurRadius: w * 0.05, offset: Offset(0, w * 0.015))],
      ),
    );
  }

  @override
  bool shouldRepaint(covariant CrestPainter old) => old.team.id != team.id;
}

/// Rim-lit silhouette portrait in team kit with the jersey number.
class PlayerAvatar extends StatelessWidget {
  const PlayerAvatar(this.player, {super.key, this.size = 56, this.team});

  final Player player;
  final double size;
  final TeamInfo? team;

  @override
  Widget build(BuildContext context) {
    final t = team ?? (player.team26 != null ? teamById(player.team26!) : null);
    return CustomPaint(size: Size.square(size), painter: AvatarPainter(player, t));
  }
}

class AvatarPainter extends CustomPainter {
  AvatarPainter(this.player, this.team);

  final Player player;
  final TeamInfo? team;

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final c = team?.primary ?? const Color(0xFF34407A);
    final g = team?.glow ?? AppColors.cyan;
    final sec = team?.secondary ?? Colors.white;
    final trim = sec.luma < 0.2 ? AppColors.gold : sec;
    final rect = Offset.zero & size;
    final center = Offset(w / 2, w / 2);
    const skin = Color(0xFF8A5A3B);

    canvas.save();
    canvas.clipPath(Path()..addOval(rect));
    // Dark studio backdrop with a soft spotlight behind the head.
    canvas.drawRect(
      rect,
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFF1A1F4A), Color(0xFF070918)],
        ).createShader(rect),
    );
    canvas.drawCircle(
      Offset(w * 0.5, w * 0.36),
      w * 0.48,
      Paint()..shader = RadialGradient(colors: [g.o(0.55), g.o(0)]).createShader(Rect.fromCircle(center: Offset(w * 0.5, w * 0.36), radius: w * 0.48)),
    );

    // Jersey.
    final shoulders = Path()
      ..moveTo(w * 0.02, w * 1.04)
      ..cubicTo(w * 0.06, w * 0.8, w * 0.22, w * 0.72, w * 0.5, w * 0.7)
      ..cubicTo(w * 0.78, w * 0.72, w * 0.94, w * 0.8, w * 0.98, w * 1.04)
      ..close();
    final sRect = Rect.fromLTWH(0, w * 0.68, w, w * 0.36);
    canvas.drawPath(
      shoulders,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [c.lighten(0.25), c, c.darken(0.4)],
        ).createShader(sRect),
    );
    for (final sx in [-1.0, 1.0]) {
      canvas.drawPath(
        Path()
          ..moveTo(w * (0.5 + sx * 0.2), w * 0.73)
          ..quadraticBezierTo(w * (0.5 + sx * 0.36), w * 0.76, w * (0.5 + sx * 0.44), w * 0.9)
          ..lineTo(w * (0.5 + sx * 0.4), w * 0.92)
          ..quadraticBezierTo(w * (0.5 + sx * 0.32), w * 0.8, w * (0.5 + sx * 0.18), w * 0.77)
          ..close(),
        Paint()..color = trim.o(0.85),
      );
    }
    canvas.drawPath(
      Path()
        ..moveTo(w * 0.4, w * 0.705)
        ..lineTo(w * 0.5, w * 0.81)
        ..lineTo(w * 0.6, w * 0.705)
        ..lineTo(w * 0.55, w * 0.705)
        ..lineTo(w * 0.5, w * 0.76)
        ..lineTo(w * 0.45, w * 0.705)
        ..close(),
      Paint()..color = trim,
    );
    // Neck + head.
    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTRB(w * 0.44, w * 0.56, w * 0.56, w * 0.73), Radius.circular(w * 0.04)),
      Paint()..color = skin.darken(0.25),
    );
    final head = Rect.fromCenter(center: Offset(w * 0.5, w * 0.45), width: w * 0.29, height: w * 0.35);
    canvas.drawOval(
      head,
      Paint()
        ..shader = RadialGradient(center: const Alignment(-0.35, -0.3), colors: [skin.lighten(0.15), skin, skin.darken(0.35)], stops: const [0, 0.5, 1]).createShader(head),
    );

    final batting = player.role == Role.bat || player.role == Role.wk;
    if (batting) {
      final dome = Rect.fromCenter(center: Offset(w * 0.5, w * 0.42), width: w * 0.37, height: w * 0.37);
      canvas.drawArc(
        dome,
        math.pi * 1.0,
        math.pi * 1.0,
        true,
        Paint()..shader = RadialGradient(center: const Alignment(-0.4, -0.6), colors: [c.lighten(0.3), c.darken(0.15), c.darken(0.5)], stops: const [0, 0.55, 1]).createShader(dome),
      );
      // Visor peak.
      canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.29, w * 0.405, w * 0.42, w * 0.05), Radius.circular(w * 0.02)),
        Paint()..color = c.darken(0.55),
      );
      // Steel grille.
      final grille = Paint()
        ..color = const Color(0xFFD7DCE4)
        ..strokeWidth = math.max(1, w * 0.016)
        ..strokeCap = StrokeCap.round;
      for (var i = 0; i < 3; i++) {
        final y = w * (0.49 + i * 0.05);
        canvas.drawLine(Offset(w * 0.37, y), Offset(w * 0.63, y), grille);
      }
      canvas.drawLine(Offset(w * 0.44, w * 0.47), Offset(w * 0.44, w * 0.6), grille);
      canvas.drawLine(Offset(w * 0.56, w * 0.47), Offset(w * 0.56, w * 0.6), grille);
      // Neck guard.
      canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.3, w * 0.43, w * 0.06, w * 0.12), Radius.circular(w * 0.02)),
        Paint()..color = c.darken(0.35),
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.64, w * 0.43, w * 0.06, w * 0.12), Radius.circular(w * 0.02)),
        Paint()..color = c.darken(0.35),
      );
    } else {
      final capRect = Rect.fromCenter(center: Offset(w * 0.5, w * 0.39), width: w * 0.31, height: w * 0.26);
      canvas.drawArc(
        capRect,
        math.pi,
        math.pi,
        true,
        Paint()..shader = RadialGradient(center: const Alignment(-0.4, -0.6), colors: [c.lighten(0.3), c.darken(0.2)]).createShader(capRect),
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(Rect.fromLTWH(w * 0.33, w * 0.375, w * 0.4, w * 0.045), Radius.circular(w * 0.02)),
        Paint()..color = c.darken(0.45),
      );
      canvas.drawCircle(Offset(w * 0.5, w * 0.265), w * 0.018, Paint()..color = trim);
      // Hair line and shadowed jaw for the uncovered face.
      canvas.drawArc(
        Rect.fromCenter(center: Offset(w * 0.5, w * 0.5), width: w * 0.27, height: w * 0.26),
        0.25,
        math.pi - 0.5,
        false,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = w * 0.035
          ..color = const Color(0xFF2A1A12).o(0.55),
      );
    }
    canvas.drawPath(
      shoulders,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = w * 0.018
        ..shader = LinearGradient(colors: [g, g.o(0)], stops: const [0, 0.55]).createShader(sRect),
    );
    paintText(
      canvas,
      '${player.jersey}',
      Offset(w * 0.5, w * 0.905),
      T.display(w * 0.17, color: trim, weight: FontWeight.w700, height: 1),
    );
    canvas.restore();
    canvas.drawCircle(
      center,
      w * 0.485,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = w * 0.03
        ..shader = SweepGradient(colors: [g, g.o(0.1), sec.o(0.6), g]).createShader(rect),
    );
  }

  @override
  bool shouldRepaint(covariant AvatarPainter old) => old.player.id != player.id || old.team?.id != team?.id;
}
