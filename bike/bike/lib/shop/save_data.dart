import 'dart:math';

import 'package:flutter/foundation.dart';

/// Everything in a player's save. The same shape is kept on the device and
/// in the player's cloud save.
@immutable
class SaveData {
  const SaveData({
    this.wallet = 0,
    this.best = 0,
    this.doubleCoins = false,
    this.owned = const {},
    this.selected,
    this.delivered = const [],
  });

  final int wallet;
  final int best;
  final bool doubleCoins;
  final Set<String> owned;
  final String? selected;

  /// Store orders whose coins have already been credited.
  final List<String> delivered;

  /// What the player has once this cloud save reaches a device holding
  /// [local].
  ///
  /// Coins, credited orders and the equipped bike follow the cloud. Bikes,
  /// upgrades and the best score are only ever gained, so the device never
  /// gives them up: a purchase can't be undone by an update that arrives
  /// late or out of date.
  ///
  /// With [guest], [local] is progress made before signing in, so its coins
  /// and orders are added on top instead of replaced.
  SaveData merge(SaveData local, {bool guest = false}) {
    final bikes = {...owned, ...local.owned};
    return SaveData(
      wallet: max(0, guest ? wallet + local.wallet : wallet),
      best: max(best, local.best),
      doubleCoins: doubleCoins || local.doubleCoins,
      owned: bikes,
      selected: bikes.contains(selected) ? selected : local.selected,
      delivered:
          guest ? {...delivered, ...local.delivered}.toList() : delivered,
    );
  }

  /// The changes that turn [base] into this save.
  SaveDelta since(SaveData base) => SaveDelta(
        coins: wallet - base.wallet,
        best: best > base.best ? best : null,
        doubleCoins: doubleCoins && !base.doubleCoins,
        bikes: owned.difference(base.owned),
        selected: selected != base.selected ? selected : null,
        orders: [
          for (final order in delivered)
            if (!base.delivered.contains(order)) order,
        ],
      );

  @override
  bool operator ==(Object other) =>
      other is SaveData &&
      other.wallet == wallet &&
      other.best == best &&
      other.doubleCoins == doubleCoins &&
      other.selected == selected &&
      setEquals(other.owned, owned) &&
      listEquals(other.delivered, delivered);

  @override
  int get hashCode => Object.hash(
      wallet, best, doubleCoins, selected, owned.length, delivered.length);
}

/// A change to a save, described so that two devices can both send theirs
/// without undoing each other: coins as an amount to add or take away, bikes
/// and orders as things to add.
@immutable
class SaveDelta {
  const SaveDelta({
    this.coins = 0,
    this.best,
    this.doubleCoins = false,
    this.bikes = const {},
    this.selected,
    this.orders = const [],
  });

  final int coins;

  /// A new best score, or null if it was not beaten.
  final int? best;

  /// True if double coins was just unlocked.
  final bool doubleCoins;
  final Set<String> bikes;

  /// The newly equipped bike, or null if it did not change.
  final String? selected;
  final List<String> orders;

  bool get isEmpty =>
      coins == 0 &&
      best == null &&
      !doubleCoins &&
      bikes.isEmpty &&
      selected == null &&
      orders.isEmpty;
}
