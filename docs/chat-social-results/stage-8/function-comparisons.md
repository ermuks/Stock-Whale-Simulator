# 8단계 함수 단위 원본·수정본 비교

7단계 커밋 대비 전체 함수 문맥입니다. `-`는 원본, `+`는 수정본이며 실행용 패치가 아닌 검토용 기록입니다.

```diff
diff --git a/Assets/Scripts/NightShift/ChatSocialAssets.cs b/Assets/Scripts/NightShift/ChatSocialAssets.cs
index 19e793f..f93d284 100644
--- a/Assets/Scripts/NightShift/ChatSocialAssets.cs
+++ b/Assets/Scripts/NightShift/ChatSocialAssets.cs
@@ -5,30 +5,70 @@ using UnityEngine;
 /// <summary>One immutable catalog generation per load. Failed reload leaves the old generation intact.</summary>
 public static class ChatSocialAssets
 {
     public static SocialActCatalog Acts { get; private set; }
     public static SocialTemplateDocument Templates { get; private set; }
     public static int Generation { get; private set; }
+    private static Dictionary<(string act, string style, int intensity, string variant), List<SocialTemplate>> templateIndex;
+    public static IReadOnlyList<SocialTemplate> Candidates(SocialAct act, string style, int intensity, string variant)
+    {
+        EnsureLoaded();
+        return templateIndex.TryGetValue((act.ToString(), style, intensity, variant), out var rows) ? rows : Array.Empty<SocialTemplate>();
+    }
     public static void EnsureLoaded()
     { if (Templates == null) { Reload(); } }
     public static void Reload()
     {
         var config = Read<ChatSocialConfig>("chat_social_config"); config.Validate();
         var acts = Read<SocialActCatalog>("chat_act_weights");
         var templates = Read<SocialTemplateDocument>("chat_templates");
+        Install(config, acts, templates);
+    }
+    public static void Install(ChatSocialConfig config, SocialActCatalog acts, SocialTemplateDocument templates)
+    {
+        config.Validate();
         if (acts.schemaVersion != 1 || templates.schemaVersion != 1 || acts.motives == null || acts.replies == null || acts.effects == null || templates.templates == null)
         { throw new ArgumentException("Invalid social catalog schema"); }
         var keys = new HashSet<string>();
+        var nextIndex = new Dictionary<(string, string, int, string), List<SocialTemplate>>();
         foreach (var row in templates.templates)
         {
-            if (row == null || string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key) || string.IsNullOrWhiteSpace(row.text) || !Enum.TryParse<SocialAct>(row.act, out _))
+            if (row == null || string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key) || string.IsNullOrWhiteSpace(row.text) || !Enum.TryParse<SocialAct>(row.act, out _) ||
+                row.style != "formal" && row.style != "casual" && row.style != "rough" || row.intensity < 1 || row.intensity > 3 || row.variant != "plain" && row.variant != "memory" ||
+                !Enum.IsDefined(typeof(SocialPromisedAction), row.promisedAction))
             { throw new ArgumentException("Invalid/duplicate social template"); }
+            var key = (row.act, row.style, row.intensity, row.variant);
+            if (!nextIndex.TryGetValue(key, out var bucket)) { bucket = new List<SocialTemplate>(); nextIndex.Add(key, bucket); }
+            bucket.Add(row);
+        }
+        ValidateWeights(acts.motives, false); ValidateWeights(acts.replies, false); ValidateWeights(acts.effects, true);
+        if (templates.suffixes != null) { ValidateWeights(templates.suffixes, false); }
+        if (templates.contexts != null)
+        {
+            var contexts = new HashSet<string>();
+            foreach (var context in templates.contexts)
+            {
+                if (context == null || string.IsNullOrEmpty(context.key) || !contexts.Add(context.key + "." + context.style) || context.texts == null || context.texts.Length < 6 ||
+                    context.style != "formal" && context.style != "casual" && context.style != "rough") { throw new ArgumentException("Invalid social context phrase group"); }
+                foreach (string text in context.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty social context phrase"); } }
+            }
+        }
+        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; templateIndex = nextIndex; Generation++;
+    }
+    private static void ValidateWeights(SocialActTable[] tables, bool signed)
+    {
+        var groups = new HashSet<string>();
+        foreach (var table in tables)
+        {
+            if (table == null || string.IsNullOrEmpty(table.key) || !groups.Add(table.key) || table.weights == null) { throw new ArgumentException("Invalid social weight table"); }
+            var keys = new HashSet<string>();
+            foreach (var weight in table.weights)
+            { if (weight == null || string.IsNullOrEmpty(weight.key) || !keys.Add(weight.key) || double.IsNaN(weight.value) || double.IsInfinity(weight.value) || !signed && weight.value < 0) { throw new ArgumentException("Invalid social weight"); } }
         }
-        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; Generation++;
     }
     private static T Read<T>(string key)
     {
         var asset = Resources.Load<TextAsset>("Chat/" + key);
         if (asset == null) { throw new ArgumentException("Missing " + key); }
         return JsonUtility.FromJson<T>(asset.text);
     }
 }
diff --git a/Assets/Scripts/NightShift/ChatSocialData.cs b/Assets/Scripts/NightShift/ChatSocialData.cs
index 4c7ca19..631ac17 100644
--- a/Assets/Scripts/NightShift/ChatSocialData.cs
+++ b/Assets/Scripts/NightShift/ChatSocialData.cs
@@ -32,67 +32,70 @@ public sealed class SocialRandom
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
     public bool Valid => temper >= 0 && temper <= 100 && grudge >= 0 && grudge <= 100 && suggestibility >= 0 && suggestibility <= 100 &&
         sociability >= 0 && sociability <= 100 && bravado >= 0 && bravado <= 100 && politeness >= 0 && politeness <= 100;
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
     public string symbol, stockName, amount, pct, avgPrice, price, quantity, profitKind, memSymbol, memPct, memWhen;
     public long eventId;
     public bool bought, sold, held, profitable, losing, directLead;
+    public bool canPromise;
     public bool dailyProfit, dailyLoss, recovered, recentSellProfit, recentSellLoss, sellThenRise, stressRelief, signal;
     public string bidStatus, askStatus, signalResult;
     public string sourceEvent;
     public string notice, departedName;
     public string memoryKind, memAmount;
     public long memoryEventId;
     public double at;
 }
 [Serializable] public sealed class SocialWeight { public string key; public double value; }
 [Serializable] public sealed class SocialMessageDebug
 {
     public string trigger;
+    public string contextKey;
     public long causeId;
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
+    public SocialPromisedAction promisedAction;
     public SocialMessageDebug debug;
 }
 public sealed class SocialIntent
@@ -177,146 +180,152 @@ public sealed class SocialClock
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
         public InvestorQuadrant quadrant;
         public uint randomState;
         public SocialTraits traits;
         public SocialRelation[] relations;
         public SocialMemory[] memories;
         public FomoLotSnapshot[] fomoLots;
         public RoomCooldownSnapshot[] rejoin, mutes;
         public double lastQuadrantReview = -1, lastFomoObservation = -1, lastRelationshipSpeech = -1;
+        public SocialPromisedAction promisedAction;
+        public string promisedSymbol;
+        public long promiseMessage;
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
             fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
             traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
+            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
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
+        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
         Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
+        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
         Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
         Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
         FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
         FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
         LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
         FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
         RejoinAt.Clear(); MutedUntil.Clear();
         if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
         if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
         if (saved.traits != null) { Traits = saved.traits; }
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
diff --git a/Assets/Scripts/NightShift/ChatSocialPipeline.cs b/Assets/Scripts/NightShift/ChatSocialPipeline.cs
index f87a95b..2385ed4 100644
--- a/Assets/Scripts/NightShift/ChatSocialPipeline.cs
+++ b/Assets/Scripts/NightShift/ChatSocialPipeline.cs
@@ -6,373 +6,381 @@ using System.Text.RegularExpressions;
 /// <summary>The existing society owns the scheduler; this partial isolates deterministic conversation work from Unity frames.</summary>
 public sealed partial class InvestorSociety
 {
     public bool SocialReady => socialBots.Count > 0;
     public readonly SocialClock SocialTime = new();
     public readonly SocialRoomState PublicSocial = new();
     public readonly Dictionary<int, TradeBot> SocialBots = new();
     private readonly List<TradeBot> socialBots = new();
     private readonly Dictionary<int, double> nextIdle = new();
     private readonly Dictionary<int, SocialAct> lastAct = new();
     private readonly Dictionary<int, int> consecutiveAct = new();
     private readonly Dictionary<long, SocialMessage> socialMessageIndex = new();
     private readonly Queue<long> socialMessageOrder = new();
     private readonly List<(TradeBot bot, SocialIntent intent)> readySocialIntents = new();
     private int socialCursor, knownBotCount = -1;
     public long SocialAttempts { get; private set; }
     public long TemplateBlocked { get; private set; }
     public long SocialPublished { get; private set; }
     public event Action<SocialMessage> SocialMessagePublished;
     public Func<TradeBot, Stock> ChooseSocialTopic;
     public Func<TradeBot, SocialFacts, SocialFacts> RefreshOfferedFacts;
     private static readonly CultureInfo SocialCulture = CultureInfo.InvariantCulture;
     private static readonly Regex SlotPattern = new("\\{([A-Za-z]+)\\}", RegexOptions.Compiled);
     private ChatSocialConfig Config => ChatSocialConfig.Current;
     public void BindSocialBots(IReadOnlyList<TradeBot> bots)
     {
         ChatSocialAssets.EnsureLoaded();
         if (knownBotCount == bots.Count)
         { return; }
         knownBotCount = bots.Count; socialBots.Clear(); SocialBots.Clear();
         foreach (var bot in bots)
         {
             if (bot == null || bot.Social == null || bot.IsExchangeParticipant)
             { continue; }
             socialBots.Add(bot); SocialBots[bot.Social.Id] = bot;
         }
         socialBots.Sort((a, b) => a.Social.Id.CompareTo(b.Social.Id));
         var ids = new List<int>(); foreach (var bot in socialBots) { ids.Add(bot.Social.Id); }
         foreach (var bot in socialBots) { bot.Social.Psychology.SeedFriends(ids); }
     }
     public SocialRoomState SocialRoom(int id) => id == 0 ? PublicSocial : GetRoom(id)?.Social;
     public bool Present(TradeBot bot, int room)
     {
         if (bot == null || !bot.isActiveAndEnabled || bot.IsExchangeParticipant || room != 0 && bot.Social.Psychology.Room != room)
         { return false; }
         var mind = bot.Social.Psychology;
         return SocialRoom(room) != null && !SocialRoom(room).banned.Contains(bot.Social.Id) &&
             (!mind.MutedUntil.TryGetValue(room, out double until) || SocialTime.GameMinute >= until);
     }
     public void SocialAdvance(int dayNumber, double gameMinute, double scaledSeconds, int budget)
     {
         SocialTime.Set(dayNumber, gameMinute, scaledSeconds); ChatSocialAudit.GameMinute = SocialTime.GameMinute;
         if (!SocialReady)
         { return; }
         BeforeSocialTick();
         readySocialIntents.Clear();
         int count = Math.Min(Math.Max(1, budget), socialBots.Count);
         for (int i = 0; i < count; i++)
         {
             if (socialCursor >= socialBots.Count) { socialCursor = 0; }
             var bot = socialBots[socialCursor++]; var mind = bot.Social.Psychology;
             BeforeSocialBot(bot);
             if (!bot.isActiveAndEnabled)
             { continue; }
             double now = SocialTime.Seconds;
             for (int j = mind.Intentions.Count - 1; j >= 0; j--)
             {
                 if (mind.Intentions[j].expires < now || !Present(bot, mind.Intentions[j].roomId))
                 { ExpireSocialIntent(bot, mind.Intentions[j]); mind.Intentions.RemoveAt(j); }
             }
             if (!nextIdle.TryGetValue(mind.SocialId, out double idleAt) || now >= idleAt)
             {
                 nextIdle[mind.SocialId] = now + Config.Get("idleInterval");
                 double chill = SocialRoom(mind.Room).chill;
                 double probability = Config.Get("idleChance") * mind.Traits.Sociability / 50d * Math.Max(Config.Get("chillSpeechFloor"), 1 - chill / Config.Get("chillSpeechDivisor"));
                 if (Present(bot, mind.Room) && mind.Intentions.Count == 0 && mind.ActiveAt((float)now) && mind.SocialRng.Next() < probability)
                 { Offer(bot, mind.Room, SocialMotive.IDLE); }
             }
             int selected = -1;
             for (int j = 0; j < mind.Intentions.Count; j++)
             {
                 var intent = mind.Intentions[j];
                 double cooldown = Config.Get("speakCooldownSec") * (intent.motive == SocialMotive.REPLY_OBLIGATION ? Config.Get("replyCooldownScale") : 1);
                 if (intent.due > now || now - mind.SocialLastSpeech < cooldown)
                 { continue; }
                 if (selected < 0 || intent.motive < mind.Intentions[selected].motive)
                 { selected = j; }
             }
             if (selected < 0)
             { continue; }
             var chosen = mind.Intentions[selected]; mind.Intentions.RemoveAt(selected);
             readySocialIntents.Add((bot, chosen));
         }
         // A busy early bot must not consume the room quota before another bot's due reply.
         readySocialIntents.Sort((a, b) =>
         {
             int order = a.intent.motive.CompareTo(b.intent.motive);
             if (order == 0) { order = a.intent.due.CompareTo(b.intent.due); }
             if (order == 0) { order = a.bot.Social.Psychology.SocialLastSpeech.CompareTo(b.bot.Social.Psychology.SocialLastSpeech); }
             return order != 0 ? order : a.bot.Social.Id.CompareTo(b.bot.Social.Id);
         });
         foreach (var ready in readySocialIntents)
         {
             var bot = ready.bot; var mind = bot.Social.Psychology; var chosen = ready.intent; double now = SocialTime.Seconds;
             var room = SocialRoom(chosen.roomId);
             while (room.sent.Count > 0 && now - room.sent.Peek() >= Config.Get("roomWindowSec")) { room.sent.Dequeue(); }
             if (room.sent.Count >= Config.Int("roomMaxMsgPer10Sec"))
             {
                 // Pending high priority work is retained; idle overflow is discarded, never burst-published later.
                 if (chosen.motive < SocialMotive.WITNESSED_EVENT) { chosen.due = room.sent.Peek() + Config.Get("roomWindowSec"); mind.Intentions.Add(chosen); }
                 continue;
             }
             GenerateSocialMessage(bot, chosen);
         }
     }
     public void Offer(TradeBot bot, int room, SocialMotive motive, SocialFacts facts = null, SocialAct? forced = null, int target = 0, long cause = 0)
     {
         if (bot?.Social == null || SocialRoom(room) == null)
         { return; }
         var mind = bot.Social.Psychology;
         foreach (var queued in mind.Intentions)
         { if (queued.motive == motive && queued.causeId == cause && queued.roomId == room && queued.forcedAct == forced) { return; } }
         string expiry = motive == SocialMotive.REPLY_OBLIGATION ? "replyExpiry" : motive == SocialMotive.EMOTION_THRESHOLD ? "emotionExpiry" : motive == SocialMotive.SELF_EVENT ? "selfExpiry" : "witnessExpiry";
         mind.Intentions.Add(new SocialIntent { roomId = room, motive = motive, facts = facts, forcedAct = forced, targetId = target, causeId = cause,
             due = SocialTime.Seconds, expires = motive == SocialMotive.IDLE ? double.PositiveInfinity : SocialTime.Seconds + Config.Get(expiry) });
     }
     public void OfferMarketEvent(ChatEvent kind, Stock stock, TradeBot bot)
     {
         if (!SocialReady)
         { return; }
         if (bot == null)
         {
             var chooser = socialBots[socialCursor % socialBots.Count].Social.Psychology.SocialRng;
             bot = socialBots[chooser.Range(0, socialBots.Count)];
         }
         if (!SocialBots.ContainsKey(bot.Social.Id))
         { return; }
         var facts = CaptureFacts(bot, stock);
         facts.sourceEvent = kind.ToString();
         var motive = kind == ChatEvent.ProfitRealized || kind == ChatEvent.LossRealized ? SocialMotive.SELF_EVENT : kind == ChatEvent.SmallTalk ? SocialMotive.IDLE : SocialMotive.WITNESSED_EVENT;
         Offer(bot, 0, motive, facts);
     }
     public SocialFacts CaptureFacts(TradeBot bot, Stock preferred = null)
     {
         var mind = bot.Social.Psychology;
         Stock stock = preferred ?? ChooseSocialTopic?.Invoke(bot);
         if (stock == null)
         {
             foreach (var pair in bot.Social.Positions)
             { if (bot.GetOwnedVolume(pair.Key) > 0) { stock = pair.Key; break; } }
         }
         if (stock == null && StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
         { stock = StockManager.Instance.stocks[mind.SocialRng.Range(0, StockManager.Instance.stocks.Count)]; }
         var facts = new SocialFacts { at = SocialTime.GameMinute };
         if (stock == null)
         { return facts; }
         facts.symbol = stock.symbol; facts.stockName = stock.stockName; facts.price = Money(stock.currentPrice);
+        facts.canPromise = bot.CanMakeSocialPromise && mind.PromisedAction == SocialPromisedAction.None;
         decimal qty = bot.GetOwnedVolume(stock), basis = bot.GetOwnedAverage(stock);
         facts.quantity = Money(qty); facts.held = qty > 0;
         if (qty > 0 && basis > 0)
         {
             decimal profit = (stock.currentPrice - basis) * qty;
             facts.avgPrice = Money(basis); facts.amount = Money(profit); facts.pct = Percent((stock.currentPrice / basis - 1m) * 100m);
             facts.profitable = profit > 0; facts.losing = profit < 0;
             facts.profitKind = "평가";
         }
         foreach (var fill in mind.SocialFills)
         {
             if (fill.symbol != stock.symbol || SocialTime.GameMinute - fill.at > Config.Get("tradeFactMinutes"))
             { continue; }
             facts.bought |= fill.bought; facts.sold |= !fill.bought;
             if (qty == 0 && !fill.bought && fill.cost > 0)
             {
                 decimal profit = (fill.price - fill.cost) * fill.quantity;
                 facts.amount = Money(profit); facts.pct = Percent((fill.price / fill.cost - 1m) * 100m);
                 facts.profitable = profit > 0; facts.losing = profit < 0; facts.profitKind = "실현";
             }
         }
         int today = (int)(SocialTime.GameMinute / 1440d) + 1;
         facts.dailyProfit = bot.DailySettledDay == today && bot.DailyReturn > 0;
         facts.dailyLoss = bot.DailySettledDay == today && bot.DailyReturn < 0;
         if (bot.Social.Positions.TryGetValue(stock, out var memory))
         {
             double age = SocialTime.Seconds - memory.SellTime;
             bool recent = age >= 0 && age <= Config.Get("legacyTradeSeconds");
             facts.recentSellProfit = recent && memory.SellProfit > 0; facts.recentSellLoss = recent && memory.SellProfit < 0;
             facts.sellThenRise = recent && memory.SellPrice > 0 && stock.currentPrice >= memory.SellPrice * (1m + (decimal)Config.Get("legacyAfterSellRatio"));
             facts.recovered = facts.held && stock.currentPrice >= basis && SocialTime.Seconds - memory.CrossTime <= Config.Get("legacyEventSeconds");
         }
         facts.stressRelief = bot.Social.LastDropStock == stock && SocialTime.Seconds - bot.Social.LastDropTime <= Config.Get("legacyEventSeconds") && bot.Social.LastDropAmount >= Config.Get("legacyStressDrop");
         return facts;
     }
     public static string Money(decimal value) => value.ToString("#,##0", SocialCulture);
     public static string Percent(decimal value) => value.ToString("+0.0;-0.0;0.0", SocialCulture);
     private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
     {
         var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
         if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
         { return; }
         // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
         Stock topic = FindSocialStock(intent.facts?.symbol);
         var facts = CaptureFacts(bot, topic);
         facts.sourceEvent = intent.facts?.sourceEvent;
         facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
         if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
         if (intent.facts != null && intent.facts.directLead) { facts = intent.facts; }
         if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
         { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
         PrepareSocialFacts(bot, intent, ref facts);
         var candidates = BuildSocialWeights(bot, intent, facts);
         SocialAttempts++;
         var debugWeights = candidates.ToArray();
         while (candidates.Count > 0)
         {
             double total = 0; foreach (var candidate in candidates) { total += candidate.value; }
             if (total <= 0)
             { return; }
             double roll = mind.SocialRng.Next() * total; int chosen = candidates.Count - 1;
             for (int i = 0; i < candidates.Count; i++) { roll -= candidates[i].value; if (roll <= 0) { chosen = i; break; } }
             var selection = candidates[chosen]; candidates.RemoveAt(chosen);
             if (!Enum.TryParse<SocialAct>(selection.key, out var act))
             { return; }
             if (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER || act == SocialAct.CALL_PLAYER) { intent.targetId = -1; }
             else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT)) { intent.targetId = 0; }
             else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
             var templates = EligibleTemplates(bot, intent, facts, act);
             if (templates.Count == 0)
-            { continue; }
+            { RecordTemplateMiss(act); continue; }
             var template = templates[mind.SocialRng.Range(0, templates.Count)];
             var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                 at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                 facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
+                promisedAction = template.promisedAction,
                 debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
             string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
-            message.text = SlotPattern.Replace(template.text, match => Slot(facts, match.Groups[1].Value, target));
+            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
+            string phrase = ContextualSocialText(template, message, parentMessage);
+            message.text = SlotPattern.Replace(phrase, match => Slot(facts, match.Groups[1].Value, target));
+            message.text = DecorateSocialText(mind, message, message.text);
             if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
             BeforeSocialPublish(bot, intent, message);
+            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
             mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
             mind.RecentTemplateKeys.Enqueue(template.key);
             while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
             room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
             room.sent.Enqueue(SocialTime.Seconds);
             consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
             lastAct[mind.SocialId] = act;
             socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
             while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
             SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
             return;
         }
         TemplateBlocked++;
     }
     private List<SocialWeight> BuildSocialWeights(TradeBot bot, SocialIntent intent, SocialFacts facts)
     {
         var result = new List<SocialWeight>(); var mind = bot.Social.Psychology;
         SocialActTable table = null;
         foreach (var row in ChatSocialAssets.Acts.motives) { if (row.key == intent.motive.ToString()) { table = row; break; } }
         SelectSocialReplyTable(intent, ref table);
         if (table == null)
         { return result; }
         var entries = intent.forcedAct.HasValue ? new[] { new SocialWeight { key = intent.forcedAct.Value.ToString(), value = 1 } } : table.weights;
         foreach (var entry in entries)
         {
             if (!Enum.TryParse<SocialAct>(entry.key, out var act)) { result.Add(new SocialWeight { key = entry.key, value = entry.value }); continue; }
             double weight = entry.value;
             if (act == SocialAct.BRAG && !facts.profitable || act == SocialAct.LAMENT && !facts.losing ||
                 (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER) && !facts.directLead && facts.notice != "KICK_WARNING" && facts.notice != "LEFT_WARNING" ||
                 (act == SocialAct.ANSWER || act == SocialAct.AGREE || act == SocialAct.REBUT || act == SocialAct.MOCK || act == SocialAct.INSULT || act == SocialAct.COMFORT || act == SocialAct.APOLOGIZE || act == SocialAct.MEDIATE) && intent.targetId == 0)
             { weight = 0; }
             if (act == SocialAct.BRAG || act == SocialAct.MOCK) { weight *= .5 + mind.Traits.Bravado / 100d; }
             if (act == SocialAct.MEDIATE) { weight *= mind.Traits.Sociability / 100d * (1.5 - mind.Traits.Temper / 100d); }
             if (act == SocialAct.LAMENT) { weight *= .5 + mind.Fear / 100d; }
             if (act == SocialAct.PRAISE_PLAYER) { weight *= Math.Max(Config.Get("chillSpeechFloor"), 1 - SocialRoom(intent.roomId).chill / Config.Get("chillSpeechDivisor")); }
             if (act == SocialAct.MOCK || act == SocialAct.INSULT || act == SocialAct.REBUT) { weight *= 1 - SocialRoom(intent.roomId).discipline / Config.Get("attackDisciplineDivisor"); }
             if (lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act && consecutiveAct[mind.SocialId] >= Config.Int("maxConsecutiveAct") && !(intent.threadId > 0 && (act == SocialAct.MOCK || act == SocialAct.REBUT)))
             { weight = 0; }
             AdjustSocialWeight(bot, intent, act, ref weight);
             if (weight > 0) { result.Add(new SocialWeight { key = entry.key, value = weight }); }
         }
         return result;
     }
     private List<SocialTemplate> EligibleTemplates(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act)
     {
         var result = new List<SocialTemplate>(); var mind = bot.Social.Psychology;
         string style = mind.Traits.Politeness >= Config.Int("styleFormalMin") ? "formal" : mind.Traits.Politeness >= Config.Int("styleCasualMin") ? "casual" : "rough";
         double emotion = act == SocialAct.BRAG ? mind.Greed : act == SocialAct.LAMENT ? mind.Fear : 0;
         PrepareSocialStyle(bot, intent, act, ref style, ref emotion);
         int intensity = emotion >= Config.Get("intensityThree") ? 3 : emotion >= Config.Get("intensityTwo") ? 2 : 1;
-        foreach (var template in ChatSocialAssets.Templates.templates)
+        foreach (var template in ChatSocialAssets.Candidates(act, style, intensity, "plain"))
         {
             if (template.act != act.ToString() || template.style != style || template.intensity != intensity || template.variant != "plain" || facts.signal != (template.requires != null && Array.IndexOf(template.requires, "signal") >= 0))
             { continue; }
             if (mind.RecentTemplateKeys.Contains(template.key))
             { continue; }
+            if (template.promisedAction != SocialPromisedAction.None && (!facts.canPromise || template.promisedAction == SocialPromisedAction.Sell && !facts.held))
+            { continue; }
             if (facts.notice != null && (template.requires == null || Array.IndexOf(template.requires, "notice:" + facts.notice) < 0))
             { continue; }
             bool repeat = false; foreach (var recent in SocialRoom(intent.roomId).recent) { if (recent.templateKey == template.key) { repeat = true; break; } }
             if (repeat || !TemplateFactsMatch(template, facts))
             { continue; }
             bool missing = false;
             foreach (Match match in SlotPattern.Matches(template.text))
             { if (string.IsNullOrEmpty(Slot(facts, match.Groups[1].Value, intent.targetId == 0 ? "" : "target"))) { missing = true; break; } }
             if (!missing) { result.Add(template); }
         }
         PrepareMemoryTemplates(bot, intent, facts, act, style, intensity, result);
         return result;
     }
     private static bool TemplateFactsMatch(SocialTemplate template, SocialFacts facts)
     {
         if (template.requires == null)
         { return true; }
         foreach (string requirement in template.requires)
         {
             if (requirement.StartsWith("notice:", StringComparison.Ordinal))
             { if (facts.notice != requirement.Substring(7)) { return false; } continue; }
             if (requirement.StartsWith("event:", StringComparison.Ordinal))
             { if (facts.sourceEvent != requirement.Substring(6)) { return false; } continue; }
             if (requirement.StartsWith("memory:", StringComparison.Ordinal))
             { if (facts.memoryKind != requirement.Substring(7)) { return false; } continue; }
             bool valid = requirement switch
             {
                 "held" or "UnrealizedProfitPositive" => facts.held && (requirement == "held" || facts.profitable),
                 "UnrealizedProfitNegative" => facts.held && facts.losing,
                 "bought" => facts.bought, "sold" => facts.sold, "profit" => facts.profitable, "loss" => facts.losing,
                 "leadGain" => facts.directLead && facts.profitable, "leadLoss" => facts.directLead && facts.losing,
                 "notHeld" => !facts.held, "RecentProfitableSell" => facts.recentSellProfit, "RecentLosingSell" => facts.recentSellLoss,
                 "RecentSellThenPriceRise" => facts.sellThenRise, "DailyReturnPositive" => facts.dailyProfit,
                 "DailyReturnNegative" => facts.dailyLoss, "CrossedUnrealizedLossToNonNegative" => facts.recovered,
                 "RecentStressDrop" => facts.stressRelief, "signal" => facts.signal,
                 _ => false
             };
             if (!valid) { return false; }
         }
         return true;
     }
     private static string Slot(SocialFacts f, string slot, string target) => slot switch
     {
         "symbol" or "stockName" => f.stockName ?? f.symbol, "target" => target, "pct" => f.pct, "amount" => f.amount,
         "avgPrice" => f.avgPrice, "price" => f.price, "quantity" => f.quantity, "profitKind" => f.profitKind, "memSymbol" => f.memSymbol, "memPct" => f.memPct, "memWhen" => f.memWhen,
         "bidStatus" => f.bidStatus, "askStatus" => f.askStatus, "signalResult" => f.signalResult, "departedName" => f.departedName, "memAmount" => f.memAmount, _ => null
     };
     private Stock FindSocialStock(string symbol)
     {
         if (string.IsNullOrEmpty(symbol) || StockManager.Instance == null)
         { return null; }
         foreach (var stock in StockManager.Instance.stocks) { if (stock.symbol == symbol) { return stock; } }
         return null;
     }
     private bool ValidateSocialIntent(TradeBot bot, SocialIntent intent)
     {
         if (intent.targetId > 0 && (!SocialBots.TryGetValue(intent.targetId, out var target) || !Present(target, intent.roomId)))
         { return false; }
         bool valid = true; CheckSocialThread(bot, intent, ref valid); return valid;
     }
     partial void BeforeSocialTick();
     partial void BeforeSocialBot(TradeBot bot);
     partial void ExpireSocialIntent(TradeBot bot, SocialIntent intent);
     partial void SelectSocialReplyTable(SocialIntent intent, ref SocialActTable table);
     partial void AdjustSocialWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight);
     partial void PrepareSocialFacts(TradeBot bot, SocialIntent intent, ref SocialFacts facts);
     partial void PrepareSocialStyle(TradeBot bot, SocialIntent intent, SocialAct act, ref string style, ref double emotion);
     partial void PrepareMemoryTemplates(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act, string style, int intensity, List<SocialTemplate> result);
     partial void CheckSocialThread(TradeBot bot, SocialIntent intent, ref bool valid);
     partial void BeforeSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message);
     partial void AfterSocialPublish(TradeBot bot, SocialIntent intent, SocialMessage message);
 }

 [Serializable] public sealed class SocialTradeFact
 {
     public string symbol;
     public bool bought;
     public decimal price, quantity, cost;
     public double at;
 }
diff --git a/Assets/Scripts/NightShift/ChatSocialUnity.cs b/Assets/Scripts/NightShift/ChatSocialUnity.cs
index 5732566..9b38397 100644
--- a/Assets/Scripts/NightShift/ChatSocialUnity.cs
+++ b/Assets/Scripts/NightShift/ChatSocialUnity.cs
@@ -5,118 +5,121 @@ using UnityEngine;
 public sealed partial class InvestorSociety
 {
     private bool socialWired;
-    private StreamWriter socialLog;
+    private SocialLogSink socialLog;
     private float nextSocialLogFlush;
     private void PrepareSocialRuntime()
     {
         BindSocialBots(day.Bots);
         if (!socialWired)
         {
             ChooseSocialTopic = bot => day.ConversationFocus.Choose(bot.Social.Psychology.SocialRng.Next(), Time.time);
             RefreshOfferedFacts = RefreshNpcFacts;
             SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, Time.time).ReturnPercent;
             WitnessChance = bot => Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + bot.Social.Psychology.Activity * Config.Get("witnessActivityScale"));
             SocialMessagePublished += DeliverSocialMessage;
             SocialDeparted += OnSocialDeparture;
             CanModerateRoom = room => IsRoomOpen(room);
             SocialSystemNotice += DeliverSocialSystemNotice;
             try
             {
                 string directory = Path.Combine(Application.persistentDataPath, "SocialLogs"); Directory.CreateDirectory(directory);
-                socialLog = new StreamWriter(Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl"));
+                socialLog = new SocialLogSink(new StreamWriter(Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl")),
+                    message => Debug.LogWarning("Social log disabled; in-memory audit continues: " + message));
                 ChatSocialAudit.Journal.Changed += WriteSocialChange; ChatSocialAudit.Journal.Happened += WriteSocialEvent;
             }
             catch (IOException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
+            catch (UnauthorizedAccessException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
             socialWired = true;
         }
         SocialTime.Set(day.Day, day.GameMinute, Time.time);
         if (socialLog != null && Time.unscaledTime >= nextSocialLogFlush)
         { socialLog.Flush(); nextSocialLogFlush = Time.unscaledTime + (float)Config.Get("logFlushSeconds"); }
     }
-    private void WriteSocialChange(SocialChange change) { socialLog?.WriteLine(JsonUtility.ToJson(change)); }
-    private void WriteSocialEvent(SocialEvent record) { socialLog?.WriteLine(JsonUtility.ToJson(record)); }
+    private void WriteSocialChange(SocialChange change) { socialLog?.Write(JsonUtility.ToJson(change)); }
+    private void WriteSocialEvent(SocialEvent record) { socialLog?.Write(JsonUtility.ToJson(record)); }
     private void DisposeSocialRuntime()
     {
         ChatSocialAudit.Journal.Changed -= WriteSocialChange; ChatSocialAudit.Journal.Happened -= WriteSocialEvent;
         SocialMessagePublished -= DeliverSocialMessage; socialLog?.Dispose(); socialLog = null;
         SocialDeparted -= OnSocialDeparture;
         SocialSystemNotice -= DeliverSocialSystemNotice;
+        socialWired = false;
     }
     private void DeliverSocialSystemNotice(int roomId, string text, long id)
     {
         var room = GetRoom(roomId);
         if (room == null || day == null) { return; }
         room.Add(new BotChatMessage(room.Version + 1, null, day.Day, day.GameMinute, text, "SYSTEM.MODERATION", ChatEvent.Agreement,
             new InvestorMessageContext(id, roomId, 0, 1, 0, false)));
         LastAction = text;
     }
     private void OnSocialDeparture(TradeBot bot, int room, string reason) { Leave(bot, reason); }
     private void DeliverSocialMessage(SocialMessage message)
     {
         if (!SocialBots.TryGetValue(message.senderId, out var bot))
         { return; }
         var context = new InvestorMessageContext(message.facts?.eventId ?? 0, message.roomId,
             message.act == SocialAct.PRAISE_PLAYER ? 1 : message.act == SocialAct.WARN_PLAYER ? -1 : 0,
             bot.Social.Confidence, bot.Social.Psychology.Trust, message.facts != null && message.facts.directLead,
             message.replyToId, target: message.targetId);
         if (message.roomId == 0) { day.PublishSocialParticipant(bot, message, context); }
         else
         {
             var room = GetRoom(message.roomId);
             room?.Add(new BotChatMessage(room.Version + 1, bot, day.Day, day.GameMinute, message.text, message.templateKey, ChatEvent.Agreement, context, message));
         }
     }
     public void ObserveSocialFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
     {
         if (ReferenceEquals(buyer, seller) || volume <= 0 || resting == null)
         { return; }
         double at = day == null ? 0 : (day.Day - 1) * 1440d + day.GameMinute;
         if (buyer is TradeBot buying && buying.Social != null)
         {
             buying.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = true, price = price, quantity = volume, cost = price, at = at });
             Offer(buying, buying.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(buying, stock));
         }
         if (seller is TradeBot selling && selling.Social != null)
         {
             decimal basis = ReferenceEquals(resting.owner, seller) ? resting.costBasis : selling.SubmittedSellCost;
             selling.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = false, price = price, quantity = volume, cost = basis, at = at });
             bool leading = Orders?.Get(selling) is LeadingRoomOrders.Plan plan && plan.Command.Stock == stock && !plan.Reported && plan.Remaining > 0;
             if (!leading && basis > 0)
             {
                 decimal profit = (price - basis) * volume;
                 selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, Time.time,
                     assetBefore: Math.Max(0, selling.Equity() - profit)), false);
             }
             Offer(selling, selling.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(selling, stock));
         }
     }
     public void OfferExistingSpeech(TradeBot bot, int room, InvestorMessageContext context, ChatEvent kind)
     {
         var facts = CaptureFacts(bot);
         if (context.DirectExperience && bot.Social.Psychology.Latest.HasValue)
         {
             var experience = bot.Social.Psychology.Latest.Value;
             if (experience.EventId == context.EventId)
             {
                 facts = new SocialFacts { symbol = experience.Symbol, stockName = experience.Symbol, amount = Money(experience.Proceeds - experience.Cost),
                     pct = Percent(experience.ReturnPercent), directLead = true, eventId = experience.EventId, at = SocialTime.GameMinute,
                     profitable = experience.ReturnPercent > 0, losing = experience.ReturnPercent < 0, profitKind = "실현" };
             }
         }
         Offer(bot, room, facts.directLead ? SocialMotive.SELF_EVENT : SocialMotive.WITNESSED_EVENT, facts);
     }
     public void OfferNpcObservation(TradeBot leader)
     {
         var facts = RefreshNpcFacts(leader, null);
         if (facts != null) { Offer(leader, NpcLeadingRoom.RoomId, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: facts.eventId); }
     }
     private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
     {
         var npc = NpcLeadingRoom.Instance; var signal = npc?.Current;
         if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= Time.time)
         { return null; }
         return new SocialFacts { signal = true, eventId = signal.Id, symbol = signal.Stock.symbol, stockName = signal.Stock.stockName, at = SocialTime.GameMinute,
             bidStatus = signal.Buy > 0 ? Money(signal.Buy) + (signal.RestingBuy ? "원 매수 주문 대기" : "원 최근 매수 체결") : "매수 주문 없음",
             askStatus = signal.Sell > 0 ? Money(signal.Sell) + "원 매도 주문 대기" : "매도 주문 없음", signalResult = Money(npc.RealizedProfit) };
     }
 }
diff --git a/Assets/Scripts/TradeBot/TradeBot.cs b/Assets/Scripts/TradeBot/TradeBot.cs
index 414770d..128dc82 100644
--- a/Assets/Scripts/TradeBot/TradeBot.cs
+++ b/Assets/Scripts/TradeBot/TradeBot.cs
@@ -5,851 +5,867 @@ using UnityEngine;
 public abstract partial class TradeBot : Trader
 {
     [Header("주문 규모 (비율)")]
     [Tooltip("매수 시 쓸 현금 비율 (0.1 = 10%)")]
     public decimal buyCashRatio = 0.1m;
     [Tooltip("매도 시 쓸 보유 수량 비율 (0.1 = 10%)")]
     public decimal sellHoldingRatio = 0.1m;
     [Tooltip("비율로 계산한 수량이 이보다 작으면 주문 안 함")]
     public decimal minOrderVolume = 1m;
     [Header("매도 주문 취소")]
     [Tooltip("대기 매도 중 최근가와 이 비율(0.2=20%) 이상 차이나면 취소하고 이번 턴 종료")]
     [Range(0.01f, 1f)]
     public float cancelSellOrderPercentDiff = 0.2f;
     [Header("주문 지연 (자연스러운 매매)")]
     [Tooltip("매매 판단 후 주문까지 최소 지연 시간(초)")]
     public float orderDelayMin = 1f;
     [Tooltip("매매 판단 후 주문까지 최대 지연 시간(초)")]
     public float orderDelayMax = 5f;
     [Header("목표가 기반 주문")]
     [Tooltip("현재가가 목표가에서 이 비율(0.02=2%) 이내면 주문 실행")]
     [Range(0.005f, 0.1f)]
     public float targetPriceThreshold = 0.02f;
     [Tooltip("대기 중인 목표가가 현재가와 이 비율(0.1=10%) 이상 차이나면 취소")]
     [Range(0.05f, 0.3f)]
     public float targetPriceCancelThreshold = 0.10f;
     [Header("스프레드 보호")]
     [Tooltip("매수/매도 호가 갭이 이 비율(0.03=3%) 이상이면 스프레드 안쪽에 주문")]
     [Range(0.01f, 0.1f)]
     public float maxSpreadRatio = 0.03f;
     protected class PendingOrder
     {
         public Stock stock;
         public decimal targetPrice;
         public decimal volume;
         public bool isBuy;
         public int subTickSteps;
         public float createdTime;
         public int intentVersion;
     }

     private float nextThinBookOffer;
     private readonly List<(decimal price, decimal volume)> thinBookAsks = new(3), thinBookBids = new(1);
     private readonly List<Order> thinBookOwnedOrders = new();
     protected List<PendingOrder> _pendingOrders = new List<PendingOrder>();
     [Header("심리적 저항선 반응")]
     [Tooltip("저항선 근처에서 매도벽 형성에 참여할 확률")]
     [Range(0f, 1f)]
     public float wallParticipationChance = 0.3f;
     [Tooltip("돌파 직전 매도 주문 철회 확률")]
     [Range(0f, 1f)]
     public float wallWithdrawChance = 0.5f;
     [Header("랜덤 주문 취소")]
     [Tooltip("매 Think 턴마다 이 확률로 대기 주문 랜덤 취소 시도 (0이면 비활성)")]
     [Range(0f, 1f)]
     public float chanceToTryRandomCancel = .4f;
     [Tooltip("취소 시도 시 각 주문당 취소될 확률")]
     [Range(0f, 1f)]
     public float randomOrderCancelChancePerOrder = 0.08f;
     [Header("종목 전문화")]
     [Tooltip("단일 종목 전문 트레이더 여부 (런타임 자동 설정)")]
     [SerializeField]
     protected bool _isSingleStockTrader;
     [Tooltip("전문 거래 종목 (단일 종목 트레이더인 경우)")]
     [SerializeField]
     protected Stock _specializedStock;
     /// <summary>유동성 봇용 종목별 선호도. 수익 시 누적, 퇴장 후 복귀 시 0으로 리셋.</summary>
     protected Dictionary<Stock, float> _stockPreference = new Dictionary<Stock, float>();
     /// <summary>
     /// 단일 종목 전문 트레이더가 될 확률. 봇 유형별로 오버라이드.
     /// 기본값 0 = 이 기능을 사용하지 않음 (MonitorBot, ScalperBot 등)
     /// </summary>
     protected virtual float GetSingleStockChance() => 0f;
     /// <summary>단일 종목 전문화 초기화. Start()에서 호출.</summary>
     protected void InitializeSingleStockSpecialization()
     {
         float chance = GetSingleStockChance();
         if (chance <= 0f)
         { return; }

         if (Random.value < chance)
         {
             if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
             {
                 _isSingleStockTrader = true;
                 _specializedStock = StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)];
             }
         }
     }

     /// <summary>단일 종목 전문 트레이더 여부</summary>
     public bool IsSingleStockTrader => _isSingleStockTrader;
     /// <summary>전문 거래 종목</summary>
     public Stock SpecializedStock => _specializedStock;
     public BotSocialState Social { get; internal set; }

     protected override void Awake()
     {
         if (MarketDayController.Instance != null && !MarketDayController.Instance.Register(this))
         {
             enabled = false;
             Destroy(gameObject);
             return;
         }

         base.Awake();
         StockManager.OnStockDelisted += OnStockDelisted;
     }

     public static T Create<T>(string name, Transform parent = null)
         where T : TradeBot
     {
         if (typeof(T) == typeof(ExchangeBot) && ExchangeBot.Instance != null)
         { return ExchangeBot.Instance as T; }

         if (MarketDayController.Instance != null && MarketDayController.Instance.Bots.Count >= Mathf.Clamp(MarketDayController.Instance.maxBots, 1, 2000))
         { return null; }

         var go = new GameObject(name);
         if (parent != null)
         {
             go.transform.SetParent(parent, false);
         }

         return go.AddComponent<T>();
     }

     public virtual void SuspendSession()
     {
         OnDisable();
     }

     public void ResumeSession()
     {
         if (_started && isActiveAndEnabled)
         {
             OnEnable();
         }
     }

     private bool _started;
     protected bool HasStarted => _started;

     private Coroutine _thinkLoop;
     protected virtual void Start()
     {
         InitializeRiskPreference(StockManager.Instance == null ? 12345 : StockManager.Instance.economy.seed);
         _started = true;
         ResumeThinking();
         StartCoroutine(FinalizeInitialAccount());
     }

     private IEnumerator FinalizeInitialAccount()
     {
         yield return null;
         if (StockManager.Instance != null && StockManager.Instance.SetupConfirmed)
         {
             StockManager.Instance.AssignInitialCash(this);
         }

         if (MarketDayController.Instance != null && MarketDayController.Instance.Ready)
         {
             BeginDailyBaseline();
         }
     }

     protected virtual void OnEnable()
     {
         if (_started)
         {
             ResumeThinking();
         }
     }

     private void ResumeThinking()
     {
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || _thinkLoop != null)
         { return; }

         PsychologicalLevelManager.OnWallForming += OnWallForming;
         PsychologicalLevelManager.OnBreakthroughImminent += OnBreakthroughImminent;
         _thinkLoop = StartCoroutine(ThinkLoop());
     }

     protected virtual void OnDisable()
     {
         PsychologicalLevelManager.OnWallForming -= OnWallForming;
         PsychologicalLevelManager.OnBreakthroughImminent -= OnBreakthroughImminent;
         StopAllCoroutines();
         _thinkLoop = null;
         // These are intentions only; no cash/shares have been reserved yet.
         _pendingOrders.Clear();
     }

     /// <summary>이탈 청산 전에 사고·지연 주문·거래 이벤트를 중단. 청산 SellLimit은 아직 활성 상태에서 실행.</summary>
     public virtual void PrepareForDeparture()
     {
         OnDisable();
     }

     protected override void OnDestroy()
     {
         OnDisable();
         InvestorSociety.Instance?.Forget(this);
         StockManager.OnStockDelisted -= OnStockDelisted;
         MarketDayController.Instance?.Unregister(this);
         base.OnDestroy();
     }

     public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
     {
         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
         { return; }

         if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
         { return; }
         if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
         { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
         MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
         base.BuyLimit(price, volume, subTickSteps, stock);
     }

     public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
     {
         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         if (!CanSellStock(ResolveStock(stock)))
         { return; }

         if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
         { return; }
         MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
         base.SellLimit(price, volume, subTickSteps, stock);
     }

     /// <summary>상장폐지 시 해당 종목 키/참조 정리. 파생 봇에서 오버라이드해 자체 Dictionary 정리.</summary>
     void OnStockDelisted(Stock stock)
     {
         if (stock == null)
         { return; }

         Social?.Positions.Remove(stock);
         if (Social != null && Social.LastDropStock == stock)
         {
             Social.LastDropStock = null;
         }

         _stockPreference.Remove(stock);
         riskResponses.Remove(stock);
         intentVersions.Remove(stock);
         holdings.Remove(stock);
         avgPrice.Remove(stock);
         if (_specializedStock == stock)
         {
             _specializedStock = null;
         }

         _pendingOrders.RemoveAll(o => o.stock == stock);
         OnStockDelistedCleanup(stock);
     }

     protected virtual void OnStockDelistedCleanup(Stock stock)
     {
     }

     /// <summary>저항선 근처 벽 형성 시 매도 주문 추가</summary>
     protected virtual void OnWallForming(Stock stock, decimal resistancePrice)
     {
         if (stock == null)
         { return; }

         if (Random.value > wallParticipationChance)
         { return; }

         decimal holding = GetHolding(stock);
         if (holding <= 0m)
         { return; }

         decimal sellVol = ClampMinVolume(holding * 0.2m);
         if (sellVol <= 0m)
         { return; }

         SellLimit(resistancePrice, sellVol, 0, stock);
     }

     /// <summary>돌파 직전 매도 주문 철회</summary>
     protected virtual void OnBreakthroughImminent(Stock stock, decimal resistancePrice)
     {
         if (!CanSellStock(stock))
         { return; }

         if (stock == null)
         { return; }

         if (Random.value > wallWithdrawChance)
         { return; }

         stock.CancelSellOrdersByOwner(this, stock.currentPrice, -0.01m, 0.02m);
     }

     /// <summary>ThinkLoop 대기 구간(초). 모니터봇처럼 수가 많으면 오버라이드해 더 길게 두면 부하 감소.</summary>
     protected virtual float GetThinkDelayMin() => 2f;
     protected virtual float GetThinkDelayMax() => 10f;
+    public bool CanMakeSocialPromise => !IsCompanyOwner && !IsExchangeParticipant && !(this is WhaleFollowerBot) && MarketRhythmDirector.Instance != null;
+    private Stock PendingSocialStock()
+    {
+        if (Social == null || Social.Psychology.PromisedAction == SocialPromisedAction.None || StockManager.Instance == null)
+        { return null; }
+        foreach (var stock in StockManager.Instance.stocks)
+        {
+            if (stock.symbol != Social.Psychology.PromisedSymbol || !stock.AcceptsOrders) { continue; }
+            if (CanBuyStock(stock) || CanSellStock(stock)) { return stock; }
+            Social.Psychology.ClearPromise("insufficient-resources"); return null;
+        }
+        Social.Psychology.ClearPromise("stock-unavailable"); return null;
+    }
     IEnumerator ThinkLoop()
     {
         while (true)
         {
             yield return new WaitForSeconds(Random.Range(GetThinkDelayMin(), GetThinkDelayMax()));
             if (!MarketDayController.CanTrade)
             { continue; }

             ProcessPendingOrders();
             if (chanceToTryRandomCancel > 0f && Random.value < chanceToTryRandomCancel && StockManager.Instance != null)
             {
                 foreach (var stock in StockManager.Instance.stocks)
                 {
                     stock.CancelRandomOrdersByOwner(this, randomOrderCancelChancePerOrder);
                 }
             }

-            Stock pick = PickStock();
+            Stock promised = PendingSocialStock();
+            Stock pick = promised ?? PickStock();
             if (pick != null && pick.AcceptsOrders && (CanBuyStock(pick) || CanSellStock(pick)) && StockManager.Instance != null && StockManager.Instance.stocks.Contains(pick))
             {
                 RefreshLiquidityOrders(pick);
                 if (CancelDistantSellOrdersAndEndTurnIfAny(pick))
                 { continue; }

                 MarketDayController.Instance?.Consider(this, pick);
+                if (promised != null && MarketRhythmDirector.Instance != null && MarketRhythmDirector.Instance.TryAct(this, pick, true))
+                { continue; }
                 if ((WorldDramaDirector.Instance == null || !WorldDramaDirector.Instance.TryFundDecision(this, pick)) && !TryThinBookSellOffer(pick) && (MarketRhythmDirector.Instance == null || !MarketRhythmDirector.Instance.TryAct(this, pick)))
                 {
                     ThinkForStock(pick);
                 }
             }
         }
     }

     /// <summary>유동성 봇의 오래된 호가만 회수해 예치금과 보유량을 다시 매매에 사용한다.</summary>
     private void RefreshLiquidityOrders(Stock stock)
     {
         if (IsExchangeParticipant || IsCompanyOwner || !(UseStockPreference() || this is ScalperBot))
         { return; }
         stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
         int cancelled = 0;
         foreach (var order in thinBookOwnedOrders)
         {
             if (order.purpose != OrderPurpose.Ordinary)
             { continue; }
             float lifetime = this is ScalperBot ? 20f : 60f;
             if (!order.isBuy && order.price > stock.currentPrice * 1.15m)
             {
                 lifetime = 120f;
             }
             if (Time.time - order.createdAt >= lifetime && stock.orderBook.CancelOrder(order, stock))
             {
                 cancelled++;
                 if (cancelled >= 2)
                 { break; }
             }
         }
     }

     private bool TryThinBookSellOffer(Stock stock)
     {
         if (IsExchangeParticipant || !CanSellStock(stock) || Time.time < nextThinBookOffer || sellHoldingRatio <= 0m || HasPendingOrderFor(stock, false))
         { return false; }
         decimal holding = GetHolding(stock);
         if (holding < minOrderVolume || stock.currentPrice < 1m)
         { return false; }
         stock.orderBook.GetTopAsks(thinBookAsks, 3);
         bool thin = thinBookAsks.Count < 3 || thinBookAsks[0].price > stock.currentPrice * 1.08m;
         if (!thin)
         { return false; }
         stock.orderBook.CopyOwnerOrders(this, thinBookOwnedOrders);
         foreach (var order in thinBookOwnedOrders)
         {
             if (!order.isBuy)
             { return false; }
         }
         float risk = Social == null ? 0.5f : Social.Risk;
         float stress = Social == null ? 0f : Mathf.Clamp01(Social.Stress / 100f);
         if (Random.value > 0.15f + risk * 0.2f + stress * 0.1f)
         { return false; }
         stock.orderBook.GetTopBids(thinBookBids, 1);
         decimal basis = stock.currentPrice;
         if (thinBookBids.Count > 0)
         {
             basis = System.Math.Max(basis, thinBookBids[0].price);
         }
         // Most offers are nearby; a few holders test a deliberately ambitious price.
         float premium = Random.value < 0.15f + risk * 0.15f ? Random.Range(0.15f, 0.65f) : Random.Range(0.01f, 0.08f);
         premium *= 1f - stress * 0.5f;
         decimal price = PriceTickPolicy.CeilToTick(basis * (1m + (decimal)premium));
         decimal ratio = System.Math.Min(0.05m, sellHoldingRatio * (decimal)Random.Range(0.1f, 0.5f));
         decimal volume = ClampMinVolume(holding * ratio);
         if (volume < 1m)
         { return false; }
         nextThinBookOffer = Time.time + Mathf.Max(0f, orderDelayMax) + Random.Range(15f, 30f);
         DelayedSellLimit(price, volume, 0, stock);
         return true;
     }

     /// <summary>대기 중인 목표가 주문 처리: 목표가 근접 시 실행, 10% 이상 차이 시 취소</summary>
     protected virtual void ProcessPendingOrders()
     {
         for (int i = _pendingOrders.Count - 1; i >= 0; i--)
         {
             var order = _pendingOrders[i];
             if (order.stock == null || !order.stock.AcceptsOrders || order.intentVersion != IntentVersion(order.stock) || order.isBuy && !CanBuyStock(order.stock) || order.targetPrice <= 0m)
             {
                 _pendingOrders.RemoveAt(i);
                 continue;
             }

             decimal current = order.stock.currentPrice;
             if (current <= 0m)
             { continue; }

             decimal diff = System.Math.Abs(current - order.targetPrice) / order.targetPrice;
             if (diff >= (decimal)targetPriceCancelThreshold)
             {
                 _pendingOrders.RemoveAt(i);
                 continue;
             }

             if (diff <= (decimal)targetPriceThreshold)
             {
                 if (order.isBuy)
                 {
                     DelayedBuyLimit(order.targetPrice, order.volume, order.subTickSteps, order.stock);
                 }
                 else
                 {
                     DelayedSellLimit(order.targetPrice, order.volume, order.subTickSteps, order.stock);
                 }

                 _pendingOrders.RemoveAt(i);
             }
         }
     }

     /// <summary>목표가 매수 주문 등록. 현재가가 목표가 근처에 도달하면 실행됨.</summary>
     protected void RegisterTargetBuy(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
     {
         if (!CanBuyStock(stock))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
         { return; }

         _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = true, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
     }

     /// <summary>목표가 매도 주문 등록. 현재가가 목표가 근처에 도달하면 실행됨.</summary>
     protected void RegisterTargetSell(Stock stock, decimal targetPrice, decimal volume, int subTickSteps = 0)
     {
         if (!CanSellStock(stock))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled || stock == null || !stock.AcceptsOrders || targetPrice <= 0m || volume <= 0m)
         { return; }

         _pendingOrders.Add(new PendingOrder { stock = stock, targetPrice = targetPrice, volume = volume, isBuy = false, subTickSteps = subTickSteps, createdTime = Time.time, intentVersion = IntentVersion(stock) });
     }

     /// <summary>해당 종목의 대기 주문이 있는지 확인</summary>
     protected bool HasPendingOrderFor(Stock stock, bool? isBuy = null)
     {
         foreach (var order in _pendingOrders)
         {
             if (order.stock == stock)
             {
                 if (!isBuy.HasValue || order.isBuy == isBuy.Value)
                 { return true; }
             }
         }

         return false;
     }

     /// <summary>이번에 거래할 종목 선택. 단일 종목 전문 트레이더는 전문 종목만 반환. 유동성 봇은 선호도·뉴스관심도 반영.</summary>
     protected virtual Stock PickStock()
     {
         if (_isSingleStockTrader)
         { return _specializedStock != null && _specializedStock.AcceptsOrders ? _specializedStock : null; }

         if (Social != null && Social.Psychology.HasFomo && StockManager.Instance != null && Random.value < ChatSocialConfig.Current.Get("fomoPickChance"))
         {
             foreach (var candidate in StockManager.Instance.stocks)
             { if (candidate.AcceptsOrders && Social.Psychology.HasFomoFor(candidate)) { return candidate; } }
         }

         if (Social != null && Social.InfluencedStock != null && Social.InfluencedStock.AcceptsOrders && Time.time < Social.InfluenceUntil && Random.value < .1f + Social.Greed * .25f)
         { return Social.InfluencedStock; }

         if (StockInterestManager.Instance != null)
         { return StockInterestManager.Instance.PickStockByWeightForBot(this); }

         if (StockManager.Instance != null && StockManager.Instance.stocks.Count > 0)
         { return StockManager.Instance.stocks[Random.Range(0, StockManager.Instance.stocks.Count)]; }

         return null;
     }

     /// <summary>유동성 봇: true면 종목별 선호도·뉴스 관심도로 PickStock 가중치 적용. 선호도 높을수록 해당 종목 ThinkForStock 호출 빈도 증가.</summary>
     public virtual bool UseStockPreference() => false;
     /// <summary>종목 선호도 (수익 시 누적). 퇴장 후 복귀 시 0.</summary>
     public float GetPreference(Stock stock)
     {
         float value = stock != null && _stockPreference.TryGetValue(stock, out float preference) ? preference : 0f;
         return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, StockManager.Instance == null ? 100f : Mathf.Max(1f, StockManager.Instance.economy.maximumAttentionFactor));
     }

     /// <summary>퇴장 후 복귀 시 모든 종목 선호도 0으로 리셋. TradeBotManager.BotReturn에서 호출.</summary>
     public void ResetStockPreferences()
     {
         _stockPreference.Clear();
     }

     protected override void OnSellFilled(Stock stock, decimal filledVolume, decimal totalCost, decimal avgFillPrice, decimal avgCostBeforeSell)
     {
         if (!UseStockPreference() || stock == null || avgCostBeforeSell <= 0m)
         { return; }

         if (avgFillPrice <= avgCostBeforeSell)
         { return; }

         decimal profitRate = (avgFillPrice - avgCostBeforeSell) / avgCostBeforeSell;
         if (!_stockPreference.ContainsKey(stock))
         {
             _stockPreference[stock] = 0f;
         }

         _stockPreference[stock] += (float)profitRate;
     }

     /// <summary>
     /// 매매 기준가(FakePrice). StupidBot·SpreadFillerBot만 오버라이드해서 현재가 사용.
     /// 나머지 봇은 fakeValue 기준 매매. fakeValue는 realValue 쪽으로 초당 일정 비율 수렴.
     /// </summary>
     protected virtual decimal GetReferencePrice(Stock stock)
     {
         if (stock == null)
         { return 0m; }

         return stock.fakeValue;
     }

     /// <summary>
     /// RealPrice만 보다 매도 호가가 현재가에서 너무 멀어지는 것을 방지.
     /// 현재가가 Real보다 높을 때: refBasedSellPrice와 현재가 부근(1틱 아래) 중 더 높은 쪽을 사용해, 시장에 매도 호가가 나가도록 함.
     /// </summary>
     protected decimal GetEffectiveSellPrice(Stock stock, decimal refBasedSellPrice)
     {
         if (stock == null || refBasedSellPrice <= 0m)
         { return refBasedSellPrice; }

         decimal real = GetReferencePrice(stock);
         decimal current = stock.currentPrice;
         if (current <= 0m || current <= real)
         { return refBasedSellPrice; }

         decimal nearCurrent = PriceTickPolicy.OneTickBelow(current);
         if (nearCurrent <= 0m)
         { return refBasedSellPrice; }

         return refBasedSellPrice > nearCurrent ? refBasedSellPrice : nearCurrent;
     }

     /// <summary>
     /// RealPrice만 보다 매수 대기가 현재가에서 너무 멀어지는 것을 방지.
     /// 현재가가 Real보다 낮을 때: refBasedBuyPrice와 현재가 부근(1틱 위) 중 더 낮은 쪽을 사용해, 시장에 매수 호가가 나가도록 함.
     /// </summary>
     protected decimal GetEffectiveBuyPrice(Stock stock, decimal refBasedBuyPrice)
     {
         if (stock == null)
         { return refBasedBuyPrice; }

         decimal real = GetReferencePrice(stock);
         decimal current = stock.currentPrice;
         if (current <= 0m || real <= 0m || current >= real)
         { return refBasedBuyPrice; }

         decimal nearCurrent = PriceTickPolicy.OneTickAbove(current);
         if (nearCurrent <= 0m)
         { return refBasedBuyPrice; }

         if (refBasedBuyPrice <= 0m)
         { return nearCurrent; }

         return refBasedBuyPrice < nearCurrent ? refBasedBuyPrice : nearCurrent;
     }

     /// <summary>현금의 일정 비율로 매수할 수량 (가격 반영). stock 없으면 CurrentStock. referencePrice 넣으면 그 가격 기준(미입력 시 현재가). 항상 정수로 내림.</summary>
     protected decimal GetBuyVolume(decimal cashRatio, Stock stock = null, decimal? referencePrice = null)
     {
         Stock s = ResolveStock(stock);
         decimal price = referencePrice.HasValue && referencePrice.Value > 0m ? referencePrice.Value : s.currentPrice;
         if (price <= 0m)
         { return 0m; }

         decimal available = System.Math.Max(0m, cash - reservedCash);
         return decimal.Floor((available * System.Math.Clamp(cashRatio, 0m, 1m)) / price);
     }

     /// <summary>보유 수량의 일정 비율로 매도할 수량. stock 없으면 CurrentStock. 항상 정수로 내림.</summary>
     protected decimal GetSellVolume(decimal holdingRatio, Stock stock = null)
     { return decimal.Floor(GetHolding(ResolveStock(stock)) * holdingRatio); }

     /// <summary>최소 주문 수량 체크. 정수로 내림 처리.</summary>
     protected decimal ClampMinVolume(decimal volume)
     {
         volume = decimal.Floor(volume);
         return volume >= minOrderVolume ? volume : 0m;
     }

     /// <summary>대기 매도 취소: 현재가 대비 -20%~+80% 밖이면 무조건 취소, 안이면 거리 비율로 확률 취소. 하나라도 취소되면 true(한 턴 끝).</summary>
     protected bool CancelDistantSellOrdersAndEndTurnIfAny(Stock stock)
     {
         if (stock == null || stock.currentPrice <= 0m)
         { return false; }

         int n = stock.CancelSellOrdersByOwner(this, stock.currentPrice, rangeMinPct: -0.20m, rangeMaxPct: 0.80m);
         return n > 0;
     }

     /// <summary>관심도로 종목을 고른 뒤 호출됨. 이 종목에 대해 매매 로직 실행.</summary>
     protected abstract void ThinkForStock(Stock stock);
     /// <summary>
     /// 스프레드가 넓을 때 (3% 이상) 스프레드 안쪽 가격 반환.
     /// 매수 시: 최고매수가 + 1틱 ~ 중간값 사이
     /// 매도 시: 중간값 ~ 최저매도가 - 1틱 사이
     /// 스프레드가 좁으면 null 반환 (원래 로직 사용)
     /// </summary>
     protected decimal? GetSpreadSafePrice(Stock stock, bool isBuy)
     {
         if (stock == null)
         { return null; }

         var book = stock.orderBook;
         if (book == null)
         { return null; }

         var bids = book.GetTopBids();
         var asks = book.GetTopAsks(1);
         if (bids.Count == 0 || asks.Count == 0)
         { return null; }

         decimal bestBid = bids[0].price;
         decimal bestAsk = asks[0].price;
         if (bestBid <= 0m || bestAsk <= 0m || bestAsk <= bestBid)
         { return null; }

         decimal spread = bestAsk - bestBid;
         decimal midPrice = (bestBid + bestAsk) / 2m;
         decimal spreadRatio = spread / midPrice;
         if (spreadRatio < (decimal)maxSpreadRatio)
         { return null; }

         decimal oneTick = PriceTickPolicy.GetTickSize(midPrice);
         if (isBuy)
         {
             decimal minPrice = bestBid + oneTick;
             decimal maxPrice = midPrice;
             if (minPrice >= maxPrice)
             { return PriceTickPolicy.FloorToTick(midPrice); }

             decimal price = minPrice + (maxPrice - minPrice) * (decimal)Random.Range(0.3f, 0.7f);
             return PriceTickPolicy.FloorToTick(price);
         }
         else
         {
             decimal minPrice = midPrice;
             decimal maxPrice = bestAsk - oneTick;
             if (minPrice >= maxPrice)
             { return PriceTickPolicy.CeilToTick(midPrice); }

             decimal price = minPrice + (maxPrice - minPrice) * (decimal)Random.Range(0.3f, 0.7f);
             return PriceTickPolicy.CeilToTick(price);
         }
     }

     /// <summary>즉시 체결에 가까운 지정가 (시장가 대체). 매수=최우선매도가, 매도=최우선매수가. 호가 없으면 현재가 ± 1틱.</summary>
     protected decimal GetAggressiveLimitPrice(Stock stock, bool isBuy)
     {
         if (stock == null)
         { return 0m; }

         var book = stock.orderBook;
         decimal tick = PriceTickPolicy.GetTickSize(stock.currentPrice);
         if (book == null)
         { return isBuy ? stock.currentPrice + tick : stock.currentPrice - tick; }

         if (isBuy)
         {
             var asks = book.GetTopAsks(1);
             if (asks.Count > 0 && asks[0].price > 0m)
             { return asks[0].price; }

             return PriceTickPolicy.CeilToTick(stock.currentPrice + (tick > 0m ? tick : 0.01m));
         }
         else
         {
             var bids = book.GetTopBids();
             if (bids.Count > 0 && bids[0].price > 0m)
             { return bids[0].price; }

             return PriceTickPolicy.FloorToTick(stock.currentPrice - (tick > 0m ? tick : 0.01m));
         }
     }

     /// <summary>
     /// 스프레드가 넓은지 확인 (3% 이상)
     /// </summary>
     protected bool IsSpreadWide(Stock stock)
     {
         if (stock == null)
         { return false; }

         var book = stock.orderBook;
         if (book == null)
         { return false; }

         var bids = book.GetTopBids();
         var asks = book.GetTopAsks(1);
         if (bids.Count == 0 || asks.Count == 0)
         { return false; }

         decimal bestBid = bids[0].price;
         decimal bestAsk = asks[0].price;
         if (bestBid <= 0m || bestAsk <= 0m || bestAsk <= bestBid)
         { return false; }

         decimal spread = bestAsk - bestBid;
         decimal midPrice = (bestBid + bestAsk) / 2m;
         decimal spreadRatio = spread / midPrice;
         return spreadRatio >= (decimal)maxSpreadRatio;
     }

     /// <summary>지연 후 지정가 매수. 가격과 수량을 미리 정해두고 0.5~3초 후 주문.</summary>
     protected void DelayedBuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
     {
         if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         StartCoroutine(DelayedBuyLimitCoroutine(price, volume, subTickSteps, stock));
     }

     private IEnumerator DelayedBuyLimitCoroutine(decimal price, decimal volume, int subTickSteps, Stock stock)
     {
         Stock delayedStock = ResolveStock(stock);
         int generation = IntentVersion(delayedStock);
         stock = delayedStock;
         int session = MarketDayController.Session;
         yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
         { yield break; }

         BuyLimit(price, volume, subTickSteps, stock);
     }

     /// <summary>지연 후 지정가 매도. 가격과 수량을 미리 정해두고 0.5~3초 후 주문.</summary>
     protected void DelayedSellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
     {
         if (!CanSellStock(ResolveStock(stock)))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         StartCoroutine(DelayedSellLimitCoroutine(price, volume, subTickSteps, stock));
     }

     private IEnumerator DelayedSellLimitCoroutine(decimal price, decimal volume, int subTickSteps, Stock stock)
     {
         Stock delayedStock = ResolveStock(stock);
         int generation = IntentVersion(delayedStock);
         stock = delayedStock;
         int session = MarketDayController.Session;
         yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
         { yield break; }

         SellLimit(price, volume, subTickSteps, stock);
     }

     /// <summary>지연 후 공격적 지정가 매수. 스프레드가 넓으면 스프레드 안쪽 가격, 아니면 최우선 매도가에 지정가 주문.</summary>
     protected void DelayedBuyMarket(decimal volume, Stock stock = null)
     {
         if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         StartCoroutine(DelayedBuyMarketCoroutine(volume, stock));
     }

     private IEnumerator DelayedBuyMarketCoroutine(decimal volume, Stock stock)
     {
         Stock delayedStock = ResolveStock(stock);
         int generation = IntentVersion(delayedStock);
         stock = delayedStock;
         int session = MarketDayController.Session;
         yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
         { yield break; }

         Stock s = ResolveStock(stock);
         decimal price = GetSpreadSafePrice(s, true) ?? GetAggressiveLimitPrice(s, true);
         if (price > 0m)
         {
             BuyLimit(price, volume, 0, s);
         }
     }

     /// <summary>지연 후 공격적 지정가 매도. 스프레드가 넓으면 스프레드 안쪽 가격, 아니면 최우선 매수가에 지정가 주문.</summary>
     protected void DelayedSellMarket(decimal volume, Stock stock = null)
     {
         if (!CanSellStock(ResolveStock(stock)))
         { return; }

         if (!MarketDayController.CanTrade || !isActiveAndEnabled)
         { return; }

         StartCoroutine(DelayedSellMarketCoroutine(volume, stock));
     }

     private IEnumerator DelayedSellMarketCoroutine(decimal volume, Stock stock)
     {
         Stock delayedStock = ResolveStock(stock);
         int generation = IntentVersion(delayedStock);
         stock = delayedStock;
         int session = MarketDayController.Session;
         yield return new WaitForSeconds(Random.Range(orderDelayMin, orderDelayMax));
         if (!MarketDayController.CanTrade || !isActiveAndEnabled || session != MarketDayController.Session || generation != IntentVersion(delayedStock))
         { yield break; }

         Stock s = ResolveStock(stock);
         decimal price = GetSpreadSafePrice(s, false) ?? GetAggressiveLimitPrice(s, false);
         if (price > 0m)
         {
             SellLimit(price, volume, 0, s);
         }
     }
 }
```
