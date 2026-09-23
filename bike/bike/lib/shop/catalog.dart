import 'package:flutter/material.dart';

/// A bike the player can ride. Free, bought with coins, or bought with
/// real money (when [productId] is set).
class BikeSkin {
  const BikeSkin({
    required this.id,
    required this.name,
    required this.color,
    required this.accent,
    this.coinPrice = 0,
    this.productId,
    this.perk,
    this.startNitro = 0.4,
    this.startShield = 0,
    this.startLives = 3,
    this.glow = false,
  });

  final String id;
  final String name;
  final Color color;
  final Color accent;
  final int coinPrice;

  /// Google Play product id for premium bikes.
  final String? productId;
  final String? perk;
  final double startNitro;
  final double startShield;
  final int startLives;
  final bool glow;

  bool get isPremium => productId != null;
}

class CoinPack {
  const CoinPack(this.productId, this.coins, {this.badge});

  final String productId;
  final int coins;
  final String? badge;
}

/// Everything sold in the game. The product ids here must match the in-app
/// products created in Google Play Console exactly.
class Catalog {
  static const String doubleCoinsId = 'double_coins';

  static const List<BikeSkin> bikes = [
    BikeSkin(
      id: 'red',
      name: 'Red Rocket',
      color: Color(0xFFE53935),
      accent: Color(0xFFFFCDD2),
    ),
    BikeSkin(
      id: 'blue',
      name: 'Blue Bolt',
      color: Color(0xFF1E88E5),
      accent: Color(0xFFBBDEFB),
      coinPrice: 500,
    ),
    BikeSkin(
      id: 'sun',
      name: 'Sunburst',
      color: Color(0xFFFFB300),
      accent: Color(0xFFFFF8E1),
      coinPrice: 1200,
    ),
    BikeSkin(
      id: 'purple',
      name: 'Purple Phantom',
      color: Color(0xFF8E24AA),
      accent: Color(0xFFE1BEE7),
      coinPrice: 2500,
      perk: 'Starts with 60% nitro',
      startNitro: 0.6,
    ),
    BikeSkin(
      id: 'aqua',
      name: 'Aqua Racer',
      color: Color(0xFF00ACC1),
      accent: Color(0xFFE0F7FA),
      coinPrice: 4000,
      perk: 'Starts with 4 lives',
      startLives: 4,
    ),
    BikeSkin(
      id: 'gold',
      name: 'Gold Rush',
      color: Color(0xFFFFC107),
      accent: Color(0xFFFFFDE7),
      productId: 'bike_gold',
      perk: 'Full nitro + 5s shield at start',
      startNitro: 1,
      startShield: 5,
      glow: true,
    ),
    BikeSkin(
      id: 'neon',
      name: 'Neon Ghost',
      color: Color(0xFF263238),
      accent: Color(0xFF76FF03),
      productId: 'bike_neon',
      perk: '5 lives + 8s shield at start',
      startLives: 5,
      startShield: 8,
      startNitro: 0.6,
      glow: true,
    ),
  ];

  static const List<CoinPack> coinPacks = [
    CoinPack('coins_small', 2000),
    CoinPack('coins_medium', 6000, badge: 'POPULAR'),
    CoinPack('coins_large', 15000, badge: 'BEST VALUE'),
  ];

  static Set<String> get productIds => {
        for (final p in coinPacks) p.productId,
        doubleCoinsId,
        for (final b in bikes)
          if (b.productId != null) b.productId!,
      };

  static BikeSkin bike(String id) =>
      bikes.firstWhere((b) => b.id == id, orElse: () => bikes.first);

  static BikeSkin? bikeForProduct(String productId) {
    for (final b in bikes) {
      if (b.productId == productId) return b;
    }
    return null;
  }

  static CoinPack? coinPack(String productId) {
    for (final p in coinPacks) {
      if (p.productId == productId) return p;
    }
    return null;
  }

  static bool isConsumable(String productId) => coinPack(productId) != null;
}

/// 15000 -> "15,000"
String formatNumber(int n) {
  final s = n.abs().toString();
  final out = StringBuffer(n < 0 ? '-' : '');
  for (var i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 == 0) out.write(',');
    out.write(s[i]);
  }
  return out.toString();
}
