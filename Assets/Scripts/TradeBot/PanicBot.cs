using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 패닉셀러 봇: 급락 시 공포에 손절.
/// - 가격 하락 감지 시 손절매
/// - 시장가 매도 → 추가 하락 유발
/// - 현실적인 폭락장 연출
/// </summary>
public class PanicBot : TradeBot
{
    [Header("패닉 조건")]
    [Tooltip("기준가 대비 이 비율 이상 하락 시 패닉 (0.05 = -5%)")]
    public float panicDropThreshold = 0.05f;
    [Tooltip("패닉 시 손절 확률")]
    [Range(0f, 1f)]
    public float panicSellChance = 0.7f;
    [Tooltip("시장가 손절 확률 (vs 급매도 지정가)")]
    [Range(0f, 1f)]
    public float marketSellChance = 0.5f;

    [Header("일반 매수")]
    [Tooltip("패닉 아닐 때 매수 확률")]
    [Range(0f, 1f)]
    public float normalBuyChance = 0.4f;
    [Tooltip("싼 가격에 매수하려는 욕심 (할인율)")]
    public float greedyDiscountMin = 0.02f;
    public float greedyDiscountMax = 0.08f;

    private Dictionary<Stock, decimal> _peakPrice = new();
    private Dictionary<Stock, int> _dropCount = new();

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null) return;
        _peakPrice.Remove(stock);
        _dropCount.Remove(stock);
    }

    /// <summary>PanicBot: 40% 확률로 단일 종목 전문 (한 종목에 몰빵한 개미)</summary>
    protected override float GetSingleStockChance() => 0.4f;

    public override bool UseStockPreference() => true;

    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();

        int tier = Random.Range(0, 10);
        if (tier < 6)
            cash = (decimal)(Random.Range(6, 10) * 1_000_000);
        else if (tier < 9)
            cash = (decimal)(Random.Range(10, 16) * 1_000_000);
        else
            cash = (decimal)(Random.Range(16, 24) * 1_000_000);

        wallParticipationChance = 0.6f;
        wallWithdrawChance = 0.4f;
    }

    protected override float GetThinkDelayMin() => 5f;
    protected override float GetThinkDelayMax() => 15f;

    protected override void OnWallForming(Stock stock, decimal resistancePrice)
    {
        if (stock == null) return;
        if (Random.value > wallParticipationChance) return;

        decimal holding = GetHolding(stock);
        if (holding <= 0m) return;

        decimal sellVol = ClampMinVolume(holding * 0.4m);
        if (sellVol <= 0m) return;

        SellLimit(resistancePrice, sellVol, 0, stock);
    }

    protected override void ThinkForStock(Stock stock)
    {
        decimal refPrice = GetReferencePrice(stock); // RealPrice 기준
        decimal current = stock.currentPrice;
        if (refPrice <= 0m) return;

        decimal peak = _peakPrice.TryGetValue(stock, out var pk) ? pk : current;
        
        if (current > peak)
        {
            _peakPrice[stock] = current;
            _dropCount[stock] = 0;
            peak = current;
        }

        bool isOvervalued = MarketSentiment.Instance != null && MarketSentiment.Instance.IsOvervalued(stock);
        bool isUndervalued = MarketSentiment.Instance != null && MarketSentiment.Instance.IsUndervalued(stock);

        decimal holding = GetHolding(stock);
        
        if (holding > 0m && peak > 0m)
        {
            decimal dropRatio = (peak - current) / peak;
            
            float effectiveThreshold = panicDropThreshold;
            if (isOvervalued) effectiveThreshold *= 0.7f;
            else if (isUndervalued) effectiveThreshold *= 1.5f;
            
            if (dropRatio >= (decimal)effectiveThreshold)
            {
                int dropCount = _dropCount.TryGetValue(stock, out var dc) ? dc : 0;
                _dropCount[stock] = dropCount + 1;

                float actualPanicChance = panicSellChance;
                if (dropCount >= 2) actualPanicChance *= 1.5f;
                if (dropRatio >= 0.1m) actualPanicChance *= 1.3f;
                if (isOvervalued) actualPanicChance *= 1.3f;
                if (isUndervalued) actualPanicChance *= 0.5f;
                actualPanicChance = Mathf.Clamp01(actualPanicChance);

                if (Random.value < actualPanicChance)
                {
                    decimal sellRatio = 0.3m + (decimal)(dropRatio * 2m);
                    if (sellRatio > 1m) sellRatio = 1m;
                    
                    decimal sellVol = ClampMinVolume(holding * sellRatio);
                    if (sellVol > 0m)
                    {
                        if (Random.value < marketSellChance)
                        {
                            DelayedSellMarket(sellVol, stock);
                        }
                        else
                        {
                            decimal sellPrice = GetEffectiveSellPrice(stock, PriceTickPolicy.FloorToTick(refPrice * 0.98m)); // RealPrice 기준, 더 비싸게 매도 심리
                            DelayedSellLimit(sellPrice, sellVol, 0, stock);
                        }
                        return;
                    }
                }
            }
        }

        float actualBuyChance = normalBuyChance;
        if (isUndervalued)
        {
            actualBuyChance *= 1.5f;
            if (current < refPrice * 0.9m)
                actualBuyChance *= 1.5f;
        }

        if (Random.value < actualBuyChance && cash > refPrice)
        {
            float discount = Random.Range(greedyDiscountMin, greedyDiscountMax);
            decimal targetPrice = GetEffectiveBuyPrice(stock, PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)discount))); // 더 싸게 매수 심리
            
            decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, refPrice));
            if (vol > 0m && targetPrice > 0m)
            {
                DelayedBuyLimit(targetPrice, vol, 0, stock);
            }
        }
    }
}
