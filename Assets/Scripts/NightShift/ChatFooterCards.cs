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
        ["신뢰"] = ("Trust", "信頼"), ["배신감"] = ("Betrayal", "裏切り"), ["직접 경험"] = ("Direct results", "直接経験"),
        ["{0}승 · {1}패"] = ("{0} W · {1} L", "{0}勝 · {1}敗"),
        ["충성"] = ("Loyal", "支持"), ["애증"] = ("Conflicted", "愛憎"), ["적대"] = ("Hostile", "敵対"), ["무관심"] = ("Indifferent", "無関心"),
        ["운영자"] = ("Host", "運営者"), ["체결 {0}건"] = ("{0} fills", "約定 {0}件"), ["실현 {0}"] = ("Realized {0}", "実現 {0}"),
        ["{0} 관측"] = ("Seen {0}", "{0} 観測"), ["매수"] = ("Buy", "買い"), ["매도"] = ("Sell", "売り"),
        ["매수 대기"] = ("Buy pending", "買い待機"), ["최근 매수 체결"] = ("Last buy fill", "直近の買い約定"), ["매도 대기"] = ("Sell pending", "売り待機"),
        ["신호 없음"] = ("No signal", "シグナルなし"),
        ["참여하면 운영자의 실제 주문과 대화를 볼 수 있습니다."] = ("Join to see the host's real orders and chat.", "参加すると運営者の実際の注文と会話が見られます。"),
        ["내 리딩방"] = ("My own room", "自分の部屋"), ["참여 {0}명"] = ("{0} members", "参加 {0}人"),
        ["운영 권한 필요"] = ("Hosting not unlocked yet", "運営権限が必要"), ["실제 거래대금"] = ("Traded value", "売買代金"), ["실현손익"] = ("Realized P&L", "実現損益"), ["지시"] = ("Signal", "指示"),
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
    public const float CardX = .025f, CardWidth = .95f, ProfileTop = .664f, ProfileHeight = .098f, RoomTop = .770f, RoomHeight = .192f, LoungeHeight = .192f;
    private LeadingRoomUI rooms;
    private TabletGroupChatUI chat;
    private RectTransform profilePanel, ownCard, npcCard, lobbyCard;
    private TextMeshProUGUI npcName, npcStats, npcStock, npcSeen, npcBuyCaption, npcBuyValue, npcSellCaption, npcSellValue, npcMessage, npcRole;
    private TextMeshProUGUI ownTitle, ownMembers, ownMood, ownCommand, lobbyHint;
    private GameObject lockedGroup, openGroup;
    private TextMeshProUGUI turnoverCaption, turnoverValue, profitCaption, profitValue, roomCaption, roomValue, callCaption, callValue;
    private RectTransform turnoverFill, profitFill;
    // 라운지 화제: 지금 공용 라운지 대화에 가장 많이 오른 종목 셋. 화면에 보이는 대화를 센 것뿐이다.
    private const int TopicCount = 3, TopicWindow = 40;
    private TextMeshProUGUI topicHeading, topicEmpty;
    private readonly TextMeshProUGUI[] topicNames = new TextMeshProUGUI[TopicCount], topicValues = new TextMeshProUGUI[TopicCount];
    private readonly RectTransform[] topicFills = new RectTransform[TopicCount];
    private readonly GameObject[] topicColumns = new GameObject[TopicCount];
    private readonly Dictionary<string, int> topicTally = new();
    private readonly List<KeyValuePair<string, int>> topicRank = new();
    private GameObject npcCells;
    private readonly TextMeshProUGUI[] ownCounts = new TextMeshProUGUI[6], ownCountCaptions = new TextMeshProUGUI[6];
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

        BuildNpc(like); BuildOwn(like); BuildLobby(like);

        // 초대는 참여자 카드의 머리줄 오른쪽에 둔다. 누구를 초대하는지가 바로 옆에 보인다.
        ChatFooterKit.Place((RectTransform)rooms.invite.transform, profilePanel, .776f, .11f, .20f, .27f);
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
        ownCard = CardArea("Area :: Own Room Card", rooms.playerControls);
        var controls = rooms.playerControls.transform;
        // 머리줄: 방 이름, 인원, 방 분위기.
        ownTitle = ChatFooterKit.Label("Text :: Room Title", ownCard, like, .024f, .04f, .30f, .11f, 46f, ChatFooterKit.Ink);
        ownMembers = ChatFooterKit.Tag("Room Members", ownCard, like, .29f, .058f, .15f, .075f, 28f); ownMembers.color = ChatFooterKit.Muted;
        ownMood = ChatFooterKit.Label("Text :: Room Mood", ownCard, like, .44f, .04f, .30f, .11f, 30f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineRight);
        // 이용료 단추는 원래 이 자리 근처에 따로 떠 있었다. 머리줄 오른쪽 끝에 붙인다.
        foreach (var button in controls.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.parent != controls || !button.name.Contains("Room Fee")) { continue; }
            ChatFooterKit.Place((RectTransform)button.transform, ownCard, .776f, .045f, .20f, .10f); ChatFooterKit.StyleButton(button, 34f);
        }
        // 지금 걸려 있는 지시와 그 지시를 따르는 사람 수.
        var box = ChatFooterKit.CellBox("Panel :: Room Command", ownCard, .024f, .175f, .952f, .355f).transform;
        ownCommand = ChatFooterKit.Label("Text :: Active Command", box, like, .03f, .07f, .94f, .34f, 40f, ChatFooterKit.Ink);
        ChatFooterKit.Flat("Image :: Command Divider", box, .03f, .44f, .94f, .008f, ChatFooterKit.Track);
        for (int i = 0; i < 6; i++)
        {
            float x = .03f + i * .94f / 6f, w = .94f / 6f;
            ownCountCaptions[i] = ChatFooterKit.Label("Text :: Count Caption " + i, box, like, x, .49f, w, .20f, 28f, ChatFooterKit.Muted, TextAlignmentOptions.Center);
            ownCounts[i] = ChatFooterKit.Label("Text :: Count " + i, box, like, x, .68f, w, .27f, 40f, ChatFooterKit.Ink, TextAlignmentOptions.Center);
        }
        // 새 지시 입력: 가격 두 칸과 버튼 두 개가 한 줄.
        var buyLabel = controls.Find("Text :: Buy Trigger Label") as RectTransform; var sellLabel = controls.Find("Text :: Sell Trigger Label") as RectTransform;
        if (buyLabel != null) { ChatFooterKit.Place(buyLabel, ownCard, .024f, .56f, .28f, .06f); ChatFooterKit.Restyle(buyLabel.GetComponent<TextMeshProUGUI>(), 28f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineLeft, false); }
        if (sellLabel != null) { ChatFooterKit.Place(sellLabel, ownCard, .320f, .56f, .28f, .06f); ChatFooterKit.Restyle(sellLabel.GetComponent<TextMeshProUGUI>(), 28f, ChatFooterKit.Muted, TextAlignmentOptions.MidlineLeft, false); }
        ChatFooterKit.Place((RectTransform)rooms.buyPrice.transform, ownCard, .024f, .63f, .28f, .15f);
        ChatFooterKit.Place((RectTransform)rooms.sellPrice.transform, ownCard, .320f, .63f, .28f, .15f);
        ChatFooterKit.Place((RectTransform)rooms.issue.transform, ownCard, .636f, .63f, .165f, .15f);
        ChatFooterKit.Place((RectTransform)rooms.cancel.transform, ownCard, .811f, .63f, .165f, .15f);
        ChatFooterKit.StyleButton(rooms.issue, 40f); ChatFooterKit.StyleButton(rooms.cancel, 40f);
        ChatFooterKit.Place(rooms.feedback.rectTransform, ownCard, .024f, .825f, .952f, .11f);
        ChatFooterKit.Restyle(rooms.feedback, 28f, ChatFooterKit.Faint, TextAlignmentOptions.MidlineLeft, false);
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
        turnoverFill = Progress(lockedGroup.transform, like, .024f, out turnoverCaption, out turnoverValue);
        profitFill = Progress(lockedGroup.transform, like, .512f, out profitCaption, out profitValue);
        openGroup = ChatFooterKit.Area("Area :: Own Room Glance", lobbyCard, 0, .315f, 1, .27f).gameObject;
        Glance(openGroup.transform, like, .024f, .30f, out roomCaption, out roomValue);
        Glance(openGroup.transform, like, .34f, .636f, out callCaption, out callValue);
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
    private static RectTransform Progress(Transform root, TextMeshProUGUI like, float x, out TextMeshProUGUI caption, out TextMeshProUGUI value)
    {
        const float width = .464f;
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
        lockedGroup.SetActive(locked); openGroup.SetActive(!locked);
        RefreshTopics();
        ChatFooterKit.Lit(lobbyHint, locked ? ChatFooterText.T("운영 권한 필요") : "");
        if (locked)
        {
            ChatFooterKit.Lit(turnoverCaption, ChatFooterText.T("실제 거래대금")); ChatFooterKit.Lit(profitCaption, ChatFooterText.T("실현손익"));
            ChatFooterKit.Lit(turnoverValue, career.Turnover.ToString("N0") + " / " + DeviceTerminalUI.Money(career.rules.leadingTurnover));
            ChatFooterKit.Lit(profitValue, career.RealizedProfit.ToString("N0") + " / " + DeviceTerminalUI.Money(career.rules.leadingRealizedProfit));
            Ratio(turnoverFill, career.Turnover, career.rules.leadingTurnover); Ratio(profitFill, career.RealizedProfit, career.rules.leadingRealizedProfit);
            return;
        }
        var own = society.GetRoom(1);
        if (own == null) { return; }
        bool active = own.Command != null && !own.Command.Canceled;
        ChatFooterKit.Lit(roomCaption, ChatFooterText.T("내 리딩방")); ChatFooterKit.Lit(roomValue, ChatFooterText.F("참여 {0}명", own.Members.Count));
        ChatFooterKit.Lit(callCaption, ChatFooterText.T("지시")); ChatFooterKit.Lit(callValue, active ? CommandText(own) : ChatFooterText.T("활성 지시 없음"));
        callValue.color = active ? ChatFooterKit.Ink : ChatFooterKit.Faint;
    }
    private static string CommandText(InvestorSociety.Room room)
        => GameLocale.Text(room.Command.Stock.stockName) + "    " + ChatFooterText.F("매수 ≤ {0}", room.Command.Buy.ToString("N0")) + "  /  " + ChatFooterText.F("매도 ≥ {0}", room.Command.Sell.ToString("N0"));

    void LateUpdate()
    {
        if (!built || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f; Refresh();
    }
    private void Refresh()
    {
        var society = InvestorSociety.Instance;
        if (society == null || chat == null) { return; }
        var room = society.GetRoom(chat.SelectedRoom);
        bool npcSelected = room != null && !room.PlayerOwned, operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        // 남의 방에서는 초대할 수 없으니 단추를 아예 내지 않는다.
        bool inviting = !npcSelected && operate && chat.visualProfile.SelectedBotId > 0;
        if (rooms.invite.gameObject.activeSelf != inviting) { rooms.invite.gameObject.SetActive(inviting); }
        foreach (var button in new[] { rooms.invite, rooms.npcBuy, rooms.npcSell, rooms.issue, rooms.cancel }) { ChatFooterKit.Dim(button); }
        if (npcSelected) { RefreshNpc(); }
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
    private void RefreshOwn(InvestorSociety society, InvestorSociety.Room room)
    {
        ChatFooterKit.Lit(ownTitle, ChatFooterText.T("내 리딩방"));
        ChatFooterKit.Lit(ownMembers, ChatFooterText.F("참여 {0}명", room.Members.Count)); ChatFooterKit.Follow(ownTitle, ownMembers, 22f, 26f);
        ChatFooterKit.Lit(ownMood, ChatFooterText.T("경각심") + " " + room.Social.discipline.ToString("0") + "  ·  " + ChatFooterText.T("위축") + " " + room.Social.chill.ToString("0"));
        bool active = room.Command != null && !room.Command.Canceled;
        ChatFooterKit.Lit(ownCommand, active ? CommandText(room) : ChatFooterText.T("활성 지시 없음"));
        ownCommand.color = active ? ChatFooterKit.Ink : ChatFooterKit.Faint;
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
        for (int i = 0; i < 6; i++)
        {
            ChatFooterKit.Lit(ownCountCaptions[i], ChatFooterText.T(CountNames[i]));
            ChatFooterKit.Lit(ownCounts[i], counts[i].ToString());
            // 0은 흐리게: 숫자가 있는 칸만 눈에 들어온다.
            ownCounts[i].color = counts[i] > 0 ? ChatFooterKit.Ink : ChatFooterKit.Faint;
        }
    }
}
