# 새 스크립트 전문
최종 런타임 구현과 명시적으로 실행하는 검증 도구. 거래 데이터는 기존 엔진을 사용한다.

## Assets/Scripts/Settings/GameSettings.cs
```csharp
using System;
using UnityEngine;

/// <summary>Local preferences, independent of a market session. Only implemented controls are exposed.</summary>
[DefaultExecutionOrder(-19900)]
public sealed class GameSettings : MonoBehaviour
{
    [Serializable] public sealed class Values
    {
        public int version = 1, frameRate = 60, quality = 1;
        public bool vSync, fullscreen = true, muteUnfocused = true;
        public float volume = .8f, sensitivity = 1f, wheel = 3f, menuScale = 1f;
    }
    public const string Preference = "DonDon.Settings.v1";
    public static Values Current { get; private set; } = new();
    public static event Action Changed;
    private bool focused = true;
    private float saveAt = -1;
    private static GameSettings instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { instance = null; Current = new(); Changed = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var root = new GameObject("System :: Preferences"); DontDestroyOnLoad(root);
        instance = root.AddComponent<GameSettings>();
        try { Current = JsonUtility.FromJson<Values>(PlayerPrefs.GetString(Preference,"")) ?? new Values(); }
        catch (ArgumentException) { Current = new Values(); }
        if (Current.version != 1) { Current = new Values(); }
        Clamp(); Apply();
    }
    private static float Valid(float value,float fallback,float min,float max)
    { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value,min,max); }
    private static void Clamp()
    {
        Current.quality = Mathf.Clamp(Current.quality,0,Math.Max(0,QualitySettings.names.Length-1));
        if (Current.frameRate != 30 && Current.frameRate != 60 && Current.frameRate != 120) { Current.frameRate = 60; }
        Current.volume = Valid(Current.volume,.8f,0,1); Current.sensitivity = Valid(Current.sensitivity,1,.25f,2.5f);
        Current.wheel = Valid(Current.wheel,3,1,6); Current.menuScale = Valid(Current.menuScale,1,.9f,1.1f);
    }
    public static void Commit()
    {
        Clamp(); Apply(); PlayerPrefs.SetString(Preference,JsonUtility.ToJson(Current));
        if (instance != null) { instance.saveAt = Time.unscaledTime+.35f; } else { PlayerPrefs.Save(); }
        Changed?.Invoke();
    }
    public static void Defaults() { Current = new Values(); Commit(); }
    private static void Apply()
    {
        // Avoid expensive pipeline/OS switches on every slider movement.
        if (QualitySettings.GetQualityLevel() != Current.quality) { QualitySettings.SetQualityLevel(Current.quality,true); }
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0; Application.targetFrameRate = Current.frameRate;
        if (SessionStore.ValidationMode) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 20; }
        if (!Application.isEditor)
        {
            var mode = Current.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode) { Screen.fullScreenMode = mode; }
        }
        AudioListener.volume = Current.muteUnfocused && instance != null && !instance.focused ? 0 : Current.volume;
    }
    private void OnApplicationFocus(bool value) { focused = value; Apply(); }
    private void Update()
    { if (saveAt >= 0 && Time.unscaledTime >= saveAt) { saveAt = -1; PlayerPrefs.Save(); } }
    private void OnApplicationQuit() { PlayerPrefs.Save(); }
}
```

## Assets/Scripts/Settings/MenuBackdrop.cs
```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>Capture only when opening a menu. Two small blur passes, never a live extra camera.</summary>
public sealed class MenuBackdrop : System.IDisposable
{
    private readonly RawImage image;
    private RenderTexture capture, blurred, scratch;
    private readonly Material material;
    public bool Ready => blurred != null && image.texture == blurred;
    public MenuBackdrop(RawImage graphic)
    {
        image = graphic; var shader = Resources.Load<Shader>("UI/SystemMenu/MenuBlur");
        if (shader != null) { material = new Material(shader); }
    }
    public void Capture()
    {
        var camera = Camera.main;
        if (camera == null || material == null) { return; }
        int width = 768, height = Mathf.Max(1, Mathf.RoundToInt(width / camera.aspect));
        if (capture == null || capture.height != height)
        {
            Release(); capture = Make(width, height, 24); scratch = Make(width, height, 0); blurred = Make(width, height, 0);
        }
        var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = capture; camera.Render();
            material.SetVector("_Direction", new Vector4(2f / width, 0, 0, 0)); Graphics.Blit(capture, scratch, material);
            material.SetVector("_Direction", new Vector4(0, 2f / height, 0, 0)); Graphics.Blit(scratch, blurred, material);
            image.texture = blurred;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; }
    }
    private static RenderTexture Make(int w, int h, int depth)
    { var texture = new RenderTexture(w,h,depth) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp }; texture.Create(); return texture; }
    private void Release()
    {
        image.texture = null;
        foreach (var texture in new[] { capture, scratch, blurred })
        { if (texture != null) { texture.Release(); Object.Destroy(texture); } }
        capture = scratch = blurred = null;
    }
    public void Dispose() { Release(); if (material != null) { Object.Destroy(material); } }
}
```

## Assets/Scripts/Settings/PauseMenuController.cs
```csharp
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
    public enum Page { Closed, Pause, Settings, Home, Confirm, Busy, Report, Dead }
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
        if (loading || CurrentPage == Page.Busy) { return; }
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
        checkpointSaved = false;
        try { SessionStore.Save(); checkpointSaved = true; }
        catch (Exception ex) { Debug.LogWarning("Closing checkpoint rejected: "+ex); }
        CurrentPage = Page.Report; UI.Report(checkpointSaved);
    }
    public void ContinueAfterReport() { MarketDayController.Instance?.AcknowledgeReport(); Resume(); }
    public void Sleep()
    {
        if (PlayerRestController.Instance == null || !PlayerRestController.Instance.SleepUntilMorning()) { return; }
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
    private void OnDestroy()
    { GameSettings.Changed -= Refresh; GameLocale.Changed -= Refresh; UI?.Dispose(); if (Instance == this) { Instance = null; } }
}
```

## Assets/Scripts/Settings/PlayerRestController.cs
```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>One inexpensive overlay; no camera render/blur work on each gameplay frame.</summary>
public sealed class PlayerRestController : MonoBehaviour
{
    public static PlayerRestController Instance { get; private set; }
    public PlayerRestState State = new();
    [Min(0)] public float fatiguePerGameHour = 2.5f;
    private MarketDayController day;
    private Material vision;
    private RawImage overlay;
    private float nextRefresh;
    public bool CanSleep => day != null && day.Ready && day.Phase == MarketPhase.PostClose && !State.dead;
    public double SleepHours => CanSleep ? (1920d - day.ExactGameMinute) / 60d : 0d;
    private void Awake()
    {
        Instance = this; day = GetComponent<MarketDayController>();
        var root = new GameObject("Area :: Fatigue Vision", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29900;
        var graphic = new GameObject("Image :: Tired Eyes", typeof(RectTransform), typeof(RawImage)); graphic.transform.SetParent(root.transform, false);
        overlay = graphic.GetComponent<RawImage>(); overlay.raycastTarget = false;
        var rect = overlay.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var shader = Resources.Load<Shader>("UI/SystemMenu/FatigueVision");
        if (shader != null) { vision = new Material(shader); overlay.material = vision; }
        overlay.enabled = false;
    }
    public void ObserveClock()
    {
        if (day == null || !day.Ready) { return; }
        State.Advance(day.AbsoluteGameMinute, fatiguePerGameHour);
    }
    private void Update()
    {
        if (Time.timeScale > 0) { ObserveClock(); }
        if (Time.unscaledTime < nextRefresh) { return; }
        nextRefresh = Time.unscaledTime + .05f;
        bool shown = vision != null && State.fatigue > 25 && !PauseMenuController.IsOpen;
        overlay.enabled = shown;
        if (!shown) { return; }
        vision.SetFloat("_Fatigue", (float)State.fatigue / 100f);
        // Slow eyelid motion, never rapid flashing or a change to the user's camera pose.
        vision.SetFloat("_Breath", .5f + .5f * Mathf.Sin(SessionClock.Time * .5f));
    }
    public bool SleepUntilMorning()
    {
        if (!CanSleep) { return false; }
        ObserveClock(); double hours = SleepHours;
        if (hours <= 0) { return false; }
        State.Sleep(hours, (day.Day - 1) * 1440d + 1920d);
        day.StartMorningAfterSleep(); return true;
    }
    public void NightPassedAwake()
    {
        ObserveClock(); State.MissNight();
        if (State.dead) { PauseMenuController.Instance?.ShowDeath(); }
    }
    private void OnDestroy()
    { if (vision != null) { Destroy(vision); } if (Instance == this) { Instance = null; } }
}
```

## Assets/Scripts/Settings/PlayerRestState.cs
```csharp
using System;

/// <summary>Game-hour accounting, independent of rendering and the wall clock.</summary>
[Serializable]
public sealed class PlayerRestState
{
    public double fatigue, lastGameMinute = -1;
    public int sleeplessNights;
    public bool dead;
    public void Advance(double minute, double fatiguePerHour)
    {
        if (lastGameMinute >= 0 && !dead)
        { fatigue = Math.Clamp(fatigue + Math.Max(0, minute - lastGameMinute) / 60d * fatiguePerHour, 0d, 100d); }
        lastGameMinute = minute;
    }
    public void Sleep(double hours, double wakingMinute)
    {
        if (hours <= 0 || dead) { return; }
        fatigue = Math.Clamp(fatigue - hours * 15d, 0d, 100d);
        sleeplessNights = 0; lastGameMinute = wakingMinute;
    }
    public void MissNight()
    { if (++sleeplessNights >= 4) { dead = true; } }
}
```

## Assets/Scripts/Settings/SessionClock.cs
```csharp
using UnityEngine;

/// <summary>Restorable timestamps for the existing simulation; delta time and speed remain Unity's own.</summary>
public static class SessionClock
{
    private static float offset;
    public static float Time => UnityEngine.Time.time + offset;
    public static void Restore(float saved) { offset = saved - UnityEngine.Time.time; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetClock() { offset = 0; }
}
```

## Assets/Scripts/Settings/SessionStore.cs
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A versioned, local whole-session checkpoint. Shared stocks/orders remain shared; decimal money stays exact.
/// Only explicitly listed simulation components are roots. No UI hierarchy, callbacks, native handles or settings are serialized.
/// Reflection is used only on explicit save/load, never on a simulation tick. Unknown schemas/types fail closed.
/// </summary>
public static class SessionStore
{
    private const int Schema = 2, MaxNodes = 500000, MaxBytes = 128*1024*1024;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> RootNames = new(StringComparer.Ordinal)
    {
        "StockManager","StockStatistics","CandleHistoryManager","ExchangeWarehouse","MarketDayController","InvestorSociety",
        "PlayerProgression","PlayerAccounts","DailyIncomeController","FakeNewsManager","NewsManager","ItCompanyManager",
        "StockNewsAttentionManager","StockInterestManager","MarketSentiment","DelistingManager","PsychologicalLevelManager",
        "LiquidityBotManager","TradeBotManager","InstitutionBootstrapper","WorldDramaDirector","MarketRhythmDirector",
        "TradingOperationController","DeviceOrderSession","NpcLeadingRoom","CompanyNewsAudience","SocialContactController","PhoneCallController","IncumbentExchangeAI","PlayerRestController"
    };
    public static bool ValidationMode => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT"));
    public static string PathName => Path.Combine(ValidationMode ? Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT") : Application.persistentDataPath,"session-v2.json");
    public static bool Exists => File.Exists(PathName);
    public static bool CanSave => StockManager.Instance != null && StockManager.Instance.SetupConfirmed && MarketDayController.Instance != null &&
        MarketDayController.Instance.CheckpointReady && !MarketDayController.Instance.IntegrityFailed &&
        !StockManager.Instance.stocks.Any(stock=>stock.IsDelisting);
    private static bool Root(Component value) => value is Trader || RootNames.Contains(value.GetType().Name);
    public static void Save()
    {
        if (!CanSave) { throw new InvalidOperationException("Only a completed closing report can create a checkpoint."); }
        var writer = new Graph();
        var roots = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).OrderBy(Key,StringComparer.Ordinal).ToArray();
        var bindings = new JArray();
        foreach (var component in roots) { bindings.Add(new JObject { ["key"] = Key(component), ["value"] = writer.WriteComponent(component), ["enabled"] = component.enabled, ["active"] = component.gameObject.activeSelf }); }
        var data = new JObject { ["schema"] = Schema, ["scene"] = "TradeScene", ["clock"] = SessionClock.Time,
            ["random"] = JsonUtility.ToJson(UnityEngine.Random.state), ["roots"] = bindings, ["nodes"] = writer.Nodes, ["types"] = writer.Types,
            ["newsCooldown"] = FakeNewsManager.Instance?.PlayerCooldown ?? 0, ["savedUtc"] = DateTime.UtcNow.Ticks,
            ["socialId"] = typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null).ToString() };
        string payload = data.ToString(Formatting.None);
        data = null; writer = null;
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length > MaxBytes) { throw new InvalidOperationException("Checkpoint exceeds storage limit."); }
        string file = new JObject { ["schema"] = Schema, ["sha256"] = Hash(bytes), ["payload"] = payload }.ToString(Formatting.None);
        Directory.CreateDirectory(Path.GetDirectoryName(PathName));
        string temp = PathName+".tmp"; File.WriteAllText(temp,file,new UTF8Encoding(false));
        // Verify on disk before atomic replacement. A failed write leaves the previous save intact.
        Verify(File.ReadAllText(temp));
        if (File.Exists(PathName)) { File.Replace(temp,PathName,PathName+".bak"); } else { File.Move(temp,PathName); }
    }
    public static string Read()
    {
        if (new FileInfo(PathName).Length > MaxBytes*2L) { throw new InvalidDataException("Checkpoint exceeds storage limit."); }
        string text=File.ReadAllText(PathName); Verify(text); return text;
    }
    private static string Verify(string text)
    {
        using var reader = new JsonTextReader(new StringReader(text)) { MaxDepth = 128, DateParseHandling = DateParseHandling.None };
        var envelope=JObject.Load(reader);
        string payload=(string)envelope["payload"];
        if ((int?)envelope["schema"] != Schema || payload == null || payload.Length > MaxBytes || (string)envelope["sha256"] != Hash(Encoding.UTF8.GetBytes(payload)))
        { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }
        return payload;
    }
    private static JObject Decode(string text)
    {
        var data=JObject.Parse(Verify(text));
        if ((int?)data["schema"] != Schema || (string)data["scene"] != "TradeScene" || data["nodes"] is not JArray nodes || nodes.Count > MaxNodes)
        { throw new InvalidDataException("Invalid checkpoint schema."); }
        return data;
    }
    private static string Hash(byte[] bytes) { using var sha=SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }
    public static void Restore(string text)
    {
        if (MarketDayController.Instance == null || !MarketDayController.Instance.Ready) { throw new InvalidOperationException("World is not ready."); }
        RestoreData(Decode(text));
    }
    // A saved market may contain newcomers absent from a fresh scene. Let their normal Start lifecycle
    // finish while the world is paused, then restore balances/psychology over that initialized shell.
    public static IEnumerator RestoreInitialized(string text,Action<Exception> complete)
    {
        JObject data=null; bool created=false; Exception error=null;
        try
        {
            data=Decode(text);
            var nodes=(JArray)data["nodes"]; var types=(JArray)data["types"];
            var existing=new HashSet<string>(Object.FindObjectsByType<Trader>(FindObjectsInactive.Include).Select(Key),StringComparer.Ordinal);
            var idField=typeof(Trader).GetField("<InitialAccountId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var binding in ((JArray)data["roots"]).OrderBy(b=> ParseTraderId((string)b["key"])))
            {
                string key=(string)binding["key"];
                if (!key.StartsWith("trader:",StringComparison.Ordinal) || existing.Contains(key)) { continue; }
                var node=nodes[(int)binding["value"]["r"]]; Type type=Type.GetType((string)types[(int)node["t"]],false);
                if (type==null || !typeof(TradeBot).IsAssignableFrom(type) || type.IsAbstract || type==typeof(ExchangeBot))
                { throw new InvalidDataException("Cannot recreate checkpoint participant: "+key); }
                var go=new GameObject(type.Name+"_Restored"); var trader=(Trader)go.AddComponent(type);
                idField.SetValue(trader,ParseTraderId(key)); existing.Add(key); created=true;
            }
        }
        catch(Exception ex) { error=ex; }
        if (error!=null) { complete(error); yield break; }
        if (created) { yield return null; yield return null; }
        try { RestoreData(data); }
        catch(Exception ex) { error=ex; }
        complete(error);
    }
    private static int ParseTraderId(string key)
    { return key.StartsWith("trader:",StringComparison.Ordinal) && int.TryParse(key.Split(':')[1],out int id) ? id : 0; }
    private static void RestoreData(JObject data)
    {
        var graph=new Graph();
        graph.Restore(data);
        typeof(Trader).GetField("nextInitialAccountId",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Trader.Registry.Max(t=>t.InitialAccountId));
        SessionClock.Restore((float)data["clock"]);
        UnityEngine.Random.state=JsonUtility.FromJson<UnityEngine.Random.State>((string)data["random"]);
        FakeNewsManager.Instance?.RestorePlayerCooldown((double)data["newsCooldown"],(long)data["savedUtc"]);
        typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
        InvestorSociety.Instance?.ReconnectSession();
        var day=MarketDayController.Instance;
        day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
        foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
        foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
        var roots=(JArray)data["roots"];
        var components=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).ToDictionary(Key,StringComparer.Ordinal);
        foreach (var binding in roots)
        {
            var component=components[(string)binding["key"]];
            component.enabled=(bool)binding["enabled"];
            if (component.gameObject.activeSelf!=(bool)binding["active"]) { component.gameObject.SetActive((bool)binding["active"]); }
        }
        GameLocale.ProtectLiteral(DailyIncomeController.Instance?.CompanyName);
        foreach (var bot in MarketDayController.Instance.Bots) { if (bot?.Social != null) { GameLocale.ProtectLiteral(bot.Social.Nickname); } }
        foreach (var list in Object.FindObjectsByType<StockListUIManager>(FindObjectsInactive.Include)) { list.ReconnectSession(); }
        var market=StockManager.Instance; int selected=market.stocks.IndexOf(market.CurrentStock);
        market.CurrentStock=null;
        if (market.stocks.Count>0) { market.SelectStock(Math.Max(0,selected)); }
    }
    private static string Key(Object value)
    {
        if (value is Trader trader) { return "trader:"+trader.InitialAccountId+":"+trader.GetType().FullName; }
        if (value is Component component)
        {
            string path=component.name; var parent=component.transform.parent;
            while (parent != null) { path=parent.name+"/"+path; parent=parent.parent; }
            int index=Array.IndexOf(component.GetComponents(component.GetType()),component);
            return "component:"+component.GetType().FullName+":"+path+":"+index;
        }
        return "asset:"+value.GetType().FullName+":"+value.name;
    }
    private sealed class ReferenceComparer : IEqualityComparer<object>
    { public new bool Equals(object a,object b) => ReferenceEquals(a,b); public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value); }
    private sealed class Graph
    {
        public readonly JArray Nodes = new();
        public readonly JArray Types = new();
        private readonly Dictionary<Type,int> typeIds = new();
        private readonly Dictionary<object,int> ids = new(new ReferenceComparer());
        private readonly Dictionary<string,Object> unity = new(StringComparer.Ordinal);
        private readonly Dictionary<string,Stock> stocks = new(StringComparer.Ordinal);
        private object[] restored;
        private JArray saved;
        private readonly Dictionary<Type,FieldInfo[]> fields = new();
        public Graph()
        {
            foreach(var component in Object.FindObjectsByType<Component>(FindObjectsInactive.Include)) { unity[Key(component)]=component; }
            foreach(var stock in StockManager.Instance.stocks.Concat(StockManager.Instance.UnlistedStocks)) { stocks[stock.symbol]=stock; }
        }
        private FieldInfo[] Fields(Type type)
        {
            if (fields.TryGetValue(type,out var found)) { return found; }
            var all=new List<FieldInfo>();
            for(var current=type; current!=null && current!=typeof(object) && current!=typeof(MonoBehaviour);current=current.BaseType)
            {
                foreach(var field in current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    var t=field.FieldType;
                    if (typeof(Delegate).IsAssignableFrom(t) || typeof(System.IO.Stream).IsAssignableFrom(t) ||
                        t==typeof(Coroutine) || typeof(YieldInstruction).IsAssignableFrom(t) || t==typeof(SocialLogSink) ||
                        t.Namespace=="Unity.Profiling" || t==typeof(Transform) || t==typeof(RectTransform) || t==typeof(GameObject) ||
                        typeof(Object).IsAssignableFrom(t) && !typeof(Trader).IsAssignableFrom(t) && !RootNames.Contains(t.Name)) { continue; }
                    if (type==typeof(PhoneCallController) && (field.Name=="raised" || field.Name=="previousLocked")) { continue; }
                    if (type==typeof(PhoneCallController) && field.Name=="nextUi" || type==typeof(PlayerAccounts) && field.Name=="next") { continue; }
                    if (type==typeof(InvestorSociety) && (field.Name=="socialWired" || field.Name=="nextSocialLogFlush")) { continue; }
                    all.Add(field);
                }
            }
            return fields[type]=all.ToArray();
        }
        private static string FieldKey(FieldInfo field) => field.DeclaringType.FullName+"|"+field.Name;
        public JToken WriteComponent(Component value) => Write(value,true);
        private int TypeId(Type type)
        { if (!typeIds.TryGetValue(type,out int id)) { id=Types.Count; typeIds.Add(type,id); Types.Add(type.AssemblyQualifiedName); } return id; }
        private JToken Write(object value,bool component=false,Type expected=null)
        {
            if (value==null || value is Object obj && obj==null) { return JValue.CreateNull(); }
            Type type=value.GetType();
            if (value is Object native && !component) { return new JObject { ["u"]=Key(native) }; }
            if (type==typeof(string)) { return new JValue((string)value); }
            if (type.IsPrimitive || type.IsEnum || type==typeof(decimal))
            { return expected != null && expected!=typeof(object) ? new JValue(Convert.ToString(value,Culture)) : new JObject { ["p"]=TypeId(type),["v"]=Convert.ToString(value,Culture) }; }
            if (!Allowed(type)) { throw new InvalidDataException("Unsupported session type: "+type); }
            if (!type.IsValueType && ids.TryGetValue(value,out int existing)) { return new JObject { ["r"]=existing }; }
            int id=Nodes.Count;
            if (id>=MaxNodes) { throw new InvalidDataException("Checkpoint object budget reached."); }
            var node=new JObject { ["t"]=TypeId(type) }; Nodes.Add(node);
            if (!type.IsValueType) { ids.Add(value,id); }
            if (component) { node["component"]=Key((Object)value); }
            if (value is Stock stock && !string.IsNullOrEmpty(stock.symbol)) { node["stock"]=stock.symbol; }
            if (value is IDictionary dictionary)
            {
                var entries=new JArray(); node["dict"]=entries;
                if (type.IsGenericType && type.GetGenericTypeDefinition()==typeof(SortedList<,>))
                {
                    var comparer=type.GetProperty("Comparer").GetValue(value);
                    var keys=type.GetGenericArguments()[0];
                    if (keys!=typeof(decimal)) { throw new InvalidDataException("Unsupported sorted key."); }
                    node["descending"]=((IComparer<decimal>)comparer).Compare(0m,1m)>0;
                }
                var arguments=type.GetGenericArguments();
                foreach(DictionaryEntry pair in dictionary) { entries.Add(new JArray(Write(pair.Key,false,arguments[0]),Write(pair.Value,false,arguments[1]))); }
            }
            else if (value is IEnumerable enumerable && type!=typeof(string))
            { var values=new JArray(); node["items"]=values; var itemType=type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]; foreach(var item in enumerable) { values.Add(Write(item,false,itemType)); } }
            else
            {
                var values=new JObject(); node["fields"]=values;
                foreach(var field in Fields(type)) { values[FieldKey(field)]=Write(field.GetValue(value),false,field.FieldType); }
            }
            return new JObject { ["r"]=id };
        }
        private static bool Allowed(Type type)
        {
            if (type.IsPointer || typeof(Delegate).IsAssignableFrom(type)) { return false; }
            if (type.IsPrimitive || type.IsEnum || type==typeof(string) || type==typeof(decimal) || type==typeof(System.Random)) { return true; }
            if (type.Assembly==typeof(Stock).Assembly) { return !typeof(Object).IsAssignableFrom(type) || typeof(Trader).IsAssignableFrom(type) || RootNames.Contains(type.Name); }
            if (type.IsArray) { return Allowed(type.GetElementType()); }
            if (type.IsGenericType)
            {
                var def=type.GetGenericTypeDefinition(); string name=def.FullName;
                return (def==typeof(List<>) || def==typeof(Dictionary<,>) || def==typeof(SortedList<,>) || def==typeof(HashSet<>) || def==typeof(Queue<>) ||
                    def==typeof(Stack<>) || name.StartsWith("System.ValueTuple`",StringComparison.Ordinal) || def==typeof(Nullable<>)) && type.GetGenericArguments().All(Allowed);
            }
            return type.IsValueType && type.Namespace=="UnityEngine" || type.FullName.StartsWith("System.Random+",StringComparison.Ordinal);
        }
        private static Type Resolve(string name)
        {
            var type=Type.GetType(name,false);
            if(type==null || !Allowed(type)) { throw new InvalidDataException("Unsupported checkpoint type: "+name); }
            return type;
        }
        public void Restore(JObject data)
        {
            saved=(JArray)data["nodes"]; restored=new object[saved.Count];
            Types.Merge((JArray)data["types"]);
            var roots=(JArray)data["roots"];
            // Preflight every root and field before mutating the initialized world.
            foreach(var binding in roots)
            { if (!unity.TryGetValue((string)binding["key"],out var value) || value is not Component c || !Root(c)) { throw new InvalidDataException("Session participant missing: "+binding["key"]); } }
            for(int i=0;i<saved.Count;i++)
            {
                var node=(JObject)saved[i]; Type type=Resolve((string)Types[(int)node["t"]]);
                if (node["component"]!=null) { restored[i]=unity[(string)node["component"]]; }
                else if (node["stock"]!=null)
                {
                    if(!stocks.TryGetValue((string)node["stock"],out var stock)) { throw new InvalidDataException("Stock missing: "+node["stock"]); }
                    restored[i]=stock;
                }
                else if(type==typeof(LeadingRoomOrders))
                { restored[i]=InvestorSociety.Instance.EnsureSessionOrders(); }
                else if(type.IsArray) { restored[i]=Array.CreateInstance(type.GetElementType(),((JArray)node["items"]).Count); }
                else if(node["descending"]!=null && (bool)node["descending"])
                { restored[i]=Activator.CreateInstance(type,Comparer<decimal>.Create((a,b)=>b.CompareTo(a))); }
                else if(node["items"]!=null || node["dict"]!=null) { restored[i]=Activator.CreateInstance(type); }
                else { restored[i]=FormatterServices.GetUninitializedObject(type); }
                if(node["fields"] is JObject values)
                {
                    var names=new HashSet<string>(Fields(type).Select(FieldKey));
                    foreach(var field in values.Properties()) { if(!names.Contains(field.Name)) { throw new InvalidDataException("Session fields changed: "+field.Name); } }
                }
            }
            // Values (including tuple keys) are populated before the hash-based collections.
            for(int i=0;i<saved.Count;i++) { FillFields(i); }
            for(int i=0;i<saved.Count;i++) { FillCollection(i); }
        }
        private readonly HashSet<int> filling = new(), filled = new();
        private void FillFields(int index)
        {
            if(filled.Contains(index) || !filling.Add(index)) { return; }
            if(saved[index]["fields"] is JObject values)
            {
                foreach(var field in Fields(restored[index].GetType()))
                { if(values.TryGetValue(FieldKey(field),out var token)) { field.SetValue(restored[index],Read(token,field.FieldType)); } }
            }
            filling.Remove(index); filled.Add(index);
        }
        private object Read(JToken token,Type expected)
        {
            if(token==null || token.Type==JTokenType.Null) { return null; }
            if(token.Type==JTokenType.String)
            { var target=Nullable.GetUnderlyingType(expected) ?? expected; string value=(string)token; return target.IsEnum ? Enum.Parse(target,value) : Convert.ChangeType(value,target,Culture); }
            if(token["u"]!=null)
            { if(!unity.TryGetValue((string)token["u"],out var value)) { throw new InvalidDataException("Missing native reference: "+token["u"]); } return value; }
            if(token["r"]!=null)
            { int id=(int)token["r"]; if(id<0 || id>=restored.Length) { throw new InvalidDataException("Bad session reference."); } FillFields(id); return restored[id]; }
            var type=Resolve((string)Types[(int)token["p"]]); string raw=(string)token["v"];
            return type.IsEnum ? Enum.Parse(type,raw) : Convert.ChangeType(raw,type,Culture);
        }
        private void FillCollection(int index)
        {
            var target=restored[index]; var node=saved[index];
            if(node["dict"] is JArray entries)
            { var dictionary=(IDictionary)target; var arguments=target.GetType().GetGenericArguments(); foreach(var pair in entries) { dictionary.Add(Read(pair[0],arguments[0]),Read(pair[1],arguments[1])); } }
            else if(node["items"] is JArray items)
            {
                if(target is Array array) { for(int i=0;i<items.Count;i++) { array.SetValue(Read(items[i],array.GetType().GetElementType()),i); } }
                else
                {
                    string method=target.GetType().GetGenericTypeDefinition()==typeof(Queue<>) ? "Enqueue" : target.GetType().GetGenericTypeDefinition()==typeof(Stack<>) ? "Push" : "Add";
                    var add=target.GetType().GetMethod(method);
                    var sequence=method=="Push" ? items.Reverse() : items;
                    foreach(var item in sequence) { add.Invoke(target,new[]{Read(item,target.GetType().GetGenericArguments()[0])}); }
                }
            }
        }
    }
}
```

## Assets/Scripts/Settings/SystemMenuUI.cs
```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Cached pages on one translucent, blurred game backdrop. All text remains in TMP/localization.</summary>
public sealed class SystemMenuUI : IDisposable
{
    private readonly PauseMenuController owner;
    public readonly GameObject Root;
    public readonly RectTransform Paper; // Layout root only, no settings backplate.
    public readonly MenuBackdrop Backdrop;
    private readonly Dictionary<string,RectTransform> pages = new(), categories = new();
    private readonly List<Action> refreshers = new();
    private readonly TMP_FontAsset font;
    private readonly Sprite controlSprite;
    private readonly TextMeshProUGUI title, rest, reportDate, reportTotal, checkpointStatus, dialogText;
    private readonly TextMeshProUGUI[] reportValues;
    private readonly Button close, sleep, reportSleep, retrySave, confirm, cancel, continueSession;
    private readonly Button[] tabs;
    private readonly string[] categoryNames = { "그래픽","오디오","게임","UI" };
    private string category = "게임";
    private static readonly Color Ink = new(.92f,.94f,.96f), Muted = new(.63f,.67f,.71f);
    public SystemMenuUI(PauseMenuController controller)
    {
        owner=controller; font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        controlSprite=Resources.Load<Sprite>("UI/SystemMenu/MenuControl-v2");
        Root=new GameObject("Area :: System Menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); Root.transform.SetParent(owner.transform,false);
        var canvas=Root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000;
        var scaler=Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=1;
        var background=Area(Root.transform,"Image :: Blurred Game",0,0,0,0); Stretch(background);
        var raw=background.gameObject.AddComponent<RawImage>(); raw.raycastTarget=false; Backdrop=new MenuBackdrop(raw);
        var shade=Box(Root.transform,"Image :: Translucent Black",new Color(0,0,0,.64f)); Stretch(shade); shade.GetComponent<Image>().raycastTarget=true;
        Paper=Area(Root.transform,"Area :: Menu Layout",0,0,1200,700); Paper.anchorMin=Paper.anchorMax=Paper.pivot=new Vector2(.5f,.5f); Paper.anchoredPosition=Vector2.zero;
        title=Label(Paper,"일시정지",0,0,1200,64,38,FontStyles.Bold);
        close=Button(Paper,"Button :: Close","돌아가기",1000,640,200,48,owner.Back);
        var pause=NewPage("pause");
        var actions=new (string,Action)[] { ("이어서 하기",owner.Resume),("설정",()=>owner.Settings()),("세션 떠나기",owner.ConfirmDiscard),("게임 종료",owner.ConfirmQuit) };
        for (int i=0;i<actions.Length;i++) { Button(pause,"Button :: Pause "+i,actions[i].Item1,390,120+i*66,420,52,actions[i].Item2,i==0); }
        sleep=Button(pause,"Button :: Sleep","08:00까지 취침",390,416,420,52,owner.Sleep);
        rest=Label(pause,"",270,500,660,42,22); rest.alignment=TextAlignmentOptions.Center;
        var home=NewPage("home");
        continueSession=Button(home,"Button :: Continue Session","마지막 마감에서 계속",390,120,420,52,owner.LoadSession,true);
        Button(home,"Button :: New Session","새 세션",390,186,420,52,owner.NewSession);
        Button(home,"Button :: Home Settings","설정",390,252,420,52,()=>owner.Settings());
        Button(home,"Button :: Home Quit","게임 종료",390,318,420,52,owner.Quit);
        var settings=NewPage("settings"); tabs=new Button[4];
        for (int i=0;i<categoryNames.Length;i++)
        {
            string name=categoryNames[i]; tabs[i]=Button(settings,"Button :: Category "+i,name,i*300,90,288,48,()=>Settings(name));
            categories.Add(name,Area(settings,"Area :: "+name,40,180,1120,420));
        }
        BuildSettings(); Button(settings,"Button :: Reset Preferences","기본값 복원",0,640,230,48,owner.ConfirmDefaults);
        var dialog=NewPage("dialog"); dialogText=Label(dialog,"",250,156,700,150,25); dialogText.alignment=TextAlignmentOptions.Center;
        confirm=Button(dialog,"Button :: Confirm Action","확인",295,350,290,52,()=> { },true);
        cancel=Button(dialog,"Button :: Cancel Action","취소",615,350,290,52,owner.Back);
        var report=NewPage("report"); reportDate=Label(report,"",0,72,400,32,22);
        reportTotal=Label(report,"",0,120,560,80,48,FontStyles.Bold); Label(report,"당일 합계",0,202,520,30,21);
        string[] rows={ "당일 실현손익","기본 수입","이용료 수입","손실 보전","당일 세금","납부 세금","미납 세금","총자산" };
        reportValues=new TextMeshProUGUI[rows.Length];
        for (int i=0;i<rows.Length;i++)
        {
            float x=i/4*640, y=270+i%4*56; Label(report,rows[i],x,y,240,44,22);
            reportValues[i]=Label(report,"",x+235,y,325,44,25); reportValues[i].alignment=TextAlignmentOptions.MidlineRight; Line(report,x,y+49,560);
        }
        checkpointStatus=Label(report,"",0,520,1200,38,21);
        retrySave=Button(report,"Button :: Retry Save","다시 저장",0,586,300,52,owner.ReceiveClosingReport);
        Button(report,"Button :: Prepare Night","준비 계속",575,586,300,52,owner.ContinueAfterReport);
        reportSleep=Button(report,"Button :: Report Sleep","08:00까지 취침",900,586,300,52,owner.Sleep,true);
        var death=NewPage("death"); var deathText=Label(death,"4일 연속 밤샘",250,156,700,80,25); deathText.alignment=TextAlignmentOptions.Center;
        Button(death,"Button :: Restart Checkpoint","마지막 마감으로",390,310,420,52,()=> { if (SessionStore.Exists) { owner.LoadSession(); } else { owner.NewSession(); } },true);
        Root.SetActive(false);
    }
    private RectTransform NewPage(string key)
    { var rect=Area(Paper,"Area :: Menu "+key,0,0,1200,700); pages.Add(key,rect); rect.gameObject.SetActive(false); return rect; }
    private void Show(string key,string heading)
    {
        foreach (var pair in pages) { pair.Value.gameObject.SetActive(pair.Key==key); }
        title.alignment=key=="settings" || key=="report" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        GameLocalizedText.Set(title,heading); close.gameObject.SetActive(key=="settings"); close.transform.SetAsLastSibling();
        EventSystem.current?.SetSelectedGameObject(null); Refresh();
    }
    public void Pause() { Show("pause","일시정지"); }
    public void Home() { Show("home","DONDON"); continueSession.interactable=SessionStore.Exists; }
    public void Settings(string selected=null)
    {
        if (selected!=null) { category=selected; }
        foreach (var pair in categories) { pair.Value.gameObject.SetActive(pair.Key==category); }
        Show("settings","설정");
    }
    public void Refresh()
    {
        Paper.localScale=Vector3.one*GameSettings.Current.menuScale;
        foreach (var refresh in refreshers) { refresh(); }
        for (int i=0;i<tabs.Length;i++) { Tint(tabs[i],categoryNames[i]==category); }
        var player=PlayerRestController.Instance; sleep.gameObject.SetActive(player!=null && player.CanSleep);
        if (player!=null) { GameLocalizedText.Set(rest,$"피로 {player.State.fatigue:0}% · 밤샘 {player.State.sleeplessNights}/3"); }
    }
    private void BuildSettings()
    {
        var body=categories["그래픽"]; Header(body,"디스플레이",0);
        Choice(body,"화면 모드",48,new[]{"창 모드","전체 화면"},()=>GameSettings.Current.fullscreen ? 1 : 0,v=>GameSettings.Current.fullscreen=v==1);
        Header(body,"렌더링",130);
        Choice(body,"품질",178,new[]{"낮음","높음"},()=>GameSettings.Current.quality,v=>GameSettings.Current.quality=v);
        Choice(body,"프레임 제한",242,new[]{"30","60","120"},()=>Array.IndexOf(new[]{30,60,120},GameSettings.Current.frameRate),v=>GameSettings.Current.frameRate=new[]{30,60,120}[v]);
        Choice(body,"수직 동기화",306,new[]{"끔","켬"},()=>GameSettings.Current.vSync ? 1 : 0,v=>GameSettings.Current.vSync=v==1);
        body=categories["오디오"]; Header(body,"출력",0);
        Slider(body,"전체 음량",55,0,1,()=>GameSettings.Current.volume,v=>GameSettings.Current.volume=v,v=>Mathf.RoundToInt(v*100)+"%");
        Choice(body,"다른 창 사용 시 음소거",128,new[]{"끔","켬"},()=>GameSettings.Current.muteUnfocused ? 1 : 0,v=>GameSettings.Current.muteUnfocused=v==1);
        body=categories["게임"]; Header(body,"언어",0);
        Choice(body,"표시 언어",48,new[]{"한국어","English","日本語"},()=>Array.IndexOf(new[]{"ko","en","ja"},GameLocale.Code),v=>GameLocale.SetLanguage(new[]{"ko","en","ja"}[v]),false);
        Header(body,"마우스",130);
        Slider(body,"시점 감도",185,.25f,2.5f,()=>GameSettings.Current.sensitivity,v=>GameSettings.Current.sensitivity=v,v=>v.ToString("0.00")+"×");
        Slider(body,"휠 감도",258,1,6,()=>GameSettings.Current.wheel,v=>GameSettings.Current.wheel=v,v=>v.ToString("0.0"));
        body=categories["UI"]; Header(body,"시스템 메뉴",0);
        Slider(body,"메뉴 크기",55,.9f,1.1f,()=>GameSettings.Current.menuScale,v=>GameSettings.Current.menuScale=v,v=>Mathf.RoundToInt(v*100)+"%");
    }
    public void Report(bool saved)
    {
        Show("report","장 마감 정산"); var reports=DailyIncomeController.Instance?.Ledger.Reports;
        if (reports==null || reports.Count==0) { return; }
        var report=reports[reports.Count-1]; GameLocalizedText.Set(reportDate,"DAY "+report.day+"  17:00",true);
        GameLocalizedText.Set(reportTotal,report.DayResult.ToString("+#,##0;-#,##0;0")+"원");
        reportTotal.color=report.DayResult>0 ? new Color(.94f,.36f,.4f) : report.DayResult<0 ? new Color(.3f,.59f,.91f) : Ink;
        decimal[] values={report.Realized,report.company,report.fees,report.Relief,report.Tax,report.TaxPaid,report.UnpaidTax,report.Assets};
        for (int i=0;i<values.Length;i++) { GameLocalizedText.Set(reportValues[i],values[i].ToString("N0")+"원"); }
        GameLocalizedText.Set(checkpointStatus,saved ? "마감 저장 완료" : "저장 실패 · 이전 마감 유지");
        retrySave.gameObject.SetActive(!saved);
        reportSleep.interactable=PlayerRestController.Instance!=null && PlayerRestController.Instance.CanSleep;
    }
    public void Death() { Show("death","과로로 사망했습니다"); }
    public void Confirm(string heading,string message,Action accept,string caption)
    {
        Show("dialog",heading); GameLocalizedText.Set(dialogText,message); GameLocalizedText.Set(confirm.GetComponentInChildren<TextMeshProUGUI>(),caption);
        confirm.onClick.RemoveAllListeners(); confirm.onClick.AddListener(()=>accept()); cancel.gameObject.SetActive(true); confirm.interactable=true;
    }
    public void Busy(string heading) { Confirm(heading,"",()=> { },heading); confirm.interactable=false; cancel.gameObject.SetActive(false); }
    public void Error(string message) { Confirm("처리하지 못했습니다",message,owner.Back,"돌아가기"); }
    private void Header(Transform body,string text,float y) { Label(body,text,0,y,1120,30,21,FontStyles.Bold); Line(body,0,y+36,1120); }
    private void Choice(Transform body,string label,float y,string[] names,Func<int> get,Action<int> set,bool save=true)
    {
        Label(body,label,0,y,380,48,23); var buttons=new Button[names.Length]; float width=(700-8*(names.Length-1))/names.Length;
        for (int i=0;i<names.Length;i++)
        {
            int index=i; buttons[i]=Button(body,"Button :: "+label+" "+i,names[i],420+i*(width+8),y,width,48,()=> { set(index); if (save) { GameSettings.Commit(); } Refresh(); });
            if (!save) { GameLocalizedText.Set(buttons[i].GetComponentInChildren<TextMeshProUGUI>(),names[i],true); }
        }
        refreshers.Add(()=> { for (int i=0;i<buttons.Length;i++) { Tint(buttons[i],get()==i); } });
    }
    private void Slider(Transform body,string label,float y,float min,float max,Func<float> get,Action<float> set,Func<float,string> format)
    {
        Label(body,label,0,y,380,48,23); var frame=Box(body,"Area :: "+label,new Color(1,1,1,.14f)); Place(frame,420,y,700,48);
        var slider=frame.gameObject.AddComponent<Slider>(); slider.targetGraphic=frame.GetComponent<Image>(); frame.GetComponent<Image>().raycastTarget=true; slider.transition=Selectable.Transition.None;
        var inset=Area(frame,"Area :: Gauge Interior",2,2,696,44); var fill=Box(inset,"Image :: Gauge Fill",new Color(.76f,.82f,.87f,.37f)); Stretch(fill); slider.fillRect=fill;
        slider.minValue=min; slider.maxValue=max; slider.SetValueWithoutNotify(get());
        var number=Label(frame,format(get()),16,0,668,48,22,FontStyles.Bold); number.alignment=TextAlignmentOptions.MidlineRight; number.color=Color.white;
        slider.onValueChanged.AddListener(value=> { set(value); GameSettings.Commit(); });
        refreshers.Add(()=> { slider.SetValueWithoutNotify(get()); GameLocalizedText.Set(number,format(get()),true); });
    }
    private Button Button(Transform parent,string name,string caption,float x,float y,float w,float h,Action action,bool primary=false)
    {
        var rect=Box(parent,name,Color.white); Place(rect,x,y,w,h); var graphic=rect.GetComponent<Image>(); graphic.raycastTarget=true;
        graphic.sprite=controlSprite; graphic.type=Image.Type.Sliced; graphic.pixelsPerUnitMultiplier=2f;
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=graphic;
        var text=Label(rect,caption,16,0,w-32,h,23,primary ? FontStyles.Bold : FontStyles.Normal); text.alignment=TextAlignmentOptions.Center;
        button.onClick.AddListener(()=>action()); Tint(button,primary); return button;
    }
    private static void Tint(Button button,bool selected)
    {
        var colors=button.colors; colors.normalColor=new Color(1,1,1,selected ? .24f : .09f); colors.highlightedColor=new Color(1,1,1,.31f);
        colors.selectedColor=colors.normalColor; colors.pressedColor=new Color(1,1,1,.4f); colors.disabledColor=new Color(1,1,1,.035f); colors.fadeDuration=.1f; button.colors=colors;
    }
    private TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size,FontStyles style=FontStyles.Normal)
    {
        var rect=Area(parent,"Text :: "+text,x,y,w,h); var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font; label.fontSize=size; label.enableAutoSizing=false; label.fontStyle=style; label.color=size<23 ? Muted : Ink;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.textWrappingMode=TextWrappingModes.Normal; label.raycastTarget=false; GameLocalizedText.Set(label,text); return label;
    }
    private static void Line(Transform parent,float x,float y,float width)
    { var rect=Box(parent,"Image :: Divider",new Color(1,1,1,.12f)); Place(rect,x,y,width,1); }
    private static RectTransform Box(Transform parent,string name,Color color)
    { var rect=Area(parent,name,0,0,0,0); var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return rect; }
    private static RectTransform Area(Transform parent,string name,float x,float y,float w,float h)
    { var go=new GameObject(name,typeof(RectTransform)); var rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false); Place(rect,x,y,w,h); return rect; }
    private static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); }
    private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    public void Dispose() { Backdrop.Dispose(); }
}
```

## Assets/Editor/SystemMenuSetup.cs
```csharp
using UnityEditor;
using UnityEngine;

public static class SystemMenuSetup
{
    [MenuItem("DonDon/Settings/Import menu artwork")]
    public static void Import()
    {
        const string path="Assets/Resources/UI/SystemMenu/MenuControl-v2.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.spriteBorder=new Vector4(32,32,32,32);
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect; settings.spriteAlignment=(int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        DonDonLocalizationSetup.Import();
    }
}
```

## Assets/Editor/SystemMenuValidation.cs
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

/// <summary>Explicit, bounded UI/session tests; isolated save location and backed-up preferences.</summary>
public static class SystemMenuValidation
{
    [Serializable] private sealed class PrefBackup { public bool languageExists,settingsExists; public string language,settings; }
    private static string output;
    private static PrefBackup prefs;
    private static bool oldOptions,created,finished,restart;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase,locale,category,checks;
    private static float due,pausedClock;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static string snapshot;
    private static decimal cash,reserved,owned,worldCash;
    private static long orderNumber;
    private static int chatCount;
    private static int closingDay;
    private static double savedFatigue;
    private static int inactiveTraderCount;
    private static int newcomerId;
    private static string selectedSymbol;
    private static readonly string[] Languages={"ko","en","ja"};
    private static readonly string[] Categories={"게임","그래픽","오디오","UI"};
    private static readonly List<string> errors=new();
    public static void Run() { Begin(false); }
    public static void AfterRestart() { Begin(true); }
    private static void Begin(bool afterRestart)
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if(string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Test output must be inside workspace."); }
        Directory.CreateDirectory(output); restart=afterRestart;
        string backup="Validation/Settings/preferences-before.json";
        if(!afterRestart)
        {
            if(File.Exists(backup) && !File.Exists("Validation/Settings/preferences-restored.flag")) { prefs=JsonUtility.FromJson<PrefBackup>(File.ReadAllText(backup)); }
            else
            {
                prefs=new PrefBackup { languageExists=PlayerPrefs.HasKey("DonDon.Language"),language=PlayerPrefs.GetString("DonDon.Language"),settingsExists=PlayerPrefs.HasKey(GameSettings.Preference),settings=PlayerPrefs.GetString(GameSettings.Preference) };
                File.WriteAllText(backup,JsonUtility.ToJson(prefs));
                if(File.Exists("Validation/Settings/preferences-restored.flag")) { File.Delete("Validation/Settings/preferences-restored.flag"); }
            }
            PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.DeleteKey(GameSettings.Preference); PlayerPrefs.Save();
            SystemMenuSetup.Import();
        }
        else { prefs=JsonUtility.FromJson<PrefBackup>(File.ReadAllText(backup)); }
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if(finished) { return; }
        if(EditorApplication.timeSinceStartup-began>240) { Finish("Timeout"); return; }
        if(EditorApplication.isPlaying && !created) { created=true; new GameObject("Test :: Settings Probe").AddComponent<SystemMenuProbe>(); }
    }
    private static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception) { errors.Add(message); File.AppendAllText(Path.Combine(output,"errors.txt"),message+"\n"+stack+"\n"); } }
    private static void Check(bool pass,string name)
    {
        File.AppendAllText(Path.Combine(output,"checks.txt"),(pass ? "PASS " : "FAIL ")+name+"\n");
        if(!pass) { throw new InvalidOperationException(name); } checks++;
    }
    private static Button Button(string name) => PauseMenuController.Instance.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    public static void Step()
    {
        if(finished || !GameLocale.Ready || PauseMenuController.Instance==null || Time.unscaledTime<due) { return; }
        try
        {
            var menu=PauseMenuController.Instance;
            if(restart)
            {
                Check(GameLocale.Code=="ja","Japanese preference survived a fresh Unity process");
                Check(Mathf.Abs(GameSettings.Current.volume-.37f)<.001f && Mathf.Abs(GameSettings.Current.menuScale-1.1f)<.001f,"audio and UI settings survived restart");
                menu.Open(); menu.Settings(); Capture("restart-ja",1600,900); Audit(); Finish(null); return;
            }
            if(phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if(setup==null || setup.businessName==null) { return; }
                setup.businessName.SetTextWithoutNotify("설정 검증 회사"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase=1; due=Time.unscaledTime+2; return;
            }
            if(phase==1)
            {
                Time.timeScale=1;
                if(!MarketDayController.Instance.Ready || MarketDayController.Instance.Phase!=MarketPhase.Trading) { return; }
                StockManager.Instance.SelectStock(3); selectedSymbol=StockManager.Instance.CurrentStock.symbol;
                var orders=PlayerAccounts.Instance.primaryOrders; var stock=StockManager.Instance.CurrentStock;
                var newcomer=TradeBot.Create<RandomBot>("Test :: New Market Entrant");
                Check(newcomer!=null,"existing population factory can create a newcomer"); newcomerId=newcomer.InitialAccountId;
                orders.Prepare(stock); Check(orders.Submit(stock,true,1,3,1,1),"pending buy created through existing order system");
                menu.Open(); pausedClock=SessionClock.Time; cameraPosition=Camera.main.transform.position; cameraRotation=Camera.main.transform.rotation;
                Check(menu.CurrentPage==PauseMenuController.Page.Pause && Time.timeScale==0 && Cursor.lockState==CursorLockMode.None,"menu pauses market and frees cursor");
                Check(menu.UI.Root.GetComponentsInChildren<Button>().Count()==4,"four centered pause actions, no mid-session save");
                Check(menu.UI.Paper.GetComponent<Image>()==null && menu.UI.Backdrop.Ready,"shared real-camera blur, no settings backplate");
                Check(Button("Button :: Pause 0").GetComponent<Image>().sprite!=null,"generated image used by menu buttons");
                RestModelChecks();
                Capture("pause-ko",1600,900); Audit(); menu.Settings(); phase=2; due=Time.unscaledTime+.5f; return;
            }
            if(phase==2)
            {
                menu.UI.Settings("게임"); Button("Button :: 표시 언어 "+locale).onClick.Invoke(); phase=3; due=Time.unscaledTime+.4f; return;
            }
            if(phase==3)
            {
                if(GameLocale.Code!=Languages[locale]) { return; }
                Check(Mathf.Approximately(pausedClock,SessionClock.Time),"market clock frozen while language changes: "+GameLocale.Code);
                Check(Camera.main.transform.position==cameraPosition && Camera.main.transform.rotation==cameraRotation,"view unchanged behind menu: "+GameLocale.Code);
                Check(GameLocale.MissingSources.Count==0,"complete String Table: "+GameLocale.Code);
                menu.UI.Settings(Categories[category]); Audit(); Capture(GameLocale.Code+"-"+category,1600,900); Capture(GameLocale.Code+"-"+category,1920,1080);
                if(++category<Categories.Length) { due=Time.unscaledTime+.15f; return; }
                category=0; menu.UI.Pause(); Capture("pause-"+GameLocale.Code,1920,1080); Audit();
                if(++locale<Languages.Length) { phase=2; return; }
                menu.UI.Settings("게임"); phase=12; due=Time.unscaledTime+.2f; return;
            }
            if(phase==12) { PointerChecks(); phase=4; return; }
            if(phase==4)
            {
                menu.UI.Settings("오디오"); var volume=menu.UI.Root.GetComponentsInChildren<Slider>().Single(); volume.value=.37f;
                Check(Mathf.Abs(GameSettings.Current.volume-.37f)<.001f,"audio slider changes real preference");
                menu.UI.Settings("UI"); menu.UI.Root.GetComponentsInChildren<Slider>().Single().value=1.1f;
                Check(Mathf.Abs(menu.UI.Paper.localScale.x-1.1f)<.001f,"menu scale applied immediately"); Audit(); Capture("ja-ui-large",1600,900);
                menu.Back(); menu.Resume(); Check(Time.timeScale==1 && !PauseMenuController.IsOpen,"resume restores original running state");
                Time.timeScale=0; menu.Open(); menu.Resume(); Check(Time.timeScale==0,"resume preserves pre-existing pause");
                Time.timeScale=1; menu.Open();
                Check(!SessionStore.CanSave,"mid-session checkpoints prohibited");
                bool denied=false; try { SessionStore.Save(); } catch(InvalidOperationException) { denied=true; }
                Check(denied && !SessionStore.Exists,"no checkpoint created by mid-session save attempt");
                menu.Resume();
                var inactive=MarketDayController.Instance.Bots.First(b=>b.isActiveAndEnabled && !b.IsExchangeParticipant);
                inactive.PrepareForDeparture(); inactive.gameObject.SetActive(false);
                SetElapsed(MarketDayController.Instance.tradingSeconds);
                phase=5; return;
            }
            if(phase==5)
            {
                if(menu.CurrentPage!=PauseMenuController.Page.Report) { return; }
                var day=MarketDayController.Instance;
                Check(day.Phase==MarketPhase.PostClose && day.GameMinute==1020 && day.ReportPending,"closing report delivered at 17:00 with night paused");
                Check(SessionStore.Exists,"closing pipeline wrote atomic checkpoint");
                Check(PlayerAccounts.Instance.primary.ReservedCash==0 && PlayerAccounts.Instance.primary.EscrowVolume==0,"closing releases order cash and shares");
                Check(DailyIncomeController.Instance.Ledger.SettledThrough==day.Day,"daily tax and income settled before checkpoint");
                Capture("report-ja",1920,1080); Audit();
                cash=PlayerAccounts.Instance.primary.cash; reserved=PlayerAccounts.Instance.primary.ReservedCash;
                owned=PlayerAccounts.Instance.primary.GetOwnedVolume(StockManager.Instance.CurrentStock); worldCash=Trader.Registry.Sum(t=>t.cash);
                chatCount=day.MessageCount; closingDay=day.Day; savedFatigue=PlayerRestController.Instance.State.fatigue;
                inactiveTraderCount=Trader.Registry.Count(t=>!t.gameObject.activeSelf);
                snapshot=SessionStore.Read();
                PlayerAccounts.Instance.primary.cash+=12345m;
                PlayerRestController.Instance.State.fatigue=99;
                SessionStore.Restore(snapshot);
                AssertLedger();
                Check(MarketDayController.Instance.MessageCount==chatCount,"chat history restored with market state");
                Check(PlayerRestController.Instance.State.fatigue==savedFatigue,"fatigue restored from closing checkpoint");
                foreach(var stock in StockManager.Instance.stocks) { Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"share conservation after restore: "+stock.symbol); }
                snapshot=null; GC.Collect();
                menu.SessionLoaded+=Loaded; menu.LoadSession(); phase=6; due=Time.unscaledTime+.5f; return;
            }
            if(phase==6) { return; }
            if(phase==7)
            {
                Check(GameLocale.Code=="ja","session restore does not overwrite language");
                var day=MarketDayController.Instance; menu.ContinueAfterReport();
                Check(Time.timeScale==1 && !day.ReportPending && !MarketDayController.CanTrade,"night preparation resumes, trading remains closed");
                SetElapsed(day.tradingSeconds); // 8 game hours after 17:00 = 01:00, same clock pace.
                Check(day.ClockMinute==60 && day.CalendarDay==closingDay+1,"night clock wraps to next calendar day 01:00");
                PlayerRestController.Instance.State.fatigue=100; PlayerRestController.Instance.ObserveClock();
                menu.Open(); Capture("night-pause-ja",1600,900); Button("Button :: Sleep").onClick.Invoke();
                Check(day.Phase==MarketPhase.PreOpen && day.Day==closingDay+1 && day.GameMinute==480,"sleep wakes at 08:00");
                Check(PlayerRestController.Instance.State.fatigue==0 && PlayerRestController.Instance.State.sleeplessNights==0,"seven-hour sleep clears fatigue and consecutive nights");
                Check(!MarketDayController.CanTrade,"08:00 preparation remains closed");
                SetElapsed(day.tradingSeconds/8f); phase=8; return;
            }
            if(phase==8)
            {
                var day=MarketDayController.Instance;
                if(day.Phase!=MarketPhase.Trading) { return; }
                Check(day.GameMinute>=540 && day.GameMinute<545 && MarketDayController.CanTrade,"next market opens at 09:00");
                PlayerRestController.Instance.State.fatigue=90; phase=9; due=Time.unscaledTime+.2f; return;
            }
            if(phase==9)
            {
                Capture("fatigue90",1920,1080);
                var player=PlayerRestController.Instance; player.State.sleeplessNights=3; player.NightPassedAwake();
                Check(player.State.dead && menu.CurrentPage==PauseMenuController.Page.Dead && Time.timeScale==0,"fourth sleepless night presents death and freezes play");
                Capture("death-ja",1600,900); Audit();
                menu.SessionLoaded-=Loaded; menu.SessionLoaded+=AfterDeathLoaded; menu.LoadSession(); phase=10; return;
            }
            if(phase==10) { return; }
            if(phase==11)
            {
                Check(errors.Count==0,"no runtime errors"); PlayerPrefs.Save(); Finish(null,true);
            }
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void Loaded()
    {
        try
        {
            PauseMenuController.Instance.Open(); AssertLedger();
            foreach(var stock in StockManager.Instance.stocks) { Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"share conservation after cold scene reload: "+stock.symbol); }
            Check(MarketDayController.Instance.GameMinute==1020 && MarketDayController.Instance.Day==closingDay,"cold reload resumes previous closing time");
            Check(PauseMenuController.Instance.CurrentPage==PauseMenuController.Page.Report,"reload shows saved closing report");
            Check(Trader.Registry.Count(t=>!t.gameObject.activeSelf)==inactiveTraderCount,"offline traders retain their inactive state on reload");
            Check(Trader.Registry.Any(t=>t.InitialAccountId==newcomerId && t is RandomBot),"new market entrant survives a cold scene reload");
            Check(StockManager.Instance.CurrentStock.symbol==selectedSymbol && Object.FindAnyObjectByType<ChartManager>().textSymbol.text==selectedSymbol,"selected stock and actual center display reconnect on reload");
            phase=7;
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void AfterDeathLoaded()
    {
        try
        {
            Check(!PlayerRestController.Instance.State.dead && PlayerRestController.Instance.State.fatigue==savedFatigue,"death restart restores living player and closing fatigue");
            Check(MarketDayController.Instance.Day==closingDay && MarketDayController.Instance.GameMinute==1020,"death restart returns to previous 17:00");
            AssertLedger(); phase=11;
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void SetElapsed(float seconds)
    { typeof(MarketDayController).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(MarketDayController.Instance,seconds); }
    private static void PointerChecks()
    {
        var ui=PauseMenuController.Instance.UI; ui.Settings("게임"); Canvas.ForceUpdateCanvases();
        var categoryButton=Button("Button :: Category 1");
        var buttonRect=(RectTransform)categoryButton.transform;
        var pointer=new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(null,buttonRect.TransformPoint(buttonRect.rect.center)),button=PointerEventData.InputButton.Left };
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        File.AppendAllText(Path.Combine(output,"pointer.txt"),"screen="+Screen.width+"x"+Screen.height+" point="+pointer.position+" rect="+buttonRect.rect+" canvas="+ui.Root.GetComponent<Canvas>().renderMode+" caster="+ui.Root.GetComponent<GraphicRaycaster>().enabled+" depth="+categoryButton.GetComponent<Image>().depth+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name))+"\n");
        Check(hits.Count>0 && hits[0].gameObject==categoryButton.gameObject,"overlay raycast reaches top settings tab");
        ExecuteEvents.Execute(categoryButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        var slider=ui.Root.GetComponentsInChildren<Slider>().Single();
        Canvas.ForceUpdateCanvases(); var rect=(RectTransform)slider.transform;
        pointer.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.75f,rect.rect.center.y,0)));
        slider.OnPointerDown(pointer); slider.OnDrag(pointer); slider.OnPointerUp(pointer);
        Check(Mathf.Abs(slider.normalizedValue-.75f)<.02f,"rectangular slider accepts pointer click and drag");
        Check(Mathf.Abs(slider.fillRect.anchorMax.x-slider.normalizedValue)<.001f,"filled gauge width tracks slider value");
        int count=ui.Root.GetComponentsInChildren<RectTransform>(true).Length;
        ui.Settings("UI"); ui.Settings("오디오");
        Check(count==ui.Root.GetComponentsInChildren<RectTransform>(true).Length,"page switching toggles existing objects without rebuilding");
    }
    private static void RestModelChecks()
    {
        var rest=new PlayerRestState { fatigue=100,lastGameMinute=0 };
        rest.Sleep(1,60); Check(rest.fatigue==85,"one sleeping hour recovers 15 percentage points");
        rest.fatigue=100; rest.Sleep(3,240); Check(rest.fatigue==55,"partial three-hour sleep recovers 45 points");
        rest.fatigue=100; rest.Sleep(7,660); Check(rest.fatigue==0,"seven sleeping hours clamp 105-point recovery to zero");
        rest.Advance(1140,2.5); Check(rest.fatigue==20,"eight waking game hours add tuned 20 fatigue");
        rest.Advance(1140,2.5); Check(rest.fatigue==20,"paused game clock adds no fatigue");
        for(int night=1;night<=3;night++) { rest.MissNight(); Check(!rest.dead && rest.sleeplessNights==night,"sleepless night "+night+" remains survivable"); }
        rest.MissNight(); Check(rest.dead,"fourth consecutive sleepless night is fatal");
    }
    private static void AssertLedger()
    {
        Check(PlayerAccounts.Instance.primary.cash==cash && PlayerAccounts.Instance.primary.ReservedCash==reserved,"cash and reservation restored exactly");
        Check(PlayerAccounts.Instance.primary.GetOwnedVolume(StockManager.Instance.CurrentStock)==owned,"player holdings preserved");
        Check(Trader.Registry.Sum(t=>t.cash)==worldCash,"all participant cash conserved");
    }
    private static void Audit()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var text in PauseMenuController.Instance.UI.Root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            if(text.isTextOverflowing || text.isTextTruncated) { throw new InvalidOperationException("Menu text clipped: "+text.text+" / "+text.rectTransform.rect); }
        }
        Check(true,"menu labels fit: "+GameLocale.Code+" category "+category);
    }
    private static void Capture(string name,int width,int height)
    {
        var camera=Camera.main; var overlays=new List<(Canvas canvas,Camera camera,float distance)>();
        var target=new RenderTexture(width,height,24); var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active; Texture2D pixels=null;
        try
        {
            foreach(var canvas in Object.FindObjectsByType<Canvas>())
            {
                if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay) { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+.1f;
            }
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=oldTarget; RenderTexture.active=oldActive; if(pixels!=null) { Object.Destroy(pixels); } target.Release(); Object.Destroy(target);
            foreach(var item in overlays) { item.canvas.renderMode=RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera=item.camera; item.canvas.planeDistance=item.distance; }
        }
    }
    private static void Finish(string failure,bool keepForRestart=false)
    {
        if(finished) { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if(!keepForRestart)
        {
            if(prefs.languageExists) { PlayerPrefs.SetString("DonDon.Language",prefs.language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); }
            if(prefs.settingsExists) { PlayerPrefs.SetString(GameSettings.Preference,prefs.settings); } else { PlayerPrefs.DeleteKey(GameSettings.Preference); }
            PlayerPrefs.Save();
            File.WriteAllText("Validation/Settings/preferences-restored.flag","Restored user preferences.");
        }
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class SystemMenuProbe : MonoBehaviour
{
    private void Awake() { DontDestroyOnLoad(gameObject); Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    private void LateUpdate() { SystemMenuValidation.Step(); }
}
```
