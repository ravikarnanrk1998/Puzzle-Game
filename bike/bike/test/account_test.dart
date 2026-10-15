import 'dart:async';

import 'package:bike/account/cloud_save.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/catalog.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/save_data.dart';
import 'package:bike/shop/store_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'fakes.dart';

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
        diamonds: 5,
        owned: {'red', 'sun'},
        selected: 'sun',
        delivered: ['GPA.1'],
        dailyClaim: '2026-10-10',
      );
      const guest = SaveData(
        wallet: 50,
        diamonds: 2,
        owned: {'red', 'blue'},
        selected: 'blue',
        delivered: ['GPA.2'],
        dailyClaim: '2026-10-12',
      );
      final merged = cloud.merge(guest, guest: true);
      expect(merged.wallet, 350);
      expect(merged.diamonds, 7);
      expect(merged.dailyClaim, '2026-10-12',
          reason: 'the later claim counts, so a day is never claimed twice');
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

  test('PlayerProgress remembers unlocked stages and the one picked',
      () async {
    SharedPreferences.setMockInitialValues({});
    final p = await PlayerProgress.load();
    p
      ..unlockTrack(Catalog.track('mountain'))
      ..selectTrack(Catalog.track('mountain'));
    final nextLaunch = await PlayerProgress.load();
    expect(nextLaunch.tracks, {'mountain'});
    expect(nextLaunch.selectedTrack.id, 'mountain');
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

    test('an unlocked stage follows the account and is never lost', () async {
      final mountain = Catalog.track('mountain');
      await cloud.attach('ravi');
      final outOfDate = server.saves['ravi']!;
      phone.unlockTrack(mountain);
      await pumpEventQueue();
      expect(server.saves['ravi']!.tracks, {'mountain'});
      server.saves['ravi'] = outOfDate;
      server.notify();
      await pumpEventQueue();
      expect(phone.hasTrack(mountain), isTrue);
      expect(server.saves['ravi']!.tracks, {'mountain'},
          reason: 'the phone sends the stage again');

      // The same player on a new phone.
      cloud.detach();
      expect(phone.hasTrack(mountain), isFalse);
      await cloud.attach('ravi');
      expect(phone.hasTrack(mountain), isTrue);
    });

    test('diamonds and the daily claim follow the account', () async {
      await cloud.attach('ravi');
      phone
        ..claimDaily(DateTime(2026, 10, 12, 9))
        ..addDiamonds(10)
        ..spendDiamonds(1);
      await pumpEventQueue();
      expect(server.saves['ravi']!.diamonds, 10);
      expect(server.saves['ravi']!.dailyClaim, '2026-10-12');

      // The same player on a new phone.
      cloud.detach();
      expect(phone.diamonds, 0);
      await cloud.attach('ravi');
      expect(phone.diamonds, 10);
      expect(phone.canClaimDaily(DateTime(2026, 10, 12, 20)), isFalse,
          reason: 'today was already claimed on the other phone');
      expect(phone.canClaimDaily(DateTime(2026, 10, 13, 8)), isTrue);
    });

    test('deleting the account removes the save for good', () async {
      await cloud.attach('ravi');
      phone.addCoins(5000);
      await pumpEventQueue();
      await cloud.erase();
      await pumpEventQueue();
      expect(server.saves, isNot(contains('ravi')),
          reason: 'the phone must not send its copy back');
      // The account itself is gone next, which signs the player out.
      cloud.detach();
      expect(phone.wallet, 0);
      await cloud.attach('ravi');
      expect(phone.wallet, 0, reason: 'a new account starts from nothing');
    });

    test('a delete that fails half way loses nothing', () async {
      await cloud.attach('ravi');
      phone.addCoins(5000);
      await pumpEventQueue();
      // The save is deleted, but then deleting the account itself fails.
      await cloud.erase();
      await cloud.attach('ravi');
      await pumpEventQueue();
      expect(phone.wallet, 5000);
      expect(server.saves['ravi']!.wallet, 5000);
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

    // The account button on the menu offers exactly two things.
    Future<void> tapAndWait(Finder finder) async {
      await tester.tap(finder);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
    }

    await tapAndWait(find.text(email));
    expect(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.bySubtype<ButtonStyleButton>(),
      ),
      findsNWidgets(2),
    );
    expect(find.text('DELETE ACCOUNT'), findsOneWidget);
    await tapAndWait(find.text('LOG OUT'));
    expect(find.text('Continue with Google'), findsOneWidget);
    expect(progress.wallet, 0);
    expect(server.saves[email]!.wallet, 42000);
  });

  testWidgets('deleting the account asks first, then removes everything',
      (tester) async {
    const email = 'ravi@example.com';
    final server = FakeSaves()
      ..saves[email] =
          const SaveData(wallet: 42000, owned: {'red'}, selected: 'red');
    final progress = PlayerProgress.memory();
    final account = TestAccount(CloudSave(progress, server));
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
      account: account,
    ));
    await account.signInWithEmail(email, 'secret1');
    await tester.pump();
    await tester.pump();
    expect(find.text('42,000'), findsOneWidget);

    Future<void> tapAndWait(Finder finder) async {
      await tester.tap(finder);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
    }

    // Changing your mind keeps everything.
    await tapAndWait(find.text(email));
    await tapAndWait(find.text('DELETE ACCOUNT'));
    expect(find.text('Delete account?'), findsOneWidget);
    await tapAndWait(find.text('CANCEL'));
    expect(find.text('PLAY'), findsOneWidget);
    expect(server.saves[email]!.wallet, 42000);

    await tapAndWait(find.text(email));
    await tapAndWait(find.text('DELETE ACCOUNT'));
    await tapAndWait(find.text('DELETE'));
    expect(find.text('Enter your password to confirm.'), findsOneWidget);
    expect(server.saves[email]!.wallet, 42000);

    await tester.enterText(find.byType(TextField), 'secret1');
    await tapAndWait(find.text('DELETE'));
    expect(find.text('Continue with Google'), findsOneWidget);
    expect(find.text('Delete account?'), findsNothing);
    expect(server.saves, isNot(contains(email)));
    expect(progress.wallet, 0);
    expect(progress.owner, isNull);
    // Let the "deleted" message finish showing.
    await tester.pump(const Duration(seconds: 5));
    await tester.pump(const Duration(seconds: 1));
  });
}
