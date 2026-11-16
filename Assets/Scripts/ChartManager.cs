using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Chart용 디스플레이 전용. 캔들 데이터는 CandleHistoryManager에서 가져와 그리며,
/// 최대 56개만 표시. 드래그로 구간 스크롤( startIndex ) 변경.
/// Raycast는 Display 레이어만 사용. 커서 추적은 이 디스플레이에 붙인 DisplayAimCursor가 담당.
/// (Display는 2종: StockList용 / Chart용 → 각각 DisplayAimCursor 1개씩 붙이면 됨)
/// </summary>
[DefaultExecutionOrder(-190)]
public partial class ChartManager : MonoBehaviour
{
    public CandleDrawManager candleRenderer;
    [Tooltip("선택 캔들 데이터. 비어 있으면 같은 GameObject에서 GetComponent 시도")]
    public ChartCandleSelectData candleSelectData;
    [Header("현재 종목 정보 (TMP_Text)")]
    [Tooltip("현재 종목 심볼")]
    public TextMeshProUGUI textSymbol;
    [Tooltip("현재 종목 이름")]
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textRiskBadge;
    [Tooltip("현재 종목 최근 거래가")]
    public TextMeshProUGUI textLastPrice;
    [Tooltip("현재 종목 상승률")]
    public TextMeshProUGUI textChangePct;
    [Tooltip("같은 전일 종가를 기준으로 계산한 원 단위 등락")]
    public TextMeshProUGUI textChangeAmount;
    [Tooltip("현재 종목 진짜가치")]
    public TextMeshProUGUI textRealValue;
    [Tooltip("현재 종목 가짜가치")]
    public TextMeshProUGUI textFakeValue;
    [Tooltip("현재 종목 거래량")]
    public TextMeshProUGUI textVolume;
    [Tooltip("현재 종목 거래대금")]
    public TextMeshProUGUI textTurnover;
    [Header("추가 통계 정보")]
    [Tooltip("52주 최고가")]
    public TextMeshProUGUI text52WeekHigh;
    [Tooltip("52주 최저가")]
    public TextMeshProUGUI text52WeekLow;
    [Tooltip("전일 종가")]
    public TextMeshProUGUI textPrevClose;
    [Tooltip("당일 고가")]
    public TextMeshProUGUI textDayHigh;
    [Tooltip("당일 고가 vs 전일종가 상승률")]
    public TextMeshProUGUI textDayHighPct;
    [Tooltip("당일 저가")]
    public TextMeshProUGUI textDayLow;
    [Tooltip("당일 저가 vs 전일종가 상승률")]
    public TextMeshProUGUI textDayLowPct;
    [Tooltip("체결강도")]
    public TextMeshProUGUI textTradeStrength;
    [Header("최근 거래 목록")]
    [Tooltip("최근 거래 아이템들의 부모 Transform")]
    public Transform tradeHistoryContainer;
    [Tooltip("TradeHistoryItem 프리팹")]
    public GameObject tradeHistoryItemPrefab;
    [Tooltip("표시할 최대 거래 기록 수")]
    public int maxDisplayTrades = 20;
    private List<TradeHistoryItem> _tradeHistoryItems = new List<TradeHistoryItem>();
    [Tooltip("차트에 한 번에 표시할 최대 캔들 수 (형성중 포함, 56 고정 권장)")]
    public int maxVisibleCandles = 56;
    [Tooltip("드래그 시 픽셀당 스크롤 감도 (이 픽셀만큼 드래그 시 캔들 1개씩 이동)")]
    public float dragPixelsPerCandle = 12f;
    [Tooltip("에임 잠금(마우스 커서 없음)일 때 스크롤 감도. Mouse X 축 1당 이 픽셀만큼 드래그로 간주")]
    public float dragSensitivityWhenAimLocked = 200f;
    [Header("Chart 레이어 (드래그 Raycast용)")]
    [Tooltip("차트 드래그 레이캐스트 대상 레이어. 모니터 전체 Display 콜라이더가 있어도 차트만 잡히도록 Chart 레이어만 사용 권장.")]
    public LayerMask chartLayerMask;
    [Tooltip("비우면 Camera.main 사용. 차트를 찍는 카메라가 다르면 여기 할당.")]
    public Camera chartRaycastCamera;
    [Header("성능 (차트 메시 갱신 주기)")]
    [Tooltip("평상시 차트 갱신 간격(초). 매 프레임 갱신 시 FPS 급락 방지.")]
    public float refreshInterval = 0.15f;
    [Tooltip("드래그 중 갱신 간격(초). 0이면 드래그 시 매 프레임 갱신")]
    public float refreshIntervalWhileDragging = 0.05f;
    [Header("캔들 클릭 / 차트 드래그")]
    [Tooltip("이 거리(픽셀) 이상 움직이면 드래그로 간주, 미만이면 클릭")]
    public float dragClickThresholdPixels = 6f;
    [Header("디버그")]
    [Tooltip("드래그 시작/종료·스크롤량·레이 미적중 시 Console에 로그 출력. 확인 후 끄면 됨.")]
    public bool enableDragDebugLog = true;
    private Stock currentStock;
    private int startIndex, maxStartIndex;
    private bool followLatest = true;
    private bool isDraggingChart;
    private bool _potentialChartDrag;
    private Vector2 _chartDragStartPos;
    private float lastMouseX;
    private Vector2 _lockedDragTravel;
    private float _dragRemainder;
    private static ChartManager pointerOwner;
    private static int lastPointerFrame = -1;
    public static bool HasPointerCapture => (pointerOwner != null && (pointerOwner._potentialChartDrag || pointerOwner.isDraggingChart)) || lastPointerFrame == Time.frameCount;
    private Collider chartCollider;
    private Camera mainCam;
    private Camera _rayCam;
    private float _lastRefreshTime = -999f;
    /// <summary>현재 보이는 캔들 목록 (클릭 시 인덱스·캔들 조회, 캔들 밀림 시 십자선 Refresh용)</summary>
    private List<CandleData> _currentVisibleCandles = new List<CandleData>();
    private readonly List<CandleData> _fullCandles = new List<CandleData>(101);
    private ChartGuideOverlay _guides;
    /// <summary>종목별 세션 거래량·거래대금 (거래 체결 시 누적, 차트 표시용)</summary>
    private readonly Dictionary<Stock, (decimal volume, decimal turnover)> _sessionStats = new Dictionary<Stock, (decimal, decimal)>();
    private float _lastStatsRefreshTime;
    private const float StatsRefreshInterval = 0.5f;
    void Start()
    {
        mainCam = Camera.main;
        ConfigureHeaderLayout();
        _rayCam = chartRaycastCamera != null ? chartRaycastCamera : mainCam;
        if (chartLayerMask == 0)
        {
            chartLayerMask = LayerMask.GetMask("Chart");
        }

        if (candleSelectData == null)
        {
            candleSelectData = GetComponent<ChartCandleSelectData>();
        }

        EnsureGuides();
        CreateNavigation();
        // Collider는 CandleDrawManager.GenerateMesh()에서 추가되므로 1프레임 뒤에 잡힘 → HandleChartDrag에서 null이면 재시도
        if (StockManager.Instance != null)
        {
            foreach (var s in StockManager.Instance.stocks)
            {
                if (s != null)
                {
                    s.OnTradeExecutedWithStock += OnAnyStockTraded;
                }
            }

            currentStock = StockManager.Instance.CurrentStock;
            if (currentStock != null)
            {
                currentStock.OnRealOrFakeValueChanged += RefreshRealFakeValues;
            }
        }

        RefreshSymbolAndName();
        RefreshTradeBasedValues();
        RefreshRealFakeValues();
        RefreshStatistics();
        InitializeTradeHistoryItems();
        StartCoroutine(RefreshNextFrame());
    }

    /// <summary>기존 헤더의 텍스트만 한 번 정렬한다. 차트·호가·월드 Canvas 영역은 유지한다.</summary>
    public void ConfigureHeaderLayout()
    {
        if (textName == null || textName.transform.parent is not RectTransform header)
        { return; }
        PlaceHeaderText(textName,header,.025f,.035f,.30f,.28f,36f);
        PlaceHeaderText(textSymbol,header,.025f,.31f,.30f,.20f,22f,true);
        PlaceHeaderText(textRiskBadge,header,.35f,.075f,.15f,.215f,22f,true);
        PlaceHeaderText(textLastPrice,header,.025f,.50f,.30f,.48f,58f);
        PlaceHeaderText(textChangePct,header,.35f,.48f,.22f,.28f,36f);
        PlaceHeaderText(textChangeAmount,header,.35f,.76f,.22f,.23f,26f);
        // Both statistics columns share caption/value baselines and right edges.
        PlaceHeaderText(textRealValue,header,.63f,.27f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textFakeValue,header,.63f,.72f,.14f,.23f,25f,true,true);
        PlaceHeaderText(textVolume,header,.80f,.27f,.175f,.23f,25f,true,true);
        PlaceHeaderText(textTurnover,header,.80f,.72f,.175f,.23f,25f,true,true);
        PlaceHeaderCaption(header,"Real Caption",.63f,.08f,.14f);
        PlaceHeaderCaption(header,"Fake Caption",.63f,.53f,.14f);
        PlaceHeaderCaption(header,"Volume Caption",.80f,.08f,.175f);
        PlaceHeaderCaption(header,"Turnover Caption",.80f,.53f,.175f);
    }
    private static void PlaceHeaderCaption(RectTransform header,string name,float x,float y,float width)
    {
        var child = header.Find("Text :: " + name);
        if (child != null)
        { PlaceHeaderText(child.GetComponent<TextMeshProUGUI>(),header,x,y,width,.16f,19f,true,true); }
    }
    private static void PlaceHeaderText(TextMeshProUGUI text,RectTransform parent,float x,float y,float width,float height,float size,bool muted = false,bool right = false)
    {
        if (text == null)
        { return; }
        var rect = text.rectTransform;
        if (rect.parent != parent) { rect.SetParent(parent,false); }
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        text.margin = Vector4.zero;
        text.fontSize = size; text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.color = muted ? new Color32(138,150,163,255) : new Color32(230,235,239,255);
    }

    void OnEnable()
    {
        MarketColors.Changed += RefreshMarketColors;
        StockManager.OnStockSelected += SetStock;
        StockManager.OnStockDelisted += OnStockDelisted;
        StockManager.OnStockListed += OnStockListed;
    }

    void OnDisable()
    {
        MarketColors.Changed -= RefreshMarketColors;
        ReleaseChartPointer();
        if (_guides != null) { _guides.HideCursor(); }
        StockManager.OnStockSelected -= SetStock;
        StockManager.OnStockDelisted -= OnStockDelisted;
        StockManager.OnStockListed -= OnStockListed;
    }

    private void RefreshMarketColors()
    {
        RefreshTradeBasedValues(); RefreshStatistics(); RefreshTradeHistory();
        if (_guides != null && currentStock != null) { _guides.Refresh(currentStock); }
    }
    void OnStockListed(Stock stock)
    {
        stock.OnTradeExecutedWithStock -= OnAnyStockTraded;
        stock.OnTradeExecutedWithStock += OnAnyStockTraded;
        if (currentStock == null || currentStock == stock)
        {
            SetStock(StockManager.Instance.CurrentStock);
        }
    }

    void OnStockDelisted(Stock s)
    {
        if (s != null)
        {
            _sessionStats.Remove(s);
        }

        if (StockManager.Instance == null)
        { return; }

        currentStock = StockManager.Instance.CurrentStock;
        RefreshChartFromHistory();
        RefreshSymbolAndName();
        RefreshTradeBasedValues();
        RefreshRealFakeValues();
        RefreshStatistics();
    }

    System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null;
        RefreshChartFromHistory();
    }

    void Update()
    {
        if (PauseMenuController.BlocksInput) { ReleaseChartPointer(); return; }
        HandleChartDrag();
        UpdateNavigation();
        float interval = isDraggingChart ? refreshIntervalWhileDragging : refreshInterval;
        if (interval <= 0f && isDraggingChart)
        {
            RefreshChartFromHistory();
        }
        else if (SessionClock.Time - _lastRefreshTime >= interval)
        {
            RefreshChartFromHistory();
        }

        if (SessionClock.Time - _lastStatsRefreshTime >= StatsRefreshInterval)
        {
            RefreshSymbolAndName();
            RefreshTradeBasedValues();
            RefreshStatistics();
            RefreshTradeHistory();
            _lastStatsRefreshTime = SessionClock.Time;
        }
    }

    void HandleChartDrag()
    {
        if (ChartInputBlocked())
        { ReleaseChartPointer(); return; }
        Vector2 aimScreen = MouseCameraController.GetAimScreenPosition();
        bool locked = MouseCameraController.IsCursorLocked;
        if (Input.GetMouseButtonDown(0) && TryGetChartPoint(out Vector3 pressedPoint) && pressedPoint.x < candleRenderer.PlotWidth)
        {
            _potentialChartDrag = true; pointerOwner = this;
            _chartDragStartPos = aimScreen; lastMouseX = aimScreen.x;
            _lockedDragTravel = Vector2.zero; _dragRemainder = 0f;
        }
        if ((_potentialChartDrag || isDraggingChart) && Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0))
        {
            Vector2 delta = locked ? new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * dragSensitivityWhenAimLocked : new Vector2(aimScreen.x - lastMouseX, 0f);
            lastMouseX = aimScreen.x;
            _lockedDragTravel += delta;
            float moved = locked ? _lockedDragTravel.magnitude : (aimScreen - _chartDragStartPos).magnitude;
            if (_potentialChartDrag && moved >= dragClickThresholdPixels)
            {
                isDraggingChart = true; _potentialChartDrag = false;
                _dragRemainder = locked ? _lockedDragTravel.x : aimScreen.x - _chartDragStartPos.x;
            }
            else if (isDraggingChart)
            { _dragRemainder += delta.x; }
            if (isDraggingChart)
            {
                float pixelsPerCandle = Mathf.Max(1f, dragPixelsPerCandle);
                int steps = (int)(_dragRemainder / pixelsPerCandle);
                _dragRemainder -= steps * pixelsPerCandle;
                if (steps != 0)
                {
                    startIndex = Mathf.Clamp(startIndex - steps, 0, maxStartIndex);
                    followLatest = startIndex == maxStartIndex;
                    CaptureHistoryAnchor();
                    candleSelectData?.Clear();
                    _lastRefreshTime = -999f;
                }
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (_potentialChartDrag && TryGetChartPoint(out Vector3 point)) { HandleCandleClick(point); }
            ReleaseChartPointer();
        }
        else if (!Input.GetMouseButton(0)) { ReleaseChartPointer(); }
    }

    void HandleCandleClick(Vector3 local)
    {
        if (candleRenderer == null || !candleRenderer.TryCandleAt(local.x, out int index))
        { return; }
        int globalIndex = startIndex + index;
        CandleData candle = _currentVisibleCandles[index];
        float? previous = globalIndex > 0 ? _fullCandles[globalIndex - 1].close : (float?)null;
        if (candleSelectData != null)
        { candleSelectData.SetSelection(candle, globalIndex, previous); }
    }

    void EnsureGuides()
    {
        if (candleRenderer == null || _guides != null)
        { return; }
        _guides = ChartGuideOverlay.Create(candleRenderer, textRealValue != null ? textRealValue : textLastPrice, maxVisibleCandles);
    }

    void LateUpdate()
    {
        if (PauseMenuController.BlocksInput) { _guides?.HideCursor(); return; }
        if (_guides == null)
        { return; }
        if (ChartInputBlocked() || isDraggingChart || !TryGetChartPoint(out Vector3 point) || point.x >= candleRenderer.PlotWidth || point.y < 0f)
        { _guides.HideCursor(); return; }
        if (!candleRenderer.TryCandleAt(point.x, out int index) || index >= _currentVisibleCandles.Count)
        { _guides.ShowCursor(point, default, false); return; }
        bool forming = startIndex + index == _fullCandles.Count - 1;
        if (periodMinutes > 0)
        {
            var clock = MarketDayController.Instance;
            var now = new CandleData { marketDay = clock == null ? 0 : clock.Day, marketMinute = clock == null ? 0 : clock.GameMinute };
            forming = forming && MarketDayController.CanTrade && ChartHistorySeries.Key(now, periodMinutes) == ChartHistorySeries.Key(_currentVisibleCandles[index], periodMinutes);
        }
        _guides.ShowCursor(point, _currentVisibleCandles[index], forming);
    }

    bool TryGetChartPoint(out Vector3 point)
    {
        point = default;
        if (candleRenderer == null)
        { return false; }
        if (chartCollider == null) { chartCollider = candleRenderer.GetComponent<Collider>(); }
        Camera cam = chartRaycastCamera != null ? chartRaycastCamera : _rayCam;
        if (cam == null || chartCollider == null)
        { return false; }
        Ray ray = cam.ScreenPointToRay(MouseCameraController.GetAimScreenPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit, cam.farClipPlane, chartLayerMask) || hit.collider != chartCollider)
        { return false; }
        // BoxCollider 두께 대신 실제 메시 평면을 사용해 원근 상태에서도 눈금을 맞춘다.
        Transform chart = candleRenderer.transform;
        var plane = new Plane(chart.forward, chart.position);
        if (!plane.Raycast(ray, out float distance))
        { return false; }
        point = chart.InverseTransformPoint(ray.GetPoint(distance));
        return point.x >= 0f && point.x <= candleRenderer.ChartWidth && point.y >= -candleRenderer.VolumePlotHeight && point.y <= candleRenderer.chartHeight;
    }
    static bool ChartInputBlocked() => PauseMenuController.BlocksInput || DeskViewController.Instance == null || DeskViewController.BlockChartPointer || TabletViewController.IsTabletHeld || PhoneCallController.IsRaised || MouseCameraController.TextEntryActive || (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed);
    void ReleaseChartPointer()
    {
        _potentialChartDrag = false; isDraggingChart = false;
        if (pointerOwner == this) { lastPointerFrame = Time.frameCount; pointerOwner = null; }
    }

    public void SetStock(Stock stock)
    {
        if (currentStock != null)
        {
            currentStock.OnRealOrFakeValueChanged -= RefreshRealFakeValues;
        }

        currentStock = stock;
        startIndex = 0;
        maxStartIndex = 0;
        followLatest = true;
        if (candleSelectData != null)
        {
            candleSelectData.Clear();
        }

        ReleaseChartPointer();
        if (_guides != null) { _guides.HideCursor(); }
        if (currentStock != null)
        {
            currentStock.OnRealOrFakeValueChanged += RefreshRealFakeValues;
        }

        RefreshSymbolAndName();
        RefreshTradeBasedValues();
        RefreshRealFakeValues();
        RefreshStatistics();
        RefreshTradeHistory();
        RefreshChartFromHistory();
    }

    /// <summary>종목이 바뀔 때만 호출. 심볼·이름 갱신.</summary>
    void RefreshSymbolAndName()
    {
        if (currentStock == null)
        { return; }

        if (textSymbol != null)
        {
            GameLocalizedText.Set(textSymbol,currentStock.symbol);
        }

        if (textName != null)
        {
            GameLocalizedText.Set(textName,currentStock.stockName);
        }
        if (textRiskBadge != null)
        {
            GameLocalizedText.Set(textRiskBadge,currentStock.RiskLabel);
            textRiskBadge.color = currentStock.IsTradingHalted ? new Color32(145, 152, 158, 255) : currentStock.RiskGrade == StockRiskGrade.Safe ? new Color32(127, 165, 140, 255) : currentStock.RiskGrade == StockRiskGrade.Warning ? new Color32(205, 182, 116, 255) : currentStock.RiskGrade == StockRiskGrade.Danger ? new Color32(217, 151, 102, 255) : new Color32(221, 119, 127, 255);
        }
    }

    public static string FormatDailyChange(decimal currentPrice, decimal previousClose)
    {
        if (previousClose <= 0m)
        { return "—"; }
        decimal difference = currentPrice - previousClose;
        if (difference == 0m)
        { return "-   0"; }
        return (difference > 0m ? "▲ " : "▼ ") + System.Math.Abs(difference).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>거래 체결 이벤트 시 호출. 최근 거래가·상승률·거래량·거래대금 갱신.</summary>
    void RefreshTradeBasedValues()
    {
        if (currentStock == null)
        { return; }

        if (textLastPrice != null)
        {
            GameLocalizedText.Set(textLastPrice,PriceTickPolicy.FormatPrice(currentStock.currentPrice));
        }

        decimal refPrice = GetReferencePriceForChangePct();
        decimal change = refPrice > 0m ? currentStock.currentPrice - refPrice : 0m;
        decimal pct = refPrice > 0m ? change / refPrice * 100m : 0m;
        Color changeColor = change > 0m ? MarketColors.Up : change < 0m ? MarketColors.Down : new Color32(138, 150, 163, 255);
        if (textChangePct != null)
        {
            GameLocalizedText.Set(textChangePct,refPrice > 0m ? (pct > 0m ? $"+{pct:N2}%" : $"{pct:N2}%") : "—");
            textChangePct.color = changeColor;
        }
        if (textChangeAmount != null)
        {
            string value = FormatDailyChange(currentStock.currentPrice, refPrice);
            if (!GameLocalizedText.Matches(textChangeAmount,value))
            { GameLocalizedText.Set(textChangeAmount,value); }
            if (textChangeAmount.color != changeColor)
            { textChangeAmount.color = changeColor; }
        }

        if (_sessionStats.TryGetValue(currentStock, out var stats))
        {
            if (textVolume != null)
            {
                GameLocalizedText.Set(textVolume,stats.volume.ToString("N0"));
            }

            if (textTurnover != null)
            {
                GameLocalizedText.Set(textTurnover,stats.turnover.ToString("N0"));
            }
        }
        else
        {
            if (textVolume != null)
            {
                GameLocalizedText.Set(textVolume,"0");
            }

            if (textTurnover != null)
            {
                GameLocalizedText.Set(textTurnover,"0");
            }
        }
    }

    /// <summary>진짜가치/가짜가치 변경 시 호출 (itCompany·세력 조작 이벤트).</summary>
    void RefreshRealFakeValues()
    {
        if (currentStock == null)
        { return; }

        if (textRealValue != null)
        {
            // 기업 실제 가치와 시장이 믿는 가격은 플레이어가 해금한 뒤에만 보인다.
            GameLocalizedText.Set(textRealValue,PlayerProgression.Has(PlayerProgression.RealValueUnlock) ? PriceTickPolicy.FormatPrice(currentStock.realValue) : "—");
        }

        if (textFakeValue != null)
        {
            GameLocalizedText.Set(textFakeValue,PlayerProgression.Has(PlayerProgression.CrowdValueUnlock) ? PriceTickPolicy.FormatPrice(currentStock.fakeValue) : "—");
        }
    }

    decimal GetReferencePriceForChangePct()
    {
        if (currentStock == null || StockStatistics.Instance == null)
        { return 0m; }
        return StockStatistics.Instance.GetStats(currentStock).prevDayClose;
    }

    /// <summary>52주 최고/최저, 전일 종가, 당일 고가/저가, 체결강도 갱신</summary>
    void RefreshStatistics()
    {
        if (currentStock == null || StockStatistics.Instance == null)
        { return; }

        var stats = StockStatistics.Instance.GetStats(currentStock);
        if (stats.stock == null)
        { return; }

        if (text52WeekHigh != null)
        {
            GameLocalizedText.Set(text52WeekHigh,PriceTickPolicy.FormatPrice(stats.week52High));
        }

        if (text52WeekLow != null)
        {
            GameLocalizedText.Set(text52WeekLow,PriceTickPolicy.FormatPrice(stats.week52Low));
        }

        if (textPrevClose != null)
        {
            GameLocalizedText.Set(textPrevClose,PriceTickPolicy.FormatPrice(stats.prevDayClose));
        }

        if (textDayHigh != null)
        {
            GameLocalizedText.Set(textDayHigh,PriceTickPolicy.FormatPrice(stats.dayHigh));
        }

        if (textDayHighPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayHigh - stats.prevDayClose) / stats.prevDayClose * 100m;
            GameLocalizedText.Set(textDayHighPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
            textDayHighPct.color = pct >= 0m ? MarketColors.Up : MarketColors.Down;
        }

        if (textDayLow != null)
        {
            GameLocalizedText.Set(textDayLow,PriceTickPolicy.FormatPrice(stats.dayLow));
        }

        if (textDayLowPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayLow - stats.prevDayClose) / stats.prevDayClose * 100m;
            GameLocalizedText.Set(textDayLowPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
            textDayLowPct.color = pct >= 0m ? MarketColors.Up : MarketColors.Down;
        }

        if (textTradeStrength != null)
        {
            float strength = StockStatistics.Instance.GetTradeStrength(currentStock);
            GameLocalizedText.Set(textTradeStrength,strength >= 999f ? "999+" : $"{strength:N1}%");
            textTradeStrength.color = strength >= 100f ? MarketColors.Up : MarketColors.Down;
        }
    }

    void InitializeTradeHistoryItems()
    {
        if (tradeHistoryContainer == null || tradeHistoryItemPrefab == null)
        { return; }

        _tradeHistoryItems.Clear();
        foreach (Transform child in tradeHistoryContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < maxDisplayTrades; i++)
        {
            var go = Instantiate(tradeHistoryItemPrefab, tradeHistoryContainer);
            var item = go.GetComponent<TradeHistoryItem>();
            if (item != null)
            {
                _tradeHistoryItems.Add(item);
                go.SetActive(false);
            }
        }
    }

    void RefreshTradeHistory()
    {
        if (currentStock == null || StockStatistics.Instance == null)
        { return; }

        var trades = StockStatistics.Instance.GetRecentTrades(currentStock);
        for (int i = 0; i < _tradeHistoryItems.Count; i++)
        {
            int tradeIdx = trades.Count - 1 - i;
            if (tradeIdx >= 0 && tradeIdx < trades.Count)
            {
                var trade = trades[tradeIdx];
                _tradeHistoryItems[i].Set(trade.price, trade.volume, trade.isBuyInitiated);
                _tradeHistoryItems[i].gameObject.SetActive(true);
            }
            else
            {
                _tradeHistoryItems[i].gameObject.SetActive(false);
            }
        }
    }

    void OnAnyStockTraded(Stock stock, decimal price, decimal volume)
    {
        if (stock == null)
        { return; }

        if (!_sessionStats.TryGetValue(stock, out var v))
        {
            v = (0m, 0m);
        }

        _sessionStats[stock] = (v.volume + volume, v.turnover + price * volume);
        if (stock == currentStock)
        {
            RefreshTradeBasedValues();
        }
    }

    /// <summary>해당 Stock의 과거 캔들 + 형성중 캔들 중 최대 56개만 슬라이스해서 차트 갱신.</summary>
    void RefreshChartFromHistory()
    {
        if (candleRenderer == null)
        { return; }

        if (currentStock == null)
        {
            if (StockManager.Instance != null)
            {
                currentStock = StockManager.Instance.CurrentStock;
            }

            if (currentStock == null)
            { return; }
        }

        if (CandleHistoryManager.Instance == null)
        { return; }

        var closed = CandleHistoryManager.Instance.ChartHistory(currentStock);
        var forming = CandleHistoryManager.Instance.GetFormingCandle(currentStock);
        float price = (float)currentStock.currentPrice;
        if (forming.low == forming.high && forming.high == 0f && price > 0f)
        { forming.open = forming.close = forming.high = forming.low = price; }
        if (periodMinutes == 0)
        { ChartHistorySeries.Build(closed, forming, 0, _fullCandles); }
        else
        { ChartHistorySeries.Build(CandleHistoryManager.Instance.MinuteHistory(currentStock), default, periodMinutes, _fullCandles, false); }
        int take = Mathf.Min(maxVisibleCandles, _fullCandles.Count);
        maxStartIndex = Mathf.Max(0, _fullCandles.Count - take);
        RestoreHistoryAnchor();
        startIndex = followLatest ? maxStartIndex : Mathf.Clamp(startIndex, 0, maxStartIndex);
        _currentVisibleCandles.Clear();
        for (int i = 0; i < take; i++)
        { _currentVisibleCandles.Add(_fullCandles[startIndex + i]); }
        candleRenderer.candles = _currentVisibleCandles;
        candleRenderer.GenerateMesh();
        if (_guides != null) { _guides.Refresh(currentStock); }
        _lastRefreshTime = SessionClock.Time;
        // 기존 클릭 선택의 OHLC 정보 유지. 커서 정보는 LateUpdate에서 따로 처리한다.
        if (candleSelectData != null && candleSelectData.HasSelection && candleSelectData.SelectedGlobalIndex.HasValue)
        {
            int g = candleSelectData.SelectedGlobalIndex.Value;
            if (g >= startIndex && g < startIndex + take)
            {
                int localIdx = g - startIndex;
                var candle = _currentVisibleCandles[localIdx];
                float? prevClose = g > 0 ? _fullCandles[g - 1].close : (float? )null;
                candleSelectData.SetSelection(candle, g, prevClose);
                bool candleOnLeft = localIdx < maxVisibleCandles * 0.5f;
                candleSelectData.SetPanelSide(candleOnLeft);
            }
        }
    }

    void OnDestroy()
    {
        ReleaseChartPointer();
        if (_guides != null) { Destroy(_guides.gameObject); }
        if (StockManager.Instance != null)
        {
            foreach (var s in StockManager.Instance.stocks)
            {
                if (s != null)
                {
                    s.OnTradeExecutedWithStock -= OnAnyStockTraded;
                }
            }
        }

        if (currentStock != null)
        {
            currentStock.OnRealOrFakeValueChanged -= RefreshRealFakeValues;
            currentStock = null;
        }
    }
}
