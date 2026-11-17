using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>Small shared meshes and materials for dynamic world objects.</summary>
    static class WorldKit
    {
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        public static Mesh Get(string key, System.Action<MeshBuilder> build)
        {
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            build(mb);
            m = mb.ToMesh(key, true);
            Meshes[key] = m;
            return m;
        }

        public static GameObject Obj(string name, Transform parent, Vector3 pos, Mesh mesh, Material[] mats, int layer = Layers.World, bool shadows = true)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Soft additive billboard glow (pickups and objectives readable in the dark).</summary>
        public static GameObject Glow(Transform parent, Vector3 local, float size, Color c)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var mesh = Get("glowquad", mb => mb.Plane(0, new Vector3(-0.5f, -0.5f, 0), Vector3.right, Vector3.up, Vector2.zero, Vector2.one));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Cached("glow" + c, () => Mats.Additive(c, Tex.Soft));
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<Billboard>();
            return go;
        }
    }

    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = PlayerRig.Cam;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    // ===================================================================== hazards

    public static class Hazards
    {
        public static Transform Root;

        public static void Spawn(HazardKind k, Vector3 p, Rng r)
        {
            switch (k)
            {
                case HazardKind.ExplosiveBarrel: ExplosiveBarrel.Create(p, r); break;
                case HazardKind.ToxicPool: SpawnToxicPool(p, -1, 10, r.Range(1.6f, 2.6f)); break;
                case HazardKind.FireBarrel: FireBarrel.Create(p); break;
                case HazardKind.Sparks: SparkBox.Create(p, r); break;
            }
        }

        public static void SpawnToxicPool(Vector3 p, float duration, float dps, float radius = 1.8f)
        {
            var go = new GameObject("ToxicPool");
            if (Root != null) go.transform.SetParent(Root, true);
            go.transform.position = Level.OnNav(p, 2);
            var t = go.AddComponent<ToxicPool>();
            t.Life = duration;
            t.Dps = dps;
            t.Radius = radius;
            Fx.Decal(DecalKind.Acid, go.transform.position + Vector3.up * 0.02f, Vector3.up, radius * 2.2f);
        }
    }

    public sealed class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        float _hp = 50;
        bool _done;
        public bool IsAlive => !_done;

        public static void Create(Vector3 p, Rng r)
        {
            var mesh = WorldKit.Get("xbarrel", mb =>
            {
                mb.Cylinder(0, Vector3.zero, Vector3.up * 0.9f, 0.3f, 0.3f, 14);
                mb.Cylinder(1, Vector3.up * 0.25f, Vector3.up * 0.4f, 0.305f, 0.305f, 14, false, false);
                mb.Cylinder(0, Vector3.up * 0.88f, Vector3.up * 0.92f, 0.29f, 0.29f, 14);
            });
            var red = Mats.Cached("xbarrelRed", () => Mats.For(Tex.PaintedMetal(X.Hex(0x9C1810), 301, 0.35f), Color.white));
            var band = Mats.Cached("xbarrelBand", () => Mats.For(Tex.PaintedMetal(Color.yellow, 302, 0.2f, 3), Color.white));
            var go = WorldKit.Obj("ExplosiveBarrel", Hazards.Root, p, mesh, new[] { red, band });
            go.transform.rotation = Quaternion.Euler(0, r.Range(0, 360f), 0);
            var c = go.AddComponent<CapsuleCollider>();
            c.center = Vector3.up * 0.45f;
            c.radius = 0.32f;
            c.height = 0.9f;
            go.AddComponent<SurfaceTag>().Kind = SurfaceKind.Metal;
            go.AddComponent<ExplosiveBarrel>();
        }

        public void TakeDamage(DamageInfo d)
        {
            if (_done) return;
            _hp -= d.Amount;
            if (_hp > 0) return;
            _done = true;
            var p = transform.position + Vector3.up * 0.5f;
            gameObject.SetActive(false);
            Blast.Explode(p, 5.5f, 260, d.FromPlayer);
            Zombie.Noise(p, 40);
            FireBarrel.Burning(p - Vector3.up * 0.5f, 6f);
        }
    }

    public sealed class ToxicPool : MonoBehaviour
    {
        public float Life = -1, Dps = 10, Radius = 1.8f;
        float _t, _gas;

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (Life > 0 && _t > Life) { Destroy(gameObject); return; }
            _gas -= dt;
            if (_gas <= 0)
            {
                _gas = 0.25f;
                Fx.Gas(transform.position + new Vector3(Random.Range(-Radius, Radius) * 0.6f, 0.2f, Random.Range(-Radius, Radius) * 0.6f), 0.8f, new Color(0.3f, 0.7f, 0.12f, 0.22f));
            }
            if (PlayerRig.Instance != null && (PlayerRig.Pos - transform.position).Flat().sqrMagnitude < Radius * Radius && PlayerRig.Instance.Grounded)
                PlayerRig.Instance.Hurt(Dps * dt, transform.position, HurtKind.Toxic, true);
        }
    }

    public sealed class FireBarrel : MonoBehaviour
    {
        float _t, _life = -1;
        Light _light;

        public static void Create(Vector3 p)
        {
            var mesh = WorldKit.Get("firebarrel", mb =>
            {
                mb.Cylinder(0, Vector3.zero, Vector3.up * 0.85f, 0.3f, 0.3f, 12, true, false);
                mb.Disc(1, Vector3.up * 0.75f, Vector3.up, Vector3.right, 0.28f, 10);
            });
            var rust = Mats.Cached("fbRust", () => Mats.For(Tex.PaintedMetal(X.Hex(0x4A3020), 303, 0.9f), Color.white));
            var ember = Mats.Cached("fbEmber", () => Mats.Emissive(new Color(1f, 0.4f, 0.1f), new Color(5f, 1.8f, 0.3f)));
            var go = WorldKit.Obj("FireBarrel", Hazards.Root, p, mesh, new[] { rust, ember });
            var c = go.AddComponent<CapsuleCollider>();
            c.center = Vector3.up * 0.42f; c.radius = 0.32f; c.height = 0.85f;
            go.AddComponent<SurfaceTag>().Kind = SurfaceKind.Metal;
            go.AddComponent<FireBarrel>();
        }

        /// <summary>A temporary fire on the ground (after explosions).</summary>
        public static void Burning(Vector3 p, float life)
        {
            var go = new GameObject("Fire");
            if (Hazards.Root != null) go.transform.SetParent(Hazards.Root, true);
            go.transform.position = p;
            var f = go.AddComponent<FireBarrel>();
            f._life = life;
        }

        void Start()
        {
            _light = new GameObject("FireLight").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.transform.localPosition = Vector3.up * 1.4f;
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.5f, 0.15f);
            _light.range = 9;
            _light.intensity = 18f;
            _light.shadows = LightShadows.None;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_life > 0 && _t > _life) { Destroy(gameObject); return; }
            var top = transform.position + Vector3.up * (_life > 0 ? 0.1f : 0.85f);
            if (Random.value < 0.6f) Fx.Burn(top, _life > 0 ? 1.4f : 1f);
            if (_light != null) _light.intensity = 15f + Mathf.PerlinNoise(_t * 8, 0) * 9f;
            // Ignite zombies that brush past; singe the player.
            if (Random.value < 0.25f)
            {
                foreach (var z in Zombie.Alive)
                    if ((z.transform.position - transform.position).Flat().sqrMagnitude < 1.1f) z.Ignite(12, 4);
            }
            if (PlayerRig.Instance != null && (PlayerRig.Pos - transform.position).Flat().sqrMagnitude < 0.8f) PlayerRig.Instance.Hurt(12 * dt, transform.position, HurtKind.Fire, true);
        }
    }

    public sealed class SparkBox : MonoBehaviour
    {
        float _t;

        public static void Create(Vector3 p, Rng r)
        {
            var mesh = WorldKit.Get("sparkbox", mb =>
            {
                mb.Box(0, new Vector3(0, 0.7f, 0), new Vector3(0.8f, 1.4f, 0.5f));
                mb.Box(1, new Vector3(0, 0.7f, 0.26f), new Vector3(0.6f, 1.1f, 0.02f));
            });
            var gray = Mats.Cached("sbGray", () => Mats.For(Tex.PaintedMetal(X.Hex(0x5A6066), 304, 0.4f), Color.white));
            var haz = Mats.Cached("sbHaz", () => Mats.For(Tex.PaintedMetal(Color.yellow, 305, 0.2f, 3), Color.white));
            var go = WorldKit.Obj("SparkBox", Hazards.Root, p, mesh, new[] { gray, haz });
            go.transform.rotation = Quaternion.Euler(0, r.Range(0, 360f), 0);
            var c = go.AddComponent<BoxCollider>();
            c.center = Vector3.up * 0.7f; c.size = new Vector3(0.8f, 1.4f, 0.5f);
            go.AddComponent<SurfaceTag>().Kind = SurfaceKind.Metal;
            go.AddComponent<SparkBox>();
        }

        void Update()
        {
            _t -= Time.deltaTime;
            if (_t > 0) return;
            _t = Random.Range(0.6f, 3f);
            var p = transform.position + Vector3.up * Random.Range(0.6f, 1.3f) + transform.forward * 0.3f;
            Fx.Sparks(p, transform.forward + Vector3.up * 0.5f, 18, 5);
            Fx.LightFlash(p, new Color(0.6f, 0.8f, 1f), 4, 6, 0.12f);
            Sfx.Zap(p);
            if (PlayerRig.Instance != null && (PlayerRig.Pos - transform.position).Flat().sqrMagnitude < 1.6f) PlayerRig.Instance.Hurt(10, p, HurtKind.Fire);
            foreach (var z in Zombie.Alive)
                if ((z.transform.position - transform.position).Flat().sqrMagnitude < 2f)
                    z.TakeDamage(new DamageInfo { Amount = 40, Point = p, Dir = transform.forward, Zone = HitZone.Body, Stagger = 1, ArmorPierce = 1 });
        }
    }

    // ===================================================================== pickups

    public sealed class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();
        public LootKind Kind;
        public int Amount;
        public bool Supply;   // mission supply crate
        float _t, _baseY;
        Transform _visual;

        public static Pickup Create(LootKind k, Vector3 p, Transform parent, bool supply = false, int amount = 0)
        {
            Mesh mesh;
            Material[] mats;
            Color glow;
            switch (k)
            {
                case LootKind.Health:
                    // Red medic case with white crosses, a carry handle and a dark seam band.
                    mesh = WorldKit.Get("medkit2", mb =>
                    {
                        mb.RoundBox(0, new Vector3(0, 0.14f, 0), new Vector3(0.44f, 0.28f, 0.17f), 0.035f, 1);
                        foreach (var z in new[] { 0.087f, -0.087f })
                        {
                            mb.Box(1, new Vector3(0, 0.14f, z), new Vector3(0.17f, 0.055f, 0.006f));
                            mb.Box(1, new Vector3(0, 0.14f, z), new Vector3(0.055f, 0.17f, 0.006f));
                        }
                        mb.Box(2, new Vector3(0, 0.14f, 0), new Vector3(0.448f, 0.014f, 0.174f));
                        mb.RoundBox(2, new Vector3(0, 0.305f, 0), new Vector3(0.2f, 0.025f, 0.045f), 0.01f, 1);
                        foreach (var x in new[] { -0.085f, 0.085f }) mb.Box(2, new Vector3(x, 0.29f, 0), new Vector3(0.025f, 0.04f, 0.04f));
                    });
                    mats = new[]
                    {
                        Mats.Cached("pkMedRed", () => Mats.Lit(X.Hex(0xB31717), 0.62f)),
                        Mats.Cached("pkMedCross", () => Mats.Emissive(new Color(0.95f, 0.95f, 0.95f), new Color(1.1f, 1.1f, 1.1f))),
                        Mats.Cached("pkDark", () => Mats.Lit(X.Hex(0x1B1B1D), 0.4f)),
                    };
                    glow = new Color(1f, 0.3f, 0.3f, 0.5f);
                    break;
                case LootKind.Armor:
                    // Plate carrier: body, shoulder straps, magazine pouches and a lit ID panel.
                    mesh = WorldKit.Get("armorvest2", mb =>
                    {
                        mb.RoundBox(0, new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.44f, 0.13f), 0.045f, 1);
                        foreach (var x in new[] { -0.13f, 0.13f }) mb.RoundBox(0, new Vector3(x, 0.57f, -0.005f), new Vector3(0.09f, 0.16f, 0.11f), 0.03f, 1);
                        for (int k = 0; k < 3; k++) mb.RoundBox(1, new Vector3(-0.11f + k * 0.11f, 0.17f, 0.08f), new Vector3(0.09f, 0.13f, 0.05f), 0.012f, 1);
                        mb.Box(2, new Vector3(0, 0.41f, 0.068f), new Vector3(0.22f, 0.08f, 0.006f));
                    });
                    mats = new[]
                    {
                        Mats.Cached("pkVest", () => Mats.For(Tex.Fabric(X.Hex(0x4A4E44), 306, 0.3f), Color.white)),
                        Mats.Cached("pkPouch", () => Mats.Lit(X.Hex(0x34382C), 0.3f)),
                        Mats.Cached("pkBlue", () => Mats.Emissive(new Color(0.2f, 0.4f, 0.9f), new Color(0.3f, 0.7f, 2.2f))),
                    };
                    glow = new Color(0.3f, 0.6f, 1f, 0.5f);
                    break;
                case LootKind.Coins:
                    mesh = WorldKit.Get("coins", mb =>
                    {
                        for (int i = 0; i < 6; i++) mb.Cylinder(0, new Vector3((i % 3 - 1) * 0.07f, i / 3 * 0.025f, (i % 2) * 0.05f), new Vector3((i % 3 - 1) * 0.07f, i / 3 * 0.025f + 0.02f, (i % 2) * 0.05f), 0.05f, 0.05f, 12);
                        mb.Cylinder(0, new Vector3(0, 0.05f, 0), new Vector3(0, 0.07f, 0), 0.05f, 0.05f, 12);
                    });
                    mats = new[] { Mats.Cached("pkGold", () => Mats.Emissive(new Color(1f, 0.75f, 0.2f), new Color(1.2f, 0.8f, 0.15f), null, 0.85f)) };
                    glow = new Color(1f, 0.8f, 0.2f, 0.5f);
                    break;
                case LootKind.Grenades:
                    // Open crate of frag grenades (body, fuse cap, spoon).
                    mesh = WorldKit.Get("grenadebox2", mb =>
                    {
                        mb.Box(0, new Vector3(0, 0.1f, 0), new Vector3(0.42f, 0.2f, 0.27f));
                        for (int i = 0; i < 3; i++)
                        {
                            var c = new Vector3(-0.13f + i * 0.13f, 0.25f, 0);
                            mb.Sphere(1, c, new Vector3(0.05f, 0.065f, 0.05f), 10, 7);
                            mb.Cylinder(2, c + Vector3.up * 0.055f, c + Vector3.up * 0.085f, 0.018f, 0.018f, 8);
                            mb.Box(2, c + new Vector3(0.022f, 0.04f, 0), new Vector3(0.008f, 0.07f, 0.016f));
                        }
                    });
                    mats = new[]
                    {
                        Mats.Cached("pkOliveBox", () => Mats.Lit(X.Hex(0x3E4428), 0.3f)),
                        Mats.Cached("pkGrenade", () => Mats.Lit(X.Hex(0x3A4426), 0.55f, 0.2f)),
                        Mats.Cached("pkFuse", () => Mats.Lit(X.Hex(0x9A9C9E), 0.7f, 1f)),
                    };
                    glow = new Color(1f, 0.6f, 0.2f, 0.5f);
                    break;
                default:
                    mesh = WorldKit.Get("ammobox", mb =>
                    {
                        mb.RoundBox(0, new Vector3(0, 0.13f, 0), new Vector3(0.48f, 0.26f, 0.22f), 0.02f, 1);
                        mb.Box(1, new Vector3(0, 0.27f, 0), new Vector3(0.2f, 0.03f, 0.05f));
                        mb.Box(1, new Vector3(0, 0.15f, 0.112f), new Vector3(0.3f, 0.08f, 0.005f));
                    });
                    mats = new[] { Mats.Cached("pkAmmo", () => Mats.For(Tex.PaintedMetal(X.Hex(0x3E4A2C), 307, 0.3f), Color.white)), Mats.Cached("pkAmmoLabel", () => Mats.Emissive(new Color(0.9f, 0.7f, 0.2f), new Color(1.6f, 1.1f, 0.2f))) };
                    glow = new Color(1f, 0.75f, 0.3f, 0.5f);
                    break;
            }
            if (supply)
            {
                mesh = WorldKit.Get("supply", mb =>
                {
                    mb.RoundBox(0, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.55f), 0.04f, 1);
                    mb.Box(1, new Vector3(0, 0.3f, 0.28f), new Vector3(0.3f, 0.08f, 0.005f));
                    mb.Box(1, new Vector3(0, 0.3f, 0.28f), new Vector3(0.08f, 0.3f, 0.005f));
                    mb.Box(1, new Vector3(0, 0.605f, 0), new Vector3(0.3f, 0.005f, 0.08f));
                    mb.Box(1, new Vector3(0, 0.605f, 0), new Vector3(0.08f, 0.005f, 0.3f));
                });
                mats = new[] { Mats.Cached("pkWhite", () => Mats.Lit(new Color(0.85f, 0.85f, 0.82f), 0.5f)), Mats.Cached("pkRed", () => Mats.Emissive(new Color(0.8f, 0.05f, 0.05f), new Color(1.5f, 0.1f, 0.1f))) };
                glow = new Color(0.3f, 1f, 0.4f, 0.6f);
            }
            var go = new GameObject("Pickup" + k);
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            // Imported model for this pickup when one exists (Resources/UAArt/Items), else the procedural one.
            string artKey = supply ? null : k == LootKind.Health ? "Items/Health" : k == LootKind.Grenades ? "Items/Grenades" : k == LootKind.Ammo ? "Items/Ammo" : null;
            var prefab = artKey != null ? ArtLibrary.Get(artKey) : null;
            GameObject vis;
            if (prefab != null)
            {
                vis = UnityEngine.Object.Instantiate(prefab, go.transform, false);
                vis.name = "Visual";
                foreach (var t in vis.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Pickup;
                foreach (var r in vis.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else vis = WorldKit.Obj("Visual", go.transform, p, mesh, mats, Layers.Pickup, false);
            var pk = go.AddComponent<Pickup>();
            pk.Kind = k;
            pk.Supply = supply;
            pk.Amount = amount;
            pk._visual = vis.transform;
            pk._baseY = 0.15f;
            WorldKit.Glow(go.transform, Vector3.up * 0.35f, supply ? 2.2f : 1.3f, glow);
            All.Add(pk);
            return pk;
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            _t += Time.deltaTime;
            _visual.localPosition = new Vector3(0, _baseY + Mathf.Sin(_t * 2.5f) * 0.05f, 0);
            _visual.localRotation = Quaternion.Euler(0, _t * 60, 0);
            var pl = PlayerRig.Instance;
            if (pl == null || !pl.Alive) return;
            if ((PlayerRig.Pos - transform.position).Flat().sqrMagnitude < 1.6f * 1.6f && Mathf.Abs(PlayerRig.Pos.y - transform.position.y) < 1.5f)
            {
                if (StageRunner.Instance != null && StageRunner.Instance.Collect(this)) Destroy(gameObject);
            }
        }
    }

    // ===================================================================== objectives

    public enum ObjKind { Survivor, Nest, Beacon, Flare }

    /// <summary>Mission objects: survivors to rescue, nests to destroy, the beacon to defend, the extraction flare.</summary>
    public sealed class ObjectiveThing : MonoBehaviour, IDamageable
    {
        public static readonly List<ObjectiveThing> All = new List<ObjectiveThing>();
        public ObjKind Kind;
        public bool Done;
        public float Hp, MaxHp, Hold, HoldNeeded = 6f, Radius = 3f;
        public bool Active = true;
        public bool IsAlive => Kind == ObjKind.Nest && !Done;
        public ZombieRig Survivor;
        float _t, _spawnT;
        Light _light;
        Transform _visual;
        GameObject _glow;

        public static ObjectiveThing Create(ObjKind kind, Vector3 p, Transform parent, StagePlan plan)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            var o = go.AddComponent<ObjectiveThing>();
            o.Kind = kind;
            switch (kind)
            {
                case ObjKind.Survivor:
                {
                    var look = new ZLook { Tone = Random.Range(0, 2) + 4, Decay = 0f, Tatter = 0.2f, ShirtCol = X.Hex(0x3A5A8A), PantsCol = X.Hex(0x2A2A30), ShirtKind = 0, PantsKind = 1, EyeGlow = new Color(0.05f, 0.05f, 0.05f), Seed = Random.Range(0, 99) };
                    var shape = new ZShape { Shirt = 2, Hair = 1 + Random.Range(0, 2) };
                    o.Survivor = ZombieRig.Create(go.transform, shape, look, ZombieRig.ArtVariant("Survivors/Survivor", look.Seed, 3));
                    o.Survivor.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                    o.HoldNeeded = 5f;
                    o.Radius = 3f;
                    o._light = AddLight(go.transform, new Color(0.4f, 0.9f, 0.5f), 4, 1.2f);
                    o._glow = WorldKit.Glow(go.transform, Vector3.up * 2.2f, 0.8f, new Color(0.3f, 1f, 0.4f, 0.8f));
                    var c = go.AddComponent<CapsuleCollider>();
                    c.center = Vector3.up * 0.7f; c.radius = 0.35f; c.height = 1.4f;
                    go.layer = Layers.Pickup;
                    break;
                }
                case ObjKind.Nest:
                {
                    var mesh = WorldKit.Get("nest", mb =>
                    {
                        var r = new System.Random(5);
                        for (int i = 0; i < 9; i++)
                        {
                            var c = new Vector3((float)(r.NextDouble() - 0.5) * 1.6f, (float)r.NextDouble() * 0.6f, (float)(r.NextDouble() - 0.5) * 1.6f);
                            mb.Sphere(0, c, new Vector3(0.5f, 0.4f, 0.5f) * (0.6f + (float)r.NextDouble() * 0.7f), 10, 7);
                        }
                        for (int i = 0; i < 7; i++)
                        {
                            var c = new Vector3((float)(r.NextDouble() - 0.5) * 1.8f, 0.2f + (float)r.NextDouble() * 0.8f, (float)(r.NextDouble() - 0.5) * 1.8f);
                            mb.Sphere(1, c, Vector3.one * (0.1f + (float)r.NextDouble() * 0.12f), 8, 6);
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * 60 * Mathf.Deg2Rad;
                            mb.Cylinder(0, Vector3.up * 0.3f, new Vector3(Mathf.Cos(a) * 2.4f, 0.02f, Mathf.Sin(a) * 2.4f), 0.12f, 0.03f, 6);
                        }
                    });
                    Texture2D fn;
                    var flesh = Mats.Cached("nestFlesh", () => Mats.For(new Surface { Albedo = ZombieTex.Flesh(11, out fn), Normal = fn, Smooth = 0.65f }, new Color(0.8f, 0.85f, 0.7f)));
                    var sacs = Mats.Cached("nestSacs", () => Mats.Emissive(new Color(0.6f, 1f, 0.3f), new Color(1.2f, 3f, 0.6f)));
                    var vis = WorldKit.Obj("Nest", go.transform, p, mesh, new[] { flesh, sacs }, Layers.Default);
                    o._visual = vis.transform;
                    var c = vis.AddComponent<SphereCollider>();
                    c.center = Vector3.up * 0.4f;
                    c.radius = 1f;
                    var hb = vis.AddComponent<Hitbox>();
                    hb.Owner = o;
                    hb.Zone = HitZone.Body;
                    o.MaxHp = o.Hp = 900 * plan.HpMul;
                    o._light = AddLight(go.transform, new Color(0.5f, 1f, 0.3f), 7, 2f);
                    o._spawnT = Random.Range(4f, 8f);
                    break;
                }
                case ObjKind.Beacon:
                {
                    var mesh = WorldKit.Get("beacon", mb =>
                    {
                        mb.Box(0, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 1f, 0.6f));
                        mb.Box(1, new Vector3(0, 0.75f, 0.31f), new Vector3(0.5f, 0.25f, 0.01f));
                        mb.Cylinder(0, new Vector3(0.3f, 1f, 0), new Vector3(0.3f, 3.2f, 0), 0.025f, 0.01f, 6);
                        mb.Sphere(2, new Vector3(0.3f, 3.25f, 0), Vector3.one * 0.07f, 8, 6);
                    });
                    var olive = Mats.Cached("bcOlive", () => Mats.For(Tex.PaintedMetal(X.Hex(0x3A4028), 308, 0.3f), Color.white));
                    var screen = Mats.Cached("bcScreen", () => Mats.Emissive(new Color(0.1f, 0.4f, 0.2f), new Color(0.3f, 1.6f, 0.5f)));
                    var red = Mats.Cached("bcRed", () => Mats.Emissive(Color.red, new Color(5f, 0.4f, 0.2f)));
                    var vis = WorldKit.Obj("Beacon", go.transform, p, mesh, new[] { olive, screen, red });
                    var c = vis.AddComponent<BoxCollider>();
                    c.center = Vector3.up * 0.5f; c.size = new Vector3(0.9f, 1f, 0.6f);
                    o._light = AddLight(go.transform, new Color(1f, 0.2f, 0.1f), 9, 2.5f);
                    o._light.transform.localPosition = Vector3.up * 3.3f;
                    o.Radius = 10f;
                    o._glow = WorldKit.Glow(go.transform, Vector3.up * 3.25f, 1.6f, new Color(1f, 0.25f, 0.15f, 0.8f));
                    break;
                }
                case ObjKind.Flare:
                {
                    o._light = AddLight(go.transform, new Color(1f, 0.15f, 0.1f), 14, 4f);
                    o._light.transform.localPosition = Vector3.up * 0.6f;
                    o._glow = WorldKit.Glow(go.transform, Vector3.up * 0.3f, 2.4f, new Color(1f, 0.2f, 0.12f, 0.9f));
                    o.Radius = 4f;
                    o.HoldNeeded = 6f;
                    o.Active = false;
                    o._light.enabled = false;
                    o._glow.SetActive(false);
                    break;
                }
            }
            All.Add(o);
            return o;
        }

        static Light AddLight(Transform t, Color c, float range, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = Vector3.up * 1.5f;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity * 8f;
            l.shadows = LightShadows.None;
            return l;
        }

        public void Activate()
        {
            Active = true;
            if (_light != null) _light.enabled = true;
            if (_glow != null) _glow.SetActive(true);
        }

        void OnDestroy() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (Done) return;
            switch (Kind)
            {
                case ObjKind.Survivor:
                {
                    // Crouched, waving when the player is near.
                    var rig = Survivor;
                    float near = Mathf.Clamp01(1 - ((PlayerRig.Pos - transform.position).Flat().magnitude - 2) / 10);
                    rig[ZB.Hips].localPosition = new Vector3(0, 0.62f, 0);
                    rig[ZB.ThighL].localRotation = Quaternion.Euler(-80, 0, -10);
                    rig[ZB.ThighR].localRotation = Quaternion.Euler(-70, 0, 10);
                    rig[ZB.ShinL].localRotation = Quaternion.Euler(130, 0, 0);
                    rig[ZB.ShinR].localRotation = Quaternion.Euler(120, 0, 0);
                    rig[ZB.Spine].localRotation = Quaternion.Euler(20, 0, 0);
                    rig[ZB.Head].localRotation = Quaternion.Euler(-10, Mathf.Sin(_t) * 30, 0);
                    rig[ZB.ArmR].localRotation = Quaternion.Euler(-160 * near, 0, 30 * near + Mathf.Sin(_t * 8) * 25 * near);
                    rig[ZB.ArmL].localRotation = Quaternion.Euler(-50, 0, -10);
                    rig[ZB.ForeL].localRotation = Quaternion.Euler(-80, 0, 0);
                    if (_light != null) _light.intensity = 8 + Mathf.Sin(_t * 4) * 3f;
                    break;
                }
                case ObjKind.Nest:
                {
                    float s = 1 + Mathf.Sin(_t * 3) * 0.04f;
                    if (_visual != null) _visual.localScale = new Vector3(s, 1 + Mathf.Sin(_t * 3 + 1) * 0.05f, s);
                    _spawnT -= dt;
                    if (_spawnT <= 0 && Director.Instance != null)
                    {
                        _spawnT = Random.Range(7f, 12f);
                        if (Zombie.Alive.Count < Director.Instance.MaxAlive) Director.Instance.SpawnMinions(Random.value < 0.5f ? ZombieType.Crawler : ZombieType.Walker, 1, transform.position + Random.insideUnitSphere.Flat() * 2.5f);
                        Fx.Gore(transform.position + Vector3.up * 0.6f, Vector3.up, 0.5f);
                    }
                    if (Random.value < 0.08f) Fx.Gas(transform.position + Vector3.up * 0.5f, 1.2f, new Color(0.35f, 0.6f, 0.15f, 0.2f));
                    break;
                }
                case ObjKind.Beacon:
                    if (_light != null) _light.intensity = ((int)(_t * 2) % 2 == 0) ? 24f : 2f;
                    break;
                case ObjKind.Flare:
                    if (Active && Random.value < 0.7f) Fx.Smoke(transform.position + Vector3.up * 0.4f, 0.8f, new Color(0.8f, 0.15f, 0.1f, 0.5f), Vector3.up * 1.8f + Random.insideUnitSphere * 0.3f);
                    if (_light != null && Active) _light.intensity = 28f + Mathf.PerlinNoise(_t * 10, 0) * 16f;
                    break;
            }
        }

        public void TakeDamage(DamageInfo d)
        {
            if (Kind != ObjKind.Nest || Done) return;
            Hp -= d.Amount * (d.Fire ? 2f : d.Explosive ? 1.5f : 1f);
            Fx.Blood(d.Point, d.Dir, 0.6f);
            if (Hp <= 0)
            {
                Done = true;
                Fx.Gore(transform.position + Vector3.up * 0.6f, Vector3.up, 3f);
                Fx.Explosion(transform.position, 3f, true);
                Sfx.Splat(transform.position);
                if (_visual != null) _visual.gameObject.SetActive(false);
                if (_light != null) _light.enabled = false;
                StageRunner.Instance?.ObjectiveProgress(this);
            }
        }

        /// <summary>Rescue / extraction hold; returns true when complete.</summary>
        public bool TickHold(float dt, bool inside)
        {
            if (Done || !Active) return false;
            Hold = inside ? Mathf.Min(HoldNeeded, Hold + dt) : Mathf.Max(0, Hold - dt * 0.5f);
            if (Hold >= HoldNeeded)
            {
                Done = true;
                if (Kind == ObjKind.Survivor)
                {
                    if (Survivor != null) Survivor.gameObject.SetActive(false);
                    if (_light != null) _light.enabled = false;
                    if (_glow != null) _glow.SetActive(false);
                    Fx.Smoke(transform.position + Vector3.up, 1.2f, new Color(0.4f, 1f, 0.5f, 0.4f), Vector3.up);
                }
                return true;
            }
            return false;
        }
    }

    /// <summary>Air-dropped supply crate (Supply Drop event): falls with smoke, then becomes a pickup cluster.</summary>
    public sealed class SupplyDrop : MonoBehaviour
    {
        Vector3 _target;
        float _t;

        public static void Create(Vector3 target, Transform parent)
        {
            var go = new GameObject("SupplyDrop");
            go.transform.SetParent(parent, false);
            go.transform.position = target + Vector3.up * 30;
            var d = go.AddComponent<SupplyDrop>();
            d._target = target;
            var mesh = WorldKit.Get("dropcrate", mb =>
            {
                mb.RoundBox(0, new Vector3(0, 0.5f, 0), new Vector3(1.1f, 1f, 1.1f), 0.05f, 1);
                mb.Lathe(1, new[] { new Vector2(0.01f, 4.5f), new Vector2(1.6f, 3.6f), new Vector2(1.8f, 3.1f) }, 12);
            });
            var olive = Mats.Cached("dropOlive", () => Mats.For(Tex.PaintedMetal(X.Hex(0x3A4028), 309, 0.3f), Color.white));
            var chute = Mats.Cached("dropChute", () => Mats.For(Tex.Fabric(X.Hex(0xC8C0A8), 310), Color.white));
            WorldKit.Obj("Crate", go.transform, go.transform.position, mesh, new[] { olive, chute });
            WorldKit.Glow(go.transform, Vector3.up * 0.5f, 2.5f, new Color(1f, 0.6f, 0.2f, 0.7f));
            Hud.Announce("SUPPLY DROP", "Ammo and medical supplies incoming", new Color(1f, 0.7f, 0.3f));
        }

        void Update()
        {
            _t += Time.deltaTime;
            var p = transform.position;
            if (p.y > _target.y + 0.05f)
            {
                transform.position = Vector3.MoveTowards(p, _target, 3.2f * Time.deltaTime);
                if (Random.value < 0.4f) Fx.Smoke(p + Vector3.up * 0.4f, 0.6f, new Color(1f, 0.5f, 0.2f, 0.4f), Vector3.up);
                return;
            }
            if (enabled)
            {
                enabled = false;
                Fx.Impact(_target, Vector3.up, SurfaceKind.Dirt, 3);
                Sfx.Thud(_target);
                var parent = transform.parent;
                Pickup.Create(LootKind.Ammo, _target + new Vector3(0.9f, 0, 0), parent);
                Pickup.Create(LootKind.Health, _target + new Vector3(-0.9f, 0, 0), parent);
                Pickup.Create(LootKind.Armor, _target + new Vector3(0, 0, 0.9f), parent);
                Pickup.Create(LootKind.Grenades, _target + new Vector3(0, 0, -0.9f), parent);
                Pickup.Create(LootKind.Coins, _target + new Vector3(0.9f, 0, 0.9f), parent, false, 300);
                transform.GetChild(0).localScale = new Vector3(1, 1, 1);
            }
        }
    }
}
