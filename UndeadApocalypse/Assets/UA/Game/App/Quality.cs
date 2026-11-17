using UnityEngine;

namespace UA.Game
{
    public enum Gfx { Low = 0, Medium = 1, Ultra = 2 }

    /// <summary>
    /// LOW / MEDIUM / ULTRA graphics presets. Each maps to a Quality level with its own URP asset
    /// (render scale, MSAA, shadows, HDR, light counts, SSAO on ULTRA), plus runtime choices:
    /// texture resolution, enemy cap, particle density and post-processing.
    /// Adaptive mode drops one preset if the frame rate stays well under target.
    /// </summary>
    public static class Quality
    {
        public static readonly string[] Names = { "LOW", "MEDIUM", "ULTRA" };
        public static Gfx Current { get; private set; } = Gfx.Medium;
        public static event System.Action Changed;

        public static Gfx Detect()
        {
            if (!Application.isMobilePlatform) return Gfx.Ultra;
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            if (ram >= 7000 && cores >= 8 && SystemInfo.maxTextureSize >= 8192) return Gfx.Ultra;
            if (ram >= 3500 && cores >= 6) return Gfx.Medium;
            return Gfx.Low;
        }

        public static void Apply(Gfx g)
        {
            Current = g;
            int idx = Mathf.Clamp((int)g, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != idx) QualitySettings.SetQualityLevel(idx, true);
            QualitySettings.vSyncCount = 0;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.anisotropicFiltering = g == Gfx.Ultra ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            QualitySettings.skinWeights = g == Gfx.Low ? SkinWeights.TwoBones : SkinWeights.FourBones;
            QualitySettings.lodBias = g == Gfx.Low ? 0.7f : g == Gfx.Medium ? 1f : 1.5f;
            QualitySettings.shadowDistance = g == Gfx.Low ? 25 : g == Gfx.Medium ? 40 : 60;
            QualitySettings.particleRaycastBudget = g == Gfx.Low ? 16 : 64;
            Changed?.Invoke();
        }

        public static int MaxZombies => Current == Gfx.Low ? 10 : Current == Gfx.Medium ? 16 : 24;
        public static float ParticleMul => Current == Gfx.Low ? 0.45f : Current == Gfx.Medium ? 0.75f : 1f;
        public static int MaxDecals => Current == Gfx.Low ? 40 : Current == Gfx.Medium ? 90 : 160;
        public static int MaxCorpses => Current == Gfx.Low ? 4 : Current == Gfx.Medium ? 8 : 14;
        public static bool LightShafts => Current != Gfx.Low;
        public static bool FlashlightShadows => Current == Gfx.Ultra;
        /// <summary>Reflection probes on every preset (Low gets a single small probe captured once at load).</summary>
        public static bool Reflections => true;
        public static float PropDensity => Current == Gfx.Low ? 0.55f : Current == Gfx.Medium ? 0.85f : 1f;
        /// <summary>Level lights per stage (mobile GPUs cap clustered lighting at 32 visible lights).</summary>
        public static int LightBudget => Current == Gfx.Low ? 16 : Current == Gfx.Medium ? 22 : Application.isMobilePlatform ? 28 : 40;
        public static int ZombieSegs => Current == Gfx.Low ? 8 : Current == Gfx.Medium ? 11 : 14;

        public static int TargetFps => SaveSystem.Data.Settings.Fps60 ? 60 : 30;

        public static void ApplyFrameRate(bool inGame)
        {
            Application.targetFrameRate = inGame || !Application.isMobilePlatform ? TargetFps : 30;
        }

        public static Gfx FromSave()
        {
            var s = SaveSystem.Data.Settings;
            if (s.Graphics < 0 || s.Graphics > 2)
            {
                var d = Detect();
                s.Graphics = (int)d;
                SaveSystem.MarkDirty();
                return d;
            }
            return (Gfx)s.Graphics;
        }

        // --------------------------------------------------------------- adaptive

        static float _accum;
        static int _frames;
        static float _lowTime;
        public static float SmoothedFps { get; private set; } = 60;

        /// <summary>Called every frame in game. Lowers the preset after ~8 s well below target.</summary>
        public static void Tick(float unscaledDt)
        {
            _accum += unscaledDt;
            _frames++;
            if (_accum < 1f) return;
            SmoothedFps = _frames / _accum;
            _accum = 0;
            _frames = 0;
            var s = SaveSystem.Data.Settings;
            if (!s.AdaptiveQuality || Current == Gfx.Low || AutoPilot.Active) { _lowTime = 0; return; }
            if (SmoothedFps < TargetFps * 0.72f) _lowTime += 1;
            else _lowTime = Mathf.Max(0, _lowTime - 0.5f);
            if (_lowTime >= 8)
            {
                _lowTime = 0;
                var next = (Gfx)((int)Current - 1);
                Log.I("Adaptive quality: " + SmoothedFps.ToString("0") + " fps, dropping to " + Names[(int)next]);
                s.Graphics = (int)next;
                SaveSystem.MarkDirty();
                Apply(next);
                Hud.Toast("Graphics lowered to " + Names[(int)next] + " for a smoother frame rate");
            }
        }
    }
}
