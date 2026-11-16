using System;
using UnityEngine;

/// <summary>Display-only market palette. Independent of language, trading rules and risk/heat colors.</summary>
public static class MarketColors
{
    public const int RedBlue = 0, GreenRed = 1;
    private static readonly Color Red = new Color32(239,89,100,255), Blue = new Color32(76,150,232,255), Green = new Color32(55,201,149,255);
    private static int applied = -1;
    public static int Version { get; private set; }
    public static event Action Changed;
    public static Color Up => Preview(GameSettings.Current.marketColors,true);
    public static Color Down => Preview(GameSettings.Current.marketColors,false);
    public static Color Preview(int theme,bool up) => theme==GreenRed ? up ? Green : Red : up ? Red : Blue;
    public static Color ForChange(decimal value,Color neutral) => value>0 ? Up : value<0 ? Down : neutral;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { applied=-1; Version=0; Changed=null; }
    public static void Apply()
    {
        int selected=GameSettings.Current.marketColors;
        if (applied==selected)
        { return; }
        applied=selected; Version++; Changed?.Invoke();
    }
}
