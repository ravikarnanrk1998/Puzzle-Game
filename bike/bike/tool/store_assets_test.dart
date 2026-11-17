// Generates the app icon and the Google Play listing graphics.
//
//   flutter test tool/store_assets_test.dart
//   flutter pub run flutter_launcher_icons
//
// Output: assets/icon/*.png (launcher icon sources, made from the logo in
// assets/icon/logo_source.png) and store_listing/*.png.
// The screenshots are numbered in the order to upload them.
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:bike/game/bike_art.dart';
import 'package:bike/game/bike_game.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';

import 'store_kit.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(loadFonts);

  test('app icon', () async {
    final logo = await _loadLogo();
    final art = await _logoArtwork(logo);
    const full = Size(1024, 1024);
    // Phones and Google Play round off the corners of the full icon, which
    // leaves the picture whole.
    await _save('assets/icon/icon.png', full,
        (c, s) => _paintFullLogo(c, s, logo));
    await _save('store_listing/icon_512.png', const Size(512, 512),
        (c, s) => _paintFullLogo(c, s, logo));
    // An adaptive icon's mask shows only the middle of it, as a circle or
    // other shape, so the picture is smaller there to keep the words in.
    // It is all in the background layer, with nothing in front.
    await _save('assets/icon/icon_background.png', full,
        (c, s) => _paintLogo(c, s, art, 0.58));
    await _save('assets/icon/icon_foreground.png', full, (c, s) {});
  });

  test('feature graphic', () async {
    await _save('store_listing/feature_graphic_1024x500.png',
        const Size(1024, 500), _featureGraphic,
        alpha: false);
  });

  testWidgets('phone screenshots', (tester) async {
    tester.view.physicalSize = const Size(1080, 1920);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);

    final progress = PlayerProgress.memory()..addCoins(48250);
    progress.recordRun(score: 12840, coins: 0);
    // What the scenes below ride, and the garage shows as owned.
    progress
      ..unlockRide('monster')
      ..unlockRide('turbo')
      ..unlockSkin('flames')
      ..unlockTrack(Catalog.track('mountain'));
    final key = GlobalKey();
    await tester.pumpWidget(RepaintBoundary(
      key: key,
      child: BikeRushApp(
        progress: progress,
        store: StoreService(progress, enabled: false),
      ),
    ));
    await loadImages(tester);
    await _frames(tester, 40);
    await _shot(tester, key, 'store_listing/screenshot_8_menu.png');

    await tester.tap(find.text('PLAY'));
    await _frames(tester, 10);
    final game = gameOf(tester);

    stageRace(game);
    await _frames(tester, 1);
    game.texts.clear();
    await _frames(tester, 1);
    await _shot(tester, key, 'store_listing/screenshot_1_race.png');

    stageRace(game);
    game.nitro = 1;
    game.activateBoost();
    await _frames(tester, 25);
    game.texts.clear();
    await _frames(tester, 1);
    await _shot(tester, key, 'store_listing/screenshot_4_nitro.png');

    // The monster truck on infinity nitro, smashing a car.
    await _rideNext(tester, game, () {
      progress.select(Catalog.ride('monster'));
    });
    stageRace(game);
    game
      ..level = 5
      ..distance = 3000
      ..score = 15360
      ..coins = 84
      ..activateBoost()
      ..vehicles.add(Vehicle(
          lane: 1,
          y: game.playerY - 20,
          speed: 250,
          color: const Color(0xFFFFEE58)));
    await _frames(tester, 8);
    await _shot(tester, key, 'store_listing/screenshot_2_monster_truck.png');

    // The Mountain Pass in the Turbo GT, with flames on.
    await _rideNext(tester, game, () {
      progress
        ..select(Catalog.ride('turbo'))
        ..selectSkin(Catalog.skin('flames'))
        ..selectTrack(Catalog.track('mountain'));
    });
    stageMountain(game);
    await _frames(tester, 2);
    await _shot(tester, key, 'store_listing/screenshot_3_mountain.png');

    game.goToMenu();
    progress
      ..selectSkin(Catalog.skins.first)
      ..selectTrack(Catalog.tracks.first);
    await _frames(tester, 5);
    await tester.tap(find.text('GARAGE'));
    await _frames(tester, 30);
    await _shot(tester, key, 'store_listing/screenshot_7_bikes.png');
    progress.selectSkin(Catalog.skin('flames'));
    // Long enough for the tap's highlight on the tab to fade.
    await tester.tap(find.text('CARS'));
    await _frames(tester, 90);
    await _shot(tester, key, 'store_listing/screenshot_5_cars.png');
    await tester.tap(find.text('SKINS'));
    await _frames(tester, 90);
    await _shot(tester, key, 'store_listing/screenshot_6_skins.png');
  });
}

/// Back to the menu, [pick] what to ride, and PLAY.
Future<void> _rideNext(
    WidgetTester tester, BikeGame game, VoidCallback pick) async {
  game.goToMenu();
  pick();
  await _frames(tester, 2);
  await tester.tap(find.text('PLAY'));
  await _frames(tester, 10);
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
    File(path)
      ..createSync(recursive: true)
      ..writeAsBytesSync(await png24(image));
  });
}

/// Draws a picture with [draw] and saves it. Without [alpha], as a 24-bit
/// PNG, which is what Google Play wants for listing graphics.
Future<void> _save(String path, Size size, void Function(Canvas, Size) draw,
    {bool alpha = true}) async {
  final recorder = ui.PictureRecorder();
  draw(Canvas(recorder), size);
  final image = await recorder
      .endRecording()
      .toImage(size.width.round(), size.height.round());
  final bytes = alpha
      ? (await image.toByteData(format: ui.ImageByteFormat.png))!
          .buffer
          .asUint8List()
      : await png24(image);
  File(path)
    ..createSync(recursive: true)
    ..writeAsBytesSync(bytes);
}

// ------------------------------------------------------------------- art

/// The logo the icons are made from.
const _logoPath = 'assets/icon/logo_source.png';

/// The square of the logo that holds its whole picture: inside the rounded
/// frame, which the launcher's own mask takes the place of, and clear of
/// the mark in the corner outside it.
const _logoArt = Rect.fromLTWH(57, 50, 735, 735);

/// The inside of the logo's rounded frame, as a square: left and right it
/// reaches a little way under the frame, top and bottom it stops at it.
const _logoInside = Rect.fromLTWH(22, 14, 806, 806);

Future<ui.Image> _loadLogo() async {
  final codec =
      await ui.instantiateImageCodec(File(_logoPath).readAsBytesSync());
  return (await codec.getNextFrame()).image;
}

/// Fills the square with the inside of the [logo]'s frame, at full size.
/// Where the frame curves in at the corners, the picture's own colours
/// there carry on instead.
void _paintFullLogo(Canvas c, Size s, ui.Image logo) {
  final all = Offset.zero & s;
  c.drawRect(
    all,
    Paint()
      ..shader = const LinearGradient(
        begin: Alignment.topCenter,
        end: Alignment.bottomCenter,
        colors: [Color(0xFF285486), Color(0xFF1A2B59)],
      ).createShader(all),
  );
  // The inside of the frame, a few pixels in from it: 821 x 806 with
  // corners of radius 116 in the logo.
  final k = s.width / _logoInside.width;
  c.save();
  c.clipRRect(RRect.fromRectAndRadius(
    Rect.fromLTRB(-8 * k, 3 * k, s.width + 7 * k, s.height - 3 * k),
    Radius.circular(113 * k),
  ));
  c.drawImageRect(
      logo, _logoInside, all, Paint()..filterQuality = FilterQuality.high);
  c.restore();
}

/// The picture in the [logo], cut out of its frame.
Future<ui.Image> _logoArtwork(ui.Image logo) async {
  final recorder = ui.PictureRecorder();
  Canvas(recorder).drawImageRect(logo, _logoArt, Offset.zero & _logoArt.size,
      Paint()..filterQuality = FilterQuality.high);
  return recorder
      .endRecording()
      .toImage(_logoArt.width.round(), _logoArt.height.round());
}

/// Draws [art] in the middle of the icon, [scale] times its width. The
/// colours at the art's edges carry on out to the icon's, so the square is
/// filled without a seam.
void _paintLogo(Canvas c, Size s, ui.Image art, double scale) {
  final side = s.width * scale;
  final k = side / art.width;
  final matrix = Float64List.fromList([
    k, 0, 0, 0, //
    0, k, 0, 0,
    0, 0, 1, 0,
    (s.width - side) / 2, (s.height - side) / 2, 0, 1,
  ]);
  c.drawRect(
    Offset.zero & s,
    Paint()
      ..shader = ui.ImageShader(art, TileMode.clamp, TileMode.clamp, matrix,
          filterQuality: FilterQuality.high),
  );
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
  paintRide(c, Catalog.bikes.first, boosting: true, time: 0.4);
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
