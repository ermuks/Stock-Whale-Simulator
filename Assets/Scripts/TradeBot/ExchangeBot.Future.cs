using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public sealed class ReservedCompanyNews
{
    public readonly int NodeId;
    public readonly long EventId;
    public readonly double PublishAt;
    public readonly float StaySeconds, CriticalValue;
    internal readonly ItCompanyNode Node;
    public bool IsDelisting => Node.EventKind == CompanyEventKind.Delisting;

    internal ReservedCompanyNews(ItCompanyNode node, long id, double at, float stay, float value)
    {
        Node = node;
        NodeId = node.id;
        EventId = id;
        PublishAt = at;
        StaySeconds = stay;
        CriticalValue = value;
    }
}

public partial class ExchangeBot
{
    private sealed class CompanySchedule
    {
        internal Stock Stock;
        internal readonly Queue<ReservedCompanyNews> Items = new(128);
        internal ItCompanyNode Tail;
        internal double NextAt, LastReservedAt;
        internal uint RandomState;
        internal bool Ended;
        internal int PublishedNode;
        internal decimal BaseValue;
        internal ReservedCompanyNews NearestDelisting;
    }

    [Serializable]
    public class FutureDebug
    {
        public string stockId, ownerId;
        public int count, currentNode;
        public double nextPublication, nearestDelisting;
        public bool ended;
        public string[] reserved;
    }

    [SerializeField]
    private List<FutureDebug> futureDebug = new();
    public bool showFutureDebug;
    private readonly Dictionary<Stock, CompanySchedule> schedules = new();
    private readonly List<CompanySchedule> scheduleOrder = new();
    private int futureCursor;
    public double NewsClock { get; private set; }

    private bool newsPaused, skipResumeDelta;
    private float nextFutureDebug;
    private static readonly ProfilerMarker FutureMarker = new("DonDon.FutureNews");
    private EconomySettings Settings => StockManager.Instance == null ? fallbackSettings : StockManager.Instance.economy;

    private readonly EconomySettings fallbackSettings = new();
    private static double NextRandom(CompanySchedule schedule)
    {
        uint x = schedule.RandomState;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        schedule.RandomState = x == 0 ? 1u : x;
        return schedule.RandomState / 4294967296.0;
    }

    internal void RegisterCompanySchedule(Stock stock, ItCompanyNode start)
    {
        if (stock == null || start == null || schedules.ContainsKey(stock) || !stock.IsListed)
        { return; }

        var schedule = new CompanySchedule
        {
            Stock = stock,
            Tail = start,
            PublishedNode = start.id,
            BaseValue = stock.realValue,
            RandomState = (uint)CandleHistoryManager.StableSeed(Settings.seed, "NEWS:" + stock.symbol) | 1u
        };
        schedule.NextAt = NewsClock + Settings.firstNewsMin + NextRandom(schedule) * Math.Max(0f, Settings.firstNewsMax - Settings.firstNewsMin);
        schedules.Add(stock, schedule);
        scheduleOrder.Add(schedule);
        futureDebug.Add(new FutureDebug { stockId = stock.symbol, ownerId = stock.OwnerId });
    }

    public bool TryGetOwnFuture(WhaleBot requester, out IReadOnlyList<ReservedCompanyNews> result)
    {
        result = null;
        if (requester == null || requester.OwnedCompany == null || requester.OwnedCompany.Owner != requester || !schedules.TryGetValue(requester.OwnedCompany, out var schedule))
        { return false; }

        result = schedule.Items.ToArray();
        return true;
    }

    internal ReservedCompanyNews NearestOwnedDelisting(WhaleBot requester)
    {
        if (requester == null || requester.OwnedCompany == null || requester.OwnedCompany.Owner != requester || !schedules.TryGetValue(requester.OwnedCompany, out var schedule))
        { return null; }

        return schedule.NearestDelisting;
    }

    internal int PublishedCompanyNode(Stock stock) => schedules.TryGetValue(stock, out var schedule) ? schedule.PublishedNode : 0;
    private bool NeedsReservation(CompanySchedule schedule)
    { return !schedule.Ended && schedule.Items.Count < Mathf.Clamp(Settings.futureLimit, 10, 128) && (schedule.Items.Count < Mathf.Clamp(Settings.futureCount, 10, 128) || schedule.LastReservedAt < NewsClock + Math.Max(Settings.futureHorizonSeconds, Settings.ownerNoticeSeconds)); }

    private void ReserveOne(CompanySchedule schedule)
    {
        var manager = ItCompanyManager.Instance;
        if (manager == null || schedule.Tail.next == null || schedule.Tail.next.Length == 0)
        {
            schedule.Ended = true;
            return;
        }

        int[] choices = schedule.Tail.next, weights = schedule.Tail.nextWeight;
        int selected = 0;
        if (weights != null && weights.Length == choices.Length)
        {
            long total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += Math.Max(0, weights[i]);
            }

            double pick = NextRandom(schedule) * total;
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= Math.Max(0, weights[i]);
                if (pick < 0)
                {
                    selected = i;
                    break;
                }
            }
        }
        else
        {
            selected = Math.Min(choices.Length - 1, (int)(NextRandom(schedule) * choices.Length));
        }

        var node = manager.FindNode(choices[selected]);
        if (node == null)
        {
            schedule.Ended = true;
            return;
        }

        float minimum = Mathf.Max(0.001f, Settings.minimumStaySeconds, Mathf.Max(Settings.futureHorizonSeconds, Settings.ownerNoticeSeconds) / Math.Max(1, Mathf.Clamp(Settings.futureLimit, 10, 128) - 1));
        float stay = Mathf.Max(minimum, (float)(node.stayTimeRangeMin + NextRandom(schedule) * Math.Max(0f, node.stayTimeRangeMax - node.stayTimeRangeMin)) * 60f);
        float value = (float)(node.criticalValueMin + NextRandom(schedule) * Math.Max(0f, node.criticalValueMax - node.criticalValueMin));
        double at = schedule.NextAt;
        if (node.EventKind == CompanyEventKind.Delisting)
        {
            at = Math.Max(at, NewsClock + Mathf.Max(0f, Settings.ownerNoticeSeconds));
        }

        long eventId = NewsManager.Instance == null ? ++fallbackNewsId : NewsManager.Instance.ReserveEventId(schedule.Stock);
        var reserved = new ReservedCompanyNews(node, eventId, at, stay, value);
        schedule.Items.Enqueue(reserved);
        schedule.LastReservedAt = at;
        schedule.Tail = node;
        schedule.NextAt = at + stay;
        if (reserved.IsDelisting && schedule.NearestDelisting == null)
        {
            schedule.NearestDelisting = reserved;
        }

        if (node.next == null || node.next.Length == 0)
        {
            schedule.Ended = true;
        }
    }

    private long fallbackNewsId;
    private void TickFutureNews()
    {
        if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed || newsPaused)
        { return; }

        if (skipResumeDelta)
        {
            skipResumeDelta = false;
        }
        else
        {
            NewsClock += Time.deltaTime;
        }

        if (!AssetsInitialized || scheduleOrder.Count == 0)
        { return; }

        using (FutureMarker.Auto())
        {
            int work = Math.Max(1, Settings.futureWorkPerFrame), visited = 0;
            while (work > 0 && visited < scheduleOrder.Count)
            {
                if (futureCursor >= scheduleOrder.Count)
                {
                    futureCursor = 0;
                }

                var schedule = scheduleOrder[futureCursor++];
                visited++;
                if (schedule.Stock.IsDelisting || schedule.Stock.IsDelisted)
                { continue; }

                while (work > 0 && NeedsReservation(schedule))
                {
                    ReserveOne(schedule);
                    work--;
                }

                if (schedule.NearestDelisting != null && schedule.Stock.Owner is WhaleBot owner && NewsClock >= schedule.NearestDelisting.PublishAt - Settings.ownerNoticeSeconds)
                {
                    owner.BeginPreDisposal(schedule.NearestDelisting);
                }

                if (work > 0 && schedule.Items.Count > 0 && schedule.Items.Peek().PublishAt <= NewsClock)
                {
                    var item = schedule.Items.Dequeue();
                    work--;
                    ItCompanyManager.Instance?.PublishReserved(schedule.Stock, item, schedule.BaseValue);
                    schedule.PublishedNode = item.NodeId;
                }
            }
        }

        if (SessionClock.Time >= nextFutureDebug)
        {
            nextFutureDebug = SessionClock.Time + 1f;
            for (int i = 0; i < scheduleOrder.Count; i++)
            {
                var schedule = scheduleOrder[i];
                var debug = futureDebug[i];
                debug.count = schedule.Items.Count;
                debug.currentNode = schedule.PublishedNode;
                debug.nextPublication = schedule.Items.Count == 0 ? -1 : schedule.Items.Peek().PublishAt;
                debug.nearestDelisting = schedule.NearestDelisting == null ? -1 : schedule.NearestDelisting.PublishAt;
                debug.ended = schedule.Ended;
                if (!showFutureDebug)
                { continue; }

                // 미래 문자열은 공개 UI·채팅으로 전달하지 않고 Inspector에만 보관한다.
                if (debug.reserved == null || debug.reserved.Length != schedule.Items.Count)
                {
                    debug.reserved = new string[schedule.Items.Count];
                }

                int n = 0;
                foreach (var item in schedule.Items)
                {
                    debug.reserved[n++] = $"{item.EventId}: node {item.NodeId}, t={item.PublishAt:F2}, stay={item.StaySeconds:F2}, value={item.CriticalValue:F6}";
                }
            }
        }
    }

    private void OnApplicationPause(bool paused)
    {
        newsPaused = paused;
        if (!paused)
        {
            skipResumeDelta = true;
        }
    }

    private void RemoveCompanySchedule(Stock stock)
    {
        if (!schedules.TryGetValue(stock, out var schedule))
        { return; }

        int index = scheduleOrder.IndexOf(schedule);
        schedules.Remove(stock);
        scheduleOrder.RemoveAt(index);
        futureDebug.RemoveAt(index);
    }
}
