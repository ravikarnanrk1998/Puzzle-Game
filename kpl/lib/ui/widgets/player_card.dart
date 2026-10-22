import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../models/player.dart';
import 'common.dart';
import 'crest.dart';

List<Color> tierFrame(String tier) => switch (tier) {
      'ELITE' => const [Color(0xFFFFE9A3), Color(0xFFB78CFF), Color(0xFF38E8FF), Color(0xFFFF3D9A), Color(0xFFFFE9A3)],
      'GOLD' => const [Color(0xFFFFF1B8), Color(0xFFFFC84A), Color(0xFFB8860B), Color(0xFFFFE08A)],
      'SILVER' => const [Color(0xFFFFFFFF), Color(0xFFC9D3E3), Color(0xFF7D8AA0), Color(0xFFE8EEF7)],
      _ => const [Color(0xFFFFD2A8), Color(0xFFD08A57), Color(0xFF7A4321), Color(0xFFF0B27A)],
    };

/// Premium dark trading-card for a player (used in the auction room).
class PlayerCard extends StatelessWidget {
  const PlayerCard({super.key, required this.player, this.width = 230, this.team, this.sheen});

  final Player player;
  final double width;
  final TeamInfo? team;
  final Animation<double>? sheen;

  @override
  Widget build(BuildContext context) {
    final h = width * 1.36;
    final t = team ?? (player.team26 != null ? teamById(player.team26!) : null);
    final frame = tierFrame(player.tier);
    final s = width / 230;
    return SizedBox(
      width: width,
      height: h,
      child: CustomPaint(
        painter: _CardPainter(frame: frame, tint: t?.primary ?? const Color(0xFF2A3570), glow: t?.glow ?? AppColors.cyan, sheen: sheen),
        child: Padding(
          padding: EdgeInsets.fromLTRB(16 * s, 14 * s, 16 * s, 14 * s),
          child: Column(
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      GradientText(
                        '${player.overall}',
                        style: T.display(54 * s, weight: FontWeight.w700, height: 0.9),
                        gradient: LinearGradient(colors: frame.take(3).toList(), begin: Alignment.topCenter, end: Alignment.bottomCenter),
                      ),
                      Text(player.role.code, style: T.display(20 * s, color: roleColor(player.role), weight: FontWeight.w700, height: 1)),
                      SizedBox(height: 4 * s),
                      CountryChip(player.country, size: 10.5 * s),
                    ],
                  ),
                  const Spacer(),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Pill(player.tier, color: frame[1], fontSize: 10 * s),
                      SizedBox(height: 6 * s),
                      if (t != null) TeamCrest(t, size: 34 * s),
                      if (!player.capped) ...[
                        SizedBox(height: 6 * s),
                        Pill('UNCAPPED', color: AppColors.green, fontSize: 9 * s),
                      ],
                    ],
                  ),
                ],
              ),
              Expanded(
                child: Center(child: PlayerAvatar(player, size: width * 0.46, team: t)),
              ),
              Text(
                player.surname,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: T.display(30 * s, weight: FontWeight.w700, spacing: 1.4, height: 0.95),
              ),
              Text(
                player.name.split(' ').first.toUpperCase(),
                maxLines: 1,
                style: T.label(11 * s, color: AppColors.textDim),
              ),
              SizedBox(height: 8 * s),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                children: [
                  _stat('BAT', player.bat, s),
                  _divider(s),
                  _stat('BOWL', player.bowl, s),
                  _divider(s),
                  _stat('PWR', player.power, s),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _divider(double s) => Container(width: 1, height: 22 * s, color: Colors.white.o(0.15));

  Widget _stat(String label, int v, double s) => Column(
        children: [
          Text('$v', style: T.display(22 * s, weight: FontWeight.w700, height: 1)),
          Text(label, style: T.label(9 * s)),
        ],
      );
}

class _CardPainter extends CustomPainter {
  _CardPainter({required this.frame, required this.tint, required this.glow, this.sheen}) : super(repaint: sheen);

  final List<Color> frame;
  final Color tint;
  final Color glow;
  final Animation<double>? sheen;

  Path _shape(Size s) {
    final w = s.width, h = s.height, c = w * 0.09;
    return Path()
      ..moveTo(c, 0)
      ..lineTo(w * 0.36, 0)
      ..lineTo(w * 0.4, h * 0.022)
      ..lineTo(w * 0.6, h * 0.022)
      ..lineTo(w * 0.64, 0)
      ..lineTo(w - c, 0)
      ..lineTo(w, c)
      ..lineTo(w, h - c * 1.4)
      ..lineTo(w - c * 1.4, h)
      ..lineTo(c * 1.4, h)
      ..lineTo(0, h - c * 1.4)
      ..lineTo(0, c)
      ..close();
  }

  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    final path = _shape(size);
    canvas.drawPath(
      path.shift(const Offset(0, 6)),
      Paint()
        ..color = glow.o(0.35)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 18),
    );
    canvas.drawPath(
      path,
      Paint()
        ..shader = LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [tint.darken(0.15), const Color(0xFF0B0E26), const Color(0xFF05060F)],
          stops: const [0, 0.55, 1],
        ).createShader(rect),
    );
    canvas.save();
    canvas.clipPath(path);
    // Diagonal light streaks.
    final streak = Paint()..color = Colors.white.o(0.04);
    for (var i = 0; i < 6; i++) {
      final x = size.width * (i * 0.22 - 0.2);
      canvas.drawPath(
        Path()
          ..moveTo(x, 0)
          ..lineTo(x + size.width * 0.08, 0)
          ..lineTo(x - size.width * 0.3, size.height)
          ..lineTo(x - size.width * 0.38, size.height)
          ..close(),
        streak,
      );
    }
    canvas.drawCircle(
      Offset(size.width * 0.5, size.height * 0.42),
      size.width * 0.55,
      Paint()
        ..shader = RadialGradient(colors: [glow.o(0.28), glow.o(0)])
            .createShader(Rect.fromCircle(center: Offset(size.width * 0.5, size.height * 0.42), radius: size.width * 0.55)),
    );
    final v = sheen?.value;
    if (v != null) {
      final x = -size.width + v * size.width * 3;
      final band = Rect.fromLTWH(x - size.width * 0.3, 0, size.width * 0.6, size.height);
      canvas.drawRect(
        band,
        Paint()
          ..shader = LinearGradient(
            colors: [Colors.white.o(0), Colors.white.o(0.16), Colors.white.o(0)],
            transform: const GradientRotation(math.pi / 9),
          ).createShader(band),
      );
    }
    canvas.restore();
    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3
        ..shader = SweepGradient(colors: frame, transform: GradientRotation((v ?? 0) * math.pi * 2)).createShader(rect),
    );
    canvas.drawPath(
      _shape(Size(size.width - 12, size.height - 12)).shift(const Offset(6, 6)),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..color = frame[1].o(0.35),
    );
  }

  @override
  bool shouldRepaint(covariant _CardPainter old) => old.tint != tint || old.frame != frame;
}
