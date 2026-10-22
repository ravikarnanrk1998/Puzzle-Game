using System;
using System.Collections.Generic;

namespace PCC.Core
{
    /// <summary>Seeded random helper so matches are reproducible.</summary>
    public sealed class Rng
    {
        readonly Random _r;

        public Rng(int seed) { _r = new Random(seed); }

        public double Next() => _r.NextDouble();
        public int NextInt(int max) => _r.Next(max);
        public double Range(double a, double b) => a + (b - a) * _r.NextDouble();

        /// <summary>Inclusive integer range.</summary>
        public int RangeInt(int a, int b) => a + _r.Next(b - a + 1);
        public bool Chance(double p) => _r.NextDouble() < p;
        public T Pick<T>(IList<T> items) => items[_r.Next(items.Count)];

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = _r.Next(i + 1);
                T t = items[i];
                items[i] = items[j];
                items[j] = t;
            }
        }

        public int WeightedIndex(IList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0) return _r.Next(weights.Count);
            double x = _r.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                x -= weights[i];
                if (x <= 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>Picks one of <paramref name="keys"/> with the matching weight.</summary>
        public T Weighted<T>(T[] keys, double[] weights) => keys[WeightedIndex(weights)];

        /// <summary>Standard normal sample (Box-Muller).</summary>
        public double Gauss()
        {
            double u1 = Math.Max(1e-9, _r.NextDouble());
            double u2 = _r.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
