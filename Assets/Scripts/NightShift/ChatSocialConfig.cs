using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ChatSocialConfig
{
    public int schemaVersion = 1;
    public SocialWeight[] values;
    private Dictionary<string, double> index;
    private static ChatSocialConfig current;
    public static ChatSocialConfig Current
    {
        get
        {
            if (current == null)
            {
                var asset = Resources.Load<TextAsset>("Chat/chat_social_config");
                if (asset == null) { throw new InvalidOperationException("Missing chat_social_config"); }
                Install(JsonUtility.FromJson<ChatSocialConfig>(asset.text));
            }
            return current;
        }
    }
    public static void Install(ChatSocialConfig config)
    { config.Validate(); current = config; }
    public void Validate()
    {
        if (schemaVersion != 1 || values == null) { throw new ArgumentException("Invalid social config version"); }
        index = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value == null || string.IsNullOrEmpty(value.key) || double.IsNaN(value.value) || double.IsInfinity(value.value) || index.ContainsKey(value.key))
            { throw new ArgumentException("Invalid/duplicate social config key"); }
            index.Add(value.key, value.value);
        }
        foreach (string key in new[] { "hotWindowMinutes", "hotBaselineMinutes", "hotReturnDivisor", "replyExpiry", "cacheTrimSeconds", "cacheGraceSeconds", "logFlushSeconds", "watchMovePercent", "watchObservationMinMinutes", "watchObservationMaxMinutes", "watchMaximumGapMinutes", "watchReactionCooldownMinutes", "watchRetargetMinutes", "topicMemorySeconds", "topicContinueWeight", "topicNewWeight", "topicHesitationMinSec", "typingMinSec" })
        { if (Get(key) <= 0) { throw new ArgumentException("Expected positive social setting: " + key); } }
        foreach (string key in new[] { "suffixChance", "witnessProbability", "hotDwellFloor", "hotReentryScale", "seriousSpeechChance", "communityPlayfulChance", "communitySeriousChance", "colloquialNumberChance", "seriousColloquialNumberChance", "mediationExitBaseChance", "mediationExitMaxChance", "mediationTauntChance", "departureReactionChance", "voiceStyleChance", "topicHesitationChance" })
        { if (Get(key) < 0 || Get(key) > 1) { throw new ArgumentException("Expected probability: " + key); } }
        foreach (string key in new[] { "closedThreadCache", "logCapacity", "memoryCapacity", "roomRecentTemplates", "hotListCount", "topicPreferredCount", "typingPerRoom" })
        { if (Get(key) < 1 || Get(key) != Math.Floor(Get(key))) { throw new ArgumentException("Expected positive count: " + key); } }
        if (Get("hotBaselineMinutes") < Get("hotWindowMinutes") || Get("replyDelayMin") > Get("replyDelayMax") || Get("watchObservationMinMinutes") > Get("watchObservationMaxMinutes") || Get("watchMaximumGapMinutes") < Get("watchObservationMaxMinutes") ||
            Get("angerStage1") >= Get("angerStage2") || Get("angerStage2") >= Get("angerStage3") || Get("angerStage3") >= Get("angerStage4") || Get("angerStage4") > 100)
        { throw new ArgumentException("Invalid social window/threshold ordering"); }
        if (Get("mediationStressMin") < 0 || Get("mediationStressMin") >= 100 || Get("mediationExitBaseChance") > Get("mediationExitMaxChance") ||
            Get("mediationExitDelayMinSec") < 0 || Get("mediationExitDelayMaxSec") < Get("mediationExitDelayMinSec") || Get("mediationRejoinMinutes") <= 0 ||
            Get("mediationTauntBravadoMin") < 0 || Get("mediationTauntBravadoMin") > 100 || Get("mediationTauntAngerMin") < 0 || Get("mediationTauntAngerMin") > 100)
        { throw new ArgumentException("Invalid mediation reaction settings"); }
        if (Get("topicPreferredCount") > 32 || Get("topicNewWeight") > Get("topicContinueWeight") || Get("topicHotMultiplier") < 0 ||
            Get("topicHesitationMaxSec") < Get("topicHesitationMinSec") || Get("typingMaxSec") < Get("typingMinSec") || Get("typingPerRoom") > 16)
        { throw new ArgumentException("Invalid conversation focus/typing settings"); }
        foreach (string key in new[] { "riskBulletinSeconds", "riskSpeechGapSeconds", "riskPauseSeconds", "riskAttentionSeconds", "roomReferralMinutes" })
        { if (Get(key) <= 0) { throw new ArgumentException("Expected positive duration: " + key); } }
        foreach (string key in new[] { "riskWitnessChance", "riskSpeechChance", "riskBuyReduction", "riskPauseConcern" })
        { if (Get(key) < 0 || Get(key) > 1) { throw new ArgumentException("Expected risk probability/fraction: " + key); } }
        if (Get("riskFearPerGrade") < 0 || Get("riskFearPerGrade") > 100 || Get("riskVoicesPerNotice") < 1 || Get("riskVoicesPerNotice") > 4 || Get("riskVoicesPerNotice") != Math.Floor(Get("riskVoicesPerNotice")))
        { throw new ArgumentException("Invalid market risk response limits"); }
        if (Get("riskAttentionSeconds") < Get("riskPauseSeconds")) { throw new ArgumentException("Risk attention cannot end before the pause"); }
    }
    public double Get(string key)
    {
        if (index == null) { Validate(); }
        if (!index.TryGetValue(key, out double value)) { throw new ArgumentException("Missing social config: " + key); }
        return value;
    }
    public int Int(string key) => checked((int)Get(key));
}
[Serializable] public sealed class SocialActTable
{
    public string key;
    public SocialWeight[] weights;
}
[Serializable] public sealed class SocialActCatalog
{
    public int schemaVersion = 1;
    public SocialActTable[] motives, replies, effects;
}
[Serializable] public sealed class SocialTemplate
{
    public string key, act, style, variant, text;
    public int intensity;
    public string[] requires;
    public SocialPromisedAction promisedAction;
}
[Serializable] public sealed class SocialTemplateDocument
{
    public int schemaVersion = 1;
    public SocialTemplate[] templates;
    public SocialActTable[] suffixes;
    public SocialContextPhrases[] contexts;
    public SocialCommunityPhrases[] community;
    public SocialVoicePhrases[] voices;
}
[Serializable] public sealed class SocialContextPhrases
{
    public string key, style;
    public string[] texts;
}
[Serializable] public sealed class SocialCommunityPhrases
{
    public string key, act, context;
    public string[] requires, texts;
}
[Serializable] public sealed class SocialVoicePhrases
{
    public string key, act, context, voice, style;
    public string[] requires, texts;
}
