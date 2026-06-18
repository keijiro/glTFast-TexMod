using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    [CustomEditor(typeof(GltfTexBakeSettings))]
    class GltfTexBakeSettingsEditor : Editor
    {
        [MenuItem("Assets/Create/glTF Texture Bake Settings", priority = 300)]
        static void CreateSettings()
        {
            var existing = GltfTexBakeSettings.Instance;
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            var asset = CreateInstance<GltfTexBakeSettings>();
            AssetDatabase.CreateAsset(asset, GltfTexBakeSettings.AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaults"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Imported glTF Assets", EditorStyles.boldLabel);

            var entries = serializedObject.FindProperty("entries");
            if (entries.arraySize == 0)
                EditorGUILayout.HelpBox("No glTF assets imported yet. Import a .glb/.gltf to populate this list.", MessageType.Info);

            for (var i = 0; i < entries.arraySize; i++)
                DrawEntry(entries.GetArrayElementAtIndex(i));

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply & Reimport All", GUILayout.Height(28)))
                ReimportAll(entries);

            serializedObject.ApplyModifiedProperties();
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
