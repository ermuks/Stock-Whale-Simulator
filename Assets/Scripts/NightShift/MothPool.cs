using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// 불나방 풀. 급등주·급락주가 생기면 평소 시장에 없던 계좌들이 몰려왔다가, 거래량이 식으면 이익이든 손해든 팔고 나간다.
//
// - 계좌는 미리 만들어 둔다(Size개). 평소에는 돈도 주식도 없이 풀에서 쉰다. 봇이 아니다: 생각하는 루프가 없고, 이 관리자 하나가 대신 움직인다.
// - 어떤 종목의 거래가 평소의 몇 배로 터지면서 값이 크게 움직이면 그 종목으로 보낸다. 한꺼번에 넣지 않는다: 초당 DeployPerSecond명씩 차례로 들어간다.
// - 들어갈 때마다 완전히 새로 세팅된다: 새 돈을 들고 온다(바깥에서 들어온 돈으로 계좌에 기록된다). 그 종목의 유통 물량 값에 맞춰 돈의 크기를 정한다.
// - 매도 호가를 몇 % 뚫고 세 번에 나눠 산다. 그 종목의 거래대금이 가장 뜨거웠을 때의 일부로 식으면(계좌마다 기준이 다르다) 판다.
//   팔 때는 매수 호가를 뚫지 않는다. 서 있는 호가만큼만, 한 종목에서 초당 몇 번까지만 판다(한꺼번에 던지면 서로의 값을 밟고 바닥까지 내려간다).
// - 풀로 돌아가는 조건은 하나다: 주식이 한 주도 없을 것. 못 팔았으면(받아 줄 호가가 없다, 거래정지, 장 마감) 팔릴 때까지 풀로 돌아가지 않는다. 다음 날에도 이어서 판다.
// - 풀로 돌아갈 때 남은 돈은 들고 나간다(계좌는 0돈이 된다). 평균적으로는 비싸게 사서 싸게 파니, 그 차이가 시장에 남는다.
//
// 누구나 보는 것만 본다: 그 종목의 거래대금과 값의 움직임. 진짜 가치도, 누가 올리는지도 모른다. 채팅에는 나오지 않는다.

/// <summary>불나방의 계좌. 봇이 아닌 맨 계좌이고, 번호는 봇들의 번호 줄과 겹치지 않게 따로 준다.</summary>
public sealed class MothAccount : Trader
{
    /// <summary>불나방 계좌 번호의 시작. 풀의 n번째 칸은 언제나 FirstId + n번이다(저장을 불러올 때 같은 칸에 얹힌다).</summary>
    public const int FirstId = 900000;
    private static readonly FieldInfo IdField = typeof(Trader).GetField("<InitialAccountId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo NextField = typeof(Trader).GetField("nextInitialAccountId", BindingFlags.Static | BindingFlags.NonPublic);
    protected override void Awake()
    {
        base.Awake();
        // 봇들의 번호를 한 칸 먹지 않게 되돌린다. 내 번호는 풀이(또는 저장을 불러오는 쪽이) 따로 준다.
        if (NextField != null) { NextField.SetValue(null, Math.Max(0, (int)NextField.GetValue(null) - 1)); }
        MothPool.Adopt(this);
    }
    public void Number(int id) { IdField?.SetValue(this, id); }
    /// <summary>주식을 한 주라도 들고 있는가.</summary>
    public bool HoldsAnything { get { foreach (var pair in holdings) { if (pair.Value > 0m) { return true; } } return false; } }
    public Stock FirstHeld { get { foreach (var pair in holdings) { if (pair.Value > 0m && pair.Key != null) { return pair.Key; } } return null; } }
    /// <summary>저장에 남겨야 하는가: 주식이나 돈을 들고 있을 때만. 풀에서 쉬는 빈 계좌는 저장하지 않는다.</summary>
    public bool Keep => HoldsAnything || cash > 0m;
}

public sealed class MothPool : MonoBehaviour
{
    public static MothPool Instance { get; private set; }
    /// <summary>풀의 크기. 성능을 보면서 조절한다.</summary>
    public const int Size = 500;
    /// <summary>초당 몇 명씩 들여보내는가. 한꺼번에 넣으면 주문이 한 프레임에 몰려 끊긴다.</summary>
    public const float DeployPerSecond = 12f;
    /// <summary>한 프레임에 처리하는 불나방의 수(사고, 팔고, 돌아가는 일). 나머지는 다음 프레임으로 넘긴다.</summary>
    public const int ActionsPerFrame = 24;
    /// <summary>몰려오는 조건: 거래가 평소의 이 배수 이상으로 터지고(StockInterestManager.Heat), 값이 이만큼 올랐거나(급등) 이만큼 빠졌을 때(급락).</summary>
    public const float RallyHeat = 5f, RallyUp = .03f, RallyDown = -.06f;
    /// <summary>뜨거운 정도에 따라 부르는 수: 열기가 4에서 HeatFull까지 오르는 동안 0명에서 풀 전체까지.</summary>
    public const float HeatFull = 20f;
    /// <summary>풀 전체가 한 종목에 다 들어갔을 때 들고 오는 돈: 그 종목 유통 물량 값의 이만큼. 계좌 하나의 돈은 그것을 풀 크기로 나눈 값의 0.5~1.5배다(상한·하한 있음).</summary>
    public const decimal FirepowerShare = .04m, PurseMin = 500_000m, PurseMax = 30_000_000m;
    /// <summary>
    /// 나갈 때의 규칙. 첫 측정에서 150명이 저마다 "지금 매수 호가보다 2~6% 아래"로 던지자 서로의 값을 밟고 내려가 5초 만에 반 토막이 났고, 개미의 투매가 이어져 −98%까지 갔다.
    /// 그래서: 매수 호가를 뚫지 않는다. 최우선 매수 호가에 서 있는 물량만큼만 그 값에 판다. 한 종목에서 초당 ExitPerSecond번까지만 판다.
    /// 최우선 매수 호가가 마지막 체결가보다 ExitGap 넘게 아래에 있으면(호가가 비어 있으면) 던지지 않고 기다린다.
    /// </summary>
    public const float ExitPerSecond = 6f;
    public const decimal ExitGap = .03m;
    /// <summary>식었다고 보는 기준: 10초 거래대금이 가장 뜨거웠던 10초의 이 비율(계좌마다 이 범위에서 다르다) 아래로 내려오면 판다. 그 전에라도 이만큼 지나면 판다.</summary>
    public const float LeaveShareMin = .25f, LeaveShareMax = .6f, MaxHoldSeconds = 150f;
    /// <summary>장 마감 이만큼 전부터는 새로 들어가지 않고, 들어가 있던 계좌는 판다. 한 번 몰렸던 종목에는 이만큼 지나야 다시 몰린다.</summary>
    public const float CloseGuardSeconds = 90f, CooldownSeconds = 180f;

    private enum Phase { Idle, Buying, Holding, Leaving }
    private sealed class Moth { public MothAccount account; public Phase phase; public Stock stock; public Rally rally; public decimal purse; public float nextAt, joinedAt, leaveShare; public int buys; }
    private sealed class Rally
    {
        public Stock stock; public bool up, cooling, forced; public float startedAt, windowAt, deployAt, exitAt;
        public decimal turnover, windowMark, window, windowPeak, brought, taken, startPrice, peakPrice;
        public int wanted, deployed, active;
    }

    private readonly List<Moth> moths = new(Size);
    private readonly Dictionary<MothAccount, Moth> byAccount = new();
    private readonly List<Moth> busy = new(Size);
    private readonly Dictionary<Stock, Rally> rallies = new();
    private readonly List<Rally> rallyList = new();
    private readonly Dictionary<Stock, float> cooldown = new();
    private float scanAt; private int cursor; private bool built;
    private readonly List<(decimal price, decimal volume)> levels = new(2);
    /// <summary>확인용: 지금까지 몰린 횟수, 들어간 계좌 수, 들고 온 돈, 들고 나간 돈. 그 차이가 시장에 남은 돈이다.</summary>
    public static int RalliesTotal, DeployedTotal; public static decimal BroughtTotal, TakenTotal;
    public int IdleCount { get { int count = 0; foreach (var moth in moths) { if (moth.phase == Phase.Idle) { count++; } } return count; } }
    public int BusyCount => busy.Count;
    public int CountOn(Stock stock) { int count = 0; foreach (var moth in busy) { if (moth.stock == stock) { count++; } } return count; }
    public int HoldingOn(Stock stock) { int count = 0; foreach (var moth in busy) { if (moth.stock == stock && moth.account.GetOwnedVolume(stock) > 0m) { count++; } } return count; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Instance = null; RalliesTotal = DeployedTotal = 0; BroughtTotal = TakenTotal = 0m; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; Ensure(); }
    private static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(); }
    private static void Ensure()
    {
        if (Instance != null || StockManager.Instance == null) { return; }
        new GameObject("System :: Moth Pool").AddComponent<MothPool>();
    }

    void Awake()
    {
        Instance = this;
        // 계좌는 처음에 다 만들어 둔다(빈 계좌라 가볍다). 시장에 들어가는 것만 차례로 한다.
        for (int i = 0; i < Size; i++)
        {
            var holder = new GameObject("Area :: Moth Account");
            holder.transform.SetParent(transform, false);
            var account = holder.AddComponent<MothAccount>();
            account.Number(MothAccount.FirstId + 1 + i); account.cash = 0m;
        }
        built = true;
    }
    void OnDestroy()
    {
        foreach (var rally in rallyList) { if (rally.stock != null) { rally.stock.OnMatchedTrade -= Count; } }
        if (Instance == this) { Instance = null; }
    }

    /// <summary>계좌가 생길 때 풀에 올린다(풀이 만든 것도, 저장을 불러오며 되살린 것도).</summary>
    public static void Adopt(MothAccount account)
    {
        var pool = Instance;
        if (pool == null || account == null || pool.byAccount.ContainsKey(account)) { return; }
        var moth = new Moth { account = account }; pool.moths.Add(moth); pool.byAccount.Add(account, moth);
        // 풀이 다 만들어진 뒤에 생긴 계좌는 저장에서 되살아난 것이다. 주식을 들고 있을 수 있으니 한 번 살핀다.
        if (pool.built) { pool.reconnect = true; }
    }
    private bool reconnect;
    /// <summary>저장을 불러온 뒤: 주식을 들고 있는 계좌는 파는 중으로 돌려놓는다. 주식이 있는 채로 풀에서 쉬는 계좌가 없게 한다.</summary>
    public void Reconnect()
    {
        reconnect = false;
        foreach (var moth in moths)
        {
            if (moth.phase != Phase.Idle) { continue; }
            var held = moth.account.FirstHeld;
            if (held != null) { moth.stock = held; moth.rally = null; moth.phase = Phase.Leaving; moth.nextAt = 0f; busy.Add(moth); }
            else if (moth.account.cash != 0m) { moth.account.cash = 0m; }
        }
    }

    private void Count(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    { if (rallies.TryGetValue(stock, out var rally)) { rally.turnover += price * volume; if (price > rally.peakPrice) { rally.peakPrice = price; } } }

    /// <summary>검증용: 이 종목에 지금 몰리게 한다. 들어가고 나가는 방식은 평소와 같다.</summary>
    public bool Force(Stock stock, int wanted)
    {
        if (stock == null || rallies.ContainsKey(stock)) { return false; }
        Begin(stock, true, Mathf.Clamp(wanted, 1, moths.Count), SessionClock.Time).forced = true;
        return true;
    }

    private Rally Begin(Stock stock, bool up, int wanted, float now)
    {
        var rally = new Rally { stock = stock, up = up, wanted = wanted, startedAt = now, windowAt = now, deployAt = now, startPrice = stock.currentPrice, peakPrice = stock.currentPrice };
        rallies.Add(stock, rally); rallyList.Add(rally); stock.OnMatchedTrade += Count; RalliesTotal++;
        Debug.Log($"[불나방] {stock.stockName}({stock.symbol}) {(up ? "급등" : "급락")}에 몰려든다 — {wanted}명까지 · 지금 {stock.currentPrice:N0}돈 · 풀에 쉬는 계좌 {IdleCount}개");
        return rally;
    }

    void Update()
    {
        var market = MarketDayController.Instance; var manager = StockManager.Instance;
        if (market == null || manager == null || !built) { return; }
        if (reconnect) { Reconnect(); }
        float now = SessionClock.Time;
        bool open = MarketDayController.CanTrade && market.Phase == MarketPhase.Trading;
        bool closing = !open || market.RemainingSeconds <= CloseGuardSeconds;
        if (open && !closing && now >= scanAt) { scanAt = now + 1f; Scan(manager, now); }
        for (int i = rallyList.Count - 1; i >= 0; i--) { Tend(rallyList[i], now, open, closing); }
        // 한 프레임에 정해진 수만큼만 움직인다. 나머지는 다음 프레임에 이어서 한다.
        int actions = Math.Min(ActionsPerFrame, busy.Count);
        for (int i = 0; i < actions && busy.Count > 0; i++)
        {
            if (cursor >= busy.Count) { cursor = 0; }
            var moth = busy[cursor];
            if (Act(moth, now, open, closing)) { busy.RemoveAt(cursor); } else { cursor++; }
        }
    }

    /// <summary>몰릴 종목을 찾는다. 이미 몰린 종목은 뜨거워진 만큼 더 부른다.</summary>
    private void Scan(StockManager manager, float now)
    {
        var interest = StockInterestManager.Instance;
        if (interest == null) { return; }
        foreach (var stock in manager.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            float heat = interest.Heat(stock);
            int wanted = Mathf.RoundToInt(moths.Count * Mathf.Clamp01((heat - 4f) / (HeatFull - 4f)));
            if (rallies.TryGetValue(stock, out var rally)) { if (!rally.cooling && wanted > rally.wanted) { rally.wanted = wanted; } continue; }
            if (heat < RallyHeat || wanted < 1 || cooldown.TryGetValue(stock, out float until) && now < until) { continue; }
            float drift = interest.Drift(stock);
            if (drift >= RallyUp) { Begin(stock, true, wanted, now); }
            else if (drift <= RallyDown) { Begin(stock, false, wanted, now); }
        }
    }

    /// <summary>몰린 종목 하나를 돌본다: 식었는지 재고, 차례로 들여보내고, 다 나갔으면 접는다.</summary>
    private void Tend(Rally rally, float now, bool open, bool closing)
    {
        if (now - rally.windowAt >= 10f)
        {
            rally.window = rally.turnover - rally.windowMark; rally.windowMark = rally.turnover; rally.windowAt = now;
            if (rally.window > rally.windowPeak) { rally.windowPeak = rally.window; }
            // 20초는 지켜본 뒤, 10초 거래대금이 가장 뜨거웠던 때의 60% 아래로 내려오면 더 들여보내지 않는다.
            if (!rally.cooling && now - rally.startedAt >= 20f && rally.window < rally.windowPeak * (decimal)LeaveShareMax) { rally.cooling = true; }
        }
        if (closing || !rally.stock.AcceptsOrders) { rally.cooling = true; }
        if (open && !rally.cooling && rally.deployed < rally.wanted && now >= rally.deployAt)
        {
            rally.deployAt = now + 1f / DeployPerSecond;
            Deploy(rally, now);
        }
        if (rally.active > 0 || !rally.cooling && now - rally.startedAt < 20f && rally.deployed < rally.wanted) { return; }
        // 다 나갔다(또는 아무도 못 들어갔다).
        rally.stock.OnMatchedTrade -= Count; rallies.Remove(rally.stock); rallyList.Remove(rally); cooldown[rally.stock] = now + CooldownSeconds;
        decimal start = Math.Max(1m, rally.startPrice);
        Debug.Log($"[불나방] {rally.stock.stockName}({rally.stock.symbol}) 다 떠났다 — {rally.deployed}명 · 들고 온 돈 {rally.brought:N0}돈 · 들고 나간 돈 {rally.taken:N0}돈 · 시장에 남긴 돈 {rally.brought - rally.taken:N0}돈 · 고점 {(rally.peakPrice / start - 1m) * 100m:+0.0;-0.0}% · 지금 {(rally.stock.currentPrice / start - 1m) * 100m:+0.0;-0.0}%");
    }

    private void Deploy(Rally rally, float now)
    {
        Moth moth = null;
        foreach (var candidate in moths) { if (candidate.phase == Phase.Idle && !candidate.account.HoldsAnything) { moth = candidate; break; } }
        if (moth == null) { rally.wanted = rally.deployed; return; }
        // 완전히 새로 세팅한다: 새 돈, 새 기준. 돈의 크기는 그 종목의 유통 물량 값에 맞춘다.
        decimal floatValue = StockManager.Instance.FloatValue(rally.stock);
        decimal typical = floatValue > 0m ? floatValue * FirepowerShare / Math.Max(1, moths.Count) : 5_000_000m;
        moth.purse = decimal.Floor(Math.Max(PurseMin, Math.Min(PurseMax, typical * (decimal)UnityEngine.Random.Range(.5f, 1.5f))));
        moth.account.cash = moth.purse;
        moth.stock = rally.stock; moth.rally = rally; moth.phase = Phase.Buying; moth.buys = 0;
        moth.joinedAt = now; moth.nextAt = now + UnityEngine.Random.Range(0f, 1.5f); moth.leaveShare = UnityEngine.Random.Range(LeaveShareMin, LeaveShareMax);
        busy.Add(moth);
        rally.deployed++; rally.active++; rally.brought += moth.purse; DeployedTotal++; BroughtTotal += moth.purse;
    }

    /// <summary>불나방 하나를 움직인다. 풀로 돌아갔으면 true.</summary>
    private bool Act(Moth moth, float now, bool open, bool closing)
    {
        var stock = moth.stock; var account = moth.account;
        if (stock == null) { return Return(moth); }
        if (now < moth.nextAt) { return false; }
        bool tradable = open && stock.AcceptsOrders && stock.orderBook != null;
        switch (moth.phase)
        {
            case Phase.Buying:
                if (closing || moth.rally == null || moth.rally.cooling) { moth.phase = Phase.Leaving; moth.nextAt = now; return false; }
                if (!tradable || !stock.orderBook.TryBest(out _, out decimal ask)) { moth.nextAt = now + 1f; return false; }
                // 급등에는 매도 호가를 1~3% 뚫고 따라 사고, 급락에는 매도 호가를 받아 줍는다. 가진 돈을 세 번에 나눠 쓴다.
                decimal reach = moth.rally.up ? (decimal)UnityEngine.Random.Range(.01f, .03f) : (decimal)UnityEngine.Random.Range(0f, .01f);
                decimal limit = PriceTickPolicy.CeilToTick(ask * (1m + reach));
                decimal free = Math.Max(0m, account.cash - account.ReservedCash);
                decimal quantity = limit > 0m ? decimal.Floor(Math.Min(free, moth.purse / 3m) / limit) : 0m;
                if (quantity >= 1m)
                {
                    account.BuyLimit(limit, quantity, 0, stock);
                    // 다 못 산 주문은 걸어 두지 않는다.
                    stock.orderBook.CancelAllOrdersByOwner(account, stock);
                }
                moth.buys++; moth.nextAt = now + UnityEngine.Random.Range(1f, 3f);
                if (moth.buys >= 3) { moth.phase = Phase.Holding; }
                return false;
            case Phase.Holding:
                moth.nextAt = now + .5f;
                var rally = moth.rally;
                bool faded = rally == null || rally.cooling && rally.window < rally.windowPeak * (decimal)moth.leaveShare;
                if (closing || faded || now - moth.joinedAt >= MaxHoldSeconds || account.GetOwnedVolume(stock) <= 0m) { moth.phase = Phase.Leaving; moth.nextAt = now; }
                return false;
            default:
                // 판다. 주식이 한 주라도 남아 있으면 풀로 돌아가지 않는다.
                if (stock.orderBook != null) { stock.orderBook.CancelAllOrdersByOwner(account, stock); }
                if (account.GetOwnedVolume(stock) <= 0m) { return Return(moth); }
                moth.nextAt = now + UnityEngine.Random.Range(1f, 3f);
                if (!tradable) { return false; }
                // 한 종목에서 초당 정해진 횟수만 판다. 내 차례가 아니면 조금 뒤에 다시 본다.
                var selling = moth.rally;
                if (selling != null) { if (now < selling.exitAt) { moth.nextAt = now + UnityEngine.Random.Range(.2f, .8f); return false; } selling.exitAt = now + 1f / ExitPerSecond; }
                stock.orderBook.GetTopBids(levels, 1);
                if (levels.Count == 0) { return false; }
                decimal bid = levels[0].price, standing = levels[0].volume;
                // 받아 줄 호가가 멀리 떨어져 있으면(호가가 비었으면) 던지지 않고 기다린다.
                if (bid < stock.currentPrice * (1m - ExitGap)) { return false; }
                decimal held = decimal.Floor(account.GetHolding(stock));
                if (held < 1m || standing < 1m) { return false; }
                // 이익이든 손해든 판다. 다만 매수 호가를 뚫지 않는다: 그 호가에 서 있는 만큼만, 그 값에.
                account.SellLimit(bid, Math.Min(held, decimal.Floor(standing)), 0, stock);
                stock.orderBook.CancelAllOrdersByOwner(account, stock);
                return account.GetOwnedVolume(stock) <= 0m && Return(moth);
        }
    }

    /// <summary>풀로 돌려보낸다. 주식이 남아 있으면 돌려보내지 않는다(false).</summary>
    private bool Return(Moth moth)
    {
        var account = moth.account;
        if (account.HoldsAnything)
        {
            // 다른 종목의 주식이 남아 있을 리는 없지만, 있다면 그것부터 판다.
            moth.stock = account.FirstHeld; moth.phase = Phase.Leaving;
            return false;
        }
        decimal left = Math.Max(0m, account.cash);
        if (moth.rally != null) { moth.rally.taken += left; moth.rally.active = Math.Max(0, moth.rally.active - 1); }
        TakenTotal += left;
        account.cash = 0m;
        moth.phase = Phase.Idle; moth.stock = null; moth.rally = null; moth.purse = 0m; moth.buys = 0;
        return true;
    }
}
