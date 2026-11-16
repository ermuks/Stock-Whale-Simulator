using System;
using System.Collections.Generic;

/// <summary>Three preferred subjects, never a hard topic ban. Recent speech decays and hot newcomers can take over.</summary>
public sealed class ChatSocialTopicFocus
{
    private sealed class Topic { public string key; public double score, at, observed; }
    private readonly Dictionary<string, Topic> topics = new();
    private readonly List<Topic> ranking = new();
    private double rankedAt = double.NegativeInfinity;
    public void Observe(string key, double now)
    {
        if (string.IsNullOrEmpty(key)) { return; }
        double memory = ChatSocialConfig.Current.Get("topicMemorySeconds");
        if (!topics.TryGetValue(key, out var topic))
        {
            if (topics.Count >= 32)
            {
                Topic oldest = null;
                foreach (var item in topics.Values) { if (oldest == null || item.observed < oldest.observed) { oldest = item; } }
                topics.Remove(oldest.key);
            }
            topic = new Topic { key = key, at = now }; topics.Add(key, topic);
        }
        topic.score = topic.score * Math.Exp(-Math.Max(0, now - topic.at) / memory) + 1;
        topic.at = topic.observed = now; rankedAt = double.NegativeInfinity;
    }
    private void Rank(double now)
    {
        if (now >= rankedAt && now - rankedAt < 1) { return; }
        rankedAt = now; ranking.Clear();
        double memory = ChatSocialConfig.Current.Get("topicMemorySeconds");
        foreach (var topic in topics.Values)
        {
            topic.score *= Math.Exp(-Math.Max(0, now - topic.at) / memory); topic.at = now;
            if (topic.score >= .1) { ranking.Add(topic); }
        }
        ranking.Sort((a, b) => { int order = b.score.CompareTo(a.score); return order != 0 ? order : string.CompareOrdinal(a.key, b.key); });
    }
    public bool IntroducingAnother(string key, double now)
    {
        Rank(now);
        int count = ChatSocialConfig.Current.Int("topicPreferredCount");
        if (ranking.Count < count || string.IsNullOrEmpty(key)) { return false; }
        for (int i = 0; i < Math.Min(count, ranking.Count); i++) { if (ranking[i].key == key) { return false; } }
        return true;
    }
    public double Weight(string key, double urgency, double now)
    {
        Rank(now);
        if (ranking.Count == 0) { return 1; }
        var config = ChatSocialConfig.Current;
        int count = Math.Min(config.Int("topicPreferredCount"), ranking.Count);
        for (int i = 0; i < count; i++) { if (ranking[i].key == key) { return config.Get("topicContinueWeight"); } }
        double quiet = config.Get("topicNewWeight");
        return quiet + Math.Clamp(urgency, 0, 1) * (config.Get("topicContinueWeight") - quiet);
    }
}

public sealed partial class InvestorSociety
{
    private MarketConversationFocus liveConversationMarket;
    private readonly Dictionary<int, ChatSocialTopicFocus> topicFocus = new();
    public bool LiveConversationEnabled => liveConversationMarket != null;
    public void ConfigureLiveConversation(MarketConversationFocus market) { liveConversationMarket = market; }
    public ChatSocialTopicFocus TopicFocus(int room)
    {
        if (!topicFocus.TryGetValue(room, out var focus)) { focus = new ChatSocialTopicFocus(); topicFocus.Add(room, focus); }
        return focus;
    }
    private double TopicUrgency(Stock stock)
    {
        if (stock == null || liveConversationMarket == null) { return 0; }
        var snapshot = liveConversationMarket.Read(stock, (float)SocialTime.Seconds);
        // Existing heat already combines actual volume and recent price movement relative to each stock's history.
        return Math.Clamp((snapshot.Probability * Math.Max(1, liveConversationMarket.Count) - 1) / 2, 0, 1);
    }
    public Stock ChooseRoomTopic(TradeBot bot, int room, double roll)
    {
        if (liveConversationMarket == null || StockManager.Instance == null) { return null; }
        var focus = TopicFocus(room);
        double total = 0;
        foreach (var stock in StockManager.Instance.stocks) { total += Weight(stock); }
        roll = Math.Clamp(double.IsNaN(roll) ? .5 : roll, 0, .999999999) * total;
        Stock last = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            double weight = Weight(stock);
            if (weight <= 0) { continue; }
            last = stock; roll -= weight;
            if (roll <= 0) { return stock; }
        }
        return last;
        double Weight(Stock stock)
        {
            if (!stock.IsListed || stock.IsDelisted) { return 0; }
            var snapshot = liveConversationMarket.Read(stock, (float)SocialTime.Seconds);
            double urgency = TopicUrgency(stock);
            return Math.Max(.0001, snapshot.Probability) * focus.Weight(stock.symbol, urgency, SocialTime.Seconds) *
                (1 + urgency * Config.Get("topicHotMultiplier"));
        }
    }
    private static SocialRandom PresentationRandom(TradeBot bot, SocialIntent intent, uint salt)
    {
        uint key = unchecked((uint)bot.Social.Id * 486187739u ^ (uint)intent.causeId ^ (uint)(long)(intent.due * 1000) ^ (uint)intent.roomId * 8191u ^ salt);
        key ^= key >> 16; key = unchecked(key * 0x7feb352du); key ^= key >> 15;
        return new SocialRandom(unchecked((int)key));
    }
    private bool HesitateOnNewTopic(TradeBot bot, SocialIntent intent)
    {
        if (LiveConversationEnabled && intent.facts == null && intent.replyToId == 0 && intent.threadId == 0 && intent.targetId == 0)
        {
            var subject = ChooseRoomTopic(bot, intent.roomId, bot.Social.Psychology.SocialRng.Next());
            if (subject != null) { intent.facts = CaptureFacts(bot, subject); }
        }
        if (!LiveConversationEnabled || intent.topicHesitated || intent.replyToId > 0 || intent.threadId > 0 || intent.targetId != 0 ||
            intent.facts == null || intent.facts.directLead || intent.facts.signal || intent.facts.notice == "WATCH_RISE" || intent.facts.notice == "WATCH_FALL" || IsMarketRiskNotice(intent.facts.notice))
        { return false; }
        var stock = FindSocialStock(intent.facts.symbol);
        if (stock == null || !TopicFocus(intent.roomId).IntroducingAnother(stock.symbol, SocialTime.Seconds)) { return false; }
        intent.topicHesitated = true;
        double urgency = TopicUrgency(stock);
        var random = PresentationRandom(bot, intent, 0x6715a9d3u);
        double hesitation = Config.Get("topicHesitationChance") * (1 - urgency) * (1.2 - bot.Social.Psychology.Traits.Sociability / 200d);
        if (random.Next() >= hesitation) { return false; }
        intent.due = SocialTime.Seconds + Config.Get("topicHesitationMinSec") + random.Next() * (Config.Get("topicHesitationMaxSec") - Config.Get("topicHesitationMinSec"));
        bot.Social.Psychology.Intentions.Add(intent);
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "TOPIC_HESITATION", actor = bot.Social.Id, room = intent.roomId, reason = stock.symbol, value = intent.due - SocialTime.Seconds });
        return true;
    }
    private void ObserveConversationTopic(SocialMessage message)
    {
        if (!LiveConversationEnabled) { return; }
        string key = message.act == SocialAct.SMALLTALK ? "__smalltalk" : message.facts?.symbol;
        TopicFocus(message.roomId).Observe(key, SocialTime.Seconds);
    }
}
