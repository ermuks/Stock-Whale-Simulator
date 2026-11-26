using System;
using System.Collections.Generic;

/// <summary>차트 전용 읽기 모델. 매도 예약 수량을 포함한 평단과 실제 미체결 주문만 읽는다.</summary>
public sealed class ChartTradeLevels
{
    public enum Kind { Average, Buy, Sell, Crowd }
    public readonly struct Level
    {
        public readonly int Account;
        public readonly Kind Type;
        public readonly decimal Price, Quantity;
        public Level(int account, Kind type, decimal price, decimal quantity)
        { Account = account; Type = type; Price = price; Quantity = quantity; }
    }

    private readonly List<Level> levels = new(16);
    private readonly List<Order> orders = new(32);
    private readonly Dictionary<(int, Kind, decimal), int> indices = new();
    public IReadOnlyList<Level> Levels => levels;

    public void Read(Stock stock, Trader primary, Trader automated)
    {
        levels.Clear(); indices.Clear();
        if (stock == null || stock.IsDelisted)
        { return; }
        ReadAccount(stock, primary, 1);
        if (automated != primary)
        { ReadAccount(stock, automated, 2); }
        // 시장이 믿는 가격(봇들 기대값의 평균). 값은 이쪽으로 끌려가므로 차트 위에 선으로 겹쳐 그 방향을 보인다.
        if (stock.fakeValue > 0m && PlayerProgression.Has(PlayerProgression.CrowdValueUnlock))
        { Add(0, Kind.Crowd, PriceTickPolicy.RoundToTick(stock.fakeValue), 1m); }
        levels.Sort(ComparePrice);
    }

    private void ReadAccount(Stock stock, Trader account, int number)
    {
        if (account == null)
        { return; }
        decimal owned = account.GetOwnedVolume(stock);
        if (owned > 0m)
        { Add(number, Kind.Average, account.GetOwnedAverage(stock), owned); }
        if (stock.orderBook == null)
        { return; }
        // 소유자 인덱스로 두 플레이어 계좌만 조회한다. 봇 주문 전체를 순회하지 않는다.
        stock.orderBook.CopyOwnerOrders(account, orders);
        foreach (var order in orders)
        {
            if (order == null || order.volume <= 0m || !stock.orderBook.IsActive(order))
            { continue; }
            Add(number, order.isBuy ? Kind.Buy : Kind.Sell, order.price, order.volume);
        }
    }

    private void Add(int account, Kind kind, decimal price, decimal quantity)
    {
        if (price <= 0m || quantity <= 0m)
        { return; }
        var key = (account, kind, price);
        if (indices.TryGetValue(key, out int index))
        { levels[index] = new Level(account, kind, price, levels[index].Quantity + quantity); }
        else
        { indices.Add(key, levels.Count); levels.Add(new Level(account, kind, price, quantity)); }
    }

    public static bool InRange(Level level, ChartPriceScale scale)
    { return (double)level.Price >= scale.Minimum && (double)level.Price <= scale.Maximum; }

    public static int ComparePrice(Level left, Level right)
    {
        int price = left.Price.CompareTo(right.Price);
        if (price != 0)
        { return price; }
        int kind = left.Type.CompareTo(right.Type);
        return kind != 0 ? kind : left.Account.CompareTo(right.Account);
    }
}
