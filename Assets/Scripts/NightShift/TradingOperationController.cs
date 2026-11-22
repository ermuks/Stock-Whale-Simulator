using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>실제 계좌와 호가를 사용하는 자동 주문 세션. 가격이나 잔고를 생성하지 않는다.</summary>
public sealed partial class TradingOperationController : MonoBehaviour
{
    public enum Mode { Standby, Prepared, Build, Drive, Unwind, Paused, Review, Spread, Accumulate }
    public static TradingOperationController Instance { get; private set; }
    public Trader account;
    public Mode CurrentMode { get; private set; }
    public Stock Target { get; private set; }
    public decimal Budget { get; private set; }
    public decimal Spent { get; private set; }
    public decimal Proceeds { get; private set; }
    public decimal Inventory { get; private set; }
    public decimal Bought { get; private set; }
    public decimal Sold { get; private set; }
    public decimal InitialInventory { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal Result { get; private set; }
    public float Elapsed { get; private set; }
    public string Feedback { get; private set; } = "주문 없이도 시장은 계속 움직입니다.";
    public string Debrief { get; private set; } = "아직 종료한 작업이 없습니다.";
    public bool Active => Target != null && CurrentMode != Mode.Review && CurrentMode != Mode.Standby;
    public decimal MarkedResult => Target == null ? 0m : Proceeds - Spent + Inventory * Target.currentPrice - InitialInventory * EntryPrice;
    public float BudgetFraction { get; private set; } = .05f;
    public int CompletedSessions { get; private set; }
    public int ProfitableSessions { get; private set; }
    public int FillCount { get; private set; }
    public int OutstandingCount => pending.Count;
    public decimal CommittedCash
    {
        get
        {
            decimal result = 0m;
            foreach (var order in pending)
            {
                if (order.isBuy && order.volume > 0m)
                { result += order.price * order.volume; }
            }
            return result;
        }
    }
    private readonly List<Order> pending = new(8), snapshot = new(16);
    private readonly HashSet<Order> beforeSubmit = new();
    private readonly List<(decimal price, decimal volume)> levels = new(3);
    private bool submitting;
    private float nextOrder;
    private static readonly string[] ModeNames = { "대기", "준비 완료", "지정가 매수", "즉시 분할 매수", "분할 매도", "관망", "주문 기록", "스프레드", "매집" };
    public string ModeName => ModeNames[(int)CurrentMode];

    void Awake()
    {
        if (Instance != null && Instance != this)
        { enabled = false; return; }
        Instance = this;
    }
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks) { stock.OnMatchedTrade -= OnFill; }
        if (Target != null && Active) { Target.OnMatchedTrade += OnFill; }
    }
    void Start()
    {
        if (account == null && StockManager.Instance != null)
        { account = StockManager.Instance.playerAccount; }
    }
    public void CycleBudget()
    {
        if (Active)
        { Feedback = "작업 종료 후 다음 예산을 변경할 수 있습니다."; return; }
        // 예산은 2% → 5% → 10% → 25% → 50% 순으로 돈다. 큰 예산이어야 주가를 실제로 밀 수 있다.
        BudgetFraction = BudgetFraction < .03f ? .05f : BudgetFraction < .08f ? .1f : BudgetFraction < .2f ? .25f : BudgetFraction < .4f ? .5f : .02f;
    }
    public bool Prepare()
    {
        if (PlayerAccounts.Instance != null && account == PlayerAccounts.Instance.automated && !PlayerAccounts.Instance.AutomatedUnlocked)
        { Feedback = "2번 계좌가 잠겨 있습니다."; return false; }
        if (Active)
        { Feedback = "진행 중인 작업을 종료한 뒤 새 종목을 준비하세요."; return false; }
        var manager = StockManager.Instance;
        if (account == null || manager == null || !MarketDayController.CanTrade || manager.CurrentStock == null || !manager.CurrentStock.AcceptsOrders)
        { Feedback = "거래 가능한 종목과 계좌가 필요합니다."; return false; }
        decimal available = Math.Max(0m, account.cash - account.ReservedCash);
        Budget = decimal.Floor(available * (decimal)BudgetFraction);
        if (Budget < manager.CurrentStock.currentPrice)
        { Feedback = "선택한 예산으로 1주를 주문할 수 없습니다."; return false; }
        Unsubscribe();
        Target = manager.CurrentStock;
        EntryPrice = Target.currentPrice;
        InitialInventory = Math.Min(account.GetHolding(Target), decimal.Floor(Budget / EntryPrice));
        Inventory = InitialInventory;
        Spent = Proceeds = Bought = Sold = Result = 0m;
        Elapsed = 0f;
        FillCount = 0;
        pending.Clear();
        Target.OnMatchedTrade += OnFill;
        CurrentMode = Mode.Prepared;
        Feedback = "예산이 준비되었습니다. 필요할 때 주문 방식을 선택하세요. 시간 제한은 없습니다.";
        return true;
    }
    public void SetMode(Mode mode)
    {
        if (!Active)
        { Feedback = "먼저 종목과 예산을 준비하세요."; return; }
        if (mode != Mode.Build && mode != Mode.Drive && mode != Mode.Unwind && mode != Mode.Paused)
        { return; }
        CancelPending();
        CurrentMode = mode;
        nextOrder = SessionClock.Time;
        Feedback = mode == Mode.Paused ? "내 미체결 주문을 회수했습니다. 시장은 계속 움직입니다." : ModeName + " 실행. 주문·체결·잔여 물량을 확인하세요.";
    }
    void Update()
    {
        if (!Active || Time.timeScale <= 0f)
        { return; }
        if (CurrentMode == Mode.Spread)
        { RefreshSpread(); return; }
        if (CurrentMode == Mode.Accumulate)
        { RefreshAccumulate(); return; }
        if (Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { Finish("대상 종목 거래 종료"); return; }
        if (!MarketDayController.CanTrade)
        {
            if (MarketDayController.Instance != null && MarketDayController.Instance.Phase != MarketPhase.PreOpen)
            { Finish("장 종료"); }
            return;
        }
        if (!Target.AcceptsOrders)
        { SetMode(Mode.Paused); Feedback = "거래중지: 미체결 주문 회수. 재개 후 직접 다시 실행하세요."; return; }
        if (CurrentMode == Mode.Prepared)
        { return; }
        Elapsed += Time.deltaTime;
        if (CurrentMode == Mode.Paused || SessionClock.Time < nextOrder)
        { return; }
        nextOrder = SessionClock.Time + (CurrentMode == Mode.Drive ? 2f : 3.5f);
        CancelPending();
        SubmitSlice();
    }
    public bool SubmitSlice()
    {
        if (CurrentMode == Mode.Spread || CurrentMode == Mode.Accumulate)
        { return false; }
        if (!Active || !MarketDayController.CanTrade || !Target.AcceptsOrders || CurrentMode == Mode.Prepared || CurrentMode == Mode.Paused)
        { return false; }
        bool buy = CurrentMode != Mode.Unwind;
        if (buy)
        {
            if (CurrentMode == Mode.Drive)
            { Target.orderBook.GetTopAsks(levels, 2); }
            else
            { Target.orderBook.GetTopBids(levels, 1); }
        }
        else
        { Target.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { Feedback = "해당 호가가 비었습니다. 다음 주문 시점을 기다립니다."; return false; }
        decimal price = levels[levels.Count - 1].price;
        // A bounded game action, never an unlimited market sweep into an empty book.
        // 즉시 분할 매수(끌어올리기)는 호가가 얇아져도 5%까지 따라 올라간다.
        if (Math.Abs(price / Target.currentPrice - 1m) > (CurrentMode == Mode.Drive ? .05m : .025m))
        { Feedback = "호가 간격이 큽니다. 현재가 2.5% 이내 호가를 기다립니다."; return false; }
        decimal cashLeft = Math.Max(0m, Math.Min(Budget - Spent - CommittedCash, account.cash - account.ReservedCash));
        // 한 번에 내는 수량은 예산의 1/30(끌어올릴 때는 1/20). 주식 수로 묶지 않아서 예산이 크면 그만큼 세게 민다.
        decimal desired = Math.Max(20m, decimal.Floor(Budget / EntryPrice / (CurrentMode == Mode.Drive ? 20m : 30m)));
        decimal quantity = decimal.Floor(buy ? Math.Min(desired, cashLeft / price) : Math.Min(desired * 2m, Math.Min(Inventory, account.GetHolding(Target))));
        if (quantity < 1m)
        {
            if (!buy)
            { Finish("작업 물량 회수 완료"); }
            else
            { SetMode(Mode.Paused); Feedback = "추가 매수 예산 소진. 회수 시점을 결정하세요."; }
            return false;
        }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try
        {
            if (buy)
            { account.BuyLimit(price, quantity, 0, Target); }
            else
            { account.SellLimit(price, quantity, 0, Target); }
        }
        finally
        { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!beforeSubmit.Contains(order))
            { pending.Add(order); }
        }
        Feedback = $"{(buy ? "매수" : "매도")} {quantity:N0}주 · {price:N0}돈 주문 / 체결은 호가 상황에 따라 달라집니다.";
        return true;
    }
    private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!Active || (!submitting && !pending.Contains(resting)))
        { return; }
        if (CurrentMode==Mode.Spread && buyer!=seller && (ReferenceEquals(buyer,account) || ReferenceEquals(seller,account)))
        {
            bool buy=ReferenceEquals(buyer,account);
            SpreadFills.Insert(0,new DeviceMarketJournal.Fill { Stock=stock,Time=DeviceMarketJournal.FullStamp(),Buy=buy,Price=price,Quantity=volume,Realized=buy ? 0 : (price-(resting?.costBasis ?? account.SubmittedSellCost))*volume });
            if (SpreadFills.Count>32) { SpreadFills.RemoveAt(32); }
        }
        if (ReferenceEquals(buyer, account) && !ReferenceEquals(seller, account))
        {
            Bought += volume; Spent += price * volume; Inventory += volume; FillCount++;
            if (CurrentMode == Mode.Accumulate)
            {
                AccumulateFills.Insert(0,new DeviceMarketJournal.Fill { Stock=stock,Time=DeviceMarketJournal.FullStamp(),Buy=true,Price=price,Quantity=volume });
                if (AccumulateFills.Count>32) { AccumulateFills.RemoveAt(32); }
            }
        }
        else if (ReferenceEquals(seller, account) && !ReferenceEquals(buyer, account))
        {
            Sold += volume; Proceeds += price * volume; Inventory -= volume; FillCount++;
            if (CurrentMode == Mode.Spread)
            { SpreadRealized += (price - resting.costBasis) * volume; }
        }
    }
    private void CancelPending()
    {
        if (Target != null)
        {
            foreach (var order in pending)
            { Target.orderBook.CancelOrder(order, Target); }
        }
        pending.Clear();
    }
    public void Finish(string reason = "직접 종료")
    {
        if (!Active)
        { return; }
        if (SpreadRunning) { SpreadEndedGameMinute=DeviceMarketJournal.GameNow; }
        CancelPending();
        Result = MarkedResult;
        CompletedSessions++;
        if (Result > 0m)
        { ProfitableSessions++; }
        string grade = Result > 0m ? "수익 기록" : Result < 0m ? "손실 기록" : "변동 없음";
        Debrief = $"{grade} · {reason}\n매수 {Bought:N0}주 / 매도 {Sold:N0}주 / 체결 {FillCount:N0}건\n작업 손익 {Result:+#,##0;-#,##0;0}돈 (잔여 평가 포함)\n잔여 {Inventory:N0}주는 계좌에 남습니다.";
        CurrentMode = Mode.Review;
        Feedback = "결산을 확인하고 다른 종목이나 예산으로 다음 작업을 준비하세요.";
        Unsubscribe();
    }
    private void Unsubscribe()
    {
        if (Target != null)
        { Target.OnMatchedTrade -= OnFill; }
    }
    void OnDisable()
    {
        if (Active)
        { Finish("작업 장치 중지"); }
    }
    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
        { Instance = null; }
    }
}
