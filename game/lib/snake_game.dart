import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'store/diamonds.dart';
import 'store/store_service.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';

const int kSnakeCols = 13;
const int kSnakeRows = 19;

enum SnakeDirection {
  up,
  down,
  left,
  right;

  SnakeDirection get opposite => switch (this) {
    up => down,
    down => up,
    left => right,
    right => left,
  };
}

/// Turns swiped between moves, applied one per move. Two quick swipes (say up
/// then left, to double back) both happen instead of the second being dropped.
class SnakeTurns {
  final List<SnakeDirection> _queue = [];

  void clear() => _queue.clear();

  /// Queues [turn] unless it repeats or reverses the way the snake will be
  /// heading by then.
  void add(SnakeDirection turn, SnakeDirection current) {
    final heading = _queue.isEmpty ? current : _queue.last;
    if (turn == heading || turn == heading.opposite) return;
    if (_queue.length < 2) _queue.add(turn);
  }

  /// The direction for the next move.
  SnakeDirection next(SnakeDirection current) =>
      _queue.isEmpty ? current : _queue.removeAt(0);
}

class SnakeMapDef {
  final String id;
  final String name;
  final Set<Point<int>> walls;
  final bool premium;

  SnakeMapDef({
    required this.id,
    required this.name,
    required this.walls,
    this.premium = false,
  });

  bool get locked =>
      premium && !StoreService.instance.isUnlocked(LockIds.snakeMap(id));
}

Set<Point<int>> _hLine(int y, int x0, int x1) => {
  for (int x = x0; x <= x1; x++) Point(x, y),
};

Set<Point<int>> _vLine(int x, int y0, int y1) => {
  for (int y = y0; y <= y1; y++) Point(x, y),
};

Set<Point<int>> _ring(int x0, int y0, int x1, int y1) => {
  ..._hLine(y0, x0, x1),
  ..._hLine(y1, x0, x1),
  ..._vLine(x0, y0, y1),
  ..._vLine(x1, y0, y1),
};

Set<Point<int>> _bordersWalls() {
  final walls = <Point<int>>{};
  const inset = 2;
  for (int x = inset; x < kSnakeCols - inset; x++) {
    walls.add(Point(x, inset));
    walls.add(Point(x, kSnakeRows - 1 - inset));
  }
  for (int y = inset; y < kSnakeRows - inset; y++) {
    walls.add(Point(inset, y));
    walls.add(Point(kSnakeCols - 1 - inset, y));
  }
  final midX = kSnakeCols ~/ 2;
  walls.remove(Point(midX, inset));
  walls.remove(Point(midX, kSnakeRows - 1 - inset));
  return walls;
}

Set<Point<int>> _mazeWalls() {
  final walls = <Point<int>>{};
  for (int i = 1; i <= 4; i++) {
    final y = (kSnakeRows * i / 5).round();
    if (i.isOdd) {
      for (int x = 0; x < kSnakeCols - 3; x++) {
        walls.add(Point(x, y));
      }
    } else {
      for (int x = 3; x < kSnakeCols; x++) {
        walls.add(Point(x, y));
      }
    }
  }
  return walls;
}

Set<Point<int>> _crossingWalls() {
  final walls = <Point<int>>{};
  final midX = kSnakeCols ~/ 2;
  final midY = kSnakeRows ~/ 2;
  for (int y = 2; y < kSnakeRows - 2; y++) {
    if ((y - midY).abs() > 1) walls.add(Point(midX, y));
  }
  for (int x = 2; x < kSnakeCols - 2; x++) {
    if ((x - midX).abs() > 1) walls.add(Point(x, midY));
  }
  return walls;
}

/// A solid frame around the whole board: no wrapping through the edges.
Set<Point<int>> _outriWalls() => _ring(0, 0, kSnakeCols - 1, kSnakeRows - 1);

/// Two bars like an equals sign.
Set<Point<int>> _meqWalls() => {..._hLine(6, 3, 9), ..._hLine(12, 3, 9)};

/// Two full-height columns, each with a gap in the middle.
Set<Point<int>> _beltlingWalls() {
  final midY = kSnakeRows ~/ 2;
  return {..._vLine(3, 0, kSnakeRows - 1), ..._vLine(9, 0, kSnakeRows - 1)}
    ..removeWhere((p) => (p.y - midY).abs() <= 1);
}

/// Two nested frames with doorways on every side.
Set<Point<int>> _echoWalls() {
  final midX = kSnakeCols ~/ 2;
  final midY = kSnakeRows ~/ 2;
  return {..._ring(2, 2, 10, 16), ..._ring(4, 6, 8, 12)}..removeAll([
    Point(midX, 2),
    Point(midX, 16),
    Point(2, midY),
    Point(10, midY),
    Point(4, midY),
    Point(8, midY),
  ]);
}

/// A grid of 2x2 pillars.
Set<Point<int>> _seekWalls() => {
  for (final x in [2, 6, 10])
    for (final y in [3, 8, 13])
      for (int dx = 0; dx < 2; dx++)
        for (int dy = 0; dy < 2; dy++) Point(x + dx, y + dy),
};

/// Solid side walls (only top/bottom wrap) and a bar across the middle.
Set<Point<int>> _frontierWalls() => {
  ..._vLine(0, 0, kSnakeRows - 1),
  ..._vLine(kSnakeCols - 1, 0, kSnakeRows - 1),
  ..._hLine(kSnakeRows ~/ 2, 4, 8),
};

/// Four diagonals pointing in from the corners.
Set<Point<int>> _frenzyWalls() => {
  for (int i = 0; i < 4; i++) ...[
    Point(2 + i, 3 + i),
    Point(10 - i, 3 + i),
    Point(2 + i, 15 - i),
    Point(10 - i, 15 - i),
  ],
};

/// Stacked bars that leave narrow alleys, open at both sides.
Set<Point<int>> _alleysWalls() => {
  for (final y in [3, 6, 9, 12, 15]) ..._hLine(y, 2, 10),
};

final List<SnakeMapDef> kSnakeMaps = [
  SnakeMapDef(id: 'classic', name: 'Classic', walls: {}),
  SnakeMapDef(id: 'borders', name: 'Borders', walls: _bordersWalls()),
  SnakeMapDef(id: 'maze', name: 'Maze', walls: _mazeWalls()),
  SnakeMapDef(id: 'crossing', name: 'Crossing', walls: _crossingWalls()),
  SnakeMapDef(id: 'outri', name: 'Outri', walls: _outriWalls(), premium: true),
  SnakeMapDef(id: 'meq', name: 'Meq', walls: _meqWalls(), premium: true),
  SnakeMapDef(
    id: 'beltling',
    name: 'Beltling',
    walls: _beltlingWalls(),
    premium: true,
  ),
  SnakeMapDef(id: 'echo', name: 'Echo', walls: _echoWalls(), premium: true),
  SnakeMapDef(id: 'seek', name: 'Seek', walls: _seekWalls(), premium: true),
  SnakeMapDef(
    id: 'frontier',
    name: 'Frontier',
    walls: _frontierWalls(),
    premium: true,
  ),
  SnakeMapDef(
    id: 'frenzy',
    name: 'Frenzy',
    walls: _frenzyWalls(),
    premium: true,
  ),
  SnakeMapDef(
    id: 'alleys',
    name: 'Alleys',
    walls: _alleysWalls(),
    premium: true,
  ),
];

class SnakeMapSelectPage extends StatelessWidget {
  const SnakeMapSelectPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Select Map',
          style: TextStyle(fontWeight: FontWeight.w900, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
        actions: [
          const DiamondBalance(),
          IconButton(
            icon: const Icon(Icons.settings),
            tooltip: 'Speed settings',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const SnakeSettingsPage()),
              );
            },
          ),
        ],
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0B1D26), Color(0xFF091420)],
        child: SafeArea(
          child: ListenableBuilder(
            listenable: StoreService.instance,
            builder: (context, _) => GridView.builder(
              padding: const EdgeInsets.fromLTRB(20, 100, 20, 30),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 3,
                crossAxisSpacing: 14,
                mainAxisSpacing: 14,
                childAspectRatio: 0.85,
              ),
              itemCount: kSnakeMaps.length,
              itemBuilder: (context, index) =>
                  _MapTile(mapDef: kSnakeMaps[index]),
            ),
          ),
        ),
      ),
    );
  }
}

class _MapTile extends StatelessWidget {
  final SnakeMapDef mapDef;
  const _MapTile({required this.mapDef});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: () async {
        if (mapDef.locked &&
            !await confirmDiamondUnlock(
              context,
              lockId: LockIds.snakeMap(mapDef.id),
              name: '${mapDef.name} map',
            )) {
          return;
        }
        if (!context.mounted) return;
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => SnakeGamePage(mapDef: mapDef)),
        );
      },
      child: Opacity(
        opacity: mapDef.locked ? 0.6 : 1.0,
        child: GlassPanel(
          padding: const EdgeInsets.all(10),
          borderRadius: BorderRadius.circular(18),
          glowColor: mapDef.locked ? Colors.white : Colors.greenAccent,
          glowOpacity: mapDef.locked ? 0.05 : 0.2,
          blur: 10,
          child: Stack(
            children: [
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Expanded(
                    child: CustomPaint(
                      painter: _MapPreviewPainter(walls: mapDef.walls),
                      child: Container(),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    mapDef.name.toUpperCase(),
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 0.5,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  if (mapDef.locked) ...[
                    const SizedBox(height: 3),
                    const DiamondPrice(StoreService.unlockCost, size: 12),
                  ],
                ],
              ),
              if (mapDef.premium)
                Positioned(
                  top: 0,
                  right: 0,
                  child: Icon(
                    mapDef.locked ? Icons.lock : Icons.workspace_premium,
                    color: mapDef.locked ? Colors.white70 : Colors.amberAccent,
                    size: 14,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MapPreviewPainter extends CustomPainter {
  final Set<Point<int>> walls;
  _MapPreviewPainter({required this.walls});

  @override
  void paint(Canvas canvas, Size size) {
    final cellW = size.width / kSnakeCols;
    final cellH = size.height / kSnakeRows;

    canvas.drawRect(
      Rect.fromLTWH(0, 0, size.width, size.height),
      Paint()
        ..color = Colors.white.withValues(alpha: 0.06)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1,
    );

    final wallPaint = Paint()..color = Colors.greenAccent.withValues(alpha: 0.65);
    for (final w in walls) {
      canvas.drawRect(
        Rect.fromLTWH(w.x * cellW, w.y * cellH, cellW, cellH),
        wallPaint,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _MapPreviewPainter oldDelegate) => false;
}

class SnakeSettingsPage extends StatefulWidget {
  const SnakeSettingsPage({super.key});

  @override
  State<SnakeSettingsPage> createState() => _SnakeSettingsPageState();
}

class _SnakeSettingsPageState extends State<SnakeSettingsPage> {
  int speedLevel = 3;
  bool isReady = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      speedLevel = prefs.getInt('snake_speed_level') ?? 3;
      isReady = true;
    });
  }

  Future<void> _setSpeed(int level) async {
    final clamped = level.clamp(1, 5);
    setState(() => speedLevel = clamped);
    final prefs = await SharedPreferences.getInstance();
    prefs.setInt('snake_speed_level', clamped);
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF0B1D26),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Settings',
          style: TextStyle(fontWeight: FontWeight.w900, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0B1D26), Color(0xFF091420)],
        child: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(24.0),
              child: GlassPanel(
                padding: const EdgeInsets.all(24),
                borderRadius: BorderRadius.circular(24),
                glowColor: Colors.greenAccent,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'SPEED',
                      style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w900,
                        fontSize: 16,
                        letterSpacing: 2,
                      ),
                    ),
                    const SizedBox(height: 20),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        IconButton(
                          icon: const Icon(
                            Icons.chevron_left,
                            color: Colors.white,
                            size: 32,
                          ),
                          onPressed: speedLevel > 1
                              ? () => _setSpeed(speedLevel - 1)
                              : null,
                        ),
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: List.generate(5, (i) {
                            final filled = i < speedLevel;
                            return Container(
                              margin: const EdgeInsets.symmetric(horizontal: 3),
                              width: 10,
                              height: 16.0 + i * 6,
                              decoration: BoxDecoration(
                                color: filled
                                    ? Colors.greenAccent
                                    : Colors.white24,
                                borderRadius: BorderRadius.circular(3),
                                boxShadow: filled
                                    ? [
                                        BoxShadow(
                                          color: Colors.greenAccent
                                              .withValues(alpha: 0.6),
                                          blurRadius: 8,
                                        ),
                                      ]
                                    : [],
                              ),
                            );
                          }),
                        ),
                        IconButton(
                          icon: const Icon(
                            Icons.chevron_right,
                            color: Colors.white,
                            size: 32,
                          ),
                          onPressed: speedLevel < 5
                              ? () => _setSpeed(speedLevel + 1)
                              : null,
                        ),
                      ],
                    ),
                    const SizedBox(height: 28),
                    SizedBox(
                      width: double.infinity,
                      child: ElevatedButton(
                        onPressed: () => Navigator.pop(context),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: Colors.greenAccent,
                          foregroundColor: Colors.black,
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(30),
                          ),
                        ),
                        child: const Text(
                          'BACK',
                          style: TextStyle(
                            fontWeight: FontWeight.w900,
                            letterSpacing: 1,
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
      ),
    );
  }
}

class SnakeGamePage extends StatefulWidget {
  final SnakeMapDef mapDef;
  const SnakeGamePage({super.key, required this.mapDef});

  @override
  State<SnakeGamePage> createState() => _SnakeGamePageState();
}

class _SnakeGamePageState extends State<SnakeGamePage>
    with SingleTickerProviderStateMixin {
  static const List<int> _tickMillis = [300, 250, 200, 150, 100];

  late List<Point<int>> snake;
  // Where the snake was before the last move, and how far the drawing has
  // glided from there (0) to [snake] (1).
  List<Point<int>> prevSnake = [];
  late final AnimationController _moveAnim;
  int moves = 0;
  SnakeDirection direction = SnakeDirection.right;
  final SnakeTurns _turns = SnakeTurns();
  late Point<int> food;
  Timer? _timer;
  int score = 0;
  int highScore = 0;
  bool isGameOver = false;
  bool isPaused = false;
  bool isReady = false;
  int speedLevel = 3;
  final Random _random = Random();
  Offset _swipeAccum = Offset.zero;

  @override
  void initState() {
    super.initState();
    _moveAnim = AnimationController(vsync: this, value: 1);
    _loadAndStart();
  }

  Future<void> _loadAndStart() async {
    final prefs = await SharedPreferences.getInstance();
    speedLevel = prefs.getInt('snake_speed_level') ?? 3;
    highScore = prefs.getInt('snake_high_score_${widget.mapDef.id}') ?? 0;
    _startGame();
  }

  void _startGame() {
    _placeSnakeSafely();
    prevSnake = List.of(snake);
    _moveAnim.value = 1;
    moves = 0;
    direction = SnakeDirection.right;
    _turns.clear();
    score = 0;
    isGameOver = false;
    isPaused = false;
    _placeFood();
    _timer?.cancel();
    _timer = Timer.periodic(_tickDuration(), (_) => _tick());
    setState(() {
      isReady = true;
    });
  }

  void _placeSnakeSafely() {
    final walls = widget.mapDef.walls;
    int startRow = kSnakeRows ~/ 2;
    for (int r = 0; r < kSnakeRows; r++) {
      bool ok = true;
      for (int c = 1; c <= 4; c++) {
        if (walls.contains(Point(c, r))) {
          ok = false;
          break;
        }
      }
      if (ok) {
        startRow = r;
        break;
      }
    }
    snake = [Point(4, startRow), Point(3, startRow), Point(2, startRow)];
  }

  Duration _tickDuration() {
    return Duration(milliseconds: _tickMillis[(speedLevel - 1).clamp(0, 4)]);
  }

  void _placeFood() {
    Point<int> p;
    do {
      p = Point(_random.nextInt(kSnakeCols), _random.nextInt(kSnakeRows));
    } while (snake.contains(p) || widget.mapDef.walls.contains(p));
    food = p;
  }

  void _setDirection(SnakeDirection d) => _turns.add(d, direction);

  void _onPanStart(DragStartDetails details) {
    _swipeAccum = Offset.zero;
  }

  void _onPanUpdate(DragUpdateDetails details) {
    _swipeAccum += details.delta;
    const threshold = 18.0;
    if (_swipeAccum.dx.abs() > threshold || _swipeAccum.dy.abs() > threshold) {
      if (_swipeAccum.dx.abs() > _swipeAccum.dy.abs()) {
        _setDirection(
          _swipeAccum.dx > 0 ? SnakeDirection.right : SnakeDirection.left,
        );
      } else {
        _setDirection(
          _swipeAccum.dy > 0 ? SnakeDirection.down : SnakeDirection.up,
        );
      }
      _swipeAccum = Offset.zero;
    }
  }

  void _tick() {
    if (isPaused || isGameOver) return;
    direction = _turns.next(direction);

    final head = snake.first;
    Point<int> newHead;
    switch (direction) {
      case SnakeDirection.up:
        newHead = Point(head.x, head.y - 1);
        break;
      case SnakeDirection.down:
        newHead = Point(head.x, head.y + 1);
        break;
      case SnakeDirection.left:
        newHead = Point(head.x - 1, head.y);
        break;
      case SnakeDirection.right:
        newHead = Point(head.x + 1, head.y);
        break;
    }

    newHead = Point(
      (newHead.x + kSnakeCols) % kSnakeCols,
      (newHead.y + kSnakeRows) % kSnakeRows,
    );

    final hitWall = widget.mapDef.walls.contains(newHead);
    final hitSelf = snake.contains(newHead);

    if (hitWall || hitSelf) {
      _gameOver();
      return;
    }

    setState(() {
      prevSnake = List.of(snake);
      moves++;
      snake.insert(0, newHead);
      if (newHead == food) {
        score += 10;
        _placeFood();
      } else {
        snake.removeLast();
      }
    });
    _moveAnim.duration = _tickDuration();
    _moveAnim.forward(from: 0);
  }

  void _gameOver() {
    _timer?.cancel();
    setState(() {
      isGameOver = true;
    });
    _saveHighScore();
    Future.delayed(const Duration(milliseconds: 150), () {
      if (!mounted) return;
      _showGameOverDialog();
    });
  }

  Future<void> _saveHighScore() async {
    if (score > highScore) {
      highScore = score;
      final prefs = await SharedPreferences.getInstance();
      prefs.setInt('snake_high_score_${widget.mapDef.id}', highScore);
    }
  }

  void _togglePause() {
    setState(() {
      isPaused = !isPaused;
    });
  }

  Future<void> _openSettings() async {
    setState(() => isPaused = true);
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const SnakeSettingsPage()),
    );
    if (!mounted) return;
    final prefs = await SharedPreferences.getInstance();
    final newSpeed = prefs.getInt('snake_speed_level') ?? speedLevel;
    if (newSpeed != speedLevel) {
      speedLevel = newSpeed;
      _timer?.cancel();
      _timer = Timer.periodic(_tickDuration(), (_) => _tick());
    }
    setState(() => isPaused = false);
  }

  void _showGameOverDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => Dialog(
        backgroundColor: Colors.transparent,
        child: Container(
          padding: const EdgeInsets.all(30),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: [Color(0xFF0F2027), Color(0xFF203A43)],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(30),
            border: Border.all(color: Colors.greenAccent.withValues(alpha: 0.4)),
            boxShadow: [
              BoxShadow(
                color: Colors.greenAccent.withValues(alpha: 0.3),
                blurRadius: 30,
                spreadRadius: 5,
              ),
            ],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'GAME OVER',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 30,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 3,
                ),
              ),
              const SizedBox(height: 16),
              Text(
                'Score: $score',
                style: const TextStyle(
                  color: Colors.greenAccent,
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                'Best: $highScore',
                style: const TextStyle(color: Colors.white70, fontSize: 14),
              ),
              const SizedBox(height: 28),
              ElevatedButton(
                onPressed: () {
                  Navigator.of(context).pop();
                  _startGame();
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.greenAccent,
                  foregroundColor: Colors.black,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 40,
                    vertical: 14,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(30),
                  ),
                ),
                child: const Text(
                  'PLAY AGAIN',
                  style: TextStyle(fontWeight: FontWeight.w900),
                ),
              ),
              const SizedBox(height: 10),
              TextButton(
                onPressed: () {
                  Navigator.of(context).pop();
                  Navigator.of(context).pop();
                },
                child: const Text(
                  'Main Menu',
                  style: TextStyle(color: Colors.white70),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  void dispose() {
    _timer?.cancel();
    _moveAnim.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF0B1D26),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0B1D26), Color(0xFF091420)],
        child: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 12,
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    GlassPanel(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 8,
                      ),
                      borderRadius: BorderRadius.circular(16),
                      glowColor: Colors.greenAccent,
                      blur: 8,
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'SCORE: $score',
                            style: const TextStyle(
                              color: Colors.greenAccent,
                              fontWeight: FontWeight.w900,
                              fontSize: 16,
                              letterSpacing: 1,
                            ),
                          ),
                          const SizedBox(width: 12),
                          Text(
                            'BEST: $highScore',
                            style: const TextStyle(
                              color: Colors.white70,
                              fontWeight: FontWeight.bold,
                              fontSize: 13,
                              letterSpacing: 0.5,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Row(
                      children: [
                        _iconGlassButton(
                          icon: Icons.settings,
                          onTap: _openSettings,
                        ),
                        const SizedBox(width: 10),
                        _iconGlassButton(
                          icon: isPaused ? Icons.play_arrow : Icons.pause,
                          onTap: _togglePause,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              Expanded(
                child: Center(
                  child: AspectRatio(
                    aspectRatio: kSnakeCols / kSnakeRows,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: GestureDetector(
                        onPanStart: _onPanStart,
                        onPanUpdate: _onPanUpdate,
                        child: GlassPanel(
                          padding: const EdgeInsets.all(6),
                          borderRadius: BorderRadius.circular(20),
                          glowColor: Colors.greenAccent,
                          blur: 6,
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(14),
                            child: Stack(
                              fit: StackFit.expand,
                              children: [
                                // Repaints every frame while the snake
                                // glides; keep that off the glass panels.
                                RepaintBoundary(
                                  child: CustomPaint(
                                    painter: _SnakeBoardPainter(
                                      snake: snake,
                                      prevSnake: prevSnake,
                                      progress: _moveAnim,
                                      tongueOut: moves % 10 < 2,
                                      food: food,
                                      walls: widget.mapDef.walls,
                                    ),
                                  ),
                                ),
                                if (isPaused)
                                  Container(
                                    color: Colors.black.withValues(
                                      alpha: 0.55,
                                    ),
                                    alignment: Alignment.center,
                                    child: Column(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        const Text(
                                          'PAUSED',
                                          style: TextStyle(
                                            color: Colors.white,
                                            fontSize: 26,
                                            fontWeight: FontWeight.w900,
                                            letterSpacing: 3,
                                          ),
                                        ),
                                        const SizedBox(height: 14),
                                        IconButton(
                                          icon: const Icon(
                                            Icons.play_circle_fill,
                                            color: Colors.greenAccent,
                                            size: 56,
                                          ),
                                          onPressed: _togglePause,
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
                ),
              ),
              const SizedBox(height: 20),
            ],
          ),
        ),
      ),
    );
  }

  Widget _iconGlassButton({required IconData icon, required VoidCallback onTap}) {
    return GestureDetector(
      onTap: onTap,
      child: GlassPanel(
        padding: const EdgeInsets.all(8),
        borderRadius: BorderRadius.circular(30),
        glowColor: Colors.cyanAccent,
        blur: 8,
        child: Icon(icon, color: Colors.white, size: 22),
      ),
    );
  }

}

class _SnakeBoardPainter extends CustomPainter {
  final List<Point<int>> snake;
  final List<Point<int>> prevSnake;

  /// 0 right after a move, reaching 1 by the next one.
  final Animation<double> progress;
  final bool tongueOut;
  final Point<int> food;
  final Set<Point<int>> walls;

  _SnakeBoardPainter({
    required this.snake,
    required this.prevSnake,
    required this.progress,
    required this.tongueOut,
    required this.food,
    required this.walls,
  }) : super(repaint: progress);

  static const _headColor = Color(0xFF8BF26B);
  static const _tailColor = Color(0xFF1F7A45);
  static const _outlineColor = Color(0xFF062B17);
  static const _spotColor = Color(0xFF145C34);
  static const _tongueColor = Color(0xFFFF3B5C);

  @override
  void paint(Canvas canvas, Size size) {
    final cellW = size.width / kSnakeCols;
    final cellH = size.height / kSnakeRows;
    canvas.clipRect(Offset.zero & size);

    canvas.drawRect(
      Offset.zero & size,
      Paint()..color = const Color(0xFF081018),
    );

    final gridPaint = Paint()
      ..color = Colors.white.withValues(alpha: 0.035)
      ..strokeWidth = 1;
    for (int c = 1; c < kSnakeCols; c++) {
      canvas.drawLine(
        Offset(c * cellW, 0),
        Offset(c * cellW, size.height),
        gridPaint,
      );
    }
    for (int r = 1; r < kSnakeRows; r++) {
      canvas.drawLine(
        Offset(0, r * cellH),
        Offset(size.width, r * cellH),
        gridPaint,
      );
    }

    final wallPaint = Paint()..color = const Color(0xFF6C63FF).withValues(alpha: 0.55);
    for (final w in walls) {
      final rect = Rect.fromLTWH(
        w.x * cellW + 1,
        w.y * cellH + 1,
        cellW - 2,
        cellH - 2,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, const Radius.circular(3)),
        wallPaint,
      );
    }

    final t = progress.value;
    final grew = snake.length > prevSnake.length;
    final foodRadius = min(cellW, cellH) * 0.35;
    _paintFood(canvas, food, foodRadius, cellW, cellH);
    // The food just eaten shrinks away as the head slides over it.
    if (grew && t < 1) {
      _paintFood(canvas, snake.first, foodRadius * (1 - t), cellW, cellH);
    }

    _paintSnake(canvas, cellW, cellH, t, grew);
  }

  void _paintFood(
    Canvas canvas,
    Point<int> at,
    double radius,
    double cellW,
    double cellH,
  ) {
    final center = Offset((at.x + 0.5) * cellW, (at.y + 0.5) * cellH);
    canvas.drawCircle(
      center,
      radius * 1.8,
      Paint()
        ..color = Colors.pinkAccent.withValues(alpha: 0.5)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 8),
    );
    canvas.drawCircle(center, radius, Paint()..color = Colors.pinkAccent);
  }

  /// One step between neighbouring cells, -1/0/1 per axis, taking the short
  /// way across a wrapped board edge.
  static Offset _step(Point<int> from, Point<int> to) {
    var dx = to.x - from.x;
    var dy = to.y - from.y;
    if (dx > 1) dx -= kSnakeCols;
    if (dx < -1) dx += kSnakeCols;
    if (dy > 1) dy -= kSnakeRows;
    if (dy < -1) dy += kSnakeRows;
    return Offset(dx.toDouble(), dy.toDouble());
  }

  void _paintSnake(
    Canvas canvas,
    double cellW,
    double cellH,
    double t,
    bool grew,
  ) {
    final n = snake.length;
    if (n < 2) return;

    // Cell positions along the body, unwrapped so it stays in one piece where
    // it crosses a board edge.
    final cells = [Offset(snake[0].x.toDouble(), snake[0].y.toDouble())];
    for (int i = 1; i < n; i++) {
      cells.add(cells[i - 1] + _step(snake[i - 1], snake[i]));
    }
    final oldTail = grew
        ? cells.last
        : cells.last + _step(snake.last, prevSnake.last);

    // The head glides into its new cell and the tail out of its old one.
    final path = [
      Offset.lerp(cells[1], cells[0], t)!,
      ...cells.skip(1),
      if (!grew) Offset.lerp(oldTail, cells.last, t)!,
    ];

    // Each body segment slides from where the one behind it was, so its
    // markings move with the body instead of jumping cell to cell.
    final markings = <Offset>[];
    for (int j = 1; j < n - 1; j++) {
      final from = cells[j + 1];
      final at = Offset.lerp(from, cells[j], t)!;
      final along = cells[j] - from;
      final side = Offset(-along.dy, along.dx) * (j.isEven ? 0.17 : -0.17);
      markings.add(at + side);
    }

    var minX = path.first.dx, maxX = minX;
    var minY = path.first.dy, maxY = minY;
    for (final p in path) {
      minX = min(minX, p.dx);
      maxX = max(maxX, p.dx);
      minY = min(minY, p.dy);
      maxY = max(maxY, p.dy);
    }
    // Draw it once per board-sized shift that lands on the board, so a snake
    // passing through an edge shows on both sides. The head and tongue reach
    // this many cells past a cell center.
    const reach = 1.6;
    final firstKx = ((minX + 0.5 - reach) / kSnakeCols).floor();
    final lastKx = ((maxX + 0.5 + reach) / kSnakeCols).floor();
    final firstKy = ((minY + 0.5 - reach) / kSnakeRows).floor();
    final lastKy = ((maxY + 0.5 + reach) / kSnakeRows).floor();
    for (int kx = firstKx; kx <= lastKx; kx++) {
      for (int ky = firstKy; ky <= lastKy; ky++) {
        final shift = Offset(
          -kx * kSnakeCols.toDouble(),
          -ky * kSnakeRows.toDouble(),
        );
        _paintSnakeBody(
          canvas,
          path,
          markings,
          cells[0] - cells[1],
          shift,
          cellW,
          cellH,
        );
      }
    }
  }

  /// [heading] is the direction of the last move, one cell long. (Not taken
  /// from [path]: right after a move its first two points coincide.)
  void _paintSnakeBody(
    Canvas canvas,
    List<Offset> path,
    List<Offset> markings,
    Offset heading,
    Offset shift,
    double cellW,
    double cellH,
  ) {
    Offset px(Offset c) =>
        Offset((c.dx + shift.dx + 0.5) * cellW, (c.dy + shift.dy + 0.5) * cellH);
    final base = min(cellW, cellH);
    final pts = [for (final p in path) px(p)];
    final last = pts.length - 1;
    // Full width along the front, narrowing to a point at the tail.
    double widthAt(int i) {
      final f = (i / last).clamp(0.0, 1.0);
      return base * (f < 0.45 ? 0.74 : 0.74 - (f - 0.45) / 0.55 * 0.62);
    }

    // A tube through the points: a round joint at each one and a tapered
    // quad between neighbours.
    void drawTube(double extra, Color Function(int i) colorAt) {
      final paint = Paint();
      for (int i = 0; i <= last; i++) {
        final wa = widthAt(i) + extra;
        paint.color = colorAt(i);
        canvas.drawCircle(pts[i], wa / 2, paint);
        if (i == last) break;
        final a = pts[i];
        final b = pts[i + 1];
        final d = b - a;
        if (d.distance < 0.01) continue;
        final nrm = Offset(-d.dy, d.dx) / d.distance;
        final wb = widthAt(i + 1) + extra;
        canvas.drawPath(
          Path()
            ..moveTo(a.dx + nrm.dx * wa / 2, a.dy + nrm.dy * wa / 2)
            ..lineTo(b.dx + nrm.dx * wb / 2, b.dy + nrm.dy * wb / 2)
            ..lineTo(b.dx - nrm.dx * wb / 2, b.dy - nrm.dy * wb / 2)
            ..lineTo(a.dx - nrm.dx * wa / 2, a.dy - nrm.dy * wa / 2)
            ..close(),
          paint,
        );
      }
    }

    drawTube(base * 0.12, (_) => _outlineColor);
    drawTube(0, (i) => Color.lerp(_headColor, _tailColor, i / last)!);

    final shine = Offset(-1, -1) * (base * 0.1);
    final stroke = Paint()
      ..strokeCap = StrokeCap.round
      ..color = Colors.white.withValues(alpha: 0.16);
    for (int k = 0; k < last - 1; k++) {
      stroke.strokeWidth = widthAt(k) * 0.3;
      canvas.drawLine(pts[k] + shine, pts[k + 1] + shine, stroke);
    }
    final spotPaint = Paint()..color = _spotColor;
    for (int j = 0; j < markings.length; j++) {
      canvas.drawCircle(px(markings[j]), widthAt(j + 1) * 0.17, spotPaint);
    }

    _paintHead(canvas, pts.first, atan2(heading.dy, heading.dx), base);
  }

  void _paintHead(Canvas canvas, Offset center, double angle, double base) {
    final r = base * 0.46;
    canvas.save();
    canvas.translate(center.dx, center.dy);
    canvas.rotate(angle);

    if (tongueOut) {
      final tongue = Paint()
        ..color = _tongueColor
        ..strokeWidth = max(1.5, base * 0.07)
        ..strokeCap = StrokeCap.round;
      final fork = Offset(r * 2.0, 0);
      canvas.drawLine(Offset(r * 1.2, 0), fork, tongue);
      canvas.drawLine(fork, fork + Offset(r * 0.32, -r * 0.24), tongue);
      canvas.drawLine(fork, fork + Offset(r * 0.32, r * 0.24), tongue);
    }

    final head = Rect.fromCenter(
      center: Offset(r * 0.2, 0),
      width: r * 2.5,
      height: r * 2.0,
    );
    canvas.drawOval(
      head.inflate(r * 0.15),
      Paint()
        ..color = _headColor.withValues(alpha: 0.35)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6),
    );
    canvas.drawOval(head.inflate(base * 0.06), Paint()..color = _outlineColor);
    canvas.drawOval(head, Paint()..color = _headColor);

    final white = Paint()..color = Colors.white;
    final pupil = Paint()..color = Colors.black;
    for (final side in [-1.0, 1.0]) {
      final eye = Offset(r * 0.6, side * r * 0.48);
      canvas.drawCircle(eye, r * 0.3, white);
      canvas.drawCircle(eye + Offset(r * 0.1, 0), r * 0.16, pupil);
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _SnakeBoardPainter oldDelegate) => true;
}
