using System;
using System.Collections.Generic;
using PCC.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCC.Game
{
    public static class Pal
    {
        public static readonly Color Bg0 = new Color32(0x03, 0x04, 0x0B, 255);
        public static readonly Color Bg1 = new Color32(0x08, 0x0C, 0x24, 255);
        public static readonly Color Card = new Color32(0x0B, 0x10, 0x30, 0xE6);
        public static readonly Color Stroke = new Color(1, 1, 1, 0.18f);
        public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x4A, 255);
        public static readonly Color GoldLight = new Color32(0xFF, 0xE9, 0xA3, 255);
        public static readonly Color GoldDeep = new Color32(0xFF, 0x8A, 0x00, 255);
        public static readonly Color Cyan = new Color32(0x38, 0xE8, 0xFF, 255);
        public static readonly Color Violet = new Color32(0x8B, 0x5C, 0xFF, 255);
        public static readonly Color Pink = new Color32(0xFF, 0x3D, 0x9A, 255);
        public static readonly Color Green = new Color32(0x2E, 0xE5, 0x9D, 255);
        public static readonly Color Red = new Color32(0xFF, 0x4D, 0x5E, 255);
        public static readonly Color Text = new Color32(0xF3, 0xF5, 0xFF, 255);
        public static readonly Color Dim = new Color32(0xB4, 0xBC, 0xE0, 255);
        public static readonly Color Mute = new Color32(0x6E, 0x77, 0xA0, 255);
    }

    /// <summary>Code-built UGUI helpers (no prefabs needed).</summary>
    public static class Ui
    {
        public static Font Display => TextBaker.Font("Teko-Bold");
        public static Font Body => TextBaker.Font("Rajdhani-Bold");

        static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
        static Sprite _gloss, _circle, _glow;

        /// <summary>9-sliced rounded rectangle sprite.</summary>
        public static Sprite Round(int radius)
        {
            if (Rounded.TryGetValue(radius, out var s)) return s;
            int size = radius * 2 + 4;
            var px = new Px(size, size, new Color32(255, 255, 255, 0));
            px.RoundRect(0, 0, size - 1, size - 1, radius, Color.white, Color.white);
            var tex = px.ToTexture("Round" + radius, false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            Rounded[radius] = s;
            return s;
        }

        /// <summary>Soft top highlight for buttons.</summary>
        public static Sprite Gloss
        {
            get
            {
                if (_gloss != null) return _gloss;
                var px = new Px(4, 64, new Color32(255, 255, 255, 0));
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 4; x++)
                    px.P[y * 4 + x] = new Color(1, 1, 1, Mathf.Clamp01((y - 30) / 34f) * 0.32f);
                var t = px.ToTexture("Gloss", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0);
                _gloss = Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
                return _gloss;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                var px = new Px(128, 128, new Color32(255, 255, 255, 0));
                px.Circle(64, 64, 62, Color.white);
                _circle = Sprite.Create(px.ToTexture("Circle", false, TextureWrapMode.Clamp, FilterMode.Bilinear, 0), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
                return _circle;
            }
        }

        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                var t = Tex.Glow(128);
                _glow = Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
                return _glow;
            }
        }

        // ------------------------------------------------------------ layout

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Fills the parent with insets (left, top, right, bottom).</summary>
        public static RectTransform Fill(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Anchors at (ax, ay) of the parent (0..1), pivot the same, offset and size in reference px.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Stretches between two anchors with fixed height.</summary>
        public static RectTransform Band(this RectTransform rt, float ax0, float ax1, float ay, float y, float h, float inset = 0)
        {
            rt.anchorMin = new Vector2(ax0, ay);
            rt.anchorMax = new Vector2(ax1, ay);
            rt.pivot = new Vector2(0.5f, ay);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-inset * 2, h);
            return rt;
        }

        // ------------------------------------------------------------ widgets

        public static Image Panel(Transform parent, Color color, int radius = 16, string name = "Panel", Color? border = null)
        {
            var rt = Node(parent, name);
            if (border != null)
            {
                var b = rt.gameObject.AddComponent<Image>();
                b.sprite = Round(radius);
                b.type = Image.Type.Sliced;
                b.color = border.Value;
                var inner = Node(rt, "Fill").Fill(1.5f, 1.5f, 1.5f, 1.5f);
                var img = inner.gameObject.AddComponent<Image>();
                img.sprite = Round(radius);
                img.type = Image.Type.Sliced;
                img.color = color;
                img.raycastTarget = false;
                return b;
            }
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = Round(radius);
            i.type = Image.Type.Sliced;
            i.color = color;
            return i;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            var rt = Node(parent, name);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.preserveAspect = true;
            i.raycastTarget = false;
            return i;
        }

        public static Text Label(Transform parent, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool display = true, string name = "Label")
        {
            var rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = display ? Display : Body;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Wrapping paragraph text.</summary>
        public static Text Para(Transform parent, string text, float size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = Label(parent, text, size, color, anchor, false, "Para");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static void Shadow(Graphic g, float d = 2, float a = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectDistance = new Vector2(d, -d);
            s.effectColor = new Color(0, 0, 0, a);
        }

        /// <summary>Glossy gradient button with a press-scale animation.</summary>
        public static Button Button(Transform parent, string label, Color a, Color b, Action onClick, float fontSize = 24, string subtitle = null, int radius = 14)
        {
            var bg = Panel(parent, a, radius, "Button", Color.Lerp(a, Color.white, 0.35f));
            var rt = (RectTransform)bg.transform;
            var fill = rt.Find("Fill").GetComponent<Image>();
            fill.color = Color.Lerp(a, b, 0.5f);
            var grad = Node(rt.Find("Fill"), "Grad").Fill();
            var gi = grad.gameObject.AddComponent<Image>();
            gi.sprite = Gloss;
            gi.color = Color.white;
            gi.raycastTarget = false;
            var bottom = Node(rt.Find("Fill"), "Shade").Fill();
            bottom.anchorMax = new Vector2(1, 0.5f);
            var bi = bottom.gameObject.AddComponent<Image>();
            bi.color = b.WithA(0.55f);
            bi.raycastTarget = false;
            var fg = a.Luma() > 0.62f ? new Color(0.1f, 0.07f, 0.02f) : Color.white;
            var txt = Label(rt, label, fontSize, fg, TextAnchor.MiddleCenter, true, "Text");
            txt.rectTransform.Fill(6, subtitle != null ? 2 : 0, 6, subtitle != null ? fontSize * 0.45f : 0);
            if (subtitle != null)
            {
                var st = Label(rt, subtitle, fontSize * 0.42f, fg.WithA(0.85f), TextAnchor.LowerCenter, false, "Sub");
                st.rectTransform.Fill(6, 0, 6, 4);
            }
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.6f);
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                Sfx.I?.Click();
                onClick?.Invoke();
            });
            bg.gameObject.AddComponent<PressScale>();
            return btn;
        }

        public static Button Ghost(Transform parent, string label, Color color, Action onClick, float fontSize = 20)
        {
            // Opaque dark fill: the border image is a full rounded rect underneath.
            var bg = Panel(parent, new Color(0.05f, 0.07f, 0.19f, 0.94f), 12, "Ghost", color.WithA(0.75f));
            var txt = Label(bg.transform, label, fontSize, color, TextAnchor.MiddleCenter, true, "Text");
            txt.rectTransform.Fill(6, 0, 6, 0);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                Sfx.I?.Click();
                onClick?.Invoke();
            });
            bg.gameObject.AddComponent<PressScale>();
            return btn;
        }

        /// <summary>Segmented selector (overs, difficulty...).</summary>
        public static List<Image> Segments(Transform parent, string[] labels, int selected, Action<int> onPick, float fontSize = 20)
        {
            var list = new List<Image>();
            int n = labels.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var p = Panel(parent, Color.white, 12, "Seg" + i, Pal.Stroke);
                var rt = (RectTransform)p.transform;
                rt.anchorMin = new Vector2(i / (float)n, 0);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                rt.offsetMin = new Vector2(3, 0);
                rt.offsetMax = new Vector2(-3, 0);
                var t = Label(rt, labels[i], fontSize, Pal.Text, TextAnchor.MiddleCenter, true, "Text");
                t.rectTransform.Fill();
                var b = p.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() =>
                {
                    Sfx.I?.Click();
                    onPick(idx);
                    Highlight(list, idx);
                });
                p.gameObject.AddComponent<PressScale>();
                list.Add(p);
            }
            Highlight(list, selected);
            return list;
        }

        static void Highlight(List<Image> segs, int selected)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                bool on = i == selected;
                var fill = segs[i].transform.Find("Fill").GetComponent<Image>();
                fill.color = on ? Pal.Gold : new Color(0.07f, 0.09f, 0.22f, 0.95f);
                segs[i].transform.Find("Text").GetComponent<Text>().color = on ? new Color(0.1f, 0.07f, 0.02f) : Pal.Text;
            }
        }

        /// <summary>
        /// Vertical scrolling list (drag or wheel). Position the returned viewport, then add rows to
        /// <paramref name="content"/> with <see cref="Row"/> and set its height with <see cref="SetContentHeight"/>.
        /// </summary>
        public static RectTransform ScrollList(Transform parent, out RectTransform content, string name = "Scroll")
        {
            var view = Node(parent, name);
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            content = Node(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;
            sr.inertia = true;
            return view;
        }

        /// <summary>A full-width row of the given height at <paramref name="top"/> px from the top of a list.</summary>
        public static RectTransform Row(RectTransform content, float top, float height, string name = "Row")
        {
            var r = Node(content, name);
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -top);
            r.sizeDelta = new Vector2(0, height);
            return r;
        }

        public static void SetContentHeight(RectTransform content, float height) => content.sizeDelta = new Vector2(0, height);

        /// <summary>Horizontal rating bar (0-99) with its value.</summary>
        public static void Bar(Transform parent, string label, int value, Color color, float x, float y, float width)
        {
            var l = Label(parent, label, 13, Pal.Dim, TextAnchor.MiddleLeft, false);
            l.rectTransform.At(0, 1, x, y, 60, 16);
            var bg = Img(parent, Round(4), new Color(1, 1, 1, 0.1f), "BarBg");
            bg.type = Image.Type.Sliced;
            bg.preserveAspect = false;
            bg.rectTransform.At(0, 1, x + 58, y - 4, width - 96, 9);
            var fill = Img(bg.transform, Round(4), color, "Fill");
            fill.type = Image.Type.Sliced;
            fill.preserveAspect = false;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(Mathf.Clamp01(value / 99f), 1);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            var v = Label(parent, value.ToString(), 17, Pal.Text, TextAnchor.MiddleRight);
            v.rectTransform.At(0, 1, x + width - 34, y + 1, 34, 18);
        }

        /// <summary>Single-line text box (opens the phone keyboard); <paramref name="onDone"/> runs when editing ends.</summary>
        public static InputField TextField(Transform parent, string value, string placeholder, float fontSize, int maxLength, Action<string> onDone)
        {
            var bg = Panel(parent, new Color(0.02f, 0.03f, 0.1f, 0.97f), 8, "Field", Pal.Cyan.WithA(0.45f));
            var text = Label(bg.transform, "", fontSize, Pal.Text, TextAnchor.MiddleLeft, false, "Text");
            text.rectTransform.Fill(10, 0, 10, 0);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Label(bg.transform, placeholder, fontSize, Pal.Mute, TextAnchor.MiddleLeft, false, "Placeholder");
            ph.rectTransform.Fill(10, 0, 10, 0);
            var input = bg.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = maxLength;
            input.lineType = InputField.LineType.SingleLine;
            input.text = value;
            input.caretColor = Pal.Gold;
            input.selectionColor = Pal.Cyan.WithA(0.35f);
            input.onEndEdit.AddListener(v => onDone?.Invoke(v));
            return input;
        }

        public static string Ord(int n)
        {
            int m = n % 100;
            if (m >= 11 && m <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }
    }

    /// <summary>Shrinks a control slightly while pressed.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1;

        public void OnPointerDown(PointerEventData e) => _target = 0.95f;
        public void OnPointerUp(PointerEventData e) => _target = 1;
        public void OnPointerExit(PointerEventData e) => _target = 1;

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, _target, Time.unscaledDeltaTime * 18);
            transform.localScale = new Vector3(s, s, 1);
        }
    }

    /// <summary>Procedural franchise crests (original shield designs, no real logos).</summary>
    public static class Crest
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Of(TeamInfo team)
        {
            if (Cache.TryGetValue(team.Id, out var s)) return s;
            const int w = 112, h = 126;
            var px = new Px(w, h, new Color32(0, 0, 0, 0));
            var p = Mats.Hex(team.Primary);
            var sec = Mats.Hex(team.Secondary);
            var rim = sec.Luma() < 0.2f ? Pal.Gold : sec;
            // Shield: top straight, sides curve to a point at the bottom.
            bool Inside(float x, float y, float inset)
            {
                float u = x / w, v = 1 - y / h; // v: 0 top, 1 bottom
                if (v < inset / h + 0.06f) return false;
                float half = v < 0.55f ? 0.46f : 0.46f * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((v - 0.55f) / 0.45f, 2)));
                half -= inset / w;
                return Mathf.Abs(u - 0.5f) < half && v < 1 - inset / h;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!Inside(x + 0.5f, y + 0.5f, 0)) continue;
                bool inner = Inside(x + 0.5f, y + 0.5f, 5);
                float v = 1 - y / (float)h;
                Color c;
                if (!inner) c = rim;
                else
                {
                    c = Color.Lerp(p.Lighten(0.25f), p.Darken(0.4f), v);
                    // Diagonal sash.
                    float d = (x / (float)w) * 0.35f + v;
                    if (d > 0.62f && d < 0.74f) c = Color.Lerp(c, rim, 0.8f);
                    if (v < 0.48f) c = Color.Lerp(c, Color.white, (0.48f - v) * 0.35f);
                }
                c.a = 1;
                px.P[y * w + x] = c;
            }
            for (int i = 0; i < team.Titles; i++)
            {
                float cx = w * 0.5f + (i - (team.Titles - 1) / 2f) * 12;
                px.Circle(cx, h * 0.8f, 3.2f, Pal.GoldLight);
            }
            var fg = p.Luma() > 0.7f ? new Color(0.1f, 0.1f, 0.15f) : Color.white;
            TextBaker.Draw(px, team.Id, w * 0.5f, h * 0.36f, 36, fg);
            var tex = px.ToTexture("Crest" + team.Id, true, TextureWrapMode.Clamp, FilterMode.Trilinear, 1);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            Cache[team.Id] = s;
            return s;
        }

        public static Image Add(Transform parent, TeamInfo team, float size, bool glow = false)
        {
            var root = Ui.Node(parent, "Crest " + team.Id);
            root.sizeDelta = new Vector2(size, size * 1.12f);
            if (glow)
            {
                var g = Ui.Img(root, Ui.Glow, Mats.Hex(team.Glow).WithA(0.55f), "Glow");
                g.rectTransform.At(0.5f, 0.5f, 0, 0, size * 2f, size * 2f);
            }
            var img = Ui.Img(root, Of(team), Color.white, "Shield");
            img.rectTransform.Fill();
            return img;
        }
    }
}
