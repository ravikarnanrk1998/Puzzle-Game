import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../data/teams.dart';
import '../../engine/auction.dart';
import '../../engine/career.dart';
import '../../models/franchise.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import '../widgets/effects.dart';
import 'season_hub_screen.dart';

class AuctionSummaryScreen extends StatefulWidget {
  const AuctionSummaryScreen({super.key});

  @override
  State<AuctionSummaryScreen> createState() => _AuctionSummaryScreenState();
}

class _AuctionSummaryScreenState extends State<AuctionSummaryScreen> {
  int _tab = 0;
  int _confetti = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => setState(() => _confetti = 1));
  }

  @override
  Widget build(BuildContext context) {
    final c = Career.instance;
    final me = c.user;
    final team = c.userTeam;
    final lots = c.auction?.lots ?? const <AuctionLot>[];
    final sold = lots.where((l) => l.status == LotStatus.sold).toList()..sort((a, b) => b.price.compareTo(a.price));
    final ranks = c.franchises.values.toList()..sort((a, b) => b.strength.compareTo(a.strength));
    final squad = List.of(me.squad)..sort((a, b) => b.player.overall.compareTo(a.player.overall));
    final myRank = ranks.indexWhere((f) => f.id == me.id) + 1;
    final spent = sold.where((l) => l.soldTo == me.id).fold(0, (s, l) => s + l.price);
    return Stack(
      children: [
        ScreenFrame(
          colors: [team.glow, team.primary, AppColors.gold, team.secondary],
          child: Column(
            children: [
              TopBar(
                title: 'AUCTION COMPLETE',
                subtitle: '${c.year} • ${sold.length} PLAYERS SOLD',
                onBack: null,
                actions: [
                  SizedBox(
                    width: 300,
                    child: NeonButton(
                      label: 'START ${League.name} ${c.year} SEASON',
                      icon: Icons.sports_cricket_rounded,
                      height: 44,
                      fontSize: 20,
                      onTap: () => Navigator.of(context).pushReplacement(fadeRoute(const SeasonHubScreen())),
                    ),
                  ),
                ],
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(12, 2, 12, 10),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SizedBox(width: 270, child: _teamCard(c, me, team, spent, myRank)),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          children: [
                            Row(
                              children: [
                                for (final (i, label) in const ['YOUR SQUAD', 'TOP BUYS', 'POWER RANKINGS'].indexed)
                                  Expanded(
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(horizontal: 3),
                                      child: PressScale(
                                        onTap: () => setState(() => _tab = i),
                                        child: AnimatedContainer(
                                          duration: const Duration(milliseconds: 180),
                                          height: 36,
                                          alignment: Alignment.center,
                                          decoration: BoxDecoration(
                                            borderRadius: BorderRadius.circular(12),
                                            gradient: _tab == i ? AppGradients.goldFlat : null,
                                            color: _tab == i ? null : Colors.white.o(0.06),
                                          ),
                                          child: Text(label, style: T.display(16.5, color: _tab == i ? const Color(0xFF1A1205) : AppColors.text, weight: FontWeight.w700)),
                                        ),
                                      ),
                                    ),
                                  ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Expanded(
                              child: switch (_tab) {
                                0 => _grid(squad.length, (i) => _squadTile(squad[i], team)),
                                1 => _grid(sold.length.clamp(0, 30), (i) => _buyTile(i, sold[i])),
                                _ => _grid(ranks.length, (i) => _rankTile(i, ranks[i], ranks[i].id == me.id)),
                              },
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
        Positioned.fill(child: ConfettiBurst(trigger: _confetti, colors: [team.primary, team.glow, AppColors.gold, Colors.white], origin: const Alignment(0, -0.6))),
      ],
    );
  }

  Widget _grid(int n, Widget Function(int) item) => GridView.builder(
        padding: EdgeInsets.zero,
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisExtent: 56, mainAxisSpacing: 7, crossAxisSpacing: 8),
        itemCount: n,
        itemBuilder: (context, i) => item(i),
      );

  Widget _teamCard(Career c, Franchise me, TeamInfo team, int spent, int myRank) => GlassCard(
        blur: true,
        glow: team.glow,
        borderColor: team.glow.o(0.6),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  TeamCrest(team, size: 56, glow: true),
                  const SizedBox(width: 10),
                  Expanded(child: Text(team.name.toUpperCase(), maxLines: 2, style: T.display(21, weight: FontWeight.w700, height: 1))),
                ],
              ),
              const SizedBox(height: 8),
              Text('${me.size} players • ${me.overseasCount} overseas\nspent ${money(spent)} at the auction', style: T.ui(12.5, color: AppColors.textDim)),
              const SizedBox(height: 8),
              StatBar(label: 'BAT', value: me.battingRating, color: AppColors.cyan),
              const SizedBox(height: 4),
              StatBar(label: 'BOWL', value: me.bowlingRating, color: AppColors.pink),
              const SizedBox(height: 10),
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('#$myRank', style: T.display(38, color: AppColors.gold, weight: FontWeight.w700, height: 1)),
                        Text('POWER RANK', style: T.label(9)),
                      ],
                    ),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(money(me.purse), style: T.display(24, weight: FontWeight.w700, height: 1)),
                      Text('PURSE LEFT', style: T.label(9)),
                    ],
                  ),
                ],
              ),
              if (c.auctionNotes.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text('Auto-completed squad: ${c.auctionNotes.join(' • ')}', style: T.ui(11.5, color: AppColors.goldLight)),
              ],
            ],
          ),
        ),
      );

  Widget _squadTile(Contract x, TeamInfo team) => GlassCard(
        padding: const EdgeInsets.fromLTRB(8, 6, 10, 6),
        color: const Color(0xB30B1030),
        child: Row(
          children: [
            PlayerAvatar(x.player, size: 38, team: team),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(x.player.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14, weight: FontWeight.w700)),
                  Row(children: [
                    RoleBadge(x.player.role, size: 9),
                    const SizedBox(width: 4),
                    CountryChip(x.player.country, size: 8.5),
                    const SizedBox(width: 6),
                    Flexible(child: Text(x.how.label, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(10.5, color: AppColors.textMute))),
                  ]),
                ],
              ),
            ),
            Text(money(x.price), style: T.display(18, color: AppColors.gold)),
            const SizedBox(width: 8),
            RatingBadge(x.player.overall, size: 30),
          ],
        ),
      );

  Widget _buyTile(int i, AuctionLot l) {
    final t = teamById(l.soldTo!);
    return GlassCard(
      padding: const EdgeInsets.fromLTRB(8, 6, 10, 6),
      gradient: LinearGradient(colors: [t.primary.o(0.35), const Color(0xD9070A1E)]),
      child: Row(
        children: [
          SizedBox(width: 24, child: Text('${i + 1}', style: T.display(20, color: AppColors.textDim))),
          TeamCrest(t, size: 28),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(l.player.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14, weight: FontWeight.w700)),
                Text('${l.player.role.label} • base ${money(l.base)}${l.viaRtm ? ' • RTM' : ''}', maxLines: 1, style: T.ui(11, color: AppColors.textDim)),
              ],
            ),
          ),
          GradientText(money(l.price), style: T.display(21, weight: FontWeight.w700)),
        ],
      ),
    );
  }

  Widget _rankTile(int i, Franchise f, bool isMe) => GlassCard(
        padding: const EdgeInsets.fromLTRB(8, 6, 10, 6),
        borderColor: isMe ? AppColors.gold : AppColors.strokeSoft,
        color: const Color(0xB30B1030),
        child: Row(
          children: [
            SizedBox(width: 24, child: Text('${i + 1}', style: T.display(20, color: isMe ? AppColors.gold : AppColors.textDim))),
            TeamCrest(f.info, size: 28),
            const SizedBox(width: 8),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  StatBar(label: 'BAT', value: f.battingRating, color: AppColors.cyan),
                  const SizedBox(height: 3),
                  StatBar(label: 'BOWL', value: f.bowlingRating, color: AppColors.pink),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Text('${f.strength}', style: T.display(28, weight: FontWeight.w700, color: isMe ? AppColors.gold : AppColors.text)),
          ],
        ),
      );
}
