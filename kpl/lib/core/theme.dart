import 'package:flutter/material.dart';

/// Night-broadcast palette: deep midnight blues with gold + neon accents.
class AppColors {
  static const bg0 = Color(0xFF03040B);
  static const bg1 = Color(0xFF080C24);
  static const bg2 = Color(0xFF10164A);
  static const bg3 = Color(0xFF1A2266);
  static const glass = Color(0x14FFFFFF);
  static const glassStrong = Color(0x26FFFFFF);
  static const stroke = Color(0x2EFFFFFF);
  static const strokeSoft = Color(0x1AFFFFFF);
  static const gold = Color(0xFFFFC84A);
  static const goldLight = Color(0xFFFFE9A3);
  static const goldDeep = Color(0xFFFF8A00);
  static const cyan = Color(0xFF38E8FF);
  static const violet = Color(0xFF8B5CFF);
  static const pink = Color(0xFFFF3D9A);
  static const green = Color(0xFF2EE59D);
  static const red = Color(0xFFFF4D5E);
  static const text = Color(0xFFF3F5FF);
  static const textDim = Color(0xFFB4BCE0);
  static const textMute = Color(0xFF6E77A0);
}

class AppGradients {
  static const gold = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [AppColors.goldLight, AppColors.gold, AppColors.goldDeep],
  );
  static const goldFlat = LinearGradient(
    colors: [AppColors.gold, AppColors.goldDeep],
  );
  static const neon = LinearGradient(
    colors: [AppColors.cyan, AppColors.violet, AppColors.pink],
  );
  static const page = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [AppColors.bg2, AppColors.bg1, AppColors.bg0],
  );
}

/// Typography helpers. Teko = condensed broadcast numerals, Rajdhani = UI.
class T {
  static TextStyle display(
    double size, {
    Color color = AppColors.text,
    FontWeight weight = FontWeight.w600,
    double height = 0.95,
    double spacing = 0.6,
    List<Shadow>? shadows,
  }) =>
      TextStyle(
        fontFamily: 'Teko',
        fontSize: size,
        fontWeight: weight,
        color: color,
        height: height,
        letterSpacing: spacing,
        shadows: shadows,
      );

  static TextStyle ui(
    double size, {
    Color color = AppColors.text,
    FontWeight weight = FontWeight.w600,
    double? height,
    double spacing = 0.2,
  }) =>
      TextStyle(
        fontFamily: 'Rajdhani',
        fontSize: size,
        fontWeight: weight,
        color: color,
        height: height,
        letterSpacing: spacing,
      );

  static TextStyle label(double size, {Color color = AppColors.textDim}) =>
      TextStyle(
        fontFamily: 'Rajdhani',
        fontSize: size,
        fontWeight: FontWeight.w700,
        color: color,
        letterSpacing: 1.6,
      );
}

extension ColorX on Color {
  Color o(double alpha) => withValues(alpha: alpha.clamp(0.0, 1.0));
  Color darken([double t = 0.25]) => Color.lerp(this, Colors.black, t)!;
  Color lighten([double t = 0.25]) => Color.lerp(this, Colors.white, t)!;

  /// Perceived brightness 0..1, used to pick readable text on team colours.
  double get luma => 0.299 * r + 0.587 * g + 0.114 * b;
  Color get onColor => luma > 0.6 ? const Color(0xFF10131F) : Colors.white;
}

ThemeData buildTheme() {
  final base = ThemeData(
    useMaterial3: true,
    brightness: Brightness.dark,
    fontFamily: 'Rajdhani',
    scaffoldBackgroundColor: AppColors.bg0,
    colorScheme: const ColorScheme.dark(
      primary: AppColors.gold,
      secondary: AppColors.cyan,
      surface: AppColors.bg1,
      error: AppColors.red,
    ),
    splashFactory: InkSparkle.splashFactory,
  );
  return base.copyWith(
    textTheme: base.textTheme.apply(
      fontFamily: 'Rajdhani',
      bodyColor: AppColors.text,
      displayColor: AppColors.text,
    ),
    dialogTheme: const DialogThemeData(backgroundColor: AppColors.bg1),
    snackBarTheme: SnackBarThemeData(
      backgroundColor: AppColors.bg3,
      contentTextStyle: T.ui(15),
      behavior: SnackBarBehavior.floating,
    ),
    pageTransitionsTheme: const PageTransitionsTheme(
      builders: {
        TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
        TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
        TargetPlatform.windows: FadeForwardsPageTransitionsBuilder(),
        TargetPlatform.linux: FadeForwardsPageTransitionsBuilder(),
        TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
      },
    ),
  );
}
