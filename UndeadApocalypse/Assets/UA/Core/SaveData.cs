using System;
using System.Collections.Generic;

namespace UA.Core
{
    [Serializable]
    public sealed class WeaponSave
    {
        public int Id;
        public int[] Levels = new int[6];
        public int Optic = -1;          // Attachment index (RedDot/Holo/Scope) or -1
        public bool Suppressor, ExtMag, Foregrip;
        public int[] OwnedAttachments = new int[0];
        public int Skin;
        public int[] OwnedSkins = new int[0];
    }

    [Serializable]
    public sealed class ButtonLayout
    {
        public string Id = "";
        public float X, Y, Scale = 1;
    }

    [Serializable]
    public sealed class Settings
    {
        public int Graphics = -1;           // -1 = auto-detect on first launch
        public bool Fps60;
        public bool AdaptiveQuality = true;
        public float Sensitivity = 1f, AdsSensitivity = 0.65f;
        public bool Gyro;
        public float GyroSensitivity = 1f;
        public int AimAssist = 1;           // 0 off, 1 low, 2 high
        public bool AutoFire;
        public bool InvertY;
        public bool LeftFire = true;
        public float Music = 0.6f, Sfx = 0.9f;
        public bool Vibration = true;
        public bool ShowFps;
        public float HudScale = 1f;
        public float ButtonAlpha = 0.85f;
        public List<ButtonLayout> Layout = new List<ButtonLayout>();
    }

    [Serializable]
    public sealed class MissionState
    {
        public string Id = "";
        public int Kind, Param;
        public long Target, Progress;
        public bool Claimed;
        public long Coins, Gems, Xp;
        public bool Weekly;
    }

    [Serializable]
    public sealed class Stats
    {
        public long Kills, Headshots, BossKills, EliteKills, StagesCleared, ShotsFired, ShotsHit, Deaths, ExplosiveKills, Rescued, NestsDestroyed;
        public long CoinsEarned, PlayTimeSeconds, DailyChallenges, CampaignCleared, LongestStreak;
        public long[] KillsByType = new long[9];
        public long[] KillsByClass = new long[6];
    }

    /// <summary>The whole player save. Plain serialisable fields only (Unity JsonUtility).</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 1;
        public long Created;
        public long WorldSeed;
        public string PlayerId = "";

        // Wallet.
        public long Coins = 1500, Gems = 50;

        // Progression.
        public int Level = 1;
        public long Xp;
        public int SkillPoints;
        public long Stage = 1, HighestStage = 1;
        public int[] PlayerUpgrades = new int[7];
        public int[] Skills = new int[12];

        // Arsenal.
        public List<WeaponSave> Weapons = new List<WeaponSave>();
        public int[] Loadout = { 0, -1, -1 };
        public int ExtraSlots;
        public int Grenades = 3;
        public int Outfit, Gloves;
        public int[] OwnedOutfits = { 0 };
        public int[] OwnedGloves = { 0 };

        // Modes.
        public int CampaignCleared;
        public int[] CampaignStars = new int[0];
        public int BossRushBest;
        public long DailyDone = -1;

        // Daily login.
        public long LoginDay = -1;
        public int LoginStreak, LoginCycle;
        public long LastSeenDay = -1;

        // Missions & achievements.
        public long MissionDay = -1, MissionWeek = -1;
        public List<MissionState> Missions = new List<MissionState>();
        public List<int> AchievementsClaimed = new List<int>();

        // Boosters (counted in stages so device clock changes cannot extend them).
        public int BoostCoins, BoostDamage, BoostXp;

        // Purchases (never trusted for currency; used for entitlements and duplicate protection).
        public List<string> OwnedProducts = new List<string>();
        public List<string> ProcessedTx = new List<string>();

        // Ads.
        public long AdDay = -1;
        public int AdsToday;

        public Stats Stats = new Stats();
        public Settings Settings = new Settings();

        // Cloud sync bookkeeping.
        public long Revision;
        public long ModifiedUtc;

        public bool OwnsProduct(string id) => OwnedProducts.Contains(id);

        public WeaponSave Weapon(WeaponId id)
        {
            foreach (var w in Weapons) if (w.Id == (int)id) return w;
            return null;
        }

        public bool Owns(WeaponId id) => Weapon(id) != null;

        public WeaponSave GrantWeapon(WeaponId id)
        {
            var w = Weapon(id);
            if (w != null) return w;
            w = new WeaponSave { Id = (int)id };
            Weapons.Add(w);
            if (UA.Core.Weapons.Get(id).BuiltInScope) w.Optic = (int)Attachment.Scope;
            return w;
        }

        public int LoadoutSlots => 2 + Math.Min(1, ExtraSlots);

        /// <summary>Fixes up anything missing or out of range after loading (old saves, edited files).</summary>
        public void Repair()
        {
            if (WorldSeed == 0) WorldSeed = (long)Rng.Hash(Created, 12345);
            if (Weapons == null) Weapons = new List<WeaponSave>();
            GrantWeapon(WeaponId.StandardPistol);
            Weapons.RemoveAll(w => w == null || w.Id < 0 || w.Id >= UA.Core.Weapons.All.Length);
            foreach (var w in Weapons)
            {
                if (w.Levels == null || w.Levels.Length != 6)
                {
                    var l = new int[6];
                    if (w.Levels != null) Array.Copy(w.Levels, l, Math.Min(6, w.Levels.Length));
                    w.Levels = l;
                }
                for (int i = 0; i < 6; i++) w.Levels[i] = Math.Max(0, Math.Min(UA.Core.Weapons.MaxLevel, w.Levels[i]));
                if (w.OwnedAttachments == null) w.OwnedAttachments = new int[0];
                if (w.OwnedSkins == null) w.OwnedSkins = new int[0];
                if (w.Skin < 0 || w.Skin >= UA.Core.Weapons.SkinNames.Length) w.Skin = 0;
                var def = UA.Core.Weapons.Get((WeaponId)w.Id);
                if (def.BuiltInScope) w.Optic = (int)Attachment.Scope;
            }
            if (Loadout == null || Loadout.Length != 3) Loadout = new[] { 0, -1, -1 };
            for (int i = 0; i < 3; i++)
                if (Loadout[i] >= 0 && !Owns((WeaponId)Loadout[i])) Loadout[i] = -1;
            if (Loadout[0] < 0) Loadout[0] = (int)WeaponId.StandardPistol;
            Fix(ref PlayerUpgrades, 7, 0, PlayerProgress.MaxUpgrade);
            Fix(ref Skills, Skill.All.Length, 0, 5);
            for (int i = 0; i < Skills.Length; i++) Skills[i] = Math.Min(Skills[i], Skill.All[i].MaxRank);
            if (CampaignStars == null) CampaignStars = new int[0];
            if (OwnedOutfits == null || OwnedOutfits.Length == 0) OwnedOutfits = new[] { 0 };
            if (OwnedGloves == null || OwnedGloves.Length == 0) OwnedGloves = new[] { 0 };
            if (Array.IndexOf(OwnedOutfits, Outfit) < 0) Outfit = 0;
            if (Array.IndexOf(OwnedGloves, Gloves) < 0) Gloves = 0;
            if (Missions == null) Missions = new List<MissionState>();
            if (AchievementsClaimed == null) AchievementsClaimed = new List<int>();
            if (OwnedProducts == null) OwnedProducts = new List<string>();
            if (ProcessedTx == null) ProcessedTx = new List<string>();
            if (Stats == null) Stats = new Stats();
            if (Stats.KillsByType == null || Stats.KillsByType.Length != 9) Stats.KillsByType = new long[9];
            if (Stats.KillsByClass == null || Stats.KillsByClass.Length != 6) Stats.KillsByClass = new long[6];
            if (Settings == null) Settings = new Settings();
            if (Settings.Layout == null) Settings.Layout = new List<ButtonLayout>();
            Settings.Sensitivity = Safe.Clamp(Settings.Sensitivity, 0.2f, 3f);
            Settings.AdsSensitivity = Safe.Clamp(Settings.AdsSensitivity, 0.2f, 2f);
            Settings.HudScale = Safe.Clamp(Settings.HudScale, 0.7f, 1.3f);
            Coins = Safe.ClampCurrency(Coins);
            Gems = Safe.ClampCurrency(Gems);
            Level = Math.Max(1, Math.Min(PlayerProgress.MaxPlayerLevel, Level));
            Xp = Math.Max(0, Xp);
            SkillPoints = Math.Max(0, SkillPoints);
            Stage = Math.Max(1, Stage);
            HighestStage = Math.Max(HighestStage, Stage);
            Grenades = Math.Max(0, Math.Min(9, Grenades));
            BoostCoins = Math.Max(0, BoostCoins);
            BoostDamage = Math.Max(0, BoostDamage);
            BoostXp = Math.Max(0, BoostXp);
            if (ProcessedTx.Count > 300) ProcessedTx.RemoveRange(0, ProcessedTx.Count - 300);
        }

        static void Fix(ref int[] a, int len, int min, int max)
        {
            if (a == null || a.Length != len)
            {
                var n = new int[len];
                if (a != null) Array.Copy(a, n, Math.Min(len, a.Length));
                a = n;
            }
            for (int i = 0; i < a.Length; i++) a[i] = Math.Max(min, Math.Min(max, a[i]));
        }
    }
}
