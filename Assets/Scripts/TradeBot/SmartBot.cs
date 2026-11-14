using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 평단 기반 매매 봇. 목표 매수가/매도가를 정해두고 가격이 근접하면 주문.
/// - 보유 없을 때: 기준가 대비 -3%~-8% 사이의 목표 매수가 설정
/// - 보유 있을 때: 평단 대비 +3%~+10% 사이의 목표 매도가 설정
/// - 평단보다 낮을 때: 물타기 목표가 설정
/// </summary>
public class SmartBot : TradeBot
{
    [Header("SmartBot 목표가 설정")]
    [Tooltip("매수 목표가: 기준가 대비 최소 할인율 (0.03 = -3%)")]
    public float buyTargetMinDiscount = 0.03f;
    [Tooltip("매수 목표가: 기준가 대비 최대 할인율 (0.08 = -8%)")]
    public float buyTargetMaxDiscount = 0.08f;
    [Tooltip("매도 목표가: 평단 대비 최소 수익률 (0.03 = +3%)")]
    public float sellTargetMinProfit = 0.03f;
    [Tooltip("매도 목표가: 평단 대비 최대 수익률 (0.10 = +10%)")]
    public float sellTargetMaxProfit = 0.10f;
    private Dictionary<Stock, decimal> _targetBuyPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _targetSellPrice = new Dictionary<Stock, decimal>();
    public override void SuspendSession()
    {
        _targetBuyPrice.Clear();
        _targetSellPrice.Clear();
        base.SuspendSession();
    }

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null)
        { return; }

        _targetBuyPrice.Remove(stock);
        _targetSellPrice.Remove(stock);
    }

    /// <summary>SmartBot: 45% 확률로 단일 종목 전문 (평단 관리, 분석형 투자자)</summary>
    protected override float GetSingleStockChance() => 0.45f;
    public override bool UseStockPreference() => true;
    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();
        int tier = Random.Range(0, 10);
        if (tier < 5)
        {
            cash = (decimal)(Random.Range(8, 14) * 1_000_000);
        }
        else if (tier < 8)
        {
            cash = (decimal)(Random.Range(14, 20) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(20, 28) * 1_000_000);
        }
    }

    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock);
        decimal current = stock.currentPrice;
        if (refPrice <= 0m || current <= 0m)
        { return; }

        if (!holdings.ContainsKey(stock) || GetHolding(stock) <= 0m)
        {
            if (!_targetBuyPrice.TryGetValue(stock, out decimal targetBuy) || targetBuy <= 0m)
            {
                float discount = Random.Range(buyTargetMinDiscount, buyTargetMaxDiscount);
                targetBuy = PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)discount));
                _targetBuyPrice[stock] = targetBuy;
            }

            decimal diff = System.Math.Abs(current - targetBuy) / targetBuy;
            if (diff >= (decimal)targetPriceCancelThreshold)
            {
                float discount = Random.Range(buyTargetMinDiscount, buyTargetMaxDiscount);
                targetBuy = PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)discount));
                _targetBuyPrice[stock] = targetBuy;
                return;
            }

            if (diff <= (decimal)targetPriceThreshold)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, targetBuy));
                if (vol > 0m)
                {
                    DelayedBuyLimit(GetEffectiveBuyPrice(stock, targetBuy), vol, 0, stock);
                    _targetBuyPrice[stock] = 0m;
                }
            }

            return;
        }

        decimal avg = avgPrice[stock];
        if (avg <= 0m)
        { return; }

        if (current < avg)
        {
            if (!_targetBuyPrice.TryGetValue(stock, out decimal targetBuy) || targetBuy <= 0m)
            {
                float discount = Random.Range(buyTargetMinDiscount, buyTargetMaxDiscount);
                targetBuy = PriceTickPolicy.FloorToTick(current * (1m - (decimal)discount));
                _targetBuyPrice[stock] = targetBuy;
            }

            decimal diff = System.Math.Abs(current - targetBuy) / targetBuy;
            if (diff >= (decimal)targetPriceCancelThreshold)
            {
                float discount = Random.Range(buyTargetMinDiscount, buyTargetMaxDiscount);
                targetBuy = PriceTickPolicy.FloorToTick(current * (1m - (decimal)discount));
                _targetBuyPrice[stock] = targetBuy;
                return;
            }

            if (diff <= (decimal)targetPriceThreshold)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, targetBuy));
                if (vol > 0m)
                {
                    DelayedBuyLimit(GetEffectiveBuyPrice(stock, targetBuy), vol, 0, stock);
                    _targetBuyPrice[stock] = 0m;
                }
            }

            return;
        }

        if (!_targetSellPrice.TryGetValue(stock, out decimal targetSell) || targetSell <= 0m)
        {
            float profit = Random.Range(sellTargetMinProfit, sellTargetMaxProfit);
            targetSell = PriceTickPolicy.CeilToTick(avg * (1m + (decimal)profit));
            _targetSellPrice[stock] = targetSell;
        }

        decimal sellDiff = System.Math.Abs(current - targetSell) / targetSell;
        if (sellDiff >= (decimal)targetPriceCancelThreshold)
        {
            float profit = Random.Range(sellTargetMinProfit, sellTargetMaxProfit);
            targetSell = PriceTickPolicy.CeilToTick(avg * (1m + (decimal)profit));
            _targetSellPrice[stock] = targetSell;
            return;
        }

        if (sellDiff <= (decimal)targetPriceThreshold)
        {
            decimal vol = ClampMinVolume(GetSellVolume(sellHoldingRatio, stock));
            if (vol > 0m)
            {
                DelayedSellLimit(GetEffectiveSellPrice(stock, targetSell), vol, 0, stock);
                _targetSellPrice[stock] = 0m;
            }
        }
    }
}
