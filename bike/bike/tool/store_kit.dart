// Shared by the store listing tools: real fonts, staged scenes, an
// autopilot to ride for the video, and PNGs the way Google Play wants them.
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:bike/game/bike_game.dart';
import 'package:bike/game/game_painter.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// Loads the Roboto and Material Icons fonts that ship with the Flutter SDK,
/// so text renders properly instead of as test boxes.
Future<void> loadFonts() async {
  // flutter_tester lives in <flutter>/bin/cache/artifacts/engine/<platform>/
  final artifacts = File(Platform.resolvedExecutable).parent.parent.parent;
  final dir = '${artifacts.path}${Platform.pathSeparator}material_fonts';
  Future<ByteData> read(String name) async {
    final bytes =
        await File('$dir${Platform.pathSeparator}$name').readAsBytes();
    return ByteData.view(bytes.buffer);
  }

  final roboto = FontLoader('Roboto');
  for (final f in [
    'roboto-regular.ttf',
    'roboto-medium.ttf',
    'roboto-bold.ttf',
    'roboto-black.ttf',
    'roboto-italic.ttf',
    'roboto-bolditalic.ttf',
    'roboto-blackitalic.ttf',
  ]) {
    roboto.addFont(read(f));
  }
  await roboto.load();
  await (FontLoader('MaterialIcons')
        ..addFont(read('materialicons-regular.otf')))
      .load();
}

/// Loads the pictures in the game, such as the developer's signature on the
/// menu, which a test would otherwise leave out: decoding them takes real
/// time, not the test's.
Future<void> loadImages(WidgetTester tester) async {
  await tester.runAsync(() => precacheImage(
      const AssetImage('assets/image/develop.png'),
      tester.element(find.byType(Scaffold).first)));
  await tester.pump();
}

/// The game on screen.
BikeGame gameOf(WidgetTester tester) => (tester
        .widget<CustomPaint>(find.byWidgetPredicate(
            (w) => w is CustomPaint && w.painter is GamePainter))
        .painter! as GamePainter)
    .game;

/// Encodes [image] as a 24-bit PNG: without the alpha channel that Google
/// Play turns away in screenshots and the feature graphic.
Future<Uint8List> png24(ui.Image image) async {
  final rgba = (await image.toByteData(format: ui.ImageByteFormat.rawRgba))!
      .buffer
      .asUint8List();
  final w = image.width, h = image.height;
  final rows = Uint8List(h * (w * 3 + 1));
  var o = 0;
  for (var y = 0; y < h; y++) {
    rows[o++] = 0; // No filter on this row.
    for (var i = y * w * 4, end = i + w * 4; i < end; i += 4) {
      rows[o++] = rgba[i];
      rows[o++] = rgba[i + 1];
      rows[o++] = rgba[i + 2];
    }
  }
  final out = BytesBuilder(copy: false)
    ..add(const [137, 80, 78, 71, 13, 10, 26, 10]);
  void chunk(String type, List<int> data) {
    final body = Uint8List.fromList([...ascii.encode(type), ...data]);
    out
      ..add(_uint32(data.length))
      ..add(body)
      ..add(_uint32(_crc32(body)));
  }

  // 8 bits a channel, colour type 2: RGB.
  chunk('IHDR', [..._uint32(w), ..._uint32(h), 8, 2, 0, 0, 0]);
  chunk('IDAT', zlib.encode(rows));
  chunk('IEND', const []);
  return out.takeBytes();
}

List<int> _uint32(int n) =>
    [(n >> 24) & 0xFF, (n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF];

final List<int> _crcTable = List.generate(256, (n) {
  var c = n;
  for (var k = 0; k < 8; k++) {
    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
  }
  return c;
});

int _crc32(List<int> bytes) {
  var c = 0xFFFFFFFF;
  for (final b in bytes) {
    c = _crcTable[(c ^ b) & 0xFF] ^ (c >>> 8);
  }
  return c ^ 0xFFFFFFFF;
}

// --------------------------------------------------------------- scenes

/// Mid-race on the highway: traffic all around and coins ahead.
void stageRace(BikeGame g) {
  g
    ..playTime = 10
    ..lives = 3
    ..invincibleTimer = 0
    ..levelBannerTimer = 0
    ..level = 3
    ..distance = 1650
    ..score = 4820
    ..coins = 37
    ..nitro = 0.8
    ..speed = 520
    ..targetLane = 1
    ..playerLane = 1;
  g.texts.clear();
  g.vehicles
    ..clear()
    ..addAll([
      Vehicle(
          lane: 2,
          y: g.playerY - 90,
          speed: 250,
          color: const Color(0xFF42A5F5)),
      Vehicle(
          lane: 0,
          y: 170,
          speed: 200,
          color: const Color(0xFF26A69A),
          kind: VehicleKind.truck),
      Vehicle(
          lane: 3,
          y: 260,
          speed: 230,
          color: const Color(0xFFFFEE58))
        ..signalDir = -1
        ..signal = 0.6,
    ]);
  g.pickups
    ..clear()
    ..addAll([
      for (var i = 0; i < 4; i++)
        Pickup(PickupKind.coin, 1, g.playerY - 150 - i * 46),
      Pickup(PickupKind.nitro, 2, 90),
    ]);
}

/// On the Mountain Pass, at the top of a jump off a ramp, just over a rock,
/// with a barrier and an oil slick further on.
void stageMountain(BikeGame g) {
  g
    ..playTime = 10
    ..lives = 4
    ..invincibleTimer = 0
    ..levelBannerTimer = 0
    ..level = 4
    ..distance = 2300
    ..score = 7410
    ..coins = 52
    ..nitro = 0.6
    ..speed = 480
    ..targetLane = 2
    ..playerLane = 2
    ..airTimer = BikeGame.jumpTime / 2;
  g.texts
    ..clear()
    ..add(FloatingText('BIG AIR +100', g.playerX, g.playerY - 90,
        const Color(0xFFFFD740),
        size: 24));
  g.vehicles
    ..clear()
    ..add(
        Vehicle(lane: 3, y: 195, speed: 120, color: const Color(0xFF42A5F5)));
  g.pickups
    ..clear()
    ..addAll([
      for (var i = 0; i < 4; i++)
        Pickup(PickupKind.coin, 2, g.playerY - 230 - i * 46),
    ]);
  g.obstacles
    ..clear()
    ..addAll([
      Obstacle(ObstacleKind.rock, 2, g.playerY + 55),
      Obstacle(ObstacleKind.barrier, 0, g.playerY - 200),
      Obstacle(ObstacleKind.oil, 3, g.playerY - 120),
      Obstacle(ObstacleKind.pothole, 1, 150),
      Obstacle(ObstacleKind.rock, 0, 130),
    ]);
}

// ------------------------------------------------------------ autopilot

/// Steers the way a good player would: out of the way of whatever is ahead
/// in its lane, and over to the coins when the way is clear. Boosting, it
/// goes straight for the traffic instead, to smash it.
void autopilot(BikeGame g) {
  if (!g.isRunning || g.skidTimer > 0 || g.playerLane != g.targetLane) return;
  final lane = g.targetLane;
  final front = g.playerY - g.ride.height / 2;
  final rear = g.playerY + g.ride.height / 2;

  /// How far the road ahead in [l] is clear, in world units. Below 0 when
  /// something is right alongside.
  double clear(int l) {
    var gap = double.infinity;
    for (final v in g.vehicles) {
      if (v.lane != l && v.x.round() != l) continue;
      if (v.y - v.height / 2 > rear) continue;
      var d = front - (v.y + v.height / 2);
      // Oncoming traffic closes in faster, so it counts as nearer.
      if (v.oncoming && d > 0) d /= 1.8;
      gap = min(gap, d);
    }
    for (final o in g.obstacles) {
      if (o.kind == ObstacleKind.ramp || !o.covers(l)) continue;
      if (o.y - o.height / 2 > rear) continue;
      gap = min(gap, front - (o.y + o.height / 2));
    }
    return gap;
  }

  final sides = [
    for (final l in [lane - 1, lane + 1])
      if (l >= 0 && l < BikeGame.laneCount) l,
  ];

  if (g.boosting) {
    // The nearest car ahead, in this lane or the next, is the one to hit.
    double nearestCar(int l) {
      var gap = double.infinity;
      for (final v in g.vehicles) {
        if (v.lane != l && v.x.round() != l) continue;
        final d = front - (v.y + v.height / 2);
        if (d > 0) gap = min(gap, d);
      }
      return gap;
    }

    final target = [lane, ...sides].reduce(
        (a, b) => nearestCar(b) < nearestCar(a) - 40 ? b : a);
    if (target != lane) g.steer(target - lane);
    return;
  }

  const safe = 230.0;
  final here = clear(lane);
  if (here < safe) {
    final best = sides.isEmpty
        ? lane
        : sides.reduce((a, b) => clear(b) > clear(a) ? b : a);
    if (best != lane && clear(best) > here) g.steer(best - lane);
    return;
  }
  for (final l in sides) {
    final coin = g.pickups.any((p) =>
        p.lane == l && p.y < g.playerY && p.y > g.playerY - 320);
    if (coin && clear(l) > safe + 120) {
      g.steer(l - lane);
      return;
    }
  }
}
