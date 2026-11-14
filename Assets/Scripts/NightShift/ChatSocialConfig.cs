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
        foreach (string key in new[] { "hotWindowMinutes", "hotBaselineMinutes", "hotReturnDivisor", "replyExpiry", "cacheTrimSeconds", "cacheGraceSeconds", "logFlushSeconds" })
        { if (Get(key) <= 0) { throw new ArgumentException("Expected positive social setting: " + key); } }
        foreach (string key in new[] { "suffixChance", "witnessProbability", "hotDwellFloor", "hotReentryScale" })
        { if (Get(key) < 0 || Get(key) > 1) { throw new ArgumentException("Expected probability: " + key); } }
        foreach (string key in new[] { "closedThreadCache", "logCapacity", "memoryCapacity", "roomRecentTemplates", "hotListCount" })
        { if (Get(key) < 1 || Get(key) != Math.Floor(Get(key))) { throw new ArgumentException("Expected positive count: " + key); } }
        if (Get("hotBaselineMinutes") < Get("hotWindowMinutes") || Get("replyDelayMin") > Get("replyDelayMax") ||
            Get("angerStage1") >= Get("angerStage2") || Get("angerStage2") >= Get("angerStage3") || Get("angerStage3") >= Get("angerStage4") || Get("angerStage4") > 100)
        { throw new ArgumentException("Invalid social window/threshold ordering"); }
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
}
[Serializable] public sealed class SocialContextPhrases
{
    public string key, style;
    public string[] texts;
}
