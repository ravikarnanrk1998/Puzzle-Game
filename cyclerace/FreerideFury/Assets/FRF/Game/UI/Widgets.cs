using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FRF
{
    /// <summary>
    /// Round in-race action button: multi-touch hold state plus a "pressed this frame" edge.
    /// Its label and colour can change on the fly (ground actions vs air tricks).
    /// </summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        readonly HashSet<int> _pointers = new HashSet<int>();
        bool _down, _enabledLook = true;
        Image _face, _rim, _icon, _glowImg;
        Text _label;
        float _scale = 1, _size;
        Color _color;

        public bool Held    => _pointers.Count > 0 || KeyHeld;
        public bool KeyHeld;

        /// <summary>
        /// Premium glossy round button: soft outer glow, dark shadow, shaded face, icon + caption.
        /// </summary>
        public static HoldButton Create(Transform parent, string label, Color color, float size, Sprite icon = null)
        {
            var root = Ui.Node(parent, "Btn " + label);
            root.sizeDelta = new Vector2(size, size);

            // Invisible hit area (slightly larger for fat thumbs)
            var hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Ui.Circle;
            hit.color  = new Color(0, 0, 0, 0.001f);

            var hb = root.gameObject.AddComponent<HoldButton>();
            hb._size  = size;
            hb._color = color;

            // Outer neon glow ring (colour-matched, very subtle)
            var glowNode = Ui.Node(root, "Glow");
            glowNode.Fill(-size * 0.12f, -size * 0.12f, -size * 0.12f, -size * 0.12f);
            var gi = glowNode.gameObject.AddComponent<Image>();
            gi.sprite = Ui.NeonGlow;
            gi.color  = color.WithA(0.28f);
            gi.raycastTarget = false;
            hb._glowImg = gi;

            // Drop shadow
            var shadow = Ui.Img(root, Ui.Circle, new Color(0, 0, 0, 0.50f), "Shadow");
            shadow.rectTransform.At(0.5f, 0.5f, 0, -4, size * 0.98f, size * 0.98f);

            // Rim (thin outer ring that lights up when held)
            hb._rim = Ui.Img(root, Ui.Circle, Color.white.WithA(0.85f), "Rim");
            hb._rim.rectTransform.Fill(1, 1, 1, 1);

            // Button face (shaded circle sprite)
            hb._face = Ui.Img(root, Icons.ButtonFace, color, "Face");
            hb._face.rectTransform.Fill(5, 5, 5, 5);

            // Icon
            hb._icon = Ui.Img(root, icon, Color.white, "Icon");
            hb._icon.rectTransform.At(0.5f, 0.5f, 0, size * 0.09f, size * 0.42f, size * 0.42f);
            var iconShadow = hb._icon.gameObject.AddComponent<Shadow>();
            iconShadow.effectColor    = new Color(0, 0, 0, 0.40f);
            iconShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Label
            hb._label = Ui.Label(root, label, size * 0.15f, Color.white, TextAnchor.MiddleCenter, true, "Label");
            Ui.Shadow(hb._label, 1.5f, 0.55f);

            hb.Set(label, color, icon);
            return hb;
        }

        /// <summary>With an icon, the caption is small under it; without, the text fills the button.</summary>
        public void Set(string label, Color color, Sprite icon = null)
        {
            _label.text  = label;
            _color       = color;
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null);
            var lr = _label.rectTransform;
            if (icon != null)
            {
                _label.fontSize = Mathf.RoundToInt(_size * 0.15f);
                lr.At(0.5f, 0, 0, _size * 0.13f, _size, _size * 0.22f);
            }
            else
            {
                _label.fontSize = Mathf.RoundToInt(_size * 0.21f);
                lr.Fill(6, 6, 6, 6);
            }
            ApplyLook();
        }

        public void SetEnabledLook(bool on)
        {
            if (on == _enabledLook) return;
            _enabledLook = on;
            ApplyLook();
        }

        void ApplyLook()
        {
            bool held = Held;
            var c = _enabledLook ? _color : Color.Lerp(_color, new Color(0.22f, 0.25f, 0.32f), 0.72f);
            _face.color  = held ? c.Lighten(0.22f) : c;
            _icon.color  = _label.color = _enabledLook ? Color.white : Color.white.WithA(0.42f);
            _rim.color   = held ? Pal.Yellow : Color.white.WithA(_enabledLook ? 0.85f : 0.38f);
            if (_glowImg != null)
                _glowImg.color = (_enabledLook ? _color : Color.gray).WithA(held ? 0.55f : 0.24f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            _pointers.Add(e.pointerId);
            _down = true;
        }

        public void OnPointerUp(PointerEventData e) => _pointers.Remove(e.pointerId);

        /// <summary>True once per press (consumed by the reader).</summary>
        public bool TakeDown()
        {
            bool d = _down;
            _down = false;
            return d;
        }

        public void PressFromKey() => _down = true;

        public void Release()
        {
            _pointers.Clear();
            _down   = false;
            KeyHeld = false;
        }

        bool _wasHeld;

        void Update()
        {
            bool held   = Held;
            float target = held ? 0.90f : 1f;
            // Critically-damped spring for snappy feel
            _scale += (target - _scale) * Mathf.Min(1, Time.unscaledDeltaTime * 26f);
            transform.localScale = new Vector3(_scale, _scale, 1);
            if (held != _wasHeld)
            {
                _wasHeld = held;
                ApplyLook();
            }
        }
    }

    /// <summary>Left-thumb steering: two big arrow keys; slide between them without lifting.</summary>
    public sealed class SteerPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // Premium dark glass with slight blue tint
        static readonly Color Idle    = new Color(0.03f, 0.05f, 0.09f, 0.62f);
        static readonly Color Active  = Pal.Orange;

        readonly Dictionary<int, float> _touches = new Dictionary<int, float>();
        RectTransform _rt;
        Key _left, _right;
        public float KeyValue;

        sealed class Key
        {
            public Image Border, Fill, Arrow;
            public RectTransform Rt;
            public bool On;
        }

        public float Value
        {
            get
            {
                float v = 0;
                foreach (var kv in _touches) v += kv.Value;
                v += KeyValue;
                return Mathf.Clamp(v, -1, 1);
            }
        }

        public static SteerPad Create(Transform parent, float w, float h)
        {
            var root = Ui.Node(parent, "SteerPad");
            root.sizeDelta = new Vector2(w, h);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            var pad   = root.gameObject.AddComponent<SteerPad>();
            pad._rt   = root;
            float bw  = (w - 14) * 0.5f;
            pad._left  = MakeKey(root, -1, bw, h);
            pad._right = MakeKey(root,  1, bw, h);
            return pad;
        }

        static Key MakeKey(RectTransform root, int dir, float w, float h)
        {
            var k = new Key();
            // Shadow
            var shadow = Ui.Panel(root, new Color(0, 0, 0, 0.40f), 26, "Shadow");
            shadow.raycastTarget = false;
            shadow.rectTransform.At(dir < 0 ? 0 : 1, 0.5f, 0, -4, w, h);

            // Border + fill panel
            k.Border = Ui.Panel(root, Idle, 26, dir < 0 ? "Left" : "Right", Color.white.WithA(0.40f));
            k.Border.raycastTarget = false;
            k.Rt = k.Border.rectTransform;
            k.Rt.At(dir < 0 ? 0 : 1, 0.5f, 0, 0, w, h);
            k.Fill = k.Rt.Find("Fill").GetComponent<Image>();

            // Top gloss
            var gloss = Ui.Img(k.Fill.transform, Icons.FadeUp, Color.white.WithA(0.10f), "Gloss");
            gloss.preserveAspect = false;
            gloss.rectTransform.Fill();
            gloss.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);

            // Arrow icon
            k.Arrow = Ui.Img(k.Rt, Icons.Arrow(dir), Color.white, "Arrow");
            k.Arrow.rectTransform.At(0.5f, 0.5f, 0, 0, h * 0.62f, h * 0.62f);
            var s = k.Arrow.gameObject.AddComponent<Shadow>();
            s.effectColor    = new Color(0, 0, 0, 0.45f);
            s.effectDistance = new Vector2(2, -2);
            return k;
        }

        float Side(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local);
            return local.x < _rt.rect.center.x ? -1 : 1;
        }

        public void OnPointerDown(PointerEventData e) => _touches[e.pointerId] = Side(e);
        public void OnDrag(PointerEventData e)        => _touches[e.pointerId] = Side(e);
        public void OnPointerUp(PointerEventData e)   => _touches.Remove(e.pointerId);

        public void Release()
        {
            _touches.Clear();
            KeyValue = 0;
        }

        void Update()
        {
            float v = Value;
            Light(_left,  v < -0.1f);
            Light(_right, v > 0.1f);
        }

        static void Light(Key k, bool on)
        {
            if (k == null || on == k.On) return;
            k.On = on;
            // Neon orange fill when active, deep glass when idle
            k.Fill.color   = on ? Active.WithA(0.90f) : Idle;
            k.Border.color = on ? Pal.Orange.WithA(0.80f) : Color.white.WithA(0.38f);
            k.Arrow.color  = on ? Color.white : Color.white.WithA(0.60f);
        }
    }

    /// <summary>Falling confetti for wins and medals — slightly more pieces, wider burst.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        struct Bit
        {
            public RectTransform Rt;
            public Vector2 V;
            public float Spin, Life;
        }

        Bit[] _bits;

        public static Confetti Create(Transform parent)
        {
            var root = Ui.Node(parent, "Confetti").Fill();
            var c = root.gameObject.AddComponent<Confetti>();
            c._bits = new Bit[100];
            for (int i = 0; i < c._bits.Length; i++)
            {
                var img = Ui.Img(root, null, Color.white, "Bit");
                img.rectTransform.sizeDelta = new Vector2(7, 13);
                img.gameObject.SetActive(false);
                c._bits[i].Rt = img.rectTransform;
            }
            return c;
        }

        public void Burst(Color[] colors)
        {
            for (int i = 0; i < _bits.Length; i++)
            {
                ref var b = ref _bits[i];
                b.Rt.gameObject.SetActive(true);
                b.Rt.GetComponent<Image>().color = colors[i % colors.Length];
                b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0.60f);
                b.Rt.anchoredPosition = Vector2.zero;
                float a = Random.Range(0, Mathf.PI * 2);
                float s = Random.Range(180, 560);
                b.V    = new Vector2(Mathf.Cos(a) * s, Mathf.Abs(Mathf.Sin(a)) * s + 130);
                b.Spin = Random.Range(-800, 800);
                b.Life = Random.Range(1.7f, 2.8f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _bits.Length; i++)
            {
                ref var b = ref _bits[i];
                if (b.Life <= 0) continue;
                b.Life -= dt;
                b.V    += new Vector2(0, -660) * dt;
                b.V    *= 1 - dt * 0.55f;
                b.Rt.anchoredPosition += b.V * dt;
                b.Rt.Rotate(0, 0, b.Spin * dt);
                if (b.Life <= 0) b.Rt.gameObject.SetActive(false);
            }
        }
    }
}
