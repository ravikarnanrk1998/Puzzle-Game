import 'dart:math' as math;

/// Seeded random helper so auctions and matches are reproducible.
class Rng {
  Rng(int seed) : _r = math.Random(seed);

  final math.Random _r;

  double next() => _r.nextDouble();
  int nextInt(int max) => _r.nextInt(max);
  double range(double a, double b) => a + (b - a) * _r.nextDouble();

  /// Inclusive integer range.
  int rangeInt(int a, int b) => a + _r.nextInt(b - a + 1);
  bool chance(double p) => _r.nextDouble() < p;
  T pick<T>(List<T> items) => items[_r.nextInt(items.length)];
  void shuffle<T>(List<T> items) => items.shuffle(_r);

  int weightedIndex(List<double> weights) {
    var total = 0.0;
    for (final w in weights) {
      total += w;
    }
    if (total <= 0) return _r.nextInt(weights.length);
    var x = _r.nextDouble() * total;
    for (var i = 0; i < weights.length; i++) {
      x -= weights[i];
      if (x <= 0) return i;
    }
    return weights.length - 1;
  }

  T weighted<T>(Map<T, double> table) {
    final keys = table.keys.toList();
    return keys[weightedIndex(keys.map((k) => table[k]!).toList())];
  }

  /// Standard normal sample (Box-Muller).
  double gauss() {
    final u1 = math.max(1e-9, _r.nextDouble());
    final u2 = _r.nextDouble();
    return math.sqrt(-2 * math.log(u1)) * math.cos(2 * math.pi * u2);
  }
}
