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
    // Prototype: intercept embedded glTF PNG/JPEG textures on import and bake them
    // downscaled + GPU-compressed (BC). All options are hardcoded for verification.
    static class TextureBakeRegistration
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            ImportAddonRegistry.RegisterImportAddon(new TextureBakeAddon());
            Debug.Log("[GltfTexBake] Texture bake add-on registered.");
        }
    }

    class TextureBakeAddon : ImportAddon<TextureBakeAddonInstance> { }

    class TextureBakeAddonInstance : ImportAddonInstance, ITextureImageLoader
    {
        // --- Hardcoded options ---------------------------------------------
        const int k_MaxSize = 1024;                                   // clamp longest edge
        const TextureCompressionQuality k_Quality = TextureCompressionQuality.Normal;

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

            // 2. Compute target size: clamp longest edge to k_MaxSize, round to
            //    a multiple of 4 (BC block size), keep aspect ratio.
            ComputeTargetSize(sw, sh, out var tw, out var th);

            // 3. Resample through a RenderTexture (matching color space).
            var rt = RenderTexture.GetTemporary(
                tw, th, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var dst = new Texture2D(tw, th, TextureFormat.RGBA32, generateMipMaps, linear);
            dst.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
            dst.Apply(generateMipMaps, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(src);

            // 4. GPU-compress (editor, high quality).
            var format = linear
                ? TextureFormat.DXT1            // normal / ORM: no alpha needed
                : (hasAlpha ? TextureFormat.DXT5 : TextureFormat.DXT1);
            EditorUtility.CompressTexture(dst, format, k_Quality);
            dst.Apply(false, !readable);

            Debug.Log($"[GltfTexBake] {sw}x{sh} -> {tw}x{th} {format} (linear={linear}, mips={generateMipMaps})");

            return Task.FromResult(new ImageResult(dst));
        }

        static void ComputeTargetSize(int w, int h, out int tw, out int th)
        {
            var scale = 1f;
            var longest = Mathf.Max(w, h);
            if (longest > k_MaxSize)
                scale = k_MaxSize / (float)longest;

            tw = RoundToMultipleOf4(Mathf.RoundToInt(w * scale));
            th = RoundToMultipleOf4(Mathf.RoundToInt(h * scale));
        }

        static int RoundToMultipleOf4(int v) => Mathf.Max(4, (v + 2) / 4 * 4);
    }
}
