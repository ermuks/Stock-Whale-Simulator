using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 덤머니(개미) 봇: 매수 위주, 가끔 시장가 충동 매수.
/// - 싸게 사려고 하지만 참을성 없어서 시장가도 씀
/// - 거래소 매도벽 소화에 기여
/// </summary>
public class StupidBot : TradeBot
{
    [Header("매수 전략")]
    [Tooltip("시장가 매수 확률 (충동 매수)")]
    [Range(0f, 1f)]
    public float marketBuyChance = 0.35f;
    [Tooltip("공격적 지정가 (매도 호가에 직접) 확률")]
    [Range(0f, 1f)]
    public float aggressiveBuyChance = 0.30f;
    [Tooltip("할인 매수 범위 최소")]
    public float buyDiscountMin = 0.005f;
    [Tooltip("할인 매수 범위 최대")]
    public float buyDiscountMax = 0.03f;

    /// <summary>StupidBot: 35% 확률로 단일 종목 전문 (이것만 아는 무지한 투자자)</summary>
    protected override float GetSingleStockChance() => 0.35f;

    public override bool UseStockPreference() => true;

    /// <summary>현재가 기준 매매 (스프레드/충동 매수)</summary>
    protected override decimal GetReferencePrice(Stock stock) => stock != null ? stock.currentPrice : 0m;

    protected override void Start()
    {
        base.Start();
        InitializeSingleStockSpecialization();

        int tier = Random.Range(0, 10);
        if (tier < 5)
            cash = (decimal)(Random.Range(4, 8) * 1_000_000);
        else if (tier < 8)
            cash = (decimal)(Random.Range(8, 14) * 1_000_000);
        else
            cash = (decimal)(Random.Range(14, 20) * 1_000_000);

        chanceToTryRandomCancel = 0.22f;
        randomOrderCancelChancePerOrder = 0.07f;
    }

    protected override float GetThinkDelayMin() => 2f;
    protected override float GetThinkDelayMax() => 8f;

    protected override void ThinkForStock(Stock stock)
    {
        decimal current = stock.currentPrice;
        if (current <= 0m || cash <= current) return;

        float marketBias = MarketSentiment.Instance != null ? MarketSentiment.Instance.GetBuyBias() : 0.5f;
        float buyBoost = (marketBias - 0.5f) * 0.5f;

        float actualMarketBuyChance = Mathf.Clamp01(marketBuyChance + buyBoost);
        float actualAggressiveBuyChance = Mathf.Clamp01(aggressiveBuyChance + buyBoost * 0.5f);

        decimal vol = ClampMinVolume(GetBuyVolume(buyCashRatio, stock, current));
        if (vol <= 0m) return;

        float roll = Random.value;
        
        if (roll < actualMarketBuyChance)
        {
            DelayedBuyMarket(vol, stock);
        }
        else if (roll < actualMarketBuyChance + actualAggressiveBuyChance)
        {
            var asks = stock.orderBook.GetTopAsks();
            if (asks.Count > 0)
            {
                decimal askPrice = asks[0].price;
                if (cash >= askPrice * vol)
                    DelayedBuyLimit(askPrice, vol, 0, stock);
            }
            else
            {
                DelayedBuyMarket(vol, stock);
            }
        }
        else
        {
            float discount = Random.Range(buyDiscountMin, buyDiscountMax);
            decimal price = PriceTickPolicy.FloorToTick(current * (1m - (decimal)discount));
            if (price > 0m)
                DelayedBuyLimit(price, vol, 0, stock);
        }
    }
}
