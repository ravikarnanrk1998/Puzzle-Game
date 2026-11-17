using System;
using System.Collections;
using System.Collections.Generic;
using UA.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Grid = UA.Core.Grid;

namespace UA.Game
{
    /// <summary>A built stage: geometry, colliders, lights, NavMesh and the world positions of everything the mission needs.</summary>
    public sealed class Level
    {
        public const float Cell = StageGen.CellSize;
        public StagePlan Plan;
        public GameObject Root;
        public Bounds Bounds;
        public float Ceiling;
        public bool Indoor;
        public Vector3 Start;
        public float StartYaw;
        public readonly List<Vector3> SpawnPoints = new List<Vector3>();
        public readonly List<Vector3> Reserved = new List<Vector3>();
        public NavMeshSurface Nav;
        public Atmosphere Atmosphere;
        public int DrawMeshes, LightCount, Colliders;
        public SurfaceKind FloorKind = SurfaceKind.Concrete;

        public Vector3 CellCenter(int cell) => new Vector3((Plan.Grid.X(cell) + 0.5f) * Cell, 0, (Plan.Grid.Y(cell) + 0.5f) * Cell);

        public Vector3 SpotPos(Spot s) => new Vector3((Plan.Grid.X(s.Cell) + s.Fx) * Cell, 0, (Plan.Grid.Y(s.Cell) + s.Fy) * Cell);

        /// <summary>Nearest point on the NavMesh (falls back to the input).</summary>
        public static Vector3 OnNav(Vector3 p, float radius = 3f)
        {
            if (NavMesh.SamplePosition(p, out var hit, radius, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        public void Destroy()
        {
            Look.ClearProbes();
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
        }
    }

    /// <summary>Turns a <see cref="StagePlan"/> grid into a dressed, lit, navigable 3D level.</summary>
    public static class LevelBuilder
    {
        const float C = Level.Cell;

        public static IEnumerator Build(StagePlan plan, Action<Level> done, Action<float, string> progress)
        {
            var lv = new Level { Plan = plan, Indoor = plan.Indoor };
            var g = plan.Grid;
            lv.Root = new GameObject("Level");
            lv.Bounds = new Bounds(new Vector3(g.W * C / 2, 3, g.H * C / 2), new Vector3(g.W * C, 6, g.H * C));
            var bd = plan.BiomeDef;
            lv.Ceiling = bd.Indoor ? bd.Ceiling : 0;
            lv.Atmosphere = Look.For(plan);
            var r = new Rng(Rng.Hash((long)plan.Seed, 991));
            var sink = new Sink(lv.Root.transform, g.W * C);
            var kit = new BiomeKit(plan, lv.Atmosphere);
            sink.Resolve = kit.Resolve;
            progress?.Invoke(0.05f, "Generating terrain");
            yield return null;

            // Reserved points (keep props off them).
            lv.Start = lv.CellCenter(plan.StartCell);
            lv.Reserved.Add(lv.Start);
            foreach (var s in plan.Loot) lv.Reserved.Add(lv.SpotPos(s));
            foreach (var s in plan.ObjectiveSpots) lv.Reserved.Add(lv.SpotPos(s));
            foreach (var s in plan.Hazards) lv.Reserved.Add(lv.SpotPos(s));
            // Face the start toward the most open direction.
            lv.StartYaw = FacingYaw(g, plan.StartCell);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long tPrep, tShell, tDress, tSink, tNav;
            Pix.ResetStats();
            kit.Prepare();
            tPrep = sw.ElapsedMilliseconds;
            progress?.Invoke(0.12f, "Building structures");
            yield return null;
            sw.Restart();
            Shell(lv, sink, kit, r);
            tShell = sw.ElapsedMilliseconds;
            progress?.Invoke(0.3f, "Placing wreckage");
            yield return null;
            var dresser = new Dresser(lv, sink, kit, r);
            tDress = 0;
            sw.Restart();
            for (int i = 0; i < g.Open.Length; i++)
            {
                if (!g.Open[i]) continue;
                dresser.DressCell(i);
                if (i % 24 == 0) { tDress += sw.ElapsedMilliseconds; yield return null; sw.Restart(); }
            }
            dresser.Finish();
            tDress += sw.ElapsedMilliseconds;
            progress?.Invoke(0.55f, "Lighting");
            yield return null;
            sw.Restart();
            lv.DrawMeshes = sink.Build();
            lv.LightCount = sink.Lights.Count;
            lv.Colliders = sink.ColliderCount;
            tSink = sw.ElapsedMilliseconds;
            progress?.Invoke(0.65f, "Computing navigation");
            yield return null;
            sw.Restart();

            // NavMesh from the physics colliders.
            var surf = lv.Root.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = 1 << Layers.World;
            surf.overrideVoxelSize = true;
            surf.voxelSize = 0.2f;
            surf.overrideTileSize = true;
            surf.tileSize = 128;
            surf.BuildNavMesh();
            lv.Nav = surf;
            tNav = sw.ElapsedMilliseconds;
            Log.I("LEVEL phases ms: textures " + tPrep + ", shell " + tShell + ", dress " + tDress + ", merge " + tSink + ", navmesh " + tNav + " | meshes " + lv.DrawMeshes + " lights " + lv.LightCount + " colliders " + lv.Colliders
                + " | " + Pix.Stats());
            progress?.Invoke(0.8f, "Placing infected");
            yield return null;

            foreach (int c in plan.SpawnCells)
            {
                var p = lv.CellCenter(c) + new Vector3(r.Range(-1.5f, 1.5f), 0, r.Range(-1.5f, 1.5f));
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) lv.SpawnPoints.Add(hit.position);
            }
            if (lv.SpawnPoints.Count == 0) lv.SpawnPoints.Add(Level.OnNav(lv.CellCenter(plan.ExitCell)));
            lv.Start = Level.OnNav(lv.Start, 4);
            progress?.Invoke(0.85f, "Capturing reflections");
            yield return null;
            sw.Restart();
            Look.Apply(lv.Atmosphere);
            int reflFrames = 0;
            foreach (var _ in Look.CaptureReflections(lv.Root.transform, lv.Bounds, lv.Indoor ? lv.Ceiling : 12)) { reflFrames++; yield return null; }
            Log.I("LEVEL reflections " + reflFrames + " frames, " + sw.ElapsedMilliseconds + " ms wall");
            progress?.Invoke(1f, "Ready");
            done(lv);
        }

        static float FacingYaw(Grid g, int cell)
        {
            int x = g.X(cell), y = g.Y(cell);
            int best = 0, bestRun = -1;
            int[] dx = { 0, 1, 0, -1 }, dy = { 1, 0, -1, 0 };
            for (int d = 0; d < 4; d++)
            {
                int run = 0;
                for (int k = 1; k < 8 && g.IsOpen(x + dx[d] * k, y + dy[d] * k); k++) run++;
                if (run > bestRun) { bestRun = run; best = d; }
            }
            return best * 90f;
        }

        // ================================================================== shell

        static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

        /// <summary>Floors, walls, ceilings, building facades and floor colliders.</summary>
        static void Shell(Level lv, Sink s, BiomeKit kit, Rng r)
        {
            var plan = lv.Plan;
            var g = plan.Grid;
            float H = lv.Indoor ? lv.Ceiling : 0;
            // Floor colliders: merged runs along x.
            for (int y = 0; y < g.H; y++)
            {
                int x = 0;
                while (x < g.W)
                {
                    if (!g.IsOpen(x, y)) { x++; continue; }
                    int x0 = x;
                    while (x < g.W && g.IsOpen(x, y)) x++;
                    float w = (x - x0) * C;
                    s.Box(new Vector3(x0 * C + w / 2, -0.25f, (y + 0.5f) * C), new Vector3(w, 0.5f, C), 0, kit.FloorSurface);
                }
            }
            for (int i = 0; i < g.Open.Length; i++)
            {
                if (!g.Open[i]) continue;
                int cx = g.X(i), cy = g.Y(i);
                float x0 = cx * C, z0 = cy * C;
                var center = new Vector3(x0 + C / 2, 0, z0 + C / 2);
                // Floor.
                string floor = kit.FloorFor(g, i);
                s.Part(floor, Matrix4x4.identity, center).Floor(0, x0, z0, C, C, 0, 1f / kit.FloorTile(floor));
                if (lv.Indoor) s.Part("ceiling", Matrix4x4.identity, center).Ceiling(0, x0, z0, C, C, H, 1f / 2.4f);
                // Edges.
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (g.IsOpen(nx, ny)) continue;
                    // Edge endpoints and inward normal.
                    Vector3 a, b;
                    var inward = new Vector3(-DX[d], 0, -DY[d]);
                    switch (d)
                    {
                        case 0: a = new Vector3(x0 + C, 0, z0); b = new Vector3(x0 + C, 0, z0 + C); break;
                        case 1: a = new Vector3(x0, 0, z0); b = new Vector3(x0, 0, z0 + C); break;
                        case 2: a = new Vector3(x0, 0, z0 + C); b = new Vector3(x0 + C, 0, z0 + C); break;
                        default: a = new Vector3(x0, 0, z0); b = new Vector3(x0 + C, 0, z0); break;
                    }
                    if (lv.Indoor) IndoorWall(lv, s, kit, r, a, b, inward, H, i, d);
                    else if (plan.Kind == LayoutKind.Streets) Facade(lv, s, kit, r, a, b, inward, g.Idx(nx, ny));
                    else Perimeter(lv, s, kit, r, a, b, inward, g, nx, ny);
                }
            }
            // Outdoor solid blocks inside yards: hangars / container stacks / thickets.
            if (!lv.Indoor && plan.Kind != LayoutKind.Streets) YardBlocks(lv, s, kit, r);
            if (plan.Kind == LayoutKind.Metro) MetroDetails(lv, s, kit, r);
            if (plan.Kind == LayoutKind.Arena && lv.Indoor) CofferedCeiling(lv, s);
            if (plan.Kind == LayoutKind.Streets) Sidewalks(lv, s, kit, r);
        }

        /// <summary>
        /// Solid backing behind a wall face: from the face 3 m into the closed cell (half of it), exactly one cell wide
        /// so it never pokes into open space. The face colliders are only 0.6-1 m thick; a player squeezed into a
        /// corner by a crowd could be pushed out of their far side and fall out of the level. With this backing the
        /// shortest way out of any overlap is always back into the room.
        /// </summary>
        static void Backing(Sink s, Vector3 mid, Vector3 inward, float height, SurfaceKind kind)
        {
            s.Box(mid - inward * 1.5f + Vector3.up * height / 2, new Vector3(C - 0.02f, height, 3f), Quaternion.LookRotation(inward), kind);
        }

        static void IndoorWall(Level lv, Sink s, BiomeKit kit, Rng r, Vector3 a, Vector3 b, Vector3 inward, float H, int cell, int d)
        {
            var mid = (a + b) / 2;
            bool shop = lv.Plan.Biome == Biome.Mall;
            if (shop)
            {
                s.Part("facadeG", Matrix4x4.identity, mid).Wall(0, a, b, 4f, inward, 1f / C);
                s.Part("wall", Matrix4x4.identity, mid).Wall(0, a + Vector3.up * 4, b + Vector3.up * 4, H - 4, inward, 1f / 4f, 0.85f);
            }
            else
            {
                // Full-height wall texture: u in metres / 4, v spans floor..ceiling (one tile vertically).
                var pb = s.Part("wall", Matrix4x4.identity, mid);
                var u = b - a;
                if (Vector3.Dot(Vector3.Cross(u, Vector3.up), inward) < 0) { var t = a; a = b; b = t; u = -u; }
                pb.Plane(0, a, u, Vector3.up * H, new Vector2(r.Range(0, 4f) / 4f, 0), new Vector2(r.Range(0, 4f) / 4f + C / 4f, 1));
            }
            // Baseboard and a ceiling cornice.
            var along = (b - a).normalized;
            float yaw = Quaternion.LookRotation(inward).eulerAngles.y;
            s.Part("trim", Matrix4x4.TRS(mid + inward * 0.03f + Vector3.up * 0.06f, Quaternion.LookRotation(inward), Vector3.one), mid).Box(0, Vector3.zero, new Vector3(C, 0.12f, 0.06f));
            s.Part("trim", Matrix4x4.TRS(mid + inward * 0.05f + Vector3.up * (H - 0.08f), Quaternion.LookRotation(inward), Vector3.one), mid).Box(0, Vector3.zero, new Vector3(C, 0.16f, 0.1f));
            // Wall collider (thick, behind the surface).
            s.Box(mid - inward * 0.3f + Vector3.up * H / 2, new Vector3(C + 0.6f, H + 0.2f, 0.6f), Quaternion.LookRotation(inward), kit.WallSurface);
            Backing(s, mid, inward, H + 0.2f, kit.WallSurface);
        }

        static void Facade(Level lv, Sink s, BiomeKit kit, Rng r, Vector3 a, Vector3 b, Vector3 inward, int buildingCell)
        {
            var mid = (a + b) / 2;
            var u = b - a;
            if (Vector3.Dot(Vector3.Cross(u, Vector3.up), inward) < 0) { var t = a; a = b; b = t; u = -u; }
            int floors = 2 + (int)(Rng.Hash((long)lv.Plan.Seed, buildingCell, 5) % 4);
            if (lv.Plan.Biome == Biome.Residential) floors = 1 + (int)(Rng.Hash((long)lv.Plan.Seed, buildingCell, 5) % 2);
            float gh = 4f, fh = 3.3f;
            float top = gh + floors * fh;
            string fg = "facadeG" + (buildingCell % kit.FacadeVariants), fu = "facadeU" + (buildingCell % kit.FacadeVariants);
            s.Part(fg, Matrix4x4.identity, mid).Plane(0, a, u, Vector3.up * gh, Vector2.zero, new Vector2(1, 1));
            s.Part(fu, Matrix4x4.identity, mid).Plane(0, a + Vector3.up * gh, u, Vector3.up * floors * fh, Vector2.zero, new Vector2(1, floors));
            // Cornice and ledges.
            var rot = Quaternion.LookRotation(inward);
            s.Part("concrete", Matrix4x4.TRS(mid + inward * 0.2f + Vector3.up * top, rot, Vector3.one), mid).Box(0, Vector3.zero, new Vector3(C, 0.5f, 0.45f));
            s.Part("concrete", Matrix4x4.TRS(mid + inward * 0.12f + Vector3.up * gh, rot, Vector3.one), mid).Box(0, Vector3.zero, new Vector3(C, 0.22f, 0.25f));
            // Fire escape / AC units / awning detail.
            float k = r.Float();
            if (k < 0.25f && floors >= 2)
            {
                for (int f = 1; f < floors; f++)
                    s.Part("metalDark", Matrix4x4.TRS(mid + inward * 0.6f + Vector3.up * (gh + f * fh), rot, Vector3.one), mid).Box(0, Vector3.zero, new Vector3(2.4f, 0.06f, 1.1f));
            }
            else if (k < 0.5f)
            {
                s.Part("metal", Matrix4x4.TRS(mid + inward * 0.35f + Vector3.up * (gh + fh * 0.4f), rot, Vector3.one), mid).Box(0, new Vector3(r.Range(-2f, 2f), 0, 0), new Vector3(0.9f, 0.6f, 0.6f));
            }
            else if (k < 0.7f)
            {
                s.Part(kit.AwningMat(r), Matrix4x4.TRS(mid + inward * 0.75f + Vector3.up * 3.3f, rot * Quaternion.Euler(-18, 0, 0), Vector3.one), mid).Box(0, Vector3.zero, new Vector3(C * 0.8f, 0.05f, 1.5f));
            }
            // Wall-mounted lamp.
            if (r.Chance(lv.Atmosphere.LightChance * 0.45f))
            {
                var lp = mid + inward * 0.35f + Vector3.up * 3.6f + u.normalized * r.Range(-2f, 2f);
                s.Part("metalDark", Matrix4x4.TRS(lp, rot, Vector3.one), lp).Box(0, Vector3.zero, new Vector3(0.3f, 0.2f, 0.5f));
                s.Part("emitSodium", Matrix4x4.TRS(lp + Vector3.down * 0.1f, rot, Vector3.one), lp).Box(0, Vector3.zero, new Vector3(0.22f, 0.02f, 0.35f));
                s.Light(lp + Vector3.down * 0.2f + inward * 0.2f, kit.Practical(r), 9, 2.6f, spot: true, angle: 130, flicker: r.Chance(0.3) ? 0.3f : 0, beam: true);
            }
            s.Box(mid - inward * 0.5f + Vector3.up * top / 2, new Vector3(C + 1f, top, 1f), rot, SurfaceKind.Concrete);
            Backing(s, mid, inward, top, SurfaceKind.Concrete);
        }

        static void Sidewalks(Level lv, Sink s, BiomeKit kit, Rng r)
        {
            var g = lv.Plan.Grid;
            float sw = 1.7f, h = 0.15f;
            for (int i = 0; i < g.Open.Length; i++)
            {
                if (!g.Open[i] || !g.Has(i, CellTag.Road)) continue;
                int cx = g.X(i), cy = g.Y(i);
                for (int d = 0; d < 4; d++)
                {
                    if (g.IsOpen(cx + DX[d], cy + DY[d])) continue;
                    var c = new Vector3((cx + 0.5f) * C, 0, (cy + 0.5f) * C) + new Vector3(DX[d], 0, DY[d]) * (C / 2 - sw / 2);
                    var size = d < 2 ? new Vector3(sw, h, C) : new Vector3(C, h, sw);
                    s.Part("sidewalk", Matrix4x4.identity, c).Box(0, c + Vector3.up * h / 2, size, 1f / 2f, true);
                    var curb = c - new Vector3(DX[d], 0, DY[d]) * (sw / 2);
                    s.Part("curb", Matrix4x4.identity, c).Box(0, curb + Vector3.up * h / 2, d < 2 ? new Vector3(0.15f, h + 0.01f, C) : new Vector3(C, h + 0.01f, 0.15f), 1, true);
                    s.Box(c + Vector3.up * h / 2, size, 0, SurfaceKind.Concrete);
                }
            }
        }

        static void Perimeter(Level lv, Sink s, BiomeKit kit, Rng r, Vector3 a, Vector3 b, Vector3 inward, Grid g, int nx, int ny)
        {
            bool boundary = nx <= 0 || ny <= 0 || nx >= g.W - 1 || ny >= g.H - 1;
            var mid = (a + b) / 2;
            var rot = Quaternion.LookRotation(inward);
            if (!boundary) return; // interior solid blocks are built by YardBlocks
            switch (lv.Plan.Biome)
            {
                case Biome.ForestCamp:
                    for (int i = 0; i < 3; i++) Props.PineTree(s, mid - inward * r.Range(1f, 3f) + (b - a).normalized * r.Range(-2.5f, 2.5f), r, false, r.Chance(0.3));
                    Props.Fence(s, a + inward * 0.3f, b + inward * 0.3f, 1.4f, false);
                    s.Box(mid - inward * 0.6f + Vector3.up * 3, new Vector3(C + 1, 6, 1), rot, SurfaceKind.Wood);
                    Backing(s, mid, inward, 6, SurfaceKind.Wood);
                    break;
                case Biome.Port:
                case Biome.Airport:
                    Props.Fence(s, a + inward * 0.2f, b + inward * 0.2f, 3f, true);
                    Backing(s, mid, inward, 4, SurfaceKind.Metal);
                    break;
                default:
                {
                    // Concrete perimeter wall with barbed wire.
                    float h = 3.2f;
                    var pb = s.Part("perimeter", Matrix4x4.identity, mid);
                    var u = b - a;
                    if (Vector3.Dot(Vector3.Cross(u, Vector3.up), inward) < 0) { var t = a; a = b; b = t; u = -u; }
                    pb.Plane(0, a, u, Vector3.up * h, Vector2.zero, new Vector2(C / 4f, 1));
                    s.Part("concrete", Matrix4x4.TRS(mid + Vector3.up * h, rot, Vector3.one), mid).Box(0, new Vector3(0, 0.05f, -0.15f), new Vector3(C, 0.12f, 0.35f));
                    for (int i = 0; i < 3; i++)
                        s.Part("metalDark", Matrix4x4.identity, mid).Cylinder(0, a + Vector3.up * (h + 0.25f + i * 0.12f) - inward * 0.1f, b + Vector3.up * (h + 0.25f + i * 0.12f) - inward * 0.1f, 0.008f, 0.008f, 4, false, false);
                    s.Box(mid - inward * 0.4f + Vector3.up * h / 2, new Vector3(C + 0.8f, h + 3, 0.8f), rot, SurfaceKind.Concrete);
                    Backing(s, mid, inward, h + 3, SurfaceKind.Concrete);
                    break;
                }
            }
        }

        static void YardBlocks(Level lv, Sink s, BiomeKit kit, Rng r)
        {
            var g = lv.Plan.Grid;
            for (int i = 0; i < g.Open.Length; i++)
            {
                if (g.Open[i]) continue;
                int x = g.X(i), y = g.Y(i);
                if (x <= 0 || y <= 0 || x >= g.W - 1 || y >= g.H - 1) continue;
                var c = new Vector3((x + 0.5f) * C, 0, (y + 0.5f) * C);
                switch (lv.Plan.Biome)
                {
                    case Biome.Port:
                    {
                        int stack = r.Range(1, 4);
                        float yaw = r.Chance(0.5) ? 0 : 90;
                        for (int k = 0; k < stack; k++) Props.Container(s, c + Vector3.up * k * 2.62f, yaw + r.Range(-3f, 3f), r, r.Range(0, 4), false);
                        s.Box(c + Vector3.up * 4, new Vector3(C, 8, C), 0, SurfaceKind.Metal);
                        break;
                    }
                    case Biome.ForestCamp:
                        for (int k = 0; k < 4; k++) Props.PineTree(s, c + new Vector3(r.Range(-2.5f, 2.5f), 0, r.Range(-2.5f, 2.5f)), r, false, r.Chance(0.25));
                        Props.Rock(s, c, r, r.Range(1.2f, 2f));
                        s.Box(c + Vector3.up * 3, new Vector3(C - 0.4f, 6, C - 0.4f), 0, SurfaceKind.Wood);
                        break;
                    default:
                    {
                        // Hangar / barracks block: corrugated walls and a flat roof.
                        float h = r.Range(4.5f, 7f);
                        for (int d = 0; d < 4; d++)
                        {
                            if (!g.IsOpen(x + DX[d], y + DY[d])) continue;
                            var n = new Vector3(DX[d], 0, DY[d]);
                            var a = c + n * C / 2 - Vector3.Cross(Vector3.up, n) * C / 2;
                            var b = c + n * C / 2 + Vector3.Cross(Vector3.up, n) * C / 2;
                            s.Part("corrugated", Matrix4x4.identity, c).Wall(0, a, b, h, n, 1f / 3f);
                            if (r.Chance(0.4)) s.Part("trim", Matrix4x4.TRS(c + n * (C / 2 + 0.02f) + Vector3.up * 1.4f, Quaternion.LookRotation(n), Vector3.one), c).Box(0, Vector3.zero, new Vector3(1.6f, 2.8f, 0.04f));
                        }
                        s.Part("metalDark", Matrix4x4.identity, c).Box(0, c + Vector3.up * (h + 0.1f), new Vector3(C + 0.3f, 0.2f, C + 0.3f));
                        s.Box(c + Vector3.up * h / 2, new Vector3(C, h, C), 0, SurfaceKind.Metal);
                        break;
                    }
                }
            }
        }

        static void MetroDetails(Level lv, Sink s, BiomeKit kit, Rng r)
        {
            var g = lv.Plan.Grid;
            float H = lv.Ceiling;
            // Platform edge (between rows 5 and 6): tactile yellow strip, stone coping.
            float zEdge = 6 * C;
            for (int x = 1; x < g.W - 1; x++)
            {
                if (!g.IsOpen(x, 5)) continue;
                float x0 = x * C;
                s.Part("tactile", Matrix4x4.identity, new Vector3(x0, 0, zEdge)).Floor(0, x0, zEdge - 1.0f, C, 0.45f, 0.006f, 1);
                s.Part("platformEdge", Matrix4x4.identity, new Vector3(x0, 0, zEdge)).Floor(0, x0, zEdge - 0.4f, C, 0.4f, 0.008f, 1);
            }
            // Concrete sleepers and profiled rails (foot, web, polished head) on both tracks.
            foreach (int row in new[] { 6, 7 })
            {
                float zc = (row + 0.5f) * C;
                for (int x = 1; x < g.W - 1; x++)
                {
                    float x0 = x * C;
                    var c = new Vector3(x0 + C / 2, 0, zc);
                    for (float sx = 0.3f; sx < C; sx += 0.65f) s.Part("sleeper", Matrix4x4.identity, c).Box(0, new Vector3(x0 + sx, 0.05f, zc), new Vector3(0.26f, 0.1f, 2.5f));
                    foreach (var dz in new[] { -0.72f, 0.72f })
                    {
                        s.Part("rail", Matrix4x4.identity, c).Box(0, new Vector3(x0 + C / 2, 0.115f, zc + dz), new Vector3(C, 0.03f, 0.14f));
                        s.Part("rail", Matrix4x4.identity, c).Box(0, new Vector3(x0 + C / 2, 0.165f, zc + dz), new Vector3(C, 0.08f, 0.035f));
                        s.Part("railHead", Matrix4x4.identity, c).Box(0, new Vector3(x0 + C / 2, 0.215f, zc + dz), new Vector3(C, 0.035f, 0.07f));
                    }
                }
            }
            // Coffered concrete ceiling: a deep beam over every internal cell boundary.
            for (int y = 0; y < g.H; y++)
                for (int x = 1; x < g.W; x++)
                {
                    if (!g.IsOpen(x, y) || !g.IsOpen(x - 1, y)) continue;
                    var bc = new Vector3(x * C, H - 0.3f, (y + 0.5f) * C);
                    s.Part("concrete", Matrix4x4.identity, bc).Box(0, bc, new Vector3(0.45f, 0.6f, C + 0.02f), 0.5f);
                }
            MetroLighting(lv, s, kit, r);
            MetroTunnels(lv, s, kit);
            // Parked train over the obstacle span of row 7.
            int t0 = -1, t1 = -1;
            for (int x = 0; x < g.W; x++) if (g.Has(g.Idx(x, 7), CellTag.Obstacle)) { if (t0 < 0) t0 = x; t1 = x; }
            if (t0 >= 0)
            {
                float len = (t1 - t0 + 1) * C - 1f;
                Props.Carriage(s, new Vector3((t0 + t1 + 1) * C / 2, 0, 7.5f * C), 90, r, true, len);
            }
            // Columns along the platform middle.
            for (int x = 2; x < g.W - 1; x += 2)
            {
                foreach (int row in new[] { 4, 5 })
                {
                    if (!g.IsOpen(x, row) || !g.IsOpen(x - 1, row)) continue;
                    Props.Column(s, new Vector3(x * C, 0, row * C), H, "tileColumn", false, false);
                }
            }
            // Signs over the stair openings.
            for (int x = 1; x < g.W - 1; x++)
            {
                if (!g.IsOpen(x, 2)) continue;
                var p = new Vector3((x + 0.5f) * C, H - 0.6f, 3 * C + 0.2f);
                kit.Sign(s, p, 0, r.Chance(0.5) ? 0 : 1, 2.2f);
            }
        }

        /// <summary>Deep concrete beams over every internal cell boundary in both directions (parking-deck look).</summary>
        static void CofferedCeiling(Level lv, Sink s)
        {
            var g = lv.Plan.Grid;
            float H = lv.Ceiling;
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (!g.IsOpen(x, y)) continue;
                    if (x > 0 && g.IsOpen(x - 1, y))
                    {
                        var bc = new Vector3(x * C, H - 0.3f, (y + 0.5f) * C);
                        s.Part("concrete", Matrix4x4.identity, bc).Box(0, bc, new Vector3(0.45f, 0.6f, C + 0.02f), 0.5f);
                    }
                    if (y > 0 && g.IsOpen(x, y - 1))
                    {
                        var bc = new Vector3((x + 0.5f) * C, H - 0.22f, y * C);
                        s.Part("concrete", Matrix4x4.identity, bc).Box(0, bc, new Vector3(C + 0.02f, 0.44f, 0.35f), 0.5f);
                    }
                }
        }

        /// <summary>
        /// Station lighting: continuous rows of strip fixtures along the platform, concourse and over the
        /// tracks (emissive on every cell), with real lights spread evenly over the rows within the light budget.
        /// </summary>
        static void MetroLighting(Level lv, Sink s, BiomeKit kit, Rng r)
        {
            var g = lv.Plan.Grid;
            float H = lv.Ceiling;
            var a = lv.Atmosphere;
            string lit = kit.FixtureMat;
            var lights = new List<Vector3>();
            for (int y = 0; y < g.H; y++)
            {
                bool track = y >= 6;
                if (y == 7) continue;   // one fixture row between the two tracks (placed from row 6)
                for (int x = 0; x < g.W; x++)
                {
                    if (!g.IsOpen(x, y)) continue;
                    float z = track ? 7 * C : (y + 0.5f) * C;
                    float cx = (x + 0.5f) * C;
                    foreach (var dx in new[] { -C * 0.25f, C * 0.25f })
                    {
                        var p = new Vector3(cx + dx, H - 0.62f, z);
                        bool on = r.Chance(Mathf.Lerp(0.5f, 1f, a.LightChance));
                        var rot = Quaternion.Euler(0, 90, 0);
                        s.Part("metalDark", Matrix4x4.TRS(p, rot, Vector3.one), p).Box(0, Vector3.zero, new Vector3(0.42f, 0.1f, 2.3f));
                        s.Part(on ? lit : "trim", Matrix4x4.TRS(p + Vector3.down * 0.055f, rot, Vector3.one), p).Box(0, Vector3.zero, new Vector3(0.32f, 0.02f, 2.2f));
                    }
                    // Light candidates: platform middle, concourse and the track centre line.
                    if ((y == 4 && x % 2 == 0) || (track && x % 2 == 1) || (y == 1 && x % 3 == 0)) lights.Add(new Vector3(cx, H - 0.75f, z));
                }
            }
            // Keep a few lights for props (train interior, lightboxes) and spread the rest evenly.
            int budget = Mathf.Max(4, s.LightBudget - s.Lights.Count - Mathf.Max(2, s.LightBudget / 6));
            float step = Mathf.Max(1f, lights.Count / (float)budget);
            // When the budget drops candidates, the remaining lights cover the gaps (wider and brighter).
            float gapBoost = Mathf.Min(1.8f, step), gapRange = Mathf.Min(1.4f, Mathf.Sqrt(step));
            for (float k = 0; k < lights.Count; k += step)
            {
                var p = lights[(int)k];
                s.Light(p, kit.Practical(r), 13 * gapRange, 3.6f * gapBoost, spot: true, angle: 155, flicker: r.Chance(a.Flicker * 0.5f) ? 0.25f : 0);
            }
        }

        /// <summary>Dark tunnel mouths with concrete portals and signal lamps where the tracks meet the end walls.</summary>
        static void MetroTunnels(Level lv, Sink s, BiomeKit kit)
        {
            var g = lv.Plan.Grid;
            int x0 = -1, x1 = -1;
            for (int x = 0; x < g.W; x++) if (g.IsOpen(x, 6)) { if (x0 < 0) x0 = x; x1 = x; }
            if (x0 < 0) return;
            foreach (var (wx, dir) in new[] { (x0 * C, 1f), ((x1 + 1) * C, -1f) })
            {
                foreach (int row in new[] { 6, 7 })
                {
                    if (!g.IsOpen(dir > 0 ? x0 : x1, row)) continue;
                    float zc = (row + 0.5f) * C;
                    var face = new Vector3(wx + dir * 0.03f, 0, zc);
                    s.Part("tunnel", Matrix4x4.identity, face).Box(0, face + Vector3.up * 1.9f, new Vector3(0.04f, 3.8f, 4.4f));
                    // Portal: side piers and a header beam.
                    foreach (var dz in new[] { -2.45f, 2.45f })
                        s.Part("concrete", Matrix4x4.identity, face).Box(0, new Vector3(wx + dir * 0.12f, 2.1f, zc + dz), new Vector3(0.24f, 4.2f, 0.5f), 0.5f);
                    s.Part("concrete", Matrix4x4.identity, face).Box(0, new Vector3(wx + dir * 0.12f, 4.05f, zc), new Vector3(0.24f, 0.5f, 5.4f), 0.5f);
                    // Signal lamp beside the portal.
                    var sig = new Vector3(wx + dir * 0.2f, 2.6f, zc - 2.0f);
                    s.Part("metalDark", Matrix4x4.identity, sig).Box(0, sig, new Vector3(0.12f, 0.6f, 0.3f));
                    s.Part("emitRed", Matrix4x4.identity, sig).Box(0, sig + new Vector3(dir * 0.065f, 0.15f, 0), new Vector3(0.02f, 0.14f, 0.14f));
                    s.Part("emitGreen", Matrix4x4.identity, sig).Box(0, sig + new Vector3(dir * 0.065f, -0.12f, 0), new Vector3(0.02f, 0.14f, 0.14f));
                }
            }
        }

        // ================================================================== dressing

        sealed class Dresser
        {
            readonly Level _lv;
            readonly Sink _s;
            readonly BiomeKit _k;
            readonly Rng _r;
            readonly Grid _g;
            readonly float _density;

            public Dresser(Level lv, Sink s, BiomeKit k, Rng r)
            {
                _lv = lv;
                _s = s;
                _k = k;
                _r = r;
                _g = lv.Plan.Grid;
                _density = Quality.PropDensity;
                // Indoor ceiling-fixture lights are placed at the end, evenly over the whole map; keep most of
                // the budget for them so props in the first cells cannot use it all up.
                if (lv.Indoor && lv.Plan.Kind != LayoutKind.Metro) _s.HeldBack = Mathf.RoundToInt(_s.LightBudget * 0.65f);
            }

            struct FixtureLight { public Vector3 P; public Color C; public float Range, Intensity, Angle, Flicker; public bool Beam; }
            readonly List<FixtureLight> _fixtures = new List<FixtureLight>();

            /// <summary>Places the deferred fixture lights evenly across the level within the remaining budget.</summary>
            void PlaceFixtureLights()
            {
                _s.HeldBack = 0;
                if (_fixtures.Count == 0) return;
                int avail = Mathf.Max(1, _s.LightBudget - _s.Lights.Count);
                float step = Mathf.Max(1f, _fixtures.Count / (float)avail);
                // Fewer lights than fixtures: the chosen ones reach further so no region is left black.
                float gapBoost = Mathf.Min(1.8f, step), gapRange = Mathf.Min(1.45f, Mathf.Sqrt(step));
                for (float k = 0; k < _fixtures.Count; k += step)
                {
                    var f = _fixtures[(int)k];
                    _s.Light(f.P, f.C, f.Range * gapRange, f.Intensity * gapBoost, spot: true, angle: f.Angle, flicker: f.Flicker, beam: f.Beam);
                }
            }

            bool Free(Vector3 p, float radius)
            {
                foreach (var q in _lv.Reserved) if ((q - p).Flat().sqrMagnitude < radius * radius) return false;
                return true;
            }

            void Reserve(Vector3 p) => _lv.Reserved.Add(p);

            Vector3 InCell(int cell, float margin)
            {
                var c = _lv.CellCenter(cell);
                float h = C / 2 - margin;
                return c + new Vector3(_r.Range(-h, h), 0, _r.Range(-h, h));
            }

            /// <summary>A point against a wall of the cell (if it has one) and the yaw facing into the cell.</summary>
            bool AgainstWall(int cell, float depth, out Vector3 p, out float yaw)
            {
                int x = _g.X(cell), y = _g.Y(cell);
                var walls = new List<int>();
                for (int d = 0; d < 4; d++) if (!_g.IsOpen(x + DX[d], y + DY[d])) walls.Add(d);
                p = default;
                yaw = 0;
                if (walls.Count == 0) return false;
                int w = walls[_r.Range(0, walls.Count)];
                var n = new Vector3(DX[w], 0, DY[w]);
                var t = Vector3.Cross(Vector3.up, n);
                p = _lv.CellCenter(cell) + n * (C / 2 - depth) + t * _r.Range(-1.8f, 1.8f);
                yaw = Quaternion.LookRotation(-n).eulerAngles.y;
                return true;
            }

            public void DressCell(int i)
            {
                var c = _lv.CellCenter(i);
                bool start = i == _lv.Plan.StartCell;
                bool obstacle = _g.Has(i, CellTag.Obstacle) && !start;
                var b = _lv.Plan.Biome;
                // Floor clutter everywhere.
                int papers = (int)((_lv.Indoor ? 18 : 14) * _density), bits = (int)(7 * _density);
                Props.Litter(_s, c, C / 2 - 0.3f, _r, papers, bits);
                if (_k.Wet && _r.Chance(0.55)) Props.Puddle(_s, InCell(i, 1), _r, _r.Range(1.2f, 2.6f));
                if (_r.Chance(0.3)) Props.BloodPool(_s, InCell(i, 1), _r, _r.Range(0.8f, 1.8f));
                // Ceiling light for indoor cells (the metro has its own fixture rows, see MetroLighting).
                if (_lv.Indoor && _lv.Plan.Kind != LayoutKind.Metro) CeilingFixture(i, c);
                switch (_lv.Plan.Kind)
                {
                    case LayoutKind.Streets: Street(i, c, obstacle, start); break;
                    case LayoutKind.Metro: Metro(i, c, obstacle, start); break;
                    case LayoutKind.Halls: Hall(i, c, obstacle, start, b); break;
                    case LayoutKind.Yard: Yard(i, c, obstacle, start, b); break;
                    case LayoutKind.Arena: Arena(i, c, obstacle, start); break;
                }
            }

            void CeilingFixture(int i, Vector3 c)
            {
                float H = _lv.Ceiling;
                var a = _lv.Atmosphere;
                bool lit = _r.Chance(a.LightChance);
                float yaw = _r.Chance(0.5) ? 0 : 90;
                var p = c + new Vector3(_r.Range(-0.8f, 0.8f), H - 0.06f, _r.Range(-0.8f, 0.8f));
                var rot = Quaternion.Euler(0, yaw, 0);
                bool high = H > 5.5f;
                if (high)
                {
                    // High-bay lamp on a drop rod.
                    _s.Part("metalDark", Matrix4x4.identity, p).Cylinder(0, p, p + Vector3.down * 1.4f, 0.02f, 0.02f, 4, false, false);
                    _s.Part("metalDark", Matrix4x4.TRS(p + Vector3.down * 1.6f, Quaternion.identity, Vector3.one), p).Lathe(0, new[] { new Vector2(0.45f, 0), new Vector2(0.3f, 0.15f), new Vector2(0.1f, 0.3f) }, 12);
                    _s.Part(lit ? "emitSodium" : "trim", Matrix4x4.identity, p).Disc(0, p + Vector3.down * 1.6f, Vector3.down, Vector3.right, 0.4f, 12);
                    if (lit) _fixtures.Add(new FixtureLight { P = p + Vector3.down * 1.7f, C = _k.Practical(_r), Range = 14, Intensity = 4.5f, Angle = 115, Flicker = _r.Chance(a.Flicker) ? 0.3f : 0, Beam = true });
                }
                else
                {
                    // Fluorescent strip fixture.
                    _s.Part("trim", Matrix4x4.TRS(p, rot, Vector3.one), p).Box(0, new Vector3(0, 0, 0), new Vector3(0.35f, 0.08f, 1.3f));
                    _s.Part(lit ? _k.FixtureMat : "trim", Matrix4x4.TRS(p + Vector3.down * 0.045f, rot, Vector3.one), p).Box(0, Vector3.zero, new Vector3(0.26f, 0.02f, 1.2f));
                    if (lit) _fixtures.Add(new FixtureLight { P = p + Vector3.down * 0.15f, C = _k.Practical(_r), Range = H > 4 ? 10 : 8, Intensity = 3.2f, Angle = 140, Flicker = _r.Chance(a.Flicker) ? 0.35f : 0, Beam = H > 3.8f });
                    else if (_r.Chance(0.3))
                    {
                        // Dangling broken fixture.
                        var dp = p + Vector3.down * 0.7f;
                        _s.Part("trim", Matrix4x4.TRS(dp, Quaternion.Euler(_r.Range(30f, 60f), yaw, 0), Vector3.one), p).Box(0, Vector3.zero, new Vector3(0.3f, 0.06f, 1.2f));
                    }
                }
            }

            // ----------------------------------------------------------- streets

            void Street(int i, Vector3 c, bool obstacle, bool start)
            {
                var b = _lv.Plan.Biome;
                bool snow = b == Biome.Frozen;
                bool road = _g.Has(i, CellTag.Road);
                if (obstacle)
                {
                    // Wreck cluster: 1-3 cars, maybe a checkpoint.
                    int cars = _r.Range(1, 4);
                    float baseYaw = RoadYaw(i);
                    for (int k = 0; k < cars; k++)
                    {
                        var p = c + new Vector3(_r.Range(-1.6f, 1.6f), 0, _r.Range(-1.6f, 1.6f));
                        if (!Free(p, 2.4f)) continue;
                        var kind = (Props.CarKind)_r.Range(0, 6);
                        if (_r.Chance(0.25)) Props.Carriage(_s, p, baseYaw + _r.Range(-15f, 15f), _r, false, 11f);
                        else Props.Car(_s, p, baseYaw + _r.Range(-35f, 35f), _r, kind, snow, _r.Chance(0.2));
                        Reserve(p);
                        break;
                    }
                    if (_r.Chance(0.5))
                    {
                        var p = c + new Vector3(_r.Range(-2f, 2f), 0, _r.Range(-2f, 2f));
                        if (Free(p, 1.6f)) { if (_r.Chance(0.5)) Props.Barricade(_s, p, _r.Range(0, 360f), _r, _r.Chance(0.6)); else Props.Jersey(_s, p, _r.Range(0, 360f), _r); Reserve(p); }
                    }
                    if (_r.Chance(0.4)) Props.Rubble(_s, InCell(i, 1.2f), _r, _r.Range(0.6f, 1.2f));
                    return;
                }
                if (start) return;
                // Parked car along the kerb.
                if (road && _r.Chance(0.35 * _density))
                {
                    var p = c + new Vector3(_r.Range(-1.4f, 1.4f), 0, _r.Range(-1.4f, 1.4f));
                    if (Free(p, 2.5f)) { Props.Car(_s, p, RoadYaw(i) + _r.Range(-12f, 12f) + (_r.Chance(0.5) ? 180 : 0), _r, (Props.CarKind)_r.Range(0, 4), snow); Reserve(p); }
                }
                if (AgainstWall(i, 2.2f, out var wp, out var wy))
                {
                    if (_r.Chance(0.3) && Free(wp, 1.5f)) { Props.StreetLight(_s, wp, wy, _r, _k.Practical(_r), _r.Chance(_lv.Atmosphere.LightChance), _r.Chance(0.3)); Reserve(wp); }
                    else if (_r.Chance(0.2) && Free(wp, 1.5f)) { Props.Dumpster(_s, wp, wy + 90, _r); Reserve(wp); }
                    else if (_r.Chance(0.25)) Props.TrashBags(_s, wp, _r);
                    else if (_r.Chance(0.12) && Free(wp, 2f)) { Props.BusStop(_s, wp, wy, _r); Reserve(wp); }
                    else if (_r.Chance(0.15)) Props.Hydrant(_s, wp);
                }
                if (AgainstWall(i, 0.5f, out var ap, out var ay) && _r.Chance(0.18)) Props.Lightbox(_s, ap, ay, _r, false, _r.Range(0, 8), _r.Chance(0.5));
                if (_r.Chance(0.25 * _density)) Props.Cone(_s, InCell(i, 0.8f), _r);
                if (snow && _r.Chance(0.4)) { var p = InCell(i, 1f); _s.Part("snow", Matrix4x4.TRS(p, Quaternion.Euler(0, _r.Range(0, 360f), 0), Vector3.one), p).RoundBox(0, new Vector3(0, 0.15f, 0), new Vector3(_r.Range(1f, 2.5f), 0.5f, _r.Range(0.8f, 1.6f)), 0.25f); }
                if (_r.Chance(0.12)) Props.Rubble(_s, InCell(i, 1f), _r, _r.Range(0.4f, 0.8f));
                if (_r.Chance(0.05)) Props.BodyBag(_s, InCell(i, 1f), _r.Range(0, 360f), _r);
            }

            float RoadYaw(int i)
            {
                int x = _g.X(i), y = _g.Y(i);
                bool ew = _g.IsOpen(x - 1, y) && _g.IsOpen(x + 1, y);
                return ew ? 90 : 0;
            }

            // ----------------------------------------------------------- metro

            void Metro(int i, Vector3 c, bool obstacle, bool start)
            {
                int row = _g.Y(i);
                if (row >= 6) return; // tracks
                if (row >= 3)
                {
                    if (obstacle) { if (_r.Chance(0.5)) Props.Bench(_s, c, 90, true); else Props.Barricade(_s, c, _r.Range(-20f, 20f), _r, true); return; }
                    if (row == 3 && AgainstWall(i, 0.2f, out var wp, out var wy) && _r.Chance(0.7)) Props.Lightbox(_s, wp, wy, _r, false, _r.Range(0, 8), _r.Chance(0.85));
                    // Station furniture along the back wall: potted plants, extinguisher cabinets, benches.
                    if (row == 3 && !start && AgainstWall(i, 0.45f, out var pp, out var py))
                    {
                        if (_r.Chance(0.35) && Free(pp, 1.2f)) { Props.Plant(_s, pp, _r); Reserve(pp); }
                        else if (_r.Chance(0.3)) Props.Extinguisher(_s, pp + Quaternion.Euler(0, py, 0) * Vector3.back * 0.3f, py);
                    }
                    if (row == 4 && !start && _r.Chance(0.3)) { var bp = c + new Vector3(_r.Range(-1.5f, 1.5f), 0, 0); if (Free(bp, 1.6f)) { Props.Bench(_s, bp, 0, true); Reserve(bp); } }
                    if (_r.Chance(0.2)) Props.TrashBags(_s, InCell(i, 1f), _r);
                    if (_r.Chance(0.15) && !start) { var p = InCell(i, 1.5f); if (Free(p, 1.4f)) { Props.Lightbox(_s, p, 90 + _r.Range(-10f, 10f), _r, true, _r.Range(0, 8), true); Reserve(p); } }
                    if (_r.Chance(0.1)) Props.BodyBag(_s, InCell(i, 1f), _r.Range(0, 360f), _r);
                }
                else
                {
                    if (_r.Chance(0.35) && AgainstWall(i, 0.5f, out var wp, out var wy)) Props.Bench(_s, wp, wy, true);
                    if (_r.Chance(0.3) && AgainstWall(i, 0.6f, out var tp, out var ty)) { Props.Desk(_s, tp, ty, _r, false); }
                }
            }

            // ----------------------------------------------------------- halls

            void Hall(int i, Vector3 c, bool obstacle, bool start, Biome b)
            {
                bool corridor = _g.Has(i, CellTag.Corridor);
                bool pillars = _g.Has(i, CellTag.Pillars);
                int x = _g.X(i), y = _g.Y(i);
                // Columns at room corners shared by four open cells.
                if (pillars && _g.IsOpen(x + 1, y) && _g.IsOpen(x, y + 1) && _g.IsOpen(x + 1, y + 1) && (x + y) % 2 == 0)
                {
                    var cp = new Vector3((x + 1) * C, 0, (y + 1) * C);
                    if (Free(cp, 1f)) Props.Column(_s, cp, _lv.Ceiling, b == Biome.Mall ? "marbleColumn" : "concrete", b == Biome.Parking || b == Biome.Factory, b == Biome.Mall);
                }
                if (start) return;
                if (obstacle)
                {
                    var p = InCell(i, 2f);
                    if (!Free(p, 2f)) return;
                    Reserve(p);
                    switch (b)
                    {
                        case Biome.Factory: Props.Machine(_s, p, _r.Range(0, 4) * 90f, _r); break;
                        case Biome.Nuclear: if (_r.Chance(0.5)) Props.BigTank(_s, p, _r, _r.Range(4f, 6.5f), _r.Chance(0.7)); else Props.Machine(_s, p, _r.Range(0, 4) * 90f, _r); break;
                        case Biome.Lab: Props.SpecimenTank(_s, p, _r, _r.Chance(0.35)); break;
                        case Biome.Hospital: Props.HospitalBed(_s, p, _r.Range(0, 360f), _r, true); Props.IvStand(_s, p + Vector3.right * 0.8f); break;
                        case Biome.Mall: Props.Lightbox(_s, p, _r.Range(0, 360f), _r, true, _r.Range(0, 8), true); Props.Bench(_s, p + Vector3.forward * 1.5f, 0, false); break;
                        case Biome.Parking: Props.Car(_s, p, _r.Range(0, 2) * 90f + _r.Range(-8f, 8f), _r, (Props.CarKind)_r.Range(0, 4), false, _r.Chance(0.15)); break;
                        default: Props.Crates(_s, p, _r.Range(0, 360f), _r, false); break;
                    }
                    return;
                }
                switch (b)
                {
                    case Biome.Hospital:
                        if (!corridor && AgainstWall(i, 1.1f, out var bp, out var by) && Free(bp, 1.4f)) { Props.HospitalBed(_s, bp, by, _r, false); Reserve(bp); if (_r.Chance(0.5)) Props.IvStand(_s, bp + Quaternion.Euler(0, by, 0) * new Vector3(0.7f, 0, 0)); }
                        if (corridor && _r.Chance(0.3) && AgainstWall(i, 0.6f, out var gp, out var gy) && Free(gp, 1.4f)) { Props.HospitalBed(_s, gp, gy + 90, _r, true); Reserve(gp); }
                        if (_r.Chance(0.25) && AgainstWall(i, 0.5f, out var sp, out var sy)) Props.Shelves(_s, sp, sy, _r, 1.4f);
                        if (_r.Chance(0.12)) Props.BodyBag(_s, InCell(i, 1f), _r.Range(0, 360f), _r);
                        if (_r.Chance(0.25) && AgainstWall(i, 0.05f, out var np, out var ny)) _k.Sign(_s, np + Vector3.up * 2.4f, ny, _r.Range(0, 8), 1.6f);
                        break;
                    case Biome.Factory:
                        if (!corridor && _r.Chance(0.4) && AgainstWall(i, 0.5f, out var fp, out var fy) && Free(fp, 1.4f)) { Props.Shelves(_s, fp, fy, _r, 2.4f); Reserve(fp); }
                        if (_r.Chance(0.35)) Props.Barrels(_s, InCell(i, 1f), _r, _r.Chance(0.5) ? "rust" : "barrelBlue");
                        if (_r.Chance(0.3)) Props.Crates(_s, InCell(i, 1.2f), _r.Range(0, 360f), _r, false);
                        if (_r.Chance(0.3) && AgainstWall(i, 0.3f, out var pp, out var py)) Props.Pipes(_s, pp - Quaternion.Euler(0, py, 0) * Vector3.right * 3, pp + Quaternion.Euler(0, py, 0) * Vector3.right * 3, _lv.Ceiling - 1.2f, 0.12f, 2);
                        break;
                    case Biome.Mall:
                        if (_r.Chance(0.3) && !corridor) { var p = InCell(i, 1.5f); if (Free(p, 1.2f)) { Props.Bench(_s, p, _r.Range(0, 4) * 90f, false); Reserve(p); } }
                        if (_r.Chance(0.2) && !corridor) { var p = InCell(i, 1.5f); if (Free(p, 1.5f)) { Props.Desk(_s, p, _r.Range(0, 360f), _r, false); Reserve(p); } }
                        if (_r.Chance(0.25)) Props.TrashBags(_s, InCell(i, 1f), _r);
                        break;
                    case Biome.Parking:
                        ParkingBays(i, c);
                        break;
                    case Biome.Lab:
                        if (_r.Chance(0.45) && AgainstWall(i, 0.5f, out var dp, out var dy) && Free(dp, 1.3f)) { Props.Desk(_s, dp, dy, _r, true); Reserve(dp); }
                        if (_r.Chance(0.25) && AgainstWall(i, 0.5f, out var rp, out var ry) && Free(rp, 1f)) { Props.ServerRack(_s, rp, ry, _r); Reserve(rp); }
                        if (_r.Chance(0.2) && !corridor) { var p = InCell(i, 1.8f); if (Free(p, 1.6f)) { Props.SpecimenTank(_s, p, _r, _r.Chance(0.4)); Reserve(p); } }
                        if (_r.Chance(0.25) && AgainstWall(i, 0.05f, out var np2, out var ny2)) _k.Sign(_s, np2 + Vector3.up * 2.2f, ny2, _r.Range(0, 8), 1.6f);
                        break;
                    case Biome.Nuclear:
                        if (_r.Chance(0.35) && AgainstWall(i, 0.6f, out var cp2, out var cy2) && Free(cp2, 1.3f)) { Props.Desk(_s, cp2, cy2, _r, true); Reserve(cp2); }
                        if (_r.Chance(0.4) && AgainstWall(i, 0.3f, out var pp2, out var py2)) Props.Pipes(_s, pp2 - Quaternion.Euler(0, py2, 0) * Vector3.right * 3, pp2 + Quaternion.Euler(0, py2, 0) * Vector3.right * 3, _lv.Ceiling - 1.0f, 0.18f, 3);
                        if (_r.Chance(0.3)) Props.Barrels(_s, InCell(i, 1f), _r, "barrelYellow");
                        if (_r.Chance(0.25) && AgainstWall(i, 0.05f, out var np3, out var ny3)) _k.Sign(_s, np3 + Vector3.up * 2.6f, ny3, _r.Range(0, 8), 1.8f);
                        break;
                }
                if (_r.Chance(0.15)) Props.Rubble(_s, InCell(i, 1.2f), _r, _r.Range(0.4f, 0.8f));
            }

            void ParkingBays(int i, Vector3 c)
            {
                // Painted bay lines and parked cars facing the walls.
                int x = _g.X(i), y = _g.Y(i);
                for (int k = 0; k < 3; k++)
                {
                    float lx = x * C + 0.6f + k * 2.4f;
                    _s.Part("lineWhite", Matrix4x4.identity, c).Floor(0, lx, y * C + 0.4f, 0.1f, 2.2f, 0.006f, 1);
                }
                if (_r.Chance(0.55 * _density))
                {
                    var p = new Vector3(x * C + 1.8f + _r.Range(0, 2) * 2.4f, 0, y * C + 2.5f);
                    if (Free(p, 2.4f)) { Props.Car(_s, p, _r.Chance(0.5) ? 0 : 180, _r, (Props.CarKind)_r.Range(0, 4), false, _r.Chance(0.12)); Reserve(p); }
                }
            }

            // ----------------------------------------------------------- yards

            void Yard(int i, Vector3 c, bool obstacle, bool start, Biome b)
            {
                if (start) return;
                bool road = _g.Has(i, CellTag.Road);
                if (road && b == Biome.Airport) _s.Part("lineYellow", Matrix4x4.identity, c).Floor(0, c.x - C / 2, c.z - 0.1f, C, 0.2f, 0.006f, 1);
                if (!obstacle)
                {
                    if (_r.Chance(0.2 * _density)) Props.Barrels(_s, InCell(i, 1f), _r, b == Biome.Desert ? "rust" : "barrelBlue");
                    if (_r.Chance(0.2 * _density)) Props.Crates(_s, InCell(i, 1f), _r.Range(0, 360f), _r, b == Biome.MilitaryBase || b == Biome.Desert);
                    if (b == Biome.ForestCamp && _r.Chance(0.3)) Props.PineTree(_s, InCell(i, 0.5f), _r, false, _r.Chance(0.3));
                    if ((b == Biome.ForestCamp || b == Biome.Desert) && _r.Chance(0.2)) Props.Rock(_s, InCell(i, 1f), _r, _r.Range(0.5f, 1.2f));
                    return;
                }
                var p = InCell(i, 2f);
                if (!Free(p, 2.2f)) return;
                Reserve(p);
                float yaw = _r.Range(0, 4) * 90f + _r.Range(-15f, 15f);
                switch (b)
                {
                    case Biome.MilitaryBase:
                    {
                        double k = _r.NextDouble();
                        if (k < 0.2) Props.Truck(_s, p, yaw, _r, false);
                        else if (k < 0.3) Props.TankWreck(_s, p, yaw, _r);
                        else if (k < 0.5) Props.Sandbags(_s, p, yaw, _r, 4f);
                        else if (k < 0.65) Props.Hesco(_s, p, yaw, _r, 3);
                        else if (k < 0.8) Props.Tent(_s, p, yaw, _r, "canvas");
                        else if (k < 0.9) Props.Watchtower(_s, p, yaw, _r);
                        else Props.Container(_s, p, yaw, _r, _r.Range(0, 4), false);
                        break;
                    }
                    case Biome.Desert:
                    {
                        double k = _r.NextDouble();
                        if (k < 0.25) Props.Truck(_s, p, yaw, _r, true);
                        else if (k < 0.45) Props.Hesco(_s, p, yaw, _r, 3);
                        else if (k < 0.65) Props.Jersey(_s, p, yaw, _r);
                        else if (k < 0.8) Props.Tent(_s, p, yaw, _r, "canvasTan");
                        else Props.Rubble(_s, p, _r, 1.4f);
                        break;
                    }
                    case Biome.Port:
                    {
                        double k = _r.NextDouble();
                        if (k < 0.5) Props.Container(_s, p, yaw, _r, _r.Range(0, 4), _r.Chance(0.3));
                        else if (k < 0.6 && _g.OpenNeighbours(i) == 4) Props.Crane(_s, p, yaw, _r);
                        else if (k < 0.8) Props.Truck(_s, p, yaw, _r, false);
                        else Props.Crates(_s, p, yaw, _r, false);
                        break;
                    }
                    case Biome.Airport:
                    {
                        double k = _r.NextDouble();
                        if (k < 0.18 && _g.OpenNeighbours(i) >= 3) Props.PlaneWreck(_s, p, yaw, _r);
                        else if (k < 0.45) Props.Truck(_s, p, yaw, _r, false);
                        else if (k < 0.7) Props.Car(_s, p, yaw, _r, Props.CarKind.Van, false, _r.Chance(0.3));
                        else { Props.Crates(_s, p, yaw, _r, false); Props.Cone(_s, p + Vector3.right * 2, _r); }
                        break;
                    }
                    case Biome.ForestCamp:
                    {
                        double k = _r.NextDouble();
                        if (k < 0.35) Props.Tent(_s, p, yaw, _r, _r.Chance(0.5) ? "tentBlue" : "canvas");
                        else if (k < 0.5) Props.Campfire(_s, p, _r);
                        else if (k < 0.7) Props.Truck(_s, p, yaw, _r, false);
                        else { for (int t = 0; t < 3; t++) Props.PineTree(_s, p + new Vector3(_r.Range(-2f, 2f), 0, _r.Range(-2f, 2f)), _r, false, _r.Chance(0.3)); }
                        break;
                    }
                    default:
                        Props.Crates(_s, p, yaw, _r, true);
                        break;
                }
            }

            void Arena(int i, Vector3 c, bool obstacle, bool start)
            {
                if (start) return;
                if (obstacle)
                {
                    var p = InCell(i, 2f);
                    if (!Free(p, 2f)) return;
                    Reserve(p);
                    if (_lv.Indoor) Props.Column(_s, p, _lv.Ceiling, "concrete", true, false);
                    else if (_r.Chance(0.5)) Props.Jersey(_s, p, _r.Range(0, 360f), _r);
                    else Props.Car(_s, p, _r.Range(0, 360f), _r, (Props.CarKind)_r.Range(0, 6), _lv.Plan.Biome == Biome.Frozen, true);
                    return;
                }
                if (_lv.Indoor && AgainstWall(i, 1.1f, out var wp, out var wy))
                {
                    // Edge dressing like an abandoned service deck: crates, drums, wrecks and signage.
                    float roll = _r.Float();
                    if (roll < 0.3f && Free(wp, 1.6f)) { Props.Crates(_s, wp, wy + _r.Range(-15f, 15f), _r, _r.Chance(0.3)); Reserve(wp); }
                    else if (roll < 0.5f && Free(wp, 1.4f)) { Props.Barrels(_s, wp, _r, _r.Chance(0.5) ? "barrelBlue" : "barrelYellow"); Reserve(wp); }
                    else if (roll < 0.6f && Free(wp, 3f)) { Props.Car(_s, wp + Quaternion.Euler(0, wy, 0) * Vector3.forward * 0.8f, wy + 90 + _r.Range(-10f, 10f), _r, (Props.CarKind)_r.Range(0, 4), false, _r.Chance(0.3)); Reserve(wp); }
                    if (_r.Chance(0.12) && AgainstWall(i, 0.08f, out var sp, out var sy)) _k.Sign(_s, sp + Vector3.up * 2.6f, sy, 0, 3.6f);
                }
                if (!_lv.Indoor && _r.Chance(0.25)) Props.Rubble(_s, InCell(i, 1f), _r, _r.Range(0.5f, 1f));
                if (!_lv.Indoor && _r.Chance(0.15)) { var p = InCell(i, 1.5f); if (Free(p, 1.5f)) { Props.StreetLight(_s, p, _r.Range(0, 360f), _r, _k.Practical(_r), _r.Chance(0.6), _r.Chance(0.4)); Reserve(p); } }
            }

            public void Finish()
            {
                PlaceFixtureLights();
                // Outdoor arenas and yards: floodlights around the edge so bosses read clearly.
                if (!_lv.Indoor && (_lv.Plan.Kind == LayoutKind.Arena || _lv.Plan.Kind == LayoutKind.Yard))
                {
                    var b = _lv.Bounds;
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * 90 + 45;
                        var p = b.center + Quaternion.Euler(0, a, 0) * Vector3.forward * (b.extents.x * 0.8f);
                        p.y = 9;
                        _s.Light(p, new Color(1f, 0.92f, 0.8f), 40, 14f, spot: true, angle: 70, dir: (b.center - p).normalized + Vector3.down * 0.6f, beam: true);
                    }
                }
            }
        }
    }
}
