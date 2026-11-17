using System;
using System.Collections;
using System.Collections.Generic;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// Procedural cricket stadium: outfield, pitch, stumps, rope, LED boards, two-tier stands with
    /// an animated crowd, roofs, sight screens, giant screens, floodlight towers and a city skyline.
    /// Each franchise ground differs in size, stand style, seat colours and number of towers.
    /// </summary>
    public sealed class Stadium : MonoBehaviour
    {
        public TeamDef Venue, Home, Away;
        public bool Night;
        public Ground Ground;
        public Transform StrikerStumps, BowlerStumps;
        public readonly List<Transform> StumpPieces = new List<Transform>();
        readonly List<Material> _crowdMats = new List<Material>();
        Texture2D _crowdA, _crowdB;
        float _cheerUntil, _cheerTimer;
        Material _ledMat;
        float _screenFlashUntil;
        Material _stumpMatA, _stumpMatB;
        float _stumpFlashUntil;
        int _stumpFlashEnd = -1;
        readonly List<Transform> _flags = new List<Transform>();
        readonly List<Transform> _glows = new List<Transform>();
        public readonly List<Light> FloodSpots = new List<Light>();

        public static IEnumerator Build(TeamDef venue, TeamDef home, TeamDef away, bool night, Transform parent, Action<Stadium> done, Action<float> progress = null)
        {
            var go = new GameObject("Stadium " + venue.Id);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Stadium>();
            s.Venue = venue;
            s.Home = home;
            s.Away = away;
            s.Night = night;
            s.Ground = new Ground(venue);
            var steps = new List<(string, Action)>
            {
                ("ground", s.BuildGround),
                ("pitch", s.BuildPitch),
                ("stumps", s.BuildStumps),
                ("rope", s.BuildRopeAndBoards),
                ("stands", s.BuildStands),
                ("screens", s.BuildScreensAndSight),
                ("lights", s.BuildFloodlights),
                ("city", s.BuildSurroundings),
            };
            for (int i = 0; i < steps.Count; i++)
            {
                try
                {
                    steps[i].Item2();
                }
                catch (Exception e)
                {
                    Debug.LogError("[KPL] Stadium step " + steps[i].Item1 + " failed: " + e);
                }
                progress?.Invoke((i + 1) / (float)steps.Count);
                yield return null;
            }
            done?.Invoke(s);
        }

        /// <summary>Synchronous build (menus).</summary>
        public static Stadium BuildNow(TeamDef venue, TeamDef home, TeamDef away, bool night, Transform parent)
        {
            Stadium result = null;
            var it = Build(venue, home, away, night, parent, s => result = s);
            while (it.MoveNext()) { }
            return result;
        }

        float Extent => (float)Math.Max(Ground.Square, Ground.Straight) + 14;

        // ------------------------------------------------------------ ground

        void BuildGround()
        {
            float ext = Extent;
            var b = new MeshBuilder();
            int segs = 96, rings = 24;
            float rs = (float)Ground.Square + 12, rz = (float)Ground.Straight + 12;
            int center = b.Add(new Vector3(0, 0, 0), Vector3.up, new Vector2(0.5f, 0.5f));
            for (int r = 1; r <= rings; r++)
            {
                float k = r / (float)rings;
                k = k * k * 0.3f + k * 0.7f;
                for (int i = 0; i <= segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2;
                    var p = new Vector3(Mathf.Sin(a) * rs * k, 0, Mathf.Cos(a) * rz * k);
                    b.Add(p, Vector3.up, new Vector2(p.x / (2 * ext) + 0.5f, p.z / (2 * ext) + 0.5f));
                }
            }
            for (int i = 0; i < segs; i++) b.Tri(0, center, 1 + i, 1 + i + 1);
            for (int r = 1; r < rings; r++)
            {
                int o0 = 1 + (r - 1) * (segs + 1), o1 = 1 + r * (segs + 1);
                for (int i = 0; i < segs; i++) b.Quad(0, o0 + i, o1 + i, o1 + i + 1, o0 + i + 1);
            }
            var tex = Tex.Outfield(ext, Ground.Square, Ground.Straight, Venue.Primary);
            var mat = Mats.LitDetail(tex, Tex.GrassDetail, Tex.GrassNormal, new Vector2(ext * 2 / 1.7f, ext * 2 / 1.7f), 0.12f);
            var go = MeshKit.Spawn("Outfield", b, transform, mat);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Wide apron and surrounding land beyond the stands so there is never a void.
            var land = new MeshBuilder();
            float r0 = Mathf.Max(rs, rz) - 2, r1 = 900;
            for (int i = 0; i < 64; i++)
            {
                float a0 = i / 64f * Mathf.PI * 2, a1 = (i + 1) / 64f * Mathf.PI * 2;
                var p0 = new Vector3(Mathf.Sin(a0) * r0, -0.05f, Mathf.Cos(a0) * r0);
                var p1 = new Vector3(Mathf.Sin(a1) * r0, -0.05f, Mathf.Cos(a1) * r0);
                var p2 = new Vector3(Mathf.Sin(a1) * r1, -0.05f, Mathf.Cos(a1) * r1);
                var p3 = new Vector3(Mathf.Sin(a0) * r1, -0.05f, Mathf.Cos(a0) * r1);
                int ia = land.Add(p0, Vector3.up, new Vector2(p0.x, p0.z) / 20);
                int ib = land.Add(p1, Vector3.up, new Vector2(p1.x, p1.z) / 20);
                int ic = land.Add(p2, Vector3.up, new Vector2(p2.x, p2.z) / 20);
                int id = land.Add(p3, Vector3.up, new Vector2(p3.x, p3.z) / 20);
                land.Quad(0, ia, id, ic, ib);
            }
            var landMat = Mats.Lit(Night ? new Color(0.12f, 0.14f, 0.12f) : new Color(0.36f, 0.4f, 0.33f), 0.05f, 0, Tex.Concrete);
            var lg = MeshKit.Spawn("Land", land, transform, landMat);
            MeshKit.CastOnly(lg, false, true);
        }

        void BuildPitch()
        {
            var b = new MeshBuilder();
            float w = 3.66f, l = 24f;
            b.Quad(0, new Vector3(-w / 2, 0.006f, -l / 2), new Vector3(w / 2, 0.006f, -l / 2), new Vector3(w / 2, 0.006f, l / 2), new Vector3(-w / 2, 0.006f, l / 2), Vector2.zero, Vector2.one);
            // Front face points up: quad corners go anticlockwise from above, so flip.
            var mesh = b.ToMesh("Pitch");
            FlipIfDown(mesh);
            var mat = Mats.LitNormal(Color.white, Tex.Pitch, Tex.PitchNormal, 0.22f, 0.9f);
            var go = MeshKit.Spawn("Pitch", mesh, transform, mat);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void FlipIfDown(Mesh m)
        {
            var n = m.normals;
            if (n.Length > 0 && n[0].y < 0)
            {
                var tris = m.triangles;
                for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                m.triangles = tris;
                for (int i = 0; i < n.Length; i++) n[i] = -n[i];
                m.normals = n;
            }
        }

        // ------------------------------------------------------------ stumps

        void BuildStumps()
        {
            var trim = Crest.Trim(Home);
            _stumpMatA = Mats.Emissive(new Color(0.95f, 0.93f, 0.88f), Color.black, null, 0.55f);
            _stumpMatB = Mats.Emissive(trim, Color.black, null, 0.5f);
            StrikerStumps = MakeStumps("StumpsStriker", (float)Pitch.StrikerStumpsZ);
            BowlerStumps = MakeStumps("StumpsBowler", (float)Pitch.StumpsZ);
        }

        Transform MakeStumps(string name, float z)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0, 0, z);
            for (int i = -1; i <= 1; i++)
            {
                var b = new MeshBuilder();
                b.Cylinder(0, Vector3.zero, new Vector3(0, 0.62f, 0), 0.018f, 0.018f, 10, false);
                b.Cylinder(1, new Vector3(0, 0.62f, 0), new Vector3(0, 0.711f, 0), 0.018f, 0.0175f, 10, true);
                var st = MeshKit.Spawn("Stump" + i, b, root, _stumpMatA, _stumpMatB);
                st.transform.localPosition = new Vector3(i * 0.096f, 0, 0);
                StumpPieces.Add(st.transform);
            }
            for (int i = 0; i < 2; i++)
            {
                var b = new MeshBuilder();
                b.Cylinder(0, new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), 0.008f, 0.008f, 8, true);
                var bail = MeshKit.Spawn("Bail" + i, b, root, _stumpMatB);
                bail.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * 0.048f, 0.722f, 0);
                StumpPieces.Add(bail.transform);
            }
            return root;
        }

        /// <summary>Knocks the stumps and bails flying and flashes the LED stumps.</summary>
        public void BreakStumps(bool strikerEnd, Vector3 impulseDir)
        {
            var root = strikerEnd ? StrikerStumps : BowlerStumps;
            _stumpFlashUntil = Time.time + 2.2f;
            _stumpFlashEnd = strikerEnd ? 0 : 1;
            foreach (Transform t in root)
            {
                var fly = t.gameObject.GetComponent<Flyer>() ?? t.gameObject.AddComponent<Flyer>();
                bool bail = t.name.StartsWith("Bail");
                fly.Launch(impulseDir * (bail ? UnityEngine.Random.Range(4f, 7f) : UnityEngine.Random.Range(0.8f, 2f)) + Vector3.up * (bail ? UnityEngine.Random.Range(2.5f, 4.5f) : 0.6f),
                    UnityEngine.Random.insideUnitSphere * (bail ? 900 : 160), bail);
            }
        }

        public void ResetStumps()
        {
            foreach (var t in new[] { StrikerStumps, BowlerStumps })
            {
                int i = 0;
                foreach (Transform c in t)
                {
                    var fly = c.GetComponent<Flyer>();
                    if (fly != null) Destroy(fly);
                    bool bail = c.name.StartsWith("Bail");
                    c.localPosition = bail ? new Vector3((c.name.EndsWith("0") ? -1 : 1) * 0.048f, 0.722f, 0) : new Vector3((i - 1) * 0.096f, 0, 0);
                    c.localRotation = Quaternion.identity;
                    if (!bail) i++;
                }
            }
            _stumpFlashUntil = 0;
            Mats.SetEmission(_stumpMatA, Color.black);
            Mats.SetEmission(_stumpMatB, Color.black);
        }

        // ------------------------------------------------------- rope + boards

        Vector3 Ell(float a, float extra, float y = 0)
        {
            float rs = (float)Ground.Square + extra, rz = (float)Ground.Straight + extra;
            return new Vector3(Mathf.Sin(a) * rs, y, Mathf.Cos(a) * rz);
        }

        void BuildRopeAndBoards()
        {
            // Rope: a padded tube in alternating colours.
            var rope = new MeshBuilder();
            int segs = Quality.Current == Gfx.Low ? 128 : 256;
            int sides = 8;
            float rr = 0.11f;
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2;
                var c = Ell(a, 0, rr);
                var tangent = (Ell(a + 0.001f, 0) - Ell(a - 0.001f, 0)).normalized;
                var outN = Vector3.Cross(Vector3.up, tangent).normalized * -1;
                for (int k = 0; k <= sides; k++)
                {
                    float b = k / (float)sides * Mathf.PI * 2;
                    var n = outN * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    rope.Add(c + n * rr, n, new Vector2(i / (float)segs * 60, k / (float)sides));
                }
            }
            for (int i = 0; i < segs; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a0 = i * (sides + 1) + k, a1 = (i + 1) * (sides + 1) + k;
                    rope.Quad(0, a0, a0 + 1, a1 + 1, a1);
                }
            }
            var ropeTex = StripeTex(Crest.Primary(Home), Color.white);
            var rg = MeshKit.Spawn("Rope", rope, transform, Mats.Lit(Color.white, 0.35f, 0, ropeTex));
            FixWinding(rg);
            rg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // LED boards: angled panels just beyond the rope, scrolling a strip of sponsor ads.
            var boards = new MeshBuilder();
            int panels = 72;
            float off = 3.2f, h = 0.95f;
            for (int i = 0; i < panels; i++)
            {
                float a0 = (i + 0.06f) / panels * Mathf.PI * 2, a1 = (i + 0.94f) / panels * Mathf.PI * 2;
                var p0 = Ell(a0, off);
                var p1 = Ell(a1, off);
                var back = (Ell(a0, off + 0.35f) - p0);
                var q0 = p0 + back + Vector3.up * h;
                var q1 = p1 + back + Vector3.up * h;
                float u0 = (i % 8) * 0.125f, u1 = u0 + 0.125f;
                var n = Vector3.Cross(p1 - p0, q0 - p0).normalized;
                if (Vector3.Dot(n, -p0) < 0) n = -n;
                int ia = boards.Add(p0, n, new Vector2(u0, 0));
                int ib = boards.Add(p1, n, new Vector2(u1, 0));
                int ic = boards.Add(q1, n, new Vector2(u1, 1));
                int id = boards.Add(q0, n, new Vector2(u0, 1));
                boards.Quad(0, ia, id, ic, ib);
                // Dark back and frame.
                var pb0 = p0 + back * 1.6f;
                var pb1 = p1 + back * 1.6f;
                int ja = boards.Add(pb0, -n, Vector2.zero);
                int jb = boards.Add(pb1, -n, Vector2.zero);
                int jc = boards.Add(q1 + back * 0.6f, -n, Vector2.zero);
                int jd = boards.Add(q0 + back * 0.6f, -n, Vector2.zero);
                boards.Quad(1, ja, jb, jc, jd);
            }
            var adTex = Brand.BoardStrip();
            _ledMat = Mats.Emissive(Color.white, Night ? new Color(1.6f, 1.6f, 1.6f) : new Color(1.05f, 1.05f, 1.05f), adTex, 0.6f);
            var bg = MeshKit.Spawn("LedBoards", boards, transform, _ledMat, Mats.Lit(new Color(0.08f, 0.08f, 0.1f), 0.3f));
            FixWinding(bg);

            // Wall and apron behind the boards.
            var wall = new MeshBuilder();
            int ws = 96;
            float wr = 10.5f, wh = 1.8f;
            for (int i = 0; i < ws; i++)
            {
                float a0 = i / (float)ws * Mathf.PI * 2, a1 = (i + 1) / (float)ws * Mathf.PI * 2;
                var p0 = Ell(a0, wr);
                var p1 = Ell(a1, wr);
                wall.Quad(0, p0, p1, p1 + Vector3.up * wh, p0 + Vector3.up * wh, new Vector2(0, 0), new Vector2(3, 0.4f));
                wall.Quad(1, p0 + Vector3.up * (wh - 0.3f), p1 + Vector3.up * (wh - 0.3f), p1 + Vector3.up * wh, p0 + Vector3.up * wh, Vector2.zero, Vector2.one);
            }
            var wg = MeshKit.Spawn("Wall", wall, transform, Mats.Lit(new Color(0.55f, 0.56f, 0.6f), 0.15f, 0, Tex.Concrete), Mats.Lit(Crest.Primary(Home), 0.4f));
            FixWinding(wg);
        }

        /// <summary>Makes every triangle face the pitch (the stadium is viewed from inside).</summary>
        static void FixWinding(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            var m = mf.sharedMesh;
            var v = m.vertices;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tris = m.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = v[tris[i]];
                    var b = v[tris[i + 1]];
                    var c = v[tris[i + 2]];
                    var n = Vector3.Cross(b - a, c - a);
                    var mid = (a + b + c) / 3;
                    var toCenter = new Vector3(-mid.x, 30 - mid.y, -mid.z);
                    if (Vector3.Dot(n, toCenter) < 0) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                }
                m.SetTriangles(tris, s);
            }
            m.RecalculateNormals();
        }

        static Texture2D StripeTex(Color a, Color b)
        {
            var t = new Texture2D(32, 8, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 8; y++) t.SetPixel(x, y, x < 16 ? a : b);
            }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------ stands

        Color SeatColor(int i)
        {
            var p = Crest.Primary(Home);
            var s = Crest.Trim(Home);
            switch (i % 3)
            {
                case 0: return p * 0.85f;
                case 1: return Color.Lerp(p, Color.white, 0.25f);
                default: return s * 0.8f;
            }
        }

        /// <summary>Angular half-width of the open ends (behind the sight screen and the giant screen).</summary>
        static bool IsEnd(float angle) => Mathf.Abs(Mathf.Cos(angle)) > 0.972f;

        float _lowOut;

        void BuildStands()
        {
            var home = Crest.Primary(Home);
            var homeSec = Crest.Trim(Home);
            var away = Crest.Primary(Away);
            int seed = Teams.IndexOf(Venue.Id) * 7 + 3;
            var seat = SeatColor(0);
            _crowdA = Tex.Crowd(home, homeSec, away, seat, 0, seed);
            _crowdB = Tex.Crowd(home, homeSec, away, seat, 1, seed);
            for (int g = 0; g < 4; g++) _crowdMats.Add(Mats.Lit(Color.white, 0.08f, 0, _crowdA));

            int style = Venue.StadiumStyle;
            int sections = 40;
            var crowd = new MeshBuilder[4];
            for (int g = 0; g < 4; g++) crowd[g] = new MeshBuilder();
            var concrete = new MeshBuilder();
            var aisles = new MeshBuilder();
            var darkSeats = new MeshBuilder();
            var facade = new MeshBuilder();
            var ribbon = new MeshBuilder();
            var roof = new MeshBuilder();
            var roofUnder = new MeshBuilder();
            var fascia = new MeshBuilder();
            float lowIn = 12f, lowOut = style == 0 ? 52f : 40f, lowH0 = 2.2f, lowH1 = style == 0 ? 26f : 15f;
            float upIn = lowOut + 5, upOut = upIn + 26, upH0 = lowH1 + 6.5f, upH1 = upH0 + 17;
            _lowOut = lowOut;
            for (int i = 0; i < sections; i++)
            {
                float a0 = i / (float)sections * Mathf.PI * 2, a1 = (i + 1) / (float)sections * Mathf.PI * 2;
                float mid = (a0 + a1) / 2;
                int g = (i * 7 + seed) % 4;
                float gap = 0.0042f;
                if (IsEnd(mid))
                {
                    // Behind the screens the seats are left empty and dark; no upper tier, so trees and sky show.
                    Slope(darkSeats, a0, a1, lowIn + 6, lowOut * 0.7f, lowH0 + 2, lowH1 * 0.6f, 0.5f);
                    Band(concrete, a0, a1, lowOut * 0.7f + 0.2f, 0, lowH1 * 0.6f + 1.2f, 0);
                    continue;
                }
                Slope(crowd[g], a0 + gap, a1 - gap, lowIn, lowOut, lowH0, lowH1);
                Slope(aisles, a1 - gap, a1 + gap, lowIn, lowOut, lowH0 + 0.05f, lowH1 + 0.05f, 0.4f);
                // Front wall of the lower tier and the back wall down to the ground.
                Band(facade, a0, a1, lowIn, lowH0 - 1.4f, lowH0 + 0.2f, 1);
                Band(concrete, a0, a1, lowOut + 0.2f, 0, lowH1 + 1.2f, 0);
                if (style != 0)
                {
                    // Concourse, hospitality glass, then an LED ribbon board along the upper tier's front.
                    Flat(concrete, a0, a1, lowOut, upIn, lowH1 + 0.6f);
                    Band(facade, a0, a1, upIn - 0.4f, lowH1 + 0.6f, upH0 - 2.4f, 1);
                    var p0 = Ell(a0, upIn - 0.6f, upH0 - 2.4f);
                    var p1 = Ell(a1, upIn - 0.6f, upH0 - 2.4f);
                    float u0 = (i % 8) * 0.125f;
                    ribbon.Quad(0, p0, p1, p1 + Vector3.up * 2.2f, p0 + Vector3.up * 2.2f, new Vector2(u0, 0), new Vector2(u0 + 0.125f, 1));
                    Slope(crowd[(g + 1) % 4], a0 + gap, a1 - gap, upIn, upOut, upH0, upH1);
                    Slope(aisles, a1 - gap, a1 + gap, upIn, upOut, upH0 + 0.05f, upH1 + 0.05f, 0.4f);
                    Band(concrete, a0, a1, upOut + 0.2f, 0, upH1 + 2, 0);
                    // White roof over the upper tier (styles 1 and 2 on the long sides) with a deep front beam.
                    bool sideStand = Mathf.Abs(Mathf.Cos(mid)) < 0.8f;
                    if (style == 1 || (style == 2 && sideStand))
                    {
                        var r0 = Ell(a0, upOut + 2, upH1 + 7);
                        var r1 = Ell(a1, upOut + 2, upH1 + 7);
                        var r2 = Ell(a1, upIn - 6, upH1 + 5);
                        var r3 = Ell(a0, upIn - 6, upH1 + 5);
                        roof.Quad(0, r0, r1, r2, r3, Vector2.zero, new Vector2(2, 2));
                        roofUnder.Quad(0, r3 - Vector3.up * 0.5f, r2 - Vector3.up * 0.5f, r1 - Vector3.up * 0.5f, r0 - Vector3.up * 0.5f, Vector2.zero, new Vector2(2, 2));
                        fascia.Quad(0, r3 - Vector3.up * 1.4f, r2 - Vector3.up * 1.4f, r2 + Vector3.up * 0.2f, r3 + Vector3.up * 0.2f, Vector2.zero, Vector2.one);
                        // Steel supports.
                        concrete.Cylinder(0, Ell(a0, upOut + 1, upH1), Ell(a0, upOut + 1, upH1 + 7.2f), 0.35f, 0.3f, 6, false);
                        fascia.Cylinder(0, Ell(a0, upOut + 1, upH1 + 7), Ell(a0, upIn - 5, upH1 + 5.2f), 0.25f, 0.18f, 6, false);
                    }
                }
                else
                {
                    Band(concrete, a0, a1, lowOut + 0.2f, lowH1, lowH1 + 2.4f, 0);
                }
            }
            for (int g = 0; g < 4; g++)
            {
                if (crowd[g].VertexCount == 0) continue;
                var cg = MeshKit.Spawn("Crowd" + g, crowd[g], transform, _crowdMats[g]);
                FixWinding(cg);
            }
            var concreteMat = Mats.Lit(new Color(0.7f, 0.71f, 0.74f), 0.12f, 0, Tex.Concrete, new Vector2(0.25f, 0.25f));
            FixWinding(MeshKit.Spawn("Concrete", concrete, transform, concreteMat));
            FixWinding(MeshKit.Spawn("Aisles", aisles, transform, Mats.Lit(new Color(0.78f, 0.79f, 0.8f), 0.15f, 0, Tex.Concrete)));
            if (darkSeats.VertexCount > 0) FixWinding(MeshKit.Spawn("ScreenBlock", darkSeats, transform, Mats.Lit(new Color(0.08f, 0.085f, 0.1f), 0.2f, 0, Tex.Concrete)));
            var glass = Night ? Mats.Emissive(Color.Lerp(home, Color.white, 0.3f), Color.Lerp(home, Color.white, 0.5f) * 1.2f, null, 0.8f)
                : Mats.Lit(Color.Lerp(new Color(0.55f, 0.68f, 0.78f), home, 0.2f), 0.9f, 0.35f);
            FixWinding(MeshKit.Spawn("Facade", facade, transform, glass));
            if (ribbon.VertexCount > 0 && _ledMat != null) FixWinding(MeshKit.Spawn("RibbonBoards", ribbon, transform, _ledMat));
            if (roof.VertexCount > 0)
            {
                var rgo = MeshKit.Spawn("Roof", roof, transform, Mats.Lit(new Color(0.94f, 0.95f, 0.96f), 0.5f, 0.3f, Tex.MetalPanels, new Vector2(0.1f, 0.1f)));
                var ugo = MeshKit.Spawn("RoofUnder", roofUnder, transform, Night ? Mats.Emissive(new Color(0.85f, 0.87f, 0.9f), new Color(0.35f, 0.37f, 0.4f)) : Mats.Lit(new Color(0.86f, 0.88f, 0.9f), 0.2f));
                FixRoof(rgo, true);
                FixRoof(ugo, false);
                FixWinding(MeshKit.Spawn("RoofBeam", fascia, transform, Mats.Lit(new Color(0.96f, 0.96f, 0.97f), 0.55f, 0.4f)));
            }
            if (style == 2) CornerTowers(upOut, upH1);
        }

        static void FixRoof(GameObject go, bool up)
        {
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            var v = m.vertices;
            var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var n = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if ((n.y > 0) != up) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            m.triangles = tris;
            m.RecalculateNormals();
        }

        /// <summary>Sloped seating between two angles; the crowd texture repeats every 8 m around and 8 rows up.</summary>
        void Slope(MeshBuilder b, float a0, float a1, float rIn, float rOut, float hIn, float hOut, float uvScale = 1)
        {
            int steps = 5;
            float arc = (Ell(a0, rIn) - Ell(a1, rIn)).magnitude;
            float slopeLen = Mathf.Sqrt((rOut - rIn) * (rOut - rIn) + (hOut - hIn) * (hOut - hIn));
            float rows = slopeLen / 0.85f;
            for (int s = 0; s < steps; s++)
            {
                float k0 = s / (float)steps, k1 = (s + 1) / (float)steps;
                float r0 = Mathf.Lerp(rIn, rOut, k0), r1 = Mathf.Lerp(rIn, rOut, k1);
                float h0 = Mathf.Lerp(hIn, hOut, k0), h1 = Mathf.Lerp(hIn, hOut, k1);
                float v0 = rows * k0 / 8f, v1 = rows * k1 / 8f;
                var p0 = Ell(a0, r0, h0);
                var p1 = Ell(a1, r0, h0);
                var p2 = Ell(a1, r1, h1);
                var p3 = Ell(a0, r1, h1);
                b.Quad(0, p0, p1, p2, p3, new Vector2(0, v0 * uvScale), new Vector2(arc / 8f * uvScale, v1 * uvScale));
            }
        }

        void Band(MeshBuilder b, float a0, float a1, float r, float h0, float h1, int sub)
        {
            var p0 = Ell(a0, r, h0);
            var p1 = Ell(a1, r, h0);
            float arc = (p1 - p0).magnitude;
            b.Quad(sub, p0, p1, Ell(a1, r, h1), Ell(a0, r, h1), Vector2.zero, new Vector2(arc / 8f, (h1 - h0) / 8f));
        }

        void Flat(MeshBuilder b, float a0, float a1, float rIn, float rOut, float h)
        {
            b.Quad(0, Ell(a0, rIn, h), Ell(a1, rIn, h), Ell(a1, rOut, h), Ell(a0, rOut, h), Vector2.zero, new Vector2(1, 1));
        }

        void CornerTowers(float r, float h)
        {
            var b = new MeshBuilder();
            var led = new MeshBuilder();
            foreach (float deg in new[] { 40f, 140f, 220f, 320f })
            {
                float a = deg * Mathf.Deg2Rad;
                var p = Ell(a, r - 6, 0);
                b.Box(0, p + Vector3.up * (h + 8) / 2, new Vector3(9, h + 8, 9), 0.1f);
                led.Box(0, p + Vector3.up * (h + 4), new Vector3(9.4f, 3, 9.4f), 1);
            }
            MeshKit.Spawn("Towers", b, transform, Mats.Lit(new Color(0.5f, 0.52f, 0.56f), 0.4f, 0.2f, Tex.Concrete));
            var trim = Crest.Trim(Home);
            MeshKit.Spawn("TowerLed", led, transform, Mats.Emissive(trim, trim * (Night ? 2.2f : 0.8f)));
        }

        // ------------------------------------------------- sight screens, screens

        void BuildScreensAndSight()
        {
            // Black sight screen (white ball) behind the bowler's arm.
            var sight = new MeshBuilder();
            foreach (float a in new[] { 0f })
            {
                var c = Ell(a, 9);
                var right = Vector3.Cross(Vector3.up, -c.normalized).normalized;
                var bl = c - right * 7 + Vector3.up * 0.3f;
                var br = c + right * 7 + Vector3.up * 0.3f;
                sight.Quad(0, bl, br, br + Vector3.up * 9, bl + Vector3.up * 9, Vector2.zero, Vector2.one);
                sight.Cylinder(1, c - right * 7.2f, c - right * 7.2f + Vector3.up * 9.5f, 0.15f, 0.15f, 6, false);
                sight.Cylinder(1, c + right * 7.2f, c + right * 7.2f + Vector3.up * 9.5f, 0.15f, 0.15f, 6, false);
            }
            var sg = MeshKit.Spawn("SightScreens", sight, transform, Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.15f), Mats.Lit(new Color(0.6f, 0.6f, 0.62f), 0.5f, 0.6f));
            FixWinding(sg);

            // Giant screen behind the striker's end (seen over the batter in the broadcast view),
            // and a second one high in a corner for the wide shots.
            var behind = Ell(Mathf.PI, 13, 0);
            AddScreen(behind + Vector3.up * 7.6f, 25, 12.5f, true);
            bool roofless = Venue.StadiumStyle == 0;
            AddScreen(Ell(330 * Mathf.Deg2Rad, roofless ? 50 : 72, roofless ? 27 : 36), 23, 11.5f, false);
        }

        void AddScreen(Vector3 pos, float w, float h, bool legs)
        {
            var screenRoot = new GameObject("BigScreen").transform;
            screenRoot.SetParent(transform, false);
            screenRoot.position = pos;
            screenRoot.rotation = Quaternion.LookRotation(new Vector3(-pos.x, 0, -pos.z).normalized * -1, Vector3.up);
            var frame = new MeshBuilder();
            frame.Box(0, new Vector3(0, 0, 0.45f), new Vector3(w + 1, h + 1, 0.8f), 0.2f);
            if (legs)
            {
                frame.Box(0, new Vector3(-w * 0.3f, -pos.y / 2 - h / 4, 0.6f), new Vector3(0.9f, pos.y - h / 2 + 0.5f, 0.9f), 0.2f);
                frame.Box(0, new Vector3(w * 0.3f, -pos.y / 2 - h / 4, 0.6f), new Vector3(0.9f, pos.y - h / 2 + 0.5f, 0.9f), 0.2f);
            }
            else
            {
                frame.Box(0, new Vector3(-w * 0.26f, -h * 0.85f, 0.6f), new Vector3(0.8f, h * 0.8f, 0.8f), 0.2f);
                frame.Box(0, new Vector3(w * 0.26f, -h * 0.85f, 0.6f), new Vector3(0.8f, h * 0.8f, 0.8f), 0.2f);
            }
            MeshKit.Spawn("Frame", frame, screenRoot, Mats.Lit(new Color(0.1f, 0.1f, 0.12f), 0.4f, 0.5f));
            MakeScreenCanvas(screenRoot, w, h);
        }

        readonly List<Text> _screenScore = new List<Text>(), _screenInfo = new List<Text>(), _screenFlashText = new List<Text>();
        readonly List<CanvasGroup> _screenFlash = new List<CanvasGroup>();

        void MakeScreenCanvas(Transform parent, float w, float h)
        {
            var cgo = new GameObject("ScreenCanvas", typeof(RectTransform));
            cgo.transform.SetParent(parent, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)cgo.transform;
            rt.sizeDelta = new Vector2(1000, 500);
            rt.localScale = new Vector3(w / 1000f, h / 500f, 1);
            rt.localPosition = new Vector3(0, 0, -0.05f);
            // The screen root faces away from the pitch, so an unrotated canvas reads correctly from the ground.
            rt.localRotation = Quaternion.identity;
            var art = Ui.Node(rt, "Art").Fill().gameObject.AddComponent<RawImage>();
            art.texture = Brand.ScreenArt();
            // Score bar along the bottom.
            var bar = Ui.Img(rt, null, new Color(0.02f, 0.05f, 0.14f, 0.86f), "Bar");
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0.2f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            var accent = Ui.Img(bar.transform, null, Pal.Accent, "Accent");
            accent.rectTransform.TopStrip(0, 6);
            var score = Ui.Label(bar.transform, Home.Id + " v " + Away.Id, 62, Color.white, TextAnchor.MiddleLeft, Font.Display);
            score.rectTransform.Fill(30, 6, 400, 0);
            var info = Ui.Label(bar.transform, Venue.GroundShort.ToUpperInvariant(), 40, Pal.Gold, TextAnchor.MiddleRight, Font.Bold);
            info.rectTransform.Fill(500, 6, 30, 0);
            // Flash overlay for SIX / FOUR / OUT.
            var flash = Ui.Node(rt, "Flash").Fill();
            var fg = flash.gameObject.AddComponent<CanvasGroup>();
            fg.alpha = 0;
            var fbg = Ui.Img(flash, Sprites.VGradient(0.95f, 0.75f), Pal.Hex(0x050A1C), "FlashBg");
            fbg.rectTransform.Fill();
            var ft = Ui.Label(flash, "", 230, Color.white, TextAnchor.MiddleCenter, Font.Display);
            ft.rectTransform.Fill();
            Ui.Outline(ft, 6, 0.6f);
            _screenScore.Add(score);
            _screenInfo.Add(info);
            _screenFlash.Add(fg);
            _screenFlashText.Add(ft);
            foreach (var g in cgo.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }

        public void SetScreen(string big, string small)
        {
            foreach (var t in _screenScore) t.text = big;
            foreach (var t in _screenInfo) t.text = small;
        }

        public void FlashScreen(string big, Color c, float seconds = 3)
        {
            _screenFlashUntil = Time.time + seconds;
            foreach (var t in _screenFlashText)
            {
                t.text = big;
                t.color = c;
            }
        }

        // ----------------------------------------------------------- lights

        void BuildFloodlights()
        {
            int n = Venue.Floodlights;
            float r = Venue.StadiumStyle == 0 ? 60 : 88;
            var mast = new MeshBuilder();
            var lamps = new MeshBuilder();
            var lampMat = Night ? Mats.Emissive(new Color(1, 1, 0.97f), new Color(6, 6, 5.6f)) : Mats.Lit(new Color(0.85f, 0.86f, 0.88f), 0.7f, 0.3f);
            for (int i = 0; i < n; i++)
            {
                float a = (n == 6 ? (i * 60 + 30) : (i * 90 + 45)) * Mathf.Deg2Rad;
                var basePos = Ell(a, r, 0);
                float height = 64;
                var top = basePos + Vector3.up * height;
                // Four-legged tapering mast with cross bracing.
                var inward = new Vector3(-basePos.x, 0, -basePos.z).normalized;
                var side = Vector3.Cross(Vector3.up, inward);
                for (int k = 0; k < 4; k++)
                {
                    var o = (k % 2 == 0 ? side : -side) * 1.6f + (k < 2 ? inward : -inward) * 1.6f;
                    mast.Cylinder(0, basePos + o, top + o * 0.35f, 0.22f, 0.14f, 6, false);
                }
                for (float y = 6; y < height; y += 7)
                {
                    float k = y / height;
                    float w = Mathf.Lerp(1.6f, 0.56f, k);
                    var c = basePos + Vector3.up * y;
                    mast.Cylinder(0, c + side * w + inward * w, c - side * w + inward * w, 0.07f, 0.07f, 4, false);
                    mast.Cylinder(0, c + side * w - inward * w, c - side * w - inward * w, 0.07f, 0.07f, 4, false);
                }
                // Lamp head tilted towards the pitch.
                var headCenter = top + inward * 1.2f + Vector3.up * 2.5f;
                var tilt = Quaternion.LookRotation((new Vector3(0, 0, 0) - headCenter).normalized, Vector3.up);
                var hx = tilt * Vector3.right;
                var hy = tilt * Vector3.up;
                var hz = tilt * Vector3.forward;
                mast.Box(0, headCenter - hz * 0.5f, new Vector3(1, 1, 1) * 0.1f);
                var corners = new[] { headCenter - hx * 6 - hy * 3.5f, headCenter + hx * 6 - hy * 3.5f, headCenter + hx * 6 + hy * 3.5f, headCenter - hx * 6 + hy * 3.5f };
                mast.Quad(0, corners[1] - hz * 0.4f, corners[0] - hz * 0.4f, corners[3] - hz * 0.4f, corners[2] - hz * 0.4f, Vector2.zero, Vector2.one);
                for (int gx = 0; gx < 6; gx++)
                {
                    for (int gy = 0; gy < 4; gy++)
                    {
                        var lc = headCenter + hx * (-5 + gx * 2) + hy * (-2.6f + gy * 1.75f);
                        lamps.Quad(0, lc - hx * 0.8f - hy * 0.7f, lc + hx * 0.8f - hy * 0.7f, lc + hx * 0.8f + hy * 0.7f, lc - hx * 0.8f + hy * 0.7f, Vector2.zero, Vector2.one);
                    }
                }
                if (Night)
                {
                    var glow = new GameObject("Glow").transform;
                    glow.SetParent(transform, false);
                    glow.position = headCenter + hz * 0.6f;
                    var gm = MeshKit.Spawn("GlowQuad", MeshKit.Quad(30, 22), glow, Mats.Additive(new Color(1f, 0.96f, 0.85f, 0.55f), Tex.Glow));
                    MeshKit.NoShadows(gm);
                    glow.gameObject.AddComponent<Billboard>();
                    _glows.Add(glow);
                    if (Quality.SpotLights && FloodSpots.Count < 4 && i % (n / 4 > 1 ? 2 : 1) == 0)
                    {
                        var lgo = new GameObject("FloodSpot");
                        lgo.transform.SetParent(transform, false);
                        lgo.transform.position = headCenter;
                        lgo.transform.rotation = Quaternion.LookRotation((new Vector3(0, 0, 0) - headCenter).normalized);
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Spot;
                        l.spotAngle = 70;
                        l.range = 220;
                        l.intensity = 2200;
                        l.color = new Color(1, 0.98f, 0.94f);
                        l.shadows = LightShadows.None;
                        FloodSpots.Add(l);
                    }
                }
            }
            var mg = MeshKit.Spawn("Masts", mast, transform, Mats.Lit(new Color(0.55f, 0.57f, 0.6f), 0.5f, 0.6f));
            MeshKit.CastOnly(mg, Quality.Current >= Gfx.High, true);
            var lg = MeshKit.Spawn("Lamps", lamps, transform, lampMat);
            FixFacingCenter(lg);
            MeshKit.NoShadows(lg);

            if (Quality.Current >= Gfx.Medium) Spidercam(r);
        }

        static void FixFacingCenter(GameObject go)
        {
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            var v = m.vertices;
            var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = v[tris[i]];
                var n = Vector3.Cross(v[tris[i + 1]] - a, v[tris[i + 2]] - a);
                if (Vector3.Dot(n, new Vector3(0, 0, 0) - a) < 0) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            m.triangles = tris;
            m.RecalculateNormals();
        }

        void Spidercam(float r)
        {
            var b = new MeshBuilder();
            var cam = new Vector3(18, 28, 30);
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                var p = Ell(deg * Mathf.Deg2Rad, r, 60);
                b.Cylinder(0, p, cam, 0.035f, 0.035f, 4, false);
            }
            b.Box(0, cam, new Vector3(0.8f, 0.6f, 0.8f));
            var go = MeshKit.Spawn("Spidercam", b, transform, Mats.Lit(new Color(0.12f, 0.12f, 0.12f), 0.3f));
            MeshKit.NoShadows(go);
        }

        // ------------------------------------------------------- surroundings

        void BuildSurroundings()
        {
            // Team flags along the rim.
            var crestTex = Crest.Texture(Home, 128);
            var awayTex = Crest.Texture(Away, 128);
            float r = Venue.StadiumStyle == 0 ? 54 : 72, h = Venue.StadiumStyle == 0 ? 28 : 36;
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                var pole = new MeshBuilder();
                var basePos = Ell(a, r, h);
                pole.Cylinder(0, basePos, basePos + Vector3.up * 7, 0.08f, 0.06f, 6, false);
                MeshKit.Spawn("FlagPole", pole, transform, Mats.Lit(new Color(0.8f, 0.8f, 0.82f), 0.6f, 0.8f));
                var flag = new GameObject("Flag").transform;
                flag.SetParent(transform, false);
                flag.position = basePos + Vector3.up * 6;
                var tex = i % 2 == 0 ? crestTex : awayTex;
                var fm = Mats.Lit(i % 2 == 0 ? Crest.Primary(Home) : Crest.Primary(Away), 0.2f);
                var q = MeshKit.Spawn("Cloth", MeshKit.Quad(3.2f, 2.0f), flag, fm);
                q.transform.localPosition = new Vector3(1.6f, 0, 0);
                var em = MeshKit.Spawn("Badge", MeshKit.Quad(1.6f, 1.6f), flag, Mats.Cutout(Color.white, tex, 0.4f));
                em.transform.localPosition = new Vector3(1.6f, 0, -0.02f);
                var em2 = MeshKit.Spawn("Badge2", MeshKit.Quad(1.6f, 1.6f), flag, Mats.Cutout(Color.white, tex, 0.4f));
                em2.transform.localPosition = new Vector3(1.6f, 0, 0.02f);
                em2.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var back = MeshKit.Spawn("ClothBack", MeshKit.Quad(3.2f, 2.0f), flag, fm);
                back.transform.localPosition = new Vector3(1.6f, 0, 0.005f);
                back.transform.localRotation = Quaternion.Euler(0, 180, 0);
                _flags.Add(flag);
            }

            // Tree belts behind both open ends: full canopies of several overlapping crowns.
            var ends = new MeshBuilder();
            var trng = new System.Random(Teams.IndexOf(Venue.Id) * 29 + 1);
            foreach (float endA in new[] { 0f, Mathf.PI })
            {
                int n = Quality.Current == Gfx.Low ? 14 : 26;
                for (int i = 0; i < n; i++)
                {
                    float a = endA + (float)(trng.NextDouble() - 0.5) * 0.62f;
                    float extra = _lowOut * 0.7f + 8 + (float)trng.NextDouble() * 26;
                    var p = Ell(a, extra, 0);
                    float s = 4.5f + (float)trng.NextDouble() * 3.5f;
                    ends.Cylinder(1, p, p + Vector3.up * s * 0.9f, 0.5f, 0.35f, 5, false);
                    int sub = trng.NextDouble() < 0.5 ? 0 : 2;
                    ends.Sphere(sub, p + Vector3.up * s * 1.35f, new Vector3(s * 0.85f, s * 0.75f, s * 0.85f), 9, 6);
                    ends.Sphere(sub, p + new Vector3(s * 0.45f, s * 1.1f, s * 0.2f), new Vector3(s * 0.6f, s * 0.55f, s * 0.6f), 8, 5);
                    ends.Sphere(sub == 0 ? 2 : 0, p + new Vector3(-s * 0.4f, s * 1.05f, -s * 0.25f), new Vector3(s * 0.62f, s * 0.55f, s * 0.62f), 8, 5);
                }
            }
            var leafA = Mats.Lit(Night ? new Color(0.05f, 0.09f, 0.05f) : new Color(0.17f, 0.4f, 0.13f), 0.12f, 0, Tex.GrassDetail, new Vector2(3, 3));
            var leafB = Mats.Lit(Night ? new Color(0.06f, 0.1f, 0.06f) : new Color(0.24f, 0.5f, 0.17f), 0.12f, 0, Tex.GrassDetail, new Vector2(3, 3));
            var eg = MeshKit.Spawn("EndTrees", ends, transform, leafA, Mats.Lit(new Color(0.32f, 0.24f, 0.16f), 0.1f), leafB);
            MeshKit.CastOnly(eg, false, true);

            if (!Quality.Skyline) return;
            // City skyline (kept out of the open ends so the trees and sky show behind the screens).
            var bld = new MeshBuilder();
            var rng = new System.Random(Teams.IndexOf(Venue.Id) * 13 + 5);
            int count = Quality.Current == Gfx.Ultra ? 90 : 60;
            for (int i = 0; i < count; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                if (Mathf.Abs(Mathf.Cos(a)) > 0.9f) continue;
                float dist = 230 + (float)rng.NextDouble() * 220;
                float w = 18 + (float)rng.NextDouble() * 26, d = 18 + (float)rng.NextDouble() * 22;
                float ht = 18 + (float)Math.Pow(rng.NextDouble(), 2.2) * 110;
                var c = new Vector3(Mathf.Sin(a) * dist, ht / 2, Mathf.Cos(a) * dist);
                bld.Box(0, c, new Vector3(w, ht, d), 1 / 24f);
            }
            var winTex = Tex.Windows(Night);
            var bm = Night ? Mats.Emissive(Color.white, new Color(0.9f, 0.9f, 0.9f), winTex, 0.5f) : Mats.Lit(Color.white, 0.55f, 0.1f, winTex);
            var bgo = MeshKit.Spawn("Skyline", bld, transform, bm);
            MeshKit.NoShadows(bgo);

            // Trees between the stadium and the city.
            var trees = new MeshBuilder();
            for (int i = 0; i < 70; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                float dist = 175 + (float)rng.NextDouble() * 45;
                var p = new Vector3(Mathf.Sin(a) * dist, 0, Mathf.Cos(a) * dist);
                float s = 5 + (float)rng.NextDouble() * 5;
                trees.Cylinder(1, p, p + Vector3.up * s * 0.6f, 0.4f, 0.3f, 5, false);
                trees.Sphere(0, p + Vector3.up * s * 1.1f, new Vector3(s * 0.75f, s * 0.7f, s * 0.75f), 7, 5);
            }
            var tg = MeshKit.Spawn("Trees", trees, transform, Mats.Lit(Night ? new Color(0.06f, 0.1f, 0.06f) : new Color(0.2f, 0.35f, 0.16f), 0.1f), Mats.Lit(new Color(0.3f, 0.22f, 0.15f), 0.1f));
            MeshKit.NoShadows(tg);
        }

        // ------------------------------------------------------------ runtime

        /// <summary>Crowd rises: frames flip for a few seconds.</summary>
        public void Cheer(float seconds) => _cheerUntil = Mathf.Max(_cheerUntil, Time.time + seconds);

        void Update()
        {
            float t = Time.time;
            // LED boards step through the sponsor strip every few seconds, like real perimeter boards.
            if (_ledMat != null) _ledMat.SetTextureOffset("_BaseMap", new Vector2(Mathf.Floor(t / 5f) * 0.125f, 0));
            float flashA = t < _screenFlashUntil ? 1 : 0;
            foreach (var g in _screenFlash) g.alpha = Mathf.MoveTowards(g.alpha, flashA, Time.deltaTime * 5);
            _cheerTimer -= Time.deltaTime;
            if (_cheerTimer <= 0)
            {
                _cheerTimer = 0.18f;
                bool cheering = t < _cheerUntil;
                for (int i = 0; i < _crowdMats.Count; i++)
                {
                    bool up = cheering ? UnityEngine.Random.value < 0.6f : UnityEngine.Random.value < 0.04f;
                    Mats.SetBase(_crowdMats[i], up ? _crowdB : _crowdA);
                }
            }
            for (int i = 0; i < _flags.Count; i++) _flags[i].localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.7f + i) * 25 + i * 45, Mathf.Sin(t * 3.1f + i * 2) * 4);
            if (_stumpFlashEnd >= 0 && _stumpMatA != null)
            {
                bool on = t < _stumpFlashUntil && Mathf.Repeat(t * 6, 1) < 0.55f;
                var c = on ? new Color(2.2f, 0.25f, 0.2f) : Color.black;
                Mats.SetEmission(_stumpMatA, c);
                Mats.SetEmission(_stumpMatB, c);
                if (t >= _stumpFlashUntil) _stumpFlashEnd = -1;
            }
        }
    }

    /// <summary>Faces the main camera every frame.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, Vector3.up);
        }
    }

    /// <summary>Simple ballistic tumble for stumps and bails knocked out of the ground.</summary>
    public sealed class Flyer : MonoBehaviour
    {
        Vector3 _v, _spin;
        bool _active;
        bool _light;

        public void Launch(Vector3 v, Vector3 spin, bool light)
        {
            _v = v;
            _spin = spin;
            _active = true;
            _light = light;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            _v += Physics.gravity * dt;
            transform.position += _v * dt;
            transform.Rotate(_spin * dt, Space.World);
            float floor = _light ? 0.01f : 0.02f;
            if (transform.position.y < floor)
            {
                var p = transform.position;
                p.y = floor;
                transform.position = p;
                _v = new Vector3(_v.x * 0.45f, -_v.y * 0.25f, _v.z * 0.45f);
                _spin *= 0.5f;
                if (_v.magnitude < 0.25f) _active = false;
            }
            if (!_light)
            {
                // Stumps lean back rather than flying far.
                if (transform.position.y < 0.05f && _v.magnitude < 1) _active = false;
            }
        }
    }
}
