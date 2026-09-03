import 'dart:math';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:audioplayers/audioplayers.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';
import 'ui/result_dialog.dart';

class ShapeDef {
  final List<List<bool>> matrix;
  final Color color;
  ShapeDef(this.matrix, this.color);
}

final List<ShapeDef> _allShapes = [
  ShapeDef([
    [true],
  ], const Color(0xFFFFD700)),
  ShapeDef([
    [true, true],
  ], const Color(0xFF00E5FF)),
  ShapeDef([
    [true],
    [true],
  ], const Color(0xFF00E5FF)),
  ShapeDef([
    [true, true, true],
  ], const Color(0xFF00E676)),
  ShapeDef([
    [true],
    [true],
    [true],
  ], const Color(0xFF00E676)),
  ShapeDef([
    [true, true],
    [true, true],
  ], const Color(0xFFFF1744)),
  ShapeDef([
    [true, false],
    [true, true],
  ], const Color(0xFFFF9100)),
  ShapeDef([
    [true, true],
    [false, true],
  ], const Color(0xFFD500F9)),
  ShapeDef([
    [true, true, true],
    [false, true, false],
  ], const Color(0xFFF50057)),
];

class BlockPuzzlePage extends StatefulWidget {
  final bool isNewGame;
  const BlockPuzzlePage({super.key, required this.isNewGame});

  @override
  State<BlockPuzzlePage> createState() => _BlockPuzzlePageState();
}

class _BlockPuzzlePageState extends State<BlockPuzzlePage> {
  static const int boardSize = 8;
  late List<List<Color?>> board;
  late List<List<bool>> isClearing;
  List<ShapeDef?> availableShapes = [];

  int score = 0;
  int highScore = 0;
  int hammers = 3;
  bool isEraserSelected = false;
  bool _isAnimating = false;
  bool isReady = false;

  final Random _random = Random();
  int? draggingShapeIndex;
  int? hoverRow;
  int? hoverCol;
  bool _showPerfectClear = false;
  int? _scorePopupValue;
  int _scorePopupToken = 0;
  final AudioPlayer _audioPlayer = AudioPlayer();
  final AudioPlayer _lineClearPlayer = AudioPlayer();

  @override
  void initState() {
    super.initState();
    isClearing = List.generate(
      boardSize,
      (_) => List.generate(boardSize, (_) => false),
    );
    if (widget.isNewGame) {
      _loadHighScoreOnly().then((_) => _initGame());
    } else {
      _loadGame();
    }
  }

  @override
  void dispose() {
    _audioPlayer.dispose();
    _lineClearPlayer.dispose();
    super.dispose();
  }

  Future<void> _loadHighScoreOnly() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      highScore = prefs.getInt('block_high_score') ?? 0;
    });
  }

  Future<void> _loadGame() async {
    final prefs = await SharedPreferences.getInstance();
    highScore = prefs.getInt('block_high_score') ?? 0;

    String? boardStr = prefs.getString('block_saved_board');
    if (boardStr != null) {
      List<dynamic> bRaw = jsonDecode(boardStr);
      board = bRaw
          .map<List<Color?>>(
            (row) => (row as List)
                .map<Color?>((val) => val == 0 ? null : Color(val as int))
                .toList(),
          )
          .toList();

      String shapesStr = prefs.getString('block_saved_shapes') ?? '[]';
      List<dynamic> sRaw = jsonDecode(shapesStr);
      availableShapes = sRaw
          .map<ShapeDef?>((idx) => (idx as int) == -1 ? null : _allShapes[idx])
          .toList();

      score = prefs.getInt('block_saved_score') ?? 0;
      hammers = prefs.getInt('block_saved_hammers') ?? 3;
      setState(() {
        isReady = true;
      });
    } else {
      _initGame();
    }
  }

  Future<void> _saveGame() async {
    final prefs = await SharedPreferences.getInstance();
    List<List<int>> boardInts = board
        .map((row) => row.map((c) => c?.toARGB32() ?? 0).toList())
        .toList();
    prefs.setString('block_saved_board', jsonEncode(boardInts));

    List<int> shapesInts = availableShapes
        .map((s) => s == null ? -1 : _allShapes.indexOf(s))
        .toList();
    prefs.setString('block_saved_shapes', jsonEncode(shapesInts));

    prefs.setInt('block_saved_score', score);
    prefs.setInt('block_saved_hammers', hammers);

    if (score > highScore) {
      highScore = score;
      prefs.setInt('block_high_score', highScore);
    }
  }

  void _clearSave() async {
    final prefs = await SharedPreferences.getInstance();
    prefs.remove('block_saved_board');
  }

  void _initGame() {
    board = List.generate(
      boardSize,
      (_) => List.generate(boardSize, (_) => null),
    );
    isClearing = List.generate(
      boardSize,
      (_) => List.generate(boardSize, (_) => false),
    );
    score = 0;
    hammers = 3;
    isEraserSelected = false;
    _isAnimating = false;
    _generateShapes();
    setState(() {
      isReady = true;
    });
  }

  void _generateShapes() {
    availableShapes = List.generate(
      3,
      (_) => _allShapes[_random.nextInt(_allShapes.length)],
    );
    setState(() {});
    _saveGame();
    _checkGameOver();
  }

  bool _canPlaceShape(ShapeDef shapeDef, int startRow, int startCol) {
    final shape = shapeDef.matrix;
    for (int r = 0; r < shape.length; r++) {
      for (int c = 0; c < shape[r].length; c++) {
        if (shape[r][c]) {
          int boardRow = startRow + r;
          int boardCol = startCol + c;
          if (boardRow >= boardSize || boardCol >= boardSize) return false;
          if (boardRow < 0 || boardCol < 0) return false;
          if (board[boardRow][boardCol] != null) return false;
        }
      }
    }
    return true;
  }

  void _placeShape(int shapeIndex, int startRow, int startCol) async {
    if (_isAnimating) return;
    final shapeDef = availableShapes[shapeIndex];
    if (shapeDef == null || !_canPlaceShape(shapeDef, startRow, startCol)) {
      return;
    }

    setState(() {
      final shape = shapeDef.matrix;
      for (int r = 0; r < shape.length; r++) {
        for (int c = 0; c < shape[r].length; c++) {
          if (shape[r][c]) {
            board[startRow + r][startCol + c] = shapeDef.color;
            score += 10;
          }
        }
      }
      availableShapes[shapeIndex] = null;
      if (availableShapes.every((s) => s == null)) {
        _generateShapes();
      }
      draggingShapeIndex = null;
      hoverRow = null;
      hoverCol = null;
    });

    _saveGame();
    await _clearLines();
    if (mounted && !_isAnimating) {
      _checkGameOver();
    }
  }

  Future<void> _clearLines() async {
    List<int> rowsToClear = [];
    List<int> colsToClear = [];

    for (int r = 0; r < boardSize; r++) {
      if (board[r].every((cell) => cell != null)) rowsToClear.add(r);
    }
    for (int c = 0; c < boardSize; c++) {
      bool colFull = true;
      for (int r = 0; r < boardSize; r++) {
        if (board[r][c] == null) {
          colFull = false;
          break;
        }
      }
      if (colFull) colsToClear.add(c);
    }

    if (rowsToClear.isNotEmpty || colsToClear.isNotEmpty) {
      _isAnimating = true;
      _lineClearPlayer
          .play(AssetSource('sounds/line_clear.mp3'))
          .catchError((_) {});

      setState(() {
        for (int r in rowsToClear) {
          for (int c = 0; c < boardSize; c++) {
            isClearing[r][c] = true;
          }
        }
        for (int c in colsToClear) {
          for (int r = 0; r < boardSize; r++) {
            isClearing[r][c] = true;
          }
        }
      });

      await Future.delayed(const Duration(milliseconds: 350));
      if (!mounted) return;

      bool isPerfectClear = false;
      int gained = 0;
      setState(() {
        for (int r in rowsToClear) {
          for (int c = 0; c < boardSize; c++) {
            board[r][c] = null;
            isClearing[r][c] = false;
          }
        }
        for (int c in colsToClear) {
          for (int r = 0; r < boardSize; r++) {
            board[r][c] = null;
            isClearing[r][c] = false;
          }
        }
        gained = (rowsToClear.length + colsToClear.length) * 100;
        int intersect = rowsToClear.length * colsToClear.length;
        gained -= intersect * 10;

        isPerfectClear = board.every(
          (row) => row.every((cell) => cell == null),
        );
        if (isPerfectClear) {
          gained *= 2;
        }
        score += gained;
        _isAnimating = false;
      });
      _saveGame();

      if (isPerfectClear) {
        _celebratePerfectClear();
      } else {
        _showScoreGain(gained);
      }
    }
  }

  void _showScoreGain(int gained) {
    _scorePopupToken++;
    final token = _scorePopupToken;
    setState(() {
      _scorePopupValue = gained;
    });
    Future.delayed(const Duration(milliseconds: 900), () {
      if (!mounted || _scorePopupToken != token) return;
      setState(() {
        _scorePopupValue = null;
      });
    });
  }

  void _celebratePerfectClear() {
    _audioPlayer
        .play(AssetSource('sounds/perfect_clear.mp3'))
        .catchError((_) {});
    setState(() {
      _showPerfectClear = true;
    });
    Future.delayed(const Duration(milliseconds: 1400), () {
      if (!mounted) return;
      setState(() {
        _showPerfectClear = false;
      });
    });
  }

  void _useEraser(int r, int c) {
    if (_isAnimating) return;
    if (hammers > 0 && board[r][c] != null) {
      setState(() {
        board[r][c] = null;
        hammers--;
        isEraserSelected = false;
      });
      _saveGame();
      _checkGameOver();
    }
  }

  void _checkGameOver() {
    bool hasMove = false;
    for (var shapeDef in availableShapes) {
      if (shapeDef == null) continue;
      for (int r = 0; r < boardSize; r++) {
        for (int c = 0; c < boardSize; c++) {
          if (_canPlaceShape(shapeDef, r, c)) {
            hasMove = true;
            break;
          }
        }
        if (hasMove) break;
      }
      if (hasMove) break;
    }

    if (!hasMove && availableShapes.any((s) => s != null)) {
      _clearSave();
      Future.delayed(const Duration(milliseconds: 100), () {
        if (!mounted) return;
        showDialog(
          context: context,
          barrierDismissible: false,
          builder: (_) => ResultDialog(
            icon: Icons.sentiment_dissatisfied_rounded,
            accentColor: Colors.pinkAccent,
            title: 'GAME OVER',
            stats: [
              ResultStat('SCORE', '$score', highlight: true),
              ResultStat('BEST', '$highScore'),
            ],
            primaryLabel: 'PLAY AGAIN',
            onPrimary: () {
              Navigator.of(context).pop();
              _initGame();
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

  Widget _buildShapeGrid(ShapeDef shapeDef, {double cellSize = 18.0}) {
    final shape = shapeDef.matrix;
    int rows = shape.length;
    int cols = shape[0].length;
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(rows, (r) {
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: List.generate(cols, (c) {
            return Container(
              width: cellSize,
              height: cellSize,
              margin: const EdgeInsets.all(1.5),
              decoration: BoxDecoration(
                color: shape[r][c] ? shapeDef.color : Colors.transparent,
                borderRadius: BorderRadius.circular(4),
                boxShadow: shape[r][c]
                    ? [
                        BoxShadow(
                          color: shapeDef.color.withValues(alpha: 0.6),
                          blurRadius: 4,
                        ),
                      ]
                    : [],
              ),
            );
          }),
        );
      }),
    );
  }

  Widget _buildShapeCard(ShapeDef? shapeDef, int index) {
    if (shapeDef == null) return const SizedBox(width: 90, height: 90);

    bool isDragging = draggingShapeIndex == index;

    Widget card = AnimatedContainer(
      duration: const Duration(milliseconds: 200),
      padding: const EdgeInsets.all(8),
      alignment: Alignment.center,
      constraints: const BoxConstraints(minWidth: 90, minHeight: 90),
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.4),
        border: Border.all(color: Colors.white12, width: 1),
        borderRadius: BorderRadius.circular(16),
      ),
      child: _buildShapeGrid(shapeDef),
    );

    if (_isAnimating) return card;

    return Draggable<int>(
      data: index,
      dragAnchorStrategy: pointerDragAnchorStrategy,
      feedback: Opacity(
        opacity: 0.85,
        child: Material(
          color: Colors.transparent,
          child: _buildShapeGrid(shapeDef),
        ),
      ),
      childWhenDragging: Opacity(opacity: 0.3, child: card),
      onDragStarted: () {
        setState(() {
          draggingShapeIndex = index;
          isEraserSelected = false;
        });
      },
      onDragEnd: (_) {
        setState(() {
          draggingShapeIndex = null;
          hoverRow = null;
          hoverCol = null;
        });
      },
      onDraggableCanceled: (_, _) {
        setState(() {
          draggingShapeIndex = null;
          hoverRow = null;
          hoverCol = null;
        });
      },
      child: AnimatedScale(
        duration: const Duration(milliseconds: 150),
        scale: isDragging ? 1.05 : 1.0,
        child: card,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF140D36),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Block Puzzle',
          style: TextStyle(fontWeight: FontWeight.bold, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: Stack(
        children: [
          AmbientGlowBackground(
            gradientColors: const [Color(0xFF140D36), Color(0xFF2A0845)],
            orbColors: const [Colors.cyanAccent, Colors.pinkAccent],
            child: SafeArea(
              child: Column(
                children: [
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 24.0,
                  vertical: 10.0,
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    GlassPanel(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 18,
                        vertical: 10,
                      ),
                      borderRadius: BorderRadius.circular(18),
                      blur: 10,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text(
                            'HIGH SCORE',
                            style: TextStyle(
                              color: Colors.white54,
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              letterSpacing: 1,
                            ),
                          ),
                          Text(
                            '$highScore',
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w900,
                              color: Colors.amberAccent,
                            ),
                          ),
                          const SizedBox(height: 5),
                          const Text(
                            'SCORE',
                            style: TextStyle(
                              color: Colors.white54,
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              letterSpacing: 1,
                            ),
                          ),
                          Text(
                            '$score',
                            style: const TextStyle(
                              fontSize: 34,
                              fontWeight: FontWeight.w900,
                              color: Colors.cyanAccent,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Column(
                      children: [
                        GestureDetector(
                          onTap: () {
                            if (hammers > 0 && !_isAnimating) {
                              setState(() {
                                isEraserSelected = !isEraserSelected;
                              });
                            }
                          },
                          child: AnimatedContainer(
                            duration: const Duration(milliseconds: 200),
                            padding: const EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 8,
                            ),
                            decoration: BoxDecoration(
                              color: isEraserSelected
                                  ? Colors.cyanAccent
                                  : Colors.black45,
                              borderRadius: BorderRadius.circular(20),
                              border: Border.all(
                                color: isEraserSelected
                                    ? Colors.white
                                    : Colors.cyanAccent,
                                width: 2,
                              ),
                              boxShadow: isEraserSelected
                                  ? [
                                      BoxShadow(
                                        color: Colors.cyanAccent.withValues(
                                          alpha: 0.5,
                                        ),
                                        blurRadius: 10,
                                        spreadRadius: 2,
                                      ),
                                    ]
                                  : [],
                            ),
                            child: Row(
                              children: [
                                Icon(
                                  Icons.build,
                                  color: isEraserSelected
                                      ? Colors.black
                                      : (hammers > 0
                                            ? Colors.cyanAccent
                                            : Colors.grey),
                                  size: 20,
                                ),
                                const SizedBox(width: 8),
                                Text(
                                  '$hammers',
                                  style: TextStyle(
                                    color: isEraserSelected
                                        ? Colors.black
                                        : (hammers > 0
                                              ? Colors.white
                                              : Colors.grey),
                                    fontWeight: FontWeight.bold,
                                    fontSize: 18,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 15),
                        Container(
                          decoration: BoxDecoration(
                            color: Colors.pinkAccent.withValues(alpha: 0.2),
                            shape: BoxShape.circle,
                          ),
                          child: IconButton(
                            icon: const Icon(Icons.refresh, size: 26),
                            color: Colors.pinkAccent,
                            onPressed: () {
                              if (!_isAnimating) _initGame();
                            },
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 5),
              Text(
                isEraserSelected
                    ? '⚡ Tap a placed block to shatter it! ⚡'
                    : 'Drag a shape onto the grid to place it.',
                style: TextStyle(
                  color: isEraserSelected ? Colors.amberAccent : Colors.white70,
                  fontWeight: FontWeight.bold,
                  fontSize: 14,
                  shadows: isEraserSelected
                      ? [
                          Shadow(
                            color: Colors.amberAccent.withValues(alpha: 0.5),
                            blurRadius: 5,
                          ),
                        ]
                      : [],
                ),
              ),
              const SizedBox(height: 10),
              Expanded(
                child: Center(
                  child: AspectRatio(
                    aspectRatio: 1,
                    child: Container(
                      margin: const EdgeInsets.symmetric(horizontal: 20),
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.black.withValues(alpha: 0.4),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(
                          color: isEraserSelected
                              ? Colors.amberAccent
                              : Colors.cyanAccent.withValues(alpha: 0.3),
                          width: isEraserSelected ? 3 : 2,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color:
                                (isEraserSelected
                                        ? Colors.amberAccent
                                        : Colors.black)
                                    .withValues(alpha: 0.5),
                            blurRadius: 20,
                            spreadRadius: 5,
                          ),
                        ],
                      ),
                      child: GridView.builder(
                        physics: const NeverScrollableScrollPhysics(),
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                              crossAxisCount: boardSize,
                            ),
                        itemCount: boardSize * boardSize,
                        itemBuilder: (context, index) {
                          int r = index ~/ boardSize;
                          int c = index % boardSize;
                          Color? cellColor = board[r][c];
                          bool clearing = isClearing[r][c];
                          bool canErase =
                              isEraserSelected &&
                              cellColor != null &&
                              !clearing;

                          bool isPreviewCell = false;
                          Color? previewColor;
                          if (draggingShapeIndex != null &&
                              hoverRow != null &&
                              hoverCol != null &&
                              availableShapes[draggingShapeIndex!] != null) {
                            final shapeDef =
                                availableShapes[draggingShapeIndex!]!;
                            final dr = r - hoverRow!;
                            final dc = c - hoverCol!;
                            if (dr >= 0 &&
                                dr < shapeDef.matrix.length &&
                                dc >= 0 &&
                                dc < shapeDef.matrix[dr].length &&
                                shapeDef.matrix[dr][dc]) {
                              isPreviewCell = true;
                              bool valid = _canPlaceShape(
                                shapeDef,
                                hoverRow!,
                                hoverCol!,
                              );
                              previewColor = valid
                                  ? shapeDef.color
                                  : Colors.redAccent;
                            }
                          }

                          return DragTarget<int>(
                            onWillAcceptWithDetails: (_) => !_isAnimating,
                            onMove: (details) {
                              if (hoverRow != r ||
                                  hoverCol != c ||
                                  draggingShapeIndex != details.data) {
                                setState(() {
                                  draggingShapeIndex = details.data;
                                  hoverRow = r;
                                  hoverCol = c;
                                });
                              }
                            },
                            onLeave: (_) {
                              if (hoverRow == r && hoverCol == c) {
                                setState(() {
                                  hoverRow = null;
                                  hoverCol = null;
                                });
                              }
                            },
                            onAcceptWithDetails: (details) {
                              _placeShape(details.data, r, c);
                            },
                            builder: (context, candidateData, rejectedData) {
                              return GestureDetector(
                                onTap: () {
                                  if (isEraserSelected) {
                                    _useEraser(r, c);
                                  }
                                },
                                child: AnimatedContainer(
                                  duration: clearing
                                      ? const Duration(milliseconds: 320)
                                      : const Duration(milliseconds: 150),
                                  curve: clearing
                                      ? Curves.easeIn
                                      : Curves.easeOut,
                                  margin: const EdgeInsets.all(2),
                                  transform: clearing
                                      ? (Matrix4.identity()
                                          ..rotateZ(0.7)
                                          ..scaleByDouble(0.1, 0.1, 0.1, 1.0))
                                      : Matrix4.identity(),
                                  transformAlignment: Alignment.center,
                                  decoration: BoxDecoration(
                                    color: clearing
                                        ? Colors.white
                                        : (isPreviewCell
                                              ? previewColor!.withValues(
                                                  alpha: 0.55,
                                                )
                                              : (cellColor ??
                                                    Colors.white.withValues(
                                                      alpha: 0.05,
                                                    ))),
                                    borderRadius: BorderRadius.circular(
                                      clearing ? 24 : 6,
                                    ),
                                    border: Border.all(
                                      color: clearing
                                          ? Colors.white
                                          : (isPreviewCell
                                                ? previewColor!
                                                : (canErase
                                                      ? Colors.amberAccent
                                                      : (cellColor != null
                                                            ? Colors.white24
                                                            : Colors
                                                                  .transparent))),
                                      width: clearing
                                          ? 0
                                          : (isPreviewCell
                                                ? 2
                                                : (canErase ? 2 : 1)),
                                    ),
                                    boxShadow: clearing
                                        ? [
                                            BoxShadow(
                                              color: Colors.white,
                                              blurRadius: 24,
                                              spreadRadius: 6,
                                            ),
                                            if (cellColor != null)
                                              BoxShadow(
                                                color: cellColor.withValues(
                                                  alpha: 0.9,
                                                ),
                                                blurRadius: 34,
                                                spreadRadius: 10,
                                              ),
                                          ]
                                        : (isPreviewCell
                                              ? [
                                                  BoxShadow(
                                                    color: previewColor!
                                                        .withValues(
                                                          alpha: 0.6,
                                                        ),
                                                    blurRadius: 10,
                                                  ),
                                                ]
                                              : (cellColor != null
                                                    ? [
                                                        BoxShadow(
                                                          color: canErase
                                                              ? Colors
                                                                    .amberAccent
                                                              : cellColor
                                                                    .withValues(
                                                                      alpha:
                                                                          0.5,
                                                                    ),
                                                          blurRadius: canErase
                                                              ? 12
                                                              : 8,
                                                          offset:
                                                              const Offset(
                                                                1,
                                                                1,
                                                              ),
                                                        ),
                                                      ]
                                                    : [])),
                                  ),
                                  child: canErase
                                      ? const Icon(
                                          Icons.close,
                                          color: Colors.amberAccent,
                                          size: 16,
                                        )
                                      : null,
                                ),
                              );
                            },
                          );
                        },
                      ),
                    ),
                  ),
                ),
              ),
              Container(
                height: 140,
                padding: const EdgeInsets.symmetric(vertical: 10),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: List.generate(3, (index) {
                    return _buildShapeCard(availableShapes[index], index);
                  }),
                ),
              ),
              const SizedBox(height: 15),
                ],
              ),
            ),
          ),
          if (_scorePopupValue != null) _buildScorePopup(),
          if (_showPerfectClear) _buildPerfectClearOverlay(),
        ],
      ),
    );
  }

  Widget _buildScorePopup() {
    return Positioned.fill(
      key: ValueKey(_scorePopupToken),
      child: IgnorePointer(
        child: Center(
          child: TweenAnimationBuilder<double>(
            tween: Tween(begin: 0.0, end: 1.0),
            duration: const Duration(milliseconds: 900),
            curve: Curves.easeOut,
            builder: (context, t, child) {
              return Opacity(
                opacity: (1 - t).clamp(0.0, 1.0),
                child: Transform.translate(
                  offset: Offset(0, -60 * t),
                  child: child,
                ),
              );
            },
            child: Text(
              '+$_scorePopupValue',
              style: const TextStyle(
                color: Colors.amberAccent,
                fontSize: 42,
                fontWeight: FontWeight.w900,
                letterSpacing: 1,
                shadows: [
                  Shadow(
                    color: Colors.black87,
                    blurRadius: 12,
                    offset: Offset(0, 3),
                  ),
                  Shadow(color: Colors.orangeAccent, blurRadius: 24),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildPerfectClearOverlay() {
    return Positioned.fill(
      child: IgnorePointer(
        child: Container(
          color: Colors.black.withValues(alpha: 0.35),
          alignment: Alignment.center,
          child: TweenAnimationBuilder<double>(
            tween: Tween(begin: 0.5, end: 1.0),
            duration: const Duration(milliseconds: 400),
            curve: Curves.elasticOut,
            builder: (context, scale, child) {
              return Transform.scale(scale: scale, child: child);
            },
            child: Container(
              padding: const EdgeInsets.symmetric(
                horizontal: 36,
                vertical: 24,
              ),
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  colors: [Color(0xFFFFD700), Color(0xFFFF512F)],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(24),
                boxShadow: [
                  BoxShadow(
                    color: const Color(0xFFFFD700).withValues(alpha: 0.6),
                    blurRadius: 30,
                    spreadRadius: 5,
                  ),
                ],
              ),
              child: const Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'WOW! PERFECT CLEAR',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 26,
                      fontWeight: FontWeight.w900,
                      letterSpacing: 1.5,
                      shadows: [
                        Shadow(
                          color: Colors.black45,
                          blurRadius: 10,
                          offset: Offset(0, 4),
                        ),
                      ],
                    ),
                  ),
                  SizedBox(height: 8),
                  Text(
                    'SCORE x2 BONUS!',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                      letterSpacing: 1,
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
