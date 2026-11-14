using UnityEngine;

/// <summary>
/// 유동성 봇 공통 베이스. 3~20초 주기로 종목을 선택하고 랜덤 범위로 주문.
/// </summary>
public abstract class LiquidityBotBase : TradeBot
{
    [Header("유동 봇 주기")]
    public float intervalMin = 1f;
    public float intervalMax = 3f;

    [Header("주문 가격 범위 (현재가 P 기준)")]
    [Tooltip("매수 가격: P * (buyMinRatio ~ buyMaxRatio)")]
    public float buyMinRatio = 0.90f;
    public float buyMaxRatio = 1.01f;
    [Tooltip("매도 가격: P * (sellMinRatio ~ sellMaxRatio)")]
    public float sellMinRatio = 0.99f;
    public float sellMaxRatio = 1.10f;

    [Header("주문 규모")]
    [Tooltip("1회 주문에 쓸 현금 비율")]
    [Range(0.01f, 0.4f)]
    public float orderCashRatio = 0.08f;
    [Tooltip("1회 매도에 쓸 보유 비율")]
    [Range(0.01f, 0.6f)]
    public float orderHoldingRatio = 0.12f;
    [Tooltip("최대 주문 수량 상한")]
    public decimal maxOrderVolume = 50_000m;

    protected override float GetThinkDelayMin() => Mathf.Max(0.05f, intervalMin);
    protected override float GetThinkDelayMax() => Mathf.Max(GetThinkDelayMin(), intervalMax);

    protected decimal RandomBuyPrice(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m) return 0m;
        float min = Mathf.Min(buyMinRatio, buyMaxRatio);
        float max = Mathf.Max(buyMinRatio, buyMaxRatio);
        decimal p = stock.currentPrice * (decimal)Random.Range(min, max);
        return PriceTickPolicy.FloorToTick(p);
    }

    protected decimal RandomSellPrice(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m) return 0m;
        float min = Mathf.Min(sellMinRatio, sellMaxRatio);
        float max = Mathf.Max(sellMinRatio, sellMaxRatio);
        decimal p = stock.currentPrice * (decimal)Random.Range(min, max);
        return PriceTickPolicy.CeilToTick(p);
    }

    protected decimal ComputeBuyVolume(decimal price)
    {
        if (price <= 0m) return 0m;
        decimal budget = (cash - reservedCash) * (decimal)orderCashRatio;
        if (budget <= 0m) return 0m;
        decimal vol = decimal.Floor(budget / price);
        if (vol > maxOrderVolume) vol = maxOrderVolume;
        return ClampMinVolume(vol);
    }

    protected decimal ComputeSellVolume(Stock stock)
    {
        if (stock == null) return 0m;
        decimal have = GetHolding(stock);
        if (have <= 0m) return 0m;
        decimal vol = decimal.Floor(have * (decimal)orderHoldingRatio);
        if (vol > maxOrderVolume) vol = maxOrderVolume;
        return ClampMinVolume(vol);
    }
}

