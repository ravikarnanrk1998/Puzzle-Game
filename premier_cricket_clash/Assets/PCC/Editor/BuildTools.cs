using System;
using System.IO;
using PCC.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PCC.EditorTools
{
    /// <summary>
    /// One-click project setup and Android builds. Usable from the menu
    /// (PCC/...) or from the command line with -executeMethod.
    /// </summary>
    public static class BuildTools
    {
        public const string ProductName = "Premier Cricket Clash 2027";
        public const string CompanyName = "RK Game";
        public const string PackageId = "com.rkgame.premiercricketclase";
        public const string Version = "1.0.2";
        public const int VersionCode = 3;
        const string SigningFile = "Signing/keystore.properties";
        const string MatDir = "Assets/PCC/Resources/Materials";
        const string SceneDir = "Assets/PCC/Scenes";
        const string ScenePath = SceneDir + "/Boot.unity";
        const string IconDir = "Assets/PCC/Icon";
        const string IconPath = IconDir + "/AppIcon.png";

        [MenuItem("PCC/1. Setup Project")]
        public static void Setup()
        {
            EnsureUrpAsset();
            CreateMaterials();
            CreateBootScene();
            CreateIcon();
            ConfigurePlayer();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log("[PCC] Project setup complete.");
        }

        // ------------------------------------------------------------ pipeline

        const string UrpDir = "Assets/PCC/Settings";

        /// <summary>Projects not made from the URP template get a URP asset here and on every quality level.</summary>
        static void EnsureUrpAsset()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Directory.CreateDirectory(UrpDir);
                string assetPath = UrpDir + "/PCC_URP.asset", rendererPath = UrpDir + "/PCC_URP_Renderer.asset";
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                if (urp == null)
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                    var post = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    var so = new SerializedObject(renderer);
                    var p = so.FindProperty("postProcessData");
                    if (p != null && post != null)
                    {
                        p.objectReferenceValue = post;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    urp = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(urp, assetPath);
                    AssetDatabase.SaveAssets();
                }
                GraphicsSettings.defaultRenderPipeline = urp;
                Debug.Log("[PCC] Created and assigned " + assetPath);
            }
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) != null) continue;
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        // ------------------------------------------------------------ materials

        static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null) throw new Exception("Shader not found: " + name + " (is URP installed and active?)");
            return s;
        }

        static void Save(Material m, string name)
        {
            Directory.CreateDirectory(MatDir);
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(m, path);
            }
        }

        static void Transparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static void CreateMaterials()
        {
            var lit = Find("Universal Render Pipeline/Lit");
            var unlit = Find("Universal Render Pipeline/Unlit");

            var m = new Material(lit) { name = "PCC_Lit" };
            m.SetFloat("_Smoothness", 0.25f);
            Save(m, "PCC_Lit");

            m = new Material(lit) { name = "PCC_LitDouble" };
            m.SetFloat("_Cull", 0);
            m.doubleSidedGI = true;
            Save(m, "PCC_LitDouble");

            m = new Material(lit) { name = "PCC_LitEmissive" };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Save(m, "PCC_LitEmissive");

            m = new Material(lit) { name = "PCC_LitCutout" };
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.4f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            Save(m, "PCC_LitCutout");

            Save(new Material(unlit) { name = "PCC_Unlit" }, "PCC_Unlit");

            m = new Material(unlit) { name = "PCC_UnlitTransparent" };
            Transparent(m, false);
            Save(m, "PCC_UnlitTransparent");

            m = new Material(unlit) { name = "PCC_Additive" };
            Transparent(m, true);
            Save(m, "PCC_Additive");

            var particles = Shader.Find("Universal Render Pipeline/Particles/Simple Lit") ?? lit;
            m = new Material(particles) { name = "PCC_VertexColor" };
            m.SetFloat("_Cull", 0);
            m.SetColor("_BaseColor", Color.white);
            Save(m, "PCC_VertexColor");

            m = new Material(Find("Skybox/Procedural")) { name = "PCC_Sky" };
            Save(m, "PCC_Sky");
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ scene

        static void CreateBootScene()
        {
            Directory.CreateDirectory(SceneDir);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera") { tag = "MainCamera" };
                var c = cam.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.01f, 0.02f, 0.07f);
                cam.AddComponent<AudioListener>();
                cam.AddComponent<UniversalAdditionalCameraData>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------ icon

        /// <summary>
        /// Paints the app icon: glowing ball and stumps on the league's night-blue.
        /// <paramref name="scale"/> &lt; 1 shrinks the art into the centre (adaptive icon safe zone).
        /// </summary>
        static Texture2D PaintIcon(bool background, bool art, float scale)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var gold = new Color(1f, 0.78f, 0.29f);
            var cyan = new Color(0.22f, 0.91f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x / (float)size, py = y / (float)size;
                var c = new Color(0, 0, 0, 0);
                if (background)
                {
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(0.5f, 0.56f));
                    c = Color.Lerp(new Color(0.09f, 0.12f, 0.36f), new Color(0.01f, 0.02f, 0.07f), Mathf.Clamp01(d * 1.6f));
                    c.a = 1;
                }
                if (art)
                {
                    float u = 0.5f + (px - 0.5f) / scale, v = 0.5f + (py - 0.5f) / scale;
                    // Stumps and bails.
                    for (int s = -1; s <= 1; s++)
                    {
                        float sx = 0.5f + s * 0.11f;
                        if (Mathf.Abs(u - sx) < 0.03f && v > 0.16f && v < 0.68f) c = Color.Lerp(new Color(0.95f, 0.93f, 0.88f), gold, (0.68f - v) * 0.6f);
                    }
                    if (v > 0.68f && v < 0.705f && Mathf.Abs(u - 0.5f) < 0.15f) c = gold;
                    // Ball with a glow and seam.
                    float bd = Vector2.Distance(new Vector2(u, v), new Vector2(0.66f, 0.72f));
                    if (bd < 0.13f)
                    {
                        c = Color.Lerp(new Color(0.85f, 0.1f, 0.15f), new Color(0.45f, 0.02f, 0.06f), bd / 0.13f);
                        float seam = Mathf.Abs((u - 0.66f) + (v - 0.72f) * 0.35f);
                        if (seam < 0.012f) c = new Color(0.98f, 0.95f, 0.85f);
                    }
                    else if (bd < 0.2f)
                    {
                        var glow = cyan;
                        glow.a = (0.2f - bd) / 0.07f * 0.35f;
                        c = background ? Color.Lerp(c, cyan, glow.a) : (c.a > 0 ? c : glow);
                    }
                    // Ground line.
                    if (v > 0.14f && v < 0.16f && u > 0.18f && u < 0.82f) c = cyan;
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        static Texture2D SaveIcon(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CreateIcon()
        {
            var full = SaveIcon(PaintIcon(true, true, 1), IconPath);
            var bg = SaveIcon(PaintIcon(true, false, 1), IconDir + "/AppIconBackground.png");
            var fg = SaveIcon(PaintIcon(false, true, 0.62f), IconDir + "/AppIconForeground.png");
            if (full == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            // Android launchers use adaptive / round / legacy icons, which do not fall back to the default.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2)
                    {
                        icon.SetTexture(bg, 0);
                        icon.SetTexture(fg, 1);
                    }
                    else
                    {
                        icon.SetTexture(full, 0);
                    }
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
        }

        // ------------------------------------------------------------ player

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            // Raise VersionCode for every Play Store upload.
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            // Landscape only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // GLES3 first, Vulkan as fallback: the safest choice across phones and emulators.
            // Optimized Frame Pacing (Swappy) aborted at startup on the Vulkan path, so it is off.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.optimizedFramePacing = false;
        }

        static void ConfigureUrp()
        {
            foreach (var rp in new[] { GraphicsSettings.defaultRenderPipeline }) Tune(rp as UniversalRenderPipelineAsset);
            for (int i = 0; i < QualitySettings.names.Length; i++) Tune(QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
        }

        static void Tune(UniversalRenderPipelineAsset urp)
        {
            if (urp == null) return;
            urp.supportsHDR = true;
            urp.msaaSampleCount = 2;
            urp.shadowDistance = 150;
            urp.shadowCascadeCount = 2;
            urp.maxAdditionalLightsCount = Mathf.Max(urp.maxAdditionalLightsCount, 4);
            var so = new SerializedObject(urp);
            void SetInt(string prop, int v)
            {
                var p = so.FindProperty(prop);
                if (p != null) p.intValue = v;
            }
            void SetBool(string prop, bool v)
            {
                var p = so.FindProperty(prop);
                if (p != null) p.boolValue = v;
            }
            SetInt("m_MainLightShadowmapResolution", 2048);
            SetBool("m_MainLightShadowsSupported", true);
            SetBool("m_SoftShadowsSupported", true);
            SetInt("m_AdditionalLightsRenderingMode", 1); // per pixel
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        // ------------------------------------------------------------ builds

        [MenuItem("PCC/2. Build Test APK")]
        public static void BuildApk() => Build(false);

        [MenuItem("PCC/3. Build Play Store AAB")]
        public static void BuildAab() => Build(true);

        /// <summary>Windows player for quick desktop testing and screenshots (run with -pccShots &lt;dir&gt;).</summary>
        [MenuItem("PCC/4. Build Windows Test Player")]
        public static void BuildWindows()
        {
            Setup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/PremierCricketClash2027.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var s = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("[PCC] Build " + s.result + " -> " + s.outputPath + " (" + s.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Upload-key signing, from environment variables (PCC_KEYSTORE, PCC_KEYSTORE_PASS,
        /// PCC_KEY_ALIAS, PCC_KEY_PASS) or else from Signing/keystore.properties
        /// (storeFile=, storePassword=, keyAlias=, keyPassword=). The Signing folder is git-ignored.
        /// </summary>
        static void ApplySigning()
        {
            string ks = Environment.GetEnvironmentVariable("PCC_KEYSTORE");
            string ksPass = Environment.GetEnvironmentVariable("PCC_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("PCC_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("PCC_KEY_PASS");
            if (string.IsNullOrEmpty(ks) && File.Exists(SigningFile))
            {
                var props = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(SigningFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.TrimStart().StartsWith("#")) props[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                props.TryGetValue("storeFile", out ks);
                props.TryGetValue("storePassword", out ksPass);
                props.TryGetValue("keyAlias", out alias);
                props.TryGetValue("keyPassword", out keyPass);
                if (!string.IsNullOrEmpty(ks) && !Path.IsPathRooted(ks)) ks = Path.GetFullPath(Path.Combine("Signing", ks));
            }
            if (string.IsNullOrEmpty(ks) || !File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = ksPass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log("[PCC] Signing with upload key '" + alias + "' from " + ks);
        }

        /// <summary>
        /// Puts the native symbol table into the bundle so Play Console can symbolicate crashes
        /// (set by reflection: the Android editor API moved between Unity versions).
        /// </summary>
        static void EnableDebugSymbols()
        {
            try
            {
                var t = Type.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols, UnityEditor.Android.Extensions");
                var level = t?.GetProperty("level", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var format = t?.GetProperty("format", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (level == null || format == null)
                {
                    Debug.LogWarning("[PCC] Debug symbol settings not found; building without symbols.");
                    return;
                }
                level.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
                format.SetValue(null, Enum.Parse(format.PropertyType, "IncludeInBundle"));
                Debug.Log("[PCC] Native symbol table included in the bundle.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PCC] Could not enable debug symbols: " + e.Message);
            }
        }

        static void Build(bool aab)
        {
            Setup();
            ApplySigning();
            if (aab && !PlayerSettings.Android.useCustomKeystore)
                Debug.LogWarning("[PCC] Building an AAB with the debug key: Google Play will reject it. Set PCC_KEYSTORE etc.");
            // Unity 6.6+ builds ARM only (x86_64 emulators run ARM64 apps through translation).
            PlayerSettings.Android.targetArchitectures = aab
                ? AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7
                : AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = aab;
            if (aab) EnableDebugSymbols();
            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = aab ? "Builds/PremierCricketClash2027.aab" : "Builds/PremierCricketClash2027.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[PCC] Build " + s.result + " -> " + s.outputPath + " (" + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
