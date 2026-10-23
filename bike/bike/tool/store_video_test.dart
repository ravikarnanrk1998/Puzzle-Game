// Renders the frames of the Google Play promo video: 1920x1080 landscape,
// 30 frames a second, the game played in the middle with the title on one
// side and a caption for each scene on the other.
//
//   flutter test tool/store_video_test.dart
//   ffmpeg -y -framerate 30 -i build/store_video/f%04d.png
//     -i assets/music/background-music.mp3 -map 0:v -map 1:a
//     -c:v libx264 -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k
//     -af "afade=t=out:st=<length - 2>:d=2" -shortest
//     store_listing/promo_video_1920x1080.mp4
//
// Google Play takes the video as a YouTube link: upload it to YouTube
// (public or unlisted, with ads off) and paste the link into Play Console.
import 'dart:async';
import 'dart:io';

import 'package:bike/game/bike_game.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
import 'package:bike/ui/overlays.dart' show GameTitle;
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';

import 'store_kit.dart';

const _fps = 30;
const _frameTime = Duration(microseconds: 1000000 ~/ _fps);

/// The game is laid out as on a 360 x 640 phone, scaled up to fill the
/// height of the video.
const _scale = 1080 / 640;

typedef _Caption = ({String title, String text});

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(loadFonts);

  testWidgets('promo video frames', (tester) async {
    tester.view
      ..physicalSize = const Size(1920, 1080)
      ..devicePixelRatio = _scale;
    addTearDown(tester.view.reset);
    final dir = Directory('build/store_video');
    if (dir.existsSync()) dir.deleteSync(recursive: true);
    dir.createSync(recursive: true);

    final progress = PlayerProgress.memory()..addCoins(48250);
    progress.recordRun(score: 12840, coins: 0);
    progress
      ..unlockRide('monster')
      ..unlockRide('turbo')
      ..unlockSkin('flames')
      ..unlockTrack(Catalog.track('mountain'));
    final caption = ValueNotifier<_Caption>((
      title: 'RACE THROUGH TRAFFIC',
      text: 'Bikes, cars and a monster truck',
    ));
    final key = GlobalKey();
    await tester.pumpWidget(RepaintBoundary(
      key: key,
      child: _Frame(
        caption: caption,
        child: BikeRushApp(
          progress: progress,
          store: StoreService(progress, enabled: false),
        ),
      ),
    ));
    await loadImages(tester);

    var frame = 0;
    Future<void> record(double seconds,
        [FutureOr<void> Function()? eachFrame]) async {
      for (var i = 0; i < seconds * _fps; i++) {
        await eachFrame?.call();
        await tester.pump(_frameTime);
        final boundary =
            key.currentContext!.findRenderObject()! as RenderRepaintBoundary;
        final path =
            '${dir.path}/f${(frame++).toString().padLeft(4, '0')}.png';
        await tester.runAsync(() async {
          final image = await boundary.toImage(pixelRatio: _scale);
          File(path).writeAsBytesSync(await png24(image));
        });
      }
    }

    /// Back to the menu, [pick] what to ride, and PLAY at [level]: off
    /// camera, and without the controls hint or a level banner, which the
    /// first run has already shown.
    Future<BikeGame> rideNext(int level, VoidCallback pick) async {
      gameOf(tester).goToMenu();
      pick();
      await tester.pump(_frameTime);
      await tester.tap(find.text('PLAY'));
      await tester.pump(_frameTime);
      return gameOf(tester)
        ..playTime = 10
        ..level = level
        ..distance = BikeGame.levelDistance * (level - 1);
    }

    // The menu.
    await record(2.5);

    // Dodging traffic on the Red Rocket.
    caption.value = (
      title: 'DODGE THE TRAFFIC',
      text: 'Tap or swipe to change lanes',
    );
    await tester.tap(find.text('PLAY'));
    final bike = gameOf(tester);
    // Once the stage's name has gone, straight on to level 3, with its
    // banner.
    await record(1.7);
    bike.distance = BikeGame.levelDistance * 2;
    await record(4.8, () => autopilot(bike));

    caption.value = (
      title: 'FIRE THE NITRO',
      text: 'Smash through anything in your way',
    );
    bike
      ..nitro = 1
      ..activateBoost();
    await record(3.5, () => autopilot(bike));

    // The Mountain Pass in the Turbo GT, with flames on.
    caption.value = (
      title: '6 STAGES',
      text: 'Rocks, oil slicks and jump ramps',
    );
    final turbo = await rideNext(3, () {
      progress
        ..select(Catalog.ride('turbo'))
        ..selectSkin(Catalog.skin('flames'))
        ..selectTrack(Catalog.track('mountain'));
    });
    await record(1.2, () => autopilot(turbo));
    // A ramp ahead, to show off a jump.
    turbo.obstacles
        .add(Obstacle(ObstacleKind.ramp, turbo.targetLane, turbo.playerY - 420));
    await record(4.8, () {
      if (turbo.obstacles.any((o) => o.kind == ObstacleKind.ramp && !o.hit)) {
        return;
      }
      autopilot(turbo);
    });

    // The monster truck on infinity nitro.
    caption.value = (
      title: 'MONSTER TRUCK',
      text: 'Infinity nitro: boost for ever!',
    );
    final monster = await rideNext(4, () {
      progress
        ..select(Catalog.ride('monster'))
        ..selectSkin(Catalog.skins.first)
        ..selectTrack(Catalog.tracks.first);
    });
    await record(0.8);
    monster.activateBoost();
    await record(5.5, () => autopilot(monster));

    // The garage.
    caption.value = (
      title: 'NEW CARS & SKINS',
      text: 'Make your ride your own',
    );
    monster.goToMenu();
    progress
      ..select(Catalog.ride('turbo'))
      ..selectSkin(Catalog.skin('flames'));
    await tester.pump(_frameTime);
    await tester.tap(find.text('GARAGE'));
    await record(1.5);
    await tester.tap(find.text('CARS'));
    await record(1.6);
    await tester.tap(find.text('SKINS'));
    await record(1);
    final drag = await tester.startGesture(const Offset(568, 450));
    await record(1.6, () => drag.moveBy(const Offset(0, -9)));
    await drag.up();
    await record(0.4);

    // Back to the menu to finish.
    caption.value = (title: 'PLAY BIKE RUSH', text: 'Free to play');
    await tester.tap(find.byTooltip('Back'));
    await record(2.6);

    // ignore: avoid_print
    print('Rendered $frame frames: ${frame / _fps} seconds.');
  });
}

/// A frame of the video: the [child] game in the middle, as tall as the
/// video, with the title to its left and the [caption] to its right.
class _Frame extends StatelessWidget {
  const _Frame({required this.caption, required this.child});

  final ValueListenable<_Caption> caption;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Material(
        type: MaterialType.transparency,
        child: DecoratedBox(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.centerLeft,
              end: Alignment.centerRight,
              colors: [Color(0xFF0D1020), Color(0xFF311B92), Color(0xFFBF360C)],
            ),
          ),
          child: Row(
            children: [
              const Expanded(
                child: Padding(
                  padding: EdgeInsets.all(28),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      GameTitle(),
                      SizedBox(height: 14),
                      Text(
                        'Dodge traffic • Grab coins • Fire the NITRO',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: Colors.white70,
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              SizedBox(
                width: 360,
                height: 640,
                child: ClipRect(child: child),
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.all(28),
                  child: ValueListenableBuilder(
                    valueListenable: caption,
                    builder: (context, c, _) => AnimatedSwitcher(
                      duration: const Duration(milliseconds: 350),
                      child: _CaptionText(key: ValueKey(c.title), caption: c),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CaptionText extends StatelessWidget {
  const _CaptionText({super.key, required this.caption});

  final _Caption caption;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 56,
          height: 6,
          margin: const EdgeInsets.only(bottom: 14),
          decoration: BoxDecoration(
            color: const Color(0xFFFFC107),
            borderRadius: BorderRadius.circular(3),
          ),
        ),
        Text(
          caption.title,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: Colors.white,
            fontSize: 36,
            fontWeight: FontWeight.w900,
            fontStyle: FontStyle.italic,
            height: 1.05,
          ),
        ),
        const SizedBox(height: 10),
        Text(
          caption.text,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: Colors.white70,
            fontSize: 18,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    );
  }
}
