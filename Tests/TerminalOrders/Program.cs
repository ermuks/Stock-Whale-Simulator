using System;
using System.Collections.Generic;
using UnityEngine;

public static class TerminalOrderTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) { throw new Exception("FAIL " + name); }
        passed++; Console.WriteLine("PASS " + name);
    }
    private static (Trader player, DeviceOrderSession session, Stock a, Stock b) Setup()
    {
        Time.time = 0; Time.timeScale = 1;
        StockManager.Instance = new StockManager(); MarketDayController.Instance = new MarketDayController();
        var a = new Stock("A","종목 A",1000,1000000); var b = new Stock("B","종목 B",1000,1000000);
        StockManager.Instance.stocks.Add(a); StockManager.Instance.stocks.Add(b);
        var player = new GameObject("Player").AddComponent<Trader>(); player.cash = 10000;
        StockManager.Instance.playerAccount = player;
        var session = new GameObject("Orders").AddComponent<DeviceOrderSession>(); session.account = player; session.Prepare(a);
        return (player,session,a,b);
    }
    private static Trader Seller(Stock stock, decimal quantity)
    {
        var trader = new GameObject("Seller").AddComponent<Trader>(); trader.cash = 1000000; trader.GrantShares(stock,quantity,1000); return trader;
    }
    private static void Tick(DeviceOrderSession session, float seconds = 1)
    { Time.time += seconds; GameObject.Invoke(session,"Update"); }
    public static int Main(string[] args)
    {
        Resources.Root = args[0];
        try { MultipleOrders(); SamePriceAndImmediate(); SplitOrders(); DailyCandle(); }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        Console.WriteLine("Terminal checks passed=" + passed); return 0;
    }
    private static void MultipleOrders()
    {
        var (p,s,a,b) = Setup(); var seller = Seller(a,100);
        Check(s.Submit(a,true,900,3,1,1),"first limit order accepted"); var first = s.Latest;
        Check(s.Submit(a,true,800,2,1,1),"second limit accepted while first waits"); var second = s.Latest;
        Check(s.Prepare(b) && s.Submit(b,true,100,4,1,1),"switching stock keeps earlier orders alive");
        Check(s.ActiveOrders.Count == 3 && p.ReservedCash == 4700 && s.AvailableCash == 5300,"independent prices and stocks reserve cash once");
        seller.SellLimit(900,1,0,a); Tick(s);
        Check(first.Filled == 1 && first.PendingVolume == 2 && second.Filled == 0,"older order still tracks partial fills after a new submission");
        Check(s.Cancel(first) && p.GetOwnedVolume(a) == 1 && p.ReservedCash == 2000,"individual cancellation keeps partial fills and other reservations");
        Check(!s.Cancel(first) && p.ReservedCash == 2000,"repeated stale cancellation cannot refund twice");
        seller.SellLimit(800,2,0,a); Tick(s);
        Check(second.Filled == 2 && p.GetOwnedVolume(a) == 3 && p.cash == 7500,"second order fills and debits independently");
        s.Prepare(a); Check(s.Submit(a,false,1300,2,1,1),"sell can be submitted beside a pending buy"); var sell = s.Latest;
        Check(s.Submit(a,false,1400,1,1,1) && s.AvailableShares(a) == 0,"multiple sells reserve separate share quantities");
        Check(!s.Submit(a,false,1500,1,1,1),"already reserved shares cannot be sold twice");
        var buyer = Seller(a,0); buyer.BuyLimit(1300,1,0,a); Tick(s);
        Check(sell.Filled == 1 && s.Cancel(sell) && p.GetHolding(a) == 1 && p.GetOwnedVolume(a) == 2,"partial sell cancellation restores only unfilled shares and their basis");
        var entries = new List<DeviceOrderSession.PendingEntry>(); s.CopyPendingOrders(entries);
        Check(entries.Count == 2,"pending list removes canceled/filled orders and retains distinct live orders");
        MarketDayController.Instance.Ready = false; Tick(s);
        Check(!s.Active && p.ReservedCash == 0 && p.EscrowVolume == 0,"session close cancels every remaining order without discarding fills");
        GameObject.Invoke(s,"OnDestroy");
    }
    private static void SamePriceAndImmediate()
    {
        var (p,s,a,b) = Setup(); var seller = Seller(a,100);
        seller.SellLimit(1000,2,0,a); s.Submit(a,true,1100,5,1,1); var first = s.Latest;
        Check(first.Filled == 2 && first.PendingVolume == 3 && p.cash == 8000 && p.ReservedCash == 3300,"immediate partial fill tracked at actual execution price");
        s.Cancel(); s.Submit(a,true,900,2,1,1); var older = s.Latest; s.Submit(a,true,900,2,1,1); var newer = s.Latest;
        seller.SellLimit(900,3,0,a); Tick(s);
        Check(older.Filled == 2 && newer.Filled == 1 && newer.PendingVolume == 1,"same-price orders keep FIFO identity and separate fill totals");
        var other = new GameObject("Other account").AddComponent<Trader>(); other.cash = 10000;
        var otherSession = new GameObject("Other orders").AddComponent<DeviceOrderSession>(); otherSession.account = other; otherSession.Prepare(a); otherSession.Submit(a,true,800,2,1,1);
        Check(!s.CancelPending(new DeviceOrderSession.PendingEntry(otherSession.Latest)),"cannot cancel another account's request");
        s.Cancel(); Check(other.ReservedCash == 1600 && otherSession.Active,"canceling this account leaves another account unchanged");
        otherSession.Cancel();
        s.Submit(a,true,900,1,1,1); var raced = s.Latest; seller.SellLimit(900,1,0,a);
        Check(!s.Cancel(raced) && raced.Status == "체결 완료" && !s.Active,"fill completed just before cancellation is reported as filled without a duplicate refund");
        p.BuyLimit(500,1,0,b);
        var entries = new List<DeviceOrderSession.PendingEntry>(); s.CopyPendingOrders(entries);
        Check(entries.Count == 1 && entries[0].Request == null && s.CancelPending(entries[0]) && p.ReservedCash == 0,"legacy/automation engine orders also appear and cancel through the same list");
        GameObject.Invoke(s,"OnDestroy"); GameObject.Invoke(otherSession,"OnDestroy");
    }
    private static void SplitOrders()
    {
        var (p,s,a,b) = Setup();
        Check(s.Submit(a,true,500,10,2,3),"split request starts"); var split = s.Latest;
        Check(p.ReservedCash == 2500 && s.ScheduledBuyCost == 2500 && s.AvailableCash == 5000,"future slices counted separately from engine escrow without double subtraction");
        Check(s.Submit(a,true,400,10,1,1) && s.AvailableCash == 1000,"ordinary order accepted alongside split schedule");
        Check(!s.Submit(a,true,500,3,1,1),"new order cannot spend money promised to future split slices");
        Tick(s,4); Check(split.Unsubmitted == 0 && split.PendingVolume == 10 && p.ReservedCash == 9000,"split completes its submissions independently of newer order");
        s.Cancel(); p.GrantShares(a,10,1000);
        s.Submit(a,false,1500,6,3,2); var sell = s.Latest;
        Check(s.AvailableShares(a) == 4 && p.GetHolding(a) == 8,"future sell slices reserve availability but do not change owned shares");
        Check(s.Submit(a,false,1600,4,1,1) && !s.Submit(a,false,1700,1,1,1),"parallel sell excludes both submitted and scheduled shares");
        s.Cancel(sell); Tick(s,20);
        Check(s.AvailableShares(a) == 6 && sell.Unsubmitted == 0 && s.ActiveOrders.Count == 1,"canceling one split stops future slices while another sell stays active");
        s.Cancel(); GameObject.Invoke(s,"OnDestroy");
    }
    private static void DailyCandle()
    {
        var (p,s,a,b) = Setup(); var stats = new GameObject("Statistics").AddComponent<StockStatistics>(); stats.RegisterStock(a);
        Check(stats.TryGetDailyCandle(a,out var initial) && initial.open == 1000 && initial.close == 1000,"daily candle starts at the session opening price");
        var seller = Seller(a,100); var buyer = Seller(a,0);
        foreach (decimal price in new[] { 1400m,800m,1100m,1000m,1200m,900m,1000m,900m })
        { seller.SellLimit(price,1,0,a); buyer.BuyLimit(price,1,0,a); }
        Time.time = 500; GameObject.Invoke(stats,"Update");
        Check(stats.TryGetDailyCandle(a,out var daily) && daily.open == 1000 && daily.high == 1400 && daily.low == 800 && daily.close == 900,"daily OHLC survives short-candle grouping and the old sixty-second timer");
        Check(!stats.TryGetDailyCandle(b,out _) && !stats.TryGetDailyCandle(null,out _),"missing daily data is not replaced with unrelated short candles");
        stats.BeginTradingDay(new Dictionary<Stock,decimal> { [a] = 900 });
        Check(stats.TryGetDailyCandle(a,out var next) && next.open == 900 && next.high == 900 && next.low == 900 && stats.TryGetDailyChange(a,out var pct) && pct == 0,"only market opening resets OHLC and retains the previous-close percentage baseline");
        buyer.BuyLimit(1200,1,0,a); buyer.BuyLimit(1100,1,0,a); seller.SellLimit(1000,2,0,a);
        seller.SellLimit(700,1,0,a); seller.SellLimit(800,1,0,a); buyer.BuyLimit(1000,2,0,a);
        Check(stats.TryGetDailyCandle(a,out var swept) && swept.open == 900 && swept.high == 1200 && swept.low == 700 && swept.close == 800,"daily high/low include every matched price when a single order sweeps several levels");
        GameObject.Invoke(stats,"OnDestroy"); GameObject.Invoke(s,"OnDestroy");
    }
}
