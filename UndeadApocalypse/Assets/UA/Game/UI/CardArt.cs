using System.Collections.Generic;
using UA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UA.Game
{
    /// <summary>
    /// 3D-rendered card art baked in the editor (UA / Make Card Art, Tools\ua.ps1 thumbs) into Resources/Thumbs.
    /// Every getter returns null when an image is missing, and callers then keep the flat procedural icon.
    /// </summary>
    public static class CardArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Thumbs/" + name);
            Cache[name] = s;
            return s;
        }

        /// <summary>Factory-finish side view of the weapon (2:1, transparent background).</summary>
        public static Sprite Weapon(WeaponId id) => Load("W_" + id);

        /// <summary>Boss head-and-shoulders portrait (528 x 320, opaque).</summary>
        public static Sprite Boss(BossType t) => Load("B_" + t);

        /// <summary>
        /// Weapon art if baked, else the weapon-class icon. Not-owned weapons are darkened (the flat icon used a
        /// light grey tint, which would wash out a rendered gun).
        /// </summary>
        public static Image WeaponIcon(Transform parent, WeaponDef w, bool dim, string name = "Icon")
        {
            var art = Weapon(w.Id);
            if (art == null) return Ui.Img(parent, Sprites.Of(Sprites.ClassIcon(w.Class)), dim ? Pal.Dim : Color.white, name);
            var img = Ui.Img(parent, art, dim ? new Color(0.42f, 0.44f, 0.48f, 0.9f) : Color.white, name);
            img.preserveAspect = true;
            return img;
        }
    }
}
