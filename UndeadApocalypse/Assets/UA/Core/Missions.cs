using System;
using System.Collections.Generic;

namespace UA.Core
{
    public enum MissionKind { Kills, Headshots, Stages, KillType, ClassKills, DailyChallenge, Coins, BossKills, ExplosiveKills, Rescues }

    /// <summary>Daily / weekly missions, achievements and the 7-day login calendar.</summary>
    public static class Missions
    {
        // ------------------------------------------------------------- missions

        public static long WeekOf(long day) => (day + 3) / 7;   // weeks start on Monday (1970-01-01 was a Thursday)

        /// <summary>Rolls new daily / weekly missions when the day or week changes. Never rolls backwards.</summary>
        public static void Refresh(SaveData d, long today)
        {
            if (today < d.MissionDay) return;   // clock moved backwards: keep current set, grant nothing new
            if (today != d.MissionDay)
            {
                d.Missions.RemoveAll(m => !m.Weekly);
                d.Missions.AddRange(Daily(today, d));
                d.MissionDay = today;
            }
            long week = WeekOf(today);
            if (week != d.MissionWeek)
            {
                d.Missions.RemoveAll(m => m.Weekly);
                d.Missions.AddRange(Weekly(week, d));
                d.MissionWeek = week;
            }
        }

        static IEnumerable<MissionState> Daily(long day, SaveData d)
        {
            var r = new Rng(Rng.Hash(day, 9001, d.WorldSeed));
            var kinds = new List<int> { 0, 1, 2, 3, 4, 6 };
            r.Shuffle(kinds);
            float scale = 1 + Math.Min(3, d.Level / 15f);
            for (int i = 0; i < 3; i++)
                yield return Make("d" + day + "_" + i, kinds[i], r, scale, false);
        }

        static IEnumerable<MissionState> Weekly(long week, SaveData d)
        {
            var r = new Rng(Rng.Hash(week, 9002, d.WorldSeed));
            float scale = 1 + Math.Min(3, d.Level / 15f);
            yield return Make("w" + week + "_0", 0, r, scale * 8, true);
            yield return Make("w" + week + "_1", r.Chance(0.5) ? 1 : 8, r, scale * 6, true);
            yield return Make("w" + week + "_2", r.Chance(0.5) ? 7 : 2, r, scale * 5, true);
        }

        static MissionState Make(string id, int kind, Rng r, float scale, bool weekly)
        {
            var m = new MissionState { Id = id, Weekly = weekly };
            switch (kind)
            {
                case 0: m.Kind = (int)MissionKind.Kills; m.Target = Round(60 * scale); break;
                case 1: m.Kind = (int)MissionKind.Headshots; m.Target = Round(25 * scale); break;
                case 2: m.Kind = (int)MissionKind.Stages; m.Target = Math.Max(2, (long)Math.Round(3 * scale / 2)); break;
                case 3: m.Kind = (int)MissionKind.KillType; m.Param = r.Range(0, 3); m.Target = Round(20 * scale); break;
                case 4: m.Kind = (int)MissionKind.ClassKills; m.Param = r.Range(0, 5); m.Target = Round(30 * scale); break;
                case 6: m.Kind = (int)MissionKind.Coins; m.Target = Round(3000 * scale); break;
                case 7: m.Kind = (int)MissionKind.BossKills; m.Target = Math.Max(1, (long)(scale / 6)); break;
                case 8: m.Kind = (int)MissionKind.ExplosiveKills; m.Target = Round(10 * scale); break;
                default: m.Kind = (int)MissionKind.Kills; m.Target = Round(50 * scale); break;
            }
            m.Coins = weekly ? Round(4000 * scale / 4) : Round(600 * scale);
            m.Gems = weekly ? 25 : (kind == 2 || kind == 1 ? 5 : 0);
            m.Xp = weekly ? Round(800 * scale / 4) : Round(150 * scale);
            return m;
        }

        static long Round(double v) => Math.Max(1, (long)(Math.Round(v / 5.0) * 5));

        public static string Describe(MissionState m)
        {
            string[] cls = { "pistols", "rifles", "shotguns", "sniper rifles", "machine guns", "special weapons" };
            switch ((MissionKind)m.Kind)
            {
                case MissionKind.Kills: return "Kill " + m.Target + " zombies";
                case MissionKind.Headshots: return "Land " + m.Target + " headshot kills";
                case MissionKind.Stages: return "Clear " + m.Target + " stages";
                case MissionKind.KillType: return "Kill " + m.Target + " " + Bestiary.All[m.Param].Name + "s";
                case MissionKind.ClassKills: return "Get " + m.Target + " kills with " + cls[Math.Max(0, Math.Min(5, m.Param))];
                case MissionKind.DailyChallenge: return "Complete the daily challenge";
                case MissionKind.Coins: return "Earn " + m.Target.ToString("#,0") + " coins";
                case MissionKind.BossKills: return "Defeat " + m.Target + " boss" + (m.Target > 1 ? "es" : "");
                case MissionKind.ExplosiveKills: return "Kill " + m.Target + " zombies with explosions";
                case MissionKind.Rescues: return "Rescue " + m.Target + " survivors";
                default: return "Mission";
            }
        }

        public static void Track(SaveData d, MissionKind kind, int param, long amount)
        {
            if (amount <= 0) return;
            foreach (var m in d.Missions)
            {
                if (m.Claimed || m.Kind != (int)kind) continue;
                if ((kind == MissionKind.KillType || kind == MissionKind.ClassKills) && m.Param != param) continue;
                m.Progress = Math.Min(m.Target, Safe.Add(m.Progress, amount));
            }
        }

        public static bool CanClaim(MissionState m) => !m.Claimed && m.Progress >= m.Target;

        public static bool Claim(SaveData d, string id)
        {
            foreach (var m in d.Missions)
            {
                if (m.Id != id || !CanClaim(m)) continue;
                m.Claimed = true;
                Wallet.Add(d, Currency.Coins, m.Coins);
                Wallet.Add(d, Currency.Gems, m.Gems);
                PlayerProgress.AddXp(d, m.Xp);
                return true;
            }
            return false;
        }

        public static int Claimable(SaveData d)
        {
            int n = 0;
            foreach (var m in d.Missions) if (CanClaim(m)) n++;
            for (int i = 0; i < Achievements.All.Length; i++) if (Achievements.CanClaim(d, i)) n++;
            return n;
        }

        // ---------------------------------------------------------------- login

        public static readonly long[] LoginCoins = { 500, 1000, 0, 2000, 2500, 3000, 0 };
        public static readonly long[] LoginGems = { 0, 0, 15, 0, 5, 0, 50 };

        public static bool CanClaimLogin(SaveData d, long today) => today > d.LoginDay;

        /// <summary>Claims today's login reward once. Returns the cycle day claimed (1-7) or 0.</summary>
        public static int ClaimLogin(SaveData d, long today, out long coins, out long gems)
        {
            coins = gems = 0;
            if (!CanClaimLogin(d, today)) return 0;
            d.LoginStreak = d.LoginDay == today - 1 ? d.LoginStreak + 1 : 1;
            d.LoginCycle = d.LoginDay == today - 1 ? d.LoginCycle % 7 + 1 : 1;
            d.LoginDay = today;
            double streakMul = 1 + 0.1 * Math.Min(5, (d.LoginStreak - 1) / 7);
            coins = (long)(LoginCoins[d.LoginCycle - 1] * streakMul);
            gems = LoginGems[d.LoginCycle - 1];
            Wallet.Add(d, Currency.Coins, coins);
            Wallet.Add(d, Currency.Gems, gems);
            if (d.Stats.LongestStreak < d.LoginStreak) d.Stats.LongestStreak = d.LoginStreak;
            return d.LoginCycle;
        }

        /// <summary>The cycle day that would be claimed today (1-7).</summary>
        public static int NextLoginDay(SaveData d, long today) => d.LoginDay == today - 1 ? d.LoginCycle % 7 + 1 : d.LoginDay == today ? d.LoginCycle : 1;
    }

    public sealed class AchievementDef
    {
        public string Name, Desc;
        public Func<SaveData, long> Value;
        public long Target, Coins, Gems;
    }

    public static class Achievements
    {
        public static readonly AchievementDef[] All = Build();

        static AchievementDef[] Build()
        {
            var l = new List<AchievementDef>();
            void A(string n, string desc, Func<SaveData, long> v, long target, long coins, long gems) =>
                l.Add(new AchievementDef { Name = n, Desc = desc, Value = v, Target = target, Coins = coins, Gems = gems });
            A("FIRST BLOOD", "Kill 100 zombies", d => d.Stats.Kills, 100, 1000, 5);
            A("EXTERMINATOR", "Kill 1,000 zombies", d => d.Stats.Kills, 1000, 5000, 15);
            A("HORDE BREAKER", "Kill 10,000 zombies", d => d.Stats.Kills, 10000, 25000, 50);
            A("APOCALYPSE ENDER", "Kill 100,000 zombies", d => d.Stats.Kills, 100000, 100000, 150);
            A("SHARPSHOOTER", "100 headshot kills", d => d.Stats.Headshots, 100, 1500, 5);
            A("HEAD HUNTER", "1,000 headshot kills", d => d.Stats.Headshots, 1000, 8000, 20);
            A("SURGEON", "10,000 headshot kills", d => d.Stats.Headshots, 10000, 40000, 75);
            A("SURVIVOR", "Reach stage 10", d => d.HighestStage, 10, 2000, 10);
            A("VETERAN", "Reach stage 25", d => d.HighestStage, 25, 5000, 20);
            A("WARLORD", "Reach stage 50", d => d.HighestStage, 50, 10000, 30);
            A("CENTURION", "Reach stage 100", d => d.HighestStage, 100, 25000, 60);
            A("LEGEND", "Reach stage 250", d => d.HighestStage, 250, 60000, 120);
            A("IMMORTAL", "Reach stage 500", d => d.HighestStage, 500, 120000, 200);
            A("INFINITE", "Reach stage 1,000", d => d.HighestStage, 1000, 250000, 400);
            A("GIANT SLAYER", "Defeat a boss", d => d.Stats.BossKills, 1, 3000, 15);
            A("BOSS HUNTER", "Defeat 10 bosses", d => d.Stats.BossKills, 10, 15000, 40);
            A("ELITE KILLER", "Kill 50 elite zombies", d => d.Stats.EliteKills, 50, 6000, 15);
            A("COLLECTOR", "Own 5 weapons", d => d.Weapons.Count, 5, 4000, 10);
            A("ARSENAL", "Own 10 weapons", d => d.Weapons.Count, 10, 12000, 30);
            A("ARMORY MASTER", "Own all " + Weapons.All.Length + " weapons", d => d.Weapons.Count, Weapons.All.Length, 50000, 100);
            A("GUNSMITH", "Max any weapon stat", d => MaxLevel(d), Weapons.MaxLevel, 5000, 15);
            A("SPECIALIST", "Spend 10 skill points", d => Spent(d), 10, 5000, 15);
            A("FIRST RESPONDER", "Rescue 10 survivors", d => d.Stats.Rescued, 10, 3000, 10);
            A("NEST BURNER", "Destroy 20 infection nests", d => d.Stats.NestsDestroyed, 20, 4000, 10);
            A("DEMOLITION", "100 explosive kills", d => d.Stats.ExplosiveKills, 100, 4000, 10);
            A("CHAPTER ONE", "Complete campaign chapter 1", d => d.CampaignCleared, 6, 5000, 20);
            A("LAST LIGHT", "Complete the campaign", d => d.CampaignCleared, 30, 30000, 100);
            A("DAILY GRIND", "Complete 7 daily challenges", d => d.Stats.DailyChallenges, 7, 7000, 25);
            A("LOYAL", "Reach a 7-day login streak", d => d.Stats.LongestStreak, 7, 5000, 25);
            A("BOSS RUSH", "Reach boss rush round 5", d => d.BossRushBest, 5, 10000, 40);
            return l.ToArray();
        }

        static long MaxLevel(SaveData d)
        {
            int m = 0;
            foreach (var w in d.Weapons) foreach (var lv in w.Levels) m = Math.Max(m, lv);
            return m;
        }

        static long Spent(SaveData d)
        {
            long n = 0;
            for (int i = 0; i < d.Skills.Length; i++) n += d.Skills[i] * Skill.All[i].Cost;
            return n;
        }

        public static bool Claimed(SaveData d, int i) => d.AchievementsClaimed.Contains(i);

        public static bool CanClaim(SaveData d, int i) => !Claimed(d, i) && All[i].Value(d) >= All[i].Target;

        public static bool Claim(SaveData d, int i)
        {
            if (i < 0 || i >= All.Length || !CanClaim(d, i)) return false;
            d.AchievementsClaimed.Add(i);
            Wallet.Add(d, Currency.Coins, All[i].Coins);
            Wallet.Add(d, Currency.Gems, All[i].Gems);
            return true;
        }
    }
}
