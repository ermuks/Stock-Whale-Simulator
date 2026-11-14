using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>태블릿 주문 입력을 기존 Trader/OrderBook에 전달한다. 예시 체결이나 자산을 생성하지 않는다.</summary>
public sealed class DeviceOrderSession : MonoBehaviour
{
    public Trader account;
    public Stock Target { get; private set; }
    public decimal Budget { get; private set; }
    public decimal Spent { get; private set; }
    public decimal Limit { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Filled { get; private set; }
    public decimal Unsubmitted { get; private set; }
    public bool Buy { get; private set; }
    public bool Active { get; private set; }
    public int Number { get; private set; }
    public int Parts { get; private set; }
    public float Interval { get; private set; }
    public string Status { get; private set; } = "미제출";
    public string Feedback { get; private set; } = "가격·수량을 고른 뒤 매수 또는 매도 버튼을 누르세요.";
    public decimal Reserved => PendingVolume * Limit;
    public decimal PendingVolume { get; private set; }
    public decimal AvailableCash => PlayerAccounts.Available(account);
    // ReservedCash already includes pending buy orders. Only future, unsubmitted slices remain to subtract.
    public decimal RemainingBudget => Math.Max(0m, AvailableCash - (Buy && Active ? Unsubmitted * Limit : 0m));
    private readonly List<Order> pending = new(16), snapshot = new(16);
    private readonly HashSet<Order> before = new();
    private int part;
    private float next;
    private bool submitting;

    void Start()
    {
        if (account == null && StockManager.Instance != null)
        { account = StockManager.Instance.playerAccount; }
    }
    public bool Prepare(Stock stock)
    {
        if (Active || stock == null || account == null)
        { return false; }
        if (Target != null)
        { Target.OnMatchedTrade -= OnFill; }
        Target = stock;
        Target.OnMatchedTrade += OnFill;
        Budget = AvailableCash;
        Spent = Filled = Quantity = PendingVolume = Unsubmitted = 0m;
        pending.Clear();
        Status = "미제출";
        Feedback = "가격·수량 선택 → 매수·매도. 일반 주문은 바로 접수됩니다.";
        return true;
    }
    public string Validate(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        if (PlayerAccounts.Instance != null && account == PlayerAccounts.Instance.automated && !PlayerAccounts.Instance.AutomatedUnlocked)
        { return "2번 계좌가 잠겨 있습니다."; }
        if (account == null || stock == null || stock != Target)
        { return "거래할 종목과 계좌를 준비하세요."; }
        if (!MarketDayController.CanTrade || !stock.AcceptsOrders)
        { return "현재 거래할 수 없는 종목 또는 장 상태입니다."; }
        if (Active)
        { return "진행 중인 주문의 체결 또는 취소 후 제출하세요."; }
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
        if (!buy && quantity > account.GetHolding(stock))
        { return "매도 가능 수량은 " + account.GetHolding(stock).ToString("N0") + "주입니다."; }
        return "";
    }
    public bool Submit(Stock stock, bool buy, decimal price, decimal quantity, int parts, float interval)
    {
        string error = Validate(stock, buy, price, quantity, parts, interval);
        if (!string.IsNullOrEmpty(error))
        { Feedback = error; return false; }
        Buy = buy; Limit = price; Quantity = quantity; Parts = parts; Interval = interval;
        Budget = AvailableCash; Spent = Filled = PendingVolume = 0m;
        Unsubmitted = quantity; part = 0; Number++; Active = true;
        next = Time.time;
        Status = "접수 · 체결 대기";
        Feedback = "주문 #" + Number.ToString("D4") + " 접수 · 지정가 조건에 따라 체결됩니다.";
        return true;
    }
    void Update()
    {
        if (!Active)
        { return; }
        if (Target == null || !Target.AcceptsOrders || !MarketDayController.CanTrade)
        { Cancel("거래 종료·중지로 미체결 취소"); return; }
        if (Time.timeScale <= 0f || Time.time < next)
        { return; }
        RefreshPending();
        if (Unsubmitted > 0m)
        { SendSlice(); }
        else
        { next = Time.time + .25f; }
        if (!Active)
        { return; }
        RefreshPending();
        if (Unsubmitted <= 0m && PendingVolume <= 0m)
        {
            Active = false;
            Status = Filled >= Quantity ? "체결 완료" : "주문 종료";
            Feedback = Status + " · " + Filled.ToString("N0") + "/" + Quantity.ToString("N0") + "주 체결";
        }
        else
        { Status = Filled > 0m ? "부분 체결" : "접수 · 체결 대기"; }
    }
    private void SendSlice()
    {
        decimal quantity = decimal.Floor(Quantity / Parts) + (part < Quantity % Parts ? 1m : 0m);
        if (Buy && quantity * Limit > AvailableCash)
        { Cancel("다음 회차의 주문 가능 현금 부족"); return; }
        if (!Buy && quantity > account.GetHolding(Target))
        { Cancel("다음 회차의 매도 가능 수량 부족"); return; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        before.Clear();
        foreach (var order in snapshot)
        { before.Add(order); }
        Unsubmitted -= quantity; part++;
        submitting = true;
        try
        {
            if (Buy)
            { account.BuyLimit(Limit, quantity, 0, Target); }
            else
            { account.SellLimit(Limit, quantity, 0, Target); }
        }
        finally
        { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!before.Contains(order))
            { pending.Add(order); }
        }
        next = Time.time + (Unsubmitted > 0m ? Interval : .25f);
        Feedback = part + "/" + Parts + "회 제출 · " + Filled.ToString("N0") + "주 실제 체결";
    }
    private void RefreshPending()
    {
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        PendingVolume = 0m;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].volume <= 0m || !snapshot.Contains(pending[i]))
            { pending.RemoveAt(i); }
            else
            { PendingVolume += pending[i].volume; }
        }
    }
    private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!Active || (!submitting && !pending.Contains(resting)) || buyer == seller)
        { return; }
        if (Buy && ReferenceEquals(buyer, account) || !Buy && ReferenceEquals(seller, account))
        {
            Filled += volume;
            if (Buy)
            { Spent += price * volume; }
        }
    }
    public void Cancel(string reason = "미체결 취소")
    {
        if (!Active)
        { return; }
        Active = false;
        foreach (var order in pending)
        { Target.orderBook.CancelOrder(order, Target); }
        pending.Clear(); PendingVolume = Unsubmitted = 0m;
        Status = "미체결 취소됨";
        Feedback = reason + " · 체결된 " + Filled.ToString("N0") + "주는 계좌에 유지됩니다.";
    }
    void OnDestroy()
    {
        Cancel("주문 장치 종료");
        if (Target != null)
        { Target.OnMatchedTrade -= OnFill; }
    }
}
