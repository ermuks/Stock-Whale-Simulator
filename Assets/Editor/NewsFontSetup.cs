using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Creates the news reader's dedicated SDF assets without touching scenes or other fonts.</summary>
public static class NewsFontSetup
{
    private const string Folder = "Assets/Resources/UI/News";
    [MenuItem("DonDon/UI/Prepare News Reading Font")]
    public static void Ensure()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var regular = Create("Pretendard Reading", "Regular");
        var bold = Create("Pretendard Semibold", "SemiBold");
        if (regular.fontWeightTable[7].regularTypeface != bold)
        {
            regular.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
            EditorUtility.SetDirty(regular);
        }
        AssetDatabase.SaveAssets();
    }
    private static TMP_FontAsset Create(string name, string weight)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) { return existing; }
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/News/Pretendard-" + weight + ".otf");
        if (source == null) { throw new FileNotFoundException("Pretendard source font missing: " + weight); }
        var font = TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
        font.name = name; font.isMultiAtlasTexturesEnabled = true;
        font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        if (fallback != null) { font.fallbackFontAssetTable.Add(fallback); }
        AssetDatabase.CreateAsset(font,path);
        AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        string corpus = File.ReadAllText("Assets/Resources/News/news_writing.json") + "시장뉴스기사목록알림기록성장권한최근전체보유선택미확인발행현실시간다음대기검색완료1234567890:+-·%";
        font.TryAddCharacters(new string(corpus.Distinct().ToArray()));
        EditorUtility.SetDirty(font);
        return font;
    }
}
