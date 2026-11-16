using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>공개 뉴스와 실제 체결을 읽는 UI용 기록. 시장·봇 판단에는 값을 쓰지 않는다.</summary>
public sealed class DeviceMarketJournal : MonoBehaviour
{
    public Trader account;
    public sealed class Entry
    {
        public Stock Stock;
        public long Id;
        public string Title, Source, Time, Certainty;
        public bool Urgent;
        public decimal Volume, BuyVolume;
        public NewsArticle Article;
    }
    public sealed class Fill
    {
        public Stock Stock;
        public string Time;
        public bool Buy;
        public decimal Price, Quantity, Realized;
    }
    private sealed class Tape
    {
        public readonly int[] Seconds = new int[60];
        public readonly decimal[] Buy = new decimal[60], Sell = new decimal[60];
    }
    public readonly List<Entry> Events = new(40);
    public readonly List<Fill> Fills = new(64);
    public int Version { get; private set; }
    private readonly Dictionary<Stock, Tape> tapes = new();
    private string lastFeed;
    private float next;
    public static double GameNow => MarketDayController.Instance?.AbsoluteGameMinute ?? 0;
    public static double GameSecondsPerRealSecond => 28800d / Math.Max(.1d,MarketDayController.Instance?.tradingSeconds ?? 900d);
    public static string FullStamp() => MarketDayController.Instance==null ? "DAY — —:—" : "DAY "+MarketDayController.Instance.CalendarDay+" "+Stamp();
    public static string Duration(double gameMinutes)
    {
        long minutes=(long)Math.Ceiling(Math.Max(0,gameMinutes));
        return (minutes/60).ToString("00")+":"+(minutes%60).ToString("00");
    }
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks) { stock.OnMatchedTrade-=Observe; stock.OnMatchedTrade+=Observe; if (!tapes.ContainsKey(stock)) { tapes.Add(stock,new Tape()); } }
    }
    public void ClearHistory() { Fills.Clear(); Events.Clear(); Version++; }
    public static string Stamp()
    {
        var day = MarketDayController.Instance;
        if (day==null)
        { return "—:—"; }
        int minute = day.ClockMinute;
        return (minute / 60).ToString("D2") + ":" + (minute % 60).ToString("D2");
    }
    void OnEnable()
    {
        StockManager.OnStockListed += Register;
        StockManager.OnStockDelisted += Remove;
        NewsManager.OnNewsPublished += News;
        StockStatistics.OnVolumeSurge += Surge;
    }
    void Start()
    {
        if (StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            { Register(stock); }
        }
    }
    private void Register(Stock stock)
    {
        if (stock == null)
        { return; }
        if (!tapes.ContainsKey(stock)) { tapes.Add(stock,new Tape()); }
        stock.OnMatchedTrade -= Observe;
        stock.OnMatchedTrade += Observe;
    }
    private void Remove(Stock stock)
    {
        stock.OnMatchedTrade -= Observe;
        tapes.Remove(stock);
    }
    private void Observe(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (resting == null || quantity <= 0m || !tapes.TryGetValue(stock, out var tape))
        { return; }
        int second = Mathf.FloorToInt(SessionClock.Time), slot = second % 60;
        if (tape.Seconds[slot] != second)
        { tape.Seconds[slot] = second; tape.Buy[slot] = tape.Sell[slot] = 0m; }
        if (resting.isBuy)
        { tape.Sell[slot] += quantity; }
        else
        { tape.Buy[slot] += quantity; }
        var player = account != null ? account : StockManager.Instance.playerAccount;
        if (buyer == seller || !ReferenceEquals(buyer, player) && !ReferenceEquals(seller, player))
        { return; }
        bool isBuy = ReferenceEquals(buyer, player);
        decimal basis = ReferenceEquals(resting.owner, player) ? resting.costBasis : player.SubmittedSellCost;
        Fills.Insert(0, new Fill { Stock = stock, Time = FullStamp(), Buy = isBuy, Price = price, Quantity = quantity, Realized = isBuy ? 0m : (price - basis) * quantity });
        if (Fills.Count > 64)
        { Fills.RemoveAt(64); }
        Version++;
    }
    public void Flow(Stock stock, out decimal volume, out decimal buy)
    {
        volume = buy = 0m;
        if (stock == null || !tapes.TryGetValue(stock, out var tape))
        { return; }
        int now = Mathf.FloorToInt(SessionClock.Time);
        for (int i = 0; i < 60; i++)
        {
            if (now - tape.Seconds[i] >= 60)
            { continue; }
            buy += tape.Buy[i]; volume += tape.Buy[i] + tape.Sell[i];
        }
    }
    private void News(Stock stock, long id, string title)
    { Add(stock, id, title, "시장 뉴스", "보도 · 사실 여부 별도 확인", false); }
    private void Surge(Stock stock, float multiple)
    { Add(stock, 0, stock.stockName + " 거래량 급증 (" + multiple.ToString("F1") + "배)", "실제 체결 관측", "관측된 변화", true); }
    private void Add(Stock stock, long id, string title, string source, string certainty, bool urgent)
    {
        Flow(stock, out decimal volume, out decimal buy);
        Events.Insert(0, new Entry { Stock = stock, Id = id, Title = title, Source = source, Certainty = certainty, Urgent = urgent, Time = FullStamp(), Volume = volume, BuyVolume = buy,
            Article = id > 0 ? NewsManager.Instance?.FindArticle(id) : null });
        if (Events.Count > 40)
        { Events.RemoveAt(40); }
        Version++;
    }
    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        string feed = MarketEventFeed.Instance == null ? null : MarketEventFeed.Instance.LatestEvent;
        if (string.IsNullOrEmpty(feed) || feed == lastFeed)
        { return; }
        lastFeed = feed;
        Add(null, 0, feed, "시장 알림", "알림 원문", false);
    }
    void OnDisable()
    {
        StockManager.OnStockListed -= Register;
        StockManager.OnStockDelisted -= Remove;
        NewsManager.OnNewsPublished -= News;
        StockStatistics.OnVolumeSurge -= Surge;
        foreach (var stock in tapes.Keys)
        { stock.OnMatchedTrade -= Observe; }
        tapes.Clear();
    }
}
