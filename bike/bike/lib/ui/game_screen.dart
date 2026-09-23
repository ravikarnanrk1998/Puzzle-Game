import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';

import '../game/bike_game.dart';
import '../game/game_painter.dart';
import '../shop/player_progress.dart';
import '../shop/store_service.dart';
import 'overlays.dart';
import 'shop_screen.dart';

class GameScreen extends StatefulWidget {
  const GameScreen({super.key, required this.progress, required this.store});

  final PlayerProgress progress;
  final StoreService store;

  @override
  State<GameScreen> createState() => _GameScreenState();
}

class _GameScreenState extends State<GameScreen>
    with SingleTickerProviderStateMixin, WidgetsBindingObserver {
  final BikeGame _game = BikeGame();
  final FocusNode _focus = FocusNode();
  late final Ticker _ticker;
  Duration _last = Duration.zero;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _game.bike = widget.progress.selectedBike;
    widget.progress.addListener(_syncBike);
    _game.onGameOver = _bankRun;
    _game.onImpact = (heavy) =>
        heavy ? HapticFeedback.heavyImpact() : HapticFeedback.mediumImpact();
    _ticker = createTicker(_onTick)..start();
  }

  @override
  void dispose() {
    widget.progress.removeListener(_syncBike);
    WidgetsBinding.instance.removeObserver(this);
    _ticker.dispose();
    _game.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed) _game.pause();
  }

  void _onTick(Duration elapsed) {
    var dt = (elapsed - _last).inMicroseconds / Duration.microsecondsPerSecond;
    _last = elapsed;
    // Clamp long frames (e.g. after a hitch) so nothing teleports.
    if (dt > 1 / 30) dt = 1 / 30;
    _game.update(dt);
  }

  void _start() {
    _game.startGame();
    _focus.requestFocus();
  }

  void _menu() {
    _game.goToMenu();
    _focus.requestFocus();
  }

  void _syncBike() => _game.bike = widget.progress.selectedBike;

  /// Moves the coins from this run into the wallet.
  void _bankRun() {
    widget.progress.recordRun(
      score: _game.scoreValue,
      coins: _game.coins - _game.coinsBanked,
    );
    _game.coinsBanked = _game.coins;
  }

  void _revive() {
    if (!_game.canRevive) return;
    if (widget.progress.spend(BikeGame.reviveCost)) {
      _game.revive();
      _focus.requestFocus();
    } else {
      _openShop(tab: 1);
    }
  }

  Future<void> _openShop({int tab = 0}) async {
    await Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => ShopScreen(
        progress: widget.progress,
        store: widget.store,
        initialTab: tab,
      ),
    ));
    _focus.requestFocus();
  }

  KeyEventResult _onKey(FocusNode node, KeyEvent event) {
    if (event is! KeyDownEvent) return KeyEventResult.ignored;
    final key = event.logicalKey;
    final phase = _game.phase;
    final canStart = phase == GamePhase.menu || phase == GamePhase.gameOver;
    if (key == LogicalKeyboardKey.arrowLeft || key == LogicalKeyboardKey.keyA) {
      _game.steer(-1);
    } else if (key == LogicalKeyboardKey.arrowRight ||
        key == LogicalKeyboardKey.keyD) {
      _game.steer(1);
    } else if (key == LogicalKeyboardKey.space ||
        key == LogicalKeyboardKey.arrowUp ||
        key == LogicalKeyboardKey.keyW) {
      canStart ? _start() : _game.activateBoost();
    } else if (key == LogicalKeyboardKey.escape ||
        key == LogicalKeyboardKey.keyP) {
      _game.togglePause();
    } else if (key == LogicalKeyboardKey.enter) {
      if (canStart) {
        _start();
      } else {
        _game.resume();
      }
    } else {
      return KeyEventResult.ignored;
    }
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF1B1B24),
      body: Focus(
        focusNode: _focus,
        autofocus: true,
        onKeyEvent: _onKey,
        child: LayoutBuilder(
          builder: (context, constraints) {
            _game.resize(constraints.biggest);
            return Stack(
              fit: StackFit.expand,
              children: [
                Listener(
                  behavior: HitTestBehavior.opaque,
                  onPointerDown: (e) {
                    if (!_game.isRunning) return;
                    _game.steer(
                        e.localPosition.dx < constraints.maxWidth / 2 ? -1 : 1);
                  },
                  child: RepaintBoundary(
                    child: CustomPaint(
                      painter: GamePainter(_game),
                      size: Size.infinite,
                    ),
                  ),
                ),
                ListenableBuilder(
                  listenable: Listenable.merge([_game, widget.progress]),
                  builder: (context, _) => _buildOverlay(),
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _buildOverlay() {
    final hud = Hud(
      game: _game,
      onPause: _game.pause,
      onBoost: _game.activateBoost,
    );
    switch (_game.phase) {
      case GamePhase.menu:
        return MenuOverlay(
          game: _game,
          progress: widget.progress,
          onPlay: _start,
          onShop: _openShop,
        );
      case GamePhase.playing:
      case GamePhase.crashing:
        return hud;
      case GamePhase.paused:
        return Stack(
          fit: StackFit.expand,
          children: [
            IgnorePointer(child: hud),
            PauseOverlay(
              onResume: _game.resume,
              onRestart: _start,
              onMenu: _menu,
            ),
          ],
        );
      case GamePhase.gameOver:
        return GameOverOverlay(
          game: _game,
          progress: widget.progress,
          onRestart: _start,
          onRevive: _revive,
          onMenu: _menu,
          onShop: () => _openShop(tab: 1),
        );
    }
  }
}
