using System;
using System.Collections.Generic;
using UnityEngine;

// 지나가던 빌런. 아주 드물게, 방에 없던 누군가가 공개 라운지에 들어와 다들 당혹스러울 한마디를 던지고 곧바로 내보내진다.
// 내보내질 만한 말이어야 한다: 저속한 한마디("섹스")나 방 사람들을 향한 악담("오늘도 다들 손실 나길 바랍니다 ^^"). 생일 얘기처럼 무해한 말로 쫓겨나면 이상하다.
// "섹스" 한마디 쓰고 추방당하는 그 사람이다. 자주 나오면 안 된다.
// 아무 때나 나오지 않는다. 극한의 상황에서만, 그것도 낮은 확률로 나온다:
//  - 어떤 종목이 고점에서 18% 넘게 무너졌을 때(차트 수다의 dump2), 또는 하루에 22% 넘게 빠지며 신저가를 찍을 때(dayLow 2단계).
//  - 그런 일이 벌어질 때마다 VillainChance(6%)로 나오고, 하루에 한 번을 넘지 않는다. 그런 일이 없는 날에는 절대 나오지 않는다.
//
// 넣지 않는 말: 혐오·갈등 발언(장애·성별·인종·지역·세대 같은 집단을 비하하는 말), 자해를 부추기는 말, 성폭력을 떠올리게 하는 말.
// 저속한 말과 욕설, 방 사람들을 향한 악담까지만 쓴다.
//
// 표시 전용이다. 진짜 봇이 아니라 한 번 쓰고 버리는 이름이라, 어떤 봇의 계좌·감정·기억·관계·매매에도 닿지 않는다.
// 방의 봇 한두 명이 어리둥절해하는 말도 차트 수다와 같은 표시 전용 통로(PublishHype)로 나간다.
// 대사는 언어마다 따로 쓴 파일(Chat/chat_villain.ko/en/ja)에서 온다. 서로의 번역이 아니다.
[Serializable] public sealed class ChatVillainLocale
{
    public int schemaVersion = 1;
    public string[] names, lines, reactions, after;
    public string notice;
}

public sealed partial class InvestorSociety
{
    /// <summary>극한의 상황 한 번에 빌런이 나올 확률, 그리고 그 일이 벌어진 뒤 몇 초 만에 들어오는지(방이 술렁이는 한가운데).</summary>
    public const double VillainChance = .06;
    private const double VillainDelayMin = 4, VillainDelayMax = 10;
    private int villainUsedDay = -1;
    private static readonly Dictionary<string, ChatVillainLocale> villainLocales = new(StringComparer.Ordinal);
    private int villainDay = -1, villainStep;
    private double villainAt = -1, villainNext;
    private string villainName;
    /// <summary>확인용: 지금까지 빌런이 지나간 횟수.</summary>
    public int VillainsSeen { get; private set; }

    private static ChatVillainLocale VillainLines()
    {
        string code = GameLocale.Code;
        if (!villainLocales.TryGetValue(code, out var locale))
        {
            var asset = Resources.Load<TextAsset>("Chat/chat_villain." + code);
            locale = asset == null ? null : JsonUtility.FromJson<ChatVillainLocale>(asset.text);
            if (locale != null && (locale.schemaVersion != 1 || locale.names == null || locale.names.Length == 0 || locale.lines == null || locale.lines.Length == 0 || string.IsNullOrEmpty(locale.notice))) { locale = null; }
            villainLocales[code] = locale;
        }
        return locale;
    }

    private void TickVillain()
    {
        if (!HypeAllowed || day == null || !SocialReady || !ChatHypeAssets.Ready) { return; }
        double now = SocialTime.Seconds;
        if (villainDay != day.Day) { villainDay = day.Day; villainStep = 0; villainAt = -1; }
        if (villainStep == 0)
        {
            if (villainAt < 0 || day.Phase != MarketPhase.Trading || SessionClock.Time < villainAt) { return; }
            villainAt = -1; BeginVillain(now);
            return;
        }
        if (now < villainNext) { return; }
        var locale = VillainLines();
        if (locale == null) { villainStep = 0; return; }
        if (villainStep == 1 || villainStep == 2)
        {
            // 방의 한두 명이 어리둥절해한다.
            VillainReact(locale.reactions, now);
            bool more = villainStep == 1 && HypeRandom.Next() < .6;
            // 한마디가 화면 위로 밀려 올라가기 전에 끝난다: 첫 반응까지 1초 남짓, 내보내기까지 모두 합쳐 3~4초.
            villainStep = more ? 2 : 3; villainNext = now + (more ? .5 + HypeRandom.Next() * .7 : .9 + HypeRandom.Next() * .9);
            return;
        }
        if (villainStep == 3)
        {
            day.PublishRoomNotice(locale.notice.Replace("{name}", villainName), ChatRoomNotice.Kick, 0);
            villainStep = 4; villainNext = now + 1.4 + HypeRandom.Next() * 1.6;
            return;
        }
        // 내보내진 뒤 한마디(절반쯤은 아무도 말하지 않는다).
        if (HypeRandom.Next() < .55) { VillainReact(locale.after, now); }
        villainStep = 0;
    }

    /// <summary>극한의 상황이 벌어졌다(ChartTalk가 부른다). 낮은 확률로, 하루에 한 번까지만 빌런을 부른다.</summary>
    private void OfferVillain()
    {
        if (day == null || villainStep != 0 || villainAt >= 0 || villainUsedDay == day.Day || HypeRandom.Next() >= VillainChance) { return; }
        villainUsedDay = day.Day; villainDay = day.Day;
        villainAt = SessionClock.Time + VillainDelayMin + HypeRandom.Next() * (VillainDelayMax - VillainDelayMin);
    }

    private void BeginVillain(double now)
    {
        var locale = VillainLines();
        if (locale == null) { return; }
        villainName = locale.names[HypeRandom.Range(0, locale.names.Length)];
        string line = locale.lines[HypeRandom.Range(0, locale.lines.Length)];
        // 봇이 아닌 발신자로 올린다. 이 이름은 이번 한 번만 쓰인다.
        day.PublishSpeaker(villainName, line, "villain", null);
        VillainsSeen++;
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "VILLAIN", reason = villainName + ": " + line });
        hypeQuietUntil = Math.Max(hypeQuietUntil, now + 1.2);
        villainStep = 1; villainNext = now + .8 + HypeRandom.Next() * .8;
    }

    private void VillainReact(string[] lines, double now)
    {
        if (lines == null || lines.Length == 0) { return; }
        var bot = PickHypeSpeaker(now);
        if (bot == null) { return; }
        PublishHype(bot, lines[HypeRandom.Range(0, lines.Length)], "villain.react", null, 0, 0);
        hypeSpokeAt[bot.Social.Id] = now;
    }

    /// <summary>확인용: 지금 바로 빌런을 지나가게 한다(캡처 도구). 아무 봇에게도 닿지 않는 것은 같다.</summary>
    public bool ForceVillain()
    {
        if (day == null || villainStep != 0 || VillainLines() == null) { return false; }
        BeginVillain(SocialTime.Seconds);
        return villainStep == 1;
    }
}
