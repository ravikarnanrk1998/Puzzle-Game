using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UA.Core;
using UnityEngine;

namespace UA.Game
{
    /// <summary>
    /// Local save: JsonUtility data, obfuscated and signed with HMAC-SHA256 so an edited or damaged
    /// file is detected (and the backup restored). Writes go to a temp file first and the previous
    /// save is kept as a backup, so a crash mid-write never loses progress.
    /// </summary>
    public static class SaveSystem
    {
        const string Magic = "UAIW1";
        static readonly byte[] Key = Encoding.UTF8.GetBytes("undead-apocalypse::infinite-war::rk-games::v1");
        static SaveData _data;
        static bool _dirty;
        static float _lastWrite;

        public static event Action Saved;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        static string Dir => Application.persistentDataPath;
        static string MainPath => Path.Combine(Dir, "ua_save.dat");
        static string BackupPath => Path.Combine(Dir, "ua_save.bak");
        static string TempPath => Path.Combine(Dir, "ua_save.tmp");

        public static string LastLoadNote { get; private set; } = "";

        public static void Load()
        {
            _data = TryRead(MainPath, out var why);
            if (_data != null) LastLoadNote = "loaded";
            else
            {
                var backup = TryRead(BackupPath, out var why2);
                if (backup != null)
                {
                    _data = backup;
                    LastLoadNote = "restored from backup (" + why + ")";
                    Log.W("Save " + why + "; restored the backup.");
                }
                else
                {
                    _data = NewSave();
                    LastLoadNote = File.Exists(MainPath) ? "reset (" + why + ", backup " + why2 + ")" : "new";
                    if (File.Exists(MainPath)) Log.W("Save unreadable (" + why + "); starting fresh.");
                }
            }
            _data.Repair();
        }

        static SaveData NewSave()
        {
            var d = new SaveData { Created = DateTime.UtcNow.Ticks };
            d.WorldSeed = (long)Rng.Hash(d.Created, Environment.TickCount, SystemInfo.deviceUniqueIdentifier.GetHashCode());
            d.PlayerId = Guid.NewGuid().ToString("N");
            return d;
        }

        /// <summary>Parses save text (also used for cloud copies). Returns null if invalid.</summary>
        public static SaveData Decode(string text, out string why)
        {
            why = "";
            try
            {
                var parts = text.Split(':');
                if (parts.Length != 3 || parts[0] != Magic) { why = "bad header"; return null; }
                var raw = Convert.FromBase64String(parts[1]);
                Xor(raw);
                string json = Encoding.UTF8.GetString(raw);
                if (Sign(json) != parts[2]) { why = "signature mismatch"; return null; }
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

        public static string Encode(SaveData d)
        {
            string json = JsonUtility.ToJson(d);
            var raw = Encoding.UTF8.GetBytes(json);
            Xor(raw);
            return Magic + ":" + Convert.ToBase64String(raw) + ":" + Sign(json);
        }

        static SaveData TryRead(string path, out string why)
        {
            why = "";
            try
            {
                if (!File.Exists(path)) { why = "missing"; return null; }
                return Decode(File.ReadAllText(path, Encoding.UTF8), out why);
            }
            catch (Exception e)
            {
                why = e.GetType().Name;
                return null;
            }
        }

        /// <summary>Marks the save as changed; it is written soon (or on pause / quit).</summary>
        public static void MarkDirty() => _dirty = true;

        public static void FlushIfDirty()
        {
            if (_dirty && Time.unscaledTime - _lastWrite > 0.5f) Save();
        }

        /// <summary>Writes the save atomically (temp file + swap). Returns false if the write failed.</summary>
        public static bool Save()
        {
            if (_data == null) return false;
            _dirty = false;
            _lastWrite = Time.unscaledTime;
            try
            {
                _data.Revision++;
                _data.ModifiedUtc = DateTime.UtcNow.Ticks;
                string text = Encode(_data);
                Directory.CreateDirectory(Dir);
                // Same bytes as File.WriteAllText(.., Encoding.UTF8), but forced to disk before the swap, so a power
                // cut or a killed process right after saving cannot leave an empty or missing save.
                using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(text);
                    sw.Flush();
                    fs.Flush(true);
                }
                if (File.Exists(MainPath))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(MainPath, BackupPath);
                }
                File.Move(TempPath, MainPath);
                Saved?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Log.E("Save failed: " + e.Message);
                _dirty = true;
                return false;
            }
        }

        /// <summary>Replaces the current save (e.g. with a newer cloud copy) and writes it.</summary>
        public static void Replace(SaveData d)
        {
            var keepSettings = _data?.Settings;
            _data = d;
            if (keepSettings != null) _data.Settings = keepSettings;
            _data.Repair();
            Save();
        }

        /// <summary>Deletes all progress (Settings › Reset progress). Purchases are kept as entitlements.</summary>
        public static void ResetAll()
        {
            var old = _data;
            _data = NewSave();
            if (old != null)
            {
                _data.Settings = old.Settings;
                _data.OwnedProducts = old.OwnedProducts;
                _data.ProcessedTx = old.ProcessedTx;
                foreach (var id in old.OwnedProducts) Store.Grant(_data, id, null, true);
                _data.PlayerId = old.PlayerId;
            }
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
