import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

import '../../core/theme.dart';

class _Particle {
  _Particle({
    required this.pos,
    required this.vel,
    required this.color,
    required this.size,
    required this.spin,
    required this.life,
    this.rect = true,
  });

  Offset pos;
  Offset vel;
  final Color color;
  final double size;
  final double spin;
  final double life;
  final bool rect;
  double age = 0;
  double rot = 0;
  Offset prev = Offset.zero;
}

/// Confetti cannon. Changing [trigger] fires a new burst.
class ConfettiBurst extends StatefulWidget {
  const ConfettiBurst({
    super.key,
    required this.trigger,
    required this.colors,
    this.count = 150,
    this.origin = const Alignment(0, -0.1),
  });

  final int trigger;
  final List<Color> colors;
  final int count;
  final Alignment origin;

  @override
  State<ConfettiBurst> createState() => _ConfettiBurstState();
}

class _ConfettiBurstState extends State<ConfettiBurst> with SingleTickerProviderStateMixin {
  final _particles = <_Particle>[];
  final _rng = math.Random();
  late final Ticker _ticker;
  Duration _last = Duration.zero;
  Size _size = Size.zero;
  final _repaint = ValueNotifier<int>(0);

  @override
  void initState() {
    super.initState();
    _ticker = createTicker(_tick);
    if (widget.trigger > 0) WidgetsBinding.instance.addPostFrameCallback((_) => _fire());
  }

  @override
  void didUpdateWidget(covariant ConfettiBurst old) {
    super.didUpdateWidget(old);
    if (old.trigger != widget.trigger && widget.trigger > 0) _fire();
  }

  void _fire() {
    if (!mounted || _size == Size.zero) return;
    final o = Offset((widget.origin.x + 1) / 2 * _size.width, (widget.origin.y + 1) / 2 * _size.height);
    for (var i = 0; i < widget.count; i++) {
      final a = -math.pi / 2 + (_rng.nextDouble() - 0.5) * math.pi * 1.25;
      final sp = 380 + _rng.nextDouble() * 820;
      _particles.add(_Particle(
        pos: o,
        vel: Offset(math.cos(a) * sp, math.sin(a) * sp),
        color: widget.colors[_rng.nextInt(widget.colors.length)],
        size: 5 + _rng.nextDouble() * 7,
        spin: (_rng.nextDouble() - 0.5) * 14,
        life: 2.0 + _rng.nextDouble() * 1.4,
        rect: _rng.nextDouble() > 0.25,
      ));
    }
    _last = Duration.zero;
    if (!_ticker.isActive) _ticker.start();
  }

  void _tick(Duration elapsed) {
    final dt = _last == Duration.zero ? 0.016 : (elapsed - _last).inMicroseconds / 1e6;
    _last = elapsed;
    for (final p in _particles) {
      p.age += dt;
      p.vel = Offset(p.vel.dx * (1 - 1.6 * dt), p.vel.dy * (1 - 1.1 * dt) + 980 * dt);
      p.pos += p.vel * dt;
      p.rot += p.spin * dt;
    }
    _particles.removeWhere((p) => p.age > p.life);
    _repaint.value++;
    if (_particles.isEmpty) {
      _ticker.stop();
      _last = Duration.zero;
    }
  }

  @override
  void dispose() {
    _ticker.dispose();
    _repaint.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: LayoutBuilder(builder: (context, c) {
        _size = c.biggest;
        return CustomPaint(painter: _ConfettiPainter(_particles, _repaint), size: c.biggest);
      }),
    );
  }
}

class _ConfettiPainter extends CustomPainter {
  _ConfettiPainter(this.particles, Listenable repaint) : super(repaint: repaint);
  final List<_Particle> particles;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint();
    for (final p in particles) {
      final fade = p.age > p.life * 0.7 ? 1 - (p.age - p.life * 0.7) / (p.life * 0.3) : 1.0;
      paint.color = p.color.o(fade.clamp(0.0, 1.0));
      canvas.save();
      canvas.translate(p.pos.dx, p.pos.dy);
      canvas.rotate(p.rot);
      if (p.rect) {
        final squash = (math.cos(p.rot * 1.7)).abs() * 0.8 + 0.2;
        canvas.drawRect(Rect.fromCenter(center: Offset.zero, width: p.size, height: p.size * 0.55 * squash), paint);
      } else {
        canvas.drawCircle(Offset.zero, p.size * 0.32, paint);
      }
      canvas.restore();
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => true;
}

/// Continuous fireworks over a celebration screen.
class FireworksLayer extends StatefulWidget {
  const FireworksLayer({super.key, this.colors, this.active = true, this.rate = 1.4});

  final List<Color>? colors;
  final bool active;

  /// Bursts per second.
  final double rate;

  @override
  State<FireworksLayer> createState() => _FireworksLayerState();
}

class _FireworksLayerState extends State<FireworksLayer> with SingleTickerProviderStateMixin {
  final _sparks = <_Particle>[];
  final _rng = math.Random();
  late final Ticker _ticker = createTicker(_tick);
  Duration _last = Duration.zero;
  Size _size = Size.zero;
  double _acc = 0;
  final _repaint = ValueNotifier<int>(0);

  List<Color> get _colors =>
      widget.colors ?? const [AppColors.gold, AppColors.cyan, AppColors.pink, AppColors.green, Colors.white, AppColors.violet];

  @override
  void initState() {
    super.initState();
    _ticker.start();
  }

  void _burst() {
    if (_size == Size.zero) return;
    final c = Offset(_size.width * (0.12 + _rng.nextDouble() * 0.76), _size.height * (0.08 + _rng.nextDouble() * 0.38));
    final col = _colors[_rng.nextInt(_colors.length)];
    final col2 = _colors[_rng.nextInt(_colors.length)];
    final n = 46 + _rng.nextInt(30);
    final speed = 150 + _rng.nextDouble() * 140;
    for (var i = 0; i < n; i++) {
      final a = i / n * math.pi * 2 + _rng.nextDouble() * 0.1;
      final sp = speed * (0.75 + _rng.nextDouble() * 0.35);
      _sparks.add(_Particle(
        pos: c,
        vel: Offset(math.cos(a) * sp, math.sin(a) * sp),
        color: i.isEven ? col : col2,
        size: 2.2 + _rng.nextDouble() * 1.6,
        spin: 0,
        life: 1.2 + _rng.nextDouble() * 0.7,
      )..prev = c);
    }
  }

  void _tick(Duration elapsed) {
    final dt = _last == Duration.zero ? 0.016 : ((elapsed - _last).inMicroseconds / 1e6).clamp(0.0, 0.05);
    _last = elapsed;
    if (widget.active) {
      _acc += dt * widget.rate;
      while (_acc >= 1) {
        _acc -= 1;
        _burst();
      }
    }
    for (final p in _sparks) {
      p.age += dt;
      p.prev = p.pos;
      p.vel = Offset(p.vel.dx * (1 - 1.2 * dt), p.vel.dy * (1 - 1.2 * dt) + 120 * dt);
      p.pos += p.vel * dt;
    }
    _sparks.removeWhere((p) => p.age > p.life);
    _repaint.value++;
  }

  @override
  void dispose() {
    _ticker.dispose();
    _repaint.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: LayoutBuilder(builder: (context, c) {
        _size = c.biggest;
        return CustomPaint(painter: _FireworksPainter(_sparks, _repaint), size: c.biggest);
      }),
    );
  }
}

class _FireworksPainter extends CustomPainter {
  _FireworksPainter(this.sparks, Listenable repaint) : super(repaint: repaint);
  final List<_Particle> sparks;

  @override
  void paint(Canvas canvas, Size size) {
    final glow = Paint()..maskFilter = const MaskFilter.blur(BlurStyle.normal, 4);
    final core = Paint()..strokeCap = StrokeCap.round;
    for (final p in sparks) {
      final f = (1 - p.age / p.life).clamp(0.0, 1.0);
      final tail = p.pos - (p.pos - p.prev) * 5;
      core
        ..color = p.color.o(f)
        ..strokeWidth = p.size * f + 0.5;
      canvas.drawLine(tail, p.pos, core);
      glow.color = p.color.o(f * 0.5);
      canvas.drawCircle(p.pos, p.size * 2.2, glow);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => true;
}

/// Periodic glossy light sweep across [child].
class Shine extends StatefulWidget {
  const Shine({super.key, required this.child, this.period = const Duration(milliseconds: 3200)});
  final Widget child;
  final Duration period;

  @override
  State<Shine> createState() => _ShineState();
}

class _ShineState extends State<Shine> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: widget.period)..repeat();

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      child: widget.child,
      builder: (context, child) {
        final t = _c.value * 1.6 - 0.3;
        return ShaderMask(
          blendMode: BlendMode.srcATop,
          shaderCallback: (r) => LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Colors.white.o(0), Colors.white.o(0.55), Colors.white.o(0)],
            stops: [(t - 0.12).clamp(0.0, 1.0), t.clamp(0.0, 1.0), (t + 0.12).clamp(0.0, 1.0)],
          ).createShader(r),
          child: child,
        );
      },
    );
  }
}

/// Gentle breathing scale + glow, for call-to-action elements.
class Pulse extends StatefulWidget {
  const Pulse({super.key, required this.child, this.min = 0.97, this.max = 1.03, this.period = const Duration(milliseconds: 1300)});
  final Widget child;
  final double min, max;
  final Duration period;

  @override
  State<Pulse> createState() => _PulseState();
}

class _PulseState extends State<Pulse> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: widget.period)..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ScaleTransition(
      scale: Tween(begin: widget.min, end: widget.max).animate(CurvedAnimation(parent: _c, curve: Curves.easeInOut)),
      child: widget.child,
    );
  }
}
