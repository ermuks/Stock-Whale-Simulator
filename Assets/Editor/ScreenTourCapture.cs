using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 채팅 화면과 메인 모니터의 여러 상태를 실제 씬에서 차례로 찍는다.
// 열려 있는 편집기에서 돈다: 메뉴 DonDon/Capture/Screen tour, 또는 Validation/Screens/capture.request 파일을 만들면 시작한다.
// 씬을 열거나 저장하지 않는다. 씬에 저장하지 않은 변경이 있으면 시작하지 않는다.
// 게임 저장은 Validation/Screens/save 폴더로 돌려 실제 저장 파일을 건드리지 않는다.
[InitializeOnLoad]
public static class ScreenTourCapture
{
    private const string Folder = "Validation/Screens", Request = Folder + "/capture.request", ScenePath = "Assets/Scenes 1/TradeScene.unity";
    private const string RefreshedKey = "DonDon.ScreenTour.Refreshed";
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new();
    private static bool running, oldOptions, oldBackground;
    private static EnterPlayModeOptions oldFlags;
    private static double began, ready, nextWatch;
    private static int phase, waited;
    private static Camera camera;
    private static TabletGroupChatUI chat;
    private static LeadingRoomUI rooms;
    private static RectTransform chartScreen;
    // 전체 화면 점검: 화면마다 한·영·일로 찍고 넘치는 글자를 적는다.
    private static bool audit;
    private static readonly List<(string name, Action prepare, Func<RectTransform> target)> steps = new();
    private static readonly string[] languages = { "ko", "en", "ja" };
    private static int stepIndex, languageIndex;
    private static bool sawPlaying;
    private static string only = "";
    private const string LanguageKey = "DonDon.ScreenTour.Language";

    private const string RunningKey = "DonDon.ScreenTour.Running", OptionsEnabledKey = "DonDon.ScreenTour.OptionsEnabled", OptionsKey = "DonDon.ScreenTour.Options",
        BackgroundKey = "DonDon.ScreenTour.Background", RefreshedAtKey = "DonDon.ScreenTour.RefreshedAt";

    static ScreenTourCapture()
    {
        EditorApplication.update += Watch;
        // 캡처 도중에 스크립트가 다시 올라오면(도메인 재시작) 진행 상태가 사라진다. 그때는 바꿔 둔 설정을 되돌리고 플레이를 끝낸다.
        if (SessionState.GetBool(RunningKey, false)) { EditorApplication.delayCall += Recover; }
        else if (!SessionState.GetBool(RepairedKey, false)) { SessionState.SetBool(RepairedKey, true); EditorApplication.delayCall += MatchSavedPlayModeOptions; }
    }
    private const string RepairedKey = "DonDon.ScreenTour.Repaired";
    /// <summary>
    /// 이전 판의 이 도구는 끊긴 실행 뒤에 플레이 모드 설정을 되돌리지 못할 수 있었다.
    /// 편집기를 켠 뒤 한 번, 메모리의 설정을 디스크에 저장된 프로젝트 설정과 맞춘다(프로젝트 설정 파일은 고치지 않는다).
    /// </summary>
    private static void MatchSavedPlayModeOptions()
    {
        const string path = "ProjectSettings/EditorSettings.asset";
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        foreach (string line in File.ReadAllLines(path))
        {
            string text = line.Trim();
            if (text.StartsWith("m_EnterPlayModeOptionsEnabled:") && int.TryParse(text.Substring(text.IndexOf(':') + 1).Trim(), out int enabled))
            { if (EditorSettings.enterPlayModeOptionsEnabled != (enabled != 0)) { EditorSettings.enterPlayModeOptionsEnabled = enabled != 0; } }
            else if (text.StartsWith("m_EnterPlayModeOptions:") && int.TryParse(text.Substring(text.IndexOf(':') + 1).Trim(), out int options))
            { if ((int)EditorSettings.enterPlayModeOptions != options) { EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)options; } }
        }
    }
    private static void Recover()
    {
        Restore();
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED: scripts reloaded during the capture" + Environment.NewLine);
        orphanUntil = EditorApplication.timeSinceStartup + 30; EditorApplication.update += EndOrphanPlay;
    }
    private static double orphanUntil;
    /// <summary>끊긴 캡처가 남긴 플레이를 끝낸다. 플레이로 넘어가는 중일 수 있으므로 잠시 지켜본다.</summary>
    private static void EndOrphanPlay()
    {
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); EditorApplication.update -= EndOrphanPlay; return; }
        if (!EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup > orphanUntil) { EditorApplication.update -= EndOrphanPlay; }
    }
    /// <summary>캡처를 위해 바꾼 편집기 설정을 원래대로 돌린다. 원래 값은 도메인이 다시 올라와도 남는 곳에 적어 둔다.</summary>
    private static void Restore()
    {
        if (!SessionState.GetBool(RunningKey, false)) { return; }
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(OptionsEnabledKey, false);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(OptionsKey, 0);
        Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", null);
        // 점검 중에 바꾼 언어 설정도 되돌린다.
        string language = SessionState.GetString(LanguageKey, "");
        if (language.Length > 0 && GameLocale.Code != language) { try { GameLocale.SetLanguage(language); } catch (Exception) { } }
        SessionState.SetString(LanguageKey, "");
        SessionState.SetBool(RunningKey, false);
    }

    private static void Watch()
    {
        if (running || EditorApplication.timeSinceStartup < nextWatch) { return; }
        nextWatch = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) { return; }
        // 요청을 받으면 먼저 바뀐 스크립트를 불러들인다. 컴파일이 끝난 뒤(도메인이 다시 올라온 뒤) 시작한다.
        // 누군가 플레이 중이면 건드리지 않는다. 플레이 중에 스크립트를 다시 올리면 그 플레이가 망가진다.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request); SessionState.SetBool(RefreshedKey, false);
            Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), "REFUSED: play mode is already running" + Environment.NewLine);
            return;
        }
        // 바뀐 스크립트가 있으면 플레이에 들어가는 순간 편집기가 다시 컴파일하면서 이 도구의 진행 상태가 사라진다.
        // 그래서 먼저 불러들이고 컴파일을 직접 요청한 뒤, 스크립트가 다시 올라온 것을 보고 시작한다.
        if (!SessionState.GetBool(RefreshedKey, false))
        {
            SessionState.SetBool(RefreshedKey, true); SessionState.SetFloat(RefreshedAtKey, (float)EditorApplication.timeSinceStartup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return;
        }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(RefreshedAtKey, 0f) < 8 || EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
        string request = File.ReadAllText(Request);
        audit = request.Contains("audit");
        only = request.Contains("only:") ? request.Substring(request.IndexOf("only:") + 5).Trim() : "";
        // 요청에 import가 있으면 번역 표를 먼저 다시 들여온다(편집기 메뉴와 같은 동작).
        if (request.Contains("import"))
        {
            File.WriteAllText(Request, request.Replace("import", ""));
            try { DonDonLocalizationSetup.Import(); AssetDatabase.SaveAssets(); }
            catch (Exception error) { File.Delete(Request); File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED: translation import failed: " + error.Message + Environment.NewLine); return; }
            SessionState.SetBool(RefreshedKey, true); SessionState.SetFloat(RefreshedAtKey, (float)EditorApplication.timeSinceStartup);
            return;
        }
        // 요청이 icon 한 마디면 게임 아이콘만 지정하고 끝낸다(촬영은 하지 않는다).
        if (request.Trim() == "icon")
        {
            File.Delete(Request); SessionState.SetBool(RefreshedKey, false); Directory.CreateDirectory(Folder);
            try { GameIconSetup.Apply(); File.WriteAllText(Path.Combine(Folder, "report.txt"), "ICON applied " + GameIconSetup.IconPath + Environment.NewLine); }
            catch (Exception error) { File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED: icon failed: " + error + Environment.NewLine); }
            return;
        }
        // 요청에 textures가 있으면 방의 벽지·책상 그림을 먼저 다시 만든다(편집기 메뉴와 같은 동작).
        if (request.Contains("textures"))
        {
            File.WriteAllText(Request, request.Replace("textures", ""));
            try { RoomTextureGenerator.Generate(); }
            catch (Exception error) { File.Delete(Request); File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED: texture build failed: " + error + Environment.NewLine); return; }
            SessionState.SetBool(RefreshedKey, true); SessionState.SetFloat(RefreshedAtKey, (float)EditorApplication.timeSinceStartup);
            return;
        }
        SessionState.SetBool(RefreshedKey, false);
        File.Delete(Request); Run();
    }

    /// <summary>편집기를 띄우지 않고 돌릴 때 쓴다: 씬을 열고, 번역 표를 들여오고, 전체 화면 점검을 한다.</summary>
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        DonDonLocalizationSetup.Import(); AssetDatabase.SaveAssets();
        audit = true; Run();
        if (!running) { EditorApplication.Exit(2); }
    }

    [MenuItem("DonDon/Capture/Screen tour")]
    public static void Run()
    {
        if (running) { return; }
        Directory.CreateDirectory(Folder); report.Clear(); errors.Clear();
        if (File.Exists(Folder + "/report.txt")) { File.Delete(Folder + "/report.txt"); }
        var scene = EditorSceneManager.GetActiveScene();
        string refusal = EditorApplication.isPlayingOrWillChangePlaymode ? "play mode is already running" : EditorApplication.isCompiling ? "scripts are compiling" :
            scene.path != ScenePath ? "active scene is " + scene.path : scene.isDirty ? "the scene has unsaved changes" : null;
        if (refusal != null) { File.WriteAllText(Path.Combine(Folder, "report.txt"), "REFUSED: " + refusal + "\n"); return; }
        running = true;
        Directory.CreateDirectory(Folder + "/save");
        foreach (string old in Directory.GetFiles(Folder + "/save")) { File.Delete(old); }
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", Path.GetFullPath(Folder + "/save"));
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions; oldBackground = Application.runInBackground;
        SessionState.SetBool(OptionsEnabledKey, oldOptions); SessionState.SetInt(OptionsKey, (int)oldFlags); SessionState.SetBool(BackgroundKey, oldBackground); SessionState.SetBool(RunningKey, true);
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.runInBackground = true;
        began = EditorApplication.timeSinceStartup; ready = began + 3; phase = 0; waited = 0; sawPlaying = false;
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    private static void Log(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) { errors.Add(message + "\n" + stack); } }
    private static void Next(double seconds = 1.5) { phase++; ready = EditorApplication.timeSinceStartup + seconds; }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - began > (audit ? 900 : 300)) { Finish(new Exception("timeout at phase " + phase + " playing=" + EditorApplication.isPlaying)); return; }
        // 누군가 플레이를 멈추면 더 기다리지 않고 끝낸다.
        if (EditorApplication.isPlaying) { sawPlaying = true; }
        else if (sawPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(new Exception("play mode ended before the capture finished")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready) { return; }
        try
        {
            if (phase == 0)
            {
                camera = Camera.main;
                report.AppendLine("CLOCK at start " + DeviceMarketJournal.Stamp() + " phase " + MarketDayController.Instance.Phase + " day " + MarketDayController.Instance.Day + " intro playing " + DayIntroOverlay.Instance.Playing);
                chat = Object.FindObjectsByType<TabletGroupChatUI>().Single(c => c.monitorChat); rooms = chat.GetComponent<LeadingRoomUI>();
                var manager = Object.FindAnyObjectByType<ChartManager>();
                chartScreen = (RectTransform)manager.textName.transform.parent.parent;
                Next(2); return;
            }
            if (!StockManager.Instance.SetupConfirmed)
            {
                var setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup != null && setup.confirm != null && setup.confirm.interactable && !StockManager.Instance.SetupInProgress) { setup.confirm.onClick.Invoke(); }
                ready = EditorApplication.timeSinceStartup + 1; return;
            }
            // 점검 단계에서는 일시정지 메뉴를 열기도 한다. 그때는 거래가 멈춰 있어도 계속 진행한다.
            if (phase < 100 && (!MarketDayController.CanTrade || !MarketDayController.Instance.Ready)) { return; }
            if (phase == 1)
            {
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                Time.timeScale = 1f;
                Next(20); return;   // 대화와 기사가 쌓일 때까지.
            }
            if (phase == 2) { chat.SelectRoom(0); Next(); return; }
            if (phase == 3)
            {
                var bot = InvestorSociety.Instance.SocialBots.Values.OrderBy(b => b.Social.Id).FirstOrDefault(b => b.Social.Psychology.Room == 0);
                if (bot != null) { chat.SelectProfile(bot.Social.Id); }
                report.AppendLine("lounge messages=" + MarketDayController.Instance.MessageCount + " rooms opened=" + PlayerProgression.Instance.CanOperateRooms);
                Next(); return;
            }
            if (phase == 4) { Shoot((RectTransform)chat.transform, "1-chat-lounge-investor", 900, 1600); chat.SelectRoom(1); Next(2); return; }
            if (phase == 5)
            {
                Shoot((RectTransform)chat.transform, "2-chat-my-room-before-opening", 900, 1600);
                // 개설 뒤 화면을 찍기 위해 이 실행에서만 방을 연다(저장되지 않는다).
                var career = PlayerProgression.Instance;
                var property = career.GetType().GetProperty("CanOperateRooms");
                property.SetValue(career, true);
                chat.SelectRoom(1); Next(2.5); return;
            }
            if (phase == 6) { Shoot((RectTransform)chat.transform, "3-chat-my-room-opened", 900, 1600); chat.SelectRoom(0); Next(2); return; }
            if (phase == 7)
            {
                var bot = InvestorSociety.Instance.SocialBots.Values.OrderBy(b => b.Social.Id).FirstOrDefault(b => b.Social.Psychology.Room == 0);
                if (bot != null) { chat.SelectProfile(bot.Social.Id); }
                Next(); return;
            }
            if (phase == 8) { Shoot((RectTransform)chat.transform, "4-chat-lounge-after-opening", 900, 1600); Next(.5); return; }
            if (phase == 9)
            {
                var list = GameObject.Find("Panel :: Stock List Screen");
                if (list != null)
                {
                    var area = ((RectTransform)list.transform).rect;
                    Shoot((RectTransform)list.transform, "10-stock-list", area.width >= area.height ? 1920 : 1080, area.width >= area.height ? 1080 : 1920);
                    var first = StockManager.Instance.stocks[0]; StockStatistics.Instance.GetDayWindow(first, out decimal volume, out decimal turnover);
                    report.AppendLine("day window " + first.symbol + " volume=" + volume.ToString("N0") + " turnover=" + turnover.ToString("N0") + " session turnover=" + StockStatistics.Instance.GetSessionTurnover(first).ToString("N0"));
                }
            }
            if (phase == 9) { Shoot(chartScreen, "5-monitor-chart", 1920, 1080); Press("Button :: Information"); Next(2); return; }
            if (phase == 10)
            {
                // 기사가 아직 없으면 조금 더 기다린다.
                int count = NewsManager.Instance == null ? 0 : NewsManager.Instance.Articles.Count;
                if (count == 0 && waited++ < 20) { ready = EditorApplication.timeSinceStartup + 2; return; }
                report.AppendLine("articles=" + count);
                Next(1.5); return;
            }
            if (phase == 11) { Shoot(chartScreen, "6-monitor-information-list", 1920, 1080); report.AppendLine("article row pressed=" + Press("Button :: Article Row 1")); Next(2); return; }
            if (phase == 12) { Shoot(chartScreen, "7-monitor-information-article", 1920, 1080); report.AppendLine("next pressed=" + Press("Button :: Next Article")); Next(2); return; }
            if (phase == 13) { Shoot(chartScreen, "8-monitor-information-next-article", 1920, 1080); report.AppendLine("back pressed=" + Press("Button :: Back To Chart")); Next(2); return; }
            if (phase == 14)
            {
                Shoot(chartScreen, "9-monitor-chart-after-closing", 1920, 1080);
                if (!audit) { Finish(null); return; }
                BuildAudit(); stepIndex = 0; languageIndex = 0;
                SessionState.SetString(LanguageKey, GameLocale.Code);
                Directory.CreateDirectory(Folder + "/audit");
                foreach (string old in Directory.GetFiles(Folder + "/audit", "*.png")) { File.Delete(old); }
                phase = 100; ready = EditorApplication.timeSinceStartup + .5; return;
            }
            if (phase == 100)
            {
                if (languageIndex >= languages.Length) { Finish(null); return; }
                if (stepIndex == 0 && GameLocale.Code != languages[languageIndex]) { GameLocale.SetLanguage(languages[languageIndex]); ready = EditorApplication.timeSinceStartup + 3; phase = 102; return; }
                phase = 102; return;
            }
            if (phase == 102)
            {
                try { steps[stepIndex].prepare(); }
                catch (Exception error) { report.AppendLine("STEP FAILED " + steps[stepIndex].name + " prepare: " + error.Message); }
                phase = 101; ready = EditorApplication.timeSinceStartup + (only.Length > 0 && !steps[stepIndex].name.Contains(only) ? .05 : steps[stepIndex].name.StartsWith("tac-") && languageIndex == 0 && only.Contains("tac") ? (steps[stepIndex].name == "tac-0" ? 2 : steps[stepIndex].name == "tac-1" ? 6 : steps[stepIndex].name == "tac-2" ? 20 : steps[stepIndex].name == "tac-3" ? 8 : steps[stepIndex].name == "tac-4" || steps[stepIndex].name == "tac-5" ? 45 : 11) : steps[stepIndex].name == "found-wall" && languageIndex == 0 && only.Contains("found") ? 20 : steps[stepIndex].name.StartsWith("dawn-talk") && languageIndex == 0 && only.Contains("dawn") ? 7 : steps[stepIndex].name == "dawn-end" && languageIndex == 0 && only.Contains("dawn") ? 12 : steps[stepIndex].name.StartsWith("heat-") && languageIndex == 0 && only.Contains("heat") ? 45 : steps[stepIndex].name.StartsWith("talk-") && steps[stepIndex].name != "talk-0" && languageIndex == 0 && only.Contains("talk") ? 45 : steps[stepIndex].name.StartsWith("room-note") && only.Contains("room") ? 3 : steps[stepIndex].name.StartsWith("perf-") && languageIndex == 0 && only.Contains("perf") ? 40 : steps[stepIndex].name == "rush-npc" && languageIndex == 0 ? 66 : steps[stepIndex].name.EndsWith("accumulate-running") ? 25 : steps[stepIndex].name.EndsWith("accumulate-crash") ? 12 : 1.8); return;
            }
            if (phase == 101)
            {
                var step = steps[stepIndex];
                try
                {
                    var target = step.target();
                    if (only.Length > 0 && !step.name.Contains(only)) { target = null; }
                    if (target == null) { if (only.Length == 0 && step.name != "menu-closed" && step.name != "large-caps") { report.AppendLine("STEP SKIPPED " + step.name + ": screen not found"); } }
                    else if (target.GetComponent<Canvas>() is Canvas overlay && overlay.renderMode == RenderMode.ScreenSpaceOverlay)
                    { ShootOverlay(overlay, "audit/" + languages[languageIndex] + "-" + (stepIndex + 1).ToString("00") + "-" + step.name); }
                    else
                    {
                        var area = target.rect; bool wide = area.width >= area.height;
                        Shoot(target, "audit/" + languages[languageIndex] + "-" + (stepIndex + 1).ToString("00") + "-" + step.name, wide ? 1920 : 1080, wide ? 1080 : 1920);
                    }
                }
                catch (Exception error) { report.AppendLine("STEP FAILED " + step.name + " shot: " + error.Message); }
                stepIndex++;
                if (stepIndex >= steps.Count) { stepIndex = 0; languageIndex++; }
                phase = 100; ready = EditorApplication.timeSinceStartup + .2; return;
            }
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>다음 날 치 이용료가 따로 나가는지 본다(날짜만 하루 뒤로 넣어 본다).</summary>
    private static string PayNext(PlayerProgression career, Trader player)
    {
        decimal before = player.cash; bool ok = career.PayDawnFee(player, MarketDayController.Instance.Day + 1);
        string result = ok + " " + (before - player.cash).ToString("N0");
        // 확인이 끝났으니 날짜 기록을 오늘로 되돌린다.
        typeof(PlayerProgression).GetProperty("DawnFeeDay").SetValue(career, MarketDayController.Instance.Day);
        return result;
    }
    private static void LogAccumulate(string moment)
    {
        var operation = TradingOperationController.Instance;
        report.AppendLine("accumulate " + moment + ": status=" + operation.AccumulateStatus + " bought=" + operation.Bought.ToString("N0") + " spent=" + operation.Spent.ToString("N0") +
            " average=" + operation.AccumulateAverage.ToString("N1") + " price=" + (operation.Target == null ? 0m : operation.Target.currentPrice).ToString("N0") +
            " resting=" + operation.AccumulateRestingPrice.ToString("N0") + "x" + operation.AccumulateRestingQuantity.ToString("N0") + " fills=" + operation.FillCount + " left=" + operation.AccumulateBudgetLeft.ToString("N0"));
    }
    private static void LogLargeCaps()
    {
        if (StockManager.Instance == null || StockStatistics.Instance == null) { return; }
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (StockStatistics.Instance.TryGetDailyChange(stock, out decimal change))
            { report.AppendLine((stock.currentPrice >= 100000m ? "large cap " : "stock ") + stock.symbol + " day change " + change.ToString("0.0") + "% at " + DeviceMarketJournal.Stamp()); }
        }
    }
    private static RectTransform Named(string name)
    {
        var item = GameObject.Find(name);
        return item == null ? null : item.transform as RectTransform;
    }
    private static void SelectInvestor()
    {
        var bot = InvestorSociety.Instance.SocialBots.Values.OrderBy(b => b.Social.Id).FirstOrDefault(b => b.Social.Psychology.Room == 0);
        if (bot != null) { chat.SelectProfile(bot.Social.Id); }
    }
    private static void BuildAudit()
    {
        steps.Clear();
        RectTransform chatScreen = (RectTransform)chat.transform;
        steps.Add(("chat-lounge", () => { chat.SelectRoom(0); SelectInvestor(); }, () => chatScreen));
        steps.Add(("chat-my-room", () => chat.SelectRoom(1), () => chatScreen));
        steps.Add(("chat-host-room", () => rooms.ShowNpc(), () => chatScreen));
        // 새벽 관찰실: 잠금 화면 → 해금 → 입장(이용료) → 내 채팅과 봇의 답, 종목 분석 AI.
        steps.Add(("dawn-locked", () => { if (only.Contains("dawn")) { chat.SelectRoom(NpcLeadingRoom.RoomId); } }, () =>
        {
            if (!only.Contains("dawn")) { return null; }
            var career = PlayerProgression.Instance; var npc = NpcLeadingRoom.Instance; var player = StockManager.Instance.playerAccount;
            if (languageIndex == 0) { report.AppendLine("DAWN locked: unlocked " + career.DawnUnlocked + " eligible " + career.DawnEligible + " turnover " + career.Turnover.ToString("N0") + " joined " + npc.PlayerJoined + " cash " + player.cash.ToString("N0") + " try unlock " + career.UnlockDawn(player) + " try join " + npc.JoinPlayer() + " (" + npc.Status + ") can write " + InvestorSociety.Instance.PlayerCanWrite(NpcLeadingRoom.RoomId)); }
            return chatScreen;
        }));
        steps.Add(("dawn-unlocked", () =>
        {
            if (!only.Contains("dawn") || languageIndex != 0) { return; }
            var career = PlayerProgression.Instance; var player = StockManager.Instance.playerAccount;
            typeof(PlayerProgression).GetProperty("Turnover").SetValue(career, (decimal)career.rules.dawnRoomTurnover);
            decimal before = player.cash; bool opened = career.UnlockDawn(player);
            report.AppendLine("DAWN INJECTED turnover " + career.Turnover.ToString("N0") + " -> unlock " + opened + " paid " + (before - player.cash).ToString("N0") + " unlocked " + career.DawnUnlocked);
        }, () => only.Contains("dawn") ? chatScreen : null));
        string[] lines = { "님들 안녕하세요", "ㅋㅋㅋㅋ 오늘 장 재밌네", "!분석 노보", "!분석 없는종목", "노보손해보험 지금 살까?", "뭐 살지 추천 좀", "대만디스플레이 오늘 간다 가즈아", "수익 30% 먹었다 ㅋㅋ", "아 물렸다 ㅠㅠ", "이 바보들아", "미안", "내가 세력이다", "ㅂㅂ" };
        steps.Add(("dawn-joined", () =>
        {
            if (!only.Contains("dawn") || languageIndex != 0) { return; }
            var career = PlayerProgression.Instance; var npc = NpcLeadingRoom.Instance; var player = StockManager.Instance.playerAccount;
            decimal before = player.cash; npc.RequestJoin(); bool joined = npc.JoinPlayer();
            report.AppendLine("DAWN join " + joined + " (" + npc.Status + ") fee paid " + (before - player.cash).ToString("N0") + " fee day " + career.DawnFeeDay + " can write " + InvestorSociety.Instance.PlayerCanWrite(NpcLeadingRoom.RoomId));
            before = player.cash; bool again = career.PayDawnFee(player, MarketDayController.Instance.Day);
            report.AppendLine("DAWN same day fee again: ok " + again + " paid " + (before - player.cash).ToString("N0") + "; next day: paid " + PayNext(career, player));
            chat.SelectRoom(NpcLeadingRoom.RoomId);
        }, () => only.Contains("dawn") ? chatScreen : null));
        for (int index = 0; index < lines.Length; index++)
        {
            int number = index;
            steps.Add(("dawn-talk-" + number, () =>
            {
                if (!only.Contains("dawn") || languageIndex != 0) { return; }
                bool sent = InvestorSociety.Instance.SubmitPlayerChat(NpcLeadingRoom.RoomId, lines[number]);
                if (!sent) { report.AppendLine("DAWN could not send: " + lines[number]); }
            }, () =>
            {
                if (!only.Contains("dawn") || languageIndex != 0) { return null; }
                var society = InvestorSociety.Instance;
                while (society.PlayerTalkTrace.Count > 0) { report.AppendLine("TALK " + society.PlayerTalkTrace.Dequeue()); }
                return number == 3 || number == lines.Length - 1 ? chatScreen : null;
            }));
        }
        steps.Add(("dawn-end", () => { }, () =>
        {
            if (only.Contains("dawn") && languageIndex == 0)
            {
                var society = InvestorSociety.Instance; int hostile = 0, friendly = 0; float trust = 0f; int count = 0;
                foreach (var bot in society.SocialBots.Values) { if (bot.Social == null || bot.Social.Psychology.Room != NpcLeadingRoom.RoomId) { continue; } count++; trust += bot.Social.Psychology.Trust; if (bot.Social.Psychology.Betrayal >= 40f) { hostile++; } if (bot.Social.Psychology.Trust >= 40f) { friendly++; } }
                report.AppendLine("DAWN end: player lines " + society.PlayerLines + " replies " + society.PlayerReplies + " analyses " + society.AnalysesPosted + " room bots " + count + " average trust " + (count == 0 ? 0f : trust / count).ToString("F1") + " friendly " + friendly + " hostile " + hostile);
                while (society.PlayerTalkTrace.Count > 0) { report.AppendLine("TALK " + society.PlayerTalkTrace.Dequeue()); }
            }
            return only.Contains("dawn") ? chatScreen : null;
        }));
        steps.Add(("monitor-chart", () => chat.SelectRoom(0), () => chartScreen));
        // 보유 현황 칸의 수익·손실 모습. 요청에 only:monitor-holding 이 있을 때만 주식을 쥐여 준다(다른 화면에 영향을 주지 않게).
        steps.Add(("monitor-holding-profit", () =>
        {
            if (!only.Contains("holding")) { return; }
            var stock = StockManager.Instance.CurrentStock;
            PlayerAccounts.Instance.primary.GrantShares(stock, 1000, stock.currentPrice * .8m);
        }, () => chartScreen));
        steps.Add(("monitor-holding-loss", () =>
        {
            if (!only.Contains("holding")) { return; }
            var stock = StockManager.Instance.CurrentStock;
            PlayerAccounts.Instance.primary.GrantShares(stock, 1000, stock.currentPrice * 1.7m);
        }, () => chartScreen));
        steps.Add(("monitor-information", () => Press("Button :: Information"), () => chartScreen));
        steps.Add(("monitor-article", () => Press("Button :: Article Row 1"), () => chartScreen));
        // 세력의 장 시작 몰아주기. 요청에 only:rush 가 있을 때만, 첫 언어에서 한 번 벌이고 66초 뒤의 차트와 결과를 남긴다.
        steps.Add(("rush-npc", () =>
        {
            if (!only.Contains("rush") || languageIndex != 0) { return; }
            bool started = OpeningRush.Instance.ForceNpc();
            report.AppendLine("RUSH started=" + started + (started ? " " + OpeningRush.Instance.NpcStock.symbol + " price " + OpeningRush.Instance.NpcStock.currentPrice.ToString("N0") : ""));
            if (started) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(OpeningRush.Instance.NpcStock)); }
        }, () => { if (only.Contains("rush") && languageIndex == 0) { report.AppendLine("RUSH result " + OpeningRush.Instance.LastReport); } return chartScreen; }));
        steps.Add(("stock-list", () => Press("Button :: Back To Chart"), () => Named("Panel :: Stock List Screen")));
        // 거래정지 종목의 모습. 요청에 only:stock-list-halted 가 있을 때만 두 번째 종목을 정지 상태로 바꿔 놓는다(촬영용 주입).
        steps.Add(("stock-list-halted", () =>
        {
            if (!only.Contains("halted")) { return; }
            var stock = StockManager.Instance.stocks[1];
            typeof(Stock).GetProperty("RiskGrade").SetValue(stock, StockRiskGrade.Halted);
            typeof(Stock).GetProperty("HaltUntil").SetValue(stock, SessionClock.Time + 90f);
        }, () => Named("Panel :: Stock List Screen")));
        foreach (string panel in new[] { "Panel :: Stock Holdings Screen", "Panel :: Operation Monitor", "Panel :: Market Clock Canvas", "Panel :: Phone Screen" })
        {
            string name = panel;
            steps.Add((name.Replace("Panel :: ", "").Replace(" ", "-").ToLowerInvariant(), () => { }, () => Named(name)));
        }
        foreach (var tablet in Object.FindObjectsByType<TabletAccountUI>().OrderBy(t => t.second))
        {
            var device = tablet; string prefix = device.second ? "tablet2-" : "tablet1-";
            RectTransform screen = (RectTransform)device.transform;
            if (device.second)
            {
                steps.Add((prefix + "locked", () => { }, () => screen));
                // 잠긴 화면을 찍은 다음, 나머지 쪽을 보기 위해 이 실행에서만 연다(저장되지 않는다).
                steps.Add((prefix + "automation", () =>
                {
                    var accounts = device.accounts; accounts.GetType().GetProperty("AutomatedUnlocked").SetValue(accounts, true);
                    device.lockRoot.SetActive(false); device.contentRoot.SetActive(true); device.ShowPage(TabletAccountUI.Page.Automation);
                }, () => screen));
                steps.Add((prefix + "accumulate", () =>
                {
                    foreach (var button in device.GetComponentsInChildren<Button>(true)) { if (button.name == "Button :: Tab Accumulate") { button.onClick.Invoke(); break; } }
                }, () => screen));
                // 작전 탭: 잠금 표시와 여는 조건.
                steps.Add((prefix + "tactics", () =>
                {
                    foreach (var button in device.GetComponentsInChildren<Button>(true)) { if (button.name == "Button :: Tab Tactics") { button.onClick.Invoke(); break; } }
                    var career = PlayerProgression.Instance; var company = CorporateEconomy.Instance.PlayerCompany;
                    report.AppendLine("TACTICS " + languages[languageIndex] + " unlocked close " + PlayerTactics.Unlocked(PlayerTactics.Tool.Close) + " (turnover " + career.Turnover.ToString("N0") + " / " + career.rules.tacticCloseTurnover.ToString("N0") + ") wash " + PlayerTactics.Unlocked(PlayerTactics.Tool.Wash) + " (rooms " + career.CanOperateRooms + ") wall " + PlayerTactics.Unlocked(PlayerTactics.Tool.Wall) + " (company " + (company == null ? "none" : company.symbol + " listed " + company.IsListed) + ")");
                    bool wash = PlayerTactics.Instance.StartWash(StockManager.Instance.CurrentStock); string washNote = PlayerTactics.Instance.Notice; PlayerTactics.Instance.StopWash();
                    bool wall = PlayerTactics.Instance.SetWall(StockManager.Instance.CurrentStock, .1f); string wallNote = PlayerTactics.Instance.Notice; PlayerTactics.Instance.PullWall();
                    report.AppendLine("TACTICS " + languages[languageIndex] + " try wash " + wash + " (" + washNote + ") try wall " + wall + " (" + wallNote + ")");
                }, () => screen));
                steps.Add((prefix + "tactics-leave", () =>
                {
                    foreach (var button in device.GetComponentsInChildren<Button>(true)) { if (button.name == "Button :: Tab Accumulate") { button.onClick.Invoke(); break; } }
                }, () => null));
                // 매집을 실제로 돌려 본다: 작업 계좌에 현금을 옮기고, 현재가 −5%~현재가로 시작한다.
                steps.Add((prefix + "accumulate-running", () =>
                {
                    var operation = TradingOperationController.Instance; var stock = StockManager.Instance.CurrentStock;
                    bool moved = device.accounts.Transfer(device.accounts.primary, 20_000_000m, out string reason);
                    bool started = operation.StartAccumulate(stock, stock.currentPrice * .95m, stock.currentPrice, .5f);
                    report.AppendLine("accumulate transfer=" + moved + " (" + reason + ") started=" + started + " stock=" + stock.symbol + " price=" + stock.currentPrice.ToString("N0") + " band=" + operation.AccumulateLow.ToString("N0") + "~" + operation.AccumulateHigh.ToString("N0") + " budget=" + operation.Budget.ToString("N0") + " feedback=" + operation.Feedback);
                }, () => { LogAccumulate("after 25s"); return screen; }));
                // 폭락 대응: 최근 가격 기록에 높은 값을 하나 넣어 "30초 안에 4% 넘게 떨어진" 상황을 만든다(시장 가격은 건드리지 않는다).
                steps.Add((prefix + "accumulate-crash", () =>
                {
                    var operation = TradingOperationController.Instance;
                    var field = operation.GetType().GetField("accumulatePrices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var queue = (Queue<(float time, decimal price)>)field.GetValue(operation);
                    queue.Enqueue((SessionClock.Time, operation.Target.currentPrice * 1.06m));
                    LogAccumulate("before crash");
                }, () => { LogAccumulate("12s into crash"); return screen; }));
                steps.Add((prefix + "accumulate-stopped", () => TradingOperationController.Instance.CancelAccumulate(), () => { LogAccumulate("stopped"); return screen; }));
                steps.Add((prefix + "assets", () => device.ShowPage(TabletAccountUI.Page.Dashboard), () => screen));
                steps.Add((prefix + "special", () => device.ShowPage(TabletAccountUI.Page.Special), () => screen));
                steps.Add((prefix + "records", () => device.ShowPage(TabletAccountUI.Page.Records), () => screen));
            }
            else
            {
                steps.Add((prefix + "assets", () => device.ShowPage(TabletAccountUI.Page.Dashboard), () => screen));
                steps.Add((prefix + "orders", () => device.ShowPage(TabletAccountUI.Page.Orders), () => screen));
                steps.Add((prefix + "company", () => device.ShowPage(TabletAccountUI.Page.Management), () => screen));
                // 인수 사다리: 처음 모습, 그리고 가장 큰 회사의 지분을 대주주에게서 넘겨받았을 때(넣은 것. 순서를 건너뛰어도 되는지 본다).
                steps.Add((prefix + "ladder", () =>
                {
                    if (!only.Contains("ladder")) { return; }
                    device.ShowPage(TabletAccountUI.Page.Management);
                    var board = device.GetComponentInChildren<CompanyDashboardUI>(true); board.OpenLadder();
                    if (languageIndex != 0) { return; }
                    var economy = CorporateEconomy.Instance; var line = new StringBuilder("LADDER start synergy " + economy.LadderSynergy.ToString("F2") + " taken " + economy.LadderControlled + " |");
                    foreach (var rung in economy.Ladder()) { line.Append(" " + (rung.Rank + 1) + " " + rung.Stock.symbol + " cap " + rung.MarketCap.ToString("N0") + " need " + rung.CostToControl.ToString("N0") + " +" + rung.Bonus.ToString("P0") + " |"); }
                    report.AppendLine(line.ToString());
                }, () => only.Contains("ladder") ? screen : null));
                steps.Add((prefix + "ladder-taken", () =>
                {
                    if (!only.Contains("ladder") || languageIndex != 0) { return; }
                    var economy = CorporateEconomy.Instance; var player = StockManager.Instance.playerAccount; var rungs = economy.Ladder();
                    var top = rungs[rungs.Count - 1].Stock; var small = rungs[0].Stock;
                    foreach (var target in new[] { top, small })
                    {
                        decimal need = decimal.Floor(target.issuedShares * .5m) + 1m - player.GetOwnedVolume(target); decimal moved = 0m;
                        foreach (var holder in Trader.Registry.ToArray())
                        {
                            if (holder == player || need <= 0m) { continue; }
                            target.orderBook?.CancelAllOrdersByOwner(holder, target);
                            decimal take = Math.Min(need, holder.GetHolding(target));
                            if (take < 1m) { continue; }
                            holder.ForceRemoveHolding(target, take); player.GrantShares(target, take, target.currentPrice); need -= take; moved += take;
                        }
                        economy.ObserveControl(target);
                        report.AppendLine("LADDER INJECTED " + target.symbol + " shares moved to player " + moved.ToString("N0") + " -> controls " + WorldDramaDirector.Instance.Controls(target) + " ledger " + StockIntegrityChecker.Instance.CheckStock(target).isValid + " synergy " + economy.LadderSynergy.ToString("F2") + " taken " + economy.LadderControlled + " feed: " + MarketEventFeed.Instance.LatestEvent);
                    }
                }, () => only.Contains("ladder") ? screen : null));
                steps.Add((prefix + "ladder-close", () => { if (only.Contains("ladder")) { var view = device.GetComponentInChildren<AcquisitionLadderUI>(true); if (view != null) { view.Close(); } } }, () => null));
                steps.Add((prefix + "records", () => device.ShowPage(TabletAccountUI.Page.Records), () => screen));
                steps.Add((prefix + "news", () => device.ShowPage(TabletAccountUI.Page.Events), () => screen));
                steps.Add((prefix + "unlocks", () => device.ShowPage(TabletAccountUI.Page.Unlocks), () => screen));
            }
        }
        // 새 날 연출: 흐림이 깔리고 큰 글자가 자리를 잡은 순간을 찍는다.
        if (DayIntroOverlay.Instance != null)
        {
            RectTransform intro = (RectTransform)DayIntroOverlay.Instance.Root.transform;
            steps.Add(("day-intro", () => DayIntroOverlay.Instance.Play(2), () => intro));
        }
        steps.Add(("large-caps", LogLargeCaps, () => null));
        // 성능 재기. 요청에 only:perf 가 있을 때만, 첫 언어에서 40초씩 세 번 잰다: 자리에서 볼 때, 차트를 볼 때, 태블릿을 볼 때.
        // 결과는 Validation/Screens/perf.txt 에 쌓인다.
        // 거래 쏠림 관찰: 45초마다 종목별 최근 거래대금 비중과 열기를 적는다. 아무것도 넣지 않고 지켜보기만 한다.
        for (int beat = 1; beat <= 12; beat++)
        {
            int number = beat;
            steps.Add(("heat-" + number, () => { }, () =>
            {
                if (only.Contains("heat") && languageIndex == 0)
                {
                    var interest = StockInterestManager.Instance; var rows = new List<(string name, double recent, double usual, float heat, float weight, decimal price)>(); double sum = 0;
                    foreach (var stock in StockManager.Instance.stocks)
                    {
                        if (!stock.AcceptsOrders) { continue; }
                        rows.Add((stock.symbol, interest.RecentTurnover(stock), interest.UsualTurnover(stock), interest.Heat(stock), interest.GetWeight(stock), stock.currentPrice)); sum += interest.RecentTurnover(stock);
                    }
                    rows.Sort((a, b) => b.recent.CompareTo(a.recent));
                    int hot = 0; foreach (var row in rows) { if (row.heat >= .5f) { hot++; } }
                    var line = new StringBuilder("HEAT " + number + " day " + MarketDayController.Instance.Day + " " + MarketDayController.Instance.Phase + " stocks " + rows.Count + " hot " + hot + " |");
                    foreach (var row in rows) { line.Append(" " + row.name + " " + (sum > 0 ? row.recent / sum : 0).ToString("P0") + " h" + row.heat.ToString("F1") + " w" + row.weight.ToString("F0") + " @" + row.price.ToString("F0") + " |"); }
                    report.AppendLine(line.ToString());
                }
                return null;
            }));
        }
        // 차트 수다: 먼저 지어낸 가격 흐름으로 감지기를 점검하고(말은 하지 않는다), 그 뒤 9분 동안 실제 장에서 알아챈 모양과 한 말을 적는다.
        steps.Add(("talk-0", () => { }, () =>
        {
            if (only.Contains("talk") && languageIndex == 0)
            {
                var society = InvestorSociety.Instance;
                report.AppendLine("CHART INJECTED pump then dump: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (60, 1150m), (16, 1090m), (20, 1030m), (20, 1012m), (30, 930m)));
                report.AppendLine("CHART INJECTED slow fade after pump: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (60, 1150m), (200, 1030m)));
                report.AppendLine("CHART INJECTED plunge then V: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (30, 890m), (20, 930m), (20, 950m), (40, 1015m)));
                report.AppendLine("CHART INJECTED dead cat: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (30, 890m), (20, 940m), (20, 905m), (20, 880m)));
                report.AppendLine("CHART INJECTED double top: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (60, 1100m), (30, 1050m), (30, 1098m), (30, 1055m)));
                report.AppendLine("CHART INJECTED double bottom: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (60, 900m), (30, 945m), (30, 902m), (30, 945m)));
                report.AppendLine("CHART INJECTED grind up: " + society.ChartSelfTest((0, 1000m), (160, 1085m)));
                report.AppendLine("CHART INJECTED grind down: " + society.ChartSelfTest((0, 1000m), (160, 915m)));
                report.AppendLine("CHART INJECTED box breakout: " + society.ChartSelfTest((0, 1000m), (50, 1008m), (50, 998m), (50, 1006m), (10, 1042m)));
                report.AppendLine("CHART INJECTED box breakdown: " + society.ChartSelfTest((0, 1000m), (50, 1008m), (50, 998m), (50, 1006m), (10, 962m)));
                report.AppendLine("CHART INJECTED upper wick: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (8, 1062m), (8, 1004m), (10, 1004m)));
                report.AppendLine("CHART INJECTED lower wick: " + society.ChartSelfTest((0, 1000m), (60, 1000m), (8, 940m), (8, 997m), (10, 997m)));
                report.AppendLine("CHART INJECTED quiet 1% drift (should be empty): " + society.ChartSelfTest((0, 1000m), (120, 1010m), (120, 1002m), (120, 1012m)));
                report.AppendLine("CHART INJECTED 4% wiggle (should be empty): " + society.ChartSelfTest((0, 1000m), (60, 1040m), (60, 1005m), (60, 1038m)));
            }
            return null;
        }));
        for (int beat = 1; beat <= 12; beat++)
        {
            int number = beat;
            steps.Add(("talk-" + number, () => { }, () =>
            {
                if (only.Contains("talk") && languageIndex == 0)
                {
                    var society = InvestorSociety.Instance;
                    report.AppendLine("CHART " + number + " day " + MarketDayController.Instance.Day + " " + MarketDayController.Instance.Phase + " shapes " + society.ChartShapesSeen + " lines " + society.ChartLinesSaid);
                    while (society.ChartTrace.Count > 0) { report.AppendLine("CHART   " + society.ChartTrace.Dequeue()); }
                }
                return null;
            }));
        }
        // 작전 도구 확인: 플레이어 도구 세 가지를 실제 계좌로 써 보고(넣은 것임을 적는다), 그 뒤 세력 봇과 개미가 스스로 쓰는지 지켜본다.
        Stock tacticStock = null; decimal tacticCash = 0m, tacticHeld = 0m; float tacticHeat = 0f;
        steps.Add(("tac-0", () => { }, () =>
        {
            if (only.Contains("tac") && languageIndex == 0)
            {
                var player = PlayerAccounts.Instance.primary; decimal widest = 0m;
                foreach (var stock in StockManager.Instance.stocks)
                {
                    if (!stock.AcceptsOrders || stock.Owner == player || !stock.orderBook.TryBest(out decimal bid, out decimal ask)) { continue; }
                    decimal ticks = (ask - bid) / PriceTickPolicy.GetTickSize(bid);
                    if (ticks > widest) { widest = ticks; tacticStock = stock; }
                }
                tacticStock.orderBook.TryBest(out _, out decimal offer);
                decimal quantity = decimal.Floor(player.cash * .08m / offer);
                player.BuyLimit(PriceTickPolicy.CeilToTick(offer * 1.01m), quantity, 0, tacticStock);
                report.AppendLine("TACTIC INJECTED setup: " + tacticStock.symbol + " spread " + widest.ToString("F0") + " ticks, bought " + player.GetHolding(tacticStock).ToString("N0") + " of " + quantity.ToString("N0") + " shares");
            }
            return null;
        }));
        steps.Add(("tac-1", () => { }, () =>
        {
            if (only.Contains("tac") && languageIndex == 0)
            {
                var player = PlayerAccounts.Instance.primary; var interest = StockInterestManager.Instance;
                tacticCash = player.cash; tacticHeld = player.GetOwnedVolume(tacticStock); tacticHeat = interest.Heat(tacticStock);
                bool started = PlayerTactics.Instance.StartWash(tacticStock);
                report.AppendLine("TACTIC INJECTED wash start " + started + ": cash " + tacticCash.ToString("N0") + " shares " + tacticHeld.ToString("N0") + " heat " + tacticHeat.ToString("F1") + " usual " + interest.UsualTurnover(tacticStock).ToString("N0") + " recent " + interest.RecentTurnover(tacticStock).ToString("N0") + " notice " + PlayerTactics.Instance.Notice);
            }
            return null;
        }));
        steps.Add(("tac-2", () => { }, () =>
        {
            if (only.Contains("tac") && languageIndex == 0)
            {
                var player = PlayerAccounts.Instance.primary; var interest = StockInterestManager.Instance; var tactics = PlayerTactics.Instance;
                report.AppendLine("TACTIC INJECTED wash after 20s: rounds " + tactics.WashRounds + " painted " + tactics.WashPainted.ToString("N0") + " cash change " + (player.cash - tacticCash).ToString("N0") + " share change " + (player.GetOwnedVolume(tacticStock) - tacticHeld).ToString("N0")
                    + " heat " + tacticHeat.ToString("F1") + " -> " + interest.Heat(tacticStock).ToString("F1") + " recent " + interest.RecentTurnover(tacticStock).ToString("N0") + " nominee cash " + ManipulationTools.Nominee(player).cash.ToString("N0") + " nominee shares " + ManipulationTools.Nominee(player).GetHolding(tacticStock).ToString("N0")
                    + " ledger " + StockIntegrityChecker.Instance.CheckStock(tacticStock).isValid);
                tactics.StopWash();
                int before = ManipulationTools.BookTilt(tacticStock);
                bool placed = tactics.SetWall(tacticStock, .5f);
                report.AppendLine("TACTIC INJECTED wall placed " + placed + ": price " + tactics.WallPrice.ToString("N0") + " vs last " + tacticStock.currentPrice.ToString("N0") + " shown " + tactics.WallShown.ToString("N0") + " reserved " + player.ReservedCash.ToString("N0") + " tilt before " + before + " notice " + tactics.Notice);
            }
            return null;
        }));
        steps.Add(("tac-3", () => { }, () =>
        {
            if (only.Contains("tac") && languageIndex == 0)
            {
                var player = PlayerAccounts.Instance.primary; var tactics = PlayerTactics.Instance;
                report.AppendLine("TACTIC INJECTED wall after 8s: standing " + (tactics.WallStock != null) + " price " + tactics.WallPrice.ToString("N0") + " shown " + tactics.WallShown.ToString("N0") + " tilt now " + ManipulationTools.BookTilt(tacticStock) + " notice " + tactics.Notice);
                tactics.PullWall();
                report.AppendLine("TACTIC INJECTED wall pulled: reserved " + player.ReservedCash.ToString("N0") + " notice " + tactics.Notice);
                bool armed = tactics.ArmClose(tacticStock, .1f);
                report.AppendLine("TACTIC INJECTED close armed " + armed + " budget " + tactics.CloseBudget.ToString("N0") + " (left armed; it buys in the last 40 seconds of the session)");
                // 세력 한 곳의 매집을 거의 끝난 상태로 당겨 놓는다(넣은 것). 그 뒤의 수법과 개미의 반응은 스스로 일어나는 것만 적는다.
                var field = typeof(WhaleBot).GetField("operation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var stock in StockManager.Instance.stocks)
                {
                    if (stock.Owner is WhaleBot whale && whale.Operating && field.GetValue(whale) is WhaleOperationState state && state.stage == WhaleOperationState.Build)
                    {
                        report.AppendLine("TACTIC INJECTED shortened accumulation: " + stock.symbol + " build " + state.stageSeconds.ToString("F0") + "/" + state.buildSeconds.ToString("F0") + "s -> 20s left, budget " + state.budget.ToString("N0") + " price " + stock.currentPrice.ToString("N0"));
                        state.buildSeconds = 400f; state.stageSeconds = 380f;
                    }
                }
            }
            return null;
        }));
        for (int beat = 4; beat <= 11; beat++)
        {
            int number = beat;
            steps.Add(("tac-" + number, () => { }, () =>
            {
                if (only.Contains("tac") && languageIndex == 0)
                {
                    var market = MarketDayController.Instance; var tactics = PlayerTactics.Instance; var society = InvestorSociety.Instance;
                    foreach (var stock in StockManager.Instance.stocks)
                    {
                        if (stock.Owner is WhaleBot whale && whale.Operating && typeof(WhaleBot).GetField("operation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(whale) is WhaleOperationState state)
                        { report.AppendLine("TACTIC   whale " + stock.symbol + " stage " + state.stage + " price " + stock.currentPrice.ToString("N0") + " start " + state.startPrice.ToString("N0") + " heat " + StockInterestManager.Instance.Heat(stock).ToString("F1") + " tilt " + ManipulationTools.BookTilt(stock)); }
                    }
                    report.AppendLine("TACTIC " + number + " day " + market.Day + " " + market.Phase + " left " + market.RemainingSeconds.ToString("F0") + "s | whales: wash rounds " + WhaleBot.WashRoundsTotal + " painted " + WhaleBot.WashPaintedTotal.ToString("N0") + " walls placed " + WhaleBot.WallsPlacedTotal + " pulled " + WhaleBot.WallsPulledTotal + " close buys " + WhaleBot.CloseBuysTotal
                        + " | bots: chases " + TradeBot.ChasesTotal + " sold " + TradeBot.ChaseSoldTotal.ToString("N0") + " | player close spent " + tactics.CloseSpent.ToString("N0") + " of " + tactics.CloseBudget.ToString("N0") + " " + tactics.Notice);
                    report.AppendLine("INFLUENCE messages " + society.InfluenceMessages + " turned " + society.InfluenceTurned);
                    while (society.InfluenceTrace.Count > 0) { report.AppendLine("INFLUENCE   " + society.InfluenceTrace.Dequeue()); }
                    int rotations = 0; while (society.ChartTrace.Count > 0) { string line = society.ChartTrace.Dequeue(); if (line.Contains("[rotate]")) { report.AppendLine("TACTIC   " + line); } else if (line.Contains(" rotate ")) { rotations++; } }
                    if (rotations > 0) { report.AppendLine("TACTIC   rotations noted since last beat: " + rotations); }
                    if (number == 5)
                    {
                        // 장 마감 38초 전으로 시계를 당긴다(넣은 것). 종가 관리가 실제로 사들이는지 보기 위해서다.
                        var clock = typeof(MarketDayController).GetField("elapsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        float before = market.RemainingSeconds; clock.SetValue(market, (float)clock.GetValue(market) + Mathf.Max(0f, before - 38f));
                        report.AppendLine("TACTIC INJECTED clock moved to 38s before the close (was " + before.ToString("F0") + "s left)");
                    }
                }
                return null;
            }));
        }
        // 기업 설립 해금과 호가창의 벽 인식.
        steps.Add(("found-check", () => { }, () =>
        {
            if (only.Contains("found") && languageIndex == 0)
            {
                var economy = CorporateEconomy.Instance; var career = PlayerProgression.Instance; var player = StockManager.Instance.playerAccount;
                report.AppendLine("FOUND start: company " + (economy.PlayerCompany == null ? "none" : economy.PlayerCompany.symbol) + " founded flag " + career.CompanyFounded + " cost " + career.FoundingCost.ToString("N0") + " cash " + player.cash.ToString("N0") + " wall tool " + PlayerTactics.Unlocked(PlayerTactics.Tool.Wall));
                bool poor = economy.FoundCompany();
                report.AppendLine("FOUND try without money: " + poor + " (" + economy.GrowthResult + ") company " + (economy.PlayerCompany == null ? "none" : economy.PlayerCompany.symbol));
                decimal gift = career.FoundingCost + 50_000_000m - player.cash; player.cash += gift;
                report.AppendLine("FOUND INJECTED cash +" + gift.ToString("N0") + " -> " + player.cash.ToString("N0"));
                decimal before = player.cash; bool rich = economy.FoundCompany(); var company = economy.PlayerCompany;
                report.AppendLine("FOUND try with money: " + rich + " (" + economy.GrowthResult + ") paid " + (before - player.cash).ToString("N0") + " company " + (company == null ? "none" : company.symbol + " listed " + company.IsListed + " shares " + player.GetOwnedVolume(company).ToString("N0") + "/" + company.issuedShares.ToString("N0")
                    + " company cash " + company.business.Cash.ToString("N0") + " founder " + company.business.growth.FounderBusiness + " ledger " + StockIntegrityChecker.Instance.CheckStock(company).isValid) + " founded flag " + career.CompanyFounded);
                report.AppendLine("FOUND again: " + economy.FoundCompany() + " (" + economy.GrowthResult + ")");
                // 벽: 지금 호가창에서 읽히는 벽, 그리고 큰 매수 주문을 하나 넣었을 때(넣은 것).
                foreach (var stock in StockManager.Instance.stocks)
                {
                    if (!stock.AcceptsOrders) { continue; }
                    var view = ManipulationTools.Walls(stock);
                    if (view.Bid > 0f || view.Ask > 0f) { report.AppendLine("WALL natural " + stock.symbol + " bid " + view.Bid.ToString("F2") + " @" + view.BidPrice.ToString("N0") + " ask " + view.Ask.ToString("F2") + " @" + view.AskPrice.ToString("N0")); }
                }
                var target = StockManager.Instance.CurrentStock; var seen = ManipulationTools.Walls(target);
                decimal price = ManipulationTools.PlaceWall(player, target, player.cash * .6m);
                report.AppendLine("WALL INJECTED bid on " + target.symbol + " at " + price.ToString("N0") + " worth " + (ManipulationTools.WallVolume(player, target, price) * price).ToString("N0") + " (before: bid " + seen.Bid.ToString("F2") + " ask " + seen.Ask.ToString("F2") + ")");
            }
            return null;
        }));
        steps.Add(("found-wall", () => { }, () =>
        {
            if (only.Contains("found") && languageIndex == 0)
            {
                var target = StockManager.Instance.CurrentStock; var view = ManipulationTools.Walls(target); var society = InvestorSociety.Instance;
                report.AppendLine("WALL after 20s on " + target.symbol + ": bid " + view.Bid.ToString("F2") + " @" + view.BidPrice.ToString("N0") + " ask " + view.Ask.ToString("F2") + " lean " + view.Lean.ToString("F2") + " price " + target.currentPrice.ToString("N0"));
                while (society.ChartTrace.Count > 0) { string line = society.ChartTrace.Dequeue(); if (line.Contains("Wall")) { report.AppendLine("WALL   " + line); } }
            }
            return null;
        }));
        steps.Add(("perf-desk", () => { if (only.Contains("perf") && languageIndex == 0) { File.Delete(Path.Combine(Folder, "perf.txt")); DeskViewController.Instance.ExitView(true); PerfProbe.Begin(); } },
            () => { if (only.Contains("perf") && languageIndex == 0) { PerfProbe.End(Path.Combine(Folder, "perf.txt"), "desk view, market open " + DeviceMarketJournal.Stamp() + " bots " + MarketDayController.Instance.Bots.Count); } return null; }));
        steps.Add(("perf-chart", () => { if (only.Contains("perf") && languageIndex == 0) { DeskViewController.Instance.Focus(DeskViewTarget.Chart); PerfProbe.Begin(); } },
            () => { if (only.Contains("perf") && languageIndex == 0) { PerfProbe.End(Path.Combine(Folder, "perf.txt"), "chart focused " + DeviceMarketJournal.Stamp()); } return null; }));
        steps.Add(("perf-second", () => { if (only.Contains("perf") && languageIndex == 0) { DeskViewController.Instance.ExitView(true); PerfProbe.Begin(); } },
            () => { if (only.Contains("perf") && languageIndex == 0) { PerfProbe.End(Path.Combine(Folder, "perf.txt"), "desk view again (leak check) " + DeviceMarketJournal.Stamp()); } return null; }));
        // 방 전체 모습(화면 빛을 끈 것과 켠 것)과 잠자기 안내. 요청에 only:room 이 있을 때만 찍는다.
        steps.Add(("room-glow-off", () => { if (only.Contains("room")) { report.AppendLine("CLOCK " + DeviceMarketJournal.Stamp() + " phase " + MarketDayController.Instance.Phase + " day " + MarketDayController.Instance.Day); MarketDayController.Instance.GetComponent<ScreenGlow>().enabled = false; } },
            () => { if (only.Contains("room") && languageIndex == 0) { ShootView("audit/room-glow-off"); } return null; }));
        steps.Add(("room-glow-on", () => { if (only.Contains("room")) { MarketDayController.Instance.GetComponent<ScreenGlow>().enabled = true; } },
            () => { if (only.Contains("room") && languageIndex == 0) { ShootView("audit/room-glow-on"); } return null; }));
        // 책상을 비스듬히 위에서 내려다본 모습. 어두워서 안 보이니 찍는 동안만 등을 하나 켠다. 게임의 카메라는 건드리지 않는다.
        steps.Add(("room-desk", () => { }, () => { if (only.Contains("room") && languageIndex == 0) { ShootDesk("audit/room-desk"); } return null; }));
        // 서랍: 열어서 들여다본 모습. 게임과 같은 길(시선 옮기기)로 연다.
        steps.Add(("room-drawer", () =>
        {
            if (!only.Contains("room")) { return; }
            bool opened = DrawerProp.Instance.Found && DeskViewController.Instance.FocusCustom((Pose)typeof(DrawerProp).GetMethod("ViewPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(DrawerProp.Instance, new object[] { camera }), 52f);
            if (opened) { typeof(DrawerProp).GetProperty("Open").SetValue(DrawerProp.Instance, true); }
            report.AppendLine("DRAWER opened=" + opened + " fortune " + DrawerProp.Instance.FortuneIndex);
        }, () => { if (only.Contains("room")) { ShootView("audit/" + languages[languageIndex] + "-room-drawer"); } return null; }));
        steps.Add(("room-drawer-close", () => { if (only.Contains("room")) { DeskViewController.Instance.ExitView(true); } },
            () => { if (only.Contains("room")) { report.AppendLine("DRAWER closed, fortune now " + DrawerProp.Instance.FortuneIndex); } return null; }));
        // 소품을 가까이서 본 모습. 찍는 동안만 등을 켠다.
        steps.Add(("room-props", () => { }, () =>
        {
            if (only.Contains("room") && languageIndex == 0)
            {
                foreach (string model in new[] { "Model :: Mug", "Model :: Lotion Bottle", "Model :: Empty Can", "Model :: Crumpled Paper", "Model :: Plastic Bag", "Model :: Pen Cup", "Model :: Small Box" })
                { ShootProp(model); }
            }
            return null;
        }));
        // 등 뒤의 방과 문, 그리고 문틈 쪽지: 들어온 모습, 들어 올린 모습, 모니터에 붙은 모습.
        steps.Add(("room-back", () => { }, () =>
        {
            if (only.Contains("room") && languageIndex == 0)
            {
                var shell = RoomShell.Instance;
                report.AppendLine("ROOM built " + shell.Built + " door " + shell.DoorCenter.ToString("F2") + " walk " + MouseCameraController.WalkMin.ToString("F2") + " .. " + MouseCameraController.WalkMax.ToString("F2"));
                ShootFrom("audit/room-back", new Vector3(0f, 5.5f, -4.2f), shell.DoorCenter + Vector3.down * 1.2f, 62f, 3.2f);
            }
            return null;
        }));
        steps.Add(("room-note-floor", () =>
        {
            if (!only.Contains("room")) { return; }
            DeskViewController.Instance.ExitView(true);
            DoorNotes.Instance.AnnounceAt(StockManager.Instance.stocks[languageIndex + 1], 2, 14 * 60 + 20); DoorNotes.Instance.DeliverNow();
        }, () =>
        {
            if (only.Contains("room"))
            {
                var note = DoorNotes.Instance.Notes[DoorNotes.Instance.Notes.Count - 1];
                report.AppendLine("NOTE on floor state " + note.State + " at " + note.Root.position.ToString("F2"));
                ShootFrom("audit/" + languages[languageIndex] + "-room-note-floor", note.Root.position + new Vector3(.3f, 2.6f, 2.4f), note.Root.position, 50f, 1.4f);
            }
            return null;
        }));
        // 걸어서 설 수 있는 자리에서 실제 눈높이로 쪽지를 겨눴을 때 잡히는지 본다(가까이, 조금 멀리, 방 한가운데).
        steps.Add(("room-note-aim", () => { }, () =>
        {
            if (only.Contains("room"))
            {
                var note = DoorNotes.Instance.Notes[DoorNotes.Instance.Notes.Count - 1]; Vector3 spot = note.Root.position; float eye = DeskViewController.Instance.transform.position.y;
                foreach (float back in new[] { 1.2f, 3f, 5f, 8f })
                {
                    var stand = new Vector3(Mathf.Clamp(spot.x, MouseCameraController.WalkMin.x, MouseCameraController.WalkMax.x), eye, Mathf.Clamp(spot.z + back, MouseCameraController.WalkMin.y, MouseCameraController.WalkMax.y));
                    Vector3 toward = (spot - stand).normalized; float pitch = Mathf.Asin(-toward.y) * Mathf.Rad2Deg;
                    report.AppendLine("NOTE aim from " + stand.ToString("F2") + " distance " + Vector3.Distance(stand, spot).ToString("F2") + " pitch " + pitch.ToString("F0") + " hit " + (DoorNotes.Instance.Aim(new Ray(stand, toward)) == note)
                        + " off by 4 degrees " + (DoorNotes.Instance.Aim(new Ray(stand, Quaternion.AngleAxis(4f, Vector3.up) * toward)) == note));
                }
            }
            return null;
        }));
        steps.Add(("room-note-held", () => { if (only.Contains("room")) { DoorNotes.Instance.PickUp(DoorNotes.Instance.Notes[DoorNotes.Instance.Notes.Count - 1]); } },
            () => { if (only.Contains("room")) { report.AppendLine("NOTE held " + DoorNotes.Holding); ShootView("audit/" + languages[languageIndex] + "-room-note-held"); } return null; }));
        steps.Add(("room-note-stuck", () => { if (only.Contains("room")) { DoorNotes.Instance.PutDown(); } },
            () => { if (only.Contains("room")) { var note = DoorNotes.Instance.Notes[DoorNotes.Instance.Notes.Count - 1]; report.AppendLine("NOTE stuck state " + note.State + " slot " + note.Slot); ShootView("audit/" + languages[languageIndex] + "-room-note-stuck"); } return null; }));
        // 침대·쓰레기통·문, 문이 열린 모습, 잠들고 일어나는 카메라.
        // 돈이 모자란 매수를 한 번 내 본다(나가지 않아야 하고 실패음이 불려야 한다). 봇의 주문은 실패음을 내지 않는다.
        steps.Add(("room-deny", () => { }, () =>
        {
            if (only.Contains("room") && languageIndex == 0 && Sfx.Instance != null)
            {
                var player = PlayerAccounts.Instance.primary; Stock held = null, empty = null;
                foreach (var stock in StockManager.Instance.stocks) { if (!stock.AcceptsOrders) { continue; } if (player.GetHolding(stock) <= 0m) { empty ??= stock; } held ??= stock; }
                Sfx.Instance.Played.TryGetValue("UiDeny", out int before); decimal cash = player.cash;
                player.BuyLimit(held.currentPrice, decimal.Floor(player.cash / held.currentPrice) * 50m + 1000m, 0, held);
                Sfx.Instance.Played.TryGetValue("UiDeny", out int afterBuy);

                report.AppendLine("DENY can trade " + MarketDayController.CanTrade + " oversized buy: cash unchanged " + (player.cash == cash) + ", UiDeny " + before + " -> " + afterBuy + (empty == null ? ", no empty stock to try a sell" : ", empty stock " + empty.symbol));
            }
            return null;
        }));
        steps.Add(("room-bed", () => { }, () =>
        {
            if (only.Contains("room") && RoomAmbience.Instance != null) { report.AppendLine("SOUND " + languages[languageIndex] + " listener volume " + AudioListener.volume.ToString("F2") + " " + RoomAmbience.Instance.Describe()); report.AppendLine("SFX " + languages[languageIndex] + " " + (Sfx.Instance == null ? "missing" : Sfx.Instance.Describe())); }
            if (only.Contains("room") && languageIndex == 0)
            {
                var shell = RoomShell.Instance; Vector3 bed = shell.BedBounds.center;
                report.AppendLine("BED bounds " + shell.BedBounds.min.ToString("F2") + " .. " + shell.BedBounds.max.ToString("F2") + " pillow eye " + shell.PillowEye.ToString("F2") + " stand " + shell.BedsideStand.ToString("F2") + " can " + shell.TrashCan.position.ToString("F2") + " blocks " + MouseCameraController.WalkBlocks.Count);
                ShootFrom("audit/room-bed", new Vector3(1.2f, 6.2f, -9.5f), bed + Vector3.down * .4f, 60f, 3.4f);
                ShootFrom("audit/room-bed-close", new Vector3(bed.x - 4.2f, 5.6f, bed.z + 1.2f), new Vector3(bed.x + .3f, 2.4f, bed.z + 2.2f), 55f, 3f);
                ShootFrom("audit/room-pillow", new Vector3(bed.x - 2.2f, 4.6f, bed.z + 2.0f), shell.PillowEye + Vector3.down * 1f, 40f, 2.6f);
                ShootFrom("audit/room-can", shell.TrashCan.position + new Vector3(-1.6f, 2.4f, -2.6f), shell.TrashCan.position + Vector3.up * .8f, 40f, 2.4f);
                ShootFrom("audit/room-wide", new Vector3(-3.6f, 7.4f, -12.6f), new Vector3(3.2f, 2.2f, -1.5f), 78f, 3.6f);
                ShootFrom("audit/room-door", new Vector3(shell.DoorCenter.x + 1.2f, 5.2f, shell.DoorCenter.z + 7.5f), shell.DoorCenter, 58f, 3.2f);
                ShootFrom("audit/room-door-handle", new Vector3(shell.DoorCenter.x + .2f, 3.9f, shell.DoorCenter.z + 2.6f), shell.DoorCenter + new Vector3(1.2f, -.2f, 0f), 40f, 2.2f);
                DoorExit.Swing(shell, 1f);
                ShootFrom("audit/room-door-open", new Vector3(shell.DoorCenter.x + 1.2f, 5.2f, shell.DoorCenter.z + 7.5f), shell.DoorCenter, 58f, 1.2f);
                DoorExit.Swing(shell, 0f);
            }
            return null;
        }));
        steps.Add(("room-rise", () => { }, () =>
        {
            if (only.Contains("room") && languageIndex == 0)
            {
                var shell = RoomShell.Instance; var eye = DeskViewController.Instance.transform; var keep = new Pose(eye.position, eye.rotation);
                foreach (float second in new[] { 0f, 1.2f, 2.3f, 3.4f, 4.5f, 6f })
                {
                    var pose = BedSleep.RisePose(shell, second); eye.SetPositionAndRotation(pose.position, pose.rotation);
                    report.AppendLine("RISE " + second.ToString("F1") + " position " + pose.position.ToString("F2") + " euler " + pose.rotation.eulerAngles.ToString("F0"));
                    ShootView("audit/room-rise-" + second.ToString("0.0").Replace(".", "_"));
                }
                eye.SetPositionAndRotation(keep.position, keep.rotation);
            }
            return null;
        }));
        // 꿈에서 깨기: 마지막 언어에서 한 번만. 리딩방·내 기업·계좌가 비워지고도 주식 수 장부가 맞는지 적어 둔다.
        steps.Add(("room-dream", () => { }, () =>
        {
            if (only.Contains("room") && languageIndex == languages.Length - 1)
            {
                var player = StockManager.Instance.playerAccount; var economy = CorporateEconomy.Instance; var company = economy.PlayerCompany; var society = InvestorSociety.Instance;
                int members = 0; foreach (var room in society.Rooms) { if (room.PlayerOwned) { members += room.Members.Count; } }
                int held = 0; foreach (var stock in StockManager.Instance.stocks) { if (player.GetHolding(stock) > 0m) { held++; } }
                report.AppendLine("DREAM before cash " + player.cash.ToString("N0") + " equity " + player.Equity().ToString("N0") + " company " + (company == null ? "none" : company.symbol + " owned " + player.GetOwnedVolume(company).ToString("N0") + "/" + company.issuedShares.ToString("N0")) + " rooms " + PlayerProgression.Instance.CanOperateRooms + " members " + members + " held stocks " + held);
                society.DissolvePlayerRooms(); PlayerProgression.Instance.WakeFromDream(); economy.ReleaseFounderFromDream();
                player.WakeFromDream(Object.FindAnyObjectByType<ExchangeBot>(), DoorExit.PocketMoney);
                members = 0; foreach (var room in society.Rooms) { if (room.PlayerOwned) { members += room.Members.Count; } }
                int broken = 0; held = 0; decimal worst = 0m;
                foreach (var stock in StockManager.Instance.stocks)
                {
                    if (!StockIntegrityChecker.Instance.CheckStock(stock).isValid) { broken++; report.AppendLine("DREAM broken ledger " + stock.symbol); }
                    if (player.GetHolding(stock) > 0m) { held++; worst = stock.currentPrice / player.GetAvgPrice(stock) - 1m; }
                }
                if (company != null)
                {
                    report.AppendLine("DREAM company " + company.symbol + " owner " + (company.Owner == null ? "none" : company.Owner.name) + " owner shares " + (company.Owner == null ? 0m : company.Owner.GetOwnedVolume(company)).ToString("N0") + " player shares " + player.GetOwnedVolume(company).ToString("N0") + " ledger " + StockIntegrityChecker.Instance.CheckStock(company).isValid);
                }
                report.AppendLine("DREAM after cash " + player.cash.ToString("N0") + " equity " + player.Equity().ToString("N0") + " company " + (economy.PlayerCompany == null ? "none" : economy.PlayerCompany.symbol) + " rooms " + PlayerProgression.Instance.CanOperateRooms + " members " + members + " held stocks " + held + " return " + worst.ToString("P0") + " broken ledgers " + broken);
            }
            return null;
        }));
        steps.Add(("room-restore", () => { }, () => null));
        // 시스템 메뉴(ESC): 일시정지 화면과 언어 설정.
        var menu = PauseMenuController.Instance;
        if (menu != null)
        {
            RectTransform menuScreen = (RectTransform)menu.UI.Root.transform;
            steps.Add(("menu-pause", () => menu.Open(), () => menuScreen));
            steps.Add(("menu-language", () => { menu.Settings(); menu.UI.Settings("게임"); }, () => menuScreen));
            steps.Add(("menu-closed", () => { menu.Back(); menu.Resume(); }, () => null));
        }
        // 요청에 only:이름 이 있으면 그 이름이 들어간 화면만 찍는다(준비 단계는 그대로 밟는다).
        report.AppendLine("audit steps=" + steps.Count + (only.Length > 0 ? " only=" + only : ""));
    }

    private static bool Press(string name)
    {
        foreach (var button in chartScreen.GetComponentsInChildren<Button>(true))
        {
            if (button.name != name || !button.gameObject.activeInHierarchy || !button.interactable) { continue; }
            button.onClick.Invoke(); return true;
        }
        return false;
    }

    /// <summary>화면을 정면에서 찍는다. 넘치는 글자가 있으면 적는다.</summary>
    private static void Shoot(RectTransform rect, string name, int width, int height)
    {
        Canvas.ForceUpdateCanvases();
        int overflow = 0, spill = 0, korean = 0;
        foreach (var text in rect.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
        {
            if (string.IsNullOrEmpty(text.text)) { continue; }
            text.ForceMeshUpdate();
            string shown = text.text.Replace("\n", " | "); if (shown.Length > 90) { shown = shown.Substring(0, 90) + "…"; }
            // 영어·일본어 화면에 한글이 남아 있으면 번역이 빠진 것이다. 대화·목록 안의 글자(닉네임, 대사)는 뺀다.
            if (GameLocale.Code != "ko" && text.GetComponentInParent<ScrollRect>() == null && HasHangul(text.text))
            { korean++; if (korean <= 40) { report.AppendLine("KOREAN " + name + " / " + Trail(text.transform, rect) + " / " + shown); } }
            if (text.isTextOverflowing)
            { overflow++; if (overflow <= 30) { report.AppendLine("CUT " + name + " / " + Trail(text.transform, rect) + " / " + shown); } continue; }
            // 넘쳐도 그대로 그리는 글자: 자기 칸보다 크면 옆 칸을 덮을 수 있다. 스크롤 안의 글자는 원래 길 수 있으므로 뺀다.
            if (text.overflowMode != TMPro.TextOverflowModes.Overflow || text.GetComponentInParent<ScrollRect>() != null) { continue; }
            var box = text.rectTransform.rect; var size = text.GetPreferredValues(text.text, text.textWrappingMode == TMPro.TextWrappingModes.NoWrap ? 0f : box.width, 0f);
            if (size.x > box.width + 4f || size.y > box.height + 4f)
            { spill++; if (spill <= 30) { report.AppendLine("SPILL " + name + " / " + Trail(text.transform, rect) + " / needs " + size.x.ToString("0") + "x" + size.y.ToString("0") + " has " + box.width.ToString("0") + "x" + box.height.ToString("0") + " / " + shown); } }
        }
        report.AppendLine("SHOT " + name + " cut=" + overflow + " spill=" + spill + (GameLocale.Code != "ko" ? " korean=" + korean : ""));
        var area = rect.rect;
        var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        Vector3 center = rect.TransformPoint(area.center), forward = -rect.forward;
        float worldHeight = Vector3.Distance(rect.TransformPoint(new Vector3(0, area.yMin, 0)), rect.TransformPoint(new Vector3(0, area.yMax, 0)));
        float worldWidth = Vector3.Distance(rect.TransformPoint(new Vector3(area.xMin, 0, 0)), rect.TransformPoint(new Vector3(area.xMax, 0, 0)));
        // 화면이 가로·세로 어느 쪽으로도 잘리지 않는 거리.
        const float view = 27f; float tangent = Mathf.Tan(view * .5f * Mathf.Deg2Rad);
        float distance = Mathf.Max(worldHeight, worldWidth * height / width) * .53f / tangent;
        camera.transform.position = center + forward * distance; camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, rect.up); camera.fieldOfView = view;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
    }

    private static bool HasHangul(string value)
    {
        foreach (char letter in value) { if (letter >= 0xAC00 && letter <= 0xD7A3) { return true; } }
        return false;
    }
    /// <summary>화면 위에 덮어 그리는 메뉴는 카메라에 잠깐 붙여서 찍는다.</summary>
    /// <summary>플레이어 카메라가 보는 방 그대로를 찍는다.</summary>
    private static void ShootView(string name)
    {
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        report.AppendLine("VIEW " + name);
    }
    private static void ShootDesk(string name)
    {
        var desk = GameObject.Find("desk_body"); if (desk == null) { report.AppendLine("VIEW " + name + " skipped: no desk"); return; }
        var bounds = desk.GetComponent<Renderer>().bounds; float reach = Mathf.Max(bounds.size.x, bounds.size.z);
        report.AppendLine("DESK bounds size " + bounds.size.ToString("F2") + " center " + bounds.center.ToString("F2") + " view forward " + camera.transform.forward.ToString("F2"));
        // 방의 큰 면(벽·바닥·천장)과 조명, 카메라 자리를 적어 둔다.
        report.AppendLine("CAMERA position " + camera.transform.position.ToString("F2") + " forward " + camera.transform.forward.ToString("F2") + " fov " + camera.fieldOfView.ToString("F1") + " near " + camera.nearClipPlane.ToString("F2") + " parent " + (camera.transform.parent == null ? "none" : camera.transform.parent.name));
        foreach (var surface in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (surface.bounds.size.magnitude < 4f) { continue; }
            report.AppendLine("SURFACE " + surface.name + " center " + surface.bounds.center.ToString("F2") + " size " + surface.bounds.size.ToString("F2") + " material " + (surface.sharedMaterial == null ? "none" : surface.sharedMaterial.name) + " collider " + (surface.GetComponent<Collider>() != null) + " parent " + (surface.transform.parent == null ? "none" : surface.transform.parent.name));
        }
        foreach (var glow in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        { report.AppendLine("LIGHT " + glow.name + " " + glow.type + " position " + glow.transform.position.ToString("F2") + " range " + glow.range.ToString("F1") + " intensity " + glow.intensity.ToString("F2")); }
        var clutter = Object.FindAnyObjectByType<DeskClutter>();
        if (clutter != null) { report.AppendLine("CLUTTER moved " + clutter.Moved + " removed " + clutter.Removed + " left " + (GameObject.Find("Area :: Desk Clutter") == null ? 0 : GameObject.Find("Area :: Desk Clutter").transform.childCount)); }
        foreach (var note in Object.FindObjectsByType<StickyNotePaper>(FindObjectsInactive.Include))
        {
            report.AppendLine("NOTE " + note.name + " active " + note.gameObject.activeInHierarchy + " released " + note.Released + " settled " + note.Settled + " position " + note.transform.position.ToString("F2") + " up " + note.transform.up.ToString("F2"));
            var resting = note.transform.Find("Mesh :: Resting Paper");
            if (resting != null)
            {
                var shape = resting.GetComponent<MeshFilter>().sharedMesh; var drawn = resting.GetComponent<MeshRenderer>();
                report.AppendLine("  resting vertices " + shape.vertexCount + " local bounds " + shape.bounds.center.ToString("F3") + " / " + shape.bounds.size.ToString("F3") + " world bounds " + drawn.bounds.center.ToString("F2") + " / " + drawn.bounds.size.ToString("F2") + " enabled " + drawn.enabled + " cull " + drawn.sharedMaterial.GetFloat("_Cull") + " first vertex " + shape.vertices[0].ToString("F3"));
            }
        }
        var drawer = GameObject.Find("desk_drawer");
        if (drawer != null)
        {
            var mesh = drawer.GetComponent<MeshFilter>().sharedMesh; var box = drawer.GetComponent<Renderer>().bounds;
            report.AppendLine("DRAWER world size " + box.size.ToString("F2") + " center " + box.center.ToString("F2") + " vertices " + mesh.vertexCount + " triangles " + mesh.triangles.Length / 3 + " local size " + mesh.bounds.size.ToString("F3") + " forward " + drawer.transform.forward.ToString("F2") + " up " + drawer.transform.up.ToString("F2") + " camera " + camera.transform.position.ToString("F2"));
            // 서랍이 속이 빈 통인지 보려고, 찍는 동안만 플레이어 쪽으로 빼 놓는다.
            if (only.Contains("drawerout")) { Vector3 pull = -camera.transform.forward; pull.y = 0f; drawer.transform.position += pull.normalized * box.size.z * .7f; }
        }
        var holder = new GameObject("Camera :: Desk Check"); var view = holder.AddComponent<Camera>(); view.CopyFrom(camera); view.fieldOfView = 55f;
        // 플레이어 자리 쪽 위에서 책상을 내려다본다.
        Vector3 back = -camera.transform.forward; back.y = 0f; back.Normalize();
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        holder.transform.position = top + back * reach * .75f + Vector3.up * reach * .55f; holder.transform.LookAt(top);
        var lamp = new GameObject("Light :: Desk Check").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = reach * 4f; lamp.intensity = reach * reach * .9f;
        lamp.transform.position = top + back * reach * .3f + Vector3.up * reach * .8f;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
        view.targetTexture = texture; view.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        view.targetTexture = null; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        Object.Destroy(holder); Object.Destroy(lamp.gameObject);
        report.AppendLine("VIEW " + name);
    }
    private static void ShootProp(string model)
    {
        var item = GameObject.Find(model); if (item == null) { report.AppendLine("PROP missing " + model); return; }
        var renderers = item.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) { return; }
        var bounds = renderers[0].bounds; int triangles = 0;
        foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); var filter = renderer.GetComponent<MeshFilter>(); if (filter != null && filter.sharedMesh != null) { triangles += filter.sharedMesh.triangles.Length / 3; } }
        float reach = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        var holder = new GameObject("Camera :: Prop Check"); var view = holder.AddComponent<Camera>(); view.CopyFrom(camera); view.fieldOfView = 34f; view.nearClipPlane = .02f;
        Vector3 back = -camera.transform.forward; back.y = 0f; back.Normalize();
        holder.transform.position = bounds.center + back * reach * 1.7f + Vector3.up * reach * 1.15f + Vector3.Cross(Vector3.up, back) * reach * .6f; holder.transform.LookAt(bounds.center);
        var lamp = new GameObject("Light :: Prop Check").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = reach * 12f; lamp.intensity = reach * reach * 9f;
        lamp.transform.position = bounds.center + back * reach * 1.2f + Vector3.up * reach * 2.2f - Vector3.Cross(Vector3.up, back) * reach * 1.0f;
        const int width = 900, height = 700;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
        view.targetTexture = texture; view.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        string file = "audit/prop-" + model.Replace("Model :: ", "").Replace(" ", "-").ToLowerInvariant();
        File.WriteAllBytes(Path.Combine(Folder, file + ".png"), pixels.EncodeToPNG());
        view.targetTexture = null; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        Object.Destroy(holder); Object.Destroy(lamp.gameObject);
        report.AppendLine("PROP " + model + " triangles " + triangles + " -> " + file);
    }
    /// <summary>정해 준 자리에서 정해 준 곳을 바라보고 찍는다. 찍는 동안만 등을 켠다. 게임의 카메라는 건드리지 않는다.</summary>
    private static void ShootFrom(string name, Vector3 eye, Vector3 target, float fieldOfView, float brightness)
    {
        var holder = new GameObject("Camera :: Room Check"); var view = holder.AddComponent<Camera>(); view.CopyFrom(camera); view.fieldOfView = fieldOfView;
        holder.transform.position = eye; holder.transform.LookAt(target);
        var glowHolder = new GameObject("Light :: Room Check"); var fill = glowHolder.AddComponent<Light>(); fill.type = LightType.Point; fill.range = 30f; fill.intensity = brightness * 8f;
        glowHolder.transform.position = eye + Vector3.up * 1.2f;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
        view.targetTexture = texture; view.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        view.targetTexture = null; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        Object.Destroy(holder); Object.Destroy(glowHolder);
        report.AppendLine("VIEW " + name);
    }
    private static void ShootOverlay(Canvas canvas, string name)
    {
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .2f;
        Canvas.ForceUpdateCanvases();
        int cut = 0;
        foreach (var text in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
        {
            if (string.IsNullOrEmpty(text.text)) { continue; }
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) { cut++; report.AppendLine("CUT " + name + " / " + text.name + " / " + text.text); }
        }
        report.AppendLine("SHOT " + name + " cut=" + cut);
        camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
    }
    private static string Trail(Transform item, Transform root)
    {
        string path = item.name;
        for (var parent = item.parent; parent != null && parent != root && path.Length < 90; parent = parent.parent) { path = parent.name + " > " + path; }
        return path;
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Restore();
        if (error != null) { report.AppendLine("FAIL phase " + phase + " " + error); }
        report.AppendLine("runtime errors: " + errors.Count); foreach (string line in errors.Take(12)) { report.AppendLine(line); }
        report.AppendLine(error == null ? "DONE" : "STOPPED");
        File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
        running = false;
        if (Application.isBatchMode) { EditorApplication.Exit(error == null && errors.Count == 0 ? 0 : 1); }
        else if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
    }
}
