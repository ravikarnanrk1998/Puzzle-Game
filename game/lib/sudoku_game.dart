import 'dart:async';
import 'dart:math';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';
import 'ui/result_dialog.dart';

const Map<int, List<int>> _boxDims = {
  3: [1, 3],
  6: [2, 3],
  9: [3, 3],
};

const Map<int, int> _targetClues = {3: 5, 6: 20, 9: 32};

class _SudokuGen {
  final int size;
  final int boxRows;
  final int boxCols;
  final Random random;
  final List<List<int>> grid;

  _SudokuGen({
    required this.size,
    required this.boxRows,
    required this.boxCols,
    required this.random,
  }) : grid = List.generate(size, (_) => List.filled(size, 0));

  bool _isValid(int r, int c, int val) {
    for (int i = 0; i < size; i++) {
      if (grid[r][i] == val) return false;
      if (grid[i][c] == val) return false;
    }
    final boxRowStart = (r ~/ boxRows) * boxRows;
    final boxColStart = (c ~/ boxCols) * boxCols;
    for (int rr = boxRowStart; rr < boxRowStart + boxRows; rr++) {
      for (int cc = boxColStart; cc < boxColStart + boxCols; cc++) {
        if (grid[rr][cc] == val) return false;
      }
    }
    return true;
  }

  bool _fill(int pos) {
    if (pos == size * size) return true;
    final r = pos ~/ size;
    final c = pos % size;
    if (grid[r][c] != 0) return _fill(pos + 1);
    final values = List.generate(size, (i) => i + 1)..shuffle(random);
    for (final v in values) {
      if (_isValid(r, c, v)) {
        grid[r][c] = v;
        if (_fill(pos + 1)) return true;
        grid[r][c] = 0;
      }
    }
    return false;
  }

  List<List<int>> generateSolved() {
    _fill(0);
    return grid.map((row) => List<int>.from(row)).toList();
  }
}

int _countSolutions(
  List<List<int>> g,
  int size,
  int boxRows,
  int boxCols, {
  int limit = 2,
}) {
  int count = 0;

  bool isValid(int r, int c, int val) {
    for (int i = 0; i < size; i++) {
      if (g[r][i] == val || g[i][c] == val) return false;
    }
    final br = (r ~/ boxRows) * boxRows;
    final bc = (c ~/ boxCols) * boxCols;
    for (int rr = br; rr < br + boxRows; rr++) {
      for (int cc = bc; cc < bc + boxCols; cc++) {
        if (g[rr][cc] == val) return false;
      }
    }
    return true;
  }

  bool solve(int pos) {
    if (count >= limit) return true;
    if (pos == size * size) {
      count++;
      return count >= limit;
    }
    final r = pos ~/ size;
    final c = pos % size;
    if (g[r][c] != 0) return solve(pos + 1);
    for (int v = 1; v <= size; v++) {
      if (isValid(r, c, v)) {
        g[r][c] = v;
        if (solve(pos + 1)) return true;
        g[r][c] = 0;
      }
    }
    return false;
  }

  solve(0);
  return count;
}

List<List<int>> _createPuzzle(
  List<List<int>> solved,
  int size,
  int boxRows,
  int boxCols,
  int targetClues,
  Random random,
) {
  final puzzle = solved.map((row) => List<int>.from(row)).toList();
  final positions = [for (int i = 0; i < size * size; i++) i]
    ..shuffle(random);
  int clues = size * size;
  for (final pos in positions) {
    if (clues <= targetClues) break;
    final r = pos ~/ size;
    final c = pos % size;
    final backup = puzzle[r][c];
    puzzle[r][c] = 0;
    final testGrid = puzzle.map((row) => List<int>.from(row)).toList();
    final solCount = _countSolutions(testGrid, size, boxRows, boxCols, limit: 2);
    if (solCount == 1) {
      clues--;
    } else {
      puzzle[r][c] = backup;
    }
  }
  return puzzle;
}

class _GeneratedPuzzle {
  final List<List<int>> solution;
  final List<List<int>> puzzle;
  _GeneratedPuzzle(this.solution, this.puzzle);
}

_GeneratedPuzzle _generatePuzzle(int size) {
  final dims = _boxDims[size]!;
  final boxRows = dims[0];
  final boxCols = dims[1];
  final random = Random();
  final gen = _SudokuGen(
    size: size,
    boxRows: boxRows,
    boxCols: boxCols,
    random: random,
  );
  final solved = gen.generateSolved();
  final targetClues = _targetClues[size]!;
  final puzzle = _createPuzzle(solved, size, boxRows, boxCols, targetClues, random);
  return _GeneratedPuzzle(solved, puzzle);
}

class SudokuLevelSelectPage extends StatelessWidget {
  const SudokuLevelSelectPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Sudoku',
          style: TextStyle(fontWeight: FontWeight.w900, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0B1330), Color(0xFF1A1440)],
        child: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _levelCard(
                    context,
                    size: 3,
                    title: '3x3',
                    subtitle: 'Easy warm-up',
                    color: const Color(0xFF00E676),
                  ),
                  const SizedBox(height: 20),
                  _levelCard(
                    context,
                    size: 6,
                    title: '6x6',
                    subtitle: 'Medium challenge',
                    color: const Color(0xFF00C9FF),
                  ),
                  const SizedBox(height: 20),
                  _levelCard(
                    context,
                    size: 9,
                    title: '9x9',
                    subtitle: 'Classic Sudoku',
                    color: const Color(0xFFD500F9),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _levelCard(
    BuildContext context, {
    required int size,
    required String title,
    required String subtitle,
    required Color color,
  }) {
    return GestureDetector(
      onTap: () {
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => SudokuGamePage(size: size)),
        );
      },
      child: GlassPanel(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
        borderRadius: BorderRadius.circular(22),
        glowColor: color,
        blur: 10,
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(
                  colors: [color.withValues(alpha: 0.6), Colors.transparent],
                ),
              ),
              child: Icon(Icons.grid_on, color: color, size: 30),
            ),
            const SizedBox(width: 20),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w900,
                      color: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    subtitle,
                    style: TextStyle(
                      fontSize: 13,
                      color: Colors.white.withValues(alpha: 0.7),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ),
            ),
            Icon(Icons.arrow_forward_ios, color: color, size: 20),
          ],
        ),
      ),
    );
  }
}

class SudokuGamePage extends StatefulWidget {
  final int size;
  const SudokuGamePage({super.key, required this.size});

  @override
  State<SudokuGamePage> createState() => _SudokuGamePageState();
}

class _SudokuGamePageState extends State<SudokuGamePage> {
  late List<List<int>> solution;
  late List<List<int>> puzzle;
  late List<List<int>> board;
  int? selectedRow;
  int? selectedCol;
  int mistakes = 0;
  int elapsedSeconds = 0;
  int? bestSeconds;
  Timer? _timer;
  Timer? _revealTimer;
  int _revealCount = 0;
  bool isReady = false;

  int get boxRows => _boxDims[widget.size]![0];
  int get boxCols => _boxDims[widget.size]![1];

  String get _bestTimeKey => 'sudoku_best_time_${widget.size}';

  @override
  void initState() {
    super.initState();
    _loadBestTime();
    _generate();
  }

  Future<void> _loadBestTime() async {
    final prefs = await SharedPreferences.getInstance();
    final saved = prefs.getInt(_bestTimeKey);
    if (!mounted) return;
    setState(() => bestSeconds = saved);
  }

  Future<void> _saveBestTime() async {
    if (bestSeconds != null && elapsedSeconds >= bestSeconds!) return;
    setState(() => bestSeconds = elapsedSeconds);
    final prefs = await SharedPreferences.getInstance();
    prefs.setInt(_bestTimeKey, elapsedSeconds);
  }

  String _formatTime(int seconds) {
    final minutes = seconds ~/ 60;
    final secs = seconds % 60;
    return '$minutes:${secs.toString().padLeft(2, '0')}';
  }

  @override
  void dispose() {
    _timer?.cancel();
    _revealTimer?.cancel();
    super.dispose();
  }

  Future<void> _generate() async {
    setState(() => isReady = false);
    final generated = await compute(_generatePuzzle, widget.size);
    solution = generated.solution;
    puzzle = generated.puzzle;
    board = puzzle.map((row) => List<int>.from(row)).toList();
    mistakes = 0;
    elapsedSeconds = 0;
    selectedRow = null;
    selectedCol = null;
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() => elapsedSeconds++);
    });
    if (!mounted) return;
    setState(() => isReady = true);
    _startRevealAnimation();
  }

  void _startRevealAnimation() {
    _revealTimer?.cancel();
    _revealCount = 0;
    final total = widget.size * widget.size;
    final intervalMs = (600 / total).clamp(4, 30).round();
    _revealTimer = Timer.periodic(Duration(milliseconds: intervalMs), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() => _revealCount++);
      if (_revealCount >= total) {
        timer.cancel();
      }
    });
  }

  bool _isClue(int r, int c) => puzzle[r][c] != 0;

  bool _hasConflict(int r, int c) {
    final val = board[r][c];
    if (val == 0) return false;
    for (int i = 0; i < widget.size; i++) {
      if (i != c && board[r][i] == val) return true;
      if (i != r && board[i][c] == val) return true;
    }
    final br = (r ~/ boxRows) * boxRows;
    final bc = (c ~/ boxCols) * boxCols;
    for (int rr = br; rr < br + boxRows; rr++) {
      for (int cc = bc; cc < bc + boxCols; cc++) {
        if ((rr != r || cc != c) && board[rr][cc] == val) return true;
      }
    }
    return false;
  }

  void _selectCell(int r, int c) {
    setState(() {
      selectedRow = r;
      selectedCol = c;
    });
  }

  void _enterNumber(int val) {
    if (selectedRow == null || selectedCol == null) return;
    final r = selectedRow!;
    final c = selectedCol!;
    if (_isClue(r, c)) return;
    setState(() {
      board[r][c] = val;
      if (val != solution[r][c]) {
        mistakes++;
      }
    });
    _checkWin();
  }

  void _clearCell() {
    if (selectedRow == null || selectedCol == null) return;
    final r = selectedRow!;
    final c = selectedCol!;
    if (_isClue(r, c)) return;
    setState(() => board[r][c] = 0);
  }

  void _checkWin() {
    for (int r = 0; r < widget.size; r++) {
      for (int c = 0; c < widget.size; c++) {
        if (board[r][c] != solution[r][c]) return;
      }
    }
    _timer?.cancel();
    final timeStr = _formatTime(elapsedSeconds);
    _saveBestTime();
    Future.delayed(const Duration(milliseconds: 150), () {
      if (!mounted) return;
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => ResultDialog(
          icon: Icons.emoji_events_rounded,
          accentColor: Colors.cyanAccent,
          title: 'SOLVED!',
          stats: [
            ResultStat('TIME', timeStr, highlight: true),
            ResultStat('BEST', _formatTime(bestSeconds ?? elapsedSeconds)),
            ResultStat('MISTAKES', '$mistakes'),
          ],
          primaryLabel: 'NEW PUZZLE',
          onPrimary: () {
            Navigator.of(context).pop();
            _generate();
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

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF0B1330),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: Text(
          'Sudoku ${widget.size}x${widget.size}',
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
            icon: const Icon(Icons.refresh),
            tooltip: 'New puzzle',
            onPressed: _generate,
          ),
        ],
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0B1330), Color(0xFF1A1440)],
        child: SafeArea(
          child: SingleChildScrollView(
            child: Padding(
              padding: const EdgeInsets.all(20.0),
              child: Column(
                children: [
                  GlassPanel(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 20,
                      vertical: 10,
                    ),
                    borderRadius: BorderRadius.circular(18),
                    glowColor: Colors.cyanAccent,
                    blur: 8,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        _statColumn('TIME', _formatTime(elapsedSeconds)),
                        _statColumn(
                          'BEST',
                          bestSeconds == null ? '-' : _formatTime(bestSeconds!),
                        ),
                        _statColumn('MISTAKES', '$mistakes'),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                  Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 420),
                      child: AspectRatio(
                        aspectRatio: 1,
                        child: GlassPanel(
                          padding: const EdgeInsets.all(6),
                          borderRadius: BorderRadius.circular(16),
                          glowColor: Colors.deepPurpleAccent,
                          blur: 8,
                          child: _buildGrid(),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  _buildNumberPad(),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _statColumn(String label, String value) {
    return Column(
      children: [
        Text(
          label,
          style: const TextStyle(
            color: Colors.white54,
            fontSize: 11,
            fontWeight: FontWeight.bold,
            letterSpacing: 1,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: const TextStyle(
            color: Colors.cyanAccent,
            fontSize: 20,
            fontWeight: FontWeight.w900,
          ),
        ),
      ],
    );
  }

  Widget _buildGrid() {
    final n = widget.size;
    return GridView.builder(
      padding: EdgeInsets.zero,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(crossAxisCount: n),
      itemCount: n * n,
      itemBuilder: (context, index) {
        final r = index ~/ n;
        final c = index % n;
        final value = board[r][c];
        final isClue = _isClue(r, c);
        final isSelected = selectedRow == r && selectedCol == c;
        final isPeer =
            selectedRow != null &&
            selectedCol != null &&
            !isSelected &&
            (r == selectedRow ||
                c == selectedCol ||
                ((r ~/ boxRows) == (selectedRow! ~/ boxRows) &&
                    (c ~/ boxCols) == (selectedCol! ~/ boxCols)));
        final conflict = _hasConflict(r, c);

        final isRevealed = index < _revealCount;

        return GestureDetector(
          onTap: () => _selectCell(r, c),
          child: AnimatedOpacity(
            opacity: isRevealed ? 1.0 : 0.0,
            duration: const Duration(milliseconds: 180),
            curve: Curves.easeOut,
            child: AnimatedScale(
              scale: isRevealed ? 1.0 : 0.6,
              duration: const Duration(milliseconds: 180),
              curve: Curves.easeOut,
              child: Container(
                decoration: BoxDecoration(
                  color: isSelected
                      ? Colors.cyanAccent.withValues(alpha: 0.35)
                      : (isPeer
                            ? Colors.white.withValues(alpha: 0.06)
                            : Colors.white.withValues(alpha: 0.02)),
                  border: Border(
                    right: BorderSide(
                      color: Colors.white.withValues(
                        alpha: (c + 1) % boxCols == 0 ? 0.5 : 0.12,
                      ),
                      width: (c + 1) % boxCols == 0 ? 1.6 : 0.6,
                    ),
                    bottom: BorderSide(
                      color: Colors.white.withValues(
                        alpha: (r + 1) % boxRows == 0 ? 0.5 : 0.12,
                      ),
                      width: (r + 1) % boxRows == 0 ? 1.6 : 0.6,
                    ),
                    left: BorderSide(
                      color: Colors.white.withValues(
                        alpha: c == 0 ? 0.5 : 0.0,
                      ),
                      width: c == 0 ? 1.6 : 0,
                    ),
                    top: BorderSide(
                      color: Colors.white.withValues(
                        alpha: r == 0 ? 0.5 : 0.0,
                      ),
                      width: r == 0 ? 1.6 : 0,
                    ),
                  ),
                ),
                alignment: Alignment.center,
                child: value == 0
                    ? null
                    : Text(
                        '$value',
                        style: TextStyle(
                          fontSize: n == 9 ? 18 : (n == 6 ? 22 : 28),
                          fontWeight: isClue
                              ? FontWeight.w900
                              : FontWeight.w600,
                          color: conflict
                              ? Colors.redAccent
                              : (isClue ? Colors.white : Colors.cyanAccent),
                        ),
                      ),
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildNumberPad() {
    return Wrap(
      alignment: WrapAlignment.center,
      spacing: 10,
      runSpacing: 10,
      children: [
        for (int v = 1; v <= widget.size; v++)
          GestureDetector(
            onTap: () => _enterNumber(v),
            child: GlassPanel(
              padding: const EdgeInsets.all(14),
              borderRadius: BorderRadius.circular(12),
              glowColor: Colors.cyanAccent,
              blur: 6,
              child: SizedBox(
                width: 20,
                height: 20,
                child: Center(
                  child: Text(
                    '$v',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
              ),
            ),
          ),
        GestureDetector(
          onTap: _clearCell,
          child: GlassPanel(
            padding: const EdgeInsets.all(14),
            borderRadius: BorderRadius.circular(12),
            glowColor: Colors.pinkAccent,
            blur: 6,
            child: const SizedBox(
              width: 20,
              height: 20,
              child: Icon(Icons.backspace_outlined, color: Colors.pinkAccent, size: 20),
            ),
          ),
        ),
      ],
    );
  }
}
