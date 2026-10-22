using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Heightfield around a track. Each grid point knows its nearest point on the trail
    /// (distance, lateral side, track distance), so the valley follows the course and
    /// trees and rocks can stay clear of every section of it.
    /// </summary>
    public sealed class TerrainField
    {
        public readonly Track Track;
        public readonly float X0, Z0, Cell;
        public readonly int Nx, Nz;
        public readonly float[] H;
        /// <summary>Signed lateral offset to the nearest trail point (metres, right positive) and its track distance.</summary>
        public readonly float[] Lat, S;
        public float MinY, MaxY;
        readonly float _sideSeed, _bigSeed;

        public TerrainField(Track track, float cell, float margin)
        {
            Track = track;
            Cell = cell;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in track.Pos)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            X0 = minX - margin;
            Z0 = minZ - margin;
            Nx = Mathf.CeilToInt((maxX - minX + margin * 2) / cell) + 1;
            Nz = Mathf.CeilToInt((maxZ - minZ + margin * 2) / cell) + 1;
            H = new float[Nx * Nz];
            Lat = new float[Nx * Nz];
            S = new float[Nx * Nz];
            _sideSeed = track.Def.Seed * 0.013f % 50;
            _bigSeed = track.Def.Seed * 0.007f % 70;
            Nearest(margin);
            Heights();
        }

        public Vector3 GridPos(int i, int j) => new Vector3(X0 + i * Cell, H[j * Nx + i], Z0 + j * Cell);

        /// <summary>Splats every trail sample onto nearby grid points, keeping the closest.</summary>
        void Nearest(float margin)
        {
            var best = new float[Nx * Nz];
            for (int k = 0; k < best.Length; k++) best[k] = float.MaxValue;
            float reach = margin + 60;
            int rc = Mathf.CeilToInt(reach / Cell);
            var tr = Track;
            for (int n = 0; n < tr.N; n++)
            {
                var c = tr.Pos[n];
                float h = tr.Head[n];
                var fw = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
                var rt = new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h));
                int ci = Mathf.RoundToInt((c.x - X0) / Cell), cj = Mathf.RoundToInt((c.z - Z0) / Cell);
                for (int j = Mathf.Max(0, cj - rc); j <= Mathf.Min(Nz - 1, cj + rc); j++)
                for (int i = Mathf.Max(0, ci - rc); i <= Mathf.Min(Nx - 1, ci + rc); i++)
                {
                    float dx = X0 + i * Cell - c.x, dz = Z0 + j * Cell - c.z;
                    float d2 = dx * dx + dz * dz;
                    int k = j * Nx + i;
                    if (d2 >= best[k]) continue;
                    best[k] = d2;
                    float along = Mathf.Clamp(dx * fw.x + dz * fw.z, -Track.Step * 0.6f, Track.Step * 0.6f);
                    float s = n * Track.Step + along;
                    // Beyond the ends the course continues straight.
                    if (n == 0) s = Mathf.Min(s, 0);
                    Lat[k] = dx * rt.x + dz * rt.z;
                    S[k] = Mathf.Clamp(s, 0, tr.Length);
                }
            }
            for (int k = 0; k < best.Length; k++)
            {
                if (best[k] < float.MaxValue) continue;
                // Far corners: use the nearest sample by brute force.
                int i = k % Nx, j = k / Nx;
                float wx = X0 + i * Cell, wz = Z0 + j * Cell, bd = float.MaxValue;
                for (int n = 0; n < tr.N; n += 3)
                {
                    float dx = wx - tr.Pos[n].x, dz = wz - tr.Pos[n].z, d2 = dx * dx + dz * dz;
                    if (d2 >= bd) continue;
                    bd = d2;
                    float h = tr.Head[n];
                    Lat[k] = dx * Mathf.Cos(h) - dz * Mathf.Sin(h);
                    S[k] = n * Track.Step;
                }
            }
        }

        /// <summary>Terrain height from the nearest trail point (analytic, used for the grid).</summary>
        float Profile(float wx, float wz, float s, float lat)
        {
            var tr = Track;
            float half = tr.WidthAt(s) * 0.5f;
            float d = Mathf.Abs(lat);
            const float verge = 9;
            if (d <= half + verge)
            {
                // Under the trail and verge meshes: follow them, a little lower.
                float sink = d <= half ? 0.8f : Mathf.Lerp(0.6f, 0.05f, (d - half) / verge);
                return tr.TrailY(s, lat) - sink;
            }
            float edgeY = tr.TrailY(s, Mathf.Sign(lat) * (half + verge)) - 0.05f;
            float out_ = d - half - verge;
            // One side of the valley climbs, the other rolls away gently; it changes slowly along the course.
            float side = (Tex.Noise(s * 0.004f + _sideSeed, 1.3f) - 0.5f) * 2;
            float slope = 0.3f + 0.28f * side * Mathf.Sign(lat);
            float hill = out_ * slope + out_ * out_ * 0.0009f;
            float bumps = (Tex.Fbm(wx * 0.018f, wz * 0.018f, 3) - 0.5f) * 7 * Tex.SmoothStep(0, 30, out_);
            float big = Tex.Fbm(wx * 0.0028f + _bigSeed, wz * 0.0028f, 4) * 170 * Tex.SmoothStep(30, 320, out_);
            return edgeY + hill + bumps + big;
        }

        void Heights()
        {
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            for (int j = 0; j < Nz; j++)
            for (int i = 0; i < Nx; i++)
            {
                int k = j * Nx + i;
                float y = Profile(X0 + i * Cell, Z0 + j * Cell, S[k], Lat[k]);
                H[k] = y;
                MinY = Mathf.Min(MinY, y);
                MaxY = Mathf.Max(MaxY, y);
            }
        }

        /// <summary>Height of the rendered grid surface (same triangle split as the mesh).</summary>
        public float HeightAt(float wx, float wz)
        {
            float fx = (wx - X0) / Cell, fz = (wz - Z0) / Cell;
            int i = Mathf.Clamp((int)fx, 0, Nx - 2), j = Mathf.Clamp((int)fz, 0, Nz - 2);
            float tx = Mathf.Clamp01(fx - i), tz = Mathf.Clamp01(fz - j);
            float h00 = H[j * Nx + i], h10 = H[j * Nx + i + 1], h01 = H[(j + 1) * Nx + i], h11 = H[(j + 1) * Nx + i + 1];
            // Triangles (00, 01, 11) and (00, 11, 10).
            if (tz > tx) return h00 + (h11 - h01) * tx + (h01 - h00) * tz;
            return h00 + (h10 - h00) * tx + (h11 - h10) * tz;
        }

        /// <summary>Distance beyond the trail edge to the nearest section (negative = on the trail).</summary>
        public float Clearance(float wx, float wz)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt((wx - X0) / Cell), 0, Nx - 1), j = Mathf.Clamp(Mathf.RoundToInt((wz - Z0) / Cell), 0, Nz - 1);
            int k = j * Nx + i;
            float half = Track.WidthAt(S[k]) * 0.5f;
            // Grid rounding error is up to ~0.7 cells.
            return Mathf.Abs(Lat[k]) - half - Cell * 0.7f;
        }

        public Vector3 NormalAt(float wx, float wz)
        {
            float e = Cell * 0.5f;
            float hx = HeightAt(wx + e, wz) - HeightAt(wx - e, wz);
            float hz = HeightAt(wx, wz + e) - HeightAt(wx, wz - e);
            return new Vector3(-hx, 2 * e, -hz).normalized;
        }

        public Vector2 Uv(float wx, float wz) => new Vector2((wx - X0) / ((Nx - 1) * Cell), (wz - Z0) / ((Nz - 1) * Cell));

        public float SizeX => (Nx - 1) * Cell;
        public float SizeZ => (Nz - 1) * Cell;
    }
}
