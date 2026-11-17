using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FRF.EditorTools
{
    /// <summary>
    /// Prepares the Poly Haven photo assets for mobile:
    /// texture import settings (normal maps, ASTC, size caps), luminance detail maps baked from
    /// the ground photos, and the 4K HDR skies baked into tone-mapped 4096x2048 JPGs plus a small
    /// text file with the sun position and the sky, horizon and ground colours.
    /// </summary>
    public static class ArtImport
    {
        const string HdriDir = "Assets/FRF/Art/HDRI";
        const string TexDir = "Assets/FRF/Resources/Textures";
        const string SkyDir = "Assets/FRF/Resources/Skies";

        public static void ImportArt()
        {
            if (!Directory.Exists(TexDir)) return;
            BakeDetailMaps();
            AssetDatabase.Refresh();
            ConfigureTextures();
            BakeSkies();
        }

        // ------------------------------------------------------------ textures

        static void Platform(TextureImporter ti, int maxSize, TextureImporterFormat androidFormat)
        {
            ti.maxTextureSize = maxSize;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = androidFormat;
            ti.SetPlatformTextureSettings(android);
        }

        static void ConfigureTextures()
        {
            foreach (var path in Directory.GetFiles(TexDir).Where(p => p.EndsWith(".jpg") || p.EndsWith(".png")))
            {
                string p = path.Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                string name = Path.GetFileNameWithoutExtension(p);
                bool normal = name.EndsWith("_nor");
                bool trail = name.StartsWith("rocky_trail");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                ti.isReadable = false;
                Platform(ti, trail ? 2048 : 1024, TextureImporterFormat.ASTC_6x6);
                ti.SaveAndReimport();
            }
        }

        /// <summary>
        /// Grey detail maps (mean 0.5 = no change) from the ground photos, so terrain keeps the
        /// colours of its baked colour map but gains real grain.
        /// </summary>
        static void BakeDetailMaps()
        {
            foreach (var path in Directory.GetFiles(TexDir, "*_diff.jpg"))
            {
                string name = Path.GetFileNameWithoutExtension(path).Replace("_diff", "");
                string outPath = Path.Combine(TexDir, name + "_detail.png");
                if (File.Exists(outPath) && File.GetLastWriteTimeUtc(outPath) > File.GetLastWriteTimeUtc(path)) continue;
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(path));
                const int size = 1024;
                var lum = new float[size * size];
                double sum = 0, sum2 = 0;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = src.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                    float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    lum[y * size + x] = l;
                    sum += l;
                    sum2 += l * l;
                }
                Object.DestroyImmediate(src);
                double n = size * size, mean = sum / n, sd = System.Math.Sqrt(System.Math.Max(1e-6, sum2 / n - mean * mean));
                var outTex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var px = new Color32[size * size];
                for (int i = 0; i < px.Length; i++)
                {
                    float v = 0.5f + (float)((lum[i] - mean) / sd) * 0.15f;
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255), 0, 255);
                    px[i] = new Color32(b, b, b, 255);
                }
                outTex.SetPixels32(px);
                outTex.Apply();
                File.WriteAllBytes(outPath, outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);
                Debug.Log("[FRF] Baked detail map " + outPath);
            }
        }

        // ------------------------------------------------------------ skies

        static Color Aces(Color c)
        {
            float F(float x) => Mathf.Clamp01(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f));
            return new Color(F(c.r), F(c.g), F(c.b), 1);
        }

        static Color Encode(Color linear) => new Color(Mathf.Pow(linear.r, 1 / 2.2f), Mathf.Pow(linear.g, 1 / 2.2f), Mathf.Pow(linear.b, 1 / 2.2f), 1);

        static void BakeSkies()
        {
            if (!Directory.Exists(HdriDir)) return;
            Directory.CreateDirectory(SkyDir);
            foreach (var path in Directory.GetFiles(HdriDir, "*.hdr"))
            {
                string p = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(p);
                string jpg = SkyDir + "/" + name + ".jpg", txt = SkyDir + "/" + name + ".txt";
                if (!File.Exists(jpg) || !File.Exists(txt) || File.GetLastWriteTimeUtc(jpg) < File.GetLastWriteTimeUtc(p)) Bake(p, name, jpg, txt);
                var ti = AssetImporter.GetAtPath(jpg) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.isReadable = false;
                Platform(ti, 4096, TextureImporterFormat.ASTC_6x6);
                ti.SaveAndReimport();
            }
        }

        static void Bake(string hdrPath, string name, string jpgPath, string txtPath)
        {
            // Read the HDR as uncompressed half floats (editor only; the HDR never ships).
            var ti = (TextureImporter)AssetImporter.GetAtPath(hdrPath);
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var def = ti.GetDefaultPlatformTextureSettings();
            def.format = TextureImporterFormat.RGBAHalf;
            ti.SetPlatformTextureSettings(def);
            ti.SaveAndReimport();
            var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
            int w = hdr.width, h = hdr.height;
            var src = hdr.GetPixels();

            // Exposure from the log-average luminance of the sky (upper half), and the sun = brightest pixel.
            double logSum = 0;
            int count = 0, sunI = 0;
            float sunL = 0;
            for (int y = h / 2; y < h; y += 2)
            for (int x = 0; x < w; x += 2)
            {
                var c = src[y * w + x];
                float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                logSum += System.Math.Log(1e-4 + l);
                count++;
                if (l > sunL)
                {
                    sunL = l;
                    sunI = y * w + x;
                }
            }
            float avg = (float)System.Math.Exp(logSum / count);
            float exposure = 0.32f / Mathf.Max(1e-4f, avg);

            var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            Color skySum = Color.clear, horizonSum = Color.clear, groundSum = Color.clear;
            int skyN = 0, horizonN = 0, groundN = 0;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    var c = Encode(Aces(src[y * w + x] * exposure));
                    px[y * w + x] = c;
                    if ((x & 7) != 0) continue;
                    if (v > 0.75f) { skySum += c; skyN++; }
                    else if (v > 0.5f && v < 0.535f) { horizonSum += c; horizonN++; }
                    else if (v > 0.3f && v < 0.45f) { groundSum += c; groundN++; }
                }
            }
            outTex.SetPixels32(px);
            outTex.Apply();
            File.WriteAllBytes(jpgPath, outTex.EncodeToJPG(92));
            Object.DestroyImmediate(outTex);

            float sunU = (sunI % w + 0.5f) / w, sunV = (sunI / w + 0.5f) / h;
            string F(Color c) => string.Format(CultureInfo.InvariantCulture, "{0:0.0000} {1:0.0000} {2:0.0000}", c.r, c.g, c.b);
            File.WriteAllText(txtPath, string.Format(CultureInfo.InvariantCulture,
                "sun {0:0.00000} {1:0.00000}\nsky {2}\nhorizon {3}\nground {4}\n",
                sunU, sunV, F(skySum / Mathf.Max(1, skyN)), F(horizonSum / Mathf.Max(1, horizonN)), F(groundSum / Mathf.Max(1, groundN))));
            AssetDatabase.ImportAsset(jpgPath);
            AssetDatabase.ImportAsset(txtPath);
            Debug.Log("[FRF] Baked sky " + name + " (exposure " + exposure.ToString("0.000") + ", sun uv " + sunU.ToString("0.000") + "," + sunV.ToString("0.000") + ")");
        }
    }
}
