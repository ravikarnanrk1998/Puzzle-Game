using System;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Vector in "cricket space": x = lateral (+x = right-hander's leg side),
    /// y = along the pitch from the bowler's stumps (0) to the striker's (20.12),
    /// z = up. All gameplay maths uses this space; <see cref="U"/> converts to
    /// Unity's axes (x, y up, z forward) only when placing objects.
    /// </summary>
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
        public static readonly V3 Up = new V3(0, 0, 1);

        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, double s) => new V3(a.x * s, a.y * s, a.z * s);

        public double Length => Math.Sqrt(x * x + y * y + z * z);
        public double Dot(V3 o) => x * o.x + y * o.y + z * o.z;
        public V3 Cross(V3 o) => new V3(y * o.z - z * o.y, z * o.x - x * o.z, x * o.y - y * o.x);
        public V3 WithZ(double nz) => new V3(x, y, nz);
        public V3 Ground => new V3(x, y, 0);
        public double DistTo(V3 o) => (this - o).Length;

        public V3 Normalized
        {
            get
            {
                double l = Length;
                return l < 1e-9 ? Up : new V3(x / l, y / l, z / l);
            }
        }

        public static V3 Lerp(V3 a, V3 b, double t) => new V3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);

        /// <summary>Unity world position.</summary>
        public Vector3 U => new Vector3((float)x, (float)z, (float)y);

        public static V3 FromUnity(Vector3 v) => new V3(v.x, v.z, v.y);

        /// <summary>Rodrigues rotation of <paramref name="v"/> about unit axis <paramref name="k"/>.</summary>
        public static V3 Rotate(V3 v, V3 k, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            double kv = k.Dot(v);
            var cr = k.Cross(v);
            return new V3(
                v.x * c + cr.x * s + k.x * kv * (1 - c),
                v.y * c + cr.y * s + k.y * kv * (1 - c),
                v.z * c + cr.z * s + k.z * kv * (1 - c));
        }

        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public static class Pitch
    {
        public const double Length = 20.12;
        public const double StrikerCrease = 18.9;
        public const double BowlerCrease = 1.22;
        public const double Mid = 10.06;
    }

    public static class MathX
    {
        public static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double EaseInOut(double t)
        {
            double x = Clamp01(t);
            return x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;
        }
        public static double Smooth(double t)
        {
            double x = Clamp01(t);
            return x * x * (3 - 2 * x);
        }
    }
}
