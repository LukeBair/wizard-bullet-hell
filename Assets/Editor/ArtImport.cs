using System.IO;
using UnityEditor;
using UnityEngine;

// Import settings for generated art in Assets/Resources/Art/<Style>/: sprites, uncompressed and
// point-filtered for the pixel styles, and the style's pixels-per-unit.
class ArtImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Art/";

    // Bump when these settings change, so Unity reimports the art with them.
    public override uint GetVersion() => 3;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        string style = assetPath.Substring(Root.Length).Split('/')[0];
        string file = Path.GetFileNameWithoutExtension(assetPath);
        var ti = (TextureImporter)assetImporter;
        ti.GetSourceTextureWidthAndHeight(out int width, out _);

        // Big sheets (the 128px boss sheets are 1280x4096) must keep their size, or Unity's 2048 default
        // shrinks them and the sheet JSON's frame rects no longer land on the frames.
        ti.maxTextureSize = 8192;
        // The 256px sheets are only sources the game resamples (PixelDensity), so they compress (ASTC on iOS).
        bool source = file.EndsWith("_256") || file.EndsWith("_256_n");
        if (source)
        {
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = 8192, format = TextureImporterFormat.ASTC_6x6 });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = 8192, format = TextureImporterFormat.RGBA32 });
        }

        // <sheet>_n.png: a normal map for the 2D lights. Linear data, never compressed or filtered.
        if (file.EndsWith("_n"))
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.textureCompression = source ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Point;
            return;
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePivot = new Vector2(0.5f, 0.5f);
        ti.spritePixelsPerUnit = ArtStyle.PixelsPerUnit(style, file, width);
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        // Pixel styles stay uncompressed so every pixel keeps its exact palette colour; the smooth
        // Vector art compresses (ASTC on iOS) to a quarter of the size with no visible difference.
        ti.textureCompression = style == "Vector" || source ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = style == "Vector" ? FilterMode.Bilinear : FilterMode.Point;
    }
}
