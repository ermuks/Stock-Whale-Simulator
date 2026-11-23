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
        /// <summary>바로 윗줄과 같은 사람이 이어서 쓴 줄인가. 그러면 얼굴과 이름 없이 말만 보인다.</summary>
        public bool Continued;
        /// <summary>내 글에 단 답인가. 그러면 줄 전체에 강조 띠를 깔고, 위에 내가 쓴 글을 한 줄로 인용한다.</summary>
        public bool ToMe;
        public Image ReplyBand; public TextMeshProUGUI ReplyQuote;
        public RectTransform[] Header; public float[] HeaderY;
        // 손익 인증 카드
        public RectTransform Card;
        public Image CardAccent;
        public TextMeshProUGUI CardTitle, CardKind, CardAmount, CardDetail;
        // 종목 분석 카드
        public RectTransform Analysis;
        public Image GaugeTrack, GaugeFill;
        public TextMeshProUGUI AnalysisTitle, AnalysisScore, AnalysisVerdict;
        public readonly TextMeshProUGUI[] AnalysisCaptions = new TextMeshProUGUI[4], AnalysisValues = new TextMeshProUGUI[4];
        public bool HasCard;
        // 상호작용 단추들: 커서를 올린 줄의 오른쪽 끝에만 보인다. 0 초대, 1 경고, 2 채팅 금지, 3 추방, 4 메시지 삭제.
        public Button Invite;
        public readonly Button[] Actions = new Button[ActionNames.Length];
        public float ActionY;
    }

    /// <summary>채팅 줄의 + 단추를 눌렀을 때. 방 화면이 연결한다. 연결되지 않은 화면에는 단추가 나오지 않는다.</summary>
    public System.Action<int> InviteRequested;
    /// <summary>화면 점검용: 커서가 없어도 모든 줄의 단추를 보여 준다.</summary>
    public bool AlwaysShowActions;
    // 플레이어 자신의 말에는 닉네임 대신 이 이름을 쓴다.
    private static string PlayerLabel => GameLocale.Code == "ja" ? "自分" : GameLocale.Code == "en" ? "me" : BotChatMessage.PlayerName;
    // 말풍선 오른쪽에 초대 단추가 들어갈 자리.
    private const float InviteSize = 44f, InviteGap = 8f;
    private Canvas pointerCanvas;

    private static readonly string[] ActionNames = { "invite", "warn", "mute", "kick", "delete" };
    private static readonly Color[] ActionColors = { new(.16f, .43f, .47f, 1f), new(.55f, .43f, .14f, 1f), new(.27f, .33f, .42f, 1f), new(.58f, .20f, .22f, 1f), new(.22f, .25f, .29f, 1f) };
    /// <summary>방장이 가린 메시지들(이번 실행 동안만).</summary>
    private static readonly HashSet<long> removedMessages = new();
    private static string RemovedText => GameLocale.Code == "ja" ? "ホストが削除したメッセージです" : GameLocale.Code == "en" ? "Message removed by the host" : "방장이 가린 메시지입니다";
    private void BuildInviteButton(Row row)
    {
        for (int i = 0; i < ActionNames.Length; i++)
        {
            int index = i;
            var item = new GameObject("Button :: Chat " + ActionNames[i], typeof(RectTransform), typeof(Image), typeof(Button));
            item.layer = row.Rect.gameObject.layer; item.transform.SetParent(row.Rect, false);
            var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = itemRect.anchorMax = new Vector2(1f, 1f); itemRect.pivot = new Vector2(1f, .5f);
            itemRect.sizeDelta = Vector2.one * (InviteSize * layoutScale);
            var plate = item.GetComponent<Image>(); var shape = row.Bubble.GetComponent<Image>();
            plate.sprite = shape.sprite; plate.type = Image.Type.Sliced; plate.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
            plate.color = ActionColors[i]; plate.raycastTarget = true;
            var glyph = new GameObject("Image :: Chat Action Icon", typeof(RectTransform), typeof(Image)); glyph.layer = item.layer; glyph.transform.SetParent(item.transform, false);
            var glyphRect = (RectTransform)glyph.transform; glyphRect.anchorMin = new Vector2(.18f, .18f); glyphRect.anchorMax = new Vector2(.82f, .82f); glyphRect.offsetMin = glyphRect.offsetMax = Vector2.zero;
            var picture = glyph.GetComponent<Image>(); picture.sprite = ChatActionIcons.Get(ActionNames[i]); picture.color = CardInk; picture.raycastTarget = false;
            row.Actions[i] = item.GetComponent<Button>(); row.Actions[i].targetGraphic = plate;
            row.Actions[i].onClick.AddListener(() => RunAction(row, index));
            item.SetActive(false);
        }
        row.Invite = row.Actions[0];
    }
    private void RunAction(Row row, int index)
    {
        if (!ActionAllowed(row, index)) { return; }
        if (index == 0) { InviteRequested.Invoke(row.BotId); return; }
        if (index == 4)
        {
            removedMessages.Add(row.Sequence); displayedMessages.Remove(row.Sequence);
            row.Sequence = -1; version = -1; dirty = true;
            return;
        }
        InvestorSociety.Instance?.Moderate(SelectedRoom, row.BotId, (SocialModeration)(index - 1), out _);
    }
    /// <summary>내가 방장인 방이고, 그 봇이 지금 그 방에 있는가.</summary>
    private bool CanModerate(Row row, bool memberOnly)
    {
        var society = InvestorSociety.Instance;
        if (society == null || row.IsNotice || row.BotId <= 0 || SelectedRoom < 1 || SelectedRoom > InvestorSociety.PlayerRoomCount || !society.IsRoomOpen(SelectedRoom)) { return false; }
        if (society.CanModerateRoom != null && !society.CanModerateRoom(SelectedRoom)) { return false; }
        return !memberOnly || society.SocialBots.TryGetValue(row.BotId, out var bot) && bot.Social.Psychology.Room == SelectedRoom;
    }
    private bool ActionAllowed(Row row, int index)
    {
        if (!row.Rect.gameObject.activeSelf) { return false; }
        if (index == 0) { return CanInvite(row); }
        if (index == 4) { return CanModerate(row, false) && row.Sequence >= 0 && !removedMessages.Contains(row.Sequence); }
        return CanModerate(row, true);
    }
    /// <summary>지금 방에서 줄마다 나올 수 있는 단추의 수(말풍선이 그만큼 자리를 비운다).</summary>
    private int ActionSlots => SelectedRoom >= 1 && SelectedRoom <= InvestorSociety.PlayerRoomCount ? 4 : 1;
    // 공용 라운지에 있고 아직 어느 방에도 들어가지 않은 사람만 초대할 수 있다.
    private bool CanInvite(Row row)
    {
        if (InviteRequested == null || SelectedRoom != 0 || row.IsNotice || row.BotId <= 0 || !row.Rect.gameObject.activeSelf)
        { return false; }
        var bot = day.FindBot(row.BotId);
        return bot != null && bot.Social != null && bot.isActiveAndEnabled && !bot.IsExchangeParticipant && bot.Social.Psychology.Room == 0;
    }

    private void RefreshInviteButtons()
    {
        if (pointerCanvas == null) { pointerCanvas = scroll.GetComponentInParent<Canvas>(); }
        Camera eye = pointerCanvas == null || pointerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pointerCanvas.worldCamera != null ? pointerCanvas.worldCamera : Camera.main;
        Vector2 pointer = Input.mousePosition;
        bool inside = chatSelected && scrollInput != null && !scrollInput.Dragging && RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, eye);
        foreach (var row in rows)
        {
            bool hovered = row.Rect.gameObject.activeSelf && (AlwaysShowActions || inside && RectTransformUtility.RectangleContainsScreenPoint(row.Rect, pointer, eye));
            // 보이는 단추를 오른쪽 끝에서부터 채운다: 삭제가 맨 끝, 그 왼쪽으로 추방·채팅 금지·경고(또는 초대).
            int slot = 0;
            for (int i = row.Actions.Length - 1; i >= 0; i--)
            {
                bool show = hovered && ActionAllowed(row, i); var item = row.Actions[i].gameObject;
                if (item.activeSelf != show) { item.SetActive(show); }
                if (!show) { continue; }
                ((RectTransform)item.transform).anchoredPosition = new Vector2(-(14f + slot * (InviteSize + InviteGap)) * layoutScale, row.ActionY);
                slot++;
            }
        }
    }

    // 글자 줄 높이(글자 크기의 약 1.5배)보다 칸이 낮으면 말줄임 처리로 글자가 통째로 사라진다. 칸 높이를 넉넉히 둔다.
    private const float CardBlockHeight = 118f, CardHeightPixels = 110f, CardWidthPixels = 430f;
    private static readonly Color CardBack = new(.09f, .12f, .15f, .96f), CardInk = new(.92f, .94f, .96f, 1f), CardMuted = new(.60f, .66f, .71f, 1f);

    private float CardHeight(BotChatMessage message)
    {
        if (message.IsRoomNotice || message.Social == null || removedMessages.Contains(message.Sequence)) { return 0f; }
        if (message.Social.analysisCard != null) { return AnalysisBlockHeight * layoutScale; }
        return message.Social.profitCard == null ? 0f : CardBlockHeight * layoutScale;
    }

    // ── 종목 분석 카드: 왼쪽에 아래가 열린 고리 게이지(매수 추천 점수), 오른쪽에 당일 시가·현재가·고가·저가 ──
    public const string AnalysisTemplate = "AI.ANALYSIS";
    private const float AnalysisBlockHeight = 170f, AnalysisHeightPixels = 162f, GaugeSweep = .75f;
    private static Sprite analysisRing, analysisAvatar;
    private static Sprite LoadChatSprite(string name)
    {
        var texture = Resources.Load<Texture2D>("UI/Chat/" + name);
        return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
    }
    private static string AnalysisWord(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>점수(0~100)를 말로: 75 이상 강한 매수, 58 이상 매수, 42 이상 관망, 25 이상 매도, 그 아래 강한 매도.</summary>
    public static string AnalysisVerdict(int score) => score >= 75 ? AnalysisWord("강한 매수", "Strong buy", "強い買い") : score >= 58 ? AnalysisWord("매수", "Buy", "買い")
        : score >= 42 ? AnalysisWord("관망", "Hold", "様子見") : score >= 25 ? AnalysisWord("매도", "Sell", "売り") : AnalysisWord("강한 매도", "Strong sell", "強い売り");
    private Image GaugeArc(RectTransform parent, string name, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.layer = parent.gameObject.layer; item.transform.SetParent(parent, false);
        var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(74f, -74f) * layoutScale; rect.sizeDelta = new Vector2(118f, 118f) * layoutScale;
        // 고리의 4분의 3만 채운다. 채움은 아래에서 시작해 시계 방향으로 도는데, 그림을 45도 돌려 두면 빈 4분의 1이 정확히 아래에 온다.
        rect.localEulerAngles = new Vector3(0f, 0f, -45f);
        var image = item.GetComponent<Image>(); image.sprite = analysisRing; image.color = color; image.raycastTarget = false;
        image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Radial360; image.fillOrigin = (int)Image.Origin360.Bottom; image.fillClockwise = true; image.fillAmount = GaugeSweep;
        return image;
    }
    private void BuildAnalysisVisual(Row row)
    {
        if (analysisRing == null) { analysisRing = LoadChatSprite("AnalysisRing"); }
        var root = new GameObject("Panel :: Analysis Card", typeof(RectTransform), typeof(Image));
        root.layer = row.Rect.gameObject.layer; root.transform.SetParent(row.Rect, false);
        row.Analysis = (RectTransform)root.transform;
        row.Analysis.anchorMin = row.Analysis.anchorMax = row.Analysis.pivot = new Vector2(0f, 1f);
        row.Analysis.sizeDelta = new Vector2(CardWidthPixels, AnalysisHeightPixels) * layoutScale;
        var back = root.GetComponent<Image>(); back.color = CardBack; back.raycastTarget = false;
        row.GaugeTrack = GaugeArc(row.Analysis, "Image :: Gauge Track", new Color(.20f, .25f, .30f, 1f));
        row.GaugeFill = GaugeArc(row.Analysis, "Image :: Gauge Fill", CardInk);
        row.AnalysisScore = CardLabel(row.Analysis, "Text :: Analysis Score", 14f, 44f, 120f, 60f, 34f, TextAlignmentOptions.Center, CardInk, FontStyles.Bold);
        row.AnalysisVerdict = CardLabel(row.Analysis, "Text :: Analysis Verdict", 4f, 122f, 140f, 34f, 19f, TextAlignmentOptions.Center, CardInk, FontStyles.Bold);
        row.AnalysisTitle = CardLabel(row.Analysis, "Text :: Analysis Stock", 158f, 8f, 258f, 34f, 21f, TextAlignmentOptions.MidlineLeft, CardInk);
        for (int i = 0; i < 4; i++)
        {
            row.AnalysisCaptions[i] = CardLabel(row.Analysis, "Text :: Analysis Caption " + i, 158f, 46f + i * 27f, 90f, 26f, 17f, TextAlignmentOptions.MidlineLeft, CardMuted);
            row.AnalysisValues[i] = CardLabel(row.Analysis, "Text :: Analysis Value " + i, 248f, 46f + i * 27f, 168f, 26f, 19f, TextAlignmentOptions.MidlineRight, CardInk);
        }
        root.SetActive(false);
    }
    private void BindAnalysis(Row row, ChatAnalysisCard card)
    {
        if (row.Analysis == null) { return; }
        row.Analysis.gameObject.SetActive(card != null);
        if (card == null) { return; }
        bool report = !string.IsNullOrEmpty(card.kind);
        // 줄을 돌려쓰므로 칸 너비를 그때그때 맞춘다: 보고서 카드는 줄 이름(종목 이름)이 길고 값이 짧다.
        float captionWidth = report && card.captionWidth > 0 ? Mathf.Clamp(card.captionWidth, 60, 200) : 90f;
        for (int i = 0; i < 4; i++)
        {
            row.AnalysisCaptions[i].rectTransform.sizeDelta = new Vector2(captionWidth, 26f) * layoutScale;
            var value = row.AnalysisValues[i].rectTransform;
            value.anchoredPosition = new Vector2(158f + captionWidth, -(46f + i * 27f)) * layoutScale; value.sizeDelta = new Vector2(258f - captionWidth, 26f) * layoutScale;
        }
        // 고리 안의 글이 길면("100%", "-3.0%") 글자를 줄여 고리 안에 넣는다.
        int scoreLength = report && !string.IsNullOrEmpty(card.scoreText) ? card.scoreText.Length : 2;
        row.AnalysisScore.fontSize = (scoreLength <= 3 ? 34f : scoreLength == 4 ? 26f : 21f) * layoutScale;
        if (report) { BindReport(row, card); return; }
        int score = Mathf.Clamp(card.score, 0, 100);
        // 사는 쪽이면 오름 색, 파는 쪽이면 내림 색, 가운데면 흐린 글자색.
        var tone = score >= 58 ? MarketColors.ForChange(1m, CardInk) : score < 42 ? MarketColors.ForChange(-1m, CardInk) : new Color(.86f, .78f, .46f, 1f);
        row.GaugeFill.fillAmount = GaugeSweep * score / 100f; row.GaugeFill.color = tone;
        GameLocalizedText.Set(row.AnalysisScore, score.ToString(), true); row.AnalysisScore.color = tone;
        GameLocalizedText.Set(row.AnalysisVerdict, AnalysisVerdict(score), true); row.AnalysisVerdict.color = tone;
        GameLocalizedText.Set(row.AnalysisTitle, GameLocale.Text(card.stockName ?? card.symbol ?? ""), true);
        string[] captions = { AnalysisWord("시가", "Open", "始値"), AnalysisWord("현재가", "Last", "現在値"), AnalysisWord("고가", "High", "高値"), AnalysisWord("저가", "Low", "安値") };
        decimal[] values = { card.open, card.price, card.high, card.low };
        for (int i = 0; i < 4; i++)
        {
            GameLocalizedText.Set(row.AnalysisCaptions[i], captions[i], true);
            GameLocalizedText.Set(row.AnalysisValues[i], PriceTickPolicy.FormatMoney(values[i]), true);
            row.AnalysisValues[i].color = i == 0 || card.open <= 0m ? CardInk : MarketColors.ForChange(values[i] - card.open, CardInk);
        }
    }

    private static readonly Color CardAmber = new(.86f, .78f, .46f, 1f), CardAlert = new(.96f, .47f, .30f, 1f);
    private static Color ReportTone(int tone, int score)
        => tone == 1 ? MarketColors.ForChange(1m, CardInk) : tone == -1 ? MarketColors.ForChange(-1m, CardInk) : tone == 4 ? CardAlert : tone == 3 ? CardInk
            : tone == 2 ? (score >= 58 ? MarketColors.ForChange(1m, CardInk) : score < 42 ? MarketColors.ForChange(-1m, CardInk) : CardAmber) : CardAmber;
    /// <summary>AI의 보고서 카드: 고리(0~100)와 그 안의 글, 아래의 한마디, 제목, 네 줄.</summary>
    private void BindReport(Row row, ChatAnalysisCard card)
    {
        int score = Mathf.Clamp(card.score, 0, 100); var tone = ReportTone(card.tone, score);
        row.GaugeFill.fillAmount = GaugeSweep * score / 100f; row.GaugeFill.color = tone;
        GameLocalizedText.Set(row.AnalysisScore, string.IsNullOrEmpty(card.scoreText) ? score.ToString() : card.scoreText, true); row.AnalysisScore.color = tone;
        GameLocalizedText.Set(row.AnalysisVerdict, card.badge ?? "", true); row.AnalysisVerdict.color = tone;
        GameLocalizedText.Set(row.AnalysisTitle, card.title ?? "", true);
        for (int i = 0; i < 4; i++)
        {
            GameLocalizedText.Set(row.AnalysisCaptions[i], card.captions != null && i < card.captions.Length ? card.captions[i] ?? "" : "", true);
            GameLocalizedText.Set(row.AnalysisValues[i], card.values != null && i < card.values.Length ? card.values[i] ?? "" : "", true);
            int each = card.tones != null && i < card.tones.Length ? card.tones[i] : 0;
            row.AnalysisValues[i].color = each == 0 ? CardInk : ReportTone(each, 50);
        }
    }

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
    /// <summary>이 시간(게임 분) 안에 같은 사람이 이어 쓴 글은 한 묶음으로 본다. 실제 시간으로 1분쯤이다.</summary>
    private const int GroupMinutes = 30;
    /// <summary>이어 쓴 줄의 말풍선 위·아래 여백.</summary>
    private const float ContinuedTop = 2f, ContinuedBottom = 4f;
    private readonly Dictionary<long, bool> measuredContinued = new();
    /// <summary>내 글에 단 답 위에 붙는 인용 줄의 높이.</summary>
    private const float QuoteHeight = 30f;
    private static readonly Color ReplyBandColor = new(1f, .74f, .22f, .11f), ReplyBarColor = new(1f, .72f, .20f, 1f), ReplyQuoteColor = new(.80f, .78f, .70f, 1f);
    /// <summary>내 글에 단 답인가. 봇이 내게 하는 말은 언제나 나를 부르는 멘션(@당신)으로 시작한다. 봇끼리 주고받는 답은 강조하지 않는다.</summary>
    private bool RepliesToMe(BotChatMessage message)
    {
        if (message.FromPlayer || message.IsRoomNotice || removedMessages.Contains(message.Sequence)) { return false; }
        string text = DisplayMessage(message);
        foreach (string you in InvestorSociety.YouMentions) { if (text.StartsWith(ChatSocialMentions.MarkOpen + you, System.StringComparison.Ordinal)) { return true; } }
        return false;
    }
    /// <summary>
    /// 내 글을 인용해 보여 줄 답인가. 내 글에 단 답(답글 번호가 있는 것)만 인용한다.
    /// 봇이 먼저 거는 말, 되묻는 말, 토론에서 나를 끌어들이는 말은 나를 부르기만 할 뿐 내 글에 대한 답이 아니다.
    /// 예전에는 이런 말에도 바로 앞의 내 글을 인용해서, 상관없는 글에 엉뚱한 답이 달린 것처럼 보였다.
    /// </summary>
    private bool QuotesMe(BotChatMessage message) => RepliesToMe(message) && message.Social != null && message.Social.replyToId != 0;
    /// <summary>그 답이 가리키는 내 글. 번호로 찾고, 못 찾으면 바로 앞의 내 글을 쓴다.</summary>
    private string QuoteFor(int index)
    {
        var message = Message(index); long wanted = message.Social == null ? 0 : message.Social.replyToId; string nearest = null;
        for (int i = index - 1; i >= 0 && i >= index - 150; i--)
        {
            var earlier = Message(i);
            if (!earlier.FromPlayer) { continue; }
            if (wanted != 0 && earlier.Social != null && earlier.Social.id == wanted) { nearest = LocalizedChat.Text(earlier); break; }
            if (nearest == null) { nearest = LocalizedChat.Text(earlier); if (wanted == 0) { break; } }
        }
        if (string.IsNullOrEmpty(nearest)) { return ""; }
        if (nearest.Length > 34) { nearest = nearest.Substring(0, 33) + "…"; }
        return "@" + PlayerLabel + "  " + ChatSocialMentions.Literal(nearest);
    }
    private void BuildReplyVisual(Row row)
    {
        var band = new GameObject("Image :: Reply Highlight", typeof(RectTransform), typeof(Image)) { layer = row.Rect.gameObject.layer };
        var bandRect = (RectTransform)band.transform; bandRect.SetParent(row.Rect, false); bandRect.SetAsFirstSibling();
        bandRect.anchorMin = Vector2.zero; bandRect.anchorMax = Vector2.one; bandRect.offsetMin = new Vector2(0f, 2f * layoutScale); bandRect.offsetMax = Vector2.zero;
        row.ReplyBand = band.GetComponent<Image>(); row.ReplyBand.color = ReplyBandColor; row.ReplyBand.raycastTarget = false;
        var bar = new GameObject("Image :: Reply Accent", typeof(RectTransform), typeof(Image)) { layer = band.layer };
        var barRect = (RectTransform)bar.transform; barRect.SetParent(bandRect, false);
        barRect.anchorMin = Vector2.zero; barRect.anchorMax = new Vector2(0f, 1f); barRect.pivot = new Vector2(0f, .5f); barRect.sizeDelta = new Vector2(5f * layoutScale, 0f); barRect.anchoredPosition = Vector2.zero;
        bar.GetComponent<Image>().color = ReplyBarColor; bar.GetComponent<Image>().raycastTarget = false;
        row.ReplyQuote = Instantiate(row.Time, row.Rect); row.ReplyQuote.name = "Text :: Reply Quote";
        row.ReplyQuote.raycastTarget = false; row.ReplyQuote.enableAutoSizing = false; row.ReplyQuote.fontSize = row.Nickname.fontSize * .82f; row.ReplyQuote.color = ReplyQuoteColor;
        row.ReplyQuote.alignment = TextAlignmentOptions.MidlineLeft; row.ReplyQuote.textWrappingMode = TextWrappingModes.NoWrap; row.ReplyQuote.overflowMode = TextOverflowModes.Ellipsis; row.ReplyQuote.richText = true;
        var quote = row.ReplyQuote.rectTransform; quote.anchorMin = quote.anchorMax = quote.pivot = new Vector2(0f, 1f);
        quote.anchoredPosition = new Vector2(104f * layoutScale, -4f * layoutScale); quote.sizeDelta = new Vector2(520f * layoutScale, (QuoteHeight - 6f) * layoutScale);
        // 인용 줄이 붙으면 얼굴·이름·시각이 그만큼 내려간다. 원래 자리를 적어 둔다.
        row.Header = new[] { row.Avatar.rectTransform, row.Gauge.rectTransform, row.Nickname.rectTransform, row.Time.rectTransform, row.AngerDot.rectTransform };
        row.HeaderY = new float[row.Header.Length];
        for (int i = 0; i < row.Header.Length; i++) { row.HeaderY[i] = row.Header[i].anchoredPosition.y; }
        band.SetActive(false); row.ReplyQuote.gameObject.SetActive(false);
    }
    /// <summary>
    /// 바로 윗줄과 같은 사람이 이어서 쓴 줄인가. 그러면 얼굴·이름·시각을 다시 띄우지 않고 말풍선만 아래에 붙인다(메신저의 묶음 표시).
    /// 안내 줄, 날이 바뀐 줄, 한참 뒤에 쓴 줄은 묶지 않는다.
    /// </summary>
    private bool Continues(int index)
    {
        if (index <= 0 || index >= MessageCount) { return false; }
        var message = Message(index); var above = Message(index - 1);
        // 내 글에 단 답은 묶지 않는다. 누가 답했는지 이름이 보여야 한다.
        if (RepliesToMe(message)) { return false; }
        if (message.IsRoomNotice || above.IsRoomNotice || message.FromPlayer != above.FromPlayer || message.BotId != above.BotId || message.Day != above.Day) { return false; }
        if (message.Minute < above.Minute || message.Minute - above.Minute > GroupMinutes) { return false; }
        // 봇 번호가 없는 말(내 글, 분석 AI, 지나가는 사람)은 이름까지 같아야 같은 사람이다.
        return message.BotId != 0 || message.TemplateId == above.TemplateId && message.Nickname == above.Nickname;
    }
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
        // 채팅 아래의 날짜·시각 줄은 쓰지 않는다(시각은 벽시계와 태블릿에 있다).
        if (status != null) { status.gameObject.SetActive(false); }
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
        // 내 리딩방에서는 목록이 더 길어진다. 그만큼의 줄을 미리 만들어 둔다.
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 4, 1, rowPoolLimit);
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
            // 참여자 관리(경고·채팅 금지·추방)는 줄 오른쪽 끝의 단추로 옮겼다.
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowBot(row.BotId); });
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
            BuildAnalysisVisual(row);
            BuildInviteButton(row);
            BuildReplyVisual(row);
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

    /// <summary>내가 글을 올린 뒤 맨 아래(가장 새 글)로 내려간다.</summary>
    public void ShowNewestFromOutside() { followNewest = true; dirty = true; }
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
        if (botId > 0) { SteamAchievements.Unlock(SteamAchievements.Profile); }
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
        { foreach (var bot in society.SocialBots.Values) { foreach (string name in bot.Social.Names) { names.Add(name); GameLocale.ProtectLiteral(name); } } }
        var aliases = new List<KeyValuePair<string, string>>();
        if (stocks != null)
        {
            foreach (var stock in stocks.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
                aliases.Add(new KeyValuePair<string, string>(stock.stockName, stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(GameLocale.Text(stock.stockName), stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(stock.symbol, stock.symbol));
                // 줄임말로 불러도 종목으로 이어진다.
                foreach (string nickname in StockNicknames.All(stock.symbol)) { aliases.Add(new KeyValuePair<string, string>(nickname, stock.symbol)); }
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
        if (removedMessages.Contains(message.Sequence)) { return RemovedText; }
        if (!displayedMessages.TryGetValue(message.Sequence, out string text))
        {
            string localized = LocalizedChat.Text(message); text = message.IsRoomNotice ? ChatSocialMentions.Literal(localized) : mentions.Format(localized);
            // 나를 가리키는 멘션(@당신 / @You / @あなた)은 다른 멘션처럼 칠한다.
            if (!message.IsRoomNotice)
            {
                foreach (string you in InvestorSociety.YouMentions)
                { if (text.StartsWith(you + " ", System.StringComparison.Ordinal)) { text = ChatSocialMentions.MarkOpen + you + "</mark>" + text.Substring(you.Length); break; } }
            }
            displayedMessages.Add(message.Sequence, text);
        }
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
        if (notice) { foreach (var action in row.Actions) { action.gameObject.SetActive(false); } }
        row.NoticeBackground.gameObject.SetActive(notice); row.NoticeText.gameObject.SetActive(notice);
        if (notice && row.Card != null) { row.HasCard = false; row.Card.gameObject.SetActive(false); }
        if (notice && row.Analysis != null) { row.Analysis.gameObject.SetActive(false); }
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
            GameLocalizedText.Set(hudStatus,$"{day.CalendarDate}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
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
                GameLocalizedText.Set(status,$"{day.CalendarDate}일 · {day.ClockMinute / 60:D2}:{day.ClockMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - (154f + ActionSlots * (InviteSize + InviteGap)) * layoutScale);
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
                        GameLocalizedText.Set(row.Nickname,message.FromPlayer ? PlayerLabel : message.Nickname,true);
                        GameLocalizedText.Set(row.Time,$"{MarketDayController.DateOf(message.Day)}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}");
                        GameLocalizedText.Set(row.Text,DisplayMessage(message),true);
                        bool removed = removedMessages.Contains(message.Sequence);
                        BindCard(row, removed || message.IsRoomNotice || message.Social == null ? null : message.Social.profitCard);
                        BindAnalysis(row, removed || message.IsRoomNotice || message.Social == null ? null : message.Social.analysisCard);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        // 종목 분석 AI는 제 얼굴이 있다.
                        if (message.TemplateId == AnalysisTemplate)
                        {
                            if (analysisAvatar == null) { analysisAvatar = LoadChatSprite("AnalysisAvatar"); }
                            if (analysisAvatar != null) { row.Avatar.sprite = analysisAvatar; }
                        }
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    {
                        row.Continued = false; row.ToMe = false;
                        if (row.ReplyBand.gameObject.activeSelf) { row.ReplyBand.gameObject.SetActive(false); row.ReplyQuote.gameObject.SetActive(false); }
                        LayoutNotice(row, message); continue;
                    }
                    // 내 글에 단 답: 강조 띠를 깔고, 위에 내가 쓴 글을 한 줄 인용한다. 얼굴과 이름은 그만큼 내려간다.
                    bool toMe = RepliesToMe(message), quotes = toMe && QuotesMe(message); float lift = quotes ? QuoteHeight * layoutScale : 0f;
                    row.ToMe = toMe;
                    if (row.ReplyBand.gameObject.activeSelf != toMe) { row.ReplyBand.gameObject.SetActive(toMe); }
                    if (row.ReplyQuote.gameObject.activeSelf != quotes) { row.ReplyQuote.gameObject.SetActive(quotes); }
                    if (quotes)
                    {
                        string quoted = QuoteFor(index);
                        if (row.ReplyQuote.text != quoted) { row.ReplyQuote.text = quoted; }
                        row.ReplyQuote.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(60f, width));
                    }
                    for (int part = 0; part < row.Header.Length; part++)
                    {
                        var place = row.Header[part].anchoredPosition; float wantedY = row.HeaderY[part] - lift;
                        if (!Mathf.Approximately(place.y, wantedY)) { row.Header[part].anchoredPosition = new Vector2(place.x, wantedY); }
                    }
                    // 같은 사람이 이어 쓴 줄은 얼굴·이름·시각을 숨기고 말풍선만 위로 붙인다.
                    bool continued = Continues(index);
                    row.Continued = continued;
                    if (row.Avatar.gameObject.activeSelf == continued)
                    {
                        row.Avatar.gameObject.SetActive(!continued); row.Gauge.gameObject.SetActive(!continued);
                        row.Nickname.gameObject.SetActive(!continued); row.Time.gameObject.SetActive(!continued);
                        if (continued) { row.AngerDot.gameObject.SetActive(false); }
                    }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(GameLocale.Text(row.Nickname.text), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = continued ? 0f : nicknameHeight - 30f * layoutScale;
                    // top: 줄의 위 끝에서 말풍선까지. 이름이 있는 줄은 이름 자리만큼, 이어 쓴 줄은 바로 붙는다.
                    float top = continued ? ContinuedTop * layoutScale : 38f * layoutScale + extraHeader + lift, bottom = continued ? ContinuedBottom * layoutScale : 10f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -top);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -top - 10f * layoutScale);
                    // 카드가 있으면 말풍선은 그만큼 짧아지고 카드는 말풍선 바로 아래에 놓인다.
                    float cardHeight = CardHeight(message);
                    float bubbleHeight = Mathf.Max((continued ? 44f : 52f) * layoutScale, rowHeights[index] - top - bottom - cardHeight);
                    if (cardHeight > 0f)
                    {
                        var below = message.Social != null && message.Social.analysisCard != null && row.Analysis != null ? row.Analysis : row.Card;
                        below.anchoredPosition = new Vector2(104f * layoutScale, -(top + bubbleHeight + 6f * layoutScale));
                        if (below == row.Card) { row.Card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(CardWidthPixels * layoutScale, width + 40f * layoutScale)); }
                    }
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(GameLocale.Text(row.Text.text), width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                    // 상호작용 단추는 줄의 오른쪽 끝, 말풍선 높이의 가운데.
                    row.ActionY = -top - bubbleHeight * .5f;
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            RefreshInviteButtons();
            if (SessionClock.Time >= nextAngerRefresh)
            {
                nextAngerRefresh = SessionClock.Time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice || row.Continued) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null)
                    {
                        // 말하는 종목을 실제로 들고 있던 사람에게는 '주주' 표시를 붙인다.
                        var facts = row.SocialMessage == null ? null : row.SocialMessage.facts;
                        bool holder = facts != null && facts.held && !string.IsNullOrEmpty(facts.symbol);
                        // 공개방에서는 이미 어느 리딩방에 들어가 있는 사람을 표시한다. 내 방 회원과 남의 방 회원을 색으로 나눈다.
                        var joined = SelectedRoom == 0 && mind.Room != 0 ? InvestorSociety.Instance?.GetRoom(mind.Room) : null;
                        string roomTag = joined == null ? "" : joined.PlayerOwned
                            ? " <size=70%><color=#48C4BC>" + GameLocale.Text("내 방") + "</color></size>"
                            : " <size=70%><color=#B48CE6>" + GameLocale.Text("리딩방") + "</color></size>";
                        DeviceTerminalUI.Set(row.Nickname, name + roomTag + (holder ? " <size=70%><color=#6FA8E6>" + GameLocale.Text("주주") + "</color></size>" : "") + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : ""));
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
            // 윗줄이 지워져 묶음의 첫 줄이 되면 이름이 다시 붙어야 하므로, 묶였는지가 바뀐 줄은 다시 잰다.
            bool continued = Continues(i);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height) || !measuredContinued.TryGetValue(message.Sequence, out bool was) || was != continued)
            {
                float words = message.IsRoomNotice ? 0f : messageMeasure.GetPreferredValues(DisplayMessage(message), width, Mathf.Infinity).y;
                if (message.IsRoomNotice) { height = Mathf.Max(rowHeight, 52f * layoutScale); }
                else if (continued) { height = Mathf.Max(44f * layoutScale, words + 20f * layoutScale) + (ContinuedTop + ContinuedBottom) * layoutScale + CardHeight(message); }
                else
                {
                    float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(GameLocale.Text(message.Nickname), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                    height = Mathf.Max(rowHeight, words + 68f * layoutScale) + extraHeader + CardHeight(message) + (QuotesMe(message) ? QuoteHeight * layoutScale : 0f);
                }
                measuredHeights[message.Sequence] = height; measuredContinued[message.Sequence] = continued;
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
            measuredHeights.Remove(sequence); measuredContinued.Remove(sequence);
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

    /// <summary>지금 켜져 있는 채팅 화면들. 씬 전체를 뒤지지 않고 채팅 화면을 찾을 때 쓴다.</summary>
    public static readonly System.Collections.Generic.List<TabletGroupChatUI> Live = new();

    void OnEnable()
    {
        if (!Live.Contains(this)) { Live.Add(this); }
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
        Live.Remove(this);
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
