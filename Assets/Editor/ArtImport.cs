using System.IO;
using UnityEditor;
using UnityEngine;

// Import settings for generated art in Assets/Resources/Art/<Style>/: sprites, uncompressed and
// point-filtered for the pixel styles, and the style's pixels-per-unit.
class ArtImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Art/";

    // Bump when these settings change, so Unity reimports the art with them.
    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        string style = assetPath.Substring(Root.Length).Split('/')[0];
        string file = Path.GetFileNameWithoutExtension(assetPath);
        var ti = (TextureImporter)assetImporter;
        ti.GetSourceTextureWidthAndHeight(out int width, out _);

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePivot = new Vector2(0.5f, 0.5f);
        ti.spritePixelsPerUnit = ArtStyle.PixelsPerUnit(style, file, width);
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        // Pixel styles stay uncompressed so every pixel keeps its exact palette colour; the smooth
        // Vector art compresses (ASTC on iOS) to a quarter of the size with no visible difference.
        ti.textureCompression = style == "Vector" ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = style == "Vector" ? FilterMode.Bilinear : FilterMode.Point;
    }
}
