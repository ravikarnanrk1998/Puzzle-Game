using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    /// <summary>Builds the 3D mountain for a <see cref="Track"/>: terrain, trail, jumps, scenery and sky dressing.</summary>
    public sealed class TrackScene : MonoBehaviour
    {
        public Track Track { get; private set; }
        public TerrainField Field { get; private set; }
        Theme Th => Track.Theme;
        Material _trail, _terrain, _planks;
        GraphicsQuality _q;

        public static TrackScene Build(TrackDef def, Transform parent, GraphicsQuality q)
        {
            var go = new GameObject("Track " + def.Name);
            go.transform.SetParent(parent, false);
            var ts = go.AddComponent<TrackScene>();
            ts._q = q;
            ts.Track = new Track(def);
            ts.BuildAll();
            return ts;
        }

        void BuildAll()
        {
            float cell = _q == GraphicsQuality.High ? 6 : _q == GraphicsQuality.Medium ? 8 : 10;
            Field = new TerrainField(Track, cell, 280);
            _trail = Mats.Lit(Color.white, Th.Biome == Biome.Glacier ? 0.45f : 0.08f, Tex.Trail(Th));
            _terrain = Mats.Terrain(ColorMap(), Tex.Grain(Th.Biome), new Vector2(Field.SizeX / 9f, Field.SizeZ / 9f), Th.Biome == Biome.Glacier ? 0.35f : 0.05f);
            _planks = Mats.Lit(Color.white, 0.15f, Tex.Planks());
            TerrainChunks();
            TrailChunks();
            Verges();
            Ramps();
            TrailDressing();
            Gates();
            Scatter();
            Crowds();
            Mountains();
            Palette.Apply();
        }

        // ------------------------------------------------------------ colour map

        Texture2D ColorMap()
        {
            int res = _q == GraphicsQuality.High ? 1024 : 640;
            float aspect = Field.SizeX / Field.SizeZ;
            int w = aspect >= 1 ? res : Mathf.Max(64, Mathf.RoundToInt(res * aspect));
            int h = aspect >= 1 ? Mathf.Max(64, Mathf.RoundToInt(res / aspect)) : res;
            var f = Field;
            int nx = f.Nx, nz = f.Nz;
            // Per grid point: slope and distance beyond the trail edge.
            var slope = new float[nx * nz];
            var clear = new float[nx * nz];
            var rel = new float[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i;
                float hx = f.H[j * nx + Mathf.Min(nx - 1, i + 1)] - f.H[j * nx + Mathf.Max(0, i - 1)];
                float hz = f.H[Mathf.Min(nz - 1, j + 1) * nx + i] - f.H[Mathf.Max(0, j - 1) * nx + i];
                slope[k] = Mathf.Sqrt(hx * hx + hz * hz) / (2 * f.Cell);
                clear[k] = Mathf.Abs(f.Lat[k]) - Track.WidthAt(f.S[k]) * 0.5f;
                // Height above the nearest bit of trail: caps belong to the tall hills only.
                rel[k] = f.H[k] - Track.BaseY(f.S[k]);
            }
            float Bil(float[] a, float fx, float fz)
            {
                int i = Mathf.Clamp((int)fx, 0, nx - 2), j = Mathf.Clamp((int)fz, 0, nz - 2);
                float tx = Mathf.Clamp01(fx - i), tz = Mathf.Clamp01(fz - j);
                return Mathf.Lerp(Mathf.Lerp(a[j * nx + i], a[j * nx + i + 1], tx), Mathf.Lerp(a[(j + 1) * nx + i], a[(j + 1) * nx + i + 1], tx), tz);
            }
            var px = new Px(w, h, Color.black);
            var th = Th;
            bool snowy = th.Biome == Biome.Glacier;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w * (nx - 1), fz = (y + 0.5f) / h * (nz - 1);
                float wx = f.X0 + fx * f.Cell, wz = f.Z0 + fz * f.Cell;
                float sl = Bil(slope, fx, fz), cl = Bil(clear, fx, fz), hh = Bil(f.H, fx, fz);
                float n1 = Tex.Fbm(wx * 0.02f, wz * 0.02f, 3);
                float n2 = Tex.Noise(wx * 0.11f, wz * 0.11f);
                var c = Color.Lerp(th.GrassDark, th.Grass, Mathf.Clamp01(n1 * 1.3f - 0.1f));
                c *= 0.92f + n2 * 0.16f;
                // Patches of bare ground and rock on steep faces.
                float rock = Tex.SmoothStep(0.42f, 0.75f, sl + (n1 - 0.5f) * 0.25f);
                var rc = Color.Lerp(th.RockDark, th.Rock, n2);
                if (th.Biome == Biome.Canyon) rc = Color.Lerp(th.RockDark, th.Rock, 0.5f + 0.5f * Mathf.Sin(hh * 0.45f + n1 * 3));
                c = Color.Lerp(c, rc, rock);
                float dirt = Tex.SmoothStep(0.55f, 0.75f, Tex.Fbm(wx * 0.006f + 40, wz * 0.006f, 3)) * 0.5f;
                if (!snowy) c = Color.Lerp(c, th.Dirt.Darken(0.1f), dirt * (1 - rock));
                // Snow (or pale) caps high above the course.
                float above = Bil(rel, fx, fz) - 95;
                float cap = Tex.SmoothStep(0, 45, above + (n1 - 0.5f) * 50) * (1 - rock * 0.6f);
                c = Color.Lerp(c, th.Cap, cap);
                // Worn, darker ground right beside the trail.
                float wear = 1 - Tex.SmoothStep(0.5f, 4.5f, cl + (n2 - 0.5f) * 2);
                c = Color.Lerp(c, snowy ? th.DirtDark.Lighten(0.3f) : th.Dirt.Darken(0.15f), wear * 0.55f);
                c.a = 1;
                px.P[y * w + x] = c;
            }
            return px.ToTexture("TerrainColor", true, TextureWrapMode.Clamp, FilterMode.Trilinear, 4);
        }

        // ------------------------------------------------------------ terrain

        void TerrainChunks()
        {
            var f = Field;
            const int ch = 40;
            var parent = new GameObject("Terrain").transform;
            parent.SetParent(transform, false);
            for (int cj = 0; cj < f.Nz - 1; cj += ch)
            for (int ci = 0; ci < f.Nx - 1; ci += ch)
            {
                int i1 = Mathf.Min(f.Nx - 1, ci + ch), j1 = Mathf.Min(f.Nz - 1, cj + ch);
                int w = i1 - ci + 1;
                var b = new MeshBuilder();
                var normals = new List<Vector3>();
                for (int j = cj; j <= j1; j++)
                for (int i = ci; i <= i1; i++)
                {
                    var p = f.GridPos(i, j);
                    b.Add(p, f.Uv(p.x, p.z));
                    float hx = f.H[j * f.Nx + Mathf.Min(f.Nx - 1, i + 1)] - f.H[j * f.Nx + Mathf.Max(0, i - 1)];
                    float hz = f.H[Mathf.Min(f.Nz - 1, j + 1) * f.Nx + i] - f.H[Mathf.Max(0, j - 1) * f.Nx + i];
                    normals.Add(new Vector3(-hx, 2 * f.Cell, -hz).normalized);
                }
                for (int j = 0; j < j1 - cj; j++)
                for (int i = 0; i < i1 - ci; i++)
                {
                    int a = j * w + i, bb = a + 1, c = a + w, d = c + 1;
                    b.Tri(a, c, d, Vector3.up);
                    b.Tri(a, d, bb, Vector3.up);
                }
                var mesh = b.Build("Terrain", false);
                mesh.SetNormals(normals);
                var go = MeshKit.Obj("Chunk", parent, mesh, _terrain, false, true);
                go.isStatic = true;
            }
        }

        Vector3 TrailNormal(float s, float x)
        {
            const float e = 0.7f;
            var tr = Track;
            var a = tr.World(s - e, x, tr.TrailY(s - e, x));
            var b = tr.World(s + e, x, tr.TrailY(s + e, x));
            var c = tr.World(s, x - e, tr.TrailY(s, x - e));
            var d = tr.World(s, x + e, tr.TrailY(s, x + e));
            var n = Vector3.Cross(b - a, d - c);
            if (n.y < 0) n = -n;
            return n.normalized;
        }

        void TrailChunks()
        {
            var tr = Track;
            const int cols = 8, rows = 50;
            var parent = new GameObject("Trail").transform;
            parent.SetParent(transform, false);
            for (int c0 = 0; c0 < tr.N - 1; c0 += rows)
            {
                int c1 = Mathf.Min(tr.N - 1, c0 + rows);
                var b = new MeshBuilder();
                var normals = new List<Vector3>();
                for (int i = c0; i <= c1; i++)
                {
                    float s = i * Track.Step, half = tr.Width[i] * 0.5f;
                    for (int k = 0; k <= cols; k++)
                    {
                        float x = -half + 2 * half * k / cols;
                        b.Add(tr.World(s, x, tr.TrailY(s, x) + 0.03f), new Vector2(k / (float)cols, s / 14f));
                        normals.Add(TrailNormal(s, x));
                    }
                }
                for (int r = 0; r < c1 - c0; r++)
                for (int k = 0; k < cols; k++)
                {
                    int a = r * (cols + 1) + k;
                    b.Quad(a, a + 1, a + cols + 2, a + cols + 1, Vector3.up);
                }
                var mesh = b.Build("Trail", false);
                mesh.SetNormals(normals);
                MeshKit.Obj("Trail", parent, mesh, _trail, false, true).isStatic = true;
            }
        }

        void Verges()
        {
            var tr = Track;
            const int rows = 50;
            float[] offs = { 0, 1.2f, 3, 5.5f, 9 };
            int cols = offs.Length + 1;
            var parent = new GameObject("Verges").transform;
            parent.SetParent(transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int c0 = 0; c0 < tr.N - 1; c0 += rows)
                {
                    int c1 = Mathf.Min(tr.N - 1, c0 + rows);
                    var b = new MeshBuilder();
                    var normals = new List<Vector3>();
                    for (int i = c0; i <= c1; i++)
                    {
                        float s = i * Track.Step, half = tr.Width[i] * 0.5f;
                        var right = tr.Right(s);
                        for (int k = 0; k < cols; k++)
                        {
                            bool skirt = k == cols - 1;
                            float x = side * (half + offs[Mathf.Min(k, offs.Length - 1)]);
                            float y = tr.TrailY(s, x) + (skirt ? -1.6f : k == 0 ? 0.0f : 0.0f);
                            var p = tr.World(s, x, y);
                            b.Add(p, Field.Uv(p.x, p.z));
                            normals.Add(skirt ? right * side : TrailNormal(s, x));
                        }
                    }
                    for (int r = 0; r < c1 - c0; r++)
                    for (int k = 0; k < cols - 1; k++)
                    {
                        int a = r * cols + k;
                        var facing = k == cols - 2 ? tr.Right(tr.SampleS(c0 + r)) * side + Vector3.up * 0.2f : Vector3.up;
                        b.Quad(a, a + 1, a + cols + 1, a + cols, facing);
                    }
                    var mesh = b.Build("Verge", false);
                    mesh.SetNormals(normals);
                    MeshKit.Obj("Verge", parent, mesh, _terrain, false, true).isStatic = true;
                }
            }
        }

        // ------------------------------------------------------------ jumps

        void Ramps()
        {
            var tr = Track;
            var wood = new MeshBuilder();
            var dirt = new MeshBuilder();
            var deco = new MeshBuilder();
            foreach (var r in tr.Ramps)
            {
                var b = r.Wood ? wood : dirt;
                const int steps = 6;
                float width = r.X1 - r.X0;
                int top0 = b.Count;
                for (int q = 0; q <= steps; q++)
                {
                    float s = r.S0 + r.Len * q / steps, lift = r.Height * q / steps + 0.04f;
                    float v = r.Wood ? q / (float)steps * 2.2f : s / 14f;
                    b.Add(tr.World(s, r.X0, tr.TrailY(s, r.X0) + lift), new Vector2(0, v));
                    b.Add(tr.World(s, r.X1, tr.TrailY(s, r.X1) + lift), new Vector2(r.Wood ? width / 1.4f : 1, v));
                }
                for (int q = 0; q < steps; q++)
                {
                    int a = top0 + q * 2;
                    b.Quad(a, a + 1, a + 3, a + 2, Vector3.up);
                }
                // Side walls and the lip face.
                foreach (float x in new[] { r.X0, r.X1 })
                {
                    float side = x == r.X0 ? -1 : 1;
                    var outward = tr.Right(r.S0) * side;
                    int w0 = b.Count;
                    for (int q = 0; q <= steps; q++)
                    {
                        float s = r.S0 + r.Len * q / steps;
                        float baseY = tr.TrailY(s, x);
                        b.Add(tr.World(s, x, baseY - 0.3f), new Vector2(q / (float)steps, 0));
                        b.Add(tr.World(s, x, baseY + r.Height * q / steps + 0.04f), new Vector2(q / (float)steps, 0.3f + r.Height * q / steps * 0.4f));
                    }
                    for (int q = 0; q < steps; q++)
                    {
                        int a = w0 + q * 2;
                        b.Quad(a, a + 2, a + 3, a + 1, outward);
                    }
                }
                {
                    var fwd = tr.Forward(r.Lip);
                    var p0 = tr.World(r.Lip, r.X0, tr.TrailY(r.Lip, r.X0) - 0.3f);
                    var p1 = tr.World(r.Lip, r.X1, tr.TrailY(r.Lip, r.X1) - 0.3f);
                    var p2 = tr.World(r.Lip, r.X1, tr.TrailY(r.Lip, r.X1) + r.Height + 0.04f);
                    var p3 = tr.World(r.Lip, r.X0, tr.TrailY(r.Lip, r.X0) + r.Height + 0.04f);
                    b.Quad(p0, p1, p2, p3, fwd, new Color32(255, 255, 255, 255), Vector2.zero, new Vector2(width / 1.4f, 0.8f));
                }
                if (r.Wood)
                {
                    // Frame posts behind the lip and a striped lip edge.
                    var post = Mats.Hex(0x4A3220);
                    for (float x = r.X0 + 0.15f; x <= r.X1 - 0.1f; x += Mathf.Max(0.8f, width / 4))
                    {
                        var top = tr.World(r.Lip + 0.12f, x, tr.TrailY(r.Lip, x) + r.Height - 0.05f);
                        deco.Beam(top + Vector3.down * (r.Height + 0.3f), top, 0.16f, 0.16f, post);
                    }
                    var l0 = tr.World(r.Lip - 0.1f, r.X0, tr.TrailY(r.Lip, r.X0) + r.Height + 0.07f);
                    var l1 = tr.World(r.Lip - 0.1f, r.X1, tr.TrailY(r.Lip, r.X1) + r.Height + 0.07f);
                    deco.Beam(l0, l1, 0.2f, 0.06f, Mats.Hex(0xFFC400));
                }
                else
                {
                    // Dirt kicker: a few stones at the base.
                    var rnd = new System.Random((int)(r.S0 * 10));
                    for (int k = 0; k < 4; k++)
                    {
                        float x = k % 2 == 0 ? r.X0 - 0.4f : r.X1 + 0.4f;
                        float s = r.S0 + r.Len * (0.3f + 0.4f * (float)rnd.NextDouble());
                        Props.Boulder(deco, tr.WorldOnGround(s, x, -0.1f), 0.45f, Th, rnd);
                    }
                }
            }
            // Rock ledges at drops.
            foreach (float s in tr.Drops)
            {
                var rnd = new System.Random((int)(s * 7));
                float half = tr.WidthAt(s) * 0.5f;
                for (float x = -half - 1.5f; x <= half + 1.5f; x += 1.6f)
                {
                    var at = tr.World(s + 1.6f, x, tr.TrailY(s + 1.6f, x) + 0.2f);
                    Props.Boulder(deco, at, 0.85f, Th, rnd, 1.1f);
                }
                foreach (float x in new[] { -half - 3.2f, half + 3.2f })
                    Props.Boulder(deco, tr.World(s - 1, x, tr.TrailY(s - 1, x) - 0.3f), 1.6f, Th, rnd, 0.9f);
            }
            var parent = new GameObject("Jumps").transform;
            parent.SetParent(transform, false);
            if (wood.Count > 0) MeshKit.Obj("WoodRamps", parent, wood.Build("WoodRamps"), _planks);
            if (dirt.Count > 0) MeshKit.Obj("DirtRamps", parent, dirt.Build("DirtRamps"), _trail);
            if (deco.Count > 0) MeshKit.Obj("RampDeco", parent, deco.Build("RampDeco"), Palette.Lit);
        }

        // ------------------------------------------------------------ dressing

        void TrailDressing()
        {
            var tr = Track;
            var b = new MeshBuilder();
            var tape = new MeshBuilder();
            float yawOf(float s) => tr.HeadAt(s) * Mathf.Rad2Deg;

            foreach (var o in tr.Obstacles) Props.Obstacle(b, tr.WorldOnGround(o.S, o.X, -0.05f), yawOf(o.S), o, Th);

            var red = Palette.Uv(Mats.Hex(0xE53935));
            var white = Palette.Uv(Mats.Hex(0xF5F5F5));
            var stakeC = Mats.Hex(0xEDEDED);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3? prev = null;
                int seg = 0;
                for (float s = tr.StartS + 26; s < tr.FinishS - 14; s += 7)
                {
                    float half = tr.WidthAt(s) * 0.5f;
                    float k = tr.CurvAt(s);
                    bool outside = Mathf.Abs(k) > 0.02f && Mathf.Sign(k) == -side;
                    float x = side * (half + 2.6f);
                    var foot = tr.World(s, x, tr.TrailY(s, x));
                    if (outside)
                    {
                        // Hay bales on the outside of hard corners.
                        for (float q = 0; q < 7; q += 1.35f)
                        {
                            float sq = s + q;
                            float hx = side * (tr.WidthAt(sq) * 0.5f + 2.3f);
                            Props.HayBale(b, tr.World(sq, hx, tr.TrailY(sq, hx) - 0.05f), yawOf(sq));
                        }
                        prev = null;
                        continue;
                    }
                    b.Beam(foot + Vector3.down * 0.3f, foot + Vector3.up * 1.0f, 0.06f, 0.06f, stakeC);
                    var top = foot + Vector3.up * 0.88f;
                    if (prev != null)
                    {
                        var a = prev.Value;
                        var mid = (a + top) * 0.5f + Vector3.down * 0.12f;
                        var uv = seg++ % 2 == 0 ? red : white;
                        foreach (var (p, q) in new[] { (a, mid), (mid, top) })
                        {
                            tape.Panel(p, q, q + Vector3.up * 0.07f, p + Vector3.up * 0.07f, uv, Vector3.Cross(q - p, Vector3.up));
                        }
                    }
                    prev = top;
                }
            }
            foreach (var (s, dir) in tr.Chevrons)
            {
                int side = -dir;
                float x = side * (tr.WidthAt(s) * 0.5f + 3.6f);
                Props.Chevron(b, tr.World(s, x, tr.TrailY(s, x)), yawOf(s) + 180, -dir);
            }
            var parent = new GameObject("Dressing").transform;
            parent.SetParent(transform, false);
            MeshKit.Obj("Props", parent, b.Build("TrailProps"), Palette.Lit);
            MeshKit.Obj("Tape", parent, tape.Build("Tape"), Palette.LitDouble, false, true);
        }

        void Gates()
        {
            var tr = Track;
            var parent = new GameObject("Gates").transform;
            parent.SetParent(transform, false);
            Gate(parent, tr.StartS + 7, "FREERIDE FURY", Mats.Hex(0xFF5A1F), false);
            Gate(parent, tr.FinishS, "FINISH", Mats.Hex(0x2A9DF4), true);

            // Start hut and fences behind the start line.
            var b = new MeshBuilder();
            float s0 = 1;
            float half = tr.WidthAt(s0) * 0.5f + 1;
            var fwd = tr.Forward(s0);
            var right = tr.Right(s0);
            var c = tr.WorldOnGround(-4, 0);
            b.Xf = Matrix4x4.TRS(c, Quaternion.LookRotation(fwd), Vector3.one);
            b.Box(new Vector3(0, 1.4f, -3), new Vector3(half, 1.4f, 2.2f), Mats.Hex(0x6B4A30));
            b.Box(new Vector3(0, 3.0f, -3), new Vector3(half + 0.5f, 0.2f, 2.6f), Mats.Hex(0x3A3A40));
            b.Box(new Vector3(0, 1.0f, -0.6f), new Vector3(half, 0.12f, 0.12f), Mats.Hex(0xE53935));
            b.Xf = Matrix4x4.identity;
            // Finish area: fence panels.
            var fence = Mats.Hex(0xB0B6BE);
            for (int side = -1; side <= 1; side += 2)
            for (float s = tr.FinishS - 40; s < tr.Length - 6; s += 2.4f)
            {
                float x = side * (tr.WidthAt(s) * 0.5f + 1.6f);
                var p = tr.World(s, x, tr.TrailY(s, x));
                var q = tr.World(s + 2.3f, x, tr.TrailY(s + 2.3f, x));
                b.Beam(p, p + Vector3.up * 1.1f, 0.05f, 0.05f, fence);
                b.Beam(p + Vector3.up * 1.05f, q + Vector3.up * 1.05f, 0.05f, 0.05f, fence);
                b.Beam(p + Vector3.up * 0.15f, q + Vector3.up * 0.15f, 0.05f, 0.05f, fence);
                var panel = (int)(s / 2.4f) % 3 == 0 ? Mats.Hex(0xFF5A1F) : (int)(s / 2.4f) % 3 == 1 ? Mats.Hex(0x15171C) : Mats.Hex(0x2A9DF4);
                b.Xf = Matrix4x4.identity;
                b.Panel(p + Vector3.up * 0.2f, q + Vector3.up * 0.2f, q + Vector3.up * 1.0f, p + Vector3.up * 1.0f, Palette.Uv(panel), -right * side);
                b.Panel(p + Vector3.up * 0.2f, q + Vector3.up * 0.2f, q + Vector3.up * 1.0f, p + Vector3.up * 1.0f, Palette.Uv(panel.Darken(0.3f)), right * side);
            }
            // End barrier.
            {
                float s = tr.Length - 6;
                float hw = tr.WidthAt(s) * 0.5f + 1.6f;
                for (float x = -hw; x < hw; x += 1.3f) Props.HayBale(b, tr.World(s, x + 0.65f, tr.TrailY(s, x + 0.65f)), tr.HeadAt(s) * Mathf.Rad2Deg);
            }
            MeshKit.Obj("StartFinish", parent, b.Build("StartFinish"), Palette.Lit);

            // Checkered finish line.
            var line = new MeshBuilder();
            float fh = tr.WidthAt(tr.FinishS) * 0.5f;
            for (int k = 0; k < 8; k++)
            {
                float x0 = -fh + 2 * fh * k / 8, x1 = -fh + 2 * fh * (k + 1) / 8;
                float sa = tr.FinishS - 0.6f, sb = tr.FinishS + 0.6f;
                line.Quad(tr.World(sa, x0, tr.TrailY(sa, x0) + 0.06f), tr.World(sa, x1, tr.TrailY(sa, x1) + 0.06f),
                    tr.World(sb, x1, tr.TrailY(sb, x1) + 0.06f), tr.World(sb, x0, tr.TrailY(sb, x0) + 0.06f), Vector3.up, new Color32(255, 255, 255, 255),
                    new Vector2(k / 8f * 2, 0), new Vector2((k + 1) / 8f * 2, 1));
            }
            MeshKit.Obj("FinishLine", parent, line.Build("FinishLine"), Mats.Lit(Color.white, 0.2f, Tex.Checker()), false, true);
        }

        void Gate(Transform parent, float s, string text, Color accent, bool finish)
        {
            var tr = Track;
            float half = tr.WidthAt(s) * 0.5f + 1.4f;
            var fwd = tr.Forward(s);
            var left = tr.WorldOnGround(s, -half);
            var right = tr.WorldOnGround(s, half);
            float top = Mathf.Max(left.y, right.y) + 5.6f;
            var b = new MeshBuilder();
            var lt = new Vector3(left.x, top, left.z);
            var rt = new Vector3(right.x, top, right.z);
            b.Cylinder(left + Vector3.down * 0.5f, lt, 0.55f, 0.5f, 10, accent);
            b.Cylinder(right + Vector3.down * 0.5f, rt, 0.55f, 0.5f, 10, accent);
            b.Cylinder(lt + (lt - rt).normalized * 0.4f, rt + (rt - lt).normalized * 0.4f, 0.55f, 0.55f, 10, accent.Darken(0.15f));
            foreach (var p in new[] { lt, rt }) b.Rock(p, new Vector3(0.62f, 0.62f, 0.62f), 3, accent.Darken(0.15f), 1);
            MeshKit.Obj("Gate", parent, b.Build("Gate"), Palette.Lit);

            // Banner hanging under the bar, readable from both sides.
            var tex = Tex.Banner(text, finish ? Mats.Hex(0x15171C) : Mats.Hex(0x15171C), Color.white, accent, 1024, 128);
            float bw = Vector3.Distance(lt, rt) - 1.6f, bh = bw / 8f;
            var mid = (lt + rt) * 0.5f + Vector3.down * (0.55f + bh * 0.5f);
            var across = (rt - lt).normalized;
            var banner = new MeshBuilder();
            var p0 = mid - across * bw * 0.5f - Vector3.up * bh * 0.5f;
            var p1 = mid + across * bw * 0.5f - Vector3.up * bh * 0.5f;
            var p2 = p1 + Vector3.up * bh;
            var p3 = p0 + Vector3.up * bh;
            banner.Quad(p0 - fwd * 0.05f, p1 - fwd * 0.05f, p2 - fwd * 0.05f, p3 - fwd * 0.05f, -fwd, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            banner.Quad(p1 + fwd * 0.05f, p0 + fwd * 0.05f, p3 + fwd * 0.05f, p2 + fwd * 0.05f, fwd, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            MeshKit.Obj("Banner", parent, banner.Build("Banner"), Mats.Lit(Color.white, 0.2f, tex), false, true);
        }

        // ------------------------------------------------------------ scatter

        void Scatter()
        {
            var tr = Track;
            var f = Field;
            float qf = _q == GraphicsQuality.High ? 1f : _q == GraphicsQuality.Medium ? 0.62f : 0.35f;
            float dens = Th.TreeDensity * qf;
            var rnd = new System.Random(tr.Def.Seed * 31 + 5);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var tiles = new Dictionary<long, MeshBuilder>();
            var glowTiles = new Dictionary<long, MeshBuilder>();
            MeshBuilder Tile(Dictionary<long, MeshBuilder> d, Vector3 p)
            {
                long key = ((long)Mathf.FloorToInt(p.x / 180f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 180f);
                if (!d.TryGetValue(key, out var mb)) d[key] = mb = new MeshBuilder();
                return mb;
            }

            void Put(Vector3 p, float band)
            {
                if (f.Clearance(p.x, p.z) < 3.2f) return;
                p.y = f.HeightAt(p.x, p.z);
                var b = Tile(tiles, p);
                double roll = rnd.NextDouble();
                switch (Th.Biome)
                {
                    case Biome.Forest:
                        if (roll < 0.1) Props.Boulder(b, p, R(0.6f, 2.2f), Th, rnd);
                        else if (roll < 0.25) Props.Broadleaf(b, p, R(6, 10), Th, rnd);
                        else Props.Pine(b, p, R(7, 15), Th, rnd);
                        break;
                    case Biome.Autumn:
                        if (roll < 0.1) Props.Boulder(b, p, R(0.6f, 2f), Th, rnd);
                        else if (roll < 0.28) Props.Pine(b, p, R(7, 13), Catalog.Forest, rnd);
                        else Props.Broadleaf(b, p, R(6, 11), Th, rnd);
                        break;
                    case Biome.Glacier:
                        if (roll < 0.3) Props.Boulder(b, p, R(0.8f, 2.6f), Th, rnd);
                        else Props.Pine(b, p, R(6, 13), Th, rnd, true);
                        break;
                    case Biome.Canyon:
                        if (roll < 0.35) Props.Cactus(b, p, R(2.4f, 4.6f), rnd);
                        else if (roll < 0.8) Props.Boulder(b, p, R(0.8f, 3.4f), Th, rnd, R(0.5f, 1.2f));
                        else Props.Broadleaf(b, p, R(2.5f, 4), Th, rnd);
                        break;
                    default:
                        if (roll < 0.45) Props.DeadTree(b, p, R(5, 9), Th, rnd);
                        else if (roll < 0.93 || band < 20) Props.Boulder(b, p, R(0.8f, 3f), Th, rnd);
                        else Props.Vent(b, Tile(glowTiles, p), p, R(1.4f, 2.6f), Th, rnd);
                        break;
                }
            }

            // Along the course in three bands.
            for (int i = 0; i < tr.N; i++)
            {
                float s = i * Track.Step;
                float half = tr.Width[i] * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rnd.NextDouble() < 0.34 * dens) Put(tr.World(s + R(-1, 1), side * (half + R(5, 18)), 0), 10);
                    if (rnd.NextDouble() < 0.38 * dens) Put(tr.World(s + R(-1, 1), side * (half + R(18, 60)), 0), 40);
                    if (rnd.NextDouble() < 0.3 * dens) Put(tr.World(s + R(-1, 1), side * (half + R(60, 170)), 0), 100);
                }
            }
            // Forest patches on the far hillsides.
            int far = Mathf.RoundToInt(1400 * dens);
            for (int k = 0; k < far; k++)
            {
                float wx = f.X0 + R(0, f.SizeX), wz = f.Z0 + R(0, f.SizeZ);
                if (Tex.Fbm(wx * 0.004f, wz * 0.004f, 3) < 0.48f) continue;
                if (f.Clearance(wx, wz) < 150) continue;
                Put(new Vector3(wx, 0, wz), 200);
            }
            var parent = new GameObject("Scenery").transform;
            parent.SetParent(transform, false);
            foreach (var kv in tiles) MeshKit.Obj("Tile", parent, kv.Value.Build("Scenery"), Palette.Lit).isStatic = true;
            foreach (var kv in glowTiles) MeshKit.Obj("Glow", parent, kv.Value.Build("Lava"), Palette.Glow, false, false).isStatic = true;
        }

        void Crowds()
        {
            var tr = Track;
            var parent = new GameObject("Crowds").transform;
            parent.SetParent(transform, false);
            var rnd = new System.Random(tr.Def.Seed + 77);
            bool cold = Th.Biome == Biome.Glacier;
            int per = _q == GraphicsQuality.Low ? 7 : _q == GraphicsQuality.Medium ? 11 : 16;
            foreach (float cs in tr.Crowds)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var b = new MeshBuilder();
                    for (int k = 0; k < per; k++)
                    {
                        float s = cs + (float)(rnd.NextDouble() * 2 - 1) * 9;
                        float half = tr.WidthAt(s) * 0.5f;
                        float x = side * (half + 3.4f + (float)rnd.NextDouble() * 3.2f);
                        var p = tr.World(s, x, tr.TrailY(s, x) - 0.05f);
                        float yaw = tr.HeadAt(s) * Mathf.Rad2Deg + (side > 0 ? -90 : 90) - side * 25;
                        Props.Spectator(b, p, yaw, rnd, cold);
                    }
                    var go = MeshKit.Obj("Crowd", parent, b.Build("Crowd"), Palette.Lit);
                    go.AddComponent<CrowdBob>().Phase = (float)rnd.NextDouble() * 6;
                }
            }
        }

        // ------------------------------------------------------------ horizon

        void Mountains()
        {
            var f = Field;
            var rnd = new System.Random(Track.Def.Seed + 501);
            var b = new MeshBuilder();
            var glow = new MeshBuilder();
            var centre = new Vector3(f.X0 + f.SizeX * 0.5f, 0, f.Z0 + f.SizeZ * 0.5f);
            float ring = Mathf.Max(f.SizeX, f.SizeZ) * 0.5f + 150;
            float baseY = f.MinY - 40;
            int peaks = 34;
            for (int k = 0; k < peaks; k++)
            {
                float a = k / (float)peaks * Mathf.PI * 2 + (float)rnd.NextDouble() * 0.15f;
                float rr = ring + (float)rnd.NextDouble() * 500;
                var p = centre + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                p.y = baseY;
                float hgt = 260 + (float)rnd.NextDouble() * 380;
                float rad = 220 + (float)rnd.NextDouble() * 260;
                var col = Color.Lerp(Th.MountainNear, Th.MountainFar, (rr - ring) / 500f);
                if (Th.Biome == Biome.Canyon)
                {
                    // Layered mesas.
                    float hh = hgt * 0.45f;
                    for (int l = 0; l < 3; l++)
                    {
                        var c = l % 2 == 0 ? col : Color.Lerp(col, Th.Cap, 0.4f);
                        float y0 = p.y + hh * l / 3f, y1 = p.y + hh * (l + 1) / 3f;
                        b.Cylinder(new Vector3(p.x, y0, p.z), new Vector3(p.x, y1, p.z), rad * (1 - l * 0.08f), rad * (0.94f - l * 0.08f), 9, c, l == 2, a);
                    }
                    continue;
                }
                var tip = p + new Vector3((float)rnd.NextDouble() * 60 - 30, hgt, (float)rnd.NextDouble() * 60 - 30);
                b.Cylinder(p, tip, rad, 0, 7 + rnd.Next(3), col, false, a);
                // Cap from 62% of the height up.
                float t0 = 0.62f + (float)rnd.NextDouble() * 0.1f;
                var capBase = Vector3.Lerp(p, tip, t0);
                b.Cylinder(capBase, tip + Vector3.up * 2, rad * (1 - t0) * 1.06f, 0, 7, Th.MountainCap, false, a);
            }
            if (Th.Biome == Biome.Volcano)
            {
                // The volcano itself, beyond the finish, with a glowing crater.
                var end = Track.Pos[Track.N - 1];
                var dir = (end - Track.Pos[0]);
                dir.y = 0;
                var v = end + dir.normalized * 900;
                v.y = baseY;
                float vh = 820;
                var rim = v + Vector3.up * vh;
                b.Cylinder(v, rim, 900, 140, 14, Th.MountainNear, false);
                glow.Cylinder(rim - Vector3.up * 6, rim + Vector3.up * 1, 128, 110, 14, Mats.Hex(0xFF6A1A), true);
                VolcanoTop = rim;
            }
            var parent = new GameObject("Horizon").transform;
            parent.SetParent(transform, false);
            MeshKit.Obj("Mountains", parent, b.Build("Mountains"), Palette.Lit, false, false);
            if (glow.Count > 0) MeshKit.Obj("Crater", parent, glow.Build("Crater"), Palette.Glow, false, false);
        }

        /// <summary>Crater rim of the volcano backdrop (Ember Peak), for its smoke plume.</summary>
        public Vector3? VolcanoTop { get; private set; }
    }

    /// <summary>Spectator groups bounce while they cheer.</summary>
    public sealed class CrowdBob : MonoBehaviour
    {
        public float Phase;
        Vector3 _base;

        void Start() => _base = transform.localPosition;

        void Update()
        {
            float t = Time.time * 7 + Phase;
            transform.localPosition = _base + Vector3.up * (Mathf.Abs(Mathf.Sin(t)) * 0.07f);
        }
    }
}
