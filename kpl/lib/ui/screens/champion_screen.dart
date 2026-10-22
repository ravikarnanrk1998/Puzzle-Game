import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/players_db.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../engine/season.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/effects.dart';
import '../widgets/paint_utils.dart';
import 'retention_screen.dart';

class ChampionScreen extends StatefulWidget {
  const ChampionScreen({super.key});

  @override
  State<ChampionScreen> createState() => _ChampionScreenState();
}

class _ChampionScreenState extends State<ChampionScreen> with SingleTickerProviderStateMixin {
  late final AnimationController _rays = AnimationController(vsync: this, duration: const Duration(seconds: 30))..repeat();
  int _confetti = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => setState(() => _confetti = 1));
  }

  @override
  void dispose() {
    _rays.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = Career.instance;
    final s = c.season!;
    final champ = teamById(s.champion!);
    final runner = teamById(s.runnerUp!);
    final userWon = s.champion == c.userTeamId;
    final pos = s.positionOf(c.userTeamId);
    final fin = s.fixtures.lastWhere((f) => f.kind == FixtureKind.finalMatch);
    return Scaffold(
      backgroundColor: AppColors.bg0,
      body: Stack(
        children: [
          Positioned.fill(child: CustomPaint(painter: _RaysPainter(_rays, champ.glow))),
          const Positioned.fill(child: FireworksLayer(rate: 1.8)),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(14, 8, 14, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(
                    flex: 11,
                    child: Column(
                      children: [
                        Text('${League.name.toUpperCase()} ${s.year} • 20TH EDITION', style: T.label(12, color: AppColors.goldLight)),
                        Shine(
                          child: GradientText(
                            userWon ? 'CHAMPIONS!' : 'CHAMPIONS',
                            style: T.display(62, weight: FontWeight.w700, height: 0.95, spacing: 3, shadows: const [Shadow(color: Color(0xAAFF8A00), blurRadius: 30)]),
                          ),
                        ),
                        Expanded(
                          child: LayoutBuilder(builder: (context, box) {
                            final h = box.maxHeight;
                            return Stack(
                              alignment: Alignment.center,
                              children: [
                                CustomPaint(size: Size(h * 0.76, h), painter: const TrophyPainter()),
                                Positioned(bottom: 0, child: TeamCrest(champ, size: h * 0.3, glow: true)),
                              ],
                            );
                          }),
                        ),
                        const SizedBox(height: 4),
                        Text(champ.name.toUpperCase(), textAlign: TextAlign.center, style: T.display(30, weight: FontWeight.w700, height: 1)),
                        Text('${fin.result!.text} in the final vs ${runner.id}', textAlign: TextAlign.center, style: T.ui(13.5, color: AppColors.textDim)),
                        if (!userWon) Text('Your ${c.userTeam.id} finished ${ordinal(pos)} in the league', style: T.ui(13, color: AppColors.goldLight)),
                      ],
                    ),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    flex: 10,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const SectionTitle('Season awards'),
                        Expanded(child: ListView(padding: EdgeInsets.zero, children: _awards(s))),
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Expanded(
                              flex: 3,
                              child: NeonButton(
                                label: 'SEASON ${c.year + 1}',
                                subtitle: 'Releases + mini auction',
                                icon: Icons.autorenew_rounded,
                                height: 50,
                                fontSize: 20,
                                onTap: () {
                                  c.startNextSeason();
                                  Navigator.of(context).pushReplacement(fadeRoute(const RetentionScreen()));
                                },
                              ),
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              flex: 2,
                              child: GhostButton(label: 'MAIN MENU', icon: Icons.home_rounded, height: 50, onTap: () => Navigator.of(context).popUntil((r) => r.isFirst)),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          Positioned.fill(
            child: ConfettiBurst(trigger: _confetti, colors: [champ.primary, champ.glow, champ.secondary, AppColors.gold, Colors.white], count: 220, origin: const Alignment(0, -0.5)),
          ),
        ],
      ),
    );
  }

  List<Widget> _awards(Season s) {
    final db = PlayerDb.instance;
    final c = Career.instance;
    TeamInfo? owner(int id) {
      for (final f in c.franchises.values) {
        if (f.contains(id)) return f.info;
      }
      return null;
    }

    Widget award(String title, Color color, IconData icon, MapEntry<int, PlayerSeasonStats>? e, String Function(PlayerSeasonStats) value) {
      if (e == null) return const SizedBox.shrink();
      final p = db.byId(e.key);
      final t = owner(p.id);
      return GlassCard(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.fromLTRB(10, 8, 12, 8),
        borderColor: color.o(0.55),
        gradient: LinearGradient(colors: [color.o(0.18), const Color(0xD9070A1E)]),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: 8),
            PlayerAvatar(p, size: 40, team: t),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: T.label(10, color: color)),
                  Text(p.name, style: T.ui(15, weight: FontWeight.w700)),
                  Text(t?.name ?? '', style: T.ui(11, color: AppColors.textDim)),
                ],
              ),
            ),
            Text(value(e.value), style: T.display(22, weight: FontWeight.w700, color: color)),
          ],
        ),
      );
    }

    MapEntry<int, PlayerSeasonStats>? top(double Function(PlayerSeasonStats) k) {
      final l = s.leaders(k, n: 1);
      return l.isEmpty ? null : l.first;
    }

    return [
      award('ORANGE CAP', const Color(0xFFFF9F1C), Icons.sports_cricket_rounded, top((x) => x.runs.toDouble()), (v) => '${v.runs} runs'),
      award('PURPLE CAP', const Color(0xFFB57BFF), Icons.sports_baseball_rounded, top((x) => x.wickets * 1000.0 - x.economy), (v) => '${v.wickets} wkts'),
      award('MOST VALUABLE PLAYER', AppColors.cyan, Icons.star_rounded, top((x) => x.mvp), (v) => '${v.mvp.toStringAsFixed(0)} pts'),
      award('MOST SIXES', AppColors.gold, Icons.rocket_launch_rounded, top((x) => x.sixes.toDouble()), (v) => '${v.sixes}'),
      award('BEST ECONOMY (min 20 ov)', AppColors.green, Icons.speed_rounded, top((x) => x.ballsBowled >= 120 ? 20 - x.economy : 0), (v) => v.economy.toStringAsFixed(2)),
    ];
  }
}

class _RaysPainter extends CustomPainter {
  _RaysPainter(this.anim, this.color) : super(repaint: anim);
  final Animation<double> anim;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    canvas.drawRect(rect, Paint()..shader = const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFF1A1240), Color(0xFF07081C), Color(0xFF020309)]).createShader(rect));
    final c = Offset(size.width * 0.28, size.height * 0.45);
    final r = size.height;
    final rot = anim.value * math.pi * 2;
    for (var i = 0; i < 16; i++) {
      final a = rot + i * math.pi / 8;
      final path = Path()
        ..moveTo(c.dx, c.dy)
        ..lineTo(c.dx + math.cos(a - 0.08) * r, c.dy + math.sin(a - 0.08) * r)
        ..lineTo(c.dx + math.cos(a + 0.08) * r, c.dy + math.sin(a + 0.08) * r)
        ..close();
      canvas.drawPath(path, Paint()..shader = RadialGradient(center: const Alignment(-0.44, -0.1), radius: 0.9, colors: [AppColors.gold.o(0.22), AppColors.gold.o(0)]).createShader(rect));
    }
    canvas.drawCircle(c, size.height * 0.8, Paint()..shader = RadialGradient(colors: [color.o(0.35), color.o(0)]).createShader(Rect.fromCircle(center: c, radius: size.height * 0.8)));
  }

  @override
  bool shouldRepaint(covariant _RaysPainter old) => false;
}

/// Original golden cup design.
class TrophyPainter extends CustomPainter {
  const TrophyPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final cx = w / 2;
    Shader gold(Rect r) => const LinearGradient(
          colors: [Color(0xFF8A5A00), Color(0xFFFFE9A3), Color(0xFFFFC84A), Color(0xFFB8860B), Color(0xFFFFF1B8), Color(0xFF7A4E00)],
          stops: [0, 0.22, 0.4, 0.6, 0.78, 1],
        ).createShader(r);

    canvas.drawCircle(Offset(cx, h * 0.34), w * 0.55, Paint()..shader = RadialGradient(colors: [AppColors.gold.o(0.45), AppColors.gold.o(0)]).createShader(Rect.fromCircle(center: Offset(cx, h * 0.34), radius: w * 0.55)));

    // Handles.
    final handle = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = w * 0.06
      ..shader = gold(Rect.fromLTWH(0, 0, w, h * 0.5));
    canvas.drawArc(Rect.fromCenter(center: Offset(cx - w * 0.33, h * 0.24), width: w * 0.3, height: h * 0.22), math.pi * 0.5, math.pi, false, handle);
    canvas.drawArc(Rect.fromCenter(center: Offset(cx + w * 0.33, h * 0.24), width: w * 0.3, height: h * 0.22), -math.pi * 0.5, math.pi, false, handle);

    // Bowl.
    final bowl = Path()
      ..moveTo(cx - w * 0.34, h * 0.08)
      ..lineTo(cx + w * 0.34, h * 0.08)
      ..cubicTo(cx + w * 0.34, h * 0.32, cx + w * 0.2, h * 0.46, cx + w * 0.07, h * 0.5)
      ..lineTo(cx - w * 0.07, h * 0.5)
      ..cubicTo(cx - w * 0.2, h * 0.46, cx - w * 0.34, h * 0.32, cx - w * 0.34, h * 0.08)
      ..close();
    canvas.drawPath(bowl, Paint()..shader = gold(Rect.fromLTWH(cx - w * 0.34, 0, w * 0.68, h * 0.5)));
    canvas.drawOval(Rect.fromCenter(center: Offset(cx, h * 0.08), width: w * 0.68, height: h * 0.05), Paint()..color = const Color(0xFF6B4300));
    canvas.drawOval(Rect.fromCenter(center: Offset(cx, h * 0.075), width: w * 0.6, height: h * 0.032), Paint()..color = const Color(0xFFFFF1B8).o(0.6));
    // Engraved star.
    canvas.drawPath(starPath(Offset(cx, h * 0.24), w * 0.09), Paint()..color = const Color(0xFF7A4E00).o(0.8));
    canvas.drawPath(starPath(Offset(cx, h * 0.235), w * 0.08), Paint()..color = const Color(0xFFFFF4C4));
    // Stem.
    final stem = Rect.fromLTWH(cx - w * 0.05, h * 0.5, w * 0.1, h * 0.17);
    canvas.drawRect(stem, Paint()..shader = gold(stem));
    canvas.drawOval(Rect.fromCenter(center: Offset(cx, h * 0.585), width: w * 0.2, height: h * 0.05), Paint()..shader = gold(Rect.fromLTWH(cx - w * 0.1, h * 0.56, w * 0.2, h * 0.05)));
    // Base.
    final base1 = Rect.fromLTWH(cx - w * 0.24, h * 0.66, w * 0.48, h * 0.06);
    canvas.drawRRect(RRect.fromRectAndRadius(base1, const Radius.circular(4)), Paint()..shader = gold(base1));
    final plinth = Rect.fromLTWH(cx - w * 0.32, h * 0.72, w * 0.64, h * 0.14);
    canvas.drawRRect(RRect.fromRectAndRadius(plinth, const Radius.circular(6)), Paint()..color = const Color(0xFF15121E));
    canvas.drawRRect(
      RRect.fromRectAndRadius(plinth, const Radius.circular(6)),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..shader = gold(plinth),
    );
    paintText(canvas, 'CHAMPIONS', Offset(cx, h * 0.79), T.display(w * 0.11, color: AppColors.goldLight, weight: FontWeight.w700, spacing: 2));
    // Shine.
    canvas.drawLine(Offset(cx - w * 0.2, h * 0.12), Offset(cx - w * 0.12, h * 0.4), Paint()
      ..color = Colors.white.o(0.55)
      ..strokeWidth = w * 0.025
      ..strokeCap = StrokeCap.round);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
