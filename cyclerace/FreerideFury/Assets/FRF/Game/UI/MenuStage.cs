using UnityEngine;

namespace FRF
{
    /// <summary>
    /// Live 3D backdrop for the menus: a low-poly hilltop with trees and peaks, and the
    /// player's rider and bike turning slowly on a wooden deck. The garage refreshes it.
    /// </summary>
    public sealed class MenuStage : MonoBehaviour
    {
        RiderModel _model;
        Transform _turn;
        Camera _cam;
        float _angle = 0.6f, _shift = 1.3f, _shiftTarget = 1.3f, _dist = 5.6f, _distTarget = 5.6f;
        Theme _theme;
        bool _frozen;
        RenderTexture _still;
        GameObject _stillImage;

        public static Theme ThemeForProgress()
        {
            int best = 0;
            for (int i = 0; i < Catalog.Career.Length; i++)
                if (Profile.Unlocked(i)) best = i;
            return Catalog.Career[best].Track.Theme;
        }

        public static MenuStage Build(Transform parent)
        {
            var go = new GameObject("MenuStage");
            go.transform.SetParent(parent, false);
            var st = go.AddComponent<MenuStage>();
            st._theme = ThemeForProgress();
            st.BuildWorld();
            SceneLook.Apply(st._theme, go.transform);
            st._cam = SceneLook.MainCamera();
            st.Refresh(Profile.Rider, Profile.Bike, Profile.PaintColor);
            st.Step(0);
            return st;
        }

        void BuildWorld()
        {
            var th = _theme;
            var rnd = new System.Random(42);
            var b = new MeshBuilder();
            // Flat-shaded hills, a bowl around the deck.
            const int n = 36;
            const float size = 360, cell = size / n;
            var h = new float[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -size / 2 + i * cell, z = -size / 2 + j * cell;
                float d = Mathf.Sqrt(x * x + z * z);
                float y = Tex.SmoothStep(8, 170, d) * 38 + (Tex.Fbm(x * 0.012f + 3, z * 0.012f, 3) - 0.5f) * 22 * Tex.SmoothStep(10, 90, d);
                // The valley opens downhill behind the deck.
                y -= Tex.SmoothStep(-20, -170, z) * Tex.SmoothStep(30, 0, Mathf.Abs(x) * 0.4f) * 30;
                h[j * (n + 1) + i] = d < 9 ? -0.05f : y;
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                Vector3 P(int ii, int jj) => new Vector3(-size / 2 + ii * cell, h[jj * (n + 1) + ii], -size / 2 + jj * cell);
                var p00 = P(i, j);
                var p10 = P(i + 1, j);
                var p01 = P(i, j + 1);
                var p11 = P(i + 1, j + 1);
                foreach (var (a, bb, c) in new[] { (p00, p01, p11), (p00, p11, p10) })
                {
                    var nrm = Vector3.Cross(bb - a, c - a).normalized;
                    if (nrm.y < 0) nrm = -nrm;
                    float slope = 1 - nrm.y;
                    float avgY = (a.y + bb.y + c.y) / 3;
                    var col = Color.Lerp(th.Grass, th.GrassDark, (float)rnd.NextDouble() * 0.6f);
                    if (slope > 0.22f) col = Color.Lerp(th.Rock, th.RockDark, (float)rnd.NextDouble());
                    if (avgY > 30 + (float)rnd.NextDouble() * 8) col = th.Cap;
                    b.FlatTri(a, bb, c, Palette.Uv(col), (a + bb + c) / 3 - nrm);
                }
            }
            // Trees and rocks around the bowl.
            for (int k = 0; k < 140; k++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float r = 14 + (float)rnd.NextDouble() * 150;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (z < -12 && Mathf.Abs(x) < 26) continue;
                int i = Mathf.Clamp(Mathf.RoundToInt((x + size / 2) / cell), 0, n), j = Mathf.Clamp(Mathf.RoundToInt((z + size / 2) / cell), 0, n);
                var p = new Vector3(x, h[j * (n + 1) + i] - 0.3f, z);
                double roll = rnd.NextDouble();
                switch (th.Biome)
                {
                    case Biome.Canyon:
                        if (roll < 0.4) Props.Cactus(b, p, 2.5f + (float)rnd.NextDouble() * 2, rnd);
                        else Props.Boulder(b, p, 1 + (float)rnd.NextDouble() * 3, th, rnd);
                        break;
                    case Biome.Volcano:
                        if (roll < 0.5) Props.DeadTree(b, p, 5 + (float)rnd.NextDouble() * 4, th, rnd);
                        else Props.Boulder(b, p, 1 + (float)rnd.NextDouble() * 3, th, rnd);
                        break;
                    case Biome.Autumn:
                        if (roll < 0.7) Props.Broadleaf(b, p, 6 + (float)rnd.NextDouble() * 5, th, rnd);
                        else Props.Pine(b, p, 7 + (float)rnd.NextDouble() * 6, Catalog.Forest, rnd);
                        break;
                    default:
                        if (roll < 0.12) Props.Boulder(b, p, 1 + (float)rnd.NextDouble() * 2, th, rnd);
                        else Props.Pine(b, p, 7 + (float)rnd.NextDouble() * 8, th, rnd, th.Biome == Biome.Glacier);
                        break;
                }
            }
            // Distant peaks.
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2;
                float r = 650 + (float)rnd.NextDouble() * 350;
                var p = new Vector3(Mathf.Cos(a) * r, -30, Mathf.Sin(a) * r);
                float hh = 220 + (float)rnd.NextDouble() * 260;
                var tip = p + Vector3.up * hh;
                b.Cylinder(p, tip, 200 + (float)rnd.NextDouble() * 160, 0, 7, th.MountainFar, false, a);
                b.Cylinder(Vector3.Lerp(p, tip, 0.66f), tip + Vector3.up, 70, 0, 7, th.MountainCap, false, a);
            }
            // The deck.
            // Wooden deck with an orange rim showing around its edge.
            b.Cylinder(new Vector3(0, -0.3f, 0), new Vector3(0, 0.13f, 0), 2.35f, 2.35f, 20, Pal.Orange);
            b.Cylinder(new Vector3(0, -0.2f, 0), new Vector3(0, 0.15f, 0), 2.15f, 2.15f, 20, Mats.Hex(0x7A5634));
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.1f;
                b.Beam(-d + Vector3.up * 0.155f, d + Vector3.up * 0.155f, 0.04f, 0.01f, Mats.Hex(0x5A3C22));
            }
            MeshKit.Obj("Stage", transform, b.Build("Stage"), Palette.Lit);
            _turn = new GameObject("Turntable").transform;
            _turn.SetParent(transform, false);
            _turn.localPosition = new Vector3(0, 0.15f, 0);
            Palette.Apply();
            if (th.Weather != Weather.None)
            {
                var fx = Fx.Create(transform);
                _weatherFx = fx;
            }
        }

        Fx _weatherFx;

        void Start()
        {
            if (_weatherFx != null && _cam != null) _weatherFx.StartWeather(_theme.Weather, _cam.transform);
        }

        public void Refresh(RiderDef rider, BikeDef bike, Color paint)
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = RiderModel.Create(_turn, rider, bike, paint, true);
            _model.ShowPose(0.15f);
        }

        /// <summary>Where the bike sits on screen: positive = to the right of centre.</summary>
        public void Frame(float shift, float distance = 5.6f)
        {
            _shiftTarget = shift;
            _distTarget = distance;
        }

        void Update()
        {
            if (!_frozen) Step(Time.deltaTime);
        }

        void Step(float dt)
        {
            _angle += dt * 0.32f;
            if (_turn != null) _turn.localRotation = Quaternion.Euler(0, _angle * Mathf.Rad2Deg, 0);
            _shift = Mathf.Lerp(_shift, _shiftTarget, 1 - Mathf.Exp(-dt * 5));
            _dist = Mathf.Lerp(_dist, _distTarget, 1 - Mathf.Exp(-dt * 5));
            if (_cam == null) return;
            var focus = new Vector3(0, 0.95f, 0);
            var camPos = new Vector3(_dist * 0.25f, 1.75f, _dist);
            var fwd = (focus - camPos).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            // Looking at a point beside the bike puts the bike off-centre.
            var look = focus - right * _shift;
            _cam.transform.position = camPos;
            _cam.transform.rotation = Quaternion.LookRotation(look - camPos, Vector3.up);
            _cam.fieldOfView = 50;
        }

        /// <summary>Freeze into a still image while a non-animated screen is up (keeps phones cool).</summary>
        public void SetLive(bool live, Transform canvas)
        {
            if (live != _frozen) return;
            if (live)
            {
                Unfreeze();
                return;
            }
            if (_cam == null) return;
            _still = new RenderTexture(Mathf.Max(64, Screen.width / 2), Mathf.Max(64, Screen.height / 2), 24, RenderTextureFormat.ARGB32);
            var prev = _cam.targetTexture;
            _cam.targetTexture = _still;
            _cam.Render();
            _cam.targetTexture = prev;
            _cam.enabled = false;
            _stillImage = new GameObject("FrozenBackdrop", typeof(RectTransform));
            _stillImage.transform.SetParent(canvas, false);
            _stillImage.transform.SetAsFirstSibling();
            var raw = _stillImage.AddComponent<UnityEngine.UI.RawImage>();
            raw.texture = _still;
            raw.raycastTarget = false;
            ((RectTransform)_stillImage.transform).Fill();
            _frozen = true;
        }

        void Unfreeze()
        {
            if (!_frozen) return;
            _frozen = false;
            if (_cam != null) _cam.enabled = true;
            if (_stillImage != null) Destroy(_stillImage);
            if (_still != null)
            {
                _still.Release();
                Destroy(_still);
            }
        }

        void OnDestroy()
        {
            Unfreeze();
            SceneLook.Clear();
        }
    }
}
