using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>계좌별 복수 주문과 분할 예약을 관리하고 실제 체결·취소는 기존 Trader/OrderBook에 위임한다.</summary>
public sealed class DeviceOrderSession : MonoBehaviour
{
    public sealed class Request
    {
        public Stock Stock { get; internal set; }
        public int Number { get; internal set; }
        public bool Buy { get; internal set; }
        public decimal Limit { get; internal set; }
        public decimal Quantity { get; internal set; }
        public decimal Filled { get; internal set; }
        public decimal Spent { get; internal set; }
        public decimal Budget { get; internal set; }
        public decimal Unsubmitted { get; internal set; }
        public decimal PendingVolume { get; internal set; }
        public int Parts { get; internal set; }
        public float Interval { get; internal set; }
        public string Status { get; internal set; }
        public string AcceptedAt { get; internal set; }
        public string UpdatedAt { get; internal set; }
        internal Trader Owner;
        internal int Part;
        internal float Next;
        internal readonly List<Order> Pending = new(4);
    }
    public readonly struct PendingEntry
    {
        public readonly Request Request;
        public readonly Stock Stock;
        public readonly Order Order;
        public bool Buy => Request != null ? Request.Buy : Order.isBuy;
        public decimal Price => Request != null ? Request.Limit : Order.price;
        public decimal Remaining => Request != null ? Request.PendingVolume + Request.Unsubmitted : Order.volume;
        public PendingEntry(Request request) { Request = request; Stock = request.Stock; Order = null; }
        public PendingEntry(Stock stock, Order order) { Request = null; Stock = stock; Order = order; }
    }
    public Trader account;
    public Stock Target { get; private set; }
    public Request Latest { get; private set; }
    public IReadOnlyList<Request> ActiveOrders => active;
    public IReadOnlyList<Request> History => history;
    private readonly List<Request> history = new(64);
    public bool Active => active.Count > 0;
    public int Number { get; private set; }
    public int Version { get; private set; }
    public decimal Budget => Latest?.Budget ?? 0m;
    public decimal Spent => Latest?.Spent ?? 0m;
    public decimal Limit => Latest?.Limit ?? 0m;
    public decimal Quantity => Latest?.Quantity ?? 0m;
    public decimal Filled => Latest?.Filled ?? 0m;
    public decimal Unsubmitted => Latest?.Unsubmitted ?? 0m;
    public decimal PendingVolume => Latest?.PendingVolume ?? 0m;
    public decimal Reserved => PendingVolume * Limit;
    public bool Buy => Latest?.Buy ?? true;
    public int Parts => Latest?.Parts ?? 1;
    public float Interval => Latest?.Interval ?? 1f;
    public string Status => Active ? "대기 " + active.Count + "건" : Latest?.Status ?? "미제출";
    public string Feedback { get; private set; } = "가격·수량을 고른 뒤 매수 또는 매도 버튼을 누르세요.";
    public decimal AvailableCash => Math.Max(0m,PlayerAccounts.Available(account)+PlayerAccounts.ScheduledCash(account)-ScheduledBuyCost);
    public decimal RemainingBudget => AvailableCash;
    public decimal ScheduledBuyCost
    {
        get
        {
            decimal value = 0m;
            foreach (var request in active)
            {
                if (request.Buy) { value += request.Unsubmitted * request.Limit; }
            }
            return value;
        }
    }
    private readonly List<Request> active = new(16);
    private readonly List<Order> snapshot = new(16);
    private readonly HashSet<Order> before = new();
    private readonly Dictionary<Order, Request> tracked = new();
    private readonly Dictionary<Stock, int> subscriptions = new();
    private Request submitting;
    private float next;
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.OnMatchedTrade -= OnFill;
            if (subscriptions.ContainsKey(stock)) { stock.OnMatchedTrade += OnFill; }
        }
    }

    void Start()
    {
        if (account == null && StockManager.Instance != null)
        { account = StockManager.Instance.playerAccount; }
    }
    public bool Prepare(Stock stock)
    {
        if (stock == null || account == null)
        { return false; }
        Target = stock;
        return true;
    }
    public decimal AvailableShares(Stock stock)
    {
        if (account == null || stock == null)
        { return 0m; }
        decimal value = account.GetHolding(stock);
        foreach (var request in active)
        {
            if (!request.Buy && request.Stock == stock) { value -= request.Unsubmitted; }
        }
        return Math.Max(0m, value);
    }
    public string Validate(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        if (PlayerAccounts.Instance != null && account == PlayerAccounts.Instance.automated && !PlayerAccounts.Instance.AutomatedUnlocked)
        { return "2번 계좌가 잠겨 있습니다."; }
        if (account == null || stock == null || stock != Target)
        { return "거래할 종목과 계좌를 준비하세요."; }
        if (!MarketDayController.CanTrade || !stock.AcceptsOrders)
        { return "현재 거래할 수 없는 종목 또는 장 상태입니다."; }
        if (!PriceTickPolicy.IsValidPrice(price))
        { return "1원 이상 · 호가 단위 " + PriceTickPolicy.GetTickSize(price).ToString("N0") + "원으로 입력하세요."; }
        if (quantity < 1m || quantity > 1000000m || quantity != decimal.Floor(quantity))
        { return "수량은 1~1,000,000주의 정수로 입력하세요."; }
        if (parts < 1 || parts > 10 || parts > quantity || interval < 1f || interval > 60f)
        { return "분할 횟수는 2~10회, 각 회차는 최소 1주입니다."; }
        if (price > decimal.MaxValue / quantity)
        { return "주문 금액이 너무 큽니다."; }
        decimal cost = price * quantity;
        if (buy && cost > AvailableCash)
        { return "주문 가능 현금이 " + PriceTickPolicy.FormatMoney(cost - AvailableCash) + "원 부족합니다."; }
        if (!buy && quantity > AvailableShares(stock))
        { return "매도 가능 수량은 " + AvailableShares(stock).ToString("N0") + "주입니다."; }
        return "";
    }
    public bool Submit(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        string error = Validate(stock, buy, price, quantity, parts, interval);
        if (!string.IsNullOrEmpty(error))
        { Feedback = error; return false; }
        var request = new Request
        {
            Stock = stock, Owner = account, Number = ++Number, Buy = buy, Limit = price,
            Quantity = quantity, Unsubmitted = quantity, Parts = parts, Interval = interval,
            Budget = AvailableCash, Next = SessionClock.Time, Status = "접수 · 체결 대기"
        };
        request.AcceptedAt=request.UpdatedAt=DeviceMarketJournal.FullStamp();
        Latest = request; active.Add(request); history.Insert(0,request); Version++;
        if (history.Count>64) { history.RemoveAt(64); }
        subscriptions.TryGetValue(stock, out int count);
        if (count == 0) { stock.OnMatchedTrade += OnFill; }
        subscriptions[stock] = count + 1;
        // Reserve synchronously so consecutive clicks cannot spend the same cash.
        SendSlice(request); RefreshPending(request);
        return true;
    }
    void Update()
    {
        if (!Active || Time.timeScale <= 0f || SessionClock.Time < next)
        { return; }
        next = SessionClock.Time + .25f;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var request = active[i];
            if (request.Stock == null || !request.Stock.AcceptsOrders || !MarketDayController.CanTrade)
            { Cancel(request, "거래 종료·중지로 미체결 취소"); continue; }
            if (request.Unsubmitted > 0m && SessionClock.Time >= request.Next)
            { SendSlice(request); }
            RefreshPending(request);
        }
    }
    private void SendSlice(Request request)
    {
        decimal quantity = decimal.Floor(request.Quantity / request.Parts) + (request.Part < request.Quantity % request.Parts ? 1m : 0m);
        if (request.Buy && quantity * request.Limit > AvailableCash+request.Unsubmitted*request.Limit)
        { Cancel(request, "다음 회차의 주문 가능 현금 부족"); return; }
        if (!request.Buy && quantity > request.Owner.GetHolding(request.Stock))
        { Cancel(request, "다음 회차의 매도 가능 수량 부족"); return; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        before.Clear();
        foreach (var order in snapshot) { before.Add(order); }
        request.Unsubmitted -= quantity; request.Part++;
        submitting = request;
        try
        {
            if (request.Buy) { request.Owner.BuyLimit(request.Limit, quantity, 0, request.Stock); }
            else { request.Owner.SellLimit(request.Limit, quantity, 0, request.Stock); }
        }
        finally { submitting = null; }
        request.Stock.orderBook.CopyOwnerOrders(request.Owner, snapshot);
        foreach (var order in snapshot)
        {
            if (!before.Contains(order))
            { request.Pending.Add(order); tracked[order] = request; }
        }
        request.Next = SessionClock.Time + request.Interval;
        Feedback = "#" + request.Number.ToString("D4") + " " + request.Stock.stockName + " · " + request.Part + "/" + request.Parts + "회 접수";
    }
    private void RefreshPending(Request request)
    {
        if (!active.Contains(request))
        { return; }
        request.PendingVolume = 0m;
        for (int i = request.Pending.Count - 1; i >= 0; i--)
        {
            var order = request.Pending[i];
            if (order.volume <= 0m || !request.Stock.orderBook.IsActive(order))
            { tracked.Remove(order); request.Pending.RemoveAt(i); }
            else { request.PendingVolume += order.volume; }
        }
        if (request.Unsubmitted <= 0m && request.PendingVolume <= 0m)
        {
            request.Status = request.Filled >= request.Quantity ? "체결 완료" : "주문 종료";
            Feedback = "#" + request.Number.ToString("D4") + " " + request.Stock.stockName + " · " + request.Status + " · " + request.Filled.ToString("N0") + "주 체결";
            Remove(request);
        }
        else { request.Status = request.Filled > 0m ? "부분 체결" : "접수 · 체결 대기"; }
    }
    private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (buyer == seller)
        { return; }
        Request request = null;
        if (resting != null) { tracked.TryGetValue(resting, out request); }
        if (request == null) { request = submitting; }
        if (request == null || request.Stock != stock)
        { return; }
        if (request.Buy && ReferenceEquals(buyer, request.Owner) || !request.Buy && ReferenceEquals(seller, request.Owner))
        {
            request.Filled += volume;
            request.UpdatedAt=DeviceMarketJournal.FullStamp(); Version++;
            if (request.Buy) { request.Spent += price * volume; }
        }
    }
    public bool Cancel(Request request, string reason = "미체결 취소")
    {
        if (request == null || !active.Contains(request))
        { return false; }
        // A fill may have finished since the list was drawn; do not report it as a cancellation.
        RefreshPending(request);
        if (!active.Contains(request))
        { return false; }
        foreach (var order in request.Pending)
        {
            request.Stock.orderBook.CancelOrder(order, request.Stock);
            tracked.Remove(order);
        }
        request.Pending.Clear(); request.PendingVolume = request.Unsubmitted = 0m;
        request.Status = "미체결 취소됨";
        request.UpdatedAt=DeviceMarketJournal.FullStamp(); Version++;
        Feedback = "#" + request.Number.ToString("D4") + " " + reason + " · 체결된 " + request.Filled.ToString("N0") + "주는 유지됩니다.";
        Remove(request); return true;
    }
    public void Cancel(string reason = "미체결 취소")
    {
        for (int i = active.Count - 1; i >= 0; i--) { Cancel(active[i], reason); }
    }
    private void Remove(Request request)
    {
        active.Remove(request);
        int count = subscriptions[request.Stock] - 1;
        if (count > 0) { subscriptions[request.Stock] = count; }
        else { subscriptions.Remove(request.Stock); request.Stock.OnMatchedTrade -= OnFill; }
    }
    public void CopyPendingOrders(List<PendingEntry> target)
    {
        target.Clear();
        foreach (var request in active) { target.Add(new PendingEntry(request)); }
        // Include automation/legacy orders belonging to this account as well.
        if (account == null || StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CopyOwnerOrders(account, snapshot);
            foreach (var order in snapshot)
            {
                if (!tracked.ContainsKey(order)) { target.Add(new PendingEntry(stock, order)); }
            }
        }
    }
    public bool CancelPending(PendingEntry entry)
    {
        if (entry.Request != null) { return Cancel(entry.Request); }
        if (entry.Stock == null || entry.Order == null || !ReferenceEquals(entry.Order.owner, account))
        { return false; }
        bool canceled = entry.Stock.orderBook.CancelOrder(entry.Order, entry.Stock);
        if (canceled) { Feedback = entry.Stock.stockName + " · 미체결 취소됨"; }
        return canceled;
    }
    void OnDestroy() { Cancel("주문 장치 종료"); }
}
