using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>
    /// The KPL brand: a white batter silhouette over an orange-gold swoosh with the KPL wordmark.
    /// Built from UI pieces so it can be shown in menus or baked into textures for boards and screens.
    /// </summary>
    public static class Brand
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Adds the logo (swoosh + batter + KPL) to a UI parent, sized by height.</summary>
        public static RectTransform Logo(Transform parent, float h, bool sub = true)
        {
            var root = Ui.Node(parent, "KplLogo");
            root.sizeDelta = new Vector2(h * 2.3f, h);
            var swoosh = Ui.Img(root, Sprites.Ring(12), Pal.Accent, "Swoosh");
            swoosh.type = Image.Type.Filled;
            swoosh.fillMethod = Image.FillMethod.Radial360;
            swoosh.fillOrigin = (int)Image.Origin360.Bottom;
            swoosh.fillAmount = 0.42f;
            swoosh.rectTransform.At(0, 0.5f, -h * 0.05f, -h * 0.02f, h * 1.65f, h * 0.78f);
            swoosh.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
            var swoosh2 = Ui.Img(root, Sprites.Ring(6), Pal.Gold, "Swoosh2");
            swoosh2.type = Image.Type.Filled;
            swoosh2.fillMethod = Image.FillMethod.Radial360;
            swoosh2.fillOrigin = (int)Image.Origin360.Bottom;
            swoosh2.fillAmount = 0.36f;
            swoosh2.rectTransform.At(0, 0.5f, h * 0.02f, -h * 0.08f, h * 1.55f, h * 0.68f);
            swoosh2.rectTransform.localRotation = Quaternion.Euler(0, 0, -26);
            var batter = Ui.Img(root, Sprites.Of(Icon.Batter), Color.white, "Batter");
            batter.rectTransform.At(0, 0.5f, h * 0.06f, h * 0.02f, h * 0.95f, h * 0.95f);
            Ui.Shadow(batter, h * 0.02f, 0.45f);
            var k = Ui.Label(root, "KPL", Mathf.RoundToInt(h * 0.62f), Color.white, TextAnchor.MiddleLeft, Font.Display);
            k.rectTransform.At(0, 0.5f, h * 0.88f, sub ? h * 0.08f : 0, h * 1.5f, h * 0.7f);
            Ui.Shadow(k, h * 0.025f, 0.5f);
            if (sub)
            {
                var c = Ui.Label(root, "CRICKET 2026", Mathf.RoundToInt(h * 0.15f), Pal.Gold, TextAnchor.MiddleLeft, Font.Bold);
                c.rectTransform.At(0, 0.5f, h * 0.92f, -h * 0.3f, h * 1.4f, h * 0.2f);
            }
            return root;
        }

        /// <summary>Full-frame logo artwork for the giant screens.</summary>
        public static Texture2D ScreenArt()
        {
            if (Cache.TryGetValue("screen", out var t) && t != null) return t;
            int w = Quality.Current >= Gfx.High ? 1024 : 512, h = w / 2;
            t = TextBake.Render(w, h, Pal.Hex(0x061033), area =>
            {
                var g = Ui.Img(area, Sprites.VGradient(0, 1), Pal.Hex(0x1440A8), "Grad");
                g.rectTransform.Fill();
                var glow = Ui.Img(area, Sprites.Glow, Pal.Accent.A(0.35f), "Glow");
                glow.rectTransform.At(0.5f, 0.5f, 0, 0, w * 0.9f, h * 1.6f);
                var stripes = Ui.Img(area, Sprites.Stripes, new Color(1, 1, 1, 0.35f), "Stripes");
                stripes.type = Image.Type.Tiled;
                stripes.rectTransform.Fill();
                var logo = Logo(area, h * 0.62f);
                logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.5f);
                logo.pivot = new Vector2(0.5f, 0.5f);
                logo.anchoredPosition = new Vector2(-w * 0.02f, h * 0.02f);
            });
            Cache["screen"] = t;
            return t;
        }

        static readonly (string Name, uint Bg, uint Fg, int Kind)[] Boards =
        {
            ("KPL", 0xFF0B2A78, 0xFFFFFFFF, 0), ("VOLTARA", 0xFF0F1A12, 0xFF7CFF6B, 2), ("CRICKET", 0xFFD7192A, 0xFFFFFFFF, 1), ("SKYNOVA", 0xFF0F6BD8, 0xFFFFFFFF, 2),
            ("KPL", 0xFF0B2A78, 0xFFFFFFFF, 0), ("ZESTIQ", 0xFFFF7A1A, 0xFF101010, 2), ("CRICKET", 0xFFD7192A, 0xFFFFFFFF, 1), ("PACEPRO", 0xFF4B2A85, 0xFFFFC94A, 2),
        };

        /// <summary>Strip of eight board designs (KPL logo, CRICKET and fictional sponsors), each 6.4:1 like a perimeter board.</summary>
        public static Texture2D BoardStrip()
        {
            if (Cache.TryGetValue("boards", out var t) && t != null) return t;
            int cw = Quality.Current >= Gfx.High ? 512 : 256, ch = cw * 10 / 64, w = cw * 8;
            t = TextBake.Render(w, ch, Color.black, area =>
            {
                for (int i = 0; i < Boards.Length; i++)
                {
                    var (name, bg, fg, kind) = Boards[i];
                    var cell = Ui.Node(area, "B" + i);
                    cell.anchorMin = new Vector2(i / 8f, 0);
                    cell.anchorMax = new Vector2((i + 1) / 8f, 1);
                    cell.offsetMin = cell.offsetMax = Vector2.zero;
                    var b = Ui.Img(cell, Sprites.HGradient(1, 1), Pal.Argb(bg), "Bg");
                    b.rectTransform.Fill();
                    var shade = Ui.Img(cell, Sprites.VGradient(0.3f, 0), Color.black, "Shade");
                    shade.rectTransform.Fill();
                    if (kind == 0 || kind == 1)
                    {
                        var sw = Ui.Img(cell, Sprites.HGradient(0, 0.9f), kind == 0 ? Pal.Accent : Pal.Gold, "Swoosh");
                        sw.rectTransform.anchorMin = new Vector2(0, 0.1f);
                        sw.rectTransform.anchorMax = new Vector2(0.55f, 0.24f);
                        sw.rectTransform.offsetMin = sw.rectTransform.offsetMax = Vector2.zero;
                        var bat = Ui.Img(cell, Sprites.Of(Icon.Batter), Color.white, "Batter");
                        bat.preserveAspect = true;
                        bat.rectTransform.anchorMin = new Vector2(0.08f, 0.04f);
                        bat.rectTransform.anchorMax = new Vector2(0.3f, 0.96f);
                        bat.rectTransform.offsetMin = bat.rectTransform.offsetMax = Vector2.zero;
                    }
                    var label = Ui.Label(cell, name, ch, Pal.Argb(fg), TextAnchor.MiddleCenter, Font.Display);
                    label.rectTransform.anchorMin = new Vector2(kind == 2 ? 0.04f : 0.3f, 0.06f);
                    label.rectTransform.anchorMax = new Vector2(0.96f, 0.94f);
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 6;
                    label.resizeTextMaxSize = (int)(ch * 0.82f);
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    var edge = Ui.Img(cell, null, new Color(0, 0, 0, 0.8f), "Edge");
                    edge.rectTransform.anchorMin = new Vector2(0.995f, 0);
                    edge.rectTransform.anchorMax = Vector2.one;
                    edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
                }
            });
            t.wrapMode = TextureWrapMode.Repeat;
            Cache["boards"] = t;
            return t;
        }
    }
}
