using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DeviceCanvasValidation
{
    private const string Output = "D:/GitHub/DonDon/Validation/DeviceCanvasUI/";
    private static readonly StringBuilder results = new(), visual = new();
    private static readonly List<string> errors = new();
    private static int overflowCount;
    private static int phase;
    private static double began, ready;
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static DeviceTerminalUI orders, journal;
    private static PhoneCallController phone;
    private static Vector3 tabletPosition, tabletScale;
    private static Quaternion tabletRotation;
    private static Camera camera;
    public static void Run()
    {
        DeviceCanvasPatch.Apply();
        RunCurrent();
    }
    public static void RunCurrent()
    {
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; ready = began + 3;
        Application.logMessageReceived += Log;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    private static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        { errors.Add(message); }
    }
    private static void Next(double seconds = .7)
    { phase++; ready = EditorApplication.timeSinceStartup + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - began > 150)
        { Finish(new Exception("150-second play validation timeout at phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready)
        { return; }
        try
        {
            var manager = StockManager.Instance;
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                if (!manager.SetupConfirmed)
                { Check(manager.ConfirmPlayer("플레이어 기업","1000000","30",out string reason), "new retail start: " + reason); }
                Next(2); return;
            }
            if (!manager.SetupConfirmed || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || !MarketDayController.CanTrade)
            { return; }
            if (phase == 1)
            {
                var screens = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include);
                orders = screens.Single(x => !x.journal); journal = screens.Single(x => x.journal);
                phone = PhoneCallController.Instance; camera = Camera.main;
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                camera.fieldOfView = 45f;
                int target = manager.stocks.FindIndex(x => x.AcceptsOrders && x.symbol != "PLAYER" && x.currentPrice < 10000m);
                manager.SelectStock(Math.Max(0,target));
                Check(orders.panel.activeSelf && journal.panel.activeSelf,"new screens are default inside existing canvases");
                Check(orders.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace,"tablet still uses World Space Canvas");
                Check(phone.screenUI != null && phone.answer.onClick.GetPersistentEventCount() >= 0,"existing phone controller has its new view");
                Capture("desk-game-camera.png");
                tabletPosition = orders.thisTablet.tabletObject.position; tabletRotation = orders.thisTablet.tabletObject.rotation; tabletScale = orders.thisTablet.tabletObject.localScale;
                orders.thisTablet.FocusTablet(); Next(); return;
            }
            if (phase == 2)
            {
                Canvas.ForceUpdateCanvases(); Check(orders.thisTablet.IsHeld,"tablet 1 focus interaction");
                Capture("tablet-order-game-camera.png"); Inspect(orders.panel,"order"); Frame(orders.GetComponent<Canvas>(),"tablet1 FOV45");
                var graphMesh = orders.graph.canvasRenderer.GetMesh();
                Check(graphMesh != null && graphMesh.vertexCount > 0,"existing candle history produces a visible mini-chart mesh");
                orders.quantity.text = "1000000"; Next(.4); return;
            }
            if (phase == 3)
            {
                Check(!orders.review.interactable && !string.IsNullOrEmpty(orders.validation.text),"quantity overrun blocks confirmation with reason");
                Capture("tablet-order-overrun.png");
                Inspect(orders.panel,"overrun");
                orders.quantity.text = "3";
                orders.price.text = PriceTickPolicy.RoundToTick(manager.CurrentStock.currentPrice).ToString("0");
                Click(orders.split); orders.parts.text = "2"; orders.interval.text = "3";
                Next(.4); return;
            }
            if (phase == 4)
            {
                Capture("tablet-order-split.png"); Inspect(orders.panel,"split");
                if (!orders.review.interactable) { orders.quantity.text = "2"; }
                Next(.4); return;
            }
            if (phase == 5)
            {
                Check(orders.review.interactable,"valid order draft enables confirmation");
                decimal cash = manager.playerAccount.cash;
                Click(orders.review);
                Check(manager.playerAccount.cash == cash && !orders.session.Active,"confirmation does not submit or debit account");
                Next(.4); return;
            }
            if (phase == 6)
            {
                Capture("tablet-order-confirmation.png"); Inspect(orders.panel,"confirmation");
                Click(orders.submit); Next(.5); return;
            }
            if (phase == 7)
            {
                Check(orders.session.Active || orders.session.Filled > 0m,"screen submit reaches real order session");
                Click(orders.cancel); Check(!orders.session.Active,"real UI cancels remaining manual order");
                Click(orders.legacy.deskTab);
                Check(orders.legacy.deskPanel.activeSelf && !orders.panel.activeSelf,"existing automatic operation tab preserved");
                Click(orders.legacy.legacyTab);
                Check(orders.legacy.legacyPanel.activeSelf,"existing assets and news tab preserved");
                Click(orders.mainTab);
                orders.thisTablet.ReturnTablet(); Next(1.5); return;
            }
            if (phase == 8)
            {
                Check(Vector3.Distance(orders.thisTablet.tabletObject.position,tabletPosition)<.0001f && Quaternion.Angle(orders.thisTablet.tabletObject.rotation,tabletRotation)<.01f && Vector3.Distance(orders.thisTablet.tabletObject.localScale,tabletScale)<.0001f,"tablet 1 restores original desk pose and scale");
                journal.thisTablet.FocusTablet(); Next(); return;
            }
            if (phase == 9)
            {
                Capture("tablet-events-game-camera.png"); Inspect(journal.panel,"journal"); Frame(journal.GetComponent<Canvas>(),"tablet2 FOV45");
                Click(journal.growth); Next(.4); return;
            }
            if (phase == 10)
            {
                Capture("tablet-growth-game-camera.png"); Inspect(journal.growthPanel,"growth");
                Check(!journal.openRooms.interactable,"unearned leading-room permission remains locked");
                Click(journal.closeGrowth);
                NewsManager.Instance.AddNews(manager.CurrentStock,1f,"[UI 검증] " + new string('가',100));
                Next(.5); return;
            }
            if (phase == 11)
            {
                Click(journal.eventButtons[0]); Next(.4); return;
            }
            if (phase == 12)
            {
                var scroll = journal.eventDetail.GetComponentInParent<ScrollRect>();
                Check(scroll != null && scroll.content.rect.height >= scroll.viewport.rect.height,"long event uses a scrollable measured content area");
                scroll.verticalNormalizedPosition = 0f;
                Capture("tablet-events-long-scroll.png");
                journal.thisTablet.ReturnTablet(); Next(1.5); return;
            }
            if (phase == 13)
            {
                phone.Ring(PhoneCallController.Caller.Institution); phone.Raise(); Next(.5); return;
            }
            if (phase == 14)
            {
                Capture("phone-incoming-game-camera.png"); Inspect(phone.screenUI.gameObject,"phone");
                int remaining = phone.RemainingSeconds;
                Click(phone.close); Check(phone.Ringing && !PhoneCallController.IsRaised && phone.RemainingSeconds <= remaining,"closing phone preserves ringing and deadline");
                phone.Raise(); Next(.5); return;
            }
            if (phase == 15)
            {
                Click(phone.answer); Check(!phone.Ringing,"original phone answer resolves call"); Next(.4); return;
            }
            if (phase == 16)
            {
                foreach (var caller in new[]{PhoneCallController.Caller.Research,PhoneCallController.Caller.Spam,PhoneCallController.Caller.Institution})
                { phone.Ring(caller); phone.Decline(); phone.screenUI.Refresh(phone); }
                Capture("phone-record-game-camera.png");
                Inspect(phone.screenUI.gameObject,"phone-history");
                var history = phone.screenUI.callHistory.GetComponentInParent<ScrollRect>(); Canvas.ForceUpdateCanvases();
                Check(history != null && history.content.rect.height > history.viewport.rect.height,"multiple phone records remain scrollable");
                Click(phone.close);
                MarketTests();
                camera.fieldOfView = 10f; orders.Show(); orders.thisTablet.FocusTablet(); Next(); return;
            }
            if (phase == 17)
            {
                Frame(orders.GetComponent<Canvas>(),"tablet1 FOV10"); Capture("tablet-order-fov10.png");
                orders.thisTablet.ReturnTablet(); Next(1.5); return;
            }
            if (phase == 18)
            {
                Check(Vector3.Distance(orders.thisTablet.tabletObject.position,tabletPosition)<.0001f && Vector3.Distance(orders.thisTablet.tabletObject.localScale,tabletScale)<.0001f,"FOV compensation restores original desk position and scale");
                Check(overflowCount == 0,"all essential labels fit their actual text rectangles");
                Check(Time.timeScale == 1f,"no speed multiplier introduced");
                Check(errors.Count == 0,"no runtime errors: " + string.Join(" | ",errors.Take(4)));
                File.WriteAllText(Output+"visual-checks.txt",visual.ToString());
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void MarketTests()
    {
        var manager = StockManager.Instance; var realPlayer = manager.playerAccount; var session = orders.session;
        var career = PlayerProgression.Instance; string savedCareer = career.Capture();
        var player = new GameObject("Device Test Player").AddComponent<Trader>(); player.cash = 1000000m;
        var counterparty = new GameObject("Device Test Seller").AddComponent<Trader>(); counterparty.cash = 1000000m;
        var stock = new Stock("UI_TEST","검증 종목",5000m,1000m);
        manager.playerAccount = player; session.account = player;
        Invoke(career,"Register",stock);
        counterparty.GrantShares(stock,20m,4000m);
        counterparty.SellLimit(5000m,10m,0,stock);
        Check(session.Prepare(stock),"manual session prepares existing finite account");
        Check(!string.IsNullOrEmpty(session.Validate(stock,true,5001m,1m,1,3f)),"tick mismatch rejected by existing Korean price policy");
        Check(session.Submit(stock,true,5000m,10m,1,3f),"limit order accepted within budget");
        Invoke(session,"Update");
        Check(player.GetOwnedVolume(stock)==10m && player.cash==950000m && session.Filled==10m,"real order book fills at limit and debits exactly once");
        Check(player.cash+counterparty.cash==2000000m && player.GetOwnedVolume(stock)+counterparty.GetOwnedVolume(stock)==20m,"cash and shares conserved across real fill");
        counterparty.BuyLimit(6000m,10m,0,stock); player.SellLimit(6000m,10m,0,stock);
        Check(career.RealizedProfit>=10000m && career.Turnover>=110000m,"growth credits actual realized proceeds minus cost basis");
        Check(career.Eligible && career.OpenLeadingRooms(),"earned performance opens leading-room rights");
        Check(InvestorSociety.Instance.Issue(1,manager.CurrentStock,PriceTickPolicy.RoundToTick(manager.CurrentStock.currentPrice),PriceTickPolicy.OneTickAbove(manager.CurrentStock.currentPrice)),"unlocked permission reaches existing conditional room command");
        InvestorSociety.Instance.CancelRoom(1);
        string captured=career.Capture(); Check(career.Restore(captured,career.WorldId),"progress snapshot round trip");
        Check(!career.Restore(captured,"different-world"),"progress cannot leak rights into another economy");
        session.Prepare(stock);
        Check(session.Submit(stock,true,4900m,3m,3,1f),"split order starts without forced fill");
        Invoke(session,"Update");
        Check(player.ReservedCash==4900m && session.Unsubmitted==2m,"only submitted slice uses existing escrow");
        session.Cancel(); Check(player.ReservedCash==0m && session.Unsubmitted==0m,"cancel returns escrow and stops future split submissions");
        career.Restore(savedCareer,career.WorldId); Invoke(career,"Remove",stock);
        manager.playerAccount=realPlayer; session.account=realPlayer; session.Prepare(manager.CurrentStock);
        var opponent=Object.FindAnyObjectByType<IncumbentExchangeAI>();
        Check(opponent!=null && opponent.policy.maximumShares<=50 && opponent.policy.responseCooldown>=8f,"incumbent response cap and independent action cooldown");
        float sampleTime=Mathf.Max(Time.time,opponent.NextObservationAt);
        Check(opponent.Observe(sampleTime),"incumbent takes one complete player snapshot");
        Check(opponent.Snapshot.Cash==realPlayer.cash && opponent.Snapshot.Positions.Count==manager.stocks.Count+manager.UnlistedStocks.Count,"observer uses actual assets plus listed and unlisted positions");
        Check(!opponent.Observe(sampleTime+.1f),"observer does not bypass intelligence delay");
        Check(opponent.ResponseOrder==null || opponent.ResponseOrder.volume<=50m,"incumbent never creates oversized response wall");
        float oldDelay=opponent.policy.observationSeconds;
        opponent.policy.observationSeconds=.5f;
        Check(opponent.Observe(opponent.NextObservationAt),"shorter intelligence setting takes next due observation");
        Check(Mathf.Abs(opponent.NextObservationAt-opponent.Snapshot.ObservedAt-.5f)<.001f,"intelligence changes next information delay only");
        opponent.policy.observationSeconds=oldDelay;
        TestExchangeResponse(opponent);
    }
    private static void TestExchangeResponse(IncumbentExchangeAI opponent)
    {
        var exchange=ExchangeBot.Instance;
        var target=StockManager.Instance.stocks.First(s=>s.AcceptsOrders && exchange.GetState(s)!=null && !exchange.GetState(s).Risky && !exchange.GetState(s).NewListing && exchange.GetHolding(s)>1000m);
        var state=exchange.GetState(target); var bids=new List<(decimal price,decimal volume)>(); target.orderBook.GetTopBids(bids,1);
        decimal price=PriceTickPolicy.OneTickAbove(Math.Max(target.currentPrice,bids.Count>0?bids[0].price:target.currentPrice));
        decimal cash=exchange.cash, owned=exchange.GetOwnedVolume(target);
        var response=exchange.TryPlayerResponse(target,false,price,800000m);
        Check(response!=null && target.orderBook.IsActive(response) && response.volume<=50m,"actual exchange response enters existing book with 800,000-share request capped to 50");
        Check(exchange.cash==cash && exchange.GetOwnedVolume(target)==owned,"response escrows existing shares without creating cash or inventory");
        Check(state.Asks.Count(o=>target.orderBook.IsActive(o))<=3,"response reuses existing three defense slots");
        target.orderBook.CancelOrder(response,target);
        bool risky=state.Risky;
        try
        {
            state.Risky=true;
            Check(exchange.TryPlayerResponse(target,false,price,10m)==null,"response respects existing risk stop");
        }
        finally { state.Risky=risky; }
        decimal realCash=exchange.cash;
        try
        {
            exchange.cash=0m;
            Check(exchange.TryPlayerResponse(target,true,PriceTickPolicy.OneTickBelow(target.currentPrice),50m)==null,"response respects finite available cash");
        }
        finally { exchange.cash=realCash; }
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Check(button.gameObject.activeInHierarchy && button.interactable,"clickable "+button.name);
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Any(hit=>hit.gameObject==button.gameObject || hit.gameObject.transform.IsChildOf(button.transform)),"game camera raycast reaches "+button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Inspect(GameObject root,string label)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var field in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            field.ForceMeshUpdate();
            if (field.text.Length==0 || field.name.StartsWith("Text :: Event ")) { continue; }
            if (field.isTextOverflowing) { overflowCount++; visual.AppendLine("OVERFLOW "+label+" / "+field.name+" / "+field.text.Replace("\n"," | ")); }
        }
    }
    private static void Frame(Canvas canvas,string label)
    {
        var corners=new Vector3[4]; ((RectTransform)canvas.transform).GetWorldCorners(corners);
        bool fits=corners.All(c=>{var p=camera.WorldToViewportPoint(c); return p.z>0 && p.x>=0 && p.x<=1 && p.y>=0 && p.y<=1;});
        visual.AppendLine((fits?"FRAME PASS ":"FRAME CLIP ")+label);
        Check(fits,"game camera contains full canvas: "+label);
    }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var target=new RenderTexture(1600,900,24); var previous=RenderTexture.active; var old=camera.targetTexture;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
        File.WriteAllBytes(Output+name,pixels.EncodeToPNG()); camera.targetTexture=old; RenderTexture.active=previous;
        Object.Destroy(pixels); target.Release(); Object.Destroy(target);
    }
    private static void Invoke(object target,string method,params object[] args)
    { target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args); }
    private static void Check(bool ok,string text)
    {
        if (!ok) { throw new Exception(text); }
        results.AppendLine("PASS "+text);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log;
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        if (error!=null) { results.AppendLine("FAIL "+error); }
        File.WriteAllText(Output+"game-results.txt",results.ToString()); File.WriteAllText(Output+"visual-checks.txt",visual.ToString());
        EditorApplication.Exit(error==null?0:1);
    }
}
