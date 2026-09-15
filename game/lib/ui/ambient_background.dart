import 'package:flutter/material.dart';

class AmbientGlowBackground extends StatelessWidget {
  final List<Color> gradientColors;
  final Widget child;

  const AmbientGlowBackground({
    super.key,
    required this.gradientColors,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      height: double.infinity,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: gradientColors,
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
      ),
      child: child,
    );
  }
}
