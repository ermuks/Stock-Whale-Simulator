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
            // 프롤로그가 떠 있는 동안에는 그것만 넘기며 찍는다. 대본은 프롤로그가 끝나고 방에 들어간 뒤에 시작한다.
            if (introMode && !introReported && StoryIntro.Playing) { IntroDrive(); return; }
            if (introMode && introStarted && !introReported) { if (PauseMenuController.IsOpen) { return; } IntroReport(); }
            // "DAY 1" 글자가 처음 뜬 틱의 게임 시계와 침대 연출 단계(3 눈 감음, 4 눈 뜨는 중, 5 일어나는 중).
            if (introMode && introReported && !introDayLogged && DayIntroOverlay.Instance != null && DayIntroOverlay.Instance.Playing)
            {
                introDayLogged = true; var market = MarketDayController.Instance;
                introNotes.Add("DAY 글자가 뜬 순간: 날 " + DayIntroOverlay.Instance.ShownDay + " · 게임 시계 " + (market == null ? "?" : market.ExactGameMinute.ToString("0.000") + "분 " + market.Phase) + " · 침대 연출 단계 " + (BedSleep.Instance == null ? "?" : BedSleep.Instance.Stage.ToString()) + " · 프롤로그 화면 " + (StoryIntro.Playing ? "남아 있음" : "없음"));
            }
            if (now < ready) { return; }
            if (script == null)
            {
                var manager = StockManager.Instance; var day = MarketDayController.Instance; var society = InvestorSociety.Instance;
                if (manager == null || day == null) { ready = now + .5; return; }
                if (!manager.SetupConfirmed)
                {
                    if (introMode && PauseMenuController.Instance != null)
                    {
                        if (NewGame.CanBegin)
                        {
                            // 녹화할 언어(DONDON_PROBE_INTRO_LANG). 없으면 지금 언어 그대로.
                            string wanted = Environment.GetEnvironmentVariable("DONDON_PROBE_INTRO_LANG");
                            if (!string.IsNullOrEmpty(wanted) && GameLocale.Supported(wanted) && GameLocale.Code != wanted) { introHomeLanguage = GameLocale.Code; GameLocale.SetLanguage(wanted); }
                            var menu = PauseMenuController.Instance; menu.ShowTitle(); menu.ShowNewGame(); menu.StartNewGame();
                        }
                    }
                    else if (NewGame.CanBegin) { NewGame.Begin(); }
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
        if (!Society.SubmitPlayerChat(sayRoom, text)) { current.notes.Add("(보내지 못함)"); yield break; }
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
                // "mood down|downhard|up|uphard|off": 시세와 상관없이 장 전체 분위기를 그쪽으로 쳐서 방의 말을 본다.
                if (line.StartsWith("mood ", StringComparison.Ordinal))
                {
                    string wanted = line.Substring(5).Trim();
                    InvestorSociety.ForcedMarketMood = wanted == "down" ? "marketDown" : wanted == "downhard" ? "marketDownHard" : wanted == "up" ? "marketUp" : wanted == "uphard" ? "marketUpHard" : null;
                    foreach (float wait in Quiet(1f, "장 분위기를 " + (InvestorSociety.ForcedMarketMood ?? "시세대로") + "(으)로 둠")) { yield return wait; }
                    continue;
                }
                // "calshot 이름 날짜": 벽 달력을 그 날짜인 것처럼 보이게 하고 정면에서 찍는다(끝나면 되돌린다).
                if (line.StartsWith("calshot ", StringComparison.Ordinal))
                {
                    var parts = line.Substring(8).Trim().Split(' ');
                    int.TryParse(parts.Length > 1 ? parts[1] : "0", out int wanted);
                    foreach (float wait in CalendarShot(parts[0], wanted)) { yield return wait; }
                    continue;
                }
                // "scenes": 방과 책상을 여러 각도에서 4K로 찍는다(상점 이미지의 바탕 그림. shots/scene-이름.png).
                // "pump": 불나방 풀을 한 종목에 몰아 진짜 급등(거래량 폭발)을 일으킨다. 그 종목을 가운데 화면에 1분봉으로 띄운다.
                if (line == "pump")
                {
                    var manager = StockManager.Instance; var pool = MothPool.Instance; Stock target = null;
                    // 값이 싼 종목을 고른다. 같은 돈이 들어와도 주식 수가 많아서 거래량 막대가 크게 솟는다.
                    if (manager != null) { foreach (var each in manager.stocks) { if (each != null && each.AcceptsOrders && each.currentPrice >= 300m && (target == null || each.currentPrice < target.currentPrice)) { target = each; } } }
                    bool forced = false;
                    if (manager != null && pool != null && target != null)
                    {
                        manager.SelectStock(manager.stocks.IndexOf(target));
                        var charts = Object.FindFirstObjectByType<ChartManager>(); if (charts != null) { charts.SetChartPeriod(1); }
                        forced = pool.Force(target, 260);
                    }
                    foreach (float wait in Quiet(1f, "급등을 일으킴: " + (target == null ? "종목 없음" : target.symbol + " " + target.currentPrice.ToString("N0")) + (forced ? "" : " (실패)"))) { yield return wait; }
                    continue;
                }
                // "steamrun": 상점 이미지용 장면을 한 번에 찍는다(잔잔한 장 → 전술 사용 → 급등 → 급락. shots/s-이름.png).
                if (line.StartsWith("propshot ")) { foreach (float wait in PropShot(line.Substring(9).Trim())) { yield return wait; } continue; }
                if (line == "shadowcheck") { foreach (float wait in ShadowCheck()) { yield return wait; } continue; }
                if (line == "tabletshot") { foreach (float wait in TabletShot()) { yield return wait; } continue; }
                if (line == "entryshot") { foreach (float wait in EntryShot()) { yield return wait; } continue; }
                if (line == "introcheck") { foreach (float wait in IntroCheck()) { yield return wait; } continue; }
                if (line == "laddercheck") { foreach (float wait in LadderCheck()) { yield return wait; } continue; }
                // "aidemo full|short": 새벽 관찰실 AI 명령을 차례로 쓰고 화면을 찍는다. "tongue en": 도중에 언어를 바꾼다(끝나면 되돌린다).
                if (line.StartsWith("aidemo", StringComparison.Ordinal)) { foreach (float wait in AiDemo(line.Substring(6).Trim())) { yield return wait; } continue; }
                if (line.StartsWith("tongue ", StringComparison.Ordinal))
                {
                    string wanted = line.Substring(7).Trim();
                    if (GameLocale.Supported(wanted) && wanted != GameLocale.Code) { if (restoreLanguage == null) { restoreLanguage = GameLocale.Code; } GameLocale.SetLanguage(wanted); }
                    foreach (float wait in Quiet(3f, "언어를 " + wanted + "(으)로 바꿈")) { yield return wait; }
                    continue;
                }
                if (line == "videorun2") { foreach (float wait in VideoRun2()) { yield return wait; } continue; }
                if (line == "videorun") { foreach (float wait in VideoRun()) { yield return wait; } continue; }
                if (line == "gifrun") { foreach (float wait in GifRun()) { yield return wait; } continue; }
                if (line == "steamrun") { foreach (float wait in SteamRun()) { yield return wait; } continue; }
                if (line == "scenes") { foreach (float wait in Scenes()) { yield return wait; } continue; }
                if (line == "saveload") { foreach (float wait in SaveLoad()) { yield return wait; } continue; }
                // "stockshot 이름": 종목 목록 화면을 찍는다. 보유 계좌 꼬리표가 보이도록 두 계좌로 몇 종목을 조금씩 사 둔다
                // (화면 확인용 실행이다. 2번 계좌를 열려고 현금을 넣으므로 이 실행은 저장하지 않는다).
                if (line.StartsWith("stockshot ", StringComparison.Ordinal))
                {
                    foreach (float wait in StockShot(line.Substring(10).Trim())) { yield return wait; }
                    continue;
                }
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
    // ── 새벽 관찰실 AI 시연 ──
    // "aidemo full|short": 새벽 관찰실에 들어가(해금·이용료는 게임의 함수로 낸다) AI 명령을 차례로 쓰고, 답이 올라온 채팅 화면을 한 장씩 찍는다.
    // 볼거리를 만들려고 게임의 기능을 쓴다: 플레이어 계좌로 두 종목을 사고, 세력이 매도 사다리를 깔고 한 번 내려찍고, 플레이어가 매수벽을 세웠다 거두고, 심어 둔 기사를 낸다.
    // 시세·주문·채팅을 직접 고치지 않는다. 확인용 실행이므로 저장하지 않는다.
    private static int sayRoom;
    private static bool aiDemoSet;
    private static Stock aiWhaleStock, aiFirst, aiSecond;
    private static IEnumerable<float> AiDemo(string mode)
    {
        foreach (float wait in Quiet(1f, "새벽 관찰실 AI 명령을 돌려 봄 (" + mode + ", " + GameLocale.Code + ")")) { yield return wait; }
        var note = current; var society = Society; var npc = NpcLeadingRoom.Instance; var career = PlayerProgression.Instance; var manager = StockManager.Instance;
        if (society == null || npc == null || career == null || manager == null || manager.playerAccount == null) { note.notes.Add("(실패: 필요한 것을 찾지 못함)"); yield break; }
        var player = manager.playerAccount;
        TabletGroupChatUI chat = null;
        foreach (var candidate in Object.FindObjectsByType<TabletGroupChatUI>()) { if (candidate != null && candidate.monitorChat) { chat = candidate; break; } }
        string W(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;
        string N(Stock stock) => GameLocale.Text(stock.stockName ?? stock.symbol);
        if (!aiDemoSet)
        {
            aiDemoSet = true;
            player.cash += 300000000m;
            typeof(PlayerProgression).GetProperty("Turnover").SetValue(career, (decimal)career.rules.dawnRoomTurnover);
            decimal before = player.cash; bool opened = career.UnlockDawn(player);
            float until = Time.realtimeSinceStartup + 60f;
            while (!npc.Available && Time.realtimeSinceStartup < until) { yield return 1f; }
            npc.RequestJoin(); bool joined = npc.JoinPlayer();
            note.notes.Add("(해금 " + opened + " · 입장 " + joined + " (" + npc.Status + ") · 낸 돈 " + (before - player.cash).ToString("N0") + " · 글쓰기 " + society.PlayerCanWrite(NpcLeadingRoom.RoomId) + ")");
            if (!joined) { yield break; }
            WhaleBot whale = null;
            foreach (var each in Object.FindObjectsByType<WhaleBot>())
            {
                var owned = each.OwnedCompany;
                if (owned != null && owned.AcceptsOrders && each.GetHolding(owned) >= 5000m) { whale = each; aiWhaleStock = owned; break; }
            }
            var rest = new List<Stock>();
            foreach (var stock in manager.stocks) { if (stock != null && stock.AcceptsOrders && stock.currentPrice > 0m && stock != aiWhaleStock) { rest.Add(stock); } }
            rest.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
            if (rest.Count < 2 || whale == null) { note.notes.Add("(실패: 종목이나 세력이 모자람)"); yield break; }
            aiFirst = rest[0]; aiSecond = rest[1];
            void Buy(Stock stock, decimal money)
            {
                decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * 1.03m);
                player.BuyLimit(price, Math.Max(1m, decimal.Floor(money / price)), 0, stock);
            }
            Buy(aiFirst, 20000000m); Buy(aiSecond, 8000000m); Buy(aiWhaleStock, 6000000m);
            var lay = typeof(WhaleBot).GetMethod("LayAskLadder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object laid = lay == null ? null : lay.Invoke(whale, new object[] { aiWhaleStock, 3000m, 0m });
            decimal slammed = ManipulationTools.Slam(whale, aiWhaleStock, 400m, .02m, 0m);
            decimal wall = ManipulationTools.PlaceWall(player, aiSecond, 60000000m);
            yield return 3f;
            if (wall > 0m) { ManipulationTools.PullWall(player, aiSecond, wall); }
            var planted = FakeNewsManager.Instance == null ? null : FakeNewsManager.Instance.PublishPlanted(aiFirst, true);
            yield return 8f;
            note.notes.Add("(준비: 세력 종목 " + aiWhaleStock.symbol + " 사다리 " + laid + "주, 내려찍기 " + slammed.ToString("N0") + "주 · 내 매수벽 " + aiSecond.symbol + " " + wall.ToString("N0") + "에 세웠다 거둠 · 심은 기사 " + (planted == null ? "없음" : aiFirst.symbol + " 「" + planted.title + "」")
                + " · 보유 " + aiFirst.symbol + " " + player.GetOwnedVolume(aiFirst).ToString("N0") + ", " + aiSecond.symbol + " " + player.GetOwnedVolume(aiSecond).ToString("N0") + ", " + aiWhaleStock.symbol + " " + player.GetOwnedVolume(aiWhaleStock).ToString("N0") + ")");
        }
        if (aiWhaleStock == null || aiFirst == null || aiSecond == null) { yield break; }
        if (chat != null) { chat.SelectRoom(NpcLeadingRoom.RoomId); shotRoomSet = true; Canvas.ForceUpdateCanvases(); }
        sayRoom = NpcLeadingRoom.RoomId;
        string tag = "ai-" + GameLocale.Code + "-"; int number = 0;
        IEnumerable<float> Ask(string text, string name, float least = 6f)
        {
            foreach (float wait in Say(text, least, least + 5f)) { yield return wait; }
            yield return 1.5f; Shot(tag + (++number).ToString("00") + "-" + name); yield return .5f;
        }
        bool full = mode == "full";
        if (full) { foreach (float wait in Ask(W("!도움말", "!help", "!ヘルプ"), "help")) { yield return wait; } }
        foreach (float wait in Ask(W("!분석 ", "!analyze ", "!分析 ") + N(aiWhaleStock), "analyze")) { yield return wait; }
        foreach (float wait in Ask(W("!세력 ", "!whale ", "!仕手 ") + N(aiWhaleStock), "whale")) { yield return wait; }
        if (full) { foreach (float wait in Ask(W("!세력 ", "!whale ", "!仕手 ") + N(aiSecond), "whale-wall")) { yield return wait; } }
        foreach (float wait in Ask(W("!비교 ", "!compare ", "!比較 ") + N(aiFirst) + " " + N(aiSecond), "compare")) { yield return wait; }
        foreach (float wait in Ask(W("!뉴스 ", "!news ", "!ニュース ") + N(aiFirst), "news")) { yield return wait; }
        if (full) { foreach (float wait in Ask(W("!뉴스 ", "!news ", "!ニュース ") + N(aiSecond), "news-other")) { yield return wait; } }
        foreach (float wait in Ask(W("!방", "!room", "!部屋"), "room")) { yield return wait; }
        foreach (float wait in Ask(W("!내계좌", "!account", "!口座"), "account")) { yield return wait; }
        if (full)
        {
            decimal tick = PriceTickPolicy.GetTickSize(aiWhaleStock.currentPrice);
            foreach (float wait in Ask(W("!알림 ", "!alert ", "!通知 ") + N(aiWhaleStock) + " " + (aiWhaleStock.currentPrice + tick).ToString("0"), "alert-set")) { yield return wait; }
            foreach (float wait in Say(W("!알림 ", "!alert ", "!通知 ") + N(aiWhaleStock) + " " + (aiWhaleStock.currentPrice - tick).ToString("0"), 4f, 6f)) { yield return wait; }
            foreach (float wait in Ask(W("!알림", "!alert", "!通知"), "alert-list", 4f)) { yield return wait; }
            int reports = society.AiReports; float until = Time.realtimeSinceStartup + 60f;
            while (society.AiReports == reports && Time.realtimeSinceStartup < until) { yield return 1f; }
            yield return 4f; Shot(tag + (++number).ToString("00") + "-alert-fired");
            note = current; note.notes.Add("(알림: " + (society.AiReports > reports ? "울림" : "60초 안에 울리지 않음") + " · 남은 알림 " + DawnAi.Alerts.Count + " · 오늘 쓴 알림 " + DawnAi.AlertsToday + "/" + DawnAi.AlertLimit + ")");
            foreach (float wait in Ask(W("!성적", "!record", "!成績"), "record-early", 4f)) { yield return wait; }
            decimal cash = player.cash;
            foreach (float wait in Ask(W("!업그레이드", "!upgrade", "!アップグレード"), "upgrade-ask", 4f)) { yield return wait; }
            current.notes.Add("(묻기만 했을 때: 등급 " + DawnAi.Tier + " · 나간 돈 " + (cash - player.cash).ToString("N0") + ")");
            foreach (float wait in Ask(W("!업그레이드 확인", "!upgrade confirm", "!アップグレード 確認"), "upgrade-done")) { yield return wait; }
            current.notes.Add("(확인한 뒤: 등급 " + DawnAi.Tier + " · 나간 돈 " + (cash - player.cash).ToString("N0") + ")");
            foreach (float wait in Ask(W("!분석 ", "!analyze ", "!分析 ") + N(aiFirst), "analyze-precise")) { yield return wait; }
            foreach (float wait in Ask(W("!세력 ", "!whale ", "!仕手 ") + N(aiWhaleStock), "whale-precise")) { yield return wait; }
            foreach (float wait in Ask(W("!방구", "!roomy", "!部屋着"), "not-a-command", 4f)) { yield return wait; }
            // 채점될 분석이 생기도록 몇 종목을 더 분석시킨다(매수·매도 의견이 나온 것만 채점된다).
            int asked = 0;
            foreach (var stock in manager.stocks)
            {
                if (stock == null || !stock.AcceptsOrders || stock == aiFirst || stock == aiSecond || stock == aiWhaleStock || asked >= 5) { continue; }
                asked++;
                foreach (float wait in Say(W("!분석 ", "!analyze ", "!分析 ") + N(stock), 3f, 5f)) { yield return wait; }
            }
            foreach (float wait in Quiet(DawnAi.GradeSeconds + 6f, "분석이 채점될 때까지 기다림")) { yield return wait; }
        }
        foreach (float wait in Ask(W("!성적", "!record", "!成績"), "record", 4f)) { yield return wait; }
        current.notes.Add("(성적: 오늘 적중 " + DawnAi.Right + " 빗나감 " + DawnAi.Wrong + " · 누적 " + DawnAi.TotalRight + "/" + (DawnAi.TotalRight + DawnAi.TotalWrong) + " · 채점 대기 " + DawnAi.Calls.Count + " · AI가 올린 글 " + society.AiReports + ")");
        foreach (float wait in Quiet(1f, "장 시작 전 브리핑을 한 번 더 올림(08:30에 저절로 올라온 것은 DAY " + DawnAi.BriefDay + ")")) { yield return wait; }
        bool briefed = society.PostAiBriefing(NpcLeadingRoom.RoomId);
        yield return 6f; Shot(tag + (++number).ToString("00") + "-brief"); yield return .5f;
        current.notes.Add("(브리핑 " + (briefed ? "올림" : "실패") + ")");
        sayRoom = 0;
    }

    // ── 상점 이미지용 촬영 ──
    // 전부 게임이 실제로 그린 화면이다. 시세·주문·채팅을 직접 고치지 않는다. 쓰는 것은 게임에 있는 기능뿐이다:
    //   현금을 넣어 둔 계좌로 주문을 내고(stockshot과 같다), 리딩방을 열고(PlayerProgression.OpenLeadingRooms),
    //   불붙이기를 쓰고(PlayerTactics.Ignite), 불나방 무리를 한 종목에 몰리게 한다(MothPool.Force).
    private static void Render(Camera camera, string name, int width, int height)
    {
        var texture = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        Directory.CreateDirectory(Folder + "/shots");
        File.WriteAllBytes(Path.Combine(Folder + "/shots", "s-" + name + ".png"), pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous;
        Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
    }
    private static void RenderFrom(Camera camera, string name, Vector3 position, Vector3 look, float fov, bool tall = false)
    {
        Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
        camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(look - position, Vector3.up); camera.fieldOfView = fov;
        if (tall) { Render(camera, name, 2160, 3240); } else { Render(camera, name, 3840, 2160); }
        camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
    }
    // ── 상점 설명란의 움직이는 그림용 연속 촬영 ──
    // "gifrun": 같은 자리에서 일정한 간격으로 여러 장을 찍는다(shots/s-g-이름-번호.png). 그림으로 엮는 일은 SteamImage/make_gifs.py가 한다.
    // steamrun과 마찬가지로 게임의 기능만 부른다. 시세·주문·채팅을 직접 고치지 않는다.
    private static IEnumerable<float> GifRun()
    {
        foreach (float wait in Quiet(1f, "설명란 그림용 연속 장면을 찍음")) { yield return wait; }
        var note = current;
        var camera = Camera.main; var views = DeskViewController.Instance;
        var manager = StockManager.Instance; var pool = MothPool.Instance;
        if (camera == null || views == null || manager == null || pool == null) { note.notes.Add("(실패: 필요한 것을 찾지 못함)"); yield break; }
        Vector3 home = camera.transform.position; float homeFov = camera.fieldOfView;
        Vector3 Center(DeskViewTarget target) { var screen = views.ScreenFor(target); return screen.TransformPoint(screen.rect.center); }
        Vector3 chart = Center(DeskViewTarget.Chart), chat = Center(DeskViewTarget.Chat);
        var charts = Object.FindAnyObjectByType<ChartManager>();
        var cheap = new List<Stock>();
        foreach (var each in manager.stocks) { if (each != null && each.AcceptsOrders && each.currentPrice >= 300m) { cheap.Add(each); } }
        cheap.Sort((a, b) => a.currentPrice.CompareTo(b.currentPrice));
        if (cheap.Count < 2) { note.notes.Add("(실패: 종목이 모자람)"); yield break; }
        Stock surge = cheap[0], tactic = cheap[1];
        const int width = 924, height = 520;
        void Frame(string name, int index, Vector3 position, Vector3 look, float fov)
        {
            Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
            camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(look - position, Vector3.up); camera.fieldOfView = fov;
            Render(camera, "g-" + name + "-" + index.ToString("000"), width, height);
            camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
        }
        Vector3 closePosition = chart + new Vector3(0f, .1f, -3.3f), closeLook = chart + Vector3.down * .05f;
        Vector3 anglePosition = chart + new Vector3(-2.3f, .5f, -3.4f), angleLook = chart + new Vector3(.2f, -.1f, 0f);
        Vector3 chatPosition = chat + new Vector3(-.9f, .1f, -3.6f), chatLook = chat + new Vector3(-.5f, -.2f, 0f);

        // 1. 평소의 책상: 6초 동안 30장.
        manager.SelectStock(manager.stocks.IndexOf(tactic)); if (charts != null) { charts.SetChartPeriod(1); }
        yield return 3f;
        for (int i = 0; i < 30; i++) { Frame("desk", i, home, chart + Vector3.down * .2f, homeFov); yield return .2f; }

        // 2. 불붙이기: 차트를 보는 자리에서, 쓰기 1초 전부터 5초 뒤까지 48장.
        bool opened = PlayerProgression.Instance != null && PlayerProgression.Instance.OpenLeadingRooms();
        yield return 1f;
        bool lit = false; decimal before = tactic.currentPrice, top = before;
        for (int i = 0; i < 48; i++)
        {
            if (i == 8) { lit = PlayerTactics.Instance != null && PlayerTactics.Instance.Ignite(tactic, .7f); }
            if (tactic.currentPrice > top) { top = tactic.currentPrice; }
            Frame("ignite", i, closePosition, closeLook, 40f); yield return .125f;
        }
        note.notes.Add("(불붙이기: 리딩방 " + opened + " · 실행 " + lit + " · " + tactic.symbol + " " + before.ToString("N0") + " → 고점 " + top.ToString("N0") + " → " + tactic.currentPrice.ToString("N0") + ")");

        // 3. 급등: 몰리기 시작한 때부터 32초 동안 64장(차트 가까이).
        manager.SelectStock(manager.stocks.IndexOf(surge)); if (charts != null) { charts.SetChartPeriod(1); }
        yield return 2f;
        decimal began = surge.currentPrice; bool forced = pool.Force(surge, 260);
        for (int i = 0; i < 64; i++) { Frame("surge", i, closePosition, closeLook, 40f); yield return .5f; }
        note.notes.Add("(급등: " + surge.symbol + " " + began.ToString("N0") + " → " + surge.currentPrice.ToString("N0") + (forced ? "" : " (실패)") + ")");

        // 4. 채팅방: 급등 직후 16초 동안 48장.
        for (int i = 0; i < 48; i++) { Frame("chat", i, chatPosition, chatLook, 44f); yield return .33f; }
        // 5. 차트와 채팅이 함께: 12초 동안 40장.
        for (int i = 0; i < 40; i++) { Frame("room", i, anglePosition, angleLook, 42f); yield return .3f; }

        // 6. 달력: 목요일(4일)에서 금요일로, 금요일에서 다음 월요일로 넘어가는 모습. 선이 그어지는 동안 촘촘히 찍는다.
        var calendar = WallCalendar.Instance;
        if (calendar != null && calendar.Pose(out Vector3 center, out Vector3 normal, out float tall))
        {
            Vector3 calendarPosition = center + normal * tall * 1.55f + Vector3.up * .15f;
            calendar.DayOverride = 4; yield return 1.5f;
            int frame = 0;
            for (int i = 0; i < 6; i++) { Frame("calendar", frame++, calendarPosition, center + Vector3.up * .15f, 34f); yield return .07f; }
            calendar.DayOverride = 5;
            for (int i = 0; i < 16; i++) { Frame("calendar", frame++, calendarPosition, center + Vector3.up * .15f, 34f); yield return .07f; }
            calendar.DayOverride = 6;
            for (int i = 0; i < 34; i++) { Frame("calendar", frame++, calendarPosition, center + Vector3.up * .15f, 34f); yield return .07f; }
            calendar.DayOverride = 0;
        }
        findings.Add("gifrun: 찍음");
    }
    // ── 미리보기 동영상용 촬영 ──
    // "videorun": 게임 시간을 1/30초씩 나아가게 하고(Time.captureFramerate) 게임 프레임마다 1920×1080 한 장을 찍는다(shots/v-이름-번호.jpg).
    // 찍는 데 걸리는 시간과 상관없이 영상 속 게임은 제 속도로 흐른다. 엮는 일은 SteamImage/make_video.py가 한다.
    // 게임의 기능만 부른다. 시세·주문·채팅을 직접 고치지 않는다.
    private static RenderTexture videoTarget; private static Texture2D videoPixels;
    private static void VideoFrame(Camera camera, string name, int index, Vector3 position, Vector3 look, float fov)
    {
        const int width = 1920, height = 1080;
        if (videoTarget == null) { videoTarget = new RenderTexture(width, height, 24); videoPixels = new Texture2D(width, height, TextureFormat.RGB24, false); }
        Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
        camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(look - position, Vector3.up); camera.fieldOfView = fov;
        var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = videoTarget; camera.Render(); RenderTexture.active = videoTarget;
        videoPixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); videoPixels.Apply();
        Directory.CreateDirectory(Folder + "/shots");
        File.WriteAllBytes(Path.Combine(Folder + "/shots", "v-" + name + "-" + index.ToString("0000") + ".jpg"), videoPixels.EncodeToJPG(95));
        camera.targetTexture = old; RenderTexture.active = previous;
        camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
    }
    private static IEnumerable<float> VideoRun()
    {
        foreach (float wait in Quiet(1f, "미리보기 동영상용 장면을 찍음")) { yield return wait; }
        var note = current;
        var camera = Camera.main; var views = DeskViewController.Instance;
        var manager = StockManager.Instance; var pool = MothPool.Instance;
        if (camera == null || views == null || manager == null || pool == null) { note.notes.Add("(실패: 필요한 것을 찾지 못함)"); yield break; }
        Vector3 home = camera.transform.position; float homeFov = camera.fieldOfView;
        Vector3 Center(DeskViewTarget target) { var screen = views.ScreenFor(target); return screen.TransformPoint(screen.rect.center); }
        Vector3 chart = Center(DeskViewTarget.Chart), chat = Center(DeskViewTarget.Chat);
        var charts = Object.FindAnyObjectByType<ChartManager>();
        var cheap = new List<Stock>();
        foreach (var each in manager.stocks) { if (each != null && each.AcceptsOrders && each.currentPrice >= 300m) { cheap.Add(each); } }
        cheap.Sort((a, b) => a.currentPrice.CompareTo(b.currentPrice));
        if (cheap.Count < 2) { note.notes.Add("(실패: 종목이 모자람)"); yield break; }
        Stock surge = cheap[0], calm = cheap[1];
        int lastFrame = -1, skipped = 0, taken = 0;
        IEnumerable<float> Take(string name, int frames, Vector3 fromPosition, Vector3 toPosition, Vector3 fromLook, Vector3 toLook, float fromFov, float toFov, System.Action<int> each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                while (Time.frameCount == lastFrame) { yield return 0f; }
                if (lastFrame >= 0 && Time.frameCount - lastFrame > 1) { skipped += Time.frameCount - lastFrame - 1; }
                lastFrame = Time.frameCount;
                if (each != null) { each(i); }
                float t = frames > 1 ? i / (float)(frames - 1) : 0f; t = t * t * (3f - 2f * t);
                VideoFrame(camera, name, i, Vector3.Lerp(fromPosition, toPosition, t), Vector3.Lerp(fromLook, toLook, t), Mathf.Lerp(fromFov, toFov, t));
                taken++;
            }
        }
        IEnumerable<float> Pass(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) { yield return 0f; }
            lastFrame = -1;
        }
        Vector3 deskLook = chart + Vector3.down * .2f;
        Vector3 closePosition = chart + new Vector3(0f, .1f, -3.3f), closeLook = chart + Vector3.down * .05f;
        Vector3 anglePosition = chart + new Vector3(-2.3f, .5f, -3.4f), angleLook = chart + new Vector3(.2f, -.1f, 0f);
        Vector3 chatPosition = chat + new Vector3(-.9f, .1f, -3.6f), chatLook = chat + new Vector3(-.5f, -.2f, 0f);

        Time.captureFramerate = 30;
        manager.SelectStock(manager.stocks.IndexOf(calm)); if (charts != null) { charts.SetChartPeriod(1); }
        foreach (float wait in Pass(3f)) { yield return wait; }

        // 1. 책상: 멀리서 천천히 다가간다. 6초.
        foreach (float wait in Take("a-desk", 180, home + new Vector3(0f, .25f, -1.6f), home, deskLook, deskLook, homeFov, homeFov)) { yield return wait; }

        // 2. 급등: 차트 가까이에서 30초(영상에서는 빠르게 돌린다). 20번째 장에서 무리가 몰리기 시작한다.
        manager.SelectStock(manager.stocks.IndexOf(surge)); if (charts != null) { charts.SetChartPeriod(1); }
        foreach (float wait in Pass(2f)) { yield return wait; }
        decimal began = surge.currentPrice; bool forced = false;
        foreach (float wait in Take("b-surge", 900, closePosition + new Vector3(0f, 0f, -.5f), closePosition, closeLook, closeLook, 40f, 40f, i => { if (i == 20) { forced = pool.Force(surge, 260); } })) { yield return wait; }
        note.notes.Add("(급등: " + surge.symbol + " " + began.ToString("N0") + " → " + surge.currentPrice.ToString("N0") + (forced ? "" : " (실패)") + ")");

        // 3. 채팅방: 10초.
        foreach (float wait in Take("c-chat", 300, chatPosition + new Vector3(-.25f, 0f, -.3f), chatPosition, chatLook, chatLook, 44f, 44f)) { yield return wait; }

        // 4. 차트와 채팅이 함께: 옆으로 천천히 미끄러진다. 7초.
        foreach (float wait in Take("d-room", 210, anglePosition + new Vector3(-.5f, 0f, 0f), anglePosition + new Vector3(.4f, 0f, 0f), angleLook, angleLook, 42f, 42f)) { yield return wait; }

        // 5. 달력: 목요일 → 금요일 → 다음 월요일. 6초.
        var calendar = WallCalendar.Instance;
        if (calendar != null && calendar.Pose(out Vector3 center, out Vector3 normal, out float tall))
        {
            Vector3 calendarLook = center + Vector3.up * .15f, calendarPosition = center + normal * tall * 1.55f + Vector3.up * .15f;
            calendar.DayOverride = 4;
            foreach (float wait in Pass(1.5f)) { yield return wait; }
            foreach (float wait in Take("e-calendar", 180, calendarPosition + normal * tall * .35f, calendarPosition, calendarLook, calendarLook, 34f, 34f, i => { if (i == 20) { calendar.DayOverride = 5; } if (i == 65) { calendar.DayOverride = 6; } })) { yield return wait; }
            calendar.DayOverride = 0;
        }

        // 6. 끝: 책상에서 천천히 물러난다(제목이 얹힐 자리). 5초.
        foreach (float wait in Take("f-end", 150, home, home + new Vector3(0f, .3f, -1.8f), deskLook, deskLook, homeFov, homeFov)) { yield return wait; }

        Time.captureFramerate = 0;
        note.notes.Add("(동영상: " + taken + "장 · 건너뛴 게임 프레임 " + skipped + ")");
        findings.Add("videorun: 찍음");
    }

    // "propshot 이름": 이름에 그 글이 들어간 물건들을 하나씩 가까이서 찍는다(shots/s-prop-번호.png). 소품 모양 확인용.
    private static IEnumerable<float> PropShot(string word)
    {
        foreach (float wait in Quiet(1f, "소품을 가까이서 찍음: " + word)) { yield return wait; }
        var camera = Camera.main; int count = 0;
        if (camera == null) { current.notes.Add("(실패: 카메라 없음)"); yield break; }
        foreach (var each in Object.FindObjectsByType<Transform>())
        {
            if (!each.name.Contains(word) || !each.gameObject.activeInHierarchy) { continue; }
            var renderers = each.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { continue; }
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
            float reach = Mathf.Max(bounds.extents.magnitude, .05f);
            Vector3 position = bounds.center + new Vector3(reach * .6f, reach * 1.5f, -reach * 2.0f);
            Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
            camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(bounds.center - position, Vector3.up); camera.fieldOfView = 40f;
            Render(camera, "prop-" + count, 1600, 1000);
            camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
            current.notes.Add("(" + each.name + " · " + bounds.center.ToString("0.00") + " · 크기 " + bounds.size.ToString("0.00") + ")");
            count++;
            if (count >= 6) { break; }
        }
        current.notes.Add("(소품 " + count + "개 찍음)");
    }
    // "shadowcheck": 그림자가 왜 안 보이는지 알아보는 확인용. 빛과 그림자 설정을 적고, 책상을 위에서 세 번 찍는다
    // (그대로 / 카메라에 달린 빛을 끄고 / 위에서 내리쬐는 시험용 빛을 하나 더 켜고). 찍은 뒤에는 모두 되돌린다.
    private static IEnumerable<float> ShadowCheck()
    {
        foreach (float wait in Quiet(2f, "그림자 설정을 확인함")) { yield return wait; }
        var note = current; var camera = Camera.main;
        if (camera == null) { note.notes.Add("(실패: 카메라 없음)"); yield break; }
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        note.notes.Add("(파이프라인 " + (pipeline == null ? "없음" : pipeline.name) + " · 품질 " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " · 그래픽 장치 " + SystemInfo.graphicsDeviceType + ")");
        foreach (var light in Object.FindObjectsByType<Light>())
        {
            if (!light.isActiveAndEnabled) { continue; }
            note.notes.Add("(빛 " + light.name + " · " + light.type + " · 세기 " + light.intensity.ToString("0.00") + " · 거리 " + light.range.ToString("0.0") + " · 그림자 " + light.shadows + " · 자리 " + light.transform.position.ToString("0.0") + ")");
        }
        var body = GameObject.Find("desk_body");
        if (body == null) { note.notes.Add("(실패: 책상 없음)"); yield break; }
        var bounds = body.GetComponent<Renderer>().bounds;
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        Vector3 position = top + new Vector3(0f, 2.6f, -3.2f);
        void Shot(string name)
        {
            Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
            camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(top - position, Vector3.up); camera.fieldOfView = 50f;
            Render(camera, "shadow-" + name, 1920, 1080);
            camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
        }
        Shot("0-asis");
        var carried = camera.GetComponentInChildren<Light>();
        if (carried != null) { carried.enabled = false; yield return .5f; Shot("1-no-camera-light"); carried.enabled = true; }
        var test = new GameObject("Light :: Shadow Test").AddComponent<Light>();
        test.type = LightType.Spot; test.spotAngle = 80f; test.range = 12f; test.intensity = 6f; test.shadows = LightShadows.Soft;
        test.transform.position = top + new Vector3(-1.5f, 4f, -1f); test.transform.rotation = Quaternion.LookRotation(top - test.transform.position, Vector3.up);
        yield return .5f; Shot("2-test-spot");
        Object.Destroy(test.gameObject);
        var sun = new GameObject("Light :: Shadow Test Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.5f; sun.shadows = LightShadows.Hard; sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        yield return 1f; Shot("3-test-sun");
        var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var asset = pipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        note.notes.Add("(카메라 그림자 " + (data == null ? "자료 없음" : data.renderShadows.ToString()) + " · 추가 빛 그림자 지원 " + (asset == null ? "?" : asset.supportsAdditionalLightShadows.ToString()) + " · 주 빛 그림자 지원 " + (asset == null ? "?" : asset.supportsMainLightShadows.ToString()) + " · 그림자 거리 " + (asset == null ? "?" : asset.shadowDistance.ToString("0")) + " · QualitySettings.shadows " + QualitySettings.shadows + ")");
        Object.Destroy(sun.gameObject);
    }
    // "tabletshot": 태블릿을 하나씩 들어 올려, 자리에 앉은 시점 그대로 찍는다(shots/s-tablet-번호.png). 들고 보는 자세 확인용.
    private static IEnumerable<float> TabletShot()
    {
        foreach (float wait in Quiet(1f, "태블릿을 들고 찍음")) { yield return wait; }
        var camera = Camera.main; int count = 0;
        if (camera == null) { current.notes.Add("(실패: 카메라 없음)"); yield break; }
        foreach (var tablet in Object.FindObjectsByType<TabletViewController>())
        {
            if (tablet.tabletObject == null) { continue; }
            tablet.FocusTablet();
            yield return 1.5f;
            Render(camera, "tablet-" + count, 1920, 1080);
            current.notes.Add("(" + tablet.name + " · 들림 " + tablet.IsHeld + " · 자리 " + tablet.tabletObject.localPosition.ToString("0.00") + " · 각도 " + tablet.tabletObject.localEulerAngles.ToString("0") + ")");
            tablet.ReturnTablet();
            yield return 2f;
            count++;
        }
        current.notes.Add("(태블릿 " + count + "개 찍음)");
    }
    // "entryshot": 보이는 입력 칸을 몇 개 골라 글쓰기 상태로 만든 뒤 가까이서 찍는다(shots/s-entry-번호.png). 입력 중 강조 확인용.
    private static IEnumerable<float> EntryShot()
    {
        foreach (float wait in Quiet(1f, "입력 칸을 잡고 찍음")) { yield return wait; }
        var camera = Camera.main; int count = 0;
        if (camera == null) { current.notes.Add("(실패: 카메라 없음)"); yield break; }
        foreach (var field in Object.FindObjectsByType<TMPro.TMP_InputField>())
        {
            if (!field.isActiveAndEnabled || !field.interactable) { continue; }
            var rect = (RectTransform)field.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * .5f; float wide = Vector3.Distance(corners[0], corners[3]);
            if (wide < .01f) { continue; }
            field.ActivateInputField();
            yield return .6f;
            Vector3 position = center - rect.forward * wide * 1.6f;
            Vector3 keptPosition = camera.transform.position; Quaternion keptRotation = camera.transform.rotation; float keptFov = camera.fieldOfView;
            camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(center - position, rect.up); camera.fieldOfView = 40f;
            Render(camera, "entry-" + count, 1600, 700);
            camera.transform.SetPositionAndRotation(keptPosition, keptRotation); camera.fieldOfView = keptFov;
            current.notes.Add("(" + field.name + " · 쓰는 중 " + field.isFocused + " · 강조 대상 " + (MouseCameraController.ActiveTextEntry == field) + ")");
            MouseCameraController.EndTextEntry();
            yield return .3f;
            current.notes.Add("(끝낸 뒤 쓰는 중 " + field.isFocused + ")");
            count++;
            if (count >= 3) { break; }
        }
        current.notes.Add("(입력 칸 " + count + "개 찍음)");
    }
    // ── 프롤로그 확인과 녹화 ──
    // 환경 변수 DONDON_PROBE_INTRO=1이면 점검 도구가 NewGame.Begin을 직접 부르지 않고, 타이틀의 "새 게임" 단추와 같은 길(PauseMenuController.StartNewGame)로 시작한다.
    // 그러면 프롤로그가 실제 흐름대로 뜬다. 그동안 재생 시각을 1/30초씩 손으로 넘기며 한 장씩 찍는다(shots/intro-번호.jpg).
    // 프롤로그 화면은 화면에 덧그리는 Canvas라 게임 카메라로는 찍히지 않는다. 찍는 동안만 전용 카메라에 걸어 그림으로 받는다.
    private static readonly bool introMode = Environment.GetEnvironmentVariable("DONDON_PROBE_INTRO") == "1";
    private static Camera introCamera; private static RenderTexture introTarget; private static Texture2D introPixels;
    private static int introFrame = -1, introFinishBefore; private static bool introStarted, introReported; private static bool introResumed; private static bool introDayLogged;
    private static string introLastScene = "", introLastCaption = ""; private static bool introLastGoal, introLastSkip;
    private static double introClockAtStart; private static float introScaleAtStart; private static string introHomeLanguage;
    // 대본이 시작될 때 findings가 비워지므로, 프롤로그 동안 적은 것은 따로 모아 두었다가 introcheck에서 보고서에 옮긴다.
    private static readonly List<string> introNotes = new();
    private static void IntroAim(StoryIntro intro, int width, int height)
    {
        if (introCamera == null)
        {
            introCamera = new GameObject("Camera :: Intro Capture").AddComponent<Camera>(); introCamera.enabled = false;
            introCamera.clearFlags = CameraClearFlags.SolidColor; introCamera.backgroundColor = Color.black; introCamera.cullingMask = 1 << 5;
            introCamera.transform.position = new Vector3(5000f, 5000f, 5000f);
        }
        if (introTarget == null || introTarget.width != width || introTarget.height != height)
        {
            if (introTarget != null) { introCamera.targetTexture = null; introTarget.Release(); Object.DestroyImmediate(introTarget); Object.DestroyImmediate(introPixels); }
            introTarget = new RenderTexture(width, height, 24); introPixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        }
        introCamera.targetTexture = introTarget;
        foreach (var item in intro.GetComponentsInChildren<Transform>(true)) { item.gameObject.layer = 5; }
        intro.Canvas.renderMode = RenderMode.ScreenSpaceCamera; intro.Canvas.worldCamera = introCamera; intro.Canvas.planeDistance = 1f;
    }
    private static void IntroSave(string name, bool jpg)
    {
        Canvas.ForceUpdateCanvases();
        var previous = RenderTexture.active;
        introCamera.Render(); RenderTexture.active = introTarget;
        introPixels.ReadPixels(new Rect(0, 0, introTarget.width, introTarget.height), 0, 0); introPixels.Apply();
        RenderTexture.active = previous;
        Directory.CreateDirectory(Folder + "/shots");
        File.WriteAllBytes(Path.Combine(Folder + "/shots", name + (jpg ? ".jpg" : ".png")), jpg ? introPixels.EncodeToJPG(95) : introPixels.EncodeToPNG());
    }
    /// <summary>실제 새 게임 흐름으로 뜬 프롤로그를 처음부터 끝까지 한 장씩 찍는다. 매 편집기 틱마다 한 번 불린다.</summary>
    private static void IntroDrive()
    {
        var intro = StoryIntro.Instance; var day = MarketDayController.Instance;
        if (!introStarted)
        {
            introStarted = true; introFrame = -1; introFinishBefore = StoryIntro.FinishCount;
            introClockAtStart = day == null ? -1 : day.ExactGameMinute; introScaleAtStart = Time.timeScale;
            IntroAim(intro, 1920, 1080);
            introNotes.Add("프롤로그 시작: 게임 시계 " + introClockAtStart.ToString("0.000") + "분 · 시계 배율 " + introScaleAtStart + " · 음악 " + (intro.MusicPlaying ? "재생 중" : "없음") + " · 길이 " + intro.Duration.ToString("0.000") + "초 · 다른 소리 멈춤 " + AudioListener.pause);
            intro.ManualClock = 0f;
            return;
        }
        // 메뉴가 닫힌(방으로 들어간) 바로 그 틱의 게임 시계. 검은 화면이 아직 덮고 있는 동안이다.
        if (!introResumed && !PauseMenuController.IsOpen)
        {
            introResumed = true;
            introNotes.Add("메뉴가 닫힌 순간: 게임 시계 " + (day == null ? "?" : day.ExactGameMinute.ToString("0.000")) + "분 · 시계 배율 " + Time.timeScale + " · 끝맺는 중 " + intro.Ending + " · 침대에서 깨어나는 연출 " + (BedSleep.Instance != null && BedSleep.Instance.Stage != 0 ? "시작됨" : "없음"));
        }
        if (intro.Ending) { return; }
        // 앞 틱에 맞춰 둔 시각이 이번 프레임에 그려졌다. 그것을 찍고, 바뀐 것을 적은 뒤 다음 시각으로 넘긴다.
        introFrame++;
        float at = introFrame / 30f;
        if (intro.ShownScene != introLastScene) { introLastScene = intro.ShownScene; introNotes.Add("  " + at.ToString("00.000") + "초 장면 → " + (introLastScene == "" ? "(없음)" : introLastScene)); }
        if (intro.ShownCaptionKey != introLastCaption) { introLastCaption = intro.ShownCaptionKey; introNotes.Add("  " + at.ToString("00.000") + "초 자막 → " + (introLastCaption == "" ? "(없음)" : introLastCaption)); }
        if (intro.ShownGoalAlpha > 0f != introLastGoal) { introLastGoal = intro.ShownGoalAlpha > 0f; introNotes.Add("  " + at.ToString("00.000") + "초 목표 표시 → " + (introLastGoal ? "보임" : "숨김")); }
        if (intro.SkipVisible != introLastSkip) { introLastSkip = intro.SkipVisible; introNotes.Add("  " + at.ToString("00.000") + "초 건너뛰기 단추 → " + (introLastSkip ? "보임" : "숨김")); }
        if (introFrame < 1800) { IntroSave("intro-" + introFrame.ToString("0000"), true); }
        intro.ManualClock = (introFrame + 1) / 30f;
    }
    /// <summary>프롤로그가 끝나고 메뉴가 닫힌 순간의 상태를 한 번 적는다.</summary>
    private static void IntroReport()
    {
        introReported = true;
        if (introHomeLanguage != null) { try { GameLocale.SetLanguage(introHomeLanguage); } catch (Exception) { } introHomeLanguage = null; }
        var day = MarketDayController.Instance; var accounts = PlayerAccounts.Instance;
        introNotes.Add("프롤로그 끝: 찍은 장면 " + (introFrame + 1) + "장 · 끝맺음 횟수 " + (StoryIntro.FinishCount - introFinishBefore) + " · 남은 프롤로그 화면 " + (GameObject.Find("Area :: Story Intro") != null ? "있음" : "없음") + " · 다른 소리 멈춤 " + AudioListener.pause);
        introNotes.Add("게임 시작 순간: 게임 시계 " + (day == null ? "?" : day.ExactGameMinute.ToString("0.000") + "분 (" + (day.ClockMinute / 60).ToString("00") + ":" + (day.ClockMinute % 60).ToString("00") + ", " + day.Phase + ")") + " · 프롤로그 직전 " + introClockAtStart.ToString("0.000") + "분 · 시계 배율 " + Time.timeScale);
        if (accounts != null && accounts.primary != null)
        {
            decimal cash = accounts.primary.cash, equity = accounts.primary.Equity();
            introNotes.Add("게임 시작 순간: 1번 계좌 현금 " + cash.ToString("N0") + " · 평가 자산 합계 " + equity.ToString("N0") + " · 회사 평가액(자산 − 현금) " + (equity - cash).ToString("N0"));
        }
    }
    // "introcheck": 게임이 시작된 뒤에 프롤로그를 다시 틀어 본다(실제 게임에는 이런 길이 없다. 확인용).
    //   1) 1.2초에 건너뛰기, 59.2초에 건너뛰기: 끝맺음이 한 번씩만 불리는지, 화면과 소리 멈춤이 남지 않는지.
    //   2) 언어 셋 × 장면 열둘: 자막이 잘리지 않는지, 글자 크기와 줄 수.
    //   3) 화면 비율 넷에서 찍어 본다(shots/introcheck-*.png).
    private static IEnumerable<float> IntroCheck()
    {
        foreach (float wait in Quiet(1f, "프롤로그의 건너뛰기·언어·화면 비율을 확인함")) { yield return wait; }
        var note = current; string home = GameLocale.Code;
        foreach (string line in introNotes) { note.notes.Add(line); }
        var dayIntro = DayIntroOverlay.Instance; var clockNow = MarketDayController.Instance;
        note.notes.Add("(DAY 글자: 보여 준 날 " + (dayIntro == null ? "?" : dayIntro.ShownDay.ToString()) + " · 지금 " + (clockNow == null ? "?" : (clockNow.ClockMinute / 60).ToString("00") + ":" + (clockNow.ClockMinute % 60).ToString("00") + " " + clockNow.Phase) + ")");
        note.notes.Add("(침대에서 깨어나는 연출: " + (BedSleep.Instance == null ? "없음" : "단계 " + BedSleep.Instance.Stage) + " · 화면 잠금 " + RoomShell.Cinematic + ")");
        foreach (float moment in new[] { 1.2f, 59.2f })
        {
            int before = StoryIntro.FinishCount, called = 0; bool pausedBefore = AudioListener.pause;
            StoryIntro.Play(() => { called++; });
            var intro = StoryIntro.Instance;
            if (intro == null) { note.notes.Add("(실패: 프롤로그가 뜨지 않음)"); yield break; }
            intro.ManualClock = moment; yield return .3f;
            string seen = intro.ShownScene; bool skipShown = intro.SkipVisible, playing = intro.MusicPlaying;
            intro.Skip(); intro.Skip(); yield return .4f;
            bool frozen = intro != null && intro.Ending, stillPlaying = StoryIntro.Playing; int early = called; string held = intro != null ? intro.ShownScene : "";
            yield return 1.6f;
            note.notes.Add("(건너뛴 0.4초 뒤: 멈춰서 어두워지는 중 " + frozen + " · 장면 그대로 " + (held == seen) + " · 아직 화면 있음 " + stillPlaying + " · 맡긴 일 " + early + "번)");
            note.notes.Add("(" + moment.ToString("0.0") + "초에 건너뛰기: 그때 장면 " + seen + " · 안내 글 " + (skipShown ? "보임" : "숨김") + " · 음악 " + (playing ? "재생 중" : "없음") + " → 끝맺음 " + (StoryIntro.FinishCount - before) + "번 · 맡긴 일 " + called + "번 · 재생 중 " + StoryIntro.Playing + " · 남은 화면 " + (GameObject.Find("Area :: Story Intro") != null ? "있음" : "없음") + " · 소리 멈춤 " + pausedBefore + "→" + AudioListener.pause + ")");
        }
        int ended = 0; StoryIntro.Play(() => { ended++; });
        var show = StoryIntro.Instance;
        if (show == null) { note.notes.Add("(실패: 프롤로그가 뜨지 않음)"); yield break; }
        show.ManualClock = 0f; IntroAim(show, 1920, 1080); yield return .3f;
        var caption = show.transform.Find("Area :: Intro Stage/Text :: Intro Narration").GetComponent<TMPro.TextMeshProUGUI>();
        float[] middles = { 2.5f, 7f, 11.5f, 15.5f, 20f, 25.5f, 31f, 36f, 40.5f, 45.5f, 50.5f, 55f };
        foreach (string code in new[] { "ko", "en", "ja" })
        {
            GameLocale.SetLanguage(code); yield return .3f;
            var line = new StringBuilder("(" + code + " 자막:");
            for (int i = 0; i < middles.Length; i++)
            {
                show.ManualClock = middles[i]; yield return .12f;
                caption.ForceMeshUpdate();
                line.Append(" " + (i + 1) + "번 " + caption.fontSize.ToString("0") + "px/" + caption.textInfo.lineCount + "줄" + (caption.isTextTruncated ? "(잘림!)" : "") + (show.ShownCaptionAlpha >= 1f ? "" : "(흐림 " + show.ShownCaptionAlpha.ToString("0.0") + ")"));
            }
            note.notes.Add(line + ")");
            foreach (float moment in new[] { 25.5f, 31f, 45.5f, 58.2f })
            { show.ManualClock = moment; yield return .12f; IntroSave("introcheck-" + code + "-1920x1080-" + moment.ToString("00.0"), false); }
        }
        // 화면 비율: 2560×1440(16:9), 1920×1200(16:10), 2560×1080(21:9). 일본어(가장 좁게 들어가는 글)로 본다.
        foreach (var size in new[] { new Vector2Int(2560, 1440), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080) })
        {
            IntroAim(show, size.x, size.y); yield return .3f;
            foreach (float moment in new[] { 31f, 58.2f })
            {
                show.ManualClock = moment; yield return .15f; caption.ForceMeshUpdate();
                IntroSave("introcheck-ja-" + size.x + "x" + size.y + "-" + moment.ToString("00.0"), false);
                var corners = new Vector3[4]; caption.rectTransform.GetWorldCorners(corners);
                Vector3 low = introCamera.WorldToViewportPoint(corners[0]), high = introCamera.WorldToViewportPoint(corners[2]);
                note.notes.Add("(" + size.x + "×" + size.y + " · " + moment.ToString("0.0") + "초: 자막 칸이 화면의 가로 " + (low.x * 100f).ToString("0") + "~" + (high.x * 100f).ToString("0") + "%, 세로 " + (low.y * 100f).ToString("0") + "~" + (high.y * 100f).ToString("0") + "% · 잘림 " + caption.isTextTruncated + ")");
            }
        }
        GameLocale.SetLanguage(home);
        show.ManualClock = 60.5f; yield return 1.6f;
        note.notes.Add("(끝까지 재생: 맡긴 일 " + ended + "번 · 재생 중 " + StoryIntro.Playing + " · 소리 멈춤 " + AudioListener.pause + ")");
        findings.Add("introcheck: 확인함");
    }
    // "laddercheck": 세력의 잘게 쪼갠 매도 사다리를 직접 불러 본다(작전이 그 단계에 이를 때까지 기다리지 않는다).
    // 사다리를 깐 뒤의 매도 호가 10칸과 내 주문을 적고, 불나방 무리로 값을 올린 뒤 다시 깔아 사다리가 위로 옮겨 가는지 본다.
    private static IEnumerable<float> LadderCheck()
    {
        foreach (float wait in Quiet(1f, "세력의 매도 사다리를 확인함")) { yield return wait; }
        var note = current; WhaleBot whale = null; Stock stock = null;
        foreach (var each in Object.FindObjectsByType<WhaleBot>())
        {
            var owned = each.OwnedCompany;
            if (owned != null && owned.AcceptsOrders && each.GetHolding(owned) >= 5000m) { whale = each; stock = owned; break; }
        }
        if (whale == null) { note.notes.Add("(실패: 주식을 가진 세력을 찾지 못함)"); yield break; }
        var lay = typeof(WhaleBot).GetMethod("LayAskLadder", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var own = new List<Order>(); var levels = new List<(decimal price, decimal volume)>();
        string Book()
        {
            levels.Clear(); stock.orderBook.GetTopAsks(levels, 10);
            stock.orderBook.CopyOwnerOrders(whale, own);
            var mine = new Dictionary<decimal, decimal>(); int count = 0;
            foreach (var order in own) { if (!order.isBuy && stock.orderBook.IsActive(order)) { count++; mine.TryGetValue(order.price, out decimal had); mine[order.price] = had + order.volume; } }
            var line = new StringBuilder("현재가 " + stock.currentPrice.ToString("N0") + " · 매도 호가 " + levels.Count + "칸 · 세력의 매도 " + count + "건 |");
            foreach (var level in levels) { line.Append(" " + level.price.ToString("N0") + ":" + level.volume.ToString("N0") + (mine.ContainsKey(level.price) ? "(세력 " + mine[level.price].ToString("N0") + ")" : "")); }
            return line.ToString();
        }
        note.notes.Add("(" + whale.name + " · " + stock.symbol + " · 보유 " + whale.GetHolding(stock).ToString("N0") + "주)");
        note.notes.Add("(깔기 전: " + Book() + ")");
        object laid = lay.Invoke(whale, new object[] { stock, 3000m, 0m });
        note.notes.Add("(3,000주를 깔았다 → 건 수량 " + laid + ": " + Book() + ")");
        laid = lay.Invoke(whale, new object[] { stock, 3000m, 0m });
        note.notes.Add("(바로 다시 불렀다(빈 칸이 없으면 더 얹지 않아야 한다) → 건 수량 " + laid + ": " + Book() + ")");
        bool forced = MothPool.Instance != null && MothPool.Instance.Force(stock, 120);
        for (int i = 0; i < 6; i++)
        {
            yield return 2.5f;
            laid = lay.Invoke(whale, new object[] { stock, 3000m, 0m });
            note.notes.Add("(무리가 사는 중" + (forced ? "" : "(실패)") + " " + ((i + 1) * 2.5f).ToString("0.0") + "초 → 건 수량 " + laid + ": " + Book() + ")");
        }
        findings.Add("laddercheck: 확인함");
    }
    // ── 미리보기 동영상용 촬영(2배속) ──
    // "videorun2": 게임 시간을 1/15초씩 나아가게 하고(Time.captureFramerate = 15) 게임 프레임마다 1920×1080 한 장을 찍는다.
    // 초당 30장으로 틀면 꼭 2배속이다(중간 프레임을 버리지 않고 처음부터 2배속으로 찍는다). 엮는 일은 SteamImage/make_video.py가 한다.
    // 차트 장면은 한 번에 이어 찍는다: 무리가 몰려 값이 goal(1,000)을 넘으면, 그 종목의 세력(대주주)이 물량을 매수 호가에 내려찍어 무너뜨린다.
    // 전부 게임의 기능이다(불나방 무리, 세력의 내려찍기). 시세·주문·채팅을 직접 고치지 않는다.
    private static IEnumerable<float> VideoRun2()
    {
        foreach (float wait in Quiet(1f, "미리보기 동영상용 장면을 2배속으로 찍음")) { yield return wait; }
        var note = current;
        var camera = Camera.main; var views = DeskViewController.Instance;
        var manager = StockManager.Instance; var pool = MothPool.Instance;
        if (camera == null || views == null || manager == null || pool == null) { note.notes.Add("(실패: 필요한 것을 찾지 못함)"); yield break; }
        Vector3 home = camera.transform.position; float homeFov = camera.fieldOfView;
        Vector3 Center(DeskViewTarget target) { var screen = views.ScreenFor(target); return screen.TransformPoint(screen.rect.center); }
        Vector3 chart = Center(DeskViewTarget.Chart), chat = Center(DeskViewTarget.Chat);
        var charts = Object.FindAnyObjectByType<ChartManager>();
        var cheap = new List<Stock>();
        foreach (var each in manager.stocks) { if (each != null && each.AcceptsOrders && each.currentPrice >= 300m) { cheap.Add(each); } }
        cheap.Sort((a, b) => a.currentPrice.CompareTo(b.currentPrice));
        if (cheap.Count < 2) { note.notes.Add("(실패: 종목이 모자람)"); yield break; }
        Stock surge = cheap[0], calm = cheap[1];
        WhaleBot whale = null;
        foreach (var each in Object.FindObjectsByType<WhaleBot>()) { if (each.OwnedCompany == surge && each.GetHolding(surge) >= 1000m) { whale = each; break; } }
        int lastFrame = -1, skipped = 0, taken = 0;
        IEnumerable<float> Next()
        {
            while (Time.frameCount == lastFrame) { yield return 0f; }
            if (lastFrame >= 0 && Time.frameCount - lastFrame > 1) { skipped += Time.frameCount - lastFrame - 1; }
            lastFrame = Time.frameCount;
        }
        IEnumerable<float> Take(string name, int frames, Vector3 fromPosition, Vector3 toPosition, Vector3 fromLook, Vector3 toLook, float fov, System.Action<int> each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                foreach (float wait in Next()) { yield return wait; }
                if (each != null) { each(i); }
                float t = frames > 1 ? i / (float)(frames - 1) : 0f; t = t * t * (3f - 2f * t);
                VideoFrame(camera, name, i, Vector3.Lerp(fromPosition, toPosition, t), Vector3.Lerp(fromLook, toLook, t), fov);
                taken++;
            }
        }
        IEnumerable<float> Pass(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) { yield return 0f; }
            lastFrame = -1;
        }
        Vector3 deskLook = chart + Vector3.down * .2f;
        Vector3 closePosition = chart + new Vector3(0f, .1f, -3.3f), closeLook = chart + Vector3.down * .05f;
        Vector3 anglePosition = chart + new Vector3(-2.3f, .5f, -3.4f), angleLook = chart + new Vector3(.2f, -.1f, 0f);
        Vector3 chatPosition = chat + new Vector3(-.9f, .1f, -3.6f), chatLook = chat + new Vector3(-.5f, -.2f, 0f);

        Time.captureFramerate = 15;
        manager.SelectStock(manager.stocks.IndexOf(calm)); if (charts != null) { charts.SetChartPeriod(1); }
        foreach (float wait in Pass(3f)) { yield return wait; }

        // 1. 책상: 멀리서 다가간다. 105장(영상 3.5초).
        foreach (float wait in Take("a-desk", 105, home + new Vector3(0f, .25f, -1.6f), home, deskLook, deskLook, homeFov)) { yield return wait; }

        // 2. 차트: 급등 → goal 돌파 → 내려찍기 → 폭락까지 한 번에.
        // 무리가 몰려도 goal 근처까지 오르는 데는 1분쯤 걸린다. 그동안은 찍지 않고 기다렸다가(차트에는 그동안의 캔들이 그대로 남는다),
        // goal의 85%를 넘으면 찍기 시작한다. goal을 3% 넘게 뚫으면 무너뜨린다: 무리의 열기를 식히고, 세력이 물량을 매수 호가에 잇달아 내려찍는다.
        const decimal goal = 1000m; const int MinClimb = 90, MaxClimb = 480, CrashFrames = 240, SlamEvery = 6, SlamCount = 40, HoldFrames = 36;
        manager.SelectStock(manager.stocks.IndexOf(surge)); if (charts != null) { charts.SetChartPeriod(1); }
        foreach (float wait in Pass(2f)) { yield return wait; }
        decimal began = surge.currentPrice, peak = began, low = 0m, sold = 0m; int trigger = -1, index = 0, slams = 0, above = 0;
        bool forced = pool.Force(surge, 450); float rolledAt = Time.time;
        while (surge.currentPrice < goal * .85m && Time.time - rolledAt < 150f) { yield return 0f; }
        float waited = Time.time - rolledAt; decimal startedAt = surge.currentPrice; lastFrame = -1;
        while (index < (trigger < 0 ? MaxClimb : trigger + CrashFrames))
        {
            foreach (float wait in Next()) { yield return wait; }
            if (trigger < 0 && surge.currentPrice > peak) { peak = surge.currentPrice; }
            // goal 위에서 HoldFrames장(영상 1.2초) 동안 버틴 뒤에 무너뜨린다. 한 장 스치고 지나간 꼬리는 넘긴 것으로 치지 않는다.
            above = surge.currentPrice >= goal ? above + 1 : 0;
            if (trigger < 0 && (index >= MinClimb && above >= HoldFrames || index >= MaxClimb - 1)) { trigger = index; low = surge.currentPrice; pool.Cool(surge); }
            if (trigger >= 0)
            {
                int since = index - trigger;
                if (whale != null && slams < SlamCount && since == slams * SlamEvery) { sold += ManipulationTools.Slam(whale, surge, decimal.Floor(whale.GetHolding(surge) * .25m), .12m, 0m); slams++; }
                if (surge.currentPrice < low) { low = surge.currentPrice; }
            }
            float t = Mathf.Clamp01(index / 120f); t = t * t * (3f - 2f * t);
            VideoFrame(camera, "b-chart", index, Vector3.Lerp(closePosition + new Vector3(0f, 0f, -.5f), closePosition, t), closeLook, 40f);
            taken++; index++;
        }
        File.WriteAllText(Path.Combine(Folder + "/shots", "video-meta.txt"), "chart_frames=" + index + " crash_at=" + trigger);
        note.notes.Add("(차트: " + surge.symbol + " " + began.ToString("N0") + " → (기다린 " + waited.ToString("0") + "초 뒤 " + startedAt.ToString("N0") + "에서 찍기 시작) → 고점 " + peak.ToString("N0") + " → 저점 " + low.ToString("N0") + " → 끝 " + surge.currentPrice.ToString("N0") + " · 무리 " + (forced ? "몰림" : "실패") + " · 내려찍기 " + (whale == null ? "세력 없음" : slams + "번, " + sold.ToString("N0") + "주") + " · 무너뜨린 장 " + trigger + " / 전체 " + index + "장)");

        // 3. 채팅방(무너진 직후의 반응): 300장. 4. 차트와 채팅이 함께: 300장. 영상에는 남는 길이만큼만 쓴다.
        foreach (float wait in Take("c-chat", 300, chatPosition + new Vector3(-.25f, 0f, -.3f), chatPosition, chatLook, chatLook, 44f)) { yield return wait; }
        foreach (float wait in Take("d-room", 300, anglePosition + new Vector3(-.5f, 0f, 0f), anglePosition + new Vector3(.4f, 0f, 0f), angleLook, angleLook, 42f)) { yield return wait; }

        // 5. 달력: 목요일 → 금요일 → 다음 월요일. 105장.
        var calendar = WallCalendar.Instance;
        if (calendar != null && calendar.Pose(out Vector3 center, out Vector3 normal, out float tall))
        {
            Vector3 calendarLook = center + Vector3.up * .15f, calendarPosition = center + normal * tall * 1.55f + Vector3.up * .15f;
            calendar.DayOverride = 4;
            foreach (float wait in Pass(1.5f)) { yield return wait; }
            foreach (float wait in Take("e-calendar", 105, calendarPosition + normal * tall * .35f, calendarPosition, calendarLook, calendarLook, 34f, i => { if (i == 12) { calendar.DayOverride = 5; } if (i == 38) { calendar.DayOverride = 6; } })) { yield return wait; }
            calendar.DayOverride = 0;
        }

        // 6. 끝: 책상에서 물러난다(제목이 얹힐 자리). 135장.
        foreach (float wait in Take("f-end", 135, home, home + new Vector3(0f, .3f, -1.8f), deskLook, deskLook, homeFov)) { yield return wait; }

        Time.captureFramerate = 0;
        note.notes.Add("(동영상: " + taken + "장 · 건너뛴 게임 프레임 " + skipped + ")");
        findings.Add("videorun2: 찍음");
    }

    private static IEnumerable<float> SteamRun()
    {
        foreach (float wait in Quiet(1f, "상점 이미지용 장면을 찍음")) { yield return wait; }
        var note = current;
        var camera = Camera.main; var views = DeskViewController.Instance; var desk = GameObject.Find("desk_body");
        var manager = StockManager.Instance; var accounts = PlayerAccounts.Instance; var pool = MothPool.Instance;
        if (camera == null || views == null || desk == null || manager == null || accounts == null || pool == null) { note.notes.Add("(실패: 필요한 것을 찾지 못함)"); yield break; }
        Vector3 home = camera.transform.position; float homeFov = camera.fieldOfView;
        Vector3 Center(DeskViewTarget target) { var screen = views.ScreenFor(target); return screen.TransformPoint(screen.rect.center); }
        Vector3 chart = Center(DeskViewTarget.Chart), chat = Center(DeskViewTarget.Chat);
        var charts = Object.FindAnyObjectByType<ChartManager>();
        // 값이 싼 순서로 두 종목: 가장 싼 것은 급등용, 그다음은 전술용.
        var cheap = new List<Stock>();
        foreach (var each in manager.stocks) { if (each != null && each.AcceptsOrders && each.currentPrice >= 300m) { cheap.Add(each); } }
        cheap.Sort((a, b) => a.currentPrice.CompareTo(b.currentPrice));
        if (cheap.Count < 2) { note.notes.Add("(실패: 종목이 모자람)"); yield break; }
        Stock surge = cheap[0], tactic = cheap[1];
        void Seat(string name) { RenderFrom(camera, name, home, chart + Vector3.down * .2f, homeFov); }
        void ChartClose(string name) { RenderFrom(camera, name, chart + new Vector3(0f, .1f, -3.3f), chart + Vector3.down * .05f, 40f); }
        void ChartAngle(string name) { RenderFrom(camera, name, chart + new Vector3(-2.3f, .5f, -3.4f), chart + new Vector3(.2f, -.1f, 0f), 42f); }
        void ChatFront(string name) { RenderFrom(camera, name, chat + new Vector3(-.9f, .1f, -3.6f), chat + new Vector3(-.5f, -.2f, 0f), 44f); }
        void Profile()
        {
            var market = MarketDayController.Instance; int chosen = 0;
            for (int i = market.MessageCount - 1; i >= 0 && chosen == 0; i--) { var message = market.GetMessage(i); if (message.Social != null && message.BotId > 0) { chosen = message.BotId; } }
            foreach (var board in Object.FindObjectsByType<TabletGroupChatUI>()) { if (board != null && board.monitorChat && chosen != 0) { board.SelectProfile(chosen); } }
        }

        // 1. 잔잔한 장: 평소의 책상.
        manager.SelectStock(manager.stocks.IndexOf(tactic)); if (charts != null) { charts.SetChartPeriod(1); }
        yield return 3f;
        Seat("calm-seat"); RenderFrom(camera, "calm-seathigh", home + new Vector3(0f, 1.3f, .9f), chart + Vector3.down * 1.0f, 60f);

        // 2. 세력 전술: 리딩방을 열어 불붙이기를 열고, 둘째 태블릿의 작전 화면을 든 채로 쓴다.
        bool opened = PlayerProgression.Instance != null && PlayerProgression.Instance.OpenLeadingRooms();
        TabletAccountUI device = null;
        foreach (var each in Object.FindObjectsByType<TabletAccountUI>())
        { if (views.tablets[1] != null && (each.transform.IsChildOf(views.tablets[1].transform) || views.tablets[1].screen != null && each.transform.IsChildOf(views.tablets[1].screen))) { device = each; } }
        if (device != null)
        {
            device.ShowPage(TabletAccountUI.Page.Automation);
            foreach (var button in device.GetComponentsInChildren<UnityEngine.UI.Button>(true)) { if (button.name == "Button :: Tab Tactics") { button.onClick.Invoke(); break; } }
        }
        if (views.tablets[1] != null) { views.FocusTablet(views.tablets[1]); }
        yield return 3f;
        decimal before = tactic.currentPrice;
        Render(camera, "tactic-tablet-before", 3840, 2160);
        bool lit = PlayerTactics.Instance != null && PlayerTactics.Instance.Ignite(tactic, .7f);
        yield return 1.5f;
        Render(camera, "tactic-tablet-after", 3840, 2160);
        note.notes.Add("(전술: 리딩방 개설 " + opened + " · 작전 화면 " + (device != null) + " · 불붙이기 " + lit + " · " + tactic.symbol + " " + before.ToString("N0") + " → " + tactic.currentPrice.ToString("N0") + " · " + (PlayerTactics.Instance == null ? "" : PlayerTactics.Instance.Notice) + ")");
        views.ExitView(true);
        yield return 2.5f;
        ChartClose("tactic-chart"); ChartAngle("tactic-angle"); Seat("tactic-seat");
        yield return 12f;
        Profile(); yield return 1.5f;
        ChatFront("tactic-chat"); ChartAngle("tactic-angle-later");

        // 3. 급등: 불나방 무리가 가장 싼 종목에 몰린다.
        manager.SelectStock(manager.stocks.IndexOf(surge)); if (charts != null) { charts.SetChartPeriod(1); }
        bool forced = pool.Force(surge, 260); decimal began = surge.currentPrice;
        yield return 28f;
        Profile(); yield return 1.5f;
        note.notes.Add("(급등: " + surge.symbol + " " + began.ToString("N0") + " → " + surge.currentPrice.ToString("N0") + (forced ? "" : " (실패)") + ")");
        Seat("surge-seat"); ChartClose("surge-chartclose"); ChartAngle("surge-chartangle"); ChatFront("surge-chatfront");
        RenderFrom(camera, "surge-seathigh", home + new Vector3(0f, 1.3f, .9f), chart + Vector3.down * 1.0f, 60f);
        // 투자자 심리 칸이 크게 보이는 자리(채팅 화면의 아래쪽과 차트의 호가창).
        RenderFrom(camera, "surge-psych", chat + new Vector3(-1.5f, -1.1f, -2.7f), chat + new Vector3(-.3f, -1.15f, 0f), 42f);
        RenderFrom(camera, "surge-psych-wide", chat + new Vector3(-2.2f, -.7f, -3.3f), chat + new Vector3(-.7f, -.9f, 0f), 44f);
        // 세로 구도(세로 캡슐·라이브러리 캡슐): 차트 중심 둘, 방 중심 둘.
        RenderFrom(camera, "tall-chart", chart + new Vector3(.7f, .4f, -5.4f), chart + new Vector3(.55f, -.75f, 0f), 50f, true);
        RenderFrom(camera, "tall-chart2", chart + new Vector3(.3f, .9f, -6.0f), chart + new Vector3(.4f, -.9f, 0f), 46f, true);
        RenderFrom(camera, "tall-room", home + new Vector3(-2.2f, 1.7f, -2.4f), chart + new Vector3(.5f, -.9f, 0f), 56f, true);
        RenderFrom(camera, "tall-room2", home + new Vector3(2.0f, 1.9f, -2.8f), chart + new Vector3(-.3f, -.9f, 0f), 56f, true);

        // 4. 급락: 몰렸던 무리가 빠져나가며 값이 꺼진다. 꺼질 때까지 기다린다(길어야 3분).
        decimal peak = surge.currentPrice; double until = EditorApplication.timeSinceStartup + 190;
        while (EditorApplication.timeSinceStartup < until && surge.currentPrice > peak * .80m) { if (surge.currentPrice > peak) { peak = surge.currentPrice; } yield return 2f; }
        yield return 6f;
        Profile(); yield return 1.5f;
        note.notes.Add("(급락: " + surge.symbol + " 고점 " + peak.ToString("N0") + " → " + surge.currentPrice.ToString("N0") + ")");
        ChartClose("crash-chartclose"); ChartAngle("crash-chartangle"); ChatFront("crash-chatfront"); Seat("crash-seat");
        RenderFrom(camera, "crash-psych", chat + new Vector3(-1.5f, -1.1f, -2.7f), chat + new Vector3(-.3f, -1.15f, 0f), 42f);
        findings.Add("steamrun: 찍음");
    }
    private static IEnumerable<float> Scenes()
    {
        foreach (float wait in Quiet(1f, "방과 책상을 여러 각도에서 찍음")) { yield return wait; }
        var camera = Camera.main; var views = DeskViewController.Instance; var desk = GameObject.Find("desk_body");
        if (camera == null || views == null || desk == null) { current.notes.Add("(실패: 카메라·책상을 찾지 못함)"); yield break; }
        var box = desk.GetComponent<Renderer>().bounds;
        Vector3 home = camera.transform.position; Quaternion homeRotation = camera.transform.rotation; float homeFov = camera.fieldOfView;
        Vector3 Center(DeskViewTarget target) { var screen = views.ScreenFor(target); return screen == null ? box.center + Vector3.up * 3f : screen.TransformPoint(screen.rect.center); }
        Vector3 chart = Center(DeskViewTarget.Chart), chat = Center(DeskViewTarget.Chat), stocks = Center(DeskViewTarget.Stocks);
        float top = box.max.y;
        var calendar = WallCalendar.Instance; Vector3 wall = chart + Vector3.left * 5f;
        if (calendar != null) { calendar.DayOverride = 9; calendar.Pose(out wall, out _, out _); }
        // 채팅 화면에서 방금 말한 투자자 하나를 골라 둔다(그 사람의 신뢰·심리가 아래 칸에 뜬다).
        var market = MarketDayController.Instance; int chosen = 0;
        for (int i = market.MessageCount - 1; i >= 0 && chosen == 0; i--) { var message = market.GetMessage(i); if (message.Social != null && message.BotId > 0) { chosen = message.BotId; } }
        foreach (var board in Object.FindObjectsByType<TabletGroupChatUI>()) { if (board != null && board.monitorChat && chosen != 0) { board.SelectProfile(chosen); } }
        yield return 2f;
        current.notes.Add("(자리 " + home.ToString("0.0") + " · 책상 " + box.min.ToString("0.0") + "~" + box.max.ToString("0.0") + " · 가운데 화면 " + chart.ToString("0.0") + " · 채팅 " + chat.ToString("0.0") + " · 종목 " + stocks.ToString("0.0") + " · 달력 " + wall.ToString("0.0") + ")");
        var shots = new (string name, Vector3 position, Vector3 look, float fov)[]
        {
            ("seat", home, chart + Vector3.down * .2f, homeFov),
            ("chartclose", chart + new Vector3(0f, .1f, -3.3f), chart + Vector3.down * .05f, 40f),
            ("seathigh", home + new Vector3(0f, 1.3f, .9f), chart + Vector3.down * 1.0f, 60f),
            ("chatfront", chat + new Vector3(-.9f, .1f, -3.6f), chat + new Vector3(-.5f, -.2f, 0f), 44f),
            ("chartangle", chart + new Vector3(-2.3f, .5f, -3.4f), chart + new Vector3(.2f, -.1f, 0f), 42f),
            ("wide", home + new Vector3(0f, 1.0f, -3.6f), chart + Vector3.down * .6f, 52f),
            ("left", chart + new Vector3(-6.2f, .9f, -5.6f), chart + new Vector3(.6f, -.3f, 0f), 46f),
            ("right", chart + new Vector3(6.0f, .7f, -5.2f), chart + new Vector3(-.6f, -.3f, 0f), 46f),
            ("low", new Vector3(box.center.x - 2.9f, top + .7f, box.min.z - .4f), chat + Vector3.down * .9f, 40f),
            ("lowleft", new Vector3(box.center.x + 3.0f, top + .8f, box.min.z - .3f), stocks + Vector3.down * .8f, 40f),
            ("tablets", new Vector3(box.center.x, top + 3.4f, box.min.z - .9f), new Vector3(box.center.x, top, box.center.z - .2f), 44f),
            ("clock", chart + new Vector3(0f, 1.2f, -3.6f), chart + new Vector3(0f, 1.5f, 0f), 36f),
            ("chatside", chat + new Vector3(-2.6f, .3f, -3.2f), chat, 40f),
            ("stockside", stocks + new Vector3(2.6f, .3f, -3.2f), stocks, 40f),
            ("calendar", home + new Vector3(1.6f, .3f, -1.2f), Vector3.Lerp(wall, chart, .28f), 58f),
            ("room", new Vector3(box.center.x - 1.2f, home.y + 1.0f, box.min.z - .2f), home + new Vector3(1.5f, -1.6f, -7f), 66f),
        };
        const int width = 3840, height = 2160;
        var texture = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        Directory.CreateDirectory(Folder + "/shots");
        foreach (var shot in shots)
        {
            camera.transform.position = shot.position; camera.transform.rotation = Quaternion.LookRotation(shot.look - shot.position, Vector3.up); camera.fieldOfView = shot.fov;
            var previous = RenderTexture.active; var old = camera.targetTexture;
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Folder + "/shots", "scene-" + shot.name + ".png"), pixels.EncodeToPNG());
            camera.targetTexture = old; RenderTexture.active = previous;
            camera.transform.SetPositionAndRotation(home, homeRotation); camera.fieldOfView = homeFov;
            yield return .3f;
        }
        Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
        // 세로 구도(세로 캡슐·라이브러리 캡슐의 바탕). 2160x3240.
        var tall = new (string name, Vector3 position, Vector3 look, float fov)[]
        {
            ("tall-center", home + new Vector3(0f, .5f, 2.0f), chart + Vector3.down * .55f, 62f),
            ("tall-high", home + new Vector3(0f, 1.6f, 1.2f), chart + Vector3.down * 1.1f, 60f),
            ("tall-angle", chart + new Vector3(-2.7f, 1.0f, -4.4f), chart + new Vector3(.9f, -.6f, 0f), 56f),
            ("tall-right", chart + new Vector3(2.7f, 1.0f, -4.4f), chart + new Vector3(-.9f, -.6f, 0f), 56f),
        };
        const int tallWidth = 2160, tallHeight = 3240;
        var tallTexture = new RenderTexture(tallWidth, tallHeight, 24); var tallPixels = new Texture2D(tallWidth, tallHeight, TextureFormat.RGB24, false);
        foreach (var shot in tall)
        {
            camera.transform.position = shot.position; camera.transform.rotation = Quaternion.LookRotation(shot.look - shot.position, Vector3.up); camera.fieldOfView = shot.fov;
            var previous = RenderTexture.active; var old = camera.targetTexture;
            camera.targetTexture = tallTexture; camera.Render(); RenderTexture.active = tallTexture;
            tallPixels.ReadPixels(new Rect(0, 0, tallWidth, tallHeight), 0, 0); tallPixels.Apply();
            File.WriteAllBytes(Path.Combine(Folder + "/shots", "scene-" + shot.name + ".png"), tallPixels.EncodeToPNG());
            camera.targetTexture = old; RenderTexture.active = previous;
            camera.transform.SetPositionAndRotation(home, homeRotation); camera.fieldOfView = homeFov;
            yield return .3f;
        }
        Object.DestroyImmediate(tallPixels); tallTexture.Release(); Object.DestroyImmediate(tallTexture);
        if (calendar != null) { calendar.DayOverride = 0; }
        findings.Add("scenes: " + (shots.Length + tall.Length) + "장 찍음");
    }
    private static IEnumerable<float> CalendarShot(string name, int day)
    {
        var calendar = WallCalendar.Instance; var camera = Camera.main;
        foreach (float wait in Quiet(1f, "벽 달력을 " + (day > 0 ? day + "일인 것처럼 " : "") + "찍음")) { yield return wait; }
        if (calendar == null || camera == null) { current.notes.Add("(실패: 달력이나 카메라가 없음)"); yield break; }
        // 날짜를 하루씩 넘겨서 선이 그어지는 과정도 거치게 한다.
        if (day > 0) { for (int step = Mathf.Max(1, day - 2); step <= day; step++) { calendar.DayOverride = step; yield return 1.6f; } }
        yield return 1f;
        if (!calendar.Pose(out Vector3 center, out Vector3 normal, out float tall)) { current.notes.Add("(실패: 달력이 아직 만들어지지 않음)"); calendar.DayOverride = 0; yield break; }
        current.notes.Add("(달력 자리 " + center.ToString("0.00") + ", 카메라 자리 " + camera.transform.position.ToString("0.00") + ")");
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        try
        {
            // 한 장은 정면에서 가까이, 한 장은 앉은 자리에서 고개를 돌려 본 모습.
            for (int view = 0; view < 2; view++)
            {
                if (view == 0) { camera.transform.position = center + normal * tall * 2.1f; camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up); camera.fieldOfView = 30f; }
                else { camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(center - position, Vector3.up); camera.fieldOfView = 55f; }
                const int width = 1100, height = 1100;
                var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Folder + "/shots");
                File.WriteAllBytes(Path.Combine(Folder + "/shots", name + (view == 0 ? "" : "-seat") + ".png"), pixels.EncodeToPNG());
                camera.targetTexture = old; RenderTexture.active = previous; Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
            }
            findings.Add("calshot " + name + ": 찍음");
        }
        catch (Exception error) { findings.Add("calshot " + name + ": 실패 " + error.Message); }
        camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
        calendar.DayOverride = 0;
    }
    private static IEnumerable<float> StockShot(string name)
    {
        foreach (float wait in Quiet(1f, "종목 화면을 찍으려고 두 계좌로 조금씩 삼")) { yield return wait; }
        var accounts = PlayerAccounts.Instance; var stocks = new List<Stock>();
        foreach (var stock in StockManager.Instance.stocks) { if (stock != null && stock.AcceptsOrders && stock.currentPrice > 0m) { stocks.Add(stock); } }
        if (accounts == null || accounts.primary == null || stocks.Count < 3) { current.notes.Add("(실패: 계좌나 종목이 없음)"); yield break; }
        accounts.primary.cash += 300000000m; accounts.EvaluateUnlock();
        if (accounts.automated != null) { accounts.automated.cash += 50000000m; }
        // 첫 종목은 1번 계좌만, 둘째는 두 계좌 모두, 셋째는 2번 계좌만.
        void Buy(Trader trader, Stock stock)
        {
            if (trader == null) { return; }
            decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * 1.03m);
            trader.BuyLimit(price, Math.Max(1m, decimal.Floor(2000000m / price)), 0, stock);
        }
        Buy(accounts.primary, stocks[0]); Buy(accounts.primary, stocks[1]);
        if (accounts.AutomatedUnlocked) { Buy(accounts.automated, stocks[1]); Buy(accounts.automated, stocks[2]); }
        yield return 12f;
        current.notes.Add("(2번 계좌 " + (accounts.AutomatedUnlocked ? "열림" : "잠김") + " · 1번 보유: " + stocks[0].symbol + " " + accounts.primary.GetOwnedVolume(stocks[0]) + ", " + stocks[1].symbol + " " + accounts.primary.GetOwnedVolume(stocks[1]) +
            (accounts.automated == null ? "" : " · 2번 보유: " + stocks[1].symbol + " " + accounts.automated.GetOwnedVolume(stocks[1]) + ", " + stocks[2].symbol + " " + accounts.automated.GetOwnedVolume(stocks[2])) + ")");
        var views = DeskViewController.Instance; var screen = views == null ? null : views.ScreenFor(DeskViewTarget.Stocks);
        var camera = Camera.main;
        if (screen == null || camera == null) { current.notes.Add("(실패: 종목 화면이나 카메라를 찾지 못함)"); yield break; }
        Canvas.ForceUpdateCanvases();
        yield return 1f;
        try
        {
            var area = screen.rect;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            Vector3 forward = -screen.forward, center = screen.TransformPoint(area.center);
            float worldHeight = Vector3.Distance(screen.TransformPoint(new Vector3(0, area.yMin, 0)), screen.TransformPoint(new Vector3(0, area.yMax, 0)));
            camera.transform.position = center + forward * worldHeight * 2.2f; camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, screen.up); camera.fieldOfView = 27f;
            int height = 1400, width = Mathf.Clamp(Mathf.RoundToInt(height * area.width / Mathf.Max(1f, area.height)), 600, 2600);
            var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            Directory.CreateDirectory(Folder + "/shots");
            File.WriteAllBytes(Path.Combine(Folder + "/shots", name + ".png"), pixels.EncodeToPNG());
            camera.targetTexture = old; RenderTexture.active = previous; Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            findings.Add("stockshot " + name + ": 찍음 (" + width + "x" + height + ")");
        }
        catch (Exception error) { findings.Add("stockshot " + name + ": 실패 " + error.Message); }
    }
    private static void Finish(Exception error)
    {
        TradeBot.AnchorPsychology = true; InvestorSociety.ForcedMarketMood = null;
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
