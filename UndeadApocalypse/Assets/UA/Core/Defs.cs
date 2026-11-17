using System;
using System.Collections.Generic;

namespace UA.Core
{
    public enum GameMode { Survival, Campaign, Daily, BossRush }

    public enum Currency { Coins, Gems, Money }

    // ------------------------------------------------------------------ weapons

    public enum WeaponClass { Pistol, Rifle, Shotgun, Sniper, MachineGun, Special }

    public enum FireMode { Semi, Auto, Burst, Pump, Bolt, Flame, Launcher, Beam }

    public enum WeaponId
    {
        StandardPistol, HeavyPistol, TacticalPistol,
        VK74, R4Carbine, RavenTR, BastionBurst,
        PumpShotgun, AutoShotgun, HeavyShotgun,
        TacticalSniper, HeavySniper, SemiSniper,
        LightMG, HeavyMG,
        Flamethrower, Launcher, ArcRifle,
        // Appended later: keep new ids at the end so saved weapon indices stay valid.
        Smg, RocketLauncher,
    }

    public enum WStat { Damage, Magazine, Reload, Accuracy, FireRate, Stability }

    public enum Attachment { RedDot, Holo, Scope, Suppressor, ExtMag, Foregrip }

    public enum Skin { Factory, Woodland, Desert, UrbanDigital, Arctic, BloodRed, Carbon, Toxic, Gold, Dragon }

    public enum Outfit { Ranger, UrbanOps, DesertStorm, BlackOps, Arctic, Hazmat }

    public enum Gloves { Tactical, Tan, RedKnuckle, Carbon, Gold }

    public sealed class WeaponDef
    {
        public WeaponId Id;
        public string Name, Desc;
        public WeaponClass Class;
        public FireMode Mode;
        public float Damage, Rpm, Reload, HipSpread, AdsSpread, RecoilV, RecoilH, Range, HeadMul = 2.5f, AdsZoom = 0.78f, MoveMul = 1;
        public int Mag, Reserve, Pellets = 1, BurstCount, Pierce, Rarity;
        public float ExplosionRadius, ExplosionDamage, ProjectileSpeed, ArmorPierce, BurnDps;
        public bool BuiltInScope, ShellReload;
        public long Price;
        public Currency PriceCurrency;
        public int UnlockLevel;

        public float Interval => 60f / Math.Max(1, Rpm);
        public bool IsAuto => Mode == FireMode.Auto || Mode == FireMode.Flame || Mode == FireMode.Beam;
    }

    public static class Weapons
    {
        public static readonly WeaponDef[] All = Build();
        public const int MaxLevel = 10;

        public static WeaponDef Get(WeaponId id) => All[(int)id];

        static WeaponDef[] Build()
        {
            var l = new List<WeaponDef>
            {
                new WeaponDef { Id = WeaponId.StandardPistol, Name = "P9 Sentinel", Desc = "Reliable 9mm sidearm. Never runs dry on spare ammo.", Class = WeaponClass.Pistol, Mode = FireMode.Semi, Damage = 34, Rpm = 400, Mag = 15, Reserve = 9999, Reload = 1.35f, HipSpread = 1.6f, AdsSpread = 0.35f, RecoilV = 1.6f, RecoilH = 0.5f, Range = 45, HeadMul = 3f, MoveMul = 1.06f, Price = 0, PriceCurrency = Currency.Coins },
                new WeaponDef { Id = WeaponId.HeavyPistol, Name = "Vindicator .50", Desc = "Hand cannon. Slow, brutal, one-shots walkers to the head.", Class = WeaponClass.Pistol, Mode = FireMode.Semi, Damage = 92, Rpm = 160, Mag = 7, Reserve = 9999, Reload = 1.7f, HipSpread = 2.2f, AdsSpread = 0.4f, RecoilV = 5.5f, RecoilH = 1.2f, Range = 55, HeadMul = 3.2f, MoveMul = 1.04f, ArmorPierce = 0.35f, Price = 3500, PriceCurrency = Currency.Coins, UnlockLevel = 3, Rarity = 1 },
                new WeaponDef { Id = WeaponId.TacticalPistol, Name = "TX-18 Machine Pistol", Desc = "Select-fire tactical pistol that empties fast.", Class = WeaponClass.Pistol, Mode = FireMode.Auto, Damage = 24, Rpm = 900, Mag = 24, Reserve = 9999, Reload = 1.4f, HipSpread = 2.4f, AdsSpread = 0.9f, RecoilV = 1.1f, RecoilH = 0.9f, Range = 35, HeadMul = 2.6f, MoveMul = 1.06f, Price = 5000, PriceCurrency = Currency.Coins, UnlockLevel = 5, Rarity = 1 },

                new WeaponDef { Id = WeaponId.VK74, Name = "VK-74 Rifle", Desc = "Rugged 7.62 assault rifle with wooden furniture. Hits hard, kicks hard.", Class = WeaponClass.Rifle, Mode = FireMode.Auto, Damage = 40, Rpm = 600, Mag = 30, Reserve = 210, Reload = 2.3f, HipSpread = 2.8f, AdsSpread = 0.5f, RecoilV = 1.9f, RecoilH = 0.9f, Range = 80, HeadMul = 2.5f, MoveMul = 0.95f, ArmorPierce = 0.2f, Price = 6000, PriceCurrency = Currency.Coins, UnlockLevel = 2, Rarity = 1 },
                new WeaponDef { Id = WeaponId.R4Carbine, Name = "R4 Carbine", Desc = "Accurate 5.56 carbine with a rail system. Easy to control.", Class = WeaponClass.Rifle, Mode = FireMode.Auto, Damage = 33, Rpm = 780, Mag = 30, Reserve = 240, Reload = 2.1f, HipSpread = 2.4f, AdsSpread = 0.35f, RecoilV = 1.25f, RecoilH = 0.55f, Range = 85, HeadMul = 2.5f, MoveMul = 0.97f, Price = 9500, PriceCurrency = Currency.Coins, UnlockLevel = 6, Rarity = 2 },
                new WeaponDef { Id = WeaponId.RavenTR, Name = "Raven TR-9", Desc = "Bullpup tactical rifle. High rate of fire, low recoil.", Class = WeaponClass.Rifle, Mode = FireMode.Auto, Damage = 31, Rpm = 900, Mag = 36, Reserve = 288, Reload = 2.0f, HipSpread = 2.2f, AdsSpread = 0.32f, RecoilV = 1.05f, RecoilH = 0.5f, Range = 80, HeadMul = 2.5f, MoveMul = 0.98f, Price = 16000, PriceCurrency = Currency.Coins, UnlockLevel = 12, Rarity = 2 },
                new WeaponDef { Id = WeaponId.BastionBurst, Name = "Bastion B3", Desc = "Three-round burst rifle. Every trigger pull is a headshot waiting to happen.", Class = WeaponClass.Rifle, Mode = FireMode.Burst, BurstCount = 3, Damage = 46, Rpm = 1000, Mag = 30, Reserve = 240, Reload = 2.2f, HipSpread = 2.0f, AdsSpread = 0.25f, RecoilV = 1.4f, RecoilH = 0.4f, Range = 95, HeadMul = 2.8f, MoveMul = 0.97f, Price = 13000, PriceCurrency = Currency.Coins, UnlockLevel = 9, Rarity = 2 },

                new WeaponDef { Id = WeaponId.PumpShotgun, Name = "Ironclad Pump-12", Desc = "Pump-action 12 gauge. Clears a doorway in one shot.", Class = WeaponClass.Shotgun, Mode = FireMode.Pump, Pellets = 9, Damage = 18, Rpm = 75, Mag = 7, Reserve = 56, Reload = 0.55f, ShellReload = true, HipSpread = 5.5f, AdsSpread = 4.2f, RecoilV = 6.5f, RecoilH = 1.5f, Range = 25, HeadMul = 1.8f, MoveMul = 0.96f, Price = 7000, PriceCurrency = Currency.Coins, UnlockLevel = 3, Rarity = 1 },
                new WeaponDef { Id = WeaponId.AutoShotgun, Name = "Hurricane AS-20", Desc = "Drum-fed automatic shotgun. Pure crowd control.", Class = WeaponClass.Shotgun, Mode = FireMode.Auto, Pellets = 8, Damage = 15, Rpm = 300, Mag = 20, Reserve = 100, Reload = 2.8f, HipSpread = 6f, AdsSpread = 4.6f, RecoilV = 3.6f, RecoilH = 1.4f, Range = 22, HeadMul = 1.7f, MoveMul = 0.92f, Price = 19000, PriceCurrency = Currency.Coins, UnlockLevel = 14, Rarity = 2 },
                new WeaponDef { Id = WeaponId.HeavyShotgun, Name = "Juggernaut 10-Gauge", Desc = "Heavy combat shotgun firing slug-buck. Staggers anything.", Class = WeaponClass.Shotgun, Mode = FireMode.Semi, Pellets = 10, Damage = 26, Rpm = 160, Mag = 8, Reserve = 64, Reload = 2.6f, HipSpread = 4.8f, AdsSpread = 3.6f, RecoilV = 7.5f, RecoilH = 2f, Range = 30, HeadMul = 1.9f, MoveMul = 0.9f, ArmorPierce = 0.4f, Price = 320, PriceCurrency = Currency.Gems, UnlockLevel = 8, Rarity = 3 },

                new WeaponDef { Id = WeaponId.TacticalSniper, Name = "Longreach SR-7", Desc = "Bolt-action tactical sniper. One shot, one kill.", Class = WeaponClass.Sniper, Mode = FireMode.Bolt, Damage = 190, Rpm = 48, Mag = 5, Reserve = 40, Reload = 2.6f, HipSpread = 4f, AdsSpread = 0.02f, RecoilV = 7f, RecoilH = 1f, Range = 200, HeadMul = 3.5f, AdsZoom = 0.32f, MoveMul = 0.92f, Pierce = 1, ArmorPierce = 0.5f, BuiltInScope = true, Price = 15000, PriceCurrency = Currency.Coins, UnlockLevel = 7, Rarity = 2 },
                new WeaponDef { Id = WeaponId.HeavySniper, Name = "Behemoth .50 AMR", Desc = "Anti-materiel rifle. Punches through rows of the dead.", Class = WeaponClass.Sniper, Mode = FireMode.Semi, Damage = 380, Rpm = 55, Mag = 6, Reserve = 36, Reload = 3.2f, HipSpread = 5f, AdsSpread = 0.02f, RecoilV = 10f, RecoilH = 2f, Range = 250, HeadMul = 3f, AdsZoom = 0.28f, MoveMul = 0.86f, Pierce = 4, ArmorPierce = 0.9f, BuiltInScope = true, Price = 480, PriceCurrency = Currency.Gems, UnlockLevel = 15, Rarity = 3 },
                new WeaponDef { Id = WeaponId.SemiSniper, Name = "Hawkeye DMR", Desc = "Semi-automatic marksman rifle for rapid precise shots.", Class = WeaponClass.Sniper, Mode = FireMode.Semi, Damage = 105, Rpm = 210, Mag = 12, Reserve = 96, Reload = 2.4f, HipSpread = 3.2f, AdsSpread = 0.05f, RecoilV = 3.6f, RecoilH = 0.8f, Range = 160, HeadMul = 3f, AdsZoom = 0.42f, MoveMul = 0.93f, Pierce = 1, ArmorPierce = 0.35f, BuiltInScope = true, Price = 23000, PriceCurrency = Currency.Coins, UnlockLevel = 16, Rarity = 2 },

                new WeaponDef { Id = WeaponId.LightMG, Name = "Warden LMG", Desc = "Belt-fed light machine gun with a 100-round box.", Class = WeaponClass.MachineGun, Mode = FireMode.Auto, Damage = 34, Rpm = 760, Mag = 100, Reserve = 400, Reload = 4.2f, HipSpread = 3.4f, AdsSpread = 0.8f, RecoilV = 1.35f, RecoilH = 1.0f, Range = 85, HeadMul = 2.3f, MoveMul = 0.84f, Price = 26000, PriceCurrency = Currency.Coins, UnlockLevel = 18, Rarity = 2 },
                new WeaponDef { Id = WeaponId.HeavyMG, Name = "Titan HMG", Desc = "Heavy machine gun. Shreds armor and anything behind it.", Class = WeaponClass.MachineGun, Mode = FireMode.Auto, Damage = 52, Rpm = 650, Mag = 150, Reserve = 450, Reload = 5f, HipSpread = 3.8f, AdsSpread = 1.0f, RecoilV = 1.8f, RecoilH = 1.3f, Range = 100, HeadMul = 2.3f, MoveMul = 0.78f, Pierce = 1, ArmorPierce = 0.6f, Price = 650, PriceCurrency = Currency.Gems, UnlockLevel = 20, Rarity = 3 },

                new WeaponDef { Id = WeaponId.Flamethrower, Name = "Inferno FT-6", Desc = "Pressurised flamethrower. Sets whole crowds alight.", Class = WeaponClass.Special, Mode = FireMode.Flame, Damage = 11, Rpm = 1200, Mag = 120, Reserve = 360, Reload = 3.4f, HipSpread = 7f, AdsSpread = 6f, RecoilV = 0.15f, RecoilH = 0.15f, Range = 13, HeadMul = 1f, MoveMul = 0.88f, BurnDps = 14, Pierce = 8, Price = 420, PriceCurrency = Currency.Gems, UnlockLevel = 10, Rarity = 3 },
                new WeaponDef { Id = WeaponId.Launcher, Name = "Thunder GL-6", Desc = "Six-shot revolving grenade launcher.", Class = WeaponClass.Special, Mode = FireMode.Launcher, Damage = 60, Rpm = 90, Mag = 6, Reserve = 24, Reload = 3.6f, HipSpread = 1.5f, AdsSpread = 0.8f, RecoilV = 6f, RecoilH = 1.5f, Range = 70, HeadMul = 1f, MoveMul = 0.86f, ExplosionRadius = 5.5f, ExplosionDamage = 260, ProjectileSpeed = 32, Price = 32000, PriceCurrency = Currency.Coins, UnlockLevel = 22, Rarity = 2 },
                new WeaponDef { Id = WeaponId.ArcRifle, Name = "Arc Pulse Rifle", Desc = "Experimental energy weapon. Bolts arc through several targets.", Class = WeaponClass.Special, Mode = FireMode.Beam, Damage = 58, Rpm = 420, Mag = 40, Reserve = 200, Reload = 2.4f, HipSpread = 1.4f, AdsSpread = 0.3f, RecoilV = 0.9f, RecoilH = 0.3f, Range = 90, HeadMul = 2.2f, MoveMul = 0.95f, Pierce = 3, ArmorPierce = 1f, Price = 850, PriceCurrency = Currency.Gems, UnlockLevel = 25, Rarity = 3 },
                new WeaponDef { Id = WeaponId.Smg, Name = "Viper SMG", Desc = "Compact 9mm submachine gun. Very fast, very light, made for close quarters.", Class = WeaponClass.Rifle, Mode = FireMode.Auto, Damage = 26, Rpm = 980, Mag = 32, Reserve = 320, Reload = 1.8f, HipSpread = 1.9f, AdsSpread = 0.6f, RecoilV = 0.95f, RecoilH = 0.7f, Range = 45, HeadMul = 2.4f, MoveMul = 1.05f, Price = 7500, PriceCurrency = Currency.Coins, UnlockLevel = 4, Rarity = 1 },
                new WeaponDef { Id = WeaponId.RocketLauncher, Name = "Hellfire RPG", Desc = "Shoulder-fired rocket. Flies straight and flattens crowds.", Class = WeaponClass.Special, Mode = FireMode.Launcher, Damage = 90, Rpm = 50, Mag = 1, Reserve = 10, Reload = 2.8f, HipSpread = 1.2f, AdsSpread = 0.4f, RecoilV = 7f, RecoilH = 1.2f, Range = 120, HeadMul = 1f, MoveMul = 0.82f, ExplosionRadius = 7f, ExplosionDamage = 420, ProjectileSpeed = 48, Price = 700, PriceCurrency = Currency.Gems, UnlockLevel = 24, Rarity = 3 },
            };
            l.Sort((a, b) => a.Id.CompareTo(b.Id));
            return l.ToArray();
        }

        // ------------------------------------------------------------ upgrades

        public static readonly string[] StatNames = { "DAMAGE", "MAGAZINE", "RELOAD", "ACCURACY", "FIRE RATE", "STABILITY" };

        /// <summary>Coin cost of raising a stat from <paramref name="level"/> to level + 1.</summary>
        public static long UpgradeCost(WeaponDef w, WStat s, int level)
        {
            if (level >= MaxLevel) return 0;
            double basis = w.PriceCurrency == Currency.Gems ? 1600 + w.Price * 6 : Math.Max(220, w.Price / 9.0);
            double statMul = s == WStat.Damage ? 1.25 : s == WStat.FireRate ? 1.15 : 1.0;
            return (long)(Math.Round(basis * statMul * Math.Pow(1.48, level) / 10.0) * 10);
        }

        public static bool AttachmentFits(WeaponDef w, Attachment a)
        {
            switch (w.Class)
            {
                case WeaponClass.Pistol: return a == Attachment.RedDot || a == Attachment.Suppressor || a == Attachment.ExtMag;
                case WeaponClass.Rifle: return true;
                case WeaponClass.Shotgun: return a == Attachment.RedDot || a == Attachment.Holo || a == Attachment.ExtMag || a == Attachment.Foregrip;
                case WeaponClass.Sniper: return a == Attachment.Suppressor || a == Attachment.ExtMag;
                case WeaponClass.MachineGun: return a != Attachment.Suppressor;
                default: return a == Attachment.ExtMag;
            }
        }

        public static bool IsOptic(Attachment a) => a == Attachment.RedDot || a == Attachment.Holo || a == Attachment.Scope;

        public static readonly string[] AttachmentNames = { "RED DOT SIGHT", "HOLOGRAPHIC SIGHT", "4X SCOPE", "SUPPRESSOR", "EXTENDED MAG", "FOREGRIP" };
        public static readonly string[] AttachmentDesc =
        {
            "Tighter aim and faster sighting.",
            "Wide window, even tighter aim.",
            "Long-range magnification.",
            "Quieter shots draw fewer zombies. Less recoil, -5% damage.",
            "+35% magazine size.",
            "-25% recoil, -15% hip-fire spread.",
        };
        public static readonly long[] AttachmentPrice = { 1500, 2500, 4000, 3000, 3500, 2800 };

        public static readonly string[] SkinNames = { "FACTORY", "WOODLAND", "DESERT", "URBAN DIGITAL", "ARCTIC", "BLOOD RED", "CARBON", "TOXIC", "GOLD", "DRAGON" };
        public static readonly long[] SkinGems = { 0, 40, 40, 60, 60, 100, 120, 150, 300, 500 };
        /// <summary>Playable characters (outfit slots keep their index, price and save data).</summary>
        public static readonly string[] OutfitNames = { "SOLDIER", "SPECIAL FORCES", "SURVIVOR", "HUNTER", "ENGINEER", "MEDIC" };
        public static readonly long[] OutfitGems = { 0, 60, 60, 120, 120, 200 };
        public static readonly string[] GloveNames = { "TACTICAL", "TAN LEATHER", "RED KNUCKLE", "CARBON", "GOLD" };
        public static readonly long[] GloveGems = { 0, 40, 80, 120, 250 };
    }

    /// <summary>Final numbers for one weapon after upgrades, attachments and player skills.</summary>
    public struct WeaponStats
    {
        public float Damage, Interval, Reload, HipSpread, AdsSpread, RecoilV, RecoilH, AdsZoom, Range, CritChance, CritMul, HeadMul;
        public int Mag, Reserve, Pellets, Pierce;
        public bool Suppressed;
    }

    // ------------------------------------------------------------------ zombies

    public enum ZombieType { Walker, Runner, Crawler, Explosive, Armored, Toxic, Military, Mutant, Hunter }

    public enum BossType { None, Butcher, BroodMother, Juggernaut, Wraith }

    public sealed class ZombieDef
    {
        public ZombieType Type;
        public string Name, Desc;
        public float Hp, BodyArmor, HeadMul = 1, Speed, WalkSpeed, Damage, Range = 1.5f, AttackInterval = 1.3f, Scale = 1;
        public int Coins, Xp, UnlockStage;
        public float Weight;
    }

    public sealed class BossDef
    {
        public BossType Type;
        public string Name, Title, Desc;
        public float Hp, BodyArmor, Speed, Damage, Scale;
        public int Coins, Gems, Xp;
    }

    public static class Bestiary
    {
        public static readonly ZombieDef[] All =
        {
            new ZombieDef { Type = ZombieType.Walker, Name = "Walker", Desc = "Slow and relentless. Dangerous in numbers.", Hp = 100, Speed = 1.25f, WalkSpeed = 0.7f, Damage = 10, AttackInterval = 1.4f, Coins = 8, Xp = 10, UnlockStage = 1, Weight = 10 },
            new ZombieDef { Type = ZombieType.Runner, Name = "Runner", Desc = "Fresh infected that sprint and try to surround you.", Hp = 80, Speed = 4.6f, WalkSpeed = 1.2f, Damage = 8, AttackInterval = 0.95f, Coins = 11, Xp = 13, UnlockStage = 3, Weight = 6 },
            new ZombieDef { Type = ZombieType.Crawler, Name = "Crawler", Desc = "Drags itself along the ground and lunges from cover.", Hp = 65, Speed = 2.3f, WalkSpeed = 0.8f, Damage = 12, Range = 1.3f, AttackInterval = 1.1f, Coins = 10, Xp = 12, UnlockStage = 5, Weight = 4 },
            new ZombieDef { Type = ZombieType.Explosive, Name = "Bloater", Desc = "Swollen with volatile gas. Explodes when close or killed.", Hp = 120, Speed = 1.7f, WalkSpeed = 0.7f, Damage = 48, Range = 2.3f, AttackInterval = 2f, Scale = 1.1f, Coins = 15, Xp = 18, UnlockStage = 8, Weight = 2.5f },
            new ZombieDef { Type = ZombieType.Armored, Name = "Riot Shell", Desc = "Riot armor absorbs half of all body damage. Aim for the head.", Hp = 230, BodyArmor = 0.5f, HeadMul = 0.85f, Speed = 1.35f, WalkSpeed = 0.7f, Damage = 18, AttackInterval = 1.5f, Scale = 1.08f, Coins = 22, Xp = 26, UnlockStage = 12, Weight = 2.5f },
            new ZombieDef { Type = ZombieType.Toxic, Name = "Spitter", Desc = "Leaks poison gas, spits acid and leaves a toxic pool when killed.", Hp = 140, Speed = 1.5f, WalkSpeed = 0.7f, Damage = 14, Range = 1.5f, AttackInterval = 1.4f, Coins = 18, Xp = 22, UnlockStage = 16, Weight = 2.2f },
            new ZombieDef { Type = ZombieType.Military, Name = "Fallen Soldier", Desc = "Infected troops in body armor and helmets. Jogs and hits hard.", Hp = 180, BodyArmor = 0.3f, HeadMul = 0.7f, Speed = 2.7f, WalkSpeed = 0.9f, Damage = 16, AttackInterval = 1.2f, Coins = 20, Xp = 24, UnlockStage = 20, Weight = 2.4f },
            new ZombieDef { Type = ZombieType.Mutant, Name = "Mutant", Desc = "A hulking abomination. Charges and slams the ground.", Hp = 950, BodyArmor = 0.15f, HeadMul = 0.8f, Speed = 2.3f, WalkSpeed = 1.1f, Damage = 34, Range = 2.4f, AttackInterval = 2.0f, Scale = 1.55f, Coins = 70, Xp = 80, UnlockStage = 25, Weight = 0.6f },
            new ZombieDef { Type = ZombieType.Hunter, Name = "Stalker", Desc = "Bends light around itself. Only visible up close, then it pounces.", Hp = 170, Speed = 5.3f, WalkSpeed = 1.6f, Damage = 22, Range = 1.8f, AttackInterval = 1.0f, Coins = 34, Xp = 38, UnlockStage = 35, Weight = 1.0f },
        };

        public static ZombieDef Get(ZombieType t) => All[(int)t];

        public static readonly BossDef[] Bosses =
        {
            null,
            new BossDef { Type = BossType.Butcher, Name = "THE BUTCHER", Title = "Abattoir Abomination", Desc = "Charges, slams the ground and hurls wreckage.", Hp = 7000, BodyArmor = 0.1f, Speed = 2.6f, Damage = 40, Scale = 2.0f, Coins = 1500, Gems = 10, Xp = 800 },
            new BossDef { Type = BossType.BroodMother, Name = "BROOD MOTHER", Title = "Plague Incubator", Desc = "Spews acid and births crawlers from her swollen body.", Hp = 6000, BodyArmor = 0.05f, Speed = 1.6f, Damage = 30, Scale = 1.9f, Coins = 1500, Gems = 10, Xp = 800 },
            new BossDef { Type = BossType.Juggernaut, Name = "JUGGERNAUT", Title = "Project Titan Subject 01", Desc = "Armor plating everywhere except the glowing weak points.", Hp = 9000, BodyArmor = 0.55f, Speed = 2.0f, Damage = 45, Scale = 2.1f, Coins = 1800, Gems = 12, Xp = 950 },
            new BossDef { Type = BossType.Wraith, Name = "THE WRAITH", Title = "Apex Stalker", Desc = "Cloaks, dashes and summons stalkers from the dark.", Hp = 5200, BodyArmor = 0.0f, Speed = 5.5f, Damage = 32, Scale = 1.7f, Coins = 1600, Gems = 10, Xp = 850 },
        };

        public static BossDef Boss(BossType t) => Bosses[(int)t];
    }

    // ------------------------------------------------------------------ world

    public enum Biome { City, Metro, Factory, Hospital, MilitaryBase, Mall, Parking, Residential, Airport, ForestCamp, Nuclear, Desert, Port, Lab, Frozen }

    public enum LayoutKind { Streets, Halls, Metro, Yard, Arena }

    public enum Weather { Clear, Rain, Storm, Fog, Snow, Dust, Ash }

    public enum LightMood { Night, Dusk, Overcast, Fluorescent, Emergency, Sodium, Toxic }

    public sealed class BiomeDef
    {
        public Biome Id;
        public string Name, Desc;
        public bool Indoor;
        public LayoutKind[] Layouts;
        public Weather[] Weathers;
        public LightMood[] Moods;
        public float Ceiling;
    }

    public static class Biomes
    {
        public static readonly BiomeDef[] All =
        {
            new BiomeDef { Id = Biome.City, Name = "ABANDONED CITY", Desc = "Downtown streets choked with wrecks.", Layouts = new[] { LayoutKind.Streets }, Weathers = new[] { Weather.Clear, Weather.Rain, Weather.Storm, Weather.Fog }, Moods = new[] { LightMood.Night, LightMood.Dusk, LightMood.Sodium } },
            new BiomeDef { Id = Biome.Metro, Name = "UNDERGROUND METRO", Desc = "A flooded station where the last trains stopped.", Indoor = true, Ceiling = 5.2f, Layouts = new[] { LayoutKind.Metro }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Sodium, LightMood.Fluorescent, LightMood.Emergency } },
            new BiomeDef { Id = Biome.Factory, Name = "INDUSTRIAL FACTORY", Desc = "Rusting assembly halls and silent machines.", Indoor = true, Ceiling = 7f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog, Weather.Ash }, Moods = new[] { LightMood.Sodium, LightMood.Emergency } },
            new BiomeDef { Id = Biome.Hospital, Name = "ST. AUDREY HOSPITAL", Desc = "Wards where the outbreak was first treated.", Indoor = true, Ceiling = 3.6f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Fluorescent, LightMood.Emergency } },
            new BiomeDef { Id = Biome.MilitaryBase, Name = "FORT HALVERSON", Desc = "An overrun military base.", Layouts = new[] { LayoutKind.Yard }, Weathers = new[] { Weather.Clear, Weather.Rain, Weather.Fog, Weather.Storm }, Moods = new[] { LightMood.Night, LightMood.Overcast, LightMood.Dusk } },
            new BiomeDef { Id = Biome.Mall, Name = "PARKVIEW MALL", Desc = "Looted shops and a dead escalator atrium.", Indoor = true, Ceiling = 6.5f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Fluorescent, LightMood.Emergency, LightMood.Sodium } },
            new BiomeDef { Id = Biome.Parking, Name = "UNDERGROUND PARKING", Desc = "Low ceilings, flickering lights, rows of dead cars.", Indoor = true, Ceiling = 3.2f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Fluorescent, LightMood.Sodium, LightMood.Emergency } },
            new BiomeDef { Id = Biome.Residential, Name = "MAPLE HEIGHTS", Desc = "Burned-out suburbs and barricaded homes.", Layouts = new[] { LayoutKind.Streets }, Weathers = new[] { Weather.Clear, Weather.Rain, Weather.Fog, Weather.Ash }, Moods = new[] { LightMood.Dusk, LightMood.Night, LightMood.Overcast } },
            new BiomeDef { Id = Biome.Airport, Name = "KESTREL AIRPORT", Desc = "Wrecked jets on a fog-bound apron.", Layouts = new[] { LayoutKind.Yard }, Weathers = new[] { Weather.Fog, Weather.Rain, Weather.Clear }, Moods = new[] { LightMood.Overcast, LightMood.Night, LightMood.Sodium } },
            new BiomeDef { Id = Biome.ForestCamp, Name = "PINE RIDGE CAMP", Desc = "A survivor camp swallowed by the forest.", Layouts = new[] { LayoutKind.Yard }, Weathers = new[] { Weather.Fog, Weather.Rain, Weather.Clear }, Moods = new[] { LightMood.Night, LightMood.Dusk, LightMood.Overcast } },
            new BiomeDef { Id = Biome.Nuclear, Name = "HELIOS REACTOR", Desc = "A research reactor bathed in warning lights.", Indoor = true, Ceiling = 8f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Toxic, LightMood.Emergency } },
            new BiomeDef { Id = Biome.Desert, Name = "OUTPOST SIERRA", Desc = "A sand-blasted desert checkpoint.", Layouts = new[] { LayoutKind.Yard }, Weathers = new[] { Weather.Dust, Weather.Clear }, Moods = new[] { LightMood.Dusk, LightMood.Overcast, LightMood.Night } },
            new BiomeDef { Id = Biome.Port, Name = "GREYWATER PORT", Desc = "Container stacks and silent cranes.", Layouts = new[] { LayoutKind.Yard }, Weathers = new[] { Weather.Rain, Weather.Fog, Weather.Storm }, Moods = new[] { LightMood.Night, LightMood.Sodium, LightMood.Overcast } },
            new BiomeDef { Id = Biome.Lab, Name = "BLACKSITE LAB", Desc = "Where the virus was engineered.", Indoor = true, Ceiling = 4f, Layouts = new[] { LayoutKind.Halls }, Weathers = new[] { Weather.Clear, Weather.Fog }, Moods = new[] { LightMood.Toxic, LightMood.Fluorescent, LightMood.Emergency } },
            new BiomeDef { Id = Biome.Frozen, Name = "FROZEN CITY", Desc = "A blizzard has buried the dead city in ice.", Layouts = new[] { LayoutKind.Streets }, Weathers = new[] { Weather.Snow }, Moods = new[] { LightMood.Night, LightMood.Overcast } },
        };

        public static BiomeDef Get(Biome b) => All[(int)b];
    }
}
