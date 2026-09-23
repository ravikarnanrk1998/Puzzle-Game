import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'shop/player_progress.dart';
import 'shop/store_service.dart';
import 'ui/game_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  final progress = await PlayerProgress.load();
  // Start listening for purchases as early as possible so none are missed.
  final store = StoreService(progress)..init();
  runApp(BikeRushApp(progress: progress, store: store));
}

class BikeRushApp extends StatelessWidget {
  const BikeRushApp({super.key, required this.progress, required this.store});

  final PlayerProgress progress;
  final StoreService store;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Bike Rush',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        colorSchemeSeed: Colors.orange,
      ),
      home: GameScreen(progress: progress, store: store),
    );
  }
}
