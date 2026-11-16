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
        BuildCard();
        Clear();
    }
    // ── 참여자 카드 ──
    // 플레이에 쓰는 것만 보인다: 누구인가, 나와의 관계(성향·신뢰·배신감), 내 리딩을 따른 결과.
    // 예전 게이지와 상세 창은 남겨 두되 화면에는 내지 않는다.
    private GameObject emptyState, filledState;
    private TextMeshProUGUI cardName, cardQuadrant, cardFomo, cardHint, trustCaption, trustValue, betrayalCaption, betrayalValue, recordCaption, recordValue;
    private RectTransform trustFill, betrayalFill, winFill, lossFill;
    private int cardRevision = -1;
    private string emptyCaption = "메시지를 눌러 참여자를 확인하세요";
    private void BuildCard()
    {
        if (filledState != null || heading == null) { return; }
        var panel = (RectTransform)transform; var like = heading;
        ChatFooterKit.Place(panel, panel.parent, ChatFooterCards.CardX, ChatFooterCards.ProfileTop, ChatFooterCards.CardWidth, ChatFooterCards.ProfileHeight);
        var legacy = ChatFooterKit.Area("Area :: Legacy Meters", panel, 0, 0, 1, 1); legacy.gameObject.SetActive(false);
        foreach (var old in new[] { heading.transform, summary.transform, meters.transform, details.transform }) { old.SetParent(legacy, false); }
        ChatFooterKit.Card("Panel :: Investor Card", panel, 0, 0, 1, 1).transform.SetAsFirstSibling();

        emptyState = ChatFooterKit.Area("Area :: No Investor", panel, 0, 0, 1, 1).gameObject;
        var icon = ChatFooterKit.Area("Image :: Investor Icon", emptyState.transform, .44f, .17f, .12f, .34f).gameObject.AddComponent<Image>();
        icon.sprite = Resources.Load<Sprite>("UI/TradingTerminal/Icons/Icon_People"); icon.preserveAspect = true; icon.color = ChatFooterKit.Faint; icon.raycastTarget = false;
        cardHint = ChatFooterKit.Label("Text :: Investor Hint", emptyState.transform, like, .05f, .57f, .90f, .26f, 34f, ChatFooterKit.Muted, TextAlignmentOptions.Center);

        // 머리줄: 누구인가, 나와 어떤 사이인가. 그 아래 세 칸: 신뢰, 배신감, 내 리딩을 따른 결과.
        filledState = ChatFooterKit.Area("Area :: Investor", panel, 0, 0, 1, 1).gameObject;
        var root = filledState.transform;
        cardName = ChatFooterKit.Label("Text :: Investor Name", root, like, .024f, .11f, .44f, .27f, 48f, ChatFooterKit.Ink);
        cardQuadrant = ChatFooterKit.Tag("Relationship", root, like, .47f, .15f, .13f, .19f, 28f);
        cardFomo = ChatFooterKit.Tag("Fomo", root, like, .61f, .15f, .11f, .19f, 28f); cardFomo.color = ChatFooterKit.Amber;
        trustFill = Meter(root, like, .024f, ChatFooterKit.Teal, out trustCaption, out trustValue, out _);
        betrayalFill = Meter(root, like, .349f, ChatFooterKit.Rose, out betrayalCaption, out betrayalValue, out _);
        winFill = Meter(root, like, .674f, ChatFooterKit.Teal, out recordCaption, out recordValue, out var recordTrack);
        lossFill = (RectTransform)ChatFooterKit.Flat("Image :: Loss Fill", recordTrack, 0, 0, 1, 1, ChatFooterKit.Rose).transform;
    }
    /// <summary>이름표·값·막대 한 벌. 막대는 0~100을 채우고, 값은 100을 넘어도 그대로 적는다.</summary>
    private static RectTransform Meter(Transform root, TextMeshProUGUI like, float x, Color color, out TextMeshProUGUI caption, out TextMeshProUGUI value, out Transform track)
    {
        const float width = .302f;
        caption = ChatFooterKit.Label("Text :: Meter Caption", root, like, x, .50f, width * .5f, .20f, 30f, ChatFooterKit.Muted);
        value = ChatFooterKit.Label("Text :: Meter Value", root, like, x + width * .4f, .50f, width * .6f, .20f, 38f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        track = ChatFooterKit.Flat("Image :: Meter Track", root, x, .76f, width, .05f, ChatFooterKit.Track).transform;
        return (RectTransform)ChatFooterKit.Flat("Image :: Meter Fill", track, 0, 0, 1, 1, color).transform;
    }
    private static void Fill(RectTransform fill, float from, float to)
    { fill.anchorMin = new Vector2(Mathf.Clamp01(from), 0f); fill.anchorMax = new Vector2(Mathf.Clamp01(to), 1f); fill.offsetMin = fill.offsetMax = Vector2.zero; }
    private void RefreshCard()
    {
        if (filledState == null) { return; }
        cardRevision = GameLocale.Revision;
        bool has = state != null;
        emptyState.SetActive(!has); filledState.SetActive(has);
        if (!has) { ChatFooterKit.Lit(cardHint, ChatFooterText.T(emptyCaption)); return; }
        var mind = state.Psychology;
        ChatFooterKit.Lit(cardName, state.Nickname);
        ChatFooterKit.Lit(cardQuadrant, ChatFooterText.T(mind.QuadrantLabel));
        cardQuadrant.color = mind.Quadrant switch { InvestorQuadrant.Loyal => ChatFooterKit.Teal, InvestorQuadrant.Ambivalent => ChatFooterKit.Amber, InvestorQuadrant.Hostile => ChatFooterKit.Rose, _ => ChatFooterKit.Muted };
        cardFomo.transform.parent.gameObject.SetActive(mind.HasFomo);
        ChatFooterKit.Lit(cardFomo, "FOMO");
        // 꼬리표는 이름 바로 뒤에 붙는다.
        ChatFooterKit.Follow(cardName, cardQuadrant, 22f, 24f);
        if (mind.HasFomo) { ChatFooterKit.FollowTag(cardQuadrant, cardFomo, 12f, 24f); }
        ChatFooterKit.Lit(trustCaption, ChatFooterText.T("신뢰")); ChatFooterKit.Lit(trustValue, mind.Trust.ToString("0") + "%"); Fill(trustFill, 0f, mind.Trust / 100f);
        ChatFooterKit.Lit(betrayalCaption, ChatFooterText.T("배신감")); ChatFooterKit.Lit(betrayalValue, mind.Betrayal.ToString("0") + "%"); Fill(betrayalFill, 0f, mind.Betrayal / 100f);
        // 승패는 한 막대를 나눠 보여 준다: 왼쪽이 이긴 몫, 오른쪽이 진 몫.
        int total = mind.Wins + mind.Losses; float won = total == 0 ? 0f : mind.Wins / (float)total;
        ChatFooterKit.Lit(recordCaption, ChatFooterText.T("직접 경험")); ChatFooterKit.Lit(recordValue, ChatFooterText.F("{0}승 · {1}패", mind.Wins, mind.Losses));
        recordValue.color = total == 0 ? ChatFooterKit.Faint : ChatFooterKit.Ink;
        Fill(winFill, 0f, won); Fill(lossFill, total == 0 ? 1f : won, 1f);
    }
    void Update()
    { if (cardRevision != GameLocale.Revision) { RefreshCard(); } }
    public void Clear(string caption = "참여자 선택")
    {
        state = null; SelectedMetric = hovered = -1;
        meters.SetActive(false); detailPanel.SetActive(false); details.interactable = false;
        DeviceTerminalUI.Set(heading,caption); DeviceTerminalUI.Set(summary,"메시지를 눌러 확인");
        emptyCaption = caption == "참여자 퇴장" ? caption : "메시지를 눌러 참여자를 확인하세요";
        RefreshCard();
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
        RefreshCard();
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
