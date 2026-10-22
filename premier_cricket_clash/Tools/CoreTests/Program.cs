using System;
using System.Collections.Generic;
using System.Linq;
using PCC.Core;

/// Plain console test runner for the game logic (no Unity needed).
static class Program
{
    static int _failed;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) _failed++;
    }

    static int Main()
    {
        Console.WriteLine("Player database");
        var db = PlayerDb.Instance;
        Check(Teams.All.All(t => db.Squad(t.Id).Count >= 18 && db.Squad(t.Id).Count <= 25) && db.All.Count == Teams.All.Sum(t => db.Squad(t.Id).Count),
            "every franchise has an 18-25 player squad (" + db.All.Count + " players" + (PlayerDb.UseRealNames ? ", real names" : "") + ")");
        Check(db.All.Select(p => p.Name).Distinct().Count() == db.All.Count, "all player names are unique");
        foreach (var t in Teams.All)
        {
            var xi = Lineup.PickBestXI(db.Squad(t.Id));
            string problem = Lineup.Problem(xi);
            Check(problem == null, t.Id + " best XI is legal (rating " + Lineup.TeamRating(xi) + ")" + (problem == null ? "" : ": " + problem));
        }
        var top = db.All.OrderByDescending(p => p.Overall).Take(8).Select(p => p.Name + " " + p.Overall);
        Console.WriteLine("  Top players: " + string.Join(", ", top));

        Console.WriteLine("Match engine (400 simulated T20s)");
        double r1 = 0, w1 = 0, r2 = 0, sixes = 0;
        int chases = 0, superOvers = 0;
        bool quotas = true;
        const int n = 400;
        for (int i = 0; i < n; i++)
        {
            var h = Teams.All[i % 10];
            var a = Teams.All[(i * 3 + 1) % 10 == i % 10 ? (i + 1) % 10 : (i * 3 + 1) % 10];
            var m = new MatchEngine(TeamSheet.Auto(h, db.Squad(h.Id)), TeamSheet.Auto(a, db.Squad(a.Id)), h, i * 7919);
            m.SimulateToEnd();
            if (!m.Finished) { Check(false, "match " + i + " finished"); continue; }
            r1 += m.InningsList[0].Runs;
            w1 += m.InningsList[0].Wickets;
            r2 += m.InningsList[1].Runs;
            sixes += m.InningsList[0].Bat.Values.Sum(c => c.Sixes);
            if (m.Outcome.WinnerId == m.InningsList[1].Batting.Team.Id) chases++;
            if (m.Outcome.SuperOver) superOvers++;
            foreach (var inn in m.InningsList.Take(2))
            {
                if (inn.Bowl.Values.Any(b => b.Balls > 24) || inn.LegalBalls > 120) quotas = false;
            }
        }
        Console.WriteLine($"  Avg 1st inns {r1 / n:F1}/{w1 / n:F1}, 2nd {r2 / n:F1}, sixes/inns {sixes / n:F1}, chase win {chases * 100 / n}%, super overs {superOvers}");
        Check(r1 / n >= 160 && r1 / n <= 210, "average first-innings score is realistic for T20");
        Check(w1 / n >= 5.0 && w1 / n <= 8.5, "average first-innings wickets is realistic");
        Check(quotas, "bowling quotas (4 overs) and 20 overs respected");

        Console.WriteLine("User batting");
        var home = Teams.All[2];
        var away = Teams.All[1];
        int perfectRuns = 0, lateRuns = 0, perfectOuts = 0, lateOuts = 0;
        for (int i = 0; i < 600; i++)
        {
            var m = new MatchEngine(TeamSheet.Auto(home, db.Squad(home.Id)), TeamSheet.Auto(away, db.Squad(away.Id)), home, i);
            m.SetToss(home.Id, true);
            m.AutoBowlerIfNeeded();
            var plan = Deliveries.PlanAi(m.Rng, m.Bowler, 1);
            var good = m.ResolveUserShot(plan, new ShotInput { Dir = ShotDir.Straight, TimingMs = 5 });
            var bad = m.ResolveUserShot(plan, new ShotInput { Dir = ShotDir.Straight, TimingMs = 230 });
            perfectRuns += good.Runs;
            lateRuns += bad.Runs;
            if (good.Wicket) perfectOuts++;
            if (bad.Wicket) lateOuts++;
        }
        Console.WriteLine($"  perfect: {perfectRuns / 600.0:F2} r/b, out {perfectOuts * 100 / 600.0:F1}%   late: {lateRuns / 600.0:F2} r/b, out {lateOuts * 100 / 600.0:F1}%");
        Check(perfectRuns > lateRuns && perfectOuts < lateOuts, "good timing scores more and gets out less");

        Console.WriteLine("User bowling");
        var rng = new Rng(7);
        bool ok = true;
        for (int i = 0; i < 400; i++)
        {
            var m = new MatchEngine(TeamSheet.Auto(away, db.Squad(away.Id)), TeamSheet.Auto(home, db.Squad(home.Id)), home, i);
            m.SetToss(away.Id, true);
            m.AutoBowlerIfNeeded();
            var types = (DeliveryType[])Enum.GetValues(typeof(DeliveryType));
            var lines = (BallLine[])Enum.GetValues(typeof(BallLine));
            var plan = Deliveries.PlanUser(m.Rng, m.Bowler, types[rng.NextInt(types.Length)], lines[rng.NextInt(lines.Length)], rng.Next(), m.Current.Phase,
                aimY: 8.12 + rng.Next() * 11.6, aimX: -1.3 + rng.Next() * 2.6, swing: rng.Chance(0.5) ? 0.27 : (double?)null);
            try
            {
                m.Apply(m.ResolveUserBowling(plan, new List<DeliveryType> { DeliveryType.Yorker }));
            }
            catch (Exception e)
            {
                ok = false;
                Console.WriteLine("  " + e);
                break;
            }
        }
        Check(ok, "user deliveries resolve for any aim, accuracy and type");

        Console.WriteLine("Mega auction (AI only, 20 auctions)");
        Check(db.FreeAgents.Count > 0, db.FreeAgents.Count + " free agents in the pool");
        Check(db.All.Concat(db.FreeAgents).Select(p => p.Name).Distinct().Count() == db.All.Count + db.FreeAgents.Count, "free agent names are unique");
        bool legal = true, unique = true, purseOk = true, retentionsOk = true;
        int sold = 0, lotsTotal = 0, rtmUsed = 0, topPrice = 0;
        string topName = "";
        double avgSpent = 0;
        for (int i = 0; i < 20; i++)
        {
            var user = Teams.All[i % 10];
            var keep = Retention.AiPick(db.Squad(user.Id));
            if (Retention.Problem(keep) != null) retentionsOk = false;
            var a = MegaAuction.Create(user.Id, keep, 1000 + i);
            a.AiManagesUser = true;
            int guard = 0;
            while (!a.Finished && guard++ < 2000)
            {
                a.ResolveInstantly();
                a.Advance();
            }
            a.FillSquads();
            lotsTotal += a.Lots.Count;
            sold += a.SoldCount;
            rtmUsed += a.Lots.Count(l => l.ViaRtm);
            var ids = a.Franchises.Values.SelectMany(f => f.Squad.Select(c => c.Player.Id)).ToList();
            if (ids.Distinct().Count() != ids.Count) unique = false;
            foreach (var f in a.Franchises.Values)
            {
                string why = f.Size < AuctionRules.MinSquad || f.Size > AuctionRules.MaxSquad ? "size " + f.Size
                    : f.OverseasCount > AuctionRules.MaxOverseas ? "overseas " + f.OverseasCount
                    : Lineup.Problem(Lineup.PickBestXI(f.Players));
                if (why != null)
                {
                    if (legal) Console.WriteLine("  auction " + i + ": " + f.Id + " -> " + why + " (purse " + AuctionRules.Money(f.Purse) + ", WK " + f.Mix.Wk + ")");
                    legal = false;
                }
                if (f.Purse < 0) purseOk = false;
                avgSpent += f.Spent / 200.0;
            }
            var saved = AuctionResult.Parse(AuctionResult.Serialize(user.Id, a.Franchises.Values));
            if (saved == null || saved.Value.UserId != user.Id
                || saved.Value.Teams.Values.Sum(f => f.Size) != a.Franchises.Values.Sum(f => f.Size)
                || saved.Value.Teams[user.Id].Purse != a.Franchises[user.Id].Purse) unique = false;
            foreach (var l in a.Lots.Where(l => l.Status == LotStatus.Sold && l.Price > topPrice))
            {
                topPrice = l.Price;
                topName = l.Player.Name + " (" + l.Player.Overall + ")";
            }
        }
        Console.WriteLine($"  lots/auction {lotsTotal / 20}, sold {sold / 20}, RTM used {rtmUsed / 20.0:F1}, avg spend {AuctionRules.Money((int)avgSpent)}, record buy {AuctionRules.Money(topPrice)} for {topName}");
        Check(retentionsOk, "AI retentions follow the slab rules");
        Check(legal, "every squad ends with 18-25 players, max 8 overseas and a legal XI");
        Check(purseOk, "no franchise overspends its purse");
        Check(unique, "no player is signed by two franchises, and results save / load exactly");

        Console.WriteLine("Auction pause / resume");
        {
            var user = Teams.All[3];
            var a = MegaAuction.Create(user.Id, Retention.AiPick(db.Squad(user.Id)), 4242);
            a.AiManagesUser = true;
            for (int k = 0; k < 90; k++)
            {
                a.ResolveInstantly();
                a.Advance();
            }
            a.OpenLot();
            a.AiTick();
            string saved = AuctionProgress.Serialize(a);
            var b = AuctionProgress.Deserialize(saved, 99);
            bool same = b != null && b.Index == a.Index && b.Lots.Count == a.Lots.Count && b.UserId == a.UserId
                        && a.Franchises.Values.All(f => b.Franchises[f.Id].Purse == f.Purse && b.Franchises[f.Id].Size == f.Size && b.Franchises[f.Id].RtmCards == f.RtmCards)
                        && b.Lots.Take(a.Index).Zip(a.Lots.Take(a.Index), (x, y) => x.Status == y.Status && x.Price == y.Price && x.SoldTo == y.SoldTo).All(ok2 => ok2)
                        && b.Lot.Status == LotStatus.Waiting && b.RtmTeamOf.Count == a.RtmTeamOf.Count;
            Check(same, "a saved auction (lot " + (a.Index + 1) + " of " + a.Lots.Count + ") restores purses, squads, RTM cards and every lot");
            b.AiManagesUser = true;
            int guard = 0;
            while (!b.Finished && guard++ < 2000)
            {
                b.ResolveInstantly();
                b.Advance();
            }
            b.FillSquads();
            Check(b.Franchises.Values.All(f => f.Size >= AuctionRules.MinSquad && f.Size <= AuctionRules.MaxSquad && f.Purse >= 0 && f.OverseasCount <= AuctionRules.MaxOverseas),
                "a resumed auction finishes with legal squads");
            Check(AuctionProgress.Deserialize("garbage", 1) == null, "a corrupt save is ignored");
        }

        Console.WriteLine("Player renames");
        {
            var p = db.Squad("MUM")[0];
            string original = p.Name;
            Player.NameOverride = id => id == p.Id ? "Test Renamed" : null;
            bool ok3 = p.Name == "Test Renamed" && p.ShortName == "T Renamed" && p.Surname == "RENAMED" && p.OriginalName == original;
            Player.NameOverride = null;
            Check(ok3 && p.Name == original, "renames apply to name, short name and surname, and reset cleanly");
        }

        Console.WriteLine(_failed == 0 ? "ALL TESTS PASSED" : _failed + " TEST(S) FAILED");
        return _failed == 0 ? 0 : 1;
    }
}
