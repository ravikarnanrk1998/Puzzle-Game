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
  static const hammers20 = 'hammers_20';
  static const hammers30 = 'hammers_30';
  static const hammers50 = 'hammers_50';
  static const hammers100 = 'hammers_100';
  static const hints10 = 'hints_10';
  static const hints20 = 'hints_20';
  static const hints30 = 'hints_30';
  static const hints50 = 'hints_50';
  static const hints100 = 'hints_100';
  static const diamonds10 = 'diamonds_10';
  static const diamonds20 = 'diamonds_20';
  static const diamonds30 = 'diamonds_30';
  static const diamonds50 = 'diamonds_50';
  static const diamonds100 = 'diamonds_100';
  static const diamonds500 = 'diamonds_500';

  static const all = {
    premium,
    hammers10,
    hammers20,
    hammers30,
    hammers50,
    hammers100,
    hints10,
    hints20,
    hints30,
    hints50,
    hints100,
    diamonds10,
    diamonds20,
    diamonds30,
    diamonds50,
    diamonds100,
    diamonds500,
  };
}

/// Levels and maps that start locked. Each unlocks for good with
/// [StoreService.unlockCost] diamonds, and Premium unlocks them all.
class LockIds {
  static const sliding5x5 = 'sliding_5x5';
  static const jigsawHard = 'jigsaw_hard';
  static const sudoku9x9 = 'sudoku_9x9';
  static String snakeMap(String mapId) => 'snake_$mapId';
}

enum StoreItem { hammer, hint, diamond }

/// What each consumable product grants when bought.
const Map<String, (StoreItem, int)> kConsumableGrants = {
  StoreIds.hammers10: (StoreItem.hammer, 10),
  StoreIds.hammers20: (StoreItem.hammer, 20),
  StoreIds.hammers30: (StoreItem.hammer, 30),
  StoreIds.hammers50: (StoreItem.hammer, 50),
  StoreIds.hammers100: (StoreItem.hammer, 100),
  StoreIds.hints10: (StoreItem.hint, 10),
  StoreIds.hints20: (StoreItem.hint, 20),
  StoreIds.hints30: (StoreItem.hint, 30),
  StoreIds.hints50: (StoreItem.hint, 50),
  StoreIds.hints100: (StoreItem.hint, 100),
  StoreIds.diamonds10: (StoreItem.diamond, 10),
  StoreIds.diamonds20: (StoreItem.diamond, 20),
  StoreIds.diamonds30: (StoreItem.diamond, 30),
  StoreIds.diamonds50: (StoreItem.diamond, 50),
  StoreIds.diamonds100: (StoreItem.diamond, 100),
  StoreIds.diamonds500: (StoreItem.diamond, 500),
};

/// Owns in-app purchases and the player's purchased stock (premium flag,
/// hammers, hints, diamonds, unlocked levels). Listen to it to rebuild when
/// anything changes.
class StoreService extends ChangeNotifier {
  StoreService._();
  static final StoreService instance = StoreService._();

  /// Diamonds to unlock one locked level or map.
  static const unlockCost = 10;

  /// Diamonds to continue a Block Puzzle (6 Shapes) game after game over.
  static const continueCost = 1;

  static const _premiumKey = 'store_premium';
  static const _hammersKey = 'store_hammers';
  static const _hintsKey = 'store_hints';
  static const _diamondsKey = 'store_diamonds';
  static const _unlockedKey = 'store_unlocked';
  static const _deliveredKey = 'store_delivered_ids';

  bool isPremium = false;
  int hammers = 0;
  int hints = 0;
  int diamonds = 0;
  Set<String> _unlocked = {};

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
    diamonds = _prefs.getInt(_diamondsKey) ?? 0;
    _unlocked = (_prefs.getStringList(_unlockedKey) ?? []).toSet();
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
    switch (item) {
      case StoreItem.hammer:
        hammers += amount;
        _prefs.setInt(_hammersKey, hammers);
      case StoreItem.hint:
        hints += amount;
        _prefs.setInt(_hintsKey, hints);
      case StoreItem.diamond:
        diamonds += amount;
        _prefs.setInt(_diamondsKey, diamonds);
    }
  }

  int _stock(StoreItem item) => switch (item) {
    StoreItem.hammer => hammers,
    StoreItem.hint => hints,
    StoreItem.diamond => diamonds,
  };

  /// Spends one purchased hammer. Returns false if there are none.
  bool useHammer() => _spend(StoreItem.hammer, 1);

  /// Spends one purchased hint. Returns false if there are none.
  bool useHint() => _spend(StoreItem.hint, 1);

  /// Spends [amount] diamonds. Returns false if there aren't enough.
  bool spendDiamonds(int amount) => _spend(StoreItem.diamond, amount);

  bool _spend(StoreItem item, int amount) {
    if (_stock(item) < amount) return false;
    _addStock(item, -amount);
    notifyListeners();
    return true;
  }

  bool isUnlocked(String lockId) => isPremium || _unlocked.contains(lockId);

  /// Spends [unlockCost] diamonds to unlock [lockId] for good. Returns false
  /// if there aren't enough diamonds.
  bool unlock(String lockId) {
    if (isUnlocked(lockId)) return true;
    if (!spendDiamonds(unlockCost)) return false;
    _unlocked.add(lockId);
    _prefs.setStringList(_unlockedKey, _unlocked.toList());
    notifyListeners();
    return true;
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }
}
