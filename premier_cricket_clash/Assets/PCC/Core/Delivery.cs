using System;

namespace PCC.Core
{
    public enum DeliveryType { Yorker, WideYorker, Full, GoodLength, Short, Bouncer, Slower, Flighted, Stock, Quicker, Variation, FullToss }

    public enum BallLine { WideOff, Off, Middle, Leg }

    /// <summary>Shot directions. Angle: 0 = straight, +90 = square off side, -90 = square leg side.</summary>
    public enum ShotDir { Straight, Cover, Point, ThirdMan, MidWicket, SquareLeg, FineLeg }

    public enum TimingGrade { Perfect, Good, Early, Late, VeryEarly, VeryLate, Miss }

    public enum Difficulty { Easy, Normal, Hard }

    public static class DeliveryX
    {
        static readonly string[] Labels =
            { "Yorker", "Wide yorker", "Full", "Good length", "Short", "Bouncer", "Slower ball", "Flighted", "Stock ball", "Quicker", "Mystery ball", "Full toss" };
        static readonly string[] Shorts =
            { "YORKER", "WIDE YKR", "FULL", "GOOD", "SHORT", "BOUNCER", "SLOWER", "FLIGHT", "STOCK", "QUICKER", "MYSTERY", "FULL TOSS" };
        static readonly double[] PitchFrom = { 1.3, 1.4, 3.2, 6.2, 8.4, 10.8, 5.2, 3.2, 4.8, 5.8, 4.6, 0.0 };
        static readonly double[] Arrive = { 0.1, 0.12, 0.38, 0.78, 1.15, 1.8, 0.62, 0.42, 0.6, 0.55, 0.6, 1.0 };

        public static string Label(this DeliveryType t) => Labels[(int)t];
        public static string Short(this DeliveryType t) => Shorts[(int)t];
        public static bool IsSpinType(this DeliveryType t) => t >= DeliveryType.Flighted && t <= DeliveryType.Variation;

        /// <summary>Metres in front of the batter's stumps where the ball pitches (0 = full toss).</summary>
        public static double PitchFromStumps(this DeliveryType t) => PitchFrom[(int)t];

        /// <summary>Height (m) of the ball when it reaches the batter.</summary>
        public static double ArriveHeight(this DeliveryType t) => Arrive[(int)t];

        public static int LengthRow(this DeliveryType t)
        {
            switch (t)
            {
                case DeliveryType.Yorker:
                case DeliveryType.WideYorker: return 0;
                case DeliveryType.Full:
                case DeliveryType.Flighted: return 1;
                case DeliveryType.Short:
                case DeliveryType.Quicker: return 3;
                case DeliveryType.Bouncer: return 4;
                case DeliveryType.FullToss: return 5;
                default: return 2;
            }
        }

        static readonly string[] LineLabels = { "Wide off", "Off stump", "Middle", "Leg stump" };
        static readonly double[] Legward = { -0.78, -0.12, 0.0, 0.14 };
        public static string Label(this BallLine l) => LineLabels[(int)l];

        /// <summary>Lateral offset (m) from middle stump towards the batter's leg side.</summary>
        public static double LegwardOffset(this BallLine l) => Legward[(int)l];

        static readonly string[] DirLabels = { "STRAIGHT", "COVER", "POINT", "THIRD MAN", "MID-WICKET", "SQUARE LEG", "FINE LEG" };
        static readonly double[] DirAngles = { 0, 52, 95, 145, -52, -95, -150 };
        public static string Label(this ShotDir d) => DirLabels[(int)d];
        public static double Angle(this ShotDir d) => DirAngles[(int)d];

        /// <summary>Nearest shot direction to a field angle in degrees.</summary>
        public static ShotDir Nearest(double angle)
        {
            var best = ShotDir.Straight;
            double bd = 1e9;
            foreach (ShotDir d in Enum.GetValues(typeof(ShotDir)))
            {
                double diff = Math.Abs(d.Angle() - angle);
                if (diff > 180) diff = 360 - diff;
                if (diff < bd)
                {
                    bd = diff;
                    best = d;
                }
            }
            return best;
        }

        static readonly string[] GradeLabels = { "PERFECT", "GOOD", "EARLY", "LATE", "TOO EARLY", "TOO LATE", "MISSED" };
        public static string Label(this TimingGrade g) => GradeLabels[(int)g];

        static readonly string[] DiffLabels = { "EASY", "NORMAL", "HARD" };
        static readonly double[] SlowMos = { 2.7, 2.2, 1.8 };
        static readonly TimingWindows[] WindowTable =
        {
            new TimingWindows(75, 150, 240, 340),
            new TimingWindows(58, 115, 195, 285),
            new TimingWindows(42, 88, 150, 230),
        };
        static readonly double[] AiBonus = { -0.08, 0.0, 0.06 };

        public static string Label(this Difficulty d) => DiffLabels[(int)d];

        /// <summary>Real ball flight time is multiplied by this so humans can react.</summary>
        public static double SlowMo(this Difficulty d) => SlowMos[(int)d];
        public static TimingWindows Timing(this Difficulty d) => WindowTable[(int)d];
        public static double AiBowlBonus(this Difficulty d) => AiBonus[(int)d];
    }

    public sealed class TimingWindows
    {
        public TimingWindows(double perfect, double good, double ok, double poor)
        {
            Perfect = perfect;
            Good = good;
            Ok = ok;
            Poor = poor;
        }

        public double Perfect { get; }
        public double Good { get; }
        public double Ok { get; }
        public double Poor { get; }

        public TimingGrade Grade(double errMs)
        {
            double a = Math.Abs(errMs);
            if (a <= Perfect) return TimingGrade.Perfect;
            if (a <= Good) return TimingGrade.Good;
            if (a <= Ok) return errMs < 0 ? TimingGrade.Early : TimingGrade.Late;
            if (a <= Poor) return errMs < 0 ? TimingGrade.VeryEarly : TimingGrade.VeryLate;
            return TimingGrade.Miss;
        }
    }

    /// <summary>The batter's input for one ball.</summary>
    public sealed class ShotInput
    {
        public ShotDir? Dir;
        public bool Loft;
        public bool Defend;

        /// <summary>Signed timing error in ms (negative = early). Null when no shot was played.</summary>
        public double? TimingMs;

        public bool Leave => Dir == null && !Defend;

        public static readonly ShotInput None = new ShotInput();
    }

    /// <summary>A planned delivery - drives both the outcome model and the 3D animation.</summary>
    public sealed class DeliveryPlan
    {
        public DeliveryType Type;
        public BallLine Line;

        /// <summary>km/h.</summary>
        public double Speed;

        /// <summary>Lateral drift in the air (m, +x = right-hander's leg side).</summary>
        public double Swing;

        /// <summary>Lateral deviation after pitching (m).</summary>
        public double Turn;

        /// <summary>0..1 execution accuracy.</summary>
        public double Accuracy;
        public bool LeftArm;
        public bool Spin;
        public bool Wide;
        public bool NoBall;

        /// <summary>Exact pitching point / line (world metres) when the user aimed with the target.</summary>
        public double? PitchY;
        public double? LineX;

        public bool Bounces => Type != DeliveryType.FullToss;

        /// <summary>Would this delivery go on to hit the stumps (for bowled / LBW)?</summary>
        public bool HitsStumps(bool leftHandBatter)
        {
            if (Wide) return false;
            if (Type == DeliveryType.Bouncer || Type == DeliveryType.Short || Type == DeliveryType.WideYorker) return false;
            double dir = leftHandBatter ? -1 : 1;
            double lat = Line.LegwardOffset() + (Swing + Turn) * dir;
            return lat > -0.16 && lat < 0.16;
        }

        public string Describe()
        {
            string s = Math.Round(Speed) + " km/h • " + Type.Label();
            if (!Spin && Math.Abs(Swing) > 0.12) s += Swing > 0 ? " • Inswing" : " • Outswing";
            if (Spin && Math.Abs(Turn) > 0.12) s += " • Turning";
            if (Wide) s += " • WIDE";
            if (NoBall) s += " • NO BALL";
            return s;
        }
    }

    public static class Deliveries
    {
        public static readonly DeliveryType[] Pace =
            { DeliveryType.Yorker, DeliveryType.Full, DeliveryType.GoodLength, DeliveryType.Short, DeliveryType.Bouncer, DeliveryType.Slower, DeliveryType.WideYorker };
        public static readonly DeliveryType[] Spin =
            { DeliveryType.Flighted, DeliveryType.Stock, DeliveryType.Quicker, DeliveryType.Variation };

        /// <summary>AI delivery for <paramref name="bowler"/> in a match phase (0 powerplay, 1 middle, 2 death).</summary>
        public static DeliveryPlan PlanAi(Rng rng, Player bowler, int phase, double spinFactor = 1.0, double accuracyBonus = 0)
        {
            bool spin = bowler.IsSpin;
            DeliveryType type;
            if (spin)
            {
                bool mystery = bowler.Bowl >= 84;
                type = rng.Weighted(
                    new[] { DeliveryType.Stock, DeliveryType.Flighted, DeliveryType.Quicker, DeliveryType.Variation },
                    new[] { 0.4, phase == 2 ? 0.1 : 0.22, phase == 2 ? 0.34 : 0.22, mystery ? 0.26 : 0.12 });
            }
            else
            {
                switch (phase)
                {
                    case 0:
                        type = rng.Weighted(
                            new[] { DeliveryType.GoodLength, DeliveryType.Full, DeliveryType.Short, DeliveryType.Bouncer, DeliveryType.Yorker, DeliveryType.Slower },
                            new[] { 0.4, 0.24, 0.12, 0.06, 0.07, 0.11 });
                        break;
                    case 1:
                        type = rng.Weighted(
                            new[] { DeliveryType.GoodLength, DeliveryType.Short, DeliveryType.Full, DeliveryType.Slower, DeliveryType.Bouncer, DeliveryType.Yorker },
                            new[] { 0.34, 0.16, 0.14, 0.16, 0.08, 0.12 });
                        break;
                    default:
                        type = rng.Weighted(
                            new[] { DeliveryType.Yorker, DeliveryType.WideYorker, DeliveryType.Slower, DeliveryType.Full, DeliveryType.GoodLength, DeliveryType.Bouncer },
                            new[] { 0.34, 0.15, 0.2, 0.08, 0.1, 0.13 });
                        break;
                }
            }
            var line = type == DeliveryType.WideYorker
                ? BallLine.WideOff
                : rng.Weighted(new[] { BallLine.Off, BallLine.Middle, BallLine.WideOff, BallLine.Leg }, new[] { 0.46, 0.26, 0.14, 0.14 });
            double accuracy = Math.Clamp(0.6 + bowler.Bowl / 260.0 + accuracyBonus + rng.Gauss() * 0.08, 0.2, 1.0);
            // Execution errors: missed yorkers become full tosses etc.
            if (accuracy < 0.55)
            {
                if (type == DeliveryType.Yorker || type == DeliveryType.WideYorker || type == DeliveryType.Slower)
                    type = rng.Chance(0.6) ? DeliveryType.FullToss : DeliveryType.Full;
                else if (type == DeliveryType.GoodLength || type == DeliveryType.Stock)
                    type = rng.Chance(0.5) ? DeliveryType.Full : DeliveryType.Short;
            }
            bool wide = rng.Chance(type == DeliveryType.WideYorker ? 0.12 : (spin ? 0.015 : 0.03));
            if (wide && line != BallLine.WideOff) line = rng.Chance(0.5) ? BallLine.WideOff : BallLine.Leg;
            bool noBall = !spin && rng.Chance(0.006);
            return Build(rng, bowler, type, line, accuracy, wide, noBall, phase, spinFactor);
        }

        /// <summary>The user's chosen delivery with the accuracy from the meter.</summary>
        public static DeliveryPlan PlanUser(Rng rng, Player bowler, DeliveryType type, BallLine line, double accuracy, int phase,
            double spinFactor = 1.0, double? aimY = null, double? aimX = null, double? swing = null)
        {
            var t = type;
            var l = line;
            bool wide = false, noBall = false;
            if (accuracy < 0.15)
            {
                if (rng.Chance(0.55))
                {
                    wide = true;
                    l = rng.Chance(0.5) ? BallLine.WideOff : BallLine.Leg;
                }
                else
                {
                    t = DeliveryType.FullToss;
                    noBall = !bowler.IsSpin && rng.Chance(0.4);
                }
            }
            else if (accuracy < 0.4)
            {
                if (t == DeliveryType.Yorker || t == DeliveryType.WideYorker) t = rng.Chance(0.55) ? DeliveryType.FullToss : DeliveryType.Full;
                else if (t == DeliveryType.Bouncer)
                {
                    if (rng.Chance(0.35)) wide = true;
                    else t = DeliveryType.Short;
                }
                else if (t == DeliveryType.GoodLength || t == DeliveryType.Stock || t == DeliveryType.Slower) t = rng.Chance(0.5) ? DeliveryType.Full : DeliveryType.Short;
                else if (t == DeliveryType.Flighted) t = DeliveryType.FullToss;
            }
            else if (t == DeliveryType.WideYorker && accuracy < 0.6 && rng.Chance(0.3))
            {
                wide = true;
            }
            var built = Build(rng, bowler, t, l, accuracy, wide, noBall, phase, spinFactor);
            // Execution error scatters the ball around the dropped target.
            double err = 1 - accuracy;
            if (aimY != null && t != DeliveryType.FullToss) built.PitchY = aimY.Value + rng.Gauss() * err * 1.1;
            if (aimX != null && !wide) built.LineX = aimX.Value + rng.Gauss() * err * 0.22;
            if (swing != null) built.Swing = swing.Value;
            return built;
        }

        static DeliveryPlan Build(Rng rng, Player bowler, DeliveryType type, BallLine line, double accuracy, bool wide, bool noBall, int phase, double spinFactor)
        {
            bool spin = bowler.IsSpin;
            double speed = bowler.BowlStyle.BaseSpeed() + rng.Gauss() * 3;
            if (speed < 60) speed = 110 + rng.Gauss() * 4; // part-timers
            if (type == DeliveryType.Slower) speed *= 0.8;
            if (type == DeliveryType.Quicker) speed *= 1.08;
            if (type == DeliveryType.Flighted) speed *= 0.92;
            if (type == DeliveryType.Bouncer) speed += 3;
            double swing = 0, turn = 0;
            if (spin)
            {
                double t = bowler.BowlStyle.StockTurn() * rng.Range(0.12, 0.42) * spinFactor;
                if (type == DeliveryType.Variation) t = -t * 0.9;
                if (type == DeliveryType.Quicker) t *= 0.45;
                if (type == DeliveryType.Flighted) t *= 1.2;
                turn = t;
            }
            else
            {
                double amt = phase == 0 ? rng.Range(0.05, 0.32) : rng.Range(0.0, 0.14);
                swing = (rng.Chance(0.5) ? 1 : -1) * amt;
                turn = rng.Gauss() * 0.05; // seam
            }
            return new DeliveryPlan
            {
                Type = type,
                Line = line,
                Speed = speed,
                Swing = swing,
                Turn = turn,
                Accuracy = accuracy,
                LeftArm = bowler.BowlStyle.LeftArm(),
                Spin = spin,
                Wide = wide,
                NoBall = noBall,
            };
        }
    }
}
