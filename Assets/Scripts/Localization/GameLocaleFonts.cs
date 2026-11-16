using TMPro;
using UnityEngine;

public sealed class GameLocaleFonts : ScriptableObject
{
    public TMP_FontAsset japanese;
    public Font japaneseLegacy;
    public static GameLocaleFonts Current { get; private set; }
    public static void Load()
    {
        Current = Resources.Load<GameLocaleFonts>("Localization/Fonts");
        if (Current == null || Current.japanese == null) { return; }
        if (!TMP_Settings.fallbackFontAssets.Contains(Current.japanese)) { TMP_Settings.fallbackFontAssets.Add(Current.japanese); }
    }
}
