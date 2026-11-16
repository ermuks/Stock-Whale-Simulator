using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Observation only: the saved TradeScene owns all market, membership and speech decisions.
public static partial class AutonomousChatObservation
{
    [Serializable] private sealed class Status
    {
        public string mode = "actual TradeScene; no emotion/trait/speech/seed overrides; no player trades", phase, outcome, failure;
        public int seed, day, minute, bots, messages, arguments, mediations, accepted, rejected, completedMediatedThreads, fills, errors;
        public float timeScale, captureDeltaTime, tradingSeconds, postCloseSeconds;
        public double wallSeconds, simulationSeconds;
        public bool setupConfirmed, integrityFailed;
    }
    [Serializable] private sealed class ChatRow
    {
        public int day, minute, room, bot;
        public long sequence, socialId;
        public float seconds;
        public string nickname, text, notice;
    }
    [Serializable] private sealed class BotRow
    {
        public int id, room;
        public string name, kind, cash, equity;
        public float stress, greed, fear, trust, betrayal;
        public SocialTraits traits;
    }
    private static string folder;
    private static double began, nextSample;
    private static float readyAt = -1;
    private static bool oldOptions, confirmed, finishing, recordedBots;
    private static EnterPlayModeOptions oldFlags;
    private static int frames;
    private static InvestorSociety society;
    private static SocialJournal journal;
    private static readonly Status status = new();
    private static readonly HashSet<Stock> stocks = new();
    private static readonly HashSet<long> mediated = new(), closed = new();
    private static readonly Dictionary<int, long> roomVersions = new();
    private static StreamWriter speech, chat, events, changes, tape, errors;

    public static void Run()
    {
        InitializeConversationSmoke();
        folder = Environment.GetEnvironmentVariable("DONDON_AUTONOMOUS_CHAT_OUTPUT");
        string project = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
        if (string.IsNullOrEmpty(folder) || !Path.GetFullPath(folder).StartsWith(project, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Observation output must be inside this project."); }
        Directory.CreateDirectory(folder);
        speech = Writer("social-messages.jsonl"); chat = Writer("chat.jsonl"); events = Writer("events.jsonl");
        changes = Writer("emotion-changes.jsonl"); tape = Writer("trades.csv"); errors = Writer("errors.txt");
        tape.WriteLine("day,minute,seconds,symbol,price,quantity");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += Log;
        EditorApplication.update += Monitor;
        EditorApplication.EnterPlaymode();
    }
    private static StreamWriter Writer(string name)
    { return new StreamWriter(Path.Combine(folder, name), false, new System.Text.UTF8Encoding(false), 65536); }
    private static void Monitor()
    {
        if (finishing) { return; }
        if (EditorApplication.timeSinceStartup - began > 1260) { Finish("wall-clock observation budget", null); return; }
        if (!EditorApplication.isPlaying) { return; }
        if (UnityEngine.Object.FindAnyObjectByType<AutonomousChatObservationProbe>() == null)
        { new GameObject("Area :: Temporary Autonomous Chat Observer").AddComponent<AutonomousChatObservationProbe>(); }
    }
    private static void Log(string text, string stack, LogType kind)
    {
        if (kind != LogType.Error && kind != LogType.Exception) { return; }
        status.errors++; errors?.WriteLine(text + "\n" + stack); errors?.Flush();
    }
    internal static void Step()
    {
        if (finishing) { return; }
        try
        {
            frames++;
            var day = MarketDayController.Instance; var manager = StockManager.Instance;
            if (day == null || manager == null) { return; }
            if (society == null && InvestorSociety.Instance != null)
            { society = InvestorSociety.Instance; society.SocialMessagePublished += OnSpeech; }
            if (!ReferenceEquals(journal, ChatSocialAudit.Journal))
            {
                if (journal != null) { journal.Happened -= OnEvent; journal.Changed -= OnChange; }
                journal = ChatSocialAudit.Journal; journal.Happened += OnEvent; journal.Changed += OnChange;
            }
            foreach (var stock in manager.stocks)
            { if (stocks.Add(stock)) { stock.OnMatchedTrade += OnTrade; } }
            if (!confirmed && frames > 3)
            {
                var setup = UnityEngine.Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (!manager.SetupConfirmed && setup != null && setup.confirm != null && setup.confirm.interactable)
                { setup.confirm.onClick.Invoke(); }
                confirmed = manager.SetupConfirmed;
                if (confirmed)
                {
                    if (conversationSmokeSeconds == 0)
                    {
                        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)) { canvas.enabled = false; }
                        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) { camera.enabled = false; }
                    }
                }
            }
            if (status.errors > 0) { Finish("runtime error", "See errors.txt"); return; }
            if (day.IntegrityFailed) { Finish("market integrity failure", "Production integrity check failed."); return; }
            if (Time.timeScale != 1 || Time.captureDeltaTime != 0)
            { Finish("clock changed", "Observation requires normal timeScale=1 and captureDeltaTime=0."); return; }
            if (day.Ready && readyAt < 0) { readyAt = Time.time; }
            if (day.Ready && !recordedBots)
            {
                using var initial = Writer("initial-bots.jsonl");
                foreach (var bot in day.Bots)
                {
                    if (bot.Social == null) { continue; }
                    var mind = bot.Social.Psychology;
                    initial.WriteLine(JsonUtility.ToJson(new BotRow { id = bot.Social.Id, name = bot.Social.Nickname, kind = bot.GetType().Name,
                        cash = bot.cash.ToString(System.Globalization.CultureInfo.InvariantCulture), equity = bot.Equity().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        room = mind.Room, stress = bot.Social.Stress, greed = mind.Greed, fear = mind.Fear, trust = mind.Trust, betrayal = mind.Betrayal, traits = mind.Traits }));
                }
                recordedBots = true;
            }
            double now = EditorApplication.timeSinceStartup;
            SampleConversationSmoke();
            if (conversationSmokeSeconds > 0 && readyAt >= 0 && Time.time - readyAt >= conversationSmokeSeconds)
            { Finish("short normal-speed conversation smoke completed", ConversationSmokeFailure()); return; }
            if (now < nextSample) { return; }
            nextSample = now + 2;
            CaptureChats(day);
            WriteStatus(day);
            Flush();
            if (readyAt >= 0 && Time.time - readyAt >= 300 && status.completedMediatedThreads >= 2)
            { Finish("observed at least two completed mediated arguments after five normal-speed minutes", null); }
            else if (readyAt >= 0 && (day.Day > 1 || Time.time - readyAt >= 1200))
            { Finish("one market day / observation budget completed", null); }
        }
        catch (Exception error) { Finish("observer failure", error.ToString()); }
    }
    private static void OnSpeech(SocialMessage message)
    {
        speech.WriteLine(JsonUtility.ToJson(message)); status.messages++;
        if (message.act == SocialAct.MEDIATE && message.threadId > 0) { mediated.Add(message.threadId); }
    }
    private static void OnEvent(SocialEvent record)
    {
        events.WriteLine(JsonUtility.ToJson(record));
        if (record.kind == "ARGUMENT") { status.arguments++; }
        if (record.kind == "MEDIATION")
        { status.mediations++; if (record.value > 0) { status.accepted++; } else { status.rejected++; } }
        if (record.kind == "THREAD_END") { closed.Add(record.id); }
    }
    private static void OnChange(SocialChange record)
    {
        // Keep all real emotion evidence; this callback never changes values or queues speech.
        changes.WriteLine(JsonUtility.ToJson(record));
    }
    private static void OnTrade(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        var day = MarketDayController.Instance; status.fills++;
        tape.WriteLine(FormattableString.Invariant($"{day.Day},{day.GameMinute},{Time.time:F3},{stock.symbol},{price},{quantity}"));
    }
    private static void CaptureChats(MarketDayController day)
    {
        CaptureRoom(0, day.MessageCount, day.GetMessage);
        if (society == null) { return; }
        foreach (var room in society.Rooms) { CaptureRoom(room.Id, room.Count, room.Get); }
    }
    private static void CaptureRoom(int room, int count, Func<int, BotChatMessage> get)
    {
        roomVersions.TryGetValue(room, out long last);
        for (int i = 0; i < count; i++)
        {
            var message = get(i);
            if (message.Sequence <= last) { continue; }
            chat.WriteLine(JsonUtility.ToJson(new ChatRow { day = message.Day, minute = message.Minute, room = room, bot = message.BotId,
                sequence = message.Sequence, socialId = message.Social?.id ?? 0, seconds = Time.time, nickname = message.Nickname,
                text = message.Text, notice = message.Notice.ToString() }));
            last = message.Sequence;
        }
        roomVersions[room] = last;
    }
    private static void WriteStatus(MarketDayController day)
    {
        status.seed = day.seed; status.day = day.Day; status.minute = day.GameMinute; status.bots = day.Bots.Count;
        status.phase = day.Phase.ToString(); status.wallSeconds = EditorApplication.timeSinceStartup - began;
        status.simulationSeconds = readyAt < 0 ? 0 : Time.time - readyAt; status.setupConfirmed = confirmed;
        status.timeScale = Time.timeScale; status.captureDeltaTime = Time.captureDeltaTime;
        status.tradingSeconds = day.tradingSeconds; status.postCloseSeconds = day.postCloseSeconds; status.integrityFailed = day.IntegrityFailed;
        status.completedMediatedThreads = 0;
        foreach (long id in mediated) { if (closed.Contains(id)) { status.completedMediatedThreads++; } }
        File.WriteAllText(Path.Combine(folder, "status.json"), JsonUtility.ToJson(status, true));
    }
    private static void Flush()
    { speech.Flush(); chat.Flush(); events.Flush(); changes.Flush(); tape.Flush(); errors.Flush(); }
    private static void Finish(string outcome, string failure)
    {
        if (finishing) { return; }
        finishing = true; status.outcome = outcome; status.failure = failure;
        if (MarketDayController.Instance != null) { CaptureChats(MarketDayController.Instance); WriteStatus(MarketDayController.Instance); }
        Flush(); speech.Dispose(); chat.Dispose(); events.Dispose(); changes.Dispose(); tape.Dispose(); errors.Dispose();
        if (society != null) { society.SocialMessagePublished -= OnSpeech; }
        if (journal != null) { journal.Happened -= OnEvent; journal.Changed -= OnChange; }
        foreach (var stock in stocks) { stock.OnMatchedTrade -= OnTrade; }
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Monitor; Application.logMessageReceived -= Log;
        EditorApplication.Exit(failure == null && status.errors == 0 ? 0 : 1);
    }
}

[DefaultExecutionOrder(-32000)]
public sealed class AutonomousChatObservationProbe : MonoBehaviour
{
    private void Awake()
    {
        Application.runInBackground = true; Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
        // The normal scene runs in real time. No fixed-step acceleration is used.
        Time.timeScale = 1; Time.captureDeltaTime = 0;
    }
    private void Update() { AutonomousChatObservation.Step(); }
}
