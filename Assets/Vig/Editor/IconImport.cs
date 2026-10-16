// The charm icons (U33) are pixel art: imported point-filtered, uncompressed, without mipmaps, so 32x32
// shows as crisp 3x blocks on the catalogue's cards.
using UnityEditor;
using UnityEngine;

namespace Vig.EditorTools
{
    public sealed class IconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Resources/VigIcons/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.alphaIsTransparency = true;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
