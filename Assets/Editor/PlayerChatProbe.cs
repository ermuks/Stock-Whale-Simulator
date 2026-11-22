using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 플레이어 채팅 확인: 실제 씬에서 플레이어인 척 공개 라운지에 글을 쓰고, 봇들의 답과 대화가 어디까지 이어지는지 적는다.
// 열려 있는 편집기에서 돈다: 메뉴 DonDon/Validate/Player chat probe, 또는 Validation/ChatProbe/probe.request 파일을 만들면 시작한다.
// 씬을 열거나 저장하지 않는다. 씬에 저장하지 않은 변경이 있거나 누가 플레이 중이면 시작하지 않는다.
// 게임 저장은 Validation/ChatProbe/save 폴더로 돌려 실제 저장 파일을 건드리지 않는다. 게임 속도는 바꾸지 않는다(1배).
// 주문을 내지 않고 봇의 마음·난수열을 건드리지 않는다. 하는 일은 SubmitPlayerChat을 부르고 PlayerTalkTrace를 읽는 것뿐이다.
// 결과는 Validation/ChatProbe/report.txt 에 남는다(진행 중에도 계속 쓴다).
[InitializeOnLoad]
public static class PlayerChatProbe
{
    private const string Folder = "Validation/ChatProbe", Request = Folder + "/probe.request", ScenePath = "Assets/Scenes 1/TradeScene.unity";
    private const string RunningKey = "ChatProbe.Running", OptionsEnabledKey = "ChatProbe.OptionsEnabled", OptionsKey = "ChatProbe.Options", BackgroundKey = "ChatProbe.Background", RefreshedKey = "ChatProbe.Refreshed", RefreshedAtKey = "ChatProbe.RefreshedAt";
    private const double Budget = 1700;
    private sealed class Reply { public string bot, text, key; public float delay; }
    private sealed class Turn { public string said, read, section; public readonly List<Reply> replies = new(); public readonly List<string> notes = new(); public string phase; public int clock; }
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new();
    private static readonly List<Turn> turns = new();
    private static bool running, sawPlaying;
    private static double began, ready, nextWatch, nextFlush;
    private static IEnumerator<float> script;
    private static Turn current;
    private static double saidAt, lastReplyAt;
    private static string section = "";
    private static bool chainOnly, deepMode, perfMode, fineMode, debateMode, crashMode, plainMode, marketMode;
    private static int answered, lastChain;
    private static string[] sayLines;
    // say 요청에 "lang en"(또는 ja, ko) 줄이 있으면 그 언어로 바꿔서 돌리고, 끝나면 원래 언어로 되돌린다.
    private static string sayLanguage, restoreLanguage;

    static PlayerChatProbe()
    {
        EditorApplication.update += Watch;
        // 배치 모드(창 없이 돌리는 편집기)에서는 요청 하나를 처리하고 스스로 끝낸다. 무슨 일이 있어도 40분이 지나면 끝낸다.
        if (Application.isBatchMode) { EditorApplication.update += BatchWatchdog; }
        // 화면 촬영 도구의 요청 파일을 빌려 스크립트를 불러들이게 한 경우, 그 요청은 여기서 거둔다(촬영은 하지 않는다).
        const string borrowed = "Validation/Screens/capture.request";
        try
        {
            if (File.Exists(borrowed) && File.ReadAllText(borrowed).Contains("chat-probe-bootstrap")) { File.Delete(borrowed); SessionState.SetBool("DonDon.ScreenTour.Refreshed", false); }
        }
        catch (IOException) { }
        if (SessionState.GetBool(RunningKey, false)) { EditorApplication.delayCall += Recover; }
    }
    private static bool batchQuit;
    private static void BatchWatchdog()
    {
        if (EditorApplication.timeSinceStartup > 2400) { EditorApplication.Exit(2); return; }
        // 요청이 없으면 할 일이 없다(스크립트를 불러들인 직후 잠깐은 기다린다).
        if (!running && !batchQuit && !File.Exists(Request) && !SessionState.GetBool(RunningKey, false) && EditorApplication.timeSinceStartup > 120 && !EditorApplication.isCompiling) { batchQuit = true; }
        if (batchQuit && !running && !EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.Exit(0); }
    }
    private static void Recover()
    {
        Restore();
        Directory.CreateDirectory(Folder);
        File.AppendAllText(Path.Combine(Folder, "report.txt"), Environment.NewLine + "STOPPED: scripts reloaded during the probe" + Environment.NewLine);
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
    }
    private static void Restore()
    {
        if (!SessionState.GetBool(RunningKey, false)) { return; }
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(OptionsEnabledKey, false);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(OptionsKey, 0);
        Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", null);
        SessionState.SetBool(RunningKey, false);
    }
    private static void Watch()
    {
        if (running || EditorApplication.timeSinceStartup < nextWatch) { return; }
        nextWatch = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) { return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request); SessionState.SetBool(RefreshedKey, false);
            Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), "REFUSED: play mode is already running" + Environment.NewLine);
            return;
        }
        // 바뀐 스크립트를 먼저 불러들이고, 컴파일이 끝난 뒤(도메인이 다시 올라온 뒤) 시작한다.
        if (!SessionState.GetBool(RefreshedKey, false))
        {
            SessionState.SetBool(RefreshedKey, true); SessionState.SetFloat(RefreshedAtKey, (float)EditorApplication.timeSinceStartup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return;
        }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(RefreshedAtKey, 0f) < 8 || EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        SessionState.SetBool(RefreshedKey, false);
        // 요청에 chain이 있으면 주고받기 길이만 잰다(짧게 끝난다).
        // deep: 기억·되묻기·먼저 말 걸기를 본다. perf: 도는 동안 느린 프레임을 프로파일러로 잡는다(다른 것과 함께 쓸 수 있다).
        string request = File.ReadAllText(Request);
        chainOnly = request.Contains("chain"); deepMode = request.Contains("deep"); perfMode = request.Contains("perf"); fineMode = request.Contains("fine"); debateMode = request.Contains("debate"); crashMode = request.Contains("crash"); marketMode = request.Contains("market"); plainMode = request.Contains("plain");
        // say: 첫 줄이 say면 그 아래 줄들을 그대로 채팅에 쓰고 답을 본다(특정 문장을 확인할 때).
        sayLines = null;
        if (request.TrimStart().StartsWith("say", StringComparison.Ordinal))
        {
            var lines = new List<string>();
            sayLanguage = null;
            foreach (string raw in request.Split((char)10, (char)13))
            {
                string line = raw.Trim();
                if (line.StartsWith("lang ", StringComparison.Ordinal)) { string code = line.Substring(5).Trim(); if (GameLocale.Supported(code)) { sayLanguage = code; } continue; }
                if (line.Length > 0 && line != "say") { lines.Add(line); }
            }
            sayLines = lines.ToArray(); chainOnly = deepMode = perfMode = fineMode = debateMode = crashMode = marketMode = false;
        }
        File.Delete(Request); Run();
    }

    [MenuItem("DonDon/Validate/Player chat probe")]
    public static void Run()
    {
        if (running) { return; }
        Directory.CreateDirectory(Folder); report.Clear(); errors.Clear(); turns.Clear();
        var scene = EditorSceneManager.GetActiveScene();
        // 배치 모드에서는 열려 있는 씬이 없으므로 직접 연다.
        if (Application.isBatchMode && scene.path != ScenePath) { scene = EditorSceneManager.OpenScene(ScenePath); }
        string refusal = EditorApplication.isPlayingOrWillChangePlaymode ? "play mode is already running" : EditorApplication.isCompiling ? "scripts are compiling" :
            EditorUtility.scriptCompilationFailed ? "scripts have compile errors" : scene.path != ScenePath ? "active scene is " + scene.path : scene.isDirty ? "the scene has unsaved changes" : null;
        if (refusal != null) { File.WriteAllText(Path.Combine(Folder, "report.txt"), "REFUSED: " + refusal + "\n"); batchQuit = true; return; }
        running = true;
        Directory.CreateDirectory(Folder + "/save");
        foreach (string old in Directory.GetFiles(Folder + "/save")) { File.Delete(old); }
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", Path.GetFullPath(Folder + "/save"));
        SessionState.SetBool(OptionsEnabledKey, EditorSettings.enterPlayModeOptionsEnabled); SessionState.SetInt(OptionsKey, (int)EditorSettings.enterPlayModeOptions);
        SessionState.SetBool(BackgroundKey, Application.runInBackground); SessionState.SetBool(RunningKey, true);
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.runInBackground = true;
        began = EditorApplication.timeSinceStartup; ready = began + 3; sawPlaying = false; script = null; current = null; section = "";
        File.WriteAllText(Path.Combine(Folder, "report.txt"), "RUNNING: entering play mode" + Environment.NewLine);
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    private static void Log(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) { errors.Add(message + "\n" + stack); } }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - began > Budget) { Finish(new Exception("time budget reached")); return; }
        if (EditorApplication.isPlaying) { sawPlaying = true; }
        else if (sawPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(new Exception("play mode ended before the probe finished")); return; }
        if (!EditorApplication.isPlaying) { return; }
        try
        {
            Drain(now);
            if (FrameSpikes.Running && MarketDayController.Instance != null) { FrameSpikes.Poll(Clock()); }
            if (now >= nextFlush) { nextFlush = now + 5; Write(false, null); }
            if (now < ready) { return; }
            if (script == null)
            {
                var manager = StockManager.Instance; var day = MarketDayController.Instance; var society = InvestorSociety.Instance;
                if (manager == null || day == null) { ready = now + .5; return; }
                if (!manager.SetupConfirmed)
                {
                    if (NewGame.CanBegin) { NewGame.Begin(); }
                    ready = now + 1; return;
                }
                if (!day.Ready || society == null || !society.PlayerCanWrite(0)) { ready = now + .5; return; }
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                if (Time.timeScale != 1f) { Finish(new Exception("time scale is " + Time.timeScale)); return; }
                script = Script();
            }
            if (!script.MoveNext()) { Finish(null); return; }
            ready = now + script.Current;
        }
        catch (Exception error) { Finish(error); }
    }

    // ── 대본 ──
    private static InvestorSociety Society => InvestorSociety.Instance;
    private static string Clock() { var day = MarketDayController.Instance; return day.Phase + " " + (day.ClockMinute / 60).ToString("00") + ":" + (day.ClockMinute % 60).ToString("00"); }
    /// <summary>한 줄을 쓰고 답을 기다린다: 적어도 least초, 마지막 답이 온 뒤 2.5초, 길어도 most초.</summary>
    private static IEnumerable<float> Say(string text, float least = 5f, float most = 12f)
    {
        Drain(EditorApplication.timeSinceStartup);
        var day = MarketDayController.Instance;
        current = new Turn { said = text, section = section, phase = day.Phase.ToString(), clock = day.ClockMinute };
        turns.Add(current); saidAt = lastReplyAt = EditorApplication.timeSinceStartup;
        if (!Society.SubmitPlayerChat(0, text)) { current.notes.Add("(보내지 못함)"); yield break; }
        while (true)
        {
            yield return .25f;
            double now = EditorApplication.timeSinceStartup;
            if (now - saidAt >= most || now - saidAt >= least && now - lastReplyAt >= 2.5) { break; }
        }
    }
    private static IEnumerable<float> Quiet(float seconds, string why)
    {
        current = new Turn { said = null, section = section, read = "(" + seconds.ToString("0") + "초 동안 아무 말도 하지 않음: " + why + ")", phase = MarketDayController.Instance.Phase.ToString(), clock = MarketDayController.Instance.ClockMinute };
        turns.Add(current); saidAt = lastReplyAt = EditorApplication.timeSinceStartup;
        yield return seconds;
    }
    // ── 저장하고 불러온 뒤 ──
    private static bool sessionLoaded;
    private static void OnSessionLoaded() { sessionLoaded = true; }
    private static IEnumerable<float> SaveLoad()
    {
        // 진짜 저장 파일을 덮어쓰지 않는다: 저장 폴더를 검증용으로 돌려 둔 실행(DONDON_SETTINGS_OUTPUT)에서만 한다.
        if (!SessionStore.ValidationMode)
        { foreach (float wait in Quiet(1f, "저장·불러오기 건너뜀: DONDON_SETTINGS_OUTPUT이 없어 진짜 저장을 덮어쓰게 됨")) { yield return wait; } yield break; }
        var day = MarketDayController.Instance; var menu = PauseMenuController.Instance; var society = Society;
        foreach (float wait in Quiet(1f, "장을 마감시킴")) { yield return wait; }
        var note = current;
        note.notes.Add("(마감 직전: 라운지 글 " + day.MessageCount + "줄, 토론 " + (society.DebateRunning ? "진행 중" : "없음") + ", 군중 " + society.HypeStatus + ", 끝난 대화 묶음 " + society.ClosedThreads.Count + ")");
        typeof(MarketDayController).GetField("elapsed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(day, day.tradingSeconds);
        double until = EditorApplication.timeSinceStartup + 90;
        while (EditorApplication.timeSinceStartup < until && !(menu.CurrentPage == PauseMenuController.Page.Report && SessionStore.Exists)) { yield return .5f; }
        if (menu.CurrentPage != PauseMenuController.Page.Report || !SessionStore.Exists) { note.notes.Add("(실패: 마감 보고서나 저장 파일이 생기지 않음)"); yield break; }
        yield return 2f;
        CaptureRoom(EditorApplication.timeSinceStartup + 2);
        int savedCount = day.MessageCount; long savedLast = roomLast;
        note.notes.Add("(마감 저장됨: 라운지 글 " + savedCount + "줄, " + day.Phase + " " + (day.ClockMinute / 60).ToString("00") + ":" + (day.ClockMinute % 60).ToString("00") + ")");
        sessionLoaded = false; menu.SessionLoaded += OnSessionLoaded; menu.LoadSession();
        until = EditorApplication.timeSinceStartup + 180;
        while (EditorApplication.timeSinceStartup < until && !sessionLoaded) { yield return .5f; }
        menu = PauseMenuController.Instance; if (menu != null) { menu.SessionLoaded -= OnSessionLoaded; }
        if (!sessionLoaded) { note.notes.Add("(실패: 3분 안에 불러오기가 끝나지 않음)"); yield break; }
        yield return 2f;
        day = MarketDayController.Instance; society = Society;
        note.notes.Add("(불러옴: 라운지 글 " + day.MessageCount + "줄" + (day.MessageCount == savedCount ? " — 저장 때와 같음" : " — 저장 때는 " + savedCount + "줄") +
            ", 토론 " + (society.DebateRunning ? "진행 중" : "없음") + ", 군중 " + society.HypeStatus + ")");
        menu.ContinueAfterReport();
        foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
        yield return 1f;
        if (Time.timeScale <= 0f && !PauseMenuController.IsOpen) { Time.timeScale = 1f; }
        note.notes.Add("(보고서를 넘김: 시계 배율 " + Time.timeScale + ", " + day.Phase + ")");
        int rowsBefore = roomRows;
        foreach (float wait in Quiet(120f, "불러온 뒤 밤 시간의 채팅을 지켜봄")) { yield return wait; }
        CaptureRoom(EditorApplication.timeSinceStartup + 2);
        current.notes.Add("(불러온 뒤 2분 동안 새로 올라온 봇의 글 " + (roomRows - rowsBefore) + "줄, 토론 " + (Society.DebateRunning ? "진행 중" : "없음") + ", 군중 " + Society.HypeStatus + ")");
        roomLog.AppendLine("---- 위는 저장 전, 아래는 불러온 뒤 (" + savedLast + " 다음부터) ----");
    }
    // ── 방 전체 기록 ──
    // 내 말에 달린 답만이 아니라 라운지에 올라온 모든 글을 순서대로 적는다(Validation/ChatProbe/room.txt).
    // 봇끼리의 대화가 앞뒤로 이어지는지, 같은 사건에 서로 다른 말을 하는지는 이 기록으로 본다.
    private static readonly StringBuilder roomLog = new();
    private static long roomLast; private static double roomNext; private static int roomRows, roomReplies, roomMentions;
    private static readonly Dictionary<string, int> roomTexts = new();
    private static readonly HashSet<string> roomSpeakers = new();
    private static void CaptureRoom(double now)
    {
        var day = MarketDayController.Instance;
        if (day == null || now < roomNext) { return; }
        roomNext = now + 1;
        for (int i = 0; i < day.MessageCount; i++)
        {
            var message = day.GetMessage(i);
            if (message.Sequence <= roomLast) { continue; }
            roomLast = message.Sequence;
            var social = message.Social;
            // 화면에 보이는 그대로 적는다(영어·일본어에서는 화면이 그 언어로 바꿔 보여 준다).
            string shown; try { shown = LocalizedChat.Text(message); } catch (Exception) { shown = message.Text; }
            string tag = social == null ? message.Notice.ToString() : social.templateKey + (social.replyToId != 0 ? " ↩" : "") + (social.threadId != 0 ? " #" + social.threadId : "");
            roomLog.Append((message.Minute / 60).ToString("00")).Append(':').Append((message.Minute % 60).ToString("00")).Append("  ").Append(message.Nickname).Append(": ").Append(shown).Append("    [").Append(tag).AppendLine("]");
            if (social == null) { continue; }
            roomRows++; roomSpeakers.Add(message.Nickname ?? "");
            if (social.replyToId != 0) { roomReplies++; }
            if (shown != null && shown.IndexOf('@') >= 0) { roomMentions++; }
            string text = shown ?? "";
            roomTexts[text] = roomTexts.TryGetValue(text, out int seen) ? seen + 1 : 1;
        }
    }
    private static void Drain(double now)
    {
        var society = Society;
        if (society == null) { return; }
        CaptureRoom(now);
        // 라운지 토론의 흐름도 같이 적는다.
        while (society.DebateTrace.Count > 0) { string line = society.DebateTrace.Dequeue(); if (current != null) { current.notes.Add("[토론] " + line.Trim()); } }
        while (society.PlayerTalkTrace.Count > 0)
        {
            string line = society.PlayerTalkTrace.Dequeue();
            if (current == null) { continue; }
            if (line.StartsWith("나: ", StringComparison.Ordinal)) { int arrow = line.IndexOf("  → ", StringComparison.Ordinal); current.read = arrow < 0 ? line : line.Substring(arrow + 4); continue; }
            if (line.StartsWith("    > ", StringComparison.Ordinal))
            {
                string body = line.Substring(6); int colon = body.IndexOf(": ", StringComparison.Ordinal), bracket = body.LastIndexOf("  [", StringComparison.Ordinal);
                if (colon > 0 && bracket > colon)
                {
                    current.replies.Add(new Reply { bot = body.Substring(0, colon), text = body.Substring(colon + 2, bracket - colon - 2), key = body.Substring(bracket + 3).TrimEnd(']'), delay = (float)(now - saidAt) });
                    lastReplyAt = now; continue;
                }
            }
            current.notes.Add(line.Trim());
        }
    }
    private static Stock PickStock()
    {
        Stock best = null; float hottest = -1f;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
            if (heat > hottest) { hottest = heat; best = stock; }
        }
        return best;
    }
    private static Reply Asked(Turn turn)
    {
        foreach (var reply in turn.replies) { if (reply.text.IndexOf('?') >= 0 || reply.text.IndexOf('？') >= 0) { return reply; } }
        return null;
    }
    private static readonly List<string> findings = new();
    private static int flip;
    /// <summary>봇이 되물은 말의 갈래에 맞는 답을 고른다(번갈아 긍정·부정). 갈래를 모르면 null.</summary>
    private static string AnswerFor(Reply asked, string name)
    {
        bool first = flip++ % 2 == 0; string key = asked.key;
        if (key.Contains("askBack.ate") || key.Contains("ping.topic.food")) { return first ? "라면 먹었음" : "아직 안 먹음"; }
        if (key.Contains("askBack.profit")) { return first ? "ㅇㅇ 좀 벌었음" : "아니 물렸음"; }
        if (key.Contains("askBack.style")) { return first ? "단타임" : "장투임"; }
        if (key.Contains("askBack.howlong")) { return first ? "오늘 처음임" : "3년 됐음"; }
        if (key.Contains("askBack.watch")) { return first ? name + " 보는 중" : "딱히 없음"; }
        if (key.Contains("askBack.hold") || key.Contains("ping.stock")) { return first ? "없음 현금임" : name + " 들고 있음"; }
        return null;
    }
    /// <summary>봇이 내게 먼저 말을 걸 때까지 가만히 기다린다. 걸었으면 true를 lastPing에 남긴다.</summary>
    private static bool lastPing;
    private static IEnumerable<float> WaitPing(float seconds)
    {
        current = new Turn { said = null, section = section, read = "(봇이 먼저 말을 걸기를 최대 " + seconds.ToString("0") + "초 기다림)", phase = MarketDayController.Instance.Phase.ToString(), clock = MarketDayController.Instance.ClockMinute };
        turns.Add(current); saidAt = lastReplyAt = EditorApplication.timeSinceStartup; lastPing = false;
        while (EditorApplication.timeSinceStartup - saidAt < seconds)
        {
            yield return .25f;
            foreach (var reply in current.replies) { if (reply.key.StartsWith("ping", StringComparison.Ordinal)) { lastPing = true; } }
            if (lastPing) { yield return 1.5f; yield break; }
        }
    }
    /// <summary>라운지 토론을 지켜보다가 가끔 끼어든다: 맞장구, 종목 얘기, 딴 얘기, 웃음, 반대. 끼어든 뒤 판이 어떻게 바뀌는지 적는다.</summary>
    private static IEnumerable<float> WatchDebate(string name)
    {
        section = "토론 관찰";
        string[] says = { "ㅇㅇ", name + " 이거 보통 뭐라고 줄여 말함?", "어제 떡볶이 먹었는데 맛있더라", "요즘 넷플릭스 뭐가 재밌음?", "ㄴㄴ", "강아지 산책 시키고 왔음", "주말에 등산 갈까 고민 중", name + " 오를 것 같은데" };
        int said = 0; double until = EditorApplication.timeSinceStartup + 420, nextSay = EditorApplication.timeSinceStartup + 40;
        foreach (float wait in Quiet(1f, "토론이 시작되기를 기다림")) { yield return wait; }
        while (EditorApplication.timeSinceStartup < until)
        {
            yield return .5f;
            if (Society.DebateRunning && EditorApplication.timeSinceStartup >= nextSay && said < says.Length)
            {
                foreach (float wait in Say(says[said++], 9f, 16f)) { yield return wait; }
                nextSay = EditorApplication.timeSinceStartup + 38;
                foreach (float wait in Quiet(1f, "지켜봄")) { yield return wait; }
            }
        }
        findings.Add("호감·인지도: 호감이 오른 횟수 " + Society.FondnessGains + ", 내린 횟수 " + Society.FondnessLosses + ", 인지도 " + Society.PlayerRenown.ToString("0.0") + " (" + Society.PlayerRenownTier + "), 내 말을 따라 매매하기로 한 봇 " + Society.PlayerFollowers + "명");
        findings.Add("채팅 예약: 작업 스레드가 완성한 글 " + ChatScheduler.Composed + "줄, 조합한 기분 한마디 " + Society.ComposedLines + "줄, 잔움직임 반응(쏘나/쏟는다) " + Society.TwitchLines + "줄, 오타 " + Society.TyposMade + "번");
        findings.Add("토론 " + Society.DebatesStarted + "판, 토론에서 나온 말 " + Society.DebateLinesSaid + "줄, 옆길로 샌 횟수 " + Society.DebateBranches + ", 내가 끼어든 횟수 " + said + ", 조합해서 한 말 " + Society.ComposedLines + "줄, 오타 " + Society.TyposMade + "번");
    }
    /// <summary>
    /// 급등 뒤 급락 측정: 한 종목에 불나방을 몰아넣고(들어가고 나가는 것은 풀이 평소대로 한다) 값이 어디까지 올랐다가 어디서 받쳐지는지 적는다.
    /// 요청에 plain이 있으면 기준점 심리와 버티기 심리를 끄고 잰다(비교용).
    /// </summary>
    private static IEnumerable<float> WatchCrash()
    {
        section = plainMode ? "급등락 측정 (기준점 심리 끔)" : "급등락 측정 (기준점 심리 켬)";
        TradeBot.AnchorPsychology = !plainMode;
        foreach (float wait in Quiet(1f, "장이 자리 잡기를 기다림")) { yield return wait; }
        yield return 30f;
        // 값이 너무 싸지도 비싸지도 않은 종목 하나(호가 단위의 영향이 작다). 두 번 재도 같은 종목이 되도록 이름순으로 고른다.
        Stock subject = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice < 2000m || stock.currentPrice > 50000m) { continue; }
            if (subject == null || string.CompareOrdinal(stock.symbol, subject.symbol) < 0) { subject = stock; }
        }
        if (subject == null || MothPool.Instance == null) { findings.Add("측정할 종목이나 불나방 풀이 없음"); yield break; }
        decimal start = subject.currentPrice, reference = start;
        if (StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyChange(subject, out decimal change)) { reference = start / (1m + change / 100m); }
        bool forced = MothPool.Instance.Force(subject, 350);
        current.notes.Add("종목 " + subject.symbol + " · 기준가(전일 종가) " + reference.ToString("N0") + " · 시작 " + start.ToString("N0") + " · 불나방 몰기 " + (forced ? "성공" : "실패"));
        decimal peak = start, low = start; double began = EditorApplication.timeSinceStartup, peakAt = 0, lowAt = 0, nextLog = 0; int panicPeak = 0;
        while (EditorApplication.timeSinceStartup - began < 420)
        {
            yield return 1f;
            double at = EditorApplication.timeSinceStartup - began; decimal price = subject.currentPrice;
            if (price > peak) { peak = price; peakAt = at; low = price; lowAt = at; }
            if (price < low) { low = price; lowAt = at; }
            int panicking = 0;
            foreach (var bot in Society.SocialBots.Values) { if (bot != null && bot.Social != null && bot.Social.Psychology.HasPanicFor(subject)) { panicking++; } }
            panicPeak = Math.Max(panicPeak, panicking);
            if (at >= nextLog)
            {
                nextLog = at + 15;
                current.notes.Add(at.ToString("000") + "s  " + price.ToString("N0") + "돈  기준가 대비 " + ((price / reference - 1m) * 100m).ToString("+0.0;-0.0") + "%  불나방 " + MothPool.Instance.CountOn(subject) + "명  투매 중 " + panicking + "명");
            }
            if (!subject.AcceptsOrders) { current.notes.Add(at.ToString("000") + "s  거래가 멈춤"); break; }
        }
        decimal end = subject.currentPrice;
        findings.Add((plainMode ? "[심리 끔] " : "[심리 켬] ") + subject.symbol + " 기준가 " + reference.ToString("N0") + " → 고점 " + peak.ToString("N0") + " (" + ((peak / reference - 1m) * 100m).ToString("+0;-0") + "%, " + peakAt.ToString("0") + "초) → 고점 뒤 저점 "
            + low.ToString("N0") + " (" + ((low / reference - 1m) * 100m).ToString("+0;-0") + "%, " + lowAt.ToString("0") + "초) → 끝 " + end.ToString("N0") + " (" + ((end / reference - 1m) * 100m).ToString("+0;-0") + "%) · 한때 투매한 봇 최대 " + panicPeak + "명");
    }
    /// <summary>
    /// 하루 장을 그대로 지켜본다(아무것도 시키지 않는다. 세력의 작전도 불나방도 게임이 알아서 한다).
    /// 종목마다 기준가(전일 종가) 대비 고점, 고점 뒤의 저점, 끝값을 적는다. 급등했다가 처음 값보다 훨씬 아래로 꺼지는 종목이 있는지 본다.
    /// 요청에 plain이 있으면 기준점 심리와 버티기 심리를 끄고 잰다(비교용).
    /// </summary>
    private static IEnumerable<float> WatchMarket()
    {
        section = plainMode ? "하루 장 관찰 (기준점 심리 끔)" : "하루 장 관찰 (기준점 심리 켬)";
        TradeBot.AnchorPsychology = !plainMode;
        foreach (float wait in Quiet(1f, "장이 끝날 때까지 지켜봄")) { yield return wait; }
        var stocks = new List<Stock>(); var reference = new List<decimal>(); var peak = new List<decimal>(); var low = new List<decimal>(); var floor = new List<decimal>();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            decimal start = stock.currentPrice;
            if (StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyChange(stock, out decimal change)) { start = stock.currentPrice / (1m + change / 100m); }
            stocks.Add(stock); reference.Add(start); peak.Add(stock.currentPrice); low.Add(stock.currentPrice); floor.Add(stock.currentPrice);
        }
        var day = MarketDayController.Instance; double began = EditorApplication.timeSinceStartup; int ralliesBefore = MothPool.RalliesTotal;
        while (day.Phase == MarketPhase.Trading && EditorApplication.timeSinceStartup - began < 880)
        {
            yield return 1f;
            for (int i = 0; i < stocks.Count; i++)
            {
                decimal price = stocks[i].currentPrice;
                if (price <= 0m) { continue; }
                // 새 고점이 나오면 "고점 뒤의 저점"은 거기서 다시 잰다.
                if (price > peak[i]) { peak[i] = price; low[i] = price; }
                if (price < low[i]) { low[i] = price; }
                if (price < floor[i]) { floor[i] = price; }
            }
        }
        int crashed = 0;
        for (int i = 0; i < stocks.Count; i++)
        {
            decimal up = (peak[i] / reference[i] - 1m) * 100m, after = (low[i] / reference[i] - 1m) * 100m, end = (stocks[i].currentPrice / reference[i] - 1m) * 100m, worst = (floor[i] / reference[i] - 1m) * 100m;
            bool surged = up >= 20m; if (surged && after <= -20m) { crashed++; }
            current.notes.Add(stocks[i].symbol.PadRight(5) + " 기준가 " + reference[i].ToString("N0").PadLeft(10) + "  고점 " + up.ToString("+0;-0").PadLeft(5) + "%  고점 뒤 저점 " + after.ToString("+0;-0").PadLeft(5) + "%  하루 최저 " + worst.ToString("+0;-0").PadLeft(5) + "%  끝 " + end.ToString("+0;-0").PadLeft(5) + "%"
                + (surged ? "  ← 급등" + (after <= -20m ? " 뒤 기준가보다 20% 넘게 꺼짐" : " 뒤 기준가 근처에서 받침") : ""));
        }
        findings.Add((plainMode ? "[심리 끔] " : "[심리 켬] ") + "지켜본 시간 " + (EditorApplication.timeSinceStartup - began).ToString("0") + "초, 불나방이 몰린 횟수 " + (MothPool.RalliesTotal - ralliesBefore)
            + ", 20% 넘게 급등했다가 기준가보다 20% 넘게 꺼진 종목 " + crashed + "개 / " + stocks.Count + "개");
    }
    /// <summary>프레임만 본다: 7분 동안 평소처럼 두고, 40초마다 한 줄씩만 쓴다.</summary>
    private static IEnumerable<float> Observe(string name)
    {
        section = "프레임 관찰";
        string[] lines = { "다들 뭐 보고 있음?", name + " 어때", "배고프다", "오늘 장 어때", name + " 오를 것 같아서 보는 중", "ㅋㅋㅋ", "누가 정보좀", "졸리다", "세력 어디 붙었음?", "점심 뭐 먹지" };
        double until = EditorApplication.timeSinceStartup + 420; int index = 0;
        while (EditorApplication.timeSinceStartup < until)
        {
            foreach (float wait in Say(lines[index++ % lines.Length])) { yield return wait; }
            yield return 32f;
        }
    }
    /// <summary>깊은 대화: 되물음에 맞는 답, 같은 얘기 이어가기, 종목을 보는 까닭, 내가 한 말을 기억했다가 먼저 말 걸기.</summary>
    private static IEnumerable<float> Deep(string name)
    {
        string[] answers = { "그냥 구경 중", "ㅇㅇ", "몰라", "그냥 감임", "ㄴㄴ", "글쎄", "ㅇㅋ" };
        section = "A. 되물음에 맞춰 답하기";
        int longest = 0; var lengths = new List<int>();
        foreach (string start in new[] { "점심 뭐 먹지", name + " 보는 중", "오늘 날씨 좋다", "안녕", name + " 보는 중", "기분 좋다", "배고프다", name + " 오를 듯", "너네 봇이냐", name + " 보는 중" })
        {
            foreach (float wait in Converse(start, answers, name)) { yield return wait; }
            lengths.Add(lastChain); if (lastChain > longest) { longest = lastChain; }
        }
        findings.Add("주고받기 사슬: 가장 길게 " + longest + "번, 시작말별 " + string.Join(",", lengths));

        section = "B. 같은 얘기 이어가기";
        foreach (string line in new[] { "치킨 먹고 싶다", "양념으로", "콜라도 같이", "회사 가기 싫다", "부장 때문에", "내일도 가야 됨" }) { foreach (float wait in Say(line)) { yield return wait; } }

        section = "C. 종목을 보는 까닭";
        foreach (string line in new[] { name + " 오를 것 같아서 보는 중", "왜?", "근거는?", "진짜?", "왜?" }) { foreach (float wait in Say(line)) { yield return wait; } }

        section = "D. 봇이 먼저 말 걸기";
        foreach (float wait in Say(name + " 오를 것 같아서 보는 중")) { yield return wait; }
        foreach (float wait in Say("배고프다")) { yield return wait; }
        int pings = 0, answeredPings = 0;
        for (int round = 0; round < 3; round++)
        {
            foreach (float wait in WaitPing(130f)) { yield return wait; }
            if (!lastPing) { break; }
            pings++;
            // 처음 두 번은 받아 준다. 받아 주면 또 걸어 오는지 본다.
            if (round < 2)
            {
                Reply asked = null; foreach (var reply in current.replies) { if (reply.key.StartsWith("ping", StringComparison.Ordinal)) { asked = reply; } }
                foreach (float wait in Converse(AnswerFor(asked, name) ?? "ㅇㅇ", answers, name)) { yield return wait; }
                answeredPings++;
            }
        }
        findings.Add("봇이 먼저 건 말: " + pings + "번 (그 가운데 내가 받아 준 것 " + answeredPings + "번)");
        // 받아 주지 않으면 두 번까지만 거는지 본다.
        int before = Society.PlayerPings;
        foreach (float wait in Quiet(300f, "받아 주지 않으면 몇 번까지 거는지 봄")) { yield return wait; }
        findings.Add("내가 300초 동안 받아 주지 않는 사이 봇이 건 말: " + (Society.PlayerPings - before) + "번 (두 번까지만 걸어야 함)");
        findings.Add("게임 쪽 집계: 먼저 건 말 " + Society.PlayerPings + "번");
    }
    /// <summary>말을 꺼내고, 봇이 되물으면 답하고, 또 되물으면 또 답한다. 몇 번 주고받았는지 lastChain에 남긴다.</summary>
    private static IEnumerable<float> Converse(string start, string[] answers, string name = null)
    {
        foreach (float wait in Say(start)) { yield return wait; }
        int chain = 0;
        while (chain < 12 && Asked(current) != null)
        {
            string fitting = name == null ? null : AnswerFor(Asked(current), name);
            foreach (float wait in Say(fitting ?? answers[answered++ % answers.Length])) { yield return wait; }
            chain++;
        }
        lastChain = chain;
        current.notes.Add("(주고받기: " + chain + "번)");
    }

    private static IEnumerator<float> Script()
    {
        var day = MarketDayController.Instance;
        findings.Clear(); answered = 0;
        report.AppendLine("START " + Clock() + " day " + day.Day + " language " + GameLocale.Code + " social bots " + Society.SocialBots.Count + " lounge messages " + day.LoungeMessageCount);

        // 1. 장이 열리기 전에 묻는 말.
        section = "1. 개장 전";
        if (day.Phase == MarketPhase.PreOpen)
        {
            if (!chainOnly) { foreach (string line in new[] { "밤에 무슨 일 없었음?", "누가 정보좀", "다들 뭐 보고 있음?" }) { foreach (float wait in Say(line)) { yield return wait; } } }
            while (day.Phase == MarketPhase.PreOpen) { yield return 1f; }
            yield return 25f;   // 시세와 기사가 조금 쌓일 때까지.
        }
        var stock = PickStock(); string name = stock == null ? "산삼전자" : GameLocale.Text(stock.stockName);
        // fine이 붙으면 40ms(초당 25프레임 아래)부터 적는다.
        if (perfMode) { FrameSpikes.Threshold = fineMode ? 20f : 60f; FrameSpikes.Begin(); }
        if (sayLines != null)
        {
            section = "지정한 문장";
            if (sayLanguage != null && sayLanguage != GameLocale.Code)
            {
                restoreLanguage = GameLocale.Code; GameLocale.SetLanguage(sayLanguage);
                findings.Add("언어를 " + restoreLanguage + " → " + sayLanguage + "로 바꿔서 돌림(끝나면 되돌림)");
                yield return 3f;
            }
            foreach (string line in sayLines)
            {
                // "~60"처럼 물결표로 시작하는 줄은 그만큼(초) 아무 말 없이 기다린다(봇이 먼저 말을 거는지 볼 때).
                if (line.StartsWith("~", StringComparison.Ordinal) && float.TryParse(line.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pause))
                { foreach (float wait in Quiet(pause, "기다림")) { yield return wait; } continue; }
                // "shot 이름": 채팅 화면을 정면에서 한 장 찍는다(창이 없는 배치 모드에서도 되도록 카메라로 직접 그린다).
                // "surge"·"crash": 지금 가장 많이 움직인 종목으로 급등(급락) 소동을 일으킨다(시세는 건드리지 않고 방의 반응만 본다).
                if (line == "surge" || line == "crash")
                {
                    bool forced = Society != null && Society.ForceHype(line == "crash");
                    foreach (float wait in Quiet(1f, line == "crash" ? "급락 소동을 일으킴" + (forced ? "" : " (실패)") : "급등 소동을 일으킴" + (forced ? "" : " (실패)"))) { yield return wait; }
                    continue;
                }
                // "saveload": 장을 마감시켜 마감 저장을 쓰게 하고, 그 저장을 불러온 뒤 밤 시간으로 넘어가 채팅이 다시 도는지 본다.
                if (line == "saveload") { foreach (float wait in SaveLoad()) { yield return wait; } continue; }
                if (line.StartsWith("shot ", StringComparison.Ordinal))
                {
                    // 방을 처음 띄운 직후에는 목록이 채워질 때까지 잠깐 기다렸다가 찍는다.
                    if (!shotRoomSet) { Shot(null); yield return 2.5f; }
                    yield return 1.5f; Shot(line.Substring(5).Trim()); yield return .5f; continue;
                }
                foreach (float wait in Say(line.Replace("{stock}", name))) { yield return wait; }
            }
            // 채팅 화면을 한 장 찍어 둔다(배치 확인용).
            if (DeskViewController.Instance != null) { DeskViewController.Instance.Focus(DeskViewTarget.Chat); }
            yield return 3f;
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "chat.png"));
            yield return 2f;
            yield break;
        }
        if (marketMode) { foreach (float wait in WatchMarket()) { yield return wait; } yield break; }
        if (crashMode) { foreach (float wait in WatchCrash()) { yield return wait; } yield break; }
        if (debateMode) { foreach (float wait in WatchDebate(name)) { yield return wait; } yield break; }
        if (deepMode) { foreach (float wait in Deep(name)) { yield return wait; } yield break; }
        if (perfMode && !chainOnly) { foreach (float wait in Observe(name)) { yield return wait; } yield break; }
        string[] answers = { "그냥 구경 중", name + " 보는 중", "ㅇㅇ", "몰라", "라면 먹었음", "그냥 감임", "ㄴㄴ", "회사원임", name + " 오를 듯", "글쎄", "ㅇㅋ", "잘 모르겠음" };
        if (!chainOnly)
        {
        // 2. 장중에 묻는 말: 봇이 아는 사실로 답하는가.
        section = "2. 장중 금융 얘기";
        foreach (string line in new[] { "밤에 무슨 일 없었음?", "누가 정보좀", "다들 뭐 보고 있음?", "오늘 장 어때", "세력 어디 붙었음?", "다들 수익 어때", "뭐 들고 있음?",
            name + " 왜 이럼?", name + " 소식 있음?", name + " 살까?", name + " 세력 있음?", "뭐 살지 추천 좀", "이거 어떻게 하는 거임" })
        { foreach (float wait in Say(line)) { yield return wait; } }

        // 3. 시답잖은 얘기.
        section = "3. 잡담";
        foreach (string line in new[] { "점심 뭐 먹지", "졸려 죽겠다", "너네 봇이냐", "지금 몇 시임", "회사 가기 싫다", "부자 되고 싶다", "아무 말이나 해봄", "ㅋㅋㅋㅋ", "헐", "이 방은 원래 이런가" })
        { foreach (float wait in Say(line)) { yield return wait; } }

        }
        // 4. 되묻기: 종목 얘기를 꺼낸 뒤 "왜?"를 몇 번까지 받아 주는가.
        section = "4. 되묻기 사슬";
        foreach (float wait in Say(name + " 오를 것 같아서 보는 중")) { yield return wait; }
        int whyChain = 0; string whyBot = null; bool sameBot = true;
        foreach (string line in new[] { "왜?", "근거는?", "진짜?", "왜 그렇게 생각함?", "그래서?", "왜?", "이유가 뭔데?", "진짜?", "왜?", "ㄹㅇ?" })
        {
            foreach (float wait in Say(line)) { yield return wait; }
            var reply = current.replies.Count > 0 ? current.replies[0] : null;
            if (reply == null || !reply.key.StartsWith("follow", StringComparison.Ordinal)) { break; }
            if (whyBot != null && whyBot != reply.bot) { sameBot = false; }
            whyBot = reply.bot;
            if (reply.key.StartsWith("follow.tired", StringComparison.Ordinal)) { current.notes.Add("(봇이 그만 물으라며 대화를 접음)"); break; }
            whyChain++;
        }
        findings.Add("되묻기(\"왜?\") 사슬: " + whyChain + "번 풀어 준 뒤 끝남 (최대 10번 시도), 같은 봇이 계속 받음: " + (whyChain > 0 && sameBot));

        // 5. 봇이 되물으면 답하고, 또 되물으면 또 답한다: 몇 번까지 주고받는가.
        section = "5. 주고받기 사슬";
        int longest = 0; string longestStart = ""; var lengths = new List<int>();
        string[] starts = chainOnly
            ? new[] { "안녕", "배고프다", name + " 보는 중", "너네 봇이냐", "기분 좋다", "심심하다", name + " 어때", "나 뉴비임", "점심 뭐 먹지", "졸리다", name + " 보는 중", "부자 되고 싶다", "너네 뭐해", "오늘 날씨 좋다" }
            : new[] { "안녕", "심심하다", "나 뉴비임", "너네 뭐해", "배고프다", "ㅇㅇ", "기분 좋다" };
        foreach (string start in starts)
        {
            foreach (float wait in Converse(start, answers)) { yield return wait; }
            lengths.Add(lastChain);
            if (lastChain > longest) { longest = lastChain; longestStart = start; }
        }
        findings.Add("주고받기 사슬(봇이 되묻고 내가 답함): 가장 길게 " + longest + "번 (시작말 \"" + longestStart + "\"), 시작말별 " + string.Join(",", lengths));

        // 6. 말을 섞은 봇의 기억: 45초가 지나면 "왜?"를 누가 어떻게 받는가.
        section = "6. 시간이 지난 뒤 되묻기";
        if (!chainOnly)
        {
        foreach (float wait in Say("치킨 먹고 싶다")) { yield return wait; }
        foreach (float wait in Quiet(50f, "말을 섞은 봇의 기억(45초)이 지나기를 기다림")) { yield return wait; }
        foreach (float wait in Say("왜?")) { yield return wait; }
        }

        // 7. 가만히 있으면 봇이 내게 먼저 말을 거는가, 그리고 봇이 방에 던진 물음에 답하면 물은 봇이 받는가.
        section = "7. 봇의 물음에 답하기";
        foreach (float wait in Quiet(chainOnly ? 50f : 80f, "앞의 대화가 식기를 기다리며 봇이 먼저 말을 거는지 봄")) { yield return wait; }
        // 종목 분석 AI의 결과에 다는 한마디(aiReact)는 새벽 관찰실의 일이지 내게 거는 말이 아니다.
        int unprompted = 0; foreach (var reply in current.replies) { if (!reply.key.StartsWith("aiReact", StringComparison.Ordinal)) { unprompted++; } }
        findings.Add("내가 말하지 않는 사이 봇이 내게 건 말: " + unprompted + "줄");
        int heard = 0; var roomChains = new List<int>();
        for (int round = 0; round < 3; round++)
        {
            double until = EditorApplication.timeSinceStartup + 150; string question = null;
            while (EditorApplication.timeSinceStartup < until && (question = Society.OpenBotQuestionText(0)) == null) { yield return .5f; }
            if (question == null) { findings.Add("150초를 기다렸지만 봇이 라운지에 던진 물음이 없었음 (시도 " + (round + 1) + ")"); break; }
            yield return 2f;   // 사람이 읽고 치는 시간.
            int before = Society.PlayerAnswersHeard;
            var first = turns.Count;
            foreach (float wait in Converse(name + (round % 2 == 0 ? " 오를 것 같아서 보는 중" : " 보는 중"), answers)) { yield return wait; }
            turns[first].notes.Insert(0, "(봇의 물음: " + question + ")");
            if (Society.PlayerAnswersHeard > before) { heard++; }
            roomChains.Add(lastChain);
            foreach (float wait in Quiet(20f, "다음 물음을 기다림")) { yield return wait; }
        }
        findings.Add("봇이 라운지에 던진 물음에 답한 횟수: " + heard + ", 그 뒤 이어진 주고받기 " + string.Join(",", roomChains));
    }

    // ── 보고 ──
    private static void Write(bool final, Exception error)
    {
        var text = new StringBuilder();
        text.AppendLine(final ? (error == null ? "DONE" : "STOPPED: " + error.Message) : "RUNNING " + (EditorApplication.timeSinceStartup - began).ToString("0") + "s");
        text.Append(report);
        string last = null; int said = 0, silent = 0, lines = 0; float delaySum = 0f; int delayCount = 0;
        var bots = new HashSet<string>(); var texts = new Dictionary<string, int>();
        foreach (var turn in turns)
        {
            if (turn.section != last) { last = turn.section; text.AppendLine().AppendLine("== " + last + " =="); }
            if (turn.said == null) { text.AppendLine("  " + turn.read); }
            else
            {
                said++; if (turn.replies.Count == 0) { silent++; }
                text.AppendLine("나: " + turn.said + "    [" + turn.read + " | " + turn.phase + " " + (turn.clock / 60).ToString("00") + ":" + (turn.clock % 60).ToString("00") + "]");
            }
            foreach (var reply in turn.replies)
            {
                text.AppendLine("    " + reply.delay.ToString("0.0") + "s  " + reply.bot + ": " + reply.text + "    [" + reply.key + "]");
                lines++; bots.Add(reply.bot); texts[reply.text] = texts.TryGetValue(reply.text, out int count) ? count + 1 : 1;
            }
            if (turn.replies.Count > 0) { delaySum += turn.replies[0].delay; delayCount++; }
            foreach (string note in turn.notes) { text.AppendLine("    " + note); }
        }
        text.AppendLine().AppendLine("== 요약 ==");
        text.AppendLine("내가 쓴 줄 " + said + ", 답이 하나도 없던 줄 " + silent + ", 봇의 답 " + lines + "줄, 답한 봇 " + bots.Count + "명, 첫 답까지 평균 " + (delayCount == 0 ? 0f : delaySum / delayCount).ToString("0.0") + "초");
        int repeated = 0; foreach (var pair in texts) { if (pair.Value > 1) { repeated += pair.Value - 1; } }
        text.AppendLine("같은 문장이 되풀이된 횟수 " + repeated + " (서로 다른 답 " + texts.Count + "가지)");
        foreach (string finding in findings) { text.AppendLine(finding); }
        if (Society != null) { text.AppendLine("게임 쪽 집계: 내 글 " + Society.PlayerLines + ", 봇의 답 " + Society.PlayerReplies + ", 봇의 물음에 답함 " + Society.PlayerAnswersHeard); }
        if (Society != null)
        {
            // 봇끼리의 대화: 누구를 부른 말에 실제로 답이 달렸는가, 대화 묶음이 몇 개 생겨 어떻게 끝났는가.
            var society = Society; var ends = new Dictionary<string, int>(); int turnsSum = 0;
            foreach (var thread in society.ClosedThreads) { string why = thread.kind + "/" + thread.endReason; ends[why] = ends.TryGetValue(why, out int n) ? n + 1 : 1; turnsSum += thread.turnCount; }
            text.AppendLine("봇끼리: 대화 쪽 글 " + society.SocialPublished + ", 누구를 부른 글 " + society.DirectedMessages + ", 그중 답이 달린 글 " + society.AnsweredCalls +
                (society.DirectedMessages > 0 ? " (" + (society.AnsweredCalls * 100d / society.DirectedMessages).ToString("0.0") + "%)" : "") +
                ", 끝난 대화 묶음 " + society.ClosedThreads.Count + (society.ClosedThreads.Count > 0 ? " (평균 " + (turnsSum / (float)society.ClosedThreads.Count).ToString("0.0") + "턴)" : ""));
            foreach (var pair in ends) { text.AppendLine("    " + pair.Key + " " + pair.Value); }
            text.AppendLine("토론 " + society.DebatesStarted + "판, " + society.DebateLinesSaid + "줄, 옆길 " + society.DebateBranches + "번");
            int again = 0; foreach (var pair in roomTexts) { if (pair.Value > 1 && pair.Key.Length > 6) { again += pair.Value - 1; } }
            text.AppendLine("방 전체: 봇의 글 " + roomRows + "줄, 말한 봇 " + roomSpeakers.Count + "명, 답글 " + roomReplies + "줄, @가 든 글 " + roomMentions + "줄, 같은 문장(7자 이상)이 되풀이된 횟수 " + again);
        }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "room.txt"), roomLog.ToString(), new UTF8Encoding(false));
        text.AppendLine("runtime errors: " + errors.Count); for (int i = 0; i < errors.Count && i < 8; i++) { text.AppendLine(errors[i]); }
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "report.txt"), text.ToString(), new UTF8Encoding(false));
    }
    /// <summary>채팅 화면을 정면에서 찍어 Validation/ChatProbe/shots/이름.png로 남긴다.</summary>
    private static bool shotRoomSet;
    private static void Shot(string name)
    {
        try
        {
            // 채팅을 띄우는 모니터의 화면을 고른다(태블릿에도 같은 부품이 있다). 라운지 방을 띄워 둔다.
            TabletGroupChatUI chat = null;
            foreach (var candidate in Object.FindObjectsByType<TabletGroupChatUI>()) { if (candidate != null && candidate.monitorChat) { chat = candidate; break; } }
            if (chat != null && !shotRoomSet) { chat.SelectRoom(0); shotRoomSet = true; Canvas.ForceUpdateCanvases(); }
            var camera = Camera.main;
            if (chat == null || camera == null) { findings.Add("shot " + name + ": 채팅 화면이나 카메라를 찾지 못함"); return; }
            if (name == null) { return; }
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)chat.transform; var area = rect.rect;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            Vector3 forward = -rect.forward, center = rect.TransformPoint(area.center);
            float worldHeight = Vector3.Distance(rect.TransformPoint(new Vector3(0, area.yMin, 0)), rect.TransformPoint(new Vector3(0, area.yMax, 0)));
            camera.transform.position = center + forward * worldHeight * 2.2f; camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, rect.up); camera.fieldOfView = 27f;
            const int width = 900, height = 1600;
            var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            Directory.CreateDirectory(Folder + "/shots");
            File.WriteAllBytes(Path.Combine(Folder + "/shots", name + ".png"), pixels.EncodeToPNG());
            camera.targetTexture = old; RenderTexture.active = previous; Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            findings.Add("shot " + name + ": 찍음");
        }
        catch (Exception error) { findings.Add("shot " + name + ": 실패 " + error.Message); }
    }
    private static void Finish(Exception error)
    {
        TradeBot.AnchorPsychology = true;
        if (restoreLanguage != null) { try { GameLocale.SetLanguage(restoreLanguage); } catch (Exception) { } restoreLanguage = null; }
        if (FrameSpikes.Running)
        {
            try { File.WriteAllText(Path.Combine(Folder, "spikes.txt"), FrameSpikes.End(deepMode ? "deep chat run" : "observation run"), new UTF8Encoding(false)); }
            catch (Exception failure) { File.WriteAllText(Path.Combine(Folder, "spikes.txt"), "FAILED: " + failure + Environment.NewLine); }
        }
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        try { Drain(EditorApplication.timeSinceStartup); Write(true, error); }
        catch (Exception failure) { File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED: " + (error ?? failure) + Environment.NewLine); }
        Restore();
        running = false; script = null; current = null; batchQuit = true;
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
    }
}
