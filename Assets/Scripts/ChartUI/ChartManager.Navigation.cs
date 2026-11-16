using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ChartManager
{
    private int periodMinutes;
    private long historyAnchor;
    private CanvasGroup navigation;
    private Button latestButton;
    private readonly Button[] periodButtons = new Button[6];
    private readonly int[] periods = { 0, 1, 5, 15, 60, 1440 };
    private void CaptureHistoryAnchor()
    {
        if (!followLatest && startIndex >= 0 && startIndex < _fullCandles.Count)
        { historyAnchor = ChartHistorySeries.Key(_fullCandles[startIndex], periodMinutes); }
    }
    private void RestoreHistoryAnchor()
    {
        if (followLatest)
        { return; }
        for (int i = 0; i < _fullCandles.Count; i++)
        {
            if (ChartHistorySeries.Key(_fullCandles[i], periodMinutes) == historyAnchor)
            { startIndex = i; return; }
        }
        // Only the oldest retained boundary can move when the bounded archive expires.
        startIndex = 0;
        CaptureHistoryAnchor();
    }
    public void ShowLatestCandles()
    {
        if (ChartInputBlocked())
        { return; }
        followLatest = true; ReleaseChartPointer();
        candleSelectData?.Clear(); RefreshChartFromHistory();
    }
    public void SetChartPeriod(int minutes)
    {
        if (ChartInputBlocked() || System.Array.IndexOf(periods, minutes) < 0 || periodMinutes == minutes)
        { return; }
        periodMinutes = minutes; followLatest = true; ReleaseChartPointer();
        candleSelectData?.Clear(); RefreshChartFromHistory(); UpdateNavigation();
    }
    private void CreateNavigation()
    {
        if (_guides == null || textLastPrice == null)
        { return; }
        var root = new GameObject("Area :: Chart Navigation", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = _guides.gameObject.layer;
        root.transform.SetParent(_guides.transform, false);
        navigation = root.GetComponent<CanvasGroup>();
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        var canvas = candleRenderer.GetComponentInParent<Canvas>();
        float scaleY = canvas.transform.InverseTransformVector(candleRenderer.transform.TransformVector(Vector3.up)).magnitude;
        rect.anchoredPosition = new Vector2(0, candleRenderer.chartHeight * scaleY + 6);
        rect.sizeDelta = new Vector2(570, 36);
        string[] labels = { "기본", "1m", "5m", "15m", "1h", "1D" };
        for (int i = 0; i < periods.Length; i++)
        {
            int minutes = periods[i];
            periodButtons[i] = NavigationButton(rect, labels[i], i * 78, () => SetChartPeriod(minutes));
        }
        latestButton = NavigationButton(rect, "→", 490, ShowLatestCandles);
        UpdateNavigation();
    }
    private Button NavigationButton(RectTransform parent, string label, float x, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Button :: Chart " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(70, 36);
        var image = go.GetComponent<Image>(); image.color = new Color32(25, 36, 42, 255);
        var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var textObject = new GameObject("Text :: Period", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = go.layer;
        var text = textObject.GetComponent<TextMeshProUGUI>(); text.transform.SetParent(rect, false);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = textLastPrice.font; text.fontSize = 23; text.alignment = TextAlignmentOptions.Center;
        GameLocalizedText.Set(text, label); text.raycastTarget = false;
        return button;
    }
    private void UpdateNavigation()
    {
        if (navigation == null)
        { return; }
        bool enabled = !ChartInputBlocked();
        navigation.interactable = navigation.blocksRaycasts = enabled;
        for (int i = 0; i < periods.Length; i++)
        {
            var color = periods[i] == periodMinutes ? new Color32(30, 108, 119, 255) : new Color32(25, 36, 42, 255);
            if (periodButtons[i].image.color != color)
            { periodButtons[i].image.color = color; }
        }
        latestButton.gameObject.SetActive(!followLatest);
    }
}
