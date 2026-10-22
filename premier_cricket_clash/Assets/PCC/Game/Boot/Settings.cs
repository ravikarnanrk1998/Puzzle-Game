using PCC.Core;
using UnityEngine;

namespace PCC.Game
{
    public enum BattingControls { Swipe, Buttons }

    public enum MatchTime { Auto, Day, Night }

    /// <summary>Rendering cost: LOW for older / hot phones, MEDIUM default, HIGH for flagships.</summary>
    public enum GraphicsQuality { Low, Medium, High }

    /// <summary>Player options, stored in PlayerPrefs.</summary>
    public static class Settings
    {
        public static Difficulty Difficulty
        {
            get => (Difficulty)PlayerPrefs.GetInt("pcc.difficulty", (int)Difficulty.Normal);
            set => PlayerPrefs.SetInt("pcc.difficulty", (int)value);
        }

        /// <summary>Swipe to play shots, or joystick + DEFEND / GROUND / LOFT buttons.</summary>
        public static BattingControls Batting
        {
            get => (BattingControls)PlayerPrefs.GetInt("pcc.batting", (int)BattingControls.Buttons);
            set => PlayerPrefs.SetInt("pcc.batting", (int)value);
        }

        public static MatchTime Lighting
        {
            get => (MatchTime)PlayerPrefs.GetInt("pcc.time", (int)MatchTime.Auto);
            set => PlayerPrefs.SetInt("pcc.time", (int)value);
        }

        public static bool TimingHint
        {
            get => PlayerPrefs.GetInt("pcc.hint", 1) == 1;
            set => PlayerPrefs.SetInt("pcc.hint", value ? 1 : 0);
        }

        public static bool Sound
        {
            get => PlayerPrefs.GetInt("pcc.sound", 1) == 1;
            set => PlayerPrefs.SetInt("pcc.sound", value ? 1 : 0);
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("pcc.vibrate", 1) == 1;
            set => PlayerPrefs.SetInt("pcc.vibrate", value ? 1 : 0);
        }

        static int _gfx = -1;

        public static GraphicsQuality Graphics
        {
            get
            {
                if (_gfx < 0) _gfx = PlayerPrefs.GetInt("pcc.gfx", (int)GraphicsQuality.Medium);
                return (GraphicsQuality)_gfx;
            }
            set
            {
                _gfx = (int)value;
                PlayerPrefs.SetInt("pcc.gfx", _gfx);
            }
        }

        /// <summary>60 FPS in matches (smoother, warmer phone). Off = 30 FPS battery saver.</summary>
        public static bool Smooth60
        {
            get => PlayerPrefs.GetInt("pcc.fps60", 0) == 1;
            set => PlayerPrefs.SetInt("pcc.fps60", value ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void Buzz()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Vibration) Handheld.Vibrate();
#endif
        }
    }
}
