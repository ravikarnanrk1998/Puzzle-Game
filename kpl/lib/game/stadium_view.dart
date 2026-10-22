import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

import '../core/theme.dart';
import '../data/teams.dart';
import 'camera.dart';
import 'ground.dart';
import 'stadium_mesh.dart';

/// Live stadium shot with a slowly orbiting camera (menus, intros).
class StadiumPanorama extends StatefulWidget {
  const StadiumPanorama({super.key, required this.home, this.away, this.hype = 0.25, this.dim = 0, this.day = false});

  final TeamInfo home;
  final TeamInfo? away;
  final double hype;
  final double dim;
  final bool day;

  @override
  State<StadiumPanorama> createState() => _StadiumPanoramaState();
}

class _StadiumPanoramaState extends State<StadiumPanorama> with SingleTickerProviderStateMixin {
  late StadiumMesh _mesh = _make();
  final _ground = GroundRenderer();
  final _time = ValueNotifier<double>(0);
  late final Ticker _ticker = createTicker((d) => _time.value = d.inMicroseconds / 1e6);

  StadiumMesh _make() {
    final m = StadiumMesh(home: widget.home, away: widget.away ?? kTeams[(kTeams.indexOf(widget.home) + 3) % kTeams.length], day: widget.day);
    m.prepare().then((_) {
      if (mounted) setState(() {});
    });
    return m;
  }

  @override
  void initState() {
    super.initState();
    GroundRenderer.load().then((_) {
      if (mounted) setState(() {});
    });
    _ticker.start();
  }

  @override
  void didUpdateWidget(covariant StadiumPanorama old) {
    super.didUpdateWidget(old);
    if (old.home.id != widget.home.id || old.day != widget.day) {
      _mesh.dispose();
      _mesh = _make();
    }
  }

  @override
  void dispose() {
    _ticker.dispose();
    _time.dispose();
    _mesh.dispose();
    _ground.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: CustomPaint(
        painter: _PanoramaPainter(_mesh, _ground, _time, widget.hype, widget.dim),
        child: const SizedBox.expand(),
      ),
    );
  }
}

class _PanoramaPainter extends CustomPainter {
  _PanoramaPainter(this.mesh, this.ground, this.time, this.hype, this.dim) : super(repaint: time);

  final StadiumMesh mesh;
  final GroundRenderer ground;
  final ValueNotifier<double> time;
  final double hype;
  final double dim;

  @override
  void paint(Canvas canvas, Size size) {
    final t = time.value;
    final cam = Rigs.orbit(size, 0.35 + t * 0.035);
    ground.paint(canvas, size, cam, rx: mesh.rx, ry: mesh.ry, team: mesh.home.primary, time: t, day: mesh.day);
    mesh.paint(canvas, size, cam, time: t, hype: hype);
    if (dim > 0) canvas.drawRect(Offset.zero & size, Paint()..color = AppColors.bg0.o(dim));
  }

  @override
  bool shouldRepaint(covariant _PanoramaPainter old) => old.mesh != mesh || old.dim != dim || old.hype != hype;
}
