using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// 불나방 풀을 빌드에서 재는 도구. 평소에는 아무 일도 하지 않는다.
// 환경 변수 DONDON_MOTH_BENCH에 결과 파일 경로를 주고 게임을 띄우면: 새 게임을 스스로 시작하고, 장이 열리면 종목 둘에 불나방을 차례로 부르고(주입),
// 가격·거래대금·프레임 시간·메모리를 5초마다 그 파일에 적은 뒤 게임을 끈다. 들어가고 나가는 것은 풀이 평소대로 한다.
// 저장 폴더를 따로 쓰려면 DONDON_SETTINGS_OUTPUT도 함께 준다(검증 모드: 타이틀 화면 없이 바로 시작한다).
public sealed class MothBench : MonoBehaviour
{
    private string path;
    private readonly List<float> frames = new(8192);
    private float frameSum, frameMax; private int frameCount;
    private decimal turnover, peak; private int trades;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string output = Environment.GetEnvironmentVariable("DONDON_MOTH_BENCH");
        if (string.IsNullOrEmpty(output)) { return; }
        var holder = new GameObject("System :: Moth Bench"); DontDestroyOnLoad(holder);
        holder.AddComponent<MothBench>().path = output;
    }

    void Start() { Application.runInBackground = true; File.WriteAllText(path, ""); StartCoroutine(Run()); }
    void Update()
    {
        if (Application.targetFrameRate != -1) { Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0; }
        float dt = Time.unscaledDeltaTime; frames.Add(dt); frameSum += dt; frameCount++; if (dt > frameMax) { frameMax = dt; }
    }
    private void Line(string text) { File.AppendAllText(path, text + "\n"); }
    private string FrameStats()
    {
        frames.Sort();
        float median = frames.Count == 0 ? 0f : frames[frames.Count / 2], p95 = frames.Count == 0 ? 0f : frames[(int)(frames.Count * .95f)], p99 = frames.Count == 0 ? 0f : frames[(int)(frames.Count * .99f)];
        string text = "frames " + frameCount + " avg " + (frameCount == 0 ? 0f : frameSum / frameCount * 1000f).ToString("0.0", Culture) + "ms median " + (median * 1000f).ToString("0.0", Culture) + "ms p95 " + (p95 * 1000f).ToString("0.0", Culture) + "ms p99 " + (p99 * 1000f).ToString("0.0", Culture) + "ms max " + (frameMax * 1000f).ToString("0.0", Culture) + "ms";
        frames.Clear(); frameSum = 0f; frameMax = 0f; frameCount = 0;
        return text;
    }
    private void Trade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting) { turnover += price * volume; trades++; if (price > peak) { peak = price; } }

    private IEnumerator Run()
    {
        while (StockManager.Instance == null) { yield return null; }
        // 새 게임: 장면의 시작 창에서 확인을 누른다.
        float waited = 0f;
        while (!StockManager.Instance.SetupConfirmed && waited < 120f)
        {
            var setup = FindAnyObjectByType<PlayerCompanySetupUI>();
            if (setup != null && setup.confirm != null && setup.confirm.interactable && !StockManager.Instance.SetupInProgress) { setup.confirm.onClick.Invoke(); }
            yield return new WaitForSecondsRealtime(1f); waited += 1f;
        }
        while (MarketDayController.Instance == null || !MarketDayController.CanTrade || MarketDayController.Instance.Phase != MarketPhase.Trading) { yield return new WaitForSecondsRealtime(.5f); }
        Line("BENCH " + Application.version + " " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName + " | " + Screen.width + "x" + Screen.height + " | accounts " + Trader.Registry.Count + " | pool " + MothPool.Size + " idle " + (MothPool.Instance == null ? -1 : MothPool.Instance.IdleCount));
        if (DeskViewController.Instance != null) { DeskViewController.Instance.Focus(DeskViewTarget.Chart); }
        // 장이 자리를 잡을 때까지 잠깐 둔다.
        yield return new WaitForSecondsRealtime(40f);
        string[] symbols = { "GFD", "KMF" }; int[] call = { 150, MothPool.Size };
        for (int round = 0; round < symbols.Length; round++)
        {
            Stock stock = null;
            foreach (var candidate in StockManager.Instance.stocks) { if (candidate.symbol == symbols[round]) { stock = candidate; } }
            stock ??= StockManager.Instance.stocks[round];
            StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock));
            var five = GameObject.Find("Button :: Chart 5m"); if (five != null && five.GetComponent<Button>() != null) { five.GetComponent<Button>().onClick.Invoke(); }
            // 프레임 제한을 풀어 실제 프레임 시간을 본다.
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            stock.OnMatchedTrade += Trade; turnover = 0m; trades = 0; peak = stock.currentPrice;
            decimal began = stock.currentPrice, broughtAt = MothPool.BroughtTotal, takenAt = MothPool.TakenTotal, lastTurnover = 0m; int lastTrades = 0;
            Line("ROUND " + (round + 1) + " stock " + stock.symbol + " " + stock.stockName + " price " + stock.currentPrice.ToString("N0", Culture) + " float value " + StockManager.Instance.FloatValue(stock).ToString("N0", Culture) + " | natural rallies so far " + MothPool.RalliesTotal + " | clock " + MarketDayController.Instance.ClockMinute / 60 + ":" + (MarketDayController.Instance.ClockMinute % 60).ToString("00"));
            FrameStats();
            // 30초(5초씩 여섯 번)는 평소 수준, 그 뒤 170초는 부른 뒤.
            for (int tick = 0; tick < 40; tick++)
            {
                if (tick == 6)
                {
                    began = stock.currentPrice; peak = began;
                    Line("ROUND " + (round + 1) + " INJECTED call " + call[round] + " moths: " + (MothPool.Instance != null && MothPool.Instance.Force(stock, call[round])));
                }
                float from = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(5f);
                float span = Mathf.Max(.01f, Time.realtimeSinceStartup - from);
                var pool = MothPool.Instance;
                Line("R" + (round + 1) + " t" + (tick * 5 + 5) + "s " + (tick < 6 ? "baseline" : "called") + " price " + stock.currentPrice.ToString("N0", Culture) + " (" + ((stock.currentPrice / began - 1m) * 100m).ToString("+0.0;-0.0", Culture) + "%) peak " + ((peak / began - 1m) * 100m).ToString("+0.0;-0.0", Culture) + "% | turnover/s " + ((turnover - lastTurnover) / (decimal)span).ToString("N0", Culture) + " trades/s " + ((trades - lastTrades) / span).ToString("0.0", Culture) + " | moths here " + (pool == null ? 0 : pool.CountOn(stock)) + " holding " + (pool == null ? 0 : pool.HoldingOn(stock)) + " busy anywhere " + (pool == null ? 0 : pool.BusyCount) + " brought " + (MothPool.BroughtTotal - broughtAt).ToString("N0", Culture) + " taken " + (MothPool.TakenTotal - takenAt).ToString("N0", Culture) + " | " + FrameStats() + " | memory " + (GC.GetTotalMemory(false) / 1048576L) + "MB managed " + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576L) + "MB native");
                lastTurnover = turnover; lastTrades = trades;
            }
            int stuck = 0; foreach (var account in FindObjectsByType<MothAccount>(FindObjectsInactive.Include)) { if (account.HoldsAnything) { stuck++; } }
            bool sound = StockIntegrityChecker.Instance == null || StockIntegrityChecker.Instance.CheckStock(stock).isValid;
            Line("ROUND " + (round + 1) + " RESULT peak " + ((peak / began - 1m) * 100m).ToString("+0.0;-0.0", Culture) + "% end " + ((stock.currentPrice / began - 1m) * 100m).ToString("+0.0;-0.0", Culture) + "% | brought " + (MothPool.BroughtTotal - broughtAt).ToString("N0", Culture) + " taken " + (MothPool.TakenTotal - takenAt).ToString("N0", Culture) + " left in the market " + (MothPool.BroughtTotal - broughtAt - (MothPool.TakenTotal - takenAt)).ToString("N0", Culture) + " | still here " + (MothPool.Instance == null ? 0 : MothPool.Instance.CountOn(stock)) + " | accounts still holding any stock " + stuck + " | idle " + (MothPool.Instance == null ? 0 : MothPool.Instance.IdleCount) + " | natural rallies " + MothPool.RalliesTotal + " | share check " + sound);
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(path) ?? ".", "round" + (round + 1) + "-" + stock.symbol + ".png"));
            yield return new WaitForSecondsRealtime(1.5f);
            stock.OnMatchedTrade -= Trade;
        }
        Line("DONE");
        Application.Quit();
    }
}
