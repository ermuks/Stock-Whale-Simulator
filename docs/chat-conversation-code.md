# 채팅 말투·주제·입력 상태 변경 코드

이번 작업 직전 복사본을 원본으로 삼았습니다. 이전 사용자 변경분을 Git HEAD로 되돌려 비교하지 않았습니다.

## 신규 파일: Assets/Scripts/NightShift/ChatSocialVoices.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed partial class InvestorSociety
{
    private static readonly string[] VoiceKeys = { "brief", "measured", "chatty", "wry", "hesitant", "expressive" };
    private static readonly string[] VoiceLabels = { "짧고 담백함", "차분한 관찰형", "수다형", "능청형", "조심스러운 말투", "반응이 큰 말투" };
    public static bool IsVoiceKey(string key) => Array.IndexOf(VoiceKeys, key) >= 0;
    private static int VoiceIndex(InvestorPsychology mind)
    {
        // Existing saved identity/traits define a stable voice without new save fields or decision-RNG draws.
        var traits = mind.Traits;
        uint hash = unchecked((uint)mind.SocialId * 486187739u ^ (uint)(traits.Temper * 31 + traits.Grudge * 127 + traits.Sociability * 8191 + traits.Politeness * 131071));
        hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352du); hash ^= hash >> 15;
        return (int)(hash % (uint)VoiceKeys.Length);
    }
    public static string VoiceKey(InvestorPsychology mind) => VoiceKeys[VoiceIndex(mind)];
    public static string VoiceLabel(InvestorPsychology mind) => VoiceLabels[VoiceIndex(mind)];
    private static bool TryVoiceText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target,
        IEnumerable<SocialMessage> recent, out string text)
    {
        text = null;
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None ||
            message.promisedAction != SocialPromisedAction.None)
        { return false; }
        bool fomo = facts.notice == "FOMO" && message.replyToId == 0;
        if (!fomo && (!string.IsNullOrEmpty(facts.notice) || !template.key.Contains(".plain.")))
        { return false; }
        // Empathy and refusal after mediation must keep their specific parent context.
        if (parent != null && (message.act == SocialAct.AGREE && parent.act == SocialAct.LAMENT ||
            message.act == SocialAct.REBUT && parent.act == SocialAct.MEDIATE))
        { return false; }
        var random = CosmeticRandom(mind, message, 0x174c58edu);
        if (random.Next() >= ChatSocialConfig.Current.Get("voiceStyleChance"))
        { return false; }
        string voice = VoiceKey(mind), style = template.style == "formal" ? "formal" : "casual";
        string context = fomo ? "FOMO" : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Voice(message.act, context, voice, style);
        if (group == null && !fomo)
        {
            context = message.act == SocialAct.BRAG || message.act == SocialAct.LAMENT || message.act == SocialAct.ASK ||
                message.act == SocialAct.REACT || message.act == SocialAct.SMALLTALK || message.act == SocialAct.DOUBT ? "NONE" : "TARGET";
            group = ChatSocialAssets.Voice(message.act, context, voice, style);
        }
        if (group == null || !SpeechRequirementsMatch(group.requires, facts))
        { return false; }
        var choices = new List<int>();
        for (int i = 0; i < group.texts.Length; i++)
        {
            string key = group.key + "." + i;
            bool repeated = mind.RecentSpeechKeys.Contains(key);
            if (!repeated && recent != null)
            { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
            if (repeated) { continue; }
            bool complete = true;
            foreach (Match match in SlotPattern.Matches(group.texts[i]))
            { if (string.IsNullOrEmpty(SpeechSlot(facts, match.Groups[1].Value, target, 0))) { complete = false; break; } }
            if (complete) { choices.Add(i); }
        }
        if (choices.Count == 0)
        { return false; }
        int selected = choices[random.Range(0, choices.Count)];
        text = group.texts[selected]; message.debug.speechKey = group.key + "." + selected;
        return true;
    }
    internal static string VoiceStockParticles(string text, string name)
    {
        if (string.IsNullOrEmpty(name)) { return text; }
        int last = name[name.Length - 1] - 0xAC00;
        if (last < 0 || last > 11171) { return text; }
        int final = last % 28;
        if (final == 0)
        {
            text = text.Replace("{symbol}이네요", "{symbol}네요").Replace("{symbol}이 ", "{symbol}가 ")
                .Replace("{symbol}은", "{symbol}는").Replace("{symbol}을", "{symbol}를");
        }
        if (final == 0 || final == 8) { text = text.Replace("{symbol}으로", "{symbol}로"); }
        return text;
    }
}
```

## 신규 파일: Assets/Scripts/NightShift/ChatSocialTopicFocus.cs

```csharp
using System;
using System.Collections.Generic;

/// <summary>Three preferred subjects, never a hard topic ban. Recent speech decays and hot newcomers can take over.</summary>
public sealed class ChatSocialTopicFocus
{
    private sealed class Topic { public string key; public double score, at, observed; }
    private readonly Dictionary<string, Topic> topics = new();
    private readonly List<Topic> ranking = new();
    private double rankedAt = double.NegativeInfinity;
    public void Observe(string key, double now)
    {
        if (string.IsNullOrEmpty(key)) { return; }
        double memory = ChatSocialConfig.Current.Get("topicMemorySeconds");
        if (!topics.TryGetValue(key, out var topic))
        {
            if (topics.Count >= 32)
            {
                Topic oldest = null;
                foreach (var item in topics.Values) { if (oldest == null || item.observed < oldest.observed) { oldest = item; } }
                topics.Remove(oldest.key);
            }
            topic = new Topic { key = key, at = now }; topics.Add(key, topic);
        }
        topic.score = topic.score * Math.Exp(-Math.Max(0, now - topic.at) / memory) + 1;
        topic.at = topic.observed = now; rankedAt = double.NegativeInfinity;
    }
    private void Rank(double now)
    {
        if (now >= rankedAt && now - rankedAt < 1) { return; }
        rankedAt = now; ranking.Clear();
        double memory = ChatSocialConfig.Current.Get("topicMemorySeconds");
        foreach (var topic in topics.Values)
        {
            topic.score *= Math.Exp(-Math.Max(0, now - topic.at) / memory); topic.at = now;
            if (topic.score >= .1) { ranking.Add(topic); }
        }
        ranking.Sort((a, b) => { int order = b.score.CompareTo(a.score); return order != 0 ? order : string.CompareOrdinal(a.key, b.key); });
    }
    public bool IntroducingAnother(string key, double now)
    {
        Rank(now);
        int count = ChatSocialConfig.Current.Int("topicPreferredCount");
        if (ranking.Count < count || string.IsNullOrEmpty(key)) { return false; }
        for (int i = 0; i < Math.Min(count, ranking.Count); i++) { if (ranking[i].key == key) { return false; } }
        return true;
    }
    public double Weight(string key, double urgency, double now)
    {
        Rank(now);
        if (ranking.Count == 0) { return 1; }
        var config = ChatSocialConfig.Current;
        int count = Math.Min(config.Int("topicPreferredCount"), ranking.Count);
        for (int i = 0; i < count; i++) { if (ranking[i].key == key) { return config.Get("topicContinueWeight"); } }
        double quiet = config.Get("topicNewWeight");
        return quiet + Math.Clamp(urgency, 0, 1) * (config.Get("topicContinueWeight") - quiet);
    }
}

public sealed partial class InvestorSociety
{
    private MarketConversationFocus liveConversationMarket;
    private readonly Dictionary<int, ChatSocialTopicFocus> topicFocus = new();
    public bool LiveConversationEnabled => liveConversationMarket != null;
    public void ConfigureLiveConversation(MarketConversationFocus market) { liveConversationMarket = market; }
    public ChatSocialTopicFocus TopicFocus(int room)
    {
        if (!topicFocus.TryGetValue(room, out var focus)) { focus = new ChatSocialTopicFocus(); topicFocus.Add(room, focus); }
        return focus;
    }
    private double TopicUrgency(Stock stock)
    {
        if (stock == null || liveConversationMarket == null) { return 0; }
        var snapshot = liveConversationMarket.Read(stock, (float)SocialTime.Seconds);
        // Existing heat already combines actual volume and recent price movement relative to each stock's history.
        return Math.Clamp((snapshot.Probability * Math.Max(1, liveConversationMarket.Count) - 1) / 2, 0, 1);
    }
    public Stock ChooseRoomTopic(TradeBot bot, int room, double roll)
    {
        if (liveConversationMarket == null || StockManager.Instance == null) { return null; }
        var focus = TopicFocus(room);
        double total = 0;
        foreach (var stock in StockManager.Instance.stocks) { total += Weight(stock); }
        roll = Math.Clamp(double.IsNaN(roll) ? .5 : roll, 0, .999999999) * total;
        Stock last = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            double weight = Weight(stock);
            if (weight <= 0) { continue; }
            last = stock; roll -= weight;
            if (roll <= 0) { return stock; }
        }
        return last;
        double Weight(Stock stock)
        {
            if (!stock.IsListed || stock.IsDelisted) { return 0; }
            var snapshot = liveConversationMarket.Read(stock, (float)SocialTime.Seconds);
            double urgency = TopicUrgency(stock);
            return Math.Max(.0001, snapshot.Probability) * focus.Weight(stock.symbol, urgency, SocialTime.Seconds) *
                (1 + urgency * Config.Get("topicHotMultiplier"));
        }
    }
    private static SocialRandom PresentationRandom(TradeBot bot, SocialIntent intent, uint salt)
    {
        uint key = unchecked((uint)bot.Social.Id * 486187739u ^ (uint)intent.causeId ^ (uint)(long)(intent.due * 1000) ^ (uint)intent.roomId * 8191u ^ salt);
        key ^= key >> 16; key = unchecked(key * 0x7feb352du); key ^= key >> 15;
        return new SocialRandom(unchecked((int)key));
    }
    private bool HesitateOnNewTopic(TradeBot bot, SocialIntent intent)
    {
        if (LiveConversationEnabled && intent.facts == null && intent.replyToId == 0 && intent.threadId == 0 && intent.targetId == 0)
        {
            var subject = ChooseRoomTopic(bot, intent.roomId, bot.Social.Psychology.SocialRng.Next());
            if (subject != null) { intent.facts = CaptureFacts(bot, subject); }
        }
        if (!LiveConversationEnabled || intent.topicHesitated || intent.replyToId > 0 || intent.threadId > 0 || intent.targetId != 0 ||
            intent.facts == null || intent.facts.directLead || intent.facts.signal || intent.facts.notice == "WATCH_RISE" || intent.facts.notice == "WATCH_FALL")
        { return false; }
        var stock = FindSocialStock(intent.facts.symbol);
        if (stock == null || !TopicFocus(intent.roomId).IntroducingAnother(stock.symbol, SocialTime.Seconds)) { return false; }
        intent.topicHesitated = true;
        double urgency = TopicUrgency(stock);
        var random = PresentationRandom(bot, intent, 0x6715a9d3u);
        double hesitation = Config.Get("topicHesitationChance") * (1 - urgency) * (1.2 - bot.Social.Psychology.Traits.Sociability / 200d);
        if (random.Next() >= hesitation) { return false; }
        intent.due = SocialTime.Seconds + Config.Get("topicHesitationMinSec") + random.Next() * (Config.Get("topicHesitationMaxSec") - Config.Get("topicHesitationMinSec"));
        bot.Social.Psychology.Intentions.Add(intent);
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "TOPIC_HESITATION", actor = bot.Social.Id, room = intent.roomId, reason = stock.symbol, value = intent.due - SocialTime.Seconds });
        return true;
    }
    private void ObserveConversationTopic(SocialMessage message)
    {
        if (!LiveConversationEnabled) { return; }
        string key = message.act == SocialAct.SMALLTALK ? "__smalltalk" : message.facts?.symbol;
        TopicFocus(message.roomId).Observe(key, SocialTime.Seconds);
    }
}
```

## 신규 파일: Assets/Scripts/NightShift/ChatSocialTyping.cs

```csharp
using System;
using System.Collections.Generic;

public readonly struct SocialTypingStatus
{
    public readonly int Count;
    public readonly string Nickname;
    public SocialTypingStatus(int count, string nickname) { Count = count; Nickname = nickname; }
    public string Text => Count == 0 ? "" : Count == 1 ? Nickname + "님이 채팅 입력 중..." : Count + "명이 입력 중...";
}

public sealed partial class InvestorSociety
{
    private sealed class TypingEntry
    {
        public TradeBot bot;
        public SocialIntent intent;
        public double ready;
    }
    private readonly List<TypingEntry> typing = new();
    public int TypingVersion { get; private set; }
    private bool IsTyping(TradeBot bot)
    {
        foreach (var entry in typing) { if (entry.bot == bot) { return true; } }
        return false;
    }
    public SocialTypingStatus ReadTyping(int room)
    {
        int count = 0; string name = null;
        foreach (var entry in typing)
        {
            if (entry.intent.roomId != room || entry.intent.expires < SocialTime.Seconds || !Present(entry.bot, room)) { continue; }
            count++; name = entry.bot.Social.Nickname;
        }
        return new SocialTypingStatus(count, count == 1 ? name : null);
    }
    private bool BeginTyping(TradeBot bot, SocialIntent intent)
    {
        if (!LiveConversationEnabled) { return false; }
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent)) { return true; }
        if (ReadTyping(intent.roomId).Count >= Config.Int("typingPerRoom"))
        { intent.due = Math.Max(intent.due, SocialTime.Seconds + .25); bot.Social.Psychology.Intentions.Add(intent); return true; }
        var random = PresentationRandom(bot, intent, 0x461b82a7u);
        double duration = Config.Get("typingMinSec") + random.Next() * (Config.Get("typingMaxSec") - Config.Get("typingMinSec"));
        typing.Add(new TypingEntry { bot = bot, intent = intent, ready = SocialTime.Seconds + duration }); TypingVersion++;
        return true;
    }
    private void FlushTyping()
    {
        if (!LiveConversationEnabled || typing.Count == 0) { return; }
        double now = SocialTime.Seconds;
        for (int i = 0; i < typing.Count;)
        {
            var entry = typing[i]; var intent = entry.intent;
            var stock = FindSocialStock(intent.facts?.symbol);
            if (intent.expires < now) { ExpireSocialIntent(entry.bot, intent); }
            if (intent.expires < now || !Present(entry.bot, intent.roomId) || !ValidateSocialIntent(entry.bot, intent) || stock != null && (!stock.IsListed || stock.IsDelisted))
            { typing.RemoveAt(i); TypingVersion++; continue; }
            if (entry.ready > now) { i++; continue; }
            var room = SocialRoom(intent.roomId);
            while (room.sent.Count > 0 && now - room.sent.Peek() >= Config.Get("roomWindowSec")) { room.sent.Dequeue(); }
            if (room.sent.Count >= Config.Int("roomMaxMsgPer10Sec")) { i++; continue; }
            typing.RemoveAt(i); TypingVersion++;
            // Portfolio facts and target availability are checked again at actual publication.
            GenerateSocialMessage(entry.bot, intent);
        }
    }
}
```

## 신규 파일: Assets/Scripts/MarketDay/ChatStockLinkUI.cs

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Uses the existing World Space event camera and lets parent ScrollRect handle dragging.</summary>
public sealed class ChatStockLinkUI : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private TMP_Text label;
    private Action<string> activate;
    private Action fallback;
    public void Bind(TMP_Text text, Action<string> onLink, Action onText)
    { label = text; activate = onLink; fallback = onText; }
    // Claim the press so the ancestor message Button cannot steal it before this link's click handler.
    public void OnPointerDown(PointerEventData eventData) { }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || !eventData.eligibleForClick || label == null)
        { return; }
        int index = TMP_TextUtilities.FindIntersectingLink(label, eventData.position, eventData.pressEventCamera);
        if (index >= 0) { activate?.Invoke(label.textInfo.linkInfo[index].GetLinkID()); }
        else { fallback?.Invoke(); }
    }
}
```

## 신규 파일: Assets/Editor/ConversationFlowSmoke.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Optional short, real-time smoke. Never queues speech or changes bot emotions, market prices, or orders.
public static partial class AutonomousChatObservation
{
    [Serializable] private sealed class ConversationSmokeResult
    {
        public int maxTyping, typingSamples, stockLinkCount, selectionEvents;
        public bool singleTyping, multipleTyping, indicatorMatched, dragIgnored, clickSelected, chartSelected, pressRouted, scrollRouted;
        public string selectedSymbol, expectedChartName, chartName;
        public float indicatorFontSize, indicatorHeight, viewportBottom;
        public string limitation = "Headless geometry/callback test; no rendered visual readability or physical mouse test.";
    }
    private static int conversationSmokeSeconds;
    private static readonly ConversationSmokeResult conversationResult = new();
    private static readonly List<string> typingTrace = new();
    private static float nextConversationSample, verifySelectionAt;
    private static int previousTyping = -1;
    private static string previousTypingName;
    private static TabletGroupChatUI chatUi;
    private static TextMeshProUGUI typingText;
    private static Stock restoreStock;
    private static bool checkedLink;
    private static void InitializeConversationSmoke()
    {
        int.TryParse(Environment.GetEnvironmentVariable("DONDON_CHAT_SMOKE_SECONDS"), out conversationSmokeSeconds);
        if (conversationSmokeSeconds < 0 || conversationSmokeSeconds > 300) { throw new ArgumentOutOfRangeException("DONDON_CHAT_SMOKE_SECONDS"); }
        if (conversationSmokeSeconds > 0) { status.mode += "; short UI stock-link callback probe, selection restored"; }
    }
    private static void SampleConversationSmoke()
    {
        if (conversationSmokeSeconds == 0 || society == null || readyAt < 0 || Time.unscaledTime < nextConversationSample) { return; }
        nextConversationSample = Time.unscaledTime + .25f;
        var typingStatus = society.ReadTyping(0);
        conversationResult.maxTyping = Math.Max(conversationResult.maxTyping, typingStatus.Count);
        conversationResult.typingSamples++;
        conversationResult.singleTyping |= typingStatus.Count == 1;
        conversationResult.multipleTyping |= typingStatus.Count > 1;
        if (previousTyping != typingStatus.Count || previousTypingName != typingStatus.Nickname)
        {
            typingTrace.Add(FormattableString.Invariant($"{Time.time:F2} | {typingStatus.Count} | {typingStatus.Text}"));
            previousTyping = typingStatus.Count; previousTypingName = typingStatus.Nickname;
        }
        if (chatUi == null)
        {
            foreach (var ui in UnityEngine.Object.FindObjectsByType<TabletGroupChatUI>(FindObjectsInactive.Exclude))
            { if (ui.monitorChat && ui.SelectedRoom == 0) { chatUi = ui; break; } }
            if (chatUi == null) { return; }
            foreach (var label in chatUi.GetComponentsInChildren<TextMeshProUGUI>(true))
            { if (label.name == "Text :: Chat Typing") { typingText = label; break; } }
        }
        if (typingText != null)
        {
            conversationResult.indicatorMatched |= typingStatus.Count > 0 && typingText.text == typingStatus.Text;
            conversationResult.indicatorFontSize = typingText.fontSize;
            conversationResult.indicatorHeight = typingText.rectTransform.rect.height;
            conversationResult.viewportBottom = chatUi.scroll.viewport.offsetMin.y;
        }
        if (verifySelectionAt > 0 && Time.unscaledTime >= verifySelectionAt)
        {
            var chart = UnityEngine.Object.FindAnyObjectByType<ChartManager>();
            conversationResult.chartName = chart?.textName?.text;
            conversationResult.chartSelected = conversationResult.chartName == conversationResult.expectedChartName;
            verifySelectionAt = 0;
            int index = StockManager.Instance.stocks.IndexOf(restoreStock);
            if (index >= 0) { StockManager.Instance.SelectStock(index); }
        }
        if (!checkedLink) { ProbeStockLink(); }
        File.WriteAllText(Path.Combine(folder, "conversation-smoke.json"), JsonUtility.ToJson(conversationResult, true));
        File.WriteAllLines(Path.Combine(folder, "typing.txt"), typingTrace);
    }
    private static void ProbeStockLink()
    {
        foreach (var linkHandler in chatUi.GetComponentsInChildren<ChatStockLinkUI>())
        {
            var label = linkHandler.GetComponent<TMP_Text>(); label.ForceMeshUpdate();
            for (int i = 0; i < label.textInfo.linkCount; i++)
            {
                var link = label.textInfo.linkInfo[i];
                if (!ChatSocialMentions.TryStockSymbol(link.GetLinkID(), out string symbol)) { continue; }
                var manager = StockManager.Instance;
                var target = manager.stocks.Find(stock => stock.symbol == symbol);
                if (target == null || target == manager.CurrentStock || link.linkTextLength == 0) { continue; }
                var raycaster = label.GetComponentInParent<GraphicRaycaster>();
                if (raycaster == null || raycaster.eventCamera == null) { continue; }
                var glyph = label.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
                var point = label.transform.TransformPoint((glyph.bottomLeft + glyph.topRight) * .5f);
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, eligibleForClick = true,
                    position = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, point), pointerPressRaycast = new RaycastResult { module = raycaster }, dragging = true };
                restoreStock = manager.CurrentStock;
                linkHandler.OnPointerClick(pointer);
                conversationResult.dragIgnored = manager.CurrentStock == restoreStock;
                void Selected(Stock stock) { conversationResult.selectionEvents++; }
                StockManager.OnStockSelected += Selected;
                pointer.dragging = false;
                var pressed = ExecuteEvents.ExecuteHierarchy(label.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                conversationResult.pressRouted = pressed == linkHandler.gameObject && ExecuteEvents.GetEventHandler<IPointerClickHandler>(label.gameObject) == pressed;
                conversationResult.scrollRouted = ExecuteEvents.GetEventHandler<IDragHandler>(label.gameObject) == chatUi.scroll.gameObject;
                if (conversationResult.pressRouted) { ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler); }
                StockManager.OnStockSelected -= Selected;
                conversationResult.clickSelected = manager.CurrentStock == target;
                conversationResult.selectedSymbol = symbol; conversationResult.expectedChartName = target.stockName;
                conversationResult.stockLinkCount = label.textInfo.linkCount;
                checkedLink = true; verifySelectionAt = Time.unscaledTime + .5f;
                return;
            }
        }
    }
    private static string ConversationSmokeFailure()
    {
        return conversationResult.singleTyping && conversationResult.multipleTyping && conversationResult.indicatorMatched &&
            conversationResult.pressRouted && conversationResult.scrollRouted && conversationResult.dragIgnored && conversationResult.clickSelected && conversationResult.chartSelected ? null : "Short live conversation smoke incomplete; see conversation-smoke.json.";
    }
}
```

## 신규 파일: Tests/ChatSocial/TestsConversationFlow.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void ConversationFlowScenarios()
    {
        ChatSocialAssets.Reload();
        StockLinkScenarios(); TopicFlowScenarios(); TypingScenarios();
    }
    private static void StockLinkScenarios()
    {
        var formatter = new ChatSocialMentions(new[] { "개미왕", "NM" }, new[] {
            new KeyValuePair<string, string>("노보손해보험", "NM"), new KeyValuePair<string, string>("NM", "NM"),
            new KeyValuePair<string, string>("미래바이오", "MBI"), new KeyValuePair<string, string>("미래바이오홀딩스", "MBIH") });
        string text = formatter.Format("개미왕님 노보손해보험은 NM보다 미래바이오홀딩스 보고 있음");
        Check(text.Split("<link=").Length - 1 == 3 && text.Contains(ChatSocialMentions.MarkOpen + "개미왕</mark>") && text.Contains("stock:TUJJSA=="), "Names/tickers link alongside nickname marks; longest actual stock name wins");
        Check(formatter.Format("@NM NM") == ChatSocialMentions.MarkOpen + "@NM</mark> <link=\"stock:Tk0=\">" + ChatSocialMentions.StockMarkOpen + "NM</color></mark></link>", "Explicit at-mention stays a nickname even if it matches a ticker");
        Check(formatter.Format("NMZ 노보손해보험사") == "NMZ 노보손해보험사", "Partial or unknown stock names do not become clickable");
        string injected = formatter.Format("<link=\"stock:Tk0=\">fake</link> 노보손해보험");
        Check(injected.Split("<link=").Length - 1 == 1 && injected.Contains("<noparse><</noparse>link="), "Only roster-generated links are executable; chat markup is quoted");
        Check(ChatSocialMentions.TryStockSymbol("stock:Tk0=", out string symbol) && symbol == "NM", "Stock link restores the exact symbol");
        Check(!ChatSocialMentions.TryStockSymbol("stock:!bad!", out _) && !ChatSocialMentions.TryStockSymbol("stock:/w==", out _) &&
            !ChatSocialMentions.TryStockSymbol("http:Tk0=", out _) && !ChatSocialMentions.TryStockSymbol("stock:", out _), "Invalid link/base64/UTF8/empty symbol safely rejected");
    }
    private static void TopicFlowScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        while (StockManager.Instance.stocks.Count < 8)
        { int id = StockManager.Instance.stocks.Count; StockManager.Instance.stocks.Add(new Stock { symbol = "S" + id, stockName = "종목" + id }); }
        double minute = 0;
        var market = new MarketConversationFocus { GameMinutes = () => minute };
        var stocks = StockManager.Instance.stocks;
        for (int t = 0; t < 60; t++)
        {
            minute = t;
            foreach (var stock in stocks) { market.Record(stock, 1000, 100, t); }
            market.Read(stocks[0], t);
        }
        society.ConfigureLiveConversation(market); society.SocialTime.Set(1, minute, 60);
        var focus = society.TopicFocus(0);
        for (int i = 0; i < 3; i++) { for (int j = 0; j < 8; j++) { focus.Observe(stocks[i].symbol, 60); } }
        Check(!focus.IntroducingAnother("S2", 60) && focus.IntroducingAnother("S3", 60), "Three familiar subjects encourage hesitation for a fourth without banning it");
        Check(focus.Weight("S3", 0, 60) > 0 && focus.Weight("S3", 1, 60) == focus.Weight("S0", 0, 60), "Every new topic keeps a chance; a hot newcomer can match a familiar topic");
        var bot = society.SocialBots[1]; int familiar = 0, outside = 0;
        for (int i = 0; i < 10000; i++)
        { var chosen = society.ChooseRoomTopic(bot, 0, (i + .5) / 10000); if (stocks.IndexOf(chosen) < 3) { familiar++; } else { outside++; } }
        Check(familiar >= 8500 && outside > 0, $"Soft focus weighted choices: familiar={familiar}, other={outside}/10000 (not a three-topic cap)");
        Check(!society.TopicFocus(1).IntroducingAnother("S3", 60) && society.TopicFocus(1).Weight("S3", 0, 60) == 1, "Each room maintains independent conversational attention");
        minute = 61;
        foreach (var stock in stocks) { market.Record(stock, stock == stocks[3] ? 1500 : 1000, stock == stocks[3] ? 3000 : 100, 63); }
        society.SocialTime.Set(1, minute, 63); int hot = 0;
        for (int i = 0; i < 10000; i++) { if (society.ChooseRoomTopic(bot, 0, (i + .5) / 10000) == stocks[3]) { hot++; } }
        Check(hot > outside * 2 && market.Read(stocks[3], 63).ReturnPercent == 50, $"Actual volume/price shock brings a new stock into attention: {hot}/10000 choices");
        var quietIntent = new SocialIntent { roomId = 0, due = 63, expires = 100, causeId = 1, facts = society.CaptureFacts(bot, stocks[4]) };
        bool delayed = false;
        for (int cause = 1; cause <= 30 && !delayed; cause++)
        {
            quietIntent.causeId = cause; quietIntent.topicHesitated = false;
            delayed = (bool)InvokeSociety(society, "HesitateOnNewTopic", bot, quietIntent);
        }
        Check(delayed && quietIntent.due >= 65 && quietIntent.due <= 70 && quietIntent.expires == 100 &&
            !(bool)InvokeSociety(society, "HesitateOnNewTopic", bot, quietIntent), "New-topic hesitation delays once, does not silently cancel or extend event lifetime");
        var shock = new SocialIntent { roomId = 0, facts = society.CaptureFacts(bot, stocks[4]) }; shock.facts.notice = "WATCH_RISE";
        Check(!(bool)InvokeSociety(society, "HesitateOnNewTopic", bot, shock), "Personal sudden-move observation can interrupt a quiet conversation");
        Check(!focus.IntroducingAnother("S7", 2000) && focus.Weight("S7", 0, 2000) == 1, "Old focus decays completely so yesterday's topics do not freeze the room");
        Console.WriteLine($"Focus measurement: familiar={familiar/100d:F2}%, outside={outside/100d:F2}%, new hot stock={hot/100d:F2}%");
    }
    private static void TypingScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        var market = new MarketConversationFocus(); foreach (var stock in StockManager.Instance.stocks) { market.Register(stock); }
        society.ConfigureLiveConversation(market); society.SocialTime.Set(1, 600, 100);
        var a = society.SocialBots[1]; var b = society.SocialBots[2]; var stockA = StockManager.Instance.stocks[1];
        SocialIntent Intent(TradeBot bot, int room = 1) => new SocialIntent { roomId = room, due = society.SocialTime.Seconds,
            expires = society.SocialTime.Seconds + 60, motive = SocialMotive.SELF_EVENT, forcedAct = SocialAct.REACT, facts = society.CaptureFacts(bot, stockA) };
        InvokeSociety(society, "BeginTyping", a, Intent(a));
        Check(replay.Messages.Count == 0 && society.ReadTyping(1).Count == 1 && society.ReadTyping(1).Text == a.Social.Nickname + "님이 채팅 입력 중...", "Single typing indicator precedes any actual publication");
        for (int id = 2; id <= 4; id++) { InvokeSociety(society, "BeginTyping", society.SocialBots[id], Intent(society.SocialBots[id])); }
        Check(society.ReadTyping(1).Count == 4 && society.ReadTyping(1).Text == "4명이 입력 중..." && society.ReadTyping(0).Text == "", "Multiple actual waiting authors use the count; another room stays empty");
        InvokeSociety(society, "BeginTyping", society.SocialBots[5], Intent(society.SocialBots[5]));
        Check(society.ReadTyping(1).Count == 4 && society.SocialBots[5].Social.Psychology.Intentions.Count == 1, "Typing queue is bounded and overflow stays an intention");
        stockA.currentPrice += 321;
        society.SocialTime.Set(1, 601, 100.2); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 0, "Typing is a real delay rather than a label shown after sending");
        society.SocialTime.Set(1, 602, 103); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 2 && replay.Messages.All(m => m.facts.price == InvestorSociety.Money(stockA.currentPrice)) &&
            society.ReadTyping(1).Count == 2, "Publication refreshes live facts and honors existing two-per-ten-second quota");
        society.SocialTime.Set(1, 603, 114); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 4 && society.ReadTyping(1).Count == 0 && society.ReadTyping(1).Text == "", "Waiting authors send in a later quota window and the indicator clears");
        int published = replay.Messages.Count;
        InvokeSociety(society, "BeginTyping", a, Intent(a)); a.Social.Psychology.Room = 0;
        InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Leaving the room cancels in-progress typing without a ghost message");
        a.Social.Psychology.Room = 1; InvokeSociety(society, "BeginTyping", a, Intent(a));
        a.Social.Psychology.MutedUntil[1] = 700; InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Muted author disappears from the waiting indicator");
        a.Social.Psychology.MutedUntil.Clear(); var expiring = Intent(a); expiring.expires = 114.1;
        InvokeSociety(society, "BeginTyping", a, expiring); society.SocialTime.Set(1, 604, 115); InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Expired facts do not publish because they had begun typing");
        InvokeSociety(society, "BeginTyping", b, Intent(b)); stockA.IsDelisted = true; InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Delisting invalidates a queued stock conversation");
        var live = SocialReplay(901, 8, steps: 0); var s = live.Society; var leader = s.SocialBots[1];
        s.ConfigureLiveConversation(new MarketConversationFocus());
        s.Offer(leader, 1, SocialMotive.SELF_EVENT, s.CaptureFacts(leader, StockManager.Instance.stocks[0]), SocialAct.REACT, cause: 44);
        s.SocialAdvance(1, 540, 0, 20);
        Check(live.Messages.All(m => m.senderId != leader.Social.Id) && s.ReadTyping(1).Count > 0, "Real production scheduler routes selected utterances through typing");
        s.SocialAdvance(1, 541, 3, 20);
        Check(live.Messages.Any(m => m.senderId == leader.Social.Id), "Production scheduler publishes the prepared utterance on a later tick");
    }
    private static void VoiceVarietyScenarios()
    {
        ChatSocialAssets.Reload();
        var config = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var templates = ChatSocialAssets.Templates;
        Check(templates.voices.Length == 60 && templates.voices.Sum(v => v.texts.Length) == 240 && templates.voices.All(v => v.texts.Distinct().Count() == 4), "Six voices cover both formality levels and five contexts with 240 phrases");
        var minds = Enumerable.Range(1, 120).Select(id => new InvestorPsychology(id, 73129)).ToArray();
        Check(minds.Select(InvestorSociety.VoiceKey).Distinct().Count() == 6, "Bot population contains all six stable speaking styles");
        Check(InvestorSociety.VoiceStockParticles("{symbol}이 계속 신경 쓰임. 결국 {symbol}으로 돌아옴", "미래바이오") ==
            "{symbol}가 계속 신경 쓰임. 결국 {symbol}로 돌아옴", "Vowel-ending stock name uses correct subject/direction particles");
        Check(InvestorSociety.VoiceStockParticles("{symbol}은 {symbol}을 {symbol}이네요", "미래바이오") == "{symbol}는 {symbol}를 {symbol}네요" &&
            InvestorSociety.VoiceStockParticles("{symbol}은 {symbol}을 {symbol}으로", "노보손해보험") == "{symbol}은 {symbol}을 {symbol}으로" &&
            InvestorSociety.VoiceStockParticles("{symbol}으로", "서울") == "{symbol}로", "Topic/object/copula particles retain consonants and handle final rieul");
        bool stable = true, guard = true; var samples = new List<string>();
        var fixture = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(config));
        fixture.values.Single(v => v.key == "communityPlayfulChance").value = 0;
        fixture.values.Single(v => v.key == "communitySeriousChance").value = 0;
        ChatSocialAssets.Install(fixture, acts, templates);
        try
        {
            foreach (var mind in minds)
            {
                uint before = mind.SocialRng.State;
                string key = InvestorSociety.VoiceKey(mind);
                var copy = new InvestorPsychology(mind.SocialId, 999); copy.RestoreSocial(mind.CaptureSocial());
                stable &= key == InvestorSociety.VoiceKey(copy) && before == mind.SocialRng.State;
                var message = VoiceMessage(mind, 90000 + mind.SocialId, SocialAct.REACT); message.facts.notice = "FOMO";
                string text = InvestorSociety.RenderSocialText(mind, VoiceTemplate("REACT"), message, null, "");
                guard &= message.debug.speechKey.StartsWith("voice.") && message.debug.speechVoice == InvestorSociety.VoiceLabel(mind) && !text.Contains("{") && !text.Contains("ㅋㅋ") && !text.Contains("ㄷㄷ");
                if (!samples.Any(row => row.StartsWith(key + " |"))) { samples.Add(key + " | " + text); }
                string prior = message.debug.speechKey; mind.RecentSpeechKeys.Enqueue(prior);
                InvestorSociety.RenderSocialText(mind, VoiceTemplate("REACT"), message, null, "");
                guard &= prior != message.debug.speechKey;
            }
            Check(stable, "Voice remains stable through save/load and never consumes decision RNG");
            Check(guard, "FOMO expressions resolve facts, avoid recent wording and keep serious speech free of meme fillers");
        }
        finally { ChatSocialAssets.Install(config, acts, templates); }
        var on = SocialReplay(73129, 8, steps: 1800);
        var disabled = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(config)); disabled.values.Single(v => v.key == "voiceStyleChance").value = 0;
        ChatSocialAssets.Install(disabled, acts, templates);
        var off = SocialReplay(73129, 8, steps: 1800);
        Check(on.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.templateKey}:{m.targetId}:{m.threadId}").SequenceEqual(off.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.templateKey}:{m.targetId}:{m.threadId}")) &&
            on.Journal.Changes.Select(JsonUtility.ToJson).SequenceEqual(off.Journal.Changes.Select(JsonUtility.ToJson)), "Phrase variety changes wording while keeping decision/emotion replay identical");
        ChatSocialAssets.Install(config, acts, templates);
        var malformed = JsonUtility.FromJson<SocialTemplateDocument>(JsonUtility.ToJson(templates)); malformed.voices[0].voice = "unknown";
        bool rejected = false;
        try { ChatSocialAssets.Install(config, acts, malformed); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ReferenceEquals(ChatSocialAssets.Templates, templates), "Invalid voice catalog cannot replace installed assets");
        string output = Path.Combine(root, "Validation/ChatVoiceVariety"); Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "renderer-samples.txt"), samples, new UTF8Encoding(false));
    }
}
```

## 수정 함수 비교: Assets/Scripts/MarketDay/TabletGroupChatUI.cs

### Start

기존 풀 생성·닉네임 버튼·알림 띠를 유지하고 본문별 링크 입력 처리기와 고정 입력 상태 푸터를 한 번 연결합니다.

**원본**

```csharp
    void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        messageMeasure.richText = true;
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            row.Click = rect.GetComponent<Button>();
            row.Click.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; row.AngerDot.text = "●";
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

**수정본**

```csharp
    void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        messageMeasure.richText = true;
        BuildTypingIndicator();
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            row.Click = rect.GetComponent<Button>();
            row.Click.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; row.AngerDot.text = "●";
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            row.Text.raycastTarget = true;
            var stockLinks = row.Text.gameObject.AddComponent<ChatStockLinkUI>();
            stockLinks.Bind(row.Text, SelectMentionedStock, () => row.Click.onClick.Invoke());
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

### EnsureMentionRoster

닉네임뿐 아니라 현재 상장 종목명/심볼을 표시 트리에 넣습니다. 명단·상장 변경 때만 문자열/높이 캐시를 무효화합니다.

**원본**

```csharp
    private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && mentionSociety == society && mentionBotCount == count)
        { return; }
        var names = new List<string> { "방장" };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); } }
        mentions = new ChatSocialMentions(names); mentionSociety = society; mentionBotCount = count;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
```

**수정본**

```csharp
    private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        var stocks = StockManager.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && !mentionRosterDirty && mentionSociety == society && mentionBotCount == count && mentionStockManager == stocks)
        { return; }
        var names = new List<string> { "방장" };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); } }
        var aliases = new List<KeyValuePair<string, string>>();
        if (stocks != null)
        {
            foreach (var stock in stocks.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
                aliases.Add(new KeyValuePair<string, string>(stock.stockName, stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(stock.symbol, stock.symbol));
            }
        }
        mentions = new ChatSocialMentions(names, aliases); mentionSociety = society; mentionBotCount = count;
        mentionStockManager = stocks; mentionRosterDirty = false;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
```

### Update

기존 가상 행·스크롤 갱신 전에 0.15초 주기의 작은 입력 상태 표시만 갱신합니다.

**원본**

```csharp
    void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

**수정본**

```csharp
    void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

### OnEnable

상장·상폐 이벤트를 구독하여 매 프레임 종목 명단을 재구축하지 않게 합니다.

**원본**

```csharp
    void OnEnable()
    {
        dirty = true;
        lastSecond = -1;
    }
```

**수정본**

```csharp
    void OnEnable()
    {
        dirty = true;
        lastSecond = -1;
        mentionRosterDirty = true;
        StockManager.OnStockListed += InvalidateMentionRoster;
        StockManager.OnStockDelisted += InvalidateMentionRoster;
    }
```

### OnDisable

추가한 상장 이벤트 구독을 해제합니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    void OnDisable()
    {
        StockManager.OnStockListed -= InvalidateMentionRoster;
        StockManager.OnStockDelisted -= InvalidateMentionRoster;
    }
```

### InvalidateMentionRoster

명단 변경을 플래그로 남기고 다음 표시 갱신 때 처리합니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    private void InvalidateMentionRoster(Stock stock) { mentionRosterDirty = true; }
```

### SelectMentionedStock

실제 등록된 심볼을 확인하고 기존 StockManager.SelectStock을 호출합니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    public void SelectMentionedStock(string link)
    {
        if (!ChatSocialMentions.TryStockSymbol(link, out string symbol)) { return; }
        var manager = StockManager.Instance;
        if (manager == null) { return; }
        for (int i = 0; i < manager.stocks.Count; i++)
        {
            var stock = manager.stocks[i];
            if (stock.symbol == symbol && stock.IsListed && !stock.IsDelisted)
            { manager.SelectStock(i); return; }
        }
    }
```

### BuildTypingIndicator

목록 아래 고정 푸터를 확보하고 기존 TMP SDF 폰트를 사용합니다. 최신 이동 버튼도 같은 높이만큼 올립니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    private void BuildTypingIndicator()
    {
        float height = 44f * layoutScale;
        // Reserve a fixed footer once; typing never resizes the pooled message content.
        scroll.viewport.offsetMin += new Vector2(0, height);
        ((RectTransform)newestButton.transform).anchoredPosition += new Vector2(0, height);
        var item = new GameObject("Text :: Chat Typing", typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = scroll.gameObject.layer; item.transform.SetParent(scroll.transform, false);
        typingLabel = item.GetComponent<TextMeshProUGUI>();
        typingLabel.font = messageMeasure.font; typingLabel.fontSize = 22f * layoutScale;
        typingLabel.fontStyle = FontStyles.Normal; typingLabel.color = new Color(.60f, .66f, .71f, 1f);
        typingLabel.raycastTarget = false; typingLabel.richText = false; typingLabel.enableAutoSizing = false;
        typingLabel.textWrappingMode = TextWrappingModes.NoWrap; typingLabel.overflowMode = TextOverflowModes.Ellipsis;
        typingLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var rect = typingLabel.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
        rect.offsetMin = new Vector2(18f * layoutScale, 0); rect.offsetMax = new Vector2(-18f * layoutScale, height);
        typingLabel.text = "";
    }
```

### RefreshTypingIndicator

현재 읽는 방의 실제 대기자를 표시하며 장 중이 아니거나 읽을 수 없는 방에서는 비웁니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    private void RefreshTypingIndicator()
    {
        if (typingLabel == null || Time.unscaledTime < nextTypingRefresh) { return; }
        nextTypingRefresh = Time.unscaledTime + .15f;
        var society = InvestorSociety.Instance;
        string text = chatSelected && CanReadRoom && MarketDayController.CanTrade && society != null ? society.ReadTyping(SelectedRoom).Text : "";
        DeviceTerminalUI.Set(typingLabel, text);
    }
```

## 수정 함수 비교: Assets/Scripts/NightShift/ChatSocialConfig.cs

### Validate

주제 가중치·입력 대기 시간의 양수/정수/확률/최솟값·최댓값 관계를 검사합니다.

**원본**

```csharp
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
        foreach (string key in new[] { "hotWindowMinutes", "hotBaselineMinutes", "hotReturnDivisor", "replyExpiry", "cacheTrimSeconds", "cacheGraceSeconds", "logFlushSeconds", "watchMovePercent", "watchObservationMinMinutes", "watchObservationMaxMinutes", "watchMaximumGapMinutes", "watchReactionCooldownMinutes", "watchRetargetMinutes" })
        { if (Get(key) <= 0) { throw new ArgumentException("Expected positive social setting: " + key); } }
        foreach (string key in new[] { "suffixChance", "witnessProbability", "hotDwellFloor", "hotReentryScale", "seriousSpeechChance", "communityPlayfulChance", "communitySeriousChance", "colloquialNumberChance", "seriousColloquialNumberChance", "mediationExitBaseChance", "mediationExitMaxChance", "mediationTauntChance", "departureReactionChance" })
        { if (Get(key) < 0 || Get(key) > 1) { throw new ArgumentException("Expected probability: " + key); } }
        foreach (string key in new[] { "closedThreadCache", "logCapacity", "memoryCapacity", "roomRecentTemplates", "hotListCount" })
        { if (Get(key) < 1 || Get(key) != Math.Floor(Get(key))) { throw new ArgumentException("Expected positive count: " + key); } }
        if (Get("hotBaselineMinutes") < Get("hotWindowMinutes") || Get("replyDelayMin") > Get("replyDelayMax") || Get("watchObservationMinMinutes") > Get("watchObservationMaxMinutes") || Get("watchMaximumGapMinutes") < Get("watchObservationMaxMinutes") ||
            Get("angerStage1") >= Get("angerStage2") || Get("angerStage2") >= Get("angerStage3") || Get("angerStage3") >= Get("angerStage4") || Get("angerStage4") > 100)
        { throw new ArgumentException("Invalid social window/threshold ordering"); }
        if (Get("mediationStressMin") < 0 || Get("mediationStressMin") >= 100 || Get("mediationExitBaseChance") > Get("mediationExitMaxChance") ||
            Get("mediationExitDelayMinSec") < 0 || Get("mediationExitDelayMaxSec") < Get("mediationExitDelayMinSec") || Get("mediationRejoinMinutes") <= 0 ||
            Get("mediationTauntBravadoMin") < 0 || Get("mediationTauntBravadoMin") > 100 || Get("mediationTauntAngerMin") < 0 || Get("mediationTauntAngerMin") > 100)
        { throw new ArgumentException("Invalid mediation reaction settings"); }
    }
```

**수정본**

```csharp
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
    }
```

## 수정 함수 비교: Assets/Scripts/NightShift/ChatSocialAssets.cs

### Install

새 말투 자료를 독립 인덱스로 검증하고 기존 카탈로그와 함께 원자적으로 설치합니다.

**원본**

```csharp
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
        var nextCommunity = new Dictionary<(string, string), SocialCommunityPhrases>();
        if (templates.community != null)
        {
            var communityKeys = new HashSet<string>();
            foreach (var group in templates.community)
            {
                if (group == null || string.IsNullOrEmpty(group.key) || !communityKeys.Add(group.key) || !Enum.TryParse<SocialAct>(group.act, out _) ||
                    string.IsNullOrEmpty(group.context) || group.texts == null || group.texts.Length < 6 || nextCommunity.ContainsKey((group.act, group.context)))
                { throw new ArgumentException("Invalid community phrase group"); }
                var unique = new HashSet<string>();
                foreach (string text in group.texts)
                { if (string.IsNullOrWhiteSpace(text) || !unique.Add(text)) { throw new ArgumentException("Empty/duplicate community phrase"); } }
                nextCommunity.Add((group.act, group.context), group);
            }
        }
        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; templateIndex = nextIndex; communityIndex = nextCommunity; Generation++;
    }
```

**수정본**

```csharp
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
        var nextCommunity = new Dictionary<(string, string), SocialCommunityPhrases>();
        if (templates.community != null)
        {
            var communityKeys = new HashSet<string>();
            foreach (var group in templates.community)
            {
                if (group == null || string.IsNullOrEmpty(group.key) || !communityKeys.Add(group.key) || !Enum.TryParse<SocialAct>(group.act, out _) ||
                    string.IsNullOrEmpty(group.context) || group.texts == null || group.texts.Length < 6 || nextCommunity.ContainsKey((group.act, group.context)))
                { throw new ArgumentException("Invalid community phrase group"); }
                var unique = new HashSet<string>();
                foreach (string text in group.texts)
                { if (string.IsNullOrWhiteSpace(text) || !unique.Add(text)) { throw new ArgumentException("Empty/duplicate community phrase"); } }
                nextCommunity.Add((group.act, group.context), group);
            }
        }
        var nextVoices = new Dictionary<(string, string, string, string), SocialVoicePhrases>();
        if (templates.voices != null)
        {
            var voiceKeys = new HashSet<string>();
            foreach (var group in templates.voices)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.key) || !voiceKeys.Add(group.key) || !Enum.TryParse<SocialAct>(group.act, out _) ||
                    string.IsNullOrWhiteSpace(group.context) || !InvestorSociety.IsVoiceKey(group.voice) || group.style != "formal" && group.style != "casual" ||
                    group.texts == null || group.texts.Length < 4 || nextVoices.ContainsKey((group.act, group.context, group.voice, group.style)))
                { throw new ArgumentException("Invalid voice phrase group"); }
                var unique = new HashSet<string>();
                foreach (string text in group.texts)
                { if (string.IsNullOrWhiteSpace(text) || !unique.Add(text)) { throw new ArgumentException("Empty/duplicate voice phrase"); } }
                nextVoices.Add((group.act, group.context, group.voice, group.style), group);
            }
        }
        ChatSocialConfig.Install(config); Acts = acts; Templates = templates; templateIndex = nextIndex; communityIndex = nextCommunity; voiceIndex = nextVoices; Generation++;
    }
```

## 수정 함수 비교: Assets/Scripts/NightShift/ChatSocialPipeline.cs

### SocialAdvance

실제 입력 대기 처리와 새 주제의 일회 망설임을 기존 우선순위·방별 발화 제한에 연결합니다.

**원본**

```csharp
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
```

**수정본**

```csharp
    public void SocialAdvance(int dayNumber, double gameMinute, double scaledSeconds, int budget)
    {
        SocialTime.Set(dayNumber, gameMinute, scaledSeconds); ChatSocialAudit.GameMinute = SocialTime.GameMinute;
        if (!SocialReady)
        { return; }
        BeforeSocialTick();
        FlushTyping();
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
            if (IsTyping(bot)) { continue; }
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
            if (HesitateOnNewTopic(bot, chosen) || BeginTyping(bot, chosen)) { continue; }
            GenerateSocialMessage(bot, chosen);
        }
    }
```

### GenerateSocialMessage

방에 맞는 종목을 선택하고 실제 게시가 끝난 경우에만 방의 주제 주목도를 갱신합니다.

**원본**

```csharp
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
        PreserveWatchObservation(bot, intent, facts);
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
            { RecordTemplateMiss(act); continue; }
            var template = templates[mind.SocialRng.Range(0, templates.Count)];
            var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
                promisedAction = template.promisedAction,
                debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
            string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
            message.text = RenderSocialText(mind, template, message, parentMessage, target, room.recent);
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            mind.RecentSpeechKeys.Enqueue(message.debug.speechKey ?? "");
            while (mind.RecentSpeechKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentSpeechKeys.Dequeue(); }
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
```

**수정본**

```csharp
    private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        if (topic == null && LiveConversationEnabled)
        { topic = ChooseRoomTopic(bot, intent.roomId, mind.SocialRng.Next()); }
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
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
            { RecordTemplateMiss(act); continue; }
            var template = templates[mind.SocialRng.Range(0, templates.Count)];
            var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
                promisedAction = template.promisedAction,
                debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
            string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
            message.text = RenderSocialText(mind, template, message, parentMessage, target, room.recent);
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            mind.RecentSpeechKeys.Enqueue(message.debug.speechKey ?? "");
            while (mind.RecentSpeechKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentSpeechKeys.Dequeue(); }
            room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
            room.sent.Enqueue(SocialTime.Seconds);
            consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
            lastAct[mind.SocialId] = act;
            socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
            while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
            ObserveConversationTopic(message);
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
```

## 수정 함수 비교: Assets/Scripts/NightShift/ChatSocialSpeech.cs

### RenderSocialText

기존 커뮤니티 확률 이후 고유 말투를 선택하고 주가·수량·원문 사실은 유지합니다. 종목명의 한국어 조사를 보정합니다.

**원본**

```csharp
    public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null;
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT"))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        return community ? text : DecorateSocialText(mind, message, text);
    }
```

**수정본**

```csharp
    public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT"))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        string voiceText = null;
        bool voiced = !community && TryVoiceText(mind, template, message, parent, target, recent, out voiceText);
        if (voiced) { phrase = voiceText; }
        phrase = VoiceStockParticles(phrase, message.facts.stockName ?? message.facts.symbol);
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        return community || voiced ? text : DecorateSocialText(mind, message, text);
    }
```

### CommunityGroup

실제 FOMO 감정 사건에도 별도 커뮤니티 문구를 허용합니다. 다른 제재·신호의 제한은 유지합니다.

**원본**

```csharp
    private static SocialCommunityPhrases CommunityGroup(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None)
        { return null; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        bool departure = facts.notice == "OPPONENT_LEFT" && !string.IsNullOrEmpty(facts.departedName);
        if (watch && (facts.observationBotId != message.senderId || message.replyToId != 0))
        { return null; }
        if (!watch && !departure && (facts.notice != null || !template.key.Contains(".plain.")))
        { return null; }
        string context = watch || departure ? facts.notice : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Community(message.act, context);
        if (group == null && !watch && !departure) { group = ChatSocialAssets.Community(message.act, parent != null || message.targetId != 0 ? "TARGET" : "NONE"); }
        return group != null && SpeechRequirementsMatch(group.requires, facts) ? group : null;
    }
```

**수정본**

```csharp
    private static SocialCommunityPhrases CommunityGroup(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None)
        { return null; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        bool fomo = facts.notice == "FOMO" && message.replyToId == 0;
        bool departure = facts.notice == "OPPONENT_LEFT" && !string.IsNullOrEmpty(facts.departedName);
        if (watch && (facts.observationBotId != message.senderId || message.replyToId != 0))
        { return null; }
        if (!watch && !departure && !fomo && (!string.IsNullOrEmpty(facts.notice) || !template.key.Contains(".plain.")))
        { return null; }
        string context = watch || departure || fomo ? facts.notice : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Community(message.act, context);
        if (group == null && !watch && !departure && !fomo) { group = ChatSocialAssets.Community(message.act, parent != null || message.targetId != 0 ? "TARGET" : "NONE"); }
        return group != null && SpeechRequirementsMatch(group.requires, facts) ? group : null;
    }
```

## 수정 함수 비교: Assets/Scripts/NightShift/ChatSocialMentions.cs

### ChatSocialMentions

명단 트리에 실제 종목 별칭/심볼을 함께 넣습니다.

**원본**

```csharp
    public ChatSocialMentions(IEnumerable<string> nicknames)
    {
        foreach (string name in nicknames)
        {
            if (string.IsNullOrWhiteSpace(name))
            { continue; }
            var node = root;
            foreach (char letter in name)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.Terminal = true;
        }
    }
```

**수정본**

```csharp
    public ChatSocialMentions(IEnumerable<string> nicknames, IEnumerable<KeyValuePair<string, string>> stocks = null)
    {
        foreach (string name in nicknames)
        {
            if (string.IsNullOrWhiteSpace(name))
            { continue; }
            var node = root;
            foreach (char letter in name)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.Terminal = true;
        }
        if (stocks == null) { return; }
        foreach (var stock in stocks)
        {
            if (string.IsNullOrWhiteSpace(stock.Key) || string.IsNullOrWhiteSpace(stock.Value)) { continue; }
            var node = root;
            foreach (char letter in stock.Key)
            {
                if (!node.Children.TryGetValue(letter, out var next)) { next = new Node(); node.Children.Add(letter, next); }
                node = next;
            }
            node.StockSymbol = stock.Value;
        }
    }
```

### Format

닉네임 강조와 종목 링크를 구별하고 메시지가 자체 TMP 링크를 주입하지 못하게 합니다.

**원본**

```csharp
    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            int end = index == 0 || !Word(text[index - 1]) ? MatchEnd(text, start) : -1;
            if (end > start)
            {
                result.Append(MarkOpen); AppendLiteral(result, text, index, end); result.Append("</mark>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
```

**수정본**

```csharp
    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            string symbol = null;
            int end = index == 0 || !Word(text[index - 1]) ? MatchEnd(text, start, text[index] == '@', out symbol) : -1;
            if (end > start)
            {
                if (symbol != null) { result.Append("<link=\"stock:").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(symbol))).Append("\">").Append(StockMarkOpen); }
                else { result.Append(MarkOpen); }
                AppendLiteral(result, text, index, end);
                result.Append(symbol == null ? "</mark>" : "</color></mark></link>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
```

### MatchEnd

가장 긴 등록 이름과 한국어 조사 경계를 인식하며 @ 접두사는 닉네임으로 유지합니다.

**원본**

```csharp
    private int MatchEnd(string text, int start)
    {
        var node = root; int end = -1;
        for (int i = start; i < text.Length && node.Children.TryGetValue(text[i], out node); i++)
        {
            if (node.Terminal && BoundaryAfter(text, i + 1)) { end = i + 1; }
        }
        return end;
    }
```

**수정본**

```csharp
    private int MatchEnd(string text, int start, bool nicknameOnly, out string symbol)
    {
        symbol = null;
        var node = root; int end = -1;
        for (int i = start; i < text.Length && node.Children.TryGetValue(text[i], out node); i++)
        {
            if ((node.Terminal || !nicknameOnly && node.StockSymbol != null) && BoundaryAfter(text, i + 1))
            { end = i + 1; symbol = nicknameOnly ? null : node.StockSymbol; }
        }
        return end;
    }
```

### TryStockSymbol

명시적 stock 링크만 엄격한 UTF-8로 복원합니다.

**원본**

기존에 없던 함수입니다.

**수정본**

```csharp
    public static bool TryStockSymbol(string link, out string symbol)
    {
        symbol = null;
        if (string.IsNullOrEmpty(link) || !link.StartsWith("stock:", StringComparison.Ordinal) || link.Length > 256) { return false; }
        try { symbol = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(link.Substring(6))); }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException) { return false; }
        return !string.IsNullOrWhiteSpace(symbol);
    }
```
