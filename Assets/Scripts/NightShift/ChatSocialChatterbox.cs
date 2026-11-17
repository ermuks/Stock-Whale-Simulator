using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    /// <summary>방에서 유독 말이 많은 사람인가. 저장하지 않고 방 구성에서 다시 정한다.</summary>
    internal bool Chatterbox;
}

/// <summary>
/// 수다쟁이: 방마다 유독 말이 많고 남의 말에 자꾸 끼어드는 사람을 몇 명 둔다.
/// 번호로 고정해서 뽑기 때문에 같은 세계에서는 늘 같은 닉네임이 떠든다.
/// 말의 내용은 다른 사람과 같은 규칙으로 정해진다. 빈도와 말 거는 횟수, 전용 대사 몇 줄만 다르다.
/// </summary>
public sealed partial class InvestorSociety
{
    /// <summary>게임 쪽에서 켠다. 꺼져 있으면 수다쟁이를 뽑지 않는다.</summary>
    public static bool ChatterboxEnabled;

    private const int PublicChatterboxes = 2, RoomChatterboxes = 1;
    private const double ChatterRosterSeconds = 30;
    // 마지막으로 말한 뒤 이 시간이 지나야 새 말을 꺼낸다. 방 하나가 하루에 내보내는 말의 열에 하나쯤을 한 사람이 차지하게 된다.
    private const double ChatterGapSeconds = 45;
    // 쉬는 시간이 지난 뒤 말할 차례가 올 때마다(2초 간격) 말을 꺼낼 확률. 수다쟁이의 말은 방의 도배 한도를 기다리지 않고 나간다.
    private const double ChatterIdleChance = .5;
    // 말을 꺼낼 때 방금 말한 사람에게 말을 걸 확률.
    private const double ChatterInitiateChance = .7;
    // 전용 대사를 쓰는 비율(100번 중).
    private const int ChatterLinePercent = 35;

    private readonly List<TradeBot> chatterboxes = new();
    private readonly Dictionary<int, List<TradeBot>> chatterRooms = new();
    private double chatterRosterAt = double.NegativeInfinity;

    public IReadOnlyList<TradeBot> Chatterboxes => chatterboxes;

    private static uint ChatterRank(TradeBot bot) => CompanyBusiness.Hash("CHATTER:" + bot.Social.Id);

    private void RefreshChatterboxes(double now)
    {
        if (!ChatterboxEnabled || now >= chatterRosterAt && now - chatterRosterAt < ChatterRosterSeconds)
        { return; }

        chatterRosterAt = now;
        foreach (var bot in chatterboxes)
        {
            if (bot != null && bot.Social != null) { bot.Social.Psychology.Chatterbox = false; }
        }

        chatterboxes.Clear();
        foreach (var list in chatterRooms.Values)
        { list.Clear(); }

        foreach (var bot in socialBots)
        {
            if (bot == null || bot.Social == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || bot.KnowsRealValue)
            { continue; }

            var mind = bot.Social.Psychology;
            // 진지하거나 존댓말을 쓰는 사람은 수다쟁이가 되지 않는다.
            if (mind.Traits.Serious || mind.Traits.Politeness >= Config.Int("styleFormalMin") || !Present(bot, mind.Room))
            { continue; }

            if (!chatterRooms.TryGetValue(mind.Room, out var members))
            {
                members = new List<TradeBot>();
                chatterRooms.Add(mind.Room, members);
            }

            members.Add(bot);
        }

        foreach (var pair in chatterRooms)
        {
            var members = pair.Value;
            int wanted = Math.Min(members.Count, pair.Key == 0 ? PublicChatterboxes : RoomChatterboxes);
            for (int picked = 0; picked < wanted; picked++)
            {
                int best = -1;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].Social.Psychology.Chatterbox)
                    { continue; }

                    if (best < 0 || ChatterRank(members[i]) > ChatterRank(members[best]) ||
                        ChatterRank(members[i]) == ChatterRank(members[best]) && members[i].Social.Id < members[best].Social.Id)
                    { best = i; }
                }

                if (best < 0)
                { break; }

                members[best].Social.Psychology.Chatterbox = true;
                chatterboxes.Add(members[best]);
            }
        }
    }

    /// <summary>수다쟁이가 혼자 꺼내는 말에 가끔 쓰는 전용 대사 묶음.</summary>
    private static SocialCommunityPhrases ChatterGroup(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        if (!mind.Chatterbox || parent != null || message.targetId != 0 || !CommunityAllowed(template, message) ||
            CompanyBusiness.Hash("CHATTER-LINE:" + message.id) % 100 >= ChatterLinePercent)
        { return null; }

        return ChatSocialAssets.Community(message.act, "CHATTER");
    }
}
