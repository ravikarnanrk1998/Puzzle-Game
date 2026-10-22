import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

bool get isMobilePlatform =>
    !kIsWeb && (defaultTargetPlatform == TargetPlatform.android || defaultTargetPlatform == TargetPlatform.iOS);

/// The whole game runs in landscape, full screen.
Future<void> lockLandscape() async {
  await SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
  await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
}
