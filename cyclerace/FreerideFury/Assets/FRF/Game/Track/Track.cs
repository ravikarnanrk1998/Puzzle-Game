using System;
using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    /// <summary>Wooden or dirt kicker: the ground rises linearly to <see cref="Height"/> at the lip, then drops away.</summary>
    public sealed class Ramp
    {
        public float S0, Len, Height, X0, X1;
        public bool Wood;
        public float Lip => S0 + Len;
    }

    public enum ObstacleKind { Rock, Log, Stump, Cactus }

    /// <summary>Box in track space: centre (S, X), half sizes (HalfS, HalfX), height H above the ground.</summary>
    public sealed class Obstacle
    {
        public float S, X, HalfS, HalfX, H, Yaw;
        public ObstacleKind Kind;
        public int Seed;
    }

    public sealed class OrbSpot
    {
        public float S, X, Lift;
    }

    /// <summary>
    /// A generated downhill course. Everything is described in track space: distance
    /// <c>s</c> along the centre line (horizontal metres), lateral offset <c>x</c> (right
    /// positive) and absolute height <c>y</c>. Riders, AI and scenery all use these queries.
    /// </summary>
    public sealed class Track
    {
        public const float Step = 2f;
        public readonly TrackDef Def;
        public Theme Theme => Def.Theme;
        public int N;
        public Vector3[] Pos;
        public float[] Head, Curv, Width, Bank, Y, Grade;
        public float Length, StartS = 8, FinishS;
        public readonly List<Ramp> Ramps = new List<Ramp>();
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        public readonly List<OrbSpot> Orbs = new List<OrbSpot>();
        public readonly List<float> Drops = new List<float>();
        public readonly List<float> Crowds = new List<float>();
        public readonly List<(float S, int Dir)> Chevrons = new List<(float, int)>();

        /// <summary>Maximum comfortable speed per sample from curvature and grip (AI braking).</summary>
        public float[] SafeSpeed;

        public Track(TrackDef def)
        {
            Def = def;
            Generate(new System.Random(def.Seed));
        }

        // ------------------------------------------------------------ generation

        void Generate(System.Random rnd)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            const float startLen = 70, endLen = 170;
            Length = Mathf.Ceil((Def.Length + startLen + endLen) / Step) * Step;
            N = (int)(Length / Step) + 1;
            FinishS = Length - 130;
            Pos = new Vector3[N];
            Head = new float[N];
            Curv = new float[N];
            Width = new float[N];
            Bank = new float[N];
            Y = new float[N];
            Grade = new float[N];

            // Curvature plan: straights and arcs, keeping the course heading downhill (+Z) so it never crosses itself.
            var k = new float[N];
            int i = (int)(startLen / Step);
            float heading = 0;
            int endI = N - (int)(endLen / Step);
            while (i < endI)
            {
                bool straight = rnd.NextDouble() < 0.42 - Def.Turns * 0.2;
                if (straight)
                {
                    i += (int)(R(30, 100) / Step);
                    continue;
                }
                float angle = R(25, 70 + 40 * Def.Turns) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(80, 24, Def.Turns * (float)rnd.NextDouble() + (1 - Def.Turns) * 0.15f);
                int dir = rnd.NextDouble() < 0.5 ? -1 : 1;
                if (heading > 0.55f) dir = -1;
                else if (heading < -0.55f) dir = 1;
                const float maxHead = 1.25f;
                float room = dir > 0 ? maxHead - heading : heading + maxHead;
                angle = Mathf.Min(angle, room);
                if (angle < 0.15f)
                {
                    i += 10;
                    continue;
                }
                int n = Mathf.Max(4, (int)(radius * angle / Step));
                for (int j = 0; j < n && i < endI; j++, i++) k[i] = dir / radius;
                heading += dir * angle;
                // S-bends: a short straight then often the opposite way.
                i += (int)(R(6, 24) / Step);
            }
            for (int pass = 0; pass < 3; pass++) k = Smooth(k, 5);
            Array.Copy(k, Curv, N);

            // Plan view.
            var p = Vector3.zero;
            heading = 0;
            for (i = 0; i < N; i++)
            {
                Pos[i] = p;
                Head[i] = heading;
                p += new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * Step;
                heading += Curv[i] * Step;
            }

            // Width: wide start pen and finish area, theme width with gentle variation.
            float wSeed = R(0, 100);
            for (i = 0; i < N; i++)
            {
                float s = i * Step;
                float w = Def.Width + (Tex.Noise(s * 0.012f + wSeed, 3.7f) - 0.5f) * 3f;
                w += Mathf.Abs(Curv[i]) * 40;
                float start = 1 - Tex.SmoothStep(30, 80, s);
                float finish = Tex.SmoothStep(FinishS - 90, FinishS - 20, s);
                w = Mathf.Lerp(w, 17, Mathf.Max(start, finish));
                Width[i] = w;
                Bank[i] = -Mathf.Clamp(Curv[i] * 9f, -0.3f, 0.3f);
            }

            // Grade: steep start ramp, then mixed sections, flattening into the finish.
            var g = new float[N];
            i = 0;
            while (i < N)
            {
                float s = i * Step;
                int len = (int)(R(40, 130) / Step);
                double roll = rnd.NextDouble();
                float grade = Def.Grade * (roll < 0.18 ? R(1.6f, 2.1f) : roll < 0.38 ? R(0.35f, 0.6f) : R(0.8f, 1.25f));
                for (int j = 0; j < len && i < N; j++, i++)
                {
                    s = i * Step;
                    float gi = grade;
                    if (s < 14) gi = 0.0f;
                    else if (s < 44) gi = 0.32f;
                    else if (s > FinishS - 40) gi = Mathf.Lerp(grade, 0.03f, Tex.SmoothStep(FinishS - 40, FinishS + 20, s));
                    g[i] = gi;
                }
            }
            g = Smooth(g, 4);
            Y[0] = 0;
            for (i = 1; i < N; i++) Y[i] = Y[i - 1] - g[i - 1] * Step;

            PlaceFeatures(rnd);

            // Final grade (positive = downhill) from the finished height profile.
            for (i = 0; i < N; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(N - 1, i + 1);
                Grade[i] = -(Y[b] - Y[a]) / ((b - a) * Step);
                Pos[i].y = Y[i];
            }

            SafeSpeed = new float[N];
            for (i = 0; i < N; i++)
            {
                float kk = 0;
                for (int j = -3; j <= 3; j++) kk = Mathf.Max(kk, Mathf.Abs(Curv[Mathf.Clamp(i + j, 0, N - 1)]));
                float grip = 8.6f * Theme.Grip + Mathf.Abs(Bank[i]) * 9.81f * 1.4f;
                SafeSpeed[i] = kk < 1e-4f ? 40 : Mathf.Min(40, Mathf.Sqrt(grip / kk));
            }
        }

        static float[] Smooth(float[] a, int half)
        {
            var o = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                float sum = 0;
                int n = 0;
                for (int j = -half; j <= half; j++)
                {
                    int q = i + j;
                    if (q < 0 || q >= a.Length) continue;
                    sum += a[q];
                    n++;
                }
                o[i] = sum / n;
            }
            return o;
        }

        bool StraightAround(float s, float before, float after, float maxK = 0.012f)
        {
            for (float q = s - before; q <= s + after; q += Step)
            {
                int i = Mathf.Clamp((int)(q / Step), 0, N - 1);
                if (Mathf.Abs(Curv[i]) > maxK) return false;
            }
            return true;
        }

        void PlaceFeatures(System.Random rnd)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var busy = new List<(float A, float B)>();
            bool Free(float a, float b)
            {
                foreach (var (x0, x1) in busy)
                    if (b > x0 && a < x1) return false;
                return true;
            }

            // Kickers, each followed by a steeper landing.
            float spacing = Mathf.Lerp(230, 120, Def.Jumps);
            for (float s = 110; s < FinishS - 90; s += spacing * R(0.75f, 1.25f))
            {
                float at = s;
                bool placed = false;
                for (int tries = 0; tries < 8 && !placed; tries++, at += 14)
                {
                    if (!StraightAround(at, 16, 34) || !Free(at - 20, at + 40)) continue;
                    float w = WidthAt(at);
                    var ramp = new Ramp { S0 = at, Len = R(5.5f, 7.5f), Height = R(1.3f, 2.1f) * (0.85f + Def.Jumps * 0.25f), Wood = rnd.NextDouble() < 0.55 };
                    double lane = rnd.NextDouble();
                    if (lane < 0.45 || w < 10)
                    {
                        ramp.X0 = -w / 2 + 0.6f;
                        ramp.X1 = w / 2 - 0.6f;
                    }
                    else if (lane < 0.75)
                    {
                        ramp.X0 = -3.2f;
                        ramp.X1 = 3.2f;
                    }
                    else
                    {
                        bool left = rnd.NextDouble() < 0.5;
                        ramp.X0 = left ? -w / 2 + 0.6f : 0.4f;
                        ramp.X1 = left ? -0.4f : w / 2 - 0.6f;
                    }
                    Ramps.Add(ramp);
                    // Landing: the ground falls away a little faster after the lip.
                    int i0 = (int)(ramp.Lip / Step);
                    float extra = R(1.2f, 2.4f);
                    int span = (int)(18 / Step);
                    for (int i = i0; i < N; i++)
                    {
                        float f = Mathf.Clamp01((i - i0) / (float)span);
                        Y[i] -= extra * Tex.SmoothStep(0, 1, f);
                    }
                    busy.Add((ramp.S0 - 22, ramp.Lip + 34));
                    Orbs.Add(new OrbSpot { S = ramp.Lip + 9, X = (ramp.X0 + ramp.X1) * 0.5f, Lift = ramp.Height + 2.2f });
                    if (rnd.NextDouble() < 0.6) Crowds.Add(ramp.Lip + 6);
                    placed = true;
                }
            }

            // Rock drops: a sudden step down in the trail.
            int drops = Mathf.RoundToInt(Def.Length / 600f * (0.5f + Def.Jumps));
            for (int d = 0; d < drops; d++)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    float s = R(160, FinishS - 120);
                    if (!StraightAround(s, 12, 24, 0.016f) || !Free(s - 18, s + 26)) continue;
                    float h = R(1.6f, 2.8f);
                    int i0 = (int)(s / Step);
                    for (int i = i0 + 1; i < N; i++) Y[i] -= h;
                    Drops.Add(s);
                    busy.Add((s - 18, s + 26));
                    break;
                }
            }

            // Rollers on some straights.
            for (float s = 90; s < FinishS - 60; s += R(120, 260))
            {
                if (!StraightAround(s, 0, 50, 0.02f) || !Free(s, s + 50)) continue;
                float amp = R(0.35f, 0.75f), wave = R(13, 18);
                int i0 = (int)(s / Step), i1 = (int)((s + 48) / Step);
                for (int i = i0; i <= i1 && i < N; i++)
                {
                    float q = (i - i0) * Step;
                    float env = Mathf.Sin(q / 48f * Mathf.PI);
                    Y[i] += Mathf.Sin(q / wave * Mathf.PI * 2) * amp * env;
                }
                busy.Add((s, s + 50));
            }

            // Obstacles on the trail, always leaving a clear gap.
            int count = Mathf.RoundToInt(Def.Length / 55f * Def.Obstacles);
            for (int o = 0; o < count; o++)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    float s = R(70, FinishS - 50);
                    if (!Free(s - 6, s + 6)) continue;
                    float w = WidthAt(s);
                    var ob = new Obstacle { S = s, Seed = rnd.Next() };
                    double kind = rnd.NextDouble();
                    if (Theme.Biome == Biome.Canyon && kind < 0.3) ob.Kind = ObstacleKind.Cactus;
                    else if (Theme.Biome != Biome.Canyon && Theme.Biome != Biome.Volcano && kind < 0.35) ob.Kind = ObstacleKind.Log;
                    else if ((Theme.Biome == Biome.Forest || Theme.Biome == Biome.Autumn) && kind < 0.5) ob.Kind = ObstacleKind.Stump;
                    else ob.Kind = ObstacleKind.Rock;
                    switch (ob.Kind)
                    {
                        case ObstacleKind.Log:
                            ob.HalfS = 0.3f;
                            ob.HalfX = R(1.4f, 2.2f);
                            ob.H = 0.5f;
                            ob.Yaw = R(-0.25f, 0.25f);
                            break;
                        case ObstacleKind.Stump:
                            ob.HalfS = ob.HalfX = 0.42f;
                            ob.H = 0.6f;
                            break;
                        case ObstacleKind.Cactus:
                            ob.HalfS = ob.HalfX = 0.4f;
                            ob.H = 2.6f;
                            break;
                        default:
                            ob.HalfS = ob.HalfX = R(0.5f, 0.85f);
                            ob.H = ob.HalfX * 1.25f;
                            break;
                    }
                    float side = w / 2 - ob.HalfX - 0.4f;
                    if (side < 1) continue;
                    ob.X = R(-side, side);
                    // Keep at least 4 m of clear trail on one side.
                    float clearL = (ob.X - ob.HalfX) + w / 2, clearR = w / 2 - (ob.X + ob.HalfX);
                    if (Mathf.Max(clearL, clearR) < 4) continue;
                    bool clash = false;
                    foreach (var other in Obstacles)
                        if (Mathf.Abs(other.S - s) < 12) clash = true;
                    if (clash) continue;
                    Obstacles.Add(ob);
                    break;
                }
            }

            // Ground orbs (adrenaline pickups) in short lines.
            for (float s = 130; s < FinishS - 40; s += R(110, 190))
            {
                float w = WidthAt(s);
                float x = R(-w / 2 + 1.5f, w / 2 - 1.5f);
                for (int j = 0; j < 3; j++)
                {
                    float q = s + j * 5;
                    bool blocked = false;
                    foreach (var ob in Obstacles)
                        if (Mathf.Abs(ob.S - q) < 3 && Mathf.Abs(ob.X - x) < ob.HalfX + 1) blocked = true;
                    foreach (var r in Ramps)
                        if (q > r.S0 - 2 && q < r.Lip + 2) blocked = true;
                    if (!blocked) Orbs.Add(new OrbSpot { S = q, X = x, Lift = 1.0f });
                }
            }

            // Warning chevrons on the outside of tight turns.
            float lastChevron = -100;
            for (int i = 0; i < N; i++)
            {
                float s = i * Step;
                if (Mathf.Abs(Curv[i]) > 0.026f && s - lastChevron > 60 && s > 60 && s < FinishS)
                {
                    Chevrons.Add((s - 6, Curv[i] > 0 ? 1 : -1));
                    lastChevron = s;
                }
            }
            Crowds.Add(StartS + 18);
            Crowds.Add(FinishS - 10);
            Crowds.Add(FinishS + 8);
        }

        // ------------------------------------------------------------ queries

        void Index(float s, out int i, out float t)
        {
            float f = Mathf.Clamp(s, 0, Length - Step * 1.001f) / Step;
            i = (int)f;
            t = f - i;
            if (i >= N - 1)
            {
                i = N - 2;
                t = 1;
            }
        }

        float Lerp(float[] a, float s)
        {
            Index(s, out int i, out float t);
            return a[i] + (a[i + 1] - a[i]) * t;
        }

        public float WidthAt(float s) => Lerp(Width, s);
        public float CurvAt(float s) => Lerp(Curv, s);
        public float BankAt(float s) => Lerp(Bank, s);
        public float BaseY(float s) => Lerp(Y, s);
        public float GradeAt(float s) => Lerp(Grade, s);
        public float HeadAt(float s) => Lerp(Head, s);
        public float SafeAt(float s) => Lerp(SafeSpeed, s);

        public Vector3 Center(float s)
        {
            Index(s, out int i, out float t);
            return Vector3.Lerp(Pos[i], Pos[i + 1], t);
        }

        public Vector3 Forward(float s)
        {
            float h = HeadAt(s);
            return new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
        }

        public Vector3 Right(float s)
        {
            float h = HeadAt(s);
            return new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h));
        }

        /// <summary>Kicker height at (s, x), 0 off the ramp.</summary>
        public float RampHeight(float s, float x, out Ramp hit)
        {
            hit = null;
            foreach (var r in Ramps)
            {
                if (s < r.S0 || s > r.Lip) continue;
                float edge = Mathf.Min(x - r.X0, r.X1 - x);
                if (edge < 0) continue;
                hit = r;
                return r.Height * (s - r.S0) / r.Len * Mathf.Clamp01(edge / 0.25f);
            }
            return 0;
        }

        /// <summary>Riding surface height without ramps: trail, berm banking and the verge rising beyond the edge.</summary>
        public float TrailY(float s, float x)
        {
            float half = WidthAt(s) * 0.5f;
            float y = BaseY(s) + BankAt(s) * Mathf.Clamp(x, -half, half);
            float over = Mathf.Abs(x) - half;
            if (over > 0) y += over * 0.18f + over * over * 0.02f;
            return y;
        }

        public float GroundY(float s, float x) => TrailY(s, x) + RampHeight(s, x, out _);

        public Vector3 World(float s, float x, float y)
        {
            var c = Center(s);
            var r = Right(s);
            return new Vector3(c.x + r.x * x, y, c.z + r.z * x);
        }

        public Vector3 WorldOnGround(float s, float x, float lift = 0) => World(s, x, GroundY(s, x) + lift);

        /// <summary>Ground normal at (s, x) from finite differences.</summary>
        public Vector3 Normal(float s, float x)
        {
            const float e = 0.6f;
            var a = WorldOnGround(s - e, x);
            var b = WorldOnGround(s + e, x);
            var c = WorldOnGround(s, x - e);
            var d = WorldOnGround(s, x + e);
            var n = Vector3.Cross(b - a, c - d);
            if (n.y < 0) n = -n;
            return n.normalized;
        }

        /// <summary>Next ramp lip at or after <paramref name="s"/> (null if none within <paramref name="range"/>).</summary>
        public Ramp NextRamp(float s, float range)
        {
            Ramp best = null;
            foreach (var r in Ramps)
            {
                if (r.Lip < s || r.S0 > s + range) continue;
                if (best == null || r.S0 < best.S0) best = r;
            }
            return best;
        }

        public float SampleS(int i) => i * Step;
    }
}
