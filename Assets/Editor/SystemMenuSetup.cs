using UnityEditor;
using UnityEngine;

public static class SystemMenuSetup
{
    [MenuItem("DonDon/Settings/Import menu artwork")]
    public static void Import()
    {
        const string path="Assets/Resources/UI/SystemMenu/MenuControl-v2.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.spriteBorder=new Vector4(32,32,32,32);
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect; settings.spriteAlignment=(int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        DonDonLocalizationSetup.Import();
    }
}
