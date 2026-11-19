using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 종목 리스트 한 행. 보조 정보는 전달된 최근 캔들(현재 최대 18개) 합계 기준.
/// 가격=현재가, 등락률=전일 종가 대비. 미니 캔들은 당일 시가/고가/저가/현재가 기준.
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
    private static Color Up => MarketColors.Up;
    private static Color Down => MarketColors.Down;
    const string IconPath = "Stock/";
    /// <summary>이 행이 표시하는 종목. Set() 시 저장, 버튼 클릭 시 이 종목으로 오더북·차트 전환.</summary>
    private Stock _stock;
    public Stock BoundStock => _stock;
    /// <summary>정렬에 쓰는 값: 게임 시간으로 최근 24시간의 거래대금.</summary>
    public float TurnoverSortValue { get; private set; }
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

    private bool arranged;
    /// <summary>
    /// 행의 오른쪽을 두 기둥으로 다시 놓는다(프리팹의 자리를 덮어쓴다).
    /// 맨 오른쪽: 위에 거래량과 종목 기호(123k SSE), 아래에 거래대금(₩만 붙인다).
    /// 그 왼쪽: 위에 최근 거래가, 아래에 조금 작게 전일 대비 등락률. 종목 기호를 따로 적던 줄은 없앤다.
    /// </summary>
    private void Arrange()
    {
        if (arranged) { return; }
        arranged = true;
        const float edge = 28f, side = 290f, gap = 30f, middle = 330f;
        Column(textVolume10, true, edge, side, 38f, new Color32(190, 200, 208, 255));
        Column(textTurnover, false, edge, side, 34f, new Color32(138, 150, 163, 255));
        Column(textPrice, true, edge + side + gap, middle, 56f, PriceInk);
        Column(textChangePct10, false, edge + side + gap, middle, 40f, new Color32(138, 150, 163, 255));
        // 이름과 꼬리표는 행의 세로 가운데에 둔다.
        if (textName != null) { Middle(textName.rectTransform); textName.alignment = TextAlignmentOptions.MidlineLeft; }
        if (riskBadgeBackground != null) { Middle(riskBadgeBackground.rectTransform); }
        if (textRisk != null) { Middle(textRisk.rectTransform); }
        if (textSymbol != null) { textSymbol.gameObject.SetActive(false); }
    }
    private static readonly Color32 PriceInk = new(236, 240, 243, 255);
    private static void Middle(RectTransform rect)
    {
        rect.anchorMin = new Vector2(rect.anchorMin.x, .5f); rect.anchorMax = new Vector2(rect.anchorMax.x, .5f); rect.pivot = new Vector2(rect.pivot.x, .5f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
    }
    private static void Column(TextMeshProUGUI label, bool top, float right, float width, float size, Color color)
    {
        if (label == null) { return; }
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(1f, top ? .5f : .08f); rect.anchorMax = new Vector2(1f, top ? .92f : .5f); rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(width, 0f); rect.anchoredPosition = new Vector2(-right, 0f);
        label.enableAutoSizing = false; label.fontSize = size; label.alignment = top ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.TopRight;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.color = color; label.raycastTarget = false;
    }

    public void Set(Stock stock, IReadOnlyList<CandleData> last10)
    {
        if (stock == null)
        { return; }

        Arrange();

        _stock = stock;
        if (icon != null && iconSymbol != stock.symbol)
        {
            iconSymbol = stock.symbol;
            var sprite = Resources.Load<Sprite>(IconPath + stock.symbol);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (textName != null)
        {
            string name = textRisk != null ? stock.stockName : stock.stockName + " · " + stock.RiskDisplay;
            if (!GameLocalizedText.Matches(textName,name))
            {
                GameLocalizedText.Set(textName,name);
                badgeLayoutDirty = true;
            }
        }

        if (textPrice != null)
        {
            DeviceTerminalUI.Set(textPrice,PriceTickPolicy.FormatPrice(stock.currentPrice));
        }

        if (!stock.IsListed) { ApplyWaiting(); return; }
        ApplyRiskBadge(stock);
        ApplyHalt(stock.IsTradingHalted);
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

        // 거래량/거래대금: 게임 시간으로 최근 24시간의 실제 체결 합
        // 미니 캔들은 당일 OHLC. 등락률/등락률 정렬은 고정된 전일 종가 기준.
        float volumeSum = 0f;
        float turnoverSum = 0f;
        decimal dailyChange = 0m;
        var statistics = StockStatistics.Instance;
        bool hasDailyChange = statistics != null && statistics.TryGetDailyChange(stock, out dailyChange);
        float changePct = (float)dailyChange;
        CandleData? summary = null;
        if (statistics != null && statistics.TryGetDailyCandle(stock, out var dailyCandle))
        {
            summary = dailyCandle;
        }
        if (statistics != null)
        {
            statistics.GetDayWindow(stock, out decimal dayVolume, out decimal dayTurnover);
            volumeSum = (float)dayVolume; turnoverSum = (float)dayTurnover;
        }

        if (textVolume10 != null)
        {
            string volumeLine = FormatVolume(volumeSum) + " " + stock.symbol;
            if (!GameLocalizedText.Matches(textVolume10, volumeLine)) { GameLocalizedText.Set(textVolume10, volumeLine, true); }
        }

        if (textTurnover != null)
        {
            string turnoverLine = "₩" + FormatTurnover(turnoverSum);
            if (!GameLocalizedText.Matches(textTurnover, turnoverLine)) { GameLocalizedText.Set(textTurnover, turnoverLine, true); }
        }

        if (textChangePct10 != null)
        {
            DeviceTerminalUI.Set(textChangePct10,hasDailyChange ? (changePct > 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%") : "—");
            textChangePct10.color = changePct > 0 ? Up : changePct < 0 ? Down : new Color32(138,150,163,255);
        }
        // 현재가도 전일 대비 방향의 색을 따른다(거래정지 중에는 정지 색 그대로).
        if (textPrice != null && !stock.IsTradingHalted) { textPrice.color = !hasDailyChange || changePct == 0f ? (Color)PriceInk : changePct > 0 ? Up : Down; }
        TurnoverSortValue = turnoverSum; ChangeSortValue = changePct;
        // 0% 기준은 등락률과 같은 전일 종가. 전일 종가가 없으면 당일 시가를 쓴다.
        float reference = hasDailyChange && changePct > -99f && summary.HasValue ? summary.Value.close / (1f + changePct / 100f) : summary.HasValue ? summary.Value.open : 0f;
        ApplyCandleAnchors(summary, reference);
    }

    /// <summary>아직 상장하지 않은(다음 개장에 상장하는) 종목의 행: 꼬리표에 '상장 대기 중', 거래는 내일 09:00부터.</summary>
    private void ApplyWaiting()
    {
        string code = GameLocale.Code;
        string badge = code == "ja" ? "上場待ち" : code == "en" ? "Listing soon" : "상장 대기 중";
        string note = code == "ja" ? "明日 09:00" : code == "en" ? "09:00 tomorrow" : "내일 09:00";
        Color color = new Color32(46, 230, 208, 255);
        if (textRisk != null)
        {
            if (!GameLocalizedText.Matches(textRisk, badge)) { GameLocalizedText.Set(textRisk, badge, true); badgeLayoutDirty = true; }
            textRisk.color = color;
        }
        if (riskBadgeBackground != null) { color.a = .12f; riskBadgeBackground.color = color; }
        ApplyHalt(false); LayoutBadge();
        selected = false; selectionInitialized = true;
        if (selectionHighlight != null) { selectionHighlight.enabled = false; }
        if (textVolume10 != null && !GameLocalizedText.Matches(textVolume10, _stock.symbol)) { GameLocalizedText.Set(textVolume10, _stock.symbol, true); }
        if (textTurnover != null) { GameLocalizedText.Set(textTurnover, "—", true); }
        if (textChangePct10 != null) { if (!GameLocalizedText.Matches(textChangePct10, note)) { GameLocalizedText.Set(textChangePct10, note, true); } textChangePct10.color = new Color32(46, 230, 208, 255); }
        TurnoverSortValue = 0f; ChangeSortValue = 0f;
        ApplyCandleAnchors(null, 0f);
    }

    private int badgeLayoutLocale = -1;
    private void LayoutBadge()
    {
        if (textName == null || textRisk == null || riskBadgeBackground == null || rowRect == null)
        { return; }
        float width = rowRect.rect.width;
        // 언어를 바꾸면 종목 이름의 길이가 달라진다. 그때도 꼬리표 자리를 다시 잡는다(영어 이름 위에 꼬리표가 겹쳤다).
        if (!badgeLayoutDirty && Mathf.Approximately(width,badgeLayoutWidth) && badgeLayoutLocale == GameLocale.Revision)
        { return; }
        badgeLayoutDirty = false; badgeLayoutWidth = width; badgeLayoutLocale = GameLocale.Revision;
        // 꼬리표는 글자 길이만큼만(짧게).
        float badgeWidth = Mathf.Clamp(textRisk.GetPreferredValues(textRisk.text).x + 26f,64f,248f);
        float nameWidth = Mathf.Clamp(width - 40f - 570f - 32f - badgeWidth - 16f - textName.rectTransform.anchoredPosition.x,120f,650f);
        textName.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,nameWidth);
        float x = textName.rectTransform.anchoredPosition.x + Mathf.Min(nameWidth,textName.GetPreferredValues(textName.text).x) + 16f;
        var position = riskBadgeBackground.rectTransform.anchoredPosition; position.x = x;
        riskBadgeBackground.rectTransform.anchoredPosition = position; textRisk.rectTransform.anchoredPosition = position;
        riskBadgeBackground.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
        textRisk.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
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

    // 거래정지: 행 전체를 붉게 덮고 왼쪽에 붉은 띠를 세운다. 이름·가격도 붉게 흐려서 주문할 수 없는 종목임이 한눈에 보이게 한다.
    private static readonly Color32 HaltInk = new(236, 96, 104, 255), HaltDim = new(196, 124, 128, 255);
    private Image haltTint, haltBar;
    private bool haltShown;
    private Color nameColor, priceColor, symbolColor;
    void ApplyHalt(bool halted)
    {
        if (halted == haltShown)
        { return; }
        haltShown = halted;
        if (haltTint == null)
        {
            haltTint = HaltImage("Image :: Halt Tint", new Color32(214, 54, 66, 46));
            haltTint.rectTransform.anchorMin = Vector2.zero; haltTint.rectTransform.anchorMax = Vector2.one;
            haltBar = HaltImage("Image :: Halt Bar", HaltInk);
            haltBar.rectTransform.anchorMin = Vector2.zero; haltBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            haltBar.rectTransform.pivot = new Vector2(0f, .5f); haltBar.rectTransform.sizeDelta = new Vector2(8f, 0f);
            // 바탕 그림 바로 위, 글자와 캔들 아래에 둔다.
            int index = rowBackground != null && rowBackground.transform.parent == transform ? rowBackground.transform.GetSiblingIndex() + 1 : 0;
            haltTint.transform.SetSiblingIndex(index); haltBar.transform.SetSiblingIndex(index + 1);
            if (textName != null) { nameColor = textName.color; }
            if (textPrice != null) { priceColor = textPrice.color; }
            if (textSymbol != null) { symbolColor = textSymbol.color; }
        }
        haltTint.enabled = haltBar.enabled = halted;
        if (textName != null) { textName.color = halted ? HaltInk : nameColor; }
        if (textPrice != null) { textPrice.color = halted ? HaltDim : priceColor; }
        if (textSymbol != null) { textSymbol.color = halted ? HaltDim : symbolColor; }
    }
    private Image HaltImage(string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(transform, false);
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = color; image.raycastTarget = false;
        return image;
    }

    void ApplyRiskBadge(Stock stock)
    {
        Color color = stock.RiskPending ? new Color32(157, 166, 175, 255) : stock.RiskGrade switch
        {
            StockRiskGrade.Safe => new Color32(133, 182, 151, 255),
            StockRiskGrade.Warning => new Color32(218, 195, 111, 255),
            StockRiskGrade.Danger => new Color32(225, 160, 104, 255),
            StockRiskGrade.HighRisk => new Color32(228, 127, 135, 255),
            StockRiskGrade.Halted => HaltInk,
            _ => new Color32(157, 166, 175, 255)
        };
        if (textRisk != null)
        {
            string label = stock.RiskLabel;
            if (stock.IsTradingHalted)
            {
                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - SessionClock.Time)) + "초";
            }
            if (!GameLocalizedText.Matches(textRisk,label))
            {
                GameLocalizedText.Set(textRisk,label);
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

    // 미니 캔들 세로 눈금은 고정이다. 가운데가 0%, 맨 아래가 -10%, 맨 위가 +10%. 그보다 큰 움직임은 끝에 붙는다.
    // (±50%였을 때는 하루 1~3% 움직임이 점처럼 보였다.)
    // (요청으로 다시 ±50%: 맨 위가 +50%, 맨 아래가 -50%.)
    private const float CandleScalePercent = 50f;
    void ApplyCandleAnchors(CandleData? summary, float reference)
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
        if (reference <= 0f)
        { reference = candle.open; }
        float Map(float price) => Mathf.Clamp01(.5f + (price / reference - 1f) * 100f / (CandleScalePercent * 2f));
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
            if (top - bottom < .06f)
            {
                float center = Mathf.Clamp((top + bottom) * .5f, .03f, .97f);
                bottom = center - .03f;
                top = center + .03f;
            }
            rect.anchorMin = new Vector2(bongAnchorMinX, bottom);
            rect.anchorMax = new Vector2(bongAnchorMaxX, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            imgBong.color = color;
        }
    }
}
