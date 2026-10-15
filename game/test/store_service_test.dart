import 'package:flutter_test/flutter_test.dart';
import 'package:game/snake_game.dart';
import 'package:game/store/store_service.dart';
import 'package:in_app_purchase/in_app_purchase.dart';
import 'package:shared_preferences/shared_preferences.dart';

PurchaseDetails _purchase(
  String productId, {
  String? id,
  PurchaseStatus status = PurchaseStatus.purchased,
}) {
  return PurchaseDetails(
    purchaseID: id,
    productID: productId,
    verificationData: PurchaseVerificationData(
      localVerificationData: '',
      serverVerificationData: '',
      source: 'test',
    ),
    transactionDate: null,
    status: status,
  );
}

void main() {
  final store = StoreService.instance;

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    await store.loadLocal();
    store.purchasePending = false;
    store.lastError = null;
  });

  test('buying a hammer pack adds hammers and saves them', () async {
    await store.handlePurchases([_purchase(StoreIds.hammers10, id: 'GPA.1')]);
    expect(store.hammers, 10);

    await store.loadLocal();
    expect(store.hammers, 10);
  });

  test('the same purchase reported twice is only granted once', () async {
    await store.handlePurchases([_purchase(StoreIds.hints50, id: 'GPA.2')]);
    await store.handlePurchases([
      _purchase(StoreIds.hints50, id: 'GPA.2', status: PurchaseStatus.restored),
    ]);
    expect(store.hints, 50);

    await store.handlePurchases([_purchase(StoreIds.hints10, id: 'GPA.3')]);
    expect(store.hints, 60);
  });

  test('premium unlocks and raises free items per game', () async {
    expect(store.isPremium, isFalse);
    expect(store.freeHammersPerGame, 3);

    await store.handlePurchases([_purchase(StoreIds.premium, id: 'GPA.4')]);
    expect(store.isPremium, isTrue);
    expect(store.freeHammersPerGame, 5);
    expect(store.freeHintsPerPuzzle, 5);

    await store.loadLocal();
    expect(store.isPremium, isTrue);
  });

  test('pending payments grant nothing until confirmed', () async {
    await store.handlePurchases([
      _purchase(
        StoreIds.hammers50,
        id: 'GPA.5',
        status: PurchaseStatus.pending,
      ),
    ]);
    expect(store.purchasePending, isTrue);
    expect(store.hammers, 0);

    await store.handlePurchases([_purchase(StoreIds.hammers50, id: 'GPA.5')]);
    expect(store.purchasePending, isFalse);
    expect(store.hammers, 50);
  });

  test('failed and canceled purchases grant nothing', () async {
    await store.handlePurchases([
      _purchase(StoreIds.hammers10, id: 'GPA.6', status: PurchaseStatus.error),
      _purchase(StoreIds.hints10, id: 'GPA.7', status: PurchaseStatus.canceled),
    ]);
    expect(store.hammers, 0);
    expect(store.hints, 0);
    expect(store.lastError, isNotNull);
  });

  test('spending stops at zero', () async {
    await store.handlePurchases([_purchase(StoreIds.hammers10, id: 'GPA.8')]);
    for (int i = 0; i < 10; i++) {
      expect(store.useHammer(), isTrue);
    }
    expect(store.useHammer(), isFalse);
    expect(store.hammers, 0);
    expect(store.useHint(), isFalse);
  });

  test('hammers, hints and diamonds each sell 10/20/30/50/100 packs', () {
    for (final item in StoreItem.values) {
      final sizes = [
        for (final (i, n) in kConsumableGrants.values)
          if (i == item) n,
      ];
      expect(sizes, containsAll([10, 20, 30, 50, 100]), reason: item.name);
    }
    expect(StoreIds.all, containsAll(kConsumableGrants.keys));
  });

  test('every pack grants its amount and it is saved', () async {
    for (final MapEntry(key: id, value: (item, amount))
        in kConsumableGrants.entries) {
      final before = (store.hammers, store.hints, store.diamonds);
      await store.handlePurchases([_purchase(id, id: 'GPA.$id')]);
      final expected = switch (item) {
        StoreItem.hammer => (before.$1 + amount, before.$2, before.$3),
        StoreItem.hint => (before.$1, before.$2 + amount, before.$3),
        StoreItem.diamond => (before.$1, before.$2, before.$3 + amount),
      };
      expect((store.hammers, store.hints, store.diamonds), expected,
          reason: id);
    }
    expect(store.hammers, 210);
    expect(store.hints, 210);
    expect(store.diamonds, 710);

    await store.loadLocal();
    expect((store.hammers, store.hints, store.diamonds), (210, 210, 710));
  });

  test('unlocking costs 10 diamonds once and is saved', () async {
    await store.handlePurchases([_purchase(StoreIds.diamonds20, id: 'GPA.9')]);
    expect(store.isUnlocked(LockIds.sudoku9x9), isFalse);

    expect(store.unlock(LockIds.sudoku9x9), isTrue);
    expect(store.diamonds, 10);
    expect(store.unlock(LockIds.sudoku9x9), isTrue);
    expect(store.diamonds, 10, reason: 'already unlocked, no second charge');

    await store.loadLocal();
    expect(store.isUnlocked(LockIds.sudoku9x9), isTrue);
    expect(store.isUnlocked(LockIds.jigsawHard), isFalse);
  });

  test('unlocking needs enough diamonds', () async {
    await store.handlePurchases([_purchase(StoreIds.diamonds10, id: 'GPA.10')]);
    expect(store.spendDiamonds(StoreService.continueCost), isTrue);
    expect(store.diamonds, 9);

    expect(store.unlock(LockIds.jigsawHard), isFalse);
    expect(store.isUnlocked(LockIds.jigsawHard), isFalse);
    expect(store.diamonds, 9);
  });

  test('locked snake maps unlock with diamonds', () async {
    final echo = kSnakeMaps.firstWhere((m) => m.id == 'echo');
    final classic = kSnakeMaps.firstWhere((m) => m.id == 'classic');
    expect(classic.locked, isFalse);
    expect(echo.locked, isTrue);

    await store.handlePurchases([_purchase(StoreIds.diamonds10, id: 'GPA.11')]);
    expect(store.unlock(LockIds.snakeMap(echo.id)), isTrue);
    expect(echo.locked, isFalse);
    expect(store.diamonds, 0);
  });

  test('premium unlocks every locked level without diamonds', () async {
    await store.handlePurchases([_purchase(StoreIds.premium, id: 'GPA.12')]);
    for (final id in [
      LockIds.sliding5x5,
      LockIds.jigsawHard,
      LockIds.sudoku9x9,
      LockIds.snakeMap('alleys'),
    ]) {
      expect(store.isUnlocked(id), isTrue, reason: id);
    }
    expect(store.diamonds, 0);
  });
}
