import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'sliding_puzzle.dart';
import 'image_sliding_puzzle.dart';
import 'block_puzzle.dart';
import 'snake_game.dart';
import 'classic_block_puzzle.dart';
import 'sudoku_game.dart';
import 'jigsaw_puzzle.dart';
import 'ui/ambient_background.dart';

void main() {
  runApp(const PuzzleApp());
}

class PuzzleApp extends StatelessWidget {
  const PuzzleApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Puzzle Hub',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        fontFamily: 'Montserrat',
        colorScheme: ColorScheme.fromSeed(
          seedColor: Colors.indigo,
          brightness: Brightness.dark,
        ),
        useMaterial3: true,
      ),
      home: const MainMenuPage(),
    );
  }
}

class MainMenuPage extends StatefulWidget {
  const MainMenuPage({super.key});

  @override
  State<MainMenuPage> createState() => _MainMenuPageState();
}

class _MainMenuPageState extends State<MainMenuPage>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 900),
    )..forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Widget _entrance({
    required double start,
    required double end,
    required Widget child,
  }) {
    final anim = CurvedAnimation(
      parent: _controller,
      curve: Interval(start, end, curve: Curves.easeOut),
    );
    return FadeTransition(
      opacity: anim,
      child: SlideTransition(
        position: Tween<Offset>(
          begin: const Offset(0, 0.12),
          end: Offset.zero,
        ).animate(anim),
        child: child,
      ),
    );
  }

  void _openSlidingMenu(BuildContext context) async {
    final prefs = await SharedPreferences.getInstance();
    bool hasSave = prefs.containsKey('sliding_saved_tiles');

    if (!context.mounted) return;
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: const Color(0xFF1B2838),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text(
          'Sliding Puzzle',
          style: TextStyle(
            color: Colors.cyanAccent,
            fontWeight: FontWeight.bold,
          ),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (hasSave) ...[
              ElevatedButton.icon(
                onPressed: () {
                  Navigator.pop(context);
                  int savedSize = prefs.getInt('sliding_grid_size') ?? 3;
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => SlidingPuzzlePage(
                        isNewGame: false,
                        gridSize: savedSize,
                      ),
                    ),
                  );
                },
                icon: const Icon(Icons.play_arrow),
                label: const Text('Resume Game'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.amberAccent,
                  foregroundColor: Colors.black,
                  padding: const EdgeInsets.symmetric(vertical: 12),
                ),
              ),
              const Divider(color: Colors.white24, height: 30),
            ],
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) =>
                        const SlidingPuzzlePage(isNewGame: true, gridSize: 3),
                  ),
                );
              },
              child: const Text('New Game 3x3'),
            ),
            const SizedBox(height: 10),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) =>
                        const SlidingPuzzlePage(isNewGame: true, gridSize: 4),
                  ),
                );
              },
              child: const Text('New Game 4x4'),
            ),
            const SizedBox(height: 10),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) =>
                        const SlidingPuzzlePage(isNewGame: true, gridSize: 5),
                  ),
                );
              },
              child: const Text('New Game 5x5'),
            ),
            const Divider(color: Colors.white24, height: 30),
            ElevatedButton.icon(
              onPressed: () {
                Navigator.pop(context);
                _openPhotoPuzzleFlow(context);
              },
              icon: const Icon(Icons.photo_library),
              label: const Text('Play with a Photo'),
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.cyanAccent,
                foregroundColor: Colors.black,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _openPhotoPuzzleFlow(BuildContext context) async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.image,
      withData: true,
    );
    final Uint8List? bytes = result?.files.single.bytes;
    if (bytes == null || !context.mounted) return;

    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: const Color(0xFF1B2838),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text(
          'Choose Grid Size',
          style: TextStyle(
            color: Colors.cyanAccent,
            fontWeight: FontWeight.bold,
          ),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [3, 4, 5].map((size) {
            return Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: ElevatedButton(
                onPressed: () {
                  Navigator.pop(context);
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => ImageSlidingPuzzlePage(
                        gridSize: size,
                        imageBytes: bytes,
                      ),
                    ),
                  );
                },
                child: Text('${size}x$size'),
              ),
            );
          }).toList(),
        ),
      ),
    );
  }

  void _openBlockMenu(BuildContext context) async {
    final prefs = await SharedPreferences.getInstance();
    bool hasSave = prefs.containsKey('block_saved_board');

    if (!context.mounted) return;
    showDialog(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: const Color(0xFF2A0845),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text(
          'Block Puzzle',
          style: TextStyle(
            color: Colors.pinkAccent,
            fontWeight: FontWeight.bold,
          ),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (hasSave) ...[
              ElevatedButton.icon(
                onPressed: () {
                  Navigator.pop(context);
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => const BlockPuzzlePage(isNewGame: false),
                    ),
                  );
                },
                icon: const Icon(Icons.play_arrow),
                label: const Text('Resume Game'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.amberAccent,
                  foregroundColor: Colors.black,
                  padding: const EdgeInsets.symmetric(vertical: 12),
                ),
              ),
              const Divider(color: Colors.white24, height: 30),
            ],
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) =>
                        const BlockPuzzlePage(isNewGame: true, slotCount: 3),
                  ),
                );
              },
              style: ElevatedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              child: const Text('New Game (3 Shapes)'),
            ),
            const SizedBox(height: 10),
            ElevatedButton(
              onPressed: () {
                Navigator.pop(context);
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) =>
                        const BlockPuzzlePage(isNewGame: true, slotCount: 6),
                  ),
                );
              },
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.pinkAccent,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              child: const Text('New Game (6 Shapes)'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _confetti({
    required double size,
    required double angle,
    required Color color,
    double? top,
    double? left,
    double? right,
    double? bottom,
  }) {
    return Positioned(
      top: top,
      left: left,
      right: right,
      bottom: bottom,
      child: IgnorePointer(
        child: Opacity(
          opacity: 0.5,
          child: Transform.rotate(
            angle: angle,
            child: Icon(Icons.extension_rounded, size: size, color: color),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: AmbientGlowBackground(
        gradientColors: const [
          Color(0xFF0F0C29),
          Color(0xFF302B63),
          Color(0xFF24243E),
        ],
        child: Stack(
          children: [
            _confetti(
              size: 60,
              angle: -0.4,
              color: Colors.amberAccent,
              top: 20,
              left: 10,
            ),
            _confetti(
              size: 54,
              angle: 0.5,
              color: Colors.purpleAccent,
              top: 30,
              right: 10,
            ),
            _confetti(
              size: 56,
              angle: 0.3,
              color: Colors.amberAccent,
              bottom: 90,
              left: 6,
            ),
            _confetti(
              size: 50,
              angle: -0.5,
              color: Colors.cyanAccent,
              bottom: 100,
              right: 12,
            ),
            SafeArea(
              child: Column(
                children: [
                  Expanded(
                    child: SingleChildScrollView(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const SizedBox(height: 10),
                          _entrance(
                            start: 0.0,
                            end: 0.5,
                            child: _buildLogo(),
                          ),
                          const SizedBox(height: 20),
                          _entrance(
                            start: 0.12,
                            end: 0.6,
                            child: _buildTitle(),
                          ),
                          const SizedBox(height: 8),
                          _entrance(
                            start: 0.15,
                            end: 0.6,
                            child: _buildTagline(),
                          ),
                          const SizedBox(height: 32),
                          Padding(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 16,
                            ),
                            child: GridView.count(
                              crossAxisCount: 3,
                              shrinkWrap: true,
                              physics: const NeverScrollableScrollPhysics(),
                              mainAxisSpacing: 14,
                              crossAxisSpacing: 14,
                              childAspectRatio: 0.72,
                              children: [
                                _entrance(
                                  start: 0.2,
                                  end: 0.65,
                                  child: _GameCard(
                                    title: 'Sliding Puzzle',
                                    icon: Icons.grid_on,
                                    imagePath: 'assets/image/sliding.png',
                                    color1: const Color(0xFF00C9FF),
                                    color2: const Color(0xFF0072FF),
                                    onTap: () => _openSlidingMenu(context),
                                  ),
                                ),
                                _entrance(
                                  start: 0.26,
                                  end: 0.7,
                                  child: _GameCard(
                                    title: 'Block Puzzle',
                                    icon: Icons.extension,
                                    imagePath: 'assets/image/block.png',
                                    color1: const Color(0xFFFF5FA2),
                                    color2: const Color(0xFFDD2476),
                                    onTap: () => _openBlockMenu(context),
                                  ),
                                ),
                                _entrance(
                                  start: 0.32,
                                  end: 0.75,
                                  child: _GameCard(
                                    title: 'Jigsaw Puzzle',
                                    icon: Icons.extension_outlined,
                                    imagePath: 'assets/image/jigsaw.png',
                                    color1: const Color(0xFFB06AB3),
                                    color2: const Color(0xFF7B2FF7),
                                    onTap: () {
                                      Navigator.push(
                                        context,
                                        MaterialPageRoute(
                                          builder: (_) =>
                                              const JigsawLevelSelectPage(),
                                        ),
                                      );
                                    },
                                  ),
                                ),
                                _entrance(
                                  start: 0.38,
                                  end: 0.8,
                                  child: _GameCard(
                                    title: 'Snake Game',
                                    icon: Icons.timeline,
                                    imagePath: 'assets/image/snack.png',
                                    color1: const Color(0xFF56D96A),
                                    color2: const Color(0xFF11998E),
                                    onTap: () {
                                      Navigator.push(
                                        context,
                                        MaterialPageRoute(
                                          builder: (_) =>
                                              const SnakeMapSelectPage(),
                                        ),
                                      );
                                    },
                                  ),
                                ),
                                _entrance(
                                  start: 0.44,
                                  end: 0.85,
                                  child: _GameCard(
                                    title: 'Sudoku',
                                    icon: Icons.grid_on,
                                    imagePath: 'assets/image/sudoku.png',
                                    color1: const Color(0xFF29B6F6),
                                    color2: const Color(0xFF1976D2),
                                    onTap: () {
                                      Navigator.push(
                                        context,
                                        MaterialPageRoute(
                                          builder: (_) =>
                                              const SudokuLevelSelectPage(),
                                        ),
                                      );
                                    },
                                  ),
                                ),
                                _entrance(
                                  start: 0.5,
                                  end: 1.0,
                                  child: _GameCard(
                                    title: 'Classic Puzzle',
                                    icon: Icons.view_module,
                                    imagePath: 'assets/image/classic.png',
                                    color1: const Color(0xFFFFD200),
                                    color2: const Color(0xFFF7971E),
                                    onTap: () {
                                      Navigator.push(
                                        context,
                                        MaterialPageRoute(
                                          builder: (_) =>
                                              const ClassicBlockPuzzlePage(),
                                        ),
                                      );
                                    },
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 20),
                        ],
                      ),
                    ),
                  ),
                  _buildFooter(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildLogo() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(24),
        gradient: const LinearGradient(
          colors: [Colors.cyanAccent, Colors.pinkAccent],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.cyanAccent.withValues(alpha: 0.45),
            blurRadius: 30,
            spreadRadius: 4,
          ),
        ],
      ),
      child: const Icon(
        Icons.videogame_asset,
        size: 48,
        color: Colors.white,
      ),
    );
  }

  Widget _buildTitle() {
    return RichText(
      text: TextSpan(
        style: const TextStyle(
          fontSize: 40,
          fontWeight: FontWeight.w900,
          letterSpacing: 2,
        ),
        children: [
          const TextSpan(
            text: 'PUZZLE ',
            style: TextStyle(
              color: Colors.white,
              shadows: [
                Shadow(
                  color: Colors.cyanAccent,
                  blurRadius: 16,
                ),
              ],
            ),
          ),
          TextSpan(
            text: 'HUB',
            style: TextStyle(
              foreground: Paint()
                ..shader =
                    const LinearGradient(
                      colors: [Colors.orangeAccent, Colors.pinkAccent],
                    ).createShader(
                      const Rect.fromLTWH(0, 0, 140, 40),
                    ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTagline() {
    Widget dot() => Container(
      width: 5,
      height: 5,
      margin: const EdgeInsets.symmetric(horizontal: 8),
      decoration: const BoxDecoration(
        color: Colors.amberAccent,
        shape: BoxShape.circle,
      ),
    );

    TextStyle style = TextStyle(
      color: Colors.white.withValues(alpha: 0.85),
      fontSize: 15,
      fontWeight: FontWeight.w700,
    );

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Think', style: style),
        dot(),
        Text('Solve', style: style),
        dot(),
        Text('Enjoy', style: style),
      ],
    );
  }

  Widget _buildFooter() {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 14),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            'Developed by ',
            style: TextStyle(
              color: Colors.white.withValues(alpha: 0.6),
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
          Image.asset('assets/image/develop.png', height: 22, fit: BoxFit.contain),
        ],
      ),
    );
  }
}

class _GameCard extends StatefulWidget {
  final String title;
  final IconData icon;
  final String? imagePath;
  final Color color1;
  final Color color2;
  final VoidCallback onTap;

  const _GameCard({
    required this.title,
    required this.icon,
    this.imagePath,
    required this.color1,
    required this.color2,
    required this.onTap,
  });

  @override
  State<_GameCard> createState() => _GameCardState();
}

class _GameCardState extends State<_GameCard> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTapDown: (_) => setState(() => _pressed = true),
      onTapCancel: () => setState(() => _pressed = false),
      onTapUp: (_) => setState(() => _pressed = false),
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _pressed ? 0.95 : 1.0,
        duration: const Duration(milliseconds: 120),
        curve: Curves.easeOut,
        child: Container(
                width: double.infinity,
                height: double.infinity,
                padding: const EdgeInsets.symmetric(
                  vertical: 14,
                  horizontal: 6,
                ),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(22),
                  gradient: LinearGradient(
                    colors: [widget.color1, widget.color2],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.35),
                      blurRadius: 12,
                      offset: const Offset(0, 5),
                    ),
                  ],
                ),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Container(
                      width: 46,
                      height: 46,
                      decoration: BoxDecoration(
                        color: widget.imagePath == null
                            ? Colors.white
                            : null,
                        borderRadius: BorderRadius.circular(14),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.15),
                            blurRadius: 5,
                            offset: const Offset(0, 3),
                          ),
                        ],
                      ),
                      child: widget.imagePath != null
                          ? ClipRRect(
                              borderRadius: BorderRadius.circular(14),
                              child: Image.asset(
                                widget.imagePath!,
                                width: 46,
                                height: 46,
                                fit: BoxFit.cover,
                              ),
                            )
                          : Icon(widget.icon, size: 26, color: widget.color2),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      widget.title,
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 12.5,
                        fontWeight: FontWeight.w900,
                        color: Colors.white,
                        letterSpacing: 0.1,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Container(
                      width: 26,
                      height: 26,
                      decoration: BoxDecoration(
                        color: Colors.white.withValues(alpha: 0.3),
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.arrow_forward,
                        size: 16,
                        color: Colors.white,
                      ),
                    ),
                  ],
                ),
              ),
      ),
    );
  }
}
