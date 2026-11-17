using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    public enum Gfx { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>
    /// The four graphics profiles. Each maps to a Quality level with its own URP asset (render scale,
    /// MSAA, shadow resolution and distance, HDR, extra lights, SSAO on ULTRA), plus runtime choices
    /// such as texture resolution, crowd detail, floodlight spot lights and post-processing.
    /// </summary>
    public static class Quality
    {
        public static readonly string[] Names = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
        public static Gfx Current { get; private set; } = Gfx.Medium;

        /// <summary>Picks a sensible default from the device's memory, CPU and GPU.</summary>
        public static Gfx Detect()
        {
            if (!Application.isMobilePlatform) return SystemInfo.graphicsMemorySize >= 3500 ? Gfx.Ultra : Gfx.High;
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            int maxTex = SystemInfo.maxTextureSize;
            if (ram >= 7500 && cores >= 8 && maxTex >= 8192) return Gfx.High;
            if (ram >= 5000 && cores >= 6) return Gfx.Medium;
            return Gfx.Low;
        }

        public static void Apply(Gfx g)
        {
            Current = g;
            int levels = QualitySettings.names.Length;
            int idx = Mathf.Clamp((int)g, 0, levels - 1);
            if (QualitySettings.GetQualityLevel() != idx) QualitySettings.SetQualityLevel(idx, true);
            QualitySettings.vSyncCount = 0;
            QualitySettings.globalTextureMipmapLimit = g == Gfx.Low ? 1 : 0;
            QualitySettings.anisotropicFiltering = g >= Gfx.High ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            QualitySettings.skinWeights = g >= Gfx.High ? SkinWeights.FourBones : SkinWeights.TwoBones;
            QualitySettings.lodBias = g == Gfx.Low ? 0.6f : g == Gfx.Medium ? 0.9f : g == Gfx.High ? 1.2f : 1.6f;
            QualitySettings.shadowDistance = g == Gfx.Low ? 45 : g == Gfx.Medium ? 70 : g == Gfx.High ? 100 : 140;
            SceneLook.RefreshQuality();
        }

        public static int TexSize(int high) => Current == Gfx.Low ? high / 2 : Current == Gfx.Ultra ? high : Current == Gfx.High ? high : Mathf.Max(64, high * 3 / 4);
        public static int KitSize => Current >= Gfx.High ? 512 : 256;
        public static bool SpotLights => Current >= Gfx.High;
        public static bool Bloom => Current >= Gfx.Medium;
        public static bool Vignette => Current >= Gfx.High;
        public static bool Skyline => Current >= Gfx.Medium;
        public static int CrowdRows => Current == Gfx.Low ? 6 : 8;
        public static float RopeDetail => Current == Gfx.Low ? 0.5f : 1f;

        public static int TargetFps(bool inMatch)
        {
            var s = SaveSystem.Data.Settings;
            if (s.Fps60) return 60;
            return 30;
        }

        public static void ApplyFrameRate(bool inMatch) => Application.targetFrameRate = TargetFps(inMatch);

        public static Gfx FromSave()
        {
            var s = SaveSystem.Data.Settings;
            if (s.Graphics < 0)
            {
                var d = Detect();
                s.Graphics = (int)d;
                SaveSystem.MarkDirty();
                return d;
            }
            return (Gfx)Mathf.Clamp(s.Graphics, 0, 3);
        }
    }
}
