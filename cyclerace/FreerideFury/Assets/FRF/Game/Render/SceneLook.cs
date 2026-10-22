using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FRF
{
    /// <summary>
    /// Lighting and camera look per mountain: sun, procedural sky, ambient, fog and
    /// post-processing. Cost scales with Settings.Graphics so phones stay cool.
    /// </summary>
    public static class SceneLook
    {
        static GraphicsQuality Q => Settings.Graphics;

        public static Camera MainCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 3200;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 62;
            ApplyCamera(cam);
            return cam;
        }

        static void ApplyCamera(Camera cam)
        {
            var q = Q;
            cam.allowHDR = q == GraphicsQuality.High;
            cam.allowMSAA = q == GraphicsQuality.High;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = q != GraphicsQuality.Low;
            data.antialiasing = q == GraphicsQuality.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : q == GraphicsQuality.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            data.renderShadows = q != GraphicsQuality.Low;
        }

        /// <summary>Render pipeline cost knobs for the chosen graphics level.</summary>
        public static void ApplyPipeline()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            var q = Q;
            urp.renderScale = q == GraphicsQuality.High ? 1f : q == GraphicsQuality.Medium ? 0.85f : 0.7f;
            urp.supportsHDR = q == GraphicsQuality.High;
            urp.msaaSampleCount = q == GraphicsQuality.High ? 2 : 1;
            urp.shadowDistance = q == GraphicsQuality.High ? 120 : q == GraphicsQuality.Medium ? 60 : 0;
            urp.shadowCascadeCount = q == GraphicsQuality.High ? 2 : 1;
            urp.mainLightShadowmapResolution = q == GraphicsQuality.High ? 2048 : 1024;
            urp.maxAdditionalLightsCount = 0;
            var cam = Camera.main;
            if (cam != null) ApplyCamera(cam);
        }

        static GameObject _root;
        public static Light Sun { get; private set; }

        public static Vector3 SunDirection(Theme th)
        {
            float el = th.SunElevation * Mathf.Deg2Rad, az = th.SunAzimuth * Mathf.Deg2Rad;
            // Direction the light travels (from the sun towards the ground).
            return -new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        }

        /// <summary>(Re)builds lights, sky, fog and post-processing under <paramref name="parent"/>.</summary>
        public static void Apply(Theme th, Transform parent)
        {
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("Lighting");
            _root.transform.SetParent(parent, false);
            ApplyPipeline();
            var q = Q;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = q == GraphicsQuality.High ? LightShadows.Soft : q == GraphicsQuality.Medium ? LightShadows.Hard : LightShadows.None;
            sun.color = th.Sun;
            sun.intensity = th.SunIntensity;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.6f;
            sun.shadowNormalBias = 0.6f;
            // Shadows want the sun not too low; the sky disc keeps the theme's real elevation.
            sunGo.transform.rotation = Quaternion.LookRotation(SunDirection(th), Vector3.up);
            RenderSettings.sun = sun;
            Sun = sun;

            var skyBase = Resources.Load<Material>("Materials/FRF_Sky");
            Material sky = skyBase != null ? new Material(skyBase) : null;
            if (sky == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null) sky = new Material(sh);
            }
            if (sky != null)
            {
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_SunSizeConvergence", 5);
                sky.SetFloat("_AtmosphereThickness", th.Atmosphere);
                sky.SetColor("_SkyTint", th.SkyTint);
                sky.SetColor("_GroundColor", th.SkyGround);
                sky.SetFloat("_Exposure", th.Exposure);
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = th.AmbSky;
            RenderSettings.ambientEquatorColor = th.AmbEquator;
            RenderSettings.ambientGroundColor = th.AmbGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = th.FogStart;
            RenderSettings.fogEndDistance = th.FogEnd * (q == GraphicsQuality.Low ? 0.8f : 1f);
            RenderSettings.fogColor = th.Fog;

            if (q == GraphicsQuality.Low) return;
            var volGo = new GameObject("PostFX");
            volGo.transform.SetParent(_root.transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = profile;
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.45f);
            ca.contrast.Override(16);
            ca.saturation.Override(th.Biome == Biome.Glacier ? 6 : 18);
            if (q != GraphicsQuality.High) return;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(th.Lava ? 0.9f : 0.35f);
            bloom.scatter.Override(0.68f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
        }

        /// <summary>Removes the lighting rig (between scenes).</summary>
        public static void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }
    }
}
