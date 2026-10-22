import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'core/display.dart';
import 'core/league.dart';
import 'core/theme.dart';
import 'engine/career.dart';
import 'game/ground.dart';
import 'ui/screens/splash_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await lockLandscape();
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.light,
    systemNavigationBarColor: Colors.transparent,
    systemNavigationBarIconBrightness: Brightness.light,
  ));
  await Future.wait([Career.instance.loadSettings(), GroundRenderer.load()]);
  runApp(const KplApp());
}

class KplApp extends StatelessWidget {
  const KplApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: '${League.name} ${League.startYear} - Auction & Season',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
      builder: (context, child) => PhoneFrame(child: child ?? const SizedBox()),
      home: const SplashScreen(),
    );
  }
}

/// On desktop / web the game is shown in a landscape phone-shaped frame.
/// Phones use the full screen.
class PhoneFrame extends StatelessWidget {
  const PhoneFrame({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (isMobilePlatform) return child;
    final mq = MediaQuery.of(context);
    final w = mq.size.width, h = mq.size.height;
    final fw = w.clamp(0.0, h * 2.1);
    final frame = Size(fw, (fw / 2.1).clamp(0.0, h));
    if ((frame.width - w).abs() < 2 && (frame.height - h).abs() < 2) return child;
    return ColoredBox(
      color: AppColors.bg0,
      child: Center(
        child: Container(
          width: frame.width,
          height: frame.height,
          decoration: BoxDecoration(boxShadow: [BoxShadow(color: AppColors.violet.o(0.35), blurRadius: 60)]),
          child: ClipRect(
            child: MediaQuery(data: mq.copyWith(size: frame), child: child),
          ),
        ),
      ),
    );
  }
}
