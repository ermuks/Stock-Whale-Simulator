using System;

/// <summary>
/// 개미가 차트를 읽는 숙련도와, 관찰에서 생기는 투매 충동.
/// 숙련도는 직접 체결 경험으로만 오르고 소문·목격으로는 오르지 않는다.
/// </summary>
public sealed partial class InvestorPsychology
{
    /// <summary>0이면 눈앞의 가격만 보고, 1이면 최근 봉 흐름과 과열을 읽는다.</summary>
    public float ChartSkill { get; private set; }
    public float ChartSkillCap { get; private set; } = 1f;
    public int ChartLessons { get; private set; }
    private double nextLesson;
    public string PanicSymbol;
    public double PanicUntil, PanicCooldownUntil;
    public bool HasPanic => PanicSymbol != null && ChatSocialAudit.GameMinute < PanicUntil;
    public bool HasPanicFor(Stock stock) => HasPanic && stock != null && stock.symbol == PanicSymbol;

    public void InitializeChartSkill(float start, float cap)
    {
        ChartSkillCap = Math.Max(0f, Math.Min(1f, cap));
        ChartSkill = Math.Max(0f, Math.Min(ChartSkillCap, start));
    }

    public void RestoreChartSkill(float skill, int lessons)
    {
        if (float.IsNaN(skill) || float.IsInfinity(skill) || skill <= 0f)
        { return; }

        ChartSkill = Math.Max(0f, Math.Min(ChartSkillCap, skill));
        ChartLessons = Math.Max(0, lessons);
    }

    /// <summary>실현한 매매 한 건에서 배운다. 손실이 수익보다 더 많이 가르친다.</summary>
    public void LearnFromTrade(double returnPercent)
    {
        if (double.IsNaN(returnPercent) || double.IsInfinity(returnPercent) || Math.Abs(returnPercent) < .5)
        { return; }

        // 한 번의 매도가 여러 체결로 쪼개져도 교훈은 한 번이다.
        if (ChatSocialAudit.GameMinute < nextLesson)
        { return; }

        nextLesson = ChatSocialAudit.GameMinute + 5;

        float gain = returnPercent < 0 ? .012f + (float)Math.Min(.02, -returnPercent * .001) : .004f;
        ChartSkill = Math.Min(ChartSkillCap, ChartSkill + gain * (1f - ChartSkill * .5f));
        ChartLessons++;
    }

    /// <summary>보유 종목의 하락을 직접 본 봇만 호출한다. 확률은 호출자가 그 봇의 성향으로 계산한다.</summary>
    public bool TryPanic(string symbol, double gameMinute, double chance, double roll, double minutes)
    {
        if (string.IsNullOrEmpty(symbol) || gameMinute < PanicUntil || gameMinute < PanicCooldownUntil)
        { return false; }

        if (roll >= Math.Max(0, Math.Min(1, chance)))
        { return false; }

        PanicSymbol = symbol;
        PanicUntil = gameMinute + Math.Max(1, minutes);
        PanicCooldownUntil = PanicUntil + Math.Max(1, minutes) * 2;
        Fear = Math.Min(100f, Fear + 12f);
        LastCause = "하락 흐름 목격: " + symbol + " 투매 충동";
        ChatSocialAudit.Journal.Event(new SocialEvent { time = gameMinute, actor = SocialId, kind = "PANIC_START", reason = symbol, value = PanicUntil });
        return true;
    }

    public void EndPanic()
    {
        PanicUntil = Math.Min(PanicUntil, ChatSocialAudit.GameMinute);
    }
}
