import 'dart:math';

import 'package:flutter/material.dart';

import '../game/bike_game.dart';
import '../game/game_painter.dart';
import '../shop/catalog.dart';

/// A still of [track] being ridden, drawn by the game's own painter: the
/// sample picture on the track's card. It fills the space it is given.
class TrackPreview extends StatelessWidget {
  const TrackPreview({super.key, required this.track});

  final Track track;

  /// Each still is set up once and kept, as the menu rebuilds every frame.
  static final Map<String, _PreviewPainter> _painters = {};

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: CustomPaint(
        painter: _painters.putIfAbsent(
            track.id, () => _PreviewPainter(_sampleRide(track))),
        size: Size.infinite,
      ),
    );
  }
}

/// The world size the still is laid out in, where the game draws at 1:1.
const _world = Size(BikeGame.worldWidth, 700);

/// A moment of a run on [track]: traffic going the track's way, and on an
/// obstacle road a row of obstacles with the lane it leaves open.
BikeGame _sampleRide(Track track) {
  final g = BikeGame(random: Random(track.stage))
    ..resize(_world)
    ..setTrack(track)
    ..playerLane = 1
    ..targetLane = 1;

  void vehicle(int lane, double y, int color,
      {VehicleKind kind = VehicleKind.car}) {
    g.vehicles.add(Vehicle(
      lane: lane,
      y: y,
      speed: 0,
      color: BikeGame.carColors[color],
      kind: kind,
      oncoming: g.oncomingLane(lane),
    ));
  }

  void coins(int lane, List<double> ys) {
    for (final y in ys) {
      g.pickups.add(Pickup(PickupKind.coin, lane, y));
    }
  }

  if (track.hasObstacles) {
    const row = 440.0;
    g.obstacles.addAll([
      Obstacle(ObstacleKind.rock, 0, row),
      Obstacle(ObstacleKind.ramp, 1, row),
      Obstacle(ObstacleKind.barrier, 2, row),
    ]);
    // A car pulled up behind the barrier: above it if it comes the other
    // way, below it if it goes the bike's way.
    vehicle(3, g.oncomingLane(3) ? 376 : 504, 1);
    if (g.oncomingLane(3) && !g.oncomingLane(0)) {
      // A two-way road: one going the bike's way behind the rock too.
      vehicle(0, 514, 5);
    } else {
      g.obstacles.add(Obstacle(ObstacleKind.oil, 0, 522));
    }
    coins(1, const [344, 380]);
  } else {
    vehicle(0, 382, 1);
    vehicle(2, 410, 6, kind: VehicleKind.truck);
    vehicle(3, 505, 0);
    coins(1, const [360, 406, 452]);
  }
  return g;
}

class _PreviewPainter extends CustomPainter {
  _PreviewPainter(BikeGame game) : _game = GamePainter(game);

  final GamePainter _game;

  /// The part of the world shown: the road just ahead of the bike, in
  /// about the shape of a card's picture.
  static const _view = Rect.fromLTWH(36, 322, 328, 244);

  @override
  void paint(Canvas canvas, Size size) {
    // Covers the space, cutting off the sides or the ends of the view if
    // it is a different shape.
    final scale = max(size.width / _view.width, size.height / _view.height);
    canvas.save();
    canvas.clipRect(Offset.zero & size);
    canvas.translate(size.width / 2, size.height / 2);
    canvas.scale(scale);
    canvas.translate(-_view.center.dx, -_view.center.dy);
    _game.paint(canvas, _world);
    canvas.restore();
  }

  @override
  bool shouldRepaint(_PreviewPainter oldDelegate) => false;
}
