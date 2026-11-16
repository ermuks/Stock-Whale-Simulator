using UnityEngine;

/// <summary>
/// 존버(Hold) 봇: 매수 위주, 거의 안 파는 장기 투자자.
/// - 꾸준히 분할 매수
/// - 극단적 상황 아니면 매도 안 함
/// - 유통량 감소 효과 → 가격 상승 압력
/// </summary>
public class HolderBot : TradeBot
{
    [Header("매수 전략")]
    [Tooltip("매 틱마다 매수 시도 확률")]
    [Range(0f, 1f)]
    public float buyChance = 0.5f;
    [Tooltip("현재가 대비 할인 매수 범위 최소 (0.01 = -1%)")]
    public float buyDiscountMin = 0.005f;
    [Tooltip("현재가 대비 할인 매수 범위 최대 (0.03 = -3%)")]
    public float buyDiscountMax = 0.025f;
    [Tooltip("시장가 매수 확률 (매도벽 뚫기)")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.15f;

    [Header("매도 조건 (극히 제한적)")]
    [Tooltip("이 수익률 이상일 때만 일부 매도 고려 (0.5 = +50%)")]
    public float sellProfitThreshold = 0.5f;
    [Tooltip("수익 실현 시 매도 확률")]
    [Range(0f, 1f)]
    public float sellChance = 0.1f;

    /// <summary>HolderBot: 50% 확률로 단일 종목 전문 (장기 투자자 특성)</summary>
    protected override float GetSingleStockChance() => 0.5f;

    public override bool UseStockPreference() => true;

    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();

        int tier = Random.Range(0, 10);
        if (tier < 5)
            cash = (decimal)(Random.Range(12, 24) * 1_000_000);
        else if (tier < 8)
            cash = (decimal)(Random.Range(24, 36) * 1_000_000);
        else
            cash = (decimal)(Random.Range(36, 60) * 1_000_000);

        wallParticipationChance = 0.5f;
        wallWithdrawChance = 0.3f;
    }

    protected override float GetThinkDelayMin() => 10f;
    protected override float GetThinkDelayMax() => 30f;

    protected override void OnWallForming(Stock stock, decimal resistancePrice)
    {
        if (stock == null) return;
        if (Random.value > wallParticipationChance) return;

        decimal holding = GetHolding(stock);
        if (holding <= 0m) return;

        decimal sellVol = ClampMinVolume(holding * 0.3m);
        if (sellVol <= 0m) return;

        SellLimit(resistancePrice, sellVol, 0, stock);
    }

    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock); // RealPrice 기준
        decimal current = stock.currentPrice;
        if (refPrice <= 0m) return;

        decimal holding = GetHolding(stock);
        decimal avg = (holdings.ContainsKey(stock) && avgPrice.ContainsKey(stock)) ? avgPrice[stock] : 0m;

        bool isOvervalued = FeelsExpensive(stock);
        bool isUndervalued = FeelsCheap(stock);

        if (holding > 0m && avg > 0m)
        {
            decimal profitRatio = (current - avg) / avg;
            
            bool shouldSell = profitRatio >= (decimal)sellProfitThreshold;
            if (isOvervalued && current > refPrice * 1.05m)
                shouldSell = shouldSell || (profitRatio >= 0.1m && Random.value < 0.3f);

            if (shouldSell && Random.value < sellChance)
            {
                decimal sellVol = ClampMinVolume(holding * 0.1m);
                if (sellVol > 0m)
                {
                    decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.FloorToTick(refPrice * 0.99m)); // 더 비싸게 매도 심리
                    DelayedSellLimit(sellPrice, sellVol, 0, stock);
                }
                return;
            }
        }

        float actualBuyChance = buyChance;
        if (isUndervalued)
        {
            float undervaluedRatio = refPrice > 0m ? (float)((refPrice - current) / refPrice) : 0f;
            actualBuyChance += undervaluedRatio * 2f;
        }
        else if (isOvervalued)
        {
            actualBuyChance *= 0.3f;
        }

        if (Random.value >= actualBuyChance) return;
        if (cash <= refPrice) return;

        decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
        if (vol <= 0m) return;

        if (isUndervalued && Random.value < 0.4f)
        {
            DelayedBuyMarket(vol, stock);
        }
        else if (Random.value < marketBuyChance)
        {
            DelayedBuyMarket(vol, stock);
        }
        else
        {
            float discount = Random.Range(buyDiscountMin, buyDiscountMax);
            decimal price = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)discount))); // 더 싸게 매수 심리
            if (price > 0m)
                DelayedBuyLimit(price, vol, 0, stock);
        }
    }
}
