using System;
using System.Collections.Generic;

namespace UA.Core
{
    public enum ProductKind { Consumable, NonConsumable }

    /// <summary>One Google Play in-app product. The id must match the product id in Play Console.</summary>
    public sealed class ProductDef
    {
        public string Id, Title, Desc, Section, Badge = "", FallbackPrice;
        public ProductKind Kind;
        public long Gems, Coins;
        public WeaponId[] Weapons = new WeaponId[0];
        public Skin[] Skins = new Skin[0];
        public int Outfit = -1, Gloves = -1, BoostDamage, BoostCoins, BoostXp;
        public bool ExtraSlot, CoinDoubler;
    }

    /// <summary>Soft-currency offer (paid with gems or coins inside the game, no real money).</summary>
    public sealed class GemOffer
    {
        public string Id, Title, Desc;
        public Currency Price;
        public long Cost, Coins;
        public int BoostDamage, BoostCoins, BoostXp, Grenades;
        public bool ExtraSlot;
    }

    public static class Store
    {
        public const string CoinDoubler = "coin_doubler";
        public const string ExtraSlotId = "extra_loadout_slot";

        public static readonly string[] Sections = { "weapons", "gems", "coins", "packs", "cosmetics", "boosters" };
        public static readonly string[] SectionTitles = { "WEAPONS", "GEMS", "COINS", "PACKS", "COSMETICS", "BOOSTERS" };

        public static readonly ProductDef[] Products =
        {
            new ProductDef { Id = "gems_100", Title = "100 GEMS", Desc = "A handful of gems.", Section = "gems", Kind = ProductKind.Consumable, Gems = 100, FallbackPrice = "$0.99" },
            new ProductDef { Id = "gems_500", Title = "500 GEMS", Desc = "+50 bonus gems.", Section = "gems", Kind = ProductKind.Consumable, Gems = 550, FallbackPrice = "$4.99" },
            new ProductDef { Id = "gems_1000", Title = "1,000 GEMS", Desc = "+200 bonus gems.", Section = "gems", Kind = ProductKind.Consumable, Gems = 1200, Badge = "POPULAR", FallbackPrice = "$9.99" },
            new ProductDef { Id = "gems_2500", Title = "2,500 GEMS", Desc = "+500 bonus gems.", Section = "gems", Kind = ProductKind.Consumable, Gems = 3000, FallbackPrice = "$19.99" },
            new ProductDef { Id = "gems_5000", Title = "5,000 GEMS", Desc = "+1,500 bonus gems.", Section = "gems", Kind = ProductKind.Consumable, Gems = 6500, Badge = "BEST VALUE", FallbackPrice = "$39.99" },

            new ProductDef { Id = "coins_small", Title = "25,000 COINS", Desc = "A crate of coins.", Section = "coins", Kind = ProductKind.Consumable, Coins = 25000, FallbackPrice = "$1.99" },
            new ProductDef { Id = "coins_large", Title = "120,000 COINS", Desc = "A vault of coins.", Section = "coins", Kind = ProductKind.Consumable, Coins = 120000, Badge = "POPULAR", FallbackPrice = "$7.99" },
            new ProductDef { Id = "coins_mega", Title = "400,000 COINS", Desc = "The war chest.", Section = "coins", Kind = ProductKind.Consumable, Coins = 400000, Badge = "BEST VALUE", FallbackPrice = "$19.99" },

            new ProductDef { Id = "pack_starter", Title = "STARTER PACK", Desc = "R4 Carbine, 20,000 coins and 200 gems. One per player.", Section = "packs", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.R4Carbine }, Coins = 20000, Gems = 200, Badge = "x5 VALUE", FallbackPrice = "$2.99" },
            new ProductDef { Id = "pack_survival", Title = "SURVIVAL PACK", Desc = "Ironclad Pump-12, Hurricane AS-20, 50,000 coins and 3 damage boosts.", Section = "packs", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.PumpShotgun, WeaponId.AutoShotgun }, Coins = 50000, BoostDamage = 3, FallbackPrice = "$6.99" },
            new ProductDef { Id = "pack_military", Title = "MILITARY PACK", Desc = "Titan HMG, Hawkeye DMR, Black Ops outfit, 100,000 coins and 500 gems.", Section = "packs", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.HeavyMG, WeaponId.SemiSniper }, Outfit = 3, Coins = 100000, Gems = 500, FallbackPrice = "$14.99" },
            new ProductDef { Id = "pack_legendary", Title = "LEGENDARY PACK", Desc = "Arc Pulse Rifle, Behemoth .50, Dragon skin, Gold gloves and 1,500 gems.", Section = "packs", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.ArcRifle, WeaponId.HeavySniper }, Skins = new[] { Skin.Dragon }, Gloves = 4, Gems = 1500, Badge = "LEGENDARY", FallbackPrice = "$29.99" },

            new ProductDef { Id = "bundle_heavy", Title = "HEAVY WEAPONS BUNDLE", Desc = "Juggernaut 10-Gauge and Titan HMG.", Section = "weapons", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.HeavyShotgun, WeaponId.HeavyMG }, FallbackPrice = "$9.99" },
            new ProductDef { Id = "bundle_special", Title = "SPECIAL OPS BUNDLE", Desc = "Inferno FT-6, Thunder GL-6 and Arc Pulse Rifle.", Section = "weapons", Kind = ProductKind.NonConsumable, Weapons = new[] { WeaponId.Flamethrower, WeaponId.Launcher, WeaponId.ArcRifle }, Badge = "BEST VALUE", FallbackPrice = "$12.99" },
            new ProductDef { Id = "skins_elite", Title = "ELITE SKIN COLLECTION", Desc = "Gold, Carbon and Toxic skins for every weapon you own.", Section = "cosmetics", Kind = ProductKind.NonConsumable, Skins = new[] { Skin.Gold, Skin.Carbon, Skin.Toxic }, FallbackPrice = "$4.99" },

            new ProductDef { Id = CoinDoubler, Title = "COIN DOUBLER", Desc = "Permanently doubles every coin you earn.", Section = "boosters", Kind = ProductKind.NonConsumable, CoinDoubler = true, Badge = "PERMANENT", FallbackPrice = "$4.99" },
            new ProductDef { Id = ExtraSlotId, Title = "EXTRA LOADOUT SLOT", Desc = "Carry a third weapon into every stage.", Section = "boosters", Kind = ProductKind.NonConsumable, ExtraSlot = true, FallbackPrice = "$2.99" },
        };

        public static readonly GemOffer[] Offers =
        {
            new GemOffer { Id = "boost_coins", Title = "DOUBLE COINS x3", Desc = "Double coin rewards for your next 3 stages.", Price = Currency.Gems, Cost = 30, BoostCoins = 3 },
            new GemOffer { Id = "boost_damage", Title = "DAMAGE BOOST x3", Desc = "+25% weapon damage for your next 3 stages.", Price = Currency.Gems, Cost = 40, BoostDamage = 3 },
            new GemOffer { Id = "boost_xp", Title = "XP BOOST x3", Desc = "Double XP for your next 3 stages.", Price = Currency.Gems, Cost = 25, BoostXp = 3 },
            new GemOffer { Id = "grenades", Title = "GRENADE CRATE", Desc = "Refill to 9 frag grenades.", Price = Currency.Coins, Cost = 1500, Grenades = 9 },
            new GemOffer { Id = "slot_gems", Title = "EXTRA LOADOUT SLOT", Desc = "Carry a third weapon into every stage.", Price = Currency.Gems, Cost = 300, ExtraSlot = true },
            new GemOffer { Id = "exchange_s", Title = "10,000 COINS", Desc = "Exchange gems for coins.", Price = Currency.Gems, Cost = 50, Coins = 10000 },
            new GemOffer { Id = "exchange_l", Title = "60,000 COINS", Desc = "Exchange gems for coins (+20%).", Price = Currency.Gems, Cost = 250, Coins = 60000 },
        };

        public static ProductDef Product(string id)
        {
            foreach (var p in Products) if (p.Id == id) return p;
            return null;
        }

        public static bool IsOwned(SaveData d, ProductDef p) => p.Kind == ProductKind.NonConsumable && d.OwnsProduct(p.Id);

        /// <summary>
        /// Grants a verified purchase exactly once. <paramref name="transactionId"/> is the store's
        /// order id; a repeat delivery of the same transaction (app restart, restore) grants nothing.
        /// Non-consumables are recorded as entitlements so a restore re-grants the items, never currency twice.
        /// </summary>
        public static bool Grant(SaveData d, string productId, string transactionId, bool isRestore)
        {
            var p = Product(productId);
            if (p == null) return false;
            if (!string.IsNullOrEmpty(transactionId) && d.ProcessedTx.Contains(transactionId)) return false;
            bool alreadyOwned = p.Kind == ProductKind.NonConsumable && d.OwnsProduct(p.Id);
            if (!string.IsNullOrEmpty(transactionId))
            {
                d.ProcessedTx.Add(transactionId);
                if (d.ProcessedTx.Count > 300) d.ProcessedTx.RemoveAt(0);
            }
            // Items and entitlements (idempotent).
            foreach (var w in p.Weapons) d.GrantWeapon(w);
            foreach (var s in p.Skins) Wallet.GrantSkinAll(d, s);
            if (p.Outfit >= 0 && Array.IndexOf(d.OwnedOutfits, p.Outfit) < 0)
            {
                var l = new List<int>(d.OwnedOutfits) { p.Outfit };
                d.OwnedOutfits = l.ToArray();
            }
            if (p.Gloves >= 0 && Array.IndexOf(d.OwnedGloves, p.Gloves) < 0)
            {
                var l = new List<int>(d.OwnedGloves) { p.Gloves };
                d.OwnedGloves = l.ToArray();
            }
            if (p.ExtraSlot) d.ExtraSlots = 1;
            if (p.Kind == ProductKind.NonConsumable && !d.OwnsProduct(p.Id)) d.OwnedProducts.Add(p.Id);
            // Currency and boosters only on a fresh purchase (never on a restore of something already owned).
            if (!alreadyOwned && !isRestore)
            {
                Wallet.Add(d, Currency.Gems, p.Gems);
                Wallet.Add(d, Currency.Coins, p.Coins);
                d.BoostDamage += p.BoostDamage;
                d.BoostCoins += p.BoostCoins;
                d.BoostXp += p.BoostXp;
            }
            return true;
        }

        public static bool BuyOffer(SaveData d, GemOffer o)
        {
            if (o.ExtraSlot && d.ExtraSlots > 0) return false;
            if (o.Grenades > 0 && d.Grenades >= o.Grenades) return false;
            if (!Wallet.Spend(d, o.Price, o.Cost)) return false;
            Wallet.Add(d, Currency.Coins, o.Coins);
            d.BoostCoins += o.BoostCoins;
            d.BoostDamage += o.BoostDamage;
            d.BoostXp += o.BoostXp;
            if (o.Grenades > 0) d.Grenades = o.Grenades;
            if (o.ExtraSlot) d.ExtraSlots = 1;
            return true;
        }

        /// <summary>Consumes one stage from each active booster (called when a stage ends).</summary>
        public static void TickBoosters(SaveData d)
        {
            if (d.BoostCoins > 0) d.BoostCoins--;
            if (d.BoostDamage > 0) d.BoostDamage--;
            if (d.BoostXp > 0) d.BoostXp--;
        }
    }
}
