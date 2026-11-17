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

        /// <summary>True when the current scene uses a photographed (Poly Haven) sky.</summary>
        public static bool PhotoSky { get; private set; }

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

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = th.FogStart;
            RenderSettings.fogEndDistance = th.FogEnd * (q == GraphicsQuality.Low ? 0.8f : 1f);

            var photo = Art.Sky(th.Sky);
            PhotoSky = photo != null;
            if (photo != null)
            {
                // Photographed sky: the sun, haze and ambient light all come from the panorama.
                var pano = new Material(Mats.SkyPanoBase);
                pano.SetTexture("_MainTex", photo.Texture);
                pano.SetFloat("_Exposure", th.SkyExposure);
                pano.SetFloat("_Rotation", th.SkyRotation);
                RenderSettings.skybox = pano;
                var sd = photo.SunDirection(th.SkyRotation);
                // Keep shadows readable: clamp the sun between 16 and 62 degrees up, same bearing.
                float el = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(sd.y, -1, 1)), 16 * Mathf.Deg2Rad, 62 * Mathf.Deg2Rad);
                var flat = new Vector3(sd.x, 0, sd.z);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                sd = flat.normalized * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el);
                sunGo.transform.rotation = Quaternion.LookRotation(-sd, Vector3.up);
                RenderSettings.ambientSkyColor = photo.Sky * 1.05f;
                RenderSettings.ambientEquatorColor = photo.Horizon * 0.95f;
                RenderSettings.ambientGroundColor = Color.Lerp(photo.Ground, th.AmbGround, 0.4f) * 0.9f;
                RenderSettings.fogColor = photo.Horizon;
                if (q != GraphicsQuality.Low)
                {
                    // One small capture of the sky only, so the lake and glossy paint reflect the photo.
                    var probeGo = new GameObject("SkyReflection");
                    probeGo.transform.SetParent(_root.transform, false);
                    var probe = probeGo.AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Realtime;
                    probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                    probe.cullingMask = 0;
                    probe.resolution = 128;
                    probe.size = new Vector3(20000, 20000, 20000);
                    probe.importance = 0;
                    probe.RenderProbe();
                }
            }
            else
            {
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
                CloudDome.Create(th, _root.transform);
                RenderSettings.ambientSkyColor = th.AmbSky;
                RenderSettings.ambientEquatorColor = th.AmbEquator;
                RenderSettings.ambientGroundColor = th.AmbGround;
                RenderSettings.fogColor = th.Fog;
            }

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
            ca.postExposure.Override(0.5f);
            ca.contrast.Override(18);
            ca.saturation.Override(th.Biome == Biome.Glacier ? 10 : 26);
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

    /// <summary>
    /// A flat cloud layer seen from below: a dome far beyond the scenery that follows the
    /// camera, textured by projecting each view direction onto a plane overhead.
    /// </summary>
    public sealed class CloudDome : MonoBehaviour
    {
        const float Radius = 2600;

        public static void Create(Theme th, Transform parent)
        {
            float cover = th.Biome == Biome.Canyon ? 0.15f : th.Biome == Biome.Volcano ? 0.75f : th.Biome == Biome.Glacier ? 0.55f : 0.45f;
            var mat = new Material(Mats.CloudsBase);
            mat.SetTexture("_BaseMap", Tex.Clouds(cover, (int)th.Biome * 17 + 3));
            var tint = th.Biome == Biome.Volcano ? new Color(0.62f, 0.52f, 0.5f) : th.Biome == Biome.Autumn ? new Color(1f, 0.93f, 0.85f) : Color.white;
            mat.SetColor("_BaseColor", tint.WithA(th.Biome == Biome.Canyon ? 0.75f : 0.95f));
            mat.SetColor("_ShadeColor", Color.Lerp(th.SkyTint, th.Fog, 0.5f).Lighten(th.Biome == Biome.Volcano ? 0f : 0.25f));
            var go = MeshKit.Obj("Clouds", parent, Dome(), mat, false, false);
            go.AddComponent<CloudDome>();
        }

        static Mesh Dome()
        {
            var b = new MeshBuilder();
            const int rings = 10, seg = 32;
            for (int i = 0; i <= rings; i++)
            {
                // Elevation from 2 to 90 degrees, denser near the horizon.
                float t = i / (float)rings;
                float el = Mathf.Lerp(2, 90, t * t) * Mathf.Deg2Rad;
                for (int j = 0; j <= seg; j++)
                {
                    float az = j / (float)seg * Mathf.PI * 2;
                    var dir = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
                    // Plane projection: clouds shrink towards the horizon like a real layer.
                    var uv = new Vector2(dir.x, dir.z) / Mathf.Max(0.05f, dir.y) * 0.32f;
                    byte a = (byte)(Tex.SmoothStep(3, 20, el * Mathf.Rad2Deg) * 255);
                    b.Add(dir * Radius, uv, new Color32(255, 255, 255, a));
                }
            }
            for (int i = 0; i < rings; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j, c = a + seg + 1;
                b.Quad(a, a + 1, c + 1, c, Vector3.zero - b.Verts[a]);
            }
            var m = b.Build("CloudDome", false);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * Radius * 2.2f);
            return m;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position;
        }
    }
}
