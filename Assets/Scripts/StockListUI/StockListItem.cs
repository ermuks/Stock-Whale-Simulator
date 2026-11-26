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
    /// 맨 오른쪽: 위에 거래량과 종목 기호(123k SSE), 아래에 거래대금(Ð만 붙인다).
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
        // 이름 왼쪽에 회사 로고, 이름 밑에 업종(그림 + 이름). 이름은 그만큼 오른쪽·위로 옮긴다.
        if (textName != null)
        {
            var nameRect = textName.rectTransform; nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x + LogoShift, 26f);
            logo = new GameObject("Area :: Company Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            logo.SetParent(transform, false); logo.gameObject.layer = gameObject.layer;
            logo.anchorMin = logo.anchorMax = new Vector2(0f, .5f); logo.pivot = new Vector2(0f, .5f); logo.sizeDelta = new Vector2(140f, 140f); logo.anchoredPosition = new Vector2(34f, 0f);
            sectorIcon = new GameObject("Image :: Sector Icon", typeof(RectTransform)).AddComponent<Image>(); sectorIcon.raycastTarget = false; sectorIcon.preserveAspect = true;
            var iconRect = sectorIcon.rectTransform; iconRect.SetParent(transform, false); sectorIcon.gameObject.layer = gameObject.layer;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f); iconRect.pivot = new Vector2(0f, .5f); iconRect.sizeDelta = new Vector2(40f, 40f); iconRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -44f);
            sectorLabel = Instantiate(textName, transform); sectorLabel.name = "Text :: Sector"; sectorLabel.raycastTarget = false;
            var labelRect = sectorLabel.rectTransform; labelRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x + 52f, -44f); labelRect.sizeDelta = new Vector2(460f, 50f);
            sectorLabel.fontSize = 38f; sectorLabel.enableAutoSizing = false; sectorLabel.color = SectorInk; sectorIcon.color = SectorInk;
        }
        if (riskBadgeBackground != null) { Middle(riskBadgeBackground.rectTransform); riskBadgeBackground.rectTransform.anchoredPosition = new Vector2(riskBadgeBackground.rectTransform.anchoredPosition.x, 20f); }
        if (textRisk != null) { Middle(textRisk.rectTransform); textRisk.rectTransform.anchoredPosition = new Vector2(textRisk.rectTransform.anchoredPosition.x, 20f); }
        if (textSymbol != null) { textSymbol.gameObject.SetActive(false); }
        // 이름과 가격 사이의 빈 자리에 오늘의 흐름 선을 둔다.
        var holder = new GameObject("Image :: Flow Line", typeof(RectTransform), typeof(CanvasRenderer)) { layer = gameObject.layer };
        holder.transform.SetParent(transform, false);
        flow = holder.AddComponent<StockFlowLine>(); flow.raycastTarget = false;
        var area = flow.rectTransform;
        area.anchorMin = new Vector2(1f, .2f); area.anchorMax = new Vector2(1f, .8f); area.pivot = new Vector2(1f, .5f);
        area.sizeDelta = new Vector2(FlowWidth, 0f); area.anchoredPosition = new Vector2(-(edge + side + gap + middle + FlowGap), 0f);
        // 오늘의 미니 캔들(시가·고가·저가·종가)은 흐름 선 바로 왼쪽으로 옮긴다(맨 왼쪽은 로고 자리다).
        var candle = transform.Find("Image :: Simple Candle") as RectTransform;
        if (candle != null)
        {
            candle.anchorMin = new Vector2(1f, candle.anchorMin.y); candle.anchorMax = new Vector2(1f, candle.anchorMax.y); candle.pivot = new Vector2(1f, candle.pivot.y);
            candle.anchoredPosition = new Vector2(-(edge + side + gap + middle + FlowGap + FlowWidth + 18f), candle.anchoredPosition.y);
        }
        // 오른쪽 끝의 보조 정보(거래량·거래대금)와 등락률은 한 단 밝고 크게: 예전에는 너무 흐려 읽히지 않았다.
        Column(textVolume10, true, edge, side, 40f, new Color32(206, 214, 222, 255));
        Column(textTurnover, false, edge, side, 38f, new Color32(166, 180, 192, 255));
        Column(textPrice, true, edge + side + gap, middle, 62f, PriceInk);
        Column(textChangePct10, false, edge + side + gap, middle, 44f, new Color32(166, 180, 192, 255));
        // 고른 종목: 줄 왼쪽 끝의 굵은 청록 막대 + 은은한 청록 바탕. 체결 깜빡임은 줄 전체를 물들이지 않고 로고 옆 작은 눈금으로만 보인다.
        selectBar = HaltImage("Image :: Selected Bar", TabletKitStyle.Accent);
        selectBar.rectTransform.anchorMin = new Vector2(0f, .08f); selectBar.rectTransform.anchorMax = new Vector2(0f, .92f); selectBar.rectTransform.pivot = new Vector2(0f, .5f);
        selectBar.rectTransform.sizeDelta = new Vector2(10f, 0f); selectBar.rectTransform.anchoredPosition = Vector2.zero; selectBar.enabled = false;
        if (selectionHighlight != null) { selectionHighlight.color = new Color(.18f, .91f, .81f, .08f); }
        tradeTick = HaltImage("Image :: Trade Tick", Color.clear);
        tradeTick.rectTransform.anchorMin = tradeTick.rectTransform.anchorMax = new Vector2(0f, .5f); tradeTick.rectTransform.pivot = new Vector2(0f, .5f);
        tradeTick.rectTransform.sizeDelta = new Vector2(8f, 56f); tradeTick.rectTransform.anchoredPosition = new Vector2(184f, 0f); tradeTick.enabled = false;
    }
    private Image selectBar, tradeTick;

    // ── 흐름 선 ──
    // 오늘의 분봉 종가를 선으로 잇는다. 가운데가 전일 종가다. 가로는 오늘 지금까지 흐른 만큼을 폭에 가득 채운다(장 초반에도 모양이 보이게. 적어도 20분 폭).
    // 세로 폭은 오늘 전일 종가에서 가장 멀리 간 만큼에 맞춘다(적어도 ±3%). 잔잔한 날이 요란해 보이지 않게.
    private const float FlowWidth = 250f, FlowGap = 28f, FlowMinimumRange = .03f, FlowMinimumMinutes = 20f;
    private const int FlowPoints = 64;
    private StockFlowLine flow;
    private readonly List<Vector2> flowPoints = new(FlowPoints + 2);
    private float flowNext; private int flowCount = -1; private decimal flowLast; private float flowReference;
    private void ShowFlow(Stock stock, float reference)
    {
        if (flow == null) { return; }
        var history = CandleHistoryManager.Instance; var market = MarketDayController.Instance;
        if (stock == null || history == null || market == null || reference <= 0f || !stock.IsListed) { if (flow.enabled) { flow.enabled = false; } flowCount = -1; return; }
        if (!flow.enabled) { flow.enabled = true; flowCount = -1; }
        var minutes = history.MinuteHistory(stock);
        // 1초에 한 번, 그리고 값이나 분봉 수가 달라졌을 때만 다시 그린다.
        if (Time.unscaledTime < flowNext || minutes.Count == flowCount && stock.currentPrice == flowLast && Mathf.Approximately(reference, flowReference)) { return; }
        flowNext = Time.unscaledTime + 1f; flowCount = minutes.Count; flowLast = stock.currentPrice; flowReference = reference;
        int today = market.Day, first = minutes.Count;
        while (first > 0 && minutes[first - 1].marketDay == today && !minutes[first - 1].historical) { first--; }
        int count = minutes.Count - first;
        flowPoints.Clear();
        float range = reference * FlowMinimumRange;
        for (int i = first; i < minutes.Count; i++) { range = Mathf.Max(range, Mathf.Abs(minutes[i].close - reference)); }
        range = Mathf.Max(range, Mathf.Abs((float)stock.currentPrice - reference));
        float Height(float price) => Mathf.Clamp01(.5f + (price - reference) / range * .46f);
        // 시작점: 전일 종가에서 출발한다.
        flowPoints.Add(new Vector2(0f, .5f));
        int stride = Mathf.Max(1, Mathf.CeilToInt(count / (float)FlowPoints));
        float span = Mathf.Max(FlowMinimumMinutes, count + 1);
        float lastX = 0f;
        for (int i = first; i < minutes.Count; i += stride)
        {
            float x = Mathf.Clamp01((i - first + 1) / span);
            if (x <= lastX) { continue; }
            lastX = x; flowPoints.Add(new Vector2(x, Height(minutes[i].close)));
        }
        // 끝점: 지금 값.
        flowPoints.Add(new Vector2(Mathf.Clamp01(lastX + 1f / span), Height((float)stock.currentPrice)));
        flow.Show(flowPoints, 5f);
    }
    private static readonly Color32 PriceInk = new(236, 240, 243, 255), SectorInk = new(150, 166, 178, 255);
    private const float LogoShift = 96f;
    private RectTransform logo;
    private Image sectorIcon;
    private TextMeshProUGUI sectorLabel;
    private Stock logoStock;
    private int sectorLocale = -1;
    /// <summary>로고와 업종 줄을 그 종목으로 맞춘다(종목이나 언어가 바뀔 때만).</summary>
    private void ApplyIdentity(Stock stock)
    {
        if (logo == null || stock == null) { return; }
        if (logoStock == stock && sectorLocale == GameLocale.Revision) { return; }
        logoStock = stock; sectorLocale = GameLocale.Revision;
        UiPictograms.Logo(logo, stock, new Color32(23, 29, 34, 255));
        var sector = UiPictograms.SectorOf(stock);
        sectorIcon.sprite = UiPictograms.SectorIcon(sector);
        GameLocalizedText.Set(sectorLabel, UiPictograms.SectorName(sector), true);
    }
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

        if (_stock != stock) { ListenTrades(stock); }
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

        ApplyIdentity(stock);
        if (!stock.IsListed) { ApplyAccounts(null); ApplyWaiting(); return; }
        ApplyRiskBadge(stock);
        ApplyAccounts(stock);
        ApplyOperating(stock);
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
                if (selectBar != null) { selectBar.enabled = selected; }
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
            string turnoverLine = "Ð" + FormatTurnover(turnoverSum);
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
        ShowFlow(stock, reference);
    }

    /// <summary>아직 상장하지 않은(다음 개장에 상장하는) 종목의 행: 꼬리표에 '상장 대기 중', 거래는 내일 09:00부터.</summary>
    private void ApplyWaiting()
    {
        // 상장 대기 꼬리표는 늘 보인다(안전 등급이라 숨겨 둔 꼬리표를 다시 켠다).
        if (riskHidden) { riskHidden = false; badgeLayoutDirty = true; if (textRisk != null) { textRisk.gameObject.SetActive(true); } if (riskBadgeBackground != null) { riskBadgeBackground.gameObject.SetActive(true); } }
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
        ApplyCandleAnchors(null, 0f); ShowFlow(null, 0f);
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
        float badgeWidth = riskHidden ? -AccountGap : Mathf.Clamp(textRisk.GetPreferredValues(textRisk.text).x + 26f,64f,248f);
        // 보유 계좌 꼬리표가 차지할 폭(켜져 있는 것만).
        float accountsWidth = 0f;
        for (int i = 0; i < AccountCount; i++)
        {
            if (accountLabels[i] == null || !accountLabels[i].gameObject.activeSelf) { accountWidths[i] = 0f; continue; }
            accountWidths[i] = Mathf.Clamp(accountLabels[i].GetPreferredValues(accountLabels[i].text).x + 26f, 64f, 200f);
            accountsWidth += accountWidths[i] + AccountGap;
        }
        // 흐름 선의 자리(FlowWidth + 여백)만큼 이름이 쓸 수 있는 폭을 줄인다.
        float nameWidth = Mathf.Clamp(width - 40f - 570f - 32f - badgeWidth - accountsWidth - 16f - textName.rectTransform.anchoredPosition.x - FlowWidth - FlowGap * 2f - 64f,120f,650f);
        textName.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,nameWidth);
        float x = textName.rectTransform.anchoredPosition.x + Mathf.Min(nameWidth,textName.GetPreferredValues(textName.text).x) + 16f;
        var position = riskBadgeBackground.rectTransform.anchoredPosition; position.x = x;
        riskBadgeBackground.rectTransform.anchoredPosition = position; textRisk.rectTransform.anchoredPosition = position;
        riskBadgeBackground.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
        textRisk.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,badgeWidth);
        // 보유 계좌 꼬리표는 위험 꼬리표 바로 오른쪽에 차례로 놓는다.
        float next = x + badgeWidth + AccountGap;
        for (int i = 0; i < AccountCount; i++)
        {
            if (accountWidths[i] <= 0f) { continue; }
            var spot = new Vector2(next, position.y);
            accountChips[i].rectTransform.anchoredPosition = spot; accountLabels[i].rectTransform.anchoredPosition = spot;
            accountChips[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, accountWidths[i]);
            accountLabels[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, accountWidths[i]);
            next += accountWidths[i] + AccountGap;
        }
    }

    // ── 보유 계좌 ──
    // 내가 이 종목을 들고 있는 계좌를 이름 옆에 작은 꼬리표 하나로 보인다(계좌1, 계좌2, 둘 다면 계좌1·2). 들고 있지 않으면 아무것도 없다.
    // 꼬리표는 위험 꼬리표를 본떠 만든다(크기·글꼴이 같다). 2번 계좌는 열려 있을 때만 본다.
    // 두 계좌를 꼬리표 두 개로 따로 보였을 때는 긴 종목 이름이 잘리고 흐름 선과 겹쳤다. 그래서 하나로 묶는다.
    private const int AccountCount = 2;
    private const float AccountGap = 10f;
    // 1번만: 청록, 2번만: 호박색, 둘 다: 밝은 회색.
    private static readonly Color32[] AccountInk = { new(46, 230, 208, 255), new(236, 180, 90, 255), new(214, 222, 230, 255) };
    private readonly Image[] accountChips = new Image[AccountCount];
    private readonly TextMeshProUGUI[] accountLabels = new TextMeshProUGUI[AccountCount];
    private readonly float[] accountWidths = new float[AccountCount];
    private int accountMask, accountLocale = -1;
    private void ApplyAccounts(Stock stock)
    {
        var accounts = PlayerAccounts.Instance;
        int mask = 0;
        if (stock != null && accounts != null && accounts.Ready)
        {
            if (accounts.primary != null && accounts.primary.GetOwnedVolume(stock) > 0m) { mask |= 1; }
            if (accounts.automated != null && accounts.automated != accounts.primary && accounts.automated.GetOwnedVolume(stock) > 0m) { mask |= 1; }
        }
        if (mask == accountMask && accountLocale == GameLocale.Revision) { return; }
        accountMask = mask; accountLocale = GameLocale.Revision; badgeLayoutDirty = true;
        if (textRisk == null || riskBadgeBackground == null) { return; }
        bool shown = mask != 0;
        if (accountChips[0] == null)
        {
            if (!shown) { return; }
            accountChips[0] = Instantiate(riskBadgeBackground, transform); accountChips[0].name = "Image :: Held Accounts";
            accountLabels[0] = Instantiate(textRisk, transform); accountLabels[0].name = "Text :: Held Accounts";
            accountChips[0].raycastTarget = false; accountLabels[0].raycastTarget = false;
        }
        if (accountChips[0].gameObject.activeSelf != shown) { accountChips[0].gameObject.SetActive(shown); accountLabels[0].gameObject.SetActive(shown); }
        if (!shown) { return; }
        // 계좌가 하나라 번호는 붙이지 않는다. 들고 있다는 것만 알린다.
        string code = GameLocale.Code;
        GameLocalizedText.Set(accountLabels[0], code == "ja" ? "保有" : code == "en" ? "Held" : "보유", true);
        mask = 1;
        Color ink = AccountInk[mask - 1]; accountLabels[0].color = ink; ink.a = .14f; accountChips[0].color = ink;
    }

    // ── 작전 중 ──
    // 이 종목에서 내가 무언가를 돌리고 있으면(작전 도구, 스프레드, 매집) 보유 꼬리표 옆에 호박색 꼬리표 하나를 더 붙인다.
    // 목록을 훑어보기만 해도 어디에 손을 대고 있는지 보인다.
    private bool operating; private int operatingLocale = -1; private float nextOperating;
    private void ApplyOperating(Stock stock)
    {
        bool on = stock != null && stock.IsListed && PlayerOperations.Active(stock);
        if (on == operating && operatingLocale == GameLocale.Revision) { return; }
        operating = on; operatingLocale = GameLocale.Revision; badgeLayoutDirty = true;
        if (textRisk == null || riskBadgeBackground == null) { return; }
        if (accountChips[1] == null)
        {
            if (!on) { return; }
            accountChips[1] = Instantiate(riskBadgeBackground, transform); accountChips[1].name = "Image :: Operating";
            accountLabels[1] = Instantiate(textRisk, transform); accountLabels[1].name = "Text :: Operating";
            accountChips[1].raycastTarget = false; accountLabels[1].raycastTarget = false;
        }
        if (accountChips[1].gameObject.activeSelf != on) { accountChips[1].gameObject.SetActive(on); accountLabels[1].gameObject.SetActive(on); }
        if (!on) { return; }
        GameLocalizedText.Set(accountLabels[1], PlayerOperations.Chip, true);
        Color ink = AccountInk[1]; accountLabels[1].color = ink; ink.a = .16f; accountChips[1].color = ink;
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
    // ── 체결 깜빡임 ──
    // 이 종목에서 체결이 날 때마다 줄 전체가 잠깐(0.6초) 물든다. 거래가 몰리는 종목은 쉴 새 없이 깜빡여서 목록에서 바로 눈에 띈다.
    // 색은 "누가 쳤는가"를 따른다: 걸려 있던 매수 주문이 체결됐으면 누군가 팔아 치운 것이므로 매도색, 걸려 있던 매도 주문이 체결됐으면 누군가 사 간 것이므로 매수색.
    private const float TradeFlashSeconds = .6f, TradeFlashAlpha = .02f;
    private Image tradeFlash;
    private Stock listened;
    private float flashUntil = -1f;
    private void ListenTrades(Stock stock)
    {
        if (listened != null) { listened.OnMatchedTrade -= FlashTrade; }
        listened = isActiveAndEnabled ? stock : null;
        if (listened != null) { listened.OnMatchedTrade += FlashTrade; }
        if (tradeFlash != null) { tradeFlash.enabled = false; }
        flashUntil = -1f;
    }
    private void FlashTrade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (resting == null || volume <= 0m)
        { return; }
        // 줄 전체를 물들이던 깜빡임 대신, 로고 옆 작은 눈금이 체결 쪽 색으로 잠깐 켜진다(거래가 몰리는 종목은 쉴 새 없이 깜빡인다).
        if (tradeTick == null) { return; }
        tradeFlash = tradeTick;
        tradeTick.color = resting.isBuy ? Down : Up; tradeTick.enabled = true;
        flashUntil = Time.unscaledTime + TradeFlashSeconds;
    }
    void Update()
    {
        if (flashUntil >= 0f && Time.unscaledTime >= flashUntil)
        {
            flashUntil = -1f;
            if (tradeFlash != null) { tradeFlash.enabled = false; }
        }
    }
    void OnEnable() { if (_stock != null && listened == null) { ListenTrades(_stock); } }
    void OnDisable() { ListenTrades(null); }

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

    /// <summary>안전한 종목에는 꼬리표를 달지 않는다(모든 줄에 붙은 "안전"이 정작 다른 표시를 묻었다). 주의·위험·거래정지만 보인다.</summary>
    private bool riskHidden;
    void ApplyRiskBadge(Stock stock)
    {
        bool quiet = !stock.IsTradingHalted && (stock.RiskPending || stock.RiskGrade == StockRiskGrade.Safe);
        if (quiet != riskHidden) { riskHidden = quiet; badgeLayoutDirty = true; if (textRisk != null) { textRisk.gameObject.SetActive(!quiet); } if (riskBadgeBackground != null) { riskBadgeBackground.gameObject.SetActive(!quiet); } }
        if (quiet) { return; }
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
