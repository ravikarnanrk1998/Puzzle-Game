using System;
using System.Collections.Generic;
using PCC.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace PCC.Game
{
    public enum ScreenEvent { None, Four, Six, Out, Wide, NoBall }

    /// <summary>
    /// Builds the full stadium in 3D: outfield, pitch, rope, LED boards, two-tier
    /// stands with crowds, cantilever roof with flags, big screens, sight screens,
    /// floodlight towers and a city skyline. Day / night changes materials.
    /// </summary>
    public sealed class Stadium : MonoBehaviour
    {
        /// <summary>Floodlight tower angles around the ground.</summary>
        public static readonly double[] TowerAngles = { 0.6632, 2.2340, 3.8048, 5.3756 };

        /// <summary>Direction towards the sun in cricket space (afternoon games).</summary>
        public static readonly V3 SunDir = new V3(0.42, -0.55, 0.72).Normalized;

        public TeamInfo Home { get; private set; }
        public TeamInfo Away { get; private set; }
        public bool Day { get; private set; }
        public float Rx { get; private set; }
        public float Ry { get; private set; }

        Material _crowdMat, _cloudMat;
        Texture2D _crowdTex, _cheerTex;
        Mesh _flagMesh;
        Vector3[] _flagBase;
        Vector3[] _flagVerts;
        readonly List<Material> _screenMats = new List<Material>();
        readonly Dictionary<ScreenEvent, Texture2D> _screenTex = new Dictionary<ScreenEvent, Texture2D>();
        Material _boardMat;
        float _hype;
        ScreenEvent _event = ScreenEvent.None;
        Color _trim;

        static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();

        static Texture2D Cached(string key, Func<Texture2D> make)
        {
            if (TexCache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            TexCache[key] = t;
            return t;
        }

        public static Stadium Build(TeamInfo home, TeamInfo away, bool day, Transform parent = null)
        {
            var go = new GameObject("Stadium " + home.Id);
            if (parent != null) go.transform.SetParent(parent, false);
            var s = go.AddComponent<Stadium>();
            s.Construct(home, away, day);
            return s;
        }

        void Construct(TeamInfo home, TeamInfo away, bool day)
        {
            Home = home;
            Away = away;
            Day = day;
            Rx = home.Boundary;
            Ry = Rx + 4;
            var primary = Mats.Hex(home.Primary);
            var secondary = Mats.Hex(home.Secondary);
            _trim = secondary.Luma() < 0.2f ? Mats.Hex(home.Glow) : secondary;

            BuildGround(day);
            BuildBoundary(primary);
            BuildStands(home, away, day, primary);
            BuildRoof(day, primary);
            BuildScreens(home, away);
            BuildTowers(day);
            BuildCity(day);
            if (day) BuildClouds();
        }

        // ------------------------------------------------------------ ground

        void BuildGround(bool day)
        {
            float world = 2 * (Rx + 14);
            var grass = Cached("grass" + Rx + day, () => Tex.Grass(1024, world, Rx, Ry, day));
            var b = new MeshBuilder();
            int n = 24;
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, v = j / (float)n;
                var p = new V3(-world / 2 + u * world, Pitch.Mid - world / 2 + v * world, 0);
                b.Add(p.U, new Vector2(u, v));
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i;
                b.Quad(a, a + 1, a + n + 2, a + n + 1, Vector3.up);
            }
            var field = MeshKit.Obj("Outfield", transform, b.Build("Outfield"), Mats.Lit(Color.white, 0.08f, grass), false, true);
            field.transform.localPosition = Vector3.zero;

            // Concrete / town beyond the stadium.
            var outer = MeshKit.Obj("Town", transform, MeshKit.Quad, Mats.Lit(day ? new Color(0.5f, 0.5f, 0.48f) : new Color(0.05f, 0.06f, 0.08f), 0.05f), false, true);
            outer.transform.localPosition = new V3(0, Pitch.Mid, -0.05).U;
            outer.transform.localRotation = Quaternion.Euler(90, 0, 0);
            outer.transform.localScale = new Vector3(1400, 1400, 1);

            // Pitch strip, a hair above the grass.
            var pitchTex = Cached("pitch" + day, () => Tex.PitchStrip(day));
            var pb = new MeshBuilder();
            pb.Quad(new V3(-1.52, -1.94, 0.012).U, new V3(1.52, -1.94, 0.012).U, new V3(1.52, 22.06, 0.012).U, new V3(-1.52, 22.06, 0.012).U,
                Vector3.up, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            MeshKit.Obj("Pitch", transform, pb.Build("Pitch"), Mats.Lit(Color.white, 0.12f, pitchTex), false, true);
        }

        void BuildBoundary(Color primary)
        {
            // Rope: white tube with a team-colour stripe on top.
            int cols = 220;
            var rope = MeshKit.RingBand(Rx, Ry, -0.16, 0.16, 0.13, 0.13, cols, 1, 10);
            MeshKit.Obj("Rope", transform, MeshKit.RingBand(Rx, Ry, -0.18, -0.18, 0, 0.14, cols, 1, 10), Mats.Lit(new Color(0.96f, 0.96f, 0.96f), 0.3f), true, true);
            MeshKit.Obj("RopeOut", transform, MeshKit.RingBand(Rx, Ry, 0.18, 0.18, 0, 0.14, cols, 1, 10, null, true), Mats.Lit(new Color(0.96f, 0.96f, 0.96f), 0.3f), true, true);
            MeshKit.Obj("RopeTop", transform, rope, Mats.Lit(primary, 0.35f), false, true);

            // LED advertising boards on the apron.
            var boardTex = BoardTexture();
            _boardMat = Mats.UnlitInstance(Color.white, boardTex);
            MeshKit.Obj("Boards", transform, MeshKit.RingBand(Rx, Ry, 3.0, 3.0, 0, 1.0, 200, 1, 34), _boardMat, false, false);
            MeshKit.Obj("BoardsBack", transform, MeshKit.RingBand(Rx, Ry, 3.05, 3.05, 0, 1.0, 200, 1, 34, null, true), Mats.Lit(new Color(0.08f, 0.09f, 0.12f), 0.2f), false, false);

            // Sight screens at both ends.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double y = Pitch.Mid + sgn * (Ry + 4.2);
                var sb = new MeshBuilder();
                sb.Quad(new V3(-7.5, y, 0).U, new V3(7.5, y, 0).U, new V3(7.5, y, 7.2).U, new V3(-7.5, y, 7.2).U,
                    new Vector3(0, 0, -sgn), new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
                MeshKit.Obj("SightScreen", transform, sb.Build("Sight"), Mats.Lit(new Color(0.02f, 0.025f, 0.04f), 0.4f, null, true), true, true);
            }
        }

        Texture2D BoardTexture()
        {
            return Cached("boards" + Home.Id + Away.Id, () =>
            {
                const int w = 2048, h = 96;
                var px = new Px(w, h, Color.black);
                var home = Mats.Hex(Home.Primary);
                var away = Mats.Hex(Away.Primary);
                var panels = new[]
                {
                    (home, Home.Name.ToUpperInvariant()),
                    (new Color(0.04f, 0.06f, 0.2f), League.Name),
                    (_trim, "FOUR  •  SIX  •  WICKET"),
                    (away, Away.Name.ToUpperInvariant()),
                };
                for (int i = 0; i < panels.Length; i++)
                {
                    var (bg, text) = panels[i];
                    float x0 = i * 512f;
                    for (int y = 0; y < h; y++)
                    {
                        var c = Color.Lerp(bg.Darken(0.25f), bg, Mathf.Sin(y / (float)h * Mathf.PI));
                        for (int x = (int)x0; x < x0 + 512; x++) px.P[y * w + x] = c;
                    }
                    var fg = bg.Luma() > 0.6f ? new Color(0.08f, 0.08f, 0.12f) : Color.white;
                    if (i == 1) fg = new Color(1f, 0.8f, 0.3f);
                    TextBaker.Draw(px, text, x0 + 256, 26, text.Length > 22 ? 38 : 48, fg);
                }
                return px.ToTexture("Boards", true, TextureWrapMode.Repeat);
            });
        }

        // ------------------------------------------------------------ stands

        static Color32 Gray(float k)
        {
            byte v = (byte)Mathf.Clamp(k * 255, 0, 255);
            return new Color32(v, v, v, 255);
        }

        void BuildStands(TeamInfo home, TeamInfo away, bool day, Color primary)
        {
            var awayC = Mats.Hex(away.Primary);
            _crowdTex = Cached("crowd" + home.Id + away.Id + day, () => Tex.Crowd(primary, _trim, awayC, day, false, 7));
            _cheerTex = Cached("cheer" + home.Id + away.Id + day, () => Tex.Crowd(primary, _trim, awayC, day, true, 7));
            _crowdMat = new Material(Mats.Lit(Color.white, 0.1f, _crowdTex));

            const int cols = 200;
            MeshKit.Obj("Wall", transform, MeshKit.RingBand(Rx, Ry, 9.0, 9.0, 0, 1.55, cols, 1, 20), Mats.Lit(day ? new Color(0.16f, 0.2f, 0.32f) : new Color(0.06f, 0.07f, 0.16f), 0.3f), true, true);
            MeshKit.Obj("WallTrim", transform, MeshKit.RingBand(Rx, Ry, 8.98, 8.98, 1.3, 1.55, cols, 1, 20), Mats.Lit(_trim, 0.4f), false, true);
            MeshKit.Obj("LowerTier", transform, MeshKit.RingBand(Rx, Ry, 9.2, 22.3, 1.5, 8.5, cols, 6, 40), _crowdMat, true, true);
            MeshKit.Obj("Fascia", transform, MeshKit.RingBand(Rx, Ry, 22.4, 22.4, 8.4, 11.4, cols, 1, 20), Mats.Lit(day ? new Color(0.1f, 0.12f, 0.2f) : new Color(0.03f, 0.04f, 0.1f), 0.3f), true, true);
            MeshKit.Obj("FasciaBanner", transform, MeshKit.RingBand(Rx, Ry, 22.32, 22.32, 9.1, 10.7, cols, 1, 32), Mats.Unlit(Color.white, BannerTexture(primary)), false, false);
            MeshKit.Obj("UpperTier", transform, MeshKit.RingBand(Rx, Ry, 23, 34.7, 11.4, 19.5, cols, 6, 40), _crowdMat, true, true);
            MeshKit.Obj("BackWall", transform, MeshKit.RingBand(Rx, Ry, 34.8, 36.5, 19.4, 21.1, cols, 1, 20), Mats.Lit(day ? new Color(0.36f, 0.39f, 0.45f) : new Color(0.05f, 0.06f, 0.13f), 0.2f), true, true);
            // Outside face of the bowl.
            MeshKit.Obj("Facade", transform, MeshKit.RingBand(Rx, Ry, 36.6, 36.6, 0, 21.2, cols, 1, 20, null, true), Mats.Lit(day ? new Color(0.62f, 0.64f, 0.68f) : new Color(0.06f, 0.07f, 0.12f), 0.2f), true, true);
        }

        Texture2D BannerTexture(Color primary)
        {
            return Cached("banner" + Home.Id, () =>
            {
                const int w = 2048, h = 96;
                var px = new Px(w, h, primary);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px.P[y * w + x] = Color.Lerp(primary.Darken(0.3f), primary, Mathf.Sin(y / (float)h * Mathf.PI));
                px.Rect(0, 0, w, 5, _trim);
                px.Rect(0, h - 5, w, h, _trim);
                var fg = primary.Luma() > 0.6f ? new Color(0.08f, 0.08f, 0.12f) : Color.white;
                for (int i = 0; i < 4; i++) TextBaker.Draw(px, Home.Name.ToUpperInvariant(), i * 512 + 256, 28, 44, fg);
                return px.ToTexture("Banner", true, TextureWrapMode.Repeat);
            });
        }

        // ------------------------------------------------------------ roof

        void BuildRoof(bool day, Color primary)
        {
            const int cols = 200;
            var roofMat = Mats.Lit(day ? new Color(0.88f, 0.9f, 0.93f) : new Color(0.1f, 0.11f, 0.2f), 0.35f, null, true);
            MeshKit.Obj("Roof", transform, MeshKit.RingBand(Rx, Ry, 36.5, 26, 21, 24.2, cols, 2, 20), roofMat, true, true);
            var edge = day ? Mats.Lit(Color.white, 0.5f) : Mats.Lit(primary, 0.4f, null, false, 0, primary * 1.6f);
            MeshKit.Obj("RoofEdge", transform, MeshKit.RingBand(Rx, Ry, 26, 26, 24.1, 25.7, cols, 1, 20), edge, true, true);

            // Steel ribs under the canopy.
            var ribMat = Mats.Lit(day ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.03f, 0.03f, 0.06f), 0.4f, null, true);
            const int ribs = 64;
            var rb = new MeshBuilder();
            for (int i = 0; i < ribs; i++)
            {
                double phi = -Math.PI / 2 + (i + 0.5) / ribs * Math.PI * 2;
                var side = new V3(-Math.Sin(phi), Math.Cos(phi), 0) * 0.35;
                V3 At(double e, double z) => new V3(Math.Cos(phi) * (Rx + e), Pitch.Mid + Math.Sin(phi) * (Ry + e), z);
                var a = At(36.3, 20.9);
                var b2 = At(26.2, 24.0);
                rb.Quad((a - side).U, (a + side).U, (b2 + side).U, (b2 - side).U, Vector3.down, new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
            }
            MeshKit.Obj("Ribs", transform, rb.Build("Ribs"), ribMat, false, true);

            // Flags along the roof edge (vertices animated each frame).
            const int flags = 56;
            var fb = new MeshBuilder();
            var cols4 = new[] { primary, _trim, Color.white, primary.Lighten(0.15f) };
            for (int i = 0; i < flags; i++)
            {
                var c = (Color32)cols4[i % cols4.Length];
                var pole = new Color32(220, 224, 230, 255);
                // 4 verts pole + 6 verts flag (3 columns x 2 rows).
                for (int k = 0; k < 4; k++) fb.Add(Vector3.zero, Vector2.zero, pole);
                for (int k = 0; k < 6; k++) fb.Add(Vector3.zero, Vector2.zero, c);
            }
            _flagVerts = new Vector3[fb.Verts.Count];
            _flagBase = new Vector3[fb.Verts.Count];
            LayoutFlags(0);
            for (int i = 0; i < fb.Verts.Count; i++) fb.Verts[i] = _flagVerts[i];
            for (int i = 0; i < flags; i++)
            {
                int o = i * 10;
                fb.Quad(o, o + 1, o + 2, o + 3, Vector3.up + Vector3.right);
                // Flag panels (double sided material, facing doesn't matter).
                fb.Quad(o + 4, o + 5, o + 8, o + 7, Vector3.forward);
                fb.Quad(o + 5, o + 6, o + 9, o + 8, Vector3.forward);
            }
            _flagMesh = fb.Build("Flags");
            _flagMesh.MarkDynamic();
            var flagMat = Mats.Lit(Color.white, 0.25f, null, true);
            var flagGo = MeshKit.Obj("Flags", transform, _flagMesh, VertexColorMat(flagMat), true, false);
            flagGo.GetComponent<MeshRenderer>().sharedMaterial = VertexColorMat(flagMat);
        }

        static Material _vcMat;

        /// <summary>Flags carry their colour in vertex colours; tint via a white material plus colour per submesh.</summary>
        static Material VertexColorMat(Material fallback)
        {
            if (_vcMat != null) return _vcMat;
            var m = Resources.Load<Material>("Materials/PCC_VertexColor");
            _vcMat = m != null ? m : fallback;
            return _vcMat;
        }

        void LayoutFlags(float time)
        {
            const int flags = 56;
            int o = 0;
            for (int i = 0; i < flags; i++)
            {
                double phi = -Math.PI / 2 + (i + 0.5) / flags * Math.PI * 2;
                double ox = Math.Cos(phi), oy = Math.Sin(phi);
                double tx = -oy, ty = ox;
                double bx = ox * (Rx + 25.8), by = Pitch.Mid + oy * (Ry + 25.8);
                const double z0 = 25.6, z1 = 29.6;
                _flagVerts[o++] = new V3(bx - tx * 0.07, by - ty * 0.07, z0).U;
                _flagVerts[o++] = new V3(bx + tx * 0.07, by + ty * 0.07, z0).U;
                _flagVerts[o++] = new V3(bx + tx * 0.07, by + ty * 0.07, z1).U;
                _flagVerts[o++] = new V3(bx - tx * 0.07, by - ty * 0.07, z1).U;
                double ph = time * 3.2 + i * 0.9;
                for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    double u = col / 2.0, v = row;
                    double off = Math.Sin(ph - u * 3) * 0.35 * u;
                    _flagVerts[o++] = new V3(bx + tx * 2.5 * u + ox * off, by + ty * 2.5 * u + oy * off, z1 - 0.05 - v * 1.5 - u * u * 0.15).U;
                }
            }
        }

        // ------------------------------------------------------------ screens

        void BuildScreens(TeamInfo home, TeamInfo away)
        {
            foreach (ScreenEvent e in Enum.GetValues(typeof(ScreenEvent)))
            {
                _screenTex[e] = Cached("screen" + home.Id + away.Id + e, () => ScreenTexture(e));
            }
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double y = Pitch.Mid + sgn * (Ry + 22.6);
                const double hw = 13, z0 = 11.8, z1 = 19.6;
                // Frame.
                var frame = MeshKit.Obj("ScreenFrame", transform, MeshKit.Cube, Mats.Lit(new Color(0.03f, 0.03f, 0.05f), 0.5f), true, true);
                frame.transform.localPosition = new V3(0, y + sgn * 0.4, (z0 + z1) / 2).U;
                frame.transform.localScale = new Vector3((float)hw + 0.5f, (float)(z1 - z0) / 2 + 0.5f, 0.35f);
                var sb = new MeshBuilder();
                // u runs left-to-right as seen from the field.
                double l = -sgn * hw, r = sgn * hw;
                sb.Quad(new V3(l, y, z0).U, new V3(r, y, z0).U, new V3(r, y, z1).U, new V3(l, y, z1).U, new Vector3(0, 0, -sgn),
                    new Color32(255, 255, 255, 255), Vector2.zero, Vector2.one);
                var mat = Mats.UnlitInstance(Color.white, _screenTex[ScreenEvent.None]);
                _screenMats.Add(mat);
                MeshKit.Obj("BigScreen", transform, sb.Build("Screen"), mat, false, false);
                // Legs.
                foreach (double x in new[] { -hw * 0.7, hw * 0.7 })
                {
                    var leg = MeshKit.Obj("Leg", transform, MeshKit.Cube, Mats.Lit(new Color(0.18f, 0.2f, 0.25f), 0.4f), true, true);
                    leg.transform.localPosition = new V3(x, y + sgn * 0.6, z0 / 2).U;
                    leg.transform.localScale = new Vector3(0.25f, (float)z0 / 2, 0.25f);
                }
            }
        }

        Texture2D ScreenTexture(ScreenEvent e)
        {
            const int w = 1024, h = 288;
            var primary = Mats.Hex(Home.Primary);
            var px = new Px(w, h, new Color(0.02f, 0.02f, 0.04f));
            for (int y = 10; y < h - 10; y++)
            for (int x = 10; x < w - 10; x++)
            {
                float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f);
                var c = Color.Lerp(primary.Lighten(0.12f), primary.Darken(0.5f), Mathf.Clamp01(Mathf.Sqrt(dx * dx * 0.5f + dy * dy)));
                if (x % 6 == 0) c *= 0.8f;
                c.a = 1;
                px.P[y * w + x] = c;
            }
            var fg = primary.Luma() > 0.6f ? new Color(0.08f, 0.08f, 0.14f) : Color.white;
            switch (e)
            {
                case ScreenEvent.None:
                    TextBaker.Draw(px, Home.Name.ToUpperInvariant(), w / 2f, 110, 92, fg);
                    TextBaker.Draw(px, League.Name + "  " + League.Year, w / 2f, 40, 40, new Color(1f, 0.82f, 0.3f));
                    TextBaker.Draw(px, Home.Id + " v " + Away.Id, w / 2f, 220, 44, fg);
                    break;
                default:
                {
                    string label;
                    Color col;
                    switch (e)
                    {
                        case ScreenEvent.Four: label = "FOUR!"; col = new Color(0.22f, 0.9f, 1f); break;
                        case ScreenEvent.Six: label = "SIX!"; col = new Color(0.8f, 0.5f, 1f); break;
                        case ScreenEvent.Out: label = "OUT!"; col = new Color(1f, 0.3f, 0.36f); break;
                        case ScreenEvent.Wide: label = "WIDE"; col = new Color(1f, 0.8f, 0.3f); break;
                        default: label = "FREE HIT"; col = new Color(1f, 0.8f, 0.3f); break;
                    }
                    for (int i = 0; i < px.P.Length; i++) px.P[i] = Color.Lerp(px.P[i], Color.black, 0.5f);
                    TextBaker.Draw(px, label, w / 2f, 64, 170, col);
                    break;
                }
            }
            return px.ToTexture("Screen" + e, true, TextureWrapMode.Clamp);
        }

        public void SetScreen(ScreenEvent e)
        {
            if (e == _event) return;
            _event = e;
            foreach (var m in _screenMats) Mats.SetTexture(m, _screenTex[e]);
        }

        // ------------------------------------------------------------ towers

        void BuildTowers(bool day)
        {
            var mast = Mats.Lit(day ? new Color(0.62f, 0.65f, 0.72f) : new Color(0.06f, 0.07f, 0.1f), 0.5f, null, false, 0.4f);
            var head = Mats.Lit(day ? new Color(0.3f, 0.33f, 0.4f) : new Color(0.1f, 0.1f, 0.14f), 0.4f);
            var lamp = day ? Mats.Lit(new Color(0.8f, 0.8f, 0.82f), 0.8f) : Mats.Unlit(new Color(1f, 0.98f, 0.9f) * 3f);
            foreach (double phi in TowerAngles)
            {
                double cx = Math.Cos(phi) * (Rx + 42), cy = Pitch.Mid + Math.Sin(phi) * (Ry + 42);
                var m = MeshKit.Obj("Mast", transform, MeshKit.Cube, mast, true, true);
                m.transform.localPosition = new V3(cx, cy, 25).U;
                m.transform.localScale = new Vector3(0.9f, 25.5f, 0.9f);
                var toCentre = (new V3(0, Pitch.Mid, 0) - new V3(cx, cy, 0)).Normalized;
                var rot = Quaternion.LookRotation(toCentre.U, Vector3.up) * Quaternion.Euler(18, 0, 0);
                var h = MeshKit.Obj("LampHead", transform, MeshKit.Cube, head, true, true);
                h.transform.localPosition = new V3(cx, cy, 53.6).U;
                h.transform.localRotation = rot;
                h.transform.localScale = new Vector3(5.8f, 3.2f, 0.4f);
                var face = MeshKit.Obj("Lamps", transform, MeshKit.Cube, lamp, false, false);
                face.transform.localPosition = new V3(cx, cy, 53.6).U + rot * new Vector3(0, 0, 0.45f);
                face.transform.localRotation = rot;
                face.transform.localScale = new Vector3(5.4f, 2.8f, 0.05f);
                if (!day)
                {
                    var glow = MeshKit.Obj("Glow", transform, MeshKit.Quad, Mats.Unlit(new Color(1f, 0.95f, 0.8f, 0.55f), Cached("glow", () => Tex.Glow()), true, true), false, false);
                    glow.transform.localPosition = new V3(cx, cy, 53.6).U + rot * new Vector3(0, 0, 1.2f);
                    glow.transform.localRotation = rot * Quaternion.Euler(0, 180, 0);
                    glow.transform.localScale = new Vector3(26, 26, 1);
                    glow.AddComponent<Billboard>();
                }
            }
        }

        // ------------------------------------------------------------ city

        void BuildCity(bool day)
        {
            var rnd = new Random(Home.Id.GetHashCode());
            double R() => rnd.NextDouble();
            var facades = new Texture2D[3];
            for (int k = 0; k < 3; k++)
            {
                int kind = k;
                facades[k] = Cached("facade" + k + day, () => Tex.Facade(kind, day));
            }
            for (int i = 0; i < 46; i++)
            {
                double phi = R() * Math.PI * 2;
                double r = Rx + 115 + R() * 150;
                double hgt = 34 + Math.Pow(R(), 1.6) * 120;
                double w = 16 + R() * 26, d = 14 + R() * 20;
                int kind = rnd.Next(3);
                var tex = facades[kind];
                var go = MeshKit.Obj("Tower", transform, MeshKit.Cube, Mats.Lit(Color.white.Darken((float)(R() * 0.18)), day ? 0.35f : 0.1f, tex,
                    false, 0, day ? (Color?)null : new Color(0.55f, 0.48f, 0.3f)), false, true);
                go.transform.localPosition = new V3(Math.Cos(phi) * r, Pitch.Mid + Math.Sin(phi) * (r + 4), hgt / 2).U;
                go.transform.localRotation = Quaternion.Euler(0, (float)(phi * Mathf.Rad2Deg + R() * 30), 0);
                go.transform.localScale = new Vector3((float)w / 2, (float)hgt / 2, (float)d / 2);
                // Scale the facade so windows stay a sensible size.
                var mr = go.GetComponent<MeshRenderer>();
                var block = new MaterialPropertyBlock();
                block.SetVector("_BaseMap_ST", new Vector4((float)(w / 26), (float)(hgt / 26), 0, 0));
                mr.SetPropertyBlock(block);
            }

            // Palm trees near the stadium.
            var trunk = Mats.Lit(day ? new Color(0.48f, 0.36f, 0.24f) : new Color(0.04f, 0.04f, 0.05f), 0.1f);
            var leaf = Mats.Lit(day ? new Color(0.22f, 0.5f, 0.2f) : new Color(0.03f, 0.05f, 0.05f), 0.15f, null, true);
            for (int g = 0; g < 9; g++)
            {
                double phi = R() * Math.PI * 2;
                int n = 2 + rnd.Next(3);
                for (int k = 0; k < n; k++)
                {
                    double p = phi + (R() - 0.5) * 0.12, e = 40 + R() * 9, hgt = 27 + R() * 9;
                    var root = new GameObject("Palm").transform;
                    root.SetParent(transform, false);
                    root.localPosition = new V3(Math.Cos(p) * (Rx + e), Pitch.Mid + Math.Sin(p) * (Ry + e), 0).U;
                    var t = MeshKit.Obj("Trunk", root, MeshKit.TaperedLimb(0.45f, 0.28f, 8), trunk, true, true);
                    t.transform.localScale = new Vector3(1, (float)hgt, 1);
                    t.transform.localRotation = Quaternion.Euler((float)(R() - 0.5) * 8, 0, (float)(R() - 0.5) * 8);
                    for (int f = 0; f < 9; f++)
                    {
                        var fr = MeshKit.Obj("Frond", root, MeshKit.Cube, leaf, true, false);
                        float a = f / 9f * 360f + (float)R() * 20;
                        fr.transform.localPosition = new Vector3(0, (float)hgt, 0);
                        fr.transform.localRotation = Quaternion.Euler(0, a, 0) * Quaternion.Euler(-28 - (float)R() * 20, 0, 0);
                        fr.transform.localPosition += fr.transform.localRotation * new Vector3(0, 0, 2.6f);
                        fr.transform.localScale = new Vector3(0.7f, 0.05f, 2.8f);
                    }
                }
            }
        }

        void BuildClouds()
        {
            var tex = Cached("clouds", () => Tex.Clouds());
            _cloudMat = Mats.UnlitInstance(new Color(1, 1, 1, 0.95f), tex, true);
            _cloudMat.SetTextureScale("_BaseMap", new Vector2(3, 3));
            var b = new MeshBuilder();
            const int seg = 32, rings = 6;
            const float radius = 900, height = 180;
            for (int j = 0; j <= rings; j++)
            {
                float f = j / (float)rings;
                float rr = radius * Mathf.Cos(f * Mathf.PI * 0.5f) + 10;
                float h = height * Mathf.Sin(f * Mathf.PI * 0.5f) + 60;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2;
                    float alpha = Tex.SmoothStep(0, 0.35f, f);
                    b.Add(new Vector3(Mathf.Cos(a) * rr, h, Mathf.Sin(a) * rr + (float)Pitch.Mid), new Vector2(i / (float)seg, f), new Color(1, 1, 1, alpha));
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * (seg + 1) + i;
                b.Quad(a, a + 1, a + seg + 2, a + seg + 1, Vector3.down);
            }
            var dome = MeshKit.Obj("Clouds", transform, b.Build("Clouds"), _cloudMat, false, false);
            dome.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------ live

        public void SetHype(float h) => _hype = h;

        void Update()
        {
            float t = Time.time;
            // Flag cloth is CPU work: every frame on HIGH, every 3rd frame on MEDIUM, still on LOW.
            var q = Settings.Graphics;
            bool flags = q == GraphicsQuality.High || (q == GraphicsQuality.Medium && Time.frameCount % 3 == 0);
            if (_flagMesh != null && flags)
            {
                LayoutFlags(t);
                _flagMesh.vertices = _flagVerts;
                _flagMesh.RecalculateNormals();
            }
            if (_crowdMat != null)
            {
                bool cheer = _hype > 0.55f && Mathf.FloorToInt(t * 4) % 2 == 0;
                Mats.SetTexture(_crowdMat, cheer ? _cheerTex : _crowdTex);
            }
            if (_cloudMat != null) Mats.SetOffset(_cloudMat, new Vector2(t * 0.0015f, t * 0.0006f));
            if (_boardMat != null) Mats.SetOffset(_boardMat, new Vector2(-t * (0.012f + _hype * 0.03f), 0));
        }
    }

    /// <summary>Keeps a quad facing the active camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
