import 'package:flutter/material.dart';

import '../../core/theme.dart';

/// Broadcast-style scrolling news ticker.
class NewsTicker extends StatefulWidget {
  const NewsTicker({super.key, required this.items, this.label = 'NEWS', this.speed = 42});

  final List<String> items;
  final String label;

  /// Pixels per second.
  final double speed;

  @override
  State<NewsTicker> createState() => _NewsTickerState();
}

class _NewsTickerState extends State<NewsTicker> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(seconds: 600))..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final text = '${widget.items.join('     ◆     ')}     ◆     ';
    return Container(
      height: 34,
      decoration: BoxDecoration(
        color: Colors.black.o(0.55),
        border: Border(top: BorderSide(color: AppColors.gold.o(0.5)), bottom: BorderSide(color: AppColors.gold.o(0.2))),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            alignment: Alignment.center,
            decoration: const BoxDecoration(gradient: AppGradients.goldFlat),
            child: Text(widget.label, style: T.display(18, color: const Color(0xFF1A1205), weight: FontWeight.w700, spacing: 1.5)),
          ),
          Expanded(
            child: ClipRect(
              child: CustomPaint(
                painter: _MarqueePainter(text, _c, widget.speed),
                child: const SizedBox.expand(),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MarqueePainter extends CustomPainter {
  _MarqueePainter(this.text, this.anim, this.speed) : super(repaint: anim);

  final String text;
  final Animation<double> anim;
  final double speed;
  TextPainter? _tp;

  @override
  void paint(Canvas canvas, Size size) {
    final tp = _tp ??= TextPainter(
      text: TextSpan(text: text, style: T.ui(14, color: AppColors.text, weight: FontWeight.w700, spacing: 0.4)),
      textDirection: TextDirection.ltr,
      maxLines: 1,
    )..layout();
    final elapsed = anim.value * 600;
    final w = tp.width;
    final x = -((elapsed * speed) % w);
    final y = (size.height - tp.height) / 2;
    tp.paint(canvas, Offset(x, y));
    tp.paint(canvas, Offset(x + w, y));
  }

  @override
  bool shouldRepaint(covariant _MarqueePainter old) => old.text != text;
}
