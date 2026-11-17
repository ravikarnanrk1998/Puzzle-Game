using UnityEngine;

namespace KPL.Game
{
    /// <summary>Particle effects built in code: pitch dust, fireworks over the stands, confetti.</summary>
    public static class Fx
    {
        static Material _additive, _alpha;

        static Material Additive => _additive != null ? _additive : (_additive = Mats.Additive(Color.white, Tex.Glow));
        static Material Alpha => _alpha != null ? _alpha : (_alpha = Mats.UnlitTransparent(Color.white, Tex.Glow));

        static ParticleSystem Make(Transform parent, string name, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return ps;
        }

        public static void Dust(Transform parent, Vector3 at)
        {
            if (Quality.Current == Gfx.Low) return;
            var ps = Make(parent, "Dust", at, Alpha);
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new Color(0.72f, 0.6f, 0.42f, 0.55f);
            main.gravityModifier = 0.15f;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 10) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
        }

        /// <summary>Bursts of coloured sparks above the stands.</summary>
        public static void Fireworks(Transform parent, int bursts)
        {
            if (Quality.Current == Gfx.Low) bursts = Mathf.Min(bursts, 2);
            for (int i = 0; i < bursts; i++)
            {
                float a = Random.Range(0, Mathf.PI * 2);
                var pos = new Vector3(Mathf.Sin(a) * 85, Random.Range(45, 65), Mathf.Cos(a) * 85);
                var ps = Make(parent, "Firework", pos, Additive);
                var main = ps.main;
                main.duration = 0.2f;
                main.startDelay = i * 0.35f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(10, 18);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                Color c = Color.HSVToRGB(Random.value, 0.7f, 1f) * 2.2f;
                main.startColor = c;
                main.gravityModifier = 0.35f;
                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0, Quality.Current >= Gfx.High ? 90 : 45) });
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.5f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                col.color = g;
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.2f));
                ps.Play();
            }
        }
    }
}
