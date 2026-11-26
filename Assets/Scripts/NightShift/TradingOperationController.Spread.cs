using System;
using UnityEngine;

/// <summary>유한한 가용 자산으로 양방향 지정가를 유지한다. UI 갱신과 주문 실행을 분리한다.</summary>
public sealed partial class TradingOperationController
{
    public const int MaxSpreadTicks = 20;
    public DeviceOrderSession spreadSession;
    [SerializeField, Min(.25f)] private float spreadRefreshSeconds = .5f;
    public int SpreadTicks { get; private set; } = 2;
    public bool SpreadRunning => CurrentMode == Mode.Spread && Active;
    public decimal SpreadBid { get; private set; }
    public decimal SpreadAsk { get; private set; }
    public decimal SpreadRealized { get; private set; }
    public decimal SpreadBuyRemaining => Live(spreadBuy) ? spreadBuy.volume : 0m;
    public decimal SpreadSellRemaining => Live(spreadSell) ? spreadSell.volume : 0m;
    public decimal SpreadAvailableCash => account == null ? 0m : spreadSession != null && spreadSession.account == account ? spreadSession.AvailableCash : PlayerAccounts.Available(account);
    private Order spreadBuy, spreadSell;
    private float nextSpread;
    public readonly System.Collections.Generic.List<DeviceMarketJournal.Fill> SpreadFills=new(32);
    public double SpreadStartedGameMinute { get; private set; }
    public double SpreadUpdatedGameMinute { get; private set; }
    public double SpreadEndedGameMinute { get; private set; }
    public string SpreadUpdatedStamp { get; private set; }
    public double SpreadElapsedGameMinutes => Math.Max(0,(SpreadRunning ? DeviceMarketJournal.GameNow : SpreadEndedGameMinute)-SpreadStartedGameMinute);

    public decimal SpreadAvailableShares(Stock stock)
    {
        if (account == null || stock == null)
        { return 0m; }
        return spreadSession != null && spreadSession.account == account ? spreadSession.AvailableShares(stock) : account.GetHolding(stock);
    }
    public void SetSpreadTicks(int ticks)
    {
        int next = Mathf.Clamp(ticks, 1, MaxSpreadTicks);
        if (SpreadTicks == next)
        { return; }
        SpreadTicks = next;
        nextSpread = 0f;
    }
    public static bool SpreadPrices(decimal center, int ticks, out decimal bid, out decimal ask)
    {
        bid = ask = 0m;
        if (center < 1m || center > decimal.MaxValue - 100000m)
        { return false; }
        bid = PriceTickPolicy.FloorToTick(center);
        ask = PriceTickPolicy.CeilToTick(center);
        for (int i = 0; i < Mathf.Clamp(ticks, 1, MaxSpreadTicks); i++)
        {
            bid = Math.Max(1m, PriceTickPolicy.OneTickBelow(bid));
            ask = PriceTickPolicy.OneTickAbove(ask);
        }
        return bid >= 1m && ask > bid && PriceTickPolicy.IsValidPrice(bid) && PriceTickPolicy.IsValidPrice(ask);
    }
    /// <summary>
    /// 스프레드 매수 호가에 쓸 현금. 스프레드는 쓸 수 있는 현금을 모두 매수 호가에 건다.
    /// 여러 종목에서 돌리면 먼저 시작한 종목이 현금을 다 가져가 나머지는 매수 호가를 못 낸다.
    /// 그래서 같은 계좌의 스프레드들이 (남은 현금 + 지금 걸어 둔 매수 호가의 돈)을 고르게 나눠 쓴다. 하나만 돌 때는 예전과 같다.
    /// </summary>
    private decimal SpreadCashShare()
    {
        decimal own = SpreadAvailableCash + (Live(spreadBuy) ? spreadBuy.price * spreadBuy.volume : 0m);
        decimal pool = SpreadAvailableCash; int runs = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || !other.SpreadRunning || other.account != account) { continue; }
            runs++;
            if (other.Live(other.spreadBuy)) { pool += other.spreadBuy.price * other.spreadBuy.volume; }
        }
        return runs <= 1 ? own : Math.Min(own, pool / runs);
    }
    public bool StartSpread(Stock stock)
    {
        Adopt();
        if (Active)
        { Feedback = "매매 실행 중"; return false; }
        if (RunOn(stock) != null)
        { Feedback = "매매 실행 중"; return false; }
        var accounts = PlayerAccounts.Instance;
        if (accounts != null && account == accounts.automated && !accounts.AutomatedUnlocked)
        { Feedback = "계좌 잠김"; return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade)
        { Feedback = "거래 불가"; return false; }
        if (!SpreadPrices(stock.currentPrice, SpreadTicks, out decimal bid, out decimal ask))
        { Feedback = "시세 대기"; return false; }
        if (spreadSession == null && accounts != null)
        { spreadSession = account == accounts.automated ? accounts.automatedOrders : account == accounts.primary ? accounts.primaryOrders : null; }
        Unsubscribe();
        Target = stock;
        EntryPrice = stock.currentPrice;
        InitialInventory = Inventory = account.GetOwnedVolume(stock);
        Budget = SpreadAvailableCash;
        Spent = Proceeds = Bought = Sold = Result = SpreadRealized = 0m;
        SpreadFills.Clear();
        FillCount = 0; Elapsed = 0f;
        pending.Clear(); spreadBuy = spreadSell = null;
        SpreadBid = bid; SpreadAsk = ask;
        CurrentMode = Mode.Spread; StartedAt = SessionClock.Time;
        SpreadStartedGameMinute=SpreadUpdatedGameMinute=DeviceMarketJournal.GameNow; SpreadEndedGameMinute=0;
        Target.OnMatchedTrade += OnFill;
        nextSpread = 0f;
        RefreshSpread();
        return true;
    }
    public void CancelSpread()
    {
        if (!SpreadRunning)
        { return; }
        SpreadEndedGameMinute=DeviceMarketJournal.GameNow; Finish("매매 취소");
        spreadBuy = spreadSell = null;
        Feedback = "매매 취소됨";
    }
    private bool Live(Order order) => order != null && Target != null && order.volume > 0m && Target.orderBook.IsActive(order);
    private void Retire(ref Order order)
    {
        if (order == null)
        { return; }
        if (Live(order))
        { Target.orderBook.CancelOrder(order, Target); }
        pending.Remove(order); order = null;
    }
    private void RefreshSpread()
    {
        if (!SpreadRunning || Time.timeScale <= 0f)
        { return; }
        Elapsed += Time.deltaTime;
        if (Time.unscaledTime < nextSpread)
        { return; }
        nextSpread = Time.unscaledTime + Math.Max(.25f, spreadRefreshSeconds);
        if (!MarketDayController.CanTrade || !Target.AcceptsOrders || Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { CancelSpread(); Feedback = "거래 종료"; return; }
        if (!SpreadPrices(Target.currentPrice, SpreadTicks, out decimal bid, out decimal ask))
        { CancelSpread(); Feedback = "시세 대기"; return; }
        SpreadBid = bid; SpreadAsk = ask;
        SpreadUpdatedGameMinute=DeviceMarketJournal.GameNow; SpreadUpdatedStamp=DeviceMarketJournal.FullStamp();
        // 먼저 이전 가격의 양쪽 호가를 회수한다. 새 호가가 자신의 옛 호가와 교차하지 않게 한다.
        if (!Live(spreadBuy) || spreadBuy.price != bid)
        { Retire(ref spreadBuy); }
        if (!Live(spreadSell) || spreadSell.price != ask)
        { Retire(ref spreadSell); }
        decimal buyCash = SpreadCashShare();
        decimal sellShares = SpreadAvailableShares(Target) + SpreadSellRemaining;
        MaintainQuote(ref spreadBuy, true, bid, decimal.Floor(buyCash / bid));
        MaintainQuote(ref spreadSell, false, ask, decimal.Floor(sellShares));
        Feedback = SpreadBuyRemaining > 0m && SpreadSellRemaining > 0m ? "양방향 대기" : SpreadBuyRemaining > 0m ? "매수 대기" : SpreadSellRemaining > 0m ? "매도 대기" : "자산 · 호가 대기";
    }
    private void MaintainQuote(ref Order quote, bool buy, decimal price, decimal quantity)
    {
        if (Live(quote) && quote.price == price && quote.volume == quantity)
        { return; }
        Retire(ref quote);
        if (quantity < 1m)
        { return; }
        // 스프레드 주문은 유동성을 제공한다. 반대 호가를 쓸어 먹거나 수동 주문을 자전 취소하지 않는다.
        if (buy) { Target.orderBook.GetTopAsks(levels, 1); }
        else { Target.orderBook.GetTopBids(levels, 1); }
        if (levels.Count > 0 && (buy ? levels[0].price <= price : levels[0].price >= price))
        { return; }
        quantity = decimal.Floor(Math.Min(quantity, buy ? SpreadAvailableCash / price : SpreadAvailableShares(Target)));
        if (quantity < 1m)
        { return; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try
        {
            if (buy) { account.BuyLimit(price, quantity, 0, Target); }
            else { account.SellLimit(price, quantity, 0, Target); }
        }
        finally { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!beforeSubmit.Contains(order))
            { quote = order; pending.Add(order); }
        }
    }
}
