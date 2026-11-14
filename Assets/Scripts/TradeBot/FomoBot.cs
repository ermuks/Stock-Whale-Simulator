using UnityEngine;

/// <summary>
/// FOMO(Fear Of Missing Out) 봇: 가격 상승 감지 시 추격 매수.
/// - 최근 가격 변동률 체크
/// - 상승 중이면 시장가 또는 공격적 지정가로 매수
/// - 거래량 급증 종목으로 이동 (기존 수익 종목 정리 후)
/// - 매도벽을 뚫는 핵심 역할
/// </summary>
public class FomoBot : TradeBot
{
    [Header("FOMO 매수 조건")]
    [Tooltip("최근 가격 상승률이 이 값 이상이면 FOMO 발동 (0.01 = 1%)")]
    public float fomoTriggerRise = 0.003f;
    [Tooltip("FOMO 발동 시 시장가 매수 확률")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.4f;
    [Tooltip("공격적 지정가: 현재 매도 호가에 바로 매수")]
    [Range(0f, 1f)]
    public float aggressiveLimitChance = 0.35f;
    [Header("매수 강도")]
    [Tooltip("FOMO 매수 시 현금 비율 (일반보다 높음)")]
    public decimal fomoBuyCashRatio = 0.15m;
    [Tooltip("상승폭에 따른 추가 매수 배율 (상승률 × 이 값)")]
    public float riseMultiplier = 5f;
    [Header("일반 매수")]
    [Tooltip("FOMO 아닐 때 일반 매수 확률")]
    [Range(0f, 1f)]
    public float normalBuyChance = 0.3f;
    [Header("거래량 급증 반응")]
    [Tooltip("거래량 급증 시 이동 확률")]
    [Range(0f, 1f)]
    public float surgeReactionChance = 0.5f;
    private System.Collections.Generic.Dictionary<Stock, decimal> _lastPrice = new();
    private System.Collections.Generic.Dictionary<Stock, int> _riseCount = new();
    private Stock _surgeTargetStock;
    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null)
        { return; }

        _lastPrice.Remove(stock);
        _riseCount.Remove(stock);
    }

    private float _surgeTargetTime;
    /// <summary>FomoBot: 25% 확률로 단일 종목 전문 (이 종목만 오를 거라 믿는 광신도)</summary>
    protected override float GetSingleStockChance() => 0.25f;
    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();
        int tier = Random.Range(0, 10);
        if (tier < 5)
        {
            cash = (decimal)(Random.Range(8, 16) * 1_000_000);
        }
        else if (tier < 8)
        {
            cash = (decimal)(Random.Range(16, 24) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(24, 40) * 1_000_000);
        }

        wallParticipationChance = 0.1f;
        wallWithdrawChance = 0.7f;
        StockStatistics.OnVolumeSurge -= OnVolumeSurge;
        if (MarketDayController.CanTrade)
        {
            StockStatistics.OnVolumeSurge += OnVolumeSurge;
        }

        PsychologicalLevelManager.OnBreakthroughComplete -= OnBreakthroughComplete;
        if (MarketDayController.CanTrade)
        {
            PsychologicalLevelManager.OnBreakthroughComplete += OnBreakthroughComplete;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!HasStarted || !MarketDayController.CanTrade)
        { return; }

        StockStatistics.OnVolumeSurge -= OnVolumeSurge;
        if (MarketDayController.CanTrade)
        {
            StockStatistics.OnVolumeSurge += OnVolumeSurge;
        }

        PsychologicalLevelManager.OnBreakthroughComplete -= OnBreakthroughComplete;
        if (MarketDayController.CanTrade)
        {
            PsychologicalLevelManager.OnBreakthroughComplete += OnBreakthroughComplete;
        }
    }

    public override void SuspendSession()
    {
        _surgeTargetStock = null;
        base.SuspendSession();
    }

    protected override void OnDisable()
    {
        StockStatistics.OnVolumeSurge -= OnVolumeSurge;
        PsychologicalLevelManager.OnBreakthroughComplete -= OnBreakthroughComplete;
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    void OnBreakthroughComplete(Stock stock, decimal resistancePrice)
    {
        if (stock == null)
        { return; }

        if (Random.value > 0.6f)
        { return; }

        decimal refPrice = GetReferencePrice(stock);
        if (refPrice <= 0m)
        { return; }

        decimal buyVol = ClampMinVolume(GetBuyVolume(fomoBuyCashRatio * 2m, stock, refPrice));
        if (buyVol > 0m && cash >= refPrice * buyVol)
        {
            BuyLimit(GetAggressiveLimitPrice(stock, true), buyVol, 0, stock);
        }
    }

    void OnVolumeSurge(Stock stock, float surgeRatio)
    {
        if (stock == null)
        { return; }

        if (_isSingleStockTrader)
        { return; }

        if (Random.value > surgeReactionChance)
        { return; }

        SellProfitableHoldingsAndMoveTo(stock);
    }

    void SellProfitableHoldingsAndMoveTo(Stock targetStock)
    {
        if (StockManager.Instance == null)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == targetStock)
            { continue; }

            decimal holding = GetHolding(stock);
            if (holding <= 0m)
            { continue; }

            if (!avgPrice.TryGetValue(stock, out decimal avg) || avg <= 0m)
            { continue; }

            decimal profitRatio = (stock.currentPrice - avg) / avg;
            if (profitRatio > 0m)
            {
                decimal sellVol = profitRatio >= 0.05m ? holding : ClampMinVolume(holding * 0.5m);
                if (sellVol > 0m)
                {
                    decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.FloorToTick(GetReferencePrice(stock) * 0.99m));
                    if (sellPrice > 0m)
                    {
                        SellLimit(sellPrice, sellVol, 0, stock);
                    }
                }
            }
        }

        _surgeTargetStock = targetStock;
        _surgeTargetTime = Time.time + Random.Range(5f, 15f);
    }

    protected override Stock PickStock()
    {
        if (_isSingleStockTrader && _specializedStock != null)
        { return _specializedStock; }

        if (_surgeTargetStock != null && Time.time < _surgeTargetTime)
        { return _surgeTargetStock; }

        _surgeTargetStock = null;
        return base.PickStock();
    }

    protected override float GetThinkDelayMin() => 1f;
    protected override float GetThinkDelayMax() => 5f;
    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock); // RealPrice 기준
        decimal current = stock.currentPrice;
        if (refPrice <= 0m)
        { return; }

        decimal lastPrice = _lastPrice.TryGetValue(stock, out var lp) ? lp : current;
        _lastPrice[stock] = current;
        if (lastPrice <= 0m)
        { return; }

        bool isUndervalued = MarketSentiment.Instance != null && MarketSentiment.Instance.IsUndervalued(stock);
        if (isUndervalued && refPrice > current)
        {
            float buyBoost = (float)((refPrice - current) / current);
            float boostedChance = normalBuyChance + buyBoost;
            if (Random.value < boostedChance)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(fomoBuyCashRatio * 1.5m, stock, refPrice));
                if (vol > 0m && cash >= refPrice * vol)
                {
                    if (Random.value < 0.5f)
                    {
                        DelayedBuyMarket(vol, stock);
                    }
                    else
                    {
                        decimal bidPrice = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * 0.98m)); // 더 싸게 매수 심리
                        DelayedBuyLimit(bidPrice, vol, 0, stock);
                    }

                    return;
                }
            }
        }

        decimal changeRatio = (current - lastPrice) / lastPrice;
        if (changeRatio >= (decimal)fomoTriggerRise)
        {
            int riseCount = _riseCount.TryGetValue(stock, out var rc) ? rc : 0;
            _riseCount[stock] = riseCount + 1;
            decimal extraRatio = System.Math.Min((decimal)(changeRatio * (decimal)riseMultiplier), 0.5m);
            decimal buyRatio = fomoBuyCashRatio * (1m + extraRatio);
            if (riseCount >= 2)
            {
                buyRatio *= 1.5m;
            }

            decimal vol = ClampMinVolume(GetBuyVolume(buyRatio, stock, refPrice));
            if (vol <= 0m || cash < refPrice * vol)
            { return; }

            float roll = Random.value;
            if (roll < marketBuyChance)
            {
                DelayedBuyMarket(vol, stock);
            }
            else if (roll < marketBuyChance + aggressiveLimitChance)
            {
                var asks = stock.orderBook.GetTopAsks();
                if (asks.Count > 0)
                {
                    decimal askPrice = asks[0].price;
                    DelayedBuyLimit(askPrice, vol, 0, stock);
                }
                else
                {
                    DelayedBuyLimit(GetAggressiveLimitPrice(stock, true), vol, 0, stock);
                }
            }
            else
            {
                decimal bidPrice = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * 0.995m)); // 더 싸게 매수 심리
                if (bidPrice > 0m)
                {
                    DelayedBuyLimit(bidPrice, vol, 0, stock);
                }
            }
        }
        else
        {
            if (changeRatio < 0m)
            {
                _riseCount[stock] = 0;
            }

            if (Random.value < normalBuyChance)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
                if (vol > 0m && cash >= refPrice * vol)
                {
                    float pct = Random.Range(-0.02f, 0.01f); // 더 싸게 매수 심리
                    decimal price = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * (1m + (decimal)pct)));
                    if (price > 0m)
                    {
                        DelayedBuyLimit(price, vol, 0, stock);
                    }
                }
            }
        }

        TryTakeProfitSell(stock, current, refPrice);
    }

    void TryTakeProfitSell(Stock stock, decimal current, decimal refPrice)
    {
        decimal holding = GetHolding(stock);
        if (holding <= 0m)
        { return; }

        if (!avgPrice.TryGetValue(stock, out decimal avg) || avg <= 0m)
        { return; }

        decimal profitRatio = (current - avg) / avg;
        if (profitRatio >= 0.05m && Random.value < 0.4f)
        {
            decimal sellVol = ClampMinVolume(holding * 0.3m);
            if (sellVol > 0m)
            {
                decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.FloorToTick(refPrice * 0.998m)); // 더 비싸게 매도 심리
                DelayedSellLimit(sellPrice, sellVol, 0, stock);
            }
        }
    }
}
