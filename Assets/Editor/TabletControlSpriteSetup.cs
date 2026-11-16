using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Import individual imagegen assets without replacing existing UI sprites.</summary>
public static class TabletControlSpriteSetup
{
    [MenuItem("DonDon/UI/Import Tablet Control Sprites")]
    public static void Ensure()
    {
        foreach (string name in new[] { "ActionPrimary-v1", "ActionSecondary-v1", "InputField-v1" })
        {
            string path = "Assets/Resources/UI/TabletControls/" + name + ".png";
            if (!File.Exists(path)) { throw new FileNotFoundException(path); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
