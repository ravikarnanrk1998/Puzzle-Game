using System;
using System.Collections.Generic;

namespace KPL.Core
{
    /// <summary>Double-precision vector for the simulation (Unity-free so the rules run in plain .NET tests).</summary>
    public readonly struct V3
    {
        public readonly double x, y, z;

        public V3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly V3 Zero = new V3(0, 0, 0);
        public static readonly V3 Up = new V3(0, 1, 0);

        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, double k) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator *(double k, V3 a) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator /(V3 a, double k) => new V3(a.x / k, a.y / k, a.z / k);

        public double Length => Math.Sqrt(x * x + y * y + z * z);
        public double LengthXZ => Math.Sqrt(x * x + z * z);
        public V3 XZ => new V3(x, 0, z);
        public V3 Normalized
        {
            get
            {
                double l = Length;
                return l < 1e-9 ? Zero : this / l;
            }
        }

        public static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static V3 Lerp(V3 a, V3 b, double t) => a + (b - a) * t;
        public static double Dist(V3 a, V3 b) => (a - b).Length;
        public static double DistXZ(V3 a, V3 b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
        public V3 WithY(double ny) => new V3(x, ny, z);
        public override string ToString() => "(" + x.ToString("0.00") + ", " + y.ToString("0.00") + ", " + z.ToString("0.00") + ")";
    }

    /// <summary>Small deterministic RNG (SplitMix64) so a seed replays a whole match identically.</summary>
    public sealed class Rng
    {
        ulong _s;

        public Rng(long seed) { _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; }

        public ulong NextU64()
        {
            ulong z = (_s += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform [0, 1).</summary>
        public double Next() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        public double Range(double a, double b) => a + (b - a) * Next();
        public int Int(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(Next() * maxExclusive);
        public int Int(int min, int maxExclusive) => min + Int(maxExclusive - min);
        public bool Chance(double p) => Next() < p;

        public double Gauss()
        {
            double u = 1.0 - Next(), v = Next();
            return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v);
        }

        public T Pick<T>(IList<T> list) => list[Int(list.Count)];

        public int Weighted(IList<double> weights)
        {
            double sum = 0;
            for (int i = 0; i < weights.Count; i++) sum += Math.Max(0, weights[i]);
            if (sum <= 0) return 0;
            double x = Next() * sum;
            for (int i = 0; i < weights.Count; i++)
            {
                x -= Math.Max(0, weights[i]);
                if (x < 0) return i;
            }
            return weights.Count - 1;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Int(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public long Fork() => (long)(NextU64() & 0x7FFFFFFFFFFFFFFFUL);
    }

    public static class MathX
    {
        public const double Deg = Math.PI / 180.0;
        public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
        public static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double InvLerp(double a, double b, double v) => Math.Abs(b - a) < 1e-12 ? 0 : Clamp01((v - a) / (b - a));
        public static double Smooth(double t) => t * t * (3 - 2 * t);

        /// <summary>Wraps an angle in degrees to (-180, 180].</summary>
        public static double WrapDeg(double a)
        {
            a %= 360;
            if (a <= -180) a += 360;
            if (a > 180) a -= 360;
            return a;
        }
    }

    public enum Difficulty { Easy, Medium, Hard, Pro }

    /// <summary>Pitch and ground geometry in metres. The pitch runs along Z; the striker is at -Z,
    /// the bowler runs in from +Z. For a right-hander the off side is +X.</summary>
    public static class Pitch
    {
        public const double StumpsZ = 10.06;
        public const double CreaseZ = 8.84;
        public const double StrikerStumpsZ = -StumpsZ;
        public const double StrikerCreaseZ = -CreaseZ;
        public const double StumpHalfWidth = 0.1143;
        public const double StumpHeight = 0.711;
        public const double BallRadius = 0.036;
        public const double Length = 20.12;
        public const double WideOff = 0.90;
        public const double WideLeg = 0.42;
        public const double Gravity = 9.81;

        /// <summary>Air drag coefficient for a cricket ball (0.5 rho Cd A / m).</summary>
        public const double Drag = 0.0062;

        /// <summary>Unit vector on the ground for a batter-relative angle (0 = straight back past the bowler,
        /// +90 = square on the off side, 180 = straight behind the keeper).</summary>
        public static V3 Dir(double azimuthDeg, bool leftHanded)
        {
            double a = azimuthDeg * MathX.Deg;
            double side = leftHanded ? -1 : 1;
            return new V3(Math.Sin(a) * side, 0, Math.Cos(a));
        }

        /// <summary>Batter-relative angle of a ground point measured from the striker.</summary>
        public static double AzimuthOf(V3 p, bool leftHanded)
        {
            double dx = p.x * (leftHanded ? -1 : 1), dz = p.z - StrikerCreaseZ;
            return Math.Atan2(dx, dz) / MathX.Deg;
        }
    }
}
