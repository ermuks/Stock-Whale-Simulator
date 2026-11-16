using UnityEngine;

/// <summary>
/// 고래 추종자 봇 = 현실로 치면 커뮤니티에서 리딩방 가입한 회원 같은 존재.
/// - 평소엔 고래가 떠먹여 줌 (펌프에 합류해서 FOMO 매수, 익절 기회)
/// - 상장폐지 때는 고래가 내놓는 물량을 받아먹다가 전부 물림 → 고래는 팔로워를 잃고, 팔로워는 털림.
/// - 스스로 익절/손절 판단, 패닉셀. 결국 고래한테 털리는 구조.
/// </summary>
public class WhaleFollowerBot : TradeBot
{
    public static readonly System.Collections.Generic.List<WhaleFollowerBot> RegisteredFollowers = new();
    public WhaleBot Commander { get; private set; }

    public bool TryAssignCommander(WhaleBot whale)
    {
        if (whale == null || Commander != null && Commander != whale)
        { return false; }

        Commander = whale;
        return true;
    }

    protected override void Awake()
    {
        base.Awake();
        if (isActiveAndEnabled)
        {
            RegisteredFollowers.Add(this);
        }
    }

    protected override void OnDestroy()
    {
        Commander?.followers.Remove(this);
        RegisteredFollowers.Remove(this);
        base.OnDestroy();
    }

    public enum Mode
    {
        Pooled,
        FomoBuy,
        Holding,
        PanicSell,
        Done
    }

    [Header("상태")]
    public Mode mode = Mode.Pooled;
    public Stock operationStock;
    public decimal entryAvgPrice;
    public float joinTime;
    [Header("FOMO 매수")]
    [Tooltip("매수 간격(초)")]
    public float buyIntervalMin = 3f;
    public float buyIntervalMax = 8f;
    [Tooltip("1회 매수 현금 비율")]
    public decimal buyCashRatio = 0.15m;
    [Tooltip("시장가 매수 확률 (낮게 유지)")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.15f;
    [Tooltip("매수 지속 시간(초) - 이후 Holding으로")]
    public float buyDurationMin = 30f;
    public float buyDurationMax = 90f;
    [Header("익절 조건")]
    [Tooltip("이 수익률 이상이면 익절 고려")]
    public float takeProfitThreshold = 0.05f;
    [Tooltip("익절 확률 (틱마다)")]
    [Range(0f, 1f)]
    public float takeProfitChance = 0.3f;
    [Tooltip("체결강도가 이 값 이상이면 과열로 판단하고 익절 (안정 균형: 50~60%)")]
    public float tradeStrengthSellThreshold = 150f;
    [Tooltip("체결강도가 이 값 이하이면 매도세 강함 → 패닉")]
    public float tradeStrengthPanicThreshold = 40f;
    [Header("패닉셀 조건")]
    [Tooltip("평단 대비 이 비율 이상 하락 시 패닉")]
    public float panicThreshold = 0.03f;
    [Tooltip("패닉 시 손절 확률")]
    [Range(0f, 1f)]
    public float panicSellChance = 0.5f;
    [Tooltip("시장가 손절 확률 (낮게 유지)")]
    [Range(0f, 1f)]
    public float panicMarketSellChance = 0.2f;
    [Header("타임아웃")]
    [Tooltip("이 시간(초) 후에도 보유 중이면 그냥 정리")]
    public float maxHoldDuration = 180f;
    float _nextActionTime;
    float _buyEndTime;
    float _peakPrice;
    /// <summary>고래가 JoinPump 시 준 목표가. 이 가격을 기준으로 따라감.</summary>
    decimal _whaleTargetPrice;
    protected override float GetThinkDelayMin() => 1f;
    protected override float GetThinkDelayMax() => 4f;
    public void SetAsPooled()
    {
        if (operationStock != null)
        {
            operationStock.orderBook.CancelAllOrdersByOwner(this, operationStock);
            decimal remaining = GetHolding(operationStock);
            if (remaining > 0m)
            {
                if (ExchangeWarehouse.Instance != null)
                {
                    ExchangeWarehouse.Instance.ReceiveShares(operationStock, remaining);
                }

                ForceRemoveHolding(operationStock, remaining);
            }
        }

        Commander?.followers.Remove(this);
        Commander = null;
        mode = Mode.Pooled;
        operationStock = null;
        entryAvgPrice = 0m;
        joinTime = 0f;
        _nextActionTime = 0f;
        _buyEndTime = 0f;
        _peakPrice = 0f;
        _whaleTargetPrice = 0m;
        reservedCash = 0m;
        ResetCash();
    }

    void ResetCash()
    {
        if (InitialCashAssigned && StockManager.Instance != null && StockManager.Instance.useInitialEconomy)
        { return; }

        int tier = Random.Range(0, 10);
        if (tier < 5)
        {
            cash = (decimal)(Random.Range(6, 10) * 1_000_000);
        }
        else if (tier < 8)
        {
            cash = (decimal)(Random.Range(10, 15) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(14, 22) * 1_000_000);
        }
    }

    public void JoinPump(Stock stock, decimal target)
    {
        if (stock == null)
        { return; }

        operationStock = stock;
        _whaleTargetPrice = target > 0m ? target : stock.fakeValue;
        entryAvgPrice = 0m;
        joinTime = SessionClock.Time;
        mode = Mode.FomoBuy;
        _nextActionTime = SessionClock.Time + Random.Range(0.1f, 1f);
        _buyEndTime = SessionClock.Time + Random.Range(buyDurationMin, buyDurationMax);
        _peakPrice = (float)stock.currentPrice;
    }

    public void CommandSell()
    {
    }

    public void AllowSell()
    {
    }

    public void ExitAndReturnToPool()
    {
        SetAsPooled();
        OnReturnedToPool?.Invoke(this);
    }

    public static System.Action<WhaleFollowerBot> OnReturnedToPool;
    /// <summary>작전 중에는 고래가 정한 목표가를 기준가로 따름. 풀 대기 중이면 FakePrice(베이스).</summary>
    protected override decimal GetReferencePrice(Stock stock)
    {
        if (stock == null)
        { return 0m; }

        if (mode != Mode.Pooled && mode != Mode.Done && operationStock == stock && _whaleTargetPrice > 0m)
        { return _whaleTargetPrice; }

        return base.GetReferencePrice(stock);
    }

    protected override Stock PickStock()
    {
        if (mode == Mode.Pooled || mode == Mode.Done)
        { return null; }

        return operationStock;
    }

    protected override void Start()
    {
        base.Start();
        int tier = Random.Range(0, 10);
        if (tier < 5)
        {
            cash = (decimal)(Random.Range(6, 10) * 1_000_000);
        }
        else if (tier < 8)
        {
            cash = (decimal)(Random.Range(10, 15) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(14, 22) * 1_000_000);
        }

        SetAsPooled();
    }

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || operationStock == null || stock != operationStock)
        { return; }

        float now = SessionClock.Time;
        if (now < _nextActionTime)
        { return; }

        decimal refPrice = GetReferencePrice(stock); // RealPrice 기준
        decimal current = stock.currentPrice;
        if (refPrice <= 0m)
        {
            _nextActionTime = now + 1f;
            return;
        }

        if ((float)current > _peakPrice)
        {
            _peakPrice = (float)current;
        }

        if (now - joinTime > maxHoldDuration)
        {
            ForceExit(stock, refPrice, now);
            return;
        }

        switch (mode)
        {
            case Mode.FomoBuy:
                ExecuteFomoBuy(stock, refPrice, now);
                break;
            case Mode.Holding:
                ExecuteHolding(stock, current, refPrice, now);
                break;
            case Mode.PanicSell:
                ExecutePanicSell(stock, refPrice, now);
                break;
        }
    }

    void ExecuteFomoBuy(Stock stock, decimal refPrice, float now)
    {
        if (now >= _buyEndTime)
        {
            mode = Mode.Holding;
            _nextActionTime = now + 1f;
            return;
        }

        decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
        if (vol > 0m && cash > refPrice * vol)
        {
            decimal buyPrice = PriceTickPolicy.FloorToLargerTick(refPrice);
            if (buyPrice <= 0m)
            {
                buyPrice = PriceTickPolicy.FloorToTick(refPrice);
            }

            buyPrice = GetEffectiveBuyPrice(stock, buyPrice);
            decimal? safePrice = GetSpreadSafePrice(stock, true);
            if (safePrice.HasValue && safePrice.Value < buyPrice)
            {
                buyPrice = safePrice.Value;
            }

            if (Random.value < marketBuyChance)
            {
                BuyLimit(GetAggressiveLimitPrice(stock, true), vol, 0, stock);
            }
            else
            {
                BuyLimit(buyPrice, vol, 0, stock);
            }

            UpdateEntryAvg(stock);
        }

        _nextActionTime = now + Random.Range(buyIntervalMin, buyIntervalMax);
    }

    void ExecuteHolding(Stock stock, decimal current, decimal refPrice, float now)
    {
        decimal holding = GetHolding(stock);
        if (holding <= 0m)
        {
            mode = Mode.Done;
            ExitAndReturnToPool();
            return;
        }

        if (entryAvgPrice <= 0m)
        {
            _nextActionTime = now + 1f;
            return;
        }

        decimal profitRatio = (current - entryAvgPrice) / entryAvgPrice;
        decimal dropFromPeak = _peakPrice > 0 ? (decimal)((_peakPrice - (float)current) / _peakPrice) : 0m;
        // 체결강도 체크 (안정 균형: 50~60%)
        float tradeStrength = 100f;
        if (StockStatistics.Instance != null)
        {
            tradeStrength = StockStatistics.Instance.GetTradeStrength(stock);
        }

        // 체결강도가 너무 높으면(매수세 과열) → 수익 중이면 미리 빠짐
        if (profitRatio > 0m && tradeStrength >= tradeStrengthSellThreshold)
        {
            TakeProfit(stock, refPrice, holding);
            return;
        }

        // 체결강도가 너무 낮으면(매도세 강함) → 패닉셀
        if (tradeStrength <= tradeStrengthPanicThreshold)
        {
            mode = Mode.PanicSell;
            _nextActionTime = now;
            return;
        }

        if (profitRatio <= -(decimal)panicThreshold || dropFromPeak >= 0.05m)
        {
            if (Random.value < panicSellChance)
            {
                mode = Mode.PanicSell;
                _nextActionTime = now;
                return;
            }
        }

        if (profitRatio >= (decimal)takeProfitThreshold)
        {
            if (Random.value < takeProfitChance)
            {
                TakeProfit(stock, refPrice, holding);
                return;
            }
        }

        _nextActionTime = now + Random.Range(1f, 3f);
    }

    void ExecutePanicSell(Stock stock, decimal refPrice, float now)
    {
        decimal holding = GetHolding(stock);
        if (holding <= 0m)
        {
            mode = Mode.Done;
            ExitAndReturnToPool();
            return;
        }

        decimal sellRatio = (decimal)Random.Range(0.3f, 0.7f);
        decimal sellVol = ClampMinVolume(holding * sellRatio);
        if (sellVol <= 0m)
        {
            sellVol = holding;
        }

        decimal sellPrice = PriceTickPolicy.CeilToLargerTick(refPrice);
        if (sellPrice <= 0m)
        {
            sellPrice = PriceTickPolicy.CeilToTick(refPrice);
        }

        sellPrice = GetEffectiveSellPrice(stock, sellPrice);
        decimal? safePrice = GetSpreadSafePrice(stock, false);
        if (safePrice.HasValue && safePrice.Value > sellPrice)
        {
            sellPrice = safePrice.Value;
        }

        if (Random.value < panicMarketSellChance)
        {
            SellLimit(GetAggressiveLimitPrice(stock, false), sellVol, 0, stock);
        }
        else
        {
            SellLimit(sellPrice, sellVol, 0, stock);
        }

        if (GetHolding(stock) <= 0m || Random.value < 0.5f)
        {
            mode = Mode.Done;
            _nextActionTime = now + 2f;
        }
        else
        {
            _nextActionTime = now + Random.Range(0.5f, 2f);
        }
    }

    void TakeProfit(Stock stock, decimal refPrice, decimal holding)
    {
        decimal sellRatio = (decimal)Random.Range(0.5f, 1f);
        decimal sellVol = ClampMinVolume(holding * sellRatio);
        if (sellVol <= 0m)
        {
            sellVol = holding;
        }

        decimal sellPrice = PriceTickPolicy.CeilToLargerTick(refPrice);
        if (sellPrice <= 0m)
        {
            sellPrice = PriceTickPolicy.CeilToTick(refPrice);
        }

        sellPrice = GetEffectiveSellPrice(stock, sellPrice);
        decimal? safePrice = GetSpreadSafePrice(stock, false);
        if (safePrice.HasValue && safePrice.Value > sellPrice)
        {
            sellPrice = safePrice.Value;
        }

        SellLimit(sellPrice, sellVol, 0, stock);
        if (sellVol >= holding * 0.8m)
        {
            mode = Mode.Done;
            _nextActionTime = SessionClock.Time + 2f;
        }
        else
        {
            _nextActionTime = SessionClock.Time + Random.Range(2f, 5f);
        }
    }

    void ForceExit(Stock stock, decimal refPrice, float now)
    {
        decimal holding = GetHolding(stock);
        if (holding > 0m)
        {
            decimal sellPrice = PriceTickPolicy.CeilToLargerTick(refPrice);
            if (sellPrice <= 0m)
            {
                sellPrice = PriceTickPolicy.CeilToTick(refPrice);
            }

            sellPrice = GetEffectiveSellPrice(stock, sellPrice);
            decimal? safePrice = GetSpreadSafePrice(stock, false);
            if (safePrice.HasValue && safePrice.Value > sellPrice)
            {
                sellPrice = safePrice.Value;
            }

            SellLimit(sellPrice, holding, 0, stock);
        }

        mode = Mode.Done;
        ExitAndReturnToPool();
    }

    void UpdateEntryAvg(Stock stock)
    {
        if (holdings.ContainsKey(stock) && avgPrice.ContainsKey(stock))
        {
            entryAvgPrice = avgPrice[stock];
        }
    }

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null)
        { return; }

        if (operationStock == stock)
        {
            SetAsPooled();
        }
    }
}
