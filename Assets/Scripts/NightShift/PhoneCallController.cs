using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>로컬 게임 전화 이벤트. 외부 통신 없이 기존 손/휴대폰 그립을 표시한다.</summary>
public sealed class PhoneCallController : MonoBehaviour
{
    public enum Caller { Institution, Research, Spam }
    public static PhoneCallController Instance { get; private set; }
    public static bool IsRaised => Instance != null && Instance.raised;
    public Transform grip;
    public TextMeshProUGUI title, body, alert;
    public Button answer, decline, close;
    public PhoneScreenUI screenUI;
    public int RemainingSeconds => Mathf.CeilToInt(Mathf.Max(0f, expires - SessionClock.Time));
    public string LastMessage => message;
    public bool Ringing { get; private set; }
    public Caller CurrentCaller { get; private set; }
    public int ResolvedCalls { get; private set; }
    private bool raised, previousLocked;
    private Vector3 authoredScale, authoredPosition;
    private float nextCall = 35f, expires, nextUi;
    private System.Random random = new System.Random(34117);
    private string message = "수신 기록 없음";
    /// <summary>전화 기능은 쓰지 않기로 했다. false인 동안에는 전화가 오지 않고, Tab으로 휴대폰을 들 수도 없다. 씬의 물체와 이 코드는 남겨 두었으니 true로 바꾸면 되살아난다.</summary>
    public static readonly bool Available = false;
    void Awake()
    {
        if (!Available)
        {
            if (grip != null) { grip.gameObject.SetActive(false); }
            enabled = false; return;
        }
        Instance = this;
    }
    void Start()
    {
        authoredScale = grip.localScale; authoredPosition = grip.localPosition;
        grip.gameObject.SetActive(false);
        answer.onClick.AddListener(Answer);
        decline.onClick.AddListener(Decline);
        close.onClick.AddListener(Lower);
        nextCall = SessionClock.Time + 35f;
    }
    void Update()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && SessionClock.Time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && SessionClock.Time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            GameLocalizedText.Set(title,Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록");
            GameLocalizedText.Set(body,Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message);
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
    public void Ring(Caller caller)
    {
        if (Ringing)
        { return; }
        CurrentCaller = caller; Ringing = true; expires = SessionClock.Time + 70f;
    }
    public void Answer()
    {
        if (!Ringing || !MarketDayController.CanTrade)
        { return; }
        var stock = StockManager.Instance.CurrentStock;
        string name = stock == null ? "시장" : stock.stockName;
        message = CurrentCaller == Caller.Institution
            ? "기관 담당자라고 합니다.\n\"" + name + "에 관심이 있다는 얘기는 들었습니다. 확인된 공시는 아닙니다.\"\n\n통화 기록: 신원과 내용 모두 미확인."
            : CurrentCaller == Caller.Research
            ? "리서치 데스크\n" + (MarketRhythmDirector.Instance == null ? "자료를 수집 중입니다." : MarketRhythmDirector.Instance.Observation(stock)) + "\n\n관찰된 체결 요약입니다. 이후 방향을 보장하지 않습니다."
            : "스팸 전화\n\"곧 큰 기회가 옵니다. 특별 회원에…\"\n\n근거 없는 권유입니다. 통화를 종료했습니다.";
        Resolve();
    }
    public void Decline()
    {
        if (!Ringing)
        { return; }
        message = "전화를 거절했습니다. 시장은 계속 움직입니다.";
        Resolve();
    }
    private void Resolve() { Ringing = false; ResolvedCalls++; nextCall = SessionClock.Time + random.Next(210, 360); }
    public void Raise()
    {
        DeskViewController.Instance?.ExitView(true);
        if (raised || TabletViewController.IsTabletHeld)
        { return; }
        previousLocked = Cursor.lockState == CursorLockMode.Locked;
        float framing = Mathf.Tan(Camera.main.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(22.5f * Mathf.Deg2Rad);
        grip.localScale = authoredScale * framing;
        grip.localPosition = new Vector3(authoredPosition.x * framing, authoredPosition.y * framing, authoredPosition.z);
        raised = true; grip.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void Lower()
    {
        if (!raised)
        { return; }
        raised = false; grip.gameObject.SetActive(false);
        grip.localScale = authoredScale; grip.localPosition = authoredPosition;
        if (previousLocked && Time.timeScale > 0f) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
