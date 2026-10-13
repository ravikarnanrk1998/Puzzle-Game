import 'dart:async';

import 'package:bike/account/account_service.dart';
import 'package:bike/account/cloud_save.dart';
import 'package:bike/shop/save_data.dart';
import 'package:bike/versus/match_backend.dart';

/// Stands in for Firestore: one server shared by every device in a test.
class FakeSaves implements SaveBackend {
  final Map<String, SaveData> saves = {};
  final List<SaveDelta> writes = [];
  final List<void Function()> _watchers = [];

  /// While offline, devices only see the copy they have cached.
  bool online = true;

  @override
  Stream<CloudSnapshot> watch(String uid) {
    final out = StreamController<CloudSnapshot>();
    void emit() => out.add((save: saves[uid], fromServer: online));
    _watchers.add(emit);
    out.onCancel = () => _watchers.remove(emit);
    emit();
    return out.stream;
  }

  @override
  void write(String uid, SaveDelta delta) {
    writes.add(delta);
    change(uid, delta);
  }

  /// Changes a save the way a write does, e.g. from another device.
  void change(String uid, SaveDelta d) {
    final s = saves[uid] ?? const SaveData();
    saves[uid] = SaveData(
      wallet: s.wallet + d.coins,
      diamonds: s.diamonds + d.diamonds,
      best: d.best ?? s.best,
      doubleCoins: s.doubleCoins || d.doubleCoins,
      owned: {...s.owned, ...d.bikes},
      selected: d.selected ?? s.selected,
      delivered: {...s.delivered, ...d.orders}.toList(),
      dailyClaim: d.dailyClaim ?? s.dailyClaim,
    );
    notify();
  }

  void notify() {
    for (final emit in [..._watchers]) {
      emit();
    }
  }

  @override
  Future<void> flush() async {}

  @override
  Future<void> delete(String uid) async {
    saves.remove(uid);
    notify();
  }
}

/// An [AccountService] that never talks to Firebase.
class TestAccount extends AccountService {
  TestAccount(super.cloud);

  @override
  Future<String?> signInWithEmail(String email, String password) async {
    if (password != 'secret1') return 'Wrong email or password.';
    usesPassword = true;
    handleUser(email, name: email);
    return null;
  }

  @override
  Future<void> signOut() async => handleUser(null);

  @override
  Future<String?> deleteAccount({String? password}) async {
    if (password != 'secret1') return 'Wrong email or password.';
    await cloud.erase();
    handleUser(null);
    return null;
  }
}

/// Stands in for Firestore: the matches every player in a test can see.
class FakeMatches implements MatchBackend {
  final Map<String, MatchSnapshot> matches = {};
  final Map<String, List<StreamController<MatchSnapshot?>>> _watchers = {};
  int _nextCode = 100000;

  @override
  Future<String> create(MatchPlayer host) async {
    final code = '${_nextCode++}';
    matches[code] = (host: host, guest: null);
    return code;
  }

  @override
  Future<void> join(String code, MatchPlayer guest) async {
    final match = matches[code];
    if (match == null) {
      throw const MatchError('No match found with that code.');
    }
    if (match.host.uid == guest.uid) {
      throw const MatchError('That is your own code. Send it to a friend.');
    }
    if (match.guest != null) {
      throw const MatchError('That match already has two players.');
    }
    matches[code] = (host: match.host, guest: guest);
    _notify(code);
  }

  @override
  Stream<MatchSnapshot?> watch(String code) {
    final out = StreamController<MatchSnapshot?>();
    (_watchers[code] ??= []).add(out);
    out.onCancel = () => _watchers[code]?.remove(out);
    out.add(matches[code]);
    return out.stream;
  }

  @override
  void update(String code, MatchPlayer player, {required bool host}) {
    final match = matches[code];
    if (match == null) return;
    matches[code] = host
        ? (host: player, guest: match.guest)
        : (host: match.host, guest: player);
    _notify(code);
  }

  @override
  Future<void> close(String code) async {
    matches.remove(code);
    _notify(code);
  }

  void _notify(String code) {
    for (final watcher in [...?_watchers[code]]) {
      watcher.add(matches[code]);
    }
  }
}
