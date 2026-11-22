using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One bounded play session: actual device/tab interactions and isolated account display fixtures.</summary>
public static class HoldingsTabletValidation
{
    private const string Output = "Validation/HoldingsTablet/";
    private static readonly StringBuilder results = new();
    private static readonly List<string> errors = new();
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static int phase;
    private static double began, ready;
    private static Camera camera;
    private static DeviceTerminalUI orders, journal;
    private static HoldUIManager holdings;
    private static HoldListItem detail;
    private static Vector3[] deskPosition, deskScale;
    private static Quaternion[] deskRotation;
    private static Trader fixture;
    private static Stock sample;
    public static void Run()
    {
        HoldingsTabletPatch.Apply();
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; ready = began + 3;
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{"Assets/Scenes 1/TradeScene.unity"},locationPathName = Output+"Player/DonDon.exe",target = BuildTarget.StandaloneWindows64,options = BuildOptions.Development });
        var result = report.summary;
        File.WriteAllText(Output+"build-results.txt",result.result+"\nErrors: "+result.totalErrors+"\nWarnings: "+result.totalWarnings+"\nSeconds: "+result.totalTime.TotalSeconds+"\nOutput: "+result.outputPath);
        EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
    }
    private static void Log(string message,string stack,LogType type)
    { if (type == LogType.Error || type == LogType.Exception) { errors.Add(message); } }
    private static void Next(double delay = .6) { phase++; ready = EditorApplication.timeSinceStartup + delay; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup-began > 110)
        { Finish(new Exception("110 second timeout at phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready)
        { return; }
        try
        {
            var market = StockManager.Instance;
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                if (!market.SetupConfirmed) { Check(market.ConfirmPlayer("플레이어 기업","1000000","30",out string reason),"initial retail account: " + reason); }
                Next(2); return;
            }
            if (!market.SetupConfirmed || !MarketDayController.Instance.Ready || !MarketDayController.CanTrade)
            { return; }
            if (phase == 1)
            {
                camera = Camera.main;
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                Time.timeScale = 1f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; camera.fieldOfView = 45f;
                var screens = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include); orders = screens.Single(x => !x.journal); journal = screens.Single(x => x.journal);
                holdings = journal.legacy.legacyPanel.GetComponent<HoldUIManager>(); detail = holdings.selectedPosition;
                var devices = new[]{orders.thisTablet.tabletObject,journal.thisTablet.tabletObject};
                deskPosition = devices.Select(t => t.position).ToArray(); deskRotation = devices.Select(t => t.rotation).ToArray(); deskScale = devices.Select(t => t.localScale).ToArray();
                Check(holdings.account == market.playerAccount,"holdings tab binds to the actual player account");
                Check(!Object.FindAnyObjectByType<InteractionManager>().enabled,"obsolete E prompt remains disabled");
                orders.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 2)
            {
                Check(orders.thisTablet.IsHeld,"tablet 1 opens through the shortcut toggle path");
                orders.thisTablet.FocusTablet(); Check(orders.thisTablet.IsHeld,"programmatic FocusTablet remains an open-only action");
                orders.quantity.Select(); orders.quantity.ActivateInputField(); Next(.3); return;
            }
            if (phase == 3)
            {
                Check(MouseCameraController.TextEntryActive,"quantity field receives text input");
                orders.thisTablet.ToggleTablet(); Check(orders.thisTablet.IsHeld,"typing 1 or 2 cannot close the active tablet");
                orders.quantity.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                orders.thisTablet.ToggleTablet(); Check(!orders.thisTablet.IsHeld,"same toggle closes tablet 1");
                orders.thisTablet.ToggleTablet(); Check(!orders.thisTablet.IsHeld,"rapid repeated toggle cannot interrupt the return animation");
                Next(1); return;
            }
            if (phase == 4)
            {
                CheckDesk(orders.thisTablet,0); journal.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 5)
            {
                Check(journal.thisTablet.IsHeld,"tablet 2 opens through its own toggle path");
                Click(journal.legacy.legacyTab); Next(); return;
            }
            if (phase == 6)
            {
                Check(journal.legacy.legacyPanel.activeInHierarchy && !journal.panel.activeSelf,"holdings tab exclusively shows the existing holdings panel");
                Check(detail.textName.text == market.CurrentStock.stockName && detail.textName.text != "삼성전자","sample stock name is replaced by current selection");
                Check(detail.textEmpty.text == "보유 없음" && detail.textQuantity.text == "0주" && detail.textAvgPrice.text == "—","unowned stock has an explicit empty state without invented average");
                Check(holdings.textCash.text == DeviceTerminalUI.Money(market.playerAccount.cash) && holdings.textTotal.text == holdings.textCash.text,"initial summary shows actual cash and zero equity");
                Check(holdings.activeTabImage.color != journal.mainTab.image.color,"current holdings tab is visibly selected");
                Inspect("live empty holdings"); Frame(); Capture("holdings-empty.png");
                market.SelectStock((market.stocks.IndexOf(market.CurrentStock)+1)%market.stocks.Count); Next(); return;
            }
            if (phase == 7)
            {
                Check(detail.textName.text == market.CurrentStock.stockName && detail.textSymbol.text == market.CurrentStock.symbol,"selection changes update name and ticker");
                Fixture(); Inspect("six share profit and escrow"); Capture("holdings-profit-fixture.png"); Next(); return;
            }
            if (phase == 8)
            {
                sample.currentPrice = 4500m; detail.SetFromAccount(sample,fixture);
                Check(detail.textEvaluationProfit.text == "-3,000돈" && detail.textProfitPct.text == "-10.0%","negative position shows exact won loss and return");
                Check(detail.textEvaluationProfit.color == new Color32(76,150,232,255),"loss uses the existing blue palette");
                Inspect("loss");
                sample.orderBook.CancelAllOrdersByOwner(fixture,sample); fixture.GrantShares(sample,1000000000m,5000m); sample.currentPrice = 5390m;
                sample.stockName = "매우긴종목명을가진보유상세가독성검증기업";
                fixture.cash = 1234567890123m; detail.SetFromAccount(sample,fixture);
                Invoke(holdings,"SetSummaryTexts",fixture.cash,fixture.GetOwnedVolume(sample)*sample.currentPrice);
                Inspect("large amounts",true); Capture("holdings-large-fixture.png");
                Check(!detail.textName.enableAutoSizing && detail.textName.overflowMode == TextOverflowModes.Ellipsis,"long names retain fixed typography and ellipsis");
                Check(detail.textQuantity.text == "1,000,000,006주","large holdings preserve integer share count");
                fixture.ForceRemoveHolding(sample,fixture.GetHolding(sample)); detail.SetFromAccount(sample,fixture);
                Check(detail.textEmpty.text == "보유 없음" && detail.textProfitPct.text == "—","position removal clears prior profit and return");
                holdings.account = market.playerAccount; holdings.enabled = true; Next(); return;
            }
            if (phase == 9)
            {
                Check(detail.textName.text == market.CurrentStock.stockName && holdings.textCash.text == DeviceTerminalUI.Money(market.playerAccount.cash),"fixture display restores actual player bindings");
                Click(journal.mainTab); Check(!holdings.viewRoot.activeInHierarchy && journal.panel.activeSelf,"return to events hides holdings");
                Click(journal.legacy.legacyTab); Next(); return;
            }
            if (phase == 10)
            {
                Inspect("reopened holdings"); Frame(); Capture("holdings-live.png");
                journal.thisTablet.ToggleTablet(); Check(!journal.thisTablet.IsHeld,"same toggle closes tablet 2"); Next(1); return;
            }
            if (phase == 11)
            {
                CheckDesk(journal.thisTablet,1); camera.fieldOfView = 10f; journal.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 12)
            {
                Frame(); Inspect("holdings FOV 10"); Capture("holdings-fov10.png"); journal.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 13)
            {
                CheckDesk(journal.thisTablet,1);
                Check(Time.timeScale == 1f && errors.Count == 0,"normal simulation speed and no runtime errors: " + string.Join(" | ",errors)); Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Fixture()
    {
        holdings.enabled = false;
        sample = new Stock("HLD_TEST","보유 상세 검증",5390m,10000000000m);
        fixture = new GameObject("Holdings Display Fixture").AddComponent<Trader>(); fixture.cash = 1000000m; fixture.GrantShares(sample,6m,5000m);
        holdings.account = fixture;
        fixture.SellLimit(6000m,2m,0,sample); fixture.BuyLimit(4000m,1m,0,sample);
        detail.SetFromAccount(sample,fixture); Invoke(holdings,"SetSummaryTexts",fixture.cash,fixture.GetOwnedVolume(sample)*sample.currentPrice);
        Check(detail.textQuantity.text == "6주" && detail.textAvgPrice.text == "5,000돈","cost basis and quantity include pending sell escrow");
        Check(detail.textEvaluationProfit.text == "+2,340돈" && detail.textProfitPct.text == "+7.8%","six shares at 5000 with price 5390 show exact profit");
        Check(detail.textCurrentPrice.text == "5,390돈" && detail.textCurrentValue.text == "32,340돈","current price and total position value are distinct");
        Check(detail.textAvailability.text == "매도 가능 4주  ·  매도 대기 2주","available shares are separated from pending sells");
        Check(holdings.textCash.text == "1,000,000돈" && holdings.textTotal.text == "1,032,340돈","reserved cash is included once in total assets");
        Check(holdings.textCashAvailability.text == "가용 현금 996,000돈  ·  매수 예치 4,000돈","cash available and buy-order reserve are separately labelled");
        Check(detail.textEvaluationProfit.color == new Color32(239,89,100,255),"profit uses the existing red palette");
    }
    private static void CheckDesk(TabletViewController tablet,int index)
    {
        var t = tablet.tabletObject;
        Check(!tablet.IsHeld && !tablet.IsReturning && Vector3.Distance(t.position,deskPosition[index]) < .0001f && Quaternion.Angle(t.rotation,deskRotation[index]) < .01f && Vector3.Distance(t.localScale,deskScale[index]) < .0001f,"tablet " + (index+1) + " restores its original desk pose and scale");
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); Check(button.gameObject.activeInHierarchy && button.interactable,"clickable " + button.name);
        var rect = (RectTransform)button.transform; var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"topmost game camera raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Inspect(string stage,bool longName = false)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var label in holdings.viewRoot.GetComponentsInChildren<TextMeshProUGUI>())
        {
            label.ForceMeshUpdate();
            if (longName && label == detail.textName)
            { continue; }
            if (label.isTextOverflowing) { throw new Exception("Text overflow " + stage + " / " + label.name + " / " + label.text); }
            if (label.raycastTarget || label.enableAutoSizing) { throw new Exception("Invalid text settings " + label.name); }
        }
        Check(true,"all visible labels fit with fixed font sizes: " + stage);
    }
    private static void Frame()
    {
        var corners = new Vector3[4]; ((RectTransform)journal.transform).GetWorldCorners(corners);
        Check(corners.All(c => { var p = camera.WorldToViewportPoint(c); return p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1; }),"tablet Canvas fits actual game camera at FOV " + camera.fieldOfView);
    }
    private static void Capture(string filename)
    {
        Canvas.ForceUpdateCanvases(); var target = new RenderTexture(1600,900,24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1600,900,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply(); File.WriteAllBytes(Output+filename,pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); target.Release(); Object.Destroy(target);
    }
    private static void Invoke(object target,string method,params object[] values) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,values);
    private static void Check(bool ok,string text)
    { if (!ok) { throw new Exception(text); } results.AppendLine("PASS " + text); }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        if (sample != null && fixture != null) { sample.orderBook.CancelAllOrdersByOwner(fixture,sample); }
        if (error != null) { results.AppendLine("FAIL " + error); }
        File.WriteAllText(Output+"game-results.txt",results.ToString()); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
