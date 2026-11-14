using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 호가창 끝·소단위·빈틈 채우기 지정가 매수/매도.
/// 목표가를 정해두고 현재가가 근접하면 주문 실행, 10% 이상 벗어나면 재설정.
/// </summary>
public class BookBot : TradeBot
{
    private int _strategyIndex;
    private Dictionary<Stock, decimal> _targetBuyPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _targetSellPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, int> _targetBuySubTick = new Dictionary<Stock, int>();
    private Dictionary<Stock, int> _targetSellSubTick = new Dictionary<Stock, int>();

    [Header("BookBot 설정")]
    [Tooltip("시장가 매수 확률")]
    [Range(0f, 0.5f)]
    public float marketBuyChance = 0.25f;
    [Tooltip("시장가 매도 확률")]
    [Range(0f, 0.5f)]
    public float marketSellChance = 0.15f;

    /// <summary>BookBot: 30% 확률로 단일 종목 전문 (특정 종목 호가창만 깊게 파는 매매자)</summary>
    protected override float GetSingleStockChance() => 0.3f;

    public override bool UseStockPreference() => true;

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null) return;
        _targetBuyPrice.Remove(stock);
        _targetSellPrice.Remove(stock);
        _targetBuySubTick.Remove(stock);
        _targetSellSubTick.Remove(stock);
    }

    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();

        int tier = Random.Range(0, 10);
        if (tier < 6)
            cash = (decimal)(Random.Range(4, 10) * 1_000_000);
        else
            cash = (decimal)(Random.Range(10, 16) * 1_000_000);

        chanceToTryRandomCancel = 0.2f;
        randomOrderCancelChancePerOrder = 0.06f;
    }

    protected override float GetThinkDelayMin() => 2f;
    protected override float GetThinkDelayMax() => 10f;

    protected override void ThinkForStock(Stock stock)
    {
        var book = stock.orderBook;
        decimal refPrice = GetReferencePrice(stock);
        decimal current = stock.currentPrice;
        decimal realValue = stock.realValue;
        if (refPrice <= 0m || current <= 0m) return;

        bool isUndervalued = MarketSentiment.Instance != null && MarketSentiment.Instance.IsUndervalued(stock);
        bool isOvervalued = MarketSentiment.Instance != null && MarketSentiment.Instance.IsOvervalued(stock);

        float actualBuyChance = marketBuyChance;
        float actualSellChance = marketSellChance;

        if (isUndervalued)
        {
            actualBuyChance *= 1.5f;
            actualSellChance *= 0.5f;
        }
        else if (isOvervalued)
        {
            actualBuyChance *= 0.5f;
            actualSellChance *= 1.5f;
        }

        if (Random.value < actualBuyChance && cash > refPrice)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m)
            {
                DelayedBuyMarket(vol, stock);
                return;
            }
        }

        decimal holding = GetHolding(stock);
        if (Random.value < actualSellChance && holding > 0m)
        {
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m)
            {
                DelayedSellMarket(vol, stock);
                return;
            }
        }

        var bids = book.GetTopBids();
        var asks = book.GetTopAsks();

        bool needNewBuyTarget = !_targetBuyPrice.TryGetValue(stock, out decimal targetBuy) || targetBuy <= 0m;
        bool needNewSellTarget = !_targetSellPrice.TryGetValue(stock, out decimal targetSell) || targetSell <= 0m;

        if (!needNewBuyTarget)
        {
            decimal diff = System.Math.Abs(current - targetBuy) / targetBuy;
            if (diff >= (decimal)targetPriceCancelThreshold)
                needNewBuyTarget = true;
            else if (diff <= (decimal)targetPriceThreshold)
            {
                decimal buyVol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, targetBuy));
                if (buyVol > 0m)
                {
                    int subTick = _targetBuySubTick.TryGetValue(stock, out int st) ? st : 0;
                    DelayedBuyLimit(GetEffectiveBuyPrice(stock, targetBuy), buyVol, subTick, stock);
                    _targetBuyPrice[stock] = 0m;
                    needNewBuyTarget = true;
                }
            }
        }

        if (!needNewSellTarget)
        {
            decimal diff = System.Math.Abs(current - targetSell) / targetSell;
            if (diff >= (decimal)targetPriceCancelThreshold)
                needNewSellTarget = true;
            else if (diff <= (decimal)targetPriceThreshold)
            {
                decimal sellVol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
                if (sellVol > 0m && GetHolding(stock) >= sellVol)
                {
                    int subTick = _targetSellSubTick.TryGetValue(stock, out int st) ? st : 0;
                    DelayedSellLimit(GetEffectiveSellPrice(stock, targetSell), sellVol, subTick, stock);
                    _targetSellPrice[stock] = 0m;
                    needNewSellTarget = true;
                }
            }
        }

        if (needNewBuyTarget || needNewSellTarget)
        {
            decimal tick = PriceTickPolicy.GetTickSize(refPrice);
            decimal bestBid = bids.Count > 0 ? bids[0].price : PriceTickPolicy.FloorToTick(refPrice);
            decimal bestAsk = asks.Count > 0 ? asks[0].price : PriceTickPolicy.FloorToTick(refPrice + tick);
            decimal lowestBid = bids.Count > 0 ? bids[bids.Count - 1].price : PriceTickPolicy.FloorToTick(refPrice - tick);
            decimal highestAsk = asks.Count > 0 ? asks[asks.Count - 1].price : PriceTickPolicy.FloorToTick(refPrice + tick);

            decimal belowBestAsk = PriceTickPolicy.OneTickBelow(bestAsk);
            decimal belowBestBid = PriceTickPolicy.OneTickBelow(bestBid);

            decimal buyFloor1 = PriceTickPolicy.FloorToSubTick(refPrice, 1);
            decimal sellCeil1 = PriceTickPolicy.CeilToSubTick(refPrice, 1);
            decimal buyFloor2 = PriceTickPolicy.FloorToSubTick(refPrice, 2);
            decimal sellCeil2 = PriceTickPolicy.CeilToSubTick(refPrice, 2);

            int strategy = _strategyIndex % 8;
            _strategyIndex++;

            decimal? buyPrice = null;
            decimal? sellPrice = null;
            int buySubTick = 0;
            int sellSubTick = 0;

            switch (strategy)
            {
                case 0:
                    buyPrice = lowestBid;
                    sellPrice = highestAsk;
                    break;
                case 1:
                    buyPrice = buyFloor1;
                    sellPrice = sellCeil1;
                    buySubTick = 1;
                    sellSubTick = 1;
                    break;
                case 2:
                    buyPrice = buyFloor2;
                    sellPrice = sellCeil2;
                    buySubTick = 2;
                    sellSubTick = 2;
                    break;
                case 3:
                    sellPrice = belowBestAsk;
                    break;
                case 4:
                    buyPrice = belowBestAsk;
                    break;
                case 5:
                    sellPrice = belowBestBid;
                    break;
                case 6:
                    buyPrice = belowBestBid;
                    break;
                default:
                    FillGapPrices(stock, bids, asks, out buyPrice, out sellPrice);
                    break;
            }

            if (needNewBuyTarget && buyPrice.HasValue && buyPrice.Value > 0m)
            {
                _targetBuyPrice[stock] = buyPrice.Value;
                _targetBuySubTick[stock] = buySubTick;
            }

            if (needNewSellTarget && sellPrice.HasValue && sellPrice.Value > 0m)
            {
                _targetSellPrice[stock] = sellPrice.Value;
                _targetSellSubTick[stock] = sellSubTick;
            }
        }
    }

    void FillGapPrices(Stock stock, List<(decimal price, decimal volume)> bids, List<(decimal price, decimal volume)> asks,
        out decimal? buyPrice, out decimal? sellPrice)
    {
        buyPrice = null;
        sellPrice = null;
        var buyGaps = new List<decimal>();
        for (int i = 0; i + 1 < bids.Count; i++)
        {
            decimal p = PriceTickPolicy.GapPriceBetween(bids[i + 1].price, bids[i].price);
            if (p > 0m) buyGaps.Add(p);
        }
        var sellGaps = new List<decimal>();
        for (int i = 0; i + 1 < asks.Count; i++)
        {
            decimal p = PriceTickPolicy.GapPriceBetween(asks[i].price, asks[i + 1].price);
            if (p > 0m) sellGaps.Add(p);
        }
        if (buyGaps.Count > 0)
            buyPrice = buyGaps[Random.Range(0, buyGaps.Count)];
        if (sellGaps.Count > 0)
            sellPrice = sellGaps[Random.Range(0, sellGaps.Count)];
    }
}
