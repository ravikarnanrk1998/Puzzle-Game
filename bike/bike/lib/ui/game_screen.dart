import 'package:flutter/gestures.dart' show kTouchSlop;
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';

import '../account/account_service.dart';
import '../audio/game_music.dart';
import '../game/bike_game.dart';
import '../game/game_painter.dart';
import '../shop/player_progress.dart';
import '../shop/store_service.dart';
import '../versus/match_backend.dart';
import '../versus/versus_match.dart';
import 'account_dialog.dart';
import 'overlays.dart';
import 'shop_screen.dart';
import 'versus_screen.dart';

class GameScreen extends StatefulWidget {
  const GameScreen({
    super.key,
    required this.progress,
    required this.store,
    this.music,
    this.account,
    this.matches,
  });

  final PlayerProgress progress;
  final StoreService store;

  /// Null means no music (tests).
  final GameMusic? music;

  /// Null means the game is running without accounts.
  final AccountService? account;

  /// Where 1 vs 1 matches are kept. Null means no 1 vs 1.
  final MatchBackend? matches;

  @override
  State<GameScreen> createState() => _GameScreenState();
}

class _GameScreenState extends State<GameScreen>
    with SingleTickerProviderStateMixin, WidgetsBindingObserver {
  final BikeGame _game = BikeGame();
  final FocusNode _focus = FocusNode();
  late final Ticker _ticker;
  Duration _last = Duration.zero;
  bool _foreground = true;

  /// Where each finger on the road went down. A finger is dropped from here
  /// once it has steered, or has moved too far to still be a tap.
  final Map<int, Offset> _touches = {};

  /// The 1 vs 1 match being played, if any, and the seconds left before
  /// its run starts.
  VersusMatch? _versus;
  double _countdown = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _game.bike = widget.progress.selectedBike;
    widget.progress.addListener(_syncBike);
    _game.addListener(_syncMusic);
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
    _versus
      ?..leave()
      ..dispose();
    _game.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    _foreground = state == AppLifecycleState.resumed;
    if (!_foreground) _game.pause();
    _syncMusic();
  }

  /// Music plays only while riding, and never with the app in the background
  /// (a crash in progress is not paused, so the phase alone is not enough).
  void _syncMusic() {
    final phase = _game.phase;
    widget.music?.setPlaying(_foreground &&
        (phase == GamePhase.playing || phase == GamePhase.crashing));
  }

  void _onTick(Duration elapsed) {
    var dt = (elapsed - _last).inMicroseconds / Duration.microsecondsPerSecond;
    _last = elapsed;
    // Clamp long frames (e.g. after a hitch) so nothing teleports.
    if (dt > 1 / 30) dt = 1 / 30;
    final versus = _versus;
    if (versus != null) {
      if (_countdown > 0) {
        _countdown -= dt;
        if (_countdown <= 0) {
          versus.start();
          _start();
        }
      } else if (_game.isRunning) {
        versus.report(_game.scoreValue);
        // The other player is out and already beaten: nothing left to ride for.
        if (versus.outcome == VersusOutcome.win) _game.finishRun();
      }
    }
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

  void _touchDown(PointerDownEvent e) {
    if (_game.isRunning) _touches[e.pointer] = e.localPosition;
  }

  /// A sideways swipe moves one lane that way, wherever on the screen it
  /// starts.
  void _touchMove(PointerMoveEvent e) {
    final start = _touches[e.pointer];
    if (start == null) return;
    final moved = e.localPosition - start;
    if (moved.distance <= kTouchSlop) return;
    _touches.remove(e.pointer);
    // Swipes up and down do nothing, and are not taps either.
    if (moved.dx.abs() > moved.dy.abs()) _game.steer(moved.dx < 0 ? -1 : 1);
  }

  /// A finger lifted without swiping was a tap: one lane towards the side
  /// of the screen it touched. Steering on the lift rather than on the touch
  /// is what keeps a swipe from counting as a tap first.
  void _touchUp(PointerUpEvent e, double width) {
    final start = _touches.remove(e.pointer);
    if (start != null) _game.steer(start.dx < width / 2 ? -1 : 1);
  }

  void _syncBike() => _game.bike = widget.progress.selectedBike;

  /// Moves the coins from this run into the wallet.
  void _bankRun() {
    widget.progress.recordRun(
      score: _game.scoreValue,
      coins: _game.coins - _game.coinsBanked,
    );
    _game.coinsBanked = _game.coins;
    _versus?.finish(_game.scoreValue);
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

  Future<void> _openAccount() async {
    await showAccountDialog(context, widget.account!);
    // Logging out or deleting the account replaces this screen with the
    // login screen.
    if (mounted) _focus.requestFocus();
  }

  void _claimDaily() {
    if (!widget.progress.claimDaily()) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(const SnackBar(
        content: Text('+${PlayerProgress.dailyDiamonds} diamond! '
            'Come back tomorrow for another.'),
        behavior: SnackBarBehavior.floating,
      ));
  }

  /// Sets up a 1 vs 1 match and, once a friend is in, counts down to it.
  Future<void> _openVersus() async {
    final account = widget.account!;
    final match = await Navigator.of(context).push(
      MaterialPageRoute<VersusMatch>(
        builder: (_) => VersusLobbyScreen(
          matches: widget.matches!,
          progress: widget.progress,
          uid: account.uid!,
          name: account.playerName,
          onGetDiamonds: () => _openShop(tab: 2),
        ),
      ),
    );
    if (!mounted) {
      match
        ?..leave()
        ..dispose();
      return;
    }
    _focus.requestFocus();
    if (match == null) return;
    setState(() {
      _versus = match;
      _countdown = 3;
    });
  }

  void _leaveVersus() {
    final versus = _versus;
    if (versus == null) return;
    setState(() {
      _versus = null;
      _countdown = 0;
    });
    versus
      ..leave()
      ..dispose();
    _menu();
  }

  KeyEventResult _onKey(FocusNode node, KeyEvent event) {
    if (event is! KeyDownEvent) return KeyEventResult.ignored;
    final key = event.logicalKey;
    final phase = _game.phase;
    // A 1 vs 1 run starts by itself and can't be started over.
    final canStart = _versus == null &&
        (phase == GamePhase.menu || phase == GamePhase.gameOver);
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
                  onPointerDown: _touchDown,
                  onPointerMove: _touchMove,
                  onPointerUp: (e) => _touchUp(e, constraints.maxWidth),
                  onPointerCancel: (e) => _touches.remove(e.pointer),
                  child: RepaintBoundary(
                    child: CustomPaint(
                      painter: GamePainter(_game),
                      size: Size.infinite,
                    ),
                  ),
                ),
                ListenableBuilder(
                  listenable:
                      Listenable.merge([_game, widget.progress, _versus]),
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
    final versus = _versus;
    if (versus != null && _countdown > 0) {
      return VersusCountdown(match: versus, seconds: _countdown.ceil());
    }
    final hud = Hud(
      game: _game,
      onPause: _game.pause,
      onBoost: _game.activateBoost,
      versus: versus,
    );
    final canVersus = widget.account != null && widget.matches != null;
    switch (_game.phase) {
      case GamePhase.menu:
        return MenuOverlay(
          game: _game,
          progress: widget.progress,
          onPlay: _start,
          onShop: _openShop,
          onVersus: canVersus ? _openVersus : null,
          onClaimDaily:
              canVersus && widget.progress.canClaimDaily() ? _claimDaily : null,
          accountName: widget.account?.name,
          onAccount: widget.account == null ? null : _openAccount,
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
              onRestart: versus == null ? _start : null,
              onMenu: versus == null ? _menu : _leaveVersus,
            ),
          ],
        );
      case GamePhase.gameOver:
        if (versus != null) {
          return VersusResultOverlay(
            match: versus,
            coinsEarned: widget.progress.lastRun?.coinsEarned ?? 0,
            onLeave: _leaveVersus,
          );
        }
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
