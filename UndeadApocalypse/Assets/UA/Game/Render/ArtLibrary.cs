using System.Collections.Generic;
using UnityEngine;

namespace UA.Game
{
    /// <summary>
    /// Imported (third-party, free-licensed) art prefabs under Resources/UAArt. Every lookup can return null,
    /// in which case callers build the original procedural model, so the procedural assets remain the
    /// fallback for anything not yet imported (or when imported art is switched off for comparison).
    /// Licences and sources are recorded in Assets/UA/Art/ASSET_SOURCES.md.
    /// </summary>
    public static class ArtLibrary
    {
        public static bool Enabled = true;
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static GameObject Get(string key)
        {
            if (!Enabled) return null;
            if (Cache.TryGetValue(key, out var go)) return go;
            go = Resources.Load<GameObject>("UAArt/" + key);
            Cache[key] = go;
            return go;
        }
    }
}
