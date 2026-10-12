import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:in_app_purchase/in_app_purchase.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Product IDs. Each must match a product created in Google Play Console
/// (Monetize > In-app products) / App Store Connect, character for character.
class StoreIds {
  static const premium = 'premium_pack';
  static const hammers10 = 'hammers_10';
  static const hammers50 = 'hammers_50';
  static const hints10 = 'hints_10';
  static const hints50 = 'hints_50';

  static const all = {premium, hammers10, hammers50, hints10, hints50};
}

enum StoreItem { hammer, hint }

/// What each consumable product grants when bought.
const Map<String, (StoreItem, int)> kConsumableGrants = {
  StoreIds.hammers10: (StoreItem.hammer, 10),
  StoreIds.hammers50: (StoreItem.hammer, 50),
  StoreIds.hints10: (StoreItem.hint, 10),
  StoreIds.hints50: (StoreItem.hint, 50),
};

/// Owns in-app purchases and the player's purchased stock (premium flag,
/// hammers, hints). Listen to it to rebuild when anything changes.
class StoreService extends ChangeNotifier {
  StoreService._();
  static final StoreService instance = StoreService._();

  static const _premiumKey = 'store_premium';
  static const _hammersKey = 'store_hammers';
  static const _hintsKey = 'store_hints';
  static const _deliveredKey = 'store_delivered_ids';

  bool isPremium = false;
  int hammers = 0;
  int hints = 0;

  bool storeAvailable = false;
  bool loadingProducts = true;
  bool purchasePending = false;
  String? lastError;
  List<ProductDetails> products = [];

  late SharedPreferences _prefs;
  List<String> _deliveredIds = [];
  StreamSubscription<List<PurchaseDetails>>? _sub;

  /// in_app_purchase only has Google Play and StoreKit implementations.
  static bool get platformSupported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS ||
          defaultTargetPlatform == TargetPlatform.macOS);

  int get freeHammersPerGame => isPremium ? 5 : 3;
  int get freeHintsPerPuzzle => isPremium ? 5 : 3;

  /// Reads the saved entitlements. Fast; await it before runApp.
  Future<void> loadLocal() async {
    _prefs = await SharedPreferences.getInstance();
    isPremium = _prefs.getBool(_premiumKey) ?? false;
    hammers = _prefs.getInt(_hammersKey) ?? 0;
    hints = _prefs.getInt(_hintsKey) ?? 0;
    _deliveredIds = _prefs.getStringList(_deliveredKey) ?? [];
  }

  /// Connects to the store. Must start at app launch so purchases that
  /// complete later (e.g. pending cash payments) are still delivered.
  Future<void> connect() async {
    if (!platformSupported) {
      loadingProducts = false;
      notifyListeners();
      return;
    }
    final iap = InAppPurchase.instance;
    _sub ??= iap.purchaseStream.listen(
      handlePurchases,
      onError: (Object e) {
        lastError = 'Store error: $e';
        notifyListeners();
      },
    );
    storeAvailable = await iap.isAvailable();
    if (storeAvailable) {
      await _loadProducts();
      // Silently re-sync Premium after a reinstall. Not done on iOS, where
      // restoring can prompt for an Apple ID sign-in.
      if (defaultTargetPlatform == TargetPlatform.android) {
        await iap.restorePurchases();
      }
    }
    loadingProducts = false;
    notifyListeners();
  }

  Future<void> _loadProducts() async {
    final response = await InAppPurchase.instance.queryProductDetails(
      StoreIds.all,
    );
    if (response.error != null) {
      lastError = response.error!.message;
    }
    products = response.productDetails
      ..sort((a, b) => a.rawPrice.compareTo(b.rawPrice));
  }

  Future<void> retry() async {
    lastError = null;
    loadingProducts = true;
    notifyListeners();
    await connect();
  }

  ProductDetails? product(String id) {
    for (final p in products) {
      if (p.id == id) return p;
    }
    return null;
  }

  Future<void> buy(ProductDetails product) async {
    lastError = null;
    notifyListeners();
    final param = PurchaseParam(productDetails: product);
    try {
      if (product.id == StoreIds.premium) {
        await InAppPurchase.instance.buyNonConsumable(purchaseParam: param);
      } else {
        await InAppPurchase.instance.buyConsumable(purchaseParam: param);
      }
    } on PlatformException catch (e) {
      lastError = e.message ?? 'Could not start the purchase.';
      notifyListeners();
    }
  }

  Future<void> restore() async {
    lastError = null;
    notifyListeners();
    await InAppPurchase.instance.restorePurchases();
  }

  @visibleForTesting
  Future<void> handlePurchases(List<PurchaseDetails> purchases) async {
    for (final purchase in purchases) {
      if (purchase.status == PurchaseStatus.pending) {
        purchasePending = true;
        continue;
      }
      purchasePending = false;
      switch (purchase.status) {
        case PurchaseStatus.purchased:
        case PurchaseStatus.restored:
          _deliver(purchase);
          break;
        case PurchaseStatus.error:
          lastError = purchase.error?.message ?? 'Purchase failed.';
          break;
        case PurchaseStatus.canceled:
        case PurchaseStatus.pending:
          break;
      }
      if (purchase.pendingCompletePurchase) {
        await InAppPurchase.instance.completePurchase(purchase);
      }
    }
    notifyListeners();
  }

  void _deliver(PurchaseDetails purchase) {
    if (purchase.productID == StoreIds.premium) {
      isPremium = true;
      _prefs.setBool(_premiumKey, true);
      return;
    }

    final grant = kConsumableGrants[purchase.productID];
    if (grant == null) return;
    // The store can report the same purchase again (e.g. the app closed
    // before it was completed), so grant each purchase ID only once.
    final id = purchase.purchaseID;
    if (id != null) {
      if (_deliveredIds.contains(id)) return;
      _deliveredIds.add(id);
      if (_deliveredIds.length > 200) {
        _deliveredIds.removeRange(0, _deliveredIds.length - 200);
      }
      _prefs.setStringList(_deliveredKey, _deliveredIds);
    }
    final (item, amount) = grant;
    _addStock(item, amount);
  }

  void _addStock(StoreItem item, int amount) {
    if (item == StoreItem.hammer) {
      hammers += amount;
      _prefs.setInt(_hammersKey, hammers);
    } else {
      hints += amount;
      _prefs.setInt(_hintsKey, hints);
    }
  }

  /// Spends one purchased hammer. Returns false if there are none.
  bool useHammer() => _spend(StoreItem.hammer);

  /// Spends one purchased hint. Returns false if there are none.
  bool useHint() => _spend(StoreItem.hint);

  bool _spend(StoreItem item) {
    final have = item == StoreItem.hammer ? hammers : hints;
    if (have <= 0) return false;
    _addStock(item, -1);
    notifyListeners();
    return true;
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }
}
