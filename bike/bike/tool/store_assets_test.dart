// Generates the app icon and the Google Play listing graphics.
//
//   flutter test tool/store_assets_test.dart
//   flutter pub run flutter_launcher_icons
//
// Output: assets/icon/*.png (launcher icon sources) and store_listing/*.png.
import 'dart:io';
import 'dart:math';
import 'dart:ui' as ui;

import 'package:bike/game/bike_art.dart';
import 'package:bike/game/bike_game.dart';
import 'package:bike/game/game_painter.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(_loadFonts);

  test('app icon', () async {
    const full = Size(1024, 1024);
    await _save('assets/icon/icon.png', full, (c, s) {
      _iconBackground(c, s);
      _iconBike(c, s);
    });
    await _save('assets/icon/icon_background.png', full, _iconBackground);
    await _save('assets/icon/icon_foreground.png', full, _iconBike);
    await _save('store_listing/icon_512.png', const Size(512, 512), (c, s) {
      c.scale(0.5);
      _iconBackground(c, full);
      _iconBike(c, full);
    });
  });

  test('feature graphic', () async {
    await _save('store_listing/feature_graphic_1024x500.png',
        const Size(1024, 500), _featureGraphic);
  });

  testWidgets('phone screenshots', (tester) async {
    tester.view.physicalSize = const Size(1080, 1920);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);

    final progress = PlayerProgress.memory()..addCoins(3250);
    progress.recordRun(score: 12840, coins: 0);
    final key = GlobalKey();
    await tester.pumpWidget(RepaintBoundary(
      key: key,
      child: BikeRushApp(
        progress: progress,
        store: StoreService(progress, enabled: false),
      ),
    ));
    await _frames(tester, 40);
    await _shot(tester, key, 'store_listing/screenshot_1_menu.png');

    await tester.tap(find.text('PLAY'));
    await _frames(tester, 10);
    final game = (tester
            .widget<CustomPaint>(find.byWidgetPredicate(
                (w) => w is CustomPaint && w.painter is GamePainter))
            .painter! as GamePainter)
        .game;

    _stageRace(game);
    await _frames(tester, 1);
    game.texts.clear();
    await _frames(tester, 1);
    await _shot(tester, key, 'store_listing/screenshot_2_race.png');

    _stageRace(game);
    game.nitro = 1;
    game.activateBoost();
    await _frames(tester, 25);
    game.texts.clear();
    await _frames(tester, 1);
    await _shot(tester, key, 'store_listing/screenshot_3_nitro.png');

    game.goToMenu();
    await _frames(tester, 5);
    await tester.tap(find.text('GARAGE'));
    await _frames(tester, 30);
    await _shot(tester, key, 'store_listing/screenshot_4_garage.png');
  });
}

void _stageRace(BikeGame g) {
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

Future<void> _frames(WidgetTester tester, int n) async {
  for (var i = 0; i < n; i++) {
    await tester.pump(const Duration(milliseconds: 16));
  }
}

Future<void> _shot(WidgetTester tester, GlobalKey key, String path) async {
  final boundary =
      key.currentContext!.findRenderObject()! as RenderRepaintBoundary;
  await tester.runAsync(() async {
    final image = await boundary.toImage(pixelRatio: 3);
    final data = await image.toByteData(format: ui.ImageByteFormat.png);
    File(path)
      ..createSync(recursive: true)
      ..writeAsBytesSync(data!.buffer.asUint8List());
  });
}

Future<void> _save(
    String path, Size size, void Function(Canvas, Size) draw) async {
  final recorder = ui.PictureRecorder();
  draw(Canvas(recorder), size);
  final image = await recorder
      .endRecording()
      .toImage(size.width.round(), size.height.round());
  final data = await image.toByteData(format: ui.ImageByteFormat.png);
  File(path)
    ..createSync(recursive: true)
    ..writeAsBytesSync(data!.buffer.asUint8List());
}

/// Loads the Roboto and Material Icons fonts that ship with the Flutter SDK,
/// so text renders properly instead of as test boxes.
Future<void> _loadFonts() async {
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

// ------------------------------------------------------------------- art

void _iconBackground(Canvas c, Size s) {
  final rect = Offset.zero & s;
  c.drawRect(
    rect,
    Paint()
      ..shader = const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [Color(0xFFFFB300), Color(0xFFFF3D00)],
      ).createShader(rect),
  );
  final roadW = s.width * 0.46;
  final left = (s.width - roadW) / 2;
  const curb = 34.0;
  final white = Paint()..color = Colors.white;
  final red = Paint()..color = const Color(0xFFE53935);
  c.drawRect(Rect.fromLTWH(left - curb, 0, curb, s.height), white);
  c.drawRect(Rect.fromLTWH(left + roadW, 0, curb, s.height), white);
  for (var y = 0.0; y < s.height; y += 120) {
    c.drawRect(Rect.fromLTWH(left - curb, y, curb, 60), red);
    c.drawRect(Rect.fromLTWH(left + roadW, y, curb, 60), red);
  }
  c.drawRect(Rect.fromLTWH(left, 0, roadW, s.height),
      Paint()..color = const Color(0xFF3D4049));
  final dash = Paint()..color = const Color(0xE6FFFFFF);
  for (var y = -40.0; y < s.height; y += 150) {
    c.drawRRect(
      RRect.fromRectAndRadius(
          Rect.fromLTWH(s.width / 2 - 9, y, 18, 80), const Radius.circular(6)),
      dash,
    );
  }
  final speed = Paint()
    ..color = const Color(0x66FFFFFF)
    ..strokeWidth = 10
    ..strokeCap = StrokeCap.round;
  final rng = Random(7);
  for (var i = 0; i < 10; i++) {
    final side = i.isEven ? -1 : 1;
    final x = s.width / 2 + side * (roadW / 2 + curb + 40 + rng.nextDouble() * 110);
    final y = rng.nextDouble() * s.height;
    c.drawLine(Offset(x, y), Offset(x, y + 90 + rng.nextDouble() * 80), speed);
  }
}

void _iconBike(Canvas c, Size s) {
  c.save();
  c.translate(s.width / 2, s.height / 2);
  c.rotate(-0.12);
  c.scale(s.width / 1024 * 9.5);
  paintBike(c, Catalog.bikes.first);
  c.restore();
}

void _featureGraphic(Canvas c, Size s) {
  final rect = Offset.zero & s;
  c.drawRect(
    rect,
    Paint()
      ..shader = const LinearGradient(
        begin: Alignment.centerLeft,
        end: Alignment.centerRight,
        colors: [Color(0xFF0D1020), Color(0xFF311B92), Color(0xFFBF360C)],
      ).createShader(rect),
  );

  // A slanted strip of road with the bike racing along it.
  c.save();
  c.translate(760, 335);
  c.rotate(-0.28);
  const half = 120.0;
  final white = Paint()..color = Colors.white;
  final red = Paint()..color = const Color(0xFFE53935);
  c.drawRect(const Rect.fromLTRB(-700, -half - 22, 700, half + 22), white);
  for (var x = -700.0; x < 700; x += 80) {
    c.drawRect(Rect.fromLTWH(x, -half - 22, 40, 22), red);
    c.drawRect(Rect.fromLTWH(x, half, 40, 22), red);
  }
  c.drawRect(const Rect.fromLTRB(-700, -half, 700, half),
      Paint()..color = const Color(0xFF3D4049));
  final dash = Paint()..color = const Color(0xE6FFFFFF);
  for (var x = -700.0; x < 700; x += 110) {
    c.drawRect(Rect.fromLTWH(x, -3, 55, 6), dash);
  }
  final coin = Paint()..color = const Color(0xFFFFC107);
  final coinInner = Paint()..color = const Color(0xFFFFE082);
  for (var i = 0; i < 4; i++) {
    final o = Offset(60 + i * 70.0, -55);
    c.drawCircle(o, 17, coin);
    c.drawCircle(o, 11, coinInner);
  }
  final speed = Paint()
    ..color = const Color(0x55FFFFFF)
    ..strokeWidth = 4;
  for (var i = 0; i < 7; i++) {
    final y = -100 + i * 32.0;
    c.drawLine(Offset(-420 + (i % 3) * 40, y), Offset(-200 + (i % 2) * 50, y),
        speed);
  }
  c.save();
  c.translate(-60, 40);
  c.rotate(pi / 2);
  c.scale(3.2);
  paintBike(c, Catalog.bikes.first, boosting: true, time: 0.4);
  c.restore();
  c.restore();

  // Title.
  const titleStyle = TextStyle(
    fontFamily: 'Roboto',
    fontSize: 112,
    fontWeight: FontWeight.w900,
    fontStyle: FontStyle.italic,
    letterSpacing: -2,
    height: 1,
  );
  final shadow = TextPainter(
    text: const TextSpan(
        text: 'BIKE\nRUSH',
        style: TextStyle(
          fontFamily: 'Roboto',
          fontSize: 112,
          fontWeight: FontWeight.w900,
          fontStyle: FontStyle.italic,
          letterSpacing: -2,
          height: 1,
          color: Color(0xAA000000),
        )),
    textDirection: TextDirection.ltr,
  )..layout();
  shadow.paint(c, const Offset(58, 76));
  final title = TextPainter(
    text: TextSpan(
      text: 'BIKE\nRUSH',
      style: titleStyle.copyWith(
        foreground: Paint()
          ..shader = const LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0xFFFFEB3B), Color(0xFFFF9800), Color(0xFFFF3D00)],
          ).createShader(const Rect.fromLTWH(0, 60, 400, 230)),
      ),
    ),
    textDirection: TextDirection.ltr,
  )..layout();
  title.paint(c, const Offset(50, 68));
  final tagline = TextPainter(
    text: const TextSpan(
      text: 'Dodge traffic â€¢ Grab coins â€¢ Hit NITRO',
      style: TextStyle(
        fontFamily: 'Roboto',
        fontSize: 26,
        fontWeight: FontWeight.w700,
        color: Colors.white,
      ),
    ),
    textDirection: TextDirection.ltr,
  )..layout();
  c.drawRRect(
    RRect.fromRectAndRadius(
      Rect.fromLTWH(40, 308, tagline.width + 32, tagline.height + 20),
      const Radius.circular(30),
    ),
    Paint()..color = const Color(0xCC0D1020),
  );
  tagline.paint(c, const Offset(56, 318));
}
