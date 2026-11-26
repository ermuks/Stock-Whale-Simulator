using System;
using System.Collections.Generic;
using UnityEngine;

// 시간대 잡담.
//
// 사람들은 주식 얘기만 하지 않는다. 아침에는 출근·회의·커피, 점심 무렵에는 뭐 먹을지, 오후에는 졸음과 퇴근,
// 저녁에는 저녁밥과 야근, 밤에는 야식, 새벽에는 "님들 안 잠?"을 말한다. 게임 안의 시계(시)를 보고 그 시간에 맞는 화제를 고른다.
//
// 화제는 언어마다 따로 쓴 자료 파일에 있다(Chat/chat_hours.ko / .en / .ja). 번역본이 아니다: 나라마다 하는 얘기가 다르다
// (한국의 국밥·지옥철·칼퇴, 미국의 standup·Chipotle·happy hour, 일본의 満員電車·社食·残業). 화면에 그대로 뜨므로 번역을 거치지 않는다.
// 화제 하나는 꺼내는 말(open) · 받는 말(reply) · 꺼낸 사람의 맺는말(back)로 이루어진다. 받는 말은 같은 화제에서만 고르므로
// 한 화제의 꺼내는 말은 모두 같은 것을 묻거나 말해야 한다(어느 받는 말이 붙어도 말이 되게).
//
// 도는 곳:
//   - 장중(09~17시): 라운지(공개방)에서만, 드문드문(DayTalkGap). 급등·급락으로 방이 달아올라 있으면 꺼내지 않는다.
//   - 장외(17~09시): 예전부터 있던 장외 대화(ChatSocialAfterHours.cs)의 잡담 화제로 섞여 나온다.
//   - 새벽 관찰실: 거의 하지 않는다(DawnRoomCasualChance). 그 방은 종목을 보는 방이다.
// 화제를 늘리려면 그 언어의 파일에 한 덩어리를 더 적으면 된다. 코드는 고치지 않는다.
public static class ChatHoursAssets
{
    private static readonly Dictionary<string, AfterHoursTopic[]> Cache = new(StringComparer.Ordinal);
    private static readonly AfterHoursTopic[] None = Array.Empty<AfterHoursTopic>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Cache.Clear(); }

    /// <summary>그 언어의 시간대 화제들. 파일이 없거나 틀렸으면 빈 목록(예전 화제만 쓴다).</summary>
    public static AfterHoursTopic[] For(string code)
    {
        code ??= "ko";
        if (Cache.TryGetValue(code, out var found)) { return found; }
        var asset = Resources.Load<TextAsset>("Chat/chat_hours." + code);
        found = Parse(asset == null ? null : asset.text);
        Cache[code] = found;
        return found;
    }

    /// <summary>파일의 글을 화제 목록으로 바꾼다. 꺼내는 말이나 받는 말이 없는 화제는 버린다.</summary>
    public static AfterHoursTopic[] Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) { return None; }
        AfterHoursDocument data;
        try { data = JsonUtility.FromJson<AfterHoursDocument>(json); } catch (ArgumentException) { return None; }
        if (data == null || data.schemaVersion != 1 || data.topics == null) { return None; }
        var list = new List<AfterHoursTopic>();
        foreach (var topic in data.topics)
        {
            if (topic == null || string.IsNullOrEmpty(topic.key) || topic.open == null || topic.open.Length == 0 || topic.reply == null || topic.reply.Length == 0) { continue; }
            if (topic.weight <= 0) { topic.weight = 1; }
            topic.kind = "CASUAL"; topic.native = true; topic.openMorning = null; topic.replyHolder = null;
            list.Add(topic);
        }
        return list.ToArray();
    }

    /// <summary>그 시각(시)에 꺼낼 수 있는 화제인가. fromHour가 toHour보다 크면 자정을 넘긴다.</summary>
    public static bool InHours(AfterHoursTopic topic, int hour)
        => topic.fromHour <= topic.toHour ? hour >= topic.fromHour && hour < topic.toHour : hour >= topic.fromHour || hour < topic.toHour;

    /// <summary>예전 장외 화제 가운데, 시간대 화제가 같은 얘기를 대신하는 것(한국어). 둘 다 두면 저녁밥 얘기가 연달아 나온다.</summary>
    public static bool Replaces(string oldKey)
        => oldKey == "DINNER" || oldKey == "OFFWORK" || oldKey == "SNACK" || oldKey == "SLEEP" || oldKey == "DAWN" || oldKey == "COMMUTE";
}

public sealed partial class InvestorSociety
{
    // 장중에 라운지에서 잡담을 꺼내는 간격(초). 하루(09~17시)가 900초이므로 게임의 한 시간에 한두 토막이 나온다.
    private const double DayTalkGapMin = 40, DayTalkGapMax = 95, DayTalkBusyWait = 15;
    /// <summary>새벽 관찰실에서 잡담 화제를 꺼낼 확률. 그 방은 거의 종목 얘기만 한다.</summary>
    public const double DawnRoomCasualChance = .08;
    private readonly AfterRoom dayTalk = new();
    private bool dayTalkPrimed;

    /// <summary>장중에 부른다. 라운지에서 지금 시각에 맞는 잡담 한 토막(꺼내는 말 → 받는 말 몇 개 → 맺는말)을 잇는다.</summary>
    private void TickDayTalk()
    {
        if (afterHoursOpen || SocialRoom(0) == null) { return; }
        double now = SocialTime.Seconds;
        int hour = (int)(SocialTime.GameMinute % 1440d / 60d);
        var room = dayTalk;
        // 처음이거나, 저장한 판을 불러와 시간이 되돌아갔으면 새로 기다린다.
        if (!dayTalkPrimed || room.nextThreadAt - now > DayTalkGapMax + 1)
        {
            dayTalkPrimed = true; room.thread = null; room.nextThreadAt = now + AfterRange(12, DayTalkGapMax);
            return;
        }
        if (room.thread == null)
        {
            if (now < room.nextThreadAt) { return; }
            // 방이 급등·급락으로 달아올라 있으면 점심 얘기를 꺼내지 않는다. 잠잠해지면 꺼낸다.
            if (hypeRun != null || now < hypeQuietUntil) { room.nextThreadAt = now + DayTalkBusyWait; return; }
            StartAfterThread(0, room, now, hour);
            room.nextThreadAt = now + AfterRange(DayTalkGapMin, DayTalkGapMax);
        }
        else if (now >= room.thread.nextAt || room.thread.nextAt - now > AfterReplyMax + 1)
        {
            ContinueAfterThread(0, room, now, hour);
            if (room.thread == null) { room.nextThreadAt = now + AfterRange(DayTalkGapMin, DayTalkGapMax); }
        }
    }

    /// <summary>지금 시각에 맞는 잡담 화제 하나. 그 언어로 따로 쓴 시간대 화제를 먼저 쓰고, 없으면 예전 장외 화제를 쓴다.</summary>
    private AfterHoursTopic PickCasualTopic(AfterRoom room, int hour)
    {
        string code = GameLocale.Code;
        var native = ChatHoursAssets.For(code);
        AfterHoursTopic chosen = null;
        double total = 0;
        bool any = false;
        foreach (var topic in native)
        {
            if (!ChatHoursAssets.InHours(topic, hour)) { continue; }
            any = true;
            if (RecentAfterTopic(room, topic)) { continue; }
            total += topic.weight;
            if (afterRandom.NextDouble() * total < topic.weight) { chosen = topic; }
        }
        foreach (var topic in AfterHoursLines.topics)
        {
            if (topic.kind != "CASUAL" || !InHours(topic, hour) || RecentAfterTopic(room, topic)) { continue; }
            // 예전 화제는 한국어로 쓴 것이다. 다른 언어에 그 언어의 화제가 있으면 그것만 쓰고, 한국어에서는 겹치는 얘기만 뺀다.
            if (any && (code != "ko" || ChatHoursAssets.Replaces(topic.key))) { continue; }
            total += topic.weight;
            if (afterRandom.NextDouble() * total < topic.weight) { chosen = topic; }
        }
        return chosen;
    }

    private static bool RecentAfterTopic(AfterRoom room, AfterHoursTopic topic)
    {
        foreach (string mark in room.recentTopics) { if (mark == topic.key) { return true; } }
        return false;
    }
}
