using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class ChatSocialDiagnostics
{
    public static string Message(SocialMessage message)
    {
        if (message == null) { return "No social message selected."; }
        var text = new StringBuilder();
        text.AppendLine($"Message {message.id} | room {message.roomId} | thread {message.threadId}");
        text.AppendLine($"{message.senderId} -> {message.targetId} | reply {message.replyToId}");
        text.AppendLine(message.text); text.AppendLine($"Act: {message.act} | template: {message.templateKey}");
        text.AppendLine($"Trigger: {message.debug?.trigger} | cause: {message.debug?.causeId}");
        text.AppendLine($"Context phrase: {message.debug?.contextKey ?? "base"}");
        text.AppendLine($"Speech: {message.debug?.speechDisposition} | phrase: {message.debug?.speechKey ?? "base"} | numbers: {message.debug?.numberStyle}");
        text.AppendLine($"Voice: {message.debug?.speechVoice}");
        if (message.debug?.weights != null)
        { foreach (var weight in message.debug.weights) { text.AppendLine($"  {weight.key}: {weight.value:F4}"); } }
        text.AppendLine("Facts at publication:"); text.AppendLine(JsonUtility.ToJson(message.debug?.slots ?? message.facts, true));
        return text.ToString();
    }
    public static string Bot(TradeBot bot)
    {
        if (bot?.Social == null) { return "No investor selected."; }
        var mind = bot.Social.Psychology; var traits = mind.Traits; var text = new StringBuilder();
        text.AppendLine($"{bot.Social.Nickname} #{mind.SocialId} | room {mind.Room}");
        text.AppendLine($"Temper {traits.Temper} | Grudge {traits.Grudge} | Suggestibility {traits.Suggestibility}");
        text.AppendLine($"Sociability {traits.Sociability} | Bravado {traits.Bravado} | Politeness {traits.Politeness}");
        text.AppendLine($"말투 성향: {traits.SpeechLabel}");
        text.AppendLine($"표현 습관: {InvestorSociety.VoiceLabel(mind)}");
        text.AppendLine($"Greed {mind.Greed:F0} | Fear {mind.Fear:F0} | Trust {mind.Trust:F0} | Betrayal {mind.Betrayal:F0}");
        text.AppendLine($"Quadrant {mind.Quadrant} | FOMO {(mind.HasFomo ? mind.FomoSymbol + " until " + mind.FomoUntil.ToString("F1") : "off")}");
        text.AppendLine($"Formula trust/betrayal: {mind.FormulaTrust:F0}/{mind.FormulaBetrayal:F0}");
        var relations = new List<SocialRelation>(mind.Relations.Values);
        relations.Sort((a, b) => { int order = b.anger.CompareTo(a.anger); return order != 0 ? order : a.to.CompareTo(b.to); });
        text.AppendLine("Top 5 anger:");
        for (int i = 0; i < Math.Min(5, relations.Count); i++) { Append(relations[i]); }
        relations.Sort((a, b) => { int order = Math.Abs(b.affinity).CompareTo(Math.Abs(a.affinity)); return order != 0 ? order : a.to.CompareTo(b.to); });
        text.AppendLine("Top 5 affinity magnitude:");
        for (int i = 0; i < Math.Min(5, relations.Count); i++) { Append(relations[i]); }
        text.AppendLine("Salient memories:");
        foreach (var memory in mind.Memories)
        { text.AppendLine($"  {memory.type} event={memory.eventId} target={memory.targetId} {memory.symbol} {memory.pct}% at={memory.at:F1} uses={memory.usedCount}"); }
        return text.ToString();
        void Append(SocialRelation relation) { text.AppendLine($"  #{relation.to} anger={relation.anger:F0} affinity={relation.affinity:F0} stage={InvestorPsychology.Stage(relation.anger)}"); }
    }
}
