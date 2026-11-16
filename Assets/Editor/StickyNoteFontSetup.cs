using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Korean/English pen lettering plus Japanese handwriting fallback. Source fonts stay in the build.</summary>
public static class StickyNoteFontSetup
{
    public const string Folder = "Assets/Resources/UI/StickyNotes";
    public const string PrimaryPath = Folder + "/Paper Handwriting KR Latin.asset";
    public const string JapanesePath = Folder + "/Paper Handwriting JP.asset";
    [MenuItem("DonDon/UI/Prepare Sticky Note Handwriting")]
    public static void Ensure()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var japanese = Create(JapanesePath,"Yomogi-Regular.ttf",new[] {"ja"});
        var primary = Create(PrimaryPath,"NanumPenScript-Regular.ttf",new[] {"ko","en"});
        primary.fallbackFontAssetTable = new List<TMP_FontAsset> { japanese };
        primary.material.SetFloat(ShaderUtilities.ID_FaceDilate,.08f);
        japanese.material.SetFloat(ShaderUtilities.ID_FaceDilate,.08f);
        EditorUtility.SetDirty(primary.material); EditorUtility.SetDirty(japanese.material);
        EditorUtility.SetDirty(primary); AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset Create(string path, string filename, string[] languages)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
        { return existing; }
        string sourcePath = "Assets/Fonts/StickyNotes/" + filename;
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        { throw new FileNotFoundException("Handwriting source missing: " + sourcePath); }
        var font = TMP_FontAsset.CreateFontAsset(source,72,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
        font.name = Path.GetFileNameWithoutExtension(path); font.isMultiAtlasTexturesEnabled = true;
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        AssetDatabase.CreateAsset(font,path); AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        string corpus = "0123456789+-/.,:() ";
        foreach (string language in languages)
        {
            var copy = JsonUtility.FromJson<InvestorJourneyNotes.Copy>(File.ReadAllText("Assets/Resources/Localization/StickyNotes/"+language+".json"));
            corpus += string.Join("",copy.titles)+copy.observing+copy.waiting+copy.firstSignal+copy.turnover+copy.profit+copy.preparing+copy.eligible+copy.operate+copy.complete+copy.current+copy.tenThousand+copy.hundredMillion+copy.trillion+copy.won;
        }
        if (!font.TryAddCharacters(new string(corpus.Distinct().ToArray()),out string missing))
        { throw new InvalidOperationException(filename+" is missing required glyphs: "+missing); }
        EditorUtility.SetDirty(font); return font;
    }
}
