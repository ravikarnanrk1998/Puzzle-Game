using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KPL.Game
{
    /// <summary>Accumulates geometry (with optional submeshes and bone weights) and builds a Unity mesh.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Color32> C = new List<Color32>();
        public readonly List<BoneWeight> W = new List<BoneWeight>();
        readonly List<List<int>> _subs = new List<List<int>>();
        public Color32 Tint = new Color32(255, 255, 255, 255);
        public BoneWeight Weight;
        public bool UseWeights;
        public Matrix4x4 Xf = Matrix4x4.identity;

        public int VertexCount => V.Count;

        List<int> Sub(int i)
        {
            while (_subs.Count <= i) _subs.Add(new List<int>());
            return _subs[i];
        }

        public int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(Xf.MultiplyPoint3x4(p));
            N.Add(Xf.MultiplyVector(n).normalized);
            UV.Add(uv);
            C.Add(Tint);
            if (UseWeights) W.Add(Weight);
            return V.Count - 1;
        }

        public int AddW(Vector3 p, Vector3 n, Vector2 uv, BoneWeight w)
        {
            int i = Add(p, n, uv);
            if (UseWeights) W[i] = w;
            return i;
        }

        public void Tri(int sub, int a, int b, int c)
        {
            var s = Sub(sub);
            s.Add(a);
            s.Add(b);
            s.Add(c);
        }

        public void Quad(int sub, int a, int b, int c, int d)
        {
            Tri(sub, a, b, c);
            Tri(sub, a, c, d);
        }

        /// <summary>Flat quad from four corners (counter-clockwise seen from the front).</summary>
        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv0, Vector2 uv1)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i0 = Add(a, n, new Vector2(uv0.x, uv0.y));
            int i1 = Add(b, n, new Vector2(uv1.x, uv0.y));
            int i2 = Add(c, n, new Vector2(uv1.x, uv1.y));
            int i3 = Add(d, n, new Vector2(uv0.x, uv1.y));
            Quad(sub, i0, i1, i2, i3);
        }

        public void Box(int sub, Vector3 center, Vector3 size, float uvScale = 1)
        {
            var h = size * 0.5f;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in dirs)
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
                var v = Vector3.Cross(n, u);
                float su = Mathf.Abs(Vector3.Dot(u, size)), sv = Mathf.Abs(Vector3.Dot(v, size));
                var c = center + Vector3.Scale(n, h);
                var hu = u * (su * 0.5f);
                var hv = v * (sv * 0.5f);
                int a = Add(c - hu - hv, n, new Vector2(0, 0));
                int b = Add(c + hu - hv, n, new Vector2(su * uvScale, 0));
                int d = Add(c + hu + hv, n, new Vector2(su * uvScale, sv * uvScale));
                int e = Add(c - hu + hv, n, new Vector2(0, sv * uvScale));
                Quad(sub, a, b, d, e);
            }
        }

        /// <summary>Cylinder or cone between two points.</summary>
        public void Cylinder(int sub, Vector3 a, Vector3 b, float ra, float rb, int segs, bool caps = true, float uvV = 1)
        {
            var axis = (b - a).normalized;
            var t1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            var t2 = Vector3.Cross(axis, t1);
            int start = V.Count;
            for (int i = 0; i <= segs; i++)
            {
                float ang = i / (float)segs * Mathf.PI * 2;
                var dir = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
                var slope = (ra - rb) / Mathf.Max(0.001f, (b - a).magnitude);
                var n = (dir + axis * slope).normalized;
                Add(a + dir * ra, n, new Vector2(i / (float)segs, 0));
                Add(b + dir * rb, n, new Vector2(i / (float)segs, uvV));
            }
            for (int i = 0; i < segs; i++)
            {
                int k = start + i * 2;
                Quad(sub, k, k + 2, k + 3, k + 1);
            }
            if (!caps) return;
            for (int end = 0; end < 2; end++)
            {
                var c = end == 0 ? a : b;
                float r = end == 0 ? ra : rb;
                if (r <= 0.0001f) continue;
                var n = end == 0 ? -axis : axis;
                int ci = Add(c, n, new Vector2(0.5f, 0.5f));
                int first = V.Count;
                for (int i = 0; i <= segs; i++)
                {
                    float ang = i / (float)segs * Mathf.PI * 2;
                    var dir = t1 * Mathf.Cos(ang) + t2 * Mathf.Sin(ang);
                    Add(c + dir * r, n, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
                }
                for (int i = 0; i < segs; i++)
                {
                    if (end == 0) Tri(sub, ci, first + i + 1, first + i);
                    else Tri(sub, ci, first + i, first + i + 1);
                }
            }
        }

        /// <summary>Surface of revolution around the Y axis from (radius, height) profile points.</summary>
        public void Lathe(int sub, IList<Vector2> profile, int segs, Vector3 center, float uMax = 1, float sx = 1, float sz = 1)
        {
            int start = V.Count;
            int rows = profile.Count;
            for (int i = 0; i <= segs; i++)
            {
                float ang = i / (float)segs * Mathf.PI * 2;
                float cx = Mathf.Sin(ang), cz = Mathf.Cos(ang);
                for (int j = 0; j < rows; j++)
                {
                    var p = profile[j];
                    var prev = profile[Mathf.Max(0, j - 1)];
                    var next = profile[Mathf.Min(rows - 1, j + 1)];
                    var tangent = next - prev;
                    var n2 = new Vector2(tangent.y, -tangent.x).normalized;
                    var pos = center + new Vector3(cx * p.x * sx, p.y, cz * p.x * sz);
                    var n = new Vector3(cx * n2.x / sx, n2.y, cz * n2.x / sz).normalized;
                    Add(pos, n, new Vector2(i / (float)segs * uMax, j / (float)(rows - 1)));
                }
            }
            for (int i = 0; i < segs; i++)
            {
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = start + i * rows + j;
                    int b = start + (i + 1) * rows + j;
                    Quad(sub, a, b, b + 1, a + 1);
                }
            }
        }

        public void Sphere(int sub, Vector3 center, Vector3 radii, int segs, int rings, float vMin = 0, float vMax = 1)
        {
            int start = V.Count;
            for (int r = 0; r <= rings; r++)
            {
                float v = Mathf.Lerp(vMin, vMax, r / (float)rings);
                float phi = v * Mathf.PI;
                for (int s = 0; s <= segs; s++)
                {
                    float th = s / (float)segs * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Sin(phi) * Mathf.Sin(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Cos(th));
                    var p = center + Vector3.Scale(d, radii);
                    var n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z).normalized;
                    Add(p, n, new Vector2(s / (float)segs, 1 - v));
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int a = start + r * (segs + 1) + s;
                    int b = a + segs + 1;
                    Quad(sub, a, b, b + 1, a + 1);
                }
            }
        }

        public Mesh ToMesh(string name, bool recalcNormals = false)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            m.SetColors(C);
            m.subMeshCount = Mathf.Max(1, _subs.Count);
            for (int i = 0; i < _subs.Count; i++) m.SetTriangles(_subs[i], i, false);
            if (UseWeights && W.Count == V.Count) m.boneWeights = W.ToArray();
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            if (Quality.Current >= Gfx.Medium) m.RecalculateTangents();
            return m;
        }

        public int SubCount => _subs.Count;
    }

    public static class MeshKit
    {
        public static GameObject Spawn(string name, Mesh mesh, Transform parent, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return go;
        }

        public static GameObject Spawn(string name, MeshBuilder b, Transform parent, params Material[] mats) => Spawn(name, b.ToMesh(name), parent, mats);

        public static Mesh Quad(float w, float h)
        {
            var b = new MeshBuilder();
            b.Quad(0, new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0), Vector2.zero, Vector2.one);
            return b.ToMesh("Quad");
        }

        public static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        public static void CastOnly(GameObject go, bool cast, bool receive)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = receive;
            }
        }
    }
}
