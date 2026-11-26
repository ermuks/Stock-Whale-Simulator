using System;
using System.Collections.Generic;

/// <summary>
/// 누구나 화면에서 볼 수 있는 시세 사실을 말에 담는다: 전일 대비 등락, 당일 고가·저가, 최근 몇 분의 움직임.
/// 실제 가치나 다른 사람의 계좌는 읽지 않는다. 가격과 당일 고가·저가만 쓴다.
/// </summary>
public sealed partial class InvestorSociety
{
    public struct MarketDay
    {
        /// <summary>0% 기준가(전일 종가, 첫날은 당일 시가).</summary>
        public decimal reference;
        public decimal high;
        public decimal low;
    }

    /// <summary>게임 쪽에서 연결한다. 연결하지 않으면 시세 사실 없이 말한다.</summary>
    public static Func<Stock, MarketDay> ReadMarketDay;

    private sealed class MarketTrail
    {
        public readonly Queue<(double minute, decimal price)> samples = new();
        public double last = double.MinValue;
    }

    // 예전에는 여기서 따로 찍은 5~15분 전의 값과 비교해 급등·급락을 정했다. 지금은 상황 판정(ChatSituation)이 맡는다.
    // 이 필드는 쓰지 않지만 지우지 않는다: 저장 파일에 이 이름이 들어 있어서, 없애면 예전 저장을 불러올 수 없다.
    private readonly Dictionary<Stock, MarketTrail> marketTrails = new();

    /// <summary>부호와 %가 붙은 등락률("+3.2%", "+490%"). 모든 대사의 {dayPct}·{fromHigh}·{fromLow}가 이 꼴이다. 대사 쪽에서 %를 또 붙이지 않는다.</summary>
    private static string SignedPercent(decimal value) => ChatSituationMath.SignedPercent(value);

    /// <summary>
    /// 말에 담을 시세 사실을 적는다. 무엇이 "말할 만한 상황"인지는 상황 판정이 정한다(ChatSituation.cs):
    /// 몇 분 사이에 5% 빠졌다는 것만으로 급락이라 하지 않고, 오늘 오른 폭의 몇 할을 돌려줬는지까지 본다.
    /// +500%에서 +490%가 된 종목은 여기서 UP(여전히 오른 종목)이지 DUMP가 아니다.
    /// </summary>
    private void CaptureMarketScene(Stock stock, SocialFacts facts)
    {
        if (stock == null || stock.currentPrice <= 0m)
        { return; }

        var read = SituationOf(stock);
        if (!read.valid)
        { return; }

        string state = ChatSituationRules.LegacyState(read.kind);
        if (state == null)
        { return; }

        facts.situation = read.kind.ToString();
        facts.marketState = state;
        facts.dayPct = SignedPercent(read.dayPct);
        facts.dayHigh = Money(read.high);
        facts.dayLow = Money(read.low);
        // 급등·급락 대사만 쓰는 값이다(최근 2분의 움직임). 그 상황일 때는 늘 채워져 있다.
        facts.fastPct = state == "SPIKE" || state == "DUMP" ? SignedPercent(Math.Abs(read.r120) >= Math.Abs(read.r30) ? read.r120 : read.r30) : null;
        facts.fromHigh = SignedPercent(read.fromHigh);
        facts.fromLow = SignedPercent(read.fromLow);
    }

    private static string MarketDirection(string state) => state switch
    {
        "SPIKE" or "HIGH" or "UP" or "REBOUND" => "RISE",
        "DUMP" or "LOW" or "DOWN" or "PULLBACK" => "FALL",
        "FLAT" => "FLAT",
        _ => null
    };

    /// <summary>
    /// 이 말에 시세 사실을 담을 수 있으면 대사 묶음의 이름을 돌려준다.
    /// 혼잣말은 자기가 본 시세를, 답글은 상대가 말한 종목의 지금 시세를 받는다.
    /// </summary>
    private static string MarketSpeechContext(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        var facts = message.facts;
        if (facts == null || string.IsNullOrEmpty(facts.marketState) || !string.IsNullOrEmpty(facts.notice))
        { return null; }

        string direction = MarketDirection(facts.marketState);
        string context = null;
        if (parent == null && message.targetId == 0)
        {
            if (message.act == SocialAct.REACT) { context = facts.marketState; }
            else if (message.act == SocialAct.DOUBT || message.act == SocialAct.ASK) { context = direction; }
        }
        else if (parent != null && parent.facts != null && parent.facts.symbol == facts.symbol &&
            parent.debug != null && parent.debug.speechKey != null && parent.debug.speechKey.StartsWith("community.MKT_", StringComparison.Ordinal) &&
            MarketDirection(parent.facts.marketState) == direction)
        {
            // 상대가 시세를 말했고 내가 보는 방향도 같을 때만 그 말을 받는다. 상대가 무슨 뜻으로 말했는지 모르는 말에는 시세로 맞장구치거나 반박하지 않는다.
            bool observed = parent.act == SocialAct.REACT && (message.act == SocialAct.AGREE || message.act == SocialAct.REBUT);
            bool asked = (parent.act == SocialAct.ASK || parent.act == SocialAct.DOUBT) && message.act == SocialAct.ANSWER;
            if (observed || asked) { context = direction; }
        }

        if (context == null)
        { return null; }

        return "MKT_" + context + (template.style == "formal" ? "_POLITE" : "");
    }

    /// <summary>시세를 말하는 대사 묶음. 없거나 채울 수 없는 사실이 있으면 null.</summary>
    private static SocialCommunityPhrases MarketGroup(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        string context = MarketSpeechContext(template, message, parent);
        return context == null ? null : ChatSocialAssets.Community(message.act, context);
    }

    /// <summary>시세를 말할 수 있을 때 네 번 중 세 번은 시세를 말한다. 난수열은 쓰지 않는다.</summary>
    private static bool PrefersMarketLine(SocialMessage message) => CompanyBusiness.Hash("MKT:" + message.id) % 100 < 75;
}
