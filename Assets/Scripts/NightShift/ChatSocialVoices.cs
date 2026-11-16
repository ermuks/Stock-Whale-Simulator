using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed partial class InvestorSociety
{
    private static readonly string[] VoiceKeys = { "brief", "measured", "chatty", "wry", "hesitant", "expressive" };
    private static readonly string[] VoiceLabels = { "짧고 담백함", "차분한 관찰형", "수다형", "능청형", "조심스러운 말투", "반응이 큰 말투" };
    public static bool IsVoiceKey(string key) => Array.IndexOf(VoiceKeys, key) >= 0;
    private static int VoiceIndex(InvestorPsychology mind)
    {
        // Existing saved identity/traits define a stable voice without new save fields or decision-RNG draws.
        var traits = mind.Traits;
        uint hash = unchecked((uint)mind.SocialId * 486187739u ^ (uint)(traits.Temper * 31 + traits.Grudge * 127 + traits.Sociability * 8191 + traits.Politeness * 131071));
        hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352du); hash ^= hash >> 15;
        return (int)(hash % (uint)VoiceKeys.Length);
    }
    public static string VoiceKey(InvestorPsychology mind) => VoiceKeys[VoiceIndex(mind)];
    public static string VoiceLabel(InvestorPsychology mind) => VoiceLabels[VoiceIndex(mind)];
    private static bool TryVoiceText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target,
        IEnumerable<SocialMessage> recent, out string text)
    {
        text = null;
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None ||
            message.promisedAction != SocialPromisedAction.None)
        { return false; }
        bool fomo = facts.notice == "FOMO" && message.replyToId == 0;
        if (!fomo && (!string.IsNullOrEmpty(facts.notice) || !template.key.Contains(".plain.")))
        { return false; }
        // Empathy and refusal after mediation must keep their specific parent context.
        if (parent != null && (message.act == SocialAct.AGREE && parent.act == SocialAct.LAMENT ||
            message.act == SocialAct.REBUT && parent.act == SocialAct.MEDIATE))
        { return false; }
        var random = CosmeticRandom(mind, message, 0x174c58edu);
        if (random.Next() >= ChatSocialConfig.Current.Get("voiceStyleChance"))
        { return false; }
        string voice = VoiceKey(mind), style = template.style == "formal" ? "formal" : "casual";
        string context = fomo ? "FOMO" : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Voice(message.act, context, voice, style);
        if (group == null && !fomo)
        {
            context = message.act == SocialAct.BRAG || message.act == SocialAct.LAMENT || message.act == SocialAct.ASK ||
                message.act == SocialAct.REACT || message.act == SocialAct.SMALLTALK || message.act == SocialAct.DOUBT ? "NONE" : "TARGET";
            group = ChatSocialAssets.Voice(message.act, context, voice, style);
        }
        if (group == null || !SpeechRequirementsMatch(group.requires, facts))
        { return false; }
        var choices = new List<int>();
        for (int i = 0; i < group.texts.Length; i++)
        {
            string key = group.key + "." + i;
            bool repeated = mind.RecentSpeechKeys.Contains(key);
            if (!repeated && recent != null)
            { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
            if (repeated) { continue; }
            bool complete = true;
            foreach (Match match in SlotPattern.Matches(group.texts[i]))
            { if (string.IsNullOrEmpty(SpeechSlot(facts, match.Groups[1].Value, target, 0))) { complete = false; break; } }
            if (complete) { choices.Add(i); }
        }
        if (choices.Count == 0)
        { return false; }
        int selected = choices[random.Range(0, choices.Count)];
        text = group.texts[selected]; message.debug.speechKey = group.key + "." + selected;
        return true;
    }
    internal static string VoiceStockParticles(string text, string name)
    {
        if (string.IsNullOrEmpty(name)) { return text; }
        int last = name[name.Length - 1] - 0xAC00;
        if (last < 0 || last > 11171) { return text; }
        int final = last % 28;
        if (final == 0)
        {
            text = text.Replace("{symbol}이네요", "{symbol}네요").Replace("{symbol}이 ", "{symbol}가 ")
                .Replace("{symbol}은", "{symbol}는").Replace("{symbol}을", "{symbol}를");
        }
        if (final == 0 || final == 8) { text = text.Replace("{symbol}으로", "{symbol}로"); }
        return text;
    }
}
