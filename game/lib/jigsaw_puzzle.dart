import 'dart:async';
import 'dart:math';
import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'ui/ambient_background.dart';
import 'ui/glass_panel.dart';
import 'ui/result_dialog.dart';

const String kDefaultJigsawImage = 'assets/image/jigsawdefaultimage.jpg';

class JigsawLevelSelectPage extends StatefulWidget {
  const JigsawLevelSelectPage({super.key});

  @override
  State<JigsawLevelSelectPage> createState() => _JigsawLevelSelectPageState();
}

class _JigsawLevelSelectPageState extends State<JigsawLevelSelectPage> {
  Uint8List? _customImage;
  Uint8List? _defaultImage;

  @override
  void initState() {
    super.initState();
    _loadDefaultImage();
  }

  Future<void> _loadDefaultImage() async {
    final data = await rootBundle.load(kDefaultJigsawImage);
    if (!mounted) return;
    setState(() => _defaultImage = data.buffer.asUint8List());
  }

  Uint8List? get _previewImage => _customImage ?? _defaultImage;

  Future<void> _pickPhoto() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.image,
      withData: true,
    );
    final bytes = result?.files.single.bytes;
    if (bytes == null) return;
    if (!mounted) return;
    setState(() => _customImage = bytes);
  }

  void _openLevel(int gridSize) {
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) =>
            JigsawGamePage(gridSize: gridSize, imageBytes: _customImage),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Jigsaw Puzzle',
          style: TextStyle(fontWeight: FontWeight.w900, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF1B0B2E), Color(0xFF3A1258)],
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              child: Padding(
                padding: const EdgeInsets.all(24.0),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const SizedBox(height: 60),
                    GestureDetector(
                      onTap: _pickPhoto,
                      child: GlassPanel(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                          vertical: 16,
                        ),
                        borderRadius: BorderRadius.circular(18),
                        glowColor: Colors.purpleAccent,
                        child: Row(
                          children: [
                            const Icon(
                              Icons.photo_library,
                              color: Colors.purpleAccent,
                            ),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Text(
                                _customImage != null
                                    ? 'Custom photo selected'
                                    : 'Upload a photo (optional)',
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            if (_previewImage != null)
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(
                                  _previewImage!,
                                  width: 40,
                                  height: 40,
                                  fit: BoxFit.cover,
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 30),
                    _levelCard(
                      gridSize: 4,
                      title: 'Easy',
                      subtitle: '4x4 - 16 pieces',
                      color: const Color(0xFF00E676),
                    ),
                    const SizedBox(height: 20),
                    _levelCard(
                      gridSize: 6,
                      title: 'Medium',
                      subtitle: '6x6 - 36 pieces',
                      color: const Color(0xFF00C9FF),
                    ),
                    const SizedBox(height: 20),
                    _levelCard(
                      gridSize: 8,
                      title: 'Hard',
                      subtitle: '8x8 - 64 pieces',
                      color: const Color(0xFFD500F9),
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

  Widget _levelCard({
    required int gridSize,
    required String title,
    required String subtitle,
    required Color color,
  }) {
    return GestureDetector(
      onTap: () => _openLevel(gridSize),
      child: GlassPanel(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 20),
        borderRadius: BorderRadius.circular(22),
        glowColor: color,
        child: Row(
          children: [
            Container(
              width: 58,
              height: 58,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(
                  colors: [color.withValues(alpha: 0.6), Colors.transparent],
                ),
              ),
              child: _previewImage != null
                  ? ClipOval(
                      child: Image.memory(
                        _previewImage!,
                        width: 58,
                        height: 58,
                        fit: BoxFit.cover,
                      ),
                    )
                  : Icon(Icons.extension, color: color, size: 30),
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

const double kBumpDepth = 0.28;

/// Adds one edge of a jigsaw piece to [path], from the current point.
/// [origin] is the edge's start corner, [alongUnit] the unit direction of
/// travel along the edge, [outUnit] the unit direction pointing away from
/// the piece's interior at this edge, and [sign] is +1 for a tab (bulges
/// outward), -1 for a blank/notch (cuts inward), or 0 for a straight border.
void _addJigsawEdge(
  Path path,
  Offset origin,
  Offset alongUnit,
  Offset outUnit,
  double edgeLen,
  int sign,
) {
  Offset pt(double u, double v) {
    final base = origin + alongUnit * (u * edgeLen);
    return base + outUnit * (v * sign * kBumpDepth * edgeLen);
  }

  if (sign == 0) {
    final end = pt(1, 0);
    path.lineTo(end.dx, end.dy);
    return;
  }

  var p = pt(0.35, 0);
  path.lineTo(p.dx, p.dy);

  var c1 = pt(0.38, 0.15);
  var c2 = pt(0.27, 0.35);
  p = pt(0.40, 0.5);
  path.cubicTo(c1.dx, c1.dy, c2.dx, c2.dy, p.dx, p.dy);

  c1 = pt(0.30, 0.68);
  c2 = pt(0.30, 1.0);
  p = pt(0.5, 1.0);
  path.cubicTo(c1.dx, c1.dy, c2.dx, c2.dy, p.dx, p.dy);

  c1 = pt(0.70, 1.0);
  c2 = pt(0.70, 0.68);
  p = pt(0.60, 0.5);
  path.cubicTo(c1.dx, c1.dy, c2.dx, c2.dy, p.dx, p.dy);

  c1 = pt(0.73, 0.35);
  c2 = pt(0.62, 0.15);
  p = pt(0.65, 0);
  path.cubicTo(c1.dx, c1.dy, c2.dx, c2.dy, p.dx, p.dy);

  final end = pt(1.0, 0);
  path.lineTo(end.dx, end.dy);
}

/// Builds a piece's clip path in unit-cell coordinates: the nominal cell is
/// (0,0)-(1,1), with tabs/blanks extending into the [-kBumpDepth, 1+kBumpDepth]
/// margin. [top]/[right]/[bottom]/[left] are each -1 (blank), 0 (straight
/// border), or +1 (tab), from this piece's own point of view.
Path _buildPieceShape({
  required int top,
  required int right,
  required int bottom,
  required int left,
}) {
  final path = Path();
  path.moveTo(0, 0);
  _addJigsawEdge(path, const Offset(0, 0), const Offset(1, 0), const Offset(0, -1), 1.0, top);
  _addJigsawEdge(path, const Offset(1, 0), const Offset(0, 1), const Offset(1, 0), 1.0, right);
  _addJigsawEdge(path, const Offset(1, 1), const Offset(-1, 0), const Offset(0, 1), 1.0, bottom);
  _addJigsawEdge(path, const Offset(0, 1), const Offset(0, -1), const Offset(-1, 0), 1.0, left);
  path.close();
  return path;
}

class _PieceClipper extends CustomClipper<Path> {
  final Path unitPath;
  final double cellSize;
  _PieceClipper(this.unitPath, this.cellSize);

  @override
  Path getClip(Size size) {
    final m = Matrix4.identity()
      ..scaleByDouble(cellSize, cellSize, 1.0, 1.0)
      ..translateByDouble(kBumpDepth, kBumpDepth, 0.0, 1.0);
    return unitPath.transform(m.storage);
  }

  @override
  bool shouldReclip(covariant _PieceClipper oldClipper) =>
      oldClipper.cellSize != cellSize || oldClipper.unitPath != unitPath;
}

class JigsawGamePage extends StatefulWidget {
  final int gridSize;
  final Uint8List? imageBytes;
  const JigsawGamePage({super.key, required this.gridSize, this.imageBytes});

  @override
  State<JigsawGamePage> createState() => _JigsawGamePageState();
}

class _JigsawGamePageState extends State<JigsawGamePage> {
  Uint8List? _image;
  late List<int> trayOrder;
  late List<bool> placed;
  late List<Path> pieceShapes;
  int elapsedSeconds = 0;
  Timer? _timer;
  bool isReady = false;
  final Random _random = Random();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    if (widget.imageBytes != null) {
      _image = widget.imageBytes;
    } else {
      final data = await rootBundle.load(kDefaultJigsawImage);
      _image = data.buffer.asUint8List();
    }
    _resetPieces();
    if (!mounted) return;
    setState(() => isReady = true);
  }

  void _resetPieces() {
    final n = widget.gridSize;
    final total = n * n;
    placed = List.filled(total, false);
    trayOrder = List.generate(total, (i) => i)..shuffle(_random);
    _generatePieceShapes();
    elapsedSeconds = 0;
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() => elapsedSeconds++);
    });
  }

  void _generatePieceShapes() {
    final n = widget.gridSize;
    // hEdge[r*(n-1)+c]: tab direction (+1/-1) of the vertical border between
    // column c and c+1 at row r, where +1 bulges toward increasing column.
    final hEdge = List.generate(n * (n - 1), (_) => _random.nextBool() ? 1 : -1);
    // vEdge[r*n+c]: tab direction of the horizontal border between row r and
    // r+1 at column c, where +1 bulges toward increasing row.
    final vEdge = List.generate((n - 1) * n, (_) => _random.nextBool() ? 1 : -1);

    pieceShapes = List.generate(n * n, (index) {
      final r = index ~/ n;
      final c = index % n;
      final top = r == 0 ? 0 : -vEdge[(r - 1) * n + c];
      final bottom = r == n - 1 ? 0 : vEdge[r * n + c];
      final left = c == 0 ? 0 : -hEdge[r * (n - 1) + (c - 1)];
      final right = c == n - 1 ? 0 : hEdge[r * (n - 1) + c];
      return _buildPieceShape(top: top, right: right, bottom: bottom, left: left);
    });
  }

  void _onPiecePlaced(int pieceIndex, int cellIndex) {
    if (pieceIndex != cellIndex || placed[pieceIndex]) return;
    setState(() {
      placed[pieceIndex] = true;
      trayOrder.remove(pieceIndex);
    });
    if (placed.every((p) => p)) {
      _onSolved();
    }
  }

  void _onSolved() {
    _timer?.cancel();
    final minutes = elapsedSeconds ~/ 60;
    final seconds = elapsedSeconds % 60;
    final timeStr = '$minutes:${seconds.toString().padLeft(2, '0')}';
    Future.delayed(const Duration(milliseconds: 200), () {
      if (!mounted) return;
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (_) => ResultDialog(
          icon: Icons.emoji_events_rounded,
          accentColor: Colors.purpleAccent,
          title: 'PUZZLE SOLVED!',
          stats: [ResultStat('TIME', timeStr, highlight: true)],
          primaryLabel: 'PLAY AGAIN',
          onPrimary: () {
            Navigator.of(context).pop();
            setState(_resetPieces);
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

  double _pieceSizeFor(double cellSize) => cellSize * (1 + 2 * kBumpDepth);

  Widget _pieceCrop(int index, double cellSize) {
    final n = widget.gridSize;
    final row = index ~/ n;
    final col = index % n;
    final pieceSize = _pieceSizeFor(cellSize);
    final boardSize = cellSize * n;
    final left = (col - kBumpDepth) * cellSize;
    final top = (row - kBumpDepth) * cellSize;
    final denom = boardSize - pieceSize;
    final alignX = denom == 0 ? 0.0 : (2 * left / denom) - 1;
    final alignY = denom == 0 ? 0.0 : (2 * top / denom) - 1;
    return OverflowBox(
      maxWidth: boardSize,
      maxHeight: boardSize,
      alignment: Alignment(alignX, alignY),
      child: Image.memory(
        _image!,
        width: boardSize,
        height: boardSize,
        fit: BoxFit.cover,
        gaplessPlayback: true,
      ),
    );
  }

  Widget _pieceWidget(int index, double cellSize) {
    final pieceSize = _pieceSizeFor(cellSize);
    return SizedBox(
      width: pieceSize,
      height: pieceSize,
      child: ClipPath(
        clipper: _PieceClipper(pieceShapes[index], cellSize),
        child: _pieceCrop(index, cellSize),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (!isReady) {
      return const Scaffold(
        backgroundColor: Color(0xFF1B0B2E),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    final total = placed.length;
    final placedCount = placed.where((p) => p).length;

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: Text(
          'Jigsaw ${widget.gridSize}x${widget.gridSize}',
          style: const TextStyle(
            fontWeight: FontWeight.w900,
            letterSpacing: 1.2,
          ),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF1B0B2E), Color(0xFF3A1258)],
        child: SafeArea(
          child: SingleChildScrollView(
            child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 8,
                ),
                child: Row(
                  children: [
                    ClipRRect(
                      borderRadius: BorderRadius.circular(10),
                      child: Image.memory(
                        _image!,
                        width: 56,
                        height: 56,
                        fit: BoxFit.cover,
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: GlassPanel(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 8,
                        ),
                        borderRadius: BorderRadius.circular(14),
                        glowColor: Colors.purpleAccent,
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(
                              '${elapsedSeconds ~/ 60}:${(elapsedSeconds % 60).toString().padLeft(2, '0')}',
                              style: const TextStyle(
                                color: Colors.purpleAccent,
                                fontWeight: FontWeight.w900,
                                fontSize: 16,
                              ),
                            ),
                            Text(
                              '$placedCount / $total',
                              style: const TextStyle(
                                color: Colors.white,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: AspectRatio(
                  aspectRatio: 1,
                  child: GlassPanel(
                        padding: const EdgeInsets.all(4),
                        borderRadius: BorderRadius.circular(14),
                        glowColor: Colors.purpleAccent,
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(10),
                          child: Container(
                            color: Colors.black.withValues(alpha: 0.25),
                            child: LayoutBuilder(
                                builder: (context, constraints) {
                                  final displaySize =
                                      constraints.maxWidth / widget.gridSize;
                                  return Stack(
                                    children: [
                                      Positioned.fill(
                                        child: Opacity(
                                          opacity: 0.25,
                                          child: Image.memory(
                                            _image!,
                                            fit: BoxFit.cover,
                                            gaplessPlayback: true,
                                          ),
                                        ),
                                      ),
                                      GridView.builder(
                                        padding: EdgeInsets.zero,
                                        physics:
                                            const NeverScrollableScrollPhysics(),
                                        gridDelegate:
                                            SliverGridDelegateWithFixedCrossAxisCount(
                                              crossAxisCount: widget.gridSize,
                                            ),
                                        itemCount: total,
                                        itemBuilder: (context, index) {
                                          return DragTarget<int>(
                                            onWillAcceptWithDetails: (details) =>
                                                !placed[index] &&
                                                details.data == index,
                                            onAcceptWithDetails: (details) =>
                                                _onPiecePlaced(
                                                  details.data,
                                                  index,
                                                ),
                                            builder:
                                                (
                                                  context,
                                                  candidateData,
                                                  rejectedData,
                                                ) {
                                                  return Container(
                                                    margin: const EdgeInsets.all(
                                                      0.5,
                                                    ),
                                                    decoration: BoxDecoration(
                                                      border: Border.all(
                                                        color: Colors.white
                                                            .withValues(
                                                              alpha: 0.12,
                                                            ),
                                                        width: 0.5,
                                                      ),
                                                      color:
                                                          candidateData.isNotEmpty
                                                          ? Colors.white
                                                                .withValues(
                                                                  alpha: 0.15,
                                                                )
                                                          : Colors.transparent,
                                                    ),
                                                  );
                                                },
                                          );
                                        },
                                      ),
                                      for (int index = 0; index < total; index++)
                                        if (placed[index])
                                          Positioned(
                                            left:
                                                ((index % widget.gridSize) -
                                                    kBumpDepth) *
                                                displaySize,
                                            top:
                                                ((index ~/ widget.gridSize) -
                                                    kBumpDepth) *
                                                displaySize,
                                            child: IgnorePointer(
                                              child: _pieceWidget(
                                                index,
                                                displaySize,
                                              ),
                                            ),
                                          ),
                                    ],
                                  );
                                },
                              ),
                          ),
                        ),
                      ),
                    ),
                  ),
              Padding(
                padding: const EdgeInsets.all(12.0),
                child: GlassPanel(
                  padding: const EdgeInsets.all(8),
                  borderRadius: BorderRadius.circular(16),
                  glowColor: Colors.purpleAccent,
                  child: trayOrder.isEmpty
                      ? const Center(
                          child: Text(
                            'All pieces placed!',
                            style: TextStyle(
                              color: Colors.white54,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        )
                      : Wrap(
                          alignment: WrapAlignment.center,
                          spacing: 6,
                          runSpacing: 6,
                          children: trayOrder.map((index) {
                            const traySize = 32.0;
                            return Draggable<int>(
                              data: index,
                              feedback: Material(
                                color: Colors.transparent,
                                child: _pieceWidget(index, traySize),
                              ),
                              childWhenDragging: Opacity(
                                opacity: 0.3,
                                child: _pieceWidget(index, traySize),
                              ),
                              child: _pieceWidget(index, traySize),
                            );
                          }).toList(),
                        ),
                ),
              ),
            ],
            ),
          ),
        ),
      ),
    );
  }
}
