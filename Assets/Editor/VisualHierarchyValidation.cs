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

public static class VisualHierarchyValidation
{
    private const string Output = "D:/GitHub/DonDon/Validation/VisualHierarchy/";
    private static readonly StringBuilder results = new(), visual = new();
    private static readonly List<string> errors = new();
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static double began, ready;
    private static int phase, overflow, hotBuilds, coldBuilds, particles;
    private static Camera camera;
    private static PlayerCompanySetupUI setup;
    private static TabletGroupChatUI chat;
    private static InvestorProfileUI profile;
    private static DeviceTerminalUI journal;
    private static Vector3[] heatVertices;
    private static BotSocialState fixture;
    public static void Run()
    {
        VisualHierarchyPatch.Apply(); PsychologyChecks();
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; ready = began + 3;
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{"Assets/Scenes 1/TradeScene.unity"}, locationPathName = Output + "Player/DonDon.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        var summary = report.summary;
        File.WriteAllText(Output + "build-results.txt",summary.result + "\nErrors: " + summary.totalErrors + "\nWarnings: " + summary.totalWarnings + "\nSeconds: " + summary.totalTime.TotalSeconds + "\nOutput: " + summary.outputPath);
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
    private static void PsychologyChecks()
    {
        var settings = new InvestorSocietySettings(); fixture = new BotSocialState(900001,73129,"Patient"); var mind = fixture.Psychology;
        for (int i = 1; i <= 4; i++) { mind.Record(new InvestorExperience(i,i,1,"TEST",100m,200m,i),settings); }
        Check(mind.Trust == 140f,"actual psychology accumulation permits trust above 100");
        for (int i = 5; i <= 8; i++) { mind.Record(new InvestorExperience(i,i,1,"TEST",100m,50m,i),settings); }
        Check(mind.Trust == 140f && mind.Betrayal == 180f,"trust and betrayal independently exceed 100 at the same time");
        Check(!mind.Record(new InvestorExperience(8,8,1,"TEST",100m,50m,9),settings) && mind.Betrayal == 180f,"repeated direct event cannot accumulate extra heat");
        float memory = mind.BetrayalMemory; mind.Decay(400f,settings);
        Check(mind.Trust > 100f && mind.Trust < 140f && mind.Betrayal > 100f && mind.Betrayal < 180f && mind.BetrayalMemory == memory,"overheated relationships cool while memory persists");
        float betrayal = mind.Betrayal; mind.RecallBetrayal(); Check(mind.Betrayal > betrayal,"recalling betrayal also preserves overflow");
        Check(mind.Greed <= 100f && mind.Fear <= 100f && mind.Fomo <= 100f,"other psychological indices retain 0..100 limits");
        Check(mind.Acceptance >= 0f && mind.Acceptance <= 1f && mind.JoinChance >= 0f && mind.JoinChance <= 1f && mind.LeaveChance >= 0f && mind.LeaveChance <= 1f,"decisions remain valid probabilities with overheated inputs");
        Check(Mathf.Clamp01(1f-Mathf.Clamp01(mind.Trust/100f)) >= 0f,"high trust cannot produce negative boredom");
        Check(TerminalGlyph.BandColor(0) == TerminalGlyph.BandColor(24) && TerminalGlyph.BandColor(24) != TerminalGlyph.BandColor(25),"white to yellow boundary at 25");
        Check(TerminalGlyph.BandColor(49) != TerminalGlyph.BandColor(50) && TerminalGlyph.BandColor(74) != TerminalGlyph.BandColor(75),"orange and red boundaries at 50 and 75");
        Check(TerminalGlyph.BandName(100) == "높음" && TerminalGlyph.BandName(100.1f) == "과열","heat starts strictly above 100");
    }
    private static void Log(string message,string stack,LogType type)
    { if (type == LogType.Exception || type == LogType.Error) { errors.Add(message); } }
    private static void Next(double seconds = .8) { phase++; ready = EditorApplication.timeSinceStartup + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup-began > 100)
        { Finish(new Exception("100-second timeout at phase " + phase)); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready)
        { return; }
        try
        {
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                camera = Camera.main; camera.fieldOfView = 45f; setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                chat = Object.FindObjectsByType<TabletGroupChatUI>().Single(c => c.monitorChat); profile = chat.visualProfile;
                journal = Object.FindObjectsByType<DeviceTerminalUI>().Single(t => t.journal);
                CheckSpriteAssets();
                Check(setup.retailPreview.text.Length < 25 && !setup.retailIntroduction.transform.Find("Panel :: First Journey/Text :: First Journey Steps").gameObject.activeSelf,"opening replaces prose steps with three icons");
                Inspect(setup.gameObject,"opening"); Capture("opening-icons.png",true); Click(setup.confirm); Next(2); return;
            }
            if (!StockManager.Instance.SetupConfirmed || !MarketDayController.CanTrade || !MarketDayController.Instance.Ready || !NpcLeadingRoom.Instance.PlayerJoined)
            { return; }
            if (phase == 1)
            {
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                Time.timeScale = 1f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                var rect = (RectTransform)chat.transform; camera.transform.LookAt(rect.TransformPoint(rect.rect.center)); camera.fieldOfView = 28f;
                particles = Object.FindObjectsByType<ParticleSystem>().Length; Next(2); return;
            }
            if (phase == 2)
            {
                var row = chat.scroll.content.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Button :: Chat Message Row"));
                if (row == null)
                { return; }
                Click(row); Next(1.1); return;
            }
            if (phase == 3)
            {
                var state = MarketDayController.Instance.FindBot(chat.SelectedBotId).Social;
                Check(profile.SelectedBotId == state.Id && profile.meters.activeSelf,"real chat row opens the selected investor's gauges");
                Check(profile.gauges[0].Value == state.Psychology.Trust && profile.gauges[1].Value == state.Psychology.Betrayal,"relationship gauges bind to independent live values");
                Check(!profile.summary.text.Contains("%") && !profile.detailPanel.activeSelf,"precise psychological numbers stay collapsed by default");
                Check(chat.PooledRowCount <= chat.rowPoolLimit,"chat retains bounded reusable rows");
                Check(!chat.rowTemplate.Find("Image :: Stress Background").gameObject.activeSelf,"old rectangular stress background is hidden");
                SnapshotRings(); Inspect(profile.gameObject,"live profile"); Capture("profile-live.png");
                Click(profile.metricButtons[0]); Check(profile.summary.text.Contains("신뢰") && profile.summary.text.Contains("%"),"metric click reveals exact relationship strength");
                Pointer(profile.metricButtons[3],true); Check(profile.summary.text.Contains("공포"),"hover reveals a different index without replacing pinned metric");
                Pointer(profile.metricButtons[3],false); Check(profile.summary.text.Contains("신뢰"),"leaving hover restores pinned metric");
                Click(profile.details); Next(); return;
            }
            if (phase == 4)
            {
                Check(profile.detailPanel.activeSelf && profile.detailText.text.Contains("위험") && profile.detailText.text.Contains("회복"),"original personality values remain available in detail view");
                Inspect(profile.detailPanel,"details"); Capture("profile-details.png"); Click(profile.closeDetails);
                chat.enabled = false; profile.Bind(fixture); Next(1); return;
            }
            if (phase == 5)
            {
                var trust = profile.gauges[0]; var betrayal = profile.gauges[1];
                Check(trust.Overheated && betrayal.Overheated && trust.Value > 100f && betrayal.Value > 100f,"both independent relationship gauges enter heat mode");
                Click(profile.metricButtons[0]); Inspect(profile.gameObject,"overheated profile"); Capture("profile-overheat-a.png");
                heatVertices = trust.canvasRenderer.GetMesh().vertices; hotBuilds = trust.MeshBuilds;
                Check(heatVertices.Length < 1000 && heatVertices.All(v => !float.IsNaN(v.x) && !float.IsInfinity(v.y)),"overheat mesh remains small and finite");
                Check(Object.FindObjectsByType<ParticleSystem>().Length == particles,"overheat adds no particle or steam systems");
                Next(.9); return;
            }
            if (phase == 6)
            {
                var trust = profile.gauges[0]; var current = trust.canvasRenderer.GetMesh().vertices;
                Check(current.Length == heatVertices.Length && current.Where((point,index) => Vector3.Distance(point,heatVertices[index]) > .00001f).Any(),"outer ring gently changes shape over time");
                Check(trust.MeshBuilds > hotBuilds && trust.MeshBuilds-hotBuilds < 20,"heat animation uses a bounded refresh cadence");
                Capture("profile-overheat-b.png"); trust.SetValue(100); profile.gauges[1].SetValue(100); Next(1); return;
            }
            if (phase == 7)
            {
                Check(!profile.gauges[0].Overheated && !profile.gauges[1].Overheated,"returning to 100 ends both heat states");
                coldBuilds = profile.gauges[0].MeshBuilds; Next(1); return;
            }
            if (phase == 8)
            {
                Check(profile.gauges[0].MeshBuilds == coldBuilds,"cooled gauge does not rebuild its mesh every frame");
                profile.Clear(); chat.enabled = true; chat.SelectRoom(0); Next(); return;
            }
            if (phase == 9)
            {
                var rooms = chat.GetComponent<LeadingRoomUI>();
                Check(rooms.lobbyControls.activeSelf && !rooms.playerControls.activeSelf,"public lounge hides inactive order controls behind a progress action");
                Inspect(rooms.lobbyControls,"public lounge actions"); Click(rooms.growth); Next(1.3); return;
            }
            if (phase == 10)
            {
                Check(journal.thisTablet.IsHeld && journal.visualGrowth.overview.activeSelf && !journal.growthText.gameObject.activeSelf,"growth overview uses icons and actual amounts instead of explanation paragraphs");
                Inspect(journal.growthPanel,"growth overview"); Capture("growth-gauges.png"); Click(journal.visualGrowth.details); Next(); return;
            }
            if (phase == 11)
            {
                Check(journal.growthText.gameObject.activeSelf && journal.growthText.text.Contains("미체결"),"growth explanations remain available on request");
                Inspect(journal.growthPanel,"growth conditions"); Click(journal.visualGrowth.details); journal.thisTablet.ReturnTablet(); Next(1.2); return;
            }
            if (phase == 12)
            {
                var journey = Object.FindAnyObjectByType<InvestorJourneyUI>();
                Check(journey.stages.Length == 3 && journey.detail.text.Length < 35,"guidance uses icon stages and one short status");
                Inspect(journey.card,"compact guidance"); Capture("compact-guidance.png",true);
                Check(overflow == 0,"visible essential labels have no TMP overflow");
                Check(Time.timeScale == 1f && errors.Count == 0,"normal speed and zero runtime errors: " + string.Join(" | ",errors)); Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CheckSpriteAssets()
    {
        var glyphs = Object.FindObjectsByType<TerminalGlyph>(FindObjectsInactive.Include);
        var names = new HashSet<string>();
        foreach (var glyph in glyphs)
        {
            var icon = glyph.iconImage;
            if (icon == null || icon.sprite == null) { throw new Exception("Missing generated sprite: " + glyph.name); }
            string path = AssetDatabase.GetAssetPath(icon.sprite); names.Add(path);
            if (!path.StartsWith("Assets/Resources/UI/TradingTerminal/Icons/Icon_") || !path.EndsWith(".png") || icon.type != Image.Type.Simple || !icon.preserveAspect || icon.raycastTarget)
            { throw new Exception("Invalid generated sprite connection: " + glyph.name); }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency || importer.textureCompression != TextureImporterCompression.Uncompressed)
            { throw new Exception("Invalid sprite import: " + path); }
        }
        Check(names.Count == 10,"all ten imagegen PNG assets are used by serialized single-Image references");
        Check(glyphs.All(g => g.GetComponentsInChildren<Image>(true).Length == 1),"each icon uses one sprite Image, no assembled shape objects");
    }
    private static void SnapshotRings()
    {
        var rows = (System.Collections.IEnumerable)typeof(TabletGroupChatUI).GetField("rows",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chat);
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom); bool found = false;
        foreach (var row in rows)
        {
            var type = row.GetType(); long sequence = (long)type.GetField("Sequence").GetValue(row);
            if (sequence <= 0)
            { continue; }
            var ring = (TerminalGlyph)type.GetField("StressRing").GetValue(row);
            for (int i = 0; i < room.Count; i++)
            {
                var message = room.Get(i);
                if (message.Sequence != sequence)
                { continue; }
                Check(ring != null && Mathf.Approximately(ring.Value,message.Stress),"chat stress ring preserves message snapshot " + sequence); found = true;
            }
        }
        Check(found,"at least one pooled message snapshot was checked");
    }
    private static void Pointer(Button button,bool enter)
    {
        var pointer = new PointerEventData(EventSystem.current);
        if (enter) { ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerEnterHandler); }
        else { ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerExitHandler); }
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); Check(button.gameObject.activeInHierarchy && button.interactable,"clickable " + button.name);
        var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"topmost raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Inspect(GameObject root,string label)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) { overflow++; visual.AppendLine("OVERFLOW " + label + " / " + text.name + " / " + text.text.Replace("\n"," | ")); }
        }
        visual.AppendLine("CHECK " + label);
    }
    private static void Capture(string name,bool overlays = false)
    {
        var changed = new List<(Canvas Canvas,Camera Camera,float Distance)>();
        if (overlays)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                { continue; }
                changed.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
            }
        }
        var target = new RenderTexture(1600,900,24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1600,900,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply(); File.WriteAllBytes(Output + name,pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); target.Release(); Object.Destroy(target);
        foreach (var saved in changed) { saved.Canvas.renderMode = RenderMode.ScreenSpaceOverlay; saved.Canvas.worldCamera = saved.Camera; saved.Canvas.planeDistance = saved.Distance; }
        Canvas.ForceUpdateCanvases();
    }
    private static void Check(bool pass,string description)
    {
        if (!pass) { throw new Exception(description); }
        results.AppendLine("PASS " + description);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        if (error != null) { results.AppendLine("FAIL phase " + phase + " " + error); }
        File.WriteAllText(Output + "game-results.txt",results.ToString()); File.WriteAllText(Output + "visual-checks.txt",visual.ToString()); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
