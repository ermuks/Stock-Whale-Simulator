using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One immutable catalog generation per load. Failed reload leaves the old generation intact.</summary>
public static class ChatSocialAssets
{
    public static SocialActCatalog Acts { get; private set; }
    public static SocialTemplateDocument Templates { get; private set; }
    public static int Generation { get; private set; }
    private static Dictionary<(string act, string style, int intensity, string variant), List<SocialTemplate>> templateIndex;
    public static IReadOnlyList<SocialTemplate> Candidates(SocialAct act, string style, int intensity, string variant)
    {
        EnsureLoaded();
        return templateIndex.TryGetValue((act.ToString(), style, intensity, variant), out var rows) ? rows : Array.Empty<SocialTemplate>();
    }
    public static void EnsureLoaded()
    { if (Templates == null) { Reload(); } }
    public static void Reload()
    {
        var config = Read<ChatSocialConfig>("chat_social_config"); config.Validate();
        var acts = Read<SocialActCatalog>("chat_act_weights");
        var templates = Read<SocialTemplateDocument>("chat_templates");
        Install(config, acts, templates);
    }
    public static void Install(ChatSocialConfig config, SocialActCatalog acts, SocialTemplateDocument templates)
    {
        config.Validate();
        if (acts.schemaVersion != 1 || templates.schemaVersion != 1 || acts.motives == null || acts.replies == null || acts.effects == null || templates.templates == null)
        { throw new ArgumentException("Invalid social catalog schema"); }
        var keys = new HashSet<string>();
        var nextIndex = new Dictionary<(string, string, int, string), List<SocialTemplate>>();
        foreach (var row in templates.templates)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key) || string.IsNullOrWhiteSpace(row.text) || !Enum.TryParse<SocialAct>(row.act, out _) ||
                row.style != "formal" && row.style != "casual" && row.style != "rough" || row.intensity < 1 || row.intensity > 3 || row.variant != "plain" && row.variant != "memory" ||
                !Enum.IsDefined(typeof(SocialPromisedAction), row.promisedAction))
            { throw new ArgumentException("Invalid/duplicate social template"); }
            var key = (row.act, row.style, row.intensity, row.variant);
            if (!nextIndex.TryGetValue(key, out var bucket)) { bucket = new List<SocialTemplate>(); nextIndex.Add(key, bucket); }
            bucket.Add(row);
        }
        ValidateWeights(acts.motives, false); ValidateWeights(acts.replies, false); ValidateWeights(acts.effects, true);
        if (templates.suffixes != null) { ValidateWeights(templates.suffixes, false); }
        if (templates.contexts != null)
        {
            var contexts = new HashSet<string>();
            foreach (var context in templates.contexts)
            {
                if (context == null || string.IsNullOrEmpty(context.key) || !contexts.Add(context.key + "." + context.style) || context.texts == null || context.texts.Length < 6 ||
                    context.style != "formal" && context.style != "casual" && context.style != "rough") { throw new ArgumentException("Invalid social context phrase group"); }
                foreach (string text in context.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty social context phrase"); } }
            }
        }
        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; templateIndex = nextIndex; Generation++;
    }
    private static void ValidateWeights(SocialActTable[] tables, bool signed)
    {
        var groups = new HashSet<string>();
        foreach (var table in tables)
        {
            if (table == null || string.IsNullOrEmpty(table.key) || !groups.Add(table.key) || table.weights == null) { throw new ArgumentException("Invalid social weight table"); }
            var keys = new HashSet<string>();
            foreach (var weight in table.weights)
            { if (weight == null || string.IsNullOrEmpty(weight.key) || !keys.Add(weight.key) || double.IsNaN(weight.value) || double.IsInfinity(weight.value) || !signed && weight.value < 0) { throw new ArgumentException("Invalid social weight"); } }
        }
    }
    private static T Read<T>(string key)
    {
        var asset = Resources.Load<TextAsset>("Chat/" + key);
        if (asset == null) { throw new ArgumentException("Missing " + key); }
        return JsonUtility.FromJson<T>(asset.text);
    }
}

public static class ChatSocialAudit
{
    private static SocialJournal journal;
    public static double GameMinute;
    public static SocialJournal Journal => journal ??= new SocialJournal(ChatSocialConfig.Current.Int("logCapacity"));
    public static void Begin(SocialJournal value) { journal = value; }
}

public sealed partial class InvestorPsychology
{
    private float auditedGreed, auditedFear, auditedTrust, auditedBetrayal;
    private void InitializeAudit()
    { auditedGreed = Greed; auditedFear = Fear; auditedTrust = Trust; auditedBetrayal = Betrayal; }
    private void AuditEmotion(string cause, long id = 0)
    {
        var log = ChatSocialAudit.Journal; double at = ChatSocialAudit.GameMinute;
        log.Change(at, SocialId, "greed", auditedGreed, Greed, cause, id);
        log.Change(at, SocialId, "fear", auditedFear, Fear, cause, id);
        log.Change(at, SocialId, "trust", auditedTrust, Trust, cause, id);
        log.Change(at, SocialId, "betrayal", auditedBetrayal, Betrayal, cause, id);
        InitializeAudit();
    }
}
