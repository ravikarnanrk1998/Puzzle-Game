using System;
using System.Collections.Generic;

namespace UA.Core
{
    public sealed class CampaignMission
    {
        public int Index, Chapter;
        public string Title, Story;
        public Biome Biome;
        public ObjectiveType Objective;
        public BossType Boss;
        public long Difficulty;
        public long Coins, Gems;
        public StageEvent Event;
        public LightMood? Mood;
        public Weather? Weather;
    }

    /// <summary>Story campaign "Operation Last Light": 5 chapters, 30 missions, original story.</summary>
    public static class Campaign
    {
        public static readonly string[] Chapters = { "EVACUATION", "SURVIVORS", "PATIENT ZERO", "BLACKSITE", "LAST LIGHT" };
        public static readonly string[] ChapterIntro =
        {
            "Day 1. The Lazarus strain broke quarantine in Varden City at 03:12. Ranger-6, you are the last operator inside the cordon. Get the evacuation moving.",
            "Day 4. The evacuation failed. Thousands are still hiding in malls, homes and camps. Captain Okafor wants every survivor you can find.",
            "Day 9. The outbreak started at St. Audrey Hospital. Somewhere inside is the patient who carried the strain out of the lab.",
            "Day 15. The trail leads to Halverson Blacksite, a lab that does not exist on any map. Find out what they built.",
            "Day 21. Dr. Brandt has a cure sample. Escort it out of Varden City before command levels the city at dawn.",
        };

        public static readonly CampaignMission[] All = Build();

        static CampaignMission[] Build()
        {
            var l = new List<CampaignMission>();
            void M(int ch, string title, Biome b, ObjectiveType o, string story, BossType boss = BossType.None, StageEvent ev = StageEvent.None, LightMood? mood = null, Weather? weather = null)
            {
                int i = l.Count;
                l.Add(new CampaignMission
                {
                    Index = i, Chapter = ch, Title = title, Biome = b, Objective = o, Story = story, Boss = boss, Event = ev, Mood = mood, Weather = weather,
                    Difficulty = 1 + i * 2 + (boss != BossType.None ? 4 : 0),
                    Coins = 800 + i * 220 + (boss != BossType.None ? 2500 : 0),
                    Gems = boss != BossType.None ? 30 : (i % 3 == 2 ? 10 : 0),
                });
            }
            // Chapter 1 - Evacuation
            M(0, "DAY ZERO", Biome.City, ObjectiveType.Eliminate, "The evacuation convoy is pinned down on Harbor Avenue. Clear the street so the trucks can roll.", mood: LightMood.Night, weather: Weather.Rain);
            M(0, "LAST TRAIN", Biome.Metro, ObjectiveType.Rescue, "Civilians are trapped on Platform 4 at Central Station. The trains are dead. You are their ride out.");
            M(0, "GRIDLOCK", Biome.Residential, ObjectiveType.Supplies, "Abandoned ambulances line Maple Heights. Recover their medical kits before the infected swarm the street.");
            M(0, "BELOW GROUND", Biome.Parking, ObjectiveType.Survive, "Engineers are restoring power to the freight lift. Hold the garage until it comes back online.");
            M(0, "CHECKPOINT ECHO", Biome.MilitaryBase, ObjectiveType.Defend, "The evac helicopters need a beacon. Keep the radio alive until they lock on.", ev: StageEvent.Thunderstorm);
            M(0, "THE BUTCHER", Biome.City, ObjectiveType.Boss, "Something huge is tearing through the convoy. It wears a butcher's apron. Put it down.", BossType.Butcher, mood: LightMood.Night);
            // Chapter 2 - Survivors
            M(1, "CLOSING TIME", Biome.Mall, ObjectiveType.Rescue, "Survivors barricaded themselves in Parkview Mall. Their barricades will not last the night.");
            M(1, "HOUSE TO HOUSE", Biome.Residential, ObjectiveType.Extract, "A family signalled from a rooftop across the district. Clear a path and reach the pickup flare.");
            M(1, "TRIAGE", Biome.Hospital, ObjectiveType.Supplies, "The field clinic is out of antibiotics. St. Audrey still has stock on the third floor.");
            M(1, "PINE RIDGE", Biome.ForestCamp, ObjectiveType.Defend, "The forest camp is the last safe zone. Defend the radio mast while the convoy refuels.", mood: LightMood.Night);
            M(1, "GREYWATER", Biome.Port, ObjectiveType.Eliminate, "A cargo ship can carry four hundred people. Clear the docks so they can board.", weather: Weather.Rain);
            M(1, "BROOD MOTHER", Biome.Hospital, ObjectiveType.Boss, "The maternity ward is crawling. Something is breeding them. Find it and burn it.", BossType.BroodMother);
            // Chapter 3 - Patient Zero
            M(2, "QUARANTINE WARD", Biome.Hospital, ObjectiveType.Nests, "Infection nests have grown through the isolation ward. Destroy them before they spore.", mood: LightMood.Emergency);
            M(2, "CLINICAL TRIALS", Biome.Lab, ObjectiveType.Supplies, "The hospital's research wing kept trial data on Patient Zero. Recover the drives.");
            M(2, "THE FOUNDRY", Biome.Factory, ObjectiveType.Survive, "Patient Zero was moved to a factory infirmary. Hold out while Okafor triangulates the transfer records.");
            M(2, "SPORE BEDS", Biome.Lab, ObjectiveType.Nests, "The strain is being cultivated. Burn the growth beds.", ev: StageEvent.ToxicFog);
            M(2, "REACTOR HALL", Biome.Nuclear, ObjectiveType.Defend, "Brandt is uploading the patient files from the reactor control room. Keep them off him.");
            M(2, "JUGGERNAUT", Biome.Nuclear, ObjectiveType.Boss, "Project Titan's first subject has broken containment. Its armor has weak points. Find them.", BossType.Juggernaut);
            // Chapter 4 - Blacksite
            M(3, "OUTPOST SIERRA", Biome.Desert, ObjectiveType.Eliminate, "The blacksite's supply road runs through a desert checkpoint. Take it.", weather: Weather.Dust);
            M(3, "GROUNDED", Biome.Airport, ObjectiveType.Rescue, "Blacksite staff tried to fly out. Their plane never left the apron. Get them out alive.");
            M(3, "DEEP STORAGE", Biome.Lab, ObjectiveType.Extract, "Brandt's cure research is in deep storage. Grab it and reach the service lift.");
            M(3, "WHITEOUT", Biome.Frozen, ObjectiveType.Survive, "A blizzard has hit the city. Survive until it passes and the drones can fly.");
            M(3, "THE SOURCE", Biome.Nuclear, ObjectiveType.Nests, "The original strain culture is still alive in the reactor core. End it.", mood: LightMood.Toxic);
            M(3, "THE WRAITH", Biome.Frozen, ObjectiveType.Boss, "Something is hunting you through the snow. You only see it when it wants you to.", BossType.Wraith);
            // Chapter 5 - Last Light
            M(4, "CARGO HOLD", Biome.Port, ObjectiveType.Defend, "The cure is loaded on the last ship. Defend the gangway while it is sealed.", ev: StageEvent.Thunderstorm);
            M(4, "RUNWAY", Biome.Airport, ObjectiveType.Supplies, "The escort plane needs fuel cells. They are scattered across the apron.");
            M(4, "TITANS", Biome.City, ObjectiveType.Elite, "Mutants are converging on the convoy route. Take them down one by one.");
            M(4, "DEAD LINE", Biome.Metro, ObjectiveType.Survive, "The convoy goes underground. Hold the station until the last truck is through.", ev: StageEvent.Blackout);
            M(4, "FORT HALVERSON", Biome.MilitaryBase, ObjectiveType.Extract, "The extraction helicopter is waiting at Fort Halverson. Fight your way to it.", mood: LightMood.Dusk);
            M(4, "LAST LIGHT", Biome.City, ObjectiveType.Boss, "The Butcher is back, bigger and angrier. Dawn is coming. Finish this.", BossType.Butcher, mood: LightMood.Dusk);
            return l.ToArray();
        }

        public static StagePlan Plan(int index)
        {
            var m = All[Math.Max(0, Math.Min(All.Length - 1, index))];
            var gp = new GenParams
            {
                Mode = GameMode.Campaign,
                Stage = m.Difficulty,
                Seed = Rng.Hash(777, index, 31337),
                Biome = m.Biome,
                Objective = m.Objective,
                Boss = m.Boss,
                Elite = m.Objective == ObjectiveType.Elite,
                Event = m.Event,
                Mood = m.Mood,
                Weather = m.Weather,
            };
            if (m.Boss != BossType.None && index == All.Length - 1) gp.Stage = 150;
            var p = StageGen.Build(gp);
            if (m.Boss != BossType.None && index == All.Length - 1) p.BossComplexity = 4;
            p.CampaignMission = index;
            p.Title = "MISSION " + (index + 1) + ": " + m.Title;
            p.Subtitle = "CHAPTER " + (m.Chapter + 1) + " - " + Chapters[m.Chapter];
            p.Briefing = m.Story + "\n\nObjective: " + p.ObjectiveText;
            return p;
        }

        public static int Stars(float healthLeft, float accuracy, bool cleared)
        {
            if (!cleared) return 0;
            int s = 1;
            if (healthLeft >= 0.5f) s++;
            if (accuracy >= 0.45f) s++;
            return s;
        }
    }

    /// <summary>Daily challenge: one seeded scenario per UTC day with restrictions and a reward multiplier.</summary>
    public static class DailyChallenge
    {
        public static readonly string[] ClassNames = { "PISTOLS ONLY", "RIFLES ONLY", "SHOTGUNS ONLY", "SNIPERS ONLY", "MACHINE GUNS ONLY", "SPECIAL WEAPONS ONLY" };

        public static StagePlan Plan(long day, long highestStage)
        {
            var r = new Rng(Rng.Hash(day, 4242));
            long s = Math.Max(5, Math.Min(250, (long)(highestStage * 0.75)));
            var mutators = new List<Mutator> { Mutator.HeadshotFocus, Mutator.Swift, Mutator.NoRegen, Mutator.ExplosiveHorde, Mutator.Darkness, Mutator.RunnerRush, Mutator.ArmoredHorde };
            r.Shuffle(mutators);
            var m = mutators[0];
            if (r.Chance(0.4)) m |= mutators[1];
            int cls = r.Chance(0.55) ? r.Range(0, 3) : -1;   // pistols / rifles / shotguns (always owned or cheap)
            var gp = new GenParams
            {
                Mode = GameMode.Daily,
                Stage = s,
                Seed = Rng.Hash(day, 4243),
                Biome = (Biome)r.Range(0, 15),
                Objective = r.Chance(0.6) ? ObjectiveType.Survive : ObjectiveType.Eliminate,
                Mutators = m,
                AllowedClass = cls,
                RewardMul = 2 + r.Range(0, 3) * 0.5f,
                SurviveTime = 120 + r.Range(0, 5) * 30,
            };
            if ((m & Mutator.RunnerRush) != 0) gp.ForcedTypes = new[] { ZombieType.Runner, ZombieType.Runner, ZombieType.Walker, ZombieType.Crawler };
            var p = StageGen.Build(gp);
            p.DailyDay = day;
            p.Title = "DAILY CHALLENGE";
            p.Briefing = p.ObjectiveText + "\n" + Describe(p);
            return p;
        }

        public static string Describe(StagePlan p)
        {
            var parts = new List<string>();
            if (p.AllowedClass >= 0) parts.Add(ClassNames[p.AllowedClass]);
            var m = p.Mutators;
            if ((m & Mutator.HeadshotFocus) != 0) parts.Add("HEADSHOT FOCUS (body shots deal 35%)");
            if ((m & Mutator.Swift) != 0) parts.Add("SWIFT INFECTED (+25% speed)");
            if ((m & Mutator.NoRegen) != 0) parts.Add("NO REGENERATION");
            if ((m & Mutator.ExplosiveHorde) != 0) parts.Add("EXPLOSIVE HORDE");
            if ((m & Mutator.Darkness) != 0) parts.Add("TOTAL DARKNESS");
            if ((m & Mutator.RunnerRush) != 0) parts.Add("RUNNER RUSH");
            if ((m & Mutator.ArmoredHorde) != 0) parts.Add("ARMORED HORDE");
            parts.Add("REWARDS x" + p.RewardMul.ToString("0.#"));
            return string.Join("  |  ", parts);
        }

        public static long CompletionCoins(StagePlan p) => (long)(1500 * p.RewardMul);
        public const long CompletionGems = 15;
    }

    /// <summary>Boss rush: one boss after another, each round a full boss tier harder.</summary>
    public static class BossRush
    {
        public static StagePlan Plan(int round)
        {
            round = Math.Max(1, round);
            long s = 50L * round;
            var boss = (BossType)((round - 1) % 4 + 1);
            var biomes = new[] { Biome.City, Biome.Hospital, Biome.Nuclear, Biome.Frozen, Biome.Port, Biome.MilitaryBase, Biome.Factory, Biome.Lab };
            var gp = new GenParams
            {
                Mode = GameMode.BossRush,
                Stage = s,
                Seed = Rng.Hash(round, 5150),
                Biome = biomes[(round - 1) % biomes.Length],
                Boss = boss,
                Objective = ObjectiveType.Boss,
            };
            var p = StageGen.Build(gp);
            p.Title = "BOSS RUSH - ROUND " + round;
            p.Briefing = "Defeat " + Bestiary.Boss(boss).Name + ". Health carries over between rounds.";
            return p;
        }

        public static long RoundCoins(int round) => 1000 + 750L * round;
        public static long RoundGems(int round) => round % 3 == 0 ? 15 : 0;
    }
}
