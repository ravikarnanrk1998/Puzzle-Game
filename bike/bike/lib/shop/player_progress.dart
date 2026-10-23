import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'catalog.dart';
import 'save_data.dart';

class RunReward {
  const RunReward({required this.coinsEarned, required this.newBest});

  final int coinsEarned;
  final bool newBest;
}

/// The player's saved data: coin wallet, owned bikes, cars and skins, best
/// score and purchased upgrades. Saved on the device with SharedPreferences,
/// and for a signed-in player also in the cloud (see CloudSave).
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
  static const _kDiamonds = 'diamonds';
  static const _kDailyClaim = 'daily_claim';
  static const _kTracks = 'unlocked_tracks';
  static const _kTrack = 'selected_track';
  static const _kAvatar = 'avatar';
  static const _kSkins = 'owned_skins';
  static const _kSkin = 'selected_skin';

  /// Diamonds given by the free daily claim.
  static const dailyDiamonds = 1;

  final SharedPreferences? _prefs;

  int wallet = 0;

  /// The second currency: paid to create a 1 vs 1 match, earned once a day
  /// for free or bought in the garage.
  int diamonds = 0;
  int best = 0;
  bool doubleCoins = false;

  /// The bikes and cars the player has, and the one they ride.
  Set<String> owned = {Catalog.rides.first.id};
  String selected = Catalog.rides.first.id;

  /// The skins the player has, and the one their ride wears.
  Set<String> skins = {Catalog.skins.first.id};
  String skinId = Catalog.skins.first.id;
  final List<String> _delivered = [];

  /// The day the daily diamond was last claimed, as '2026-10-13'.
  String? dailyClaim;

  /// Tracks unlocked after the first, which is always open.
  Set<String> tracks = {};

  /// The track the next run is on. Kept on this device only.
  String trackId = Catalog.tracks.first.id;

  /// The cartoon picture on the player's profile, by name. Null until the
  /// player picks one.
  String? avatar;

  /// The account this progress belongs to. Null for progress made without
  /// signing in.
  String? owner;

  /// Result of the most recent run, shown on the game over screen.
  RunReward? lastRun;

  Ride get selectedRide => Catalog.ride(selected);
  bool owns(Ride ride) => owned.contains(ride.id);

  Skin get selectedSkin => Catalog.skin(skinId);
  bool hasSkin(Skin skin) => skins.contains(skin.id);

  Track get selectedTrack => Catalog.track(trackId);
  bool hasTrack(Track track) => track.isFree || tracks.contains(track.id);

  SaveData get data => SaveData(
        wallet: wallet,
        diamonds: diamonds,
        best: best,
        doubleCoins: doubleCoins,
        owned: {...owned},
        selected: selected,
        delivered: [..._delivered],
        dailyClaim: dailyClaim,
        tracks: {...tracks},
        avatar: avatar,
        skins: {...skins},
        skin: skinId,
      );

  /// Replaces all progress, e.g. with the player's cloud save.
  void restore(SaveData data, {String? owner}) {
    wallet = data.wallet;
    diamonds = data.diamonds;
    dailyClaim = data.dailyClaim;
    best = data.best;
    doubleCoins = data.doubleCoins;
    owned = {Catalog.rides.first.id, ...data.owned};
    selected = data.selected ?? Catalog.rides.first.id;
    if (!owned.contains(selected)) selected = Catalog.rides.first.id;
    skins = {Catalog.skins.first.id, ...data.skins};
    skinId = data.skin ?? Catalog.skins.first.id;
    if (!skins.contains(skinId)) skinId = Catalog.skins.first.id;
    _delivered
      ..clear()
      ..addAll(data.delivered);
    tracks = {...data.tracks};
    _checkTrack();
    avatar = data.avatar;
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
    owned = {Catalog.rides.first.id, ...?p.getStringList(_kOwned)};
    selected = p.getString(_kSelected) ?? Catalog.rides.first.id;
    if (!owned.contains(selected)) selected = Catalog.rides.first.id;
    skins = {Catalog.skins.first.id, ...?p.getStringList(_kSkins)};
    skinId = p.getString(_kSkin) ?? Catalog.skins.first.id;
    if (!skins.contains(skinId)) skinId = Catalog.skins.first.id;
    _delivered.addAll(p.getStringList(_kDelivered) ?? const []);
    owner = p.getString(_kOwner);
    diamonds = p.getInt(_kDiamonds) ?? 0;
    dailyClaim = p.getString(_kDailyClaim);
    tracks = {...?p.getStringList(_kTracks)};
    trackId = p.getString(_kTrack) ?? Catalog.tracks.first.id;
    _checkTrack();
    avatar = p.getString(_kAvatar);
  }

  /// Back to the first track if the picked one is not unlocked (any more).
  void _checkTrack() {
    final track = selectedTrack;
    trackId = hasTrack(track) ? track.id : Catalog.tracks.first.id;
  }

  void _save() {
    final p = _prefs;
    if (p != null) {
      p.setInt(_kWallet, wallet);
      p.setInt(_kBest, best);
      p.setBool(_kDouble, doubleCoins);
      p.setStringList(_kOwned, owned.toList());
      p.setString(_kSelected, selected);
      p.setStringList(_kSkins, skins.toList());
      p.setString(_kSkin, skinId);
      p.setStringList(_kDelivered, _delivered);
      final owner = this.owner;
      if (owner == null) {
        p.remove(_kOwner);
      } else {
        p.setString(_kOwner, owner);
      }
      p.setInt(_kDiamonds, diamonds);
      final dailyClaim = this.dailyClaim;
      if (dailyClaim == null) {
        p.remove(_kDailyClaim);
      } else {
        p.setString(_kDailyClaim, dailyClaim);
      }
      p.setStringList(_kTracks, tracks.toList());
      p.setString(_kTrack, trackId);
      final avatar = this.avatar;
      if (avatar == null) {
        p.remove(_kAvatar);
      } else {
        p.setString(_kAvatar, avatar);
      }
    }
    notifyListeners();
  }

  static String _day(DateTime t) => '${t.year}-'
      '${t.month.toString().padLeft(2, '0')}-'
      '${t.day.toString().padLeft(2, '0')}';

  /// Whether the free daily diamond is still waiting to be claimed today.
  bool canClaimDaily([DateTime? now]) {
    final last = dailyClaim;
    return last == null || _day(now ?? DateTime.now()).compareTo(last) > 0;
  }

  /// Hands out today's free diamond. Returns false if it was already
  /// claimed today.
  bool claimDaily([DateTime? now]) {
    if (!canClaimDaily(now)) return false;
    dailyClaim = _day(now ?? DateTime.now());
    diamonds += dailyDiamonds;
    _save();
    return true;
  }

  bool spendDiamonds(int amount) {
    if (amount < 0 || diamonds < amount) return false;
    diamonds -= amount;
    _save();
    return true;
  }

  void addDiamonds(int amount) {
    diamonds += amount;
    _save();
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

  /// Buys a coin-priced bike or car and rides it. Returns false if not
  /// possible.
  bool buyWithCoins(Ride ride) {
    if (owns(ride) || ride.isPremium || ride.coinPrice <= 0) return false;
    if (wallet < ride.coinPrice) return false;
    wallet -= ride.coinPrice;
    owned.add(ride.id);
    selected = ride.id;
    _save();
    return true;
  }

  void select(Ride ride) {
    if (!owns(ride) || selected == ride.id) return;
    selected = ride.id;
    _save();
  }

  /// Returns true if this unlocked something new.
  bool unlockRide(String id) {
    if (!owned.add(id)) return false;
    _save();
    return true;
  }

  /// Buys a coin-priced skin and puts it on. Returns false if not possible.
  bool buySkin(Skin skin) {
    if (hasSkin(skin) || skin.isPremium || skin.coinPrice <= 0) return false;
    if (wallet < skin.coinPrice) return false;
    wallet -= skin.coinPrice;
    skins.add(skin.id);
    skinId = skin.id;
    _save();
    return true;
  }

  void selectSkin(Skin skin) {
    if (!hasSkin(skin) || skinId == skin.id) return;
    skinId = skin.id;
    _save();
  }

  /// Returns true if this unlocked something new.
  bool unlockSkin(String id) {
    if (!skins.add(id)) return false;
    _save();
    return true;
  }

  /// Opens [track] for good, e.g. for completing its unlock level. Returns
  /// true if this unlocked something new.
  bool unlockTrack(Track track) {
    if (hasTrack(track)) return false;
    tracks.add(track.id);
    _save();
    return true;
  }

  /// Buys [track] with coins, or with diamonds if [withDiamonds], and picks
  /// it for the next run. Returns false if not possible.
  bool buyTrack(Track track, {bool withDiamonds = false}) {
    if (hasTrack(track)) return false;
    if (withDiamonds) {
      if (track.diamondPrice <= 0 || diamonds < track.diamondPrice) {
        return false;
      }
      diamonds -= track.diamondPrice;
    } else {
      if (track.coinPrice <= 0 || wallet < track.coinPrice) return false;
      wallet -= track.coinPrice;
    }
    tracks.add(track.id);
    trackId = track.id;
    _save();
    return true;
  }

  void selectTrack(Track track) {
    if (!hasTrack(track) || trackId == track.id) return;
    trackId = track.id;
    _save();
  }

  /// Puts the avatar called [id] on the player's profile.
  void selectAvatar(String id) {
    if (avatar == id) return;
    avatar = id;
    _save();
  }

  /// Returns true if this unlocked something new.
  bool unlockDoubleCoins() {
    if (doubleCoins) return false;
    doubleCoins = true;
    _save();
    return true;
  }

  /// Remembers a store order so the same pack is never credited twice.
  /// Returns false if [purchaseId] was already delivered.
  bool markDelivered(String purchaseId) {
    if (_delivered.contains(purchaseId)) return false;
    _delivered.add(purchaseId);
    if (_delivered.length > 200) _delivered.removeAt(0);
    _save();
    return true;
  }
}
