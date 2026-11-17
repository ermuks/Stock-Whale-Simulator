using System;
using System.Collections.Generic;
using UnityEngine;

// 대주주 세력의 작전. 자기 회사 주식을 조용히 모으고, 치고 올려 눈길을 끈 다음, 따라붙은 매수에 넘긴다.
// 전부 자기 계좌의 실제 주문이다. 가격·가치·잔고를 직접 고치지 않고, 다른 봇에게 신호를 보내지도 않는다.
// 개미는 올라가는 가격과 체결을 보고 스스로 반응할 뿐이다.
public partial class WhaleBot
{
    /// <summary>장이 열려 있는 동안 세력 전체에서 적어도 이만큼은 작전 중이어야 한다.</summary>
    public const int MinimumOperations = 1;
    /// <summary>하루에 한 번, 이 확률로 작전이 하나 더 겹친다.</summary>
    public const float ExtraOperationChance = .25f;
    private const float StaleBuySeconds = 20f, StaleSellSeconds = 25f;

    private WhaleOperationState operation;
    /// <summary>작전을 시작할 때 갖고 있던 수량. 이보다 늘어난 만큼이 작전 물량이다.</summary>
    private decimal operationBase;
    private float operationClock;

    private static float nextPlan;
    private static int planDay = int.MinValue;
    private static bool extraToday;
    private static readonly List<WhaleBot> candidates = new(16);
    private static readonly List<(decimal price, decimal volume)> quotes = new(1);

    public bool Operating => operation != null && operation.Active;
    public int OperationStage => operation == null ? WhaleOperationState.None : operation.stage;
    public WhaleOperationState OperationState => operation;

    private static double Roll() => UnityEngine.Random.value;

    private void TickOperation(float now)
    {
        PlanOperations(now);
        if (!Operating) { return; }
        var stock = OwnedCompany;
        if (stock == null || !stock.AcceptsOrders || PreDisposal)
        {
            // 상장폐지 절차 같은 일이 생기면 작전은 그 자리에서 접는다.
            operation.stage = WhaleOperationState.None;
            if (stock != null) { CancelOperationOrders(stock, true, true, 0f); }
            return;
        }
        // 장이 닫혀 있던 시간은 세지 않는다.
        float seconds = Mathf.Clamp(now - operationClock, 0f, .5f); operationClock = now;
        int stage = operation.stage;
        var order = WhaleOperationPlan.Step(operation, seconds, stock.currentPrice, GetOwnedVolume(stock) - operationBase, Today(), Roll);
        if (operation.stage != stage) { ReportOperation(stock, stage); }
        if (order.Kind == WhaleOperationKind.None) { return; }
        SubmitOperation(stock, order);
    }

    private static int Today() => MarketDayController.Instance == null ? 0 : MarketDayController.Instance.CalendarDay;

    /// <summary>세력 전체를 보고, 작전 중인 종목이 모자라면 한 곳을 골라 시작시킨다.</summary>
    private static void PlanOperations(float now)
    {
        if (now < nextPlan && nextPlan - now < 30f) { return; }
        nextPlan = now + 5f;
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks == null) { return; }
        int day = Today();
        if (day != planDay) { planDay = day; extraToday = UnityEngine.Random.value < ExtraOperationChance; }
        int active = 0;
        foreach (var stock in manager.stocks)
        {
            if (stock.Owner is WhaleBot whale && whale.Operating) { active++; }
        }
        int want = MinimumOperations + (extraToday ? 1 : 0);
        if (active >= want) { return; }
        // 쉬는 날을 지킨 세력부터 찾고, 아무도 없으면 쉬는 날을 무시한다. 하루도 비지 않게 하기 위해서다.
        var chosen = Choose(manager, day, true) ?? Choose(manager, day, false);
        if (chosen == null) { return; }
        chosen.BeginOperation(day);
        if (active + 1 > MinimumOperations) { extraToday = false; }
    }

    private static WhaleBot Choose(StockManager manager, int day, bool rested)
    {
        candidates.Clear(); double total = 0d;
        foreach (var stock in manager.stocks)
        {
            if (stock.Owner is WhaleBot whale && whale.CanOperate(stock, day, rested)) { candidates.Add(whale); total += whale.OperationWeight(stock); }
        }
        if (candidates.Count == 0) { return null; }
        double pick = UnityEngine.Random.value * total;
        foreach (var whale in candidates)
        {
            pick -= whale.OperationWeight(whale.OwnedCompany);
            if (pick <= 0d) { return whale; }
        }
        return candidates[candidates.Count - 1];
    }

    private bool CanOperate(Stock stock, int day, bool rested)
    {
        if (!isActiveAndEnabled || !IsCompanyOwner || Operating || PreDisposal || stock != OwnedCompany || !stock.AcceptsOrders || stock.currentPrice <= 0m) { return false; }
        if (WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this)) { return false; }
        if (rested && operation != null && day < operation.restUntilDay) { return false; }
        // 최소한 몇백 주는 살 수 있어야 작전이 된다.
        return cash - ReservedCash >= stock.currentPrice * 500m;
    }

    /// <summary>세력은 진짜 가치를 안다. 가치보다 싼 종목일수록 올리기 쉬우니 더 자주 고른다.</summary>
    private double OperationWeight(Stock stock)
    {
        double ratio = stock.realValue > 0m ? (double)(stock.realValue / stock.currentPrice) : 1d;
        ratio = Math.Max(.5d, Math.Min(2d, ratio));
        return ratio * ratio;
    }

    private void BeginOperation(int day)
    {
        var stock = OwnedCompany;
        operation ??= new WhaleOperationState();
        // 위에 걸어 둔 자기 매도 물량은 거둔다. 올리려는 사람이 스스로 길을 막지 않는다.
        CancelOperationOrders(stock, true, true, 0f);
        operationBase = GetOwnedVolume(stock); operationClock = SessionClock.Time;
        decimal available = Math.Max(0m, cash - ReservedCash);
        // 가진 현금의 30~60%. 시가총액의 5%를 넘기지 않는다.
        decimal budget = Math.Min(available * (decimal)(.3f + UnityEngine.Random.value * .3f), stock.issuedShares * stock.currentPrice * .05m);
        WhaleOperationPlan.Begin(operation, stock.currentPrice, budget, day, Roll);
        Debug.Log($"[세력] 작전 시작 — {stock.stockName}({stock.symbol}) 예산 {budget:N0}원 · 목표 +{operation.gain * 100m:0}% · 매집 {operation.buildSeconds:0}초 / 끌어올리기 {operation.driveSeconds:0}초 / 털기 {operation.unwindSeconds:0}초");
    }

    private void ReportOperation(Stock stock, int before)
    {
        string name = operation.stage == WhaleOperationState.Drive ? "끌어올리기" : operation.stage == WhaleOperationState.Unwind ? "털기" : "종료";
        Debug.Log($"[세력] 작전 {name} — {stock.stockName}({stock.symbol}) 현재가 {stock.currentPrice:N0} · 시작가 {operation.startPrice:N0} · 고점 {operation.peakPrice:N0} · 매수 {operation.boughtShares:N0}주 {operation.spent:N0}원");
    }

    private void SubmitOperation(Stock stock, WhaleOperationOrder order)
    {
        if (order.Kind == WhaleOperationKind.Finish) { CancelOperationOrders(stock, true, true, 0f); return; }
        bool buying = order.Kind == WhaleOperationKind.JoinBid || order.Kind == WhaleOperationKind.TakeAsk || order.Kind == WhaleOperationKind.LiftAsk;
        // 오래 걸려 있는 주문은 거둔다. 한 가격에 벽이 쌓이지 않게 한다.
        CancelOperationOrders(stock, true, !buying, buying ? StaleBuySeconds : StaleSellSeconds);
        decimal bid = Best(stock, true), ask = Best(stock, false), tick = PriceTickPolicy.GetTickSize(stock.currentPrice);
        decimal price;
        switch (order.Kind)
        {
            case WhaleOperationKind.JoinBid:
                price = bid > 0m ? bid : PriceTickPolicy.FloorToTick(stock.currentPrice);
                break;
            case WhaleOperationKind.TakeAsk:
                if (ask <= 0m) { return; }
                price = ask;
                break;
            case WhaleOperationKind.LiftAsk:
                price = Math.Max(ask, PriceTickPolicy.CeilToTick(stock.currentPrice * 1.012m));
                break;
            case WhaleOperationKind.JoinAsk:
                // 매수·매도 사이가 벌어져 있으면 한 칸 아래에 대서 먼저 팔리게 한다.
                price = ask <= 0m ? PriceTickPolicy.CeilToTick(stock.currentPrice + tick) : bid > 0m && ask - bid > tick ? ask - tick : ask;
                break;
            default:
                if (bid <= 0m) { return; }
                price = bid;
                break;
        }
        if (price <= 0m || order.Quantity < 1m) { return; }
        if (buying) { BuyLimit(price, order.Quantity, 0, stock); }
        else { SellLimit(price, Math.Min(order.Quantity, GetHolding(stock)), 0, stock); }
    }

    private static decimal Best(Stock stock, bool bid)
    {
        if (stock.orderBook == null) { return 0m; }
        quotes.Clear();
        if (bid) { stock.orderBook.GetTopBids(quotes, 1); }
        else { stock.orderBook.GetTopAsks(quotes, 1); }
        return quotes.Count > 0 ? quotes[0].price : 0m;
    }

    private void CancelOperationOrders(Stock stock, bool buys, bool sells, float olderThan)
    {
        if (stock == null || stock.orderBook == null) { return; }
        stock.orderBook.CopyOwnerOrders(this, ownOrders);
        float now = SessionClock.Time;
        foreach (var order in ownOrders)
        {
            if (order.isBuy ? !buys : !sells) { continue; }
            if (now - order.createdAt < olderThan || !stock.orderBook.IsActive(order)) { continue; }
            stock.orderBook.CancelOrder(order, stock);
        }
    }
}
