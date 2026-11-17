using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UA.Game;
using UnityEditor;
using UnityEngine;

namespace UA.EditorTools
{
    /// <summary>
    /// Import rules for third-party art placed under Assets/UA/Art/Source (Poly Haven, Mixamo, ...):
    /// mobile texture sizes and compression, no embedded materials (URP materials are generated),
    /// no cameras/lights. Also an inspector that logs hierarchy, triangle counts and sizes.
    /// </summary>
    public sealed class ArtImportRules : AssetPostprocessor
    {
        public const string SourceRoot = "Assets/UA/Art/Source/";
        public const string GeneratedRoot = "Assets/UA/Art/Generated/";

        static bool InSource(string path)
        {
            var p = path.Replace('\\', '/');
            return p.StartsWith(SourceRoot, StringComparison.OrdinalIgnoreCase) || p.StartsWith(GeneratedRoot, StringComparison.OrdinalIgnoreCase);
        }

        void OnPreprocessTexture()
        {
            if (!InSource(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool normal = name.Contains("nor_gl") || name.Contains("normal");
            bool data = name.Contains("_arm") || name.Contains("rough") || name.Contains("metal") || name.Contains("_ao") || name.Contains("mask");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !(normal || data);
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.isReadable = false;  // packed maps are converted from the source files, not from imported pixels
            if (name.Contains("opacity"))
            {
                // Alpha-tested hair cards: keep the same coverage in every mip level, or distant hair thins out to a "cage".
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.3f;
            }
            ti.textureCompression = TextureImporterCompression.Compressed;
            // Characters (Rocketbox) come in large numbers: colour maps keep 1024 / ASTC 6x6 for faces and clothes,
            // normal maps use ASTC 8x8, and smoothness masks and emission maps (low-frequency data) 512.
            bool character = assetPath.Replace('\\', '/').Contains("/Rocketbox/");
            bool small = character && (name.Contains("mask") || name.Contains("emit"));
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = small ? 512 : 1024,
                format = small || (normal && character) ? TextureImporterFormat.ASTC_8x8 : normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50,
            });
        }

        // Bumped when the rules change, so Unity re-imports the affected textures and models.
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!InSource(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            // Props and weapons are static. Characters keep their skin: Mixamo as Humanoid (for its clips),
            // Rocketbox as Generic (the game drives the bones from its own procedural skeleton).
            string p = assetPath.Replace('\\', '/');
            bool mixamo = p.Contains("/Mixamo/"), rocketbox = p.Contains("/Rocketbox/");
            mi.animationType = mixamo ? ModelImporterAnimationType.Human : rocketbox ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            mi.importAnimation = mixamo;
            mi.importBlendShapes = false;
            // Keep material slot names so the pipeline can match each submesh to its texture set.
            if (rocketbox) mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }
    }

    public static class ArtPipeline
    {
        /// <summary>Logs every model under the source folder: parts, triangles, bounds (metres).</summary>
        public static void Inspect()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/UA/Art/Source" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int tris = 0;
                var b = new Bounds();
                bool first = true;
                sb.AppendLine("[UA] ART " + path);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var mf = t.GetComponent<MeshFilter>();
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    var mesh = mf != null ? mf.sharedMesh : smr != null ? smr.sharedMesh : null;
                    string depth = new string(' ', 2 * Depth(t, go.transform));
                    if (mesh == null) { sb.AppendLine("[UA] ART   " + depth + t.name + " pos " + t.localPosition.ToString("F3") + " rot " + t.localEulerAngles.ToString("F0") + " scale " + t.localScale.ToString("F3")); continue; }
                    int tc = mesh.triangles.Length / 3;
                    tris += tc;
                    var r = t.GetComponent<Renderer>();
                    var wb = r.bounds;
                    if (first) { b = wb; first = false; } else b.Encapsulate(wb);
                    sb.AppendLine("[UA] ART   " + depth + t.name + " tris " + tc + " submeshes " + mesh.subMeshCount + " bounds c" + wb.center.ToString("F3") + " s" + wb.size.ToString("F3") + " pos " + t.localPosition.ToString("F3") + " rot " + t.localEulerAngles.ToString("F0") + " scale " + t.localScale.ToString("F3") + (smr != null ? " SKINNED bones " + smr.bones.Length : ""));
                    sb.AppendLine("[UA] ART   " + depth + "  materials: " + string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : m.name)) + " | submesh tris: " + string.Join(", ", Enumerable.Range(0, mesh.subMeshCount).Select(s => mesh.GetIndexCount(s) / 3)));
                    if (smr != null) sb.AppendLine("[UA] ART   " + depth + "  bones: " + string.Join(", ", smr.bones.Select(x => x == null ? "null" : x.name + "@" + x.position.ToString("F2"))));
                }
                sb.AppendLine("[UA] ART   TOTAL tris " + tris + " bounds c" + b.center.ToString("F3") + " size " + b.size.ToString("F3"));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UA/Art/Source" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                sb.AppendLine("[UA] ART tex " + path + " " + t.width + "x" + t.height + " " + t.format);
            }
            Debug.Log(sb.ToString());
        }

        static int Depth(Transform t, Transform root)
        {
            int d = 0;
            while (t != root && t != null) { t = t.parent; d++; }
            return d;
        }

        public static void CliInspect() => Run(Inspect);
        public static void CliBuild() => Run(BuildAll);

        const string PrefabRoot = "Assets/UA/Resources/UAArt/";
        const string MaterialRoot = "Assets/UA/Art/Materials/";

        [MenuItem("UA/Art/Build imported prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabRoot + "Weapons");
            Directory.CreateDirectory(MaterialRoot);
            BuildServicePistol();
            BuildBoltRifle();
            // Pickups (Poly Haven, CC0), scaled so their largest horizontal side matches the procedural pickup.
            BuildProp("medical_box", "Items/Health", 0.46f);
            BuildProp("ammo_box", "Items/Ammo", 0.4f);
            // The military crate is long and flat (9 cm tall at 0.56 m), so it is shown larger to read on the floor.
            BuildProp("old_military_crate", "Items/Grenades", 0.78f);
            // Microsoft Rocketbox avatars (MIT). Zombies and bosses get generated infected maps; playable characters
            // and rescue survivors keep the original textures. Avatars that are not downloaded are skipped (the game
            // then keeps the procedural model for that key).
            foreach (var (avatar, key, opts) in RocketboxBuilds) BuildRocketbox(avatar, key, opts);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>How one Rocketbox avatar is treated.</summary>
        sealed class RbOpts
        {
            public bool Infected;
            public int Seed;
            /// <summary>Keep alpha-tested hair cards on an infected avatar (long hair; short-hair cards read as a cage).</summary>
            public bool KeepHairCards;
            /// <summary>Emissive veins over the skin (black = none).</summary>
            public Color Glow = Color.black;
            public Color SkinTint = Color.white;
            /// <summary>Multiplier on clothes and gear colours (below 1 darkens them).</summary>
            public float ClothDark = 1;
            public float Blood = 1;
            /// <summary>Multiplier on hair colour (cards and painted scalp hair); below 1 darkens it.</summary>
            public float HairDark = 1;
            /// <summary>Extra material slots to leave out (matched against the end of the slot name).</summary>
            public string[] Drop = new string[0];
        }

        static RbOpts Z(int seed, bool keepHair = false, float blood = 1, float clothDark = 1, Color? glow = null, Color? tint = null, string[] drop = null) =>
            new RbOpts { Infected = true, Seed = seed, KeepHairCards = keepHair, Blood = blood, ClothDark = clothDark, Glow = glow ?? Color.black, SkinTint = tint ?? Color.white, Drop = drop ?? new string[0] };

        static RbOpts Clean() => new RbOpts();

        static RbOpts WithHair(RbOpts o, float hairDark) { o.HairDark = hairDark; return o; }

        static readonly (string avatar, string key, RbOpts opts)[] RocketboxBuilds =
        {
            // Zombies. Walkers are the bulk of every horde, so they rotate through five infected civilians.
            ("Male_Adult_08", "Zombies/Walker", Z(101)),
            ("Male_Adult_13", "Zombies/Walker_1", Z(111)),
            ("Female_Adult_01", "Zombies/Walker_2", Z(121, keepHair: true)),
            ("Business_Male_04", "Zombies/Walker_3", Z(131)),
            ("Medical_Male_04", "Zombies/Walker_4", Z(141)),
            ("Sports_Male_04", "Zombies/Runner", Z(151)),
            ("Female_Adult_08", "Zombies/Crawler", Z(161, keepHair: true, blood: 1.3f)),
            ("Construction_Male_08", "Zombies/Explosive", Z(171)),
            ("Police_Female_01", "Zombies/Armored", Z(181, keepHair: true)),
            ("Sports_Male_01", "Zombies/Toxic", Z(191, glow: new Color(0.24f, 1.5f, 0.16f), tint: new Color(0.9f, 1.04f, 0.8f))),
            ("Military_Male_04", "Zombies/Military", Z(201)),
            // Hard hat left out so it doesn't read as the Explosive; spikes, claws and size come from the game.
            ("Construction_Male_04", "Zombies/Mutant", Z(211, blood: 1.4f, drop: new[] { "helmet" })),
            ("Male_Adult_18", "Zombies/Hunter", Z(221, clothDark: 0.4f)),
            // Bosses (the boss code scales them up and adds their gear: cleaver, claws, sacs, spikes).
            ("Military_Male_01", "Bosses/Juggernaut", Z(202)),
            ("Gardener_Male_01", "Bosses/Butcher", Z(232, blood: 1.8f)),
            // BroodMother: bloated toxic host, green-tinted skin with glowing veins; glowing sacs come from the game.
            ("Female_Adult_11", "Bosses/BroodMother", Z(242, keepHair: true, blood: 1.5f, clothDark: 0.6f, glow: new Color(0.2f, 1.2f, 0.12f), tint: new Color(0.86f, 1.05f, 0.78f))),
            // Wraith: gaunt figure in black with long hair; red eyes, claws and cloaking come from the game.
            ("Female_Adult_04", "Bosses/Wraith", WithHair(Z(252, keepHair: true, blood: 1.2f, clothDark: 0.3f), 0.3f)),
            // Playable characters, in Weapons.OutfitNames order.
            ("Military_Male_01", "Characters/Character0", Clean()),
            ("Police_Male_02", "Characters/Character1", Clean()),
            ("Male_Adult_05", "Characters/Character2", Clean()),
            ("Delivery_Male_01", "Characters/Character3", Clean()),
            ("Construction_Male_07", "Characters/Character4", Clean()),
            ("Medical_Male_02", "Characters/Character5", Clean()),
            // Rescue-objective survivors.
            ("Female_Adult_01", "Survivors/Survivor", Clean()),
            ("Male_Adult_13", "Survivors/Survivor_1", Clean()),
            ("Business_Male_04", "Survivors/Survivor_2", Clean()),
        };

        static RbOpts _opts = new RbOpts();

        // ------------------------------------------------------------------ characters (Rocketbox)

        /// <summary>Biped bone for each game bone (index = ZB); Spine1 and the fingers simply follow their parents.</summary>
        static readonly string[] BipedFor =
        {
            null, "Bip01 Pelvis", "Bip01 Spine", "Bip01 Spine2", "Bip01 Neck", "Bip01 Head", "Bip01 MJaw",
            "Bip01 L Clavicle", "Bip01 L UpperArm", "Bip01 L Forearm", "Bip01 L Hand",
            "Bip01 R Clavicle", "Bip01 R UpperArm", "Bip01 R Forearm", "Bip01 R Hand",
            "Bip01 L Thigh", "Bip01 L Calf", "Bip01 L Foot", "Bip01 R Thigh", "Bip01 R Calf", "Bip01 R Foot",
        };

        static void BuildRocketbox(string avatar, string key, RbOpts opts)
        {
            _opts = opts;
            bool infected = opts.Infected;
            int seed = opts.Seed;
            string src = ArtImportRules.SourceRoot + "Rocketbox/" + avatar + "/";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(src + avatar + ".fbx");
            if (fbx == null) { Debug.LogWarning("[UA] ART " + avatar + " not downloaded; " + key + " keeps the procedural model"); return; }
            string gen = ArtImportRules.GeneratedRoot + "Rocketbox/" + avatar + "/";
            Directory.CreateDirectory(gen + "infected");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int r = 0; r < smrs.Length; r++)
            {
                var smr = smrs[r];
                // Bind pose, at the origin: mark the head skin under the hair cards before the textures are infected.
                _scalp = infected ? ScalpMask(smr, F("Bip01 Head"), F("Bip01 LEye"), F("Bip01 REye"), ScalpSize) : null;
                _features = infected ? FeatureMasks(smr, F, ScalpSize) : null;
                var slots = smr.sharedMaterials.Select(m => m != null ? m.name : "").ToArray();
                var keep = new List<int>();
                var mats = new List<Material>();
                for (int i = 0; i < slots.Length; i++)
                {
                    if (DropSlot(slots[i], opts)) continue;
                    keep.Add(i);
                    mats.Add(CharacterMaterial(avatar, src, gen, slots[i], infected, seed));
                }
                _scalp = null;
                _features = null;
                KeepSubmeshes(smr, keep, mats, gen + key.Replace('/', '_') + (r > 0 ? "_" + r : "") + "_mesh.asset");
                Debug.Log("[UA] ART " + key + " slots: " + string.Join(", ", slots.Select((s, i) => keep.Contains(i) ? s : "-" + s)));
            }

            var root = new GameObject("ArtBody_" + key.Replace('/', '_'));
            inst.transform.SetParent(root.transform, false);
            Transform F(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            // Face +Z (the character's left side at -X).
            if (F("Bip01 L Clavicle").position.x > 0) inst.transform.localRotation = Quaternion.Euler(0, 180, 0);
            // Lower the arms from the A-pose and straighten the legs to the game's rest pose (limbs straight down).
            foreach (var (a, b) in new[] { ("Bip01 L UpperArm", "Bip01 L Forearm"), ("Bip01 L Forearm", "Bip01 L Hand"), ("Bip01 R UpperArm", "Bip01 R Forearm"), ("Bip01 R Forearm", "Bip01 R Hand"), ("Bip01 L Thigh", "Bip01 L Calf"), ("Bip01 L Calf", "Bip01 L Foot"), ("Bip01 R Thigh", "Bip01 R Calf"), ("Bip01 R Calf", "Bip01 R Foot") })
            {
                var ta = F(a); var tb = F(b);
                ta.rotation = Quaternion.FromToRotation((tb.position - ta.position).normalized, Vector3.down) * ta.rotation;
            }
            var body = root.AddComponent<UA.Game.ArtBody>();
            int n = UA.Game.ZombieRig.BindPos.Length;
            body.Bones = new Transform[n];
            body.Rest = new Quaternion[n];
            body.Pinned = new bool[n];
            body.PinLocal = new Vector3[n];
            for (int i = 1; i < n; i++) body.Bones[i] = F(BipedFor[i]);
            for (int i = 1; i < n; i++)
            {
                var bone = body.Bones[i];
                if (bone == null) continue;
                body.Rest[i] = bone.rotation;
                int zp = (int)UA.Game.ZombieRig.ParentOf((UA.Game.ZB)i);
                if (i != (int)UA.Game.ZB.Hips && zp > 0 && body.Bones[zp] != null && bone.parent != body.Bones[zp])
                {
                    body.Pinned[i] = true;
                    body.PinLocal[i] = body.Bones[zp].InverseTransformPoint(bone.position);
                }
            }
            var pelvis = body.Bones[(int)UA.Game.ZB.Hips];
            body.HipOffset = pelvis.position - UA.Game.ZombieRig.BindPos[(int)UA.Game.ZB.Hips];
            body.EyeL = F("Bip01 LEye");
            body.EyeR = F("Bip01 REye");
            body.Renderers = smrs;
            Debug.Log("[UA] ART " + key + ": " + avatar + (infected ? " (infected)" : "") + " tris " + smrs.Sum(s => (int)(s.sharedMesh.triangles.Length / 3)) + " pelvis " + pelvis.position.ToString("F3") + " pinned " + string.Join(",", Enumerable.Range(0, n).Where(i => body.Pinned[i]).Select(i => ((UA.Game.ZB)i).ToString())) + " missing " + string.Join(",", Enumerable.Range(1, n - 1).Where(i => body.Bones[i] == null).Select(i => ((UA.Game.ZB)i).ToString())));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabRoot + key));
            SavePrefab(root, PrefabRoot + key + ".prefab");
        }

        /// <summary>
        /// Material slots left out of a prefab. Weapons: zombies don't carry guns, and playable characters hold the
        /// game's own weapon. Hair cards on infected short-haired avatars: Rocketbox lays short-hair cards out as
        /// rings and bars whose opaque cores render as solid strips (a "cage" over the head, worst under the
        /// flashlight); the scalp map carries painted hair, darkened by the hair mask. One alpha-tested draw less too.
        /// </summary>
        static bool DropSlot(string slot, RbOpts o)
        {
            string s = slot.ToLowerInvariant();
            if (o.Drop.Any(d => s.EndsWith(d))) return true;
            if (s.EndsWith("opacity")) return o.Infected && !o.KeepHairCards;
            return s.Contains("machinegun") || s.Contains("shotgun") || s.Contains("pistol") || s.Contains("rifle");
        }

        /// <summary>
        /// Points the renderer at a copy of its mesh that has only the kept submeshes (saved as an asset, CPU copy
        /// dropped at runtime), with the matching materials. Nothing is copied when every submesh is kept.
        /// </summary>
        static void KeepSubmeshes(SkinnedMeshRenderer smr, List<int> keep, List<Material> mats, string assetPath)
        {
            var src = smr.sharedMesh;
            if (keep.Count == src.subMeshCount)
            {
                smr.sharedMaterials = mats.ToArray();
                return;
            }
            var m = UnityEngine.Object.Instantiate(src);
            m.name = src.name + "_trim";
            var tris = keep.Select(i => src.GetTriangles(i)).ToArray();
            m.subMeshCount = keep.Count;
            for (int k = 0; k < keep.Count; k++) m.SetTriangles(tris[k], k, false);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(m, assetPath);
            var so = new SerializedObject(m);
            var readable = so.FindProperty("m_IsReadable");
            if (readable != null) { readable.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            smr.sharedMesh = m;
            smr.sharedMaterials = mats.ToArray();
        }

        const int ScalpSize = 1024;
        static float[] _scalp;
        static float[] _lastHairW;
        static Color32[] _lastEmit;

        /// <summary>
        /// UV-space mask (ScalpSize², bottom row first) of the head skin lying under the scalp hair cards. Rocketbox
        /// heads are skin-coloured at the crown and rely on the cards for hair; once the skin is turned pale the
        /// sparse cards read as a dark "cage" over a pale dome, so the infected scalp is painted hair-coloured there.
        /// Lashes and brows (cards at eye level, at the front) are left out.
        /// </summary>
        static float[] ScalpMask(SkinnedMeshRenderer smr, Transform headBone, Transform eyeL, Transform eyeR, int size)
        {
            var mats = smr.sharedMaterials;
            int head = Array.FindIndex(mats, m => m != null && m.name.EndsWith("head"));
            int hair = Array.FindIndex(mats, m => m != null && m.name.EndsWith("opacity"));
            if (head < 0 || hair < 0 || headBone == null || eyeL == null || eyeR == null) return null;
            var mesh = smr.sharedMesh;
            // Mesh space -> world at the bind pose (the renderer's own transform does not place skinned vertices).
            var toWorld = smr.bones[0].localToWorldMatrix * mesh.bindposes[0];
            var vs = mesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
            var uv = mesh.uv;
            var eye = (eyeL.position + eyeR.position) * 0.5f;
            var fwd = Vector3.ProjectOnPlane(eye - headBone.position, Vector3.up).normalized;
            var cards = new List<Vector3[]>();
            var ht = mesh.GetTriangles(hair);
            for (int t = 0; t < ht.Length; t += 3)
            {
                Vector3 a = vs[ht[t]], b = vs[ht[t + 1]], c = vs[ht[t + 2]], m = (a + b + c) / 3;
                if (m.y > eye.y + 0.03f || Vector3.Dot(m - eye, fwd) < -0.08f) cards.Add(new[] { a, b, c });
            }
            var tri = mesh.GetTriangles(head);
            var ns = mesh.normals.Select(nm => toWorld.MultiplyVector(nm).normalized).ToArray();
            var w = new Dictionary<int, float>();
            foreach (int i in tri.Distinct())
            {
                float best = float.MaxValue;
                foreach (var c in cards) best = Mathf.Min(best, (ClosestOnTriangle(vs[i], c[0], c[1], c[2]) - vs[i]).sqrMagnitude);
                float d = Mathf.Sqrt(best);
                float near = 1 - Mathf.Clamp01((d - 0.012f) / 0.012f);  // a card within 1.2 cm, fading out by 2.4 cm
                // Or a card straight out from the skin (cards can float a few cm off the scalp, e.g. a quiff).
                float covered = 0;
                foreach (var c in cards)
                    if (RayHitsTriangle(vs[i] + ns[i] * 0.002f, ns[i], c[0], c[1], c[2], out float t) && t < 0.07f) { covered = 1; break; }
                // And the crown: skin 6-8.5 cm above eye level is under the haircut even where no card is close
                // (vertical "fin" cards run parallel to the scalp normals).
                float crown = Mathf.Clamp01((vs[i].y - (eye.y + 0.06f)) / 0.025f);
                w[i] = Mathf.Max(near, Mathf.Max(covered, crown));
            }
            var mask = Rasterize(uv, tri, i => w[i], size);
            Debug.Log("[UA] ART scalp mask: head y " + tri.Min(i => vs[i].y).ToString("0.00") + ".." + tri.Max(i => vs[i].y).ToString("0.00") + " eye y " + eye.y.ToString("0.00") + ", " + cards.Count + "/" + ht.Length / 3 + " hair cards, " + w.Count(kv => kv.Value > 0.5f) + "/" + w.Count + " head vertices under hair, " + (mask.Count(m => m > 0.5f) * 100f / mask.Length).ToString("0.0") + "% of the head map");
            return mask;
        }

        /// <summary>
        /// Per-vertex weights of one submesh, interpolated across its triangles into UV space (size x size, bottom
        /// row first) and grown 2 px so UV island edges and their mip levels are covered.
        /// </summary>
        static float[] Rasterize(Vector2[] uv, int[] tri, Func<int, float> weight, int size)
        {
            var w = new Dictionary<int, float>();
            foreach (int i in tri.Distinct()) w[i] = weight(i);
            float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            var mask = new float[size * size];
            for (int t = 0; t < tri.Length; t += 3)
            {
                int i0 = tri[t], i1 = tri[t + 1], i2 = tri[t + 2];
                float w0 = w[i0], w1 = w[i1], w2 = w[i2];
                if (w0 <= 0 && w1 <= 0 && w2 <= 0) continue;
                Vector2 p0 = uv[i0] * size, p1 = uv[i1] * size, p2 = uv[i2] * size;
                float area = Cross(p1 - p0, p2 - p0);
                if (Mathf.Abs(area) < 1e-6f) continue;
                int x0 = Mathf.Max(0, (int)Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)) - 1), x1 = Mathf.Min(size - 1, (int)Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)) + 1);
                int y0 = Mathf.Max(0, (int)Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)) - 1), y1 = Mathf.Min(size - 1, (int)Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float b0 = Cross(p1 - p, p2 - p) / area, b1 = Cross(p2 - p, p0 - p) / area, b2 = 1 - b0 - b1;
                        if (b0 < 0 || b1 < 0 || b2 < 0) continue;
                        float val = b0 * w0 + b1 * w1 + b2 * w2;
                        int k = y * size + x;
                        if (val > mask[k]) mask[k] = val;
                    }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                var grown = (float[])mask.Clone();
                for (int y = 1; y < size - 1; y++)
                    for (int x = 1; x < size - 1; x++)
                    {
                        int k = y * size + x;
                        float m = Mathf.Max(Mathf.Max(mask[k - 1], mask[k + 1]), Mathf.Max(mask[k - size], mask[k + size]));
                        if (m > grown[k]) grown[k] = m;
                    }
                mask = grown;
            }
            return mask;
        }

        /// <summary>Where the infection goes, from the avatar's own face bones and body (UV masks, ScalpSize).</summary>
        sealed class Features
        {
            /// <summary>Head map: blood around the mouth and running over the chin; sunken eye sockets; milky eyeballs.</summary>
            public float[] Mouth, Socket, Eyeball;
            /// <summary>Body map: where blood collects on a bite victim (chest front, neck, hands and forearms).</summary>
            public float[] Blood;
        }

        static Features _features;

        static float Sample(float[] m, float u, float v) =>
            m == null ? 0 : m[Mathf.Min(ScalpSize - 1, (int)(v * ScalpSize)) * ScalpSize + Mathf.Min(ScalpSize - 1, (int)(u * ScalpSize))];

        static Features FeatureMasks(SkinnedMeshRenderer smr, Func<string, Transform> bone, int size)
        {
            var f = new Features();
            var mats = smr.sharedMaterials;
            int head = Array.FindIndex(mats, m => m != null && m.name.EndsWith("head"));
            int body = Array.FindIndex(mats, m => m != null && m.name.EndsWith("body"));
            Transform eL = bone("Bip01 LEye"), eR = bone("Bip01 REye"), lipU = bone("Bip01 MUpperLip"), lipD = bone("Bip01 MBottomLip"), headB = bone("Bip01 Head");
            if (eL == null || eR == null || headB == null) return f;
            var mesh = smr.sharedMesh;
            var toWorld = smr.bones[0].localToWorldMatrix * mesh.bindposes[0];
            var vs = mesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
            var ns = mesh.normals.Select(n => toWorld.MultiplyVector(n).normalized).ToArray();
            var uv = mesh.uv;
            var fwd = Vector3.ProjectOnPlane((eL.position + eR.position) * 0.5f - headB.position, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, fwd);
            float EyeDist(Vector3 p) => Mathf.Min(Vector3.Distance(p, eL.position), Vector3.Distance(p, eR.position));
            if (head >= 0 && lipU != null && lipD != null)
            {
                var tri = mesh.GetTriangles(head);
                var mouth = (lipU.position + lipD.position) * 0.5f;
                f.Mouth = Rasterize(uv, tri, i =>
                {
                    var p = vs[i];
                    float blob = 1 - Noise.Smooth(0.022f, 0.05f, Vector3.Distance(p, mouth));
                    float below = mouth.y - p.y;
                    float lateral = Mathf.Abs(Vector3.Dot(p - mouth, side));
                    float drip = (1 - Noise.Smooth(0.014f, 0.034f, lateral)) * Noise.Smooth(-0.01f, 0.01f, below) * (1 - Noise.Smooth(0.07f, 0.11f, below))
                        * Mathf.Clamp01(Vector3.Dot(ns[i], fwd) * 2);
                    return Mathf.Max(blob, drip * 0.85f);
                }, size);
                f.Socket = Rasterize(uv, tri, i => { float d = EyeDist(vs[i]); return Noise.Smooth(0.011f, 0.017f, d) * (1 - Noise.Smooth(0.026f, 0.042f, d)); }, size);
                // Eyeballs are ~1.2 cm in radius around the eye bone; eyelid skin starts just outside.
                f.Eyeball = Rasterize(uv, tri, i => 1 - Noise.Smooth(0.0122f, 0.0135f, EyeDist(vs[i])), size);
            }
            if (body >= 0)
            {
                var tri = mesh.GetTriangles(body);
                Transform hl = bone("Bip01 L Hand"), hr = bone("Bip01 R Hand");
                f.Blood = Rasterize(uv, tri, i =>
                {
                    var p = vs[i];
                    float front = Mathf.Clamp01(Vector3.Dot(ns[i], fwd) * 1.5f + 0.2f);
                    float chest = Noise.Smooth(1.0f, 1.25f, p.y) * (1 - Noise.Smooth(1.52f, 1.62f, p.y)) * front;
                    float hands = 0;
                    if (hl != null) hands = Mathf.Max(hands, 1 - Noise.Smooth(0.1f, 0.32f, Vector3.Distance(p, hl.position)));
                    if (hr != null) hands = Mathf.Max(hands, 1 - Noise.Smooth(0.1f, 0.32f, Vector3.Distance(p, hr.position)));
                    return Mathf.Max(chest, hands);
                }, size);
            }
            return f;
        }

        /// <summary>Two-sided ray / triangle intersection (Möller–Trumbore); t is the distance along unit dir.</summary>
        static bool RayHitsTriangle(Vector3 o, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-10f) return false;
            float inv = 1 / det;
            Vector3 s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t > 0;
        }

        /// <summary>Closest point to p on triangle abc (Ericson, Real-Time Collision Detection 5.1.5).</summary>
        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1 / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        /// <summary>URP material for one Rocketbox material slot (color / normal / specular TGAs, or the opacity hair cards).</summary>
        static Material CharacterMaterial(string avatar, string src, string gen, string slot, bool infected, int seed)
        {
            string matPath = MaterialRoot + "RB_" + avatar + "_" + slot + (infected ? "_inf" : "") + ".mat";
            var files = Directory.GetFiles(src).Select(Path.GetFileName).ToArray();
            string Find(string suffix) => files.FirstOrDefault(f => f.StartsWith(slot + suffix, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".tga", StringComparison.OrdinalIgnoreCase));
            bool hair = slot.EndsWith("opacity");
            string colorFile = Find(hair ? "_color" : "_color");
            string kind = hair ? "hair" : slot.EndsWith("head") ? "head" : slot.EndsWith("body") ? "body" : "gear";
            string basePath = src + colorFile;
            _lastHairW = null;
            _lastEmit = null;
            string emitPath = null;
            if (infected)
            {
                basePath = gen + "infected/" + slot + "_inf_color.png";
                var px = Half(ReadTga(src + colorFile, out int w, out int h), ref w, ref h);
                WritePng(basePath, Infect(px, w, h, kind, seed + slot.GetHashCode() % 97), w, h);
                if (_lastEmit != null)
                {
                    emitPath = gen + "infected/" + slot + "_inf_emit.png";
                    WritePng(emitPath, _lastEmit, w, h);
                    _lastEmit = null;
                }
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 1f);
            if (hair)
            {
                // Hair, brows and lashes are alpha-tested cards, visible from both sides.
                m.SetFloat("_AlphaClip", 1);
                m.SetFloat("_Cutoff", 0.3f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0);
                m.SetFloat("_Smoothness", 0.2f);
                m.SetFloat("_Metallic", 0f);
                // Cards are mostly seen edge-on, and back faces keep the front normal (N.V = 0), so Fresnel sheen and
                // highlights turned them into pale-blue bars over the head. Hair is lit diffusely instead.
                m.SetFloat("_SpecularHighlights", 0);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                string nor = Find("_normal");
                if (nor != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(src + nor)); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                string spec = Find("_specular");
                if (spec != null)
                {
                    // Rocketbox specular (greyscale) -> URP mask: metallic 0, occlusion 1, smoothness from the specular level.
                    // Infected heads: the scalp under the hair is matte (a glossy dark scalp showed Fresnel streaks).
                    var hairW = _lastHairW;
                    string maskPath = gen + slot + (hairW != null ? "_inf" : "") + "_mask.png";
                    var sp = Half(ReadTga(src + spec, out int w, out int h), ref w, ref h);
                    var o = new Color32[sp.Length];
                    for (int i = 0; i < sp.Length; i++)
                    {
                        // Infected heads are a little less oily (a bright forehead hotspot under the flashlight).
                        float a = hairW != null ? Mathf.Clamp(36 + sp[i].r * 0.9f, 0, 170) : Mathf.Clamp(41 + sp[i].r * 1.5f, 0, 235);
                        if (hairW != null && hairW.Length == sp.Length) a = Mathf.Lerp(a, 28, hairW[i]);
                        o[i] = new Color32(0, 255, 0, (byte)a);
                    }
                    WritePng(maskPath, o, w, h);
                    m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_SmoothnessTextureChannel", 0);
                    m.SetFloat("_Smoothness", 1f);
                }
                else m.SetFloat("_Smoothness", 0.3f);
            }
            if (emitPath != null)
            {
                m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emitPath));
                m.SetColor("_EmissionColor", _opts.Glow);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ texture lab

        /// <summary>Uncompressed (type 2) TGA reader, 24/32-bit, either row order; returns Unity order (bottom row first).</summary>
        static Color32[] ReadTga(string path, out int w, out int h)
        {
            var b = File.ReadAllBytes(path);
            int idLen = b[0], cmapType = b[1], type = b[2];
            if (type != 2) throw new Exception("Unsupported TGA type " + type + ": " + path);
            int cmapLen = b[5] | b[6] << 8, cmapBits = b[7];
            w = b[12] | b[13] << 8;
            h = b[14] | b[15] << 8;
            int bytes = b[16] / 8;
            bool topDown = (b[17] & 0x20) != 0;
            int off = 18 + idLen + (cmapType != 0 ? cmapLen * ((cmapBits + 7) / 8) : 0);
            var px = new Color32[w * h];
            int W = w, H = h;
            System.Threading.Tasks.Parallel.For(0, H, y =>
            {
                int sy = topDown ? H - 1 - y : y;
                for (int x = 0; x < W; x++)
                {
                    int p = off + (sy * W + x) * bytes;
                    px[y * W + x] = new Color32(b[p + 2], b[p + 1], b[p], bytes == 4 ? b[p + 3] : (byte)255);
                }
            });
            return px;
        }

        /// <summary>Box-filters to half size when larger than 1024 (the in-game texture size).</summary>
        static Color32[] Half(Color32[] px, ref int w, ref int h)
        {
            if (w <= 1024 && h <= 1024) return px;
            int nw = w / 2, nh = h / 2, ow = w;
            var o = new Color32[nw * nh];
            System.Threading.Tasks.Parallel.For(0, nh, y =>
            {
                for (int x = 0; x < nw; x++)
                {
                    Color32 a = px[(2 * y) * ow + 2 * x], b = px[(2 * y) * ow + 2 * x + 1], c = px[(2 * y + 1) * ow + 2 * x], d = px[(2 * y + 1) * ow + 2 * x + 1];
                    o[y * nw + x] = new Color32((byte)((a.r + b.r + c.r + d.r) / 4), (byte)((a.g + b.g + c.g + d.g) / 4), (byte)((a.b + b.b + c.b + d.b) / 4), (byte)((a.a + b.a + c.a + d.a) / 4));
                }
            });
            w = nw; h = nh;
            return o;
        }

        static void WritePng(string path, Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static bool IsSkin(float h, float s, float v) => (h < 0.11f || h > 0.95f) && s > 0.14f && s < 0.75f && v > 0.28f;

        /// <summary>
        /// 0..1 "painted hair" weight per pixel: the share of dark pixels (V &lt; 0.42) in a window of ~1/40 of the
        /// texture, ignoring the black background outside the UV islands. Hair regions are mostly dark, faces are not;
        /// eyebrows also count as hair (they should stay dark).
        /// </summary>
        static float[] HairWeight(Color32[] px, int w, int h)
        {
            int R = Mathf.Max(4, w / 80), W1 = w + 1;
            var dark = new int[W1 * (h + 1)];
            var valid = new int[W1 * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                int rowD = 0, rowV = 0;
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (mx >= 8) { rowV++; if (mx < 107) rowD++; }
                    int k = (y + 1) * W1 + x + 1;
                    dark[k] = dark[k - W1] + rowD;
                    valid[k] = valid[k - W1] + rowV;
                }
            }
            var o = new float[w * h];
            System.Threading.Tasks.Parallel.For(0, h, y =>
            {
                int y0 = Mathf.Max(0, y - R), y1 = Mathf.Min(h, y + R + 1);
                for (int x = 0; x < w; x++)
                {
                    int x0 = Mathf.Max(0, x - R), x1 = Mathf.Min(w, x + R + 1);
                    int a = y0 * W1 + x0, b = y0 * W1 + x1, c = y1 * W1 + x0, d = y1 * W1 + x1;
                    int nv = valid[d] - valid[b] - valid[c] + valid[a];
                    int nd = dark[d] - dark[b] - dark[c] + dark[a];
                    float dens = nv > 0 ? (float)nd / nv : 0;
                    float t = Mathf.Clamp01((dens - 0.3f) / 0.3f);
                    o[y * w + x] = t * t * (3 - 2 * t);
                }
            });
            return o;
        }

        /// <summary>
        /// Infected version of a colour map. Skin: pale grey-green, mottled, with veins, bruises and wounds.
        /// Clothes and gear: desaturated, grimy, soaked and spattered with blood. Eyes yellowed, hair greasy.
        /// </summary>
        static Color32[] Infect(Color32[] px, int w, int h, string kind, int seed)
        {
            var o = new Color32[px.Length];
            // Head maps paint short hair on the scalp under the hair cards. Classify by neighbourhood, not per pixel,
            // so the lighter strands inside the hair stay hair (per-pixel, they turned into pale skin and the scalp
            // showed through the cards as a pale "cage").
            float[] hairW = kind == "head" ? HairWeight(px, w, h) : null;
            _lastHairW = hairW;  // the smoothness mask built next for this slot makes these areas matte
            // Glowing veins (Toxic): an emission map, white where veins run over skin, written next to the colour map.
            bool glow = _opts.Glow.maxColorComponent > 0.01f && (kind == "head" || kind == "body");
            var emit = glow ? new Color32[px.Length] : null;
            _lastEmit = emit;
            float blood = _opts.Blood, cloth = _opts.ClothDark, hairDark = _opts.HairDark;
            System.Threading.Tasks.Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = px[i];
                    // Black background between the UV islands stays black (it bleeds into seams through the mips).
                    if (kind != "hair" && Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) < 8) { o[i] = px[i]; continue; }
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color.RGBToHSV(c, out float H, out float S, out float V);
                    Color n;
                    if (kind == "head" && _scalp != null)
                    {
                        float sc = _scalp[Mathf.Min(ScalpSize - 1, (int)(v * ScalpSize)) * ScalpSize + Mathf.Min(ScalpSize - 1, (int)(u * ScalpSize))];
                        if (sc > hairW[i]) hairW[i] = sc;
                    }
                    if (kind == "hair")
                    {
                        float g = c.grayscale;
                        n = Color.Lerp(c, new Color(g, g, g), 0.5f) * 0.65f * hairDark;
                    }
                    else if (kind == "head" && hairW[i] < 0.5f && S < 0.13f && V > 0.6f)
                        n = Color.Lerp(c, new Color(0.78f, 0.72f, 0.5f), 0.65f);                       // sclera: yellowed
                    else if (kind == "head")
                    {
                        // Skin, blended into greasy dark hair (same treatment as the hair cards) by the hair weight.
                        float g = c.grayscale;
                        var hair = Color.Lerp(c, new Color(g, g, g), 0.5f) * 0.62f * hairDark;
                        var skin = hairW[i] > 0.99f ? hair : Bloody(Skinify(c, u, v, seed, 0.07f), u, v, x, y, seed, 0.18f * blood);
                        n = Color.Lerp(skin, hair, hairW[i]);
                        if (emit != null) emit[i] = Vein(u, v, seed, 1 - hairW[i]);
                    }
                    else if (kind == "body" && IsSkin(H, S, V))
                    {
                        float bm = _features != null && _features.Blood != null ? Sample(_features.Blood, u, v) : 0.6f;
                        n = Bloody(Skinify(c, u, v, seed, 0f), u, v, x, y, seed, 0.8f * blood * (0.15f + 1.1f * bm));
                        if (emit != null) emit[i] = Vein(u, v, seed, 1);
                    }
                    else
                    {
                        // Clothes and gear: desaturated, grimy, and bloodied where a bite victim bleeds (chest, hands),
                        // only lightly elsewhere (light trousers no longer come out soaked).
                        float g = c.grayscale;
                        n = Color.Lerp(c, new Color(g, g, g), 0.3f) * 0.82f * cloth;
                        float grime = Noise.Smooth(0.45f, 0.85f, Noise.Fbm(u, v, 5, 4, seed + 3));
                        n = Color.Lerp(n, new Color(0.16f, 0.13f, 0.09f) * cloth, grime * 0.45f);
                        float bm = kind == "body" && _features != null && _features.Blood != null ? Sample(_features.Blood, u, v) : 0.5f;
                        n = Bloody(n, u, v, x, y, seed, blood * (0.12f + 1.1f * bm));
                    }
                    if (kind == "head" && _features != null)
                    {
                        // Sunken, bruised eye sockets; milky eyes; blood around the mouth and down the chin.
                        float sock = Sample(_features.Socket, u, v), eye = Sample(_features.Eyeball, u, v), mouth = Sample(_features.Mouth, u, v);
                        if (sock > 0) n = Color.Lerp(n * (1 - sock * 0.45f), new Color(0.24f, 0.12f, 0.16f), sock * 0.25f);
                        if (eye > 0) n = Color.Lerp(n, new Color(0.78f, 0.76f, 0.66f), eye * 0.8f);
                        if (mouth > 0)
                        {
                            var b = Color.Lerp(new Color(0.36f, 0.03f, 0.02f), new Color(0.13f, 0.02f, 0.02f), Noise.Fbm(u, v, 30, 2, seed + 51));
                            n = Color.Lerp(n, b, mouth * (0.6f + 0.35f * Noise.Fbm(u, v, 18, 3, seed + 53)));
                        }
                    }
                    var r = (Color32)n;
                    r.a = px[i].a;
                    o[i] = r;
                }
            });
            return o;
        }

        /// <param name="woundBias">Raises the wound threshold (fewer, smaller wounds, used on faces).</param>
        static Color Skinify(Color c, float u, float v, int seed, float woundBias)
        {
            float lum = c.grayscale;
            float mott = Noise.Fbm(u, v, 9, 4, seed);
            var pale = new Color(0.58f, 0.62f, 0.52f) * _opts.SkinTint * (0.5f + lum * 0.95f) * (0.85f + mott * 0.3f);
            // Faces keep more of their original detail (features stay readable).
            var n = Color.Lerp(pale, c * new Color(0.8f, 0.82f, 0.74f), 0.22f + woundBias * 2f);
            // Thin dark veins (contour lines of a noise field, in patches), then bruising.
            float vf = Mathf.Abs(Noise.Fbm(u, v, 8, 4, seed + 3) - 0.5f);
            float veins = (1 - Noise.Smooth(0.004f, 0.014f, vf)) * Noise.Smooth(0.42f, 0.62f, Noise.Fbm(u, v, 3, 3, seed + 13));
            n = Color.Lerp(n, new Color(0.22f, 0.2f, 0.34f), veins * 0.55f);
            n = Color.Lerp(n, new Color(0.3f, 0.21f, 0.28f), Noise.Smooth(0.6f, 0.82f, Noise.Fbm(u, v, 3, 4, seed + 7)) * 0.4f);
            float wound = Noise.Fbm(u, v, 6, 3, seed + 19);
            n = Color.Lerp(n, new Color(0.45f, 0.06f, 0.05f), Noise.Smooth(0.79f + woundBias, 0.86f + woundBias, wound));
            n = Color.Lerp(n, new Color(0.2f, 0.035f, 0.03f), Noise.Smooth(0.86f + woundBias, 0.92f + woundBias, wound));
            return n;
        }

        /// <summary>
        /// Emission for glowing veins: thin contour lines of two noise fields (value noise clusters around 0.5, so
        /// ridge-style thresholds lit most of the skin), strongest in a few spreading patches.
        /// </summary>
        static Color32 Vein(float u, float v, int seed, float weight)
        {
            float a = Mathf.Abs(Noise.Fbm(u, v, 7, 4, seed + 29) - 0.5f);
            float b = Mathf.Abs(Noise.Fbm(u, v, 17, 3, seed + 37) - 0.5f);
            float line = Mathf.Max(1 - Noise.Smooth(0.004f, 0.012f, a), (1 - Noise.Smooth(0.003f, 0.008f, b)) * 0.6f);
            float patch = Noise.Smooth(0.47f, 0.66f, Noise.Fbm(u, v, 3, 3, seed + 9));
            byte e = (byte)(Mathf.Clamp01(line * (0.12f + 0.88f * patch) * weight) * 255);
            return new Color32(e, e, e, 255);
        }

        static Color Bloody(Color n, float u, float v, int x, int y, int seed, float amount)
        {
            var blood = Color.Lerp(new Color(0.34f, 0.02f, 0.015f), new Color(0.12f, 0.03f, 0.02f), Noise.Fbm(u, v, 8, 2, seed + 31));
            float stain = Noise.Smooth(0.56f, 0.66f, Noise.Fbm(u, v * 1.3f, 3, 5, seed + 11));
            // Droplets cluster in a few spray zones (an even scatter over the whole map read as spots, not blood).
            float zone = Noise.Smooth(0.58f, 0.78f, Noise.Fbm(u, v, 2, 3, seed + 41));
            float spatter = zone > 0 && Noise.Hash(x / 3, y / 3, seed + 23) > 1 - 0.07f * zone ? 0.85f : 0f;
            return Color.Lerp(n, blood, Mathf.Clamp01((stain + spatter) * amount) * 0.88f);
        }

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// URP Lit material from Poly Haven maps. The ARM map (R = AO, G = roughness, B = metal) is converted
        /// into URP's layout: R = metallic, G = occlusion, A = smoothness (one texture feeds both slots).
        /// </summary>
        static Material PbrMaterial(string name, string diff, string nor, string arm, float smoothness = 0.9f)
        {
            string maskPath = arm.Replace("_arm_", "_urpmask_");
            if (!maskPath.EndsWith(".png")) maskPath = Path.ChangeExtension(maskPath, ".png");
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            src.LoadImage(File.ReadAllBytes(arm));
            var px = src.GetPixels32();
            var o = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) o[i] = new Color32(px[i].b, px[i].r, 0, (byte)(255 - px[i].g));
            var mask = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
            mask.SetPixels32(o);
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);

            string matPath = MaterialRoot + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            m.SetTexture("_MetallicGlossMap", maskTex);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_SmoothnessTextureChannel", 0);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 1f);
            m.SetTexture("_OcclusionMap", maskTex);
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetFloat("_OcclusionStrength", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ weapons

        /// <summary>
        /// Poly Haven "Service Pistol" (CC0) as the P9 Sentinel (StandardPistol). The source file holds two
        /// laid-out copies and two magazines; one pistol plus the empty magazine (seated in the grip) is used.
        /// Anchors are measured from the mesh: bore height at the muzzle, grip line, slide top.
        /// </summary>
        static void BuildServicePistol()
        {
            const string src = ArtImportRules.SourceRoot + "PolyHaven/service_pistol/";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(src + "service_pistol_1k.fbx");
            if (fbx == null) { Debug.LogWarning("[UA] ART service pistol not imported"); return; }
            var mat = PbrMaterial("ServicePistol", src + "service_pistol_diff_1k.jpg", src + "service_pistol_nor_gl_1k.png", src + "service_pistol_arm_1k.png");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            string[] keep = { "service_pistol_pistol_a", "service_pistol_slide_a", "service_pistol_hammer_a", "service_pistol_trigger_a", "service_pistol_magazine_empty" };
            foreach (var child in inst.transform.Cast<Transform>().ToArray())
                if (Array.IndexOf(keep, child.name) < 0) UnityEngine.Object.DestroyImmediate(child.gameObject);
            Transform Part(string n) => inst.transform.Find(n);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            var frame = Part("service_pistol_pistol_a");
            var slide = Part("service_pistol_slide_a");
            var trigger = Part("service_pistol_trigger_a");
            var mag = Part("service_pistol_magazine_empty");
            var fb = frame.GetComponent<Renderer>().bounds;
            var sb = slide.GetComponent<Renderer>().bounds;
            var tb = trigger.GetComponent<Renderer>().bounds;
            var verts = WorldVerts(frame);
            // Bore height: the frontmost centimetre of the frame. Grip line: horizontal slices below the trigger guard.
            float boreY = MeanY(verts, v => v.x < fb.min.x + 0.008f, sb.center.y);
            float lowY = fb.min.y + 0.012f, midY = fb.min.y + 0.03f;
            float gripLowX = MeanX(verts, v => Mathf.Abs(v.y - lowY) < 0.006f, tb.center.x + 0.06f);
            float gripMidX = MeanX(verts, v => Mathf.Abs(v.y - midY) < 0.006f && v.x > tb.center.x, tb.center.x + 0.06f);
            // Seat the magazine in the grip: base plate just below the grip, centred on the grip line.
            var mb = mag.GetComponent<Renderer>().bounds;
            float magCy = fb.min.y - 0.004f + mb.size.y * 0.5f;
            float magCx = Mathf.LerpUnclamped(gripLowX, gripMidX, (magCy - lowY) / (midY - lowY));
            mag.position += new Vector3(magCx, magCy, fb.center.z) - mb.center;
            Debug.Log("[UA] ART pistol: frame " + fb.min.ToString("F3") + ".." + fb.max.ToString("F3") + " slide " + sb.min.ToString("F3") + ".." + sb.max.ToString("F3") + " boreY " + boreY.ToString("F3") + " grip x " + gripLowX.ToString("F3") + "/" + gripMidX.ToString("F3") + " mag c " + new Vector2(magCx, magCy).ToString("F3"));

            // Gun space: muzzle along +Z (the model points down -X), origin just behind the trigger, slide top at y 0.043.
            var p0 = new Vector3(tb.center.x + 0.008f, sb.max.y - 0.043f, fb.center.z);
            var root = new GameObject("ArtGun_StandardPistol");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localRotation = Quaternion.Euler(0, 90, 0);
            model.localPosition = -(model.localRotation * p0);
            inst.transform.SetParent(model, false);
            Vector3 G(float x, float y, float z) => root.transform.InverseTransformPoint(model.TransformPoint(new Vector3(x, y, z)));
            var rig = root.AddComponent<UA.Game.ArtGunRig>();
            rig.Slide = Pivot(root.transform, "Slide", slide);
            rig.Mag = Pivot(root.transform, "Mag", mag);
            rig.Muzzle = Anchor(root.transform, "Muzzle", G(fb.min.x - 0.002f, boreY, fb.center.z));
            rig.Sight = Anchor(root.transform, "Sight", new Vector3(0, G(0, sb.max.y + 0.002f, 0).y, 0));
            rig.GripR = Anchor(root.transform, "GripR", G((gripMidX + gripLowX) * 0.5f, (midY + lowY) * 0.5f + 0.005f, fb.center.z));
            rig.GripL = Anchor(root.transform, "GripL", G(gripLowX - 0.01f, lowY, fb.center.z));
            rig.MagWell = Anchor(root.transform, "MagWell", G(gripLowX, fb.min.y, fb.center.z));
            rig.Eject = Anchor(root.transform, "Eject", G(sb.center.x, sb.max.y - 0.01f, sb.max.z));
            rig.Length = rig.Muzzle.localPosition.z;
            Debug.Log("[UA] ART pistol anchors: muzzle " + rig.Muzzle.localPosition.ToString("F3") + " sightY " + rig.Sight.localPosition.y.ToString("F3") + " gripR " + rig.GripR.localPosition.ToString("F3") + " magwell " + rig.MagWell.localPosition.ToString("F3") + " tris " + Tris(root));
            SavePrefab(root, PrefabRoot + "Weapons/StandardPistol.prefab");
        }

        /// <summary>
        /// Poly Haven "Bolt Action Rifle 7.62" (CC0, Mateusz Sadek) as the Longreach SR-7 (TacticalSniper): wooden
        /// furniture with a cloth-wrapped stock, side-mounted PU scope (the weapon's built-in scope) and a bolt that
        /// the existing bolt-cycling animation moves. Measured into the procedural sniper's gun space: bore at
        /// y 0.02, origin at the trigger, muzzle along +Z.
        /// </summary>
        static void BuildBoltRifle()
        {
            const string src = ArtImportRules.SourceRoot + "PolyHaven/bolt_action_rifle_7_62/";
            const string pre = src + "bolt_action_rifle_7_62_";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(pre + "1k.fbx");
            if (fbx == null) { Debug.LogWarning("[UA] ART bolt-action rifle not downloaded; TacticalSniper keeps the procedural model"); return; }
            var main = PbrMaterial("BoltRifle", pre + "diff_1k.jpg", pre + "nor_gl_1k.png", pre + "arm_1k.png");
            var acc = PbrMaterial("BoltRifleAccessories", pre + "accesories_diff_1k.png", pre + "accesories_nor_gl_1k.png", pre + "accesories_arm_1k.png");
            // Cloth wrap: accessories colour with Poly Haven's alpha map, alpha-tested so the frayed edges cut out.
            string wrapTex = ArtImportRules.GeneratedRoot + "PolyHaven/bolt_action_rifle_7_62_wrap_diff.png";
            Directory.CreateDirectory(Path.GetDirectoryName(wrapTex));
            {
                var c = new Texture2D(2, 2); c.LoadImage(File.ReadAllBytes(pre + "accesories_diff_1k.png"));
                var a = new Texture2D(2, 2); a.LoadImage(File.ReadAllBytes(pre + "accesories_alpha_1k.png"));
                var px = c.GetPixels32();
                var ap = a.width == c.width && a.height == c.height ? a.GetPixels32() : null;
                if (ap != null) for (int i = 0; i < px.Length; i++) px[i].a = ap[i].r;
                WritePng(wrapTex, px, c.width, c.height);
                UnityEngine.Object.DestroyImmediate(c); UnityEngine.Object.DestroyImmediate(a);
            }
            var wrap = new Material(acc) { name = "BoltRifleWrap" };
            wrap.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(wrapTex));
            wrap.SetFloat("_AlphaClip", 1); wrap.SetFloat("_Cutoff", 0.5f); wrap.EnableKeyword("_ALPHATEST_ON"); wrap.SetFloat("_Cull", 0);
            wrap.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            SaveMaterial(wrap, MaterialRoot + "BoltRifleWrap.mat");
            var glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BoltRifleGlass" };
            glass.SetColor("_BaseColor", new Color(0.04f, 0.06f, 0.07f));
            glass.SetFloat("_Smoothness", 0.96f);
            glass.SetFloat("_Metallic", 0.1f);
            SaveMaterial(glass, MaterialRoot + "BoltRifleGlass.mat");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Transform Part(string n) => inst.transform.Find("bolt_action_rifle_7_62" + n);
            var loose = Part("_bullet_54mm");
            if (loose != null) UnityEngine.Object.DestroyImmediate(loose.gameObject);  // display cartridge beside the rifle
            var body = Part("");
            var boltA = Part("_bolt_a");
            var boltB = Part("_bolt_b");
            var scope = Part("_scope");
            var trigger = Part("_trigger");
            var wrapPart = Part("_wrap");
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = main;
            if (wrapPart != null) wrapPart.GetComponent<Renderer>().sharedMaterial = wrap;
            if (scope != null) scope.GetComponent<Renderer>().sharedMaterials = new[] { acc, glass };

            var bb = body.GetComponent<Renderer>().bounds;
            var tb = trigger.GetComponent<Renderer>().bounds;
            var sb = scope.GetComponent<Renderer>().bounds;
            var boltB_b = boltB.GetComponent<Renderer>().bounds;
            var verts = WorldVerts(body);
            // The rifle points down -X. Bore: centre height of the front centimetre of the barrel. Forestock: its
            // underside a little ahead of the receiver (support hand). Wrist: behind the trigger guard.
            float boreY = MeanY(verts, v => v.x < bb.min.x + 0.01f, bb.center.y);
            // Underside profile from the trigger to the muzzle: the forestock ends where the underside rises to the
            // barrel. The support hand goes 70% of the way along it (at most 30 cm ahead of the trigger).
            // Rays cast up through the actual triangles (long stock and barrel faces have vertices only at their ends).
            var bodyTris = body.GetComponent<MeshFilter>().sharedMesh.triangles;
            float Under(float x)
            {
                var o = new Vector3(x, bb.min.y - 0.05f, bb.center.z);
                float best = float.MaxValue;
                for (int t = 0; t < bodyTris.Length; t += 3)
                    if (RayHitsTriangle(o, Vector3.up, verts[bodyTris[t]], verts[bodyTris[t + 1]], verts[bodyTris[t + 2]], out float d) && d < best) best = d;
                return best < float.MaxValue ? o.y + best : boreY;
            }
            float stockFront = tb.center.x;
            var profile = new StringBuilder();
            for (float x = tb.center.x; x > bb.min.x; x -= 0.05f)
            {
                float u = Under(x);
                profile.Append((tb.center.x - x).ToString("0.00") + ":" + (u - boreY).ToString("0.000") + " ");
                if (u < boreY - 0.025f) stockFront = x;
            }
            float foreX = tb.center.x - Mathf.Min(0.3f, 0.7f * (tb.center.x - stockFront));
            float foreBottom = Under(foreX);
            Debug.Log("[UA] ART rifle underside (distance ahead of trigger : depth below bore) " + profile + "| stock ends " + (tb.center.x - stockFront).ToString("0.00") + " m ahead");
            float wristX = tb.center.x + 0.07f;
            float wristBottom = verts.Where(v => Mathf.Abs(v.x - wristX) < 0.015f).Select(v => v.y).DefaultIfEmpty(boreY - 0.07f).Min();
            float magX = tb.center.x - 0.08f;
            float magBottom = verts.Where(v => Mathf.Abs(v.x - magX) < 0.02f).Select(v => v.y).DefaultIfEmpty(boreY - 0.06f).Min();

            // Gun space: +Z = muzzle, origin under the bolt at the trigger, bore at y 0.02.
            var p0 = new Vector3(tb.center.x, boreY - 0.02f, bb.center.z);
            var root = new GameObject("ArtGun_TacticalSniper");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localRotation = Quaternion.Euler(0, 90, 0);
            model.localPosition = -(model.localRotation * p0);
            inst.transform.SetParent(model, false);
            Vector3 G(float x, float y, float z) => root.transform.InverseTransformPoint(model.TransformPoint(new Vector3(x, y, z)));
            var rig = root.AddComponent<UA.Game.ArtGunRig>();
            // Bolt pivot on the bolt's own axis, so the handle lifts and draws back like the procedural bolt.
            var boltPivot = Anchor(root.transform, "Bolt", G(boltB_b.center.x, boltB_b.center.y, boltB_b.center.z));
            boltA.SetParent(boltPivot, true);
            boltB.SetParent(boltPivot, true);
            rig.Bolt = boltPivot;
            rig.Muzzle = Anchor(root.transform, "Muzzle", G(bb.min.x - 0.002f, boreY, bb.center.z));
            rig.Sight = Anchor(root.transform, "Sight", new Vector3(0, G(0, sb.center.y, 0).y, 0));
            rig.GripR = Anchor(root.transform, "GripR", G(wristX, wristBottom + 0.035f, bb.center.z));
            rig.GripL = Anchor(root.transform, "GripL", G(foreX, foreBottom + 0.012f, bb.center.z));
            rig.MagWell = Anchor(root.transform, "MagWell", G(magX, magBottom, bb.center.z));
            rig.Eject = Anchor(root.transform, "Eject", G(boltB_b.center.x, boltB_b.max.y, bb.center.z + 0.015f));
            rig.HasScope = true;
            rig.Length = rig.Muzzle.localPosition.z;
            Debug.Log("[UA] ART rifle anchors: muzzle " + rig.Muzzle.localPosition.ToString("F3") + " sightY " + rig.Sight.localPosition.y.ToString("F3") + " gripR " + rig.GripR.localPosition.ToString("F3") + " gripL " + rig.GripL.localPosition.ToString("F3") + " magwell " + rig.MagWell.localPosition.ToString("F3") + " bolt " + boltPivot.localPosition.ToString("F3") + " tris " + Tris(root));
            SavePrefab(root, PrefabRoot + "Weapons/TacticalSniper.prefab");
        }

        /// <summary>
        /// A single-material Poly Haven prop as a prefab: uniformly scaled so its largest horizontal side is
        /// <paramref name="size"/> metres, bottom centre at the origin. Used for pickups.
        /// </summary>
        static void BuildProp(string asset, string key, float size)
        {
            string src = ArtImportRules.SourceRoot + "PolyHaven/" + asset + "/";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(src + asset + "_1k.fbx");
            if (fbx == null) { Debug.LogWarning("[UA] ART " + asset + " not downloaded; " + key + " keeps the procedural model"); return; }
            var mat = PbrMaterial("Prop_" + asset, src + asset + "_diff_1k.jpg", src + asset + "_nor_gl_1k.png", src + asset + "_arm_1k.png");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.sharedMaterial = mat;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float s = size / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
            var root = new GameObject("Item_" + key.Replace('/', '_'));
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localScale = Vector3.one * s;
            model.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * s;
            inst.transform.SetParent(model, false);
            Debug.Log("[UA] ART " + key + ": " + asset + " " + b.size.ToString("F3") + " m -> scale " + s.ToString("F3") + ", tris " + Tris(root));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabRoot + key));
            SavePrefab(root, PrefabRoot + key + ".prefab");
        }

        static void SaveMaterial(Material m, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }

        // ------------------------------------------------------------------ helpers

        static Transform Anchor(Transform root, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = local;
            return t;
        }

        /// <summary>Identity-rotation pivot under the root holding a moving part (keeps its world pose).</summary>
        static Transform Pivot(Transform root, string name, Transform part)
        {
            var p = Anchor(root, name, Vector3.zero);
            part.SetParent(p, true);
            return p;
        }

        static Vector3[] WorldVerts(Transform t)
        {
            var mesh = t.GetComponent<MeshFilter>().sharedMesh;
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var v = new Unity.Collections.NativeArray<Vector3>(mesh.vertexCount, Unity.Collections.Allocator.Temp);
                data[0].GetVertices(v);
                var m = t.localToWorldMatrix;
                var o = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) o[i] = m.MultiplyPoint3x4(v[i]);
                v.Dispose();
                return o;
            }
        }

        static float MeanX(Vector3[] v, Func<Vector3, bool> where, float fallback)
        {
            var s = v.Where(where).ToArray();
            return s.Length > 0 ? s.Average(p => p.x) : fallback;
        }

        static float MeanY(Vector3[] v, Func<Vector3, bool> where, float fallback)
        {
            var s = v.Where(where).ToArray();
            return s.Length > 0 ? s.Average(p => p.y) : fallback;
        }

        static int Tris(GameObject go) => go.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh != null ? (int)f.sharedMesh.GetIndexCount(0) / 3 : 0);

        static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("[UA] ART prefab " + path + (ok ? " saved" : " FAILED"));
            if (!ok) throw new Exception("Prefab save failed: " + path);
        }

        public static void CliPreview() => Run(Preview);

        /// <summary>
        /// Renders each imported character prefab in its rest pose (head front / top / back / side, and the full body)
        /// to Builds/art_preview, for checking textures and hair without building a player.
        /// </summary>
        [MenuItem("UA/Art/Render previews")]
        public static void Preview()
        {
            const string outDir = "Builds/art_preview/";
            Directory.CreateDirectory(outDir);
            // Compile shader variants before drawing; otherwise the first frames use the cyan placeholder shader.
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.47f);
            var lightGo = new GameObject("PreviewKey");
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.7f;
            key.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(35, 155, 0);
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.22f, 0.24f, 0.27f);
            cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            void Shoot(string file)
            {
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;
                File.WriteAllBytes(outDir + file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var keys = Directory.GetFiles(PrefabRoot, "*.prefab", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/').Substring(PrefabRoot.Length).Replace(".prefab", ""))
                .Where(k => !k.StartsWith("Weapons/")).OrderBy(k => k).ToArray();
            foreach (var k in keys)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + k + ".prefab");
                if (prefab == null) continue;
                var go = UnityEngine.Object.Instantiate(prefab);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var body = go.GetComponent<UA.Game.ArtBody>();
                if (body == null) { UnityEngine.Object.DestroyImmediate(go); continue; }  // props (Items/...) have no skeleton
                var head = body.Bones[(int)UA.Game.ZB.Head].position + new Vector3(0, 0.09f, 0);
                string name = k.Replace('/', '_');
                cam.fieldOfView = 28;
                // Warm-up frame: lights and renderers created this editor frame are not picked up by the first render.
                cam.transform.position = head + new Vector3(0, 0.08f, 0.7f);
                cam.transform.LookAt(head);
                cam.Render();
                // Head three-quarter, front and top (hair), then the full body front and back.
                foreach (var (view, offset) in new[] { ("34", new Vector3(0.38f, 0.06f, 0.6f)), ("front", new Vector3(0, 0.08f, 0.7f)), ("top", new Vector3(0, 0.7f, 0.22f)) })
                {
                    cam.transform.position = head + offset;
                    cam.transform.LookAt(head);
                    Shoot(name + "_head_" + view + ".png");
                }
                cam.fieldOfView = 34;
                cam.transform.position = new Vector3(0.6f, 1.25f, 3.1f);
                cam.transform.LookAt(new Vector3(0, 0.92f, 0));
                Shoot(name + "_body.png");
                cam.transform.position = new Vector3(-0.6f, 1.25f, -3.1f);
                cam.transform.LookAt(new Vector3(0, 0.92f, 0));
                Shoot(name + "_body_back.png");
                UnityEngine.Object.DestroyImmediate(go);
            }
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            ShaderUtil.allowAsyncCompilation = asyncShaders;
            Debug.Log("[UA] ART previews in " + Path.GetFullPath(outDir));
        }

        static void Run(Action a)
        {
            try
            {
                AssetDatabase.Refresh();
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
