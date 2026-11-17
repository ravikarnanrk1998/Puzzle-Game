using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FRF
{
    /// <summary>Data baked from a photo sky: sun position in the panorama and average colours.</summary>
    public sealed class SkyInfo
    {
        public Texture2D Texture;
        public float SunU, SunV;
        public Color Sky, Horizon, Ground;

        /// <summary>
        /// Direction towards the sun in world space for a Skybox/Panoramic rotation (degrees),
        /// inverting the shader's lat-long mapping and its rotation about Y.
        /// </summary>
        public Vector3 SunDirection(float rotationDeg)
        {
            float lon = (0.5f - SunU) * Mathf.PI * 2;
            float lat = (1 - SunV) * Mathf.PI;
            var d = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
            float a = -rotationDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(c * d.x - s * d.z, d.y, s * d.x + c * d.z).normalized;
        }
    }

    /// <summary>Loads the Poly Haven photo textures and skies shipped in Resources (cached).</summary>
    public static class Art
    {
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, SkyInfo> Skies = new Dictionary<string, SkyInfo>();

        public static Texture2D Tex(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Textures.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            Textures[name] = t;
            return t;
        }

        public static SkyInfo Sky(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Skies.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Skies/" + name);
            var txt = Resources.Load<TextAsset>("Skies/" + name);
            if (tex == null || txt == null)
            {
                Skies[name] = null;
                return null;
            }
            s = new SkyInfo { Texture = tex };
            foreach (var line in txt.text.Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length < 3) continue;
                float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                switch (p[0])
                {
                    case "sun":
                        s.SunU = F(1);
                        s.SunV = F(2);
                        break;
                    case "sky":
                        s.Sky = new Color(F(1), F(2), F(3));
                        break;
                    case "horizon":
                        s.Horizon = new Color(F(1), F(2), F(3));
                        break;
                    case "ground":
                        s.Ground = new Color(F(1), F(2), F(3));
                        break;
                }
            }
            Skies[name] = s;
            return s;
        }
    }
}
