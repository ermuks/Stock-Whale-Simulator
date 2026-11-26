using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 쓰는 작전 도구 다섯 가지. 세력 봇이 쓰는 것과 같은 수법이고 같은 도구(ManipulationTools)를 쓴다.
// 내 계좌의 실제 현금과 주식으로 실제 주문을 낸다. 가격·잔고·거래량을 지어내지 않는다.
//  - 거래량 부풀리기(통정매매): 차명 계좌와 같은 값에 주고받는다. 45초 동안 돈다. 그 종목 주식을 갖고 있어야 한다.
//  - 허수 매수벽: 현재가 2.5% 아래에 큰 매수 주문을 걸어 둔다. 값이 오르면 따라 올려 걸고, 값이 다가오면 거둔다. 못 거두면 진짜로 체결된다.
//  - 종가 관리: 예약해 두면 장 마감 40초 전부터 정한 돈만큼 사들인다.
//  - 불붙이기: 매도 호가를 한 번에 5%까지 쓸어 올린다. 남은 주문은 바로 거둔다. 그 뒤를 남들이 올리게 만드는 수법이다.
//  - 내려찍기: 가진 주식을 매수 호가에 한 번에 던져 6%까지 내린다. 남은 주문은 바로 거둔다(매도벽으로 남지 않는다).
//
// 켜 두는 도구(부풀리기·매수벽·종가 관리)는 여러 종목에서 한꺼번에 돌릴 수 있다. 예전에는 도구마다 자리가 하나라
// 다른 종목에서 쓰려면 먼저 돌던 것을 꺼야 했다. 이제 (도구, 종목)마다 실행 기록(Run)을 하나씩 둔다. 같은 종목에 같은 도구는 하나만 돈다.
// 종목마다 내가 얼마나 사고팔았고 얼마를 벌었는지도 여기서 센다(Book). 실제 체결에서만 세고, 게임을 켠 동안만 기억한다.
public sealed class PlayerTactics : MonoBehaviour
{
    public static PlayerTactics Instance { get; private set; }
    public const float WashSeconds = 45f, CloseSeconds = 40f;
    public enum Tool { Close, Wash, Wall, Ignite, Slam, Unload }
    /// <summary>불붙이기가 쓸어 올리는 높이와 내려찍기가 받아 치는 깊이.</summary>
    public const decimal IgniteReach = .05m, SlamDepth = .06m;
    /// <summary>
    /// 고른 세기(10·25·50%)에 따라 불붙이기가 쓸어 올리는 높이와 내려찍기가 받아 치는 깊이.
    /// 예전에는 세기와 상관없이 +5%, -6%까지만 갔다. 그래서 호가가 얇은 종목에서는 "쓸 돈 1억"을 골라도 천만 돈어치만 사고 끝났고,
    /// 값은 잠깐 튀었다가 제자리로 돌아왔다. 이제 세게 고르면 그만큼 높이 올리고(깊이 내리고), 화면에 그 폭을 함께 보여 준다.
    /// </summary>
    public static decimal IgniteReachFor(float fraction) => fraction <= .12f ? .05m : fraction <= .3f ? .10m : .20m;
    // 내려찍기의 깊이는 세기와 상관없이 -6%다. 세기는 "가진 것 중 얼마나 던지나"만 정한다.
    // (측정: 전량을 -18%까지 던지게 했더니 불나방이 몰려 +22% 오른 종목을 스스로 -19% 찍어 내려 번 돈을 다 날렸다.)
    public static decimal SlamDepthFor(float fraction) => SlamDepth;

    /// <summary>켜 두는 도구 하나가 한 종목에서 돌고 있는 기록. 화면은 읽기만 한다.</summary>
    public sealed class Run
    {
        public Tool Tool { get; internal set; }
        public Stock Stock { get; internal set; }
        /// <summary>시작한 시각(SessionClock).</summary>
        public float Started { get; internal set; }
        /// <summary>거래량 부풀리기: 끝나는 시각, 지금까지 부풀린 거래대금, 주고받은 횟수.</summary>
        public float Until { get; internal set; }
        public decimal Painted { get; internal set; }
        public int Rounds { get; internal set; }
        /// <summary>허수 매수벽: 벽을 건 값과 쓸 현금의 몫.</summary>
        public decimal Price { get; internal set; }
        public float Fraction { get; internal set; }
        /// <summary>종가 관리: 예산과 지금까지 쓴 돈.</summary>
        public decimal Budget { get; internal set; }
        public decimal Spent { get; internal set; }
        internal float Next;
        internal int Day = -1;
        /// <summary>넘기기: 지금 걸어 둔 매도 주문들의 값.</summary>
        internal readonly List<decimal> Ladder = new(4);
        /// <summary>거래량 부풀리기의 남은 시간(초). 다른 도구는 0.</summary>
        public float Left => Tool == PlayerTactics.Tool.Wash ? Mathf.Max(0f, Until - SessionClock.Time) : 0f;
    }

    /// <summary>한 종목에서 내가 한 일의 합계. 실제 체결과 도구가 돌려준 값만 더한다.</summary>
    public sealed class StockBook
    {
        /// <summary>산 수량과 쓴 돈, 판 수량과 받은 돈(매도 수수료를 뗀 값).</summary>
        public decimal Bought { get; internal set; }
        public decimal Spent { get; internal set; }
        public decimal Sold { get; internal set; }
        public decimal Received { get; internal set; }
        /// <summary>실현 손익: 판 값에서 수수료와 그 주식의 평균 매수 단가를 뺀 것.</summary>
        public decimal Realized { get; internal set; }
        /// <summary>도구별로 한 일: 불붙이기로 산 수량·쓴 돈, 내려찍기로 판 수량·받은 돈, 종가 관리에 쓴 돈, 부풀린 거래대금.</summary>
        public decimal IgniteShares { get; internal set; }
        public decimal IgniteCash { get; internal set; }
        public decimal SlamShares { get; internal set; }
        public decimal SlamCash { get; internal set; }
        public decimal CloseCash { get; internal set; }
        public decimal WashPainted { get; internal set; }
        /// <summary>넘기기로 판 수량.</summary>
        public decimal UnloadShares { get; internal set; }
    }
    private static readonly StockBook NoBook = new StockBook();

    private readonly List<Run> runs = new(8);
    /// <summary>도구마다 가장 최근에 시작한 실행. 끝난 뒤에도 마지막 숫자를 읽을 수 있게 남겨 둔다(점검 도구가 읽는다).</summary>
    private readonly Run[] latest = new Run[6];
    private readonly Dictionary<Stock, StockBook> books = new();
    private readonly HashSet<Stock> watched = new();
    private float nextScan;

    /// <summary>지금 돌고 있는 모든 실행. 시작한 순서대로 들어 있다(마지막이 가장 최근).</summary>
    public IReadOnlyList<Run> Runs => runs;
    /// <summary>그 도구가 그 종목에서 돌고 있으면 그 기록, 아니면 null.</summary>
    public Run Find(Tool tool, Stock stock)
    {
        if (stock == null) { return null; }
        for (int i = runs.Count - 1; i >= 0; i--) { if (runs[i].Tool == tool && runs[i].Stock == stock) { return runs[i]; } }
        return null;
    }
    public bool Running(Tool tool, Stock stock) => Find(tool, stock) != null;
    /// <summary>그 도구가 돌고 있는 종목 수.</summary>
    public int Count(Tool tool)
    {
        int count = 0;
        foreach (var run in runs) { if (run.Tool == tool) { count++; } }
        return count;
    }
    /// <summary>매수벽에 지금 걸려 있는 돈.</summary>
    public decimal Shown(Run run) => run == null || run.Tool != Tool.Wall ? 0m : ManipulationTools.WallVolume(Account, run.Stock, run.Price) * run.Price;
    /// <summary>그 종목에서 내가 한 일의 합계. 아직 아무것도 안 했으면 모두 0이다.</summary>
    public StockBook Book(Stock stock) => stock != null && books.TryGetValue(stock, out var book) ? book : NoBook;

    // 예전에는 도구마다 자리가 하나였다. 그때의 이름으로 읽는 곳(점검 도구)을 위해 "그 도구의 가장 최근 실행"의 값을 돌려준다.
    private Run Newest(Tool tool)
    {
        for (int i = runs.Count - 1; i >= 0; i--) { if (runs[i].Tool == tool) { return runs[i]; } }
        return null;
    }
    private Run Last(Tool tool) => Newest(tool) ?? latest[(int)tool];
    public Stock WashStock => Newest(Tool.Wash)?.Stock;
    public Stock WallStock => Newest(Tool.Wall)?.Stock;
    public Stock CloseStock => Newest(Tool.Close)?.Stock;
    public decimal WashPainted => Last(Tool.Wash)?.Painted ?? 0m;
    public int WashRounds => Last(Tool.Wash)?.Rounds ?? 0;
    public decimal WallPrice => Newest(Tool.Wall)?.Price ?? 0m;
    public decimal WallShown => Shown(Newest(Tool.Wall));
    public decimal CloseBudget => Last(Tool.Close)?.Budget ?? 0m;
    public decimal CloseSpent => Last(Tool.Close)?.Spent ?? 0m;
    public float WashLeft => Newest(Tool.Wash)?.Left ?? 0f;

    /// <summary>마지막으로 일어난 일(화면에 그대로 띄우는 한국어 열쇠. 화면이 언어에 맞게 바꾼다).</summary>
    public string Notice { get; private set; } = "";
    /// <summary>그 일이 일어난 종목. 화면이 다른 종목을 보고 있으면 종목 이름을 앞에 붙여 보여 준다.</summary>
    public Stock NoticeStock { get; private set; }
    private void Say(Stock stock, string notice) { Notice = notice; NoticeStock = stock; }

    public static Trader Account => PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
    private static decimal Free(Trader account) => account == null ? 0m : Math.Max(0m, account.cash - account.ReservedCash);

    /// <summary>
    /// 작전 도구는 차례로 열린다.
    ///  - 불붙이기·내려찍기·종가 관리: 누적 거래대금이 기준(1억 돈)을 넘으면. 내 돈으로 한 번에 사고파는 일이라 일찍 열어 준다.
    ///  - 거래량 부풀리기: 내 리딩방을 연 뒤.
    ///  - 허수 매수벽: 내 기업을 세운 뒤.
    /// 예전에는 종가 관리가 5억, 불붙이기가 리딩방, 내려찍기와 매수벽이 "내 기업 상장"이라 게임의 끝 무렵에야 써 볼 수 있었다.
    /// 진행 정보를 읽을 수 없을 때(검증용 장면 등)는 열려 있는 것으로 본다.
    /// </summary>
    public static bool Unlocked(Tool tool)
    {
        if (PlayerProgression.OpenStart) { return true; }
        var career = PlayerProgression.Instance;
        switch (tool)
        {
            case Tool.Close: case Tool.Ignite: case Tool.Slam: return career == null || career.Turnover >= career.rules.tacticCloseTurnover;
            case Tool.Wash: return career == null || career.CanOperateRooms;
            default: return career == null || CorporateEconomy.Instance == null || career.CompanyFounded || CorporateEconomy.Instance.PlayerCompany != null;
        }
    }
    /// <summary>잠겨 있을 때 보여 줄 조건(한국어 열쇠).</summary>
    public static string LockReason(Tool tool) => tool == Tool.Wash ? "내 리딩방을 연 뒤부터" : tool == Tool.Wall ? "내 기업을 세운 뒤부터" : "누적 거래대금 1억 돈부터";

    void Awake() { Instance = this; StockManager.OnStockListed += Watch; }
    void Start() { WatchAll(); }
    void OnDestroy()
    {
        StockManager.OnStockListed -= Watch;
        foreach (var stock in watched) { stock.OnMatchedTrade -= OnTrade; }
        watched.Clear();
        if (Instance == this) { Instance = null; }
    }

    // ── 종목별 장부 ─────────────────────────────────────────────────────────────
    private void Watch(Stock stock)
    {
        if (stock == null || !watched.Add(stock)) { return; }
        stock.OnMatchedTrade -= OnTrade; stock.OnMatchedTrade += OnTrade;
    }
    /// <summary>상장된 모든 종목의 체결을 듣는다. 이어하기 뒤에 새로 생긴 종목도 놓치지 않게 가끔 다시 훑는다.</summary>
    private void WatchAll()
    {
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks == null) { return; }
        foreach (var stock in manager.stocks) { Watch(stock); }
    }
    private StockBook Entry(Stock stock)
    {
        if (!books.TryGetValue(stock, out var book)) { book = new StockBook(); books.Add(stock, book); }
        return book;
    }
    /// <summary>
    /// 내가 한쪽인 체결만 센다. 양쪽이 모두 나이거나 모두 남이면 세지 않는다.
    /// 차명 계좌와 주고받은 것(거래량 부풀리기)도 세지 않는다. 주식과 돈이 제자리로 돌아오는 거래라 사고판 것이 아니다.
    /// 실현 손익의 기준은 PlayerProgression과 같다: 걸어 둔 내 매도 주문이면 그 주문의 원가, 바로 판 것이면 방금 낸 매도의 원가.
    /// </summary>
    private void OnTrade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool playerBuy = PlayerAccounts.IsPlayer(buyer), playerSell = PlayerAccounts.IsPlayer(seller);
        if (playerBuy == playerSell || stock == null || volume <= 0m) { return; }
        if (ManipulationTools.IsNominee(playerBuy ? seller : buyer)) { return; }
        var book = Entry(stock);
        if (playerBuy) { book.Bought += volume; book.Spent += price * volume; return; }
        decimal revenue = Trader.NetOfSellFee(price * volume);
        book.Sold += volume; book.Received += revenue;
        if (seller is Trader player)
        {
            decimal basis = resting != null && ReferenceEquals(resting.owner, player) ? resting.costBasis : player.SubmittedSellCost;
            book.Realized += revenue - basis * volume;
        }
    }

    // ── 거래량 부풀리기 ─────────────────────────────────────────────────────────
    public bool StartWash(Stock stock)
    {
        var account = Account;
        if (!Unlocked(Tool.Wash)) { Say(stock, LockReason(Tool.Wash)); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "거래 불가"); return false; }
        // 이미 이 종목에서 돌고 있으면 그대로 둔다.
        if (Running(Tool.Wash, stock)) { return true; }
        if (account.GetHolding(stock) < 1m) { Say(stock, "넘길 주식이 없습니다"); return false; }
        Add(new Run { Tool = Tool.Wash, Stock = stock, Until = SessionClock.Time + WashSeconds });
        Say(stock, "거래량 부풀리기 시작");
        SteamAchievements.Unlock(SteamAchievements.TacticWash);
        return true;
    }
    public void StopWash(Stock stock)
    {
        var run = Find(Tool.Wash, stock);
        if (run != null) { runs.Remove(run); Say(stock, "거래량 부풀리기 중지"); }
    }
    /// <summary>돌고 있는 거래량 부풀리기를 모두 멈춘다.</summary>
    public void StopWash()
    {
        for (int i = runs.Count - 1; i >= 0; i--) { if (runs[i].Tool == Tool.Wash) { var stock = runs[i].Stock; runs.RemoveAt(i); Say(stock, "거래량 부풀리기 중지"); } }
    }

    // ── 허수 매수벽 ─────────────────────────────────────────────────────────────
    public bool SetWall(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Wall)) { Say(stock, LockReason(Tool.Wall)); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "거래 불가"); return false; }
        if (Running(Tool.Wall, stock)) { return true; }
        decimal price = ManipulationTools.PlaceWall(account, stock, Free(account) * (decimal)Mathf.Clamp01(fraction));
        if (price <= 0m) { Say(stock, "현금 부족"); return false; }
        Add(new Run { Tool = Tool.Wall, Stock = stock, Price = price, Fraction = fraction, Next = SessionClock.Time + 2f });
        Say(stock, "매수벽을 세웠습니다");
        SteamAchievements.Unlock(SteamAchievements.TacticWall);
        return true;
    }
    public void PullWall(Stock stock)
    {
        var run = Find(Tool.Wall, stock);
        if (run == null) { return; }
        ManipulationTools.PullWall(Account, run.Stock, run.Price); runs.Remove(run);
        Say(stock, "매수벽을 거뒀습니다");
    }
    /// <summary>세워 둔 매수벽을 모두 거둔다.</summary>
    public void PullWall()
    {
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var run = runs[i];
            if (run.Tool != Tool.Wall) { continue; }
            ManipulationTools.PullWall(Account, run.Stock, run.Price); runs.RemoveAt(i);
            Say(run.Stock, "매수벽을 거뒀습니다");
        }
    }

    // ── 불붙이기·내려찍기 ───────────────────────────────────────────────────────
    /// <summary>불붙이기: 쓸 현금의 fraction만큼으로 매도 호가를 한 번에 쓸어 올린다.</summary>
    public bool Ignite(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Ignite)) { Say(stock, LockReason(Tool.Ignite)); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "거래 불가"); return false; }
        decimal cashBefore = account.cash, budget = Free(account) * (decimal)Mathf.Clamp01(fraction);
        decimal bought = ManipulationTools.Ignite(account, stock, budget, IgniteReachFor(fraction));
        Say(stock, bought >= 1m ? "불을 붙였습니다" : "현금 부족");
        if (bought >= 1m)
        {
            // 쓴 돈은 계좌에서 실제로 빠져나간 현금이다(남은 주문은 도구가 이미 거뒀다).
            decimal spent = Math.Max(0m, cashBefore - account.cash);
            var book = Entry(stock); book.IgniteShares += bought; book.IgniteCash += spent;
            SteamAchievements.Unlock(SteamAchievements.TacticIgnite);
            // 쓸어 올린 값을 잠깐 받쳐 준다. 받치지 않으면 몇 초 만에 제자리로 돌아와서 아무도 보지 못한다(측정: +10% 올린 값이 10초 뒤 +1%).
            // 고른 돈에서 남은 만큼으로, 올린 값 바로 아래에 매수 주문을 깔아 IgniteHoldSeconds 동안 지킨다. 값이 더 오르면 따라 올린다.
            var old = Find(Tool.Ignite, stock);
            if (old != null) { EndHold(old, account); runs.Remove(old); }
            decimal left = decimal.Floor(budget - spent);
            if (left >= stock.currentPrice)
            { Add(new Run { Tool = Tool.Ignite, Stock = stock, Budget = left, Until = SessionClock.Time + IgniteHoldSeconds, Fraction = fraction, Painted = account.GetOwnedVolume(stock) }); }
        }
        return bought >= 1m;
    }
    /// <summary>불붙인 값을 받쳐 주는 시간(초)과, 받치는 값이 지금 값보다 얼마나 아래인가.</summary>
    public const float IgniteHoldSeconds = 20f;
    public const decimal IgniteHoldGap = .015m;
    private static void EndHold(Run run, Trader account)
    {
        if (run.Price > 0m) { ManipulationTools.PullWall(account, run.Stock, run.Price); run.Price = 0m; }
    }
    /// <summary>불붙인 뒤 받치기: 1초마다 받치는 값을 확인하고, 값이 오르면 따라 올리고, 체결된 만큼은 예산에서 뺀다. 시간이 다 되면 주문을 거둔다.</summary>
    private bool TickIgnite(Run run, Trader account, float now)
    {
        var stock = run.Stock;
        if (now >= run.Until || !stock.AcceptsOrders || !MarketDayController.CanTrade) { EndHold(run, account); return false; }
        if (now < run.Next) { return true; }
        run.Next = now + 1f;
        // 받치다가 사게 된 주식(받치기 시작할 때보다 늘어난 만큼)은 받친 값에 산 것으로 센다.
        decimal owned = account.GetOwnedVolume(stock);
        if (owned > run.Painted && run.Price > 0m)
        {
            decimal filled = owned - run.Painted, cost = filled * run.Price;
            run.Spent += cost; var book = Entry(stock); book.IgniteShares += filled; book.IgniteCash += cost;
        }
        run.Painted = owned;
        decimal level = PriceTickPolicy.FloorToTick(stock.currentPrice * (1m - IgniteHoldGap));
        if (run.Price > 0m && level < run.Price) { level = run.Price; }
        decimal room = run.Budget - run.Spent;
        if (level < 1m || room < level) { EndHold(run, account); return false; }
        if (level != run.Price || ManipulationTools.WallVolume(account, stock, level) <= 0m)
        {
            EndHold(run, account);
            decimal quantity = decimal.Floor(Math.Min(room, Free(account)) / level);
            if (quantity < 1m) { return false; }
            account.BuyLimit(level, quantity, 0, stock); run.Price = level;
        }
        return true;
    }
    /// <summary>내려찍기: 가진 주식의 fraction만큼을 매수 호가에 한 번에 던진다.</summary>
    public bool Slam(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Slam)) { Say(stock, LockReason(Tool.Slam)); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "거래 불가"); return false; }
        decimal shares = decimal.Floor(account.GetHolding(stock) * (decimal)Mathf.Clamp01(fraction));
        if (shares < 1m) { Say(stock, "던질 주식이 없습니다"); return false; }
        decimal cashBefore = account.cash;
        decimal sold = ManipulationTools.Slam(account, stock, shares, SlamDepthFor(fraction), 0m);
        Say(stock, sold >= 1m ? "내려찍었습니다" : "받아 줄 호가가 없습니다");
        if (sold >= 1m)
        {
            // 받은 돈은 계좌에 실제로 들어온 현금이다(수수료를 뗀 값).
            var book = Entry(stock); book.SlamShares += sold; book.SlamCash += Math.Max(0m, account.cash - cashBefore);
            SteamAchievements.Unlock(SteamAchievements.TacticSlam);
        }
        return sold >= 1m;
    }

    // ── 넘기기 ─────────────────────────────────────────────────────────────────
    /// <summary>넘기기가 도는 시간(초)과, 매도 주문을 거는 높이(지금 값 위로).</summary>
    public const float UnloadSeconds = 90f;
    private static readonly decimal[] UnloadSteps = { .004m, .012m, .022m, .035m };
    private static readonly List<Order> unloadOrders = new(16);
    /// <summary>
    /// 넘기기: 오르는 값에 내 물량을 조금씩 넘긴다. 가진 주식의 fraction만큼을, 지금 값 바로 위 네 칸에 매도 주문으로 나눠 건다.
    /// 몰려든 사람들이 사 올라가면서 그 주문을 받아 간다. 값이 움직이면 1.5초마다 따라가며 다시 건다. 다 팔리거나 90초가 지나면 끝난다.
    /// 내려찍기와 반대다: 내려찍기는 매수 호가에 던져서 값을 끌어내리고, 넘기기는 값을 내리지 않고 사 주는 사람에게만 판다.
    /// (측정: 불나방이 몰려 +140% 오른 종목에서 내려찍기로는 가진 것의 5%밖에 못 팔았다. 급등 중에는 매수 호가가 비어 있기 때문이다.)
    /// </summary>
    public bool StartUnload(Stock stock, float fraction)
    {
        var account = Account;
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "거래 불가"); return false; }
        if (Running(Tool.Unload, stock)) { return true; }
        decimal shares = decimal.Floor(account.GetHolding(stock) * (decimal)Mathf.Clamp01(fraction));
        if (shares < 1m) { Say(stock, "넘길 주식이 없습니다"); return false; }
        Add(new Run { Tool = Tool.Unload, Stock = stock, Budget = shares, Fraction = fraction, Until = SessionClock.Time + UnloadSeconds, Painted = account.GetOwnedVolume(stock) });
        Say(stock, "넘기기 시작");
        return true;
    }
    public void StopUnload(Stock stock)
    {
        var run = Find(Tool.Unload, stock);
        if (run == null) { return; }
        PullLadder(run, Account); runs.Remove(run); Say(stock, "넘기기 중지");
    }
    private static void PullLadder(Run run, Trader account)
    {
        if (run.Ladder.Count == 0 || account == null || run.Stock.orderBook == null) { run.Ladder.Clear(); return; }
        run.Stock.orderBook.CopyOwnerOrders(account, unloadOrders);
        foreach (var order in unloadOrders) { if (!order.isBuy && run.Ladder.Contains(order.price) && run.Stock.orderBook.IsActive(order)) { run.Stock.orderBook.CancelOrder(order, run.Stock); } }
        run.Ladder.Clear();
    }
    private bool TickUnload(Run run, Trader account, float now)
    {
        var stock = run.Stock;
        if (now < run.Next && now < run.Until) { return true; }
        run.Next = now + 1.5f;
        // 걸어 둔 주문을 거두고(묶인 주식이 풀린다), 그 사이에 팔린 만큼을 센다.
        PullLadder(run, account);
        decimal owned = account.GetOwnedVolume(stock);
        if (owned < run.Painted) { decimal sold = run.Painted - owned; run.Spent += sold; var book = Entry(stock); book.UnloadShares += sold; }
        run.Painted = owned;
        decimal left = Math.Min(run.Budget - run.Spent, account.GetHolding(stock));
        if (left < 1m) { Say(stock, "넘기기가 끝났습니다"); return false; }
        if (now >= run.Until || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Say(stock, "넘기기가 끝났습니다"); return false; }
        decimal slice = decimal.Floor(left / UnloadSteps.Length), placed = 0m;
        for (int i = 0; i < UnloadSteps.Length; i++)
        {
            decimal quantity = i == UnloadSteps.Length - 1 ? left - placed : slice;
            if (quantity < 1m) { continue; }
            decimal price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + UnloadSteps[i]));
            if (run.Ladder.Contains(price)) { price = PriceTickPolicy.OneTickAbove(price); }
            account.SellLimit(price, quantity, 0, stock); run.Ladder.Add(price); placed += quantity;
        }
        return true;
    }

    // ── 종가 관리 ───────────────────────────────────────────────────────────────
    public bool ArmClose(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Close)) { Say(stock, LockReason(Tool.Close)); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders) { Say(stock, "거래 불가"); return false; }
        if (Running(Tool.Close, stock)) { return true; }
        decimal budget = decimal.Floor(Free(account) * (decimal)Mathf.Clamp01(fraction));
        if (budget < stock.currentPrice) { Say(stock, "현금 부족"); return false; }
        Add(new Run { Tool = Tool.Close, Stock = stock, Budget = budget, Day = MarketDayController.Instance != null ? MarketDayController.Instance.Day : -1 });
        Say(stock, "종가 관리를 예약했습니다");
        SteamAchievements.Unlock(SteamAchievements.TacticClose);
        return true;
    }
    public void CancelClose(Stock stock)
    {
        var run = Find(Tool.Close, stock);
        if (run != null) { runs.Remove(run); Say(stock, "종가 관리를 취소했습니다"); }
    }
    /// <summary>예약해 둔 종가 관리를 모두 취소한다.</summary>
    public void CancelClose()
    {
        for (int i = runs.Count - 1; i >= 0; i--) { if (runs[i].Tool == Tool.Close) { var stock = runs[i].Stock; runs.RemoveAt(i); Say(stock, "종가 관리를 취소했습니다"); } }
    }

    /// <summary>실행 하나를 멈춘다(화면의 ✕ 단추). 도구에 맞는 방법으로 멈춘다: 매수벽은 주문을 거둔다.</summary>
    public void Stop(Run run)
    {
        if (run == null || !runs.Contains(run)) { return; }
        switch (run.Tool)
        {
            case Tool.Wash: StopWash(run.Stock); break;
            case Tool.Wall: PullWall(run.Stock); break;
            case Tool.Close: CancelClose(run.Stock); break;
            case Tool.Ignite: EndHold(run, Account); runs.Remove(run); break;
            case Tool.Unload: StopUnload(run.Stock); break;
        }
    }
    private void Add(Run run)
    {
        run.Started = SessionClock.Time;
        runs.Add(run); latest[(int)run.Tool] = run;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 2f; WatchAll(); }
        var account = Account; var market = MarketDayController.Instance;
        if (account == null || market == null) { return; }
        float now = SessionClock.Time;
        // 뒤에서부터 돈다. 끝난 실행은 그 자리에서 뺀다.
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            var run = runs[i];
            bool keep = run.Tool == Tool.Wash ? TickWash(run, account, now) : run.Tool == Tool.Wall ? TickWall(run, account, now) : run.Tool == Tool.Ignite ? TickIgnite(run, account, now) : run.Tool == Tool.Unload ? TickUnload(run, account, now) : TickClose(run, account, market, now);
            if (!keep) { runs.Remove(run); }
        }
    }
    private bool TickWash(Run run, Trader account, float now)
    {
        var stock = run.Stock;
        if (now >= run.Until || !stock.AcceptsOrders || account.GetHolding(stock) < 1m) { Say(stock, "거래량 부풀리기가 끝났습니다"); return false; }
        if (MarketDayController.CanTrade && now >= run.Next)
        {
            run.Next = now + 1.8f;
            decimal usual = StockInterestManager.Instance == null ? 0m : (decimal)StockInterestManager.Instance.UsualTurnover(stock);
            decimal equity = account.Equity();
            decimal painted = ManipulationTools.WashRound(account, stock, Math.Max(equity * .002m, Math.Min(equity * .05m, usual * 3m)));
            if (painted > 0m) { run.Painted += painted; run.Rounds++; Entry(stock).WashPainted += painted; }
        }
        return true;
    }
    private bool TickWall(Run run, Trader account, float now)
    {
        if (now < run.Next) { return true; }
        run.Next = now + 2f;
        var stock = run.Stock;
        if (!stock.AcceptsOrders || !MarketDayController.CanTrade) { ManipulationTools.PullWall(account, stock, run.Price); Say(stock, "매수벽을 거뒀습니다"); return false; }
        if (ManipulationTools.WallVolume(account, stock, run.Price) <= 0m) { Say(stock, "매수벽이 체결되거나 사라졌습니다"); return false; }
        if (ManipulationTools.WallThreatened(stock, run.Price)) { ManipulationTools.PullWall(account, stock, run.Price); Say(stock, "값이 다가와 매수벽을 거뒀습니다"); return false; }
        if (ManipulationTools.WallStale(stock, run.Price))
        {
            // 값이 올라 벽이 멀어졌다. 거두고 더 높은 값에 다시 건다.
            ManipulationTools.PullWall(account, stock, run.Price);
            decimal price = ManipulationTools.PlaceWall(account, stock, Free(account) * (decimal)Mathf.Clamp01(run.Fraction));
            if (price <= 0m) { Say(stock, "현금 부족"); return false; }
            run.Price = price; Say(stock, "매수벽을 올려 걸었습니다");
        }
        return true;
    }
    private bool TickClose(Run run, Trader account, MarketDayController market, float now)
    {
        var stock = run.Stock;
        // 상장폐지된 종목의 예약은 조용히 지운다.
        if (stock.IsDelisted || !stock.IsListed) { return false; }
        if (run.Day != market.Day && market.Phase != MarketPhase.Trading) { run.Day = market.Day; }
        bool closing = market.Phase == MarketPhase.Trading && market.RemainingSeconds <= CloseSeconds;
        if (closing && stock.AcceptsOrders && now >= run.Next && stock.orderBook != null && stock.orderBook.TryBest(out _, out decimal ask))
        {
            run.Next = now + 1.5f;
            // 남은 시간에 고르게 나눠 산다. 마지막 몇 초에 몰아서 사면 호가만 비우고 끝난다.
            float stepsLeft = Mathf.Max(1f, market.RemainingSeconds / 1.5f);
            decimal step = Math.Min(Free(account), (run.Budget - run.Spent) / (decimal)stepsLeft * 1.6m);
            decimal quantity = decimal.Floor(step / ask);
            if (quantity >= 1m)
            {
                account.BuyLimit(PriceTickPolicy.CeilToTick(ask * 1.01m), quantity, 0, stock);
                run.Spent += quantity * ask; Entry(stock).CloseCash += quantity * ask; Say(stock, "종가 관리 중");
            }
        }
        if (market.Phase != MarketPhase.Trading && run.Spent > 0m) { Say(stock, "종가 관리가 끝났습니다"); return false; }
        return true;
    }
}
