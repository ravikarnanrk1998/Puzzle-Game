import 'package:flutter/material.dart';

class AmbientGlowBackground extends StatefulWidget {
  final List<Color> gradientColors;
  final List<Color> orbColors;
  final Widget child;

  const AmbientGlowBackground({
    super.key,
    required this.gradientColors,
    required this.orbColors,
    required this.child,
  });

  @override
  State<AmbientGlowBackground> createState() => _AmbientGlowBackgroundState();
}

class _AmbientGlowBackgroundState extends State<AmbientGlowBackground>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 7),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Widget _orb({
    required Color color,
    double? top,
    double? left,
    double? right,
    double? bottom,
    required double size,
    required double opacity,
  }) {
    return Positioned(
      top: top,
      left: left,
      right: right,
      bottom: bottom,
      child: IgnorePointer(
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            color: color.withValues(alpha: opacity),
            boxShadow: [
              BoxShadow(
                color: color.withValues(alpha: opacity * 0.7),
                blurRadius: 120,
                spreadRadius: 40,
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final second = widget.orbColors.length > 1
        ? widget.orbColors[1]
        : widget.orbColors[0];

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: widget.gradientColors,
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          AnimatedBuilder(
            animation: _controller,
            builder: (context, _) {
              final t = _controller.value;
              return Stack(
                children: [
                  _orb(
                    color: widget.orbColors[0],
                    top: -60 + 24 * t,
                    left: -50 - 10 * t,
                    size: 220,
                    opacity: 0.3,
                  ),
                  _orb(
                    color: second,
                    bottom: -90 + 30 * (1 - t),
                    right: -60 + 15 * t,
                    size: 260,
                    opacity: 0.28,
                  ),
                ],
              );
            },
          ),
          widget.child,
        ],
      ),
    );
  }
}
