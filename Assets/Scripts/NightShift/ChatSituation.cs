using System;
using System.Collections.Generic;
using System.Globalization;

// 시세 상황 판정.
//
// 봇들이 시세를 두고 하는 말은 여러 장치에서 나온다(대화 틀의 시세 대사, 차트 수다, 급등락 군중, 잔움직임, 토론, 장외 대화).
// 예전에는 장치마다 자기 창(몇 초, 몇 분)만 보고 "빠졌다"·"올랐다"를 정했다. 그래서 +500%까지 간 종목이 +490%로 내려왔을 때
// "벌써 -6% ㄷㄷ" 같은 호들갑이 나왔다. 오늘 번 것의 2%를 돌려준 것뿐인데 폭락처럼 떠든 것이다.
//
// 여기서는 종목마다 "지금이 어떤 상황인가"를 한 번만 정한다. 재료는 누구나 화면에서 보는 것뿐이다:
//   기준가(전일 종가) · 당일 고가와 저가(장 전체) · 현재가 · 최근 30초와 2분의 속도.
//   (이 게임의 장은 실제 시간으로 15분쯤이다. 그래서 "빠르다"를 재는 창도 30초와 2분으로 짧다. 2분이면 게임 시간으로 한 시간 남짓이다.)
// 핵심은 되돌림을 "가격의 몇 %"가 아니라 "오늘 오른 폭의 몇 할"로 재는 것이다.
//   되돌림 = (고가 − 현재가) ÷ (고가 − 기준가)      +500% → +490% 는 0.02, +30% → +5% 는 0.83
//   회복   = (현재가 − 저가) ÷ (기준가 − 저가)      −35%에서 8% 튄 것은 0.15
// 모든 장치는 말하기 전에 이 판정을 묻고, 상황과 어긋나는 말은 하지 않거나(침묵) 상황에 맞는 말로 바꾼다.
// 언어와 상관없는 부분은 여기까지다. 그 뒤(무엇을 얼마나 자주, 어떤 말투와 숫자로 말하는가)는 언어마다 따로다(ChatSituationReaction.cs).

/// <summary>종목의 지금 상황. 한 종목은 한 번에 하나의 상황에만 있다.</summary>
public enum ChatSituation
{
    /// <summary>말할 것이 없다(조금 움직였을 뿐이다).</summary>
    Quiet,
    /// <summary>하루 종일 제자리(전일 대비 ±0.7% 안, 당일 폭 1.5% 미만).</summary>
    Flat,
    /// <summary>+3% 이상 올라 있고 고점 근처에서 버틴다. 급하지는 않다.</summary>
    DriftUp,
    /// <summary>−3% 이하로 내려 있다. 급하지는 않다.</summary>
    DriftDown,
    /// <summary>1분 넘게 버티던 당일 고가를 막 넘었다(45초 안, 최근 30초 +1% 이상).</summary>
    Breakout,
    /// <summary>빠르게 오르는 중(30초 +6% 또는 2분 +10%)이고 고점 2% 안.</summary>
    Surge,
    /// <summary>당일 고가가 +100% 이상이고 아직 고점 1% 안.</summary>
    MonsterRally,
    /// <summary>당일 고가가 +100% 이상인데 오른 폭의 2할도 안 돌려준 눌림. 폭락이 아니다.</summary>
    MonsterDip,
    /// <summary>오른 폭의 2할~6할 5푼을 돌려줬다. 얘기할 만한 눌림.</summary>
    Pullback,
    /// <summary>오른 폭의 6할 5푼 이상을 돌려줬다. 왕복.</summary>
    RoundTrip,
    /// <summary>빠르게(30초 −6% 또는 2분 −10%) 그리고 크게(오른 폭의 3할 5푼 이상, 또는 고점에서 −20%, 또는 벌어 둔 것 없이) 빠진다.</summary>
    Crash,
    /// <summary>−6% 이하에서 저가 근처를 천천히 흘러내린다.</summary>
    SlowBleed,
    /// <summary>크게 빠진 날(−12% 이하) 저가에서 4% 넘게 튀었지만 빠진 폭의 절반도 못 찾았다.</summary>
    DeadCatBounce,
    /// <summary>−8% 넘게 빠졌다가 빠진 폭의 절반 이상을 되찾았다.</summary>
    Recovery,
    /// <summary>−25% 이하이고 저가 3% 안에서 아직 돌아서지 못했다.</summary>
    Capitulation
}

/// <summary>오늘 움직임의 크기(당일 고가 기준).</summary>
public enum ChatDayTier { Small, Notable, Big, Monster }

/// <summary>말하는 사람의 처지. 같은 눌림도 +300% 수익 중인 사람과 물린 사람에게는 다른 일이다.</summary>
public enum ChatSpeakerView { Watcher, DeepProfit, Profit, Even, Loss, DeepLoss }

/// <summary>대사 묶음을 고를 때 쓰는 처지: 구경꾼, 수익 중인 보유자, 물린 보유자.</summary>
public enum ChatStance { Watch, Gain, Loss }

/// <summary>판정에 넣는 값. 가격은 실제 값, 속도(r30, r120)는 %다.</summary>
public struct ChatSituationInput
{
    public decimal reference, price, high, low;
    /// <summary>고가·저가를 찍은 지 몇 초 지났는가. 둘 다 크게 움직인 날에는 더 최근의 것이 지금 얘깃거리다.</summary>
    public double highAge, lowAge;
    public decimal r30, r120;
    /// <summary>한동안 버티던 고가를 방금 넘었는가.</summary>
    public bool breakout;
}

/// <summary>판정 결과와, 말에 담을 수 있는 수치들. 퍼센트 값은 모두 % 단위다(dayPct 490 = +490%).</summary>
public struct ChatSituationRead
{
    public ChatSituation kind;
    public ChatDayTier tier;
    public decimal reference, price, high, low;
    public decimal dayPct, highPct, lowPct, fromHigh, fromLow;
    /// <summary>오늘 오른 폭 가운데 돌려준 몫(0~). 오른 것이 없으면 0.</summary>
    public decimal retrace;
    /// <summary>오늘 빠진 폭 가운데 되찾은 몫(0~). 빠진 것이 없으면 0.</summary>
    public decimal recovery;
    public decimal r30, r120;
    /// <summary>기준가의 몇 배인가(+490%면 5.9).</summary>
    public decimal multiple;
    public bool valid;
}

public static class ChatSituationMath
{
    // ── 문턱값. 바꿀 때는 Assets/Editor/ChatSituationCheck.cs의 표가 그대로 통과하는지 본다. ──
    /// <summary>이만큼 올랐던 날은 "괴물"이다. 여기서는 몇 % 빠진 것이 아니라 번 것의 몇 할을 돌려줬는지만 본다.</summary>
    public const decimal MonsterDay = 100m;
    public const decimal BigDay = 20m;
    /// <summary>당일 고가가 이만큼은 돼야 "오늘 오른 종목"으로 친다. 그 아래에서는 눌림·왕복을 따지지 않는다.</summary>
    public const decimal NotableGain = 6m;
    /// <summary>오른 폭의 2할 미만은 눌림 축에도 못 든다(괴물이면 MonsterDip, 아니면 그냥 오르는 중).</summary>
    public const decimal DipShare = .20m;
    /// <summary>오른 폭의 6할 5푼 이상을 돌려주면 왕복이다. 2할~6할 5푼은 눌림.</summary>
    public const decimal RoundTripShare = .65m;
    /// <summary>눌림·왕복이라고 부르려면 가격으로도 고점에서 이만큼(%)은 내려와야 한다(+6% 찍고 +4.5%는 눌림이 아니다).</summary>
    public const decimal PullbackMinDrop = 3m, RoundTripMinDrop = 4m;
    /// <summary>빠르다: 30초에 6%, 또는 2분에 10%.</summary>
    public const decimal Fast30 = 6m, Fast120 = 10m;
    /// <summary>폭락: 빠르면서, 오른 폭의 3할 5푼 이상을 돌려줬거나 고점에서 20% 넘게 내려왔다(벌어 둔 것이 없는 종목은 빠르기만 해도 폭락이다).</summary>
    public const decimal CrashShare = .35m, CrashFromHigh = -20m;
    /// <summary>투매: −25% 이하, 저가 3% 안, 최근 2분에 +1%도 못 돌렸다.</summary>
    public const decimal CapitulationDay = -25m, CapitulationNearLow = 3m, CapitulationTurn = 1m;
    /// <summary>저점 쪽 얘기는 −8% 넘게 빠졌던 날에만 한다. 반등은 저가에서 4% 이상, 회복은 빠진 폭의 절반 이상.</summary>
    public const decimal DeepLow = -8m, BounceRise = 4m, RecoveryShare = .5m, DeadCatDay = -12m;
    public const decimal BleedDay = -6m, BleedNearLow = 2.5m, TrendDay = 3m;
    public const decimal FlatDay = .7m, FlatRange = 1.5m;
    /// <summary>고점 근처: 괴물은 1% 안, 급등은 2% 안.</summary>
    public const decimal MonsterNearHigh = -1m, SurgeNearHigh = -2m;
    /// <summary>돌파: 넘긴 뒤 최근 30초에 이만큼(%)은 올라야 한다. 전일 대비 +3%도 안 되는 자리의 고가 갱신은 돌파라 하지 않는다.</summary>
    public const decimal BreakoutSpeed = 1m, BreakoutMinDay = 3m;
    /// <summary>말하는 사람의 처지: +50% 이상이면 크게 수익 중, ±3% 안이면 본전 근처, −20% 이하면 크게 물림.</summary>
    public const decimal DeepProfitPct = 50m, ProfitPct = 3m, DeepLossPct = -20m;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>부호와 %가 붙은 등락률. 10% 미만은 소수 한 자리까지, 그 이상은 정수로 적는다("+3.2%", "+490%", "-12%").</summary>
    public static string SignedPercent(decimal value)
        => value.ToString(Math.Abs(value) >= 10m ? "+0;-0;0" : "+0.#;-0.#;0", Culture) + "%";
    /// <summary>부호 없는 몫을 %로("15%").</summary>
    public static string SharePercent(decimal share) => Math.Round(Math.Max(0m, share) * 100m).ToString("0", Culture) + "%";

    public static ChatSituationRead Classify(in ChatSituationInput input)
    {
        var read = new ChatSituationRead { reference = input.reference, price = input.price, r30 = input.r30, r120 = input.r120 };
        if (input.reference <= 0m || input.price <= 0m) { return read; }
        decimal reference = input.reference, price = input.price;
        decimal high = Math.Max(input.high <= 0m ? price : input.high, price), low = Math.Min(input.low <= 0m ? price : input.low, price);
        read.high = high; read.low = low; read.valid = true;
        decimal day = (price / reference - 1m) * 100m, highPct = (high / reference - 1m) * 100m, lowPct = (low / reference - 1m) * 100m;
        decimal fromHigh = (price / high - 1m) * 100m, fromLow = (price / low - 1m) * 100m;
        decimal retrace = high > reference ? (high - price) / (high - reference) : 0m;
        decimal recovery = low < reference ? (price - low) / (reference - low) : 0m;
        decimal range = (high - low) / reference * 100m;
        read.dayPct = day; read.highPct = highPct; read.lowPct = lowPct; read.fromHigh = fromHigh; read.fromLow = fromLow;
        read.retrace = retrace; read.recovery = recovery; read.multiple = price / reference;
        read.tier = highPct >= MonsterDay ? ChatDayTier.Monster : highPct >= BigDay ? ChatDayTier.Big : highPct >= NotableGain ? ChatDayTier.Notable : ChatDayTier.Small;

        bool fastDown = input.r30 <= -Fast30 || input.r120 <= -Fast120, fastUp = input.r30 >= Fast30 || input.r120 >= Fast120;
        bool gained = highPct >= NotableGain;
        // 고가도 저가도 크게 벌어진 날에는 더 최근에 찍힌 쪽이 지금의 얘기다.
        bool lowSide = lowPct <= DeepLow && (!gained || input.lowAge <= input.highAge);

        // 1) 투매. 가장 깊은 상태라 먼저 본다.
        if (day <= CapitulationDay && fromLow <= CapitulationNearLow && input.r120 <= CapitulationTurn) { read.kind = ChatSituation.Capitulation; return read; }
        // 2) 폭락. 빠르기만 해서는 안 된다: 오늘 번 것의 2%를 6초 만에 돌려줘도 폭락이 아니다.
        if (fastDown && (!gained || retrace >= CrashShare || fromHigh <= CrashFromHigh)) { read.kind = ChatSituation.Crash; return read; }
        // 3) 저점 쪽: 크게 빠졌던 종목이 튀는 중이거나 아직 흘러내리는 중.
        if (lowSide && day < TrendDay)
        {
            if (fromLow >= BounceRise)
            {
                if (recovery >= RecoveryShare) { read.kind = ChatSituation.Recovery; return read; }
                if (day <= DeadCatDay) { read.kind = ChatSituation.DeadCatBounce; return read; }
                if (day <= -TrendDay) { read.kind = ChatSituation.DriftDown; return read; }
            }
            else if (day <= BleedDay && input.r120 < 0m) { read.kind = ChatSituation.SlowBleed; return read; }
            else if (day <= -TrendDay) { read.kind = ChatSituation.DriftDown; return read; }
        }
        // 4) 고점 쪽: 오늘 오른 종목. 기준가 아래로 오른 폭의 절반 넘게 더 내려갔으면 더는 "오른 종목"이 아니다.
        if (gained && retrace <= 1.5m)
        {
            if (highPct >= MonsterDay)
            {
                if (retrace < DipShare) { read.kind = fromHigh > MonsterNearHigh ? ChatSituation.MonsterRally : ChatSituation.MonsterDip; }
                else { read.kind = retrace < RoundTripShare ? ChatSituation.Pullback : ChatSituation.RoundTrip; }
                return read;
            }
            if (retrace >= RoundTripShare && fromHigh <= -RoundTripMinDrop) { read.kind = ChatSituation.RoundTrip; return read; }
            if (retrace >= DipShare && fromHigh <= -PullbackMinDrop) { read.kind = ChatSituation.Pullback; return read; }
            if (fastUp && fromHigh >= SurgeNearHigh) { read.kind = ChatSituation.Surge; return read; }
            if (input.breakout && input.r30 >= BreakoutSpeed && day >= BreakoutMinDay) { read.kind = ChatSituation.Breakout; return read; }
            if (day >= TrendDay) { read.kind = ChatSituation.DriftUp; return read; }
        }
        // 5) 나머지: 크지 않은 하루.
        if (fastUp && fromHigh >= SurgeNearHigh && day > DeadCatDay) { read.kind = ChatSituation.Surge; }
        else if (input.breakout && input.r30 >= BreakoutSpeed && day >= BreakoutMinDay) { read.kind = ChatSituation.Breakout; }
        else if (day <= BleedDay && fromLow <= BleedNearLow && input.r120 < 0m) { read.kind = ChatSituation.SlowBleed; }
        else if (day <= -TrendDay) { read.kind = ChatSituation.DriftDown; }
        else if (day >= TrendDay) { read.kind = ChatSituation.DriftUp; }
        // 장 초반처럼 고가와 저가가 아직 같으면 "횡보"라고 부를 구간이 없다.
        else if (Math.Abs(day) < FlatDay && range < FlatRange && high > low) { read.kind = ChatSituation.Flat; }
        else { read.kind = ChatSituation.Quiet; }
        return read;
    }

    /// <summary>말하는 사람의 처지. cost는 평균 단가(없거나 들고 있지 않으면 구경꾼).</summary>
    public static ChatSpeakerView View(decimal cost, decimal price, bool holds = true)
    {
        if (!holds || cost <= 0m || price <= 0m) { return ChatSpeakerView.Watcher; }
        decimal result = (price / cost - 1m) * 100m;
        return result >= DeepProfitPct ? ChatSpeakerView.DeepProfit : result >= ProfitPct ? ChatSpeakerView.Profit : result > -ProfitPct ? ChatSpeakerView.Even :
            result > DeepLossPct ? ChatSpeakerView.Loss : ChatSpeakerView.DeepLoss;
    }
    /// <summary>대사 묶음용 처지. 본전 근처는 부호로 가른다(조금이라도 위면 수익, 아래면 물림).</summary>
    public static ChatStance Stance(decimal cost, decimal price, bool holds = true)
        => !holds || cost <= 0m || price <= 0m ? ChatStance.Watch : price >= cost ? ChatStance.Gain : ChatStance.Loss;
    public static string StanceKey(ChatStance stance) => stance == ChatStance.Gain ? "gain" : stance == ChatStance.Loss ? "loss" : "watch";
}

/// <summary>
/// 종목 하나의 당일 기록: 장 전체의 고가·저가와 그 시각, 2초 간격으로 찍은 최근 2분의 값.
/// 차트 수다의 900초 기록이나 들쭉날쭉한 관찰 기록에 기대지 않고 여기서 따로 센다.
/// </summary>
public sealed class ChatSituationTrack
{
    public const double SampleSec = 2, CacheSec = 2;
    /// <summary>속도를 재는 두 창(초).</summary>
    public const double QuickSec = 30, SwingSec = 120;
    private const int RingSize = 61;   // 2분 = 60칸 + 지금
    /// <summary>고가가 이만큼(초) 버틴 뒤에 넘어야 돌파다. 넘은 뒤 이만큼(초) 동안 돌파 상황으로 본다.</summary>
    private const double BreakoutStandSec = 60, BreakoutHoldSec = 45;

    private readonly decimal[] ring = new decimal[RingSize];
    private int head, count;
    private double lastSampleAt = double.NaN, seenAt = double.NaN;
    public int Day = int.MinValue;
    public decimal High, Low, Last;
    public double HighAt, LowAt, StartAt;
    private double breakoutAt = double.NegativeInfinity; private decimal breakoutLevel;
    private ChatSituationRead cached; private double cachedAt = double.NaN;

    public void Reset(int day, double now)
    {
        head = count = 0; lastSampleAt = double.NaN; seenAt = double.NaN; Day = day; High = Low = Last = 0m; HighAt = LowAt = StartAt = now;
        breakoutAt = double.NegativeInfinity; breakoutLevel = 0m; cachedAt = double.NaN;
    }

    /// <summary>값을 본다. 고가·저가는 볼 때마다 고치고, 속도용 값은 2초에 한 칸씩만 찍는다(건너뛴 칸은 직전 값으로 메운다).</summary>
    public void Feed(double now, decimal price)
    {
        if (price <= 0m) { return; }
        if (High <= 0m) { High = Low = price; HighAt = LowAt = StartAt = now; }
        if (price > High)
        {
            // 한동안 아무도 못 넘던 고가를 넘었다.
            if (now - HighAt >= BreakoutStandSec && now - StartAt >= BreakoutStandSec) { breakoutAt = now; breakoutLevel = High; }
            High = price; HighAt = now;
        }
        if (price < Low) { Low = price; LowAt = now; }
        if (double.IsNaN(lastSampleAt)) { Push(price); lastSampleAt = now; }
        else
        {
            int pushed = 0;
            while (now - lastSampleAt >= SampleSec && pushed < RingSize)
            {
                lastSampleAt += SampleSec; pushed++;
                Push(now - lastSampleAt >= SampleSec ? Last : price);
            }
            if (pushed >= RingSize) { lastSampleAt = now; }
        }
        Last = price; seenAt = now;
    }
    private void Push(decimal price)
    {
        ring[head] = price; head = (head + 1) % RingSize;
        if (count < RingSize) { count++; }
    }
    /// <summary>seconds초 전의 값. 기록이 그 절반에도 못 미치면 false(속도를 말할 수 없다).</summary>
    private bool Ago(double seconds, out decimal price)
    {
        price = 0m;
        int wanted = (int)Math.Round(seconds / SampleSec), have = count - 1;
        if (have < 1 || have < wanted / 2) { return false; }
        int back = Math.Min(wanted, have);
        price = ring[((head - 1 - back) % RingSize + RingSize) % RingSize];
        return price > 0m;
    }
    /// <summary>시세판(게임이 보여 주는 당일 고가·저가)이 내가 못 본 체결까지 담고 있으면 그 값을 받는다.</summary>
    public void Widen(decimal boardHigh, decimal boardLow, double now)
    {
        if (boardHigh > High) { High = boardHigh; HighAt = now; }
        if (boardLow > 0m && (Low <= 0m || boardLow < Low)) { Low = boardLow; LowAt = now; }
    }
    public bool Stale(double now) => double.IsNaN(seenAt) || now < seenAt - 1 || now - seenAt > 60;

    public ChatSituationRead Read(decimal reference, double now)
    {
        // 2초 안에 다시 물으면 방금 읽은 것을 돌려준다. 기준가는 게임이 등락률에서 되짚어 주는 값이라 부를 때마다 끝자리가 조금씩 다르다.
        // 그 정도 차이(0.1% 안)는 같은 기준가로 본다.
        if (!double.IsNaN(cachedAt) && now >= cachedAt && now - cachedAt < CacheSec && cached.valid && Math.Abs(cached.reference - reference) <= cached.reference * .001m) { return cached; }
        var input = new ChatSituationInput { reference = reference, price = Last, high = High, low = Low, highAge = now - HighAt, lowAge = now - LowAt };
        if (Last > 0m && Ago(QuickSec, out decimal quick)) { input.r30 = (Last / quick - 1m) * 100m; }
        if (Last > 0m && Ago(SwingSec, out decimal swing)) { input.r120 = (Last / swing - 1m) * 100m; }
        input.breakout = now - breakoutAt <= BreakoutHoldSec && breakoutLevel > 0m && Last >= breakoutLevel;
        cached = ChatSituationMath.Classify(input); cachedAt = now;
        return cached;
    }
}

/// <summary>
/// 종목별 당일 기록의 보관소. 저장 파일에는 넣지 않는다(정적이다): 불러온 뒤에는 시세판의 당일 고가·저가로 다시 채운다.
/// </summary>
public static class ChatSituationBoard
{
    private static readonly Dictionary<Stock, ChatSituationTrack> tracks = new();
    private static readonly Dictionary<string, ChatSituationRead> latest = new(StringComparer.Ordinal);

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { tracks.Clear(); latest.Clear(); }

    /// <summary>지금 값을 보고 상황을 돌려준다. 날이 바뀌었거나 시계가 튀었으면(불러오기) 그 종목의 기록을 새로 시작한다.</summary>
    public static ChatSituationRead Observe(Stock stock, int day, double now, decimal reference, decimal boardHigh, decimal boardLow)
    {
        if (stock == null || stock.currentPrice <= 0m) { return default; }
        if (!tracks.TryGetValue(stock, out var track)) { tracks.Add(stock, track = new ChatSituationTrack()); track.Reset(day, now); }
        else if (track.Day != day || track.Stale(now)) { track.Reset(day, now); }
        track.Feed(now, stock.currentPrice);
        track.Widen(boardHigh, boardLow, now);
        var read = track.Read(reference, now);
        if (!string.IsNullOrEmpty(stock.symbol)) { latest[stock.symbol] = read; }
        return read;
    }
    /// <summary>마지막으로 읽은 상황(종목 기호로). 대사를 고르는 쪽(정적 함수)에서 쓴다.</summary>
    public static bool TryRead(string symbol, out ChatSituationRead read)
    {
        read = default;
        return !string.IsNullOrEmpty(symbol) && latest.TryGetValue(symbol, out read) && read.valid;
    }
}

/// <summary>
/// 상황에 따라 어떤 말이 되고 안 되는지의 표. 말이 나오는 모든 장치가 여기를 거친다.
/// 대사 묶음의 이름(key)만 다룬다. 문장은 언어별 파일에 있다.
/// </summary>
public static class ChatSituationRules
{
    /// <summary>모양(차트 수다)을 그대로 말할지(key), 상황 대사로 바꿔 말할지(Redirect), 입을 다물지(null).</summary>
    public const string Redirect = "sit";

    /// <summary>오르는 쪽 분위기인가.</summary>
    public static bool Rising(ChatSituation kind) => kind == ChatSituation.DriftUp || kind == ChatSituation.Breakout || kind == ChatSituation.Surge || kind == ChatSituation.MonsterRally;
    /// <summary>괴물 급등 중인가(고점 근처든 살짝 눌렸든).</summary>
    public static bool Monster(ChatSituation kind) => kind == ChatSituation.MonsterRally || kind == ChatSituation.MonsterDip;
    /// <summary>무너지는 중인가. 공포에 찬 말은 여기서만 나온다.</summary>
    public static bool Panic(ChatSituation kind) => kind == ChatSituation.Crash || kind == ChatSituation.Capitulation;
    /// <summary>내리는 쪽 분위기인가(급하든 아니든).</summary>
    public static bool Falling(ChatSituation kind) => Panic(kind) || kind == ChatSituation.SlowBleed || kind == ChatSituation.DriftDown || kind == ChatSituation.RoundTrip;
    /// <summary>아무 일도 없는가.</summary>
    public static bool Still(ChatSituation kind) => kind == ChatSituation.Quiet || kind == ChatSituation.Flat;

    /// <summary>
    /// 예전 대화 틀의 시세 상태(SPIKE·DUMP·HIGH·LOW·PULLBACK·REBOUND·UP·DOWN·FLAT). 언어별 대사 묶음이 없을 때만 이 이름의 번역 대사를 쓴다.
    /// 괴물 눌림은 UP이다(내리는 얘기가 아니다). 말할 것이 없으면 null.
    /// </summary>
    public static string LegacyState(ChatSituation kind) => kind switch
    {
        ChatSituation.Surge => "SPIKE", ChatSituation.Crash => "DUMP",
        ChatSituation.Breakout or ChatSituation.MonsterRally => "HIGH", ChatSituation.Capitulation => "LOW",
        ChatSituation.Pullback or ChatSituation.RoundTrip => "PULLBACK", ChatSituation.Recovery => "REBOUND",
        ChatSituation.DriftUp or ChatSituation.MonsterDip => "UP",
        ChatSituation.DriftDown or ChatSituation.SlowBleed or ChatSituation.DeadCatBounce => "DOWN",
        ChatSituation.Flat => "FLAT", _ => null
    };

    /// <summary>
    /// 가격이 한 계단(±5%) 움직였거나, 봇이 한동안 안 본 사이 값이 달라졌다. 이것이 말할 거리인가.
    /// 상황과 방향이 어긋나면 잡음이다: 괴물 급등 중의 한 계단 하락, 투매 중의 한 계단 반등, 아무 일 없는 종목의 출렁임.
    /// 말하게 되면 문장은 방향이 아니라 상황에 맞춰 고른다(괴물 눌림이면 느긋한 말).
    /// </summary>
    public static bool PriceEventMatters(ChatSituation kind, bool rising) => rising
        ? Rising(kind) || kind == ChatSituation.Recovery || kind == ChatSituation.DeadCatBounce
        : Falling(kind) || kind == ChatSituation.Pullback || kind == ChatSituation.MonsterDip;

    /// <summary>못 탄 사람의 조바심(FOMO)은 오르는 종목에서만 생긴다.</summary>
    public static bool FomoFits(ChatSituation kind) => Rising(kind) || kind == ChatSituation.MonsterDip || kind == ChatSituation.Recovery;

    /// <summary>급락 폭주(수십 명이 한꺼번에 비명)는 정말 무너질 때만 연다. 눌림은 차트 수다가 조용히 맡는다.</summary>
    public static bool CrashRunFits(ChatSituation kind) => Panic(kind) || kind == ChatSituation.RoundTrip;
    /// <summary>급등 폭주는 무너지는 종목의 잠깐 반등에서는 열지 않는다.</summary>
    public static bool SurgeRunFits(ChatSituation kind) => !Panic(kind) && kind != ChatSituation.DeadCatBounce && kind != ChatSituation.SlowBleed;

    private static readonly HashSet<string> DoomShapes = new(StringComparer.Ordinal) { "dump1", "dump2", "plunge", "bleed", "breakdown", "grindDown" };
    /// <summary>보유자 대사가 손실을 전제로 하는 모양("나 물린 거 맞지"). 수익 중인 보유자는 이 대사 대신 상황 대사를 한다.</summary>
    public static bool LossFlavored(string shape) => DoomShapes.Contains(shape) || shape == "wobble" || shape == "roundtrip" || shape == "deadcat" || shape == "dayLow" || shape == "doubleTop";
    /// <summary>보유자 대사가 수익을 전제로 하는 모양("오늘은 내가 승자다"). 물린 보유자는 이 대사 대신 상황 대사를 한다.</summary>
    public static bool GainFlavored(string shape) => shape == "dayHigh" || shape == "grindUp" || shape == "breakout" || shape == "bounce2" || shape == "bounce3";

    /// <summary>
    /// 차트 수다가 알아챈 모양을 지금 상황에서 어떻게 할까: 그대로 말한다(모양 이름), 상황 대사로 바꾼다(Redirect), 말하지 않는다(null).
    /// 모양은 최근 몇 분의 꺾임만 본다. 그 꺾임이 하루 전체에서 무엇인지는 상황이 안다.
    /// </summary>
    public static string ChartShape(string shape, ChatSituation kind)
    {
        switch (shape)
        {
            case "dump1": case "dump2": case "plunge": case "bleed":
                // 폭락 대사는 폭락에서만. 눌림·괴물 눌림·천천히 내리는 날에는 그 상황의 말로 바꾸고, 오르는 중이거나 조용하면 말하지 않는다.
                if (Panic(kind) || kind == ChatSituation.RoundTrip) { return shape; }
                if (shape == "bleed" && kind == ChatSituation.SlowBleed) { return shape; }
                return kind == ChatSituation.Pullback || kind == ChatSituation.MonsterDip || kind == ChatSituation.SlowBleed || kind == ChatSituation.DriftDown || kind == ChatSituation.DeadCatBounce ? Redirect : null;
            case "wobble":
                if (Monster(kind)) { return kind == ChatSituation.MonsterDip ? Redirect : null; }
                return Still(kind) || kind == ChatSituation.Capitulation ? null : shape;
            case "roundtrip":
                // "올린 거 다 뱉었네"는 하루 전체로도 돌려준 것이 클 때만 맞는 말이다(+80%인 종목의 잔파동 왕복은 아니다).
                return Falling(kind) || kind == ChatSituation.Pullback ? shape : null;
            case "breakdown": case "grindDown":
                if (kind == ChatSituation.MonsterDip || kind == ChatSituation.Pullback) { return Redirect; }
                // 오르는 종목이나 하루 전체로는 아무 일도 없는 종목에서 "지지 깨졌다"고 하지 않는다.
                return Rising(kind) || Still(kind) ? null : shape;
            case "dayLow":
                return Falling(kind) && kind != ChatSituation.RoundTrip ? shape : null;
            case "deadcat":
                return Falling(kind) || kind == ChatSituation.DeadCatBounce || kind == ChatSituation.Pullback ? shape : null;
            case "dayHigh":
                return Rising(kind) ? shape : null;
            case "bounce1":
                // "바닥 찍은 건가", "본전까지만 와라"는 괴물 급등 중의 잔눌림 뒤에는 맞지 않는다.
                return Monster(kind) ? null : shape;
            case "grindUp": case "breakout":
                return Panic(kind) || Still(kind) ? null : shape;
            case "flat":
                return Still(kind) ? shape : null;
            default:
                return shape;
        }
    }

    /// <summary>잔움직임(몇 초 사이 1%)에 어떤 투로 말할 수 있나.</summary>
    public enum TwitchMode { Normal, Calm, Wary, Silent }
    /// <summary>
    /// 살짝 밀림(spill): 크게 오른 종목(괴물, 급등·돌파 중, +20% 넘는 날)에서는 느긋한 말만 한다. "끝났다"는 없다.
    /// 무너지는 종목에서는 잔밀림을 따로 말하지 않는다(이미 더 큰 얘기가 돌고 있다).
    /// 살짝 오름(pop): 무너지는 종목에서는 시큰둥하거나 의심하는 말만 한다.
    /// </summary>
    public static TwitchMode Twitch(bool pop, in ChatSituationRead read)
    {
        var kind = read.kind;
        if (pop) { return Panic(kind) || kind == ChatSituation.SlowBleed || kind == ChatSituation.DeadCatBounce ? TwitchMode.Wary : TwitchMode.Normal; }
        if (Panic(kind) || kind == ChatSituation.SlowBleed || kind == ChatSituation.DriftDown) { return TwitchMode.Silent; }
        if (Monster(kind) || kind == ChatSituation.Surge || kind == ChatSituation.Breakout || kind == ChatSituation.DriftUp && read.tier >= ChatDayTier.Big) { return TwitchMode.Calm; }
        return TwitchMode.Normal;
    }

    /// <summary>장 마감 뒤 그 종목을 두고 할 얘기의 갈래. 종가만 보지 않고 장중 고가·저가를 같이 본다.</summary>
    public static string CloseKind(decimal reference, decimal high, decimal low, decimal close)
    {
        if (reference <= 0m || close <= 0m) { return null; }
        var read = ChatSituationMath.Classify(new ChatSituationInput { reference = reference, price = close, high = high, low = low, highAge = 0, lowAge = 1 });
        // 괴물로 끝났다: +100% 이상이고 오른 폭의 3할 5푼 넘게는 안 돌려줬다.
        if (read.dayPct >= ChatSituationMath.MonsterDay && read.retrace < ChatSituationMath.CrashShare) { return "MONSTER"; }
        // 장중에 +10% 넘게 갔다가 절반 넘게 돌려주고 끝났다.
        if (read.highPct >= 10m && read.retrace >= .5m && read.dayPct > -ChatSituationMath.TrendDay) { return "FADE"; }
        if (read.dayPct <= -20m) { return "CRASH"; }
        return null;
    }

    // ── 점검용 ──
    /// <summary>폭락·공포를 말하는 묶음인가. 괴물 눌림 같은 상황에서 이런 묶음이 열려 있으면 잘못이다.</summary>
    public static bool DoomKey(string key)
    {
        if (string.IsNullOrEmpty(key)) { return false; }
        if (key.StartsWith("chart.", StringComparison.Ordinal)) { string shape = key.Substring(6); return DoomShapes.Contains(shape) || shape == "dayLow" || shape == "deadcat" || shape == "roundtrip"; }
        if (key.StartsWith("hype.crash", StringComparison.Ordinal)) { return true; }
        if (key == "legacy.MKT_DUMP" || key == "legacy.MKT_LOW" || key == "legacy.MKT_PULLBACK" || key == "legacy.MKT_DOWN") { return true; }
        if (key == "event.PriceFall.panic" || key == "debate.bottom") { return true; }
        return key.StartsWith("market.Crash", StringComparison.Ordinal) || key.StartsWith("market.Capitulation", StringComparison.Ordinal) ||
            key.StartsWith("market.RoundTrip", StringComparison.Ordinal) || key.StartsWith("market.SlowBleed", StringComparison.Ordinal);
    }
    /// <summary>환호를 말하는 묶음인가. 투매 같은 상황에서 열려 있으면 잘못이다.</summary>
    public static bool EuphoriaKey(string key)
        => key == "chart.dayHigh" || key == "hype.surge" || key == "legacy.MKT_SPIKE" || key == "legacy.MKT_HIGH" || key == "debate.top" ||
            key != null && (key.StartsWith("market.Surge", StringComparison.Ordinal) || key.StartsWith("market.MonsterRally", StringComparison.Ordinal) || key.StartsWith("market.Breakout", StringComparison.Ordinal));
    /// <summary>잔움직임에 대한 호들갑·의심 묶음인가. 조용한 종목에서는 괜찮지만(그게 재미다) 크게 오른 종목의 잔밀림, 무너지는 종목의 잔반등에서는 열리면 안 된다.</summary>
    public static bool FussKey(string key) => key == "twitch.spill.fuss" || key == "twitch.spill.doubt" || key == "twitch.pop.fuss" || key == "twitch.pop.fomo";

    private static readonly string[] CheckedShapes = { "wobble", "dump1", "dump2", "roundtrip", "bleed", "plunge", "bounce1", "deadcat", "grindUp", "grindDown", "breakout", "breakdown", "dayHigh", "dayLow", "flat" };
    /// <summary>토론 주제별로 꺼내면 안 되는 상황(파일에 avoid가 없을 때의 기본값). "바닥이냐"는 괴물 급등에서, "고점이냐"는 투매에서 말이 안 된다.</summary>
    public static bool DebateFits(string topic, ChatSituation kind) => topic switch
    {
        "bottom" => !Rising(kind) && !Monster(kind) && !Still(kind),
        "top" => !Falling(kind) && !Still(kind) && kind != ChatSituation.DeadCatBounce,
        // "지금 따라 타도 됨?"은 달리는 종목(과 그 눌림, 되살아나는 종목)에서만 묻는다.
        "chase" => Rising(kind) || kind == ChatSituation.MonsterDip || kind == ChatSituation.Pullback || kind == ChatSituation.Recovery,
        "takeProfit" => !Panic(kind) && kind != ChatSituation.SlowBleed && kind != ChatSituation.DriftDown && kind != ChatSituation.DeadCatBounce,
        _ => true
    };

    /// <summary>
    /// 이 상황에서, 이 처지의 사람에게 열려 있는 모든 대사 묶음의 이름. 실제 게임은 이 가운데 그때 조건이 맞은 것만 쓴다.
    /// 점검 도구가 "괴물 눌림에 폭락 묶음이 열려 있지 않은가"를 볼 때 쓴다.
    /// </summary>
    public static List<string> EligibleKeys(in ChatSituationRead read, ChatStance stance)
    {
        var keys = new List<string>(); var kind = read.kind;
        if (kind != ChatSituation.Quiet) { keys.Add("market." + kind + "." + ChatSituationMath.StanceKey(stance)); }
        string legacy = LegacyState(kind);
        if (legacy != null) { keys.Add("legacy.MKT_" + legacy); }
        foreach (string shape in CheckedShapes)
        {
            string verdict = ChartShape(shape, kind);
            if (verdict != null && verdict != Redirect) { keys.Add("chart." + shape); }
        }
        if (CrashRunFits(kind)) { keys.Add("hype.crash"); }
        if (SurgeRunFits(kind) && (Rising(kind) || kind == ChatSituation.Recovery)) { keys.Add("hype.surge"); }
        var spill = Twitch(false, read); var pop = Twitch(true, read);
        if (spill == TwitchMode.Normal) { keys.Add("twitch.spill.fuss"); keys.Add("twitch.spill.doubt"); keys.Add("twitch.spill.dry"); }
        else if (spill == TwitchMode.Calm) { keys.Add("twitch.spill.calm"); keys.Add("twitch.spill.dry"); }
        if (pop == TwitchMode.Normal) { keys.Add("twitch.pop.fuss"); keys.Add("twitch.pop.fomo"); keys.Add("twitch.pop.dry"); }
        else if (pop == TwitchMode.Wary) { keys.Add("twitch.pop.doubt"); keys.Add("twitch.pop.dry"); }
        if (PriceEventMatters(kind, true)) { keys.Add("event.PriceRise"); }
        if (PriceEventMatters(kind, false)) { keys.Add(Panic(kind) || kind == ChatSituation.RoundTrip || kind == ChatSituation.SlowBleed ? "event.PriceFall.panic" : "event.PriceFall.calm"); }
        foreach (string topic in new[] { "top", "bottom", "chase", "takeProfit" }) { if (DebateFits(topic, kind)) { keys.Add("debate." + topic); } }
        return keys;
    }
}

/// <summary>
/// 지어낸 가격 흐름으로 판정을 돌려 보는 순수 함수. 게임·봇·채팅은 건드리지 않는다(에디터 점검 도구와 같은 계산을 쓴다).
/// </summary>
public static class ChatSituationSim
{
    public struct Step { public double at; public ChatSituationRead read; }
    public sealed class Result
    {
        /// <summary>상황이 바뀐 순간들(처음 포함).</summary>
        public readonly List<Step> timeline = new();
        public ChatSituationRead final;
        public ChatSpeakerView view; public ChatStance stance;
        /// <summary>마지막 상황에서 이 사람에게 열려 있는 대사 묶음.</summary>
        public List<string> keys;
        /// <summary>지나온 상황을 한 줄로("Surge > MonsterRally > MonsterDip").</summary>
        public string Trail()
        {
            var names = new List<string>(); foreach (var step in timeline) { names.Add(step.read.kind.ToString()); }
            return string.Join(" > ", names);
        }
    }

    /// <summary>
    /// reference: 기준가(전일 종가). path: (그 값까지 가는 데 걸리는 초, 값)의 나열이고 사이는 곧게 잇는다. 첫 점의 값에서 시작한다.
    /// cost: 말하는 사람의 평균 단가(null이면 구경꾼).
    /// </summary>
    public static Result Run(decimal reference, (double seconds, decimal price)[] path, decimal? cost = null)
    {
        var result = new Result(); var track = new ChatSituationTrack(); track.Reset(1, 0);
        double now = 0; decimal price = path[0].price;
        track.Feed(now, price); Note(result, track.Read(reference, now), now);
        for (int i = 1; i < path.Length; i++)
        {
            decimal from = price; int steps = Math.Max(1, (int)Math.Round(path[i].seconds / ChatSituationTrack.SampleSec));
            for (int step = 1; step <= steps; step++)
            {
                now += ChatSituationTrack.SampleSec; price = from + (path[i].price - from) * step / steps;
                track.Feed(now, price); Note(result, track.Read(reference, now), now);
            }
        }
        result.final = track.Read(reference, now + ChatSituationTrack.CacheSec);
        result.view = cost.HasValue ? ChatSituationMath.View(cost.Value, price) : ChatSpeakerView.Watcher;
        result.stance = cost.HasValue ? ChatSituationMath.Stance(cost.Value, price) : ChatStance.Watch;
        result.keys = ChatSituationRules.EligibleKeys(result.final, result.stance);
        return result;
    }
    private static void Note(Result result, ChatSituationRead read, double at)
    {
        if (result.timeline.Count == 0 || result.timeline[result.timeline.Count - 1].read.kind != read.kind) { result.timeline.Add(new Step { at = at, read = read }); }
    }
}
