using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 급등·급락 폭주, 개소리 봇, 끊어 치기를 기다리지 않고 확인하는 메뉴.
public static class ChatHypeTools
{
    private const string Menu = "DonDon/Chat/Hype/";
    [MenuItem(Menu + "Reload data")]
    private static void Reload()
    {
        try { ChatHypeAssets.Reload(); Debug.Log("chat_hype reloaded"); }
        catch (System.ArgumentException ex) { Debug.LogError("chat_hype rejected: " + ex.Message); }
    }
    [MenuItem(Menu + "Force surge (play mode)")]
    private static void ForceSurge() { Report(InvestorSociety.Instance != null && InvestorSociety.Instance.ForceHype(false), "surge"); }
    [MenuItem(Menu + "Force crash (play mode)")]
    private static void ForceCrash() { Report(InvestorSociety.Instance != null && InvestorSociety.Instance.ForceHype(true), "crash"); }
    [MenuItem(Menu + "Force oddball/surge")] private static void OddSurge() { Odd("surge"); }
    [MenuItem(Menu + "Force oddball/break")] private static void OddBreak() { Odd("break"); }
    [MenuItem(Menu + "Force oddball/crash")] private static void OddCrash() { Odd("crash"); }
    [MenuItem(Menu + "Force oddball/player chased")] private static void OddBuy() { Odd("playerBuy"); }
    [MenuItem(Menu + "Force oddball/player caught the fall")] private static void OddDip() { Odd("playerDip"); }
    [MenuItem(Menu + "Force oddball/player sold big")] private static void OddSell() { Odd("playerSell"); }
    [MenuItem(Menu + "Force oddball/round number")] private static void OddRound() { Odd("round"); }
    [MenuItem(Menu + "Force oddball/lull")] private static void OddLull() { Odd("lull"); }
    [MenuItem(Menu + "Force oddball/open")] private static void OddOpen() { Odd("open"); }
    [MenuItem(Menu + "Force oddball/close")] private static void OddClose() { Odd("close"); }
    private static void Odd(string when) { Report(InvestorSociety.Instance != null && InvestorSociety.Instance.ForceOddball(when), "oddball " + when); }
    private static void Report(bool done, string what)
    {
        var society = InvestorSociety.Instance;
        if (done) { Debug.Log("Hype " + what + " forced · " + society.HypeStatus + " · oddball: " + society.OddballNickname); }
        else { Debug.LogWarning("Hype " + what + " not forced. Enter play mode with the market open; the oddball must be in the public room."); }
    }
    [MenuItem(Menu + "Forget used oddball combos")]
    private static void Forget()
    {
        PlayerPrefs.DeleteKey("DonDon.Hype.Used"); PlayerPrefs.DeleteKey("DonDon.Hype.Count"); PlayerPrefs.Save(); ChatHypeMemory.Reset();
        Debug.Log("Oddball history cleared on this machine");
    }
    /// <summary>언어마다 개소리 예시를 끊어 치는 모양 그대로 찍는다. 플레이 모드가 아니어도 된다.</summary>
    [MenuItem(Menu + "Print sample oddball lines (all languages)")]
    private static void Samples()
    {
        try { ChatHypeAssets.Reload(); }
        catch (System.ArgumentException ex) { Debug.LogError("chat_hype rejected: " + ex.Message); return; }
        var random = new System.Random();
        foreach (string code in ChatHypeAssets.Languages)
        {
            var locale = ChatHypeAssets.Language(code);
            if (locale == null) { Debug.LogWarning("chat_hype." + code + " is missing"); continue; }
            var slots = new Dictionary<string, string> { ["stockName"] = code == "ko" ? "돈돈전자" : code == "ja" ? "ドンドン電子" : "DonDon Electronics", ["price"] = "48,700", ["movePct"] = "+17.4",
                ["digit"] = "7", ["nameLen"] = code == "en" ? "17" : code == "ja" ? "6" : "4", ["count"] = "23", ["clock"] = ChatHypeAssets.Clock(locale, 13 * 60 + 40), ["minutes"] = "12", ["round"] = "50,000", ["topic"] = "?" };
            var text = new StringBuilder(); int total = 0;
            foreach (string when in ChatHypeAssets.Moments)
            {
                var combos = new List<string>();
                foreach (var setup in locale.setups)
                {
                    if (setup.when != when) { continue; }
                    if (setup.solo) { combos.Add(setup.text); continue; }
                    foreach (var verdict in locale.verdicts) { if (ChatHypeAssets.Fits(setup, verdict)) { combos.Add(setup.text + " " + verdict.text); } }
                }
                total += combos.Count; text.AppendLine("[" + when + "] " + combos.Count);
                for (int i = 0; i < 3 && combos.Count > 0; i++)
                {
                    int index = random.Next(combos.Count); string line = ChatHypeAssets.Fill(combos[index], slots); combos.RemoveAt(index);
                    var pieces = ChatBreath.Split(line, locale.breath, 5, ChatBreathMode.Sentences);
                    text.AppendLine("  " + (pieces == null ? line : string.Join("  ⏎  ", pieces)));
                }
            }
            Debug.Log(code + " oddball combos: " + total + "\n" + text);
        }
    }
}
