using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KPL.Game
{
    /// <summary>Modal dialogs and toasts above the screens.</summary>
    public static class Popup
    {
        static readonly List<GameObject> Stack = new List<GameObject>();

        public static RectTransform Open(string title, float w, float h, bool closeButton = true, Color? edge = null)
        {
            var root = Ui.Node(GameApp.I.PopupRoot, "Popup " + title).Fill();
            var dim = Ui.Img(root, null, new Color(0.01f, 0.02f, 0.06f, 0.72f), "Dim");
            dim.raycastTarget = true;
            dim.rectTransform.Fill();
            var card = Ui.Card(root, "Card", edge ?? Pal.Accent.A(0.4f), 22);
            card.rectTransform.At(0.5f, 0.5f, 0, 0, w, h);
            Enter.Add(card, 0, 20);
            if (!string.IsNullOrEmpty(title))
            {
                var t = Ui.Label(card.transform, title, 34, Color.white, TextAnchor.MiddleLeft, Font.Display);
                t.rectTransform.TopStrip(14, 44, 26, 70);
            }
            if (closeButton)
            {
                var x = Ui.IconBtn(card.transform, Icon.Cross, () => Close(root.gameObject), 48);
                x.Rt().At(1, 1, -12, -12, 48, 48);
            }
            Stack.Add(root.gameObject);
            Audio.Play(Sfx.Pop, 0.6f);
            return card.rectTransform;
        }

        public static void Close(GameObject go)
        {
            Stack.Remove(go);
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        public static bool CloseTop()
        {
            Stack.RemoveAll(g => g == null);
            if (Stack.Count == 0) return false;
            Close(Stack[Stack.Count - 1]);
            return true;
        }

        public static void CloseAll()
        {
            foreach (var g in Stack) if (g != null) UnityEngine.Object.Destroy(g);
            Stack.Clear();
        }

        public static bool Any => Stack.Exists(g => g != null);

        public static void Confirm(string title, string body, string ok, Action onOk, string cancel = "CANCEL")
        {
            var c = Open(title, 620, 300, false);
            var p = Ui.Para(c, body, 20, Pal.Text);
            p.rectTransform.Fill(28, 72, 28, 100);
            var root = c.parent.gameObject;
            var no = Ui.Btn(c, cancel, BtnStyle.Ghost, () => Close(root), 22);
            no.Rt().At(0, 0, 26, 24, 220, 58);
            var yes = Ui.Btn(c, ok, BtnStyle.Primary, () =>
            {
                Close(root);
                onOk();
            }, 24);
            yes.Rt().At(1, 0, -26, 24, 240, 58);
        }

        public static void Message(string title, string body, string ok = "OK", Action then = null)
        {
            var c = Open(title, 620, 300, false);
            var p = Ui.Para(c, body, 20, Pal.Text);
            p.rectTransform.Fill(28, 72, 28, 100);
            var root = c.parent.gameObject;
            var b = Ui.Btn(c, ok, BtnStyle.Primary, () =>
            {
                Close(root);
                then?.Invoke();
            }, 24);
            b.Rt().At(0.5f, 0, 0, 24, 240, 58);
        }

        public static void Toast(string text, Icon icon = Icon.Info, Color? color = null)
        {
            var bg = Ui.Panel(GameApp.I.TopRoot, Pal.Hex(0x0B1426, 0.95f), 16, "Toast", (color ?? Pal.Gold).A(0.6f));
            bg.raycastTarget = false;
            bg.rectTransform.At(0.5f, 1, 0, -18, 640, 56);
            var ic = Ui.IconImg(bg.transform, icon, color ?? Pal.Gold, 30);
            ic.rectTransform.At(0, 0.5f, 16, 0, 30, 30);
            var t = Ui.Label(bg.transform, text, 20, Color.white, TextAnchor.MiddleLeft, Font.Bold);
            t.rectTransform.Fill(58, 0, 16, 0);
            Enter.Add(bg, 0, -14);
            UnityEngine.Object.Destroy(bg.gameObject, 2.6f);
        }
    }
}
