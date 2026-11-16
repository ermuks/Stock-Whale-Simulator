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
    private readonly Dictionary<long, string> displayedMessages = new();
    private ChatSocialMentions mentions;
    private InvestorSociety mentionSociety;
    private int mentionBotCount = -1;
    private StockManager mentionStockManager;
    private bool mentionRosterDirty = true;
    private int localeRevision = -1;
    private TextMeshProUGUI typingLabel;
    private float nextTypingRefresh;
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
        public SocialMessage SocialMessage;
        public long Sequence = -1;
        public Button Click;
        public Image NoticeBackground;
        public TextMeshProUGUI NoticeText;
        public bool IsNotice;
        // 손익 인증 카드
        public RectTransform Card;
        public Image CardAccent;
        public TextMeshProUGUI CardTitle, CardKind, CardAmount, CardDetail;
        public bool HasCard;
    }

    // 글자 줄 높이(글자 크기의 약 1.5배)보다 칸이 낮으면 말줄임 처리로 글자가 통째로 사라진다. 칸 높이를 넉넉히 둔다.
    private const float CardBlockHeight = 118f, CardHeightPixels = 110f, CardWidthPixels = 430f;
    private static readonly Color CardBack = new(.09f, .12f, .15f, .96f), CardInk = new(.92f, .94f, .96f, 1f), CardMuted = new(.60f, .66f, .71f, 1f);

    private float CardHeight(BotChatMessage message)
    { return message.IsRoomNotice || message.Social == null || message.Social.profitCard == null ? 0f : CardBlockHeight * layoutScale; }

    private TextMeshProUGUI CardLabel(RectTransform parent, string name, float x, float y, float w, float h, float size, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = parent.gameObject.layer;
        item.transform.SetParent(parent, false);
        var label = item.GetComponent<TextMeshProUGUI>();
        label.font = messageMeasure.font; label.fontSize = size * layoutScale; label.fontStyle = style; label.color = color;
        label.raycastTarget = false; label.richText = false; label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = alignment;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y) * layoutScale;
        rect.sizeDelta = new Vector2(w, h) * layoutScale;
        return label;
    }

    // 풀링되는 행마다 한 번만 만든다. 클릭은 막지 않는다(행 버튼과 종목 링크가 그대로 동작한다).
    private void BuildCardVisual(Row row)
    {
        var root = new GameObject("Panel :: Profit Card", typeof(RectTransform), typeof(Image));
        root.layer = row.Rect.gameObject.layer;
        root.transform.SetParent(row.Rect, false);
        row.Card = (RectTransform)root.transform;
        row.Card.anchorMin = row.Card.anchorMax = row.Card.pivot = new Vector2(0f, 1f);
        row.Card.sizeDelta = new Vector2(CardWidthPixels, CardHeightPixels) * layoutScale;
        var back = root.GetComponent<Image>(); back.color = CardBack; back.raycastTarget = false;
        var accent = new GameObject("Image :: Profit Card Accent", typeof(RectTransform), typeof(Image));
        accent.layer = root.layer; accent.transform.SetParent(root.transform, false);
        var accentRect = (RectTransform)accent.transform;
        accentRect.anchorMin = new Vector2(0f, 0f); accentRect.anchorMax = new Vector2(0f, 1f); accentRect.pivot = new Vector2(0f, .5f);
        accentRect.anchoredPosition = Vector2.zero; accentRect.sizeDelta = new Vector2(4f * layoutScale, 0f);
        row.CardAccent = accent.GetComponent<Image>(); row.CardAccent.raycastTarget = false;
        row.CardTitle = CardLabel(row.Card, "Text :: Profit Card Stock", 18f, 6f, 270f, 34f, 21f, TextAlignmentOptions.MidlineLeft, CardInk);
        row.CardKind = CardLabel(row.Card, "Text :: Profit Card Kind", 296f, 6f, 120f, 34f, 18f, TextAlignmentOptions.MidlineRight, CardMuted);
        row.CardAmount = CardLabel(row.Card, "Text :: Profit Card Amount", 18f, 38f, 398f, 40f, 25f, TextAlignmentOptions.MidlineLeft, CardInk, FontStyles.Bold);
        row.CardDetail = CardLabel(row.Card, "Text :: Profit Card Detail", 18f, 76f, 398f, 28f, 17f, TextAlignmentOptions.MidlineLeft, CardMuted);
        root.SetActive(false);
    }

    private void BindCard(Row row, ChatProfitCard card)
    {
        row.HasCard = card != null;
        row.Card.gameObject.SetActive(card != null);
        if (card == null)
        { return; }

        var tone = MarketColors.ForChange(card.amount, CardInk);
        row.CardAccent.color = tone;
        GameLocalizedText.Set(row.CardTitle, card.stockName);
        GameLocalizedText.Set(row.CardKind, card.kind == ChatProfitCardKind.Holding ? "보유 중" : card.kind == ChatProfitCardKind.RealizedGain ? (card.partial ? "일부 익절" : "익절") : card.partial ? "일부 손절" : "손절");
        GameLocalizedText.Set(row.CardAmount, (card.amount > 0m ? "+" : card.amount < 0m ? "-" : "") + DeviceTerminalUI.Money(System.Math.Abs(decimal.Round(card.amount))) + " (" + card.percent.ToString("+0.0;-0.0;0.0") + "%)", true);
        row.CardAmount.color = tone;
        bool detail = card.quantity >= 1m && card.averageCost > 0m;
        row.CardDetail.gameObject.SetActive(detail);
        if (detail)
        {
            string from = PriceTickPolicy.FormatMoney(card.averageCost), to = PriceTickPolicy.FormatMoney(card.price), quantity = decimal.Floor(card.quantity).ToString("N0");
            // 낱말 단위로 번역해 조립한다. 한국어는 수량 뒤에 붙여 쓰고 다른 언어는 띄운다.
            string shares = quantity + (GameLocale.Code == "ko" ? "" : " ") + GameLocale.Text("주");
            GameLocalizedText.Set(row.CardDetail, card.kind == ChatProfitCardKind.Holding
                ? GameLocale.Text("평단") + " " + from + " → " + to + " · " + shares
                : GameLocale.Text("매수") + " " + from + " → " + GameLocale.Text("매도") + " " + to + " · " + shares, true);
        }
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
        if (SelectedRoom > InvestorSociety.AvailablePlayerRoomCount && SelectedRoom <= InvestorSociety.PlayerRoomCount) { SelectedRoom = 0; }
        selectedBot = 0;
        if (profile != null)
        { GameLocalizedText.Set(profile,"메시지를 눌러 참여자의 경험과 심리를 확인하세요."); }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
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
            row.AngerDot.name = "Text :: Anger Indicator"; GameLocalizedText.Set(row.AngerDot,"●");
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            BuildCardVisual(row);
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

    private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        var stocks = StockManager.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && !mentionRosterDirty && mentionSociety == society && mentionBotCount == count && mentionStockManager == stocks)
        { return; }
        var names = new List<string> { "방장", GameLocale.Text("방장") };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); GameLocale.ProtectLiteral(bot.Social.Nickname); } }
        var aliases = new List<KeyValuePair<string, string>>();
        if (stocks != null)
        {
            foreach (var stock in stocks.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
                aliases.Add(new KeyValuePair<string, string>(stock.stockName, stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(GameLocale.Text(stock.stockName), stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(stock.symbol, stock.symbol));
            }
        }
        mentions = new ChatSocialMentions(names, aliases); mentionSociety = society; mentionBotCount = count;
        mentionStockManager = stocks; mentionRosterDirty = false;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
    private void InvalidateMentionRoster(Stock stock) { mentionRosterDirty = true; }
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
        GameLocalizedText.Set(typingLabel,"");
    }
    private void RefreshTypingIndicator()
    {
        if (typingLabel == null || Time.unscaledTime < nextTypingRefresh) { return; }
        nextTypingRefresh = Time.unscaledTime + .15f;
        var society = InvestorSociety.Instance;
        string text = chatSelected && CanReadRoom && MarketDayController.CanTrade && society != null ? society.ReadTyping(SelectedRoom).Text : "";
        DeviceTerminalUI.Set(typingLabel, text);
    }
    private string DisplayMessage(BotChatMessage message)
    {
        if (!displayedMessages.TryGetValue(message.Sequence, out string text))
        { string localized = LocalizedChat.Text(message); text = message.IsRoomNotice ? ChatSocialMentions.Literal(localized) : mentions.Format(localized); displayedMessages.Add(message.Sequence, text); }
        return text;
    }
    private void BuildNoticeVisual(Row row)
    {
        var background = new GameObject("Image :: Room Notice", typeof(RectTransform), typeof(Image));
        background.layer = row.Rect.gameObject.layer; background.transform.SetParent(row.Rect, false);
        row.NoticeBackground = background.GetComponent<Image>();
        var source = row.Bubble.GetComponent<Image>();
        row.NoticeBackground.sprite = source.sprite; row.NoticeBackground.type = Image.Type.Sliced;
        row.NoticeBackground.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        row.NoticeBackground.color = new Color(1f, 1f, 1f, .8f); row.NoticeBackground.raycastTarget = false;
        row.NoticeText = Instantiate(row.Text, row.Rect);
        row.NoticeText.name = "Text :: Room Notice"; row.NoticeText.fontSize = row.Text.fontSize * .86f;
        row.NoticeText.alignment = TextAlignmentOptions.Center; row.NoticeText.color = new Color(.76f, .81f, .84f, 1f);
        row.NoticeText.enableAutoSizing = false; row.NoticeText.textWrappingMode = TextWrappingModes.NoWrap;
        row.NoticeText.margin = Vector4.zero; row.NoticeText.fontStyle = FontStyles.Normal;
        row.NoticeText.overflowMode = TextOverflowModes.Ellipsis; row.NoticeText.raycastTarget = false;
        foreach (var rect in new[] { row.NoticeBackground.rectTransform, row.NoticeText.rectTransform })
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; }
        background.SetActive(false); row.NoticeText.gameObject.SetActive(false);
    }
    private void SetNoticeMode(Row row, bool notice)
    {
        row.IsNotice = notice; row.Click.interactable = !notice;
        row.Text.gameObject.SetActive(!notice); row.Bubble.gameObject.SetActive(!notice);
        row.Avatar.gameObject.SetActive(!notice); row.Gauge.gameObject.SetActive(!notice);
        row.Nickname.gameObject.SetActive(!notice); row.Time.gameObject.SetActive(!notice);
        row.AngerDot.gameObject.SetActive(false);
        row.NoticeBackground.gameObject.SetActive(notice); row.NoticeText.gameObject.SetActive(notice);
        if (notice && row.Card != null) { row.HasCard = false; row.Card.gameObject.SetActive(false); }
    }
    private void LayoutNotice(Row row, BotChatMessage message)
    {
        GameLocalizedText.Set(row.NoticeText, DisplayMessage(message),true);
        float available = Mathf.Max(1f, row.Rect.rect.width - 40f * layoutScale);
        float width = Mathf.Min(available, row.NoticeText.GetPreferredValues(row.NoticeText.text).x + 36f * layoutScale);
        float height = 40f * layoutScale;
        row.NoticeBackground.rectTransform.sizeDelta = new Vector2(width, height);
        row.NoticeText.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width - 24f * layoutScale), height);
    }
    void Update()
    {
        if (localeRevision != GameLocale.Revision)
        { localeRevision = GameLocale.Revision; mentionRosterDirty = true; }
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
            GameLocalizedText.Set(participants,lastParticipantCount.ToString("N0"));
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            GameLocalizedText.Set(hudStatus,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
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
                GameLocalizedText.Set(status,$"{day.CalendarDay}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
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
                        GameLocalizedText.Set(row.Nickname,message.Nickname,true);
                        GameLocalizedText.Set(row.Time,$"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}");
                        GameLocalizedText.Set(row.Text,DisplayMessage(message),true);
                        BindCard(row, message.IsRoomNotice || message.Social == null ? null : message.Social.profitCard);
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
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(GameLocale.Text(row.Nickname.text), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    // 카드가 있으면 말풍선은 그만큼 짧아지고 카드는 말풍선 바로 아래에 놓인다.
                    float cardHeight = CardHeight(message);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader - cardHeight);
                    if (cardHeight > 0f)
                    {
                        row.Card.anchoredPosition = new Vector2(104f * layoutScale, -(38f * layoutScale + extraHeader + bubbleHeight + 6f * layoutScale));
                        row.Card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(CardWidthPixels * layoutScale, width + 40f * layoutScale));
                    }
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(GameLocale.Text(row.Text.text), width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
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
            if (SessionClock.Time >= nextAngerRefresh)
            {
                nextAngerRefresh = SessionClock.Time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null)
                    {
                        // 말하는 종목을 실제로 들고 있던 사람에게는 '주주' 표시를 붙인다.
                        var facts = row.SocialMessage == null ? null : row.SocialMessage.facts;
                        bool holder = facts != null && facts.held && !string.IsNullOrEmpty(facts.symbol);
                        DeviceTerminalUI.Set(row.Nickname, name + (holder ? " <size=70%><color=#6FA8E6>" + GameLocale.Text("주주") + "</color></size>" : "") + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : ""));
                    }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(SessionClock.Time * 5f) * .5f + .5f) : 1f;
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
                    GameLocalizedText.Set(profile,"참여자가 퇴장했습니다.");
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    GameLocalizedText.Set(profile,$"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}");
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
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(GameLocale.Text(message.Nickname), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = message.IsRoomNotice ? Mathf.Max(rowHeight, 52f * layoutScale) : Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(DisplayMessage(message), width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader + CardHeight(message);
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in displayedMessages.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
            displayedMessages.Remove(sequence);
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
            GameLocalizedText.Set(unread,SelectedRoom > 0 ? Room.DisplayName : monitorChat ? "시장 라운지" : count > 0 ? $"새 메시지 {count}" : "시장 라운지");
        }
    }

    void OnEnable()
    {
        dirty = true;
        lastSecond = -1;
        mentionRosterDirty = true;
        StockManager.OnStockListed += InvalidateMentionRoster;
        StockManager.OnStockDelisted += InvalidateMentionRoster;
    }

    void OnDisable()
    {
        StockManager.OnStockListed -= InvalidateMentionRoster;
        StockManager.OnStockDelisted -= InvalidateMentionRoster;
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
