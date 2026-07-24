using UnityEditor;
using UnityEngine;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Auto-imports every PNG under Resources/Art as a Sprite so the code-built
    /// menu can load them with Resources.Load, with no manual import clicks.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
