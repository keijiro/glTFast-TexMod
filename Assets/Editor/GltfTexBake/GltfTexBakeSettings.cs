using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    // How the final GPU texture format is chosen.
    enum CompressionMode { Auto, BC1, BC3, BC7, Uncompressed }

    // Whether to honor glTFast's mipmap request or force it.
    enum MipmapMode { Follow, ForceOn, ForceOff }

    // A set of bake options. Used both as the global default and as a
    // per-asset override.
    [Serializable]
    struct BakeProfile
    {
        public bool enabled;
        public int maxSize;                          // longest-edge clamp; 0 = no downscale
        public CompressionMode mode;
        public TextureCompressionQuality quality;
        public MipmapMode mipmaps;
        public int minSize;                          // skip baking at or below this longest edge

        // Built-in fallback used when no settings asset exists.
        public static BakeProfile Default => new BakeProfile
        {
            enabled = true,
            maxSize = 1024,
            mode = CompressionMode.Auto,
            quality = TextureCompressionQuality.Normal,
            mipmaps = MipmapMode.Follow,
            minSize = 0
        };
    }

    // One auto-collected entry per imported glTF asset, keyed by GUID.
    [Serializable]
    class Entry
    {
        public string glbGuid;
        public string glbPath;       // cached for display; resolved from GUID on use
        public bool useCustom;       // false = use the global defaults
        public BakeProfile overrides;
        public string lastSummary;   // informational, e.g. "3 tex -> DXT1 @1024"
    }

    // Global, editor-only settings asset. Lives at a fixed path so the import
    // add-on can find it without a reference.
    class GltfTexBakeSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Editor/GltfTexBake/GltfTexBakeSettings.asset";

        [SerializeField] BakeProfile defaults = BakeProfile.Default;
        [SerializeField] List<Entry> entries = new List<Entry>();

        public BakeProfile Defaults { get => defaults; set => defaults = value; }
        public List<Entry> Entries => entries;

        static GltfTexBakeSettings s_Instance;

        // Returns the asset if one exists anywhere in the project, otherwise
        // null. Located by type so the user can freely move/rename it. Never
        // creates the asset implicitly: asset creation during import is unsafe.
        public static GltfTexBakeSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var guids = AssetDatabase.FindAssets("t:GltfTexBakeSettings");
                    if (guids.Length > 0)
                        s_Instance = AssetDatabase.LoadAssetAtPath<GltfTexBakeSettings>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                }
                return s_Instance;
            }
        }

        // Effective profile for a given glTF asset GUID.
        public BakeProfile Resolve(string guid)
        {
            var entry = FindEntry(guid);
            return entry != null && entry.useCustom ? entry.overrides : defaults;
        }

        // Adds or refreshes the entry for an imported asset. Must be called
        // outside of import (via EditorApplication.delayCall).
        public void RegisterImport(string guid, string path, string summary)
        {
            var entry = FindEntry(guid);
            if (entry == null)
            {
                entry = new Entry { glbGuid = guid, useCustom = false, overrides = defaults };
                entries.Add(entry);
            }
            entry.glbPath = path;
            entry.lastSummary = summary;
        }

        Entry FindEntry(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var e in entries)
                if (e.glbGuid == guid) return e;
            return null;
        }
    }
}
