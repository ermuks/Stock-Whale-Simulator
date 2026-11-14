using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 투자자 한 명이 자신의 실제 주문과 체결을 공개하는 방. 별도 자금 지급/자동 추종 주문은 없다.</summary>
[DefaultExecutionOrder(200)]
public sealed class NpcLeadingRoom : MonoBehaviour
{
    public const int RoomId = 4;
    [Serializable]
    public sealed class Settings
    {
        public int schemaVersion = 1;
        public string title = "새벽 관찰실";
        public float observationSeconds = 5f, signalLifetime = 60f, messageInterval = 15f;
    }
    public sealed class Signal
    {
        public long Id;
        public int LeaderId;
        public Stock Stock;
        public decimal Buy, Sell;
        public bool RestingBuy, RestingSell;
        public float ObservedAt, ExpiresAt;
        public string Time;
    }
    public static NpcLeadingRoom Instance { get; private set; }
    public Settings settings = new();
    public TabletGroupChatUI chat;
    public TradeBot Leader { get; private set; }
    public Signal Current { get; private set; }
    public bool PlayerJoined { get; private set; }
    public bool JoinRequested { get; private set; }
    public bool HasViewedSignal { get; private set; }
    public decimal Turnover { get; private set; }
    public decimal RealizedProfit { get; private set; }
    public int FillCount { get; private set; }
    public int Observations { get; private set; }
    public string Status { get; private set; } = "시장 개장 후 운영자를 기다립니다.";
    public string LeaderName => Leader == null || Leader.Social == null ? "운영자 대기" : Leader.Social.Nickname;
    public bool Available => Leader != null && Leader.isActiveAndEnabled && Leader.Social != null && Leader.Social.Psychology.Room == RoomId;
    public float NextObservationAt => nextObservation;
    public bool HasSignal => Available && Current != null && Current.LeaderId == Leader.Social.Id && Time.time < Current.ExpiresAt && Current.Stock.AcceptsOrders;
    private readonly HashSet<Stock> watched = new();
    private readonly List<Order> orders = new(32);
    private float nextObservation, nextMessage, lastBuyTime;
    private Stock lastBought;
    private decimal lastBuyPrice;
    private long nextSignal;
    private bool subscribed;
    void Awake()
    {
        Instance = this;
        var data = Resources.Load<TextAsset>("Progression/NpcLeadingRoom");
        if (data != null)
        {
            var value = JsonUtility.FromJson<Settings>(data.text);
            if (value != null && value.schemaVersion == 1 && !string.IsNullOrWhiteSpace(value.title))
            { settings = value; }
        }
    }
    void OnEnable()
    {
        StockManager.OnStockListed += Watch; StockManager.OnStockDelisted += Forget;
    }
    void Update()
    {
        if (!MarketDayController.CanTrade || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.time < nextObservation)
        { return; }
        Observe(Time.time);
    }
    public void Observe(float now)
    {
        if (!MarketDayController.CanTrade || StockManager.Instance == null || InvestorSociety.Instance == null || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || now < nextObservation)
        { return; }
        nextObservation = now + Mathf.Clamp(settings.observationSeconds,1f,30f); Observations++;
        if (!subscribed)
        {
            foreach (var stock in StockManager.Instance.stocks)
            { Watch(stock); }
            subscribed = true;
        }
        if (!Available || Leader.Equity() <= 0m)
        { AppointLeader(); }
        if (!Available)
        { Status = "거래 가능한 운영자를 기다립니다."; return; }
        if (JoinRequested && !PlayerJoined)
        { JoinPlayer(); }
        PublishObservation(now);
    }
    private void AppointLeader()
    {
        var society = InvestorSociety.Instance;
        var room = society.GetRoom(RoomId);
        if (Leader != null && Leader.Social != null && Leader.Social.Psychology.Room == RoomId)
        { society.Leave(Leader,"운영 종료"); }
        Leader = null; Current = null; lastBought = null; room.OwnerBotId = 0; room.Title = settings.title;
        Turnover = RealizedProfit = 0m; FillCount = 0;
        foreach (var bot in MarketDayController.Instance.Bots)
        {
            if (bot == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || bot.IsCompanyOwner || bot.Social == null || bot.cash - bot.ReservedCash < 1000m)
            { continue; }
            int membership = bot.Social.Psychology.Room;
            if (membership != 0 && membership != RoomId || membership == 0 && room.Members.Count >= society.settings.roomCapacity)
            { continue; }
            if (Leader == null || bot.Social.Id < Leader.Social.Id)
            { Leader = bot; }
        }
        if (Leader == null)
        { return; }
        room.OwnerBotId = Leader.Social.Id;
        if (Leader.Social.Psychology.Room == 0 && !society.Join(Leader,RoomId))
        { Leader = null; room.OwnerBotId = 0; return; }
        society.Speak(Leader,RoomId,"제가 실제로 넣은 주문과 체결을 이 방에 남기겠습니다. 각자 자금에 맞게 판단해 주세요.",default,ChatEvent.SmallTalk);
        nextMessage = 0f;
    }
    private void Watch(Stock stock)
    {
        if (stock != null && watched.Add(stock))
        { stock.OnMatchedTrade += Trade; }
    }
    private void Forget(Stock stock)
    {
        if (watched.Remove(stock))
        { stock.OnMatchedTrade -= Trade; }
        if (Current != null && Current.Stock == stock)
        { Current = null; }
    }
    private void Trade(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (Leader == null || buyer == seller || resting == null || quantity <= 0m || !ReferenceEquals(buyer,Leader) && !ReferenceEquals(seller,Leader))
        { return; }
        Turnover += price * quantity; FillCount++;
        if (ReferenceEquals(seller,Leader))
        {
            decimal basis = ReferenceEquals(resting.owner,Leader) ? resting.costBasis : Leader.SubmittedSellCost;
            RealizedProfit += (price - basis) * quantity;
        }
        else
        { lastBought = stock; lastBuyPrice = price; lastBuyTime = Time.time; }
    }
    private void PublishObservation(float now)
    {
        Stock selected = null; decimal bid = 0m, ask = 0m; bool recentFill = false;
        // Prefer an actual buy instruction accessible to a small account; otherwise show the operator's real sell quote.
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!stock.AcceptsOrders)
            { continue; }
            stock.orderBook.CopyOwnerOrders(Leader,orders);
            decimal buy = 0m, sell = 0m;
            foreach (var order in orders)
            {
                if (order.isBuy) { buy = Math.Max(buy,order.price); }
                else if (sell == 0m || order.price < sell) { sell = order.price; }
            }
            if (buy <= 0m && stock == lastBought && now - lastBuyTime <= Mathf.Clamp(settings.signalLifetime,10f,180f))
            { buy = lastBuyPrice; }
            if (buy == 0m && sell == 0m)
            { continue; }
            if (selected == null || buy > 0m && (bid == 0m || buy < bid))
            { selected = stock; bid = buy; ask = sell; recentFill = buy > 0m && !orders.Exists(o => o.isBuy && o.price == buy); }
        }
        if (selected == null)
        {
            Current = null; Status = "운영자가 주문을 관찰 중입니다. 첫 신호를 기다려도 시장은 계속 움직입니다.";
            return;
        }
        bool changed = Current == null || Current.Stock != selected || Current.Buy != bid || Current.Sell != ask || Current.RestingBuy != (bid > 0m && !recentFill) || Current.ExpiresAt <= now;
        if (changed)
        {
            Current = new Signal { Id = ++nextSignal, LeaderId = Leader.Social.Id, Stock = selected, Buy = bid, Sell = ask, RestingBuy = bid > 0m && !recentFill, RestingSell = ask > 0m, ObservedAt = now, ExpiresAt = now + Mathf.Clamp(settings.signalLifetime,10f,180f), Time = DeviceMarketJournal.Stamp() };
        }
        Status = Current.Time + " 관측 · " + (Current.RestingBuy ? "실제 매수 주문" : bid > 0m ? "최근 매수 체결" : "실제 매도 주문");
        if (!changed || now < nextMessage)
        { return; }
        nextMessage = now + Mathf.Max(5f,settings.messageInterval);
        string message = selected.stockName + ": " + (bid > 0m ? (recentFill ? "최근 " : "매수 ") + bid.ToString("N0") + (recentFill ? "원에 매수 체결됐습니다." : "원 주문을 대기 중입니다.") : "새 매수 신호는 없습니다.");
        message += ask > 0m ? " 매도 " + ask.ToString("N0") + "원에도 실제 주문이 있습니다." : " 매도 주문은 아직 없습니다.";
        message += " 부임 후 실현손익 " + RealizedProfit.ToString("+#,##0;-#,##0;0") + "원.";
        var context = new InvestorMessageContext(InvestorSociety.Instance.NextEventId(),RoomId,Math.Sign(RealizedProfit),.8f,0f,true,target:Leader.Social.Id);
        if (InvestorSociety.Instance.SocialReady) { InvestorSociety.Instance.OfferNpcObservation(Leader); }
        else { InvestorSociety.Instance.Speak(Leader,RoomId,message,context,ChatEvent.BuyConsideration); }
    }
    public void RequestJoin()
    { JoinRequested = true; }
    public bool JoinPlayer()
    {
        if (!Available || !StockManager.Instance.SetupConfirmed)
        { Status = "운영자가 준비되면 참여할 수 있습니다."; return false; }
        JoinRequested = true; PlayerJoined = true;
        chat?.SelectRoom(RoomId);
        return true;
    }
    public void LeavePlayer()
    {
        PlayerJoined = JoinRequested = false;
        if (chat != null && chat.SelectedRoom == RoomId)
        { chat.SelectRoom(0); }
        Status = "퇴장했습니다. 이미 제출한 내 주문은 태블릿에서 따로 관리합니다.";
    }
    public void MarkViewed()
    {
        if (PlayerJoined && HasSignal)
        { HasViewedSignal = true; }
    }
    void OnDisable()
    {
        StockManager.OnStockListed -= Watch; StockManager.OnStockDelisted -= Forget;
        foreach (var stock in watched)
        { stock.OnMatchedTrade -= Trade; }
        watched.Clear(); subscribed = false;
    }
    void OnDestroy()
    {
        if (Instance == this)
        { Instance = null; }
    }
}
