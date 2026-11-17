using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class AfterHoursTopic
{
    public string key;
    /// <summary>CASUAL이면 잡담, 그 밖에는 종목의 종가 상태(BREAK·UP·DOWN·FLAT).</summary>
    public string kind;
    /// <summary>이 화제가 나오는 시각(시). fromHour가 toHour보다 크면 자정을 넘긴다.</summary>
    public int fromHour, toHour;
    public double weight = 1;
    /// <summary>말을 꺼내는 문장. openMorning이 있으면 자정 이후에는 그쪽을 쓴다("어제 … 오늘").</summary>
    public string[] open, openMorning;
    /// <summary>받아치는 문장. replyHolder는 그 종목을 실제로 들고 있는 사람만 쓴다.</summary>
    public string[] reply, replyHolder;
    /// <summary>말을 꺼낸 사람이 답을 듣고 마무리하는 문장.</summary>
    public string[] back;
}

[Serializable]
public sealed class AfterHoursDocument
{
    public int schemaVersion;
    public AfterHoursTopic[] topics;
}

/// <summary>
/// 장 마감부터 다음 개장까지의 채팅.
/// 장중 대화는 매매와 시세 변화에서 나오므로 장이 닫히면 멈춘다. 그 시간에는 화제 하나를 꺼내고 몇 사람이 받아치는 짧은 대화를 잇는다.
/// 화제는 둘: 오늘 종가를 두고 하는 말(누구나 보는 종가·등락률만 쓴다)과, 주식과 상관없는 잡담.
/// 답은 같은 화제의 문장에서만 고르므로 엉뚱한 말에는 엉뚱한 답이 붙고 맥락은 이어진다.
/// 여기서 나온 말은 봇의 속마음이나 매매에 영향을 주지 않는다.
/// </summary>
public sealed partial class InvestorSociety
{
    private sealed class AfterStock
    {
        public Stock stock;
        public string kind;
        public decimal move;
        public readonly Dictionary<string, string> values = new();
    }

    private sealed class AfterThread
    {
        public AfterHoursTopic topic;
        public AfterStock about;
        public int opener, lastReplier, repliesLeft;
        public long openerMessage, lastReply;
        public bool backPending;
        public double nextAt;
        public readonly HashSet<int> speakers = new();
        public readonly HashSet<string> said = new();
    }

    private sealed class AfterRoom
    {
        public AfterThread thread;
        public double nextThreadAt;
        public readonly Queue<string> recentTopics = new();
    }

    private static AfterHoursDocument afterHoursLines;
    private static readonly Regex AfterSlot = new("\\{([A-Za-z]+)\\}", RegexOptions.Compiled);
    private static readonly decimal[] RoundSteps = { 1m, 2m, 5m };

    private const decimal AfterMovePercent = 3m, AfterFlatPercent = 1m, BreakMovePercent = 2m;
    // 한 화제에 달리는 답의 수와 말 사이의 간격(초).
    private const int AfterMaxReplies = 3;
    private const double AfterReplyMin = 3, AfterReplyMax = 8, AfterBackChance = .6, AfterHolderChance = .6;
    // 화제 사이의 간격(초). 깊은 밤(0~6시)에는 뜸해진다. 리딩방은 공개방보다 한산하다.
    private const double AfterGapMin = 7, AfterGapMax = 16, AfterNightGapMin = 25, AfterNightGapMax = 55, AfterRoomGapScale = 2.5;
    private const double AfterStockShare = .5;
    private const int AfterRecentTopics = 4, AfterRecentLines = 60, AfterRecentSpeakers = 8;
    private const double AfterChatterWeight = 5;

    private readonly Dictionary<int, AfterRoom> afterRooms = new();
    private readonly List<AfterStock> afterBoard = new();
    private readonly Queue<string> afterRecentLines = new();
    private readonly Queue<int> afterRecentSpeakers = new();
    private readonly System.Random afterRandom = new(73129);
    private bool afterHoursOpen;

    public int AfterHoursPublished { get; private set; }

    private static AfterHoursDocument AfterHoursLines
    {
        get
        {
            if (afterHoursLines != null)
            { return afterHoursLines; }

            var asset = Resources.Load<TextAsset>("Chat/chat_afterhours");
            var document = asset == null ? null : JsonUtility.FromJson<AfterHoursDocument>(asset.text);
            if (document == null || document.schemaVersion != 1 || document.topics == null)
            { throw new ArgumentException("Invalid after-hours chat catalog"); }

            foreach (var topic in document.topics)
            {
                if (topic == null || string.IsNullOrEmpty(topic.key) || string.IsNullOrEmpty(topic.kind) || topic.open == null || topic.open.Length == 0 ||
                    topic.reply == null || topic.reply.Length == 0 || topic.weight <= 0)
                { throw new ArgumentException("Invalid after-hours chat topic"); }
            }

            afterHoursLines = document;
            return afterHoursLines;
        }
    }

    /// <summary>장이 다시 열리면 부른다. 다음 마감 때 종가를 새로 읽는다.</summary>
    public void EndAfterHours()
    {
        if (!afterHoursOpen)
        { return; }

        afterHoursOpen = false;
        afterBoard.Clear();
        afterRooms.Clear();
    }

    /// <summary>장이 닫힌 뒤 처음 한 번: 오늘의 종가 판을 읽어 둔다. 밤새 이 판을 두고 얘기한다.</summary>
    private void BeginAfterHours(IEnumerable<Stock> stocks)
    {
        afterHoursOpen = true;
        afterBoard.Clear();
        afterRooms.Clear();
        if (stocks == null || ReadMarketDay == null)
        { return; }

        foreach (var stock in stocks)
        {
            if (stock == null || !stock.IsListed || stock.IsDelisted || stock.currentPrice <= 0m)
            { continue; }

            MarketDay day = ReadMarketDay(stock);
            if (day.reference <= 0m)
            { continue; }

            decimal close = stock.currentPrice, move = (close / day.reference - 1m) * 100m;
            var entry = new AfterStock { stock = stock, move = move };
            entry.values["symbol"] = stock.stockName;
            entry.values["close"] = Money(close);
            entry.values["dayPct"] = SignedPercent(move);
            decimal level = CrossedLevel(day.reference, close);
            // 딱 떨어지는 가격을 넘겼어도 제대로 오른 날에만 '돌파'라고 한다.
            if (level > 0m && move >= BreakMovePercent)
            {
                entry.kind = "BREAK";
                entry.values["level"] = Money(level);
                entry.values["nextLevel"] = Money(NextLevel(level));
            }
            else if (move >= AfterMovePercent) { entry.kind = "UP"; }
            else if (move <= -AfterMovePercent) { entry.kind = "DOWN"; }
            else if (Math.Abs(move) < AfterFlatPercent) { entry.kind = "FLAT"; }
            else { continue; }

            afterBoard.Add(entry);
        }
    }

    /// <summary>오늘 종가가 넘어선 가장 높은 딱 떨어지는 가격(1·2·5 × 10ⁿ). 없으면 0.</summary>
    private static decimal CrossedLevel(decimal reference, decimal close)
    {
        decimal best = 0m;
        for (decimal scale = 100m; scale <= close; scale *= 10m)
        {
            foreach (decimal step in RoundSteps)
            {
                decimal level = step * scale;
                if (reference < level && close >= level && level > best) { best = level; }
            }
        }

        return best;
    }

    private static decimal NextLevel(decimal level)
    {
        decimal scale = 1m;
        while (scale * 10m <= level) { scale *= 10m; }
        decimal lead = level / scale;
        return lead < 2m ? 2m * scale : lead < 5m ? 5m * scale : 10m * scale;
    }

    /// <summary>장이 닫혀 있는 동안 부른다. rooms는 대화가 이어질 방(공개방 0과 사람이 있는 리딩방).</summary>
    public void AfterHoursAdvance(IEnumerable<Stock> stocks, IReadOnlyList<int> rooms)
    {
        if (!SocialReady || rooms == null)
        { return; }

        if (!afterHoursOpen) { BeginAfterHours(stocks); }
        double now = SocialTime.Seconds;
        int hour = (int)(SocialTime.GameMinute % 1440d / 60d);
        foreach (int id in rooms)
        {
            if (SocialRoom(id) == null)
            { continue; }

            if (!afterRooms.TryGetValue(id, out var room))
            {
                room = new AfterRoom { nextThreadAt = now + AfterRange(2, AfterGapMax) * (id == 0 ? 1 : AfterRoomGapScale) };
                afterRooms.Add(id, room);
            }

            // 저장한 판을 불러와 시간이 되돌아갔으면 기다리던 것을 버린다.
            if (room.nextThreadAt - now > AfterNightGapMax * AfterRoomGapScale + 1) { room.nextThreadAt = now; room.thread = null; }
            if (room.thread == null)
            {
                if (now >= room.nextThreadAt) { StartAfterThread(id, room, now, hour); }
            }
            else if (now >= room.thread.nextAt || room.thread.nextAt - now > AfterReplyMax + 1)
            {
                ContinueAfterThread(id, room, now, hour);
            }
        }
    }

    private double AfterRange(double min, double max) => min + afterRandom.NextDouble() * (max - min);

    private double AfterGap(int roomId, int hour)
    {
        bool night = hour < 6;
        double gap = night ? AfterRange(AfterNightGapMin, AfterNightGapMax) : AfterRange(AfterGapMin, AfterGapMax);
        return roomId == 0 ? gap : gap * AfterRoomGapScale;
    }

    private static bool InHours(AfterHoursTopic topic, int hour)
        => topic.fromHour <= topic.toHour ? hour >= topic.fromHour && hour < topic.toHour : hour >= topic.fromHour || hour < topic.toHour;

    private void StartAfterThread(int roomId, AfterRoom room, double now, int hour)
    {
        room.nextThreadAt = now + AfterGap(roomId, hour);
        AfterStock about = null;
        AfterHoursTopic topic = null;
        if (afterBoard.Count > 0 && afterRandom.NextDouble() < AfterStockShare)
        {
            about = PickAfterStock(room);
            if (about != null) { topic = PickAfterTopic(room, hour, about.kind, about.stock.symbol); }
        }

        if (topic == null)
        {
            about = null;
            topic = PickAfterTopic(room, hour, "CASUAL", null);
        }

        if (topic == null)
        { return; }

        var opener = PickAfterSpeaker(roomId, null);
        if (opener == null)
        { return; }

        bool morning = hour < 12;
        var lines = morning && topic.openMorning != null && topic.openMorning.Length > 0 ? topic.openMorning : topic.open;
        var thread = new AfterThread { topic = topic, about = about, opener = opener.Social.Id, repliesLeft = 1 + afterRandom.Next(AfterMaxReplies) };
        thread.speakers.Add(thread.opener);
        thread.openerMessage = PublishAfterLine(opener, roomId, thread, "open", PickAfterLine(lines, thread), 0, null);
        thread.lastReply = thread.openerMessage;
        thread.nextAt = now + AfterRange(AfterReplyMin, AfterReplyMax);
        room.thread = thread;
        room.recentTopics.Enqueue(TopicMark(topic, about == null ? null : about.stock.symbol));
        while (room.recentTopics.Count > AfterRecentTopics) { room.recentTopics.Dequeue(); }
    }

    private void ContinueAfterThread(int roomId, AfterRoom room, double now, int hour)
    {
        var thread = room.thread;
        SocialBots.TryGetValue(thread.opener, out var opener);
        if (thread.repliesLeft > 0)
        {
            var replier = PickAfterSpeaker(roomId, thread.speakers);
            if (replier == null || opener == null || !Present(opener, roomId))
            { EndAfterThread(roomId, room, now, hour); return; }

            bool holder = thread.about != null && thread.topic.replyHolder != null && thread.topic.replyHolder.Length > 0 &&
                replier.GetOwnedVolume(thread.about.stock) > 0m && afterRandom.NextDouble() < AfterHolderChance;
            string line = PickAfterLine(holder ? thread.topic.replyHolder : thread.topic.reply, thread);
            thread.speakers.Add(replier.Social.Id);
            thread.lastReplier = replier.Social.Id;
            thread.lastReply = PublishAfterLine(replier, roomId, thread, holder ? "holder" : "reply", line, thread.openerMessage, opener);
            thread.repliesLeft--;
            if (thread.repliesLeft == 0) { thread.backPending = thread.topic.back != null && thread.topic.back.Length > 0 && afterRandom.NextDouble() < AfterBackChance; }
            if (thread.repliesLeft == 0 && !thread.backPending) { EndAfterThread(roomId, room, now, hour); return; }
            thread.nextAt = now + AfterRange(AfterReplyMin, AfterReplyMax);
            return;
        }

        if (thread.backPending && opener != null && Present(opener, roomId) && SocialBots.TryGetValue(thread.lastReplier, out var last))
        { PublishAfterLine(opener, roomId, thread, "back", PickAfterLine(thread.topic.back, thread), thread.lastReply, last); }

        EndAfterThread(roomId, room, now, hour);
    }

    private void EndAfterThread(int roomId, AfterRoom room, double now, int hour)
    {
        room.thread = null;
        room.nextThreadAt = now + AfterGap(roomId, hour);
    }

    private static string TopicMark(AfterHoursTopic topic, string symbol) => symbol == null ? topic.key : topic.key + ":" + symbol;

    private AfterStock PickAfterStock(AfterRoom room)
    {
        // 많이 움직인 종목일수록 입에 오른다. 방금 얘기한 종목은 건너뛴다.
        AfterStock chosen = null;
        double total = 0;
        foreach (var entry in afterBoard)
        {
            bool recent = false;
            foreach (string mark in room.recentTopics) { if (mark.EndsWith(":" + entry.stock.symbol, StringComparison.Ordinal)) { recent = true; break; } }
            if (recent || !entry.stock.IsListed || entry.stock.IsDelisted)
            { continue; }

            double weight = 1 + (double)Math.Min(20m, Math.Abs(entry.move)) + (entry.kind == "BREAK" ? 10 : 0);
            total += weight;
            if (afterRandom.NextDouble() * total < weight) { chosen = entry; }
        }

        return chosen;
    }

    private AfterHoursTopic PickAfterTopic(AfterRoom room, int hour, string kind, string symbol)
    {
        AfterHoursTopic chosen = null;
        double total = 0;
        foreach (var topic in AfterHoursLines.topics)
        {
            if (topic.kind != kind || !InHours(topic, hour))
            { continue; }

            bool recent = false;
            foreach (string mark in room.recentTopics) { if (mark == TopicMark(topic, symbol)) { recent = true; break; } }
            if (recent)
            { continue; }

            total += topic.weight;
            if (afterRandom.NextDouble() * total < topic.weight) { chosen = topic; }
        }

        return chosen;
    }

    /// <summary>반말로 가볍게 말할 사람만 고른다. 수다쟁이는 더 자주 낀다.</summary>
    private TradeBot PickAfterSpeaker(int roomId, HashSet<int> exclude)
    {
        TradeBot chosen = null;
        double total = 0;
        int formal = Config.Int("styleFormalMin");
        foreach (var bot in socialBots)
        {
            if (bot == null || bot.Social == null || !Present(bot, roomId) || exclude != null && exclude.Contains(bot.Social.Id))
            { continue; }

            var mind = bot.Social.Psychology;
            if (mind.Traits.Politeness >= formal || !mind.Chatterbox && afterRecentSpeakers.Contains(bot.Social.Id))
            { continue; }

            double weight = mind.Chatterbox ? AfterChatterWeight : mind.Traits.Serious ? .3 : 1;
            total += weight;
            if (afterRandom.NextDouble() * total < weight) { chosen = bot; }
        }

        return chosen;
    }

    private string PickAfterLine(string[] lines, AfterThread thread)
    {
        // 한 대화 안에서는 같은 문장을 두 번 쓰지 않는다. 그다음으로 최근에 나온 문장을 피한다.
        string chosen = null, spare = null;
        int seen = 0, spares = 0;
        foreach (string line in lines)
        {
            if (thread.said.Contains(line) || !FillsAfterSlots(line, thread))
            { continue; }

            if (afterRecentLines.Contains(line))
            {
                spares++;
                if (afterRandom.Next(spares) == 0) { spare = line; }
                continue;
            }

            seen++;
            if (afterRandom.Next(seen) == 0) { chosen = line; }
        }

        chosen ??= spare ?? lines[0];
        thread.said.Add(chosen);
        afterRecentLines.Enqueue(chosen);
        while (afterRecentLines.Count > AfterRecentLines) { afterRecentLines.Dequeue(); }
        return chosen;
    }

    private static bool FillsAfterSlots(string line, AfterThread thread)
    {
        foreach (Match slot in AfterSlot.Matches(line))
        {
            if (thread.about == null || !thread.about.values.ContainsKey(slot.Groups[1].Value)) { return false; }
        }

        return true;
    }

    private long PublishAfterLine(TradeBot bot, int roomId, AfterThread thread, string part, string phrase, long replyTo, TradeBot target)
    {
        var facts = new SocialFacts { at = SocialTime.GameMinute };
        if (thread.about != null)
        {
            facts.symbol = thread.about.stock.symbol; facts.stockName = thread.about.stock.stockName;
            facts.price = thread.about.values["close"]; facts.held = bot.GetOwnedVolume(thread.about.stock) > 0m;
        }

        var values = new List<SocialSpeechValue>();
        string text = AfterSlot.Replace(phrase, slot =>
        {
            string name = slot.Groups[1].Value, value = thread.about != null && thread.about.values.TryGetValue(name, out string found) ? found : "";
            bool listed = false;
            foreach (var item in values) { if (item.name == name) { listed = true; break; } }
            if (!listed) { values.Add(new SocialSpeechValue { name = name, value = value }); }
            return value;
        });
        string key = "AFTER." + thread.topic.key + "." + part;
        var message = new SocialMessage
        {
            id = ChatSocialIds.Next(), roomId = roomId, senderId = bot.Social.Id, targetId = target == null ? 0 : target.Social.Id,
            at = SocialTime.GameMinute, realAt = SocialTime.Seconds, replyToId = replyTo, facts = facts, topicEvent = "AFTER_HOURS", templateKey = key,
            act = part == "open" ? (thread.about == null ? SocialAct.SMALLTALK : SocialAct.ASK) : part == "back" ? SocialAct.REACT : SocialAct.ANSWER,
            localizedPhrase = phrase, localizedValues = values.ToArray(), localizedTarget = target == null ? null : target.Social.Nickname,
            debug = new SocialMessageDebug { trigger = "AFTER_HOURS", speechKey = key, weights = Array.Empty<SocialWeight>(), slots = facts }
        };
        message.text = target == null ? text : "@" + target.Social.Nickname + " " + text;
        bot.Social.LastSpeech = (float)SocialTime.Seconds;
        afterRecentSpeakers.Enqueue(bot.Social.Id);
        while (afterRecentSpeakers.Count > AfterRecentSpeakers) { afterRecentSpeakers.Dequeue(); }
        AfterHoursPublished++;
        SocialMessagePublished?.Invoke(message);
        return message.id;
    }
}
