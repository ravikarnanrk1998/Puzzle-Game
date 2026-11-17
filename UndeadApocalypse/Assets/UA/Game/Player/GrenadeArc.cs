using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UA.Game
{
    /// <summary>
    /// Aiming guide while the grenade button is held: a red line along the grenade's predicted path (same launch
    /// velocity, gravity, bounces and fuse as <see cref="Projectile.Grenade"/>) and red rings where it will explode:
    /// a small target ring and the blast radius. Visual only.
    /// </summary>
    public sealed class GrenadeArc : MonoBehaviour
    {
        static readonly Color Red = new Color(1f, 0.13f, 0.08f, 0.92f);

        /// <summary>Explosion point of the last arc shown (the autopilot compares it with the real explosion).</summary>
        public static Vector3 LastPredicted;

        LineRenderer _line;
        Transform _marker;
        readonly List<Vector3> _pts = new List<Vector3>(160);
        Vector3[] _buf = new Vector3[160];

        public static GrenadeArc Create(Transform parent)
        {
            var go = new GameObject("GrenadeArc");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<GrenadeArc>();
            a.Build();
            a.Hide();
            return a;
        }

        void Build()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 3;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;
            // The colour lives in the material: the URP unlit shader ignores line vertex colours (white otherwise).
            _line.material = Mats.Cached("grenadeArcLineRed", () => Mats.UnlitTransparent(Red));
            _line.startColor = _line.endColor = Color.white;
            // Thin near the hand, wider far away, so it reads at a similar thickness along its length.
            _line.widthCurve = new AnimationCurve(new Keyframe(0, 0.02f), new Keyframe(0.35f, 0.065f), new Keyframe(1, 0.11f));

            _marker = new GameObject("Target").transform;
            _marker.SetParent(transform, false);
            // Blast area (soft fill + ring) and the impact point. Heights are in marker units (x blast radius, ~5.5 m):
            // a few centimetres above the ground so they never sink into it.
            Disc(_marker, "BlastFill", Sprites.Glow.texture, new Color(1f, 0.1f, 0.06f, 0.22f), 1f, 0.004f);
            Disc(_marker, "BlastRing", Sprites.Ring(4).texture, new Color(1f, 0.15f, 0.1f, 0.6f), 1f, 0.006f);
            Disc(_marker, "Impact", Sprites.Ring(16).texture, Red, 0.09f, 0.008f);
            Disc(_marker, "ImpactDot", Sprites.Circle.texture, Red, 0.025f, 0.01f);
        }

        /// <summary>Flat disc of radius <paramref name="scale"/> x the blast radius (the marker is scaled to the radius).</summary>
        static void Disc(Transform parent, string name, Texture tex, Color c, float scale, float lift)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, lift, 0);
            go.transform.localScale = Vector3.one * scale;
            var mesh = new Mesh { name = "disc" };
            mesh.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = Mats.Cached("grenadeArc_" + name, () => Mats.UnlitTransparent(c, tex));
        }

        public void Hide()
        {
            if (_line != null) _line.enabled = false;
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        /// <summary>Predicts and draws the path of a grenade thrown from <paramref name="from"/> with <paramref name="vel"/>.</summary>
        public void Show(Vector3 from, Vector3 vel, float fuse, float radius, Vector3 eye)
        {
            float dt = Mathf.Clamp(Time.smoothDeltaTime, 1f / 60f, 1f / 20f);
            var end = Simulate(from, vel, fuse, dt, _pts);
            LastPredicted = end;
            // Skip the first few centimetres in front of the camera (a thick blob at the bottom of the screen).
            int first = 0;
            while (first < _pts.Count - 2 && (_pts[first] - eye).sqrMagnitude < 1.1f * 1.1f) first++;
            int n = _pts.Count - first;
            if (_buf.Length < n) _buf = new Vector3[n * 2];
            for (int i = 0; i < n; i++) _buf[i] = _pts[first + i];
            _line.positionCount = n;
            _line.SetPositions(_buf);
            _line.enabled = n >= 2;
            // Rings on the ground under the explosion point, flat against it.
            var up = Vector3.up;
            var at = end;
            if (Physics.Raycast(end + Vector3.up * 0.3f, Vector3.down, out var hit, 40f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                at = hit.point;
                up = hit.normal;
            }
            _marker.gameObject.SetActive(true);
            _marker.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, up));
            _marker.localScale = Vector3.one * radius;
        }

        /// <summary>
        /// The grenade's flight, step for step like Projectile.Update: gravity, sphere-cast bounces that keep 45 % of
        /// the speed, and the fuse. Fills <paramref name="pts"/> and returns where it explodes.
        /// </summary>
        public static Vector3 Simulate(Vector3 p, Vector3 vel, float fuse, float dt, List<Vector3> pts)
        {
            pts.Clear();
            pts.Add(p);
            for (int guard = 0; fuse > 0 && guard < 400; guard++)
            {
                vel.y -= 9.8f * dt;
                var step = vel * dt;
                float len = step.magnitude;
                if (len > 1e-5f && Physics.SphereCast(p, 0.06f, step / len, out var gh, len + 0.01f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    vel = Vector3.Reflect(vel, gh.normal) * 0.45f;
                    p = gh.point + gh.normal * 0.07f;
                }
                else p += step;
                pts.Add(p);
                fuse -= dt;
            }
            return p;
        }
    }
}
