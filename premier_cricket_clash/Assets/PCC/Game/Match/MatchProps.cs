using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PCC.Game
{
    /// <summary>Ball, stumps, pitch map, aim target and pitch marker.</summary>
    public sealed class MatchProps : MonoBehaviour
    {
        Transform _ball, _ballGlow, _ballRim, _ballShadow;
        TrailRenderer _trail;
        readonly Transform[][] _stumps = new Transform[2][];
        readonly Transform[][] _bails = new Transform[2][];
        Transform _aim, _marker, _markerPin;
        Material _aimMat, _markerMat;
        readonly List<Transform> _zones = new List<Transform>();
        readonly List<Material> _zoneMats = new List<Material>();
        int _litZone = -2;
        double _hitFar = -1, _hitNear = -1;
        Material _stumpBand;

        public static readonly (double From, double To, string Label, Color Color)[] Zones =
        {
            (0.0, 2.0, "YORKER", new Color32(0xFF, 0x4D, 0x5E, 255)),
            (2.0, 4.5, "FULL", new Color32(0xFF, 0xB0, 0x20, 255)),
            (4.5, 7.2, "GOOD", new Color32(0x2E, 0xE5, 0x9D, 255)),
            (7.2, 9.6, "SHORT", new Color32(0x38, 0xE8, 0xFF, 255)),
            (9.6, 12.0, "BOUNCER", new Color32(0xB5, 0x7B, 0xFF, 255)),
        };

        public static MatchProps Build(Transform parent, Color stumpColor)
        {
            var go = new GameObject("Props");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<MatchProps>();
            p.Construct(stumpColor);
            return p;
        }

        /// <summary>
        /// Marker sprite (tinted by the material colour): dark outline, bright ring,
        /// translucent fill and a solid centre dot.
        /// </summary>
        static Texture2D MarkerTex()
        {
            var px = new Px(128, 128, new Color32(255, 255, 255, 0));
            for (int j = 0; j < 128; j++)
            for (int i = 0; i < 128; i++)
            {
                float dx = (i + 0.5f) / 64f - 1, dy = (j + 0.5f) / 64f - 1, d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c;
                if (d > 1) c = new Color(1, 1, 1, 0);
                else if (d > 0.93f) c = new Color(0.08f, 0.08f, 0.1f, 0.85f);
                else if (d > 0.72f) c = new Color(1, 1, 1, 1);
                else if (d > 0.26f) c = new Color(1, 1, 1, 0.5f);
                else if (d > 0.2f) c = new Color(0.08f, 0.08f, 0.1f, 0.85f);
                else c = new Color(1, 1, 1, 1);
                px.P[j * 128 + i] = c;
            }
            return px.ToTexture("Marker", true, TextureWrapMode.Clamp, FilterMode.Bilinear, 1);
        }

        void Construct(Color stumpColor)
        {
            // Ball (oversized so it reads on a phone screen). It glows a little so it stays white
            // in shadow, and a dark halo outlines it against the light pitch and the sightscreen.
            _ball = MeshKit.Obj("Ball", transform, MeshKit.Sphere,
                Mats.Lit(Color.white, 0.75f, Tex.Ball(true), false, 0, new Color(0.55f, 0.55f, 0.55f)), true, false).transform;
            _ball.localScale = Vector3.one * 0.06f;
            var glowTex = Tex.Glow();
            _ballRim = MeshKit.Obj("BallRim", transform, MeshKit.Quad, Mats.Unlit(new Color(0.02f, 0.03f, 0.08f, 0.75f), glowTex, true), false, false).transform;
            _ballRim.localScale = Vector3.one * 0.26f;
            _ballRim.gameObject.AddComponent<Billboard>();
            _ballGlow = MeshKit.Obj("BallGlow", transform, MeshKit.Quad, Mats.Unlit(new Color(1, 1, 1, 0.22f), glowTex, true, true), false, false).transform;
            _ballGlow.localScale = Vector3.one * 0.32f;
            _ballGlow.gameObject.AddComponent<Billboard>();
            _ballShadow = MeshKit.Obj("BallShadow", transform, MeshKit.Quad, Mats.Unlit(new Color(0, 0, 0, 0.45f), glowTex, true), false, false).transform;
            _ballShadow.localRotation = Quaternion.Euler(90, 0, 0);
            _trail = _ball.gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.14f;
            _trail.startWidth = 0.09f;
            _trail.endWidth = 0f;
            _trail.minVertexDistance = 0.05f;
            _trail.sharedMaterial = Mats.Unlit(new Color(1, 1, 1, 0.5f), null, true, true);
            _trail.shadowCastingMode = ShadowCastingMode.Off;
            SetBall(null);

            // Stumps and bails at both ends.
            var wood = Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.6f);
            _stumpBand = Mats.Lit(stumpColor, 0.6f);
            var stumpMesh = MeshKit.TaperedLimb(0.019f, 0.017f, 10);
            var bailMesh = MeshKit.TaperedLimb(0.009f, 0.009f, 6);
            for (int end = 0; end < 2; end++)
            {
                _stumps[end] = new Transform[3];
                _bails[end] = new Transform[2];
                for (int i = 0; i < 3; i++)
                {
                    var s = MeshKit.Obj("Stump", transform, stumpMesh, wood, true, true).transform;
                    var band = MeshKit.Obj("Band", s, MeshKit.TaperedLimb(0.0195f, 0.0195f, 10), _stumpBand, false, true).transform;
                    band.localPosition = new Vector3(0, 0.55f, 0);
                    band.localScale = new Vector3(1, 0.2f, 1);
                    _stumps[end][i] = s;
                }
                for (int i = 0; i < 2; i++) _bails[end][i] = MeshKit.Obj("Bail", transform, bailMesh, wood, true, true).transform;
            }
            ResetStumps();

            // Pitch map zones (shown while the user aims a delivery).
            foreach (var z in Zones)
            {
                double y0 = Pitch.Length - z.To, y1 = Pitch.Length - z.From;
                var b = new MeshBuilder();
                b.Quad(new V3(-1.45, y0, 0.02).U, new V3(1.45, y0, 0.02).U, new V3(1.45, y1, 0.02).U, new V3(-1.45, y1, 0.02).U, Vector3.up,
                    new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
                var zoneMat = Mats.UnlitInstance(z.Color.WithA(0.26f), null, true);
                _zoneMats.Add(zoneMat);
                var q = MeshKit.Obj("Zone " + z.Label, transform, b.Build("Zone"), zoneMat, false, false);
                var edge = new MeshBuilder();
                edge.Quad(new V3(-1.45, y0, 0.021).U, new V3(1.45, y0, 0.021).U, new V3(1.45, y0 + 0.04, 0.021).U, new V3(-1.45, y0 + 0.04, 0.021).U,
                    Vector3.up, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
                MeshKit.Obj("Edge", q.transform, edge.Build("Edge"), Mats.Unlit(z.Color.WithA(0.8f), null, true), false, false);
                _zones.Add(q.transform);
            }
            ShowZones(false);

            // Bowler's target: white ring with a dark outline, readable on every length zone.
            var markerTex = MarkerTex();
            _aimMat = Mats.UnlitInstance(Color.white, markerTex, true);
            _aim = MeshKit.Obj("Aim", transform, MeshKit.Quad, _aimMat, false, false).transform;
            _aim.localRotation = Quaternion.Euler(90, 0, 0);
            // Pitch marker: a solid disc on the pitch plus a floating dot that faces the camera,
            // so the landing spot reads even from the low bowling view.
            _markerMat = Mats.UnlitInstance(Pal.Cyan, markerTex, true);
            _marker = MeshKit.Obj("Marker", transform, MeshKit.Quad, _markerMat, false, false).transform;
            _marker.localRotation = Quaternion.Euler(90, 0, 0);
            _markerPin = MeshKit.Obj("MarkerPin", transform, MeshKit.Quad, _markerMat, false, false).transform;
            _markerPin.gameObject.AddComponent<Billboard>();
            ShowAim(null, 0);
            ShowMarker(null, 0);
        }

        public void SetBall(V3? p)
        {
            bool on = p != null;
            _ball.gameObject.SetActive(on);
            _ballGlow.gameObject.SetActive(on);
            _ballRim.gameObject.SetActive(on);
            _ballShadow.gameObject.SetActive(on);
            if (!on)
            {
                _trail.Clear();
                return;
            }
            var b = p.Value;
            _ball.localPosition = b.U;
            _ball.Rotate(Vector3.right, 720 * Time.deltaTime, Space.Self);
            _ballGlow.localPosition = b.U;
            _ballRim.localPosition = b.U;
            _ballShadow.localPosition = new V3(b.x, b.y, 0.025).U;
            float s = (float)(0.22 * (1 - Math.Min(1.0, b.z / 8)) + 0.06);
            _ballShadow.localScale = new Vector3(s, s, 1);
        }

        public void ClearTrail() => _trail.Clear();

        public void ShowZones(bool on)
        {
            foreach (var z in _zones) z.gameObject.SetActive(on);
        }

        /// <summary>Bowler's target ring (white unless a colour is given).</summary>
        public void ShowAim(V3? at, float time, Color? color = null)
        {
            _aim.gameObject.SetActive(at != null);
            if (at == null) return;
            float k = 0.75f * (1 + 0.1f * Mathf.Sin(time * 6));
            _aim.localPosition = new V3(at.Value.x, at.Value.y, 0.03).U;
            _aim.localScale = new Vector3(k, k, 1);
            Mats.SetColor(_aimMat, color ?? Color.white);
        }

        /// <summary>Brightens the selected length zone and dims the others (-1 = all normal).</summary>
        public void HighlightZone(int index)
        {
            if (index == _litZone) return;
            _litZone = index;
            for (int i = 0; i < _zoneMats.Count; i++)
            {
                float a = index < 0 ? 0.26f : (i == index ? 0.55f : 0.14f);
                Mats.SetColor(_zoneMats[i], Zones[i].Color.WithA(a));
            }
        }

        /// <summary>Index into <see cref="Zones"/> for a pitching spot this many metres in front of the striker's stumps.</summary>
        public static int ZoneIndex(double fromStumps)
        {
            for (int i = 0; i < Zones.Length; i++)
            {
                if (fromStumps < Zones[i].To) return i;
            }
            return Zones.Length - 1;
        }

        /// <summary>Length zone (label and colour) for a pitching spot this many metres in front of the striker's stumps.</summary>
        public static (string Label, Color Color) ZoneAt(double fromStumps)
        {
            var z = Zones[ZoneIndex(fromStumps)];
            return (z.Label, z.Color);
        }

        /// <summary>Pulsing pitch marker where the ball lands, tinted by length zone.</summary>
        public void ShowMarker(V3? at, float alpha, Color? color = null)
        {
            bool on = at != null && alpha > 0.01f;
            _marker.gameObject.SetActive(on);
            _markerPin.gameObject.SetActive(on);
            if (!on) return;
            float pulse = 1 + 0.08f * Mathf.Sin(Time.time * 8);
            _marker.localPosition = new V3(at.Value.x, at.Value.y, 0.028).U;
            _marker.localScale = new Vector3(1.3f * pulse, 1.3f * pulse, 1);
            _markerPin.localPosition = new V3(at.Value.x, at.Value.y, 0.32).U;
            _markerPin.localScale = new Vector3(0.42f * pulse, 0.42f * pulse, 1);
            Mats.SetColor(_markerMat, (color ?? Pal.Cyan).WithA(alpha));
        }

        public void HitStumps(bool far)
        {
            if (far) _hitFar = 0;
            else _hitNear = 0;
        }

        public void ResetStumps()
        {
            _hitFar = -1;
            _hitNear = -1;
            PlaceStumps(0, Pitch.Length, -1);
            PlaceStumps(1, 0, -1);
        }

        void PlaceStumps(int end, double y, double hit)
        {
            double[] xs = { -0.114, 0.0, 0.114 };
            double away = y > 10 ? 0.62 : -0.62;
            for (int i = 0; i < 3; i++)
            {
                var basePt = new V3(xs[i], y, 0);
                var top = new V3(xs[i], y, 0.71);
                if (hit >= 0)
                {
                    double t = Math.Min(1.0, hit / 0.35), e = 1 - (1 - t) * (1 - t);
                    double side = (i - 1) * 0.5 + 0.15;
                    top = new V3(xs[i] + Math.Sin(side) * 0.55 * e, y + away * e, 0.71 - 0.42 * e);
                }
                var d = (top - basePt).U;
                var tr = _stumps[end][i];
                tr.localPosition = basePt.U;
                tr.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                tr.localScale = new Vector3(1, d.magnitude, 1);
            }
            for (int b = 0; b < 2; b++)
            {
                var a = new V3(xs[b], y, 0.725);
                var z = new V3(xs[b + 1], y, 0.725);
                if (hit >= 0)
                {
                    double t = Math.Min(hit, 1.0);
                    double up = 3.2 * t - 4.9 * t * t;
                    double dx = (b == 0 ? -1 : 1) * 0.9 * t;
                    double dy = (y > 10 ? 1.1 : -1.1) * t;
                    a = new V3(a.x + dx, y + dy, Math.Max(0.01, 0.725 + up));
                    z = new V3(z.x + dx + 0.1 * Math.Sin(t * 9), y + dy + 0.08 * Math.Cos(t * 9), Math.Max(0.01, 0.725 + up + 0.06));
                }
                var d = (z - a).U;
                var tr = _bails[end][b];
                tr.localPosition = a.U;
                tr.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                tr.localScale = new Vector3(1, d.magnitude, 1);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_hitFar >= 0)
            {
                _hitFar += dt;
                PlaceStumps(0, Pitch.Length, _hitFar);
            }
            if (_hitNear >= 0)
            {
                _hitNear += dt;
                PlaceStumps(1, 0, _hitNear);
            }
        }
    }
}
