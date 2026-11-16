using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whale(세력) 컨트롤러 - Phase 1: Accumulation(조용한 매집) 구현.
/// - 회사 소유주 계좌는 직접 투자하며, 레거시 비소유주 고래는 팔로워 컨트롤러로만 동작.
/// - 여러 WhaleFollowerBot을 지휘해 작은 지정가/가끔 공격적 매수로 천천히 물량을 모음.
/// - 눈에 띄는 매수벽을 피하기 위해 수량을 작게(10~50주), 가격을 분산(현재가×0.97~0.995).
/// - 1~3초 간격으로 행동하고, 오래된 주문은 랜덤으로 취소해 벽이 고정되지 않게 함.
/// </summary>
public partial class WhaleBot : TradeBot
{
    /// <summary>고래 작전 시작 시 (대장 계좌, 종목, 관심도 보너스)</summary>
    public static event System.Action<WhaleBot, Stock, float> OnWhaleOperationStarted;
    /// <summary>고래 펌프 시작 시 (대장 계좌, 종목, 목표가)</summary>
    public static event System.Action<WhaleBot, Stock, decimal> OnWhalePumpStarted;
    /// <summary>고래 작전 종료 시 (대장 계좌, 종목)</summary>
    public static event System.Action<WhaleBot, Stock> OnWhaleOperationEnded;
    public enum Phase
    {
        Accumulation,
        Pump
    }

    [Header("팔로워")]
    [Tooltip("WhaleFollowerBot을 자동으로 찾아서 followers에 채울지 여부")]
    public bool autoDiscoverFollowers = true;
    [Tooltip("직접 지정할 팔로워 리스트(자동 탐색을 끄면 사용)")]
    public List<WhaleFollowerBot> followers = new List<WhaleFollowerBot>();
    [Header("Phase")]
    public Phase currentPhase = Phase.Accumulation;
    [Header("타겟 종목")]
    [Tooltip("매집 타겟을 고정할지 여부. false면 관심도 기반으로 자동 선택")]
    public bool lockTargetStock;
    public Stock targetStock;
    [Header("행동 간격")]
    [Tooltip("행동 간격이 너무 길면 고래가 잘 안 움직인 것처럼 보임")]
    public float actionIntervalMin = 0.6f;
    public float actionIntervalMax = 2f;
    [Header("주문 파라미터 (매집 = FakePrice 기준)")]
    [Tooltip("지정가 매수 가격 범위 = FakePrice × random(min~max). FakePrice 이용 매집")]
    public float buyPriceRatioMin = 0.97f;
    public float buyPriceRatioMax = 0.995f;
    [Tooltip("지정가 주문 수량(주). 작으면 매집 효과가 약하게 보임")]
    public int orderSizeMin = 30;
    public int orderSizeMax = 120;
    [Tooltip("한 번 행동에서 뽑을 팔로워 수. 많을수록 호가창에 주문이 많이 쌓임")]
    public int followersPerActionMin = 8;
    public int followersPerActionMax = 18;
    [Tooltip("공격적 매수(최우선매도가) 확률. 높을수록 실제 체결·가격 상승에 기여")]
    [Range(0f, 1f)]
    public float aggressiveBuyChance = 0.24f;
    [Header("주문 취소(은근한 벽 이동)")]
    [Tooltip("행동 때마다, 선택된 팔로워의 대기 주문을 이 확률로 랜덤 취소 시도")]
    [Range(0f, 1f)]
    public float tryCancelChancePerAction = 0.35f;
    [Tooltip("취소 시도 시 각 주문당 취소 확률 (작게: 스르륵)")]
    [Range(0f, 0.2f)]
    public float cancelChancePerOrder = 0.06f;
    [Header("Phase: Pump (가짜가치 조작 + 가짜 뉴스)")]
    [Tooltip("펌프 시 fakeValue = realValue × (1 + 이 비율). 예: 0.15 = 15% 상승")]
    [Range(0.05f, 0.5f)]
    public float pumpFakeValueRatio = 0.15f;
    [Tooltip("가짜가치 조작 간격(초). 한 번 조작 후 FakeNewsManager.minSecondsAfterManipulation 경과 시 가짜 뉴스 발행 가능")]
    public float pumpManipulationInterval = 120f;
    [Tooltip("조작 후 N초 지났을 때 가짜 뉴스 발행 시도 간격(초)")]
    public float pumpNewsCheckInterval = 5f;
    [Header("목표 도달 시 Real 정렬")]
    [Tooltip("FakePrice/현재가가 펌프 목표의 이 비율 이상이면 목표 도달로 보고 fakeValue = realValue 로 맞춤")]
    [Range(0.9f, 1f)]
    public float pumpTargetReachedRatio = 0.995f;
    [Tooltip("목표 도달 체크 간격(초)")]
    public float pumpMonitorInterval = 0.1f;
    private float _nextActionTime;
    private float _lastPumpManipulationTime = -999f;
    private float _lastNewsCheckTime = -999f;
    private float _lastPumpMonitorTime = -999f;
    /// <summary>이번 펌프 목표가. 이 가격에 도달하면 fakeValue를 realValue에 맞춤.</summary>
    private decimal _pumpTargetPrice = 0m;
    // 소유주 계좌는 기존 기준가 투자 전략을 사용하고, 비소유주 컨트롤러만 직접 거래를 쉬게 한다.
    protected override Stock PickStock()
    {
        if (!IsCompanyOwner)
        { return null; }

        // 자기 회사가 진짜 가치에서 크게 벗어나 있으면 그 종목부터 본다. 세력은 가치를 알고 움직이는 주체다.
        var own = OwnedCompany;
        if (own != null && own.AcceptsOrders && own.realValue > 0m && own.currentPrice > 0m)
        {
            decimal gap = System.Math.Abs(own.currentPrice / own.realValue - 1m);
            if (gap > 0.15m && Random.value < 0.6f)
            { return own; }
        }

        return base.PickStock();
    }
    protected override void ThinkForStock(Stock stock)
    {
        InvestOwnerAccount(stock);
    }

    protected override float GetThinkDelayMin() => IsCompanyOwner ? 2f : 9999f;
    protected override float GetThinkDelayMax() => IsCompanyOwner ? 10f : 9999f;
    protected override void Start()
    {
        base.Start();
        // 소유주 초기 현금을 보존한다. 비소유주 레거시 컨트롤러는 자금을 쓰지 않는다.
        if (!IsCompanyOwner)
        {
            cash = 0m;
            reservedCash = 0m;
        }

        ScheduleNextAction(SessionClock.Time);
    }

    void Update()
    {
        if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        { return; }

        if (PreDisposal)
        {
            UpdatePreDisposal();
            return;
        }

        if (!MarketDayController.CanTrade)
        { return; }

        if (StockManager.Instance == null || StockManager.Instance.stocks == null || StockManager.Instance.stocks.Count == 0)
        { return; }

        if (autoDiscoverFollowers)
        {
            RefreshFollowersIfNeeded();
        }

        float now = SessionClock.Time;
        // 0.1초마다 FakePrice 모니터링: 목표 도달 시 fakeValue = realValue 로 맞춤
        if (now - _lastPumpMonitorTime >= pumpMonitorInterval)
        {
            _lastPumpMonitorTime = now;
            MonitorPumpTarget(now);
        }

        // Accumulation은 팔로워 필요, Pump는 팔로워 없이 가짜가치 조작만
        if (currentPhase == Phase.Accumulation && (followers == null || followers.Count == 0))
        { return; }

        if (now < _nextActionTime)
        { return; }

        UpdateStrategy(now);
        ScheduleNextAction(now);
    }

    /// <summary>펌프 목표가 도달 시 fakeValue를 realValue에 맞춤.</summary>
    void MonitorPumpTarget(float now)
    {
        if (_pumpTargetPrice <= 0m)
        { return; }

        Stock stock = ResolveTargetStock();
        if (stock == null)
        { return; }

        decimal threshold = _pumpTargetPrice * (decimal)pumpTargetReachedRatio;
        if (stock.fakeValue >= threshold || stock.currentPrice >= threshold)
        {
            stock.SetFakeValue(stock.realValue);
            Debug.Log($"[세력] 목표 도달 — {stock.stockName}({stock.symbol}) fakeValue → realValue 정렬 (현재가 {stock.currentPrice:N0})");
            _pumpTargetPrice = 0m;
        }
    }

    void ScheduleNextAction(float now)
    {
        float min = Mathf.Max(0.05f, actionIntervalMin);
        float max = Mathf.Max(min, actionIntervalMax);
        _nextActionTime = now + Random.Range(min, max);
    }

    private float _nextFollowerSearch;
    private readonly List<int> followerIndices = new();
    void RefreshFollowersIfNeeded()
    {
        if (followers == null)
        {
            followers = new List<WhaleFollowerBot>();
        }

        // 한 번이라도 채워져 있으면 매 프레임 전체 탐색은 피함 (필요시 사용자가 비워서 갱신)
        if (followers.Count > 0)
        { return; }

        if (SessionClock.Time < _nextFollowerSearch)
        { return; }

        _nextFollowerSearch = SessionClock.Time + 5f;
        foreach (var follower in WhaleFollowerBot.RegisteredFollowers)
        {
            if (follower != null && follower.TryAssignCommander(this))
            {
                followers.Add(follower);
            }
        }
    }

    public void UpdateStrategy(float now)
    {
        switch (currentPhase)
        {
            case Phase.Accumulation:
                Phase1_Accumulation(now);
                break;
            case Phase.Pump:
                Phase2_Pump(now);
                break;
        }
    }

    /// <summary>
    /// Pump: fakeValue 조작 → N초 후 가짜 뉴스 발행. FakeValueAndNews.md 설계.
    /// </summary>
    void Phase2_Pump(float now)
    {
        Stock stock = ResolveTargetStock();
        if (stock == null)
        { return; }

        // 주기적으로 fakeValue 조작 + RecordManipulation (폭등 유도)
        if (now - _lastPumpManipulationTime >= pumpManipulationInterval)
        {
            decimal newFake = stock.realValue * (decimal)(1f + pumpFakeValueRatio);
            if (newFake > 0m)
            {
                stock.SetFakeValue(newFake);
                _pumpTargetPrice = newFake; // 목표 도달 모니터링용
                if (FakeNewsManager.Instance != null)
                {
                    FakeNewsManager.Instance.RecordManipulation(stock);
                }

                _lastPumpManipulationTime = now;
                Debug.Log($"[세력] 펌프 — {stock.stockName}({stock.symbol}) fakeValue 상승 목표 {newFake:N0} (real {stock.realValue:N0}, 현재가 {stock.currentPrice:N0})");
                OnWhalePumpStarted?.Invoke(this, stock, newFake);
            }
        }

        // 조작 후 N초 지났으면 가짜 뉴스 발행 시도
        if (now - _lastNewsCheckTime >= pumpNewsCheckInterval)
        {
            _lastNewsCheckTime = now;
            if (FakeNewsManager.Instance != null && FakeNewsManager.Instance.CanPublish(stock))
            {
                FakeNewsManager.Instance.TryPublishFakeNews(stock, isPump: true);
            }
        }
    }

    public void Phase1_Accumulation(float now)
    {
        Stock stock = ResolveTargetStock();
        if (stock == null)
        { return; }

        if (stock.currentPrice <= 0m || stock.orderBook == null)
        { return; }

        int minF = Mathf.Max(1, followersPerActionMin);
        int maxF = Mathf.Max(minF, followersPerActionMax);
        int take = Mathf.Min(Random.Range(minF, maxF + 1), followers.Count);
        if (take <= 0)
        { return; }

        // 팔로워 셔플 후 앞에서 take만 사용
        var indices = followerIndices;
        indices.Clear();
        for (int i = 0; i < followers.Count; i++)
        {
            indices.Add(i);
        }

        Shuffle(indices);
        // 매집은 FakePrice 기준 (고래가 FakePrice로 매집·폭등 유도)
        decimal refPrice = stock.fakeValue > 0m ? stock.fakeValue : stock.currentPrice;
        decimal current = stock.currentPrice;
        float prMin = Mathf.Clamp(buyPriceRatioMin, 0.5f, 1.2f);
        float prMax = Mathf.Clamp(buyPriceRatioMax, prMin, 1.2f);
        int osMin = Mathf.Max(1, orderSizeMin);
        int osMax = Mathf.Max(osMin, orderSizeMax);
        for (int i = 0; i < take; i++)
        {
            var follower = followers[indices[i]];
            if (follower == null || follower.Commander != this)
            { continue; }

            // 가끔 주문을 지워 벽이 고정되지 않게
            if (Random.value < tryCancelChancePerAction)
            {
                stock.CancelRandomOrdersByOwner(follower, cancelChancePerOrder);
            }

            int qtyInt = Random.Range(osMin, osMax + 1);
            decimal qty = qtyInt;
            // 가끔 공격적 매수(최우선매도가/현재가 근처)로 소량 체결시켜 티를 덜 내면서도 매집 속도를 유지
            if (Random.value < aggressiveBuyChance)
            {
                decimal px = GetAggressiveLimitPrice(stock, true);
                if (px > 0m)
                {
                    follower.BuyLimit(px, qty, 0, stock);
                }

                continue;
            }

            // 지정가: FakePrice 기준 아래쪽으로 분산 (FakePrice 이용 매집)
            float ratio = Random.Range(prMin, prMax);
            decimal price = PriceTickPolicy.FloorToTick(refPrice * (decimal)ratio);
            if (price <= 0m)
            { continue; }

            // 너무 뻔한 한 가격 몰빵 방지: 한 틱 랜덤 이동
            if (Random.value < 0.35f)
            {
                price = PriceTickPolicy.OneTickBelow(price);
            }

            if (price <= 0m)
            { continue; }

            follower.BuyLimit(price, qty, 0, stock);
        }
    }

    Stock ResolveTargetStock()
    {
        if (IsCompanyOwner)
        { return OwnedCompany != null && OwnedCompany.AcceptsOrders ? OwnedCompany : null; }

        if (lockTargetStock && targetStock != null && targetStock.AcceptsOrders)
        { return targetStock; }

        if (StockInterestManager.Instance != null)
        {
            // 매집은 눈에 덜 띄는 종목/관심 낮은 종목을 선호
            var s = StockInterestManager.Instance.PickStockByInverseWeight();
            if (s != null)
            {
                targetStock = s;
                return s;
            }
        }

        // fallback
        if (StockManager.Instance != null && StockManager.Instance.stocks != null && StockManager.Instance.stocks.Count > 0)
        {
            targetStock = StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)];
            return targetStock;
        }

        return null;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock != null && targetStock == stock)
        {
            targetStock = null;
            OnWhaleOperationEnded?.Invoke(this, stock);
        }
    }
}
