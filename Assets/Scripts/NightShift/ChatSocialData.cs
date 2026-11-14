using System;
using System.Collections.Generic;
using UnityEngine;

public enum SocialAct { BRAG, LAMENT, ASK, ANSWER, AGREE, REBUT, DOUBT, MOCK, INSULT, COMFORT, APOLOGIZE, MEDIATE, PRAISE_PLAYER, WARN_PLAYER, CALL_PLAYER, REACT, SMALLTALK, FAREWELL }
public enum SocialMotive { REPLY_OBLIGATION, EMOTION_THRESHOLD, SELF_EVENT, WITNESSED_EVENT, IDLE }
public enum SocialThreadKind { CHAT, QUESTION, ARGUMENT }
public enum SocialEnd { NONE, MAX_TURNS, NO_REPLY, RESOLVED, EXIT, PLAYER_ACTION, INTERRUPTED }
public enum SocialMemoryKind { LOSS_BY_LEAD, GAIN_BY_LEAD, KICKED, FOUGHT, SAW_KICK, SAW_BRAG_THEN_CRASH, BIG_SELF_LOSS, BIG_SELF_GAIN }
public enum InvestorQuadrant { Indifferent, Loyal, Ambivalent, Hostile }

/// <summary>Separate stream: adding social choices must never consume the trading RNG.</summary>
public sealed class SocialRandom
{
    public uint State { get; private set; }
    public SocialRandom(int seed) { State = unchecked((uint)seed) | 1u; }
    public double Next()
    { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x; return x / 4294967296d; }
    public int Range(int min, int exclusiveMax) => min + (int)(Next() * (exclusiveMax - min));
    public int Normal(double mean, double deviation)
    { return (int)Math.Round(mean + deviation * Math.Sqrt(-2 * Math.Log(Math.Max(double.Epsilon, Next()))) * Math.Cos(2 * Math.PI * Next())); }
    public void Restore(uint state) { State = state == 0 ? 1u : state; }
}

[Serializable]
public sealed class SocialTraits
{
    [SerializeField] private int temper, grudge, suggestibility, sociability, bravado, politeness;
    public int Temper => temper;
    public int Grudge => grudge;
    public int Suggestibility => suggestibility;
    public int Sociability => sociability;
    public int Bravado => bravado;
    public int Politeness => politeness;
    public SocialTraits(SocialRandom random, ChatSocialConfig config)
    {
        temper = Draw("temper"); grudge = Draw("grudge"); suggestibility = Draw("suggestibility");
        sociability = Draw("sociability"); bravado = Draw("bravado"); politeness = Draw("politeness");
        int Draw(string name) => Math.Max(0, Math.Min(100, random.Normal(config.Get(name + "Mean"), config.Get(name + "Sd"))));
    }
}

[Serializable] public sealed class SocialRelation
{
    public int to;
    public double affinity, anger, lastInteractionAt, lastWarningAt = -1e9;
    public int highestStage;
}
[Serializable] public sealed class SocialMemory
{
    public SocialMemoryKind type;
    public int actorId, targetId, salience, usedCount;
    public long eventId;
    public string symbol, amount, pct;
    public double at;
}
[Serializable] public sealed class SocialFacts
{
    public string symbol, stockName, amount, pct, avgPrice, price, quantity, memSymbol, memPct, memWhen;
    public long eventId;
    public bool bought, sold, held, profitable, losing, directLead;
    public double at;
}
[Serializable] public sealed class SocialWeight { public string key; public double value; }
[Serializable] public sealed class SocialMessageDebug
{
    public string trigger;
    public SocialWeight[] weights;
    public SocialFacts slots;
}
[Serializable] public sealed class SocialMessage
{
    public long id, replyToId, threadId;
    public int roomId, senderId, targetId;
    public double at, realAt;
    public SocialAct act;
    public SocialFacts facts;
    public string topicEvent, templateKey, text;
    public bool isLie, memory;
    public SocialMessageDebug debug;
}
public sealed class SocialIntent
{
    public SocialMotive motive;
    public SocialAct? forcedAct;
    public int targetId, roomId;
    public long replyToId, threadId, causeId;
    public double due, expires = double.PositiveInfinity;
    public SocialFacts facts;
}
[Serializable] public sealed class SocialThread
{
    public long id, parentMessage;
    public int roomId, turnCount, maxTurns, attacks;
    public SocialThreadKind kind;
    public SocialEnd endReason;
    public List<int> participants = new();
    public double lastAt, firstStage3At = -1;
    public bool calledPlayer;
    public bool Active => endReason == SocialEnd.NONE;
}
[Serializable] public sealed class SocialRoomState
{
    public double discipline, chill, updatedGameMinute;
    public readonly List<SocialThread> threads = new();
    public readonly Queue<SocialMessage> recent = new();
    public readonly Queue<double> sent = new();
    public readonly Queue<double> kicks = new();
    public readonly HashSet<int> banned = new();
}
[Serializable] public sealed class SocialChange
{
    public double time, before, after;
    public int target, other;
    public string field, causeType;
    public long causeId;
}
[Serializable] public sealed class SocialEvent
{
    public double time;
    public string kind, reason;
    public long id;
    public int room, actor, target;
    public double value;
}
/// <summary>Release builds keep the same audit stream as development builds.</summary>
public sealed class SocialJournal
{
    public readonly Queue<SocialChange> Changes = new();
    public readonly Queue<SocialEvent> Events = new();
    public event Action<SocialChange> Changed;
    public event Action<SocialEvent> Happened;
    private readonly int capacity;
    public SocialJournal(int capacity) { this.capacity = Math.Max(1, capacity); }
    public void Change(double time, int target, string field, double before, double after, string cause, long id, int other = 0)
    {
        if (before == after)
        { return; }
        var record = new SocialChange { time = time, target = target, other = other, field = field, before = before, after = after, causeType = cause, causeId = id };
        if (Changes.Count >= capacity) { Changes.Dequeue(); }
        Changes.Enqueue(record); Changed?.Invoke(record);
    }
    public void Event(SocialEvent record)
    {
        if (Events.Count >= capacity) { Events.Dequeue(); }
        Events.Enqueue(record); Happened?.Invoke(record);
    }
}

/// <summary>Clock adapter accepts the existing day/minute and scaled real time; it never changes timeScale.</summary>
public sealed class SocialClock
{
    public double GameMinute { get; private set; }
    public double Seconds { get; private set; }
    public void Set(int day, double minute, double scaledSeconds)
    { GameMinute = (day - 1) * 1440d + minute; Seconds = scaledSeconds; }
}

public sealed partial class InvestorPsychology
{
    public int SocialId { get; private set; }
    public SocialTraits Traits { get; private set; }
    public readonly Dictionary<int, SocialRelation> Relations = new();
    public readonly List<SocialMemory> Memories = new();
    public readonly List<SocialIntent> Intentions = new();
    public readonly Queue<string> RecentTemplateKeys = new();
    public readonly Dictionary<int, double> RejoinAt = new(), MutedUntil = new();
    public double FomoUntil, FomoCooldownUntil, SocialLastSpeech = double.NegativeInfinity;
    public long ActiveThread;
    public SocialRandom SocialRng { get; private set; }
    public InvestorQuadrant Quadrant { get; private set; }
    public float FormulaTrust => Mathf.Min(Trust, 100f);
    public float FormulaBetrayal => Mathf.Min(Betrayal, 100f);
    private void InitializeSocial(int id, int seed)
    {
        SocialId = id; SocialRng = new SocialRandom(unchecked(seed ^ id * 104729 ^ 0x353091));
        Traits = new SocialTraits(SocialRng, ChatSocialConfig.Current);
        InitializeAudit();
    }
    public SocialRelation Relation(int other)
    {
        if (!Relations.TryGetValue(other, out var relation))
        { relation = new SocialRelation { to = other }; Relations.Add(other, relation); }
        return relation;
    }
    public void SeedFriends(IReadOnlyList<int> sortedIds)
    {
        if (Relations.Count > 0 || sortedIds.Count < 2)
        { return; }
        var candidates = new List<int>();
        foreach (int id in sortedIds) { if (id != SocialId) { candidates.Add(id); } }
        var config = ChatSocialConfig.Current;
        int count = Math.Min(candidates.Count, SocialRng.Range(config.Int("friendsMin"), config.Int("friendsMax") + 1));
        for (int i = 0; i < count; i++)
        {
            int index = SocialRng.Range(0, candidates.Count), target = candidates[index]; candidates.RemoveAt(index);
            Relation(target).affinity = SocialRng.Range(config.Int("friendAffinityMin"), config.Int("friendAffinityMax") + 1);
        }
    }
    public void Remember(SocialMemory memory)
    {
        if (memory == null)
        { return; }
        foreach (var previous in Memories)
        { if (memory.eventId != 0 && previous.eventId == memory.eventId && previous.type == memory.type) { return; } }
        Memories.Add(memory);
        if (Memories.Count <= ChatSocialConfig.Current.Int("memoryCapacity"))
        { return; }
        int index = 0;
        for (int i = 1; i < Memories.Count; i++)
        {
            if (Memories[i].salience < Memories[index].salience || Memories[i].salience == Memories[index].salience && Memories[i].at < Memories[index].at)
            { index = i; }
        }
        Memories.RemoveAt(index);
    }
    [Serializable] public sealed class SocialSnapshot
    {
        public int version = 2, id, seed, room;
        public float greed, fear, trust, betrayal, fomo;
        public double fomoUntil, fomoCooldownUntil;
        public uint randomState;
        public SocialTraits traits;
        public SocialRelation[] relations;
        public SocialMemory[] memories;
    }
    public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(Relations.Values); relations.Sort((a, b) => a.to.CompareTo(b.to));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            fomo = Fomo, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = Memories.ToArray() };
    }
    public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            float.IsNaN(saved.trust) || float.IsInfinity(saved.trust) || float.IsNaN(saved.betrayal) || float.IsInfinity(saved.betrayal))
        { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal; Fomo = 0;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        if (saved.traits != null) { Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                row.anger = Math.Max(0, Math.Min(100, row.anger)); row.affinity = Math.Max(-100, Math.Min(100, row.affinity));
                Relations[row.to] = row;
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { Remember(memory); } }
        Clamp(); AuditEmotion("restore"); return true;
    }
}
