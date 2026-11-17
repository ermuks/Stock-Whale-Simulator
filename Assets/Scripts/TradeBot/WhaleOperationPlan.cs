using System;

// 세력 작전의 판단부. 매집 → 끌어올리기 → 털기를 순서대로 밟는다.
// 여기서는 "다음에 어떤 주문을 얼마나 낼지"만 정한다. 가격·잔고·호가는 건드리지 않고, 실제 주문은 WhaleBot이 자기 계좌로 낸다.

/// <summary>작전 한 건의 진행 상태. 세력 계좌와 함께 저장된다.</summary>
public sealed class WhaleOperationState
{
    public const int None = 0, Build = 1, Drive = 2, Unwind = 3;
    public int stage;
    /// <summary>이번 작전에 쓰기로 한 현금과 지금까지 산 금액.</summary>
    public decimal budget, spent;
    /// <summary>작전으로 늘어난 보유 수량의 직전 값과 최대값.</summary>
    public decimal lastPosition, boughtShares;
    public decimal startPrice, targetPrice, peakPrice;
    /// <summary>끌어올릴 목표 상승률. 끌어올리기를 시작하는 순간의 가격에 곱한다.</summary>
    public decimal gain;
    /// <summary>단계별 길이(장중 초)와 지금 단계에서 흐른 시간, 다음 주문까지 남은 시간.</summary>
    public float buildSeconds, driveSeconds, unwindSeconds, stageSeconds, wait;
    public int startDay, restUntilDay, finished;
    public bool Active => stage != None;
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
    public const decimal BuildShare = .45m;
    /// <summary>평균 매수가에서 이만큼 밀리면 호가를 기다리지 않고 판다.</summary>
    public const decimal CutLoss = .96m;

    /// <summary>작전을 연다. 길이는 장중 초 기준이고 하루 장은 900초다. 합치면 하루에서 사흘 남짓이 된다.</summary>
    public static void Begin(WhaleOperationState state, decimal price, decimal budget, int day, Func<double> roll)
    {
        state.stage = WhaleOperationState.Build;
        state.budget = budget; state.spent = 0m; state.lastPosition = 0m; state.boughtShares = 0m;
        state.startPrice = price; state.peakPrice = price; state.targetPrice = 0m;
        state.gain = .08m + (decimal)roll() * .17m;
        state.buildSeconds = 400f + (float)roll() * 1100f;
        state.driveSeconds = 150f + (float)roll() * 350f;
        state.unwindSeconds = 300f + (float)roll() * 900f;
        state.stageSeconds = 0f; state.wait = 0f; state.startDay = day;
    }

    /// <summary>
    /// 장중에만 부른다. position은 작전을 시작한 뒤 늘어난 보유 수량이다(실제 체결로만 변한다).
    /// </summary>
    public static WhaleOperationOrder Step(WhaleOperationState state, float seconds, decimal price, decimal position, int day, Func<double> roll)
    {
        if (!state.Active || price <= 0m) { return default; }
        state.stageSeconds += seconds; state.wait -= seconds;
        if (state.stage != WhaleOperationState.Unwind)
        {
            // 산 만큼만 쓴 돈으로 친다. 체결되지 않은 주문은 세지 않는다.
            if (position > state.lastPosition) { state.spent += (position - state.lastPosition) * price; }
            state.boughtShares = Math.Max(state.boughtShares, position);
        }
        state.lastPosition = position;
        if (price > state.peakPrice) { state.peakPrice = price; }

        if (state.stage == WhaleOperationState.Build)
        {
            if (state.spent >= state.budget * BuildShare || state.stageSeconds >= state.buildSeconds)
            {
                state.stage = WhaleOperationState.Drive; state.stageSeconds = 0f; state.wait = 0f;
                state.targetPrice = price * (1m + state.gain);
                return default;
            }
            if (state.wait > 0f) { return default; }
            state.wait = 2.5f + (float)roll() * 3.5f;
            decimal left = state.budget * BuildShare - state.spent;
            decimal actions = (decimal)Math.Max(1f, (state.buildSeconds - state.stageSeconds) / 4.25f);
            decimal quantity = decimal.Floor(left / actions / price * (decimal)(.6 + roll() * .8));
            return quantity < 1m ? default : new WhaleOperationOrder(roll() < .3 ? WhaleOperationKind.TakeAsk : WhaleOperationKind.JoinBid, quantity);
        }

        if (state.stage == WhaleOperationState.Drive)
        {
            if (price >= state.targetPrice || state.spent >= state.budget * .98m || state.stageSeconds >= state.driveSeconds)
            {
                state.stage = WhaleOperationState.Unwind; state.stageSeconds = 0f; state.wait = 0f;
                return default;
            }
            if (state.wait > 0f) { return default; }
            state.wait = 1f + (float)roll() * 1.5f;
            decimal left = state.budget - state.spent;
            decimal actions = (decimal)Math.Max(1f, (state.driveSeconds - state.stageSeconds) / 1.75f);
            decimal quantity = decimal.Floor(left / actions / price * (decimal)(.5 + roll() * 1.5));
            return quantity < 1m ? default : new WhaleOperationOrder(WhaleOperationKind.LiftAsk, quantity);
        }

        // 털기: 작전으로 산 물량만 판다. 원래 갖고 있던 지분은 건드리지 않는다.
        if (position < 1m || state.stageSeconds >= state.unwindSeconds * 2f)
        {
            state.stage = WhaleOperationState.None; state.finished++;
            state.restUntilDay = day + 1 + (int)(roll() * 3);
            return new WhaleOperationOrder(WhaleOperationKind.Finish, 0m);
        }
        if (state.wait > 0f) { return default; }
        state.wait = 2f + (float)roll() * 3f;
        bool late = state.stageSeconds >= state.unwindSeconds;
        decimal average = state.boughtShares > 0m ? state.spent / state.boughtShares : price;
        bool losing = price < average * CutLoss;
        decimal remaining = (decimal)Math.Max(1f, (state.unwindSeconds - state.stageSeconds) / 3.5f);
        decimal sell = decimal.Ceiling(position / remaining * (decimal)(.6 + roll() * .8));
        if (losing || late) { sell *= 2m; }
        sell = Math.Min(decimal.Floor(position), Math.Max(1m, sell));
        return new WhaleOperationOrder(losing || late || roll() < .4 ? WhaleOperationKind.HitBid : WhaleOperationKind.JoinAsk, sell);
    }
}
