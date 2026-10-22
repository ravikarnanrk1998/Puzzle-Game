using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCC.Game
{
    /// <summary>
    /// Mesh builder working in Unity space. Triangles are oriented automatically
    /// so their front face points along a requested direction, which avoids
    /// winding mistakes when geometry is generated procedurally.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Tris = new List<int>();

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
    }

    public static class MeshKit
    {
        static Mesh _sphere, _cube, _quad;
        static readonly Dictionary<long, Mesh> Tapered = new Dictionary<long, Mesh>();

        /// <summary>Unit sphere (radius 1).</summary>
        public static Mesh Sphere => _sphere ??= BuildSphere(18, 12);

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
        public static Mesh TaperedLimb(float ra, float rb, int seg = 12)
        {
            long key = ((long)Mathf.RoundToInt(ra * 1000) << 32) | (long)(Mathf.RoundToInt(rb * 1000) * 64 + seg);
            if (Tapered.TryGetValue(key, out var cached)) return cached;
            var b = new MeshBuilder();
            int ring0 = b.Verts.Count;
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
                int a = ring0 + j, c = ring0 + seg + 1 + j;
                float ang = (j + 0.5f) / seg * Mathf.PI * 2;
                var outward = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                b.Quad(a, a + 1, c + 1, c, outward);
            }
            var m = b.Build("Limb", true);
            Tapered[key] = m;
            return m;
        }

        /// <summary>Elliptical ring band: points at angle phi on an ellipse offset by <paramref name="extra"/> metres.</summary>
        public static Vector3 Ring(double rx, double ry, double phi, double extra, double z) =>
            new V3(Math.Cos(phi) * (rx + extra), Pitch.Mid + Math.Sin(phi) * (ry + extra), z).U;

        /// <summary>
        /// Builds a ring-shaped band (stands, walls, roof) between offsets e0 → e1
        /// and heights z0 → z1. Faces point towards the field centre (and up).
        /// </summary>
        public static Mesh RingBand(double rx, double ry, double e0, double e1, double z0, double z1, int cols, int rows,
            float uTile, Func<double, double, Color32> color = null, bool faceOut = false)
        {
            var b = new MeshBuilder();
            double meanR = rx + (e0 + e1) / 2;
            for (int j = 0; j <= rows; j++)
            {
                double f = j / (double)rows;
                double e = e0 + (e1 - e0) * f, z = z0 + (z1 - z0) * f;
                for (int i = 0; i <= cols; i++)
                {
                    double phi = -Math.PI / 2 + i / (double)cols * Math.PI * 2;
                    float u = (float)(phi * meanR / uTile);
                    var c = color != null ? color(phi, f) : new Color32(255, 255, 255, 255);
                    b.Add(Ring(rx, ry, phi, e, z), new Vector2(-u, (float)f), c);
                }
            }
            var centre = new V3(0, Pitch.Mid, z0 + 2).U;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int a = j * (cols + 1) + i, c = a + cols + 1;
                    var mid = (b.Verts[a] + b.Verts[c + 1]) * 0.5f;
                    var inward = centre - mid;
                    inward.y = Mathf.Max(inward.y, 0) + 8f;
                    b.Quad(a, a + 1, c + 1, c, faceOut ? -inward : inward);
                }
            }
            return b.Build("Ring", true);
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
    }
}
