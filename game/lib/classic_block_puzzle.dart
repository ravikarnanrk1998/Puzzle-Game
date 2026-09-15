import 'dart:async';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';
import 'ui/result_dialog.dart';

const int kBoardRows = 20;
const int kBoardCols = 10;

enum TetrominoType { i, o, t, s, z, j, l }

class _PieceDef {
  final List<List<bool>> matrix;
  final Color color;
  const _PieceDef(this.matrix, this.color);
}

final Map<TetrominoType, _PieceDef> _pieceDefs = {
  TetrominoType.i: _PieceDef(const [
    [false, false, false, false],
    [true, true, true, true],
    [false, false, false, false],
    [false, false, false, false],
  ], const Color(0xFF00E5FF)),
  TetrominoType.o: _PieceDef(const [
    [true, true],
    [true, true],
  ], const Color(0xFFFFD700)),
  TetrominoType.t: _PieceDef(const [
    [false, true, false],
    [true, true, true],
    [false, false, false],
  ], const Color(0xFFD500F9)),
  TetrominoType.s: _PieceDef(const [
    [false, true, true],
    [true, true, false],
    [false, false, false],
  ], const Color(0xFF00E676)),
  TetrominoType.z: _PieceDef(const [
    [true, true, false],
    [false, true, true],
    [false, false, false],
  ], const Color(0xFFFF1744)),
  TetrominoType.j: _PieceDef(const [
    [true, false, false],
    [true, true, true],
    [false, false, false],
  ], const Color(0xFF2979FF)),
  TetrominoType.l: _PieceDef(const [
    [false, false, true],
    [true, true, true],
    [false, false, false],
  ], const Color(0xFFFF9100)),
};

List<List<bool>> _rotateCW(List<List<bool>> m) {
  final n = m.length;
  return List.generate(n, (r) => List.generate(n, (c) => m[n - 1 - c][r]));
}

class _ActivePiece {
  final TetrominoType type;
  List<List<bool>> shape;
  final Color color;
  int row;
  int col;

  _ActivePiece({
    required this.type,
    required this.shape,
    required this.color,
    required this.row,
    required this.col,
  });
}

class ClassicBlockPuzzlePage extends StatefulWidget {
  const ClassicBlockPuzzlePage({super.key});

  @override
  State<ClassicBlockPuzzlePage> createState() =>
      _ClassicBlockPuzzlePageState();
}

class _ClassicBlockPuzzlePageState extends State<ClassicBlockPuzzlePage> {
  late List<List<Color?>> board;
  _ActivePiece? current;
  TetrominoType? nextType;
  TetrominoType? heldType;
  bool holdUsed = false;
  List<TetrominoType> _bag = [];
  int score = 0;
  int highScore = 0;
  int level = 1;
  int linesCleared = 0;
  bool isGameOver = false;
  bool isPaused = false;
  bool isReady = false;
  Timer? _timer;
  final Random _random = Random();
  double _dxAccum = 0;
  double _dyAccum = 0;
  Timer? _repeatTimer;

  @override
  void initState() {
    super.initState();
    _loadHighScoreAndStart();
  }

  Future<void> _loadHighScoreAndStart() async {
    final prefs = await SharedPreferences.getInstance();
    highScore = prefs.getInt('classic_block_high_score') ?? 0;
    _startGame();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _repeatTimer?.cancel();
    super.dispose();
  }

  void _startGame() {
    board = List.generate(
      kBoardRows,
      (_) => List<Color?>.filled(kBoardCols, null),
    );
    score = 0;
    level = 1;
    linesCleared = 0;
    isGameOver = false;
    isPaused = false;
    heldType = null;
    holdUsed = false;
    _bag = [];
    current = null;
    nextType = _drawFromBag();
    _spawnPiece();
    _restartTimer();
    setState(() {
      isReady = true;
    });
  }

  TetrominoType _drawFromBag() {
    if (_bag.isEmpty) {
      _bag = TetrominoType.values.toList()..shuffle(_random);
    }
    return _bag.removeLast();
  }

  void _spawnPiece() {
    final type = nextType ?? _drawFromBag();
    nextType = _drawFromBag();
    final def = _pieceDefs[type]!;
    final n = def.matrix.length;
    final col = (kBoardCols - n) ~/ 2;
    final piece = _ActivePiece(
      type: type,
      shape: def.matrix.map((r) => List<bool>.from(r)).toList(),
      color: def.color,
      row: 0,
      col: col,
    );
    current = piece;
    holdUsed = false;
    if (_collides(piece.shape, piece.row, piece.col)) {
      _gameOver();
    }
  }

  bool _collides(List<List<bool>> shape, int row, int col) {
    for (int r = 0; r < shape.length; r++) {
      for (int c = 0; c < shape[r].length; c++) {
        if (!shape[r][c]) continue;
        final br = row + r;
        final bc = col + c;
        if (bc < 0 || bc >= kBoardCols || br >= kBoardRows) return true;
        if (br >= 0 && board[br][bc] != null) return true;
      }
    }
    return false;
  }

  bool _tryMove(int dr, int dc) {
    if (current == null || isPaused || isGameOver) return false;
    final p = current!;
    final newRow = p.row + dr;
    final newCol = p.col + dc;
    if (_collides(p.shape, newRow, newCol)) return false;
    setState(() {
      p.row = newRow;
      p.col = newCol;
    });
    return true;
  }

  void _rotate() {
    if (current == null || isPaused || isGameOver) return;
    final p = current!;
    final rotated = _rotateCW(p.shape);
    const kicks = [0, -1, 1, -2, 2];
    for (final k in kicks) {
      if (!_collides(rotated, p.row, p.col + k)) {
        setState(() {
          p.shape = rotated;
          p.col += k;
        });
        return;
      }
    }
  }

  void _softDrop() {
    if (current == null || isPaused || isGameOver) return;
    if (_tryMove(1, 0)) {
      setState(() => score += 1);
    } else {
      _lockPiece();
    }
  }

  void _hardDrop() {
    if (current == null || isPaused || isGameOver) return;
    int dropped = 0;
    while (_tryMove(1, 0)) {
      dropped++;
    }
    if (dropped > 0) {
      setState(() => score += dropped * 2);
    }
    _lockPiece();
  }

  void _lockPiece() {
    if (current == null) return;
    final p = current!;
    for (int r = 0; r < p.shape.length; r++) {
      for (int c = 0; c < p.shape[r].length; c++) {
        if (!p.shape[r][c]) continue;
        final br = p.row + r;
        final bc = p.col + c;
        if (br < 0) continue;
        board[br][bc] = p.color;
      }
    }
    current = null;
    _clearLines();
    if (!isGameOver) {
      _spawnPiece();
    }
    setState(() {});
  }

  void _clearLines() {
    final fullRows = <int>[];
    for (int r = 0; r < kBoardRows; r++) {
      if (board[r].every((c) => c != null)) fullRows.add(r);
    }
    if (fullRows.isEmpty) return;

    final keptRows = <List<Color?>>[];
    for (int r = 0; r < kBoardRows; r++) {
      if (!fullRows.contains(r)) keptRows.add(board[r]);
    }
    final newBoard = List.generate(
      fullRows.length,
      (_) => List<Color?>.filled(kBoardCols, null),
    );
    newBoard.addAll(keptRows);
    board = newBoard;

    linesCleared += fullRows.length;
    const points = {1: 100, 2: 300, 3: 500, 4: 800};
    score += (points[fullRows.length] ?? 800) * level;

    final newLevel = (linesCleared ~/ 10) + 1;
    if (newLevel != level) {
      level = newLevel;
      _restartTimer();
    }
  }

  void _hold() {
    if (current == null || holdUsed || isPaused || isGameOver) return;
    final currentType = current!.type;
    setState(() {
      if (heldType == null) {
        heldType = currentType;
        current = null;
        _spawnPiece();
      } else {
        final swapType = heldType!;
        heldType = currentType;
        final def = _pieceDefs[swapType]!;
        final n = def.matrix.length;
        final col = (kBoardCols - n) ~/ 2;
        current = _ActivePiece(
          type: swapType,
          shape: def.matrix.map((r) => List<bool>.from(r)).toList(),
          color: def.color,
          row: 0,
          col: col,
        );
      }
      holdUsed = true;
    });
  }

  Duration _tickDuration() {
    final ms = (500 - (level - 1) * 35).clamp(90, 500);
    return Duration(milliseconds: ms);
  }

  void _restartTimer() {
    _timer?.cancel();
    _timer = Timer.periodic(_tickDuration(), (_) => _tick());
  }

  void _tick() {
    if (isPaused || isGameOver) return;
    if (!_tryMove(1, 0)) {
      _lockPiece();
    }
  }

  void _togglePause() {
    if (isGameOver) return;
    setState(() => isPaused = !isPaused);
  }

  void _gameOver() {
    _timer?.cancel();
    isGameOver = true;
    _saveHighScore();
    Future.delayed(const Duration(milliseconds: 150), () {
      if (!mounted) return;
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => ResultDialog(
          icon: Icons.videogame_asset_off_rounded,
          accentColor: Colors.orangeAccent,
          title: 'GAME OVER',
          stats: [
            ResultStat('SCORE', '$score', highlight: true),
            ResultStat('LEVEL', '$level'),
            ResultStat('LINES', '$linesCleared'),
          ],
          primaryLabel: 'PLAY AGAIN',
          onPrimary: () {
            Navigator.of(context).pop();
            _startGame();
          },
          secondaryLabel: 'Main Menu',
          onSecondary: () {
            Navigator.of(context).pop();
            Navigator.of(context).pop();
          },
        ),
      );
    });
  }

  Future<void> _saveHighScore() async {
    if (score > highScore) {
      highScore = score;
      final prefs = await SharedPreferences.getInstance();
      prefs.setInt('classic_block_high_score', highScore);
    }
  }

  int _ghostRowOffset() {
    if (current == null) return 0;
    final p = current!;
    int offset = 0;
    while (!_collides(p.shape, p.row + offset + 1, p.col)) {
      offset++;
    }
    return offset;
  }

  void _onPanStart(DragStartDetails details) {
    _dxAccum = 0;
    _dyAccum = 0;
  }

  void _onPanUpdate(DragUpdateDetails details) {
    _dxAccum += details.delta.dx;
    _dyAccum += details.delta.dy;
    const moveThreshold = 24.0;
    if (_dxAccum.abs() > moveThreshold) {
      _tryMove(0, _dxAccum > 0 ? 1 : -1);
      _dxAccum = 0;
    }
    if (_dyAccum > moveThreshold) {
      _softDrop();
      _dyAccum = 0;
    }
  }

  void _onPanEnd(DragEndDetails details) {
    if (details.velocity.pixelsPerSecond.dy > 1000) {
      _hardDrop();
    }
    _dxAccum = 0;
    _dyAccum = 0;
  }

  void _startRepeat(VoidCallback action) {
    action();
    _repeatTimer?.cancel();
    _repeatTimer = Timer.periodic(
      const Duration(milliseconds: 120),
      (_) => action(),
    );
  }

  void _stopRepeat() {
    _repeatTimer?.cancel();
    _repeatTimer = null;
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF120B24),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF120B24), Color(0xFF1E1140)],
        child: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 6),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    GestureDetector(
                      onTap: _togglePause,
                      child: GlassPanel(
                        padding: const EdgeInsets.all(10),
                        borderRadius: BorderRadius.circular(30),
                        glowColor: Colors.amberAccent,
                        blur: 8,
                        child: Icon(
                          isPaused ? Icons.play_arrow : Icons.pause,
                          color: Colors.white,
                          size: 22,
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: GlassPanel(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 8,
                        ),
                        borderRadius: BorderRadius.circular(16),
                        glowColor: Colors.amberAccent,
                        blur: 8,
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            _statColumn('LEVEL', '$level'),
                            _statColumn('SCORE', '$score'),
                            _statColumn('BEST', '$highScore'),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    _previewPanel('NEXT', nextType, onTap: null),
                    const SizedBox(width: 10),
                    _previewPanel('HOLD', heldType, onTap: _hold),
                  ],
                ),
              ),
              const SizedBox(height: 4),
              const Text(
                'Swipe to move • Tap to rotate • Swipe down to drop • Flick down to slam',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: Colors.white54,
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 8),
              Expanded(
                child: Center(
                  child: AspectRatio(
                    aspectRatio: kBoardCols / kBoardRows,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: GestureDetector(
                        onPanStart: _onPanStart,
                        onPanUpdate: _onPanUpdate,
                        onPanEnd: _onPanEnd,
                        onTap: _rotate,
                        child: GlassPanel(
                          padding: const EdgeInsets.all(6),
                          borderRadius: BorderRadius.circular(20),
                          glowColor: Colors.deepPurpleAccent,
                          blur: 6,
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(14),
                            child: Stack(
                              fit: StackFit.expand,
                              children: [
                                CustomPaint(
                                  painter: _BoardPainter(
                                    board: board,
                                    current: current,
                                    ghostOffset: _ghostRowOffset(),
                                  ),
                                ),
                                if (isPaused)
                                  Container(
                                    color: Colors.black.withValues(
                                      alpha: 0.6,
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
                                            color: Colors.amberAccent,
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
              const SizedBox(height: 10),
              _controlButton(
                icon: Icons.rotate_right,
                onAction: _rotate,
              ),
              const SizedBox(height: 10),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 60),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    _controlButton(
                      icon: Icons.chevron_left,
                      onAction: () => _tryMove(0, -1),
                      repeat: true,
                    ),
                    _controlButton(
                      icon: Icons.keyboard_double_arrow_down,
                      onAction: _softDrop,
                      repeat: true,
                    ),
                    _controlButton(
                      icon: Icons.chevron_right,
                      onAction: () => _tryMove(0, 1),
                      repeat: true,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
            ],
          ),
        ),
      ),
    );
  }

  Widget _controlButton({
    required IconData icon,
    required VoidCallback onAction,
    bool repeat = false,
  }) {
    return GestureDetector(
      onTap: repeat ? null : onAction,
      onTapDown: repeat ? (_) => _startRepeat(onAction) : null,
      onTapUp: repeat ? (_) => _stopRepeat() : null,
      onTapCancel: repeat ? _stopRepeat : null,
      child: GlassPanel(
        padding: const EdgeInsets.all(14),
        borderRadius: BorderRadius.circular(40),
        glowColor: Colors.amberAccent,
        blur: 8,
        child: Icon(icon, color: Colors.white, size: 26),
      ),
    );
  }

  Widget _statColumn(String label, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          label,
          style: const TextStyle(
            color: Colors.white54,
            fontSize: 10,
            fontWeight: FontWeight.bold,
            letterSpacing: 0.5,
          ),
        ),
        Text(
          value,
          style: const TextStyle(
            color: Colors.amberAccent,
            fontSize: 16,
            fontWeight: FontWeight.w900,
          ),
        ),
      ],
    );
  }

  Widget _previewPanel(String label, TetrominoType? type, {VoidCallback? onTap}) {
    return GestureDetector(
      onTap: onTap,
      child: GlassPanel(
        padding: const EdgeInsets.all(8),
        borderRadius: BorderRadius.circular(16),
        glowColor: Colors.deepPurpleAccent,
        blur: 8,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              label,
              style: const TextStyle(
                color: Colors.white54,
                fontSize: 9,
                fontWeight: FontWeight.bold,
                letterSpacing: 0.5,
              ),
            ),
            const SizedBox(height: 4),
            SizedBox(
              width: 40,
              height: 40,
              child: type == null
                  ? const SizedBox.shrink()
                  : _miniShape(type),
            ),
          ],
        ),
      ),
    );
  }

  Widget _miniShape(TetrominoType type) {
    final def = _pieceDefs[type]!;
    final n = def.matrix.length;
    const cell = 9.0;
    const cellSlot = cell + 1.0;
    return Center(
      child: SizedBox(
        width: n * cellSlot,
        height: n * cellSlot,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: List.generate(n, (r) {
            return Row(
              mainAxisSize: MainAxisSize.min,
              children: List.generate(n, (c) {
                final filled = def.matrix[r][c];
                return Container(
                  width: cell,
                  height: cell,
                  margin: const EdgeInsets.all(0.5),
                  decoration: BoxDecoration(
                    color: filled ? def.color : Colors.transparent,
                    borderRadius: BorderRadius.circular(2),
                  ),
                );
              }),
            );
          }),
        ),
      ),
    );
  }
}

class _BoardPainter extends CustomPainter {
  final List<List<Color?>> board;
  final _ActivePiece? current;
  final int ghostOffset;

  _BoardPainter({
    required this.board,
    required this.current,
    required this.ghostOffset,
  });

  void _drawCell(
    Canvas canvas,
    double cellW,
    double cellH,
    int r,
    int c,
    Color color, {
    bool ghost = false,
  }) {
    final rect = Rect.fromLTWH(
      c * cellW + 1.5,
      r * cellH + 1.5,
      cellW - 3,
      cellH - 3,
    );
    final rrect = RRect.fromRectAndRadius(rect, const Radius.circular(4));
    if (ghost) {
      canvas.drawRRect(
        rrect,
        Paint()
          ..color = color.withValues(alpha: 0.5)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2,
      );
    } else {
      canvas.drawRRect(rrect, Paint()..color = color);
      canvas.drawRRect(
        rrect,
        Paint()
          ..color = Colors.white.withValues(alpha: 0.25)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1,
      );
    }
  }

  @override
  void paint(Canvas canvas, Size size) {
    final cellW = size.width / kBoardCols;
    final cellH = size.height / kBoardRows;

    canvas.drawRect(Offset.zero & size, Paint()..color = const Color(0xFF0A0714));

    final gridPaint = Paint()
      ..color = Colors.white.withValues(alpha: 0.04)
      ..strokeWidth = 1;
    for (int c = 1; c < kBoardCols; c++) {
      canvas.drawLine(
        Offset(c * cellW, 0),
        Offset(c * cellW, size.height),
        gridPaint,
      );
    }
    for (int r = 1; r < kBoardRows; r++) {
      canvas.drawLine(
        Offset(0, r * cellH),
        Offset(size.width, r * cellH),
        gridPaint,
      );
    }

    for (int r = 0; r < kBoardRows; r++) {
      for (int c = 0; c < kBoardCols; c++) {
        final color = board[r][c];
        if (color != null) _drawCell(canvas, cellW, cellH, r, c, color);
      }
    }

    final p = current;
    if (p != null) {
      for (int r = 0; r < p.shape.length; r++) {
        for (int c = 0; c < p.shape[r].length; c++) {
          if (!p.shape[r][c]) continue;
          final gr = p.row + ghostOffset + r;
          if (gr >= 0 && gr < kBoardRows) {
            _drawCell(canvas, cellW, cellH, gr, p.col + c, p.color, ghost: true);
          }
        }
      }
      for (int r = 0; r < p.shape.length; r++) {
        for (int c = 0; c < p.shape[r].length; c++) {
          if (!p.shape[r][c]) continue;
          final br = p.row + r;
          if (br >= 0 && br < kBoardRows) {
            _drawCell(canvas, cellW, cellH, br, p.col + c, p.color);
          }
        }
      }
    }
  }

  @override
  bool shouldRepaint(covariant _BoardPainter oldDelegate) => true;
}
