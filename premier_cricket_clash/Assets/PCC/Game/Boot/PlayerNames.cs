using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PCC.Core;
using UnityEngine;

namespace PCC.Game
{
    /// <summary>
    /// Player renames made on the EDIT NAMES screen. Stored only on this device and applied
    /// everywhere through <see cref="Player.NameOverride"/>.
    /// </summary>
    public static class PlayerNames
    {
        const string Key = "pcc.names";
        public const int MaxLength = 24;
        static readonly Dictionary<int, string> Map = new Dictionary<int, string>();

        public static void Init()
        {
            Map.Clear();
            foreach (var line in PlayerPrefs.GetString(Key, "").Split('\n'))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0 && int.TryParse(line.Substring(0, tab), out int id)) Map[id] = line.Substring(tab + 1);
            }
            Player.NameOverride = id => Map.TryGetValue(id, out var n) ? n : null;
        }

        public static int Count => Map.Count;
        public static bool IsRenamed(int id) => Map.ContainsKey(id);

        /// <summary>Renames a player; an empty name (or the original) restores the original.</summary>
        public static void Set(int id, string name)
        {
            name = Clean(name);
            var p = PlayerDb.Instance.ById(id);
            if (string.IsNullOrEmpty(name) || name == p.OriginalName) Map.Remove(id);
            else Map[id] = name;
            Save();
        }

        public static void ResetAll()
        {
            Map.Clear();
            Save();
        }

        static string Clean(string s)
        {
            s = Regex.Replace(s ?? "", @"[\t\r\n|]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length > MaxLength ? s.Substring(0, MaxLength).Trim() : s;
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, string.Join("\n", Map.Select(e => e.Key + "\t" + e.Value)));
            PlayerPrefs.Save();
        }
    }
}
