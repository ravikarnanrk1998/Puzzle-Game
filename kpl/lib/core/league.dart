/// League-wide rules and branding. Change [League.name] in one place if the
/// game is ever published under a different league brand.
class League {
  static const String name = 'IPL';
  static const String fullName = 'Indian Premier League';
  static const int startYear = 2027;

  /// Auction purse per franchise, in lakh (₹125 Cr - the IPL 2026 purse).
  static const int purse = 12500;
  static const int maxSquad = 25;
  static const int minSquad = 18;
  static const int maxOverseas = 8;
  static const int maxOverseasXI = 4;

  /// Mega-auction retention slabs (2025 cycle rules), lakh.
  static const List<int> cappedSlabs = [1800, 1400, 1100, 1800, 1400];
  static const int uncappedSlab = 400;
  static const int maxRetain = 6;
  static const int maxCappedRetain = 5;
  static const int maxUncappedRetain = 2;

  /// Lowest salary in the league (uncapped base), lakh.
  static const int minPrice = 30;

  static const int t20Overs = 20;
}

/// ₹ formatting for lakh amounts: 2520 -> "₹25.20 Cr", 75 -> "₹75 L".
String money(int lakh, {bool compact = false}) {
  if (lakh == 0) return '₹0';
  if (lakh.abs() >= 100) {
    final cr = lakh / 100.0;
    final whole = lakh % 100 == 0;
    final s = whole ? cr.toStringAsFixed(0) : cr.toStringAsFixed(2);
    return compact ? '₹${s}Cr' : '₹$s Cr';
  }
  return compact ? '₹${lakh}L' : '₹$lakh L';
}

/// Crore value without the currency sign, e.g. "25.20".
String crore(int lakh) {
  final cr = lakh / 100.0;
  return lakh % 100 == 0 ? cr.toStringAsFixed(0) : cr.toStringAsFixed(2);
}

/// Official-style bid increments.
int bidIncrement(int current) {
  if (current < 100) return 5;
  if (current < 200) return 10;
  if (current < 500) return 20;
  return 25;
}

int nextBidAmount(int current) => current + bidIncrement(current);

String oversText(int legalBalls) => '${legalBalls ~/ 6}.${legalBalls % 6}';

String ordinal(int n) {
  if (n % 100 >= 11 && n % 100 <= 13) return '${n}th';
  switch (n % 10) {
    case 1:
      return '${n}st';
    case 2:
      return '${n}nd';
    case 3:
      return '${n}rd';
    default:
      return '${n}th';
  }
}
