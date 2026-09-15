import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';
import 'ui/result_dialog.dart';

class ImageSlidingPuzzlePage extends StatefulWidget {
  final int gridSize;
  final Uint8List imageBytes;
  const ImageSlidingPuzzlePage({
    super.key,
    required this.gridSize,
    required this.imageBytes,
  });

  @override
  State<ImageSlidingPuzzlePage> createState() =>
      _ImageSlidingPuzzlePageState();
}

class _ImageSlidingPuzzlePageState extends State<ImageSlidingPuzzlePage> {
  late List<int> tiles;
  late Uint8List imageBytes;
  int emptyIndex = 0;
  bool isReady = false;
  int moves = 0;
  Offset _dragAccum = Offset.zero;
  bool _dragTriggered = false;

  @override
  void initState() {
    super.initState();
    imageBytes = widget.imageBytes;
    _initializePuzzle();
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
      Future.delayed(const Duration(milliseconds: 200), () {
        if (!mounted) return;
        showDialog(
          context: context,
          barrierDismissible: false,
          builder: (_) => ResultDialog(
            icon: Icons.emoji_events_rounded,
            accentColor: Colors.greenAccent,
            title: 'YOU WIN!',
            stats: [ResultStat('MOVES', '$moves', highlight: true)],
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
      });
    }
  }

  Future<void> _pickNewImage() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.image,
      withData: true,
    );
    if (result == null || result.files.single.bytes == null) return;
    if (!mounted) return;
    setState(() {
      imageBytes = result.files.single.bytes!;
    });
    _initializePuzzle();
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF021B79),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: Text(
          'Photo Puzzle ${widget.gridSize}x${widget.gridSize}',
          style: const TextStyle(
            fontWeight: FontWeight.w900,
            letterSpacing: 1.5,
          ),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
        actions: [
          IconButton(
            icon: const Icon(Icons.photo_library),
            tooltip: 'Change photo',
            onPressed: _pickNewImage,
          ),
        ],
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0575E6), Color(0xFF021B79)],
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(16),
                          child: Image.memory(
                            imageBytes,
                            width: 90,
                            height: 90,
                            fit: BoxFit.cover,
                          ),
                        ),
                        const SizedBox(width: 14),
                        GlassPanel(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 20,
                            vertical: 10,
                          ),
                          borderRadius: BorderRadius.circular(20),
                          blur: 10,
                          child: Text(
                            'Moves: $moves',
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w600,
                              color: Colors.cyanAccent,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 30),
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
                      child: LayoutBuilder(
                        builder: (context, constraints) {
                          return _buildBoard(constraints.maxWidth);
                        },
                      ),
                    ),
                    const SizedBox(height: 40),
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

  Widget _buildBoard(double boardSize) {
    const spacing = 6.0;
    final n = widget.gridSize;
    final cellSize = (boardSize - spacing * (n - 1)) / n;

    return SizedBox(
      width: boardSize,
      height: boardSize,
      child: Stack(
        children: [
          for (int value = 1; value < tiles.length; value++)
            _buildAnimatedTile(value, cellSize, spacing, boardSize),
        ],
      ),
    );
  }

  Widget _buildAnimatedTile(
    int value,
    double cellSize,
    double spacing,
    double boardSize,
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
        child: _buildTileFace(value, boardSize),
      ),
    );
  }

  Widget _buildTileFace(int value, double boardSize) {
    final n = widget.gridSize;
    final originalRow = (value - 1) ~/ n;
    final originalCol = (value - 1) % n;
    final dx = n == 1 ? 0.0 : (originalCol / (n - 1)) * 2 - 1;
    final dy = n == 1 ? 0.0 : (originalRow / (n - 1)) * 2 - 1;

    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8.0),
        border: Border.all(color: Colors.white.withValues(alpha: 0.7), width: 1.5),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.4),
            blurRadius: 6,
            offset: const Offset(1, 2),
          ),
        ],
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          OverflowBox(
            maxWidth: boardSize,
            maxHeight: boardSize,
            alignment: Alignment(dx, dy),
            child: Image.memory(
              imageBytes,
              width: boardSize,
              height: boardSize,
              fit: BoxFit.cover,
              gaplessPlayback: true,
            ),
          ),
          Positioned(
            left: 3,
            top: 2,
            child: Text(
              '$value',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.bold,
                color: Colors.white.withValues(alpha: 0.85),
                shadows: const [
                  Shadow(color: Colors.black87, blurRadius: 3),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
