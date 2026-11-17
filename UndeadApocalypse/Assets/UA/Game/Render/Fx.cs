using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    public enum SurfaceKind { Concrete, Metal, Wood, Flesh, Dirt, Glass, Water }

    public enum DecalKind { Blood, Hole, Scorch, Acid }

    /// <summary>
    /// All transient effects: particle bursts (sparks, dust, blood, gore, smoke, fire, explosions,
    /// toxic gas, casings), ring-buffer decals, tracers, flash lights and weather.
    /// Each effect type is a single world-space ParticleSystem driven by Emit(), so any number of
    /// impacts cost one draw call per type.
    /// </summary>
    public static class Fx
    {
        static Transform _root;
        static ParticleSystem _sparks, _dust, _blood, _mist, _gore, _smoke, _fireball, _fire, _debris, _shells, _shellsBig, _gas, _embers, _glow, _water, _acid;
        static ParticleSystem _weather, _motes;
        static Weather _weatherKind;
        static Transform _follow;
        static DecalBuffer _bloodDecals, _holes, _scorch, _acidDecals;
        static readonly List<TracerLine> Tracers = new List<TracerLine>();
        static readonly List<FlashLight> Flashes = new List<FlashLight>();
        static ParticleSystem.EmitParams _ep;
        static bool _ready;

        sealed class TracerLine
        {
            public LineRenderer Line;
            public float Life, Max;
        }

        sealed class FlashLight
        {
            public Light Light;
            public float Life, Max, Intensity;
        }

        public static void Init()
        {
            if (_ready) return;
            _ready = true;
            var go = new GameObject("FX");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
            var add = Tex.Soft;
            _sparks = System("Sparks", Mats.Particle(Tex.Spark, true), 400, 0.9f, stretch: 0.06f, gravity: 1.2f, sizeCurve: false, collide: false);
            _dust = System("Dust", Mats.Particle(Tex.Smoke, false), 300, 0, gravity: -0.05f, sizeGrow: 2.2f, fade: true);
            _blood = System("Blood", Mats.Particle(Tex.Soft, false), 500, 0.9f, stretch: 0.03f, gravity: 1.6f, fade: false);
            _mist = System("BloodMist", Mats.Particle(Tex.Smoke, false), 200, 0, gravity: 0.1f, sizeGrow: 2.5f, fade: true);
            _gore = MeshSystem("Gore", GoreMesh(), Mats.Lit(new Color(0.35f, 0.04f, 0.03f), 0.6f), 120, 1.5f);
            _smoke = System("Smoke", Mats.Particle(Tex.Smoke, false), 300, 0, gravity: -0.12f, sizeGrow: 3f, fade: true);
            _fireball = System("Fireball", Mats.Particle(Tex.Smoke, true), 200, 0, gravity: -0.3f, sizeGrow: 1.8f, fade: true);
            _fire = System("Fire", Mats.Particle(Tex.Fire, true), 600, 0, gravity: -0.6f, sizeGrow: 0.6f, fade: true);
            _embers = System("Embers", Mats.Particle(Tex.Soft, true), 300, 0, gravity: -0.15f, fade: true);
            _debris = MeshSystem("Debris", RockMesh(), Mats.Lit(new Color(0.3f, 0.29f, 0.27f), 0.2f), 150, 1f);
            _shells = MeshSystem("Shells", ShellMesh(false), Mats.Lit(new Color(0.85f, 0.62f, 0.25f), 0.8f, 1f), 60, 1f);
            _shellsBig = MeshSystem("ShellsRed", ShellMesh(true), Mats.Lit(new Color(0.6f, 0.06f, 0.05f), 0.5f, 0.2f), 30, 1f);
            _gas = System("Gas", Mats.Particle(Tex.Smoke, false), 300, 0, gravity: -0.02f, sizeGrow: 2f, fade: true);
            _acid = System("Acid", Mats.Particle(Tex.Soft, true), 200, 0.9f, stretch: 0.02f, gravity: 1.2f, fade: true);
            _glow = System("Glow", Mats.Particle(add, true), 100, 0, fade: true);
            _water = System("Splash", Mats.Particle(Tex.Soft, false), 200, 0.9f, stretch: 0.03f, gravity: 1.4f, fade: true);

            _bloodDecals = new DecalBuffer("BloodDecals", Mats.Transparent(new Color(0.75f, 0.75f, 0.75f, 1f), 0.75f, Tex.BloodAtlas), 2, Quality.MaxDecals);
            _holes = new DecalBuffer("Holes", Mats.Transparent(Color.white, 0.2f, Tex.BulletHole), 1, Quality.MaxDecals);
            _scorch = new DecalBuffer("Scorch", Mats.Transparent(Color.white, 0.1f, Tex.Scorch), 1, 24);
            _acidDecals = new DecalBuffer("AcidDecals", Mats.Transparent(new Color(0.4f, 1f, 0.2f, 0.9f), 0.9f, Tex.BloodAtlas), 2, 24);

            for (int i = 0; i < 14; i++)
            {
                var t = new GameObject("Tracer");
                t.transform.SetParent(_root, false);
                var lr = t.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.material = Mats.Additive(new Color(1f, 0.8f, 0.45f, 0.9f), Tex.Streak);
                lr.textureMode = LineTextureMode.Stretch;
                lr.enabled = false;
                Tracers.Add(new TracerLine { Line = lr });
            }
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject("FlashLight");
                l.transform.SetParent(_root, false);
                var light = l.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                Flashes.Add(new FlashLight { Light = light });
            }
        }

        // ------------------------------------------------------------------ builders

        static ParticleSystem System(string name, Material mat, int max, float gravityDefault, float stretch = 0, float gravity = 0, bool sizeCurve = true, float sizeGrow = 1, bool fade = true, bool collide = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(16, (int)(max * Quality.ParticleMul));
            main.startSpeed = 0;
            main.startLifetime = 1;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.enabled = false;
            if (fade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
                col.color = g;
            }
            if (sizeCurve && Mathf.Abs(sizeGrow - 1) > 0.01f)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, sizeGrow));
            }
            var rot = ps.rotationOverLifetime;
            rot.enabled = stretch <= 0;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0;
            if (stretch > 0)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = stretch;
                r.lengthScale = 1.2f;
            }
            ps.Play();
            return ps;
        }

        static ParticleSystem MeshSystem(string name, Mesh mesh, Material mat, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(16, (int)(max * Quality.ParticleMul));
            main.startSpeed = 0;
            main.gravityModifier = gravity;
            main.startRotation3D = true;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.enabled = false;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8, 8);
            rot.y = new ParticleSystem.MinMaxCurve(-8, 8);
            rot.z = new ParticleSystem.MinMaxCurve(-8, 8);
            var col = ps.collision;
            col.enabled = Quality.Current != Gfx.Low;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.collidesWith = Layers.WorldMask;
            col.quality = ParticleSystemCollisionQuality.Low;
            col.bounce = 0.35f;
            col.dampen = 0.4f;
            col.lifetimeLoss = 0;
            col.maxCollisionShapes = 64;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.alignment = ParticleSystemRenderSpace.World;
            ps.Play();
            return ps;
        }

        static Mesh RockMesh()
        {
            var mb = new MeshBuilder();
            mb.Sphere(0, Vector3.zero, new Vector3(0.5f, 0.35f, 0.42f), 5, 3);
            return mb.ToMesh("rock", false);
        }

        static Mesh GoreMesh()
        {
            var mb = new MeshBuilder();
            mb.Sphere(0, Vector3.zero, new Vector3(0.5f, 0.3f, 0.4f), 5, 3);
            mb.Sphere(0, new Vector3(0.25f, 0.1f, 0), new Vector3(0.28f, 0.22f, 0.25f), 4, 3);
            return mb.ToMesh("gore", false);
        }

        static Mesh ShellMesh(bool shotgun)
        {
            var mb = new MeshBuilder();
            if (shotgun)
            {
                mb.Cylinder(0, new Vector3(0, -0.5f, 0), new Vector3(0, 0.5f, 0), 0.22f, 0.22f, 8);
            }
            else mb.Cylinder(0, new Vector3(0, -0.5f, 0), new Vector3(0, 0.5f, 0), 0.18f, 0.16f, 8);
            return mb.ToMesh("shell", false);
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color col)
        {
            if (ps == null) return;
            _ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = col,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false,
            };
            ps.Emit(_ep, 1);
        }

        static void EmitMesh(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color col)
        {
            if (ps == null) return;
            _ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = col,
                rotation3D = new Vector3(Random.Range(0, 360f), Random.Range(0, 360f), Random.Range(0, 360f)),
            };
            ps.Emit(_ep, 1);
        }

        static int N(int n) => Mathf.Max(1, Mathf.RoundToInt(n * Quality.ParticleMul));

        // ------------------------------------------------------------------ effects

        public static void Impact(Vector3 p, Vector3 n, SurfaceKind k, float strength = 1)
        {
            if (!_ready) return;
            switch (k)
            {
                case SurfaceKind.Metal:
                    for (int i = 0; i < N(10); i++) Emit(_sparks, p, (n + Random.insideUnitSphere * 0.8f).normalized * Random.Range(3f, 9f), Random.Range(0.02f, 0.05f), Random.Range(0.15f, 0.4f), Color.white);
                    Emit(_glow, p + n * 0.05f, Vector3.zero, 0.35f, 0.06f, new Color(1f, 0.7f, 0.35f, 1));
                    _holes.Add(p, n, 0.07f, 0, Color.white);
                    break;
                case SurfaceKind.Flesh:
                    Blood(p, n, 0.6f * strength);
                    break;
                case SurfaceKind.Water:
                    for (int i = 0; i < N(10); i++) Emit(_water, p, (Vector3.up * 2 + Random.insideUnitSphere).normalized * Random.Range(1.5f, 4f), Random.Range(0.03f, 0.07f), Random.Range(0.3f, 0.6f), new Color(0.6f, 0.65f, 0.7f, 0.6f));
                    break;
                case SurfaceKind.Glass:
                    for (int i = 0; i < N(14); i++) Emit(_sparks, p, (n + Random.insideUnitSphere).normalized * Random.Range(1f, 4f), Random.Range(0.02f, 0.04f), Random.Range(0.3f, 0.7f), new Color(0.7f, 0.9f, 1f, 0.8f));
                    break;
                default:
                {
                    var dustCol = k == SurfaceKind.Dirt ? new Color(0.35f, 0.28f, 0.2f, 0.5f) : k == SurfaceKind.Wood ? new Color(0.4f, 0.3f, 0.2f, 0.5f) : new Color(0.5f, 0.48f, 0.45f, 0.45f);
                    for (int i = 0; i < N(3); i++) Emit(_dust, p + n * 0.05f, (n + Random.insideUnitSphere * 0.5f) * Random.Range(0.3f, 1.2f), Random.Range(0.15f, 0.35f) * strength, Random.Range(0.5f, 1.1f), dustCol);
                    for (int i = 0; i < N(4); i++) EmitMesh(_debris, p + n * 0.03f, (n + Random.insideUnitSphere * 0.7f).normalized * Random.Range(2f, 5f), Random.Range(0.015f, 0.04f), Random.Range(0.8f, 1.6f), Color.white);
                    if (k == SurfaceKind.Concrete) for (int i = 0; i < N(3); i++) Emit(_sparks, p, (n + Random.insideUnitSphere).normalized * Random.Range(2f, 6f), 0.025f, 0.12f, Color.white);
                    _holes.Add(p, n, 0.08f, 0, Color.white);
                    break;
                }
            }
        }

        public static void Blood(Vector3 p, Vector3 dir, float amount)
        {
            if (!_ready) return;
            int n = N((int)(8 + amount * 14));
            for (int i = 0; i < n; i++)
            {
                var v = (dir * 0.7f + Random.insideUnitSphere).normalized * Random.Range(1.5f, 5f) * (0.6f + amount * 0.5f);
                Emit(_blood, p, v, Random.Range(0.03f, 0.08f), Random.Range(0.4f, 0.8f), new Color(0.32f, 0.01f, 0.01f, 1f));
            }
            Emit(_mist, p, dir * 0.6f, 0.35f + amount * 0.3f, 0.5f, new Color(0.4f, 0.02f, 0.02f, 0.55f));
            // Splat on the nearest surface behind / below.
            if (Physics.Raycast(p, (dir + Vector3.down * 0.6f).normalized, out var hit, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                _bloodDecals.Add(hit.point, hit.normal, Random.Range(0.3f, 0.6f) * (0.7f + amount * 0.5f), Random.Range(0, 4), Color.white);
        }

        public static void BloodPool(Vector3 p, float size)
        {
            if (!_ready) return;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                _bloodDecals.Add(hit.point, hit.normal, size, Random.Range(0, 4), Color.white);
        }

        /// <summary>Head / limb burst: chunks, heavy spray and mist.</summary>
        public static void Gore(Vector3 p, Vector3 dir, float amount)
        {
            if (!_ready) return;
            Blood(p, dir, 1.5f * amount);
            for (int i = 0; i < N((int)(6 * amount)); i++) EmitMesh(_gore, p, (dir * 0.5f + Random.insideUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(2f, 6f), Random.Range(0.04f, 0.09f), Random.Range(1.5f, 3f), Color.white);
            for (int i = 0; i < N(4); i++) Emit(_mist, p + Random.insideUnitSphere * 0.15f, Random.insideUnitSphere * 0.8f, 0.5f * amount, 0.8f, new Color(0.35f, 0.02f, 0.02f, 0.65f));
        }

        public static void Explosion(Vector3 p, float radius, bool toxic = false)
        {
            if (!_ready) return;
            float s = radius / 5f;
            var core = toxic ? new Color(0.5f, 1f, 0.3f, 1) : new Color(1f, 0.55f, 0.2f, 1);
            for (int i = 0; i < N(14); i++) Emit(_fireball, p + Random.insideUnitSphere * radius * 0.25f, Random.insideUnitSphere * 3f * s + Vector3.up * 1.5f, Random.Range(1.5f, 3f) * s, Random.Range(0.35f, 0.7f), core);
            for (int i = 0; i < N(16); i++) Emit(_smoke, p + Random.insideUnitSphere * radius * 0.3f, Random.insideUnitSphere * 2f * s + Vector3.up * 2.2f, Random.Range(1.8f, 3.2f) * s, Random.Range(2f, 4f), toxic ? new Color(0.3f, 0.45f, 0.2f, 0.7f) : new Color(0.16f, 0.15f, 0.14f, 0.75f));
            for (int i = 0; i < N(30); i++) Emit(_sparks, p, Random.onUnitSphere * Random.Range(6f, 16f) + Vector3.up * 3, Random.Range(0.03f, 0.07f), Random.Range(0.4f, 1.1f), Color.white);
            for (int i = 0; i < N(18); i++) EmitMesh(_debris, p + Vector3.up * 0.3f, (Random.onUnitSphere + Vector3.up).normalized * Random.Range(4f, 12f), Random.Range(0.05f, 0.14f), Random.Range(1.5f, 3f), Color.white);
            for (int i = 0; i < N(20); i++) Emit(_embers, p, Random.onUnitSphere * Random.Range(2f, 7f) + Vector3.up * 2, Random.Range(0.04f, 0.08f), Random.Range(1f, 2.5f), new Color(1f, 0.6f, 0.2f, 1));
            LightFlash(p + Vector3.up, toxic ? new Color(0.5f, 1f, 0.3f) : new Color(1f, 0.6f, 0.25f), 9f, radius * 3.5f, 0.35f);
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                if (toxic) _acidDecals.Add(hit.point, hit.normal, radius * 0.7f, Random.Range(0, 4), Color.white);
                else _scorch.Add(hit.point, hit.normal, radius * 0.9f, 0, Color.white);
            }
        }

        /// <summary>One frame of a flamethrower stream.</summary>
        public static void Flame(Vector3 origin, Vector3 dir, float range, Vector3 inherit)
        {
            if (!_ready) return;
            for (int i = 0; i < N(4); i++)
            {
                var v = (dir + Random.insideUnitSphere * 0.08f).normalized * Random.Range(range * 1.2f, range * 1.6f) + inherit;
                Emit(_fire, origin + dir * 0.1f, v, Random.Range(0.25f, 0.5f), Random.Range(0.45f, 0.65f), new Color(1, 1, 1, 0.9f));
            }
            if (Random.value < 0.4f) Emit(_smoke, origin + dir * range * 0.7f, Vector3.up * 1.5f + dir * 2, 1.2f, 1.5f, new Color(0.1f, 0.09f, 0.08f, 0.45f));
        }

        /// <summary>Flames on a burning body part (called a few times per second).</summary>
        public static void Burn(Vector3 p, float size)
        {
            if (!_ready) return;
            Emit(_fire, p + Random.insideUnitSphere * 0.15f * size, Vector3.up * Random.Range(0.8f, 1.6f), Random.Range(0.3f, 0.55f) * size, Random.Range(0.35f, 0.6f), Color.white);
            if (Random.value < 0.3f) Emit(_embers, p, Random.insideUnitSphere + Vector3.up * 2, 0.05f, 1.2f, new Color(1f, 0.6f, 0.2f, 1));
        }

        public static void Gas(Vector3 p, float size, Color c)
        {
            if (!_ready) return;
            Emit(_gas, p + Random.insideUnitSphere * size * 0.3f, Random.insideUnitSphere * 0.25f + Vector3.up * 0.15f, size * Random.Range(0.7f, 1.2f), Random.Range(1.5f, 3f), c);
        }

        public static void AcidSplash(Vector3 p, Vector3 n)
        {
            if (!_ready) return;
            for (int i = 0; i < N(12); i++) Emit(_acid, p, (n + Random.insideUnitSphere).normalized * Random.Range(1.5f, 4f), Random.Range(0.04f, 0.08f), Random.Range(0.3f, 0.7f), new Color(0.5f, 1f, 0.2f, 1));
            _acidDecals.Add(p, n, Random.Range(0.6f, 1f), Random.Range(0, 4), Color.white);
        }

        public static void Smoke(Vector3 p, float size, Color c, Vector3 vel)
        {
            if (!_ready) return;
            Emit(_smoke, p, vel, size, Random.Range(1.2f, 2.5f), c);
        }

        public static void Glow(Vector3 p, float size, Color c, float life = 0.06f)
        {
            if (!_ready) return;
            Emit(_glow, p, Vector3.zero, size, life, c);
        }

        public static void Shell(Vector3 p, Vector3 vel, bool shotgun, float size = 0.012f)
        {
            if (!_ready) return;
            EmitMesh(shotgun ? _shellsBig : _shells, p, vel, shotgun ? size * 1.6f : size, 2.5f, Color.white);
        }

        public static void Sparks(Vector3 p, Vector3 dir, int count, float speed = 5)
        {
            if (!_ready) return;
            for (int i = 0; i < N(count); i++) Emit(_sparks, p, (dir + Random.insideUnitSphere * 0.7f).normalized * Random.Range(speed * 0.4f, speed), Random.Range(0.02f, 0.05f), Random.Range(0.2f, 0.6f), Color.white);
        }

        public static void Tracer(Vector3 a, Vector3 b, Color c, float width = 0.025f, float life = 0.05f)
        {
            if (!_ready) return;
            TracerLine t = null;
            foreach (var x in Tracers) if (x.Life <= 0) { t = x; break; }
            if (t == null) t = Tracers[Random.Range(0, Tracers.Count)];
            t.Line.SetPosition(0, a);
            t.Line.SetPosition(1, b);
            t.Line.startWidth = width * 0.4f;
            t.Line.endWidth = width;
            t.Line.startColor = t.Line.endColor = c;
            t.Line.enabled = true;
            t.Life = t.Max = life;
        }

        /// <summary>Jagged electric arc between two points (energy weapon).</summary>
        public static void Arc(Vector3 a, Vector3 b)
        {
            if (!_ready) return;
            var prev = a;
            int segs = 5;
            for (int i = 1; i <= segs; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)segs);
                if (i < segs) p += Random.insideUnitSphere * 0.25f;
                Tracer(prev, p, new Color(0.45f, 0.75f, 1f, 1f), 0.06f, 0.08f);
                prev = p;
            }
            Glow(b, 0.6f, new Color(0.4f, 0.7f, 1f, 1f), 0.1f);
        }

        public static void LightFlash(Vector3 p, Color c, float intensity, float range, float duration)
        {
            if (!_ready) return;
            FlashLight f = null;
            float best = 999;
            foreach (var x in Flashes) if (x.Life < best) { best = x.Life; f = x; }
            f.Light.transform.position = p;
            f.Light.color = c;
            f.Light.range = range;
            f.Intensity = intensity * 5f;
            f.Light.intensity = f.Intensity;
            f.Light.enabled = true;
            f.Life = f.Max = duration;
        }

        public static void Decal(DecalKind k, Vector3 p, Vector3 n, float size)
        {
            if (!_ready) return;
            switch (k)
            {
                case DecalKind.Blood: _bloodDecals.Add(p, n, size, Random.Range(0, 4), Color.white); break;
                case DecalKind.Hole: _holes.Add(p, n, size, 0, Color.white); break;
                case DecalKind.Scorch: _scorch.Add(p, n, size, 0, Color.white); break;
                case DecalKind.Acid: _acidDecals.Add(p, n, size, Random.Range(0, 4), Color.white); break;
            }
        }

        /// <summary>Removes decals and particles (between stages).</summary>
        public static void ClearWorld()
        {
            if (!_ready) return;
            _bloodDecals.Clear();
            _holes.Clear();
            _scorch.Clear();
            _acidDecals.Clear();
            foreach (var ps in _root.GetComponentsInChildren<ParticleSystem>()) ps.Clear();
            foreach (var t in Tracers) { t.Life = 0; t.Line.enabled = false; }
        }

        // ------------------------------------------------------------------ weather

        public static void SetWeather(Weather w, bool indoor, Transform follow)
        {
            Init();
            _follow = follow;
            _weatherKind = w;
            if (_weather != null) Object.Destroy(_weather.gameObject);
            if (_motes != null) Object.Destroy(_motes.gameObject);
            _weather = null;
            // Floating dust in light beams (everywhere).
            _motes = Ambient("Motes", Mats.Particle(Tex.Soft, true), 120, new Vector3(16, 6, 16), 0.04f, new Color(1f, 0.9f, 0.75f, 0.35f), 0.15f, 6, Vector3.zero, false);
            if (indoor && w != Weather.Fog) return;
            switch (w)
            {
                case Weather.Rain:
                case Weather.Storm:
                    if (!indoor) _weather = Ambient("Rain", Mats.Particle(Tex.Streak, false), w == Weather.Storm ? 1400 : 900, new Vector3(26, 1, 26), 0.035f, new Color(0.7f, 0.75f, 0.85f, 0.5f), 0, 0.9f, new Vector3(0.6f, -17f, 0.3f), true);
                    break;
                case Weather.Snow:
                    _weather = Ambient("Snow", Mats.Particle(Tex.Soft, false), 900, new Vector3(26, 1, 26), 0.06f, new Color(1f, 1f, 1f, 0.85f), 0.6f, 6f, new Vector3(1.2f, -1.6f, 0.4f), false);
                    break;
                case Weather.Dust:
                    _weather = Ambient("Dust", Mats.Particle(Tex.Smoke, false), 220, new Vector3(30, 4, 30), 2.2f, new Color(0.6f, 0.45f, 0.3f, 0.12f), 1.2f, 5f, new Vector3(4f, 0.2f, 1f), false);
                    break;
                case Weather.Ash:
                    _weather = Ambient("Ash", Mats.Particle(Tex.Soft, false), 500, new Vector3(26, 1, 26), 0.05f, new Color(0.35f, 0.33f, 0.32f, 0.8f), 0.5f, 7f, new Vector3(0.5f, -0.9f, 0.2f), false);
                    break;
                case Weather.Fog:
                    _weather = Ambient("FogBanks", Mats.Particle(Tex.Smoke, false), 60, new Vector3(36, 2, 36), 6f, new Color(0.6f, 0.62f, 0.65f, 0.06f), 1.5f, 10f, new Vector3(0.3f, 0, 0.2f), false);
                    break;
            }
        }

        static ParticleSystem Ambient(string name, Material mat, int max, Vector3 box, float size, Color col, float jitter, float life, Vector3 vel, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            int count = Mathf.Max(20, (int)(max * Quality.ParticleMul));
            main.maxParticles = count;
            main.startLifetime = life;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = col;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = count / life;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.position = new Vector3(0, vel.y < -3 ? 9 : vel.y < 0 ? 6 : 1.5f, 0);
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(vel.x - jitter, vel.x + jitter);
            vol.y = new ParticleSystem.MinMaxCurve(vel.y - jitter * 0.5f, vel.y + jitter * 0.5f);
            vol.z = new ParticleSystem.MinMaxCurve(vel.z - jitter, vel.z + jitter);
            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
            colLife.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1f;
            }
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ tick

        public static void Tick(float dt)
        {
            if (!_ready) return;
            foreach (var t in Tracers)
            {
                if (t.Life <= 0) continue;
                t.Life -= dt;
                if (t.Life <= 0) t.Line.enabled = false;
                else
                {
                    var c = t.Line.startColor;
                    c.a = t.Life / t.Max;
                    t.Line.startColor = t.Line.endColor = c;
                }
            }
            foreach (var f in Flashes)
            {
                if (f.Life <= 0) continue;
                f.Life -= dt;
                if (f.Life <= 0) f.Light.enabled = false;
                else f.Light.intensity = f.Intensity * (f.Life / f.Max);
            }
            if (_follow != null)
            {
                var fp = _follow.position;
                if (_weather != null) _weather.transform.position = new Vector3(fp.x, fp.y, fp.z) + _follow.forward.Flat() * 6;
                if (_motes != null) _motes.transform.position = fp + _follow.forward.Flat() * 5;
            }
        }
    }

    /// <summary>A fixed-capacity ring of decal quads in one mesh (one draw call per decal type).</summary>
    public sealed class DecalBuffer
    {
        readonly Mesh _mesh;
        readonly Vector3[] _v;
        readonly Vector3[] _n;
        readonly Vector2[] _uv;
        readonly Color[] _c;
        readonly int[] _tri;
        readonly int _cap, _atlas;
        int _next, _count;
        readonly GameObject _go;

        public DecalBuffer(string name, Material mat, int atlas, int capacity)
        {
            _cap = Mathf.Max(8, capacity);
            _atlas = atlas;
            _v = new Vector3[_cap * 4];
            _n = new Vector3[_cap * 4];
            _uv = new Vector2[_cap * 4];
            _c = new Color[_cap * 4];
            _tri = new int[_cap * 6];
            for (int i = 0; i < _cap; i++)
            {
                int b = i * 4;
                _tri[i * 6] = b; _tri[i * 6 + 1] = b + 1; _tri[i * 6 + 2] = b + 2;
                _tri[i * 6 + 3] = b; _tri[i * 6 + 4] = b + 2; _tri[i * 6 + 5] = b + 3;
            }
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _mesh.vertices = _v;
            _mesh.normals = _n;
            _mesh.uv = _uv;
            _mesh.colors = _c;
            _mesh.triangles = _tri;
            _go = new GameObject(name);
            Object.DontDestroyOnLoad(_go);
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = _go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        public void Add(Vector3 p, Vector3 n, float size, int cell, Color col)
        {
            var t = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            t = Quaternion.AngleAxis(Random.Range(0, 360f), n) * t;
            var b = Vector3.Cross(t, n);
            var c = p + n * (0.012f + _count % 7 * 0.0015f);
            float h = size * 0.5f;
            int i = _next * 4;
            _v[i] = c - t * h - b * h;
            _v[i + 1] = c - t * h + b * h;
            _v[i + 2] = c + t * h + b * h;
            _v[i + 3] = c + t * h - b * h;
            for (int k = 0; k < 4; k++) { _n[i + k] = n; _c[i + k] = col; }
            float cs = 1f / _atlas;
            float u0 = (cell % _atlas) * cs, v0 = (cell / _atlas % _atlas) * cs;
            _uv[i] = new Vector2(u0, v0);
            _uv[i + 1] = new Vector2(u0, v0 + cs);
            _uv[i + 2] = new Vector2(u0 + cs, v0 + cs);
            _uv[i + 3] = new Vector2(u0 + cs, v0);
            _next = (_next + 1) % _cap;
            _count++;
            _mesh.vertices = _v;
            _mesh.normals = _n;
            _mesh.uv = _uv;
            _mesh.colors = _c;
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(1);
            _mesh.bounds = bounds;
        }

        public void Clear()
        {
            for (int i = 0; i < _v.Length; i++) _v[i] = Vector3.zero;
            _mesh.vertices = _v;
            _mesh.RecalculateBounds();
            _next = 0;
            _count = 0;
        }
    }
}
