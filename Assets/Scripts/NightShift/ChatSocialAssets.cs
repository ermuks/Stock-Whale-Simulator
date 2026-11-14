using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One immutable catalog generation per load. Failed reload leaves the old generation intact.</summary>
public static class ChatSocialAssets
{
    public static SocialActCatalog Acts { get; private set; }
    public static SocialTemplateDocument Templates { get; private set; }
    public static int Generation { get; private set; }
    public static void EnsureLoaded()
    { if (Templates == null) { Reload(); } }
    public static void Reload()
    {
        var config = Read<ChatSocialConfig>("chat_social_config"); config.Validate();
        var acts = Read<SocialActCatalog>("chat_act_weights");
        var templates = Read<SocialTemplateDocument>("chat_templates");
        if (acts.schemaVersion != 1 || templates.schemaVersion != 1 || acts.motives == null || acts.replies == null || acts.effects == null || templates.templates == null)
        { throw new ArgumentException("Invalid social catalog schema"); }
        var keys = new HashSet<string>();
        foreach (var row in templates.templates)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key) || string.IsNullOrWhiteSpace(row.text) || !Enum.TryParse<SocialAct>(row.act, out _))
            { throw new ArgumentException("Invalid/duplicate social template"); }
        }
        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; Generation++;
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
    private float auditedGreed, auditedFear, auditedTrust, auditedBetrayal, auditedFomo;
    private void InitializeAudit()
    { auditedGreed = Greed; auditedFear = Fear; auditedTrust = Trust; auditedBetrayal = Betrayal; auditedFomo = Fomo; }
    private void AuditEmotion(string cause, long id = 0)
    {
        var log = ChatSocialAudit.Journal; double at = ChatSocialAudit.GameMinute;
        log.Change(at, SocialId, "greed", auditedGreed, Greed, cause, id);
        log.Change(at, SocialId, "fear", auditedFear, Fear, cause, id);
        log.Change(at, SocialId, "trust", auditedTrust, Trust, cause, id);
        log.Change(at, SocialId, "betrayal", auditedBetrayal, Betrayal, cause, id);
        log.Change(at, SocialId, "legacyFomo", auditedFomo, Fomo, cause, id);
        InitializeAudit();
    }
}
