using UnityEngine;

/// <summary>
/// 단순 마켓메이커: 현재가 주변에 양방향 호가를 유지해 체결이 일어나게 만든다.
/// - Buy/Sell 스프레드 범위는 비율로 조절
/// - 주문은 분할해서 여러 가격대에 배치
/// </summary>
public class MarketMakerBot : TradeBot
{
    [Header("마켓메이커 설정")]
    public Stock targetStock;

    [Tooltip("매수 호가: P*(1 - 이 비율) ~ P*(1 - 이 비율/2)")]
    [Range(0.001f, 0.05f)]
    public float bidDepthRatio = 0.01f;

    [Tooltip("매도 호가: P*(1 + 이 비율/2) ~ P*(1 + 이 비율)")]
    [Range(0.001f, 0.05f)]
    public float askDepthRatio = 0.01f;

    [Tooltip("한 번에 깔아둘 호가 레벨 수 (각 사이드)")]
    [Range(1, 10)]
    public int levelsPerSide = 3;

    [Tooltip("한 레벨당 주문 수량(주) 상한. 너무 크면 벽처럼 보임")]
    public decimal maxVolPerLevel = 50_000m;

    [Tooltip("오더북을 새로 깔기 전에 기존 내 주문을 랜덤 취소할 확률")]
    [Range(0f, 1f)]
    public float refreshCancelChance = 0.35f;

    [Tooltip("스프레드가 이 비율 이상일 때만 재배치 (불필요한 주문/취소 감소)")]
    [Range(0.0005f, 0.05f)]
    public float refreshSpreadRatioThreshold = 0.01f;

    [Tooltip("현재가 대비 내 호가가 너무 멀면 재배치 (0이면 비활성)")]
    [Range(0f, 0.2f)]
    public float refreshIfOrdersFarRatio = 0.03f;

    protected override float GetThinkDelayMin() => 1.0f;
    protected override float GetThinkDelayMax() => 1.8f;

    protected override Stock PickStock() => targetStock != null ? targetStock : base.PickStock();

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m) return;

        var book = stock.orderBook;
        if (book == null) return;
        var bids = book.GetTopBids();
        var asks = book.GetTopAsks(1);
        decimal bestBid = bids.Count > 0 ? bids[0].price : 0m;
        decimal bestAsk = asks.Count > 0 ? asks[0].price : 0m;

        bool shouldRefresh = false;
        if (bestBid > 0m && bestAsk > 0m)
        {
            decimal mid = (bestBid + bestAsk) / 2m;
            decimal spreadRatio = mid > 0m ? (bestAsk - bestBid) / mid : 0m;
            if (spreadRatio >= (decimal)refreshSpreadRatioThreshold)
                shouldRefresh = true;
        }
        else
        {
            // 한쪽이라도 없으면 우선 공급
            shouldRefresh = true;
        }

        if (!shouldRefresh && refreshIfOrdersFarRatio > 0f)
        {
            decimal current = stock.currentPrice;
            // 오더북 최우선가가 현재가에서 너무 멀면 재배치(가격이 튀었는데 갱신이 안 됨)
            if (bestBid > 0m && (current - bestBid) / current > (decimal)refreshIfOrdersFarRatio) shouldRefresh = true;
            if (bestAsk > 0m && (bestAsk - current) / current > (decimal)refreshIfOrdersFarRatio) shouldRefresh = true;
        }

        // 필요할 때만 내 호가를 정리해서 최신 가격대에 재배치
        if (shouldRefresh && refreshCancelChance > 0f)
            stock.CancelRandomOrdersByOwner(this, refreshCancelChance);

        decimal p = stock.currentPrice;
        decimal bidMin = PriceTickPolicy.FloorToTick(p * (1m - (decimal)bidDepthRatio));
        decimal bidMax = PriceTickPolicy.FloorToTick(p * (1m - (decimal)bidDepthRatio * 0.5m));
        decimal askMin = PriceTickPolicy.CeilToTick(p * (1m + (decimal)askDepthRatio * 0.5m));
        decimal askMax = PriceTickPolicy.CeilToTick(p * (1m + (decimal)askDepthRatio));

        if (bidMax <= 0m || askMin <= 0m) return;
        if (bidMax >= askMin) // 스프레드가 0이 되지 않게 한 틱 보정
            askMin = PriceTickPolicy.OneTickAbove(bidMax);

        if (!shouldRefresh)
            return;

        // 매수 호가 배치 (cash 기반)
        if (cash - reservedCash > 0m)
        {
            for (int i = 0; i < levelsPerSide; i++)
            {
                decimal t = levelsPerSide <= 1 ? 0m : (decimal)i / (levelsPerSide - 1);
                decimal price = PriceTickPolicy.FloorToTick(bidMax - (bidMax - bidMin) * t);
                if (price <= 0m) continue;
                decimal vol = decimal.Floor((cash - reservedCash) * 0.03m / price);
                if (vol > maxVolPerLevel) vol = maxVolPerLevel;
                vol = ClampMinVolume(vol);
                if (vol <= 0m) continue;
                BuyLimit(price, vol, 0, stock);
            }
        }

        // 매도 호가 배치 (holding 기반)
        decimal have = GetHolding(stock);
        if (have > 0m)
        {
            for (int i = 0; i < levelsPerSide; i++)
            {
                decimal t = levelsPerSide <= 1 ? 0m : (decimal)i / (levelsPerSide - 1);
                decimal price = PriceTickPolicy.CeilToTick(askMin + (askMax - askMin) * t);
                if (price <= 0m) continue;
                decimal vol = decimal.Floor(have * 0.03m);
                if (vol > maxVolPerLevel) vol = maxVolPerLevel;
                vol = ClampMinVolume(vol);
                if (vol <= 0m) continue;
                SellLimit(price, vol, 0, stock);
            }
        }
    }
}

