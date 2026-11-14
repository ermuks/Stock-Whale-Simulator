using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>가격 축과 커서 정보. 기존 차트 Canvas/폰트/3D 좌표를 그대로 사용한다.</summary>
public sealed class ChartGuideOverlay : MonoBehaviour
{
    private static readonly Color Muted = new Color32(138, 150, 163, 255);
    private static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;
    private const float FontSize = 24f, LabelHeight = 32f, TimeBand = 38f;
    private CandleDrawManager chart;
    private RectTransform root, hoverRoot, priceBadge, timeBadge;
    private ChartGuideGraphic grid, crosshair;
    private readonly TextMeshProUGUI[] ticks = new TextMeshProUGUI[13];
    private TextMeshProUGUI priceText, timeText;
    private float unitsX, unitsY, plotWidth, plotHeight, volumeHeight, axisWidth;
    private double lastMinimum = double.NaN, lastMaximum, lastStep;
    private double lastCursorPrice = double.NaN;
    private int lastDay = -1, lastMinute = -1;
    private bool lastForming, lastHistorical;
    private bool visible;
    private const int MaxTradeLines = 128, MaxTradeLabels = 16;
    private const float TradeLabelHeight = 30f;
    private readonly ChartTradeLevels tradeLevels = new();
    private readonly List<ChartTradeLevels.Level> previousLevels = new(16), visibleLevels = new(16), labelledLevels = new(16);
    private readonly List<ChartGuideGraphic.LevelLine> tradeLines = new(16);
    private readonly List<RectTransform> tradeBadges = new(8);
    private readonly List<TextMeshProUGUI> tradeTexts = new(8);
    private readonly float[] labelPositions = new float[MaxTradeLabels];
    private RectTransform tradeRoot;
    private ChartGuideGraphic tradeGraphic;
    private TMP_Text tradeFont;
    private Stock tradeStock;
    private float nextTradeRead;

    public static ChartGuideOverlay Create(CandleDrawManager chart, TMP_Text fontSource, int capacity)
    {
        var canvas = chart.GetComponentInParent<Canvas>();
        if (canvas == null || fontSource == null)
        { return null; }
        var area = new GameObject("Area :: Chart Guides", typeof(RectTransform));
        area.layer = canvas.gameObject.layer;
        area.transform.SetParent(chart.transform, false);
        var overlay = area.AddComponent<ChartGuideOverlay>();
        overlay.Initialize(chart, canvas, fontSource, capacity);
        return overlay;
    }

    private void Initialize(CandleDrawManager source, Canvas canvas, TMP_Text fontSource, int capacity)
    {
        chart = source; root = (RectTransform)transform;
        // 캔들 메시의 비균등 배율(현재 50×40)을 상쇄해 글꼴/점선이 찌그러지지 않게 한다.
        unitsX = Mathf.Max(.001f, canvas.transform.InverseTransformVector(chart.transform.TransformVector(Vector3.right)).magnitude);
        unitsY = Mathf.Max(.001f, canvas.transform.InverseTransformVector(chart.transform.TransformVector(Vector3.up)).magnitude);
        root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
        root.localScale = new Vector3(1f / unitsX, 1f / unitsY, 1f);
        root.localPosition = new Vector3(0f, 0f, -.2f);
        axisWidth = 124f;
        chart.ConfigurePlot(capacity, axisWidth / unitsX, TimeBand / unitsY, Mathf.Clamp(Mathf.FloorToInt(chart.chartHeight * unitsY / 56f), 2, 12));
        grid = Graphic("Image :: Price Grid", root);
        grid.color = new Color32(138, 150, 163, 100);
        for (int i = 0; i < ticks.Length; i++)
        { ticks[i] = Label("Text :: Price Tick " + i, root, fontSource, Muted); }
        tradeRoot = Rect("Area :: My Prices", root);
        tradeGraphic = Graphic("Image :: My Price Lines", tradeRoot);
        tradeFont = fontSource;
        hoverRoot = Rect("Area :: Hover Readout", root);
        crosshair = Graphic("Image :: Dashed Crosshair", hoverRoot);
        crosshair.color = new Color32(196, 210, 220, 200);
        priceBadge = Badge("Panel :: Cursor Price", hoverRoot);
        timeBadge = Badge("Panel :: Candle Time", hoverRoot);
        priceText = Label("Text :: Cursor Price", priceBadge, fontSource, new Color32(230, 235, 239, 255));
        timeText = Label("Text :: Candle Time", timeBadge, fontSource, new Color32(230, 235, 239, 255));
        priceText.alignment = timeText.alignment = TextAlignmentOptions.Midline;
        hoverRoot.gameObject.SetActive(false);
    }

    public void Refresh(Stock stock)
    {
        bool hasData = chart.candles != null && chart.candles.Count > 0;
        if (gameObject.activeSelf != hasData)
        { gameObject.SetActive(hasData); }
        if (!hasData)
        { HideCursor(); return; }
        var scale = chart.PriceScale;
        bool changed = scale.Minimum != lastMinimum || scale.Maximum != lastMaximum || scale.Step != lastStep;
        if (changed)
        {
            // 큰 금액도 자동 축소하지 않고, 축의 폭을 필요한 만큼만 확보한다.
            string largest = scale.Maximum.ToString("N0", Numbers);
            float required = Mathf.Max(124f, ticks[0].GetPreferredValues(largest + "원").x + 22f);
            required = Mathf.Min(chart.ChartWidth * unitsX * .28f, required);
            if (Mathf.Abs(required - axisWidth) > .5f)
            {
                axisWidth = required;
                chart.ConfigurePlot(chart.PlotCapacity, axisWidth / unitsX, TimeBand / unitsY, chart.MaximumGridIntervals);
                chart.GenerateMesh();
            }
        }
        float width = chart.PlotWidth * unitsX, height = chart.chartHeight * unitsY;
        bool geometryChanged = width != plotWidth || height != plotHeight || volumeHeight != chart.VolumePlotHeight * unitsY;
        if (!changed && !geometryChanged)
        { RefreshTrades(stock, false); return; }
        plotWidth = width; plotHeight = height; volumeHeight = chart.VolumePlotHeight * unitsY;
        root.sizeDelta = new Vector2(chart.ChartWidth * unitsX, plotHeight);
        grid.rectTransform.sizeDelta = root.sizeDelta;
        hoverRoot.sizeDelta = crosshair.rectTransform.sizeDelta = root.sizeDelta;
        grid.SetGrid(plotWidth, plotHeight, scale.Intervals);
        for (int i = 0; i < ticks.Length; i++)
        {
            bool active = i <= scale.Intervals;
            if (ticks[i].gameObject.activeSelf != active)
            { ticks[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            ticks[i].text = scale.Tick(i).ToString("N0", Numbers);
            float y = plotHeight * i / scale.Intervals;
            Place(ticks[i].rectTransform, plotWidth + 8f, Mathf.Clamp(y - LabelHeight * .5f, 0f, plotHeight - LabelHeight), axisWidth - 14f, LabelHeight);
        }
        lastMinimum = scale.Minimum; lastMaximum = scale.Maximum; lastStep = scale.Step;
        RefreshTrades(stock, true);
    }

    private void RefreshTrades(Stock stock, bool geometryChanged)
    {
        bool stockChanged = stock != tradeStock;
        if (!stockChanged && !geometryChanged && Time.unscaledTime < nextTradeRead)
        { return; }
        bool dataChanged = stockChanged;
        if (stockChanged || Time.unscaledTime >= nextTradeRead)
        {
            tradeStock = stock; nextTradeRead = Time.unscaledTime + .25f;
            var accounts = PlayerAccounts.Instance;
            Trader primary = accounts != null ? accounts.primary : StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
            tradeLevels.Read(stock, primary, accounts != null ? accounts.automated : null);
            dataChanged |= previousLevels.Count != tradeLevels.Levels.Count;
            for (int i = 0; !dataChanged && i < previousLevels.Count; i++)
            { dataChanged = !SameLevel(previousLevels[i], tradeLevels.Levels[i]); }
            if (dataChanged)
            {
                previousLevels.Clear();
                foreach (var level in tradeLevels.Levels)
                { previousLevels.Add(level); }
            }
        }
        if (!dataChanged && !geometryChanged)
        { return; }

        visibleLevels.Clear(); labelledLevels.Clear(); tradeLines.Clear();
        foreach (var level in previousLevels)
        {
            // 화면 밖 가격을 축 끝으로 clamp하면 실제 주문 가격과 다른 선이 생긴다.
            if (ChartTradeLevels.InRange(level, chart.PriceScale))
            { visibleLevels.Add(level); }
        }
        visibleLevels.Sort(ComparePriority);
        if (visibleLevels.Count > MaxTradeLines)
        { visibleLevels.RemoveRange(MaxTradeLines, visibleLevels.Count - MaxTradeLines); }
        int count = Mathf.Min(visibleLevels.Count, Mathf.Min(MaxTradeLabels, Mathf.FloorToInt(plotHeight / TradeLabelHeight)));
        for (int i = 0; i < count; i++)
        { labelledLevels.Add(visibleLevels[i]); }
        labelledLevels.Sort(ChartTradeLevels.ComparePrice);
        for (int i = 0; i < count; i++)
        {
            float y = (float)chart.PriceScale.Normalize((double)labelledLevels[i].Price) * plotHeight;
            labelPositions[i] = Mathf.Max(TradeLabelHeight * .5f, y);
            if (i > 0)
            { labelPositions[i] = Mathf.Max(labelPositions[i], labelPositions[i - 1] + TradeLabelHeight); }
        }
        for (int i = count - 1; i >= 0; i--)
        { labelPositions[i] = Mathf.Min(labelPositions[i], i == count - 1 ? plotHeight - TradeLabelHeight * .5f : labelPositions[i + 1] - TradeLabelHeight); }

        EnsureTradeLabels(count);
        tradeRoot.sizeDelta = tradeGraphic.rectTransform.sizeDelta = new Vector2(plotWidth, plotHeight);
        for (int i = 0; i < tradeBadges.Count; i++)
        {
            bool active = i < count;
            if (tradeBadges[i].gameObject.activeSelf != active)
            { tradeBadges[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            var level = labelledLevels[i]; var label = tradeTexts[i];
            string kind = level.Type == ChartTradeLevels.Kind.Average ? "내 평단" : level.Type == ChartTradeLevels.Kind.Buy ? "매수대기" : "매도대기";
            SetText(label, $"{kind} · {level.Account}번  {level.Price.ToString("N0", Numbers)}");
            Color tint = TradeColor(level.Type);
            if (label.color != tint) { label.color = tint; }
            float width = Mathf.Min(plotWidth * .48f, label.GetPreferredValues(label.text).x + 16f);
            Place(tradeBadges[i], 4f, labelPositions[i] - TradeLabelHeight * .5f, width, TradeLabelHeight - 2f);
            Place(label.rectTransform, 6f, 0f, width - 12f, TradeLabelHeight - 2f);
        }
        foreach (var level in visibleLevels)
        {
            float y = (float)chart.PriceScale.Normalize((double)level.Price) * plotHeight;
            float labelY = y, labelWidth = 0f;
            for (int i = 0; i < count; i++)
            {
                if (!SameLevel(level, labelledLevels[i]))
                { continue; }
                labelY = labelPositions[i]; labelWidth = tradeBadges[i].sizeDelta.x;
                break;
            }
            tradeLines.Add(new ChartGuideGraphic.LevelLine(y, labelY, labelWidth, TradeColor(level.Type)));
        }
        tradeGraphic.SetLevels(plotWidth, plotHeight, tradeLines);
    }

    private int ComparePriority(ChartTradeLevels.Level a, ChartTradeLevels.Level b)
    {
        bool averageA = a.Type == ChartTradeLevels.Kind.Average, averageB = b.Type == ChartTradeLevels.Kind.Average;
        if (averageA != averageB)
        { return averageA ? -1 : 1; }
        decimal current = tradeStock != null ? tradeStock.currentPrice : 0m;
        int distance = Math.Abs(a.Price - current).CompareTo(Math.Abs(b.Price - current));
        return distance != 0 ? distance : ChartTradeLevels.ComparePrice(a, b);
    }
    private static bool SameLevel(ChartTradeLevels.Level a, ChartTradeLevels.Level b)
    { return a.Account == b.Account && a.Type == b.Type && a.Price == b.Price && a.Quantity == b.Quantity; }
    private static Color TradeColor(ChartTradeLevels.Kind kind)
    { return kind == ChartTradeLevels.Kind.Average ? new Color32(73, 195, 201, 255) : kind == ChartTradeLevels.Kind.Buy ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255); }
    private void EnsureTradeLabels(int count)
    {
        while (tradeBadges.Count < count)
        {
            var badge = Badge("Panel :: My Price " + tradeBadges.Count, tradeRoot);
            badge.GetComponent<Image>().color = new Color32(16, 23, 28, 240);
            var label = Label("Text :: My Price", badge, tradeFont, Muted);
            label.fontSize = 22f; label.alignment = TextAlignmentOptions.MidlineLeft;
            tradeBadges.Add(badge); tradeTexts.Add(label);
        }
    }

    public void ShowCursor(Vector2 localPoint, CandleData candle, bool forming)
    {
        if (!gameObject.activeSelf)
        { return; }
        if (!visible)
        { visible = true; hoverRoot.gameObject.SetActive(true); }
        Vector2 point = new Vector2(Mathf.Clamp(localPoint.x * unitsX, 1f, plotWidth - 1f), Mathf.Clamp(localPoint.y * unitsY, 1f, plotHeight - 1f));
        crosshair.SetCrosshair(plotWidth, plotHeight, volumeHeight, point);
        double price = Math.Max(1d, Math.Round(chart.PriceScale.PriceAt(localPoint.y / chart.chartHeight), MidpointRounding.AwayFromZero));
        if (price != lastCursorPrice)
        { lastCursorPrice = price; SetText(priceText, price.ToString("N0", Numbers) + "원"); }
        if (candle.marketDay != lastDay || candle.marketMinute != lastMinute || candle.historical != lastHistorical || forming != lastForming)
        {
            lastDay = candle.marketDay; lastMinute = candle.marketMinute; lastHistorical = candle.historical; lastForming = forming;
            SetText(timeText, FormatCandleTime(candle, forming));
        }
        Place(priceBadge, plotWidth + 3f, Mathf.Clamp(point.y - LabelHeight * .5f, 0f, plotHeight - LabelHeight), axisWidth - 3f, LabelHeight);
        Place(priceText.rectTransform, 4f, 0f, axisWidth - 11f, LabelHeight);
        float timeWidth = Mathf.Min(320f, plotWidth);
        Place(timeBadge, Mathf.Clamp(point.x - timeWidth * .5f, 0f, plotWidth - timeWidth), -chart.volumeChartHeight * unitsY, timeWidth, LabelHeight);
        Place(timeText.rectTransform, 4f, 0f, timeWidth - 8f, LabelHeight);
    }
    public void HideCursor()
    {
        if (!visible)
        { return; }
        visible = false; hoverRoot.gameObject.SetActive(false);
    }
    public static string FormatCandleTime(CandleData candle, bool forming)
    {
        if (candle.historical)
        { return "이전 시세"; }
        if (candle.marketDay <= 0)
        { return forming ? "진행 중" : "—"; }
        int minute = Math.Max(0, Math.Min(1439, candle.marketMinute));
        return $"{candle.marketDay}일 {minute / 60:00}:{minute % 60:00}" + (forming ? " · 진행 중" : "");
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target.text != value)
        { target.text = value; }
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        var position = new Vector2(x, y); var size = new Vector2(width, height);
        if (rect.anchoredPosition != position) { rect.anchoredPosition = position; }
        if (rect.sizeDelta != size) { rect.sizeDelta = size; }
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        // 앵커/피벗 변경 후 기본 위치가 남으면 선은 가격 눈금과 다른 곳에 그려진다.
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
        return rect;
    }
    private static ChartGuideGraphic Graphic(string name, Transform parent)
    {
        var graphic = Rect(name, parent).gameObject.AddComponent<ChartGuideGraphic>();
        graphic.raycastTarget = false; return graphic;
    }
    private static RectTransform Badge(string name, Transform parent)
    {
        var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color32(32, 62, 69, 255); image.raycastTarget = false;
        return rect;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, TMP_Text source, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = source.font; label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = FontSize; label.enableAutoSizing = false; label.color = color;
        label.text = "";
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Truncate;
        label.alignment = TextAlignmentOptions.MidlineRight; label.raycastTarget = false;
        return label;
    }
}
