using System;
using System.Globalization;

/// <summary>한국거래소 일반 주식 호가 단위. 최소 주가는 1원이며 금액 표시는 원 단위.</summary>
public static class PriceTickPolicy
{
    private static readonly (decimal maxExclusive, decimal tick)[] Tiers =
    {
        (2000m, 1m),
        (5000m, 5m),
        (20000m, 10m),
        (50000m, 50m),
        (200000m, 100m),
        (500000m, 500m)
    };
    private const decimal MinPrice = 1m;
    /// <summary>Resting retail quotes cluster at round numbers without crossing the original limit.</summary>
    public static decimal RoundRetailQuote(decimal price, bool buy)
    {
        if (price <= 0m)
        { return price; }
        decimal step = 1m;
        while (price / step >= 100m)
        { step *= 10m; }
        step = Math.Max(step, GetTickSize(price));
        decimal rounded = (buy ? decimal.Floor(price / step) : decimal.Ceiling(price / step)) * step;
        return buy ? Math.Max(1m, FloorToTick(rounded)) : CeilToTick(rounded);
    }
    public static decimal GetTickSize(decimal price)
    {
        foreach (var (max, tick) in Tiers)
        {
            if (price < max)
            { return tick; }
        }
        return 1000m;
    }
    public static int GetDisplayDecimals(decimal price)
    { return 0; }

    /// <summary>주가·호가 표시. 일반 금액/평균단가는 FormatMoney 사용.</summary>
    public static string FormatPrice(decimal price)
    {
        if (price <= 0m)
        { return "0"; }
        return FormatMoney(RoundToTick(price));
    }
    public static string FormatMoney(decimal amount)
    {
        return Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
    }
    public static decimal RoundToTick(decimal price)
    {
        if (price <= 0m)
        { return 0m; }
        decimal tick = GetTickSize(price);
        decimal floor = FloorToTick(price);
        decimal remainder = price - floor;
        if (remainder >= tick / 2m && floor <= decimal.MaxValue - tick)
        {
            floor += tick;
        }
        return Math.Max(MinPrice, floor);
    }
    public static decimal FloorToTick(decimal price)
    {
        if (price <= 0m)
        { return 0m; }
        return price - price % GetTickSize(price);
    }
    public static decimal CeilToTick(decimal price)
    {
        if (price <= 0m)
        { return 0m; }
        decimal floor = FloorToTick(price);
        if (floor == price)
        { return floor; }
        return floor + GetTickSize(price);
    }
    /// <summary>경계에서도 바로 아래 유효호가: 2,000 → 1,999, 5,000 → 4,995.</summary>
    public static decimal OneTickBelow(decimal price)
    {
        if (price <= MinPrice)
        { return 0m; }
        decimal upper = CeilToTick(price);
        return FloorToTick(upper - GetTickSize(upper - MinPrice));
    }
    public static decimal OneTickAbove(decimal price)
    {
        if (price < MinPrice)
        { return MinPrice; }
        decimal lower = FloorToTick(price);
        decimal tick = GetTickSize(lower);
        if (lower > decimal.MaxValue - tick)
        { return 0m; }
        return lower + tick;
    }
    public static bool IsValidPrice(decimal price)
    {
        return price >= MinPrice && price % GetTickSize(price) == 0m;
    }
    /// <summary>호환 API. 서브틱 요청도 실제 주식 호가 단위를 사용하여 우회 주문을 막는다.</summary>
    public static decimal GetSubTickSize(decimal price, int steps)
    { return GetTickSize(price); }
    public static decimal FloorToSubTick(decimal price, int steps)
    { return FloorToTick(price); }
    public static decimal CeilToSubTick(decimal price, int steps)
    { return CeilToTick(price); }
    public static decimal GapPriceBetween(decimal low, decimal high)
    {
        if (low >= high || low <= 0m)
        { return 0m; }
        decimal price = RoundToTick(low + (high - low) / 2m);
        if (price > low && price < high)
        { return price; }
        price = OneTickAbove(low);
        if (price > low && price < high)
        { return price; }
        price = OneTickBelow(high);
        if (price > low && price < high)
        { return price; }
        return 0m;
    }
    public static decimal FloorToLargerTick(decimal price)
    {
        if (price <= 0m)
        { return 0m; }
        decimal tick = GetTickSize(price) * 10m;
        return price - price % tick;
    }
    public static decimal CeilToLargerTick(decimal price)
    {
        if (price <= 0m)
        { return 0m; }
        decimal tick = GetTickSize(price) * 10m;
        return Math.Ceiling(price / tick) * tick;
    }
}
