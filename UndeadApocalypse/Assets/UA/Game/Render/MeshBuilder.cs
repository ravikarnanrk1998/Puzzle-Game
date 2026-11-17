using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>
    /// Procedural mesh construction: boxes (sharp or rounded), cylinders, spheres, lathes, extruded
    /// 2D profiles and skinned tubes. Vertices pass through <see cref="M"/> so parts can be placed
    /// and rotated freely. Supports sub-meshes, vertex colours and bone weights.
    /// Winding: a face built from (o, o+u, o+u+v, o+v) faces along cross(u, v).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>(1024);
        readonly List<Vector3> _n = new List<Vector3>(1024);
        readonly List<Vector2> _uv = new List<Vector2>(1024);
        readonly List<Color32> _c = new List<Color32>(1024);
        readonly List<BoneWeight> _w = new List<BoneWeight>();
        readonly List<List<int>> _subs = new List<List<int>>();

        public Matrix4x4 M = Matrix4x4.identity;
        public Color32 Color = new Color32(255, 255, 255, 255);
        public bool UseWeights;
        public BoneWeight Weight = new BoneWeight { weight0 = 1 };
        public bool UseColors;

        public int VertexCount => _v.Count;

        List<int> Sub(int i)
        {
            while (_subs.Count <= i) _subs.Add(new List<int>(1024));
            return _subs[i];
        }

        /// <summary>
        /// Normalize that also works for sub-millimetre geometry: Vector3.normalized returns zero below a
        /// magnitude of 1e-5, which gave tiny parts (sight dots, reticles) zero normals and NaN pixels.
        /// </summary>
        public static Vector3 Nrm(Vector3 v, Vector3 fallback)
        {
            float m = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return m > 1e-18f ? new Vector3(v.x / m, v.y / m, v.z / m) : fallback;
        }

        public int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(M.MultiplyPoint3x4(p));
            _n.Add(Nrm(M.MultiplyVector(n), Vector3.up));
            _uv.Add(uv);
            _c.Add(Color);
            if (UseWeights) _w.Add(Weight);
            return _v.Count - 1;
        }

        public int AddW(Vector3 p, Vector3 n, Vector2 uv, BoneWeight w)
        {
            var old = Weight;
            Weight = w;
            int i = Add(p, n, uv);
            Weight = old;
            return i;
        }

        public void Tri(int sub, int a, int b, int c)
        {
            var s = Sub(sub);
            s.Add(a); s.Add(b); s.Add(c);
        }

        public void Quad(int sub, int a, int b, int c, int d)
        {
            var s = Sub(sub);
            s.Add(a); s.Add(b); s.Add(c);
            s.Add(a); s.Add(c); s.Add(d);
        }

        // ------------------------------------------------------------------ planes & boxes

        /// <summary>Quad from corner o spanning u and v; faces along cross(u, v).</summary>
        public void Plane(int sub, Vector3 o, Vector3 u, Vector3 v, Vector2 uv0, Vector2 uv1)
        {
            var n = Nrm(Vector3.Cross(u, v), Vector3.up);
            int a = Add(o, n, uv0);
            int b = Add(o + u, n, new Vector2(uv1.x, uv0.y));
            int c = Add(o + u + v, n, uv1);
            int d = Add(o + v, n, new Vector2(uv0.x, uv1.y));
            Quad(sub, a, b, c, d);
        }

        /// <summary>Plane with UVs in metres times uvScale (world-scale texturing).</summary>
        public void PlaneW(int sub, Vector3 o, Vector3 u, Vector3 v, float uvScale, Vector2 uvOff = default)
        {
            Plane(sub, o, u, v, uvOff, uvOff + new Vector2(u.magnitude * uvScale, v.magnitude * uvScale));
        }

        /// <summary>Upward-facing rectangle on the XZ plane.</summary>
        public void Floor(int sub, float x0, float z0, float w, float d, float y, float uvScale, Vector2 uvOff = default)
        {
            var o = new Vector3(x0, y, z0);
            int a = Add(o, Vector3.up, uvOff + new Vector2(x0, z0) * uvScale);
            int b = Add(o + new Vector3(0, 0, d), Vector3.up, uvOff + new Vector2(x0, z0 + d) * uvScale);
            int c = Add(o + new Vector3(w, 0, d), Vector3.up, uvOff + new Vector2(x0 + w, z0 + d) * uvScale);
            int e = Add(o + new Vector3(w, 0, 0), Vector3.up, uvOff + new Vector2(x0 + w, z0) * uvScale);
            Quad(sub, a, b, c, e);
        }

        /// <summary>Downward-facing rectangle (ceiling).</summary>
        public void Ceiling(int sub, float x0, float z0, float w, float d, float y, float uvScale)
        {
            var o = new Vector3(x0, y, z0);
            int a = Add(o, Vector3.down, new Vector2(x0, z0) * uvScale);
            int b = Add(o + new Vector3(w, 0, 0), Vector3.down, new Vector2(x0 + w, z0) * uvScale);
            int c = Add(o + new Vector3(w, 0, d), Vector3.down, new Vector2(x0 + w, z0 + d) * uvScale);
            int e = Add(o + new Vector3(0, 0, d), Vector3.down, new Vector2(x0, z0 + d) * uvScale);
            Quad(sub, a, b, c, e);
        }

        /// <summary>Vertical wall from p0 to p1 (bottom edge) of height h, facing its left side when walking p0 -> p1 ... use 'normal' to choose.</summary>
        public void Wall(int sub, Vector3 p0, Vector3 p1, float h, Vector3 facing, float uvScale, float uvY0 = 0)
        {
            var u = p1 - p0;
            var v = Vector3.up * h;
            if (Vector3.Dot(Vector3.Cross(u, v), facing) < 0)
            {
                var t = p0; p0 = p1; p1 = t;
                u = -u;
            }
            float len = u.magnitude;
            Plane(sub, p0, u, v, new Vector2(0, uvY0), new Vector2(len * uvScale, uvY0 + h * uvScale));
        }

        static void FaceAxes(int f, out Vector3 n, out Vector3 u, out Vector3 v)
        {
            switch (f)
            {
                case 0: n = Vector3.right; u = Vector3.back; v = Vector3.up; break;
                case 1: n = Vector3.left; u = Vector3.forward; v = Vector3.up; break;
                case 2: n = Vector3.up; u = Vector3.right; v = Vector3.back; break;
                case 3: n = Vector3.down; u = Vector3.right; v = Vector3.forward; break;
                case 4: n = Vector3.forward; u = Vector3.right; v = Vector3.up; break;
                default: n = Vector3.back; u = Vector3.left; v = Vector3.up; break;
            }
        }

        /// <summary>Axis-aligned (in M space) box with flat faces and metre-based UVs.</summary>
        public void Box(int sub, Vector3 center, Vector3 size, float uvScale = 1, bool skipBottom = false, bool skipTop = false)
        {
            var h = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                if (skipBottom && f == 3) continue;
                if (skipTop && f == 2) continue;
                FaceAxes(f, out var n, out var u, out var v);
                float su = Mathf.Abs(Vector3.Dot(u, size)), sv = Mathf.Abs(Vector3.Dot(v, size));
                var c = center + Vector3.Scale(n, h);
                var o = c - u * (su * 0.5f) - v * (sv * 0.5f);
                Plane(sub, o, u * su, v * sv, Vector2.zero, new Vector2(su * uvScale, sv * uvScale));
            }
        }

        /// <summary>Box with rounded edges and corners of radius r; smooth normals.</summary>
        public void RoundBox(int sub, Vector3 center, Vector3 size, float r, int seg = 2, float uvScale = 1)
        {
            var half = size * 0.5f;
            r = Mathf.Max(0.0005f, Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.98f));
            var inner = half - Vector3.one * r;
            for (int f = 0; f < 6; f++)
            {
                FaceAxes(f, out var n, out var u, out var v);
                float hu = Mathf.Abs(Vector3.Dot(u, half)), hv = Mathf.Abs(Vector3.Dot(v, half));
                float iu = Mathf.Abs(Vector3.Dot(u, inner)), iv = Mathf.Abs(Vector3.Dot(v, inner));
                float hn = Mathf.Abs(Vector3.Dot(n, half));
                var su = Samples(hu, iu, seg);
                var sv = Samples(hv, iv, seg);
                int nu = su.Length, nv = sv.Length;
                int start = VertexCount;
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        var p = n * hn + u * su[i] + v * sv[j];
                        var cl = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - cl;
                        var nn = Nrm(d, n);
                        Add(center + cl + nn * r, nn, new Vector2((su[i] + hu) * uvScale, (sv[j] + hv) * uvScale));
                    }
                for (int j = 0; j < nv - 1; j++)
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = start + j * nu + i;
                        Quad(sub, a, a + 1, a + nu + 1, a + nu);
                    }
            }
        }

        static float[] Samples(float h, float inner, int seg)
        {
            var l = new float[(seg + 1) * 2];
            for (int k = 0; k <= seg; k++)
            {
                l[k] = Mathf.Lerp(-h, -inner, k / (float)seg);
                l[seg + 1 + k] = Mathf.Lerp(inner, h, k / (float)seg);
            }
            return l;
        }

        // ------------------------------------------------------------------ round shapes

        /// <summary>Tube between two points, optionally tapered and capped.</summary>
        public void Cylinder(int sub, Vector3 a, Vector3 b, float ra, float rb, int segs = 12, bool capA = true, bool capB = true, float uvScale = 1)
        {
            var axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            var dir = axis / len;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var x = Vector3.Cross(dir, up).normalized;
            var y = Vector3.Cross(x, dir);
            int start = VertexCount;
            float slope = (ra - rb) / len;
            for (int r = 0; r <= 1; r++)
            {
                var c = r == 0 ? a : b;
                float rad = r == 0 ? ra : rb;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs * Mathf.PI * 2;
                    var radial = x * Mathf.Cos(t) + y * Mathf.Sin(t);
                    var n = (radial + dir * slope).normalized;
                    Add(c + radial * rad, n, new Vector2(s / (float)segs * Mathf.PI * 2 * Mathf.Max(ra, rb) * uvScale, r * len * uvScale));
                }
            }
            for (int s = 0; s < segs; s++)
            {
                int i0 = start + s, i1 = start + segs + 1 + s;
                Quad(sub, i0, i1, i1 + 1, i0 + 1);
            }
            if (capA && ra > 0) Disc(sub, a, -dir, x, ra, segs);
            if (capB && rb > 0) Disc(sub, b, dir, x, rb, segs);
        }

        public void Disc(int sub, Vector3 c, Vector3 n, Vector3 x, float r, int segs)
        {
            var y = Vector3.Cross(n, x).normalized;
            x = Vector3.Cross(y, n).normalized;
            int ci = Add(c, n, new Vector2(0.5f, 0.5f));
            int start = VertexCount;
            for (int s = 0; s <= segs; s++)
            {
                float t = s / (float)segs * Mathf.PI * 2;
                Add(c + (x * Mathf.Cos(t) + y * Mathf.Sin(t)) * r, n, new Vector2(0.5f + Mathf.Cos(t) * 0.5f, 0.5f + Mathf.Sin(t) * 0.5f));
            }
            for (int s = 0; s < segs; s++) Tri(sub, ci, start + s, start + s + 1);
        }

        public void Sphere(int sub, Vector3 c, Vector3 radii, int segs = 12, int rings = 8)
        {
            int start = VertexCount;
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float th = v * Mathf.PI;
                for (int s = 0; s <= segs; s++)
                {
                    float u = s / (float)segs;
                    float ph = u * Mathf.PI * 2;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    var p = new Vector3(n.x * radii.x, n.y * radii.y, n.z * radii.z);
                    var nn = new Vector3(n.x / Mathf.Max(1e-4f, radii.x), n.y / Mathf.Max(1e-4f, radii.y), n.z / Mathf.Max(1e-4f, radii.z)).normalized;
                    Add(c + p, nn, new Vector2(u, 1 - v));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + r * (segs + 1) + s, i1 = i0 + segs + 1;
                    Quad(sub, i0, i0 + 1, i1 + 1, i1);
                }
        }

        /// <summary>Surface of revolution around local Y from (radius, height) pairs (bottom to top).</summary>
        public void Lathe(int sub, IList<Vector2> profile, int segs = 16, float uvScale = 1)
        {
            int start = VertexCount;
            int rows = profile.Count;
            for (int i = 0; i < rows; i++)
            {
                var p = profile[i];
                var prev = profile[Mathf.Max(0, i - 1)];
                var next = profile[Mathf.Min(rows - 1, i + 1)];
                var tan = (next - prev).normalized;
                var n2 = new Vector2(tan.y, -tan.x);
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs * Mathf.PI * 2;
                    float c = Mathf.Cos(t), sn = Mathf.Sin(t);
                    Add(new Vector3(c * p.x, p.y, sn * p.x), new Vector3(c * n2.x, n2.y, sn * n2.x), new Vector2(s / (float)segs * uvScale, p.y * uvScale));
                }
            }
            for (int i = 0; i < rows - 1; i++)
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + i * (segs + 1) + s, i1 = i0 + segs + 1;
                    Quad(sub, i0, i1, i1 + 1, i0 + 1);
                }
        }

        /// <summary>Partial torus in the local XY plane (angles in degrees), e.g. trigger guards.</summary>
        public void Torus(int sub, Vector3 c, float R, float r, float a0, float a1, int segs = 12, int tube = 6)
        {
            int start = VertexCount;
            for (int i = 0; i <= segs; i++)
            {
                float t = Mathf.Lerp(a0, a1, i / (float)segs) * Mathf.Deg2Rad;
                var ring = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0);
                for (int j = 0; j <= tube; j++)
                {
                    float u = j / (float)tube * Mathf.PI * 2;
                    var n = ring * Mathf.Cos(u) + Vector3.forward * Mathf.Sin(u);
                    Add(c + ring * R + n * r, n, new Vector2(i / (float)segs, j / (float)tube));
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < tube; j++)
                {
                    int i0 = start + i * (tube + 1) + j, i1 = i0 + tube + 1;
                    Quad(sub, i0, i1, i1 + 1, i0 + 1);
                }
        }

        // ------------------------------------------------------------------ extrusion

        /// <summary>
        /// Extrudes a simple 2D polygon drawn in the side view (x = forward/Z, y = up/Y) across X by
        /// <paramref name="width"/> (centred). Ideal for receivers, stocks, grips and magazines.
        /// </summary>
        public void Extrude(int sub, IList<Vector2> poly, float width, float uvScale = 1)
        {
            int n = poly.Count;
            if (n < 3) return;
            float area = 0;
            for (int i = 0; i < n; i++) { var a = poly[i]; var b = poly[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            var pts = new List<Vector2>(poly);
            if (area < 0) pts.Reverse();
            float hw = width * 0.5f;
            var tris = Triangulate(pts);
            for (int side = 0; side < 2; side++)
            {
                float xs = side == 0 ? hw : -hw;
                var nrm = side == 0 ? Vector3.right : Vector3.left;
                int start = VertexCount;
                foreach (var p in pts) Add(new Vector3(xs, p.y, p.x), nrm, new Vector2(p.x * uvScale, p.y * uvScale));
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    if (side == 0) Tri(sub, start + tris[t], start + tris[t + 2], start + tris[t + 1]);
                    else Tri(sub, start + tris[t], start + tris[t + 1], start + tris[t + 2]);
                }
            }
            float run = 0;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                var e = b - a;
                float len = e.magnitude;
                if (len < 1e-6f) continue;
                // CCW polygon in (z, y): outward normal = (dy, -dz) -> world (0, -dz, dy).
                var wn = new Vector3(0, -e.x / len, e.y / len);
                int i0 = Add(new Vector3(hw, a.y, a.x), wn, new Vector2(run * uvScale, 0));
                int i1 = Add(new Vector3(hw, b.y, b.x), wn, new Vector2((run + len) * uvScale, 0));
                int i2 = Add(new Vector3(-hw, b.y, b.x), wn, new Vector2((run + len) * uvScale, width * uvScale));
                int i3 = Add(new Vector3(-hw, a.y, a.x), wn, new Vector2(run * uvScale, width * uvScale));
                Quad(sub, i0, i1, i2, i3);
                run += len;
            }
        }

        /// <summary>Ear-clipping triangulation of a simple CCW polygon. Returns index triples.</summary>
        public static List<int> Triangulate(List<Vector2> pts)
        {
            var res = new List<int>();
            var idx = new List<int>();
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    var a = pts[ia]; var b = pts[ib]; var c = pts[ic];
                    if (Cross(b - a, c - b) <= 1e-9f) continue;
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTri(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            return res;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }

        // ------------------------------------------------------------------ output

        public Mesh ToMesh(string name, bool tangents = true)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            if (UseColors) m.SetColors(_c);
            if (UseWeights && _w.Count == _v.Count) m.boneWeights = _w.ToArray();
            int subCount = Math.Max(1, _subs.Count);
            m.subMeshCount = subCount;
            for (int i = 0; i < subCount; i++) m.SetTriangles(i < _subs.Count ? _subs[i] : new List<int>(), i, false);
            m.RecalculateBounds();
            if (tangents && _v.Count > 0) m.RecalculateTangents();
            return m;
        }

        public int SubCount => _subs.Count;

        public bool HasTris(int sub) => sub < _subs.Count && _subs[sub].Count > 0;

        public bool Empty => _v.Count == 0;
    }
}
