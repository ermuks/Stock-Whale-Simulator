using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum SocialAct { BRAG, LAMENT, ASK, ANSWER, AGREE, REBUT, DOUBT, MOCK, INSULT, COMFORT, APOLOGIZE, MEDIATE, PRAISE_PLAYER, WARN_PLAYER, CALL_PLAYER, REACT, SMALLTALK, FAREWELL }
public enum SocialMotive { REPLY_OBLIGATION, EMOTION_THRESHOLD, SELF_EVENT, WITNESSED_EVENT, IDLE }
public enum SocialThreadKind { CHAT, QUESTION, ARGUMENT }
public enum SocialEnd { NONE, MAX_TURNS, NO_REPLY, RESOLVED, EXIT, PLAYER_ACTION, INTERRUPTED }
public enum SocialMemoryKind { LOSS_BY_LEAD, GAIN_BY_LEAD, KICKED, FOUGHT, SAW_KICK, SAW_BRAG_THEN_CRASH, BIG_SELF_LOSS, BIG_SELF_GAIN, ROOM_FEE }
public enum InvestorQuadrant { Indifferent, Loyal, Ambivalent, Hostile }
public static class ChatSocialIds
{
    private static long last;
    public static long Next() => ++last;
    internal static void Reset() { last = 0; }
}

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
    [SerializeField] private int speechVersion;
    [SerializeField] private bool serious;
    public int Temper => temper;
    public int Grudge => grudge;
    public int Suggestibility => suggestibility;
    public int Sociability => sociability;
    public int Bravado => bravado;
    public int Politeness => politeness;
    public bool Serious => serious;
    public string SpeechLabel => serious ? "진지함" : "진지하지 않음";
    public bool Valid => temper >= 0 && temper <= 100 && grudge >= 0 && grudge <= 100 && suggestibility >= 0 && suggestibility <= 100 &&
        sociability >= 0 && sociability <= 100 && bravado >= 0 && bravado <= 100 && politeness >= 0 && politeness <= 100 && speechVersion >= 0 && speechVersion <= 1;
    public SocialTraits(SocialRandom random, ChatSocialConfig config)
    {
        temper = Draw("temper"); grudge = Draw("grudge"); suggestibility = Draw("suggestibility");
        sociability = Draw("sociability"); bravado = Draw("bravado"); politeness = Draw("politeness");
        // A separate stream keeps new voice traits from shifting decisions or seeded friendships.
        var voiceRandom = new SocialRandom(unchecked((int)random.State ^ 0x5931a7bd));
        serious = voiceRandom.Next() < config.Get("seriousSpeechChance"); speechVersion = 1;
        int Draw(string name) => Math.Max(0, Math.Min(100, random.Normal(config.Get(name + "Mean"), config.Get(name + "Sd"))));
    }
    public void MigrateSpeech(SocialTraits generated)
    {
        if (speechVersion != 0)
        { return; }
        serious = generated.serious; speechVersion = 1;
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
    public string symbol, stockName, amount, pct, avgPrice, price, quantity, profitKind, memSymbol, memPct, memWhen;
    public long eventId;
    public bool bought, sold, held, profitable, losing, directLead;
    public bool canPromise;
    public bool dailyProfit, dailyLoss, recovered, recentSellProfit, recentSellLoss, sellThenRise, stressRelief, signal;
    /// <summary>notable: 말할 만한 크기의 평가·실현 손익인가. saleNotable: 방금 한 매도가 말할 만한가. parentNews: 답하는 상대의 말에 소식·기사가 있었는가.</summary>
    public bool notable, saleNotable, parentNews;
    public string bidStatus, askStatus, signalResult;
    public string sourceEvent;
    public string notice, departedName;
    public int riskVersion, riskGrade, riskObserver;
    public long companyNewsId;
    public string newsHeadline;
    public int newsObserver;
    public double companySentiment;
    public bool newsCorrection;
    public string observationPrice, observedPrice, observationPercent;
    public int observationBotId;
    public double observationAt, observedAt;
    public string memoryKind, memAmount;
    public long memoryEventId;
    public double at;
}
[Serializable] public sealed class SocialWeight { public string key; public double value; }
[Serializable] public sealed class SocialMessageDebug
{
    public string trigger;
    public string contextKey;
    public string speechDisposition, speechKey, numberStyle, speechVoice;
    public long causeId;
    public SocialWeight[] weights;
    public SocialFacts slots;
}
[Serializable] public sealed class SocialMessage
{
    /// <summary>손익을 말한 메시지에 붙는 인증 카드. 없으면 null.</summary>
    public ChatProfitCard profitCard;
    public long id, replyToId, threadId;
    public int roomId, senderId, targetId;
    public double at, realAt;
    public SocialAct act;
    public SocialFacts facts;
    public string topicEvent, templateKey, text;
    public bool isLie, memory;
    public SocialPromisedAction promisedAction;
    public SocialMessageDebug debug;
    public string localizedPhrase, localizedSuffix, localizedTarget;
    public SocialSpeechValue[] localizedValues;
}
[Serializable] public sealed class SocialSpeechValue
{
    public string name, value, raw;
    public bool approximate;
}
public sealed class SocialIntent
{
    public bool topicHesitated;
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
    public bool calledPlayer, moderated;
    public string symbol;
    public decimal initialPrice;
    public bool Active => endReason == SocialEnd.NONE;
}
[Serializable] public sealed class SocialRoomState
{
    public bool firstMemberInvited;
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
    public long id, threadId;
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
    public readonly Queue<string> RecentSpeechKeys = new();
    public readonly Dictionary<int, double> RejoinAt = new(), MutedUntil = new();
    public double FomoUntil, FomoCooldownUntil, SocialLastSpeech = double.NegativeInfinity;
    public long ActiveThread;
    public readonly Dictionary<int, long> SocialLastRead = new();
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
        { if (memory.eventId != 0 && previous.eventId == memory.eventId && previous.type == memory.type && previous.targetId == memory.targetId && previous.actorId == memory.actorId) { return; } }
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
        public string fomoSymbol;
        public float chartSkill;
        public int chartLessons;
        public string panicSymbol;
        public double panicUntil, panicCooldownUntil;
        public InvestorQuadrant quadrant;
        public uint randomState;
        public SocialTraits traits;
        public SocialRelation[] relations;
        public SocialMemory[] memories;
        public FomoLotSnapshot[] fomoLots;
        public RoomCooldownSnapshot[] rejoin, mutes;
        public RoomCooldownSnapshot[] knownPlayerRooms;
        public int invitedPlayerRooms;
        public CompanyOpinionBook.Opinion[] companies;
        public double lastQuadrantReview = -1, lastFomoObservation = -1, lastRelationshipSpeech = -1;
        public SocialPromisedAction promisedAction;
        public string promisedSymbol;
        public long promiseMessage;
    }
    [Serializable] public sealed class FomoLotSnapshot { public string symbol, quantity, cost, realized; }
    [Serializable] public sealed class RoomCooldownSnapshot { public int room; public double until; }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static SocialRelation CopyRelation(SocialRelation value) => new() { to = value.to, anger = Math.Round(Math.Max(0, Math.Min(100, value.anger))),
        affinity = Math.Round(Math.Max(-100, Math.Min(100, value.affinity))), lastInteractionAt = value.lastInteractionAt, lastWarningAt = value.lastWarningAt, highestStage = value.highestStage };
    private static SocialMemory CopyMemory(SocialMemory value) => new() { type = value.type, actorId = value.actorId, targetId = value.targetId, salience = value.salience,
        usedCount = value.usedCount, eventId = value.eventId, symbol = value.symbol, amount = value.amount, pct = value.pct, at = value.at };
    private static RoomCooldownSnapshot[] SaveCooldowns(Dictionary<int, double> source)
    {
        var list = new List<RoomCooldownSnapshot>();
        foreach (var row in source) { list.Add(new RoomCooldownSnapshot { room = row.Key, until = row.Value }); }
        list.Sort((a, b) => a.room.CompareTo(b.room)); return list.ToArray();
    }
    public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(); foreach (var row in Relations.Values) { relations.Add(CopyRelation(row)); }
        relations.Sort((a, b) => a.to.CompareTo(b.to));
        var memories = new List<SocialMemory>(); foreach (var row in Memories) { memories.Add(CopyMemory(row)); }
        var lots = new List<FomoLotSnapshot>();
        foreach (var row in FomoLots.Values) { lots.Add(new FomoLotSnapshot { symbol = row.symbol, quantity = row.quantity.ToString(CultureInfo.InvariantCulture), cost = row.cost.ToString(CultureInfo.InvariantCulture), realized = row.realized.ToString(CultureInfo.InvariantCulture) }); }
        lots.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            chartSkill = ChartSkill, chartLessons = ChartLessons, panicSymbol = PanicSymbol, panicUntil = PanicUntil, panicCooldownUntil = PanicCooldownUntil,
            fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
            knownPlayerRooms = SaveCooldowns(KnownPlayerRooms), invitedPlayerRooms = InvitedPlayerRooms, companies = Companies.Capture(),
            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
            lastQuadrantReview = LastQuadrantReview, lastFomoObservation = LastFomoObservation, lastRelationshipSpeech = LastRelationshipSpeech };
    }
    public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (saved.invitedPlayerRooms < 0 || saved.invitedPlayerRooms >= 1 << InvestorSociety.PlayerRoomCount) { return false; }
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { if (row == null || row.room < 1 || row.room > InvestorSociety.PlayerRoomCount || !Finite(row.until)) { return false; } } }
        if (!CompanyOpinionBook.Valid(saved.companies)) { return false; }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        RestoreChartSkill(saved.chartSkill, saved.chartLessons);
        PanicSymbol = string.IsNullOrEmpty(saved.panicSymbol) ? null : saved.panicSymbol;
        PanicUntil = Finite(saved.panicUntil) ? Math.Max(0, saved.panicUntil) : 0;
        PanicCooldownUntil = Finite(saved.panicCooldownUntil) ? Math.Max(0, saved.panicCooldownUntil) : 0;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        if (Room > InvestorSociety.AvailablePlayerRoomCount && Room <= InvestorSociety.PlayerRoomCount) { Room = 0; }
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        KnownPlayerRooms.Clear(); InvitedPlayerRooms = saved.invitedPlayerRooms;
        Companies.Restore(saved.companies);
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { KnownPlayerRooms[row.room] = row.until; } }
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
}
