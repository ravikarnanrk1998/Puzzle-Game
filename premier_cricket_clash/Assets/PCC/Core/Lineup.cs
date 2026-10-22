using System;
using System.Collections.Generic;
using System.Linq;

namespace PCC.Core
{
    /// <summary>A side's playing XI in batting order, plus captain and keeper.</summary>
    public sealed class TeamSheet
    {
        public TeamSheet(TeamInfo team, IReadOnlyList<Player> order, int captainId, int keeperId)
        {
            Team = team;
            Order = order;
            CaptainId = captainId;
            KeeperId = keeperId;
        }

        public TeamInfo Team { get; }
        public IReadOnlyList<Player> Order { get; }
        public int CaptainId { get; }
        public int KeeperId { get; }

        public Player ById(int id)
        {
            foreach (var p in Order)
            {
                if (p.Id == id) return p;
            }
            throw new ArgumentException("Player " + id + " not in " + Team.Id);
        }

        public bool Has(int id) => Order.Any(p => p.Id == id);

        public TeamSheet WithOrder(IReadOnlyList<Player> order) => new TeamSheet(Team, order, CaptainId, KeeperId);

        /// <summary>Best XI for a team's squad (used by quick matches and the AI).</summary>
        public static TeamSheet Auto(TeamInfo team, IReadOnlyList<Player> squad)
        {
            var xi = Lineup.PickBestXI(squad);
            return new TeamSheet(team, xi, Lineup.ChooseCaptain(xi).Id, Lineup.ChooseKeeper(xi).Id);
        }
    }

    public static class Lineup
    {
        static double BatValue(Player p) => p.Bat * 0.85 + p.Power * 0.15;

        /// <summary>Human readable rule violation for the XI, or null when legal.</summary>
        public static string Problem(IReadOnlyList<Player> xi)
        {
            if (xi.Count != 11) return "Pick exactly 11 players (" + xi.Count + " selected)";
            if (xi.Count(p => p.Overseas) > League.MaxOverseasXI) return "Max " + League.MaxOverseasXI + " overseas players in the XI";
            if (!xi.Any(p => p.Role == Role.Wk)) return "Pick a wicketkeeper";
            return null;
        }

        static readonly double[] BatW = { 1.0, 1.0, 1.0, 0.95, 0.9, 0.8, 0.62, 0.38, 0.2, 0.1, 0.05 };
        static readonly double[] BowlW = { 1.0, 1.0, 1.0, 0.95, 0.9, 0.3 };

        public static double XiScore(IReadOnlyList<Player> xi)
        {
            if (xi.Count != 11) return -1e9;
            if (xi.Count(p => p.Overseas) > League.MaxOverseasXI) return -1e9;
            double s = 0;
            // A keeper is mandatory: huge penalty so the search always fits one in, even when the
            // squad's keepers are overseas and a bowler or batter has to make way.
            if (!xi.Any(p => p.Role == Role.Wk)) s -= 100000;
            var bats = xi.Select(BatValue).OrderByDescending(v => v).ToList();
            for (int i = 0; i < 11; i++) s += bats[i] * BatW[i];
            var bowls = xi.Where(p => p.BowlStyle != BowlStyle.None).Select(p => (double)p.Bowl).OrderByDescending(v => v).ToList();
            for (int i = 0; i < Math.Min(6, bowls.Count); i++) s += bowls[i] * BowlW[i];
            int options = xi.Count(p => p.IsBowlingOption);
            if (options < 5) s -= (5 - options) * 50;
            if (xi.Count(p => p.IsPace && p.Bowl >= 62) < 2) s -= 30;
            if (xi.Count(p => p.IsSpin && p.Bowl >= 62) < 1) s -= 14;
            return s;
        }

        /// <summary>Greedy start + swap-based local search for the strongest legal XI.</summary>
        public static List<Player> PickBestXI(IReadOnlyList<Player> squad)
        {
            if (squad.Count <= 11) return OrderBatting(squad.ToList());
            var pool = squad.OrderByDescending(p => p.Overall).ToList();
            var xi = new List<Player>();
            int os = 0;
            foreach (var p in pool)
            {
                if (xi.Count == 11) break;
                if (p.Overseas && os >= League.MaxOverseasXI) continue;
                xi.Add(p);
                if (p.Overseas) os++;
            }
            double best = XiScore(xi);
            for (int pass = 0; pass < 12; pass++)
            {
                bool improved = false;
                for (int i = 0; i < xi.Count; i++)
                {
                    foreach (var cand in pool)
                    {
                        if (xi.Contains(cand)) continue;
                        var trial = new List<Player>(xi) { [i] = cand };
                        double sc = XiScore(trial);
                        if (sc > best + 0.01)
                        {
                            xi[i] = cand;
                            best = sc;
                            improved = true;
                        }
                    }
                }
                if (!improved) break;
            }
            return OrderBatting(xi);
        }

        /// <summary>Top order by batting skill, finishers in the middle, bowlers in the tail.</summary>
        public static List<Player> OrderBatting(List<Player> xi)
        {
            double Key(Player p) => BatValue(p) - (p.Role == Role.Bowl ? 40 : 0);
            var sorted = xi.OrderByDescending(Key).ToList();
            for (int i = 0; i < Math.Min(3, sorted.Count); i++)
            {
                var p = sorted[i];
                if (p.Power >= 92 && p.Bat < 80 && sorted.Count > 6)
                {
                    sorted.RemoveAt(i);
                    sorted.Insert(5, p);
                }
            }
            return sorted;
        }

        public static Player ChooseCaptain(IReadOnlyList<Player> xi)
        {
            var locals = xi.Where(p => !p.Overseas).OrderByDescending(p => p.Overall).ToList();
            return locals.Count > 0 ? locals[0] : xi.OrderByDescending(p => p.Overall).First();
        }

        public static Player ChooseKeeper(IReadOnlyList<Player> xi)
        {
            var keepers = xi.Where(p => p.Role == Role.Wk).OrderByDescending(p => p.Bat).ToList();
            return keepers.Count > 0 ? keepers[0] : xi.OrderByDescending(p => p.Bat).First();
        }

        public static int BatRating(IReadOnlyList<Player> xi)
        {
            var top = xi.Select(BatValue).OrderByDescending(v => v).Take(7).ToList();
            return top.Count == 0 ? 0 : (int)Math.Round(top.Average());
        }

        public static int BowlRating(IReadOnlyList<Player> xi)
        {
            var top = xi.Select(p => (double)p.Bowl).OrderByDescending(v => v).Take(5).ToList();
            return top.Count == 0 ? 0 : (int)Math.Round(top.Average());
        }

        public static int TeamRating(IReadOnlyList<Player> xi) => (int)Math.Round(BatRating(xi) * 0.52 + BowlRating(xi) * 0.48);
    }
}
