using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    public enum Biome { Forest, Canyon, Autumn, Glacier, Volcano }

    public enum Weather { None, Snow, Leaves, Ash }

    /// <summary>Look of one mountain: ground, foliage, sky, light and surface grip.</summary>
    public sealed class Theme
    {
        public Biome Biome;
        public Color Dirt, DirtDark, Grass, GrassDark, Rock, RockDark, Cap, Trunk, Dust;
        public Color[] Foliage;
        public Color SkyTint, SkyGround, Fog, Sun, AmbSky, AmbEquator, AmbGround;
        public Color MountainNear, MountainFar, MountainCap;
        public float SunIntensity = 1.5f, SunElevation = 42, SunAzimuth = 30, Exposure = 1.25f, Atmosphere = 0.9f;
        public float FogStart = 140, FogEnd = 1300;
        public float Grip = 1;
        public float TreeDensity = 1;
        public Weather Weather;
        public bool Lava;
    }

    public sealed class TrackDef
    {
        public string Id, Name, Tagline;
        public Theme Theme;
        public int Seed;
        public float Length, Grade, Turns, Jumps, Width, Obstacles;
        public int Index;
    }

    public sealed class BikeDef
    {
        public string Id, Name, Kind;
        public int Price;
        public int Speed, Accel, Handling, Strength;
        public Color Frame, Accent;
        public bool Hardtail;

        public float TopSpeed => 15.6f + Speed * 0.62f;
        public float PedalForce => 1.5f + Accel * 0.2f;
        public float Grip => 7.2f + Handling * 0.42f;
        public float SteerRate => 2.8f + Handling * 0.16f;
        public float Power => 0.72f + Strength * 0.055f;
    }

    public sealed class RiderDef
    {
        public string Id, Name, Nick, Bio;
        public int SpeedBonus, AccelBonus, HandlingBonus, StrengthBonus;
        public float TrickBonus = 1;
        public Color Jersey, Pants, Helmet, Accent, Skin;
        public int Number;
        public bool Playable;
    }

    public enum EventKind { Race, Tricks, Takedown, Final }

    public sealed class EventDef
    {
        public int Index;
        public EventKind Kind;
        public TrackDef Track;
        /// <summary>Bronze / silver / gold thresholds: positions for races, points for tricks, knock-downs for takedowns.</summary>
        public int Bronze, Silver, Gold;
        public int Rivals = 5;
        public float Skill;

        public string Title => Kind switch
        {
            EventKind.Race => "DOWNHILL RACE",
            EventKind.Tricks => "TRICK ATTACK",
            EventKind.Takedown => "TAKEDOWN BRAWL",
            _ => "CHAMPIONSHIP FINAL",
        };

        public string Goal => Kind switch
        {
            EventKind.Race => "Finish in the top 3",
            EventKind.Tricks => "Score " + Bronze.ToString("N0") + "+ trick points and finish",
            EventKind.Takedown => "Knock down " + Bronze + " rivals and finish",
            _ => "Win the final",
        };

        public string MedalRule(int medal) => Kind switch
        {
            EventKind.Race or EventKind.Final => Ui.Ord(medal == 3 ? Gold : medal == 2 ? Silver : Bronze) + " place",
            EventKind.Tricks => (medal == 3 ? Gold : medal == 2 ? Silver : Bronze).ToString("N0") + " pts",
            _ => (medal == 3 ? Gold : medal == 2 ? Silver : Bronze) + " takedowns",
        };

        /// <summary>0 = none, 1 bronze, 2 silver, 3 gold.</summary>
        public int Medal(int place, int tricks, int takedowns, bool finished)
        {
            if (!finished) return 0;
            switch (Kind)
            {
                case EventKind.Race:
                case EventKind.Final:
                    return place <= Gold ? 3 : place <= Silver ? 2 : place <= Bronze ? 1 : 0;
                case EventKind.Tricks:
                    return tricks >= Gold ? 3 : tricks >= Silver ? 2 : tricks >= Bronze ? 1 : 0;
                default:
                    return takedowns >= Gold ? 3 : takedowns >= Silver ? 2 : takedowns >= Bronze ? 1 : 0;
            }
        }

        public int MedalPrize(int medal) => medal == 3 ? 1500 + Index * 150 : medal == 2 ? 900 + Index * 90 : medal == 1 ? 500 + Index * 50 : 0;
    }

    /// <summary>All mountains, bikes, riders and the career ladder (original content).</summary>
    public static class Catalog
    {
        public static readonly Theme Forest = new Theme
        {
            Biome = Biome.Forest,
            Dirt = Mats.Hex(0x8A6440), DirtDark = Mats.Hex(0x5C3F26), Grass = Mats.Hex(0x5E9A3A), GrassDark = Mats.Hex(0x34652A),
            Rock = Mats.Hex(0x8E8C86), RockDark = Mats.Hex(0x5D5C59), Cap = Mats.Hex(0xF2F5F8), Trunk = Mats.Hex(0x5A3B24), Dust = Mats.Hex(0xA88760),
            Foliage = new[] { Mats.Hex(0x2F6B34), Mats.Hex(0x3C7D3A), Mats.Hex(0x275A2E), Mats.Hex(0x4C8A3E) },
            SkyTint = new Color(0.36f, 0.55f, 0.95f), SkyGround = new Color(0.45f, 0.48f, 0.5f), Fog = new Color(0.7f, 0.79f, 0.88f),
            Sun = new Color(1f, 0.95f, 0.86f), AmbSky = new Color(0.6f, 0.7f, 0.86f), AmbEquator = new Color(0.55f, 0.6f, 0.55f), AmbGround = new Color(0.3f, 0.34f, 0.24f),
            MountainNear = Mats.Hex(0x4E6E4A), MountainFar = Mats.Hex(0x7F95A8), MountainCap = Mats.Hex(0xF4F7FA),
            SunElevation = 46, SunAzimuth = 35,
        };

        public static readonly Theme Canyon = new Theme
        {
            Biome = Biome.Canyon,
            Dirt = Mats.Hex(0xC27A4A), DirtDark = Mats.Hex(0x8F4E2C), Grass = Mats.Hex(0xC9925A), GrassDark = Mats.Hex(0xA06A3C),
            Rock = Mats.Hex(0xB4583A), RockDark = Mats.Hex(0x7E3A26), Cap = Mats.Hex(0xE3B07A), Trunk = Mats.Hex(0x6B4A2A), Dust = Mats.Hex(0xD79A68),
            Foliage = new[] { Mats.Hex(0x5E7F3A), Mats.Hex(0x6E8A44), Mats.Hex(0x7D8F4C) },
            SkyTint = new Color(0.42f, 0.56f, 0.9f), SkyGround = new Color(0.6f, 0.45f, 0.35f), Fog = new Color(0.9f, 0.78f, 0.66f),
            Sun = new Color(1f, 0.9f, 0.76f), AmbSky = new Color(0.66f, 0.68f, 0.8f), AmbEquator = new Color(0.72f, 0.58f, 0.46f), AmbGround = new Color(0.45f, 0.3f, 0.2f),
            MountainNear = Mats.Hex(0xA9573A), MountainFar = Mats.Hex(0xC79478), MountainCap = Mats.Hex(0xD9A57C),
            SunElevation = 55, SunAzimuth = -20, TreeDensity = 0.35f, Grip = 0.95f, FogStart = 200, FogEnd = 1700,
        };

        public static readonly Theme Autumn = new Theme
        {
            Biome = Biome.Autumn,
            Dirt = Mats.Hex(0x8A6440), DirtDark = Mats.Hex(0x5A3E26), Grass = Mats.Hex(0xB09A46), GrassDark = Mats.Hex(0x7E7A34),
            Rock = Mats.Hex(0x8A8278), RockDark = Mats.Hex(0x5A544E), Cap = Mats.Hex(0xF0F0EE), Trunk = Mats.Hex(0x4A3222), Dust = Mats.Hex(0x9A7A58),
            Foliage = new[] { Mats.Hex(0xD9662A), Mats.Hex(0xE8A13A), Mats.Hex(0xB8402A), Mats.Hex(0xC98A2E), Mats.Hex(0x7A8A34) },
            SkyTint = new Color(0.55f, 0.5f, 0.75f), SkyGround = new Color(0.5f, 0.42f, 0.36f), Fog = new Color(0.95f, 0.75f, 0.58f),
            Sun = new Color(1f, 0.8f, 0.56f), AmbSky = new Color(0.72f, 0.66f, 0.78f), AmbEquator = new Color(0.8f, 0.64f, 0.5f), AmbGround = new Color(0.42f, 0.33f, 0.24f),
            MountainNear = Mats.Hex(0x7A5A3A), MountainFar = Mats.Hex(0x9C8A9A), MountainCap = Mats.Hex(0xF3E6DA),
            SunElevation = 27, SunAzimuth = 55, SunIntensity = 1.6f, Exposure = 1.35f, Atmosphere = 1.4f, FogStart = 130, FogEnd = 1200,
            Weather = Weather.Leaves,
        };

        public static readonly Theme Glacier = new Theme
        {
            Biome = Biome.Glacier,
            Dirt = Mats.Hex(0xD8E2EC), DirtDark = Mats.Hex(0xA9B8C8), Grass = Mats.Hex(0xF1F5FA), GrassDark = Mats.Hex(0xC9D6E4),
            Rock = Mats.Hex(0x6E7682), RockDark = Mats.Hex(0x4A505A), Cap = Mats.Hex(0xFFFFFF), Trunk = Mats.Hex(0x4A3A2E), Dust = Mats.Hex(0xF4F8FF),
            Foliage = new[] { Mats.Hex(0x2E5A44), Mats.Hex(0x3A6A50), Mats.Hex(0x264C3A) },
            SkyTint = new Color(0.3f, 0.5f, 0.95f), SkyGround = new Color(0.7f, 0.75f, 0.8f), Fog = new Color(0.8f, 0.86f, 0.94f),
            Sun = new Color(0.95f, 0.97f, 1f), AmbSky = new Color(0.66f, 0.75f, 0.92f), AmbEquator = new Color(0.7f, 0.74f, 0.82f), AmbGround = new Color(0.5f, 0.54f, 0.6f),
            MountainNear = Mats.Hex(0xC8D4E2), MountainFar = Mats.Hex(0x9AAEC6), MountainCap = Mats.Hex(0xFFFFFF),
            SunElevation = 30, SunAzimuth = -40, SunIntensity = 1.35f, Exposure = 1.15f, Grip = 0.8f, TreeDensity = 0.6f, FogStart = 90, FogEnd = 1000,
            Weather = Weather.Snow,
        };

        public static readonly Theme Volcano = new Theme
        {
            Biome = Biome.Volcano,
            Dirt = Mats.Hex(0x6A584E), DirtDark = Mats.Hex(0x43372F), Grass = Mats.Hex(0x5A4E48), GrassDark = Mats.Hex(0x3C3431),
            Rock = Mats.Hex(0x524844), RockDark = Mats.Hex(0x2E2725), Cap = Mats.Hex(0x7E6A60), Trunk = Mats.Hex(0x2E2522), Dust = Mats.Hex(0x8A7464),
            Foliage = new[] { Mats.Hex(0x3A3028), Mats.Hex(0x4A3A30) },
            // A neutral sky tint: strong red tints turn the procedural sky green; the fog carries the red haze.
            SkyTint = new Color(0.55f, 0.45f, 0.45f), SkyGround = new Color(0.35f, 0.16f, 0.1f), Fog = new Color(0.55f, 0.27f, 0.18f),
            Sun = new Color(1f, 0.72f, 0.52f), AmbSky = new Color(0.9f, 0.62f, 0.54f), AmbEquator = new Color(0.82f, 0.54f, 0.42f), AmbGround = new Color(0.5f, 0.27f, 0.18f),
            MountainNear = Mats.Hex(0x3A302C), MountainFar = Mats.Hex(0x6A4A40), MountainCap = Mats.Hex(0x9A5A3A),
            SunElevation = 24, SunAzimuth = -60, SunIntensity = 1.6f, Exposure = 1.3f, Atmosphere = 1.3f, FogStart = 90, FogEnd = 1000,
            TreeDensity = 0.3f, Weather = Weather.Ash, Lava = true,
        };

        public static readonly TrackDef[] Tracks =
        {
            new TrackDef { Id = "pine", Name = "PINE RIDGE", Tagline = "Alpine forest singletrack", Theme = Forest, Seed = 1107, Length = 1650, Grade = 0.13f, Turns = 0.5f, Jumps = 0.55f, Width = 11, Obstacles = 0.5f },
            new TrackDef { Id = "canyon", Name = "RED ROCK CANYON", Tagline = "Sun-baked desert gullies", Theme = Canyon, Seed = 2311, Length = 1850, Grade = 0.12f, Turns = 0.6f, Jumps = 0.8f, Width = 12, Obstacles = 0.6f },
            new TrackDef { Id = "maple", Name = "MAPLE HOLLOW", Tagline = "Golden-hour autumn woods", Theme = Autumn, Seed = 3719, Length = 1900, Grade = 0.14f, Turns = 0.75f, Jumps = 0.7f, Width = 10, Obstacles = 0.7f },
            new TrackDef { Id = "glacier", Name = "GLACIER PASS", Tagline = "Packed snow, low grip", Theme = Glacier, Seed = 4153, Length = 2050, Grade = 0.15f, Turns = 0.7f, Jumps = 0.85f, Width = 11, Obstacles = 0.7f },
            new TrackDef { Id = "ember", Name = "EMBER PEAK", Tagline = "Ash slopes of a live volcano", Theme = Volcano, Seed = 5903, Length = 2200, Grade = 0.17f, Turns = 0.85f, Jumps = 1f, Width = 10, Obstacles = 0.85f },
        };

        public static readonly BikeDef[] Bikes =
        {
            new BikeDef { Id = "scout", Name = "SCOUT HT", Kind = "Hardtail trail", Price = 0, Speed = 4, Accel = 5, Handling = 6, Strength = 4, Frame = Mats.Hex(0x2F8FD8), Accent = Mats.Hex(0xF2F2F2), Hardtail = true },
            new BikeDef { Id = "ridge", Name = "RIDGE 140", Kind = "All-mountain", Price = 2500, Speed = 5, Accel = 6, Handling = 7, Strength = 5, Frame = Mats.Hex(0x2EBF6A), Accent = Mats.Hex(0x1A1A1A) },
            new BikeDef { Id = "viper", Name = "VIPER DH", Kind = "Downhill race", Price = 6000, Speed = 7, Accel = 5, Handling = 6, Strength = 5, Frame = Mats.Hex(0xE23A3A), Accent = Mats.Hex(0x111111) },
            new BikeDef { Id = "brute", Name = "BRUTE FR", Kind = "Freeride tank", Price = 9000, Speed = 6, Accel = 5, Handling = 5, Strength = 9, Frame = Mats.Hex(0x3A3A3A), Accent = Mats.Hex(0xFFB020) },
            new BikeDef { Id = "comet", Name = "COMET PRO", Kind = "Enduro race", Price = 14000, Speed = 8, Accel = 7, Handling = 8, Strength = 6, Frame = Mats.Hex(0x8A4CF0), Accent = Mats.Hex(0x38E8FF) },
            new BikeDef { Id = "apex", Name = "APEX CARBON", Kind = "World-cup DH", Price = 22000, Speed = 9, Accel = 8, Handling = 9, Strength = 7, Frame = Mats.Hex(0x15171C), Accent = Mats.Hex(0xFF3D9A) },
        };

        static RiderDef R(string id, string name, string nick, string bio, uint jersey, uint pants, uint helmet, uint accent, int skin, int number, bool playable,
            int spd = 0, int acc = 0, int hdl = 0, int str = 0, float trick = 1) => new RiderDef
        {
            Id = id, Name = name, Nick = nick, Bio = bio, Jersey = Mats.Hex(jersey), Pants = Mats.Hex(pants), Helmet = Mats.Hex(helmet), Accent = Mats.Hex(accent),
            Skin = SkinTones[skin], Number = number, Playable = playable, SpeedBonus = spd, AccelBonus = acc, HandlingBonus = hdl, StrengthBonus = str, TrickBonus = trick,
        };

        public static readonly Color[] SkinTones =
        {
            new Color32(0xF1, 0xC8, 0xA4, 255), new Color32(0xD9, 0xA5, 0x7E, 255), new Color32(0xB9, 0x85, 0x63, 255),
            new Color32(0x8A, 0x5A, 0x3B, 255), new Color32(0x5A, 0x3A, 0x26, 255),
        };

        public static readonly RiderDef[] Riders =
        {
            R("kai", "KAI MORENO", "BLAZE", "Pure speed. Brakes are a rumour.", 0xFF5A1F, 0x222222, 0xFFB020, 0xFFFFFF, 2, 7, true, spd: 1),
            R("rhea", "RHEA STORM", "STORM", "Smoothest lines on the mountain.", 0x2A9DF4, 0x1C2A44, 0xF2F5F8, 0x38E8FF, 0, 21, true, hdl: 1),
            R("tank", "TANK OKAFOR", "TANK", "Hits like a landslide.", 0x2E7D32, 0x1A1A1A, 0x3A3A3A, 0xFFD54A, 4, 44, true, str: 2),
            R("jett", "JETT NAKAMURA", "JETT", "Air time is the only time.", 0xE91E63, 0x212121, 0xFFFFFF, 0xFFEB3B, 1, 13, true, trick: 1.25f),
            R("luna", "LUNA VANCE", "LUNA", "Explodes out of every corner.", 0x7E57C2, 0x1B1B2F, 0xB39DDB, 0x80FFEA, 0, 9, true, acc: 1),
            R("diesel", "DIESEL KOWALSKI", "DIESEL", "Old-school brawler, never quits.", 0x455A64, 0x263238, 0xD84315, 0xFFAB40, 1, 66, true, str: 1, spd: 1),
            R("max", "MAX RIVERA", "MAX", "", 0xFFC107, 0x333333, 0x212121, 0xFF5722, 2, 3, false, spd: 1),
            R("zoe", "ZOE LINDQVIST", "ZOE", "", 0x00BCD4, 0x263238, 0xFFFFFF, 0xFF4081, 0, 18, false, hdl: 1),
            R("bruno", "BRUNO SILVA", "BRUNO", "", 0x43A047, 0x1B5E20, 0xFFEB3B, 0xFFFFFF, 3, 31, false, str: 1),
            R("ivy", "IVY CHEN", "IVY", "", 0xF06292, 0x311B92, 0x7C4DFF, 0xFFFFFF, 1, 27, false, trick: 1.2f),
            R("rafa", "RAFA DUARTE", "RAFA", "", 0xD32F2F, 0x212121, 0xFAFAFA, 0x212121, 2, 5, false, acc: 1),
            R("niko", "NIKO PETROV", "NIKO", "", 0x1565C0, 0xECEFF1, 0x0D47A1, 0xFFC107, 0, 77, false, str: 1),
            R("skye", "SKYE MAHONEY", "SKYE", "", 0x26A69A, 0x004D40, 0xE0F2F1, 0xFF7043, 1, 12, false, hdl: 1),
            R("otis", "OTIS GRANT", "OTIS", "", 0x6D4C41, 0x3E2723, 0xFF8F00, 0xFFF3E0, 4, 88, false, spd: 1),
        };

        /// <summary>Selectable frame paints in the garage (index 0 = the bike's factory colour).</summary>
        public static readonly Color[] Paints =
        {
            Color.clear, Mats.Hex(0xE23A3A), Mats.Hex(0xFF8A00), Mats.Hex(0xFFD000), Mats.Hex(0x2EBF6A), Mats.Hex(0x2F8FD8),
            Mats.Hex(0x8A4CF0), Mats.Hex(0xFF3D9A), Mats.Hex(0xF2F2F2), Mats.Hex(0x15171C),
        };

        public static readonly EventDef[] Career = BuildCareer();

        static EventDef[] BuildCareer()
        {
            var list = new List<EventDef>();
            void Add(EventKind k, int track, int bronze, int silver, int gold)
            {
                var e = new EventDef { Index = list.Count, Kind = k, Track = Tracks[track], Bronze = bronze, Silver = silver, Gold = gold };
                e.Skill = Mathf.Clamp01(0.36f + e.Index * 0.04f);
                list.Add(e);
            }
            Add(EventKind.Race, 0, 3, 2, 1);
            Add(EventKind.Tricks, 0, 2500, 4500, 7000);
            Add(EventKind.Race, 1, 3, 2, 1);
            Add(EventKind.Takedown, 0, 2, 3, 5);
            Add(EventKind.Tricks, 1, 3500, 6000, 9000);
            Add(EventKind.Race, 2, 3, 2, 1);
            Add(EventKind.Takedown, 1, 3, 4, 6);
            Add(EventKind.Tricks, 2, 4500, 7500, 11000);
            Add(EventKind.Race, 3, 3, 2, 1);
            Add(EventKind.Takedown, 2, 3, 5, 7);
            Add(EventKind.Tricks, 3, 5500, 9000, 13000);
            Add(EventKind.Race, 4, 3, 2, 1);
            Add(EventKind.Takedown, 3, 4, 5, 7);
            Add(EventKind.Tricks, 4, 6500, 10000, 15000);
            Add(EventKind.Final, 4, 1, 1, 1);
            return list.ToArray();
        }

        static Catalog()
        {
            for (int i = 0; i < Tracks.Length; i++) Tracks[i].Index = i;
        }

        public static TrackDef Track(string id)
        {
            foreach (var t in Tracks)
                if (t.Id == id) return t;
            return Tracks[0];
        }

        public static BikeDef Bike(string id)
        {
            foreach (var b in Bikes)
                if (b.Id == id) return b;
            return Bikes[0];
        }

        public static RiderDef Rider(string id)
        {
            foreach (var r in Riders)
                if (r.Id == id) return r;
            return Riders[0];
        }

        /// <summary>Prize money by finishing place (1-based).</summary>
        public static int PlacePrize(int place) => place switch { 1 => 900, 2 => 650, 3 => 450, 4 => 300, 5 => 200, _ => 120 };
    }
}
