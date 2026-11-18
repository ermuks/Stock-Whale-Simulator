using UnityEngine;

/// <summary>성격·현재 스트레스·관계가 실제 선택 확률에 들어가는 작은 의사결정 모델.</summary>
public static class BotMind
{
    /// <summary>호가창의 벽이 어느 쪽으로 기울었는가(+면 매수벽, -면 매도벽). 게임 쪽에서 연결한다. 연결하지 않으면 벽을 보지 않는다.</summary>
    public static System.Func<Stock, float> WallLean;
    public static float BuyBias(TradeBot bot, Stock stock)
    {
        var s = bot.Social;
        if (s == null)
        { return 0f; }
        float fear = Mathf.Clamp01(s.Stress / 100f * .35f + s.Psychology.Fear / 100f * .45f + (1f - s.Confidence) * .2f);
        float result = (s.Greed - .5f) * .18f + (s.Impulsiveness - .5f) * .1f - fear * .14f - s.Suspicion * .05f + (s.Psychology.HasFomoFor(stock) ? (float)ChatSocialConfig.Current.Get("fomoBuyBias") : 0);
        // 큰 매수벽이 보이면 사고 싶어지고, 큰 매도벽이 보이면 손이 안 나간다.
        if (WallLean != null) { result += WallLean(stock) * .10f; }
        if (s.InfluencedStock == stock && SessionClock.Time < s.InfluenceUntil)
        { result += .06f + s.Greed * .12f - s.Suspicion * .06f; }
        return Mathf.Clamp(result, -.25f, .25f);
    }
    public static float PersuasionChance(BotSocialState s)
    {
        return s.Psychology.JoinChance;
    }
    public static string Compose(TradeBot bot, ChatEvent kind, Stock stock, string factualTemplate)
    {
        var s = bot.Social;
        if (s == null)
        { return factualTemplate; }
        int variant = (s.Id * 17 + s.Utterances++ * 7) & int.MaxValue;
        if (kind == ChatEvent.SmallTalk)
        {
            string[] topics = { "비가 계속 오네.", "커피 다 식었다.", "저녁 먹을 타이밍을 놓쳤네.", "창문 좀 열었다가 추워서 닫음.", "잠깐 눈 좀 쉬어야겠다.", "키보드 소리만 들리네.", "오늘은 라면으로 끝내야지.", "다들 아직 안 자요?", "물 마시는 것도 잊고 있었네.", "밖에 차 소리도 줄었네.", "화면 오래 보니 눈이 뻑뻑하네.", "옆집은 벌써 불 껐나 보다." };
            return topics[variant % topics.Length] + (s.Articulation < .3f && variant % 2 == 0 ? " ㅋㅋ" : "");
        }
        if (s.Articulation < .3f)
        {
            string[] tails = { "", " 음…", " ㅋㅋ", " 일단 보자." };
            return factualTemplate + tails[variant % tails.Length];
        }
        if (s.Articulation > .7f && variant % 5 == 0)
        {
            string[] thoughts = { "호가와 체결을 같이 볼 필요는 있겠네요.", "지금 보이는 정보만으로는 단정하기 어렵습니다.", "제 예상도 틀릴 수 있으니 비중은 조절하겠습니다.", "다른 참가자 반응을 조금 더 보겠습니다.", "한 번의 움직임으로 결론 내리지는 않겠습니다." };
            return factualTemplate.Replace("ㅋㅋ", "").Replace("ㄷㄷ", "") + " " + thoughts[variant % thoughts.Length];
        }
        if (s.Stress > 70f)
        { return factualTemplate + " 생각이 너무 많아지네…"; }
        if (s.Greed > .75f && variant % 3 == 0)
        { return factualTemplate + " 욕심은 나는데, 확신은 못 하겠음."; }
        return factualTemplate;
    }
}
