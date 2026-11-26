using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 채팅 화면 하단: 참여자 카드와 방 카드.
// 예전에는 여러 줄 글자가 칸 없이 겹쳐 놓여 있었다. 여기서는 같은 정보를 칸·제목·값으로 나눠 배치한다.
// 씬은 건드리지 않는다. 시작할 때 기존 요소를 카드 안으로 옮기고, 한 덩어리로 찍던 글자는 숨긴다.

/// <summary>카드 UI를 만드는 도구. 위치는 부모 기준 비율(x, 위에서부터 y, 너비, 높이)로 준다.</summary>
public static class ChatFooterKit
{
    public static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(154, 169, 180, 255), Faint = new Color32(110, 125, 135, 255),
        Track = new Color32(38, 52, 59, 255), Teal = new Color32(72, 196, 188, 255), Rose = new Color32(232, 112, 122, 255), Amber = new Color32(230, 148, 78, 255),
        Cell = new Color(.72f, .78f, .84f, 1f), Chip = new Color(.95f, 1f, 1.05f, 1f);
    private static Sprite panel, rounded;
    public static Sprite PanelSprite => panel ??= Resources.Load<Sprite>("UI/TradingTerminal/Panel_Background");
    public static Sprite RoundedSprite => rounded ??= Resources.Load<Sprite>("UI/TradingTerminal/Chat_Bubble");

    public static RectTransform Place(RectTransform rect, Transform parent, float x, float y, float w, float h)
    {
        if (rect.parent != parent) { rect.SetParent(parent, false); }
        rect.anchorMin = new Vector2(x, 1 - y - h); rect.anchorMax = new Vector2(x + w, 1 - y); rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        return rect;
    }
    public static RectTransform Area(string name, Transform parent, float x, float y, float w, float h)
    {
        var item = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        return Place((RectTransform)item.transform, parent, x, y, w, h);
    }
    /// <summary>카드 바탕. 화면 바탕보다 한 단 밝고 가는 테두리가 있다.</summary>
    public static Image Card(string name, Transform parent, float x, float y, float w, float h)
        => Sliced(name, parent, x, y, w, h, PanelSprite, Color.white, 1f);
    /// <summary>카드 안의 값 칸.</summary>
    public static Image CellBox(string name, Transform parent, float x, float y, float w, float h)
        => Sliced(name, parent, x, y, w, h, RoundedSprite, Cell, 3f);
    public static Image Sliced(string name, Transform parent, float x, float y, float w, float h, Sprite sprite, Color color, float density)
    {
        var image = Area(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.type = Image.Type.Sliced; image.fillCenter = true; image.pixelsPerUnitMultiplier = density; image.color = color; image.raycastTarget = false;
        return image;
    }
    public static Image Flat(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var image = Area(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }
    public static TextMeshProUGUI Label(string name, Transform parent, TextMeshProUGUI like, float x, float y, float w, float h, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false)
    {
        var label = Area(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = like.font; label.fontSharedMaterial = like.font.material;
        Restyle(label, size, color, align, wrap);
        return label;
    }
    public static void Restyle(TextMeshProUGUI label, float size, Color color, TextAlignmentOptions align, bool wrap)
    {
        label.fontSize = size; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal; label.color = color; label.alignment = align; label.raycastTarget = false; label.richText = false;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.margin = Vector4.zero;
    }
    /// <summary>이미 그 언어로 확정한 글자를 넣는다. 게임의 번역 표를 다시 거치지 않는다.</summary>
    public static void Lit(TextMeshProUGUI label, string text)
    {
        text ??= "";
        if (label != null && !GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); }
    }
    /// <summary>글자 색으로 뜻을 전하는 작은 꼬리표.</summary>
    public static TextMeshProUGUI Tag(string name, Transform parent, TextMeshProUGUI like, float x, float y, float w, float h, float size)
    {
        var box = Sliced("Image :: " + name, parent, x, y, w, h, RoundedSprite, Chip, 5f);
        return Label("Text :: " + name, box.transform, like, 0, 0, 1, 1, size, Ink, TextAlignmentOptions.Center);
    }
    /// <summary>꼬리표를 글자 바로 뒤에 붙인다. 이름 길이가 달라도 떨어져 떠 있지 않게 한다. 꼬리표의 오른쪽 끝(부모 기준 비율)을 돌려준다.</summary>
    public static float Follow(TextMeshProUGUI label, TextMeshProUGUI tag, float gap, float padding)
    {
        var box = (RectTransform)tag.transform.parent; var parent = (RectTransform)box.parent; float width = parent.rect.width;
        if (width <= 0f) { return box.anchorMax.x; }
        label.ForceMeshUpdate(); tag.ForceMeshUpdate();
        float x = label.rectTransform.anchorMin.x + (Mathf.Min(label.preferredWidth, label.rectTransform.rect.width) + gap) / width, w = (tag.preferredWidth + padding * 2f) / width;
        box.anchorMin = new Vector2(x, box.anchorMin.y); box.anchorMax = new Vector2(x + w, box.anchorMax.y);
        return x + w;
    }
    public static float FollowTag(TextMeshProUGUI previous, TextMeshProUGUI tag, float gap, float padding)
    {
        var before = (RectTransform)previous.transform.parent; var box = (RectTransform)tag.transform.parent; float width = ((RectTransform)box.parent).rect.width;
        if (width <= 0f) { return box.anchorMax.x; }
        tag.ForceMeshUpdate();
        float x = before.anchorMax.x + gap / width, w = (tag.preferredWidth + padding * 2f) / width;
        box.anchorMin = new Vector2(x, box.anchorMin.y); box.anchorMax = new Vector2(x + w, box.anchorMax.y);
        return x + w;
    }
    /// <summary>누를 수 없는 단추는 글자도 흐리게 한다.</summary>
    public static void Dim(Button button)
    {
        if (button == null) { return; }
        var caption = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (caption != null) { caption.color = button.interactable ? Ink : Faint; }
    }
    public static void StyleButton(Button button, float size)
    {
        if (button == null) { return; }
        var caption = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (caption != null) { caption.fontSize = size; caption.enableAutoSizing = false; caption.alignment = TextAlignmentOptions.Center; caption.textWrappingMode = TextWrappingModes.NoWrap; }
    }
}

/// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다. 용어는 게임의 기존 번역과 맞췄다.</summary>
public static class ChatFooterText
{
    private static readonly Dictionary<string, (string en, string ja)> Table = new()
    {
        ["메시지를 눌러 참여자를 확인하세요"] = ("Tap a message to view that investor", "メッセージを押すと参加者を確認できます"),
        ["참여자"] = ("Investor", "参加者"),
        ["참여자 퇴장"] = ("Investor left", "参加者退出"),
        ["신뢰"] = ("Trust", "信頼"), ["배신감"] = ("Betrayal", "裏切り"), ["직접 경험"] = ("Results", "直接経験"),
        ["{0}승 · {1}패"] = ("{0} W · {1} L", "{0}勝 · {1}敗"),
        ["리딩방 신뢰"] = ("Room trust", "部屋への信頼"), ["모름"] = ("Unaware", "知らない"),
        ["충성"] = ("Loyal", "支持"), ["애증"] = ("Conflicted", "愛憎"), ["적대"] = ("Hostile", "敵対"), ["무관심"] = ("Indifferent", "無関心"),
        ["운영자"] = ("Host", "運営者"), ["체결 {0}건"] = ("{0} fills", "約定 {0}件"), ["실현 {0}"] = ("Realized {0}", "実現 {0}"),
        ["{0} 관측"] = ("Seen {0}", "{0} 観測"), ["매수"] = ("Buy", "買い"), ["매도"] = ("Sell", "売り"),
        ["매수 대기"] = ("Buy pending", "買い待機"), ["최근 매수 체결"] = ("Last buy fill", "直近の買い約定"), ["매도 대기"] = ("Sell pending", "売り待機"),
        ["신호 없음"] = ("No signal", "シグナルなし"),
        ["참여하면 운영자의 실제 주문과 대화를 볼 수 있습니다."] = ("Join to see the host's real orders and chat.", "参加すると運営者の実際の注文と会話が見られます。"),
        ["내 리딩방"] = ("My own room", "自分の部屋"), ["참여 {0}명"] = ("{0} members", "参加 {0}人"),
        ["지시"] = ("Signal", "指示"),
        ["장 시작 몰아주기"] = ("Opening rush", "寄り付き集中"), ["예약 취소"] = ("Cancel rush", "予約取消"),
        ["장 시작 예약 · {0} · 회원 {1}까지 매수, 내 물량 매도"] = ("Queued for the open · {0} · members buy up to {1}, you sell", "寄り付き予約 · {0} · メンバーは{1}まで買い、自分は売り"),
        ["내 리딩방 탭에서 개설할 수 있습니다"] = ("Open it from the My own room tab", "「自分の部屋」タブから開設できます"),
        ["새 리딩방을 개설하세요"] = ("Open your own room", "自分の部屋を開設しましょう"),
        ["회원을 초대하고 매매 지시를 직접 내릴 수 있습니다."] = ("Invite members and send them your own calls.", "メンバーを招待して、自分で売買指示を出せます。"),
        ["개설 비용"] = ("Opening cost", "開設費用"), ["주문 가능 현금"] = ("Available cash", "注文可能な現金"),
        ["개설하기"] = ("Open room", "開設する"),
        ["리딩방 개설하기"] = ("Open my own room", "自分の部屋を開設する"),
        ["개설 조건"] = ("Requirements", "開設条件"), ["부족한 조건"] = ("What's missing", "足りない条件"),
        ["충족"] = ("Met", "達成"), ["부족"] = ("Short", "不足"),
        ["개설 비용 · 주문 가능 현금이 {0} 부족합니다"] = ("Opening cost · {0} more available cash needed", "開設費用 · 注文可能な現金が{0}足りません"),
        ["부족한 조건이 없습니다. 개설 비용은 주문 가능 현금에서 한 번만 냅니다."] = ("Nothing is missing. The cost is paid once from your available cash.", "足りない条件はありません。費用は注文可能な現金から一度だけ支払います。"),
        ["홍보하기"] = ("Promote", "宣伝する"), ["홍보 {0}초"] = ("Promote {0}s", "宣伝 {0}秒"),
        ["{0} 부족합니다"] = ("{0} short", "{0} 足りません"),
        ["주문 가능 현금에서 한 번만 냅니다."] = ("Paid once from your available cash.", "注文可能な現金から一度だけ支払います。"),
        ["라운지 화제"] = ("Lounge buzz", "ラウンジの話題"), ["{0}회"] = ("{0}×", "{0}回"), ["아직 화제가 없습니다"] = ("Nothing trending yet", "まだ話題はありません"),
        ["경각심"] = ("Alertness", "警戒"), ["위축"] = ("Inhibition", "萎縮"), ["활성 지시 없음"] = ("No active call", "有効な指示なし"),
        ["매수 ≤ {0}"] = ("Buy ≤ {0}", "買い ≤ {0}"), ["매도 ≥ {0}"] = ("Sell ≥ {0}", "売り ≥ {0}"),
        ["대기"] = ("Waiting", "待機"), ["보유"] = ("Held", "保有"), ["보류"] = ("Deferred", "保留"), ["거부"] = ("Refused", "拒否"), ["완료"] = ("Done", "達成"), ["취소"] = ("Canceled", "取消"),
    };
    public static string T(string korean)
        => GameLocale.Code == "ko" || !Table.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;
    public static string F(string korean, params object[] values) => string.Format(T(korean), values);
}

/// <summary>방 카드. 공용 라운지·내 리딩방·남의 리딩방마다 내용이 다르고, 틀과 자리는 같다.</summary>
public sealed class ChatFooterCards : MonoBehaviour
{
    // 화면 전체 기준 자리. 채팅 목록은 0.655에서 끝난다.
    public const float CardX = .025f, CardWidth = .95f, ProfileTop = .664f, ProfileHeight = .098f, RoomTop = .770f, RoomHeight = .192f, LoungeHeight = .192f, OwnShift = .075f;
    private LeadingRoomUI rooms;
    private TabletGroupChatUI chat;
    private RectTransform profilePanel, ownCard, npcCard, lobbyCard;
    private TextMeshProUGUI npcName, npcStats, npcStock, npcSeen, npcBuyCaption, npcBuyValue, npcSellCaption, npcSellValue, npcMessage, npcRole;
    private TextMeshProUGUI ownTitle, ownMembers, ownMood, ownCommand, ownBuyCaption, ownSellCaption, lobbyHint;
    private RectTransform messages;
    private float messagesBottom;
    private bool compact;
    private int shownRoom = -1;
    private GameObject lockedGroup, openGroup;
    private TextMeshProUGUI costCaption, costValue, roomCaption, roomValue, callCaption, callValue;
    private RectTransform costFill;
    // 개설 카드: 방을 열기 전에 내 리딩방을 누르면 나온다.
    private RectTransform openCard;
    private TextMeshProUGUI openTitle, openHint, openCostCaption, openCostValue, openCashCaption, openCashValue, openNote, openLabel, openRequirementHeading, openShortageHeading, openState;
    private Button openButton, promoteButton;
    private TextMeshProUGUI promoteLabel;
    public bool Built => built;
    // 라운지 화제: 지금 공용 라운지 대화에 가장 많이 오른 종목 셋. 화면에 보이는 대화를 센 것뿐이다.
    private const int TopicCount = 3, TopicWindow = 40;
    private TextMeshProUGUI topicHeading, topicEmpty;
    private readonly TextMeshProUGUI[] topicNames = new TextMeshProUGUI[TopicCount], topicValues = new TextMeshProUGUI[TopicCount];
    private readonly RectTransform[] topicFills = new RectTransform[TopicCount];
    private readonly GameObject[] topicColumns = new GameObject[TopicCount];
    private readonly Dictionary<string, int> topicTally = new();
    private readonly List<KeyValuePair<string, int>> topicRank = new();
    private GameObject npcCells;
    private static readonly string[] CountNames = { "대기", "보유", "보류", "거부", "완료", "취소" };
    private readonly int[] counts = new int[6];
    private float next;
    private bool built;

    public void Bind(LeadingRoomUI owner)
    {
        rooms = owner; chat = owner.chat;
        if (built || rooms == null || chat == null || chat.visualProfile == null || rooms.npcControls == null || rooms.playerControls == null || rooms.lobbyControls == null) { return; }
        var root = (RectTransform)rooms.transform; var like = chat.visualProfile.heading;
        profilePanel = (RectTransform)chat.visualProfile.transform;

        // 한 덩어리로 찍던 글자는 계속 갱신되지만 화면에는 내지 않는다.
        var legacy = ChatFooterKit.Area("Area :: Legacy Footer Text", root, 0, 0, 1, 1); legacy.gameObject.SetActive(false);
        foreach (var old in new[] { rooms.npcSummary, rooms.npcSignal, rooms.commandStatus }) { if (old != null) { old.transform.SetParent(legacy, false); } }

        BuildNpc(like); BuildOwn(like); BuildLobby(like); BuildOpen(like, root);

        // 초대는 참여자 카드의 머리줄 오른쪽에 둔다. 누구를 초대하는지가 바로 옆에 보인다.
        ChatFooterKit.Place((RectTransform)rooms.invite.transform, profilePanel, .70f, .11f, .276f, .27f);
        ChatFooterKit.StyleButton(rooms.invite, 36f);
        if (chat.status != null)
        {
            // 맨 아래 한 줄은 상태 표시줄.
            ChatFooterKit.Place(chat.status.rectTransform, root, CardX + .012f, .968f, CardWidth - .024f, .024f);
            ChatFooterKit.Restyle(chat.status, 32f, ChatFooterKit.Faint, TextAlignmentOptions.MidlineLeft, false);
        }
        built = true; Refresh();
    }

    private RectTransform CardArea(string name, GameObject controls)
    {
        var holder = (RectTransform)controls.transform;
        var card = ChatFooterKit.Area(name, holder, CardX, RoomTop, CardWidth, RoomHeight);
        ChatFooterKit.Card("Panel :: Card", card, 0, 0, 1, 1);
        return card;
    }
    private void BuildNpc(TextMeshProUGUI like)
    {
        npcCard = CardArea("Area :: NPC Card", rooms.npcControls);
        // 머리줄: 누구의 방인가, 그 사람의 실적.
        npcName = ChatFooterKit.Label("Text :: Host Name", npcCard, like, .024f, .045f, .40f, .12f, 46f, ChatFooterKit.Ink);
        npcRole = ChatFooterKit.Tag("Host Role", npcCard, like, .43f, .065f, .11f, .08f, 28f); npcRole.color = ChatFooterKit.Muted;
        npcStats = ChatFooterKit.Label("Text :: Host Record", npcCard, like, .55f, .045f, .426f, .12f, 34f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineRight);
        // 신호 칸: 종목과 관측 시각, 그 아래 매수·매도 두 칸.
        var signal = ChatFooterKit.CellBox("Panel :: Host Signal", npcCard, .024f, .195f, .952f, .385f).transform;
        npcStock = ChatFooterKit.Label("Text :: Signal Stock", signal, like, .03f, .08f, .60f, .32f, 44f, ChatFooterKit.Ink);
        npcSeen = ChatFooterKit.Label("Text :: Signal Seen", signal, like, .64f, .08f, .33f, .32f, 32f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineRight);
        npcCells = ChatFooterKit.Area("Area :: Signal Sides", signal, 0, .44f, 1, .50f).gameObject;
        ChatFooterKit.Flat("Image :: Signal Divider", npcCells.transform, .03f, 0, .94f, .012f, ChatFooterKit.Track);
        ChatFooterKit.Flat("Image :: Side Divider", npcCells.transform, .499f, .16f, .002f, .70f, ChatFooterKit.Track);
        npcBuyCaption = ChatFooterKit.Label("Text :: Buy Caption", npcCells.transform, like, .03f, .14f, .20f, .74f, 32f, ChatFooterKit.Muted);
        npcBuyValue = ChatFooterKit.Label("Text :: Buy Value", npcCells.transform, like, .22f, .14f, .25f, .74f, 44f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        npcSellCaption = ChatFooterKit.Label("Text :: Sell Caption", npcCells.transform, like, .53f, .14f, .20f, .74f, 32f, ChatFooterKit.Muted);
        npcSellValue = ChatFooterKit.Label("Text :: Sell Value", npcCells.transform, like, .72f, .14f, .25f, .74f, 44f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        npcMessage = ChatFooterKit.Label("Text :: Signal Message", signal, like, .04f, .10f, .92f, .80f, 36f, ChatFooterKit.Muted, TextAlignmentOptions.Center, true);
        // 행동: 왼쪽이 주문 검토, 오른쪽 끝이 방 드나들기.
        ChatFooterKit.Place((RectTransform)rooms.npcBuy.transform, npcCard, .024f, .635f, .29f, .155f);
        ChatFooterKit.Place((RectTransform)rooms.npcSell.transform, npcCard, .328f, .635f, .29f, .155f);
        ChatFooterKit.Place((RectTransform)rooms.npcMembership.transform, npcCard, .716f, .635f, .26f, .155f);
        foreach (var button in new[] { rooms.npcBuy, rooms.npcSell, rooms.npcMembership }) { ChatFooterKit.StyleButton(button, 40f); }
        ChatFooterKit.Place(rooms.npcFeedback.rectTransform, npcCard, .024f, .835f, .952f, .10f);
        ChatFooterKit.Restyle(rooms.npcFeedback, 30f, ChatFooterKit.Faint, TextAlignmentOptions.MidlineLeft, false);
    }
    private void BuildOwn(TextMeshProUGUI like)
    {
        // 내 리딩방 카드는 네 줄이다: 머리줄, 가격 두 칸과 단추 두 개, 걸려 있는 지시, 방금 한 일. 줄인 만큼 채팅이 길어진다.
        var holder = (RectTransform)rooms.playerControls.transform;
        ownCard = ChatFooterKit.Area("Area :: Own Room Card", holder, CardX, RoomTop + OwnShift, CardWidth, RoomHeight - OwnShift);
        ChatFooterKit.Card("Panel :: Card", ownCard, 0, 0, 1, 1);
        var controls = rooms.playerControls.transform;
        // 머리줄: 방 이름, 인원, 방 분위기, 이용료.
        ownTitle = ChatFooterKit.Label("Text :: Room Title", ownCard, like, .024f, .04f, .30f, .20f, 44f, ChatFooterKit.Ink);
        ownMembers = ChatFooterKit.Tag("Room Members", ownCard, like, .29f, .075f, .15f, .13f, 28f); ownMembers.color = ChatFooterKit.Muted;
        ownMood = ChatFooterKit.Label("Text :: Room Mood", ownCard, like, .44f, .04f, .31f, .20f, 28f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineRight);
        foreach (var button in controls.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.parent != controls || !button.name.Contains("Room Fee")) { continue; }
            ChatFooterKit.Place((RectTransform)button.transform, ownCard, .776f, .05f, .20f, .18f); ChatFooterKit.StyleButton(button, 34f);
        }
        // 지시 한 줄: 매수 [가격] 매도 [가격] [지시] [취소]. 씬에 있던 긴 설명 글자는 쓰지 않는다.
        foreach (string old in new[] { "Text :: Buy Trigger Label", "Text :: Sell Trigger Label" })
        {
            var label = controls.Find(old);
            if (label != null) { label.gameObject.SetActive(false); }
        }
        ownBuyCaption = ChatFooterKit.Label("Text :: Buy Caption", ownCard, like, .024f, .29f, .07f, .27f, 30f, ChatFooterKit.Muted);
        ChatFooterKit.Place((RectTransform)rooms.buyPrice.transform, ownCard, .094f, .29f, .175f, .27f);
        ownSellCaption = ChatFooterKit.Label("Text :: Sell Caption", ownCard, like, .284f, .29f, .07f, .27f, 30f, ChatFooterKit.Muted);
        ChatFooterKit.Place((RectTransform)rooms.sellPrice.transform, ownCard, .354f, .29f, .175f, .27f);
        ChatFooterKit.Place((RectTransform)rooms.issue.transform, ownCard, .544f, .29f, .12f, .27f);
        ChatFooterKit.Place((RectTransform)rooms.cancel.transform, ownCard, .674f, .29f, .12f, .27f);
        ChatFooterKit.StyleButton(rooms.issue, 38f); ChatFooterKit.StyleButton(rooms.cancel, 38f);
        // 장 시작 몰아주기: 장이 열리기 전에 예약해 두면, 장이 열릴 때 회원들에게 +30%까지 사라는 지시가 나가고 내 물량은 그 매수에 넘어간다.
        rushButton = Instantiate(rooms.issue, ownCard);
        rushButton.name = "Button :: Opening Rush"; rushButton.gameObject.SetActive(true);
        rushButton.onClick = new Button.ButtonClickedEvent(); rushButton.onClick.AddListener(ToggleRush);
        ChatFooterKit.Place((RectTransform)rushButton.transform, ownCard, .804f, .29f, .172f, .27f);
        ChatFooterKit.StyleButton(rushButton, 32f);
        rushLabel = rushButton.GetComponentInChildren<TextMeshProUGUI>(true);
        // 셋째 줄은 걸려 있는 지시와 따르는 사람 수, 넷째 줄은 방금 한 일. 둘 다 카드 너비를 다 쓴다.
        ownCommand = ChatFooterKit.Label("Text :: Active Command", ownCard, like, .024f, .61f, .952f, .16f, 28f, ChatFooterKit.Ink);
        ChatFooterKit.Place(rooms.feedback.rectTransform, ownCard, .024f, .79f, .952f, .16f);
        ChatFooterKit.Restyle(rooms.feedback, 26f, ChatFooterKit.Faint, TextAlignmentOptions.MidlineLeft, false);
        messages = chat.scroll == null ? null : (RectTransform)chat.scroll.transform;
        if (messages != null) { messagesBottom = messages.anchorMin.y; }
    }
    /// <summary>방 카드가 없을 때(리딩방을 열기 전의 공용 라운지) 참여자 카드를 화면 맨 아래로 내리는 거리. 그만큼 채팅 목록이 길어진다.</summary>
    private const float NoCardShift = RoomTop + RoomHeight - ProfileHeight - ProfileTop;
    private float shifted;
    /// <summary>
    /// 아래쪽 카드가 작거나 없으면 그만큼 채팅 목록과 참여자 카드를 아래로 내려 빈 자리를 채운다.
    /// 내 리딩방에서는 카드가 작아진 만큼, 방 카드가 아예 없는 공용 라운지에서는 카드 높이만큼 내린다.
    /// 글 쓰는 칸은 채팅 목록의 아래 가장자리를 따라온다(PlayerChatBar가 맞춘다).
    /// </summary>
    private bool profileHidden;
    /// <summary>참여자를 아직 고르지 않았으면 참여자 카드를 접고 그 자리만큼 채팅 목록을 늘린다("메시지를 눌러…" 빈 상자가 자리를 차지했다).</summary>
    private void Compact(float shift, bool emptyProfile = false)
    {
        if (Mathf.Approximately(shifted, shift) && profileHidden == emptyProfile) { return; }
        shifted = shift; compact = shift > 0f; profileHidden = emptyProfile;
        ChatFooterKit.Place(profilePanel, profilePanel.parent, CardX, ProfileTop + shift, CardWidth, ProfileHeight);
        if (profilePanel.gameObject.activeSelf == emptyProfile) { profilePanel.gameObject.SetActive(!emptyProfile); }
        if (messages != null) { messages.anchorMin = new Vector2(messages.anchorMin.x, messagesBottom - shift - (emptyProfile ? ProfileHeight + .008f : 0f)); }
    }
    private void BuildOpen(TextMeshProUGUI like, RectTransform root)
    {
        // 방을 열기 전의 내 리딩방: 대화 자리 한가운데에 개설 단추, 그 아래 개설 조건과 부족한 조건.
        openCard = ChatFooterKit.Area("Area :: Open Room Card", root, .10f, .19f, .80f, .40f);
        ChatFooterKit.Card("Panel :: Card", openCard, 0, 0, 1, 1);
        openTitle = ChatFooterKit.Label("Text :: Open Title", openCard, like, .05f, .045f, .90f, .10f, 52f, ChatFooterKit.Ink, TextAlignmentOptions.Center);
        openHint = ChatFooterKit.Label("Text :: Open Hint", openCard, like, .05f, .15f, .90f, .07f, 30f, ChatFooterKit.Muted, TextAlignmentOptions.Center);
        // 단추는 지시 단추와 같은 모양을 쓴다.
        openButton = Instantiate(rooms.issue, openCard);
        openButton.name = "Button :: Open Room"; openButton.gameObject.SetActive(true);
        openButton.onClick = new Button.ButtonClickedEvent(); openButton.onClick.AddListener(OpenRoom);
        ChatFooterKit.Place((RectTransform)openButton.transform, openCard, .22f, .255f, .56f, .14f);
        ChatFooterKit.StyleButton(openButton, 46f);
        openLabel = openButton.GetComponentInChildren<TextMeshProUGUI>(true);
        // 개설 조건: 조건마다 한 칸. 윗줄은 필요한 것, 아랫줄은 지금 가진 것.
        ChatFooterKit.Flat("Image :: Requirement Divider", openCard, .05f, .435f, .90f, .004f, ChatFooterKit.Track);
        openRequirementHeading = ChatFooterKit.Label("Text :: Requirement Heading", openCard, like, .05f, .455f, .60f, .06f, 28f, ChatFooterKit.Muted);
        var row = ChatFooterKit.CellBox("Panel :: Requirement Cost", openCard, .05f, .525f, .90f, .185f).transform;
        openCostCaption = ChatFooterKit.Label("Text :: Requirement Caption", row, like, .04f, .10f, .36f, .40f, 36f, ChatFooterKit.Ink);
        openCostValue = ChatFooterKit.Label("Text :: Requirement Value", row, like, .40f, .10f, .37f, .40f, 38f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        openCashCaption = ChatFooterKit.Label("Text :: Holding Caption", row, like, .04f, .52f, .36f, .38f, 28f, ChatFooterKit.Muted);
        openCashValue = ChatFooterKit.Label("Text :: Holding Value", row, like, .40f, .52f, .37f, .38f, 32f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        openState = ChatFooterKit.Tag("Requirement State", row, like, .81f, .28f, .15f, .44f, 30f);
        // 부족한 조건: 무엇이 얼마나 모자라는지.
        ChatFooterKit.Flat("Image :: Shortage Divider", openCard, .05f, .745f, .90f, .004f, ChatFooterKit.Track);
        openShortageHeading = ChatFooterKit.Label("Text :: Shortage Heading", openCard, like, .05f, .765f, .60f, .06f, 28f, ChatFooterKit.Muted);
        openNote = ChatFooterKit.Label("Text :: Shortage", openCard, like, .05f, .83f, .90f, .12f, 34f, ChatFooterKit.Faint, TextAlignmentOptions.TopLeft, true);
        openCard.gameObject.SetActive(false);
    }
    private void OpenRoom()
    {
        var career = PlayerProgression.Instance;
        if (career == null || !career.OpenLeadingRooms()) { return; }
        rooms.RefreshNow(); next = 0f;
    }
    private void RefreshOpen(PlayerProgression career)
    {
        decimal cost = career.OpeningCost, cash = decimal.Floor(career.OpeningCash);
        bool enough = cash >= cost;
        ChatFooterKit.Lit(openTitle, ChatFooterText.T("내 리딩방"));
        ChatFooterKit.Lit(openHint, ChatFooterText.T("회원을 초대하고 매매 지시를 직접 내릴 수 있습니다."));
        ChatFooterKit.Lit(openLabel, ChatFooterText.T("리딩방 개설하기"));
        ChatFooterKit.Lit(openRequirementHeading, ChatFooterText.T("개설 조건"));
        ChatFooterKit.Lit(openCostCaption, ChatFooterText.T("개설 비용")); ChatFooterKit.Lit(openCostValue, DeviceTerminalUI.Money(cost));
        ChatFooterKit.Lit(openCashCaption, ChatFooterText.T("주문 가능 현금")); ChatFooterKit.Lit(openCashValue, DeviceTerminalUI.Money(cash));
        openCashValue.color = enough ? ChatFooterKit.Ink : ChatFooterKit.Rose;
        ChatFooterKit.Lit(openState, ChatFooterText.T(enough ? "충족" : "부족")); openState.color = enough ? ChatFooterKit.Teal : ChatFooterKit.Rose;
        ChatFooterKit.Lit(openShortageHeading, ChatFooterText.T("부족한 조건"));
        ChatFooterKit.Lit(openNote, enough ? ChatFooterText.T("부족한 조건이 없습니다. 개설 비용은 주문 가능 현금에서 한 번만 냅니다.")
            : ChatFooterText.F("개설 비용 · 주문 가능 현금이 {0} 부족합니다", DeviceTerminalUI.Money(decimal.Ceiling(cost - cash))));
        openNote.color = enough ? ChatFooterKit.Faint : ChatFooterKit.Rose;
        openButton.interactable = enough; ChatFooterKit.Dim(openButton);
    }
    private void BuildLobby(TextMeshProUGUI like)
    {
        // 공용 라운지 카드는 세 단: 내 방의 상태와 단추, 방을 열기까지 남은 조건(또는 열린 방의 요약), 지금 라운지의 화제.
        lobbyCard = (RectTransform)rooms.lobbyControls.transform;
        ChatFooterKit.Place(lobbyCard, lobbyCard.parent, CardX, RoomTop, CardWidth, LoungeHeight);
        ChatFooterKit.Card("Panel :: Card", lobbyCard, 0, 0, 1, 1).transform.SetAsFirstSibling();
        var icon = lobbyCard.Find("Image :: Personal Room") as RectTransform;
        if (icon != null) { ChatFooterKit.Place(icon, lobbyCard, .028f, .06f, .07f, .19f); }
        ChatFooterKit.Place(rooms.lobbyStatus.rectTransform, lobbyCard, .12f, .05f, .595f, .12f);
        ChatFooterKit.Restyle(rooms.lobbyStatus, 40f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineLeft, false);
        lobbyHint = ChatFooterKit.Label("Text :: Lounge Hint", lobbyCard, like, .12f, .165f, .595f, .09f, 28f, ChatFooterKit.Muted);
        if (rooms.growth != null)
        {
            ChatFooterKit.Place((RectTransform)rooms.growth.transform, lobbyCard, .736f, .07f, .24f, .17f);
            ChatFooterKit.StyleButton(rooms.growth, 38f);
        }
        ChatFooterKit.Flat("Image :: Lounge Divider", lobbyCard, .024f, .30f, .952f, .005f, ChatFooterKit.Track);
        lockedGroup = ChatFooterKit.Area("Area :: Opening Progress", lobbyCard, 0, .315f, 1, .27f).gameObject;
        costFill = Progress(lockedGroup.transform, like, .024f, .952f, out costCaption, out costValue);
        openGroup = ChatFooterKit.Area("Area :: Own Room Glance", lobbyCard, 0, .315f, 1, .27f).gameObject;
        Glance(openGroup.transform, like, .024f, .22f, out roomCaption, out roomValue);
        Glance(openGroup.transform, like, .256f, .47f, out callCaption, out callValue);
        // 홍보: 지금 보고 있는 공개방에 내 방을 알리는 글을 올린다.
        promoteButton = Instantiate(rooms.issue, openGroup.transform);
        promoteButton.name = "Button :: Promote Room"; promoteButton.gameObject.SetActive(true);
        promoteButton.onClick = new Button.ButtonClickedEvent(); promoteButton.onClick.AddListener(Promote);
        ChatFooterKit.Place((RectTransform)promoteButton.transform, openGroup.transform, .738f, .12f, .238f, .68f);
        ChatFooterKit.StyleButton(promoteButton, 38f);
        promoteLabel = promoteButton.GetComponentInChildren<TextMeshProUGUI>(true);
        ChatFooterKit.Flat("Image :: Topic Divider", lobbyCard, .024f, .60f, .952f, .005f, ChatFooterKit.Track);
        topicHeading = ChatFooterKit.Label("Text :: Topic Heading", lobbyCard, like, .024f, .625f, .50f, .08f, 28f, ChatFooterKit.Muted);
        topicEmpty = ChatFooterKit.Label("Text :: Topic Empty", lobbyCard, like, .024f, .73f, .952f, .16f, 32f, ChatFooterKit.Faint);
        for (int i = 0; i < TopicCount; i++)
        {
            // 참여자 카드의 세 칸과 같은 틀: 이름, 값, 막대.
            float x = .024f + i * .325f; const float width = .302f;
            var column = ChatFooterKit.Area("Area :: Topic " + i, lobbyCard, x, .715f, width, .23f); topicColumns[i] = column.gameObject;
            topicNames[i] = ChatFooterKit.Label("Text :: Topic Name", column, like, 0, 0, .70f, .50f, 36f, ChatFooterKit.Ink);
            topicValues[i] = ChatFooterKit.Label("Text :: Topic Mentions", column, like, .70f, 0, .30f, .50f, 30f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineRight);
            var track = ChatFooterKit.Flat("Image :: Topic Track", column, 0, .68f, 1, .13f, ChatFooterKit.Track);
            topicFills[i] = (RectTransform)ChatFooterKit.Flat("Image :: Topic Fill", track.transform, 0, 0, 1, 1, ChatFooterKit.Amber).transform;
        }
    }
    private void RefreshTopics()
    {
        ChatFooterKit.Lit(topicHeading, ChatFooterText.T("라운지 화제"));
        topicTally.Clear(); topicRank.Clear();
        var day = MarketDayController.Instance;
        if (day != null)
        {
            // 공용 라운지에 실제로 올라온 최근 대화만 센다. 봇의 속마음이나 진짜 가치는 읽지 않는다.
            for (int i = Mathf.Max(0, day.MessageCount - TopicWindow); i < day.MessageCount; i++)
            {
                var message = day.GetMessage(i);
                string name = message.IsRoomNotice || message.BotId <= 0 || message.Social == null || message.Social.facts == null ? null : message.Social.facts.stockName;
                if (string.IsNullOrEmpty(name)) { continue; }
                topicTally.TryGetValue(name, out int count); topicTally[name] = count + 1;
            }
        }
        foreach (var pair in topicTally) { topicRank.Add(pair); }
        topicRank.Sort((left, right) => { int order = right.Value.CompareTo(left.Value); return order != 0 ? order : string.CompareOrdinal(left.Key, right.Key); });
        bool any = topicRank.Count > 0;
        topicEmpty.gameObject.SetActive(!any);
        if (!any) { ChatFooterKit.Lit(topicEmpty, ChatFooterText.T("아직 화제가 없습니다")); }
        for (int i = 0; i < TopicCount; i++)
        {
            bool has = i < topicRank.Count;
            topicColumns[i].SetActive(has);
            if (!has) { continue; }
            ChatFooterKit.Lit(topicNames[i], GameLocale.Text(topicRank[i].Key));
            ChatFooterKit.Lit(topicValues[i], ChatFooterText.F("{0}회", topicRank[i].Value));
            // 막대는 1위를 가득 찬 것으로 두고 견준다.
            topicFills[i].anchorMin = Vector2.zero; topicFills[i].anchorMax = new Vector2(topicRank[i].Value / (float)topicRank[0].Value, 1f); topicFills[i].offsetMin = topicFills[i].offsetMax = Vector2.zero;
        }
    }
    private static RectTransform Progress(Transform root, TextMeshProUGUI like, float x, float width, out TextMeshProUGUI caption, out TextMeshProUGUI value)
    {
        caption = ChatFooterKit.Label("Text :: Progress Caption", root, like, x, .16f, width * .4f, .30f, 30f, ChatFooterKit.Muted);
        value = ChatFooterKit.Label("Text :: Progress Value", root, like, x + width * .35f, .16f, width * .65f, .30f, 34f, ChatFooterKit.Ink, TextAlignmentOptions.MidlineRight);
        var track = ChatFooterKit.Flat("Image :: Progress Track", root, x, .56f, width, .075f, ChatFooterKit.Track);
        return (RectTransform)ChatFooterKit.Flat("Image :: Progress Fill", track.transform, 0, 0, 1, 1, ChatFooterKit.Teal).transform;
    }
    private static void Glance(Transform root, TextMeshProUGUI like, float x, float width, out TextMeshProUGUI caption, out TextMeshProUGUI value)
    {
        var box = ChatFooterKit.CellBox("Panel :: Glance", root, x, .12f, width, .68f).transform;
        caption = ChatFooterKit.Label("Text :: Glance Caption", box, like, .06f, .10f, .88f, .34f, 28f, ChatFooterKit.Muted);
        value = ChatFooterKit.Label("Text :: Glance Value", box, like, .06f, .46f, .88f, .44f, 36f, ChatFooterKit.Ink);
    }
    private static void Ratio(RectTransform fill, decimal now, decimal goal)
    {
        float ratio = goal <= 0m ? 0f : Mathf.Clamp01((float)(now / goal));
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(ratio, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
    }
    private void RefreshLounge(InvestorSociety society, bool operate)
    {
        var career = PlayerProgression.Instance;
        bool locked = !operate && career != null;
        // 방을 열기 전에는 이 카드(개설 비용과 라운지 화제)를 채팅 화면에 내지 않는다. 개설은 내 리딩방 탭에서 한다.
        var veil = lobbyCard.GetComponent<CanvasGroup>(); if (veil == null) { veil = lobbyCard.gameObject.AddComponent<CanvasGroup>(); }
        veil.alpha = locked ? 0f : 1f; veil.blocksRaycasts = !locked; veil.interactable = !locked;
        lockedGroup.SetActive(locked); openGroup.SetActive(!locked);
        RefreshTopics();
        ChatFooterKit.Lit(lobbyHint, locked ? ChatFooterText.T("내 리딩방 탭에서 개설할 수 있습니다") : "");
        if (locked)
        {
            // 개설 비용까지 얼마나 모았는지.
            decimal cash = decimal.Floor(career.OpeningCash);
            ChatFooterKit.Lit(costCaption, ChatFooterText.T("개설 비용"));
            ChatFooterKit.Lit(costValue, cash.ToString("N0") + " / " + DeviceTerminalUI.Money(career.OpeningCost));
            Ratio(costFill, cash, career.OpeningCost);
            return;
        }
        var own = society.GetRoom(1);
        if (own == null) { return; }
        bool active = own.Command != null && !own.Command.Canceled;
        ChatFooterKit.Lit(roomCaption, ChatFooterText.T("내 리딩방")); ChatFooterKit.Lit(roomValue, ChatFooterText.F("참여 {0}명", own.Members.Count));
        ChatFooterKit.Lit(callCaption, ChatFooterText.T("지시")); ChatFooterKit.Lit(callValue, active ? CommandText(own) : ChatFooterText.T("활성 지시 없음"));
        callValue.color = active ? ChatFooterKit.Ink : ChatFooterKit.Faint;
        // 한 번 올리면 잠시 쉬어야 한다. 남은 시간을 단추에 적는다.
        int wait = Mathf.CeilToInt((float)society.PromoteWait);
        ChatFooterKit.Lit(promoteLabel, wait > 0 ? ChatFooterText.F("홍보 {0}초", wait) : ChatFooterText.T("홍보하기"));
        promoteButton.interactable = wait <= 0 && MarketDayController.CanTrade && society.IsRoomOpen(1); ChatFooterKit.Dim(promoteButton);
    }
    private void Promote()
    {
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.PromoteRoom(1)) { next = 0f; }
    }
    private static string CommandText(InvestorSociety.Room room)
        => GameLocale.Text(room.Command.Stock.stockName) + "    " + ChatFooterText.F("매수 ≤ {0}", room.Command.Buy.ToString("N0")) + "  /  " + ChatFooterText.F("매도 ≥ {0}", room.Command.Sell.ToString("N0"));

    void LateUpdate()
    {
        if (!built) { return; }
        // 방을 바꾸면 기다리지 않고 바로 자리를 맞춘다.
        if (chat.SelectedRoom != shownRoom) { shownRoom = chat.SelectedRoom; next = 0f; }
        if (Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f; Refresh();
    }
    private void Refresh()
    {
        var society = InvestorSociety.Instance;
        if (society == null || chat == null) { return; }
        var room = society.GetRoom(chat.SelectedRoom);
        bool npcSelected = room != null && !room.PlayerOwned, operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        // 남의 방에서는 초대할 수 없으니 단추를 아예 내지 않는다.
        // 방을 아직 열지 않았어도 단추는 보인다. 누르면 개설 화면으로 간다.
        bool inviting = !npcSelected && chat.visualProfile.SelectedBotId > 0;
        if (rooms.invite.gameObject.activeSelf != inviting) { rooms.invite.gameObject.SetActive(inviting); }
        foreach (var button in new[] { rooms.invite, rooms.npcBuy, rooms.npcSell, rooms.issue, rooms.cancel }) { ChatFooterKit.Dim(button); }
        bool opening = room != null && room.PlayerOwned && !operate && PlayerProgression.Instance != null;
        bool lounge = !opening && !npcSelected && !(room != null && room.PlayerOwned && operate);
        // 방을 열기 전의 공용 라운지에는 방 카드가 없다. 그 자리를 비워 두지 않고 채팅 목록을 늘린다.
        Compact(room != null && room.PlayerOwned && operate ? OwnShift : lounge && !operate && PlayerProgression.Instance != null ? NoCardShift : 0f, chat.visualProfile.SelectedBotId <= 0);
        if (openCard.gameObject.activeSelf != opening) { openCard.gameObject.SetActive(opening); }
        if (opening) { RefreshOpen(PlayerProgression.Instance); }
        else if (npcSelected) { RefreshNpc(); }
        else if (room != null && room.PlayerOwned && operate) { RefreshOwn(society, room); }
        else { RefreshLounge(society, operate); }
    }
    private void RefreshNpc()
    {
        var npc = NpcLeadingRoom.Instance;
        if (npc == null) { return; }
        ChatFooterKit.Lit(npcName, npc.Leader == null || npc.Leader.Social == null ? GameLocale.Text(npc.LeaderName) : npc.LeaderName);
        ChatFooterKit.Lit(npcRole, ChatFooterText.T("운영자")); ChatFooterKit.Follow(npcName, npcRole, 22f, 26f);
        decimal profit = npc.RealizedProfit;
        ChatFooterKit.Lit(npcStats, ChatFooterText.F("체결 {0}건", npc.FillCount.ToString("N0")) + "  ·  " +
            ChatFooterText.F("실현 {0}", (profit > 0m ? "+" : profit < 0m ? "−" : "") + DeviceTerminalUI.Money(System.Math.Abs(profit))));
        bool signal = npc.PlayerJoined && npc.HasSignal;
        npcCells.SetActive(signal); npcStock.gameObject.SetActive(signal); npcSeen.gameObject.SetActive(signal); npcMessage.gameObject.SetActive(!signal);
        if (!signal)
        {
            ChatFooterKit.Lit(npcMessage, npc.PlayerJoined ? GameLocale.Text(npc.Status) : ChatFooterText.T("참여하면 운영자의 실제 주문과 대화를 볼 수 있습니다."));
            return;
        }
        var current = npc.Current;
        ChatFooterKit.Lit(npcStock, GameLocale.Text(current.Stock.stockName));
        ChatFooterKit.Lit(npcSeen, ChatFooterText.F("{0} 관측", current.Time));
        Side(npcBuyCaption, npcBuyValue, current.Buy, current.RestingBuy ? "매수 대기" : "최근 매수 체결", "매수");
        Side(npcSellCaption, npcSellValue, current.Sell, "매도 대기", "매도");
    }
    private static void Side(TextMeshProUGUI caption, TextMeshProUGUI value, decimal price, string active, string idle)
    {
        bool has = price > 0m;
        ChatFooterKit.Lit(caption, ChatFooterText.T(has ? active : idle));
        ChatFooterKit.Lit(value, has ? DeviceTerminalUI.Money(price) : ChatFooterText.T("신호 없음"));
        value.color = has ? ChatFooterKit.Ink : ChatFooterKit.Faint; value.fontSize = has ? 44f : 34f;
    }
    private Button rushButton;
    private TextMeshProUGUI rushLabel;
    private void ToggleRush()
    {
        var rush = OpeningRush.Instance;
        if (rush == null) { return; }
        if (rush.PlayerPending != null) { rush.CancelPlayer(); }
        else
        {
            // 매수 칸에 적은 값이 회원들이 받아 줄 값이 된다. 비어 있거나 지금 값보다 낮으면 +30%로 잡힌다.
            decimal.TryParse(rooms.buyPrice.text.Replace(",", ""), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out decimal top);
            rush.SchedulePlayer(chat.SelectedRoom, StockManager.Instance.CurrentStock, top);
        }
        Refresh();
    }
    private void RefreshOwn(InvestorSociety society, InvestorSociety.Room room)
    {
        ChatFooterKit.Lit(ownTitle, ChatFooterText.T("내 리딩방"));
        ChatFooterKit.Lit(ownMembers, ChatFooterText.F("참여 {0}명", room.Members.Count)); ChatFooterKit.Follow(ownTitle, ownMembers, 22f, 26f);
        ChatFooterKit.Lit(ownMood, ChatFooterText.T("경각심") + " " + room.Social.discipline.ToString("0") + "  ·  " + ChatFooterText.T("위축") + " " + room.Social.chill.ToString("0"));
        ChatFooterKit.Lit(ownBuyCaption, ChatFooterText.T("매수")); ChatFooterKit.Lit(ownSellCaption, ChatFooterText.T("매도"));
        bool active = room.Command != null && !room.Command.Canceled;
        for (int i = 0; i < counts.Length; i++) { counts[i] = 0; }
        if (society.Orders != null)
        {
            foreach (var plan in society.Orders.Plans)
            {
                if (plan.Command != room.Command) { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { counts[4]++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { counts[5]++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { counts[2]++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { counts[3]++; }
                else if (plan.Remaining > 0m) { counts[1]++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { counts[0]++; }
            }
        }
        // 숫자가 있는 칸만 지시 뒤에 붙인다.
        string line = active ? CommandText(room) : ChatFooterText.T("활성 지시 없음");
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0) { line += "  ·  " + ChatFooterText.T(CountNames[i]) + " " + counts[i]; }
        }
        var rush = OpeningRush.Instance; var current = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        bool queued = rush != null && rush.PlayerPending != null;
        // 예약은 장이 닫혀 있을 때만, 지금 보는 종목을 1번 계좌에 들고 있을 때만 할 수 있다.
        bool ready = rush != null && !rush.PlayerRunning && !MarketDayController.CanTrade && current != null && current.IsListed && PlayerAccounts.Instance != null && PlayerAccounts.Instance.primary.GetHolding(current) >= 1m;
        ChatFooterKit.Lit(rushLabel, ChatFooterText.T(queued ? "예약 취소" : "장 시작 몰아주기"));
        rushButton.interactable = queued || ready; ChatFooterKit.Dim(rushButton);
        if (queued) { line = ChatFooterText.F("장 시작 예약 · {0} · 회원 {1}까지 매수, 내 물량 매도", GameLocale.Text(rush.PlayerPending.stockName), rush.PlayerTop.ToString("N0")); }
        ChatFooterKit.Lit(ownCommand, line);
        ownCommand.color = active || queued ? ChatFooterKit.Ink : ChatFooterKit.Faint;
    }
}
