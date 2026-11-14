using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class InvestorSocietySettings
{
    public int seed = 73129, botsPerTick = 24, recentMessages = 10, roomCapacity = 48;
    public float updateInterval = .25f, emotionDecay = .025f, witnessProbability = .35f;
    public float directGain = .9f, directLoss = 1.2f, rumorImpact = 1.4f, rumorAttenuation = .55f;
    [Min(0f)] public float commandObservationTrustMultiplier = 1f, commandProfitTrustMultiplier = 2f;
    public float joinProbability = .08f, leaveProbability = .12f, commandWait = 240f;
    public float idleRoomLeaveProbability = .06f, roomGreetingProbability = .25f, roomMessageInterval = 4f;
    public float speechCooldown = 35f, contactCooldown = 20f, repeatContactCooldown = 90f;
    [Range(.001f, .1f)] public float commandCashFraction = .025f;
    public int maxOrderShares = 200;
    public bool trace;
}

public readonly struct InvestorExperience
{
    public readonly long EventId, CommandId;
    public readonly int Room;
    public readonly string Symbol, Reason;
    public readonly decimal Cost, Proceeds, ReturnPercent, AssetBefore;
    public readonly float Time;
    public InvestorExperience(long id, long command, int room, string symbol, decimal cost, decimal proceeds, float time, string reason = "체결", decimal assetBefore = 0m)
    {
        EventId = id; CommandId = command; Room = room; Symbol = symbol; Reason = reason;
        Cost = cost; Proceeds = proceeds; ReturnPercent = cost > 0m ? (proceeds / cost - 1m) * 100m : 0m; Time = time; AssetBefore = assetBefore;
    }
}

public readonly struct InvestorPerception
{
    public readonly int Author;
    public readonly long EventId;
    public readonly float Sentiment, Credibility;
    public InvestorPerception(int author, long id, float sentiment, float credibility)
    { Author = author; EventId = id; Sentiment = sentiment; Credibility = credibility; }
}

/// <summary>지시 성공을 관찰한 기억. 본인의 체결/수익 원장에는 넣지 않는다.</summary>
public readonly struct InvestorForecastObservation
{
    public readonly long EventId, CommandId;
    public readonly int Room;
    public readonly string Symbol;
    public readonly decimal Buy, Sell, ReturnPercent;
    public readonly float Time;
    public InvestorForecastObservation(long eventId, long commandId, int room, string symbol, decimal buy, decimal sell, float time)
    {
        EventId = eventId; CommandId = commandId; Room = room; Symbol = symbol;
        Buy = buy; Sell = sell; Time = time; ReturnPercent = buy > 0m ? (sell / buy - 1m) * 100m : 0m;
    }
}

/// <summary>Bounded exact deduplication. Evicted old IDs remain rejected by a monotonic watermark.</summary>
public sealed class InvestorEventMemory
{
    private readonly HashSet<long> ids = new();
    private readonly Queue<long> order = new();
    private long expiredThrough;
    public bool Add(long id)
    {
        if (id <= expiredThrough || !ids.Add(id))
        { return false; }
        order.Enqueue(id);
        if (order.Count > 128)
        { long old = order.Dequeue(); ids.Remove(old); expiredThrough = Math.Max(expiredThrough, old); }
        return true;
    }
}

/// <summary>개인의 감정과 장기 경험. 시세/계좌/UI에 의존하지 않아 재현 가능한 검증이 가능하다.</summary>
public sealed partial class InvestorPsychology
{
    private float rawGreed, rawFear;
    public float Greed { get => (float)Math.Round(rawGreed); private set => rawGreed = Mathf.Clamp(value, 0f, 100f); }
    public float Fear { get => (float)Math.Round(rawFear); private set => rawFear = Mathf.Clamp(value, 0f, 100f); }
    public float Trust { get; private set; }
    public float Betrayal { get; private set; }
    public readonly float BaselineGreed, Credulity, Activity, ActivityOffset;
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public float BetrayalMemory { get; private set; }
    public string LastCause { get; private set; } = "초기 상태";
    public int Room;
    public readonly long[] LastRead = new long[InvestorSociety.MaximumRoomId + 1];
    public float UpdatedAt, NextSpeech, NextMembership, LastContact = -10000f;
    public float JoinedRoomAt, LastRoomWitnessAt = -10000f;
    public BotChatMessage LastRoomWitness;
    public int LastWitnessRoom;
    public long LastSharedExperience;
    public long LastSharedObservation;
    public readonly List<InvestorExperience> Experiences = new(64);
    public readonly List<InvestorForecastObservation> Observations = new(32);
    public readonly List<InvestorPerception> Perceptions = new(32);
    private readonly InvestorEventMemory directEvents = new(), witnessed = new();
    private readonly InvestorEventMemory observedCommands = new();
    public InvestorExperience? Latest => Experiences.Count == 0 ? null : Experiences[Experiences.Count - 1];
    public InvestorForecastObservation? LatestObservation => Observations.Count == 0 ? null : Observations[Observations.Count - 1];
    public InvestorPsychology(int id, int seed)
    {
        var random = new System.Random(unchecked(seed + id * 7919));
        Greed = BaselineGreed = 50f + (float)random.NextDouble() * 30f;
        Credulity = .15f + (float)random.NextDouble() * .7f;
        Activity = .15f + (float)random.NextDouble() * .8f;
        ActivityOffset = (float)random.NextDouble() * 180f;
        InitializeSocial(id, seed);
    }
    public bool ActiveAt(float time) => (time + ActivityOffset) % 180f < 45f + Activity * 120f;
    public bool Record(InvestorExperience experience, InvestorSocietySettings settings)
    {
        if (experience.Cost <= 0m || !directEvents.Add(experience.EventId))
        { return false; }
        witnessed.Add(experience.EventId);
        RefreshQuadrant(); bool ambivalent = Quadrant == InvestorQuadrant.Ambivalent;
        float previousTrust = Trust, previousBetrayal = Betrayal;
        float roi = (float)Math.Max(-100m, Math.Min(200m, experience.ReturnPercent));
        if (roi > 0f)
        {
            Trust += experience.CommandId > 0 ? (float)experience.ReturnPercent * Mathf.Max(0f, settings.commandProfitTrustMultiplier) : Mathf.Min(35f, roi * settings.directGain * (1f + Greed / 200f + Fear / 150f));
            Greed += Mathf.Min(6f, roi * .15f); Fear -= Mathf.Min(10f, roi * .4f); Wins++;
        }
        else if (roi < 0f)
        {
            float impact = Mathf.Min(45f, -roi * settings.directLoss * (1f + Mathf.Min(5, Losses) * .15f + FormulaTrust / 200f));
            Betrayal += impact; Fear += Mathf.Min(35f, -roi * 1.3f);
            BetrayalMemory = Mathf.Clamp(BetrayalMemory + impact * .55f, 0f, 100f); Losses++;
        }
        // Trust and betrayal are independent: a bad experience never erases past trust.
        if (ambivalent && experience.CommandId > 0)
        {
            var c = ChatSocialConfig.Current;
            if (roi > 0)
            {
                Trust = previousTrust + (Trust - previousTrust) * (float)c.Get("ambivalentGainTrustScale");
                Betrayal = Mathf.Max(0, Betrayal - roi * (float)c.Get("ambivalentProfitBetrayalRelief"));
            }
            else if (roi < 0) { Betrayal = previousBetrayal + (Betrayal - previousBetrayal) * (float)c.Get("ambivalentLossScale"); }
        }
        RememberInvestment(experience, experience.CommandId > 0);
        if (Experiences.Count == 64) { Experiences.RemoveAt(0); }
        Experiences.Add(experience);
        LastCause = $"직접 {experience.Reason} #{experience.EventId}: {experience.Symbol} {experience.ReturnPercent:+0.00;-0.00;0}%";
        Clamp(); AuditEmotion("direct-experience", experience.EventId); return true;
    }
    public bool ObserveForecast(InvestorForecastObservation observation, InvestorSocietySettings settings)
    {
        if (observation.Buy <= 0m || observation.ReturnPercent <= 0m || !observedCommands.Add(observation.CommandId))
        { return false; }
        witnessed.Add(observation.EventId);
        float gain = (float)observation.ReturnPercent * Mathf.Max(0f, settings.commandObservationTrustMultiplier);
        Trust += gain; ObserveMissedGain((float)observation.ReturnPercent);
        if (Observations.Count == 32) { Observations.RemoveAt(0); }
        Observations.Add(observation);
        LastCause = $"거부한 지시 #{observation.CommandId} 목표 도달 목격: {observation.Symbol}, 신뢰 +{gain:0.00} (미체결)";
        Clamp(); AuditEmotion("forecast-observation", observation.EventId); return true;
    }
    public void Decay(float seconds, InvestorSocietySettings settings)
    {
        float amount = Mathf.Max(0f, seconds) * Mathf.Max(0f, settings.emotionDecay);
        Trust = Mathf.Max(0f, Trust - amount * .35f);
        Betrayal = Mathf.Max(0f, Betrayal - amount * .7f * (1.5f - Traits.Grudge / 100f));
        rawFear = Mathf.Max(0f, rawFear - amount * 1.4f);
        rawGreed = Mathf.MoveTowards(rawGreed, BaselineGreed, amount * .2f);
        AuditEmotion("decay");
    }
    public void RecallBetrayal()
    {
        if (BetrayalMemory <= 0f)
        { return; }
        Betrayal += Mathf.Min(28f, 4f + BetrayalMemory * .35f);
        LastCause = "재회유: 이전 손실 기억 활성화"; Clamp(); AuditEmotion("betrayal-recall");
    }
    public bool Witness(long eventId, float sentiment, float credibility, int hops, InvestorSocietySettings settings)
    {
        if (sentiment == 0f || hops > 2 || credibility <= 0f || !witnessed.Add(eventId))
        { return false; }
        float ownOpposition = sentiment > 0f ? BetrayalMemory / 100f : Mathf.Min(1f, Wins * .15f);
        float impact = settings.rumorImpact * Mathf.Clamp01(credibility) * Credulity * Mathf.Pow(settings.rumorAttenuation, hops) * (1f - ownOpposition * .8f);
        if (sentiment > 0f) { Trust += impact; }
        else { Trust -= impact; Fear += impact * .8f; }
        LastCause = $"목격 #{eventId}: {(sentiment > 0f ? "긍정" : "부정")} 소문 {impact:0.00}";
        Clamp(); AuditEmotion("legacy-witness", eventId); return true;
    }
    public float JoinChance => Mathf.Clamp01(.05f + Greed * .0015f + FormulaTrust * .005f + (HasFomo ? .1f : 0) - FormulaBetrayal * .005f);
    public float LeaveChance => Quadrant == InvestorQuadrant.Hostile ? FormulaBetrayal / (float)ChatSocialConfig.Current.Get("hostileLeaveDivisor") : 0;
    public void ObserveRisk(float returnPercent)
    {
        if (returnPercent < -2f) { Fear = Mathf.Max(Fear, Mathf.Min(85f, -returnPercent * (1.2f + Credulity))); }
        AuditEmotion("position-risk");
    }
    public void ObserveMissedGain(float returnPercent)
    { /* FOMO requires a witnessed symbol and is evaluated by the society, never a numeric accumulator. */ }
    public void RememberSpeaker(int author, long id, float sentiment, float credibility)
    {
        if (Perceptions.Count == 32) { Perceptions.RemoveAt(0); }
        Perceptions.Add(new InvestorPerception(author, id, sentiment, credibility));
    }
    private void Clamp()
    {
        Greed = Mathf.Clamp(Greed, 0f, 100f); Fear = Mathf.Clamp(Fear, 0f, 100f);
        // Relationship intensity can exceed 100. Decision probabilities remain independently bounded to 0..1.
        Trust = Mathf.Max(0f, Trust); Betrayal = Mathf.Max(0f, Betrayal); RefreshQuadrant();
    }
}

public readonly struct InvestorMessageContext
{
    public readonly long EventId, ReplyTo;
    public readonly int TargetId, Room, Hops;
    public readonly float Sentiment, Credibility, AuthorTrust;
    public readonly bool DirectExperience;
    public InvestorMessageContext(long eventId, int room, float sentiment, float credibility, float trust, bool direct, long replyTo = 0, int hops = 0, int target = -1)
    {
        EventId = eventId; Room = room; Sentiment = Mathf.Clamp(sentiment, -1f, 1f);
        Credibility = Mathf.Clamp01(credibility); AuthorTrust = trust; DirectExperience = direct; ReplyTo = replyTo; Hops = hops; TargetId = target;
    }
}
