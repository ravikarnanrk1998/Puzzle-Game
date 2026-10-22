using System.Collections.Generic;
using UnityEngine;

namespace FRF
{
    public enum SteerMode { Buttons, Tilt }

    /// <summary>Rendering cost: LOW for older / hot phones, MEDIUM default, HIGH for flagships.</summary>
    public enum GraphicsQuality { Low, Medium, High }

    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Player options, stored in PlayerPrefs.</summary>
    public static class Settings
    {
        static int Get(string k, int d) => PlayerPrefs.GetInt("frf." + k, d);
        static void Set(string k, int v) => PlayerPrefs.SetInt("frf." + k, v);

        public static SteerMode Steer
        {
            get => (SteerMode)Get("steer", (int)SteerMode.Buttons);
            set => Set("steer", (int)value);
        }

        /// <summary>0 low, 1 medium, 2 high tilt sensitivity.</summary>
        public static int TiltSensitivity
        {
            get => Get("tilt", 1);
            set => Set("tilt", value);
        }

        public static Difficulty Difficulty
        {
            get => (Difficulty)Get("difficulty", (int)Difficulty.Normal);
            set => Set("difficulty", (int)value);
        }

        public static bool Sound
        {
            get => Get("sound", 1) == 1;
            set => Set("sound", value ? 1 : 0);
        }

        public static bool Music
        {
            get => Get("music", 1) == 1;
            set => Set("music", value ? 1 : 0);
        }

        public static bool Vibration
        {
            get => Get("vibrate", 1) == 1;
            set => Set("vibrate", value ? 1 : 0);
        }

        /// <summary>Camera distance: false = close chase, true = far chase.</summary>
        public static bool FarCamera
        {
            get => Get("farcam", 0) == 1;
            set => Set("farcam", value ? 1 : 0);
        }

        static int _gfx = -1;

        public static GraphicsQuality Graphics
        {
            get
            {
                if (_gfx < 0) _gfx = Get("gfx", (int)GraphicsQuality.Medium);
                return (GraphicsQuality)_gfx;
            }
            set
            {
                _gfx = (int)value;
                Set("gfx", _gfx);
            }
        }

        /// <summary>60 FPS in races (smoother, warmer phone). Off = 30 FPS battery saver.</summary>
        public static bool Smooth60
        {
            get => Get("fps60", 0) == 1;
            set => Set("fps60", value ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void Buzz()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Vibration) Handheld.Vibrate();
#endif
        }
    }

    /// <summary>Career progress, money, garage. Stored in PlayerPrefs.</summary>
    public static class Profile
    {
        public const int StartCash = 1500;

        public static int Cash
        {
            get => PlayerPrefs.GetInt("frf.cash", StartCash);
            set => PlayerPrefs.SetInt("frf.cash", Mathf.Max(0, value));
        }

        public static string RiderId
        {
            get => PlayerPrefs.GetString("frf.rider", "kai");
            set => PlayerPrefs.SetString("frf.rider", value);
        }

        public static string BikeId
        {
            get => PlayerPrefs.GetString("frf.bike", "scout");
            set => PlayerPrefs.SetString("frf.bike", value);
        }

        /// <summary>Index into <see cref="Catalog.Paints"/> (0 = factory colour).</summary>
        public static int Paint
        {
            get => PlayerPrefs.GetInt("frf.paint", 0);
            set => PlayerPrefs.SetInt("frf.paint", value);
        }

        public static RiderDef Rider => Catalog.Rider(RiderId);
        public static BikeDef Bike => Catalog.Bike(BikeId);
        public static Color PaintColor => Catalog.Paints[Mathf.Clamp(Paint, 0, Catalog.Paints.Length - 1)];

        public static bool Owns(BikeDef b) => b.Price == 0 || PlayerPrefs.GetInt("frf.own." + b.Id, 0) == 1;

        public static bool Buy(BikeDef b)
        {
            if (Owns(b)) return true;
            if (Cash < b.Price) return false;
            Cash -= b.Price;
            PlayerPrefs.SetInt("frf.own." + b.Id, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int Medal(int eventIndex) => PlayerPrefs.GetInt("frf.medal." + eventIndex, 0);

        public static void SetMedal(int eventIndex, int medal)
        {
            if (medal > Medal(eventIndex)) PlayerPrefs.SetInt("frf.medal." + eventIndex, medal);
        }

        /// <summary>Events are unlocked in order: each one opens when the previous has a medal.</summary>
        public static bool Unlocked(int eventIndex) => eventIndex == 0 || Medal(eventIndex - 1) > 0;

        public static int MedalCount(int kind)
        {
            int n = 0;
            for (int i = 0; i < Catalog.Career.Length; i++)
                if (Medal(i) == kind) n++;
            return n;
        }

        public static float BestTime(string trackId) => PlayerPrefs.GetFloat("frf.best." + trackId, 0);

        public static bool SubmitTime(string trackId, float t)
        {
            float b = BestTime(trackId);
            if (b > 0 && b <= t) return false;
            PlayerPrefs.SetFloat("frf.best." + trackId, t);
            return true;
        }

        public static int BestTricks(string trackId) => PlayerPrefs.GetInt("frf.tricks." + trackId, 0);

        public static void SubmitTricks(string trackId, int pts)
        {
            if (pts > BestTricks(trackId)) PlayerPrefs.SetInt("frf.tricks." + trackId, pts);
        }

        public static int Races
        {
            get => PlayerPrefs.GetInt("frf.races", 0);
            set => PlayerPrefs.SetInt("frf.races", value);
        }

        /// <summary>Clears career, money and garage (settings stay).</summary>
        public static void Reset()
        {
            var keys = new List<string> { "frf.cash", "frf.rider", "frf.bike", "frf.paint", "frf.races" };
            foreach (var b in Catalog.Bikes) keys.Add("frf.own." + b.Id);
            for (int i = 0; i < Catalog.Career.Length; i++) keys.Add("frf.medal." + i);
            foreach (var t in Catalog.Tracks)
            {
                keys.Add("frf.best." + t.Id);
                keys.Add("frf.tricks." + t.Id);
            }
            foreach (var k in keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
    }
}
