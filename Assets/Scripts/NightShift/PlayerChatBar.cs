using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 채팅 화면에 얹는 두 가지.
//  1) 글 쓰는 칸: 대화 목록 바로 아래. Enter나 "보내기"로 올린다. 쓸 수 있는 방(공개방, 들어가 있는 새벽 관찰실, 열어 둔 내 리딩방)에서만 보인다.
//  2) 새벽 관찰실의 잠금 화면: 방을 골랐는데 아직 들어가 있지 않으면 대화 목록 한가운데에 뜬다.
//     열기 전에는 "해금하기"와 그 아래에 가격·조건, 연 뒤에는 "입장하기"와 하루 이용료.
// 씬 파일은 건드리지 않고 실행할 때 만든다.
public sealed class PlayerChatBar : MonoBehaviour
{
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private TabletGroupChatUI chat;
    private TMP_FontAsset font;
    private float scale;
    private RectTransform bar, gate;
    private TMP_InputField field;
    private TextMeshProUGUI placeholder, sendLabel, gateTitle, gateButtonLabel, gatePrice, gateNote, gateStatus;
    private Button send, gateButton;
    private float next, noteUntil;
    private bool shrunk;
    public TMP_InputField Field => field;
    public RectTransform Gate => gate;

    void Start()
    {
        chat = GetComponent<TabletGroupChatUI>();
        if (chat == null || chat.scroll == null) { enabled = false; return; }
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        scale = chat.layoutScale <= 0f ? 1f : chat.layoutScale;
        BuildBar(); BuildGate();
    }

    private TextMeshProUGUI Label(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = gameObject.layer };
        item.transform.SetParent(parent, false);
        var label = item.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size * scale; label.enableAutoSizing = false; label.color = color; label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        var rect = label.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return label;
    }

    private void BuildBar()
    {
        var list = (RectTransform)chat.scroll.transform; float height = 60f * scale, gap = 8f * scale;
        var holder = new GameObject("Panel :: Chat Entry", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        bar = (RectTransform)holder.transform; bar.SetParent(list.parent, false);
        // 대화 목록의 아래 가장자리에 맞춰 놓고, 목록은 그만큼 위로 줄인다.
        bar.anchorMin = new Vector2(list.anchorMin.x, list.anchorMin.y); bar.anchorMax = new Vector2(list.anchorMax.x, list.anchorMin.y); bar.pivot = new Vector2(.5f, 0f);
        bar.offsetMin = new Vector2(list.offsetMin.x, list.offsetMin.y); bar.offsetMax = new Vector2(list.offsetMax.x, list.offsetMin.y + height);
        list.offsetMin = new Vector2(list.offsetMin.x, list.offsetMin.y + height + gap); shrunk = true;
        holder.GetComponent<Image>().color = new Color(.05f, .07f, .09f, .96f); holder.GetComponent<Image>().raycastTarget = false;

        var entry = new GameObject("Input :: Chat Message", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        var entryRect = (RectTransform)entry.transform; entryRect.SetParent(bar, false);
        entryRect.anchorMin = Vector2.zero; entryRect.anchorMax = Vector2.one; entryRect.offsetMin = new Vector2(8f, 7f) * scale; entryRect.offsetMax = new Vector2(-142f, -7f) * scale;
        var entryImage = entry.GetComponent<Image>(); entryImage.color = new Color(.10f, .14f, .17f, 1f);
        field = entry.AddComponent<TMP_InputField>(); field.targetGraphic = entryImage;
        var text = Label("Text :: Chat Message", entryRect, 24f, new Color(.92f, .94f, .96f, 1f), TextAlignmentOptions.MidlineLeft);
        text.rectTransform.offsetMin = new Vector2(14f, 4f) * scale; text.rectTransform.offsetMax = new Vector2(-14f, -4f) * scale; text.raycastTarget = true; text.overflowMode = TextOverflowModes.Overflow;
        placeholder = Label("Text :: Chat Placeholder", entryRect, 22f, new Color(.48f, .55f, .60f, 1f), TextAlignmentOptions.MidlineLeft);
        placeholder.rectTransform.offsetMin = new Vector2(14f, 4f) * scale; placeholder.rectTransform.offsetMax = new Vector2(-14f, -4f) * scale;
        field.textViewport = entryRect; field.textComponent = text; field.placeholder = placeholder; field.characterLimit = 80;
        field.contentType = TMP_InputField.ContentType.Standard; field.lineType = TMP_InputField.LineType.SingleLine; field.richText = false;
        field.caretColor = new Color(.18f, .90f, .82f, 1f); field.selectionColor = new Color(.18f, .90f, .82f, .35f); field.caretWidth = Mathf.Max(1, Mathf.RoundToInt(2f * scale));
        field.onSubmit.AddListener(_ => Send());

        var button = new GameObject("Button :: Send Chat", typeof(RectTransform), typeof(Image), typeof(Button)) { layer = gameObject.layer };
        var buttonRect = (RectTransform)button.transform; buttonRect.SetParent(bar, false);
        buttonRect.anchorMin = new Vector2(1f, 0f); buttonRect.anchorMax = new Vector2(1f, 1f); buttonRect.pivot = new Vector2(1f, .5f);
        buttonRect.offsetMin = new Vector2(-134f, 7f) * scale; buttonRect.offsetMax = new Vector2(-8f, -7f) * scale;
        send = button.GetComponent<Button>(); send.targetGraphic = button.GetComponent<Image>(); TabletControlStyle.Button(send, true);
        sendLabel = Label("Text :: Send Chat", buttonRect, 23f, new Color(.04f, .08f, .10f, 1f), TextAlignmentOptions.Center);
        send.onClick.AddListener(Send);
    }

    private void BuildGate()
    {
        var holder = new GameObject("Panel :: Dawn Room Gate", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        gate = (RectTransform)holder.transform; gate.SetParent(chat.scroll.transform, false);
        gate.anchorMin = gate.anchorMax = gate.pivot = new Vector2(.5f, .5f); gate.sizeDelta = new Vector2(560f, 330f) * scale; gate.anchoredPosition = Vector2.zero;
        holder.GetComponent<Image>().color = new Color(.055f, .09f, .115f, .98f); holder.GetComponent<Image>().raycastTarget = true;
        gateTitle = Place(Label("Text :: Gate Title", gate, 34f, new Color(.92f, .94f, .96f, 1f), TextAlignmentOptions.Center), 24f, 22f, 512f, 48f);
        gateStatus = Place(Label("Text :: Gate Status", gate, 21f, new Color(.60f, .66f, .71f, 1f), TextAlignmentOptions.Center), 24f, 74f, 512f, 34f);
        var button = new GameObject("Button :: Dawn Room", typeof(RectTransform), typeof(Image), typeof(Button)) { layer = gameObject.layer };
        var rect = (RectTransform)button.transform; rect.SetParent(gate, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f); rect.anchoredPosition = new Vector2(140f, -128f) * scale; rect.sizeDelta = new Vector2(280f, 66f) * scale;
        gateButton = button.GetComponent<Button>(); gateButton.targetGraphic = button.GetComponent<Image>(); TabletControlStyle.Button(gateButton, true);
        gateButtonLabel = Label("Text :: Dawn Room Button", rect, 28f, new Color(.04f, .08f, .10f, 1f), TextAlignmentOptions.Center);
        gateButton.onClick.AddListener(PressGate);
        // 단추 아래: 가격과 조건, 그리고 이용료.
        gatePrice = Place(Label("Text :: Gate Price", gate, 25f, new Color(.18f, .90f, .82f, 1f), TextAlignmentOptions.Center), 24f, 210f, 512f, 38f);
        gateNote = Place(Label("Text :: Gate Note", gate, 21f, new Color(.60f, .66f, .71f, 1f), TextAlignmentOptions.Center), 24f, 252f, 512f, 56f);
        gateNote.textWrappingMode = TextWrappingModes.Normal;
        holder.SetActive(false);
    }
    private TextMeshProUGUI Place(TextMeshProUGUI label, float x, float y, float w, float h)
    {
        var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y) * scale; rect.sizeDelta = new Vector2(w, h) * scale;
        return label;
    }

    private void Send()
    {
        var society = InvestorSociety.Instance;
        if (society == null || field == null) { return; }
        string text = field.text;
        if (string.IsNullOrWhiteSpace(text)) { return; }
        if (society.SubmitPlayerChat(chat.SelectedRoom, text)) { field.text = ""; chat.ShowNewestFromOutside(); }
        // 이어서 바로 쓸 수 있게 칸을 다시 잡는다.
        field.ActivateInputField();
    }

    private void PressGate()
    {
        var career = PlayerProgression.Instance; var npc = NpcLeadingRoom.Instance; var payer = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        if (career == null || npc == null || payer == null) { return; }
        string note;
        if (!career.DawnUnlocked)
        {
            bool opened = career.UnlockDawn(payer);
            note = opened ? Word("해금했습니다", "Unlocked", "解放しました") : !career.DawnEligible ? Word("아직 조건이 안 됩니다", "Requirement not met yet", "まだ条件を満たしていません") : Word("현금이 모자랍니다", "Not enough cash", "現金が足りません");
            Sfx.Play(opened ? "OrderFill" : "UiDeny", .7f);
        }
        else
        {
            npc.RequestJoin(); bool joined = npc.JoinPlayer();
            note = joined ? "" : Word("지금은 들어갈 수 없습니다", "Cannot enter right now", "今は入れません") + (string.IsNullOrEmpty(npc.Status) ? "" : " · " + GameLocale.Text(npc.Status));
            if (!joined) { Sfx.Play("UiDeny", .7f); }
        }
        GameLocalizedText.Set(gateStatus, note, true); noteUntil = Time.unscaledTime + 5f; next = 0f;
    }

    void Update()
    {
        if (chat == null || bar == null || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f;
        var society = InvestorSociety.Instance; var career = PlayerProgression.Instance; var npc = NpcLeadingRoom.Instance;
        bool chatting = chat.chatPanel == null || chat.chatPanel.activeInHierarchy;
        bool write = chatting && society != null && society.PlayerCanWrite(chat.SelectedRoom);
        if (bar.gameObject.activeSelf != write) { bar.gameObject.SetActive(write); }
        if (write)
        {
            GameLocalizedText.Set(placeholder, chat.SelectedRoom == NpcLeadingRoom.RoomId ? Word("메시지 입력 · !분석 종목명", "Message · !analyze stock", "メッセージ · !分析 銘柄名") : Word("메시지 입력", "Message", "メッセージ"), true);
            GameLocalizedText.Set(sendLabel, Word("보내기", "Send", "送信"), true);
        }
        bool locked = chatting && chat.SelectedRoom == NpcLeadingRoom.RoomId && npc != null && !npc.PlayerJoined && career != null;
        if (gate.gameObject.activeSelf != locked) { gate.gameObject.SetActive(locked); if (locked) { gate.SetAsLastSibling(); } }
        if (!locked) { return; }
        var rules = career.rules; var payer = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        GameLocalizedText.Set(gateTitle, Word("새벽 관찰실", "Dawn Watch Room", "夜明けの観察室"), true);
        if (!career.DawnUnlocked)
        {
            GameLocalizedText.Set(gateButtonLabel, Word("해금하기", "Unlock", "解放する"), true);
            GameLocalizedText.Set(gatePrice, rules.dawnRoomCost.ToString("N0") + Word("원", " KRW", "ウォン"), true);
            GameLocalizedText.Set(gateNote, Word("조건: 누적 거래대금 ", "Requires lifetime turnover of ", "条件: 累計売買代金") + rules.dawnRoomTurnover.ToString("N0") + Word("원 이상", " KRW or more", "ウォン以上")
                + "\n" + Word("입장 중 하루 ", "While inside: ", "入室中は1日") + rules.dawnRoomDailyFee.ToString("N0") + Word("원", " KRW a day", "ウォン"), true);
            gateButton.interactable = career.DawnEligible && payer != null && PlayerAccounts.Available(payer) >= rules.dawnRoomCost;
            if (Time.unscaledTime >= noteUntil)
            { GameLocalizedText.Set(gateStatus, Word("지금 거래대금 ", "Turnover so far ", "現在の売買代金 ") + decimal.Floor(career.Turnover).ToString("N0") + Word("원", " KRW", "ウォン"), true); }
        }
        else
        {
            GameLocalizedText.Set(gateButtonLabel, Word("입장하기", "Enter", "入室する"), true);
            GameLocalizedText.Set(gatePrice, Word("하루 이용료 ", "Daily fee ", "1日の利用料 ") + rules.dawnRoomDailyFee.ToString("N0") + Word("원", " KRW", "ウォン"), true);
            GameLocalizedText.Set(gateNote, Word("운영자의 실제 주문 관측과 종목 분석 AI를 볼 수 있습니다.\n들어가 있는 동안 날마다 이용료가 나갑니다.", "See the host's real orders and the stock analysis AI.\nThe fee is charged each day you stay.", "運営者の実際の注文と銘柄分析AIが見られます。\n入室中は毎日利用料がかかります。"), true);
            gateButton.interactable = npc.Available && payer != null && PlayerAccounts.Available(payer) >= rules.dawnRoomDailyFee;
            if (Time.unscaledTime >= noteUntil) { GameLocalizedText.Set(gateStatus, npc.Available ? "" : Word("운영자를 기다리는 중", "Waiting for the host", "運営者を待っています"), true); }
        }
    }

    void OnDestroy()
    {
        // 목록을 줄여 둔 것을 되돌린다(화면을 다시 만들 때 두 번 줄지 않게).
        if (shrunk && chat != null && chat.scroll != null && bar != null)
        {
            var list = (RectTransform)chat.scroll.transform;
            list.offsetMin = new Vector2(list.offsetMin.x, bar.offsetMin.y);
        }
    }
}
