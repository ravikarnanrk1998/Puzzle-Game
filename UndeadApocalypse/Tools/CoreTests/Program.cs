using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UA.Core;

// Engine-free tests for the UA.Core rules: run with `dotnet run -c Release` from this folder.
static class Program
{
    static int _fail;

    static void Check(bool ok, string what)
    {
        if (ok) return;
        _fail++;
        Console.WriteLine("FAIL: " + what);
    }

    static int Main(string[] args)
    {
        var sw = Stopwatch.StartNew();
        Curves();
        Generation();
        Determinism();
        HugeStages();
        Modes();
        Economy();
        Store_();
        MissionsAndLogin();
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "ALL TESTS PASSED" : _fail + " FAILURES");
        Console.WriteLine("time " + sw.ElapsedMilliseconds + " ms");
        return _fail == 0 ? 0 : 1;
    }

    static void Curves()
    {
        Console.WriteLine("stage     hp     dmg   speed  wave waves tier alive reward  boss  elite");
        foreach (long s in new long[] { 1, 2, 5, 10, 20, 25, 30, 50, 75, 100, 150, 200, 300, 500, 1000, 5000, 100000, 1000000000 })
        {
            Console.WriteLine($"{s,10} {Difficulty.Health(s),6:0.00} {Difficulty.Damage(s),5:0.00} {Difficulty.Speed(s),5:0.00} {Difficulty.WaveSize(s),5} {Difficulty.WaveCount(s),5} {Difficulty.Tier(s),4} {Difficulty.MaxAlive(s),5} {Difficulty.Reward(s),6:0.00} {(Difficulty.IsBoss(s) ? Difficulty.BossFor(s).ToString() : "-"),11} {Difficulty.IsElite(s)}");
            Check(Difficulty.Health(s) <= Difficulty.MaxHealthMul + 0.001, "health cap " + s);
            Check(Difficulty.Damage(s) <= Difficulty.MaxDamageMul + 0.001, "damage cap " + s);
            Check(Difficulty.Speed(s) <= Difficulty.MaxSpeedMul + 0.001, "speed cap " + s);
        }
        // Monotonic, never decreasing.
        float prev = 0;
        for (long s = 1; s < 3000; s++)
        {
            float h = Difficulty.Health(s);
            Check(h >= prev - 1e-5f, "health monotonic at " + s);
            prev = h;
        }
        Check(Difficulty.IsElite(25) && !Difficulty.IsElite(50) && Difficulty.IsBoss(50) && Difficulty.IsBoss(100), "elite/boss cadence");
        Check(Difficulty.BossFor(50) == BossType.Butcher && Difficulty.BossFor(100) == BossType.BroodMother && Difficulty.BossFor(250) == BossType.Butcher, "boss rotation");
        // Composition shifts every 10 stages: walkers get rarer, armored types appear.
        float w1 = Difficulty.Weight(Bestiary.Get(ZombieType.Walker), 5), w2 = Difficulty.Weight(Bestiary.Get(ZombieType.Walker), 95);
        Check(w2 < w1, "walker weight drops with tier");
        Check(Difficulty.Weight(Bestiary.Get(ZombieType.Armored), 11) == 0 && Difficulty.Weight(Bestiary.Get(ZombieType.Armored), 12) > 0, "armored unlock at 12");
    }

    static void Generation()
    {
        ulong seed = 0xC0FFEE;
        var biomeCount = new int[15];
        var objCount = new int[9];
        int minOpen = int.MaxValue, maxOpen = 0, maxSpawns = 0;
        for (long s = 1; s <= 1500; s++)
        {
            var p = StageGen.Survival(s, seed);
            biomeCount[(int)p.Biome]++;
            objCount[(int)p.Objective]++;
            var g = p.Grid;
            int open = g.OpenCount;
            minOpen = Math.Min(minOpen, open);
            maxOpen = Math.Max(maxOpen, open);
            Check(open >= 18, $"stage {s} too small ({open})");
            Check(g.Open[p.StartCell], $"stage {s} start not open");
            var d = g.Distances(p.StartCell);
            for (int i = 0; i < g.Open.Length; i++) if (g.Open[i] && d[i] < 0) { Check(false, $"stage {s} unreachable cell {i}"); break; }
            foreach (int c in p.SpawnCells) Check(g.Open[c] && c != p.StartCell, $"stage {s} bad spawn cell");
            foreach (var l in p.Loot) Check(g.Open[l.Cell], $"stage {s} loot in wall");
            foreach (var o in p.ObjectiveSpots) Check(g.Open[o.Cell], $"stage {s} objective in wall");
            Check(p.Waves.Count >= 3 && p.TotalSpawns > 0, $"stage {s} waves");
            Check(!Difficulty.IsBoss(s) || p.Objective == ObjectiveType.Boss, $"stage {s} boss objective");
            Check(!Difficulty.IsElite(s) || p.Objective == ObjectiveType.Elite, $"stage {s} elite objective");
            if (p.Objective == ObjectiveType.Rescue || p.Objective == ObjectiveType.Supplies || p.Objective == ObjectiveType.Nests)
                Check(p.ObjectiveCount > 0 && p.ObjectiveSpots.Count == p.ObjectiveCount, $"stage {s} objective spots");
            maxSpawns = Math.Max(maxSpawns, p.TotalSpawns);
            // No zombie type before its unlock stage.
            foreach (var w in p.Waves) foreach (var e in w.Spawns) Check(Bestiary.Get(e.Type).UnlockStage <= s, $"stage {s} early {e.Type}");
        }
        Console.WriteLine();
        Console.WriteLine("biomes (1500 stages): " + string.Join(", ", Enumerable.Range(0, 15).Select(i => (Biome)i + "=" + biomeCount[i])));
        Console.WriteLine("objectives: " + string.Join(", ", Enumerable.Range(0, 9).Select(i => (ObjectiveType)i + "=" + objCount[i])));
        Console.WriteLine($"open cells {minOpen}..{maxOpen}, max spawns per stage {maxSpawns}");
        foreach (var c in biomeCount) Check(c > 0, "every biome appears");
        // Consecutive stages rarely share an exact layout.
        int same = 0;
        for (long s = 31; s < 600; s++)
        {
            var a = StageGen.Survival(s, seed).Grid;
            var b = StageGen.Survival(s + 1, seed).Grid;
            if (a.W == b.W && a.H == b.H && a.Open.SequenceEqual(b.Open)) same++;
        }
        Check(same == 0, "identical consecutive layouts: " + same);
        PrintGrid(StageGen.Survival(2, seed));
        PrintGrid(StageGen.Survival(7, seed));
        PrintGrid(StageGen.Survival(16, seed));
    }

    static void PrintGrid(StagePlan p)
    {
        var g = p.Grid;
        Console.WriteLine();
        Console.WriteLine($"{p.Title} {p.Subtitle} {p.Kind} {p.Weather}/{p.Mood} event={p.Event} obj={p.ObjectiveText} spawns={p.TotalSpawns}");
        var spots = new HashSet<int>(p.ObjectiveSpots.Select(o => o.Cell));
        var loot = new HashSet<int>(p.Loot.Select(o => o.Cell));
        for (int y = g.H - 1; y >= 0; y--)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < g.W; x++)
            {
                int i = g.Idx(x, y);
                char c = !g.Open[i] ? '#' : i == p.StartCell ? 'S' : i == p.ExitCell ? 'E' : spots.Contains(i) ? 'O' : g.Has(i, CellTag.Obstacle) ? 'x' : loot.Contains(i) ? '+' : g.Has(i, CellTag.Road) ? '=' : g.Has(i, CellTag.Track) ? '~' : '.';
                sb.Append(c).Append(' ');
            }
            Console.WriteLine("  " + sb);
        }
    }

    static void Determinism()
    {
        foreach (long s in new long[] { 1, 13, 50, 77, 333, 1000 })
        {
            var a = StageGen.Survival(s, 42);
            var b = StageGen.Survival(s, 42);
            Check(a.Biome == b.Biome && a.Kind == b.Kind && a.Objective == b.Objective && a.Event == b.Event, "determinism meta " + s);
            Check(a.Grid.Open.SequenceEqual(b.Grid.Open), "determinism grid " + s);
            Check(a.TotalSpawns == b.TotalSpawns && a.Waves.SelectMany(w => w.Spawns).Select(e => e.Type).SequenceEqual(b.Waves.SelectMany(w => w.Spawns).Select(e => e.Type)), "determinism waves " + s);
            Check(a.Loot.Select(l => l.Cell).SequenceEqual(b.Loot.Select(l => l.Cell)), "determinism loot " + s);
            var c = StageGen.Survival(s, 43);
            Check(!(c.Grid.Open.SequenceEqual(a.Grid.Open) && c.Biome == a.Biome && c.Grid.W == a.Grid.W), "different world seed changes stage " + s);
        }
    }

    static void HugeStages()
    {
        foreach (long s in new long[] { 10_000, 999_999, 1_000_000_000, long.MaxValue / 4, long.MaxValue - 1 })
        {
            try
            {
                var p = StageGen.Survival(s, 99);
                Check(p.TotalSpawns > 0 && p.TotalSpawns < 400, "huge stage spawns " + s + " = " + p.TotalSpawns);
                Check(!float.IsNaN(p.HpMul) && !float.IsInfinity(p.HpMul) && p.HpMul <= 20.01f, "huge hp " + s);
                Check(!float.IsNaN(p.RewardMul) && p.RewardMul > 0 && p.RewardMul < 100, "huge reward " + s);
                var ps = PlayerProgress.Compute(NewSave());
                var r = Rewards.ForStage(p, true, new long[9] { 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000 }, 999999, 1, ps, true);
                Check(r.Coins >= 0 && r.Coins <= Safe.MaxCurrency, "huge reward coins " + s);
                if (p.IsBoss) Check(!float.IsNaN(p.BossHpMul) && p.BossHpMul < 100, "boss hp " + s);
            }
            catch (Exception e)
            {
                Check(false, "huge stage " + s + " threw " + e.GetType().Name + ": " + e.Message);
            }
        }
        Console.WriteLine("huge stages OK");
    }

    static void Modes()
    {
        for (int i = 0; i < Campaign.All.Length; i++)
        {
            var p = Campaign.Plan(i);
            Check(p.Objective == Campaign.All[i].Objective, "campaign objective " + i);
            Check(p.Biome == Campaign.All[i].Biome, "campaign biome " + i);
            Check(p.Grid.OpenCount >= 18, "campaign grid " + i);
        }
        for (long day = 20000; day < 20400; day++)
        {
            var p = DailyChallenge.Plan(day, 40);
            Check(p.Mode == GameMode.Daily && p.RewardMul >= 2, "daily " + day);
            Check(DailyChallenge.Plan(day, 40).Seed == p.Seed, "daily determinism");
        }
        for (int r = 1; r <= 40; r++)
        {
            var p = BossRush.Plan(r);
            Check(p.IsBoss && p.Objective == ObjectiveType.Boss, "boss rush " + r);
        }
        Console.WriteLine("campaign: " + Campaign.All.Length + " missions; daily sample: " + DailyChallenge.Describe(DailyChallenge.Plan(20400, 40)));
    }

    static SaveData NewSave()
    {
        var d = new SaveData { Created = 1 };
        d.Repair();
        return d;
    }

    static void Economy()
    {
        var d = NewSave();
        Check(d.Owns(WeaponId.StandardPistol) && d.Loadout[0] == 0, "starter pistol");
        long c0 = d.Coins;
        Check(!Wallet.BuyWeapon(d, WeaponId.R4Carbine), "cannot buy locked weapon");
        d.Level = 10;
        d.Coins = 100000;
        Check(Wallet.BuyWeapon(d, WeaponId.R4Carbine) && d.Owns(WeaponId.R4Carbine), "buy carbine");
        Check(!Wallet.BuyWeapon(d, WeaponId.R4Carbine), "no double buy");
        Check(Wallet.UpgradeWeapon(d, WeaponId.R4Carbine, WStat.Damage), "upgrade");
        Check(!Wallet.Spend(d, Currency.Coins, -50), "negative spend rejected");
        d.Coins = 5;
        Check(!Wallet.UpgradeWeapon(d, WeaponId.R4Carbine, WStat.Damage), "cannot afford");
        Check(d.Coins == 5, "no charge on failure");
        d.Coins = Safe.MaxCurrency;
        Wallet.Add(d, Currency.Coins, long.MaxValue);
        Check(d.Coins == Safe.MaxCurrency, "currency cap");
        // XP: many level-ups at once.
        var e = NewSave();
        int ups = PlayerProgress.AddXp(e, 5_000_000);
        Check(ups > 10 && e.SkillPoints == ups, "level ups " + ups);
        Check(Skill.Unlock(e, SkillId.Vitality) && !Skill.Unlock(e, SkillId.Regeneration), "skill prerequisite");
        Skill.Unlock(e, SkillId.Vitality);
        Check(Skill.Unlock(e, SkillId.Regeneration), "skill unlock after prereq");
        var ps = PlayerProgress.Compute(e);
        Check(ps.MaxHealth > 100 && ps.Regen > 0, "skills apply");
        // Weapon stats.
        var ws = PlayerProgress.Weapon(d, WeaponId.R4Carbine, PlayerProgress.Compute(d));
        Check(ws.Damage > Weapons.Get(WeaponId.R4Carbine).Damage, "upgraded damage");
        Console.WriteLine($"upgrade cost curve (R4 damage): {string.Join(", ", Enumerable.Range(0, 10).Select(l => Weapons.UpgradeCost(Weapons.Get(WeaponId.R4Carbine), WStat.Damage, l)))}");
        Console.WriteLine($"player upgrade curve: {string.Join(", ", Enumerable.Range(0, 15).Select(l => PlayerProgress.UpgradeCost(PStat.Health, l)))}");
        // Typical stage payout.
        var plan = StageGen.Survival(10, 1);
        var kills = new long[9];
        foreach (var w in plan.Waves) foreach (var s in w.Spawns) kills[(int)s.Type]++;
        var r = Rewards.ForStage(plan, true, kills, 20, 0, PlayerProgress.Compute(NewSave()), true);
        Console.WriteLine($"stage 10 payout: {r.Coins} coins ({r.KillCoins} kills + {r.ClearCoins} clear), {r.Xp} xp");
        Check(r.Coins > 0 && r.Xp > 0, "stage payout");
        // Save round trip through JSON (field-based, like JsonUtility).
        var opts = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var json = System.Text.Json.JsonSerializer.Serialize(d, opts);
        var back = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json, opts);
        back.Repair();
        Check(back.Owns(WeaponId.R4Carbine) && back.Weapon(WeaponId.R4Carbine).Levels[0] == 1, "save round trip");
        // Repair clamps edited values.
        back.Coins = -500;
        back.Gems = long.MaxValue;
        back.Weapons[0].Levels[0] = 9999;
        back.Repair();
        Check(back.Coins == 0 && back.Gems == Safe.MaxCurrency && back.Weapons[0].Levels[0] == Weapons.MaxLevel, "repair clamps");
    }

    static void Store_()
    {
        var d = NewSave();
        long g0 = d.Gems;
        Check(Store.Grant(d, "gems_500", "GPA.1", false) && d.Gems == g0 + 550, "grant gems");
        Check(!Store.Grant(d, "gems_500", "GPA.1", false) && d.Gems == g0 + 550, "duplicate transaction ignored");
        Check(Store.Grant(d, "gems_500", "GPA.2", false) && d.Gems == g0 + 1100, "second purchase grants");
        long c = d.Coins;
        Check(Store.Grant(d, "pack_starter", "GPA.3", false) && d.Owns(WeaponId.R4Carbine) && d.Coins == c + 20000, "starter pack");
        Check(Store.Grant(d, "pack_starter", "GPA.4", true) && d.Coins == c + 20000, "restore does not re-grant currency");
        Check(!Store.Grant(d, "nonexistent", "GPA.5", false), "unknown product");
        Check(Store.Grant(d, "extra_loadout_slot", "GPA.6", false) && d.LoadoutSlots == 3, "extra slot");
        Check(Store.Grant(d, "coin_doubler", "GPA.7", false) && PlayerProgress.Compute(d).CoinMul >= 2, "coin doubler");
        d.Gems = 100;
        Check(Store.BuyOffer(d, Store.Offers[0]) && d.BoostCoins == 3 && d.Gems == 70, "gem offer");
        Store.TickBoosters(d);
        Check(d.BoostCoins == 2, "booster ticks");
        var ids = new HashSet<string>();
        foreach (var p in Store.Products) Check(ids.Add(p.Id) && p.Id == p.Id.ToLowerInvariant() && !p.Id.Contains(" "), "product id " + p.Id);
    }

    static void MissionsAndLogin()
    {
        var d = NewSave();
        long today = 20400;
        Missions.Refresh(d, today);
        Check(d.Missions.Count(m => !m.Weekly) == 3 && d.Missions.Count(m => m.Weekly) == 3, "missions rolled");
        var first = d.Missions[0];
        Check(!Missions.Claim(d, first.Id), "cannot claim unfinished");
        Missions.Track(d, (MissionKind)first.Kind, first.Param, first.Target * 2);
        Check(first.Progress == first.Target, "progress clamps");
        long coins = d.Coins;
        Check(Missions.Claim(d, first.Id) && d.Coins > coins, "claim mission");
        Check(!Missions.Claim(d, first.Id), "no double claim");
        Missions.Refresh(d, today - 3);
        Check(d.MissionDay == today, "clock rollback ignored");
        Check(Missions.ClaimLogin(d, today, out var lc, out _) == 1, "login day 1");
        Check(Missions.ClaimLogin(d, today, out _, out _) == 0, "login once per day");
        Check(Missions.ClaimLogin(d, today - 1, out _, out _) == 0, "login rollback");
        Check(Missions.ClaimLogin(d, today + 1, out _, out _) == 2 && d.LoginStreak == 2, "login streak");
        Check(Missions.ClaimLogin(d, today + 5, out _, out _) == 1 && d.LoginStreak == 1, "streak reset");
        d.Stats.Kills = 150;
        Check(Achievements.CanClaim(d, 0) && Achievements.Claim(d, 0) && !Achievements.Claim(d, 0), "achievement once");
        Console.WriteLine("missions: " + string.Join(" | ", d.Missions.Select(Missions.Describe)));
    }
}
