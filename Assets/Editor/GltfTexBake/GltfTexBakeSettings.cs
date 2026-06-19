using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    // GPU compression level. Mirrors Unity's TextureImporterCompression levels
    // but with the labels requested for this tool. Maps to a desktop BC format
    // and compression quality in TextureBakeAddon.
    //   None          -> uncompressed (RGBA32)
    //   LowQuality    -> DXT1/DXT5, Fast quality
    //   NormalQuality -> DXT1/DXT5, Normal quality
    //   HighQuality   -> BC7, Best quality
    enum Compression { None, LowQuality, NormalQuality, HighQuality }

    // A set of bake options. Used both as the global default and as a
    // per-asset override.
    [Serializable]
    struct BakeProfile
    {
        public bool enabled;
        public int maxSize;                          // longest-edge clamp; 0 = no downscale
        public Compression compression;
        public bool forceTrilinear;                  // force FilterMode.Trilinear

        // Built-in fallback used when no settings asset exists.
        public static BakeProfile Default => new BakeProfile
        {
            enabled = true,
            maxSize = 1024,
            compression = Compression.NormalQuality,
            forceTrilinear = false
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

    // Global, editor-only settings stored under ProjectSettings/ and surfaced
    // in the Project Settings window (see GltfTexBakeSettingsProvider).
    [FilePath("ProjectSettings/GltfTexBakeSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    class GltfTexBakeSettings : ScriptableSingleton<GltfTexBakeSettings>
    {
        [SerializeField] BakeProfile defaults = BakeProfile.Default;
        [SerializeField] List<Entry> entries = new List<Entry>();

        public BakeProfile Defaults { get => defaults; set => defaults = value; }
        public List<Entry> Entries => entries;

        // Persist to the ProjectSettings/ file.
        public void Persist() => Save(true);

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
