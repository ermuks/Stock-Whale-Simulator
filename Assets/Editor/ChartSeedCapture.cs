using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        if (type == LogType.Log && message.Contains("하루 벌이")) { payLogs.Add(message); }
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

    private static void Plan(DeskViewController views)
    {
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
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
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
