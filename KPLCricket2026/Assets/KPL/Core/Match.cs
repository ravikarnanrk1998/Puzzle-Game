using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    public sealed class BatCard
    {
        public Player P;
        public int Runs, Balls, Fours, Sixes;
        public bool Batted, Out;
        public WicketKind? How;
        public Player Bowler, Fielder;
        public int Order;

        public double StrikeRate => Balls == 0 ? 0 : Runs * 100.0 / Balls;

        public string DismissalText
        {
            get
            {
                if (!Batted) return "";
                if (!Out) return "not out";
                switch (How)
                {
                    case WicketKind.Bowled: return "b " + Bowler.Last;
                    case WicketKind.Lbw: return "lbw b " + Bowler.Last;
                    case WicketKind.Caught: return "c " + Fielder?.Last + " b " + Bowler.Last;
                    case WicketKind.CaughtBehind: return "c †" + Fielder?.Last + " b " + Bowler.Last;
                    case WicketKind.CaughtAndBowled: return "c & b " + Bowler.Last;
                    case WicketKind.Stumped: return "st †" + Fielder?.Last + " b " + Bowler.Last;
                    case WicketKind.RunOut: return "run out (" + Fielder?.Last + ")";
                    default: return "out";
                }
            }
        }
    }

    public sealed class BowlCard
    {
        public Player P;
        public int Balls, Runs, Wkts, Maidens, Dots, Wides, NoBalls, Fours, Sixes;
        public int OverRuns, Streak;
        public bool HatTrick;
        public double Economy => Balls == 0 ? 0 : Runs * 6.0 / Balls;
        public string OversText => Overs.Text(Balls);
        public string Figures => Wkts + "/" + Runs;
    }

    public sealed class BallRecord
    {
        public int Over, BallInOver;
        public string Chip;
        public int Runs, Extras;
        public bool Four, Six, Wicket, Legal;
        public Player Bowler, Batter;
        public string Shot, Commentary;
        public int Score, Wkts;
        public double Distance;
    }

    public static class Overs
    {
        public static string Text(int balls) => (balls / 6) + (balls % 6 == 0 ? "" : "." + (balls % 6));
    }

    public sealed class Innings
    {
        public TeamSheet Bat, Bowl;
        public int MaxBalls, MaxWickets;
        public int? Target;
        public bool SuperOver;
        public int Runs, Wkts, Balls, Wides, NoBalls, Byes, LegByes;
        public readonly List<BatCard> BatCards = new List<BatCard>();
        public readonly List<BowlCard> BowlCards = new List<BowlCard>();
        public int Striker, NonStriker, NextIdx;
        public Player Bowler, LastOverBowler;
        public bool FreeHit;
        public readonly List<BallRecord> Log = new List<BallRecord>();
        public readonly List<(int Score, int Wkt, int Balls, string Name)> Fow = new List<(int, int, int, string)>();
        public readonly List<int> OverRuns = new List<int>();
        public int PartnershipRuns, PartnershipBalls, OverAcc;

        public Innings(TeamSheet bat, TeamSheet bowl, int maxBalls, int maxWickets, int? target, bool superOver = false)
        {
            Bat = bat;
            Bowl = bowl;
            MaxBalls = maxBalls;
            MaxWickets = Math.Min(maxWickets, bat.XI.Count - 1);
            Target = target;
            SuperOver = superOver;
            for (int i = 0; i < bat.XI.Count; i++) BatCards.Add(new BatCard { P = bat.XI[i], Order = i + 1 });
            Striker = 0;
            NonStriker = 1;
            NextIdx = 2;
            BatCards[0].Batted = BatCards[1].Batted = true;
        }

        public int Extras => Wides + NoBalls + Byes + LegByes;
        public bool AllOut => Wkts >= MaxWickets;
        public bool Chased => Target != null && Runs >= Target.Value;
        public bool Complete => AllOut || Balls >= MaxBalls || Chased;
        public int OverIndex => Balls / 6;
        public int BallInOver => Balls % 6;
        public int BallsLeft => Math.Max(0, MaxBalls - Balls);
        public int MaxOvers => (MaxBalls + 5) / 6;
        public int RunsNeeded => Target == null ? 0 : Math.Max(0, Target.Value - Runs);
        public double RunRate => Balls == 0 ? 0 : Runs * 6.0 / Balls;
        public double RequiredRate => Target == null || BallsLeft == 0 ? 0 : RunsNeeded * 6.0 / BallsLeft;
        public string Score => Runs + "/" + Wkts;
        public string OversText => Overs.Text(Balls);
        public int QuotaPerBowler => SuperOver ? 1 : Math.Max(1, (int)Math.Ceiling(MaxOvers / 5.0));

        public BatCard StrikerCard => BatCards[Striker];
        public BatCard NonStrikerCard => BatCards[NonStriker];

        public BowlCard CardFor(Player p)
        {
            foreach (var c in BowlCards)
            {
                if (c.P.Id == p.Id) return c;
            }
            var n = new BowlCard { P = p };
            BowlCards.Add(n);
            return n;
        }

        public int OversBy(Player p)
        {
            foreach (var c in BowlCards)
            {
                if (c.P.Id == p.Id) return (c.Balls + 5) / 6;
            }
            return 0;
        }

        /// <summary>0 = powerplay (first 30%), 1 = middle, 2 = death (last 20%).</summary>
        public int Phase
        {
            get
            {
                if (SuperOver) return 2;
                int ov = MaxOvers;
                int pp = Math.Max(1, (int)Math.Round(ov * 0.3));
                int death = ov - Math.Max(1, (int)Math.Round(ov * 0.2));
                if (OverIndex < pp) return 0;
                if (OverIndex >= death) return 2;
                return 1;
            }
        }

        public List<BallRecord> ThisOver
        {
            get
            {
                if (Log.Count == 0) return new List<BallRecord>();
                int over = (BallInOver == 0 && Balls > 0) ? OverIndex - 1 : OverIndex;
                return Log.Where(b => b.Over == over).ToList();
            }
        }
    }

    /// <summary>A "scenario" match is a single innings from a given situation (challenges).</summary>
    public sealed class Scenario
    {
        public string BattingTeamId;
        public int Balls, Wickets;
        public int? Target;
        /// <summary>"spin", "pace" or null: restricts which bowlers can be used.</summary>
        public string BowlerFilter;
    }

    public sealed class MatchSetup
    {
        public TeamSheet Home, Away;
        public int Overs = 5;
        public TeamDef Venue;
        public long Seed;
        public Difficulty Difficulty = Difficulty.Medium;
        public string UserTeamId;
        public bool Night;
        public Scenario Scenario;
        public string Title = "";
    }

    public sealed class MatchResult
    {
        public string WinnerId;
        public string Text;
        public int? MarginRuns, MarginWkts;
        public bool SuperOver, Tie;
        public int PotmId;
    }

    public sealed class MatchState
    {
        public readonly MatchSetup Setup;
        public readonly Rng Rng;
        public string TossWinnerId;
        public bool TossBat;
        public readonly List<Innings> InningsList = new List<Innings>();
        public MatchResult Result;
        TeamSheet _first, _second;
        int _superOvers;

        public MatchState(MatchSetup setup)
        {
            Setup = setup;
            Rng = new Rng(setup.Seed);
        }

        public bool Started => InningsList.Count > 0;
        public bool Finished => Result != null;
        public Innings Current => InningsList[InningsList.Count - 1];
        public TeamSheet Sheet(string id) => Setup.Home.Team.Id == id ? Setup.Home : Setup.Away;
        public TeamSheet Other(string id) => Setup.Home.Team.Id == id ? Setup.Away : Setup.Home;
        public bool IsScenario => Setup.Scenario != null;

        public Player PlayerById(int id) => Setup.Home.ById(id) ?? Setup.Away.ById(id);

        public void SetToss(string winnerId, bool bat)
        {
            TossWinnerId = winnerId;
            TossBat = bat;
            var w = Sheet(winnerId);
            var l = Other(winnerId);
            _first = bat ? w : l;
            _second = bat ? l : w;
            InningsList.Clear();
            InningsList.Add(new Innings(_first, _second, Setup.Overs * 6, 10, null));
        }

        public void StartScenario()
        {
            var sc = Setup.Scenario;
            _first = Sheet(sc.BattingTeamId);
            _second = Other(sc.BattingTeamId);
            TossWinnerId = _first.Team.Id;
            TossBat = true;
            InningsList.Clear();
            InningsList.Add(new Innings(_first, _second, sc.Balls, sc.Wickets, sc.Target));
        }

        public string TossText => Sheet(TossWinnerId).Team.Name + " won the toss and chose to " + (TossBat ? "bat" : "bowl");

        // ------------------------------------------------------------ bowlers

        public bool NeedBowler => !Finished && Current.Bowler == null;

        public List<Player> EligibleBowlers()
        {
            var inn = Current;
            bool NotLast(Player p) => inn.LastOverBowler == null || p.Id != inn.LastOverBowler.Id;
            string filter = Setup.Scenario?.BowlerFilter;
            if (filter != null)
            {
                // Challenges with a bowling theme: only that kind of bowler, quotas relaxed.
                var typed = inn.Bowl.XI.Where(p => p.Id != inn.Bowl.KeeperId && NotLast(p) && (filter == "spin" ? p.IsSpin : p.IsPace)).ToList();
                if (typed.Count > 0) return typed;
            }
            var list = inn.Bowl.XI.Where(p => p.Id != inn.Bowl.KeeperId && inn.OversBy(p) < inn.QuotaPerBowler && NotLast(p)).ToList();
            if (list.Count == 0) list = inn.Bowl.XI.Where(p => p.Id != inn.Bowl.KeeperId && (inn.LastOverBowler == null || p.Id != inn.LastOverBowler.Id)).ToList();
            return list;
        }

        public void SetBowler(Player p)
        {
            Current.Bowler = p;
            Current.CardFor(p);
        }

        // -------------------------------------------------------------- apply

        /// <summary>Records a delivery. Returns the ball record (also appended to the innings log).</summary>
        public BallRecord Apply(Play play, string commentary = null)
        {
            var inn = Current;
            var bowler = inn.Bowler;
            var bc = inn.CardFor(bowler);
            var sc = inn.StrikerCard;
            bool legal = !play.Wide && !play.NoBall;
            int overBefore = inn.OverIndex, ballBefore = inn.BallInOver;
            bool freeHit = inn.FreeHit;
            var rec = new BallRecord { Over = overBefore, BallInOver = ballBefore + 1, Bowler = bowler, Batter = sc.P, Shot = play.Contact?.ShotName ?? "" };
            if (play.Flight != null && (play.Six || play.Four)) rec.Distance = play.Six ? play.Flight.Carry : 0;

            int batRuns = play.Wide ? 0 : play.Runs;
            int byes = play.Byes, legByes = play.LegByes;
            if (play.Wide)
            {
                int w = 1 + byes + play.Runs;
                inn.Wides += w;
                inn.Runs += w;
                bc.Runs += w;
                bc.Wides++;
                inn.OverAcc += w;
                bc.OverRuns += w;
                rec.Extras = w;
            }
            else
            {
                if (play.NoBall)
                {
                    inn.NoBalls += 1;
                    inn.Runs += 1;
                    bc.Runs += 1;
                    bc.NoBalls++;
                    inn.OverAcc += 1;
                    bc.OverRuns += 1;
                    rec.Extras += 1;
                }
                inn.Runs += batRuns + byes + legByes;
                inn.Byes += byes;
                inn.LegByes += legByes;
                rec.Extras += byes + legByes;
                sc.Runs += batRuns;
                sc.Balls += 1;
                bc.Runs += batRuns;
                bc.OverRuns += batRuns;
                inn.OverAcc += batRuns + byes + legByes;
                if (play.Four) { sc.Fours++; bc.Fours++; }
                if (play.Six) { sc.Sixes++; bc.Sixes++; }
            }
            rec.Runs = batRuns;
            rec.Four = play.Four && !play.Wide;
            rec.Six = play.Six && !play.Wide;
            inn.PartnershipRuns += play.TotalRuns;
            if (legal) inn.PartnershipBalls++;

            // Strike rotation by runs actually run (boundaries don't change ends).
            int ran = play.Four || play.Six ? 0 : (play.Wide ? byes + play.Runs : batRuns + byes + legByes);
            if (play.Overthrow && ran == 0) ran = batRuns;
            if (ran % 2 == 1) Swap(inn);

            // Wickets (only run outs on no-balls and free hits).
            bool wicket = play.Wicket != null && ((!play.NoBall && !freeHit) || play.Wicket == WicketKind.RunOut);
            if (wicket)
            {
                var kind = play.Wicket.Value;
                // After a run, OutBatter refers to the batter by identity at delivery time.
                BatCard outCard;
                if (kind == WicketKind.RunOut)
                {
                    var strikerAtDelivery = sc;
                    outCard = play.OutBatter == 0 ? strikerAtDelivery : (inn.StrikerCard == strikerAtDelivery ? inn.NonStrikerCard : inn.StrikerCard);
                }
                else
                {
                    outCard = sc;
                }
                outCard.Out = true;
                outCard.How = kind;
                outCard.Bowler = kind.BowlerCredit() ? bowler : null;
                if (play.Fielder >= 0) outCard.Fielder = FielderPlayer(inn, play.Fielder);
                if (kind == WicketKind.CaughtAndBowled) outCard.Fielder = bowler;
                inn.Wkts++;
                if (kind.BowlerCredit())
                {
                    bc.Wkts++;
                    bc.Streak++;
                    if (bc.Streak >= 3) bc.HatTrick = true;
                }
                inn.Fow.Add((inn.Runs, inn.Wkts, inn.Balls + (legal ? 1 : 0), outCard.P.Short));
                inn.PartnershipRuns = 0;
                inn.PartnershipBalls = 0;
                rec.Wicket = true;
                if (!inn.AllOut && inn.NextIdx < inn.BatCards.Count)
                {
                    int idx = inn.NextIdx++;
                    inn.BatCards[idx].Batted = true;
                    int outIdx = inn.BatCards.IndexOf(outCard);
                    bool caught = kind == WicketKind.Caught || kind == WicketKind.CaughtBehind || kind == WicketKind.CaughtAndBowled;
                    if (inn.Striker == outIdx) inn.Striker = idx;
                    else inn.NonStriker = idx;
                    // New batter faces after a catch (2022 playing conditions).
                    if (caught && inn.Striker != idx) Swap(inn);
                }
            }
            if (legal && !(wicket && play.Wicket.Value.BowlerCredit())) bc.Streak = 0;

            if (legal)
            {
                inn.Balls++;
                bc.Balls++;
                if (batRuns == 0 && byes == 0 && legByes == 0) bc.Dots++;
                inn.FreeHit = false;
            }
            if (play.NoBall) inn.FreeHit = true;
            else if (play.Wide && freeHit) inn.FreeHit = true;

            rec.Legal = legal;
            rec.Score = inn.Runs;
            rec.Wkts = inn.Wkts;
            rec.Chip = Chip(play, wicket);
            rec.Commentary = commentary ?? "";
            inn.Log.Add(rec);

            if (legal && inn.Balls % 6 == 0)
            {
                inn.OverRuns.Add(inn.OverAcc);
                if (bc.OverRuns == 0 && bc.Balls % 6 == 0) bc.Maidens++;
                bc.OverRuns = 0;
                inn.OverAcc = 0;
                inn.LastOverBowler = bowler;
                inn.Bowler = null;
                if (!inn.Complete) Swap(inn);
            }
            if (inn.Complete) EndInnings();
            return rec;
        }

        static Player FielderPlayer(Innings inn, int idx)
        {
            var side = inn.Bowl.XI;
            if (idx == 9) return inn.Bowl.ById(inn.Bowl.KeeperId);
            if (idx == 10) return inn.Bowler;
            var fielders = side.Where(p => p.Id != inn.Bowl.KeeperId && (inn.Bowler == null || p.Id != inn.Bowler.Id)).ToList();
            return idx < fielders.Count ? fielders[idx] : fielders.LastOrDefault();
        }

        public static string Chip(Play p, bool wicket)
        {
            if (wicket) return "W";
            if (p.Wide) return p.TotalRuns > 1 ? p.TotalRuns + "wd" : "wd";
            if (p.NoBall) return p.Runs > 0 ? p.Runs + "nb" : "nb";
            if (p.LegByes > 0) return p.LegByes + "lb";
            if (p.Byes > 0) return p.Byes + "b";
            return p.Runs == 0 ? "•" : p.Runs.ToString();
        }

        static void Swap(Innings inn) => (inn.Striker, inn.NonStriker) = (inn.NonStriker, inn.Striker);

        // ------------------------------------------------------- innings flow

        void EndInnings()
        {
            var inn = Current;
            if (inn.BallInOver != 0 || inn.OverAcc > 0)
            {
                inn.OverRuns.Add(inn.OverAcc);
                inn.OverAcc = 0;
            }
            if (IsScenario)
            {
                var sc = Setup.Scenario;
                if (sc.Target != null)
                {
                    int margin = inn.RunsNeeded - 1;
                    if (inn.Chased) Finish(inn.Bat, inn.Bat.Team.Name + " got there with " + inn.BallsLeft + " ball" + (inn.BallsLeft == 1 ? "" : "s") + " to spare", wkts: inn.MaxWickets - inn.Wkts);
                    else if (margin == 0)
                    {
                        Finish(null, "Scores level - it's a tie");
                        Result.Tie = true;
                    }
                    else Finish(inn.Bowl, inn.Bowl.Team.Name + " won by " + margin + " run" + (margin == 1 ? "" : "s"), runs: margin);
                }
                else
                {
                    Finish(null, inn.Bat.Team.Name + " made " + inn.Score + " off " + inn.Balls + " balls");
                }
                return;
            }
            if (InningsList.Count == 1)
            {
                InningsList.Add(new Innings(_second, _first, Setup.Overs * 6, 10, inn.Runs + 1));
                return;
            }
            if (InningsList.Count == 2)
            {
                Innings i1 = InningsList[0], i2 = InningsList[1];
                if (i2.Chased)
                {
                    int wk = i2.MaxWickets - i2.Wkts;
                    Finish(i2.Bat, i2.Bat.Team.Name + " won by " + wk + " wicket" + (wk == 1 ? "" : "s") + " (" + i2.BallsLeft + " balls left)", wkts: wk);
                }
                else if (i2.Runs == i1.Runs)
                {
                    StartSuperOver(i2.Bat, i2.Bowl, null);
                }
                else
                {
                    int rn = i1.Runs - i2.Runs;
                    Finish(i1.Bat, i1.Bat.Team.Name + " won by " + rn + " run" + (rn == 1 ? "" : "s"), runs: rn);
                }
                return;
            }
            // Super over pair.
            int soIdx = InningsList.Count - 2;
            if (soIdx % 2 == 1)
            {
                InningsList.Add(new Innings(SuperOverSheet(inn.Bowl), SuperOverBowl(inn.Bat), 6, 2, inn.Runs + 1, true));
                return;
            }
            Innings so1 = InningsList[InningsList.Count - 2], so2 = inn;
            if (so2.Chased) Finish(so2.Bat, so2.Bat.Team.Name + " won the Super Over", superOver: true);
            else if (so2.Runs == so1.Runs && _superOvers < 3) StartSuperOver(so2.Bat, so2.Bowl, null);
            else if (so2.Runs == so1.Runs)
            {
                int B(TeamSheet t) => InningsList.Where(i => i.Bat.Team.Id == t.Team.Id).Sum(i => i.BatCards.Sum(c => c.Fours + c.Sixes));
                var w = B(so1.Bat) >= B(so2.Bat) ? so1.Bat : so2.Bat;
                Finish(w, w.Team.Name + " won on boundary count", superOver: true);
            }
            else Finish(so1.Bat, so1.Bat.Team.Name + " won the Super Over", superOver: true);
        }

        static TeamSheet SuperOverSheet(TeamSheet s)
        {
            var order = s.XI.OrderByDescending(p => p.Bat * 0.5 + p.Power * 0.5).ToList();
            return new TeamSheet { Team = s.Team, XI = order, KeeperId = s.KeeperId, CaptainId = s.CaptainId };
        }

        static TeamSheet SuperOverBowl(TeamSheet s) => s;

        void StartSuperOver(TeamSheet lastBat, TeamSheet lastBowl, object _)
        {
            _superOvers++;
            // The side that batted second bats first in the super over.
            InningsList.Add(new Innings(SuperOverSheet(lastBat), lastBowl, 6, 2, null, true));
        }

        public bool InSuperOver => Started && Current.SuperOver;

        void Finish(TeamSheet winner, string text, int? runs = null, int? wkts = null, bool superOver = false)
        {
            Result = new MatchResult
            {
                WinnerId = winner?.Team.Id,
                Text = text,
                MarginRuns = runs,
                MarginWkts = wkts,
                SuperOver = superOver,
                PotmId = Potm(winner?.Team.Id),
            };
        }

        int Potm(string winnerId)
        {
            var score = new Dictionary<int, double>();
            void Add(int id, double v) => score[id] = (score.TryGetValue(id, out var o) ? o : 0) + v;
            foreach (var inn in InningsList.Where(i => !i.SuperOver))
            {
                foreach (var c in inn.BatCards.Where(c => c.Batted))
                {
                    double s = c.Runs + c.Fours + c.Sixes * 2.0;
                    if (c.Balls >= 8) s += (c.StrikeRate - 135) * c.Balls / 120.0;
                    Add(c.P.Id, s);
                    if (c.Out && c.Fielder != null) Add(c.Fielder.Id, c.How == WicketKind.RunOut ? 6 : 7);
                }
                foreach (var b in inn.BowlCards)
                {
                    double s = b.Wkts * 24.0 + b.Dots;
                    if (b.Balls > 0) s -= (b.Economy - 8.5) * (b.Balls / 6.0) * 2.5;
                    Add(b.P.Id, s);
                }
            }
            int best = InningsList[0].BatCards[0].P.Id;
            double bv = double.MinValue;
            foreach (var kv in score)
            {
                double v = kv.Value;
                if (winnerId != null && Sheet(winnerId).Has(kv.Key)) v = v * 1.2 + 10;
                if (v > bv)
                {
                    bv = v;
                    best = kv.Key;
                }
            }
            return best;
        }

        /// <summary>Team totals for net run rate: (runs, balls) with all-out innings counted as the full quota.</summary>
        public (int RunsFor, int BallsFor, int RunsAgainst, int BallsAgainst) NrrFigures(string teamId)
        {
            int rf = 0, bf = 0, ra = 0, ba = 0;
            foreach (var inn in InningsList.Where(i => !i.SuperOver))
            {
                int balls = inn.AllOut ? inn.MaxBalls : inn.Balls;
                if (inn.Bat.Team.Id == teamId)
                {
                    rf += inn.Runs;
                    bf += balls;
                }
                else
                {
                    ra += inn.Runs;
                    ba += balls;
                }
            }
            return (rf, bf, ra, ba);
        }
    }
}
