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
    private int ChooseTarget(TradeBot bot, int room, SocialFacts facts)
    {
        var recent = SocialRoom(room).recent.ToArray();
        for (int i = recent.Length - 1; i >= 0; i--)
        {
            if (SocialBots.TryGetValue(recent[i].senderId, out var other) && CanStartWith(bot, other, room))
            { return other.Social.Id; }
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
            mind.SocialRng.Next() < Config.Get("initiateTargetChance"))
        { intent.targetId = ChooseTarget(bot, intent.roomId, facts); }
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
        AdjustAngerWeight(bot, intent, act, ref weight);
        AdjustQuadrantWeight(bot, intent, act, ref weight);
        if (act == SocialAct.MEDIATE && (intent.threadId == 0 || FindSocialThread(intent.threadId)?.kind != SocialThreadKind.ARGUMENT)) { weight = 0; }
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
    }
    private void InviteThirdParty(SocialThread thread, SocialMessage message, List<TradeBot> witnesses)
    {
        if (thread.participants.Count >= Config.Int("maxParticipants"))
        { return; }
        foreach (var witness in witnesses)
        {
            var mind = witness.Social.Psychology;
            if (thread.participants.Contains(mind.SocialId) || mind.ActiveThread != 0 || pendingReplies.ContainsKey(mind.SocialId) ||
                mind.SocialRng.Next() >= Config.Get("thirdPartyChance") * mind.Traits.Sociability / 50d)
            { continue; }
            SocialAct? act = null;
            if (message.targetId > 0 && mind.Relation(message.targetId).affinity >= Config.Get("friendThreshold")) { act = SocialAct.REBUT; }
            else if (mind.Traits.Sociability >= Config.Get("mediatorSociability") && mind.Traits.Temper <= Config.Get("mediatorTemper")) { act = SocialAct.MEDIATE; }
            else if (mind.Traits.Bravado >= Config.Get("spectatorBravado") && mind.Relation(message.senderId).affinity < Config.Get("spectatorAffinity") && mind.Relation(message.targetId).affinity < Config.Get("spectatorAffinity")) { act = SocialAct.REACT; }
            if (!act.HasValue)
            { continue; }
            mind.Intentions.Add(new SocialIntent { roomId = thread.roomId, motive = SocialMotive.WITNESSED_EVENT, forcedAct = act,
                threadId = thread.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
            // Reserve the participant slot immediately, preventing several observers from overbooking the last slot.
            thread.participants.Add(mind.SocialId); mind.ActiveThread = thread.id; break;
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
