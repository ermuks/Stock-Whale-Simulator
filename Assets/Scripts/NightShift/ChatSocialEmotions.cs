using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public readonly InvestorEventMemory SocialHeardEvents = new();
    public double SocialDecayAt = -1;
    public int RumorDay = -1;
    public double RumorTrustToday;
    public double ExitAt = -1;
    public int ExitRoom, ExitOpponent, ExitOrigin;
    public long ExitThread;
    public bool ExitIsSympathy;
    public readonly HashSet<long> SawUnmoderatedFight = new();
    public void SocialDelta(string field, double delta, string cause, long eventId)
    {
        switch (field)
        {
            case "greed": Greed = (float)Math.Round(Math.Max(0, Math.Min(100, Greed + delta))); break;
            case "fear": Fear = (float)Math.Round(Math.Max(0, Math.Min(100, Fear + delta))); break;
            case "trust": Trust = (float)Math.Max(0, Trust + delta); break;
            case "betrayal": Betrayal = (float)Math.Max(0, Betrayal + delta); break;
        }
        RefreshQuadrant(); LastCause = cause + " #" + eventId; AuditEmotion(cause, eventId);
    }
    public int AngerStage(int target) => Relations.TryGetValue(target, out var row) ? Stage(row.anger) : 0;
    public static int Stage(double value)
    {
        var c = ChatSocialConfig.Current;
        return value >= c.Get("angerStage4") ? 4 : value >= c.Get("angerStage3") ? 3 : value >= c.Get("angerStage2") ? 2 : value >= c.Get("angerStage1") ? 1 : 0;
    }
    public double MaximumAnger(out int target)
    {
        target = 0; double result = 0;
        foreach (var relation in Relations.Values)
        { if (relation.anger > result) { target = relation.to; result = relation.anger; } }
        return result;
    }
}

public sealed partial class InvestorSociety
{
    private readonly Dictionary<(long, int, string), double> threadExposure = new();
    private readonly Dictionary<long, HashSet<int>> fightWitnesses = new();
    private readonly Dictionary<int, double> roomDecayAt = new();
    private readonly Dictionary<(int, int), double> angerDecayRemainder = new();
    private readonly HashSet<long> exitedFight = new();
    private readonly Dictionary<long, (int room, int capacity, int scheduled)> departureCaps = new();
    private readonly HashSet<(long, int)> sympathyConsidered = new();
    private readonly Dictionary<long, HashSet<double>> departureTimes = new();
    private readonly Dictionary<int, int> emotionalThresholds = new();
    public int VoluntaryExits { get; private set; }
    public int SympathyExits { get; private set; }
    public event Action<TradeBot, int, string> SocialDeparted;
    private double Effect(SocialAct act, string field)
    {
        foreach (var row in ChatSocialAssets.Acts.effects)
        {
            if (row.key != act.ToString()) { continue; }
            foreach (var entry in row.weights) { if (entry.key == field) { return entry.value; } }
        }
        return 0;
    }
    public void ChangeRelation(TradeBot receiver, int other, string field, double delta, string cause, long causeId, int roomId, bool fromMessage = true, long threadId = 0)
    {
        if (other <= 0 || other == receiver.Social.Id || delta == 0)
        { return; }
        var mind = receiver.Social.Psychology; var relation = mind.Relation(other);
        double before = field == "anger" ? relation.anger : relation.affinity;
        int beforeStage = InvestorPsychology.Stage(relation.anger);
        if (field == "anger" && delta > 0 && fromMessage)
        {
            delta *= (Config.Get("angerHalfTemper") + mind.Traits.Temper / 100d) * (1 - SocialRoom(roomId).discipline / Config.Get("angerDisciplineDivisor"));
            delta = Math.Min(Config.Get("angerMaximumPerMessage"), delta);
        }
        double after = Math.Round(Math.Max(field == "anger" ? 0 : -100, Math.Min(100, before + delta)));
        if (field == "anger" && fromMessage && delta > 0 && beforeStage < 3)
        { after = Math.Min(after, Config.Get("angerStage" + (beforeStage + 2)) - 1); }
        if (field == "anger") { relation.anger = after; } else { relation.affinity = after; }
        relation.lastInteractionAt = SocialTime.GameMinute;
        ChatSocialAudit.Journal.Change(SocialTime.GameMinute, mind.SocialId, field, before, after, cause, causeId, other);
        if (field != "anger")
        { return; }
        int stage = InvestorPsychology.Stage(after);
        if (stage > beforeStage)
        {
            relation.highestStage = Math.Max(stage, relation.highestStage);
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "ANGER_STAGE", id = causeId, actor = mind.SocialId, target = other, room = roomId, value = stage, reason = cause });
            Offer(receiver, roomId, SocialMotive.EMOTION_THRESHOLD, target: other, cause: causeId);
            if (stage == 2)
            {
                RememberFight(receiver, other, causeId);
                if (SocialBots.TryGetValue(other, out var aggressor)) { RememberFight(aggressor, mind.SocialId, causeId); }
            }
            var thread = FindSocialThread(threadId != 0 ? threadId : mind.ActiveThread);
            if (stage >= 3 && thread != null && thread.firstStage3At < 0) { thread.firstStage3At = SocialTime.Seconds; }
            if (stage == 4 && roomId > 0 && roomId <= PlayerRoomCount && mind.ExitAt < 0)
            {
                mind.ExitAt = SocialTime.Seconds + Config.Get("exitGraceSec"); mind.ExitRoom = roomId; mind.ExitOpponent = other; mind.ExitThread = thread?.id ?? 0;
                Offer(receiver, roomId, SocialMotive.EMOTION_THRESHOLD, new SocialFacts { notice = "EXIT_WARNING" }, SocialAct.REACT, cause: causeId);
                ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "EXIT_WARNING", id = causeId, actor = mind.SocialId, target = other, room = roomId });
            }
        }
    }
    private void RememberFight(TradeBot bot, int target, long id)
    {
        socialMessageIndex.TryGetValue(id, out var source);
        long incident = source != null && FindSocialThread(source.threadId) is SocialThread thread ? thread.parentMessage : id;
        bot.Social.Psychology.Remember(new SocialMemory { eventId = incident, type = SocialMemoryKind.FOUGHT, actorId = bot.Social.Id, targetId = target, symbol = source?.facts.symbol,
            at = SocialTime.GameMinute, salience = Config.Int("memoryFoughtSalience") });
    }
    partial void AdjustAngerWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight)
    {
        int stage = bot.Social.Psychology.AngerStage(intent.targetId);
        if (stage == 0 && act == SocialAct.MOCK && intent.motive != SocialMotive.REPLY_OBLIGATION)
        {
            bool loss = SocialBots.TryGetValue(intent.targetId, out var target) && CaptureFacts(target, FindSocialStock(intent.facts?.symbol)).losing;
            if (!loss) { weight = 0; }
        }
        if (stage == 1 && (act == SocialAct.MOCK || act == SocialAct.DOUBT)) { weight *= Config.Get("stage1AttackWeight"); }
        if (stage >= 2 && (act == SocialAct.MOCK || act == SocialAct.REBUT)) { weight *= Config.Get("stage2AttackWeight"); }
        if (stage >= 3 && (act == SocialAct.AGREE || act == SocialAct.APOLOGIZE)) { weight *= Config.Get("stage3PeaceWeight"); }
    }
    partial void PrepareSocialStyle(TradeBot bot, SocialIntent intent, SocialAct act, ref string style, ref double emotion)
    {
        var mind = bot.Social.Psychology;
        double anger = intent.targetId > 0 ? mind.Relation(intent.targetId).anger : mind.MaximumAnger(out _);
        if (InvestorPsychology.Stage(anger) >= 3) { style = style == "formal" ? "casual" : "rough"; }
        if (Attack(act) || act == SocialAct.DOUBT || act == SocialAct.FAREWELL) { emotion = anger; }
    }
    partial void ApplySocialEffects(TradeBot bot, SocialMessage message, List<TradeBot> witnesses)
    { ApplySocialEffectsCore(bot, message, witnesses, true); }
    partial void ApplyLateSocialWitness(TradeBot author, SocialMessage message, TradeBot observer)
    {
        ApplySocialEffectsCore(author, message, new List<TradeBot> { observer }, false);
        if (message.act == SocialAct.FAREWELL && departureCaps.TryGetValue(message.id, out var root))
        { ScheduleSympathy(author, root.room, new List<TradeBot> { observer }, 0, message.id); }
    }
    private void ApplySocialEffectsCore(TradeBot bot, SocialMessage message, List<TradeBot> witnesses, bool direct)
    {
        if (direct && message.targetId > 0 && SocialBots.TryGetValue(message.targetId, out var receiver))
        {
            double anger = Effect(message.act, "anger");
            if (message.memory && (message.act == SocialAct.MOCK || message.act == SocialAct.REBUT)) { anger *= Config.Get("memoryAngerScale"); }
            ChangeRelation(receiver, message.senderId, "anger", anger, "direct-message", message.id, message.roomId, threadId: message.threadId);
            ChangeRelation(receiver, message.senderId, "affinity", Effect(message.act, "affinity"), "direct-message", message.id, message.roomId);
            double fear = Effect(message.act, "fear");
            if (message.act != SocialAct.MOCK || CaptureFacts(receiver, FindSocialStock(message.facts?.symbol)).losing)
            { receiver.Social.Psychology.SocialDelta("fear", fear, "direct-message", message.id); }
        }
        var thread = FindSocialThread(message.threadId);
        if (direct && message.act == SocialAct.MEDIATE && thread != null) { Mediate(bot, message, thread); }
        if (thread != null && thread.kind == SocialThreadKind.ARGUMENT)
        {
            if (!fightWitnesses.TryGetValue(thread.id, out var seen)) { seen = new HashSet<int>(); fightWitnesses[thread.id] = seen; }
            foreach (var witness in witnesses) { seen.Add(witness.Social.Id); }
        }
        foreach (var witness in witnesses)
        {
            if (witness.Social.Id == message.targetId)
            { continue; }
            var mind = witness.Social.Psychology;
            RememberWitnessedResult(witness, message);
            if (mind.AngerStage(message.senderId) >= 2 && mind.ActiveThread == 0 && !pendingReplies.ContainsKey(mind.SocialId) && !pendingOrigins.ContainsKey(mind.SocialId))
            {
                if (thread != null && thread.Active && thread.participants.Count < Config.Int("maxParticipants") && !thread.participants.Contains(mind.SocialId))
                {
                    mind.Intentions.Add(new SocialIntent { roomId = message.roomId, motive = SocialMotive.WITNESSED_EVENT, forcedAct = SocialAct.REBUT,
                        threadId = thread.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
                    thread.participants.Add(mind.SocialId); mind.ActiveThread = thread.id;
                }
                else if (thread == null && CanStartWith(witness, bot, message.roomId))
                { Offer(witness, message.roomId, SocialMotive.WITNESSED_EVENT, message.facts, SocialAct.REBUT, message.senderId, message.id); }
            }
            double multiplier = Config.Get("witnessHalfSuggestibility") + mind.Traits.Suggestibility / 100d;
            if (message.targetId > 0 && mind.Relation(message.targetId).affinity >= Config.Get("friendThreshold"))
            {
                double anger = message.act == SocialAct.MOCK ? Config.Get("friendMockAnger") : message.act == SocialAct.INSULT ? Config.Get("friendInsultAnger") : 0;
                WitnessRelation(witness, message, "anger", anger * multiplier);
            }
            if (message.act == SocialAct.INSULT) { WitnessRelation(witness, message, "affinity", Config.Get("insultWitnessAffinity") * multiplier); }
            if (message.act == SocialAct.LAMENT)
            {
                bool held = CaptureFacts(witness, FindSocialStock(message.facts?.symbol)).held;
                WitnessEmotion(witness, message, "fear", (held ? Config.Get("lamentHeldFear") : Config.Get("lamentOtherFear")) * multiplier);
            }
            if (message.act == SocialAct.BRAG)
            {
                var own = CaptureFacts(witness, FindSocialStock(message.facts?.symbol));
                decimal.TryParse(own.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal ownReturn);
                decimal.TryParse(message.facts?.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal authorReturn);
                if (authorReturn > ownReturn)
                {
                    WitnessEmotion(witness, message, "greed", Config.Get("bragGreed") * multiplier);
                    ConsiderSocialFomo(witness, message);
                }
                if (own.losing) { WitnessRelation(witness, message, "affinity", Config.Get("losingBragAffinity") * multiplier); }
            }
            if ((message.act == SocialAct.PRAISE_PLAYER || message.act == SocialAct.WARN_PLAYER) && message.facts.eventId > 0 && mind.SocialHeardEvents.Add(message.facts.eventId))
            { WitnessEmotion(witness, message, "trust", (message.act == SocialAct.PRAISE_PLAYER ? Config.Get("praiseTrust") : Config.Get("warnTrust")) * multiplier); }
        }
        if (direct && message.act == SocialAct.FAREWELL && bot.Social.Psychology.ExitAt >= 0 && SocialTime.Seconds >= bot.Social.Psychology.ExitAt)
        { DepartSocial(bot, message, witnesses); }
        ApplyModerationReaction(bot, message, witnesses);
    }
    private double Exposure(TradeBot bot, SocialMessage message, string field, double delta)
    {
        var key = (message.threadId > 0 ? message.threadId : -message.id, bot.Social.Id, field);
        threadExposure.TryGetValue(key, out double before);
        double after = Math.Max(-Config.Get("witnessThreadCap"), Math.Min(Config.Get("witnessThreadCap"), before + delta));
        threadExposure[key] = after; return after - before;
    }
    private void WitnessRelation(TradeBot bot, SocialMessage message, string field, double delta)
    { ChangeRelation(bot, message.senderId, field, Exposure(bot, message, field, delta), "witness-message", message.id, message.roomId, false, message.threadId); }
    private void WitnessEmotion(TradeBot bot, SocialMessage message, string field, double delta)
    {
        var mind = bot.Social.Psychology;
        delta = Exposure(bot, message, field, delta);
        if (field == "trust")
        {
            int dayNumber = (int)(SocialTime.GameMinute / 1440);
            if (mind.RumorDay != dayNumber) { mind.RumorDay = dayNumber; mind.RumorTrustToday = 0; }
            double next = Math.Max(-Config.Get("rumorDailyTrustCap"), Math.Min(Config.Get("rumorDailyTrustCap"), mind.RumorTrustToday + delta));
            delta = next - mind.RumorTrustToday; mind.RumorTrustToday = next;
        }
        mind.SocialDelta(field, delta, "witness-message", message.id);
    }
    private void Mediate(TradeBot mediator, SocialMessage message, SocialThread thread)
    {
        double affinity = 0; int count = 0; bool severe = false;
        foreach (int id in thread.participants)
        {
            if (id == mediator.Social.Id || !SocialBots.TryGetValue(id, out var member)) { continue; }
            affinity += member.Social.Psychology.Relation(mediator.Social.Id).affinity; count++;
            severe |= InvestorPsychology.Stage(member.Social.Psychology.MaximumAnger(out _)) >= 3;
        }
        double chance = Config.Get("mediationBase") + (count == 0 ? 0 : affinity / count) / Config.Get("mediationAffinityDivisor");
        if (severe) { chance *= Config.Get("mediationStage3Scale"); }
        bool success = mediator.Social.Psychology.SocialRng.Next() < Math.Max(0, Math.Min(1, chance));
        foreach (int id in thread.participants)
        {
            if (id == mediator.Social.Id || !SocialBots.TryGetValue(id, out var member)) { continue; }
            if (success)
            {
                foreach (int other in thread.participants)
                { if (other != id && other != mediator.Social.Id) { ChangeRelation(member, other, "anger", Config.Get("mediationSuccessAnger"), "mediation", message.id, message.roomId, false); } }
            }
            else { ChangeRelation(member, mediator.Social.Id, "anger", Config.Get("mediationFailAnger"), "mediation", message.id, message.roomId); }
            ChangeRelation(member, mediator.Social.Id, "affinity", Config.Get(success ? "mediationSuccessAffinity" : "mediationFailAffinity"), "mediation", message.id, message.roomId, false);
        }
        if (success) { CloseSocialThread(thread, SocialEnd.RESOLVED); }
    }
    partial void ApplyIgnoredCall(SocialMessage message)
    {
        if (!SocialBots.TryGetValue(message.senderId, out var sender) || !Present(sender, message.roomId))
        { return; }
        ChangeRelation(sender, message.targetId, "anger", Config.Get("ignoredAnger"), "ignored-call", message.id, message.roomId, threadId: message.threadId);
        ChangeRelation(sender, message.targetId, "affinity", Config.Get("ignoredAffinity"), "ignored-call", message.id, message.roomId);
    }
    partial void BeforeSocialBot(TradeBot bot)
    {
        ReadSocialRecent(bot);
        var mind = bot.Social.Psychology;
        if (mind.SocialDecayAt >= 0)
        {
            double hours = Math.Max(0, SocialTime.GameMinute - mind.SocialDecayAt) / 60d;
            foreach (var relation in mind.Relations.Values)
            {
                var key = (mind.SocialId, relation.to); angerDecayRemainder.TryGetValue(key, out double remainder);
                double decay = remainder + hours * Config.Get("angerDecayPerHour") * (1.5 - mind.Traits.Grudge / 100d);
                double whole = Math.Floor(decay); angerDecayRemainder[key] = decay - whole;
                if (whole > 0) { ChangeRelation(bot, relation.to, "anger", -whole, "game-hour-decay", 0, mind.Room, false); }
            }
        }
        mind.SocialDecayAt = SocialTime.GameMinute;
        if (mind.ExitAt >= 0 && SocialTime.Seconds >= mind.ExitAt)
        {
            if (mind.Room != mind.ExitRoom || !mind.ExitIsSympathy && (mind.Relation(mind.ExitOpponent).anger < Config.Get("exitAngerMin") || mind.ExitThread > 0 && exitedFight.Contains(mind.ExitThread)))
            { mind.ExitAt = -1; mind.ExitIsSympathy = false; }
            else
            { Offer(bot, mind.Room, SocialMotive.EMOTION_THRESHOLD, new SocialFacts { notice = mind.ExitIsSympathy ? "SYMPATHY_EXIT" : "VOLUNTARY_EXIT", departedName = SocialBots.TryGetValue(mind.ExitOrigin, out var origin) ? origin.Social.Nickname : null }, SocialAct.FAREWELL); }
        }
        ObserveEmotionalThresholds(bot);
        TickSocialAutonomy(bot);
    }
    partial void TickSocialEmotions()
    {
        foreach (var thread in socialThreads.Values)
        {
            if (thread.firstStage3At < 0 || thread.moderated || thread.endReason == SocialEnd.PLAYER_ACTION || thread.endReason == SocialEnd.RESOLVED || SocialTime.Seconds - thread.firstStage3At < Config.Get("callPlayerDelaySec"))
            { continue; }
            if (!fightWitnesses.TryGetValue(thread.id, out var seen))
            { continue; }
            foreach (var observer in socialBots)
            {
                var mind = observer.Social.Psychology;
                if (!seen.Contains(mind.SocialId) || thread.participants.Contains(mind.SocialId) || !Present(observer, thread.roomId))
                { continue; }
                if (mind.SawUnmoderatedFight.Add(thread.id)) { mind.SocialDelta("trust", Config.Get("abandonedFightTrust"), "unmoderated-fight", thread.id); }
                if (!thread.calledPlayer)
                {
                    Offer(observer, thread.roomId, SocialMotive.EMOTION_THRESHOLD, forced: SocialAct.CALL_PLAYER, target: -1, cause: thread.id);
                    thread.calledPlayer = true;
                }
            }
        }
        TickSocialModeration();
    }
    private void DepartSocial(TradeBot bot, SocialMessage farewell, List<TradeBot> witnesses)
    {
        var mind = bot.Social.Psychology; int room = mind.Room; bool sympathy = mind.ExitIsSympathy;
        if (room <= 0 || room > PlayerRoomCount)
        { return; }
        if (!sympathy && mind.ExitThread > 0)
        {
            exitedFight.Add(mind.ExitThread);
            var fight = FindSocialThread(mind.ExitThread); CloseSocialThread(fight, SocialEnd.EXIT);
            if (SocialBots.TryGetValue(mind.ExitOpponent, out var other))
            {
                ChangeRelation(other, mind.SocialId, "anger", -Config.Get("exitOtherAngerDrop"), "opponent-left", farewell.id, room, false);
                other.Social.Psychology.ExitAt = -1;
            }
        }
        if (mind.ActiveThread != 0) { CloseSocialThread(FindSocialThread(mind.ActiveThread), SocialEnd.EXIT); }
        mind.SocialDelta("trust", Config.Get(sympathy ? "sympathyTrust" : "exitTrust"), sympathy ? "sympathy-exit" : "voluntary-exit", farewell.id);
        mind.RejoinAt[room] = SocialTime.GameMinute + Config.Get(sympathy ? "sympathyRejoinMinutes" : "exitRejoinMinutes");
        int members = 0; foreach (var candidate in socialBots) { if (candidate.Social.Psychology.Room == room) { members++; } }
        SocialDeparted?.Invoke(bot, room, sympathy ? "친구의 퇴장에 동조" : "언쟁 방치 후 이탈");
        GetRoom(room)?.Members.Remove(mind.SocialId); mind.Room = 0; mind.ExitAt = -1; mind.Intentions.Clear();
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = sympathy ? "SYMPATHY_EXIT" : "VOLUNTARY_EXIT", id = farewell.id, threadId = mind.ExitThread, room = room, actor = mind.SocialId,
            target = sympathy ? mind.ExitOrigin : mind.ExitOpponent, value = sympathy ? mind.Relation(mind.ExitOrigin).affinity : mind.Relation(mind.ExitOpponent).anger, reason = sympathy ? "depth=1" : "anger-grace" });
        if (sympathy) { SympathyExits++; }
        else
        {
            VoluntaryExits++; departureCaps[farewell.id] = (room, (int)Math.Floor(members * Config.Get("sympathyCapFraction")), 0);
            ScheduleSympathy(bot, room, witnesses, members, farewell.id);
            if (mind.SocialRng.Next() < mind.Traits.Grudge / 100d * Config.Get("exitGrudgeWarnChance"))
            { SchedulePublicWarning(bot, room, farewell.id, false); }
        }
        mind.ExitIsSympathy = false;
    }
    private void ScheduleSympathy(TradeBot departed, int room, List<TradeBot> witnesses, int members, long eventId)
    {
        var candidates = new List<TradeBot>();
        foreach (var witness in witnesses)
        {
            var mind = witness.Social.Psychology;
            if (!sympathyConsidered.Add((eventId, mind.SocialId))) { continue; }
            double affinity = mind.Relation(departed.Social.Id).affinity;
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "SYMPATHY_WITNESS", id = eventId, room = room, actor = mind.SocialId, target = departed.Social.Id, value = affinity,
                reason = mind.ExitAt >= 0 ? "already-leaving" : "observed-farewell" });
            if (mind.Room != room || mind.ExitAt >= 0 || affinity < Config.Get("sympathyAffinity"))
            { continue; }
            double chance = (affinity - Config.Get("sympathyAffinityOffset")) / 100d * (1 - mind.FormulaTrust / Config.Get("sympathyTrustDivisor")) * (1 - SocialRoom(room).discipline / Config.Get("sympathyDisciplineDivisor"));
            double roll = mind.SocialRng.Next();
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "SYMPATHY_ROLL", id = eventId, room = room, actor = mind.SocialId, target = departed.Social.Id, value = chance, reason = roll.ToString("F4", SocialCulture) });
            if (roll < Math.Max(0, Math.Min(1, chance))) { candidates.Add(witness); }
        }
        candidates.Sort((a, b) => { int order = b.Social.Psychology.Relation(departed.Social.Id).affinity.CompareTo(a.Social.Psychology.Relation(departed.Social.Id).affinity); return order != 0 ? order : a.Social.Id.CompareTo(b.Social.Id); });
        if (!departureCaps.TryGetValue(eventId, out var root)) { root = (room, (int)Math.Floor(members * Config.Get("sympathyCapFraction")), 0); }
        int cap = Math.Min(candidates.Count, Math.Max(0, root.capacity - root.scheduled));
        departureCaps[eventId] = (room, root.capacity, root.scheduled + cap);
        if (!departureTimes.TryGetValue(eventId, out var usedTimes)) { usedTimes = new HashSet<double>(); departureTimes[eventId] = usedTimes; }
        for (int i = 0; i < cap; i++)
        {
            var mind = candidates[i].Social.Psychology;
            double delay = mind.SocialRng.Range(Config.Int("sympathyDelayMin"), Config.Int("sympathyDelayMax") + 1);
            int attempts = 0, slots = Config.Int("sympathyDelayMax") - Config.Int("sympathyDelayMin") + 1;
            while (!usedTimes.Add(SocialTime.Seconds + delay))
            {
                attempts++;
                delay = attempts <= slots ? (delay < Config.Get("sympathyDelayMax") ? delay + 1 : Config.Get("sympathyDelayMin")) :
                    Config.Get("sympathyDelayMin") + (Config.Get("sympathyDelayMax") - Config.Get("sympathyDelayMin")) * (root.scheduled + i + 1d) / (root.capacity + attempts + 1d);
            }
            mind.ExitIsSympathy = true; mind.ExitOrigin = departed.Social.Id; mind.ExitRoom = room; mind.ExitAt = SocialTime.Seconds + delay;
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "SYMPATHY_SCHEDULED", id = eventId, room = room, actor = mind.SocialId, target = departed.Social.Id, value = mind.Relation(departed.Social.Id).affinity, reason = "depth=1" });
        }
    }
    private void SchedulePublicWarning(TradeBot bot, int room, long eventId, bool kicked)
    {
        var mind = bot.Social.Psychology;
        // In-game-hour deadline is checked when the intention is evaluated; delay is mapped by the existing clock rate.
        var intent = new SocialIntent { roomId = 0, motive = SocialMotive.SELF_EVENT, forcedAct = SocialAct.WARN_PLAYER,
            targetId = -1, facts = new SocialFacts { notice = kicked ? "KICK_WARNING" : "LEFT_WARNING", eventId = eventId },
            due = SocialTime.Seconds + Config.Get("replyDelayMax"), expires = SocialTime.Seconds + Config.Get("selfExpiry"), causeId = eventId };
        mind.Intentions.Add(intent);
    }
    private void ObserveEmotionalThresholds(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        int flags = (mind.Greed >= Config.Get("emotionHighThreshold") ? 1 : 0) | (mind.Fear >= Config.Get("emotionHighThreshold") ? 2 : 0);
        if (emotionalThresholds.TryGetValue(mind.SocialId, out int previous) && (flags & ~previous) != 0)
        { Offer(bot, mind.Room, SocialMotive.EMOTION_THRESHOLD, CaptureFacts(bot), SocialAct.REACT); }
        emotionalThresholds[mind.SocialId] = flags;
    }
    partial void ConsiderSocialFomo(TradeBot bot, SocialMessage message);
    partial void ApplyModerationReaction(TradeBot bot, SocialMessage message, List<TradeBot> witnesses);
    partial void TickSocialModeration();
}
