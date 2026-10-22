using System;
using System.Collections.Generic;
using System.Linq;

namespace PCC.Core
{
    public enum ExtraType { None, Wide, NoBall, LegBye, Bye }

    public enum Dismissal { Bowled, Caught, CaughtBehind, Lbw, RunOut, Stumped }

    public static class DismissalX
    {
        static readonly string[] Labels = { "BOWLED", "CAUGHT", "CAUGHT BEHIND", "LBW", "RUN OUT", "STUMPED" };
        public static string Label(this Dismissal d) => Labels[(int)d];
    }

    public sealed class BallResult
    {
        /// <summary>Runs credited to the batter.</summary>
        public int Runs;
        public ExtraType Extra;

        /// <summary>Extras conceded on this ball (wide = 1 + any runs, no-ball = 1, byes...).</summary>
        public int ExtraRuns;
        public bool Wicket;
        public Dismissal? Dismissal;
        public int? FielderId;
        public int? OutBatterId;

        /// <summary>Field angle of the shot in degrees (batter-relative, + = off side).</summary>
        public double? Angle;
        public bool Lofted;
        public TimingGrade? Timing;
        public bool Edge;
        public bool Beaten;
        public bool Defended;

        public bool Legal => Extra != ExtraType.Wide && Extra != ExtraType.NoBall;
        public int Total => Runs + ExtraRuns;
        public bool IsFour => Runs == 4;
        public bool IsSix => Runs == 6;

        public string Chip
        {
            get
            {
                if (Wicket) return "W";
                switch (Extra)
                {
                    case ExtraType.Wide: return ExtraRuns > 1 ? ExtraRuns + "wd" : "wd";
                    case ExtraType.NoBall: return Runs > 0 ? Runs + "nb" : "nb";
                    case ExtraType.LegBye: return ExtraRuns + "lb";
                    case ExtraType.Bye: return ExtraRuns + "b";
                    default: return Runs == 0 ? "•" : Runs.ToString();
                }
            }
        }
    }

    /// <summary>Probability table for one ball: wicket, then runs 0,1,2,3,4,6.</summary>
    public sealed class OutcomeDist
    {
        public double W, D0, D1, D2, D3, D4, D6;

        public OutcomeDist(double w, double d0, double d1, double d2, double d3, double d4, double d6)
        {
            W = w;
            D0 = d0;
            D1 = d1;
            D2 = d2;
            D3 = d3;
            D4 = d4;
            D6 = d6;
        }

        public OutcomeDist Normalize()
        {
            double s = W + D0 + D1 + D2 + D3 + D4 + D6;
            if (s <= 0)
            {
                D0 = 1;
                return this;
            }
            W /= s;
            D0 /= s;
            D1 /= s;
            D2 /= s;
            D3 /= s;
            D4 /= s;
            D6 /= s;
            return this;
        }

        /// <summary>Batter-vs-bowler skill: positive favours the batter.</summary>
        public void ScaleSkill(double skill)
        {
            W *= Math.Exp(-0.35 * skill);
            D4 *= Math.Exp(0.22 * skill);
            D6 *= Math.Exp(0.28 * skill);
            Normalize();
        }

        /// <summary>-1 for a wicket, otherwise runs scored.</summary>
        public int Sample(Rng rng)
        {
            double x = rng.Next() * (W + D0 + D1 + D2 + D3 + D4 + D6);
            if ((x -= W) < 0) return -1;
            if ((x -= D0) < 0) return 0;
            if ((x -= D1) < 0) return 1;
            if ((x -= D2) < 0) return 2;
            if ((x -= D3) < 0) return 3;
            if ((x -= D4) < 0) return 4;
            return 6;
        }
    }

    public sealed class BatCard
    {
        public BatCard(int playerId, int position)
        {
            PlayerId = playerId;
            Position = position;
        }

        public int PlayerId { get; }
        public int Position { get; }
        public int Runs, Balls, Fours, Sixes;
        public bool Out;
        public Dismissal? How;
        public int? BowlerId;
        public int? FielderId;
        public double StrikeRate => Balls == 0 ? 0 : Runs * 100.0 / Balls;
    }

    public sealed class BowlCard
    {
        public BowlCard(int playerId) { PlayerId = playerId; }

        public int PlayerId { get; }
        public int Balls, Runs, Wickets, Maidens, Dots, Wides, NoBalls, Fours, Sixes;
        public double Economy => Balls == 0 ? 0 : Runs * 6.0 / Balls;
        public string Figures => Overs.Text(Balls) + "-" + Maidens + "-" + Runs + "-" + Wickets;
    }

    public readonly struct Fow
    {
        public Fow(int score, int wicket, int balls, int playerId)
        {
            Score = score;
            Wicket = wicket;
            Balls = balls;
            PlayerId = playerId;
        }

        public int Score { get; }
        public int Wicket { get; }
        public int Balls { get; }
        public int PlayerId { get; }
    }

    public sealed class BallLog
    {
        public int Over;
        public BallResult Result;
        public int BowlerId, BatterId, Score, Wickets;
    }

    public static class Overs
    {
        public static string Text(int balls) => (balls / 6) + (balls % 6 == 0 ? "" : "." + (balls % 6));
    }

    public sealed class Innings
    {
        public Innings(TeamSheet batting, TeamSheet bowling, int maxOvers, int? target = null, int? maxWickets = null, bool superOver = false)
        {
            Batting = batting;
            Bowling = bowling;
            MaxOvers = maxOvers;
            Target = target;
            MaxWickets = maxWickets ?? Math.Min(10, batting.Order.Count - 1);
            SuperOver = superOver;
            Striker = batting.Order[0].Id;
            NonStriker = batting.Order[1].Id;
            Bat[Striker] = new BatCard(Striker, 1);
            Bat[NonStriker] = new BatCard(NonStriker, 2);
        }

        public TeamSheet Batting { get; }
        public TeamSheet Bowling { get; }
        public int MaxOvers { get; }
        public int? Target { get; }
        public int MaxWickets { get; }
        public bool SuperOver { get; }

        public int Runs, Wickets, LegalBalls, Wides, NoBalls, LegByes, Byes;
        public readonly Dictionary<int, BatCard> Bat = new Dictionary<int, BatCard>();
        public readonly Dictionary<int, BowlCard> Bowl = new Dictionary<int, BowlCard>();
        public readonly List<int> BowlerOrder = new List<int>();
        public readonly List<BallLog> Log = new List<BallLog>();
        public readonly List<int> OverRuns = new List<int>();
        public readonly List<Fow> FallOfWickets = new List<Fow>();
        public int Striker, NonStriker, NextIndex = 2;
        public int? BowlerId, LastOverBowler;
        public bool FreeHit;
        public int OverAcc, OverBowlerAcc, PartnershipRuns, PartnershipBalls;

        public int MaxBalls => MaxOvers * 6;
        public int BallsLeft => MaxBalls - LegalBalls;
        public int Extras => Wides + NoBalls + LegByes + Byes;
        public bool AllOut => Wickets >= MaxWickets;
        public bool Complete => AllOut || LegalBalls >= MaxBalls || (Target != null && Runs >= Target.Value);
        public int OverIndex => LegalBalls / 6;
        public int BallInOver => LegalBalls % 6;
        public double RunRate => LegalBalls == 0 ? 0 : Runs * 6.0 / LegalBalls;
        public int? RunsNeeded => Target == null ? (int?)null : Math.Max(0, Target.Value - Runs);
        public double? RequiredRate => Target == null || BallsLeft <= 0 ? (double?)null : RunsNeeded.Value * 6.0 / BallsLeft;
        public string ScoreText => Runs + "/" + Wickets;
        public string OversText => Overs.Text(LegalBalls);
        public int MaxOversPerBowler => SuperOver ? 1 : Math.Max(1, (int)Math.Ceiling(MaxOvers / 5.0));

        /// <summary>0 = powerplay, 1 = middle overs, 2 = death.</summary>
        public int Phase
        {
            get
            {
                if (SuperOver) return 2;
                int ppEnd = Math.Max(1, (int)Math.Round(MaxOvers * 0.3));
                int deathStart = MaxOvers - Math.Max(1, (int)Math.Round(MaxOvers * 0.2));
                if (OverIndex < ppEnd) return 0;
                if (OverIndex >= deathStart) return 2;
                return 1;
            }
        }

        /// <summary>Balls of the over in progress (or the last completed over).</summary>
        public List<BallLog> RecentOver
        {
            get
            {
                if (Log.Count == 0) return new List<BallLog>();
                int over = (BallInOver == 0 && LegalBalls > 0) ? OverIndex - 1 : OverIndex;
                return Log.Where(b => b.Over == over).ToList();
            }
        }

        public int OversBowledBy(int id) => Bowl.TryGetValue(id, out var c) ? c.Balls / 6 : 0;
    }

    public sealed class MatchOutcome
    {
        public string WinnerId;
        public string Text;
        public int PotmId;
        public bool SuperOver;
        public int? MarginRuns, MarginWickets;
    }

    public sealed class MatchEngine
    {
        public MatchEngine(TeamSheet home, TeamSheet away, TeamInfo venue, int seed, int overs = League.T20Overs, Difficulty difficulty = Difficulty.Normal)
        {
            Home = home;
            Away = away;
            Venue = venue;
            Overs = overs;
            Difficulty = difficulty;
            Rng = new Rng(seed);
        }

        public TeamSheet Home { get; }
        public TeamSheet Away { get; }
        public TeamInfo Venue { get; }
        public int Overs { get; }
        public Difficulty Difficulty { get; }
        public Rng Rng { get; }

        public string TossWinnerId { get; private set; }
        public bool TossElectedBat { get; private set; } = true;
        TeamSheet _battingFirst, _bowlingFirst;
        public readonly List<Innings> InningsList = new List<Innings>();
        public MatchOutcome Outcome { get; private set; }
        int _superOvers;

        public bool Started => InningsList.Count > 0;
        public bool Finished => Outcome != null;
        public Innings Current => InningsList[InningsList.Count - 1];

        public TeamSheet SheetOf(string teamId) => Home.Team.Id == teamId ? Home : Away;

        // ------------------------------------------------------------- toss

        public void SetToss(string winnerId, bool electBat)
        {
            TossWinnerId = winnerId;
            TossElectedBat = electBat;
            var winner = SheetOf(winnerId);
            var loser = winner == Home ? Away : Home;
            _battingFirst = electBat ? winner : loser;
            _bowlingFirst = electBat ? loser : winner;
            InningsList.Clear();
            InningsList.Add(new Innings(_battingFirst, _bowlingFirst, Overs));
        }

        public bool AiElectBat() => Rng.Chance(Venue.BatFactor > 1.05 ? 0.3 : 0.5);

        public void AutoToss() => SetToss((Rng.Chance(0.5) ? Home : Away).Team.Id, AiElectBat());

        public string TossText => SheetOf(TossWinnerId).Team.Name + " won the toss and chose to " + (TossElectedBat ? "bat" : "bowl");

        // ---------------------------------------------------------- bowling

        public bool NeedBowler => !Finished && Current.BowlerId == null;

        public List<Player> EligibleBowlers()
        {
            var inn = Current;
            return inn.Bowling.Order.Where(p =>
                p.Id != inn.Bowling.KeeperId &&
                inn.OversBowledBy(p.Id) < inn.MaxOversPerBowler &&
                p.Id != inn.LastOverBowler).ToList();
        }

        public Player SuggestBowler()
        {
            var inn = Current;
            int phase = inn.Phase;
            int oversLeft = inn.MaxOvers - inn.OverIndex;
            Player best = null;
            double bestScore = -1e9;
            foreach (var p in EligibleBowlers())
            {
                int left = inn.MaxOversPerBowler - inn.OversBowledBy(p.Id);
                double s = p.BowlStyle == BowlStyle.None ? -60 : p.Bowl;
                if (p.Bowl < 50) s -= 25;
                if (phase == 0) s += p.IsPace ? 5 : -3;
                if (phase == 1) s += p.IsSpin ? 7 : -1;
                if (phase == 2) s += p.IsPace ? 4 + (p.Bowl - 75) * 0.4 : -6;
                if (phase == 1 && p.IsPace && p.Bowl >= 80 && left <= 2 && oversLeft > 4) s -= 9;
                s += left * 1.5;
                s += Rng.Range(-2.5, 2.5);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = p;
                }
            }
            return best ?? inn.Bowling.Order.First(p => p.Id != inn.LastOverBowler && p.Id != inn.Bowling.KeeperId);
        }

        public void SetBowler(int id)
        {
            var inn = Current;
            inn.BowlerId = id;
            if (!inn.Bowl.ContainsKey(id))
            {
                inn.Bowl[id] = new BowlCard(id);
                inn.BowlerOrder.Add(id);
            }
        }

        public void AutoBowlerIfNeeded()
        {
            if (NeedBowler) SetBowler(SuggestBowler().Id);
        }

        public Player Striker => Current.Batting.ById(Current.Striker);
        public Player NonStriker => Current.Batting.ById(Current.NonStriker);
        public Player Bowler => Current.Bowling.ById(Current.BowlerId.Value);

        // ------------------------------------------------------ outcome model

        OutcomeDist BaseDist(Player batter, Player bowler, Innings inn, double skillShift = 0)
        {
            int bowlSkill = bowler.BowlStyle == BowlStyle.None ? 25 : bowler.Bowl;
            double skill = (batter.Bat - bowlSkill) / 22.0 + skillShift;
            var card = inn.Bat[batter.Id];
            if (card.Balls > 12) skill += 0.12;
            if (card.Balls > 25) skill += 0.08;
            if (card.Balls < 4) skill -= 0.12;
            if (bowler.IsSpin) skill -= (Venue.SpinFactor - 1) * 1.2;

            double agg = 0.22 + batter.Power / 100.0 * 0.55;
            int phase = inn.Phase;
            if (phase == 0) agg += 0.06;
            if (phase == 2) agg += 0.2;
            if (inn.Target != null && inn.BallsLeft > 0)
            {
                double rrr = inn.RunsNeeded.Value * 6.0 / inn.BallsLeft;
                agg += Math.Clamp((rrr - 8.5) / 8, -0.06, 0.45);
            }
            int wkLeft = inn.MaxWickets - inn.Wickets;
            if (wkLeft <= 3 && inn.BallsLeft > 18) agg -= 0.12;
            agg = Math.Clamp(agg, 0.05, 1.05);
            double a = agg - 0.5;

            double w = 0.047 * Math.Exp(-0.36 * skill + 0.95 * a);
            double d6 = 0.044 * Math.Exp(0.4 * skill + 1.3 * a) * (0.55 + batter.Power / 110.0);
            double d4 = 0.133 * Math.Exp(0.3 * skill + 0.6 * a);
            const double d3 = 0.006;
            double d2 = 0.066 * Math.Exp(0.08 * skill);
            double d1 = 0.36 * Math.Exp(0.05 * skill - 0.3 * a);
            double d0 = 0.35 * Math.Exp(-0.28 * skill - 0.45 * a);
            switch (phase)
            {
                case 0:
                    d4 *= 1.14;
                    d1 *= 0.95;
                    break;
                case 1:
                    d4 *= 0.9;
                    d6 *= 0.93;
                    d1 *= 1.08;
                    w *= 0.95;
                    break;
                default:
                    d6 *= 1.15;
                    w *= 1.1;
                    break;
            }
            d4 *= Venue.BatFactor;
            d6 *= Venue.BatFactor * Venue.BatFactor;
            d0 /= Venue.BatFactor;
            return new OutcomeDist(w, d0, d1, d2, d3, d4, d6).Normalize();
        }

        Dismissal PickDismissal(Player bowler)
        {
            bool spin = bowler.IsSpin;
            return Rng.Weighted(
                new[] { Dismissal.Caught, Dismissal.CaughtBehind, Dismissal.Bowled, Dismissal.Lbw, Dismissal.RunOut, Dismissal.Stumped },
                new[] { 0.5, spin ? 0.07 : 0.14, 0.17, spin ? 0.15 : 0.11, 0.05, spin ? 0.06 : 0.0 });
        }

        int RandomFielder(bool allowBowler = true)
        {
            var inn = Current;
            var opts = inn.Bowling.Order.Where(p => p.Id != inn.Bowling.KeeperId && (allowBowler || p.Id != inn.BowlerId)).ToList();
            return Rng.Pick(opts).Id;
        }

        double AngleForRuns(int runs)
        {
            switch (runs)
            {
                case 6: return Rng.Range(-115, 45);
                case 4: return Rng.Chance(0.55) ? Rng.Range(20, 165) : Rng.Range(-165, -20);
                case 0: return Rng.Range(-60, 60);
                default: return Rng.Range(-135, 135);
            }
        }

        double AngleForDismissal(Dismissal d)
        {
            switch (d)
            {
                case Dismissal.CaughtBehind:
                case Dismissal.Stumped: return Rng.Range(165, 180);
                case Dismissal.Caught: return Rng.Range(-120, 70);
                default: return Rng.Range(-30, 30);
            }
        }

        BallResult WicketResult(Dismissal d, bool noBall = false, double? angle = null, bool lofted = false, TimingGrade? timing = null, bool edge = false)
        {
            var inn = Current;
            int? fielder = null;
            int outId = inn.Striker;
            int runs = 0;
            switch (d)
            {
                case Dismissal.Caught:
                    fielder = Rng.Chance(0.06) ? inn.BowlerId : RandomFielder(false);
                    break;
                case Dismissal.CaughtBehind:
                case Dismissal.Stumped:
                    fielder = inn.Bowling.KeeperId;
                    break;
                case Dismissal.RunOut:
                    fielder = RandomFielder();
                    if (Rng.Chance(0.35)) outId = inn.NonStriker;
                    runs = Rng.Chance(0.4) ? 1 : 0;
                    break;
            }
            return new BallResult
            {
                Runs = runs,
                Wicket = true,
                Dismissal = d,
                FielderId = fielder,
                OutBatterId = outId,
                Extra = noBall ? ExtraType.NoBall : ExtraType.None,
                ExtraRuns = noBall ? 1 : 0,
                Angle = angle ?? AngleForDismissal(d),
                Lofted = lofted || d == Dismissal.Caught,
                Timing = timing,
                Edge = edge || d == Dismissal.CaughtBehind,
            };
        }

        /// <summary>Fully simulated delivery (AI batting vs AI bowling).</summary>
        public BallResult SimulateBall()
        {
            var inn = Current;
            var bwl = Bowler;
            bool pace = bwl.IsPace;
            double wideP = Math.Clamp(0.022 + (pace ? 0.012 : 0.004) + (inn.Phase == 2 ? 0.008 : 0) - (bwl.Bowl - 75) * 0.0005, 0.008, 0.06);
            if (Rng.Chance(wideP))
            {
                int extra = Rng.Chance(0.04) ? 5 : (Rng.Chance(0.05) ? 2 : 1);
                return new BallResult { Extra = ExtraType.Wide, ExtraRuns = extra, Beaten = true };
            }
            bool noBall = Rng.Chance(pace ? 0.005 : 0.0015);
            return FinishSample(BaseDist(Striker, bwl, inn), bwl, noBall);
        }

        BallResult FinishSample(OutcomeDist dist, Player bwl, bool noBall, DeliveryPlan plan = null)
        {
            var inn = Current;
            int code = dist.Sample(Rng);
            if (code == -1 && (inn.FreeHit || noBall))
            {
                if (Rng.Chance(0.06)) return WicketResult(Dismissal.RunOut, noBall);
                code = 0;
            }
            if (code == -1)
            {
                var d = PickDismissal(bwl);
                if (plan != null)
                {
                    bool stumps = plan.HitsStumps(Striker.LeftBat);
                    if ((d == Dismissal.Bowled || d == Dismissal.Lbw) && !stumps) d = Dismissal.CaughtBehind;
                    if (plan.Type == DeliveryType.Bouncer && d != Dismissal.RunOut) d = Dismissal.Caught;
                }
                return WicketResult(d);
            }
            if (!noBall && code == 0)
            {
                if (Rng.Chance(0.03)) return new BallResult { Extra = ExtraType.LegBye, ExtraRuns = 1 };
                if (Rng.Chance(0.004)) return new BallResult { Extra = ExtraType.Bye, ExtraRuns = Rng.Chance(0.3) ? 4 : 1 };
            }
            return new BallResult
            {
                Runs = code,
                Extra = noBall ? ExtraType.NoBall : ExtraType.None,
                ExtraRuns = noBall ? 1 : 0,
                Angle = AngleForRuns(code),
                Lofted = code == 6 || (code == 4 && Rng.Chance(0.2)),
                Defended = code == 0 && Rng.Chance(0.6),
                Beaten = code == 0 && Rng.Chance(0.25),
            };
        }

        // ------------------------------------------- user batting resolution

        static readonly double[][] Suit =
        {
            // straight, cover, point, thirdMan, midWicket, squareLeg, fineLeg
            new[] { 0.35, 0.05, -0.5, 0.1, 0.05, -0.45, 0.25 }, // yorker
            new[] { 1.0, 0.9, 0.25, -0.2, 0.65, 0.0, -0.1 }, // full
            new[] { 0.45, 0.45, 0.35, 0.35, 0.25, 0.05, 0.2 }, // good length
            new[] { 0.0, 0.1, 0.7, 0.55, 0.6, 0.75, 0.5 }, // short
            new[] { -0.9, -0.7, 0.15, 0.5, 0.25, 0.7, 0.85 }, // bouncer
            new[] { 1.0, 1.0, 0.65, 0.1, 1.0, 0.7, 0.3 }, // full toss
        };

        static readonly double[][] LineAdj =
        {
            new[] { -0.25, 0.15, 0.25, 0.2, -0.55, -0.55, -0.5 }, // wide off
            new[] { 0.0, 0.1, 0.1, 0.05, -0.15, -0.25, -0.2 }, // off
            new[] { 0.15, -0.05, -0.2, -0.15, 0.15, 0.05, 0.0 }, // middle
            new[] { -0.1, -0.4, -0.5, -0.4, 0.25, 0.3, 0.35 }, // leg
        };

        static BallLine EffectiveLine(DeliveryPlan d, Player batter)
        {
            double dir = batter.LeftBat ? -1 : 1;
            double lat = d.Line.LegwardOffset() + (d.Swing + d.Turn) * dir;
            if (lat < -0.45) return BallLine.WideOff;
            if (lat < -0.06) return BallLine.Off;
            if (lat < 0.07) return BallLine.Middle;
            return BallLine.Leg;
        }

        public double ShotSuitability(DeliveryPlan d, ShotDir dir, Player batter)
        {
            var line = EffectiveLine(d, batter);
            double s = Suit[d.Type.LengthRow()][(int)dir] + LineAdj[(int)line][(int)dir];
            if (d.Spin && (dir == ShotDir.FineLeg || dir == ShotDir.SquareLeg) && (d.Type == DeliveryType.Stock || d.Type == DeliveryType.Flighted)) s += 0.35;
            return Math.Clamp(s, -1.2, 1.3);
        }

        static OutcomeDist GroundDist(TimingGrade g, double q)
        {
            switch (g)
            {
                case TimingGrade.Perfect:
                    if (q >= 0.6) return new OutcomeDist(0.01, 0.05, 0.17, 0.14, 0.03, 0.58, 0.02);
                    if (q >= 0.25) return new OutcomeDist(0.02, 0.12, 0.33, 0.15, 0.02, 0.35, 0.01);
                    if (q >= -0.15) return new OutcomeDist(0.04, 0.27, 0.42, 0.1, 0.01, 0.16, 0);
                    return new OutcomeDist(0.12, 0.45, 0.3, 0.05, 0, 0.08, 0);
                case TimingGrade.Good:
                    if (q >= 0.6) return new OutcomeDist(0.02, 0.1, 0.3, 0.16, 0.02, 0.4, 0);
                    if (q >= 0.25) return new OutcomeDist(0.03, 0.2, 0.42, 0.13, 0.01, 0.21, 0);
                    if (q >= -0.15) return new OutcomeDist(0.06, 0.36, 0.4, 0.07, 0, 0.11, 0);
                    return new OutcomeDist(0.15, 0.5, 0.26, 0.03, 0, 0.06, 0);
                case TimingGrade.Early:
                case TimingGrade.Late:
                    return q < 0 ? new OutcomeDist(0.16, 0.42, 0.3, 0.04, 0, 0.08, 0) : new OutcomeDist(0.1, 0.42, 0.33, 0.05, 0, 0.1, 0);
                case TimingGrade.VeryEarly:
                case TimingGrade.VeryLate:
                    return new OutcomeDist(0.24, 0.56, 0.12, 0, 0, 0.08, 0);
                default:
                    return new OutcomeDist(0.05, 0.95, 0, 0, 0, 0, 0);
            }
        }

        static OutcomeDist LoftDist(TimingGrade g, double q)
        {
            switch (g)
            {
                case TimingGrade.Perfect:
                    if (q >= 0.5) return new OutcomeDist(0.04, 0, 0.05, 0.04, 0, 0.14, 0.73);
                    if (q >= 0.15) return new OutcomeDist(0.1, 0, 0.1, 0.06, 0, 0.2, 0.54);
                    if (q >= -0.2) return new OutcomeDist(0.2, 0.05, 0.15, 0.05, 0, 0.2, 0.35);
                    return new OutcomeDist(0.32, 0.15, 0.2, 0, 0, 0.15, 0.18);
                case TimingGrade.Good:
                    if (q >= 0.5) return new OutcomeDist(0.1, 0, 0.1, 0.08, 0, 0.25, 0.47);
                    if (q >= 0.15) return new OutcomeDist(0.16, 0.05, 0.18, 0.08, 0, 0.23, 0.3);
                    return new OutcomeDist(0.3, 0.15, 0.22, 0.05, 0, 0.15, 0.13);
                case TimingGrade.Early:
                case TimingGrade.Late:
                    return new OutcomeDist(0.36, 0.2, 0.24, 0.05, 0, 0.1, 0.05);
                case TimingGrade.VeryEarly:
                case TimingGrade.VeryLate:
                    return new OutcomeDist(0.45, 0.4, 0.1, 0, 0, 0.05, 0);
                default:
                    return new OutcomeDist(0.05, 0.95, 0, 0, 0, 0, 0);
            }
        }

        /// <summary>Outcome of the user's shot against an AI delivery.</summary>
        public BallResult ResolveUserShot(DeliveryPlan d, ShotInput s)
        {
            var inn = Current;
            var batter = Striker;
            var bwl = Bowler;
            if (d.Wide) return new BallResult { Extra = ExtraType.Wide, ExtraRuns = 1, Beaten = true };
            TimingGrade? grade = s.TimingMs == null ? (TimingGrade?)null : Difficulty.Timing().Grade(s.TimingMs.Value);
            double skill = (batter.Bat - bwl.Bowl) / 22.0;
            bool stumps = d.HitsStumps(batter.LeftBat);
            bool free = inn.FreeHit || d.NoBall;

            OutcomeDist dist;
            bool beaten = false, defended = false, edge = false;
            if (s.Leave || grade == TimingGrade.Miss)
            {
                beaten = true;
                dist = stumps ? new OutcomeDist(0.68, 0.32, 0, 0, 0, 0, 0) : new OutcomeDist(0.03, 0.97, 0, 0, 0, 0, 0);
            }
            else if (s.Defend)
            {
                defended = true;
                switch (grade.Value)
                {
                    case TimingGrade.Perfect:
                    case TimingGrade.Good:
                        dist = new OutcomeDist(0, 0.86, 0.14, 0, 0, 0, 0);
                        break;
                    case TimingGrade.Early:
                    case TimingGrade.Late:
                        dist = new OutcomeDist(0.05, 0.8, 0.12, 0, 0, 0.03, 0);
                        edge = true;
                        break;
                    default:
                        dist = new OutcomeDist(stumps ? 0.2 : 0.08, 0.75, 0.05, 0, 0, 0, 0);
                        edge = true;
                        break;
                }
            }
            else
            {
                double q = ShotSuitability(d, s.Dir.Value, batter) + skill * 0.15;
                if (d.Type == DeliveryType.Variation && grade != TimingGrade.Perfect) q -= 0.35;
                if (d.Type == DeliveryType.Slower && (grade == TimingGrade.Early || grade == TimingGrade.VeryEarly)) q -= 0.15;
                dist = s.Loft ? LoftDist(grade.Value, q) : GroundDist(grade.Value, q);
                edge = grade != TimingGrade.Perfect && grade != TimingGrade.Good;
            }
            dist.ScaleSkill(skill);
            int code = dist.Sample(Rng);
            if (code == -1 && free) code = 0;

            double angle;
            if (s.Dir != null)
            {
                var g = grade.Value;
                double noise = g == TimingGrade.Perfect ? 8 : (g == TimingGrade.Good ? 15 : 26);
                double a = s.Dir.Value.Angle() + Rng.Gauss() * noise;
                if (g == TimingGrade.Early || g == TimingGrade.VeryEarly) a -= 18;
                if (g == TimingGrade.Late || g == TimingGrade.VeryLate) a += 18;
                if (edge && code != 6 && Rng.Chance(0.5)) a = Rng.Chance(0.5) ? Rng.Range(140, 170) : Rng.Range(-170, -140);
                angle = a;
            }
            else
            {
                angle = Rng.Range(-35, 35);
            }

            if (code == -1)
            {
                Dismissal dm;
                if (beaten && stumps) dm = Rng.Chance(0.55) ? Dismissal.Bowled : Dismissal.Lbw;
                else if (beaten) dm = (d.Spin && Rng.Chance(0.5)) ? Dismissal.Stumped : Dismissal.CaughtBehind;
                else if (defended) dm = stumps && Rng.Chance(0.5) ? Dismissal.Bowled : Dismissal.CaughtBehind;
                else if (s.Loft) dm = Rng.Chance(0.9) ? Dismissal.Caught : Dismissal.CaughtBehind;
                else if (edge) dm = stumps && Rng.Chance(0.25) ? Dismissal.Bowled : (Rng.Chance(0.6) ? Dismissal.CaughtBehind : Dismissal.Caught);
                else dm = Rng.Chance(0.92) ? Dismissal.Caught : Dismissal.RunOut;
                return WicketResult(dm, d.NoBall, angle, s.Loft, grade, edge);
            }
            return new BallResult
            {
                Runs = code,
                Extra = d.NoBall ? ExtraType.NoBall : ExtraType.None,
                ExtraRuns = d.NoBall ? 1 : 0,
                Angle = angle,
                Lofted = s.Loft || code == 6,
                Timing = grade,
                Edge = edge,
                Beaten = beaten,
                Defended = defended,
            };
        }

        // ------------------------------------------ user bowling resolution

        public double DeliveryQuality(DeliveryPlan d, IList<DeliveryType> recent)
        {
            int phase = Current.Phase;
            double q;
            switch (d.Type)
            {
                case DeliveryType.Yorker: q = 0.55 + (phase == 2 ? 0.2 : 0); break;
                case DeliveryType.WideYorker: q = 0.5 + (phase == 2 ? 0.15 : 0); break;
                case DeliveryType.Full: q = (phase == 0 && !d.Spin) ? 0.15 : -0.15; break;
                case DeliveryType.GoodLength: q = 0.35 + (phase == 0 ? 0.1 : 0); break;
                case DeliveryType.Short: q = phase == 1 ? 0.05 : 0.0; break;
                case DeliveryType.Bouncer: q = 0.25; break;
                case DeliveryType.Slower: q = 0.4 + (phase == 2 ? 0.1 : 0); break;
                case DeliveryType.Flighted: q = 0.15; break;
                case DeliveryType.Stock: q = 0.3; break;
                case DeliveryType.Quicker: q = 0.3 + (phase == 2 ? 0.1 : 0); break;
                case DeliveryType.Variation: q = 0.5; break;
                default: q = -0.85; break;
            }
            int reps = recent.Take(3).Count(t => t == d.Type);
            q -= reps * 0.17;
            q = q * (0.35 + 0.65 * d.Accuracy) + (d.Accuracy - 0.6) * 0.45;
            if (!d.Spin && Math.Abs(d.Swing) > 0.15) q += 0.08;
            if (d.Spin) q += (Venue.SpinFactor - 1) * 0.8;
            return Math.Clamp(q, -1.0, 1.0);
        }

        /// <summary>Outcome of the user's delivery against the AI batter.</summary>
        public BallResult ResolveUserBowling(DeliveryPlan d, IList<DeliveryType> recent)
        {
            if (d.Wide) return new BallResult { Extra = ExtraType.Wide, ExtraRuns = Rng.Chance(0.05) ? 5 : 1, Beaten = true };
            var bwl = Bowler;
            double q = DeliveryQuality(d, recent);
            var dist = BaseDist(Striker, bwl, Current, -q * 1.15);
            switch (d.Type)
            {
                case DeliveryType.Yorker:
                case DeliveryType.WideYorker:
                    dist.D6 *= 0.35;
                    dist.D4 *= 0.7;
                    dist.W *= 1.1;
                    break;
                case DeliveryType.Bouncer:
                    dist.W *= 1.25;
                    dist.D6 *= 1.2;
                    dist.D0 *= 1.15;
                    dist.D1 *= 0.8;
                    break;
                case DeliveryType.FullToss:
                    dist.D4 *= 1.7;
                    dist.D6 *= 1.8;
                    dist.W *= 0.5;
                    break;
                case DeliveryType.Slower:
                    dist.W *= 1.15;
                    break;
                case DeliveryType.Flighted:
                    dist.D6 *= 1.3;
                    dist.W *= 1.2;
                    break;
            }
            dist.Normalize();
            return FinishSample(dist, bwl, d.NoBall, d);
        }

        // ------------------------------------------------------ apply a ball

        static void Swap(Innings inn)
        {
            int t = inn.Striker;
            inn.Striker = inn.NonStriker;
            inn.NonStriker = t;
        }

        public void Apply(BallResult r)
        {
            if (Finished) return;
            var inn = Current;
            int bowlerId = inn.BowlerId.Value;
            if (!inn.Bowl.TryGetValue(bowlerId, out var bc))
            {
                bc = new BowlCard(bowlerId);
                inn.Bowl[bowlerId] = bc;
                inn.BowlerOrder.Add(bowlerId);
            }
            var sc = inn.Bat[inn.Striker];
            int overBefore = inn.OverIndex;
            bool wasFreeHit = inn.FreeHit;
            int batterId = inn.Striker;

            switch (r.Extra)
            {
                case ExtraType.Wide:
                {
                    inn.Wides += r.ExtraRuns;
                    inn.Runs += r.ExtraRuns;
                    bc.Runs += r.ExtraRuns;
                    bc.Wides++;
                    inn.OverAcc += r.ExtraRuns;
                    inn.OverBowlerAcc += r.ExtraRuns;
                    int ran = r.ExtraRuns == 5 ? 0 : r.ExtraRuns - 1;
                    if (ran % 2 == 1) Swap(inn);
                    break;
                }
                case ExtraType.NoBall:
                    inn.NoBalls += 1;
                    inn.Runs += 1 + r.Runs;
                    bc.Runs += 1 + r.Runs;
                    bc.NoBalls++;
                    sc.Runs += r.Runs;
                    sc.Balls += 1;
                    if (r.Runs == 4) { sc.Fours++; bc.Fours++; }
                    if (r.Runs == 6) { sc.Sixes++; bc.Sixes++; }
                    inn.OverAcc += 1 + r.Runs;
                    inn.OverBowlerAcc += 1 + r.Runs;
                    if (r.Runs % 2 == 1 && !r.Wicket) Swap(inn);
                    break;
                case ExtraType.LegBye:
                case ExtraType.Bye:
                    if (r.Extra == ExtraType.LegBye) inn.LegByes += r.ExtraRuns;
                    else inn.Byes += r.ExtraRuns;
                    inn.Runs += r.ExtraRuns;
                    sc.Balls += 1;
                    inn.OverAcc += r.ExtraRuns;
                    if (r.ExtraRuns % 2 == 1) Swap(inn);
                    break;
                default:
                    inn.Runs += r.Runs;
                    sc.Runs += r.Runs;
                    sc.Balls += 1;
                    bc.Runs += r.Runs;
                    if (r.Runs == 4) { sc.Fours++; bc.Fours++; }
                    if (r.Runs == 6) { sc.Sixes++; bc.Sixes++; }
                    inn.OverAcc += r.Runs;
                    inn.OverBowlerAcc += r.Runs;
                    if (r.Runs % 2 == 1 && !r.Wicket) Swap(inn);
                    break;
            }
            inn.PartnershipRuns += r.Total;
            if (r.Legal) inn.PartnershipBalls++;

            if (r.Wicket)
            {
                int outId = r.OutBatterId ?? inn.Striker;
                var oc = inn.Bat[outId];
                oc.Out = true;
                oc.How = r.Dismissal;
                oc.BowlerId = r.Dismissal == Dismissal.RunOut ? (int?)null : bowlerId;
                oc.FielderId = r.FielderId;
                inn.Wickets++;
                if (r.Dismissal != Dismissal.RunOut) bc.Wickets++;
                inn.FallOfWickets.Add(new Fow(inn.Runs, inn.Wickets, inn.LegalBalls + (r.Legal ? 1 : 0), outId));
                inn.PartnershipRuns = 0;
                inn.PartnershipBalls = 0;
                if (!inn.AllOut && inn.NextIndex < inn.Batting.Order.Count)
                {
                    var nb = inn.Batting.Order[inn.NextIndex++];
                    inn.Bat[nb.Id] = new BatCard(nb.Id, inn.NextIndex);
                    if (outId == inn.Striker) inn.Striker = nb.Id;
                    else inn.NonStriker = nb.Id;
                    bool caught = r.Dismissal == Dismissal.Caught || r.Dismissal == Dismissal.CaughtBehind;
                    if (caught && inn.Striker != nb.Id) Swap(inn);
                }
            }

            if (r.Legal)
            {
                inn.LegalBalls++;
                bc.Balls++;
                if ((r.Extra == ExtraType.None && r.Runs == 0) || r.Extra == ExtraType.LegBye || r.Extra == ExtraType.Bye) bc.Dots++;
                inn.FreeHit = false;
            }
            if (r.Extra == ExtraType.NoBall) inn.FreeHit = true;
            else if (r.Extra == ExtraType.Wide && wasFreeHit) inn.FreeHit = true;

            inn.Log.Add(new BallLog { Over = overBefore, Result = r, BowlerId = bowlerId, BatterId = batterId, Score = inn.Runs, Wickets = inn.Wickets });

            if (r.Legal && inn.LegalBalls % 6 == 0)
            {
                inn.OverRuns.Add(inn.OverAcc);
                if (inn.OverBowlerAcc == 0) bc.Maidens++;
                inn.OverAcc = 0;
                inn.OverBowlerAcc = 0;
                inn.LastOverBowler = bowlerId;
                inn.BowlerId = null;
                if (!inn.Complete) Swap(inn);
            }

            if (inn.Complete) EndInnings();
        }

        // ------------------------------------------------------ innings flow

        TeamSheet SuperOverSheet(TeamSheet s) =>
            s.WithOrder(s.Order.OrderByDescending(p => p.Bat * 0.8 + p.Power * 0.2).ToList());

        void EndInnings()
        {
            var inn = Current;
            if (inn.LegalBalls % 6 != 0 || inn.OverAcc > 0)
            {
                inn.OverRuns.Add(inn.OverAcc);
                inn.OverAcc = 0;
            }
            if (InningsList.Count == 1)
            {
                InningsList.Add(new Innings(_bowlingFirst, _battingFirst, Overs, inn.Runs + 1));
                return;
            }
            if (InningsList.Count == 2)
            {
                Innings i1 = InningsList[0], i2 = InningsList[1];
                if (i2.Runs >= i2.Target.Value)
                {
                    int wk = i2.MaxWickets - i2.Wickets;
                    Finish(i2.Batting, i2.Batting.Team.Name + " won by " + wk + " wicket" + (wk == 1 ? "" : "s"), wickets: wk);
                }
                else if (i2.Runs == i2.Target.Value - 1)
                {
                    StartSuperOver(i2);
                }
                else
                {
                    int rn = i1.Runs - i2.Runs;
                    Finish(i1.Batting, i1.Batting.Team.Name + " won by " + rn + " run" + (rn == 1 ? "" : "s"), runs: rn);
                }
                return;
            }
            int soIndex = InningsList.Count - 2;
            if (soIndex % 2 == 1)
            {
                InningsList.Add(new Innings(SuperOverSheet(inn.Bowling), inn.Batting, 1, inn.Runs + 1, 2, true));
            }
            else
            {
                Innings so1 = InningsList[InningsList.Count - 2], so2 = inn;
                if (so2.Runs >= so2.Target.Value)
                {
                    Finish(so2.Batting, so2.Batting.Team.Name + " won the Super Over", superOver: true);
                }
                else if (so2.Runs == so2.Target.Value - 1 && _superOvers < 4)
                {
                    StartSuperOver(so2);
                }
                else if (so2.Runs == so2.Target.Value - 1)
                {
                    int Boundaries(TeamSheet t) => InningsList.Where(i => i.Batting.Team.Id == t.Team.Id)
                        .Sum(i => i.Bat.Values.Sum(c => c.Fours + c.Sixes));
                    var w = Boundaries(so1.Batting) >= Boundaries(so2.Batting) ? so1.Batting : so2.Batting;
                    Finish(w, w.Team.Name + " won on boundary count", superOver: true);
                }
                else
                {
                    Finish(so1.Batting, so1.Batting.Team.Name + " won the Super Over", superOver: true);
                }
            }
        }

        void StartSuperOver(Innings last)
        {
            _superOvers++;
            InningsList.Add(new Innings(SuperOverSheet(last.Batting), last.Bowling, 1, null, 2, true));
        }

        void Finish(TeamSheet winner, string text, int? runs = null, int? wickets = null, bool superOver = false)
        {
            Outcome = new MatchOutcome
            {
                WinnerId = winner.Team.Id,
                Text = text,
                PotmId = Potm(winner.Team.Id),
                SuperOver = superOver,
                MarginRuns = runs,
                MarginWickets = wickets,
            };
        }

        int Potm(string winnerId)
        {
            var score = new Dictionary<int, double>();
            void Add(int id, double v) => score[id] = (score.TryGetValue(id, out var o) ? o : 0) + v;
            foreach (var inn in InningsList.Take(2))
            {
                foreach (var c in inn.Bat.Values)
                {
                    double s = c.Runs + c.Fours + c.Sixes * 2.0;
                    if (c.Balls >= 10) s += (c.StrikeRate - 135) * c.Balls / 120.0;
                    Add(c.PlayerId, s);
                    if (c.Out && c.FielderId != null) Add(c.FielderId.Value, c.How == Dismissal.RunOut ? 6 : 7);
                }
                foreach (var b in inn.Bowl.Values)
                {
                    double s = b.Wickets * 24.0 + b.Dots;
                    if (b.Balls > 0) s -= (b.Economy - 8.5) * (b.Balls / 6.0) * 2.5;
                    Add(b.PlayerId, s);
                }
            }
            var winners = new HashSet<int>(SheetOf(winnerId).Order.Select(p => p.Id));
            int bestId = SheetOf(winnerId).Order[0].Id;
            double best = -1e9;
            foreach (var kv in score)
            {
                double adj = winners.Contains(kv.Key) ? kv.Value * 1.2 + 10 : kv.Value;
                if (adj > best)
                {
                    best = adj;
                    bestId = kv.Key;
                }
            }
            return bestId;
        }

        // ------------------------------------------------- fast simulation

        public void SimulateBallAuto()
        {
            if (Finished) return;
            AutoBowlerIfNeeded();
            Apply(SimulateBall());
        }

        public void SimulateOver()
        {
            if (Finished) return;
            var inn = Current;
            int startOver = inn.OverIndex;
            while (!Finished && Current == inn && inn.OverIndex == startOver) SimulateBallAuto();
        }

        public void SimulateInnings()
        {
            if (Finished) return;
            var inn = Current;
            while (!Finished && Current == inn) SimulateBallAuto();
        }

        public void SimulateToEnd()
        {
            if (!Started) AutoToss();
            int guard = 0;
            while (!Finished && guard++ < 5000) SimulateBallAuto();
        }

        public Player PlayerById(int id) => Home.Has(id) ? Home.ById(id) : Away.ById(id);
    }
}
