using System;
using System.Collections.Generic;

namespace KPL.Core
{
    /// <summary>The playing area: an ellipse centred on the pitch.</summary>
    public readonly struct Ground
    {
        public readonly double Straight, Square;

        public Ground(TeamDef venue)
        {
            Straight = venue.BoundaryStraight;
            Square = venue.BoundarySquare;
        }

        public Ground(double straight, double square)
        {
            Straight = straight;
            Square = square;
        }

        /// <summary>> 1 outside the rope.</summary>
        public double Norm(V3 p) => Math.Sqrt(p.x * p.x / (Square * Square) + p.z * p.z / (Straight * Straight));
        public bool Outside(V3 p) => Norm(p) >= 1.0;

        /// <summary>Distance from the centre to the rope in the direction of a ground point.</summary>
        public double RadiusToward(V3 p)
        {
            double a = Math.Atan2(p.x, p.z);
            double s = Math.Sin(a), c = Math.Cos(a);
            return 1.0 / Math.Sqrt(s * s / (Square * Square) + c * c / (Straight * Straight));
        }

        /// <summary>Point on a ray from <paramref name="from"/> that lies <paramref name="inset"/> metres inside the rope.</summary>
        public V3 RayToRope(V3 from, V3 dir, double inset)
        {
            double lo = 0, hi = 200;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                var p = from + dir * mid;
                if (Norm(p) >= 1.0) hi = mid;
                else lo = mid;
            }
            return from + dir * Math.Max(0, lo - inset);
        }
    }

    /// <summary>Flight of a struck ball: drag in the air, bounces, then rolling friction on the outfield.</summary>
    public sealed class Flight
    {
        public const double Dt = 0.02;
        public readonly List<V3> P = new List<V3>();
        /// <summary>Time after contact of the first bounce (-1 if it never lands inside the rope).</summary>
        public double TFirstBounce = -1;
        /// <summary>Time the ball crosses the rope (-1 if it stays in).</summary>
        public double TBoundary = -1;
        public bool Six;
        public double TStop;
        public double Carry;

        public double Duration => (P.Count - 1) * Dt;

        public V3 At(double t)
        {
            if (P.Count == 0) return V3.Zero;
            if (t <= 0) return P[0];
            double f = t / Dt;
            int i = (int)f;
            if (i >= P.Count - 1) return P[P.Count - 1];
            return V3.Lerp(P[i], P[i + 1], f - i);
        }

        public bool AirborneAt(double t) => TFirstBounce < 0 || t < TFirstBounce;

        public static Flight Simulate(V3 start, V3 vel, Ground ground, double maxT = 14)
        {
            var f = new Flight();
            var p = start;
            var v = vel;
            double r = Pitch.BallRadius;
            const int sub = 4;
            double h = Dt / sub;
            bool rolling = false;
            int bounces = 0;
            f.P.Add(p);
            int steps = (int)(maxT / Dt);
            for (int s = 1; s <= steps; s++)
            {
                for (int k = 0; k < sub; k++)
                {
                    if (!rolling)
                    {
                        double speed = v.Length;
                        var a = new V3(0, -Pitch.Gravity, 0) - v * (Pitch.Drag * speed);
                        v += a * h;
                        p += v * h;
                        if (p.y <= r && v.y < 0)
                        {
                            p = p.WithY(r);
                            if (bounces == 0)
                            {
                                f.TFirstBounce = (s - 1) * Dt + (k + 1) * h;
                                f.Carry = Math.Sqrt((p.x - start.x) * (p.x - start.x) + (p.z - start.z) * (p.z - start.z));
                            }
                            bounces++;
                            double vy = -v.y * 0.36;
                            double keep = bounces == 1 ? 0.7 : 0.82;
                            v = new V3(v.x * keep, vy, v.z * keep);
                            if (vy < 1.2) rolling = true;
                        }
                    }
                    else
                    {
                        double hs = v.LengthXZ;
                        double dec = 2.3 * h;
                        if (hs <= dec)
                        {
                            v = V3.Zero;
                        }
                        else
                        {
                            v = v.XZ * ((hs - dec) / hs);
                        }
                        p = new V3(p.x + v.x * h, r, p.z + v.z * h);
                    }
                }
                f.P.Add(p);
                if (f.TBoundary < 0 && ground.Outside(p))
                {
                    f.TBoundary = s * Dt;
                    f.Six = f.TFirstBounce < 0;
                    // Keep a few samples past the rope so the ball visibly carries into the crowd.
                    for (int extra = 0; extra < 25; extra++)
                    {
                        if (!rolling)
                        {
                            v += new V3(0, -Pitch.Gravity, 0) * Dt;
                            p += v * Dt;
                            if (p.y < r) p = p.WithY(r);
                        }
                        else
                        {
                            p += v * Dt;
                        }
                        f.P.Add(p);
                    }
                    break;
                }
                if (rolling && v.LengthXZ < 0.05)
                {
                    break;
                }
            }
            f.TStop = f.Duration;
            return f;
        }
    }
}
