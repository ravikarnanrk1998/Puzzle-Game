using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace UA.EditorTools
{
    /// <summary>
    /// Headless package installs through the Package Manager API (no hand-edited manifest).
    /// Usage: Unity -batchmode -projectPath . -executeMethod UA.EditorTools.PackageTools.CliAdd -uaPackages "com.unity.purchasing@5.4.4"
    /// </summary>
    public static class PackageTools
    {
        static AddAndRemoveRequest _req;
        static double _start;

        public static void CliAdd()
        {
            string list = Arg("-uaPackages");
            if (string.IsNullOrEmpty(list)) { Debug.LogError("[UA] -uaPackages missing"); EditorApplication.Exit(1); return; }
            var ids = list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Log("[UA] Adding packages: " + string.Join(", ", ids));
            _req = Client.AddAndRemove(ids, null);
            _start = EditorApplication.timeSinceStartup;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_req == null) return;
            if (!_req.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup - _start > 900)
                {
                    Debug.LogError("[UA] Package install timed out");
                    EditorApplication.Exit(1);
                }
                return;
            }
            EditorApplication.update -= Poll;
            if (_req.Status == StatusCode.Success)
            {
                foreach (var p in _req.Result) Debug.Log("[UA] Package " + p.name + " " + p.version);
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[UA] Package install failed: " + (_req.Error != null ? _req.Error.errorCode + " " + _req.Error.message : "unknown"));
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
