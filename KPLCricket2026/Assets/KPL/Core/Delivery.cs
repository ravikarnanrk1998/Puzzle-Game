using System;
using System.Collections.Generic;

namespace KPL.Core
{
    public enum DeliveryKind { Stock, Inswing, Outswing, Yorker, Bouncer, Slower, OffBreak, LegBreak, Wrongun, ArmBall, Flighted }

    public enum LengthClass { FullToss, Yorker, Full, Good, Short, Bouncer }

    public static class DeliveryKindX
    {
        public static string Label(this DeliveryKind k, BowlType t)
        {
            switch (k)
            {
                case DeliveryKind.Stock: return t.IsSpin() ? "STOCK" : "STOCK";
                case DeliveryKind.Inswing: return "INSWINGER";
                case DeliveryKind.Outswing: return "OUTSWINGER";
                case DeliveryKind.Yorker: return "YORKER";
                case DeliveryKind.Bouncer: return "BOUNCER";
                case DeliveryKind.Slower: return "SLOWER BALL";
                case DeliveryKind.OffBreak: return "OFF BREAK";
                case DeliveryKind.LegBreak: return t == BowlType.LeftArmSpin ? "LEFT-ARM TURN" : "LEG BREAK";
                case DeliveryKind.Wrongun: return t == BowlType.OffSpin ? "DOOSRA" : t == BowlType.LeftArmSpin ? "CHINAMAN" : "GOOGLY";
                case DeliveryKind.ArmBall: return "ARM BALL";
                case DeliveryKind.Flighted: return "FLIGHTED";
                default: return k.ToString().ToUpperInvariant();
            }
        }

        /// <summary>Deliveries a bowler of this type can choose, in menu order.</summary>
        public static DeliveryKind[] For(BowlType t)
        {
            switch (t)
            {
                case BowlType.OffSpin: return new[] { DeliveryKind.OffBreak, DeliveryKind.Flighted, DeliveryKind.ArmBall, DeliveryKind.Wrongun, DeliveryKind.Yorker };
                case BowlType.LegSpin: return new[] { DeliveryKind.LegBreak, DeliveryKind.Flighted, DeliveryKind.Wrongun, DeliveryKind.ArmBall, DeliveryKind.Yorker };
                case BowlType.LeftArmSpin: return new[] { DeliveryKind.LegBreak, DeliveryKind.Flighted, DeliveryKind.ArmBall, DeliveryKind.Wrongun, DeliveryKind.Yorker };
                default: return new[] { DeliveryKind.Stock, DeliveryKind.Outswing, DeliveryKind.Inswing, DeliveryKind.Yorker, DeliveryKind.Bouncer, DeliveryKind.Slower };
            }
        }

        /// <summary>Allowed landing distance from the striker's stumps (m) and the default spot.</summary>
        public static (double Min, double Max, double Def) LengthBand(this DeliveryKind k, bool spin)
        {
            switch (k)
            {
                case DeliveryKind.Yorker: return (0.2, 2.2, 1.2);
                case DeliveryKind.Bouncer: return (8.5, 12.0, 10.0);
                case DeliveryKind.Flighted: return (2.0, 5.5, 3.5);
                default: return spin ? (2.0, 8.0, 4.4) : (2.0, 11.0, 6.0);
            }
        }
    }

    /// <summary>What the bowler intends: delivery type, landing spot and how accurately it is executed.</summary>
    public sealed class DeliveryPlan
    {
        public DeliveryKind Kind;
        /// <summary>Landing point: lateral offset from middle stump in batter terms (+ = off side) and distance from the striker's stumps.</summary>
        public double Line, Length;
        /// <summary>0 = wild, 1 = perfect (bowling meter or AI skill).</summary>
        public double Accuracy = 0.8;
        /// <summary>0..1 extra effort from the meter (adds a little pace).</summary>
        public double Effort = 0.5;
        public bool AroundWicket;
    }

    /// <summary>
    /// The physical path of a delivery from release to beyond the stumps. Pre-bounce flight is a
    /// projectile with optional swing; after pitching the ball keeps most of its pace, deviates
    /// for spin or seam and rises with the pitch's bounce.
    /// </summary>
    public sealed class DeliveryTrack
    {
        public BowlType BowlType;
        public DeliveryKind Kind;
        public bool LeftArm, LeftBat;
        public V3 Release, Bounce;
        public V3 V0, V1;
        public double SwingAccel;
        public double TBounce;
        public double SpeedKph;
        public bool FullToss;
        public bool NoBall, Wide, Beamer;
        public double ContactZ;
        public bool FrontFoot;
        public double TContact;
        public LengthClass LengthClass;
        /// <summary>Lateral offset of the ball at the batter (batter terms, + = off side).</summary>
        public double LineAtBat;
        public double HeightAtBat;
        public bool HitsStumps;
        public double TurnAmount;
        /// <summary>How well disguised / testing the ball is, 0..1 (used by the AI batter).</summary>
        public double Quality;
        public double LengthFromStumps;

        public V3 PosAt(double t)
        {
            if (t <= TBounce || FullToss)
            {
                double tt = Math.Max(0, t);
                return new V3(Release.x + V0.x * tt + 0.5 * SwingAccel * tt * tt, Release.y + V0.y * tt - 0.5 * Pitch.Gravity * tt * tt, Release.z + V0.z * tt);
            }
            double d = t - TBounce;
            double y = Bounce.y + V1.y * d - 0.5 * Pitch.Gravity * d * d;
            return new V3(Bounce.x + V1.x * d, Math.Max(Pitch.BallRadius, y), Bounce.z + V1.z * d);
        }

        public V3 VelAt(double t)
        {
            if (t <= TBounce || FullToss) return new V3(V0.x + SwingAccel * t, V0.y - Pitch.Gravity * t, V0.z);
            double d = t - TBounce;
            return new V3(V1.x, V1.y - Pitch.Gravity * d, V1.z);
        }

        public double TimeAtZ(double z)
        {
            if (FullToss || z >= Bounce.z) return (z - Release.z) / V0.z;
            return TBounce + (z - Bounce.z) / V1.z;
        }

        /// <summary>Time the ball passes the striker's stumps.</summary>
        public double TStumps => TimeAtZ(Pitch.StrikerStumpsZ);

        public string LengthText
        {
            get
            {
                switch (LengthClass)
                {
                    case LengthClass.FullToss: return "FULL TOSS";
                    case LengthClass.Yorker: return "YORKER";
                    case LengthClass.Full: return "FULL";
                    case LengthClass.Good: return "GOOD LENGTH";
                    case LengthClass.Short: return "SHORT";
                    default: return "BOUNCER";
                }
            }
        }

        public static LengthClass Classify(double lengthFromStumps)
        {
            if (lengthFromStumps < 0.15) return LengthClass.FullToss;
            if (lengthFromStumps < 2.0) return LengthClass.Yorker;
            if (lengthFromStumps < 4.6) return LengthClass.Full;
            if (lengthFromStumps < 7.4) return LengthClass.Good;
            if (lengthFromStumps < 9.4) return LengthClass.Short;
            return LengthClass.Bouncer;
        }
    }

    public static class Deliveries
    {
        /// <summary>Base speed in m/s for a bowler type.</summary>
        public static double BaseSpeed(BowlType t, int skill)
        {
            double k = (skill - 70) / 100.0;
            switch (t)
            {
                case BowlType.Fast: return 40.0 + k * 3.5;
                case BowlType.FastMedium: return 37.0 + k * 3;
                case BowlType.Swing: return 35.0 + k * 3;
                case BowlType.OffSpin: return 24.0 + k * 1.5;
                case BowlType.LegSpin: return 23.5 + k * 1.5;
                case BowlType.LeftArmSpin: return 24.5 + k * 1.5;
                default: return 30.0;
            }
        }

        /// <summary>Builds the ball's path for a bowler executing a plan against a batter.</summary>
        public static DeliveryTrack Build(DeliveryPlan plan, Player bowler, Player batter, TeamDef venue, Rng rng)
        {
            var bt = bowler.CanBowl ? bowler.Bowl : BowlType.FastMedium;
            bool spin = bt.IsSpin();
            int skill = bowler.CanBowl ? bowler.BowlSkill : Math.Max(25, bowler.BowlSkill);
            double side = batter.LeftBat ? -1 : 1;
            var tr = new DeliveryTrack { BowlType = bt, Kind = plan.Kind, LeftArm = bowler.LeftArm, LeftBat = batter.LeftBat };

            // Execution error: worse accuracy and lower skill scatter the landing spot.
            double acc = MathX.Clamp01(plan.Accuracy);
            double sigma = (1 - acc) * 0.85 + (100 - skill) / 100.0 * 0.3;
            double length = plan.Length + rng.Gauss() * sigma * 1.25;
            double line = plan.Line + rng.Gauss() * sigma * 0.8;
            // The occasional ball that simply slips out.
            if (rng.Chance(0.02 + (1 - acc) * 0.04)) line += (rng.Chance(0.55) ? 1 : -1) * rng.Range(0.45, 0.9);
            length = MathX.Clamp(length, -1.2, 13.5);

            // Speed.
            double speed = BaseSpeed(bt, skill);
            switch (plan.Kind)
            {
                case DeliveryKind.Slower: speed *= 0.78; break;
                case DeliveryKind.Bouncer: speed *= 1.02; break;
                case DeliveryKind.Yorker: speed *= spin ? 1.12 : 1.0; break;
                case DeliveryKind.ArmBall: speed *= 1.12; break;
                case DeliveryKind.Flighted: speed *= 0.86; break;
            }
            speed *= 0.97 + plan.Effort * 0.05;
            speed *= 1 + rng.Gauss() * 0.012;
            tr.SpeedKph = speed * 3.6;

            // Release point: arm over the stumps on the bowler's side.
            bool rightArm = !bowler.LeftArm;
            double armX = (rightArm ? 0.28 : -0.28) * (plan.AroundWicket ? -1 : 1);
            tr.Release = new V3(armX, spin ? 2.02 : 2.18, 8.25);

            // Landing point in world terms.
            double wx = line * side;
            double wz = Pitch.StrikerStumpsZ + length;
            tr.LengthFromStumps = length;
            tr.FullToss = length < 0.15;
            tr.LengthClass = DeliveryTrack.Classify(length);

            // Swing: lateral acceleration towards (in) or away (out) from the batter.
            double swing = 0;
            if (!spin)
            {
                double mag = bt == BowlType.Swing ? 2.3 : bt == BowlType.FastMedium ? 1.5 : 0.9;
                mag *= 0.75 + skill / 200.0;
                if (plan.Kind == DeliveryKind.Inswing) swing = -mag * side;
                else if (plan.Kind == DeliveryKind.Outswing) swing = mag * side;
                else swing = rng.Gauss() * 0.25;
            }
            tr.SwingAccel = swing;

            double dz = wz - tr.Release.z;
            double flightT = Math.Abs(dz) / speed;
            if (plan.Kind == DeliveryKind.Flighted) flightT *= 1.04;
            double yb = Pitch.BallRadius;
            if (tr.FullToss)
            {
                // A full toss is aimed at the batter's body height rather than the ground.
                yb = 0.55 + Math.Max(0, -length) * 0.35;
            }
            double vz = dz / flightT;
            double vx = (wx - tr.Release.x - 0.5 * swing * flightT * flightT) / flightT;
            double vy = (yb - tr.Release.y + 0.5 * Pitch.Gravity * flightT * flightT) / flightT;
            tr.V0 = new V3(vx, vy, vz);
            tr.TBounce = flightT;
            tr.Bounce = new V3(wx, yb, wz);

            if (!tr.FullToss)
            {
                var vin = tr.VelAt(flightT);
                double e = (spin ? 0.47 : 0.52) * venue.PitchPace;
                if (plan.Kind == DeliveryKind.Bouncer) e *= 1.1;
                if (plan.Kind == DeliveryKind.ArmBall) e *= 0.88;
                if (plan.Kind == DeliveryKind.Flighted) e *= 1.05;
                double keep = spin ? 0.84 : 0.9;
                // Sideways velocity gained off the pitch, in world X. The direction of spin depends on
                // the bowler, not the batter: an off-break always turns towards -X (into a right-hander).
                double turn;
                if (spin)
                {
                    double t0 = (0.9 + skill / 100.0 * 1.2) * venue.PitchSpin;
                    double natural = bt == BowlType.OffSpin ? -1 : 1;
                    switch (plan.Kind)
                    {
                        case DeliveryKind.Wrongun: turn = -natural * t0 * 0.9; break;
                        case DeliveryKind.ArmBall: turn = natural * t0 * 0.08; break;
                        case DeliveryKind.Flighted: turn = natural * t0 * 1.2; break;
                        case DeliveryKind.Yorker: turn = 0; break;
                        default: turn = natural * t0; break;
                    }
                    turn += rng.Gauss() * 0.15;
                }
                else
                {
                    turn = rng.Gauss() * 0.35 * venue.PitchPace;
                }
                tr.TurnAmount = turn;
                tr.V1 = new V3(vin.x * 0.96 + turn, -vin.y * e, vin.z * keep);
            }

            // Where the batter meets it.
            tr.FrontFoot = length < 6.6;
            tr.ContactZ = tr.FrontFoot ? Pitch.StrikerCreaseZ + 0.75 : Pitch.StrikerCreaseZ - 0.35;
            if (tr.LengthClass == LengthClass.Yorker) tr.ContactZ = Pitch.StrikerCreaseZ + 0.2;
            tr.TContact = tr.TimeAtZ(tr.ContactZ);
            var atBat = tr.PosAt(tr.TContact);
            tr.LineAtBat = atBat.x * side;
            tr.HeightAtBat = atBat.y;

            var atStumps = tr.PosAt(tr.TStumps);
            tr.HitsStumps = Math.Abs(atStumps.x) <= Pitch.StumpHalfWidth + Pitch.BallRadius && atStumps.y <= Pitch.StumpHeight + 0.03;

            // Legality.
            double creaseT = tr.TimeAtZ(Pitch.StrikerCreaseZ);
            var atCrease = tr.PosAt(creaseT);
            double lat = atCrease.x * side;
            tr.Wide = lat > Pitch.WideOff || lat < -Pitch.WideLeg || (tr.LengthClass == LengthClass.Bouncer && atCrease.y > 2.15);
            tr.Beamer = tr.FullToss && tr.HeightAtBat > 1.05;
            double overstep = acc < 0.12 ? 0.25 : acc < 0.25 ? 0.06 : 0.006;
            tr.NoBall = tr.Beamer || rng.Chance(overstep);

            // Quality: on a testing line and length, well executed, and how much it moves.
            double q = acc * 0.5 + skill / 100.0 * 0.3;
            if (Math.Abs(tr.LineAtBat - 0.1) < 0.3) q += 0.1;
            if (tr.LengthClass == LengthClass.Yorker || tr.LengthClass == LengthClass.Good) q += 0.1;
            if (tr.LengthClass == LengthClass.FullToss) q -= 0.35;
            if (spin) q += Math.Abs(tr.TurnAmount) * 0.06;
            else q += Math.Abs(swing) * 0.03;
            if (plan.Kind == DeliveryKind.Slower || plan.Kind == DeliveryKind.Wrongun) q += 0.08;
            tr.Quality = MathX.Clamp01(q);
            return tr;
        }
    }
}
