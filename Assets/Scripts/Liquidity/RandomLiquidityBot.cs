using UnityEngine;

/// <summary>
/// 랜덤 유동 봇: 종목 랜덤 선택, 랜덤 매수/매도.
/// </summary>
public class RandomLiquidityBot : LiquidityBotBase
{
    [Range(0f, 1f)]
    public float buyChance = 0.55f;

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m) return;

        bool doBuy = Random.value < buyChance;
        if (doBuy)
        {
            decimal price = RandomBuyPrice(stock);
            decimal vol = ComputeBuyVolume(price);
            if (price > 0m && vol > 0m)
                BuyLimit(price, vol, 0, stock);
        }
        else
        {
            decimal vol = ComputeSellVolume(stock);
            if (vol > 0m)
            {
                decimal price = RandomSellPrice(stock);
                if (price > 0m) SellLimit(price, vol, 0, stock);
            }
        }
    }
}

