using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KPL.Game
{
    /// <summary>Lighting, sky, fog, reflections and post-processing for day and floodlit night matches.</summary>
    public static class SceneLook
    {
        static Light _sun;
        static Volume _volume;
        static VolumeProfile _profile;
        static Material _sky;
        static ReflectionProbe _probe;
        static bool _night;
        static Camera _cam;

        public static bool IsNight => _night;
        public static Light Sun => _sun;

        public static Camera MainCamera()
        {
            if (_cam != null) return _cam;
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Object.DontDestroyOnLoad(go);
                _cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _cam.nearClipPlane = 0.2f;
            _cam.farClipPlane = 1400;
            _cam.fieldOfView = 40;
            _cam.clearFlags = CameraClearFlags.Skybox;
            var data = _cam.GetComponent<UniversalAdditionalCameraData>() ?? _cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            ApplyCameraQuality();
            return _cam;
        }

        static void ApplyCameraQuality()
        {
            if (_cam == null) return;
            var data = _cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) return;
            var g = Quality.Current;
            data.antialiasing = g >= Gfx.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : g == Gfx.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            data.antialiasingQuality = g == Gfx.Ultra ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            data.renderShadows = true;
            data.renderPostProcessing = true;
            _cam.allowHDR = g >= Gfx.Medium;
        }

        public static void RefreshQuality()
        {
            ApplyCameraQuality();
            if (_sun != null) ApplyLight(_night);
            if (_profile != null) TunePost(_night);
        }

        /// <summary>Sets up sun / floodlight key light, sky, ambient, fog and post FX.</summary>
        public static void Apply(bool night, Transform parent)
        {
            _night = night;
            MainCamera();
            if (_sun == null)
            {
                var go = new GameObject("KeyLight");
                Object.DontDestroyOnLoad(go);
                _sun = go.AddComponent<Light>();
                _sun.type = LightType.Directional;
            }
            ApplyLight(night);

            if (_sky == null) _sky = new Material(Mats.Template("Sky"));
            float sunAz = 330, sunEl = 58;
            _sky.SetTexture("_MainTex", Tex.Sky(!night, sunAz, sunEl));
            _sky.SetFloat("_Exposure", night ? 1.0f : 1.05f);
            _sky.SetFloat("_Rotation", 0);
            RenderSettings.skybox = _sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            if (night)
            {
                RenderSettings.ambientSkyColor = new Color(0.32f, 0.35f, 0.45f);
                RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.32f);
                RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.12f);
            }
            else
            {
                RenderSettings.ambientSkyColor = new Color(0.64f, 0.76f, 0.95f);
                RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.58f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.38f, 0.24f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = night ? new Color(0.05f, 0.06f, 0.09f) : new Color(0.66f, 0.79f, 0.95f);
            RenderSettings.fogStartDistance = night ? 220 : 320;
            RenderSettings.fogEndDistance = night ? 900 : 1600;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = night ? 0.6f : 0.9f;
            DynamicGI.UpdateEnvironment();

            EnsureVolume();
            TunePost(night);
        }

        static void ApplyLight(bool night)
        {
            var g = Quality.Current;
            if (night)
            {
                // Floodlight key: high, cool-white, softer shadows.
                _sun.transform.rotation = Quaternion.Euler(64, 150, 0);
                _sun.color = new Color(0.96f, 0.97f, 1f);
                _sun.intensity = 1.15f;
                _sun.shadowStrength = 0.55f;
            }
            else
            {
                // Bright afternoon sun from over the bowler's shoulder, so shadows fall towards the camera's left.
                _sun.transform.rotation = Quaternion.Euler(58, 150, 0);
                _sun.color = new Color(1f, 0.95f, 0.87f);
                _sun.intensity = 1.6f;
                _sun.shadowStrength = 0.78f;
            }
            _sun.shadows = g >= Gfx.High ? LightShadows.Soft : LightShadows.Hard;
            _sun.shadowBias = 0.04f;
            _sun.shadowNormalBias = 0.35f;
        }

        static void EnsureVolume()
        {
            if (_volume != null) return;
            var go = new GameObject("PostFX");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            var src = Resources.Load<VolumeProfile>("PostFX");
            _profile = src != null ? Object.Instantiate(src) : ScriptableObject.CreateInstance<VolumeProfile>();
            if (!_profile.Has<Tonemapping>()) _profile.Add<Tonemapping>(true);
            if (!_profile.Has<Bloom>()) _profile.Add<Bloom>(true);
            if (!_profile.Has<ColorAdjustments>()) _profile.Add<ColorAdjustments>(true);
            if (!_profile.Has<Vignette>()) _profile.Add<Vignette>(true);
            if (!_profile.Has<WhiteBalance>()) _profile.Add<WhiteBalance>(true);
            _volume.sharedProfile = _profile;
        }

        static void TunePost(bool night)
        {
            var g = Quality.Current;
            if (_profile.TryGet(out Tonemapping tm))
            {
                tm.active = true;
                tm.mode.Override(TonemappingMode.ACES);
            }
            if (_profile.TryGet(out Bloom bloom))
            {
                bloom.active = Quality.Bloom;
                bloom.threshold.Override(night ? 0.85f : 1.05f);
                bloom.intensity.Override(night ? 0.9f : 0.35f);
                bloom.scatter.Override(0.65f);
                bloom.highQualityFiltering.Override(g >= Gfx.Ultra);
                bloom.downscale.Override(g >= Gfx.High ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter);
            }
            if (_profile.TryGet(out ColorAdjustments ca))
            {
                ca.active = true;
                ca.postExposure.Override(night ? 0.35f : 0.3f);
                ca.contrast.Override(night ? 14 : 16);
                ca.saturation.Override(night ? 8 : 24);
            }
            if (_profile.TryGet(out Vignette v))
            {
                v.active = Quality.Vignette;
                v.intensity.Override(0.22f);
                v.smoothness.Override(0.5f);
            }
            if (_profile.TryGet(out WhiteBalance wb))
            {
                wb.active = g >= Gfx.Medium;
                wb.temperature.Override(night ? -6 : 4);
            }
        }

        /// <summary>One-off reflection capture of the stadium (HIGH / ULTRA).</summary>
        public static void CaptureReflections(Transform parent)
        {
            if (Quality.Current < Gfx.High)
            {
                if (_probe != null) Object.Destroy(_probe.gameObject);
                return;
            }
            if (_probe == null)
            {
                var go = new GameObject("ReflectionProbe");
                go.transform.SetParent(parent, false);
                _probe = go.AddComponent<ReflectionProbe>();
                _probe.mode = ReflectionProbeMode.Realtime;
                _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                _probe.size = new Vector3(400, 200, 400);
                _probe.resolution = Quality.Current == Gfx.Ultra ? 256 : 128;
                _probe.transform.localPosition = new Vector3(0, 12, 0);
                _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                _probe.farClipPlane = 600;
            }
            _probe.RenderProbe();
        }
    }
}
