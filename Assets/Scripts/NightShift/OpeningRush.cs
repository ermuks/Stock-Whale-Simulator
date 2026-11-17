using System;
using System.Collections.Generic;
using UnityEngine;

// 장 시작 몰아주기.
// 장이 열리기 전에 방장이 추종자들에게 "장 시작하자마자 이 값까지 사라"고 시키고, 자기는 그 매수에 물량을 넘긴다.
// 얼마나 띄울지는 방장이 정한다. 세력은 그날그날 +10%에서 +80% 사이로 고르고, 플레이어는 매수 칸에 적은 값이 목표가 된다.
// 세력은 플레이어에게 보이지 않는 자기만의 방에서 하고, 플레이어는 내 리딩방에서 같은 일을 할 수 있다.
// 가격·잔고를 직접 고치지 않는다. 추종자의 매수와 방장의 매도는 모두 실제 주문이고 실제 체결이다.
public sealed class OpeningRush : MonoBehaviour
{
    public static OpeningRush Instance { get; private set; }
    /// <summary>세력이 고르는 목표 폭의 범위, 플레이어가 값을 적지 않았을 때의 폭.</summary>
    public const double MinimumGain = .10d, MaximumGain = .80d;
    public const decimal DefaultGain = .30m;
    /// <summary>방장은 목표 폭의 이만큼 이상 올라온 매수에 물량을 넘긴다. 플레이어 지시의 약속 목표가는 받아 주는 값의 이 배수다.</summary>
    public const decimal DumpShare = .6m, PromiseRatio = 1.15m;
    /// <summary>세력이 하루에 이 확률로 한 번 벌인다.</summary>
    public const float DailyChance = .4f;
    private const float FollowerSpread = 15f, FollowerHold = 45f, RunSeconds = 60f;
    private const int MaxFollowers = 80;

    private sealed class Run
    {
        public Trader Leader;
        public Stock Stock;
        public decimal Reference, Top, ToSell, Sold, Proceeds, FollowerBought, FollowerSpent, Peak;
        /// <summary>방장이 물량을 넘기기 시작하는 값.</summary>
        public decimal Floor => Reference + (Top - Reference) * DumpShare;
        public float StartedAt, NextSell;
        public bool Player, Started;
        public int Day;
        public readonly List<(TradeBot bot, decimal budget, float delay)> Followers = new();
        public readonly HashSet<TradeBot> Set = new();
        public int NextFollower;
    }
    private Run npc, player;
    private int plannedDay = int.MinValue;
    private static TradeBot buyer;
    private readonly List<(decimal price, decimal volume)> quotes = new(1);
    private readonly List<Order> orders = new(16);
    private readonly System.Random random = new(Guid.NewGuid().GetHashCode());

    // 플레이어 예약.
    private int pendingRoom;
    private Stock pendingStock;
    private decimal pendingTop;
    /// <summary>예약해 둔, 회원들이 받아 줄 값.</summary>
    public decimal PlayerTop => pendingTop;
    public Stock PlayerPending => pendingStock;
    public bool PlayerRunning => player != null;
    /// <summary>세력이 지금 몰아주기를 벌이는(또는 예고한) 종목.</summary>
    public Stock NpcStock => npc == null ? null : npc.Stock;
    public string LastReport { get; private set; } = "";

    void Awake() { Instance = this; }
    void OnDestroy()
    {
        Stop(ref npc); Stop(ref player);
        if (Instance == this) { Instance = null; }
    }

    /// <summary>지금 이 봇이 몰아주기 매수 주문을 내는 중인가. 이 주문은 가격 범위·비중 제한을 받지 않는다.</summary>
    public static bool Buying(TradeBot bot)
    {
        if (ReferenceEquals(buyer, bot)) { return true; }
        var commands = InvestorSociety.Instance == null ? null : InvestorSociety.Instance.Orders;
        return commands != null && commands.IsRushSubmitting(bot);
    }

    /// <summary>플레이어가 다음 장 시작에 몰아주기를 예약한다. 장중에는 예약할 수 없다. top은 회원들이 받아 줄 값이고, 지금 값보다 높지 않으면 +30%로 잡는다.</summary>
    public bool SchedulePlayer(int room, Stock stock, decimal top = 0m)
    {
        var society = InvestorSociety.Instance; var accounts = PlayerAccounts.Instance;
        if (society == null || accounts == null || stock == null || !stock.IsListed || stock.IsDelisted || MarketDayController.CanTrade || player != null) { return false; }
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms) { return false; }
        if (!society.IsRoomOpen(room) || accounts.primary.GetHolding(stock) < 1m) { return false; }
        pendingRoom = room; pendingStock = stock;
        pendingTop = PriceTickPolicy.RoundToTick(top > stock.currentPrice ? top : stock.currentPrice * (1m + DefaultGain));
        return true;
    }
    public void CancelPlayer() { pendingStock = null; pendingRoom = 0; pendingTop = 0m; }

    void Update()
    {
        var market = MarketDayController.Instance;
        if (market == null || !market.Ready || StockManager.Instance == null) { return; }
        // 장이 열리기 전(둘째 날부터): 세력 가운데 하나가 자기 방에 미리 지시를 돌린다.
        if (market.Phase == MarketPhase.PreOpen && plannedDay != market.Day)
        {
            plannedDay = market.Day;
            if (npc == null && random.NextDouble() < DailyChance) { npc = PlanNpc(market.Day); }
        }
        if (!MarketDayController.CanTrade) { return; }
        float now = SessionClock.Time;
        if (pendingStock != null) { StartPlayer(now); }
        Tick(ref npc, now); Tick(ref player, now);
    }

    private void StartPlayer(float now)
    {
        var stock = pendingStock; int room = pendingRoom; decimal top = pendingTop; CancelPlayer();
        var society = InvestorSociety.Instance;
        // 밤사이 값이 예약한 값 위로 올라와 있으면 그날 값의 +30%로 다시 잡는다.
        if (top <= stock.currentPrice) { top = PriceTickPolicy.RoundToTick(stock.currentPrice * (1m + DefaultGain)); }
        if (society == null || !stock.AcceptsOrders || !society.IssueRush(room, stock, top, PriceTickPolicy.RoundToTick(top * PromiseRatio))) { return; }
        player = new Run { Leader = PlayerAccounts.Instance.primary, Stock = stock, Reference = stock.currentPrice, Player = true, Day = MarketDayController.Instance.Day };
        player.Top = top; player.ToSell = player.Leader.GetHolding(stock);
        Begin(player, now);
    }

    /// <summary>세력 하나와 그 추종자들을 고른다. 추종자는 어느 방에도 들어가 있지 않은 개인들 가운데 그 세력을 따르는 사람들이다.</summary>
    private Run PlanNpc(int day)
    {
        WhaleBot chosen = null; double best = 0d;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock.Owner is not WhaleBot whale || whale.Operating || !stock.IsListed || stock.IsDelisted || stock.IsDelisting || stock.currentPrice <= 0m || whale.GetHolding(stock) < 1m) { continue; }
            // 세력은 진짜 가치를 안다. 값이 가치보다 비쌀수록 지금 넘기는 편이 이득이라 더 자주 고른다.
            double ratio = stock.realValue > 0m ? (double)(stock.currentPrice / stock.realValue) : 1d;
            double weight = Math.Max(.4d, Math.Min(2.5d, ratio)) * (.5d + random.NextDouble());
            if (weight > best) { best = weight; chosen = whale; }
        }
        if (chosen == null) { return null; }
        var run = new Run { Leader = chosen, Stock = chosen.OwnedCompany, Reference = chosen.OwnedCompany.currentPrice, Day = day };
        // 얼마나 띄울지는 세력이 정한다. 작은 폭이 더 흔하고, 가끔 크게 지른다.
        double roll = random.NextDouble(), gain = MinimumGain + roll * roll * (MaximumGain - MinimumGain);
        run.Top = PriceTickPolicy.RoundToTick(run.Reference * (1m + (decimal)gain));
        decimal total = 0m;
        foreach (var bot in MarketDayController.Instance.Bots)
        {
            if (run.Followers.Count >= MaxFollowers) { break; }
            if (bot == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || bot.IsCompanyOwner || bot.Social == null || bot.Social.Psychology.Room != 0) { continue; }
            uint hash = CompanyBusiness.Hash(run.Stock.symbol + ":follow:" + bot.Social.Id);
            if (hash % 100 >= 8) { continue; }
            decimal cash = Math.Max(0m, bot.cash - bot.ReservedCash), budget = decimal.Floor(cash * (.25m + (hash / 100 % 46) / 100m));
            if (budget < run.Top) { continue; }
            run.Followers.Add((bot, budget, (float)random.NextDouble() * FollowerSpread)); run.Set.Add(bot); total += budget;
        }
        if (run.Followers.Count == 0) { return null; }
        run.Followers.Sort((a, b) => a.delay.CompareTo(b.delay));
        // 넘길 물량: 추종자들이 받아 줄 수 있는 만큼. 가진 주식의 20%, 발행 주식의 3%를 넘기지 않는다.
        run.ToSell = decimal.Floor(Math.Min(total / run.Top, Math.Min(chosen.GetHolding(run.Stock) * .2m, run.Stock.issuedShares * .03m)));
        Debug.Log($"[세력] 장 시작 몰아주기 예고 — {run.Stock.stockName}({run.Stock.symbol}) 기준가 {run.Reference:N0} · 목표 {run.Top:N0} ({(run.Top / run.Reference - 1m) * 100m:+0}%) · 추종자 {run.Followers.Count}명 · 추종 자금 {total:N0}원 · 넘길 물량 {run.ToSell:N0}주");
        return run;
    }

    /// <summary>촬영·검증용: 지금 바로 세력 하나가 몰아주기를 벌이게 한다.</summary>
    public bool ForceNpc()
    {
        if (npc != null || MarketDayController.Instance == null) { return false; }
        npc = PlanNpc(MarketDayController.Instance.Day);
        return npc != null;
    }

    private void Begin(Run run, float now)
    {
        run.Started = true; run.StartedAt = now; run.NextSell = now + 2f; run.Peak = run.Stock.currentPrice;
        run.Stock.OnMatchedTrade += Observe;
    }
    private void Stop(ref Run run)
    {
        if (run != null && run.Started && run.Stock != null) { run.Stock.OnMatchedTrade -= Observe; }
        run = null;
    }
    private void Observe(Stock stock, decimal price, decimal volume, IOrderOwner buyerSide, IOrderOwner seller, Order resting)
    {
        foreach (var run in new[] { npc, player })
        {
            if (run == null || run.Stock != stock || ReferenceEquals(buyerSide, seller)) { continue; }
            run.Peak = Math.Max(run.Peak, price);
            if (ReferenceEquals(seller, run.Leader)) { run.Sold += volume; run.Proceeds += price * volume; }
            if (buyerSide is TradeBot bot && run.Set.Contains(bot)) { run.FollowerBought += volume; run.FollowerSpent += price * volume; }
        }
    }

    private void Tick(ref Run run, float now)
    {
        if (run == null) { return; }
        var stock = run.Stock;
        if (stock == null || stock.IsDelisted || run.Day != MarketDayController.Instance.Day) { Stop(ref run); return; }
        if (!run.Started) { Begin(run, now); }
        float time = now - run.StartedAt;
        if (stock.AcceptsOrders)
        {
            // 세력의 추종자: 저마다 조금씩 다른 때에 방장이 정한 값으로 매수를 넣는다. (플레이어 방의 회원은 리딩방 지시로 움직인다.)
            while (run.NextFollower < run.Followers.Count && run.Followers[run.NextFollower].delay <= time)
            {
                var follower = run.Followers[run.NextFollower++];
                decimal quantity = decimal.Floor(Math.Min(follower.budget, Math.Max(0m, follower.bot.cash - follower.bot.ReservedCash)) / run.Top);
                if (quantity < 1m || !follower.bot.isActiveAndEnabled) { continue; }
                buyer = follower.bot;
                try { follower.bot.BuyLimit(run.Top, quantity, 0, stock); }
                finally { buyer = null; }
            }
            // 방장: 기준가보다 충분히 높은 매수가 서 있으면 거기에 물량을 넘긴다.
            if (now >= run.NextSell && run.Sold < run.ToSell)
            {
                run.NextSell = now + 1f;
                quotes.Clear(); stock.orderBook.GetTopBids(quotes, 1);
                if (quotes.Count > 0 && quotes[0].price >= run.Floor)
                {
                    decimal quantity = decimal.Floor(Math.Min(Math.Min(run.ToSell - run.Sold, quotes[0].volume), run.Leader.GetHolding(stock)));
                    if (quantity >= 1m) { run.Leader.SellLimit(quotes[0].price, quantity, 0, stock); }
                }
            }
        }
        // 받아 주기로 한 시간이 지나면 추종자의 남은 매수는 거둔다.
        if (time >= FollowerHold && run.Followers.Count > 0)
        {
            foreach (var follower in run.Followers) { CancelOrders(stock, follower.bot, true, run.Top); }
            run.Followers.Clear();
        }
        if (time < RunSeconds) { return; }
        // 끝: 방장이 걸어 둔 채 안 팔린 매도는 거둔다.
        CancelOrders(stock, run.Leader, false, run.Floor);
        decimal gain = run.Reference > 0m ? (run.Peak / run.Reference - 1m) * 100m : 0m;
        LastReport = $"{stock.stockName}({stock.symbol}) 기준가 {run.Reference:N0} · 목표 {run.Top:N0} · 고점 {run.Peak:N0} ({gain:+0.0;-0.0}%) · 방장 매도 {run.Sold:N0}주 {run.Proceeds:N0}원" + (run.Player ? "" : $" · 추종 매수 {run.FollowerBought:N0}주 {run.FollowerSpent:N0}원");
        Debug.Log((run.Player ? "[플레이어] " : "[세력] ") + "장 시작 몰아주기 종료 — " + LastReport);
        Stop(ref run);
    }

    private void CancelOrders(Stock stock, IOrderOwner owner, bool buys, decimal floor)
    {
        if (stock.orderBook == null || owner == null) { return; }
        stock.orderBook.CopyOwnerOrders(owner, orders);
        foreach (var order in orders)
        {
            if (order.isBuy == buys && order.price >= floor) { stock.orderBook.CancelOrder(order, stock); }
        }
    }
}

public sealed partial class InvestorSociety
{
    /// <summary>내 리딩방의 장 시작 몰아주기 지시. 회원들은 buy 값까지 따라 사고, sell이 약속한 목표가다.</summary>
    public bool IssueRush(int roomId, Stock stock, decimal buy, decimal sell)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.PlayerOwned || !IsRoomOpen(roomId) || !MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders) { return false; }
        if (buy < 1m || sell <= buy) { return false; }
        CancelRoom(roomId);
        room.Command = new LeadingRoomOrders.Command { Id = ++nextCommand, Room = roomId, Stock = stock, Buy = buy, Sell = sell, Rush = true, IssuedAt = SessionClock.Time, IssuedDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day };
        LastAction = $"{room.DisplayName}: {stock.stockName} 장 시작 몰아주기. 매수 ≤ {buy:N0} / 매도 ≥ {sell:N0}. 참가자가 개별 판단합니다.";
        Speak(null, roomId, LastAction, default); return true;
    }
}
