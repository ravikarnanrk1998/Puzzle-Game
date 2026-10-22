import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../models/franchise.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import 'retention_screen.dart';

class TeamSelectScreen extends StatefulWidget {
  const TeamSelectScreen({super.key, required this.mode});

  final CareerMode mode;

  @override
  State<TeamSelectScreen> createState() => _TeamSelectScreenState();
}

class _TeamSelectScreenState extends State<TeamSelectScreen> {
  late final Map<String, Franchise> _pre = {
    for (final e in Career.preAuctionSquads().entries)
      e.key: (Franchise(teamById(e.key), squad: e.value)..purse = League.purse - e.value.fold(0, (s, c) => s + c.price)),
  };
  String _selected = 'CSK';

  void _confirm() {
    Career.instance.startNew(mode: widget.mode, teamId: _selected);
    Navigator.of(context).pushReplacement(fadeRoute(const RetentionScreen()));
  }

  @override
  Widget build(BuildContext context) {
    final team = teamById(_selected);
    final f = _pre[_selected]!;
    final stars = f.players..sort((a, b) => b.overall.compareTo(a.overall));
    return ScreenFrame(
      colors: [team.glow, team.primary, AppColors.violet, team.secondary],
      child: Column(
        children: [
          TopBar(
            title: 'CHOOSE YOUR FRANCHISE',
            subtitle: widget.mode == CareerMode.mega ? 'MEGA AUCTION ${League.startYear}' : 'MINI AUCTION ${League.startYear}',
            onBack: () => Navigator.pop(context),
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(
                    child: LayoutBuilder(builder: (context, box) {
                      const gap = 10.0;
                      final w = (box.maxWidth - gap * 4) / 5, h = (box.maxHeight - gap) / 2;
                      return GridView.builder(
                        physics: const NeverScrollableScrollPhysics(),
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 5,
                          mainAxisSpacing: gap,
                          crossAxisSpacing: gap,
                          childAspectRatio: w / h,
                        ),
                        itemCount: kTeams.length,
                        itemBuilder: (context, i) => _teamCard(kTeams[i]),
                      );
                    }),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 270,
                    child: GlassCard(
                      blur: true,
                      padding: const EdgeInsets.all(12),
                      borderColor: team.glow.o(0.6),
                      glow: team.glow,
                      gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [team.primary.o(0.5), const Color(0xF0070A1E)]),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              TeamCrest(team, size: 58, glow: true),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(team.name.toUpperCase(), maxLines: 2, style: T.display(21, weight: FontWeight.w700, height: 1)),
                                    Text(team.city, style: T.ui(12, color: AppColors.textDim)),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 8),
                          Row(
                            children: [
                              _fact('RATING', '${f.strength}'),
                              _fact('TITLES', '${team.titles}'),
                              _fact('2026', ordinal(team.finish2026)),
                            ],
                          ),
                          const SizedBox(height: 6),
                          Text(team.venue, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(12, color: AppColors.textDim)),
                          const SizedBox(height: 6),
                          Text('KEY PLAYERS', style: T.label(9.5, color: team.glow)),
                          const SizedBox(height: 4),
                          Expanded(
                            child: ListView(
                              padding: EdgeInsets.zero,
                              children: [
                                for (final p in stars.take(5))
                                  Padding(
                                    padding: const EdgeInsets.only(bottom: 4),
                                    child: Row(
                                      children: [
                                        PlayerAvatar(p, size: 26, team: team),
                                        const SizedBox(width: 8),
                                        Expanded(child: Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(13, weight: FontWeight.w700))),
                                        RatingBadge(p.overall, size: 24),
                                      ],
                                    ),
                                  ),
                              ],
                            ),
                          ),
                          Text(
                            widget.mode == CareerMode.mega
                                ? 'Retain up to 6 of your 25 • RTM cards for the rest'
                                : 'Salary ${money(f.spent)} • purse left ${money(f.purse)}',
                            style: T.ui(12, color: team.glow),
                          ),
                          const SizedBox(height: 8),
                          NeonButton(
                            label: 'LEAD ${team.id}',
                            icon: Icons.check_circle_rounded,
                            height: 46,
                            colors: [team.glow, team.primary],
                            onTap: _confirm,
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _fact(String label, String value) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: T.label(9)),
            Text(value, style: T.display(22, weight: FontWeight.w700, height: 1.05)),
          ],
        ),
      );

  Widget _teamCard(TeamInfo t) {
    final sel = t.id == _selected;
    return AnimatedScale(
      scale: sel ? 1.0 : 0.95,
      duration: const Duration(milliseconds: 200),
      child: GlassCard(
        onTap: () => setState(() => _selected = t.id),
        padding: const EdgeInsets.all(8),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [t.primary.o(sel ? 0.65 : 0.32), const Color(0xE6080B22)],
        ),
        borderColor: sel ? t.glow : AppColors.strokeSoft,
        glow: sel ? t.glow : null,
        child: Column(
          children: [
            Expanded(child: FittedBox(child: TeamCrest(t, size: 64, glow: sel))),
            const SizedBox(height: 4),
            Text(t.id, style: T.display(22, weight: FontWeight.w700, height: 1)),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text('${_pre[t.id]!.strength}', style: T.display(16, color: AppColors.gold, weight: FontWeight.w700)),
                const SizedBox(width: 6),
                Icon(Icons.emoji_events_rounded, size: 13, color: t.titles > 0 ? AppColors.gold : AppColors.textMute),
                Text('${t.titles}', style: T.ui(12, color: AppColors.textDim)),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
