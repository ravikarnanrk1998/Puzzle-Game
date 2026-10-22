import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../../engine/match_engine.dart';
import '../../models/franchise.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import 'match_screen.dart';

class QuickMatchScreen extends StatefulWidget {
  const QuickMatchScreen({super.key});

  @override
  State<QuickMatchScreen> createState() => _QuickMatchScreenState();
}

class _QuickMatchScreenState extends State<QuickMatchScreen> {
  String _mine = 'CSK';
  String _opp = 'MI';
  int _overs = 5;
  late bool _day = Career.instance.settings.matchLight != MatchLight.night;

  Franchise _team(String id) {
    final f = Franchise(teamById(id), squad: Career.preAuctionSquads()[id]);
    f.autoLineup();
    return f;
  }

  void _play() {
    final engine = MatchEngine(
      home: TeamSheet.fromFranchise(_team(_mine)),
      away: TeamSheet.fromFranchise(_team(_opp)),
      venue: teamById(_mine),
      overs: _overs,
      seed: DateTime.now().millisecondsSinceEpoch & 0x7fffffff,
      difficulty: Career.instance.settings.difficulty,
    );
    Navigator.of(context).push(fadeRoute(MatchScreen(engine: engine, userTeamId: _mine, title: 'QUICK MATCH', day: _day)));
  }

  Widget _picker(String label, String value, ValueChanged<String> onPick, {String? exclude}) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SectionTitle(label),
          Expanded(
            child: LayoutBuilder(builder: (context, box) {
              const gap = 6.0;
              final w = (box.maxWidth - gap * 4) / 5, h = (box.maxHeight - gap) / 2;
              return GridView.count(
                physics: const NeverScrollableScrollPhysics(),
                crossAxisCount: 5,
                mainAxisSpacing: gap,
                crossAxisSpacing: gap,
                childAspectRatio: w / h,
                children: [
                  for (final t in kTeams)
                    Opacity(
                      opacity: t.id == exclude ? 0.3 : 1,
                      child: GlassCard(
                        onTap: t.id == exclude ? null : () => onPick(t.id),
                        padding: const EdgeInsets.all(4),
                        borderColor: t.id == value ? t.glow : AppColors.strokeSoft,
                        glow: t.id == value ? t.glow : null,
                        color: t.id == value ? t.primary.o(0.4) : AppColors.glass,
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            TeamCrest(t, size: 30),
                            const SizedBox(width: 6),
                            Text(t.id, style: T.display(18, weight: FontWeight.w700)),
                          ],
                        ),
                      ),
                    ),
                ],
              );
            }),
          ),
        ],
      ),
    );
  }

  Widget _segment<V>(List<(V, String, IconData?)> options, V value, ValueChanged<V> onChanged) => Row(
        children: [
          for (final o in options)
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 3),
                child: PressScale(
                  onTap: () => onChanged(o.$1),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 160),
                    height: 40,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      gradient: o.$1 == value ? AppGradients.goldFlat : null,
                      color: o.$1 == value ? null : Colors.white.o(0.06),
                      border: Border.all(color: o.$1 == value ? Colors.transparent : AppColors.stroke),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        if (o.$3 != null) ...[
                          Icon(o.$3, size: 16, color: o.$1 == value ? const Color(0xFF1A1205) : AppColors.textDim),
                          const SizedBox(width: 4),
                        ],
                        Text(o.$2, style: T.display(21, color: o.$1 == value ? const Color(0xFF1A1205) : AppColors.text, weight: FontWeight.w700)),
                      ],
                    ),
                  ),
                ),
              ),
            ),
        ],
      );

  @override
  Widget build(BuildContext context) {
    final a = teamById(_mine), b = teamById(_opp);
    return ScreenFrame(
      colors: [a.glow, b.glow, AppColors.violet, a.primary],
      child: Column(
        children: [
          TopBar(title: 'QUICK MATCH', subtitle: 'CURRENT SQUADS • HOME VENUE', onBack: () => Navigator.pop(context)),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 0, 12, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    width: 300,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Expanded(
                          child: GlassCard(
                            blur: true,
                            padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 8),
                            gradient: LinearGradient(colors: [a.primary.o(0.45), const Color(0xE6070A1E), b.primary.o(0.45)]),
                            child: Row(
                              children: [
                                Expanded(
                                  child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                                    TeamCrest(a, size: 64, glow: true),
                                    const SizedBox(height: 4),
                                    Text(a.id, style: T.display(22, weight: FontWeight.w700, height: 1)),
                                    Text('YOU', style: T.label(10, color: AppColors.gold)),
                                  ]),
                                ),
                                GradientText('VS', gradient: AppGradients.neon, style: T.display(42, weight: FontWeight.w700)),
                                Expanded(
                                  child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                                    TeamCrest(b, size: 64, glow: true),
                                    const SizedBox(height: 4),
                                    Text(b.id, style: T.display(22, weight: FontWeight.w700, height: 1)),
                                    Text('CPU', style: T.label(10)),
                                  ]),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text('OVERS PER SIDE', style: T.label(10)),
                        const SizedBox(height: 4),
                        _segment<int>([for (final o in const [2, 5, 10, 20]) (o, '$o', null)], _overs, (o) => setState(() => _overs = o)),
                        const SizedBox(height: 6),
                        Text('MATCH TIME', style: T.label(10)),
                        const SizedBox(height: 4),
                        _segment<bool>(
                          const [(true, '3:30 PM', Icons.wb_sunny_rounded), (false, '7:30 PM', Icons.nightlight_round)],
                          _day,
                          (v) => setState(() => _day = v),
                        ),
                        const SizedBox(height: 6),
                        Text('${a.venue}, ${a.city}', maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(12, color: AppColors.textDim)),
                        const SizedBox(height: 6),
                        NeonButton(label: 'PLAY MATCH', icon: Icons.play_arrow_rounded, height: 48, onTap: _play),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      children: [
                        _picker('Your team', _mine, (id) => setState(() {
                              _mine = id;
                              if (_opp == id) _opp = kTeams.firstWhere((t) => t.id != id).id;
                            })),
                        _picker('Opponent', _opp, (id) => setState(() => _opp = id), exclude: _mine),
                      ],
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
}
