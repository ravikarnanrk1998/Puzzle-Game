using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Shared particle systems: one alpha-blended pool (dust, spray, smoke puffs) and one
    /// additive pool (boost streaks, sparks, orb bursts), plus weather around the camera.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        public static Fx I { get; private set; }
        ParticleSystem _dust, _glow, _weather, _plume;
        int _budget;

        public static Fx Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Fx>();
            int max = Settings.Graphics == GraphicsQuality.Low ? 300 : Settings.Graphics == GraphicsQuality.Medium ? 700 : 1400;
            I._budget = max;
            I._dust = Pool(go.transform, "Dust", Mats.Particle(Tex.Puff, false), max, true);
            I._glow = Pool(go.transform, "Glow", Mats.Particle(Tex.GlowTex, true), max / 2, false);
            return I;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        static ParticleSystem Pool(Transform parent, string name, Material mat, int max, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1;
            main.startSpeed = 0;
            main.gravityModifier = grow ? -0.02f : 0.3f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(grow ? 0.85f : 1, 0), new GradientAlphaKey(grow ? 0.5f : 0.7f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grow ? AnimationCurve.EaseInOut(0, 0.5f, 1, 2.2f) : AnimationCurve.Linear(0, 1, 1, 0.2f));
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = grow ? 0.12f : 0.05f;
            limit.limit = 0.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingFudge = grow ? 0 : -10;
            r.minParticleSize = 0;
            r.maxParticleSize = 2;
            ps.Play();
            return ps;
        }

        public void Dust(Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startColor = c, startSize = size, startLifetime = life,
                rotation = Random.value * 360, applyShapeToPosition = false,
            };
            _dust.Emit(ep, 1);
        }

        public void Glow(Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, startColor = c, startSize = size, startLifetime = life, applyShapeToPosition = false };
            _glow.Emit(ep, 1);
        }

        /// <summary>Radial puff burst (landings, crashes).</summary>
        public void Burst(Vector3 pos, Color c, int count, float speed, float size)
        {
            if (Settings.Graphics == GraphicsQuality.Low) count = Mathf.Max(2, count / 2);
            for (int i = 0; i < count; i++)
            {
                var d = Random.insideUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.6f + 0.15f;
                Dust(pos + d * 0.3f, d * speed, c.WithA(0.9f), size * (0.7f + Random.value * 0.6f), 0.7f + Random.value * 0.8f);
            }
        }

        public void Sparks(Vector3 pos, Color c, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var d = Random.onUnitSphere;
                Glow(pos, d * speed * (0.5f + Random.value), c, 0.18f + Random.value * 0.12f, 0.3f + Random.value * 0.3f);
            }
        }

        // ------------------------------------------------------------ weather and plumes

        Transform _follow;

        public void StartWeather(Weather w, Transform follow)
        {
            if (_weather != null) Destroy(_weather.gameObject);
            _weather = null;
            _follow = follow;
            if (w == Weather.None) return;
            var go = new GameObject("Weather");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            float q = Settings.Graphics == GraphicsQuality.Low ? 0.4f : Settings.Graphics == GraphicsQuality.Medium ? 0.7f : 1f;
            var em = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 2, 50);
            shape.position = new Vector3(0, 12, 24);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.85f), new GradientAlphaKey(0, 1) });
            col.color = g;
            switch (w)
            {
                case Weather.Snow:
                    main.startLifetime = 4.5f;
                    main.startSpeed = 0.5f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                    main.gravityModifier = 0.12f;
                    main.startColor = new Color(1, 1, 1, 0.95f);
                    main.maxParticles = (int)(1200 * q);
                    em.rateOverTime = 260 * q;
                    r.sharedMaterial = Mats.Particle(Tex.Flake, false);
                    break;
                case Weather.Leaves:
                    main.startLifetime = 6f;
                    main.startSpeed = 0.8f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                    main.gravityModifier = 0.05f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = new ParticleSystem.MinMaxGradient(Mats.Hex(0xE07A2A), Mats.Hex(0xC9402A));
                    main.maxParticles = (int)(260 * q);
                    em.rateOverTime = 40 * q;
                    var rot = ps.rotationOverLifetime;
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(-3, 3);
                    var noise = ps.noise;
                    noise.enabled = true;
                    noise.strength = 1.2f;
                    noise.frequency = 0.4f;
                    r.sharedMaterial = Mats.Particle(Tex.Leaf, false);
                    break;
                default:
                    main.startLifetime = 5f;
                    main.startSpeed = 0.3f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                    main.gravityModifier = 0.03f;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.22f, 0.2f, 0.9f), new Color(1f, 0.45f, 0.15f, 1f));
                    main.maxParticles = (int)(700 * q);
                    em.rateOverTime = 120 * q;
                    r.sharedMaterial = Mats.Particle(Tex.Flake, true);
                    break;
            }
            ps.Play();
            _weather = ps;
        }

        public void Plume(Vector3 at)
        {
            if (_plume != null) Destroy(_plume.gameObject);
            var go = new GameObject("Plume");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 40;
            main.startSpeed = 9;
            main.startSize = new ParticleSystem.MinMaxCurve(120, 220);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.15f, 0.14f, 0.7f), new Color(0.35f, 0.28f, 0.25f, 0.6f));
            main.maxParticles = 80;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 1.6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12;
            shape.radius = 60;
            shape.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(6);
            vel.y = new ParticleSystem.MinMaxCurve(0);
            vel.z = new ParticleSystem.MinMaxCurve(2);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Tex.Puff, false);
            r.maxParticleSize = 50;
            ps.Play();
            _plume = ps;
        }

        void LateUpdate()
        {
            if (_weather != null && _follow != null)
            {
                var p = _follow.position;
                var f = _follow.forward;
                f.y = 0;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                // The box sits ahead of the camera so riders move into fresh flakes.
                _weather.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f.normalized));
            }
        }
    }
}
