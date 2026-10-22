import 'package:flutter/material.dart';

import '../../core/theme.dart';
import '../../engine/career.dart';
import '../../engine/delivery.dart';
import '../widgets/common.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  Settings get s => Career.instance.settings;

  void _save() {
    Career.instance.saveSettings();
    setState(() {});
  }

  Widget _segment<V>(List<(V, String)> options, V value, ValueChanged<V> onChanged) {
    return Row(
      children: [
        for (final o in options)
          Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 3),
              child: PressScale(
                onTap: () => onChanged(o.$1),
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 180),
                  height: 42,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(12),
                    gradient: o.$1 == value ? AppGradients.goldFlat : null,
                    color: o.$1 == value ? null : Colors.white.o(0.06),
                    border: Border.all(color: o.$1 == value ? Colors.transparent : AppColors.stroke),
                  ),
                  child: Text(o.$2, style: T.display(19, color: o.$1 == value ? const Color(0xFF1A1205) : AppColors.text, weight: FontWeight.w700)),
                ),
              ),
            ),
          ),
      ],
    );
  }

  Widget _switch(String title, String sub, bool v, ValueChanged<bool> onChanged) {
    return GlassCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.fromLTRB(14, 8, 8, 8),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: T.ui(16, weight: FontWeight.w700)),
                Text(sub, style: T.ui(12.5, color: AppColors.textDim)),
              ],
            ),
          ),
          Switch(value: v, activeThumbColor: AppColors.gold, onChanged: onChanged),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ScreenFrame(
      child: Column(
        children: [
          TopBar(title: 'SETTINGS', subtitle: 'GAMEPLAY & CAREER', onBack: () => Navigator.pop(context)),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const SectionTitle('Difficulty'),
                        _segment<Difficulty>(
                          [for (final d in Difficulty.values) (d, d.label)],
                          s.difficulty,
                          (d) {
                            s.difficulty = d;
                            _save();
                          },
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Controls ball speed, the timing window for shots and how accurate AI bowlers are.',
                          style: T.ui(12.5, color: AppColors.textDim),
                        ),
                        const SectionTitle('Match time'),
                        _segment<MatchLight>(
                          [for (final l in MatchLight.values) (l, l.label)],
                          s.matchLight,
                          (v) {
                            s.matchLight = v;
                            _save();
                          },
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'AUTO follows the fixture: 3:30 PM games in sunshine, 7:30 PM games under the floodlights.',
                          style: T.ui(12.5, color: AppColors.textDim),
                        ),
                        const SectionTitle('Auction speed'),
                        _segment<int>(
                          const [(1, '1×'), (2, '2×'), (4, '4×')],
                          s.auctionSpeed,
                          (v) {
                            s.auctionSpeed = v;
                            _save();
                          },
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                const SectionTitle('Assists'),
                _switch('Timing ring & pitch marker', 'Shows where the ball pitches and when to play', s.timingHint, (v) {
                  s.timingHint = v;
                  _save();
                }),
                _switch('Haptic feedback', 'Vibrate on taps, boundaries and wickets', s.haptics, (v) {
                  s.haptics = v;
                  _save();
                }),
                const SectionTitle('Career', color: AppColors.red),
                GhostButton(
                  label: 'DELETE SAVED CAREER',
                  icon: Icons.delete_forever_rounded,
                  color: AppColors.red,
                  onTap: () async {
                    final ok = await showDialog<bool>(
                      context: context,
                      builder: (ctx) => AlertDialog(
                        backgroundColor: AppColors.bg1,
                        title: Text('Delete career?', style: T.display(26)),
                        content: Text('This cannot be undone.', style: T.ui(15, color: AppColors.textDim)),
                        actions: [
                          TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text('CANCEL', style: T.label(13))),
                          TextButton(onPressed: () => Navigator.pop(ctx, true), child: Text('DELETE', style: T.label(13, color: AppColors.red))),
                        ],
                      ),
                    );
                    if (ok == true) {
                      await Career.instance.deleteSave();
                      if (context.mounted) showToast(context, 'Career deleted');
                    }
                  },
                ),
                const SectionTitle('About'),
                GlassCard(
                  child: Text(
                    'Squads: IPL 2026 retention lists and 2026 auction buys with real salaries, plus the confirmed IPL 2027 trades '
                    '(Pant ⇄ Kuldeep, Natarajan ⇄ Zeeshan Ansari). Player ratings are game ratings. Team crests are original '
                    'designs - this is an unofficial fan game, not affiliated with the BCCI or any franchise.',
                    style: T.ui(13, color: AppColors.textDim, height: 1.3),
                  ),
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
    );
  }
}
