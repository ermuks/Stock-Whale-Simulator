using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최소 매도가(베스트 ask)와 최대 매수가(베스트 bid) 사이의 갭을 채우는 봇.
/// 목표가를 정해두고 현재가가 근접하면 주문 실행, 10% 이상 벗어나면 재설정.
/// </summary>
public class SpreadFillerBot : TradeBot
{
    [Tooltip("갭 호가에 넣을 매수 금액 비율")]
    [Range(0.01f, 0.3f)]
    public float gapOrderCashRatio = 0.05f;
    [Tooltip("갭 호가에 넣을 매도 수량 비율")]
    [Range(0.01f, 0.3f)]
    public float gapOrderSellRatio = 0.05f;
    private readonly List<Order> quoteOrders = new();
    private readonly List<(decimal price, decimal volume)> quoteBids = new(1), quoteAsks = new(1);
    public override bool UseStockPreference() => true;
    /// <summary>현재가 기준으로 스프레드 채우기</summary>
    protected override decimal GetReferencePrice(Stock stock) => stock != null ? stock.currentPrice : 0m;
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
    }

    protected override float GetThinkDelayMin() => 2f;
    protected override float GetThinkDelayMax() => 8f;
    protected override void ThinkForStock(Stock stock)
    {
        decimal current = stock.currentPrice;
        if (current < 1m)
        { return; }
        stock.orderBook.GetTopBids(quoteBids, 1);
        stock.orderBook.GetTopAsks(quoteAsks, 1);
        var bids = quoteBids;
        var asks = quoteAsks;
        decimal bestBid = bids.Count > 0 ? bids[0].price : PriceTickPolicy.OneTickBelow(current);
        decimal bestAsk = asks.Count > 0 ? asks[0].price : PriceTickPolicy.OneTickAbove(current);
        decimal buyPrice = PriceTickPolicy.OneTickAbove(bestBid);
        decimal sellPrice = PriceTickPolicy.OneTickBelow(bestAsk);
        if (buyPrice >= sellPrice)
        {
            buyPrice = bestBid;
            sellPrice = bestAsk;
        }
        // A distant speculative offer must not drag both new quotes away from the traded price.
        buyPrice = System.Math.Min(buyPrice, PriceTickPolicy.OneTickAbove(current));
        sellPrice = System.Math.Max(PriceTickPolicy.OneTickAbove(buyPrice), System.Math.Min(sellPrice, PriceTickPolicy.OneTickAbove(current)));
        stock.orderBook.CopyOwnerOrders(this, quoteOrders);
        bool hasBuy = false, hasSell = false;
        foreach (var order in quoteOrders)
        {
            hasBuy |= order.isBuy;
            hasSell |= !order.isBuy;
        }
        if (!hasBuy && buyPrice >= 1m)
        {
            decimal buyVol = ClampMinVolume(GetBuyVolume((decimal)gapOrderCashRatio, stock, buyPrice));
            if (buyVol > 0m)
            {
                DelayedBuyLimit(buyPrice, buyVol, 0, stock);
            }
        }
        if (!hasSell)
        {
            decimal sellVol = ClampMinVolume(GetSellVolume((decimal)gapOrderSellRatio, stock));
            if (sellVol > 0m)
            {
                DelayedSellLimit(sellPrice, sellVol, 0, stock);
            }
        }
    }
}
