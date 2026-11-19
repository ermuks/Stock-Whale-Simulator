using System;
using System.Collections.Generic;
using UnityEngine;

// 시세 조종 수법의 공통 도구. 세력 봇과 플레이어가 같은 것을 쓴다.
// 모두 실제 계좌의 현금과 주식으로, 실제 호가창에 실제 주문을 낸다. 가격·잔고·거래량을 지어내지 않는다.
//
// 1) 통정매매(거래량 부풀리기): 호가창은 자기 주문끼리 체결되지 않게 막혀 있다. 그래서 차명 계좌와 짜고,
//    매수·매도 호가 사이의 빈 값에 주식을 넘겼다가 같은 값에 되받는다. 돈과 주식은 제자리로 돌아오고 거래량만 남는다.
//    거래량이 평소의 몇 배로 뛰면 종목의 열기가 올라 다른 사람들의 눈이 쏠린다(StockInterestManager).
// 2) 허수 매수벽: 체결될 생각이 없는 큰 매수 주문을 현재가 조금 아래에 걸어 둔다. "받치는 돈이 있다"고 보이게 하려는 것이다.
//    값이 벽 가까이 내려오면 거둔다. 벽은 진짜 주문이라 미처 못 거두면 체결된다.
// 3) 호가 기울기: 다른 사람들은 매수 잔량이 매도 잔량보다 훨씬 두꺼우면 안심하고 따라 사고, 반대면 서둘러 판다. 허수벽이 통하는 이유다.
public static class ManipulationTools
{
    /// <summary>허수 매수벽을 현재 최우선 매수호가에서 이만큼 아래에 건다. 값이 벽에서 이만큼 안으로 다가오면 거둔다.</summary>
    public const decimal WallDepth = .025m, WallPullWithin = .008m;
    /// <summary>호가 기울기를 볼 때 최우선 호가에서 이만큼 안의 잔량만 센다. 한쪽이 다른 쪽의 이 배수를 넘으면 기울었다고 본다.</summary>
    public const decimal TiltBand = .04m, TiltRatio = 2.5m;
    private static readonly Dictionary<Trader, Trader> nominees = new();
    private static readonly HashSet<Trader> nomineeSet = new();
    private static readonly List<Order> owned = new(32);
    private static readonly List<(decimal price, decimal volume)> levels = new(16);
    private static readonly Dictionary<Stock, (float at, int tilt)> tilts = new();

    public static bool IsNominee(IOrderOwner owner) => owner is Trader trader && nomineeSet.Contains(trader);

    /// <summary>이 계좌의 차명 계좌. 처음 쓸 때 만든다. 돈도 주식도 없이 시작하고, 통정매매 한 바퀴가 끝나면 다시 빈 계좌가 된다.</summary>
    public static Trader Nominee(Trader owner)
    {
        if (owner == null) { return null; }
        if (nominees.TryGetValue(owner, out var existing) && existing != null) { return existing; }
        var holder = new GameObject("Area :: Nominee Account");
        holder.transform.SetParent(owner.transform, false);
        var nominee = holder.AddComponent<Trader>();
        nominee.cash = 0m;
        // 차명 계좌는 한 바퀴 안에서만 돈과 주식을 잠깐 쥔다. 계좌 명단(저장·배당·초기 자금 배정)에는 올리지 않는다.
        Trader.Registry.Remove(nominee);
        nominees[owner] = nominee; nomineeSet.Add(nominee);
        return nominee;
    }

    private static void Buy(Trader trader, decimal price, decimal volume, Stock stock)
    {
        if (trader is TradeBot bot) { bot.RawBuyLimit(price, volume, stock); } else { trader.BuyLimit(price, volume, 0, stock); }
    }
    private static void Sell(Trader trader, decimal price, decimal volume, Stock stock)
    {
        if (trader is TradeBot bot) { bot.RawSellLimit(price, volume, stock); } else { trader.SellLimit(price, volume, 0, stock); }
    }

    /// <summary>
    /// 통정매매 한 바퀴. value원어치를 차명 계좌에 넘겼다가 같은 값에 되받는다. 실제로 찍힌 거래대금을 돌려준다(0이면 못 했다).
    /// 매수·매도 호가 사이에 빈 값이 있어야 하고(두 틱 이상 벌어져 있어야 한다), 넘길 주식과 그만큼의 현금이 있어야 한다.
    /// </summary>
    public static decimal WashRound(Trader owner, Stock stock, decimal value)
    {
        if (owner == null || stock == null || value <= 0m || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.orderBook == null) { return 0m; }
        if (!stock.orderBook.TryBest(out decimal bid, out decimal ask)) { return 0m; }
        decimal tick = PriceTickPolicy.GetTickSize(bid);
        if (tick <= 0m || ask - bid < tick * 2m) { return 0m; }
        // 스프레드 위쪽에 찍는다. 체결가가 매도호가 바로 아래에 찍혀서 사자가 붙은 것처럼 보인다.
        decimal price = PriceTickPolicy.FloorToTick(ask - tick);
        if (price <= bid) { return 0m; }
        var nominee = Nominee(owner);
        Settle(owner, nominee, stock);
        decimal quantity = decimal.Floor(value / price / 2m);
        quantity = Math.Min(quantity, owner.GetHolding(stock));
        quantity = Math.Min(quantity, decimal.Floor(Math.Max(0m, owner.cash - owner.ReservedCash) / price));
        if (quantity < 1m) { return 0m; }
        decimal lend = price * quantity;
        owner.cash -= lend; nominee.cash += lend;
        // 넘기기: 내 매도가 그 값의 유일한 주문이 되고, 차명 계좌가 바로 받는다.
        Sell(owner, price, quantity, stock);
        Buy(nominee, price, quantity, stock);
        decimal passed = nominee.GetHolding(stock);
        // 되받기: 차명 계좌가 같은 값에 내놓고, 내가 받는다.
        if (passed >= 1m) { Sell(nominee, price, passed, stock); Buy(owner, price, passed, stock); }
        Cleanup(owner, nominee, stock, price);
        return passed * price * 2m;
    }
    /// <summary>차명 계좌에 남은 주식과 돈을 주인에게 돌려놓는다(한 바퀴가 중간에 어긋났을 때).</summary>
    private static void Settle(Trader owner, Trader nominee, Stock stock)
    {
        decimal stuck = nominee.GetHolding(stock);
        if (stuck >= 1m && stock.orderBook.TryBest(out decimal bid, out decimal ask))
        {
            decimal tick = PriceTickPolicy.GetTickSize(bid);
            if (ask - bid >= tick * 2m)
            {
                decimal price = PriceTickPolicy.FloorToTick(ask - tick);
                decimal afford = decimal.Floor(Math.Max(0m, owner.cash - owner.ReservedCash) / price), back = Math.Min(stuck, afford);
                if (back >= 1m) { Sell(nominee, price, back, stock); Buy(owner, price, back, stock); Cleanup(owner, nominee, stock, price); }
            }
        }
        decimal idle = Math.Max(0m, nominee.cash - nominee.ReservedCash);
        if (idle > 0m) { nominee.cash -= idle; owner.cash += idle; }
    }
    private static void Cleanup(Trader owner, Trader nominee, Stock stock, decimal price)
    {
        stock.orderBook.CancelAllOrdersByOwner(nominee, stock);
        // 넘기려던 내 매도가 남았으면 거둔다(그 값에 걸린 것만).
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (!order.isBuy && order.price == price && stock.orderBook.IsActive(order)) { stock.orderBook.CancelOrder(order, stock); } }
        decimal idle = Math.Max(0m, nominee.cash - nominee.ReservedCash);
        if (idle > 0m) { nominee.cash -= idle; owner.cash += idle; }
    }

    /// <summary>
    /// 불붙이기: 매도 호가를 한 번에 쓸어 올린다. 지금 최우선 매도호가에서 reach(예: 0.05 = 5%) 위까지, cash원어치까지 산다.
    /// 다 못 산 주문은 그 자리에서 거둔다(높은 값에 매수벽으로 남지 않는다). 산 수량을 돌려준다.
    /// 값을 끝까지 자기 돈으로 올리는 것이 아니다. 큰 양봉 하나로 눈길을 끌고, 그 뒤는 따라붙는 사람들이 올리게 둔다.
    /// </summary>
    public static decimal Ignite(Trader owner, Stock stock, decimal cash, decimal reach)
    {
        if (owner == null || stock == null || cash <= 0m || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.orderBook == null) { return 0m; }
        if (!stock.orderBook.TryBest(out _, out decimal ask)) { return 0m; }
        decimal limit = PriceTickPolicy.CeilToTick(ask * (1m + Math.Max(0m, reach)));
        decimal quantity = decimal.Floor(Math.Min(cash, Math.Max(0m, owner.cash - owner.ReservedCash)) / limit);
        if (quantity < 1m) { return 0m; }
        decimal before = owner.GetOwnedVolume(stock);
        Buy(owner, limit, quantity, stock);
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (order.isBuy && order.price == limit && stock.orderBook.IsActive(order)) { stock.orderBook.CancelOrder(order, stock); } }
        return Math.Max(0m, owner.GetOwnedVolume(stock) - before);
    }

    /// <summary>
    /// 내려찍기: 가진 주식을 매수 호가에 한 번에 던진다. 지금 최우선 매수호가에서 depth(예: 0.06 = 6%) 아래까지, floor보다 낮지 않은 값까지 받아 친다.
    /// 다 못 판 주문은 그 자리에서 거둔다. 매도벽으로 남겨 두지 않는다(위를 막아 세우면 아무도 올리지 못한다). 판 수량을 돌려준다.
    /// </summary>
    public static decimal Slam(Trader owner, Stock stock, decimal shares, decimal depth, decimal floor)
    {
        if (owner == null || stock == null || shares < 1m || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.orderBook == null) { return 0m; }
        if (!stock.orderBook.TryBest(out decimal bid, out _)) { return 0m; }
        decimal limit = Math.Max(Math.Max(1m, PriceTickPolicy.CeilToTick(floor)), PriceTickPolicy.FloorToTick(bid * (1m - Math.Max(0m, depth))));
        decimal quantity = decimal.Floor(Math.Min(shares, owner.GetHolding(stock)));
        if (limit > bid || quantity < 1m) { return 0m; }
        decimal before = owner.GetOwnedVolume(stock);
        Sell(owner, limit, quantity, stock);
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (!order.isBuy && order.price == limit && stock.orderBook.IsActive(order)) { stock.orderBook.CancelOrder(order, stock); } }
        return Math.Max(0m, before - owner.GetOwnedVolume(stock));
    }

    /// <summary>
    /// 털기: 매수 호가 몇 칸(levels)을 한 번에 받아 친다. 수량은 호가창에서 정한다: 그 칸들에 서 있는 물량을 다 받고, 그보다 조금 더(over, 예: 0.2 = 20%) 다음 칸까지 먹는다.
    /// 다 못 판 주문은 그 자리에서 거둔다. 매도 호가에 물량을 얹지 않으므로 매도벽이 생기지 않는다. 판 수량을 돌려준다.
    /// 값이 오르는 동안 티 나지 않게 조금씩 이익을 챙기는 수법이다. 내려찍기(Slam)의 작은 판이다.
    /// </summary>
    public static decimal Nip(Trader owner, Stock stock, int levelCount, decimal over, decimal maxShares, decimal floor)
    {
        if (owner == null || stock == null || maxShares < 1m || levelCount < 1 || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.orderBook == null) { return 0m; }
        stock.orderBook.GetTopBids(levels, levelCount + 1);
        if (levels.Count == 0) { return 0m; }
        decimal standing = 0m, limit = 0m; int taken = 0;
        for (int i = 0; i < levels.Count && i < levelCount; i++)
        {
            if (levels[i].price < floor) { break; }
            standing += levels[i].volume; limit = levels[i].price; taken++;
        }
        if (taken == 0) { return 0m; }
        // 조금 더 먹을 다음 칸이 있고 바닥선 위면 거기까지 값을 내린다.
        if (levels.Count > taken && levels[taken].price >= floor) { limit = levels[taken].price; }
        decimal quantity = decimal.Floor(Math.Min(Math.Min(maxShares, owner.GetHolding(stock)), decimal.Ceiling(standing * (1m + Math.Max(0m, over)))));
        if (quantity < 1m) { return 0m; }
        decimal before = owner.GetOwnedVolume(stock);
        Sell(owner, limit, quantity, stock);
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (!order.isBuy && order.price == limit && stock.orderBook.IsActive(order)) { stock.orderBook.CancelOrder(order, stock); } }
        return Math.Max(0m, before - owner.GetOwnedVolume(stock));
    }

    /// <summary>허수 매수벽을 건다. 건 값을 돌려준다(0이면 못 걸었다).</summary>
    public static decimal PlaceWall(Trader owner, Stock stock, decimal cashToShow)
    {
        if (owner == null || stock == null || cashToShow <= 0m || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.orderBook == null) { return 0m; }
        if (!stock.orderBook.TryBest(out decimal bid, out _)) { return 0m; }
        decimal price = PriceTickPolicy.FloorToTick(bid * (1m - WallDepth));
        if (price <= 0m) { return 0m; }
        decimal quantity = decimal.Floor(Math.Min(cashToShow, Math.Max(0m, owner.cash - owner.ReservedCash)) / price);
        if (quantity < 1m) { return 0m; }
        Buy(owner, price, quantity, stock);
        return WallVolume(owner, stock, price) > 0m ? price : 0m;
    }
    /// <summary>그 값에 걸려 있는 내 매수 잔량.</summary>
    public static decimal WallVolume(Trader owner, Stock stock, decimal price)
    {
        if (owner == null || stock == null || stock.orderBook == null || price <= 0m) { return 0m; }
        decimal total = 0m;
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (order.isBuy && order.price == price && stock.orderBook.IsActive(order)) { total += order.volume; } }
        return total;
    }
    public static void PullWall(Trader owner, Stock stock, decimal price)
    {
        if (owner == null || stock == null || stock.orderBook == null || price <= 0m) { return; }
        stock.orderBook.CopyOwnerOrders(owner, owned);
        foreach (var order in owned) { if (order.isBuy && order.price == price && stock.orderBook.IsActive(order)) { stock.orderBook.CancelOrder(order, stock); } }
    }
    /// <summary>값이 벽 가까이 내려왔는가(거둬야 하는가).</summary>
    public static bool WallThreatened(Stock stock, decimal wallPrice)
        => stock != null && stock.orderBook != null && stock.orderBook.TryBest(out decimal bid, out _) && bid <= wallPrice * (1m + WallPullWithin);
    /// <summary>값이 올라가 벽이 너무 멀어졌는가(올려 걸어야 하는가).</summary>
    public static bool WallStale(Stock stock, decimal wallPrice)
        => stock != null && stock.orderBook != null && stock.orderBook.TryBest(out decimal bid, out _) && wallPrice < bid * (1m - WallDepth * 1.8m);

    /// <summary>호가창에서 읽은 벽. Bid·Ask는 벽의 세기(0이면 벽이 없다, 1이면 아주 크다), BidPrice·AskPrice는 그 벽이 선 값.</summary>
    public struct WallView { public float Bid, Ask; public decimal BidPrice, AskPrice; public float Lean => Bid - Ask; }
    /// <summary>벽으로 치는 기준: 한 호가의 잔량(금액)이 주변 호가의 보통 크기의 이 배수를 넘어야 하고, WallFull배면 세기 1이다. 최우선 호가에서 5% 안만 본다.</summary>
    public const float WallFrom = 3f, WallFull = 12f; public const decimal WallBand = .05m;
    /// <summary>
    /// 벽으로 치는 최소 금액. 주변 호가보다 몇 배 크더라도 돈으로 따져 작으면 벽이 아니다
    /// (한 주가 120만 원인 종목에서 23주, 2천7백만 원어치를 보고 "엄청 받쳐 놨다"고 하던 것을 막는다).
    /// 3천만 원과 시가총액의 0.05% 가운데 큰 쪽이다.
    /// </summary>
    public const decimal WallMinimumValue = 30_000_000m, WallMinimumCapShare = .0005m;
    public static decimal WallFloor(Stock stock) => stock == null ? WallMinimumValue : Math.Max(WallMinimumValue, stock.currentPrice * stock.issuedShares * WallMinimumCapShare);
    private static readonly Dictionary<Stock, (float at, WallView view)> walls = new();
    private static readonly List<decimal> sizes = new(24);
    /// <summary>
    /// 호가창에 눈에 띄게 큰 주문(벽)이 있는지 본다. 누구나 보는 호가창만 읽고, 그 주문이 진짜인지 허수인지는 가리지 않는다(가릴 방법이 없다).
    /// 매수 쪽과 매도 쪽 각각 5% 안의 열 호가를 보고, 가장 큰 호가가 양쪽 호가의 가운데 크기(중앙값)의 몇 배인지로 세기를 정한다. 1초 동안은 같은 답을 돌려준다.
    /// </summary>
    public static WallView Walls(Stock stock)
    {
        var view = new WallView();
        if (stock == null || stock.orderBook == null) { return view; }
        float now = SessionClock.Time;
        if (walls.TryGetValue(stock, out var cached) && now - cached.at < 1f && now >= cached.at) { return cached.view; }
        if (stock.orderBook.TryBest(out decimal bid, out decimal ask))
        {
            sizes.Clear(); decimal bidTop = 0m, askTop = 0m;
            stock.orderBook.GetTopBids(levels, 10);
            foreach (var level in levels)
            {
                if (level.price < bid * (1m - WallBand)) { continue; }
                decimal size = level.price * level.volume; sizes.Add(size);
                if (size > bidTop) { bidTop = size; view.BidPrice = level.price; }
            }
            stock.orderBook.GetTopAsks(levels, 10);
            foreach (var level in levels)
            {
                if (level.price > ask * (1m + WallBand)) { continue; }
                decimal size = level.price * level.volume; sizes.Add(size);
                if (size > askTop) { askTop = size; view.AskPrice = level.price; }
            }
            if (sizes.Count >= 6)
            {
                sizes.Sort(); decimal typical = sizes[sizes.Count / 2];
                if (typical > 0m)
                {
                    decimal floor = WallFloor(stock);
                    view.Bid = bidTop < floor ? 0f : Mathf.Clamp01(((float)(bidTop / typical) - WallFrom) / (WallFull - WallFrom));
                    view.Ask = askTop < floor ? 0f : Mathf.Clamp01(((float)(askTop / typical) - WallFrom) / (WallFull - WallFrom));
                }
            }
        }
        walls[stock] = (now, view);
        return view;
    }

    /// <summary>
    /// 호가창의 기울기. 최우선 호가에서 4% 안의 잔량을 금액으로 견준다. +1: 매수 잔량이 2.5배 넘게 두껍다, -1: 매도 잔량이 그렇다, 0: 비슷하다.
    /// 누구나 보는 호가창만 읽는다. 1초 동안은 같은 답을 돌려준다.
    /// </summary>
    public static int BookTilt(Stock stock)
    {
        if (stock == null || stock.orderBook == null) { return 0; }
        float now = SessionClock.Time;
        if (tilts.TryGetValue(stock, out var cached) && now - cached.at < 1f && now >= cached.at) { return cached.tilt; }
        int tilt = 0;
        if (stock.orderBook.TryBest(out decimal bid, out decimal ask))
        {
            decimal bids = 0m, asks = 0m;
            stock.orderBook.GetTopBids(levels, 10);
            foreach (var level in levels) { if (level.price >= bid * (1m - TiltBand)) { bids += level.price * level.volume; } }
            stock.orderBook.GetTopAsks(levels, 10);
            foreach (var level in levels) { if (level.price <= ask * (1m + TiltBand)) { asks += level.price * level.volume; } }
            if (bids > 0m && asks > 0m) { tilt = bids >= asks * TiltRatio ? 1 : asks >= bids * TiltRatio ? -1 : 0; }
        }
        tilts[stock] = (now, tilt);
        return tilt;
    }
}
