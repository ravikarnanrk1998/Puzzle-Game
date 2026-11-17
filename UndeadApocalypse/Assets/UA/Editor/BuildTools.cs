using System;
using System.IO;
using System.Linq;
using UA.Game;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UA.EditorTools
{
    /// <summary>Project setup (URP tiers, quality, materials, post profile, player settings, icon, scene) and builds.</summary>
    public static class BuildTools
    {
        public const string Package = "com.rkgame.undeadapocalypse";
        public const string Product = "Undead Apocalypse: Infinite War";
        public const string Version = "1.0.9";
        public const int VersionCode = 10;
        const string Root = "Assets/UA";
        const string ScenePath = Root + "/Scenes/Boot.unity";
        static readonly string[] TierNames = { "Low", "Medium", "Ultra" };

        [MenuItem("UA/1. Setup Project")]
        public static void Setup()
        {
            Directory.CreateDirectory(Root + "/Resources/Materials");
            Directory.CreateDirectory(Root + "/Settings");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Icon");
            AssetDatabase.Refresh();
            var assets = CreatePipelineAssets();
            ConfigureQuality(assets);
            CreateMaterials();
            CreatePostProfile();
            ConfigurePlayer();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MakeIcon();
            AssetDatabase.SaveAssets();
            Debug.Log("[UA] Setup complete.");
        }

        // --------------------------------------------------------- URP tiers

        static UniversalRenderPipelineAsset[] CreatePipelineAssets()
        {
            var list = new UniversalRenderPipelineAsset[3];
            for (int i = 0; i < 3; i++)
            {
                string rPath = Root + "/Settings/UA_Renderer_" + TierNames[i] + ".asset";
                string aPath = Root + "/Settings/UA_URP_" + TierNames[i] + ".asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rPath);
                }
                var so = new SerializedObject(renderer);
                var pp = so.FindProperty("postProcessData");
                if (pp != null && pp.objectReferenceValue == null)
                {
                    var data = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    if (data != null) pp.objectReferenceValue = data;
                }
                // Forward+ (clustered lights): no per-object light limit, so the flashlight and every
                // practical light always reach characters and large merged level meshes.
                SetProp(so, "m_RenderingMode", 2);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (i >= 1) AddSsao(renderer, i == 2);

                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(aPath);
                if (asset == null)
                {
                    asset = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(asset, aPath);
                }
                var a = new SerializedObject(asset);
                var rl = a.FindProperty("m_RendererDataList");
                if (rl != null)
                {
                    rl.arraySize = 1;
                    rl.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                }
                float[] scale = { 0.72f, 0.85f, 1f };
                int[] msaa = { 1, 1, 2 };
                int[] shadowRes = { 512, 1024, 2048 };
                float[] shadowDist = { 25, 40, 60 };
                int[] lightsPerObject = { 3, 5, 8 };
                SetProp(a, "m_RenderScale", scale[i]);
                SetProp(a, "m_MSAA", msaa[i]);
                // HDR on every tier: the LDR grading path rendered Low far darker than Medium on devices, and the
                // R11G11B10 HDR target costs the same bandwidth as RGBA8.
                SetProp(a, "m_SupportsHDR", 1);
                SetProp(a, "m_HDRColorBufferPrecision", 0);
                SetProp(a, "m_MainLightRenderingMode", 1);
                SetProp(a, "m_MainLightShadowsSupported", 1);
                SetProp(a, "m_MainLightShadowmapResolution", shadowRes[i]);
                SetProp(a, "m_ShadowDistance", shadowDist[i]);
                SetProp(a, "m_ShadowCascadeCount", i == 2 ? 2 : 1);
                SetProp(a, "m_Cascade2Split", 0.3f);
                SetProp(a, "m_SoftShadowsSupported", i >= 1 ? 1 : 0);
                SetProp(a, "m_SoftShadowQuality", i == 2 ? 2 : 1);
                SetProp(a, "m_AdditionalLightsRenderingMode", 1);
                SetProp(a, "m_AdditionalLightsPerObjectLimit", lightsPerObject[i]);
                SetProp(a, "m_AdditionalLightShadowsSupported", i == 2 ? 1 : 0);
                SetProp(a, "m_AdditionalLightsShadowmapResolution", 1024);
                SetProp(a, "m_ReflectionProbeBlending", i >= 1 ? 1 : 0);
                SetProp(a, "m_ReflectionProbeBoxProjection", i >= 1 ? 1 : 0);
                SetProp(a, "m_RequireDepthTexture", 0);
                SetProp(a, "m_RequireOpaqueTexture", 0);
                SetProp(a, "m_UseSRPBatcher", 1);
                SetProp(a, "m_SupportsDynamicBatching", 0);
                SetProp(a, "m_ColorGradingMode", 1);
                SetProp(a, "m_ColorGradingLutSize", i >= 1 ? 32 : 16);
                SetProp(a, "m_ShadowDepthBias", 1.0f);
                SetProp(a, "m_ShadowNormalBias", 1.0f);
                SetProp(a, "m_SupportsLightCookies", 0);
                // Rendering layers keep world lights (flashlight, muzzle) off the first-person weapon.
                SetProp(a, "m_SupportsLightLayers", 1);
                a.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                list[i] = asset;
            }
            GraphicsSettings.defaultRenderPipeline = list[1];
            return list;
        }

        /// <summary>Screen-space ambient occlusion: full quality on Ultra, downsampled with low samples/blur on Medium.</summary>
        static void AddSsao(UniversalRendererData renderer, bool ultra)
        {
            try
            {
                var feature = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
                if (feature == null)
                {
                    var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                    if (type == null) return;
                    feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                    feature.name = "SSAO";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    renderer.rendererFeatures.Add(feature);
                    var so = new SerializedObject(renderer);
                    var map = so.FindProperty("m_RendererFeatureMap");
                    if (map != null)
                    {
                        map.arraySize++;
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Enum order in URP: AOSampleOption / BlurQuality = High, Medium, Low.
                var fso = new SerializedObject(feature);
                SetProp(fso, "m_Settings.Intensity", ultra ? 1.5f : 1.2f);
                SetProp(fso, "m_Settings.Radius", ultra ? 0.45f : 0.35f);
                SetProp(fso, "m_Settings.DirectLightingStrength", 0.25f);
                SetProp(fso, "m_Settings.Downsample", 1);
                SetProp(fso, "m_Settings.Samples", ultra ? 1 : 2);
                SetProp(fso, "m_Settings.BlurQuality", ultra ? 0 : 2);
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UA] SSAO not added: " + e.Message);
            }
        }

        static void SetProp(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: p.floatValue = v; break;
                case SerializedPropertyType.Integer: p.intValue = (int)v; break;
                case SerializedPropertyType.Boolean: p.boolValue = v > 0.5f; break;
                case SerializedPropertyType.Enum:
                    if (name == "m_MSAA" || name.Contains("Resolution")) p.intValue = (int)v;
                    else p.enumValueIndex = Mathf.Clamp((int)v, 0, p.enumNames.Length - 1);
                    break;
            }
        }

        static void ConfigureQuality(UniversalRenderPipelineAsset[] assets)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qs == null) return;
            var so = new SerializedObject(qs);
            var arr = so.FindProperty("m_QualitySettings");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = TierNames[i];
                Rel(e, "pixelLightCount", 4);
                Rel(e, "shadows", 2);
                Rel(e, "shadowResolution", i);
                Rel(e, "shadowDistance", new[] { 25f, 40, 60 }[i]);
                Rel(e, "antiAliasing", 0);
                Rel(e, "softParticles", 0);
                Rel(e, "realtimeReflectionProbes", i >= 1 ? 1 : 0);
                Rel(e, "vSyncCount", 0);
                Rel(e, "lodBias", new[] { 0.7f, 1f, 1.5f }[i]);
                Rel(e, "skinWeights", i >= 1 ? 4 : 2);
                Rel(e, "globalTextureMipmapLimit", 0);
                Rel(e, "anisotropicTextures", i == 2 ? 1 : 0);
                Rel(e, "asyncUploadTimeSlice", 2);
                Rel(e, "asyncUploadBufferSize", 16);
                Rel(e, "particleRaycastBudget", i == 0 ? 16 : 64);
                var rp = e.FindPropertyRelative("customRenderPipeline");
                if (rp != null) rp.objectReferenceValue = assets[i];
            }
            var def = so.FindProperty("m_PerPlatformDefaultQuality");
            if (def != null)
            {
                for (int i = 0; i < def.arraySize; i++)
                {
                    var el = def.GetArrayElementAtIndex(i);
                    var second = el.FindPropertyRelative("second");
                    if (second != null) second.intValue = el.FindPropertyRelative("first").stringValue == "Standalone" ? 2 : 1;
                }
            }
            SetProp(so, "m_CurrentQuality", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Rel(SerializedProperty e, string name, float v)
        {
            var p = e.FindPropertyRelative(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
            else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v > 0.5f;
            else if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = Mathf.Clamp((int)v, 0, p.enumNames.Length - 1);
            else p.intValue = (int)v;
        }

        // --------------------------------------------------------- materials

        static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var sky = Shader.Find("Skybox/Panoramic");
            var flat = FlatNormalAsset();
            var gray = Texture2D.grayTexture;
            Mat("Lit", lit, m => { });
            Mat("LitNormal", lit, m => { m.SetTexture("_BumpMap", flat); m.EnableKeyword("_NORMALMAP"); });
            Mat("LitWet", lit, m =>
            {
                m.SetTexture("_BumpMap", flat);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_SmoothnessTextureChannel", 1);
                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            });
            Mat("LitMetal", lit, m =>
            {
                m.SetTexture("_BumpMap", flat);
                m.SetTexture("_MetallicGlossMap", gray);
                m.EnableKeyword("_NORMALMAP");
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            });
            Mat("LitEmissive", lit, m =>
            {
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            Mat("LitCutout", lit, m =>
            {
                m.SetFloat("_AlphaClip", 1);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Cull", 0);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            Mat("LitTransparent", lit, m => Mats.MakeTransparent(m, false));
            Mat("Unlit", unlit, m => { });
            Mat("UnlitTransparent", unlit, m => Mats.MakeTransparent(m, false));
            Mat("UnlitAdditive", unlit, m => Mats.MakeTransparent(m, true));
            Mat("ParticleAdd", particles, m => Mats.MakeTransparent(m, true));
            Mat("ParticleAlpha", particles, m => Mats.MakeTransparent(m, false));
            Mat("Sky", sky, m =>
            {
                m.SetFloat("_Mapping", 1);
                m.SetFloat("_ImageType", 0);
            });
        }

        static Texture2D FlatNormalAsset()
        {
            string path = Root + "/Resources/Materials/FlatNormal.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                var px = Enumerable.Repeat(new Color32(128, 128, 255, 255), 16).ToArray();
                t.SetPixels32(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Mat(string name, Shader shader, Action<Material> setup)
        {
            string path = Root + "/Resources/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
        }

        static void CreatePostProfile()
        {
            string path = Root + "/Resources/PostFX.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            void Add<T>() where T : VolumeComponent
            {
                if (p.Has<T>()) return;
                var c = p.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            Add<Tonemapping>();
            Add<Bloom>();
            Add<ColorAdjustments>();
            Add<Vignette>();
            Add<FilmGrain>();
            Add<SplitToning>();
            Add<WhiteBalance>();
            Add<ChromaticAberration>();
            if (p.TryGet(out Tonemapping tm)) tm.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(p);
        }

        // ------------------------------------------------------------ player

        /// <summary>The viewmodel lighting relies on rendering layer 1 ("Viewmodel") being defined; URP strips undefined bits.</summary>
        static void EnsureRenderingLayers()
        {
            Debug.Log("[UA] Rendering layers defined: " + string.Join(",", RenderingLayerMask.GetDefinedRenderingLayerNames()) + " mask=" + RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue());
            if (RenderingLayerMask.NameToRenderingLayer("Viewmodel") == 1) return;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var p = tm.FindProperty("m_RenderingLayers");
            if (p == null || !p.isArray) { Debug.LogError("[UA] TagManager has no m_RenderingLayers"); return; }
            Debug.Log("[UA] TagManager m_RenderingLayers size=" + p.arraySize + " type=" + (p.arraySize > 0 ? p.GetArrayElementAtIndex(0).propertyType.ToString() : "?"));
            while (p.arraySize < 2) p.InsertArrayElementAtIndex(p.arraySize);
            p.GetArrayElementAtIndex(0).stringValue = "Default";
            p.GetArrayElementAtIndex(1).stringValue = "Viewmodel";
            tm.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[UA] Rendering layers now: " + string.Join(",", RenderingLayerMask.GetDefinedRenderingLayerNames()) + " mask=" + RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue());
        }

        /// <summary>
        /// UI images loaded with Resources.Load&lt;Sprite&gt;: sprite type, transparency kept, clamped, mipmapped
        /// (thin strokes such as the menu signature stay smooth when drawn small).
        /// </summary>
        static void EnsureUiSprite(string path)
        {
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType == TextureImporterType.Sprite && imp.alphaIsTransparency && imp.mipmapEnabled && imp.wrapMode == TextureWrapMode.Clamp) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        static void ConfigurePlayer()
        {
            EnsureRenderingLayers();
            EnsureUiSprite(Root + "/Resources/UI/MenuSignature.png");
            PlayerSettings.companyName = "RK Games";
            PlayerSettings.productName = Product;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Package);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, Package);
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.gpuSkinning = true;
            // CPU/GPU frame times for the autopilot's per-stage performance lines (FrameTimingManager).
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                SetProp(so, "activeInputHandler", 1);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // -------------------------------------------------------------- icon

        /// <summary>
        /// Owner-supplied icon art (full-bleed square, no baked rounded corners or frame). The as-supplied file is kept
        /// next to it as app-icon-original.png. When present it replaces the rendered icon.
        /// </summary>
        const string UserIconArt = "ArtSource/app-icon-art.png";

        [MenuItem("UA/2. Make Icon")]
        public static void MakeIcon()
        {
            try
            {
                // Owner-supplied art first; else rendered from the imported zombie models; the procedural art is the last fallback.
                Texture2D tex, adaptiveTex;
                var user = LoadPng(UserIconArt);
                if (user != null)
                {
                    tex = user.width == 1024 && user.height == 1024 ? user : Resample(user, 1024);
                    // 0.60: the whole title stays inside a circular launcher mask (Pixel), with a thin blurred border
                    // on rounded-square masks.
                    adaptiveTex = AdaptiveIcon(tex, 1024, 0.6f);
                    Debug.Log("[UA] Icon: using " + UserIconArt);
                }
                else
                {
                    tex = IconArt3D.Icon(1024) ?? IconArt.Render(1024);
                    adaptiveTex = tex;
                }
                var icon = WriteIcon(Root + "/Icon/AppIcon.png", tex);
                var adaptiveIcon = WriteIcon(Root + "/Icon/AppIconAdaptive.png", adaptiveTex);
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
                foreach (var i in adaptive)
                {
                    i.SetTexture(adaptiveIcon, 0);
                    i.SetTexture(adaptiveIcon, 1);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
                Directory.CreateDirectory("PlayStore");
                File.Copy(Root + "/Icon/AppIcon.png", "PlayStore/icon-1024.png", true);
                var feature = IconArt3D.Feature(1024, 500) ?? IconArt.Feature(1024, 500);
                File.WriteAllBytes("PlayStore/feature-graphic-1024x500.png", feature.EncodeToPNG());
                File.WriteAllBytes("PlayStore/icon-512.png", Half(tex).EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UA] Icon: " + e);
            }
        }

        // -------------------------------------------------------- card art

        /// <summary>
        /// Bakes 3D-rendered card art into Resources/Thumbs: a transparent side view of every weapon (W_&lt;id&gt;) and a
        /// portrait of every boss (B_&lt;type&gt;). The UI falls back to the flat procedural icons when one is missing.
        /// </summary>
        [MenuItem("UA/2b. Make Card Art")]
        public static void MakeThumbs()
        {
            string dir = Root + "/Resources/Thumbs/";
            Directory.CreateDirectory(dir);
            var written = new System.Collections.Generic.List<string>();
            foreach (UA.Core.WeaponId id in Enum.GetValues(typeof(UA.Core.WeaponId)))
            {
                var t = IconArt3D.WeaponThumb(id, 512, 256);
                if (t == null) { Debug.LogWarning("[UA] Card art: no model for " + id); continue; }
                string path = dir + "W_" + id + ".png";
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                written.Add(path);
            }
            var bosses = new (UA.Core.BossType type, string key, Color glow)[]
            {
                (UA.Core.BossType.Butcher, "Bosses/Butcher", new Color(0.45f, 0.05f, 0.035f)),
                (UA.Core.BossType.BroodMother, "Bosses/BroodMother", new Color(0.16f, 0.3f, 0.04f)),
                (UA.Core.BossType.Juggernaut, "Bosses/Juggernaut", new Color(0.42f, 0.17f, 0.03f)),
                (UA.Core.BossType.Wraith, "Bosses/Wraith", new Color(0.05f, 0.2f, 0.3f)),
            };
            foreach (var (type, key, glow) in bosses)
            {
                var t = IconArt3D.BossPortrait(key, glow, 528, 320);
                if (t == null) { Debug.LogWarning("[UA] Card art: no prefab for " + key); continue; }
                string path = dir + "B_" + type + ".png";
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                written.Add(path);
            }
            AssetDatabase.Refresh();
            foreach (var path in written)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 512;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            Debug.Log("[UA] Card art: " + written.Count + " images in " + dir);
        }

        // -------------------------------------------------------- promo video

        /// <summary>
        /// Encodes the autopilot "trailer" capture (Builds/trailer: JPEG frames, game audio, the combat music track)
        /// into PlayStore/promo-video-1080p.mp4 (H.264 + AAC, 30 fps) with the music as one continuous bed.
        /// </summary>
        [MenuItem("UA/7. Make Promo Video")]
        public static void MakePromoVideo()
        {
            const string dir = "Builds/trailer";
            var info = JsonUtility.FromJson<TrailerInfo>(File.ReadAllText(Path.Combine(dir, "trailer.json")));
            var frames = Directory.GetFiles(Path.Combine(dir, "frames"), "*.jpg").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (frames.Length == 0 || info.width <= 0 || info.height <= 0) throw new Exception("No trailer frames in " + dir);
            int spf = info.rate / info.fps;
            var sfx = ReadFloats(Path.Combine(dir, "audio.raw"));
            string musicPath = Path.Combine(dir, "music.raw");
            var music = File.Exists(musicPath) ? ReadFloats(musicPath) : new float[0];
            var mix = MixTrailerAudio(sfx, music, info, frames.Length * spf);

            Directory.CreateDirectory("PlayStore");
            const string outPath = "PlayStore/promo-video-1080p.mp4";
            if (File.Exists(outPath)) File.Delete(outPath);   // previous output of this tool
            // H.264 High profile at 24 Mb/s, a keyframe every second. The "High" preset of the older attributes gave
            // about 8 Mb/s, which turned muzzle flashes and explosions into visible blocks.
            var h264 = new H264EncoderAttributes { gopSize = (uint)info.fps, numConsecutiveBFrames = 2, profile = VideoEncodingProfile.H264High };
            var video = new VideoTrackEncoderAttributes(h264)
            {
                frameRate = new MediaRational(info.fps),
                width = (uint)info.width,
                height = (uint)info.height,
                targetBitRate = 24000000,
            };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(info.rate), channelCount = 2, language = "en" };
            var jpg = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var tex = new Texture2D(info.width, info.height, TextureFormat.RGBA32, false);
            using (var enc = new MediaEncoder(outPath, video, audio))
            using (var buf = new NativeArray<float>(spf * 2, Allocator.Persistent))
            {
                for (int f = 0; f < frames.Length; f++)
                {
                    jpg.LoadImage(File.ReadAllBytes(frames[f]));
                    if (jpg.width != info.width || jpg.height != info.height) throw new Exception("Frame " + f + " is " + jpg.width + "x" + jpg.height);
                    // CPU copy only: run with -nographics (Tools\ua.ps1 promovideo). Uploading every frame to a GPU
                    // device in batch mode ran out of video memory after ~450 frames.
                    tex.SetPixels32(jpg.GetPixels32());
                    enc.AddFrame(tex);
                    NativeArray<float>.Copy(mix, f * spf * 2, buf, 0, spf * 2);
                    enc.AddSamples(buf);
                    if (f % 150 == 0) Debug.Log("[UA] Promo video: frame " + f + "/" + frames.Length);
                }
            }
            UnityEngine.Object.DestroyImmediate(jpg);
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[UA] Promo video: " + outPath + " " + info.width + "x" + info.height + " " + info.fps + " fps, " + (frames.Length / (float)info.fps).ToString("0.0") + " s, " + (new FileInfo(outPath).Length / (1024f * 1024f)).ToString("0.0") + " MB");
        }

        static float[] ReadFloats(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var f = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, f, 0, f.Length * 4);
            return f;
        }

        /// <summary>
        /// Game audio (already faded at the cuts) plus the music looped underneath at about 60 % of the game audio's
        /// loudness, faded in at the start and out over the end card; soft-clipped and peak-normalised to -2 dB.
        /// </summary>
        static float[] MixTrailerAudio(float[] sfx, float[] music, TrailerInfo info, int total)
        {
            var mix = new float[total * 2];
            double sq = 0;
            int n = 0;
            for (int i = 0; i < Math.Min(sfx.Length, mix.Length); i++)
                if (Math.Abs(sfx[i]) > 1e-3f) { sq += sfx[i] * (double)sfx[i]; n++; }
            float sfxRms = n > 0 ? (float)Math.Sqrt(sq / n) : 0.1f;
            int mch = Math.Max(1, info.musicChannels);
            int mFrames = music.Length / mch;
            double msq = 0;
            for (int i = 0; i < music.Length; i++) msq += music[i] * (double)music[i];
            float mRms = music.Length > 0 ? (float)Math.Sqrt(msq / music.Length) : 0;
            float mGain = mRms > 1e-4f ? Mathf.Clamp(0.6f * sfxRms / mRms, 0.15f, 3f) : 0;
            float len = total / (float)info.rate;
            float peak = 0;
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)info.rate;
                float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((len - t) / 2.5f);
                float mL = 0, mR = 0;
                if (mFrames > 1 && info.musicRate > 0)
                {
                    double pos = (double)i * info.musicRate / info.rate % mFrames;
                    int a = (int)pos, b = (a + 1) % mFrames;
                    float fr = (float)(pos - a);
                    mL = Mathf.Lerp(music[a * mch], music[b * mch], fr);
                    mR = mch > 1 ? Mathf.Lerp(music[a * mch + 1], music[b * mch + 1], fr) : mL;
                }
                for (int c = 0; c < 2; c++)
                {
                    float s = (2 * i + c < sfx.Length ? sfx[2 * i + c] : 0) + (c == 0 ? mL : mR) * mGain * env;
                    float ax = Math.Abs(s);
                    if (ax > 0.8f) s = Math.Sign(s) * (0.8f + 0.2f * (float)Math.Tanh((ax - 0.8f) / 0.2f));
                    mix[2 * i + c] = s;
                    peak = Math.Max(peak, Math.Abs(s));
                }
            }
            if (peak > 1e-4f)
            {
                // -2 dBFS: AAC encoding overshoots by about a decibel on dense gunfire, and -1 dB still clipped.
                float g = Mathf.Min(0.79f / peak, 4f);
                for (int i = 0; i < mix.Length; i++) mix[i] *= g;
            }
            Debug.Log("[UA] Promo video audio: game rms " + sfxRms.ToString("0.000") + ", music gain " + mGain.ToString("0.00") + ", peak " + peak.ToString("0.00"));
            return mix;
        }

        // ------------------------------------------------------------ builds

        static string[] Scenes => new[] { ScenePath };

        static void Report(BuildReport r, string what)
        {
            var s = r.summary;
            Debug.Log("[UA] " + what + " " + s.result + " in " + s.totalTime + ", " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, errors " + s.totalErrors);
            if (s.result != BuildResult.Succeeded) throw new Exception(what + " failed: " + s.result);
        }

        static void Signing(bool required)
        {
            string props = "Signing/keystore.properties";
            if (!File.Exists(props))
            {
                if (required) throw new Exception("Signing/keystore.properties missing (run Tools\\ua.ps1 keystore)");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            var kv = File.ReadAllLines(props).Where(l => l.Contains("=")).ToDictionary(l => l.Substring(0, l.IndexOf('=')).Trim(), l => l.Substring(l.IndexOf('=') + 1).Trim());
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(Path.Combine("Signing", kv["storeFile"]));
            PlayerSettings.Android.keystorePass = kv["storePassword"];
            PlayerSettings.Android.keyaliasName = kv["keyAlias"];
            PlayerSettings.Android.keyaliasPass = kv["keyPassword"];
        }

        [MenuItem("UA/3. Build APK")]
        public static void BuildApk()
        {
            ConfigurePlayer();
            Signing(false);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/UndeadApocalypse.apk", target = BuildTarget.Android, options = BuildOptions.None });
            Report(r, "APK");
        }

        [MenuItem("UA/4. Build AAB (signed)")]
        public static void BuildAab()
        {
            ConfigurePlayer();
            Signing(true);
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.splitApplicationBinary = false;
            Directory.CreateDirectory("Builds");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/UndeadApocalypse.aab", target = BuildTarget.Android, options = BuildOptions.None });
            EditorUserBuildSettings.buildAppBundle = false;
            Report(r, "AAB");
        }

        [MenuItem("UA/5. Build Windows (test)")]
        public static void BuildWindows()
        {
            ConfigurePlayer();
            Directory.CreateDirectory("Builds/Windows");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/Windows/UndeadApocalypse.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Report(r, "Windows");
        }

        public static void CliSetup() => Run(Setup);
        public static void CliApk() => Run(BuildApk);
        public static void CliAab() => Run(BuildAab);
        public static void CliWin() => Run(BuildWindows);
        public static void CliCompile() => Run(() => Debug.Log("[UA] Compile OK"));
        public static void CliIcon() => Run(MakeIcon);
        public static void CliThumbs() => Run(MakeThumbs);
        public static void CliPromoVideo() => Run(MakePromoVideo);

        static Texture2D LoadPng(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) return null;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Texture2D WriteIcon(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Resample(Texture2D src, int n)
        {
            var o = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    o[y * n + x] = src.GetPixelBilinear((x + 0.5f) / n, (y + 0.5f) / n);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.SetPixels(o);
            t.Apply();
            return t;
        }

        /// <summary>
        /// Android adaptive-icon layer: launchers show only the middle 72 of 108 dp (then mask it to a circle or
        /// squircle), so a full-bleed icon would lose its title. The art is scaled to <paramref name="scale"/> of the
        /// canvas and centred; the margin is a blurred, darkened continuation of the art's own edges.
        /// </summary>
        static Texture2D AdaptiveIcon(Texture2D art, int n, float scale)
        {
            const int s = 16;
            var small = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var src = art.GetPixels();
            int aw = art.width, ah = art.height;
            var sp = new Color[s * s];
            for (int by = 0; by < s; by++)
                for (int bx = 0; bx < s; bx++)
                {
                    var sum = Color.clear;
                    int cnt = 0;
                    for (int y = by * ah / s; y < (by + 1) * ah / s; y += 2)
                        for (int x = bx * aw / s; x < (bx + 1) * aw / s; x += 2) { sum += src[y * aw + x]; cnt++; }
                    sp[by * s + bx] = sum / Mathf.Max(1, cnt);
                }
            small.SetPixels(sp);
            small.Apply();
            var o = new Color[n * n];
            float a0 = (1 - scale) / 2, feather = 2f / (n * scale);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float au = (u - a0) / scale, av = (v - a0) / scale;
                    var bg = small.GetPixelBilinear(Mathf.Clamp01(au), Mathf.Clamp01(av)) * 0.55f;
                    float inside = Mathf.Clamp01(Mathf.Min(Mathf.Min(au, 1 - au), Mathf.Min(av, 1 - av)) / feather + 0.5f);
                    var c = inside > 0 ? Color.Lerp(bg, art.GetPixelBilinear(Mathf.Clamp01(au), Mathf.Clamp01(av)), inside) : bg;
                    c.a = 1;
                    o[y * n + x] = c;
                }
            UnityEngine.Object.DestroyImmediate(small);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.SetPixels(o);
            t.Apply();
            return t;
        }

        /// <summary>2x box downscale (512 store icon from the 1024 render).</summary>
        static Texture2D Half(Texture2D src)
        {
            int w = src.width / 2, h = src.height / 2;
            var s = src.GetPixels();
            var o = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o[y * w + x] = (s[(2 * y) * src.width + 2 * x] + s[(2 * y) * src.width + 2 * x + 1] + s[(2 * y + 1) * src.width + 2 * x] + s[(2 * y + 1) * src.width + 2 * x + 1]) * 0.25f;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(o);
            t.Apply();
            return t;
        }

        static void Run(Action a)
        {
            try
            {
                a();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[UA] " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
