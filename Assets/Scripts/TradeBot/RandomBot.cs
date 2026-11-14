using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 랜덤 봇: 다양한 패턴의 매매로 시장 유동성 제공.
/// - 호가창 엣지 매매
/// - 갭 채우기
/// - 랜덤 가격대 주문
/// - 시장가 매수로 매도벽 소화
/// </summary>
public class RandomBot : TradeBot
{
    [Header("패턴 확률")]
    [Tooltip("호가 엣지 패턴 (1호가 +/- 1틱)")]
    [Range(0f, 1f)]
    public float bookEdgePatternChance = 0.25f;
    [Tooltip("갭 채우기 패턴")]
    [Range(0f, 1f)]
    public float gapFillPatternChance = 0.1f;
    [Tooltip("시장가 매수 확률")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.35f;
    [Tooltip("시장가 매도 확률")]
    [Range(0f, 1f)]
    public float marketSellChance = 0.25f;

    [Header("매수/매도 비율")]
    [Tooltip("매수 확률 (나머지는 매도). 0.6 = 60% 매수")]
    [Range(0.3f, 0.8f)]
    public float buyBias = 0.55f;

    /// <summary>RandomBot: 15% 확률로 단일 종목 전문 (유동성 분산이 목적이라 낮은 확률)</summary>
    protected override float GetSingleStockChance() => 0.15f;

    public override bool UseStockPreference() => true;

    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();

        int tier = Random.Range(0, 10);
        if (tier < 5)
            cash = (decimal)(Random.Range(6, 12) * 1_000_000);
        else if (tier < 8)
            cash = (decimal)(Random.Range(12, 18) * 1_000_000);
        else
            cash = (decimal)(Random.Range(18, 24) * 1_000_000);

        chanceToTryRandomCancel = 0.25f;
        randomOrderCancelChancePerOrder = 0.08f;
    }

    protected override float GetThinkDelayMin() => 2f;
    protected override float GetThinkDelayMax() => 8f;

    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock); // RealPrice 기준
        if (refPrice <= 0m) return;

        var book = stock.orderBook;
        var bids = book.GetTopBids();
        var asks = book.GetTopAsks();

        if (Random.value < marketBuyChance && cash > refPrice)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m)
            {
                DelayedBuyMarket(vol, stock);
                return;
            }
        }

        decimal holding = GetHolding(stock);
        if (Random.value < marketSellChance && holding > 0m)
        {
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m)
            {
                DelayedSellMarket(vol, stock);
                return;
            }
        }

        if (Random.value < bookEdgePatternChance && bids.Count > 0 && asks.Count > 0)
        {
            DoBookEdge(stock, bids, asks, refPrice);
            return;
        }

        if (Random.value < gapFillPatternChance && TryPlaceGapFillOrder(stock, bids, asks, refPrice))
            return;

        DoRandomOrder(stock, refPrice);
    }

    void DoBookEdge(Stock stock, List<(decimal price, decimal volume)> bids, List<(decimal price, decimal volume)> asks, decimal refPrice)
    {
        decimal bestBid = bids[0].price;
        decimal bestAsk = asks[0].price;
        decimal buyPrice = PriceTickPolicy.OneTickAbove(bestBid);
        decimal sellPrice = PriceTickPolicy.OneTickBelow(bestAsk);

        bool doBuy = Random.value < buyBias;
        
        if (doBuy)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m && cash >= buyPrice * vol)
                DelayedBuyLimit(buyPrice, vol, 0, stock);
        }
        else
        {
            decimal holding = GetHolding(stock);
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m && holding >= vol)
                DelayedSellLimit(sellPrice, vol, 0, stock);
        }
    }

    void DoRandomOrder(Stock stock, decimal refPrice)
    {
        bool doBuy = Random.value < buyBias;

        if (doBuy)
        {
            float pctF = Random.Range(-0.05f, 0.02f); // 더 싸게 매수 심리
            decimal price = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * (1m + (decimal)pctF)));
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m && price > 0m && cash >= price * vol)
                DelayedBuyLimit(price, vol, 0, stock);
        }
        else
        {
            decimal holding = GetHolding(stock);
            if (holding <= 0m) return;

            float pctF = Random.Range(-0.01f, 0.08f); // 더 비싸게 매도 심리
            decimal price = GetEffectiveSellPrice(stock, PriceTickPolicy.CeilToTick(refPrice * (1m + (decimal)pctF)));
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m && price > 0m)
                DelayedSellLimit(price, vol, 0, stock);
        }
    }

    bool TryPlaceGapFillOrder(Stock stock, List<(decimal price, decimal volume)> bids, List<(decimal price, decimal volume)> asks, decimal refPrice)
    {
        var sellGaps = new List<decimal>();
        for (int i = 0; i + 1 < asks.Count; i++)
        {
            decimal p = PriceTickPolicy.GapPriceBetween(asks[i].price, asks[i + 1].price);
            if (p > 0m) sellGaps.Add(p);
        }
        var buyGaps = new List<decimal>();
        for (int i = 0; i + 1 < bids.Count; i++)
        {
            decimal p = PriceTickPolicy.GapPriceBetween(bids[i + 1].price, bids[i].price);
            if (p > 0m) buyGaps.Add(p);
        }

        bool doBuy = buyGaps.Count > 0 && (sellGaps.Count == 0 || Random.value < buyBias);
        
        if (doBuy && buyGaps.Count > 0)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            decimal gapPrice = buyGaps[Random.Range(0, buyGaps.Count)];
            if (vol > 0m && cash >= gapPrice * vol)
            {
                DelayedBuyLimit(gapPrice, vol, 0, stock);
                return true;
            }
        }
        
        if (sellGaps.Count > 0 && GetHolding(stock) > 0m)
        {
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m)
            {
                DelayedSellLimit(sellGaps[Random.Range(0, sellGaps.Count)], vol, 0, stock);
                return true;
            }
        }
        return false;
    }
}
