using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>
    /// Type gear for imported bodies: explosive charges, the butcher's cleaver, claws, bone spikes and glowing
    /// pustules. The procedural rig's gear is skinned to its own proportions, so on an imported body it would
    /// float; these rigid pieces sit on the imported body's own bones instead (through <see cref="ArtBody.Socket"/>,
    /// aligned with the game skeleton) and reuse the rig's gear materials. Visual only: hitboxes stay on the
    /// game skeleton. Offsets are relative to the game bone's bind position, in the body's rest frame.
    /// </summary>
    public static class ArtGear
    {
        // Submeshes of every piece: gear metal / bone, dark straps and handles, glow.
        const int SMetal = 0, SDark = 1, SGlow = 2;
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static void Attach(ArtBody body, ZombieRig rig)
        {
            var s = rig.Shape;
            if (s == null || rig.Materials == null || rig.Materials.Length < 9) return;
            // gear2 (type metal / bone colour), shoes (dark), eyes (the type's glow colour).
            var mats = new[] { rig.Materials[8], rig.Materials[4], rig.Materials[6] };
            if (s.Charges) Add(body, rig, ZB.Chest, "charges", Charges, mats);
            if (s.Cleaver) Add(body, rig, ZB.HandR, "cleaver", Cleaver, mats);
            if (s.Claws)
            {
                Add(body, rig, ZB.HandL, "clawsL", mb => Claws(mb, -1), mats);
                Add(body, rig, ZB.HandR, "clawsR", mb => Claws(mb, 1), mats);
            }
            if (s.Spikes)
            {
                // Bone breaking through the skin: yellowed and blood-stained, not the procedural rig's clean bone.
                var bone = Mats.Cached("artBoneSpike", () => Mats.Lit(new Color(0.42f, 0.35f, 0.27f), 0.35f));
                var boneMats = new[] { bone, mats[1], mats[2] };
                Add(body, rig, ZB.Chest, "spikesBack", BackSpikes, boneMats);
                Add(body, rig, ZB.ClavL, "spikesL", mb => ShoulderSpikes(mb, -1), boneMats);
                Add(body, rig, ZB.ClavR, "spikesR", mb => ShoulderSpikes(mb, 1), boneMats);
            }
            if (s.Pustules || s.Sacs) Add(body, rig, ZB.Chest, s.Sacs ? "sacs" : "pustules", mb => Pustules(mb, s.Sacs), mats);
        }

        static void Add(ArtBody body, ZombieRig rig, ZB bone, string piece, Action<MeshBuilder> build, Material[] mats)
        {
            var socket = body.Socket(bone);
            if (socket == null) return;
            if (!Cache.TryGetValue(piece, out var mesh) || mesh == null)
            {
                var mb = new MeshBuilder();
                build(mb);
                mesh = mb.ToMesh("artGear_" + piece, false);
                Cache[piece] = mesh;
            }
            var go = new GameObject("Gear_" + piece);
            go.layer = rig.gameObject.layer;
            go.transform.SetParent(socket, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var use = new Material[mesh.subMeshCount];
            for (int i = 0; i < use.Length; i++) use[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = use;
            mr.shadowCastingMode = Quality.Current == Gfx.Low ? ShadowCastingMode.Off : ShadowCastingMode.On;
            body.AddExtra(mr, SGlow);
        }

        // Offsets below: game Chest bind (0, 1.3, 0), hands at the wrist, clavicles (+-0.03, 1.44, 0).
        // Imported chests sit about 2 cm behind the game's, so depths are pushed out slightly.

        static void Charges(MeshBuilder mb)
        {
            const float cz = 0.175f;
            for (int k = 0; k < 4; k++)
            {
                float cx = -0.12f + k * 0.08f;
                mb.Cylinder(SMetal, new Vector3(cx, -0.18f, cz), new Vector3(cx, 0.03f, cz), 0.028f, 0.028f, 8);
            }
            mb.Box(SDark, new Vector3(0, 0.01f, cz + 0.012f), new Vector3(0.32f, 0.025f, 0.05f));
            mb.Box(SDark, new Vector3(0, -0.15f, cz + 0.012f), new Vector3(0.32f, 0.025f, 0.05f));
            mb.RoundBox(SDark, new Vector3(0.07f, 0.11f, cz + 0.015f), new Vector3(0.08f, 0.055f, 0.04f), 0.01f, 1);
            mb.Sphere(SGlow, new Vector3(0.07f, 0.12f, cz + 0.04f), Vector3.one * 0.013f, 6, 4);
        }

        static void Cleaver(MeshBuilder mb)
        {
            mb.Cylinder(SDark, new Vector3(0, -0.08f, 0.02f), new Vector3(0, -0.2f, 0.02f), 0.018f, 0.018f, 6);
            mb.Box(SMetal, new Vector3(0, -0.42f, 0.09f), new Vector3(0.012f, 0.42f, 0.2f));
        }

        static void Claws(MeshBuilder mb, int side)
        {
            for (int f = 0; f < 3; f++)
            {
                float dx = (f - 1) * 0.022f;
                mb.Cylinder(SMetal, new Vector3(dx, -0.14f, 0.01f), new Vector3(dx + side * 0.01f, -0.36f, 0.06f), 0.008f, 0.001f, 5, false, false);
            }
        }

        // Spikes are shorter than the procedural rig's: on a realistic body long cones read as horns or wings.
        static void BackSpikes(MeshBuilder mb)
        {
            for (int k = 0; k < 5; k++)
            {
                var b = new Vector3(0, -0.08f + k * 0.06f, -0.11f);
                mb.Cylinder(SMetal, b, b + new Vector3(0, 0.03f, -0.07f - k * 0.006f), 0.02f, 0.002f, 6, false, false);
            }
        }

        static void ShoulderSpikes(MeshBuilder mb, int side)
        {
            for (int k = 0; k < 3; k++)
            {
                var b = new Vector3(side * 0.17f, 0.05f, -0.04f + k * 0.03f);
                var t = new Vector3(side * (0.2f + k * 0.012f), 0.13f + k * 0.015f, -0.06f + k * 0.01f);
                mb.Cylinder(SMetal, b, t, 0.016f, 0.002f, 6, false, false);
            }
        }

        static void Pustules(MeshBuilder mb, bool sacs)
        {
            var r = new System.Random(sacs ? 17 : 11);
            int n = sacs ? 8 : 7;
            for (int k = 0; k < n; k++)
            {
                float a = -0.5f + (float)r.NextDouble() * (sacs ? 3.4f : 2.2f) + (sacs ? 1.2f : 0f);
                float y = -0.28f + (float)r.NextDouble() * 0.32f;
                var c = new Vector3(Mathf.Sin(a) * 0.17f, y, Mathf.Cos(a) * 0.15f + 0.015f);
                float sz = 0.025f + (float)r.NextDouble() * (sacs ? 0.05f : 0.025f);
                mb.Sphere(SGlow, c, Vector3.one * sz, 7, 5);
            }
        }
    }
}
