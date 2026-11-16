using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, single-process Play Mode validation. Does not save the scene or game.</summary>
public static class DonDonLocalizationValidation
{
    private static readonly string[] Languages = { "ko", "en", "ja" };
    private static readonly string[] Views = { "chart", "stocks", "chat", "dashboard", "orders", "news", "settlement", "phone" };
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string folder, previousLanguage;
    private static bool previousExists, oldOptions, created, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, language, view, checks;
    private static float due;
    private static Camera camera;
    private static TabletAccountUI tablet;
    private static string orderInput;
    private static long articleId;
    private static float articlePosition;
    private static Vector3 canvasPosition;
    private static Quaternion canvasRotation;
    private static Camera eventCamera;
    [Serializable] private sealed class Row { public string path, source, text; public bool overflow, truncated; public float width, height, font; }
    [Serializable] private sealed class Rows { public List<Row> rows = new(); }
    private static void Check(bool ok,string label)
    {
        File.AppendAllText(Path.Combine(folder,"checks.txt"),(ok ? "PASS " : "FAIL ")+label+"\n");
        if (!ok) { throw new InvalidOperationException(label); }
        checks++;
    }
    public static void Run()
    {
        DonDonLocalizationSetup.Import();
        folder = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
        if (string.IsNullOrEmpty(folder) || !Path.GetFullPath(folder).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Validation output must be inside this project."); }
        Directory.CreateDirectory(folder);
        previousExists = PlayerPrefs.HasKey("DonDon.Language"); previousLanguage = PlayerPrefs.GetString("DonDon.Language","ko");
        PlayerPrefs.SetString("DonDon.Language","ko");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update += Monitor; Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
    public static void BuildContent()
    {
        try
        {
            DonDonLocalizationSetup.Import();
            var font = Resources.Load<TMP_FontAsset>("Localization/DonDon Japanese SDF");
            int glyphs = font.characterTable.Count;
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) { throw new InvalidOperationException(result.Error); }
            if (glyphs < 500 || font.characterTable.Count != glyphs) { throw new InvalidOperationException("Japanese warm glyphs were lost during build."); }
            string output = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
            File.WriteAllText(Path.Combine(output,"content-build.txt"),"PASS: Addressables player content built.\nPASS: "+glyphs+" warm Japanese characters retained.\n"+result.OutputPath);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void ValidateHeading()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity"); GameLocaleFonts.Load();
            var label = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include).First(t=>t.name == "Text :: Operation Monitor Heading");
            string output = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
            foreach (string text in new[] { "체결 흐름", "Trade flow", "約定の流れ" })
            {
                label.text = text; Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate(true);
                if (label.isTextOverflowing || label.isTextTruncated) { throw new InvalidOperationException("Caption overflow: "+text); }
                File.AppendAllText(Path.Combine(output,"heading.txt"),"PASS "+text+" at "+label.fontSize+"\n");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Log(string message,string stack,LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        { File.AppendAllText(Path.Combine(folder,"errors.txt"),message+"\n"+stack+"\n"); }
    }
    private static void Monitor()
    {
        if (finished) { return; }
        if (EditorApplication.timeSinceStartup-began > 330) { Finish("Timeout"); return; }
        if (EditorApplication.isPlaying && !created)
        { created = true; new GameObject("Test :: Locale Capture").AddComponent<DonDonLocalizationProbe>(); }
    }
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime < due) { return; }
        try
        {
            camera = DeskViewController.Instance?.ViewCamera ?? Camera.main;
            var setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
            if (phase == 0)
            {
                if (setup == null || setup.businessName == null) { return; }
                Check(LocalizationSettings.AvailableLocales.Locales.Count == 3,"only ko/en/ja locales");
                setup.businessName.SetTextWithoutNotify("안전 수익 회사");
                GameLocale.SetLanguage(Languages[language]); phase = 1; due = Time.unscaledTime+.7f; return;
            }
            if (phase == 1)
            {
                if (GameLocale.Code != Languages[language]) { return; }
                Check(setup.businessName.text == "안전 수익 회사","input unchanged: "+GameLocale.Code);
                Check(GameLocale.MissingSources.Count == 0,"no missing String Table entries: "+GameLocale.Code);
                Check(!GameLocale.Supported("zh") && !GameLocale.Supported("fr"),"unsupported languages excluded");
                Capture("setup-"+GameLocale.Code,1920,1080); Dump("setup-"+GameLocale.Code);
                if (++language < Languages.Length) { phase = 0; return; }
                setup.Refresh(); setup.confirm.onClick.Invoke(); phase = 2; due = Time.unscaledTime+3; return;
            }
            if (phase == 2)
            {
                if (!StockManager.Instance.SetupConfirmed || !StockManager.Instance.EconomyInitialized) { return; }
                tablet = Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).First(t => !t.second);
                Check(GameLocale.Text(DailyIncomeController.Instance.CompanyName) == "안전 수익 회사","player company name preserved");
                Check(Time.timeScale == 1,"no simulation speedup");
                language = 0; view = 0; phase = 3; return;
            }
            if (phase == 3)
            {
                GameLocale.SetLanguage(Languages[language]); phase = 4; due = Time.unscaledTime+.8f; return;
            }
            if (phase == 4)
            {
                if (GameLocale.Code != Languages[language]) { return; }
                Check(GameLocale.Text("안전 수익 회사") == "안전 수익 회사","literal protected after switch: "+GameLocale.Code);
                ValidateChat(); OpenView(); phase = 5; due = Time.unscaledTime+1; return;
            }
            if (phase == 5)
            {
                if (DeskViewController.Instance.IsTransitioning) { return; }
                string name = GameLocale.Code+"-"+Views[view];
                Capture(name,1600,900); Capture(name,1920,1080); Dump(name);
                if (++view < Views.Length) { OpenView(); due = Time.unscaledTime+.8f; return; }
                ExportChat();
                if (++language < Languages.Length) { view = 0; phase = 3; return; }
                PhoneCallController.Instance.Lower(); DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                tablet.ShowPage(TabletAccountUI.Page.Events);
                var reader = tablet.events.newsReader;
                var scroll = (ScrollRect)typeof(NewsDeskUI).GetField("articleScroll",Private).GetValue(reader);
                scroll.verticalNormalizedPosition = .4f; articlePosition = scroll.verticalNormalizedPosition;
                articleId = reader.SelectedArticle.id; orderInput = tablet.orders.price.text;
                var canvas = tablet.GetComponentInParent<Canvas>();
                canvasPosition = canvas.transform.localPosition; canvasRotation = canvas.transform.localRotation; eventCamera = canvas.worldCamera;
                GameLocale.SetLanguage("en"); phase = 6; due = Time.unscaledTime+1.5f;
            }
            else if (phase == 6)
            {
                var reader = tablet.events.newsReader;
                var scroll = (ScrollRect)typeof(NewsDeskUI).GetField("articleScroll",Private).GetValue(reader);
                Check(reader.SelectedArticle.id == articleId,"article selection survives language change");
                Check(Mathf.Abs(scroll.verticalNormalizedPosition-articlePosition)<.01f,"article scroll survives language change");
                Check(tablet.orders.price.text == orderInput,"order input survives language change");
                var canvas = tablet.GetComponentInParent<Canvas>();
                Check(canvas.transform.localPosition == canvasPosition && canvas.transform.localRotation == canvasRotation && canvas.worldCamera == eventCamera,"Canvas local pose and Event Camera unchanged during existing camera animation");
                string oldQuery = (string)typeof(NewsDeskUI).GetField("query",Private).GetValue(reader);
                typeof(NewsDeskUI).GetField("query",Private).SetValue(reader,GameLocale.Text(reader.SelectedArticle.title));
                Check((bool)typeof(NewsDeskUI).GetMethod("MatchesQuery",Private).Invoke(reader,new object[] { reader.SelectedArticle }),"localized headline search matches original article");
                typeof(NewsDeskUI).GetField("query",Private).SetValue(reader,oldQuery);
                Check(PlayerPrefs.GetString("DonDon.Language") == "en","language preference persisted separately from world save");
                Check(!File.Exists(Path.Combine(folder,"errors.txt")),"no Unity runtime errors"); Finish(null);
            }
        }
        catch (Exception e) { Finish(e.ToString()); }
    }
    private static void ValidateChat()
    {
        decimal cash = tablet.account.cash, reserved = tablet.account.ReservedCash;
        var social = new SocialMessage { localizedPhrase = "{stock} {return}인데 아직 안 팔았음. {gain} 찍힘 ㅋㅋ", localizedSuffix = "", localizedValues = new[] {
            new SocialSpeechValue { name = "stock", value = "대만디스플레이", raw = "대만디스플레이" },
            new SocialSpeechValue { name = "return", value = "+50퍼", raw = "50" },
            new SocialSpeechValue { name = "gain", value = "500장", raw = "5284003" } } };
        // The chosen phrase itself is looked up from the actual catalogue below; no simulated speech is injected.
        var source = Resources.Load<TextAsset>("Localization/SourceIndex");
        var entry = JsonUtility.FromJson<GameLocale.SourceCatalog>(source.text).entries.First(e => e.source.Contains("{gain}") && e.source.Contains("{symbol}") && !e.source.Contains("{positionState}"));
        social.localizedPhrase = entry.source;
        social.localizedValues = social.localizedValues.Concat(new[] { new SocialSpeechValue { name = "symbol",value = "DDP" },new SocialSpeechValue { name = "profitKind",value = "평가" } }).ToArray();
        string before = JsonUtility.ToJson(social), state = JsonUtility.ToJson(UnityEngine.Random.state);
        string rendered = LocalizedChat.Text(social,"한국어 원문");
        Check(before == JsonUtility.ToJson(social) && state == JsonUtility.ToJson(UnityEngine.Random.state),"render preserves facts and RNG: "+GameLocale.Code);
        if (GameLocale.Code == "ko") { Check(rendered == "한국어 원문","Korean source unchanged"); }
        else
        {
            Check(GameLocale.Text("장 마감 회사 수익 +250,000원").Contains("250,000"),"currency translation preserves every digit");
            Check(!rendered.Contains("5284003") && !rendered.Contains("5,284,003"),"community profit is approximate: "+GameLocale.Code);
            Check(!rendered.Contains("{gain}") && !rendered.Contains("{stock}"),"snapshot slots resolved: "+GameLocale.Code);
            Check(rendered.Contains(GameLocale.Code == "en" ? "5m" : "500万"),"KRW magnitude preserved: "+GameLocale.Code);
        }
        File.AppendAllText(Path.Combine(folder,"snapshot-test.txt"),GameLocale.Code+": "+rendered+"\n");
        Check(tablet.account.cash == cash && tablet.account.ReservedCash == reserved,"translation does not change account or reservations");
        var stock = StockManager.Instance.stocks.First(s => s.symbol == "DDP");
        string displayName = GameLocale.Text(stock.stockName);
        var mentions = new ChatSocialMentions(Array.Empty<string>(),new[] { new KeyValuePair<string,string>(displayName,stock.symbol) });
        string link = mentions.Format(displayName+(GameLocale.Code == "ja" ? "はどう？" : "?"));
        Check(link.Contains("<link=\"stock:") && ChatSocialMentions.TryStockSymbol("stock:"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stock.symbol)),out string symbol) && symbol == stock.symbol,"translated stock mention retains selection identity: "+GameLocale.Code);
        if (GameLocale.Code == "ja")
        {
            Check(mentions.Format("今日の"+displayName+"はどう？").Contains("<link="),"Japanese stock links work without spaces");
            Check(!mentions.Format(displayName+"HD").Contains("<link="),"stock link does not match longer ticker name");
        }
    }
    private static void OpenView()
    {
        PhoneCallController.Instance?.Lower();
        var statement = tablet.dashboard.GetComponent<DailyIncomeUI>();
        typeof(DailyIncomeUI).GetMethod("Close",Private)?.Invoke(statement,null);
        var desk = DeskViewController.Instance;
        if (view < 3) { desk.Focus(view == 0 ? DeskViewTarget.Chart : view == 1 ? DeskViewTarget.Stocks : DeskViewTarget.Chat); return; }
        desk.Focus(DeskViewTarget.TabletOne);
        tablet.ShowPage(view == 4 ? TabletAccountUI.Page.Orders : view == 5 ? TabletAccountUI.Page.Events : TabletAccountUI.Page.Dashboard);
        if (view == 6) { typeof(DailyIncomeUI).GetMethod("Open",Private).Invoke(statement,null); }
        if (view == 7) { desk.ExitView(true); PhoneCallController.Instance.Ring(PhoneCallController.Caller.Research); PhoneCallController.Instance.Raise(); }
    }
    private static void ExportChat()
    {
        var market = MarketDayController.Instance;
        for (int i = Math.Max(0,market.MessageCount-25); i < market.MessageCount; i++)
        {
            var message = market.GetMessage(i);
            File.AppendAllText(Path.Combine(folder,"actual-chat-"+GameLocale.Code+".txt"),message.Text+"\n=> "+LocalizedChat.Text(message)+"\n\n");
        }
    }
    private static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent)+"/"+value.name;
    private static void Dump(string name)
    {
        var rows = new Rows(); Canvas.ForceUpdateCanvases();
        foreach (var label in Object.FindObjectsByType<TMP_Text>())
        {
            if (!label.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(label.text)) { continue; }
            label.ForceMeshUpdate();
            rows.rows.Add(new Row { path = PathOf(label.transform),source = label.GetComponent<GameLocalizedText>()?.Source,
                text = label.text, overflow = label.isTextOverflowing,truncated = label.isTextTruncated,width = label.rectTransform.rect.width,height = label.rectTransform.rect.height,font = label.fontSize });
        }
        File.WriteAllText(Path.Combine(folder,name+"-text.json"),JsonUtility.ToJson(rows,true));
        if (name.EndsWith("-orders"))
        {
            var important = tablet.orders.panel.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text));
            Check(important.All(t => !t.isTextOverflowing && !t.isTextTruncated),"order controls fit without truncation: "+GameLocale.Code);
        }
    }
    private static void Capture(string name,int width,int height)
    {
        var overlays = new List<(Canvas canvas,Camera camera,float distance)>();
        var target = new RenderTexture(width,height,24);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; Texture2D pixels = null;
        try
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane+.1f;
            }
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            if (pixels != null) { Object.Destroy(pixels); }
            target.Release(); Object.Destroy(target);
            foreach (var saved in overlays)
            { saved.canvas.renderMode = RenderMode.ScreenSpaceOverlay; saved.canvas.worldCamera = saved.camera; saved.canvas.planeDistance = saved.distance; }
        }
    }
    private static void Finish(string failure)
    {
        if (finished) { return; }
        finished = true;
        File.WriteAllText(Path.Combine(folder,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (previousExists) { PlayerPrefs.SetString("DonDon.Language",previousLanguage); } else { PlayerPrefs.DeleteKey("DonDon.Language"); }
        PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Monitor; Application.logMessageReceived -= Log;
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class DonDonLocalizationProbe : MonoBehaviour
{
    private void Awake() { Application.runInBackground = true; Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0; }
    private void LateUpdate() { DonDonLocalizationValidation.Step(); }
}
