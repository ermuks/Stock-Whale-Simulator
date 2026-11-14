using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed partial class InvestorSociety
{
    private readonly Dictionary<int, Queue<SocialMessage>> witnessedBrags = new();
    public long MemoryEligibleAttempts { get; private set; }
    public long MemoryTemplateBlocked { get; private set; }
    private static bool MemoryFits(SocialMemory memory, int speaker, int target, SocialAct act)
    {
        if (act == SocialAct.MOCK || act == SocialAct.REBUT)
        { return target > 0 && memory.targetId == target && (memory.type == SocialMemoryKind.FOUGHT || memory.type == SocialMemoryKind.SAW_BRAG_THEN_CRASH); }
        if (act == SocialAct.WARN_PLAYER || act == SocialAct.DOUBT)
        { return target == -1 && memory.actorId == -1 && memory.targetId == speaker && (memory.type == SocialMemoryKind.KICKED || memory.type == SocialMemoryKind.LOSS_BY_LEAD); }
        if (act == SocialAct.PRAISE_PLAYER)
        { return memory.actorId == -1 && memory.targetId == speaker && memory.type == SocialMemoryKind.GAIN_BY_LEAD; }
        return act == SocialAct.REACT && memory.type == SocialMemoryKind.SAW_KICK;
    }
    partial void PrepareMemoryTemplates(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act, string style, int intensity, List<SocialTemplate> result)
    {
        var mind = bot.Social.Psychology;
        if (mind.Memories.Count == 0) { return; }
        bool witnessedKick = false;
        foreach (var memory in mind.Memories)
        { if (memory.type == SocialMemoryKind.SAW_KICK && memory.eventId == facts.eventId) { witnessedKick = true; break; } }
        if (act == SocialAct.REACT && !witnessedKick) { return; }
        if (facts.notice != null && facts.notice != "KICK_WARNING" && !(act == SocialAct.REACT && witnessedKick)) { return; }
        var memories = new List<SocialMemory>();
        foreach (var memory in mind.Memories)
        {
            if (memory.type == SocialMemoryKind.SAW_KICK && memory.eventId == facts.eventId) { continue; }
            if (memory.usedCount < Config.Int("memoryMaxUses") && MemoryFits(memory, mind.SocialId, intent.targetId, act)) { memories.Add(memory); }
        }
        if (memories.Count == 0 || mind.SocialRng.Next() >= Config.Get("memoryChance")) { return; }
        MemoryEligibleAttempts++;
        memories.Sort((a, b) => { int order = b.salience.CompareTo(a.salience); return order != 0 ? order : b.at.CompareTo(a.at); });
        foreach (var memory in memories)
        {
            facts.memoryEventId = memory.eventId; facts.memoryKind = memory.type.ToString();
            facts.memSymbol = FindSocialStock(memory.symbol)?.stockName ?? memory.symbol;
            facts.memPct = memory.pct; facts.memAmount = memory.amount;
            double age = SocialTime.GameMinute - memory.at;
            facts.memWhen = age < 1440 ? "아까" : age < 2880 ? "어제" : age < 43200 ? "저번에" : "지난달에";
            var candidates = new List<SocialTemplate>();
            foreach (var template in ChatSocialAssets.Templates.templates)
            {
                if (template.act != act.ToString() || template.style != style || template.intensity != intensity || template.variant != "memory" ||
                    mind.RecentTemplateKeys.Contains(template.key) || !TemplateFactsMatch(template, facts)) { continue; }
                bool blocked = false;
                foreach (var recent in SocialRoom(intent.roomId).recent) { if (recent.templateKey == template.key) { blocked = true; break; } }
                foreach (Match match in SlotPattern.Matches(template.text))
                { if (string.IsNullOrEmpty(Slot(facts, match.Groups[1].Value, intent.targetId == 0 ? "" : "target"))) { blocked = true; } }
                if (!blocked) { candidates.Add(template); }
            }
            if (candidates.Count == 0) { continue; }
            result.Clear(); result.AddRange(candidates); return;
        }
        MemoryTemplateBlocked++; facts.memoryEventId = 0; facts.memoryKind = null;
    }
    partial void BeforeEmotionPublish(TradeBot bot, SocialIntent intent, SocialMessage message)
    {
        if (message.memory)
        {
            foreach (var memory in bot.Social.Psychology.Memories)
            {
                if (memory.eventId != message.facts.memoryEventId || memory.type.ToString() != message.facts.memoryKind) { continue; }
                memory.usedCount++;
                ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "MEMORY_MENTION", id = message.id, actor = bot.Social.Id, target = message.targetId, value = memory.usedCount, reason = memory.type + " #" + memory.eventId });
                break;
            }
        }
        SocialSpeechAction?.Invoke(bot, message);
    }
    private void RememberWitnessedResult(TradeBot observer, SocialMessage message)
    {
        if (!witnessedBrags.TryGetValue(observer.Social.Id, out var brags)) { brags = new Queue<SocialMessage>(); witnessedBrags[observer.Social.Id] = brags; }
        if (message.act == SocialAct.BRAG)
        {
            brags.Enqueue(message); while (brags.Count > Config.Int("rememberedBrags")) { brags.Dequeue(); }
        }
        else if (message.act == SocialAct.LAMENT && message.facts.losing && decimal.TryParse(message.facts.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal loss) && loss <= -(decimal)Config.Get("memorySelfAssetPct"))
        {
            foreach (var brag in brags)
            {
                if (brag.senderId != message.senderId || brag.facts.symbol != message.facts.symbol) { continue; }
                observer.Social.Psychology.Remember(new SocialMemory { type = SocialMemoryKind.SAW_BRAG_THEN_CRASH, actorId = observer.Social.Id, targetId = message.senderId,
                    eventId = message.id, symbol = message.facts.symbol, pct = message.facts.pct, amount = message.facts.amount, at = SocialTime.GameMinute, salience = Config.Int("memoryBragCrashSalience") });
                break;
            }
        }
    }
}

public sealed partial class InvestorPsychology
{
    [Serializable] public sealed class FomoLot { public string symbol; public decimal quantity, cost, realized; }
    public readonly Dictionary<string, FomoLot> FomoLots = new();
    public void ObserveFomoFill(SocialTradeFact fill)
    {
        if (fill.bought && HasFomo && FomoSymbol == fill.symbol)
        {
            if (!FomoLots.TryGetValue(fill.symbol, out var lot)) { lot = new FomoLot { symbol = fill.symbol }; FomoLots[fill.symbol] = lot; }
            lot.quantity += fill.quantity; lot.cost += fill.quantity * fill.price;
        }
        else if (!fill.bought && FomoLots.TryGetValue(fill.symbol, out var lot) && lot.quantity > 0)
        {
            decimal quantity = Math.Min(lot.quantity, fill.quantity), cost = lot.cost * quantity / lot.quantity;
            lot.quantity -= quantity; lot.cost -= cost; lot.realized += quantity * fill.price - cost;
            if (lot.quantity > 0) { return; }
            var c = ChatSocialConfig.Current;
            if (lot.realized != 0) { SocialDelta(lot.realized < 0 ? "fear" : "greed", c.Get(lot.realized < 0 ? "fomoLossFear" : "fomoGainGreed"), "fomo-trade-result", 0); }
            FomoLots.Remove(fill.symbol);
        }
    }
}
