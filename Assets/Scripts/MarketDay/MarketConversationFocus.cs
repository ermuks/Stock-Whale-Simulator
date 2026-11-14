using System;
using System.Collections.Generic;

/// <summary>Attention relative to each stock's own volume history. Bounded minute rings and one shared cache.</summary>
public sealed class MarketConversationFocus
{
    private struct Bucket
    {
        public int Minute;
        public decimal Volume, Turnover, First, Last;
    }
    public struct Snapshot
    {
        public decimal Volume, PreviousVolume, Turnover, ReturnPercent;
        public double Probability, HeatScore, DwellMultiplier;
    }
    private sealed class Entry
    {
        public Stock Stock;
        public Bucket[] Buckets;
        public Snapshot Snapshot;
        public double Raw, Started, HotSince = double.NaN, ReentryUntil;
        public bool InHotList;
    }
    private readonly Dictionary<Stock,Entry> lookup = new();
    private readonly List<Entry> entries = new();
    private readonly List<Entry> ranking = new();
    private float nextRefresh = float.MinValue;
    private bool active;
    public float RefreshSeconds { get; set; } = 2f;
    public float ExplorationShare { get; set; } = .18f;
    public Func<double> GameMinutes { get; set; }
    public int RefreshCount { get; private set; }
    public int Count => entries.Count;
    public void Register(Stock stock)
    {
        if (stock == null || lookup.ContainsKey(stock))
        { return; }
        var entry = new Entry { Stock = stock, Started = double.NaN }; lookup.Add(stock,entry); entries.Add(entry); nextRefresh = float.MinValue;
    }
    public void Remove(Stock stock)
    {
        if (stock != null && lookup.TryGetValue(stock,out var entry))
        { lookup.Remove(stock); entries.Remove(entry); nextRefresh = float.MinValue; }
    }
    public void Record(Stock stock,decimal price,decimal quantity,float now)
    {
        if (stock == null || quantity <= 0m || price <= 0m)
        { return; }
        Register(stock);
        double gameMinute = GameMinutes?.Invoke() ?? now;
        int minute = (int)Math.Floor(gameMinute);
        int capacity = Math.Max(2, ChatSocialConfig.Current.Int("hotBaselineMinutes") + 2);
        var entry = lookup[stock];
        if (entry.Buckets == null || entry.Buckets.Length != capacity)
        { entry.Buckets = new Bucket[capacity]; entry.Started = gameMinute; entry.HotSince = double.NaN; entry.InHotList = false; }
        int index = ((minute % capacity) + capacity) % capacity;
        ref Bucket bucket = ref entry.Buckets[index];
        if (bucket.Minute != minute || bucket.Volume == 0m) { bucket = new Bucket { Minute = minute, First = price }; }
        bucket.Last = price; bucket.Volume += quantity; bucket.Turnover += price * quantity;
    }
    private void Refresh(float now)
    {
        if (now < nextRefresh)
        { return; }
        nextRefresh = now + Math.Max(.25f,RefreshSeconds); RefreshCount++;
        var config = ChatSocialConfig.Current;
        double gameMinute = GameMinutes?.Invoke() ?? now;
        int minute = (int)Math.Floor(gameMinute), count = 0;
        int window = config.Int("hotWindowMinutes"), history = config.Int("hotBaselineMinutes");
        decimal volume = 0m;
        ranking.Clear();
        foreach (var entry in entries)
        {
            entry.Snapshot = default; entry.Raw = 0d;
            if (!entry.Stock.IsListed || entry.Stock.IsDelisted)
            { continue; }
            count++; int first = int.MaxValue, last = int.MinValue, prior = int.MinValue;
            decimal open = 0m, close = 0m, priorPrice = 0m, historyVolume = 0m;
            if (entry.Buckets == null) { continue; }
            foreach (var bucket in entry.Buckets)
            {
                int age = minute - bucket.Minute;
                if (bucket.Volume <= 0m || age < 0 || age >= history)
                { continue; }
                historyVolume += bucket.Volume;
                if (age >= window)
                {
                    if (age < window * 2) { entry.Snapshot.PreviousVolume += bucket.Volume; }
                    if (bucket.Minute > prior) { prior = bucket.Minute; priorPrice = bucket.Last; }
                    continue;
                }
                entry.Snapshot.Volume += bucket.Volume; entry.Snapshot.Turnover += bucket.Turnover;
                if (bucket.Minute < first) { first = bucket.Minute; open = bucket.First; }
                if (bucket.Minute > last) { last = bucket.Minute; close = bucket.Last; }
            }
            if (priorPrice > 0m) { open = priorPrice; }
            if (open > 0m && close > 0m) { entry.Snapshot.ReturnPercent = (close / open - 1m) * 100m; }
            volume += entry.Snapshot.Volume;
            if (entry.Snapshot.Volume <= 0m || historyVolume <= 0m) { continue; }
            double observedMinutes = Math.Clamp(gameMinute - entry.Started + 1, window, history);
            double baseline = (double)historyVolume * window / observedMinutes;
            double dwell = entry.InHotList ? Math.Max(config.Get("hotDwellFloor"),
                1 - Math.Max(0, gameMinute - entry.HotSince - config.Get("hotDwellStartMinutes")) * config.Get("hotDwellPenaltyPerMinute")) : 1;
            double reentry = gameMinute < entry.ReentryUntil ? config.Get("hotReentryScale") : 1;
            entry.Raw = (double)entry.Snapshot.Volume / baseline *
                (1 + Math.Abs((double)entry.Snapshot.ReturnPercent) / config.Get("hotReturnDivisor")) * dwell * reentry;
            entry.Snapshot.HeatScore = entry.Raw; entry.Snapshot.DwellMultiplier = dwell;
            ranking.Add(entry);
        }
        active = volume > 0m;
        ranking.Sort((a, b) => { int order = b.Raw.CompareTo(a.Raw); return order != 0 ? order : string.CompareOrdinal(a.Stock.symbol, b.Stock.symbol); });
        double total = 0; foreach (var entry in ranking) { total += entry.Raw; }
        int hotCount = Math.Min(config.Int("hotListCount"), ranking.Count);
        foreach (var entry in entries)
        {
            int rank = ranking.IndexOf(entry); bool hot = rank >= 0 && rank < hotCount;
            if (hot && !entry.InHotList) { entry.HotSince = gameMinute; }
            if (!hot && entry.InHotList) { entry.ReentryUntil = gameMinute + config.Get("hotReentryMinutes"); entry.HotSince = double.NaN; }
            entry.InHotList = hot;
        }
        double explore = Math.Clamp(ExplorationShare,0f,1f);
        foreach (var entry in entries)
        {
            if (!entry.Stock.IsListed || entry.Stock.IsDelisted || count == 0)
            { continue; }
            entry.Snapshot.Probability = total > 0d ? explore/count+(1d-explore)*entry.Raw/total : 1d/count;
        }
    }
    public bool HasActivity(float now) { Refresh(now); return active; }
    public Snapshot Read(Stock stock,float now)
    {
        Refresh(now);
        return stock != null && lookup.TryGetValue(stock,out var entry) ? entry.Snapshot : default;
    }
    public double EventWeight(Stock stock,int topicEventCount,float now)
    {
        return (.25d+Read(stock,now).Probability*Math.Max(1,Count)*3d)/Math.Max(1,topicEventCount);
    }
    public Stock Choose(double roll,float now)
    {
        Refresh(now); roll = double.IsNaN(roll) ? .5d : Math.Clamp(roll,0d,.999999999d);
        Stock last = null;
        foreach (var entry in entries)
        {
            if (entry.Snapshot.Probability <= 0d)
            { continue; }
            last = entry.Stock; roll -= entry.Snapshot.Probability;
            if (roll <= 0d)
            { return last; }
        }
        return last;
    }
}
