using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>서로 다른 수급 기억을 가진 가상 군중. 일부 봇의 주문 의도만 바꾸며 가격·캔들·자산을 직접 쓰지 않는다.</summary>
public sealed class MarketRhythmDirector : MonoBehaviour
{
    public enum Rhythm { Quiet, Expansion, ProfitTaking, Recovery }
    public static MarketRhythmDirector Instance { get; private set; }
    /// <summary>리듬 경로가 낸 주문만 표시해, 전략 봇이 직접 낸 대기 주문을 리듬이 지우지 않게 한다.</summary>
    public static bool Submitting { get; private set; }
    /// <summary>대기 주문을 내는 중인가. 가격 범위 제한을 받지 않고 주문 목적이 Resting으로 기록된다.</summary>
    public static bool SubmittingResting { get; private set; }
    [Header("대기 주문(멀리 걸어 두는 주문)")]
    [Tooltip("한 번의 판단에서 대기 주문을 하나 더 걸 확률")]
    [Range(0f, 1f)] public float restingChance = .3f;
    [Tooltip("대기 주문에 쓰는 자산 비율(매수는 현금, 매도는 그 종목 보유량)")]
    [Range(0f, .5f)] public float restingShare = .25f;
    [Tooltip("현재가에서 가장 가까운 대기 주문 거리")]
    [Range(.005f, .1f)] public float restingNear = .02f;
    [Tooltip("현재가에서 가장 먼 대기 주문 거리")]
    [Range(.02f, .18f)] public float restingFar = .1f;
    public int RestingOrders { get; private set; }
    [Range(0f, 1f)] public float participation = .72f;
    public int seed = 8147;
    [Range(.005f, .1f)] public float maximumBookDistance = .025f;
    [Range(.001f, .02f)] public float nearbyQuoteDistance = .005f;
    [Range(.005f, .1f)] public float resourceFraction = .025f;
    private sealed class Tape
    {
        public Rhythm Rhythm;
        public float Remaining, Flow, Activity, PlayerImpulse;
        public decimal Anchor;
    }
    private readonly Dictionary<Stock, Tape> tapes = new();
    private readonly List<Order> orders = new(8);
    private readonly List<(decimal price, decimal volume)> levels = new(2);
    private System.Random random;
    public int DirectedOrders { get; private set; }
    public int Transitions { get; private set; }
    public int NearbyQuotes { get; private set; }
    void Awake()
    {
        Instance = this;
        random = new System.Random(seed);
        StockManager.OnStockListed += Register;
        StockManager.OnStockDelisted += Remove;
    }
    void Start()
    {
        if (StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks)
        { Register(stock); }
    }
    private void Register(Stock stock)
    {
        if (stock==null) { return; }
        stock.OnMatchedTrade -= Observe; stock.OnMatchedTrade += Observe;
        if (tapes.ContainsKey(stock))
        { return; }
        tapes.Add(stock, new Tape { Rhythm = (Rhythm)random.Next(4), Remaining = 25f + (float)random.NextDouble() * 55f, Anchor = stock.currentPrice });
    }
    private void Remove(Stock stock)
    {
        stock.OnMatchedTrade -= Observe;
        tapes.Remove(stock);
    }
    private void Observe(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!tapes.TryGetValue(stock, out var tape) || resting == null || quantity <= 0m)
        { return; }
        float size = Mathf.Min(3f, (float)quantity / 200f);
        float direction = resting.isBuy ? -1f : 1f;
        tape.Flow = Mathf.Clamp(tape.Flow + direction * size, -40f, 40f);
        tape.Activity = Mathf.Min(100f, tape.Activity + size);
        if (PlayerAccounts.IsPlayer(buyer) != PlayerAccounts.IsPlayer(seller))
        { tape.PlayerImpulse = Mathf.Clamp(tape.PlayerImpulse + direction * size, -12f, 12f); }
    }
    void Update()
    {
        if (!MarketDayController.CanTrade)
        { return; }
        foreach (var pair in tapes)
        {
            if (!pair.Key.AcceptsOrders)
            { continue; }
            var tape = pair.Value;
            tape.Flow = Mathf.MoveTowards(tape.Flow, 0f, Time.deltaTime * 2f);
            tape.Activity = Mathf.MoveTowards(tape.Activity, 0f, Time.deltaTime * 1.2f);
            tape.PlayerImpulse = Mathf.MoveTowards(tape.PlayerImpulse, 0f, Time.deltaTime * .5f);
            tape.Remaining -= Time.deltaTime;
            if (tape.Remaining > 0f)
            { continue; }
            // Several possible continuations, with momentum and prior gains affecting the next cohort.
            float gain = tape.Anchor > 0m ? (float)(pair.Key.currentPrice / tape.Anchor - 1m) : 0f;
            if (gain > .1f && random.NextDouble() < .7)
            { tape.Rhythm = Rhythm.ProfitTaking; }
            else if (tape.Rhythm == Rhythm.Quiet)
            { tape.Rhythm = random.NextDouble() < .7 ? Rhythm.Expansion : Rhythm.Recovery; }
            else if (tape.Rhythm == Rhythm.Expansion)
            { tape.Rhythm = random.NextDouble() < .7 ? Rhythm.ProfitTaking : Rhythm.Quiet; }
            else
            { tape.Rhythm = random.NextDouble() < .5 ? Rhythm.Recovery : Rhythm.Quiet; }
            tape.Anchor = pair.Key.currentPrice;
            tape.Remaining = 35f + (float)random.NextDouble() * 55f;
            Transitions++;
        }
    }
    public string Observation(Stock stock)
    {
        if (stock == null || !tapes.TryGetValue(stock, out var tape))
        { return "체결 흐름을 수집하는 중"; }
        string flow = tape.Flow > 7f ? "매수 체결 우세" : tape.Flow < -7f ? "매도 체결 우세" : "매수·매도 힘겨루기";
        return flow + (tape.Activity > 35f ? " · 거래 활발" : tape.Activity > 8f ? " · 거래 지속" : " · 거래 한산");
    }
    public bool TryAct(TradeBot bot, Stock stock, bool promised = false)
    {
        if (bot == null || stock == null || stock.currentPrice <= 0m || !MarketDayController.CanTrade || !stock.AcceptsOrders || bot.IsCompanyOwner || bot.IsExchangeParticipant || bot is WhaleFollowerBot || !tapes.TryGetValue(stock, out var tape) || !promised && random.NextDouble() >= participation)
        { return false; }
        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(bot, stock))
        { return true; }
        stock.orderBook.CopyOwnerOrders(bot, orders);
        int rhythmOrders = 0, regularOrders = 0, restingBuys = 0, restingSells = 0;
        decimal restingSellShares = 0m;
        // 대기 주문은 사람마다 4~10분 유지한다. 가격이 멀어져 의미가 없어지면 거둔다.
        float restingLife = 240f + (CompanyBusiness.Hash("REST:" + (bot.Social == null ? 0 : bot.Social.Id)) % 1000u) * .36f;
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            var order = orders[i];
            if (order.purpose == OrderPurpose.Resting)
            {
                if (SessionClock.Time - order.createdAt > restingLife || Math.Abs(order.price / stock.currentPrice - 1m) > (decimal)restingFar * 1.5m)
                { stock.orderBook.CancelOrder(order, stock); orders.RemoveAt(i); continue; }
                if (order.isBuy)
                { restingBuys++; }
                else
                { restingSells++; restingSellShares += order.volume; }
                continue;
            }
            regularOrders++;
            if (order.purpose != OrderPurpose.Rhythm)
            { continue; }
            if (SessionClock.Time - order.createdAt > 18f)
            { stock.orderBook.CancelOrder(order, stock); orders.RemoveAt(i); regularOrders--; continue; }
            rhythmOrders++;
        }
        if (!promised)
        { TryRest(bot, stock, restingBuys, restingSells, restingSellShares); }
        if (rhythmOrders >= 4 || regularOrders >= 8)
        { return true; }
        // 방향은 감독이 정한 국면이 아니라 이 봇의 믿음, 평단, 그리고 실제로 관찰된 체결 흐름에서 나온다.
        var mind = bot.Social.Psychology;
        float bias = tape.PlayerImpulse * .008f;
        bias += BotMind.BuyBias(bot, stock) * .5f;
        bias += Mathf.Clamp(tape.Flow / 40f, -1f, 1f) * (.04f + bot.Social.Chase * .1f) * (1f - mind.ChartSkill * .6f);
        decimal believed = bot.BelievedPrice(stock);
        if (believed > 0m)
        { bias += Mathf.Clamp((float)(believed / stock.currentPrice - 1m) * 2.5f, -.45f, .45f); }
        decimal basis = bot.GetOwnedAverage(stock);
        if (basis > 0m && bot.GetHolding(stock) > 0m)
        { bias -= Mathf.Clamp((float)(stock.currentPrice / basis - 1m) * .6f, -.12f, .45f); }
        double buyProbability = Mathf.Clamp(.5f + bias, .03f, .97f);
        buyProbability = mind.ConsumePromisedBuyProbability(stock.symbol, buyProbability);
        bool buy = random.NextDouble() < buyProbability;
        decimal room = bot.PositionRoom(stock);
        if (buy && room < stock.currentPrice)
        {
            // 이미 두고 싶은 만큼 들고 있다. 보유자는 일부를 덜어내고, 아니면 이번 턴은 지켜본다.
            if (bot.GetHolding(stock) < 1m)
            { return true; }
            buy = false;
        }
        bool chasing = buy && mind.HasFomoFor(stock);
        // 국면은 거래의 활발함만 정한다. 거래량을 유지하기 위해 남긴다.
        bool active = tape.Rhythm == Rhythm.Expansion || tape.Rhythm == Rhythm.ProfitTaking || tape.Activity > 35f;
        bool cross = random.NextDouble() < (chasing ? .8 : active ? .68 : .3);
        if (buy == cross)
        { stock.orderBook.GetTopAsks(levels, 1); }
        else
        { stock.orderBook.GetTopBids(levels, 1); }
        decimal price = levels.Count == 0 ? stock.currentPrice : levels[0].price;
        if (levels.Count == 0 || Math.Abs(price / stock.currentPrice - 1m) > (decimal)Mathf.Max(.005f, maximumBookDistance))
        {
            // A thin book should invite a modest resting quote, not suppress this investor's turn indefinitely.
            decimal distance = (decimal)Mathf.Clamp(nearbyQuoteDistance, .001f, .02f);
            price = buy ? PriceTickPolicy.FloorToTick(stock.currentPrice * (1m - distance)) : PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + distance));
            price = Math.Max(1m, price); NearbyQuotes++;
        }
        decimal available, slice;
        if (buy)
        {
            // 매수 한 번의 크기는 남은 현금이 아니라 이 종목에 두려는 금액을 기준으로 한다.
            decimal cashShares = Math.Max(0m, bot.cash - bot.ReservedCash) / price;
            decimal target = bot.TargetPositionValue(stock);
            available = room == decimal.MaxValue ? cashShares : Math.Min(cashShares, room / price);
            slice = target == decimal.MaxValue ? cashShares * (decimal)Mathf.Clamp(resourceFraction, .005f, .1f) : target * (chasing ? .15m : .08m) / price;
        }
        else
        {
            available = bot.GetHolding(stock);
            slice = available * (decimal)Mathf.Clamp(resourceFraction, .005f, .1f);
        }
        decimal quantity = decimal.Floor(Math.Min(available, Math.Min(random.Next(active ? 50 : 15, active ? 250 : 90), Math.Max(1m, slice))));
        if (quantity < 1m)
        { return true; }
        Submitting = true;
        try
        {
            if (buy)
            { bot.BuyLimit(price, quantity, 0, stock); }
            else
            { bot.SellLimit(price, quantity, 0, stock); }
        }
        finally
        { Submitting = false; }
        DirectedOrders++;
        return true;
    }
    // 당장 체결할 주문과 별도로, 현재가에서 2~10% 떨어진 가격에 작은 주문을 걸어 둔다.
    // 평소에는 체결되지 않고, 급등·급락 때 받아 주는 물량이 된다. 멀수록 얇게 건다.
    private void TryRest(TradeBot bot, Stock stock, int buys, int sells, decimal sellShares)
    {
        if (restingChance <= 0f || bot.Social == null || bot.ImpulseOrder || random.NextDouble() >= restingChance)
        { return; }
        var mind = bot.Social.Psychology;
        bool buy = buys < 2 && (sells >= 2 || random.NextDouble() < .5);
        if (!buy && sells >= 2)
        { return; }
        // 추격 중인 사람은 아래에 걸어 두지 않고 따라 산다.
        if (buy && mind.HasFomoFor(stock))
        { return; }
        double u = random.NextDouble();
        decimal near = (decimal)restingNear, span = Math.Max(.001m, (decimal)restingFar - near);
        decimal distance = near + span * (decimal)(u * u);
        decimal thin = 1m - (distance - near) / span * .6m;
        decimal share = (decimal)restingShare, price, quantity;
        if (buy)
        {
            // 이미 주문에 묶인 현금이 한도를 넘으면 더 걸지 않는다. 당장 살 돈을 남긴다.
            if (bot.cash <= 0m || bot.ReservedCash > bot.cash * share)
            { return; }
            price = Math.Max(1m, PriceTickPolicy.FloorToTick(stock.currentPrice * (1m - distance)));
            decimal free = Math.Max(0m, bot.cash - bot.ReservedCash);
            quantity = decimal.Floor(Math.Min(free, bot.cash * share / 3m * thin) / price);
        }
        else
        {
            decimal holding = bot.GetHolding(stock);
            if (holding < 1m || sellShares >= holding * share)
            { return; }
            price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + distance));
            quantity = decimal.Floor(Math.Min(holding * share - sellShares, Math.Max(1m, holding * share / 3m * thin)));
        }
        quantity = Math.Min(quantity, 500m);
        if (quantity < 1m)
        { return; }
        SubmittingResting = true;
        try
        {
            if (buy)
            { bot.BuyLimit(price, quantity, 0, stock); }
            else
            { bot.SellLimit(price, quantity, 0, stock); }
        }
        finally
        { SubmittingResting = false; }
        RestingOrders++;
    }
    public void BoundOrder(TradeBot bot, Stock stock, bool buy, ref decimal price, ref decimal quantity)
    {
        if (bot.IsExchangeParticipant || stock == null || stock.currentPrice <= 0m || price <= 0m)
        { return; }
        // 대기 주문은 일부러 멀리 거는 주문이라 가격 범위를 좁히지 않는다. 수량만 제한한다.
        if (SubmittingResting)
        { quantity = Math.Min(quantity, 500m); return; }
        // 충동(투매·추격) 주문은 호가를 몇 단계 치고 나갈 수 있다. 그래도 한 번에 5%를 넘지는 못한다.
        bool impulse = bot.ImpulseOrder || buy && bot.Social != null && bot.Social.Psychology.HasFomoFor(stock);
        decimal band = impulse ? (buy ? .03m : .04m) : .015m;
        quantity = Math.Min(quantity, bot.IsCompanyOwner ? 2500m : impulse ? 500m : 250m);
        price = buy ? Math.Min(price, PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + band))) : Math.Max(price, Math.Max(1m, PriceTickPolicy.FloorToTick(stock.currentPrice * (1m - band))));
    }
    void OnDestroy()
    {
        StockManager.OnStockListed -= Register;
        StockManager.OnStockDelisted -= Remove;
        foreach (var pair in tapes)
        { pair.Key.OnMatchedTrade -= Observe; }
        if (Instance == this)
        { Instance = null; }
    }
}
