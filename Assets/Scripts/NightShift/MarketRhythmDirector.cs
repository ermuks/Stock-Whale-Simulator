using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>서로 다른 수급 기억을 가진 가상 군중. 일부 봇의 주문 의도만 바꾸며 가격·캔들·자산을 직접 쓰지 않는다.</summary>
public sealed class MarketRhythmDirector : MonoBehaviour
{
    public enum Rhythm { Quiet, Expansion, ProfitTaking, Recovery }
    public static MarketRhythmDirector Instance { get; private set; }
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
        if (stock == null || tapes.ContainsKey(stock))
        { return; }
        tapes.Add(stock, new Tape { Rhythm = (Rhythm)random.Next(4), Remaining = 25f + (float)random.NextDouble() * 55f, Anchor = stock.currentPrice });
        stock.OnMatchedTrade += Observe;
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
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            if (Time.time - orders[i].createdAt > 18f)
            { stock.orderBook.CancelOrder(orders[i], stock); orders.RemoveAt(i); }
        }
        if (orders.Count >= 4)
        { return true; }
        float bias = tape.Rhythm == Rhythm.Expansion ? .18f : tape.Rhythm == Rhythm.ProfitTaking ? -.35f : tape.Rhythm == Rhythm.Recovery ? .02f : -.08f;
        bias += tape.PlayerImpulse * .008f;
        bias += BotMind.BuyBias(bot, stock);
        if (stock.fakeValue > 0m)
        { bias -= Mathf.Clamp((float)(stock.currentPrice / stock.fakeValue - 1m) * .45f, -.2f, .35f); }
        decimal basis = bot.GetOwnedAverage(stock);
        if (basis > 0m && bot.GetHolding(stock) > 0m)
        { bias -= Mathf.Clamp((float)(stock.currentPrice / basis - 1m) * .6f, -.12f, .3f); }
        double buyProbability = Mathf.Clamp(.5f + bias, .2f, .8f);
        buyProbability = bot.Social.Psychology.ConsumePromisedBuyProbability(stock.symbol, buyProbability);
        bool buy = random.NextDouble() < buyProbability;
        bool active = tape.Rhythm == Rhythm.Expansion || tape.Rhythm == Rhythm.ProfitTaking;
        bool cross = random.NextDouble() < (active ? .68 : .3);
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
        decimal available = buy ? Math.Max(0m, bot.cash - bot.ReservedCash) / price : bot.GetHolding(stock);
        decimal quantity = decimal.Floor(Math.Min(available, Math.Min(random.Next(active ? 50 : 15, active ? 250 : 90), Math.Max(1m, available * (decimal)Mathf.Clamp(resourceFraction, .005f, .1f)))));
        if (quantity < 1m)
        { return true; }
        if (buy)
        { bot.BuyLimit(price, quantity, 0, stock); }
        else
        { bot.SellLimit(price, quantity, 0, stock); }
        DirectedOrders++;
        return true;
    }
    public void BoundOrder(TradeBot bot, Stock stock, bool buy, ref decimal price, ref decimal quantity)
    {
        if (bot.IsExchangeParticipant || stock == null || stock.currentPrice <= 0m || price <= 0m)
        { return; }
        quantity = Math.Min(quantity, bot.IsCompanyOwner ? 1000m : 250m);
        price = buy ? Math.Min(price, PriceTickPolicy.CeilToTick(stock.currentPrice * 1.015m)) : Math.Max(price, Math.Max(1m, PriceTickPolicy.FloorToTick(stock.currentPrice * .985m)));
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
