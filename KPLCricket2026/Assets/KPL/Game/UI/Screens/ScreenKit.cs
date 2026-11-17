using System;
using System.Collections;
using KPL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Shared chrome for menu screens: header with back button and title, coins pill, scrims.</summary>
    public static class ScreenKit
    {
        public static RectTransform Page(string name, string title, string sub, Action back, float dim = 0.55f)
        {
            var page = Nav.Page(name, back);
            Scrim(page, dim);
            Header(page, title, sub, back);
            return page;
        }

        public static void Scrim(RectTransform page, float dim)
        {
            var d = Ui.Img(page, null, Pal.Hex(0x040811, dim), "Dim");
            d.rectTransform.Fill();
            var top = Ui.Img(page, Sprites.VGradient(0, 0.85f), Pal.Hex(0x040811), "TopFade");
            top.rectTransform.TopStrip(0, 140);
            var bottom = Ui.Img(page, Sprites.VGradient(0.75f, 0), Pal.Hex(0x040811), "BottomFade");
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0);
            bottom.rectTransform.sizeDelta = new Vector2(0, 160);
        }

        public static void Header(RectTransform page, string title, string sub, Action back)
        {
            if (back != null)
            {
                var b = Ui.IconBtn(page, Icon.Back, back, 56);
                b.Rt().At(0, 1, 20, -16, 56, 56);
            }
            float x = back != null ? 92 : 28;
            var t = Ui.Label(page, title, 44, Color.white, TextAnchor.MiddleLeft, Font.Display);
            t.rectTransform.At(0, 1, x, -10, 760, 52);
            Ui.Shadow(t, 2, 0.6f);
            if (!string.IsNullOrEmpty(sub))
            {
                var s = Ui.Label(page, sub, 17, Pal.Gold, TextAnchor.MiddleLeft, Font.Semi);
                s.rectTransform.At(0, 1, x + 2, -58, 760, 22);
            }
            CoinPill(page);
        }

        public static Text CoinPill(RectTransform page, bool tapToShop = true)
        {
            var pill = Ui.Panel(page, Pal.Hex(0x0B1426, 0.92f), 20, "Coins", Pal.Gold.A(0.45f));
            pill.rectTransform.At(1, 1, -20, -18, 190, 50);
            var ic = Ui.IconImg(pill.transform, Icon.Coin, Pal.Gold, 32);
            ic.rectTransform.At(0, 0.5f, 10, 0, 32, 32);
            var t = Ui.Label(pill.transform, Ui.Coins(SaveSystem.Data.Coins), 24, Color.white, TextAnchor.MiddleLeft, Font.Display);
            t.rectTransform.Fill(50, 0, 10, 0);
            var live = t.gameObject.AddComponent<LiveCoins>();
            live.Label = t;
            if (tapToShop)
            {
                var b = pill.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => ShopScreen.Show(0));
                pill.gameObject.AddComponent<Pressable>();
            }
            return t;
        }

        /// <summary>A section label with a short accent bar.</summary>
        public static Text Section(Transform parent, string text, float x, float y, float w = 400)
        {
            var bar = Ui.Img(parent, Sprites.Round(3), Pal.Accent, "Bar");
            bar.rectTransform.At(0, 1, x, y - 4, 4, 18);
            var t = Ui.Label(parent, text, 17, Pal.Dim, TextAnchor.MiddleLeft, Font.Bold);
            t.rectTransform.At(0, 1, x + 12, y, w, 26);
            return t;
        }

        /// <summary>Animated counter for coin rewards.</summary>
        public static IEnumerator CountUp(Text t, int from, int to, float seconds)
        {
            float k = 0;
            while (k < 1)
            {
                k += Time.unscaledDeltaTime / seconds;
                t.text = Ui.Coins(Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, k))));
                yield return null;
            }
            t.text = Ui.Coins(to);
        }
    }

    /// <summary>Keeps a coins label in sync with the save.</summary>
    public sealed class LiveCoins : MonoBehaviour
    {
        public Text Label;
        int _shown = -1;

        void Update()
        {
            int c = SaveSystem.Data.Coins;
            if (c == _shown) return;
            _shown = c;
            Label.text = Ui.Coins(c);
        }
    }
}
