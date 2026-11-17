using System;
using System.Collections.Generic;
using System.Linq;

namespace KPL.Core
{
    [Serializable]
    public sealed class SettingsData
    {
        /// <summary>-1 = not chosen yet (auto-detect on first launch), else 0 LOW .. 3 ULTRA.</summary>
        public int Graphics = -1;
        public bool Fps60;
        public float Music = 0.55f, Sfx = 0.9f;
        public bool Vibrate = true;
        public bool Commentary = true;
        /// <summary>0 = stick + buttons, 1 = swipe.</summary>
        public int Controls;
        /// <summary>Mirror the touch controls for left-handed players.</summary>
        public bool LeftHanded;
        public int Difficulty = 1;
        public bool TimingAssist = true;
        /// <summary>Touch-latency calibration in ms (positive = the game expects your tap later).</summary>
        public int TimingOffset;
        /// <summary>0 = broadcast (behind the bowler), 1 = behind the batter.</summary>
        public int Camera;
        public bool BallTrail = true;
        /// <summary>0 = auto, 1 = day, 2 = night.</summary>
        public int MatchTime;
        /// <summary>Tap prompts for catches when your side is fielding.</summary>
        public bool CatchPrompts = true;
    }

    [Serializable]
    public sealed class PlayerLevel
    {
        public int Id;
        public int Level;
    }

    [Serializable]
    public sealed class SquadPick
    {
        public string Team;
        public List<int> Order = new List<int>();
        public int Captain;
    }

    [Serializable]
    public sealed class AchState
    {
        public string Id;
        public bool Unlocked, Claimed;
    }

    [Serializable]
    public sealed class CareerStats
    {
        public int Matches, Wins, Losses, Ties;
        public int Runs, Balls, Fours, Sixes, HighScore, Fifties, Hundreds, Ducks, Innings, NotOuts;
        public int Wkts, BallsBowled, RunsConceded, BestW, BestR = 999, Maidens, HatTricks, FiveFors;
        public int Catches, RunOuts;
        public int TourPlayed, TourWon, TourFullWon, Playoffs;
        public int QuickWins, ChasesWon, DefendsWon, SuperOverWins;
        public int HighestTotal, BiggestSix;
        public int Challenges;

        public double BatAverage => Innings - NotOuts <= 0 ? Runs : Runs / (double)(Innings - NotOuts);
        public double StrikeRate => Balls == 0 ? 0 : Runs * 100.0 / Balls;
        public double Economy => BallsBowled == 0 ? 0 : RunsConceded * 6.0 / BallsBowled;
        public double WinPct => Matches == 0 ? 0 : Wins * 100.0 / Matches;
        public string Best => BestW == 0 && BestR == 999 ? "-" : BestW + "/" + BestR;
    }

    /// <summary>Everything the game saves. Serialized with JsonUtility, so only public fields.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public long Created;
        public string Fav = "";
        public int Coins = 750;
        public int CoinsEarned;
        public SettingsData Settings = new SettingsData();
        public int LastDailyDay = -1;
        public int DailyStreak;
        public List<int> ChallengeStars = new List<int>();
        public List<string> Owned = new List<string> { "bat_club", "spk_club", "glv_club" };
        public string Bat = "bat_club", Spikes = "spk_club", Gloves = "glv_club";
        public int CoachBat, CoachBowl, CoachField;
        public List<PlayerLevel> Training = new List<PlayerLevel>();
        public List<SquadPick> Squads = new List<SquadPick>();
        public CareerStats Stats = new CareerStats();
        public List<AchState> Ach = new List<AchState>();
        public bool HasTour;
        public Tournament Tour = new Tournament();
        public bool TutorialDone;
        public int LastSeenLevel;

        /// <summary>Fills in anything missing after loading an older or damaged save.</summary>
        public void Repair()
        {
            if (Settings == null) Settings = new SettingsData();
            if (ChallengeStars == null) ChallengeStars = new List<int>();
            while (ChallengeStars.Count < Challenges.All.Count) ChallengeStars.Add(0);
            if (Owned == null) Owned = new List<string>();
            foreach (var basic in new[] { "bat_club", "spk_club", "glv_club" })
            {
                if (!Owned.Contains(basic)) Owned.Add(basic);
            }
            if (Shop.ById(Bat) == null || !Owned.Contains(Bat)) Bat = "bat_club";
            if (Shop.ById(Spikes) == null || !Owned.Contains(Spikes)) Spikes = "spk_club";
            if (Shop.ById(Gloves) == null || !Owned.Contains(Gloves)) Gloves = "glv_club";
            if (Training == null) Training = new List<PlayerLevel>();
            if (Squads == null) Squads = new List<SquadPick>();
            if (Stats == null) Stats = new CareerStats();
            if (Ach == null) Ach = new List<AchState>();
            foreach (var a in Achievements.All)
            {
                if (!Ach.Any(x => x.Id == a.Id)) Ach.Add(new AchState { Id = a.Id });
            }
            if (Tour == null) Tour = new Tournament();
            if (HasTour && (Tour.Fixtures == null || Tour.Fixtures.Count == 0 || Tour.Table == null || Tour.Table.Count != Teams.All.Count)) HasTour = false;
            if (Tour.Stats == null) Tour.Stats = new List<SeasonStat>();
            Coins = Math.Max(0, Coins);
            CoachBat = Math.Max(0, Math.Min(Economy.MaxCoach, CoachBat));
            CoachBowl = Math.Max(0, Math.Min(Economy.MaxCoach, CoachBowl));
            CoachField = Math.Max(0, Math.Min(Economy.MaxCoach, CoachField));
            if (string.IsNullOrEmpty(Fav) || Teams.IndexOf(Fav) < 0) Fav = "";
            Settings.Difficulty = Math.Max(0, Math.Min(3, Settings.Difficulty));
            Settings.Graphics = Math.Max(-1, Math.Min(3, Settings.Graphics));
            Version = CurrentVersion;
        }

        public int Level(int playerId)
        {
            foreach (var t in Training)
            {
                if (t.Id == playerId) return t.Level;
            }
            return 0;
        }

        public void SetLevel(int playerId, int level)
        {
            foreach (var t in Training)
            {
                if (t.Id == playerId)
                {
                    t.Level = level;
                    return;
                }
            }
            Training.Add(new PlayerLevel { Id = playerId, Level = level });
        }

        public SquadPick Squad(string team) => Squads.FirstOrDefault(s => s.Team == team);

        public int TotalStars => ChallengeStars.Sum();
    }

    public enum ItemKind { Bat, Spikes, Gloves }

    public sealed class ShopItem
    {
        public string Id, Name, Blurb;
        public ItemKind Kind;
        public int Price, Tier;
        public double Timing, Power, Accuracy, Catch;
    }

    public static class Shop
    {
        public static readonly IReadOnlyList<ShopItem> Items = new[]
        {
            new ShopItem { Id = "bat_club", Kind = ItemKind.Bat, Name = "Club Willow", Blurb = "A dependable starter bat.", Price = 0, Tier = 0 },
            new ShopItem { Id = "bat_kashmir", Kind = ItemKind.Bat, Name = "Kashmir Classic", Blurb = "Light pick-up, slightly bigger sweet spot.", Price = 1200, Tier = 1, Timing = 0.05, Power = 2 },
            new ShopItem { Id = "bat_grade3", Kind = ItemKind.Bat, Name = "Willow Grade 3", Blurb = "Pressed English-style willow with real punch.", Price = 3500, Tier = 2, Timing = 0.08, Power = 4 },
            new ShopItem { Id = "bat_pro", Kind = ItemKind.Bat, Name = "Pro Grade 2", Blurb = "Thick edges and a mid-low sweet spot.", Price = 8000, Tier = 3, Timing = 0.12, Power = 6 },
            new ShopItem { Id = "bat_thunder", Kind = ItemKind.Bat, Name = "Thunder Edge", Blurb = "Power-hitter's bat. Mishits still carry.", Price = 15000, Tier = 4, Timing = 0.15, Power = 9 },
            new ShopItem { Id = "bat_legend", Kind = ItemKind.Bat, Name = "Legend Grade 1", Blurb = "Hand-picked willow. The best timing window in the game.", Price = 28000, Tier = 5, Timing = 0.2, Power = 12 },
            new ShopItem { Id = "spk_club", Kind = ItemKind.Spikes, Name = "Club Spikes", Blurb = "Standard bowling boots.", Price = 0, Tier = 0 },
            new ShopItem { Id = "spk_grip", Kind = ItemKind.Spikes, Name = "Grip Pro Spikes", Blurb = "Firmer landing: tighter line and length.", Price = 2500, Tier = 1, Accuracy = 0.04 },
            new ShopItem { Id = "spk_carbon", Kind = ItemKind.Spikes, Name = "Carbon Elite Spikes", Blurb = "A wider accuracy zone on the meter.", Price = 9000, Tier = 2, Accuracy = 0.08 },
            new ShopItem { Id = "spk_apex", Kind = ItemKind.Spikes, Name = "Apex Spikes", Blurb = "Pinpoint execution, ball after ball.", Price = 20000, Tier = 3, Accuracy = 0.12 },
            new ShopItem { Id = "glv_club", Kind = ItemKind.Gloves, Name = "Club Fielding", Blurb = "No bonus.", Price = 0, Tier = 0 },
            new ShopItem { Id = "glv_sure", Kind = ItemKind.Gloves, Name = "Sure-Grip Training", Blurb = "Catch prompts last longer; fewer drops.", Price = 2000, Tier = 1, Catch = 0.12 },
            new ShopItem { Id = "glv_safe", Kind = ItemKind.Gloves, Name = "Safe Hands Program", Blurb = "Your fielders hold more of the hard chances.", Price = 7000, Tier = 2, Catch = 0.22 },
            new ShopItem { Id = "glv_magnet", Kind = ItemKind.Gloves, Name = "Magnet Hands Elite", Blurb = "Elite catching drills for the whole side.", Price = 16000, Tier = 3, Catch = 0.32 },
        };

        public static ShopItem ById(string id) => Items.FirstOrDefault(i => i.Id == id);

        public enum BuyResult { Ok, AlreadyOwned, NotEnoughCoins, Unknown }

        /// <summary>Validates and performs a purchase.</summary>
        public static BuyResult Buy(SaveData s, string id)
        {
            var item = ById(id);
            if (item == null) return BuyResult.Unknown;
            if (s.Owned.Contains(id)) return BuyResult.AlreadyOwned;
            if (s.Coins < item.Price) return BuyResult.NotEnoughCoins;
            s.Coins -= item.Price;
            s.Owned.Add(id);
            Equip(s, id);
            return BuyResult.Ok;
        }

        public static bool Equip(SaveData s, string id)
        {
            var item = ById(id);
            if (item == null || !s.Owned.Contains(id)) return false;
            switch (item.Kind)
            {
                case ItemKind.Bat: s.Bat = id; break;
                case ItemKind.Spikes: s.Spikes = id; break;
                default: s.Gloves = id; break;
            }
            return true;
        }
    }

    public enum MatchKind { Quick, Tournament, Challenge, Practice }

    public static class Economy
    {
        public const int MaxCoach = 5;
        public const int MaxTraining = 5;
        public static readonly int[] DailyRewards = { 100, 150, 200, 300, 400, 500, 1000 };
        static readonly int[] CoachCosts = { 1500, 4000, 8000, 14000, 22000 };

        public static int CoachCost(int level) => level >= MaxCoach ? 0 : CoachCosts[level];
        public static int TrainingCost(int level) => level >= MaxTraining ? 0 : 500 + level * 450;

        public static bool BuyCoach(SaveData s, int which)
        {
            int lvl = which == 0 ? s.CoachBat : which == 1 ? s.CoachBowl : s.CoachField;
            int cost = CoachCost(lvl);
            if (lvl >= MaxCoach || s.Coins < cost) return false;
            s.Coins -= cost;
            if (which == 0) s.CoachBat++;
            else if (which == 1) s.CoachBowl++;
            else s.CoachField++;
            return true;
        }

        public static bool Train(SaveData s, Player p)
        {
            int lvl = s.Level(p.Id);
            int cost = TrainingCost(lvl);
            if (lvl >= MaxTraining || s.Coins < cost) return false;
            s.Coins -= cost;
            s.SetLevel(p.Id, lvl + 1);
            return true;
        }

        /// <summary>Rating increase for a trained player (applied to the user's team only).</summary>
        public static (int bat, int power, int bowl, int field) TrainingBoost(Player p, int level)
        {
            switch (p.Role)
            {
                case Role.Bowler: return (0, level / 2, level * 2, level / 2);
                case Role.AllRounder: return (level, level, level, level / 2);
                case Role.Keeper: return (level * 2, level, 0, level);
                default: return (level * 2, level * 2 - level / 2, 0, level / 2);
            }
        }

        /// <summary>All of the user's boosts: coaches, training and equipment.</summary>
        public static TeamSheet ApplyUserBoosts(TeamSheet sheet, SaveData s) => sheet.Boosted(p =>
        {
            var (b, pw, bw, f) = TrainingBoost(p, s.Level(p.Id));
            return (b + s.CoachBat, pw + s.CoachBat, bw + s.CoachBowl, f + s.CoachField * 2);
        });

        public static SideBoosts Boosts(SaveData s)
        {
            var bat = Shop.ById(s.Bat);
            var sp = Shop.ById(s.Spikes);
            var gl = Shop.ById(s.Gloves);
            return new SideBoosts
            {
                TimingBonus = bat?.Timing ?? 0,
                PowerBonus = bat?.Power ?? 0,
                AccuracyBonus = (sp?.Accuracy ?? 0) + s.CoachBowl * 0.008,
                CatchBonus = (gl?.Catch ?? 0) + s.CoachField * 0.03,
            };
        }

        static double DiffMul(Difficulty d) => d == Difficulty.Easy ? 0.8 : d == Difficulty.Medium ? 1.0 : d == Difficulty.Hard ? 1.3 : 1.6;

        /// <summary>Coins earned for a finished match, with a breakdown for the rewards screen.</summary>
        public static List<(string Label, int Coins)> MatchReward(MatchState m, string userId, MatchKind kind, int stage = 0)
        {
            var list = new List<(string, int)>();
            if (!m.Finished || kind == MatchKind.Practice || kind == MatchKind.Challenge) return list;
            bool won = m.Result.WinnerId == userId;
            int overs = m.Setup.Overs;
            double scale = (overs <= 2 ? 0.6 : overs <= 5 ? 1.0 : overs <= 10 ? 1.4 : 2.0) * DiffMul(m.Setup.Difficulty);
            if (kind == MatchKind.Tournament)
            {
                int baseWin = stage == 0 ? 300 : stage == (int)TourStage.Final ? 1500 : 700;
                list.Add((won ? (stage == 0 ? "League win" : "Playoff win") : "Match fee", (int)Math.Round((won ? baseWin : 100) * scale)));
            }
            else
            {
                list.Add((won ? "Victory" : m.Result.WinnerId == null ? "Tie" : "Match fee", (int)Math.Round((won ? 200 : 60) * scale)));
            }
            int fours = 0, sixes = 0, wkts = 0;
            foreach (var inn in m.InningsList.Where(i => !i.SuperOver))
            {
                if (inn.Bat.Team.Id == userId)
                {
                    fours += inn.BatCards.Sum(c => c.Fours);
                    sixes += inn.BatCards.Sum(c => c.Sixes);
                }
                else wkts += inn.Wkts;
            }
            if (fours > 0) list.Add((fours + " four" + (fours == 1 ? "" : "s"), fours * 5));
            if (sixes > 0) list.Add((sixes + " six" + (sixes == 1 ? "" : "es"), sixes * 10));
            if (wkts > 0) list.Add((wkts + " wicket" + (wkts == 1 ? "" : "s"), wkts * 15));
            if (m.Result.SuperOver && won) list.Add(("Super Over win", 150));
            return list;
        }

        public static int ChallengeReward(ChallengeDef c, int oldStars, int newStars)
        {
            int total = 0;
            for (int s = oldStars + 1; s <= newStars; s++) total += c.Reward * (s == 3 ? 2 : 1);
            return total;
        }

        /// <summary>Day number for daily rewards (local calendar day).</summary>
        public static int DayNumber(DateTime local) => (int)(local.Date - new DateTime(2020, 1, 1)).TotalDays;

        public static bool DailyAvailable(SaveData s, DateTime local) => s.LastDailyDay != DayNumber(local);

        /// <summary>Which day of the 7-day cycle the next claim is (0-6).</summary>
        public static int DailySlot(SaveData s, DateTime local)
        {
            int today = DayNumber(local);
            bool continues = s.LastDailyDay == today - 1;
            int streak = continues ? s.DailyStreak : 0;
            return streak % 7;
        }

        public static int ClaimDaily(SaveData s, DateTime local)
        {
            if (!DailyAvailable(s, local)) return 0;
            int today = DayNumber(local);
            bool continues = s.LastDailyDay == today - 1;
            s.DailyStreak = continues ? s.DailyStreak + 1 : 1;
            int slot = (s.DailyStreak - 1) % 7;
            int coins = DailyRewards[slot];
            s.LastDailyDay = today;
            s.Coins += coins;
            s.CoinsEarned += coins;
            return coins;
        }

        public static void Grant(SaveData s, int coins)
        {
            if (coins <= 0) return;
            s.Coins += coins;
            s.CoinsEarned += coins;
        }
    }

    public sealed class AchievementDef
    {
        public string Id, Title, Desc;
        public int Target, Reward;
        public Func<SaveData, int> Progress;
    }

    public static class Achievements
    {
        public static readonly IReadOnlyList<AchievementDef> All = new[]
        {
            A("first_win", "First Victory", "Win your first match", 1, 200, s => s.Stats.Wins),
            A("win10", "Winning Habit", "Win 10 matches", 10, 600, s => s.Stats.Wins),
            A("win50", "Dynasty", "Win 50 matches", 50, 3000, s => s.Stats.Wins),
            A("six25", "Six Hitter", "Hit 25 sixes", 25, 400, s => s.Stats.Sixes),
            A("six100", "Maximum Machine", "Hit 100 sixes", 100, 1500, s => s.Stats.Sixes),
            A("four100", "Boundary Rider", "Hit 100 fours", 100, 800, s => s.Stats.Fours),
            A("fifty", "Half-Century", "A batter scores 50 in an innings", 50, 300, s => s.Stats.HighScore),
            A("hundred", "Centurion", "A batter scores 100 in an innings", 100, 1500, s => s.Stats.HighScore),
            A("runs1000", "Run Mountain", "Score 1,000 runs", 1000, 1000, s => s.Stats.Runs),
            A("wkts50", "Wicket Taker", "Take 50 wickets", 50, 800, s => s.Stats.Wkts),
            A("fivefor", "Five-For", "A bowler takes 5 wickets in an innings", 5, 800, s => s.Stats.BestW),
            A("hattrick", "Hat-Trick Hero", "Take a hat-trick", 1, 1500, s => s.Stats.HatTricks),
            A("maiden", "Maiden Over", "Bowl a maiden over", 1, 200, s => s.Stats.Maidens),
            A("catch10", "Safe Hands", "Take 10 catches from catch prompts", 10, 400, s => s.Stats.Catches),
            A("playoffs", "Playoff Bound", "Reach the KPL playoffs", 1, 600, s => s.Stats.Playoffs),
            A("champ", "Champions!", "Win a KPL season", 1, 3000, s => s.Stats.TourWon),
            A("champ_full", "Full-Season Kings", "Win a full 14-game KPL season", 1, 6000, s => s.Stats.TourFullWon),
            A("stars30", "Rising Star", "Earn 30 challenge stars", 30, 800, s => s.TotalStars),
            A("stars90", "Superstar", "Earn 90 challenge stars", 90, 2500, s => s.TotalStars),
            A("all50", "Completionist", "Complete all 50 challenges", 50, 5000, s => s.ChallengeStars.Count(x => x > 0)),
            A("streak7", "Loyal Fan", "Claim daily rewards 7 days in a row", 7, 700, s => s.DailyStreak),
            A("legend_bat", "Legend's Willow", "Own the Legend Grade 1 bat", 1, 1000, s => s.Owned.Contains("bat_legend") ? 1 : 0),
            A("coach_max", "Master Coach", "Max out any coach", Economy.MaxCoach, 1500, s => Math.Max(s.CoachBat, Math.Max(s.CoachBowl, s.CoachField))),
            A("big_six", "Out of the Park", "Hit a six of 95 metres or more", 95, 600, s => s.Stats.BiggestSix),
            A("super_over", "Super Over Hero", "Win a Super Over", 1, 800, s => s.Stats.SuperOverWins),
            A("team200", "Double Century", "Post a team total of 200+", 200, 1200, s => s.Stats.HighestTotal),
        };

        static AchievementDef A(string id, string title, string desc, int target, int reward, Func<SaveData, int> progress) =>
            new AchievementDef { Id = id, Title = title, Desc = desc, Target = target, Reward = reward, Progress = progress };

        /// <summary>Marks newly met achievements as unlocked; returns them.</summary>
        public static List<AchievementDef> Check(SaveData s)
        {
            var list = new List<AchievementDef>();
            foreach (var a in All)
            {
                var st = s.Ach.FirstOrDefault(x => x.Id == a.Id);
                if (st == null)
                {
                    st = new AchState { Id = a.Id };
                    s.Ach.Add(st);
                }
                if (!st.Unlocked && a.Progress(s) >= a.Target)
                {
                    st.Unlocked = true;
                    list.Add(a);
                }
            }
            return list;
        }

        public static bool Claim(SaveData s, string id)
        {
            var st = s.Ach.FirstOrDefault(x => x.Id == id);
            var def = All.FirstOrDefault(a => a.Id == id);
            if (st == null || def == null || !st.Unlocked || st.Claimed) return false;
            st.Claimed = true;
            Economy.Grant(s, def.Reward);
            return true;
        }

        public static int Claimable(SaveData s) => s.Ach.Count(a => a.Unlocked && !a.Claimed);
    }

    public static class Career
    {
        /// <summary>Adds a finished match to the user's career stats.</summary>
        public static void Record(SaveData s, MatchState m, string userId, int catchesTaken)
        {
            var st = s.Stats;
            if (!m.Finished) return;
            bool scenario = m.IsScenario;
            if (!scenario)
            {
                st.Matches++;
                if (m.Result.WinnerId == userId) st.Wins++;
                else if (m.Result.WinnerId == null) st.Ties++;
                else st.Losses++;
                if (m.Result.SuperOver && m.Result.WinnerId == userId) st.SuperOverWins++;
            }
            else st.Challenges++;
            foreach (var inn in m.InningsList)
            {
                if (inn.Bat.Team.Id == userId)
                {
                    if (!inn.SuperOver) st.HighestTotal = Math.Max(st.HighestTotal, inn.Runs);
                    if (!scenario && inn.Target != null && inn.Chased && !inn.SuperOver) st.ChasesWon++;
                    foreach (var c in inn.BatCards.Where(c => c.Batted))
                    {
                        st.Innings++;
                        st.Runs += c.Runs;
                        st.Balls += c.Balls;
                        st.Fours += c.Fours;
                        st.Sixes += c.Sixes;
                        if (!c.Out) st.NotOuts++;
                        if (c.Out && c.Runs == 0) st.Ducks++;
                        st.HighScore = Math.Max(st.HighScore, c.Runs);
                        if (c.Runs >= 100) st.Hundreds++;
                        else if (c.Runs >= 50) st.Fifties++;
                    }
                    foreach (var b in inn.Log.Where(b => b.Six && b.Distance > 0)) st.BiggestSix = Math.Max(st.BiggestSix, (int)Math.Round(b.Distance + 4));
                }
                else if (inn.Bowl.Team.Id == userId)
                {
                    if (!scenario && inn.Target != null && !inn.Chased && !inn.SuperOver && m.Result.WinnerId == userId) st.DefendsWon++;
                    foreach (var b in inn.BowlCards)
                    {
                        st.Wkts += b.Wkts;
                        st.BallsBowled += b.Balls;
                        st.RunsConceded += b.Runs;
                        st.Maidens += b.Maidens;
                        if (b.HatTrick) st.HatTricks++;
                        if (b.Wkts >= 5) st.FiveFors++;
                        if (b.Wkts > st.BestW || (b.Wkts == st.BestW && b.Runs < st.BestR))
                        {
                            st.BestW = b.Wkts;
                            st.BestR = b.Runs;
                        }
                    }
                    st.RunOuts += inn.BatCards.Count(c => c.Out && c.How == WicketKind.RunOut);
                }
            }
            st.Catches += catchesTaken;
        }
    }
}
