using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>호가창. SortedList로 첫/끝 접근 O(1), 특정 가격 탐색 O(log n) 이진 검색.</summary>
public class OrderBook
{
    static readonly Comparer<decimal> Descending = Comparer<decimal>.Create((a, b) => b.CompareTo(a));
    // 매수: 가격 높은 순 → Keys[0]=최우선매수, Keys[Count-1]=최하위
    private SortedList<decimal, Queue<Order>> bids = new SortedList<decimal, Queue<Order>>(Descending);
    private SortedList<decimal, decimal> _bidVolumes = new SortedList<decimal, decimal>(Descending);
    // 매도: 가격 낮은 순 → Keys[0]=최우선매도, Keys[Count-1]=최고가
    private SortedList<decimal, Queue<Order>> asks = new SortedList<decimal, Queue<Order>>();
    private SortedList<decimal, decimal> _askVolumes = new SortedList<decimal, decimal>();
    public decimal lastPrice;
    public long Version { get; private set; }

    private long timestampCounter;
    private readonly HashSet<Order> activeOrders = new();
    private readonly Dictionary<IOrderOwner, HashSet<Order>> ownerOrders = new();
    public void CopyOwnerOrders(IOrderOwner owner, List<Order> target)
    {
        target.Clear();
        if (owner != null && ownerOrders.TryGetValue(owner, out var orders))
        {
            foreach (var order in orders)
            {
                target.Add(order);
            }
        }
    }

    private readonly Dictionary<(IOrderOwner owner, bool isBuy, decimal price), int> _ownerOrderCounts = new();
    private readonly Dictionary<IOrderOwner, int> _ownerOrderTotals = new();
    private void TrackOrder(IOrderOwner owner, bool isBuy, decimal price, int delta, Order order = null)
    {
        if (order != null)
        {
            if (delta > 0)
            {
                activeOrders.Add(order);
            }
            else
            {
                activeOrders.Remove(order);
            }
        }

        if (owner == null)
        { return; }

        if (order != null)
        {
            if (delta > 0)
            {
                if (!ownerOrders.TryGetValue(owner, out var orders))
                {
                    orders = new HashSet<Order>();
                    ownerOrders.Add(owner, orders);
                }

                orders.Add(order);
            }
            else if (ownerOrders.TryGetValue(owner, out var orders))
            {
                orders.Remove(order);
                if (orders.Count == 0)
                {
                    ownerOrders.Remove(owner);
                }
            }
        }

        _ownerOrderTotals.TryGetValue(owner, out int total);
        total += delta;
        if (total <= 0)
        {
            _ownerOrderTotals.Remove(owner);
        }
        else
        {
            _ownerOrderTotals[owner] = total;
        }

        var key = (owner, isBuy, price);
        _ownerOrderCounts.TryGetValue(key, out int count);
        count += delta;
        if (count <= 0)
        {
            _ownerOrderCounts.Remove(key);
        }
        else
        {
            _ownerOrderCounts[key] = count;
        }
    }

    private decimal _cachedTotalAskVolume;
    private decimal _cachedTotalBidVolume;
    private bool _askVolumeDirty = true;
    private bool _bidVolumeDirty = true;
    public OrderBook(decimal startPrice)
    {
        lastPrice = PriceTickPolicy.RoundToTick(startPrice);
    }

    private static void NotifySellCancellation(Order order, Stock stock)
    {
        if (order.owner is Trader trader)
        {
            trader.OnSellOrderCancelledAtCost(stock, order.volume, order.costBasis);
        }
        else
        {
            order.owner?.OnSellOrderCancelled(stock, order.volume, order.price);
        }
    }

    public bool IsActive(Order order) => order != null && activeOrders.Contains(order);
    public bool CancelOrder(Order order, Stock stock)
    {
        if (!IsActive(order))
        { return false; }

        var book = order.isBuy ? bids : asks;
        var volumes = order.isBuy ? _bidVolumes : _askVolumes;
        if (!book.TryGetValue(order.price, out var queue))
        { return false; }

        int count = queue.Count;
        for (int i = 0; i < count; i++)
        {
            var item = queue.Dequeue();
            if (item != order)
            {
                queue.Enqueue(item);
            }
        }

        TrackOrder(order.owner, order.isBuy, order.price, -1, order);
        volumes[order.price] -= order.volume;
        if (queue.Count == 0)
        {
            book.Remove(order.price);
            volumes.Remove(order.price);
        }

        if (order.isBuy)
        {
            order.owner?.OnBuyOrderCancelled(stock, order.volume, order.price);
        }
        else
        {
            NotifySellCancellation(order, stock);
        }

        Version++;
        _askVolumeDirty = true;
        _bidVolumeDirty = true;
        return true;
    }

    public bool HasOrders => bids.Count > 0 || asks.Count > 0;
    public int ActiveOrderCount => activeOrders.Count;

    public int CancelAllBudgeted(Stock stock, int budget)
    {
        int cancelled = 0;
        while (cancelled < budget && HasOrders)
        {
            bool isBuy = bids.Count > 0;
            var book = isBuy ? bids : asks;
            var volumes = isBuy ? _bidVolumes : _askVolumes;
            decimal price = book.Keys[0];
            var queue = book.Values[0];
            var order = queue.Dequeue();
            TrackOrder(order.owner, isBuy, price, -1, order);
            if (isBuy)
            {
                order.owner?.OnBuyOrderCancelled(stock, order.volume, price);
            }
            else
            {
                NotifySellCancellation(order, stock);
            }

            volumes[price] -= order.volume;
            if (queue.Count == 0)
            {
                book.RemoveAt(0);
                volumes.Remove(price);
            }

            cancelled++;
            Version++;
            _askVolumeDirty = true;
            _bidVolumeDirty = true;
        }

        return cancelled;
    }

#region Public API
    /// <param name = "addUnfilledToBook">false면 시장가 주문: 미체결 수량을 호가창에 넣지 않음</param>
    /// <param name = "subTickSteps">0이면 호가 단위로 내림, 1이면 1/10 단위, 2이면 1/100 단위로 내림</param>
    /// <param name = "orderOwner">매수 미체결 분이 호가창에 들어갈 때 소유자. 체결 시 OnOrderFilled 호출·예치 해제</param>
    /// <param name = "stock">체결 알림 시 전달할 종목 (orderOwner용)</param>
    /// <param name = "maxMatchAskPrice">매수 시 이 가격 초과 매도 호가는 체결하지 않음. 시장가 매수로 고가 매도가 통째로 소진되는 것 방지.</param>
    public TradeResult PlaceOrder(decimal price, decimal volume, bool isBuy, bool addUnfilledToBook = true, int subTickSteps = 0, IOrderOwner orderOwner = null, Stock stock = null, decimal? maxMatchAskPrice = null)
    {
        if (!MarketDayController.CanTrade || (stock != null && !stock.AcceptsOrders))
        { return new TradeResult(); }

        volume = decimal.Floor(volume);
        if (volume <= 0m)
        { return new TradeResult(); }
        if (price > 0m && price < 1m)
        { return new TradeResult(); }

        if (isBuy && price < decimal.MaxValue)
        {
            price = subTickSteps > 0 ? PriceTickPolicy.FloorToSubTick(price, subTickSteps) : PriceTickPolicy.FloorToTick(price);
        }
        else if (!isBuy && price > 0m)
        {
            price = subTickSteps > 0 ? PriceTickPolicy.FloorToSubTick(price, subTickSteps) : PriceTickPolicy.FloorToTick(price);
        }

        if (addUnfilledToBook && !PriceTickPolicy.IsValidPrice(price))
        { return new TradeResult(); }

        TradeResult result = new TradeResult();
        if (isBuy)
        {
            MatchBuy(price, volume, result, addUnfilledToBook, orderOwner, stock, maxMatchAskPrice);
        }
        else
        {
            MatchSell(price, volume, result, addUnfilledToBook, orderOwner, stock);
        }

        result.averagePrice = result.filledVolume > 0m ? result.totalCost / result.filledVolume : 0m;
        return result;
    }

    /// <summary>상위 10호가. SortedList 인덱스로 O(1) 접근.</summary>
    public void GetTopBids(List<(decimal price, decimal volume)> list, int count = 10)
    {
        list.Clear();
        for (int i = 0; i < bids.Count && i < count; i++)
        {
            decimal key = bids.Keys[i];
            if (_bidVolumes.TryGetValue(key, out var volume) && volume > 0m)
            {
                list.Add((key, volume));
            }
        }
    }

    public List<(decimal price, decimal volume)> GetTopBids()
    {
        var list = new List<(decimal price, decimal volume)>(Math.Min(10, bids.Count));
        for (int i = 0; i < bids.Count && i < 10; i++)
        {
            var key = bids.Keys[i];
            decimal vol = _bidVolumes.TryGetValue(key, out var v) ? v : 0m;
            if (vol > 0m)
            {
                list.Add((key, vol));
            }
        }

        return list;
    }

    /// <summary>상위 N호가(가장 낮은 매도가 순). UI에서 근접 호가 표시용.</summary>
    public void GetTopAsks(List<(decimal price, decimal volume)> list, int count = 10)
    {
        list.Clear();
        for (int i = 0; i < asks.Count && i < count; i++)
        {
            decimal key = asks.Keys[i];
            if (_askVolumes.TryGetValue(key, out var volume) && volume > 0m)
            {
                list.Add((key, volume));
            }
        }
    }

    public List<(decimal price, decimal volume)> GetTopAsks(int count = 10)
    {
        var list = new List<(decimal price, decimal volume)>(Math.Min(count, asks.Count));
        for (int i = 0; i < asks.Count && i < count; i++)
        {
            var key = asks.Keys[i];
            decimal vol = _askVolumes.TryGetValue(key, out var v) ? v : 0m;
            if (vol > 0m)
            {
                list.Add((key, vol));
            }
        }

        return list;
    }

    /// <summary>고가 매도 N호가(가장 높은 매도가 순). 현재가 대비 높은 비율의 매도 호가를 UI에 보이게 할 때 사용.</summary>
    public List<(decimal price, decimal volume)> GetHighAsks(int count = 10)
    {
        var list = new List<(decimal price, decimal volume)>();
        if (asks.Count == 0)
        { return list; }

        int start = Math.Max(0, asks.Count - count);
        for (int i = asks.Count - 1; i >= start && list.Count < count; i--)
        {
            var key = asks.Keys[i];
            decimal vol = _askVolumes.TryGetValue(key, out var v) ? v : 0m;
            if (vol > 0m)
            {
                list.Add((key, vol));
            }
        }

        return list;
    }

    /// <summary>스프레드. 첫 호가만 참조하므로 O(1).</summary>
    public decimal GetSpread()
    {
        if (bids.Count == 0 || asks.Count == 0)
        { return 0m; }

        return asks.Keys[0] - bids.Keys[0];
    }

    /// <summary>전체 매도 호가 물량 합계 (무결성 검사용). 캐싱으로 O(1), dirty시만 O(n).</summary>
    public decimal GetTotalAskVolume()
    {
        if (_askVolumeDirty)
        {
            _cachedTotalAskVolume = 0m;
            foreach (var vol in _askVolumes.Values)
            {
                _cachedTotalAskVolume += vol;
            }

            _askVolumeDirty = false;
        }

        return _cachedTotalAskVolume;
    }

    /// <summary>전체 매수 호가 물량 합계 (무결성 검사용). 캐싱으로 O(1), dirty시만 O(n).</summary>
    public decimal GetTotalBidVolume()
    {
        if (_bidVolumeDirty)
        {
            _cachedTotalBidVolume = 0m;
            foreach (var vol in _bidVolumes.Values)
            {
                _cachedTotalBidVolume += vol;
            }

            _bidVolumeDirty = false;
        }

        return _cachedTotalBidVolume;
    }

    /// <summary>소유자 매도 주문 취소: 현재가 대비 -20%~+80% 범위 밖이면 무조건 취소, 범위 내면 큰 수 법칙 역수(거리 비율)로 취소 확률 적용.</summary>
    /// <param name = "rangeMinPct">이 비율 미만(현재가 대비 하한)이면 무조건 취소. 예: -0.20 = -20%</param>
    /// <param name = "rangeMaxPct">이 비율 초과(현재가 대비 상한)면 무조건 취소. 예: 0.80 = +80%</param>
    /// <param name = "stock">취소된 물량을 소유자에게 반환할 때 사용할 종목</param>
    public int CancelSellOrdersByOwner(IOrderOwner owner, decimal currentPrice, decimal rangeMinPct = -0.20m, decimal rangeMaxPct = 0.80m, Stock stock = null)
    {
        if (owner == null || currentPrice <= 0m || !_ownerOrderTotals.ContainsKey(owner))
        { return 0; }

        int cancelled = 0;
        for (int i = 0; i < asks.Count;)
        {
            decimal price = asks.Keys[i];
            var queue = asks[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner != owner)
                {
                    keep.Enqueue(order);
                    continue;
                }

                decimal deviation = (price - currentPrice) / currentPrice;
                bool doCancel;
                if (deviation < rangeMinPct || deviation > rangeMaxPct)
                {
                    doCancel = true;
                }
                else
                {
                    decimal t;
                    if (deviation <= 0m)
                    {
                        t = (-deviation) / (-rangeMinPct > 0m ? -rangeMinPct : 0.01m);
                    }
                    else
                    {
                        t = deviation / (rangeMaxPct > 0m ? rangeMaxPct : 0.01m);
                    }

                    if (t > 1m)
                    {
                        t = 1m;
                    }

                    doCancel = (decimal)UnityEngine.Random.value < t;
                }

                if (doCancel)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.owner != null && order.volume > 0m)
                    {
                        NotifySellCancellation(order, stock);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _askVolumes.TryGetValue(price, out var av))
            {
                _askVolumes[price] = av - removedVol;
            }

            if (keep.Count == 0)
            {
                asks.RemoveAt(i);
                _askVolumes.RemoveAt(i);
            }
            else
            {
                asks[price] = keep;
                i++;
            }
        }

        if (cancelled > 0)
        {
            Version++;
        }

        if (cancelled > 0)
        {
            _askVolumeDirty = true;
        }

        return cancelled;
    }

    /// <summary>
    /// 전체 호가창 정리: 현재가 대비 너무 먼 주문 취소 (5분마다 중앙에서 1회 실행).
    /// 매도: -20% ~ +80% 밖 취소
    /// 매수: -20% ~ +20% 밖 취소
    /// 이진 검색으로 범위 경계 찾아서 범위 밖만 정리.
    /// </summary>
    public int CleanupDistantOrders(decimal currentPrice, Stock stock)
    {
        if (currentPrice <= 0m)
        { return 0; }

        int result = CleanupSide(asks, _askVolumes, currentPrice * 0.8m, currentPrice * 1.8m, stock);
        result += CleanupSide(bids, _bidVolumes, currentPrice * 0.8m, currentPrice * 1.2m, stock);
        if (result > 0)
        {
            Version++;
            _askVolumeDirty = true;
            _bidVolumeDirty = true;
        }

        return result;
    }

    private int CleanupSide(SortedList<decimal, Queue<Order>> book, SortedList<decimal, decimal> volumes, decimal min, decimal max, Stock stock)
    {
        int removed = 0;
        for (int i = 0; i < book.Count;)
        {
            decimal price = book.Keys[i];
            if (price >= min && price <= max)
            {
                i++;
                continue;
            }

            var queue = book.Values[i];
            int count = queue.Count;
            for (int n = 0; n < count; n++)
            {
                var order = queue.Dequeue();
                if (order.purpose != OrderPurpose.Ordinary && order.purpose != OrderPurpose.Rhythm && order.purpose != OrderPurpose.Resting)
                {
                    queue.Enqueue(order);
                    continue;
                }

                TrackOrder(order.owner, order.isBuy, price, -1, order);
                volumes[price] -= order.volume;
                if (order.isBuy)
                {
                    order.owner?.OnBuyOrderCancelled(stock, order.volume, price);
                }
                else
                {
                    NotifySellCancellation(order, stock);
                }

                removed++;
            }

            if (queue.Count == 0)
            {
                book.RemoveAt(i);
                volumes.Remove(price);
            }
            else
            {
                i++;
            }
        }

        return removed;
    }

    /// <summary>소유자의 매도 주문 중 가격이 maxPriceExclusive 미만인 것만 전부 취소. RealPrice 미만 벽 제거용.</summary>
    public int CancelSellOrdersByOwnerBelowPrice(IOrderOwner owner, decimal maxPriceExclusive, Stock stock)
    {
        if (owner == null || !_ownerOrderTotals.ContainsKey(owner))
        { return 0; }

        int cancelled = 0;
        for (int i = 0; i < asks.Count;)
        {
            decimal price = asks.Keys[i];
            if (price >= maxPriceExclusive)
            {
                i++;
                continue;
            }

            var queue = asks[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner == owner)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.owner != null && order.volume > 0m)
                    {
                        NotifySellCancellation(order, stock);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _askVolumes.TryGetValue(price, out var av))
            {
                _askVolumes[price] = av - removedVol;
            }

            if (keep.Count == 0)
            {
                asks.RemoveAt(i);
                _askVolumes.RemoveAt(i);
            }
            else
            {
                asks[price] = keep;
                i++;
            }
        }

        if (cancelled > 0)
        {
            Version++;
        }

        if (cancelled > 0)
        {
            _askVolumeDirty = true;
        }

        return cancelled;
    }

    /// <summary>특정 소유자의 모든 주문(매수+매도) 취소. 봇 이탈 시 호출.</summary>
    public int CancelAllOrdersByOwner(IOrderOwner owner, Stock stock)
    {
        if (owner == null || !_ownerOrderTotals.ContainsKey(owner))
        { return 0; }

        int cancelled = 0;
        for (int i = 0; i < asks.Count;)
        {
            decimal price = asks.Keys[i];
            var queue = asks[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner == owner)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.volume > 0m)
                    {
                        NotifySellCancellation(order, stock);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _askVolumes.TryGetValue(price, out var av))
            {
                _askVolumes[price] = av - removedVol;
            }

            if (keep.Count == 0)
            {
                asks.RemoveAt(i);
                _askVolumes.Remove(price);
            }
            else
            {
                asks[price] = keep;
                i++;
            }
        }

        for (int i = 0; i < bids.Count;)
        {
            decimal price = bids.Keys[i];
            var queue = bids[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner == owner)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.volume > 0m)
                    {
                        owner.OnBuyOrderCancelled(stock, order.volume, price);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _bidVolumes.TryGetValue(price, out var bv))
            {
                _bidVolumes[price] = bv - removedVol;
            }

            if (keep.Count == 0)
            {
                bids.RemoveAt(i);
                _bidVolumes.Remove(price);
            }
            else
            {
                bids[price] = keep;
                i++;
            }
        }

        if (cancelled > 0)
        {
            Version++;
        }

        if (cancelled > 0)
        {
            _askVolumeDirty = true;
            _bidVolumeDirty = true;
        }

        return cancelled;
    }

    /// <summary>특정 소유자의 대기 주문을 확률적으로 취소. 각 주문마다 cancelChance 확률로 취소.</summary>
    public int CancelRandomOrdersByOwner(IOrderOwner owner, Stock stock, float cancelChance)
    {
        if (owner == null || cancelChance <= 0f || !_ownerOrderTotals.ContainsKey(owner))
        { return 0; }

        int cancelled = 0;
        for (int i = 0; i < asks.Count;)
        {
            decimal price = asks.Keys[i];
            var queue = asks[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner == owner && UnityEngine.Random.value < cancelChance)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.volume > 0m)
                    {
                        NotifySellCancellation(order, stock);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _askVolumes.TryGetValue(price, out var av))
            {
                _askVolumes[price] = av - removedVol;
            }

            if (keep.Count == 0)
            {
                asks.RemoveAt(i);
                _askVolumes.RemoveAt(i);
            }
            else
            {
                asks[price] = keep;
                i++;
            }
        }

        for (int i = 0; i < bids.Count;)
        {
            decimal price = bids.Keys[i];
            var queue = bids[price];
            if (!_ownerOrderCounts.ContainsKey((owner, queue.Peek().isBuy, price)))
            {
                i++;
                continue;
            }

            var keep = queue;
            int remainingOrders = queue.Count;
            decimal removedVol = 0m;
            while (remainingOrders-- > 0)
            {
                var order = queue.Dequeue();
                if (order.owner == owner && UnityEngine.Random.value < cancelChance)
                {
                    cancelled++;
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    removedVol += order.volume;
                    if (order.volume > 0m)
                    {
                        owner.OnBuyOrderCancelled(stock, order.volume, price);
                    }
                }
                else
                {
                    keep.Enqueue(order);
                }
            }

            if (removedVol > 0m && _bidVolumes.TryGetValue(price, out var bv))
            {
                _bidVolumes[price] = bv - removedVol;
            }

            if (keep.Count == 0)
            {
                bids.RemoveAt(i);
                _bidVolumes.Remove(price);
            }
            else
            {
                bids[price] = keep;
                i++;
            }
        }

        if (cancelled > 0)
        {
            Version++;
        }

        if (cancelled > 0)
        {
            _askVolumeDirty = true;
            _bidVolumeDirty = true;
        }

        return cancelled;
    }

#endregion
#region Matching Logic
    static decimal Min(decimal a, decimal b) => a <= b ? a : b;
    /// <summary>매수 체결. 최우선 매도(Keys[0])부터 순서대로 매칭. maxMatchAskPrice가 있으면 이 가격 초과 매도는 체결하지 않음(고가 매도 보존).</summary>
    void MatchBuy(decimal price, decimal volume, TradeResult result, bool addUnfilledToBook = true, IOrderOwner orderOwner = null, Stock stock = null, decimal? maxMatchAskPrice = null)
    {
        bool modified = false;
        while (volume > 0m && asks.Count > 0)
        {
            decimal askPrice = asks.Keys[0];
            if (askPrice > price)
            { break; }

            if (maxMatchAskPrice.HasValue && askPrice > maxMatchAskPrice.Value)
            { break; }

            var queue = asks[askPrice];
            while (queue.Count > 0 && volume > 0m)
            {
                var order = queue.Peek();
                if (orderOwner != null && ReferenceEquals(order.owner, orderOwner))
                {
                    queue.Dequeue();
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    if (order.isBuy)
                    {
                        _bidVolumes[order.price] -= order.volume;
                        order.owner.OnBuyOrderCancelled(stock, order.volume, order.price);
                    }
                    else
                    {
                        _askVolumes[order.price] -= order.volume;
                        NotifySellCancellation(order, stock);
                    }

                    modified = true;
                    continue;
                }

                decimal traded = Min(volume, order.volume);
                volume -= traded;
                order.volume -= traded;
                if (_askVolumes.TryGetValue(askPrice, out var av))
                {
                    _askVolumes[askPrice] = av - traded;
                }

                result.filledVolume += traded;
                result.totalCost += traded * askPrice;
                lastPrice = askPrice;
                modified = true;
                if (order.owner is Trader seller && stock != null)
                {
                    seller.OnSellOrderFilledAtCost(stock, traded, traded * askPrice, askPrice, order.costBasis);
                }

                stock?.Matched(askPrice, traded, orderOwner, order.owner, order);
                if (order.volume <= 0m)
                {
                    queue.Dequeue();
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                }
            }

            if (queue.Count == 0)
            {
                asks.RemoveAt(0);
                _askVolumes.RemoveAt(0);
            }
        }

        if (modified)
        {
            Version++;
        }

        if (modified)
        {
            _askVolumeDirty = true;
        }

        if (volume > 0m && addUnfilledToBook)
        {
            result.restingOrder = AddOrder(price, volume, true, alreadyNormalized: true, orderOwner: orderOwner);
        }
    }

    /// <summary>매도 체결. 최우선 매수(Keys[0])부터 순서대로 매칭. 매수 호가가 없으면 체결 0, 미체결 분만 addUnfilledToBook이면 호가창에 등록.</summary>
    void MatchSell(decimal price, decimal volume, TradeResult result, bool addUnfilledToBook = true, IOrderOwner orderOwner = null, Stock stock = null)
    {
        bool modified = false;
        while (volume > 0m && bids.Count > 0)
        {
            decimal bidPrice = bids.Keys[0];
            if (bidPrice < price)
            { break; }

            var queue = bids[bidPrice];
            while (queue.Count > 0 && volume > 0m)
            {
                var order = queue.Peek();
                if (orderOwner != null && ReferenceEquals(order.owner, orderOwner))
                {
                    queue.Dequeue();
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                    if (order.isBuy)
                    {
                        _bidVolumes[order.price] -= order.volume;
                        order.owner.OnBuyOrderCancelled(stock, order.volume, order.price);
                    }
                    else
                    {
                        _askVolumes[order.price] -= order.volume;
                        NotifySellCancellation(order, stock);
                    }

                    modified = true;
                    continue;
                }

                decimal traded = Min(volume, order.volume);
                volume -= traded;
                order.volume -= traded;
                if (_bidVolumes.TryGetValue(bidPrice, out var bv))
                {
                    _bidVolumes[bidPrice] = bv - traded;
                }

                result.filledVolume += traded;
                result.totalCost += traded * bidPrice;
                lastPrice = bidPrice;
                modified = true;
                if (order.owner != null && stock != null)
                {
                    order.owner.OnOrderFilled(stock, traded, traded * bidPrice);
                }

                stock?.Matched(bidPrice, traded, order.owner, orderOwner, order);
                if (order.volume <= 0m)
                {
                    queue.Dequeue();
                    TrackOrder(order.owner, order.isBuy, order.price, -1, order);
                }
            }

            if (queue.Count == 0)
            {
                bids.RemoveAt(0);
                _bidVolumes.RemoveAt(0);
            }
        }

        if (modified)
        {
            Version++;
        }

        if (modified)
        {
            _bidVolumeDirty = true;
        }

        if (volume > 0m && addUnfilledToBook)
        {
            result.restingOrder = AddOrder(price, volume, false, alreadyNormalized: true, orderOwner: orderOwner);
        }
    }

#endregion
#region Book Management
    /// <summary>호가창에 추가 시 가격을 항상 틱 단위로 통일. 서브틱·소수 오차로 같은 가격이 두 줄로 나오는 것 방지.</summary>
    Order AddOrder(decimal price, decimal volume, bool isBuy, bool alreadyNormalized = false, IOrderOwner orderOwner = null)
    {
        Version++;
        price = PriceTickPolicy.FloorToTick(price);
        var book = isBuy ? bids : asks;
        var volumes = isBuy ? _bidVolumes : _askVolumes;
        if (!book.ContainsKey(price))
        {
            book.Add(price, new Queue<Order>());
        }

        var order = new Order(price, volume, isBuy, ++timestampCounter, orderOwner);
        book[price].Enqueue(order);
        TrackOrder(orderOwner, isBuy, price, 1, order);
        if (volumes.ContainsKey(price))
        {
            volumes[price] += volume;
        }
        else
        {
            volumes.Add(price, volume);
        }

        if (isBuy)
        {
            _bidVolumeDirty = true;
        }
        else
        {
            _askVolumeDirty = true;
        }

        return order;
    }
#endregion
}
