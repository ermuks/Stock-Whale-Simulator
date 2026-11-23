using System;
using System.Collections.Generic;

// 장 전체의 분위기.
//
// 종목 하나가 튀는 것(급등락 군중, 차트 얘기)과 따로, 시장 전체가 한쪽으로 쏠린 날에는 방에 그 분위기가 돈다.
// 거의 모든 종목이 빠지는 날에는 "시즌 종료 ㅈㅈ", "오늘 다 쏟네..." 같은 탄식이, 거의 모든 종목이 오르는 날에는
// "캬 오늘 소고기 썰러 간다", "대호황이로구나~~~!!" 같은 축제 분위기가 사이사이 올라온다.
//
// 판정: 상장 종목의 전일 종가 대비 등락률을 본다. 열에 일곱 넘게 같은 쪽이고 평균이 ±0.25%를 넘으면 그쪽 분위기다.
//       평균이 ±2.5%를 넘으면 폭락·대호황으로 치고, 말이 더 자주, 더 세게 나온다. 5초에 한 번만 다시 잰다.
// 말하는 사람과 말: 같은 장을 보고도 처지에 따라 다른 말을 한다. 뭔가 들고 있는 사람과 현금만 든 사람이 다르고,
//       욕심 많은 사람·겁 많은 사람·허세 센 사람이 다르다(급등락 군중과 같은 방식).
// 대사는 언어마다 따로 쓴 chat_hype 파일의 crowd에 있다(marketDown, marketDownHard, marketUp, marketUpHard).
// 표시 전용이다. 봇의 마음·주문·난수열을 건드리지 않는다.
public sealed partial class InvestorSociety
{
    private double marketMoodNext, marketMoodReadAt;
    private string marketMood;
    /// <summary>지금 읽은 장 분위기(없으면 null). 확인용.</summary>
    public string MarketMood => marketMood;
    /// <summary>확인용: 값을 주면 시세와 상관없이 그 분위기로 친다(marketDown, marketDownHard, marketUp, marketUpHard).</summary>
    public static string ForcedMarketMood;
    public int MarketMoodLines { get; private set; }

    private void TickMarketMood()
    {
        if (day == null || !SocialReady || !ChatHypeAssets.Ready || StockManager.Instance == null || day.Phase != MarketPhase.Trading) { marketMood = null; marketMoodNext = 0; return; }
        double now = SocialTime.Seconds;
        if (Math.Abs(now - marketMoodReadAt) > 5) { marketMoodReadAt = now; marketMood = ForcedMarketMood ?? ReadMarketMood(); }
        if (marketMood == null) { marketMoodNext = 0; return; }
        bool hard = marketMood.EndsWith("Hard", StringComparison.Ordinal);
        // 분위기가 막 잡혔거나 시계가 튀었으면 조금 있다가 첫마디가 나온다.
        if (marketMoodNext <= 0 || marketMoodNext - now > 120) { marketMoodNext = now + 4 + HypeRandom.Next() * 8; }
        // 한 종목의 급등락으로 방이 들끓는 동안에는 끼지 않는다.
        if (now < marketMoodNext || hypeRun != null) { return; }
        marketMoodNext = now + (hard ? 7 : 12) + HypeRandom.Next() * (hard ? 9 : 16);
        var bot = PickHypeSpeaker(now);
        if (bot == null) { return; }
        bool held = false;
        foreach (var stock in StockManager.Instance.stocks) { if (stock != null && bot.GetOwnedVolume(stock) > 0m) { held = true; break; } }
        var mind = bot.Social.Psychology;
        bool formal = mind.Traits.Politeness >= Config.Int("styleFormalMin");
        string text = null;
        if (!formal)
        {
            hypeOwn.Clear();
            string tone = mind.Traits.Bravado >= 72 ? "cocky" : mind.Fear >= 40f && mind.Fear >= mind.Greed * .6f ? "fearful" : mind.Greed >= 72f ? "greedy" : null;
            if (tone != null)
            {
                hypeOwn.AddRange(ChatHypeAssets.CrowdOf(marketMood, "any", tone));
                hypeOwn.AddRange(ChatHypeAssets.CrowdOf(marketMood, held ? "held" : "notHeld", tone));
            }
            if (hypeOwn.Count > 0 && HypeRandom.Next() < .35) { text = PickHypeText(Array.Empty<string>(), hypeOwn.ToArray()); }
        }
        text ??= PickHypeText(ChatHypeAssets.Crowd(marketMood, "any", formal), ChatHypeAssets.Crowd(marketMood, held ? "held" : "notHeld", formal));
        if (text == null) { return; }
        PublishHype(bot, ChatHypeAssets.Fill(text, HypeSlots(null, 0, 0m)), "market." + marketMood, null, 0, 1);
        hypeSpokeAt[bot.Social.Id] = now; MarketMoodLines++;
    }

    /// <summary>상장 종목들의 오늘 등락을 보고 장 분위기를 읽는다. 한쪽으로 쏠리지 않았으면 null.</summary>
    private static string ReadMarketMood()
    {
        var statistics = StockStatistics.Instance;
        if (statistics == null) { return null; }
        int seen = 0, up = 0, down = 0; decimal sum = 0m;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.IsListed || !statistics.TryGetDailyChange(stock, out decimal percent)) { continue; }
            seen++; sum += percent;
            if (percent > 0m) { up++; } else if (percent < 0m) { down++; }
        }
        if (seen < 4) { return null; }
        decimal average = sum / seen;
        if (down >= seen * .7f && average <= -.25m) { return average <= -2.5m ? "marketDownHard" : "marketDown"; }
        if (up >= seen * .7f && average >= .25m) { return average >= 2.5m ? "marketUpHard" : "marketUp"; }
        return null;
    }
}
