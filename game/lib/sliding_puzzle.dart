import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ui/ambient_background.dart';
import 'ui/result_dialog.dart';

class SlidingPuzzlePage extends StatefulWidget {
  final bool isNewGame;
  final int gridSize;
  const SlidingPuzzlePage({
    super.key,
    required this.isNewGame,
    required this.gridSize,
  });

  @override
  State<SlidingPuzzlePage> createState() => _SlidingPuzzlePageState();
}

class _SlidingPuzzlePageState extends State<SlidingPuzzlePage> {
  late List<int> tiles;
  int emptyIndex = 0;
  bool isReady = false;
  int moves = 0;
  int? bestMoves;
  Offset _dragAccum = Offset.zero;
  bool _dragTriggered = false;

  String get _bestMovesKey => 'sliding_best_moves_${widget.gridSize}';

  @override
  void initState() {
    super.initState();
    _loadBestMoves();
    if (widget.isNewGame) {
      _initializePuzzle();
    } else {
      _loadGame();
    }
  }

  Future<void> _loadBestMoves() async {
    final prefs = await SharedPreferences.getInstance();
    final saved = prefs.getInt(_bestMovesKey);
    if (!mounted) return;
    setState(() => bestMoves = saved);
  }

  Future<void> _saveBestMoves() async {
    if (bestMoves != null && moves >= bestMoves!) return;
    setState(() => bestMoves = moves);
    final prefs = await SharedPreferences.getInstance();
    prefs.setInt(_bestMovesKey, moves);
  }

  Future<void> _loadGame() async {
    final prefs = await SharedPreferences.getInstance();
    String? savedStr = prefs.getString('sliding_saved_tiles');
    if (savedStr != null) {
      setState(() {
        tiles = List<int>.from(jsonDecode(savedStr));
        emptyIndex = tiles.indexOf(0);
        isReady = true;
      });
    } else {
      _initializePuzzle();
    }
  }

  Future<void> _saveGame() async {
    final prefs = await SharedPreferences.getInstance();
    prefs.setInt('sliding_grid_size', widget.gridSize);
    prefs.setString('sliding_saved_tiles', jsonEncode(tiles));
  }

  void _clearSave() async {
    final prefs = await SharedPreferences.getInstance();
    prefs.remove('sliding_saved_tiles');
  }

  void _initializePuzzle() {
    int total = widget.gridSize * widget.gridSize;
    tiles = List.generate(total, (index) => index);

    bool isSolvedBefore() {
      for (int i = 0; i < tiles.length - 1; i++) {
        if (tiles[i] != i + 1) return false;
      }
      return tiles.last == 0;
    }

    do {
      tiles.shuffle();
    } while (!_isSolvable() || isSolvedBefore());

    emptyIndex = tiles.indexOf(0);
    setState(() {
      moves = 0;
      isReady = true;
    });
    _saveGame();
  }

  bool _isSolvable() {
    int inversions = 0;
    for (int i = 0; i < tiles.length - 1; i++) {
      for (int j = i + 1; j < tiles.length; j++) {
        if (tiles[i] != 0 && tiles[j] != 0 && tiles[i] > tiles[j]) {
          inversions++;
        }
      }
    }

    if (widget.gridSize % 2 != 0) {
      return inversions % 2 == 0;
    } else {
      int emptyRowFromBottom =
          widget.gridSize - (tiles.indexOf(0) ~/ widget.gridSize);
      if (emptyRowFromBottom % 2 == 0) {
        return inversions % 2 != 0;
      } else {
        return inversions % 2 == 0;
      }
    }
  }

  void _onTileTap(int index) {
    if (_isAdjacent(index, emptyIndex)) {
      setState(() {
        int temp = tiles[index];
        tiles[index] = tiles[emptyIndex];
        tiles[emptyIndex] = temp;
        emptyIndex = index;
        moves++;
      });
      _saveGame();
      _checkWin();
    }
  }

  bool _isAdjacent(int index1, int index2) {
    int row1 = index1 ~/ widget.gridSize;
    int col1 = index1 % widget.gridSize;
    int row2 = index2 ~/ widget.gridSize;
    int col2 = index2 % widget.gridSize;

    return (row1 == row2 && (col1 - col2).abs() == 1) ||
        (col1 == col2 && (row1 - row2).abs() == 1);
  }

  void _checkWin() {
    bool win = true;
    for (int i = 0; i < tiles.length - 1; i++) {
      if (tiles[i] != i + 1) {
        win = false;
        break;
      }
    }
    if (win && tiles.last == 0) {
      _clearSave();
      _saveBestMoves();
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => ResultDialog(
          icon: Icons.emoji_events_rounded,
          accentColor: Colors.greenAccent,
          title: 'YOU WIN!',
          stats: [
            ResultStat('MOVES', '$moves', highlight: true),
            ResultStat('BEST', '${bestMoves ?? moves}'),
          ],
          primaryLabel: 'PLAY AGAIN',
          onPrimary: () {
            Navigator.of(context).pop();
            _initializePuzzle();
          },
          secondaryLabel: 'Main Menu',
          onSecondary: () {
            Navigator.of(context).pop();
            Navigator.of(context).pop();
          },
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF021B79),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    double fontSize = widget.gridSize == 3
        ? 48
        : (widget.gridSize == 4 ? 36 : 28);

    return Scaffold(
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF29B6F6), Color(0xFF0277BD)],
        child: SafeArea(
          child: SingleChildScrollView(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      _circleButton(
                        icon: Icons.arrow_back,
                        onTap: () => Navigator.of(context).pop(),
                      ),
                      _circleButton(
                        icon: Icons.refresh,
                        onTap: _initializePuzzle,
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  _titleBanner('${widget.gridSize}x${widget.gridSize}'),
                  const SizedBox(height: 24),
                  _pillBadge(
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(
                          Icons.track_changes,
                          color: Colors.white,
                          size: 18,
                        ),
                        const SizedBox(width: 8),
                        Text(
                          'Sort numbers 1 to ${widget.gridSize * widget.gridSize - 1}',
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                            color: Colors.white,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 14),
                  _pillBadge(
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'MOVES: $moves',
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w800,
                            color: Colors.white,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Container(
                          width: 1.5,
                          height: 14,
                          color: Colors.white38,
                        ),
                        const SizedBox(width: 12),
                        Text(
                          'BEST: ${bestMoves ?? '-'}',
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w800,
                            color: Colors.white,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 30),
                  Container(
                    constraints: const BoxConstraints(
                      maxWidth: 400,
                      maxHeight: 400,
                    ),
                    decoration: BoxDecoration(
                      gradient: const LinearGradient(
                        colors: [Color(0xFF0D1B4C), Color(0xFF0A1338)],
                        begin: Alignment.topLeft,
                        end: Alignment.bottomRight,
                      ),
                      borderRadius: BorderRadius.circular(26),
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.25),
                        width: 3,
                      ),
                      boxShadow: [
                        BoxShadow(
                          color: Colors.black.withValues(alpha: 0.5),
                          blurRadius: 30,
                          spreadRadius: 5,
                        ),
                      ],
                    ),
                    padding: const EdgeInsets.all(14.0),
                    child: LayoutBuilder(
                      builder: (context, constraints) {
                        return _buildBoard(constraints.maxWidth, fontSize);
                      },
                    ),
                  ),
                  const SizedBox(height: 40),
                  GestureDetector(
                    onTap: _initializePuzzle,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 40,
                        vertical: 16,
                      ),
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          colors: [Color(0xFFFF6F91), Color(0xFFE91E63)],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        borderRadius: BorderRadius.circular(30),
                        border: Border.all(
                          color: Colors.white.withValues(alpha: 0.5),
                          width: 2.5,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.35),
                            blurRadius: 10,
                            offset: const Offset(0, 5),
                          ),
                          BoxShadow(
                            color: Colors.white.withValues(alpha: 0.25),
                            blurRadius: 2,
                            offset: const Offset(-1, -1),
                          ),
                        ],
                      ),
                      child: const Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(Icons.refresh, color: Colors.white, size: 26),
                          SizedBox(width: 10),
                          Text(
                            'Restart Game',
                            style: TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.w900,
                              color: Colors.white,
                              letterSpacing: 0.5,
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
        ),
      ),
    );
  }

  Widget _circleButton({required IconData icon, required VoidCallback onTap}) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 52,
        height: 52,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: const LinearGradient(
            colors: [Color(0xFF4FC3F7), Color(0xFF1565C0)],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          border: Border.all(
            color: Colors.white.withValues(alpha: 0.55),
            width: 2,
          ),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.35),
              blurRadius: 8,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Icon(icon, color: Colors.white, size: 24),
      ),
    );
  }

  Widget _pillBadge({required Widget child}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 10),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF4FC3F7), Color(0xFF0288D1)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(30),
        border: Border.all(
          color: Colors.white.withValues(alpha: 0.5),
          width: 2,
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.3),
            blurRadius: 8,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: child,
    );
  }

  Widget _titleBanner(String subtitle) {
    return Stack(
      clipBehavior: Clip.none,
      alignment: Alignment.center,
      children: [
        Positioned(
          left: -6,
          top: 4,
          child: Transform.rotate(
            angle: -0.35,
            child: const Icon(
              Icons.extension,
              color: Color(0xFFFFC107),
              size: 34,
            ),
          ),
        ),
        Positioned(
          right: -6,
          top: -4,
          child: Transform.rotate(
            angle: 0.3,
            child: const Icon(
              Icons.extension,
              color: Color(0xFFAB47BC),
              size: 34,
            ),
          ),
        ),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 30, vertical: 14),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: [Color(0xFF7B4FE0), Color(0xFF4B2C9E)],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(28),
            border: Border.all(
              color: Colors.white.withValues(alpha: 0.5),
              width: 2.5,
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.35),
                blurRadius: 12,
                offset: const Offset(0, 6),
              ),
            ],
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'Sliding Puzzle',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 30,
                  fontWeight: FontWeight.w900,
                  color: Colors.white,
                  letterSpacing: 0.5,
                  shadows: [
                    Shadow(color: Color(0xFF2A1760), offset: Offset(2, 2)),
                    Shadow(color: Color(0xFF2A1760), offset: Offset(-2, 2)),
                    Shadow(color: Color(0xFF2A1760), offset: Offset(2, -2)),
                    Shadow(color: Color(0xFF2A1760), offset: Offset(-2, -2)),
                  ],
                ),
              ),
              Text(
                subtitle,
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w900,
                  color: Color(0xFFFFD54F),
                  letterSpacing: 1,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildBoard(double boardSize, double fontSize) {
    const spacing = 10.0;
    final n = widget.gridSize;
    final cellSize = (boardSize - spacing * (n - 1)) / n;

    return SizedBox(
      width: boardSize,
      height: boardSize,
      child: Stack(
        children: [
          for (int value = 1; value < tiles.length; value++)
            _buildAnimatedTile(value, cellSize, spacing, fontSize),
        ],
      ),
    );
  }

  Widget _buildAnimatedTile(
    int value,
    double cellSize,
    double spacing,
    double fontSize,
  ) {
    final index = tiles.indexOf(value);
    final row = index ~/ widget.gridSize;
    final col = index % widget.gridSize;

    return AnimatedPositioned(
      key: ValueKey(value),
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOutCubic,
      left: col * (cellSize + spacing),
      top: row * (cellSize + spacing),
      width: cellSize,
      height: cellSize,
      child: GestureDetector(
        onTap: () => _onTileTap(index),
        onPanStart: (_) {
          _dragAccum = Offset.zero;
          _dragTriggered = false;
        },
        onPanUpdate: (details) {
          _dragAccum += details.delta;
          if (!_dragTriggered && _dragAccum.distance > 18) {
            _dragTriggered = true;
            _onTileTap(index);
          }
        },
        child: Container(
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: [Color(0xFF4FC3F7), Color(0xFF0288D1)],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(14.0),
            border: Border.all(
              color: Colors.white.withValues(alpha: 0.55),
              width: 2,
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.4),
                blurRadius: 6,
                offset: const Offset(0, 4),
              ),
              BoxShadow(
                color: Colors.white.withValues(alpha: 0.25),
                blurRadius: 2,
                offset: const Offset(-1, -1),
              ),
            ],
          ),
          child: Center(
            child: Text(
              '$value',
              style: TextStyle(
                fontSize: fontSize,
                fontWeight: FontWeight.w900,
                color: Colors.white,
                shadows: const [
                  Shadow(
                    color: Colors.black45,
                    blurRadius: 4,
                    offset: Offset(2, 2),
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
