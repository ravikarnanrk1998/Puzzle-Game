using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    public enum ChallengeGoal { Chase, ScoreRuns, HitSixes, HitBoundaries, Survive, Defend, TakeWickets, Economy, WinMatch }

    /// <summary>One of the 50 challenge levels. The user's favourite team takes part.</summary>
    public sealed class ChallengeDef
    {
        public int Id;
        public int Chapter;
        public string Title, Brief;
        public bool UserBats;
        public ChallengeGoal Goal;
        public int Balls, Wickets;
        public int Target;
        /// <summary>Thresholds for 1, 2 and 3 stars (meaning depends on the goal).</summary>
        public int[] Stars;
        /// <summary>"spin", "pace" or null: which bowlers are used.</summary>
        public string Bowlers;
        public int Overs;
        public string Opponent;
        public string Venue;
        public bool Night;
        public int Reward;
        public Difficulty MinDifficulty = Difficulty.Easy;

        public string GoalText
        {
            get
            {
                switch (Goal)
                {
                    case ChallengeGoal.Chase: return "Chase " + Target + " off " + Balls + " balls (" + Wickets + " wickets in hand)";
                    case ChallengeGoal.ScoreRuns: return "Score " + Stars[0] + "+ runs off " + Balls + " balls";
                    case ChallengeGoal.HitSixes: return "Hit " + Stars[0] + " six" + (Stars[0] > 1 ? "es" : "") + " in " + Balls + " balls";
                    case ChallengeGoal.HitBoundaries: return "Hit " + Stars[0] + " boundar" + (Stars[0] > 1 ? "ies" : "y") + " in " + Balls + " balls";
                    case ChallengeGoal.Survive: return "Bat " + Balls + " balls without losing a wicket";
                    case ChallengeGoal.Defend: return "Defend " + (Target - 1) + " off " + Balls + " balls (" + Wickets + " wickets left)";
                    case ChallengeGoal.TakeWickets: return "Take " + Stars[0] + " wicket" + (Stars[0] > 1 ? "s" : "") + " in " + Balls + " balls";
                    case ChallengeGoal.Economy: return "Concede " + Stars[0] + " or fewer off " + Balls + " balls";
                    default: return "Win a " + Overs + "-over match";
                }
            }
        }

        public string StarText(int star)
        {
            int i = star - 1;
            switch (Goal)
            {
                case ChallengeGoal.Chase: return star == 1 ? "Win the chase" : star == 2 ? "Win with " + Stars[1] + "+ balls to spare" : "Lose " + Stars[2] + " or fewer wickets";
                case ChallengeGoal.ScoreRuns: return "Score " + Stars[i] + "+ runs";
                case ChallengeGoal.HitSixes: return "Hit " + Stars[i] + " six" + (Stars[i] > 1 ? "es" : "");
                case ChallengeGoal.HitBoundaries: return "Hit " + Stars[i] + " boundar" + (Stars[i] > 1 ? "ies" : "y");
                case ChallengeGoal.Survive: return star == 1 ? "Don't lose a wicket" : "...and score " + Stars[i] + "+";
                case ChallengeGoal.Defend: return star == 1 ? "Defend the total" : star == 2 ? "Win by " + Stars[1] + "+ runs" : "Take " + Stars[2] + "+ wickets";
                case ChallengeGoal.TakeWickets: return "Take " + Stars[i] + " wicket" + (Stars[i] > 1 ? "s" : "");
                case ChallengeGoal.Economy: return "Concede " + Stars[i] + " or fewer";
                default: return star == 1 ? "Win the match" : star == 2 ? "Win by " + Stars[1] + "+ runs or 4+ wickets" : "Win by " + Stars[2] + "+ runs or 7+ wickets";
            }
        }

        /// <summary>Scores a finished challenge match: 0-3 stars.</summary>
        public int Evaluate(MatchState m, string userId)
        {
            if (!m.Finished) return 0;
            if (Goal == ChallengeGoal.WinMatch)
            {
                var r = m.Result;
                if (r.WinnerId != userId) return 0;
                int s = 1;
                bool byRuns = r.MarginRuns != null, byW = r.MarginWkts != null;
                if ((byRuns && r.MarginRuns >= Stars[1]) || (byW && r.MarginWkts >= 4)) s = 2;
                if ((byRuns && r.MarginRuns >= Stars[2]) || (byW && r.MarginWkts >= 7)) s = 3;
                return s;
            }
            var inn = m.InningsList[0];
            int fours = inn.BatCards.Sum(c => c.Fours), sixes = inn.BatCards.Sum(c => c.Sixes);
            int Count(int v) => v >= Stars[2] ? 3 : v >= Stars[1] ? 2 : v >= Stars[0] ? 1 : 0;
            switch (Goal)
            {
                case ChallengeGoal.Chase:
                    if (!inn.Chased) return 0;
                    return 1 + (inn.BallsLeft >= Stars[1] ? 1 : 0) + (inn.Wkts <= Stars[2] ? 1 : 0);
                case ChallengeGoal.ScoreRuns: return Count(inn.Runs);
                case ChallengeGoal.HitSixes: return Count(sixes);
                case ChallengeGoal.HitBoundaries: return Count(fours + sixes);
                case ChallengeGoal.Survive:
                    if (inn.Wkts > 0 || inn.Balls < Balls) return 0;
                    return 1 + (inn.Runs >= Stars[1] ? 1 : 0) + (inn.Runs >= Stars[2] ? 1 : 0);
                case ChallengeGoal.Defend:
                    if (inn.Chased || m.Result.Tie) return 0;
                    int margin = inn.RunsNeeded - 1;
                    return 1 + (margin >= Stars[1] ? 1 : 0) + (inn.Wkts >= Stars[2] ? 1 : 0);
                case ChallengeGoal.TakeWickets: return Count(inn.Wkts);
                case ChallengeGoal.Economy: return inn.Runs <= Stars[2] ? 3 : inn.Runs <= Stars[1] ? 2 : inn.Runs <= Stars[0] ? 1 : 0;
            }
            return 0;
        }

        public bool IsScenario => Goal != ChallengeGoal.WinMatch;
    }

    public static class Challenges
    {
        public static readonly string[] ChapterNames = { "FIRST STEPS", "SEAM & SWING", "SPIN WEB", "DEATH OVERS", "ROAD TO THE FINAL" };
        public static readonly string[] ChapterBlurbs =
        {
            "Learn to time it, place it and clear the rope.",
            "Take the new ball: line, length, swing and yorkers.",
            "Turn it square - and learn to sweep and loft the spinners.",
            "Finish chases and hold your nerve with the ball at the death.",
            "The toughest situations in the league. Win them all.",
        };

        static List<ChallengeDef> _all;

        public static IReadOnlyList<ChallengeDef> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static ChallengeDef ById(int id) => All.FirstOrDefault(c => c.Id == id);

        static void Build()
        {
            _all = new List<ChallengeDef>();
            int id = 1;
            void Add(int ch, string title, string brief, bool bats, ChallengeGoal g, int balls, int wkts, int target, int[] stars, string bowlers = null,
                int overs = 0, string opp = null, bool night = false)
            {
                _all.Add(new ChallengeDef
                {
                    Id = id, Chapter = ch, Title = title, Brief = brief, UserBats = bats, Goal = g, Balls = balls, Wickets = wkts, Target = target,
                    Stars = stars, Bowlers = bowlers, Overs = overs, Opponent = opp, Night = night || id % 3 == 0, Reward = 60 + ch * 40 + (g == ChallengeGoal.WinMatch ? 150 : 0),
                });
                id++;
            }
            const bool BAT = true, BOWL = false;
            // Chapter 1: first steps (batting).
            Add(0, "Off the Mark", "A gentle start: find the gaps and keep the scoreboard moving.", BAT, ChallengeGoal.ScoreRuns, 6, 1, 0, new[] { 6, 10, 14 });
            Add(0, "Find the Rope", "Time it sweetly and beat the field for four.", BAT, ChallengeGoal.HitBoundaries, 6, 1, 0, new[] { 1, 2, 3 });
            Add(0, "Into the Stands", "Pick the right ball and loft it over the ropes.", BAT, ChallengeGoal.HitSixes, 6, 1, 0, new[] { 1, 2, 3 });
            Add(0, "Survival Test", "The new ball is swinging. See off two overs.", BAT, ChallengeGoal.Survive, 12, 1, 0, new[] { 0, 10, 18 }, "pace");
            Add(0, "First Chase", "12 to win off 12. Stay calm.", BAT, ChallengeGoal.Chase, 12, 2, 12, new[] { 0, 3, 0 });
            Add(0, "Powerplay Burst", "Fielders are up. Make the most of it.", BAT, ChallengeGoal.ScoreRuns, 12, 2, 0, new[] { 15, 21, 28 });
            Add(0, "Twenty off Twelve", "A proper chase now.", BAT, ChallengeGoal.Chase, 12, 3, 20, new[] { 0, 2, 1 });
            Add(0, "Spin Test", "Two overs of spin. Use your feet and the sweep.", BAT, ChallengeGoal.ScoreRuns, 12, 2, 0, new[] { 12, 18, 24 }, "spin");
            Add(0, "Pace Test", "Fast bowlers on a quick pitch. Punish anything short.", BAT, ChallengeGoal.HitBoundaries, 12, 2, 0, new[] { 2, 3, 5 }, "pace");
            Add(0, "Chapter Final: Mini Match", "Win a two-over match from the toss.", BAT, ChallengeGoal.WinMatch, 0, 10, 0, new[] { 0, 8, 16 }, null, 2);
            // Chapter 2: seam and swing (bowling).
            Add(1, "First Over", "Hit your length and keep it tight.", BOWL, ChallengeGoal.Economy, 6, 3, 0, new[] { 12, 9, 6 }, "pace");
            Add(1, "Breakthrough", "Swing it away and find the edge.", BOWL, ChallengeGoal.TakeWickets, 12, 10, 0, new[] { 1, 2, 3 }, "pace");
            Add(1, "Defend Fifteen", "They need 15 off 12. Don't let them.", BOWL, ChallengeGoal.Defend, 12, 3, 16, new[] { 0, 4, 2 }, "pace");
            Add(1, "Dot-Ball Pressure", "Build pressure with dots.", BOWL, ChallengeGoal.Economy, 12, 10, 0, new[] { 14, 10, 7 }, "pace");
            Add(1, "Yorker Specialist", "10 to defend in the last over. Aim at the toes.", BOWL, ChallengeGoal.Defend, 6, 2, 11, new[] { 0, 4, 1 }, "pace");
            Add(1, "Strike Twice", "Two wickets in three overs.", BOWL, ChallengeGoal.TakeWickets, 18, 10, 0, new[] { 2, 3, 4 }, "pace");
            Add(1, "Defend Twenty-Two", "22 needed off 18 balls.", BOWL, ChallengeGoal.Defend, 18, 4, 23, new[] { 0, 5, 3 }, "pace");
            Add(1, "Tight Spell", "Three overs. Squeeze them.", BOWL, ChallengeGoal.Economy, 18, 10, 0, new[] { 20, 15, 11 }, "pace");
            Add(1, "Final-Over Nerves", "Nine to defend. One over. Everything on it.", BOWL, ChallengeGoal.Defend, 6, 3, 10, new[] { 0, 3, 1 }, "pace");
            Add(1, "Chapter Final: Three-Over Clash", "Win a three-over match.", BOWL, ChallengeGoal.WinMatch, 0, 10, 0, new[] { 0, 10, 20 }, null, 3);
            // Chapter 3: spin.
            Add(2, "Sweep Away", "Two overs of spin. Sweep and loft.", BAT, ChallengeGoal.ScoreRuns, 12, 2, 0, new[] { 16, 22, 28 }, "spin");
            Add(2, "Flight and Guile", "Bowl spin and keep them quiet.", BOWL, ChallengeGoal.Economy, 12, 10, 0, new[] { 14, 10, 7 }, "spin");
            Add(2, "Down the Track", "Charge the spinners and clear the ropes.", BAT, ChallengeGoal.HitSixes, 12, 2, 0, new[] { 2, 3, 4 }, "spin");
            Add(2, "Wrong'un", "Use the variation - two wickets in three overs.", BOWL, ChallengeGoal.TakeWickets, 18, 10, 0, new[] { 2, 3, 4 }, "spin");
            Add(2, "Turning Track Chase", "30 off 18 against the spinners.", BAT, ChallengeGoal.Chase, 18, 4, 30, new[] { 0, 3, 2 }, "spin");
            Add(2, "Spin to Win", "18 to defend off 12 with spin.", BOWL, ChallengeGoal.Defend, 12, 4, 19, new[] { 0, 5, 2 }, "spin");
            Add(2, "Patience", "See off three overs of spin - and keep scoring.", BAT, ChallengeGoal.Survive, 18, 1, 0, new[] { 0, 15, 24 }, "spin");
            Add(2, "Web of Spin", "Three wickets in four overs.", BOWL, ChallengeGoal.TakeWickets, 24, 10, 0, new[] { 3, 4, 5 }, "spin");
            Add(2, "Forty off Twenty-Four", "A tricky chase against quality spin.", BAT, ChallengeGoal.Chase, 24, 4, 40, new[] { 0, 4, 2 }, "spin");
            Add(2, "Chapter Final: Dust Bowl", "Win a four-over match on a raging turner.", BAT, ChallengeGoal.WinMatch, 0, 10, 0, new[] { 0, 12, 24 }, null, 4, "CHE");
            // Chapter 4: death overs.
            Add(3, "Last-Over Blitz", "Six balls left. Make them count.", BAT, ChallengeGoal.ScoreRuns, 6, 2, 0, new[] { 12, 18, 24 });
            Add(3, "Hold Your Nerve", "Defend 12 in the final over.", BOWL, ChallengeGoal.Defend, 6, 3, 13, new[] { 0, 5, 2 }, "pace");
            Add(3, "Sixteen to Win", "16 off the last over. Two wickets left.", BAT, ChallengeGoal.Chase, 6, 2, 16, new[] { 0, 1, 0 });
            Add(3, "Death Yorkers", "Defend 8 off 6.", BOWL, ChallengeGoal.Defend, 6, 4, 9, new[] { 0, 4, 2 }, "pace");
            Add(3, "Hat-Trick of Sixes", "Three sixes in two overs.", BAT, ChallengeGoal.HitSixes, 12, 3, 0, new[] { 3, 4, 5 });
            Add(3, "Break the Partnership", "They need 30 off 12. Take two wickets.", BOWL, ChallengeGoal.TakeWickets, 12, 6, 0, new[] { 2, 3, 4 }, "pace");
            Add(3, "Thirty-Six off Eighteen", "Three overs, three wickets in hand.", BAT, ChallengeGoal.Chase, 18, 3, 36, new[] { 0, 3, 1 });
            Add(3, "Close It Out", "25 to defend off 18.", BOWL, ChallengeGoal.Defend, 18, 5, 26, new[] { 0, 6, 3 });
            Add(3, "Fifty off Twenty-Four", "Four overs, fifty runs.", BAT, ChallengeGoal.Chase, 24, 4, 50, new[] { 0, 3, 2 });
            Add(3, "Chapter Final: Night Thriller", "Win a five-over match under lights.", BOWL, ChallengeGoal.WinMatch, 0, 10, 0, new[] { 0, 12, 25 }, null, 5, "KOL", true);
            // Chapter 5: road to the final.
            Add(4, "Big Chase", "60 off 30 with five wickets.", BAT, ChallengeGoal.Chase, 30, 5, 60, new[] { 0, 4, 2 });
            Add(4, "Squeeze", "Defend 30 off 24.", BOWL, ChallengeGoal.Defend, 24, 6, 31, new[] { 0, 6, 3 });
            Add(4, "Onslaught", "Forty off three overs.", BAT, ChallengeGoal.ScoreRuns, 18, 3, 0, new[] { 40, 48, 56 });
            Add(4, "Bowl Them Out", "Four wickets in five overs.", BOWL, ChallengeGoal.TakeWickets, 30, 10, 0, new[] { 4, 5, 6 });
            Add(4, "Twenty-Four off Twelve", "Two wickets left. Go.", BAT, ChallengeGoal.Chase, 12, 2, 24, new[] { 0, 2, 0 });
            Add(4, "Six to Defend", "Six runs. One over. One wicket left for them.", BOWL, ChallengeGoal.Defend, 6, 1, 7, new[] { 0, 3, 1 }, "pace");
            Add(4, "Super Over: Batting", "Two wickets, six balls. Set a big target.", BAT, ChallengeGoal.ScoreRuns, 6, 2, 0, new[] { 12, 16, 20 });
            Add(4, "Super Over: Bowling", "Defend 14 in the Super Over.", BOWL, ChallengeGoal.Defend, 6, 2, 15, new[] { 0, 6, 2 }, "pace");
            Add(4, "The Eighty Chase", "80 off 36 with five wickets.", BAT, ChallengeGoal.Chase, 36, 5, 80, new[] { 0, 4, 3 });
            Add(4, "GRAND FINAL", "The KPL final under the lights. Win it.", BAT, ChallengeGoal.WinMatch, 0, 10, 0, new[] { 0, 15, 30 }, null, 5, "GUJ", true);
            foreach (var c in _all.Where(c => c.Chapter == 4)) c.MinDifficulty = Difficulty.Medium;
        }

        /// <summary>The opponent for a challenge: fixed for finals, otherwise rotated, never the user's team.</summary>
        public static TeamDef Opponent(ChallengeDef c, string userId)
        {
            if (c.Opponent != null && c.Opponent != userId) return Teams.ById(c.Opponent);
            int i = (c.Id * 3 + 1) % Teams.All.Count;
            if (Teams.All[i].Id == userId) i = (i + 1) % Teams.All.Count;
            return Teams.All[i];
        }

        /// <summary>Builds the match for a challenge. Batting scenarios put the user's team in; bowling ones the opponent.</summary>
        public static MatchSetup Setup(ChallengeDef c, TeamSheet user, TeamSheet opp, long seed, Difficulty d)
        {
            var venueTeam = c.Opponent != null ? Teams.ById(c.Opponent) : user.Team;
            var setup = new MatchSetup
            {
                Home = user, Away = opp, Venue = venueTeam, Seed = seed, Difficulty = (Difficulty)Math.Max((int)d, (int)c.MinDifficulty),
                UserTeamId = user.Team.Id, Night = c.Night, Overs = c.Overs > 0 ? c.Overs : Math.Max(1, (c.Balls + 5) / 6),
                Title = "CHALLENGE " + c.Id + " • " + c.Title.ToUpperInvariant(),
            };
            if (c.IsScenario)
            {
                setup.Scenario = new Scenario
                {
                    BattingTeamId = c.UserBats ? user.Team.Id : opp.Team.Id,
                    Balls = c.Balls,
                    Wickets = c.Wickets,
                    Target = c.Goal == ChallengeGoal.Chase || c.Goal == ChallengeGoal.Defend ? c.Target : (int?)null,
                    BowlerFilter = c.Bowlers,
                };
                // "Need 30 off 12, take two wickets": the AI chases a target in TakeWickets scenarios with a set chase.
                if (c.Goal == ChallengeGoal.TakeWickets && c.Id == 36) setup.Scenario.Target = 31;
            }
            return setup;
        }
    }
}
