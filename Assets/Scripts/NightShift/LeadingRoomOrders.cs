using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>방별 조건부 지시와 각 참가자의 실제 주문·체결 원장. 체결을 가정하지 않는다.</summary>
public sealed class LeadingRoomOrders : IDisposable
{
    public enum Stage { Deferred, AwaitBuy, Buying, AwaitSell, Selling, Rejected, Canceled, Completed }
    public sealed class Command
    {
        public long Id;
        public int Room;
        public Stock Stock;
        public decimal Buy, Sell;
        public float IssuedAt;
        public bool Canceled;
        public int IssuedDay;
        public bool EntryReached { get; private set; }
        public bool TargetReached { get; private set; }
        public readonly List<Plan> Observers = new();
        // 실제 체결 이벤트만 입력한다. 진입 조건이 먼저 성립해야 목표 성공으로 인정한다.
        public bool ObserveTrade(decimal price, int day, bool trading)
        {
            if (Canceled || TargetReached || !trading || day != IssuedDay || Buy < 1m || Sell <= Buy || price <= 0m)
            { return false; }
            if (!EntryReached)
            { EntryReached = price <= Buy; return false; }
            if (price < Sell)
            { return false; }
            TargetReached = true; return true;
        }
    }
    public sealed class Plan
    {
        public TradeBot Bot;
        public Command Command;
        public Stage State;
        public decimal Goal, Bought, Remaining, Basis, RealizedCost, Proceeds;
        public float NextAttempt, DecisionAt;
        public bool Reported;
        public readonly List<Order> Pending = new(2);
    }
    private readonly InvestorSociety society;
    private readonly Dictionary<TradeBot, Plan> plans = new();
    private readonly Dictionary<Order, Plan> owners = new();
    private readonly HashSet<Order> before = new();
    private readonly List<Order> snapshot = new();
    private readonly List<Stock> subscribed = new();
    private readonly List<(decimal price, decimal volume)> bids = new(1);
    private Plan submitting;
    private readonly Queue<(TradeBot bot, InvestorForecastObservation observation)> observedResults = new(256);
    public IEnumerable<Plan> Plans => plans.Values;
    public LeadingRoomOrders(InvestorSociety society)
    {
        this.society = society;
        StockManager.OnStockListed += Subscribe;
        StockManager.OnStockDelisted += Delisted;
        foreach (var stock in StockManager.Instance.stocks) { Subscribe(stock); }
    }
    private void Subscribe(Stock stock)
    {
        if (subscribed.Contains(stock))
        { return; }
        subscribed.Add(stock); stock.OnMatchedTrade += Fill;
    }
    public Plan Get(TradeBot bot) => plans.TryGetValue(bot, out var plan) ? plan : null;
    public bool AllowsRegular(TradeBot bot, Stock stock)
    {
        return !plans.TryGetValue(bot, out var plan) || plan.Command.Stock != stock || submitting == plan ||
            plan.State == Stage.Canceled || plan.State == Stage.Completed || plan.State == Stage.Rejected || plan.State == Stage.Deferred;
    }
    public void Consider(TradeBot bot, Command command, double roll, float now)
    {
        if (command == null || command.Canceled || command.Stock == null)
        { return; }
        if (plans.TryGetValue(bot, out var previous))
        {
            if (previous.Command == command || previous.Remaining > 0m)
            { return; }
            Cancel(previous);
        }
        var plan = new Plan { Bot = bot, Command = command, DecisionAt = now + 8f + (float)roll * 20f };
        plans[bot] = plan;
        Decide(plan, roll, now);
    }
    private void Decide(Plan plan, double roll, float now)
    {
        var mind = plan.Bot.Social.Psychology;
        decimal cash = Math.Max(0m, plan.Bot.cash - plan.Bot.ReservedCash);
        if (cash < plan.Command.Buy || roll > mind.Acceptance + .35f)
        {
            plan.State = Stage.Rejected;
            if (!plan.Command.TargetReached && !plan.Command.Observers.Contains(plan)) { plan.Command.Observers.Add(plan); }
            society.Trace(plan.Bot, "명령 거부 #" + plan.Command.Id + " · 가용 현금 " + cash.ToString("N0") + " · 수락 성향 " + mind.Acceptance.ToString("F2"));
            return;
        }
        if (roll >= mind.Acceptance)
        { plan.State = Stage.Deferred; society.Trace(plan.Bot, "명령 보류 #" + plan.Command.Id); return; }
        plan.Goal = Math.Max(1m, decimal.Floor(Math.Min(society.settings.maxOrderShares, cash * (decimal)society.settings.commandCashFraction / plan.Command.Buy)));
        // Remove this bot's old orders on the same instrument before assigning a separate follow ledger.
        plan.Command.Stock.orderBook.CopyOwnerOrders(plan.Bot, snapshot);
        foreach (var order in snapshot) { plan.Command.Stock.orderBook.CancelOrder(order, plan.Command.Stock); }
        plan.State = Stage.AwaitBuy; plan.NextAttempt = now;
        society.Trace(plan.Bot, "명령 수락 #" + plan.Command.Id);
    }
    public void Tick(TradeBot bot, float now, double roll)
    {
        if (!plans.TryGetValue(bot, out var plan) || plan.State == Stage.Completed || plan.State == Stage.Rejected || plan.State == Stage.Canceled)
        { return; }
        var command = plan.Command;
        if (command.Canceled || bot.Social.Psychology.Room != command.Room || !bot.isActiveAndEnabled || command.Stock.IsDelisted)
        { Cancel(plan); return; }
        if (plan.State == Stage.Deferred)
        {
            if (now >= plan.DecisionAt)
            { Decide(plan, roll, now); plan.DecisionAt = now + 20f; }
            if (now - command.IssuedAt > society.settings.commandWait) { Cancel(plan); }
            return;
        }
        if (!MarketDayController.CanTrade || !command.Stock.AcceptsOrders || now < plan.NextAttempt)
        { return; }
        plan.NextAttempt = now + 3f;
        Prune(plan);
        if (plan.Remaining > 0m)
        { bot.Social.Psychology.ObserveRisk((float)((command.Stock.currentPrice / Math.Max(1m, plan.Basis / plan.Remaining) - 1m) * 100m)); }
        bool cutLoss = plan.Remaining > 0m && bot.Social.Psychology.Fear > 70f && roll < bot.Social.Psychology.Fear / 100f;
        if (now - command.IssuedAt > society.settings.commandWait && plan.Remaining <= 0m)
        { Cancel(plan); return; }
        if (plan.Pending.Count > 0)
        {
            if (now - plan.Pending[0].createdAt < 15f && !cutLoss)
            { return; }
            CancelPending(plan);
        }
        if (plan.Remaining > 0m && (command.Stock.currentPrice >= command.Sell || cutLoss))
        {
            decimal price = command.Sell;
            if (cutLoss)
            {
                command.Stock.orderBook.GetTopBids(bids, 1);
                if (bids.Count == 0)
                { return; }
                price = bids[0].price;
            }
            plan.State = Stage.Selling;
            Submit(plan, false, price, Math.Min(plan.Remaining, bot.GetHolding(command.Stock)));
        }
        else if (plan.Bought < plan.Goal && command.Stock.currentPrice <= command.Buy && !cutLoss)
        {
            plan.State = Stage.Buying;
            decimal affordable = decimal.Floor(Math.Max(0m, bot.cash - bot.ReservedCash) / command.Buy);
            Submit(plan, true, command.Buy, Math.Min(plan.Goal - plan.Bought, affordable));
        }
    }
    private void Submit(Plan plan, bool buy, decimal price, decimal quantity)
    {
        quantity = decimal.Floor(quantity);
        if (quantity < 1m)
        { return; }
        var stock = plan.Command.Stock;
        stock.orderBook.CopyOwnerOrders(plan.Bot, snapshot);
        before.Clear(); foreach (var order in snapshot) { before.Add(order); }
        submitting = plan;
        try
        {
            if (buy) { plan.Bot.BuyLimit(price, quantity, 0, stock); }
            else { plan.Bot.SellLimit(price, quantity, 0, stock); }
        }
        finally { submitting = null; }
        stock.orderBook.CopyOwnerOrders(plan.Bot, snapshot);
        foreach (var order in snapshot)
        {
            if (!before.Contains(order)) { plan.Pending.Add(order); owners[order] = plan; }
        }
    }
    private void Fill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (ReferenceEquals(buyer, seller) || volume <= 0m)
        { return; }
        ObserveCommands(stock, price);
        if (owners.TryGetValue(resting, out var passive)) { Attribute(passive, stock, price, volume, buyer, seller); }
        if (submitting != null && submitting != passive) { Attribute(submitting, stock, price, volume, buyer, seller); }
        // A canceled instruction releases its remaining shares to the bot, but subsequent actual sales still settle its history.
        if (seller is TradeBot bot && plans.TryGetValue(bot, out var released) && released.State == Stage.Canceled && released != passive && released != submitting)
        { Attribute(released, stock, price, volume, buyer, seller); }
    }
    private void ObserveCommands(Stock stock, decimal price)
    {
        int day = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
        // 방 3개의 지시만 검사한다. 매 체결마다 전체 봇을 순회하지 않는다.
        foreach (var room in society.Rooms)
        {
            var command = room.Command;
            if (!room.PlayerOwned || command == null || command.Stock != stock || !command.ObserveTrade(price, day, MarketDayController.CanTrade))
            { continue; }
            long eventId = society.NextEventId();
            var observation = new InvestorForecastObservation(eventId, command.Id, command.Room, stock.symbol, command.Buy, command.Sell, Time.time);
            foreach (var plan in command.Observers)
            {
                var bot = plan.Bot;
                if (plan.State != Stage.Rejected || bot == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != command.Room || !bot.Social.Psychology.ActiveAt(Time.time))
                { continue; }
                if (observedResults.Count >= 256) { FlushObservedResults(1); }
                observedResults.Enqueue((bot, observation));
            }
            command.Observers.Clear();
        }
    }
    public void FlushObservedResults(int budget)
    {
        // 목표가가 마감 직전 체결되어도, 이미 목격한 결과는 다음 틱에 안전하게 반영한다.
        for (int i = 0; i < Math.Max(1, budget) && observedResults.Count > 0; i++)
        {
            var result = observedResults.Dequeue();
            if (result.bot == null || result.bot.Social == null)
            { continue; }
            var mind = result.bot.Social.Psychology;
            if (mind.ObserveForecast(result.observation, society.settings)) { society.Trace(result.bot, mind.LastCause); }
        }
    }
    private void Attribute(Plan plan, Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
    {
        if (plan.Command.Stock != stock || plan.Reported)
        { return; }
        if (buyer == plan.Bot)
        {
            plan.Bought += volume; plan.Remaining += volume; plan.Basis += price * volume;
            if (plan.Bought >= plan.Goal) { plan.State = Stage.AwaitSell; }
        }
        else if (seller == plan.Bot && plan.Remaining > 0m)
        {
            decimal quantity = Math.Min(volume, plan.Remaining);
            decimal cost = plan.Basis * quantity / plan.Remaining;
            plan.Basis -= cost; plan.Remaining -= quantity; plan.RealizedCost += cost; plan.Proceeds += price * quantity;
            if (plan.Remaining <= 0m)
            {
                plan.Reported = true; plan.State = Stage.Completed;
                society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time));
            }
        }
    }
    private void Prune(Plan plan)
    {
        for (int i = plan.Pending.Count - 1; i >= 0; i--)
        { if (!plan.Command.Stock.orderBook.IsActive(plan.Pending[i])) { owners.Remove(plan.Pending[i]); plan.Pending.RemoveAt(i); } }
    }
    private void Delisted(Stock stock)
    {
        // Existing delisting policy removes all holdings without compensation. Settle the remaining acquired basis once.
        foreach (var plan in plans.Values)
        {
            if (plan.Command.Stock != stock || plan.Reported)
            { continue; }
            CancelPending(plan);
            if (plan.Remaining <= 0m)
            { plan.State = Stage.Canceled; continue; }
            plan.RealizedCost += plan.Basis; plan.Basis = plan.Remaining = 0m;
            plan.State = Stage.Completed; plan.Reported = true;
            society.RecordOutcome(plan.Bot, new InvestorExperience(society.NextEventId(), plan.Command.Id, plan.Command.Room, stock.symbol, plan.RealizedCost, plan.Proceeds, Time.time, "상장폐지 손실"));
        }
        stock.OnMatchedTrade -= Fill; subscribed.Remove(stock);
    }
    private void CancelPending(Plan plan)
    {
        foreach (var order in plan.Pending)
        { plan.Command.Stock.orderBook.CancelOrder(order, plan.Command.Stock); owners.Remove(order); }
        plan.Pending.Clear();
    }
    public void Cancel(Plan plan)
    {
        if (plan == null)
        { return; }
        CancelPending(plan);
        if (plan.State != Stage.Completed) { plan.State = Stage.Canceled; }
    }
    public void CancelRoom(int room)
    {
        foreach (var plan in plans.Values) { if (plan.Command.Room == room) { Cancel(plan); } }
    }
    public void Forget(TradeBot bot)
    {
        if (!plans.TryGetValue(bot, out var plan))
        { return; }
        CancelPending(plan); plans.Remove(bot);
    }
    public void Dispose()
    {
        StockManager.OnStockListed -= Subscribe;
        StockManager.OnStockDelisted -= Delisted;
        foreach (var stock in subscribed) { stock.OnMatchedTrade -= Fill; }
        foreach (var plan in plans.Values) { CancelPending(plan); }
        plans.Clear(); owners.Clear(); observedResults.Clear();
    }
}
