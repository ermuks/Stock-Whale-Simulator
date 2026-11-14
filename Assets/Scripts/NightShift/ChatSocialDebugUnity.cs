using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>F9 arms click inspection. Normal player builds contain no panel or input polling.</summary>
public sealed class ChatSocialDebugUnity : MonoBehaviour
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void ShowMessage(SocialMessage message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (instance == null || !instance.visible) { return; }
        instance.selectedMessage = message; instance.inspectBot = false; instance.RefreshText();
#endif
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void ShowBot(int id)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (instance == null || !instance.visible) { return; }
        instance.botId = id.ToString(); instance.inspectBot = true; instance.RefreshText();
#endif
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void ApplySeed(MarketDayController controller)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (restartSeed.HasValue) { controller.seed = restartSeed.Value; }
#endif
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static ChatSocialDebugUnity instance;
    private static int? restartSeed;
    private bool visible, inspectBot, confirmRestart;
    private string botId = "1", targetId = "2", anger = "75", seed = "73129", details = "F9: click a chat message or nickname.", status = "";
    private SocialMessage selectedMessage;
    private int actIndex;
    private Vector2 scroll;
    private Rect window;
    private GUIStyle detailStyle;
    private static readonly string[] acts = Enum.GetNames(typeof(SocialAct));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { instance = null; restartSeed = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) { return; }
        var root = new GameObject("Area :: Social Development Inspector"); DontDestroyOnLoad(root);
        instance = root.AddComponent<ChatSocialDebugUnity>();
    }
    private void Update()
    { if (Input.GetKeyDown(KeyCode.F9)) { visible = !visible; confirmRestart = false; } }
    private void RefreshText()
    {
        var society = InvestorSociety.Instance;
        details = inspectBot && int.TryParse(botId, out int id) && society != null && society.SocialBots.TryGetValue(id, out var bot) ?
            ChatSocialDiagnostics.Bot(bot) : ChatSocialDiagnostics.Message(selectedMessage);
        scroll = Vector2.zero;
    }
    private void OnGUI()
    {
        if (!visible) { return; }
        if (window.width == 0) { window = new Rect(Mathf.Max(0, Screen.width - 560), 16, 540, Mathf.Max(300, Screen.height - 32)); }
        detailStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
        window = GUI.Window(63681, window, Draw, "Social inspector (development only) | F9 close");
    }
    private void Draw(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Bot", GUILayout.Width(30)); botId = GUILayout.TextField(botId, GUILayout.Width(60));
        GUILayout.Label("Target", GUILayout.Width(45)); targetId = GUILayout.TextField(targetId, GUILayout.Width(60));
        if (GUILayout.Button("Inspect bot")) { inspectBot = true; RefreshText(); }
        if (GUILayout.Button("Refresh")) { RefreshText(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Anger 0-100", GUILayout.Width(85)); anger = GUILayout.TextField(anger, GUILayout.Width(50));
        if (GUILayout.Button("Set both directions")) { ForceAnger(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(25))) { actIndex = (actIndex + acts.Length - 1) % acts.Length; }
        GUILayout.Label(acts[actIndex], GUILayout.Width(140));
        if (GUILayout.Button(">", GUILayout.Width(25))) { actIndex = (actIndex + 1) % acts.Length; }
        if (GUILayout.Button("Queue act")) { ForceAct(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Seed", GUILayout.Width(40)); seed = GUILayout.TextField(seed, GUILayout.Width(95));
        if (GUILayout.Button(confirmRestart ? "Confirm reset session" : "Restart with seed"))
        {
            if (!confirmRestart) { confirmRestart = true; status = "Resets the current unsaved session; persistent save is not erased."; }
            else if (int.TryParse(seed, out int value))
            { restartSeed = value; confirmRestart = false; selectedMessage = null; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(status, detailStyle);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(details, detailStyle);
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, window.width, 22));
    }
    private bool Selection(out InvestorSociety society, out TradeBot actor, out TradeBot target)
    {
        society = InvestorSociety.Instance; actor = null; target = null;
        if (society == null || !int.TryParse(botId, out int a) || !int.TryParse(targetId, out int b) || a == b ||
            !society.SocialBots.TryGetValue(a, out actor) || !society.SocialBots.TryGetValue(b, out target))
        { status = "Select two existing, different investor IDs."; return false; }
        return true;
    }
    private void ForceAnger()
    {
        if (!Selection(out var society, out var actor, out var target) || !double.TryParse(anger, out double value) || double.IsNaN(value) || double.IsInfinity(value))
        { return; }
        value = Math.Max(0, Math.Min(100, value)); long cause = ChatSocialIds.Next();
        foreach (var pair in new[] { (actor, target), (target, actor) })
        { society.ChangeRelation(pair.Item1, pair.Item2.Social.Id, "anger", value - pair.Item1.Social.Psychology.Relation(pair.Item2.Social.Id).anger,
            "developer-set", cause, pair.Item1.Social.Psychology.Room, false); }
        status = "Both anger values updated and audited (developer override)."; inspectBot = true; RefreshText();
    }
    private void ForceAct()
    {
        if (!Selection(out var society, out var actor, out var target)) { return; }
        int room = actor.Social.Psychology.Room;
        if (!society.Present(actor, room) || !society.Present(target, room)) { status = "Both investors must be available in this room."; return; }
        society.Offer(actor, room, SocialMotive.EMOTION_THRESHOLD, society.CaptureFacts(actor), (SocialAct)actIndex, target.Social.Id, ChatSocialIds.Next());
        status = "Act queued. Live facts, cooldown and template guards still apply.";
    }
#endif
}
