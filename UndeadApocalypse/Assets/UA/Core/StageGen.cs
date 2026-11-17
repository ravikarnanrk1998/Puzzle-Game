using System;
using System.Collections.Generic;

namespace UA.Core
{
    public enum ObjectiveType { Eliminate, Survive, Rescue, Supplies, Nests, Defend, Extract, Elite, Boss }

    public enum StageEvent { None, Blackout, HordeRush, ToxicFog, SupplyDrop, BloodMoon, Thunderstorm }

    [Flags]
    public enum Mutator { None = 0, HeadshotFocus = 1, Swift = 2, NoRegen = 4, ExplosiveHorde = 8, Darkness = 16, RunnerRush = 32, ArmoredHorde = 64 }

    public enum LootKind { Ammo, Health, Armor, Coins, Grenades }

    public enum HazardKind { ExplosiveBarrel, ToxicPool, FireBarrel, Sparks }

    [Flags]
    public enum CellTag : byte { None = 0, Road = 1, Plaza = 2, Room = 4, Corridor = 8, Platform = 16, Track = 32, Obstacle = 64, Pillars = 128 }

    /// <summary>The stage floor plan: a grid of 6 m cells, each open (walkable) or solid (wall / building).</summary>
    public sealed class Grid
    {
        public readonly int W, H;
        public readonly bool[] Open;
        public readonly CellTag[] Tags;
        public readonly int[] Room;
        public int RoomCount;

        public Grid(int w, int h)
        {
            W = w;
            H = h;
            Open = new bool[w * h];
            Tags = new CellTag[w * h];
            Room = new int[w * h];
            for (int i = 0; i < Room.Length; i++) Room[i] = -1;
        }

        public int Idx(int x, int y) => y * W + x;
        public int X(int i) => i % W;
        public int Y(int i) => i / W;
        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public bool IsOpen(int x, int y) => In(x, y) && Open[Idx(x, y)];
        public bool Has(int i, CellTag t) => (Tags[i] & t) != 0;

        public void Carve(int x, int y, CellTag tag = CellTag.None)
        {
            if (x < 1 || y < 1 || x > W - 2 || y > H - 2) return;
            int i = Idx(x, y);
            Open[i] = true;
            Tags[i] |= tag;
        }

        public int OpenCount
        {
            get
            {
                int n = 0;
                foreach (var o in Open) if (o) n++;
                return n;
            }
        }

        static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

        /// <summary>Breadth-first walking distance (in cells) from one cell; -1 where unreachable.</summary>
        public int[] Distances(int from)
        {
            var d = new int[W * H];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            if (from < 0 || !Open[from]) return d;
            var q = new Queue<int>();
            d[from] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int cx = X(c), cy = Y(c);
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (!IsOpen(nx, ny)) continue;
                    int n = Idx(nx, ny);
                    if (d[n] >= 0) continue;
                    d[n] = d[c] + 1;
                    q.Enqueue(n);
                }
            }
            return d;
        }

        /// <summary>Closes every open cell that cannot be walked to from <paramref name="from"/>.</summary>
        public void KeepReachable(int from)
        {
            var d = Distances(from);
            for (int i = 0; i < Open.Length; i++)
            {
                if (Open[i] && d[i] < 0)
                {
                    Open[i] = false;
                    Tags[i] = CellTag.None;
                    Room[i] = -1;
                }
            }
        }

        public int OpenNeighbours(int i)
        {
            int n = 0, x = X(i), y = Y(i);
            for (int k = 0; k < 4; k++) if (IsOpen(x + DX[k], y + DY[k])) n++;
            return n;
        }
    }

    /// <summary>A point inside a cell (fx, fy in 0..1) with a kind code.</summary>
    public struct Spot
    {
        public int Cell;
        public float Fx, Fy;
        public int Kind;

        public Spot(int cell, float fx, float fy, int kind)
        {
            Cell = cell;
            Fx = fx;
            Fy = fy;
            Kind = kind;
        }
    }

    public struct SpawnEntry
    {
        public ZombieType Type;
        public bool Elite;
    }

    public sealed class Wave
    {
        public readonly List<SpawnEntry> Spawns = new List<SpawnEntry>();
        public float Interval = 1.2f, Delay = 3f;
    }

    /// <summary>Everything needed to build and run one stage. Produced deterministically from a seed.</summary>
    public sealed class StagePlan
    {
        public GameMode Mode;
        public long Stage;
        public ulong Seed;
        public Biome Biome;
        public LayoutKind Kind;
        public Grid Grid;
        public Weather Weather;
        public LightMood Mood;
        public StageEvent Event;
        public Mutator Mutators;
        public ObjectiveType Objective;
        public int ObjectiveCount;
        public float ObjectiveTime;
        public readonly List<Wave> Waves = new List<Wave>();
        public BossType Boss;
        public int BossComplexity;
        public ZombieType EliteType;
        public int EliteCount;
        public float HpMul = 1, DmgMul = 1, SpeedMul = 1, BossHpMul = 1, EliteChance, RewardMul = 1;
        public int MaxAlive = 12;
        public int StartCell, ExitCell;
        public readonly List<int> SpawnCells = new List<int>();
        public readonly List<Spot> ObjectiveSpots = new List<Spot>();
        public readonly List<Spot> Loot = new List<Spot>();
        public readonly List<Spot> Hazards = new List<Spot>();
        public int AllowedClass = -1;
        public int CampaignMission = -1;
        public long DailyDay;
        public string Title = "", Subtitle = "", Briefing = "", ObjectiveText = "";

        public BiomeDef BiomeDef => Biomes.Get(Biome);
        public bool Indoor => BiomeDef.Indoor;
        public bool IsBoss => Boss != BossType.None;
        public int TotalSpawns
        {
            get
            {
                int n = 0;
                foreach (var w in Waves) n += w.Spawns.Count;
                return n;
            }
        }
    }

    /// <summary>Inputs for <see cref="StageGen.Build"/>; anything left null is chosen from the seed.</summary>
    public sealed class GenParams
    {
        public GameMode Mode = GameMode.Survival;
        public long Stage = 1;
        public ulong Seed;
        public Biome? Biome;
        public LayoutKind? Layout;
        public ObjectiveType? Objective;
        public StageEvent? Event;
        public Weather? Weather;
        public LightMood? Mood;
        public Mutator Mutators;
        public BossType Boss = BossType.None;
        public bool Elite;
        public int AllowedClass = -1;
        public float RewardMul = 1;
        public ZombieType[] ForcedTypes;
        public float SurviveTime;
    }

    public static class StageGen
    {
        public const float CellSize = 6f;

        static readonly Biome[] IntroOrder =
        {
            Biome.City, Biome.Metro, Biome.Residential, Biome.Hospital, Biome.Parking, Biome.Factory, Biome.Mall, Biome.MilitaryBase,
            Biome.ForestCamp, Biome.Port, Biome.Airport, Biome.Desert, Biome.Lab, Biome.Nuclear, Biome.Frozen,
        };

        /// <summary>Biome for an infinite-mode stage: the first 30 stages tour every biome, then a seeded mix.</summary>
        public static Biome SurvivalBiome(long stage, ulong worldSeed)
        {
            if (stage <= 30) return IntroOrder[(int)((stage - 1) / 2 % IntroOrder.Length)];
            long block = (stage - 1) / 2;
            int Raw(long b) => (int)(Rng.Hash((long)worldSeed, b, 77) % 15);
            int pick = Raw(block);
            int prev = Raw(block - 1);
            if (pick == prev) pick = (pick + 1 + (int)(Rng.Hash((long)worldSeed, block, 78) % 14)) % 15;
            return (Biome)pick;
        }

        public static StagePlan Survival(long stage, ulong worldSeed)
        {
            stage = Math.Max(1, stage);
            var p = new GenParams
            {
                Mode = GameMode.Survival,
                Stage = stage,
                Seed = Rng.Hash((long)worldSeed, stage, 1),
                Biome = SurvivalBiome(stage, worldSeed),
            };
            if (Difficulty.IsBoss(stage)) p.Boss = Difficulty.BossFor(stage);
            else if (Difficulty.IsElite(stage)) p.Elite = true;
            if (stage == 1) p.Objective = ObjectiveType.Eliminate;
            if (stage <= 3) p.Event = StageEvent.None;
            return Build(p);
        }

        public static StagePlan Build(GenParams gp)
        {
            var r = new Rng(gp.Seed);
            long s = Math.Max(1, gp.Stage);
            var plan = new StagePlan { Mode = gp.Mode, Stage = s, Seed = gp.Seed, Mutators = gp.Mutators, AllowedClass = gp.AllowedClass };
            plan.Biome = gp.Biome ?? (Biome)r.Range(0, 15);
            var bd = Biomes.Get(plan.Biome);
            plan.Boss = gp.Boss;
            bool boss = gp.Boss != BossType.None;
            plan.Kind = gp.Layout ?? (boss ? LayoutKind.Arena : r.Pick(bd.Layouts));
            plan.Weather = gp.Weather ?? r.Pick(bd.Weathers);
            plan.Mood = gp.Mood ?? r.Pick(bd.Moods);

            // Floor plan (retry with derived seeds in the rare case it comes out too small).
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var lr = new Rng(Rng.Hash((long)gp.Seed, attempt, 11));
                plan.Grid = Layout(lr, plan.Kind, plan.Biome, s, out plan.StartCell);
                if (plan.Grid.OpenCount >= 18) break;
            }
            var g = plan.Grid;
            var dist = g.Distances(plan.StartCell);
            int maxD = 0;
            for (int i = 0; i < dist.Length; i++) if (dist[i] > maxD) { maxD = dist[i]; plan.ExitCell = i; }

            // Difficulty.
            plan.HpMul = Difficulty.Health(s);
            plan.DmgMul = Difficulty.Damage(s);
            plan.SpeedMul = Difficulty.Speed(s);
            plan.MaxAlive = Difficulty.MaxAlive(s);
            plan.EliteChance = (float)Math.Min(0.22, Difficulty.Tier(s) * 0.018);
            plan.RewardMul = Difficulty.Reward(s) * gp.RewardMul;
            if (boss)
            {
                plan.BossComplexity = Difficulty.BossComplexity(s);
                plan.BossHpMul = Difficulty.BossHealth(s);
            }
            if ((gp.Mutators & Mutator.Swift) != 0) plan.SpeedMul *= 1.25f;

            // Objective.
            plan.Objective = gp.Objective ?? (boss ? ObjectiveType.Boss : gp.Elite ? ObjectiveType.Elite : PickObjective(r, s));
            if (boss) plan.Objective = ObjectiveType.Boss;

            // Event.
            plan.Event = gp.Event ?? PickEvent(r, s, bd);

            // Spawn cells: far enough from the start that zombies never appear on top of the player.
            var far = new List<int>();
            int minD = Math.Max(2, (int)(maxD * 0.35));
            for (int i = 0; i < dist.Length; i++) if (dist[i] >= minD && !g.Has(i, CellTag.Obstacle)) far.Add(i);
            if (far.Count < 4) for (int i = 0; i < dist.Length; i++) if (dist[i] >= 2 && !far.Contains(i)) far.Add(i);
            r.Shuffle(far);
            for (int i = 0; i < far.Count && plan.SpawnCells.Count < 16; i++) plan.SpawnCells.Add(far[i]);
            if (plan.SpawnCells.Count == 0) plan.SpawnCells.Add(plan.ExitCell);

            Waves(plan, r, gp);
            Objective(plan, r, dist, maxD, gp);
            Loot(plan, r, s);
            Hazards(plan, r, s);
            Texts(plan);
            return plan;
        }

        // ---------------------------------------------------------------- choices

        static ObjectiveType PickObjective(Rng r, long s)
        {
            var opts = new List<ObjectiveType> { ObjectiveType.Eliminate, ObjectiveType.Survive, ObjectiveType.Supplies, ObjectiveType.Extract };
            var w = new List<float> { 3f, 2f, 1.5f, 1.1f };
            if (s >= 4) { opts.Add(ObjectiveType.Defend); w.Add(1.2f); }
            if (s >= 3) { opts.Add(ObjectiveType.Rescue); w.Add(1.5f); }
            if (s >= 6) { opts.Add(ObjectiveType.Nests); w.Add(1.3f); }
            return opts[r.Weighted(w)];
        }

        static StageEvent PickEvent(Rng r, long s, BiomeDef bd)
        {
            if (s < 4 || !r.Chance(0.24)) return StageEvent.None;
            var opts = new List<StageEvent> { StageEvent.HordeRush, StageEvent.ToxicFog };
            if (bd.Indoor) opts.Add(StageEvent.Blackout);
            else
            {
                opts.Add(StageEvent.SupplyDrop);
                opts.Add(StageEvent.BloodMoon);
                opts.Add(StageEvent.Thunderstorm);
            }
            return r.Pick(opts);
        }

        static ZombieType SampleType(Rng r, long s, Mutator m, ZombieType[] forced, ref int mutants)
        {
            if (forced != null && forced.Length > 0) return r.Pick(forced);
            var weights = new float[Bestiary.All.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = Difficulty.Weight(Bestiary.All[i], s);
            if ((m & Mutator.ExplosiveHorde) != 0) weights[(int)ZombieType.Explosive] += 6;
            if ((m & Mutator.RunnerRush) != 0) weights[(int)ZombieType.Runner] += 12;
            if ((m & Mutator.ArmoredHorde) != 0) { weights[(int)ZombieType.Armored] += 6; weights[(int)ZombieType.Military] += 4; }
            if (mutants >= Difficulty.MaxMutants(s)) weights[(int)ZombieType.Mutant] = 0;
            var t = (ZombieType)r.Weighted(weights);
            if (t == ZombieType.Mutant) mutants++;
            return t;
        }

        static void Waves(StagePlan plan, Rng r, GenParams gp)
        {
            long s = plan.Stage;
            int waves = Difficulty.WaveCount(s);
            int size = Difficulty.WaveSize(s);
            float interval = (float)Math.Max(0.35, 1.5 - Difficulty.Tier(s) * 0.04);
            if (plan.IsBoss) { waves = 3; size = Math.Max(5, size / 2); }
            if (plan.Event == StageEvent.HordeRush) { size = (int)(size * 1.4f); interval *= 0.7f; }
            for (int w = 0; w < waves; w++)
            {
                var wave = new Wave { Interval = interval, Delay = w == 0 ? 2.5f : 4f };
                int count = (int)Math.Round(size * (0.85 + 0.15 * w));
                int mutants = 0;
                for (int i = 0; i < count; i++)
                {
                    var t = SampleType(r, s, plan.Mutators, gp.ForcedTypes, ref mutants);
                    bool elite = t != ZombieType.Walker && r.Chance(plan.EliteChance);
                    wave.Spawns.Add(new SpawnEntry { Type = t, Elite = elite });
                }
                plan.Waves.Add(wave);
            }
        }

        static void Objective(StagePlan plan, Rng r, int[] dist, int maxD, GenParams gp)
        {
            var g = plan.Grid;
            long s = plan.Stage;
            var cand = new List<int>();
            for (int i = 0; i < dist.Length; i++) if (dist[i] >= Math.Max(2, maxD / 3) && !g.Has(i, CellTag.Obstacle)) cand.Add(i);
            if (cand.Count == 0) for (int i = 0; i < dist.Length; i++) if (dist[i] > 0) cand.Add(i);
            r.Shuffle(cand);
            Spot At(int cell, int kind = 0) => new Spot(cell, r.Range(0.3f, 0.7f), r.Range(0.3f, 0.7f), kind);

            switch (plan.Objective)
            {
                case ObjectiveType.Eliminate:
                    plan.ObjectiveCount = plan.Waves.Count;
                    break;
                case ObjectiveType.Survive:
                    plan.ObjectiveTime = gp.SurviveTime > 0 ? gp.SurviveTime : (float)Math.Round(75 + Math.Min(105, s * 1.2));
                    break;
                case ObjectiveType.Rescue:
                    plan.ObjectiveCount = (int)Math.Min(4, 2 + s / 20);
                    for (int i = 0; i < plan.ObjectiveCount && i < cand.Count; i++) plan.ObjectiveSpots.Add(At(cand[i]));
                    plan.ObjectiveCount = plan.ObjectiveSpots.Count;
                    break;
                case ObjectiveType.Supplies:
                    plan.ObjectiveCount = (int)Math.Min(6, 4 + s / 25);
                    for (int i = 0; i < plan.ObjectiveCount && i < cand.Count; i++) plan.ObjectiveSpots.Add(At(cand[i]));
                    plan.ObjectiveCount = plan.ObjectiveSpots.Count;
                    break;
                case ObjectiveType.Nests:
                    plan.ObjectiveCount = (int)Math.Min(4, 2 + s / 30);
                    for (int i = 0; i < plan.ObjectiveCount && i < cand.Count; i++) plan.ObjectiveSpots.Add(At(cand[i]));
                    plan.ObjectiveCount = plan.ObjectiveSpots.Count;
                    break;
                case ObjectiveType.Defend:
                {
                    // A cell about halfway through the level, preferring plazas and rooms.
                    int best = cand[0], bestScore = int.MinValue;
                    foreach (int c in cand)
                    {
                        int score = -Math.Abs(dist[c] - maxD / 2) * 4 + g.OpenNeighbours(c) * 3 + (g.Has(c, CellTag.Plaza | CellTag.Room | CellTag.Platform) ? 4 : 0) + r.Range(0, 3);
                        if (score > bestScore) { bestScore = score; best = c; }
                    }
                    plan.ObjectiveSpots.Add(new Spot(best, 0.5f, 0.5f, 0));
                    plan.ObjectiveTime = (float)Math.Round(55 + Math.Min(65, s * 0.8));
                    break;
                }
                case ObjectiveType.Extract:
                    plan.ObjectiveSpots.Add(new Spot(plan.ExitCell, 0.5f, 0.5f, 0));
                    plan.ObjectiveCount = Math.Max(2, plan.Waves.Count - 1);
                    break;
                case ObjectiveType.Elite:
                {
                    var opts = new List<ZombieType> { ZombieType.Mutant };
                    if (s >= Bestiary.Get(ZombieType.Hunter).UnlockStage) opts.Add(ZombieType.Hunter);
                    if (s >= Bestiary.Get(ZombieType.Military).UnlockStage) opts.Add(ZombieType.Military);
                    plan.EliteType = r.Pick(opts);
                    plan.EliteCount = plan.EliteType == ZombieType.Mutant ? (int)Math.Min(3, 1 + s / 150) : plan.EliteType == ZombieType.Hunter ? 3 : 5;
                    plan.ObjectiveCount = plan.EliteCount;
                    break;
                }
                case ObjectiveType.Boss:
                    plan.ObjectiveSpots.Add(new Spot(plan.ExitCell, 0.5f, 0.5f, 0));
                    plan.ObjectiveCount = 1;
                    break;
            }
        }

        static void Loot(StagePlan plan, Rng r, long s)
        {
            var g = plan.Grid;
            var cells = new List<int>();
            for (int i = 0; i < g.Open.Length; i++) if (g.Open[i] && i != plan.StartCell && !g.Has(i, CellTag.Obstacle)) cells.Add(i);
            if (cells.Count == 0) return;
            r.Shuffle(cells);
            int k = 0;
            void Add(LootKind kind, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    int c = cells[k++ % cells.Count];
                    plan.Loot.Add(new Spot(c, r.Range(0.22f, 0.78f), r.Range(0.22f, 0.78f), (int)kind));
                }
            }
            Add(LootKind.Ammo, (int)Math.Min(8, 3 + s / 12));
            Add(LootKind.Health, (int)Math.Min(5, 2 + s / 25));
            Add(LootKind.Armor, (int)Math.Min(3, 1 + s / 30));
            Add(LootKind.Coins, r.Range(1, 4));
            Add(LootKind.Grenades, r.Range(1, 3));
        }

        static void Hazards(StagePlan plan, Rng r, long s)
        {
            var g = plan.Grid;
            var cells = new List<int>();
            for (int i = 0; i < g.Open.Length; i++) if (g.Open[i] && i != plan.StartCell) cells.Add(i);
            if (cells.Count == 0) return;
            r.Shuffle(cells);
            int n = Difficulty.HazardCount(s);
            var b = plan.Biome;
            bool toxic = b == Biome.Lab || b == Biome.Nuclear || b == Biome.Hospital || plan.Event == StageEvent.ToxicFog;
            bool fire = !plan.Indoor || b == Biome.Factory;
            bool sparks = b == Biome.Factory || b == Biome.Nuclear || b == Biome.Lab || b == Biome.Metro;
            for (int i = 0; i < n && i < cells.Count; i++)
            {
                HazardKind kind = HazardKind.ExplosiveBarrel;
                double roll = r.NextDouble();
                if (toxic && roll < 0.3) kind = HazardKind.ToxicPool;
                else if (fire && roll < 0.55 && roll >= 0.3) kind = HazardKind.FireBarrel;
                else if (sparks && roll >= 0.85) kind = HazardKind.Sparks;
                plan.Hazards.Add(new Spot(cells[i], r.Range(0.2f, 0.8f), r.Range(0.2f, 0.8f), (int)kind));
            }
        }

        static void Texts(StagePlan plan)
        {
            var bd = plan.BiomeDef;
            plan.Title = plan.Mode == GameMode.Survival ? "STAGE " + plan.Stage.ToString("#,0") : plan.Title;
            plan.Subtitle = bd.Name;
            switch (plan.Objective)
            {
                case ObjectiveType.Eliminate: plan.ObjectiveText = "Clear " + plan.ObjectiveCount + " waves of infected"; break;
                case ObjectiveType.Survive: plan.ObjectiveText = "Survive for " + Clock(plan.ObjectiveTime); break;
                case ObjectiveType.Rescue: plan.ObjectiveText = "Rescue " + plan.ObjectiveCount + " trapped survivors"; break;
                case ObjectiveType.Supplies: plan.ObjectiveText = "Recover " + plan.ObjectiveCount + " medical supply crates"; break;
                case ObjectiveType.Nests: plan.ObjectiveText = "Destroy " + plan.ObjectiveCount + " infection nests"; break;
                case ObjectiveType.Defend: plan.ObjectiveText = "Defend the radio beacon for " + Clock(plan.ObjectiveTime); break;
                case ObjectiveType.Extract: plan.ObjectiveText = "Survive the horde, then reach the extraction flare"; break;
                case ObjectiveType.Elite: plan.ObjectiveText = "Eliminate " + plan.EliteCount + " elite " + Bestiary.Get(plan.EliteType).Name + (plan.EliteCount > 1 ? "s" : ""); break;
                case ObjectiveType.Boss: plan.ObjectiveText = "Defeat " + Bestiary.Boss(plan.Boss).Name; break;
            }
            string ev = EventName(plan.Event);
            plan.Briefing = plan.ObjectiveText + (ev.Length > 0 ? "\nEvent: " + ev : "");
        }

        public static string EventName(StageEvent e)
        {
            switch (e)
            {
                case StageEvent.Blackout: return "BLACKOUT - power is down";
                case StageEvent.HordeRush: return "HORDE RUSH - bigger, faster waves";
                case StageEvent.ToxicFog: return "TOXIC FOG - poison pools spread";
                case StageEvent.SupplyDrop: return "SUPPLY DROP - extra crates incoming";
                case StageEvent.BloodMoon: return "BLOOD MOON - double coins, more runners";
                case StageEvent.Thunderstorm: return "THUNDERSTORM";
                default: return "";
            }
        }

        public static string Clock(float seconds)
        {
            int t = (int)Math.Ceiling(Math.Max(0, seconds));
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        // ----------------------------------------------------------------- layouts

        public static Grid Layout(Rng r, LayoutKind kind, Biome biome, long s, out int start)
        {
            Grid g;
            int grow = (int)Math.Min(2, s / 40);
            switch (kind)
            {
                case LayoutKind.Streets: g = Streets(r, 11 + grow + r.Range(0, 2), 11 + grow + r.Range(0, 2)); break;
                case LayoutKind.Metro: g = Metro(r); break;
                case LayoutKind.Yard: g = Yard(r, 11 + grow + r.Range(0, 2), 11 + grow + r.Range(0, 2), biome); break;
                case LayoutKind.Arena: g = Arena(r, 11); break;
                default:
                {
                    bool big = biome == Biome.Factory || biome == Biome.Mall || biome == Biome.Parking || biome == Biome.Nuclear;
                    int w = 12 + grow + r.Range(0, 2), h = 12 + grow + r.Range(0, 2);
                    g = Halls(r, w, h, big ? 3 : 2, big ? 5 : 4, big ? 2 : 1, big ? r.Range(5, 8) : r.Range(6, 10), big);
                    break;
                }
            }
            start = PickStart(r, g, kind);
            g.KeepReachable(start);
            return g;
        }

        static int PickStart(Rng r, Grid g, LayoutKind kind)
        {
            // Prefer an open cell on the west edge (lowest x), a little randomised.
            var opts = new List<int>();
            for (int x = 1; x < g.W - 1 && opts.Count == 0; x++)
                for (int y = 1; y < g.H - 1; y++)
                    if (g.IsOpen(x, y) && !g.Has(g.Idx(x, y), CellTag.Obstacle)) opts.Add(g.Idx(x, y));
            if (kind == LayoutKind.Arena)
            {
                opts.Clear();
                int cx = g.W / 2;
                for (int y = 1; y < g.H - 1 && opts.Count == 0; y++)
                    for (int x = cx - 1; x <= cx + 1; x++)
                        if (g.IsOpen(x, y)) opts.Add(g.Idx(x, y));
            }
            if (opts.Count == 0)
            {
                for (int i = 0; i < g.Open.Length; i++) if (g.Open[i]) return i;
                g.Carve(1, 1);
                return g.Idx(1, 1);
            }
            int st = r.Pick(opts);
            g.Tags[st] &= ~CellTag.Obstacle;
            return st;
        }

        static Grid Streets(Rng r, int W, int H)
        {
            var g = new Grid(W, H);
            // Avenues: one or two wide roads each way, plus narrower streets.
            var xs = Lines(r, W, r.Range(2, 4));
            var ys = Lines(r, H, r.Range(2, 4));
            foreach (int x in xs)
            {
                bool wide = r.Chance(0.45) && x + 1 < W - 1;
                for (int y = 1; y < H - 1; y++)
                {
                    g.Carve(x, y, CellTag.Road);
                    if (wide) g.Carve(x + 1, y, CellTag.Road);
                }
            }
            foreach (int y in ys)
            {
                bool wide = r.Chance(0.45) && y + 1 < H - 1;
                for (int x = 1; x < W - 1; x++)
                {
                    g.Carve(x, y, CellTag.Road);
                    if (wide) g.Carve(x, y + 1, CellTag.Road);
                }
            }
            // Plazas and courtyards.
            int plazas = r.Range(1, 3);
            for (int p = 0; p < plazas; p++)
            {
                int px = r.Range(1, W - 4), py = r.Range(1, H - 4);
                for (int x = px; x < px + 3; x++) for (int y = py; y < py + 3; y++) g.Carve(x, y, CellTag.Plaza);
            }
            // Alleys: short random walks off the roads.
            int alleys = r.Range(2, 5);
            for (int a = 0; a < alleys; a++)
            {
                int x = r.Range(1, W - 1), y = r.Range(1, H - 1);
                int len = r.Range(2, 5);
                int dir = r.Range(0, 4);
                for (int i = 0; i < len; i++)
                {
                    g.Carve(x, y);
                    if (r.Chance(0.3)) dir = r.Range(0, 4);
                    x += dir == 0 ? 1 : dir == 1 ? -1 : 0;
                    y += dir == 2 ? 1 : dir == 3 ? -1 : 0;
                    x = Math.Max(1, Math.Min(W - 2, x));
                    y = Math.Max(1, Math.Min(H - 2, y));
                }
            }
            // Wreck clusters on the roads.
            for (int i = 0; i < g.Open.Length; i++)
                if (g.Open[i] && g.Has(i, CellTag.Road) && r.Chance(0.16)) g.Tags[i] |= CellTag.Obstacle;
            return g;
        }

        static List<int> Lines(Rng r, int size, int count)
        {
            var res = new List<int>();
            for (int tries = 0; tries < 40 && res.Count < count; tries++)
            {
                int v = r.Range(2, size - 3);
                bool ok = true;
                foreach (int o in res) if (Math.Abs(o - v) < 3) ok = false;
                if (ok) res.Add(v);
            }
            if (res.Count == 0) res.Add(size / 2);
            return res;
        }

        struct RoomRect
        {
            public int X, Y, W, H;
            public int Cx => X + W / 2;
            public int Cy => Y + H / 2;
        }

        static Grid Halls(Rng r, int W, int H, int minRoom, int maxRoom, int corridor, int targetRooms, bool pillars)
        {
            var g = new Grid(W, H);
            var rooms = new List<RoomRect>();
            for (int tries = 0; tries < 300 && rooms.Count < targetRooms; tries++)
            {
                var rr = new RoomRect { W = r.Range(minRoom, maxRoom + 1), H = r.Range(minRoom, maxRoom + 1) };
                rr.X = r.Range(1, Math.Max(2, W - 1 - rr.W));
                rr.Y = r.Range(1, Math.Max(2, H - 1 - rr.H));
                if (rr.X + rr.W > W - 1 || rr.Y + rr.H > H - 1) continue;
                bool overlap = false;
                foreach (var o in rooms)
                    if (rr.X - 1 < o.X + o.W && rr.X + rr.W + 1 > o.X && rr.Y - 1 < o.Y + o.H && rr.Y + rr.H + 1 > o.Y) { overlap = true; break; }
                if (overlap) continue;
                rooms.Add(rr);
            }
            if (rooms.Count == 0) rooms.Add(new RoomRect { X = 1, Y = 1, W = Math.Min(4, W - 2), H = Math.Min(4, H - 2) });
            for (int k = 0; k < rooms.Count; k++)
            {
                var rr = rooms[k];
                bool big = rr.W >= 3 && rr.H >= 3;
                for (int x = rr.X; x < rr.X + rr.W; x++)
                    for (int y = rr.Y; y < rr.Y + rr.H; y++)
                    {
                        g.Carve(x, y, CellTag.Room | (pillars && big ? CellTag.Pillars : CellTag.None));
                        g.Room[g.Idx(x, y)] = k;
                    }
            }
            g.RoomCount = rooms.Count;
            // Minimum spanning tree over room centres, plus a couple of loops.
            var connected = new List<int> { 0 };
            var rest = new List<int>();
            for (int i = 1; i < rooms.Count; i++) rest.Add(i);
            while (rest.Count > 0)
            {
                int bestA = 0, bestB = rest[0], bestD = int.MaxValue;
                foreach (int a in connected)
                    foreach (int b in rest)
                    {
                        int d = Math.Abs(rooms[a].Cx - rooms[b].Cx) + Math.Abs(rooms[a].Cy - rooms[b].Cy);
                        if (d < bestD) { bestD = d; bestA = a; bestB = b; }
                    }
                Corridor(g, r, rooms[bestA], rooms[bestB], corridor);
                connected.Add(bestB);
                rest.Remove(bestB);
            }
            int loops = rooms.Count > 3 ? r.Range(1, 3) : 0;
            for (int i = 0; i < loops; i++)
            {
                int a = r.Range(0, rooms.Count), b = r.Range(0, rooms.Count);
                if (a != b) Corridor(g, r, rooms[a], rooms[b], corridor);
            }
            // Cover clusters inside rooms.
            for (int i = 0; i < g.Open.Length; i++)
                if (g.Open[i] && g.Has(i, CellTag.Room) && g.OpenNeighbours(i) >= 3 && r.Chance(0.14)) g.Tags[i] |= CellTag.Obstacle;
            return g;
        }

        static void Corridor(Grid g, Rng r, RoomRect a, RoomRect b, int width)
        {
            int x = a.Cx, y = a.Cy;
            bool horizFirst = r.Chance(0.5);
            void Cell(int cx, int cy)
            {
                for (int w = 0; w < width; w++)
                {
                    foreach (var p in new[] { (cx, cy + w), (cx + w, cy) })
                    {
                        if (!g.In(p.Item1, p.Item2)) continue;
                        int i = g.Idx(p.Item1, p.Item2);
                        bool wasOpen = g.Open[i];
                        g.Carve(p.Item1, p.Item2);
                        if (!wasOpen && g.Open[i]) g.Tags[i] |= CellTag.Corridor;
                    }
                }
            }
            if (horizFirst)
            {
                while (x != b.Cx) { Cell(x, y); x += Math.Sign(b.Cx - x); }
                while (y != b.Cy) { Cell(x, y); y += Math.Sign(b.Cy - y); }
            }
            else
            {
                while (y != b.Cy) { Cell(x, y); y += Math.Sign(b.Cy - y); }
                while (x != b.Cx) { Cell(x, y); x += Math.Sign(b.Cx - x); }
            }
            Cell(x, y);
        }

        static Grid Metro(Rng r)
        {
            int W = r.Range(13, 17), H = 9;
            var g = new Grid(W, H);
            // Row 1: concourse; row 2: stair openings; rows 3-5: platform; rows 6-7: tracks (train on row 7).
            int c0 = r.Range(1, 4), c1 = W - 1 - r.Range(1, 4);
            for (int x = c0; x <= c1; x++) g.Carve(x, 1, CellTag.Corridor);
            int stairs = r.Range(2, 4);
            for (int i = 0; i < stairs; i++)
            {
                int sx = c0 + (int)((c1 - c0) * (i + 0.5f) / stairs);
                g.Carve(sx, 2, CellTag.Corridor);
            }
            for (int x = 1; x < W - 1; x++)
                for (int y = 3; y <= 5; y++)
                {
                    g.Carve(x, y, CellTag.Platform | CellTag.Room);
                    g.Room[g.Idx(x, y)] = 0;
                }
            for (int x = 1; x < W - 1; x++)
            {
                g.Carve(x, 6, CellTag.Track);
                g.Carve(x, 7, CellTag.Track);
            }
            int t0 = r.Range(2, 5), tl = r.Range(5, 8);
            for (int x = t0; x < Math.Min(W - 2, t0 + tl); x++) g.Tags[g.Idx(x, 7)] |= CellTag.Obstacle;
            // A few platform props (benches, kiosks).
            for (int x = 2; x < W - 2; x++) if (r.Chance(0.12)) g.Tags[g.Idx(x, 4)] |= CellTag.Obstacle;
            g.RoomCount = 1;
            return g;
        }

        static Grid Yard(Rng r, int W, int H, Biome biome)
        {
            var g = new Grid(W, H);
            for (int x = 1; x < W - 1; x++) for (int y = 1; y < H - 1; y++) g.Carve(x, y);
            // Solid blocks: hangars, barracks, container stacks, rock outcrops.
            int blocks = r.Range(3, 6);
            for (int b = 0; b < blocks; b++)
            {
                int bw = r.Range(1, 3), bh = r.Range(1, 4);
                if (r.Chance(0.5)) { int t = bw; bw = bh; bh = t; }
                int bx = r.Range(3, Math.Max(4, W - 1 - bw)), by = r.Range(1, Math.Max(2, H - 1 - bh));
                for (int x = bx; x < bx + bw && x < W - 1; x++)
                    for (int y = by; y < by + bh && y < H - 1; y++)
                    {
                        g.Open[g.Idx(x, y)] = false;
                        g.Tags[g.Idx(x, y)] = CellTag.None;
                    }
            }
            float density = biome == Biome.Port ? 0.3f : biome == Biome.ForestCamp ? 0.34f : 0.24f;
            for (int i = 0; i < g.Open.Length; i++)
                if (g.Open[i] && g.X(i) > 1 && r.Chance(density)) g.Tags[i] |= CellTag.Obstacle;
            // Paths: a road through the middle for vehicles / taxiway / camp trail.
            int ry = r.Range(2, H - 2);
            for (int x = 1; x < W - 1; x++) g.Carve(x, ry, CellTag.Road);
            for (int x = 1; x < W - 1; x++) g.Tags[g.Idx(x, ry)] &= ~CellTag.Obstacle;
            return g;
        }

        static Grid Arena(Rng r, int n)
        {
            var g = new Grid(n, n);
            float c = (n - 1) / 2f, rad = n / 2f - 1;
            for (int x = 1; x < n - 1; x++)
                for (int y = 1; y < n - 1; y++)
                {
                    float dx = x - c, dy = y - c;
                    if (dx * dx + dy * dy <= rad * rad + 0.6f) g.Carve(x, y, CellTag.Plaza);
                }
            // Cover pieces on a ring.
            int pieces = r.Range(4, 7);
            for (int i = 0; i < pieces; i++)
            {
                double a = i * Math.PI * 2 / pieces + r.Range(-0.3f, 0.3f);
                int x = (int)Math.Round(c + Math.Cos(a) * rad * 0.55), y = (int)Math.Round(c + Math.Sin(a) * rad * 0.55);
                if (g.IsOpen(x, y)) g.Tags[g.Idx(x, y)] |= CellTag.Obstacle;
            }
            return g;
        }
    }
}
