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

/// <summary>One bounded, normal-speed play session. Financial fixtures use real finite accounts and the real order book.</summary>
public static class OnboardingValidation
{
    private const string Output = "Validation/Onboarding/";
    private static readonly StringBuilder results = new(), visual = new();
    private static readonly List<string> errors = new();
    private static int phase, overflow;
    private static double began, ready, naturalStart;
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static PlayerCompanySetupUI setup;
    private static DeviceTerminalUI orders, journal;
    private static LeadingRoomUI rooms;
    private static InvestorJourneyUI journey;
    private static NpcLeadingRoom npc;
    private static Camera camera;
    private static Quaternion deskRotation;
    private static decimal cashBefore, turnoverBefore;
    private static int submittedBefore;
    private static Stock signalStock;
    private static decimal signalPrice;
    private static Trader fixtureSeller;
    private static Stock fixtureStock;

    public static void Run()
    {
        OnboardingScenePatch.Apply();
        RunCurrent();
    }
    public static void RunCurrent()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; ready = began + 3;
        Application.logMessageReceived += Log; EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    public static void Build()
    {
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes 1/TradeScene.unity" },
                locationPathName = Output + "Player/DonDon.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            var result = report.summary;
            File.WriteAllText(Output + "build-results.txt",result.result + "\nErrors: " + result.totalErrors + "\nWarnings: " + result.totalWarnings + "\nBytes: " + result.totalSize + "\nSeconds: " + result.totalTime.TotalSeconds + "\nOutput: " + result.outputPath);
            EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error)
        { File.WriteAllText(Output + "build-results.txt",error.ToString()); EditorApplication.Exit(1); }
    }
    private static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        { errors.Add(text); }
    }
    private static void Next(double seconds = .8)
    { phase++; ready = EditorApplication.timeSinceStartup + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - began > 140)
        { Finish(new Exception("140-second bounded timeout at phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready)
        { return; }
        try
        {
            var manager = StockManager.Instance;
            var society = InvestorSociety.Instance;
            var career = PlayerProgression.Instance;
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                camera = Camera.main; deskRotation = camera.transform.rotation; camera.fieldOfView = 45f;
                setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                rooms = Object.FindAnyObjectByType<LeadingRoomUI>(); npc = NpcLeadingRoom.Instance;
                journey = Object.FindAnyObjectByType<InvestorJourneyUI>();
                var terminals = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include);
                orders = terminals.Single(s => !s.journal); journal = terminals.Single(s => s.journal);
                Check(setup != null && setup.retailIntroduction.activeSelf && !setup.companySetup.activeSelf,"retail opening uses existing setup Canvas; legacy company form retained");
                Check(setup.startBalance.text == manager.economy.retailStartingCash.ToString("N0") + "원","opening balance comes from economy data");
                Check(!manager.SetupConfirmed && !career.CanOperateRooms,"market and owner rights not granted before start");
                Inspect(setup.gameObject,"opening"); Capture("opening-game-camera.png",true);
                Click(setup.controlsToggle); Next(); return;
            }
            if (phase == 1)
            {
                Check(setup.controlsPanel.activeSelf,"controls button reveals input guide");
                Inspect(setup.controlsPanel,"controls"); Capture("opening-controls.png",true);
                Click(setup.controlsToggle); Check(!setup.controlsPanel.activeSelf,"controls panel closes without starting market");
                Click(setup.confirm); Check((manager.SetupInProgress || manager.SetupConfirmed) && npc.JoinRequested,"start button begins real economy initialization and requests NPC room membership");
                Next(2); return;
            }
            if (!MarketDayController.CanTrade || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
            { return; }
            if (phase == 2)
            {
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>())
                { mouse.enabled = false; }
                Time.timeScale = 1f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                Check(manager.SetupConfirmed && !setup.gameObject.activeSelf,"original startup overlay closes after confirmed economy initialization");
                naturalStart = Time.time; Next(1); return;
            }
            if (phase == 3)
            {
                if (!npc.PlayerJoined || !npc.HasSignal || npc.Current.Buy <= 0m || Time.time - naturalStart < 25f || society.GetRoom(NpcLeadingRoom.RoomId).Members.Count < 2)
                { return; }
                var room = society.GetRoom(NpcLeadingRoom.RoomId);
                Check(MarketDayController.Instance.Bots.Contains(npc.Leader) && !npc.Leader.IsExchangeParticipant && !npc.Leader.IsCompanyOwner,"operator is an existing finite ordinary investor");
                Check(npc.Leader.Social.Psychology.Room == room.Id && room.OwnerBotId == npc.Leader.Social.Id,"room owner and membership refer to the same real bot");
                Check(rooms.chat.SelectedRoom == room.Id && npc.PlayerJoined,"new player automatically joins bot-led room");
                Check(room.Count >= 2 && room.Members.Count > 1,"NPC room publishes real observations and attracts autonomous members");
                Check(Enumerable.Range(1,3).All(id => !society.IsRoomOpen(id)),"three player rooms remain locked until opened");
                Check(!society.Issue(1,manager.CurrentStock,500m,600m),"player cannot issue own-room commands early");
                Check(career.Turnover == 0m && career.RealizedProfit == 0m && !career.CanOperateRooms,"autonomous NPC trading never awards player progress");
                int observed = npc.Observations;
                npc.Observe(npc.NextObservationAt - .01f);
                Check(npc.Observations == observed,"NPC observer cannot scan before the configured interval");
                visual.AppendLine($"NATURAL before any player order: {Time.time-naturalStart:0.0}s, members={room.Members.Count}, messages={room.Count}, leader fills={npc.FillCount}, leader turnover={npc.Turnover}, leader P/L={npc.RealizedProfit}, observations={npc.Observations}");
                for (int i = 0; i < room.Count; i++)
                { visual.AppendLine("ROOM " + room.Get(i).Nickname + ": " + room.Get(i).Text); }
                var observation = Enumerable.Range(0,room.Count).Select(room.Get).Last(m => m.Context.TargetId == npc.Leader.Social.Id);
                var listener = MarketDayController.Instance.Bots.First(b => b != npc.Leader && b.Social != null && !b.IsExchangeParticipant);
                float trust = listener.Social.Psychology.Trust;
                Check(!society.Observe(listener,observation,room.Id,Time.time,0) && trust == listener.Social.Psychology.Trust,"NPC testimony is not attributed to player trust");
                Check(listener.Social.Psychology.LastRead.Length > room.Id,"per-channel witness cursors include NPC room");
                var observer = Object.FindAnyObjectByType<IncumbentExchangeAI>();
                observer.Observe(Mathf.Max(Time.time,observer.NextObservationAt));
                Check(observer.Snapshot.Members == 0 && observer.Snapshot.RoomMembers.Count == 0,"incumbent does not count NPC members as player's faction");
                Inspect(journey.card,"journey"); Capture("desk-started.png",true);
                LookAtChat(); Next(); return;
            }
            if (phase == 4)
            {
                Capture("npc-room-game-camera.png"); Inspect(rooms.npcControls,"NPC footer");
                camera.fieldOfView = 28f; Capture("npc-room-zoom-game-camera.png"); camera.fieldOfView = 45f;
                signalStock = npc.Current.Stock; signalPrice = npc.Current.Buy;
                cashBefore = manager.playerAccount.cash; submittedBefore = orders.session.Number;
                Click(rooms.npcBuy); Next(1); return;
            }
            if (phase == 5)
            {
                Check(orders.thisTablet.IsHeld && manager.CurrentStock == signalStock,"NPC review focuses original tablet and selects matching stock");
                Check(orders.price.text == signalPrice.ToString("0") && orders.quantity.text == "1","draft preserves integer-won observed price and defaults to one share");
                Check(manager.playerAccount.cash == cashBefore && orders.session.Number == submittedBefore && !orders.session.Active,"importing signal never submits or debits cash");
                Inspect(orders.panel,"signal draft"); Capture("npc-order-draft.png");
                // A low non-crossing quote proves waiting/cancellation without forcing a market fill.
                orders.price.text = "1"; orders.quantity.text = "1"; Next(.5); return;
            }
            if (phase == 6)
            {
                Click(orders.review); Check(!orders.session.Active && manager.playerAccount.cash == cashBefore,"review screen still does not submit");
                Next(); return;
            }
            if (phase == 7)
            {
                Click(orders.submit); Next(); return;
            }
            if (phase == 8)
            {
                Check(orders.session.Number == submittedBefore + 1 && orders.session.Active && manager.playerAccount.ReservedCash == 1m,"final submit creates actual pending order and escrow");
                Check(!orders.LoadSignalDraft(signalStock,true,signalPrice,"test"),"new signal cannot overwrite an active manual order");
                npc.LeavePlayer(); Check(orders.session.Active && !npc.PlayerJoined && rooms.chat.SelectedRoom == 0,"leaving bot room does not cancel user's pending order");
                rooms.ShowNpc(); Check((int)Property(rooms.chat,"MessageCount") == 0,"NPC history is hidden after leaving");
                Check(npc.JoinPlayer() && (int)Property(rooms.chat,"MessageCount") > 0,"rejoining restores original room history");
                Check(career.Turnover == 0m && career.RealizedProfit == 0m,"unfilled order cannot unlock performance rights");
                Click(orders.cancel); Check(!orders.session.Active && manager.playerAccount.ReservedCash == 0m,"existing cancellation releases only the submitted order");
                FinancialFixture(); journal.FocusGrowth(); Next(1.5); return;
            }
            if (phase == 9)
            {
                Check(journal.thisTablet.IsHeld && !orders.thisTablet.IsHeld,"growth navigation safely returns paired tablet before raising second tablet");
                Check(career.Eligible && !career.CanOperateRooms && journal.openRooms.interactable,"enough available cash enables explicit room opening");
                Inspect(journal.growthPanel,"eligible growth"); Capture("earned-room-unlock.png");
                Click(journal.openRooms); Next(); return;
            }
            if (phase == 10)
            {
                Check(career.CanOperateRooms && Enumerable.Range(1,3).All(society.IsRoomOpen),"paid opening unlocks all three existing independent rooms");
                Check(manager.playerAccount.cash == openingCashBefore - career.OpeningCost,"opening debits the cost exactly once");
                Check(npc.PlayerJoined && society.GetRoom(4).OwnerBotId == npc.Leader.Social.Id,"opening player rooms retains independent NPC membership");
                Check(!society.Issue(4,manager.CurrentStock,500m,600m),"player cannot replace NPC operator commands");
                int hostOrders = CountOwnerOrders(npc.Leader);
                society.CancelRoom(4);
                Check(CountOwnerOrders(npc.Leader) == hostOrders,"player cancellation cannot remove NPC operator orders");
                var stock = manager.CurrentStock;
                decimal buy = PriceTickPolicy.RoundToTick(stock.currentPrice), sell = PriceTickPolicy.OneTickAbove(buy);
                Check(society.Issue(1,stock,buy,sell) && society.Issue(2,stock,buy,sell),"earned rights route to original conditional order system");
                var commandTwo = society.GetRoom(2).Command; society.CancelRoom(1);
                Check(!commandTwo.Canceled,"canceling player room one leaves room two independent");
                society.CancelRoom(2); journal.thisTablet.ReturnTablet(); camera.transform.rotation = deskRotation;
                Next(1.5); return;
            }
            if (phase == 11)
            {
                Check(journey.Step == 4,"guidance follows actual growth to room operator");
                Click(journey.dismiss); Next(.7); return;
            }
            if (phase == 12)
            {
                Check(!journey.card.activeSelf && journey.reopen.gameObject.activeSelf,"guidance is dismissible without interrupting market");
                Click(journey.reopen); Next(.7); return;
            }
            if (phase == 13)
            {
                Check(journey.card.activeSelf && Time.timeScale == 1f,"guidance reopens and simulation remains normal speed");
                Check(society.MaxBotsProcessedPerTick <= society.settings.botsPerTick,"social updates retain existing per-tick budget");
                Check(society.Rooms.All(r => r.Count <= 128),"room histories retain bounded rings");
                Inspect(journey.card,"owner guidance"); Capture("room-operator-guide.png",true);
                Check(overflow == 0,"new opening, guide, room and tablet text have no TMP overflow");
                Check(errors.Count == 0,"no Unity runtime errors: " + string.Join(" | ",errors));
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static decimal openingCashBefore;
    private static void FinancialFixture()
    {
        var manager = StockManager.Instance; var player = manager.playerAccount; var career = PlayerProgression.Instance;
        turnoverBefore = career.Turnover;
        fixtureStock = new Stock("ONBOARD_TEST","격리 체결 검증",4000m,1000m);
        fixtureSeller = new GameObject("Onboarding Finite Counterparty").AddComponent<Trader>(); fixtureSeller.cash = 1000000m;
        fixtureSeller.GrantShares(fixtureStock,10m,4000m); Invoke(career,"Register",fixtureStock);
        decimal combined = player.cash + fixtureSeller.cash;
        fixtureSeller.SellLimit(4000m,10m,0,fixtureStock);
        Check(orders.session.Prepare(fixtureStock) && orders.session.Submit(fixtureStock,true,4000m,10m,1,3f),"isolated finite fixture uses original manual order session");
        Invoke(orders.session,"Update"); Invoke(orders.session,"Update");
        Check(player.GetOwnedVolume(fixtureStock) == 10m && career.RealizedProfit == 0m,"real purchase alone grants no realized profit");
        fixtureSeller.BuyLimit(6000m,10m,0,fixtureStock); player.SellLimit(6000m,10m,0,fixtureStock);
        Check(career.RealizedProfit == 20000m && career.Turnover - turnoverBefore == 100000m,"real cost basis and fills produce exactly 100000 turnover and 20000 realized profit");
        Check(player.cash + fixtureSeller.cash == combined && player.GetOwnedVolume(fixtureStock) + fixtureSeller.GetOwnedVolume(fixtureStock) == 10m,"fixture conserves cash and shares");
        Invoke(career,"Remove",fixtureStock); orders.session.Prepare(manager.CurrentStock);
        // 검증 계좌가 낼 수 있는 금액으로 개설 비용을 낮춘다. 비용을 실제로 내는지는 그대로 확인한다.
        career.rules.leadingOpeningCost = 100000; openingCashBefore = player.cash;
    }
    private static int CountOwnerOrders(TradeBot owner)
    {
        var copy = new List<Order>(); int count = 0;
        foreach (var stock in StockManager.Instance.stocks)
        { stock.orderBook.CopyOwnerOrders(owner,copy); count += copy.Count; }
        return count;
    }
    private static object Property(object target,string name) => target.GetType().GetProperty(name,BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Invoke(object target,string name,params object[] arguments)
    { target.GetType().GetMethod(name,BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target,arguments); }
    private static void LookAtChat()
    {
        var rect = (RectTransform)rooms.transform;
        camera.transform.LookAt(rect.TransformPoint(rect.rect.center));
        camera.fieldOfView = 45f;
        visual.AppendLine("VIEW chat camera position preserved; rotation=" + camera.transform.eulerAngles + ", FOV=45");
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        Check(button.gameObject.activeInHierarchy && button.interactable,"clickable " + button.name);
        var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"topmost raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Inspect(GameObject root,string label)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var field in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            field.ForceMeshUpdate();
            if (string.IsNullOrEmpty(field.text))
            { continue; }
            visual.AppendLine((field.isTextOverflowing ? "OVERFLOW " : "TEXT PASS ") + label + " / " + field.name + " / size " + field.fontSize + " / " + field.text.Replace("\n"," | "));
            if (field.isTextOverflowing)
            { overflow++; }
        }
    }
    private static void Capture(string name,bool overlays = false)
    {
        // Camera.Render omits screen overlays; bind them to the same camera only for this offscreen capture, then restore.
        var changed = new List<(Canvas Canvas, Camera Camera, float Distance)>();
        if (overlays)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                { continue; }
                changed.Add((canvas,canvas.worldCamera,canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
            }
        }
        var target = new RenderTexture(1600,900,24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1600,900,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
        File.WriteAllBytes(Output + name,pixels.EncodeToPNG()); camera.targetTexture = old; RenderTexture.active = previous;
        Object.Destroy(pixels); target.Release(); Object.Destroy(target);
        foreach (var saved in changed)
        { saved.Canvas.renderMode = RenderMode.ScreenSpaceOverlay; saved.Canvas.worldCamera = saved.Camera; saved.Canvas.planeDistance = saved.Distance; }
        Canvas.ForceUpdateCanvases();
    }
    private static void Check(bool pass,string description)
    {
        if (!pass)
        { throw new Exception(description); }
        results.AppendLine("PASS " + description);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        if (error != null)
        { results.AppendLine("FAIL phase " + phase + " " + error); }
        File.WriteAllText(Output + "game-results.txt",results.ToString()); File.WriteAllText(Output + "visual-checks.txt",visual.ToString());
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
