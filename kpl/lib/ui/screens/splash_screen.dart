import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../game/stadium_view.dart';
import '../widgets/common.dart';
import '../widgets/effects.dart';
import 'menu_screen.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 2600))..forward();

  Animation<double> _iv(double a, double b, [Curve curve = Curves.easeOutCubic]) =>
      CurvedAnimation(parent: _c, curve: Interval(a, b, curve: curve));

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  void _go() {
    if (_c.value < 0.6) {
      _c.value = 1;
      return;
    }
    Navigator.of(context).pushReplacement(fadeRoute(const MenuScreen()));
  }

  @override
  Widget build(BuildContext context) {
    final lights = _iv(0.0, 0.35);
    return Scaffold(
      body: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: _go,
        child: Stack(
          fit: StackFit.expand,
          children: [
            AnimatedBuilder(
              animation: lights,
              builder: (context, child) => Opacity(opacity: 0.25 + 0.75 * lights.value, child: child),
              child: StadiumPanorama(home: teamById('RCB'), away: teamById('GT'), hype: 0.5, day: true),
            ),
            DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [Colors.black.o(0.05), Colors.black.o(0.25), AppColors.bg0.o(0.82), AppColors.bg0.o(0.95)],
                  stops: const [0, 0.3, 0.72, 1],
                ),
              ),
            ),
            SafeArea(
              child: LayoutBuilder(builder: (context, box) {
                final k = (box.maxHeight / 400).clamp(0.75, 1.6);
                return Column(
                  children: [
                    const Spacer(flex: 4),
                    FadeTransition(
                      opacity: _iv(0.25, 0.5),
                      child: Text('INDIAN PREMIER LEAGUE  •  20TH EDITION', style: T.label(12.5 * k, color: AppColors.goldLight)),
                    ),
                    SizedBox(height: 4 * k),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        ScaleTransition(
                          scale: Tween(begin: 1.6, end: 1.0).animate(_iv(0.2, 0.55, Curves.easeOutBack)),
                          child: FadeTransition(
                            opacity: _iv(0.2, 0.45),
                            child: Shine(
                              child: GradientText(
                                League.name,
                                style: T.display(118 * k, weight: FontWeight.w700, height: 0.82, spacing: 4, shadows: const [
                                  Shadow(color: Color(0x88FF8A00), blurRadius: 40),
                                ]),
                              ),
                            ),
                          ),
                        ),
                        SizedBox(width: 18 * k),
                        SlideTransition(
                          position: Tween(begin: const Offset(0.4, 0), end: Offset.zero).animate(_iv(0.4, 0.7)),
                          child: FadeTransition(
                            opacity: _iv(0.4, 0.7),
                            child: Padding(
                              padding: EdgeInsets.only(bottom: 8 * k),
                              child: GradientText(
                                '${League.startYear}',
                                gradient: AppGradients.neon,
                                style: T.display(76 * k, weight: FontWeight.w700, height: 0.9, spacing: 10),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                    SizedBox(height: 10 * k),
                    FadeTransition(
                      opacity: _iv(0.55, 0.8),
                      child: Container(
                        width: 260 * k,
                        height: 2,
                        decoration: const BoxDecoration(
                          gradient: LinearGradient(colors: [Colors.transparent, AppColors.gold, Colors.transparent]),
                        ),
                      ),
                    ),
                    SizedBox(height: 10 * k),
                    FadeTransition(
                      opacity: _iv(0.6, 0.85),
                      child: Text('MEGA AUCTION   •   FULL SEASON   •   GLORY', style: T.label(13 * k, color: AppColors.text)),
                    ),
                    const Spacer(flex: 3),
                    FadeTransition(
                      opacity: _iv(0.85, 1.0),
                      child: Pulse(
                        child: Container(
                          padding: EdgeInsets.symmetric(horizontal: 28 * k, vertical: 8 * k),
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(40),
                            border: Border.all(color: AppColors.gold.o(0.7)),
                            color: Colors.black.o(0.35),
                          ),
                          child: Text('TAP TO START', style: T.display(22 * k, color: AppColors.goldLight, spacing: 3)),
                        ),
                      ),
                    ),
                    SizedBox(height: 10 * k),
                    Text('Fan-made game • ratings are game ratings', style: T.ui(11, color: AppColors.textMute)),
                    const SizedBox(height: 6),
                  ],
                );
              }),
            ),
          ],
        ),
      ),
    );
  }
}
