import 'package:flutter/material.dart';

import '../shop/catalog.dart';
import '../shop/player_progress.dart';

const _gold = Color(0xFFFFC107);
const _diamond = Color(0xFF40C4FF);

List<Color> _colors(TrackTheme theme) => switch (theme) {
      TrackTheme.highway => const [Color(0xFF43A047), Color(0xFF1B5E20)],
      TrackTheme.mountain => const [Color(0xFF8D6E63), Color(0xFF4E342E)],
    };

IconData _icon(TrackTheme theme) => switch (theme) {
      TrackTheme.highway => Icons.add_road_rounded,
      TrackTheme.mountain => Icons.terrain_rounded,
    };

/// The stages on the menu: tap one to ride it next, or a locked one to see
/// how to unlock it.
class TrackPicker extends StatelessWidget {
  const TrackPicker({super.key, required this.progress, required this.onTap});

  final PlayerProgress progress;
  final ValueChanged<Track> onTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final (i, track) in Catalog.tracks.indexed) ...[
          if (i > 0) const SizedBox(width: 10),
          _TrackCard(
            track: track,
            selected: progress.trackId == track.id,
            locked: !progress.hasTrack(track),
            onTap: () => onTap(track),
          ),
        ],
      ],
    );
  }
}

class _TrackCard extends StatelessWidget {
  const _TrackCard({
    required this.track,
    required this.selected,
    required this.locked,
    required this.onTap,
  });

  final Track track;
  final bool selected;
  final bool locked;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(16);
    return Material(
      color: Colors.transparent,
      child: Ink(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: _colors(track.theme),
          ),
          borderRadius: radius,
          border: Border.all(
            color: selected ? _gold : Colors.white24,
            width: selected ? 3 : 1,
          ),
        ),
        child: InkWell(
          borderRadius: radius,
          onTap: onTap,
          child: SizedBox(
            width: 140,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(10, 6, 8, 7),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(
                        'STAGE ${track.stage}',
                        style: const TextStyle(
                          color: Colors.white70,
                          fontSize: 11,
                          fontWeight: FontWeight.w900,
                          letterSpacing: 1.2,
                        ),
                      ),
                      const Spacer(),
                      Icon(
                        locked
                            ? Icons.lock_rounded
                            : selected
                                ? Icons.check_circle_rounded
                                : _icon(track.theme),
                        color: selected ? _gold : Colors.white70,
                        size: 18,
                      ),
                    ],
                  ),
                  FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Text(
                      track.name,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 15,
                        fontWeight: FontWeight.w900,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                  ),
                  Text(
                    locked
                        ? 'TAP TO UNLOCK'
                        : selected
                            ? 'SELECTED'
                            : 'TAP TO RIDE',
                    style: TextStyle(
                      color: locked ? const Color(0xFFFFE082) : Colors.white60,
                      fontSize: 10,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 1,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// How to unlock [track]: complete its level on the stage before for free,
/// or pay for it now. Completes with true if it was bought.
Future<bool> showUnlockTrackDialog(
    BuildContext context, Track track, PlayerProgress progress) async {
  final bought = await showDialog<bool>(
    context: context,
    builder: (context) => ListenableBuilder(
      listenable: progress,
      builder: (context, _) {
        void buy({required bool withDiamonds}) {
          if (progress.buyTrack(track, withDiamonds: withDiamonds)) {
            Navigator.of(context).pop(true);
          }
        }

        final coins = progress.wallet >= track.coinPrice;
        final diamonds = progress.diamonds >= track.diamondPrice;
        return AlertDialog(
          scrollable: true,
          title: Text('Stage ${track.stage}: ${track.name}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(track.description),
              const SizedBox(height: 14),
              Row(
                children: [
                  const Icon(Icons.flag_rounded, color: _gold),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'FREE: complete level ${track.unlockLevel} in a run '
                      'on Stage ${track.stage - 1}.',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              const Text(
                'Or unlock it now:',
                style: TextStyle(color: Colors.white60, fontSize: 13),
              ),
              const SizedBox(height: 8),
              FilledButton.icon(
                onPressed: coins ? () => buy(withDiamonds: false) : null,
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFFFF9800),
                  foregroundColor: Colors.white,
                ),
                icon: const Icon(Icons.monetization_on, color: _gold),
                label: Text('${formatNumber(track.coinPrice)} COINS'),
              ),
              Padding(
                padding: const EdgeInsets.only(top: 4, bottom: 10),
                child: Text(
                  'You have ${formatNumber(progress.wallet)} coins',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white54, fontSize: 12),
                ),
              ),
              FilledButton.icon(
                onPressed: diamonds ? () => buy(withDiamonds: true) : null,
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF2979FF),
                  foregroundColor: Colors.white,
                ),
                icon: const Icon(Icons.diamond, color: _diamond),
                label: Text('${formatNumber(track.diamondPrice)} DIAMONDS'),
              ),
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'You have ${formatNumber(progress.diamonds)} diamonds',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white54, fontSize: 12),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('CLOSE'),
            ),
          ],
        );
      },
    ),
  );
  return bought ?? false;
}
