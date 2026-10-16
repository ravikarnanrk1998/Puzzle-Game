import 'dart:math';

import 'package:bike/game/bike_game.dart';
import 'package:bike/game/game_painter.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
import 'package:bike/ui/game_screen.dart';
import 'package:bike/ui/overlays.dart';
import 'package:bike/ui/track_preview.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:in_app_purchase/in_app_purchase.dart';

BikeGame newGame() =>
    BikeGame(random: Random(1))..resize(const Size(400, 800));

Vehicle carOnPlayer(BikeGame g) => Vehicle(
      lane: g.targetLane,
      y: g.playerY,
      speed: g.speed,
      color: Colors.red,
    );

PurchaseDetails purchase(String productId, String orderId) => PurchaseDetails(
      purchaseID: orderId,
      productID: productId,
      verificationData: PurchaseVerificationData(
        localVerificationData: '',
        serverVerificationData: orderId,
        source: 'test',
      ),
      transactionDate: '0',
      status: PurchaseStatus.purchased,
    );

void main() {
  group('BikeGame', () {
    test('starts in the menu and begins a run with full lives', () {
      final g = newGame();
      expect(g.phase, GamePhase.menu);
      g.startGame();
      expect(g.phase, GamePhase.playing);
      expect(g.lives, BikeGame.startLives);
    });

    test('steering stays on the road', () {
      final g = newGame()..startGame();
      for (var i = 0; i < 10; i++) {
        g.steer(-1);
      }
      expect(g.targetLane, 0);
      for (var i = 0; i < 10; i++) {
        g.steer(1);
      }
      expect(g.targetLane, BikeGame.laneCount - 1);
    });

    test('a lane change is done within a fifth of a second', () {
      final g = newGame()..startGame();
      g.steer(1);
      for (var i = 0; i < 12; i++) {
        g.update(1 / 60);
      }
      expect(g.playerLane, 2.0);
    });

    test('every 10 levels completed in a run is worth a diamond', () {
      final g = newGame()..startGame();
      var gifts = 0;
      g.onDiamondEarned = () => gifts++;
      // Rides to just short of each level's end, then one more frame.
      void rideTo(int level) {
        g.distance = BikeGame.levelDistance * (level - 1) - 0.01;
        g.update(1 / 60);
        expect(g.level, level);
      }

      rideTo(10);
      expect(gifts, 0, reason: 'level 10 reached but not completed yet');
      rideTo(11);
      expect(gifts, 1);
      rideTo(15);
      expect(gifts, 1);
      rideTo(21);
      expect(gifts, 2);
      expect(g.diamondsEarned, 2);
      g.startGame();
      expect(g.diamondsEarned, 0, reason: 'counted per run');
    });

    test('levels earn no diamonds when level diamonds are off', () {
      final g = newGame()
        ..levelDiamonds = false
        ..startGame();
      var gifts = 0;
      g.onDiamondEarned = () => gifts++;
      g.distance = BikeGame.levelDistance * 20 - 0.01;
      g.update(1 / 60);
      expect(g.level, 21);
      expect(gifts, 0);
      expect(g.diamondsEarned, 0);
    });

    test('riding advances distance and score', () {
      final g = newGame()..startGame();
      for (var i = 0; i < 60; i++) {
        g.update(1 / 60);
      }
      expect(g.distance, greaterThan(0));
      expect(g.scoreValue, greaterThan(0));
    });

    test('hitting a car costs a life and gives brief invincibility', () {
      final g = newGame()..startGame();
      g.vehicles.add(carOnPlayer(g));
      g.update(1 / 60);
      expect(g.lives, BikeGame.startLives - 1);
      expect(g.invincibleTimer, greaterThan(0));
    });

    test('losing the last life ends the run and reports it once', () {
      final g = newGame()..startGame();
      var reports = 0;
      g.onGameOver = () => reports++;
      g.lives = 1;
      g.vehicles.add(carOnPlayer(g));
      g.update(1 / 60);
      expect(g.phase, GamePhase.crashing);
      for (var i = 0; i < 120; i++) {
        g.update(1 / 60);
      }
      expect(g.phase, GamePhase.gameOver);
      expect(reports, 1);
    });

    test('a run can be continued only once', () {
      final g = newGame()..startGame();
      g.lives = 1;
      g.vehicles.add(carOnPlayer(g));
      for (var i = 0; i < 120; i++) {
        g.update(1 / 60);
      }
      expect(g.canRevive, isTrue);
      g.revive();
      expect(g.phase, GamePhase.playing);
      expect(g.lives, 1);
      expect(g.vehicles, isEmpty);

      g.invincibleTimer = 0;
      g.vehicles.add(carOnPlayer(g));
      for (var i = 0; i < 120; i++) {
        g.update(1 / 60);
      }
      expect(g.phase, GamePhase.gameOver);
      expect(g.canRevive, isFalse);
    });

    test('boosting smashes cars instead of crashing', () {
      final g = newGame()..startGame();
      g.nitro = 1;
      g.activateBoost();
      expect(g.boosting, isTrue);
      g.vehicles.add(carOnPlayer(g));
      g.update(1 / 60);
      expect(g.lives, BikeGame.startLives);
      expect(g.vehicles, isEmpty);
    });

    test('boost needs enough nitro', () {
      final g = newGame()..startGame();
      g.nitro = 0.1;
      g.activateBoost();
      expect(g.boosting, isFalse);
    });

    test('riding over a coin collects it', () {
      final g = newGame()..startGame();
      g.pickups.add(Pickup(PickupKind.coin, g.targetLane, g.playerY));
      g.update(1 / 60);
      expect(g.coins, 1);
    });

    test('premium bike perks apply at the start of a run', () {
      final g = newGame()..bike = Catalog.bike('neon');
      g.startGame();
      expect(g.lives, 5);
      expect(g.shieldTimer, greaterThan(0));
    });
  });

  group('Stage 2', () {
    final mountain = Catalog.track('mountain');

    BikeGame mountainGame() => newGame()
      ..setTrack(mountain)
      ..startGame();

    test('each stage has its own scenery', () {
      final g = newGame();
      expect(g.scenery.map((s) => s.kind),
          everyElement(isIn(const [SceneryKind.tree, SceneryKind.bush,
              SceneryKind.flowers])));
      g.setTrack(mountain);
      expect(g.scenery, isNotEmpty);
      expect(g.scenery.map((s) => s.kind),
          everyElement(isIn(const [SceneryKind.pine, SceneryKind.boulder,
              SceneryKind.pebbles])));
    });

    test('obstacles are only on the mountain, and every row leaves a lane',
        () {
      final highway = newGame()..startGame();
      final g = mountainGame();
      // Late levels, where rows are the fullest.
      highway.distance = g.distance = BikeGame.levelDistance * 9;
      var rows = 0;
      final seen = <Obstacle>{};
      for (var i = 0; i < 60 * 60; i++) {
        g.invincibleTimer = highway.invincibleTimer = 10;
        g.update(1 / 60);
        highway.update(1 / 60);
        expect(highway.obstacles, isEmpty);
        // A row is laid all at once: what is new this frame.
        final row = [for (final o in g.obstacles) if (seen.add(o)) o];
        if (row.isEmpty) continue;
        rows++;
        final blocked = {
          for (final o in row)
            if (o.kind != ObstacleKind.ramp) ...{o.lane, o.lastLane},
        };
        expect(blocked.length, lessThan(BikeGame.laneCount));
      }
      expect(rows, greaterThan(20));
    });

    test('a rock costs a life, but boosting smashes it', () {
      final g = mountainGame();
      g.obstacles.add(Obstacle(ObstacleKind.rock, g.targetLane, g.playerY));
      g.update(1 / 60);
      expect(g.lives, BikeGame.startLives - 1);

      final b = mountainGame()
        ..nitro = 1
        ..activateBoost();
      b.obstacles.add(Obstacle(ObstacleKind.barrier, 0, b.playerY));
      b.update(1 / 60);
      expect(b.lives, BikeGame.startLives);
      expect(b.obstacles, isEmpty);
    });

    test('oil skids the bike a lane over and it cannot steer for a moment',
        () {
      final g = mountainGame();
      g.obstacles.add(Obstacle(ObstacleKind.oil, 1, g.playerY));
      g.update(1 / 60);
      expect(g.targetLane, isNot(1));
      expect(g.skidTimer, greaterThan(0));
      final lane = g.targetLane;
      g.steer(lane == 0 ? 1 : -1);
      expect(g.targetLane, lane);
      for (var i = 0; i < 40; i++) {
        g.update(1 / 60);
      }
      g.steer(lane == 0 ? 1 : -1);
      expect(g.targetLane, isNot(lane), reason: 'the tyres grip again');
    });

    test('a pothole slows the bike down', () {
      final g = mountainGame();
      for (var i = 0; i < 60; i++) {
        g.update(1 / 60);
      }
      final before = g.speed;
      g.obstacles.add(Obstacle(ObstacleKind.pothole, 1, g.playerY));
      g.update(1 / 60);
      expect(g.speed, lessThan(before * 0.7));
      expect(g.lives, BikeGame.startLives);
    });

    test('a ramp flies the bike over a rock', () {
      final g = mountainGame();
      g.obstacles.add(Obstacle(ObstacleKind.ramp, 1, g.playerY));
      g.update(1 / 60);
      expect(g.air, greaterThan(0));
      g.obstacles.add(Obstacle(ObstacleKind.rock, 1, g.playerY));
      for (var i = 0; i < 60; i++) {
        g.update(1 / 60);
      }
      expect(g.airTimer, 0, reason: 'landed');
      expect(g.lives, BikeGame.startLives);
    });

    test('completing level 20 on stage 1 earns stage 2', () {
      final g = newGame()..startGame();
      final earned = <Track>[];
      g.onTrackEarned = (track) {
        earned.add(track);
        return true;
      };
      g.distance = BikeGame.levelDistance * 19 - 0.01;
      g.update(1 / 60);
      expect(g.level, 20);
      expect(earned, isEmpty, reason: 'level 20 reached but not completed');
      g.distance = BikeGame.levelDistance * 20 - 0.01;
      g.update(1 / 60);
      expect(g.level, 21);
      expect(earned, [mountain]);
    });
  });

  group('Stages 3 to 6', () {
    BikeGame gameOn(String id) => newGame()
      ..setTrack(Catalog.track(id))
      ..startGame();

    test('each unlocks by completing level 10 on the stage before it', () {
      for (final (before, next) in [
        ('mountain', 'highway_oncoming'),
        ('highway_oncoming', 'mountain_oncoming'),
        ('mountain_oncoming', 'two_way'),
        ('two_way', 'mountain_two_way'),
      ]) {
        final g = gameOn(before);
        final earned = <Track>[];
        g.onTrackEarned = (track) {
          earned.add(track);
          return true;
        };
        g.distance = BikeGame.levelDistance * 10 - 0.01;
        g.update(1 / 60);
        expect(g.level, 11);
        expect(earned, [Catalog.track(next)]);
      }
    });

    test('they are unlocked straight away with diamonds, not coins', () {
      for (final track in Catalog.tracks.skip(2)) {
        final p = PlayerProgress.memory()
          ..addCoins(1000000)
          ..addDiamonds(track.diamondPrice - 1);
        expect(p.buyTrack(track), isFalse);
        expect(p.buyTrack(track, withDiamonds: true), isFalse);
        p.addDiamonds(1);
        expect(p.buyTrack(track, withDiamonds: true), isTrue);
        expect(p.diamonds, 0);
        expect(p.selectedTrack, track);
      }
    });

    test('on stages 3 and 4 all traffic comes at the bike, faster than the '
        'road', () {
      for (final id in ['highway_oncoming', 'mountain_oncoming']) {
        final g = gameOn(id);
        var moves = 0;
        for (var i = 0; i < 60 * 30; i++) {
          g.invincibleTimer = 10;
          final before = {for (final v in g.vehicles) v: v.y};
          g.update(1 / 60);
          for (final v in g.vehicles) {
            expect(v.oncoming, isTrue);
            final y = before[v];
            if (y == null) continue;
            moves++;
            expect(v.y - y, greaterThanOrEqualTo(g.speed / 60 - 1e-6));
          }
        }
        expect(moves, greaterThan(0), reason: 'traffic on $id');
      }
    });

    test('oncoming cars pull up behind rocks instead of driving through them',
        () {
      for (final id in ['mountain_oncoming', 'mountain_two_way']) {
        final g = gameOn(id);
        // Late levels, where rows are the fullest.
        g.distance = BikeGame.levelDistance * 9;
        var oncoming = 0;
        for (var i = 0; i < 60 * 60; i++) {
          g.invincibleTimer = 10;
          g.update(1 / 60);
          for (final v in g.vehicles) {
            if (v.oncoming) oncoming++;
            for (final o in g.obstacles) {
              if (!o.blocks || !o.covers(v.lane)) continue;
              expect((v.y - o.y).abs(), greaterThan((v.height + o.height) / 2),
                  reason: 'a car drove into a ${o.kind.name} on $id');
            }
          }
        }
        expect(oncoming, greaterThan(0), reason: 'oncoming traffic on $id');
      }
    });

    test('on stages 5 and 6 the left lanes go the way of the bike and the '
        'right lanes come at it', () {
      for (final id in ['two_way', 'mountain_two_way']) {
        final g = gameOn(id);
        // Cars change lanes from level 2.
        g.distance = BikeGame.levelDistance * 5;
        final kinds = <bool>{};
        for (var i = 0; i < 60 * 60; i++) {
          g.invincibleTimer = 10;
          g.update(1 / 60);
          for (final v in g.vehicles) {
            kinds.add(v.oncoming);
            expect(v.oncoming, v.lane >= 2);
            expect(v.x.round() >= 2, v.oncoming, reason: 'stays on its side');
          }
        }
        expect(kinds, {true, false}, reason: 'both ways on $id');
      }
    });

    test('stage 6 is the mountain road, with obstacles', () {
      final g = gameOn('mountain_two_way');
      g.distance = BikeGame.levelDistance * 3;
      for (var i = 0; i < 60 * 10; i++) {
        g.invincibleTimer = 10;
        g.update(1 / 60);
      }
      expect(g.obstacles, isNotEmpty);
      expect(g.scenery.map((s) => s.kind),
          everyElement(isIn(const [SceneryKind.pine, SceneryKind.boulder,
              SceneryKind.pebbles])));
    });
  });

  group('PlayerProgress', () {
    test('banks run coins and tracks the best score', () {
      final p = PlayerProgress.memory();
      final reward = p.recordRun(score: 900, coins: 40);
      expect(reward.coinsEarned, 40);
      expect(reward.newBest, isTrue);
      expect(p.wallet, 40);
      expect(p.recordRun(score: 100, coins: 0).newBest, isFalse);
      expect(p.best, 900);
    });

    test('double coins doubles run earnings', () {
      final p = PlayerProgress.memory()..unlockDoubleCoins();
      expect(p.recordRun(score: 1, coins: 30).coinsEarned, 60);
    });

    test('bikes can be bought with enough coins only', () {
      final p = PlayerProgress.memory();
      final blue = Catalog.bike('blue');
      expect(p.buyWithCoins(blue), isFalse);
      p.addCoins(blue.coinPrice);
      expect(p.buyWithCoins(blue), isTrue);
      expect(p.wallet, 0);
      expect(p.selected, 'blue');
      expect(p.buyWithCoins(Catalog.bike('gold')), isFalse,
          reason: 'premium bikes need real money');
    });

    test('the free diamond can be claimed once a day', () {
      final p = PlayerProgress.memory();
      final morning = DateTime(2026, 10, 12, 9);
      expect(p.canClaimDaily(morning), isTrue);
      expect(p.claimDaily(morning), isTrue);
      expect(p.diamonds, 1);
      expect(p.claimDaily(DateTime(2026, 10, 12, 23, 59)), isFalse,
          reason: 'still the same day');
      expect(p.diamonds, 1);
      expect(p.claimDaily(DateTime(2026, 10, 13, 0, 5)), isTrue);
      expect(p.diamonds, 2);
    });

    test('stage 2 opens by playing, or for coins or diamonds', () {
      final mountain = Catalog.track('mountain');
      final p = PlayerProgress.memory();
      expect(p.hasTrack(Catalog.tracks.first), isTrue);
      expect(p.hasTrack(mountain), isFalse);
      p.selectTrack(mountain);
      expect(p.selectedTrack, Catalog.tracks.first, reason: 'still locked');
      expect(p.buyTrack(mountain), isFalse);
      p.addCoins(mountain.coinPrice);
      expect(p.buyTrack(mountain), isTrue);
      expect(p.wallet, 0);
      expect(p.selectedTrack, mountain);
      expect(p.unlockTrack(mountain), isFalse, reason: 'already open');

      final q = PlayerProgress.memory()..addDiamonds(mountain.diamondPrice - 1);
      expect(q.buyTrack(mountain, withDiamonds: true), isFalse);
      q.addDiamonds(1);
      expect(q.buyTrack(mountain, withDiamonds: true), isTrue);
      expect(q.diamonds, 0);

      final r = PlayerProgress.memory();
      expect(r.unlockTrack(mountain), isTrue);
      expect(r.hasTrack(mountain), isTrue);
      expect(r.selectedTrack, Catalog.tracks.first,
          reason: 'earning it does not switch tracks by itself');
    });

    test('diamonds can only be spent by a player who has them', () {
      final p = PlayerProgress.memory();
      expect(p.spendDiamonds(1), isFalse);
      p.addDiamonds(10);
      expect(p.spendDiamonds(1), isTrue);
      expect(p.diamonds, 9);
    });
  });

  group('StoreService', () {
    test('coin packs are credited once per order', () async {
      final p = PlayerProgress.memory();
      final store = StoreService(p, enabled: false);
      final order = purchase('coins_small', 'GPA.1');
      await store.handlePurchases([order]);
      await store.handlePurchases([order]);
      expect(p.wallet, Catalog.coinPack('coins_small')!.coins);
      await store.handlePurchases([purchase('coins_small', 'GPA.2')]);
      expect(p.wallet, 2 * Catalog.coinPack('coins_small')!.coins);
    });

    test('diamond packs are credited once per order', () async {
      final p = PlayerProgress.memory();
      final store = StoreService(p, enabled: false);
      final order = purchase('diamonds_20', 'GPA.5');
      await store.handlePurchases([order]);
      await store.handlePurchases([order]);
      expect(p.diamonds, 20);
      expect(p.wallet, 0);
      expect(Catalog.diamondPacks.map((pack) => pack.diamonds),
          [1, 10, 20, 50, 100]);
    });

    test('premium purchases unlock bikes and double coins', () async {
      final p = PlayerProgress.memory();
      final store = StoreService(p, enabled: false);
      await store.handlePurchases([
        purchase('bike_gold', 'GPA.3'),
        purchase(Catalog.doubleCoinsId, 'GPA.4'),
      ]);
      expect(p.owns(Catalog.bike('gold')), isTrue);
      expect(p.doubleCoins, isTrue);
    });
  });

  testWidgets('PLAY starts the game', (tester) async {
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    expect(find.text('BIKE RUSH'), findsOneWidget);
    await tester.tap(find.text('PLAY'));
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('SCORE'), findsOneWidget);
  });

  testWidgets('lanes change by tapping a side or by swiping', (tester) async {
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    await tester.tap(find.text('PLAY'));
    await tester.pump(const Duration(milliseconds: 100));
    final road = tester.widget<CustomPaint>(find.byWidgetPredicate(
        (w) => w is CustomPaint && w.painter is GamePainter));
    final game = (road.painter! as GamePainter).game;
    final screen = tester.getSize(find.byType(GameScreen));
    final left = Offset(screen.width * 0.25, screen.height / 2);
    final right = Offset(screen.width * 0.75, screen.height / 2);
    expect(game.targetLane, 1);

    await tester.tapAt(right);
    expect(game.targetLane, 2);
    await tester.tapAt(left);
    expect(game.targetLane, 1);

    // A swipe goes the way it points, whichever side it starts on, and
    // moves one lane only.
    await tester.dragFrom(right, const Offset(-120, 0));
    expect(game.targetLane, 0);
    await tester.dragFrom(left, const Offset(120, 10));
    expect(game.targetLane, 1);

    await tester.dragFrom(right, const Offset(5, -120));
    expect(game.targetLane, 1, reason: 'swiping up is not a lane change');

    // A finger held still steers without waiting for it to lift.
    final hold = await tester.startGesture(right);
    await tester.pump(const Duration(milliseconds: 40));
    expect(game.targetLane, 1, reason: 'too soon to tell tap from swipe');
    await tester.pump(const Duration(milliseconds: 40));
    expect(game.targetLane, 2);

    // Turning it into a slow swipe the other way takes the tap back.
    await hold.moveBy(const Offset(-60, 0));
    expect(game.targetLane, 0);
    await hold.up();
    expect(game.targetLane, 0, reason: 'lifting after a swipe is no tap');
  });

  testWidgets('completing level 10 adds a diamond to the player right away',
      (tester) async {
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    await tester.tap(find.text('PLAY'));
    await tester.pump(const Duration(milliseconds: 100));
    final road = tester.widget<CustomPaint>(find.byWidgetPredicate(
        (w) => w is CustomPaint && w.painter is GamePainter));
    final game = (road.painter! as GamePainter).game;

    game.distance = BikeGame.levelDistance * 10 - 0.01;
    await tester.pump(const Duration(milliseconds: 20));
    expect(game.level, 11);
    expect(progress.diamonds, 1, reason: 'given mid-run, not at the end');
  });

  testWidgets('stage 2 is bought on the menu and ridden next', (tester) async {
    final mountain = Catalog.track('mountain');
    final progress = PlayerProgress.memory()..addCoins(mountain.coinPrice);
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    final locked = Catalog.tracks.length - 1;
    expect(find.text('TAP TO UNLOCK'), findsNWidgets(locked));
    await tester.tap(find.text('Mountain Pass'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Stage 2: Mountain Pass'), findsOneWidget);
    await tester.tap(find.text('100,000 COINS'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(progress.wallet, 0);
    expect(progress.selectedTrack, mountain);
    expect(find.text('TAP TO UNLOCK'), findsNWidgets(locked - 1));
    // Let the "unlocked" message finish showing.
    await tester.pump(const Duration(seconds: 5));
    await tester.pump(const Duration(seconds: 1));

    await tester.tap(find.text('PLAY'));
    await tester.pump(const Duration(milliseconds: 100));
    final road = tester.widget<CustomPaint>(find.byWidgetPredicate(
        (w) => w is CustomPaint && w.painter is GamePainter));
    expect((road.painter! as GamePainter).game.track, mountain);
  });

  testWidgets('the menu fits a small phone without scrolling, with how to '
      'play behind the info button', (tester) async {
    tester.view
      ..physicalSize = const Size(360 * 3, 640 * 3)
      ..devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    await tester.pump();
    final scrolls = tester.widgetList<Scrollable>(find.descendant(
        of: find.byType(MenuOverlay), matching: find.byType(Scrollable)));
    expect(scrolls.map((s) => s.axisDirection), [AxisDirection.right],
        reason: 'only the row of stages scrolls');
    expect(find.byType(TrackPreview), findsNWidgets(Catalog.tracks.length));

    expect(find.text('HOW TO PLAY'), findsNothing);
    await tester.tap(find.byTooltip('How to play'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('HOW TO PLAY'), findsOneWidget);
    expect(find.textContaining('Stage 5: Two-Way Road'), findsOneWidget);
    await tester.tap(find.text('GOT IT'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('HOW TO PLAY'), findsNothing);
  });

  testWidgets('GARAGE opens the shop', (tester) async {
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
    ));
    await tester.tap(find.text('GARAGE'));
    for (var i = 0; i < 20; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }
    expect(find.text('Red Rocket'), findsOneWidget);
    expect(find.text('RIDING'), findsOneWidget);
  });
}
