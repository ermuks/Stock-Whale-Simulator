using System;
using System.Collections.Generic;
using UnityEngine;

// 채팅의 영향력. 봇이 어떤 종목 얘기를 하면 그 말에는 보이지 않는 영향력(0~100%)이 붙고,
// 그 방에서 그 말을 본 다른 봇들이 영향력만큼 그 종목으로 눈을 돌린다.
// "노보손해보험 뭔가 곧 쏠 거 같은데..." 한 줄이 97%로 붙으면 방의 절반이 그 종목을 들여다보고, 3%로 붙으면 아무도 신경 쓰지 않는다.
//
// 영향력은 화면에 보여 주지 않는다. 눈을 돌린다는 것은 한동안 그 종목을 더 자주 보고(TradeBot.PickStock) 사는 쪽으로 조금 기운다(BotMind.BuyBias)는 뜻이다.
// 사고팔지는 각자의 판단이다. 주문을 대신 내거나 믿는 값을 바꾸지 않는다.
// 봇이 한 말만 센다. 실제 시청자 채팅과 플레이어의 말은 이 길을 지나지 않는다.
public sealed partial class InvestorSociety
{
    /// <summary>눈을 돌린 채로 있는 시간: 영향력이 0이면 이만큼, 100%면 여기에 InfluenceExtraSec를 더한 만큼.</summary>
    private const float InfluenceBaseSec = 20f, InfluenceExtraSec = 70f;
    /// <summary>듣는 봇이 넘어갈 확률 = 영향력 × (이 값 + 휩쓸림 × InfluenceSuggestible).</summary>
    private const float InfluenceListenerBase = .12f, InfluenceSuggestible = .6f;
    private readonly Queue<long> influenceSeen = new();
    private readonly HashSet<long> influenceSeenSet = new();
    /// <summary>확인용: 영향력이 붙은 말의 수, 눈을 돌린 봇의 누계, 최근 기록.</summary>
    public int InfluenceMessages { get; private set; }
    public long InfluenceTurned { get; private set; }
    public readonly Queue<string> InfluenceTrace = new(64);

    /// <summary>
    /// 이 말의 영향력(0~1). 대부분의 말은 약하고, 가끔 한 줄이 크게 먹힌다(운이 세제곱으로 들어간다).
    /// 말하는 사람이 사교적이고 허세가 있을수록, 그 종목을 실제로 들고 있을수록, 수익 인증·타점 자랑처럼 증거가 붙을수록 세다.
    /// 폭주 구간에 쏟아지는 짧은 감탄사는 약하다.
    /// </summary>
    private static float InfluenceOf(TradeBot speaker, SocialMessage message, Stock stock)
    {
        var traits = speaker.Social.Psychology.Traits;
        float luck = (CompanyBusiness.Hash("INFLUENCE:" + message.id + ":" + speaker.Social.Id) % 100000u) / 99999f;
        float voice = traits.Sociability / 100f * .5f + traits.Bravado / 100f * .5f;
        float value = .04f + luck * luck * luck * .62f + voice * .22f;
        if (speaker.GetOwnedVolume(stock) > 0m) { value += .06f; }
        string key = message.templateKey ?? "";
        if (message.profitCard != null || message.act == SocialAct.BRAG) { value += .14f; }
        if (key.StartsWith("chart.sniper", StringComparison.Ordinal) || key.StartsWith("chart.topSell", StringComparison.Ordinal)) { value += .12f; }
        if (key.StartsWith("hype.", StringComparison.Ordinal) && (message.text == null || message.text.Length <= 6)) { value *= .35f; }
        if (key.EndsWith("Reply", StringComparison.Ordinal)) { value *= .5f; }
        return Mathf.Clamp01(value);
    }

    /// <summary>화면에 나가는 봇의 말 하나를 본다. 종목 얘기면 영향력을 매기고 그 방의 봇들에게 적용한다.</summary>
    private void ApplyChatInfluence(TradeBot speaker, SocialMessage message)
    {
        if (speaker == null || speaker.Social == null || message == null || message.facts == null || string.IsNullOrEmpty(message.facts.symbol) || StockManager.Instance == null) { return; }
        // 끊어 친 말은 조각마다 번호가 다르게 지나간다. 같은 사람이 같은 시각에 같은 대사로 한 말은 한 번만 센다.
        long once = unchecked((long)CompanyBusiness.Hash(message.senderId + "|" + message.templateKey + "|" + message.realAt.ToString("R")) << 20 ^ message.senderId);
        if (!influenceSeenSet.Add(once)) { return; }
        influenceSeen.Enqueue(once);
        while (influenceSeen.Count > 256) { influenceSeenSet.Remove(influenceSeen.Dequeue()); }
        Stock stock = null;
        foreach (var candidate in StockManager.Instance.stocks) { if (candidate != null && candidate.symbol == message.facts.symbol) { stock = candidate; break; } }
        if (stock == null || !stock.AcceptsOrders) { return; }
        float influence = InfluenceOf(speaker, message, stock);
        float now = SessionClock.Time, until = now + InfluenceBaseSec + influence * InfluenceExtraSec;
        int turned = 0, heard = 0;
        foreach (var listener in socialBots)
        {
            if (listener == speaker || listener.Social == null || !listener.isActiveAndEnabled || !Present(listener, message.roomId)) { continue; }
            var mind = listener.Social.Psychology;
            // 채팅을 보고 있지 않은 봇은 듣지 못한다. 공포에 질린 봇은 남의 말이 들리지 않는다.
            if (!mind.ActiveAt(now) || mind.HasPanic) { continue; }
            heard++;
            float chance = influence * (InfluenceListenerBase + mind.Traits.Suggestibility / 100f * InfluenceSuggestible);
            if (UnityEngine.Random.value >= chance) { continue; }
            // 이미 더 센 말에 끌려 다른 종목을 보고 있으면 그대로 둔다.
            if (listener.Social.InfluencedStock != null && listener.Social.InfluencedStock != stock && now < listener.Social.InfluenceUntil && listener.Social.InfluenceUntil > until) { continue; }
            listener.Social.InfluencedStock = stock; listener.Social.InfluenceUntil = until; turned++;
        }
        InfluenceMessages++; InfluenceTurned += turned;
        if (influence >= .5f || turned >= 20)
        {
            InfluenceTrace.Enqueue(speaker.Social.Nickname + ": " + message.text + "  (영향력 " + (influence * 100f).ToString("0.000") + "%, " + stock.symbol + ", 들은 봇 " + heard + ", 눈 돌린 봇 " + turned + ")");
            while (InfluenceTrace.Count > 40) { InfluenceTrace.Dequeue(); }
        }
    }
}
