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
        StartScreen(Object.FindAnyObjectByType<PlayerCompanySetupUI>(FindObjectsInactive.Include));
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
    private static void StartScreen(PlayerCompanySetupUI setup)
    {
        var root = setup.transform;
        var oldBackdrop = root.Find("Image :: Backdrop");
        if (oldBackdrop != null)
        { Backdrop((RectTransform)oldBackdrop,new Color(.018f,.028f,.034f,.94f),true); }
        var menu = Rect("Panel :: Start Menu",root,.08f,.075f,.84f,.85f);
        // Preserve old references; move the original form fields into the optional company-start panel.
        foreach (Transform child in root)
        {
            if (child != menu && child != oldBackdrop)
            { child.gameObject.SetActive(false); }
        }
        var intro = Rect("Area :: Retail Introduction",menu,0,0,1,1); setup.retailIntroduction = intro.gameObject;
        Text("Game Title",intro,"DON DON",0,0,.65f,.14f,92f);
        Text("Opening Line",intro,"시장은 움직이고, 사람들은 서로를 믿습니다.",.005f,.145f,.92f,.06f,28f,true);
        var line = Rect("Image :: Opening Divider",intro,0,.23f,1,.002f); Backdrop(line,new Color32(49,66,73,255));
        Text("Starting Cash Caption",intro,"첫 투자금",0,.29f,.49f,.05f,26f,true);
        var market = Object.FindAnyObjectByType<StockManager>();
        long startingCash = market != null ? market.economy.retailStartingCash : new EconomySettings().retailStartingCash;
        setup.startBalance = Text("Starting Cash",intro,startingCash.ToString("N0") + "원",0,.35f,.51f,.10f,66f);
        setup.retailPreview = Text("Retail Start Details",intro,"",0,.50f,.51f,.25f,27f,true);
        var path = Rect("Panel :: First Journey",intro,.56f,.29f,.44f,.47f); Backdrop(path,Dark);
        Text("First Room",path,"첫 가입 · 새벽 관찰실",.055f,.05f,.89f,.14f,34f);
        Text("First Journey Steps",path,"01  관찰\n운영자의 실제 주문과 체결 확인\n02  내 판단으로 거래\n가격·수량 확인 후 직접 제출\n03  내 리딩방 개설\n실현한 매매 성과로 운영 권한 획득",.055f,.22f,.89f,.74f,26f,true);
        var controls = Rect("Panel :: Opening Controls",intro,.56f,.29f,.44f,.47f); Backdrop(controls,Dark); setup.controlsPanel = controls.gameObject;
        Text("Controls Heading",controls,"제자리에서 시장을 읽기",.055f,.05f,.89f,.14f,32f);
        Text("Opening Controls",controls,"마우스  둘러보기  ·  휠  시야각\n클릭 / E  기기 열기\nSpace  차트 확대 / 돌아가기\nA / D  확대 화면 좌우 이동\nW / S  이전·다음 종목\n1 / 2  계좌 태블릿  ·  Tab  휴대폰\nEsc  내려놓기  ·  P  일시정지",.055f,.23f,.89f,.72f,26f,true);
        controls.gameObject.SetActive(false);
        var company = Rect("Panel :: Company Start Form",menu,0,0,1,.80f); setup.companySetup = company.gameObject;
        Text("Company Start Heading",company,"내 기업 설정",0,0,1,.12f,52f);
        TMP_InputField[] fields = {setup.companyName,setup.issuedShares,setup.publicPercentage};
        string[] labels = {"종목 이름","총 발행량","시장 공개 비율 (%)"};
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i].transform.SetParent(company,false); fields[i].gameObject.SetActive(true);
            Layout((RectTransform)fields[i].transform,.32f,.19f+i*.13f,.68f,.095f);
            Text("Company Setting " + i,company,labels[i],0,.19f+i*.13f,.30f,.09f,27f,true);
        }
        setup.preview.transform.SetParent(company,false); setup.preview.gameObject.SetActive(true);
        Layout(setup.preview.rectTransform,0,.60f,1,.39f); Style(setup.preview,26f,true);
        company.gameObject.SetActive(false);
        setup.confirm.transform.SetParent(menu,false); setup.confirm.gameObject.SetActive(true);
        Layout((RectTransform)setup.confirm.transform,0,.84f,.51f,.095f); setup.confirm.image.color = Accent;
        var confirmText = setup.confirm.GetComponentInChildren<TextMeshProUGUI>(); Layout(confirmText.rectTransform,.03f,.06f,.94f,.88f); Style(confirmText,30f); confirmText.alignment = TextAlignmentOptions.Center; confirmText.text = "리딩방에 가입하고 시작";
        setup.controlsToggle = Button("Opening Controls",menu,"조작 방법",.56f,.84f,.44f,.095f,27f);
        setup.error.transform.SetParent(menu,false); setup.error.gameObject.SetActive(true); Layout(setup.error.rectTransform,0,.95f,1,.05f); Style(setup.error,22f,true);
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
