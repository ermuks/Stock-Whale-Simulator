using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class RoomTalkGroup
{
    /// <summary>"상황.화자 유형". 예: PROMO.A1+ (홍보에 답함 · 들어는 봤고 좋게 보는 사람).</summary>
    public string key;
    /// <summary>이 말이 방을 좋게 말하는지(+1), 나쁘게 말하는지(−1), 묻거나 중립인지(0).</summary>
    public int stance;
    public string[] texts;
}

[Serializable]
public sealed class RoomTalkDocument
{
    public int schemaVersion;
    public RoomTalkGroup[] groups;
}

public sealed partial class InvestorPsychology
{
    /// <summary>플레이어의 리딩방이 있다는 것을 얼마나 아는가(0~100). 홍보나 방 얘기를 볼 때마다 오른다.</summary>
    public double RoomAwareness;
    /// <summary>남의 말로 쌓인 리딩방에 대한 인상(0~100, 50이 중립). 직접 겪은 신뢰·배신감과는 따로 둔다.</summary>
    public double RoomHearsay = 50;
    /// <summary>한 번이라도 들어가 본 플레이어 방(비트).</summary>
    public int JoinedPlayerRooms;
    /// <summary>어디까지 읽었는가. 저장하지 않는다.</summary>
    internal long RoomTalkSeen;

    /// <summary>리딩방에 대한 신뢰(0~100): 소문으로 쌓인 인상에 직접 겪은 신뢰와 배신감을 더한다.</summary>
    public double RoomTrust => Math.Max(0d, Math.Min(100d, RoomHearsay + (FormulaTrust - FormulaBetrayal) * .5d));
}

/// <summary>
/// 리딩방에 대한 인지와 신뢰, 그리고 방을 두고 오가는 말.
/// - 인지: 홍보나 방 얘기를 볼 때마다 0~60 오른다. 오래된 글일수록 오르는 폭이 0에 가까워진다.
/// - 신뢰: 다른 사람이 방을 좋게/나쁘게 말하는 것을 보면 조금씩 움직인다. 방장 자신의 홍보는 신뢰를 올리지 않는다.
/// - 말: 같은 상황에서도 그 사람의 인지와 신뢰, 회원 여부에 따라 다른 문장을 쓴다.
/// </summary>
public sealed partial class InvestorSociety
{
    /// <summary>게임 쪽에서 켠다. 꺼져 있으면 방 얘기를 스스로 꺼내지 않는다.</summary>
    public static bool RoomTalkEnabled;

    public const int RoomTalkRoom = 1;
    public const double AwarenessGainMax = 60, AwarenessHalfLifeMinutes = 20;
    /// <summary>이 값부터 방이 있다는 것을 안다(들어갈 수도 있다). 그 위는 잘 아는 사람.</summary>
    public const double AwarenessKnows = 25, AwarenessFamiliar = 60;
    public const double RoomTrustHigh = 60, RoomTrustLow = 40;
    // 남의 말 한 번이 인상을 움직이는 최대 폭. 귀가 얇은 사람일수록 크게 움직인다.
    private const double HearsayStep = 6;
    private const int RoomTalkLogSize = 40, RoomTalkRecentSpeakers = 10, RoomTalkRecentLines = 40;
    private const double RoomTalkReplyMin = 3, RoomTalkReplyMax = 9, RoomTalkChainChance = .55;
    private const int RoomTalkMaxDepth = 2;
    // 누가 방 얘기를 스스로 꺼내는 간격(초).
    private const double RoomTalkIdleMin = 90, RoomTalkIdleMax = 240;

    private sealed class RoomTalkEntry
    {
        public long id;
        public double at;
        public int sender, stance;
    }

    private sealed class RoomTalkPending
    {
        public double due;
        public string situation;
        public long replyTo;
        public int target, depth;
    }

    private static RoomTalkDocument roomTalkLines;
    private static Dictionary<string, RoomTalkGroup> roomTalkIndex;
    private readonly List<RoomTalkEntry> roomTalkLog = new();
    private readonly List<RoomTalkPending> roomTalkPending = new();
    private readonly Queue<int> roomTalkSpeakers = new();
    private readonly Queue<string> roomTalkRecentLines = new();
    private readonly System.Random roomTalkRandom = new(90511);
    private double nextRoomTalkAt = double.NaN;

    public int RoomTalkPublished { get; private set; }

    private static RoomTalkGroup RoomTalkLines(string key)
    {
        if (roomTalkIndex == null)
        {
            var asset = Resources.Load<TextAsset>("Chat/chat_roomtalk");
            var document = asset == null ? null : JsonUtility.FromJson<RoomTalkDocument>(asset.text);
            if (document == null || document.schemaVersion != 1 || document.groups == null)
            { throw new ArgumentException("Invalid room talk catalog"); }

            var index = new Dictionary<string, RoomTalkGroup>(StringComparer.Ordinal);
            foreach (var group in document.groups)
            {
                if (group == null || string.IsNullOrEmpty(group.key) || group.texts == null || group.texts.Length == 0 || group.stance < -1 || group.stance > 1 || index.ContainsKey(group.key))
                { throw new ArgumentException("Invalid room talk group"); }

                index.Add(group.key, group);
            }

            roomTalkLines = document; roomTalkIndex = index;
        }

        return roomTalkIndex.TryGetValue(key, out var found) ? found : null;
    }

    /// <summary>글이 올라온 지 오래될수록 0에 가까워진다(20분마다 절반).</summary>
    public static double RoomTalkFreshness(double ageMinutes) => Math.Pow(.5d, Math.Max(0d, ageMinutes) / AwarenessHalfLifeMinutes);

    /// <summary>방 얘기가 채팅에 올라왔음을 적어 둔다. sender 0은 방장 자신의 홍보.</summary>
    public void NoteRoomTalk(long messageId, int sender, int stance)
    {
        roomTalkLog.Add(new RoomTalkEntry { id = messageId, at = SocialTime.GameMinute, sender = sender, stance = Math.Sign(stance) });
        while (roomTalkLog.Count > RoomTalkLogSize) { roomTalkLog.RemoveAt(0); }
    }

    /// <summary>
    /// 이 사람이 아직 보지 못한 방 얘기를 읽는다. 공개방에 없던 사람은 나중에 돌아와서 읽고, 그때는 글이 오래돼 인지가 덜 오른다.
    /// 돌려주는 값은 이번에 오른 인지.
    /// </summary>
    public double WitnessRoomTalk(TradeBot bot)
    {
        if (roomTalkLog.Count == 0 || bot == null || bot.Social == null)
        { return 0; }

        var mind = bot.Social.Psychology;
        long newest = roomTalkLog[roomTalkLog.Count - 1].id;
        if (mind.RoomTalkSeen >= newest || !Present(bot, 0))
        { return 0; }

        double chance = WitnessChance?.Invoke(bot) ?? Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + mind.Activity * Config.Get("witnessActivityScale"));
        double gained = 0;
        foreach (var entry in roomTalkLog)
        {
            if (entry.id <= mind.RoomTalkSeen || entry.sender == bot.Social.Id || roomTalkRandom.NextDouble() >= chance)
            { continue; }

            double fresh = RoomTalkFreshness(SocialTime.GameMinute - entry.at);
            double gain = roomTalkRandom.NextDouble() * AwarenessGainMax * fresh;
            double before = mind.RoomAwareness;
            mind.RoomAwareness = Math.Min(100d, mind.RoomAwareness + gain);
            gained += mind.RoomAwareness - before;
            // 방장의 홍보는 방이 있다는 것만 알린다. 남이 한 말만 인상을 움직인다.
            if (entry.sender > 0 && entry.stance != 0)
            { mind.RoomHearsay = Math.Max(0d, Math.Min(100d, mind.RoomHearsay + entry.stance * HearsayStep * fresh * mind.Credulity)); }

            ChatSocialAudit.Journal.Event(new SocialEvent { id = entry.id, time = SocialTime.GameMinute, actor = bot.Social.Id, target = entry.sender == 0 ? -1 : entry.sender,
                room = RoomTalkRoom, kind = "ROOM_TALK_SEEN", reason = "인지 +" + gain.ToString("0.0") + " (신선도 " + fresh.ToString("0.00") + ")" });
        }

        mind.RoomTalkSeen = newest;
        return gained;
    }

    /// <summary>방에 들어간 사람은 방을 완전히 안다.</summary>
    private static void RememberRoomJoin(InvestorPsychology mind, int roomId)
    {
        mind.RoomAwareness = 100;
        mind.JoinedPlayerRooms |= 1 << (roomId - 1);
    }

    /// <summary>방을 아는 정도와 믿는 정도가 들어갈 마음을 얼마나 키우는가. 모르고 중립이면 1.</summary>
    public static float RoomInterest(InvestorPsychology mind)
        => (float)((1d + mind.RoomAwareness / 100d) * (.5d + mind.RoomTrust / 100d));

    /// <summary>
    /// 화자 유형. 회원(MEM)·전 회원(EX)은 겪어 본 사람으로 말하고(+ 벌었음·0 아직 결과 없음·- 잃었음),
    /// 나머지는 인지(A0 모름·A1 들어 봄·A2 잘 앎)와 신뢰(+ 좋게 봄·0 반반·- 나쁘게 봄)로 나뉜다.
    /// </summary>
    public static string RoomTalkProfile(InvestorPsychology mind)
    {
        int bit = 1 << (RoomTalkRoom - 1);
        double trust = mind.RoomTrust;
        bool member = mind.Room == RoomTalkRoom;
        if (member || ((mind.JoinedPlayerRooms | mind.InvitedPlayerRooms) & bit) != 0)
        {
            // 겪어 본 사람은 겪은 대로 말한다: 그 방의 지시로 실제로 번 횟수와 잃은 횟수. 아직 결과가 없으면 번 척도 잃은 척도 하지 않는다.
            int wins = 0, losses = 0;
            foreach (var experience in mind.Experiences)
            {
                if (experience.Room != RoomTalkRoom || experience.CommandId <= 0) { continue; }
                if (experience.ReturnPercent > 0) { wins++; } else if (experience.ReturnPercent < 0) { losses++; }
            }

            string result = wins + losses == 0 ? "0" : wins > losses ? "+" : losses > wins ? "-" : trust >= 50 ? "+" : "-";
            return (member ? "MEM" : "EX") + result;
        }

        if (mind.RoomAwareness < AwarenessKnows) { return "A0"; }
        return (mind.RoomAwareness < AwarenessFamiliar ? "A1" : "A2") + (trust >= RoomTrustHigh ? "+" : trust <= RoomTrustLow ? "-" : "0");
    }

    /// <summary>방장의 홍보에 몇 사람이 답하도록 예약한다.</summary>
    public void BeginRoomPromotionTalk(long promotionId)
    {
        NoteRoomTalk(promotionId, 0, 0);
        int replies = 1 + roomTalkRandom.Next(3);
        double due = SocialTime.Seconds;
        for (int i = 0; i < replies; i++)
        {
            due += RoomTalkReplyMin + roomTalkRandom.NextDouble() * (RoomTalkReplyMax - RoomTalkReplyMin);
            roomTalkPending.Add(new RoomTalkPending { due = due, situation = "PROMO", replyTo = promotionId, target = -1, depth = 1 });
        }
    }

    /// <summary>예약된 답을 내보내고, 가끔 누군가 방 얘기를 스스로 꺼낸다.</summary>
    public void TickRoomTalk()
    {
        if (!RoomTalkEnabled || !SocialReady)
        { return; }

        double now = SocialTime.Seconds;
        for (int i = 0; i < roomTalkPending.Count; i++)
        {
            var pending = roomTalkPending[i];
            // 불러오기로 시계가 되돌아갔으면 기다리던 답은 버린다.
            if (pending.due - now > RoomTalkReplyMax * 4) { roomTalkPending.RemoveAt(i--); continue; }
            if (pending.due > now) { continue; }
            roomTalkPending.RemoveAt(i);
            SpeakRoomTalk(pending.situation, pending.replyTo, pending.target, pending.depth);
            return;
        }

        if (double.IsNaN(nextRoomTalkAt) || nextRoomTalkAt - now > RoomTalkIdleMax + 1) { nextRoomTalkAt = now + RoomTalkIdleMin + roomTalkRandom.NextDouble() * (RoomTalkIdleMax - RoomTalkIdleMin); }
        if (now < nextRoomTalkAt)
        { return; }

        nextRoomTalkAt = now + RoomTalkIdleMin + roomTalkRandom.NextDouble() * (RoomTalkIdleMax - RoomTalkIdleMin);
        if (IsRoomOpen(RoomTalkRoom)) { SpeakRoomTalk("OPEN", 0, 0, 0); }
    }

    private TradeBot PickRoomTalkSpeaker(string situation, int exclude)
    {
        TradeBot chosen = null;
        double total = 0;
        int formal = Config.Int("styleFormalMin");
        foreach (var bot in socialBots)
        {
            if (bot == null || bot.Social == null || bot.Social.Id == exclude || bot.KnowsRealValue || !Present(bot, 0))
            { continue; }

            var mind = bot.Social.Psychology;
            if (mind.Traits.Politeness >= formal || roomTalkSpeakers.Contains(bot.Social.Id))
            { continue; }

            string profile = RoomTalkProfile(mind);
            // 방을 모르는 사람이 방 얘기를 먼저 꺼내지는 않는다.
            if (situation == "OPEN" && profile == "A0")
            { continue; }

            // 겪어 본 사람과 수다쟁이가 더 자주 나선다.
            double weight = (profile.StartsWith("MEM", StringComparison.Ordinal) || profile.StartsWith("EX", StringComparison.Ordinal) ? 2 : 1) * (mind.Chatterbox ? 3 : 1);
            total += weight;
            if (roomTalkRandom.NextDouble() * total < weight) { chosen = bot; }
        }

        return chosen;
    }

    /// <summary>한 사람이 방 얘기를 한다. 말한 사람이 없으면 0.</summary>
    public long SpeakRoomTalk(string situation, long replyTo, int target, int depth)
    {
        var bot = PickRoomTalkSpeaker(situation, target);
        if (bot == null)
        { return 0; }

        var mind = bot.Social.Psychology;
        var group = RoomTalkLines(situation + "." + RoomTalkProfile(mind));
        if (group == null)
        { return 0; }

        string phrase = null;
        int seen = 0;
        foreach (string line in group.texts)
        {
            if (roomTalkRecentLines.Contains(line)) { continue; }
            seen++;
            if (roomTalkRandom.Next(seen) == 0) { phrase = line; }
        }

        phrase ??= group.texts[roomTalkRandom.Next(group.texts.Length)];
        roomTalkRecentLines.Enqueue(phrase);
        while (roomTalkRecentLines.Count > RoomTalkRecentLines) { roomTalkRecentLines.Dequeue(); }

        string targetName = target == -1 ? "방장" : target > 0 && SocialBots.TryGetValue(target, out var other) ? other.Social.Nickname : null;
        var facts = new SocialFacts { at = SocialTime.GameMinute };
        var message = new SocialMessage
        {
            id = ChatSocialIds.Next(), roomId = 0, senderId = bot.Social.Id, targetId = targetName == null ? 0 : target,
            at = SocialTime.GameMinute, realAt = SocialTime.Seconds, replyToId = replyTo, facts = facts, topicEvent = "ROOM_TALK", templateKey = "ROOMTALK." + group.key,
            act = situation == "OPEN" && group.stance == 0 ? SocialAct.ASK : replyTo != 0 ? SocialAct.ANSWER : SocialAct.REACT,
            localizedPhrase = phrase, localizedValues = Array.Empty<SocialSpeechValue>(), localizedTarget = targetName,
            debug = new SocialMessageDebug { trigger = "ROOM_TALK", speechKey = "ROOMTALK." + group.key, weights = Array.Empty<SocialWeight>(), slots = facts }
        };
        message.text = targetName == null ? phrase : "@" + targetName + " " + phrase;
        bot.Social.LastSpeech = (float)SocialTime.Seconds;
        roomTalkSpeakers.Enqueue(bot.Social.Id);
        while (roomTalkSpeakers.Count > RoomTalkRecentSpeakers) { roomTalkSpeakers.Dequeue(); }
        RoomTalkPublished++;
        SocialMessagePublished?.Invoke(message);
        NoteRoomTalk(message.id, bot.Social.Id, group.stance);

        // 누가 한마디 하면 다른 사람이 받는다. 좋게 말했으면 맞장구나 반박이, 물었으면 대답이 붙는다.
        if (depth < RoomTalkMaxDepth && (depth == 0 || roomTalkRandom.NextDouble() < RoomTalkChainChance))
        {
            roomTalkPending.Add(new RoomTalkPending
            {
                due = SocialTime.Seconds + RoomTalkReplyMin + roomTalkRandom.NextDouble() * (RoomTalkReplyMax - RoomTalkReplyMin),
                situation = group.stance > 0 ? "REPLY_POS" : group.stance < 0 ? "REPLY_NEG" : "REPLY_ASK",
                replyTo = message.id, target = bot.Social.Id, depth = depth + 1
            });
        }

        return message.id;
    }
}
