using System;

namespace KPL.Core
{
    public enum ShotKind { Leave, Defend, Ground, Loft }

    public enum TimingGrade { Perfect, Good, Early, Late, VeryEarly, VeryLate, Miss, None }

    public enum ContactType { Left, Missed, Defended, Clean, OutsideEdge, InsideEdge, TopEdge, LeadingEdge }

    /// <summary>What the batter tries: a shot type, a direction and when the bat comes down.</summary>
    public struct ShotInput
    {
        public ShotKind Kind;
        /// <summary>Batter-relative angle: 0 = straight, + = off side, - = leg side, ±180 = behind.</summary>
        public double Azimuth;
        /// <summary>Press time minus ideal contact time in ms (negative = early). Null = no shot.</summary>
        public double? TimingMs;
        /// <summary>Spinner only: the batter came down the track (lofted shots), so a miss can be stumped.</summary>
        public bool Charged;
        /// <summary>Soft hands: placed into a gap for a single rather than struck.</summary>
        public bool Soft;

        public static ShotInput Leave => new ShotInput { Kind = ShotKind.Leave };
    }

    /// <summary>Timing windows in milliseconds either side of the ideal contact moment.</summary>
    public struct TimingWindows
    {
        public double Perfect, Good, Ok;

        public static TimingWindows For(Difficulty d, double bonus = 0)
        {
            TimingWindows w;
            switch (d)
            {
                case Difficulty.Easy: w = new TimingWindows { Perfect = 55, Good = 110, Ok = 185 }; break;
                case Difficulty.Medium: w = new TimingWindows { Perfect = 38, Good = 82, Ok = 145 }; break;
                case Difficulty.Hard: w = new TimingWindows { Perfect = 33, Good = 72, Ok = 130 }; break;
                default: w = new TimingWindows { Perfect = 25, Good = 58, Ok = 110 }; break;
            }
            double k = 1 + bonus;
            w.Perfect *= k;
            w.Good *= k;
            w.Ok *= k;
            return w;
        }

        public TimingGrade Grade(double ms)
        {
            double a = Math.Abs(ms);
            if (a <= Perfect) return TimingGrade.Perfect;
            if (a <= Good) return TimingGrade.Good;
            if (a <= Ok) return ms < 0 ? TimingGrade.Early : TimingGrade.Late;
            if (a <= Ok * 1.7) return ms < 0 ? TimingGrade.VeryEarly : TimingGrade.VeryLate;
            return TimingGrade.Miss;
        }
    }

    /// <summary>Result of bat meeting (or missing) ball.</summary>
    public sealed class Contact
    {
        public ContactType Type;
        public TimingGrade Grade = TimingGrade.None;
        public V3 Pos, Vel;
        public double Time;
        public string ShotName = "";
        public double Quality;
        public double Suitability;
        public ShotInput Input;
        /// <summary>The ball went on to hit the stumps (bowled, played on) or the pad in line (LBW).</summary>
        public bool Bowled, Lbw, PlayedOn, Stumped;
        public bool HitPad;

        public bool BatHit => Type == ContactType.Clean || Type == ContactType.Defended || Type == ContactType.OutsideEdge ||
                              Type == ContactType.InsideEdge || Type == ContactType.TopEdge || Type == ContactType.LeadingEdge;
    }

    public static class Batting
    {
        static int Bucket(double az)
        {
            double a = Math.Abs(az);
            if (a < 22) return 0; // straight
            if (az > 0)
            {
                if (a < 72) return 1; // cover / extra cover
                if (a < 118) return 2; // point
                return 3; // third man
            }
            if (a < 72) return 4; // mid-wicket / mid-on
            if (a < 118) return 5; // square leg
            return 6; // fine leg
        }

        static readonly double[][] LenSuit =
        {
            // straight, cover, point, third, midwkt, sqleg, fine
            new[] { 0.85, 0.85, 0.6, 0.2, 0.85, 0.7, 0.35 }, // full toss
            new[] { 0.25, -0.15, -0.55, 0.05, 0.05, -0.4, 0.1 }, // yorker
            new[] { 0.9, 0.85, 0.2, -0.3, 0.7, 0.2, 0.0 }, // full
            new[] { 0.4, 0.45, 0.35, 0.3, 0.35, 0.2, 0.25 }, // good
            new[] { -0.4, 0.0, 0.75, 0.5, 0.65, 0.8, 0.5 }, // short
            new[] { -0.9, -0.6, 0.2, 0.6, 0.1, 0.6, 0.7 }, // bouncer
        };

        /// <summary>How well a shot direction suits the ball (-1 = asking for trouble, 1 = the textbook shot).</summary>
        public static double Suitability(DeliveryTrack tr, ShotKind kind, double az)
        {
            if (kind == ShotKind.Defend) return tr.LengthClass == LengthClass.Bouncer ? 0.2 : 0.8;
            if (kind == ShotKind.Leave) return 1;
            int b = Bucket(az);
            double s = LenSuit[(int)tr.LengthClass][b];
            double line = tr.LineAtBat;
            bool off = b == 1 || b == 2 || b == 3, leg = b == 4 || b == 5 || b == 6;
            if (line > 0.45)
            {
                if (off) s += 0.15;
                if (leg) s -= 0.6;
                if (b == 0) s -= 0.3;
            }
            else if (line > 0.12)
            {
                if (off) s += 0.1;
                if (leg) s -= 0.25;
            }
            else if (line > -0.12)
            {
                if (b == 0) s += 0.15;
                if (b == 2) s -= 0.15;
            }
            else
            {
                if (leg) s += 0.25;
                if (off) s -= 0.45;
                if (b == 0) s -= 0.2;
            }
            bool spin = tr.BowlType.IsSpin();
            if (spin && (b == 5 || b == 6) && (tr.LengthClass == LengthClass.Full || tr.LengthClass == LengthClass.Good)) s += 0.3;
            if (kind == ShotKind.Loft)
            {
                if (tr.LengthClass == LengthClass.Good) s -= 0.15;
                if (tr.LengthClass == LengthClass.Yorker) s -= 0.3;
                if (tr.LengthClass == LengthClass.Full && b == 0) s += 0.1;
                if (spin && tr.LengthClass == LengthClass.Full) s += 0.1;
            }
            return MathX.Clamp(s, -1, 1);
        }

        public static string ShotName(DeliveryTrack tr, ShotKind kind, double az, bool spin)
        {
            int b = Bucket(az);
            var len = tr.LengthClass;
            bool shortish = len == LengthClass.Short || len == LengthClass.Bouncer;
            if (kind == ShotKind.Defend) return tr.FrontFoot ? "FORWARD DEFENCE" : "BACK-FOOT DEFENCE";
            if (kind == ShotKind.Leave) return "LEAVE";
            bool loft = kind == ShotKind.Loft;
            switch (b)
            {
                case 0:
                    if (len == LengthClass.Yorker) return loft ? "HELICOPTER SHOT" : "DIG OUT";
                    if (shortish) return loft ? "SLOG" : "BACK-FOOT PUNCH";
                    return loft ? "LOFTED DRIVE" : "STRAIGHT DRIVE";
                case 1:
                    if (shortish) return loft ? "UPPER CUT" : "BACK-FOOT DRIVE";
                    return loft ? "INSIDE-OUT LOFT" : "COVER DRIVE";
                case 2:
                    if (shortish) return loft ? "UPPER CUT" : "SQUARE CUT";
                    return loft ? "SLICE" : "SQUARE DRIVE";
                case 3:
                    if (len == LengthClass.Yorker || len == LengthClass.Full) return loft ? "REVERSE SCOOP" : "LATE CUT";
                    return loft ? "RAMP" : "DAB";
                case 4:
                    if (shortish) return loft ? "PULL SHOT" : "PULL";
                    if (len == LengthClass.Yorker) return loft ? "HELICOPTER SHOT" : "FLICK";
                    return loft ? "SLOG OVER MID-WICKET" : "ON DRIVE";
                case 5:
                    if (shortish) return loft ? "HOOK" : "PULL";
                    if (spin) return loft ? "SLOG SWEEP" : "SWEEP";
                    return loft ? "LOFTED FLICK" : "CLIP OFF THE PADS";
                default:
                    if (shortish) return loft ? "HOOK" : "GLANCE";
                    if (spin) return loft ? "PADDLE SCOOP" : "PADDLE SWEEP";
                    return loft ? "SCOOP" : "LEG GLANCE";
            }
        }

        /// <summary>
        /// Resolves the batter's shot against a delivery. The returned contact carries the ball's
        /// new velocity; fielding and running are worked out by <see cref="Fielding"/>.
        /// </summary>
        public static Contact Resolve(DeliveryTrack tr, Player batter, ShotInput s, TimingWindows w, double powerBonus, Rng rng)
        {
            var c = new Contact { Input = s, Time = tr.TContact, Pos = tr.PosAt(tr.TContact) };
            bool spin = tr.BowlType.IsSpin();
            if (s.Kind == ShotKind.Leave || s.TimingMs == null)
            {
                c.Type = ContactType.Left;
                c.ShotName = "LEAVE";
                MissOutcome(tr, batter, c, s, rng, true);
                return c;
            }

            // Lofted shots demand cleaner timing.
            var grade = s.Kind == ShotKind.Loft ? new TimingWindows { Perfect = w.Perfect * 0.85, Good = w.Good * 0.9, Ok = w.Ok }.Grade(s.TimingMs.Value) : w.Grade(s.TimingMs.Value);
            c.Grade = grade;
            double suit = Suitability(tr, s.Kind, s.Azimuth);
            c.Suitability = suit;
            c.ShotName = ShotName(tr, s.Kind, s.Azimuth, spin);
            if (grade == TimingGrade.Miss)
            {
                c.Type = ContactType.Missed;
                MissOutcome(tr, batter, c, s, rng, false);
                return c;
            }

            double tq;
            switch (grade)
            {
                case TimingGrade.Perfect: tq = 1.0; break;
                case TimingGrade.Good: tq = 0.78; break;
                case TimingGrade.Early:
                case TimingGrade.Late: tq = 0.45; break;
                default: tq = 0.2; break;
            }
            double q = tq + 0.22 * suit + (batter.Bat - 70) / 220.0;
            if ((tr.Kind == DeliveryKind.Slower || tr.Kind == DeliveryKind.Wrongun) && grade != TimingGrade.Perfect) q -= 0.12;
            if (tr.Kind == DeliveryKind.ArmBall && (grade == TimingGrade.Late || grade == TimingGrade.VeryLate)) q -= 0.1;
            q = MathX.Clamp(q, 0, 1.15);
            c.Quality = q;

            // Bad shot selection or poor timing: play and miss, or find an edge.
            double pMiss = suit < 0 ? 0.3 * -suit * (1 - tq * 0.55) : 0.0;
            if (grade == TimingGrade.Early || grade == TimingGrade.Late) pMiss += 0.06;
            if (grade == TimingGrade.VeryEarly || grade == TimingGrade.VeryLate) pMiss += 0.32;
            if (tr.Quality > 0.75 && grade != TimingGrade.Perfect) pMiss += 0.04;
            if (s.Kind == ShotKind.Defend) pMiss *= 0.3;
            double pEdge = grade == TimingGrade.Perfect || grade == TimingGrade.Good
                ? 0.007 + Math.Max(0, -suit) * 0.11
                : MathX.Clamp(0.035 + (1 - q) * 0.17 + Math.Max(0, -suit) * 0.2, 0, 0.6);
            if (s.Kind == ShotKind.Defend) pEdge *= 0.55;
            if (Math.Abs(tr.LineAtBat) > 0.75) pEdge *= 0.6;

            double r = rng.Next();
            if (r < pMiss)
            {
                c.Type = ContactType.Missed;
                MissOutcome(tr, batter, c, s, rng, false);
                return c;
            }
            if (r < pMiss + pEdge)
            {
                Edge(tr, batter, c, s, rng);
                return c;
            }

            double incoming = tr.VelAt(tr.TContact).Length;
            double pw = 0.75 + batter.Power / 100.0 * 0.38 + powerBonus / 100.0;
            double noise = grade == TimingGrade.Perfect ? 3 : grade == TimingGrade.Good ? 7 : grade == TimingGrade.Early || grade == TimingGrade.Late ? 13 : 20;
            double az = s.Azimuth + MathX.Clamp(s.TimingMs.Value * 0.11, -28, 28) + rng.Gauss() * noise;
            double speed, el;
            switch (s.Kind)
            {
                case ShotKind.Defend:
                    c.Type = ContactType.Defended;
                    speed = rng.Range(2.0, 6.5);
                    el = rng.Range(-3, 3);
                    az = s.Azimuth * 0.5 + rng.Gauss() * 14;
                    break;
                case ShotKind.Loft:
                    c.Type = ContactType.Clean;
                    speed = (12 + 22 * q) * pw + incoming * (spin ? 0.02 : 0.04);
                    el = grade == TimingGrade.Perfect ? rng.Range(28, 36) : grade == TimingGrade.Good ? rng.Range(22, 42)
                        : rng.Chance(0.5) ? rng.Range(42, 60) : rng.Range(12, 22);
                    break;
                default:
                    c.Type = ContactType.Clean;
                    // Ground shots are hit down: the ball pitches a few metres away and runs along the turf.
                    speed = (9 + 21 * q) * pw + incoming * (spin ? 0.03 : 0.08);
                    if (s.Soft) speed *= rng.Range(0.5, 0.68);
                    el = grade == TimingGrade.Perfect ? rng.Range(-9, -2) : grade == TimingGrade.Good ? rng.Range(-7, 2)
                        : grade == TimingGrade.Early || grade == TimingGrade.Late ? rng.Range(-4, 9) : rng.Range(0, 14);
                    if (tr.LengthClass == LengthClass.Bouncer || tr.LengthClass == LengthClass.Short) el += rng.Range(0, 4);
                    break;
            }
            c.Vel = Launch(az, el, speed, batter.LeftBat);
            return c;
        }

        static V3 Launch(double az, double el, double speed, bool leftBat)
        {
            var d = Pitch.Dir(az, leftBat);
            double e = el * MathX.Deg;
            return d * (speed * Math.Cos(e)) + V3.Up * (speed * Math.Sin(e));
        }

        static void Edge(DeliveryTrack tr, Player batter, Contact c, ShotInput s, Rng rng)
        {
            double incoming = tr.VelAt(tr.TContact).Length;
            bool legShot = s.Azimuth < -22;
            bool shortish = tr.LengthClass == LengthClass.Short || tr.LengthClass == LengthClass.Bouncer;
            if (legShot && shortish && rng.Chance(0.65))
            {
                c.Type = ContactType.TopEdge;
                c.ShotName = "TOP EDGE";
                c.Vel = Launch(rng.Chance(0.6) ? rng.Range(-175, -120) : rng.Range(-60, 20), rng.Range(52, 74), rng.Range(17, 25), batter.LeftBat);
                return;
            }
            if (legShot && tr.LineAtBat > 0.0 && rng.Chance(0.6))
            {
                c.Type = ContactType.LeadingEdge;
                c.ShotName = "LEADING EDGE";
                c.Vel = Launch(rng.Range(25, 95), rng.Range(28, 52), rng.Range(10, 17), batter.LeftBat);
                return;
            }
            if (tr.LineAtBat < 0.12 && rng.Chance(0.4))
            {
                c.Type = ContactType.InsideEdge;
                c.ShotName = "INSIDE EDGE";
                if (tr.HitsStumps && rng.Chance(0.35))
                {
                    c.PlayedOn = true;
                    c.Bowled = true;
                    c.Vel = new V3(rng.Range(-1, 1), 0.5, -4);
                    return;
                }
                c.Vel = Launch(rng.Range(-175, -140), rng.Range(-4, 6), incoming * rng.Range(0.25, 0.45), batter.LeftBat);
                return;
            }
            c.Type = ContactType.OutsideEdge;
            c.ShotName = "OUTSIDE EDGE";
            double el = s.Kind == ShotKind.Defend ? rng.Range(-2, 9) : rng.Range(2, 16);
            c.Vel = Launch(rng.Range(150, 176), el, incoming * rng.Range(0.42, 0.7), batter.LeftBat);
        }

        static void MissOutcome(DeliveryTrack tr, Player batter, Contact c, ShotInput s, Rng rng, bool left)
        {
            c.Time = tr.TContact;
            if (tr.Wide && !tr.HitsStumps) return;
            if (tr.HitsStumps)
            {
                bool pitchedOutsideLeg = !tr.FullToss && tr.Bounce.x * (tr.LeftBat ? -1 : 1) < -(Pitch.StumpHalfWidth + 0.02);
                bool padInLine = tr.LineAtBat > -0.22 && tr.LineAtBat < 0.3 && tr.HeightAtBat < 0.78;
                double padChance = tr.FrontFoot ? 0.62 : 0.5;
                if (tr.LengthClass == LengthClass.Yorker) padChance = 0.45;
                if (left) padChance += 0.15;
                if (padInLine && rng.Chance(padChance))
                {
                    c.HitPad = true;
                    // The umpire gives it unless it pitched outside leg.
                    c.Lbw = !pitchedOutsideLeg && rng.Chance(0.88);
                }
                else
                {
                    c.Bowled = true;
                }
                return;
            }
            if (tr.BowlType.IsSpin() && !left)
            {
                double p = s.Charged ? 0.6 : s.Kind == ShotKind.Loft ? 0.22 : 0.05;
                c.Stumped = rng.Chance(p);
            }
            if (!c.Stumped && tr.LineAtBat > -0.25 && tr.LineAtBat < 0.4 && tr.HeightAtBat < 0.9 && rng.Chance(left ? 0.12 : 0.3)) c.HitPad = true;
        }
    }
}
