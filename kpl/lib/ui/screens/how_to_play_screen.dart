import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../widgets/common.dart';

class HowToPlayScreen extends StatelessWidget {
  const HowToPlayScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final sections = <(IconData, Color, String, List<String>)>[
      (Icons.sports_cricket_rounded, AppColors.cyan, 'BATTING', [
        'Watch the pitch marker - it shows where the ball will bounce.',
        'SWIPE in the direction you want to hit the ball the moment it reaches the bat. The shrinking ring shows the perfect moment. TAP to defend.',
        'Match the shot to the length: drive full balls, pull and cut short balls, sweep spinners, defend yorkers.',
        'A fast swipe or the LOFT button goes aerial: big sixes when timed well, but a catch if you mistime it.',
        'Early timing drags the ball to the leg side, late timing to the off side - just like real cricket.',
      ]),
      (Icons.sports_baseball_rounded, AppColors.pink, 'BOWLING', [
        'DRAG the target onto the pitch map: yorker, full, good length, short or bouncer - and the line.',
        'Pick a delivery from the DROPDOWN: stock, slower ball, in-swinger, out-swinger (or quicker / mystery for spinners).',
        'Press BOWL, then stop the accuracy meter in the green. Wild deliveries become full tosses, wides or no-balls.',
        'Variety beats batters: repeating the same ball makes it easier to hit. Yorkers rule the death overs.',
        'Pick your bowler every over - each can bowl a quarter of the overs (4 in a T20).',
      ]),
      (Icons.gavel_rounded, AppColors.gold, 'THE AUCTION', [
        'Mega Auction: retain up to 6 players (max 5 capped, 2 uncapped). Slabs: ₹18, 14, 11, 18, 14 Cr; uncapped ₹4 Cr. Unused retentions become RTM cards.',
        'Purse ${money(League.purse)}. Squad 18–25 players, max 8 overseas. You must keep ₹30L for every remaining minimum slot.',
        'Tap BID to raise by the official increment. When AI teams stop, the hammer falls: "Going once… twice… SOLD!"',
        'RTM: if a player from your old squad is sold elsewhere, you may match. The winning team gets one final raise first.',
        'PASS lets the AI finish a lot quickly. SIM SET resolves the rest of the current set (hold it to simulate the whole auction).',
      ]),
      (Icons.emoji_events_rounded, AppColors.green, 'THE SEASON', [
        '70 league matches in the IPL two-group format: 14 games per team, 7 at home.',
        'Top 4 make the playoffs: Qualifier 1, Eliminator, Qualifier 2 and the Final.',
        'Play any match ball-by-ball or simulate it. You can also sim an over or an innings mid-match from the pause menu.',
        'Win 2 points per game; net run rate breaks ties. Track the Orange and Purple Caps and the MVP race.',
      ]),
    ];
    Widget card((IconData, Color, String, List<String>) s) => GlassCard(
          margin: const EdgeInsets.only(bottom: 12),
          borderColor: s.$2.o(0.4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(s.$1, color: s.$2),
                  const SizedBox(width: 8),
                  Text(s.$3, style: T.display(24, weight: FontWeight.w700, spacing: 1.2)),
                ],
              ),
              for (final line in s.$4)
                Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Padding(
                        padding: const EdgeInsets.only(top: 6, right: 8),
                        child: Container(width: 6, height: 6, decoration: BoxDecoration(color: s.$2, shape: BoxShape.circle)),
                      ),
                      Expanded(child: Text(line, style: T.ui(13.5, color: AppColors.textDim, height: 1.3))),
                    ],
                  ),
                ),
            ],
          ),
        );
    return ScreenFrame(
      child: Column(
        children: [
          TopBar(title: 'HOW TO PLAY', subtitle: 'BECOME A CHAMPION', onBack: () => Navigator.pop(context)),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(14, 4, 14, 14),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: Column(children: [card(sections[0]), card(sections[2])])),
                  const SizedBox(width: 12),
                  Expanded(child: Column(children: [card(sections[1]), card(sections[3])])),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
