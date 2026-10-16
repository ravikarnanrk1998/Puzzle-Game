import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../game/bike_game.dart';
import '../shop/catalog.dart';
import '../shop/player_progress.dart';
import '../versus/match_backend.dart';
import '../versus/versus_match.dart';
import 'avatar.dart';
import 'track_picker.dart';

const _gold = Color(0xFFFFC107);
const _diamond = Color(0xFF40C4FF);
const _panelColor = Color(0xE6141824);

const _labelStyle = TextStyle(
  color: Colors.white60,
  fontSize: 11,
  fontWeight: FontWeight.w800,
  letterSpacing: 1.5,
);
const _valueStyle = TextStyle(
  color: Colors.white,
  fontSize: 22,
  fontWeight: FontWeight.w900,
  fontStyle: FontStyle.italic,
  height: 1.1,
);
const _smallStyle = TextStyle(
  color: Colors.white,
  fontSize: 13,
  fontWeight: FontWeight.w800,
);

// ------------------------------------------------------------------ HUD

class Hud extends StatelessWidget {
  const Hud({
    super.key,
    required this.game,
    required this.onPause,
    required this.onBoost,
    this.versus,
  });

  final BikeGame game;
  final VoidCallback onPause;
  final VoidCallback onBoost;

  /// The 1 vs 1 match this run is part of, if any.
  final VersusMatch? versus;

  @override
  Widget build(BuildContext context) {
    final g = game;
    final rival = versus?.opponent;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(10),
        child: Stack(
          children: [
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  IgnorePointer(
                    child: _Chip(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('SCORE', style: _labelStyle),
                          Text('${g.scoreValue}', style: _valueStyle),
                        ],
                      ),
                    ),
                  ),
                  const Spacer(),
                  IgnorePointer(
                    child: _Chip(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'LEVEL ${g.level}',
                            style: _labelStyle.copyWith(
                                color: const Color(0xFFFFEB3B)),
                          ),
                          Text('${g.distance.floor()} m', style: _valueStyle),
                        ],
                      ),
                    ),
                  ),
                  const Spacer(),
                  IgnorePointer(
                    child: _Chip(
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.monetization_on,
                              color: _gold, size: 22),
                          const SizedBox(width: 6),
                          Text('${g.coins}', style: _valueStyle),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  _RoundButton(icon: Icons.pause_rounded, onTap: onPause),
                ],
              ),
            ),
            Positioned(
              top: 66,
              left: 0,
              child: IgnorePointer(
                child: Row(
                  children: [
                    for (var i = 0; i < max(g.lives, BikeGame.startLives); i++)
                      Padding(
                        padding: const EdgeInsets.only(right: 4),
                        child: Icon(
                          i < g.lives ? Icons.favorite : Icons.favorite_border,
                          color: const Color(0xFFFF4081),
                          size: 26,
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Positioned(
              top: 66,
              right: 0,
              child: IgnorePointer(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    if (g.shieldTimer > 0)
                      _Chip(
                        color: const Color(0xCC00897B),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.shield,
                                color: Colors.white, size: 18),
                            const SizedBox(width: 4),
                            Text('${g.shieldTimer.ceil()}s',
                                style: _smallStyle),
                          ],
                        ),
                      ),
                    if (g.combo > 1)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: _Chip(
                          color: const Color(0xCC00838F),
                          child:
                              Text('COMBO x${g.combo}', style: _smallStyle),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            if (rival != null)
              Positioned(
                top: 104,
                left: 0,
                right: 0,
                child: IgnorePointer(
                  child: Center(child: _RivalChip(rival: rival)),
                ),
              ),
            if (versus?.weakConnection ?? false)
              const Positioned(
                top: 146,
                left: 0,
                right: 0,
                child: IgnorePointer(child: Center(child: _WeakNetworkChip())),
              ),
            Positioned(
              left: 0,
              bottom: 0,
              child: IgnorePointer(
                child: _Chip(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('${g.speedKmh}',
                          style: _valueStyle.copyWith(fontSize: 26)),
                      const Text('KM/H', style: _labelStyle),
                    ],
                  ),
                ),
              ),
            ),
            Positioned(
              right: 0,
              bottom: 0,
              child: _BoostButton(game: g, onTap: onBoost),
            ),
            if (g.isRunning && g.playTime < 4.5)
              Positioned.fill(
                child: IgnorePointer(
                  child: Opacity(
                    opacity: (4.5 - g.playTime).clamp(0.0, 1.0),
                    child: const Align(
                      alignment: Alignment(0, 0.25),
                      child: _ControlsHint(),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.child,
    this.color = const Color(0xB3101521),
    this.padding = const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
  });

  final Widget child;
  final Color color;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: Colors.white12),
      ),
      child: child,
    );
  }
}

class _RoundButton extends StatelessWidget {
  const _RoundButton({
    required this.icon,
    required this.onTap,
    this.iconSize = 26,
    this.padding = 10,
    this.tooltip,
  });

  final IconData icon;
  final VoidCallback onTap;
  final double iconSize;
  final double padding;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final button = Material(
      color: const Color(0xB3101521),
      shape: const CircleBorder(side: BorderSide(color: Colors.white24)),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: Padding(
          padding: EdgeInsets.all(padding),
          child: Icon(icon, color: Colors.white, size: iconSize),
        ),
      ),
    );
    final tooltip = this.tooltip;
    return tooltip == null ? button : Tooltip(message: tooltip, child: button);
  }
}

class _ControlsHint extends StatelessWidget {
  const _ControlsHint();

  @override
  Widget build(BuildContext context) {
    const style = TextStyle(
      color: Colors.white,
      fontSize: 18,
      fontWeight: FontWeight.w900,
      letterSpacing: 2,
    );
    return const Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            _Chip(
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.chevron_left_rounded,
                      color: Colors.white, size: 30),
                  Text('TAP', style: style),
                ],
              ),
            ),
            _Chip(
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('TAP', style: style),
                  Icon(Icons.chevron_right_rounded,
                      color: Colors.white, size: 30),
                ],
              ),
            ),
          ],
        ),
        SizedBox(height: 12),
        _Chip(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.swipe_rounded, color: Colors.white, size: 24),
              SizedBox(width: 8),
              Text('OR SWIPE', style: style),
            ],
          ),
        ),
      ],
    );
  }
}

class _BoostButton extends StatelessWidget {
  const _BoostButton({required this.game, required this.onTap});

  final BikeGame game;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = game.boosting;
    final ready = game.canBoost;
    final color = active
        ? const Color(0xFFFF6D00)
        : ready
            ? const Color(0xFF00E5FF)
            : Colors.white38;
    final glow = active ? 1.0 : (ready ? 0.5 + 0.5 * sin(game.time * 8) : 0.0);
    return GestureDetector(
      onTapDown: (_) => onTap(),
      child: Container(
        width: 86,
        height: 86,
        decoration: const BoxDecoration(
          shape: BoxShape.circle,
          color: Color(0xCC101521),
        ),
        child: CustomPaint(
          painter: _RingPainter(game.nitro, color, glow),
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.bolt, color: color, size: 34),
                Text(
                  active ? 'BOOST!' : 'NITRO',
                  style: TextStyle(
                    color: color,
                    fontSize: 11,
                    fontWeight: FontWeight.w900,
                    letterSpacing: 1,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter(this.value, this.color, this.glow);

  final double value;
  final Color color;

  /// 0 to 1: how strongly the button glows.
  final double glow;

  @override
  void paint(Canvas canvas, Size size) {
    if (glow > 0) {
      // A gradient halo rather than a blurred shadow: it changes every
      // frame, and re-blurring it every frame is costly on phones.
      // Only outside the button's edge, like the shadow it replaces.
      final center = size.center(Offset.zero);
      final radius = size.width / 2 + 16;
      final edge = (size.width / 2 - 2) / radius;
      final clear = color.withValues(alpha: 0);
      canvas.drawCircle(
        center,
        radius,
        Paint()
          ..shader = RadialGradient(
            colors: [clear, clear, color.withValues(alpha: 0.6 * glow), clear],
            stops: [0, edge, edge, 1],
          ).createShader(Rect.fromCircle(center: center, radius: radius)),
      );
    }
    final rect = (Offset.zero & size).deflate(6);
    final stroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 7;
    canvas.drawArc(rect, 0, pi * 2, false, stroke..color = Colors.white12);
    if (value > 0) {
      canvas.drawArc(
        rect,
        -pi / 2,
        pi * 2 * value,
        false,
        stroke
          ..color = color
          ..strokeCap = StrokeCap.round,
      );
    }
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.value != value || old.color != color || old.glow != glow;
}

// ---------------------------------------------------------------- menus

class BigButton extends StatelessWidget {
  const BigButton({
    super.key,
    required this.label,
    required this.icon,
    required this.onTap,
    this.colors = const [Color(0xFFFF9800), Color(0xFFFF3D00)],
    this.width = 240,
    this.height = 60,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;
  final List<Color> colors;

  /// The size it asks for. A parent that sets its width, such as an
  /// [Expanded], wins.
  final double width;
  final double height;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(20);
    return Material(
      color: Colors.transparent,
      child: Ink(
        decoration: BoxDecoration(
          gradient: LinearGradient(colors: colors),
          borderRadius: radius,
          boxShadow: [
            BoxShadow(
              color: colors.last.withValues(alpha: 0.5),
              blurRadius: 18,
              offset: const Offset(0, 6),
            ),
          ],
        ),
        child: InkWell(
          borderRadius: radius,
          onTap: onTap,
          child: SizedBox(
            width: width,
            height: height,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(icon, color: Colors.white, size: 32),
                  const SizedBox(width: 8),
                  // Long labels shrink to fit rather than spill out.
                  Flexible(
                    child: FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Text(
                        label,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 24,
                          fontWeight: FontWeight.w900,
                          fontStyle: FontStyle.italic,
                          letterSpacing: 1.5,
                        ),
                      ),
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

class _Panel extends StatelessWidget {
  const _Panel({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(maxWidth: 380),
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: _panelColor,
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: Colors.white12),
        boxShadow: const [BoxShadow(color: Colors.black54, blurRadius: 30)],
      ),
      child: child,
    );
  }
}

/// Fades its child in once, when it first appears.
class _FadeIn extends StatelessWidget {
  const _FadeIn({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: const Duration(milliseconds: 350),
      curve: Curves.easeOutBack,
      builder: (context, t, child) => Opacity(
        opacity: t.clamp(0.0, 1.0),
        child: Transform.scale(scale: 0.85 + 0.15 * t, child: child),
      ),
      child: child,
    );
  }
}

/// Coin balance chip shown on the menu, game over and shop screens.
class WalletChip extends StatelessWidget {
  const WalletChip({super.key, required this.coins, this.doubled = false});

  final int coins;
  final bool doubled;

  @override
  Widget build(BuildContext context) {
    return _Chip(
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.monetization_on, color: _gold, size: 22),
          const SizedBox(width: 6),
          Text(formatNumber(coins),
              style: _valueStyle.copyWith(fontSize: 18)),
          if (doubled) ...[
            const SizedBox(width: 6),
            const Text('x2',
                style: TextStyle(
                    color: Color(0xFF69F0AE),
                    fontWeight: FontWeight.w900,
                    fontSize: 13)),
          ],
        ],
      ),
    );
  }
}

/// Diamond balance chip shown on the menu, in the shop and on the 1 vs 1
/// screen.
class DiamondChip extends StatelessWidget {
  const DiamondChip({super.key, required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    return _Chip(
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.diamond, color: _diamond, size: 20),
          const SizedBox(width: 6),
          Text(formatNumber(count),
              style: _valueStyle.copyWith(fontSize: 18)),
        ],
      ),
    );
  }
}

/// Hands out the free daily diamond.
class _DailyClaimButton extends StatelessWidget {
  const _DailyClaimButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(14);
    return Material(
      color: const Color(0xFF00C853),
      borderRadius: radius,
      child: InkWell(
        borderRadius: radius,
        onTap: onTap,
        child: const Padding(
          padding: EdgeInsets.symmetric(horizontal: 12, vertical: 9),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.card_giftcard_rounded, color: Colors.white, size: 18),
              SizedBox(width: 6),
              Text('CLAIM DAILY +1', style: _smallStyle),
              SizedBox(width: 4),
              Icon(Icons.diamond, color: Colors.white, size: 16),
            ],
          ),
        ),
      ),
    );
  }
}

class MenuOverlay extends StatelessWidget {
  const MenuOverlay({
    super.key,
    required this.game,
    required this.progress,
    required this.onPlay,
    required this.onShop,
    required this.onTrack,
    this.accountName,
    this.onAccount,
    this.onVersus,
    this.onClaimDaily,
  });

  final BikeGame game;
  final PlayerProgress progress;
  final VoidCallback onPlay;
  final VoidCallback onShop;

  /// A stage was tapped: ride it next, or see how to unlock it.
  final ValueChanged<Track> onTrack;

  /// Opens a 1 vs 1 match. The button is hidden when this is null, as
  /// matches need a signed-in player. So are the diamonds that pay for
  /// matches.
  final VoidCallback? onVersus;

  /// Claims the free daily diamond. Null once today's has been claimed.
  final VoidCallback? onClaimDaily;

  /// The signed-in player. The account button is hidden when [onAccount]
  /// is null (the game is running without accounts).
  final String? accountName;
  final VoidCallback? onAccount;

  /// How wide the buttons are.
  static const double _buttonsWidth = 300;

  @override
  Widget build(BuildContext context) {
    final pulse = 1 + sin(game.time * 4) * 0.04;
    final bike = progress.selectedBike;
    // A very dark bike would vanish on the dark pill: its accent shows.
    final bikeColor =
        bike.color.computeLuminance() < 0.05 ? bike.accent : bike.color;
    final open = Catalog.tracks.where(progress.hasTrack).length;
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xCC0D1020), Color(0x660D1020), Color(0xDD0D1020)],
        ),
      ),
      child: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(10, 10, 10, 0),
              child: _topBar(context),
            ),
            // The rest fits on one screen: on a short one it is scaled
            // down to fit, never scrolled.
            Expanded(
              child: LayoutBuilder(
                builder: (context, constraints) => FittedBox(
                  fit: BoxFit.scaleDown,
                  child: SizedBox(
                    width: constraints.maxWidth,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const GameTitle(),
                          const SizedBox(height: 6),
                          const Text(
                            'Dodge traffic • Grab coins • Fire the NITRO',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: Colors.white70,
                              fontSize: 15,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          const SizedBox(height: 20),
                          Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 18),
                            child: Row(
                              children: [
                                const Text('CHOOSE A STAGE',
                                    style: _labelStyle),
                                const Spacer(),
                                Text(
                                  '$open / ${Catalog.tracks.length} OPEN',
                                  style: _labelStyle.copyWith(color: _gold),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 8),
                          TrackPicker(progress: progress, onTap: onTrack),
                          const SizedBox(height: 18),
                          Wrap(
                            alignment: WrapAlignment.center,
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              _StatPill(
                                icon: Icons.two_wheeler,
                                color: bikeColor,
                                text: bike.name.toUpperCase(),
                              ),
                              if (progress.best > 0)
                                _StatPill(
                                  icon: Icons.emoji_events,
                                  color: _gold,
                                  text: 'BEST  ${formatNumber(progress.best)}',
                                ),
                            ],
                          ),
                          const SizedBox(height: 18),
                          Transform.scale(
                            scale: pulse,
                            // Drawn once and then only scaled, instead of
                            // redrawing the button and its shadow every
                            // frame.
                            child: RepaintBoundary(
                              child: BigButton(
                                label: 'PLAY',
                                icon: Icons.play_arrow_rounded,
                                onTap: onPlay,
                                width: _buttonsWidth,
                                height: 66,
                              ),
                            ),
                          ),
                          const SizedBox(height: 14),
                          SizedBox(
                            width: _buttonsWidth,
                            child: Row(
                              children: [
                                if (onVersus != null) ...[
                                  Expanded(
                                    child: BigButton(
                                      label: '1 VS 1',
                                      icon: Icons.people_alt_rounded,
                                      onTap: onVersus!,
                                      height: 56,
                                      colors: const [
                                        Color(0xFFFF4081),
                                        Color(0xFFC51162),
                                      ],
                                    ),
                                  ),
                                  const SizedBox(width: 12),
                                ],
                                Expanded(
                                  child: BigButton(
                                    label: 'GARAGE',
                                    icon: Icons.storefront_rounded,
                                    onTap: onShop,
                                    height: 56,
                                    colors: const [
                                      Color(0xFF7C4DFF),
                                      Color(0xFF304FFE),
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
                ),
              ),
            ),
            const _DeveloperCredit(),
          ],
        ),
      ),
    );
  }

  /// The account on the left; diamonds, coins and the how to play button
  /// on the right, with the daily claim under them while it is waiting.
  Widget _topBar(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Gives way to the balances on a narrow screen.
        Expanded(
          child: Align(
            alignment: Alignment.topLeft,
            child: onAccount == null
                ? const SizedBox.shrink()
                : _AccountChip(
                    name: accountName,
                    avatar: Avatar.of(progress.avatar),
                    onTap: onAccount!,
                  ),
          ),
        ),
        const SizedBox(width: 8),
        Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (onVersus != null) ...[
                  DiamondChip(count: progress.diamonds),
                  const SizedBox(width: 8),
                ],
                WalletChip(
                  coins: progress.wallet,
                  doubled: progress.doubleCoins,
                ),
                const SizedBox(width: 8),
                _RoundButton(
                  icon: Icons.info_outline_rounded,
                  iconSize: 22,
                  padding: 8,
                  tooltip: 'How to play',
                  onTap: () =>
                      _showHowToPlay(context, diamonds: onVersus != null),
                ),
              ],
            ),
            if (onClaimDaily != null) ...[
              const SizedBox(height: 8),
              _DailyClaimButton(onTap: onClaimDaily!),
            ],
          ],
        ),
      ],
    );
  }
}

/// A small fact on the menu, such as the bike being ridden.
class _StatPill extends StatelessWidget {
  const _StatPill({
    required this.icon,
    required this.color,
    required this.text,
  });

  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) {
    return _Chip(
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, color: color, size: 18),
          const SizedBox(width: 6),
          Text(text, style: _smallStyle.copyWith(color: color, letterSpacing: 1)),
        ],
      ),
    );
  }
}

/// The how to play notes, opened from the info button on the menu.
void _showHowToPlay(BuildContext context, {required bool diamonds}) {
  showDialog<void>(
    context: context,
    builder: (context) => Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.all(20),
      child: _HowToPlay(diamonds: diamonds),
    ),
  );
}

/// "Developed by" and the developer's logo, along the bottom of the menu.
class _DeveloperCredit extends StatelessWidget {
  const _DeveloperCredit();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 14),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            'Developed by ',
            style: TextStyle(
              color: Colors.white.withValues(alpha: 0.6),
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
          Image.asset('assets/image/develop.png',
              height: 22, fit: BoxFit.contain),
        ],
      ),
    );
  }
}

class _AccountChip extends StatelessWidget {
  const _AccountChip({
    required this.name,
    required this.avatar,
    required this.onTap,
  });

  final String? name;
  final Avatar avatar;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: _Chip(
        padding: const EdgeInsets.fromLTRB(4, 4, 12, 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            AvatarImage(avatar: avatar, size: 28),
            const SizedBox(width: 8),
            Flexible(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 120),
                child: Text(
                  name ?? 'Account',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: _smallStyle,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The game's logo, shown on the menu and the login screen.
class GameTitle extends StatelessWidget {
  const GameTitle({super.key});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        const Icon(Icons.two_wheeler, size: 64, color: Colors.white),
        FittedBox(
          fit: BoxFit.scaleDown,
          child: ShaderMask(
            shaderCallback: (rect) => const LinearGradient(
              colors: [Color(0xFFFFEB3B), Color(0xFFFF9800), Color(0xFFFF3D00)],
            ).createShader(rect),
            child: const Text(
              'BIKE RUSH',
              style: TextStyle(
                color: Colors.white,
                fontSize: 58,
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
                letterSpacing: -1,
                height: 1,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// The how to play notes: the controls and tips, then each stage and how
/// to unlock it. Shown in a dialog, and scrolls if the screen is short.
class _HowToPlay extends StatelessWidget {
  const _HowToPlay({required this.diamonds});

  /// Whether diamonds are part of the game here (they need an account).
  final bool diamonds;

  static const _textStyle = TextStyle(color: Colors.white70, fontSize: 14);

  @override
  Widget build(BuildContext context) {
    final hasKeyboard = kIsWeb ||
        defaultTargetPlatform == TargetPlatform.windows ||
        defaultTargetPlatform == TargetPlatform.macOS ||
        defaultTargetPlatform == TargetPlatform.linux;
    final tips = [
      (Icons.touch_app, 'Tap or swipe left / right to switch lanes'),
      if (hasKeyboard)
        (Icons.keyboard, 'Keyboard: ← → or A / D, Space = boost'),
      (Icons.monetization_on, 'Coins and close calls charge your NITRO'),
      (Icons.bolt, 'Boost smashes through traffic for points'),
      (Icons.storefront_rounded, 'Spend coins on faster-starting bikes'),
      if (diamonds)
        (
          Icons.diamond,
          'Every ${BikeGame.levelsPerDiamond} levels you complete in a run '
              'gives a free diamond (not in 1 vs 1)',
        ),
    ];
    void close() => Navigator.of(context).pop();
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              const Icon(Icons.info_outline_rounded, color: _gold, size: 22),
              const SizedBox(width: 8),
              const Expanded(
                child: Text(
                  'HOW TO PLAY',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.w900,
                    fontStyle: FontStyle.italic,
                    letterSpacing: 1,
                  ),
                ),
              ),
              IconButton(
                onPressed: close,
                tooltip: 'Close',
                visualDensity: VisualDensity.compact,
                icon: const Icon(Icons.close_rounded, color: Colors.white70),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Flexible(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (final (icon, text) in tips)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 5),
                      child: Row(
                        children: [
                          Icon(icon, color: _gold, size: 20),
                          const SizedBox(width: 10),
                          Expanded(child: Text(text, style: _textStyle)),
                        ],
                      ),
                    ),
                  const SizedBox(height: 14),
                  const Text('STAGES', style: _labelStyle),
                  const SizedBox(height: 6),
                  for (final track in Catalog.tracks)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 5),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(Icons.flag_rounded,
                              color: _gold, size: 20),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text.rich(
                              TextSpan(
                                style: _textStyle,
                                children: [
                                  TextSpan(
                                    text: 'Stage ${track.stage}: '
                                        '${track.name}\n',
                                    style: const TextStyle(
                                      color: Colors.white,
                                      fontWeight: FontWeight.w800,
                                    ),
                                  ),
                                  TextSpan(text: track.description),
                                  if (!track.isFree)
                                    TextSpan(
                                      text: '\nUnlock: complete level '
                                          '${track.unlockLevel} on Stage '
                                          '${track.stage - 1}',
                                      style: const TextStyle(
                                          color: Color(0xFFFFE082)),
                                    ),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 18),
          Center(
            child: BigButton(
              label: 'GOT IT',
              icon: Icons.check_rounded,
              onTap: close,
              width: 200,
              height: 52,
              colors: const [Color(0xFF00C853), Color(0xFF00897B)],
            ),
          ),
        ],
      ),
    );
  }
}

class PauseOverlay extends StatelessWidget {
  const PauseOverlay({
    super.key,
    required this.onResume,
    required this.onRestart,
    required this.onMenu,
  });

  final VoidCallback onResume;

  /// Null hides the button: a 1 vs 1 run can't be started over.
  final VoidCallback? onRestart;
  final VoidCallback onMenu;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: const Color(0x99000000),
      child: SafeArea(
        child: Center(
          child: _FadeIn(
            child: _Panel(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'PAUSED',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 38,
                      fontWeight: FontWeight.w900,
                      fontStyle: FontStyle.italic,
                    ),
                  ),
                  const SizedBox(height: 22),
                  BigButton(
                    label: 'RESUME',
                    icon: Icons.play_arrow_rounded,
                    onTap: onResume,
                  ),
                  if (onRestart != null) ...[
                    const SizedBox(height: 14),
                    BigButton(
                      label: 'RESTART',
                      icon: Icons.replay_rounded,
                      onTap: onRestart!,
                      colors: const [Color(0xFF42A5F5), Color(0xFF1565C0)],
                    ),
                  ],
                  const SizedBox(height: 8),
                  TextButton.icon(
                    onPressed: onMenu,
                    icon: const Icon(Icons.home_rounded),
                    label: const Text('MENU'),
                    style: TextButton.styleFrom(
                        foregroundColor: Colors.white70),
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

class GameOverOverlay extends StatelessWidget {
  const GameOverOverlay({
    super.key,
    required this.game,
    required this.progress,
    required this.onRestart,
    required this.onRevive,
    required this.onMenu,
    required this.onShop,
  });

  final BikeGame game;
  final PlayerProgress progress;
  final VoidCallback onRestart;
  final VoidCallback onRevive;
  final VoidCallback onMenu;
  final VoidCallback onShop;

  @override
  Widget build(BuildContext context) {
    final run = progress.lastRun;
    final canPayRevive = progress.wallet >= BikeGame.reviveCost;
    return ColoredBox(
      color: const Color(0xAA000000),
      child: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: _FadeIn(
              child: _Panel(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.car_crash,
                        color: Color(0xFFFF5252), size: 48),
                    const Text(
                      'CRASHED!',
                      style: TextStyle(
                        color: Color(0xFFFF5252),
                        fontSize: 36,
                        fontWeight: FontWeight.w900,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                    if (run?.newBest ?? false)
                      Container(
                        margin: const EdgeInsets.only(top: 8),
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 4),
                        decoration: BoxDecoration(
                          color: _gold,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: const Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.emoji_events,
                                color: Colors.black, size: 18),
                            SizedBox(width: 4),
                            Text(
                              'NEW BEST!',
                              style: TextStyle(
                                color: Colors.black,
                                fontWeight: FontWeight.w900,
                              ),
                            ),
                          ],
                        ),
                      ),
                    const SizedBox(height: 10),
                    const Text('SCORE', style: _labelStyle),
                    Text(
                      formatNumber(game.scoreValue),
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 52,
                        fontWeight: FontWeight.w900,
                        height: 1.1,
                      ),
                    ),
                    const SizedBox(height: 14),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                      children: [
                        _Stat(Icons.straighten, '${game.distance.floor()} m',
                            'DISTANCE'),
                        _Stat(Icons.monetization_on,
                            '+${run?.coinsEarned ?? 0}', 'COINS'),
                        _Stat(Icons.flag, '${game.level}', 'LEVEL'),
                      ],
                    ),
                    if (game.diamondsEarned > 0)
                      Padding(
                        padding: const EdgeInsets.only(top: 10),
                        child: _LevelDiamonds(count: game.diamondsEarned),
                      ),
                    const SizedBox(height: 12),
                    Wrap(
                      alignment: WrapAlignment.center,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      spacing: 10,
                      runSpacing: 8,
                      children: [
                        Text(
                          'BEST  ${formatNumber(progress.best)}',
                          style: const TextStyle(
                            color: _gold,
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        WalletChip(
                          coins: progress.wallet,
                          doubled: progress.doubleCoins,
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    if (game.canRevive) ...[
                      BigButton(
                        label: canPayRevive
                            ? 'CONTINUE ${BikeGame.reviveCost}'
                            : 'GET COINS',
                        icon: canPayRevive
                            ? Icons.favorite_rounded
                            : Icons.add_circle_rounded,
                        onTap: canPayRevive ? onRevive : onShop,
                        colors: const [Color(0xFF00E676), Color(0xFF00897B)],
                      ),
                      if (!canPayRevive)
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Text(
                            'Continue costs ${BikeGame.reviveCost} coins',
                            style: const TextStyle(
                                color: Colors.white54, fontSize: 12),
                          ),
                        ),
                      const SizedBox(height: 14),
                    ],
                    BigButton(
                      label: 'PLAY AGAIN',
                      icon: Icons.replay_rounded,
                      onTap: onRestart,
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        TextButton.icon(
                          onPressed: onMenu,
                          icon: const Icon(Icons.home_rounded),
                          label: const Text('MENU'),
                          style: TextButton.styleFrom(
                              foregroundColor: Colors.white70),
                        ),
                        const SizedBox(width: 8),
                        TextButton.icon(
                          onPressed: onShop,
                          icon: const Icon(Icons.storefront_rounded),
                          label: const Text('GARAGE'),
                          style: TextButton.styleFrom(
                              foregroundColor: Colors.white70),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// "+2 diamonds for completing levels", on the screens after a run.
class _LevelDiamonds extends StatelessWidget {
  const _LevelDiamonds({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.diamond, color: _diamond, size: 20),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            '+$count ${count == 1 ? 'diamond' : 'diamonds'} for completing '
            'levels',
            style: _smallStyle.copyWith(color: _diamond),
          ),
        ),
      ],
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat(this.icon, this.value, this.label);

  final IconData icon;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: Colors.white54, size: 22),
        const SizedBox(height: 4),
        Text(
          value,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 18,
            fontWeight: FontWeight.w900,
          ),
        ),
        Text(label, style: _labelStyle.copyWith(fontSize: 10)),
      ],
    );
  }
}

// --------------------------------------------------------------- 1 vs 1

/// The other player's score, shown while riding in a 1 vs 1 match.
class _RivalChip extends StatelessWidget {
  const _RivalChip({required this.rival});

  final MatchPlayer rival;

  @override
  Widget build(BuildContext context) {
    return _Chip(
      color: rival.done ? const Color(0xCC37474F) : const Color(0xCCC51162),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.people_alt_rounded, color: Colors.white, size: 18),
          const SizedBox(width: 6),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 110),
            child: Text(
              rival.name,
              overflow: TextOverflow.ellipsis,
              style: _smallStyle,
            ),
          ),
          const SizedBox(width: 8),
          Text(formatNumber(rival.score),
              style: _smallStyle.copyWith(color: _gold)),
          if (rival.done) ...[
            const SizedBox(width: 6),
            Text('OUT',
                style: _smallStyle.copyWith(color: const Color(0xFFFF8A80))),
          ],
        ],
      ),
    );
  }
}

/// Shown while riding in a 1 vs 1 match when this phone's connection is
/// not keeping up. If it doesn't recover, the match is lost.
class _WeakNetworkChip extends StatelessWidget {
  const _WeakNetworkChip();

  @override
  Widget build(BuildContext context) {
    return _Chip(
      color: const Color(0xE6D84315),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.wifi_off_rounded, color: Colors.white, size: 22),
          const SizedBox(width: 8),
          Flexible(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('WEAK NETWORK', style: _smallStyle),
                Text(
                  'Reconnect fast or you lose the match',
                  style: _smallStyle.copyWith(
                    color: Colors.white70,
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Counts down to the start of a 1 vs 1 run.
class VersusCountdown extends StatelessWidget {
  const VersusCountdown({
    super.key,
    required this.match,
    required this.seconds,
  });

  final VersusMatch match;
  final int seconds;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: const Color(0xAA000000),
      child: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('1 VS 1', style: _labelStyle),
                const SizedBox(height: 6),
                FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    'YOU  vs  ${match.opponent?.name ?? '…'}',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 24,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
                Text(
                  '$seconds',
                  style: const TextStyle(
                    color: _gold,
                    fontSize: 110,
                    fontWeight: FontWeight.w900,
                    fontStyle: FontStyle.italic,
                    height: 1.2,
                  ),
                ),
                const Text('GET READY', style: _labelStyle),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Takes the place of the game over screen in a 1 vs 1 match.
class VersusResultOverlay extends StatelessWidget {
  const VersusResultOverlay({
    super.key,
    required this.match,
    required this.coinsEarned,
    required this.onLeave,
  });

  final VersusMatch match;
  final int coinsEarned;
  final VoidCallback onLeave;

  @override
  Widget build(BuildContext context) {
    final rival = match.opponent;
    final rivalName = rival?.name ?? 'Opponent';
    final outcome = match.outcome;
    final lostMine = match.connectionLost && outcome != VersusOutcome.win;
    final (title, color, icon) = switch (outcome) {
      VersusOutcome.win => (
          'YOU WIN!',
          const Color(0xFF69F0AE),
          Icons.emoji_events,
        ),
      VersusOutcome.lose => (
          'YOU LOSE',
          const Color(0xFFFF5252),
          lostMine
              ? Icons.wifi_off_rounded
              : Icons.sentiment_dissatisfied_rounded,
        ),
      VersusOutcome.draw => ('DRAW', _gold, Icons.handshake_rounded),
      null => ('WAITING…', Colors.white, Icons.hourglass_top_rounded),
    };
    // A note about this phone's own connection is a warning.
    final (note, warning) = switch (outcome) {
      _ when lostMine => (
          'Your network is not strong enough. The connection was lost, '
              'so you lose this match.',
          true,
        ),
      VersusOutcome.win when match.opponentDropped => (
          '$rivalName lost their connection.',
          false,
        ),
      VersusOutcome.win when match.opponentLeft => (
          '$rivalName left the match.',
          false,
        ),
      null when match.weakConnection => (
          'Weak network. Reconnecting to get the result…',
          true,
        ),
      null when match.decided => ('Getting the result…', false),
      null => (
          '$rivalName is still riding. They win if they pass your score.',
          false,
        ),
      _ => (null, false),
    };
    return ColoredBox(
      color: const Color(0xAA000000),
      child: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: _FadeIn(
              child: _Panel(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(icon, color: color, size: 48),
                    Text(
                      title,
                      style: TextStyle(
                        color: color,
                        fontSize: 36,
                        fontWeight: FontWeight.w900,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                    if (note != null && warning)
                      Padding(
                        padding: const EdgeInsets.only(top: 10),
                        child: _NetworkWarning(note),
                      )
                    else if (note != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Text(
                          note,
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                              color: Colors.white70, fontSize: 14),
                        ),
                      ),
                    const SizedBox(height: 18),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: _VersusScore(
                            name: 'YOU',
                            score: match.myScore,
                            status: match.connectionLost
                                ? 'connection lost'
                                : 'finished',
                          ),
                        ),
                        Expanded(
                          child: _VersusScore(
                            name: rivalName,
                            score: rival?.score ?? 0,
                            status: match.opponentDropped
                                ? 'connection lost'
                                : match.opponentLeft
                                    ? 'left'
                                    : (rival?.done ?? false)
                                        ? 'finished'
                                        : 'riding…',
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.monetization_on,
                            color: _gold, size: 20),
                        const SizedBox(width: 6),
                        Text('+$coinsEarned coins', style: _smallStyle),
                      ],
                    ),
                    const SizedBox(height: 20),
                    BigButton(
                      label: 'MENU',
                      icon: Icons.home_rounded,
                      onTap: onLeave,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A note about this phone's own connection, on the 1 vs 1 result.
class _NetworkWarning extends StatelessWidget {
  const _NetworkWarning(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: const Color(0x33FF5252),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0x88FF5252)),
      ),
      child: Row(
        children: [
          const Icon(Icons.wifi_off_rounded,
              color: Color(0xFFFF8A80), size: 22),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 13,
                fontWeight: FontWeight.w600,
                height: 1.3,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _VersusScore extends StatelessWidget {
  const _VersusScore({
    required this.name,
    required this.score,
    required this.status,
  });

  final String name;
  final int score;
  final String status;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          name.toUpperCase(),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: _labelStyle,
        ),
        FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            formatNumber(score),
            style: const TextStyle(
              color: Colors.white,
              fontSize: 34,
              fontWeight: FontWeight.w900,
              height: 1.2,
            ),
          ),
        ),
        Text(status,
            style: const TextStyle(color: Colors.white54, fontSize: 12)),
      ],
    );
  }
}
