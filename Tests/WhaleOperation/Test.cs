using System;
using System.Collections.Generic;
public static class T
{
    public static int Main()
    {
        int fail = 0; var rng = new Random(7);
        Func<double> roll = () => rng.NextDouble();
        for (int run = 0; run < 200; run++)
        {
            var s = new WhaleOperationState();
            decimal price = 1000m + run * 37m, budget = 500_000_000m, position = 0m, cash = budget, proceeds = 0m;
            WhaleOperationPlan.Begin(s, price, budget, 3, roll);
            float total = s.buildSeconds + s.driveSeconds + s.unwindSeconds;
            if (total < 850f || total > 3200f || s.gain < .08m || s.gain > .25m) { fail++; Console.WriteLine("bad plan"); }
            bool sawDrive = false, sawUnwind = false; int steps = 0; decimal peak = price; int buyInUnwind = 0, sellBefore = 0; decimal oversold = 0m;
            bool thin = run % 10 == 9; // 매도 물량이 전혀 없는 장
            while (s.Active && steps++ < 400000)
            {
                var o = WhaleOperationPlan.Step(s, .05f, price, position, 3 + steps / 18000, roll);
                if (s.stage == WhaleOperationState.Drive) { sawDrive = true; }
                if (s.stage == WhaleOperationState.Unwind) { sawUnwind = true; }
                bool buy = o.Kind == WhaleOperationKind.JoinBid || o.Kind == WhaleOperationKind.TakeAsk || o.Kind == WhaleOperationKind.LiftAsk;
                bool sell = o.Kind == WhaleOperationKind.JoinAsk || o.Kind == WhaleOperationKind.HitBid;
                if (buy && s.stage == WhaleOperationState.Unwind) { buyInUnwind++; }
                if (sell && !sawUnwind) { sellBefore++; }
                if (o.Quantity < 0m || (buy || sell) && o.Quantity < 1m) { fail++; Console.WriteLine("bad quantity"); }
                if (buy && !thin)
                {
                    decimal fill = o.Kind == WhaleOperationKind.JoinBid ? decimal.Floor(o.Quantity * .6m) : o.Quantity;
                    fill = Math.Min(fill, decimal.Floor(cash / price));
                    position += fill; cash -= fill * price;
                    // 장난감 시장: 산 금액만큼 가격이 밀려 올라간다.
                    price += price * (o.Kind == WhaleOperationKind.LiftAsk ? .0012m : .0002m);
                }
                if (sell)
                {
                    if (o.Quantity > position) { oversold += o.Quantity - position; }
                    decimal fill = Math.Min(position, o.Kind == WhaleOperationKind.HitBid ? o.Quantity : decimal.Floor(o.Quantity * .7m));
                    position -= fill; proceeds += fill * price; price -= price * (o.Kind == WhaleOperationKind.HitBid ? .0010m : .0002m);
                }
                peak = Math.Max(peak, price);
            }
            if (s.Active) { fail++; Console.WriteLine("never ends " + run); }
            if (!sawDrive || !sawUnwind) { fail++; Console.WriteLine("skipped stage " + run); }
            if (buyInUnwind > 0 || sellBefore > 0 || oversold > 0m) { fail++; Console.WriteLine("wrong side " + run); }
            if (s.spent > budget * 1.02m) { fail++; Console.WriteLine("overspent " + run + " " + s.spent); }
            if (s.restUntilDay < 4) { fail++; Console.WriteLine("no rest"); }
            if (!thin && position > s.boughtShares * .02m + 1m) { fail++; Console.WriteLine("left " + position + " of " + s.boughtShares); }
            if (thin && (s.spent != 0m || position != 0m)) { fail++; Console.WriteLine("thin bought"); }
            if (run < 5 || thin && run < 30) { Console.WriteLine($"run {run}: {steps * .05f:0}s start {s.startPrice:N0} peak {peak:N0} (+{(peak / s.startPrice - 1m) * 100m:0.0}%) end {price:N0} spent {s.spent:N0} back {proceeds:N0} left {position:N0}{(thin ? " [no sellers]" : "")}"); }
        }
        Console.WriteLine(fail == 0 ? "PASS 200 runs" : "FAIL " + fail);
        return fail;
    }
}
