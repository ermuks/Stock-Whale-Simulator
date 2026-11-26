using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>실제 계좌와 호가를 사용하는 자동 주문 세션. 가격이나 잔고를 생성하지 않는다.</summary>
public sealed partial class TradingOperationController : MonoBehaviour
{
    public enum Mode { Standby, Prepared, Build, Drive, Unwind, Paused, Review, Spread, Accumulate }

    // ── 여러 종목을 한꺼번에 ────────────────────────────────────────────────────
    // 예전에는 이 장치가 하나뿐이라 스프레드나 매집을 한 종목에서만 돌릴 수 있었다.
    // 이제 같은 장치를 MaxRuns개 두고, 종목마다 하나씩 맡긴다(한 종목에는 하나만 돈다).
    // 장면에 놓인 첫 장치(root)가 깨어날 때 나머지를 같은 게임오브젝트에 미리 만들어 둔다.
    // 필요할 때 만들지 않고 미리 만드는 까닭: 저장은 장치를 "게임오브젝트 경로 + 몇 번째 컴포넌트"로 찾는다(SessionStore).
    // 저장할 때 있던 장치가 불러올 때 없으면 그 저장 전체가 거부되므로, 개수는 언제나 같아야 한다. MaxRuns를 줄이면 예전 저장을 못 읽는다.
    public const int MaxRuns = 6;
    private static TradingOperationController root;
    private static bool spawning;
    private static readonly List<TradingOperationController> all = new(MaxRuns);
    /// <summary>모든 장치(쉬는 것 포함). 첫째가 장면에 놓인 원래 장치다.</summary>
    public static IReadOnlyList<TradingOperationController> All => all;
    /// <summary>지금 돌고 있는 자동 매매들.</summary>
    public static IEnumerable<TradingOperationController> ActiveRuns
    {
        get
        {
            for (int i = 0; i < all.Count; i++) { if (all[i] != null && all[i].Active) { yield return all[i]; } }
        }
    }
    /// <summary>그 종목에서 돌고 있는 장치. 없으면 null.</summary>
    public static TradingOperationController RunOn(Stock stock)
    {
        if (stock == null) { return null; }
        for (int i = 0; i < all.Count; i++) { if (all[i] != null && all[i].Active && all[i].Target == stock) { return all[i]; } }
        return null;
    }
    /// <summary>
    /// 지금 보고 있는 종목(StockManager.CurrentStock)을 맡은 장치.
    ///  1) 그 종목에서 돌고 있는 장치가 있으면 그것.
    ///  2) 없으면 쉬는 장치: 그 종목을 마지막으로 맡았던 것(방금 끝난 결과가 남아 있다) → 한 번도 안 쓴 것 → 아무 쉬는 것.
    ///  3) 모두 다른 종목에서 돌고 있으면 원래 장치(돌고 있으므로 새로 시작할 수 없다).
    /// 화면은 이 값을 붙들어 두지 말고 그릴 때마다 다시 읽어야 한다(종목을 바꾸면 다른 장치가 나온다).
    /// </summary>
    public static TradingOperationController Instance
    {
        get
        {
            if (root == null) { return null; }
            var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
            TradingOperationController last = null, fresh = null, idle = null;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (item == null || !item.enabled) { continue; }
                if (item.Active)
                {
                    if (stock != null && item.Target == stock) { return item; }
                    continue;
                }
                if (stock != null && item.Target == stock) { if (last == null) { last = item; } }
                else if (item.Target == null) { if (fresh == null) { fresh = item; } }
                else if (idle == null) { idle = item; }
            }
            return last != null ? last : fresh != null ? fresh : idle != null ? idle : root;
        }
    }
    /// <summary>모든 장치가 쓸 계좌와 주문 창구를 한 번에 정한다(태블릿이 올라올 때 부른다).</summary>
    public static void Configure(Trader account, DeviceOrderSession session)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] == null) { continue; }
            all[i].account = account; all[i].spreadSession = session;
        }
    }
    /// <summary>원래 장치의 설정을 물려받는다.</summary>
    private void CopySettings(TradingOperationController source)
    {
        account = source.account; spreadSession = source.spreadSession; spreadRefreshSeconds = source.spreadRefreshSeconds;
        BudgetFraction = source.BudgetFraction; SpreadTicks = source.SpreadTicks;
    }
    /// <summary>계좌나 주문 창구가 비어 있으면 원래 장치의 것을 쓴다(시작 직전에 부른다).</summary>
    private void Adopt()
    {
        if (root != null && root != this)
        {
            if (account == null) { account = root.account; }
            if (spreadSession == null) { spreadSession = root.spreadSession; }
        }
        // 계좌는 하나다. 예전 저장을 불러와 두 번째 계좌(이제 쓰지 않는다)를 가리키게 됐으면 본계좌로 돌린다.
        // PlayerAccounts도 같은 일을 하지만 "지금 보는 종목의 장치"만 살피므로, 쉬고 있던 장치는 여기서 스스로 고친다.
        var accounts = PlayerAccounts.Instance;
        if (accounts == null || accounts.primary == null || accounts.automated == null || accounts.primary == accounts.automated) { return; }
        if (account == accounts.automated) { account = accounts.primary; }
        if (spreadSession != null && spreadSession == accounts.automatedOrders && accounts.primaryOrders != null) { spreadSession = accounts.primaryOrders; }
    }
    /// <summary>이 실행을 시작한 시각(SessionClock). 목록에서 최근 것을 먼저 보여 줄 때 쓴다.</summary>
    public float StartedAt { get; private set; }
    /// <summary>
    /// 이 실행으로 번 돈. 스프레드는 사고팔아 실현한 차익, 매집은 모은 주식의 평가손익(지금 값 − 산 값),
    /// 그 밖의 방식은 평가를 포함한 작업 손익이다.
    /// </summary>
    public decimal RunProfit => Target == null ? 0m : CurrentMode == Mode.Spread ? SpreadRealized : CurrentMode == Mode.Accumulate ? Bought * Target.currentPrice - Spent : MarkedResult;
    /// <summary>방식에 맞게 멈춘다(목록의 ✕ 단추).</summary>
    public void Stop()
    {
        if (SpreadRunning) { CancelSpread(); }
        else if (AccumulateRunning) { CancelAccumulate(); }
        else { Finish(); }
    }

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
        // 원래 장치가 만들고 있는 나머지 장치: 명단에만 올린다(끄지 않는다).
        if (spawning)
        { all.Add(this); return; }
        // 장면에 실수로 둘이 놓였을 때만 뒤의 것을 끈다.
        if (root != null && root != this)
        { enabled = false; return; }
        root = this;
        all.Clear(); all.Add(this);
        spawning = true;
        try
        {
            for (int i = 1; i < MaxRuns; i++)
            {
                var extra = gameObject.AddComponent<TradingOperationController>();
                if (!all.Contains(extra)) { all.Add(extra); }
                extra.CopySettings(this);
            }
        }
        finally
        { spawning = false; }
    }
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks) { stock.OnMatchedTrade -= OnFill; }
        if (Target != null && Active) { Target.OnMatchedTrade += OnFill; }
    }
    void Start()
    {
        Adopt();
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
        Adopt();
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
        StartedAt = SessionClock.Time;
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
        all.Remove(this);
        if (root == this)
        { root = null; }
    }
}
