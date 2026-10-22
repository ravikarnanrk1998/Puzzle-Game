import 'dart:math' as math;

enum Role { bat, wk, ar, bowl }

extension RoleX on Role {
  String get code => const ['BAT', 'WK', 'AR', 'BOWL'][index];
  String get label =>
      const ['Batter', 'Wicketkeeper', 'All-rounder', 'Bowler'][index];
}

enum BowlStyle { none, rf, rfm, rm, lf, lfm, lm, ob, lb, sla, lws }

extension BowlStyleX on BowlStyle {
  bool get isPace => index >= 1 && index <= 6;
  bool get isSpin => index >= 7;
  bool get leftArm =>
      this == BowlStyle.lf ||
      this == BowlStyle.lfm ||
      this == BowlStyle.lm ||
      this == BowlStyle.sla ||
      this == BowlStyle.lws;
  String get code => const [
        '-',
        'RF',
        'RFM',
        'RM',
        'LF',
        'LFM',
        'LM',
        'OB',
        'LB',
        'SLA',
        'LWS',
      ][index];
  String get label => const [
        '—',
        'Right-arm fast',
        'Right-arm fast-medium',
        'Right-arm medium',
        'Left-arm fast',
        'Left-arm fast-medium',
        'Left-arm medium',
        'Right-arm off-break',
        'Right-arm leg-break',
        'Left-arm orthodox',
        'Left-arm wrist-spin',
      ][index];

  /// Typical release speed in km/h.
  double get baseSpeed =>
      const [0.0, 145.0, 138.0, 129.0, 143.0, 136.0, 127.0, 88.0, 86.0, 90.0, 85.0][index];

  /// Physical direction of the stock delivery's turn in world x
  /// (+x = towards a right-hander's leg side).
  double get stockTurn {
    switch (this) {
      case BowlStyle.ob:
      case BowlStyle.lws:
        return 1;
      case BowlStyle.lb:
      case BowlStyle.sla:
        return -1;
      default:
        return 0;
    }
  }
}

class Player {
  const Player({
    required this.id,
    required this.name,
    required this.country,
    required this.role,
    required this.leftBat,
    required this.bowlStyle,
    required this.bat,
    required this.bowl,
    required this.power,
    required this.capped,
    required this.team26,
    required this.price26,
    required this.jersey,
  });

  final int id;
  final String name;
  final String country;
  final Role role;
  final bool leftBat;
  final BowlStyle bowlStyle;

  /// Batting skill (T20), 0-99.
  final int bat;

  /// Bowling skill (T20), 0-99.
  final int bowl;

  /// Hitting power / intent, 0-99. High = explosive strike rate.
  final int power;
  final bool capped;

  /// IPL 2026 franchise id, or null for players outside the 2026 squads.
  final String? team26;

  /// IPL 2026 salary in lakh (for pool players this is their base price).
  final int price26;
  final int jersey;

  bool get overseas => country != 'IND';
  bool get isPace => bowlStyle.isPace;
  bool get isSpin => bowlStyle.isSpin;
  bool get isKeeper => role == Role.wk;
  bool get canBowl => bowlStyle != BowlStyle.none && bowl >= 40;
  bool get isBowlingOption => bowlStyle != BowlStyle.none && bowl >= 58;

  int get overall {
    final b = bat.toDouble(), w = bowl.toDouble(), p = power.toDouble();
    double v;
    switch (role) {
      case Role.bat:
        v = b * 0.88 + p * 0.12;
      case Role.wk:
        v = b * 0.88 + p * 0.12 + 1;
      case Role.bowl:
        v = w * 0.96 + b * 0.05;
      case Role.ar:
        final hi = math.max(b, w), lo = math.min(b, w);
        v = math.max(hi * 0.97, hi * 0.72 + lo * 0.33);
        if (b >= w) v += (p - 75) * 0.05;
    }
    return v.round().clamp(30, 99);
  }

  /// "Card tier" used for card styling in the UI.
  String get tier {
    final o = overall;
    if (o >= 88) return 'ELITE';
    if (o >= 80) return 'GOLD';
    if (o >= 72) return 'SILVER';
    return 'BRONZE';
  }

  String get shortName {
    final parts = name.split(' ');
    if (parts.length == 1) return name;
    final first = parts.first;
    if (first.length <= 2 && first.toUpperCase() == first) return name;
    return '${first[0]} ${parts.sublist(1).join(' ')}';
  }

  String get surname {
    final parts = name.split(' ');
    if (parts.length >= 3 && parts[parts.length - 2].toLowerCase() == parts[parts.length - 2]) {
      return '${parts[parts.length - 2]} ${parts.last}'.toUpperCase();
    }
    return parts.last.toUpperCase();
  }

  String get initials {
    final parts = name.split(RegExp(r'[ -]')).where((s) => s.isNotEmpty).toList();
    if (parts.length == 1) return parts.first.substring(0, 2).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  String get styleLine {
    final batting = leftBat ? 'Left-hand bat' : 'Right-hand bat';
    if (bowlStyle == BowlStyle.none) return batting;
    return '$batting • ${bowlStyle.label}';
  }

  @override
  String toString() => name;
}

/// Approximate open-market value in lakh, used by AI auction logic and
/// retention decisions.
int marketValue(Player p) {
  final o = p.overall;
  // Exponential curve that gets steeper for stars (top-heavy like real
  // auctions), with an Indian premium since they don't use overseas slots.
  var v = 30 * math.exp(0.13 * (o - 58)) * (1 + math.max(0, o - 78) * 0.04);
  if (!p.overseas) {
    v *= p.capped ? 1.15 : 0.9;
  } else {
    v *= 0.7;
  }
  if (p.role == Role.ar) v *= 1.08;
  if (p.role == Role.wk) v *= 1.05;
  if (p.role == Role.bowl && p.isPace) v *= 1.03;
  return math.max(30, v.round());
}

/// Auction base price in lakh.
int basePriceFor(Player p) {
  if (p.team26 == null) return p.price26;
  final o = p.overall;
  if (p.capped) {
    if (o >= 80) return 200;
    if (o >= 76) return 150;
    if (o >= 73) return 125;
    if (o >= 70) return 100;
    if (o >= 66) return 75;
    return 50;
  }
  if (o >= 76) return 50;
  if (o >= 70) return 40;
  return 30;
}
