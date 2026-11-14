using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Five live gauges with quadrant/FOMO tags; exact values and the full profile are opt-in.</summary>
public sealed class InvestorProfileUI : MonoBehaviour
{
    public TextMeshProUGUI heading, summary, detailText;
    public GameObject meters, detailPanel;
    public TerminalGlyph[] gauges;
    public Button[] metricButtons;
    public Button details, closeDetails;
    public int SelectedMetric { get; private set; } = -1;
    public int SelectedBotId => state == null ? 0 : state.Id;
    private BotSocialState state;
    private int hovered = -1;
    private UnityAction[] clicks;
    private EventTrigger[] triggers;
    private EventTrigger.Entry[] enter, leave;
    private static readonly string[] Labels = {"신뢰","배신감","탐욕","공포","","스트레스"};
    void Start()
    {
        clicks = new UnityAction[6]; triggers = new EventTrigger[6]; enter = new EventTrigger.Entry[6]; leave = new EventTrigger.Entry[6];
        for (int i = 0; i < 6; i++)
        {
            if (i == 4) { metricButtons[i].gameObject.SetActive(false); continue; }
            var meterRect = (RectTransform)metricButtons[i].transform; int position = i < 4 ? i : 4;
            meterRect.anchorMin = new Vector2(position / 5f, 0); meterRect.anchorMax = new Vector2(position / 5f + .18f, 1); meterRect.offsetMin = meterRect.offsetMax = Vector2.zero;
            int index = i; clicks[i] = () => Pin(index); metricButtons[i].onClick.AddListener(clicks[i]);
            triggers[i] = metricButtons[i].GetComponent<EventTrigger>() ?? metricButtons[i].gameObject.AddComponent<EventTrigger>();
            enter[i] = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            leave[i] = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            enter[i].callback.AddListener(_ => Hover(index)); leave[i].callback.AddListener(_ => Hover(-1));
            triggers[i].triggers.Add(enter[i]); triggers[i].triggers.Add(leave[i]);
        }
        details.onClick.AddListener(ShowDetails); closeDetails.onClick.AddListener(HideDetails);
        Clear();
    }
    public void Clear(string caption = "참여자 선택")
    {
        state = null; SelectedMetric = hovered = -1;
        meters.SetActive(false); detailPanel.SetActive(false); details.interactable = false;
        DeviceTerminalUI.Set(heading,caption); DeviceTerminalUI.Set(summary,"메시지를 눌러 확인");
    }
    public void Bind(BotSocialState value)
    {
        if (value == null)
        { Clear("참여자 퇴장"); return; }
        if (state == null || state.Id != value.Id)
        { SelectedMetric = hovered = -1; detailPanel.SetActive(false); }
        state = value; meters.SetActive(true); details.interactable = true;
        state.Psychology.RefreshQuadrant();
        DeviceTerminalUI.Set(heading,state.Nickname + " · " + state.Psychology.QuadrantLabel + (state.Psychology.HasFomo ? " · FOMO" : ""));
        for (int i = 0; i < gauges.Length; i++) { if (i != 4) { gauges[i].SetValue(Amount(i)); } }
        RefreshSummary();
        if (detailPanel.activeSelf) { RefreshDetails(); }
    }
    private float Amount(int index)
    {
        var mind = state.Psychology;
        return index switch { 0 => mind.Trust, 1 => mind.Betrayal, 2 => mind.Greed, 3 => mind.Fear, _ => state.Stress };
    }
    private void Hover(int index) { hovered = index; RefreshSummary(); }
    private void Pin(int index) { SelectedMetric = SelectedMetric == index ? -1 : index; RefreshSummary(); }
    private void RefreshSummary()
    {
        if (state == null)
        { return; }
        int index = hovered >= 0 ? hovered : SelectedMetric;
        string text = index >= 0 ? Labels[index] + " " + Amount(index).ToString("0.#") + (index < 2 ? "%" : "/100") + " · " + TerminalGlyph.BandName(Amount(index)) : "직접 경험  " + state.Psychology.Wins + "승 · " + state.Psychology.Losses + "패";
        DeviceTerminalUI.Set(summary,text);
    }
    private void ShowDetails()
    {
        if (state == null)
        { return; }
        detailPanel.SetActive(true); RefreshDetails();
    }
    private void HideDetails() { detailPanel.SetActive(false); }
    private void RefreshDetails()
    {
        var mind = state.Psychology;
        double anger = mind.MaximumAnger(out int target);
        string opponent = MarketDayController.Instance?.FindBot(target)?.Social.Nickname ?? "없음";
        DeviceTerminalUI.Set(detailText,state.Nickname + " · " + state.Profile + "\n\n" +
            $"신뢰 {mind.Trust:0.#}%  ·  배신감 {mind.Betrayal:0.#}% · {mind.QuadrantLabel}\n탐욕 {mind.Greed:0}  ·  공포 {mind.Fear:0}" + (mind.HasFomo ? " · FOMO" : "") + $"\n최대 분노 {anger:0} · 대상 {opponent}\n스트레스 {state.Stress:0.#}/100\n직접 경험 {mind.Wins}승 / {mind.Losses}패\n\n위험 {state.Risk:F1}  ·  기간 {state.Horizon:F1}  ·  추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1}  ·  회복 {state.Recovery:F1}  ·  {state.Tone}\n\n최근 변화 · {mind.LastCause}");
    }
    void OnDestroy()
    {
        if (clicks != null)
        {
            for (int i = 0; i < clicks.Length; i++)
            {
                if (i == 4) { continue; }
                if (metricButtons[i] != null) { metricButtons[i].onClick.RemoveListener(clicks[i]); }
                if (triggers[i] != null) { triggers[i].triggers.Remove(enter[i]); triggers[i].triggers.Remove(leave[i]); }
            }
        }
        if (details != null) { details.onClick.RemoveListener(ShowDetails); }
        if (closeDetails != null) { closeDetails.onClick.RemoveListener(HideDetails); }
    }
}
