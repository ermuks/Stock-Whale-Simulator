using System;

// 세력 작전의 판단부.
// 조용히 모은 뒤 크게 끌어올려 개미가 올라타게 하고, 올라탄 만큼 물량을 넘긴다.
// 목표 고점에서는 한 번 크게 팔아 휘청이게 한 다음 다시 올리고, 또 휘청이게 하기를 몇 번 되풀이한다.
// 여기서는 "다음에 어떤 주문을 얼마나 낼지"만 정한다. 가격·잔고·호가는 건드리지 않고, 실제 주문은 WhaleBot이 자기 계좌로 낸다.

/// <summary>작전 한 건의 진행 상태. 세력 계좌와 함께 저장된다.</summary>
public sealed class WhaleOperationState
{
    public const int None = 0, Build = 1, Drive = 2, Shake = 3, Repump = 4, Unwind = 5;
    public int stage;
    /// <summary>이번 작전에 쓰기로 한 현금, 지금까지 산 금액과 판 금액.</summary>
    public decimal budget, spent, proceeds;
    /// <summary>작전으로 늘어난 보유 수량의 직전 값과 최대값.</summary>
    public decimal lastPosition, boughtShares;
    /// <summary>작전으로 산 것 말고도, 원래 갖고 있던 지분에서 이만큼까지 더 넘길 수 있다.</summary>
    public decimal extraShares;
    public decimal startPrice, targetPrice, peakPrice;
    /// <summary>이 값 아래로는 팔지 않는다. 세력은 자기 회사의 값어치를 안다. 손해를 보면서 바닥까지 던질 이유가 없다.</summary>
    public decimal floorPrice;
    /// <summary>지금 팔 수 있는 가장 낮은 값. 시작가 기준 바닥선과, 지금까지의 고점에서 28% 내려온 값 가운데 높은 쪽이다.
    /// 크게 띄운 뒤에는 고점 근처에서만 넘긴다. 다섯 배로 띄워 놓고 시작가까지 따라 내려가며 팔지 않는다.</summary>
    public decimal SellFloor => Math.Max(floorPrice, peakPrice * WhaleOperationPlan.PeakShare);
    /// <summary>끌어올릴 목표 상승률. 끌어올리기를 시작하는 순간의 가격에 곱한다.</summary>
    public decimal gain;
    /// <summary>단계별 길이(장중 초)와 지금 단계에서 흐른 시간, 다음 주문까지 남은 시간.</summary>
    public float buildSeconds, driveSeconds, unwindSeconds, stageSeconds, wait;
    /// <summary>휘청이게 할 횟수와 지금까지 한 횟수.</summary>
    public int shakes, shakesDone;
    /// <summary>이번 흔들기에서 팔 수량, 판 수량, 이 가격까지 밀리면 멈춘다.</summary>
    public decimal shakeQuota, shakeSold, shakeFloor;
    /// <summary>다시 올릴 때 쓸 수 있는 돈과 쓴 돈, 되찾으려는 가격.</summary>
    public decimal repumpBudget, repumpSpent, repumpGoal;
    public int startDay, restUntilDay, finished;
    public bool Active => stage != None;
    /// <summary>아직 넘길 수 있는 수량: 작전으로 산 것 + 원래 지분에서 허용한 몫.</summary>
    public decimal Remaining(decimal position) => position + extraShares;
}

public enum WhaleOperationKind
{
    None,
    /// <summary>최우선 매수호가에 조용히 줄을 선다.</summary>
    JoinBid,
    /// <summary>최우선 매도호가를 조금 받아 온다.</summary>
    TakeAsk,
    /// <summary>현재가 위로 치고 올라가며 산다.</summary>
    LiftAsk,
    /// <summary>최우선 매도호가에 물량을 올려 따라붙는 매수에 넘긴다.</summary>
    JoinAsk,
    /// <summary>최우선 매수호가에 판다.</summary>
    HitBid,
    /// <summary>작전이 끝났다. 남은 주문을 거둔다.</summary>
    Finish
}

public readonly struct WhaleOperationOrder
{
    public readonly WhaleOperationKind Kind;
    public readonly decimal Quantity;
    public WhaleOperationOrder(WhaleOperationKind kind, decimal quantity) { Kind = kind; Quantity = quantity; }
}

public static class WhaleOperationPlan
{
    /// <summary>매집에 쓰는 예산 몫. 나머지는 끌어올릴 때 쓴다.</summary>
    public const decimal BuildShare = .35m;
    /// <summary>평균 매수가에서 이만큼 밀리면 호가를 기다리지 않고 판다.</summary>
    public const decimal CutLoss = .96m;
    /// <summary>목표의 이만큼을 올린 뒤부터, 따라붙는 매수가 뜨거울수록 올리는 중에도 물량을 넘긴다.</summary>
    public const decimal SkimFrom = .35m;
    /// <summary>한 번 흔들 때 고점에서 이만큼(6~12%) 밀리면 더 팔지 않는다. 한꺼번에 무너뜨리지 않기 위해서다.</summary>
    public const decimal ShakeDepthMin = .06m, ShakeDepthMax = .12m;
    /// <summary>작전을 시작한 값의 이만큼 아래로는 물량을 내놓지 않는다. 그 밑에서는 남은 물량을 그냥 들고 끝낸다.</summary>
    public const decimal FloorShare = .85m;
    /// <summary>고점의 이만큼 아래로는 팔지 않는다.</summary>
    public const decimal PeakShare = .72m;

    /// <summary>
    /// 작전을 연다. 길이는 장중 초 기준이고 하루 장은 900초다. fast는 검증용으로 매집을 짧게 줄인다.
    /// 목표 상승률은 +40%에서 +300%까지이고 작은 쪽이 더 자주 나온다(중간값 약 +105%).
    /// </summary>
    public static void Begin(WhaleOperationState state, decimal price, decimal budget, decimal extraShares, int day, Func<double> roll, bool fast = false)
    {
        state.stage = WhaleOperationState.Build;
        state.budget = budget; state.spent = 0m; state.proceeds = 0m; state.lastPosition = 0m; state.boughtShares = 0m; state.extraShares = Math.Max(0m, decimal.Floor(extraShares));
        state.startPrice = price; state.peakPrice = price; state.targetPrice = 0m; state.floorPrice = price * FloorShare;
        double tail = roll();
        state.gain = .40m + (decimal)(tail * tail) * 2.60m;
        state.buildSeconds = fast ? 25f + (float)roll() * 10f : 400f + (float)roll() * 1100f;
        state.driveSeconds = 260f + (float)roll() * 540f;
        state.unwindSeconds = 300f + (float)roll() * 900f;
        state.shakes = 2 + (int)(roll() * 3); state.shakesDone = 0;
        state.shakeQuota = state.shakeSold = state.shakeFloor = 0m; state.repumpBudget = state.repumpSpent = state.repumpGoal = 0m;
        state.stageSeconds = 0f; state.wait = 0f; state.startDay = day;
    }

    /// <summary>
    /// 장중에만 부른다. position은 작전을 시작한 뒤 늘어난 보유 수량이다(실제 체결로만 변하고, 원래 지분까지 넘기면 음수가 된다).
    /// heat는 따라붙는 매수의 열기(0~1)로, 세력이 체결을 보고 잰 값이다.
    /// </summary>
    public static WhaleOperationOrder Step(WhaleOperationState state, float seconds, decimal price, decimal position, double heat, int day, Func<double> roll)
    {
        if (!state.Active || price <= 0m) { return default; }
        heat = Math.Max(0d, Math.Min(1d, heat));
        state.stageSeconds += seconds; state.wait -= seconds;
        // 체결된 만큼만 쓴 돈·번 돈으로 친다. 걸어 두기만 한 주문은 세지 않는다.
        decimal moved = position - state.lastPosition;
        if (moved > 0m) { state.spent += moved * price; if (state.stage == WhaleOperationState.Repump) { state.repumpSpent += moved * price; } }
        else if (moved < 0m) { state.proceeds += -moved * price; if (state.stage == WhaleOperationState.Shake) { state.shakeSold += -moved; } }
        state.boughtShares = Math.Max(state.boughtShares, position);
        state.lastPosition = position;
        if (price > state.peakPrice) { state.peakPrice = price; }
        decimal remaining = state.Remaining(position);

        switch (state.stage)
        {
            case WhaleOperationState.Build: return StepBuild(state, price, roll);
            case WhaleOperationState.Drive: return StepDrive(state, price, remaining, heat, roll);
            case WhaleOperationState.Shake: return StepShake(state, price, remaining, roll);
            case WhaleOperationState.Repump: return StepRepump(state, price, remaining, heat, roll);
            default: return StepUnwind(state, price, position, remaining, day, roll);
        }
    }

    private static WhaleOperationOrder StepBuild(WhaleOperationState state, decimal price, Func<double> roll)
    {
        // 끌어올리기는 정해 둔 시각에 시작한다. 매집할 돈을 일찍 다 썼으면 그때까지 기다린다(문틈 쪽지에 적힌 시각이 이 시각이다).
        if (state.stageSeconds >= state.buildSeconds)
        {
            state.stage = WhaleOperationState.Drive; state.stageSeconds = 0f; state.wait = 0f;
            state.targetPrice = price * (1m + state.gain);
            return default;
        }
        if (state.wait > 0f || state.spent >= state.budget * BuildShare) { return default; }
        state.wait = 2.5f + (float)roll() * 3.5f;
        decimal left = state.budget * BuildShare - state.spent;
        decimal actions = (decimal)Math.Max(1f, (state.buildSeconds - state.stageSeconds) / 4.25f);
        decimal quantity = decimal.Floor(left / actions / price * (decimal)(.6 + roll() * .8));
        return quantity < 1m ? default : new WhaleOperationOrder(roll() < .3 ? WhaleOperationKind.TakeAsk : WhaleOperationKind.JoinBid, quantity);
    }

    /// <summary>올리는 중에도 개미가 뜨겁게 따라붙으면 그만큼 물량을 넘긴다. 뜨거울수록 자주, 많이.</summary>
    private static bool Skim(WhaleOperationState state, decimal price, decimal remaining, double heat, Func<double> roll, out WhaleOperationOrder order)
    {
        order = default;
        decimal climbed = state.targetPrice > state.startPrice ? (price - state.startPrice) / (state.targetPrice - state.startPrice) : 0m;
        if (remaining < 1m || heat < .3 || climbed < SkimFrom || roll() > heat * .6) { return false; }
        decimal quantity = decimal.Floor(remaining * (decimal)(.01 + heat * heat * .05 * (.6 + roll() * .8)));
        if (quantity < 1m) { return false; }
        order = new WhaleOperationOrder(WhaleOperationKind.JoinAsk, Math.Min(quantity, decimal.Floor(remaining)));
        return true;
    }

    private static WhaleOperationOrder StepDrive(WhaleOperationState state, decimal price, decimal remaining, double heat, Func<double> roll)
    {
        bool reached = price >= state.targetPrice, spentOut = state.spent - state.proceeds * .5m >= state.budget * .98m, late = state.stageSeconds >= state.driveSeconds;
        if (reached || spentOut || late)
        {
            // 목표에 닿았으면 고점에서 흔든다. 닿지 못했더라도 충분히 올렸으면 흔들고, 거의 못 올렸으면 그냥 정리한다.
            bool lifted = price >= state.startPrice * 1.15m;
            if (lifted && remaining >= 1m) { BeginShake(state, price, remaining, heat, roll); }
            else { state.stage = WhaleOperationState.Unwind; state.stageSeconds = 0f; state.wait = 0f; }
            return default;
        }
        if (state.wait > 0f) { return default; }
        state.wait = .8f + (float)roll() * 1.2f;
        if (Skim(state, price, remaining, heat, roll, out var skim)) { return skim; }
        // 판 돈의 절반은 다시 올리는 데 쓴다.
        decimal left = state.budget + state.proceeds * .5m - state.spent;
        decimal actions = (decimal)Math.Max(1f, (state.driveSeconds - state.stageSeconds) / 1.4f);
        decimal quantity = decimal.Floor(left / actions / price * (decimal)(.6 + roll() * 1.6));
        return quantity < 1m ? default : new WhaleOperationOrder(WhaleOperationKind.LiftAsk, quantity);
    }

    private static void BeginShake(WhaleOperationState state, decimal price, decimal remaining, double heat, Func<double> roll)
    {
        state.stage = WhaleOperationState.Shake; state.stageSeconds = 0f; state.wait = 0f; state.shakeSold = 0m;
        // 회차가 거듭될수록, 그리고 뜨거울수록 더 많이 넘긴다: 남은 물량의 15% → 25% → 35% …
        decimal share = Math.Min(.6m, (.15m + .10m * state.shakesDone) * (decimal)(.7 + heat * .6));
        state.shakeQuota = Math.Max(1m, decimal.Floor(remaining * share));
        decimal depth = ShakeDepthMin + (decimal)roll() * (ShakeDepthMax - ShakeDepthMin);
        state.shakeFloor = Math.Max(price, state.peakPrice) * (1m - depth);
        state.repumpGoal = Math.Max(price, state.peakPrice);
    }

    private static WhaleOperationOrder StepShake(WhaleOperationState state, decimal price, decimal remaining, Func<double> roll)
    {
        // 정한 만큼 팔았거나, 정한 깊이까지 밀렸거나, 너무 오래 걸리면 멈춘다. 한 번에 무너뜨리지 않는다.
        if (state.shakeSold >= state.shakeQuota || price <= state.shakeFloor || price <= state.SellFloor || remaining < 1m || state.stageSeconds >= 45f)
        {
            state.shakesDone++;
            if (remaining < 1m || state.shakesDone >= state.shakes) { state.stage = WhaleOperationState.Unwind; state.stageSeconds = 0f; state.wait = 0f; return default; }
            state.stage = WhaleOperationState.Repump; state.stageSeconds = 0f; state.wait = 1.5f + (float)roll() * 2f;
            state.repumpSpent = 0m; state.repumpBudget = state.proceeds * .35m;
            return default;
        }
        if (state.wait > 0f) { return default; }
        state.wait = .5f + (float)roll() * .6f;
        decimal quantity = decimal.Ceiling((state.shakeQuota - state.shakeSold) * (decimal)(.18 + roll() * .22));
        quantity = Math.Min(decimal.Floor(remaining), Math.Max(1m, quantity));
        return new WhaleOperationOrder(WhaleOperationKind.HitBid, quantity);
    }

    private static WhaleOperationOrder StepRepump(WhaleOperationState state, decimal price, decimal remaining, double heat, Func<double> roll)
    {
        if (price >= state.repumpGoal * .985m)
        {
            // 고점을 되찾았다. 조금 더 높은 고점을 새 목표로 잡고 다시 올리다가 또 흔든다.
            state.stage = WhaleOperationState.Drive; state.stageSeconds = 0f; state.wait = 0f;
            state.targetPrice = Math.Max(price, state.peakPrice) * (1m + .05m + (decimal)roll() * .15m);
            state.driveSeconds = 60f + (float)roll() * 120f;
            return default;
        }
        // 쓸 돈을 다 썼거나 한참 지나도 못 되찾으면, 따라붙는 사람이 없다는 뜻이다. 정리한다.
        if (state.repumpSpent >= state.repumpBudget || state.stageSeconds >= 150f)
        { state.stage = WhaleOperationState.Unwind; state.stageSeconds = 0f; state.wait = 0f; return default; }
        if (state.wait > 0f) { return default; }
        state.wait = .8f + (float)roll() * 1.2f;
        if (Skim(state, price, remaining, heat, roll, out var skim) && roll() < .35) { return skim; }
        decimal left = state.repumpBudget - state.repumpSpent;
        decimal actions = (decimal)Math.Max(1f, (150f - state.stageSeconds) / 1.4f);
        decimal quantity = decimal.Floor(left / actions / price * (decimal)(1.2 + roll() * 2.0));
        return quantity < 1m ? default : new WhaleOperationOrder(WhaleOperationKind.LiftAsk, quantity);
    }

    private static WhaleOperationOrder StepUnwind(WhaleOperationState state, decimal price, decimal position, decimal remaining, int day, Func<double> roll)
    {
        // 마지막 정리: 작전으로 산 물량 가운데 남은 것만 판다. 원래 지분은 더 건드리지 않는다.
        // 값이 바닥선 아래로 내려가 있으면 팔지 않고 기다린다. 끝까지 못 팔면 남은 물량은 그대로 들고 끝낸다(어차피 자기 회사 주식이다).
        if (position < 1m || state.stageSeconds >= state.unwindSeconds * 2f)
        {
            state.stage = WhaleOperationState.None; state.finished++;
            state.restUntilDay = day + 1 + (int)(roll() * 3);
            return new WhaleOperationOrder(WhaleOperationKind.Finish, 0m);
        }
        if (state.wait > 0f) { return default; }
        state.wait = 2f + (float)roll() * 3f;
        if (price <= state.SellFloor) { return default; }
        bool late = state.stageSeconds >= state.unwindSeconds;
        decimal average = state.boughtShares > 0m ? state.spent / state.boughtShares : price;
        bool losing = price < average * CutLoss;
        decimal steps = (decimal)Math.Max(1f, (state.unwindSeconds - state.stageSeconds) / 3.5f);
        decimal sell = decimal.Ceiling(position / steps * (decimal)(.6 + roll() * .8));
        if (losing || late) { sell *= 2m; }
        sell = Math.Min(decimal.Floor(position), Math.Max(1m, sell));
        return new WhaleOperationOrder(losing || late || roll() < .4 ? WhaleOperationKind.HitBid : WhaleOperationKind.JoinAsk, sell);
    }
}
