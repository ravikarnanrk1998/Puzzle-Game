using System.Collections.Generic;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>Rigid kit pieces: helmet with grille, caps, umpire's hat and a shaped willow bat.</summary>
    public static class Gear
    {
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> Mats_ = new Dictionary<string, Material>();

        static Mesh CachedMesh(string key, System.Func<Mesh> make)
        {
            if (Meshes.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            Meshes[key] = m;
            return m;
        }

        static Material Mat(string key, System.Func<Material> make)
        {
            if (Mats_.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            Mats_[key] = m;
            return m;
        }

        public static Transform Helmet(Transform head, TeamDef t)
        {
            var mesh = CachedMesh("helmet", () =>
            {
                var b = new MeshBuilder();
                // Shell over the crown and back of the head.
                b.Sphere(0, new Vector3(0, 0.135f, -0.006f), new Vector3(0.108f, 0.118f, 0.124f), 22, 12, 0f, 0.62f);
                // Curved peak over the eyes.
                b.Sphere(0, new Vector3(0, 0.165f, 0.105f), new Vector3(0.095f, 0.012f, 0.06f), 14, 6);
                // Grille: curved horizontal bars plus two uprights.
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.085f - i * 0.032f;
                    float r = 0.112f + i * 0.004f;
                    for (int s = 0; s < 6; s++)
                    {
                        float a0 = Mathf.Lerp(-1.05f, 1.05f, s / 6f), a1 = Mathf.Lerp(-1.05f, 1.05f, (s + 1) / 6f);
                        b.Cylinder(1, new Vector3(Mathf.Sin(a0) * r, y, Mathf.Cos(a0) * r + 0.005f), new Vector3(Mathf.Sin(a1) * r, y, Mathf.Cos(a1) * r + 0.005f), 0.0045f, 0.0045f, 5, false);
                    }
                }
                foreach (float x in new[] { -0.03f, 0.03f }) b.Cylinder(1, new Vector3(x, 0.09f, 0.115f), new Vector3(x * 1.1f, 0.02f, 0.122f), 0.0045f, 0.0045f, 5, false);
                // Neck guard.
                b.Sphere(0, new Vector3(0, 0.05f, -0.07f), new Vector3(0.07f, 0.035f, 0.04f), 8, 4, 0.45f, 1f);
                return b.ToMesh("Helmet");
            });
            var shell = Mat("helmet" + t.Id, () => Mats.Lit(Crest.Primary(t).Luma() > 0.6f ? Crest.Secondary(t) : Crest.Primary(t) * 0.9f, 0.82f, 0.15f));
            var grille = Mat("grille", () => Mats.Lit(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.85f));
            var go = MeshKit.Spawn("Helmet", mesh, head, shell, grille);
            return go.transform;
        }

        public static Transform Cap(Transform head, TeamDef t)
        {
            var mesh = CachedMesh("cap", () =>
            {
                var b = new MeshBuilder();
                // Six-panel crown sitting on the head, with a curved brim attached at the forehead.
                b.Sphere(0, new Vector3(0, 0.158f, 0.004f), new Vector3(0.088f, 0.094f, 0.104f), 20, 10, 0f, 0.5f);
                b.Cylinder(0, new Vector3(0, 0.14f, 0.004f), new Vector3(0, 0.16f, 0.004f), 0.089f, 0.089f, 20, false);
                b.Xf = Matrix4x4.TRS(new Vector3(0, 0.145f, 0.118f), Quaternion.Euler(10, 0, 0), Vector3.one);
                b.Sphere(1, Vector3.zero, new Vector3(0.082f, 0.008f, 0.06f), 14, 6);
                b.Xf = Matrix4x4.identity;
                return b.ToMesh("Cap");
            });
            var c = Crest.Primary(t);
            var m0 = Mat("cap" + t.Id, () => Mats.Lit(c, 0.25f));
            var m1 = Mat("capbrim" + t.Id, () => Mats.Lit(Crest.Trim(t), 0.3f));
            return MeshKit.Spawn("Cap", mesh, head, m0, m1).transform;
        }

        public static Transform UmpireHat(Transform head)
        {
            var mesh = CachedMesh("umphat", () =>
            {
                var b = new MeshBuilder();
                b.Cylinder(0, new Vector3(0, 0.15f, 0.005f), new Vector3(0, 0.248f, 0.005f), 0.098f, 0.09f, 18, true);
                b.Cylinder(0, new Vector3(0, 0.146f, 0.005f), new Vector3(0, 0.154f, 0.005f), 0.178f, 0.178f, 22, true);
                b.Cylinder(1, new Vector3(0, 0.154f, 0.005f), new Vector3(0, 0.176f, 0.005f), 0.1f, 0.099f, 18, false);
                return b.ToMesh("UmpHat");
            });
            return MeshKit.Spawn("UmpireHat", mesh, head, Mat("umphat", () => Mats.Lit(new Color(0.94f, 0.94f, 0.9f), 0.25f)), Mat("umpband", () => Mats.Lit(new Color(0.08f, 0.1f, 0.2f), 0.3f))).transform;
        }

        /// <summary>Bat with its origin at the top of the handle, blade along -Y, hitting face towards +Z.</summary>
        public static Transform MakeBat(Transform parent)
        {
            var mesh = CachedMesh("bat", () =>
            {
                var b = new MeshBuilder();
                // Handle with grip.
                b.Cylinder(1, new Vector3(0, 0, 0), new Vector3(0, -0.3f, 0), 0.0165f, 0.0175f, 10, true, 4);
                // Blade: extruded cross-section with a raised spine on the back.
                int cols = 10, rows = 12;
                int start = b.VertexCount;
                for (int r = 0; r <= rows; r++)
                {
                    float k = r / (float)rows;
                    float y = Mathf.Lerp(-0.29f, -0.965f, k);
                    float shoulder = Mathf.Clamp01(k / 0.08f);
                    float hw = Mathf.Lerp(0.025f, 0.054f, Mathf.SmoothStep(0, 1, shoulder));
                    float spine = Mathf.Lerp(0.03f, 0.05f, Mathf.Sin(Mathf.Clamp01((k - 0.2f) / 0.75f) * Mathf.PI) * 0.8f + 0.2f) * Mathf.Lerp(0.6f, 1, shoulder);
                    if (k > 0.96f) hw *= 0.97f;
                    for (int c = 0; c <= cols; c++)
                    {
                        float a = c / (float)cols * Mathf.PI * 2;
                        float sx = Mathf.Sin(a), cz = Mathf.Cos(a);
                        // Flat face (+Z), curved back.
                        float z = cz > 0 ? 0.012f * Mathf.Min(1, cz * 3) : cz * spine * (1 - sx * sx * 0.55f);
                        var p = new Vector3(sx * hw, y, z);
                        var n = cz > 0 ? new Vector3(sx * 0.3f, 0, 1).normalized : new Vector3(sx, 0, cz).normalized;
                        b.Add(p, n, new Vector2(c / (float)cols, k));
                    }
                }
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int i0 = start + r * (cols + 1) + c, i1 = i0 + cols + 1;
                        b.Quad(0, i0, i1, i1 + 1, i0 + 1);
                    }
                }
                // Toe cap.
                int toe = b.Add(new Vector3(0, -0.97f, -0.005f), Vector3.down, new Vector2(0.5f, 1));
                int last = start + rows * (cols + 1);
                for (int c = 0; c < cols; c++) b.Tri(0, last + c, last + c + 1, toe);
                // Back sticker band.
                b.Xf = Matrix4x4.TRS(new Vector3(0, -0.52f, -0.047f), Quaternion.identity, Vector3.one);
                b.Box(2, Vector3.zero, new Vector3(0.07f, 0.2f, 0.006f));
                b.Xf = Matrix4x4.identity;
                return b.ToMesh("Bat");
            });
            var willow = Mat("willow", () => Mats.Lit(Color.white, 0.35f, 0, Tex.Willow));
            var grip = Mat("grip", () => Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.2f));
            var sticker = Mat("sticker", () => Mats.Lit(Pal.Accent, 0.5f));
            var go = MeshKit.Spawn("Bat", mesh, parent, willow, grip, sticker);
            return go.transform;
        }

        public static Transform MakeBall(Transform parent, float radius = 0.036f)
        {
            var mesh = CachedMesh("ball", () =>
            {
                var b = new MeshBuilder();
                b.Sphere(0, Vector3.zero, Vector3.one, 16, 10);
                return b.ToMesh("Ball");
            });
            var go = MeshKit.Spawn("Ball", mesh, parent, Mat("ball", () => Mats.Emissive(Color.white, new Color(0.22f, 0.22f, 0.2f), Tex.Ball, 0.55f)));
            go.transform.localScale = Vector3.one * radius;
            return go.transform;
        }
    }
}
