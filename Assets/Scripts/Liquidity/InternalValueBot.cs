using UnityEngine;

/// <summary>
/// RealValue(뉴스 기반 internal value) 괴리로 매매.
/// D_real < -0.1 => 매수, D_real > +0.1 => 매도
/// </summary>
public class InternalValueBot : LiquidityBotBase
{
    public float threshold = 0.10f;

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m || stock.realValue <= 0m) return;
        decimal dReal = (stock.currentPrice - stock.realValue) / stock.realValue;

        if ((float)dReal < -threshold)
        {
            decimal price = RandomBuyPrice(stock);
            decimal vol = ComputeBuyVolume(price);
            if (price > 0m && vol > 0m)
                BuyLimit(price, vol, 0, stock);
        }
        else if ((float)dReal > threshold)
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

