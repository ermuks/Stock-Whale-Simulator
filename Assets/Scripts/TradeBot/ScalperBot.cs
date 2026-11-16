using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 스캘퍼(초단타) 봇: 1~2틱 스프레드 차익.
/// - 양방향 호가 (시장조성자 역할)
/// - 빠른 매매로 거래량 증가
/// - 거래량 급증 종목으로 이동
/// - 스프레드 수익
/// </summary>
public class ScalperBot : TradeBot
{
    [Header("스캘핑 전략")]
    [Tooltip("스프레드가 이 틱 수 이상이면 양방향 호가")]
    public int minSpreadTicks = 2;
    [Tooltip("양방향 호가 확률")]
    [Range(0f, 1f)]
    public float marketMakingChance = 0.6f;
    [Tooltip("한쪽만 (매수 또는 매도) 호가 확률")]
    [Range(0f, 1f)]
    public float oneSideChance = 0.3f;
    [Header("빠른 매매")]
    [Tooltip("보유 시 즉시 매도 시도 확률")]
    [Range(0f, 1f)]
    public float quickSellChance = 0.7f;
    [Tooltip("목표 수익 틱 수")]
    public int targetProfitTicks = 1;
    [Tooltip("시장가 매수 확률")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.2f;
    [Tooltip("시장가 매도 확률")]
    [Range(0f, 1f)]
    public float marketSellChance = 0.3f;
    [Header("거래량 급증 반응")]
    [Tooltip("거래량 급증 시 이동 확률")]
    [Range(0f, 1f)]
    public float surgeReactionChance = 0.6f;
    private readonly List<Order> scalpOrders = new();
    private Dictionary<Stock, decimal> _entryPrice = new();
    private Stock _surgeTargetStock;
    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null)
        { return; }

        _entryPrice.Remove(stock);
    }

    private float _surgeTargetTime;
    protected override void Start()
    {
        base.Start();
        int tier = Random.Range(0, 10);
        if (tier < 6)
        {
            cash = (decimal)(Random.Range(4, 8) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(8, 12) * 1_000_000);
        }

        orderDelayMin = 0.1f;
        orderDelayMax = 0.5f;
        wallParticipationChance = 0.2f;
        wallWithdrawChance = 0.8f;
        StockStatistics.OnVolumeSurge -= OnVolumeSurge;
        if (MarketDayController.CanTrade)
        {
            StockStatistics.OnVolumeSurge += OnVolumeSurge;
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
    }

    public override void SuspendSession()
    {
        _surgeTargetStock = null;
        base.SuspendSession();
    }

    protected override void OnDisable()
    {
        StockStatistics.OnVolumeSurge -= OnVolumeSurge;
        base.OnDisable();
    }

    protected override void OnBreakthroughImminent(Stock stock, decimal resistancePrice)
    {
        if (stock == null)
        { return; }

        if (Random.value > wallWithdrawChance)
        { return; }

        stock.CancelSellOrdersByOwner(this, stock.currentPrice, -0.005m, 0.01m);
        decimal holding = GetHolding(stock);
        if (holding > 0m && Random.value < 0.5f)
        {
            decimal refPrice = GetReferencePrice(stock);
            if (refPrice <= 0m)
            { return; }

            decimal buyVol = ClampMinVolume(GetBuyVolume(buyCashRatio * 2m, stock, refPrice));
            if (buyVol > 0m && cash >= refPrice * buyVol)
            {
                BuyLimit(GetAggressiveLimitPrice(stock, true), buyVol, 0, stock);
            }
        }
    }

    void OnVolumeSurge(Stock stock, float surgeRatio)
    {
        if (stock == null)
        { return; }

        if (Random.value > surgeReactionChance)
        { return; }

        SellAllAndMoveTo(stock);
    }

    void SellAllAndMoveTo(Stock targetStock)
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

            decimal entry = _entryPrice.TryGetValue(stock, out var ep) ? ep : 0m;
            bool isProfit = entry > 0m && stock.currentPrice > entry;
            if (isProfit || Random.value < 0.5f)
            {
                decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.FloorToTick(GetReferencePrice(stock) * 0.995m));
                if (sellPrice > 0m)
                {
                    SellLimit(sellPrice, holding, 0, stock);
                }
            }
        }

        _surgeTargetStock = targetStock;
        _surgeTargetTime = SessionClock.Time + Random.Range(10f, 30f);
    }

    protected override Stock PickStock()
    {
        if (_surgeTargetStock != null && SessionClock.Time < _surgeTargetTime)
        { return _surgeTargetStock; }

        _surgeTargetStock = null;
        return base.PickStock();
    }

    protected override decimal GetReferencePrice(Stock stock) => stock == null ? 0m : stock.currentPrice;
    protected override float GetThinkDelayMin() => 0.5f;
    protected override float GetThinkDelayMax() => 3f;
    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock); // 초단타는 실제 현재가를 기준으로 거래
        decimal current = stock.currentPrice;
        if (refPrice <= 0m)
        { return; }

        var bids = stock.orderBook.GetTopBids();
        var asks = stock.orderBook.GetTopAsks();
        decimal holding = GetHolding(stock);
        bool isUndervalued = FeelsCheap(stock);
        bool isOvervalued = FeelsExpensive(stock);
        if (holding > 0m)
        {
            float actualSellChance = marketSellChance;
            if (isOvervalued)
            {
                actualSellChance *= 1.5f;
            }
            else if (isUndervalued)
            {
                actualSellChance *= 0.5f;
            }

            if (Random.value < actualSellChance)
            {
                decimal sellVol = GetScalpSellVolume(stock);
                if (sellVol > 0m)
                {
                    DelayedSellMarket(sellVol, stock);
                    return;
                }
            }

            if (TryQuickSell(stock, refPrice, asks))
            { return; }
        }

        float actualBuyChance = marketBuyChance;
        if (isUndervalued)
        {
            actualBuyChance *= 1.5f;
        }
        else if (isOvervalued)
        {
            actualBuyChance *= 0.5f;
        }

        if (Random.value < actualBuyChance && cash > refPrice)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m)
            {
                DelayedBuyMarket(vol, stock);
                _entryPrice[stock] = current;
                return;
            }
        }

        if (bids.Count == 0 || asks.Count == 0)
        {
            decimal buyPrice = bids.Count > 0 ? bids[0].price : PriceTickPolicy.OneTickBelow(current);
            decimal sellPrice = asks.Count > 0 ? asks[0].price : PriceTickPolicy.OneTickAbove(current);
            DoMarketMaking(stock, buyPrice, sellPrice, PriceTickPolicy.GetTickSize(current), refPrice);
            return;
        }

        decimal bestBid = bids[0].price;
        decimal bestAsk = asks[0].price;
        decimal tick = PriceTickPolicy.GetTickSize(refPrice);
        if (tick <= 0m)
        { return; }

        int spreadTicks = (int)((bestAsk - bestBid) / tick);
        if (spreadTicks >= minSpreadTicks)
        {
            float roll = Random.value;
            if (roll < marketMakingChance)
            {
                DoMarketMaking(stock, bestBid, bestAsk, tick, refPrice);
            }
            else if (roll < marketMakingChance + oneSideChance)
            {
                DoOneSide(stock, bestBid, bestAsk, tick, refPrice);
            }
        }
        else
        {
            if (Random.value < 0.3f)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
                if (vol > 0m && cash >= bestBid * vol)
                {
                    DelayedBuyLimit(bestBid, vol, 0, stock);
                    _entryPrice[stock] = bestBid;
                }
            }
        }
    }

    void DoMarketMaking(Stock stock, decimal bestBid, decimal bestAsk, decimal tick, decimal refPrice)
    {
        decimal myBid = PriceTickPolicy.OneTickAbove(bestBid);
        decimal myAsk = PriceTickPolicy.OneTickBelow(bestAsk);
        if (myBid >= myAsk)
        {
            myBid = bestBid;
            myAsk = bestAsk;
        }
        if (myBid < 1m || myAsk <= myBid)
        { return; }

        myBid = System.Math.Min(myBid, PriceTickPolicy.OneTickAbove(stock.currentPrice));
        myAsk = System.Math.Max(PriceTickPolicy.OneTickAbove(myBid), System.Math.Min(myAsk, PriceTickPolicy.OneTickAbove(stock.currentPrice)));
        decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio * 0.5m, stock, myBid));
        if (vol > 0m && cash - reservedCash >= myBid * vol && !HasScalpQuote(stock, true))
        {
            DelayedBuyLimit(myBid, vol, 0, stock);
            _entryPrice[stock] = myBid;
        }
        decimal sellVol = GetScalpSellVolume(stock);
        if (sellVol > 0m && !HasScalpQuote(stock, false))
        {
            DelayedSellLimit(myAsk, sellVol, 0, stock);
        }
    }

    void DoOneSide(Stock stock, decimal bestBid, decimal bestAsk, decimal tick, decimal refPrice)
    {
        if (Random.value > 0.5f)
        {
            decimal myBid = System.Math.Min(PriceTickPolicy.OneTickAbove(bestBid), PriceTickPolicy.OneTickAbove(stock.currentPrice));
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, myBid));
            if (vol > 0m && !HasScalpQuote(stock, true))
            {
                DelayedBuyLimit(myBid, vol, 0, stock);
                _entryPrice[stock] = myBid;
            }
        }
        else
        {
            decimal sellVol = GetScalpSellVolume(stock);
            if (sellVol > 0m && !HasScalpQuote(stock, false))
            {
                decimal myAsk = System.Math.Min(PriceTickPolicy.OneTickBelow(bestAsk), PriceTickPolicy.OneTickAbove(stock.currentPrice));
                DelayedSellLimit(System.Math.Max(1m, myAsk), sellVol, 0, stock);
            }
        }
    }

    private bool HasScalpQuote(Stock stock, bool isBuy)
    {
        stock.orderBook.CopyOwnerOrders(this, scalpOrders);
        foreach (var order in scalpOrders)
        {
            if (order.isBuy == isBuy)
            { return true; }
        }
        return false;
    }

    private decimal GetScalpSellVolume(Stock stock)
    {
        decimal holding = decimal.Floor(GetHolding(stock));
        if (holding < minOrderVolume || sellHoldingRatio <= 0m)
        { return 0m; }
        return System.Math.Min(holding, System.Math.Max(minOrderVolume, GetSellVolume(sellHoldingRatio, stock)));
    }

    bool TryQuickSell(Stock stock, decimal refPrice, List<(decimal price, decimal volume)> asks)
    {
        if (HasScalpQuote(stock, false) || Random.value >= quickSellChance)
        { return false; }

        decimal holding = GetHolding(stock);
        if (holding <= 0m)
        { return false; }

        decimal entry = _entryPrice.TryGetValue(stock, out var ep) ? ep : 0m;
        decimal tick = PriceTickPolicy.GetTickSize(refPrice);
        decimal targetPrice = entry > 0m ? entry + tick * targetProfitTicks : refPrice;
        decimal sellVol = GetScalpSellVolume(stock);
        if (sellVol <= 0m)
        { return false; }

        if (asks.Count > 0)
        {
            decimal bestAsk = asks[0].price;
            decimal myAsk = PriceTickPolicy.OneTickBelow(bestAsk);
            if (myAsk >= targetPrice)
            {
                DelayedSellLimit(myAsk, sellVol, 0, stock);
                return true;
            }
        }

        decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.CeilToTick(targetPrice));
        if (sellPrice > 0m)
        {
            DelayedSellLimit(sellPrice, sellVol, 0, stock);
            return true;
        }
        return false;
    }
}
