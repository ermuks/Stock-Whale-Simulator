using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-30000)]
public sealed class PauseMenuController : MonoBehaviour
{
    public enum Page { Closed, Pause, Settings, Home, Confirm, Busy, Report, Dead, Closing }
    public static PauseMenuController Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.CurrentPage != Page.Closed;
    public static bool BlocksInput => IsOpen || CollapseSleep.Active || MorningBriefing.Blocking || Instance != null && Instance.consumedFrame == Time.frameCount;
    public Page CurrentPage { get; private set; }
    public SystemMenuUI UI { get; private set; }
    public event Action SessionLoaded;
    private Page returnPage = Page.Pause, confirmReturn;
    private float priorTimeScale;
    private CursorLockMode priorCursor;
    private bool priorVisible, inSession = true, loading;
    private bool checkpointSaved;
    private SessionStore.SaveJob closingSave;
    /// <summary>마감 저장이 아직 끝나지 않았는지. 정산 화면이 상태 문구를 고르는 데 쓴다.</summary>
    public bool ClosingSaveRunning => closingSave != null && !closingSave.IsCompleted;
    private GameObject priorSelection;
    private int consumedFrame = -1;
    private readonly List<GraphicRaycaster> suspended = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) { return; }
        var root = new GameObject("System :: Pause and Settings"); DontDestroyOnLoad(root); root.AddComponent<PauseMenuController>();
    }
    private void Awake()
    { Instance = this; UI = new SystemMenuUI(this); GameSettings.Changed += Refresh; GameLocale.Changed += Refresh; }
    private IEnumerator Start()
    {
        yield return null;
        // 게임을 켜면 언제나 타이틀 화면부터다. 저장이 없어도 바로 세계에 떨어뜨리지 않는다.
        // 점검 도구로 돌릴 때는 타이틀을 건너뛴다. 도구가 NewGame.Begin으로 직접 시작한다.
        if (!SessionStore.ValidationMode) { Open(); inSession = false; fresh = true; ShowHome(); }
    }
    // 장면이 방금 올라와 아무 세션도 시작하지 않은 상태인지. 그때는 새 게임을 위해 장면을 다시 부를 필요가 없다.
    private bool fresh;
    /// <summary>타이틀 화면을 띄운다(점검 도구용).</summary>
    public void ShowTitle() { if (!IsOpen) { Open(); } ShowHome(); }
    public void BackToTitle() { if (!loading) { ShowHome(); } }
    /// <summary>새 게임의 회사 이름 화면.</summary>
    public void ShowNewGame()
    {
        var manager = StockManager.Instance;
        if (manager == null || !NewGame.CanBegin) { inSession = true; priorTimeScale = 1; priorCursor = CursorLockMode.None; priorVisible = true; Resume(); return; }
        CurrentPage = Page.Home;
        UI.NewGame(NewGame.SuggestedCompany, manager.economy.retailStartingCash);
    }
    public void StartNewGame()
    {
        if (loading) { return; }
        if (!NewGame.CanBegin) { return; }
        if (!NewGame.Begin(UI.Title.CompanyName, out string reason)) { UI.Title.ShowError(reason); return; }
        StartCoroutine(Begin());
    }
    // 세계가 준비될 때까지 잠깐 기다렸다가 방으로 들어간다.
    private IEnumerator Begin()
    {
        loading = true; CurrentPage = Page.Busy; UI.Busy("불러오는 중…");
        // 새 게임에서만 프롤로그를 튼다(불러오기는 Reload로 가므로 여기를 지나지 않는다). 메뉴가 열려 있어 게임 시계는 멈춰 있고(Time.timeScale 0),
        // 그 사이에 세계가 준비된다. 프롤로그가 끝나거나 건너뛰어진 뒤에야 방으로 들어가므로 시계는 08:30에서 출발한다.
        bool introDone = false;
        bool introShown = StoryIntro.Play(() => { introDone = true; });
        float until = Time.realtimeSinceStartup + 60;
        while ((StockManager.Instance == null || !StockManager.Instance.EconomyInitialized) && Time.realtimeSinceStartup < until) { yield return null; }
        while (!introDone) { yield return null; }
        loading = false; inSession = true; fresh = false; priorTimeScale = 1; priorCursor = CursorLockMode.Locked; priorVisible = false;
        Resume();
        // 프롤로그의 검은 화면이 걷히기 전에 눈을 감은 채 침대에 누워 있게 해 둔다. 검은 화면이 걷히면 눈을 뜨고 일어난다.
        if (introShown) { BedSleep.Instance?.WakeUp(); }
    }
    private void Update()
    {
        // 글을 쓰는 중의 Esc는 입력만 끝낸다. 메뉴는 열지 않는다.
        if (MouseCameraController.TextEntryEscape) { MouseCameraController.EndTextEntry(); consumedFrame = Time.frameCount; return; }
        // 휴대폰을 들고 있는 동안의 Esc는 휴대폰의 것이다(내용 → 목록 → 내려놓기). 메뉴는 열지 않는다.
        if (CurrentPage == Page.Closed && DeskPhone.Raised && !CollapseSleep.Active && !MorningBriefing.Blocking && Input.GetKeyDown(KeyCode.Escape)) { return; }
        if (Input.GetKeyDown(KeyCode.Escape) && !(CollapseSleep.Active && CurrentPage == Page.Closed))
        {
            consumedFrame = Time.frameCount;
            if (CurrentPage == Page.Closed) { Open(); } else { Back(); }
        }
    }
    /// <summary>메뉴가 켜질 때 뒷배경이 흐려지는 데, 꺼질 때 흐림이 풀리는 데 걸리는 시간(초).</summary>
    private const float BackdropFadeSeconds = .1f;
    private CanvasGroup fadeGroup;
    private float fadeTarget = 1f;
    /// <summary>
    /// 메뉴 화면(흐린 뒷배경과 그 위의 글자)을 0.1초 동안 서서히 드러내거나 거둔다.
    /// 켤 때는 또렷한 게임 화면 위로 흐린 그림이 차오르므로 서서히 흐려지는 것처럼 보이고, 끌 때는 그 반대다.
    /// 게임 시간이 멈춰 있어도 돌도록 실제 시간으로 잰다.
    /// </summary>
    private void FadeBackdrop(bool show)
    {
        if (fadeGroup == null) { fadeGroup = UI.Root.GetComponent<CanvasGroup>(); if (fadeGroup == null) { fadeGroup = UI.Root.AddComponent<CanvasGroup>(); } }
        // 켤 때: 닫혀 있던 화면이면 투명한 데서 시작한다. 흐림이 풀리던 도중에 다시 켜면 그 자리에서 이어서 흐려진다.
        if (show && !fadeShown && fadeGroup.alpha >= 1f) { fadeGroup.alpha = 0f; }
        fadeShown = show; fadeTarget = show ? 1f : 0f;
        fadeGroup.blocksRaycasts = show; fadeGroup.interactable = show;
        if (!show && !UI.Root.activeSelf) { fadeGroup.alpha = 0f; }
    }
    private bool fadeShown;
    private void StepBackdropFade()
    {
        if (fadeGroup == null || Mathf.Approximately(fadeGroup.alpha, fadeTarget)) { return; }
        fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, fadeTarget, Time.unscaledDeltaTime / BackdropFadeSeconds);
        if (fadeTarget <= 0f && fadeGroup.alpha <= 0f && UI.Root.activeSelf) { UI.Root.SetActive(false); }
    }
    private void LateUpdate()
    {
        StepBackdropFade();
        if (!IsOpen) { return; }
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void Refresh() { UI?.Refresh(); }
    public void Open()
    {
        if (IsOpen) { return; }
        // 쓰러져 자는 동안의 빨리 감기는 메뉴를 닫은 뒤로 이어지지 않는다(자는 쪽이 필요하면 다시 건다).
        priorTimeScale = CollapseSleep.Active ? 1f : Time.timeScale; priorCursor = Cursor.lockState; priorVisible = Cursor.visible;
        priorSelection = EventSystem.current?.currentSelectedGameObject;
        suspended.Clear();
        foreach (var caster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude))
        {
            if (caster.enabled && !caster.transform.IsChildOf(transform)) { suspended.Add(caster); caster.enabled = false; }
        }
        EventSystem.current?.SetSelectedGameObject(null);
        UI.Backdrop.Capture();
        CurrentPage = Page.Pause; returnPage = Page.Pause; UI.Root.SetActive(true); UI.Pause();
        FadeBackdrop(true);
        Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void Resume()
    {
        if (!inSession || loading || PlayerRestController.Instance != null && PlayerRestController.Instance.State.dead || MarketDayController.Instance != null && MarketDayController.Instance.ReportPending) { return; }
        // 바로 끄지 않는다. 흐림이 0.1초 동안 풀린 뒤에 꺼진다(LateUpdate).
        CurrentPage = Page.Closed; consumedFrame = Time.frameCount; FadeBackdrop(false);
        PlayerPrefs.Save();
        foreach (var caster in suspended) { if (caster != null) { caster.enabled = true; } } suspended.Clear();
        Time.timeScale = priorTimeScale; Cursor.lockState = priorCursor; Cursor.visible = priorVisible;
        if (priorSelection != null && priorSelection.activeInHierarchy) { EventSystem.current?.SetSelectedGameObject(priorSelection); }
    }
    public void Back()
    {
        if (loading || CurrentPage == Page.Busy || CurrentPage == Page.Closing) { return; }
        if (CurrentPage == Page.Settings)
        {
            if (returnPage == Page.Report) { CurrentPage = Page.Report; UI.Report(checkpointSaved); }
            else if (inSession) { CurrentPage = Page.Pause; UI.Pause(); } else { ShowHome(); }
        }
        else if (CurrentPage == Page.Confirm)
        {
            CurrentPage = confirmReturn;
            if (CurrentPage == Page.Settings) { UI.Settings(); } else if (inSession) { CurrentPage = Page.Pause; UI.Pause(); } else { ShowHome(); }
        }
        else if (CurrentPage == Page.Home && !inSession && UI.Title.Root.gameObject.activeSelf && NewGame.CanBegin)
        {
            // 새 게임 화면에서 ESC: 타이틀 메뉴로.
            ShowHome();
        }
        else if (CurrentPage == Page.Pause) { Resume(); }
    }
    public void Settings()
    { returnPage = CurrentPage; CurrentPage = Page.Settings; UI.Settings(); }
    public void ConfirmDefaults()
    {
        confirmReturn = CurrentPage; CurrentPage = Page.Confirm;
        UI.Confirm("기본값 복원","언어는 유지됩니다.",()=> { GameSettings.Defaults(); CurrentPage = Page.Settings; UI.Settings(); },"복원");
    }
    public void ConfirmDiscard()
    {
        confirmReturn = CurrentPage; CurrentPage = Page.Confirm;
        UI.Confirm("세션 떠나기","마지막 마감 이후의 진행은 사라집니다.",()=> { inSession = false; ShowHome(); },"떠나기");
    }
    public void ConfirmQuit()
    {
        confirmReturn = CurrentPage; CurrentPage = Page.Confirm;
        UI.Confirm("게임 종료","마지막 마감 이후의 진행은 사라집니다.",Quit,"게임 종료");
    }
    public void ReceiveClosingReport()
    {
        if (!IsOpen) { Open(); }
        if (ClosingSaveRunning) { return; }
        checkpointSaved = false;
        try { closingSave = SessionStore.BeginSave(); }
        catch (Exception ex)
        {
            Debug.LogWarning("Closing checkpoint rejected: "+ex); closingSave = null;
            CurrentPage = Page.Report; UI.Report(false); return;
        }
        // 정산 화면을 바로 보여 준다. 저장은 뒤에서 진행하고 화면 구석에 흐리게만 알린다.
        CurrentPage = Page.Report; UI.Report(false);
        StartCoroutine(RunClosingSave(closingSave));
    }
    // 세계 상태 수집은 프레임마다 조금씩, 파일 기록은 백그라운드에서 한다. 플레이어가 정산 화면을 떠나도 계속된다.
    private IEnumerator RunClosingSave(SessionStore.SaveJob job)
    {
        UI.SavingIndicator(true);
        yield return job.Capture();
        while (!job.IsCompleted) { yield return null; }
        UI.SavingIndicator(false);
        if (closingSave != job) { yield break; }
        checkpointSaved = job.Error == null;
        if (job.Error != null) { Debug.LogWarning("Closing checkpoint rejected: "+job.Error); }
        // 저장용으로 모았던 자료를 돌려준다.
        GC.Collect();
        if (CurrentPage == Page.Report) { UI.ReportSaveState(checkpointSaved,false); }
    }
    // 세계가 다시 움직이기 전에 상태 수집만은 끝낸다. 파일 기록은 그 뒤에도 백그라운드에서 이어진다.
    private void SealClosingCapture()
    {
        if (closingSave != null && !closingSave.CaptureFinished) { closingSave.CompleteCaptureNow(); }
    }
    public void ContinueAfterReport() { SealClosingCapture(); MarketDayController.Instance?.AcknowledgeReport(); Resume(); }
    public void Sleep()
    {
        var rest = PlayerRestController.Instance;
        if (rest == null || !rest.CanSleep) { return; }
        SealClosingCapture();
        // 메뉴를 닫고 방으로 돌아가서 잠든다: 눈이 감기고, 꿈이 지나가고, 아침에 눈을 뜬다.
        MarketDayController.Instance?.AcknowledgeReport();
        Resume();
        if (IsOpen || !rest.BeginNightSleep()) { rest.SleepUntilMorning(); }
    }
    public void ShowDeath()
    { if (!IsOpen) { Open(); } CurrentPage = Page.Dead; UI.Death(); }
    private void ShowHome() { CurrentPage = Page.Home; UI.Home(); }
    private void Fail(string text) { confirmReturn = inSession ? Page.Pause : Page.Home; CurrentPage = Page.Confirm; UI.Error(text); }
    public void LoadSession() { if (!loading) { StartCoroutine(Reload(true)); } }
    public void NewSession()
    {
        if (loading) { return; }
        if (SessionStore.Exists)
        {
            confirmReturn = Page.Home; CurrentPage = Page.Confirm;
            UI.Confirm("새 세션","다음 마감에 기존 세션을 덮어씁니다.",()=> { if (fresh) { ShowNewGame(); } else { StartCoroutine(Reload(false)); } },"시작");
        }
        else if (fresh) { ShowNewGame(); }
        else { StartCoroutine(Reload(false)); }
    }
    private IEnumerator Reload(bool load)
    {
        loading = true; CurrentPage = Page.Busy; UI.Busy("불러오는 중…");
        string snapshot = null;
        if (load)
        {
            try { snapshot = SessionStore.Read(); }
            catch (Exception ex) { Debug.LogWarning("Session read rejected: "+ex.Message); loading = false; Fail("저장 파일을 읽지 못했습니다."); yield break; }
        }
        SessionClock.ResetClock();
        var operation = SceneManager.LoadSceneAsync("TradeScene"); yield return operation; yield return null;
        suspended.Clear();
        if (load)
        {
            var manager = StockManager.Instance;
            float until = Time.realtimeSinceStartup + 60;
            while (MarketDayController.Initializers > 0 && Time.realtimeSinceStartup < until) { yield return null; }
            if (manager != null && !manager.SetupConfirmed)
            {
                string reason;
                if (!manager.ConfirmPlayer(manager.economy.companies[0].name,manager.economy.companies[0].issuedShares.ToString(),manager.economy.defaultPublicPercentage.ToString(),out reason))
                { loading = false; Fail("세션 초기화 실패"); yield break; }
            }
            while ((MarketDayController.Instance == null || !MarketDayController.Instance.Ready) && Time.realtimeSinceStartup < until) { yield return null; }
            Exception restoreError=null;
            yield return SessionStore.RestoreInitialized(snapshot,error=>restoreError=error);
            snapshot=null;
            if (restoreError!=null) { Debug.LogWarning("Session restore rejected: "+restoreError); loading = false; Fail("세션을 복원하지 못했습니다."); yield break; }
        }
        loading = false; inSession = true; priorTimeScale = 1;
        priorCursor = load ? CursorLockMode.Locked : CursorLockMode.None; priorVisible = !load;
        if (load && MarketDayController.Instance.ReportPending)
        {
            checkpointSaved = true; CurrentPage = Page.Report;
            UI.Root.SetActive(false); UI.Backdrop.Capture(); UI.Root.SetActive(true); UI.Report(true);
        }
        else if (load) { fresh = false; Resume(); }
        else { inSession = false; fresh = true; ShowNewGame(); }
        if (load) { fresh = false; SessionLoaded?.Invoke(); }
    }
    public void Quit()
    {
        PlayerPrefs.Save();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    // 저장 중에 게임을 끄면 파일 기록이 끝날 때까지 기다린다.
    private void OnApplicationQuit() { SessionStore.WaitForPendingSave(); }
    private void OnDestroy()
    { GameSettings.Changed -= Refresh; GameLocale.Changed -= Refresh; UI?.Dispose(); if (Instance == this) { Instance = null; } }
}
