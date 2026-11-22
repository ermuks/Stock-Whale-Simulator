using System;
using System.Collections.Generic;
using UnityEngine;

// 인지도(네임드)와 "내 말 따라 사기".
//
// 세력이 되려면 사람들이 나를 알아야 한다. 이름이 알려질수록 일반 채팅에서 던진 한마디에도 사람들이 움직인다.
//
// 인지도(0~100)는 방 전체가 나를 얼마나 아는가다. 봇마다 따로 갖는 믿음(Trust)·호감(Fondness)과는 다르다.
//   오른다: 인증한 수익(클수록), 맞아떨어진 예측, 내 추천을 따라 산 사람들이 번 것, 꾸준히 말 섞기(하루에 조금만).
//   내린다: 빗나간 예측(조금), 거짓 자랑.
//   단계: 뉴비(0~15) → 얼굴은 아는 사람(15~40) → 네임드(40~70) → 방의 큰손(70~). 단계가 오르면 누군가 한마디 한다.
//
// 따라 사기: 내가 종목을 사라고 하면("산삼전자 간다", "두덕리 사라", "이거 추천함") 방에 있던 봇 가운데 일부가 실제로 산다.
//   따를 확률 = (인지도 + 그 봇의 호감 + 그 봇의 믿음) × 귀가 얇은 정도. 나를 싫어하는 봇, 그 종목을 비싸다고 보는 봇은 잘 따르지 않는다.
//   인지도도 호감도 없으면 거의 아무도 따르지 않는다. 팔라고 하면 들고 있던 봇 가운데 일부가 판다.
//   따른 봇은 실제 매매를 한다(약속한 매매로 처리된다). 그 가운데 몇은 "따라 탄다"고 말한다.
//   결과가 나오면(예측이 맞았는지) 따른 봇들의 마음이 크게 움직인다: 벌었으면 호감·믿음이 크게 오르고, 물렸으면 원망한다.
public sealed partial class InvestorSociety
{
    /// <summary>방 전체가 나를 얼마나 아는가(0~100).</summary>
    public float PlayerRenown { get; private set; }
    /// <summary>확인용: 내 말을 따라 매매하기로 한 봇의 수(누적).</summary>
    public int PlayerFollowers { get; private set; }
    private int renownDay = -1; private float renownToday;
    private readonly Dictionary<long, List<int>> callFollowers = new();
    private static readonly string[] RenownTiers = { "newbie", "known", "named", "star" };
    public static int RenownTier(float renown) => renown >= 70f ? 3 : renown >= 40f ? 2 : renown >= 15f ? 1 : 0;
    public string PlayerRenownTier => RenownTiers[RenownTier(PlayerRenown)];

    /// <summary>인지도를 올리거나 내린다. 단계가 오르면 누군가 알아본다.</summary>
    private void GainRenown(float amount, string cause, int room, long replyTo, double now)
    {
        if (amount == 0f) { return; }
        int before = RenownTier(PlayerRenown); float was = PlayerRenown;
        PlayerRenown = Mathf.Clamp(PlayerRenown + amount, 0f, 100f);
        TraceTalk("    (인지도 " + was.ToString("0.0") + " → " + PlayerRenown.ToString("0.0") + ": " + cause + ")");
        int after = RenownTier(PlayerRenown);
        if (after > before) { Plan(room, "renown." + RenownTiers[after], null, replyTo, now + 2.2, 0, after >= 2 ? 2 : 1, 1f, sure: true); }
    }
    /// <summary>그냥 말을 섞는 것만으로도 조금씩 알려진다. 하루에 얻는 양에는 한도가 있다.</summary>
    private void RenownFromChatting(int room, long id, double now)
    {
        if (day == null) { return; }
        if (renownDay != day.Day) { renownDay = day.Day; renownToday = 0f; }
        if (renownToday >= 2f) { return; }
        renownToday += .12f; GainRenown(.12f, "채팅에 얼굴 비침", room, id, now);
    }

    /// <summary>
    /// 내가 종목을 사라고(팔라고) 했다. 방에 있던 봇 가운데 일부가 따른다. 따르기로 한 봇의 수를 돌려준다.
    /// strength는 말의 세기(확신에 찬 추천 1, 지나가는 말 0.5).
    /// </summary>
    private int FollowPlayerCall(int room, Stock stock, bool buy, long id, double now, float strength)
    {
        if (stock == null || !stock.AcceptsOrders || day == null || day.Phase != MarketPhase.Trading) { return 0; }
        float renown = PlayerRenown / 100f; var followers = new List<int>(); var speakers = new List<TradeBot>();
        Hearers(room, (bot, mind) =>
        {
            if (!bot.CanMakeSocialPromise || mind.PromisedAction != SocialPromisedAction.None || mind.HasPanic) { return; }
            if (!buy && bot.GetOwnedVolume(stock) <= 0m) { return; }
            // 나를 싫어하거나 내게 데인 봇은 따르지 않는다.
            if (mind.Quadrant == InvestorQuadrant.Hostile || mind.Betrayal >= 40f || mind.Fondness <= -20f) { return; }
            float chance = (renown * .22f + Mathf.Max(0f, mind.Fondness) / 100f * .22f + Mathf.Max(0f, mind.Trust) / 100f * .26f) * (.35f + mind.Traits.Suggestibility / 100f * .9f) * strength;
            // 자기 생각과 반대면(사라는데 비싸다고 본다, 팔라는데 싸다고 본다) 잘 안 따른다.
            decimal believed = bot.BelievedPrice(stock), price = stock.currentPrice;
            if (buy ? believed < price * .97m : believed > price * 1.03m) { chance *= .35f; }
            if (UnityEngine.Random.value >= Mathf.Min(.6f, chance)) { return; }
            mind.PromiseTrade(buy ? SocialPromisedAction.Buy : SocialPromisedAction.Sell, stock.symbol, id);
            bot.Social.InfluencedStock = stock; bot.Social.InfluenceUntil = SessionClock.Time + 40f;
            followers.Add(bot.Social.Id);
            if (mind.Traits.Politeness < Config.Int("styleFormalMin")) { speakers.Add(bot); }
        });
        if (followers.Count == 0) { TraceTalk("    (내 " + (buy ? "매수" : "매도") + " 추천을 따른 봇 없음 · 인지도 " + PlayerRenown.ToString("0") + ")"); return 0; }
        PlayerFollowers += followers.Count; callFollowers[id] = followers;
        while (callFollowers.Count > 6) { long oldest = long.MaxValue; foreach (long key in callFollowers.Keys) { if (key < oldest) { oldest = key; } } callFollowers.Remove(oldest); }
        TraceTalk("    (내 " + (buy ? "매수" : "매도") + " 추천을 따르기로 한 봇 " + followers.Count + "명 · 인지도 " + PlayerRenown.ToString("0") + ")");
        // 따른 봇 가운데 한둘은 따라 탄다고 말한다.
        int said = Math.Min(speakers.Count, followers.Count >= 4 ? 2 : 1);
        for (int i = 0; i < said; i++)
        {
            int index = HypeRandom.Range(0, speakers.Count); var bot = speakers[index]; speakers.RemoveAt(index);
            Plan(room, buy ? "follow.buy" : "follow.sell", stock, id, now + 2.0 + i * 1.6, bot.Social.Id, 1, 1f, sure: true);
        }
        return followers.Count;
    }
    /// <summary>내 예측의 결과가 나왔다. 그 말을 따라 매매한 봇들의 마음이 크게 움직이고, 인지도도 따라 움직인다.</summary>
    private void SettleFollowers(long messageId, bool right, int room, double now)
    {
        GainRenown(right ? 4f : -1.5f, right ? "예측 적중" : "예측 빗나감", room, messageId, now);
        if (!callFollowers.TryGetValue(messageId, out var followers)) { return; }
        callFollowers.Remove(messageId); int still = 0;
        foreach (int id in followers)
        {
            if (!SocialBots.TryGetValue(id, out var bot) || bot == null || bot.Social == null) { continue; }
            var mind = bot.Social.Psychology; still++;
            if (right) { mind.WarmToPlayer(6f, "플레이어를 따라 사서 범"); mind.ReactToPlayerTalk(8f, 0f, "플레이어를 따라 사서 범"); }
            else { mind.WarmToPlayer(-7f, "플레이어를 따라 샀다가 물림"); mind.ReactToPlayerTalk(-4f, 6f + mind.Traits.Grudge / 100f * 8f, "플레이어를 따라 샀다가 물림"); }
        }
        if (still == 0) { return; }
        // 따라 산 사람들이 벌면 소문이 난다.
        if (right) { GainRenown(Mathf.Min(6f, 1f + still * .8f), "나를 따른 " + still + "명이 범", room, messageId, now); }
        TraceTalk("    (나를 따른 봇 " + still + "명: " + (right ? "벌어서 호감·믿음이 오름" : "물려서 원망함") + ")");
        int speaker = followers[HypeRandom.Range(0, followers.Count)];
        Plan(room, right ? "follow.won" : "follow.lost", null, messageId, now + 2.0, speaker, 1, 1f, sure: true);
    }
}
