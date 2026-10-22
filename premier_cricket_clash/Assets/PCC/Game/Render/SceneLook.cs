using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCC.Game
{
    /// <summary>
    /// Lighting and camera look: afternoon sun with shadows and a sky with clouds, or a
    /// floodlit night. Post-processing and cost scale with Settings.Graphics so phones stay cool.
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
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 2500;
            cam.clearFlags = CameraClearFlags.Skybox;
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
            urp.shadowDistance = q == GraphicsQuality.High ? 150 : q == GraphicsQuality.Medium ? 70 : 0;
            urp.shadowCascadeCount = q == GraphicsQuality.High ? 2 : 1;
            urp.mainLightShadowmapResolution = q == GraphicsQuality.High ? 2048 : 1024;
            urp.maxAdditionalLightsCount = q == GraphicsQuality.High ? 4 : 0;
            var cam = Camera.main;
            if (cam != null) ApplyCamera(cam);
        }

        static GameObject _root;

        /// <summary>(Re)builds lights, sky and post-processing under <paramref name="parent"/>.</summary>
        public static void Apply(bool day, Transform parent)
        {
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("Lighting");
            _root.transform.SetParent(parent, false);
            ApplyPipeline();
            var q = Q;
            bool floodSpots = !day && q == GraphicsQuality.High;

            // Key light: the sun by day, the floodlight rig at night.
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = q == GraphicsQuality.High ? LightShadows.Soft : q == GraphicsQuality.Medium ? LightShadows.Hard : LightShadows.None;
            if (day)
            {
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.intensity = 1.55f;
                sun.shadowStrength = 0.85f;
                sunGo.transform.rotation = Quaternion.LookRotation(-Stadium.SunDir.U, Vector3.up);
            }
            else
            {
                sun.color = new Color(0.93f, 0.95f, 1f);
                // Without the four spot pools the key light carries the floodlit look on its own.
                sun.intensity = floodSpots ? 0.95f : 1.3f;
                sun.shadowStrength = 0.55f;
                sunGo.transform.rotation = Quaternion.LookRotation(-new V3(0.18, -0.32, 0.93).Normalized.U, Vector3.up);
            }
            RenderSettings.sun = sun;

            // Sky.
            var skyBase = Resources.Load<Material>("Materials/PCC_Sky");
            Material sky = skyBase != null ? new Material(skyBase) : null;
            if (sky == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null) sky = new Material(sh);
            }
            if (sky != null)
            {
                if (day)
                {
                    sky.SetFloat("_SunSize", 0.035f);
                    sky.SetFloat("_SunSizeConvergence", 6);
                    sky.SetFloat("_AtmosphereThickness", 0.85f);
                    sky.SetColor("_SkyTint", new Color(0.36f, 0.55f, 0.95f));
                    sky.SetColor("_GroundColor", new Color(0.42f, 0.44f, 0.47f));
                    sky.SetFloat("_Exposure", 1.25f);
                }
                else
                {
                    sky.SetFloat("_SunSize", 0);
                    sky.SetFloat("_AtmosphereThickness", 0.45f);
                    sky.SetColor("_SkyTint", new Color(0.25f, 0.18f, 0.45f));
                    sky.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.04f));
                    sky.SetFloat("_Exposure", 0.07f);
                }
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (day)
            {
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.88f);
                RenderSettings.ambientEquatorColor = new Color(0.58f, 0.6f, 0.58f);
                RenderSettings.ambientGroundColor = new Color(0.32f, 0.36f, 0.26f);
            }
            else
            {
                float boost = floodSpots ? 1f : 1.25f;
                RenderSettings.ambientSkyColor = new Color(0.2f, 0.22f, 0.34f) * boost;
                RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.28f) * boost;
                RenderSettings.ambientGroundColor = new Color(0.1f, 0.14f, 0.1f) * boost;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = day ? 260 : 200;
            RenderSettings.fogEndDistance = day ? 1500 : 900;
            RenderSettings.fogColor = day ? new Color(0.73f, 0.8f, 0.9f) : new Color(0.03f, 0.035f, 0.07f);

            // Floodlight pools at night (per-pixel spot lights: HIGH only, they are the costliest part).
            if (floodSpots)
            {
                foreach (double phi in Stadium.TowerAngles)
                {
                    var go = new GameObject("Floodlight");
                    go.transform.SetParent(_root.transform, false);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.color = new Color(1f, 0.97f, 0.9f);
                    // URP falls off with 1/d^2: ~6000 gives ~0.4 at the 125 m to the square.
                    l.intensity = 6000f;
                    l.range = 260;
                    l.spotAngle = 70;
                    l.shadows = LightShadows.None;
                    var pos = new V3(System.Math.Cos(phi) * 110, Pitch.Mid + System.Math.Sin(phi) * 114, 54);
                    go.transform.position = pos.U;
                    go.transform.LookAt(new V3(0, Pitch.Mid, 0).U);
                }
            }

            // Post-processing: none on LOW; colour grading on MEDIUM; plus bloom and vignette on HIGH.
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
            ca.postExposure.Override(day ? 0.35f : 0.45f);
            ca.contrast.Override(14);
            ca.saturation.Override(16);
            if (q != GraphicsQuality.High) return;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(day ? 1.05f : 0.85f);
            bloom.intensity.Override(day ? 0.3f : 0.9f);
            bloom.scatter.Override(0.68f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);
        }
    }
}
