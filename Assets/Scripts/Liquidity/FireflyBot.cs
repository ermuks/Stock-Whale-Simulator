using UnityEngine;

/// <summary>
/// 반딧불이(모멘텀 추격): ΔP, ΔVol이 높은 종목을 고른 뒤 매수 중심으로 진입.
/// </summary>
public class FireflyBot : LiquidityBotBase
{
    [Header("모멘텀 기준")]
    [Tooltip("ΔP가 이 값 이상이면 진입 후보")]
    public float minDeltaP = 0.01f;
    [Tooltip("ΔVol이 이 값 이상이면 진입 후보")]
    public float minDeltaVol = 1.10f;
    public override bool UseStockPreference() => true;
    protected override Stock PickStock()
    {
        if (_isSingleStockTrader || CandleHistoryManager.Instance == null || StockManager.Instance == null)
        { return base.PickStock(); }

        Stock best = null;
        double totalWeight = 0;
        foreach (var s in StockManager.Instance.stocks)
        {
            if (s == null || !s.AcceptsOrders || s.currentPrice <= 0m)
            { continue; }

            if (!CandleHistoryManager.Instance.TryGetRecentCloseAndVolume(s, out var pPrev, out var pCurr, out var vPrev, out var vCurr))
            { continue; }

            if (pPrev <= 0m)
            { continue; }

            decimal dP = (pCurr - pPrev) / pPrev;
            decimal dVol = vCurr / (vPrev <= 0m ? 1m : vPrev);
            if ((float)dP < minDeltaP)
            { continue; }

            if ((float)dVol < minDeltaVol)
            { continue; }

            // 선호도 보정 + 모멘텀 점수
            decimal score = dP * 4m + (dVol - 1m) * 1.5m + (decimal)GetPreference(s);
            double interest = StockInterestManager.Instance == null ? RiskSelectionMultiplier(s) : StockInterestManager.Instance.SelectionWeight(this, s);
            double weight = System.Math.Min(1000000d, System.Math.Max(0d, (double)score) * interest);
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0d)
            { continue; }

            totalWeight += weight;
            if (Random.value * totalWeight < weight)
            {
                best = s;
            }
        }

        return best != null ? best : base.PickStock();
    }

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m)
        { return; }

        // 진입: ΔP/ΔVol이 조건이면 매수
        bool momentum = false;
        if (CandleHistoryManager.Instance != null)
        {
            if (CandleHistoryManager.Instance.TryGetRecentCloseAndVolume(stock, out var pPrev, out var pCurr, out var vPrev, out var vCurr))
            {
                decimal dP = pPrev > 0m ? (pCurr - pPrev) / pPrev : 0m;
                decimal dVol = vCurr / (vPrev <= 0m ? 1m : vPrev);
                momentum = (float)dP >= minDeltaP && (float)dVol >= minDeltaVol;
            }
        }

        if (momentum)
        {
            decimal price = RandomBuyPrice(stock);
            decimal vol = ComputeBuyVolume(price);
            if (price > 0m && vol > 0m)
            {
                BuyLimit(price, vol, 0, stock);
            }
        }
        else
        {
            // 흐름 꺾이면 약하게 정리(보유 있으면 랜덤 매도)
            decimal vol = ComputeSellVolume(stock);
            if (vol > 0m)
            {
                decimal price = RandomSellPrice(stock);
                if (price > 0m)
                {
                    SellLimit(price, vol, 0, stock);
                }
            }
        }
    }
}
