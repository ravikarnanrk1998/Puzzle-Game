import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';

const int kSnakeCols = 13;
const int kSnakeRows = 19;

enum SnakeDirection { up, down, left, right }

class SnakeMapDef {
  final String id;
  final String name;
  final Set<Point<int>> walls;
  final bool locked;

  SnakeMapDef({
    required this.id,
    required this.name,
    required this.walls,
    this.locked = false,
  });
}

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

final List<SnakeMapDef> kSnakeMaps = [
  SnakeMapDef(id: 'classic', name: 'Classic', walls: {}),
  SnakeMapDef(id: 'borders', name: 'Borders', walls: _bordersWalls()),
  SnakeMapDef(id: 'maze', name: 'Maze', walls: _mazeWalls()),
  SnakeMapDef(id: 'crossing', name: 'Crossing', walls: _crossingWalls()),
  SnakeMapDef(id: 'outri', name: 'Outri', walls: {}, locked: true),
  SnakeMapDef(id: 'meq', name: 'Meq', walls: {}, locked: true),
  SnakeMapDef(id: 'beltling', name: 'Beltling', walls: {}, locked: true),
  SnakeMapDef(id: 'echo', name: 'Echo', walls: {}, locked: true),
  SnakeMapDef(id: 'seek', name: 'Seek', walls: {}, locked: true),
  SnakeMapDef(id: 'frontier', name: 'Frontier', walls: {}, locked: true),
  SnakeMapDef(id: 'frenzy', name: 'Frenzy', walls: {}, locked: true),
  SnakeMapDef(id: 'alleys', name: 'Alleys', walls: {}, locked: true),
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
        orbColors: const [Colors.greenAccent, Colors.cyanAccent],
        child: SafeArea(
          child: GridView.builder(
            padding: const EdgeInsets.fromLTRB(20, 100, 20, 30),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3,
              crossAxisSpacing: 14,
              mainAxisSpacing: 14,
              childAspectRatio: 0.85,
            ),
            itemCount: kSnakeMaps.length,
            itemBuilder: (context, index) => _MapTile(mapDef: kSnakeMaps[index]),
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
      onTap: mapDef.locked
          ? () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Coming soon!')),
              );
            }
          : () {
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => SnakeGamePage(mapDef: mapDef),
                ),
              );
            },
      child: Opacity(
        opacity: mapDef.locked ? 0.45 : 1.0,
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
                ],
              ),
              if (mapDef.locked)
                const Positioned(
                  top: 0,
                  right: 0,
                  child: Icon(Icons.lock, color: Colors.white70, size: 14),
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
        orbColors: const [Colors.greenAccent, Colors.cyanAccent],
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

class _SnakeGamePageState extends State<SnakeGamePage> {
  static const List<int> _tickMillis = [300, 250, 200, 150, 100];

  late List<Point<int>> snake;
  SnakeDirection direction = SnakeDirection.right;
  SnakeDirection? pendingDirection;
  late Point<int> food;
  Timer? _timer;
  int score = 0;
  int highScore = 0;
  bool isGameOver = false;
  bool isPaused = false;
  bool isReady = false;
  int speedLevel = 3;
  final Random _random = Random();

  @override
  void initState() {
    super.initState();
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
    direction = SnakeDirection.right;
    pendingDirection = null;
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

  void _setDirection(SnakeDirection d) {
    if ((direction == SnakeDirection.up && d == SnakeDirection.down) ||
        (direction == SnakeDirection.down && d == SnakeDirection.up) ||
        (direction == SnakeDirection.left && d == SnakeDirection.right) ||
        (direction == SnakeDirection.right && d == SnakeDirection.left)) {
      return;
    }
    pendingDirection = d;
  }

  void _tick() {
    if (isPaused || isGameOver) return;
    if (pendingDirection != null) {
      direction = pendingDirection!;
      pendingDirection = null;
    }

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
      snake.insert(0, newHead);
      if (newHead == food) {
        score += 10;
        _placeFood();
      } else {
        snake.removeLast();
      }
    });
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
        orbColors: const [Colors.greenAccent, Colors.cyanAccent],
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
                      child: Text(
                        'SCORE: $score',
                        style: const TextStyle(
                          color: Colors.greenAccent,
                          fontWeight: FontWeight.w900,
                          fontSize: 16,
                          letterSpacing: 1,
                        ),
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
                              CustomPaint(
                                painter: _SnakeBoardPainter(
                                  snake: snake,
                                  food: food,
                                  walls: widget.mapDef.walls,
                                ),
                              ),
                              if (isPaused)
                                Container(
                                  color: Colors.black.withValues(alpha: 0.55),
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
              const SizedBox(height: 14),
              _buildDPad(),
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

  Widget _buildDPad() {
    Widget arrowButton(IconData icon, SnakeDirection d) {
      return GestureDetector(
        onTap: () => _setDirection(d),
        child: GlassPanel(
          padding: const EdgeInsets.all(14),
          borderRadius: BorderRadius.circular(16),
          glowColor: Colors.cyanAccent,
          blur: 8,
          child: Icon(icon, color: Colors.white, size: 26),
        ),
      );
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        arrowButton(Icons.keyboard_arrow_up, SnakeDirection.up),
        const SizedBox(height: 8),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            arrowButton(Icons.keyboard_arrow_left, SnakeDirection.left),
            const SizedBox(width: 60),
            arrowButton(Icons.keyboard_arrow_right, SnakeDirection.right),
          ],
        ),
        const SizedBox(height: 8),
        arrowButton(Icons.keyboard_arrow_down, SnakeDirection.down),
      ],
    );
  }
}

class _SnakeBoardPainter extends CustomPainter {
  final List<Point<int>> snake;
  final Point<int> food;
  final Set<Point<int>> walls;

  _SnakeBoardPainter({
    required this.snake,
    required this.food,
    required this.walls,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final cellW = size.width / kSnakeCols;
    final cellH = size.height / kSnakeRows;

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

    final foodCenter = Offset((food.x + 0.5) * cellW, (food.y + 0.5) * cellH);
    final foodRadius = min(cellW, cellH) * 0.35;
    canvas.drawCircle(
      foodCenter,
      foodRadius * 1.8,
      Paint()
        ..color = Colors.pinkAccent.withValues(alpha: 0.5)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 8),
    );
    canvas.drawCircle(foodCenter, foodRadius, Paint()..color = Colors.pinkAccent);

    for (int i = 0; i < snake.length; i++) {
      final seg = snake[i];
      final isHead = i == 0;
      final rect = Rect.fromLTWH(
        seg.x * cellW + 1.5,
        seg.y * cellH + 1.5,
        cellW - 3,
        cellH - 3,
      );
      final rrect = RRect.fromRectAndRadius(
        rect,
        Radius.circular(isHead ? 6 : 4),
      );
      final t = snake.length <= 1 ? 1.0 : 1 - (i / snake.length) * 0.5;
      final color = Color.lerp(Colors.cyanAccent, const Color(0xFF00695C), 1 - t)!;

      if (isHead) {
        canvas.drawRRect(
          rrect,
          Paint()
            ..color = Colors.cyanAccent.withValues(alpha: 0.5)
            ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 6),
        );
      }
      canvas.drawRRect(rrect, Paint()..color = color);
    }
  }

  @override
  bool shouldRepaint(covariant _SnakeBoardPainter oldDelegate) => true;
}
