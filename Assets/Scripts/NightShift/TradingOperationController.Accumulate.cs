using System;
using System.Collections.Generic;
using UnityEngine;

// 자동 매집. 플레이어가 정한 가격대 안에서 매수 대기 주문을 유지하며 조용히 모은다.
// 가격이 갑자기 무너지면 대기 주문을 바로 거두고, 평균 단가보다 쌀 때만 조금씩 사서 단가를 낮춘다(물타기).
// 실제 계좌의 현금과 실제 호가만 쓴다. 가격이나 잔고를 만들어 내지 않는다.
public sealed partial class TradingOperationController
{
    /// <summary>이 시간(초) 안에 고점에서 이만큼 떨어지면 폭락으로 본다.</summary>
    public const float CrashWindowSeconds = 30f;
    public const decimal CrashDrop = .04m;
    /// <summary>대기 주문 하나의 크기는 예산의 이 몫이다. 폭락 중의 물타기는 그것의 이 비율만 산다.</summary>
    private const decimal SliceShare = 1m / 40m, AverageDownShare = .15m;
    private const float AccumulateRefreshSeconds = 1f, TakeSeconds = 8f, AverageDownSeconds = 6f, CrashHoldSeconds = 45f;

    public enum AccumulatePhase { Idle, Buying, AboveBand, BelowBand, Crash, OutOfBudget, Closed }
    public decimal AccumulateLow { get; private set; }
    public decimal AccumulateHigh { get; private set; }
    public AccumulatePhase AccumulateStatus { get; private set; }
    public bool AccumulateRunning => CurrentMode == Mode.Accumulate && Active;
    /// <summary>평균 매수 단가. 아직 산 것이 없으면 0.</summary>
    public decimal AccumulateAverage => Bought > 0m ? Spent / Bought : 0m;
    public decimal AccumulateRestingPrice => Live(accumulateBid) ? accumulateBid.price : 0m;
    public decimal AccumulateRestingQuantity => Live(accumulateBid) ? accumulateBid.volume : 0m;
    public decimal AccumulateBudgetLeft => Math.Max(0m, Budget - Spent);
    public readonly List<DeviceMarketJournal.Fill> AccumulateFills = new(32);
    public double AccumulateStartedGameMinute { get; private set; }
    private Order accumulateBid;
    private readonly Queue<(float time, decimal price)> accumulatePrices = new();
    private float nextAccumulate, nextTake, nextAverageDown, crashStarted;
    private decimal crashLow;

    /// <summary>가격대와 쓸 현금(가용 현금의 비율)을 정하고 매집을 시작한다.</summary>
    public bool StartAccumulate(Stock stock, decimal low, decimal high, float cashFraction)
    {
        Adopt();
        if (Active)
        { Feedback = "매매 실행 중"; return false; }
        // 한 종목에는 자동 매매를 하나만 돌린다(같은 종목의 스프레드와 매집이 서로의 주문을 건드리지 않게).
        if (RunOn(stock) != null)
        { Feedback = "매매 실행 중"; return false; }
        var accounts = PlayerAccounts.Instance;
        if (accounts != null && account == accounts.automated && !accounts.AutomatedUnlocked)
        { Feedback = "계좌 잠김"; return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade)
        { Feedback = "거래 불가"; return false; }
        low = PriceTickPolicy.FloorToTick(low); high = PriceTickPolicy.FloorToTick(high);
        if (low < 1m || high < low)
        { Feedback = "가격대를 확인하세요"; return false; }
        if (spreadSession == null && accounts != null)
        { spreadSession = account == accounts.automated ? accounts.automatedOrders : account == accounts.primary ? accounts.primaryOrders : null; }
        decimal budget = decimal.Floor(SpreadAvailableCash * (decimal)Mathf.Clamp01(cashFraction));
        if (budget < low)
        { Feedback = "현금 부족"; return false; }
        Unsubscribe();
        Target = stock; EntryPrice = stock.currentPrice;
        InitialInventory = Inventory = account.GetOwnedVolume(stock);
        Budget = budget; AccumulateLow = low; AccumulateHigh = high;
        Spent = Proceeds = Bought = Sold = Result = 0m;
        FillCount = 0; Elapsed = 0f;
        pending.Clear(); accumulateBid = null; AccumulateFills.Clear(); accumulatePrices.Clear();
        nextAccumulate = nextTake = nextAverageDown = 0f; crashStarted = 0f; crashLow = 0m;
        AccumulateStatus = AccumulatePhase.Idle;
        AccumulateStartedGameMinute = DeviceMarketJournal.GameNow;
        CurrentMode = Mode.Accumulate; StartedAt = SessionClock.Time;
        Target.OnMatchedTrade += OnFill;
        RefreshAccumulate();
        return true;
    }
    /// <summary>매집을 멈춘다. 대기 주문만 거두고, 모은 주식은 계좌에 그대로 남는다.</summary>
    public void CancelAccumulate()
    {
        if (!AccumulateRunning)
        { return; }
        Retire(ref accumulateBid);
        Finish("매집 중지");
        AccumulateStatus = AccumulatePhase.Closed;
        Feedback = "매집 중지됨";
    }

    private void RefreshAccumulate()
    {
        if (!AccumulateRunning || Time.timeScale <= 0f)
        { return; }
        Elapsed += Time.deltaTime;
        if (Time.unscaledTime < nextAccumulate)
        { return; }
        nextAccumulate = Time.unscaledTime + AccumulateRefreshSeconds;
        if (Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { CancelAccumulate(); Feedback = "거래 종료"; return; }
        // 장이 닫히면 대기 주문만 거두고 다음 장을 기다린다. 매집은 며칠에 걸쳐 이어진다.
        if (!MarketDayController.CanTrade || !Target.AcceptsOrders)
        { Retire(ref accumulateBid); accumulatePrices.Clear(); AccumulateStatus = AccumulatePhase.Closed; Feedback = "장 대기"; return; }

        float now = SessionClock.Time; decimal price = Target.currentPrice;
        accumulatePrices.Enqueue((now, price));
        while (accumulatePrices.Count > 0 && now - accumulatePrices.Peek().time > CrashWindowSeconds) { accumulatePrices.Dequeue(); }
        decimal top = price;
        foreach (var sample in accumulatePrices) { if (sample.price > top) { top = sample.price; } }
        bool falling = top > 0m && 1m - price / top >= CrashDrop;

        if (AccumulateStatus != AccumulatePhase.Crash && falling)
        {
            // 폭락: 받쳐 두었던 매수 대기를 그 자리에서 거둔다. 떨어지는 칼을 큰 주문으로 받지 않는다.
            Retire(ref accumulateBid);
            AccumulateStatus = AccumulatePhase.Crash; crashStarted = now; crashLow = price; nextAverageDown = now + 2f;
        }
        if (AccumulateStatus == AccumulatePhase.Crash)
        {
            if (price < crashLow) { crashLow = price; crashStarted = now; }
            // 한동안 새 저점이 없고 저점에서 조금 올라왔으면 폭락이 끝난 것으로 본다.
            if (!falling && now - crashStarted >= CrashHoldSeconds && price >= crashLow * 1.01m) { AccumulateStatus = AccumulatePhase.Idle; }
            else { AverageDown(now, price); Feedback = "폭락 감지 · 소량 물타기"; return; }
        }

        if (AccumulateBudgetLeft < price)
        { Retire(ref accumulateBid); AccumulateStatus = AccumulatePhase.OutOfBudget; Feedback = "예산 소진"; return; }
        if (price > AccumulateHigh)
        { Retire(ref accumulateBid); AccumulateStatus = AccumulatePhase.AboveBand; Feedback = "가격대 위 · 대기"; return; }
        if (price < AccumulateLow)
        { Retire(ref accumulateBid); AccumulateStatus = AccumulatePhase.BelowBand; Feedback = "가격대 아래 · 대기"; return; }
        AccumulateStatus = AccumulatePhase.Buying; Feedback = "가격대 안 · 매집 중";

        decimal slice = Math.Max(1m, decimal.Floor(Budget * SliceShare / price));
        // 매수 대기: 최우선 매수호가에 줄을 선다. 가격대 위로는 걸지 않는다.
        Target.orderBook.GetTopBids(levels, 1);
        decimal bid = levels.Count > 0 ? levels[0].price : PriceTickPolicy.FloorToTick(price);
        bid = Math.Min(bid, AccumulateHigh);
        if (bid >= AccumulateLow)
        {
            decimal quantity = decimal.Floor(Math.Min(slice, AffordableShares(bid, Live(accumulateBid) ? accumulateBid.price * accumulateBid.volume : 0m)));
            if (!Live(accumulateBid) || accumulateBid.price != bid) { Retire(ref accumulateBid); PlaceAccumulate(true, bid, quantity, true); }
        }
        else { Retire(ref accumulateBid); }
        // 조용한 장에서도 조금씩은 모이도록, 가끔 최우선 매도호가를 소량 받는다.
        if (now >= nextTake)
        {
            nextTake = now + TakeSeconds;
            Target.orderBook.GetTopAsks(levels, 1);
            if (levels.Count > 0 && levels[0].price <= AccumulateHigh && levels[0].price >= AccumulateLow && levels[0].price <= price * 1.025m)
            { PlaceAccumulate(true, levels[0].price, decimal.Floor(Math.Min(Math.Max(1m, slice * .5m), AffordableShares(levels[0].price, 0m))), false); }
        }
    }

    /// <summary>물타기: 평균 단가보다 쌀 때만, 아주 조금씩, 최우선 매도호가를 받는다.</summary>
    private void AverageDown(float now, decimal price)
    {
        if (now < nextAverageDown)
        { return; }
        nextAverageDown = now + AverageDownSeconds;
        decimal average = AccumulateAverage;
        if (average > 0m && price >= average * .995m)
        { return; }
        Target.orderBook.GetTopAsks(levels, 1);
        if (levels.Count == 0 || levels[0].price > price * 1.025m)
        { return; }
        decimal slice = Math.Max(1m, decimal.Floor(Budget * SliceShare / price));
        decimal quantity = decimal.Floor(Math.Min(Math.Max(1m, slice * AverageDownShare), AffordableShares(levels[0].price, 0m)));
        PlaceAccumulate(true, levels[0].price, quantity, false);
    }

    /// <summary>남은 예산과 실제 가용 현금으로 살 수 있는 수량. released는 곧 거둘 대기 주문에 묶인 돈이다.</summary>
    private decimal AffordableShares(decimal price, decimal released)
    {
        decimal cash = Math.Min(AccumulateBudgetLeft, SpreadAvailableCash + released);
        return price <= 0m ? 0m : Math.Max(0m, cash / price);
    }

    private void PlaceAccumulate(bool buy, decimal price, decimal quantity, bool resting)
    {
        if (quantity < 1m || price <= 0m)
        { return; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try { account.BuyLimit(price, quantity, 0, Target); }
        finally { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (beforeSubmit.Contains(order))
            { continue; }
            pending.Add(order);
            if (resting) { accumulateBid = order; }
        }
    }
}
