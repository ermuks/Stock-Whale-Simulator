using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>모든 종목을 담당하는 단일 거래소 계좌. 기준가와 주문 중심가/체결가는 서로 독립.</summary>
public partial class ExchangeBot : TradeBot
{
    public override bool IsExchangeParticipant => true;
    public static ExchangeBot Instance { get; private set; }

    [Header("새 게임 자산 (돈, 직렬화 가능한 설정)")]
    public bool useMarketCapCash = true;
    [Range(0f, 1f)]
    public float initialCashMarketCapRatio = 0.2f;
    [Min(0)]
    public long initialCashAmount = 25_000_000_000L;
    [Header("기준가 전용 난수 / 과거 봉 전용 난수")]
    public int referenceSeed = 12345, historySeed = 24680;
    [Min(0.1f)]
    public float referenceInterval = 1f;
    [Tooltip("퍼센트 단위. 0.001% = 비율 0.00001")]
    public float normalPercentMin = 0.001f, normalPercentMax = 0.005f;
    public float shockProbabilityPercent = 0.01f, shockPercentMin = 0.05f, shockPercentMax = 2f;
    [Min(1f)]
    public float shockSmallBias = 3f;
    [Range(0f, 1f)]
    public float marketCorrectionFraction = 0.02f;
    public float marketCorrectionMaxPercent = 0.1f;
    [Header("뉴스 강도 / 기준가 영향 (퍼센트)")]
    public float weakNewsCutoff = 0.05f, mediumNewsCutoff = 0.1f;
    public float weakNewsPercent = 0.5f, mediumNewsPercent = 1f, strongNewsPercent = 2f;
    [Header("방어 (현실 초 / 돈 / 비율)")]
    [Min(1f)]
    public float reevaluationSeconds = 60f;
    [Min(0)]
    public long buyBudgetAmount = 1_000_000_000L, sellBudgetAmount = 1_000_000_000L;
    [Range(0f, 1f)]
    public float volumeFraction = 0.5f;
    public float minimumDefenseDistance = 0.1f, levelDistance = 0.02f;
    public float riskEnter = 0.3f, riskExit = 0.2f;
    [Min(1)]
    public int riskExitCandles = 5;
    public float priceRepositionRatio = 0.005f, quantityAdjustRatio = 0.2f;
    [Header("자산 보호")]
    public float stockCashReserveRatio = 0.05f, totalCashReserveRatio = 0.3f, stockSellReserveRatio = 0.05f;
    public float inventoryFloorRatio = 0.2f, initialListingSupplyRatio = 0.1f;
    public float pressureThreshold = 0.05f, protectionSupplyRatio = 0.5f, protectionDistance = 0.05f;
    [Header("프레임 예산")]
    [Min(1)]
    public int stocksPerFrame = 1;
    [Min(6)]
    public int ordersPerFrame = 8;
    public bool AssetsInitialized { get; private set; }
    public OrderPurpose SubmittedPurpose { get; private set; } = OrderPurpose.ExchangeDefense;
    public int SubmittedSlot { get; private set; } = -1;

    private readonly List<StockState> states = new();
    private readonly Dictionary<Stock, StockState> byStock = new();
    private readonly Dictionary<Stock, (long id, decimal multiplier)> earlyNews = new();
    private int cursor, openingSession = -1, ordersLeft = int.MaxValue;
    private StockState submittingState;
    private int submittingInitialSlot = -1;
    private bool enforcing;
    private static readonly ProfilerMarker ReferenceMarker = new("DonDon.ExchangeReference"), DefenseMarker = new("DonDon.ExchangeDefense"), AssetMarker = new("DonDon.ExchangeAssets");
    public sealed class StockState
    {
        public Stock Stock;
        public decimal Reference, Middle, Deviation, OrganicAverage, Risk, InitialInventory;
        public bool NewListing, DefenseStarted, Risky, RecoveryNeedsTrade, NeedsOpening, BuyProtected, SellProtected;
        public int SafeCandles, RecoveryStage = 3, BuyClear, SellClear, ListingDay, CompletedCount;
        public float NextReference, NextRegular, NextGradeEvaluation;
        public int StableGradeCandles, GraceCandles;
        public bool RecentActualTrade;
        public long LastNewsId;
        public decimal PendingNews = 1m, TargetBuy, TargetSell, BuyWindowBudget;
        public readonly Order[] Bids = new Order[3], Asks = new Order[3], InitialOrders = new Order[50];
        public readonly decimal[] InitialPrices = new decimal[50], InitialRemaining = new decimal[50];
        public readonly decimal[] BuyPrices = new decimal[3], SellPrices = new decimal[3], BuyVolumes = new decimal[3], SellVolumes = new decimal[3];
        internal System.Random Random;
        internal readonly PressureBucket[] Pressure = new PressureBucket[61];
        internal string LimitReason = "준비";
    }

    internal struct PressureBucket
    {
        public bool Valid;
        public int Second;
        public decimal StartInventory, StartBudget, NetSold, NetSpend;
    }

    [Serializable]
    public class StockDebug
    {
        public string symbol, state, limitReason;
        public bool buyProtected, sellProtected;
        public int recoveryStep;
        public double reference, current, middle, upper, lower, risk, nextReevaluation, targetBuy, targetSell;
        public double reservedBuy, reservedSell, availableShares, availableCash, totalShares, initialUnsold;
        public int completedCandles, listingDay;
        public string grade;
        public bool gradePending;
        public double nextGradeEvaluation, haltRemaining;
        public float cautiousSelection, neutralSelection, speculativeSelection;
    }

    [Header("종목별 실행 정보 (1초 간격)")]
    public List<StockDebug> debugStocks = new();
    private float nextDebug;
    private readonly List<(decimal price, decimal volume)> sizingLevels = new(10);
    private readonly List<decimal> externalLevelSizes = new(20);
    public StockState GetState(Stock stock) => stock != null && byStock.TryGetValue(stock, out var state) ? state : null;
    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        base.Awake();
        CandleHistoryManager.OnStockCandleClosed += OnCandleCompleted;
        StockManager.OnStockListed += OnListed;
        NewsManager.OnMarketNews += OnNews;
    }

    protected override void Start()
    {
        base.Start();
        if (MarketDayController.Instance == null)
        {
            StartCoroutine(InitializeStandalone());
        }
    }

    private IEnumerator InitializeStandalone()
    {
        yield return null;
        yield return null;
        while (MarketDayController.Initializers > 0)
        {
            yield return null;
        }

        InitializeAssets();
    }

    protected override Stock PickStock() => null;
    protected override void ThinkForStock(Stock stock)
    {
    }

    protected override float GetThinkDelayMin() => 9999f;
    protected override float GetThinkDelayMax() => 9999f;
    protected override void OnWallForming(Stock stock, decimal price)
    {
    }

    protected override void OnBreakthroughImminent(Stock stock, decimal price)
    {
    }

    public void InitializeAssets()
    {
        if (AssetsInitialized || StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        { return; }

        if (StockManager.Instance.useInitialEconomy && !StockManager.Instance.SetupInProgress && !StockManager.Instance.EconomyInitialized)
        { return; }

        ExchangeWarehouse.Instance.InitializeFromStocks(StockManager.Instance.stocks);
        decimal marketCap = 0m;
        foreach (var stock in StockManager.Instance.stocks)
        {
            // 시가총액 비례 현금은 종목마다 상한까지만 센다(기존 종목은 모두 상한 아래라 전과 같다).
            marketCap += System.Math.Min(stock.issuedShares * stock.currentPrice, System.Math.Max(1L, StockManager.Instance.economy.exchangeCashCapPerStock));
            decimal remaining = ExchangeWarehouse.Instance.TakeRemaining(stock);
            if (remaining > 0m)
            {
                GrantShares(stock, remaining, stock.currentPrice);
            }

            CandleHistoryManager.Instance?.SeedHistory(stock);
            Register(stock, stock.IsNewListing);
        }

        cash = useMarketCapCash ? marketCap * (decimal)Mathf.Clamp01(initialCashMarketCapRatio) : Math.Max(0L, initialCashAmount);
        AssetsInitialized = true;
        ExchangeWarehouse.Instance.Bind(this);
        BeginDailyBaseline();
    }

    private void Register(Stock stock, bool listing)
    {
        stock.OnMatchedTrade -= OnMatched; stock.OnMatchedTrade += OnMatched;
        if (byStock.ContainsKey(stock))
        { return; }

        var state = new StockState
        {
            Stock = stock,
            Reference = stock.currentPrice,
            InitialInventory = GetOwnedVolume(stock),
            NewListing = listing,
            ListingDay = MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day,
            NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval),
            NeedsOpening = true,
            Random = new System.Random(CandleHistoryManager.StableSeed(referenceSeed, stock.symbol))
        };
        states.Add(state);
        byStock.Add(stock, state);
        if (earlyNews.TryGetValue(stock, out var news))
        {
            state.LastNewsId = news.id;
            state.PendingNews = news.multiplier;
            earlyNews.Remove(stock);
        }

        debugStocks.Add(new StockDebug { symbol = stock.symbol });
        if (listing)
        {
            decimal perLevel = decimal.Floor(state.InitialInventory * Ratio(initialListingSupplyRatio) / 50m);
            for (int i = 0; i < 50; i++)
            {
                decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + 9m * i / 49m));
                int destination = i;
                for (int n = 0; n < i; n++)
                {
                    if (state.InitialPrices[n] == price)
                    {
                        destination = n;
                        break;
                    }
                }

                state.InitialPrices[destination] = price;
                state.InitialRemaining[destination] += perLevel;
            }
        }

        RefreshBand(state);
        InitializeGrade(state);
    }

    private void OnListed(Stock stock)
    {
        if (!AssetsInitialized)
        { return; }

        Register(stock, true);
    }

    public void AcceptNewInventory(Stock stock, decimal amount)
    {
        GrantShares(stock, amount, stock.currentPrice);
    }

    public bool TransferInventory(Trader trader, Stock stock, decimal volume, decimal basis)
    {
        volume = decimal.Floor(volume);
        if (trader == null || trader == this || volume <= 0m || GetHolding(stock) < volume)
        { return false; }

        var state = GetState(stock);
        if (state != null && GetHolding(stock) - volume < state.InitialInventory * Ratio(inventoryFloorRatio))
        { return false; }

        ForceRemoveHolding(stock, volume);
        trader.GrantShares(stock, volume, basis);
        OnTradeSettled(stock);
        return true;
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
        { return; }
        TickFutureNews();
        TickRiskGrades();
        if (AssetsInitialized && SessionClock.Time >= nextDebug)
        {
            UpdateDebug();
            nextDebug = SessionClock.Time + 1f;
        }

        if (!AssetsInitialized || !MarketDayController.CanTrade || states.Count == 0)
        { return; }

        DoInitialPlacement();
        ordersLeft = Mathf.Max(6, ordersPerFrame);
        int count = Mathf.Min(states.Count, Mathf.Max(1, stocksPerFrame), Mathf.Max(1, ordersLeft / 6));
        for (int i = 0; i < count; i++)
        {
            if (cursor >= states.Count)
            {
                cursor = 0;
            }

            var state = states[cursor++];
            if (state.Stock.IsDelisting || state.Stock.IsTradingHalted)
            {
                CancelState(state);
                continue;
            }

            float now = SessionClock.Time;
            if (now >= state.NextReference)
            {
                using (ReferenceMarker.Auto())
                {
                    AdvanceReference(state);
                    TouchPressure(state, now);
                }

                state.NextReference = now + Mathf.Max(0.1f, referenceInterval);
            }

            if (state.PendingNews != 1m)
            {
                state.Reference = Positive(state.Reference * state.PendingNews);
                state.PendingNews = 1m;
            }

            if (state.NewListing && state.CompletedCount < 60)
            {
                if (state.NeedsOpening)
                {
                    state.NeedsOpening = !PlaceInitial(state);
                }

                continue;
            }

            if (state.NeedsOpening || now >= state.NextRegular)
            {
                using (DefenseMarker.Auto())
                {
                    Reevaluate(state.Stock, now);
                }
            }
        }
    }

    public void DoInitialPlacement()
    {
        if (!AssetsInitialized || !MarketDayController.CanTrade || openingSession == MarketDayController.Session)
        { return; }

        openingSession = MarketDayController.Session;
        foreach (var state in states)
        {
            state.NeedsOpening = true;
            state.NextReference = SessionClock.Time + Mathf.Max(0.1f, referenceInterval);
        }
    }

    public void AdvanceReference(StockState state)
    {
        if (!MarketDayController.CanTrade)
        { return; }

        bool shock = state.Random.NextDouble() < Math.Max(0f, shockProbabilityPercent) / 100.0;
        double unit = state.Random.NextDouble();
        double percent = shock ? Math.Max(0f, shockPercentMin) + (Math.Max(shockPercentMin, shockPercentMax) - Math.Max(0f, shockPercentMin)) * Math.Pow(unit, Math.Max(1f, shockSmallBias)) : Math.Max(0f, normalPercentMin) + (Math.Max(normalPercentMin, normalPercentMax) - Math.Max(0f, normalPercentMin)) * unit;
        decimal signedRatio = (decimal)(percent / 100.0) * (state.Random.NextDouble() < 0.5 ? -1m : 1m);
        state.Reference = Positive(state.Reference * (1m + signedRatio));
    }

    private static decimal Positive(decimal value) => Math.Max(0.00000001m, value);
    private static decimal Ratio(float value) => (decimal)Mathf.Clamp01(value);
    private void RefreshBand(StockState state)
    {
        if (CandleHistoryManager.Instance == null || !CandleHistoryManager.Instance.TryGetBand(state.Stock, out decimal middle, out decimal deviation, out decimal organic))
        { return; }

        state.Middle = middle;
        state.Deviation = deviation;
        state.OrganicAverage = organic;
        state.Risk = 2m * deviation / middle;
    }

    private void OnCandleCompleted(Stock stock, CandleData candle, int count)
    {
        var state = GetState(stock);
        if (state == null)
        { return; }

        bool previouslyRisky = state.Risky;
        state.CompletedCount = count;
        RefreshBand(state);
        decimal correction = (state.Middle - state.Reference) * Ratio(marketCorrectionFraction);
        if (state.Middle > 0m)
        {
            decimal max = state.Reference * Math.Max(0m, (decimal)marketCorrectionMaxPercent / 100m);
            state.Reference = Positive(state.Reference + Math.Max(-max, Math.Min(max, correction)));
        }

        state.RecentActualTrade = false;
        var completed = CandleHistoryManager.Instance.Completed(stock);
        for (int i = Math.Max(0, completed.Count - 5); i < completed.Count; i++)
        {
            if (!completed[i].historical && completed[i].volume > 0f)
            {
                state.RecentActualTrade = true;
            }
        }

        RecordStableCandle(state);
        if (state.RecoveryNeedsTrade && !state.Risky && state.RecentActualTrade)
        {
            state.RecoveryNeedsTrade = false;
        }

        if (state.NewListing && count == 60)
        {
            CancelInitial(state);
            state.NeedsOpening = false;
            state.NextRegular = SessionClock.Time;
        }

        if (!previouslyRisky && state.Risky)
        {
            CancelState(state);
        }
    }

    private void OnNews(Stock stock, long id, float criticalValue)
    {
        var state = GetState(stock);
        if (stock == null || float.IsNaN(criticalValue) || float.IsInfinity(criticalValue))
        { return; }

        if (state != null && id == state.LastNewsId)
        { return; }

        if (state == null && earlyNews.TryGetValue(stock, out var prior) && id <= prior.id)
        { return; }

        if (state != null)
        {
            state.LastNewsId = id;
        }

        float magnitude = Math.Abs(criticalValue - 1f);
        if (magnitude <= 0f)
        { return; }

        float percent = magnitude < weakNewsCutoff ? weakNewsPercent : magnitude < mediumNewsCutoff ? mediumNewsPercent : strongNewsPercent;
        decimal multiplier = Positive(1m + (decimal)Math.Max(0f, percent) / 100m * (criticalValue > 1f ? 1m : -1m));
        if (state == null)
        {
            earlyNews.TryGetValue(stock, out var queued);
            earlyNews[stock] = (id, (queued.multiplier > 0m ? queued.multiplier : 1m) * multiplier);
            return;
        }

        if (MarketDayController.CanTrade)
        {
            state.Reference = Positive(state.Reference * multiplier);
        }
        else
        {
            state.PendingNews = Positive(state.PendingNews * multiplier);
        }
    }

    private void OnMatched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool bought = ReferenceEquals(buyer, this), sold = ReferenceEquals(seller, this);
        if (!bought && !sold)
        { return; }

        var state = GetState(stock);
        if (state == null)
        { return; }

        bool maker = resting != null && ReferenceEquals(resting.owner, this);
        decimal beforeInventory = GetOwnedVolume(stock) + (maker ? sold ? volume : -volume : 0m);
        int index = TouchPressure(state, SessionClock.Time, beforeInventory);
        var bucket = state.Pressure[index];
        bucket.NetSold += sold ? volume : -volume;
        bucket.NetSpend += bought ? price * volume : -price * volume;
        state.Pressure[index] = bucket;
        if (sold)
        {
            int slot = resting != null && ReferenceEquals(resting.owner, this) && resting.purpose == OrderPurpose.ExchangeInitialSupply ? resting.purposeSlot : submittingState == state ? submittingInitialSlot : -1;
            if (slot >= 0 && slot < 50)
            {
                state.InitialRemaining[slot] = Math.Max(0m, state.InitialRemaining[slot] - volume);
            }
        }
    }

    private int TouchPressure(StockState state, float now, decimal? beforeInventory = null)
    {
        int second = Math.Max(0, (int)now), index = second % 61;
        if (!state.Pressure[index].Valid || state.Pressure[index].Second != second)
        {
            state.Pressure[index] = new PressureBucket
            {
                Valid = true,
                Second = second,
                StartInventory = beforeInventory ?? GetOwnedVolume(state.Stock),
                StartBudget = state.BuyWindowBudget > 0m ? state.BuyWindowBudget : Math.Min(Math.Max(0L, buyBudgetAmount), cash * Ratio(stockCashReserveRatio))
            };
        }

        return index;
    }

    private void EvaluatePressure(StockState state, float now)
    {
        TouchPressure(state, now);
        decimal netSold = 0m, netSpend = 0m, initialInventory = 0m, initialBudget = 0m;
        int earliest = int.MaxValue;
        for (int i = 0; i < state.Pressure.Length; i++)
        {
            var bucket = state.Pressure[i];
            if (!bucket.Valid || bucket.Second <= (int)now - 60 || bucket.Second > (int)now)
            { continue; }

            netSold += bucket.NetSold;
            netSpend += bucket.NetSpend;
            if (bucket.Second < earliest)
            {
                earliest = bucket.Second;
                initialInventory = bucket.StartInventory;
                initialBudget = bucket.StartBudget;
            }
        }

        bool sell = initialInventory > 0m && netSold >= initialInventory * Ratio(pressureThreshold);
        bool buy = initialBudget > 0m && netSpend >= initialBudget * Ratio(pressureThreshold);
        if (sell)
        {
            state.SellProtected = true;
            state.SellClear = 0;
        }
        else if (state.SellProtected && ++state.SellClear >= 2)
        {
            state.SellProtected = false;
            state.SellClear = 0;
        }

        if (buy)
        {
            state.BuyProtected = true;
            state.BuyClear = 0;
        }
        else if (state.BuyProtected && ++state.BuyClear >= 2)
        {
            state.BuyProtected = false;
            state.BuyClear = 0;
        }
    }

    public decimal ReservedBuy(StockState state)
    {
        decimal total = 0m;
        foreach (var order in state.Bids)
        {
            if (state.Stock.orderBook.IsActive(order))
            {
                total += order.price * order.volume;
            }
        }

        return total;
    }

    public decimal ReservedSell(StockState state, bool initial = false)
    {
        decimal total = 0m;
        foreach (var order in initial ? state.InitialOrders : state.Asks)
        {
            if (state.Stock.orderBook.IsActive(order))
            {
                total += order.volume;
            }
        }

        return total;
    }

    private decimal SellAllowance(StockState state, bool initial)
    {
        decimal owned = GetOwnedVolume(state.Stock), reserved = owned - GetHolding(state.Stock);
        decimal cap = owned * Ratio(initial ? initialListingSupplyRatio : stockSellReserveRatio);
        return decimal.Floor(Math.Max(0m, Math.Min(cap - reserved, owned - state.InitialInventory * Ratio(inventoryFloorRatio) - reserved)));
    }

    private decimal BuyAllowance(StockState state)
    { return Math.Max(0m, Math.Min(cash - reservedCash, Math.Min(cash * Ratio(stockCashReserveRatio) - ReservedBuy(state), cash * Ratio(totalCashReserveRatio) - reservedCash))); }

    public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        var state = GetState(stock);
        if (!AssetsInitialized || state == null || submittingState != state || state.Risky || (state.NewListing && state.CompletedCount < 60) || price <= 0m)
        { return; }

        price = PriceTickPolicy.FloorToTick(price);
        if (price <= 0m)
        { return; }

        volume = Math.Min(decimal.Floor(volume), decimal.Floor(BuyAllowance(state) / price));
        if (volume <= 0m)
        { return; }

        base.BuyLimit(price, volume, 0, stock);
        if (submittingState == state && SubmittedSlot >= 0 && SubmittedSlot < 3)
        {
            state.Bids[SubmittedSlot] = LastRestingOrder;
        }

        EnforceAssetLimits();
    }

    public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        var state = GetState(stock);
        if (!AssetsInitialized || state == null || submittingState != state || price <= 0m || !stock.AcceptsOrders || state.Risky && SubmittedPurpose != OrderPurpose.ExchangeInitialSupply)
        { return; }

        bool initial = SubmittedPurpose == OrderPurpose.ExchangeInitialSupply;
        if (!initial && state.NewListing && state.CompletedCount < 60)
        { return; }

        volume = Math.Min(decimal.Floor(volume), SellAllowance(state, initial));
        if (volume <= 0m)
        { return; }

        base.SellLimit(PriceTickPolicy.CeilToTick(price), volume, 0, stock);
        if (submittingState == state && SubmittedSlot >= 0)
        {
            if (initial && SubmittedSlot < 50)
            {
                state.InitialOrders[SubmittedSlot] = LastRestingOrder;
            }
            else if (SubmittedSlot < 3)
            {
                state.Asks[SubmittedSlot] = LastRestingOrder;
            }
        }

        EnforceAssetLimits();
    }

    private bool PlaceInitial(StockState state)
    {
        if (!state.Stock.AcceptsOrders)
        { return true; }

        for (int i = 0; i < 50; i++)
        {
            if (state.InitialRemaining[i] <= 0m || state.Stock.orderBook.IsActive(state.InitialOrders[i]))
            { continue; }

            if (ordersLeft <= 0)
            { return false; }

            decimal canPlace = Math.Min(state.InitialRemaining[i], SellAllowance(state, true));
            if (canPlace <= 0m)
            {
                state.LimitReason = "초기 공급 자산 한도";
                return true;
            }

            submittingState = state;
            submittingInitialSlot = i;
            SubmittedPurpose = OrderPurpose.ExchangeInitialSupply;
            SubmittedSlot = i;
            LastRestingOrder = null;
            SellLimit(state.InitialPrices[i], canPlace, 0, state.Stock);
            state.InitialOrders[i] = LastRestingOrder;
            submittingState = null;
            submittingInitialSlot = -1;
            SubmittedPurpose = OrderPurpose.ExchangeDefense;
            SubmittedSlot = -1;
            ordersLeft--;
        }

        state.LimitReason = "60봉 이전 초기 공급";
        return true;
    }

    public void Reevaluate(Stock stock, float now)
    {
        var state = GetState(stock);
        EvaluateGrade(stock, now);
        if (state == null || !AssetsInitialized || !MarketDayController.CanTrade || !stock.AcceptsOrders)
        { return; }

        if (!state.NeedsOpening && now < state.NextRegular)
        { return; }

        state.NextRegular = now + Mathf.Max(1f, reevaluationSeconds);
        state.NeedsOpening = false;
        EvaluatePressure(state, now);
        if (state.Risky || state.Middle <= 0m || (state.NewListing && (state.CompletedCount < 60 || !state.DefenseStarted && !state.RecentActualTrade)) || state.RecoveryNeedsTrade || state.RecoveryStage < 3 && !state.RecentActualTrade)
        {
            CancelDefense(state);
            state.LimitReason = state.Risky ? "위험종목" : "완료 봉/실제 거래 대기";
            return;
        }

        if (state.RecoveryStage < 3)
        {
            state.RecoveryStage++;
        }

        state.DefenseStarted = true;
        decimal recovery = state.RecoveryStage == 1 ? 0.25m : state.RecoveryStage == 2 ? 0.5m : 1m;
        decimal distance = Math.Max(2m * state.Deviation / state.Middle, Ratio(minimumDefenseDistance));
        decimal volumeTarget = state.OrganicAverage * Ratio(volumeFraction);
        decimal levelLimit = GetDefenseLevelLimit(state);
        decimal target = Math.Min(volumeTarget, levelLimit * 2m);
        decimal buy = decimal.Floor(Math.Min(Math.Max(0L, buyBudgetAmount) / state.Middle, target));
        decimal sell = decimal.Floor(Math.Min(Math.Max(0L, sellBudgetAmount) / state.Middle, target));
        // 거래소는 진짜 가치를 안다. 방어 호가의 중심을 체결가 평균에서 가치 쪽으로 조금만 옮긴다(평균의 ±10% 이내).
        // 물량 한도는 그대로이므로 벽으로 가격을 누르지 않는다.
        decimal center = state.Middle;
        if (stock.realValue > 0m)
        {
            decimal shift = (stock.realValue - state.Middle) * 0.25m, limit = state.Middle * 0.1m;
            center = state.Middle + Math.Max(-limit, Math.Min(limit, shift));
        }

        BuildLevels(center, distance + (state.BuyProtected ? Ratio(protectionDistance) : 0m), buy, true, state.BuyPrices, state.BuyVolumes);
        BuildLevels(center, distance + (state.SellProtected ? Ratio(protectionDistance) : 0m), sell, false, state.SellPrices, state.SellVolumes);
        for (int i = 0; i < 3; i++)
        {
            state.BuyVolumes[i] = Math.Min(state.BuyVolumes[i], levelLimit);
            state.SellVolumes[i] = Math.Min(state.SellVolumes[i], levelLimit);
        }
        decimal owned = GetOwnedVolume(stock);
        decimal maxSell = decimal.Floor(Math.Max(0m, Math.Min(owned * Ratio(stockSellReserveRatio), owned - state.InitialInventory * Ratio(inventoryFloorRatio))));
        LimitVolumes(state.SellVolumes, maxSell);
        decimal ownReserved = ReservedBuy(state);
        decimal targetCap = Math.Min(cash * Ratio(stockCashReserveRatio), cash * Ratio(totalCashReserveRatio) - (reservedCash - ownReserved));
        targetCap = Math.Max(0m, Math.Min(targetCap, cash - (reservedCash - ownReserved)));
        LimitCost(state.BuyPrices, state.BuyVolumes, targetCap);
        // 실제 자산 한도 내 공급을 줄인다. 큰 예산에서도 25/50%와 보호 50%가 사라지지 않게 한다.
        LimitVolumes(state.BuyVolumes, decimal.Floor(Sum(state.BuyVolumes) * recovery * (state.BuyProtected ? Ratio(protectionSupplyRatio) : 1m)));
        LimitVolumes(state.SellVolumes, decimal.Floor(Sum(state.SellVolumes) * recovery * (state.SellProtected ? Ratio(protectionSupplyRatio) : 1m)));
        state.TargetBuy = Sum(state.BuyVolumes);
        state.TargetSell = Sum(state.SellVolumes);
        state.BuyWindowBudget = Cost(state.BuyPrices, state.BuyVolumes);
        Reconcile(state, true);
        Reconcile(state, false);
        EnforceAssetLimits();
        state.LimitReason = state.TargetBuy < buy || state.TargetSell < sell ? "예약/재고 보호 한도" : state.BuyProtected || state.SellProtected ? "60초 체결 압력 보호" : "정상 방어";
    }

    private decimal GetDefenseLevelLimit(StockState state)
    {
        externalLevelSizes.Clear();
        state.Stock.orderBook.GetTopBids(sizingLevels, 10);
        CollectExternalLevelSizes(state, true);
        state.Stock.orderBook.GetTopAsks(sizingLevels, 10);
        CollectExternalLevelSizes(state, false);
        if (externalLevelSizes.Count == 0)
        { return 300m; }
        externalLevelSizes.Sort();
        int middle = externalLevelSizes.Count / 2;
        decimal median = externalLevelSizes.Count % 2 == 0 ? (externalLevelSizes[middle - 1] + externalLevelSizes[middle]) / 2m : externalLevelSizes[middle];
        return Math.Max(1m, Math.Min(1000m, decimal.Floor(median)));
    }

    private void CollectExternalLevelSizes(StockState state, bool buy)
    {
        foreach (var level in sizingLevels)
        {
            decimal volume = level.volume;
            foreach (var own in buy ? state.Bids : state.Asks)
            {
                if (own != null && own.price == level.price && state.Stock.orderBook.IsActive(own))
                {
                    volume -= own.volume;
                }
            }
            if (!buy)
            {
                foreach (var own in state.InitialOrders)
                {
                    if (own != null && own.price == level.price && state.Stock.orderBook.IsActive(own))
                    {
                        volume -= own.volume;
                    }
                }
            }
            if (volume > 0m)
            {
                externalLevelSizes.Add(volume);
            }
        }
    }

    public void BuildLevels(decimal middle, decimal distance, decimal total, bool buy, decimal[] prices, decimal[] volumes)
    {
        Array.Clear(prices, 0, 3);
        Array.Clear(volumes, 0, 3);
        decimal assigned = 0m;
        for (int i = 0; i < 3; i++)
        {
            decimal volume = i == 2 ? total - assigned : decimal.Floor(total * (i == 0 ? 0.5m : 0.3m));
            assigned += volume;
            decimal raw = middle * (1m + (buy ? -1m : 1m) * (distance + Ratio(levelDistance) * i));
            decimal price = buy ? PriceTickPolicy.FloorToTick(raw) : PriceTickPolicy.CeilToTick(raw);
            if (price <= 0m || volume <= 0m)
            { continue; }

            int destination = i;
            for (int n = 0; n < i; n++)
            {
                if (prices[n] == price)
                {
                    destination = n;
                    break;
                }
            }

            prices[destination] = price;
            volumes[destination] += volume;
        }
    }

    private static decimal Sum(decimal[] values)
    {
        decimal result = 0m;
        foreach (decimal value in values)
        {
            result += value;
        }

        return result;
    }

    private static decimal Cost(decimal[] prices, decimal[] volumes)
    {
        decimal result = 0m;
        for (int i = 0; i < prices.Length; i++)
        {
            result += prices[i] * volumes[i];
        }

        return result;
    }

    private static void LimitVolumes(decimal[] volumes, decimal max)
    {
        decimal total = Sum(volumes);
        if (total <= max || total <= 0m)
        { return; }

        int eligible = 0;
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] > 0m)
            {
                eligible |= 1 << i;
            }

            volumes[i] = decimal.Floor(volumes[i] * max / total);
        }

        decimal remainder = decimal.Floor(max) - Sum(volumes);
        for (int i = 0; i < volumes.Length && remainder > 0m; i++)
        {
            if ((eligible & (1 << i)) == 0)
            { continue; }

            volumes[i]++;
            remainder--;
        }
    }

    private static void LimitCost(decimal[] prices, decimal[] volumes, decimal max)
    {
        decimal cost = Cost(prices, volumes);
        if (cost <= max || cost <= 0m)
        { return; }

        for (int i = 0; i < volumes.Length; i++)
        {
            volumes[i] = decimal.Floor(volumes[i] * max / cost);
        }
    }

    private void Reconcile(StockState state, bool buy)
    {
        var orders = buy ? state.Bids : state.Asks;
        var prices = buy ? state.BuyPrices : state.SellPrices;
        var volumes = buy ? state.BuyVolumes : state.SellVolumes;
        for (int i = 0; i < 3; i++)
        {
            var order = orders[i];
            bool active = state.Stock.orderBook.IsActive(order);
            if (active)
            {
                decimal threshold = Math.Max(PriceTickPolicy.GetTickSize(order.price), Math.Abs(order.price) * Ratio(priceRepositionRatio));
                bool move = Math.Abs(prices[i] - order.price) >= threshold;
                bool resize = order.volume > 0m && Math.Abs(volumes[i] - order.volume) / order.volume >= Ratio(quantityAdjustRatio);
                if (prices[i] <= 0m || volumes[i] <= 0m || move || resize)
                {
                    state.Stock.orderBook.CancelOrder(order, state.Stock);
                    active = false;
                }
            }

            if (active || prices[i] <= 0m || volumes[i] <= 0m)
            { continue; }

            if (ordersLeft <= 0)
            { continue; }

            LastRestingOrder = null;
            SubmittedPurpose = OrderPurpose.ExchangeDefense;
            SubmittedSlot = i;
            submittingState = state;
            if (buy)
            {
                BuyLimit(prices[i], volumes[i], 0, state.Stock);
            }
            else
            {
                SellLimit(prices[i], volumes[i], 0, state.Stock);
            }

            orders[i] = LastRestingOrder;
            ordersLeft--;
            submittingState = null;
            SubmittedSlot = -1;
        }
    }

    public void OnTradeSettled(Stock stock)
    {
        if (StockManager.Instance != null && StockManager.Instance.SetupInProgress)
        { return; }

        if (AssetsInitialized)
        {
            EnforceAssetLimits();
        }
    }

    public void EnforceAssetLimits()
    {
        if (enforcing)
        { return; }

        enforcing = true;
        using (AssetMarker.Auto())
        {
            foreach (var state in states)
            {
                bool initial = state.NewListing && state.CompletedCount < 60;
                decimal owned = GetOwnedVolume(state.Stock), sell = ReservedSell(state) + ReservedSell(state, true);
                if (sell > Math.Max(0m, owned * Ratio(initial ? initialListingSupplyRatio : stockSellReserveRatio)) || owned - sell < state.InitialInventory * Ratio(inventoryFloorRatio))
                {
                    CancelAsks(state);
                    CancelInitial(state);
                    state.LimitReason = "재고 보호 취소";
                }

                if (ReservedBuy(state) > Math.Max(0m, cash * Ratio(stockCashReserveRatio)))
                {
                    CancelBids(state);
                    state.LimitReason = "종목 현금 보호 취소";
                }
            }

            if (reservedCash > Math.Max(0m, cash * Ratio(totalCashReserveRatio)) || reservedCash > cash)
            {
                for (int i = states.Count - 1; i >= 0 && reservedCash > Math.Max(0m, Math.Min(cash, cash * Ratio(totalCashReserveRatio))); i--)
                {
                    CancelBids(states[i]);
                    states[i].LimitReason = "전체 현금 보호 취소";
                }
            }
        }

        enforcing = false;
    }

    private void CancelOrders(StockState state, Order[] orders)
    {
        for (int i = 0; i < orders.Length; i++)
        {
            state.Stock.orderBook.CancelOrder(orders[i], state.Stock);
            orders[i] = null;
        }
    }

    private void CancelBids(StockState state)
    {
        CancelOrders(state, state.Bids);
    }

    private void CancelAsks(StockState state)
    {
        CancelOrders(state, state.Asks);
    }

    private void CancelInitial(StockState state)
    {
        CancelOrders(state, state.InitialOrders);
    }

    private void CancelDefense(StockState state)
    {
        CancelBids(state);
        CancelAsks(state);
    }

    private void CancelState(StockState state)
    {
        CancelDefense(state);
        CancelInitial(state);
    }

    public override void SuspendSession()
    {
        foreach (var state in states)
        {
            CancelState(state);
            state.NeedsOpening = true;
        }

        base.SuspendSession();
    }

    public void LiquidateStock(Stock stock)
    {
        var state = GetState(stock);
        if (state != null)
        {
            CancelState(state);
            state.LimitReason = "상장폐지 방어 중단";
        }
    }

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        RemoveCompanySchedule(stock);
        var state = GetState(stock);
        earlyNews.Remove(stock);
        if (state == null)
        { return; }

        stock.OnMatchedTrade -= OnMatched;
        int index = states.IndexOf(state);
        if (index >= 0)
        {
            states.RemoveAt(index);
            debugStocks.RemoveAt(index);
        }

        byStock.Remove(stock);
    }

    private void UpdateDebug()
    {
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            var d = debugStocks[i];
            d.reference = (double)s.Reference;
            d.current = (double)s.Stock.currentPrice;
            d.middle = (double)s.Middle;
            d.upper = (double)(s.Middle + 2m * s.Deviation);
            d.lower = (double)(s.Middle - 2m * s.Deviation);
            d.risk = (double)s.Risk;
            d.grade = s.Stock.RiskLabel;
            d.gradePending = s.Stock.RiskPending;
            d.nextGradeEvaluation = Math.Max(0f, s.NextGradeEvaluation - SessionClock.Time);
            d.haltRemaining = s.Stock.IsTradingHalted ? Math.Max(0f, s.Stock.HaltUntil - SessionClock.Time) : 0;
            int gradeIndex = Math.Min(3, (int)s.Stock.RiskGrade);
            d.cautiousSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.cautiousWeights, gradeIndex);
            d.neutralSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.neutralWeights, gradeIndex);
            d.speculativeSelection = s.Stock.IsTradingHalted ? 0f : DebugSelectionWeight(Settings.speculativeWeights, gradeIndex);
            d.nextReevaluation = Math.Max(0f, s.NextRegular - SessionClock.Time);
            d.targetBuy = (double)s.TargetBuy;
            d.targetSell = (double)s.TargetSell;
            d.reservedBuy = (double)ReservedBuy(s);
            d.reservedSell = (double)(ReservedSell(s) + ReservedSell(s, true));
            d.availableCash = (double)(cash - reservedCash);
            d.availableShares = (double)GetHolding(s.Stock);
            d.totalShares = (double)GetOwnedVolume(s.Stock);
            d.initialUnsold = (double)Sum(s.InitialRemaining);
            d.completedCandles = s.CompletedCount;
            d.listingDay = s.ListingDay;
            d.state = s.Stock.IsTradingHalted ? "거래중지" : !MarketDayController.CanTrade ? "장외" : s.Risky ? s.Stock.RiskLabel : s.NewListing && s.CompletedCount < 60 ? "신규 초기 공급" : s.RecoveryStage < 3 ? "단계 복귀" : "정상";
            d.limitReason = s.LimitReason;
            d.buyProtected = s.BuyProtected;
            d.sellProtected = s.SellProtected;
            d.recoveryStep = s.RecoveryStage;
        }
    }

    private static float DebugSelectionWeight(float[] weights, int index)
    {
        float weight = weights != null && index < weights.Length ? weights[index] : 1f;
        return float.IsNaN(weight) || float.IsInfinity(weight) ? 1f : Mathf.Clamp(weight, 0f, 10f);
    }

    protected override void OnDestroy()
    {
        CandleHistoryManager.OnStockCandleClosed -= OnCandleCompleted;
        StockManager.OnStockListed -= OnListed;
        NewsManager.OnMarketNews -= OnNews;
        foreach (var state in states)
        {
            CancelState(state);
            state.Stock.OnMatchedTrade -= OnMatched;
        }

        byStock.Clear();
        states.Clear();
        earlyNews.Clear();
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }
}
