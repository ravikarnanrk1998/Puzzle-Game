using System;
using System.Collections.Generic;
using PCC.Core;

namespace PCC.Game
{
    /// <summary>Trajectory of a delivery from release to the batter and on to the keeper.</summary>
    public sealed class DeliveryPath
    {
        public DeliveryPath(DeliveryPlan plan, bool batterLeft, double slowMo, double keeperY)
        {
            Plan = plan;
            KeeperY = keeperY;
            bool spin = plan.Spin;
            double hand = batterLeft ? -1 : 1;
            Release = new V3(plan.LeftArm ? 0.34 : -0.34, 1.3, spin ? 2.05 : 2.3);
            FinalX = (plan.LineX ?? plan.Line.LegwardOffset() * hand) + plan.Swing + plan.Turn;
            if (plan.Wide) FinalX = plan.Line == BallLine.Leg ? 0.95 * hand : -1.05 * hand;
            ArriveY = 19.05;
            ArriveZ = plan.Type.ArriveHeight();
            double v = Math.Max(15.0, plan.Speed / 3.6);
            double f = slowMo * (spin ? 0.82 : 1.0);
            if (plan.Bounces)
            {
                BounceY = Math.Clamp(plan.PitchY ?? (Pitch.Length - plan.Type.PitchFromStumps()), 5.0, 19.2);
                double aimX = FinalX - plan.Turn;
                double u = (BounceY - Release.y) / (ArriveY - Release.y);
                BounceX = Release.x + (aimX - Release.x) * u;
                TBounce = (BounceY - Release.y) / v * f;
                TArrive = TBounce + (ArriveY - BounceY) / (v * 0.86) * f;
            }
            else
            {
                BounceY = ArriveY;
                BounceX = FinalX;
                TBounce = (ArriveY - Release.y) / v * f;
                TArrive = TBounce;
            }
            Arc = spin ? 0.95 : 0.32;
            TAfter = 0.32 * f + 0.25;
        }

        public DeliveryPlan Plan { get; }
        public double KeeperY { get; }
        public V3 Release { get; }
        public double FinalX { get; }
        public double ArriveY { get; }
        public double ArriveZ { get; }
        public double BounceY { get; }
        public double BounceX { get; }
        public double TBounce { get; }
        public double TArrive { get; }
        public double TAfter { get; }
        public double Arc { get; }
        public bool Bounces => Plan.Bounces;

        /// <summary>Time the ball would reach the stumps (bowled animations).</summary>
        public double TStumps => TArrive + 0.16;

        public V3 At(double t)
        {
            if (t <= TBounce)
            {
                double u = MathX.Clamp01(t / TBounce);
                double x = Release.x + (BounceX - Release.x) * u - Plan.Swing * 0.45 * u * (1 - u);
                double y = Release.y + (BounceY - Release.y) * u;
                double endZ = Bounces ? 0.0 : ArriveZ;
                double z = Release.z + (endZ - Release.z) * u + Arc * u * (1 - u) * (Bounces ? 1 : 0.6);
                return new V3(x, y, Math.Max(0.036, z));
            }
            if (t <= TArrive)
            {
                double v = MathX.Clamp01((t - TBounce) / (TArrive - TBounce));
                return new V3(BounceX + (FinalX - BounceX) * v, BounceY + (ArriveY - BounceY) * v, Math.Max(0.036, ArriveZ * (2 * v - v * v)));
            }
            double w = MathX.Clamp01((t - TArrive) / TAfter);
            return new V3(FinalX + (FinalX - BounceX) * w * 0.35, ArriveY + (KeeperY - ArriveY) * w, ArriveZ + (0.75 - ArriveZ) * w);
        }
    }

    /// <summary>Trajectory after the bat.</summary>
    public sealed class ShotPath
    {
        public ShotPath(V3 from, V3 to, double apex, double duration, double? bounceAt = null, V3? rollTo = null)
        {
            From = from;
            To = to;
            Apex = apex;
            Duration = duration;
            BounceAt = bounceAt;
            RollTo = rollTo;
        }

        public V3 From { get; }
        public V3 To { get; }
        public double Apex { get; }
        public double Duration { get; }
        public double? BounceAt { get; }
        public V3? RollTo { get; }
        public V3 End => RollTo ?? To;

        public V3 At(double t)
        {
            double u = MathX.Clamp01(t / Duration);
            if (BounceAt != null && RollTo != null && u > BounceAt.Value)
            {
                double v = (u - BounceAt.Value) / (1 - BounceAt.Value);
                double e = 1 - (1 - v) * (1 - v);
                double hop = 0.9 * Math.Abs(Math.Sin(v * Math.PI * 2)) * (1 - v);
                var r = RollTo.Value;
                return new V3(To.x + (r.x - To.x) * e, To.y + (r.y - To.y) * e, 0.036 + hop);
            }
            double w = BounceAt == null ? u : u / BounceAt.Value;
            double x = From.x + (To.x - From.x) * w;
            double y = From.y + (To.y - From.y) * w;
            double z = From.z + (To.z - From.z) * w + Apex * 4 * w * (1 - w);
            return new V3(x, y, Math.Max(0.036, z));
        }
    }

    public sealed class FieldSetting
    {
        public FieldSetting(List<V3> positions, V3 keeper, List<V3> slips)
        {
            Positions = positions;
            Keeper = keeper;
            Slips = slips;
        }

        public List<V3> Positions { get; }
        public V3 Keeper { get; }
        public List<V3> Slips { get; }

        /// <summary>Fielding position by field angle (deg, + = off side) and distance from the striker's stumps.</summary>
        public static V3 Pos(double angleDeg, double dist, bool leftBatter)
        {
            double a = angleDeg * Math.PI / 180;
            double sx = leftBatter ? 1 : -1;
            return new V3(sx * Math.Sin(a) * dist, Pitch.Length - Math.Cos(a) * dist, 0);
        }

        public static FieldSetting For(int phase, bool spin, bool leftBatter, double boundary = 66)
        {
            double deep = boundary - 4;
            V3 P(double a, double d) => Pos(a, d, leftBatter);
            var keeper = spin ? P(180, 1.2) : P(180, 13.5);
            var slips = new List<V3>();
            var list = new List<V3>();
            if (phase == 0)
            {
                if (!spin)
                {
                    slips.Add(P(166, 15));
                    list.Add(slips[0]);
                }
                list.AddRange(new[] { P(95, 24), P(58, 26), P(20, 25), P(-22, 25), P(-60, 26), P(-95, 22), P(-152, deep), P(140, deep * 0.95) });
                if (spin) list.Add(P(-40, 18));
            }
            else if (phase == 1)
            {
                list.AddRange(new[] { P(95, 24), P(56, 26), P(18, 25), P(-60, 26), P(-95, deep), P(-55, deep), P(-12, deep), P(12, deep), P(100, deep) });
            }
            else
            {
                list.AddRange(new[] { P(98, 24), P(55, 27), P(-62, 27), P(-140, 24), P(-12, deep), P(12, deep), P(-58, deep), P(-95, deep), P(100, deep) });
            }
            if (list.Count > 9) list.RemoveRange(9, list.Count - 9);
            return new FieldSetting(list, keeper, slips);
        }

        public int Nearest(V3 target)
        {
            int best = 0;
            double bd = double.MaxValue;
            for (int i = 0; i < Positions.Count; i++)
            {
                double dx = Positions[i].x - target.x, dy = Positions[i].y - target.y, d = dx * dx + dy * dy;
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            return best;
        }
    }

    /// <summary>Everything needed to animate one shot.</summary>
    public sealed class ShotPlan
    {
        public ShotPath Path;
        public int? Fielder;
        public V3? FielderTarget;
        public int Runs;
        public bool Caught, Six, Four, RunOut;
        public double Duration => Path.Duration;

        public static ShotPlan Make(BallResult r, FieldSetting field, bool leftBatter, double boundary, Rng rng)
        {
            double a = (r.Angle ?? 0) * Math.PI / 180;
            double sx = leftBatter ? 1 : -1;
            var dir = new V3(sx * Math.Sin(a), -Math.Cos(a), 0);
            var start = new V3(leftBatter ? -0.2 : 0.2, 19.2, 0.7);
            V3 Along(double d, double z = 0) => new V3(start.x + dir.x * d, start.y + dir.y * d, z);
            double rope = boundary + 2;

            if (r.IsSix)
            {
                double d = rope + rng.Range(6, 24);
                return new ShotPlan { Path = new ShotPath(start, Along(d, 6), rng.Range(19, 31), 2.3), Runs = 6, Six = true };
            }
            if (r.IsFour && !r.Wicket)
            {
                bool lofted = r.Lofted || rng.Chance(0.3);
                double d = rope + 1.5;
                var path = lofted
                    ? new ShotPath(start, Along(d * 0.72), rng.Range(6, 11), 1.9, 0.62, Along(d))
                    : new ShotPath(start, Along(d * 0.15), 0.4, 1.75, 0.08, Along(d));
                return new ShotPlan { Path = path, Fielder = field.Nearest(Along(d * 0.85)), FielderTarget = Along(d * 0.93), Runs = 4, Four = true };
            }
            if (r.Wicket && r.Dismissal == Dismissal.Caught)
            {
                int idx = field.Nearest(Along(rng.Range(22, boundary - 2)));
                var f = field.Positions[idx];
                var catchAt = new V3(f.x + rng.Range(-2.5, 2.5), f.y + rng.Range(-2.5, 2.5), 1.4);
                double dist = Math.Sqrt((catchAt.x - start.x) * (catchAt.x - start.x) + (catchAt.y - start.y) * (catchAt.y - start.y));
                return new ShotPlan
                {
                    Path = new ShotPath(start, catchAt, dist > 35 ? rng.Range(16, 26) : rng.Range(6, 14), 1.5 + dist / 60),
                    Fielder = idx,
                    FielderTarget = catchAt.Ground,
                    Caught = true,
                };
            }
            if (r.Wicket && r.Dismissal == Dismissal.RunOut)
            {
                double d = rng.Range(16, 30);
                var target = Along(d);
                return new ShotPlan { Path = new ShotPath(start, target, 0.4, 1.1), Fielder = field.Nearest(target), FielderTarget = target, Runs = r.Runs, RunOut = true };
            }
            int runs = r.Runs + (r.Extra == ExtraType.LegBye || r.Extra == ExtraType.Bye ? r.ExtraRuns : 0);
            double dd;
            switch (runs)
            {
                case 0: dd = r.Defended ? rng.Range(3, 9) : rng.Range(12, 22); break;
                case 1: dd = rng.Range(15, 28); break;
                case 2: dd = rng.Range(38, 56); break;
                default: dd = rng.Range(56, boundary - 4); break;
            }
            var tgt = Along(dd);
            bool loft = r.Lofted && runs >= 2;
            return new ShotPlan
            {
                Path = new ShotPath(start, loft ? Along(dd * 0.8) : Along(dd * 0.2), loft ? rng.Range(5, 9) : 0.35, 0.9 + dd / 30, loft ? 0.7 : 0.15, tgt),
                Fielder = field.Nearest(tgt),
                FielderTarget = tgt,
                Runs = runs,
            };
        }
    }
}
