import 'package:flutter/material.dart';

import '../../core/league.dart';
import '../../core/theme.dart';
import '../../engine/career.dart';
import '../../models/franchise.dart';
import '../../models/player.dart';
import '../widgets/common.dart';
import '../widgets/crest.dart';
import 'retention_board_screen.dart';

class RetentionScreen extends StatefulWidget {
  const RetentionScreen({super.key});

  @override
  State<RetentionScreen> createState() => _RetentionScreenState();
}

class _RetentionScreenState extends State<RetentionScreen> {
  final Career c = Career.instance;
  late final List<Contract> _squad = c.preAuctionContracts(c.userTeamId)
    ..sort((a, b) => b.player.overall.compareTo(a.player.overall));
  final List<int> _retained = [];
  final Set<int> _released = {};

  bool get mega => c.mode == CareerMode.mega;

  void _toggle(Player p) {
    setState(() {
      if (mega) {
        if (_retained.contains(p.id)) {
          _retained.remove(p.id);
        } else {
          final trial = [..._retained.map((id) => _squad.firstWhere((x) => x.player.id == id).player), p];
          final problem = Career.retentionProblem(trial);
          if (problem != null) {
            showToast(context, problem, color: AppColors.red.darken(0.3));
            return;
          }
          _retained.add(p.id);
        }
      } else {
        if (!_released.remove(p.id)) _released.add(p.id);
      }
    });
    haptic();
  }

  void _autoSuggest() {
    setState(() {
      _released
        ..clear()
        ..addAll(_squad.where((x) => marketValue(x.player) - x.price < -400 || x.player.overall < 64).map((x) => x.player.id));
    });
  }

  void _confirm() {
    if (mega) {
      c.confirmMegaRetention(List.of(_retained));
    } else {
      c.confirmMiniReleases(Set.of(_released));
    }
    Navigator.of(context).pushReplacement(fadeRoute(const RetentionBoardScreen()));
  }

  @override
  Widget build(BuildContext context) {
    final team = c.userTeam;
    final retainedPlayers = _retained.map((id) => _squad.firstWhere((x) => x.player.id == id).player).toList();
    final costs = Career.retentionCosts(retainedPlayers);
    final cost = costs.fold(0, (s, v) => s + v);
    final kept = _squad.where((x) => !_released.contains(x.player.id)).toList();
    final keptSalary = kept.fold(0, (s, x) => s + x.price);
    final purseAfter = mega ? League.purse - cost : League.purse - keptSalary;
    final capped = retainedPlayers.where((p) => p.capped).length;
    return ScreenFrame(
      colors: [team.glow, team.primary, AppColors.violet, team.secondary],
      child: Column(
        children: [
          TopBar(
            title: mega ? 'RETENTIONS' : 'RELEASE PLAYERS',
            subtitle: mega ? 'MEGA AUCTION ${c.year} • ${team.name.toUpperCase()}' : 'MINI AUCTION ${c.year} • ${team.name.toUpperCase()}',
            onBack: () => Navigator.pop(context),
            actions: [TeamCrest(team, size: 36)],
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 2, 12, 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    width: 250,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        GlassCard(
                          blur: true,
                          borderColor: team.glow.o(0.5),
                          padding: const EdgeInsets.all(12),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  _stat(mega ? 'RETAINED' : 'SQUAD', mega ? '${_retained.length}/${League.maxRetain}' : '${kept.length}/${League.maxSquad}'),
                                  _stat(mega ? 'CAPPED' : 'OVERSEAS', mega ? '$capped/${League.maxCappedRetain}' : '${kept.where((x) => x.player.overseas).length}/${League.maxOverseas}'),
                                  _stat(mega ? 'RTM' : 'RELEASED', mega ? '${League.maxRetain - _retained.length}' : '${_released.length}'),
                                ],
                              ),
                              const Divider(color: AppColors.strokeSoft, height: 16),
                              Text('AUCTION PURSE', style: T.label(10)),
                              CountUp(
                                value: purseAfter.toDouble(),
                                builder: (_, v) => GradientText(money(v.round()), style: T.display(36, weight: FontWeight.w700, height: 1)),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          mega
                              ? 'Tap players to retain.\nSlabs ₹18 / 14 / 11 / 18 / 14 Cr • uncapped ₹4 Cr. Unused retentions become RTM cards.'
                              : 'Tap players to release - they enter the auction pool.',
                          style: T.ui(12.5, color: AppColors.textDim, height: 1.25),
                        ),
                        if (!mega)
                          Align(
                            alignment: Alignment.centerLeft,
                            child: TextButton.icon(
                              onPressed: _autoSuggest,
                              icon: const Icon(Icons.auto_awesome_rounded, size: 16, color: AppColors.cyan),
                              label: Text('AUTO SUGGEST', style: T.label(12, color: AppColors.cyan)),
                            ),
                          ),
                        const Spacer(),
                        NeonButton(
                          label: mega ? 'CONFIRM RETENTIONS' : 'CONFIRM RELEASES',
                          subtitle: mega ? 'Spend ${money(cost)} • purse ${money(purseAfter)}' : 'Purse for auction ${money(purseAfter)}',
                          fontSize: 21,
                          colors: [team.glow, team.primary],
                          onTap: purseAfter < 0 ? null : _confirm,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: GridView.builder(
                      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: 2, mainAxisExtent: 62, mainAxisSpacing: 8, crossAxisSpacing: 8),
                      itemCount: _squad.length,
                      itemBuilder: (context, i) {
                        final x = _squad[i];
                        final p = x.player;
                        final ri = _retained.indexOf(p.id);
                        final on = mega ? ri >= 0 : !_released.contains(p.id);
                        final accent = mega ? (on ? AppColors.green : AppColors.strokeSoft) : (on ? AppColors.strokeSoft : AppColors.red);
                        return GlassCard(
                          padding: const EdgeInsets.fromLTRB(8, 6, 10, 6),
                          onTap: () => _toggle(p),
                          color: mega && on
                              ? AppColors.green.o(0.12)
                              : (!mega && !on ? AppColors.red.o(0.12) : const Color(0xB30B1030)),
                          borderColor: accent,
                          child: Row(
                            children: [
                              PlayerAvatar(p, size: 44, team: team),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: T.ui(14.5, weight: FontWeight.w700)),
                                    const SizedBox(height: 2),
                                    Row(
                                      children: [
                                        RoleBadge(p.role, size: 9.5),
                                        const SizedBox(width: 4),
                                        CountryChip(p.country, size: 9),
                                        const SizedBox(width: 6),
                                        Flexible(
                                          child: Text(
                                            '2026: ${money(x.price)}${p.capped ? '' : ' • UNCAPPED'}',
                                            maxLines: 1,
                                            overflow: TextOverflow.ellipsis,
                                            style: T.ui(11, color: AppColors.textDim),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                              if (mega && ri >= 0)
                                Padding(
                                  padding: const EdgeInsets.only(right: 6),
                                  child: Column(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: [
                                      Text('#${ri + 1}', style: T.label(9.5, color: AppColors.green)),
                                      Text(money(costs[ri]), style: T.display(17, color: AppColors.green, height: 1)),
                                    ],
                                  ),
                                ),
                              if (!mega && !on)
                                Padding(
                                  padding: const EdgeInsets.only(right: 6),
                                  child: Pill('OUT', color: AppColors.red, fontSize: 9.5),
                                ),
                              RatingBadge(p.overall, size: 34),
                            ],
                          ),
                        );
                      },
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

  Widget _stat(String label, String value) => Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: T.label(9.5)),
            Text(value, style: T.display(24, weight: FontWeight.w700, height: 1.05)),
          ],
        ),
      );
}
