using System;
using System.Collections.Generic;

namespace UA.Core
{
    public enum PStat { Health, Armor, Speed, Reload, CritDamage, Damage, Ammo }

    public enum SkillId { Vitality, Regeneration, ThickSkin, SecondWind, Firepower, Precision, Deadeye, Executioner, FastHands, Bandolier, Scavenger, LuckyFinds }

    public sealed class SkillDef
    {
        public SkillId Id;
        public string Name, Desc;
        public int Branch, Tier, MaxRank, Cost = 1;
        public float PerRank;
        public SkillId? Requires;
    }

    public static class Skill
    {
        public static readonly string[] Branches = { "SURVIVAL", "COMBAT", "LOGISTICS" };

        public static readonly SkillDef[] All =
        {
            new SkillDef { Id = SkillId.Vitality, Name = "VITALITY", Desc = "+6% max health per rank", Branch = 0, Tier = 0, MaxRank = 5, PerRank = 0.06f },
            new SkillDef { Id = SkillId.Regeneration, Name = "REGENERATION", Desc = "Regenerate +0.8 HP/s per rank after 4 s without damage", Branch = 0, Tier = 1, MaxRank = 5, PerRank = 0.8f, Requires = SkillId.Vitality },
            new SkillDef { Id = SkillId.ThickSkin, Name = "THICK SKIN", Desc = "-4% damage taken per rank", Branch = 0, Tier = 2, MaxRank = 5, PerRank = 0.04f, Requires = SkillId.Regeneration },
            new SkillDef { Id = SkillId.SecondWind, Name = "SECOND WIND", Desc = "Once per stage, survive a lethal hit with 35% health", Branch = 0, Tier = 3, MaxRank = 1, Cost = 3, PerRank = 0.35f, Requires = SkillId.ThickSkin },
            new SkillDef { Id = SkillId.Firepower, Name = "FIREPOWER", Desc = "+4% weapon damage per rank", Branch = 1, Tier = 0, MaxRank = 5, PerRank = 0.04f },
            new SkillDef { Id = SkillId.Precision, Name = "PRECISION", Desc = "+3% critical hit chance per rank", Branch = 1, Tier = 1, MaxRank = 5, PerRank = 0.03f, Requires = SkillId.Firepower },
            new SkillDef { Id = SkillId.Deadeye, Name = "DEADEYE", Desc = "+10% headshot damage per rank", Branch = 1, Tier = 2, MaxRank = 5, PerRank = 0.1f, Requires = SkillId.Precision },
            new SkillDef { Id = SkillId.Executioner, Name = "EXECUTIONER", Desc = "+6% damage to elites and bosses per rank", Branch = 1, Tier = 3, MaxRank = 5, Cost = 2, PerRank = 0.06f, Requires = SkillId.Deadeye },
            new SkillDef { Id = SkillId.FastHands, Name = "FAST HANDS", Desc = "+5% reload speed per rank", Branch = 2, Tier = 0, MaxRank = 5, PerRank = 0.05f },
            new SkillDef { Id = SkillId.Bandolier, Name = "BANDOLIER", Desc = "+8% ammunition capacity per rank", Branch = 2, Tier = 1, MaxRank = 5, PerRank = 0.08f, Requires = SkillId.FastHands },
            new SkillDef { Id = SkillId.Scavenger, Name = "SCAVENGER", Desc = "+6% coins collected per rank", Branch = 2, Tier = 2, MaxRank = 5, PerRank = 0.06f, Requires = SkillId.Bandolier },
            new SkillDef { Id = SkillId.LuckyFinds, Name = "LUCKY FINDS", Desc = "+10% chance of loot drops per rank", Branch = 2, Tier = 3, MaxRank = 5, Cost = 2, PerRank = 0.1f, Requires = SkillId.Scavenger },
        };

        public static SkillDef Get(SkillId id) => All[(int)id];

        public static int Rank(SaveData d, SkillId id) => d.Skills[(int)id];

        public static bool CanUnlock(SaveData d, SkillId id, out string why)
        {
            var def = Get(id);
            int rank = Rank(d, id);
            why = "";
            if (rank >= def.MaxRank) { why = "MAXED"; return false; }
            if (def.Requires.HasValue && Rank(d, def.Requires.Value) < 2) { why = "NEEDS " + Get(def.Requires.Value).Name + " 2"; return false; }
            if (d.SkillPoints < def.Cost) { why = "NEED " + def.Cost + " POINT" + (def.Cost > 1 ? "S" : ""); return false; }
            return true;
        }

        public static bool Unlock(SaveData d, SkillId id)
        {
            if (!CanUnlock(d, id, out _)) return false;
            d.SkillPoints -= Get(id).Cost;
            d.Skills[(int)id]++;
            return true;
        }

        /// <summary>Refund every point (costs gems in the UI).</summary>
        public static void Respec(SaveData d)
        {
            int refund = 0;
            for (int i = 0; i < All.Length; i++)
            {
                refund += d.Skills[i] * All[i].Cost;
                d.Skills[i] = 0;
            }
            d.SkillPoints += refund;
        }
    }

    public struct PlayerStats
    {
        public float MaxHealth, MaxArmor, MoveMul, ReloadMul, CritChance, CritMul, DamageMul, AmmoMul, DamageTaken, Regen, CoinMul, LootMul, HeadshotMul, EliteMul, XpMul;
        public bool SecondWind;
    }

    public static class PlayerProgress
    {
        public const int MaxUpgrade = 15;
        public const int MaxPlayerLevel = 9999;

        public static readonly string[] StatNames = { "HEALTH", "ARMOR", "MOVE SPEED", "RELOAD SPEED", "CRITICAL DAMAGE", "WEAPON DAMAGE", "MAX AMMO" };
        public static readonly string[] StatDesc = { "+10 max health", "+8 armor (absorbs 60% of damage)", "+2% movement speed", "+3% reload speed", "+6% critical damage", "+3% weapon damage", "+6% ammunition reserve" };

        public static long UpgradeCost(PStat s, int level)
        {
            if (level >= MaxUpgrade) return 0;
            double basis = s == PStat.Damage || s == PStat.Health ? 360 : 300;
            return (long)(Math.Round(basis * Math.Pow(1.42, level) / 10.0) * 10);
        }

        public static PlayerStats Compute(SaveData d)
        {
            int U(PStat s) => d.PlayerUpgrades[(int)s];
            int K(SkillId s) => d.Skills[(int)s];
            var ps = new PlayerStats
            {
                MaxHealth = (100 + U(PStat.Health) * 10) * (1 + K(SkillId.Vitality) * Skill.Get(SkillId.Vitality).PerRank),
                MaxArmor = U(PStat.Armor) * 8,
                MoveMul = 1 + U(PStat.Speed) * 0.02f,
                ReloadMul = 1 + U(PStat.Reload) * 0.03f + K(SkillId.FastHands) * 0.05f,
                CritChance = 0.05f + K(SkillId.Precision) * 0.03f,
                CritMul = 1.5f + U(PStat.CritDamage) * 0.06f,
                DamageMul = (1 + U(PStat.Damage) * 0.03f) * (1 + K(SkillId.Firepower) * 0.04f),
                AmmoMul = (1 + U(PStat.Ammo) * 0.06f) * (1 + K(SkillId.Bandolier) * 0.08f),
                DamageTaken = 1 - K(SkillId.ThickSkin) * 0.04f,
                Regen = K(SkillId.Regeneration) * 0.8f,
                CoinMul = 1 + K(SkillId.Scavenger) * 0.06f,
                LootMul = 1 + K(SkillId.LuckyFinds) * 0.1f,
                HeadshotMul = 1 + K(SkillId.Deadeye) * 0.1f,
                EliteMul = 1 + K(SkillId.Executioner) * 0.06f,
                XpMul = 1,
                SecondWind = K(SkillId.SecondWind) > 0,
            };
            if (d.CoinDoublerOwned()) ps.CoinMul *= 2;
            if (d.BoostCoins > 0) ps.CoinMul *= 2;
            if (d.BoostDamage > 0) ps.DamageMul *= 1.25f;
            if (d.BoostXp > 0) ps.XpMul *= 2;
            return ps;
        }

        static bool CoinDoublerOwned(this SaveData d) => d.OwnsProduct(Store.CoinDoubler);

        public static WeaponStats Weapon(SaveData d, WeaponId id, PlayerStats ps)
        {
            var def = Weapons.Get(id);
            var ws = d.Weapon(id) ?? new WeaponSave { Id = (int)id };
            int L(WStat s) => ws.Levels[(int)s];
            var st = new WeaponStats
            {
                Damage = def.Damage * (1 + L(WStat.Damage) * 0.08f) * ps.DamageMul,
                Interval = def.Interval / (1 + L(WStat.FireRate) * 0.03f),
                Reload = def.Reload * (1 - L(WStat.Reload) * 0.04f) / ps.ReloadMul,
                HipSpread = def.HipSpread * (1 - L(WStat.Accuracy) * 0.05f),
                AdsSpread = def.AdsSpread * (1 - L(WStat.Accuracy) * 0.05f),
                RecoilV = def.RecoilV * (1 - L(WStat.Stability) * 0.05f),
                RecoilH = def.RecoilH * (1 - L(WStat.Stability) * 0.05f),
                AdsZoom = def.AdsZoom,
                Range = def.Range,
                CritChance = ps.CritChance,
                CritMul = ps.CritMul,
                HeadMul = def.HeadMul * ps.HeadshotMul,
                Mag = (int)Math.Round(def.Mag * (1 + L(WStat.Magazine) * 0.06f)),
                Reserve = def.Reserve >= 9999 ? 9999 : (int)Math.Round(def.Reserve * ps.AmmoMul),
                Pellets = def.Pellets,
                Pierce = def.Pierce,
            };
            if (ws.Optic == (int)Attachment.RedDot) { st.AdsSpread *= 0.85f; st.AdsZoom = Math.Min(st.AdsZoom, 0.8f); }
            if (ws.Optic == (int)Attachment.Holo) { st.AdsSpread *= 0.8f; st.AdsZoom = Math.Min(st.AdsZoom, 0.74f); }
            if (ws.Optic == (int)Attachment.Scope && !def.BuiltInScope) { st.AdsSpread *= 0.5f; st.AdsZoom = 0.45f; }
            if (ws.Suppressor) { st.Suppressed = true; st.RecoilV *= 0.9f; st.RecoilH *= 0.9f; st.Damage *= 0.95f; }
            if (ws.ExtMag) { st.Mag = (int)Math.Round(st.Mag * 1.35f); st.Reload *= 1.1f; }
            if (ws.Foregrip) { st.RecoilV *= 0.75f; st.RecoilH *= 0.75f; st.HipSpread *= 0.85f; }
            st.Mag = Math.Max(1, st.Mag);
            return st;
        }

        // ------------------------------------------------------------ XP / levels

        public static long XpToNext(int level) => (long)Math.Round(140 * Math.Pow(Math.Max(1, level), 1.55));

        /// <summary>Adds XP, levelling up as often as needed. Returns the number of level-ups.</summary>
        public static int AddXp(SaveData d, long xp)
        {
            if (xp <= 0) return 0;
            d.Xp = Safe.Add(d.Xp, xp);
            int ups = 0;
            while (d.Level < MaxPlayerLevel && d.Xp >= XpToNext(d.Level))
            {
                d.Xp -= XpToNext(d.Level);
                d.Level++;
                d.SkillPoints++;
                d.Coins = Safe.ClampCurrency(Safe.Add(d.Coins, LevelUpCoins(d.Level)));
                if (d.Level % 5 == 0) d.Gems = Safe.ClampCurrency(Safe.Add(d.Gems, 10));
                ups++;
            }
            if (d.Level >= MaxPlayerLevel) d.Xp = 0;
            return ups;
        }

        public static long LevelUpCoins(int level) => 250 + level * 50L;
    }

    /// <summary>Wallet operations. All amounts are validated; nothing goes negative or overflows.</summary>
    public static class Wallet
    {
        public static long Balance(SaveData d, Currency c) => c == Currency.Gems ? d.Gems : d.Coins;

        public static bool CanAfford(SaveData d, Currency c, long amount) => amount >= 0 && Balance(d, c) >= amount;

        public static bool Spend(SaveData d, Currency c, long amount)
        {
            if (amount < 0 || !CanAfford(d, c, amount)) return false;
            if (c == Currency.Gems) d.Gems -= amount;
            else d.Coins -= amount;
            return true;
        }

        public static void Add(SaveData d, Currency c, long amount)
        {
            if (amount <= 0) return;
            if (c == Currency.Gems) d.Gems = Safe.ClampCurrency(Safe.Add(d.Gems, amount));
            else
            {
                d.Coins = Safe.ClampCurrency(Safe.Add(d.Coins, amount));
                d.Stats.CoinsEarned = Safe.Add(d.Stats.CoinsEarned, amount);
            }
        }

        // -------------------------------------------------------------- shop actions

        public static bool BuyWeapon(SaveData d, WeaponId id)
        {
            var def = Weapons.Get(id);
            if (d.Owns(id) || d.Level < def.UnlockLevel) return false;
            if (!Spend(d, def.PriceCurrency, def.Price)) return false;
            d.GrantWeapon(id);
            return true;
        }

        public static bool UpgradeWeapon(SaveData d, WeaponId id, WStat s)
        {
            var ws = d.Weapon(id);
            if (ws == null || ws.Levels[(int)s] >= Weapons.MaxLevel) return false;
            long cost = Weapons.UpgradeCost(Weapons.Get(id), s, ws.Levels[(int)s]);
            if (!Spend(d, Currency.Coins, cost)) return false;
            ws.Levels[(int)s]++;
            return true;
        }

        public static bool OwnsAttachment(WeaponSave ws, Attachment a) => Array.IndexOf(ws.OwnedAttachments, (int)a) >= 0;

        public static bool BuyAttachment(SaveData d, WeaponId id, Attachment a)
        {
            var ws = d.Weapon(id);
            if (ws == null || !Weapons.AttachmentFits(Weapons.Get(id), a) || OwnsAttachment(ws, a)) return false;
            if (!Spend(d, Currency.Coins, Weapons.AttachmentPrice[(int)a])) return false;
            var l = new List<int>(ws.OwnedAttachments) { (int)a };
            ws.OwnedAttachments = l.ToArray();
            Equip(d, id, a, true);
            return true;
        }

        public static void Equip(SaveData d, WeaponId id, Attachment a, bool on)
        {
            var ws = d.Weapon(id);
            if (ws == null || (on && !OwnsAttachment(ws, a))) return;
            switch (a)
            {
                case Attachment.Suppressor: ws.Suppressor = on; break;
                case Attachment.ExtMag: ws.ExtMag = on; break;
                case Attachment.Foregrip: ws.Foregrip = on; break;
                default:
                    if (Weapons.Get(id).BuiltInScope) return;
                    ws.Optic = on ? (int)a : (ws.Optic == (int)a ? -1 : ws.Optic);
                    break;
            }
        }

        public static bool IsEquipped(WeaponSave ws, Attachment a)
        {
            switch (a)
            {
                case Attachment.Suppressor: return ws.Suppressor;
                case Attachment.ExtMag: return ws.ExtMag;
                case Attachment.Foregrip: return ws.Foregrip;
                default: return ws.Optic == (int)a;
            }
        }

        public static bool OwnsSkin(WeaponSave ws, Skin s) => s == Skin.Factory || Array.IndexOf(ws.OwnedSkins, (int)s) >= 0;

        public static bool BuySkin(SaveData d, WeaponId id, Skin s)
        {
            var ws = d.Weapon(id);
            if (ws == null) return false;
            if (!OwnsSkin(ws, s))
            {
                if (!Spend(d, Currency.Gems, Weapons.SkinGems[(int)s])) return false;
                var l = new List<int>(ws.OwnedSkins) { (int)s };
                ws.OwnedSkins = l.ToArray();
            }
            ws.Skin = (int)s;
            return true;
        }

        public static void GrantSkinAll(SaveData d, Skin s)
        {
            foreach (var ws in d.Weapons)
                if (!OwnsSkin(ws, s))
                {
                    var l = new List<int>(ws.OwnedSkins) { (int)s };
                    ws.OwnedSkins = l.ToArray();
                }
        }

        public static bool BuyOutfit(SaveData d, int outfit)
        {
            if (outfit < 0 || outfit >= Weapons.OutfitNames.Length) return false;
            if (Array.IndexOf(d.OwnedOutfits, outfit) < 0)
            {
                if (!Spend(d, Currency.Gems, Weapons.OutfitGems[outfit])) return false;
                var l = new List<int>(d.OwnedOutfits) { outfit };
                d.OwnedOutfits = l.ToArray();
            }
            d.Outfit = outfit;
            return true;
        }

        public static bool BuyGloves(SaveData d, int gloves)
        {
            if (gloves < 0 || gloves >= Weapons.GloveNames.Length) return false;
            if (Array.IndexOf(d.OwnedGloves, gloves) < 0)
            {
                if (!Spend(d, Currency.Gems, Weapons.GloveGems[gloves])) return false;
                var l = new List<int>(d.OwnedGloves) { gloves };
                d.OwnedGloves = l.ToArray();
            }
            d.Gloves = gloves;
            return true;
        }

        public static bool UpgradePlayer(SaveData d, PStat s)
        {
            int lv = d.PlayerUpgrades[(int)s];
            if (lv >= PlayerProgress.MaxUpgrade) return false;
            if (!Spend(d, Currency.Coins, PlayerProgress.UpgradeCost(s, lv))) return false;
            d.PlayerUpgrades[(int)s]++;
            return true;
        }
    }

    /// <summary>What a finished stage pays out. Built from verified in-game counters and capped.</summary>
    public sealed class StageReward
    {
        public long Coins, Xp, Gems, KillCoins, ClearCoins, BonusCoins;
        public bool FirstClear;
    }

    public static class Rewards
    {
        /// <summary>
        /// Final payout for a cleared (or failed) stage. Kill coins are recomputed from the kill
        /// counts rather than trusted, and the total is capped at what the stage could plausibly pay.
        /// </summary>
        public static StageReward ForStage(StagePlan plan, bool cleared, long[] killsByType, int headshots, int bossKills, PlayerStats ps, bool firstClear)
        {
            var r = new StageReward { FirstClear = firstClear };
            double mul = plan.RewardMul;
            long killCoins = 0, xp = 0;
            int kills = 0;
            for (int i = 0; i < killsByType.Length && i < Bestiary.All.Length; i++)
            {
                long n = Math.Max(0, Math.Min(killsByType[i], 5000));
                kills += (int)n;
                killCoins += (long)(n * Bestiary.All[i].Coins * mul);
                xp += (long)(n * Bestiary.All[i].Xp * mul);
            }
            headshots = Math.Max(0, Math.Min(headshots, kills));
            killCoins += (long)(headshots * 3 * mul);
            if (bossKills > 0 && plan.IsBoss)
            {
                var b = Bestiary.Boss(plan.Boss);
                killCoins += (long)(b.Coins * mul);
                xp += (long)(b.Xp * mul);
                r.Gems += firstClear ? b.Gems : 0;
            }
            r.KillCoins = Safe.Mul(killCoins, ps.CoinMul * (plan.Event == StageEvent.BloodMoon ? 2 : 1));
            if (cleared)
            {
                r.ClearCoins = Safe.Mul((long)(120 * mul), ps.CoinMul);
                xp += (long)(150 * mul);
                if (Difficulty.IsElite(plan.Stage) && plan.Mode == GameMode.Survival && firstClear) r.Gems += 5;
            }
            else
            {
                // A failed run still keeps half of the kill coins.
                r.KillCoins /= 2;
                xp /= 2;
            }
            r.Coins = Safe.ClampCurrency(r.KillCoins + r.ClearCoins);
            r.Xp = Safe.Mul(xp, ps.XpMul);
            // Plausibility cap: nothing can pay more than every spawn being a headshot kill plus the clear bonus x2.
            long cap = (long)((plan.TotalSpawns + 60) * 120 * mul * Math.Max(1, ps.CoinMul) * 2 + 20000 * mul);
            if (r.Coins > cap) r.Coins = cap;
            return r;
        }
    }
}
