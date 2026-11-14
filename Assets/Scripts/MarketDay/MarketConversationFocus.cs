using System;
using System.Collections.Generic;

/// <summary>Chat-only attention from real fills. Fixed 120-second rings and a shared two-second cache.</summary>
public sealed class MarketConversationFocus
{
    private const int Window = 60, Capacity = 120;
    private struct Bucket
    {
        public int Second;
        public decimal Volume, Turnover, First, Last;
    }
    public struct Snapshot
    {
        public decimal Volume, PreviousVolume, Turnover, ReturnPercent;
        public double Probability;
    }
    private sealed class Entry
    {
        public Stock Stock;
        public readonly Bucket[] Buckets = new Bucket[Capacity];
        public Snapshot Snapshot;
        public double Raw;
    }
    private readonly Dictionary<Stock,Entry> lookup = new();
    private readonly List<Entry> entries = new();
    private float nextRefresh = float.MinValue;
    private bool active;
    public float RefreshSeconds { get; set; } = 2f;
    public float ExplorationShare { get; set; } = .18f;
    public int RefreshCount { get; private set; }
    public int Count => entries.Count;
    public void Register(Stock stock)
    {
        if (stock == null || lookup.ContainsKey(stock))
        { return; }
        var entry = new Entry { Stock = stock }; lookup.Add(stock,entry); entries.Add(entry); nextRefresh = float.MinValue;
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
        int second = (int)Math.Floor(now), index = ((second % Capacity) + Capacity) % Capacity;
        var entry = lookup[stock]; ref Bucket bucket = ref entry.Buckets[index];
        if (bucket.Second != second || bucket.Volume == 0m) { bucket = new Bucket { Second = second, First = price }; }
        bucket.Last = price; bucket.Volume += quantity; bucket.Turnover += price * quantity;
    }
    private void Refresh(float now)
    {
        if (now < nextRefresh)
        { return; }
        nextRefresh = now + Math.Max(.25f,RefreshSeconds); RefreshCount++;
        int second = (int)Math.Floor(now), count = 0;
        decimal volume = 0m, turnover = 0m;
        foreach (var entry in entries)
        {
            entry.Snapshot = default; entry.Raw = 0d;
            if (!entry.Stock.IsListed || entry.Stock.IsDelisted)
            { continue; }
            count++; int first = int.MaxValue, last = int.MinValue; decimal open = 0m, close = 0m;
            foreach (var bucket in entry.Buckets)
            {
                int age = second-bucket.Second;
                if (bucket.Volume <= 0m || age < 0 || age >= Capacity)
                { continue; }
                if (age >= Window) { entry.Snapshot.PreviousVolume += bucket.Volume; continue; }
                entry.Snapshot.Volume += bucket.Volume; entry.Snapshot.Turnover += bucket.Turnover;
                if (bucket.Second < first) { first = bucket.Second; open = bucket.First; }
                if (bucket.Second > last) { last = bucket.Second; close = bucket.Last; }
            }
            if (open > 0m) { entry.Snapshot.ReturnPercent = (close/open-1m)*100m; }
            volume += entry.Snapshot.Volume; turnover += entry.Snapshot.Turnover;
        }
        active = volume > 0m;
        double total = 0d;
        foreach (var entry in entries)
        {
            var data = entry.Snapshot;
            if (data.Volume <= 0m)
            { continue; }
            double relativeVolume = (double)(data.Volume/volume);
            double relativeValue = turnover > 0m ? (double)(data.Turnover/turnover) : relativeVolume;
            double surge = data.PreviousVolume > 0m ? (double)(data.Volume/data.PreviousVolume) : 2d;
            double acceleration = 1d + .25d*Math.Clamp(surge-1d,0d,3d);
            double movement = 1d + Math.Min(20d,Math.Abs((double)data.ReturnPercent))*.05d;
            entry.Raw = (.45d*relativeVolume + .55d*relativeValue)*acceleration*movement; total += entry.Raw;
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
