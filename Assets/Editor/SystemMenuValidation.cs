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
                if (!NewGame.CanBegin) { return; }
                NewGame.Begin("설정 검증 회사");
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
