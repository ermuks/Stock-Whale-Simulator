using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 새 게임의 미리 그린 차트와 개미의 하루 벌이를 확인하는 도구. 창 없이 돌린다:
//   Unity.exe -batchmode -projectPath <프로젝트> -executeMethod ChartSeedCapture.RunBatch -logFile <로그>
//  1) 종목마다 미리 그린 분봉을 검사한다: 호가 단위에 맞는가, 오르내림의 폭은 어떤가, 마지막 종가가 지금 값과 같은가.
//  2) 두 종목의 차트를 기본·1분·5분·15분·1시간·1일 보기로 찍는다.
//  3) 첫날 장을 끝까지 넘기고 둘째 날 장이 열릴 때 개미 계좌에 실제로 들어온 돈을 센다.
// 장을 넘길 때는 다른 점검 도구와 같은 길을 쓴다(장 시계를 끝으로 옮기고, 정산 화면을 닫고, 아침을 시작한다). 게임 속도는 바꾸지 않는다.
// 씬과 프로젝트 설정은 저장하지 않고, 게임 저장은 Validation/Chart/save 로 돌린다. 결과는 Validation/Chart 에 남는다.
public static class ChartSeedCapture
{
    private const string Folder = "Validation/Chart", ScenePath = "Assets/Scenes 1/TradeScene.unity";
    private const double Budget = 540;
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new(), payLogs = new();
    private static readonly List<(string name, Action act, double wait, Func<bool> until)> steps = new();
    private static readonly Dictionary<TradeBot, decimal> cashBefore = new();
    private static bool running, prepared, sawPlaying;
    private static double began, ready, stepSince;
    private static int step;
    private static Camera camera;
    private static bool oldOptions, oldBackground;
    private static EnterPlayModeOptions oldFlags;
    private static readonly FieldInfo Elapsed = typeof(MarketDayController).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void RunBatch()
    {
        try
        {
            Directory.CreateDirectory(Folder); report.Clear(); errors.Clear(); payLogs.Clear(); cashBefore.Clear();
            foreach (string old in Directory.GetFiles(Folder, "*.png")) { File.Delete(old); }
            if (File.Exists(Folder + "/report.txt")) { File.Delete(Folder + "/report.txt"); }
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Folder + "/save");
            foreach (string old in Directory.GetFiles(Folder + "/save")) { File.Delete(old); }
            Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", Path.GetFullPath(Folder + "/save"));
            oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions; oldBackground = Application.runInBackground;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Application.runInBackground = true;
            running = true; prepared = false; sawPlaying = false; step = 0; began = EditorApplication.timeSinceStartup; ready = began + 3; stepSince = 0;
            Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED before play: " + error + Environment.NewLine);
            EditorApplication.Exit(3);
        }
    }
    private static void Log(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) { errors.Add(message + "\n" + stack); }
        if (type == LogType.Log && (message.Contains("하루 벌이") || message.Contains("매도 수수료") || message.Contains("통정매매 중단") || message.Contains("수수료 0% 이벤트"))) { payLogs.Add(message); }
    }

    private static void Tick()
    {
        if (!running) { return; }
        if (EditorApplication.timeSinceStartup - began > Budget) { Finish(new Exception("timeout at step " + step + " prepared=" + prepared)); return; }
        if (EditorApplication.isPlaying) { sawPlaying = true; }
        else if (sawPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(new Exception("play mode ended before the run finished")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready) { return; }
        try
        {
            if (!prepared) { Prepare(); return; }
            if (step >= steps.Count) { Finish(null); return; }
            var next = steps[step];
            // 조건이 있는 단계는 그 조건이 될 때까지 기다린다(2분까지).
            if (next.until != null && !next.until())
            {
                if (stepSince <= 0) { stepSince = EditorApplication.timeSinceStartup; }
                if (EditorApplication.timeSinceStartup - stepSince > 120) { throw new Exception("condition never met: " + next.name + " (" + State() + ")"); }
                ready = EditorApplication.timeSinceStartup + .1; return;
            }
            stepSince = 0; step++;
            next.act();
            ready = EditorApplication.timeSinceStartup + next.wait;
        }
        catch (Exception error) { Finish(error); }
    }
    private static string State()
    {
        var day = MarketDayController.Instance;
        return day == null ? "no market" : "day " + day.Day + " phase " + day.Phase + " report " + day.ReportPending + " menu " + PauseMenuController.IsOpen + " timeScale " + Time.timeScale;
    }

    private static void Prepare()
    {
        ready = EditorApplication.timeSinceStartup + .5;
        if (StockManager.Instance == null) { return; }
        if (!StockManager.Instance.SetupConfirmed)
        {
            if (NewGame.CanBegin) { NewGame.Begin(); }
            ready = EditorApplication.timeSinceStartup + 1; return;
        }
        var views = DeskViewController.Instance; var shell = RoomShell.Instance; var intro = DayIntroOverlay.Instance; var history = CandleHistoryManager.Instance;
        string waiting = views == null || !views.Ready ? "desk view" : shell == null || !shell.Built ? "room" : RoomShell.Cinematic ? "cinematic" : intro != null && intro.Playing ? "day intro"
            : history == null ? "candle history" : history.MinuteHistory(StockManager.Instance.stocks[0]).Count == 0 ? "seeded minutes" : null;
        if (waiting != null)
        {
            if (stepSince <= 0) { stepSince = EditorApplication.timeSinceStartup; }
            if (EditorApplication.timeSinceStartup - stepSince > 150) { throw new Exception("still waiting for: " + waiting); }
            return;
        }
        prepared = true; stepSince = 0; camera = views.ViewCamera;
        foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
        Time.timeScale = 1f;
        Inspect(history);
        Plan(views);
    }

    /// <summary>종목마다 미리 그린 분봉을 검사해 적는다.</summary>
    private static void Inspect(CandleHistoryManager history)
    {
        report.AppendLine("SEED " + CandleHistoryManager.SeedDays + " days x " + CandleHistoryManager.SeedMinutesPerDay + " minutes, base candles " + CandleHistoryManager.SeedBaseCandles);
        foreach (var stock in StockManager.Instance.stocks)
        {
            var minutes = history.MinuteHistory(stock); var baseCandles = history.Completed(stock);
            if (minutes.Count == 0) { report.AppendLine("STOCK " + stock.symbol + " no seeded minutes (listed " + stock.IsListed + ")"); continue; }
            int offTick = 0, broken = 0, flat = 0, seeded = 0, daily = 0; float low = float.MaxValue, high = 0f, dailyLow = float.MaxValue, dailyHigh = 0f; double biggest = 0d, sum = 0d, dailySum = 0d;
            var dayRange = new Dictionary<int, (float low, float high, float open, float close)>();
            for (int i = 0; i < minutes.Count; i++)
            {
                var candle = minutes[i];
                if (!candle.historical) { continue; }
                seeded++;
                // 일봉으로만 만든 먼 지난 날은 호가 단위만 본다(분봉 통계에는 넣지 않는다).
                if (candle.completedTime == CandleHistoryManager.DailyOnly)
                {
                    daily++;
                    foreach (float value in new[] { candle.open, candle.high, candle.low, candle.close }) { if (!PriceTickPolicy.IsValidPrice((decimal)value)) { offTick++; } }
                    if (candle.high < Mathf.Max(candle.open, candle.close) || candle.low > Mathf.Min(candle.open, candle.close)) { broken++; }
                    dailyLow = Mathf.Min(dailyLow, candle.low); dailyHigh = Mathf.Max(dailyHigh, candle.high); dailySum += Math.Abs(candle.close - candle.open) / candle.open;
                    continue;
                }
                foreach (float value in new[] { candle.open, candle.high, candle.low, candle.close }) { if (!PriceTickPolicy.IsValidPrice((decimal)value)) { offTick++; } }
                if (candle.high < Mathf.Max(candle.open, candle.close) || candle.low > Mathf.Min(candle.open, candle.close)) { broken++; }
                if (candle.open == candle.close) { flat++; }
                low = Mathf.Min(low, candle.low); high = Mathf.Max(high, candle.high);
                double move = Math.Abs(candle.close - candle.open) / candle.open; biggest = Math.Max(biggest, move); sum += move;
                var day = dayRange.TryGetValue(candle.marketDay, out var found) ? found : (candle.low, candle.high, candle.open, candle.close);
                dayRange[candle.marketDay] = (Mathf.Min(day.low, candle.low), Mathf.Max(day.high, candle.high), day.open, candle.close);
            }
            var days = new StringBuilder();
            foreach (var pair in dayRange) { days.Append(' ').Append(pair.Key).Append(':').Append(((pair.Value.close - pair.Value.open) / pair.Value.open * 100f).ToString("+0.0;-0.0")).Append("%/").Append(((pair.Value.high - pair.Value.low) / pair.Value.open * 100f).ToString("0.0")).Append('%'); }
            var last = minutes[seeded - 1];
            int minuteCount = Math.Max(1, seeded - daily); seeded = minuteCount + daily;
            var stats = StockStatistics.Instance.GetStats(stock); float allLow = Mathf.Min(low, dailyLow), allHigh = Mathf.Max(high, dailyHigh);
            report.AppendLine("PANEL " + stock.symbol + " start price " + stock.currentPrice.ToString("N0") + " | 52w high " + stats.week52High.ToString("N0") + " low " + stats.week52Low.ToString("N0")
                + " | chart high " + allHigh.ToString("N0") + " low " + allLow.ToString("N0") + ((float)stats.week52High == allHigh && (float)stats.week52Low == allLow ? " (match)" : " (DIFFERS)") + " | prev close " + stats.prevDayClose.ToString("N0"));
            report.AppendLine("DAILY " + stock.symbol + " days " + daily + " range " + dailyLow.ToString("N0") + "~" + dailyHigh.ToString("N0") + " avg day move " + (dailySum / Math.Max(1, daily) * 100d).ToString("0.00") + "% joins minute history at " + minutes[daily].open.ToString("N0") + " after close " + (daily > 0 ? minutes[daily - 1].close.ToString("N0") : "-"));
            report.AppendLine("STOCK " + stock.symbol + " price " + stock.currentPrice.ToString("N0") + " tick " + PriceTickPolicy.GetTickSize(stock.currentPrice) + " minutes " + minuteCount + " off-tick " + offTick + " broken " + broken
                + " last close " + last.ClosePrice.ToString("N0") + (last.ClosePrice == stock.currentPrice ? " (=price)" : " (DIFFERS)") + " range " + low.ToString("N0") + "~" + high.ToString("N0") + " (" + ((high - low) / (float)stock.currentPrice * 100f).ToString("0.0") + "%)"
                + " avg minute move " + (sum / Math.Max(1, seeded) * 100d).ToString("0.000") + "% biggest " + (biggest * 100d).ToString("0.00") + "% flat " + (flat * 100 / Math.Max(1, seeded)) + "% base " + baseCandles.Count + " | day change/range:" + days);
        }
    }

    /// <summary>
    /// 프롤로그를 처음부터 끝까지 틀어 놓고 장면마다 한 장씩 찍는다(DONDON_CAPTURE=intro, DONDON_CAPTURE_LANG로 언어).
    /// 자료(그림·시간표·대사)가 하나라도 맞지 않으면 프롤로그는 통째로 건너뛰어진다. 그때는 shown False로 적힌다.
    /// </summary>
    private static void PlanIntro()
    {
        steps.Clear();
        string language = Environment.GetEnvironmentVariable("DONDON_CAPTURE_LANG");
        if (language == "en" || language == "ja" || language == "ko") { steps.Add(("language " + language, () => GameLocale.SetLanguage(language), .6, null)); }
        bool finished = false; string seen = "";
        steps.Add(("play intro", () => { bool shown = StoryIntro.Play(() => finished = true); report.AppendLine("INTRO shown " + shown + " | playing " + StoryIntro.Playing); }, 1.6, null));
        // DONDON_CAPTURE_TIMES="2.2,2.6,6.5"처럼 주면 영상이 그 시각에 닿는 순간마다 한 장씩 찍는다(영상 속 표시의 자리를 볼 때 쓴다).
        string times = Environment.GetEnvironmentVariable("DONDON_CAPTURE_TIMES");
        if (!string.IsNullOrEmpty(times))
        {
            foreach (string part in times.Split(','))
            {
                if (!double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double at)) { continue; }
                steps.Add(("intro at " + part, () =>
                {
                    report.AppendLine("INTRO at " + part + " video " + (StoryIntro.Instance != null ? StoryIntro.Instance.VideoTime.ToString("0.00") : "ended"));
                    if (StoryIntro.Instance != null) { Shoot("intro-at-" + part.Trim().Replace('.', '_')); }
                }, .02, () => StoryIntro.Instance == null || StoryIntro.Instance.VideoTime >= at));
            }
            steps.Add(("intro skip", () => StoryIntro.Instance?.Skip(), 2.5, null));
            return;
        }
        for (int i = 0; i < 16; i++)
        {
            int index = i;
            steps.Add(("intro frame " + index, () =>
            {
                string scene = StoryIntro.Instance != null ? StoryIntro.Instance.ShownScene : "(ended)";
                report.AppendLine("INTRO step " + index + " scene " + scene + " | finished " + finished + (StoryIntro.Instance != null ? " | elapsed " + StoryIntro.Instance.Elapsed.ToString("0.00") + " | video " + StoryIntro.Instance.UsesVideo + " running " + StoryIntro.Instance.VideoRunning + " at " + StoryIntro.Instance.VideoTime.ToString("0.00") + " | caption " + StoryIntro.Instance.ShownCaptionKey + " alpha " + StoryIntro.Instance.ShownCaptionAlpha.ToString("0.00") : ""));
                if (StoryIntro.Instance != null) { seen = scene; Shoot("intro-" + (index + 1).ToString("00") + "-" + scene); }
            }, 2.0, null));
        }
        steps.Add(("intro end", () => report.AppendLine("INTRO finished " + finished + " | still playing " + StoryIntro.Playing + " | finish count " + StoryIntro.FinishCount), .5, null));
    }

    /// <summary>
    /// 두 태블릿의 모든 탭을 찍는다(DONDON_CAPTURE=tablets). 1번은 보는 곳(자산·기록·뉴스), 2번은 하는 곳(주문·작전·내 기업·기사·밤 준비)이다.
    /// 화면에 보일 것이 있도록, 장이 열린 뒤 두 종목을 조금 사고 작전 도구 몇 개를 서로 다른 종목에 켜 둔다(실제 주문이다).
    /// DONDON_CAPTURE_LANG에 en이나 ja를 주면 그 언어로 찍는다.
    /// </summary>
    private static void PlanTablets(DeskViewController views)
    {
        steps.Clear();
        var market = MarketDayController.Instance; var manager = StockManager.Instance; var player = PlayerAccounts.Instance.primary;
        string language = Environment.GetEnvironmentVariable("DONDON_CAPTURE_LANG");
        if (language == "en" || language == "ja" || language == "ko") { steps.Add(("language " + language, () => GameLocale.SetLanguage(language), .6, null)); }
        TabletAccountUI first = null, second = null;
        foreach (var tablet in Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include)) { if (tablet.second) { second = tablet; } else { first = tablet; } }
        void Press(TabletAccountUI tablet, string name)
        {
            Button found = null;
            foreach (var button in tablet.GetComponentsInChildren<Button>(true)) { if (button.name == name) { found = button; } }
            report.AppendLine("PRESS " + name + " found " + (found != null));
            if (found != null) { found.onClick.Invoke(); }
        }
        steps.Add(("wait for trading", () => report.AppendLine("TABLETS start: " + State() + " | cash " + player.cash.ToString("N0") + " | company founded " + PlayerProgression.Instance.CompanyFounded + " | rooms " + PlayerProgression.Instance.CanOperateRooms), 25, () => MarketDayController.CanTrade));
        steps.Add(("buy two stocks", () =>
        {
            foreach (int index in new[] { 1, 4 })
            {
                var stock = manager.stocks[index]; stock.orderBook.TryBest(out _, out decimal ask);
                if (ask > 0m) { player.BuyLimit(PriceTickPolicy.CeilToTick(ask * 1.01m), decimal.Floor(60_000_000m / ask), 0, stock); }
            }
        }, 6, null));
        steps.Add(("start tools", () =>
        {
            var tactics = PlayerTactics.Instance;
            report.AppendLine("TOOLS wash " + tactics.StartWash(manager.stocks[1]) + " | wall " + tactics.SetWall(manager.stocks[4], .1f) + " | close " + tactics.ArmClose(manager.stocks[2], .05f) + " | ignite " + tactics.Ignite(manager.stocks[1], .05f));
            manager.SelectStock(1);
        }, 8, null));
        steps.Add(("desk", () => Shoot("tablets-00-desk"), .3, null));
        // 모니터 세 대를 게임의 확대 시점(1·2·3번 프리셋)에서 그대로 찍는다. 검수는 이 카메라 기준이다.
        steps.Add(("focus stocks", () => views.Focus(DeskViewTarget.Stocks), 1.8, null));
        steps.Add(("shot stocks", () => Shoot("tablets-01-monitor-stocks"), .3, null));
        steps.Add(("focus chart", () => views.Focus(DeskViewTarget.Chart), 1.8, null));
        steps.Add(("shot chart", () => Shoot("tablets-02-monitor-chart"), .3, null));
        steps.Add(("focus chat", () => views.Focus(DeskViewTarget.Chat), 1.8, null));
        steps.Add(("shot chat", () => Shoot("tablets-03-monitor-chat"), .3, null));
        steps.Add(("back to desk", () => views.ExitView(true), 1.2, null));
        steps.Add(("open first tablet", () => views.Focus(DeskViewTarget.TabletOne), 1.8, null));
        steps.Add(("shot assets", () => { report.AppendLine("FIRST page " + first.CurrentPage + " locked " + first.Locked); Shoot("tablets-1a-assets"); }, .3, null));
        steps.Add(("records", () => first.ShowPage(TabletAccountUI.Page.Records), 1.0, null));
        steps.Add(("shot records", () => Shoot("tablets-1b-records"), .3, null));
        steps.Add(("news", () => first.ShowPage(TabletAccountUI.Page.Events), 1.2, null));
        steps.Add(("shot news", () => { Shoot("tablets-1c-news"); first.ShowPage(TabletAccountUI.Page.Dashboard); }, .3, null));
        steps.Add(("open second tablet", () => views.Focus(DeskViewTarget.TabletTwo), 1.8, null));
        steps.Add(("orders", () => { report.AppendLine("SECOND page " + second.CurrentPage + " locked " + second.Locked); second.ShowPage(TabletAccountUI.Page.Orders); }, 1.2, null));
        steps.Add(("shot orders", () => Shoot("tablets-2a-orders"), .3, null));
        steps.Add(("ops", () => second.ShowPage(TabletAccountUI.Page.Automation), 1.2, null));
        steps.Add(("spread tab", () => Press(second, "Button :: Tab Spread"), 1.0, null));
        steps.Add(("shot spread", () => Shoot("tablets-2b-spread"), .3, null));
        steps.Add(("accumulate tab", () => Press(second, "Button :: Tab Accumulate"), 1.0, null));
        steps.Add(("shot accumulate", () => Shoot("tablets-2c-accumulate"), .3, null));
        steps.Add(("tactics tab", () => Press(second, "Button :: Tab Tactics"), 1.5, null));
        steps.Add(("shot tactics", () => Shoot("tablets-2d-tactics"), .3, null));
        steps.Add(("pick wash", () => Press(second, "Button :: Tool 4"), 3.9, null));
        steps.Add(("shot tactics wash", () => Shoot("tablets-2e-tactics-wash-running"), .3, null));
        steps.Add(("other stock", () => { manager.SelectStock(4); Press(second, "Button :: Tool 5"); }, 3.9, null));
        steps.Add(("shot tactics wall", () => Shoot("tablets-2f-tactics-wall-other-stock"), .3, null));
        steps.Add(("company", () => second.ShowPage(TabletAccountUI.Page.Management), 1.5, null));
        steps.Add(("shot company", () => Shoot("tablets-2g-company"), .3, null));
        steps.Add(("press", () => second.ShowPage(TabletAccountUI.Page.Special), 1.5, null));
        steps.Add(("shot press", () => Shoot("tablets-2h-press"), .3, null));
        steps.Add(("night", () => second.ShowPage(TabletAccountUI.Page.Night), 1.2, null));
        steps.Add(("shot night", () => { Shoot("tablets-2i-night"); second.ShowPage(TabletAccountUI.Page.Automation); }, .3, null));
        steps.Add(("leave", () =>
        {
            views.ExitView(true);
            var tactics = PlayerTactics.Instance; var book = tactics.Book(manager.stocks[1]);
            report.AppendLine("LEDGER " + manager.stocks[1].symbol + " bought " + book.Bought.ToString("N0") + " spent " + book.Spent.ToString("N0") + " sold " + book.Sold.ToString("N0") + " realized " + book.Realized.ToString("N0") + " | runs " + tactics.Runs.Count + " | player holds " + player.GetOwnedVolume(manager.stocks[1]).ToString("N0"));
        }, .4, null));
    }

    /// <summary>
    /// 해금 탭과 작전 탭을 찍는다(환경 변수 DONDON_CAPTURE=unlocks 일 때). 해금 항목을 하나씩 골라 설명을 찍고,
    /// 둘째 태블릿은 순자산 1억 돈이 넘어야 열리므로 이 실행에서만 현금을 넣어 연다(저장되지 않는다).
    /// </summary>
    private static void PlanUnlocks(DeskViewController views)
    {
        steps.Clear();
        var career = PlayerProgression.Instance;
        report.AppendLine("UNLOCK items: " + career.UnlockItems.Count + " | tactic turnover needed " + career.rules.tacticCloseTurnover.ToString("N0"));
        foreach (var item in career.UnlockItems)
        { report.AppendLine("UNLOCK " + item.id + " | " + item.LocalTitle + " | cost " + item.cost.ToString("N0") + " | minutes " + item.minutes + " | requires " + item.requires + " | state " + career.StateOf(item) + " | lines " + item.LocalDescription.Split('\n').Length); }
        foreach (PlayerTactics.Tool tool in Enum.GetValues(typeof(PlayerTactics.Tool)))
        { report.AppendLine("TACTIC " + tool + " unlocked " + PlayerTactics.Unlocked(tool) + " | lock reason " + PlayerTactics.LockReason(tool)); }
        TabletAccountUI first = null, second = null;
        foreach (var tablet in Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include)) { if (tablet.second) { second = tablet; } else { first = tablet; } }
        var select = typeof(UnlockGridUI).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
        steps.Add(("open first tablet", () => views.Focus(DeskViewTarget.TabletOne), 1.5, null));
        steps.Add(("unlock page", () => first.ShowPage(TabletAccountUI.Page.Unlocks), 1.0, null));
        foreach (string id in new[] { PlayerProgression.CrowdValueUnlock, PlayerProgression.NightPlantUnlock, PlayerProgression.CloseUnlock, PlayerProgression.WallUnlock })
        {
            string pick = id;
            steps.Add(("select " + pick, () => select.Invoke(first.GetComponentInChildren<UnlockGridUI>(true), new object[] { pick }), 3.7, null));
            steps.Add(("shot " + pick, () => Shoot("unlock-" + pick.Replace('.', '-')), .2, null));
        }
        steps.Add(("fund second tablet", () =>
        {
            views.ExitView(true);
            var account = PlayerAccounts.Instance.primary; account.cash += 300_000_000m; PlayerAccounts.Instance.EvaluateUnlock();
            report.AppendLine("SECOND tablet locked " + second.Locked);
        }, .6, null));
        steps.Add(("open second tablet", () => views.Focus(DeskViewTarget.TabletTwo), 1.5, null));
        steps.Add(("tactics tab", () =>
        {
            Button tab = null;
            foreach (var button in second.GetComponentsInChildren<Button>(true)) { if (button.name == "Button :: Tab Tactics") { tab = button; } }
            report.AppendLine("TACTICS tab found " + (tab != null));
            if (tab != null) { tab.onClick.Invoke(); }
        }, 1.2, null));
        // 프리뷰가 다 그려진 뒤에 찍는다. 도구를 바꿔 가며 세 가지를 본다.
        foreach (int pick in new[] { 0, 3, 4 })
        {
            int index = pick;
            steps.Add(("pick tool " + index, () => { foreach (var button in second.GetComponentsInChildren<Button>(true)) { if (button.name == "Button :: Tool " + index) { button.onClick.Invoke(); } } }, 3.9, null));
            steps.Add(("shot tool " + index, () => Shoot("tactics-tool-" + index), .2, null));
        }
        steps.Add(("leave", () => views.ExitView(true), .4, null));
    }

    /// <summary>
    /// 매도 수수료를 확인한다(환경 변수 DONDON_CAPTURE=fees 일 때). 첫날 장을 실제 속도로 얼마간 돌린 뒤,
    /// 걷힌 수수료가 봇들의 매도 대금의 0.05%인지, 봇들이 낸 돈의 합과 같은지, 플레이어와 거래소는 내지 않는지 적는다.
    /// 그다음 장을 끝까지 넘겨 마감 기록(거래소의 그날 수수료 수입)이 남는지 본다.
    /// </summary>
    private static void PlanFees()
    {
        steps.Clear();
        var market = MarketDayController.Instance; decimal exchangeBefore = 0m;
        steps.Add(("trading starts", () =>
        {
            exchangeBefore = ExchangeBot.Instance.cash;
            var player = PlayerAccounts.Instance.primary;
            report.AppendLine("FEE rate " + Trader.SellFeeRate + " | pays: exchange " + ExchangeBot.Instance.PaysSellFee + ", player " + player.PaysSellFee + ", a retail bot " + market.Bots[5].PaysSellFee + " (" + market.Bots[5].GetType().Name + ")");
            report.AppendLine("FEE at open: exchange cash " + exchangeBefore.ToString("N0") + " collected so far " + Trader.SellFeesCollected.ToString("N2"));
        }, 50, () => MarketDayController.CanTrade && market.Day == 1));
        steps.Add(("after trading", () =>
        {
            decimal paid = 0m, most = 0m; int payers = 0, sellersExempt = 0; string top = "";
            foreach (var trader in Trader.Registry)
            {
                if (trader.SellFeesPaid > 0m) { payers++; paid += trader.SellFeesPaid; if (trader.SellFeesPaid > most) { most = trader.SellFeesPaid; top = trader.GetType().Name; } if (!trader.PaysSellFee) { sellersExempt++; } }
            }
            decimal collected = Trader.SellFeesCollected, basis = Trader.SellFeeBase;
            report.AppendLine("FEE after 50s of trading: collected " + collected.ToString("N2") + " on bot sell proceeds of " + basis.ToString("N0") + " -> rate " + (basis > 0m ? (collected / basis * 100m).ToString("0.00000") : "-") + "%");
            report.AppendLine("FEE paid by " + payers + " accounts, sum " + paid.ToString("N2") + (paid == collected ? " (= collected)" : " (DIFFERS from collected)") + " | exempt accounts that paid: " + sellersExempt + " | biggest payer " + top + " " + most.ToString("N2"));
            report.AppendLine("FEE exchange cash now " + ExchangeBot.Instance.cash.ToString("N0") + " (change " + (ExchangeBot.Instance.cash - exchangeBefore).ToString("N0") + ", which also includes its own trades) | player paid " + PlayerAccounts.Instance.primary.SellFeesPaid.ToString("N2") + " | exchange paid " + ExchangeBot.Instance.SellFeesPaid.ToString("N2"));
            report.AppendLine("WHALE wash rounds " + WhaleBot.WashRoundsTotal + " painted " + WhaleBot.WashPaintedTotal.ToString("N0") + " wash fees " + WhaleBot.WashFeesTotal.ToString("N2"));
            Elapsed.SetValue(market, market.tradingSeconds);
        }, .5, null));
        steps.Add(("closed", () => { foreach (string line in payLogs) { report.AppendLine("LOG " + line); } report.AppendLine("DAY1 closed: " + State()); }, .2, () => market.Phase == MarketPhase.PostClose && market.ReportPending));
    }

    /// <summary>
    /// 수수료 0% 이벤트를 확인한다(DONDON_CAPTURE=holiday, DONDON_FEE_HOLIDAY=2로 둘째 날을 이벤트 날로 못 박고 돌린다).
    /// 첫날: 플레이어가 한 번 사고팔아 수수료를 내는지 보고, 장을 넘겨 예고 문자가 오는지 본다.
    /// 둘째 날: 08:30에 시작 문자·화면 표시·봇들의 대화가 시작되는지, 장이 열리면 세력의 즉흥 작전이 시작되고 수수료가 0으로 걷히는지 본다.
    /// </summary>
    private static void PlanHoliday(DeskViewController views)
    {
        steps.Clear();
        var market = MarketDayController.Instance; var phone = DeskPhone.Instance; var player = PlayerAccounts.Instance.primary;
        TabletAccountUI first = null;
        foreach (var tablet in Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include)) { if (!tablet.second) { first = tablet; } }
        decimal feesBefore = 0m; int chatBefore = 0;
        report.AppendLine("HOLIDAY days: " + string.Join(",", System.Linq.Enumerable.Where(System.Linq.Enumerable.Range(1, 30), FeeHoliday.IsEventDay)) + " (forced " + Environment.GetEnvironmentVariable("DONDON_FEE_HOLIDAY") + ") | player pays fee " + player.PaysSellFee + " | rate now " + Trader.SellFeeRate);
        steps.Add(("player buys", () =>
        {
            var stock = StockManager.Instance.stocks[1];
            stock.orderBook.TryBest(out _, out decimal ask);
            player.BuyLimit(PriceTickPolicy.CeilToTick(ask * 1.02m), 2000, 0, stock);
            report.AppendLine("PLAYER bought " + player.GetHolding(stock).ToString("N0") + " of " + stock.symbol + " near " + ask.ToString("N0"));
        }, 2.0, () => MarketDayController.CanTrade && market.Day == 1 && StockManager.Instance.stocks[1].orderBook.TryBest(out _, out _)));
        steps.Add(("player sells", () =>
        {
            var stock = StockManager.Instance.stocks[1]; decimal held = player.GetHolding(stock), cash = player.cash;
            if (!stock.orderBook.TryBest(out decimal bid, out _) || bid <= 0m) { bid = stock.currentPrice; }
            player.SellLimit(PriceTickPolicy.FloorToTick(bid * .95m), held, 0, stock);
            decimal sold = held - player.GetOwnedVolume(stock), gained = player.cash - cash;
            report.AppendLine("PLAYER sold " + sold.ToString("N0") + " for " + gained.ToString("N2") + " in hand | fee paid " + player.SellFeesPaid.ToString("N2") + " | gross " + (gained + player.SellFeesPaid).ToString("N2")
                + " | rate " + (gained + player.SellFeesPaid > 0m ? (player.SellFeesPaid / (gained + player.SellFeesPaid) * 100m).ToString("0.00000") : "-") + "%");
        }, 1.0, null));
        steps.Add(("order screen day one", () => { views.Focus(DeskViewTarget.TabletOne); first.ShowPage(TabletAccountUI.Page.Orders); }, 2.0, null));
        steps.Add(("shot order day one", () => { Shoot("holiday-1-order-screen-normal"); views.ExitView(true); }, .5, null));
        steps.Add(("skip day one", () => Elapsed.SetValue(market, market.tradingSeconds), .5, null));
        steps.Add(("close report", () => { PauseMenuController.Instance.ContinueAfterReport(); }, 2.5, () => market.Phase == MarketPhase.PostClose && market.ReportPending));
        steps.Add(("notice the day before", () =>
        {
            report.AppendLine("NOTICE after day 1: messages " + phone.Messages.Count + " unread " + phone.UnreadCount + (phone.Messages.Count > 0 ? " | newest: " + phone.Messages[phone.Messages.Count - 1].Heading + " / " + phone.Messages[phone.Messages.Count - 1].BodyText : ""));
            market.StartMorningAfterSleep();
            // 정산 화면을 단추가 아니라 코드로 닫으면 시간이 멈춘 채 남을 때가 있다. 이 도구에서만 다시 흐르게 한다.
            Time.timeScale = 1f;
        }, 1.0, () => market.Phase == MarketPhase.PostClose && !market.ReportPending));
        steps.Add(("event starts", () =>
        {
            feesBefore = Trader.SellFeesCollected; chatBefore = InvestorSociety.Instance.FeeHolidayLines;
            report.AppendLine("EVENT started: " + State() + " clock " + market.ClockMinute / 60 + ":" + (market.ClockMinute % 60).ToString("00") + " | active " + FeeHoliday.Active + " rate " + Trader.SellFeeRate + " text " + FeeHoliday.RateText
                + " | messages " + phone.Messages.Count + " newest: " + phone.Messages[phone.Messages.Count - 1].Heading);
        }, 22, () => market.Day == 2 && FeeHoliday.Started));
        steps.Add(("chat before open", () =>
        {
            report.AppendLine("CHAT lines about the event after 22s: " + (InvestorSociety.Instance.FeeHolidayLines - chatBefore) + " | " + State());
            views.Focus(DeskViewTarget.Chat);
        }, 1.6, null));
        steps.Add(("shot chat", () => { Shoot("holiday-2-chat-before-open"); views.ExitView(true); }, .6, null));
        steps.Add(("shot desk", () => Shoot("holiday-3-desk-with-badge"), .3, null));
        steps.Add(("raise phone", () => phone.Raise(), 1.0, null));
        steps.Add(("shot phone list", () => Shoot("holiday-4-phone-list"), .2, null));
        steps.Add(("open notice", () => phone.OpenMessage(0), .8, null));
        steps.Add(("shot phone notice", () => { Shoot("holiday-5-phone-notice"); phone.Lower(); }, .8, null));
        steps.Add(("wait for open", () => { report.AppendLine("OPEN day 2: " + State()); }, 14, () => MarketDayController.CanTrade && market.Day == 2));
        steps.Add(("after open", () =>
        {
            int operating = 0;
            foreach (var stock in StockManager.Instance.stocks) { if (stock.Owner is WhaleBot whale && whale.Operating) { operating++; } }
            report.AppendLine("WHALES impromptu operations started " + WhaleBot.HolidayOperationsTotal + " | operating now " + operating + " | fees collected on the event day " + (Trader.SellFeesCollected - feesBefore).ToString("N2")
                + " | chat lines " + (InvestorSociety.Instance.FeeHolidayLines - chatBefore) + " | phone messages " + phone.Messages.Count);
            foreach (string line in payLogs) { report.AppendLine("LOG " + line); }
            views.Focus(DeskViewTarget.TabletOne); first.ShowPage(TabletAccountUI.Page.Orders);
        }, 2.0, null));
        steps.Add(("shot order event", () => { Shoot("holiday-6-order-screen-event"); views.ExitView(true); }, .4, null));
    }

    private static void Plan(DeskViewController views)
    {
        if (Environment.GetEnvironmentVariable("DONDON_CAPTURE") == "tablets") { PlanTablets(views); return; }
        if (Environment.GetEnvironmentVariable("DONDON_CAPTURE") == "intro") { PlanIntro(); return; }
        if (Environment.GetEnvironmentVariable("DONDON_CAPTURE") == "unlocks") { PlanUnlocks(views); return; }
        if (Environment.GetEnvironmentVariable("DONDON_CAPTURE") == "fees") { PlanFees(); return; }
        if (Environment.GetEnvironmentVariable("DONDON_CAPTURE") == "holiday") { PlanHoliday(views); return; }
        steps.Clear();
        var chart = Object.FindAnyObjectByType<ChartManager>(); var market = MarketDayController.Instance; var manager = StockManager.Instance;
        int[] periods = { 0, 1, 5, 15, 60, 1440 }; string[] names = { "base", "1m", "5m", "15m", "1h", "1d" };
        // 값이 싼 종목(호가 1돈)과 중간 값의 종목(호가 10돈)을 본다.
        int[] picks = { 0, Mathf.Min(6, manager.stocks.Count - 1) };
        steps.Add(("focus chart", () => views.Focus(DeskViewTarget.Chart), 1.2, null));
        foreach (int pick in picks)
        {
            int index = pick;
            steps.Add(("select " + index, () => { manager.SelectStock(index); report.AppendLine("CHART stock " + manager.CurrentStock.symbol + " " + manager.CurrentStock.stockName); }, .8, null));
            for (int i = 0; i < periods.Length; i++)
            {
                int minutes = periods[i]; string label = names[i];
                steps.Add(("period " + label, () => chart.SetChartPeriod(minutes), .9, null));
                steps.Add(("shot " + label, () => Shoot("chart-" + manager.stocks[index].symbol + "-" + (Array.IndexOf(periods, minutes) + 1) + "-" + label), .2, null));
            }
        }
        steps.Add(("back to base", () => { chart.SetChartPeriod(0); views.ExitView(true); }, .5, null));

        // 둘째 날까지 넘겨서 개미의 하루 벌이가 실제로 들어오는지 본다.
        steps.Add(("skip day one", () => { report.AppendLine("DAY1 trading reached: " + State()); Elapsed.SetValue(market, market.tradingSeconds); }, .5, () => MarketDayController.CanTrade && market.Day == 1));
        steps.Add(("close report", () => { report.AppendLine("DAY1 closed: " + State()); PauseMenuController.Instance.ContinueAfterReport(); }, 1.0, () => market.Phase == MarketPhase.PostClose && market.ReportPending));
        steps.Add(("morning", () => { market.StartMorningAfterSleep(); report.AppendLine("DAY2 morning: " + State()); }, 1.0, () => market.Phase == MarketPhase.PostClose && !market.ReportPending));
        steps.Add(("snapshot", () =>
        {
            // 장이 열리기 전(봇이 쉬는 동안)의 현금을 적어 둔다.
            cashBefore.Clear();
            foreach (var bot in market.Bots) { if (StockManager.IsRetail(bot)) { cashBefore[bot] = bot.cash; } }
            report.AppendLine("DAY2 before open: retail bots " + cashBefore.Count + " of " + market.Bots.Count + " | " + State());
            Time.timeScale = 1f; Elapsed.SetValue(market, 100000f);
        }, 0, () => market.Day == 2 && market.Phase == MarketPhase.PreOpen));
        steps.Add(("compare", () =>
        {
            decimal total = 0m, least = decimal.MaxValue, most = decimal.MinValue; int inRange = 0, other = 0, onStep = 0;
            foreach (var pair in cashBefore)
            {
                decimal gained = pair.Key.cash - pair.Value; total += gained; least = Math.Min(least, gained); most = Math.Max(most, gained);
                if (gained >= StockManager.RetailPayMin && gained <= StockManager.RetailPayMax) { inRange++; if (gained % StockManager.RetailPayStep == 0m) { onStep++; } } else { other++; }
            }
            report.AppendLine("PAY day 2: " + cashBefore.Count + " retail accounts gained " + total.ToString("N0") + " in total, average " + (cashBefore.Count == 0 ? 0m : total / cashBefore.Count).ToString("N0")
                + ", least " + least.ToString("N0") + ", most " + most.ToString("N0") + " | in 300k~700k: " + inRange + " (on 10k step: " + onStep + "), outside: " + other + " | " + State());
            foreach (string line in payLogs) { report.AppendLine("PAY log: " + line); }
        }, .2, () => market.Day == 2 && market.Phase == MarketPhase.Trading));
    }

    /// <summary>플레이어 카메라가 보는 그대로를 게임 화면과 같은 비율로 찍는다.</summary>
    private static void Shoot(string name)
    {
        int factor = Mathf.Max(1, Mathf.CeilToInt(1080f / Mathf.Max(1, Screen.height)));
        int width = Screen.width * factor, height = Screen.height * factor;
        // 화면에 덮어 그리는 표시(이벤트 표시, 휴대폰 안내)는 카메라로 찍히지 않으므로 찍는 동안만 카메라에 붙인다.
        var overlays = new List<(Canvas canvas, float scale)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>())
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.activeInHierarchy && (canvas.name == "Area :: Fee Holiday" || canvas.name == "Area :: Phone Hint" || canvas.GetComponentInParent<StoryIntro>() != null))
            { overlays.Add((canvas, canvas.scaleFactor)); }
        }
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture;
        foreach (var item in overlays)
        { item.canvas.renderMode = RenderMode.ScreenSpaceCamera; item.canvas.worldCamera = camera; item.canvas.planeDistance = camera.nearClipPlane + .2f; item.canvas.scaleFactor = item.scale * factor; }
        Canvas.ForceUpdateCanvases();
        camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        foreach (var item in overlays) { item.canvas.renderMode = RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera = null; item.canvas.scaleFactor = item.scale; }
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        report.AppendLine("VIEW " + name);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags; Application.runInBackground = oldBackground;
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", null);
        if (error != null) { report.AppendLine("FAIL step " + step + (step < steps.Count ? " (" + steps[step].name + ")" : "") + " " + error); }
        report.AppendLine("runtime errors: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 12; i++) { report.AppendLine(errors[i]); }
        report.AppendLine(error == null ? "DONE" : "STOPPED");
        Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
        running = false;
        if (Application.isBatchMode) { EditorApplication.Exit(error == null && errors.Count == 0 ? 0 : 1); }
        else if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
    }
}
