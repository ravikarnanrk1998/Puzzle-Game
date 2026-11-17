using System;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// The store: premium weapons and bundles, gem packs, coin packs, survival packs, cosmetics and
    /// boosters. Real-money items go through Google Play Billing (<see cref="Iap"/>); gem / coin
    /// offers are spent in-game after a confirmation.
    /// </summary>
    public sealed class StoreScreen : UiScreen
    {
        public override string Title => "STORE";
        public int Section;
        RectTransform _body;

        public override void Build(RectTransform root)
        {
            _body = Ui.Rect(root, "Body");
            _body.Fill();
            Iap.OnChanged(Refresh);
            Rebuild();
        }

        public override void OnHide() => Iap.RemoveChanged(Refresh);

        void Refresh() { if (_body != null) Rebuild(); }

        void Rebuild()
        {
            Ui.Clear(_body);
            Screens.RefreshTopBar();
            Icon[] icons = { Icon.Rifle, Icon.Gem, Icon.Coin, Icon.Supply, Icon.Star, Icon.Bolt };
            for (int i = 0; i < Store.Sections.Length; i++)
            {
                int s = i;
                var b = Ui.Button(_body, Store.SectionTitles[i], () => { Section = s; Rebuild(); }, i == Section ? Pal.Accent : Pal.Panel2, 28, icons[i]);
                b.Bg.rectTransform.At(0, 1, 190 + i * 300, -40, 290, 70);
            }
            var content = Ui.Scroll(_body, out var scroll, true);
            scroll.GetComponent<RectTransform>().Fill(40, 100, 40, 110);
            float x = 0;
            string sec = Store.Sections[Section];
            // Real-money products in this section.
            foreach (var p in Store.Products)
            {
                if (p.Section != sec) continue;
                x = ProductCard(content, x, p);
            }
            // Gem-priced weapons in the weapon section.
            if (sec == "weapons")
                foreach (var w in Weapons.All)
                    if (w.PriceCurrency == Currency.Gems) x = WeaponCard(content, x, w);
            // In-game offers.
            foreach (var o in Store.Offers)
            {
                bool show = sec == "boosters" ? o.BoostCoins + o.BoostDamage + o.BoostXp > 0 || o.ExtraSlot || o.Grenades > 0 : sec == "coins" && o.Coins > 0;
                if (show) x = OfferCard(content, x, o);
            }
            if (sec == "cosmetics")
            {
                x = LinkCard(content, x, "WEAPON SKINS", "Woodland, desert, digital, arctic, blood red, carbon, toxic, gold and dragon camos for every weapon.", Icon.Rifle, () => Screens.Show(new ArmoryScreen()));
                x = LinkCard(content, x, "OUTFITS & GLOVES", "Ranger, Urban Ops, Desert Storm, Black Ops, Arctic and Hazmat sleeves, plus five glove styles.", Icon.Survivor, () => Screens.Show(new UpgradesScreen()));
            }
            content.sizeDelta = new Vector2(x, 0);
            // Footer.
            var status = Ui.Label(_body, Iap.Available ? "Payments are processed securely by Google Play." : Iap.Status, 24, Iap.Available ? Pal.Dim : Pal.Accent2, TextAnchor.MiddleLeft, FontKind.Semi);
            status.rectTransform.At(0, 0, 640, 50, 1200, 60);
            var restore = Ui.Button(_body, "RESTORE PURCHASES", () => Iap.Restore(), Pal.Panel2, 26, Icon.Restore);
            restore.Bg.rectTransform.At(1, 0, -230, 54, 400, 74);
        }

        static RectTransform Card(RectTransform content, float x, string title, string desc, string badge, Icon icon, Color iconCol, bool owned, Sprite art = null)
        {
            var card = Ui.Panel(content, Pal.Panel.A(0.94f), "Card", true, false);
            var rt = card.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(x, 0);
            rt.offsetMax = new Vector2(x + 400, 0);
            var glow = Ui.Img(rt, Sprites.Glow, iconCol.A(0.25f), "Glow");
            glow.rectTransform.At(0.5f, 1, 0, -170, 360, 300);
            if (art != null)
            {
                // Rendered weapon (2:1) across the card instead of the flat class icon.
                var a = Ui.Img(rt, art, Color.white, "Art");
                a.preserveAspect = true;
                a.rectTransform.At(0.5f, 1, 0, -170, 372, 186);
            }
            else
            {
                var ic = Ui.Img(rt, Sprites.Of(icon), iconCol, "Icon");
                ic.rectTransform.At(0.5f, 1, 0, -170, 170, 170);
            }
            var t = Ui.Label(rt, title, 38, Pal.Text, TextAnchor.UpperCenter, FontKind.Display);
            t.rectTransform.Fill(16, 300, 16, 0);
            var ds = Ui.Label(rt, desc, 24, Pal.Dim, TextAnchor.UpperCenter, FontKind.Body);
            ds.rectTransform.Fill(28, 352, 28, 130);
            if (!string.IsNullOrEmpty(badge))
            {
                var bb = Ui.Img(rt, Sprites.Cut(8), Pal.Bad, "Badge");
                bb.rectTransform.At(0.5f, 1, 0, -24, 240, 46);
                var bt = Ui.Label(bb.transform, badge, 24, Color.white, TextAnchor.MiddleCenter, FontKind.Display);
                bt.rectTransform.Fill();
            }
            if (owned)
            {
                var o = Ui.Label(rt, "OWNED", 36, Pal.Good, TextAnchor.LowerCenter, FontKind.Display);
                o.rectTransform.Fill(0, 0, 0, 50);
            }
            return rt;
        }

        float ProductCard(RectTransform content, float x, ProductDef p)
        {
            bool owned = Store.IsOwned(SaveSystem.Data, p);
            Icon icon = p.Section == "gems" ? Icon.Gem : p.Section == "coins" ? Icon.Coin : p.Weapons.Length > 0 ? Sprites.ClassIcon(Weapons.Get(p.Weapons[0]).Class) : p.CoinDoubler ? Icon.Coin : p.ExtraSlot ? Icon.Plus : p.Skins.Length > 0 ? Icon.Star : Icon.Supply;
            Color col = p.Section == "gems" ? Pal.Gem : p.Section == "coins" || p.CoinDoubler ? Pal.Coin : p.Id == "pack_legendary" ? Pal.Accent2 : Pal.Accent;
            var rt = Card(content, x, p.Title, p.Desc, p.Badge, icon, col, owned, p.Section != "gems" && p.Section != "coins" && p.Weapons.Length > 0 ? CardArt.Weapon(p.Weapons[0]) : null);
            if (!owned)
            {
                var b = Ui.Button(rt, Iap.Price(p), () => Iap.Buy(p), Iap.Available ? Pal.Good.Mul(0.85f) : Pal.Panel2, 36);
                b.Bg.rectTransform.At(0.5f, 0, 0, 62, 320, 88);
            }
            return x + 420;
        }

        float WeaponCard(RectTransform content, float x, WeaponDef w)
        {
            var d = SaveSystem.Data;
            bool owned = d.Owns(w.Id);
            var rt = Card(content, x, w.Name.ToUpper(), w.Desc + "\nUnlocks at level " + w.UnlockLevel + ".", w.Rarity >= 3 ? "LEGENDARY" : "", Sprites.ClassIcon(w.Class), Pal.Accent2, owned, CardArt.Weapon(w.Id));
            if (!owned)
            {
                var b = Ui.Button(rt, w.Price + " GEMS", () => Screens.Confirm("BUY " + w.Name.ToUpper() + "?", "Spend " + w.Price + " gems on the " + w.Name + ".", "BUY", () =>
                {
                    if (Wallet.BuyWeapon(d, w.Id)) { Sfx.Buy(); SaveSystem.MarkDirty(); Screens.Toast(w.Name + " unlocked"); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast(d.Level < w.UnlockLevel ? "Reach level " + w.UnlockLevel : "Not enough gems"); }
                }), Pal.Gem.Mul(0.6f), 34, Icon.Gem);
                b.Bg.rectTransform.At(0.5f, 0, 0, 62, 320, 88);
            }
            return x + 420;
        }

        float OfferCard(RectTransform content, float x, GemOffer o)
        {
            var d = SaveSystem.Data;
            bool owned = o.ExtraSlot && d.ExtraSlots > 0;
            Icon icon = o.Coins > 0 ? Icon.Coin : o.BoostDamage > 0 ? Icon.Fire : o.BoostXp > 0 ? Icon.Star : o.Grenades > 0 ? Icon.Grenade : o.ExtraSlot ? Icon.Plus : Icon.Coin;
            var rt = Card(content, x, o.Title, o.Desc, "", icon, o.Coins > 0 ? Pal.Coin : Pal.Toxic, owned);
            if (!owned)
            {
                bool gems = o.Price == Currency.Gems;
                var b = Ui.Button(rt, o.Cost.ToString("#,0") + (gems ? " GEMS" : " COINS"), () => Screens.Confirm(o.Title, "Spend " + o.Cost.ToString("#,0") + (gems ? " gems" : " coins") + "?", "BUY", () =>
                {
                    if (Store.BuyOffer(d, o)) { Sfx.Buy(); SaveSystem.MarkDirty(); Screens.Toast(o.Title + " added"); Rebuild(); }
                    else { Sfx.Error(); Screens.Toast(o.Grenades > 0 && d.Grenades >= o.Grenades ? "Grenades already full" : "Not enough " + (gems ? "gems" : "coins")); }
                }), gems ? Pal.Gem.Mul(0.6f) : Pal.Coin.Mul(0.7f), 34, gems ? Icon.Gem : Icon.Coin);
                b.Bg.rectTransform.At(0.5f, 0, 0, 62, 320, 88);
            }
            return x + 420;
        }

        float LinkCard(RectTransform content, float x, string title, string desc, Icon icon, Action go)
        {
            var rt = Card(content, x, title, desc, "", icon, Pal.Accent2, false);
            var b = Ui.Button(rt, "BROWSE", go, Pal.Panel2, 34, Icon.Play);
            b.Bg.rectTransform.At(0.5f, 0, 0, 62, 320, 88);
            return x + 420;
        }
    }
}
