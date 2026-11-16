using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>Dedicated SDF numerals with equal digit advances; existing fonts are never modified.</summary>
public static class TradingNumeralFontSetup
{
    [MenuItem("DonDon/UI/Prepare Trading Numerals")]
    public static void Ensure()
    {
        const string path = "Assets/Resources/UI/TabletControls/Trading Numerals.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
        { return; }
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/News/Pretendard-Regular.otf");
        if (source == null) { throw new FileNotFoundException("Pretendard Regular source missing"); }
        var font = TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic);
        font.name = "Trading Numerals";
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!font.TryAddCharacters("0123456789,+-−.%/—()")) { throw new System.InvalidOperationException("Numeric glyph generation failed"); }
        float advance = 0;
        for (uint digit = '0'; digit <= '9'; digit++) { advance = Mathf.Max(advance,font.characterLookupTable[digit].glyph.metrics.horizontalAdvance); }
        for (uint digit = '0'; digit <= '9'; digit++)
        {
            var glyph = font.characterLookupTable[digit].glyph;
            var metrics = glyph.metrics;
            glyph.metrics = new GlyphMetrics(metrics.width,metrics.height,metrics.horizontalBearingX + (advance - metrics.horizontalAdvance) * .5f,metrics.horizontalBearingY,advance);
        }
        // Static prevents a later dynamic repack from restoring proportional source advances.
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);
        AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
    }
}
