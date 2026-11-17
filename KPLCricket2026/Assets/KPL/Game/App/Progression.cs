using System;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>What a finished match earned, for the result and rewards screens.</summary>
    public sealed class RecordSummary
    {
        public bool Won, Tie;
        public readonly List<(string Label, int Coins)> Coins = new List<(string, int)>();
        public int Total;
        public readonly List<AchievementDef> NewAchievements = new List<AchievementDef>();
        public int StarsBefore, StarsAfter;
        public string Note = "";
        public bool Champion, SeasonOver;
    }

    public static class Progression
    {
        public static RecordSummary Record(MatchContext ctx, MatchState m, int userCatches)
        {
            var s = SaveSystem.Data;
            var sum = new RecordSummary();
            string user = ctx.UserId;
            sum.Won = m.Result != null && m.Result.WinnerId == user;
            sum.Tie = m.Result != null && m.Result.WinnerId == null;
            if (ctx.Kind == MatchKind.Practice)
            {
                sum.Note = "Practice session: no rewards, but your timing just got better.";
                return sum;
            }
            switch (ctx.Kind)
            {
                case MatchKind.Quick:
                    sum.Coins.AddRange(Economy.MatchReward(m, user, MatchKind.Quick));
                    if (sum.Won) s.Stats.QuickWins++;
                    break;
                case MatchKind.Tournament:
                    RecordTournament(ctx, m, s, sum);
                    break;
                case MatchKind.Challenge:
                {
                    var c = ctx.Challenge;
                    int idx = c.Id - 1;
                    int stars = c.Evaluate(m, user);
                    sum.StarsBefore = s.ChallengeStars[idx];
                    sum.StarsAfter = Math.Max(stars, sum.StarsBefore);
                    sum.Won = stars > 0;
                    int reward = Economy.ChallengeReward(c, sum.StarsBefore, stars);
                    if (reward > 0) sum.Coins.Add((stars > sum.StarsBefore ? "New stars (" + stars + "★)" : "Challenge", reward));
                    if (stars > sum.StarsBefore) s.ChallengeStars[idx] = stars;
                    sum.Note = stars > 0 ? c.Title + ": " + stars + " star" + (stars > 1 ? "s" : "") : "Challenge not completed - try again!";
                    break;
                }
            }
            Career.Record(s, m, user, userCatches);
            sum.Total = sum.Coins.Sum(c => c.Coins);
            Economy.Grant(s, sum.Total);
            sum.NewAchievements.AddRange(Achievements.Check(s));
            SaveSystem.Save();
            return sum;
        }

        static void RecordTournament(MatchContext ctx, MatchState m, SaveData s, RecordSummary sum)
        {
            var t = s.Tour;
            var f = t.Fixtures.FirstOrDefault(x => x.No == ctx.FixtureNo);
            if (f == null || f.Played) return;
            var stageBefore = t.StageEnum;
            t.Record(f, m, true);
            sum.Coins.AddRange(Economy.MatchReward(m, t.UserTeam, MatchKind.Tournament, f.Stage));
            AfterTournamentProgress(t, s, sum, stageBefore);
            SimAhead(t);
            AfterTournamentProgress(t, s, sum, stageBefore);
        }

        /// <summary>Plays out AI fixtures up to the user's next game.</summary>
        public static int SimAhead(Tournament t)
        {
            var rng = new System.Random((int)(t.Seed & 0x7FFFFFFF) + t.SimCounter * 7919);
            int n = t.SimUntilUser(fx =>
            {
                t.SimCounter++;
                return BallEngine.QuickSim(new MatchSetup
                {
                    Home = TeamSheet.Default(Teams.ById(fx.Home)), Away = TeamSheet.Default(Teams.ById(fx.Away)), Overs = t.Overs,
                    Venue = Teams.ById(fx.Home), Seed = rng.Next(),
                });
            });
            return n;
        }

        /// <summary>Simulates the user's own next fixture (with their upgraded squad).</summary>
        public static MatchState SimUserFixture(Tournament t, Fixture f)
        {
            var home = Teams.ById(f.Home);
            var away = Teams.ById(f.Away);
            var setup = new MatchSetup
            {
                Home = f.Home == t.UserTeam ? Matches.UserSheet(home) : TeamSheet.Default(home),
                Away = f.Away == t.UserTeam ? Matches.UserSheet(away) : TeamSheet.Default(away),
                Overs = t.Overs, Venue = home, Seed = DateTime.Now.Ticks & 0x7FFFFFFF,
            };
            return BallEngine.QuickSim(setup);
        }

        public static void AfterTournamentProgress(Tournament t, SaveData s, RecordSummary sum, TourStage before)
        {
            if (before == TourStage.League && t.StageEnum != TourStage.League && !t.UserEliminated && sum != null && !sum.Note.Contains("playoffs"))
            {
                s.Stats.Playoffs++;
                sum.Note = "Qualified for the playoffs!";
                sum.Coins.Add(("Playoff qualification", 800));
            }
            if (t.Complete && !sum.SeasonOver)
            {
                sum.SeasonOver = true;
                s.Stats.TourPlayed++;
                if (t.Champion == t.UserTeam)
                {
                    sum.Champion = true;
                    s.Stats.TourWon++;
                    if (t.Format == TourFormat.Full) s.Stats.TourFullWon++;
                    int bonus = t.Format == TourFormat.Full ? 6000 : 3000;
                    sum.Coins.Add(("KPL CHAMPIONS!", bonus));
                    sum.Note = "Champions of KPL " + League.Year + "!";
                }
                else if (t.RunnerUp == t.UserTeam)
                {
                    sum.Coins.Add(("Runners-up", 1200));
                    sum.Note = "Runners-up in KPL " + League.Year;
                }
                else sum.Note = Teams.ById(t.Champion).Name + " are the KPL " + League.Year + " champions.";
            }
        }
    }
}
