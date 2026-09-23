import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:in_app_purchase/in_app_purchase.dart';

import 'catalog.dart';
import 'player_progress.dart';

/// Google Play / App Store in-app purchases.
///
/// Purchases are delivered on the device. For a bigger game you should
/// verify `verificationData` on your own server before granting items.
class StoreService extends ChangeNotifier {
  StoreService(this.progress, {bool? enabled})
      : enabled = enabled ?? _platformSupported;

  static bool get _platformSupported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  final PlayerProgress progress;

  /// False on web/desktop, where there is no store.
  final bool enabled;

  bool available = false;
  bool loading = false;
  final Map<String, ProductDetails> products = {};
  final Set<String> _busy = {};
  final StreamController<String> _messages =
      StreamController<String>.broadcast();
  StreamSubscription<List<PurchaseDetails>>? _sub;

  /// Short messages for the UI ("Purchase cancelled", "+2,000 coins!", ...).
  Stream<String> get messages => _messages.stream;

  InAppPurchase get _iap => InAppPurchase.instance;

  bool isBusy(String productId) => _busy.contains(productId);
  ProductDetails? product(String? productId) =>
      productId == null ? null : products[productId];

  /// Call once at app start. Safe to call again to retry loading products.
  Future<void> init() async {
    if (!enabled || loading) return;
    loading = true;
    notifyListeners();
    _sub ??= _iap.purchaseStream.listen(
      handlePurchases,
      onError: (Object e) => _messages.add('Store error: $e'),
    );
    try {
      available = await _iap.isAvailable();
      if (available) {
        final response = await _iap.queryProductDetails(Catalog.productIds);
        for (final p in response.productDetails) {
          products[p.id] = p;
        }
        if (response.notFoundIDs.isNotEmpty) {
          debugPrint('Products missing in store: ${response.notFoundIDs}');
        }
        // Silently give back premium items after a reinstall. (Android
        // only; on iOS this can show a sign-in prompt.)
        if (products.isNotEmpty &&
            defaultTargetPlatform == TargetPlatform.android) {
          await _iap.restorePurchases();
        }
      }
    } catch (e) {
      debugPrint('Store init failed: $e');
      available = false;
    }
    loading = false;
    notifyListeners();
  }

  Future<void> buy(String productId) async {
    final details = products[productId];
    if (!enabled || !available || details == null) {
      _messages.add('Store is not available right now. Try again later.');
      if (enabled && !loading) init();
      return;
    }
    _busy.add(productId);
    notifyListeners();
    final param = PurchaseParam(productDetails: details);
    try {
      final started = Catalog.isConsumable(productId)
          ? await _iap.buyConsumable(purchaseParam: param)
          : await _iap.buyNonConsumable(purchaseParam: param);
      if (!started) _busy.remove(productId);
    } catch (e) {
      debugPrint('Purchase failed to start: $e');
      _busy.remove(productId);
      _messages.add('Could not start the purchase. Please try again.');
    }
    notifyListeners();
  }

  Future<void> restore() async {
    if (!enabled || !available) {
      _messages.add('Store is not available right now.');
      return;
    }
    _messages.add('Restoring purchases…');
    await _iap.restorePurchases();
  }

  @visibleForTesting
  Future<void> handlePurchases(List<PurchaseDetails> purchases) async {
    for (final p in purchases) {
      switch (p.status) {
        case PurchaseStatus.pending:
          _messages.add('Payment pending…');
        case PurchaseStatus.purchased:
        case PurchaseStatus.restored:
          _deliver(p);
        case PurchaseStatus.error:
          _messages.add(
              'Purchase failed: ${p.error?.message ?? 'unknown error'}');
        case PurchaseStatus.canceled:
          _messages.add('Purchase cancelled');
      }
      if (p.status != PurchaseStatus.pending) _busy.remove(p.productID);
      if (p.pendingCompletePurchase) await _iap.completePurchase(p);
    }
    notifyListeners();
  }

  void _deliver(PurchaseDetails p) {
    final id = p.productID;
    final pack = Catalog.coinPack(id);
    if (pack != null) {
      // A coin pack can be reported again if the app closed before the
      // purchase was completed; credit each order only once.
      final orderId =
          p.purchaseID ?? p.verificationData.serverVerificationData;
      if (!progress.markDelivered(orderId)) return;
      progress.addCoins(pack.coins);
      _messages.add('+${formatNumber(pack.coins)} coins added!');
      return;
    }
    if (id == Catalog.doubleCoinsId) {
      if (progress.unlockDoubleCoins()) {
        _messages.add('Double coins unlocked!');
      }
      return;
    }
    final bike = Catalog.bikeForProduct(id);
    if (bike != null && progress.unlockBike(bike.id)) {
      _messages.add('${bike.name} unlocked!');
    }
  }

  @override
  void dispose() {
    _sub?.cancel();
    _messages.close();
    super.dispose();
  }
}
