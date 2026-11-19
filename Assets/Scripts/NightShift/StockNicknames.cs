using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 종목 줄임말. 채팅에서 봇들이 긴 이름 대신 부르는 이름이고(산삼전자 → 산전), 플레이어가 그렇게 써도 알아듣는다.
/// 언어마다 따로 적는다(Resources/Chat/stock_nicknames.json). 영어에서는 종목 기호도 줄임말로 쓴다.
/// </summary>
public static class StockNicknames
{
    [Serializable] private sealed class Entry { public string symbol; public string[] ko, en, ja; }
    [Serializable] private sealed class Catalog { public Entry[] stocks; }
    private static Dictionary<string, Entry> entries;
    private static readonly List<string> scratch = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { entries = null; ChatHypeAssets.ShortName = Spoken; }

    private static void Load()
    {
        if (entries != null) { return; }
        entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var file = Resources.Load<TextAsset>("Chat/stock_nicknames");
        if (file == null) { return; }
        var catalog = JsonUtility.FromJson<Catalog>(file.text);
        if (catalog?.stocks == null) { return; }
        foreach (var entry in catalog.stocks) { if (entry != null && !string.IsNullOrEmpty(entry.symbol)) { entries[entry.symbol] = entry; } }
    }
    private static string[] For(Entry entry, string code) => (code == "ja" ? entry.ja : code == "en" ? entry.en : entry.ko) ?? Array.Empty<string>();

    /// <summary>지금 언어로 부를 줄임말 하나. 없으면 null.</summary>
    public static string Spoken(string symbol)
    {
        Load();
        if (symbol == null || !entries.TryGetValue(symbol, out var entry)) { return null; }
        scratch.Clear(); scratch.AddRange(For(entry, GameLocale.Code));
        // 영어권에서는 종목 기호로 부르는 일이 흔하다.
        if (GameLocale.Code == "en" && !symbol.StartsWith(CorporateEconomy.SmallPrefix, StringComparison.Ordinal)) { scratch.Add(symbol); }
        return scratch.Count == 0 ? null : scratch[UnityEngine.Random.Range(0, scratch.Count)];
    }
    /// <summary>이 종목의 줄임말 전부(세 언어).</summary>
    public static IEnumerable<string> All(string symbol)
    {
        Load();
        if (symbol == null || !entries.TryGetValue(symbol, out var entry)) { yield break; }
        foreach (string code in new[] { "ko", "en", "ja" }) { foreach (string name in For(entry, code)) { if (!string.IsNullOrWhiteSpace(name)) { yield return name; } } }
    }
}
