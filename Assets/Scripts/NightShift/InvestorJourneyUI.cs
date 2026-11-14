using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 참여·체결·실현성과를 읽는 첫 플레이 안내. 닫아도 시장과 성장은 계속된다.</summary>
public sealed class InvestorJourneyUI : MonoBehaviour
{
    public GameObject card;
    public TextMeshProUGUI heading, detail, actionLabel;
    public Button action, dismiss, reopen;
    public LeadingRoomUI rooms;
    public DeviceTerminalUI orders, journal;
    public TerminalGlyph[] stages;
    public int Step { get; private set; }
    private bool hidden;
    private float next;
    void Start()
    {
        action.onClick.AddListener(Act); dismiss.onClick.AddListener(Hide); reopen.onClick.AddListener(Show);
    }
    private void Hide() { hidden = true; next = 0f; }
    private void Show() { hidden = false; next = 0f; }
    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        bool visible = StockManager.Instance != null && StockManager.Instance.SetupConfirmed && !DeskViewController.OwnsView && !TabletViewController.IsTabletHeld && !PhoneCallController.IsRaised;
        card.SetActive(visible && !hidden); reopen.gameObject.SetActive(visible && hidden);
        if (!visible)
        { return; }
        var npc = NpcLeadingRoom.Instance; var career = PlayerProgression.Instance;
        if (npc == null || career == null)
        { return; }
        Step = career.CanOperateRooms ? 4 : career.Eligible ? 3 : career.Turnover > 0m ? 2 : npc.PlayerJoined ? 1 : 0;
        string title, body, button;
        if (Step == 0)
        {
            title = "리딩방 참여"; body = npc.JoinRequested ? "가입 대기" : "운영자 관찰"; button = "방 보기";
        }
        else if (Step == 1)
        {
            title = "첫 매매"; body = npc.HasSignal ? "새 주문 신호" : "신호 대기"; button = "신호 보기";
        }
        else if (Step == 2)
        {
            title = "내 리딩방 준비"; body = "실현손익 " + career.RealizedProfit.ToString("+#,##0;-#,##0;0") + "원"; button = "성과 보기";
        }
        else if (Step == 3)
        {
            title = "개설 준비 완료"; body = "운영 조건 달성"; button = "방 열기";
        }
        else
        {
            title = "리딩방 운영자"; body = "내 방 1"; button = "방 관리";
        }
        DeviceTerminalUI.Set(heading,title); DeviceTerminalUI.Set(detail,body); DeviceTerminalUI.Set(actionLabel,button);
        if (stages != null)
        {
            int current = Step <= 1 ? 0 : Step == 2 ? 1 : 2;
            for (int i = 0; i < stages.Length; i++)
            { stages[i].SetTint(i == current ? new Color32(37,175,192,255) : i < current ? Color.white : new Color(.35f,.4f,.43f)); }
        }
    }
    private void Act()
    {
        if (Step <= 1)
        { rooms.ShowNpc(); }
        else if (Step < 4)
        { journal.FocusGrowth(); }
        else
        { rooms.chat.SelectRoom(1); }
    }
    void OnDestroy()
    {
        if (action != null) { action.onClick.RemoveListener(Act); }
        if (dismiss != null) { dismiss.onClick.RemoveListener(Hide); }
        if (reopen != null) { reopen.onClick.RemoveListener(Show); }
    }
}
