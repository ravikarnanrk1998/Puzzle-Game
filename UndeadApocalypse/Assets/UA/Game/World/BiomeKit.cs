using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using Grid = UA.Core.Grid;

namespace UA.Game
{
    /// <summary>
    /// Biome look: maps every material key used by the level shell and props to a procedural PBR
    /// material appropriate to the biome, mood and weather. Also owns the poster and sign atlases.
    /// </summary>
    public sealed class BiomeKit
    {
        readonly StagePlan _plan;
        readonly Atmosphere _atm;
        readonly int _seed;
        public bool Wet;
        public SurfaceKind FloorSurface = SurfaceKind.Concrete, WallSurface = SurfaceKind.Concrete;
        public int FacadeVariants = 3;
        public string FixtureMat = "emitCool";
        static Texture2D _posters;
        static readonly Dictionary<Biome, Texture2D> SignAtlases = new Dictionary<Biome, Texture2D>();
        static Texture2D _chain;

        public BiomeKit(StagePlan plan, Atmosphere atm)
        {
            _plan = plan;
            _atm = atm;
            _seed = (int)(plan.Seed % 9973);
            var b = plan.Biome;
            Wet = plan.Weather == Weather.Rain || plan.Weather == Weather.Storm || b == Biome.Metro || b == Biome.Parking || b == Biome.Port || b == Biome.Factory;
            if (b == Biome.Frozen || b == Biome.Desert) Wet = false;
            if (b == Biome.Nuclear) FloorSurface = SurfaceKind.Metal;
            if (b == Biome.Desert || b == Biome.ForestCamp || b == Biome.MilitaryBase) FloorSurface = SurfaceKind.Dirt;
            switch (plan.Mood)
            {
                case LightMood.Sodium: FixtureMat = "emitSodium"; break;
                case LightMood.Emergency: FixtureMat = "emitRed"; break;
                case LightMood.Toxic: FixtureMat = "emitToxic"; break;
                default: FixtureMat = "emitCool"; break;
            }
        }

        public void Prepare()
        {
            // Pre-generate the biggest textures up front so the level build does not stall mid-way.
            Resolve("floor");
        }

        public Color Practical(Rng r) => _atm.Practicals[r.Range(0, _atm.Practicals.Length)];

        public string AwningMat(Rng r) => "awning" + r.Range(0, 3);

        // ------------------------------------------------------------------ floors

        public string FloorFor(Grid g, int i)
        {
            switch (_plan.Kind)
            {
                case LayoutKind.Streets: return g.Has(i, CellTag.Road) ? "road" : "plaza";
                case LayoutKind.Metro: return g.Y(i) >= 6 ? "trackbed" : "floor";
                case LayoutKind.Yard: return g.Has(i, CellTag.Road) ? "groundPath" : "ground";
                case LayoutKind.Arena: return _plan.Indoor ? "floor" : (g.Has(i, CellTag.Obstacle) ? "groundPath" : "ground");
                default: return "floor";
            }
        }

        public float FloorTile(string key)
        {
            switch (key)
            {
                case "road": return 5f;
                case "ground": case "groundPath": return 5f;
                case "trackbed": return 3f;
                case "plaza": return 2.4f;
                default:
                    switch (_plan.Biome)
                    {
                        case Biome.Hospital: return 4f;
                        case Biome.Mall: case Biome.Lab: return 3f;
                        case Biome.Nuclear: return 1.5f;
                        case Biome.Factory: case Biome.Parking: return 4f;
                        default: return 2.4f;
                    }
            }
        }

        // ------------------------------------------------------------------ materials

        static Material M(Surface s, Color tint) => Mats.For(s, tint);

        Surface FloorSurf()
        {
            // Indoor arenas (boss rooms, service decks): wet sealed concrete that reflects the fixtures.
            if (_plan.Kind == LayoutKind.Arena && _plan.Indoor) return Tex.Concrete(19, X.Hex(0x64625D), 0.5f);
            switch (_plan.Biome)
            {
                case Biome.Metro: return Tex.FloorTiles(11, X.Hex(0x6C6F73), X.Hex(0x5A5D61), 4, 1f, 0.15f);
                case Biome.Hospital: return Tex.Linoleum(12, X.Hex(0x84968A));
                case Biome.Factory: return Tex.Concrete(13, X.Hex(0x75716A), 0.45f);
                case Biome.Mall: return Tex.Marble(14, X.Hex(0xB8B0A2));
                case Biome.Parking: return Tex.Concrete(15, X.Hex(0x8E8E8A), 0.4f);
                case Biome.Nuclear: return Tex.Grating(16);
                case Biome.Lab: return Tex.FloorTiles(17, X.Hex(0xD9DEE0), X.Hex(0xBFC6C9), 3, 0.2f, 0.3f);
                default: return Tex.Concrete(18, X.Hex(0x8A8780), Wet ? 0.7f : 0.1f);
            }
        }

        Surface WallSurf()
        {
            switch (_plan.Biome)
            {
                case Biome.Metro: return Tex.Wall(0, X.Hex(0x5F7898), X.Hex(0x1E3352), 21, 0.25f, 0.35f);
                case Biome.Hospital: return Tex.Wall(1, X.Hex(0xC9D2CA), X.Hex(0x3E8C86), 22, 1f);
                case Biome.Factory: return Tex.Wall(4, X.Hex(0x7A3C2A), new Color(0, 0, 0, 0), 23, 0.4f);
                case Biome.Mall: return Tex.Wall(1, X.Hex(0xD9CFBD), new Color(0, 0, 0, 0), 24, 0.4f);
                case Biome.Parking: return Tex.Wall(2, X.Hex(0x9A9892), X.Hex(0xC9A12A), 25, 0.5f);
                case Biome.Nuclear: return Tex.Wall(3, X.Hex(0x6D7A80), X.Hex(0xC9A12A), 26, 0.4f);
                case Biome.Lab: return Tex.Wall(3, X.Hex(0xD5DBDD), X.Hex(0x2C9C9A), 27, 0.6f);
                default: return Tex.Wall(2, X.Hex(0x8E8C86), new Color(0, 0, 0, 0), 28, 0.5f);
            }
        }

        Surface GroundSurf(bool path)
        {
            switch (_plan.Biome)
            {
                case Biome.Desert: return path ? Tex.Asphalt(31, 0) : Tex.Ground(1, 31);
                case Biome.ForestCamp: return path ? Tex.Ground(0, 32) : Tex.Ground(3, 32);
                case Biome.MilitaryBase: return path ? Tex.Asphalt(33, Wet ? 0.8f : 0.1f) : Tex.Ground(4, 33);
                case Biome.Airport: return Tex.Asphalt(34, Wet ? 0.8f : 0.1f);
                case Biome.Port: return path ? Tex.Asphalt(35, 0.8f) : Tex.Concrete(35, X.Hex(0x7C7B76), 0.7f);
                case Biome.Frozen: return Tex.Ground(2, 36);
                case Biome.Residential: return path ? Tex.Asphalt(37, Wet ? 0.8f : 0.1f) : Tex.Ground(3, 37);
                case Biome.City: return path ? Tex.Asphalt(38, Wet ? 0.8f : 0.1f) : Tex.FloorTiles(38, X.Hex(0x77746C), X.Hex(0x66635C), 3, Wet ? 0.6f : 0.1f, 0.25f);
                default: return path ? Tex.Asphalt(39, Wet ? 0.8f : 0.1f) : Tex.Ground(0, 39);
            }
        }

        static readonly Color[] FacadeColors =
        {
            X.Hex(0x6E3B2E), X.Hex(0x8A8174), X.Hex(0x5E6266), X.Hex(0x9C8A6E), X.Hex(0x4E5A52), X.Hex(0x7E6A5A),
        };

        static readonly Color[] HouseColors = { X.Hex(0x8FA3AE), X.Hex(0xB5A98F), X.Hex(0xA9B09A), X.Hex(0xB0A6A0), X.Hex(0x9A8670) };

        public (Material, bool) Resolve(string key)
        {
            var b = _plan.Biome;
            int s = _seed;
            switch (key)
            {
                // ---- shell
                case "floor": return (M(FloorSurf(), Color.white), false);
                case "wall": return (M(WallSurf(), Color.white), true);
                case "ceiling":
                    if (b == Biome.Metro || _plan.Kind == LayoutKind.Arena) return (M(Tex.Concrete(42, X.Hex(0x7A7873), 0, false), Color.white), false);
                    return (b == Biome.Hospital || b == Biome.Mall || b == Biome.Lab ? M(Tex.CeilingPanels(41), Color.white) : M(Tex.Concrete(42, X.Hex(0x55534E), 0, false), Color.white), false);
                case "road": return (M(b == Biome.Frozen ? Tex.Ground(2, 43) : Tex.Asphalt(43, Wet ? 0.85f : 0.1f), Color.white), false);
                case "plaza": return (M(b == Biome.Frozen ? Tex.Ground(2, 44) : b == Biome.Residential ? Tex.Ground(3, 44) : Tex.FloorTiles(44, X.Hex(0x7A766E), X.Hex(0x69655E), 3, Wet ? 0.6f : 0.1f, 0.2f), Color.white), false);
                case "sidewalk": return (M(b == Biome.Frozen ? Tex.Ground(2, 45) : Tex.FloorTiles(45, X.Hex(0x8B877E), X.Hex(0x7A766E), 2, Wet ? 0.5f : 0.05f, 0.1f), Color.white), true);
                case "curb": return (M(Tex.Concrete(46, X.Hex(0x9C9890), 0), Color.white), true);
                case "trackbed": return (M(Tex.Ballast(47, Wet ? 0.8f : 0.2f), Color.white), false);
                case "sleeper": return (M(Tex.Concrete(58, X.Hex(0x8A877F), 0.2f), Color.white), true);
                case "railHead": return (Mats.Lit(X.Hex(0xC8CCD0), 0.86f, 1f), false);
                case "trainBlue": return (M(Tex.PaintedMetal(X.Hex(0x1C4C9A), 98, 0.08f), Color.white), true);
                case "trainWindow": return (Mats.Emissive(X.Hex(0xE8DCC4), new Color(1.25f, 1.12f, 0.92f)), false);
                case "headlight": return (Mats.Emissive(X.Hex(0xFFF6E0), new Color(6f, 5.6f, 4.8f)), false);
                case "leaf": return (Mats.Lit(X.Hex(0x2F5E24), 0.38f), true);
                case "leafLight": return (Mats.Lit(X.Hex(0x4C8A30), 0.38f), true);
                case "potDark": return (M(Tex.PaintedMetal(X.Hex(0x2C2C2E), 99, 0.1f), Color.white), true);
                case "tactile": return (Mats.Lit(X.Hex(0xE0B21C), 0.5f), false);
                case "tunnel": return (Mats.Lit(X.Hex(0x030304), 0.1f), false);
                case "ground": return (M(GroundSurf(false), Color.white), false);
                case "groundPath": return (M(GroundSurf(true), Color.white), false);
                case "perimeter": return (M(Tex.Wall(2, X.Hex(0x8A877E), new Color(0, 0, 0, 0), 48, 0.2f), Color.white), true);
                case "corrugated": return (M(Tex.Corrugated(b == Biome.Desert ? X.Hex(0xA08A68) : X.Hex(0x5E6B72), 49), Color.white), true);
                case "tileColumn": return (M(Tex.FloorTiles(50, X.Hex(0xDCD8CC), X.Hex(0xC8C3B6), 8, 0, 0.3f), Color.white), true);
                case "marbleColumn": return (M(Tex.Marble(51, X.Hex(0xE4DCCB)), Color.white), true);
                case "platformEdge": return (Mats.Lit(X.Hex(0x9A968C), 0.3f), false);
                case "lineYellow": return (Mats.Lit(X.Hex(0xC9A21C), 0.45f), false);
                case "lineWhite": return (Mats.Lit(X.Hex(0xB8B8B2), 0.4f), false);
                case "rail": return (Mats.Lit(X.Hex(0x8A8A88), 0.75f, 1f), true);

                // ---- generic materials
                case "trim": return (Mats.Lit(X.Hex(0x16181A), 0.4f), true);
                case "black": return (Mats.Lit(Color.black, 0.2f), false);
                case "concrete": return (M(Tex.Concrete(52, X.Hex(0x8E8B84), 0), Color.white), true);
                case "metal": return (M(Tex.PaintedMetal(X.Hex(0x707478), 53, 0.3f), Color.white), true);
                case "metalDark": return (M(Tex.PaintedMetal(X.Hex(0x24272A), 54, 0.25f), Color.white), true);
                case "rust": return (M(Tex.PaintedMetal(X.Hex(0x6A4A36), 55, 0.95f), Color.white), true);
                case "wood": return (M(Tex.Wood(56, X.Hex(0x8A6A48)), Color.white), true);
                case "woodDark": return (M(Tex.Wood(57, X.Hex(0x4A3828)), Color.white), true);
                case "glass": return (Mats.Lit(X.Hex(0x0A0D10), 0.95f, 0.2f), true);
                case "glassBroken": return (Mats.Lit(X.Hex(0x050505), 0.55f), true);
                case "rubber": return (M(Tex.Rubber, Color.white), true);
                case "chrome": return (Mats.Lit(X.Hex(0x9A9DA0), 0.88f, 1f), true);
                case "car0": return (M(Tex.PaintedMetal(X.Hex(0x5A1414), 60, 0.12f), Color.white), true);
                case "car1": return (M(Tex.PaintedMetal(X.Hex(0x1C2A44), 61, 0.12f), Color.white), true);
                case "car2": return (M(Tex.PaintedMetal(X.Hex(0x8A8D90), 62, 0.12f), Color.white), true);
                case "car3": return (M(Tex.PaintedMetal(X.Hex(0x2A2D2A), 63, 0.12f), Color.white), true);
                case "carYellow": return (M(Tex.PaintedMetal(X.Hex(0xC79A12), 64, 0.15f), Color.white), true);
                case "carWhite": return (M(Tex.PaintedMetal(X.Hex(0xC8CACC), 65, 0.12f), Color.white), true);
                case "burnt": return (M(Tex.PaintedMetal(X.Hex(0x1A1612), 66, 1f), Color.white), true);
                case "trainBody": return (M(Tex.PaintedMetal(X.Hex(0xB8BDC2), 67, 0.06f), Color.white), true);
                case "accent": return (Mats.Lit(X.Hex(0xA82020), 0.5f), true);
                case "white": return (Mats.Lit(X.Hex(0xC8C8C2), 0.4f), true);
                case "whiteMetal": return (M(Tex.PaintedMetal(X.Hex(0xBFC2C0), 68, 0.12f), Color.white), true);
                case "red": return (M(Tex.PaintedMetal(X.Hex(0x9A1A12), 69, 0.3f), Color.white), true);
                case "orange": return (Mats.Lit(X.Hex(0xE0601A), 0.5f), true);
                case "olive": return (M(Tex.PaintedMetal(X.Hex(0x3E4428), 70, 0.3f), Color.white), true);
                case "tan": return (M(Tex.PaintedMetal(X.Hex(0x9A845E), 71, 0.3f), Color.white), true);
                case "hazard": return (M(Tex.PaintedMetal(Color.yellow, 72, 0.25f, 3), Color.white), true);
                case "canvas": return (M(Tex.Fabric(X.Hex(0x48502E), 73), Color.white), true);
                case "canvasTan": return (M(Tex.Fabric(X.Hex(0x9A8660), 74), Color.white), true);
                case "tentBlue": return (M(Tex.Fabric(X.Hex(0x2A4A7A), 75), Color.white), true);
                case "sandbag": return (M(Tex.Fabric(X.Hex(0x8C7A58), 76, 0.6f), Color.white), true);
                case "dirtTop": return (M(Tex.Ground(0, 77), Color.white), false);
                case "dumpGreen": return (M(Tex.PaintedMetal(X.Hex(0x2E4A32), 78, 0.5f), Color.white), true);
                case "dumpBlue": return (M(Tex.PaintedMetal(X.Hex(0x23395A), 79, 0.5f), Color.white), true);
                case "barrelBlue": return (M(Tex.PaintedMetal(X.Hex(0x1E3E7A), 80, 0.45f), Color.white), true);
                case "barrelYellow": return (M(Tex.PaintedMetal(X.Hex(0xB89018), 81, 0.45f), Color.white), true);
                case "trashbag": return (Mats.Lit(X.Hex(0x0B0C0D), 0.78f), true);
                case "cardboard": return (Mats.Lit(X.Hex(0x7A5C3A), 0.15f), true);
                case "mattress": return (M(Tex.Fabric(X.Hex(0x6E8296), 82), Color.white), true);
                case "sheet": return (M(Tex.Fabric(X.Hex(0xC8CBC8), 83, 0.7f), Color.white), true);
                case "machineGreen": return (M(Tex.PaintedMetal(X.Hex(0x3C5A48), 84, 0.4f), Color.white), true);
                case "machineGray": return (M(Tex.PaintedMetal(X.Hex(0x5A6066), 85, 0.35f), Color.white), true);
                case "specimen": return (Mats.Lit(X.Hex(0x1E2A1C), 0.6f), false);
                case "cont0": return (M(Tex.Corrugated(X.Hex(0x8A2A1E), 86), Color.white), true);
                case "cont1": return (M(Tex.Corrugated(X.Hex(0x1E4A7A), 87), Color.white), true);
                case "cont2": return (M(Tex.Corrugated(X.Hex(0x2E5A36), 88), Color.white), true);
                case "cont3": return (M(Tex.Corrugated(X.Hex(0xB8661E), 89), Color.white), true);
                case "cranePaint": return (M(Tex.PaintedMetal(X.Hex(0xC08A1E), 90, 0.5f), Color.white), true);
                case "planeWhite": return (M(Tex.PaintedMetal(X.Hex(0xC9CCCE), 91, 0.25f), Color.white), true);
                case "bark": return (M(Tex.Wood(92, X.Hex(0x3A2C22)), Color.white), true);
                case "pine": return (Mats.Lit(X.Hex(0x15200F), 0.08f), true);
                case "pineSnow": return (Mats.Lit(X.Hex(0x7E8A90), 0.2f), true);
                case "rock": return (M(Tex.Concrete(93, X.Hex(0x5E5A54), 0), Color.white), true);
                case "snow": return (M(Tex.Ground(2, 94), Color.white), true);
                case "bottleGreen": return (Mats.Lit(X.Hex(0x0E2A12), 0.92f), false);
                case "bottleBrown": return (Mats.Lit(X.Hex(0x2A1606), 0.92f), false);
                case "can": return (Mats.Lit(X.Hex(0x8A1C14), 0.8f, 0.8f), false);
                case "awning0": return (M(Tex.Fabric(X.Hex(0x7A1E1E), 95), Color.white), true);
                case "awning1": return (M(Tex.Fabric(X.Hex(0x1E4A3A), 96), Color.white), true);
                case "awning2": return (M(Tex.Fabric(X.Hex(0x2A2E5A), 97), Color.white), true);

                // ---- decals and cutouts
                case "litter": return (Mats.Cutout(new Color(0.85f, 0.85f, 0.85f), Tex.Litter, 0.5f, 0.15f), false);
                case "puddle": return (Mats.Transparent(new Color(0.03f, 0.03f, 0.035f, 0.8f), 0.9f, Tex.Puddle), false);
                case "blood": return (Mats.Transparent(new Color(0.8f, 0.8f, 0.8f, 1f), 0.8f, Tex.BloodAtlas), false);
                case "bloodStain": return (Mats.Transparent(new Color(0.8f, 0.8f, 0.8f, 1f), 0.6f, Tex.BloodAtlas), false);
                case "chainlink": return (Mats.Cutout(X.Hex(0x9A9C9E), ChainLink, 0.4f, 0.6f), true);
                case "posterLit": return (Mats.Emissive(Color.white, new Color(1.3f, 1.3f, 1.3f), Posters, 0.6f), false);
                case "posterDim": return (Mats.Lit(new Color(0.6f, 0.6f, 0.6f), 0.5f, 0, Posters), false);
                case "sign": return (Mats.Emissive(Color.white, new Color(1.1f, 1.1f, 1.1f), SignAtlas(b), 0.5f), false);

                // ---- emissive fixtures
                case "emitWarm": return (Mats.Emissive(X.Hex(0xFFE0B0), new Color(3.5f, 2.6f, 1.6f)), false);
                case "emitWarmDim": return (Mats.Emissive(X.Hex(0x806040), new Color(0.9f, 0.6f, 0.3f)), false);
                case "emitSodium": return (Mats.Emissive(X.Hex(0xFFB060), new Color(5f, 2.6f, 0.9f)), false);
                case "emitCool": return (Mats.Emissive(X.Hex(0xE0F0FF), new Color(3.2f, 3.8f, 4.4f)), false);
                case "emitCoolDim": return (Mats.Emissive(X.Hex(0x9AB0C0), new Color(0.9f, 1.15f, 1.35f)), false);
                case "emitRed": return (Mats.Emissive(X.Hex(0xFF3020), new Color(4f, 0.5f, 0.3f)), false);
                case "emitRedDim": return (Mats.Emissive(X.Hex(0x601010), new Color(0.6f, 0.05f, 0.03f)), false);
                case "emitBlue": return (Mats.Emissive(X.Hex(0x2040FF), new Color(0.4f, 0.8f, 4f)), false);
                case "emitAmber": return (Mats.Emissive(X.Hex(0xFFA020), new Color(5f, 2.6f, 0.4f)), false);
                case "amberDim": return (Mats.Lit(X.Hex(0x5A3A10), 0.7f), false);
                case "emitGreen": return (Mats.Emissive(X.Hex(0x30FF60), new Color(0.6f, 3f, 0.8f)), false);
                case "emitScreen": return (Mats.Emissive(X.Hex(0x103040), new Color(0.3f, 1.0f, 1.2f)), false);
                case "emitToxic": return (Mats.Emissive(X.Hex(0x60FF40), new Color(1.2f, 4.5f, 0.8f)), false);
                case "emitFire": return (Mats.Emissive(X.Hex(0xFF8030), new Color(6f, 2.4f, 0.5f)), false);
        }
            if (key.StartsWith("facadeG") || key.StartsWith("facadeU"))
            {
                int v = key.Length > 7 ? key[7] - '0' : 0;
                bool house = b == Biome.Residential;
                var col = house ? HouseColors[(v + s) % HouseColors.Length] : FacadeColors[(v + s) % FacadeColors.Length];
                if (b == Biome.Frozen) col = Color.Lerp(col, new Color(0.7f, 0.75f, 0.8f), 0.4f);
                var surf = Tex.Facade(key[6] == 'G' ? 0 : 1, col, 100 + v * 7 + (house ? 50 : 0));
                return (Mats.EmissiveMap(surf, Color.white, surf.Emission, new Color(1.6f, 1.6f, 1.6f)), true);
            }
            if (key.StartsWith("beam")) return (Mats.Additive(new Color(1, 1, 1, 0.08f), Tex.Beam), false);
            Debug.LogWarning("[UA] Unknown material key " + key);
            return (Mats.Lit(Color.magenta, 0.3f), true);
        }

        // ------------------------------------------------------------------ atlases

        /// <summary>4 x 2 grid of advertising posters (each cell 256 x 512).</summary>
        public static Texture2D Posters
        {
            get
            {
                if (_posters != null) return _posters;
                int cw = 256, ch = 512;
                var atlas = new Pix(cw * 4, ch * 2);
                for (int i = 0; i < 8; i++)
                {
                    var p = PaintPoster(i * 31 + 7, cw, ch);
                    int ox = (i % 4) * cw, oy = (i / 4) * ch;
                    for (int y = 0; y < ch; y++)
                        for (int x = 0; x < cw; x++)
                            atlas.C[(oy + y) * atlas.W + ox + x] = p.C[y * cw + x];
                }
                _posters = atlas.ToTexture("posters", false, true, TextureWrapMode.Clamp);
                return _posters;
            }
        }

        static Pix PaintPoster(int seed, int w, int h)
        {
            var r = new System.Random(seed);
            var top = Color.HSVToRGB((float)r.NextDouble(), 0.75f, 0.9f);
            var bot = Color.HSVToRGB((float)r.NextDouble(), 0.85f, 0.45f);
            var p = new Pix(w, h).Fill((u, v, x, y) => Color.Lerp(bot, top, v + Noise.Fbm(u, v, 3, 3, seed) * 0.25f));
            int shapes = 2 + r.Next(4);
            for (int i = 0; i < shapes; i++)
            {
                var sc = Color.HSVToRGB((float)r.NextDouble(), 0.55f, 1f);
                p.Blot((float)r.NextDouble() * w, h * (0.3f + (float)r.NextDouble() * 0.45f), 30 + (float)r.NextDouble() * 80, sc.A(0.9f), 0.93f, false);
            }
            // A stylised figure / product silhouette.
            if (r.NextDouble() < 0.6)
            {
                float cx = w * (0.3f + (float)r.NextDouble() * 0.4f);
                p.Blot(cx, h * 0.6f, w * 0.12f, new Color(0.05f, 0.05f, 0.08f, 0.9f), 0.9f, false);
                p.Rect((int)(cx - w * 0.14f), (int)(h * 0.3f), (int)(w * 0.28f), (int)(h * 0.25f), new Color(0.05f, 0.05f, 0.08f, 0.9f));
            }
            string[] words = { "VARDEN", "FRESH", "COLA", "METRO", "SALE", "ROCK", "NOVA", "ZEST", "PULSE", "LIVE", "CITY", "NEW", "RADIO", "FIGHT", "SURF", "GLOW" };
            string a = words[r.Next(words.Length)], b2 = words[r.Next(words.Length)];
            int s1 = Mathf.Clamp(220 / Mathf.Max(1, PixelFont.Width(a, 1)), 3, 8);
            PixelFont.DrawCentered(p, a, w / 2, (int)(h * 0.84f), s1, Color.white);
            PixelFont.DrawCentered(p, b2, w / 2, (int)(h * 0.12f), 4, new Color(1, 1, 0.85f));
            for (int i = 0; i < 8; i++) p.Blot((float)r.NextDouble() * w, (float)r.NextDouble() * h * 0.45f, 3 + (float)r.NextDouble() * 14, new Color(0.3f, 0.02f, 0.02f, 0.85f), 0.6f, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float e = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (e < 6) p.C[y * w + x] = new Color(0.9f, 0.9f, 0.88f);
                }
            return p;
        }

        static string[] SignTexts(Biome b)
        {
            switch (b)
            {
                case Biome.Metro: return new[] { "PLATFORM 4", "EXIT", "CENTRAL", "LINE 2", "NO ENTRY", "MIND THE GAP", "TRAINS", "EVACUATE" };
                case Biome.Hospital: return new[] { "WARD 3", "ICU", "QUARANTINE", "EXIT", "SURGERY", "BIOHAZARD", "STAFF ONLY", "MORGUE" };
                case Biome.Lab: return new[] { "LAB 7", "BIOHAZARD", "LEVEL 4", "EXIT", "CONTAINMENT", "NO ENTRY", "DECON", "SPECIMENS" };
                case Biome.Nuclear: return new[] { "DANGER", "RADIATION", "REACTOR", "EXIT", "CONTROL", "COOLANT", "NO ENTRY", "EVACUATE" };
                case Biome.Mall: return new[] { "FOOD COURT", "EXIT", "LEVEL 1", "CINEMA", "SALE", "PARKING", "TOILETS", "INFO" };
                case Biome.Parking: return new[] { "LEVEL B2", "EXIT", "PAY HERE", "NO PARKING", "STAIRS", "B3", "WAY OUT", "STOP" };
                default: return new[] { "EXIT", "DANGER", "NO ENTRY", "EVACUATE", "STAY OUT", "KEEP CLEAR", "STOP", "QUARANTINE" };
            }
        }

        static Texture2D SignAtlas(Biome b)
        {
            if (SignAtlases.TryGetValue(b, out var t) && t != null) return t;
            var texts = SignTexts(b);
            int w = 512, rh = 64;
            var p = new Pix(w, rh * 8);
            for (int i = 0; i < 8; i++)
            {
                string tx = texts[i];
                bool green = tx == "EXIT" || tx == "WAY OUT" || tx == "STAIRS";
                bool warn = tx.Contains("DANGER") || tx.Contains("BIO") || tx.Contains("RADIATION") || tx.Contains("QUARANTINE") || tx.Contains("NO ") || tx == "STOP" || tx.Contains("EVAC");
                var bg = green ? X.Hex(0x0E7A3A) : warn ? X.Hex(0xA81C14) : X.Hex(0x1A2A44);
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float e = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, rh - 1 - y));
                        var c = e < 4 ? new Color(0.9f, 0.9f, 0.9f) : bg * (0.85f + Noise.Fbm(x / (float)w, y / (float)rh, 6, 3, i) * 0.3f);
                        c.a = 1;
                        p.C[(i * rh + y) * w + x] = c;
                    }
                int scale = Mathf.Clamp(440 / Mathf.Max(1, PixelFont.Width(tx, 1)), 3, 6);
                PixelFont.Draw(p, tx, (w - PixelFont.Width(tx, scale)) / 2, i * rh + (rh - 7 * scale) / 2, scale, Color.white, 0.1f, i);
            }
            t = p.ToTexture("signs" + b, false, true, TextureWrapMode.Clamp);
            SignAtlases[b] = t;
            return t;
        }

        /// <summary>Places a glowing sign plate (row <paramref name="index"/> of the atlas) facing yaw.</summary>
        public void Sign(Sink s, Vector3 pos, float yaw, int index, float width)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var t = Matrix4x4.TRS(pos, rot, Vector3.one);
            float h = width / 8f * 1f;
            s.Part("trim", t, pos).Box(0, new Vector3(0, 0, -0.03f), new Vector3(width + 0.06f, h + 0.06f, 0.05f));
            float v0 = (index % 8) / 8f;
            // The plane faces +Z (its normal is u x v), so a reader in front sees +X on their left: U runs 1 -> 0 along +X
            // or the text reads mirrored.
            s.Part("sign", t, pos).Plane(0, new Vector3(-width / 2, -h / 2, 0), new Vector3(width, 0, 0), new Vector3(0, h, 0), new Vector2(1, v0), new Vector2(0, v0 + 1 / 8f));
        }

        public static Texture2D ChainLink
        {
            get
            {
                if (_chain != null) return _chain;
                var p = new Pix(128, 128).Fill((u, v, x, y) =>
                {
                    float a = Mathf.Repeat((u + v) * 6, 1), b = Mathf.Repeat((u - v) * 6, 1);
                    float wire = Mathf.Max(1 - Noise.Smooth(0.02f, 0.06f, Mathf.Min(a, 1 - a)), 1 - Noise.Smooth(0.02f, 0.06f, Mathf.Min(b, 1 - b)));
                    return new Color(0.7f, 0.72f, 0.74f, wire);
                });
                _chain = p.ToTexture("chainlink");
                return _chain;
            }
        }
    }
}
