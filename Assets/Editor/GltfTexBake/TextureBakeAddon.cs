using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Addons;
using GLTFast.Schema;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace GltfTexBake
{
    // Intercepts embedded glTF PNG/JPEG textures on import and bakes them
    // (downscale + GPU compression) according to the per-asset settings
    // resolved from GltfTexBakeSettings.
    static class TextureBakeRegistration
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            ImportAddonRegistry.RegisterImportAddon(new TextureBakeAddon());
        }
    }

    class TextureBakeAddon : ImportAddon<TextureBakeAddonInstance> { }

    class TextureBakeAddonInstance : ImportAddonInstance, ITextureImageLoader
    {
        // --- ImportAddonInstance -------------------------------------------
        public override bool SupportsGltfExtension(string extensionName) => false;

        public override void Inject(GltfImportBase gltfImport) => gltfImport.AddImportAddonInstance(this);

        public override void Inject(IInstantiator instantiator) { }

        public override void Dispose() { }

        // --- ITextureImageLoader -------------------------------------------

        // We do not add support for any glTF texture extension.
        public bool IsAbleToLoad(TextureBase texture, out int imageIndex)
        {
            imageIndex = -1;
            return false;
        }

        // Content-based detection: this is the only hook reached for textures
        // embedded in a .glb (buffer view, no URI).
        public bool IsAbleToLoad(ReadOnlySpan<byte> data) => ImageFormatDetection.IsPngOrJpeg(data);

        public Task<ImageResult> LoadImage(
            NativeArray<byte>.ReadOnly data,
            bool linear,
            bool readable,
            bool generateMipMaps,
            CancellationToken cancellationToken)
        {
            var profile = ResolveProfile();
            var wantMips = profile.mipmaps switch
            {
                MipmapMode.ForceOn => true,
                MipmapMode.ForceOff => false,
                _ => generateMipMaps
            };

            var bytes = data.ToArray();
            var hasAlpha = ImageFormatDetection.IsPng(bytes) && !linear; // JPEG never has alpha; linear maps drop alpha

            // 1. Decode at full resolution into a temporary readable texture.
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            if (!src.LoadImage(bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(src);
                return Task.FromResult(ImageResult.Null);
            }

            int sw = src.width, sh = src.height;
            var longest = Mathf.Max(sw, sh);

            var bake = profile.enabled && longest > profile.minSize;
            var doDownscale = bake && profile.maxSize > 0 && longest > profile.maxSize;
            var doCompress = bake && profile.mode != CompressionMode.Uncompressed;

            // 2. Target size (rounded to a multiple of 4 when compressing).
            int tw = sw, th = sh;
            if (doDownscale)
            {
                var scale = profile.maxSize / (float)longest;
                tw = Mathf.RoundToInt(sw * scale);
                th = Mathf.RoundToInt(sh * scale);
            }
            if (doCompress)
            {
                tw = RoundToMultipleOf4(tw);
                th = RoundToMultipleOf4(th);
            }

            // 3. Resample through a RenderTexture (matching color space).
            //    Skip the blit only when nothing about the texture changes.
            Texture2D dst;
            if (tw == sw && th == sh && !wantMips)
            {
                dst = src;
            }
            else
            {
                var rt = RenderTexture.GetTemporary(
                    tw, th, 0, RenderTextureFormat.ARGB32,
                    linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                dst = new Texture2D(tw, th, TextureFormat.RGBA32, wantMips, linear);
                dst.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                dst.Apply(wantMips, false);

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(src);
            }

            // 4. GPU-compress (editor, high quality).
            string formatLabel;
            if (doCompress)
            {
                var format = SelectFormat(profile.mode, linear, hasAlpha);
                EditorUtility.CompressTexture(dst, format, profile.quality);
                formatLabel = format.ToString();
            }
            else
            {
                formatLabel = "RGBA32";
            }
            dst.Apply(false, !readable);

            Debug.Log($"[GltfTexBake] {sw}x{sh}->{tw}x{th} {formatLabel} (linear={linear}, mips={wantMips})");

            return Task.FromResult(new ImageResult(dst));
        }

        static BakeProfile ResolveProfile()
        {
            // AssetDatabase access during import can be restricted depending on
            // the import worker context; degrade gracefully to built-in defaults.
            try
            {
                var settings = GltfTexBakeSettings.Instance;
                if (settings == null) return BakeProfile.Default;
                var guid = AssetDatabase.AssetPathToGUID(GltfTexBakeImportTracker.CurrentGlbPath ?? string.Empty);
                return settings.Resolve(guid);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GltfTexBake] Falling back to default profile: {e.Message}");
                return BakeProfile.Default;
            }
        }

        static TextureFormat SelectFormat(CompressionMode mode, bool linear, bool hasAlpha) => mode switch
        {
            CompressionMode.BC1 => TextureFormat.DXT1,
            CompressionMode.BC3 => TextureFormat.DXT5,
            CompressionMode.BC7 => TextureFormat.BC7,
            // Auto: drop alpha for linear (normal/ORM) data; pick DXT5 only when
            // the source actually carries alpha.
            _ => linear ? TextureFormat.DXT1 : (hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1)
        };

        static int RoundToMultipleOf4(int v) => Mathf.Max(4, (v + 2) / 4 * 4);
    }
}
