using System;
using System.Collections.Generic;
using UnityEngine;

// 대주주 세력의 작전. 자기 회사 주식을 조용히 모으고, 크게 끌어올려 개미가 올라타게 한 다음, 올라탄 만큼 물량을 넘긴다.
// 목표 고점에서는 한 번 크게 팔아 휘청이게 하고 다시 올리기를 몇 번 되풀이한다.
// 전부 자기 계좌의 실제 주문이다. 가격·가치·잔고를 직접 고치지 않고, 다른 봇에게 신호를 보내지도 않는다.
// 개미는 올라가는 가격과 체결을 보고 스스로 반응할 뿐이고, 세력도 체결을 보고 열기를 잴 뿐이다.
public partial class WhaleBot
{
    /// <summary>장이 열려 있는 동안 세력 전체에서 적어도 이만큼은 작전 중이어야 한다.</summary>
    public const int MinimumOperations = 1;
    /// <summary>하루에 한 번, 이 확률로 작전이 하나 더 겹친다.</summary>
    public const float ExtraOperationChance = .25f;
    private const float StaleBuySeconds = 20f, StaleSellSeconds = 25f;
    /// <summary>열기를 재는 시간 창(초)과, 한 번에 나눠 낼 수 있는 주문 수.</summary>
    private const float HeatWindow = 30f;
    private const int OrdersPerStep = 6;
    /// <summary>원래 지분에서 더 넘길 수 있는 한도: 발행 주식의 이 비율까지, 그리고 작전으로 살 수 있는 수량의 이 배수까지.</summary>
    private const decimal ExtraIssuedShare = .03m, ExtraBudgetMultiple = 1.5m;

    private WhaleOperationState operation;
    /// <summary>작전을 시작할 때 갖고 있던 수량. 이보다 늘어난 만큼이 작전 물량이다.</summary>
    private decimal operationBase;
    private float operationClock;
    // 남들이 치고 들어온 매수(시장가에 가까운 매수)의 금액. 최근 것과, 매집하는 동안의 평소 수준.
    private readonly Queue<(float time, decimal value)> crowdBuys = new();
    private decimal crowdRecent, crowdBaseline;
    private float crowdBaselineSeconds;

    private static float nextPlan;
    private static int planDay = int.MinValue;
    private static bool extraToday;
    private static readonly List<WhaleBot> candidates = new(16);
    private static readonly List<(decimal price, decimal volume)> quotes = new(1);
    /// <summary>작전 예산과 내놓을 원래 지분의 한도: 시중에 도는 물량(내 지분을 뺀 것)에 대한 비율.
    /// 예전에는 시가총액의 8%, 발행 주식의 3%였는데, 대주주가 98% 넘게 쥔 종목에서는 그것이 유통 물량 전체보다 컸다.</summary>
    private const decimal BudgetFloatShare = .25m, ExtraFloatShare = .12m;
    // 체결을 듣고 있는 종목. 저장되지 않으므로 불러온 뒤에는 다시 연결된다.
    private static readonly Dictionary<WhaleBot, Stock> listening = new();

    public bool Operating => operation != null && operation.Active;
    public int OperationStage => operation == null ? WhaleOperationState.None : operation.stage;
    public WhaleOperationState OperationState => operation;
    /// <summary>따라붙는 매수의 열기(0~1). 평소의 1.5배부터 오르기 시작해 6배에서 가득 찬다.</summary>
    public double OperationHeat
    {
        get
        {
            if (operation == null) { return 0d; }
            decimal usual = crowdBaselineSeconds > 1f ? crowdBaseline / (decimal)crowdBaselineSeconds * (decimal)HeatWindow : 0m;
            usual = Math.Max(usual, operation.budget * .002m);
            if (usual <= 0m) { return 0d; }
            double ratio = (double)(crowdRecent / usual);
            return Math.Max(0d, Math.Min(1d, (ratio - 1.5d) / 4.5d));
        }
    }

    private static double Roll() => UnityEngine.Random.value;

    private void TickOperation(float now)
    {
        PlanOperations(now);
        if (!Operating) { StopListening(); return; }
        var stock = OwnedCompany;
        if (stock == null || !stock.AcceptsOrders || PreDisposal)
        {
            // 상장폐지 절차 같은 일이 생기면 작전은 그 자리에서 접는다.
            operation.stage = WhaleOperationState.None; StopListening(); EndTactics(stock);
            if (stock != null) { CancelOperationOrders(stock, true, true, 0f); }
            return;
        }
        Listen(stock);
        // 장이 닫혀 있던 시간은 세지 않는다.
        float seconds = Mathf.Clamp(now - operationClock, 0f, .5f); operationClock = now;
        while (crowdBuys.Count > 0 && now - crowdBuys.Peek().time > HeatWindow) { crowdRecent -= crowdBuys.Dequeue().value; }
        if (operation.stage == WhaleOperationState.Build) { crowdBaselineSeconds += seconds; }
        int stage = operation.stage;
        var order = WhaleOperationPlan.Step(operation, seconds, stock.currentPrice, GetOwnedVolume(stock) - operationBase, OperationHeat, Today(), Roll);
        if (operation.stage != stage) { ReportOperation(stock); }
        // 작전의 큰 줄기와 따로, 단계에 맞는 눈속임(통정매매·허수 매수벽·종가 관리)을 얹는다.
        if (operation.Active) { TickTactics(stock, now); } else { EndTactics(stock); }
        if (order.Kind == WhaleOperationKind.None) { return; }
        SubmitOperation(stock, order);
    }

    private void Listen(Stock stock)
    {
        if (listening.TryGetValue(this, out var current) && current == stock) { return; }
        StopListening();
        stock.OnMatchedTrade += ObserveTrade; listening[this] = stock;
    }
    private void StopListening()
    {
        if (!listening.TryGetValue(this, out var current)) { return; }
        if (current != null) { current.OnMatchedTrade -= ObserveTrade; }
        listening.Remove(this);
    }
    /// <summary>남이 호가를 받아 가며 산 체결만 센다. 내 주문은 세지 않는다.</summary>
    private void ObserveTrade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        // 내 차명 계좌가 받은 것(통정매매)은 따라붙는 매수가 아니다.
        if (operation == null || !operation.Active || ReferenceEquals(buyer, this) || ManipulationTools.IsNominee(buyer) || resting == null || resting.isBuy) { return; }
        decimal value = price * volume;
        crowdBuys.Enqueue((SessionClock.Time, value)); crowdRecent += value;
        if (operation.stage == WhaleOperationState.Build) { crowdBaseline += value; }
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
        crowdBuys.Clear(); crowdRecent = crowdBaseline = 0m; crowdBaselineSeconds = 0f;
        decimal available = Math.Max(0m, cash - ReservedCash);
        // 가진 현금의 50~85%. 시중에 도는 물량 값어치의 25%를 넘기지 않는다(시장이 받아 낼 수 있는 크기여야 한다).
        decimal floating = Math.Max(0m, stock.issuedShares - GetOwnedVolume(stock));
        decimal budget = Math.Min(available * (decimal)(.5f + UnityEngine.Random.value * .35f), floating * stock.currentPrice * BudgetFloatShare);
        // 올려놓고 넘길 물량은 작전으로 산 것만이 아니다. 원래 지분에서도 일부를 내놓는다. 이것도 유통 물량의 12%까지만이다.
        decimal extra = Math.Min(Math.Min(stock.issuedShares * ExtraIssuedShare, floating * ExtraFloatShare), budget / stock.currentPrice * ExtraBudgetMultiple);
        extra = Math.Min(extra, Math.Max(0m, GetHolding(stock)));
        bool fast = Environment.GetEnvironmentVariable("DONDON_WHALE_FAST") == "1";
        WhaleOperationPlan.Begin(operation, stock.currentPrice, budget, extra, day, Roll, fast);
        // 누군가 이 계획을 플레이어에게 흘린다: 어느 종목을 몇 시 몇 분에 끌어올리는지.
        DoorNotes.Instance?.AnnounceAfterTrading(stock, operation.buildSeconds);
        Debug.Log($"[세력] 작전 시작 — {stock.stockName}({stock.symbol}) 시작가 {stock.currentPrice:N0} · 예산 {budget:N0}원 · 목표 +{operation.gain * 100m:0}% · 흔들기 {operation.shakes}회 · 원래 지분에서 {operation.extraShares:N0}주까지 · 매집 {operation.buildSeconds:0}초 / 끌어올리기 {operation.driveSeconds:0}초");
    }

    private void ReportOperation(Stock stock)
    {
        string name = operation.stage switch
        {
            WhaleOperationState.Drive => "끌어올리기", WhaleOperationState.Shake => "흔들기", WhaleOperationState.Repump => "다시 올리기",
            WhaleOperationState.Unwind => "정리", _ => "종료"
        };
        decimal start = operation.startPrice > 0m ? operation.startPrice : stock.currentPrice;
        Debug.Log($"[세력] 작전 {name} — {stock.stockName}({stock.symbol}) 현재가 {stock.currentPrice:N0} ({(stock.currentPrice / start - 1m) * 100m:+0.0;-0.0}%) · 고점 {operation.peakPrice:N0} ({(operation.peakPrice / start - 1m) * 100m:+0.0;-0.0}%) · 산 금액 {operation.spent:N0}원 · 판 금액 {operation.proceeds:N0}원 · 열기 {OperationHeat:0.00} · 흔들기 {operation.shakesDone}/{operation.shakes}");
    }

    private void SubmitOperation(Stock stock, WhaleOperationOrder order)
    {
        if (order.Kind == WhaleOperationKind.Finish) { CancelOperationOrders(stock, true, true, 0f); StopListening(); return; }
        bool buying = order.Kind == WhaleOperationKind.JoinBid || order.Kind == WhaleOperationKind.TakeAsk || order.Kind == WhaleOperationKind.LiftAsk;
        // 오래 걸려 있는 주문은 거둔다. 한 가격에 벽이 쌓이지 않게 한다.
        CancelOperationOrders(stock, true, !buying, buying ? StaleBuySeconds : StaleSellSeconds);
        decimal left = order.Quantity;
        // 한 주문에 낼 수 있는 수량에는 한도가 있다. 큰 수량은 몇 번에 나눠 낸다.
        for (int i = 0; i < OrdersPerStep && left >= 1m; i++)
        {
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
                    // 최우선 매도호가에 나란히 선다. 예전에는 한 칸 아래에 댔는데, 사는 사람이 없으면 자기 주문보다 또 한 칸 아래에 대기를 되풀이해서
                    // 세력 혼자 매도 호가를 계단처럼 끌어내렸다. 이미 그 근처에 내 매도가 걸려 있으면 더 얹지 않고 팔리기를 기다린다.
                    price = ask <= 0m ? PriceTickPolicy.CeilToTick(stock.currentPrice + tick) : ask;
                    if (price < operation.SellFloor || OwnAskNear(stock, price)) { return; }
                    break;
                default:
                    // 매수 호가를 받아 칠 때는 그 호가에 실제로 서 있는 수량만큼만 판다. 남는 수량을 더 낮은 값에 걸어 호가를 차례로 무너뜨리지 않는다.
                    // 바닥선 아래의 매수에는 팔지 않는다.
                    if (bid <= 0m || bid < operation.SellFloor) { return; }
                    price = bid; left = Math.Min(left, BestVolume);
                    break;
            }
            if (price <= 0m) { return; }
            decimal chunk = Math.Min(left, 2500m);
            if (buying) { BuyLimit(price, chunk, 0, stock); }
            else
            {
                chunk = Math.Min(chunk, GetHolding(stock));
                if (chunk < 1m) { return; }
                SellLimit(price, chunk, 0, stock);
            }
            left -= chunk;
            // 줄을 서는 주문과 매수 호가를 받아 치는 주문은 한 번이면 된다. 위로 받아 가는 매수만 이어서 낸다.
            if (order.Kind == WhaleOperationKind.JoinBid || order.Kind == WhaleOperationKind.JoinAsk || order.Kind == WhaleOperationKind.HitBid) { break; }
        }
    }

    /// <summary>이 값이나 그 바로 위(1% 안)에 내 매도 주문이 이미 걸려 있는가.</summary>
    private bool OwnAskNear(Stock stock, decimal price)
    {
        stock.orderBook.CopyOwnerOrders(this, ownOrders);
        foreach (var order in ownOrders)
        {
            if (!order.isBuy && order.price <= price * 1.01m && stock.orderBook.IsActive(order)) { return true; }
        }
        return false;
    }
    /// <summary>방금 Best로 읽은 최우선 호가에 서 있는 수량.</summary>
    private static decimal BestVolume;
    private static decimal Best(Stock stock, bool bid)
    {
        BestVolume = 0m;
        if (stock.orderBook == null) { return 0m; }
        quotes.Clear();
        if (bid) { stock.orderBook.GetTopBids(quotes, 1); }
        else { stock.orderBook.GetTopAsks(quotes, 1); }
        if (quotes.Count > 0) { BestVolume = quotes[0].volume; }
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
