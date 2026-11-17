using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KPL.Core;
using UnityEngine;

namespace KPL.Game
{
    /// <summary>
    /// Local save: JsonUtility data, lightly obfuscated and signed with an HMAC so a damaged or
    /// edited file is detected. Writes go to a temp file first and the previous save is kept as a
    /// backup, so a crash mid-write never loses progress.
    /// </summary>
    public static class SaveSystem
    {
        const string Magic = "KPL1";
        static readonly byte[] Key = Encoding.UTF8.GetBytes("kpl-2026::c0v3r-dr1v3::rk-games");
        static SaveData _data;
        static bool _dirty;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        static string Dir => Application.persistentDataPath;
        static string MainPath => Path.Combine(Dir, "kpl_save.dat");
        static string BackupPath => Path.Combine(Dir, "kpl_save.bak");
        static string TempPath => Path.Combine(Dir, "kpl_save.tmp");

        public static string LastLoadNote { get; private set; } = "";

        public static void Load()
        {
            _data = TryRead(MainPath, out var why);
            if (_data != null)
            {
                LastLoadNote = "loaded";
            }
            else
            {
                var backup = TryRead(BackupPath, out var why2);
                if (backup != null)
                {
                    _data = backup;
                    LastLoadNote = "restored from backup (" + why + ")";
                    Debug.LogWarning("[KPL] Save " + why + "; restored the backup.");
                }
                else
                {
                    _data = new SaveData { Created = DateTime.UtcNow.Ticks };
                    LastLoadNote = File.Exists(MainPath) ? "reset (" + why + ", backup " + why2 + ")" : "new";
                    if (File.Exists(MainPath)) Debug.LogWarning("[KPL] Save unreadable (" + why + "); starting fresh.");
                }
            }
            _data.Repair();
        }

        static SaveData TryRead(string path, out string why)
        {
            why = "";
            try
            {
                if (!File.Exists(path))
                {
                    why = "missing";
                    return null;
                }
                string text = File.ReadAllText(path, Encoding.UTF8);
                var parts = text.Split(':');
                if (parts.Length != 3 || parts[0] != Magic)
                {
                    why = "bad header";
                    return null;
                }
                var raw = Convert.FromBase64String(parts[1]);
                Xor(raw);
                string json = Encoding.UTF8.GetString(raw);
                if (Sign(json) != parts[2])
                {
                    why = "signature mismatch";
                    return null;
                }
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null) why = "empty";
                return d;
            }
            catch (Exception e)
            {
                why = e.GetType().Name;
                return null;
            }
        }

        /// <summary>Marks the save as changed; it is written at the end of the frame (or on pause / quit).</summary>
        public static void MarkDirty() => _dirty = true;

        public static void FlushIfDirty()
        {
            if (_dirty) Save();
        }

        public static void Save()
        {
            if (_data == null) return;
            _dirty = false;
            try
            {
                string json = JsonUtility.ToJson(_data);
                var raw = Encoding.UTF8.GetBytes(json);
                Xor(raw);
                string text = Magic + ":" + Convert.ToBase64String(raw) + ":" + Sign(json);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(TempPath, text, Encoding.UTF8);
                if (File.Exists(MainPath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(MainPath, BackupPath);
                }
                File.Move(TempPath, MainPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[KPL] Save failed: " + e.Message);
            }
        }

        /// <summary>Deletes all progress (Settings › Reset progress).</summary>
        public static void ResetAll()
        {
            var keepSettings = _data?.Settings;
            _data = new SaveData { Created = DateTime.UtcNow.Ticks };
            if (keepSettings != null) _data.Settings = keepSettings;
            _data.Repair();
            Save();
        }

        static void Xor(byte[] b)
        {
            for (int i = 0; i < b.Length; i++) b[i] ^= (byte)(Key[i % Key.Length] + i * 31);
        }

        static string Sign(string json)
        {
            using (var h = new HMACSHA256(Key))
            {
                var mac = h.ComputeHash(Encoding.UTF8.GetBytes(json));
                var sb = new StringBuilder(64);
                foreach (var x in mac) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
