# Market color theme — 신규 코드 및 원본 비교

함수별 변경 의도와 테스트 결과는 market-color-theme.md에 있다.

## 신규 Assets/Scripts/Settings/MarketColors.cs
```csharp
using System;
using UnityEngine;

/// <summary>Display-only market palette. Independent of language, trading rules and risk/heat colors.</summary>
public static class MarketColors
{
    public const int RedBlue = 0, GreenRed = 1;
    private static readonly Color Red = new Color32(239,89,100,255), Blue = new Color32(76,150,232,255), Green = new Color32(55,201,149,255);
    private static int applied = -1;
    public static int Version { get; private set; }
    public static event Action Changed;
    public static Color Up => Preview(GameSettings.Current.marketColors,true);
    public static Color Down => Preview(GameSettings.Current.marketColors,false);
    public static Color Preview(int theme,bool up) => theme==GreenRed ? up ? Green : Red : up ? Red : Blue;
    public static Color ForChange(decimal value,Color neutral) => value>0 ? Up : value<0 ? Down : neutral;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { applied=-1; Version=0; Changed=null; }
    public static void Apply()
    {
        int selected=GameSettings.Current.marketColors;
        if (applied==selected)
        { return; }
        applied=selected; Version++; Changed?.Invoke();
    }
}

```

## 신규 Assets/Editor/MarketColorsValidation.cs
```csharp
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
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName==null)
                { return; }
                Check(GameSettings.Current.marketColors==0,"old settings migrate to red/blue");
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke(); Wait(1); return;
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

```

## Assets/Scripts/CandleDrawManager.cs
```diff
--- 원본/Assets/Scripts/CandleDrawManager.cs
+++ 수정/Assets/Scripts/CandleDrawManager.cs
@@ -34,16 +34,43 @@
     private readonly List<int> upTriangles = new();
     private readonly List<int> downTriangles = new();
     private readonly List<int> volTriangles = new();
     private readonly List<int> downTrianglesOffset = new();
     private readonly List<int> volTrianglesOffset = new();
     private readonly List<int> triangles = new();
     private readonly List<Color> colors = new();
     private readonly Material[] _materials = new Material[3];
+    private MaterialPropertyBlock marketBlock;
+    private int materialColorVersion=-1;
+    private static readonly int TintColor=Shader.PropertyToID("_Color"), BaseColor=Shader.PropertyToID("_BaseColor");
+    void OnEnable() { MarketColors.Changed += RefreshMarketColors; RefreshMarketColors(); }
+    void OnDisable() { MarketColors.Changed -= RefreshMarketColors; }
+    private void RefreshMarketColors()
+    {
+        upColor=MarketColors.Up; downColor=MarketColors.Down;
+        if (mesh==null)
+        { return; }
+        if (materialUp!=null && materialDown!=null) { ApplyMaterialColors(); }
+        else { GenerateMesh(); }
+    }
+    private void ApplyMaterialColors()
+    {
+        if (meshRenderer==null || mesh.subMeshCount<2 || materialColorVersion==MarketColors.Version)
+        { return; }
+        marketBlock ??= new MaterialPropertyBlock();
+        for (int i=0;i<2;i++)
+        {
+            meshRenderer.GetPropertyBlock(marketBlock,i);
+            Color tint=i==0 ? MarketColors.Up : MarketColors.Down;
+            marketBlock.SetColor(TintColor,tint); marketBlock.SetColor(BaseColor,tint);
+            meshRenderer.SetPropertyBlock(marketBlock,i); marketBlock.Clear();
+        }
+        materialColorVersion=MarketColors.Version;
+    }
     void OnDestroy()
     {
         if (mesh != null)
         {
             Destroy(mesh);
         }
     }
 
@@ -189,16 +216,17 @@
         mesh.SetTriangles(downTrianglesOffset, 1);
         mesh.SetTriangles(volTrianglesOffset, 2);
         mesh.RecalculateBounds();
         UpdateChartCollider();
         _materials[0] = materialUp;
         _materials[1] = materialDown;
         _materials[2] = materialVolume != null ? materialVolume : materialDown;
         meshRenderer.sharedMaterials = _materials;
+        ApplyMaterialColors();
     }
 
     void AddQuadVertices(List<Vector3> vertices, List<int> triangles, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, ref int vertexIndex)
     {
         vertices.Add(bl);
         vertices.Add(br);
         vertices.Add(tr);
         vertices.Add(tl);

```

## Assets/Scripts/ChartCandleSelectData.cs
```diff
--- 원본/Assets/Scripts/ChartCandleSelectData.cs
+++ 수정/Assets/Scripts/ChartCandleSelectData.cs
@@ -34,18 +34,20 @@
     public bool HasSelection => SelectedCandle.HasValue && SelectedGlobalIndex.HasValue;
 
     /// <summary>전캔들 종가 (색상: 올랐으면 녹색, 내렸으면 적색). null이면 기준 없음.</summary>
     float? _previousClose;
 
     /// <summary>선택이 바뀌었을 때 (설정/해제 시). (캔들, 전역인덱스) 또는 (null, null)</summary>
     public event Action<CandleData?, int?> OnSelectionChanged;
 
-    static readonly Color Up = new Color(239f / 255f, 89f / 255f, 100f / 255f);
-    static readonly Color Down = new Color(76f / 255f, 150f / 255f, 232f / 255f);
+    private static Color Up => MarketColors.Up;
+    private static Color Down => MarketColors.Down;
+    void OnEnable() { MarketColors.Changed += RefreshTexts; RefreshTexts(); }
+    void OnDisable() { MarketColors.Changed -= RefreshTexts; }
 
     /// <summary>해당 캔들로 선택 설정. previousClose: 전캔들 종가(전캔들 대비 색상용). 캔들 값이 바뀔 때마다 ChartManager가 다시 호출하면 TMP가 이벤트성 Refresh됨.</summary>
     public void SetSelection(CandleData candle, int globalIndex, float? previousClose = null)
     {
         SelectedCandle = candle;
         SelectedGlobalIndex = globalIndex;
         _previousClose = previousClose;
         RefreshTexts();

```

## Assets/Scripts/ChartManager.cs
```diff
--- 원본/Assets/Scripts/ChartManager.cs
+++ 수정/Assets/Scripts/ChartManager.cs
@@ -188,30 +188,37 @@
         text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
         text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
         text.raycastTarget = false;
         text.color = muted ? new Color32(138,150,163,255) : new Color32(230,235,239,255);
     }
 
     void OnEnable()
     {
+        MarketColors.Changed += RefreshMarketColors;
         StockManager.OnStockSelected += SetStock;
         StockManager.OnStockDelisted += OnStockDelisted;
         StockManager.OnStockListed += OnStockListed;
     }
 
     void OnDisable()
     {
+        MarketColors.Changed -= RefreshMarketColors;
         ReleaseChartPointer();
         if (_guides != null) { _guides.HideCursor(); }
         StockManager.OnStockSelected -= SetStock;
         StockManager.OnStockDelisted -= OnStockDelisted;
         StockManager.OnStockListed -= OnStockListed;
     }
 
+    private void RefreshMarketColors()
+    {
+        RefreshTradeBasedValues(); RefreshStatistics(); RefreshTradeHistory();
+        if (_guides != null && currentStock != null) { _guides.Refresh(currentStock); }
+    }
     void OnStockListed(Stock stock)
     {
         stock.OnTradeExecutedWithStock -= OnAnyStockTraded;
         stock.OnTradeExecutedWithStock += OnAnyStockTraded;
         if (currentStock == null || currentStock == stock)
         {
             SetStock(StockManager.Instance.CurrentStock);
         }
@@ -441,17 +448,17 @@
         if (textLastPrice != null)
         {
             GameLocalizedText.Set(textLastPrice,PriceTickPolicy.FormatPrice(currentStock.currentPrice));
         }
 
         decimal refPrice = GetReferencePriceForChangePct();
         decimal change = refPrice > 0m ? currentStock.currentPrice - refPrice : 0m;
         decimal pct = refPrice > 0m ? change / refPrice * 100m : 0m;
-        Color changeColor = change > 0m ? new Color32(239, 89, 100, 255) : change < 0m ? new Color32(76, 150, 232, 255) : new Color32(138, 150, 163, 255);
+        Color changeColor = change > 0m ? MarketColors.Up : change < 0m ? MarketColors.Down : new Color32(138, 150, 163, 255);
         if (textChangePct != null)
         {
             GameLocalizedText.Set(textChangePct,refPrice > 0m ? (pct > 0m ? $"+{pct:N2}%" : $"{pct:N2}%") : "—");
             textChangePct.color = changeColor;
         }
         if (textChangeAmount != null)
         {
             string value = FormatDailyChange(currentStock.currentPrice, refPrice);
@@ -540,36 +547,36 @@
         {
             GameLocalizedText.Set(textDayHigh,PriceTickPolicy.FormatPrice(stats.dayHigh));
         }
 
         if (textDayHighPct != null && stats.prevDayClose > 0m)
         {
             decimal pct = (stats.dayHigh - stats.prevDayClose) / stats.prevDayClose * 100m;
             GameLocalizedText.Set(textDayHighPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
-            textDayHighPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
+            textDayHighPct.color = pct >= 0m ? MarketColors.Up : MarketColors.Down;
         }
 
         if (textDayLow != null)
         {
             GameLocalizedText.Set(textDayLow,PriceTickPolicy.FormatPrice(stats.dayLow));
         }
 
         if (textDayLowPct != null && stats.prevDayClose > 0m)
         {
             decimal pct = (stats.dayLow - stats.prevDayClose) / stats.prevDayClose * 100m;
             GameLocalizedText.Set(textDayLowPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
-            textDayLowPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
+            textDayLowPct.color = pct >= 0m ? MarketColors.Up : MarketColors.Down;
         }
 
         if (textTradeStrength != null)
         {
             float strength = StockStatistics.Instance.GetTradeStrength(currentStock);
             GameLocalizedText.Set(textTradeStrength,strength >= 999f ? "999+" : $"{strength:N1}%");
-            textTradeStrength.color = strength >= 100f ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
+            textTradeStrength.color = strength >= 100f ? MarketColors.Up : MarketColors.Down;
         }
     }
 
     void InitializeTradeHistoryItems()
     {
         if (tradeHistoryContainer == null || tradeHistoryItemPrefab == null)
         { return; }
 

```

## Assets/Scripts/ChartUI/ChartGuideOverlay.cs
```diff
--- 원본/Assets/Scripts/ChartUI/ChartGuideOverlay.cs
+++ 수정/Assets/Scripts/ChartUI/ChartGuideOverlay.cs
@@ -119,18 +119,21 @@
             GameLocalizedText.Set(ticks[i],scale.Tick(i).ToString("N0", Numbers));
             float y = plotHeight * i / scale.Intervals;
             Place(ticks[i].rectTransform, plotWidth + 8f, Mathf.Clamp(y - LabelHeight * .5f, 0f, plotHeight - LabelHeight), axisWidth - 14f, LabelHeight);
         }
         lastMinimum = scale.Minimum; lastMaximum = scale.Maximum; lastStep = scale.Step;
         RefreshTrades(stock, true);
     }
 
+    private int marketColorVersion=-1;
     private void RefreshTrades(Stock stock, bool geometryChanged)
     {
+        geometryChanged |= marketColorVersion!=MarketColors.Version;
+        marketColorVersion=MarketColors.Version;
         bool stockChanged = stock != tradeStock;
         if (!stockChanged && !geometryChanged && Time.unscaledTime < nextTradeRead)
         { return; }
         bool dataChanged = stockChanged;
         if (stockChanged || Time.unscaledTime >= nextTradeRead)
         {
             tradeStock = stock; nextTradeRead = Time.unscaledTime + .25f;
             var accounts = PlayerAccounts.Instance;
@@ -214,17 +217,17 @@
         { return averageA ? -1 : 1; }
         decimal current = tradeStock != null ? tradeStock.currentPrice : 0m;
         int distance = Math.Abs(a.Price - current).CompareTo(Math.Abs(b.Price - current));
         return distance != 0 ? distance : ChartTradeLevels.ComparePrice(a, b);
     }
     private static bool SameLevel(ChartTradeLevels.Level a, ChartTradeLevels.Level b)
     { return a.Account == b.Account && a.Type == b.Type && a.Price == b.Price && a.Quantity == b.Quantity; }
     private static Color TradeColor(ChartTradeLevels.Kind kind)
-    { return kind == ChartTradeLevels.Kind.Average ? new Color32(73, 195, 201, 255) : kind == ChartTradeLevels.Kind.Buy ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255); }
+    { return kind == ChartTradeLevels.Kind.Average ? new Color32(73, 195, 201, 255) : kind == ChartTradeLevels.Kind.Buy ? MarketColors.Up : MarketColors.Down; }
     private void EnsureTradeLabels(int count)
     {
         while (tradeBadges.Count < count)
         {
             var badge = Badge("Panel :: My Price " + tradeBadges.Count, tradeRoot);
             badge.GetComponent<Image>().color = new Color32(16, 23, 28, 240);
             var label = Label("Text :: My Price", badge, tradeFont, Muted);
             label.fontSize = 22f; label.alignment = TextAlignmentOptions.MidlineLeft;

```

## Assets/Scripts/ChartUI/TradeHistoryItem.cs
```diff
--- 원본/Assets/Scripts/ChartUI/TradeHistoryItem.cs
+++ 수정/Assets/Scripts/ChartUI/TradeHistoryItem.cs
@@ -7,25 +7,31 @@
 public class TradeHistoryItem : MonoBehaviour
 {
     [Tooltip("체결가")]
     public TextMeshProUGUI txtPrice;
     [Tooltip("체결량")]
     public TextMeshProUGUI txtVolume;
 
     private bool _isBuyInitiated;
+    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
+    void OnDisable() { MarketColors.Changed -= RefreshColors; }
+    private void RefreshColors()
+    {
+        if (txtPrice!=null) { txtPrice.color=_isBuyInitiated ? MarketColors.Up : MarketColors.Down; }
+    }
 
     public void Set(decimal price, decimal volume, bool isBuyInitiated)
     {
         _isBuyInitiated = isBuyInitiated;
 
         if (txtPrice != null)
         {
             GameLocalizedText.Set(txtPrice,PriceTickPolicy.FormatPrice(price));
-            txtPrice.color = isBuyInitiated ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.3f, 0.3f);
+            RefreshColors();
         }
 
         if (txtVolume != null)
         {
             decimal totalAmount = price * volume;
             GameLocalizedText.Set(txtVolume,totalAmount.ToString("N0"));
         }
     }

```

## Assets/Scripts/Hold/HoldListItem.cs
```diff
--- 원본/Assets/Scripts/Hold/HoldListItem.cs
+++ 수정/Assets/Scripts/Hold/HoldListItem.cs
@@ -11,38 +11,48 @@
     public TextMeshProUGUI textSymbol;
     public TextMeshProUGUI textName;
     public TextMeshProUGUI textProfitPct;
     public TextMeshProUGUI textEvaluationProfit;
     public TextMeshProUGUI textAvgPrice;
     public TextMeshProUGUI textQuantity;
     public TextMeshProUGUI textCurrentPrice, textCurrentValue, textAvailability, textEmpty;
     public bool currencySuffix;
-    private static readonly Color Up = new Color32(239,89,100,255), Down = new Color32(76,150,232,255), Neutral = new Color32(166,177,186,255);
+    private static readonly Color Neutral = new Color32(166,177,186,255);
+    private decimal shownProfit;
+    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
+    void OnDisable() { MarketColors.Changed -= RefreshColors; }
+    private void RefreshColors()
+    {
+        Color color=MarketColors.ForChange(shownProfit,Neutral);
+        if (textProfitPct!=null) { textProfitPct.color=color; }
+        if (textEvaluationProfit!=null) { textEvaluationProfit.color=color; }
+    }
 
     static readonly NumberFormatInfo _nfi;
 
     static HoldListItem()
     {
         _nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
         _nfi.NumberGroupSizes = new[] { 3 };
         _nfi.NumberGroupSeparator = ",";
         _nfi.NumberDecimalSeparator = ".";
     }
 
     /// <summary>수익률(%): (현재가-평단)/평단*100. 평가손익: (현재가-평단)*수량. 평균매가는 원 단위로 표시. 보유수량 정수.</summary>
     public void Set(string symbol, string name, decimal profitPct, decimal evaluationProfitLoss, decimal avgPrice, decimal quantity)
     {
+        shownProfit=evaluationProfitLoss;
         DeviceTerminalUI.Set(textSymbol,symbol ?? ""); DeviceTerminalUI.Set(textName,name ?? "");
         DeviceTerminalUI.Set(textProfitPct,profitPct.ToString("+#,##0.0;-#,##0.0;0.0",_nfi) + "%");
         string unit = currencySuffix ? "원" : "";
         DeviceTerminalUI.Set(textEvaluationProfit,(evaluationProfitLoss > 0m ? "+" : "") + PriceTickPolicy.FormatMoney(evaluationProfitLoss) + unit);
         DeviceTerminalUI.Set(textAvgPrice,FormatAvgPrice(avgPrice) + unit);
         DeviceTerminalUI.Set(textQuantity,decimal.Truncate(quantity).ToString("N0",_nfi) + (currencySuffix ? "주" : ""));
-        Color color = evaluationProfitLoss > 0m ? Up : evaluationProfitLoss < 0m ? Down : Neutral;
+        Color color = MarketColors.ForChange(evaluationProfitLoss,Neutral);
         if (textProfitPct != null && textProfitPct.color != color) { textProfitPct.color = color; }
         if (textEvaluationProfit != null && textEvaluationProfit.color != color) { textEvaluationProfit.color = color; }
     }
 
     public void SetFromAccount(Stock stock,Trader account)
     {
         decimal quantity = stock == null || account == null ? 0m : account.GetOwnedVolume(stock);
         decimal average = stock == null || account == null ? 0m : account.GetOwnedAverage(stock);

```

## Assets/Scripts/NightShift/DeviceTerminalUI.cs
```diff
--- 원본/Assets/Scripts/NightShift/DeviceTerminalUI.cs
+++ 수정/Assets/Scripts/NightShift/DeviceTerminalUI.cs
@@ -43,17 +43,17 @@
     private DeviceMarketJournal.Entry selectedEvent;
     private System.Collections.IEnumerator focusing;
     private GameObject quickQuantityPanel;
     private TextMeshProUGUI reviewLabel;
     private Button[] quickButtons;
     private LeadingRoomUI leadingRooms;
     private PendingOrderListUI pendingOrders;
     private readonly StringBuilder text = new(1024);
-    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(166, 177, 186, 255), Up = new Color32(239, 89, 100, 255), Down = new Color32(76, 150, 232, 255), Active = new Color32(23, 55, 62, 255), Neutral = new Color32(23, 33, 38, 255);
+    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(166, 177, 186, 255), Active = new Color32(23, 55, 62, 255), Neutral = new Color32(23, 33, 38, 255);
     public static void Set(TextMeshProUGUI target, string value)
     {
         if (target != null && !GameLocalizedText.Matches(target,value))
         { GameLocalizedText.Set(target,value); }
     }
     public static string Money(decimal value) => PriceTickPolicy.FormatMoney(value) + (GameLocale.Code=="ko" ? "원" : " "+GameLocale.Text("원"));
     void Start()
     {
@@ -313,17 +313,17 @@
             decimal previous = StockStatistics.Instance == null ? selected.currentPrice : StockStatistics.Instance.GetStats(selected).prevDayClose;
             tradeView.Header(selected,previous); return;
         }
         Set(stockName, selected.stockName); Set(ticker, selected.symbol + " · 현재 선택 종목");
         Set(currentPrice, Money(selected.currentPrice));
         decimal reference = StockStatistics.Instance == null ? selected.currentPrice : StockStatistics.Instance.GetStats(selected).prevDayClose;
         decimal delta = selected.currentPrice - reference;
         Set(change, (delta >= 0m ? "+" : "") + Money(delta) + "  (" + (reference > 0m ? (delta / reference * 100m).ToString("+0.0;-0.0;0.0") : "0.0") + "%)");
-        change.color = delta >= 0m ? Up : Down;
+        change.color = MarketColors.ForChange(delta,Ink);
         graph.color = change.color; graph.SetStock(selected);
         Set(graphRange, "최근 40개 캔들 · 전일 " + Money(reference));
     }
     private void Order()
     {
         tradeView.Account(selected);
         pendingOrders.Refresh(); cancel.interactable = pendingOrders.Count > 0;
         draftPanel.SetActive(!confirming);

```

## Assets/Scripts/NightShift/InvestmentDashboard.cs
```diff
--- 원본/Assets/Scripts/NightShift/InvestmentDashboard.cs
+++ 수정/Assets/Scripts/NightShift/InvestmentDashboard.cs
@@ -21,17 +21,17 @@
     private Stock selectedUnlisted, lastGlobal;
     private readonly List<Stock> owned = new(), ordered = new();
     private readonly List<InvestmentPositionRow> pool = new();
     private float next;
     private bool sorted;
     public static readonly Color Ink = new Color32(230,235,239,255);
     public static string Signed(decimal value) => value.ToString("+#,##0;-#,##0;0");
     public static string Rate(decimal pnl, decimal basis) => basis > 0m ? (pnl / basis * 100m).ToString("+0.00;-0.00;0.00") + "%" : "—";
-    public static Color ProfitColor(decimal pnl) => pnl > 0m ? new Color32(239,89,100,255) : pnl < 0m ? new Color32(76,150,232,255) : Ink;
+    public static Color ProfitColor(decimal pnl) => pnl > 0m ? MarketColors.Up : pnl < 0m ? MarketColors.Down : Ink;
     void Start()
     { sort.onClick.AddListener(CycleSort); detail.onClick.AddListener(() => navigation.ShowPage(TabletAccountUI.Page.Detail)); }
     void OnEnable()
     {
         next = 0f;
         StockManager.OnStockSelected += FollowSelection;
         var current = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
         if (lastGlobal != current) { FollowSelection(current); }

```

## Assets/Scripts/NightShift/PendingOrderListUI.cs
```diff
--- 원본/Assets/Scripts/NightShift/PendingOrderListUI.cs
+++ 수정/Assets/Scripts/NightShift/PendingOrderListUI.cs
@@ -49,17 +49,17 @@
         if(visibleCount!=entries.Count)
         {
             visibleCount=entries.Count; content.sizeDelta=new Vector2(0,visibleCount*RowHeight*scale);
             for(int i=0;i<rows.Count;i++) { rows[i].Root.gameObject.SetActive(i<visibleCount); }
         }
         for(int i=0;i<entries.Count;i++)
         {
             var entry=entries[i]; var row=rows[i]; row.Entry=entry;
-            DeviceTerminalUI.Set(row.Side,entry.Buy?"매수":"매도"); row.Side.color=entry.Buy?new Color32(239,89,100,255):new Color32(76,150,232,255);
+            DeviceTerminalUI.Set(row.Side,entry.Buy?"매수":"매도"); row.Side.color=entry.Buy?MarketColors.Up:MarketColors.Down;
             DeviceTerminalUI.Set(row.Name,entry.Stock.stockName);
             DeviceTerminalUI.Set(row.Symbol,entry.Request!=null ? entry.Request.AcceptedAt ?? "—" : entry.Stock.symbol);
             DeviceTerminalUI.Set(row.Price,TradingOrderView.Number(entry.Price));
             DeviceTerminalUI.Set(row.Remaining,TradingOrderView.Number(entry.Remaining));
             DeviceTerminalUI.Set(row.Filled,entry.Request==null?"—":TradingOrderView.Number(entry.Request.Filled));
         }
     }
     public void CancelAll()

```

## Assets/Scripts/NightShift/SpreadTradingUI.cs
```diff
--- 원본/Assets/Scripts/NightShift/SpreadTradingUI.cs
+++ 수정/Assets/Scripts/NightShift/SpreadTradingUI.cs
@@ -171,17 +171,17 @@
         DeviceTerminalUI.Set(bid,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadBid : buyPrice) : "—");
         DeviceTerminalUI.Set(ask,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadAsk : sellPrice) : "—");
         DeviceTerminalUI.Set(cash,TradingOrderView.Number(operation.SpreadAvailableCash));
         DeviceTerminalUI.Set(shares,TradingOrderView.Number(operation.SpreadAvailableShares(stock)));
         DeviceTerminalUI.Set(buyRemaining,TradingOrderView.Number(operation.SpreadBuyRemaining));
         DeviceTerminalUI.Set(sellRemaining,TradingOrderView.Number(operation.SpreadSellRemaining));
         DeviceTerminalUI.Set(bought,TradingOrderView.Number(operation.Bought)); DeviceTerminalUI.Set(sold,TradingOrderView.Number(operation.Sold));
         DeviceTerminalUI.Set(realized,InvestmentDashboard.Signed(operation.SpreadRealized));
-        realized.color = operation.SpreadRealized > 0m ? new Color32(239,89,100,255) : operation.SpreadRealized < 0m ? new Color32(76,150,232,255) : Ink;
+        realized.color = operation.SpreadRealized > 0m ? MarketColors.Up : operation.SpreadRealized < 0m ? MarketColors.Down : Ink;
         previous.interactable=following.interactable=!operation.Active;
         DeviceTerminalUI.Set(elapsed,operation.SpreadStartedGameMinute>0 ? "실행 경과 · "+DeviceMarketJournal.Duration(operation.SpreadElapsedGameMinutes) : "");
         DeviceTerminalUI.Set(updated,operation.SpreadUpdatedStamp==null ? "" : "갱신 · "+operation.SpreadUpdatedStamp);
         var buffer=new System.Text.StringBuilder();
         int shown=0;
         foreach (var fill in operation.SpreadFills)
         {
             if (++shown>3)

```

## Assets/Scripts/NightShift/TabletKitStyle.cs
```diff
--- 원본/Assets/Scripts/NightShift/TabletKitStyle.cs
+++ 수정/Assets/Scripts/NightShift/TabletKitStyle.cs
@@ -17,18 +17,18 @@
     private static Color Parse(string value) { ColorUtility.TryParseHtmlString(value,out var color); return color; }
     public static Color Background => Parse(Palette.background);
     public static Color Panel => Parse(Palette.panel);
     public static Color Hover => Parse(Palette.panelHover);
     public static Color Line => Parse(Palette.divider);
     public static Color Ink => Parse(Palette.textPrimary);
     public static Color Muted => Parse(Palette.textSecondary);
     public static Color Accent => Parse(Palette.accent);
-    public static Color Up => Parse(Palette.profitKR);
-    public static Color Down => Parse(Palette.lossKR);
+    public static Color Up => MarketColors.Up;
+    public static Color Down => MarketColors.Down;
     public static Sprite Icon(string name)
     {
         if (icons.TryGetValue(name,out var sprite))
         { return sprite; }
         int index = Array.IndexOf(names,name);
         var texture = Resources.Load<Texture2D>("UI/TabletKit/Icons");
         if (index < 0 || texture == null)
         { return null; }

```

## Assets/Scripts/NightShift/TradingOrderView.cs
```diff
--- 원본/Assets/Scripts/NightShift/TradingOrderView.cs
+++ 수정/Assets/Scripts/NightShift/TradingOrderView.cs
@@ -13,17 +13,17 @@
     private TextMeshProUGUI available, reserved, owned, sellable, average, profit, afterCash, afterShares;
     private TextMeshProUGUI[] confirmationValues;
     private TextMeshProUGUI reviewCaption, submitCaption;
     private Image buyLine, sellLine;
     private Button current;
     public const float ControlHeight = 56f;
     private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
     private static readonly Color Background = new Color32(13,23,29,255), Line = new Color32(44,60,69,255), Selected = new Color32(32,49,57,255);
-    private static readonly Color Up = new Color32(239,89,100,255), Down = new Color32(76,150,232,255);
+    private static readonly Color Error = new Color32(239,89,100,255);
     private static TMP_FontAsset numerals;
     public static TMP_FontAsset Numerals => numerals ??= Resources.Load<TMP_FontAsset>("UI/TabletControls/Trading Numerals");
     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
     private static void ResetFont() { numerals = null; }
 
     public void Configure(DeviceTerminalUI terminal, GameObject pending, GameObject quick, Button[] quickButtons)
     {
         ui = terminal; reading = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
@@ -82,33 +82,33 @@
         Place((RectTransform)ui.split.transform,draft,0,360,128,ControlHeight,736,704); Action(ui.split,"분할 OFF");
         SplitInput(ui.parts,"횟수","회",144,296);
         SplitInput(ui.interval,"간격","초",448,600);
         Rule(draft,0,436,736,736,704);
         Label("Order Total",draft,"예상 금액",0,452,280,56,28,736,704).color = Muted;
         Place(ui.amount.rectTransform,draft,288,452,376,56,736,704); TextStyle(ui.amount,36,true); ui.amount.alignment = TextAlignmentOptions.MidlineRight;
         Label("Order Total Unit",draft,"원",696,452,40,56,26,736,704).color = Muted;
         afterCash = Forecast(draft,"주문 후 현금",516,"원"); afterShares = Forecast(draft,"주문 후 보유",552,"주");
-        Place(ui.validation.rectTransform,draft,0,600,736,32,736,704); TextStyle(ui.validation,25,false); ui.validation.color = Up;
+        Place(ui.validation.rectTransform,draft,0,600,736,32,736,704); TextStyle(ui.validation,25,false); ui.validation.color = Error;
         Place((RectTransform)ui.review.transform,draft,0,648,736,ControlHeight,736,704); Action(ui.review,"매수 주문",true);
 
         Transform confirm = ui.reviewPanel.transform;
         Label("Review Heading",confirm,"주문 확인",0,0,736,56,32,736,704);
         confirmationValues = new TextMeshProUGUI[6];
         string[] captions = {"종목","가격","수량","금액","횟수","간격"};
         string[] units = {"","원","주","원","회","초"};
         for (int i=0;i<captions.Length;i++)
         {
             float y=80+i*72;
             Label("Review " + captions[i],confirm,captions[i],0,y,136,56,28,736,704).color=Muted;
             var value=Label("Review Value " + i,confirm,"",152,y,512,56,30,736,704);
             if(i>0) { Numeric(value); } value.alignment=TextAlignmentOptions.MidlineRight; confirmationValues[i]=value;
             Label("Review Unit " + i,confirm,units[i],696,y,40,56,26,736,704).color=Muted;
         }
-        Place(ui.confirmation.rectTransform,confirm,0,536,736,72,736,704); TextStyle(ui.confirmation,25,false); ui.confirmation.color=Up;
+        Place(ui.confirmation.rectTransform,confirm,0,536,736,72,736,704); TextStyle(ui.confirmation,25,false); ui.confirmation.color=Error;
         Place((RectTransform)ui.back.transform,confirm,0,648,360,ControlHeight,736,704); Action(ui.back,"수정");
         Place((RectTransform)ui.submit.transform,confirm,376,648,360,ControlHeight,736,704); Action(ui.submit,"주문",true);
         ui.reviewPanel.SetActive(false);
     }
     private void SplitInput(TMP_InputField field,string caption,string unit,float labelX,float fieldX)
     {
         Transform root=ui.splitPanel.transform;
         Label("Split " + caption,root,caption,labelX,0,72,56,26,736,56).color=Muted;
@@ -243,17 +243,17 @@
         if(error.Contains("거래할 수"))
         { return "거래 불가"; }
         if(error.Contains("금액"))
         { return "금액 범위 오류"; }
         return "주문 불가";
     }
     public static string Number(decimal value) => value.ToString("N0",CultureInfo.InvariantCulture);
     private static string Signed(decimal value) => value.ToString("+#,##0;−#,##0;0",CultureInfo.InvariantCulture);
-    private static Color SignColor(decimal value) => value>0?Up:value<0?Down:Ink;
+    private static Color SignColor(decimal value) => MarketColors.ForChange(value,Ink);
     private TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,float pw,float ph)
     {
         var label=new GameObject("Text :: "+name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
         Place(label.rectTransform,parent,x,y,w,h,pw,ph); TextStyle(label,size,false); label.alignment=TextAlignmentOptions.MidlineLeft; GameLocalizedText.Set(label,text); return label;
     }
     private static RectTransform Area(string name,Transform parent,float x,float y,float w,float h,float pw,float ph)
     {
         var rect=new GameObject("Area :: "+name,typeof(RectTransform)).GetComponent<RectTransform>();

```

## Assets/Scripts/OrderBookUI/DrawRawOrder.cs
```diff
--- 원본/Assets/Scripts/OrderBookUI/DrawRawOrder.cs
+++ 수정/Assets/Scripts/OrderBookUI/DrawRawOrder.cs
@@ -1,20 +1,35 @@
 using TMPro;
 using UnityEngine;
+using UnityEngine.UI;
 
 public class DrawRawOrder : MonoBehaviour
 {
     public TMP_Text txtPrice;
     public TMP_Text txtVolume;
     public RectTransform volumeGuage;
 
     public GameObject highlight;
 
     private decimal currentPrice;
+    private bool buySide;
+    private Image depth;
+    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
+    void OnDisable() { MarketColors.Changed -= RefreshColors; }
+    public void SetSide(bool buy) { buySide=buy; RefreshColors(); }
+    private void RefreshColors()
+    {
+        // Bid rows remain the lower-price color; asks remain the higher-price color.
+        Color color=buySide ? MarketColors.Down : MarketColors.Up;
+        if (txtPrice!=null) { txtPrice.color=color; }
+        if (txtVolume!=null) { txtVolume.color=color; }
+        if (depth==null && volumeGuage!=null) { depth=volumeGuage.GetComponent<Image>(); }
+        if (depth!=null) { color.a=.55f; depth.color=color; }
+    }
 
     public void SetData(decimal price, decimal volume, decimal maxVolume, decimal lastPrice)
     {
         SetData(price, volume, maxVolume, lastPrice, false);
     }
 
     public void SetData(decimal price, decimal displayValue, decimal maxValue, decimal lastPrice, bool isTurnover)
     {

```

## Assets/Scripts/OrderBookUI/OrderBookUIManager.cs
```diff
--- 원본/Assets/Scripts/OrderBookUI/OrderBookUIManager.cs
+++ 수정/Assets/Scripts/OrderBookUI/OrderBookUIManager.cs
@@ -90,22 +90,22 @@
             GameLocalizedText.Set(toggleButtonText,_displayMode == VolumeDisplayMode.Volume ? "거래량 보기" : "거래대금 보기");
         }
     }
 
     void InitializeRows()
     {
         foreach (Transform child in buyParent)
         {
-            buyRows.Add(child.GetComponent<DrawRawOrder>());
+            var row=child.GetComponent<DrawRawOrder>(); row.SetSide(true); buyRows.Add(row);
         }
 
         foreach (Transform child in sellParent)
         {
-            sellRows.Add(child.GetComponent<DrawRawOrder>());
+            var row=child.GetComponent<DrawRawOrder>(); row.SetSide(false); sellRows.Add(row);
         }
     }
 
     public void SetStock(Stock stock)
     {
         if (currentStock != null)
         {
             currentStock.OnTradeExecuted -= MarkDirty;

```

## Assets/Scripts/Settings/GameSettings.cs
```diff
--- 원본/Assets/Scripts/Settings/GameSettings.cs
+++ 수정/Assets/Scripts/Settings/GameSettings.cs
@@ -3,16 +3,17 @@
 
 /// <summary>Local preferences, independent of a market session. Only implemented controls are exposed.</summary>
 [DefaultExecutionOrder(-19900)]
 public sealed class GameSettings : MonoBehaviour
 {
     [Serializable] public sealed class Values
     {
         public int version = 1, frameRate = 60, quality = 1;
+        public int marketColors;
         public bool vSync, fullscreen = true, muteUnfocused = true;
         public float volume = .8f, sensitivity = 1f, wheel = 3f, menuScale = 1f;
     }
     public const string Preference = "DonDon.Settings.v1";
     public static Values Current { get; private set; } = new();
     public static event Action Changed;
     private bool focused = true;
     private float saveAt = -1;
@@ -28,30 +29,32 @@
         catch (ArgumentException) { Current = new Values(); }
         if (Current.version != 1) { Current = new Values(); }
         Clamp(); Apply();
     }
     private static float Valid(float value,float fallback,float min,float max)
     { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value,min,max); }
     private static void Clamp()
     {
+        if (Current.marketColors!=MarketColors.RedBlue && Current.marketColors!=MarketColors.GreenRed) { Current.marketColors=MarketColors.RedBlue; }
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
+        MarketColors.Apply();
         // Avoid expensive pipeline/OS switches on every slider movement.
         if (QualitySettings.GetQualityLevel() != Current.quality) { QualitySettings.SetQualityLevel(Current.quality,true); }
         QualitySettings.vSyncCount = Current.vSync ? 1 : 0; Application.targetFrameRate = Current.frameRate;
         if (SessionStore.ValidationMode) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 20; }
         if (!Application.isEditor)
         {
             var mode = Current.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
             if (Screen.fullScreenMode != mode) { Screen.fullScreenMode = mode; }

```

## Assets/Scripts/Settings/SystemMenuUI.cs
```diff
--- 원본/Assets/Scripts/Settings/SystemMenuUI.cs
+++ 수정/Assets/Scripts/Settings/SystemMenuUI.cs
@@ -123,25 +123,45 @@
         Choice(body,"다른 창 사용 시 음소거",128,new[]{"끔","켬"},()=>GameSettings.Current.muteUnfocused ? 1 : 0,v=>GameSettings.Current.muteUnfocused=v==1);
         body=categories["게임"]; Header(body,"언어",0);
         Choice(body,"표시 언어",48,new[]{"한국어","English","日本語"},()=>Array.IndexOf(new[]{"ko","en","ja"},GameLocale.Code),v=>GameLocale.SetLanguage(new[]{"ko","en","ja"}[v]),false);
         Header(body,"마우스",130);
         Slider(body,"시점 감도",185,.25f,2.5f,()=>GameSettings.Current.sensitivity,v=>GameSettings.Current.sensitivity=v,v=>v.ToString("0.00")+"×");
         Slider(body,"휠 감도",258,1,6,()=>GameSettings.Current.wheel,v=>GameSettings.Current.wheel=v,v=>v.ToString("0.0"));
         body=categories["UI"]; Header(body,"시스템 메뉴",0);
         Slider(body,"메뉴 크기",55,.9f,1.1f,()=>GameSettings.Current.menuScale,v=>GameSettings.Current.menuScale=v,v=>Mathf.RoundToInt(v*100)+"%");
+        Header(body,"시세 색상",150);
+        MarketColorChoices(body,210);
+    }
+    private void MarketColorChoices(Transform body,float y)
+    {
+        for (int i=0;i<2;i++)
+        {
+            int index=i;
+            var frame=Box(body,"Button :: 상승 / 하락 "+i,Color.white); Place(frame,i*368,y,344,168);
+            var border=frame.GetComponent<Image>(); border.raycastTarget=true;
+            var button=frame.gameObject.AddComponent<Button>(); button.targetGraphic=border;
+            var colors=button.colors; colors.normalColor=colors.selectedColor=Color.white;
+            colors.highlightedColor=new Color(1.3f,1.3f,1.3f,1); colors.pressedColor=new Color(.7f,.7f,.7f,1); colors.fadeDuration=.1f; button.colors=colors;
+            var surface=Box(frame,"Image :: Candle Option Surface",new Color32(19,26,32,255)); Place(surface,2,2,340,164);
+            var preview=Box(frame,"Image :: Candle Theme",Color.white); Place(preview,40,6,264,156);
+            var image=preview.GetComponent<Image>(); image.sprite=Resources.Load<Sprite>("UI/SystemMenu/"+(i==0 ? "CandleThemeRedBlue" : "CandleThemeGreenRed"));
+            image.type=Image.Type.Simple; image.preserveAspect=true; image.raycastTarget=false;
+            button.onClick.AddListener(()=> { GameSettings.Current.marketColors=index; GameSettings.Commit(); });
+            refreshers.Add(()=>border.color=GameSettings.Current.marketColors==index ? new Color(1,1,1,.9f) : new Color(1,1,1,.16f));
+        }
     }
     public void Report(bool saved)
     {
         reportSaved=saved;
         Show("report","장 마감 정산"); var reports=DailyIncomeController.Instance?.Ledger.Reports;
         if (reports==null || reports.Count==0) { return; }
         var report=reports[reports.Count-1]; GameLocalizedText.Set(reportDate,"DAY "+report.day+"  17:00",true);
         GameLocalizedText.Set(reportTotal,report.DayResult.ToString("+#,##0;-#,##0;0")+"원");
-        reportTotal.color=report.DayResult>0 ? new Color(.94f,.36f,.4f) : report.DayResult<0 ? new Color(.3f,.59f,.91f) : Ink;
+        reportTotal.color=MarketColors.ForChange(report.DayResult,Ink);
         decimal[] values={report.Realized,report.company,report.fees,report.Dividends,report.Relief,report.Tax,report.TaxPaid,report.UnpaidTax,report.Assets};
         for (int i=0;i<values.Length;i++) { GameLocalizedText.Set(reportValues[i],values[i].ToString("N0")+"원"); }
         GameLocalizedText.Set(checkpointStatus,saved ? "마감 저장 완료" : "저장 실패 · 이전 마감 유지");
         retrySave.gameObject.SetActive(!saved);
         reportSleep.interactable=PlayerRestController.Instance!=null && PlayerRestController.Instance.CanSleep;
     }
     public void Death() { Show("death","과로로 사망했습니다"); }
     public void ReportCompanies()

```

## Assets/Scripts/StockListUI/StockListItem.cs
```diff
--- 원본/Assets/Scripts/StockListUI/StockListItem.cs
+++ 수정/Assets/Scripts/StockListUI/StockListItem.cs
@@ -32,18 +32,18 @@
     [Header("Trading Terminal Style")]
     public Image rowBackground;
     public Sprite normalBackground, selectedBackground;
     public TextMeshProUGUI textRisk;
     public Image selectionHighlight;
     public Image riskBadgeBackground;
     private bool selected, selectionInitialized;
     private string iconSymbol;
-    private static readonly Color Up = new Color32(239, 89, 100, 255);
-    private static readonly Color Down = new Color32(76, 150, 232, 255);
+    private static Color Up => MarketColors.Up;
+    private static Color Down => MarketColors.Down;
     const string IconPath = "Stock/";
     /// <summary>이 행이 표시하는 종목. Set() 시 저장, 버튼 클릭 시 이 종목으로 오더북·차트 전환.</summary>
     private Stock _stock;
     public Stock BoundStock => _stock;
     public float VolumeSortValue { get; private set; }
     public float ChangeSortValue { get; private set; }
     private RectTransform rowRect;
     private bool badgeLayoutDirty = true;

```

## Assets/Scripts/StockListUI/StockListUIManager.cs
```diff
--- 원본/Assets/Scripts/StockListUI/StockListUIManager.cs
+++ 수정/Assets/Scripts/StockListUI/StockListUIManager.cs
@@ -34,23 +34,25 @@
     {
         yield return null;
         BuildList();
         InvokeRepeating(nameof(RefreshAll), refreshInterval, refreshInterval);
     }
 
     void OnEnable()
     {
+        MarketColors.Changed += RefreshAll;
         VisibleList = this;
         StockManager.OnStockDelisted += OnStockDelisted;
         StockManager.OnStockListed += OnStockListed;
     }
 
     void OnDisable()
     {
+        MarketColors.Changed -= RefreshAll;
         if (VisibleList == this) { VisibleList = null; }
         StockManager.OnStockDelisted -= OnStockDelisted;
         StockManager.OnStockListed -= OnStockListed;
     }
 
     void OnDestroy()
     {
         CancelInvoke(nameof(RefreshAll));

```
