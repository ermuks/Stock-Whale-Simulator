using UnityEditor;
using UnityEngine;

// 프롤로그 삽화(Resources/StoryIntro/Art)를 들여올 때의 설정.
// 1920×1080 그대로(2의 거듭제곱으로 늘리지 않음), 밉맵 없이, 투명한 바깥이 번지지 않게, 화질이 좋은 압축으로 들여온다.
public sealed class StoryIntroImport : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/StoryIntro/Art/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder))
        { return; }
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.sRGBTexture = true; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
