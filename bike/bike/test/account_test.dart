import 'dart:async';

import 'package:bike/account/account_service.dart';
import 'package:bike/account/cloud_save.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/save_data.dart';
import 'package:bike/shop/store_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

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
      best: d.best ?? s.best,
      doubleCoins: s.doubleCoins || d.doubleCoins,
      owned: {...s.owned, ...d.bikes},
      selected: d.selected ?? s.selected,
      delivered: {...s.delivered, ...d.orders}.toList(),
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
}

/// An [AccountService] that never talks to Firebase.
class TestAccount extends AccountService {
  TestAccount(super.cloud);

  @override
  Future<String?> signInWithEmail(String email, String password) async {
    if (password != 'secret1') return 'Wrong email or password.';
    handleUser(email, name: email);
    return null;
  }

  @override
  Future<void> signOut() async => handleUser(null);
}

void main() {
  group('SaveData', () {
    test('a cloud save never takes away bikes, upgrades or the best score',
        () {
      const cloud =
          SaveData(wallet: 300, best: 500, owned: {'red'}, selected: 'red');
      const local = SaveData(
        wallet: 900,
        best: 800,
        doubleCoins: true,
        owned: {'red', 'gold'},
        selected: 'gold',
      );
      final merged = cloud.merge(local);
      expect(merged.wallet, 300, reason: 'coins follow the cloud');
      expect(merged.best, 800);
      expect(merged.doubleCoins, isTrue);
      expect(merged.owned, {'red', 'gold'});
      expect(merged.selected, 'red');
    });

    test('progress made before signing in is added to the account', () {
      const cloud = SaveData(
        wallet: 300,
        owned: {'red', 'sun'},
        selected: 'sun',
        delivered: ['GPA.1'],
      );
      const guest = SaveData(
        wallet: 50,
        owned: {'red', 'blue'},
        selected: 'blue',
        delivered: ['GPA.2'],
      );
      final merged = cloud.merge(guest, guest: true);
      expect(merged.wallet, 350);
      expect(merged.owned, {'red', 'sun', 'blue'});
      expect(merged.selected, 'sun');
      expect(merged.delivered, ['GPA.1', 'GPA.2']);
    });
  });

  test('PlayerProgress remembers whose progress is on the device', () async {
    SharedPreferences.setMockInitialValues({});
    final p = await PlayerProgress.load();
    p.restore(
      const SaveData(wallet: 70, owned: {'blue'}, selected: 'blue'),
      owner: 'ravi',
    );
    final nextLaunch = await PlayerProgress.load();
    expect(nextLaunch.owner, 'ravi');
    expect(nextLaunch.wallet, 70);
    expect(nextLaunch.selected, 'blue');
  });

  group('CloudSave', () {
    late FakeSaves server;
    late PlayerProgress phone;
    late CloudSave cloud;

    setUp(() {
      server = FakeSaves();
      phone = PlayerProgress.memory();
      cloud = CloudSave(phone, server);
    });

    test('a new account keeps progress made before signing in', () async {
      phone
        ..addCoins(5000)
        ..buyWithCoins(Catalog.bike('blue'))
        ..addCoins(120);
      await cloud.attach('ravi');
      expect(phone.owner, 'ravi');
      expect(server.saves['ravi'], phone.data);
      expect(server.saves['ravi']!.wallet, 120);
      expect(server.saves['ravi']!.owned, {'red', 'blue'});
    });

    test('a save comes back on a new phone', () async {
      server.saves['ravi'] = const SaveData(
        wallet: 42000,
        best: 9000,
        doubleCoins: true,
        owned: {'red', 'gold'},
        selected: 'gold',
        delivered: ['GPA.1'],
      );
      await cloud.attach('ravi');
      expect(phone.wallet, 42000);
      expect(phone.best, 9000);
      expect(phone.doubleCoins, isTrue);
      expect(phone.selected, 'gold');
      expect(phone.markDelivered('GPA.1'), isFalse,
          reason: 'that order was already paid out');
      expect(server.writes, isEmpty);
    });

    test('coins are sent as amounts, so two devices add up', () async {
      await cloud.attach('ravi');
      phone.addCoins(100);
      await pumpEventQueue();
      expect(server.writes.last.coins, 100);
      // Meanwhile the same player earns coins on a tablet.
      server.change('ravi', const SaveDelta(coins: 500));
      await pumpEventQueue();
      expect(phone.wallet, 600);
      expect(phone.spend(250), isTrue);
      await pumpEventQueue();
      expect(server.saves['ravi']!.wallet, 350);
    });

    test('a coin pack and its order reach the cloud in one write', () async {
      await cloud.attach('ravi');
      final before = server.writes.length;
      // What the store does when a coin pack has been paid for.
      phone.markDelivered('GPA.7');
      phone.addCoins(10000);
      await pumpEventQueue();
      expect(server.writes.length, before + 1);
      expect(server.writes.last.coins, 10000);
      expect(server.writes.last.orders, ['GPA.7']);
    });

    test('signing out clears the phone, signing in brings it all back',
        () async {
      await cloud.attach('ravi');
      phone
        ..addCoins(5000)
        ..buyWithCoins(Catalog.bike('blue'))
        ..recordRun(score: 1200, coins: 35);
      await pumpEventQueue();
      cloud.detach();
      expect(phone.wallet, 0);
      expect(phone.best, 0);
      expect(phone.owns(Catalog.bike('blue')), isFalse);
      expect(phone.owner, isNull);
      expect(server.saves['ravi']!.wallet, 35,
          reason: 'signing out must not touch the cloud save');
      await cloud.attach('ravi');
      expect(phone.wallet, 35);
      expect(phone.best, 1200);
      expect(phone.selected, 'blue');
    });

    test('the next account on the same phone starts from its own save',
        () async {
      await cloud.attach('ravi');
      phone.addCoins(5000);
      await pumpEventQueue();
      await cloud.attach('priya');
      expect(phone.owner, 'priya');
      expect(phone.wallet, 0);
      expect(server.saves['priya']!.wallet, 0);
      expect(server.saves['ravi']!.wallet, 5000);
    });

    test('joining an account waits for the server, not a cached copy',
        () async {
      phone.addCoins(700);
      server.saves['ravi'] =
          const SaveData(wallet: 1000, owned: {'red'}, selected: 'red');
      server.online = false;
      var loaded = false;
      unawaited(cloud.attach('ravi').then((_) => loaded = true));
      await pumpEventQueue();
      expect(loaded, isFalse);
      expect(phone.wallet, 700);
      expect(server.writes, isEmpty);
      server
        ..online = true
        ..notify();
      await pumpEventQueue();
      expect(loaded, isTrue);
      expect(phone.wallet, 1700);
      expect(server.saves['ravi']!.wallet, 1700);
    });

    test('a bike is not lost to an out-of-date update', () async {
      await cloud.attach('ravi');
      final outOfDate = server.saves['ravi']!;
      phone.unlockBike('gold');
      await pumpEventQueue();
      expect(server.saves['ravi']!.owned, contains('gold'));
      server.saves['ravi'] = outOfDate;
      server.notify();
      await pumpEventQueue();
      expect(phone.owns(Catalog.bike('gold')), isTrue);
      expect(server.saves['ravi']!.owned, contains('gold'),
          reason: 'the phone sends the bike again');
    });
  });

  testWidgets('the game opens on the login screen until the player signs in',
      (tester) async {
    const email = 'ravi@example.com';
    final server = FakeSaves()
      ..saves[email] =
          const SaveData(wallet: 42000, owned: {'red'}, selected: 'red');
    final progress = PlayerProgress.memory();
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
      account: TestAccount(CloudSave(progress, server)),
    ));
    expect(find.text('Continue with Google'), findsOneWidget);
    expect(find.text('PLAY'), findsNothing);

    Future<void> signIn() async {
      await tester.ensureVisible(find.text('SIGN IN'));
      await tester.tap(find.text('SIGN IN'));
      await tester.pump();
      await tester.pump();
    }

    await signIn();
    expect(find.text('Enter your email and password.'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, email);
    await tester.enterText(find.byType(TextField).last, 'oops');
    await signIn();
    expect(find.text('Wrong email or password.'), findsOneWidget);

    await tester.enterText(find.byType(TextField).last, 'secret1');
    await signIn();
    expect(find.text('PLAY'), findsOneWidget);
    expect(find.text('42,000'), findsOneWidget);

    // The account button on the menu signs out again.
    await tester.tap(find.text(email));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.tap(find.text('SIGN OUT'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Continue with Google'), findsOneWidget);
    expect(progress.wallet, 0);
    expect(server.saves[email]!.wallet, 42000);
  });
}
