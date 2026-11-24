using System;

// 세력 작전의 판단부.
// 조용히 모은 뒤 크게 끌어올려 개미가 올라타게 하고, 올라탄 만큼 물량을 넘긴다.
// 목표 고점에서는 한 번 크게 팔아 휘청이게 한 다음 다시 올리고, 또 휘청이게 하기를 몇 번 되풀이한다.
// 여기서는 "다음에 어떤 주문을 얼마나 낼지"만 정한다. 가격·잔고·호가는 건드리지 않고, 실제 주문은 WhaleBot이 자기 계좌로 낸다.

/// <summary>작전 한 건의 진행 상태. 세력 계좌와 함께 저장된다.</summary>
public sealed class WhaleOperationState
{
    public const int None = 0, Build = 1, Drive = 2, Shake = 3, Repump = 4, Unwind = 5, Scoop = 6;
    /// <summary>던지기를 시작한 값, 그리고 던진 뒤 저점에서 다시 받아 담는 데 쓸 돈과 쓴 돈, 그 길이(장중 초).</summary>
    public decimal dumpTop, scoopBudget, scoopSpent;
    public float scoopSeconds;
    /// <summary>
    /// 작전의 물결: 올리고 → 던지고 → 저점에서 줍고 → 더 높이 올린다. 이것을 몇 번 되풀이할지(waves)와 지금 몇 번째인지(wave, 0부터).
    /// 줍는 까닭은 다음 물결에서 더 높이 올리기 위해서다. 보통 두세 번에서 네 번이고, 드물게 여덟 번에서 열네 번까지 간다.
    /// </summary>
    public int wave, waves;
    /// <summary>이번 물결의 고점. "고점 근처에서만 넘긴다"는 규칙과 흔들기의 깊이는 이 값을 기준으로 한다(지난 물결의 고점이 아니다).</summary>
    public decimal wavePeak;
    /// <summary>이번 물결에서 시작가 대비 몇 배까지 올릴지(0.25면 +25%), 그리고 던질 때 멈추는 값.
    /// 계단처럼 오른다: +25%까지 올리고 +10%까지 던지고, 주워서 +50%까지 올리고 +20%까지 던지는 식이다. 수치는 세력이 작전마다, 물결마다 정한다.</summary>
    public decimal waveGain, dumpFloor;
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
    public decimal SellFloor => Dumping ? Math.Max(floorPrice, dumpFloor) : Math.Max(floorPrice, wavePeak * WhaleOperationPlan.PeakShare);
    /// <summary>
    /// 세력의 이익: 작전을 시작한 값(startPrice)을 기준으로 잰다. 띄운 값에 넘긴 돈 − 쓴 돈 + 늘어난(줄어든) 주식을 작전 전 값으로 친 것.
    /// 세력은 자기 회사의 대주주다. 원래 지분을 띄운 값에 넘기고 싸게 되사면 이익이고, 올리느라 비싸게 산 것을 싸게 던지면 손해다.
    /// 던질 때는 떨어뜨리려는 깊이(dumpFloor, 언제나 시작가보다 높다) 아래로 팔지 않는다.
    /// </summary>
    public decimal Profit => proceeds - spent - fees + lastPosition * startPrice;
    /// <summary>
    /// 주식 수가 변하지 않는 거래에서 나간 매도 수수료(통정매매). 넘겼다가 되받으면 주식은 그대로인데 수수료만 나간다.
    /// 보통의 매도 수수료는 proceeds에 이미 빠져 있다(판 돈은 수수료를 뗀 값으로 적는다). 세력은 이 둘을 모두 비용으로 본다.
    /// 예전 저장에는 없어서 0으로 읽힌다.
    /// </summary>
    public decimal fees;
    /// <summary>작전으로 산 수량의 합과, 그 평균 매수가(기록용).</summary>
    public decimal sharesBought;
    public decimal AverageCost => sharesBought > 0m ? spent / sharesBought : 0m;
    /// <summary>지금까지의 손익: 판 돈 − 산 돈(실현), 그리고 남은 작전 물량을 지금 값으로 친 것까지 더한 값(평가).</summary>
    public decimal Realized => proceeds - spent - fees;
    /// <summary>남은 물량을 지금 값에 다 팔면 그때도 수수료가 나간다. 그것까지 뺀 값이다.</summary>
    public decimal Paper(decimal price) => proceeds - spent - fees + Trader.NetOfSellFee(Math.Max(0m, lastPosition) * price);
    /// <summary>이번 물결을 시작할 때까지의 고점. 이번 물결이 이것을 넘지 못하면 계단이 끊긴 것이다.</summary>
    public decimal priorPeak;
    /// <summary>넘기는 동안 마지막으로 팔린 때(단계 시작부터의 초)와 그때의 판 금액.</summary>
    public float handSoldAt; public decimal handProceeds;
    /// <summary>
    /// 불붙이기: 세력은 값을 끝까지 자기 돈으로 올리지 않는다. 한 번 크게 쳐 올리고(sparkReach만큼) 손을 뗀 뒤, 개미가 따라붙어 올리는 동안에는 사지 않는다.
    /// 값이 멈추면 다시 친다. 물결 하나에 sparkLimit번(7~12번)까지만 치고, 그래도 안 붙으면 그 물결은 거기까지다.
    /// </summary>
    public int sparks, sparkLimit;
    public decimal sparkReach;
    /// <summary>값이 스스로 오르고 있는가(2초마다 잰다).</summary>
    public bool rideRising, rideFalling; public float rideClock; public decimal ridePrice;
    /// <summary>마지막으로 불을 붙이기 직전의 값과, 불을 붙였는데 그 값 밑으로 도로 밀린 횟수(연속). 두 번 연달아 밀리면 사람들이 받아 주지 않는 것이다.</summary>
    public decimal sparkBase; public int sparkFails;
    /// <summary>이번 작전에서 불이 붙은 적이 있는가(시작가 +18%를 넘겼는가).</summary>
    public bool wasLit;
    /// <summary>끌어올리기를 시작할 때까지 쓴 돈(매집에 쓴 돈). 직접 끄는 데 쓴 돈은 여기서부터 센다.</summary>
    public decimal driveBase;
    /// <summary>이번 물결을 시작할 때까지 번 돈(작전 시작가 기준). 다음 물결을 직접 끄는 데는 이만큼까지만 쓴다.</summary>
    public decimal waveStake;
    /// <summary>내려찍기: 던질 때 몇 번에 나눠 찍을지와 지금까지 찍은 횟수.</summary>
    public int slams, slamsDone;
    /// <summary>마지막 정리를 한꺼번에 던지는 것으로 정했는지, 그리고 그것을 이미 정했는지.</summary>
    public bool dump, unwindSet;
    /// <summary>
    /// 던지기의 앞부분: 고점에서 몇 초 동안은 매수 호가를 치지 않고 매도 호가에 물량을 내놓아, 뒤늦게 따라붙는 매수에 먼저 넘긴다.
    /// 나쁜 소문은 이것이 끝난 뒤에 퍼뜨린다. 소문부터 내면 개미가 먼저 던져서 세력이 팔 자리가 없다.
    /// </summary>
    public bool handing;
    public float handSeconds;
    /// <summary>지금 던지는 중인가. 던질 때는 고점 근처를 지키지 않고, 시작가 기준 바닥선까지 매수 호가를 차례로 받아 친다.</summary>
    public bool Dumping => stage == Unwind && dump;
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
    /// <summary>불붙이기: 매도 호가를 한 번에 쓸어 올린다(ManipulationTools.Ignite). 수량은 쓸 돈을 현재가로 나눈 것이다.</summary>
    Ignite,
    /// <summary>털기: 매수 호가 두세 칸을 한 번에 받아 치고 남은 주문은 거둔다(ManipulationTools.Nip). 수량은 이번에 팔 수 있는 한도이고, 실제 수량은 호가창이 정한다.</summary>
    Nip,
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
    public const decimal BuildShare = .5m;
    /// <summary>평균 매수가에서 이만큼 밀리면 호가를 기다리지 않고 판다.</summary>
    public const decimal CutLoss = .96m;
    /// <summary>목표의 이만큼을 올린 뒤부터, 따라붙는 매수가 뜨거울수록 올리는 중에도 물량을 넘긴다.</summary>
    public const decimal SkimFrom = .35m;
    /// <summary>오르는 동안의 조용한 분산은 시작가에서 이만큼은 오른 뒤부터 한다. 그 밑에서는 팔아도 남는 것이 없다.</summary>
    public const decimal QuietSellFrom = 1.04m;
    /// <summary>불이 붙는 높이(시작가 대비)와, 거기까지 직접 끄는 데 쓸 수 있는 돈(예산 대비).</summary>
    public const decimal LitGain = .18m, LitBudget = .12m;
    /// <summary>한 번 흔들 때 고점에서 이만큼(6~12%) 밀리면 더 팔지 않는다. 한꺼번에 무너뜨리지 않기 위해서다.</summary>
    public const decimal ShakeDepthMin = .06m, ShakeDepthMax = .12m;
    /// <summary>작전을 시작한 값의 이만큼 아래로는 물량을 내놓지 않는다. 그 밑에서는 남은 물량을 그냥 들고 끝낸다.</summary>
    public const decimal FloorShare = .85m;
    /// <summary>고점의 이만큼 아래로는 팔지 않는다(던질 때는 예외다).</summary>
    public const decimal PeakShare = .72m;
    /// <summary>
    /// 충분히 띄운 작전(시작가의 1.3배 이상)의 마지막 정리가 "던지기"가 될 확률. 던질 때는 20~60초 안에 남은 물량을 매수 호가에 쏟아붓는다.
    /// 나머지는 예전처럼 천천히 넘긴다. 작전마다 끝이 다르다.
    /// </summary>
    public const double DumpChance = .7;
    /// <summary>
    /// 던진 뒤의 줍기: 판 돈의 이만큼까지를 저점에서 다시 산다. 던지기를 시작한 값보다 이만큼 넘게 싸졌을 때만 산다.
    /// 무너진 값을 세력이 받아 가는 것이라, 급락은 바닥 없이 이어지지 않고 어딘가에서 멎는다.
    /// </summary>
    public const decimal ScoopShare = .35m, ScoopBelow = .80m;
    /// <summary>던질 때도 던지기 시작한 값의 이만큼 아래로는 팔지 않는다. 물결마다 저점이 앞 물결보다 높게 남는다.</summary>
    public const decimal DumpDepth = .62m;
    /// <summary>드물게 나오는 큰 작전의 확률. 여덟 번에서 열네 번까지 물결을 되풀이해 수십 배까지 띄운다.</summary>
    public const double GrandChance = .06;
    public const decimal DumpFromGain = 1.10m;
    /// <summary>고점에서 넘기는 시간(장중 초). 개미의 주문이 판단에서 5~10초 늦게 닿으므로 그만큼은 받아 준다. 넘기는 중에 값이 이만큼 밀리면 바로 던진다.</summary>
    public const float HandMin = 5f, HandMax = 9f;
    /// <summary>넘기는 물량이 계속 팔리는 동안에는 더 넘긴다(길어도 이만큼). 이만큼 동안 아무도 안 사면 던진다.</summary>
    public const float HandLimit = 28f, HandQuiet = 3.5f;
    public const decimal HandSlip = .96m;
    /// <summary>던진 뒤 다시 올릴 돈이 예산의 이만큼도 안 남았으면 다음 물결을 하지 않고 작전을 접는다.</summary>
    public const decimal NextWaveFunds = .15m;
    /// <summary>던질 때 남겨 두는 몫: 시작가에서 오른 폭의 30~55%는 남기고 나머지를 무너뜨린다(+25%였다면 +8~14%에서 멈춘다).</summary>
    public const decimal DumpKeepMin = .30m, DumpKeepMax = .55m;
    /// <summary>다음 물결의 목표는 앞 물결 목표의 1.5~2.2배다(+25% → +50% → +100% …). 아무리 길어도 +6000%를 넘기지 않는다.</summary>
    public const decimal WaveGrowthMin = 1.5m, WaveGrowthMax = 2.2m, GainLimit = 60m;

    /// <summary>
    /// 작전을 연다. 길이는 장중 초 기준이고 하루 장은 900초다. fast는 검증용으로 매집을 짧게 줄인다.
    /// 첫 물결의 목표 상승률은 +15%에서 +40%까지다. 그 뒤의 물결은 앞 물결 목표의 1.5~2.2배를 잡는다.
    /// </summary>
    public static void Begin(WhaleOperationState state, decimal price, decimal budget, decimal extraShares, int day, Func<double> roll, bool fast = false)
    {
        state.stage = WhaleOperationState.Build;
        state.budget = budget; state.spent = 0m; state.proceeds = 0m; state.fees = 0m; state.lastPosition = 0m; state.boughtShares = 0m; state.extraShares = Math.Max(0m, decimal.Floor(extraShares));
        state.startPrice = price; state.peakPrice = price; state.targetPrice = 0m; state.floorPrice = price * FloorShare;
        double tail = roll();
        state.gain = .15m + (decimal)tail * .25m; state.waveGain = state.gain; state.dumpFloor = 0m;
        state.buildSeconds = fast ? 25f + (float)roll() * 10f : 400f + (float)roll() * 1100f;
        state.driveSeconds = 260f + (float)roll() * 540f;
        state.unwindSeconds = 300f + (float)roll() * 900f;
        state.shakes = 0; state.shakesDone = 0;
        state.shakeQuota = state.shakeSold = state.shakeFloor = 0m; state.repumpBudget = state.repumpSpent = state.repumpGoal = 0m;
        state.stageSeconds = 0f; state.wait = 0f; state.startDay = day;
        state.dump = false; state.unwindSet = false; state.dumpTop = state.scoopBudget = state.scoopSpent = 0m; state.scoopSeconds = 0f;
        state.wasLit = false; state.sparks = 0; state.sparkFails = 0; state.sparkBase = price; state.rideFalling = false; state.sparkLimit = 7 + (int)(roll() * 6); state.rideRising = false; state.rideClock = 0f; state.ridePrice = price; state.slams = state.slamsDone = 0;
        state.handing = false; state.handSeconds = 0f; state.sharesBought = 0m; state.priorPeak = price; state.handSoldAt = 0f; state.handProceeds = 0m;
        // 물결의 수: 대개 2~4번(평균 3번), 드물게 8~14번.
        state.wavePeak = price;
        state.wave = 0; state.waves = roll() < GrandChance ? 8 + (int)(roll() * 7) : 2 + (int)(roll() * 3);
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
        if (moved > 0m) { state.sharesBought += moved; state.spent += moved * price; if (state.stage == WhaleOperationState.Repump) { state.repumpSpent += moved * price; } if (state.stage == WhaleOperationState.Scoop) { state.scoopSpent += moved * price; } }
        // 판 돈은 매도 수수료를 뗀 값으로 적는다. 손에 들어온 돈만 번 돈이다(다음 물결의 자금, 이익 판단이 모두 이 값을 쓴다).
        else if (moved < 0m) { state.proceeds += Trader.NetOfSellFee(-moved * price); if (state.stage == WhaleOperationState.Shake) { state.shakeSold += -moved; } }
        state.boughtShares = Math.Max(state.boughtShares, position);
        state.lastPosition = position;
        if (price > state.peakPrice) { state.peakPrice = price; }
        if (price > state.wavePeak) { state.wavePeak = price; }
        decimal remaining = state.Remaining(position);
        state.rideClock += seconds;
        if (state.rideClock >= 2f) { state.rideRising = price > state.ridePrice * 1.002m; state.rideFalling = price < state.ridePrice * .997m; state.ridePrice = price; state.rideClock = 0f; }

        switch (state.stage)
        {
            case WhaleOperationState.Build: return StepBuild(state, price, roll);
            case WhaleOperationState.Drive: return StepDrive(state, price, remaining, heat, roll);
            case WhaleOperationState.Shake: return StepShake(state, price, remaining, roll);
            case WhaleOperationState.Repump: return StepRepump(state, price, remaining, heat, roll);
            case WhaleOperationState.Scoop: return StepScoop(state, price, day, roll);
            default: return StepUnwind(state, price, position, remaining, day, roll);
        }
    }

    private static WhaleOperationOrder StepBuild(WhaleOperationState state, decimal price, Func<double> roll)
    {
        // 끌어올리기는 정해 둔 시각에 시작한다. 매집할 돈을 일찍 다 썼으면 그때까지 기다린다(문틈 쪽지에 적힌 시각이 이 시각이다).
        if (state.stageSeconds >= state.buildSeconds)
        {
            state.stage = WhaleOperationState.Drive; state.stageSeconds = 0f; state.wait = 0f;
            state.targetPrice = price * (1m + state.gain); state.driveBase = state.spent;
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
        // 첫 물결은 판 돈의 절반을, 그 뒤의 물결은 판 돈의 대부분을 다시 쓴다.
        decimal reuse = state.wave == 0 ? .5m : .85m;
        decimal left = state.budget + state.proceeds * reuse - state.spent;
        // 개미가 올리고 있는가: 값이 스스로 오르고 있다.
        // (따라붙는 매수가 뜨거워도 값이 안 오르면 올려 주는 것이 아니다. 그때는 다시 친다.)
        bool carried = state.rideRising;
        // 칠 만큼 쳤는데(또는 돈이 없는데) 아무도 안 올린다: 이 물결은 여기까지다.
        // 불을 붙였는데 두 번 연달아 그 전 값 밑으로 밀렸다: 올려 주는 족족 팔아 치우는 장이다. 더 돈을 쓰지 않는다.
        // 불이 붙었는가: 시작가에서 LitGain(+18%)을 넘으면 급등주로 눈에 띄어 사람들이 스스로 몰린다. 그 전까지는 아무도 안 따라온다.
        // 붙기 전: 세력이 직접 끈다. 짧은 간격으로 쳐 올리고, 밀려도 다시 친다. 여기에 쓰는 돈은 예산의 LitBudget(12%)까지다. 그 돈으로도 못 붙이면 접는다.
        // 붙은 뒤: 손을 떼고 개미가 올리게 둔다. 멈추면 가끔 다시 치고, 두 번 연달아 밀리면 그 물결은 거기까지다.
        // 다음 물결도 마찬가지다: 던진 뒤 값이 그 밑으로 내려가 있으면 다시 직접 끌어 올려야 한다(그 물결에 쓸 돈은 그 물결을 시작할 때부터 다시 센다).
        bool lit = price >= state.startPrice * (1m + LitGain);
        if (lit != state.wasLit) { state.wasLit = lit; if (lit) { state.sparks = 0; state.sparkFails = 0; state.sparkBase = price; } }
        // 다음 물결부터는 번 돈으로만 민다: 그 물결을 시작할 때까지의 이익(waveStake)을 넘겨 쓰지 않는다. 이긴 작전을 다음 물결에서 손해로 만들지 않는다.
        decimal pullBudget = state.wave == 0 ? state.budget * LitBudget : Math.Min(state.budget * LitBudget, Math.Max(0m, state.waveStake));
        bool cold = !lit && state.spent - state.driveBase >= pullBudget;
        bool spentOut = !carried && state.wait <= 0f && (cold || lit && (state.sparks >= state.sparkLimit || state.sparkFails >= 2) || left < price);
        bool reached = price >= state.targetPrice, late = state.stageSeconds >= state.driveSeconds;
        if (reached || spentOut || late)
        {
            // 계단이 끊겼다: 이번 물결이 앞 고점을 넘지 못했다(따라붙는 사람이 없다). 더 올리지 않고 이번에 정리하고 끝낸다.
            if (state.wave > 0 && state.wavePeak <= state.priorPeak) { state.waves = state.wave + 1; }
            bool lifted = price >= state.startPrice * 1.15m;
            if (lifted && remaining >= 1m && state.shakesDone < state.shakes) { BeginShake(state, price, remaining, heat, roll); }
            else { state.stage = WhaleOperationState.Unwind; state.stageSeconds = 0f; state.wait = 0f; }
            return default;
        }
        if (state.wait > 0f) { return default; }
        if (!lit)
        {
            // 직접 끌기: 스스로 오르는 중이면 지켜보고, 아니면 1~2.5초마다 3~6%씩 쳐 올린다.
            if (carried) { state.wait = 1f; return default; }
            state.sparks++; state.wait = 1f + (float)roll() * 1.5f;
            state.sparkReach = (decimal)(.03 + roll() * .03);
            decimal pull = decimal.Floor(state.budget * LitBudget * (decimal)(.10 + roll() * .10) / price);
            return pull < 1m ? default : new WhaleOperationOrder(WhaleOperationKind.Ignite, pull);
        }
        if (carried)
        {
            // 개미가 올리는 중이다. 세력은 사지 않고, 오르는 내내 티 나지 않게 조금씩 턴다(분산).
            // 매도 호가에 물량을 얹지 않는다(얹으면 매도벽이 된다). 매수 호가 두세 칸을 한 번에 받아 치고 남은 주문은 거둔다.
            // 얼마나 팔지는 남은 물량의 비율이 아니라 호가창이 정한다: 그 칸들에 서 있는 물량보다 조금 더(WhaleBot이 정한다). 여기서 주는 수량은 한 번의 한도다.
            // 세력의 이익은 여기서 나온다: 싸게 모은 물량과 원래 지분이 개미가 올려 준 값에 팔린다.
            state.wait = 2f + (float)roll() * 2.5f;
            if (remaining < 1m || price < state.startPrice * QuietSellFrom) { return default; }
            return new WhaleOperationOrder(WhaleOperationKind.Nip, Math.Max(1m, decimal.Floor(remaining * .08m)));
        }
        // 떨어지는 중에는 치지 않는다. 떨어지는 값에 돈을 부으면 파는 사람들 좋은 일만 한다. 멈출 때까지 기다린다.
        if (state.rideFalling) { state.wait = 1.5f; return default; }
        if (state.sparks > 0) { state.sparkFails = price < state.sparkBase ? state.sparkFails + 1 : 0; if (state.sparkFails >= 2) { return default; } }
        state.sparkBase = price;
        // 값이 멈췄다: 한 번 크게 쳐 올려 불을 붙인다. 그러고는 개미가 보고 따라붙을 때까지(주문은 판단에서 1~3초 늦게 닿는다) 기다린다.
        state.sparks++; state.wait = 3.5f + (float)roll() * 3.5f;
        // 물결이 클수록 한 번에 높이 친다: 이번 물결 목표의 4분의 1쯤, 3~12%.
        state.sparkReach = Math.Max(.03m, Math.Min(.12m, state.waveGain * .25m)) * (decimal)(.7 + roll() * .6);
        // 불이 붙은 뒤에 다시 치는 것은 작게 친다: 한 번에 예산의 1.5~3%. 크게 쳐 봐야 올려 주는 값에 남들이 팔 뿐이다.
        decimal quantity = decimal.Floor(Math.Min(left, state.budget * (decimal)(.015 + roll() * .015)) / price);
        return quantity < 1m ? default : new WhaleOperationOrder(WhaleOperationKind.Ignite, quantity);
    }

    private static void BeginShake(WhaleOperationState state, decimal price, decimal remaining, double heat, Func<double> roll)
    {
        state.stage = WhaleOperationState.Shake; state.stageSeconds = 0f; state.wait = 0f; state.shakeSold = 0m;
        // 회차가 거듭될수록, 그리고 뜨거울수록 더 많이 넘긴다: 남은 물량의 15% → 25% → 35% …
        decimal share = Math.Min(.6m, (.15m + .10m * state.shakesDone) * (decimal)(.7 + heat * .6));
        state.shakeQuota = Math.Max(1m, decimal.Floor(remaining * share));
        // 작은 물결에서는 흔들기도 얕다(+25% 올린 데서 12%를 흔들면 절반이 날아간다).
        decimal depth = (ShakeDepthMin + (decimal)roll() * (ShakeDepthMax - ShakeDepthMin)) * Math.Max(.35m, Math.Min(1m, state.waveGain / .6m));
        state.shakeFloor = Math.Max(price, state.wavePeak) * (1m - depth);
        state.repumpGoal = Math.Max(price, state.wavePeak);
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
            state.targetPrice = Math.Max(price, state.wavePeak) * (1m + .05m + (decimal)roll() * .15m);
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

    private static WhaleOperationOrder StepScoop(WhaleOperationState state, decimal price, int day, Func<double> roll)
    {
        // 정한 돈을 다 썼거나 시간이 다 되면 끝낸다. 주운 물량은 그대로 들고 간다(자기 회사 주식이다).
        if (state.scoopSpent >= state.scoopBudget * .97m || state.stageSeconds >= state.scoopSeconds || state.scoopBudget < price)
        {
            // 다 주웠다. 다음 물결: 앞 고점보다 20~60% 더 높은 곳을 새 목표로 잡고 다시 끌어올린다.
            state.wave++; state.wavePeak = price; state.priorPeak = state.peakPrice;
            state.sparks = 0; state.sparkFails = 0; state.sparkBase = price; state.sparkLimit = 7 + (int)(roll() * 6); state.driveBase = state.spent; state.wasLit = false; state.waveStake = state.Profit;
            state.stage = WhaleOperationState.Drive; state.stageSeconds = 0f; state.wait = 0f;
            state.waveGain = Math.Min(GainLimit, state.waveGain * (WaveGrowthMin + (decimal)roll() * (WaveGrowthMax - WaveGrowthMin)));
            state.targetPrice = Math.Max(state.startPrice * (1m + state.waveGain), Math.Max(price, state.peakPrice) * 1.08m);
            state.driveSeconds = 120f + (float)roll() * 240f; state.unwindSeconds = 300f + (float)roll() * 900f;
            state.shakes = 0; state.shakesDone = 0;
            state.dump = false; state.unwindSet = false; state.handing = false;
            return default;
        }
        if (state.wait > 0f) { return default; }
        state.wait = 1.2f + (float)roll() * 1.8f;
        // 충분히 싸졌을 때만 받는다. 아직 높으면 더 내려오기를 기다린다.
        // 무너진 폭의 아래쪽 3분의 1 근처까지 내려왔을 때 받는다.
        if (state.dumpTop > state.dumpFloor && price > state.dumpFloor + (state.dumpTop - state.dumpFloor) * .35m) { return default; }
        decimal left = state.scoopBudget - state.scoopSpent;
        decimal actions = (decimal)Math.Max(1f, (state.scoopSeconds - state.stageSeconds) / 2.1f);
        // 줍는 데 쓰기로 한 돈을 넘기지 않는다.
        decimal quantity = Math.Min(decimal.Floor(left / price), decimal.Floor(left / actions / price * (decimal)(.7 + roll() * 1.2)));
        // 대부분은 매수 호가에 줄을 서서 던지는 물량을 받고, 가끔은 매도 호가를 받아 간다.
        return quantity < 1m ? default : new WhaleOperationOrder(roll() < .35 ? WhaleOperationKind.TakeAsk : WhaleOperationKind.JoinBid, quantity);
    }

    private static WhaleOperationOrder StepUnwind(WhaleOperationState state, decimal price, decimal position, decimal remaining, int day, Func<double> roll)
    {
        // 마지막 정리: 작전으로 산 물량 가운데 남은 것만 판다. 원래 지분은 더 건드리지 않는다.
        // 값이 바닥선 아래로 내려가 있으면 팔지 않고 기다린다. 끝까지 못 팔면 남은 물량은 그대로 들고 끝낸다(어차피 자기 회사 주식이다).
        if (!state.unwindSet)
        {
            // 정리에 들어서는 순간 끝을 정한다: 높이 띄워 놓았으면 대개는 받치던 것을 거두고 한꺼번에 던진다.
            state.unwindSet = true;
            // 뒤에 물결이 더 남았으면 반드시 던진다(던져야 싸게 주워서 더 높이 올린다). 마지막 물결만 열에 셋은 천천히 넘긴다.
            bool more = state.wave + 1 < state.waves;
            // 충분히 띄웠으면 언제나 내려찍는다. 못 띄웠으면 팔지 않고 그 자리에서 끝낸다(산 물량은 들고 간다. 자기 회사 주식이다).
            // 천천히 흘려 파는 정리는 하지 않는다. 값이 캔들마다 한 칸씩 흘러내리는 밋밋한 하락만 만들고 세력도 손해를 본다.
            state.dump = price >= state.startPrice * DumpFromGain;
            if (!state.dump)
            {
                state.stage = WhaleOperationState.None; state.finished++;
                state.restUntilDay = day + 1 + (int)(roll() * 3);
                return new WhaleOperationOrder(WhaleOperationKind.Finish, 0m);
            }
            if (state.dump)
            {
                state.unwindSeconds = 6f + (float)roll() * 10f; state.dumpTop = price;
                decimal keep = DumpKeepMin + (decimal)roll() * (DumpKeepMax - DumpKeepMin);
                state.dumpFloor = state.startPrice + Math.Max(0m, price - state.startPrice) * keep;
                // 고점에서 매도 호가에 물량을 쌓아 두지 않는다(위를 막아 세우면 아무도 올리지 못한다). 두 번에서 네 번, 크게 내려찍는다.
                state.handing = false; state.slams = 2 + (int)(roll() * 3); state.slamsDone = 0;
            }
        }
        // 던질 때는 작전으로 산 것뿐 아니라 넘기기로 한 원래 지분의 남은 몫까지 함께 던진다.
        decimal stock = state.dump ? remaining : position;
        if (state.dump && state.handing)
        {
            // 먼저 넘긴다: 매도 호가에 물량을 내놓고 따라붙는 매수가 받아 가게 둔다. 시간이 다 됐거나 값이 밀리기 시작하면 그때 던진다.
            // 따라붙는 매수가 받아 가는 동안에는 계속 넘긴다. 한동안 아무도 안 사면 그때 던진다.
            if (state.proceeds > state.handProceeds) { state.handProceeds = state.proceeds; state.handSoldAt = state.stageSeconds; }
            bool quiet = state.stageSeconds >= state.handSeconds && state.stageSeconds - state.handSoldAt >= HandQuiet;
            if (stock < 1m || quiet || state.stageSeconds >= HandLimit || price <= state.dumpTop * HandSlip) { state.handing = false; state.stageSeconds = 0f; state.wait = 0f; return default; }
            if (state.wait > 0f) { return default; }
            state.wait = .35f + (float)roll() * .35f;
            decimal turns = (decimal)Math.Max(1f, (state.handSeconds - state.stageSeconds) / .5f);
            decimal offer = decimal.Ceiling(stock / turns * (decimal)(.8 + roll() * .8));
            return new WhaleOperationOrder(WhaleOperationKind.JoinAsk, Math.Min(decimal.Floor(stock), Math.Max(1m, offer)));
        }
        // 다음 물결을 올릴 돈이 남아 있을 때만 줍고 이어 간다. 돈이 바닥났으면 여기서 접는다.
        // 다음 물결은 돈이 남아 있고, 지금까지 이익을 보고 있을 때만 한다. 손해를 보고 있으면 여기서 접는다.
        bool funded = state.budget + state.proceeds * .85m - state.spent >= state.budget * NextWaveFunds && state.Profit > 0m;
        bool slammed = state.dump && state.slamsDone >= state.slams && state.wait <= 0f;
        if (state.dump && funded && state.wave + 1 < state.waves && (stock < 1m || slammed || state.stageSeconds >= state.unwindSeconds * 2f))
        {
            // 다 던졌다. 이제 무너진 값을 저점에서 받아 담는다. 쓸 수 있는 돈은 판 돈의 일부이고, 처음 예산과 판 돈의 합을 넘지 않는다.
            state.stage = WhaleOperationState.Scoop; state.stageSeconds = 0f; state.wait = 3f + (float)roll() * 4f;
            state.scoopSpent = 0m; state.scoopSeconds = 25f + (float)roll() * 45f;
            state.scoopBudget = Math.Max(0m, Math.Min(state.proceeds * ScoopShare, (state.budget + state.proceeds - state.spent) * .9m));
            return default;
        }
        if (stock < 1m || slammed || state.stageSeconds >= state.unwindSeconds * 2f)
        {
            state.stage = WhaleOperationState.None; state.finished++;
            state.restUntilDay = day + 1 + (int)(roll() * 3);
            return new WhaleOperationOrder(WhaleOperationKind.Finish, 0m);
        }
        if (state.wait > 0f) { return default; }
        if (state.dump)
        {
            // 내려찍기: 남은 물량을 두 번에서 네 번에 나눠 매수 호가에 한꺼번에 던진다. 다 못 판 주문은 그 자리에서 거둔다(매도벽으로 남기지 않는다).
            // 바닥선(떨어뜨리려는 깊이, 평균 매수가, 시작가의 85%) 아래로는 팔지 않는다.
            state.wait = .6f + (float)roll() * 1.2f;
            if (price <= state.SellFloor) { return default; }
            int slamsLeft = Math.Max(1, state.slams - state.slamsDone); state.slamsDone++;
            decimal slam = decimal.Ceiling(stock / slamsLeft);
            return new WhaleOperationOrder(WhaleOperationKind.HitBid, Math.Min(decimal.Floor(stock), Math.Max(1m, slam)));
        }
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
