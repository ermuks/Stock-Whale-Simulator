using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 거래소의 전지적 플레이어 관찰자. 짧은 관찰 간격은 더 최신 정보로 대응함을 뜻한다.</summary>
[DefaultExecutionOrder(250)]
public sealed class IncumbentExchangeAI : MonoBehaviour
{
    [Serializable]
    public sealed class Policy
    {
        public int schemaVersion = 1;
        public float observationSeconds = 5f, responseCooldown = 8f, quoteLifetime = 6f, depthFraction = .15f;
        public int maximumShares = 50;
    }
    public sealed class Position
    {
        public Stock Stock;
        public decimal Owned, Average, Price, PendingBuy, PendingSell, Ownership, Change;
    }
    public sealed class PlayerSnapshot
    {
        public float ObservedAt;
        public decimal Cash, ReservedCash, Equity, Turnover, RealizedProfit;
        public decimal OperationBudget, OperationSpent, OperationInventory, ManualLimit, ManualBudget;
        public string SelectedSymbol, OwnedCompanySymbol, OperationMode, OperationSymbol, OrderSymbol;
        public bool LeadingRights, ManualBuy;
        public decimal ManualRemaining;
        public int Members, Supporters, Opponents, ControlledCompanies;
        public readonly List<Position> Positions = new(32);
        public readonly List<(string Symbol, bool Buy, decimal Price, decimal Quantity, long Priority)> OpenOrders = new(32);
        public readonly List<(string Symbol, bool Buy, decimal Limit, decimal Remaining)> AccountIntents = new(2);
        public readonly List<(int Bot, int Room, float Trust, float Betrayal)> Relationships = new(128);
        public readonly List<(int Room, int Bot)> RoomMembers = new(128);
        public readonly List<(int Room, string Symbol, decimal Buy, decimal Sell)> RoomCommands = new(3);
    }
    public Policy policy = new();
    public ExchangeBot exchange;
    public DeviceOrderSession manual;
    public readonly PlayerSnapshot Snapshot = new();
    public int ObservationCount { get; private set; }
    public int ResponseCount { get; private set; }
    public float NextObservationAt { get; private set; }
    public string LastReason { get; private set; } = "플레이어 관찰 대기";
    public Order ResponseOrder { get; private set; }
    public Stock ResponseStock { get; private set; }
    private float nextResponse, expires;
    private readonly Dictionary<Stock, decimal> previousHoldings = new();
    private readonly Dictionary<Stock, decimal> roomPressure = new();
    private readonly List<Order> orders = new(32);
    private readonly List<Order> secondaryOrders = new(16);
    private readonly List<(decimal price, decimal volume)> levels = new(3);
    void Awake()
    {
        var data = Resources.Load<TextAsset>("Progression/IncumbentExchange");
        if (data != null)
        {
            var loaded = JsonUtility.FromJson<Policy>(data.text);
            if (loaded != null && loaded.schemaVersion == 1)
            { policy = loaded; }
        }
    }
    void Update()
    {
        if (!MarketDayController.CanTrade)
        { ClearResponse(); return; }
        if (SessionClock.Time >= expires)
        { ClearResponse(); }
        Observe(SessionClock.Time);
    }
    public bool Observe(float now)
    {
        var manager = StockManager.Instance;
        if (now < NextObservationAt || manager == null || !manager.SetupConfirmed || manager.playerAccount == null || !MarketDayController.CanTrade)
        { return false; }
        NextObservationAt = now + Mathf.Clamp(policy.observationSeconds,.5f,60f);
        var player = manager.playerAccount;
        Snapshot.ObservedAt = now; Snapshot.Cash = player.cash; Snapshot.ReservedCash = player.ReservedCash + PlayerAccounts.TaxDebt(player); Snapshot.Equity = player.Equity() - PlayerAccounts.TaxDebt(player);
        var secondary = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automated;
        if (secondary != null)
        { Snapshot.Cash += secondary.cash; Snapshot.ReservedCash += secondary.ReservedCash; Snapshot.Equity += secondary.Equity(); }
        Snapshot.AccountIntents.Clear();
        if (manual != null)
        {
            foreach (var request in manual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        var autoManual = PlayerAccounts.Instance == null ? null : PlayerAccounts.Instance.automatedOrders;
        if (autoManual != null)
        {
            foreach (var request in autoManual.ActiveOrders)
            { Snapshot.AccountIntents.Add((request.Stock.symbol,request.Buy,request.Limit,request.Unsubmitted + request.PendingVolume)); }
        }
        Snapshot.SelectedSymbol = manager.CurrentStock == null ? "" : manager.CurrentStock.symbol;
        Snapshot.OwnedCompanySymbol = player.OwnedCompany == null ? "" : player.OwnedCompany.symbol;
        var career = PlayerProgression.Instance;
        Snapshot.LeadingRights = career != null && career.CanOperateRooms;
        Snapshot.Turnover = career == null ? 0m : career.Turnover; Snapshot.RealizedProfit = career == null ? 0m : career.RealizedProfit;
        var operation = TradingOperationController.Instance;
        Snapshot.OperationMode = operation == null ? "" : operation.ModeName;
        Snapshot.OperationSymbol = operation == null || operation.Target == null ? "" : operation.Target.symbol;
        Snapshot.OperationBudget = operation == null ? 0m : operation.Budget;
        Snapshot.OperationSpent = operation == null ? 0m : operation.Spent;
        Snapshot.OperationInventory = operation == null ? 0m : operation.Inventory;
        Snapshot.OrderSymbol = manual?.Latest != null ? manual.Latest.Stock.symbol : "";
        Snapshot.ManualBuy = manual != null && manual.Buy;
        Snapshot.ManualRemaining = manual != null && manual.Active ? manual.Unsubmitted + manual.PendingVolume : 0m;
        Snapshot.ManualLimit = manual == null ? 0m : manual.Limit; Snapshot.ManualBudget = manual == null ? 0m : manual.Budget;
        Snapshot.Members = Snapshot.Supporters = Snapshot.Opponents = Snapshot.ControlledCompanies = 0;
        Snapshot.RoomCommands.Clear(); Snapshot.RoomMembers.Clear(); Snapshot.Relationships.Clear(); Snapshot.OpenOrders.Clear(); roomPressure.Clear();
        if (InvestorSociety.Instance != null)
        {
            foreach (var room in InvestorSociety.Instance.Rooms)
            {
                if (!room.PlayerOwned)
                { continue; }
                Snapshot.Members += room.Members.Count;
                foreach (int member in room.Members)
                { Snapshot.RoomMembers.Add((room.Id,member)); }
                if (room.Command != null && !room.Command.Canceled)
                { Snapshot.RoomCommands.Add((room.Id,room.Command.Stock.symbol,room.Command.Buy,room.Command.Sell)); }
            }
            if (InvestorSociety.Instance.Orders != null)
            {
                foreach (var plan in InvestorSociety.Instance.Orders.Plans)
                {
                    if (plan.Command.Canceled || plan.Bot == null || !plan.Bot.isActiveAndEnabled)
                    { continue; }
                    decimal intent = plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying ? Math.Max(0m,plan.Goal - plan.Bought) : plan.State == LeadingRoomOrders.Stage.AwaitSell || plan.State == LeadingRoomOrders.Stage.Selling ? -plan.Remaining : 0m;
                    roomPressure.TryGetValue(plan.Command.Stock,out decimal previous);
                    roomPressure[plan.Command.Stock] = previous + intent;
                }
            }
        }
        if (MarketDayController.Instance != null)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (bot == null || bot.Social == null)
                { continue; }
                var mind = bot.Social.Psychology;
                Snapshot.Relationships.Add((bot.Social.Id,mind.Room,mind.Trust,mind.Betrayal));
                mind.RefreshQuadrant();
                if (mind.Quadrant == InvestorQuadrant.Loyal) { Snapshot.Supporters++; }
                if (mind.Quadrant == InvestorQuadrant.Hostile) { Snapshot.Opponents++; }
            }
        }
        int positionCount = manager.stocks.Count + manager.UnlistedStocks.Count;
        while (Snapshot.Positions.Count < positionCount)
        { Snapshot.Positions.Add(new Position()); }
        if (Snapshot.Positions.Count > positionCount)
        { Snapshot.Positions.RemoveRange(positionCount, Snapshot.Positions.Count - positionCount); }
        Position strongest = null; decimal strongestPressure = 0m;
        for (int i = 0; i < positionCount; i++)
        {
            var stock = i < manager.stocks.Count ? manager.stocks[i] : manager.UnlistedStocks[i - manager.stocks.Count]; var position = Snapshot.Positions[i];
            position.Stock = stock; position.Owned = player.GetOwnedVolume(stock); position.Average = player.GetOwnedAverage(stock); position.Price = stock.currentPrice;
            if (secondary != null)
            {
                position.Owned += secondary.GetOwnedVolume(stock);
                position.Average = position.Owned > 0m ? (player.GetOwnedCost(stock) + secondary.GetOwnedCost(stock)) / position.Owned : 0m;
            }
            position.Change = previousHoldings.TryGetValue(stock, out decimal previous) ? position.Owned - previous : 0m;
            previousHoldings[stock] = position.Owned;
            position.Ownership = stock.issuedShares > 0m ? position.Owned / stock.issuedShares : 0m;
            if (position.Ownership > .5m) { Snapshot.ControlledCompanies++; }
            stock.orderBook.CopyOwnerOrders(player, orders); position.PendingBuy = position.PendingSell = 0m;
            if (secondary != null) { stock.orderBook.CopyOwnerOrders(secondary,secondaryOrders); orders.AddRange(secondaryOrders); }
            foreach (var order in orders)
            {
                Snapshot.OpenOrders.Add((stock.symbol,order.isBuy,order.price,order.volume,order.timestamp));
                if (order.isBuy) { position.PendingBuy += order.volume; }
                else { position.PendingSell += order.volume; }
            }
            decimal pressure = position.Change + (position.PendingBuy - position.PendingSell) * .25m;
            if (manual != null)
            {
                foreach (var request in manual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (autoManual != null)
            {
                foreach (var request in autoManual.ActiveOrders)
                { if (request.Stock == stock) { pressure += request.Unsubmitted * (request.Buy ? .25m : -.25m); } }
            }
            if (roomPressure.TryGetValue(stock,out decimal roomIntent))
            { pressure += roomIntent * .1m; }
            if (stock.AcceptsOrders && Math.Abs(pressure) > Math.Abs(strongestPressure))
            { strongest = position; strongestPressure = pressure; }
        }
        ObservationCount++;
        LastReason = "자산·지분·미체결·운용·리딩방·평판 관찰 " + ObservationCount + "회";
        if (strongest != null && now >= nextResponse && (ResponseStock == null || !ResponseStock.orderBook.IsActive(ResponseOrder)))
        { Respond(strongest.Stock, strongestPressure, now); }
        return true;
    }
    private void Respond(Stock stock, decimal pressure, float now)
    {
        exchange ??= ExchangeBot.Instance;
        if (exchange == null || !stock.AcceptsOrders || stock.currentPrice <= 0m || pressure == 0m)
        { return; }
        bool buy = pressure < 0m;
        if (buy) { stock.orderBook.GetTopBids(levels,3); }
        else { stock.orderBook.GetTopAsks(levels,3); }
        decimal depth = 0m;
        foreach (var level in levels) { depth += level.volume; }
        decimal cap = depth > 0m ? decimal.Floor(depth / levels.Count * (decimal)Mathf.Clamp(policy.depthFraction,.01f,.25f)) : 5m;
        decimal quantity = Math.Min(Math.Min(50,Math.Max(1,policy.maximumShares)), Math.Min(Math.Max(1m,cap), Math.Max(1m,decimal.Floor(Math.Abs(pressure)))));
        decimal price = levels.Count > 0 ? levels[0].price : buy ? PriceTickPolicy.OneTickBelow(stock.currentPrice) : PriceTickPolicy.OneTickAbove(stock.currentPrice);
        price = buy ? Math.Min(price,PriceTickPolicy.OneTickBelow(stock.currentPrice)) : Math.Max(price,PriceTickPolicy.OneTickAbove(stock.currentPrice));
        ResponseOrder = exchange.TryPlayerResponse(stock,buy,price,quantity);
        nextResponse = now + Mathf.Max(8f,policy.responseCooldown);
        if (ResponseOrder == null)
        { LastReason = stock.symbol + " 대응 보류 · 거래소 예산·재고·위험 제한"; return; }
        ResponseStock = stock; expires = now + Mathf.Clamp(policy.quoteLifetime,1f,8f); ResponseCount++;
        LastReason = stock.symbol + (buy ? " 매도 압력에 제한 매수 " : " 매수 압력에 제한 매도 ") + ResponseOrder.volume.ToString("N0") + "주 · 관찰 간격 " + policy.observationSeconds + "초";
    }
    private void ClearResponse()
    {
        if (ResponseStock != null && ResponseStock.orderBook.IsActive(ResponseOrder))
        { ResponseStock.orderBook.CancelOrder(ResponseOrder,ResponseStock); }
        ResponseOrder = null; ResponseStock = null;
    }
    void OnDisable() { ClearResponse(); }
}
