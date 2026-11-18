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

    private float haste = -1f;
    /// <summary>조급함(0~1). 낮을수록 느긋하고 높을수록 급하다. 급하다·느긋하다로 나뉘는 것이 아니라 계좌마다 다른 수치이고, 한 번 정해지면 변하지 않는다.</summary>
    public float Haste
    {
        get
        {
            if (haste < 0f) { haste = (CompanyBusiness.Hash("HASTE:" + InitialAccountId) % 1000u) / 999f; }
            return haste;
        }
    }

    /// <summary>
    /// 봇이 지금 값이라고 여기는 가격. 마지막 체결가만 보지 않는다.
    /// 마지막 체결가가 매수·매도 호가 사이에 있으면 체결가와 호가 한가운데의 중간으로 보고,
    /// 체결가가 호가 밖으로 밀려나 있으면(이미 지나간 값) 호가 사이의 값으로 본다.
    /// 호가가 멀리 벌어져 있을 때는 체결가에서 1.5% 넘게 벗어나 보지 않는다.
    /// </summary>
    protected decimal SeenPrice(Stock stock)
    {
        if (stock == null) { return 0m; }
        decimal last = stock.currentPrice;
        if (last <= 0m || stock.orderBook == null || !stock.orderBook.TryBest(out decimal bid, out decimal ask)) { return last; }
        decimal middle = Math.Max(last * 0.985m, Math.Min(last * 1.015m, (bid + ask) * 0.5m));
        if (last < bid || last > ask) { return middle; }
        return (last + middle) * 0.5m;
    }

    /// <summary>
    /// 성격에 따라 주문 값을 고친다. 급할수록 기다리는 값 대신 맞은편 호가를 바로 치고(시장가처럼),
    /// 느긋할수록 바로 칠 수 있어도 자기 쪽 호가에 줄을 서서 기다린다.
    /// 공포·추격 상태, 리딩방 지시, 세력·거래소·대주주의 주문은 건드리지 않는다.
    /// </summary>
    private decimal PacedPrice(Stock stock, bool buy, decimal price)
    {
        if (stock == null || price <= 0m || rotating || IsExchangeParticipant || IsCompanyOwner || this is WhaleBot || stock.orderBook == null) { return price; }
        if (InvestorSociety.Instance?.Orders != null && InvestorSociety.Instance.Orders.IsSubmitting(this)) { return price; }
        if (Social != null && (Social.Psychology.HasPanicFor(stock) || Social.Psychology.HasFomoFor(stock))) { return price; }
        if (!stock.orderBook.TryBest(out decimal bid, out decimal ask)) { return price; }
        float temper = Haste; decimal tick = PriceTickPolicy.GetTickSize(bid);
        // 호가창이 한쪽으로 크게 기울어 있으면 마음이 달라진다. 매수 잔량이 두꺼우면(받치는 돈이 보이면) 사는 사람은 서두르고 파는 사람은 느긋해진다.
        // 매도 잔량이 두꺼우면 반대다. 그 잔량이 진짜인지 허수인지는 알 수 없다.
        // 눈에 띄는 벽이 서 있으면 더 그렇다: 매수벽 밑에서는 "받친다, 오른다"고 보고, 매도벽 앞에서는 "못 뚫는다, 내린다"고 본다.
        float lean = Mathf.Clamp(ManipulationTools.BookTilt(stock) * .5f + ManipulationTools.Walls(stock).Lean, -1f, 1f);
        float mine = buy ? lean : -lean;
        float eager = Mathf.Clamp(1f + mine * .7f, .4f, 1.7f), calm = Mathf.Clamp(1f - mine * .5f, .5f, 1.5f);
        if (buy ? price >= ask : price <= bid)
        {
            // 바로 체결될 값이다. 느긋할수록 치지 않고 줄을 선다(틈이 있으면 한 틱 앞에).
            if (UnityEngine.Random.value >= Mathf.Pow(1f - temper, 1.5f) * 0.8f * calm) { return price; }
            bool gap = ask - bid > tick;
            return buy ? (gap ? bid + tick : bid) : (gap ? ask - tick : ask);
        }
        // 기다리는 값이다. 급할수록 맞은편 호가를 친다. 다만 내려던 값에서 너무 멀면(0.4~2%) 치지 않는다.
        decimal reach = price * (decimal)(0.004f + temper * 0.016f);
        if (UnityEngine.Random.value < Mathf.Clamp01(Mathf.Pow(temper, 1.5f) * 0.8f * eager) && (buy ? ask <= price + reach : bid >= price - reach)) { return buy ? ask : bid; }
        return price;
    }

    /// <summary>
    /// 호가창의 벽을 보고 품는 기대. 큰 매수벽이 받치고 있으면 "오를 것"이라 여겨 믿는 값을 최대 3% 높게 치고, 큰 매도벽이 막고 있으면 "내릴 것"이라 여겨 3% 낮게 친다.
    /// 진짜 가치를 아는 계좌(세력·거래소)는 벽에 속지 않는다. 벽이 허수인지는 개미도 알 수 없다.
    /// </summary>
    private decimal WallHope(Stock stock)
    {
        if (KnowsRealValue || IsExchangeParticipant || Social == null) { return 0m; }
        return (decimal)(ManipulationTools.Walls(stock).Lean * .03f);
    }

    // ── 인기 종목으로 갈아타기 ──
    // 어떤 종목이 갑자기 크게 달아올라 오르고 있으면, 현금이 모자란 봇 가운데 일부는 가진 다른 종목을 급히 팔고 그 종목에 뛰어든다.
    // 냉정한 판단이 아니다. 급한 성격, 욕심, 남의 말에 휩쓸리는 정도가 클수록, 그리고 이미 그 종목에 마음이 쏠려 있을수록(FOMO) 잘 저지른다.
    private const float ChaseHeat = 5f, ChaseDrift = .03f, ChaseSeconds = 25f, ChaseRestSeconds = 150f;
    private Stock chaseStock; private float chaseUntil, chaseRest; private bool rotating;
    private static readonly List<Stock> chaseHeld = new();
    /// <summary>확인용: 지금까지 갈아타기를 저지른 횟수와, 그때 판 금액.</summary>
    public static int ChasesTotal; public static decimal ChaseSoldTotal;
    private bool IsChasing(Stock stock) => stock != null && stock == chaseStock && SessionClock.Time < chaseUntil;

    /// <summary>이번 턴을 갈아타기에 썼으면 true.</summary>
    private bool TryChase(Stock hot, float heat)
    {
        float now = SessionClock.Time;
        if (IsChasing(hot))
        {
            // 판 돈이 들어오는 대로 달아오른 종목을 받아 산다.
            decimal volume = GetBuyVolume((decimal)(.6f + Haste * .4f), hot);
            if (volume >= 1m) { BuyLimit(GetAggressiveLimitPrice(hot, true), volume, 0, hot); }
            return true;
        }
        if (heat < ChaseHeat || now < chaseRest || Social == null || IsExchangeParticipant || IsCompanyOwner || this is WhaleBot || StockInterestManager.Instance == null) { return false; }
        // 오르면서 달아오른 종목만 쫓는다. 빠지면서 거래가 터진 종목에는 뛰어들지 않는다.
        if (StockInterestManager.Instance.Drift(hot) < ChaseDrift) { return false; }
        var mind = Social.Psychology;
        if (mind.HasPanic) { return false; }
        // 아무나 저지르지 않는다. 그런 기질이 있는 계좌(급할수록 많다: 열에 한둘에서 셋)와, 이미 그 종목에 마음이 쏠린(FOMO) 계좌만 한다.
        bool fomo = mind.HasFomoFor(hot);
        if (!fomo && CompanyBusiness.Hash("CHASE:" + InitialAccountId) % 100u >= (uint)(12f + Haste * 18f)) { return false; }
        float urge = Mathf.Clamp01((heat - 4f) / 8f) * (.04f + Haste * .22f + mind.Greed / 100f * .15f + mind.Traits.Suggestibility / 100f * .15f) * (fomo ? 2f : 1f);
        if (UnityEngine.Random.value >= urge) { return false; }
        // 현금이 넉넉해도 판다. 따져 보고 하는 일이 아니다. "저걸 들고 있을 때가 아니다"라는 조급함이다.
        decimal equity = Equity();
        if (equity <= 0m) { return false; }
        chaseHeld.Clear();
        foreach (var pair in holdings)
        { if (pair.Value >= 1m && pair.Key != hot && pair.Key != null && pair.Key.AcceptsOrders && pair.Key != OwnedCompany) { chaseHeld.Add(pair.Key); } }
        if (chaseHeld.Count == 0) { return false; }
        chaseHeld.Sort((a, b) => (GetHolding(b) * b.currentPrice).CompareTo(GetHolding(a) * a.currentPrice));
        // 팔 것이 자산의 0.5%도 안 되면 갈아탄다고 할 것도 없다.
        if (GetHolding(chaseHeld[0]) * chaseHeld[0].currentPrice < equity * .005m) { return false; }
        // 가장 큰 것부터 하나 또는 둘을, 급할수록 많이(절반~전부) 최우선 매수호가에 던진다.
        decimal share = (decimal)(.5f + Haste * .5f), sold = 0m; Stock biggest = null;
        rotating = true;
        try
        {
            for (int i = 0; i < chaseHeld.Count && i < (Haste > .6f ? 2 : 1); i++)
            {
                var other = chaseHeld[i]; decimal volume = decimal.Floor(GetHolding(other) * share);
                if (volume < 1m) { continue; }
                decimal price = GetAggressiveLimitPrice(other, false);
                SellLimit(price, volume, 0, other); sold += volume * price; biggest ??= other;
            }
        }
        finally { rotating = false; }
        if (biggest == null) { return false; }
        chaseStock = hot; chaseUntil = now + ChaseSeconds; chaseRest = now + ChaseRestSeconds;
        ChasesTotal++; ChaseSoldTotal += sold;
        InvestorSociety.Instance?.NoteRotation(this, biggest, hot);
        return true;
    }

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
    { return stock != null && stock.currentPrice > 0m && SeenPrice(stock) < BelievedPrice(stock) * (0.97m + WallHope(stock)); }

    protected bool FeelsExpensive(Stock stock)
    { return stock != null && SeenPrice(stock) > BelievedPrice(stock) * (1.03m + WallHope(stock)); }

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

        decimal gap = BelievedPrice(stock) / SeenPrice(stock) - 1m + WallHope(stock);
        // 믿는 값보다 쌀수록, 그리고 추격할 때 쉬고 있던 현금을 크게 꺼낸다. 개미가 부자가 된 만큼 올라탈 때 들어오는 돈도 커진다.
        decimal conviction = Math.Max(0.25m, Math.Min(4m, 1m + gap * 5m));
        if (mind.HasFomoFor(stock))
        {
            conviction *= 2.5m;
        }

        // 개미 전체가 처음 들고 있는 주식 비중(실측 약 58%)과 같아지도록 맞춘 값이다. 평균 0.5 × 여유 1.15 ≈ 0.58.
        // 이보다 크면 개장 직후 전 종목을 사들이는 편향이 생긴다.
        decimal exposure = (decimal)((0.2f + Social.Risk * 0.6f) * StockManager.Instance.retailExposureScale);
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
