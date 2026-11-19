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
        var peaks = new List<decimal>(); var shakeDrops = new List<decimal>(); var soldShares = new List<decimal>(); var dumps = new List<decimal>(); var scoops = new List<decimal>(); var waves = new List<int>(); var planned = new List<int>(); var profits = new List<decimal>(); var papers = new List<decimal>();
        for (int run = 0; run < 200; run++)
        {
            var s = new WhaleOperationState();
            decimal price = 1000m + run * 37m, budget = 500_000_000m, position = 0m, cash = budget, extra = 200_000m;
            bool thin = run % 10 == 9;   // 매도 물량이 전혀 없는 장
            bool cold = run % 10 == 8;   // 아무도 따라붙지 않는 장
            WhaleOperationPlan.Begin(s, price, budget, extra, 3, roll);
            if (s.gain < .15m || s.gain > .40m || s.shakes != 0 || s.waves < 2 || s.waves > 14) { fail++; Console.WriteLine("bad plan"); }
            decimal dumpTop = 0m, dumpLow = decimal.MaxValue, follow = 0m; int dumpWave = -1;
            int steps = 0, stage = s.stage, wrong = 0; decimal peak = price, minPosition = 0m, shakeTop = 0m, lowestSale = decimal.MaxValue, low = price; var path = new List<int> { stage };
            while (s.Active && steps++ < 600000)
            {
                double heat = cold || thin ? 0d : Math.Max(0d, Math.Min(1d, (double)((price / s.startPrice - 1.15m) / .6m)));
                var o = WhaleOperationPlan.Step(s, .05f, price, position, heat, 3 + steps / 18000, roll);
                if (s.stage != stage)
                {
                    if (s.stage == WhaleOperationState.Shake) { shakeTop = Math.Max(price, s.wavePeak); }
                    if (stage == WhaleOperationState.Shake && shakeTop > 0m) { shakeDrops.Add(1m - price / shakeTop); }
                    stage = s.stage; path.Add(stage);
                }
                bool buy = o.Kind == WhaleOperationKind.JoinBid || o.Kind == WhaleOperationKind.TakeAsk || o.Kind == WhaleOperationKind.LiftAsk || o.Kind == WhaleOperationKind.Ignite;
                bool sell = o.Kind == WhaleOperationKind.JoinAsk || o.Kind == WhaleOperationKind.HitBid || o.Kind == WhaleOperationKind.Nip;
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
                    // 불붙이기는 정한 높이(sparkReach)까지만 쓸어 올린다. 그 뒤 따라붙는 사람이 있으면(차가운 장이 아니면) 몇 초에 걸쳐 그만큼 더 오른다.
                    if (o.Kind == WhaleOperationKind.Ignite) { price += price * s.sparkReach; if (!cold) { follow += price * s.sparkReach * .8m; } }
                    else { price += price * (o.Kind == WhaleOperationKind.LiftAsk ? .0016m : .0002m) * (decimal)(1 + heat * 2); }
                }
                if (sell)
                {
                    decimal can = position + extra; lowestSale = Math.Min(lowestSale, price);
                    // 고점의 72% 아래로는 팔지 않는다(주문 하나만큼의 여유).
                    if (s.Dumping) { if (dumpWave != s.wave) { if (dumpTop > 0m) { dumps.Add(1m - dumpLow / dumpTop); } dumpWave = s.wave; dumpTop = price; dumpLow = price; } dumpLow = Math.Min(dumpLow, price); }
                    else if (price < s.wavePeak * WhaleOperationPlan.PeakShare * .985m) { fail++; Console.WriteLine("sold far below the peak in run " + run + ": " + price + " vs peak " + s.wavePeak); }
                    if (o.Quantity > can + .5m) { fail++; Console.WriteLine("sells more than allowed"); }
                    // 털기는 매수 호가 두세 칸만 받는다: 한도의 일부만 팔리고 값이 0.3% 밀린다.
                    decimal fill = Math.Min(can, o.Kind == WhaleOperationKind.HitBid ? o.Quantity : o.Kind == WhaleOperationKind.Nip ? decimal.Floor(o.Quantity * .15m) : decimal.Floor(o.Quantity * (decimal)(.3 + heat * .7)));
                    position -= fill; cash += fill * price;
                    price -= price * (o.Kind == WhaleOperationKind.HitBid ? .012m : o.Kind == WhaleOperationKind.Nip ? .003m : .0006m);
                }
                // 나쁜 소문: 세력이 넘기기를 끝내고 소문을 내면 개미가 스스로 던진다. 세력이 떨어뜨리려는 깊이(dumpFloor)까지만 내려간다(게임에서는 믿는 값이 거기서 멎는다).
                if ((s.Dumping && !s.handing || s.stage == WhaleOperationState.Scoop) && s.dumpFloor > 0m && price > s.dumpFloor && !thin)
                { price -= price * .0015m; if (dumpWave == s.wave) { dumpLow = Math.Min(dumpLow, price); } }
                if (follow > 0m && s.stage == WhaleOperationState.Drive) { decimal step = Math.Min(follow, price * .0004m); price += step; follow -= step; }
                // 뜨거워진 개미가 스스로 조금씩 더 올린다.
                if (heat > 0d && steps % 20 == 0) { price += price * (decimal)(heat * .0006); }
                peak = Math.Max(peak, price); minPosition = Math.Min(minPosition, position); low = Math.Min(low, price);
            }
            if (dumpTop > 0m) { dumps.Add(1m - dumpLow / dumpTop); }
            if (s.scoopSpent > 0m) { scoops.Add(s.scoopSpent / Math.Max(1m, s.proceeds)); if (s.scoopSpent > s.scoopBudget * 1.1m + price * 3000m) { fail++; Console.WriteLine("scooped more than planned in run " + run); } }
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
                bool ok = a == 1 && b == 2 || a == 2 && (b == 3 || b == 5) || a == 3 && (b == 4 || b == 5) || a == 4 && (b == 2 || b == 5) || a == 5 && (b == 0 || b == 6) || a == 6 && b == 2;
                if (!ok) { fail++; Console.WriteLine("bad stage order " + string.Join(">", path)); break; }
            }
            if (thin && (s.spent != 0m || position != 0m)) { fail++; Console.WriteLine("thin market traded"); }
            if (!thin && !cold) { waves.Add(s.wave + 1); planned.Add(s.waves); }
            if (!thin && !cold) { profits.Add(s.Profit / s.budget); papers.Add(s.Paper(price) / s.budget); }
            if (!thin && !cold) { peaks.Add(peak / s.startPrice - 1m); soldShares.Add(Math.Max(0m, s.boughtShares - position)); }
            if (run < 6 || (thin || cold) && run < 20)
            {
                Console.WriteLine($"run {run}: {steps * .05f:0}s target +{s.gain * 100m:0}% peak +{(peak / s.startPrice - 1m) * 100m:0}% end {(price / s.startPrice - 1m) * 100m:+0;-0}% shakes {s.shakesDone}/{s.shakes} bought {s.spent:N0} sold {s.proceeds:N0} path {string.Join(">", path)}{(thin ? " [no sellers]" : cold ? " [nobody follows]" : "")}");
            }
        }
        peaks.Sort();
        Console.WriteLine($"whale profit over budget (model market): valued at the start price average {profits.Average() * 100m:0.0}%, at the end price {papers.Average() * 100m:0.0}%, operations ending with profit {profits.Count(x => x > 0m)} of {profits.Count}");
        Console.WriteLine($"peak gain: median +{peaks[peaks.Count / 2] * 100m:0}%, lowest +{peaks[0] * 100m:0}%, highest +{peaks[peaks.Count - 1] * 100m:0}%");
        Console.WriteLine($"shakes: {shakeDrops.Count} (the plan now dumps straight from the top)");
        if (peaks[peaks.Count / 2] < .40m) { fail++; Console.WriteLine("pumps are too small"); }
        // 한 번 흔들 때 정한 깊이(최대 12%)에서 주문 하나만큼만 더 밀릴 수 있다.
        // 고점에 닿으면 흔들지 않고 바로 던진다.
        if (shakeDrops.Count > 0) { fail++; Console.WriteLine("the plan still shakes at the top"); }
        Console.WriteLine($"dumps: {dumps.Count} of 200 operations ended in a dump, average fall {(dumps.Count == 0 ? 0m : dumps.Average()) * 100m:0.0}%, deepest {(dumps.Count == 0 ? 0m : dumps.Max()) * 100m:0.0}%");
        Console.WriteLine($"scoops: {scoops.Count} dumps were bought back near the low, spending on average {(scoops.Count == 0 ? 0m : scoops.Average()) * 100m:0.0}% of the money sold");
        Console.WriteLine($"waves: planned average {planned.Average():0.00} (most {planned.Max()}), carried out average {waves.Average():0.00} (most {waves.Max()})");
        if (planned.Average() < 2.6 || planned.Average() > 4.2) { fail++; Console.WriteLine("planned waves are not about three on average"); }
        if (waves.Average() < 2.0) { fail++; Console.WriteLine("too few waves are carried out"); }
        if (dumps.Count < 40) { fail++; Console.WriteLine("too few dumps"); }
        if (scoops.Count < 20) { fail++; Console.WriteLine("too few scoops"); }
        Console.WriteLine(fail == 0 ? "PASS 200 runs" : "FAIL " + fail);
        return fail;
    }
}
