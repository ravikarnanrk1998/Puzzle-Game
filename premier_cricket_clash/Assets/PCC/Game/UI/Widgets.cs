using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCC.Game
{
    /// <summary>Full-screen touch surface behind the HUD: swipes, taps and aim drags on the 3D scene.</summary>
    public sealed class TouchLayer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Down;
        public Action<Vector2> Drag;
        public Action<Vector2, bool> Up;
        Vector2 _start;
        float _startTime;
        bool _moved;

        public void OnPointerDown(PointerEventData e)
        {
            _start = e.position;
            _startTime = Time.unscaledTime;
            _moved = false;
            Down?.Invoke(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if ((e.position - _start).magnitude > 18) _moved = true;
            Drag?.Invoke(e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            bool tap = !_moved && Time.unscaledTime - _startTime < 0.35f;
            Up?.Invoke(e.position, tap);
        }
    }

    /// <summary>Virtual thumbstick (batting direction in BUTTONS mode).</summary>
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Value { get; private set; }
        RectTransform _knob, _rt;
        float _radius;

        public static Joystick Create(Transform parent, float size)
        {
            var bg = Ui.Img(parent, Ui.Circle, new Color(0.04f, 0.06f, 0.16f, 0.6f), "Joystick");
            bg.raycastTarget = true;
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var ring = Ui.Img(bg.transform, Ui.Circle, Pal.Cyan.WithA(0.18f), "Ring");
            ring.rectTransform.Fill(6, 6, 6, 6);
            var j = bg.gameObject.AddComponent<Joystick>();
            j._rt = bg.rectTransform;
            j._radius = size * 0.32f;
            foreach (var (label, x, y) in new[] { ("STRAIGHT", 0f, 0.36f), ("OFF", -0.36f, 0f), ("LEG", 0.36f, 0f), ("BEHIND", 0f, -0.36f) })
            {
                var t = Ui.Label(bg.transform, label, 11, Pal.Dim, TextAnchor.MiddleCenter, false);
                t.rectTransform.At(0.5f, 0.5f, x * size, y * size, 80, 16);
            }
            var knob = Ui.Img(bg.transform, Ui.Circle, Color.white, "Knob");
            knob.rectTransform.At(0.5f, 0.5f, 0, 0, size * 0.36f, size * 0.36f);
            j._knob = knob.rectTransform;
            var glow = Ui.Img(knob.transform, Ui.Glow, Pal.Cyan.WithA(0.7f), "Glow");
            glow.rectTransform.At(0.5f, 0.5f, 0, 0, size * 0.6f, size * 0.6f);
            glow.transform.SetAsFirstSibling();
            return j;
        }

        /// <summary>
        /// Off / leg labels as seen on the broadcast view: a right-hander's off side is
        /// screen left; left-handers flip.
        /// </summary>
        public void SetLeftHanded(bool left)
        {
            foreach (Transform c in transform)
            {
                var t = c.GetComponent<Text>();
                if (t == null) continue;
                if (t.text == "OFF" || t.text == "LEG")
                {
                    var rt = t.rectTransform;
                    bool isOff = t.text == "OFF";
                    float x = Mathf.Abs(rt.anchoredPosition.x) * ((isOff ^ left) ? -1 : 1);
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                }
            }
        }

        void Set(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local);
            var v = Vector2.ClampMagnitude(local / _radius, 1);
            Value = v;
            _knob.anchoredPosition = v * _radius;
        }

        public void OnPointerDown(PointerEventData e) => Set(e);
        public void OnDrag(PointerEventData e) => Set(e);

        public void OnPointerUp(PointerEventData e)
        {
            Value = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
        }

        /// <summary>Screen angle of the stick (0 = up / straight), or null when centred.</summary>
        public double? Angle => Value.magnitude < 0.3f ? (double?)null : Math.Atan2(Value.x, Value.y) * 180 / Math.PI;
    }

    /// <summary>Falling confetti for boundaries, fifties and wins.</summary>
    public sealed class Confetti : MonoBehaviour
    {
        struct Bit
        {
            public RectTransform Rt;
            public Vector2 V;
            public float Spin, Life;
        }

        Bit[] _bits;
        RectTransform _rt;

        public static Confetti Create(Transform parent)
        {
            var root = Ui.Node(parent, "Confetti").Fill();
            var c = root.gameObject.AddComponent<Confetti>();
            c._rt = root;
            c._bits = new Bit[90];
            for (int i = 0; i < c._bits.Length; i++)
            {
                var img = Ui.Img(root, null, Color.white, "Bit");
                img.rectTransform.sizeDelta = new Vector2(7, 12);
                img.gameObject.SetActive(false);
                c._bits[i].Rt = img.rectTransform;
            }
            return c;
        }

        public void Burst(Color[] colors)
        {
            var size = _rt.rect.size;
            for (int i = 0; i < _bits.Length; i++)
            {
                ref var b = ref _bits[i];
                b.Rt.gameObject.SetActive(true);
                b.Rt.GetComponent<Image>().color = colors[i % colors.Length];
                b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(0.5f, 0.6f);
                b.Rt.anchoredPosition = Vector2.zero;
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                float s = UnityEngine.Random.Range(160, 520);
                b.V = new Vector2(Mathf.Cos(a) * s, Mathf.Abs(Mathf.Sin(a)) * s + 120);
                b.Spin = UnityEngine.Random.Range(-720, 720);
                b.Life = UnityEngine.Random.Range(1.6f, 2.6f);
            }
            _ = size;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _bits.Length; i++)
            {
                ref var b = ref _bits[i];
                if (b.Life <= 0) continue;
                b.Life -= dt;
                b.V += new Vector2(0, -620) * dt;
                b.V *= 1 - dt * 0.6f;
                b.Rt.anchoredPosition += b.V * dt;
                b.Rt.Rotate(0, 0, b.Spin * dt);
                if (b.Life <= 0) b.Rt.gameObject.SetActive(false);
            }
        }
    }
}
