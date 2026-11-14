using System;
using System.Collections.Generic;

public sealed partial class InvestorSociety
{
    public readonly Dictionary<SocialAct, long> TemplateMissesByAct = new();
    private void RecordTemplateMiss(SocialAct act)
    {
        TemplateMissesByAct.TryGetValue(act, out long previous); TemplateMissesByAct[act] = previous + 1;
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "TEMPLATE_BLOCKED", reason = act.ToString(), value = previous + 1 });
    }
    public static string ContextualSocialText(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        // Only generic base phrases change. Actual-trade, memory, FOMO and moderation notices retain their guards.
        if (!template.key.Contains(".plain.") || template.promisedAction != SocialPromisedAction.None || ChatSocialAssets.Templates.contexts == null)
        { return template.text; }
        string source = parent != null ? parent.act.ToString() : message.targetId == 0 ? "NONE" : null;
        if (source == null) { return template.text; }
        string key = source + ">" + message.act;
        foreach (var group in ChatSocialAssets.Templates.contexts)
        {
            if (group.key != key || group.style != template.style) { continue; }
            int split = template.key.LastIndexOf('.');
            if (!int.TryParse(template.key.Substring(split + 1), out int index) || index < 1) { return template.text; }
            string text = group.texts[(index - 1) % group.texts.Length];
            foreach (System.Text.RegularExpressions.Match slot in SlotPattern.Matches(text))
            { if (string.IsNullOrEmpty(Slot(message.facts, slot.Groups[1].Value, "target"))) { return template.text; } }
            message.debug.contextKey = key + "." + template.style + "." + index;
            return text;
        }
        return template.text;
    }
    // Cosmetic randomness is independent: editing punctuation cannot change a reply, order or exit roll.
    public static string DecorateSocialText(InvestorPsychology mind, SocialMessage message, string text)
    {
        var config = ChatSocialConfig.Current;
        if (mind.Traits.Politeness >= 67 || ChatSocialAssets.Templates.suffixes == null)
        { return text; }
        double anger = message.targetId > 0 && mind.Relations.TryGetValue(message.targetId, out var relation) ? relation.anger : 0;
        string key = Attack(message.act) && anger >= config.Get("suffixEmotionMin") ? "anger" :
            message.act == SocialAct.LAMENT && mind.Fear >= config.Get("suffixEmotionMin") ? "fear" :
            message.act == SocialAct.BRAG && mind.Greed >= config.Get("suffixEmotionMin") ? "greed" : null;
        if (key == null)
        { return text; }
        uint seed = unchecked((uint)message.id ^ (uint)(message.id >> 32) ^ (uint)mind.SocialId * 486187739u);
        seed ^= seed >> 16; seed = unchecked(seed * 0x7feb352du); seed ^= seed >> 15; seed = unchecked(seed * 0x846ca68bu); seed ^= seed >> 16;
        var random = new SocialRandom(unchecked((int)seed));
        if (random.Next() >= config.Get("suffixChance"))
        { return text; }
        foreach (var group in ChatSocialAssets.Templates.suffixes)
        {
            if (group.key != key) { continue; }
            double total = 0; foreach (var row in group.weights) { total += row.value; }
            double choice = random.Next() * total;
            foreach (var row in group.weights)
            {
                choice -= row.value;
                if (choice <= 0 && row.value > 0) { return text + " " + row.key; }
            }
        }
        return text;
    }
}
