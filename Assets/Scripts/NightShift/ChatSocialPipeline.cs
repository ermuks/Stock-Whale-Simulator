using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>The existing society owns the scheduler; this partial isolates deterministic conversation work from Unity frames.</summary>
public sealed partial class InvestorSociety
{
    public bool SocialReady => socialBots.Count > 0;
    public readonly SocialClock SocialTime = new();
    public readonly SocialRoomState PublicSocial = new();
    public readonly Dictionary<int, TradeBot> SocialBots = new();
    private readonly List<TradeBot> socialBots = new();
    private readonly Dictionary<int, double> nextIdle = new();
    private readonly Dictionary<int, SocialAct> lastAct = new();
    private readonly Dictionary<int, int> consecutiveAct = new();
    private readonly Dictionary<long, SocialMessage> socialMessageIndex = new();
    private readonly Queue<long> socialMessageOrder = new();
    private readonly List<(TradeBot bot, SocialIntent intent)> readySocialIntents = new();
    private int socialCursor, knownBotCount = -1;
    public long SocialAttempts { get; private set; }
    public long TemplateBlocked { get; private set; }
    public long SocialPublished { get; private set; }
    public event Action<SocialMessage> SocialMessagePublished;
    public Func<TradeBot, Stock> ChooseSocialTopic;
    public Func<TradeBot, SocialFacts, SocialFacts> RefreshOfferedFacts;
    private static readonly CultureInfo SocialCulture = CultureInfo.InvariantCulture;
    private static readonly Regex SlotPattern = new("\\{([A-Za-z]+)\\}", RegexOptions.Compiled);
    private ChatSocialConfig Config => ChatSocialConfig.Current;
    public void BindSocialBots(IReadOnlyList<TradeBot> bots)
    {
        ChatSocialAssets.EnsureLoaded();
        if (knownBotCount == bots.Count)
        { return; }
        knownBotCount = bots.Count; socialBots.Clear(); SocialBots.Clear();
        foreach (var bot in bots)
        {
            if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
            { continue; }
            socialBots.Add(bot); SocialBots[bot.Social.Id] = bot;
        }
        socialBots.Sort((a, b) => a.Social.Id.CompareTo(b.Social.Id));
        var ids = new List<int>(); foreach (var bot in socialBots) { ids.Add(bot.Social.Id); }
        foreach (var bot in socialBots) { bot.Social.Psychology.SeedFriends(ids); }
    }
    public SocialRoomState SocialRoom(int id) => id == 0 ? PublicSocial : GetRoom(id)?.Social;
    public bool Present(TradeBot bot, int room)
    {
        if (bot == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || room != 0 && bot.Social.Psychology.Room != room)
        { return false; }
        var mind = bot.Social.Psychology;
        if (room == 0 && mind.PublicReturnAt > SocialTime.GameMinute)
        { return false; }
        return SocialRoom(room) != null && !SocialRoom(room).banned.Contains(bot.Social.Id) &&
            (!mind.MutedUntil.TryGetValue(room, out double until) || SocialTime.GameMinute >= until);
    }
    public void SocialAdvance(int dayNumber, double gameMinute, double scaledSeconds, int budget)
    {
        SocialTime.Set(dayNumber, gameMinute, scaledSeconds); ChatSocialAudit.GameMinute = SocialTime.GameMinute;
        if (!SocialReady)
        { return; }
        BeforeSocialTick();
        RefreshChatterboxes(SocialTime.Seconds);
        FlushTyping();
        TickRoomTalk();
        readySocialIntents.Clear();
        int count = Math.Min(Math.Max(1, budget), socialBots.Count);
        for (int i = 0; i < count; i++)
        {
            if (socialCursor >= socialBots.Count) { socialCursor = 0; }
            var bot = socialBots[socialCursor++]; var mind = bot.Social.Psychology;
            BeforeSocialBot(bot);
            if (!bot.isActiveAndEnabled)
            { continue; }
            WitnessRoomTalk(bot);
            double now = SocialTime.Seconds;
            for (int j = mind.Intentions.Count - 1; j >= 0; j--)
            {
                if (mind.Intentions[j].expires < now || !Present(bot, mind.Intentions[j].roomId))
                { ExpireSocialIntent(bot, mind.Intentions[j]); mind.Intentions.RemoveAt(j); }
            }
            if (IsTyping(bot)) { continue; }
            if (!nextIdle.TryGetValue(mind.SocialId, out double idleAt) || now >= idleAt)
            {
                nextIdle[mind.SocialId] = now + Config.Get("idleInterval");
                double chill = SocialRoom(mind.Room).chill;
                double probability = Config.Get("idleChance") * mind.Traits.Sociability / 50d * Math.Max(Config.Get("chillSpeechFloor"), 1 - chill / Config.Get("chillSpeechDivisor"));
                // 수다쟁이는 쉬는 시간대 없이 훨씬 자주 말을 꺼낸다. 방 분위기가 얼어붙으면 같이 줄어든다.
                if (mind.Chatterbox) { probability = now - mind.SocialLastSpeech < ChatterGapSeconds ? 0 : ChatterIdleChance * Math.Max(Config.Get("chillSpeechFloor"), 1 - chill / Config.Get("chillSpeechDivisor")); }
                if (Present(bot, mind.Room) && mind.Intentions.Count == 0 && (mind.Chatterbox || mind.ActiveAt((float)now)) && mind.SocialRng.Next() < probability)
                { Offer(bot, mind.Room, SocialMotive.IDLE); }
            }
            int selected = -1;
            for (int j = 0; j < mind.Intentions.Count; j++)
            {
                var intent = mind.Intentions[j];
                double cooldown = Config.Get("speakCooldownSec") * (intent.motive == SocialMotive.REPLY_OBLIGATION ? Config.Get("replyCooldownScale") : 1);
                if (intent.due > now || now - mind.SocialLastSpeech < cooldown)
                { continue; }
                if (selected < 0 || intent.motive < mind.Intentions[selected].motive)
                { selected = j; }
            }
            if (selected < 0)
            { continue; }
            var chosen = mind.Intentions[selected]; mind.Intentions.RemoveAt(selected);
            readySocialIntents.Add((bot, chosen));
        }
        // A busy early bot must not consume the room quota before another bot's due reply.
        readySocialIntents.Sort((a, b) =>
        {
            // 방이 꽉 차 있어도 수다쟁이의 말은 먼저 나간다. 대신 수다쟁이는 한 번 말하면 정해진 시간 동안 새 말을 꺼내지 않는다.
            int order = b.bot.Social.Psychology.Chatterbox.CompareTo(a.bot.Social.Psychology.Chatterbox);
            if (order == 0) { order = a.intent.motive.CompareTo(b.intent.motive); }
            if (order == 0) { order = a.intent.due.CompareTo(b.intent.due); }
            if (order == 0) { order = a.bot.Social.Psychology.SocialLastSpeech.CompareTo(b.bot.Social.Psychology.SocialLastSpeech); }
            return order != 0 ? order : a.bot.Social.Id.CompareTo(b.bot.Social.Id);
        });
        foreach (var ready in readySocialIntents)
        {
            var bot = ready.bot; var mind = bot.Social.Psychology; var chosen = ready.intent; double now = SocialTime.Seconds;
            var room = SocialRoom(chosen.roomId);
            while (room.sent.Count > 0 && now - room.sent.Peek() >= Config.Get("roomWindowSec")) { room.sent.Dequeue(); }
            if (room.sent.Count >= Config.Int("roomMaxMsgPer10Sec") && !mind.Chatterbox)
            {
                // Pending high priority work is retained; idle overflow is discarded, never burst-published later.
                if (chosen.motive < SocialMotive.WITNESSED_EVENT) { chosen.due = room.sent.Peek() + Config.Get("roomWindowSec"); mind.Intentions.Add(chosen); }
                continue;
            }
            if (HesitateOnNewTopic(bot, chosen) || BeginTyping(bot, chosen)) { continue; }
            GenerateSocialMessage(bot, chosen);
        }
    }
    public void Offer(TradeBot bot, int room, SocialMotive motive, SocialFacts facts = null, SocialAct? forced = null, int target = 0, long cause = 0)
    {
        if (bot?.Social == null || SocialRoom(room) == null)
        { return; }
        var mind = bot.Social.Psychology;
        foreach (var queued in mind.Intentions)
        { if (queued.motive == motive && queued.causeId == cause && queued.roomId == room && queued.forcedAct == forced) { return; } }
        string expiry = motive == SocialMotive.REPLY_OBLIGATION ? "replyExpiry" : motive == SocialMotive.EMOTION_THRESHOLD ? "emotionExpiry" : motive == SocialMotive.SELF_EVENT ? "selfExpiry" : "witnessExpiry";
        mind.Intentions.Add(new SocialIntent { roomId = room, motive = motive, facts = facts, forcedAct = forced, targetId = target, causeId = cause,
            due = SocialTime.Seconds, expires = motive == SocialMotive.IDLE ? double.PositiveInfinity : SocialTime.Seconds + Config.Get(expiry) });
    }
    public void OfferMarketEvent(ChatEvent kind, Stock stock, TradeBot bot)
    {
        if (!SocialReady)
        { return; }
        if (bot == null)
        {
            var chooser = socialBots[socialCursor % socialBots.Count].Social.Psychology.SocialRng;
            bot = socialBots[chooser.Range(0, socialBots.Count)];
        }
        if (!SocialBots.ContainsKey(bot.Social.Id))
        { return; }
        var facts = CaptureFacts(bot, stock);
        facts.sourceEvent = kind.ToString();
        var motive = kind == ChatEvent.ProfitRealized || kind == ChatEvent.LossRealized ? SocialMotive.SELF_EVENT : kind == ChatEvent.SmallTalk ? SocialMotive.IDLE : SocialMotive.WITNESSED_EVENT;
        // 말할 만한 매도였다면 그 매도를 화제로 꺼낸다. 익절은 잘 말하고 손절은 가끔만 말한다. 난수열은 쓰지 않는다.
        SocialAct? forced = null;
        if (motive == SocialMotive.SELF_EVENT && SaleNotable(bot, stock, facts))
        {
            int bravado = bot.Social.Psychology.Traits.Bravado;
            bool gain = kind == ChatEvent.ProfitRealized;
            float chance = gain ? 45f + bravado * .4f : 12f + (100 - bravado) * .2f;
            if (CompanyBusiness.Hash("SALE:" + bot.Social.Id + ":" + stock.symbol + ":" + (long)(SocialTime.Seconds / 30d)) % 100 < chance)
            { forced = gain ? SocialAct.BRAG : SocialAct.LAMENT; }
        }
        Offer(bot, 0, motive, facts, forced);
    }
    public SocialFacts CaptureFacts(TradeBot bot, Stock preferred = null)
    {
        var mind = bot.Social.Psychology;
        Stock stock = preferred ?? ChooseSocialTopic?.Invoke(bot);
        if (stock == null)
        {
            foreach (var pair in bot.Social.Positions)
            { if (bot.GetOwnedVolume(pair.Key) > 0) { stock = pair.Key; break; } }
        }
        if (stock == null && StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
        { stock = StockManager.Instance.stocks[mind.SocialRng.Range(0, StockManager.Instance.stocks.Count)]; }
        var facts = new SocialFacts { at = SocialTime.GameMinute };
        if (stock == null)
        { return facts; }
        facts.symbol = stock.symbol; facts.stockName = stock.stockName; facts.price = Money(stock.currentPrice);
        CaptureMarketScene(stock, facts);
        facts.canPromise = bot.CanMakeSocialPromise && mind.PromisedAction == SocialPromisedAction.None;
        decimal qty = bot.GetOwnedVolume(stock), basis = bot.GetOwnedAverage(stock);
        facts.quantity = Money(qty); facts.held = qty > 0;
        if (qty > 0 && basis > 0)
        {
            decimal profit = (stock.currentPrice - basis) * qty;
            facts.avgPrice = Money(basis); facts.amount = Money(profit); facts.pct = Percent((stock.currentPrice / basis - 1m) * 100m);
            facts.profitable = profit > 0; facts.losing = profit < 0;
            facts.profitKind = "평가";
            facts.notable = Notable(bot, profit, (stock.currentPrice / basis - 1m) * 100m, 1m);
        }
        foreach (var fill in mind.SocialFills)
        {
            if (fill.symbol != stock.symbol || SocialTime.GameMinute - fill.at > Config.Get("tradeFactMinutes"))
            { continue; }
            facts.bought |= fill.bought; facts.sold |= !fill.bought;
            if (qty == 0 && !fill.bought && fill.cost > 0)
            {
                decimal profit = (fill.price - fill.cost) * fill.quantity;
                facts.amount = Money(profit); facts.pct = Percent((fill.price / fill.cost - 1m) * 100m);
                facts.profitable = profit > 0; facts.losing = profit < 0; facts.profitKind = "실현";
                facts.notable = Notable(bot, profit, (fill.price / fill.cost - 1m) * 100m, .5m);
            }
        }
        int today = (int)(SocialTime.GameMinute / 1440d) + 1;
        facts.dailyProfit = bot.DailySettledDay == today && bot.DailyReturn > 0;
        facts.dailyLoss = bot.DailySettledDay == today && bot.DailyReturn < 0;
        if (bot.Social.Positions.TryGetValue(stock, out var memory))
        {
            double age = SocialTime.Seconds - memory.SellTime;
            bool recent = age >= 0 && age <= Config.Get("legacyTradeSeconds");
            facts.recentSellProfit = recent && memory.SellProfit > 0; facts.recentSellLoss = recent && memory.SellProfit < 0;
            facts.sellThenRise = recent && memory.SellPrice > 0 && stock.currentPrice >= memory.SellPrice * (1m + (decimal)Config.Get("legacyAfterSellRatio"));
            facts.recovered = facts.held && stock.currentPrice >= basis && SocialTime.Seconds - memory.CrossTime <= Config.Get("legacyEventSeconds");
        }
        facts.stressRelief = bot.Social.LastDropStock == stock && SocialTime.Seconds - bot.Social.LastDropTime <= Config.Get("legacyEventSeconds") && bot.Social.LastDropAmount >= Config.Get("legacyStressDrop");
        return facts;
    }
    public static string Money(decimal value) => value.ToString("#,##0", SocialCulture);
    public static string Percent(decimal value) => value.ToString("+0.0;-0.0;0.0", SocialCulture);
    /// <summary>
    /// 말할 만한 손익인가. 자기 자산에 비해 의미 있는 금액이고 수익률도 눈에 띄어야 한다.
    /// 허세가 큰 봇은 기준이 낮다(자산의 0.3%·수익률 2%부터), 진지한 봇은 높다(자산의 1%·수익률 6%).
    /// </summary>
    private static bool Notable(TradeBot bot, decimal profit, decimal percent, decimal scale)
    {
        decimal equity = bot.Equity();
        if (equity <= 0m || profit == 0m)
        { return false; }
        int bravado = bot.Social.Psychology.Traits.Bravado;
        decimal asset = Math.Abs(profit) / equity * 100m;
        decimal minimumAsset = (.3m + (100 - bravado) * .007m) * scale, minimumReturn = (2m + (100 - bravado) * .04m) * scale;
        return asset >= minimumAsset && Math.Abs(percent) >= minimumReturn || asset >= minimumAsset * 5m;
    }
    /// <summary>방금 한 매도 묶음이 말할 만한가(보유 평가 손익보다 기준을 절반으로 둔다).</summary>
    private static bool SaleNotable(TradeBot bot, Stock stock, SocialFacts facts)
    {
        if (stock == null || !bot.Social.Positions.TryGetValue(stock, out var sale) || sale.SellVolume < 1m || sale.SellPrice <= 0m)
        { return false; }
        decimal cost = sale.SellPrice * sale.SellVolume - sale.SellProfit;
        facts.saleNotable = cost > 0m && Notable(bot, sale.SellProfit, sale.SellProfit / cost * 100m, .5m);
        return facts.saleNotable;
    }
    private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        if (topic == null && LiveConversationEnabled)
        { topic = ChooseRoomTopic(bot, intent.roomId, mind.SocialRng.Next()); }
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        bool saleEvent = facts.sourceEvent == "ProfitRealized" || facts.sourceEvent == "LossRealized";
        if (saleEvent) { SaleNotable(bot, topic, facts); }
        // 답글은 상대의 알림 종류(FOMO·관찰 등)를 물려받지 않는다. 물려받으면 답글용 문장이 전부 걸러진다.
        facts.notice = intent.replyToId != 0 ? null : intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
        if (!RefreshMarketRiskFacts(bot, intent, facts)) { return; }
        if (!PreserveCompanyNews(bot, intent, facts)) { return; }
        if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
        // 남의 리딩 성과에 답할 때 그 사람의 손익을 내 것처럼 말하지 않는다.
        if (intent.facts != null && intent.facts.directLead && intent.replyToId == 0) { facts = intent.facts; }
        if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
        { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
        PrepareSocialFacts(bot, intent, ref facts);
        if (intent.replyToId != 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var answered) && answered.facts != null)
        { facts.parentNews = answered.facts.companyNewsId > 0 || !string.IsNullOrEmpty(answered.facts.newsHeadline); }
        var candidates = BuildSocialWeights(bot, intent, facts);
        SocialAttempts++;
        var debugWeights = candidates.ToArray();
        while (candidates.Count > 0)
        {
            double total = 0; foreach (var candidate in candidates) { total += candidate.value; }
            if (total <= 0)
            { return; }
            double roll = mind.SocialRng.Next() * total; int chosen = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++) { roll -= candidates[i].value; if (roll <= 0) { chosen = i; break; } }
            var selection = candidates[chosen]; candidates.RemoveAt(chosen);
            if (!Enum.TryParse<SocialAct>(selection.key, out var act))
            { return; }
            if (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER || act == SocialAct.CALL_PLAYER) { intent.targetId = -1; }
            // 상대의 말과 이어지지 않는 혼잣말(자랑·한탄·반응·잡담)에는 @상대를 붙이지 않는다.
            else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT || act == SocialAct.BRAG || act == SocialAct.SMALLTALK)) { intent.targetId = 0; }
            else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
            var templates = EligibleTemplates(bot, intent, facts, act);
            if (saleEvent && (act == SocialAct.BRAG || act == SocialAct.LAMENT))
            {
                // 매도를 계기로 한 말이면 매도를 말하는 문장을 고른다. 그런 문장이 없고 평가 손익도 말할 만하지 않으면 이 말은 하지 않는다.
                string wanted = "event:" + facts.sourceEvent;
                var about = templates.FindAll(t => t.requires != null && Array.IndexOf(t.requires, wanted) >= 0);
                if (about.Count > 0) { templates = about; }
                else if (!facts.notable) { templates.Clear(); }
            }
            if (templates.Count == 0)
            { RecordTemplateMiss(act); continue; }
            var template = templates[mind.SocialRng.Range(0, templates.Count)];
            var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
                promisedAction = template.promisedAction,
                debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
            string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
            message.text = RenderSocialText(mind, template, message, parentMessage, target, room.recent);
            // 누구의 말에 다는 답이 아닌 질문은 방 전체에 묻는 문장이다("혹시 OO 보는 사람 있어…?"). 그런 말 앞에 @상대를 붙이면 한 사람을 불러 놓고 모두에게 묻는 꼴이 된다.
            // 표시만 바꾼다. 누구에게 향한 말인지(targetId)는 그대로 둔다.
            bool toRoom = act == SocialAct.ASK && message.replyToId == 0;
            if (message.targetId != 0 && !toRoom) { message.text = "@" + target + " " + message.text; }
            message.profitCard = CaptureProfitCard(bot, message);
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            mind.RecentSpeechKeys.Enqueue(message.debug.speechKey ?? "");
            while (mind.RecentSpeechKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentSpeechKeys.Dequeue(); }
            room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
            room.sent.Enqueue(SocialTime.Seconds);
            consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
            lastAct[mind.SocialId] = act;
            socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
            while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
            ObserveConversationTopic(message);
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
    private List<SocialWeight> BuildSocialWeights(TradeBot bot, SocialIntent intent, SocialFacts facts)
    {
        var result = new List<SocialWeight>(); var mind = bot.Social.Psychology;
        SocialActTable table = null;
        foreach (var row in ChatSocialAssets.Acts.motives) { if (row.key == intent.motive.ToString()) { table = row; break; } }
        SelectSocialReplyTable(intent, ref table);
        if (table == null)
        { return result; }
        var entries = intent.forcedAct.HasValue ? new[] { new SocialWeight { key = intent.forcedAct.Value.ToString(), value = 1 } } : table.weights;
        foreach (var entry in entries)
        {
            if (!Enum.TryParse<SocialAct>(entry.key, out var act)) { result.Add(new SocialWeight { key = entry.key, value = entry.value }); continue; }
            double weight = entry.value;
            // 자랑·한탄은 말할 만한 손익일 때만. 1,800원 벌었다고 자랑하지 않는다.
            bool worthBrag = facts.profitable && (facts.notable || facts.directLead) || facts.sourceEvent == "ProfitRealized" && facts.recentSellProfit && facts.saleNotable;
            bool worthLament = facts.losing && (facts.notable || facts.directLead) || facts.sourceEvent == "LossRealized" && facts.recentSellLoss && facts.saleNotable;
            if (act == SocialAct.BRAG && !worthBrag || act == SocialAct.LAMENT && !worthLament ||
                (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER) && !facts.directLead && facts.notice != "KICK_WARNING" && facts.notice != "LEFT_WARNING" ||
                (act == SocialAct.ANSWER || act == SocialAct.AGREE || act == SocialAct.REBUT || act == SocialAct.MOCK || act == SocialAct.INSULT || act == SocialAct.COMFORT || act == SocialAct.APOLOGIZE || act == SocialAct.MEDIATE) && intent.targetId == 0)
            { weight = 0; }
            if (act == SocialAct.BRAG || act == SocialAct.MOCK) { weight *= .5 + mind.Traits.Bravado / 100d; }
            if (act == SocialAct.MEDIATE) { weight *= mind.Traits.Sociability / 100d * (1.5 - mind.Traits.Temper / 100d); }
            if (act == SocialAct.LAMENT) { weight *= .5 + mind.Fear / 100d; }
            if (act == SocialAct.PRAISE_PLAYER) { weight *= Math.Max(Config.Get("chillSpeechFloor"), 1 - SocialRoom(intent.roomId).chill / Config.Get("chillSpeechDivisor")); }
            if (act == SocialAct.MOCK || act == SocialAct.INSULT || act == SocialAct.REBUT) { weight *= 1 - SocialRoom(intent.roomId).discipline / Config.Get("attackDisciplineDivisor"); }
            if (lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act && consecutiveAct[mind.SocialId] >= Config.Int("maxConsecutiveAct") && !(intent.threadId > 0 && (act == SocialAct.MOCK || act == SocialAct.REBUT)))
            { weight = 0; }
            AdjustSocialWeight(bot, intent, act, ref weight);
            if (weight > 0) { result.Add(new SocialWeight { key = entry.key, value = weight }); }
        }
        return result;
    }
    private List<SocialTemplate> EligibleTemplates(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act)
    {
        var result = new List<SocialTemplate>(); var mind = bot.Social.Psychology;
        string style = mind.Traits.Politeness >= Config.Int("styleFormalMin") ? "formal" : mind.Traits.Politeness >= Config.Int("styleCasualMin") ? "casual" : "rough";
        double emotion = act == SocialAct.BRAG ? mind.Greed : act == SocialAct.LAMENT ? mind.Fear : 0;
        PrepareSocialStyle(bot, intent, act, ref style, ref emotion);
        int intensity = emotion >= Config.Get("intensityThree") ? 3 : emotion >= Config.Get("intensityTwo") ? 2 : 1;
        foreach (var template in ChatSocialAssets.Candidates(act, style, intensity, "plain"))
        {
            if (template.act != act.ToString() || template.style != style || template.intensity != intensity || template.variant != "plain" || facts.signal != (template.requires != null && Array.IndexOf(template.requires, "signal") >= 0))
            { continue; }
            if (mind.RecentTemplateKeys.Contains(template.key))
            { continue; }
            if (template.promisedAction != SocialPromisedAction.None && (!facts.canPromise || template.promisedAction == SocialPromisedAction.Sell && !facts.held))
            { continue; }
            if (facts.notice != null && (template.requires == null || Array.IndexOf(template.requires, "notice:" + facts.notice) < 0))
            { continue; }
            bool repeat = false; foreach (var recent in SocialRoom(intent.roomId).recent) { if (recent.templateKey == template.key) { repeat = true; break; } }
            if (repeat || !TemplateFactsMatch(template, facts))
            { continue; }
            bool missing = false;
            foreach (Match match in SlotPattern.Matches(template.text))
            { if (string.IsNullOrEmpty(Slot(facts, match.Groups[1].Value, intent.targetId == 0 ? "" : "target"))) { missing = true; break; } }
            if (!missing) { result.Add(template); }
        }
        PrepareMemoryTemplates(bot, intent, facts, act, style, intensity, result);
        return result;
    }
    private static bool TemplateFactsMatch(SocialTemplate template, SocialFacts facts)
        => SpeechRequirementsMatch(template.requires, facts);
    private static bool SpeechRequirementsMatch(string[] requirements, SocialFacts facts)
    {
        if (requirements == null)
        { return true; }
        foreach (string requirement in requirements)
        {
            if (requirement.StartsWith("notice:", StringComparison.Ordinal))
            { if (facts.notice != requirement.Substring(7)) { return false; } continue; }
            if (requirement.StartsWith("event:", StringComparison.Ordinal))
            { if (facts.sourceEvent != requirement.Substring(6)) { return false; } continue; }
            if (requirement.StartsWith("memory:", StringComparison.Ordinal))
            { if (facts.memoryKind != requirement.Substring(7)) { return false; } continue; }
            bool valid = requirement switch
            {
                "held" or "UnrealizedProfitPositive" => facts.held && (requirement == "held" || facts.profitable),
                "UnrealizedProfitNegative" => facts.held && facts.losing,
                "bought" => facts.bought, "sold" => facts.sold, "profit" => facts.profitable, "loss" => facts.losing,
                "leadGain" => facts.directLead && facts.profitable, "leadLoss" => facts.directLead && facts.losing,
                "notHeld" => !facts.held, "RecentProfitableSell" => facts.recentSellProfit, "RecentLosingSell" => facts.recentSellLoss,
                "RecentSellThenPriceRise" => facts.sellThenRise, "DailyReturnPositive" => facts.dailyProfit,
                "DailyReturnNegative" => facts.dailyLoss, "CrossedUnrealizedLossToNonNegative" => facts.recovered,
                "RecentStressDrop" => facts.stressRelief, "signal" => facts.signal, "parentNews" => facts.parentNews,
                _ => false
            };
            if (!valid) { return false; }
        }
        return true;
    }
    private static string Slot(SocialFacts f, string slot, string target) => slot switch
    {
        "symbol" or "stockName" => f.stockName ?? f.symbol, "target" => target, "pct" => f.pct, "amount" => f.amount,
        "avgPrice" => f.avgPrice, "price" => f.price, "quantity" => f.quantity, "profitKind" => f.profitKind, "memSymbol" => f.memSymbol, "memPct" => f.memPct, "memWhen" => f.memWhen,
        "bidStatus" => f.bidStatus, "askStatus" => f.askStatus, "signalResult" => f.signalResult, "departedName" => f.departedName, "memAmount" => f.memAmount,
        "seenPrice" => f.observationPrice, "movePct" => f.observationPercent,
        "dayPct" => f.dayPct, "dayHigh" => f.dayHigh, "dayLow" => f.dayLow, "fastPct" => f.fastPct, "fromHigh" => f.fromHigh, "fromLow" => f.fromLow,
        "riskLabel" => IsMarketRiskNotice(f.notice) ? MarketRiskLabel(f.riskGrade) : null,
        "newsHeadline" => f.newsHeadline,
        "seenMinutes" => f.observationPrice != null ? (f.observedAt - f.observationAt).ToString("0.#", SocialCulture) : null, _ => null
    };
    private Stock FindSocialStock(string symbol)
    {
        if (string.IsNullOrEmpty(symbol) || StockManager.Instance == null)
        { return null; }
        foreach (var stock in StockManager.Instance.stocks) { if (stock.symbol == symbol) { return stock; } }
        return null;
    }
    private bool ValidateSocialIntent(TradeBot bot, SocialIntent intent)
    {
        if (intent.targetId > 0 && (!SocialBots.TryGetValue(intent.targetId, out var target) || !Present(target, intent.roomId)))
        { return false; }
        bool valid = true; CheckSocialThread(bot, intent, ref valid); return valid;
    }
    partial void BeforeSocialTick();
    partial void BeforeSocialBot(TradeBot bot);
    partial void ExpireSocialIntent(TradeBot bot, SocialIntent intent);
    partial void SelectSocialReplyTable(SocialIntent intent, ref SocialActTable table);
    partial void AdjustSocialWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight);
    partial void PrepareSocialFacts(TradeBot bot, SocialIntent intent, ref SocialFacts facts);
    partial void PrepareSocialStyle(TradeBot bot, SocialIntent intent, SocialAct act, ref string style, ref double emotion);
    partial void PrepareMemoryTemplates(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act, string style, int intensity, List<SocialTemplate> result);
    partial void CheckSocialThread(TradeBot bot, SocialIntent intent, ref bool valid);
    partial void BeforeSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message);
    partial void AfterSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message);
}

[Serializable] public sealed class SocialTradeFact
{
    public string symbol;
    public bool bought;
    public decimal price, quantity, cost;
    public double at;
}
public sealed partial class InvestorPsychology
{
    public readonly Queue<SocialTradeFact> SocialFills = new();
    public void RecordSocialFill(SocialTradeFact fact)
    {
        ObserveFomoFill(fact);
        SocialFills.Enqueue(fact);
        while (SocialFills.Count > ChatSocialConfig.Current.Int("tradeFactCapacity") || SocialFills.Count > 0 && fact.at - SocialFills.Peek().at > ChatSocialConfig.Current.Get("tradeFactMinutes"))
        { SocialFills.Dequeue(); }
    }
}
