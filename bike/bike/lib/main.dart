import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'account/account_service.dart';
import 'audio/game_music.dart';
import 'shop/player_progress.dart';
import 'shop/store_service.dart';
import 'ui/game_screen.dart';
import 'ui/login_screen.dart';
import 'versus/match_backend.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  final progress = await PlayerProgress.load();
  // Start listening for purchases as early as possible so none are missed.
  final store = StoreService(progress)..init();
  final account = await AccountService.start(progress);
  runApp(BikeRushApp(
    progress: progress,
    store: store,
    music: GameMusic(),
    account: account,
    matches: account == null ? null : FirestoreMatches(),
  ));
}

class BikeRushApp extends StatelessWidget {
  const BikeRushApp({
    super.key,
    required this.progress,
    required this.store,
    this.music,
    this.account,
    this.matches,
  });

  final PlayerProgress progress;
  final StoreService store;
  final GameMusic? music;

  /// Null means no sign-in: the game opens straight away and saves on the
  /// device only.
  final AccountService? account;

  /// Where 1 vs 1 matches are kept. Null means no 1 vs 1.
  final MatchBackend? matches;

  @override
  Widget build(BuildContext context) {
    final account = this.account;
    final game = GameScreen(
      progress: progress,
      store: store,
      music: music,
      account: account,
      matches: matches,
    );
    return MaterialApp(
      title: 'Bike Rush',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        colorSchemeSeed: Colors.orange,
      ),
      home: account == null
          ? game
          : ListenableBuilder(
              listenable: account,
              builder: (context, _) {
                if (!account.signedIn) return LoginScreen(account: account);
                if (account.loading) return const LoadingSaveScreen();
                return game;
              },
            ),
    );
  }
}
