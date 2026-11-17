using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>Marks a collider with the material it represents (for impacts and footsteps).</summary>
    public sealed class SurfaceTag : MonoBehaviour
    {
        public SurfaceKind Kind;
    }

    /// <summary>
    /// Collects all static level geometry into one mesh per (chunk, material), so a whole stage is a
    /// few dozen draw calls. Also creates box colliders, lights and fake volumetric light beams.
    /// </summary>
    public sealed class Sink
    {
        readonly Dictionary<long, MeshBuilder> _builders = new Dictionary<long, MeshBuilder>();
        readonly List<Material> _mats = new List<Material>();
        readonly List<bool> _castShadows = new List<bool>();
        readonly Dictionary<string, int> _matIndex = new Dictionary<string, int>();
        readonly Dictionary<Color, Material> _beamMats = new Dictionary<Color, Material>();
        public readonly Transform Root, ColliderRoot, LightRoot;
        public readonly List<Light> Lights = new List<Light>();
        public readonly float ChunkSize;
        readonly int _chunksX;
        public int LightBudget;
        public int ColliderCount;

        public Sink(Transform root, float levelWidth, float chunkSize = 18f)
        {
            Root = root;
            ChunkSize = chunkSize;
            _chunksX = Mathf.Max(1, Mathf.CeilToInt(levelWidth / chunkSize) + 2);
            ColliderRoot = X.Child(root, "Colliders").transform;
            LightRoot = X.Child(root, "Lights").transform;
            LightBudget = Quality.LightBudget;
        }

        public void Mat(string key, Material m, bool shadows = true)
        {
            if (_matIndex.ContainsKey(key)) return;
            _matIndex[key] = _mats.Count;
            _mats.Add(m);
            _castShadows.Add(shadows);
        }

        public bool HasMat(string key) => _matIndex.ContainsKey(key);

        /// <summary>Builder for a material in the chunk containing <paramref name="at"/>, with M set to <paramref name="m"/>.</summary>
        /// <summary>Creates materials on first use: returns (material, casts shadows).</summary>
        public System.Func<string, (Material, bool)> Resolve;

        public MeshBuilder Part(string mat, Matrix4x4 m, Vector3 at)
        {
            if (!_matIndex.TryGetValue(mat, out int mi))
            {
                if (Resolve != null)
                {
                    var (made, shadows) = Resolve(mat);
                    Mat(mat, made, shadows);
                    mi = _matIndex[mat];
                }
                else
                {
                    Debug.LogWarning("[UA] Missing level material " + mat);
                    mi = 0;
                }
            }
            int cx = Mathf.Clamp(Mathf.FloorToInt(at.x / ChunkSize) + 1, 0, _chunksX - 1);
            int cz = Mathf.Max(0, Mathf.FloorToInt(at.z / ChunkSize) + 1);
            long key = ((long)(cz * _chunksX + cx) << 16) | (long)mi;
            if (!_builders.TryGetValue(key, out var b))
            {
                b = new MeshBuilder();
                _builders[key] = b;
            }
            b.M = m;
            return b;
        }

        public MeshBuilder Part(string mat, Vector3 pos, Quaternion rot, Vector3 scale) => Part(mat, Matrix4x4.TRS(pos, rot, scale), pos);

        public MeshBuilder Part(string mat, Vector3 pos, float yaw = 0) => Part(mat, Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one), pos);

        public BoxCollider Box(Vector3 center, Vector3 size, Quaternion rot, SurfaceKind kind = SurfaceKind.Concrete)
        {
            var go = new GameObject("C");
            go.layer = Layers.World;
            go.transform.SetParent(ColliderRoot, false);
            go.transform.position = center;
            go.transform.rotation = rot;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            if (kind != SurfaceKind.Concrete) go.AddComponent<SurfaceTag>().Kind = kind;
            ColliderCount++;
            return c;
        }

        public BoxCollider Box(Vector3 center, Vector3 size, float yaw = 0, SurfaceKind kind = SurfaceKind.Concrete) => Box(center, size, Quaternion.Euler(0, yaw, 0), kind);

        /// <summary>
        /// URP lights fall off with the inverse square of distance; prop code passes "artist" intensities
        /// (about 1 = a modest practical at 1 m) which are scaled here so fixtures 3-7 m away read properly.
        /// </summary>
        public const float LightScale = 9f;

        /// <summary>Adds a light if the budget allows. Spots point down unless a direction is given.</summary>
        /// <summary>Budget held back for lights placed later (ceiling fixtures spread over the whole level).</summary>
        public int HeldBack;

        public Light Light(Vector3 pos, Color c, float range, float intensity, bool spot = false, float angle = 100, Vector3? dir = null, float flicker = 0, bool beam = false)
        {
            if (Lights.Count >= LightBudget - HeldBack) return null;
            // Smaller light budgets (Low / Medium) place fewer lights, so the ones placed reach further.
            float boost = Quality.Current == Gfx.Low ? 1.35f : Quality.Current == Gfx.Medium ? 1.15f : 1f;
            intensity *= LightScale * boost;
            range *= Mathf.Lerp(1f, 1.2f, (boost - 1f) / 0.35f);
            var go = new GameObject(spot ? "Spot" : "Point");
            go.transform.SetParent(LightRoot, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = spot ? LightType.Spot : LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            if (spot)
            {
                l.spotAngle = angle;
                l.innerSpotAngle = angle * 0.55f;
                go.transform.rotation = Quaternion.LookRotation(dir ?? Vector3.down, Mathf.Abs((dir ?? Vector3.down).y) > 0.9f ? Vector3.forward : Vector3.up);
            }
            if (flicker > 0)
            {
                var f = go.AddComponent<Flicker>();
                f.Amount = flicker;
                f.Base = intensity;
            }
            Lights.Add(l);
            if (beam && Quality.LightShafts) Beam(pos, dir ?? Vector3.down, Mathf.Min(range * 0.75f, 7f), Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * 0.7f, c);
            return l;
        }

        /// <summary>A soft additive cone suggesting light scattering in dusty air.</summary>
        public void Beam(Vector3 apex, Vector3 dir, float length, float spread, Color c)
        {
            if (!Quality.LightShafts) return;
            var key = new Color(Mathf.Round(c.r * 8) / 8, Mathf.Round(c.g * 8) / 8, Mathf.Round(c.b * 8) / 8, 1);
            string matKey = "beam" + key;
            if (!_matIndex.ContainsKey(matKey))
            {
                if (!_beamMats.TryGetValue(key, out var bm))
                {
                    bm = Mats.Additive(new Color(key.r, key.g, key.b, 0.035f), Tex.Beam);
                    _beamMats[key] = bm;
                }
                Mat(matKey, bm, false);
            }
            var rot = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            var b = Part(matKey, Matrix4x4.TRS(apex, rot, Vector3.one), apex);
            int segs = 10;
            float r0 = 0.08f, r1 = Mathf.Max(0.4f, length * spread);
            int start = b.VertexCount;
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Add(radial * r0, -radial, new Vector2(s / (float)segs, 1));
                b.Add(radial * r1 + Vector3.down * length, -radial, new Vector2(s / (float)segs, 0));
            }
            for (int s = 0; s < segs; s++)
            {
                int i0 = start + s * 2;
                // Double-sided so the cone reads from inside and outside.
                b.Quad(0, i0, i0 + 2, i0 + 3, i0 + 1);
                b.Quad(0, i0, i0 + 1, i0 + 3, i0 + 2);
            }
        }

        /// <summary>Creates the renderers. Returns the number of draw-call meshes.</summary>
        public int Build()
        {
            var meshRoot = X.Child(Root, "Geometry").transform;
            int n = 0;
            foreach (var kv in _builders)
            {
                var b = kv.Value;
                if (b.Empty) continue;
                int mi = (int)(kv.Key & 0xFFFF);
                var go = new GameObject("G" + mi);
                go.layer = Layers.World;
                go.transform.SetParent(meshRoot, false);
                var mesh = b.ToMesh("level" + mi, true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _mats[mi];
                r.shadowCastingMode = _castShadows[mi] ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                n++;
            }
            _builders.Clear();
            return n;
        }
    }

    /// <summary>Flickering practical light (dying fluorescent tube, sputtering bulb, emergency strobe).</summary>
    public sealed class Flicker : MonoBehaviour
    {
        public float Amount = 0.3f, Base = 1, Speed = 1;
        public bool Strobe;
        Light _l;
        float _t, _next, _target = 1, _cur = 1;

        void Awake()
        {
            _l = GetComponent<Light>();
            _t = Random.value * 10;
        }

        void Update()
        {
            if (_l == null) return;
            float dt = Time.deltaTime;
            _t += dt * Speed;
            if (Strobe)
            {
                _l.intensity = Base * (Mathf.Sin(_t * 4) > 0.2f ? 1 : 0.05f);
                return;
            }
            _next -= dt;
            if (_next <= 0)
            {
                // Mostly steady, with bursts of rapid flicker.
                bool burst = Random.value < Amount;
                _target = burst ? Random.Range(0.05f, 0.6f) : Random.Range(0.85f, 1.05f);
                _next = burst ? Random.Range(0.03f, 0.09f) : Random.Range(0.2f, 2.2f);
            }
            _cur = Mathf.Lerp(_cur, _target, 1 - Mathf.Exp(-dt * 40));
            _l.intensity = Base * _cur;
        }
    }
}
