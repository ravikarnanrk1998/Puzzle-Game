using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KPL.Core;

static class Program
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        if (!ok)
        {
            _fail++;
            Console.WriteLine("  FAIL: " + what);
        }
    }

    static int Main(string[] args)
    {
        string only = args.Length > 0 ? args[0] : "";
        if (only == "" || only == "db") Database();
        if (only == "" || only == "balance") Balance(20, 400);
        if (only == "" || only == "balance5") Balance(5, 600);
        if (only == "" || only == "user") UserBatting();
        if (only == "diag") Diag();
        if (only == "" || only == "tour") Tour(TourFormat.Express, 5);
        if (only == "" || only == "tour") Tour(TourFormat.Full, 20);
        if (only == "" || only == "chal") ChallengesTest();
        if (only == "" || only == "econ") EconomyTest();
        Console.WriteLine(_fail == 0 ? "ALL CHECKS PASSED" : _fail + " CHECK(S) FAILED");
        return _fail == 0 ? 0 : 1;
    }

    static void Database()
    {
        Console.WriteLine("== Player database");
        var all = PlayerDb.All;
        Check(all.Count == Teams.All.Count * League.SquadSize, "squad sizes");
        Check(all.Select(p => p.Name).Distinct().Count() == all.Count, "unique names");
        foreach (var t in Teams.All)
        {
            var sheet = TeamSheet.Default(t);
            Check(sheet.XI.Count == 11, t.Id + " XI has 11");
            Check(sheet.XI.Count(p => p.Overseas) <= 4, t.Id + " overseas <= 4");
            Check(sheet.XI.Count(p => p.CanBowl) >= 5, t.Id + " five bowling options");
            Check(sheet.XI.Any(p => p.IsKeeper), t.Id + " has keeper");
            Console.WriteLine("  {0} {1,-20} OVR {2} BAT {3} BOWL {4}  XI: {5}", t.Id, t.Name, sheet.Overall, sheet.BattingStrength, sheet.BowlingStrength,
                string.Join(", ", sheet.XI.Select(p => p.Short)));
        }
    }

    static void Balance(int overs, int matches)
    {
        Console.WriteLine("== AI v AI balance, {0} overs, {1} matches", overs, matches);
        var sw = Stopwatch.StartNew();
        var rng = new Rng(42);
        int inns = 0, runs = 0, wkts = 0, fours = 0, sixes = 0, wides = 0, noBalls = 0, balls = 0, allOut = 0, ties = 0, chased = 0;
        var kinds = new Dictionary<WicketKind, int>();
        int superOvers = 0;
        for (int i = 0; i < matches; i++)
        {
            var a = Teams.All[rng.Int(10)];
            TeamDef b;
            do b = Teams.All[rng.Int(10)];
            while (b == a);
            var setup = new MatchSetup { Home = TeamSheet.Default(a), Away = TeamSheet.Default(b), Overs = overs, Venue = a, Seed = rng.Fork() };
            var m = BallEngine.QuickSim(setup);
            Check(m.Finished, "match finished");
            if (m.Result.SuperOver) superOvers++;
            for (int k = 0; k < 2; k++)
            {
                var inn = m.InningsList[k];
                inns++;
                runs += inn.Runs;
                wkts += inn.Wkts;
                balls += inn.Balls;
                wides += inn.Wides;
                noBalls += inn.NoBalls;
                if (inn.AllOut) allOut++;
                fours += inn.BatCards.Sum(c => c.Fours);
                sixes += inn.BatCards.Sum(c => c.Sixes);
                foreach (var c in inn.BatCards.Where(c => c.Out)) kinds[c.How.Value] = kinds.TryGetValue(c.How.Value, out var n) ? n + 1 : 1;
                if (k == 1 && inn.Chased) chased++;
                int bowled = inn.BowlCards.Sum(c => c.Balls);
                Check(bowled == inn.Balls, "bowler balls add up");
                Check(inn.BowlCards.All(c => (c.Balls + 5) / 6 <= inn.QuotaPerBowler), "bowling quota respected");
                int batRuns = inn.BatCards.Sum(c => c.Runs);
                Check(batRuns + inn.Extras == inn.Runs, "runs add up (" + batRuns + "+" + inn.Extras + " vs " + inn.Runs + ")");
            }
        }
        double perInn(int x) => x / (double)inns;
        Console.WriteLine("  avg score {0:0.0}/{1:0.0}  RR {2:0.00}  4s {3:0.0}  6s {4:0.0}  wd {5:0.0} nb {6:0.00}  all-out {7:P0}  chased {8:P0}  super overs {9}",
            perInn(runs), perInn(wkts), runs * 6.0 / balls, perInn(fours), perInn(sixes), perInn(wides), perInn(noBalls), allOut / (double)inns, chased / (double)matches, superOvers);
        Console.WriteLine("  dismissals: " + string.Join(", ", kinds.OrderByDescending(k => k.Value).Select(k => k.Key + " " + (k.Value * 100.0 / Math.Max(1, wkts)).ToString("0") + "%")));
        Console.WriteLine("  {0:0} ms per match", sw.Elapsed.TotalMilliseconds / matches);
        double rr = runs * 6.0 / balls;
        if (overs == 20)
        {
            Check(rr > 7.4 && rr < 9.8, "T20 run rate in a realistic range");
            Check(perInn(wkts) > 5 && perInn(wkts) < 8.5, "T20 wickets in a realistic range");
            Check(perInn(sixes) > 4 && perInn(sixes) < 12, "T20 sixes in a realistic range");
        }
    }

    static void Tour(TourFormat fmt, int overs)
    {
        Console.WriteLine("== Tournament {0}, {1} overs", fmt, overs);
        var sw = Stopwatch.StartNew();
        var t = Tournament.Create(fmt, overs, "MUM", 99, Difficulty.Medium);
        int leagueGames = fmt == TourFormat.Express ? 45 : 70;
        Check(t.Fixtures.Count == leagueGames, "league fixture count " + t.Fixtures.Count);
        foreach (var team in Teams.All)
        {
            int n = t.Fixtures.Count(f => f.Involves(team.Id));
            Check(n == (fmt == TourFormat.Express ? 9 : 14), team.Id + " plays the right number of games (" + n + ")");
        }
        var rng = new Rng(3);
        MatchState Play(Fixture f) => BallEngine.QuickSim(new MatchSetup
        {
            Home = TeamSheet.Default(Teams.ById(f.Home)), Away = TeamSheet.Default(Teams.ById(f.Away)), Overs = overs, Venue = Teams.ById(f.Home), Seed = rng.Fork(),
        });
        int guard = 0;
        while (!t.Complete && guard++ < 200)
        {
            t.SimUntilUser(Play);
            var f = t.NextFixture();
            if (f == null) break;
            t.Record(f, Play(f), true);
        }
        Check(t.Complete, "season completes");
        Check(t.Fixtures.Count == leagueGames + 4, "four playoff games");
        Check(t.Champion != "" && t.RunnerUp != "" && t.Champion != t.RunnerUp, "champion and runner-up");
        Check(t.Table.Sum(r => r.W) == t.Table.Sum(r => r.L), "wins equal losses");
        Check(t.Table.Sum(r => r.Pts) == leagueGames * 2, "points total");
        var st = t.Standings();
        foreach (var r in st) Console.WriteLine("  {0} P{1,2} W{2,2} L{3,2} Pts{4,3} NRR {5}  {6}", r.Team, r.P, r.W, r.L, r.Pts, r.NrrText, r.Form);
        foreach (var f in t.Fixtures.Where(f => f.Stage > 0)) Console.WriteLine("  {0,-12} {1} v {2}: {3}", ((TourStage)f.Stage).Label(), f.Home, f.Away, f.Result);
        var top = t.TopRuns(1)[0];
        var topW = t.TopWickets(1)[0];
        Console.WriteLine("  Champion {0}. Most runs {1} ({2}), most wickets {3} ({4}). {5:0} ms", t.Champion, PlayerDb.ById(top.PlayerId).Name, top.Runs,
            PlayerDb.ById(topW.PlayerId).Name, topW.Wkts, sw.Elapsed.TotalMilliseconds);
    }

    static void ChallengesTest()
    {
        Console.WriteLine("== Challenges");
        Check(Challenges.All.Count == 50, "50 challenges");
        Check(Challenges.All.Select(c => c.Id).Distinct().Count() == 50, "unique ids");
        var rng = new Rng(11);
        var line = new List<string>();
        foreach (var c in Challenges.All)
        {
            var user = TeamSheet.Default(Teams.ById("MUM"));
            var opp = TeamSheet.Default(Challenges.Opponent(c, "MUM"));
            int total = 0;
            for (int k = 0; k < 30; k++)
            {
                var setup = Challenges.Setup(c, user, opp, rng.Fork(), Difficulty.Medium);
                var m = new MatchState(setup);
                new BallEngine(m).SimToEnd();
                int stars = c.Evaluate(m, "MUM");
                Check(stars >= 0 && stars <= 3, "stars in range");
                Check(m.Finished, "challenge finishes");
                total += stars;
            }
            line.Add(c.Id + ":" + (total / 30.0).ToString("0.0"));
            Check(!string.IsNullOrEmpty(c.GoalText) && !string.IsNullOrEmpty(c.StarText(3)), "texts");
        }
        Console.WriteLine("  avg stars when the AI plays it: " + string.Join(" ", line));
    }

    static void EconomyTest()
    {
        Console.WriteLine("== Economy and achievements");
        var s = new SaveData();
        s.Repair();
        Check(s.ChallengeStars.Count == 50, "stars list sized");
        Check(Shop.Buy(s, "bat_legend") == Shop.BuyResult.NotEnoughCoins, "can't afford legend bat");
        s.Coins = 1300;
        Check(Shop.Buy(s, "bat_kashmir") == Shop.BuyResult.Ok && s.Coins == 100 && s.Bat == "bat_kashmir", "buy + equip");
        Check(Shop.Buy(s, "bat_kashmir") == Shop.BuyResult.AlreadyOwned, "no double purchase");
        Check(!Economy.BuyCoach(s, 0), "coach needs coins");
        s.Coins = 1500;
        Check(Economy.BuyCoach(s, 0) && s.CoachBat == 1 && s.Coins == 0, "coach level 1");
        var day = new DateTime(2026, 11, 3, 10, 0, 0);
        Check(Economy.ClaimDaily(s, day) == 100 && Economy.ClaimDaily(s, day) == 0, "daily once per day");
        Check(Economy.ClaimDaily(s, day.AddDays(1)) == 150 && s.DailyStreak == 2, "streak continues");
        Check(Economy.ClaimDaily(s, day.AddDays(3)) == 100 && s.DailyStreak == 1, "streak resets after a gap");
        s.Stats.Wins = 1;
        var got = Achievements.Check(s);
        Check(got.Any(a => a.Id == "first_win"), "first win unlocks");
        int before = s.Coins;
        Check(Achievements.Claim(s, "first_win") && s.Coins == before + 200, "claim reward");
        Check(!Achievements.Claim(s, "first_win"), "claim once");
        var bad = new SaveData { Settings = null, Owned = null, Bat = "nope", Coins = -5, Tour = null, HasTour = true };
        bad.Repair();
        Check(bad.Settings != null && bad.Bat == "bat_club" && bad.Coins == 0 && !bad.HasTour, "repair damaged save");
    }

    /// <summary>Per-delivery breakdown of AI v AI: what the batter tried, how it went, and who took the catches.</summary>
    static void Diag()
    {
        var rng = new Rng(5);
        var tally = new Dictionary<string, int>();
        void T(string k) => tally[k] = tally.TryGetValue(k, out var n) ? n + 1 : 1;
        int total = 0;
        for (int i = 0; i < 150; i++)
        {
            var setup = new MatchSetup { Home = TeamSheet.Default(Teams.All[i % 10]), Away = TeamSheet.Default(Teams.All[(i + 3) % 10]), Overs = 20, Venue = Teams.All[i % 10], Seed = rng.Fork() };
            var m = new MatchState(setup);
            m.SetToss(setup.Home.Team.Id, true);
            var e = new BallEngine(m);
            while (!m.Finished && m.InningsList.Count <= 2)
            {
                e.EnsureBowler();
                if (m.Current.BallInOver == 0) e.PrepareField();
                var tr = e.BuildTrack(e.AiPlan());
                var shot = e.AiShot(tr);
                var play = e.Resolve(tr, shot, false);
                total++;
                T("shot." + shot.Kind);
                T("contact." + play.Contact.Type);
                if (play.Contact.Grade != TimingGrade.None) T("grade." + play.Contact.Grade);
                T("len." + tr.LengthClass);
                if (play.CatchChance) T("catchchance.f" + (play.Fielder >= 9 ? play.Fielder.ToString() : e.Side.Layout.Names[play.Fielder]));
                if (play.Wicket != null) T("wkt." + play.Wicket + "." + play.Contact.Type);
                if (play.Four) T("runs.4");
                else if (play.Six) T("runs.6");
                else T("runs." + play.Runs);
                e.Commit(play);
            }
        }
        foreach (var kv in tally.OrderBy(k => k.Key)) Console.WriteLine("  {0,-40} {1,6:0.00}%", kv.Key, kv.Value * 100.0 / total);
    }

    /// <summary>A bot pressing buttons with a given timing spread, as a stand-in for a human.</summary>
    static void UserBatting()
    {
        Console.WriteLine("== User batting model (bot with timing spread)");
        foreach (var spread in new[] { 30.0, 60.0, 110.0 })
        {
            foreach (var diff in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
            {
                var rng = new Rng(7);
                int runs = 0, wk = 0, balls = 0, six = 0;
                for (int i = 0; i < 120; i++)
                {
                    var setup = new MatchSetup { Home = TeamSheet.Default(Teams.All[0]), Away = TeamSheet.Default(Teams.All[1]), Overs = 5, Venue = Teams.All[0], Seed = rng.Fork(), UserTeamId = "MUM", Difficulty = diff };
                    var m = new MatchState(setup);
                    m.SetToss("MUM", true);
                    var e = new BallEngine(m);
                    var inn = m.Current;
                    while (!m.Finished && m.Current == inn)
                    {
                        e.EnsureBowler();
                        if (inn.BallInOver == 0) e.PrepareField();
                        var tr = e.BuildTrack(e.AiPlan());
                        // Bot: picks the best-suited direction, lofts a third of the time.
                        var kind = rng.Chance(0.35) ? ShotKind.Loft : ShotKind.Ground;
                        double best = 0, bs = -9;
                        foreach (var az in new[] { 0.0, 45, 95, 140, -45, -95, -145 })
                        {
                            double s = Batting.Suitability(tr, kind, az);
                            if (s > bs) { bs = s; best = az; }
                        }
                        var shot = new ShotInput { Kind = kind, Azimuth = best + rng.Gauss() * 10, TimingMs = rng.Gauss() * spread };
                        e.Commit(e.Resolve(tr, shot, true));
                    }
                    runs += inn.Runs;
                    wk += inn.Wkts;
                    balls += inn.Balls;
                    six += inn.BatCards.Sum(c => c.Sixes);
                }
                Console.WriteLine("  timing sd {0,3}ms {1,-6}: RR {2:0.0}  wkts/5ov {3:0.0}  6s/5ov {4:0.0}", spread, diff, runs * 6.0 / balls, wk / 120.0, six / 120.0);
            }
        }
    }
}
