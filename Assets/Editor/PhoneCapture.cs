using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 상호작용 원과 책상 휴대폰(문자)을 찍는 짧은 촬영 도구. 창 없이 돌린다:
//   Unity.exe -batchmode -projectPath <프로젝트> -executeMethod PhoneCapture.RunBatch -logFile <로그>
// 씬을 열어 플레이에 들어가고, 정해 둔 자리들을 겨눠 가며 찍은 뒤 스스로 끝낸다. 씬과 프로젝트 설정은 저장하지 않는다.
// 게임 저장은 Validation/Phone/save 폴더로 돌려 실제 저장 파일을 건드리지 않는다.
// 키를 누를 수 없으므로 게임의 같은 함수(Raise, OpenMessage, LowerAny + Focus)를 직접 부른다. 결과는 Validation/Phone 에 남는다.
public static class PhoneCapture
{
    private const string Folder = "Validation/Phone", ScenePath = "Assets/Scenes 1/TradeScene.unity";
    private const double Budget = 420;
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new();
    private static readonly List<(string name, Action act, double wait)> steps = new();
    private static bool running, prepared, sawPlaying;
    private static double began, ready, waitingSince;
    private static int step;
    private static Camera camera;
    private static bool oldOptions, oldBackground;
    private static EnterPlayModeOptions oldFlags;

    public static void RunBatch()
    {
        try
        {
            Directory.CreateDirectory(Folder); report.Clear(); errors.Clear();
            foreach (string old in Directory.GetFiles(Folder, "*.png")) { File.Delete(old); }
            if (File.Exists(Folder + "/report.txt")) { File.Delete(Folder + "/report.txt"); }
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Folder + "/save");
            foreach (string old in Directory.GetFiles(Folder + "/save")) { File.Delete(old); }
            Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", Path.GetFullPath(Folder + "/save"));
            oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions; oldBackground = Application.runInBackground;
            // 도메인을 다시 올리지 않아야 이 도구의 진행 상태가 플레이 안으로 이어진다.
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Application.runInBackground = true;
            running = true; prepared = false; sawPlaying = false; step = 0; began = EditorApplication.timeSinceStartup; ready = began + 3; waitingSince = 0;
            Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), "STOPPED before play: " + error + Environment.NewLine);
            EditorApplication.Exit(3);
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) { errors.Add(message + "\n" + stack); } }

    private static void Tick()
    {
        if (!running) { return; }
        if (EditorApplication.timeSinceStartup - began > Budget) { Finish(new Exception("timeout at step " + step + " prepared=" + prepared + " playing=" + EditorApplication.isPlaying)); return; }
        if (EditorApplication.isPlaying) { sawPlaying = true; }
        else if (sawPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(new Exception("play mode ended before the capture finished")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready) { return; }
        try
        {
            if (!prepared) { Prepare(); return; }
            if (step >= steps.Count) { Finish(null); return; }
            var next = steps[step++];
            next.act();
            ready = EditorApplication.timeSinceStartup + next.wait;
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>게임이 책상 앞에 앉은 상태가 될 때까지 기다린 뒤, 찍을 차례를 짠다.</summary>
    private static void Prepare()
    {
        ready = EditorApplication.timeSinceStartup + .5;
        if (StockManager.Instance == null) { return; }
        if (!StockManager.Instance.SetupConfirmed)
        {
            if (NewGame.CanBegin) { NewGame.Begin(); }
            ready = EditorApplication.timeSinceStartup + 1; return;
        }
        var views = DeskViewController.Instance; var shell = RoomShell.Instance; var phone = DeskPhone.Instance; var intro = DayIntroOverlay.Instance;
        string waiting = views == null || !views.Ready ? "desk view" : shell == null || !shell.Built ? "room" : RoomShell.Cinematic ? "cinematic" : intro != null && intro.Playing ? "day intro"
            : PauseMenuController.IsOpen ? "menu" : phone == null ? "phone component" : !phone.Ready ? "phone build (unavailable=" + phone.Unavailable + ")" : null;
        if (waiting != null)
        {
            if (waitingSince <= 0) { waitingSince = EditorApplication.timeSinceStartup; }
            if (EditorApplication.timeSinceStartup - waitingSince > 150) { throw new Exception("still waiting for: " + waiting); }
            return;
        }
        prepared = true; camera = views.ViewCamera;
        // 커서를 잠글 수 없으므로 시선을 직접 돌리고, 조준점을 화면 한가운데에 고정한다.
        foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
        Time.timeScale = 1f;
        MouseCameraController.AimOverride = new Vector2(Screen.width * .5f, Screen.height * .5f);
        var markers = Object.FindAnyObjectByType<DeskInteractionMarkers>();
        var hud = markers == null ? null : markers.canvas == null ? null : markers.canvas.rootCanvas;
        var scaler = hud == null ? null : hud.GetComponent<CanvasScaler>();
        report.AppendLine("SCREEN " + Screen.width + "x" + Screen.height + " camera fov " + camera.fieldOfView.ToString("F1") + " aspect " + camera.aspect.ToString("F3") + " position " + camera.transform.position.ToString("F2") + " forward " + camera.transform.forward.ToString("F2"));
        report.AppendLine("HUD " + (hud == null ? "none" : hud.name + " mode " + hud.renderMode + " scale " + hud.scaleFactor.ToString("F3") + " scaler " + (scaler == null ? "none" : scaler.uiScaleMode.ToString())));
        var rig = GameObject.Find("Area :: Desk Phone").transform;
        phone.MarkerPoint(out Vector3 spot);
        var body = new Bounds(spot, Vector3.zero); int parts = 0;
        foreach (var renderer in rig.GetComponentsInChildren<MeshRenderer>()) { body.Encapsulate(renderer.bounds); parts++; }
        report.AppendLine("PHONE rest " + spot.ToString("F3") + " scale " + rig.localScale.x.ToString("F3") + " up " + rig.up.ToString("F2") + " forward " + rig.forward.ToString("F2") + " parts " + parts + " world size " + body.size.ToString("F3") + " center " + body.center.ToString("F3"));
        for (int i = 3; i < 5; i++)
        {
            var screen = views.ScreenFor((DeskViewTarget)i); var corners = new Vector3[4]; screen.GetWorldCorners(corners);
            report.AppendLine("TABLET " + (i - 2) + " center " + ((corners[0] + corners[2]) * .5f).ToString("F3") + " size " + Vector3.Distance(corners[0], corners[3]).ToString("F3") + " x " + Vector3.Distance(corners[0], corners[1]).ToString("F3"));
        }
        var desk = GameObject.Find("desk_body");
        if (desk != null) { report.AppendLine("DESK top " + desk.GetComponent<Renderer>().bounds.max.y.ToString("F3")); }
        var clutter = Object.FindAnyObjectByType<DeskClutter>();
        if (clutter != null) { report.AppendLine("CLUTTER moved " + clutter.Moved + " removed " + clutter.Removed); }
        Plan(views, phone, spot);
    }

    private static void Plan(DeskViewController views, DeskPhone phone, Vector3 spot)
    {
        steps.Clear();
        var chart = views.ScreenFor(DeskViewTarget.Chart); var corners = new Vector3[4]; chart.GetWorldCorners(corners);
        Vector3 chartCenter = (corners[0] + corners[2]) * .5f, chartTop = (corners[1] + corners[2]) * .5f, up = (corners[1] - corners[0]).normalized;
        float height = Vector3.Distance(corners[0], corners[1]);
        // 화면 위쪽 테두리에서 화면 높이의 share만큼(보이는 화면 기준) 떨어진 곳.
        Vector3 Above(float share)
        { return chartTop + up * (share * 2f * Vector3.Distance(camera.transform.position, chartTop) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad)); }

        // 1. 아무것도 겨누지 않을 때: 원이 하나도 없어야 한다.
        steps.Add(("aim far", () => Aim(Above(.30f)), 1.0));
        steps.Add(("shot far", () => Shoot("01-no-circles"), .2));
        // 2. 가운데 모니터에 조금 가까이: 테두리 원.
        steps.Add(("aim near", () => Aim(Above(.06f)), 1.0));
        steps.Add(("shot near", () => Shoot("02-ring-near-monitor"), .2));
        // 3. 가운데 모니터를 겨눔: 빛나는 꽉 찬 원. 움직임을 보려고 조금 사이를 두고 두 번 찍는다.
        steps.Add(("aim chart", () => Aim(chartCenter + up * height * .22f), 1.0));
        steps.Add(("shot chart a", () =>
        {
            report.AppendLine("HOVER " + views.Hovered + " focused " + views.IsFocused + " blocks " + PauseMenuController.BlocksInput);
            Diagnose("markers", Object.FindAnyObjectByType<DeskInteractionMarkers>());
            foreach (var image in Object.FindObjectsByType<Image>()) { if (image.name == "Image :: Wallpaper") { Diagnose("wallpaper image", image); } }
            Shoot("03-filled-monitor-a");
        }, .55));
        steps.Add(("shot chart b", () => Shoot("03-filled-monitor-b"), .2));
        // 4. 서랍.
        steps.Add(("aim drawer", () => { if (DrawerProp.Instance != null && DrawerProp.Instance.MarkerPoint(out Vector3 handle)) { Aim(handle); } else { report.AppendLine("DRAWER no marker point"); } }, 1.0));
        steps.Add(("shot drawer", () => { report.AppendLine("DRAWER hovered " + (DrawerProp.Instance != null && DrawerProp.Instance.Hovered)); Shoot("04-filled-drawer"); }, .2));
        // 5. 문자 세 통이 온다: 이미 지난 작전, 조금 뒤의 작전, 내일의 작전.
        // 앞의 두 통은 내용을 열어 읽은 것으로 만든다. 그 사이에 목록만 보고 내려놓아도 읽음이 되지 않는지도 본다.
        steps.Add(("receive first", () =>
        {
            var market = MarketDayController.Instance; var stocks = StockManager.Instance.stocks; int now = market.ClockMinute;
            phone.Receive(stocks[0], market.Day, Mathf.Max(0, now - 40));
            phone.Receive(stocks[3], market.Day, Mathf.Min(1439, now + 35));
            phone.Raise();
        }, .8));
        steps.Add(("look and lower", () => { phone.Lower(); }, .8));
        steps.Add(("raise again", () => { report.AppendLine("READ after only looking at the list: unread " + phone.UnreadCount + " of " + phone.Messages.Count); phone.Raise(); }, .8));
        steps.Add(("open both", () => { phone.OpenMessage(0); phone.ShowList(); phone.OpenMessage(1); phone.Lower(); report.AppendLine("READ after opening both: unread " + phone.UnreadCount); }, .8));
        steps.Add(("receive", () =>
        {
            var market = MarketDayController.Instance; var stocks = StockManager.Instance.stocks; int now = market.ClockMinute;
            report.AppendLine("CLOCK day " + market.Day + " minute " + now + " phase " + market.Phase);
            phone.Receive(stocks[1], market.Day, Mathf.Min(1439, now + 75));
            phone.Receive(stocks[2], market.Day + 1, 10 * 60 + 30);
            report.AppendLine("PHONE messages " + phone.Messages.Count + " unread " + phone.UnreadCount);
            Aim(Above(.30f));
        }, 1.2));
        steps.Add(("shot arrival", () => Shoot("05-message-arrived"), .2));
        steps.Add(("shot closeup", () => Closeup("06-phone-on-desk-closeup", spot), .2));
        // 6. 휴대폰을 겨눔.
        steps.Add(("aim phone", () => Aim(spot), 1.0));
        steps.Add(("shot phone", () => { report.AppendLine("PHONE aiming " + DeskPhone.Aiming + " hovered screen " + views.Hovered); Shoot("07-filled-phone"); }, .2));
        // 7. 들어 올림: 목록.
        steps.Add(("raise", () => { phone.Raise(); report.AppendLine("PHONE raised " + DeskPhone.Raised); }, 1.0));
        steps.Add(("shot list", () => Shoot("08-phone-list"), .2));
        // 8. 가장 새 문자를 엶: 내용.
        steps.Add(("open", () => { phone.OpenMessage(0); report.AppendLine("PHONE open " + (phone.Open != null)); }, .7));
        steps.Add(("shot detail", () => Shoot("09-phone-detail"), .2));
        // 9. Esc와 같은 길: 목록으로. 그다음 2번 키와 같은 길: 내려놓으면서 곧바로 가운데 화면 확대.
        steps.Add(("back", () => { phone.ShowList(); }, .5));
        steps.Add(("key two", () => { PhoneCallController.LowerAny(); views.Focus(DeskViewTarget.Chart); report.AppendLine("KEY2 raised " + DeskPhone.Raised + " target " + views.Target); }, .12));
        steps.Add(("shot switching", () => Shoot("10-key2-switching"), 1.0));
        steps.Add(("shot zoomed", () => { report.AppendLine("KEY2 after: raised " + DeskPhone.Raised + " target " + views.Target + " unread " + phone.UnreadCount); Shoot("11-key2-zoomed"); }, .2));
        steps.Add(("exit", () => views.ExitView(true), .6));
        steps.Add(("shot closeup read", () => Closeup("12-phone-on-desk-read", spot), .2));
    }

    /// <summary>직접 그리는 도형이 실제로 무엇을 내놓는지 적는다: 꼭짓점 수, 차지하는 상자, 캔버스에서의 그리기 상태.</summary>
    private static void Diagnose(string label, Graphic graphic)
    {
        if (graphic == null) { report.AppendLine("GRAPHIC " + label + " missing"); return; }
        var helper = new VertexHelper(); var mesh = new Mesh();
        typeof(Graphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
        int count = helper.currentVertCount; helper.FillMesh(mesh); helper.Dispose();
        var colors = mesh.colors32; int opaque = 0; foreach (var tint in colors) { if (tint.a > 8) { opaque++; } }
        var renderer = graphic.canvasRenderer; var material = graphic.materialForRendering;
        report.AppendLine("GRAPHIC " + label + " verts " + count + " visible verts " + opaque + " bounds " + mesh.bounds.center.ToString("F1") + " / " + mesh.bounds.size.ToString("F1")
            + " rect " + graphic.rectTransform.rect + " active " + graphic.isActiveAndEnabled + " color " + graphic.color + " depth " + renderer.absoluteDepth + " cull " + renderer.cull + " alpha " + renderer.GetInheritedAlpha().ToString("F2")
            + " materials " + renderer.materialCount + " material " + (material == null ? "none" : material.name + " / " + material.shader.name) + " texture " + (graphic.mainTexture == null ? "none" : graphic.mainTexture.name)
            + " canvas " + (graphic.canvas == null ? "none" : graphic.canvas.name + " " + graphic.canvas.renderMode) + " world " + graphic.transform.position.ToString("F2") + " lossy " + graphic.transform.lossyScale.ToString("F5"));
        Object.Destroy(mesh);
    }

    private static void Aim(Vector3 point)
    { camera.transform.rotation = Quaternion.LookRotation(point - camera.transform.position, Vector3.up); }

    /// <summary>
    /// 플레이어 카메라가 보는 그대로를 찍는다. 화면에 덮어 그리는 것(원, 안내 문구)은 카메라로 찍히지 않으므로 찍는 동안만 카메라에 붙인다.
    /// 그림은 게임 화면과 같은 비율로, 세로가 1080 이상이 되게 정수배로 키운다(원의 자리는 게임 화면 크기로 계산돼 있다).
    /// </summary>
    private static void Shoot(string name)
    {
        int factor = Mathf.Max(1, Mathf.CeilToInt(1080f / Mathf.Max(1, Screen.height)));
        int width = Screen.width * factor, height = Screen.height * factor;
        var overlays = new List<(Canvas canvas, float scale)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.gameObject.activeInHierarchy) { continue; }
            bool wanted = canvas.GetComponentInChildren<DeskInteractionMarkers>() != null || canvas.name == "Area :: Phone Hint" || canvas.name == "Area :: Drawer Hint";
            if (wanted) { overlays.Add((canvas, canvas.scaleFactor)); }
        }
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture;
        foreach (var item in overlays)
        { item.canvas.renderMode = RenderMode.ScreenSpaceCamera; item.canvas.worldCamera = camera; item.canvas.planeDistance = camera.nearClipPlane + .2f; item.canvas.scaleFactor = item.scale * factor; }
        Canvas.ForceUpdateCanvases();
        camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        foreach (var item in overlays) { item.canvas.renderMode = RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera = null; item.canvas.scaleFactor = item.scale; }
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        report.AppendLine("VIEW " + name + " " + width + "x" + height + " overlays " + overlays.Count);
    }

    /// <summary>책상 위의 휴대폰을 가까이서 내려다본다. 어두워서 안 보이니 찍는 동안만 등을 하나 켠다. 게임의 카메라는 건드리지 않는다.</summary>
    private static void Closeup(string name, Vector3 spot)
    {
        var rig = GameObject.Find("Area :: Desk Phone").transform;
        float reach = 1.36f * rig.localScale.x;
        var holder = new GameObject("Camera :: Phone Check"); var view = holder.AddComponent<Camera>(); view.CopyFrom(camera); view.fieldOfView = 40f; view.nearClipPlane = .05f; view.targetTexture = null;
        Vector3 back = camera.transform.position - spot; back.y = 0f; back.Normalize();
        holder.transform.position = spot + back * reach * 1.9f + Vector3.up * reach * 2.3f; holder.transform.LookAt(spot);
        var lamp = new GameObject("Light :: Phone Check").AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = reach * 14f; lamp.intensity = reach * reach * 10f;
        lamp.transform.position = spot + back * reach * 1.0f + Vector3.up * reach * 3.0f + Vector3.Cross(Vector3.up, back) * reach * 1.2f;
        const int width = 1600, height = 1200;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
        view.targetTexture = texture; view.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        view.targetTexture = null; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
        Object.Destroy(holder); Object.Destroy(lamp.gameObject);
        report.AppendLine("VIEW " + name);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        MouseCameraController.AimOverride = null; DoorNotes.ByPhone = true;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags; Application.runInBackground = oldBackground;
        Environment.SetEnvironmentVariable("DONDON_SETTINGS_OUTPUT", null);
        if (error != null) { report.AppendLine("FAIL step " + step + (step > 0 && step <= steps.Count ? " (" + steps[step - 1].name + ")" : "") + " " + error); }
        report.AppendLine("runtime errors: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 12; i++) { report.AppendLine(errors[i]); }
        report.AppendLine(error == null ? "DONE" : "STOPPED");
        Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
        running = false;
        if (Application.isBatchMode) { EditorApplication.Exit(error == null && errors.Count == 0 ? 0 : 1); }
        else if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); }
    }
}
