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
    public static bool BlocksInput => IsOpen || Instance != null && Instance.consumedFrame == Time.frameCount;
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
        if (SessionStore.Exists && !SessionStore.ValidationMode) { Open(); inSession = false; ShowHome(); }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            consumedFrame = Time.frameCount;
            if (CurrentPage == Page.Closed) { Open(); } else { Back(); }
        }
    }
    private void LateUpdate()
    {
        if (!IsOpen) { return; }
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void Refresh() { UI?.Refresh(); }
    public void Open()
    {
        if (IsOpen) { return; }
        priorTimeScale = Time.timeScale; priorCursor = Cursor.lockState; priorVisible = Cursor.visible;
        priorSelection = EventSystem.current?.currentSelectedGameObject;
        suspended.Clear();
        foreach (var caster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude))
        {
            if (caster.enabled && !caster.transform.IsChildOf(transform)) { suspended.Add(caster); caster.enabled = false; }
        }
        EventSystem.current?.SetSelectedGameObject(null);
        UI.Backdrop.Capture();
        CurrentPage = Page.Pause; returnPage = Page.Pause; UI.Root.SetActive(true); UI.Pause();
        Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void Resume()
    {
        if (!inSession || loading || PlayerRestController.Instance != null && PlayerRestController.Instance.State.dead || MarketDayController.Instance != null && MarketDayController.Instance.ReportPending) { return; }
        CurrentPage = Page.Closed; consumedFrame = Time.frameCount; UI.Root.SetActive(false);
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
        if (PlayerRestController.Instance == null) { return; }
        SealClosingCapture();
        if (!PlayerRestController.Instance.SleepUntilMorning()) { return; }
        Resume();
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
            UI.Confirm("새 세션","다음 마감에 기존 세션을 덮어씁니다.",()=>StartCoroutine(Reload(false)),"시작");
        }
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
        else { Resume(); }
        if (load) { SessionLoaded?.Invoke(); }
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
