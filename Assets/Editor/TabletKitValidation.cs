using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

/// <summary>One page/process, actual World Space raycasts, real order engine; isolated test fixtures only.</summary>
public static class TabletKitValidation
{
    private static string output,language;
    private static bool hadLanguage,oldOptions,spawned,finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase,checks,errors,locale,dueFrame,mode;
    private static TabletAccountUI tablet,second;
    private static Stock stock;
    private static Trader seller;
    private static readonly string[] languages={"ko","en","ja"};
    public static void Assets() { mode=0; Run(); }
    public static void Orders() { mode=1; Run(); }
    public static void Company() { mode=2; Run(); }
    public static void Auto() { mode=3; Run(); }
    public static void Save() { mode=4; Run(); }
    public static void Special() { mode=5; Run(); }
    public static void Cold() { mode=6; Run(); }
    private static void Run()
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output)) { throw new Exception("Use bounded runner"); }
        Directory.CreateDirectory(output); hadLanguage=PlayerPrefs.HasKey("DonDon.Language"); language=PlayerPrefs.GetString("DonDon.Language");
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log; EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>200) { Finish("time budget"); return; }
        if (EditorApplication.isPlaying&&!spawned) { spawned=true; new GameObject("Test :: Tablet Kit").AddComponent<TabletKitProbe>(); }
    }
    private static void Log(string message,string stack,LogType type)
    { if (type==LogType.Error||type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),message+"\n"+stack+"\n"); } }
    private static void Check(bool ok,string name)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(ok?"PASS ":"FAIL ")+name+"\n"); if (!ok) { throw new Exception(name); } checks++; }
    private static void Wait(int next) { phase=next; dueFrame=Time.frameCount+12; }
    public static void Step()
    {
        if (finished||!GameLocale.Ready||Time.frameCount<dueFrame)
        { return; }
        try
        {
            var day=MarketDayController.Instance;
            if (phase==0)
            {
                if (!NewGame.CanBegin) { return; }
                NewGame.Begin("플레이어 기업"); Wait(1); return;
            }
            if (phase==1)
            {
                if (day==null||!day.Ready||day.Phase!=MarketPhase.Trading)
                { return; }
                var views=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include);
                tablet=views.Single(t=>!t.second); second=views.Single(t=>t.second);
                Check(tablet.orders.session!=second.orders.session&&tablet.account!=second.account,"separate account and order instances");
                Check(second.Locked&&second.lockRoot.GetComponentsInChildren<TMP_Text>().Length==0,"lock screen has icon only");
                for (int i=0;i<48;i++) { Check(TabletKitStyle.Icon(File.ReadAllLines("Design/TabletUIKit/Docs/IconIndex.csv")[i+1].Split(',')[1])!=null,"atlas cell "+i); }
                if (mode==4||mode==6) { SaveChecks(); return; }
                if (mode==0)
                {
                    second.account.cash=0; second.dashboard.Refresh(); Check(second.dashboard.total.text=="0돈"&&second.dashboard.empty.gameObject.activeSelf,"empty zero balance is real zero");
                }
                Fixture();
                if (mode==1) { OrderChecks(); }
                if (mode==2)
                {
                    tablet.ShowPage(TabletAccountUI.Page.Management); CompanyChecks();
                    SessionStore.Restore(File.ReadAllText("Validation/Settings/CompanyGrowth02/session-v2.json"));
                    Check(CorporateEconomy.Instance.PlayerCompany.business.growth.CompletedProjects==2,"visual uses actual paid-growth DAY 5 checkpoint");
                }
                if (mode==3||mode==5) { AutoChecks(); tablet=second; }
                if (mode==5) { StockManager.Instance.SelectStock(0); }
                tablet.ShowPage(mode==0 ? TabletAccountUI.Page.Dashboard : mode==1 ? TabletAccountUI.Page.Orders : mode==2 ? TabletAccountUI.Page.Management : mode==5 ? TabletAccountUI.Page.Special : TabletAccountUI.Page.Automation);
                DeskViewController.Instance.Focus(mode==3||mode==5 ? DeskViewTarget.TabletTwo : DeskViewTarget.TabletOne);
                Wait(2); return;
            }
            if (phase==2)
            {
                if (GameLocale.Code!=languages[locale])
                { return; }
                Capture("page-"+mode+"-"+GameLocale.Code,1600,900); Capture("page-"+mode+"-"+GameLocale.Code,1920,1080);
                Audit(tablet.contentRoot);
                if (mode==3&&locale==0)
                { tablet.ShowPage(TabletAccountUI.Page.Dashboard); tablet.dashboard.Refresh(); Wait(3); return; }
                if (locale==0) { InteractionChecks(); }
                if (++locale<3) { GameLocale.SetLanguage(languages[locale]); Wait(2); return; }
                Check(errors==0,"no runtime exceptions"); Finish(null);
            }
            if (phase==3)
            {
                Capture("work-account-assets",1920,1080); Audit(tablet.contentRoot); InteractionChecks();
                locale=1; GameLocale.SetLanguage(languages[locale]); Wait(2);
            }
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }
    private static void Fixture()
    {
        seller=new GameObject("Test :: Seller").AddComponent<Trader>(); seller.cash=10000000;
        stock=new Stock("KIT","LC디스플레이",2350m,10000m); StockManager.Instance.stocks.Add(stock); seller.GrantShares(stock,10000m,2000m);
        tablet.events.records.ReconnectSession(); second.events.records.ReconnectSession();
        StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock));
        tablet.account.cash=245500m; seller.SellLimit(2350m,20m,0,stock); tablet.account.BuyLimit(2350m,20m,0,stock);
        tablet.orders.session.Prepare(stock);
        if (mode==0)
        {
            for (int i=0;i<7;i++)
            {
                var s=new Stock("KIT"+i,new[]{"굿데이금융","그린푸드","노보손해보험","미래바이오","덕상가주에이아이","두더리식품","LC디스플레이"}[i],1000m+i*100m,10000m);
                StockManager.Instance.stocks.Add(s); seller.GrantShares(s,10000m,800m); seller.SellLimit(s.currentPrice,10m+i,0,s); tablet.account.BuyLimit(s.currentPrice,10m+i,0,s);
                s.currentPrice+=i%2==0 ? 150m : -150m;
            }
        }
    }
    private static void OrderChecks()
    {
        var session=tablet.orders.session; tablet.account.cash=100000m;
        Check(session.Submit(stock,true,100m,100m,2,60),"split order accepted");
        Check(session.AvailableCash==90000m&&PlayerAccounts.Available(tablet.account)==90000m,"scheduled and resting cash reserved once across UI and domain");
        Check(session.Submit(stock,true,101m,10m,1,1),"second order while first pending");
        Check(!session.Submit(stock,true,1000m,1000m,1,1),"budget overflow rejected");
        var first=session.ActiveOrders[0]; var secondOrder=session.ActiveOrders[1];
        seller.SellLimit(101m,4m,0,stock);
        Check(secondOrder.Filled==4m&&secondOrder.UpdatedAt.StartsWith("DAY "),"partial fill has game timestamp");
        decimal cash=tablet.account.cash;
        Check(session.Cancel(secondOrder)&&session.ActiveOrders.Count==1,"individual cancel preserves other order");
        Check(tablet.account.cash==cash&&session.AvailableCash==cash-10000m,"cancel releases only remaining reserve, not spent cash");
        session.Cancel();
        Check(tablet.account.ReservedCash==0&&session.ScheduledBuyCost==0,"all actual/scheduled reservations returned");
        Check(session.Submit(stock,false,4000m,2m,1,1),"sell order escrow");
        Check(session.AvailableShares(stock)==tablet.account.GetOwnedVolume(stock)-2m,"reserved sell shares excluded");
        Check(session.Submit(stock,true,100m,10m,1,1),"buy alongside sell pending");
        tablet.orders.price.SetTextWithoutNotify("100"); tablet.orders.quantity.SetTextWithoutNotify("3");
    }
    private static void CompanyChecks()
    {
        var company=CorporateEconomy.Instance.PlayerCompany; var data=company.business; var g=data.growth; var rules=CorporateEconomy.Instance.GrowthRules;
        var view=tablet.GetComponentInChildren<CompanyGrowthUI>(true);
        var apply=view.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Button :: Apply IPO");
        void Gate(bool expected,string message) { view.Refresh(); Check(apply.interactable==expected,message); }
        g.Appraisal=rules.listingValue; g.ProfitableCloses=rules.listingProfitableCloses; g.CompletedProjects=rules.listingProjects; data.Debt=0;
        Check(g.Eligible(data,rules),"all four IPO gates pass");
        Gate(true,"IPO action enabled with all criteria");
        g.Appraisal--; Check(!g.Eligible(data,rules),"IPO appraisal gate"); Gate(false,"IPO button rejects insufficient appraisal"); g.Appraisal++;
        g.ProfitableCloses--; Check(!g.Eligible(data,rules),"IPO profit gate"); Gate(false,"IPO button rejects insufficient profitable closes"); g.ProfitableCloses++;
        g.CompletedProjects--; Check(!g.Eligible(data,rules),"IPO expansion gate"); Gate(false,"IPO button rejects insufficient completed projects"); g.CompletedProjects++;
        data.Debt=1; Check(!g.Eligible(data,rules),"IPO debt gate"); Gate(false,"IPO button rejects debt"); data.Debt=0;
        data.Cash=10000000m; decimal personal=tablet.account.cash, treasury=data.Cash;
        decimal cost=g.ProjectCost(data,CompanyProject.Capacity,rules);
        Check(CorporateEconomy.Instance.StartProject(company,CompanyProject.Capacity),"funded company project starts");
        Check(data.Cash==treasury-cost&&tablet.account.cash==personal,"project charged only company once");
        Check(!CorporateEconomy.Instance.StartProject(company,CompanyProject.Capacity)&&data.Cash==treasury-cost,"duplicate project cannot debit again");
    }
    private static void AutoChecks()
    {
        var accounts=PlayerAccounts.Instance; decimal other=tablet.account.Equity()-tablet.account.cash;
        tablet.account.cash=100000000m-other; accounts.EvaluateUnlock(); Check(!accounts.AutomatedUnlocked,"exact 100M remains locked");
        tablet.account.cash++; accounts.EvaluateUnlock(); Check(accounts.AutomatedUnlocked,"100M plus one unlocks");
        accounts.Transfer(tablet.account,100000m,out _); var op=TradingOperationController.Instance;
        Check(op.StartSpread(stock),"spread starts using work account"); Check(!op.StartSpread(stock),"duplicate start blocked");
        seller.SellLimit(op.SpreadBid,2m,0,stock);
        Check(op.SpreadFills.Count==1&&op.Bought==2m&&op.SpreadFills[0].Time.StartsWith("DAY "),"spread captures actual matching engine fill and game date");
        Check(second.account.ReservedCash>0&&tablet.account.ReservedCash==0,"spread reserves only work cash");
        op.CancelSpread(); Check(second.account.ReservedCash==0,"stop returns reservation"); Check(op.StartSpread(stock),"restart succeeds");
        seller.SellLimit(op.SpreadBid,1m,0,stock);
        Check(FakeNewsManager.FormatCooldown(600)=="05:20","600 real seconds displayed as 5h20 game time at default rate");
        var clock=new NewsPublicationClock();
        Check(clock.TryConsume(100)&&!clock.TryConsume(699.999)&&clock.TryConsume(700),"cooldown real-time boundary is exactly 600 seconds");
        Check(clock.Restore(500,120,10)&&clock.Remaining(10)==380&&clock.Remaining(15)==375,"cooldown reload accounts for real offline duration");
        Check(!clock.Restore(601,0,10)&&!clock.TryConsume(double.NaN),"cooldown rejects invalid saved times");
    }
    private static void InteractionChecks()
    {
        if (mode==0)
        {
            var ui=tablet.dashboard; Click(ui.sort); Check(ui.SortMode==1,"sort button follows actual raycast");
            var order=ui.Ordered.ToArray(); stock.currentPrice*=2; ui.Refresh(); Check(order.SequenceEqual(ui.Ordered),"price ticks preserve clicked sort order");
            Canvas.ForceUpdateCanvases(); var scroll=ui.scroll; var p=new PointerEventData(EventSystem.current) { position=Center(scroll.viewport),scrollDelta=new Vector2(0,-3) };
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits); p.pointerPressRaycast=hits[0]; p.pressPosition=p.position;
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.beginDragHandler); p.position+=new Vector2(0,120); p.delta=new Vector2(0,120);
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.dragHandler); ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.endDragHandler);
            Check(scroll.verticalNormalizedPosition<.99f,"holdings responds to actual world-space drag"); scroll.verticalNormalizedPosition=1;
            ExecuteEvents.Execute(scroll.gameObject,p,ExecuteEvents.scrollHandler); Check(scroll.verticalNormalizedPosition<1,"holdings scroll handles wheel");
            ui.scroll.verticalNormalizedPosition=1;
        }
        if (mode==1)
        {
            var input=tablet.orders.price; input.Select(); input.ActivateInputField();
            Check(EventSystem.current.currentSelectedGameObject==input.gameObject,"price input owns keyboard focus");
            input.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            tablet.orders.price.text="100"; tablet.orders.quantity.text="3";
            typeof(DeviceTerminalUI).GetMethod("Order",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(tablet.orders,null);
            int count=tablet.orders.session.ActiveOrders.Count; Click(tablet.orders.review); Check(tablet.orders.session.ActiveOrders.Count==count+1,"actual primary button adds independent order");
            var cancel=tablet.contentRoot.GetComponentsInChildren<Button>().First(b=>b.name=="Button :: Cancel Pending Order"); Click(cancel);
            Check(tablet.orders.session.ActiveOrders.Count==count,"row cancel leaves other requests");
        }
        if (mode==3)
        {
            Click(tablet.orderButton);
            Check(tablet.orders.tradeView!=null&&tablet.orders.panel.activeInHierarchy&&tablet.orders.session.account==PlayerAccounts.Instance.automated,"work-account manual orders initialize after unlock");
            tablet.ShowPage(TabletAccountUI.Page.Automation);
        }
        if (mode==5)
        {
            var button=tablet.contentRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Publish Draft");
            int before=NewsManager.Instance.ArticleVersion; Click(button);
            Check(NewsManager.Instance.ArticleVersion==before+1&&FakeNewsManager.Instance.PlayerCooldown>598,"actual studio publishes one article and starts real cooldown");
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            Check(NewsManager.Instance.ArticleVersion==before+1,"repeat publish cannot bypass cooldown before UI refresh");
        }
    }
    private static Vector2 Center(RectTransform r) => RectTransformUtility.WorldToScreenPoint(Camera.main,r.TransformPoint(r.rect.center));
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); var p=new PointerEventData(EventSystem.current) {position=Center((RectTransform)button.transform)};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"raycast reaches "+button.name+"; hits="+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name))+"; active="+button.gameObject.activeInHierarchy);
        ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Audit(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        var problems=new List<string>();
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            if (text.GetComponentInParent<ScrollRect>()!=null||text.GetComponentInParent<TMP_InputField>()!=null)
            { continue; }
            text.ForceMeshUpdate(); if (text.isTextTruncated||text.isTextOverflowing) { problems.Add(text.name+" / "+text.text+" / "+text.rectTransform.rect); }
        }
        File.WriteAllLines(Path.Combine(output,"layout-"+GameLocale.Code+".txt"),problems);
        // Capture evidence before reporting layout failures; every reported failure must be corrected.
        Check(problems.Count==0,"visible fixed labels fit "+GameLocale.Code+": "+string.Join(" | ",problems));
    }
    private static void SaveChecks()
    {
        if (mode==6)
        {
            SessionStore.Restore(File.ReadAllText("Validation/Settings/TabletKitSave01/session-v2.json"));
            Check(tablet.events.records.Fills.Count>0&&tablet.events.records.Fills[0].Time.StartsWith("DAY "),"cold process restores dated journal");
            Check(tablet.events.records.Fills[0].Stock==CorporateEconomy.Instance.PlayerCompany,"cold restored journal uses actual stock identity");
            Check(errors==0,"cold restore no runtime errors"); Finish(null); return;
        }
        SessionStore.Restore(File.ReadAllText("Validation/Settings/CompanyListing02/session-v2.json"));
        var journal=tablet.events.records; var sample=CorporateEconomy.Instance.PlayerCompany;
        string stamp=DeviceMarketJournal.FullStamp(); journal.Fills.Insert(0,new DeviceMarketJournal.Fill {Stock=sample,Time=stamp,Price=sample.currentPrice,Quantity=2,Buy=true});
        SessionStore.Save(); string saved=File.ReadAllText(SessionStore.PathName); journal.Fills.Clear(); SessionStore.Restore(saved);
        Check(journal.Fills.Count>0&&journal.Fills[0].Time==stamp&&journal.Fills[0].Stock==CorporateEconomy.Instance.PlayerCompany,"journal game date and stock identity survive checkpoint reload");
        Check(errors==0,"save reload no runtime errors"); Finish(null);
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
        { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); } PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class TabletKitProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { TabletKitValidation.Step(); }
}
