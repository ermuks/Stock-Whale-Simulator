# 커뮤니티/중재 패치 코드 전문·함수 비교

원본은 이번 후속 작업을 시작할 때의 파일이다. 앞선 미니 캔들·주문·관심주 패치를 포함한 작업 상태를 기준으로 비교한다.

## Assets/Scripts/NightShift/ChatSocialData.cs

### public sealed class SocialTraits

원본:
```csharp
public sealed class SocialTraits
{
    [SerializeField] private int temper, grudge, suggestibility, sociability, bravado, politeness;
    public int Temper => temper;
    public int Grudge => grudge;
    public int Suggestibility => suggestibility;
    public int Sociability => sociability;
    public int Bravado => bravado;
    public int Politeness => politeness;
    public bool Valid => temper >= 0 && temper <= 100 && grudge >= 0 && grudge <= 100 && suggestibility >= 0 && suggestibility <= 100 &&
        sociability >= 0 && sociability <= 100 && bravado >= 0 && bravado <= 100 && politeness >= 0 && politeness <= 100;
    public SocialTraits(SocialRandom random, ChatSocialConfig config)
    {
        temper = Draw("temper"); grudge = Draw("grudge"); suggestibility = Draw("suggestibility");
        sociability = Draw("sociability"); bravado = Draw("bravado"); politeness = Draw("politeness");
        int Draw(string name) => Math.Max(0, Math.Min(100, random.Normal(config.Get(name + "Mean"), config.Get(name + "Sd"))));
    }
}
```

수정본:
```csharp
public sealed class SocialTraits
{
    [SerializeField] private int temper, grudge, suggestibility, sociability, bravado, politeness;
    [SerializeField] private int speechVersion;
    [SerializeField] private bool serious;
    public int Temper => temper;
    public int Grudge => grudge;
    public int Suggestibility => suggestibility;
    public int Sociability => sociability;
    public int Bravado => bravado;
    public int Politeness => politeness;
    public bool Serious => serious;
    public string SpeechLabel => serious ? "진지함" : "진지하지 않음";
    public bool Valid => temper >= 0 && temper <= 100 && grudge >= 0 && grudge <= 100 && suggestibility >= 0 && suggestibility <= 100 &&
        sociability >= 0 && sociability <= 100 && bravado >= 0 && bravado <= 100 && politeness >= 0 && politeness <= 100 && speechVersion >= 0 && speechVersion <= 1;
    public SocialTraits(SocialRandom random, ChatSocialConfig config)
    {
        temper = Draw("temper"); grudge = Draw("grudge"); suggestibility = Draw("suggestibility");
        sociability = Draw("sociability"); bravado = Draw("bravado"); politeness = Draw("politeness");
        // A separate stream keeps new voice traits from shifting decisions or seeded friendships.
        var voiceRandom = new SocialRandom(unchecked((int)random.State ^ 0x5931a7bd));
        serious = voiceRandom.Next() < config.Get("seriousSpeechChance"); speechVersion = 1;
        int Draw(string name) => Math.Max(0, Math.Min(100, random.Normal(config.Get(name + "Mean"), config.Get(name + "Sd"))));
    }
    public void MigrateSpeech(SocialTraits generated)
    {
        if (speechVersion != 0)
        { return; }
        serious = generated.serious; speechVersion = 1;
    }
}
```

### public sealed class SocialMessageDebug

원본:
```csharp
[Serializable] public sealed class SocialMessageDebug
{
    public string trigger;
    public string contextKey;
    public long causeId;
    public SocialWeight[] weights;
    public SocialFacts slots;
}
```

수정본:
```csharp
[Serializable] public sealed class SocialMessageDebug
{
    public string trigger;
    public string contextKey;
    public string speechDisposition, speechKey, numberStyle;
    public long causeId;
    public SocialWeight[] weights;
    public SocialFacts slots;
}
```

### public bool RestoreSocial(

원본:
```csharp
    public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

수정본:
```csharp
    public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

## Assets/Scripts/NightShift/ChatSocialPipeline.cs

### public bool Present(

원본:
```csharp
    public bool Present(TradeBot bot, int room)
    {
        if (bot == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || room != 0 && bot.Social.Psychology.Room != room)
        { return false; }
        var mind = bot.Social.Psychology;
        return SocialRoom(room) != null && !SocialRoom(room).banned.Contains(bot.Social.Id) &&
            (!mind.MutedUntil.TryGetValue(room, out double until) || SocialTime.GameMinute >= until);
    }
```

수정본:
```csharp
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
```

### private void GenerateSocialMessage(

원본:
```csharp
    private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
        if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
        if (intent.facts != null && intent.facts.directLead) { facts = intent.facts; }
        if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
        { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
        PrepareSocialFacts(bot, intent, ref facts);
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
            else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT)) { intent.targetId = 0; }
            else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
            var templates = EligibleTemplates(bot, intent, facts, act);
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
            string phrase = ContextualSocialText(template, message, parentMessage);
            message.text = SlotPattern.Replace(phrase, match => Slot(facts, match.Groups[1].Value, target));
            message.text = DecorateSocialText(mind, message, message.text);
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
            room.sent.Enqueue(SocialTime.Seconds);
            consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
            lastAct[mind.SocialId] = act;
            socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
            while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
```

수정본:
```csharp
    private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
        if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
        if (intent.facts != null && intent.facts.directLead) { facts = intent.facts; }
        if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
        { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
        PrepareSocialFacts(bot, intent, ref facts);
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
            else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT)) { intent.targetId = 0; }
            else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
            var templates = EligibleTemplates(bot, intent, facts, act);
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
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
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
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
```

### private static bool TemplateFactsMatch(

원본:
```csharp
    private static bool TemplateFactsMatch(SocialTemplate template, SocialFacts facts)
    {
        if (template.requires == null)
        { return true; }
        foreach (string requirement in template.requires)
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
                "RecentStressDrop" => facts.stressRelief, "signal" => facts.signal,
                _ => false
            };
            if (!valid) { return false; }
        }
        return true;
    }
```

수정본:
```csharp
    private static bool TemplateFactsMatch(SocialTemplate template, SocialFacts facts)
        => SpeechRequirementsMatch(template.requires, facts);
```

### private static bool SpeechRequirementsMatch(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
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
                "RecentStressDrop" => facts.stressRelief, "signal" => facts.signal,
                _ => false
            };
            if (!valid) { return false; }
        }
        return true;
    }
```

## Assets/Scripts/NightShift/ChatSocialEmotions.cs

### private void Mediate(

원본:
```csharp
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
```

수정본:
```csharp
    private void Mediate(TradeBot mediator, SocialMessage message, SocialThread thread)
    {
        if (thread.kind != SocialThreadKind.ARGUMENT || !thread.Active)
        { return; }
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
        ChatSocialAudit.Journal.Event(new SocialEvent { kind = "MEDIATION", id = message.id, threadId = thread.id, room = message.roomId, actor = mediator.Social.Id, time = SocialTime.GameMinute, value = success ? 1 : 0, reason = success ? "accepted" : "rejected" });
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
            ConsiderMediationExit(member, mediator, message, thread);
        }
        if (success) { CloseSocialThread(thread, SocialEnd.RESOLVED); }
    }
```

### partial void BeforeSocialBot(

원본:
```csharp
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
```

수정본:
```csharp
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
        TickMediationReaction(bot);
    }
```

### private void DepartSocial(

원본:
```csharp
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
```

수정본:
```csharp
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
        SocialDeparted?.Invoke(bot, room, sympathy ? "친구의 퇴장에 동조" : "언쟁 방치 후 이탈", ChatRoomNotice.Leave);
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
        OfferDepartureReaction(bot, room, mind.ExitOpponent, farewell.id);
    }
```

## Assets/Scripts/MarketDay/TabletGroupChatUI.cs

### public void SelectRoom(

원본:
```csharp
    public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

수정본:
```csharp
    public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

### void Start()

원본:
```csharp
    void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            rect.GetComponent<Button>().onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; row.AngerDot.text = "●";
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

수정본:
```csharp
    void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        messageMeasure.richText = true;
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            row.Click = rect.GetComponent<Button>();
            row.Click.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; row.AngerDot.text = "●";
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

### private void EnsureMentionRoster(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && mentionSociety == society && mentionBotCount == count)
        { return; }
        var names = new List<string> { "방장" };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); } }
        mentions = new ChatSocialMentions(names); mentionSociety = society; mentionBotCount = count;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
```

### private string DisplayMessage(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    private string DisplayMessage(BotChatMessage message)
    {
        if (!displayedMessages.TryGetValue(message.Sequence, out string text))
        { text = message.IsRoomNotice ? ChatSocialMentions.Literal(message.Text) : mentions.Format(message.Text); displayedMessages.Add(message.Sequence, text); }
        return text;
    }
```

### private void BuildNoticeVisual(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    private void BuildNoticeVisual(Row row)
    {
        var background = new GameObject("Image :: Room Notice", typeof(RectTransform), typeof(Image));
        background.layer = row.Rect.gameObject.layer; background.transform.SetParent(row.Rect, false);
        row.NoticeBackground = background.GetComponent<Image>();
        var source = row.Bubble.GetComponent<Image>();
        row.NoticeBackground.sprite = source.sprite; row.NoticeBackground.type = Image.Type.Sliced;
        row.NoticeBackground.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        row.NoticeBackground.color = new Color(1f, 1f, 1f, .8f); row.NoticeBackground.raycastTarget = false;
        row.NoticeText = Instantiate(row.Text, row.Rect);
        row.NoticeText.name = "Text :: Room Notice"; row.NoticeText.fontSize = row.Text.fontSize * .86f;
        row.NoticeText.alignment = TextAlignmentOptions.Center; row.NoticeText.color = new Color(.76f, .81f, .84f, 1f);
        row.NoticeText.enableAutoSizing = false; row.NoticeText.textWrappingMode = TextWrappingModes.NoWrap;
        row.NoticeText.margin = Vector4.zero; row.NoticeText.fontStyle = FontStyles.Normal;
        row.NoticeText.overflowMode = TextOverflowModes.Ellipsis; row.NoticeText.raycastTarget = false;
        foreach (var rect in new[] { row.NoticeBackground.rectTransform, row.NoticeText.rectTransform })
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; }
        background.SetActive(false); row.NoticeText.gameObject.SetActive(false);
    }
```

### private void SetNoticeMode(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    private void SetNoticeMode(Row row, bool notice)
    {
        row.IsNotice = notice; row.Click.interactable = !notice;
        row.Text.gameObject.SetActive(!notice); row.Bubble.gameObject.SetActive(!notice);
        row.Avatar.gameObject.SetActive(!notice); row.Gauge.gameObject.SetActive(!notice);
        row.Nickname.gameObject.SetActive(!notice); row.Time.gameObject.SetActive(!notice);
        row.AngerDot.gameObject.SetActive(false);
        row.NoticeBackground.gameObject.SetActive(notice); row.NoticeText.gameObject.SetActive(notice);
    }
```

### private void LayoutNotice(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    private void LayoutNotice(Row row, BotChatMessage message)
    {
        DeviceTerminalUI.Set(row.NoticeText, DisplayMessage(message));
        float available = Mathf.Max(1f, row.Rect.rect.width - 40f * layoutScale);
        float width = Mathf.Min(available, row.NoticeText.GetPreferredValues(row.NoticeText.text).x + 36f * layoutScale);
        float height = 40f * layoutScale;
        row.NoticeBackground.rectTransform.sizeDelta = new Vector2(width, height);
        row.NoticeText.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width - 24f * layoutScale), height);
    }
```

### void Update()

원본:
```csharp
    void Update()
    {
        if (day == null)
        { return; }

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = message.Text;
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }

                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

수정본:
```csharp
    void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

### private void RebuildMessageLayout(

원본:
```csharp
    private void RebuildMessageLayout(float width)
    {
        layoutFrame = Time.frameCount;
        float y = scroll.content.anchoredPosition.y;
        long oldest = MessageCount > 0 ? Message(0).Sequence : 0;
        long removed = oldestSequence > 0 ? System.Math.Max(0, oldest - oldestSequence) : 0;
        if (!followNewest && removed > 0 && rowOffsets.Count > 0)
        {
            int removedRows = (int)System.Math.Min(removed, rowOffsets.Count);
            float removedHeight = removedRows < rowOffsets.Count ? rowOffsets[removedRows] : rowOffsets[rowOffsets.Count - 1] + rowHeights[rowHeights.Count - 1];
            y = Mathf.Max(0f, y - removedHeight);
        }

        if (!Mathf.Approximately(width, measuredWidth))
        {
            measuredHeights.Clear();
            measuredWidth = width;
        }

        rowOffsets.Clear();
        rowHeights.Clear();
        retainedSequences.Clear();
        float total = 0f;
        for (int i = 0; i < MessageCount; i++)
        {
            var message = Message(i);
            retainedSequences.Add(message.Sequence);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height))
            {
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(message.Nickname, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(message.Text, width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader;
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in measuredHeights.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
        }

        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        if (followNewest)
        {
            SnapToNewest();
        }
        else
        {
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, total - scroll.viewport.rect.height)));
        }

        oldestSequence = oldest;
        version = MessageVersion;
        dirty = true;
    }
```

수정본:
```csharp
    private void RebuildMessageLayout(float width)
    {
        layoutFrame = Time.frameCount;
        float y = scroll.content.anchoredPosition.y;
        long oldest = MessageCount > 0 ? Message(0).Sequence : 0;
        long removed = oldestSequence > 0 ? System.Math.Max(0, oldest - oldestSequence) : 0;
        if (!followNewest && removed > 0 && rowOffsets.Count > 0)
        {
            int removedRows = (int)System.Math.Min(removed, rowOffsets.Count);
            float removedHeight = removedRows < rowOffsets.Count ? rowOffsets[removedRows] : rowOffsets[rowOffsets.Count - 1] + rowHeights[rowHeights.Count - 1];
            y = Mathf.Max(0f, y - removedHeight);
        }

        if (!Mathf.Approximately(width, measuredWidth))
        {
            measuredHeights.Clear();
            measuredWidth = width;
        }

        rowOffsets.Clear();
        rowHeights.Clear();
        retainedSequences.Clear();
        float total = 0f;
        for (int i = 0; i < MessageCount; i++)
        {
            var message = Message(i);
            retainedSequences.Add(message.Sequence);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height))
            {
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(message.Nickname, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = message.IsRoomNotice ? Mathf.Max(rowHeight, 52f * layoutScale) : Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(DisplayMessage(message), width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader;
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in displayedMessages.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
            displayedMessages.Remove(sequence);
        }

        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        if (followNewest)
        {
            SnapToNewest();
        }
        else
        {
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, total - scroll.viewport.rect.height)));
        }

        oldestSequence = oldest;
        version = MessageVersion;
        dirty = true;
    }
```

## Assets/Scripts/NightShift/InvestorSociety.cs

### public bool Join(

원본:
```csharp
    public bool Join(TradeBot bot, int roomId)
    {
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

수정본:
```csharp
    public bool Join(TradeBot bot, int roomId)
    {
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

### public void Leave(

원본:
```csharp
    public void Leave(TradeBot bot, string reason)
    {
        int room = bot.Social.Psychology.Room;
        if (room == 0)
        { return; }
        GetRoom(room).Members.Remove(bot.Social.Id); bot.Social.Psychology.Room = 0;
        Orders?.Cancel(Orders.Get(bot)); Leaves++;
        Trace(bot, room + "번 방 이탈: " + reason);
    }
```

수정본:
```csharp
    public void Leave(TradeBot bot, string reason, ChatRoomNotice notice = ChatRoomNotice.Leave)
    {
        int room = bot.Social.Psychology.Room;
        if (room == 0)
        { return; }
        GetRoom(room).Members.Remove(bot.Social.Id); bot.Social.Psychology.Room = 0;
        Orders?.Cancel(Orders.Get(bot)); Leaves++;
        bot.Social.Psychology.MediationExitAt = -1;
        if (notice != ChatRoomNotice.None) { PostRoomNotice(room, notice, bot.Social.Nickname + " 님이 퇴장했습니다."); }
        Trace(bot, room + "번 방 이탈: " + reason);
    }
```

### public void PostRoomNotice(

원본:
```csharp
(신규 함수/선언)
```

수정본:
```csharp
    public void PostRoomNotice(int roomId, ChatRoomNotice notice, string text)
    {
        day ??= MarketDayController.Instance;
        if (day == null || notice == ChatRoomNotice.None)
        { return; }
        DeliverSocialSystemNotice(roomId, text, NextEventId(), notice);
    }
```

## 신규 C# 파일 전문

### Assets/Scripts/NightShift/ChatSocialSpeech.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed partial class InvestorSociety
{
    private static readonly Regex SpokenNumberPattern = new(@"\{(amount|memAmount|pct|memPct)\}(?:원|%)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static SocialRandom CosmeticRandom(InvestorPsychology mind, SocialMessage message, uint salt)
    {
        uint seed = unchecked((uint)message.id ^ (uint)(message.id >> 32) ^ (uint)mind.SocialId * 486187739u ^ salt);
        seed ^= seed >> 16; seed = unchecked(seed * 0x7feb352du); seed ^= seed >> 15; seed = unchecked(seed * 0x846ca68bu); seed ^= seed >> 16;
        return new SocialRandom(unchecked((int)seed));
    }
    public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null;
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT"))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        return community ? text : DecorateSocialText(mind, message, text);
    }
    private static string SpeechChoiceKey(SocialCommunityPhrases group, int index)
    {
        bool shared = group.context == "TARGET" && (group.act == "BRAG" || group.act == "LAMENT" || group.act == "ASK" || group.act == "REACT" || group.act == "SMALLTALK");
        return (shared ? group.key.Replace(".TARGET.", ".NONE.") : group.key) + "." + index;
    }
    private static SocialCommunityPhrases CommunityGroup(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None)
        { return null; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        bool departure = facts.notice == "OPPONENT_LEFT" && !string.IsNullOrEmpty(facts.departedName);
        if (watch && (facts.observationBotId != message.senderId || message.replyToId != 0))
        { return null; }
        if (!watch && !departure && (facts.notice != null || !template.key.Contains(".plain.")))
        { return null; }
        string context = watch || departure ? facts.notice : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Community(message.act, context);
        if (group == null && !watch && !departure) { group = ChatSocialAssets.Community(message.act, parent != null || message.targetId != 0 ? "TARGET" : "NONE"); }
        return group != null && SpeechRequirementsMatch(group.requires, facts) ? group : null;
    }
    private static string SpeechSlot(SocialFacts facts, string slot, string target, int dialect) => slot switch
    {
        "gain" or "loss" => ChatSocialNumbers.Amount(facts.amount, dialect),
        "return" => ChatSocialNumbers.Return(facts.pct, dialect),
        "moveTalk" => ChatSocialNumbers.Return(facts.observationPercent, dialect),
        "positionState" => facts.profitKind == "실현" ? "팔고 보니까" : facts.held ? "아직 들고 있는데" : null,
        _ => Slot(facts, slot, target)
    };
}
```

### Assets/Scripts/NightShift/ChatSocialNumbers.cs

```csharp
using System;
using System.Globalization;

/// <summary>Presentation only. Raw social facts and account values are never modified.</summary>
public static class ChatSocialNumbers
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    public static string Amount(string raw, int dialect, bool signed = false, bool compactNoun = false)
    {
        if (!decimal.TryParse(raw, NumberStyles.Number, Culture, out decimal value))
        { return raw; }
        decimal magnitude = Math.Abs(value), rounded = magnitude;
        if (magnitude >= 10000m)
        {
            decimal step = 10000m;
            while (step <= magnitude / 10m) { step *= 10m; }
            rounded = Math.Floor(magnitude / step) * step;
        }
        else if (magnitude >= 1000m) { rounded = Math.Floor(magnitude / 1000m) * 1000m; }
        else if (magnitude >= 100m) { rounded = Math.Floor(magnitude / 100m) * 100m; }
        else { rounded = Math.Floor(magnitude); }
        string sign = signed && value != 0 ? value > 0 ? "+" : "−" : "";
        if (magnitude > 0 && rounded == 0)
        { return sign + "1원 미만"; }
        if (rounded == 0)
        { return "0원"; }
        if (rounded >= 100000000m)
        { return sign + (rounded / 100000000m).ToString("0.#", Culture) + (compactNoun ? "억원쯤" : "억 정도"); }
        if (rounded >= 10000m)
        {
            string units = (rounded / 10000m).ToString("#,##0", Culture);
            if (compactNoun) { return sign + units + "만원쯤"; }
            // One '장' always means 10,000 won; 5,284,003 won must never become '5장'.
            return sign + units + (dialect == 1 ? "장" : dialect == 2 ? " 정도" : "만 정도");
        }
        if (rounded >= 1000m)
        { return sign + (rounded / 1000m).ToString("0", Culture) + (compactNoun ? "천원쯤" : "천 원 정도"); }
        return sign + rounded.ToString("0", Culture) + (compactNoun ? "원쯤" : "원 정도");
    }
    public static string Return(string raw, int dialect)
    {
        if (!decimal.TryParse(raw, NumberStyles.Number, Culture, out decimal value))
        { return raw; }
        decimal magnitude = Math.Abs(value);
        decimal rounded = Math.Round(magnitude, magnitude < 1m ? 2 : magnitude < 10m ? 1 : 0, MidpointRounding.AwayFromZero);
        string sign = value < 0 ? "−" : "";
        if (magnitude > 0 && rounded == 0)
        { return sign + "0.01" + (dialect == 1 ? "% 미만" : "퍼 미만"); }
        return sign + rounded.ToString("0.##", Culture) + (dialect == 1 ? "%" : "퍼");
    }
}
```

### Assets/Scripts/NightShift/ChatSocialMentions.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>UI-only nickname highlighting. A roster trie avoids scanning every bot for each message.</summary>
public sealed class ChatSocialMentions
{
    public const string MarkOpen = "<mark=#D4E1E832>";
    private sealed class Node
    {
        public readonly Dictionary<char, Node> Children = new();
        public bool Terminal;
    }
    private readonly Node root = new();
    private static readonly string[] Particles = { "님", "한테", "에게", "이랑", "은", "는", "이", "가", "을", "를", "랑", "도", "의", "아", "야" };
    public ChatSocialMentions(IEnumerable<string> nicknames)
    {
        foreach (string name in nicknames)
        {
            if (string.IsNullOrWhiteSpace(name))
            { continue; }
            var node = root;
            foreach (char letter in name)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.Terminal = true;
        }
    }
    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            int end = index == 0 || !Word(text[index - 1]) ? MatchEnd(text, start) : -1;
            if (end > start)
            {
                result.Append(MarkOpen); AppendLiteral(result, text, index, end); result.Append("</mark>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
    public static string Literal(string text)
    {
        if (string.IsNullOrEmpty(text)) { return ""; }
        var result = new StringBuilder(text.Length);
        AppendLiteral(result, text, 0, text.Length); return result.ToString();
    }
    private int MatchEnd(string text, int start)
    {
        var node = root; int end = -1;
        for (int i = start; i < text.Length && node.Children.TryGetValue(text[i], out node); i++)
        {
            if (node.Terminal && BoundaryAfter(text, i + 1)) { end = i + 1; }
        }
        return end;
    }
    private static bool Word(char letter) => char.IsLetterOrDigit(letter) || letter == '_';
    private static bool BoundaryAfter(string text, int index)
    {
        if (index >= text.Length || !Word(text[index]))
        { return true; }
        foreach (string particle in Particles)
        {
            if (index + particle.Length > text.Length || string.CompareOrdinal(text, index, particle, 0, particle.Length) != 0)
            { continue; }
            int end = index + particle.Length;
            if (end == text.Length || !Word(text[end])) { return true; }
            if (particle == "님")
            {
                foreach (string suffix in Particles)
                {
                    int tail = end + suffix.Length;
                    if (tail <= text.Length && string.CompareOrdinal(text, end, suffix, 0, suffix.Length) == 0 && (tail == text.Length || !Word(text[tail]))) { return true; }
                }
            }
        }
        return false;
    }
    private static void AppendLiteral(StringBuilder result, string text, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            // Quote each '<' separately: even user-supplied </noparse> cannot escape into a TMP tag.
            if (text[i] == '<') { result.Append("<noparse><</noparse>"); }
            else { result.Append(text[i]); }
        }
    }
}
```

### Assets/Scripts/NightShift/ChatSocialMediation.cs

```csharp
using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public double MediationExitAt = -1, PublicReturnAt = -1;
    public int MediationExitRoom, MediationExitOpponent;
    public long MediationExitCause, MediationExitThread;
}
public sealed partial class InvestorSociety
{
    private void ConsiderMediationExit(TradeBot member, TradeBot mediator, SocialMessage message, SocialThread thread)
    {
        var mind = member.Social.Psychology;
        if (mind.MediationExitAt >= 0 || !Present(member, message.roomId) || member.Social.Stress < Config.Get("mediationStressMin") ||
            message.roomId > 0 && GetRoom(message.roomId)?.OwnerBotId == member.Social.Id)
        { return; }
        double pressure = Math.Max(0, Math.Min(1, (member.Social.Stress - Config.Get("mediationStressMin")) / (100 - Config.Get("mediationStressMin"))));
        double chance = Config.Get("mediationExitBaseChance") + pressure * (Config.Get("mediationExitMaxChance") - Config.Get("mediationExitBaseChance"));
        if (mind.SocialRng.Next() >= chance)
        { return; }
        int opponent = 0; double anger = -1;
        foreach (int id in thread.participants)
        {
            if (id == member.Social.Id || id == mediator.Social.Id)
            { continue; }
            if (mind.Relation(id).anger > anger) { opponent = id; anger = mind.Relation(id).anger; }
        }
        mind.MediationExitAt = SocialTime.Seconds + Config.Get("mediationExitDelayMinSec") + mind.SocialRng.Next() * (Config.Get("mediationExitDelayMaxSec") - Config.Get("mediationExitDelayMinSec"));
        mind.MediationExitRoom = message.roomId; mind.MediationExitOpponent = opponent; mind.MediationExitCause = message.id; mind.MediationExitThread = thread.id;
        ChatSocialAudit.Journal.Event(new SocialEvent { kind = "MEDIATION_OVERLOAD", id = message.id, room = message.roomId, threadId = thread.id, actor = member.Social.Id, target = mediator.Social.Id,
            time = SocialTime.GameMinute, value = member.Social.Stress, reason = "stress-exit-pending" });
    }
    private void TickMediationReaction(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        if (mind.PublicReturnAt >= 0 && SocialTime.GameMinute >= mind.PublicReturnAt)
        {
            mind.PublicReturnAt = -1;
            SocialSystemNotice?.Invoke(0, bot.Social.Nickname + " 님이 입장했습니다.", ChatSocialIds.Next(), ChatRoomNotice.Join);
        }
        if (mind.MediationExitAt < 0 || SocialTime.Seconds < mind.MediationExitAt)
        { return; }
        int room = mind.MediationExitRoom; long cause = mind.MediationExitCause;
        mind.MediationExitAt = -1;
        if (!Present(bot, room) || mind.MediationExitThread > 0 && exitedFight.Contains(mind.MediationExitThread))
        { return; }
        if (mind.MediationExitThread > 0) { exitedFight.Add(mind.MediationExitThread); }
        CloseSocialThread(FindSocialThread(mind.MediationExitThread), SocialEnd.EXIT);
        mind.Intentions.RemoveAll(intent => intent.roomId == room);
        if (room == 0)
        {
            mind.PublicReturnAt = SocialTime.GameMinute + Config.Get("mediationRejoinMinutes");
            SocialSystemNotice?.Invoke(room, bot.Social.Nickname + " 님이 퇴장했습니다.", cause, ChatRoomNotice.Leave);
        }
        else
        {
            mind.RejoinAt[room] = SocialTime.GameMinute + Config.Get("mediationRejoinMinutes");
            SocialDeparted?.Invoke(bot, room, "중재를 듣고 스트레스로 대화 중단", ChatRoomNotice.Leave);
            GetRoom(room)?.Members.Remove(mind.SocialId); mind.Room = 0;
            mind.ExitAt = -1; mind.ExitIsSympathy = false;
            if (room <= PlayerRoomCount) { VoluntaryExits++; }
        }
        ChatSocialAudit.Journal.Event(new SocialEvent { kind = "MEDIATION_STRESS_EXIT", id = cause, room = room, threadId = mind.MediationExitThread, actor = mind.SocialId,
            target = mind.MediationExitOpponent, time = SocialTime.GameMinute, value = bot.Social.Stress, reason = "overwhelmed-by-mediation" });
        OfferDepartureReaction(bot, room, mind.MediationExitOpponent, cause);
    }
    private void ReactToRejectedMediation(SocialMessage message, SocialThread thread, List<TradeBot> witnesses)
    {
        if (thread == null || !thread.Active || thread.kind != SocialThreadKind.ARGUMENT || !Attack(message.act) ||
            !socialMessageIndex.TryGetValue(message.replyToId, out var parent) || parent.act != SocialAct.MEDIATE)
        { return; }
        foreach (var observer in witnesses)
        {
            var mind = observer.Social.Psychology;
            if (observer.Social.Id == message.targetId || observer.Social.Id == message.senderId || !thread.participants.Contains(mind.SocialId) ||
                mind.Traits.Bravado < Config.Get("mediationTauntBravadoMin") || mind.Relation(message.senderId).anger < Config.Get("mediationTauntAngerMin") ||
                pendingReplies.ContainsKey(mind.SocialId) || mind.Intentions.Exists(intent => intent.threadId == thread.id))
            { continue; }
            if (mind.SocialRng.Next() >= Config.Get("mediationTauntChance"))
            { continue; }
            mind.Intentions.Add(new SocialIntent { motive = SocialMotive.WITNESSED_EVENT, forcedAct = SocialAct.MOCK, roomId = message.roomId, threadId = thread.id,
                replyToId = message.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
            break;
        }
    }
    private void OfferDepartureReaction(TradeBot departed, int room, int opponentId, long cause)
    {
        if (!SocialBots.TryGetValue(opponentId, out var opponent) || !Present(opponent, room) || !opponent.Social.Psychology.ActiveAt((float)SocialTime.Seconds))
        { return; }
        var mind = opponent.Social.Psychology;
        if (mind.Traits.Bravado < Config.Get("mediationTauntBravadoMin") || mind.Relation(departed.Social.Id).anger < Config.Get("mediationTauntAngerMin") ||
            DepartureReactionRoll(opponentId, cause) >= Config.Get("departureReactionChance"))
        { return; }
        Offer(opponent, room, SocialMotive.WITNESSED_EVENT, new SocialFacts { notice = "OPPONENT_LEFT", departedName = departed.Social.Nickname, eventId = cause }, SocialAct.REACT, cause: cause);
    }
    private static double DepartureReactionRoll(int botId, long eventId)
    {
        // An optional event reaction must not shift every later decision when the bot stays silent.
        uint seed = unchecked((uint)eventId ^ (uint)(eventId >> 32) ^ (uint)botId * 486187739u ^ 0x4a7e613bu);
        seed ^= seed >> 16; seed = unchecked(seed * 0x7feb352du); seed ^= seed >> 15; seed = unchecked(seed * 0x846ca68bu); seed ^= seed >> 16;
        return new SocialRandom(unchecked((int)seed)).Next();
    }
}
```

### Tests/ChatSocial/TestsSpeech.cs

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static bool CommunityLine(SocialMessage message) => message.debug?.speechKey?.StartsWith("community.", StringComparison.Ordinal) == true;
    private static SocialFacts SpeechFacts(bool gain = true) => new()
    {
        symbol = "NM", stockName = "노보손해보험", amount = gain ? "5,284,003" : "-5,284,003", pct = gain ? "+50.0" : "-50.0",
        price = "5,600", quantity = "1,234", avgPrice = "3,733", held = true, profitable = gain, losing = !gain, profitKind = "평가"
    };
    private static SocialMessage VoiceMessage(InvestorPsychology mind, long id, SocialAct act = SocialAct.BRAG, SocialFacts facts = null) => new()
    { id = id, senderId = mind.SocialId, act = act, facts = facts ?? SpeechFacts(), debug = new SocialMessageDebug() };
    private static SocialTemplate VoiceTemplate(string act = "BRAG") => ChatSocialAssets.Templates.templates.Single(t => t.key == act + ".casual.1.plain.02");
    private static void SpeechScenarios()
    {
        ChatSocialAssets.Reload();
        var population = Enumerable.Range(1, 1000).Select(id => new InvestorPsychology(id, 73129)).ToArray();
        var playful = population.First(m => !m.Traits.Serious); var serious = population.First(m => m.Traits.Serious);
        int seriousCount = population.Count(m => m.Traits.Serious);
        Check(seriousCount >= 400 && seriousCount <= 500, $"Stable speech trait population: serious={seriousCount}/1000, both types present");
        Check(population.All(m => m.Traits.Serious == new InvestorPsychology(m.SocialId, 73129).Traits.Serious), "Speech disposition repeats for the same seed and ID");
        Check(population.Any(m => m.Traits.Serious && m.Traits.Politeness < 34) && population.Any(m => !m.Traits.Serious && m.Traits.Politeness >= 67), "Seriousness is independent of polite/rough register");
        var oldRandom = new SocialRandom(98271); var newRandom = new SocialRandom(98271);
        foreach (string trait in new[] { "temper", "grudge", "suggestibility", "sociability", "bravado", "politeness" })
        { oldRandom.Normal(ChatSocialConfig.Current.Get(trait + "Mean"), ChatSocialConfig.Current.Get(trait + "Sd")); }
        _ = new SocialTraits(newRandom, ChatSocialConfig.Current);
        Check(oldRandom.State == newRandom.State, "Adding a voice trait consumes no decision/friendship RNG draws");
        var restored = new InvestorPsychology(serious.SocialId, 73129);
        var snapshot = JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(JsonUtility.ToJson(serious.CaptureSocial()));
        Check(restored.RestoreSocial(snapshot) && restored.Traits.Serious && restored.SocialRng.State == serious.SocialRng.State, "New speech trait survives save/restore with social RNG intact");
        var oldSave = JsonNode.Parse(JsonUtility.ToJson(playful.CaptureSocial()));
        oldSave["traits"].AsObject().Remove("speechVersion"); oldSave["traits"].AsObject().Remove("serious");
        var migrated = new InvestorPsychology(playful.SocialId, 73129);
        Check(migrated.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(oldSave.ToJsonString())) && !migrated.Traits.Serious && migrated.Traits.Politeness == playful.Traits.Politeness, "Old v2 traits migrate without resetting existing personality");
        oldSave["version"] = 1;
        Check(migrated.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(oldSave.ToJsonString())), "Old v1 social snapshot with traits remains loadable");
        snapshot.traits = JsonUtility.FromJson<SocialTraits>("{\"speechVersion\":99}");
        Check(!restored.RestoreSocial(snapshot), "Unknown speech save version is rejected without overwriting the bot");

        var template = VoiceTemplate(); int playfulLines = 0, seriousLines = 0; bool repeated = true, truthful = true;
        uint playfulState = playful.SocialRng.State, seriousState = serious.SocialRng.State;
        for (int id = 1; id <= 10000; id++)
        {
            var a = VoiceMessage(playful, id); var b = VoiceMessage(serious, id);
            string factsBefore = JsonUtility.ToJson(a.facts);
            string first = InvestorSociety.RenderSocialText(playful, template, a, null, "");
            string second = InvestorSociety.RenderSocialText(serious, template, b, null, "");
            if (CommunityLine(a)) { playfulLines++; }
            if (CommunityLine(b)) { seriousLines++; }
            repeated &= first == InvestorSociety.RenderSocialText(playful, template, a, null, "");
            truthful &= factsBefore == JsonUtility.ToJson(a.facts) && first.Contains("노보손해보험") && !first.Contains("{") && !second.Contains("{");
        }
        Check(playfulLines >= 6700 && playfulLines <= 7300, $"Playful eligible community rate: {playfulLines}/10000 (target 70%)");
        Check(seriousLines >= 140 && seriousLines <= 260, $"Serious eligible community rate: {seriousLines}/10000 (target 2%)");
        Check(repeated && playfulState == playful.SocialRng.State && seriousState == serious.SocialRng.State, "Voice/number choices are replayable and never advance social decision RNG");
        Check(truthful, "Colloquial messages preserve raw P/L metadata and resolve every slot");
        Check(ChatSocialNumbers.Amount("5,284,003", 0) == "500만 정도" && ChatSocialNumbers.Amount("5,284,003", 1) == "500장" && ChatSocialNumbers.Amount("5,284,003", 2) == "500 정도", "5,284,003 won uses natural approximate Korean amounts");
        Check(ChatSocialNumbers.Amount("50,000", 1) == "5장" && ChatSocialNumbers.Amount("5,284,003", 1) != "5장", "Jang consistently means ten thousand won, never a hundredfold error");
        Check(ChatSocialNumbers.Amount("-5,284,003", 0) == "500만 정도" && ChatSocialNumbers.Amount("-5,284,003", 0, signed: true).StartsWith("−500"), "Loss magnitude and signed P/L remain distinguishable");
        Check(ChatSocialNumbers.Return("+50.0", 0) == "50퍼" && ChatSocialNumbers.Return("+50.0", 1) == "50%" && ChatSocialNumbers.Return("-50.0", 1) == "−50%", "Spoken return drops redundant decimal/plus while retaining loss direction");
        Check(ChatSocialNumbers.Amount("0", 1) == "0원" && ChatSocialNumbers.Amount("0.5", 1) == "1원 미만" && ChatSocialNumbers.Return("-0.001", 0) == "−0.01퍼 미만", "Tiny nonzero results do not turn into a false zero");
        Check(ChatSocialNumbers.Amount("528", 0) == "500원 정도" && ChatSocialNumbers.Amount("5284", 0) == "5천 원 정도" && ChatSocialNumbers.Amount("528400300", 0) == "5억 정도", "Small and large amounts keep their Korean scale");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(ChatSocialNumbers.Amount("5,284,003", 1) == "500장" && ChatSocialNumbers.Return("+50.0", 0) == "50퍼", "Chat amount parsing is independent of OS number culture");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        SpeechGuards(playful, serious); MentionScenarios(); VoiceReplayAndSamples();
        ChatSocialAssets.Reload();
    }
    private static void SpeechGuards(InvestorPsychology playful, InvestorPsychology serious)
    {
        var original = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var catalog = ChatSocialAssets.Templates;
        var config = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original));
        config.values.Single(v => v.key == "communityPlayfulChance").value = 1;
        config.values.Single(v => v.key == "communitySeriousChance").value = 0;
        config.values.Single(v => v.key == "colloquialNumberChance").value = 1;
        ChatSocialAssets.Install(config, acts, catalog);
        try
        {
            var template = VoiceTemplate(); var message = VoiceMessage(playful, 51001);
            string text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(CommunityLine(message), "Probability one selects an eligible community phrase");
            var firstKey = message.debug.speechKey; playful.RecentSpeechKeys.Enqueue(firstKey);
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(message.debug.speechKey != firstKey, "Recent community wording is skipped without changing the semantic template");
            playful.RecentSpeechKeys.Clear();
            message = VoiceMessage(serious, 51002); InvestorSociety.RenderSocialText(serious, template, message, null, "");
            Check(!CommunityLine(message), "Probability zero never chooses community wording");
            message = VoiceMessage(playful, 51003, facts: SpeechFacts(false));
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Loss facts cannot select a gain boast from the community catalog");
            message = VoiceMessage(playful, 51004, SocialAct.LAMENT, SpeechFacts(false));
            text = InvestorSociety.RenderSocialText(playful, VoiceTemplate("LAMENT"), message, null, "");
            Check(CommunityLine(message) && !text.Contains("-5,284,003") && (text.Contains("손실") || text.Contains("마이너스") || text.Contains("−50")), "Loss complaint is colloquial without claiming a profit");
            message = VoiceMessage(playful, 51005); message.facts.signal = true;
            template = new SocialTemplate { key = "BRAG.casual.1.plain.02", act = "BRAG", style = "casual", variant = "plain", text = "{price}원 {quantity}주, {amount}원, {pct}%" };
            text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message) && text.Contains("5,600원 1,234주, 5,284,003원, +50.0%"), "Signal/order facts retain exact prices, quantities, amounts and returns");
            template.promisedAction = SocialPromisedAction.Buy; message.facts.signal = false;
            text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message) && text.Contains("5,600원 1,234주"), "Voice rewriting cannot erase an actual future-trade promise");
            template = VoiceTemplate(); message.memory = true;
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Memory narration is not replaced by an unrelated current result");
            message = VoiceMessage(playful, 51006); message.facts.notice = "PLAYER_KICK";
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Moderation notice cannot turn into a boast or a joke about prices");
            message = VoiceMessage(playful, 51007, SocialAct.AGREE); message.targetId = 2;
            text = InvestorSociety.RenderSocialText(playful, VoiceTemplate("AGREE"), message, new SocialMessage { act = SocialAct.LAMENT }, "익명002");
            Check(message.debug.speechKey.StartsWith("community.LAMENT.AGREE") && !text.Contains("ㅋㅋ"), "Agreement with a loss uses an empathetic response rather than profit cheering");
            var watch = catalog.templates.Single(t => t.key == "watch.RISE.rough.1.01");
            var facts = SpeechFacts(); facts.notice = "WATCH_RISE"; facts.observationBotId = serious.SocialId; facts.observationPrice = "3,000"; facts.observationPercent = "+50.0"; facts.observedAt = 5;
            message = VoiceMessage(serious, 51008, SocialAct.REACT, facts);
            text = InvestorSociety.RenderSocialText(serious, watch, message, null, "");
            Check(!CommunityLine(message) && !text.Contains("ㅋㅋ") && !text.Contains("ㄷㄷ") && !text.Contains("뭐임"), "Serious watcher avoids pre-existing meme-style surprise outside its rare roll");
            message = VoiceMessage(playful, 51009, SocialAct.REACT, facts);
            InvestorSociety.RenderSocialText(playful, watch, message, null, "");
            Check(!CommunityLine(message), "Community rewrite cannot impersonate another bot's watch observation");
            facts.observationBotId = playful.SocialId;
            text = InvestorSociety.RenderSocialText(playful, watch, message, null, "");
            Check(CommunityLine(message) && text.Contains("노보손해보험") && !text.Contains("수익"), "Watch surprise uses observed movement, not invented portfolio profit");
        }
        finally { ChatSocialAssets.Install(original, acts, catalog); }
        var invalid = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original)); invalid.values.Single(v => v.key == "communitySeriousChance").value = 1.1;
        bool rejected = false;
        try { ChatSocialAssets.Install(invalid, acts, catalog); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ReferenceEquals(original, ChatSocialConfig.Current), "Out-of-range voice probability cannot replace the live config");
    }
    private static void MentionScenarios()
    {
        var mentions = new ChatSocialMentions(new[] { "개미", "개미왕", "노보호가256", "방장", "A<B" });
        string open = ChatSocialMentions.MarkOpen;
        Check(mentions.Format("@노보호가256 그 말 맞음") == open + "@노보호가256</mark> 그 말 맞음", "At-mention receives a translucent inline background");
        Check(mentions.Format("개미왕님, 방장은 어떻게 봄?") == open + "개미왕</mark>님, " + open + "방장</mark>은 어떻게 봄?", "Multiple mentions and Korean suffixes highlight only the nickname");
        Check(mentions.Format("개미왕님은 어떰? 개미왕") == open + "개미왕</mark>님은 어떰? " + open + "개미왕</mark>", "Longest nickname match wins and repeated mentions are retained");
        Check(mentions.Format("개미왕국 노보호가2560 익명999") == "개미왕국 노보호가2560 익명999", "Partial names, longer IDs and unknown names remain plain text");
        Check(mentions.Format("hello 개미왕 ㅋㅋ\n@방장") == "hello " + open + "개미왕</mark> ㅋㅋ\n" + open + "@방장</mark>", "Nickname highlight works across wrapped/multiline messages");
        string hostile = mentions.Format("<size=999>개미왕</size> </noparse><mark=#FFFFFFFF> @방장");
        Check(!hostile.Contains("<size=999>") && !hostile.Contains("<mark=#FFFFFFFF>") && hostile.Contains("<noparse><</noparse>/noparse>"), "Message text cannot inject TMP size, color or noparse tags");
        Check(mentions.Format("A<B") == open + "A<noparse><</noparse>B</mark>" && mentions.Format(null) == "", "Special-character nickname and empty message are safe");
        var many = new ChatSocialMentions(Enumerable.Range(1, 1200).Select(i => "익명" + i.ToString("D4")));
        string sample = "@익명1191 익명0001님 ㅋㅋ 익명11910은 누구";
        Check(many.Format(sample).Split("<mark=").Length - 1 == 2, "1200-bot roster highlights exact names without scanning all bots per character");
    }
    private static void VoiceReplayAndSamples()
    {
        var on = SocialReplay(73129, 8, steps: 1800);
        var config = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(ChatSocialConfig.Current));
        foreach (var row in config.values.Where(v => v.key == "communityPlayfulChance" || v.key == "communitySeriousChance" || v.key == "colloquialNumberChance" || v.key == "seriousColloquialNumberChance")) { row.value = 0; }
        ChatSocialAssets.Install(config, ChatSocialAssets.Acts, ChatSocialAssets.Templates);
        var off = SocialReplay(73129, 8, steps: 1800);
        Check(on.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.targetId}:{m.threadId}:{m.templateKey}").SequenceEqual(off.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.targetId}:{m.threadId}:{m.templateKey}")), "Voice enabled/disabled leaves all message decisions, replies and threads unchanged");
        Check(on.Journal.Changes.Select(JsonUtility.ToJson).SequenceEqual(off.Journal.Changes.Select(JsonUtility.ToJson)) && on.FactErrors == 0 && off.FactErrors == 0, "Cosmetic dialect does not change emotions, economic facts or event effects");
        Check(on.Messages.Any(CommunityLine) && !off.Messages.Any(CommunityLine), "Production scheduler publishes community voices only when enabled");
        string folder = Path.Combine(root, "Validation/ChatVoice"); Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "conversation-samples.txt"), on.Messages.Where(CommunityLine).Take(45).Select(m => $"[{m.debug.speechDisposition}] #{m.senderId} {m.text} ({m.debug.speechKey})"));
        File.WriteAllLines(Path.Combine(folder, "conversation-raw.jsonl"), on.Messages.Select(JsonUtility.ToJson));
    }
}
```

### Tests/ChatSocial/TestsMediation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static object InvokeSociety(InvestorSociety society, string method, params object[] arguments) =>
        typeof(InvestorSociety).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(society, arguments);
    private static void SpeechPersonality(TradeBot bot, bool serious, int polite, int temper, int sociability, int bravado)
    {
        var saved = JsonNode.Parse(JsonUtility.ToJson(bot.Social.Psychology.CaptureSocial()));
        saved["traits"]["serious"] = serious; saved["traits"]["politeness"] = polite; saved["traits"]["temper"] = temper;
        saved["traits"]["sociability"] = sociability; saved["traits"]["bravado"] = bravado;
        bot.Social.Psychology.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(saved.ToJsonString()));
    }
    private static void MediationScenarios()
    {
        ChatSocialAssets.Reload();
        var original = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var templates = ChatSocialAssets.Templates;
        var fixture = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original));
        void Setting(string key, double value) { fixture.values.Single(v => v.key == key).value = value; }
        Setting("thirdPartyChance", 1); Setting("mediationExitBaseChance", 1); Setting("mediationExitMaxChance", 1);
        Setting("mediationExitDelayMinSec", 8); Setting("mediationExitDelayMaxSec", 8);
        Setting("mediationTauntChance", 1); Setting("departureReactionChance", 1);
        Setting("mediationStage3Scale", 1); Setting("communityPlayfulChance", 1); Setting("communitySeriousChance", 0);
        var folder = Path.Combine(root, "Validation/ChatVoice"); Directory.CreateDirectory(folder);
        try
        {
            RunCase(true); RunCase(false);
            Setting("mediationBase", 0); ChatSocialAssets.Install(fixture, acts, templates);
            var publicFixture = CreateFight(9021, 0); var s = publicFixture.society; var a = publicFixture.a; var b = publicFixture.b; var c = publicFixture.c;
            a.Social.Stress = 100; b.Social.Stress = 10;
            var message = new SocialMessage { id = 81001, senderId = c.Social.Id, roomId = 0, act = SocialAct.MEDIATE, threadId = publicFixture.thread.id };
            int leaves = 0, returns = 0;
            s.SocialSystemNotice += (room, text, id, kind) => { if (room == 0 && kind == ChatRoomNotice.Leave) { leaves++; } if (room == 0 && kind == ChatRoomNotice.Join) { returns++; } };
            InvokeSociety(s, "Mediate", c, message, publicFixture.thread);
            s.SocialTime.Set(1, 610, a.Social.Psychology.MediationExitAt + 1);
            InvokeSociety(s, "TickMediationReaction", a);
            Check(!s.Present(a, 0) && a.isActiveAndEnabled && leaves == 1, "Public stress exit pauses lounge participation while the investor keeps trading");
            double returnAt = a.Social.Psychology.PublicReturnAt;
            s.SocialTime.Set(1, returnAt, s.SocialTime.Seconds + 70); InvokeSociety(s, "TickMediationReaction", a);
            Check(s.Present(a, 0) && returns == 1, "Public return resumes visibility once and emits a centered join notice");
            foreach (var kind in new[] { ChatRoomNotice.Join, ChatRoomNotice.Leave, ChatRoomNotice.Kick })
            {
                var notice = new BotChatMessage(1, null, 1, 600, "참여자 알림", "SYSTEM.ROOM", ChatEvent.Agreement, notice: kind);
                Check(notice.IsRoomNotice && notice.BotId == 0 && notice.Notice == kind, "Typed " + kind + " notice is distinguishable from ordinary bubble content");
            }
            var bulletin = new BotChatMessage(2, null, 1, 600, "시장 안내", "bulletin", ChatEvent.RiskChanged);
            Check(!bulletin.IsRoomNotice, "Ordinary market news is not mistaken for an entry/exit notice");
        }
        finally { ChatSocialAssets.Install(original, acts, templates); }

        void RunCase(bool accepted)
        {
            Setting("mediationBase", accepted ? 1 : 0); ChatSocialAssets.Install(fixture, acts, templates);
            var data = CreateFight(73129, 1); var society = data.society; var a = data.a; var b = data.b; var c = data.c; var thread = data.thread;
            var trace = new List<string>();
            society.SocialMessagePublished += m => trace.Add(society.SocialBots[m.senderId].Social.Nickname + ": " + m.text);
            int departures = 0;
            society.SocialDeparted += (bot, room, reason, kind) =>
            {
                if (kind != ChatRoomNotice.None) { trace.Add("── " + bot.Social.Nickname + " 님이 퇴장했습니다. ──"); }
                departures++;
            };
            a.Social.Stress = accepted ? 20 : 100; b.Social.Stress = 10;
            var source = society.CaptureFacts(a, StockManager.Instance.stocks[1]);
            SocialMessage Emit(TradeBot author, SocialAct act, int target, long reply = 0)
            {
                SocialMessage published = null;
                void Record(SocialMessage message) { if (message.senderId == author.Social.Id) { published = message; } }
                society.SocialMessagePublished += Record;
                author.Social.Psychology.Intentions.Clear();
                InvokeSociety(society, "GenerateSocialMessage", author, new SocialIntent { roomId = 1, motive = SocialMotive.REPLY_OBLIGATION, forcedAct = act, targetId = target,
                    threadId = thread.id, replyToId = reply, facts = source });
                society.SocialMessagePublished -= Record; return published;
            }
            var opening = Emit(a, SocialAct.INSULT, b.Social.Id);
            var answer = Emit(b, SocialAct.MOCK, a.Social.Id, opening.id);
            // Give this eligible, actually invited third participant a turn through the same production pipeline.
            c.isActiveAndEnabled = true;
            c.Social.Psychology.Relation(a.Social.Id).affinity = 70;
            InvokeSociety(society, "InviteThirdParty", thread, answer, new List<TradeBot> { c });
            Check(c.Social.Psychology.Intentions.Any(i => i.forcedAct == SocialAct.MEDIATE), "Calm sociable witness chooses mediation even when a friend is involved");
            double angerA = a.Social.Psychology.Relation(b.Social.Id).anger, angerB = b.Social.Psychology.Relation(a.Social.Id).anger;
            var mediated = Emit(c, SocialAct.MEDIATE, a.Social.Id);
            Check(mediated != null, "Mediator publishes an actual generated sentence in the ongoing argument");
            if (accepted)
            {
                Check(a.Social.Psychology.Relation(b.Social.Id).anger < angerA && b.Social.Psychology.Relation(a.Social.Id).anger < angerB && thread.endReason == SocialEnd.RESOLVED, "Accepted mediation reduces both directional anger gauges and resolves the argument");
                Check(a.Social.Psychology.MediationExitAt < 0 && b.Social.Psychology.MediationExitAt < 0 && departures == 0, "Low-stress participants do not exit merely because someone mediates");
                var late = society.SocialBots[4]; late.isActiveAndEnabled = true;
                SpeechPersonality(late, true, 80, 10, 100, 10);
                InvokeSociety(society, "InviteThirdParty", thread, mediated, new List<TradeBot> { late });
                Check(late.Social.Psychology.ActiveThread == 0 && !late.Social.Psychology.Intentions.Any(i => i.threadId == thread.id), "Resolved mediation cannot trap a late observer in a closed thread");
                trace.Add("[검증 상태] 양쪽 분노 하락 · 언쟁 종료");
            }
            else
            {
                Check(thread.Active && a.Social.Psychology.MediationExitAt > society.SocialTime.Seconds && b.Social.Psychology.MediationExitAt < 0, "Rejected mediation keeps the conversation open and schedules only the overloaded investor to leave");
                var refusal = Emit(a, SocialAct.REBUT, c.Social.Id, mediated.id);
                b.Social.Psychology.Intentions.Clear();
                Private<Dictionary<int, long>>(society, "pendingReplies").Remove(b.Social.Id);
                InvokeSociety(society, "ReactToRejectedMediation", refusal, thread, new List<TradeBot> { b });
                var taunt = b.Social.Psychology.Intentions.FirstOrDefault(i => i.forcedAct == SocialAct.MOCK);
                Check(taunt != null && taunt.targetId == a.Social.Id && taunt.replyToId == refusal.id, "An angry, boastful opponent who heard the refusal may taunt its author");
                if (taunt != null) { InvokeSociety(society, "GenerateSocialMessage", b, taunt); }
                double deadline = a.Social.Psychology.MediationExitAt;
                society.SocialTime.Set(1, 605, deadline - .1); InvokeSociety(society, "TickMediationReaction", a);
                Check(a.Social.Psychology.Room == 1 && departures == 0, "Stress departure leaves a brief window for refusal and a reply");
                society.SocialTime.Set(1, 606, deadline + .1); InvokeSociety(society, "TickMediationReaction", a);
                Check(a.Social.Psychology.Room == 0 && departures == 1 && !thread.Active && !a.Social.Psychology.Intentions.Any(i => i.roomId == 1), "Stress exit removes membership and stale room intentions through the existing departure hook");
                Check(society.VoluntaryExits == 1, "Stress-based private-room exit counts in R5 voluntary departures");
                var reaction = b.Social.Psychology.Intentions.FirstOrDefault(i => i.facts?.notice == "OPPONENT_LEFT");
                Check(reaction != null && reaction.facts.departedName == a.Social.Nickname, "Opponent reacts to the actual departure using the departed nickname");
                if (reaction != null) { InvokeSociety(society, "GenerateSocialMessage", b, reaction); }
                int before = departures; InvokeSociety(society, "TickMediationReaction", a);
                Check(departures == before && a.Social.Psychology.RejoinAt[1] > society.SocialTime.GameMinute, "Departure is emitted once and respects the rejoin cooldown");
                b.Social.Psychology.MediationExitAt = society.SocialTime.Seconds;
                b.Social.Psychology.MediationExitThread = thread.id; b.Social.Psychology.MediationExitRoom = 1;
                InvokeSociety(society, "TickMediationReaction", b);
                Check(b.Social.Psychology.Room == 1 && departures == before, "One argument cannot trigger cascading stress departures of both opponents");
            }
            string path = Path.Combine(folder, accepted ? "mediation-accepted.txt" : "mediation-rejected.txt");
            File.WriteAllLines(path, trace);
        }
    }
    private static (InvestorSociety society, TradeBot a, TradeBot b, TradeBot c, SocialThread thread) CreateFight(int seed, int room)
    {
        var replay = SocialReplay(seed, 8, steps: 0); var society = replay.Society;
        var a = society.SocialBots[1]; var b = society.SocialBots[2]; var c = society.SocialBots[3];
        foreach (var bot in society.SocialBots.Values) { bot.isActiveAndEnabled = false; bot.Social.Psychology.Room = room; bot.Social.Psychology.Intentions.Clear(); }
        a.isActiveAndEnabled = b.isActiveAndEnabled = true;
        SpeechPersonality(a, false, 10, 70, 50, 40); SpeechPersonality(b, false, 10, 70, 50, 90); SpeechPersonality(c, true, 90, 10, 100, 10);
        // Choose a common time in the opponent's real activity window; no market speed changes.
        int activeAt = Enumerable.Range(0, 180).First(t => b.Social.Psychology.ActiveAt(t) && b.Social.Psychology.ActiveAt(t + 12));
        society.SocialTime.Set(1, 600, activeAt);
        var thread = new SocialThread { id = 9001, roomId = room, kind = SocialThreadKind.ARGUMENT, maxTurns = 8, symbol = "S1" };
        thread.participants.Add(a.Social.Id); thread.participants.Add(b.Social.Id);
        if (room == 0) { thread.participants.Add(c.Social.Id); c.isActiveAndEnabled = true; }
        Private<Dictionary<long, SocialThread>>(society, "socialThreads").Add(thread.id, thread); society.SocialRoom(room).threads.Add(thread);
        a.Social.Psychology.ActiveThread = b.Social.Psychology.ActiveThread = thread.id;
        a.Social.Psychology.Relation(b.Social.Id).anger = 80; b.Social.Psychology.Relation(a.Social.Id).anger = 80;
        return (society, a, b, c, thread);
    }
}
```
