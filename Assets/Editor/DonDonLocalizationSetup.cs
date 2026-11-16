using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

/// <summary>Initial import into editable Unity String Tables. Reimport is an explicit menu action.</summary>
public static class DonDonLocalizationSetup
{
    private const string Root = "Assets/Localization";
    [MenuItem("DonDon/Localization/Import reviewed translations")]
    public static void Import()
    {
        Directory.CreateDirectory(Root + "/Tables"); Directory.CreateDirectory(Root + "/Locales");
        Directory.CreateDirectory("Assets/Resources/Localization");
        AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings,Root + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        var locales = new List<Locale>();
        foreach (string code in new[] { "ko", "en", "ja" })
        {
            string path = Root + "/Locales/" + code + ".asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null) { locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale,path); }
            LocalizationEditorSettings.AddLocale(locale); locales.Add(locale);
        }
        settings.GetStartupLocaleSelectors().Clear();
        settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("ko") });
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameLocale.TableName);
        if (collection == null) { collection = LocalizationEditorSettings.CreateStringTableCollection(GameLocale.TableName,Root+"/Tables",locales); }
        var rows = ReadCsv(File.ReadAllText(Root + "/Import/DonDon.csv"));
        if (rows.Count < 2 || rows[0].Length < 5) { throw new InvalidDataException("Invalid translation CSV."); }
        foreach (var locale in locales)
        {
            var table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null) { table = collection.AddNewTable(locale.Identifier) as StringTable; }
            int column = locale.Identifier.Code == "ko" ? 2 : locale.Identifier.Code == "en" ? 3 : 4;
            foreach (var row in rows.Skip(1))
            {
                if (row.Length < 5 || string.IsNullOrEmpty(row[column])) { throw new InvalidDataException("Missing translation: " + row[0]); }
                var entry = table.AddEntry(row[0],row[column]);
                entry.IsSmart = false;
            }
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData); EditorUtility.SetDirty(settings);
        BuildFonts(rows);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Localization imported: " + (rows.Count-1) + " entries, ko/en/ja.");
    }
    private static void BuildFonts(List<string[]> rows)
    {
        const string fontPath = "Assets/Resources/Localization/DonDon Japanese SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Localization/NotoSansCJKjp-Regular.otf");
        if (source == null) { throw new InvalidDataException("Japanese source font missing."); }
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name = "DonDon Japanese SDF"; font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font,fontPath);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach (var atlas in font.atlasTextures) { AssetDatabase.AddObjectToAsset(atlas,font); }
        }
        string corpus = new string(string.Concat(rows.Skip(1).Select(row=>row[4])).Distinct().ToArray());
        // Keep the reviewed Japanese glyphs warm in player content; avoid an initial atlas rebuild.
        var fontSettings = new SerializedObject(font);
        fontSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        // Align fallback line metrics with Pretendard. Noto's default 1.45em line hides
        // Japanese glyphs in the existing single-line trading rows (1.2em tall).
        var reference = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (reference != null)
        {
            var face = font.faceInfo; var metrics = reference.faceInfo;
            float ratio = face.pointSize / metrics.pointSize;
            face.ascentLine = metrics.ascentLine * ratio; face.descentLine = metrics.descentLine * ratio;
            face.lineHeight = metrics.lineHeight * ratio; font.faceInfo = face;
        }
        font.TryAddCharacters(corpus,out string missing);
        if (!string.IsNullOrEmpty(missing)) { Debug.LogWarning("Japanese font missing: " + missing); }
        foreach (var atlas in font.atlasTextures)
        { if (!AssetDatabase.Contains(atlas)) { AssetDatabase.AddObjectToAsset(atlas,font); } }
        const string resource = "Assets/Resources/Localization/Fonts.asset";
        var fonts = AssetDatabase.LoadAssetAtPath<GameLocaleFonts>(resource);
        if (fonts == null) { fonts = ScriptableObject.CreateInstance<GameLocaleFonts>(); AssetDatabase.CreateAsset(fonts,resource); }
        fonts.japanese = font; fonts.japaneseLegacy = source;
        EditorUtility.SetDirty(font); EditorUtility.SetDirty(fonts);
        Directory.CreateDirectory("Assets/StreamingAssets/ThirdPartyLicenses");
        File.Copy("Assets/Fonts/Localization/OFL.txt","Assets/StreamingAssets/ThirdPartyLicenses/NotoSansCJK-OFL.txt",true);
    }
    public static List<string[]> ReadCsv(string text)
    {
        var rows = new List<string[]>(); var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int i=0;i<text.Length;i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (quoted && i+1<text.Length && text[i+1]=='"') { cell.Append('"'); i++; }
                else { quoted = !quoted; }
            }
            else if (!quoted && c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else if (!quoted && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && i+1<text.Length && text[i+1]=='\n') { i++; }
                cells.Add(cell.ToString()); cell.Clear(); rows.Add(cells.ToArray()); cells.Clear();
            }
            else { cell.Append(c); }
        }
        if (quoted) { throw new InvalidDataException("Unclosed CSV field."); }
        if (cell.Length>0 || cells.Count>0) { cells.Add(cell.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }
    [MenuItem("DonDon/Localization/Preview Korean")]
    private static void Korean() { Select("ko"); }
    [MenuItem("DonDon/Localization/Preview English")]
    private static void English() { Select("en"); }
    [MenuItem("DonDon/Localization/Preview Japanese")]
    private static void Japanese() { Select("ja"); }
    private static void Select(string code)
    {
        if (EditorApplication.isPlaying) { GameLocale.SetLanguage(code); }
        else { UnityEditor.Localization.LocalizationEditorSettings.ActiveLocalizationSettings.SetSelectedLocale(LocalizationEditorSettings.GetLocales().First(locale=>locale.Identifier.Code==code)); }
    }
    public static void RunImport()
    {
        try { Import(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
