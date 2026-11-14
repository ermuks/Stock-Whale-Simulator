using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 종목 리스트 한 행. 보조 정보는 전달된 최근 캔들(현재 최대 18개) 합계 기준.
/// 가격=현재가, 등락률=전일 종가 대비. 거래량/거래대금과 미니 캔들은 전달된 최근 봉 기준.
/// </summary>
public class StockListItem : MonoBehaviour
{
    [Header("Icon & Texts")]
    public Image icon;
    public TextMeshProUGUI textSymbol;
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textPrice;
    public TextMeshProUGUI textVolume10;
    public TextMeshProUGUI textTurnover;
    public TextMeshProUGUI textChangePct10;
    [Header("미니 캔들 (Anchor 0~1)")]
    [Tooltip("캔들 몸통. Anchor Y = 시가~종가 구간")]
    public Image imgBong;
    [Tooltip("캔들 심지( high-low ). Anchor Y = 0~1 전체")]
    public Image imgLine;
    [Header("캔들/라인 좌우 비율 (0~1)")]
    [Tooltip("심지 가운데 영역. 예: 0.45~0.55")]
    public float lineAnchorMinX = 0.45f;
    public float lineAnchorMaxX = 0.55f;
    [Tooltip("몸통 좌우. 예: 0.2~0.8")]
    public float bongAnchorMinX = 0.2f;
    public float bongAnchorMaxX = 0.8f;
    [Header("Trading Terminal Style")]
    public Image rowBackground;
    public Sprite normalBackground, selectedBackground;
    public TextMeshProUGUI textRisk;
    public Image selectionHighlight;
    public Image riskBadgeBackground;
    private bool selected, selectionInitialized;
    private string iconSymbol;
    private static readonly Color Up = new Color32(239, 89, 100, 255);
    private static readonly Color Down = new Color32(76, 150, 232, 255);
    const string IconPath = "Stock/";
    /// <summary>이 행이 표시하는 종목. Set() 시 저장, 버튼 클릭 시 이 종목으로 오더북·차트 전환.</summary>
    private Stock _stock;
    public Stock BoundStock => _stock;
    public float VolumeSortValue { get; private set; }
    public float ChangeSortValue { get; private set; }
    private RectTransform rowRect;
    private bool badgeLayoutDirty = true;
    private float badgeLayoutWidth = -1f;
    void Awake()
    {
        rowRect = (RectTransform)transform;
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnSelectStock);
        }
    }

    /// <summary>버튼 클릭 시 선택 종목으로 오더북·차트 초기화 후 해당 종목 데이터 표시.</summary>
    void OnSelectStock()
    {
        if (_stock == null || StockManager.Instance == null)
        { return; }

        var stocks = StockManager.Instance.stocks;
        int index = stocks.IndexOf(_stock);
        if (index < 0)
        { return; }

        StockManager.Instance.SelectStock(index);

    }

    public void Set(Stock stock, IReadOnlyList<CandleData> last10)
    {
        if (stock == null)
        { return; }

        _stock = stock;
        if (icon != null && iconSymbol != stock.symbol)
        {
            iconSymbol = stock.symbol;
            var sprite = Resources.Load<Sprite>(IconPath + stock.symbol);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (textSymbol != null)
        {
            DeviceTerminalUI.Set(textSymbol,stock.symbol);
        }

        if (textName != null)
        {
            string name = textRisk != null ? stock.stockName : stock.stockName + " · " + stock.RiskDisplay;
            if (textName.text != name)
            {
                textName.text = name;
                badgeLayoutDirty = true;
            }
        }

        if (textPrice != null)
        {
            DeviceTerminalUI.Set(textPrice,PriceTickPolicy.FormatPrice(stock.currentPrice));
        }

        ApplyRiskBadge(stock);
        LayoutBadge();
        bool nextSelected = StockManager.Instance != null && StockManager.Instance.CurrentStock == stock;
        if (!selectionInitialized || selected != nextSelected)
        {
            selected = nextSelected;
            selectionInitialized = true;
            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = selected;
            }
            else if (rowBackground != null)
            {
                rowBackground.sprite = selected ? selectedBackground : normalBackground;
            }
        }

        // 거래량/거래대금: 최근 18캔들 전체 합
        // 미니 캔들만 최근 6개 묶음. 등락률/등락률 정렬은 고정된 전일 종가 기준.
        float volumeSum = 0f;
        float turnoverSum = 0f;
        decimal dailyChange = 0m;
        bool hasDailyChange = StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyChange(stock, out dailyChange);
        float changePct = (float)dailyChange;
        CandleData? summary = null;
        int nAll = last10 != null ? last10.Count : 0;
        const int groupSize = 6;
        if (nAll > 0)
        {
            for (int i = 0; i < nAll; i++)
            {
                volumeSum += last10[i].volume;
                turnoverSum += last10[i].close * last10[i].volume;
            }

            // 현재 구간: 마지막 캔들 인덱스가 속한 6개 묶음 (0~5 → 0, 6~11 → 1, 12~17 → 2)
            int lastIndex = nAll - 1;
            int groupIndex = lastIndex / groupSize;
            int from = groupIndex * groupSize;
            int to = Mathf.Min(from + groupSize, nAll);
            summary = AggregateCandleRange(last10, from, to);
        }

        if (textVolume10 != null)
        {
            DeviceTerminalUI.Set(textVolume10,FormatVolume(volumeSum) + "주");
        }

        if (textTurnover != null)
        {
            DeviceTerminalUI.Set(textTurnover,"거래대금(추정) " + FormatTurnover(turnoverSum) + "원");
        }

        if (textChangePct10 != null)
        {
            DeviceTerminalUI.Set(textChangePct10,hasDailyChange ? (changePct > 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%") : "—");
            textChangePct10.color = changePct > 0 ? Up : changePct < 0 ? Down : new Color32(138,150,163,255);
        }
        VolumeSortValue = volumeSum; ChangeSortValue = changePct;
        ApplyCandleAnchors(summary);
    }

    private void LayoutBadge()
    {
        if (textName == null || textRisk == null || riskBadgeBackground == null || rowRect == null)
        { return; }
        float width = rowRect.rect.width;
        if (!badgeLayoutDirty && Mathf.Approximately(width,badgeLayoutWidth))
        { return; }
        badgeLayoutDirty = false; badgeLayoutWidth = width;
        float badgeWidth = Mathf.Clamp(textRisk.GetPreferredValues(textRisk.text).x + 28f,160f,248f);
        float nameWidth = Mathf.Clamp(width - 40f - 570f - 32f - badgeWidth - 16f - textName.rectTransform.anchoredPosition.x,120f,650f);
        textName.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,nameWidth);
        float x = textName.rectTransform.anchoredPosition.x + Mathf.Min(nameWidth,textName.GetPreferredValues(textName.text).x) + 16f;
        var position = riskBadgeBackground.rectTransform.anchoredPosition; position.x = x;
        riskBadgeBackground.rectTransform.anchoredPosition = position; textRisk.rectTransform.anchoredPosition = position;
        riskBadgeBackground.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
        textRisk.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
    }

    /// <summary>구간 캔들을 하나의 캔들로 합침. open=첫 시가, close=마지막 종가, high/low=구간 최고/최저.</summary>
    static CandleData AggregateCandleRange(IReadOnlyList<CandleData> candles, int fromInclusive, int toExclusive)
    {
        if (candles == null)
        { return default; }

        int n = candles.Count;
        int from = Mathf.Clamp(fromInclusive, 0, n);
        int to = Mathf.Clamp(toExclusive, 0, n);
        if (to <= from)
        { return default; }

        float open = candles[from].open;
        float close = candles[to - 1].close;
        float high = candles[from].high;
        float low = candles[from].low;
        float vol = 0f;
        for (int i = from; i < to; i++)
        {
            if (candles[i].high > high)
            {
                high = candles[i].high;
            }

            if (candles[i].low < low)
            {
                low = candles[i].low;
            }

            vol += candles[i].volume;
        }

        return new CandleData
        {
            open = open,
            close = close,
            high = high,
            low = low,
            volume = vol
        };
    }

    /// <summary>거래량 표기: 0~9,999 → #,##0 / 10,000~999,999 → k / 1,000,000~ → M</summary>
    static string FormatVolume(float volumeSum)
    {
        if (volumeSum <= 9999f)
        { return ((int)volumeSum).ToString("#,##0"); }

        if (volumeSum <= 999999f)
        { return (volumeSum / 1000f).ToString("0.#") + "k"; }

        return (volumeSum / 1000000f).ToString("0.#") + "M";
    }

    /// <summary>거래대금 표기: 0~9,999 → #,##0 / 10,000~999,999 → #,##0.0k / 1,000,000~999,999,999 → #,##0.00M / 1,000,000,000~ → #,##0.00B</summary>
    static string FormatTurnover(float turnover)
    {
        if (turnover < 10000f)
        { return ((long)turnover).ToString("#,##0"); }

        if (turnover < 1000000f)
        { return (turnover / 1000f).ToString("#,##0.0") + "k"; }

        if (turnover < 1000000000f)
        { return (turnover / 1000000f).ToString("#,##0.00") + "M"; }

        return (turnover / 1000000000f).ToString("#,##0.00") + "B";
    }

    void ApplyRiskBadge(Stock stock)
    {
        Color color = stock.RiskPending ? new Color32(157, 166, 175, 255) : stock.RiskGrade switch
        {
            StockRiskGrade.Safe => new Color32(133, 182, 151, 255),
            StockRiskGrade.Warning => new Color32(218, 195, 111, 255),
            StockRiskGrade.Danger => new Color32(225, 160, 104, 255),
            StockRiskGrade.HighRisk => new Color32(228, 127, 135, 255),
            _ => new Color32(157, 166, 175, 255)
        };
        if (textRisk != null)
        {
            string label = stock.RiskLabel;
            if (stock.IsTradingHalted)
            {
                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - Time.time)) + "초";
            }
            if (textRisk.text != label)
            {
                textRisk.text = label;
                badgeLayoutDirty = true;
            }
            if (textRisk.color != color)
            {
                textRisk.color = color;
            }
        }
        if (riskBadgeBackground != null)
        {
            color.a = .12f;
            if (riskBadgeBackground.color != color)
            {
                riskBadgeBackground.color = color;
            }
        }
    }

    void ApplyCandleAnchors(CandleData? summary)
    {
        if (imgLine != null)
        {
            imgLine.enabled = summary.HasValue;
        }
        if (imgBong != null)
        {
            imgBong.enabled = summary.HasValue;
        }
        if (!summary.HasValue)
        { return; }
        CandleData candle = summary.Value;
        float low = Mathf.Min(candle.low, candle.open, candle.close);
        float high = Mathf.Max(candle.high, candle.open, candle.close);
        float range = Mathf.Max(high - low, 1f);
        float middle = (low + high) * .5f;
        low = middle - range * .5f - range * .12f;
        high = middle + range * .5f + range * .12f;
        float Map(float price) => Mathf.Clamp01((price - low) / (high - low));
        Color color = candle.close >= candle.open ? Up : Down;
        if (imgLine != null)
        {
            var rect = imgLine.rectTransform;
            rect.anchorMin = new Vector2(lineAnchorMinX, Map(candle.low));
            rect.anchorMax = new Vector2(lineAnchorMaxX, Map(candle.high));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            imgLine.color = color;
        }
        if (imgBong != null)
        {
            var rect = imgBong.rectTransform;
            float bottom = Mathf.Min(Map(candle.open), Map(candle.close));
            float top = Mathf.Max(Map(candle.open), Map(candle.close));
            if (top - bottom < .025f)
            {
                float center = (top + bottom) * .5f;
                bottom = center - .0125f;
                top = center + .0125f;
            }
            rect.anchorMin = new Vector2(bongAnchorMinX, bottom);
            rect.anchorMax = new Vector2(bongAnchorMaxX, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            imgBong.color = color;
        }
    }
}
