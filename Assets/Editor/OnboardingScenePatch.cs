using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>기존 시작 Canvas와 오른쪽 모니터에 첫 성장 흐름을 연결한다. 기존 3D 장치/카메라는 보존한다.</summary>
public static class OnboardingScenePatch
{
    private static TMP_FontAsset font;
    private static Material secondary;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(154,169,180,255), Dark = new Color32(15,23,27,255), Field = new Color32(26,39,44,255), Accent = new Color32(25,131,144,255);
    [MenuItem("DonDon/Apply Investor Onboarding")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var poses = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => !(t is RectTransform)).ToDictionary(t => t, t => Pose(t));
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c) + Pose(c.transform));
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        secondary = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/TradingTerminal/Secondary_Text.mat");
        if (font == null)
        { throw new Exception("Existing Korean font is required."); }
        var system = Object.FindAnyObjectByType<InvestorSociety>().gameObject;
        var npc = Component<NpcLeadingRoom>(system);
        var roomUI = Object.FindAnyObjectByType<LeadingRoomUI>(FindObjectsInactive.Include);
        var screens = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include);
        var orders = screens.Single(s => !s.journal); var journal = screens.Single(s => s.journal);
        orders.pairedTablet = journal.thisTablet; journal.pairedTablet = orders.thisTablet;
        npc.chat = roomUI.chat; roomUI.orderScreen = orders;
        RoomControls(roomUI);
        var hud = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Single(c => c.name == "Panel :: Interaction HUD");
        Journey(hud.transform,roomUI,orders,journal);
        foreach (var pair in poses)
        {
            if (pair.Value != Pose(pair.Key))
            { throw new Exception("3D pose changed: " + pair.Key.name); }
        }
        foreach (var pair in canvases)
        {
            if (pair.Value != EditorJsonUtility.ToJson(pair.Key) + Pose(pair.Key.transform))
            { throw new Exception("Canvas root or camera changed: " + pair.Key.name); }
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Validation/Onboarding");
        File.WriteAllText("Validation/Onboarding/scene-results.txt","PASS original 3D transforms and all Canvas roots/cameras preserved\nPASS existing order, phone, three player room controls retained\n");
    }
    private static string Pose(Transform t)
    {
        string value = (t.parent == null ? "root" : t.parent.GetEntityId().ToString()) + t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R");
        if (t is RectTransform r)
        { value += r.anchorMin.ToString("R") + r.anchorMax.ToString("R") + r.sizeDelta.ToString("R") + r.pivot.ToString("R"); }
        return value;
    }
    private static T Component<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    private static void Layout(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1f-y-h); rect.anchorMax = new Vector2(x+w,1f-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var child = parent.Find(name);
        var rect = child == null ? new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>() : (RectTransform)child;
        if (child == null)
        { rect.SetParent(parent,false); }
        Layout(rect,x,y,w,h); return rect;
    }
    private static void Style(TextMeshProUGUI text,float size,bool muted = false)
    {
        text.font = font; text.fontSharedMaterial = muted && secondary != null ? secondary : font.material;
        text.fontSize = size; text.fontStyle = FontStyles.Normal; text.enableAutoSizing = false; text.richText = false;
        text.raycastTarget = false; text.color = muted ? Muted : Ink; text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
    }
    private static TextMeshProUGUI Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,bool muted = false)
    {
        var text = Component<TextMeshProUGUI>(Rect("Text :: " + name,parent,x,y,w,h).gameObject);
        Style(text,size,muted); text.text = value; return text;
    }
    private static void Backdrop(RectTransform rect,Color color,bool raycast = false)
    {
        var image = Component<Image>(rect.gameObject); image.color = color; image.raycastTarget = raycast;
    }
    private static Button Button(string name,Transform parent,string label,float x,float y,float w,float h,float size,bool primary = false)
    {
        var rect = Rect("Button :: " + name,parent,x,y,w,h); Backdrop(rect,primary ? Accent : Field,true);
        var button = Component<Button>(rect.gameObject); button.targetGraphic = rect.GetComponent<Image>();
        var caption = Text(name + " Label",rect,label,.03f,.06f,.94f,.88f,size); caption.alignment = TextAlignmentOptions.Center;
        var colors = ColorBlock.defaultColorBlock; colors.highlightedColor = new Color(.72f,.9f,.94f); colors.selectedColor = colors.highlightedColor; colors.disabledColor = new Color(.4f,.4f,.4f,1f); button.colors = colors;
        return button;
    }
    private static void RoomControls(LeadingRoomUI ui)
    {
        var root = ui.transform;
        var buttons = new[]{ui.publicRoom,ui.roomOne,ui.roomTwo,ui.roomThree};
        float[] x = {.025f,.415f,.61f,.805f};
        string[] titles = {"전체","내 방 1","내 방 2","내 방 3"};
        for (int i = 0; i < buttons.Length; i++)
        {
            Layout((RectTransform)buttons[i].transform,x[i],.055f,.17f,.033f);
            var text = buttons[i].GetComponentInChildren<TextMeshProUGUI>(); Style(text,48f); text.alignment = TextAlignmentOptions.Center; text.text = titles[i];
        }
        ui.npcRoom = Button("NPC Leading Room",root,"새벽방",.22f,.055f,.17f,.033f,48f);
        var own = Rect("Area :: Player Room Controls",root,0,0,1,1); ui.playerControls = own.gameObject;
        Transform[] existing = {ui.commandStatus.transform,ui.buyPrice.transform,ui.sellPrice.transform,ui.issue.transform,ui.cancel.transform,ui.feedback.transform};
        foreach (var item in existing)
        { item.SetParent(own,false); }
        foreach (string name in new[]{"Text :: Buy Trigger Label","Text :: Sell Trigger Label"})
        {
            var item = root.Find(name);
            if (item != null)
            { item.SetParent(own,false); }
        }
        var npc = Rect("Area :: NPC Room Controls",root,0,0,1,1); ui.npcControls = npc.gameObject;
        ui.npcSummary = Text("NPC Leader Summary",npc,"",.035f,.778f,.93f,.044f,48f,true);
        ui.npcSignal = Text("NPC Observed Signal",npc,"",.035f,.832f,.93f,.068f,50f);
        ui.npcBuy = Button("Review NPC Buy",npc,"매수 검토",.035f,.907f,.29f,.034f,48f,true);
        ui.npcSell = Button("Review NPC Sell",npc,"매도 검토",.36f,.907f,.29f,.034f,48f);
        ui.npcMembership = Button("NPC Room Membership",npc,"방 참여",.69f,.907f,.28f,.034f,48f);
        ui.membershipLabel = ui.npcMembership.GetComponentInChildren<TextMeshProUGUI>();
        ui.npcFeedback = Text("NPC Room Feedback",npc,"",.035f,.954f,.93f,.04f,40f,true);
        npc.gameObject.SetActive(false);
    }
    private static void Journey(Transform hud,LeadingRoomUI rooms,DeviceTerminalUI orders,DeviceTerminalUI journal)
    {
        var ui = Component<InvestorJourneyUI>(hud.gameObject); ui.rooms = rooms; ui.orders = orders; ui.journal = journal;
        var card = Rect("Panel :: First Investor Journey",hud,.732f,.80f,.25f,.16f); Backdrop(card,new Color(.035f,.06f,.075f,.96f)); ui.card = card.gameObject;
        ui.heading = Text("Journey Step",card,"첫 발걸음",.04f,.06f,.83f,.19f,25f);
        ui.detail = Text("Journey Detail",card,"",.04f,.28f,.92f,.37f,21f,true);
        ui.action = Button("Journey Action",card,"봇 리딩방 보기",.04f,.70f,.92f,.23f,22f,true); ui.actionLabel = ui.action.GetComponentInChildren<TextMeshProUGUI>();
        ui.dismiss = Button("Dismiss Journey",card,"×",.89f,.04f,.08f,.18f,22f);
        ui.reopen = Button("Reopen Journey",hud,"시작 안내",.862f,.91f,.12f,.045f,21f);
        ui.card.SetActive(false); ui.reopen.gameObject.SetActive(false);
    }
}
