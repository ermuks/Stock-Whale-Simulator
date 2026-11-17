using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 월드 채팅 Canvas의 공용·플레이어 방·봇 운영 방. 행 풀과 조건부 주문 기능을 공유한다.</summary>
public sealed class LeadingRoomUI : MonoBehaviour
{
    public TabletGroupChatUI chat;
    public Button publicRoom, roomOne, roomTwo, roomThree, invite, issue, cancel;
    public TMP_InputField buyPrice, sellPrice;
    public TextMeshProUGUI commandStatus, feedback;
    public Button npcRoom, npcMembership, npcBuy, npcSell;
    public TextMeshProUGUI npcSummary, npcSignal, npcFeedback, membershipLabel;
    public GameObject playerControls, npcControls;
    public DeviceTerminalUI orderScreen;
    public GameObject lobbyControls;
    public TextMeshProUGUI lobbyStatus;
    public Button growth;
    public DeviceTerminalUI growthScreen;
    private string npcResult = "";
    private float npcResultUntil;
    private float nextUpdate;
    private Stock selected;
    private TextMeshProUGUI inviteLabel;
    private int invitationBot;
    private float inviteFeedbackUntil;
    private ChatFooterCards footer;
    void Start()
    {
        ConfigureRoomTabs();
        DailyIncomeUI.InstallRoom(this);
        publicRoom.onClick.AddListener(Public); roomOne.onClick.AddListener(One);
        roomTwo.onClick.AddListener(Two); roomThree.onClick.AddListener(Three);
        invite.onClick.AddListener(Invite); issue.onClick.AddListener(Issue); cancel.onClick.AddListener(Cancel);
        inviteLabel = invite.GetComponentInChildren<TextMeshProUGUI>();
        if (growth != null) { growth.onClick.AddListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.AddListener(ShowNpc); npcMembership.onClick.AddListener(Membership);
            npcBuy.onClick.AddListener(NpcBuy); npcSell.onClick.AddListener(NpcSell);
        }
        // 하단을 참여자 카드와 방 카드로 정리한다(씬은 그대로, 시작할 때 배치).
        footer = gameObject.AddComponent<ChatFooterCards>(); footer.Bind(this);
        // 채팅 줄의 + 단추: 닉네임을 고르고 방을 옮기지 않아도 그 자리에서 초대한다.
        chat.InviteRequested = InviteFromChat;
    }
    private void ConfigureRoomTabs()
    {
        if (roomTwo != null) { roomTwo.gameObject.SetActive(false); }
        if (roomThree != null) { roomThree.gameObject.SetActive(false); }
        var tabs = npcRoom == null ? new[] { publicRoom, roomOne } : new[] { publicRoom, npcRoom, roomOne };
        float width = (.95f - .025f * (tabs.Length - 1)) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = (RectTransform)tabs[i].transform;
            float x = .025f + i * (width + .025f);
            rect.anchorMin = new Vector2(x, rect.anchorMin.y); rect.anchorMax = new Vector2(x + width, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0, rect.offsetMin.y); rect.offsetMax = new Vector2(0, rect.offsetMax.y);
        }
        var label = roomOne.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) { GameLocalizedText.Set(label,"내 리딩방"); }
    }
    private void Public() { chat.SelectRoom(0); }
    private void One() { ShowPlayerRoom(1); }
    private void Two() { ShowPlayerRoom(2); }
    private void Three() { ShowPlayerRoom(3); }
    private void ShowPlayerRoom(int room)
    {
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        chat.SelectRoom(room); nextUpdate = 0f;
    }
    private void ShowGrowth() { growthScreen.FocusGrowth(); }
    public void RefreshNow() { nextUpdate = 0f; }
    private void InviteFromChat(int botId)
    {
        // 아직 방을 열지 않았으면 개설 화면으로 데려간다.
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { ShowPlayerRoom(1); return; }
        if (SocialContactController.Instance != null) { SocialContactController.Instance.Contact(botId, 1); }
        inviteFeedbackUntil = Time.unscaledTime + 6f;
        nextUpdate = 0f;
    }
    public void ShowNpc()
    { chat.SelectRoom(NpcLeadingRoom.RoomId); NpcLeadingRoom.Instance?.MarkViewed(); }
    private void Membership()
    {
        var npc = NpcLeadingRoom.Instance;
        if (npc == null)
        { return; }
        if (npc.PlayerJoined)
        { npc.LeavePlayer(); }
        else
        { npc.RequestJoin(); npc.JoinPlayer(); }
        nextUpdate = 0f;
    }
    private void NpcBuy() { DraftNpc(true); }
    private void NpcSell() { DraftNpc(false); }
    private void DraftNpc(bool buy)
    {
        var npc = NpcLeadingRoom.Instance;
        if (npc == null || !npc.PlayerJoined || !npc.HasSignal)
        { npcResult = "참여 상태와 신호 유효시간을 확인하세요."; npcResultUntil = Time.unscaledTime + 4f; return; }
        var signal = npc.Current;
        decimal limit = buy ? signal.Buy : signal.Sell;
        if (limit <= 0m || !orderScreen.LoadSignalDraft(signal.Stock,buy,limit,npc.settings.title + " " + signal.Time + " 관측"))
        { npcResult = "진행 중인 주문 또는 거래 조건을 태블릿에서 확인하세요."; npcResultUntil = Time.unscaledTime + 4f; return; }
        npc.MarkViewed();
    }
    private void Invite()
    {
        // 아직 방을 열지 않았으면 개설 화면으로 데려간다.
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { ShowPlayerRoom(1); return; }
        int roomId = chat.SelectedRoom > 0 ? chat.SelectedRoom : 1;
        int candidate = chat.SelectedBotId > 0 ? chat.SelectedBotId : invitationBot;
        bool joined = SocialContactController.Instance != null && SocialContactController.Instance.Contact(candidate, roomId);
        if (joined) { invitationBot = 0; chat.SelectRoom(roomId); }
        inviteFeedbackUntil = Time.unscaledTime + 6f;
        nextUpdate = 0f;
    }
    private void Issue()
    {
        if (!decimal.TryParse(buyPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal buy) ||
            !decimal.TryParse(sellPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal sell))
        { GameLocalizedText.Set(feedback,"소수점 없이 원 단위 가격을 입력하세요."); nextUpdate = Time.unscaledTime + 3f; return; }
        InvestorSociety.Instance?.Issue(chat.SelectedRoom, StockManager.Instance.CurrentStock, buy, sell);
    }
    private void Cancel() { InvestorSociety.Instance?.CancelRoom(chat.SelectedRoom); }
    void Update()
    {
        if (Time.unscaledTime < nextUpdate || StockManager.Instance == null || InvestorSociety.Instance == null)
        { return; }
        nextUpdate = Time.unscaledTime + .5f;
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        var stock = StockManager.Instance.CurrentStock;
        if (selected != stock && stock != null && !buyPrice.isFocused && !sellPrice.isFocused)
        {
            selected = stock;
            buyPrice.SetTextWithoutNotify(stock.currentPrice.ToString("0", CultureInfo.InvariantCulture));
            sellPrice.SetTextWithoutNotify(System.Math.Max(PriceTickPolicy.OneTickAbove(stock.currentPrice), PriceTickPolicy.RoundToTick(stock.currentPrice * 1.08m)).ToString("0", CultureInfo.InvariantCulture));
        }
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom);
        bool operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        // 방을 열기 전에도 누를 수 있다. 누르면 개설 안내가 나온다.
        roomOne.interactable = true;
        roomTwo.interactable = roomThree.interactable = false;
        bool npcSelected = room != null && !room.PlayerOwned;
        bool ownSelected = room != null && room.PlayerOwned;
        if (playerControls != null)
        { playerControls.SetActive(!npcSelected && (lobbyControls == null || ownSelected && operate)); npcControls.SetActive(npcSelected); }
        if (lobbyControls != null)
        {
            bool opening = ownSelected && !operate && footer != null && footer.Built;
            lobbyControls.SetActive(!npcSelected && (!ownSelected || !operate) && !opening);
            Set(lobbyStatus,operate ? InvestorSociety.Instance.InvitedRoomMask == 0 && Time.unscaledTime >= inviteFeedbackUntil ? "첫 회원 초대 · 채팅에서 + 를 누르세요" : InvestorSociety.Instance.LastAction : "내 리딩방 · 개설 전");
        }
        if (npcSelected && npcControls != null)
        {
            NpcDetails(); invite.interactable = false; return;
        }
        string title = room == null ? "공용 라운지 · 메시지를 눌러 참여자 선택" : $"내 리딩방 · 참여 {room.Members.Count}명";
        if (room != null && room.PlayerOwned) { title += $" · 경각심 {room.Social.discipline:0} · 위축 {room.Social.chill:0}"; }
        int waiting = 0, holding = 0, deferred = 0, rejected = 0, completed = 0, canceled = 0;
        if (InvestorSociety.Instance.Orders != null && room != null)
        {
            foreach (var plan in InvestorSociety.Instance.Orders.Plans)
            {
                if (plan.Command != room.Command)
                { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { completed++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { canceled++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { deferred++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { rejected++; }
                else if (plan.Remaining > 0m) { holding++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { waiting++; }
            }
        }
        string active = room?.Command == null || room.Command.Canceled ? "활성 지시 없음" : $"{room.Command.Stock.stockName}  매수 ≤ {room.Command.Buy:N0} / 매도 ≥ {room.Command.Sell:N0}";
        Set(commandStatus, title + "\n" + active + $"\n대기 {waiting} · 보유 {holding} · 보류 {deferred} · 거부 {rejected}\n완료 {completed} · 취소 {canceled}");
        Set(feedback, operate ? InvestorSociety.Instance.LastAction : PlayerProgression.Instance.LockReason);
        var invitationRoom = room ?? InvestorSociety.Instance.GetRoom(1);
        bool firstInvite = invitationRoom.PlayerOwned && !invitationRoom.Social.firstMemberInvited;
        if (inviteLabel != null) { Set(inviteLabel, firstInvite ? "첫 회원 초대" : "초대"); }
        if (operate && firstInvite && Time.unscaledTime >= inviteFeedbackUntil)
        {
            var candidate = MarketDayController.Instance?.FindBot(invitationBot);
            Set(feedback, candidate == null ? "공용 라운지 채팅에서 + 를 눌러 첫 회원을 초대하세요" : candidate.Social.Nickname + "님 초대 · 이용료와 투자 지시는 각자 판단");
        }
        issue.interactable = operate && room != null && MarketDayController.CanTrade;
        cancel.interactable = room?.Command != null && !room.Command.Canceled;
        invite.interactable = (chat.SelectedBotId > 0 || invitationBot > 0) && (!operate || MarketDayController.CanTrade);
    }
    private void NpcDetails()
    {
        var npc = NpcLeadingRoom.Instance;
        if (npc == null)
        { return; }
        Set(npcSummary,npc.LeaderName + " · 운영자\n체결 " + npc.FillCount + "건 · 실현 " + npc.RealizedProfit.ToString("+#,##0;-#,##0;0") + "원");
        Set(membershipLabel,npc.PlayerJoined ? "방 나가기" : npc.Available ? "방 참여" : "참여 예약");
        bool signal = npc.PlayerJoined && npc.HasSignal;
        var current = npc.Current;
        Set(npcSignal,signal ? current.Stock.stockName + " · " + current.Time + " 관측\n" + (current.Buy > 0m ? (current.RestingBuy ? "매수 대기 " : "최근 매수 체결 ") + current.Buy.ToString("N0") + "원" : "매수 신호 없음") + "\n" + (current.Sell > 0m ? "매도 대기 " + current.Sell.ToString("N0") + "원" : "매도 신호 없음") : npc.PlayerJoined ? npc.Status : "참여하면 운영자의 실제 주문과 대화를 볼 수 있습니다.");
        Set(npcFeedback,Time.unscaledTime < npcResultUntil ? npcResult : "주문 검토 → 태블릿 확인 → 직접 제출");
        bool available = signal && MarketDayController.CanTrade;
        npcBuy.interactable = available && current.Buy > 0m;
        npcSell.interactable = available && current.Sell > 0m && orderScreen.session.account.GetHolding(current.Stock) > 0m;
    }
    private static void Set(TextMeshProUGUI label, string text) { if (!GameLocalizedText.Matches(label,text)) { GameLocalizedText.Set(label,text); } }
    void OnDestroy()
    {
        if (publicRoom != null) { publicRoom.onClick.RemoveListener(Public); }
        if (roomOne != null) { roomOne.onClick.RemoveListener(One); }
        if (roomTwo != null) { roomTwo.onClick.RemoveListener(Two); }
        if (roomThree != null) { roomThree.onClick.RemoveListener(Three); }
        if (invite != null) { invite.onClick.RemoveListener(Invite); }
        if (issue != null) { issue.onClick.RemoveListener(Issue); }
        if (cancel != null) { cancel.onClick.RemoveListener(Cancel); }
        if (growth != null) { growth.onClick.RemoveListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.RemoveListener(ShowNpc); npcMembership.onClick.RemoveListener(Membership);
            npcBuy.onClick.RemoveListener(NpcBuy); npcSell.onClick.RemoveListener(NpcSell);
        }
    }
}
