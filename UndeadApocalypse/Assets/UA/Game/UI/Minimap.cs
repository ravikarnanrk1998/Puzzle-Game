using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// Round minimap (HUD, top left). The stage's floor plan turns with the player so that "up" is the way they face.
    /// The player is the arrow in the centre, zombies are red dots, a boss is a red skull and the objective is a gold
    /// pin. Zombies, the boss and the objective beyond the map's range sit on the rim in their direction, so the player
    /// can always find them. Cloaked Hunters are not shown.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        /// <summary>Metres from the centre to the rim.</summary>
        const float Range = 34f;
        const int CellPx = 12;

        RectTransform _root, _map, _dots, _north;
        RawImage _mapImg;
        Image _boss, _marker;
        readonly List<Image> _pool = new List<Image>();
        Texture2D _tex;
        float _radius, _ppm, _worldW, _worldH;

        public static Minimap Create(RectTransform parent, Vector2 topLeft, float size)
        {
            var root = Ui.Rect(parent, "Minimap");
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0, 1);
            root.anchoredPosition = topLeft;
            root.sizeDelta = new Vector2(size, size);
            var m = root.gameObject.AddComponent<Minimap>();
            m._root = root;
            m.Build(size);
            return m;
        }

        void Build(float size)
        {
            _radius = size / 2 - 6;
            _ppm = _radius / Range;
            // Dark disc that also masks everything inside it to a circle.
            var disc = Ui.Img(_root, Sprites.Circle, new Color(0.03f, 0.04f, 0.055f, Ui.Dim(0.72f)), "Disc");
            disc.rectTransform.Fill(4, 4, 4, 4);
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pivot = Ui.Rect(disc.transform, "MapPivot");
            pivot.At(0.5f, 0.5f, 0, 0, 0, 0);
            _mapImg = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            _mapImg.transform.SetParent(pivot, false);
            _mapImg.raycastTarget = false;
            _mapImg.enabled = false;
            _map = _mapImg.rectTransform;
            _map.anchorMin = _map.anchorMax = new Vector2(0.5f, 0.5f);
            _map.anchoredPosition = Vector2.zero;
            // Range rings (faint) under the dots.
            var ring = Ui.Img(disc.transform, Sprites.Ring(2), new Color(1, 1, 1, 0.12f), "HalfRange");
            ring.rectTransform.At(0.5f, 0.5f, 0, 0, _radius, _radius);
            _dots = Ui.Rect(disc.transform, "Dots");
            _dots.At(0.5f, 0.5f, 0, 0, 0, 0);
            _marker = Ui.Img(_dots, Sprites.Of(Icon.Pin), Pal.Accent2, "Objective");
            _marker.rectTransform.sizeDelta = new Vector2(22, 22);
            _marker.gameObject.SetActive(false);
            _boss = Ui.Img(_dots, Sprites.Of(Icon.Skull), Pal.Bad, "Boss");
            _boss.rectTransform.sizeDelta = new Vector2(26, 26);
            _boss.gameObject.SetActive(false);
            // The player: an arrow pointing the way they face (always up), drawn above the dots.
            var me = Ui.Img(disc.transform, Sprites.Of(Icon.Play), Color.white, "Player");
            me.rectTransform.At(0.5f, 0.5f, 0, 0, 22, 22);
            me.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            Ui.Shadow(me, 1.5f, 0.8f);
            // Rim and the north mark (outside the mask so the label is never cut).
            var rim = Ui.Img(_root, Sprites.Ring(3.5f), new Color(1, 1, 1, 0.35f), "Rim");
            rim.rectTransform.Fill(2, 2, 2, 2);
            var n = Ui.Label(_root, "N", 20, Pal.Accent2, TextAnchor.MiddleCenter, FontKind.Display, "North");
            _north = n.rectTransform;
            _north.anchorMin = _north.anchorMax = new Vector2(0.5f, 0.5f);
            _north.sizeDelta = new Vector2(30, 26);
        }

        /// <summary>Paints the floor plan: open cells light with a bright edge, walls and buildings left dark.</summary>
        void BuildMap(Level level)
        {
            var g = level.Plan.Grid;
            int w = g.W * CellPx, h = g.H * CellPx;
            var px = new Color32[w * h];
            var floor = new Color32(86, 96, 108, 170);
            var edge = new Color32(170, 182, 196, 235);
            var clear = new Color32(0, 0, 0, 0);
            bool Open(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && g.IsOpen(x / CellPx, y / CellPx);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Open(x, y)) { px[y * w + x] = clear; continue; }
                    bool border = !Open(x - 1, y) || !Open(x + 1, y) || !Open(x, y - 1) || !Open(x, y + 1);
                    px[y * w + x] = border ? edge : floor;
                }
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Minimap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _tex.SetPixels32(px);
            _tex.Apply(false, true);
            _mapImg.texture = _tex;
            _worldW = g.W * Level.Cell;
            _worldH = g.H * Level.Cell;
            _map.sizeDelta = new Vector2(_worldW * _ppm, _worldH * _ppm);
            _mapImg.enabled = true;
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        /// <summary>Player-relative position in map pixels (up = the way the player faces).</summary>
        Vector2 Local(Vector3 world, Vector3 me, float sin, float cos)
        {
            float dx = world.x - me.x, dz = world.z - me.z;
            return new Vector2(dx * cos - dz * sin, dx * sin + dz * cos) * _ppm;
        }

        /// <summary>Places an icon; beyond the range it is pinned to the rim. Returns true when it is inside the range.</summary>
        bool Place(RectTransform rt, Vector2 local, float rimInset)
        {
            float max = _radius - rimInset;
            bool inside = local.sqrMagnitude <= max * max;
            rt.anchoredPosition = inside ? local : local.normalized * max;
            return inside;
        }

        void LateUpdate()
        {
            var p = PlayerRig.Instance;
            var run = StageRunner.Instance;
            if (p == null || p.WorldCam == null) return;
            if (_tex == null && run != null && run.Level != null && run.Level.Plan != null) BuildMap(run.Level);
            var me = PlayerRig.Pos;
            float yaw = p.WorldCam.transform.eulerAngles.y;
            float sin = Mathf.Sin(yaw * Mathf.Deg2Rad), cos = Mathf.Cos(yaw * Mathf.Deg2Rad);
            // Floor plan: the player's spot at the centre, turned so the facing direction is up.
            if (_tex != null)
            {
                _map.pivot = new Vector2(Mathf.Clamp01(me.x / _worldW), Mathf.Clamp01(me.z / _worldH));
                _map.anchoredPosition = Vector2.zero;
                _map.localEulerAngles = new Vector3(0, 0, yaw);
            }
            // North mark on the rim.
            var north = new Vector2(-sin, cos);
            _north.anchoredPosition = north * (_radius + 2);
            // Zombies.
            int used = 0;
            foreach (var z in Zombie.Alive)
            {
                if (z == null || !z.IsAlive || z.Cloaked) continue;
                if (used == _pool.Count)
                {
                    var dot = Ui.Img(_dots, Sprites.Circle, Pal.Bad, "Zombie");
                    _pool.Add(dot);
                }
                var img = _pool[used++];
                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                bool inside = Place(img.rectTransform, Local(z.transform.position, me, sin, cos), 5);
                img.rectTransform.sizeDelta = inside ? new Vector2(11, 11) : new Vector2(8, 8);
                img.color = inside ? Pal.Bad : Pal.Bad.A(0.55f);
            }
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
            // Boss.
            var boss = Boss.Current;
            bool showBoss = boss != null && boss.IsAlive;
            if (_boss.gameObject.activeSelf != showBoss) _boss.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                Place(_boss.rectTransform, Local(boss.transform.position, me, sin, cos), 13);
                _boss.transform.SetAsLastSibling();
            }
            // Objective marker (the same target the HUD's objective arrow points at).
            var goal = run != null ? run.MarkerTarget : null;
            if (_marker.gameObject.activeSelf != goal.HasValue) _marker.gameObject.SetActive(goal.HasValue);
            if (goal.HasValue)
            {
                Place(_marker.rectTransform, Local(goal.Value, me, sin, cos), 11);
                _marker.rectTransform.anchoredPosition += new Vector2(0, 8);   // the pin's tip on the spot
            }
        }
    }
}
