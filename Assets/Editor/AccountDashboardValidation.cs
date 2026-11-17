using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Single bounded game run. Financial fixtures are transient and never saved into the scene.</summary>
public static class AccountDashboardValidation
{
    private const string Output = "Validation/AccountDashboard/";
    private static readonly StringBuilder results = new();
    private static readonly List<string> errors = new();
    private static readonly List<Stock> fixtures = new();
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static int phase;
    private static double began, next;
    private static Camera camera;
    private static TabletAccountUI first, second;
    private static PlayerAccounts accounts;
    private static Trader counterparty;
    private static Vector3 firstPosition, secondPosition;
    private static decimal beforeCash;
    private static bool dashboardReady;
    private static int secondaryStep;
    private static decimal transferBefore;
    public static void Run() { AccountDashboardPatch.Apply(); Begin(); }
    public static void RunCurrent() { EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity"); Begin(); }
    public static void RunFinal() { AccountDashboardPatch.FinalizeInputSurface(); Begin(); }
    private static void Begin()
    {
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; next = began + 2;
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{"Assets/Scenes 1/TradeScene.unity"},locationPathName = Output+"Player/DonDon.exe",target = BuildTarget.StandaloneWindows64,options = BuildOptions.Development });
        var summary = report.summary;
        File.WriteAllText(Output+"build-results.txt",summary.result+"\nErrors: "+summary.totalErrors+"\nWarnings: "+summary.totalWarnings+"\nSeconds: "+summary.totalTime.TotalSeconds);
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    private static void Log(string message,string stack,LogType type)
    { if (type == LogType.Error || type == LogType.Exception) { errors.Add(message+"\n"+stack); } }
    private static void Next(double seconds=.6) { phase++; next = EditorApplication.timeSinceStartup+seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup-began > 115)
        { Finish(new Exception("Timeout at phase "+phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next)
        { return; }
        try
        {
            var market = StockManager.Instance;
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                accounts = PlayerAccounts.Instance;
                var screens = Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include); first = screens.Single(x=>!x.second); second = screens.Single(x=>x.second);
                first.dashboard.Refresh(); Check(first.dashboard.valuation.text == "—","loading is distinct from zero");
                Check(market.ConfirmPlayer("플레이어 기업","1000000","30",out string reason),"initial economy confirmation: "+reason); Next(2); return;
            }
            if (!MarketDayController.Instance.Ready || !MarketDayController.CanTrade)
            { return; }
            if (phase == 1)
            {
                camera = Camera.main; camera.fieldOfView = 45f;
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                Time.timeScale = 1f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                firstPosition = first.orders.thisTablet.tabletObject.position; secondPosition = second.orders.thisTablet.tabletObject.position;
                Check(accounts.primary.cash == market.economy.retailStartingCash && accounts.automated.cash == 0m,"configured retail cash and zero newly created account cash");
                Check(first.orders.session != second.orders.session && first.events.records != second.events.records,"independent sessions and execution journals");
                Check(second.Locked && second.lockRoot.activeInHierarchy && !second.contentRoot.activeSelf,"second tablet shows only lock layer");
                Check(second.lockRoot.GetComponentsInChildren<TextMeshProUGUI>(true).Length == 0,"locked screen contains no text");
                Check(!TradingOperationController.Instance.Prepare(),"automated orders blocked before unlock");
                Check(!accounts.Transfer(accounts.primary,1,out _),"transfers blocked before unlock");
                Check(!Object.FindAnyObjectByType<InteractionManager>().enabled,"obsolete E popup stays disabled");
                first.orders.thisTablet.FocusTablet(); Next(1); return;
            }
            if (phase == 2)
            {
                first.ShowPage(TabletAccountUI.Page.Dashboard); first.dashboard.Refresh();
                string startingCash = market.economy.retailStartingCash.ToString("N0") + "원";
                Check(first.dashboard.total.text == startingCash && first.dashboard.available.text == startingCash && first.dashboard.valuation.text == "0원","empty account displays real cash and total assets");
                Check(first.dashboard.empty.gameObject.activeSelf && !first.dashboard.detail.interactable,"empty list and disabled unowned detail");
                Inspect(first.dashboard.gameObject,"empty dashboard"); Capture("empty-1600x900.png",1600,900); Capture("empty-1920x1080.png",1920,1080);
                Click(first.tabs[1]); Check(first.events.panel.activeInHierarchy,"events available on account one while account two is locked");
                first.events.FocusGrowth(); Check(first.events.growthPanel.activeInHierarchy,"initial progression remains accessible on account one");
                first.events.growthPanel.SetActive(false); Canvas.ForceUpdateCanvases(); Capture("events-debug.png",1600,900); Click(first.managementButton); Check(first.events.legacy.deskPanel.activeInHierarchy,"existing management remains connected");
                first.orders.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 3)
            {
                Check(Vector3.Distance(firstPosition,first.orders.thisTablet.tabletObject.position)<.001f,"tablet one returns to original desk position");
                second.orders.thisTablet.FocusTablet(); Next(1); return;
            }
            if (phase == 4)
            {
                Capture("locked-1600x900.png",1600,900);
                beforeCash = accounts.primary.cash; accounts.primary.cash = 100000000m; accounts.EvaluateUnlock(); Check(!accounts.AutomatedUnlocked,"exactly one hundred million remains locked");
                accounts.primary.cash = 100000001m; accounts.EvaluateUnlock(); Check(accounts.AutomatedUnlocked && !second.Locked && second.contentRoot.activeSelf,"strictly above threshold unlocks immediately");
                accounts.primary.cash = beforeCash; Check(accounts.AutomatedUnlocked,"unlock persists after asset decline in the same world");
                Check(accounts.Transfer(accounts.primary,200000m,out _),"explicit primary to secondary transfer");
                Check(accounts.primary.cash == 800000m && accounts.automated.cash == 200000m,"transfer conserves combined cash");
                Check(accounts.Transfer(accounts.automated,200000m,out _),"reverse transfer supported");
                Check(!accounts.Transfer(accounts.primary,1000001m,out _),"overdraft rejected");
                second.orders.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 5)
            {
                Check(Vector3.Distance(secondPosition,second.orders.thisTablet.tabletObject.position)<.001f,"tablet two returns to original desk position");
                CreateFixtures(); first.orders.thisTablet.FocusTablet(); first.ShowPage(TabletAccountUI.Page.Dashboard); Next(1); return;
            }
            if (phase == 6)
            {
                FinancialChecks(); first.dashboard.Refresh(); Inspect(first.dashboard.gameObject,"mixed holdings");
                Capture("holdings-1600x900.png",1600,900); Capture("holdings-1920x1080.png",1920,1080);
                var row = first.dashboard.content.GetComponentsInChildren<InvestmentPositionRow>().First(); Click(row.button);
                Check(market.CurrentStock == row.Stock,"row click selects the existing global stock");
                Click(first.dashboard.detail); Check(first.holdings.gameObject.activeInHierarchy,"selected position routes to existing detail"); Next(); return;
            }
            if (phase == 7)
            {
                if (!dashboardReady)
                {
                    Check(first.holdings.selectedPosition.textName.text == market.CurrentStock.stockName,"detail reads selected account position");
                    Click(first.tabs[0]); dashboardReady = true; next = EditorApplication.timeSinceStartup + .4; return;
                }
                var before = first.dashboard.Ordered.ToArray(); float scrollY = first.dashboard.content.anchoredPosition.y;
                before[0].currentPrice = 1m; first.dashboard.Refresh();
                Check(before.SequenceEqual(first.dashboard.Ordered),"price tick never silently reorders rows");
                Check(first.dashboard.content.anchoredPosition.y == scrollY && market.CurrentStock == before[0],"refresh preserves scroll and selected stock");
                Click(first.dashboard.sort); Check(first.dashboard.SortMode == 1,"explicit return sort");
                before[0].currentPrice = 2350m;
                for (int i=0; i<7; i++) { var stock = new Stock("EX"+i,"추가 종목 "+i,100m,10000m); fixtures.Add(stock); market.stocks.Add(stock); accounts.primary.GrantShares(stock,1,100m); }
                first.dashboard.Refresh(); Canvas.ForceUpdateCanvases();
                var pointer = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0,-3), position = Center(first.dashboard.scroll.viewport) };
                ExecuteEvents.Execute(first.dashboard.scroll.gameObject,pointer,ExecuteEvents.scrollHandler);
                Check(first.dashboard.content.anchoredPosition.y > 0f && camera.fieldOfView == 45f,"holdings scroll leaves camera FOV unchanged");
                first.dashboard.Refresh(); Check(first.dashboard.content.anchoredPosition.y > 0f,"scroll remains after data refresh");
                OrderChecks(); Click(first.tabs[2]); Next(); return;
            }
            if (phase == 8)
            {
                Check(first.ledger.text.Contains("대만디스플레이"),"transaction tab shows actual account fills");
                var progress = PlayerProgression.Instance; string saved = progress.Capture(); Check(progress.Restore(saved,progress.WorldId),"existing progression capture/restore remains compatible");
                Check(!progress.Restore(saved,"different-world"),"cross-world progress restore remains rejected");
                first.orders.thisTablet.ToggleTablet(); Next(1); return;
            }
            if (phase == 9)
            {
                second.orders.thisTablet.FocusTablet(); second.ShowPage(TabletAccountUI.Page.Dashboard); Next(1); return;
            }
            if (phase == 10)
            {
                if (secondaryStep == 0)
                {
                    second.dashboard.Refresh(); Check(second.dashboard.Ordered.Count == 1,"secondary dashboard uses only secondary holdings");
                    Capture("second-account-1600x900.png",1600,900);
                    Click(second.transferButton); Check(second.transferPanel.activeSelf,"transfer dialog opens inside the same tablet");
                    transferBefore = accounts.automated.cash; secondaryStep++; next = EditorApplication.timeSinceStartup+.4; return;
                }
                if (secondaryStep == 1)
                {
                    second.transferAmount.text = "1000"; Click(second.transferSubmit);
                    Check(accounts.automated.cash == transferBefore-1000m && second.transferStatus.text.Contains("이체 완료"),"transfer button executes exact amount and reports success");
                    Click(second.transferCancel); secondaryStep++; next = EditorApplication.timeSinceStartup+.4; return;
                }
                Click(second.orderButton); Check(second.orders.panel.activeInHierarchy && second.orders.session.account == accounts.automated,"second tablet uses own manual order screen");
                second.orders.quantity.Select(); second.orders.quantity.ActivateInputField(); Next(); return;
            }
            if (phase == 11)
            {
                Check(MouseCameraController.TextEntryActive,"order field receives text input");
                second.orders.thisTablet.ToggleTablet(); Check(second.orders.thisTablet.IsHeld,"typing digits cannot toggle tablet");
                second.orders.quantity.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                second.orders.price.text = "1"; second.orders.quantity.text = "1"; Click(second.orders.buy); Click(second.orders.review);
                Next(); return;
            }
            if (phase == 12)
            {
                Check(second.orders.reviewPanel.activeInHierarchy,"existing review button shows confirmation on secondary tablet");
                Click(second.orders.submit); Next(); return;
            }
            if (phase == 13)
            {
                Check(second.orders.session.Active && accounts.automated.ReservedCash == 1m,"actual secondary submit button creates the engine order");
                second.orders.session.Cancel(); second.ShowPage(TabletAccountUI.Page.Automation);
                Check(second.orders.legacy.deskPanel.activeInHierarchy && TradingOperationController.Instance.account == accounts.automated,"existing automated trading belongs to account two");
                ExtraChecks();
                Check(errors.Count == 0,"no runtime errors: "+string.Join("\n",errors)); Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CreateFixtures()
    {
        counterparty = new GameObject("Validation Counterparty").AddComponent<Trader>(); counterparty.cash = 10000000;
        string[] names = {"대만디스플레이","국민금융","그린푸드","노보손해보험","미래바이오"};
        decimal[] prices={2350,1280,890,535,1420}, costs={2100,1160,940,480,1580}, amounts={120,150,200,300,100};
        for(int i=0;i<5;i++)
        {
            var stock = new Stock("QA"+i,names[i],prices[i],10000000); fixtures.Add(stock); StockManager.Instance.stocks.Add(stock);
            accounts.primary.GrantShares(stock,amounts[i],costs[i]);
            Invoke(first.events.records,"Register",stock); Invoke(second.events.records,"Register",stock); Invoke(PlayerProgression.Instance,"Register",stock);
        }
        StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(fixtures[0]));
        accounts.primary.cash = 245500m;
    }
    private static void FinancialChecks()
    {
        var ui = first.dashboard; ui.Refresh();
        Check(ui.valuation.text == "954,500원" && ui.total.text == "1,200,000원","valuation and total agree with exact ledger math");
        Check(ui.profit.text == "+38,500원  (+4.20%)" && ui.cost.text == "총 매수원금  916,000원","aggregate return uses total cost, not average of returns");
        var stock = fixtures[0]; accounts.primary.BuyLimit(1m,100m,0,stock); accounts.primary.SellLimit(5000m,20m,0,stock); ui.Refresh();
        Check(accounts.primary.ReservedCash == 100m && ui.available.text == "245,400원","available cash excludes reserved buys");
        Check(ui.total.text == "1,200,000원" && ui.valuation.text == "954,500원","reserved cash and pending sells remain in total assets");
        Check(accounts.primary.GetHolding(stock) == 100m && accounts.primary.GetOwnedVolume(stock) == 120m && accounts.primary.GetOwnedCost(stock) == 252000m,"sell escrow retains exact quantity and cost");
        Check(!accounts.Transfer(accounts.primary,245401m,out _),"transfer cannot spend reserved cash");
        var rows = ui.content.GetComponentsInChildren<InvestmentPositionRow>();
        Check(rows.Any(r=>r.profit.color == new Color32(239,89,100,255)) && rows.Any(r=>r.profit.color == new Color32(76,150,232,255)),"mixed gains and losses have signs and distinct colors");
        decimal cash=accounts.primary.cash,quantity=accounts.primary.GetOwnedVolume(stock),reserved=accounts.primary.ReservedCash;
        ui.Refresh(); ui.Refresh(); Check(cash==accounts.primary.cash && quantity==accounts.primary.GetOwnedVolume(stock) && reserved==accounts.primary.ReservedCash,"UI refresh never mutates assets or orders");
        stock.orderBook.CancelAllOrdersByOwner(accounts.primary,stock);
    }
    private static void OrderChecks()
    {
        var stock = fixtures[0]; counterparty.GrantShares(stock,100,stock.currentPrice); counterparty.SellLimit(stock.currentPrice,30,0,stock);
        Check(first.orders.session.Prepare(stock) && first.orders.session.Submit(stock,true,stock.currentPrice,1,1,3),"primary manual order accepted by existing validation");
        Invoke(first.orders.session,"Update");
        Check(first.orders.session.Filled == 1 && first.events.records.Fills.Count == 1 && second.events.records.Fills.Count == 0,"primary fill appears only in its own ledger");
        Check(accounts.Transfer(accounts.primary,100000m,out _),"secondary funded with existing player cash");
        Check(second.orders.session.Prepare(stock) && second.orders.session.Submit(stock,true,stock.currentPrice,1,1,3),"secondary independently submits a manual order");
        Invoke(second.orders.session,"Update");
        Check(second.orders.session.Filled == 1 && second.events.records.Fills.Count == 1 && first.events.records.Fills.Count == 1,"secondary fill remains isolated");
        Check(first.orders.session.Prepare(stock) && first.orders.session.Submit(stock,true,100m,2,1,3),"primary independently waits for a limit price");
        Check(second.orders.session.Prepare(stock) && second.orders.session.Submit(stock,true,100m,3,1,3),"secondary independently waits for a limit price");
        Invoke(first.orders.session,"Update"); Invoke(second.orders.session,"Update");
        Check(accounts.primary.ReservedCash == 200m && accounts.automated.ReservedCash == 300m,"pending orders reserve separate cash ledgers");
        first.orders.session.Cancel(); Check(accounts.primary.ReservedCash == 0m && accounts.automated.ReservedCash == 300m && second.orders.session.Active,"canceling one account leaves the other's orders untouched");
        second.orders.session.Cancel();
        decimal before = PlayerProgression.Instance.RealizedProfit;
        stock.orderBook.CancelAllOrdersByOwner(counterparty,stock);
        counterparty.BuyLimit(2400m,2,0,stock); accounts.automated.SellLimit(2400m,1,0,stock);
        Check(PlayerProgression.Instance.RealizedProfit-before == 50m,"secondary realized profit contributes to player progression");
        accounts.automated.GrantShares(stock,6,2300m);
        var zero = fixtures.Last(); accounts.primary.ForceRemoveHolding(zero,accounts.primary.GetOwnedVolume(zero)); accounts.primary.GrantShares(zero,2,0m); first.dashboard.Refresh();
        var row = first.dashboard.content.GetComponentsInChildren<InvestmentPositionRow>().Single(r=>r.Stock==zero);
        Check(row.percent.text == "—" && row.average.text == "내 평균 —","zero acquisition cost displays undefined return as dash");
        zero.currentPrice=0m; first.dashboard.Refresh(); Check(first.dashboard.valuation.text == "—" && row.price.text == "—","missing valuation is distinct from true zero"); zero.currentPrice=100m;
    }
    private static void ExtraChecks()
    {
        var hidden = new Stock("UNLISTED_QA","목록 밖 보유 종목",150m,10000m);
        accounts.primary.GrantShares(hidden,2,100m);
        first.ShowPage(TabletAccountUI.Page.Dashboard); first.dashboard.Refresh();
        Check(first.dashboard.Ordered.Contains(hidden),"owned positions outside the listed menu remain in dashboard");
        first.dashboard.Select(hidden); first.ShowPage(TabletAccountUI.Page.Detail);
        Invoke(first.holdings,"Refresh"); Check(first.holdings.selectedPosition.textName.text == hidden.stockName,"unlisted position can open its own holdings detail");
        first.ShowPage(TabletAccountUI.Page.Dashboard);
        decimal cash = accounts.primary.cash; accounts.primary.cash = 9876543210m; first.dashboard.Refresh();
        first.dashboard.valuation.ForceMeshUpdate(); first.dashboard.total.ForceMeshUpdate(); first.dashboard.available.ForceMeshUpdate();
        Check(!first.dashboard.total.isTextOverflowing && !first.dashboard.available.isTextOverflowing,"large cash amounts fit without per-row auto-size");
        accounts.primary.cash = cash;
        var row = first.dashboard.content.GetComponentsInChildren<InvestmentPositionRow>().First(); string name = row.Stock.stockName;
        row.Stock.stockName = "아주 긴 종목명을 가진 가상 기업의 이름을 표시하는 테스트"; first.dashboard.scroll.verticalNormalizedPosition = 1f;
        first.dashboard.Refresh(); Canvas.ForceUpdateCanvases(); row.company.ForceMeshUpdate(true,true);
        bool needsEllipsis = row.company.GetPreferredValues(row.company.text).x > row.company.rectTransform.rect.width;
        Check(row.company.overflowMode == TextOverflowModes.Ellipsis && needsEllipsis,"long names use intentional ellipsis within their own column"); row.Stock.stockName = name;
        var incumbent = Object.FindAnyObjectByType<IncumbentExchangeAI>();
        incumbent.Observe(incumbent.NextObservationAt+1f);
        Check(incumbent.Snapshot.Cash == accounts.primary.cash + accounts.automated.cash,"incumbent observes combined cash from both player accounts");
    }
    private static void Inspect(GameObject root,string state)
    {
        Canvas.ForceUpdateCanvases();
        foreach(var label in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            label.ForceMeshUpdate();
            Check(!label.isTextOverflowing,state+" fits "+label.name);
            Check(!label.enableAutoSizing && !label.raycastTarget,state+" fixed nonblocking text "+label.name);
        }
    }
    private static Vector2 Center(RectTransform rect)
    { return RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)); }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = Center((RectTransform)button.transform) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"raycast reaches "+button.name+" (hits: "+string.Join(", ",hits.Take(3).Select(h=>h.gameObject.name))+")");
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Capture(string file,int width,int height)
    {
        Canvas.ForceUpdateCanvases(); var target = new RenderTexture(width,height,24); var previous=RenderTexture.active; var old=camera.targetTexture;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply(); File.WriteAllBytes(Output+file,texture.EncodeToPNG());
        camera.targetTexture=old; RenderTexture.active=previous; Object.Destroy(texture); target.Release(); Object.Destroy(target);
    }
    private static void Invoke(object target,string method,params object[] args) => target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    private static void Check(bool condition,string message)
    { if(!condition) { throw new Exception(message); } results.AppendLine("PASS "+message); }
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        if(error!=null) { results.AppendLine("FAIL "+error); }
        if(errors.Count>0) { results.AppendLine(string.Join("\n",errors)); }
        File.WriteAllText(Output+"game-results.txt",results.ToString()); EditorApplication.Exit(error==null ? 0 : 1);
    }
}
