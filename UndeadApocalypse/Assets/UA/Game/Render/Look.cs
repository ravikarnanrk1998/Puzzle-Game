using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UA.Game
{
    /// <summary>Lighting recipe for a stage: key light, ambient, fog, sky, practical light palette, grading.</summary>
    public sealed class Atmosphere
    {
        public Color KeyColor = Color.white, AmbientSky, AmbientEquator, AmbientGround, FogColor;
        public float KeyIntensity, KeyPitch = 50, KeyYaw = 30, FogDensity = 0.03f, Exposure, Saturation, Contrast = 16, Temperature;
        public bool KeyShadows = true;
        public int SkyKind;
        public Color[] Practicals = { new Color(1f, 0.62f, 0.28f) };
        // Cinematic grade: cool blue shadows, warm golden highlights.
        // (Split toning strength is the colour's distance from mid grey, so these stay close to 0.5.)
        public Color SplitShadows = new Color(0.45f, 0.5f, 0.6f), SplitHighlights = new Color(0.62f, 0.55f, 0.45f);
        public float Darkness;   // 0 bright .. 1 pitch black (drives the flashlight)
        public float LightChance = 0.5f, Flicker = 0.15f;
    }

    /// <summary>Scene lighting, fog, sky, reflections, post-processing and weather flashes.</summary>
    public static class Look
    {
        static Light _key;
        static Volume _volume, _hurtVolume;
        static VolumeProfile _profile, _hurtProfile;
        static Material _sky;
        static readonly List<ReflectionProbe> Probes = new List<ReflectionProbe>();
        static float _flash, _baseKey;
        static Atmosphere _atm;

        public static Light Key => _key;
        public static Atmosphere Current => _atm;

        /// <summary>Builds the atmosphere for a biome / mood / weather / event combination.</summary>
        public static Atmosphere For(StagePlan plan)
        {
            var a = new Atmosphere();
            bool indoor = plan.Indoor;
            switch (plan.Mood)
            {
                case LightMood.Night:
                    a.KeyColor = new Color(0.55f, 0.65f, 0.95f); a.KeyIntensity = 0.32f; a.KeyPitch = 38;
                    a.AmbientSky = new Color(0.07f, 0.09f, 0.14f); a.AmbientEquator = new Color(0.05f, 0.055f, 0.07f); a.AmbientGround = new Color(0.02f, 0.02f, 0.025f);
                    a.FogColor = new Color(0.035f, 0.045f, 0.065f); a.FogDensity = 0.032f; a.SkyKind = 0; a.Darkness = 0.75f;
                    a.Practicals = new[] { new Color(1f, 0.6f, 0.25f), new Color(1f, 0.55f, 0.2f), new Color(0.55f, 0.75f, 1f) };
                    break;
                case LightMood.Dusk:
                    a.KeyColor = new Color(1f, 0.55f, 0.3f); a.KeyIntensity = 0.95f; a.KeyPitch = 9; a.KeyYaw = 250;
                    a.AmbientSky = new Color(0.22f, 0.2f, 0.26f); a.AmbientEquator = new Color(0.2f, 0.14f, 0.12f); a.AmbientGround = new Color(0.06f, 0.05f, 0.05f);
                    a.FogColor = new Color(0.36f, 0.24f, 0.2f); a.FogDensity = 0.024f; a.SkyKind = 1; a.Darkness = 0.25f;
                    a.Practicals = new[] { new Color(1f, 0.62f, 0.3f) };
                    break;
                case LightMood.Overcast:
                    a.KeyColor = new Color(0.8f, 0.84f, 0.9f); a.KeyIntensity = 0.75f; a.KeyPitch = 60;
                    a.AmbientSky = new Color(0.3f, 0.32f, 0.35f); a.AmbientEquator = new Color(0.22f, 0.23f, 0.24f); a.AmbientGround = new Color(0.08f, 0.08f, 0.08f);
                    a.FogColor = new Color(0.4f, 0.42f, 0.44f); a.FogDensity = 0.028f; a.SkyKind = 2; a.Darkness = 0.1f; a.LightChance = 0.25f;
                    a.Practicals = new[] { new Color(1f, 0.85f, 0.6f) };
                    break;
                case LightMood.Fluorescent:
                    a.KeyColor = new Color(0.7f, 0.8f, 1f); a.KeyIntensity = 0.05f;
                    a.AmbientSky = new Color(0.06f, 0.075f, 0.085f); a.AmbientEquator = new Color(0.05f, 0.06f, 0.065f); a.AmbientGround = new Color(0.025f, 0.03f, 0.03f);
                    a.FogColor = new Color(0.05f, 0.07f, 0.08f); a.FogDensity = 0.04f; a.Darkness = 0.45f; a.LightChance = 0.6f; a.Flicker = 0.25f;
                    a.Practicals = new[] { new Color(0.75f, 0.9f, 1f), new Color(0.65f, 0.85f, 0.95f), new Color(1f, 0.65f, 0.3f) };
                    break;
                case LightMood.Emergency:
                    a.KeyColor = new Color(1f, 0.3f, 0.2f); a.KeyIntensity = 0.04f;
                    a.AmbientSky = new Color(0.07f, 0.03f, 0.03f); a.AmbientEquator = new Color(0.05f, 0.025f, 0.025f); a.AmbientGround = new Color(0.02f, 0.01f, 0.01f);
                    a.FogColor = new Color(0.07f, 0.02f, 0.02f); a.FogDensity = 0.045f; a.Darkness = 0.7f; a.LightChance = 0.55f; a.Flicker = 0.3f;
                    a.Practicals = new[] { new Color(1f, 0.15f, 0.08f), new Color(1f, 0.2f, 0.1f), new Color(1f, 0.55f, 0.2f) };
                    a.SplitShadows = new Color(0.4f, 0.2f, 0.2f);
                    break;
                case LightMood.Toxic:
                    a.KeyColor = new Color(0.5f, 1f, 0.4f); a.KeyIntensity = 0.06f;
                    a.AmbientSky = new Color(0.04f, 0.08f, 0.04f); a.AmbientEquator = new Color(0.035f, 0.06f, 0.035f); a.AmbientGround = new Color(0.015f, 0.025f, 0.015f);
                    a.FogColor = new Color(0.05f, 0.09f, 0.04f); a.FogDensity = 0.05f; a.SkyKind = 4; a.Darkness = 0.6f; a.LightChance = 0.55f;
                    a.Practicals = new[] { new Color(0.5f, 1f, 0.35f), new Color(0.6f, 1f, 0.4f), new Color(1f, 0.5f, 0.15f) };
                    a.SplitShadows = new Color(0.25f, 0.5f, 0.3f);
                    break;
                default: // Sodium
                    a.KeyColor = new Color(0.6f, 0.7f, 1f); a.KeyIntensity = indoor ? 0.04f : 0.25f; a.KeyPitch = 40;
                    a.AmbientSky = new Color(0.06f, 0.065f, 0.09f); a.AmbientEquator = new Color(0.06f, 0.05f, 0.04f); a.AmbientGround = new Color(0.025f, 0.02f, 0.015f);
                    a.FogColor = new Color(0.07f, 0.06f, 0.06f); a.FogDensity = 0.038f; a.SkyKind = 0; a.Darkness = 0.55f; a.LightChance = 0.6f;
                    a.Practicals = new[] { new Color(1f, 0.58f, 0.22f), new Color(1f, 0.62f, 0.25f), new Color(0.45f, 0.7f, 1f) };
                    break;
            }
            if (indoor)
            {
                a.KeyShadows = false;
                a.SkyKind = 0;
            }
            // Global lift: the scenes must stay bright and readable on phone screens. Indoors the bounce light is
            // pushed toward a cool blue so the warm fixtures read against it.
            a.AmbientSky *= 3.1f;
            a.AmbientEquator *= 3.0f;
            a.AmbientGround *= 2.6f;
            if (indoor)
            {
                a.AmbientSky = Color.Lerp(a.AmbientSky, new Color(0.22f, 0.26f, 0.33f), 0.15f);
                a.FogDensity *= 0.6f;
            }
            if (Quality.Current == Gfx.Low)
            {
                // Low has fewer lights, no HDR bloom and no SSAO: lift the bounce light to keep scenes readable.
                a.AmbientSky *= 1.6f;
                a.AmbientEquator *= 1.6f;
                a.AmbientGround *= 1.45f;
            }
            a.LightChance = Mathf.Min(0.95f, a.LightChance + 0.25f);
            a.KeyIntensity *= 1.3f;
            a.Exposure += 0.22f;
            switch (plan.Weather)
            {
                case Weather.Fog: a.FogDensity *= 1.7f; break;
                case Weather.Rain: a.FogDensity *= 1.2f; a.Saturation -= 8; break;
                case Weather.Storm: a.FogDensity *= 1.3f; a.KeyIntensity *= 0.6f; a.Saturation -= 12; break;
                case Weather.Snow: a.FogColor = new Color(0.45f, 0.5f, 0.58f) * (a.Darkness > 0.5f ? 0.4f : 1f); a.FogDensity *= 1.6f; a.SkyKind = 5; a.Temperature = -18; a.Saturation -= 15; break;
                case Weather.Dust: a.FogColor = new Color(0.5f, 0.38f, 0.24f) * (a.Darkness > 0.5f ? 0.35f : 1f); a.FogDensity *= 1.8f; a.Temperature = 18; break;
                case Weather.Ash: a.FogColor = new Color(0.22f, 0.18f, 0.16f); a.FogDensity *= 1.4f; a.Saturation -= 10; break;
            }
            switch (plan.Event)
            {
                case StageEvent.BloodMoon:
                    a.SkyKind = 3; a.KeyColor = new Color(1f, 0.25f, 0.18f); a.FogColor = new Color(0.12f, 0.02f, 0.02f); a.SplitHighlights = new Color(1f, 0.3f, 0.2f); a.Saturation += 6;
                    break;
                case StageEvent.Blackout:
                    a.LightChance *= 0.25f; a.Darkness = 1; a.AmbientSky *= 0.4f; a.AmbientEquator *= 0.4f; a.AmbientGround *= 0.4f;
                    a.Practicals = new[] { new Color(1f, 0.12f, 0.06f) };
                    break;
                case StageEvent.ToxicFog:
                    a.FogColor = Color.Lerp(a.FogColor, new Color(0.12f, 0.2f, 0.06f), 0.7f); a.FogDensity *= 1.4f;
                    break;
            }
            bool pitchBlack = plan.Event == StageEvent.Blackout || (plan.Mutators & Mutator.Darkness) != 0;
            if ((plan.Mutators & Mutator.Darkness) != 0)
            {
                a.LightChance *= 0.2f; a.Darkness = 1; a.KeyIntensity *= 0.2f;
                a.AmbientSky *= 0.3f; a.AmbientEquator *= 0.3f; a.AmbientGround *= 0.3f;
            }
            // Visibility floor: no stage may drop below a readable bounce level (deliberate blackouts keep a lower one).
            float floorSky = pitchBlack ? 0.13f : Quality.Current == Gfx.Low ? 0.3f : 0.26f;
            a.AmbientSky = AtLeast(a.AmbientSky, floorSky);
            a.AmbientEquator = AtLeast(a.AmbientEquator, floorSky * 0.85f);
            a.AmbientGround = AtLeast(a.AmbientGround, floorSky * 0.45f);
            return a;
        }

        /// <summary>Scales a colour up (keeping its hue) until its luminance reaches <paramref name="lum"/>.</summary>
        static Color AtLeast(Color c, float lum)
        {
            float g = c.grayscale;
            if (g >= lum) return c;
            if (g < 0.004f) return new Color(lum, lum, lum * 1.1f, c.a);
            float k = lum / g;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        public static void Apply(Atmosphere a)
        {
            _atm = a;
            EnsureKey();
            _key.color = a.KeyColor;
            _key.intensity = _baseKey = a.KeyIntensity;
            _key.transform.rotation = Quaternion.Euler(a.KeyPitch, a.KeyYaw, 0);
            _key.shadows = a.KeyShadows && a.KeyIntensity > 0.1f ? (Quality.Current == Gfx.Ultra ? LightShadows.Soft : Quality.Current == Gfx.Medium ? LightShadows.Hard : LightShadows.None) : LightShadows.None;
            _key.shadowStrength = 0.85f;
            _key.shadowBias = 0.05f;
            _key.shadowNormalBias = 0.4f;
            _key.enabled = a.KeyIntensity > 0.01f;

            if (_sky == null) _sky = new Material(Mats.Template("Sky"));
            _sky.SetTexture("_MainTex", Tex.Sky(a.SkyKind));
            _sky.SetFloat("_Exposure", a.SkyKind == 2 || a.SkyKind == 5 ? 0.6f : 1f);
            _sky.SetFloat("_Mapping", 1);
            _sky.SetFloat("_ImageType", 0);
            RenderSettings.skybox = _sky;
            RenderSettings.sun = _key;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = a.AmbientSky;
            RenderSettings.ambientEquatorColor = a.AmbientEquator;
            RenderSettings.ambientGroundColor = a.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = a.FogColor;
            RenderSettings.fogDensity = a.FogDensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = a.SkyKind == 0 && a.KeyShadows == false ? 0.9f : 0.7f;
            EnsureVolumes();
            TunePost();
        }

        static void EnsureKey()
        {
            if (_key != null) return;
            var go = new GameObject("KeyLight");
            Object.DontDestroyOnLoad(go);
            _key = go.AddComponent<Light>();
            _key.type = LightType.Directional;
        }

        static void EnsureVolumes()
        {
            if (_volume != null) return;
            var go = new GameObject("PostFX");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 0;
            var src = Resources.Load<VolumeProfile>("PostFX");
            _profile = src != null ? Object.Instantiate(src) : ScriptableObject.CreateInstance<VolumeProfile>();
            Add<Tonemapping>(_profile);
            Add<Bloom>(_profile);
            Add<ColorAdjustments>(_profile);
            Add<Vignette>(_profile);
            Add<FilmGrain>(_profile);
            Add<SplitToning>(_profile);
            Add<WhiteBalance>(_profile);
            Add<ChromaticAberration>(_profile);
            _volume.sharedProfile = _profile;

            var hgo = new GameObject("HurtFX");
            Object.DontDestroyOnLoad(hgo);
            _hurtVolume = hgo.AddComponent<Volume>();
            _hurtVolume.isGlobal = true;
            _hurtVolume.priority = 10;
            _hurtVolume.weight = 0;
            _hurtProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var v = _hurtProfile.Add<Vignette>(true);
            v.color.Override(new Color(0.55f, 0, 0));
            v.intensity.Override(0.55f);
            v.smoothness.Override(0.9f);
            var ca = _hurtProfile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-55);
            _hurtVolume.sharedProfile = _hurtProfile;
        }

        static void Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.Has<T>()) p.Add<T>(true);
        }

        static void TunePost()
        {
            var g = Quality.Current;
            var a = _atm ?? new Atmosphere();
            if (_profile.TryGet(out Tonemapping tm)) { tm.active = true; tm.mode.Override(TonemappingMode.ACES); }
            if (_profile.TryGet(out Bloom b))
            {
                b.active = true;
                b.threshold.Override(g == Gfx.Low ? 1.05f : 0.95f);
                b.intensity.Override(g == Gfx.Low ? 0.45f : 0.75f);
                b.scatter.Override(0.7f);
                b.clamp.Override(24f);
                b.highQualityFiltering.Override(g == Gfx.Ultra);
                b.downscale.Override(g == Gfx.Ultra ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter);
            }
            if (_profile.TryGet(out ColorAdjustments ca))
            {
                ca.active = true;
                ca.postExposure.Override(0.8f + a.Exposure);
                ca.contrast.Override(a.Contrast);
                ca.saturation.Override(-6 + a.Saturation);
            }
            if (_profile.TryGet(out Vignette v)) { v.active = true; v.intensity.Override(0.22f); v.smoothness.Override(0.5f); v.color.Override(Color.black); }
            // Clean image: a whisper of grain on Ultra only.
            if (_profile.TryGet(out FilmGrain fg)) { fg.active = g == Gfx.Ultra; fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.08f); fg.response.Override(0.8f); }
            if (_profile.TryGet(out SplitToning st)) { st.active = g != Gfx.Low; st.shadows.Override(a.SplitShadows); st.highlights.Override(a.SplitHighlights); st.balance.Override(-12); }
            if (_profile.TryGet(out WhiteBalance wb)) { wb.active = g != Gfx.Low; wb.temperature.Override(a.Temperature); }
            if (_profile.TryGet(out ChromaticAberration chr)) { chr.active = g == Gfx.Ultra; chr.intensity.Override(0.05f); }
        }

        public static void RefreshQuality()
        {
            if (_profile != null) TunePost();
            if (_atm != null && _key != null) Apply(_atm);
        }

        /// <summary>Red vignette / desaturation when hurt (0..1).</summary>
        public static void SetHurt(float k)
        {
            if (_hurtVolume != null) _hurtVolume.weight = Mathf.Clamp01(k);
        }

        public static void Lightning(float strength)
        {
            _flash = Mathf.Max(_flash, strength);
        }

        public static void Tick(float dt)
        {
            if (_key == null) return;
            if (_flash > 0)
            {
                _flash = Mathf.Max(0, _flash - dt * 3.5f);
                float f = _flash * (0.7f + 0.3f * Mathf.Sin(Time.time * 70));
                _key.intensity = _baseKey + f * 2.2f;
                _key.enabled = true;
                RenderSettings.ambientSkyColor = (_atm?.AmbientSky ?? Color.black) + new Color(0.35f, 0.38f, 0.45f) * f;
            }
            else if (_atm != null && RenderSettings.ambientSkyColor != _atm.AmbientSky)
            {
                _key.intensity = _baseKey;
                _key.enabled = _baseKey > 0.01f;
                RenderSettings.ambientSkyColor = _atm.AmbientSky;
            }
        }

        // --------------------------------------------------------------- reflections

        public static void ClearProbes()
        {
            foreach (var p in Probes) if (p != null) Object.Destroy(p.gameObject);
            Probes.Clear();
        }

        /// <summary>Places reflection probes over the level and renders them once (spread over frames).</summary>
        public static IEnumerable<int> CaptureReflections(Transform parent, Bounds level, float ceiling)
        {
            ClearProbes();
            if (!Quality.Reflections) yield break;
            // Box-projected probes on a grid of roughly 12-16 m cells so wet floors and glazed tiles reflect
            // the fixtures near them (one level-wide probe smeared everything).
            var size = level.size;
            float cell = Quality.Current == Gfx.Ultra ? 12f : 16f;
            int max = Quality.Current == Gfx.Ultra ? 5 : Quality.Current == Gfx.Medium ? 3 : 1;
            int nx = Mathf.Clamp(Mathf.RoundToInt(size.x / cell), 1, max);
            int nz = Mathf.Clamp(Mathf.RoundToInt(size.z / cell), 1, max);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var go = new GameObject("Probe");
                    go.transform.SetParent(parent, false);
                    var p = go.AddComponent<ReflectionProbe>();
                    p.mode = ReflectionProbeMode.Realtime;
                    p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    p.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    p.resolution = Quality.Current == Gfx.Ultra ? 256 : Quality.Current == Gfx.Medium ? 128 : 64;
                    p.boxProjection = true;
                    p.hdr = true;
                    p.clearFlags = ReflectionProbeClearFlags.SolidColor;
                    p.backgroundColor = RenderSettings.fogColor;
                    p.farClipPlane = 80;
                    p.cullingMask = 1 << Layers.World | 1 << Layers.Default;
                    var cs = new Vector3(size.x / nx, Mathf.Max(4, ceiling), size.z / nz);
                    var c = level.min + new Vector3(cs.x * (i + 0.5f), 0, cs.z * (j + 0.5f));
                    go.transform.position = new Vector3(c.x, 1.6f, c.z);
                    p.size = cs + new Vector3(2, 0, 2);
                    p.center = new Vector3(0, cs.y * 0.5f - 1.6f, 0);
                    p.blendDistance = 2;
                    Probes.Add(p);
                    p.RenderProbe();
                    yield return 1;
                }
        }
    }
}
