using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class ChartNavigationValidation
{
    private static string output;
    private static double began, next;
    private static int phase, checks;
    private static bool finished;
    private static bool oldEnabled;
    private static EnterPlayModeOptions oldOptions;
    private static ChartManager chart;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output))
        { throw new Exception("Run with guarded Tests/Settings/Run.ps1"); }
        var tape = new List<CandleData>(); var grouped = new List<CandleData>();
        ChartHistorySeries.RecordTrade(tape, 1, 540, 100, 3, true, 8192);
        ChartHistorySeries.RecordTrade(tape, 1, 540, 110, 2, false, 8192);
        ChartHistorySeries.RecordTrade(tape, 1, 544, 90, 4, true, 8192);
        ChartHistorySeries.RecordTrade(tape, 1, 545, 105, 1, true, 8192);
        ChartHistorySeries.Build(tape, default, 5, grouped, false);
        Check(grouped.Count == 2 && grouped[0].open == 100 && grouped[0].close == 90 && grouped[0].high == 110 && grouped[0].low == 90 && grouped[0].volume == 9 && grouped[0].nonExchangeVolume == 7, "5m OHLCV and exchange volume");
        ChartHistorySeries.RecordTrade(tape, 2, 540, 95, 1, true, 8192);
        ChartHistorySeries.Build(tape, default, 1440, grouped, false);
        Check(grouped.Count == 2 && grouped[0].volume == 10, "daily buckets stay separate");
        for (int i = 0; i < 9000; i++)
        { ChartHistorySeries.RecordTrade(tape, 3 + i / 480, 540 + i % 480, 100, 1, true, 8192); }
        Check(tape.Count == 8192, "bounded archive");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldEnabled = EditorSettings.enterPlayModeOptionsEnabled; oldOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; EditorApplication.update += Step; EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string name)
    {
        File.AppendAllText(Path.Combine(output, "checks.txt"), (condition ? "PASS " : "FAIL ") + name + "\n");
        if (!condition)
        { throw new Exception(name); }
        checks++;
    }
    private static void Step()
    {
        if (finished)
        { return; }
        try
        {
            if (EditorApplication.timeSinceStartup - began > 110)
            { throw new Exception("Runtime timeout"); }
            if (!EditorApplication.isPlaying || !GameLocale.Ready || EditorApplication.timeSinceStartup < next)
            { return; }
            Application.targetFrameRate = 20;
            if (phase == 0)
            {
                var setup = UnityEngine.Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName == null)
                { return; }
                setup.businessName.SetTextWithoutNotify("차트 검증"); setup.Refresh(); setup.confirm.onClick.Invoke(); phase++; return;
            }
            if (phase == 1)
            {
                if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
                { return; }
                chart = UnityEngine.Object.FindAnyObjectByType<ChartManager>();
                chart.SetChartPeriod(15);
                Check((int)typeof(ChartManager).GetField("periodMinutes", Fields).GetValue(chart) == 0, "unfocused period input blocked");
                DeskViewController.Instance.Focus(DeskViewTarget.Chart); phase++; next = EditorApplication.timeSinceStartup + 2; return;
            }
            if (phase == 2)
            {
                Check(!DeskViewController.BlockChartPointer, "chart focus ready");
                var stock = StockManager.Instance.CurrentStock;
                var dictionary = (Dictionary<Stock, List<CandleData>>)typeof(CandleHistoryManager).GetField("minuteHistory", Fields).GetValue(CandleHistoryManager.Instance);
                var tape = new List<CandleData>(); dictionary[stock] = tape;
                for (int i = 0; i < 150; i++)
                { ChartHistorySeries.RecordTrade(tape, 1, 540 + i, 100 + i % 20, 10, true, 8192); }
                chart.SetChartPeriod(1);
                Check(chart.candleRenderer.candles.Count == 56, "visible capacity");
                typeof(ChartManager).GetField("followLatest", Fields).SetValue(chart, false);
                typeof(ChartManager).GetField("startIndex", Fields).SetValue(chart, 10);
                typeof(ChartManager).GetMethod("CaptureHistoryAnchor", Fields).Invoke(chart, null);
                ChartHistorySeries.RecordTrade(tape, 1, 690, 125, 5, true, 8192);
                typeof(ChartManager).GetMethod("RefreshChartFromHistory", Fields).Invoke(chart, null);
                Check((int)typeof(ChartManager).GetField("startIndex", Fields).GetValue(chart) == 10, "new candle preserves history viewport");
                tape.RemoveAt(0);
                typeof(ChartManager).GetMethod("RefreshChartFromHistory", Fields).Invoke(chart, null);
                Check((int)typeof(ChartManager).GetField("startIndex", Fields).GetValue(chart) == 9, "expired archive preserves timestamp anchor");
                var button = (Button)typeof(ChartManager).GetField("latestButton", Fields).GetValue(chart);
                button.onClick.Invoke();
                Check((bool)typeof(ChartManager).GetField("followLatest", Fields).GetValue(chart), "arrow restores latest follow");
                Canvas.ForceUpdateCanvases();
                var periods = (Button[])typeof(ChartManager).GetField("periodButtons", Fields).GetValue(chart);
                var rect = (RectTransform)periods[2].transform;
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(Camera.main, rect.TransformPoint(rect.rect.center)) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == periods[2], "world-space period button receives pointer hit");
                ExecuteEvents.Execute(periods[2].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Check(chart.candleRenderer.candles.Count == 31, "5m button binds trade history");
                phase++; next = EditorApplication.timeSinceStartup + 1; return;
            }
            Capture(1600, 900); Capture(1920, 1080); Finish(null);
        }
        catch (Exception error)
        { Finish(error.ToString()); }
    }
    private static void Capture(int width, int height)
    {
        var camera = Camera.main; var prior = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(width, height, 24); var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, "chart-" + width + "x" + height + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = prior; RenderTexture.active = active; target.Release();
            UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(target);
        }
    }
    private static void Finish(string error)
    {
        finished = true; EditorApplication.update -= Step;
        EditorSettings.enterPlayModeOptionsEnabled = oldEnabled; EditorSettings.enterPlayModeOptions = oldOptions;
        File.WriteAllText(Path.Combine(output, "result.txt"), "Checks: " + checks + "\n" + (error ?? "PASS"));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
