using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GLTFastTweaks
{
    // Surfaces the settings in Project Settings > glTFast Tweaks > Texture Overrides.
    // Built with UI Toolkit (activateHandler + SerializedObject binding).
    static class TextureOverrideSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/glTFast Tweaks", SettingsScope.Project)
            {
                label = "glTFast Tweaks",
                activateHandler = BuildUI,
                // Persist once when leaving the page. Bound edits already live in
                // the in-memory singleton; the disk write only needs to happen
                // before the session ends. (Saving inside a per-frame value
                // tracker would feed back into itself and loop.)
                deactivateHandler = () => TextureOverrideSettings.instance.Persist(),
                keywords = new[] { "glTF", "glb", "tweaks", "texture", "override", "compression", "trilinear", "downscale" }
            };
        }

        static void BuildUI(string searchContext, VisualElement root)
        {
            var settings = TextureOverrideSettings.instance;
            var so = new SerializedObject(settings);

            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            // Feature section (one of several glTFast tweaks hosted on this page).
            var title = new Label("Texture Overrides");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginBottom = 6;
            root.Add(title);

            var body = new VisualElement { style = { marginLeft = 6 } };
            root.Add(body);

            // --- Defaults (the struct foldout labels itself "Defaults") -------
            body.Add(new PropertyField(so.FindProperty("defaults")));

            // --- Imported glTF Assets ----------------------------------------
            var importedHeader = new Label("Imported glTF Assets");
            importedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            importedHeader.style.marginTop = 10;
            importedHeader.style.marginBottom = 2;
            body.Add(importedHeader);

            var emptyHelp = new HelpBox(
                "No glTF assets imported yet. Import a .glb/.gltf to populate this list.",
                HelpBoxMessageType.Info);
            body.Add(emptyHelp);

            var entriesContainer = new VisualElement();
            body.Add(entriesContainer);

            var reimportAll = new Button(() => ReimportAll(so.FindProperty("entries")))
            { text = "Apply & Reimport All" };
            reimportAll.style.height = 28;
            reimportAll.style.marginTop = 8;
            body.Add(reimportAll);

            // (Re)builds the per-entry rows. Called on activate and whenever the
            // entries array length changes (e.g. a new glTF is imported).
            void RebuildEntries()
            {
                entriesContainer.Clear();
                var entries = so.FindProperty("entries");
                emptyHelp.style.display = entries.arraySize == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                for (var i = 0; i < entries.arraySize; i++)
                    entriesContainer.Add(BuildEntry(entries.GetArrayElementAtIndex(i)));
            }

            RebuildEntries();
            root.Bind(so);

            // Rebuild rows when the entry count changes (e.g. a glTF is imported
            // while this page is open). No saving here — see deactivateHandler.
            // The tracker lives on its own element: an element may host only one
            // serialized-object tracker, and root already owns a binding context
            // from Bind() above.
            var tracker = new VisualElement();
            root.Add(tracker);
            var lastCount = so.FindProperty("entries").arraySize;
            tracker.TrackSerializedObjectValue(so, o =>
            {
                var count = o.FindProperty("entries").arraySize;
                if (count == lastCount) return;
                lastCount = count;
                RebuildEntries();
                entriesContainer.Bind(so);
            });
        }

        static VisualElement BuildEntry(SerializedProperty entry)
        {
            var useCustom = entry.FindPropertyRelative("useCustom");
            var summary = entry.FindPropertyRelative("lastSummary").stringValue;

            var box = new Box();
            box.style.paddingLeft = box.style.paddingRight = 6;
            box.style.paddingTop = box.style.paddingBottom = 4;
            box.style.marginBottom = 4;

            var pathLabel = new Label(entry.FindPropertyRelative("glbPath").stringValue);
            pathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(pathLabel);

            if (!string.IsNullOrEmpty(summary))
            {
                var sum = new Label(summary);
                sum.style.fontSize = 10;
                sum.style.opacity = 0.7f;
                box.Add(sum);
            }

            box.Add(new Toggle("Use Custom Override") { bindingPath = useCustom.propertyPath });

            var overridesField = new PropertyField(entry.FindPropertyRelative("overrides"));
            overridesField.style.marginLeft = 12;
            box.Add(overridesField);

            void UpdateVisibility() =>
                overridesField.style.display = useCustom.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateVisibility();
            box.TrackPropertyValue(useCustom, _ => UpdateVisibility());

            var reimport = new Button(() => Reimport(entry)) { text = "Reimport" };
            reimport.style.marginTop = 4;
            reimport.SetEnabled(!string.IsNullOrEmpty(GetEntryPath(entry)));
            box.Add(reimport);

            return box;
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
