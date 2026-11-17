using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    public enum TourFormat { Express, Full }

    public enum TourStage { League = 0, Qualifier1 = 1, Eliminator = 2, Qualifier2 = 3, Final = 4, Done = 5 }

    public static class TourStageX
    {
        public static string Label(this TourStage s)
        {
            switch (s)
            {
                case TourStage.Qualifier1: return "QUALIFIER 1";
                case TourStage.Eliminator: return "ELIMINATOR";
                case TourStage.Qualifier2: return "QUALIFIER 2";
                case TourStage.Final: return "FINAL";
                case TourStage.Done: return "SEASON COMPLETE";
                default: return "LEAGUE STAGE";
            }
        }
    }

    [Serializable]
    public sealed class Fixture
    {
        public int No;
        public string Home, Away;
        public int Stage;
        public bool Played;
        public string Winner = "";
        public int HomeRuns, HomeWkts, HomeBalls, AwayRuns, AwayWkts, AwayBalls;
        public string Result = "";
        public bool SuperOver, UserPlayed;
        public int PotmId;

        public bool Involves(string id) => Home == id || Away == id;
        public string Opponent(string id) => Home == id ? Away : Home;
        public string HomeScore => HomeRuns + "/" + HomeWkts + " (" + Overs.Text(HomeBalls) + ")";
        public string AwayScore => AwayRuns + "/" + AwayWkts + " (" + Overs.Text(AwayBalls) + ")";
    }

    [Serializable]
    public sealed class TableRow
    {
        public string Team;
        public int P, W, L, NR, Pts;
        public int RunsFor, BallsFor, RunsAgainst, BallsAgainst;
        public string Form = "";

        public double Nrr => (BallsFor == 0 ? 0 : RunsFor * 6.0 / BallsFor) - (BallsAgainst == 0 ? 0 : RunsAgainst * 6.0 / BallsAgainst);
        public string NrrText => (Nrr >= 0 ? "+" : "") + Nrr.ToString("0.000");
    }

    [Serializable]
    public sealed class SeasonStat
    {
        public int PlayerId;
        public string Team;
        public int Inns, Runs, Balls, Fours, Sixes, NotOuts, High, Fifties, Hundreds;
        public int BallsBowled, RunsConceded, Wkts, BestW, BestR = 999;
        public int Potm;

        public double Average => Inns - NotOuts <= 0 ? Runs : Runs / (double)(Inns - NotOuts);
        public double StrikeRate => Balls == 0 ? 0 : Runs * 100.0 / Balls;
        public double Economy => BallsBowled == 0 ? 0 : RunsConceded * 6.0 / BallsBowled;
    }

    /// <summary>
    /// A KPL season: league stage (Express = single round robin of 9 games each, Full = the
    /// 14-game two-group format), then Qualifier 1, Eliminator, Qualifier 2 and the Final.
    /// Fully serializable so it survives app restarts.
    /// </summary>
    [Serializable]
    public sealed class Tournament
    {
        public int Version = 1;
        public TourFormat Format;
        public int Overs = 5;
        public string UserTeam;
        public long Seed;
        public List<Fixture> Fixtures = new List<Fixture>();
        public List<TableRow> Table = new List<TableRow>();
        public List<SeasonStat> Stats = new List<SeasonStat>();
        public int Stage;
        public string Champion = "";
        public string RunnerUp = "";
        public int SimCounter;
        public bool UserEliminated;
        public int DifficultyLevel = 1;

        public TourStage StageEnum => (TourStage)Stage;
        public bool Complete => StageEnum == TourStage.Done;

        public static Tournament Create(TourFormat format, int overs, string userTeam, long seed, Difficulty d)
        {
            var t = new Tournament { Format = format, Overs = overs, UserTeam = userTeam, Seed = seed, DifficultyLevel = (int)d };
            foreach (var team in Teams.All) t.Table.Add(new TableRow { Team = team.Id });
            var rng = new Rng(seed);
            var pairs = format == TourFormat.Express ? RoundRobin(rng) : TwoGroups(rng);
            int no = 1;
            foreach (var (a, b) in Schedule(pairs, rng))
            {
                t.Fixtures.Add(new Fixture { No = no++, Home = a, Away = b, Stage = 0 });
            }
            return t;
        }

        static List<(string, string)> RoundRobin(Rng rng)
        {
            var ids = Teams.All.Select(x => x.Id).ToList();
            var list = new List<(string, string)>();
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++) list.Add(rng.Chance(0.5) ? (ids[i], ids[j]) : (ids[j], ids[i]));
            }
            return list;
        }

        /// <summary>Two groups of five: group rivals twice, the other group once, plus one extra cross-group game = 14 each.</summary>
        static List<(string, string)> TwoGroups(Rng rng)
        {
            var ids = Teams.All.Select(x => x.Id).ToList();
            var a = ids.Take(5).ToList();
            var b = ids.Skip(5).ToList();
            var list = new List<(string, string)>();
            foreach (var g in new[] { a, b })
            {
                for (int i = 0; i < 5; i++)
                {
                    for (int j = i + 1; j < 5; j++)
                    {
                        list.Add((g[i], g[j]));
                        list.Add((g[j], g[i]));
                    }
                }
            }
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++) list.Add(rng.Chance(0.5) ? (a[i], b[j]) : (b[j], a[i]));
                // Each team meets its "row partner" in the other group a second time.
                list.Add(rng.Chance(0.5) ? (a[i], b[i]) : (b[i], a[i]));
            }
            return list;
        }

        /// <summary>Orders games into rounds where nobody plays twice, so every team's games are spread out.</summary>
        static List<(string, string)> Schedule(List<(string, string)> pairs, Rng rng)
        {
            var left = pairs.ToList();
            rng.Shuffle(left);
            var ordered = new List<(string, string)>();
            var lastPlayed = new Dictionary<string, int>();
            int round = 0;
            while (left.Count > 0)
            {
                var busy = new HashSet<string>();
                var thisRound = new List<(string, string)>();
                // Teams that have waited longest go first.
                foreach (var p in left.OrderBy(p => Math.Max(Last(lastPlayed, p.Item1), Last(lastPlayed, p.Item2))).ToList())
                {
                    if (busy.Contains(p.Item1) || busy.Contains(p.Item2)) continue;
                    busy.Add(p.Item1);
                    busy.Add(p.Item2);
                    thisRound.Add(p);
                    left.Remove(p);
                }
                foreach (var p in thisRound)
                {
                    lastPlayed[p.Item1] = round;
                    lastPlayed[p.Item2] = round;
                }
                ordered.AddRange(thisRound);
                round++;
            }
            return ordered;
        }

        static int Last(Dictionary<string, int> d, string k) => d.TryGetValue(k, out var v) ? v : -1;

        public TableRow Row(string id) => Table.First(r => r.Team == id);

        public List<TableRow> Standings() => Table.OrderByDescending(r => r.Pts).ThenByDescending(r => r.Nrr).ThenByDescending(r => r.W).ThenBy(r => r.Team).ToList();

        public Fixture NextFixture() => Fixtures.FirstOrDefault(f => !f.Played);

        public Fixture NextUserFixture() => Fixtures.FirstOrDefault(f => !f.Played && f.Involves(UserTeam));

        public int UserGamesPlayed => Fixtures.Count(f => f.Played && f.Stage == 0 && f.Involves(UserTeam));
        public int UserGamesTotal => Fixtures.Count(f => f.Stage == 0 && f.Involves(UserTeam));
        public int LeagueGamesLeft => Fixtures.Count(f => f.Stage == 0 && !f.Played);

        // --------------------------------------------------------- results

        public void Record(Fixture fx, MatchState m, bool userPlayed)
        {
            fx.Played = true;
            fx.UserPlayed = userPlayed;
            fx.Winner = m.Result.WinnerId ?? "";
            fx.Result = m.Result.Text;
            fx.SuperOver = m.Result.SuperOver;
            fx.PotmId = m.Result.PotmId;
            var main = m.InningsList.Where(i => !i.SuperOver).ToList();
            foreach (var inn in main)
            {
                if (inn.Bat.Team.Id == fx.Home)
                {
                    fx.HomeRuns = inn.Runs;
                    fx.HomeWkts = inn.Wkts;
                    fx.HomeBalls = inn.Balls;
                }
                else
                {
                    fx.AwayRuns = inn.Runs;
                    fx.AwayWkts = inn.Wkts;
                    fx.AwayBalls = inn.Balls;
                }
            }
            AddStats(m);
            if (fx.Stage == 0)
            {
                foreach (var id in new[] { fx.Home, fx.Away })
                {
                    var row = Row(id);
                    var (rf, bf, ra, ba) = m.NrrFigures(id);
                    row.P++;
                    row.RunsFor += rf;
                    row.BallsFor += bf;
                    row.RunsAgainst += ra;
                    row.BallsAgainst += ba;
                    bool won = fx.Winner == id;
                    if (fx.Winner == "")
                    {
                        row.NR++;
                        row.Pts += 1;
                    }
                    else if (won)
                    {
                        row.W++;
                        row.Pts += 2;
                    }
                    else row.L++;
                    row.Form = (row.Form + (fx.Winner == "" ? "N" : won ? "W" : "L"));
                    if (row.Form.Length > 5) row.Form = row.Form.Substring(row.Form.Length - 5);
                }
            }
            Advance();
        }

        void AddStats(MatchState m)
        {
            foreach (var inn in m.InningsList.Where(i => !i.SuperOver))
            {
                foreach (var c in inn.BatCards.Where(c => c.Batted))
                {
                    var s = Stat(c.P.Id, inn.Bat.Team.Id);
                    s.Inns++;
                    s.Runs += c.Runs;
                    s.Balls += c.Balls;
                    s.Fours += c.Fours;
                    s.Sixes += c.Sixes;
                    if (!c.Out) s.NotOuts++;
                    s.High = Math.Max(s.High, c.Runs);
                    if (c.Runs >= 100) s.Hundreds++;
                    else if (c.Runs >= 50) s.Fifties++;
                }
                foreach (var b in inn.BowlCards)
                {
                    var s = Stat(b.P.Id, inn.Bowl.Team.Id);
                    s.BallsBowled += b.Balls;
                    s.RunsConceded += b.Runs;
                    s.Wkts += b.Wkts;
                    if (b.Wkts > s.BestW || (b.Wkts == s.BestW && b.Runs < s.BestR))
                    {
                        s.BestW = b.Wkts;
                        s.BestR = b.Runs;
                    }
                }
            }
            var p = m.PlayerById(m.Result.PotmId);
            if (p != null) Stat(p.Id, p.TeamId).Potm++;
        }

        SeasonStat Stat(int id, string team)
        {
            foreach (var s in Stats)
            {
                if (s.PlayerId == id) return s;
            }
            var n = new SeasonStat { PlayerId = id, Team = team };
            Stats.Add(n);
            return n;
        }

        public List<SeasonStat> TopRuns(int n) => Stats.OrderByDescending(s => s.Runs).ThenByDescending(s => s.StrikeRate).Take(n).ToList();
        public List<SeasonStat> TopWickets(int n) => Stats.Where(s => s.BallsBowled > 0).OrderByDescending(s => s.Wkts).ThenBy(s => s.Economy).Take(n).ToList();
        public List<SeasonStat> TopSixes(int n) => Stats.OrderByDescending(s => s.Sixes).Take(n).ToList();

        /// <summary>Creates the next playoff fixture(s) once the previous stage is complete.</summary>
        void Advance()
        {
            if (StageEnum == TourStage.Done) return;
            if (Fixtures.Any(f => !f.Played)) return;
            var st = Standings();
            int next = Fixtures.Count + 1;
            Fixture Find(TourStage s) => Fixtures.FirstOrDefault(f => f.Stage == (int)s);
            string Loser(Fixture f) => f.Winner == f.Home ? f.Away : f.Home;
            switch (StageEnum)
            {
                case TourStage.League:
                    Stage = (int)TourStage.Qualifier1;
                    Fixtures.Add(new Fixture { No = next++, Home = st[0].Team, Away = st[1].Team, Stage = (int)TourStage.Qualifier1 });
                    Fixtures.Add(new Fixture { No = next, Home = st[2].Team, Away = st[3].Team, Stage = (int)TourStage.Eliminator });
                    if (!st.Take(4).Any(r => r.Team == UserTeam)) UserEliminated = true;
                    break;
                case TourStage.Qualifier1:
                case TourStage.Eliminator:
                {
                    var q1 = Find(TourStage.Qualifier1);
                    var el = Find(TourStage.Eliminator);
                    if (el != null && UserTeam == Loser(el)) UserEliminated = true;
                    Stage = (int)TourStage.Qualifier2;
                    Fixtures.Add(new Fixture { No = next, Home = Loser(q1), Away = el.Winner, Stage = (int)TourStage.Qualifier2 });
                    break;
                }
                case TourStage.Qualifier2:
                {
                    var q1 = Find(TourStage.Qualifier1);
                    var q2 = Find(TourStage.Qualifier2);
                    if (UserTeam == Loser(q2)) UserEliminated = true;
                    Stage = (int)TourStage.Final;
                    Fixtures.Add(new Fixture { No = next, Home = q1.Winner, Away = q2.Winner, Stage = (int)TourStage.Final });
                    break;
                }
                case TourStage.Final:
                {
                    var f = Find(TourStage.Final);
                    Champion = f.Winner;
                    RunnerUp = Loser(f);
                    if (UserTeam != Champion) UserEliminated = true;
                    Stage = (int)TourStage.Done;
                    break;
                }
            }
        }

        /// <summary>Sims every AI game before the user's next fixture (or all remaining if the user is out).</summary>
        public int SimUntilUser(Func<Fixture, MatchState> play)
        {
            int n = 0;
            while (true)
            {
                var f = NextFixture();
                if (f == null || (!UserEliminated && f.Involves(UserTeam))) break;
                var m = play(f);
                Record(f, m, false);
                n++;
                if (n > 200) break;
            }
            return n;
        }
    }
}
