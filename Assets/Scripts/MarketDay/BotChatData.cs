using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChatEvent
{
    PriceRise,
    PriceFall,
    HoldingProfit,
    HoldingLoss,
    BreakEvenRecovered,
    ProfitRealized,
    LossRealized,
    PriceRiseAfterSell,
    BuyConsideration,
    MarketOpen,
    MarketClose,
    DailyProfit,
    DailyLoss,
    StressRelief,
    RiskChanged,
    SmallTalk,
    Agreement
}

public enum BotPersonality
{
    Any,
    Balanced,
    Momentum,
    Patient,
    Cautious
}

public enum BotTone
{
    Casual,
    Formal,
    Brief
}

public enum HoldingCondition
{
    Any,
    Holding,
    NotHolding
}

public enum ChatRequirement
{
    UnrealizedProfitPositive,
    UnrealizedProfitNegative,
    CrossedUnrealizedLossToNonNegative,
    RecentProfitableSell,
    RecentLosingSell,
    RecentSellThenPriceRise,
    DailyReturnPositive,
    DailyReturnNegative,
    RecentStressDrop
}

[Serializable]
public sealed class BotChatTemplateCatalog
{
    [Serializable]
    public sealed class Document
    {
        public int schemaVersion;
        public string language;
        public Entry[] templates;
    }

    [Serializable]
    public sealed class Entry
    {
        public string id, personality, tone, holdingCondition, text;
        public string @event;
        public string[] requirements;
        public float minStress, maxStress, weight;
        [NonSerialized]
        public ChatEvent eventKind;
        [NonSerialized]
        public BotPersonality personalityKind;
        [NonSerialized]
        public BotTone toneKind;
        [NonSerialized]
        public HoldingCondition holdingKind;
        [NonSerialized]
        public ChatRequirement[] facts;
    }

    private readonly List<Entry>[] byEvent = new List<Entry>[Enum.GetValues(typeof(ChatEvent)).Length];
    public int Count { get; private set; }
    public int Rejected { get; private set; }

    public BotChatTemplateCatalog(string json, Action<string> report)
    {
        for (int i = 0; i < byEvent.Length; i++)
        {
            byEvent[i] = new List<Entry>();
        }

        Document doc;
        try
        {
            doc = JsonUtility.FromJson<Document>(json);
        }
        catch (Exception ex)
        {
            report?.Invoke("Chat JSON: " + ex.Message);
            return;
        }

        if (doc == null || doc.schemaVersion != 1 || doc.templates == null)
        {
            report?.Invoke("Chat JSON: unsupported schema or missing templates");
            return;
        }

        var ids = new HashSet<string>();
        foreach (var entry in doc.templates)
        {
            bool valid = entry != null && !string.IsNullOrWhiteSpace(entry.id) && ids.Add(entry.id);
            if (entry == null)
            {
                Rejected++;
                continue;
            }

            valid &= Parse(entry.@event, out entry.eventKind) && Parse(entry.personality, out entry.personalityKind) && Parse(entry.tone, out entry.toneKind) && Parse(entry.holdingCondition, out entry.holdingKind);
            valid &= !float.IsNaN(entry.weight) && !float.IsInfinity(entry.weight) && entry.weight > 0f && entry.minStress >= 0f && entry.maxStress <= 100f && entry.minStress <= entry.maxStress;
            valid &= !string.IsNullOrWhiteSpace(entry.text);
            string tokens = (entry.text ?? "").Replace("{stockName}", "");
            valid &= tokens.IndexOf('{') < 0 && tokens.IndexOf('}') < 0;
            entry.facts = new ChatRequirement[entry.requirements == null ? 0 : entry.requirements.Length];
            for (int i = 0; i < entry.facts.Length; i++)
            {
                valid &= Parse(entry.requirements[i], out entry.facts[i]);
            }

            if (!valid)
            {
                Rejected++;
                report?.Invoke("Chat JSON: rejected template " + entry.id);
                continue;
            }

            byEvent[(int)entry.eventKind].Add(entry);
            Count++;
        }
    }

    private static bool Parse<T>(string text, out T value)
        where T : struct
    { return Enum.TryParse(text, false, out value) && Enum.IsDefined(typeof(T), value) && !int.TryParse(text, out _); }

    public IReadOnlyList<Entry> ForEvent(ChatEvent kind)
    { return byEvent[(int)kind]; }
}

public sealed class BotSocialState
{
    public readonly int Id;
    public readonly string Nickname, Profile;
    public readonly BotPersonality Personality;
    public readonly BotTone Tone = BotTone.Casual;
    public readonly float Risk, Horizon, Chase, Sensitivity, Recovery;
    public readonly float Impulsiveness, Articulation, Confidence;
    public readonly InvestorPsychology Psychology;
    public float Greed => Psychology.Greed / 100f;
    public float Trust => Psychology.Trust / 100f;
    public float Suspicion = .2f, LastPersuasion = -10000f;
    public Stock InfluencedStock;
    public float InfluenceUntil;
    public int Utterances;
    public readonly string Role;
    public float Stress, LastSpeech = -10000f, LastDropTime = -10000f, LastDropAmount;
    public Stock LastDropStock;
    public float LastSampleTime = -10000f;
    public double LastTradingSeconds;
    public readonly Dictionary<Stock, PositionMemory> Positions = new();
    public readonly float[] LastEvents = new float[Enum.GetValues(typeof(ChatEvent)).Length];
    public readonly Queue<(string id, float time)> RecentSpeech = new(16);
    public BotSocialState(int id, int seed, string type)
    {
        Id = id;
        var rng = new System.Random(unchecked(seed + id * 7919));
        Nickname = "익명" + id.ToString("D4");
        Personality = type.Contains("Fomo") || type.Contains("Firefly") ? BotPersonality.Momentum : type.Contains("Holder") ? BotPersonality.Patient : type.Contains("Panic") ? BotPersonality.Cautious : BotPersonality.Balanced;
        Risk = (float)rng.NextDouble();
        Horizon = (float)rng.NextDouble();
        Chase = Personality == BotPersonality.Momentum ? 0.9f : (float)rng.NextDouble();
        Sensitivity = 0.7f + (float)rng.NextDouble() * 0.6f;
        Recovery = 0.7f + (float)rng.NextDouble() * 0.6f;
        Psychology = new InvestorPsychology(id, seed);
        Impulsiveness = (float)rng.NextDouble();
        Articulation = (float)rng.NextDouble();
        Confidence = .2f + .75f * (float)rng.NextDouble();
        Suspicion = .15f + .55f * (float)rng.NextDouble();
        Role = type.Contains("Whale") ? "고래" : type.Contains("Smart") || type.Contains("Book") ? "기관" : "개인";
        string[] names = { "새벽개미", "종가메모", "관망수달", "느린호가", "파란노트", "커피두잔", "야간정찰", "먼지상자", "잔고지킴", "우산너구리" };
        Nickname = names[rng.Next(names.Length)] + id.ToString("D3");
        Profile = Role + " · " + (Articulation < .3f ? "짧게 말함" : Articulation > .7f ? "분석적으로 말함" : "대화형");
        for (int i = 0; i < LastEvents.Length; i++)
        {
            LastEvents[i] = -10000f;
        }
    }

    public sealed class PositionMemory
    {
        public bool Observed, WasLoss;
        public decimal LastPrice, LastReturn, SellPrice, SellVolume, SellProfit, PendingProfit, PendingBasis;
        public float SellTime = -10000f, CrossTime = -10000f, LastRealizedResponse = -10000f;
        public double LossSeconds, AcquiredTradingSeconds;
    }
}

public readonly struct BotChatMessage
{
    public readonly long Sequence;
    public readonly int BotId, Day, Minute;
    public readonly string Nickname, Profile, Text, TemplateId;
    public readonly float Stress;
    public readonly ChatEvent Event;
    public readonly InvestorMessageContext Context;
    public readonly SocialMessage Social;
    public BotChatMessage(long sequence, TradeBot bot, int day, int minute, string text, string templateId, ChatEvent kind, InvestorMessageContext context = default, SocialMessage social = null)
    {
        Social = social;
        Context = context.EventId > 0 ? context : new InvestorMessageContext(0, 0, 0f, bot == null ? 1f : bot.Social.Confidence, bot == null ? 0f : bot.Social.Psychology.Trust, false, target: 0);
        Sequence = sequence;
        BotId = bot == null ? 0 : bot.Social.Id;
        Day = day;
        Minute = minute;
        Nickname = bot == null ? "시장 안내" : bot.Social.Nickname;
        Profile = bot == null ? "공개 종목 상태 안내" : bot.Social.Profile;
        Text = text;
        TemplateId = templateId;
        Event = kind;
        Stress = bot == null ? 0f : bot.Social.Stress;
    }
}
