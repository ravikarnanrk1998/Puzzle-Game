import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../game/bike_game.dart';
import '../shop/catalog.dart';
import '../shop/player_progress.dart';

const _gold = Color(0xFFFFC107);
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
  });

  final BikeGame game;
  final VoidCallback onPause;
  final VoidCallback onBoost;

  @override
  Widget build(BuildContext context) {
    final g = game;
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
                      child: _TapHint(),
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
  const _Chip({required this.child, this.color = const Color(0xB3101521)});

  final Widget child;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
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
  const _RoundButton({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: const Color(0xB3101521),
      shape: const CircleBorder(side: BorderSide(color: Colors.white24)),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Icon(icon, color: Colors.white, size: 26),
        ),
      ),
    );
  }
}

class _TapHint extends StatelessWidget {
  const _TapHint();

  @override
  Widget build(BuildContext context) {
    const style = TextStyle(
      color: Colors.white,
      fontSize: 18,
      fontWeight: FontWeight.w900,
      letterSpacing: 2,
    );
    return const Row(
      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
      children: [
        _Chip(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.chevron_left_rounded, color: Colors.white, size: 30),
              Text('TAP', style: style),
            ],
          ),
        ),
        _Chip(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('TAP', style: style),
              Icon(Icons.chevron_right_rounded, color: Colors.white, size: 30),
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
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: const Color(0xCC101521),
          boxShadow: [
            if (glow > 0)
              BoxShadow(
                color: color.withValues(alpha: 0.6 * glow),
                blurRadius: 22,
                spreadRadius: 2,
              ),
          ],
        ),
        child: CustomPaint(
          painter: _RingPainter(game.nitro, color),
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
  _RingPainter(this.value, this.color);

  final double value;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
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
      old.value != value || old.color != color;
}

// ---------------------------------------------------------------- menus

class BigButton extends StatelessWidget {
  const BigButton({
    super.key,
    required this.label,
    required this.icon,
    required this.onTap,
    this.colors = const [Color(0xFFFF9800), Color(0xFFFF3D00)],
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;
  final List<Color> colors;

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
            width: 240,
            height: 60,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(icon, color: Colors.white, size: 32),
                const SizedBox(width: 8),
                Text(
                  label,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 24,
                    fontWeight: FontWeight.w900,
                    fontStyle: FontStyle.italic,
                    letterSpacing: 1.5,
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

class MenuOverlay extends StatelessWidget {
  const MenuOverlay({
    super.key,
    required this.game,
    required this.progress,
    required this.onPlay,
    required this.onShop,
  });

  final BikeGame game;
  final PlayerProgress progress;
  final VoidCallback onPlay;
  final VoidCallback onShop;

  @override
  Widget build(BuildContext context) {
    final pulse = 1 + sin(game.time * 4) * 0.04;
    final bike = progress.selectedBike;
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xCC0D1020), Color(0x660D1020), Color(0xDD0D1020)],
        ),
      ),
      child: SafeArea(
        child: Stack(
          children: [
            Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(24, 56, 24, 24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const _Title(),
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
                    const SizedBox(height: 22),
                    Text('RIDING: ${bike.name.toUpperCase()}',
                        style: _labelStyle.copyWith(color: bike.color)),
                    const SizedBox(height: 16),
                    Transform.scale(
                      scale: pulse,
                      child: BigButton(
                        label: 'PLAY',
                        icon: Icons.play_arrow_rounded,
                        onTap: onPlay,
                      ),
                    ),
                    const SizedBox(height: 14),
                    BigButton(
                      label: 'GARAGE',
                      icon: Icons.storefront_rounded,
                      onTap: onShop,
                      colors: const [Color(0xFF7C4DFF), Color(0xFF304FFE)],
                    ),
                    if (progress.best > 0) ...[
                      const SizedBox(height: 18),
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.emoji_events, color: _gold),
                          const SizedBox(width: 6),
                          Text(
                            'BEST  ${formatNumber(progress.best)}',
                            style: const TextStyle(
                              color: _gold,
                              fontSize: 18,
                              fontWeight: FontWeight.w900,
                            ),
                          ),
                        ],
                      ),
                    ],
                    const SizedBox(height: 26),
                    const _HowToPlay(),
                  ],
                ),
              ),
            ),
            Positioned(
              top: 10,
              right: 10,
              child: WalletChip(
                  coins: progress.wallet, doubled: progress.doubleCoins),
            ),
          ],
        ),
      ),
    );
  }
}

class _Title extends StatelessWidget {
  const _Title();

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

class _HowToPlay extends StatelessWidget {
  const _HowToPlay();

  @override
  Widget build(BuildContext context) {
    final hasKeyboard = kIsWeb ||
        defaultTargetPlatform == TargetPlatform.windows ||
        defaultTargetPlatform == TargetPlatform.macOS ||
        defaultTargetPlatform == TargetPlatform.linux;
    final rows = [
      (Icons.touch_app, 'Tap left / right side to switch lanes'),
      if (hasKeyboard)
        (Icons.keyboard, 'Keyboard: ← → or A / D, Space = boost'),
      (Icons.monetization_on, 'Coins and close calls charge your NITRO'),
      (Icons.bolt, 'Boost smashes through traffic for points'),
      (Icons.storefront_rounded, 'Spend coins on faster-starting bikes'),
    ];
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('HOW TO PLAY', style: _labelStyle),
          const SizedBox(height: 10),
          for (final (icon, text) in rows)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Icon(icon, color: _gold, size: 20),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      text,
                      style: const TextStyle(
                          color: Colors.white70, fontSize: 14),
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

class PauseOverlay extends StatelessWidget {
  const PauseOverlay({
    super.key,
    required this.onResume,
    required this.onRestart,
    required this.onMenu,
  });

  final VoidCallback onResume;
  final VoidCallback onRestart;
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
                  const SizedBox(height: 14),
                  BigButton(
                    label: 'RESTART',
                    icon: Icons.replay_rounded,
                    onTap: onRestart,
                    colors: const [Color(0xFF42A5F5), Color(0xFF1565C0)],
                  ),
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
