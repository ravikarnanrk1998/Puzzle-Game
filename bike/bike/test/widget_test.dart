import 'dart:math';

import 'package:bike/game/bike_game.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
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
