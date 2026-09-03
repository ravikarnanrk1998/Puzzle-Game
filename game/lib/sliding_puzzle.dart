import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';

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

  @override
  void initState() {
    super.initState();
    if (widget.isNewGame) {
      _initializePuzzle();
    } else {
      _loadGame();
    }
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
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => AlertDialog(
          backgroundColor: const Color(0xFF1B2838),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(20),
          ),
          title: const Text(
            'You Win!',
            style: TextStyle(
              color: Colors.greenAccent,
              fontWeight: FontWeight.bold,
            ),
          ),
          content: const Text(
            'Congratulations, you solved the puzzle!',
            style: TextStyle(color: Colors.white),
          ),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(context).pop();
                Navigator.of(context).pop(); // Go back to menu
              },
              child: const Text(
                'Main Menu',
                style: TextStyle(color: Colors.cyanAccent),
              ),
            ),
            TextButton(
              onPressed: () {
                Navigator.of(context).pop();
                _initializePuzzle();
              },
              child: const Text(
                'Play Again',
                style: TextStyle(color: Colors.cyanAccent),
              ),
            ),
          ],
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
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: Text(
          'Sliding Puzzle ${widget.gridSize}x${widget.gridSize}',
          style: const TextStyle(
            fontWeight: FontWeight.w900,
            letterSpacing: 1.5,
          ),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0575E6), Color(0xFF021B79)],
        orbColors: const [Colors.cyanAccent, Color(0xFF92FE9D)],
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    GlassPanel(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 20,
                        vertical: 10,
                      ),
                      borderRadius: BorderRadius.circular(20),
                      blur: 10,
                      child: Text(
                        'Sort numbers 1 to ${widget.gridSize * widget.gridSize - 1}',
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w600,
                          color: Colors.cyanAccent,
                        ),
                      ),
                    ),
                    const SizedBox(height: 50),
                    Container(
                      constraints: const BoxConstraints(
                        maxWidth: 400,
                        maxHeight: 400,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.black.withValues(alpha: 0.3),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(
                          color: Colors.white.withValues(alpha: 0.1),
                          width: 2,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.5),
                            blurRadius: 30,
                            spreadRadius: 5,
                          ),
                        ],
                      ),
                      padding: const EdgeInsets.all(12.0),
                      child: GridView.builder(
                        shrinkWrap: true,
                        physics: const NeverScrollableScrollPhysics(),
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: widget.gridSize,
                          crossAxisSpacing: 10.0,
                          mainAxisSpacing: 10.0,
                        ),
                        itemCount: tiles.length,
                        itemBuilder: (context, index) {
                          final tileValue = tiles[index];
                          final isEmpty = tileValue == 0;

                          return GestureDetector(
                            onTap: () => _onTileTap(index),
                            child: AnimatedContainer(
                              duration: const Duration(milliseconds: 250),
                              curve: Curves.easeOut,
                              decoration: BoxDecoration(
                                gradient: isEmpty
                                    ? null
                                    : const LinearGradient(
                                        colors: [
                                          Color(0xFF00C9FF),
                                          Color(0xFF92FE9D),
                                        ],
                                        begin: Alignment.topLeft,
                                        end: Alignment.bottomRight,
                                      ),
                                color: isEmpty ? Colors.black26 : null,
                                borderRadius: BorderRadius.circular(12.0),
                                border: Border.all(
                                  color: isEmpty
                                      ? Colors.transparent
                                      : Colors.white.withValues(alpha: 0.6),
                                  width: 2,
                                ),
                                boxShadow: isEmpty
                                    ? []
                                    : [
                                        BoxShadow(
                                          color: const Color(
                                            0xFF00C9FF,
                                          ).withValues(alpha: 0.5),
                                          blurRadius: 8,
                                          offset: const Offset(2, 4),
                                        ),
                                      ],
                              ),
                              child: Center(
                                child: Text(
                                  isEmpty ? '' : '$tileValue',
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
                          );
                        },
                      ),
                    ),
                    const SizedBox(height: 60),
                    ElevatedButton.icon(
                      onPressed: _initializePuzzle,
                      icon: const Icon(Icons.refresh, size: 28),
                      label: const Text(
                        'Restart Game',
                        style: TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      style: ElevatedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 40,
                          vertical: 16,
                        ),
                        backgroundColor: Colors.pinkAccent,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(30),
                        ),
                        elevation: 10,
                        shadowColor: Colors.pinkAccent.withValues(alpha: 0.5),
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
