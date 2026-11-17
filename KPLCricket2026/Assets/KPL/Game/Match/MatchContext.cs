using System;
using System.Collections.Generic;
using System.Linq;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>What kind of match is being played and what to do with the result.</summary>
    public sealed class MatchContext
    {
        public MatchKind Kind;
        public MatchSetup Setup;
        public int FixtureNo = -1;
        public ChallengeDef Challenge;
        public string UserId => Setup.UserTeamId;
        public bool Practice => Kind == MatchKind.Practice;
        /// <summary>Nets: the user only bats (true) or only bowls (false).</summary>
        public bool NetsBat = true;
    }

    /// <summary>Builds team sheets and match setups for every mode, applying the user's upgrades.</summary>
    public static class Matches
    {
        public static Difficulty Diff => (Difficulty)SaveSystem.Data.Settings.Difficulty;

        /// <summary>The user's team with their chosen XI / batting order and all upgrades applied.</summary>
        public static TeamSheet UserSheet(TeamDef t)
        {
            var save = SaveSystem.Data;
            var squad = PlayerDb.Squad(t.Id);
            var pick = save.Squad(t.Id);
            List<Player> xi = null;
            if (pick != null && pick.Order.Count == 11)
            {
                xi = pick.Order.Select(id => squad.FirstOrDefault(p => p.Id == id)).Where(p => p != null).ToList();
                if (xi.Count != 11 || xi.Count(p => p.Overseas) > League.MaxOverseasXI) xi = null;
            }
            xi = xi ?? TeamSheet.AutoXI(squad);
            var sheet = TeamSheet.Create(t, xi);
            if (pick != null && xi.Any(p => p.Id == pick.Captain)) sheet.CaptainId = pick.Captain;
            return Economy.ApplyUserBoosts(sheet, save);
        }

        public static TeamSheet AiSheet(TeamDef t) => TeamSheet.Default(t);

        static long Seed() => (DateTime.Now.Ticks ^ UnityEngine.Random.Range(0, int.MaxValue)) & 0x7FFFFFFFFFFF;

        public static MatchContext Quick(TeamDef user, TeamDef opp, int overs, bool night)
        {
            var setup = new MatchSetup
            {
                Home = UserSheet(user), Away = AiSheet(opp), Overs = overs, Venue = user, Seed = Seed(), Difficulty = Diff, UserTeamId = user.Id, Night = night,
                Title = "QUICK MATCH  •  " + overs + " OVERS",
            };
            return new MatchContext { Kind = MatchKind.Quick, Setup = setup };
        }

        public static MatchContext Tournament(Tournament t, Fixture f)
        {
            var home = Teams.ById(f.Home);
            var away = Teams.ById(f.Away);
            bool userHome = f.Home == t.UserTeam;
            var stage = (TourStage)f.Stage;
            // Most league games are day games; every third one and all playoffs are under lights.
            bool night = stage != TourStage.League || f.No % 3 == 0;
            var setup = new MatchSetup
            {
                Home = userHome ? UserSheet(home) : AiSheet(home),
                Away = userHome ? AiSheet(away) : UserSheet(away),
                Overs = t.Overs, Venue = stage == TourStage.Final ? Teams.ById("GUJ") : home, Seed = Seed(), Difficulty = (Difficulty)t.DifficultyLevel,
                UserTeamId = t.UserTeam, Night = night,
                Title = "KPL " + League.Year + "  •  " + (stage == TourStage.League ? "MATCH " + f.No : stage.Label()),
            };
            return new MatchContext { Kind = MatchKind.Tournament, Setup = setup, FixtureNo = f.No };
        }

        public static MatchContext Challenge(ChallengeDef c)
        {
            var fav = Teams.ById(SaveSystem.Data.Fav);
            var opp = Challenges.Opponent(c, fav.Id);
            var setup = Challenges.Setup(c, UserSheet(fav), AiSheet(opp), Seed(), Diff);
            return new MatchContext { Kind = MatchKind.Challenge, Setup = setup, Challenge = c };
        }

        /// <summary>Practice nets: open-ended batting or bowling against the favourite team's rivals.</summary>
        public static MatchContext Nets(bool bat)
        {
            var fav = Teams.ById(SaveSystem.Data.Fav);
            var opp = Teams.All[(Teams.IndexOf(fav.Id) + 5) % Teams.All.Count];
            var setup = new MatchSetup
            {
                Home = UserSheet(fav), Away = AiSheet(opp), Overs = 5, Venue = fav, Seed = Seed(), Difficulty = Diff, UserTeamId = fav.Id,
                Night = GameApp.ResolveNight(SaveSystem.Data.Settings.MatchTime), Title = "NETS  •  " + (bat ? "BATTING" : "BOWLING") + " PRACTICE",
                Scenario = new Scenario { BattingTeamId = bat ? fav.Id : opp.Id, Balls = 600, Wickets = 10, Target = null },
            };
            return new MatchContext { Kind = MatchKind.Practice, Setup = setup, NetsBat = bat };
        }
    }
}
