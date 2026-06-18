using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    // Bridges the asset import pipeline and the bake add-on:
    //  - Captures the .glb path currently being imported so the add-on (which
    //    only receives raw bytes) can resolve per-asset settings.
    //  - Registers/refreshes a settings entry for each imported glTF asset,
    //    *after* import, on the main thread.
    class GltfTexBakeImportTracker : AssetPostprocessor
    {
        // Set on the import worker thread in OnPreprocessAsset and read by the
        // add-on on the same thread during the same import. (Worker and add-on
        // share a process/thread per asset, so a thread-static is sufficient;
        // it is NOT shared with the main-process postprocess below.)
        [System.ThreadStatic] static string s_CurrentGlbPath;
        public static string CurrentGlbPath => s_CurrentGlbPath;

        static bool s_Flushing;

        static bool IsGltf(string path) =>
            path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessAsset()
        {
            s_CurrentGlbPath = IsGltf(assetPath) ? assetPath : null;
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (s_Flushing) return;

            var pending = new List<(string guid, string path)>();
            foreach (var path in importedAssets)
            {
                if (!IsGltf(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                    pending.Add((guid, path));
            }

            if (pending.Count == 0) return;

            // Defer the settings-asset edit until after the import completes.
            EditorApplication.delayCall += () =>
            {
                var settings = GltfTexBakeSettings.Instance;
                Debug.Log($"[GltfTexBake] postprocess: {pending.Count} glTF imported, settings={(settings != null)}");
                if (settings == null) return; // no settings asset yet; nothing to record

                s_Flushing = true;
                try
                {
                    foreach (var (guid, path) in pending)
                        settings.RegisterImport(guid, path, BuildSummary(path));
                    EditorUtility.SetDirty(settings);
                }
                finally
                {
                    s_Flushing = false;
                }
            };
        }

        // Build a short summary from the baked texture sub-assets (read on the
        // main thread after import).
        static string BuildSummary(string path)
        {
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var lines = new List<string>();
            foreach (var rep in reps)
                if (rep is Texture2D t)
                    lines.Add($"{t.width}x{t.height} {t.format}");
            return lines.Count > 0 ? $"{lines.Count} tex: {string.Join("; ", lines)}" : null;
        }
    }
}
