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
      owned: {...s.owned, ...d.rides},
      selected: d.selected ?? s.selected,
      skins: {...s.skins, ...d.skins},
      skin: d.skin ?? s.skin,
      delivered: {...s.delivered, ...d.orders}.toList(),
      dailyClaim: d.dailyClaim ?? s.dailyClaim,
      tracks: {...s.tracks, ...d.tracks},
      avatar: d.avatar ?? s.avatar,
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
///
/// Used directly it is a phone that never loses its connection. [phone]
/// gives one whose connection a test can cut.
class FakeMatches implements MatchBackend {
  final Map<String, MatchSnapshot> matches = {};
  final List<FakePhone> _phones = [];
  late final FakePhone _always = phone();
  int _nextCode = 100000;

  FakePhone phone() {
    final phone = FakePhone._(this);
    _phones.add(phone);
    return phone;
  }

  @override
  Future<String> create(MatchPlayer host) => _always.create(host);

  @override
  Future<void> join(String code, MatchPlayer guest) =>
      _always.join(code, guest);

  @override
  Stream<MatchSnapshot?> watch(String code) => _always.watch(code);

  @override
  Future<void> update(String code, MatchPlayer player, {required bool host}) =>
      _always.update(code, player, host: host);

  @override
  Future<String?> settle(String code, String winner) =>
      _always.settle(code, winner);

  @override
  Future<void> close(String code) => _always.close(code);

  void _notify(String code) {
    for (final phone in [..._phones]) {
      phone._deliver(code);
    }
  }
}

/// One phone's connection to [FakeMatches]. While it is offline, what it
/// sends waits, it hears nothing, and anything that needs an answer from
/// the server fails, like Firestore.
class FakePhone implements MatchBackend {
  FakePhone._(this._server);

  final FakeMatches _server;
  final Map<String, List<StreamController<MatchSnapshot?>>> _watchers = {};
  final List<void Function()> _outbox = [];
  bool _online = true;

  Map<String, MatchSnapshot> get _matches => _server.matches;

  bool get online => _online;

  set online(bool value) {
    _online = value;
    if (!value) return;
    final waiting = [..._outbox];
    _outbox.clear();
    for (final send in waiting) {
      send();
    }
    _watchers.keys.forEach(_deliver);
  }

  void _needServer() {
    if (!_online) throw const MatchError('No internet connection.');
  }

  /// Runs [write] on the server now, or once back online.
  Future<void> _write(void Function() write) {
    final answered = Completer<void>();
    void send() {
      write();
      answered.complete();
    }

    _online ? send() : _outbox.add(send);
    return answered.future;
  }

  @override
  Future<String> create(MatchPlayer host) async {
    _needServer();
    final code = '${_server._nextCode++}';
    _matches[code] = (host: host, guest: null, winner: null);
    return code;
  }

  @override
  Future<void> join(String code, MatchPlayer guest) async {
    _needServer();
    final match = _matches[code];
    if (match == null) {
      throw const MatchError('No match found with that code.');
    }
    if (match.host.uid == guest.uid) {
      throw const MatchError('That is your own code. Send it to a friend.');
    }
    if (match.guest != null) {
      throw const MatchError('That match already has two players.');
    }
    _matches[code] = (host: match.host, guest: guest, winner: null);
    _server._notify(code);
  }

  @override
  Stream<MatchSnapshot?> watch(String code) {
    final out = StreamController<MatchSnapshot?>();
    (_watchers[code] ??= []).add(out);
    out.onCancel = () => _watchers[code]?.remove(out);
    out.add(_matches[code]);
    return out.stream;
  }

  @override
  Future<void> update(String code, MatchPlayer player, {required bool host}) =>
      _write(() {
        final match = _matches[code];
        if (match == null) return;
        _matches[code] = host
            ? (host: player, guest: match.guest, winner: match.winner)
            : (host: match.host, guest: player, winner: match.winner);
        _server._notify(code);
      });

  @override
  Future<String?> settle(String code, String winner) async {
    _needServer();
    final match = _matches[code];
    if (match == null) return null;
    if (match.winner != null) return match.winner;
    _matches[code] = (host: match.host, guest: match.guest, winner: winner);
    _server._notify(code);
    return winner;
  }

  @override
  Future<void> close(String code) => _write(() {
        _matches.remove(code);
        _server._notify(code);
      });

  void _deliver(String code) {
    if (!_online) return;
    for (final watcher in [...?_watchers[code]]) {
      watcher.add(_matches[code]);
    }
  }
}
