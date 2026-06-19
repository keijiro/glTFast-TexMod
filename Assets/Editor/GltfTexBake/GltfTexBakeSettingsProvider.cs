using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    // Surfaces the settings in Project Settings > glTF Texture Bake.
    static class GltfTexBakeSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/glTF Texture Bake", SettingsScope.Project)
            {
                label = "glTF Texture Bake",
                guiHandler = OnGUI,
                keywords = new[] { "glTF", "glb", "texture", "compression", "bake", "trilinear", "downscale" }
            };
        }

        static SerializedObject s_So;

        static void OnGUI(string searchContext)
        {
            if (s_So == null || s_So.targetObject == null)
                s_So = new SerializedObject(GltfTexBakeSettings.instance);
            s_So.Update();

            EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(s_So.FindProperty("defaults"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Imported glTF Assets", EditorStyles.boldLabel);

            var entries = s_So.FindProperty("entries");
            if (entries.arraySize == 0)
                EditorGUILayout.HelpBox("No glTF assets imported yet. Import a .glb/.gltf to populate this list.", MessageType.Info);

            for (var i = 0; i < entries.arraySize; i++)
                DrawEntry(entries.GetArrayElementAtIndex(i));

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply & Reimport All", GUILayout.Height(28)))
                ReimportAll(entries);

            if (s_So.ApplyModifiedProperties())
                GltfTexBakeSettings.instance.Persist();
        }

        static void DrawEntry(SerializedProperty entry)
        {
            var path = entry.FindPropertyRelative("glbPath");
            var useCustom = entry.FindPropertyRelative("useCustom");
            var summary = entry.FindPropertyRelative("lastSummary");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(path.stringValue, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(summary.stringValue))
                    EditorGUILayout.LabelField(summary.stringValue, EditorStyles.miniLabel);

                EditorGUILayout.PropertyField(useCustom, new GUIContent("Use Custom Override"));
                if (useCustom.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("overrides"), true);
                    EditorGUI.indentLevel--;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(GetEntryPath(entry))))
                {
                    if (GUILayout.Button("Reimport"))
                        Reimport(entry);
                }
            }
        }

        // Resolve the live asset path from the stored GUID (rename-safe),
        // falling back to the cached path.
        static string GetEntryPath(SerializedProperty entry)
        {
            var guid = entry.FindPropertyRelative("glbGuid").stringValue;
            var path = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? entry.FindPropertyRelative("glbPath").stringValue : path;
        }

        static void Reimport(SerializedProperty entry)
        {
            var path = GetEntryPath(entry);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void ReimportAll(SerializedProperty entries)
        {
            for (var i = 0; i < entries.arraySize; i++)
                Reimport(entries.GetArrayElementAtIndex(i));
        }
    }
}
