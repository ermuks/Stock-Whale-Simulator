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

    private readonly Dictionary<Stock, MarketTrail> marketTrails = new();

    // 최근 움직임은 5~15분 전에 본 가격과 비교한다. 그보다 오래된 가격은 버린다.
    private const double TrailSampleMinutes = 1, TrailNearMinutes = 5, TrailFarMinutes = 15;
    private const decimal FastMovePercent = 2.5m, ExtremeDayPercent = 3m, ExtremeGapPercent = .4m, SwingPercent = 3m;
    private const decimal TrendPercent = 1.5m, FlatDayPercent = .7m, FlatRangePercent = 1.5m;

    private static string SignedPercent(decimal value) => value.ToString("+0.0;-0.0;0.0", SocialCulture) + "%";

    private void CaptureMarketScene(Stock stock, SocialFacts facts)
    {
        if (stock == null || stock.currentPrice <= 0m)
        { return; }

        double now = SocialTime.GameMinute;
        decimal price = stock.currentPrice;
        if (!marketTrails.TryGetValue(stock, out var trail))
        {
            trail = new MarketTrail();
            marketTrails.Add(stock, trail);
        }

        // 날이 바뀌었거나 시간이 되돌아갔으면(불러오기) 이전 관찰은 버린다.
        if (now < trail.last || Math.Floor(now / 1440d) != Math.Floor(trail.last / 1440d))
        { trail.samples.Clear(); }

        while (trail.samples.Count > 0 && now - trail.samples.Peek().minute > TrailFarMinutes)
        { trail.samples.Dequeue(); }

        decimal fast = 0m;
        bool hasFast = false;
        if (trail.samples.Count > 0 && now - trail.samples.Peek().minute >= TrailNearMinutes && trail.samples.Peek().price > 0m)
        {
            fast = (price / trail.samples.Peek().price - 1m) * 100m;
            hasFast = true;
        }

        if (trail.samples.Count == 0 || now - trail.last >= TrailSampleMinutes)
        {
            trail.samples.Enqueue((now, price));
            trail.last = now;
        }

        if (ReadMarketDay == null)
        { return; }

        MarketDay day = ReadMarketDay(stock);
        if (day.reference <= 0m || day.high <= 0m || day.low <= 0m)
        { return; }

        decimal high = Math.Max(day.high, price), low = Math.Min(day.low, price);
        decimal dayPercent = (price / day.reference - 1m) * 100m;
        decimal fromHigh = (price / high - 1m) * 100m, fromLow = (price / low - 1m) * 100m;
        decimal highPercent = (high / day.reference - 1m) * 100m, lowPercent = (low / day.reference - 1m) * 100m;
        decimal range = (high - low) / day.reference * 100m;

        string state = null;
        if (hasFast && fast >= FastMovePercent) { state = "SPIKE"; }
        else if (hasFast && fast <= -FastMovePercent) { state = "DUMP"; }
        else if (dayPercent >= ExtremeDayPercent && fromHigh >= -ExtremeGapPercent) { state = "HIGH"; }
        else if (dayPercent <= -ExtremeDayPercent && fromLow <= ExtremeGapPercent) { state = "LOW"; }
        else if (highPercent >= SwingPercent && fromHigh <= -SwingPercent) { state = "PULLBACK"; }
        else if (lowPercent <= -SwingPercent && fromLow >= SwingPercent) { state = "REBOUND"; }
        else if (dayPercent >= TrendPercent) { state = "UP"; }
        else if (dayPercent <= -TrendPercent) { state = "DOWN"; }
        // 장 초반처럼 고가와 저가가 아직 같으면 "구간"이라고 부를 것이 없다. 그때는 횡보 얘기를 하지 않는다.
        else if (Math.Abs(dayPercent) < FlatDayPercent && range < FlatRangePercent && Money(high) != Money(low)) { state = "FLAT"; }

        if (state == null)
        { return; }

        facts.marketState = state;
        facts.dayPct = SignedPercent(dayPercent);
        facts.dayHigh = Money(high);
        facts.dayLow = Money(low);
        facts.fastPct = hasFast ? SignedPercent(fast) : null;
        facts.fromHigh = SignedPercent(fromHigh);
        facts.fromLow = SignedPercent(fromLow);
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
