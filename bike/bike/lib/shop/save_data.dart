import 'dart:math';

import 'package:flutter/foundation.dart';

/// Everything in a player's save. The same shape is kept on the device and
/// in the player's cloud save.
@immutable
class SaveData {
  const SaveData({
    this.wallet = 0,
    this.diamonds = 0,
    this.best = 0,
    this.doubleCoins = false,
    this.owned = const {},
    this.selected,
    this.delivered = const [],
    this.dailyClaim,
    this.tracks = const {},
    this.avatar,
    this.skins = const {},
    this.skin,
  });

  final int wallet;
  final int diamonds;
  final int best;
  final bool doubleCoins;

  /// The bikes and cars the player has, and the one being ridden.
  final Set<String> owned;
  final String? selected;

  /// The skins the player has, and the one being worn.
  final Set<String> skins;
  final String? skin;

  /// The profile picture the player picked, or null if they never have.
  final String? avatar;

  /// Store orders whose coins or diamonds have already been credited.
  final List<String> delivered;

  /// The day the free daily diamond was last claimed, as '2026-10-13'.
  final String? dailyClaim;

  /// Tracks unlocked after the first, which is always open.
  final Set<String> tracks;

  /// What the player has once this cloud save reaches a device holding
  /// [local].
  ///
  /// Coins, diamonds, credited orders, the ride and skin in use and the
  /// avatar follow the cloud. Rides, skins, tracks, upgrades, the best score
  /// and the last daily claim only ever move forward, so the device never
  /// gives them up: a purchase can't be undone by an update that arrives
  /// late or out of date.
  ///
  /// With [guest], [local] is progress made before signing in, so its
  /// coins, diamonds and orders are added on top instead of replaced.
  SaveData merge(SaveData local, {bool guest = false}) {
    final rides = {...owned, ...local.owned};
    final paints = {...skins, ...local.skins};
    return SaveData(
      wallet: max(0, guest ? wallet + local.wallet : wallet),
      diamonds: max(0, guest ? diamonds + local.diamonds : diamonds),
      best: max(best, local.best),
      doubleCoins: doubleCoins || local.doubleCoins,
      owned: rides,
      selected: rides.contains(selected) ? selected : local.selected,
      skins: paints,
      skin: paints.contains(skin) ? skin : local.skin,
      delivered:
          guest ? {...delivered, ...local.delivered}.toList() : delivered,
      dailyClaim:
          _isLater(local.dailyClaim, dailyClaim) ? local.dailyClaim : dailyClaim,
      tracks: {...tracks, ...local.tracks},
      avatar: avatar ?? local.avatar,
    );
  }

  /// The changes that turn [base] into this save.
  SaveDelta since(SaveData base) => SaveDelta(
        coins: wallet - base.wallet,
        diamonds: diamonds - base.diamonds,
        best: best > base.best ? best : null,
        doubleCoins: doubleCoins && !base.doubleCoins,
        rides: owned.difference(base.owned),
        selected: selected != base.selected ? selected : null,
        skins: skins.difference(base.skins),
        skin: skin != base.skin ? skin : null,
        orders: [
          for (final order in delivered)
            if (!base.delivered.contains(order)) order,
        ],
        dailyClaim: _isLater(dailyClaim, base.dailyClaim) ? dailyClaim : null,
        tracks: tracks.difference(base.tracks),
        avatar: avatar != base.avatar ? avatar : null,
      );

  /// Whether day [a] comes after day [b]. A missing day is before any other.
  static bool _isLater(String? a, String? b) =>
      a != null && (b == null || a.compareTo(b) > 0);

  @override
  bool operator ==(Object other) =>
      other is SaveData &&
      other.wallet == wallet &&
      other.diamonds == diamonds &&
      other.best == best &&
      other.doubleCoins == doubleCoins &&
      other.selected == selected &&
      other.skin == skin &&
      other.avatar == avatar &&
      other.dailyClaim == dailyClaim &&
      setEquals(other.owned, owned) &&
      setEquals(other.skins, skins) &&
      listEquals(other.delivered, delivered) &&
      setEquals(other.tracks, tracks);

  @override
  int get hashCode => Object.hash(
      wallet,
      diamonds,
      best,
      doubleCoins,
      selected,
      skin,
      avatar,
      dailyClaim,
      owned.length,
      skins.length,
      delivered.length,
      tracks.length);
}

/// A change to a save, described so that two devices can both send theirs
/// without undoing each other: coins and diamonds as an amount to add or
/// take away, rides, skins and orders as things to add.
@immutable
class SaveDelta {
  const SaveDelta({
    this.coins = 0,
    this.diamonds = 0,
    this.best,
    this.doubleCoins = false,
    this.rides = const {},
    this.selected,
    this.orders = const [],
    this.dailyClaim,
    this.tracks = const {},
    this.avatar,
    this.skins = const {},
    this.skin,
  });

  final int coins;
  final int diamonds;

  /// A new best score, or null if it was not beaten.
  final int? best;

  /// True if double coins was just unlocked.
  final bool doubleCoins;

  /// Newly owned bikes and cars.
  final Set<String> rides;

  /// The newly picked bike or car, or null if it did not change.
  final String? selected;

  /// Newly owned skins.
  final Set<String> skins;

  /// The newly worn skin, or null if it did not change.
  final String? skin;
  final List<String> orders;

  /// The day of a new daily claim, or null if there was none.
  final String? dailyClaim;

  /// Newly unlocked tracks.
  final Set<String> tracks;

  /// The newly picked avatar, or null if it did not change.
  final String? avatar;

  bool get isEmpty =>
      coins == 0 &&
      diamonds == 0 &&
      best == null &&
      !doubleCoins &&
      rides.isEmpty &&
      selected == null &&
      skins.isEmpty &&
      skin == null &&
      orders.isEmpty &&
      dailyClaim == null &&
      tracks.isEmpty &&
      avatar == null;
}
