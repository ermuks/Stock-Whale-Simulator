using UnityEditor;
using UnityEngine;

// 게임 아이콘(캔들 네 개: 짧게 시작해 치솟았다가 긴 윗꼬리를 남기고 쏟아지는 모양)을 플레이어 설정의 기본 아이콘으로 지정한다.
// 그림은 Assets/Branding/Icon_1024.png 이다. 메뉴: DonDon/Branding/Apply game icon
public static class GameIconSetup
{
    public const string IconPath = "Assets/Branding/Icon_1024.png";

    [MenuItem("DonDon/Branding/Apply game icon")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in new[] { IconPath, "Assets/Branding/Icon_512.png" })
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { continue; }
            // 아이콘은 줄이지도 압축하지도 않고 그대로 쓴다. 둥근 모서리 바깥은 투명하다.
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 1024; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { throw new System.InvalidOperationException("Missing icon: " + IconPath); }
        // 기본 아이콘: 플랫폼별로 따로 정하지 않은 모든 크기에 이 그림이 쓰인다.
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log("[아이콘] 게임 기본 아이콘을 지정했습니다: " + IconPath);
    }
}
