using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PCC.Bootstrap
{
    /// <summary>
    /// Adds the packages the game needs (URP, uGUI) to a fresh project.
    /// Lives in its own editor assembly with no package references, so it still
    /// compiles while the game code waits for URP to arrive.
    /// </summary>
    [InitializeOnLoad]
    public static class PackageBootstrap
    {
        // The Input System is optional: the game also runs on the legacy input backend.
        static readonly string[] Needed = { "com.unity.render-pipelines.universal", "com.unity.ugui" };

        static PackageBootstrap()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                var missing = Missing();
                if (missing.Length == 0) return;
                Debug.Log("[PCC] Adding packages: " + string.Join(", ", missing));
                Client.AddAndRemove(missing, null);
            };
        }

        static string[] Missing()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json");
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            return Needed.Where(n => !text.Contains("\"" + n + "\"")).ToArray();
        }

        /// <summary>Command line: -executeMethod PCC.Bootstrap.PackageBootstrap.EnsurePackages</summary>
        public static void EnsurePackages()
        {
            var missing = Missing();
            if (missing.Length == 0)
            {
                Debug.Log("[PCC] Packages already present.");
                return;
            }
            Debug.Log("[PCC] Adding packages: " + string.Join(", ", missing));
            var req = Client.AddAndRemove(missing, null);
            var sw = Stopwatch.StartNew();
            while (!req.IsCompleted && sw.Elapsed.TotalMinutes < 20) Thread.Sleep(250);
            if (req.Status != StatusCode.Success)
            {
                Debug.LogError("[PCC] Package install failed: " + (req.Error != null ? req.Error.message : "timeout"));
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log("[PCC] Packages added.");
        }
    }
}
