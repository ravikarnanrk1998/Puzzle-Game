using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    public enum FieldSetting { Attacking, Balanced, Defensive }

    public enum WicketKind { Bowled, Caught, CaughtBehind, CaughtAndBowled, Lbw, RunOut, Stumped }

    public static class WicketKindX
    {
        public static string Label(this WicketKind k)
        {
            switch (k)
            {
                case WicketKind.Bowled: return "BOWLED";
                case WicketKind.Caught: return "CAUGHT";
                case WicketKind.CaughtBehind: return "CAUGHT BEHIND";
                case WicketKind.CaughtAndBowled: return "CAUGHT & BOWLED";
                case WicketKind.Lbw: return "LBW";
                case WicketKind.RunOut: return "RUN OUT";
                default: return "STUMPED";
            }
        }

        public static bool BowlerCredit(this WicketKind k) => k != WicketKind.RunOut;
    }

    /// <summary>Where the nine fielders (plus keeper and bowler) stand.</summary>
    public sealed class FieldLayout
    {
        public string[] Names = new string[9];
        public V3[] Pos = new V3[9];
        public V3 Keeper, Bowler;
        public int OutCount;

        static readonly Dictionary<string, (double Az, double Dist)> Spots = new Dictionary<string, (double, double)>
        {
            { "Slip", (166, 13.5) }, { "Gully", (132, 17) }, { "Point", (95, 22) }, { "Deep Point", (100, 999) },
            { "Cover", (60, 26) }, { "Extra Cover", (38, 29) }, { "Deep Cover", (52, 999) }, { "Mid-off", (15, 28) },
            { "Long-off", (13, 999) }, { "Mid-on", (-15, 28) }, { "Long-on", (-13, 999) }, { "Mid-wicket", (-55, 26) },
            { "Deep Mid-wicket", (-55, 999) }, { "Square Leg", (-95, 22) }, { "Deep Square Leg", (-92, 999) },
            { "Fine Leg", (-155, 999) }, { "Short Fine Leg", (-150, 18) }, { "Third Man", (150, 999) }, { "Short Third", (138, 26) },
            { "Short Leg", (-75, 6.5) }, { "Silly Point", (75, 6.5) }, { "Leg Slip", (-165, 12) },
        };

        static readonly Dictionary<string, string[]> Presets = new Dictionary<string, string[]>
        {
            { "pp-pace-A", new[] { "Slip", "Gully", "Point", "Cover", "Mid-off", "Mid-on", "Mid-wicket", "Third Man", "Fine Leg" } },
            { "pp-pace-B", new[] { "Slip", "Point", "Cover", "Mid-off", "Mid-on", "Mid-wicket", "Square Leg", "Third Man", "Fine Leg" } },
            { "pp-pace-D", new[] { "Point", "Cover", "Extra Cover", "Mid-off", "Mid-on", "Mid-wicket", "Square Leg", "Third Man", "Fine Leg" } },
            { "pp-spin-A", new[] { "Slip", "Short Leg", "Point", "Cover", "Mid-off", "Mid-on", "Mid-wicket", "Deep Mid-wicket", "Long-on" } },
            { "pp-spin-B", new[] { "Point", "Cover", "Extra Cover", "Mid-off", "Mid-on", "Mid-wicket", "Square Leg", "Deep Mid-wicket", "Long-on" } },
            { "pp-spin-D", new[] { "Point", "Cover", "Mid-off", "Mid-on", "Mid-wicket", "Square Leg", "Short Fine Leg", "Deep Mid-wicket", "Long-on" } },
            { "mid-pace-A", new[] { "Slip", "Point", "Cover", "Mid-off", "Mid-on", "Square Leg", "Deep Mid-wicket", "Third Man", "Fine Leg" } },
            { "mid-pace-B", new[] { "Point", "Cover", "Mid-off", "Mid-on", "Square Leg", "Deep Cover", "Deep Mid-wicket", "Third Man", "Fine Leg" } },
            { "mid-pace-D", new[] { "Point", "Mid-off", "Mid-on", "Short Fine Leg", "Deep Cover", "Long-off", "Long-on", "Deep Mid-wicket", "Third Man" } },
            { "mid-spin-A", new[] { "Slip", "Short Leg", "Point", "Cover", "Mid-off", "Mid-on", "Mid-wicket", "Deep Mid-wicket", "Long-on" } },
            { "mid-spin-B", new[] { "Point", "Cover", "Mid-off", "Mid-wicket", "Short Fine Leg", "Long-off", "Long-on", "Deep Mid-wicket", "Deep Square Leg" } },
            { "mid-spin-D", new[] { "Point", "Mid-off", "Short Fine Leg", "Deep Cover", "Long-off", "Long-on", "Deep Mid-wicket", "Deep Square Leg", "Mid-on" } },
            { "death-pace-A", new[] { "Point", "Cover", "Mid-off", "Mid-on", "Short Fine Leg", "Long-off", "Long-on", "Deep Mid-wicket", "Third Man" } },
            { "death-pace-B", new[] { "Point", "Mid-off", "Mid-on", "Short Third", "Deep Cover", "Long-off", "Long-on", "Deep Mid-wicket", "Fine Leg" } },
            { "death-pace-D", new[] { "Mid-off", "Mid-on", "Short Third", "Deep Point", "Deep Cover", "Long-off", "Long-on", "Deep Mid-wicket", "Deep Square Leg" } },
        };

        /// <summary>Lays out the field for the striker's hand. Powerplay limits two fielders outside the circle.</summary>
        public static FieldLayout Create(FieldSetting setting, bool spinner, bool leftBat, Ground ground, int phase)
        {
            string ph = phase == 0 ? "pp" : phase == 1 ? "mid" : "death";
            string kind = spinner ? "spin" : "pace";
            if (ph == "death" && spinner) ph = "mid";
            string s = setting == FieldSetting.Attacking ? "A" : setting == FieldSetting.Balanced ? "B" : "D";
            var names = Presets[ph + "-" + kind + "-" + s];
            var lay = new FieldLayout();
            var striker = new V3(0, 0, Pitch.StrikerCreaseZ);
            for (int i = 0; i < 9; i++)
            {
                var (az, dist) = Spots[names[i]];
                var dir = Pitch.Dir(az, leftBat);
                lay.Names[i] = names[i];
                if (dist > 900)
                {
                    lay.Pos[i] = ground.RayToRope(striker, dir, 4.0);
                    lay.OutCount++;
                }
                else
                {
                    lay.Pos[i] = striker + dir * dist;
                }
            }
            lay.Keeper = spinner ? new V3(leftBat ? 0.25 : -0.25, 0, Pitch.StrikerStumpsZ - 0.9) : new V3(leftBat ? 0.4 : -0.4, 0, Pitch.StrikerStumpsZ - 12.0);
            lay.Bowler = new V3(0.8, 0, 3.0);
            return lay;
        }
    }

    /// <summary>
    /// Everything that happens after the ball reaches the batter: the struck ball's flight, which
    /// fielder gets there first, catches, throws, how many runs the batters take, and any wicket.
    /// The visual match replays this script; fast simulation just reads the outcome.
    /// </summary>
    public sealed class Play
    {
        public DeliveryTrack Track;
        public Contact Contact;
        public Flight Flight;

        // Scoring.
        public int Runs, Byes, LegByes;
        public bool Four, Six, Wide, NoBall;
        public WicketKind? Wicket;
        /// <summary>0 = striker, 1 = non-striker.</summary>
        public int OutBatter;
        /// <summary>Index into the fielding side's fielder list (0-8 = fielders, 9 = keeper, 10 = bowler), -1 none.</summary>
        public int Fielder = -1;
        public bool DirectHit, Overthrow;

        // Animation script (times are seconds after contact).
        public int Chaser = -1;
        public V3 ChaseFrom, ChaseTo;
        public double ChaseStart, ChaseArrive;
        public bool Dive;
        public bool CatchChance;
        public double CatchProb, CatchTime;
        public bool Dropped;
        public V3 PickPos;
        public double PickTime = -1;
        public bool Thrown;
        /// <summary>0 = keeper's end, 1 = bowler's end.</summary>
        public int ThrowEnd;
        public double ThrowStart, ThrowArrive;
        public double[] RunTimes = new double[0];
        public int RunsAttempted;
        public double Duration;
        public double BatterSpeed;
        public bool BoundaryOverRope;

        /// <summary>If the catch is pending (user fields), the result had the catch been dropped.</summary>
        public Play DropBranch;

        public bool IsDot => Runs == 0 && Byes == 0 && LegByes == 0 && !Wide && !NoBall && Wicket == null;
        public int TotalRuns => Runs + Byes + LegByes + (Wide ? 1 : 0) + (NoBall ? 1 : 0);
    }

    public static class Fielding
    {
        public sealed class Side
        {
            /// <summary>Fielders 0-8, keeper 9, bowler 10.</summary>
            public Player[] Players = new Player[11];
            public V3[] Pos = new V3[11];
            public FieldLayout Layout;
        }

        public static double RunSpeed(Player p) => 5.3 + p.Pace / 100.0 * 2.3;

        static double FielderSpeed(Player p) => 5.5 + p.Field / 100.0 * 1.8 + p.Pace / 100.0 * 0.6;

        static double ThrowSpeed(Player p, double dist) => (dist > 45 ? 23 : 26) + p.Field / 100.0 * 7;

        /// <summary>Resolves the play after contact. <paramref name="aggression"/> (0..1) affects how hard the batters run.</summary>
        public static Play Resolve(DeliveryTrack tr, Contact c, Side side, Player striker, Player nonStriker, Ground ground, double aggression, Rng rng)
        {
            var play = new Play { Track = tr, Contact = c, BatterSpeed = RunSpeed(striker) };
            play.Wide = tr.Wide && !c.BatHit;
            play.NoBall = tr.NoBall;
            if (!c.BatHit)
            {
                NoContact(play, tr, c, side, rng);
                return play;
            }
            if (c.PlayedOn)
            {
                play.Wicket = WicketKind.Bowled;
                play.Duration = 1.6;
                return play;
            }
            play.Flight = Flight.Simulate(c.Pos, c.Vel, ground);
            Chase(play, side, striker, nonStriker, ground, aggression, rng);
            return play;
        }

        static void NoContact(Play play, DeliveryTrack tr, Contact c, Side side, Rng rng)
        {
            play.Duration = 1.4;
            if (c.Bowled)
            {
                play.Wicket = WicketKind.Bowled;
                play.Duration = 2.0;
            }
            else if (c.Lbw)
            {
                play.Wicket = WicketKind.Lbw;
                play.Duration = 2.2;
            }
            else if (c.Stumped)
            {
                double p = 0.75 + side.Players[9].Field / 100.0 * 0.2;
                if (rng.Chance(p))
                {
                    play.Wicket = WicketKind.Stumped;
                    play.Fielder = 9;
                    play.Duration = 1.8;
                }
            }
            if (play.Wicket != null) return;
            // Byes and leg byes: the keeper misses a wayward ball, or the batters scamper off the pad.
            bool wayward = Math.Abs(tr.LineAtBat) > 0.7 || tr.LengthClass == LengthClass.Bouncer && tr.HeightAtBat > 2.0;
            double miss = (wayward ? 0.07 : 0.008) * (1.3 - side.Players[9].Field / 100.0);
            if (rng.Chance(miss))
            {
                play.Byes = rng.Chance(0.6) ? 4 : 1;
                play.Duration = play.Byes == 4 ? 3.2 : 3.0;
                return;
            }
            if (c.HitPad && rng.Chance(0.3))
            {
                play.LegByes = 1;
                play.Duration = 3.0;
                play.RunTimes = new[] { 3.0 };
            }
        }

        static void Chase(Play play, Side side, Player striker, Player nonStriker, Ground ground, double aggression, Rng rng)
        {
            var f = play.Flight;
            double bestT = double.MaxValue;
            int best = -1;
            bool bestAir = false, bestDive = false;
            V3 bestPos = V3.Zero;
            double bestNeedFrac = 0;
            for (int i = 0; i < 11; i++)
            {
                var fp = side.Players[i];
                if (fp == null) continue;
                var start = side.Pos[i];
                bool close = V3.DistXZ(start, new V3(0, 0, Pitch.StrikerCreaseZ)) < 24;
                double react = i == 10 ? 0.45 : close ? 0.22 : 0.33;
                double spd = FielderSpeed(fp);
                double reach = i == 9 ? 1.4 : 1.05;
                for (int k = 1; k < f.P.Count; k++)
                {
                    double t = k * Flight.Dt;
                    if (t > bestT) break;
                    if (f.TBoundary >= 0 && t >= f.TBoundary) break;
                    var b = f.P[k];
                    // The keeper only takes what goes past the stumps; everyone else ignores the batter's own space.
                    if (i == 9 && b.z > Pitch.StrikerStumpsZ + 0.3) continue;
                    if (i != 9 && b.z < Pitch.StrikerCreaseZ + 2.0 && b.z > Pitch.StrikerStumpsZ - 1.0 && Math.Abs(b.x) < 2.2) continue;
                    bool air = f.AirborneAt(t);
                    if (air ? (b.y > 2.55 || b.y < 0.12) : b.y > 1.3) continue;
                    double run = Math.Max(0, t - react) * spd;
                    double d = V3.DistXZ(b, start) - reach;
                    bool dive = false;
                    if (d > run)
                    {
                        double diveReach = air ? 1.7 : 1.3;
                        if (d - diveReach <= run && (air || b.LengthXZ > 0)) dive = true;
                        else continue;
                    }
                    if (dive && !air)
                    {
                        // A diving stop on a fast ground ball does not always hold it.
                        double hs = k + 1 < f.P.Count ? V3.DistXZ(f.P[k + 1], b) / Flight.Dt : 0;
                        if (hs > 12 && !rng.Chance(0.55)) break;
                    }
                    bestT = t;
                    best = i;
                    bestAir = air;
                    bestDive = dive;
                    bestPos = b;
                    bestNeedFrac = run <= 0 ? 1 : MathX.Clamp01(Math.Max(0, d) / Math.Max(0.01, run));
                    break;
                }
            }

            // Nobody gets there before the rope: boundary.
            if (best < 0)
            {
                double tb = f.TBoundary >= 0 ? f.TBoundary : f.Duration;
                var rope = f.TBoundary >= 0 ? f.At(f.TBoundary) : f.P[f.P.Count - 1];
                if (f.TBoundary >= 0)
                {
                    if (f.Six) play.Six = true;
                    else play.Four = true;
                    play.Runs = f.Six ? 6 : 4;
                }
                int ch = NearestFielder(side, rope);
                play.Chaser = ch;
                play.ChaseFrom = side.Pos[ch];
                play.ChaseTo = Towards(side.Pos[ch], rope, Math.Max(0, tb - 0.35) * FielderSpeed(side.Players[ch]));
                play.ChaseStart = 0.35;
                play.ChaseArrive = tb;
                play.Duration = tb + (f.Six ? 2.4 : 1.8);
                if (f.TBoundary < 0)
                {
                    // Stopped dead inside the rope with nobody near (very rare): batters run what they can.
                    play.PickPos = f.P[f.P.Count - 1];
                    play.PickTime = f.Duration + V3.DistXZ(play.ChaseTo, play.PickPos) / FielderSpeed(side.Players[ch]);
                    play.Fielder = ch;
                    play.Four = play.Six = false;
                    play.Runs = 0;
                    ThrowAndRun(play, side, ch, striker, nonStriker, aggression, rng);
                }
                return;
            }

            var fielder = side.Players[best];
            play.Chaser = best;
            play.ChaseFrom = side.Pos[best];
            play.ChaseTo = bestPos.WithY(0);
            play.ChaseStart = best == 10 ? 0.45 : 0.25;
            play.ChaseArrive = bestT;
            play.Dive = bestDive;
            play.Fielder = best;

            if (bestAir)
            {
                double p = best == 9 ? 0.9 : 0.85;
                p += (fielder.Field - 70) / 100.0 * 0.3;
                if (bestDive) p *= 0.5;
                else if (bestNeedFrac > 0.85) p *= 0.86;
                if (bestPos.y > 2.1) p *= 0.85;
                double vAt = bestT + Flight.Dt < f.Duration ? V3.Dist(f.At(bestT), f.At(bestT + Flight.Dt)) / Flight.Dt : 10;
                if (vAt > 28) p *= 0.8;
                else if (vAt > 22) p *= 0.9;
                if (best <= 8 && side.Layout != null && side.Layout.Names[best] == "Slip") p *= 0.92;
                bool nearRope = ground.Norm(bestPos) > 0.965;
                play.CatchChance = true;
                play.CatchProb = MathX.Clamp(p, 0.08, 0.97);
                play.CatchTime = bestT;
                // Work out the dropped branch so a later decision (user catch prompt) can use it.
                var drop = new Play
                {
                    Track = play.Track, Contact = play.Contact, Flight = f, NoBall = play.NoBall, BatterSpeed = play.BatterSpeed,
                    Chaser = best, ChaseFrom = play.ChaseFrom, ChaseTo = play.ChaseTo, ChaseStart = play.ChaseStart, ChaseArrive = bestT, Dive = bestDive,
                    Fielder = best, Dropped = true, CatchChance = true, CatchProb = play.CatchProb, CatchTime = bestT,
                };
                if (nearRope && rng.Chance(0.35))
                {
                    // Juggled over the rope.
                    drop.Six = true;
                    drop.Runs = 6;
                    drop.BoundaryOverRope = true;
                    drop.Duration = bestT + 2.4;
                }
                else
                {
                    drop.PickPos = bestPos.WithY(0);
                    drop.PickTime = bestT + (bestDive ? 1.4 : 0.9);
                    ThrowAndRun(drop, side, best, striker, nonStriker, aggression, rng);
                }
                play.DropBranch = drop;
                bool caught = rng.Chance(play.CatchProb);
                if (caught)
                {
                    play.Wicket = best == 9 ? WicketKind.CaughtBehind : best == 10 ? WicketKind.CaughtAndBowled : WicketKind.Caught;
                    play.Duration = bestT + 2.2;
                    play.DropBranch = drop;
                }
                else
                {
                    CopyInto(drop, play);
                }
                return;
            }

            // Ground fielding.
            double fumble = rng.Range(0.12, 0.35) + (bestDive ? 0.6 : 0);
            if (rng.Chance(0.03 * (1.4 - fielder.Field / 100.0))) fumble += 1.0;
            play.PickPos = bestPos.WithY(0);
            play.PickTime = bestT + fumble;
            ThrowAndRun(play, side, best, striker, nonStriker, aggression, rng);
        }

        /// <summary>Copies the outcome of a resolved branch into <paramref name="dst"/>, keeping the catch chance info.</summary>
        public static void CopyInto(Play src, Play dst)
        {
            dst.Runs = src.Runs;
            dst.Four = src.Four;
            dst.Six = src.Six;
            dst.Wicket = src.Wicket;
            dst.OutBatter = src.OutBatter;
            dst.Fielder = src.Fielder;
            dst.DirectHit = src.DirectHit;
            dst.Overthrow = src.Overthrow;
            dst.Dropped = src.Dropped;
            dst.PickPos = src.PickPos;
            dst.PickTime = src.PickTime;
            dst.Thrown = src.Thrown;
            dst.ThrowEnd = src.ThrowEnd;
            dst.ThrowStart = src.ThrowStart;
            dst.ThrowArrive = src.ThrowArrive;
            dst.RunTimes = src.RunTimes;
            dst.RunsAttempted = src.RunsAttempted;
            dst.Duration = src.Duration;
            dst.BoundaryOverRope = src.BoundaryOverRope;
        }

        static void ThrowAndRun(Play play, Side side, int fi, Player striker, Player nonStriker, double aggression, Rng rng)
        {
            var fielder = side.Players[fi];
            double vs = RunSpeed(striker), vn = RunSpeed(nonStriker);
            double v = Math.Min(vs, vn);
            bool defended = play.Contact.Type == ContactType.Defended;
            // Crease to crease, less the bat's reach: about 16 m a run.
            double start = defended ? 0.6 : 0.45;
            double runT = 16.0 / v;
            double turn = 0.55;
            var keeperEnd = new V3(0, 0.8, Pitch.StrikerStumpsZ);
            var bowlerEnd = new V3(0, 0.8, Pitch.StumpsZ);
            double release = fi == 9 || fi == 10 ? 0.3 : 0.5;

            double Arrive(int end)
            {
                var target = end == 0 ? keeperEnd : bowlerEnd;
                double d = V3.DistXZ(play.PickPos, target);
                if (d < 2.5) return play.PickTime + 0.15;
                return play.PickTime + release + d / ThrowSpeed(fielder, d) + 0.25;
            }

            // Arrival time of the batter running to a given end on run j (1-based).
            double BatterAt(int end, int j)
            {
                double t = start + j * runT + (j - 1) * turn;
                bool strikerToBowler = j % 2 == 1;
                bool strikerRunsThisEnd = (end == 1) == strikerToBowler;
                // The non-striker backs up, so arrives a touch earlier.
                return strikerRunsThisEnd ? t : t - 0.3;
            }

            int completed = 0;
            bool runOut = false;
            int outEnd = 0;
            var times = new List<double>();
            double margin = MathX.Lerp(0.55, 0.12, MathX.Clamp01(aggression));
            for (int j = 1; j <= 4; j++)
            {
                // Batters judge the run with some error.
                double est0 = Arrive(0) + rng.Gauss() * 0.28, est1 = Arrive(1) + rng.Gauss() * 0.28;
                double slack0 = est0 - BatterAt(0, j), slack1 = est1 - BatterAt(1, j);
                double slack = Math.Min(slack0, slack1);
                if (j == 1 && defended && slack < 1.2) break;
                if (slack < margin) break;
                // Committed: does the real throw beat them?
                double real0 = Arrive(0) - BatterAt(0, j), real1 = Arrive(1) - BatterAt(1, j);
                int danger = real0 < real1 ? 0 : 1;
                double realSlack = Math.Min(real0, real1);
                if (realSlack < 0 || (realSlack < 0.12 && rng.Chance(0.5)))
                {
                    runOut = true;
                    outEnd = danger;
                    times.Add(BatterAt(danger, j) + 0.2);
                    break;
                }
                completed = j;
                times.Add(Math.Max(BatterAt(0, j), BatterAt(1, j)));
                if (j == 3 && rng.Chance(0.85)) break;
            }

            play.Runs = completed;
            play.RunsAttempted = completed + (runOut ? 1 : 0);
            play.RunTimes = times.ToArray();
            play.Thrown = true;
            if (runOut)
            {
                play.ThrowEnd = outEnd;
                play.Wicket = WicketKind.RunOut;
                bool strikerToBowler = play.RunsAttempted % 2 == 1;
                bool strikerOut = (outEnd == 1) == strikerToBowler;
                play.OutBatter = strikerOut ? 0 : 1;
                play.DirectHit = V3.DistXZ(play.PickPos, outEnd == 0 ? keeperEnd : bowlerEnd) > 6 && rng.Chance(0.35);
                play.Fielder = fi;
            }
            else
            {
                // Throw to whichever end the last run finished at; keeper's end by default.
                play.ThrowEnd = completed == 0 ? (V3.DistXZ(play.PickPos, bowlerEnd) < V3.DistXZ(play.PickPos, keeperEnd) * 0.6 ? 1 : 0)
                    : (completed % 2 == 1 ? 0 : 1);
                if (completed > 0 && rng.Chance(0.025))
                {
                    play.Overthrow = true;
                    play.Runs += 1;
                }
            }
            play.ThrowStart = play.PickTime + release;
            play.ThrowArrive = Arrive(play.ThrowEnd);
            double last = times.Count > 0 ? times[times.Count - 1] : 0;
            play.Duration = Math.Max(play.ThrowArrive, last) + (runOut ? 1.6 : 0.9);
        }

        static int NearestFielder(Side side, V3 p)
        {
            int best = 0;
            double bd = double.MaxValue;
            for (int i = 0; i < 9; i++)
            {
                double d = V3.DistXZ(side.Pos[i], p);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            return best;
        }

        static V3 Towards(V3 from, V3 to, double maxDist)
        {
            var d = (to - from).XZ;
            double l = d.Length;
            if (l <= maxDist) return to.WithY(0);
            return (from + d / l * maxDist).WithY(0);
        }
    }
}
