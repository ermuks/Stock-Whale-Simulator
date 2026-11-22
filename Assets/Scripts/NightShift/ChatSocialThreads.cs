using System;
using System.Collections.Generic;

public sealed partial class InvestorSociety
{
    private sealed class ReplyWait
    {
        public SocialMessage message;
        public double expires;
        public bool answered, resolved;
    }
    private readonly Dictionary<long, ReplyWait> replyWaits = new();
    private readonly Dictionary<int, long> pendingReplies = new();
    private readonly Dictionary<int, long> pendingOrigins = new();
    private readonly Dictionary<long, SocialThread> socialThreads = new();
    private readonly Dictionary<(int, int), double> pairCooldown = new();
    private readonly List<long> expiredReplies = new();
    private long nextThread;
    public long DirectedMessages { get; private set; }
    public long AnsweredCalls { get; private set; }
    public readonly List<SocialThread> ClosedThreads = new();
    public Func<TradeBot, double> WitnessChance;
    public SocialThread FindSocialThread(long id) => socialThreads.TryGetValue(id, out var thread) ? thread : null;
    private static bool Attack(SocialAct act) => act == SocialAct.MOCK || act == SocialAct.REBUT || act == SocialAct.INSULT;
    private static (int, int) Pair(int a, int b) => a < b ? (a, b) : (b, a);
    private bool CanStartWith(TradeBot sender, TradeBot other, int room)
    {
        if (other == sender || !Present(other, room) || other.Social.Psychology.ActiveThread != 0 || pendingReplies.ContainsKey(other.Social.Id) || pendingOrigins.ContainsKey(other.Social.Id))
        { return false; }
        return !pairCooldown.TryGetValue(Pair(sender.Social.Id, other.Social.Id), out double until) || SocialTime.Seconds >= until;
    }
    private int ChooseTarget(TradeBot bot, int room, SocialFacts facts, out SocialMessage about)
    {
        about = null;
        var recent = SocialRoom(room).recent.ToArray();
        // 방금 올라온 말 가운데 이어 말할 거리가 있는 것(종목이 있는 말)에 답한다.
        // 예전에는 가장 최근 글이면 몇 분 전 것이든 화제가 없는 것이든 골랐고, 화제가 없으면 답글을 접었다.
        // 먼저 아직 아무도 받지 않은 물음·의심("이거 왜 오르는 거임?")을 찾아 답한다. 없으면 다른 말에 붙는다.
        // 맞장구·위로·사과처럼 얘기를 맺는 말에는 새로 붙지 않는다(예전에는 "나도 그렇게 봄"에 또 "나도"가 줄줄이 달렸다).
        double freshness = Config.Get("replyExpiry") * 1.5;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = recent.Length - 1; i >= 0; i--)
            {
                var said = recent[i];
                if (SocialTime.Seconds - said.realAt > freshness) { break; }
                if (string.IsNullOrEmpty(said.facts?.symbol)) { continue; }
                bool open = (said.act == SocialAct.ASK || said.act == SocialAct.DOUBT) && said.threadId == 0 && said.replyToId == 0;
                bool closes = said.act == SocialAct.AGREE || said.act == SocialAct.COMFORT || said.act == SocialAct.APOLOGIZE || said.act == SocialAct.MEDIATE || said.act == SocialAct.FAREWELL;
                if (pass == 0 ? !open : open || closes) { continue; }
                if (SocialBots.TryGetValue(said.senderId, out var other) && CanStartWith(bot, other, room))
                { about = said; return other.Social.Id; }
            }
        }
        var stock = FindSocialStock(facts.symbol);
        if (stock != null && facts.held)
        {
            foreach (var other in socialBots)
            { if (CanStartWith(bot, other, room) && other.GetOwnedVolume(stock) > 0) { return other.Social.Id; } }
        }
        int best = 0; double score = double.MinValue;
        foreach (var other in socialBots)
        {
            if (!CanStartWith(bot, other, room))
            { continue; }
            bot.Social.Psychology.Relations.TryGetValue(other.Social.Id, out var relation);
            double value = relation == null ? 0 : Math.Abs(relation.affinity) + relation.anger;
            if (value > score) { score = value; best = other.Social.Id; }
        }
        return best;
    }
    private int ActiveThreads(int room, bool argumentsOnly = false)
    {
        int count = 0;
        foreach (var thread in SocialRoom(room).threads)
        { if (thread.Active && (!argumentsOnly || thread.kind == SocialThreadKind.ARGUMENT)) { count++; } }
        return count;
    }
    partial void PrepareSocialFacts(TradeBot bot, SocialIntent intent, ref SocialFacts facts)
    {
        var mind = bot.Social.Psychology;
        if (facts.directLead && intent.replyToId == 0) { intent.targetId = -1; }
        if (intent.targetId == 0 && intent.threadId == 0 && mind.ActiveThread == 0 && !pendingReplies.ContainsKey(mind.SocialId) && !pendingOrigins.ContainsKey(mind.SocialId) &&
            ActiveThreads(intent.roomId) < Config.Int("maxThreads") &&
            mind.SocialRng.Next() < (mind.Chatterbox ? Math.Max(ChatterInitiateChance, Config.Get("initiateTargetChance")) : Config.Get("initiateTargetChance")))
        {
            intent.targetId = ChooseTarget(bot, intent.roomId, facts, out var about);
            if (about != null)
            {
                // 방금 말한 사람에게 말을 걸 때는 그 사람이 한 말에 답한다: 같은 종목을 화제로 삼고, 그 말의 종류에 맞는 답을 고른다.
                var subject = FindSocialStock(about.facts?.symbol);
                if (subject != null && socialMessageIndex.ContainsKey(about.id) && !facts.directLead && !facts.signal && facts.notice == null)
                {
                    intent.replyToId = about.id;
                    facts = CaptureFacts(bot, subject);
                }
                else
                {
                    // 이어 말할 화제가 없으면 이름을 부르지 않는다.
                    intent.targetId = 0;
                }
            }
        }
        PrepareMemoryFacts(bot, intent, facts);
    }
    partial void SelectSocialReplyTable(SocialIntent intent, ref SocialActTable table)
    {
        if (intent.replyToId == 0 || !socialMessageIndex.TryGetValue(intent.replyToId, out var parent))
        { return; }
        foreach (var candidate in ChatSocialAssets.Acts.replies)
        { if (candidate.key == parent.act.ToString()) { table = candidate; return; } }
    }
    partial void AdjustSocialWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight)
    {
        var mind = bot.Social.Psychology;
        var relation = intent.targetId > 0 ? mind.Relation(intent.targetId) : null;
        double affinity = relation?.affinity ?? 0;
        if (act == SocialAct.AGREE || act == SocialAct.COMFORT) { weight *= Math.Max(Config.Get("relationWeightFloor"), 1 + affinity / 100d); }
        if (act == SocialAct.MOCK || act == SocialAct.DOUBT) { weight *= Math.Max(Config.Get("relationWeightFloor"), 1 - affinity / 100d); }
        if (intent.motive == SocialMotive.REPLY_OBLIGATION && (act == SocialAct.AGREE || act == SocialAct.APOLOGIZE)) { weight *= 1.5 - mind.Traits.Temper / 100d; }
        if (act == SocialAct.INSULT && (relation?.anger ?? 0) < Config.Get("angerStage3")) { weight = 0; }
        if (Attack(act) && ActiveThreads(intent.roomId, true) >= Config.Int("maxArguments"))
        {
            var thread = FindSocialThread(intent.threadId);
            bool secondAttack = thread != null ? thread.kind != SocialThreadKind.ARGUMENT && thread.attacks > 0 :
                intent.replyToId > 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var parent) && Attack(parent.act);
            if (secondAttack) { weight = 0; }
        }
        // 맞장구·답변·위로·사과는 받을 말이 있을 때만 한다. 아무 말도 하지 않은 사람을 불러 "맞는 말임" 하지 않는다.
        if ((act == SocialAct.AGREE || act == SocialAct.ANSWER || act == SocialAct.COMFORT || act == SocialAct.APOLOGIZE) && intent.replyToId == 0 && intent.threadId == 0) { weight = 0; }
        if (intent.replyToId > 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var replied))
        {
            // 되묻는 말은 그 말에 맞춰 쓴 물음이 있을 때만 한다(자랑에 "얼마에 들어갔어?", 시세 얘기에 "어디까지 갈 거 같음?").
            // 없으면 방 전체에 던지는 물음이 한 사람에게 붙어서 나간다.
            if (act == SocialAct.ASK && ChatSocialAssets.Community(SocialAct.ASK, replied.act.ToString()) == null) { weight = 0; }
            // 딴 얘기(잡담)는 잡담이나 놀림에만 붙는다. 답변에 대고 "의자에서 일어났더니 다리가…" 하지 않는다.
            if (act == SocialAct.SMALLTALK && replied.act != SocialAct.SMALLTALK && replied.act != SocialAct.MOCK) { weight = 0; }
        }
        AdjustAngerWeight(bot, intent, act, ref weight);
        AdjustQuadrantWeight(bot, intent, act, ref weight);
        if (act == SocialAct.MEDIATE && (intent.threadId == 0 || FindSocialThread(intent.threadId)?.kind != SocialThreadKind.ARGUMENT)) { weight = 0; }
        // 의심은 상대가 무언가를 주장했을 때만 한다(수익 자랑, 답변, 반박, 리딩 평가, 소식 등). 혼잣말이나 감탄에 "근거가 뭐냐"고 묻지 않는다.
        // 누구를 지목하지 않고 종목을 두고 혼자 하는 의심("더 갈 근거가 있나")은 막지 않는다. 그런 말에 남이 반박하면서 대화와 다툼이 시작된다.
        // 방장을 향한 의심(리딩 결과를 겪고 나서)도 막지 않는다. 막는 것은 다른 참가자의 주장 아닌 말에 붙는 의심뿐이다.
        if (act == SocialAct.DOUBT && intent.threadId == 0 && (intent.replyToId > 0 || intent.targetId > 0))
        {
            bool claim = intent.replyToId > 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var said) &&
                (said.act == SocialAct.BRAG || said.act == SocialAct.ANSWER || said.act == SocialAct.REBUT || said.act == SocialAct.AGREE || said.act == SocialAct.PRAISE_PLAYER || said.act == SocialAct.WARN_PLAYER ||
                 said.facts != null && (said.facts.companyNewsId > 0 || !string.IsNullOrEmpty(said.facts.newsHeadline) || said.facts.signal || said.facts.directLead));
            if (!claim) { weight = 0; }
        }
        // 위로는 상대가 힘들다는 말을 했을 때만 한다(한탄, 손실 중, 손절, 팔고 나서 오름, 못 타서 조급함). 가벼운 혼잣말에는 위로하지 않는다.
        if (act == SocialAct.COMFORT && intent.replyToId > 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var heard))
        {
            var said = heard.facts;
            bool hardship = heard.act == SocialAct.LAMENT || said != null &&
                (said.losing || said.recentSellLoss || said.sellThenRise || said.notice == "FOMO" || said.notice == "WATCH_RISE");
            if (!hardship) { weight = 0; }
        }
        // 답변은 질문이나 의심에만 한다.
        if (act == SocialAct.ANSWER && intent.replyToId > 0 && socialMessageIndex.TryGetValue(intent.replyToId, out var asked) && asked.act != SocialAct.ASK && asked.act != SocialAct.DOUBT) { weight = 0; }
    }
    partial void CheckSocialThread(TradeBot bot, SocialIntent intent, ref bool valid)
    {
        if (intent.threadId == 0)
        {
            if (intent.replyToId > 0 && (bot.Social.Psychology.ActiveThread != 0 || ActiveThreads(intent.roomId) >= Config.Int("maxThreads"))) { valid = false; }
            if (intent.replyToId > 0 && intent.targetId > 0 && SocialBots.TryGetValue(intent.targetId, out var origin) && origin.Social.Psychology.ActiveThread != 0) { valid = false; }
            return;
        }
        var thread = FindSocialThread(intent.threadId);
        valid &= thread != null && thread.Active && thread.turnCount < thread.maxTurns &&
            (bot.Social.Psychology.ActiveThread == 0 || bot.Social.Psychology.ActiveThread == thread.id);
    }
    partial void BeforeSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message)
    {
        SocialThread thread = message.threadId > 0 ? FindSocialThread(message.threadId) : null;
        if (message.replyToId > 0 && socialMessageIndex.TryGetValue(message.replyToId, out var parent))
        {
            if (replyWaits.TryGetValue(parent.id, out var wait) && !wait.answered && parent.targetId == bot.Social.Id)
                { wait.answered = true; AnsweredCalls++; pendingReplies.Remove(bot.Social.Id); pendingOrigins.Remove(parent.senderId); }
            if (thread == null && SocialBots.TryGetValue(parent.senderId, out var origin))
            {
                thread = new SocialThread { id = ++nextThread, roomId = message.roomId, parentMessage = parent.id, turnCount = 1,
                    kind = parent.act == SocialAct.ASK || parent.act == SocialAct.DOUBT ? SocialThreadKind.QUESTION : SocialThreadKind.CHAT,
                    attacks = Attack(parent.act) ? 1 : 0, lastAt = SocialTime.Seconds, symbol = parent.facts?.symbol,
                    initialPrice = FindSocialStock(parent.facts?.symbol)?.currentPrice ?? 0 };
                thread.maxTurns = DrawTurns(bot, thread.kind); thread.participants.Add(parent.senderId); thread.participants.Add(message.senderId);
                socialThreads[thread.id] = thread; SocialRoom(message.roomId).threads.Add(thread); parent.threadId = thread.id;
                origin.Social.Psychology.ActiveThread = thread.id;
                ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "THREAD_START", id = thread.id, room = thread.roomId, actor = parent.senderId, target = message.senderId });
            }
        }
        if (thread != null)
        {
            message.threadId = thread.id; thread.turnCount++; thread.lastAt = SocialTime.Seconds;
            if (!thread.participants.Contains(message.senderId)) { thread.participants.Add(message.senderId); }
            bot.Social.Psychology.ActiveThread = thread.id;
            if (Attack(message.act)) { thread.attacks++; }
            if (thread.attacks >= Config.Int("argumentAttackCount") && thread.kind != SocialThreadKind.ARGUMENT && ActiveThreads(thread.roomId, true) < Config.Int("maxArguments"))
            {
                thread.kind = SocialThreadKind.ARGUMENT; thread.maxTurns = DrawTurns(bot, thread.kind);
                thread.maxTurns = Math.Max(thread.turnCount, thread.maxTurns);
                ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "ARGUMENT", id = thread.id, room = thread.roomId, actor = message.senderId, target = message.targetId });
            }
        }
        BeforeEmotionPublish(bot, intent, message);
    }
    private int DrawTurns(TradeBot bot, SocialThreadKind kind)
    {
        string prefix = kind == SocialThreadKind.ARGUMENT ? "argument" : kind == SocialThreadKind.QUESTION ? "question" : "chat";
        return bot.Social.Psychology.SocialRng.Range(Config.Int(prefix + "TurnsMin"), Config.Int(prefix + "TurnsMax") + 1);
    }
    partial void AfterSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message)
    {
        var thread = FindSocialThread(message.threadId);
        var witnesses = new List<TradeBot>();
        foreach (var observer in socialBots)
        {
            if (observer == bot || !Present(observer, message.roomId) || !observer.Social.Psychology.ActiveAt((float)SocialTime.Seconds))
            { continue; }
            var mind = observer.Social.Psychology;
            mind.SocialLastRead[message.roomId] = message.id;
            double chance = WitnessChance?.Invoke(observer) ?? Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + mind.Activity * Config.Get("witnessActivityScale"));
            if (observer.Social.Id != message.targetId && mind.SocialRng.Next() >= chance)
            { continue; }
            witnesses.Add(observer);
        }
        ApplySocialEffects(bot, message, witnesses);
        ReactToRejectedMediation(message, thread, witnesses);
        // 방 전체에 던진 물음("혹시 OO 보는 사람 있어…?")은 누구도 답할 까닭이 없어 거의 늘 묻혔다. 들은 사람 가운데 하나가 답할 마음을 먹는다.
        if (message.act == SocialAct.ASK && message.targetId == 0 && message.replyToId == 0 && thread == null) { OfferRoomAnswer(bot, message, witnesses); }
        if (message.targetId > 0)
        {
            DirectedMessages++;
            if (thread == null || thread.Active && thread.turnCount < thread.maxTurns)
            { QueueReply(message); }
        }
        if (thread == null)
        { return; }
        if (thread.turnCount >= thread.maxTurns) { CloseSocialThread(thread, SocialEnd.MAX_TURNS); }
        else if (message.act == SocialAct.AGREE || message.act == SocialAct.APOLOGIZE) { CloseSocialThread(thread, SocialEnd.RESOLVED); }
        else if (thread.kind == SocialThreadKind.ARGUMENT) { InviteThirdParty(thread, message, witnesses); }
    }
    private void QueueReply(SocialMessage message)
    {
        if (!SocialBots.TryGetValue(message.targetId, out var receiver) || !Present(receiver, message.roomId))
        { return; }
        var mind = receiver.Social.Psychology;
        if (pendingReplies.ContainsKey(mind.SocialId) || mind.ActiveThread != 0 && mind.ActiveThread != message.threadId)
        { return; }
        double baseChance = Config.Get("replyBase." + message.act);
        double probability = Math.Max(Config.Get("replyMin"), Math.Min(Config.Get("replyMax"), baseChance +
            (mind.Traits.Sociability - 50) * Config.Get("replySociability") + mind.Relation(message.senderId).anger * Config.Get("replyAnger") - SocialRoom(message.roomId).chill * Config.Get("replyChill")));
        var wait = new ReplyWait { message = message, expires = SocialTime.Seconds + Config.Get("replyExpiry") };
        replyWaits[message.id] = wait; pendingReplies[mind.SocialId] = message.id; pendingOrigins[message.senderId] = message.id;
        if (mind.SocialRng.Next() >= probability)
        { return; }
        double delay = Config.Get("replyDelayMin") + mind.SocialRng.Next() * (Config.Get("replyDelayMax") - Config.Get("replyDelayMin"));
        mind.Intentions.Add(new SocialIntent { roomId = message.roomId, motive = SocialMotive.REPLY_OBLIGATION, targetId = message.senderId,
            replyToId = message.id, threadId = message.threadId, causeId = message.id, facts = message.facts,
            due = SocialTime.Seconds + delay, expires = wait.expires });
        ExpectSoon(mind.SocialId);
    }
    /// <summary>
    /// 방 전체에 던진 물음에 답할 사람을 하나 정한다. 그 종목을 들고 있는 사람이 먼저다(아는 사람이 답한다).
    /// 늘 답이 달리지는 않는다: 열에 서넛은 그냥 묻힌다. 답하는 사람이 없으면 아무 일도 일어나지 않는다(부른 사람이 없으니 무시당했다고 화내지도 않는다).
    /// </summary>
    private void OfferRoomAnswer(TradeBot asker, SocialMessage message, List<TradeBot> witnesses)
    {
        if (ActiveThreads(message.roomId) >= Config.Int("maxThreads") || asker.Social.Psychology.ActiveThread != 0) { return; }
        var stock = FindSocialStock(message.facts?.symbol);
        TradeBot answerer = null;
        foreach (var witness in witnesses)
        {
            var heard = witness.Social.Psychology;
            if (heard.ActiveThread != 0 || heard.Intentions.Count > 0 || pendingReplies.ContainsKey(heard.SocialId) || pendingOrigins.ContainsKey(heard.SocialId) || !CanStartWith(witness, asker, message.roomId)) { continue; }
            if (answerer == null) { answerer = witness; }
            if (stock != null && witness.GetOwnedVolume(stock) > 0m) { answerer = witness; break; }
        }
        if (answerer == null) { return; }
        var mind = answerer.Social.Psychology;
        if (mind.SocialRng.Next() >= .45 + mind.Traits.Sociability / 200d) { return; }
        double delay = Config.Get("replyDelayMin") + mind.SocialRng.Next() * (Config.Get("replyDelayMax") - Config.Get("replyDelayMin"));
        mind.Intentions.Add(new SocialIntent { roomId = message.roomId, motive = SocialMotive.REPLY_OBLIGATION, targetId = message.senderId,
            replyToId = message.id, causeId = message.id, facts = message.facts,
            due = SocialTime.Seconds + delay, expires = SocialTime.Seconds + Config.Get("replyExpiry") });
        ExpectSoon(mind.SocialId);
    }
    private void InviteThirdParty(SocialThread thread, SocialMessage message, List<TradeBot> witnesses)
    {
        if (!thread.Active || thread.kind != SocialThreadKind.ARGUMENT || thread.participants.Count >= Config.Int("maxParticipants"))
        { return; }
        foreach (var witness in witnesses)
        {
            var mind = witness.Social.Psychology;
            if (thread.participants.Contains(mind.SocialId) || mind.ActiveThread != 0 || pendingReplies.ContainsKey(mind.SocialId) ||
                mind.SocialRng.Next() >= Config.Get("thirdPartyChance") * mind.Traits.Sociability / 50d)
            { continue; }
            SocialAct? act = null;
            if (mind.Traits.Sociability >= Config.Get("mediatorSociability") && mind.Traits.Temper <= Config.Get("mediatorTemper")) { act = SocialAct.MEDIATE; }
            else if (message.targetId > 0 && mind.Relation(message.targetId).affinity >= Config.Get("friendThreshold")) { act = SocialAct.REBUT; }
            else if (mind.Traits.Bravado >= Config.Get("spectatorBravado") && mind.Relation(message.senderId).affinity < Config.Get("spectatorAffinity") && mind.Relation(message.targetId).affinity < Config.Get("spectatorAffinity")) { act = SocialAct.REACT; }
            if (!act.HasValue)
            { continue; }
            mind.Intentions.Add(new SocialIntent { roomId = thread.roomId, motive = SocialMotive.WITNESSED_EVENT, forcedAct = act,
                threadId = thread.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
            // Reserve the participant slot immediately, preventing several observers from overbooking the last slot.
            thread.participants.Add(mind.SocialId); mind.ActiveThread = thread.id; ExpectSoon(mind.SocialId); break;
        }
    }
    partial void BeforeSocialTick()
    {
        expiredReplies.Clear();
        foreach (var pair in replyWaits)
        {
            if (!pair.Value.answered && !pair.Value.resolved && pair.Value.expires > SocialTime.Seconds)
            { continue; }
            if (!pair.Value.answered && !pair.Value.resolved) { IgnoreCall(pair.Value.message); }
            if (pendingReplies.TryGetValue(pair.Value.message.targetId, out long pending) && pending == pair.Key) { pendingReplies.Remove(pair.Value.message.targetId); }
            if (pendingOrigins.TryGetValue(pair.Value.message.senderId, out long origin) && origin == pair.Key) { pendingOrigins.Remove(pair.Value.message.senderId); }
            expiredReplies.Add(pair.Key);
        }
        foreach (long id in expiredReplies) { replyWaits.Remove(id); }
        foreach (var thread in socialThreads.Values)
        {
            if (thread.Active && SocialTime.Seconds - thread.lastAt > Config.Get("replyExpiry"))
            { CloseSocialThread(thread, SocialEnd.NO_REPLY); }
            var stock = FindSocialStock(thread.symbol);
            if (thread.Active && stock != null && thread.initialPrice > 0 && Math.Abs(stock.currentPrice / thread.initialPrice - 1) * 100 >= (decimal)Config.Get("interruptMovePct"))
            {
                foreach (int id in thread.participants)
                { if (SocialBots.TryGetValue(id, out var holder) && holder.GetOwnedVolume(stock) > 0) { CloseSocialThread(thread, SocialEnd.INTERRUPTED); break; } }
            }
        }
        TickSocialEmotions();
        TrimSocialCaches();
    }
    private void ReadSocialRecent(TradeBot observer)
    {
        var mind = observer.Social.Psychology;
        if (!mind.ActiveAt((float)SocialTime.Seconds))
        { return; }
        ReadRoom(0);
        if (mind.Room > 0) { ReadRoom(mind.Room); }
        void ReadRoom(int roomId)
        {
            if (!Present(observer, roomId)) { return; }
            var messages = SocialRoom(roomId).recent.ToArray();
            mind.SocialLastRead.TryGetValue(roomId, out long last);
            for (int i = Math.Max(0, messages.Length - Config.Int("recentReadCount")); i < messages.Length; i++)
            {
                var message = messages[i];
                if (message.id <= last) { continue; }
                mind.SocialLastRead[roomId] = message.id;
                if (message.senderId == mind.SocialId || message.targetId == mind.SocialId || !SocialBots.TryGetValue(message.senderId, out var author)) { continue; }
                double chance = WitnessChance?.Invoke(observer) ?? Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + mind.Activity * Config.Get("witnessActivityScale"));
                if (mind.SocialRng.Next() < chance) { ApplyLateSocialWitness(author, message, observer); }
            }
        }
    }
    private void IgnoreCall(SocialMessage message)
    {
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "CALL_IGNORED", id = message.id, room = message.roomId, actor = message.senderId, target = message.targetId });
        ApplyIgnoredCall(message);
        var thread = FindSocialThread(message.threadId);
        if (thread != null && thread.Active) { CloseSocialThread(thread, SocialEnd.NO_REPLY); }
    }
    partial void ExpireSocialIntent(TradeBot bot, SocialIntent intent)
    {
        if (intent.replyToId > 0 && replyWaits.TryGetValue(intent.replyToId, out var wait)) { wait.expires = SocialTime.Seconds; }
    }
    public void CloseSocialThread(SocialThread thread, SocialEnd reason)
    {
        if (thread == null || !thread.Active)
        { return; }
        thread.endReason = reason; ClosedThreads.Add(thread);
        foreach (var wait in replyWaits.Values) { if (wait.message.threadId == thread.id) { wait.resolved = true; } }
        foreach (int id in thread.participants)
        {
            if (SocialBots.TryGetValue(id, out var bot))
            {
                if (bot.Social.Psychology.ActiveThread == thread.id) { bot.Social.Psychology.ActiveThread = 0; }
                bot.Social.Psychology.Intentions.RemoveAll(i => i.threadId == thread.id);
                if (reason == SocialEnd.PLAYER_ACTION)
                { bot.Social.Psychology.Intentions.RemoveAll(i => i.threadId == 0 && i.motive == SocialMotive.EMOTION_THRESHOLD && thread.participants.Contains(i.targetId)); }
            }
            foreach (int other in thread.participants)
            { if (id != other) { pairCooldown[Pair(id, other)] = SocialTime.Seconds + Config.Get("pairCooldownSec"); } }
        }
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "THREAD_END", id = thread.id, room = thread.roomId, reason = reason.ToString(), value = thread.turnCount });
        OnSocialThreadClosed(thread);
    }
    partial void PrepareMemoryFacts(TradeBot bot, SocialIntent intent, SocialFacts facts);
    partial void AdjustAngerWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight);
    partial void BeforeEmotionPublish(TradeBot bot, SocialIntent intent, SocialMessage message);
    partial void ApplySocialEffects(TradeBot bot, SocialMessage message, List<TradeBot> witnesses);
    partial void ApplyIgnoredCall(SocialMessage message);
    partial void TickSocialEmotions();
    partial void OnSocialThreadClosed(SocialThread thread);
    partial void ApplyLateSocialWitness(TradeBot author, SocialMessage message, TradeBot observer);
}
