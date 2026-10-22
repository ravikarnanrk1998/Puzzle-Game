using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FRF.EditorTools
{
    /// <summary>
    /// One-click project setup and builds. Usable from the menu (FRF/...) or from the
    /// command line with -executeMethod.
    /// </summary>
    public static class BuildTools
    {
        public const string ProductName = "Freeride Fury";
        public const string CompanyName = "RK Game";
        public const string PackageId = "com.rkgame.freeridefury";
        public const string Version = "1.0.1";
        public const int VersionCode = 2;
        const string SigningFile = "Signing/keystore.properties";
        const string MatDir = "Assets/FRF/Resources/Materials";
        const string SceneDir = "Assets/FRF/Scenes";
        const string ScenePath = SceneDir + "/Boot.unity";
        const string IconDir = "Assets/FRF/Icon";
        const string IconPath = IconDir + "/AppIcon.png";
        const string UrpDir = "Assets/FRF/Settings";

        [MenuItem("FRF/1. Setup Project")]
        public static void Setup()
        {
            EnsureUrpAsset();
            CreateMaterials();
            CreateBootScene();
            CreateIcon();
            ConfigurePlayer();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log("[FRF] Project setup complete.");
        }

        /// <summary>Command line: compile check plus setup, then quit.</summary>
        public static void SetupAndQuit()
        {
            Setup();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------ pipeline

        static void EnsureUrpAsset()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Directory.CreateDirectory(UrpDir);
                string assetPath = UrpDir + "/FRF_URP.asset", rendererPath = UrpDir + "/FRF_URP_Renderer.asset";
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
                Debug.Log("[FRF] Created and assigned " + assetPath);
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

        static Texture2D GreyTexture()
        {
            string path = MatDir + "/DetailGrey.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(MatDir);
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(128, 128, 128, 255);
                t.SetPixels32(px);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void CreateMaterials()
        {
            var lit = Find("Universal Render Pipeline/Lit");
            var unlit = Find("Universal Render Pipeline/Unlit");

            var m = new Material(lit) { name = "FRF_Lit" };
            m.SetFloat("_Smoothness", 0.25f);
            Save(m, "FRF_Lit");

            m = new Material(lit) { name = "FRF_LitDouble" };
            m.SetFloat("_Cull", 0);
            m.doubleSidedGI = true;
            Save(m, "FRF_LitDouble");

            m = new Material(lit) { name = "FRF_LitEmissive" };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Save(m, "FRF_LitEmissive");

            m = new Material(lit) { name = "FRF_LitDetail" };
            m.SetFloat("_Smoothness", 0.08f);
            m.SetTexture("_DetailAlbedoMap", GreyTexture());
            m.SetFloat("_DetailAlbedoMapScale", 1);
            m.EnableKeyword("_DETAIL_MULX2");
            Save(m, "FRF_LitDetail");

            m = new Material(lit) { name = "FRF_LitCutout" };
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.4f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            Save(m, "FRF_LitCutout");

            Save(new Material(unlit) { name = "FRF_Unlit" }, "FRF_Unlit");

            m = new Material(unlit) { name = "FRF_UnlitTransparent" };
            Transparent(m, false);
            Save(m, "FRF_UnlitTransparent");

            m = new Material(unlit) { name = "FRF_Additive" };
            Transparent(m, true);
            Save(m, "FRF_Additive");

            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? unlit;
            m = new Material(particles) { name = "FRF_ParticleAlpha" };
            Transparent(m, false);
            m.SetColor("_BaseColor", Color.white);
            Save(m, "FRF_ParticleAlpha");
            m = new Material(particles) { name = "FRF_ParticleAdd" };
            Transparent(m, true);
            m.SetColor("_BaseColor", Color.white);
            Save(m, "FRF_ParticleAdd");

            m = new Material(Find("Skybox/Procedural")) { name = "FRF_Sky" };
            Save(m, "FRF_Sky");
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
                c.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
                cam.AddComponent<AudioListener>();
                cam.AddComponent<UniversalAdditionalCameraData>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ------------------------------------------------------------ icon

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// Paints the app icon: a bike in mid-air over sunset peaks.
        /// <paramref name="scale"/> &lt; 1 shrinks the art into the centre (adaptive icon safe zone).
        /// </summary>
        static Texture2D PaintIcon(bool background, bool art, float scale)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var top = new Color(1f, 0.55f, 0.15f);
            var bottom = new Color(0.55f, 0.08f, 0.12f);
            var peak = new Color(0.1f, 0.08f, 0.14f);
            var white = new Color(0.98f, 0.97f, 0.95f);
            var yellow = new Color(1f, 0.78f, 0.1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x / (float)size, py = y / (float)size;
                var c = new Color(0, 0, 0, 0);
                if (background)
                {
                    c = Color.Lerp(bottom, top, Mathf.Clamp01(py * 1.2f - 0.1f));
                    float sun = Vector2.Distance(new Vector2(px, py), new Vector2(0.68f, 0.62f));
                    if (sun < 0.16f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.55f), 0.9f);
                    c.a = 1;
                }
                if (art)
                {
                    float u = 0.5f + (px - 0.5f) / scale, v = 0.5f + (py - 0.5f) / scale;
                    // Mountains.
                    float m1 = 0.42f - Mathf.Abs(u - 0.3f) * 1.1f, m2 = 0.36f - Mathf.Abs(u - 0.78f) * 0.9f;
                    float ridge = Mathf.Max(m1, m2, 0.12f);
                    if (v < ridge && u > -0.2f && u < 1.2f)
                    {
                        c = peak;
                        bool cap = (v > m1 - 0.06f && v < m1 + 0.01f && m1 > 0.3f) || (v > m2 - 0.05f && m2 > 0.27f);
                        if (cap) c = white;
                        c.a = 1;
                    }
                    // Bike: two wheels and a frame, tilted for a jump.
                    var p = new Vector2(u, v);
                    var w1 = new Vector2(0.33f, 0.56f);
                    var w2 = new Vector2(0.66f, 0.66f);
                    float r = 0.105f, th = 0.022f;
                    float d1 = Mathf.Abs(Vector2.Distance(p, w1) - r), d2 = Mathf.Abs(Vector2.Distance(p, w2) - r);
                    var bb = new Vector2(0.47f, 0.6f);
                    var seat = new Vector2(0.43f, 0.73f);
                    var head = new Vector2(0.6f, 0.79f);
                    var bar = new Vector2(0.58f, 0.85f);
                    float frame = Mathf.Min(SegDist(p, w1, bb), SegDist(p, bb, head), SegDist(p, seat, bb), SegDist(p, seat, head), SegDist(p, head, w2), SegDist(p, head, bar), SegDist(p, seat, w1));
                    if (d1 < th || d2 < th) c = white;
                    if (frame < 0.016f) c = yellow;
                    if (Vector2.Distance(p, w1) < 0.022f || Vector2.Distance(p, w2) < 0.022f) c = yellow;
                    // Speed streaks.
                    for (int k = 0; k < 3; k++)
                    {
                        float sy = 0.5f + k * 0.08f, sx0 = 0.04f + k * 0.03f, sx1 = 0.2f - k * 0.02f;
                        if (u > sx0 && u < sx1 && Mathf.Abs(v - sy) < 0.009f) c = white;
                    }
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
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageId);
            PlayerSettings.Android.bundleVersionCode = VersionCode;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // Desktop test player keeps running unfocused (Android pauses on its own).
            PlayerSettings.runInBackground = true;

            // GLES3 first, Vulkan as fallback; Optimized Frame Pacing off (aborted on the Vulkan path).
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.optimizedFramePacing = false;
        }

        static void ConfigureUrp()
        {
            Tune(GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
            for (int i = 0; i < QualitySettings.names.Length; i++) Tune(QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
        }

        static void Tune(UniversalRenderPipelineAsset urp)
        {
            if (urp == null) return;
            urp.supportsHDR = true;
            urp.msaaSampleCount = 2;
            urp.shadowDistance = 120;
            urp.shadowCascadeCount = 2;
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
            SetInt("m_AdditionalLightsRenderingMode", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        // ------------------------------------------------------------ builds

        [MenuItem("FRF/2. Build Test APK")]
        public static void BuildApk() => Build(false);

        [MenuItem("FRF/3. Build Play Store AAB")]
        public static void BuildAab() => Build(true);

        /// <summary>Windows player for quick desktop testing and screenshots (run with -frfShots &lt;dir&gt;).</summary>
        [MenuItem("FRF/4. Build Windows Test Player")]
        public static void BuildWindows()
        {
            Setup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/FreerideFury.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var s = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("[FRF] Build " + s.result + " -> " + s.outputPath + " (" + s.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Upload-key signing, from environment variables (FRF_KEYSTORE, FRF_KEYSTORE_PASS,
        /// FRF_KEY_ALIAS, FRF_KEY_PASS) or else from Signing/keystore.properties
        /// (storeFile=, storePassword=, keyAlias=, keyPassword=). The Signing folder is git-ignored.
        /// </summary>
        static void ApplySigning()
        {
            string ks = Environment.GetEnvironmentVariable("FRF_KEYSTORE");
            string ksPass = Environment.GetEnvironmentVariable("FRF_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("FRF_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("FRF_KEY_PASS");
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
            Debug.Log("[FRF] Signing with upload key '" + alias + "' from " + ks);
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
                    Debug.LogWarning("[FRF] Debug symbol settings not found; building without symbols.");
                    return;
                }
                level.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
                format.SetValue(null, Enum.Parse(format.PropertyType, "IncludeInBundle"));
                Debug.Log("[FRF] Native symbol table included in the bundle.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FRF] Could not enable debug symbols: " + e.Message);
            }
        }

        static void Build(bool aab)
        {
            Setup();
            ApplySigning();
            if (aab && !PlayerSettings.Android.useCustomKeystore)
                Debug.LogWarning("[FRF] Building an AAB with the debug key: Google Play will reject it. Set FRF_KEYSTORE etc.");
            PlayerSettings.Android.targetArchitectures = aab ? AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7 : AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = aab;
            if (aab) EnableDebugSymbols();
            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = aab ? "Builds/FreerideFury.aab" : "Builds/FreerideFury.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[FRF] Build " + s.result + " -> " + s.outputPath + " (" + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
