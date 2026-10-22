import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/players_db.dart';
import '../../data/teams.dart';
import '../../engine/career.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import 'auction_screen.dart';

/// "Retention Day" - every franchise's retained core and purse.
class RetentionBoardScreen extends StatelessWidget {
  const RetentionBoardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final c = Career.instance;
    final mega = c.mode == CareerMode.mega;
    final list = c.franchises.values.toList()..sort((a, b) => b.purse.compareTo(a.purse));
    final poolSize = c.auction?.lots.length ?? 0;
    return ScreenFrame(
      child: Column(
        children: [
          TopBar(
            title: 'RETENTION DAY',
            subtitle: '${c.year} • ${mega ? 'MEGA' : 'MINI'} AUCTION • $poolSize PLAYERS IN THE POOL',
            onBack: null,
            actions: [
              SizedBox(
                width: 290,
                child: NeonButton(
                  label: 'ENTER THE AUCTION HALL',
                  icon: Icons.gavel_rounded,
                  height: 44,
                  fontSize: 20,
                  onTap: () => Navigator.of(context).pushReplacement(fadeRoute(const AuctionScreen())),
                ),
              ),
            ],
          ),
          Expanded(
            child: GridView.builder(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisExtent: 96, mainAxisSpacing: 8, crossAxisSpacing: 8),
              itemCount: list.length,
              itemBuilder: (context, i) {
                final f = list[i];
                final t = f.info;
                final isUser = f.id == c.userTeamId;
                final names = mega
                    ? (f.players..sort((a, b) => b.overall.compareTo(a.overall))).map((p) => p.name).toList()
                    : (c.released[f.id] ?? const <int>[]).map((id) => PlayerDb.instance.byId(id).name).toList();
                return GlassCard(
                  padding: const EdgeInsets.all(10),
                  gradient: LinearGradient(colors: [t.primary.o(isUser ? 0.5 : 0.25), const Color(0xE6070A1E)]),
                  borderColor: isUser ? AppColors.gold : AppColors.strokeSoft,
                  glow: isUser ? t.glow : null,
                  child: Row(
                    children: [
                      TeamCrest(t, size: 46),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Text(t.id, style: T.display(22, weight: FontWeight.w700, height: 1)),
                                if (isUser) ...[
                                  const SizedBox(width: 6),
                                  Pill('YOU', color: AppColors.gold, fontSize: 9.5, filled: true),
                                ],
                              ],
                            ),
                            const SizedBox(height: 2),
                            Text(
                              mega
                                  ? (names.isEmpty ? 'No retentions - full rebuild' : 'Retained: ${names.join(', ')}')
                                  : (names.isEmpty ? 'No releases' : 'Released: ${names.take(6).join(', ')}${names.length > 6 ? ' +${names.length - 6}' : ''}'),
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: T.ui(12, color: AppColors.textDim, height: 1.2),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          GradientText(money(f.purse), style: T.display(24, weight: FontWeight.w700, height: 1)),
                          Text(mega ? '${f.size} kept • ${f.rtmCards} RTM' : '${f.size} players', style: T.ui(11, color: AppColors.textDim)),
                          Text('${League.maxSquad - f.size} slots • ${f.overseasLeft} OS', style: T.ui(11, color: AppColors.textDim)),
                        ],
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

String teamName(String id) => teamById(id).name;
