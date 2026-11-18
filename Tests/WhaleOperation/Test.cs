using System;
using System.Collections.Generic;
using System.Linq;

// 세력 작전 판단부를 장난감 시장에서 돌린다.
// 장난감 시장: 세력이 사면 가격이 조금 오르고 팔면 조금 내린다. 가격이 시작가보다 많이 오를수록 개미가 뜨겁게 따라붙어(열기)
// 스스로 가격을 더 올리고, 세력이 내놓은 매도 물량을 받아 간다.
public static class T
{
    public static int Main()
    {
        int fail = 0; var rng = new Random(7);
        Func<double> roll = () => rng.NextDouble();
        var peaks = new List<decimal>(); var shakeDrops = new List<decimal>(); var soldShares = new List<decimal>();
        for (int run = 0; run < 200; run++)
        {
            var s = new WhaleOperationState();
            decimal price = 1000m + run * 37m, budget = 500_000_000m, position = 0m, cash = budget, extra = 200_000m;
            bool thin = run % 10 == 9;   // 매도 물량이 전혀 없는 장
            bool cold = run % 10 == 8;   // 아무도 따라붙지 않는 장
            WhaleOperationPlan.Begin(s, price, budget, extra, 3, roll);
            if (s.gain < .40m || s.gain > 3.0m || s.shakes < 2 || s.shakes > 4) { fail++; Console.WriteLine("bad plan"); }
            int steps = 0, stage = s.stage, wrong = 0; decimal peak = price, minPosition = 0m, shakeTop = 0m, lowestSale = decimal.MaxValue, low = price; var path = new List<int> { stage };
            while (s.Active && steps++ < 600000)
            {
                double heat = cold || thin ? 0d : Math.Max(0d, Math.Min(1d, (double)((price / s.startPrice - 1.15m) / .6m)));
                var o = WhaleOperationPlan.Step(s, .05f, price, position, heat, 3 + steps / 18000, roll);
                if (s.stage != stage)
                {
                    if (s.stage == WhaleOperationState.Shake) { shakeTop = Math.Max(price, s.peakPrice); }
                    if (stage == WhaleOperationState.Shake && shakeTop > 0m) { shakeDrops.Add(1m - price / shakeTop); }
                    stage = s.stage; path.Add(stage);
                }
                bool buy = o.Kind == WhaleOperationKind.JoinBid || o.Kind == WhaleOperationKind.TakeAsk || o.Kind == WhaleOperationKind.LiftAsk;
                bool sell = o.Kind == WhaleOperationKind.JoinAsk || o.Kind == WhaleOperationKind.HitBid;
                if ((buy || sell) && o.Quantity < 1m) { fail++; Console.WriteLine("bad quantity"); }
                // 단계에 맞지 않는 주문: 흔들 때 사거나, 매집·다시 올리기에서 호가를 치며 팔면 안 된다.
                if (buy && (stage == WhaleOperationState.Shake || stage == WhaleOperationState.Unwind)) { wrong++; }
                if (o.Kind == WhaleOperationKind.HitBid && stage != WhaleOperationState.Shake && stage != WhaleOperationState.Unwind) { wrong++; }
                if (sell && stage == WhaleOperationState.Build) { wrong++; }
                if (buy && !thin)
                {
                    decimal fill = o.Kind == WhaleOperationKind.JoinBid ? decimal.Floor(o.Quantity * .6m) : o.Quantity;
                    fill = Math.Min(fill, decimal.Floor(cash / price));
                    position += fill; cash -= fill * price;
                    price += price * (o.Kind == WhaleOperationKind.LiftAsk ? .0016m : .0002m) * (decimal)(1 + heat * 2);
                }
                if (sell)
                {
                    decimal can = position + extra; lowestSale = Math.Min(lowestSale, price);
                    // 고점의 72% 아래로는 팔지 않는다(주문 하나만큼의 여유).
                    if (price < s.peakPrice * WhaleOperationPlan.PeakShare * .985m) { fail++; Console.WriteLine("sold far below the peak in run " + run + ": " + price + " vs peak " + s.peakPrice); }
                    if (o.Quantity > can + .5m) { fail++; Console.WriteLine("sells more than allowed"); }
                    decimal fill = Math.Min(can, o.Kind == WhaleOperationKind.HitBid ? o.Quantity : decimal.Floor(o.Quantity * (decimal)(.3 + heat * .7)));
                    position -= fill; cash += fill * price;
                    price -= price * (o.Kind == WhaleOperationKind.HitBid ? .012m : .0006m);
                }
                // 뜨거워진 개미가 스스로 조금씩 더 올린다.
                if (heat > 0d && steps % 20 == 0) { price += price * (decimal)(heat * .0006); }
                peak = Math.Max(peak, price); minPosition = Math.Min(minPosition, position); low = Math.Min(low, price);
            }
            if (s.Active) { fail++; Console.WriteLine("never ends " + run); }
            // 세력은 시작가의 85% 아래로는 팔지 않는다. 그래서 세력 혼자서는 값을 그 밑으로 끌어내리지 못한다(주문 하나만큼의 여유는 둔다).
            if (lowestSale < s.startPrice * WhaleOperationPlan.FloorShare * .999m) { fail++; Console.WriteLine("sold below the floor in run " + run + ": " + lowestSale + " < " + s.startPrice * WhaleOperationPlan.FloorShare); }
            if (low < s.startPrice * (WhaleOperationPlan.FloorShare - .02m)) { fail++; Console.WriteLine("price pushed far below the floor in run " + run + ": " + low); }
            if (wrong > 0) { fail++; Console.WriteLine("order on the wrong side of its stage: " + wrong + " in run " + run); }
            if (minPosition < -extra - .5m) { fail++; Console.WriteLine("sold more of the original stake than allowed " + minPosition); }
            // 쓸 수 있는 돈은 처음 예산과 그동안 판 돈까지다.
            if (s.spent > budget + s.proceeds + budget * .03m || cash < -1m) { fail++; Console.WriteLine("overspent " + run + " " + s.spent); }
            if (s.shakesDone > s.shakes) { fail++; Console.WriteLine("too many shakes"); }
            if (s.restUntilDay < 4) { fail++; Console.WriteLine("no rest"); }
            // 단계 순서: 매집 → 끌어올리기 → (흔들기 → 다시 올리기 → 끌어올리기)* → 정리 → 끝.
            for (int i = 1; i < path.Count; i++)
            {
                int a = path[i - 1], b = path[i];
                bool ok = a == 1 && b == 2 || a == 2 && (b == 3 || b == 5) || a == 3 && (b == 4 || b == 5) || a == 4 && (b == 2 || b == 5) || a == 5 && b == 0;
                if (!ok) { fail++; Console.WriteLine("bad stage order " + string.Join(">", path)); break; }
            }
            if (thin && (s.spent != 0m || position != 0m)) { fail++; Console.WriteLine("thin market traded"); }
            if (!thin && !cold) { peaks.Add(peak / s.startPrice - 1m); soldShares.Add(Math.Max(0m, s.boughtShares - position)); }
            if (run < 6 || (thin || cold) && run < 20)
            {
                Console.WriteLine($"run {run}: {steps * .05f:0}s target +{s.gain * 100m:0}% peak +{(peak / s.startPrice - 1m) * 100m:0}% end {(price / s.startPrice - 1m) * 100m:+0;-0}% shakes {s.shakesDone}/{s.shakes} bought {s.spent:N0} sold {s.proceeds:N0} path {string.Join(">", path)}{(thin ? " [no sellers]" : cold ? " [nobody follows]" : "")}");
            }
        }
        peaks.Sort();
        Console.WriteLine($"peak gain: median +{peaks[peaks.Count / 2] * 100m:0}%, lowest +{peaks[0] * 100m:0}%, highest +{peaks[peaks.Count - 1] * 100m:0}%");
        Console.WriteLine($"shake depth: {shakeDrops.Count} shakes, average {shakeDrops.Average() * 100m:0.0}%, deepest {shakeDrops.Max() * 100m:0.0}%");
        if (peaks[peaks.Count / 2] < .40m) { fail++; Console.WriteLine("pumps are too small"); }
        // 한 번 흔들 때 정한 깊이(최대 12%)에서 주문 하나만큼만 더 밀릴 수 있다.
        if (shakeDrops.Max() > .16m) { fail++; Console.WriteLine("a shake dropped the price too far at once"); }
        if (shakeDrops.Count < 200) { fail++; Console.WriteLine("too few shakes"); }
        Console.WriteLine(fail == 0 ? "PASS 200 runs" : "FAIL " + fail);
        return fail;
    }
}
