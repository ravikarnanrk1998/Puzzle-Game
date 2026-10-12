import 'package:flutter_test/flutter_test.dart';
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
}
