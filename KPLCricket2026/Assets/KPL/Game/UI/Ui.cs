using System;
using System.Collections.Generic;
using KPL.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>KPL visual identity: deep navy glass panels, saffron accent, gold highlights.</summary>
    public static class Pal
    {
        public static readonly Color Ink = Hex(0x060A14);
        public static readonly Color Panel = Hex(0x0E1626, 0.94f);
        public static readonly Color Panel2 = Hex(0x15223A, 0.96f);
        public static readonly Color Panel3 = Hex(0x1D2D4A, 1f);
        public static readonly Color Stroke = new Color(1, 1, 1, 0.09f);
        public static readonly Color Accent = Hex(0xFF7A1A);
        public static readonly Color AccentDeep = Hex(0xD9480F);
        public static readonly Color Gold = Hex(0xFFC94A);
        public static readonly Color GoldDeep = Hex(0xC98A12);
        public static readonly Color Teal = Hex(0x19C3B1);
        public static readonly Color Sky = Hex(0x4FA8FF);
        public static readonly Color Red = Hex(0xFF4A5C);
        public static readonly Color Green = Hex(0x2FD07A);
        public static readonly Color Violet = Hex(0x8F6BFF);
        public static readonly Color Text = Hex(0xF3F6FB);
        public static readonly Color Dim = Hex(0x97A6BD);
        public static readonly Color Mute = Hex(0x5E6B80);

        public static Color Hex(uint rgb, float a = 1) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        public static Color Argb(uint argb) => new Color(((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, 1);
        public static Color A(this Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static float Luma(this Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        public static Color Mul(this Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static string Rgb(this Color c) => ColorUtility.ToHtmlStringRGB(c);
    }

    public enum Font { Display, Bold, Semi, Body }

    public enum BtnStyle { Primary, Gold, Secondary, Ghost, Danger, Teal }

    public static class Ui
    {
        static UnityEngine.Font _display, _bold, _semi, _body;

        public static UnityEngine.Font F(Font f)
        {
            switch (f)
            {
                case Font.Display: return _display ? _display : (_display = Resources.Load<UnityEngine.Font>("Fonts/BarlowCondensed-ExtraBold"));
                case Font.Bold: return _bold ? _bold : (_bold = Resources.Load<UnityEngine.Font>("Fonts/BarlowCondensed-Bold"));
                case Font.Semi: return _semi ? _semi : (_semi = Resources.Load<UnityEngine.Font>("Fonts/BarlowCondensed-SemiBold"));
                default: return _body ? _body : (_body = Resources.Load<UnityEngine.Font>("Fonts/Barlow-Medium"));
            }
        }

        // ------------------------------------------------------------ layout

        public static RectTransform Node(Transform parent, string name = "Node")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchors at (ax, ay) of the parent with the same pivot, offset (x, y), size (w, h).</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Stretches horizontally between anchors with insets, fixed height at the top.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float y, float h, float l = 0, float r = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(l, -y - h);
            rt.offsetMax = new Vector2(-r, -y);
            return rt;
        }

        public static RectTransform Rt(this Component c) => (RectTransform)c.transform;

        // ------------------------------------------------------------ visuals

        public static Image Img(Transform parent, Sprite s, Color c, string name = "Img")
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            if (s != null && s.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, Color c, int radius = 18, string name = "Panel", Color? outline = null)
        {
            var img = Img(parent, radius > 0 ? Sprites.Round(radius) : null, c, name);
            img.raycastTarget = true;
            if (outline != null)
            {
                var o = Img(img.transform, Sprites.Outline(radius, 2), outline.Value, "Outline");
                o.rectTransform.Fill();
            }
            return img;
        }

        /// <summary>Glassy card: dark panel, faint top highlight and an outline.</summary>
        public static Image Card(Transform parent, string name = "Card", Color? edge = null, int radius = 20)
        {
            var p = Panel(parent, Pal.Panel, radius, name, edge ?? Pal.Stroke);
            var hl = Img(p.transform, Sprites.VGradient(0, 0.06f), Color.white, "Sheen");
            hl.rectTransform.Fill(2, 2, 2, 2);
            hl.transform.SetSiblingIndex(0);
            return p;
        }

        public static Image IconImg(Transform parent, Icon i, Color c, float size)
        {
            var img = Img(parent, Sprites.Of(i), c, "Icon" + i);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft, Font f = Font.Bold)
        {
            var rt = Node(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = F(f);
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Para(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.UpperLeft, Font f = Font.Body)
        {
            var t = Label(parent, text, size, c, align, f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.1f;
            return t;
        }

        public static void Shadow(Graphic g, float d = 2, float a = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectDistance = new Vector2(d, -d);
            s.effectColor = new Color(0, 0, 0, a);
        }

        public static void Outline(Graphic g, float d = 1.5f, float a = 0.7f)
        {
            var s = g.gameObject.AddComponent<UnityEngine.UI.Outline>();
            s.effectDistance = new Vector2(d, -d);
            s.effectColor = new Color(0, 0, 0, a);
        }

        // ------------------------------------------------------------ buttons

        public static Button Btn(Transform parent, string label, BtnStyle style, Action onClick, int size = 24, Icon? icon = null, string sub = null)
        {
            Color bg, fg, edge;
            switch (style)
            {
                case BtnStyle.Primary: bg = Pal.Accent; fg = Color.white; edge = Pal.Hex(0xFFB26B, 0.9f); break;
                case BtnStyle.Gold: bg = Pal.Gold; fg = Pal.Hex(0x2A1A00); edge = Pal.Hex(0xFFE6A3); break;
                case BtnStyle.Teal: bg = Pal.Teal; fg = Pal.Hex(0x02201C); edge = Pal.Hex(0x9BF1E6); break;
                case BtnStyle.Danger: bg = Pal.Red; fg = Color.white; edge = Pal.Hex(0xFF9AA4); break;
                case BtnStyle.Ghost: bg = new Color(1, 1, 1, 0.06f); fg = Pal.Text; edge = new Color(1, 1, 1, 0.18f); break;
                default: bg = Pal.Panel3; fg = Pal.Text; edge = new Color(1, 1, 1, 0.14f); break;
            }
            var rt = Node(parent, "Btn " + label);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprites.Round(16);
            img.type = Image.Type.Sliced;
            img.color = bg;
            if (style == BtnStyle.Primary || style == BtnStyle.Gold || style == BtnStyle.Teal || style == BtnStyle.Danger)
            {
                var sheen = Img(rt, Sprites.VGradient(0, 0.28f), Color.white, "Sheen");
                sheen.rectTransform.Fill(2, 2, 2, 2);
                var shade = Img(rt, Sprites.VGradient(0.22f, 0f), Color.black, "Shade");
                shade.rectTransform.Fill(2, 2, 2, 2);
            }
            var o = Img(rt, Sprites.Outline(16, 2), edge, "Edge");
            o.rectTransform.Fill();
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<Pressable>();
            b.onClick.AddListener(() =>
            {
                Audio.Play(style == BtnStyle.Ghost && label.Contains("BACK") ? Sfx.Back : Sfx.Click);
                onClick?.Invoke();
            });
            var content = Node(rt, "Content").Fill(10, 0, 10, 0);
            float textX = 0;
            if (icon != null)
            {
                var ic = IconImg(content, icon.Value, fg, size * 1.05f);
                if (string.IsNullOrEmpty(label)) ic.rectTransform.At(0.5f, 0.5f, 0, 0, size * 1.2f, size * 1.2f);
                else
                {
                    ic.rectTransform.At(0, 0.5f, 6, sub != null ? 0 : 0, size * 1.05f, size * 1.05f);
                    textX = size * 1.05f + 10;
                }
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(content, label, size, fg, icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Font.Display);
                if (sub != null)
                {
                    t.rectTransform.anchorMin = new Vector2(0, 0.42f);
                    t.rectTransform.anchorMax = new Vector2(1, 1);
                    t.rectTransform.offsetMin = new Vector2(textX, 0);
                    t.rectTransform.offsetMax = new Vector2(0, -2);
                    var st = Label(content, sub, Mathf.Max(12, size / 2), fg.A(0.8f), icon != null ? TextAnchor.UpperLeft : TextAnchor.UpperCenter, Font.Semi);
                    st.rectTransform.anchorMin = new Vector2(0, 0);
                    st.rectTransform.anchorMax = new Vector2(1, 0.42f);
                    st.rectTransform.offsetMin = new Vector2(textX, 2);
                    st.rectTransform.offsetMax = Vector2.zero;
                }
                else
                {
                    t.rectTransform.Fill(textX, 0, 0, 0);
                }
            }
            return b;
        }

        public static Button IconBtn(Transform parent, Icon icon, Action onClick, float size = 56, Color? tint = null, BtnStyle style = BtnStyle.Ghost)
        {
            var b = Btn(parent, "", style, onClick, (int)(size * 0.46f), icon);
            if (tint != null)
            {
                foreach (var img in b.GetComponentsInChildren<Image>())
                {
                    if (img.name.StartsWith("Icon")) img.color = tint.Value;
                }
            }
            b.Rt().sizeDelta = new Vector2(size, size);
            return b;
        }

        public static void SetInteractable(Button b, bool on)
        {
            b.interactable = on;
            var g = b.GetComponent<CanvasGroup>() ?? b.gameObject.AddComponent<CanvasGroup>();
            g.alpha = on ? 1 : 0.45f;
        }

        /// <summary>Segmented control: one option highlighted.</summary>
        public static RectTransform Segments(Transform parent, string[] options, int selected, Action<int> pick, int size = 18, Color? accent = null)
        {
            var root = Node(parent, "Segments");
            var bg = Img(root, Sprites.Round(14), new Color(1, 1, 1, 0.06f), "Bg");
            bg.rectTransform.Fill();
            var lay = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.childForceExpandWidth = true;
            lay.childForceExpandHeight = true;
            lay.spacing = 4;
            lay.padding = new RectOffset(3, 3, 3, 3);
            var ac = accent ?? Pal.Accent;
            var btns = new List<(Image bg, Text t)>();
            void Refresh(int sel)
            {
                for (int k = 0; k < btns.Count; k++)
                {
                    btns[k].bg.color = k == sel ? ac : new Color(0, 0, 0, 0);
                    btns[k].t.color = k == sel ? (ac.Luma() > 0.6f ? Pal.Ink : Color.white) : Pal.Dim;
                }
            }
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var rt = Node(root, options[i]);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Sprites.Round(12);
                img.type = Image.Type.Sliced;
                var t = Label(rt, options[i], size, Pal.Dim, TextAnchor.MiddleCenter, Font.Bold);
                t.rectTransform.Fill();
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() =>
                {
                    Audio.Play(Sfx.Click);
                    Refresh(idx);
                    pick(idx);
                });
                btns.Add((img, t));
            }
            bg.transform.SetAsFirstSibling();
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Refresh(selected);
            return root;
        }

        public static Slider SliderRow(Transform parent, float value, Action<float> changed)
        {
            var rt = Node(parent, "Slider");
            var bg = Img(rt, Sprites.Round(8), new Color(1, 1, 1, 0.1f), "Track");
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 10);
            var fillArea = Node(rt, "FillArea");
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 10);
            var fill = Img(fillArea, Sprites.Round(8), Pal.Accent, "Fill");
            fill.rectTransform.Fill();
            var handleArea = Node(rt, "HandleArea").Fill(12, 0, 12, 0);
            var handle = Img(handleArea, Sprites.Circle, Color.white, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(30, 30);
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = value;
            s.onValueChanged.AddListener(v => changed(v));
            return s;
        }

        /// <summary>Vertical scrolling list; returns the content node (add children with LayoutElement heights).</summary>
        public static RectTransform ScrollList(Transform parent, float spacing = 8, RectOffset pad = null)
        {
            var view = Node(parent, "Scroll");
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var content = Node(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var lay = content.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.spacing = spacing;
            lay.padding = pad ?? new RectOffset(0, 0, 4, 12);
            lay.childForceExpandHeight = false;
            lay.childControlHeight = true;
            lay.childControlWidth = true;
            lay.childForceExpandWidth = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30;
            sr.decelerationRate = 0.12f;
            return content;
        }

        /// <summary>Horizontal scrolling strip.</summary>
        public static RectTransform HScroll(Transform parent, float spacing = 12)
        {
            var view = Node(parent, "HScroll");
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var content = Node(view, "Content");
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var lay = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = spacing;
            lay.childForceExpandHeight = true;
            lay.childForceExpandWidth = false;
            lay.childControlWidth = true;
            lay.childControlHeight = true;
            lay.padding = new RectOffset(4, 24, 0, 0);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.vertical = false;
            sr.scrollSensitivity = 30;
            return content;
        }

        public static RectTransform Row(Transform list, float height, string name = "Row")
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
        }

        public static RectTransform Col(Transform list, float width, string name = "Col")
        {
            var rt = Node(list, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            return rt;
        }

        // ------------------------------------------------------------ chips

        public static RectTransform Pill(Transform parent, string text, Color bg, Color fg, int size = 16, Icon? icon = null)
        {
            var p = Panel(parent, bg, 14, "Pill");
            var rt = p.rectTransform;
            float x = 10;
            if (icon != null)
            {
                var ic = IconImg(rt, icon.Value, fg, size + 4);
                ic.rectTransform.At(0, 0.5f, 8, 0, size + 4, size + 4);
                x = size + 16;
            }
            var t = Label(rt, text, size, fg, TextAnchor.MiddleLeft, Font.Bold);
            t.rectTransform.Fill(x, 0, 8, 0);
            return rt;
        }

        public static string Coins(int n) => n.ToString("N0");

        public static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);
    }

    /// <summary>Squash on press, spring back on release.</summary>
    public sealed class Pressable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1, _s = 1;
        public float Depth = 0.94f;

        public void OnPointerDown(PointerEventData e) => _target = Depth;
        public void OnPointerUp(PointerEventData e) => _target = 1;
        public void OnPointerExit(PointerEventData e) => _target = 1;

        void Update()
        {
            if (Mathf.Abs(_s - _target) < 0.001f) return;
            _s = Mathf.Lerp(_s, _target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 22));
            transform.localScale = new Vector3(_s, _s, 1);
        }
    }

    /// <summary>Screen entrance: fade in and slide up a little.</summary>
    public sealed class Enter : MonoBehaviour
    {
        CanvasGroup _g;
        float _t;
        public float Delay;
        public float Dy = 18;
        Vector2 _base;
        RectTransform _rt;

        void Awake()
        {
            _g = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _g.alpha = 0;
            _rt = (RectTransform)transform;
        }

        void Start()
        {
            _base = _rt.anchoredPosition;
            Step();
        }

        void Update() => Step();

        void Step()
        {
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((_t - Delay) / 0.28f);
            float e = 1 - Mathf.Pow(1 - k, 3);
            _g.alpha = e;
            _rt.anchoredPosition = _base + new Vector2(0, -Dy * (1 - e));
            if (k >= 1) Destroy(this);
        }

        public static T Add<T>(T c, float delay, float dy = 18) where T : Component
        {
            var e = c.gameObject.AddComponent<Enter>();
            e.Delay = delay;
            e.Dy = dy;
            return c;
        }
    }

    /// <summary>Gentle pulse for calls to action.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.04f, Speed = 2.4f;

        void Update()
        {
            float s = 1 + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            transform.localScale = new Vector3(s, s, 1);
        }
    }

    /// <summary>Spins a UI element (loading rings).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = -240;
        void Update() => transform.Rotate(0, 0, Speed * Time.unscaledDeltaTime);
    }
}
