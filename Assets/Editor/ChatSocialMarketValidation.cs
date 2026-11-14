using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only fixed-step replay of the real scene. No runtime speed control or scene save is added.
public static class ChatSocialMarketValidation
{
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static string folder;
    private static StreamWriter samples;
    private static readonly Dictionary<Stock, SortedDictionary<int, decimal>> volumes = new();
    private static readonly Dictionary<TradeBot, Stock> lastTrade = new();
    private static readonly Dictionary<Stock, int> leaderMinutes = new();
    private static readonly List<double> shares = new();
    private static Stock leader;
    private static int lastMinute = -1, tradingSamples, leaderChanges, fills, startupFrames;
    private static bool confirmed, finishing;
    private static readonly List<string> errors = new();

    public static void Run()
    {
        string label = Environment.GetEnvironmentVariable("DONDON_CHAT_MARKET_CASE") ?? "baseline";
        int seed = int.TryParse(Environment.GetEnvironmentVariable("DONDON_CHAT_MARKET_SEED"), out int parsed) ? parsed : 73129;
        folder = "docs/chat-social-results/stage-7/" + label + "-" + seed;
        Directory.CreateDirectory(folder);
        samples = new StreamWriter(folder + "/minutes.csv") { AutoFlush = true };
        samples.WriteLine("day,minute,leader,top30minVolumeShare,lastTradedBreadth,filledTrades,bots");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        UnityEngine.Object.FindAnyObjectByType<MarketDayController>().seed = seed;
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update += Monitor;
        Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finishing) { return; }
        if (EditorApplication.timeSinceStartup - began > 900) { Finish("900-second wall-clock timeout"); return; }
        if (!EditorApplication.isPlaying) { return; }
        if (UnityEngine.Object.FindAnyObjectByType<ChatSocialMarketProbe>() == null)
        { new GameObject("Chat Social Market Validation (temporary)").AddComponent<ChatSocialMarketProbe>(); }
    }
    private static void Log(string text, string stack, LogType kind)
    {
        if ((kind == LogType.Exception || kind == LogType.Error) && errors.Count < 30) { errors.Add(text + "\n" + stack); }
    }
    internal static void Step()
    {
        if (finishing) { return; }
        try
        {
            startupFrames++;
            var manager = StockManager.Instance; var day = MarketDayController.Instance;
            if (manager == null || day == null) { return; }
            foreach (var stock in manager.stocks)
            {
                if (volumes.ContainsKey(stock)) { continue; }
                volumes[stock] = new SortedDictionary<int, decimal>();
                stock.OnMatchedTrade += Trade;
            }
            if (!confirmed && startupFrames > 3)
            {
                var setup = UnityEngine.Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup == null || setup.confirm == null) { return; }
                setup.confirm.onClick.Invoke(); confirmed = true;
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)) { canvas.enabled = false; }
                foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) { camera.enabled = false; }
            }
            if (!day.Ready) { return; }
            if (day.IntegrityFailed) { Finish("Production market integrity check failed"); return; }
            if (day.Day > 3) { Finish(null); return; }
            if (day.Phase != MarketPhase.Trading || day.GameMinute == lastMinute) { return; }
            lastMinute = day.GameMinute;
            int now = day.Day * 1440 + day.GameMinute;
            decimal total = 0, maximum = 0; Stock hottest = null; double score = -1;
            foreach (var stock in manager.stocks)
            {
                if (!stock.IsListed || stock.IsDelisted) { continue; }
                var recent = volumes[stock];
                foreach (int expired in recent.Keys.TakeWhile(t => t <= now - 30).ToArray()) { recent.Remove(expired); }
                decimal volume = recent.Values.Sum(); total += volume; maximum = Math.Max(maximum, volume);
                double interest = day.ConversationFocus.Read(stock, Time.time).Probability;
                if (interest > score) { score = interest; hottest = stock; }
            }
            if (hottest != null)
            {
                if (leader != null && leader != hottest) { leaderChanges++; }
                leader = hottest;
                leaderMinutes.TryGetValue(leader, out int minutes); leaderMinutes[leader] = minutes + 1;
            }
            tradingSamples++;
            if (day.GameMinute >= 570 && total > 0) { shares.Add((double)(maximum / total)); }
            int listed = manager.stocks.Count(s => s.IsListed && !s.IsDelisted);
            double breadth = (double)lastTrade.Values.Distinct().Count(s => s.IsListed && !s.IsDelisted) / Math.Max(1, listed);
            samples.WriteLine($"{day.Day},{day.GameMinute},{leader?.symbol},{(total > 0 ? maximum / total : 0)},{breadth},{fills},{day.Bots.Count}");
        }
        catch (Exception error) { Finish(error.ToString()); }
    }
    private static void Trade(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order order)
    {
        var day = MarketDayController.Instance;
        if (day == null || day.Phase != MarketPhase.Trading) { return; }
        int minute = day.Day * 1440 + day.GameMinute;
        var buckets = volumes[stock]; buckets.TryGetValue(minute, out decimal old); buckets[minute] = old + quantity;
        if (buyer is TradeBot buyBot && !buyBot.IsExchangeParticipant) { lastTrade[buyBot] = stock; }
        if (seller is TradeBot sellBot && !sellBot.IsExchangeParticipant) { lastTrade[sellBot] = stock; }
        fills++;
    }
    private static void Finish(string failure)
    {
        if (finishing) { return; }
        finishing = true; samples?.Dispose();
        var config = ChatSocialConfig.Current;
        int listed = StockManager.Instance?.stocks.Count(s => s.IsListed && !s.IsDelisted) ?? 0;
        double share = shares.Count > 0 ? shares.Average() : 1;
        double interval = (double)tradingSamples / Math.Max(1, leaderChanges);
        double duration = leaderMinutes.Count == 0 ? 1 : (double)leaderMinutes.Values.Max() / Math.Max(1, tradingSamples);
        double breadth = (double)lastTrade.Values.Distinct().Count(s => s.IsListed && !s.IsDelisted) / Math.Max(1, listed);
        bool pass = failure == null && fills > 0 && errors.Count == 0 && share <= config.Get("volumeShareMax") &&
            interval <= config.Get("hotLeaderIntervalMax") && duration <= config.Get("leaderTimeShareMax") && breadth >= config.Get("stockBreadthMin");
        string result = $"{(pass ? "PASS" : "FAIL")} actual TradeScene, no player trades/actions\n" +
            $"daysCompleted={(MarketDayController.Instance?.Day ?? 1) - 1}; bots={MarketDayController.Instance?.Bots.Count}; fills={fills}; listed={listed}\n" +
            $"volumeShare={share:F6}; leaderIntervalGameMinutes={interval:F6}; leaderTimeShare={duration:F6}; lastTradedBreadth={breadth:F6}\n" +
            $"tradingMinuteSamples={tradingSamples}; wallSeconds={EditorApplication.timeSinceStartup - began:F1}\n" +
            "Editor-only fixed 0.1s simulation steps; timeScale=1; normal 900s trading/180s post-close settings; rendering disabled; full production scene participants.\n" +
            $"failure={failure}\n" + string.Join("\n", errors);
        File.WriteAllText(folder + "/result.txt", result);
        Time.captureDeltaTime = 0;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Monitor; Application.logMessageReceived -= Log;
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

[DefaultExecutionOrder(-32000)]
public sealed class ChatSocialMarketProbe : MonoBehaviour
{
    void Awake()
    {
        Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        Time.timeScale = 1; Time.captureDeltaTime = .1f;
    }
    void Update() { ChatSocialMarketValidation.Step(); }
}
