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
