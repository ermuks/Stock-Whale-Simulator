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
    public InvestorJourneyNotes paperNotes;
    public int Step { get; private set; }
    private bool hidden;
    private float next;
    void Start()
    {
        if (action != null) { action.onClick.AddListener(Act); }
        if (dismiss != null) { dismiss.onClick.AddListener(Hide); }
        if (reopen != null) { reopen.onClick.AddListener(Show); }
    }
    private void Hide() { hidden = true; next = 0f; }
    private void Show() { hidden = false; next = 0f; }
    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        if (paperNotes != null)
        {
            if (card != null && card.activeSelf) { card.SetActive(false); }
            if (reopen != null && reopen.gameObject.activeSelf) { reopen.gameObject.SetActive(false); }
            bool ready = StockManager.Instance != null && StockManager.Instance.SetupConfirmed;
            if (paperNotes.gameObject.activeSelf != ready) { paperNotes.gameObject.SetActive(ready); }
            var sourceRoom = NpcLeadingRoom.Instance; var progress = PlayerProgression.Instance;
            if (ready && sourceRoom != null && progress != null)
            {
                paperNotes.InitializeMilestones(progress.JourneyMilestones);
                Step = ResolveStep(sourceRoom, progress);
                paperNotes.Refresh(sourceRoom, progress, Step);
            }
            return;
        }
        bool visible = StockManager.Instance != null && StockManager.Instance.SetupConfirmed && !DeskViewController.OwnsView && !TabletViewController.IsTabletHeld && !PhoneCallController.IsRaised;
        card.SetActive(visible && !hidden); reopen.gameObject.SetActive(visible && hidden);
        if (!visible)
        { return; }
        var npc = NpcLeadingRoom.Instance; var career = PlayerProgression.Instance;
        if (npc == null || career == null)
        { return; }
        Step = ResolveStep(npc, career);
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
            title = "내 리딩방 준비"; body = "개설 비용 " + career.OpeningCost.ToString("N0") + "원"; button = "비용 보기";
        }
        else if (Step == 3)
        {
            title = "개설 준비 완료"; body = "개설 비용 준비됨"; button = "방 열기";
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
    private static int ResolveStep(NpcLeadingRoom npc, PlayerProgression career)
    { return career.CanOperateRooms ? 4 : career.Eligible ? 3 : career.Turnover > 0m ? 2 : npc.PlayerJoined ? 1 : 0; }
    void OnDestroy()
    {
        if (action != null) { action.onClick.RemoveListener(Act); }
        if (dismiss != null) { dismiss.onClick.RemoveListener(Hide); }
        if (reopen != null) { reopen.onClick.RemoveListener(Show); }
    }
}
