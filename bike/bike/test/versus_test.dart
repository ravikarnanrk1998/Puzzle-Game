import 'package:bike/account/cloud_save.dart';
import 'package:bike/main.dart';
import 'package:bike/shop/player_progress.dart';
import 'package:bike/shop/store_service.dart';
import 'package:bike/versus/match_backend.dart';
import 'package:bike/versus/versus_match.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fakes.dart';

void main() {
  group('VersusMatch', () {
    late FakeMatches server;
    late VersusMatch host;
    late VersusMatch guest;

    /// Every match opened by a test, closed again when it ends.
    final open = <VersusMatch>[];

    void closeAll() {
      for (final match in open) {
        match.dispose();
      }
      open.clear();
    }

    /// Ravi creates a code and Priya enters it.
    Future<void> bothJoin() async {
      host = await VersusMatch.create(server, uid: 'ravi', name: 'Ravi');
      guest = await VersusMatch.join(server, host.code,
          uid: 'priya', name: 'Priya');
      open.addAll([host, guest]);
    }

    setUp(() => server = FakeMatches());
    tearDown(closeAll);

    test('a friend joins with the code and both see each other', () async {
      host = await VersusMatch.create(server, uid: 'ravi', name: 'Ravi');
      open.add(host);
      await pumpEventQueue();
      expect(host.code, hasLength(6));
      expect(host.ready, isFalse);

      guest = await VersusMatch.join(server, host.code,
          uid: 'priya', name: 'Priya');
      open.add(guest);
      await pumpEventQueue();
      expect(host.ready, isTrue);
      expect(host.opponent!.name, 'Priya');
      expect(guest.opponent!.name, 'Ravi');
      expect(host.outcome, isNull);
    });

    test('a wrong, own or used code is refused', () async {
      await bothJoin();
      Future<VersusMatch> join(String code, String uid) =>
          VersusMatch.join(server, code, uid: uid, name: uid);
      await expectLater(join('999999', 'arun'), throwsA(isA<MatchError>()));
      await expectLater(join(host.code, 'arun'), throwsA(isA<MatchError>()),
          reason: 'the match already has two players');

      final lonely = await VersusMatch.create(server, uid: 'arun', name: 'A');
      open.add(lonely);
      await expectLater(join(lonely.code, 'arun'), throwsA(isA<MatchError>()),
          reason: 'nobody can join their own match');
    });

    test('the higher score wins once both have crashed', () async {
      await bothJoin();
      host.finish(900);
      guest.finish(400);
      await pumpEventQueue();
      expect(host.outcome, VersusOutcome.win);
      expect(guest.outcome, VersusOutcome.lose);
    });

    test('equal scores are a draw', () async {
      await bothJoin();
      host.finish(500);
      guest.finish(500);
      await pumpEventQueue();
      expect(host.outcome, VersusOutcome.draw);
      expect(guest.outcome, VersusOutcome.draw);
    });

    test('whoever is still riding wins the moment they pass a crashed score',
        () async {
      await bothJoin();
      await pumpEventQueue();
      guest.finish(300);
      await pumpEventQueue();
      host.report(300);
      expect(host.outcome, isNull, reason: 'level is not ahead yet');
      host.report(301);
      expect(host.outcome, VersusOutcome.win);
    });

    // The next two depend on the passing of time, which only testWidgets
    // can fast-forward.
    testWidgets('a crashed player waits, and loses if the other rides past',
        (tester) async {
      await bothJoin();
      await tester.pump();
      host.start();
      guest.start();
      host.finish(500);
      guest.report(200);
      await tester.pump(VersusMatch.sendEvery);
      expect(host.opponent!.score, 200);
      expect(host.outcome, isNull, reason: 'Priya is behind but still riding');

      guest.report(501);
      await tester.pump(VersusMatch.sendEvery);
      expect(host.outcome, VersusOutcome.lose);
      closeAll();
    });

    testWidgets('an opponent who goes silent has left, and that is a win',
        (tester) async {
      await bothJoin();
      await tester.pump();
      host.start();
      guest.start();
      await tester.pump(const Duration(seconds: 6));
      expect(host.opponentLeft, isFalse, reason: 'Priya is still sending');

      // Priya's game closes: nothing more is sent.
      open.remove(guest);
      guest.dispose();
      await tester.pump(VersusMatch.silenceLimit);
      await tester.pump(const Duration(seconds: 1));
      expect(host.opponentLeft, isTrue);
      expect(host.outcome, VersusOutcome.win);
      closeAll();
    });

    test('the host leaving closes the match', () async {
      await bothJoin();
      await pumpEventQueue();
      host.leave();
      await pumpEventQueue();
      expect(server.matches, isEmpty);
      expect(guest.opponentLeft, isTrue);
      expect(guest.outcome, VersusOutcome.win);
    });

    test('a guest leaving mid-run gives up with the score they have',
        () async {
      await bothJoin();
      await pumpEventQueue();
      guest.report(120);
      guest.leave();
      await pumpEventQueue();
      expect(host.opponent!.done, isTrue);
      host.report(121);
      expect(host.outcome, VersusOutcome.win);
    });
  });

  testWidgets('1 VS 1: create a code, a friend joins, the higher score wins',
      (tester) async {
    final matches = FakeMatches();
    final progress = PlayerProgress.memory();
    final account = TestAccount(CloudSave(progress, FakeSaves()));
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
      account: account,
      matches: matches,
    ));
    await account.signInWithEmail('ravi@example.com', 'secret1');
    await tester.pump();
    await tester.pump();

    /// Lets the game run for a while, a frame at a time like a real phone.
    Future<void> ride(double seconds) async {
      for (var t = 0.0; t < seconds; t += 1 / 30) {
        await tester.pump(const Duration(milliseconds: 34));
      }
    }

    // Today's free diamond pays for the code.
    await tester.tap(find.text('CLAIM DAILY +1'));
    await ride(0.2);
    expect(progress.diamonds, 1);
    expect(find.text('CLAIM DAILY +1'), findsNothing);

    await tester.tap(find.text('1 VS 1'));
    await ride(0.6);
    await tester.tap(find.text('CREATE CODE'));
    await ride(0.2);
    expect(find.text('100000'), findsOneWidget);
    expect(find.text('Waiting for your friend…'), findsOneWidget);
    expect(progress.diamonds, 0);

    // The friend enters the code on their own phone.
    final friend = await VersusMatch.join(matches, '100000',
        uid: 'priya', name: 'Priya');
    await ride(0.6);
    expect(find.text('YOU  vs  Priya'), findsOneWidget);
    expect(find.text('GET READY'), findsOneWidget);
    expect(matches.matches['100000']!.host.name, 'ravi',
        reason: 'other players never see the whole email address');

    // After the countdown both are riding, with the rival's score on screen.
    await ride(3.2);
    expect(find.text('SCORE'), findsOneWidget);
    expect(find.text('Priya'), findsOneWidget);

    // The friend crashes straight away; riding past their score wins.
    friend
      ..start()
      ..finish(0);
    await ride(1.5);
    expect(find.text('YOU WIN!'), findsOneWidget);
    expect(find.text('PLAY AGAIN'), findsNothing,
        reason: 'a match run cannot be started over');

    await tester.tap(find.text('MENU'));
    await ride(0.3);
    expect(find.text('PLAY'), findsOneWidget);
    expect(matches.matches, isEmpty, reason: 'the host closes the match');
    expect(progress.diamonds, 0, reason: 'a played match is not paid back');
    friend
      ..leave()
      ..dispose();
    // Let the daily claim message finish showing.
    await tester.pump(const Duration(seconds: 5));
    await tester.pump(const Duration(seconds: 1));
  });

  testWidgets('creating a code costs a diamond, paid back if nobody joins',
      (tester) async {
    final matches = FakeMatches();
    final progress = PlayerProgress.memory();
    final account = TestAccount(CloudSave(progress, FakeSaves()));
    await tester.pumpWidget(BikeRushApp(
      progress: progress,
      store: StoreService(progress, enabled: false),
      account: account,
      matches: matches,
    ));
    await account.signInWithEmail('ravi@example.com', 'secret1');
    await tester.pump();
    await tester.pump();

    Future<void> tapAndWait(Finder finder) async {
      await tester.tap(finder);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 400));
    }

    // No diamonds: no code.
    await tapAndWait(find.text('1 VS 1'));
    await tapAndWait(find.text('CREATE CODE'));
    expect(find.textContaining('You need 1 diamond'), findsOneWidget);
    expect(find.text('GET DIAMONDS'), findsOneWidget);
    expect(matches.matches, isEmpty);

    progress.addDiamonds(1);
    await tester.pump();
    expect(find.text('GET DIAMONDS'), findsNothing);
    await tapAndWait(find.text('CREATE CODE'));
    expect(find.text('100000'), findsOneWidget);
    expect(progress.diamonds, 0);

    await tapAndWait(find.text('CANCEL'));
    expect(progress.diamonds, 1, reason: 'nobody joined, so it is paid back');
    expect(matches.matches, isEmpty);

    // Going back instead of pressing CANCEL pays back too.
    await tapAndWait(find.text('CREATE CODE'));
    expect(progress.diamonds, 0);
    await tester.pageBack();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));
    expect(find.text('PLAY'), findsOneWidget);
    expect(progress.diamonds, 1);
    expect(matches.matches, isEmpty);
  });
}
