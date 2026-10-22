using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FRF
{
    /// <summary>
    /// Mesh builder working in Unity space. Triangles are oriented automatically so their
    /// front face points along a requested direction, which avoids winding mistakes when
    /// geometry is generated procedurally. <see cref="Xf"/> places primitives (low-poly props
    /// are flat shaded: every face gets its own vertices).
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Tris = new List<int>();
        public Matrix4x4 Xf = Matrix4x4.identity;

        public int Count => Verts.Count;

        public int Add(Vector3 p, Vector2 uv, Color32 c)
        {
            Verts.Add(p);
            Uvs.Add(uv);
            Colors.Add(c);
            return Verts.Count - 1;
        }

        public int Add(Vector3 p, Vector2 uv) => Add(p, uv, new Color32(255, 255, 255, 255));

        /// <summary>Adds triangle (a, b, c) with its front face towards <paramref name="facing"/>.</summary>
        public void Tri(int a, int b, int c, Vector3 facing)
        {
            var n = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
            if (Vector3.Dot(n, facing) < 0)
            {
                int t = b;
                b = c;
                c = t;
            }
            Tris.Add(a);
            Tris.Add(b);
            Tris.Add(c);
        }

        /// <summary>Quad a-b-c-d (in order around the edge) facing <paramref name="facing"/>.</summary>
        public void Quad(int a, int b, int c, int d, Vector3 facing)
        {
            Tri(a, b, c, facing);
            Tri(a, c, d, facing);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Color32 col, Vector2 uv0, Vector2 uv1)
        {
            int ia = Add(a, new Vector2(uv0.x, uv0.y), col);
            int ib = Add(b, new Vector2(uv1.x, uv0.y), col);
            int ic = Add(c, new Vector2(uv1.x, uv1.y), col);
            int id = Add(d, new Vector2(uv0.x, uv1.y), col);
            Quad(ia, ib, ic, id, facing);
        }

        // ------------------------------------------------------------ palette primitives (local space through Xf)

        Vector3 P(Vector3 p) => Xf.MultiplyPoint3x4(p);
        Vector3 D(Vector3 d) => Xf.MultiplyVector(d);

        /// <summary>Flat triangle in local space, front face away from <paramref name="inside"/>.</summary>
        public void FlatTri(Vector3 a, Vector3 b, Vector3 c, Vector2 uv, Vector3 inside)
        {
            var wa = P(a);
            var wb = P(b);
            var wc = P(c);
            var mid = (wa + wb + wc) / 3f;
            int ia = Add(wa, uv), ib = Add(wb, uv), ic = Add(wc, uv);
            Tri(ia, ib, ic, mid - P(inside));
        }

        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 inside)
        {
            FlatTri(a, b, c, uv, inside);
            FlatTri(a, c, d, uv, inside);
        }

        /// <summary>Flat quad facing an explicit local direction (single sided panels).</summary>
        public void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 facing)
        {
            int ia = Add(P(a), uv), ib = Add(P(b), uv), ic = Add(P(c), uv), id = Add(P(d), uv);
            Quad(ia, ib, ic, id, D(facing));
        }

        /// <summary>Box with half extents <paramref name="h"/> centred at <paramref name="c"/> (local).</summary>
        public void Box(Vector3 c, Vector3 h, Color col)
        {
            var uv = Palette.Uv(col);
            Vector3 V(float x, float y, float z) => c + new Vector3(h.x * x, h.y * y, h.z * z);
            FlatQuad(V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1), uv, c);
            FlatQuad(V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1), uv, c);
            FlatQuad(V(1, -1, -1), V(1, -1, 1), V(1, 1, 1), V(1, 1, -1), uv, c);
            FlatQuad(V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1), uv, c);
            FlatQuad(V(-1, 1, -1), V(1, 1, -1), V(1, 1, 1), V(-1, 1, 1), uv, c);
            FlatQuad(V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1), uv, c);
        }

        /// <summary>Box spanning from <paramref name="a"/> to <paramref name="b"/> (a beam / plank) with the given cross size.</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float h, Color col, Vector3? upHint = null)
        {
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            var f = dir / len;
            var up = upHint ?? Vector3.up;
            if (Mathf.Abs(Vector3.Dot(up.normalized, f)) > 0.95f) up = Vector3.right;
            var r = Vector3.Cross(up, f).normalized;
            var u = Vector3.Cross(f, r);
            var uv = Palette.Uv(col);
            var c = (a + b) * 0.5f;
            Vector3 V(float x, float y, float z) => c + r * (w * 0.5f * x) + u * (h * 0.5f * y) + f * (len * 0.5f * z);
            FlatQuad(V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1), uv, c);
            FlatQuad(V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1), uv, c);
            FlatQuad(V(1, -1, -1), V(1, -1, 1), V(1, 1, 1), V(1, 1, -1), uv, c);
            FlatQuad(V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1), uv, c);
            FlatQuad(V(-1, 1, -1), V(1, 1, -1), V(1, 1, 1), V(-1, 1, 1), uv, c);
            FlatQuad(V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1), uv, c);
        }

        /// <summary>Faceted cylinder / cone frustum from a (radius ra) to b (radius rb).</summary>
        public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int sides, Color col, bool caps = true, float twist = 0)
        {
            var axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            var f = axis / len;
            var r = Vector3.Cross(Mathf.Abs(f.y) > 0.9f ? Vector3.forward : Vector3.up, f).normalized;
            var u = Vector3.Cross(f, r);
            var uv = Palette.Uv(col);
            var mid = (a + b) * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2 + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2 + twist;
                var d0 = r * Mathf.Cos(a0) + u * Mathf.Sin(a0);
                var d1 = r * Mathf.Cos(a1) + u * Mathf.Sin(a1);
                if (rb > 1e-4f) FlatQuad(a + d0 * ra, a + d1 * ra, b + d1 * rb, b + d0 * rb, uv, mid);
                else FlatTri(a + d0 * ra, a + d1 * ra, b, uv, mid);
                if (caps)
                {
                    FlatTri(a, a + d1 * ra, a + d0 * ra, uv, a + f);
                    if (rb > 1e-4f) FlatTri(b, b + d0 * rb, b + d1 * rb, uv, b - f);
                }
            }
        }

        /// <summary>Low-poly lumpy boulder (a jittered octahedron sphere).</summary>
        public void Rock(Vector3 c, Vector3 size, int seed, Color col, int detail = 1)
        {
            var uv = Palette.Uv(col);
            var rnd = new System.Random(seed);
            int lat = 3 + detail, lon = 5 + detail * 2;
            var pts = new Vector3[(lat + 1) * lon];
            for (int i = 0; i <= lat; i++)
            {
                float v = i / (float)lat;
                float phi = Mathf.PI * (v - 0.5f);
                for (int j = 0; j < lon; j++)
                {
                    float th = j / (float)lon * Mathf.PI * 2 + (i % 2) * 0.4f;
                    float jit = 0.78f + (float)rnd.NextDouble() * 0.38f;
                    var p = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th)) * jit;
                    if (i == 0) p = new Vector3(0, -0.85f, 0);
                    if (i == lat) p = new Vector3(0, 0.75f + (float)rnd.NextDouble() * 0.2f, 0);
                    // Flatten the base so rocks sit on the ground.
                    if (p.y < -0.35f) p.y = -0.35f - (p.y + 0.35f) * 0.25f;
                    pts[i * lon + j] = c + Vector3.Scale(p, size);
                }
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int j1 = (j + 1) % lon;
                var p00 = pts[i * lon + j];
                var p01 = pts[i * lon + j1];
                var p10 = pts[(i + 1) * lon + j];
                var p11 = pts[(i + 1) * lon + j1];
                // Bottom ring collapses p00/p01, top ring collapses p10/p11.
                if (i > 0) FlatTri(p00, p01, p11, uv, c);
                if (i < lat - 1) FlatTri(p00, p11, p10, uv, c);
            }
        }

        /// <summary>Copies another builder's geometry through <see cref="Xf"/>.</summary>
        public void Append(MeshBuilder other)
        {
            int o = Verts.Count;
            for (int i = 0; i < other.Verts.Count; i++) Add(P(other.Verts[i]), other.Uvs[i], other.Colors[i]);
            foreach (int t in other.Tris) Tris.Add(o + t);
        }

        public Mesh Build(string name, bool recalcNormals = true)
        {
            var m = new Mesh { name = name };
            if (Verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(Verts);
            m.SetUVs(0, Uvs);
            m.SetColors(Colors);
            m.SetTriangles(Tris, 0);
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public void Clear()
        {
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
            Xf = Matrix4x4.identity;
        }
    }

    public static class MeshKit
    {
        static Mesh _sphere, _cube, _quad;
        static readonly Dictionary<long, Mesh> Tapered = new Dictionary<long, Mesh>();

        /// <summary>Unit sphere (radius 1), smooth.</summary>
        public static Mesh Sphere => _sphere ??= BuildSphere(16, 10);

        /// <summary>Cube with half-extent 1.</summary>
        public static Mesh Cube => _cube ??= BuildCube();

        /// <summary>Quad in the XY plane, 1x1, facing -Z (like Unity's).</summary>
        public static Mesh Quad => _quad ??= BuildQuad();

        static Mesh BuildSphere(int seg, int rings)
        {
            var b = new MeshBuilder();
            for (int i = 0; i <= rings; i++)
            {
                float v = i / (float)rings;
                float lat = Mathf.PI * (v - 0.5f);
                for (int j = 0; j <= seg; j++)
                {
                    float u = j / (float)seg;
                    float lon = u * Mathf.PI * 2;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    b.Add(p, new Vector2(u, v));
                }
            }
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, c = a + seg + 1;
                    var mid = (b.Verts[a] + b.Verts[a + 1] + b.Verts[c] + b.Verts[c + 1]) * 0.25f;
                    if (i > 0) b.Tri(a, a + 1, c + 1, mid);
                    if (i < rings - 1) b.Tri(a, c + 1, c, mid);
                }
            }
            var m = b.Build("Sphere", false);
            var n = new Vector3[b.Verts.Count];
            for (int i = 0; i < n.Length; i++) n[i] = b.Verts[i].normalized;
            m.normals = n;
            return m;
        }

        static Mesh BuildCube()
        {
            var b = new MeshBuilder();
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in dirs)
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(n, u);
                var c = n;
                b.Quad(c - u - v, c + u - v, c + u + v, c - u + v, n, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            }
            return b.Build("Cube");
        }

        static Mesh BuildQuad()
        {
            var b = new MeshBuilder();
            b.Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
                Vector3.back, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            return b.Build("Quad");
        }

        /// <summary>
        /// Capped cone segment along +Y from 0 to 1 with radius <paramref name="ra"/>
        /// at the bottom and <paramref name="rb"/> at the top. Scale Y by the bone length.
        /// </summary>
        public static Mesh TaperedLimb(float ra, float rb, int seg = 10)
        {
            long key = ((long)Mathf.RoundToInt(ra * 1000) << 32) | (long)(Mathf.RoundToInt(rb * 1000) * 64 + seg);
            if (Tapered.TryGetValue(key, out var cached)) return cached;
            var b = new MeshBuilder();
            for (int k = 0; k <= 1; k++)
            {
                float r = k == 0 ? ra : rb;
                for (int j = 0; j <= seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2;
                    b.Add(new Vector3(Mathf.Cos(a) * r, k, Mathf.Sin(a) * r), new Vector2(j / (float)seg, k));
                }
            }
            for (int j = 0; j < seg; j++)
            {
                int a = j, c = seg + 1 + j;
                float ang = (j + 0.5f) / seg * Mathf.PI * 2;
                var outward = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                b.Quad(a, a + 1, c + 1, c, outward);
            }
            // Caps.
            int cb = b.Add(Vector3.zero, new Vector2(0.5f, 0)), ct = b.Add(Vector3.up, new Vector2(0.5f, 1));
            for (int j = 0; j < seg; j++)
            {
                b.Tri(cb, j, j + 1, Vector3.down);
                b.Tri(ct, seg + 1 + j, seg + 2 + j, Vector3.up);
            }
            var m = b.Build("Limb", true);
            Tapered[key] = m;
            return m;
        }

        public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true, bool receive = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
            return go;
        }

        /// <summary>Points a +Y limb mesh transform from <paramref name="a"/> to <paramref name="b"/> (parent space).</summary>
        public static void Span(Transform t, Vector3 a, Vector3 b, float thickness = 1)
        {
            var d = b - a;
            float len = d.magnitude;
            t.localPosition = a;
            t.localRotation = len > 1e-5f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            t.localScale = new Vector3(thickness, Mathf.Max(1e-4f, len), thickness);
        }
    }
}
