import 'package:flutter/material.dart';

/// The shape a ride is drawn with. The first four are bikes, the rest cars.
enum RideBody {
  street,
  dirt,
  chopper,
  superbike,
  sedan(car: true),
  sports(car: true),
  muscle(car: true),
  hyper(car: true),
  monster(car: true);

  const RideBody({this.car = false});

  final bool car;
}

/// A bike or car the player can ride. Free, bought with coins, or bought
/// with real money (when [productId] is set).
class Ride {
  const Ride({
    required this.id,
    required this.name,
    required this.color,
    required this.accent,
    this.body = RideBody.street,
    this.coinPrice = 0,
    this.productId,
    this.perk,
    this.startNitro = 0.4,
    this.startShield = 0,
    this.startLives = 3,
    this.glow = false,
    this.infiniteNitro = false,
  });

  final String id;
  final String name;
  final Color color;
  final Color accent;
  final RideBody body;

  /// Nitro that never runs out: once boosting, it stays boosting, and so
  /// smashes through everything, until the run is finished.
  final bool infiniteNitro;
  final int coinPrice;

  /// Google Play product id for premium rides.
  final String? productId;
  final String? perk;
  final double startNitro;
  final double startShield;
  final int startLives;
  final bool glow;

  bool get isPremium => productId != null;
  bool get isCar => body.car;

  /// Its size on the road, in world units: what it is drawn as, and what
  /// it crashes into things with.
  double get width => switch (body) {
        RideBody.monster => 44,
        _ => isCar ? 38 : 30,
      };
  double get height => switch (body) {
        RideBody.monster => 80,
        _ => isCar ? 76 : 62,
      };

  /// Its colour for a label or border on the dark menus. A very dark ride
  /// would vanish there, so its accent shows instead.
  Color get labelColor => color.computeLuminance() < 0.05 ? accent : color;
}

/// What a skin paints over a ride.
enum SkinPattern {
  none,
  stripes,
  flames,
  lightning,
  tiger,
  camo,
  checker,
  galaxy,
  chrome,
}

/// A paint job, worn by whichever bike or car is being ridden. Free, bought
/// with coins, or bought with real money (when [productId] is set).
class Skin {
  const Skin({
    required this.id,
    required this.name,
    required this.description,
    this.pattern = SkinPattern.none,
    this.colors = const [],
    this.base,
    this.coinPrice = 0,
    this.productId,
  });

  final String id;
  final String name;
  final String description;
  final SkinPattern pattern;

  /// The pattern's colours, the main one first.
  final List<Color> colors;

  /// Paints the whole ride this colour first, instead of its own.
  final Color? base;
  final int coinPrice;

  /// Google Play product id for premium skins.
  final String? productId;

  bool get isPremium => productId != null;
}

/// How a track looks, and for [mountain] also how it plays: the road has
/// rocks, barriers, potholes, oil slicks and jump ramps on it.
enum TrackTheme { highway, mountain }

/// Which way the traffic on a track drives.
enum TrafficFlow {
  /// All of it the same way as the bike, which overtakes it.
  sameWay,

  /// All of it towards the bike.
  oncoming,

  /// The left half of the lanes the same way as the bike, the right half
  /// towards it, with a line down the middle of the road.
  twoWay,
}

/// A road to ride: one stage of the game. The first is open from the start.
/// Each one after it unlocks by completing [unlockLevel] levels in a run on
/// the one before, or straight away for diamonds (and for some, coins).
class Track {
  const Track({
    required this.id,
    required this.stage,
    required this.name,
    required this.description,
    required this.theme,
    this.traffic = TrafficFlow.sameWay,
    this.unlockLevel = 0,
    this.coinPrice = 0,
    this.diamondPrice = 0,
  });

  final String id;

  /// 1 for the first track, 2 for the next, ...
  final int stage;
  final String name;
  final String description;
  final TrackTheme theme;
  final TrafficFlow traffic;
  final int unlockLevel;

  /// Coins it costs to unlock straight away. 0 when it can't be bought
  /// with coins.
  final int coinPrice;
  final int diamondPrice;

  bool get isFree => unlockLevel == 0;
  bool get hasObstacles => theme == TrackTheme.mountain;
}

class CoinPack {
  const CoinPack(this.productId, this.coins, {this.badge});

  final String productId;
  final int coins;
  final String? badge;
}

class DiamondPack {
  const DiamondPack(this.productId, this.diamonds, {this.badge});

  final String productId;
  final int diamonds;
  final String? badge;
}

/// Everything sold in the game. The product ids here must match the in-app
/// products created in Google Play Console exactly.
class Catalog {
  static const String doubleCoinsId = 'double_coins';

  /// Diamonds it costs to create a 1 vs 1 match. Joining one is free.
  static const int versusCost = 1;

  static const List<Ride> bikes = [
    Ride(
      id: 'red',
      name: 'Red Rocket',
      color: Color(0xFFE53935),
      accent: Color(0xFFFFCDD2),
    ),
    Ride(
      id: 'blue',
      name: 'Blue Bolt',
      color: Color(0xFF1E88E5),
      accent: Color(0xFFBBDEFB),
      coinPrice: 5000,
    ),
    Ride(
      id: 'dirt',
      name: 'Dirt Hopper',
      body: RideBody.dirt,
      color: Color(0xFF7CB342),
      accent: Color(0xFFF1F8E9),
      coinPrice: 20000,
      perk: 'Off-road racer, starts with 50% nitro',
      startNitro: 0.5,
    ),
    Ride(
      id: 'sun',
      name: 'Sunburst',
      color: Color(0xFFFFB300),
      accent: Color(0xFFFFF8E1),
      coinPrice: 50000,
    ),
    Ride(
      id: 'purple',
      name: 'Purple Phantom',
      color: Color(0xFF8E24AA),
      accent: Color(0xFFE1BEE7),
      coinPrice: 100000,
      perk: 'Starts with 60% nitro',
      startNitro: 0.6,
    ),
    Ride(
      id: 'chopper',
      name: 'Thunder Chopper',
      body: RideBody.chopper,
      color: Color(0xFF37474F),
      accent: Color(0xFFFF6E40),
      coinPrice: 250000,
      perk: '4 lives + 60% nitro at start',
      startLives: 4,
      startNitro: 0.6,
    ),
    Ride(
      id: 'aqua',
      name: 'Aqua Racer',
      color: Color(0xFF00ACC1),
      accent: Color(0xFFE0F7FA),
      coinPrice: 500000,
      perk: 'Starts with 4 lives',
      startLives: 4,
    ),
    Ride(
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
    Ride(
      id: 'neon',
      name: 'Neon Ghost',
      color: Color(0xFF263238),
      accent: Color(0xFF76FF03),
      productId: 'bike_neons',
      perk: '5 lives + 8s shield at start',
      startLives: 5,
      startShield: 8,
      startNitro: 0.6,
      glow: true,
    ),
    Ride(
      id: 'inferno',
      name: 'Inferno R1',
      body: RideBody.superbike,
      color: Color(0xFFD50000),
      accent: Color(0xFFFFD600),
      productId: 'bike_inferno',
      perk: '5 lives + full nitro + 5s shield',
      startLives: 5,
      startNitro: 1,
      startShield: 5,
      glow: true,
    ),
  ];

  /// Cars are bigger than bikes, so harder to squeeze past traffic with.
  /// Every one makes up for it with extra lives or nitro. The monster truck
  /// is the biggest, with nitro that never runs out.
  static const List<Ride> cars = [
    Ride(
      id: 'cruiser',
      name: 'Street Cruiser',
      body: RideBody.sedan,
      color: Color(0xFF00B8D4),
      accent: Color(0xFFE0F7FA),
      coinPrice: 30000,
      perk: 'Steel body: starts with 4 lives',
      startLives: 4,
    ),
    Ride(
      id: 'turbo',
      name: 'Turbo GT',
      body: RideBody.sports,
      color: Color(0xFFC6FF00),
      accent: Color(0xFF212121),
      coinPrice: 150000,
      perk: '4 lives + 80% nitro at start',
      startLives: 4,
      startNitro: 0.8,
    ),
    Ride(
      id: 'muscle',
      name: 'Muscle Beast',
      body: RideBody.muscle,
      color: Color(0xFF212121),
      accent: Color(0xFFFFD600),
      coinPrice: 750000,
      perk: '5 lives + 3s shield at start',
      startLives: 5,
      startShield: 3,
    ),
    Ride(
      id: 'hyper',
      name: 'Hyper X',
      body: RideBody.hyper,
      color: Color(0xFFFF3D00),
      accent: Color(0xFFFFEA00),
      productId: 'car_hyper',
      perk: '5 lives + full nitro + 8s shield',
      startLives: 5,
      startNitro: 1,
      startShield: 8,
      glow: true,
    ),
    Ride(
      id: 'monster',
      name: 'Monster Truck',
      body: RideBody.monster,
      color: Color(0xFF6A1B9A),
      accent: Color(0xFF00E5FF),
      productId: 'car_monster',
      perk: 'INFINITY NITRO: boost for ever and smash it all '
          '(not in 1 VS 1)',
      startNitro: 1,
      infiniteNitro: true,
      glow: true,
    ),
  ];

  /// Every bike, then every car.
  static const List<Ride> rides = [...bikes, ...cars];

  /// The first is free and worn until another is picked.
  static const List<Skin> skins = [
    Skin(
      id: 'stock',
      name: 'Factory Paint',
      description: 'Your ride in its own colours',
    ),
    Skin(
      id: 'stripes',
      name: 'Racing Stripes',
      description: 'Two white stripes, nose to tail',
      pattern: SkinPattern.stripes,
      colors: [Color(0xFFFFFFFF)],
      coinPrice: 3000,
    ),
    Skin(
      id: 'flames',
      name: 'Hot Flames',
      description: 'Flames licking back from the front',
      pattern: SkinPattern.flames,
      colors: [Color(0xFFFF6D00), Color(0xFFFFD600)],
      coinPrice: 15000,
    ),
    Skin(
      id: 'lightning',
      name: 'Lightning Bolt',
      description: 'A yellow bolt from front to back',
      pattern: SkinPattern.lightning,
      colors: [Color(0xFFFFEA00), Color(0x99000000)],
      coinPrice: 40000,
    ),
    Skin(
      id: 'tiger',
      name: 'Tiger Claw',
      description: 'Orange with black tiger stripes',
      pattern: SkinPattern.tiger,
      base: Color(0xFFFF8F00),
      colors: [Color(0xFF1A1A1A)],
      coinPrice: 80000,
    ),
    Skin(
      id: 'camo',
      name: 'Jungle Camo',
      description: 'Army camouflage in green and brown',
      pattern: SkinPattern.camo,
      base: Color(0xFF6B7F3A),
      colors: [Color(0xFF3E4A22), Color(0xFF8D7B4F), Color(0xFF2B2B1F)],
      coinPrice: 150000,
    ),
    Skin(
      id: 'checker',
      name: 'Checkered Flag',
      description: 'Black and white, like the finish line',
      pattern: SkinPattern.checker,
      base: Color(0xFFFFFFFF),
      colors: [Color(0xFF212121)],
      coinPrice: 300000,
    ),
    Skin(
      id: 'chrome',
      name: 'Liquid Chrome',
      description: 'Polished silver that shines',
      pattern: SkinPattern.chrome,
      base: Color(0xFFB0BEC5),
      colors: [Color(0xCCFFFFFF), Color(0x55263238)],
      productId: 'skin_chrome',
    ),
    Skin(
      id: 'galaxy',
      name: 'Galaxy',
      description: 'Deep space, purple clouds and stars',
      pattern: SkinPattern.galaxy,
      base: Color(0xFF1A1036),
      colors: [Color(0x99AA00FF), Color(0x88FF4081), Color(0xFFFFFFFF)],
      productId: 'skin_galaxy',
    ),
  ];

  static const List<Track> tracks = [
    Track(
      id: 'highway',
      stage: 1,
      name: 'Green Highway',
      description: 'Dodge the traffic on the open road',
      theme: TrackTheme.highway,
    ),
    Track(
      id: 'mountain',
      stage: 2,
      name: 'Mountain Pass',
      description: 'Rocks, barriers, potholes, oil slicks and jump ramps',
      theme: TrackTheme.mountain,
      unlockLevel: 20,
      coinPrice: 100000,
      diamondPrice: 100,
    ),
    Track(
      id: 'highway_oncoming',
      stage: 3,
      name: 'Head-On Highway',
      description: 'The Green Highway, with all the traffic coming at you',
      theme: TrackTheme.highway,
      traffic: TrafficFlow.oncoming,
      unlockLevel: 10,
      diamondPrice: 150,
    ),
    Track(
      id: 'mountain_oncoming',
      stage: 4,
      name: 'Head-On Pass',
      description: 'The Mountain Pass, with all the traffic coming at you',
      theme: TrackTheme.mountain,
      traffic: TrafficFlow.oncoming,
      unlockLevel: 10,
      diamondPrice: 200,
    ),
    Track(
      id: 'two_way',
      stage: 5,
      name: 'Two-Way Road',
      description: 'The two left lanes go your way, and the two right lanes '
          'come at you',
      theme: TrackTheme.highway,
      traffic: TrafficFlow.twoWay,
      unlockLevel: 10,
      diamondPrice: 250,
    ),
    Track(
      id: 'mountain_two_way',
      stage: 6,
      name: 'Two-Way Pass',
      description: 'The Mountain Pass, with the two left lanes going your '
          'way and the two right lanes coming at you',
      theme: TrackTheme.mountain,
      traffic: TrafficFlow.twoWay,
      unlockLevel: 10,
      diamondPrice: 300,
    ),
  ];

  static const List<CoinPack> coinPacks = [
    CoinPack('coins_small', 10000),
    CoinPack('coins_medium', 30000, badge: 'POPULAR'),
    CoinPack('coins_large', 80000, badge: 'BEST VALUE'),
  ];

  static const List<DiamondPack> diamondPacks = [
    DiamondPack('diamonds_1', 1),
    DiamondPack('diamonds_10', 10),
    DiamondPack('diamonds_20', 20, badge: 'POPULAR'),
    DiamondPack('diamonds_50', 50),
    DiamondPack('diamonds_100', 100, badge: 'BEST VALUE'),
  ];

  static Set<String> get productIds => {
        for (final p in coinPacks) p.productId,
        for (final p in diamondPacks) p.productId,
        doubleCoinsId,
        for (final r in rides)
          if (r.productId != null) r.productId!,
        for (final s in skins)
          if (s.productId != null) s.productId!,
      };

  static Ride ride(String id) =>
      rides.firstWhere((r) => r.id == id, orElse: () => rides.first);

  static Skin skin(String id) =>
      skins.firstWhere((s) => s.id == id, orElse: () => skins.first);

  static Track track(String id) =>
      tracks.firstWhere((t) => t.id == id, orElse: () => tracks.first);

  /// The track that riding [track] unlocks, or null for the last one.
  static Track? nextTrack(Track track) {
    final i = tracks.indexOf(track);
    return i >= 0 && i + 1 < tracks.length ? tracks[i + 1] : null;
  }

  static Ride? rideForProduct(String productId) {
    for (final r in rides) {
      if (r.productId == productId) return r;
    }
    return null;
  }

  static Skin? skinForProduct(String productId) {
    for (final s in skins) {
      if (s.productId == productId) return s;
    }
    return null;
  }

  static CoinPack? coinPack(String productId) {
    for (final p in coinPacks) {
      if (p.productId == productId) return p;
    }
    return null;
  }

  static DiamondPack? diamondPack(String productId) {
    for (final p in diamondPacks) {
      if (p.productId == productId) return p;
    }
    return null;
  }

  static bool isConsumable(String productId) =>
      coinPack(productId) != null || diamondPack(productId) != null;
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
