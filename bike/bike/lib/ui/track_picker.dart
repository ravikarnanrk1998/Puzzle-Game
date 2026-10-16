import 'package:flutter/material.dart';

import '../shop/catalog.dart';
import '../shop/player_progress.dart';
import 'track_preview.dart';

const _gold = Color(0xFFFFC107);
const _diamond = Color(0xFF40C4FF);

List<Color> _colors(TrackTheme theme) => switch (theme) {
      TrackTheme.highway => const [Color(0xFF43A047), Color(0xFF1B5E20)],
      TrackTheme.mountain => const [Color(0xFF8D6E63), Color(0xFF4E342E)],
    };

IconData _icon(Track track) => switch ((track.traffic, track.theme)) {
      (TrafficFlow.oncoming, _) => Icons.south_rounded,
      (TrafficFlow.twoWay, _) => Icons.swap_vert_rounded,
      (_, TrackTheme.highway) => Icons.add_road_rounded,
      (_, TrackTheme.mountain) => Icons.terrain_rounded,
    };

const _cardWidth = 150.0;
const _cardGap = 12.0;
const _pictureHeight = 112.0;

/// Space before the first card and after the last.
const _edge = 16.0;

/// The stages on the menu, in a row that scrolls sideways: tap one to ride
/// it next, or a locked one to see how to unlock it.
class TrackPicker extends StatefulWidget {
  const TrackPicker({super.key, required this.progress, required this.onTap});

  final PlayerProgress progress;
  final ValueChanged<Track> onTap;

  @override
  State<TrackPicker> createState() => _TrackPickerState();
}

class _TrackPickerState extends State<TrackPicker> {
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    // Opens with the picked stage in the middle.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scroll.hasClients) return;
      final position = _scroll.position;
      final i = Catalog.tracks.indexOf(widget.progress.selectedTrack);
      final centre = _edge + i * (_cardWidth + _cardGap) + _cardWidth / 2;
      _scroll.jumpTo((centre - position.viewportDimension / 2)
          .clamp(0.0, position.maxScrollExtent));
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final progress = widget.progress;
    return SingleChildScrollView(
      controller: _scroll,
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: _edge),
      child: Row(
        children: [
          for (final (i, track) in Catalog.tracks.indexed) ...[
            if (i > 0) const SizedBox(width: _cardGap),
            _TrackCard(
              track: track,
              selected: progress.trackId == track.id,
              locked: !progress.hasTrack(track),
              onTap: () => widget.onTap(track),
            ),
          ],
        ],
      ),
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
    return SizedBox(
      width: _cardWidth,
      child: Material(
        color: const Color(0xFF151A27),
        clipBehavior: Clip.antiAlias,
        elevation: selected ? 8 : 2,
        shadowColor: selected ? _gold : Colors.black,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: selected ? _gold : Colors.white24,
            width: selected ? 3 : 1,
          ),
        ),
        child: InkWell(
          onTap: onTap,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SizedBox(
                height: _pictureHeight,
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    TrackPreview(track: track),
                    if (locked) ...[
                      const ColoredBox(color: Color(0x99000000)),
                      const Center(
                        child: Icon(Icons.lock_rounded,
                            color: Colors.white, size: 34),
                      ),
                    ],
                    Positioned(
                      top: 6,
                      left: 6,
                      child: _Badge(
                        child: Text(
                          'STAGE ${track.stage}',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 10,
                            fontWeight: FontWeight.w900,
                            letterSpacing: 1.2,
                          ),
                        ),
                      ),
                    ),
                    Positioned(
                      top: 6,
                      right: 6,
                      child: _Badge(
                        child: Icon(
                          selected ? Icons.check_circle_rounded : _icon(track),
                          color: selected ? _gold : Colors.white,
                          size: 14,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: _colors(track.theme),
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(10, 6, 8, 7),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
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
                          color: locked
                              ? const Color(0xFFFFE082)
                              : selected
                                  ? _gold
                                  : Colors.white70,
                          fontSize: 10,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 1,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// A small dark label over a stage's picture.
class _Badge extends StatelessWidget {
  const _Badge({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xB3000000),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
        child: child,
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
              if (track.coinPrice > 0) ...[
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
                    style:
                        const TextStyle(color: Colors.white54, fontSize: 12),
                  ),
                ),
              ],
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
