using System;
using System.Collections.Generic;
using UnityEngine;

namespace UA.Game
{
    /// <summary>Physics layers (names are set in ProjectSettings/TagManager).</summary>
    public static class Layers
    {
        public const int Default = 0, World = 8, Enemy = 9, Player = 10, Viewmodel = 11, Pickup = 12, Debris = 13;
        public const int ShotMask = (1 << World) | (1 << Enemy) | (1 << Default);
        public const int WorldMask = (1 << World) | (1 << Default);
        public const int EnemyMask = 1 << Enemy;

        public static void ConfigurePhysics()
        {
            Physics.IgnoreLayerCollision(Enemy, Enemy, true);
            Physics.IgnoreLayerCollision(Debris, Enemy, true);
            Physics.IgnoreLayerCollision(Debris, Player, true);
            Physics.IgnoreLayerCollision(Debris, Debris, true);
            Physics.IgnoreLayerCollision(Pickup, Enemy, true);
            Physics.IgnoreLayerCollision(Viewmodel, World, true);
            Physics.IgnoreLayerCollision(Viewmodel, Enemy, true);
            Physics.IgnoreLayerCollision(Viewmodel, Player, true);
            Physics.queriesHitTriggers = false;
        }

        public static void SetRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetRecursive(t.gameObject, layer);
        }
    }

    /// <summary>Logging with an error counter (read by the autopilot) and a freeze breadcrumb.</summary>
    public static class Log
    {
        public static int Errors, Exceptions;
        public static string Crumb = "boot";
        static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception) { Exceptions++; Errors++; }
                else if (type == LogType.Error) Errors++;
            };
        }

        public static void I(string msg) => Debug.Log("[UA] " + msg);
        public static void W(string msg) => Debug.LogWarning("[UA] " + msg);
        public static void E(string msg) => Debug.LogError("[UA] " + msg);
    }

    /// <summary>UTC calendar helpers. Days are counted from 1970-01-01.</summary>
    public static class Clock
    {
        public static long Today => (long)Math.Floor((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays);

        public static TimeSpan UntilTomorrow
        {
            get
            {
                var now = DateTime.UtcNow;
                return now.Date.AddDays(1) - now;
            }
        }

        public static string Countdown(TimeSpan t) => ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
    }

    /// <summary>Command-line switches (e.g. -uaAuto -uaShots dir -uaPlan all).</summary>
    public static class Args
    {
        static string[] _args;

        static string[] All
        {
            get
            {
                if (_args != null) return _args;
                var list = new List<string>(Environment.GetCommandLineArgs());
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var intent = act.Call<AndroidJavaObject>("getIntent"))
                    {
                        string extra = intent.Call<string>("getStringExtra", "unity");
                        if (!string.IsNullOrEmpty(extra)) list.AddRange(extra.Split(' '));
                    }
                }
                catch (Exception) { }
#endif
                _args = list.ToArray();
                return _args;
            }
        }

        public static bool Has(string key) => Array.IndexOf(All, key) >= 0;

        public static string Get(string key, string def = null)
        {
            var a = All;
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return def;
        }
    }

    /// <summary>Small helpers used everywhere.</summary>
    public static class X
    {
        public static Color Hex(uint rgb, float a = 1) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Color A(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Color Mul(this Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, c.a);

        public static float Luma(this Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);

        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1 - Mathf.Exp(-lambda * dt));

        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1 - Mathf.Exp(-lambda * dt));

        public static Quaternion Damp(Quaternion a, Quaternion b, float lambda, float dt) => Quaternion.Slerp(a, b, 1 - Mathf.Exp(-lambda * dt));

        public static T Get<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static GameObject Child(Transform parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        /// <summary>0 outside [a,b], rises to 1 at the middle, back to 0 (a smooth bump).</summary>
        public static float Bump(float t, float a, float b)
        {
            if (t <= a || t >= b) return 0;
            return Mathf.Sin((t - a) / (b - a) * Mathf.PI);
        }

        public static float Ramp(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-5f, b - a));

        public static string Money(long v) => UA.Core.Safe.Short(v);
    }

    /// <summary>Critically damped spring for recoil, sway and hit reactions.</summary>
    public struct Spring
    {
        public float Value, Velocity;

        public void Step(float target, float stiffness, float damping, float dt)
        {
            float a = (target - Value) * stiffness - Velocity * damping;
            Velocity += a * dt;
            Value += Velocity * dt;
        }
    }

    public struct Spring3
    {
        public Vector3 Value, Velocity;

        public void Step(Vector3 target, float stiffness, float damping, float dt)
        {
            var a = (target - Value) * stiffness - Velocity * damping;
            Velocity += a * dt;
            Value += Velocity * dt;
        }
    }
}
