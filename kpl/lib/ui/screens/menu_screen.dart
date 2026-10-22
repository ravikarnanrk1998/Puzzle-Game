import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/players_db.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../game/stadium_view.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/ticker.dart';
import 'how_to_play_screen.dart';
import 'quick_match_screen.dart';
import 'router.dart';
import 'settings_screen.dart';
import 'team_select_screen.dart';

class MenuScreen extends StatefulWidget {
  const MenuScreen({super.key});

  @override
  State<MenuScreen> createState() => _MenuScreenState();
}

class _MenuScreenState extends State<MenuScreen> {
  bool _hasSave = false;
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _check();
  }

  Future<void> _check() async {
    final c = Career.instance;
    final has = c.active || await c.hasSave();
    if (mounted) setState(() => _hasSave = has);
  }

  Future<void> _continue() async {
    final c = Career.instance;
    setState(() => _loading = true);
    final ok = c.active || await c.load();
    if (!mounted) return;
    setState(() => _loading = false);
    if (!ok) {
      showToast(context, 'Could not load the saved career');
      return;
    }
    await Navigator.of(context).push(fadeRoute(screenForStage(c)));
    _check();
  }

  Future<void> _newCareer(CareerMode mode) async {
    final c = Career.instance;
    if (c.active || _hasSave) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          backgroundColor: AppColors.bg1,
          title: Text('Start a new career?', style: T.display(26)),
          content: Text('Your current saved career will be replaced.', style: T.ui(15, color: AppColors.textDim)),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text('CANCEL', style: T.label(13))),
            TextButton(onPressed: () => Navigator.pop(ctx, true), child: Text('START NEW', style: T.label(13, color: AppColors.gold))),
          ],
        ),
      );
      if (ok != true || !mounted) return;
    }
    await Navigator.of(context).push(fadeRoute(TeamSelectScreen(mode: mode)));
    _check();
  }

  @override
  Widget build(BuildContext context) {
    final c = Career.instance;
    final bgTeam = c.active ? c.userTeam : teamById('RCB');
    return Scaffold(
      body: Stack(
        fit: StackFit.expand,
        children: [
          StadiumPanorama(home: bgTeam, hype: 0.3, dim: 0.2, day: true),
          DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [AppColors.bg0.o(0.92), AppColors.bg0.o(0.62), AppColors.bg0.o(0.45)],
                stops: const [0, 0.36, 1],
              ),
            ),
          ),
          SafeArea(
            child: Column(
              children: [
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(14, 10, 14, 10),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        SizedBox(
                          width: 290,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              FittedBox(
                                fit: BoxFit.scaleDown,
                                alignment: Alignment.centerLeft,
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  crossAxisAlignment: CrossAxisAlignment.end,
                                  children: [
                                    GradientText(League.name, style: T.display(52, weight: FontWeight.w700, height: 0.85, spacing: 2)),
                                    const SizedBox(width: 8),
                                    GradientText('${League.startYear}', gradient: AppGradients.neon, style: T.display(34, weight: FontWeight.w700, height: 0.9, spacing: 4)),
                                  ],
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text('AUCTION  •  SEASON  •  GLORY', maxLines: 1, overflow: TextOverflow.fade, style: T.label(11.5, color: AppColors.goldLight)),
                              const Spacer(),
                              if (_hasSave) ...[
                                _ContinueCard(loading: _loading, onTap: _continue),
                                const SizedBox(height: 10),
                              ],
                              _TitleHistory(),
                            ],
                          ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            children: [
                              Expanded(
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.stretch,
                                  children: [
                                    Expanded(
                                      child: _ModeCard(
                                        title: 'MEGA AUCTION ${League.startYear}',
                                        tag: 'ALL PLAYERS',
                                        description:
                                            'Every star goes under the hammer. Retain up to 6, then build your squad from a ${PlayerDb.instance.all.length}-player pool with a ${money(League.purse)} purse and RTM cards.',
                                        colors: const [AppColors.gold, AppColors.goldDeep, AppColors.pink],
                                        icon: Icons.gavel_rounded,
                                        chips: const ['Marquee sets', 'RTM cards', 'Bidding wars'],
                                        onTap: () => _newCareer(CareerMode.mega),
                                      ),
                                    ),
                                    const SizedBox(width: 12),
                                    Expanded(
                                      child: _ModeCard(
                                        title: 'MINI AUCTION ${League.startYear}',
                                        tag: 'REAL SQUADS',
                                        description:
                                            'Start from the real IPL 2026 squads with the confirmed 2027 trades (Pant to DC, Kuldeep to LSG, Natarajan to SRH). Release players, then rebuild.',
                                        colors: const [AppColors.cyan, AppColors.violet, AppColors.pink],
                                        icon: Icons.swap_horiz_rounded,
                                        chips: const ['2026 squads', '2027 trades', 'Release & rebuild'],
                                        onTap: () => _newCareer(CareerMode.mini),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(height: 10),
                              SizedBox(
                                height: 64,
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.stretch,
                                  children: [
                                    Expanded(
                                      child: _Tile(
                                        icon: Icons.sports_cricket_rounded,
                                        title: 'QUICK MATCH',
                                        subtitle: 'Any two teams',
                                        color: AppColors.green,
                                        onTap: () => Navigator.of(context).push(fadeRoute(const QuickMatchScreen())),
                                      ),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: _Tile(
                                        icon: Icons.menu_book_rounded,
                                        title: 'HOW TO PLAY',
                                        subtitle: 'Batting, bowling, bids',
                                        color: AppColors.cyan,
                                        onTap: () => Navigator.of(context).push(fadeRoute(const HowToPlayScreen())),
                                      ),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: _Tile(
                                        icon: Icons.settings_rounded,
                                        title: 'SETTINGS',
                                        subtitle: 'Difficulty, match time',
                                        color: AppColors.gold,
                                        onTap: () => Navigator.of(context).push(fadeRoute(const SettingsScreen())).then((_) => setState(() {})),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const NewsTicker(items: kHeadlines, label: 'IPL NEWS'),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ContinueCard extends StatelessWidget {
  const _ContinueCard({required this.onTap, required this.loading});
  final VoidCallback onTap;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final c = Career.instance;
    final team = c.active ? c.userTeam : null;
    final stage = c.active
        ? switch (c.stage) {
            Stage.retention => 'Retention phase',
            Stage.auction => 'Auction in progress',
            Stage.season => 'Season ${c.year} in progress',
            Stage.finished => 'Season ${c.year} complete',
          }
        : 'Saved career';
    return GlassCard(
      onTap: loading ? null : onTap,
      blur: true,
      glow: team?.glow ?? AppColors.green,
      borderColor: (team?.glow ?? AppColors.green).o(0.7),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          if (team != null) TeamCrest(team, size: 46, glow: true) else const Icon(Icons.save_rounded, color: AppColors.green, size: 40),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text('CONTINUE CAREER', maxLines: 1, style: T.display(24, weight: FontWeight.w700, spacing: 1.2)),
                ),
                Text(team != null ? '${team.name} • $stage' : stage, style: T.ui(13, color: AppColors.textDim)),
              ],
            ),
          ),
          loading
              ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.play_circle_fill_rounded, color: AppColors.green, size: 36),
        ],
      ),
    );
  }
}

class _ModeCard extends StatefulWidget {
  const _ModeCard({
    required this.title,
    required this.tag,
    required this.description,
    required this.colors,
    required this.icon,
    required this.chips,
    required this.onTap,
  });

  final String title;
  final String tag;
  final String description;
  final List<Color> colors;
  final IconData icon;
  final List<String> chips;
  final VoidCallback onTap;

  @override
  State<_ModeCard> createState() => _ModeCardState();
}

class _ModeCardState extends State<_ModeCard> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(seconds: 5))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PressScale(
      onTap: widget.onTap,
      child: AnimatedBuilder(
        animation: _c,
        builder: (context, child) => Container(
          padding: const EdgeInsets.all(2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(22),
            gradient: SweepGradient(
              colors: [...widget.colors, widget.colors.first],
              transform: GradientRotation(_c.value * math.pi * 2),
            ),
            boxShadow: [BoxShadow(color: widget.colors.first.o(0.35), blurRadius: 30, spreadRadius: -6)],
          ),
          child: child,
        ),
        child: Container(
          padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(20),
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [const Color(0xF2141A48), const Color(0xF2080B22)],
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      gradient: LinearGradient(colors: [widget.colors[0], widget.colors[1]]),
                    ),
                    child: Icon(widget.icon, color: widget.colors[0].onColor, size: 26),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Pill(widget.tag, color: widget.colors[0], fontSize: 10.5),
                        const SizedBox(height: 3),
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          alignment: Alignment.centerLeft,
                          child: GradientText(
                            widget.title,
                            gradient: LinearGradient(colors: [Colors.white, widget.colors[0].lighten(0.3)]),
                            style: T.display(30, weight: FontWeight.w700, height: 1, spacing: 1),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Icon(Icons.arrow_forward_ios_rounded, color: widget.colors[0], size: 20),
                ],
              ),
              const SizedBox(height: 10),
              Expanded(
                child: Text(widget.description, overflow: TextOverflow.fade, style: T.ui(14, color: AppColors.textDim, height: 1.25)),
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [for (final c in widget.chips) Pill(c, color: widget.colors[1].lighten(0.2), fontSize: 11)],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.icon, required this.title, required this.subtitle, required this.color, required this.onTap});
  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GlassCard(
      onTap: onTap,
      color: const Color(0xCC0B1030),
      borderColor: color.o(0.45),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(borderRadius: BorderRadius.circular(12), color: color.o(0.16), border: Border.all(color: color.o(0.5))),
            child: Icon(icon, color: color, size: 24),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text(title, maxLines: 1, style: T.display(21, weight: FontWeight.w700, spacing: 1, height: 1)),
                ),
                Text(subtitle, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(12, color: AppColors.textDim)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _TitleHistory extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final recent = <(int, String)>[(2026, 'RCB'), (2025, 'RCB'), (2024, 'KKR'), (2023, 'CSK'), (2022, 'GT')];
    return GlassCard(
      color: const Color(0xB30B1030),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('RECENT CHAMPIONS', style: T.label(11.5, color: AppColors.gold)),
          const SizedBox(height: 10),
          Row(
            children: [
              for (final r in recent)
                Expanded(
                  child: Column(
                    children: [
                      TeamCrest(teamById(r.$2), size: 40),
                      const SizedBox(height: 4),
                      FittedBox(fit: BoxFit.scaleDown, child: Text('${r.$1}', style: T.display(17, color: AppColors.textDim))),
                    ],
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}
