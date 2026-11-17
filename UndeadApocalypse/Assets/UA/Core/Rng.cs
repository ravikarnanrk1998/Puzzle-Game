using System;
using System.Collections.Generic;

namespace UA.Core
{
    /// <summary>
    /// Deterministic random numbers (xoshiro256** seeded through SplitMix64). The same seed always
    /// produces the same sequence on every platform, so any stage can be rebuilt from its seed.
    /// </summary>
    public sealed class Rng
    {
        ulong _s0, _s1, _s2, _s3;

        public Rng(ulong seed)
        {
            ulong x = seed;
            _s0 = SplitMix(ref x);
            _s1 = SplitMix(ref x);
            _s2 = SplitMix(ref x);
            _s3 = SplitMix(ref x);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 0x9E3779B97F4A7C15UL;
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextU64()
        {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => (NextU64() >> 11) * (1.0 / (1UL << 53));

        public float Float() => (float)NextDouble();

        public float Range(float min, float max) => min + (max - min) * Float();

        /// <summary>Integer in [min, max) (max exclusive).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            ulong span = (ulong)((long)max - min);
            return (int)(min + (long)(NextU64() % span));
        }

        public bool Chance(double p) => NextDouble() < p;

        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        public int Sign() => Chance(0.5) ? 1 : -1;

        /// <summary>Picks an index with probability proportional to its weight.</summary>
        public int Weighted(IList<float> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return 0;
            double r = NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <summary>Stable 64-bit hash of a set of values (used to derive sub-seeds).</summary>
        public static ulong Hash(params long[] values)
        {
            ulong h = 0xCBF29CE484222325UL;
            foreach (var v in values)
            {
                ulong x = (ulong)v;
                h ^= SplitMix(ref x);
                h *= 0x100000001B3UL;
                h = Rotl(h, 29);
            }
            ulong y = h;
            return SplitMix(ref y);
        }

        public static ulong HashString(string s)
        {
            ulong h = 0xCBF29CE484222325UL;
            if (s != null)
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 0x100000001B3UL;
                }
            ulong y = h;
            return SplitMix(ref y);
        }
    }

    /// <summary>Overflow-safe arithmetic for values that grow forever (stages, coins, kills).</summary>
    public static class Safe
    {
        public const long MaxCurrency = 999_999_999_999L;

        public static long Add(long a, long b)
        {
            if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
            if (b < 0 && a < long.MinValue - b) return long.MinValue;
            return a + b;
        }

        public static long Mul(long a, double m)
        {
            double r = a * m;
            if (double.IsNaN(r)) return 0;
            if (r >= long.MaxValue) return long.MaxValue;
            if (r <= long.MinValue) return long.MinValue;
            return (long)Math.Round(r);
        }

        public static long ClampCurrency(long v) => v < 0 ? 0 : v > MaxCurrency ? MaxCurrency : v;

        public static int ClampInt(long v) => v > int.MaxValue ? int.MaxValue : v < int.MinValue ? int.MinValue : (int)v;

        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>Saturating "Hill" curve: 0 at x = 0, max/2 at x = half, approaching max forever.</summary>
        public static double Hill(double x, double max, double half, double exponent)
        {
            if (x <= 0) return 0;
            double xe = Math.Pow(x, exponent), he = Math.Pow(half, exponent);
            return max * xe / (xe + he);
        }

        /// <summary>Compact number text (12.3K, 4.5M, 1.2B).</summary>
        public static string Short(long v)
        {
            double a = Math.Abs((double)v);
            string s = v < 0 ? "-" : "";
            if (a < 10_000) return v.ToString("#,0");
            if (a < 1_000_000) return s + (a / 1000).ToString(a < 100_000 ? "0.#" : "0") + "K";
            if (a < 1_000_000_000) return s + (a / 1_000_000).ToString(a < 100_000_000 ? "0.##" : "0.#") + "M";
            return s + (a / 1_000_000_000).ToString("0.##") + "B";
        }
    }
}
