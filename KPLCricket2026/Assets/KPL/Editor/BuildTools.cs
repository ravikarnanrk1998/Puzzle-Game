using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KPL.Core;
using KPL.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KPL.EditorTools
{
    /// <summary>Project setup (URP quality tiers, materials, player settings, icon, scene) and builds.</summary>
    public static class BuildTools
    {
        public const string Package = "com.rkgame.kplcricket2026";
        public const string Product = "KPL Cricket 2026";
        public const string Version = "1.0.0";
        public const int VersionCode = 1;
        const string Root = "Assets/KPL";
        const string ScenePath = Root + "/Scenes/Boot.unity";
        static readonly string[] TierNames = { "Low", "Medium", "High", "Ultra" };

        [MenuItem("KPL/1. Setup Project")]
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
            Debug.Log("[KPL] Setup complete.");
        }

        // --------------------------------------------------------- URP tiers

        static UniversalRenderPipelineAsset[] CreatePipelineAssets()
        {
            var list = new UniversalRenderPipelineAsset[4];
            for (int i = 0; i < 4; i++)
            {
                string rPath = Root + "/Settings/KPL_Renderer_" + TierNames[i] + ".asset";
                string aPath = Root + "/Settings/KPL_URP_" + TierNames[i] + ".asset";
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
                SetProp(so, "m_RenderingMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (i == 3) AddSsao(renderer);

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
                float[] scale = { 0.72f, 0.85f, 1f, 1f };
                int[] msaa = { 1, 1, 2, 4 };
                int[] shadowRes = { 512, 1024, 2048, 4096 };
                float[] shadowDist = { 45, 70, 100, 140 };
                int[] cascades = { 1, 1, 2, 2 };
                SetProp(a, "m_RenderScale", scale[i]);
                SetProp(a, "m_MSAA", msaa[i]);
                SetProp(a, "m_SupportsHDR", i >= 1 ? 1 : 0);
                SetProp(a, "m_HDRColorBufferPrecision", 0);
                SetProp(a, "m_MainLightRenderingMode", 1);
                SetProp(a, "m_MainLightShadowsSupported", 1);
                SetProp(a, "m_MainLightShadowmapResolution", shadowRes[i]);
                SetProp(a, "m_ShadowDistance", shadowDist[i]);
                SetProp(a, "m_ShadowCascadeCount", cascades[i]);
                SetProp(a, "m_Cascade2Split", 0.3f);
                SetProp(a, "m_SoftShadowsSupported", i >= 2 ? 1 : 0);
                SetProp(a, "m_SoftShadowQuality", i == 3 ? 3 : 2);
                SetProp(a, "m_AdditionalLightsRenderingMode", i >= 2 ? 1 : 0);
                SetProp(a, "m_AdditionalLightsPerObjectLimit", i >= 2 ? 4 : 0);
                SetProp(a, "m_AdditionalLightShadowsSupported", 0);
                SetProp(a, "m_RequireDepthTexture", i == 3 ? 1 : 0);
                SetProp(a, "m_RequireOpaqueTexture", 0);
                SetProp(a, "m_UseSRPBatcher", 1);
                SetProp(a, "m_SupportsDynamicBatching", 0);
                SetProp(a, "m_ColorGradingMode", i >= 2 ? 1 : 0);
                SetProp(a, "m_ColorGradingLutSize", i >= 2 ? 32 : 16);
                SetProp(a, "m_ShadowDepthBias", 1.2f);
                SetProp(a, "m_ShadowNormalBias", 1.0f);
                a.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                list[i] = asset;
            }
            GraphicsSettings.defaultRenderPipeline = list[1];
            return list;
        }

        static void AddSsao(UniversalRendererData renderer)
        {
            try
            {
                if (renderer.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion")) return;
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) return;
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
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
                EditorUtility.SetDirty(renderer);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KPL] SSAO not added: " + e.Message);
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
                case SerializedPropertyType.Enum: p.enumValueIndex = Mathf.Clamp((int)v, 0, p.enumNames.Length - 1); if (name == "m_MSAA" || name.Contains("Resolution")) p.intValue = (int)v; break;
            }
        }

        static void ConfigureQuality(UniversalRenderPipelineAsset[] assets)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qs == null) return;
            var so = new SerializedObject(qs);
            var arr = so.FindProperty("m_QualitySettings");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = TierNames[i];
                Rel(e, "pixelLightCount", i >= 2 ? 4 : 1);
                Rel(e, "shadows", 2);
                Rel(e, "shadowResolution", i);
                Rel(e, "shadowDistance", new[] { 45f, 70, 100, 140 }[i]);
                Rel(e, "antiAliasing", 0);
                Rel(e, "softParticles", 0);
                Rel(e, "realtimeReflectionProbes", i >= 2 ? 1 : 0);
                Rel(e, "vSyncCount", 0);
                Rel(e, "lodBias", new[] { 0.6f, 0.9f, 1.2f, 1.6f }[i]);
                Rel(e, "skinWeights", i >= 2 ? 4 : 2);
                Rel(e, "globalTextureMipmapLimit", i == 0 ? 1 : 0);
                Rel(e, "anisotropicTextures", i >= 2 ? 1 : 0);
                Rel(e, "asyncUploadTimeSlice", 2);
                Rel(e, "asyncUploadBufferSize", 16);
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
            var sky = Shader.Find("Skybox/Panoramic");
            var flat = FlatNormalAsset();
            Mat("Lit", lit, m => { });
            Mat("LitNormal", lit, m =>
            {
                m.SetTexture("_BumpMap", flat);
                m.EnableKeyword("_NORMALMAP");
            });
            Mat("LitDetail", lit, m =>
            {
                m.SetTexture("_BumpMap", flat);
                m.SetTexture("_DetailAlbedoMap", Texture2D.grayTexture);
                m.SetTexture("_DetailNormalMap", flat);
                m.EnableKeyword("_NORMALMAP");
                m.EnableKeyword("_DETAIL_MULX2");
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
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            Mat("LitTransparent", lit, m => Transparent(m, false));
            Mat("Unlit", unlit, m => { });
            Mat("UnlitTransparent", unlit, m => Transparent(m, false));
            Mat("UnlitAdditive", unlit, m => Transparent(m, true));
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

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
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
            Add<WhiteBalance>();
            if (p.TryGet(out Tonemapping tm)) tm.mode.Override(TonemappingMode.ACES);
            if (p.TryGet(out Bloom b))
            {
                b.intensity.Override(0.5f);
                b.threshold.Override(0.9f);
            }
            EditorUtility.SetDirty(p);
        }

        // ------------------------------------------------------------ player

        static void ConfigurePlayer()
        {
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
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            // Legacy input manager only (touch via Input + uGUI).
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                SetProp(so, "activeInputHandler", 0);
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

        [MenuItem("KPL/2. Make Icon")]
        public static void MakeIcon()
        {
            try
            {
                Quality.Apply(Gfx.High);
                var tex = TextBake.Render(1024, 1024, Pal.Hex(0x081226), area =>
                {
                    var bg = Ui.Img(area, Sprites.VGradient(1, 0), Pal.Hex(0x1B3C8F), "Grad");
                    bg.rectTransform.Fill();
                    var glow = Ui.Img(area, Sprites.Glow, Pal.Accent.A(0.55f), "Glow");
                    glow.rectTransform.At(0.5f, 0.5f, 0, 60, 900, 900);
                    var stripes = Ui.Img(area, Sprites.Stripes, new Color(1, 1, 1, 0.5f), "Stripes");
                    stripes.type = UnityEngine.UI.Image.Type.Tiled;
                    stripes.rectTransform.Fill();
                    var ballShadow = Ui.Img(area, Sprites.Glow, new Color(0, 0, 0, 0.45f), "BallShadow");
                    ballShadow.rectTransform.At(0.5f, 0.5f, 225, 205, 380, 380);
                    var ball = Ui.Img(area, Sprites.Circle, Pal.Hex(0xB3121F), "Ball");
                    ball.rectTransform.At(0.5f, 0.5f, 210, 230, 300, 300);
                    var shade = Ui.Img(ball.transform, Sprites.Glow, Pal.Hex(0xFF5A5A, 0.55f), "Shade");
                    shade.rectTransform.At(0.5f, 0.5f, -50, 50, 260, 260);
                    // Curved seam: two large rings clipped by the ball.
                    ball.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
                    var seamA = Ui.Img(ball.transform, Sprites.Ring(6), new Color(1, 0.95f, 0.9f, 0.95f), "SeamA");
                    seamA.rectTransform.At(0.5f, 0.5f, 250, -60, 560, 560);
                    var seamB = Ui.Img(ball.transform, Sprites.Ring(6), new Color(1, 0.95f, 0.9f, 0.7f), "SeamB");
                    seamB.rectTransform.At(0.5f, 0.5f, 268, -72, 560, 560);
                    var gloss = Ui.Img(ball.transform, Sprites.Glow, new Color(1, 1, 1, 0.5f), "Gloss");
                    gloss.rectTransform.At(0.5f, 0.5f, -70, 70, 90, 90);
                    var bat = Ui.Img(area, Sprites.Of(Icon.Bat), Pal.Hex(0xF2DFB0), "Bat");
                    bat.rectTransform.At(0.5f, 0.5f, -150, 120, 520, 520);
                    var k = Ui.Label(area, "KPL", 380, Color.white, TextAnchor.MiddleCenter, KPL.Game.Font.Display);
                    k.rectTransform.At(0.5f, 0.5f, 0, -140, 1000, 380);
                    Ui.Shadow(k, 10, 0.6f);
                    var y = Ui.Panel(area, Pal.Accent, 40, "Year");
                    y.rectTransform.At(0.5f, 0, 0, 110, 520, 130);
                    var yt = Ui.Label(y.transform, "CRICKET 2026", 92, Color.white, TextAnchor.MiddleCenter, KPL.Game.Font.Display);
                    yt.rectTransform.Fill();
                }, false);
                string path = Root + "/Icon/AppIcon.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                // Android adaptive icon (background + foreground layers).
                var adaptive = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
                foreach (var i in adaptive)
                {
                    i.SetTexture(icon, 0);
                    i.SetTexture(icon, 1);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);
                Directory.CreateDirectory("PlayStore");
                File.Copy(path, "PlayStore/icon-1024.png", true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KPL] Icon: " + e.Message);
            }
        }

        // ------------------------------------------------------------ builds

        static string[] Scenes => new[] { ScenePath };

        static void Report(BuildReport r, string what)
        {
            var s = r.summary;
            Debug.Log("[KPL] " + what + " " + s.result + " in " + s.totalTime + ", " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, errors " + s.totalErrors);
            if (s.result != BuildResult.Succeeded) throw new Exception(what + " failed: " + s.result);
        }

        static void Signing(bool required)
        {
            string props = "Signing/keystore.properties";
            if (!File.Exists(props))
            {
                if (required) throw new Exception("Signing/keystore.properties missing");
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

        [MenuItem("KPL/3. Build APK")]
        public static void BuildApk()
        {
            ConfigurePlayer();
            Signing(false);
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/KPLCricket2026.apk", target = BuildTarget.Android, options = BuildOptions.None });
            Report(r, "APK");
        }

        [MenuItem("KPL/4. Build AAB (signed)")]
        public static void BuildAab()
        {
            ConfigurePlayer();
            Signing(true);
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.splitApplicationBinary = false;
            Directory.CreateDirectory("Builds");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/KPLCricket2026.aab", target = BuildTarget.Android, options = BuildOptions.None });
            EditorUserBuildSettings.buildAppBundle = false;
            Report(r, "AAB");
        }

        [MenuItem("KPL/5. Build Windows (test)")]
        public static void BuildWindows()
        {
            ConfigurePlayer();
            Directory.CreateDirectory("Builds/Windows");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/Windows/KPLCricket2026.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            Report(r, "Windows");
        }

        public static void CliSetup() => Run(Setup);
        public static void CliApk() => Run(BuildApk);
        public static void CliAab() => Run(BuildAab);
        public static void CliWin() => Run(BuildWindows);

        static void Run(Action a)
        {
            try
            {
                a();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[KPL] " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
