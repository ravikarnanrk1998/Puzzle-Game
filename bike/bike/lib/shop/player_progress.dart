import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'catalog.dart';
import 'save_data.dart';

class RunReward {
  const RunReward({required this.coinsEarned, required this.newBest});

  final int coinsEarned;
  final bool newBest;
}

/// The player's saved data: coin wallet, owned bikes, best score and
/// purchased upgrades. Saved on the device with SharedPreferences, and for
/// a signed-in player also in the cloud (see CloudSave).
class PlayerProgress extends ChangeNotifier {
  PlayerProgress._(this._prefs);

  /// In-memory only (nothing saved). Used by tests.
  PlayerProgress.memory() : _prefs = null;

  static Future<PlayerProgress> load() async {
    final prefs = await SharedPreferences.getInstance();
    return PlayerProgress._(prefs).._read();
  }

  static const _kWallet = 'wallet';
  static const _kBest = 'best';
  static const _kDouble = 'double_coins';
  static const _kOwned = 'owned_bikes';
  static const _kSelected = 'selected_bike';
  static const _kDelivered = 'delivered_purchases';
  static const _kOwner = 'owner';

  final SharedPreferences? _prefs;

  int wallet = 0;
  int best = 0;
  bool doubleCoins = false;
  Set<String> owned = {Catalog.bikes.first.id};
  String selected = Catalog.bikes.first.id;
  final List<String> _delivered = [];

  /// The account this progress belongs to. Null for progress made without
  /// signing in.
  String? owner;

  /// Result of the most recent run, shown on the game over screen.
  RunReward? lastRun;

  BikeSkin get selectedBike => Catalog.bike(selected);
  bool owns(BikeSkin bike) => owned.contains(bike.id);

  SaveData get data => SaveData(
        wallet: wallet,
        best: best,
        doubleCoins: doubleCoins,
        owned: {...owned},
        selected: selected,
        delivered: [..._delivered],
      );

  /// Replaces all progress, e.g. with the player's cloud save.
  void restore(SaveData data, {String? owner}) {
    wallet = data.wallet;
    best = data.best;
    doubleCoins = data.doubleCoins;
    owned = {Catalog.bikes.first.id, ...data.owned};
    selected = data.selected ?? Catalog.bikes.first.id;
    if (!owned.contains(selected)) selected = Catalog.bikes.first.id;
    _delivered
      ..clear()
      ..addAll(data.delivered);
    this.owner = owner;
    _save();
  }

  /// Back to a fresh start, e.g. after signing out.
  void reset() {
    lastRun = null;
    restore(const SaveData());
  }

  void _read() {
    final p = _prefs!;
    wallet = p.getInt(_kWallet) ?? 0;
    best = p.getInt(_kBest) ?? 0;
    doubleCoins = p.getBool(_kDouble) ?? false;
    owned = {Catalog.bikes.first.id, ...?p.getStringList(_kOwned)};
    selected = p.getString(_kSelected) ?? Catalog.bikes.first.id;
    if (!owned.contains(selected)) selected = Catalog.bikes.first.id;
    _delivered.addAll(p.getStringList(_kDelivered) ?? const []);
    owner = p.getString(_kOwner);
  }

  void _save() {
    final p = _prefs;
    if (p != null) {
      p.setInt(_kWallet, wallet);
      p.setInt(_kBest, best);
      p.setBool(_kDouble, doubleCoins);
      p.setStringList(_kOwned, owned.toList());
      p.setString(_kSelected, selected);
      p.setStringList(_kDelivered, _delivered);
      final owner = this.owner;
      if (owner == null) {
        p.remove(_kOwner);
      } else {
        p.setString(_kOwner, owner);
      }
    }
    notifyListeners();
  }

  /// Banks the coins from a run and updates the best score.
  RunReward recordRun({required int score, required int coins}) {
    final earned = coins * (doubleCoins ? 2 : 1);
    wallet += earned;
    final newBest = score > best;
    if (newBest) best = score;
    lastRun = RunReward(coinsEarned: earned, newBest: newBest);
    _save();
    return lastRun!;
  }

  bool spend(int amount) {
    if (amount < 0 || wallet < amount) return false;
    wallet -= amount;
    _save();
    return true;
  }

  void addCoins(int amount) {
    wallet += amount;
    _save();
  }

  /// Buys a coin-priced bike and equips it. Returns false if not possible.
  bool buyWithCoins(BikeSkin bike) {
    if (owns(bike) || bike.isPremium || bike.coinPrice <= 0) return false;
    if (wallet < bike.coinPrice) return false;
    wallet -= bike.coinPrice;
    owned.add(bike.id);
    selected = bike.id;
    _save();
    return true;
  }

  void select(BikeSkin bike) {
    if (!owns(bike) || selected == bike.id) return;
    selected = bike.id;
    _save();
  }

  /// Returns true if this unlocked something new.
  bool unlockBike(String id) {
    if (!owned.add(id)) return false;
    _save();
    return true;
  }

  /// Returns true if this unlocked something new.
  bool unlockDoubleCoins() {
    if (doubleCoins) return false;
    doubleCoins = true;
    _save();
    return true;
  }

  /// Remembers a store order so the same coin pack is never credited twice.
  /// Returns false if [purchaseId] was already delivered.
  bool markDelivered(String purchaseId) {
    if (_delivered.contains(purchaseId)) return false;
    _delivered.add(purchaseId);
    if (_delivered.length > 200) _delivered.removeAt(0);
    _save();
    return true;
  }
}
