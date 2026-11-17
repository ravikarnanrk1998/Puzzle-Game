using System.Collections.Generic;
using UA.Core;
using UnityEngine;

namespace UA.Game
{
    /// <summary>
    /// Parametric prop library. Every prop writes its geometry straight into the level's chunk meshes
    /// (via <see cref="Sink"/>), so hundreds of props cost only a handful of draw calls, and registers
    /// box colliders for physics, bullets and the NavMesh.
    /// Local space: origin on the ground, +Z = length / forward, +X = right.
    /// </summary>
    public static class Props
    {
        // ------------------------------------------------------------------ helpers

        static Matrix4x4 T(Vector3 pos, float yaw) => Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one);

        static Matrix4x4 L(Matrix4x4 t, Vector3 p, Vector3 euler) => t * Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one);

        static MeshBuilder P(Sink s, string mat, Matrix4x4 t) => s.Part(mat, t, t.GetColumn(3));

        static void Box(Sink s, string mat, Matrix4x4 t, Vector3 c, Vector3 size, float uv = 1) => P(s, mat, t).Box(0, c, size, uv);

        static void RBox(Sink s, string mat, Matrix4x4 t, Vector3 c, Vector3 size, float r, float uv = 1) => P(s, mat, t).RoundBox(0, c, size, r, 2, uv);

        static void Cyl(Sink s, string mat, Matrix4x4 t, Vector3 a, Vector3 b, float r, int segs = 10, float r2 = -1) => P(s, mat, t).Cylinder(0, a, b, r, r2 < 0 ? r : r2, segs);

        static void Col(Sink s, Matrix4x4 t, Vector3 c, Vector3 size, SurfaceKind k = SurfaceKind.Metal)
        {
            s.Box(t.MultiplyPoint3x4(c), size, t.rotation, k);
        }

        // ------------------------------------------------------------------ vehicles

        public enum CarKind { Sedan, Suv, Van, Taxi, Police, Ambulance }

        /// <summary>Abandoned car: extruded body, glass cabin, pillars, wheels, lights, optional snow cap.</summary>
        public static void Car(Sink s, Vector3 pos, float yaw, Rng r, CarKind kind, bool snow = false, bool wrecked = false)
        {
            float roll = wrecked ? r.Range(-6f, 6f) : r.Range(-1.5f, 1.5f);
            float pitch = wrecked ? r.Range(-3f, 3f) : 0;
            var t = Matrix4x4.TRS(pos, Quaternion.Euler(pitch, yaw, roll), Vector3.one);
            string paint = kind == CarKind.Taxi ? "carYellow" : kind == CarKind.Police || kind == CarKind.Ambulance ? "carWhite" : "car" + r.Range(0, 4);
            if (wrecked) paint = "burnt";
            bool tall = kind == CarKind.Suv || kind == CarKind.Van || kind == CarKind.Ambulance;
            float len = kind == CarKind.Van || kind == CarKind.Ambulance ? 5.1f : kind == CarKind.Suv ? 4.7f : 4.5f;
            float hl = len / 2;
            float belt = tall ? 1.15f : 0.98f, roof = kind == CarKind.Van || kind == CarKind.Ambulance ? 2.15f : tall ? 1.72f : 1.42f;
            // Lower body side profile (z, y).
            var body = new List<Vector2>
            {
                new Vector2(-hl, 0.3f), new Vector2(hl, 0.3f), new Vector2(hl, 0.72f), new Vector2(hl - 0.2f, belt - 0.08f),
                new Vector2(hl - 1.1f, belt), new Vector2(-hl + 0.5f, belt), new Vector2(-hl, belt - 0.12f),
            };
            P(s, paint, t).Extrude(0, body, 1.78f, 0.5f);
            // Cabin glass.
            float cf = kind == CarKind.Van || kind == CarKind.Ambulance ? hl - 0.9f : hl - 1.25f;
            float cr = kind == CarKind.Van || kind == CarKind.Ambulance ? -hl + 0.15f : -hl + 0.85f;
            var cabin = new List<Vector2>
            {
                new Vector2(cr, belt - 0.02f), new Vector2(cf, belt - 0.02f), new Vector2(cf - 0.6f, roof - 0.04f), new Vector2(cr + 0.45f, roof - 0.04f),
            };
            P(s, wrecked || r.Chance(0.3) ? "glassBroken" : "glass", t).Extrude(0, cabin, 1.56f, 0.5f);
            // Roof panel and pillars.
            Box(s, paint, t, new Vector3(0, roof, (cf - 0.6f + cr + 0.45f) / 2), new Vector3(1.6f, 0.06f, (cf - 0.6f) - (cr + 0.45f) + 0.05f));
            foreach (var x in new[] { -0.79f, 0.79f })
            {
                var pt = L(t, new Vector3(x, (belt + roof) / 2, cf - 0.3f), new Vector3(Mathf.Atan2(0.6f, roof - belt) * Mathf.Rad2Deg, 0, 0));
                Box(s, paint, pt, Vector3.zero, new Vector3(0.05f, roof - belt + 0.05f, 0.08f));
                var pr = L(t, new Vector3(x, (belt + roof) / 2, cr + 0.22f), new Vector3(-Mathf.Atan2(0.45f, roof - belt) * Mathf.Rad2Deg, 0, 0));
                Box(s, paint, pr, Vector3.zero, new Vector3(0.05f, roof - belt + 0.05f, 0.1f));
                Box(s, paint, t, new Vector3(x, (belt + roof) / 2, (cf + cr) / 2 - 0.1f), new Vector3(0.05f, roof - belt, 0.07f));
            }
            // Bumpers, lights, plate, mirrors.
            Box(s, "trim", t, new Vector3(0, 0.42f, hl + 0.03f), new Vector3(1.8f, 0.18f, 0.1f));
            Box(s, "trim", t, new Vector3(0, 0.42f, -hl - 0.03f), new Vector3(1.8f, 0.18f, 0.1f));
            bool lit = !wrecked && r.Chance(0.25);
            foreach (var x in new[] { -0.62f, 0.62f })
            {
                Box(s, lit ? "emitWarm" : "chrome", t, new Vector3(x, 0.68f, hl - 0.02f), new Vector3(0.36f, 0.12f, 0.06f));
                Box(s, "emitRedDim", t, new Vector3(x, 0.76f, -hl + 0.01f), new Vector3(0.32f, 0.12f, 0.06f));
                Box(s, paint, t, new Vector3(x * 1.48f, belt + 0.06f, cf - 0.15f), new Vector3(0.18f, 0.1f, 0.08f));
            }
            Box(s, "white", t, new Vector3(0, 0.55f, -hl - 0.05f), new Vector3(0.48f, 0.12f, 0.02f));
            // Wheels.
            foreach (var x in new[] { -0.8f, 0.8f })
                foreach (var z in new[] { -hl + 0.85f, hl - 0.85f })
                {
                    float flat = wrecked || r.Chance(0.15) ? 0.08f : 0;
                    var c = new Vector3(x, 0.34f - flat, z);
                    P(s, "rubber", t).Cylinder(0, c + Vector3.right * -0.12f, c + Vector3.right * 0.12f, 0.34f, 0.34f, 12);
                    if (!wrecked) P(s, "chrome", t).Disc(0, c + Vector3.right * (x > 0 ? 0.125f : -0.125f), Vector3.right * Mathf.Sign(x), Vector3.up, 0.2f, 10);
                }
            // Kind extras.
            if (kind == CarKind.Taxi)
            {
                Box(s, "emitWarm", t, new Vector3(0, roof + 0.1f, 0), new Vector3(0.6f, 0.16f, 0.25f));
            }
            else if (kind == CarKind.Police)
            {
                Box(s, "trim", t, new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.36f, len * 0.6f));
                Box(s, "emitRed", t, new Vector3(-0.3f, roof + 0.08f, 0.1f), new Vector3(0.5f, 0.1f, 0.22f));
                Box(s, "emitBlue", t, new Vector3(0.3f, roof + 0.08f, 0.1f), new Vector3(0.5f, 0.1f, 0.22f));
            }
            else if (kind == CarKind.Ambulance)
            {
                Box(s, "emitRed", t, new Vector3(0, 0.95f, 0), new Vector3(1.81f, 0.14f, len * 0.7f));
                Box(s, "emitRed", t, new Vector3(0, roof + 0.08f, cf - 0.3f), new Vector3(1.2f, 0.1f, 0.2f));
            }
            if (snow)
            {
                RBox(s, "snow", t, new Vector3(0, roof + 0.07f, (cf + cr) / 2), new Vector3(1.55f, 0.16f, 1.7f), 0.07f);
                RBox(s, "snow", t, new Vector3(0, belt + 0.05f, hl - 0.55f), new Vector3(1.6f, 0.14f, 0.9f), 0.06f);
            }
            if (wrecked && r.Chance(0.5)) s.Light(t.MultiplyPoint3x4(new Vector3(0, 1.2f, 0.5f)), new Color(1f, 0.45f, 0.15f), 7, 1.2f, flicker: 0.6f);
            Col(s, t, new Vector3(0, 0.8f, 0), new Vector3(1.85f, 1.6f, len));
        }

        /// <summary>City bus or metro carriage (long box with window band, doors and roof).</summary>
        public static void Carriage(Sink s, Vector3 pos, float yaw, Rng r, bool train, float length)
        {
            if (train) { MetroTrain(s, pos, yaw, length); return; }
            var t = T(pos, yaw);
            float w = train ? 2.9f : 2.55f, h = train ? 3.4f : 3.1f, floorY = train ? 0.9f : 0.35f;
            float hl = length / 2;
            string paint = train ? "trainBody" : "car" + r.Range(0, 4);
            Box(s, paint, t, new Vector3(0, floorY + 0.55f, 0), new Vector3(w, 1.1f, length));
            Box(s, train ? "emitCoolDim" : "glass", t, new Vector3(0, floorY + 1.55f, 0), new Vector3(w - 0.04f, 0.9f, length - 0.3f));
            Box(s, paint, t, new Vector3(0, floorY + 2.3f, 0), new Vector3(w, 0.6f, length));
            RBox(s, paint, t, new Vector3(0, floorY + 2.62f, 0), new Vector3(w - 0.2f, 0.2f, length - 0.2f), 0.08f);
            // Window pillars.
            for (float z = -hl + 1.2f; z < hl - 0.6f; z += 1.6f) Box(s, paint, t, new Vector3(0, floorY + 1.55f, z), new Vector3(w + 0.02f, 0.92f, 0.12f));
            // Doors.
            for (float z = -hl + length * 0.25f; z < hl; z += length * 0.5f)
                foreach (var side in new[] { -1, 1 })
                    Box(s, "trim", t, new Vector3(side * (w / 2 + 0.005f), floorY + 1.05f, z), new Vector3(0.02f, 2f, 1.3f));
            // Stripe.
            Box(s, train ? "accent" : "white", t, new Vector3(0, floorY + 0.95f, 0), new Vector3(w + 0.02f, 0.12f, length + 0.02f));
            // Running gear.
            if (train)
            {
                foreach (var z in new[] { -hl + 2f, hl - 2f })
                {
                    Box(s, "metal", t, new Vector3(0, 0.45f, z), new Vector3(2.2f, 0.5f, 2.6f));
                    foreach (var dz in new[] { -0.7f, 0.7f })
                        foreach (var x in new[] { -0.72f, 0.72f })
                            P(s, "metal", t).Cylinder(0, new Vector3(x - 0.08f, 0.42f, z + dz), new Vector3(x + 0.08f, 0.42f, z + dz), 0.42f, 0.42f, 10);
                }
                // Lit interior glow spilling out.
                for (float z = -hl + 3; z < hl - 2; z += 6)
                    s.Light(t.MultiplyPoint3x4(new Vector3(0, floorY + 2, z)), new Color(0.75f, 0.88f, 1f), 6, 1.4f, flicker: 0.2f);
            }
            else
            {
                foreach (var z in new[] { -hl + 2f, hl - 2.4f })
                    foreach (var x in new[] { -1.2f, 1.2f })
                        P(s, "rubber", t).Cylinder(0, new Vector3(x - 0.15f, 0.5f, z), new Vector3(x + 0.15f, 0.5f, z), 0.5f, 0.5f, 12);
            }
            Col(s, t, new Vector3(0, (floorY + h) / 2, 0), new Vector3(w, floorY + h, length));
        }

        /// <summary>
        /// Parked metro train: silver cars with a blue livery band and red pinstripe, framed windows onto a lit
        /// interior, glazed sliding doors, roof equipment, bogies, gangways and cab ends with head/tail lights.
        /// Its collider is the same box the old carriage used, so cover and navigation are unchanged.
        /// </summary>
        static void MetroTrain(Sink s, Vector3 pos, float yaw, float length)
        {
            var t = T(pos, yaw);
            const float w = 2.9f, floorY = 0.95f;
            float hl = length / 2, roofY = floorY + 2.75f;
            int cars = Mathf.Max(1, Mathf.RoundToInt(length / 15f));
            float carLen = length / cars;
            for (int k = 0; k < cars; k++)
            {
                float zc = -hl + carLen * (k + 0.5f);
                float cl = carLen - 0.5f, ch = cl / 2;
                Box(s, "trainBody", t, new Vector3(0, floorY + 0.5f, zc), new Vector3(w, 1.0f, cl), 0.5f);
                Box(s, "metalDark", t, new Vector3(0, floorY - 0.15f, zc), new Vector3(w - 0.2f, 0.3f, cl - 0.4f));
                Box(s, "trainBody", t, new Vector3(0, floorY + 2.32f, zc), new Vector3(w, 0.54f, cl), 0.5f);
                RBox(s, "trainBody", t, new Vector3(0, roofY - 0.08f, zc), new Vector3(w - 0.12f, 0.36f, cl - 0.1f), 0.16f, 0.5f);
                // Livery.
                Box(s, "trainBlue", t, new Vector3(0, floorY + 0.8f, zc), new Vector3(w + 0.02f, 0.34f, cl + 0.01f));
                Box(s, "accent", t, new Vector3(0, floorY + 0.58f, zc), new Vector3(w + 0.025f, 0.06f, cl + 0.012f));
                Box(s, "trainBlue", t, new Vector3(0, floorY + 2.45f, zc), new Vector3(w + 0.02f, 0.08f, cl + 0.01f));
                // Lit interior seen through the window band, framed by pillars, sill and header.
                Box(s, "trainWindow", t, new Vector3(0, floorY + 1.55f, zc), new Vector3(w - 0.06f, 1.0f, cl - 0.2f));
                float d0 = zc - cl * 0.25f, d1 = zc + cl * 0.25f;
                for (float z = zc - ch + 0.35f; z <= zc + ch - 0.3f; z += 1.45f)
                {
                    if (Mathf.Abs(z - d0) < 1.0f || Mathf.Abs(z - d1) < 1.0f) continue;
                    Box(s, "trainBody", t, new Vector3(0, floorY + 1.55f, z), new Vector3(w + 0.02f, 1.02f, 0.24f));
                }
                Box(s, "rubber", t, new Vector3(0, floorY + 1.04f, zc), new Vector3(w + 0.03f, 0.05f, cl - 0.2f));
                Box(s, "rubber", t, new Vector3(0, floorY + 2.06f, zc), new Vector3(w + 0.03f, 0.05f, cl - 0.2f));
                // Doors: body-coloured leaves with tall windows and a dark centre seam.
                foreach (var dz in new[] { d0, d1 })
                    foreach (var side in new[] { -1, 1 })
                    {
                        Box(s, "trainBody", t, new Vector3(side * (w / 2 + 0.012f), floorY + 1.05f, dz), new Vector3(0.03f, 2.1f, 1.4f));
                        foreach (var dd in new[] { -0.36f, 0.36f })
                            Box(s, "trainWindow", t, new Vector3(side * (w / 2 + 0.03f), floorY + 1.5f, dz + dd), new Vector3(0.01f, 0.95f, 0.46f));
                        Box(s, "rubber", t, new Vector3(side * (w / 2 + 0.032f), floorY + 1.05f, dz), new Vector3(0.012f, 2.1f, 0.04f));
                    }
                // Roof equipment and bogies.
                Box(s, "metal", t, new Vector3(0, roofY + 0.22f, zc - ch * 0.4f), new Vector3(1.6f, 0.3f, 2.4f));
                Box(s, "metal", t, new Vector3(0, roofY + 0.16f, zc + ch * 0.45f), new Vector3(1.2f, 0.18f, 1.6f));
                foreach (var bz in new[] { zc - ch + 2.2f, zc + ch - 2.2f })
                {
                    Box(s, "metalDark", t, new Vector3(0, 0.52f, bz), new Vector3(2.3f, 0.42f, 2.7f));
                    foreach (var dz in new[] { -0.8f, 0.8f })
                        foreach (var x in new[] { -0.72f, 0.72f })
                            P(s, "metal", t).Cylinder(0, new Vector3(x - 0.07f, 0.42f, bz + dz), new Vector3(x + 0.07f, 0.42f, bz + dz), 0.42f, 0.42f, 12);
                }
                s.Light(t.MultiplyPoint3x4(new Vector3(0, floorY + 2.0f, zc)), new Color(1f, 0.92f, 0.78f), 7, 1.1f);
            }
            for (int k = 1; k < cars; k++)
                Box(s, "rubber", t, new Vector3(0, floorY + 1.4f, -hl + carLen * k), new Vector3(2.2f, 2.4f, 0.6f));
            // Cab ends.
            foreach (var end in new[] { -1, 1 })
            {
                float ez = end * (hl - 0.25f + 0.02f);
                Box(s, "glass", t, new Vector3(0, floorY + 1.72f, ez), new Vector3(w - 0.5f, 0.9f, 0.04f));
                Box(s, "trainBlue", t, new Vector3(0, floorY + 0.8f, ez), new Vector3(w, 0.34f, 0.045f));
                foreach (var x in new[] { -0.95f, 0.95f })
                {
                    Box(s, "headlight", t, new Vector3(x, floorY + 0.4f, ez + end * 0.01f), new Vector3(0.3f, 0.14f, 0.03f));
                    Box(s, "emitRed", t, new Vector3(x * 0.72f, floorY + 0.4f, ez + end * 0.01f), new Vector3(0.14f, 0.1f, 0.03f));
                }
            }
            Col(s, t, new Vector3(0, (0.9f + 3.4f) / 2, 0), new Vector3(w, 0.9f + 3.4f, length));
        }

        /// <summary>Military truck with a canvas-covered bed.</summary>
        public static void Truck(Sink s, Vector3 pos, float yaw, Rng r, bool desert)
        {
            var t = T(pos, yaw);
            string paint = desert ? "tan" : "olive";
            Box(s, "metal", t, new Vector3(0, 0.75f, 0), new Vector3(2.0f, 0.3f, 7f));
            RBox(s, paint, t, new Vector3(0, 1.55f, 2.6f), new Vector3(2.3f, 1.5f, 1.8f), 0.08f);
            Box(s, "glassBroken", t, new Vector3(0, 1.95f, 3.52f), new Vector3(1.9f, 0.6f, 0.04f));
            Box(s, paint, t, new Vector3(0, 1.0f, -1f), new Vector3(2.4f, 0.4f, 4.6f));
            var canvas = new List<Vector2> { new Vector2(-1.2f, 0), new Vector2(1.2f, 0), new Vector2(1.2f, 1.4f), new Vector2(0.9f, 1.75f), new Vector2(-0.9f, 1.75f), new Vector2(-1.2f, 1.4f) };
            var ct = L(t, new Vector3(0, 1.2f, -1f), new Vector3(0, 90, 0));
            P(s, desert ? "canvasTan" : "canvas", ct).Extrude(0, canvas, 4.6f, 0.5f);
            foreach (var z in new[] { 2.6f, -0.2f, -2.4f })
                foreach (var x in new[] { -1.05f, 1.05f })
                    P(s, "rubber", t).Cylinder(0, new Vector3(x - 0.18f, 0.55f, z), new Vector3(x + 0.18f, 0.55f, z), 0.55f, 0.55f, 12);
            Col(s, t, new Vector3(0, 1.5f, 0), new Vector3(2.5f, 3f, 7.2f));
        }

        /// <summary>Burnt-out tank hull with a turret and barrel.</summary>
        public static void TankWreck(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, r.Range(-4f, 4f)), Vector3.one);
            var hull = new List<Vector2> { new Vector2(-3.4f, 0.4f), new Vector2(3.2f, 0.4f), new Vector2(3.6f, 1.0f), new Vector2(3.0f, 1.55f), new Vector2(-3.2f, 1.55f), new Vector2(-3.5f, 1.1f) };
            P(s, "burnt", t).Extrude(0, hull, 3.3f, 0.4f);
            foreach (var x in new[] { -1.45f, 1.45f }) RBox(s, "rubber", t, new Vector3(x, 0.55f, 0), new Vector3(0.6f, 1.0f, 6.8f), 0.3f);
            var tt = L(t, new Vector3(0, 1.55f, -0.3f), new Vector3(0, r.Range(-50f, 50f), 0));
            RBox(s, "burnt", tt, new Vector3(0, 0.45f, 0), new Vector3(2.3f, 0.9f, 2.8f), 0.2f);
            Cyl(s, "burnt", tt, new Vector3(0, 0.5f, 1.3f), new Vector3(0, 0.6f - r.Range(0, 0.5f), 5.2f), 0.13f, 8);
            Col(s, t, new Vector3(0, 1.2f, 0), new Vector3(3.4f, 2.4f, 7f));
        }

        /// <summary>Crashed airliner section: broken fuselage, wing stub and tail.</summary>
        public static void PlaneWreck(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, r.Range(-8f, 8f)), Vector3.one);
            Cyl(s, "planeWhite", t, new Vector3(0, 2.1f, -9f), new Vector3(0, 2.1f, 9f), 2.1f, 18);
            P(s, "planeWhite", t).Sphere(0, new Vector3(0, 2.1f, 9f), new Vector3(2.1f, 2.1f, 3f), 14, 7);
            for (float z = -8; z < 8; z += 1.1f)
                foreach (var side in new[] { -1, 1 })
                    Box(s, "emitWarmDim", t, new Vector3(side * 2.02f, 2.6f, z), new Vector3(0.12f, 0.3f, 0.22f));
            var wing = L(t, new Vector3(0, 1.0f, -1f), new Vector3(0, 0, -6));
            Box(s, "planeWhite", wing, new Vector3(-7f, 0, 0), new Vector3(12f, 0.35f, 3.5f));
            Box(s, "planeWhite", wing, new Vector3(5f, 0, 0), new Vector3(4f, 0.35f, 3.5f));
            Box(s, "burnt", t, new Vector3(0, 6f, -8f), new Vector3(0.4f, 4f, 3f));
            Cyl(s, "metal", t, new Vector3(-5.5f, 0.3f, 0.5f), new Vector3(-5.5f, 0.3f, -2.5f), 0.9f, 12);
            s.Light(t.MultiplyPoint3x4(new Vector3(0, 1f, -9.5f)), new Color(1f, 0.45f, 0.12f), 10, 2f, flicker: 0.5f);
            Col(s, t, new Vector3(0, 2.1f, 0), new Vector3(4.2f, 4.2f, 21f));
            Col(s, wing, new Vector3(-6f, 0, 0), new Vector3(14f, 1f, 3.5f));
        }

        // ------------------------------------------------------------------ barriers & cover

        /// <summary>Striped sawhorse barricade with amber warning lamps (lamps may light up).</summary>
        public static void Barricade(Sink s, Vector3 pos, float yaw, Rng r, bool light)
        {
            var t = T(pos, yaw);
            float w = 2.4f;
            foreach (var x in new[] { -w / 2 + 0.15f, w / 2 - 0.15f })
            {
                var l1 = L(t, new Vector3(x, 0.55f, 0.18f), new Vector3(-18, 0, 0));
                var l2 = L(t, new Vector3(x, 0.55f, -0.18f), new Vector3(18, 0, 0));
                Box(s, "trim", l1, Vector3.zero, new Vector3(0.08f, 1.15f, 0.08f));
                Box(s, "trim", l2, Vector3.zero, new Vector3(0.08f, 1.15f, 0.08f));
            }
            Box(s, "hazard", t, new Vector3(0, 0.95f, 0), new Vector3(w, 0.25f, 0.06f), 0.8f);
            Box(s, "hazard", t, new Vector3(0, 0.55f, 0), new Vector3(w, 0.25f, 0.06f), 0.8f);
            foreach (var x in new[] { -w / 2 + 0.2f, w / 2 - 0.2f })
            {
                Box(s, "trim", t, new Vector3(x, 1.12f, 0), new Vector3(0.1f, 0.1f, 0.1f));
                P(s, light ? "emitAmber" : "amberDim", t).Sphere(0, new Vector3(x, 1.24f, 0), new Vector3(0.1f, 0.1f, 0.1f), 8, 5);
            }
            if (light) s.Light(t.MultiplyPoint3x4(new Vector3(0, 1.3f, 0.4f)), new Color(1f, 0.62f, 0.2f), 7f, 0.8f, flicker: 0.12f);
            Col(s, t, new Vector3(0, 0.7f, 0), new Vector3(w, 1.4f, 0.5f), SurfaceKind.Wood);
        }

        public static void Jersey(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            var prof = new List<Vector2> { new Vector2(-0.3f, 0), new Vector2(0.3f, 0), new Vector2(0.3f, 0.08f), new Vector2(0.12f, 0.3f), new Vector2(0.08f, 0.82f), new Vector2(-0.08f, 0.82f), new Vector2(-0.12f, 0.3f), new Vector2(-0.3f, 0.08f) };
            P(s, "concrete", L(t, Vector3.zero, new Vector3(0, 90, 0))).Extrude(0, prof, 3f, 0.6f);
            if (r.Chance(0.5)) Box(s, "hazard", t, new Vector3(0, 0.6f, 0), new Vector3(3.01f, 0.15f, 0.18f), 0.8f);
            Col(s, t, new Vector3(0, 0.41f, 0), new Vector3(3f, 0.82f, 0.6f), SurfaceKind.Concrete);
        }

        public static void Sandbags(Sink s, Vector3 pos, float yaw, Rng r, float length, int rows = 3)
        {
            var t = T(pos, yaw);
            for (int row = 0; row < rows; row++)
            {
                float off = row % 2 == 0 ? 0 : 0.3f;
                for (float x = -length / 2 + 0.3f + off; x < length / 2 - 0.2f; x += 0.6f)
                {
                    var bt = L(t, new Vector3(x, 0.13f + row * 0.24f, r.Range(-0.03f, 0.03f)), new Vector3(0, r.Range(-6f, 6f), r.Range(-4f, 4f)));
                    RBox(s, "sandbag", bt, Vector3.zero, new Vector3(0.58f, 0.24f, 0.4f), 0.1f);
                }
            }
            Col(s, t, new Vector3(0, rows * 0.12f, 0), new Vector3(length, rows * 0.24f, 0.45f), SurfaceKind.Dirt);
        }

        /// <summary>Mesh-filled barrier cubes (HESCO style) in a row.</summary>
        public static void Hesco(Sink s, Vector3 pos, float yaw, Rng r, int count)
        {
            var t = T(pos, yaw);
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) / 2f) * 1.1f;
                Box(s, "canvasTan", t, new Vector3(x, 0.65f, 0), new Vector3(1.05f, 1.3f, 1.05f), 0.8f);
                Box(s, "dirtTop", t, new Vector3(x, 1.31f, 0), new Vector3(0.98f, 0.04f, 0.98f));
            }
            Col(s, t, new Vector3(0, 0.65f, 0), new Vector3(count * 1.1f, 1.3f, 1.1f), SurfaceKind.Dirt);
        }

        public static void Crates(Sink s, Vector3 pos, float yaw, Rng r, bool military)
        {
            var t = T(pos, yaw);
            // Wooden stacks stay below ~2.2 m so they never reach up into indoor ceiling lamps (inverse-square blow-out).
            int n = military ? r.Range(1, 4) : r.Range(1, 3);
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float sz = military ? 0.7f : r.Range(0.8f, 1.1f);
                var ct = L(t, new Vector3(r.Range(-0.15f, 0.15f), y, r.Range(-0.15f, 0.15f)), new Vector3(0, r.Range(-20f, 20f), 0));
                if (military)
                {
                    Box(s, "olive", ct, new Vector3(0, sz * 0.35f, 0), new Vector3(sz * 1.6f, sz * 0.7f, sz));
                    Box(s, "trim", ct, new Vector3(0, sz * 0.72f, 0), new Vector3(sz * 1.62f, 0.04f, sz * 1.02f));
                    y += sz * 0.7f;
                }
                else
                {
                    Box(s, "wood", ct, new Vector3(0, sz / 2, 0), new Vector3(sz, sz, sz), 1.2f);
                    foreach (var e in new[] { -1, 1 })
                    {
                        Box(s, "woodDark", ct, new Vector3(e * sz / 2, sz / 2, 0), new Vector3(0.03f, sz, 0.12f));
                        Box(s, "woodDark", ct, new Vector3(0, sz / 2, e * sz / 2), new Vector3(0.12f, sz, 0.03f));
                    }
                    y += sz;
                }
                if (r.Chance(0.4)) break;
            }
            Col(s, t, new Vector3(0, y / 2, 0), new Vector3(1.4f, y, 1.2f), SurfaceKind.Wood);
        }

        public static void Barrels(Sink s, Vector3 pos, Rng r, string paint)
        {
            int n = r.Range(1, 4);
            for (int i = 0; i < n; i++)
            {
                var p = pos + new Vector3(r.Range(-0.6f, 0.6f), 0, r.Range(-0.6f, 0.6f));
                bool tipped = r.Chance(0.2);
                var t = tipped ? Matrix4x4.TRS(p + Vector3.up * 0.3f, Quaternion.Euler(90, r.Range(0, 360f), 0), Vector3.one) * Matrix4x4.Translate(Vector3.down * 0.45f) : T(p, r.Range(0, 360f));
                Cyl(s, paint, t, Vector3.zero, Vector3.up * 0.9f, 0.3f, 12);
                Cyl(s, "metal", t, Vector3.up * 0.28f, Vector3.up * 0.32f, 0.31f, 12);
                Cyl(s, "metal", t, Vector3.up * 0.6f, Vector3.up * 0.64f, 0.31f, 12);
                s.Box(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f), 0, SurfaceKind.Metal);
            }
        }

        public static void Dumpster(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            string paint = r.Chance(0.5) ? "dumpGreen" : "dumpBlue";
            var prof = new List<Vector2> { new Vector2(-0.9f, 0.2f), new Vector2(0.9f, 0.2f), new Vector2(1.0f, 1.3f), new Vector2(-1.0f, 1.3f) };
            P(s, paint, L(t, Vector3.zero, new Vector3(0, 90, 0))).Extrude(0, prof, 1.9f, 0.5f);
            var lid = L(t, new Vector3(0, 1.32f, 0.2f), new Vector3(r.Range(-35f, 5f), 0, 0));
            Box(s, "trim", lid, new Vector3(0, 0, -0.2f), new Vector3(1.95f, 0.05f, 1.05f));
            for (int i = 0; i < 4; i++) P(s, "rubber", t).Sphere(0, new Vector3(i < 2 ? -0.8f : 0.8f, 0.1f, i % 2 == 0 ? -0.8f : 0.8f), Vector3.one * 0.1f, 6, 4);
            Col(s, t, new Vector3(0, 0.7f, 0), new Vector3(1.95f, 1.4f, 2f));
        }

        public static void TrashBags(Sink s, Vector3 pos, Rng r)
        {
            int n = r.Range(2, 6);
            for (int i = 0; i < n; i++)
            {
                var p = pos + new Vector3(r.Range(-0.7f, 0.7f), 0, r.Range(-0.7f, 0.7f));
                var t = T(p, r.Range(0, 360f));
                P(s, "trashbag", t).Sphere(0, new Vector3(0, 0.22f, 0), new Vector3(r.Range(0.28f, 0.38f), r.Range(0.2f, 0.3f), r.Range(0.25f, 0.35f)), 9, 6);
                P(s, "trashbag", t).Cylinder(0, new Vector3(0, 0.4f, 0), new Vector3(0.03f, 0.55f, 0.02f), 0.06f, 0.02f, 6);
            }
        }

        public static void Rubble(Sink s, Vector3 pos, Rng r, float size)
        {
            int n = (int)(6 * size) + r.Range(2, 6);
            for (int i = 0; i < n; i++)
            {
                var p = pos + new Vector3(r.Range(-1f, 1f) * size, 0, r.Range(-1f, 1f) * size);
                float sz = r.Range(0.15f, 0.55f) * size;
                var t = Matrix4x4.TRS(p, Quaternion.Euler(r.Range(0, 360f), r.Range(0, 360f), r.Range(0, 360f)), new Vector3(1, r.Range(0.4f, 0.8f), r.Range(0.6f, 1.2f)));
                P(s, "concrete", t).Box(0, Vector3.zero, Vector3.one * sz);
            }
            if (r.Chance(0.6))
            {
                var t = T(pos, r.Range(0, 360f)) * Matrix4x4.Rotate(Quaternion.Euler(r.Range(-20f, 20f), 0, r.Range(10f, 30f)));
                Box(s, "woodDark", t, new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.05f, 2.2f * size));
                Cyl(s, "rust", t, new Vector3(0.3f, 0.2f, -0.8f), new Vector3(0.4f, 0.4f, 0.9f), 0.02f, 4);
            }
            s.Box(pos + Vector3.up * 0.2f * size, new Vector3(1.6f * size, 0.4f * size, 1.6f * size), 0, SurfaceKind.Concrete);
        }

        // ------------------------------------------------------------------ street furniture

        /// <summary>Street lamp; lit ones get a sodium spot light and a dusty light cone.</summary>
        public static void StreetLight(Sink s, Vector3 pos, float yaw, Rng r, Color c, bool lit, bool flicker)
        {
            var t = T(pos, yaw);
            float h = 6.5f;
            Cyl(s, "metalDark", t, Vector3.zero, Vector3.up * h, 0.09f, 8, 0.07f);
            Cyl(s, "metalDark", t, Vector3.up * 0.0f, Vector3.up * 0.5f, 0.16f, 8, 0.12f);
            var arm = L(t, new Vector3(0, h, 0), new Vector3(0, 0, 0));
            Cyl(s, "metalDark", arm, Vector3.zero, new Vector3(0, 0.25f, 1.6f), 0.05f, 6);
            RBox(s, "metalDark", arm, new Vector3(0, 0.22f, 1.85f), new Vector3(0.35f, 0.12f, 0.7f), 0.05f);
            Box(s, lit ? "emitSodium" : "glassBroken", arm, new Vector3(0, 0.15f, 1.85f), new Vector3(0.28f, 0.03f, 0.6f));
            if (lit) s.Light(t.MultiplyPoint3x4(new Vector3(0, h - 0.05f, 1.85f)), c, 15, 4.2f, spot: true, angle: 120, flicker: flicker ? 0.35f : 0, beam: true);
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(0.25f, h, 0.25f));
        }

        /// <summary>Back-lit advertising lightbox (wall-mounted or free-standing) that throws coloured light.</summary>
        public static void Lightbox(Sink s, Vector3 pos, float yaw, Rng r, bool standing, int poster, bool lit)
        {
            var t = T(pos, yaw);
            float w = 1.5f, h = 2.3f, y0 = standing ? 0.1f : 0.6f;
            Box(s, "trim", t, new Vector3(0, y0 + h / 2, 0), new Vector3(w + 0.12f, h + 0.12f, 0.2f));
            var b = P(s, lit ? "posterLit" : "posterDim", t);
            float cu = (poster % 4) * 0.25f, cv = (poster / 4 % 2) * 0.5f;
            foreach (var face in standing ? new[] { 1, -1 } : new[] { 1 })
            {
                var o = new Vector3(-face * w / 2, y0, face * 0.105f);
                // U runs right-to-left along the plane's u axis: the face is seen from its normal side (u x v), where the
                // u axis points to the viewer's left; mapping it left-to-right printed the posters mirrored.
                b.Plane(0, o, new Vector3(face * w, 0, 0), new Vector3(0, h, 0), new Vector2(cu + 0.25f, cv), new Vector2(cu, cv + 0.5f));
            }
            if (lit)
            {
                var c = Color.HSVToRGB(r.Float(), 0.35f, 1f);
                s.Light(t.MultiplyPoint3x4(new Vector3(0, y0 + h * 0.5f, 1.2f)), Color.Lerp(c, new Color(0.6f, 0.8f, 1f), 0.5f), 7, 0.8f, flicker: 0.05f);
            }
            if (standing) Col(s, t, new Vector3(0, h / 2 + y0, 0), new Vector3(w + 0.1f, h + y0, 0.3f));
        }

        public static void Bench(Sink s, Vector3 pos, float yaw, bool metal)
        {
            var t = T(pos, yaw);
            string m = metal ? "metalDark" : "wood";
            for (int i = 0; i < 3; i++) Box(s, m, t, new Vector3(0, 0.45f, -0.15f + i * 0.15f), new Vector3(1.8f, 0.04f, 0.12f));
            for (int i = 0; i < 2; i++) Box(s, m, t, new Vector3(0, 0.65f + i * 0.15f, 0.26f), new Vector3(1.8f, 0.1f, 0.04f));
            foreach (var x in new[] { -0.8f, 0.8f }) Box(s, "metalDark", t, new Vector3(x, 0.22f, 0.05f), new Vector3(0.06f, 0.45f, 0.5f));
            Col(s, t, new Vector3(0, 0.4f, 0.05f), new Vector3(1.8f, 0.8f, 0.55f), SurfaceKind.Wood);
        }

        /// <summary>Tall potted plant: dark planter, soil and a crown of long arching leaves.</summary>
        public static void Plant(Sink s, Vector3 pos, Rng r)
        {
            var t = T(pos, r.Range(0, 360f));
            P(s, "potDark", t).Lathe(0, new[] { new Vector2(0.24f, 0), new Vector2(0.3f, 0.62f), new Vector2(0.32f, 0.66f), new Vector2(0.27f, 0.66f) }, 14);
            P(s, "dirtTop", t).Disc(0, new Vector3(0, 0.6f, 0), Vector3.up, Vector3.right, 0.27f, 12);
            int leaves = r.Range(9, 14);
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * 360f / leaves + r.Range(-14f, 14f);
                float tilt = r.Range(18f, 62f);
                float len = r.Range(0.55f, 0.95f);
                var lt = L(t, new Vector3(0, 0.62f, 0), new Vector3(0, yaw, 0)) * Matrix4x4.Rotate(Quaternion.Euler(-90 + tilt, 0, 0));
                P(s, r.Chance(0.4) ? "leafLight" : "leaf", lt).Sphere(0, new Vector3(0, 0, len * 0.5f), new Vector3(0.07f, 0.012f, len * 0.5f), 6, 4);
            }
            Cyl(s, "bark", t, new Vector3(0, 0.6f, 0), new Vector3(0, 1.0f, 0), 0.025f, 5);
            Col(s, t, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.7f, 0.6f), SurfaceKind.Wood);
        }

        /// <summary>Wall-mounted fire extinguisher cabinet with a glass door.</summary>
        public static void Extinguisher(Sink s, Vector3 pos, float yaw)
        {
            var t = T(pos, yaw);
            Box(s, "red", t, new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.9f, 0.2f));
            Box(s, "glass", t, new Vector3(0, 1.2f, 0.102f), new Vector3(0.4f, 0.75f, 0.01f));
            Cyl(s, "red", t, new Vector3(0, 0.88f, 0.03f), new Vector3(0, 1.45f, 0.03f), 0.08f, 10);
            Box(s, "white", t, new Vector3(0, 1.72f, 0.02f), new Vector3(0.5f, 0.12f, 0.16f));
        }

        public static void Hydrant(Sink s, Vector3 pos)
        {
            var t = T(pos, 0);
            Cyl(s, "red", t, Vector3.zero, Vector3.up * 0.65f, 0.12f, 10);
            P(s, "red", t).Sphere(0, Vector3.up * 0.68f, new Vector3(0.13f, 0.1f, 0.13f), 8, 5);
            Cyl(s, "red", t, new Vector3(-0.2f, 0.45f, 0), new Vector3(0.2f, 0.45f, 0), 0.05f, 6);
        }

        public static void Cone(Sink s, Vector3 pos, Rng r)
        {
            var t = r.Chance(0.25) ? Matrix4x4.TRS(pos + Vector3.up * 0.15f, Quaternion.Euler(90, r.Range(0, 360f), 0), Vector3.one) : T(pos, r.Range(0, 360f));
            Box(s, "orange", t, new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.04f, 0.4f));
            Cyl(s, "orange", t, Vector3.zero, Vector3.up * 0.7f, 0.15f, 10, 0.03f);
            Cyl(s, "white", t, Vector3.up * 0.38f, Vector3.up * 0.48f, 0.095f, 10, 0.075f);
        }

        public static void BusStop(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            foreach (var x in new[] { -1.4f, 1.4f }) Cyl(s, "metalDark", t, new Vector3(x, 0, -0.5f), new Vector3(x, 2.4f, -0.5f), 0.04f, 6);
            Box(s, "metalDark", t, new Vector3(0, 2.45f, -0.1f), new Vector3(3.1f, 0.08f, 1.3f));
            Box(s, r.Chance(0.5) ? "glassBroken" : "glass", t, new Vector3(0, 1.3f, -0.55f), new Vector3(2.8f, 2f, 0.03f));
            Lightbox(s, pos + Quaternion.Euler(0, yaw, 0) * new Vector3(1.6f, 0, -0.4f), yaw + 90, r, true, r.Range(0, 8), r.Chance(0.6));
            Bench(s, pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -0.2f), yaw + 180, true);
            Col(s, t, new Vector3(0, 1.2f, -0.55f), new Vector3(3f, 2.4f, 0.2f), SurfaceKind.Glass);
        }

        // ------------------------------------------------------------------ indoor furniture

        public static void Column(Sink s, Vector3 pos, float height, string mat, bool hazard, bool round)
        {
            var t = T(pos, 0);
            if (round) Cyl(s, mat, t, Vector3.zero, Vector3.up * height, 0.35f, 14);
            else Box(s, mat, t, new Vector3(0, height / 2, 0), new Vector3(0.7f, height, 0.7f), 0.5f);
            Box(s, "trim", t, new Vector3(0, 0.08f, 0), new Vector3(0.78f, 0.16f, 0.78f));
            if (hazard) Box(s, "hazard", t, new Vector3(0, 0.8f, 0), new Vector3(0.72f, 0.5f, 0.72f), 1.2f);
            s.Box(pos + Vector3.up * height / 2, new Vector3(0.75f, height, 0.75f), 0, SurfaceKind.Concrete);
        }

        public static void HospitalBed(Sink s, Vector3 pos, float yaw, Rng r, bool gurney)
        {
            var t = T(pos, yaw);
            float h = gurney ? 0.8f : 0.6f;
            Box(s, "whiteMetal", t, new Vector3(0, h, 0), new Vector3(0.95f, 0.06f, 2.05f));
            RBox(s, gurney ? "sheet" : "mattress", t, new Vector3(0, h + 0.1f, 0), new Vector3(0.9f, 0.16f, 1.95f), 0.06f);
            if (!gurney)
            {
                Box(s, "whiteMetal", t, new Vector3(0, h + 0.35f, -1.0f), new Vector3(0.95f, 0.75f, 0.05f));
                RBox(s, "sheet", t, new Vector3(0, h + 0.22f, -0.75f), new Vector3(0.6f, 0.12f, 0.35f), 0.05f);
            }
            foreach (var x in new[] { -0.4f, 0.4f })
                foreach (var z in new[] { -0.9f, 0.9f })
                {
                    Cyl(s, "whiteMetal", t, new Vector3(x, 0.08f, z), new Vector3(x, h, z), 0.025f, 6);
                    P(s, "rubber", t).Sphere(0, new Vector3(x, 0.06f, z), Vector3.one * 0.06f, 6, 4);
                }
            if (r.Chance(0.6)) Box(s, "bloodStain", t, new Vector3(0, h + 0.185f, r.Range(-0.4f, 0.4f)), new Vector3(0.6f, 0.005f, 0.8f));
            Col(s, t, new Vector3(0, h / 2 + 0.1f, 0), new Vector3(0.95f, h + 0.2f, 2.05f), SurfaceKind.Metal);
        }

        public static void IvStand(Sink s, Vector3 pos)
        {
            var t = T(pos, 0);
            Cyl(s, "whiteMetal", t, Vector3.zero, Vector3.up * 1.9f, 0.015f, 6);
            Box(s, "whiteMetal", t, new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.03f, 0.05f));
            Box(s, "whiteMetal", t, new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.03f, 0.5f));
            P(s, "glass", t).Sphere(0, new Vector3(0.08f, 1.75f, 0), new Vector3(0.07f, 0.12f, 0.04f), 6, 4);
        }

        public static void Shelves(Sink s, Vector3 pos, float yaw, Rng r, float width = 2f)
        {
            var t = T(pos, yaw);
            float h = 2.2f, d = 0.6f;
            foreach (var x in new[] { -width / 2, width / 2 }) Box(s, "metalDark", t, new Vector3(x, h / 2, 0), new Vector3(0.05f, h, d));
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.65f;
                Box(s, "metal", t, new Vector3(0, y, 0), new Vector3(width, 0.04f, d));
                for (float x = -width / 2 + 0.2f; x < width / 2 - 0.2f; x += r.Range(0.25f, 0.5f))
                {
                    if (r.Chance(0.35)) continue;
                    float bw = r.Range(0.2f, 0.4f), bh = r.Range(0.15f, 0.4f);
                    Box(s, r.Chance(0.6) ? "cardboard" : "white", L(t, new Vector3(x, y + 0.02f, r.Range(-0.1f, 0.1f)), new Vector3(0, r.Range(-15f, 15f), 0)), new Vector3(0, bh / 2, 0), new Vector3(bw, bh, bw * 0.9f));
                }
            }
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(width, h, d), SurfaceKind.Metal);
        }

        public static void Desk(Sink s, Vector3 pos, float yaw, Rng r, bool console)
        {
            var t = T(pos, yaw);
            Box(s, console ? "metalDark" : "wood", t, new Vector3(0, 0.74f, 0), new Vector3(1.6f, 0.05f, 0.8f));
            Box(s, console ? "metalDark" : "woodDark", t, new Vector3(0, 0.37f, -0.35f), new Vector3(1.55f, 0.72f, 0.05f));
            foreach (var x in new[] { -0.75f, 0.75f }) Box(s, "metalDark", t, new Vector3(x, 0.37f, 0), new Vector3(0.05f, 0.72f, 0.75f));
            int screens = console ? 3 : r.Range(0, 2);
            for (int i = 0; i < screens; i++)
            {
                var st = L(t, new Vector3((i - (screens - 1) / 2f) * 0.5f, 0.77f, -0.15f), new Vector3(-10, r.Range(-12f, 12f), 0));
                Box(s, "trim", st, new Vector3(0, 0.22f, 0), new Vector3(0.46f, 0.3f, 0.04f));
                Box(s, r.Chance(0.6) ? "emitScreen" : "glass", st, new Vector3(0, 0.22f, 0.022f), new Vector3(0.42f, 0.26f, 0.005f));
            }
            if (r.Chance(0.5)) Box(s, "white", L(t, new Vector3(r.Range(-0.5f, 0.5f), 0.765f, 0.15f), new Vector3(0, r.Range(0, 90f), 0)), Vector3.zero, new Vector3(0.21f, 0.01f, 0.3f));
            Col(s, t, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 0.8f), SurfaceKind.Wood);
        }

        public static void ServerRack(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            Box(s, "trim", t, new Vector3(0, 1.05f, 0), new Vector3(0.65f, 2.1f, 0.9f));
            for (int i = 0; i < 10; i++)
            {
                Box(s, "metalDark", t, new Vector3(0, 0.2f + i * 0.18f, 0.451f), new Vector3(0.6f, 0.14f, 0.01f));
                if (r.Chance(0.7)) Box(s, r.Chance(0.75) ? "emitGreen" : "emitRed", t, new Vector3(r.Range(-0.25f, 0.25f), 0.2f + i * 0.18f, 0.457f), new Vector3(0.02f, 0.02f, 0.005f));
            }
            Col(s, t, new Vector3(0, 1.05f, 0), new Vector3(0.65f, 2.1f, 0.9f));
        }

        /// <summary>Industrial machine: housing, control panel, pipes, vents and warning stripes.</summary>
        public static void Machine(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            float w = r.Range(2f, 3f), d = r.Range(1.6f, 2.4f), h = r.Range(1.6f, 2.6f);
            string paint = r.Chance(0.5) ? "machineGreen" : "machineGray";
            RBox(s, paint, t, new Vector3(0, h / 2, 0), new Vector3(w, h, d), 0.06f, 0.5f);
            Box(s, "hazard", t, new Vector3(0, 0.12f, 0), new Vector3(w + 0.02f, 0.24f, d + 0.02f));
            var panel = L(t, new Vector3(w * 0.25f, h * 0.6f, d / 2 + 0.05f), new Vector3(-15, 0, 0));
            Box(s, "trim", panel, Vector3.zero, new Vector3(0.8f, 0.5f, 0.1f));
            for (int i = 0; i < 6; i++) Box(s, i % 3 == 0 ? "emitRed" : "emitGreen", panel, new Vector3(-0.3f + i * 0.12f, 0.12f, 0.055f), new Vector3(0.05f, 0.05f, 0.01f));
            Box(s, "emitScreen", panel, new Vector3(0, -0.08f, 0.052f), new Vector3(0.5f, 0.2f, 0.005f));
            int pipes = r.Range(1, 4);
            for (int i = 0; i < pipes; i++)
            {
                float x = -w / 2 + 0.3f + i * 0.35f;
                Cyl(s, "rust", t, new Vector3(x, h, -d / 4), new Vector3(x, h + r.Range(1f, 3f), -d / 4), 0.1f, 8);
            }
            for (int i = 0; i < 4; i++) Box(s, "metalDark", t, new Vector3(-w / 2 - 0.01f, h * 0.4f + i * 0.12f, 0), new Vector3(0.02f, 0.06f, d * 0.6f));
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(w, h, d));
        }

        /// <summary>Glass specimen tank with glowing fluid and a floating silhouette.</summary>
        public static void SpecimenTank(Sink s, Vector3 pos, Rng r, bool broken)
        {
            var t = T(pos, r.Range(0, 360f));
            Cyl(s, "metalDark", t, Vector3.zero, Vector3.up * 0.4f, 0.75f, 16);
            Cyl(s, "metalDark", t, Vector3.up * 2.6f, Vector3.up * 2.9f, 0.75f, 16);
            if (!broken)
            {
                Cyl(s, "emitToxic", t, Vector3.up * 0.4f, Vector3.up * 2.6f, 0.62f, 16);
                P(s, "specimen", t).Sphere(0, Vector3.up * 1.55f, new Vector3(0.22f, 0.7f, 0.18f), 8, 6);
                P(s, "specimen", t).Sphere(0, Vector3.up * 2.35f, new Vector3(0.13f, 0.15f, 0.13f), 8, 5);
                s.Light(t.MultiplyPoint3x4(Vector3.up * 1.5f), new Color(0.45f, 1f, 0.35f), 6, 1f, flicker: 0.05f);
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72 + r.Range(-20f, 20f);
                    var shard = L(t, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.7f, 0.4f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.7f), new Vector3(r.Range(-10f, 10f), -a, 0));
                    Box(s, "glass", shard, new Vector3(0, r.Range(0.2f, 0.6f), 0), new Vector3(0.4f, r.Range(0.4f, 1.2f), 0.02f));
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120 * Mathf.Deg2Rad;
                Cyl(s, "metal", t, new Vector3(Mathf.Cos(a) * 0.7f, 0.4f, Mathf.Sin(a) * 0.7f), new Vector3(Mathf.Cos(a) * 0.7f, 2.6f, Mathf.Sin(a) * 0.7f), 0.04f, 6);
            }
            s.Box(pos + Vector3.up * 1.45f, new Vector3(1.4f, 2.9f, 1.4f), 0, broken ? SurfaceKind.Metal : SurfaceKind.Glass);
        }

        /// <summary>Reactor / chemical tank: big cylinder with rings, ladder and a glowing ring.</summary>
        public static void BigTank(Sink s, Vector3 pos, Rng r, float h, bool glow)
        {
            var t = T(pos, r.Range(0, 360f));
            Cyl(s, "machineGray", t, Vector3.zero, Vector3.up * h, 1.6f, 20);
            P(s, "machineGray", t).Sphere(0, Vector3.up * h, new Vector3(1.6f, 0.6f, 1.6f), 20, 6);
            for (float y = 0.8f; y < h; y += 1.6f) Cyl(s, "metalDark", t, Vector3.up * y, Vector3.up * (y + 0.12f), 1.65f, 20);
            if (glow)
            {
                Cyl(s, "emitToxic", t, Vector3.up * (h * 0.45f), Vector3.up * (h * 0.45f + 0.25f), 1.66f, 20);
                s.Light(t.MultiplyPoint3x4(new Vector3(0, h * 0.45f, 2.2f)), new Color(0.45f, 1f, 0.35f), 7, 2f);
            }
            for (float y = 0.3f; y < h - 0.3f; y += 0.35f) Box(s, "metal", t, new Vector3(0, y, 1.72f), new Vector3(0.45f, 0.03f, 0.04f));
            Box(s, "hazard", t, new Vector3(0, 0.25f, 0), new Vector3(3.3f, 0.5f, 0.1f));
            s.Box(pos + Vector3.up * h / 2, new Vector3(3.2f, h, 3.2f), 0, SurfaceKind.Metal);
        }

        public static void Pipes(Sink s, Vector3 a, Vector3 b, float y, float r0, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var off = Vector3.up * (y - i * r0 * 2.6f);
                var t = Matrix4x4.identity;
                P(s, i % 2 == 0 ? "rust" : "metal", Matrix4x4.Translate(Vector3.zero)).Cylinder(0, a + off, b + off, r0, r0, 8, false, false);
            }
        }

        // ------------------------------------------------------------------ outdoor

        public static void Container(Sink s, Vector3 pos, float yaw, Rng r, int color, bool longOne)
        {
            var t = T(pos, yaw);
            float len = longOne ? 12.2f : 6.06f, w = 2.44f, h = 2.6f;
            string m = "cont" + color;
            Box(s, m, t, new Vector3(0, h / 2, 0), new Vector3(w, h, len), 0.4f);
            Box(s, "metalDark", t, new Vector3(0, h - 0.06f, 0), new Vector3(w + 0.04f, 0.12f, len + 0.04f));
            Box(s, "metalDark", t, new Vector3(0, 0.06f, 0), new Vector3(w + 0.04f, 0.12f, len + 0.04f));
            foreach (var x in new[] { -0.4f, 0.4f }) Box(s, "metalDark", t, new Vector3(x, h / 2, len / 2 + 0.02f), new Vector3(0.04f, h - 0.2f, 0.04f));
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(w, h, len));
        }

        public static void Tent(Sink s, Vector3 pos, float yaw, Rng r, string fabric)
        {
            var t = T(pos, yaw);
            float w = 3f, len = r.Range(3.5f, 5f), h = 2.3f;
            var prof = new List<Vector2> { new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 2 - 0.3f, 1.2f), new Vector2(0, h), new Vector2(-w / 2 + 0.3f, 1.2f) };
            P(s, fabric, L(t, Vector3.zero, new Vector3(0, 90, 0))).Extrude(0, prof, len, 0.6f);
            Box(s, "trim", t, new Vector3(0, 0.75f, len / 2 + 0.01f), new Vector3(1f, 1.5f, 0.01f));
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(w, h, len), SurfaceKind.Wood);
        }

        public static void PineTree(Sink s, Vector3 pos, Rng r, bool snow, bool dead)
        {
            float h = r.Range(6f, 10f);
            var t = T(pos, r.Range(0, 360f));
            Cyl(s, "bark", t, Vector3.zero, Vector3.up * h, 0.22f, 8, 0.05f);
            if (dead)
            {
                for (int i = 0; i < 6; i++)
                {
                    float y = h * r.Range(0.35f, 0.85f);
                    float a = r.Range(0, 360f);
                    var bt = L(t, Vector3.up * y, new Vector3(r.Range(-60f, -20f), a, 0));
                    Cyl(s, "bark", bt, Vector3.zero, Vector3.forward * r.Range(1f, 2.4f), 0.06f, 5, 0.01f);
                }
            }
            else
            {
                int tiers = 5;
                for (int i = 0; i < tiers; i++)
                {
                    float k = i / (float)tiers;
                    float y = h * (0.25f + k * 0.65f);
                    float rad = Mathf.Lerp(2.2f, 0.5f, k);
                    var prof = new[] { new Vector2(rad, 0), new Vector2(rad * 0.7f, 0.25f), new Vector2(0.05f, 1.9f - k * 0.6f) };
                    var b = P(s, snow ? "pineSnow" : "pine", L(t, Vector3.up * y, new Vector3(0, i * 37, 0)));
                    b.Lathe(0, prof, 9, 0.5f);
                }
            }
            s.Box(pos + Vector3.up * 2, new Vector3(0.6f, 4, 0.6f), 0, SurfaceKind.Wood);
        }

        public static void Rock(Sink s, Vector3 pos, Rng r, float size)
        {
            var t = Matrix4x4.TRS(pos, Quaternion.Euler(r.Range(-10f, 10f), r.Range(0, 360f), r.Range(-10f, 10f)), new Vector3(size * r.Range(0.8f, 1.3f), size * r.Range(0.5f, 0.8f), size));
            P(s, "rock", t).Sphere(0, Vector3.zero, new Vector3(1, 1, 0.8f), 7, 5);
            s.Box(pos + Vector3.up * size * 0.25f, new Vector3(size * 1.6f, size * 0.7f, size * 1.4f), 0, SurfaceKind.Concrete);
        }

        /// <summary>Chain-link fence run with posts and barbed wire (cutout texture).</summary>
        public static void Fence(Sink s, Vector3 a, Vector3 b, float h, bool barbed)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var dir = d / len;
            var n = Vector3.Cross(Vector3.up, dir);
            var pb = s.Part("chainlink", Matrix4x4.identity, (a + b) / 2);
            pb.Plane(0, a, d, Vector3.up * h, Vector3.zero, new Vector2(len * 0.5f, h * 0.5f));
            pb.Plane(0, b, -d, Vector3.up * h, Vector3.zero, new Vector2(len * 0.5f, h * 0.5f));
            for (float x = 0; x <= len + 0.01f; x += 3f)
            {
                var p = a + dir * Mathf.Min(x, len);
                s.Part("metalDark", Matrix4x4.identity, p).Cylinder(0, p, p + Vector3.up * (h + 0.3f), 0.04f, 0.04f, 6);
            }
            if (barbed)
                for (int i = 0; i < 3; i++)
                {
                    var off = Vector3.up * (h + 0.1f + i * 0.12f) + n * (i - 1) * 0.08f;
                    s.Part("metalDark", Matrix4x4.identity, a).Cylinder(0, a + off, b + off, 0.008f, 0.008f, 4, false, false);
                }
            s.Box((a + b) / 2 + Vector3.up * h / 2, new Vector3(0.15f, h, len), Quaternion.LookRotation(dir), SurfaceKind.Metal);
        }

        public static void Watchtower(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            float h = 5f;
            foreach (var x in new[] { -1.2f, 1.2f })
                foreach (var z in new[] { -1.2f, 1.2f })
                    Cyl(s, "woodDark", t, new Vector3(x, 0, z), new Vector3(x * 0.85f, h, z * 0.85f), 0.1f, 6);
            Box(s, "wood", t, new Vector3(0, h, 0), new Vector3(2.8f, 0.12f, 2.8f));
            Sandbags(s, t.MultiplyPoint3x4(new Vector3(0, h + 0.06f, 1.3f)), yaw, r, 2.6f, 2);
            Box(s, "canvas", t, new Vector3(0, h + 2.2f, 0), new Vector3(3f, 0.08f, 3f));
            s.Light(t.MultiplyPoint3x4(new Vector3(0, h + 1.8f, 1.5f)), new Color(1f, 0.95f, 0.85f), 26, 12f, spot: true, angle: 60, dir: Quaternion.Euler(35, yaw + r.Range(-40f, 40f), 0) * Vector3.forward, beam: true);
            Col(s, t, new Vector3(0, h / 2, 0), new Vector3(2.6f, h, 2.6f), SurfaceKind.Wood);
        }

        public static void Crane(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            float h = 22f;
            foreach (var x in new[] { -4f, 4f })
                foreach (var z in new[] { -2f, 2f })
                    Box(s, "cranePaint", t, new Vector3(x, h / 2, z), new Vector3(0.6f, h, 0.6f));
            for (float y = 4; y < h; y += 4)
                foreach (var z in new[] { -2f, 2f }) Box(s, "cranePaint", t, new Vector3(0, y, z), new Vector3(8f, 0.3f, 0.3f));
            Box(s, "cranePaint", t, new Vector3(0, h + 0.8f, 6f), new Vector3(1.5f, 1.4f, 30f));
            Box(s, "cranePaint", t, new Vector3(0, h - 1.2f, 0), new Vector3(3f, 2.4f, 3f));
            s.Light(t.MultiplyPoint3x4(new Vector3(0, h + 1.6f, 20f)), new Color(1f, 0.2f, 0.1f), 4, 3f, flicker: 0f);
            foreach (var x in new[] { -4f, 4f })
                foreach (var z in new[] { -2f, 2f })
                    Col(s, t, new Vector3(x, h / 2, z), new Vector3(0.7f, h, 0.7f));
        }

        public static void Campfire(Sink s, Vector3 pos, Rng r)
        {
            var t = T(pos, r.Range(0, 360f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45 * Mathf.Deg2Rad;
                P(s, "rock", t).Sphere(0, new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, Mathf.Sin(a) * 0.55f), new Vector3(0.14f, 0.1f, 0.12f), 5, 3);
            }
            for (int i = 0; i < 4; i++)
            {
                var lt = L(t, new Vector3(0, 0.15f, 0), new Vector3(70, i * 90, 0));
                Cyl(s, "burnt", lt, Vector3.zero, Vector3.up * 0.6f, 0.06f, 5);
            }
            P(s, "emitFire", t).Sphere(0, new Vector3(0, 0.12f, 0), new Vector3(0.35f, 0.08f, 0.35f), 8, 4);
            s.Light(pos + Vector3.up * 0.8f, new Color(1f, 0.5f, 0.15f), 9, 1.5f, flicker: 0.7f);
        }

        // ------------------------------------------------------------------ litter

        /// <summary>Scatters paper, cans, bottles and debris in a cell (floor-level quads, no colliders).</summary>
        public static void Litter(Sink s, Vector3 center, float half, Rng r, int papers, int bits)
        {
            var b = s.Part("litter", Matrix4x4.identity, center);
            for (int i = 0; i < papers; i++)
            {
                var p = center + new Vector3(r.Range(-half, half), 0.015f + i * 0.0004f, r.Range(-half, half));
                float size = r.Range(0.18f, 0.42f);
                float a = r.Range(0, Mathf.PI * 2);
                var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
                var v = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * size * r.Range(0.7f, 1.3f);
                int cell = r.Range(0, 16);
                float cu = (cell % 4) * 0.25f, cv = (cell / 4) * 0.25f;
                // Upward-facing quad: cross(v, u) points up for this ordering.
                b.Plane(0, p - u * 0.5f - v * 0.5f, v, u, new Vector2(cu, cv), new Vector2(cu + 0.25f, cv + 0.25f));
            }
            for (int i = 0; i < bits; i++)
            {
                var p = center + new Vector3(r.Range(-half, half), 0, r.Range(-half, half));
                int k = r.Range(0, 4);
                var t = Matrix4x4.TRS(p + Vector3.up * 0.035f, Quaternion.Euler(90, r.Range(0, 360f), 0), Vector3.one);
                if (k == 0) s.Part("bottleGreen", t, p).Cylinder(0, Vector3.down * 0.12f, Vector3.up * 0.1f, 0.035f, 0.035f, 7);
                else if (k == 1) s.Part("bottleBrown", t, p).Cylinder(0, Vector3.down * 0.11f, Vector3.up * 0.09f, 0.03f, 0.03f, 7);
                else if (k == 2) s.Part("can", t, p).Cylinder(0, Vector3.down * 0.06f, Vector3.up * 0.06f, 0.033f, 0.033f, 7);
                else s.Part("concrete", Matrix4x4.TRS(p, Quaternion.Euler(r.Range(0, 360f), r.Range(0, 360f), 0), Vector3.one), p).Box(0, Vector3.up * 0.04f, new Vector3(r.Range(0.06f, 0.16f), r.Range(0.04f, 0.08f), r.Range(0.06f, 0.14f)));
            }
        }

        public static void Puddle(Sink s, Vector3 c, Rng r, float size)
        {
            var b = s.Part("puddle", Matrix4x4.identity, c);
            float a = r.Range(0, Mathf.PI * 2);
            var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
            var v = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * size * r.Range(0.6f, 1f);
            b.Plane(0, c + Vector3.up * 0.01f - u * 0.5f - v * 0.5f, v, u, Vector2.zero, Vector2.one);
        }

        public static void BloodPool(Sink s, Vector3 c, Rng r, float size)
        {
            var b = s.Part("blood", Matrix4x4.identity, c);
            float a = r.Range(0, Mathf.PI * 2);
            var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
            var v = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * size;
            int cell = r.Range(0, 4);
            float cu = (cell % 2) * 0.5f, cv = (cell / 2) * 0.5f;
            b.Plane(0, c + Vector3.up * 0.012f - u * 0.5f - v * 0.5f, v, u, new Vector2(cu, cv), new Vector2(cu + 0.5f, cv + 0.5f));
        }

        public static void BodyBag(Sink s, Vector3 pos, float yaw, Rng r)
        {
            var t = T(pos, yaw);
            RBox(s, "trashbag", t, new Vector3(0, 0.14f, 0), new Vector3(0.6f, 0.26f, 1.85f), 0.12f);
            P(s, "trashbag", t).Sphere(0, new Vector3(0, 0.2f, 0.75f), new Vector3(0.22f, 0.15f, 0.2f), 8, 5);
            BloodPool(s, pos + new Vector3(r.Range(-0.4f, 0.4f), 0, r.Range(-0.4f, 0.4f)), r, r.Range(0.8f, 1.4f));
        }
    }
}
