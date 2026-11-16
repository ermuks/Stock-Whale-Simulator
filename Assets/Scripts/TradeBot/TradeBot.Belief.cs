using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개미는 진짜 가치를 모른다. 자기 턴에 직접 본 가격·봉 흐름으로 종목마다 "믿는 가격"을 만들고 그 믿음으로 매매한다.
/// 추격과 투매는 전역 신호가 아니라 이 관찰에서 봇마다 따로 발동한다.
/// </summary>
public abstract partial class TradeBot
{
    /// <summary>진짜 가치를 볼 수 있는 주체(세력·거래소). 개미는 false.</summary>
    public virtual bool KnowsRealValue => IsCompanyOwner || IsExchangeParticipant;
    /// <summary>충동 주문 제출 중. 가격 제한을 넓혀 실제로 호가를 치고 나가게 한다.</summary>
    public bool ImpulseOrder { get; private set; }

    private float nextPanicAct;
    private readonly List<(decimal price, decimal volume)> impulseLevels = new(1);

    public decimal BelievedPrice(Stock stock)
    {
        if (stock == null)
        { return 0m; }

        if (KnowsRealValue)
        { return stock.realValue > 0m ? stock.realValue : stock.currentPrice; }

        if (Social == null || stock.currentPrice <= 0m)
        { return stock.currentPrice; }

        double belief = Social.Psychology.Companies.Belief(stock.symbol, (double)stock.currentPrice, SessionClock.Time);
        return belief > 0 ? (decimal)belief : stock.currentPrice;
    }

    protected bool FeelsCheap(Stock stock)
    { return stock != null && stock.currentPrice > 0m && stock.currentPrice < BelievedPrice(stock) * 0.97m; }

    protected bool FeelsExpensive(Stock stock)
    { return stock != null && stock.currentPrice > BelievedPrice(stock) * 1.03m; }

    /// <summary>이번 턴에 고른 종목을 직접 본다. 숙련도가 낮으면 직전 변화만, 높으면 더 긴 흐름과 평균 대비 과열을 읽는다.</summary>
    private void ObserveMarket(Stock stock)
    {
        if (Social == null || KnowsRealValue || stock == null || stock.currentPrice <= 0m)
        { return; }

        var mind = Social.Psychology;
        float skill = mind.ChartSkill;
        double price = (double)stock.currentPrice, trend = 0, average = price;
        var candles = CandleHistoryManager.Instance == null ? null : CandleHistoryManager.Instance.Completed(stock);
        if (candles != null && candles.Count > 0)
        {
            int window = Mathf.Min(candles.Count, 4 + Mathf.RoundToInt(skill * 16f));
            double first = (double)candles[candles.Count - window].ClosePrice;
            if (first > 0)
            {
                trend = price / first - 1;
            }

            int span = Mathf.Min(candles.Count, 20);
            double sum = 0;
            for (int i = candles.Count - span; i < candles.Count; i++)
            {
                sum += (double)candles[i].ClosePrice;
            }

            if (sum > 0)
            {
                average = sum / span;
            }
        }

        float fear = mind.Fear / 100f;
        double projection = trend >= 0
            ? trend * (0.1 + Social.Chase * 0.45 + Social.Greed * 0.2) * (1 - skill * 0.6)
            : trend * (0.1 + fear * 0.6 + Mathf.Max(0f, Social.Sensitivity - 0.7f) * 0.3) * (1 - skill * 0.5);
        // 숙련된 개미는 최근 평균에서 멀어진 가격을 과열·과매도로 의심한다.
        projection -= skill * 0.6 * (price / average - 1);
        if (mind.HasFomoFor(stock))
        {
            projection += 0.03 + Social.Chase * 0.05;
        }

        if (mind.HasPanicFor(stock))
        {
            projection -= 0.03 + fear * 0.05;
        }

        // 한 번 본 것으로 생각을 다 바꾸지 않는다. 느리게 따라가는 믿음이 급락 때는 저가 매수, 급등 때는 차익 매물이 된다.
        double pull = 0.06 + mind.Traits.Suggestibility / 100.0 * 0.22;
        mind.Companies.ObservePrice(stock.symbol, price, projection, pull, SessionClock.Time);
        if (mind.HasPanicFor(stock) && trend > 0.02)
        {
            mind.EndPanic();
        }

        if (trend >= 0 || GetOwnedVolume(stock) <= 0m)
        { return; }

        // 투매는 흔한 잔파도에서는 나오지 않는다. 눈에 띄는 하락을 봤고, 대개는 내 계좌가 물려 있을 때다.
        double drop = -trend;
        double threshold = 0.045 + skill * 0.05 + Mathf.Max(0f, 1.3f - Social.Sensitivity) * 0.04;
        if (drop < threshold)
        { return; }

        decimal basis = GetOwnedAverage(stock);
        double loss = basis > 0m ? Math.Max(0, 1 - price / (double)basis) : 0;
        double chance = (fear * 0.3 + Social.Stress / 100f * 0.2 + Social.Impulsiveness * 0.1 + loss * 1.2 + (drop - threshold) * 1.5) * (1 - skill * 0.6) / Math.Max(0.5f, Social.Recovery) * 0.16;
        if (mind.TryPanic(stock.symbol, ChatSocialAudit.GameMinute, chance, UnityEngine.Random.value, 10 + Social.Impulsiveness * 20))
        {
            mind.Companies.ShiftBelief(stock.symbol, -0.03 - fear * 0.03, SessionClock.Time);
        }
    }

    /// <summary>투매 충동 상태의 봇이 자기 턴에 보유분을 매수 호가 아래로 던진다. 없는 주식은 팔지 않는다.</summary>
    private bool TryPanicSell(Stock stock)
    {
        if (Social == null || !Social.Psychology.HasPanicFor(stock))
        { return false; }

        if (SessionClock.Time < nextPanicAct)
        { return true; }

        nextPanicAct = SessionClock.Time + UnityEngine.Random.Range(5f, 12f);
        stock.orderBook.CancelAllOrdersByOwner(this, stock);
        decimal holding = GetHolding(stock);
        if (holding < 1m)
        {
            Social.Psychology.EndPanic();
            return false;
        }

        stock.orderBook.GetTopBids(impulseLevels, 1);
        // 받아 줄 매수 호가가 있을 때만 그 아래로 던진다. 호가가 비어 있으면 현재가 바로 아래에 내놓고 기다린다(허공에 가격을 깎지 않는다).
        bool bidNearby = impulseLevels.Count > 0 && impulseLevels[0].price >= stock.currentPrice * 0.9m;
        decimal price = bidNearby
            ? Math.Max(1m, PriceTickPolicy.FloorToTick(impulseLevels[0].price * (1m - (decimal)UnityEngine.Random.Range(0f, 0.02f))))
            : Math.Max(1m, PriceTickPolicy.OneTickBelow(stock.currentPrice));
        decimal volume = decimal.Floor(Math.Max(1m, holding * (decimal)(0.2f + Social.Impulsiveness * 0.35f)));
        ImpulseOrder = bidNearby;
        try
        {
            SellLimit(price, Math.Min(holding, volume), 0, stock);
        }
        finally
        {
            ImpulseOrder = false;
        }

        return true;
    }

    /// <summary>
    /// 이 봇이 이 종목에 두고 싶은 금액. 자산 중 주식 비중(성향)과 종목의 유통 시총 비중에서 출발해 믿음 괴리와 충동으로 늘거나 준다.
    /// 현금이 많다는 이유만으로 작은 종목을 끝없이 사는 일을 막는다. 가치 정보는 쓰지 않는다.
    /// </summary>
    public decimal TargetPositionValue(Stock stock)
    {
        if (Social == null || KnowsRealValue || StockManager.Instance == null || stock == null || stock.currentPrice <= 0m)
        { return decimal.MaxValue; }

        decimal share = StockManager.Instance.FloatShare(stock);
        if (share <= 0m)
        { return decimal.MaxValue; }

        var mind = Social.Psychology;
        if (mind.HasPanicFor(stock))
        { return 0m; }

        decimal gap = BelievedPrice(stock) / stock.currentPrice - 1m;
        decimal conviction = Math.Max(0.25m, Math.Min(2.5m, 1m + gap * 4m));
        if (mind.HasFomoFor(stock))
        {
            conviction *= 1.5m;
        }

        // 개미 전체가 처음 들고 있는 주식 비중(실측 약 58%)과 같아지도록 맞춘 값이다. 평균 0.5 × 여유 1.15 ≈ 0.58.
        // 이보다 크면 개장 직후 전 종목을 사들이는 편향이 생긴다.
        decimal exposure = (decimal)(0.2f + Social.Risk * 0.6f);
        return Equity() * exposure * share * conviction;
    }

    /// <summary>목표 비중까지 더 살 수 있는 금액.</summary>
    public decimal PositionRoom(Stock stock)
    {
        decimal target = TargetPositionValue(stock);
        if (target == decimal.MaxValue)
        { return decimal.MaxValue; }

        return Math.Max(0m, target * 1.15m - GetOwnedVolume(stock) * stock.currentPrice);
    }
}
