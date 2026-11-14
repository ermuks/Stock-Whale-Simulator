using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

/// <summary>가변 높이 가상 목록. 보이는 행 + 여유 두 개만 재사용, 읽는 중 스크롤 고정.</summary>
public sealed class TabletGroupChatUI : MonoBehaviour
{
    public bool monitorChat;
    public float layoutScale = 1f;
    public TabletViewController tablet;
    public GameObject chatPanel, informationPanel;
    public ScrollRect scroll;
    public TextMeshProUGUI status, unread, profile, hudStatus;
    public Button chatTab, informationTab, newestButton;
    public RectTransform rowTemplate;
    public float rowHeight = 84f;
    public int rowPoolLimit = 16;
    public Sprite avatarOrange, avatarBlue, avatarPink;
    public TextMeshProUGUI participants;
    public InvestorProfileUI visualProfile;
    private readonly List<float> rowOffsets = new();
    private readonly List<float> rowHeights = new();
    private readonly Dictionary<long, float> measuredHeights = new();
    private readonly HashSet<long> retainedSequences = new();
    private readonly List<long> expiredSequences = new();
    private TextMeshProUGUI messageMeasure;
    private TextMeshProUGUI nicknameMeasure;
    private float measuredWidth = -1f;
    private int lastParticipantCount = -1;
    private ScrollRect informationScroll;
    private sealed class Row
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public Image Gauge, Avatar;
        public TerminalGlyph StressRing;
        public TextMeshProUGUI Nickname, Time, AngerDot;
        public RectTransform Bubble;
        public int BotId;
        public long Sequence = -1;
    }

    private readonly List<Row> rows = new();
    private float nextAngerRefresh;
    private ChatSocialMenuUnity moderationMenu;
    private MarketDayController day;
    private long version = -1, readSequence, oldestSequence;
    private int selectedBot, lastSecond = -1, firstVisible = -1, lastMinute = -1, lastDay = -1, lastHudMinute = -1, lastHudDay = -1;
    private bool followNewest = true, chatSelected, dirty = true;
    private ChatScrollInput scrollInput;
    private int layoutFrame = -1;
    public bool FollowingNewest => followNewest;
    private static readonly ProfilerMarker UiMarker = new("DonDon.ChatUI");
    public int PooledRowCount => rows.Count;
    public int SelectedRoom { get; private set; }
    public int SelectedBotId => selectedBot;
    private InvestorSociety.Room Room => InvestorSociety.Instance?.GetRoom(SelectedRoom);
    private bool CanReadRoom => SelectedRoom != NpcLeadingRoom.RoomId || NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined;
    private int MessageCount => SelectedRoom == 0 ? day.MessageCount : Room == null || !CanReadRoom ? 0 : Room.Count;
    private long MessageVersion => SelectedRoom == 0 ? day.MessageVersion : Room == null || !CanReadRoom ? 0 : Room.Version;
    private BotChatMessage Message(int index) => SelectedRoom == 0 ? day.GetMessage(index) : Room.Get(index);
    public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }

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
            rect.GetComponent<Button>().onClick.AddListener(() => SelectProfile(row.BotId));
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); });
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
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }

    public void ShowChat()
    {
        chatSelected = true;
        chatPanel.SetActive(true);
        if (informationPanel != null)
        { informationPanel.SetActive(false); }
        if (tablet != null)
        { tablet.scrollRect = scroll; }
        dirty = true;
    }

    public void ShowInformation()
    {
        chatSelected = false;
        chatPanel.SetActive(false);
        informationPanel.SetActive(true);
        tablet.scrollRect = informationScroll;
    }

    public void ShowNewest()
    {
        followNewest = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture(); SnapToNewest();
        dirty = true;
    }

    private void OnScroll(Vector2 position)
    {
        dirty = true;
    }

    private void ConfigureScrolling()
    {
        scroll.vertical = true; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scrollInput = scroll.gameObject.AddComponent<ChatScrollInput>(); scrollInput.chat = this; scrollInput.scroll = scroll;
        var surface = scroll.viewport.GetComponent<Image>();
        if (surface == null) { surface = scroll.viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear; }
        surface.raycastTarget = true;
        var rect = (RectTransform)newestButton.transform; rect.SetParent(scroll.transform,false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,0); rect.pivot = new Vector2(.5f,0);
        rect.sizeDelta = Vector2.one * (96f * layoutScale); rect.anchoredPosition = new Vector2(0,16f * layoutScale);
        rect.localScale = Vector3.one; rect.SetAsLastSibling(); rect.name = "Button :: Jump To Latest Chat";
        foreach (var label in newestButton.GetComponentsInChildren<TextMeshProUGUI>(true)) { label.gameObject.SetActive(false); }
        newestButton.image.sprite = Resources.Load<Sprite>("UI/TradingTerminal/Icons/Icon_ChatDown");
        newestButton.image.type = Image.Type.Simple; newestButton.image.preserveAspect = true;
        newestButton.image.color = Color.white; newestButton.image.raycastTarget = true;
        newestButton.transition = Selectable.Transition.ColorTint; newestButton.gameObject.SetActive(false);
    }
    public void BeginManualScroll() { followNewest = false; }
    public bool ReadUserScrollPosition()
    {
        if (layoutFrame == Time.frameCount)
        { return false; }
        float bottom = Mathf.Max(0f,scroll.content.rect.height-scroll.viewport.rect.height);
        followNewest = bottom-scroll.content.anchoredPosition.y <= 18f * layoutScale;
        dirty = true;
        return true;
    }
    private void SnapToNewest()
    {
        float bottom = Mathf.Max(0f,scroll.content.rect.height-scroll.viewport.rect.height);
        if (Mathf.Abs(scroll.content.anchoredPosition.y-bottom) > .1f)
        { scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x,bottom); dirty = true; }
    }
    void LateUpdate()
    {
        if (!chatSelected || !chatPanel.activeInHierarchy)
        { return; }
        if (followNewest && scrollInput != null && !scrollInput.Dragging)
        { scroll.StopMovement(); SnapToNewest(); }
        bool show = !followNewest && MessageCount > 0 && CanReadRoom;
        if (newestButton.gameObject.activeSelf != show) { newestButton.gameObject.SetActive(show); }
    }

    public void SelectProfile(int botId)
    {
        selectedBot = botId;
        lastProfileSecond = -1;
        dirty = true;
    }

    void Update()
    {
        if (day == null)
        { return; }

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
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = message.Text;
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }

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
                    if (!row.Rect.gameObject.activeSelf) { continue; }
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

    private void RebuildMessageLayout(float width)
    {
        layoutFrame = Time.frameCount;
        float y = scroll.content.anchoredPosition.y;
        long oldest = MessageCount > 0 ? Message(0).Sequence : 0;
        long removed = oldestSequence > 0 ? System.Math.Max(0, oldest - oldestSequence) : 0;
        if (!followNewest && removed > 0 && rowOffsets.Count > 0)
        {
            int removedRows = (int)System.Math.Min(removed, rowOffsets.Count);
            float removedHeight = removedRows < rowOffsets.Count ? rowOffsets[removedRows] : rowOffsets[rowOffsets.Count - 1] + rowHeights[rowHeights.Count - 1];
            y = Mathf.Max(0f, y - removedHeight);
        }

        if (!Mathf.Approximately(width, measuredWidth))
        {
            measuredHeights.Clear();
            measuredWidth = width;
        }

        rowOffsets.Clear();
        rowHeights.Clear();
        retainedSequences.Clear();
        float total = 0f;
        for (int i = 0; i < MessageCount; i++)
        {
            var message = Message(i);
            retainedSequences.Add(message.Sequence);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height))
            {
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(message.Nickname, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(message.Text, width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader;
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in measuredHeights.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
        }

        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        if (followNewest)
        {
            SnapToNewest();
        }
        else
        {
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, total - scroll.viewport.rect.height)));
        }

        oldestSequence = oldest;
        version = MessageVersion;
        dirty = true;
    }

    private int FindFirstVisible(float y)
    {
        int low = 0, high = rowOffsets.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (rowOffsets[middle] + rowHeights[middle] <= y)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Mathf.Clamp(low, 0, Mathf.Max(0, rowOffsets.Count - 1));
    }

    private bool wasHeld;
    private int lastHudSecond = -1;
    private MarketPhase lastHudPhase;
    private static string PhaseText(MarketPhase phase)
    { return phase == MarketPhase.PreOpen ? "개장 전" : phase == MarketPhase.Trading ? "거래 중" : phase == MarketPhase.Closing ? "마감 정리" : "마감 후"; }

    private int lastProfileSecond = -1, lastUnread = -1;
    private void UpdateUnread()
    {
        int count = (int)System.Math.Min(MessageCount, MessageVersion - readSequence);
        if (count != lastUnread)
        {
            lastUnread = count;
            unread.text = SelectedRoom > 0 ? Room.DisplayName : monitorChat ? "시장 라운지" : count > 0 ? $"새 메시지 {count}" : "시장 라운지";
        }
    }

    void OnEnable()
    {
        dirty = true;
        lastSecond = -1;
    }

    void OnDestroy()
    {
        if (chatTab != null)
        {
            chatTab.onClick.RemoveListener(ShowChat);
        }

        if (informationTab != null)
        {
            informationTab.onClick.RemoveListener(ShowInformation);
        }

        if (newestButton != null)
        {
            newestButton.onClick.RemoveListener(ShowNewest);
        }

        if (scroll != null)
        {
            scroll.onValueChanged.RemoveListener(OnScroll);
        }
    }
}
