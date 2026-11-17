using System;

namespace UA.Core
{
    /// <summary>
    /// Endless difficulty. Every curve is saturating (it approaches a configured ceiling but never
    /// overflows), so stage 1,000,000 is as well defined as stage 10. Not everything grows at the
    /// same rate: health rises fastest, damage and speed slower, and wave size levels off early
    /// while the enemy mix keeps getting tougher every 10 stages.
    /// </summary>
    public static class Difficulty
    {
        // Ceilings (tunable).
        public const double MaxHealthMul = 20, MaxDamageMul = 4.5, MaxSpeedMul = 1.35;

        static double X(long s) => Math.Max(0, s - 1);

        public static float Health(long s) => (float)(1 + Safe.Hill(X(s), MaxHealthMul - 1, 160, 1.15));

        public static float Damage(long s) => (float)(1 + Safe.Hill(X(s), MaxDamageMul - 1, 220, 1.1));

        public static float Speed(long s) => (float)(1 + Safe.Hill(X(s), MaxSpeedMul - 1, 110, 1.2));

        /// <summary>Zombies per wave (before the wave index bonus).</summary>
        public static int WaveSize(long s) => 6 + (int)Math.Round(Safe.Hill(X(s), 22, 60, 1.0));

        public static int WaveCount(long s) => 3 + (int)Math.Min(2, X(s) / 25);

        /// <summary>Enemy-mix tier: rises by one every 10 stages.</summary>
        public static int Tier(long s) => (int)Math.Min(1000, X(s) / 10);

        /// <summary>Suggested simultaneous zombies (the graphics profile caps this further).</summary>
        public static int MaxAlive(long s) => (int)Math.Min(26, 7 + X(s) / 4);

        /// <summary>Reward multiplier: grows forever, logarithmically.</summary>
        public static float Reward(long s) => (float)(1 + 0.9 * Math.Log(1 + X(s) / 6.0));

        public static bool IsBoss(long s) => s > 0 && s % 50 == 0;

        public static bool IsElite(long s) => s > 0 && s % 25 == 0 && !IsBoss(s);

        /// <summary>1 for the first boss (stage 50), 2 for stage 100, ...</summary>
        public static int BossTier(long s) => (int)Math.Min(100000, Math.Max(1, s / 50));

        public static BossType BossFor(long s)
        {
            int idx = (int)((BossTier(s) - 1) % 4);
            return (BossType)(idx + 1);
        }

        /// <summary>Boss health relative to its base: stage health curve plus a per-encounter bump.</summary>
        public static float BossHealth(long s) => Health(s) * (float)(1 + Safe.Hill(BossTier(s) - 1, 1.5, 6, 1));

        /// <summary>How many extra attack patterns a boss uses (0..4).</summary>
        public static int BossComplexity(long s) => (int)Math.Min(4, BossTier(s) - 1);

        public static int HazardCount(long s) => 2 + (int)Math.Min(6, X(s) / 15);

        /// <summary>Spawn weight of a zombie type at a stage (0 when not unlocked yet).</summary>
        public static float Weight(ZombieDef d, long s)
        {
            if (s < d.UnlockStage) return 0;
            double t = Math.Min(Tier(s), 40);
            double growth;
            switch (d.Type)
            {
                case ZombieType.Walker: return (float)(d.Weight / (1 + 0.07 * t));
                case ZombieType.Runner: growth = 0.06; break;
                case ZombieType.Crawler: growth = 0.04; break;
                case ZombieType.Explosive: growth = 0.05; break;
                case ZombieType.Armored: growth = 0.08; break;
                case ZombieType.Toxic: growth = 0.07; break;
                case ZombieType.Military: growth = 0.09; break;
                case ZombieType.Mutant: growth = 0.05; break;
                case ZombieType.Hunter: growth = 0.06; break;
                default: growth = 0.05; break;
            }
            // Newly unlocked types ease in over a few stages.
            double ramp = Math.Min(1, (s - d.UnlockStage + 1) / 4.0);
            return (float)(d.Weight * (1 + growth * t) * ramp);
        }

        /// <summary>Most mutants allowed in one wave.</summary>
        public static int MaxMutants(long s) => 1 + Tier(s) / 10;
    }
}
