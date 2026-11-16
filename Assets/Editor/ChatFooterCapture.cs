using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 채팅 화면 하단(참여자 카드·방 카드)을 실제 씬에서 방 상태별·언어별로 찍는다. 씬은 저장하지 않는다.
// 출력 폴더: 환경 변수 DONDON_FOOTER_OUT (없으면 Validation/ChatFooter).
public static class ChatFooterCapture
{
    private static string output;
    private static readonly StringBuilder report = new();
    private static readonly List<string> errors = new();
    private static bool oldOptions;
    private static EnterPlayModeOptions oldFlags;
    private static double began, ready;
    private static int phase;
    private static Camera camera;
    private static TabletGroupChatUI chat;
    private static LeadingRoomUI rooms;

    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("DONDON_FOOTER_OUT");
        if (string.IsNullOrEmpty(output)) { output = "Validation/ChatFooter"; }
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; ready = began + 3; phase = 0;
        Application.logMessageReceived += Log; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    private static void Log(string message, string stack, LogType type)
    { if ((type == LogType.Exception || type == LogType.Error) && !stack.Contains("UnityEditor.Search")) { errors.Add(message + "\n" + stack); } }
    private static void Next(double seconds = 1) { phase++; ready = EditorApplication.timeSinceStartup + seconds; }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - began > 420)
        {
            var manager = StockManager.Instance; var day = MarketDayController.Instance; var setupUi = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
            Finish(new Exception("timeout at phase " + phase + " playing=" + EditorApplication.isPlaying + " confirmed=" + (manager != null && manager.SetupConfirmed) + " inProgress=" + (manager != null && manager.SetupInProgress) +
                " canTrade=" + MarketDayController.CanTrade + " ready=" + (day != null && day.Ready) + " phase=" + (day == null ? "-" : day.Phase.ToString()) +
                " confirmButton=" + (setupUi == null || setupUi.confirm == null ? "none" : setupUi.confirm.interactable + "/" + setupUi.confirm.gameObject.activeInHierarchy) + " timeScale=" + Time.timeScale));
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < ready) { return; }
        try
        {
            if (phase == 0)
            {
                Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0;
                camera = Camera.main;
                chat = Object.FindObjectsByType<TabletGroupChatUI>().Single(c => c.monitorChat); rooms = chat.GetComponent<LeadingRoomUI>();
                Next(2); return;
            }
            if (!StockManager.Instance.SetupConfirmed)
            {
                // 시작 화면의 확인 단추가 눌릴 수 있게 되면 누른다.
                var setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup != null && setup.confirm != null && setup.confirm.interactable && !StockManager.Instance.SetupInProgress) { setup.confirm.onClick.Invoke(); }
                ready = EditorApplication.timeSinceStartup + 1; return;
            }
            if (!MarketDayController.CanTrade || !MarketDayController.Instance.Ready) { return; }
            if (phase == 1)
            {
                foreach (var mouse in Object.FindObjectsByType<MouseCameraController>()) { mouse.enabled = false; }
                Time.timeScale = 1f;
                Next(18); return;   // 대화가 쌓이고 운영자 신호가 나올 때까지.
            }
            if (phase == 2) { chat.SelectRoom(0); Next(1.5); return; }
            if (phase == 3) { Shoot("ko-1-lounge-empty"); if (!ClickRow()) { return; } Next(1.5); return; }
            if (phase == 4) { Shoot("ko-2-lounge-investor"); rooms.ShowNpc(); Next(1.5); return; }
            if (phase == 5)
            {
                var npc = NpcLeadingRoom.Instance;
                report.AppendLine("npc joined=" + (npc != null && npc.PlayerJoined) + " signal=" + (npc != null && npc.HasSignal));
                Shoot("ko-3-host-room"); ClickRow(); Next(1.5); return;
            }
            if (phase == 6)
            {
                Shoot("ko-4-host-room-investor");
                // 내 리딩방 화면도 찍기 위해 이 실행에서만 운영 권한을 연다.
                var career = PlayerProgression.Instance; Put(career.GetType(), career, "CanOperateRooms", true);
                chat.SelectRoom(1); Next(2); return;
            }
            if (phase == 7)
            {
                report.AppendLine("own room controls=" + rooms.playerControls.activeSelf + " lobby=" + rooms.lobbyControls.activeSelf);
                Shoot("ko-5-own-room"); GameLocale.SetLanguage("en"); rooms.ShowNpc(); Next(4); return;
            }
            if (phase == 8) { ClickRow(); Next(1.5); return; }
            if (phase == 9) { Shoot("en-4-host-room-investor"); chat.SelectRoom(1); Next(1.5); return; }
            if (phase == 10) { Shoot("en-5-own-room"); GameLocale.SetLanguage("ja"); rooms.ShowNpc(); Next(4); return; }
            if (phase == 11) { ClickRow(); Next(1.5); return; }
            if (phase == 12) { Shoot("ja-4-host-room-investor"); chat.SelectRoom(1); Next(1.5); return; }
            if (phase == 13) { Shoot("ja-5-own-room"); GameLocale.SetLanguage("ko"); Finish(null); }
        }
        catch (Exception error) { Finish(error); }
    }
    /// <summary>
    /// 참여자 한 명을 고른다. 장 초반에는 신뢰·배신감·승패가 전부 0이라 막대 모양을 볼 수 없으므로,
    /// 이 캡처 실행 안에서만 보기용 값을 넣는다(저장되지 않는다).
    /// </summary>
    private static bool ClickRow()
    {
        var bot = InvestorSociety.Instance.SocialBots.Values.OrderBy(b => b.Social.Id).Skip(demo++ % 5).FirstOrDefault();
        if (bot == null) { return false; }
        var mind = bot.Social.Psychology; var type = mind.GetType();
        float[][] looks = { new[] { 62f, 18f, 3f, 1f }, new[] { 84f, 6f, 5f, 0f }, new[] { 71f, 64f, 4f, 4f }, new[] { 12f, 77f, 1f, 5f }, new[] { 0f, 0f, 0f, 0f } };
        var look = looks[(demo - 1) % looks.Length];
        Put(type, mind, "Trust", look[0]); Put(type, mind, "Betrayal", look[1]); Put(type, mind, "Wins", (int)look[2]); Put(type, mind, "Losses", (int)look[3]);
        mind.RefreshQuadrant();
        chat.SelectProfile(bot.Social.Id);
        return true;
    }
    private static int demo;
    private static void Put(Type type, object target, string name, object value)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var property = type.GetProperty(name, flags);
        if (property != null && property.GetSetMethod(true) != null) { property.SetValue(target, Convert.ChangeType(value, property.PropertyType)); return; }
        var field = type.GetField(name, flags) ?? type.GetField("<" + name + ">k__BackingField", flags);
        if (field != null) { field.SetValue(target, Convert.ChangeType(value, field.FieldType)); } else { report.AppendLine("demo value not set: " + name); }
    }
    /// <summary>화면 전체와 하단 확대, 두 장을 찍고 넘치는 글자를 적는다.</summary>
    private static void Shoot(string name)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)chat.transform; var area = rect.rect;
        int overflow = 0;
        foreach (var text in rect.GetComponentsInChildren<TextMeshProUGUI>())
        {
            var item = text.rectTransform; Vector3 local = rect.InverseTransformPoint(item.TransformPoint(item.rect.center));
            if ((area.yMax - local.y) / area.height < .655f) { continue; }
            text.ForceMeshUpdate();
            if (text.isTextOverflowing) { overflow++; report.AppendLine("OVERFLOW " + name + " / " + text.name + " / " + text.text.Replace("\n", " | ")); }
        }
        report.AppendLine("SHOT " + name + " overflow=" + overflow);
        var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        // 화면을 정면에서 본다: 카드의 정렬과 간격을 원근 왜곡 없이 확인하기 위해서다.
        Vector3 forward = -rect.forward, footer = rect.TransformPoint(new Vector3(area.center.x, area.yMax - area.height * .825f, 0));
        float worldHeight = Vector3.Distance(rect.TransformPoint(new Vector3(0, area.yMin, 0)), rect.TransformPoint(new Vector3(0, area.yMax, 0)));
        Frame(rect.TransformPoint(area.center) + forward * worldHeight * 2.2f, rect.TransformPoint(area.center), rect.up, 27f, name + "-full", 900, 1600);
        Frame(footer + forward * worldHeight * 1.1f, footer, rect.up, 22f, name + "-footer", 1600, 1200);
        camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
    }
    private static void Frame(Vector3 from, Vector3 target, Vector3 up, float fov, string name, int width, int height)
    {
        camera.transform.position = from; camera.transform.rotation = Quaternion.LookRotation(target - from, up); camera.fieldOfView = fov;
        var texture = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var old = camera.targetTexture;
        camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; Object.Destroy(pixels); texture.Release(); Object.Destroy(texture);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        if (error != null) { report.AppendLine("FAIL phase " + phase + " " + error); }
        report.AppendLine("runtime errors: " + errors.Count); foreach (string line in errors.Take(12)) { report.AppendLine(line); }
        File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
        EditorApplication.Exit(error == null && errors.Count == 0 ? 0 : 1);
    }
}
