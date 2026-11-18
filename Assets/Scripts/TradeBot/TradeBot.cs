using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class TradeBot : Trader
{
    [Header("주문 규모 (비율)")]
    [Tooltip("매수 시 쓸 현금 비율 (0.1 = 10%)")]
    public decimal buyCashRatio = 0.1m;
    [Tooltip("매도 시 쓸 보유 수량 비율 (0.1 = 10%)")]
    public decimal sellHoldingRatio = 0.1m;
    [Tooltip("비율로 계산한 수량이 이보다 작으면 주문 안 함")]
    public decimal minOrderVolume = 1m;
    [Header("매도 주문 취소")]
    [Tooltip("대기 매도 중 최근가와 이 비율(0.2=20%) 이상 차이나면 취소하고 이번 턴 종료")]
    [Range(0.01f, 1f)]
    public float cancelSellOrderPercentDiff = 0.2f;
    [Header("주문 지연 (자연스러운 매매)")]
    [Tooltip("매매 판단 후 주문까지 최소 지연 시간(초)")]
    public float orderDelayMin = 1f;
    [Tooltip("매매 판단 후 주문까지 최대 지연 시간(초)")]
    public float orderDelayMax = 5f;
    [Header("목표가 기반 주문")]
    [Tooltip("현재가가 목표가에서 이 비율(0.02=2%) 이내면 주문 실행")]
    [Range(0.005f, 0.1f)]
    public float targetPriceThreshold = 0.02f;
    [Tooltip("대기 중인 목표가가 현재가와 이 비율(0.1=10%) 이상 차이나면 취소")]
    [Range(0.05f, 0.3f)]
    public float targetPriceCancelThreshold = 0.10f;
    [Header("스프레드 보호")]
    [Tooltip("매수/매도 호가 갭이 이 비율(0.03=3%) 이상이면 스프레드 안쪽에 주문")]
    [Range(0.01f, 0.1f)]
    public float maxSpreadRatio = 0.03f;
    protected class PendingOrder
    {
        public Stock stock;
        public decimal targetPrice;
        public decimal volume;
        public bool isBuy;
        public int subTickSteps;
        public float createdTime;
        public int intentVersion;
    }

    private float nextThinBookOffer;
    private readonly List<(decimal price, decimal volume)> thinBookAsks = new(3), thinBookBids = new(1);
    private readonly List<Order> thinBookOwnedOrders = new();
    protected List<PendingOrder> _pendingOrders = new List<PendingOrder>();
    [Header("심리적 저항선 반응")]
    [Tooltip("저항선 근처에서 매도벽 형성에 참여할 확률")]
    [Range(0f, 1f)]
    public float wallParticipationChance = 0.3f;
    [Tooltip("돌파 직전 매도 주문 철회 확률")]
    [Range(0f, 1f)]
    public float wallWithdrawChance = 0.5f;
    [Header("랜덤 주문 취소")]
    [Tooltip("매 Think 턴마다 이 확률로 대기 주문 랜덤 취소 시도 (0이면 비활성)")]
    [Range(0f, 1f)]
    public float chanceToTryRandomCancel = .4f;
    [Tooltip("취소 시도 시 각 주문당 취소될 확률")]
    [Range(0f, 1f)]
    public float randomOrderCancelChancePerOrder = 0.08f;
    [Header("종목 전문화")]
    [Tooltip("단일 종목 전문 트레이더 여부 (런타임 자동 설정)")]
    [SerializeField]
    protected bool _isSingleStockTrader;
    [Tooltip("전문 거래 종목 (단일 종목 트레이더인 경우)")]
    [SerializeField]
    protected Stock _specializedStock;
    /// <summary>유동성 봇용 종목별 선호도. 수익 시 누적, 퇴장 후 복귀 시 0으로 리셋.</summary>
    protected Dictionary<Stock, float> _stockPreference = new Dictionary<Stock, float>();
    /// <summary>
    /// 단일 종목 전문 트레이더가 될 확률. 봇 유형별로 오버라이드.
    /// 기본값 0 = 이 기능을 사용하지 않음 (MonitorBot, ScalperBot 등)
    /// </summary>
    protected virtual float GetSingleStockChance() => 0f;
    /// <summary>단일 종목 전문화 초기화. Start()에서 호출.</summary>
    protected void InitializeSingleStockSpecialization()
    {
        float chance = GetSingleStockChance();
        if (chance <= 0f)
        { return; }

        if (Random.value < chance)
        {
            if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
            {
                _isSingleStockTrader = true;
                _specializedStock = StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)];
            }
        }
    }

    /// <summary>단일 종목 전문 트레이더 여부</summary>
    public bool IsSingleStockTrader => _isSingleStockTrader;
    /// <summary>전문 거래 종목</summary>
    public Stock SpecializedStock => _specializedStock;
    public BotSocialState Social { get; internal set; }

    protected override void Awake()
    {
        if (MarketDayController.Instance != null && !MarketDayController.Instance.Register(this))
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        base.Awake();
        StockManager.OnStockDelisted += OnStockDelisted;
    }

    public static T Create<T>(string name, Transform parent = null)
        where T : TradeBot
    {
        if (typeof(T) == typeof(ExchangeBot) && ExchangeBot.Instance != null)
        { return ExchangeBot.Instance as T; }

        if (MarketDayController.Instance != null && MarketDayController.Instance.Bots.Count >= Mathf.Clamp(MarketDayController.Instance.maxBots, 1, 2000))
        { return null; }

        var go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go.AddComponent<T>();
    }

    public virtual void SuspendSession()
    {
        OnDisable();
    }

    public void ResumeSession()
    {
        if (_started && isActiveAndEnabled)
        {
            OnEnable();
        }
    }

    private bool _started;
    protected bool HasStarted => _started;

    private Coroutine _thinkLoop;
    protected virtual void Start()
    {
        InitializeRiskPreference(StockManager.Instance == null ? 12345 : StockManager.Instance.economy.seed);
        _started = true;
        ResumeThinking();
        StartCoroutine(FinalizeInitialAccount());
    }

    private IEnumerator FinalizeInitialAccount()
    {
        yield return null;
        if (StockManager.Instance != null && StockManager.Instance.SetupConfirmed)
        {
            StockManager.Instance.AssignInitialCash(this);
        }

        if (MarketDayController.Instance != null && MarketDayController.Instance.Ready)
        {
            BeginDailyBaseline();
        }
    }

    protected virtual void OnEnable()
    {
        if (_started)
        {
            ResumeThinking();
        }
    }

    private void ResumeThinking()
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled || _thinkLoop != null)
        { return; }

        PsychologicalLevelManager.OnWallForming += OnWallForming;
        PsychologicalLevelManager.OnBreakthroughImminent += OnBreakthroughImminent;
        _thinkLoop = StartCoroutine(ThinkLoop());
    }

    protected virtual void OnDisable()
    {
        PsychologicalLevelManager.OnWallForming -= OnWallForming;
        PsychologicalLevelManager.OnBreakthroughImminent -= OnBreakthroughImminent;
        StopAllCoroutines();
        _thinkLoop = null;
        // These are intentions only; no cash/shares have been reserved yet.
        _pendingOrders.Clear();
    }

    /// <summary>이탈 청산 전에 사고·지연 주문·거래 이벤트를 중단. 청산 SellLimit은 아직 활성 상태에서 실행.</summary>
    public virtual void PrepareForDeparture()
    {
        OnDisable();
    }

    protected override void OnDestroy()
    {
        OnDisable();
        InvestorSociety.Instance?.Forget(this);
        StockManager.OnStockDelisted -= OnStockDelisted;
        MarketDayController.Instance?.Unregister(this);
        base.OnDestroy();
    }

    /// <summary>판단·제한을 거치지 않고 계좌의 기본 주문을 그대로 낸다. 세력의 작전 도구(통정매매·허수벽·종가 관리)만 쓴다.</summary>
    public void RawBuyLimit(decimal price, decimal volume, Stock stock) { if (MarketDayController.CanTrade && isActiveAndEnabled) { base.BuyLimit(price, volume, 0, stock); } }
    public void RawSellLimit(decimal price, decimal volume, Stock stock) { if (MarketDayController.CanTrade && isActiveAndEnabled) { base.SellLimit(price, volume, 0, stock); } }

    public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
        { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
        // 장 시작 몰아주기 매수는 방장이 시킨 값 그대로 낸다. 가격 범위·위험 축소·비중 제한을 받지 않는다.
        bool rush = OpeningRush.Buying(this);
        if (!rush) { MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume); price = PacedPrice(ResolveStock(stock), true, price); }
        if (!rush && !IsExchangeParticipant && Social != null)
        {
            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, SessionClock.Time);
            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, SessionClock.Time);
            volume = decimal.Floor(volume * (decimal)(riskScale * newsScale));
            if (volume < 1m) { return; }
        }
        if (!IsExchangeParticipant && price > 0m)
        {
            // 가용 현금을 넘는 주문은 전량 거절되므로 살 수 있는 수량으로 줄인다.
            volume = System.Math.Min(volume, decimal.Floor(System.Math.Max(0m, cash - ReservedCash) / price));
            if (volume < 1m) { return; }
            // 목표 비중을 넘는 매수는 하지 않는다. 리딩방 지시 주문은 수락한 약속이므로 제외한다.
            bool commanded = rush || InvestorSociety.Instance?.Orders != null && InvestorSociety.Instance.Orders.IsSubmitting(this);
            decimal room = commanded ? decimal.MaxValue : PositionRoom(ResolveStock(stock));
            if (room != decimal.MaxValue)
            {
                volume = System.Math.Min(volume, decimal.Floor(room / price));
                if (volume < 1m) { return; }
                // 한 번의 주문으로 목표 비중을 다 채우지 않는다. 충동 상태가 아니면 목표의 30%까지만 산다.
                bool chasing = Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)) || IsChasing(ResolveStock(stock));
                decimal target = TargetPositionValue(ResolveStock(stock));
                if (!chasing && target != decimal.MaxValue)
                {
                    volume = System.Math.Min(volume, System.Math.Max(1m, decimal.Floor(target * 0.3m / price)));
                }
            }
        }
        base.BuyLimit(price, volume, subTickSteps, stock);
    }

    public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
        price = PacedPrice(ResolveStock(stock), false, price);
        if (!IsExchangeParticipant && Social != null)
        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, SessionClock.Time)); }
        base.SellLimit(price, volume, subTickSteps, stock);
    }

    /// <summary>상장폐지 시 해당 종목 키/참조 정리. 파생 봇에서 오버라이드해 자체 Dictionary 정리.</summary>
    void OnStockDelisted(Stock stock)
    {
        if (stock == null)
        { return; }

        Social?.Positions.Remove(stock);
        if (Social != null && Social.LastDropStock == stock)
        {
            Social.LastDropStock = null;
        }

        _stockPreference.Remove(stock);
        riskResponses.Remove(stock);
        intentVersions.Remove(stock);
        holdings.Remove(stock);
        avgPrice.Remove(stock);
        if (_specializedStock == stock)
        {
            _specializedStock = null;
        }

        _pendingOrders.RemoveAll(o => o.stock == stock);
        OnStockDelistedCleanup(stock);
    }

    protected virtual void OnStockDelistedCleanup(Stock stock)
    {
    }

    /// <summary>저항선 근처 벽 형성 시 매도 주문 추가</summary>
    protected virtual void OnWallForming(Stock stock, decimal resistancePrice)
    {
        if (stock == null)
        { return; }

        if (Random.value > wallParticipationChance)
        { return; }

        decimal holding = GetHolding(stock);
        if (holding <= 0m)
        { return; }

        decimal sellVol = ClampMinVolume(holding * 0.2m);
        if (sellVol <= 0m)
        { return; }

        SellLimit(resistancePrice, sellVol, 0, stock);
    }

    /// <summary>돌파 직전 매도 주문 철회</summary>
    protected virtual void OnBreakthroughImminent(Stock stock, decimal resistancePrice)
    {
        if (!CanSellStock(stock))
        { return; }

        if (stock == null)
        { return; }

        if (Random.value > wallWithdrawChance)
        { return; }

        stock.CancelSellOrdersByOwner(this, stock.currentPrice, -0.01m, 0.02m);
    }

    /// <summary>ThinkLoop 대기 구간(초). 모니터봇처럼 수가 많으면 오버라이드해 더 길게 두면 부하 감소.</summary>
    protected virtual float GetThinkDelayMin() => 2f;
    protected virtual float GetThinkDelayMax() => 10f;
    public bool CanMakeSocialPromise => !IsCompanyOwner && !IsExchangeParticipant && !(this is WhaleFollowerBot) && MarketRhythmDirector.Instance != null;
    private Stock PendingSocialStock()
    {
        if (Social == null || Social.Psychology.PromisedAction == SocialPromisedAction.None || StockManager.Instance == null)
        { return null; }
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock.symbol != Social.Psychology.PromisedSymbol || !stock.AcceptsOrders) { continue; }
            if (CanBuyStock(stock) || CanSellStock(stock)) { return stock; }
            Social.Psychology.ClearPromise("insufficient-resources"); return null;
        }
        Social.Psychology.ClearPromise("stock-unavailable"); return null;
    }
    private float lastPickHeat;
    IEnumerator ThinkLoop()
    {
        while (true)
        {
            // 방금 본 종목이 달아올라 있으면 다음 판단이 빨라진다(최대 세 배). 인기 종목에서는 거래 자체가 늘어난다.
            yield return new WaitForSeconds(Random.Range(GetThinkDelayMin(), GetThinkDelayMax()) / Mathf.Clamp(1f + lastPickHeat * .3f, 1f, 3f));
            lastPickHeat = 0f;
            if (!MarketDayController.CanTrade)
            { continue; }

            ProcessPendingOrders();
            if (chanceToTryRandomCancel > 0f && Random.value < chanceToTryRandomCancel && StockManager.Instance != null)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    stock.CancelRandomOrdersByOwner(this, randomOrderCancelChancePerOrder);
                }
            }

            Stock promised = PendingSocialStock();
            Stock pick = promised ?? PickStock();
            lastPickHeat = pick != null && StockInterestManager.Instance != null ? StockInterestManager.Instance.Heat(pick) : 0f;
            // 크게 달아오른 종목을 보면, 가진 다른 종목을 급히 팔고 뛰어드는 봇이 있다.
            if (pick != null && pick.AcceptsOrders && TryChase(pick, lastPickHeat)) { continue; }
            if (pick != null && pick.AcceptsOrders && (CanBuyStock(pick) || CanSellStock(pick)) && StockManager.Instance != null && StockManager.Instance.stocks.Contains(pick))
            {
                ObserveMarket(pick);
                if (TryPanicSell(pick))
                { continue; }

                RefreshLiquidityOrders(pick);
                if (CancelDistantSellOrdersAndEndTurnIfAny(pick))
                { continue; }

                MarketDayController.Instance?.Consider(this, pick);
                if (promised != null && MarketRhythmDirector.Instance != null && MarketRhythmDirector.Instance.TryAct(this, pick, true))
                { continue; }
                if ((WorldDramaDirector.Instance == null || !WorldDramaDirector.Instance.TryFundDecision(this, pick)) && !TryThinBookSellOffer(pick) && (MarketRhythmDirector.Instance == null || !MarketRhythmDirector.Instance.TryAct(this, pick)))
                {
                    ThinkForStock(pick);
                }
            }
        }
    }

    /// <summary>유동성 봇의 오래된 호가만 회수해 예치금과 보유량을 다시 매매에 사용한다.</summary>
    private void RefreshLiquidityOrders(Stock stock)
    {
        if (IsExchangeParticipant || IsCompanyOwner || !(UseStockPreference() || this is ScalperBot))
        { return; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        int cancelled = 0;
        foreach (var order in thinBookOwnedOrders)
        {
            if (order.purpose != OrderPurpose.Ordinary)
            { continue; }
            float lifetime = this is ScalperBot ? 20f : 60f;
            if (!order.isBuy && order.price > stock.currentPrice * 1.15m)
            {
                lifetime = 120f;
            }
            if (SessionClock.Time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
            {
                cancelled++;
                if (cancelled >= 2)
                { break; }
            }
        }
    }

    private bool TryThinBookSellOffer(Stock stock)
    {
        if (IsExchangeParticipant || !CanSellStock(stock) || SessionClock.Time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
        { return false; }
        decimal holding = GetHolding(stock);
        if (holding < minOrderVolume || stock.currentPrice < 1m)
        { return false; }
        stock.orderBook.GetTopAsks(thinBookAsks, 3);
        bool thin = thinBookAsks.Count < 3 || thinBookAsks[0].price > stock.currentPrice * 1.08m;
        if (!thin)
        { return false; }
        stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
        foreach (var order in thinBookOwnedOrders)
        {
            if (!order.isBuy)
            { return false; }
        }
        float risk = Social == null ? 0.5f : Social.Risk;
        float stress = Social == null ? 0f : Mathf.Clamp01(Social.Stress / 100f);
        if (Random.value > 0.15f + risk * 0.2f + stress * 0.1f)
        { return false; }
        stock.orderBook.GetTopBids(thinBookBids, 1);
        decimal basis = stock.currentPrice;
        if (thinBookBids.Count > 0)
        {
            basis = System.Math.Max(basis, thinBookBids[0].price);
        }
        // Most offers are nearby; a few holders test a deliberately ambitious price.
        float premium = Random.value < 0.15f + risk * 0.15f ? Random.Range(0.15f, 0.65f) : Random.Range(0.01f, 0.08f);
        premium *= 1f - stress * 0.5f;
        decimal price = PriceTickPolicy.CeilToTick(basis * (1m + (decimal)premium));
        decimal ratio = System.Math.Min(0.05m, sellHoldingRatio * (decimal)Random.Range(0.1f, 0.5f));
        decimal volume = ClampMinVolume(holding * ratio);
        if (volume < 1m)
        { return false; }
        nextThinBookOffer = SessionClock.Time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
        DelayedSellLimit(price, volume, 0, stock);
        return true;
    }

    /// <summary>대기 중인 목표가 주문 처리: 목표가 근접 시 실행, 10% 이상 차이 시 취소</summary>
    protected virtual void ProcessPendingOrders()
    {
        for (int i = _pendingOrders.Count - 1; i >= 0; i--)
        {
            var order = _pendingOrders[i];
            if (order.stock == null || !order.stock.AcceptsOrders || order.intentVersion != IntentVersion(order.stock) || order.isBuy && !CanBuyStock(order.stock) || order.targetPrice <= 0m)
            {
                _pendingOrders.RemoveAt(i);
                continue;
            }

            decimal current = order.stock.currentPrice;
            if (current <= 0m)
            { continue; }

            decimal diff = System.Math.Abs(current - order.targetPrice) / order.targetPrice;
            if (diff >= (decimal)targetPriceCancelThreshold)
            {
                _pendingOrders.RemoveAt(i);
                continue;
            }

            if (diff <= (decimal)targetPriceThreshold)
            {
                if (order.isBuy)
                {
                    DelayedBuyLimit(order.targetPrice, order.volume, order.subTickSteps, order.stock);
                }
                else
                {
                    DelayedSellLimit(order.targetPrice, order.volume, order.subTickSteps, order.stock);
                }

                _pendingOrders.RemoveAt(i);
            }
        }
    }

    /// <summary>목표가 매수 주문 등록. 현재가가 목표가 근처에 도달하면 실행됨.</summary>
    protected void RegisterTargetBuy(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanBuyStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
    }

    /// <summary>목표가 매도 주문 등록. 현재가가 목표가 근처에 도달하면 실행됨.</summary>
    protected void RegisterTargetSell(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
    {
        if (!CanSellStock(stock))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
        { return; }

        _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = SessionClock.Time, intentVersion = IntentVersion(stock) });
    }

    /// <summary>해당 종목의 대기 주문이 있는지 확인</summary>
    protected bool HasPendingOrderFor(Stock stock, bool? isBuy = null)
    {
        foreach (var order in _pendingOrders)
        {
            if (order.stock == stock)
            {
                if (!isBuy.HasValue || order.isBuy == isBuy.Value)
                { return true; }
            }
        }

        return false;
    }

    /// <summary>이번에 거래할 종목 선택. 단일 종목 전문 트레이더는 전문 종목만 반환. 유동성 봇은 선호도·뉴스관심도 반영.</summary>
    protected virtual Stock PickStock()
    {
        // 갈아타는 중에는 그 종목만 본다.
        if (chaseStock != null && SessionClock.Time < chaseUntil && chaseStock.AcceptsOrders) { return chaseStock; }

        if (_isSingleStockTrader)
        { return _specializedStock != null && _specializedStock.AcceptsOrders ? _specializedStock : null; }

        if (Social != null && Social.Psychology.HasPanic && StockManager.Instance != null)
        {
            foreach (var candidate in StockManager.Instance.stocks)
            { if (candidate.AcceptsOrders && Social.Psychology.HasPanicFor(candidate) && GetOwnedVolume(candidate) > 0m) { return candidate; } }
        }

        if (Social != null && Social.Psychology.HasFomo && StockManager.Instance != null && Random.value < ChatSocialConfig.Current.Get("fomoPickChance"))
        {
            foreach (var candidate in StockManager.Instance.stocks)
            { if (candidate.AcceptsOrders && Social.Psychology.HasFomoFor(candidate)) { return candidate; } }
        }

        if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && SessionClock.Time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
        { return Social.InfluencedStock; }

        if (StockInterestManager.Instance != null)
        { return StockInterestManager.Instance.PickStockByWeightForBot(this); }

        if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
        { return StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)]; }

        return null;
    }

    /// <summary>유동성 봇: true면 종목별 선호도·뉴스 관심도로 PickStock 가중치 적용. 선호도 높을수록 해당 종목 ThinkForStock 호출 빈도 증가.</summary>
    public virtual bool UseStockPreference() => false;
    /// <summary>종목 선호도 (수익 시 누적). 퇴장 후 복귀 시 0.</summary>
    public float GetPreference(Stock stock)
    {
        float value = stock != null && _stockPreference.TryGetValue(stock, out float preference) ? preference : 0f;
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, StockManager.Instance == null ? 100f : Mathf.Max(1f, StockManager.Instance.economy.maximumAttentionFactor));
    }

    /// <summary>퇴장 후 복귀 시 모든 종목 선호도 0으로 리셋. TradeBotManager.BotReturn에서 호출.</summary>
    public void ResetStockPreferences()
    {
        _stockPreference.Clear();
    }

    protected override void OnSellFilled(Stock stock, decimal filledVolume, decimal totalCost, decimal avgFillPrice, decimal avgCostBeforeSell)
    {
        if (!UseStockPreference() || stock == null || avgCostBeforeSell <= 0m)
        { return; }

        if (avgFillPrice <= avgCostBeforeSell)
        { return; }

        decimal profitRate = (avgFillPrice - avgCostBeforeSell) / avgCostBeforeSell;
        if (!_stockPreference.ContainsKey(stock))
        {
            _stockPreference[stock] = 0f;
        }

        _stockPreference[stock] += (float)profitRate;
    }

    /// <summary>
    /// 매매 기준가(FakePrice). StupidBot·SpreadFillerBot만 오버라이드해서 현재가 사용.
    /// 나머지 봇은 fakeValue 기준 매매. fakeValue는 realValue 쪽으로 초당 일정 비율 수렴.
    /// </summary>
    protected virtual decimal GetReferencePrice(Stock stock)
    {
        if (stock == null)
        { return 0m; }

        // 개미는 자기가 믿는 가격, 세력·거래소는 진짜 가치를 기준으로 삼는다.
        return BelievedPrice(stock);
    }

    /// <summary>
    /// RealPrice만 보다 매도 호가가 현재가에서 너무 멀어지는 것을 방지.
    /// 현재가가 Real보다 높을 때: refBasedSellPrice와 현재가 부근(1틱 아래) 중 더 높은 쪽을 사용해, 시장에 매도 호가가 나가도록 함.
    /// </summary>
    protected decimal GetEffectiveSellPrice(Stock stock, decimal refBasedSellPrice)
    {
        refBasedSellPrice = ApplyRoundQuotePreference(stock, refBasedSellPrice, false);
        if (stock == null || refBasedSellPrice <= 0m)
        { return refBasedSellPrice; }

        decimal real = GetReferencePrice(stock);
        decimal current = SeenPrice(stock);
        if (current <= 0m || current <= real)
        { return refBasedSellPrice; }

        decimal nearCurrent = PriceTickPolicy.OneTickBelow(current);
        if (nearCurrent <= 0m)
        { return refBasedSellPrice; }

        return refBasedSellPrice > nearCurrent ? refBasedSellPrice : nearCurrent;
    }

    /// <summary>
    /// RealPrice만 보다 매수 대기가 현재가에서 너무 멀어지는 것을 방지.
    /// 현재가가 Real보다 낮을 때: refBasedBuyPrice와 현재가 부근(1틱 위) 중 더 낮은 쪽을 사용해, 시장에 매수 호가가 나가도록 함.
    /// </summary>
    protected decimal GetEffectiveBuyPrice(Stock stock, decimal refBasedBuyPrice)
    {
        refBasedBuyPrice = ApplyRoundQuotePreference(stock, refBasedBuyPrice, true);
        if (stock == null)
        { return refBasedBuyPrice; }

        decimal real = GetReferencePrice(stock);
        decimal current = SeenPrice(stock);
        if (current <= 0m || real <= 0m || current >= real)
        { return refBasedBuyPrice; }

        decimal nearCurrent = PriceTickPolicy.OneTickAbove(current);
        if (nearCurrent <= 0m)
        { return refBasedBuyPrice; }

        if (refBasedBuyPrice <= 0m)
        { return nearCurrent; }

        return refBasedBuyPrice < nearCurrent ? refBasedBuyPrice : nearCurrent;
    }

    private decimal ApplyRoundQuotePreference(Stock stock, decimal price, bool buy)
    {
        if (stock == null || price <= 0m || IsExchangeParticipant || IsCompanyOwner)
        { return price; }
        // Stable investor preference; never move an aggressive exit or a specified leading-room order.
        if (CompanyBusiness.Hash("ROUND:" + InitialAccountId) % 100 >= 45 || (buy ? price >= stock.currentPrice : price <= stock.currentPrice))
        { return price; }
        return PriceTickPolicy.RoundRetailQuote(price, buy);
    }

    /// <summary>현금의 일정 비율로 매수할 수량 (가격 반영). stock 없으면 CurrentStock. referencePrice 넣으면 그 가격 기준(미입력 시 현재가). 항상 정수로 내림.</summary>
    protected decimal GetBuyVolume(decimal cashRatio, Stock stock = null, decimal? referencePrice = null)
    {
        Stock s = ResolveStock(stock);
        decimal price = referencePrice.HasValue && referencePrice.Value > 0m ? referencePrice.Value : s.currentPrice;
        // 기준가가 현재가보다 낮아도 실제 체결은 현재가 부근이므로, 의도한 현금 비율을 넘는 수량을 만들지 않는다.
        if (s.currentPrice > price)
        {
            price = s.currentPrice;
        }

        if (price <= 0m)
        { return 0m; }

        decimal available = System.Math.Max(0m, cash - reservedCash);
        return decimal.Floor((available * System.Math.Clamp(cashRatio, 0m, 1m)) / price);
    }

    /// <summary>보유 수량의 일정 비율로 매도할 수량. stock 없으면 CurrentStock. 항상 정수로 내림.</summary>
    protected decimal GetSellVolume(decimal holdingRatio, Stock stock = null)
    { return decimal.Floor(GetHolding(ResolveStock(stock)) * holdingRatio); }

    /// <summary>최소 주문 수량 체크. 정수로 내림 처리.</summary>
    protected decimal ClampMinVolume(decimal volume)
    {
        volume = decimal.Floor(volume);
        return volume >= minOrderVolume ? volume : 0m;
    }

    /// <summary>대기 매도 취소: 현재가 대비 -20%~+80% 밖이면 무조건 취소, 안이면 거리 비율로 확률 취소. 하나라도 취소되면 true(한 턴 끝).</summary>
    protected bool CancelDistantSellOrdersAndEndTurnIfAny(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m)
        { return false; }

        int n = stock.CancelSellOrdersByOwner(this, stock.currentPrice, rangeMinPct: -0.20m, rangeMaxPct: 0.80m);
        return n > 0;
    }

    /// <summary>관심도로 종목을 고른 뒤 호출됨. 이 종목에 대해 매매 로직 실행.</summary>
    protected abstract void ThinkForStock(Stock stock);
    /// <summary>
    /// 스프레드가 넓을 때 (3% 이상) 스프레드 안쪽 가격 반환.
    /// 매수 시: 최고매수가 + 1틱 ~ 중간값 사이
    /// 매도 시: 중간값 ~ 최저매도가 - 1틱 사이
    /// 스프레드가 좁으면 null 반환 (원래 로직 사용)
    /// </summary>
    protected decimal? GetSpreadSafePrice(Stock stock, bool isBuy)
    {
        if (stock == null)
        { return null; }

        var book = stock.orderBook;
        if (book == null)
        { return null; }

        var bids = book.GetTopBids();
        var asks = book.GetTopAsks(1);
        if (bids.Count == 0 || asks.Count == 0)
        { return null; }

        decimal bestBid = bids[0].price;
        decimal bestAsk = asks[0].price;
        if (bestBid <= 0m || bestAsk <= 0m || bestAsk <= bestBid)
        { return null; }

        decimal spread = bestAsk - bestBid;
        decimal midPrice = (bestBid + bestAsk) / 2m;
        decimal spreadRatio = spread / midPrice;
        if (spreadRatio < (decimal)maxSpreadRatio)
        { return null; }

        decimal oneTick = PriceTickPolicy.GetTickSize(midPrice);
        if (isBuy)
        {
            decimal minPrice = bestBid + oneTick;
            decimal maxPrice = midPrice;
            if (minPrice >= maxPrice)
            { return PriceTickPolicy.FloorToTick(midPrice); }

            decimal price = minPrice + (maxPrice - minPrice) * (decimal)Random.Range(0.3f, 0.7f);
            return PriceTickPolicy.FloorToTick(price);
        }
        else
        {
            decimal minPrice = midPrice;
            decimal maxPrice = bestAsk - oneTick;
            if (minPrice >= maxPrice)
            { return PriceTickPolicy.CeilToTick(midPrice); }

            decimal price = minPrice + (maxPrice - minPrice) * (decimal)Random.Range(0.3f, 0.7f);
            return PriceTickPolicy.CeilToTick(price);
        }
    }

    /// <summary>즉시 체결에 가까운 지정가 (시장가 대체). 매수=최우선매도가, 매도=최우선매수가. 호가 없으면 현재가 ± 1틱.</summary>
    protected decimal GetAggressiveLimitPrice(Stock stock, bool isBuy)
    {
        if (stock == null)
        { return 0m; }

        var book = stock.orderBook;
        decimal tick = PriceTickPolicy.GetTickSize(stock.currentPrice);
        if (book == null)
        { return isBuy ? stock.currentPrice + tick : stock.currentPrice - tick; }

        if (isBuy)
        {
            var asks = book.GetTopAsks(1);
            if (asks.Count > 0 && asks[0].price > 0m)
            { return asks[0].price; }

            return PriceTickPolicy.CeilToTick(stock.currentPrice + (tick > 0m ? tick : 0.01m));
        }
        else
        {
            var bids = book.GetTopBids();
            if (bids.Count > 0 && bids[0].price > 0m)
            { return bids[0].price; }

            return PriceTickPolicy.FloorToTick(stock.currentPrice - (tick > 0m ? tick : 0.01m));
        }
    }

    /// <summary>
    /// 스프레드가 넓은지 확인 (3% 이상)
    /// </summary>
    protected bool IsSpreadWide(Stock stock)
    {
        if (stock == null)
        { return false; }

        var book = stock.orderBook;
        if (book == null)
        { return false; }

        var bids = book.GetTopBids();
        var asks = book.GetTopAsks(1);
        if (bids.Count == 0 || asks.Count == 0)
        { return false; }

        decimal bestBid = bids[0].price;
        decimal bestAsk = asks[0].price;
        if (bestBid <= 0m || bestAsk <= 0m || bestAsk <= bestBid)
        { return false; }

        decimal spread = bestAsk - bestBid;
        decimal midPrice = (bestBid + bestAsk) / 2m;
        decimal spreadRatio = spread / midPrice;
        return spreadRatio >= (decimal)maxSpreadRatio;
    }

    /// <summary>지연 후 지정가 매수. 가격과 수량을 미리 정해두고 0.5~3초 후 주문.</summary>
    protected void DelayedBuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        StartCoroutine(DelayedBuyLimitCoroutine(price, volume, subTickSteps, stock));
    }

    private IEnumerator DelayedBuyLimitCoroutine(decimal price, decimal volume, int subTickSteps, Stock stock)
    {
        Stock delayedStock = ResolveStock(stock);
        int generation = IntentVersion(delayedStock);
        stock = delayedStock;
        int session = MarketDayController.Session;
        yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
        if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
        { yield break; }

        BuyLimit(price, volume, subTickSteps, stock);
    }

    /// <summary>지연 후 지정가 매도. 가격과 수량을 미리 정해두고 0.5~3초 후 주문.</summary>
    protected void DelayedSellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        StartCoroutine(DelayedSellLimitCoroutine(price, volume, subTickSteps, stock));
    }

    private IEnumerator DelayedSellLimitCoroutine(decimal price, decimal volume, int subTickSteps, Stock stock)
    {
        Stock delayedStock = ResolveStock(stock);
        int generation = IntentVersion(delayedStock);
        stock = delayedStock;
        int session = MarketDayController.Session;
        yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
        if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
        { yield break; }

        SellLimit(price, volume, subTickSteps, stock);
    }

    /// <summary>지연 후 공격적 지정가 매수. 스프레드가 넓으면 스프레드 안쪽 가격, 아니면 최우선 매도가에 지정가 주문.</summary>
    protected void DelayedBuyMarket(decimal volume, Stock stock = null)
    {
        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        StartCoroutine(DelayedBuyMarketCoroutine(volume, stock));
    }

    private IEnumerator DelayedBuyMarketCoroutine(decimal volume, Stock stock)
    {
        Stock delayedStock = ResolveStock(stock);
        int generation = IntentVersion(delayedStock);
        stock = delayedStock;
        int session = MarketDayController.Session;
        yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
        if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
        { yield break; }

        Stock s = ResolveStock(stock);
        decimal price = GetSpreadSafePrice(s, true) ?? GetAggressiveLimitPrice(s, true);
        if (price > 0m)
        {
            BuyLimit(price, volume, 0, s);
        }
    }

    /// <summary>지연 후 공격적 지정가 매도. 스프레드가 넓으면 스프레드 안쪽 가격, 아니면 최우선 매수가에 지정가 주문.</summary>
    protected void DelayedSellMarket(decimal volume, Stock stock = null)
    {
        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        StartCoroutine(DelayedSellMarketCoroutine(volume, stock));
    }

    private IEnumerator DelayedSellMarketCoroutine(decimal volume, Stock stock)
    {
        Stock delayedStock = ResolveStock(stock);
        int generation = IntentVersion(delayedStock);
        stock = delayedStock;
        int session = MarketDayController.Session;
        yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
        if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
        { yield break; }

        Stock s = ResolveStock(stock);
        decimal price = GetSpreadSafePrice(s, false) ?? GetAggressiveLimitPrice(s, false);
        if (price > 0m)
        {
            SellLimit(price, volume, 0, s);
        }
    }
}
