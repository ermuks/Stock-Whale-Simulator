using UnityEngine;

/// <summary>
/// FakeValue 괴리로 매매.
/// D_fake < -0.1 => 매수, D_fake > +0.1 => 매도
/// </summary>
public class FakeValueBot : LiquidityBotBase
{
    public float threshold = 0.10f;

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m || stock.fakeValue <= 0m) return;
        decimal dFake = (stock.currentPrice - stock.fakeValue) / stock.fakeValue;

        if ((float)dFake < -threshold)
        {
            decimal price = RandomBuyPrice(stock);
            decimal vol = ComputeBuyVolume(price);
            if (price > 0m && vol > 0m)
                BuyLimit(price, vol, 0, stock);
        }
        else if ((float)dFake > threshold)
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

