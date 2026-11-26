using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 두 태블릿 안의 실행/기록 탭. UI는 씬에 저장하고 데이터만 4Hz로 갱신한다.</summary>
public sealed class DeskOperationUI : MonoBehaviour
{
    public bool journal;
    public Trader displayAccount;
    public bool managedNavigation;
    public GameObject deskPanel, legacyPanel;
    public Button deskTab, legacyTab, prepare, budget, build, drive, unwind, pause, finish;
    public TextMeshProUGUI heading, accountText, stateText, details, feedback, history, budgetText;
    public Image heatFill, progressFill;
    public TextMeshProUGUI monitorStatus;
    [System.NonSerialized] public SpreadTradingUI spreadView;
    // 자동 매매 장치는 종목마다 하나씩 돈다. 지금 보는 종목의 장치를 붙들어 두지 않고 쓸 때마다 다시 읽는다.
    private static TradingOperationController Operation => TradingOperationController.Instance;
    private float nextRefresh;
    void Start()
    {
        deskTab.onClick.AddListener(ShowDesk);
        legacyTab.onClick.AddListener(ShowLegacy);
        if (!journal)
        {
            prepare.onClick.AddListener(Prepare);
            budget.onClick.AddListener(ChangeBudget);
            build.onClick.AddListener(Build);
            drive.onClick.AddListener(Drive);
            unwind.onClick.AddListener(Unwind);
            pause.onClick.AddListener(Pause);
            finish.onClick.AddListener(Finish);
        }
        if (!managedNavigation) { ShowDesk(); }
    }
    public void ShowDesk()
    {
        legacyPanel.SetActive(false);
        deskPanel.SetActive(true);
        nextRefresh = 0f;
    }
    public void ShowLegacy()
    {
        deskPanel.SetActive(false);
        legacyPanel.SetActive(true);
    }
    private void Prepare() { Operation?.Prepare(); nextRefresh = 0f; }
    private void ChangeBudget() { Operation?.CycleBudget(); nextRefresh = 0f; }
    private void Build() { Operation?.SetMode(TradingOperationController.Mode.Build); }
    private void Drive() { Operation?.SetMode(TradingOperationController.Mode.Drive); }
    private void Unwind() { Operation?.SetMode(TradingOperationController.Mode.Unwind); }
    private void Pause() { Operation?.SetMode(TradingOperationController.Mode.Paused); }
    private void Finish() { Operation?.Finish(); }
    private static void Text(TextMeshProUGUI target, string value)
    {
        if (target != null && !GameLocalizedText.Matches(target,value))
        { GameLocalizedText.Set(target,value); }
    }
    void Update()
    {
        if (spreadView != null || Time.unscaledTime < nextRefresh)
        { return; }
        var operation = Operation;
        if (operation == null)
        { return; }
        nextRefresh = Time.unscaledTime + .5f;
        var manager = StockManager.Instance;
        var selected = manager == null ? null : manager.CurrentStock;
        var target = operation.Active ? operation.Target : selected;
        string observation = MarketRhythmDirector.Instance == null ? "체결 관찰 대기" : MarketRhythmDirector.Instance.Observation(selected);
        Text(monitorStatus, observation + "\n\n" + (operation.Active ? operation.Target.stockName + " · " + operation.ModeName + $"\n체결 {operation.FillCount}건 · 잔여 {operation.Inventory:N0}주" : "주문 없이도 시장은 계속 움직입니다."));
        if (!deskPanel.activeInHierarchy)
        { return; }
        Text(heading, (journal ? "시장 기록" : "포지션 관리") + "     " + (target == null ? "" : target.stockName));
        Text(stateText, Time.timeScale <= 0f ? "일시정지" : journal ? "관찰 → 비교 → 판단" : operation.ModeName);
        var shownAccount = journal && displayAccount != null ? displayAccount : operation.account;
        if (shownAccount != null)
        { Text(accountText, $"가용 현금 {System.Math.Max(0m, shownAccount.cash - shownAccount.ReservedCash):N0}돈    ·    예치 {shownAccount.ReservedCash:N0}돈"); }
        if (journal)
        {
            Text(details, observation + "\n\n최근 주문 기록\n" + operation.Debrief);
            Text(history, WorldDramaDirector.Instance == null ? "사건을 관찰하는 중입니다." : WorldDramaDirector.Instance.Journal);
            Text(feedback, WorldDramaDirector.Instance == null ? "" : WorldDramaDirector.Instance.BoardResult);
        }
        else
        {
            decimal shownBudget = operation.Active ? operation.Budget : operation.account == null ? 0m : decimal.Floor(System.Math.Max(0m, operation.account.cash - operation.account.ReservedCash) * (decimal)operation.BudgetFraction);
            Text(budgetText, $"예산 {operation.BudgetFraction * 100f:0}%");
            Text(details, $"주문 예산 {shownBudget:N0}돈\n매수 {operation.Spent:N0}돈 / 매도 {operation.Proceeds:N0}돈\n물량 {operation.Inventory:N0}주 · 미체결 {operation.OutstandingCount}건\n평가 포함 손익 {operation.MarkedResult:+#,##0;-#,##0;0}돈");
            Text(history, "종목과 예산을 준비한 뒤\n필요한 주문 방식만 선택하세요.\n\n관망은 이 계획의 미체결만 취소합니다.\n이미 체결된 주식은 계좌에 남습니다.");
            Text(feedback, operation.Feedback);
            prepare.interactable = !operation.Active && MarketDayController.CanTrade;
            budget.interactable = !operation.Active;
            build.interactable = drive.interactable = unwind.interactable = pause.interactable = finish.interactable = operation.Active;
        }
        if (heatFill != null) { heatFill.gameObject.SetActive(false); }
        if (progressFill != null) { progressFill.fillAmount = operation.Budget > 0m ? Mathf.Clamp01((float)(operation.Spent / operation.Budget)) : 0f; }
    }
    void OnDestroy()
    {
        if (deskTab != null) { deskTab.onClick.RemoveListener(ShowDesk); }
        if (legacyTab != null) { legacyTab.onClick.RemoveListener(ShowLegacy); }
        if (journal)
        { return; }
        if (prepare != null) { prepare.onClick.RemoveListener(Prepare); }
        if (budget != null) { budget.onClick.RemoveListener(ChangeBudget); }
        if (build != null) { build.onClick.RemoveListener(Build); }
        if (drive != null) { drive.onClick.RemoveListener(Drive); }
        if (unwind != null) { unwind.onClick.RemoveListener(Unwind); }
        if (pause != null) { pause.onClick.RemoveListener(Pause); }
        if (finish != null) { finish.onClick.RemoveListener(Finish); }
    }
}
