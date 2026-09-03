import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'sliding_puzzle.dart';
import 'image_sliding_puzzle.dart';
import 'block_puzzle.dart';
import 'snake_game.dart';
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
                    builder: (_) => const BlockPuzzlePage(isNewGame: true),
                  ),
                );
              },
              style: ElevatedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              child: const Text('Start New Game'),
            ),
          ],
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
        orbColors: const [Colors.cyanAccent, Colors.pinkAccent],
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _entrance(start: 0.0, end: 0.5, child: _buildLogo()),
                  const SizedBox(height: 28),
                  _entrance(start: 0.12, end: 0.6, child: _buildTitle()),
                  const SizedBox(height: 56),
                  _entrance(
                    start: 0.3,
                    end: 0.85,
                    child: _GameCard(
                      title: 'Sliding Puzzle',
                      subtitle: 'Classic Number Brain Teaser',
                      icon: Icons.grid_on,
                      color1: const Color(0xFF00C9FF),
                      color2: const Color(0xFF92FE9D),
                      onTap: () => _openSlidingMenu(context),
                    ),
                  ),
                  const SizedBox(height: 26),
                  _entrance(
                    start: 0.4,
                    end: 0.9,
                    child: _GameCard(
                      title: 'Block Puzzle',
                      subtitle: 'Match & Clear Grid',
                      icon: Icons.extension,
                      color1: const Color(0xFFFF512F),
                      color2: const Color(0xFFDD2476),
                      onTap: () => _openBlockMenu(context),
                    ),
                  ),
                  const SizedBox(height: 26),
                  _entrance(
                    start: 0.5,
                    end: 1.0,
                    child: _GameCard(
                      title: 'Snake Game',
                      subtitle: 'Classic Retro Arcade',
                      icon: Icons.timeline,
                      color1: const Color(0xFF11998E),
                      color2: const Color(0xFF38EF7D),
                      onTap: () {
                        Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => const SnakeMapSelectPage(),
                          ),
                        );
                      },
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

  Widget _buildLogo() {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: RadialGradient(
          colors: [
            Colors.cyanAccent.withValues(alpha: 0.25),
            Colors.transparent,
          ],
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.cyanAccent.withValues(alpha: 0.35),
            blurRadius: 50,
            spreadRadius: 8,
          ),
        ],
      ),
      child: ShaderMask(
        shaderCallback: (bounds) => const LinearGradient(
          colors: [Colors.cyanAccent, Colors.pinkAccent],
        ).createShader(bounds),
        child: const Icon(
          Icons.videogame_asset,
          size: 72,
          color: Colors.white,
        ),
      ),
    );
  }

  Widget _buildTitle() {
    return ShaderMask(
      shaderCallback: (bounds) => const LinearGradient(
        colors: [Colors.cyanAccent, Colors.white, Colors.pinkAccent],
      ).createShader(bounds),
      child: const Text(
        'PUZZLE HUB',
        style: TextStyle(
          fontSize: 40,
          fontWeight: FontWeight.w900,
          color: Colors.white,
          letterSpacing: 6,
        ),
      ),
    );
  }
}

class _GameCard extends StatefulWidget {
  final String title;
  final String subtitle;
  final IconData icon;
  final Color color1;
  final Color color2;
  final VoidCallback onTap;

  const _GameCard({
    required this.title,
    required this.subtitle,
    required this.icon,
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
        scale: _pressed ? 0.97 : 1.0,
        duration: const Duration(milliseconds: 120),
        curve: Curves.easeOut,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 30),
          child: Container(
            padding: const EdgeInsets.symmetric(
              horizontal: 22,
              vertical: 22,
            ),
            decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(26),
                  gradient: LinearGradient(
                    colors: [
                      widget.color1.withValues(alpha: 0.35),
                      widget.color2.withValues(alpha: 0.2),
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.25),
                    width: 1.3,
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: widget.color2.withValues(alpha: 0.45),
                      blurRadius: 26,
                      spreadRadius: 1,
                      offset: const Offset(0, 10),
                    ),
                  ],
                ),
                child: Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        gradient: LinearGradient(
                          colors: [widget.color1, widget.color2],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: widget.color1.withValues(alpha: 0.6),
                            blurRadius: 16,
                            spreadRadius: 1,
                          ),
                        ],
                      ),
                      child: Icon(widget.icon, size: 36, color: Colors.white),
                    ),
                    const SizedBox(width: 20),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            widget.title,
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.w900,
                              color: Colors.white,
                              letterSpacing: 0.6,
                            ),
                          ),
                          const SizedBox(height: 5),
                          Text(
                            widget.subtitle,
                            style: TextStyle(
                              fontSize: 14,
                              color: Colors.white.withValues(alpha: 0.75),
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Icon(
                      Icons.arrow_forward_ios,
                      color: Colors.white.withValues(alpha: 0.85),
                      size: 22,
                    ),
                  ],
                ),
              ),
        ),
      ),
    );
  }
}
