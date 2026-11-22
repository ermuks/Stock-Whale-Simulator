using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Bounded, sequential real UI/persistence regression. Preferences and session saves are isolated.</summary>
public static class MarketColorsValidation
{
    [Serializable] private sealed class Preferences { public bool settingsExists,languageExists; public string settings,language; }
    private static string output;
    private static Preferences backup;
    private static bool cold,finished,spawned,oldOptions,visualOnly;
    private static EnterPlayModeOptions oldFlags;
    private static double began,clock;
    private static int phase,dueFrame,checks,errors,locale;
    private static decimal cash;
    private static long bookVersion;
    private static Color originalUp,originalDown;
    private static Stock stock;
    private static ChartManager chart;
    private static readonly string[] languages={"ko","en","ja"};
    private const string BackupPath="Validation/MarketColors/preferences-before.json", RestoredPath="Validation/MarketColors/preferences-restored.flag";
    public static void Run() { Begin(false); }
    public static void RunVisual() { visualOnly=true; Begin(false); }
    public static void AfterRestart() { Begin(true); }
    private static void Begin(bool restart)
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output)) { throw new InvalidOperationException("Use Tests/Settings/Run.ps1"); }
        Directory.CreateDirectory(output); cold=restart;
        if (File.Exists(BackupPath) && !File.Exists(RestoredPath)) { backup=JsonUtility.FromJson<Preferences>(File.ReadAllText(BackupPath)); }
        else
        {
            backup=new Preferences {settingsExists=PlayerPrefs.HasKey(GameSettings.Preference),settings=PlayerPrefs.GetString(GameSettings.Preference),languageExists=PlayerPrefs.HasKey("DonDon.Language"),language=PlayerPrefs.GetString("DonDon.Language")};
            File.WriteAllText(BackupPath,JsonUtility.ToJson(backup));
            if (File.Exists(RestoredPath)) { File.Delete(RestoredPath); }
        }
        if (!cold)
        {
            // Old v1 preferences without the new field must retain the Korean default.
            PlayerPrefs.SetString(GameSettings.Preference,"{\"version\":1,\"frameRate\":60,\"volume\":0.42,\"sensitivity\":1.2}");
            PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        }
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log; EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>150) { Finish("Time budget"); return; }
        if (EditorApplication.isPlaying && !spawned) { spawned=true; new GameObject("Test :: Market Color Probe").AddComponent<MarketColorProbe>(); }
    }
    private static void Log(string message,string stack,LogType type)
    { if (type==LogType.Error || type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),message+"\n"+stack+"\n"); } }
    private static void Check(bool ok,string name)
    {
        File.AppendAllText(Path.Combine(output,"checks.txt"),(ok ? "PASS " : "FAIL ")+name+"\n");
        if (!ok) { throw new Exception(name); }
        checks++;
    }
    private static bool Same(Color a,Color b) => Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.01f;
    private static void Wait(int next) { phase=next; dueFrame=Time.frameCount+12; }
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.frameCount<dueFrame)
        { return; }
        try
        {
            if (cold)
            {
                Check(GameSettings.Current.marketColors==MarketColors.GreenRed,"new Unity process restores green/red preference");
                Check(Same(MarketColors.Up,MarketColors.Preview(1,true)),"cold startup palette is applied");
                Check(Mathf.Abs(GameSettings.Current.volume-.42f)<.001f && Mathf.Abs(GameSettings.Current.sensitivity-1.2f)<.001f,"unrelated preferences survive restart");
                Check(errors==0,"no cold-start exceptions"); Finish(null); return;
            }
            if (phase==0)
            {
                if (!NewGame.CanBegin) { return; }
                Check(GameSettings.Current.marketColors==0,"old settings migrate to red/blue");
                NewGame.Begin("플레이어 기업"); Wait(1); return;
            }
            if (phase==1)
            {
                if (MarketDayController.Instance==null || !MarketDayController.Instance.Ready || MarketDayController.Instance.Phase!=MarketPhase.Trading)
                { return; }
                stock=StockManager.Instance.CurrentStock; chart=Object.FindAnyObjectByType<ChartManager>();
                DeskViewController.Instance.Focus(DeskViewTarget.Chart); Wait(2); return;
            }
            if (phase==2)
            {
                originalUp=chart.candleRenderer.materialUp.color; originalDown=chart.candleRenderer.materialDown.color;
                PauseMenuController.Instance.Open(); PauseMenuController.Instance.Settings(); PauseMenuController.Instance.UI.Settings("UI");
                clock=MarketDayController.Instance.AbsoluteGameMinute; cash=StockManager.Instance.playerAccount.cash; bookVersion=stock.orderBook.Version;
                Wait(3); return;
            }
            if (phase==3)
            {
                for (int i=0;i<2;i++)
                {
                    var button=Option(i);
                    Check(button.GetComponentsInChildren<TMP_Text>(true).Length==0,"candle choice has no text: "+i);
                    var preview=button.transform.Find("Image :: Candle Theme").GetComponent<Image>();
                    Check(preview.sprite!=null && preview.preserveAspect && !preview.raycastTarget,"one aspect-preserving PNG preview: "+i);
                    Check(preview.sprite.name==(i==0 ? "CandleThemeRedBlue" : "CandleThemeGreenRed"),"correct generated candle image: "+i);
                }
                VerifyPalette(0); Capture("settings-red-blue-ko",1600,900);
                Click(Option(1)); VerifyPalette(1);
                Check(Option(1).image.color.a>.8f && Option(0).image.color.a<.2f,"selected image button has clear border");
                int revision=MarketColors.Version; GameSettings.Commit(); Check(MarketColors.Version==revision,"unchanged settings do not repaint market");
                Check(JsonUtility.FromJson<GameSettings.Values>(PlayerPrefs.GetString(GameSettings.Preference)).marketColors==1,"selection stored in preferences");
                Wait(4); return;
            }
            if (phase==4)
            {
                if (GameLocale.Code!=languages[locale])
                { return; }
                VerifyPalette(1); Capture("settings-green-red-"+GameLocale.Code,1600,900); Capture("settings-green-red-"+GameLocale.Code,1920,1080);
                Audit();
                if (++locale<languages.Length) { GameLocale.SetLanguage(languages[locale]); Wait(4); return; }
                Click(Option(0)); VerifyPalette(0); Click(Option(1)); VerifyPalette(1);
                GameSettings.Current.marketColors=99; GameSettings.Commit(); Check(GameSettings.Current.marketColors==0,"invalid preference falls back to Korean default");
                Click(Option(1));
                Check(MarketDayController.Instance.AbsoluteGameMinute==clock && Time.timeScale==0,"menu remains paused during changes");
                Check(StockManager.Instance.playerAccount.cash==cash && stock.orderBook.Version==bookVersion,"theme does not mutate cash or orders");
                Check(Same(chart.candleRenderer.materialUp.color,originalUp) && Same(chart.candleRenderer.materialDown.color,originalDown),"shared candle material assets unchanged");
                GameLocale.SetLanguage("ko"); PauseMenuController.Instance.UI.Root.SetActive(false); Wait(5); return;
            }
            if (phase==5)
            {
                Capture("chart-green-red",1920,1080); Check(errors==0,"no runtime exceptions"); PlayerPrefs.Save(); Finish(null);
            }
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }
    private static Button Option(int index) => PauseMenuController.Instance.UI.Root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Button :: 상승 / 하락 "+index);
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); var rect=(RectTransform)button.transform;
        var p=new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"raycast reaches "+button.name);
        ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void VerifyPalette(int selected)
    {
        var up=MarketColors.Preview(selected,true); var down=MarketColors.Preview(selected,false);
        Check(GameSettings.Current.marketColors==selected && Same(MarketColors.Up,up) && Same(MarketColors.Down,down),"selected directional palette "+selected);
        var renderer=chart.candleRenderer.GetComponent<MeshRenderer>(); var block=new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block,0); Check(Same(block.GetColor("_Color"),up),"candle up material tint updates while paused");
        renderer.GetPropertyBlock(block,1); Check(Same(block.GetColor("_Color"),down),"candle down material tint updates while paused");
        decimal delta=stock.currentPrice-StockStatistics.Instance.GetStats(stock).prevDayClose;
        Check(Same(chart.textChangePct.color,MarketColors.ForChange(delta,new Color32(138,150,163,255))),"header direction follows theme");
        var book=Object.FindAnyObjectByType<OrderBookUIManager>();
        Check(book.buyParent.GetComponentsInChildren<DrawRawOrder>(true).All(r=>Same(r.txtPrice.color,down)),"bid rows follow lower-price palette");
        Check(book.sellParent.GetComponentsInChildren<DrawRawOrder>(true).All(r=>Same(r.txtPrice.color,up)),"ask rows follow higher-price palette");
        Check(Same(InvestmentDashboard.ProfitColor(1),up) && Same(InvestmentDashboard.ProfitColor(-1),down) && Same(InvestmentDashboard.ProfitColor(0),InvestmentDashboard.Ink),"profit/loss theme and neutral zero");
        Check(Same(TerminalGlyph.BandColor(100),new Color32(239,89,100,255)),"psychology heat remains warning red");
        foreach (var item in Object.FindObjectsByType<StockListItem>())
        {
            if (item.BoundStock==null || !StockStatistics.Instance.TryGetDailyChange(item.BoundStock,out var change))
            { continue; }
            Check(Same(item.textChangePct10.color,MarketColors.ForChange(change,new Color32(138,150,163,255))),"stock list refreshes while paused: "+item.BoundStock.symbol);
        }
        foreach (var item in Object.FindObjectsByType<TradeHistoryItem>())
        { Check(Same(item.txtPrice.color,up)||Same(item.txtPrice.color,down),"existing fill row uses selected pair"); }
    }
    private static void Audit()
    {
        var root=PauseMenuController.Instance.UI.Root; int count=0;
        foreach (var label in root.GetComponentsInChildren<TMP_Text>())
        {
            if (string.IsNullOrEmpty(label.text))
            { continue; }
            label.ForceMeshUpdate(); Check(!label.isTextOverflowing,"settings label fits "+GameLocale.Code+": "+label.text); count++;
        }
        Check(count>10,"visible settings content audited");
    }
    private static void Capture(string name,int width,int height)
    {
        var camera=Camera.main; var overlays=new List<(Canvas canvas,Camera camera,float distance)>();
        var target=new RenderTexture(width,height,24); var prior=camera.targetTexture; var active=RenderTexture.active; Texture2D pixels=null;
        try
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode!=RenderMode.ScreenSpaceOverlay)
                { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+.1f;
            }
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=prior; RenderTexture.active=active; if (pixels!=null) { Object.Destroy(pixels); } target.Release(); Object.Destroy(target);
            foreach (var item in overlays) { item.canvas.renderMode=RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera=item.camera; item.canvas.planeDistance=item.distance; }
        }
    }
    private static void Finish(string failure)
    {
        if (finished)
        { return; }
        finished=true; File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (cold || visualOnly || failure!=null)
        {
            if (backup.settingsExists) { PlayerPrefs.SetString(GameSettings.Preference,backup.settings); } else { PlayerPrefs.DeleteKey(GameSettings.Preference); }
            if (backup.languageExists) { PlayerPrefs.SetString("DonDon.Language",backup.language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); }
            PlayerPrefs.Save(); File.WriteAllText(RestoredPath,"restored");
        }
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class MarketColorProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { MarketColorsValidation.Step(); }
}
