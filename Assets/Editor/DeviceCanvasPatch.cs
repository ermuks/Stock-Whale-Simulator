using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>기존 장치 Canvas 안의 표시만 배치한다. 모델/Canvas 루트/이벤트 카메라는 변경하지 않는다.</summary>
public static class DeviceCanvasPatch
{
    private static TMP_FontAsset font;
    private static Sprite background;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(166,177,186,255), Dark = new Color32(16,23,27,255), Field = new Color32(23,33,38,255), Accent = new Color32(37,175,192,255);
    private const string Folder = "Validation/DeviceCanvasUI/";
    [MenuItem("DonDon/Apply Device Canvas UI")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => !(t is RectTransform)).ToArray();
        var poses = transforms.Select(Pose).ToArray();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        var canvasState = canvases.Select(c => EditorJsonUtility.ToJson(c) + Pose(c.transform)).ToArray();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/TradingTerminal/Panel_Background.png");
        if (font == null)
        { throw new Exception("Existing Korean TMP font is missing."); }
        var operation = Object.FindAnyObjectByType<TradingOperationController>(FindObjectsInactive.Include);
        var session = Component<DeviceOrderSession>(operation.gameObject);
        session.account = Object.FindAnyObjectByType<StockManager>().playerAccount;
        var records = Component<DeviceMarketJournal>(operation.gameObject);
        Component<PlayerProgression>(operation.gameObject);
        var incumbent = Component<IncumbentExchangeAI>(operation.gameObject);
        incumbent.manual = session; incumbent.exchange = Object.FindAnyObjectByType<ExchangeBot>(FindObjectsInactive.Include);
        var oldScreens = Object.FindObjectsByType<DeskOperationUI>(FindObjectsInactive.Include);
        DeviceTerminalUI orders = Tablet(oldScreens.Single(x => !x.journal), session, records);
        DeviceTerminalUI journal = Tablet(oldScreens.Single(x => x.journal), session, records);
        journal.orderScreen = orders;
        var interactions = Object.FindObjectsByType<TabletViewController>(FindObjectsInactive.Include);
        orders.thisTablet = interactions.Single(x => x.focusKey == KeyCode.Alpha1);
        journal.thisTablet = interactions.Single(x => x.focusKey == KeyCode.Alpha2);
        journal.orderTablet = orders.thisTablet;
        Phone(Object.FindAnyObjectByType<PhoneCallController>(FindObjectsInactive.Include));
        for (int i = 0; i < transforms.Length; i++)
        {
            if (Pose(transforms[i]) != poses[i])
            { throw new Exception("3D transform changed: " + transforms[i].name); }
        }
        for (int i = 0; i < canvases.Length; i++)
        {
            if (EditorJsonUtility.ToJson(canvases[i]) + Pose(canvases[i].transform) != canvasState[i])
            { throw new Exception("Canvas configuration changed: " + canvases[i].name); }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "patch-results.txt", "PASS existing 3D transforms, Canvas roots, render modes and cameras preserved\nPASS original operation, holdings, news and phone controls retained\n");
        Debug.Log("Device Canvas UI applied.");
    }
    public static void ApplyBatch()
    {
        try { Apply(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static T Component<T>(GameObject target) where T : Component
    { return target.GetComponent<T>() ?? target.AddComponent<T>(); }
    private static string Pose(Transform value)
    {
        string result = value.parent == null ? "root" : value.parent.GetEntityId().ToString();
        result += value.localPosition.ToString("R") + value.localRotation.ToString("R") + value.localScale.ToString("R");
        if (value is RectTransform rect)
        { result += rect.anchorMin.ToString("R") + rect.anchorMax.ToString("R") + rect.sizeDelta.ToString("R") + rect.pivot.ToString("R"); }
        return result;
    }
    private static void Layout(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var child = parent.Find(name);
        var rect = child == null ? new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>() : (RectTransform)child;
        if (child == null) { rect.SetParent(parent, false); }
        Layout(rect, x, y, w, h);
        return rect;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, string value, float x, float y, float w, float h, float size = 34f, bool muted = false)
    {
        var label = Component<TextMeshProUGUI>(Rect("Text :: " + name, parent, x,y,w,h).gameObject);
        Style(label, size, muted); label.text = value;
        return label;
    }
    private static void Style(TextMeshProUGUI label, float size, bool muted = false)
    {
        label.font = font; label.fontSharedMaterial = font.material; label.fontSize = size; label.enableAutoSizing = false;
        label.fontStyle = FontStyles.Normal; label.lineSpacing = 0f;
        label.color = muted ? Muted : Ink; label.raycastTarget = false; label.richText = false;
        label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.TopLeft;
    }
    private static Image Image(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var image = Component<Image>(Rect("Image :: " + name, parent,x,y,w,h).gameObject);
        image.color = color; image.raycastTarget = false; return image;
    }
    private static Button Button(string name, Transform parent, string caption, float x, float y, float w, float h, float size = 34f, bool primary = false)
    {
        var rect = Rect("Button :: " + name,parent,x,y,w,h);
        var image = Component<Image>(rect.gameObject); image.sprite = null; image.color = primary ? new Color32(17,126,139,255) : Field; image.raycastTarget = true;
        var button = Component<Button>(rect.gameObject); button.targetGraphic = image;
        var colors = ColorBlock.defaultColorBlock; colors.highlightedColor = new Color(.7f,.9f,.95f); colors.selectedColor = colors.highlightedColor; colors.disabledColor = new Color(.45f,.45f,.45f,1f); button.colors = colors;
        var text = Text(name + " Label",rect,caption,.025f,.06f,.95f,.88f,size);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static TMP_InputField Input(string name, Transform parent, string value, float x,float y,float w,float h,float size = 38f)
    {
        var rect = Rect("Area :: " + name,parent,x,y,w,h);
        var image = Component<Image>(rect.gameObject); image.color = Field; image.raycastTarget = true;
        var input = Component<TMP_InputField>(rect.gameObject); input.targetGraphic = image;
        var viewport = Rect("Area :: Input Viewport",rect,.04f,.07f,.92f,.86f); Component<RectMask2D>(viewport.gameObject);
        var text = Text(name + " Value",viewport,value,0,0,1,1,size); text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        input.textViewport = viewport; input.textComponent = text; input.fontAsset = font;
        input.contentType = TMP_InputField.ContentType.IntegerNumber; input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 12; input.text = value;
        return input;
    }
    private static RectTransform Scroll(string name, Transform parent, float x,float y,float w,float h, out RectTransform content)
    {
        var root = Rect("Area :: " + name,parent,x,y,w,h);
        var image = Component<Image>(root.gameObject); image.color = new Color(0,0,0,.001f); image.raycastTarget = true;
        var viewport = Rect("Area :: Viewport",root,0,0,1,1); Component<RectMask2D>(viewport.gameObject);
        content = Rect("Area :: Content",viewport,0,0,1,1);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.anchoredPosition = Vector2.zero;
        var scroll = Component<ScrollRect>(root.gameObject); scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f;
        return root;
    }
    private static DeviceTerminalUI Tablet(DeskOperationUI old, DeviceOrderSession session, DeviceMarketJournal records)
    {
        var ui = Component<DeviceTerminalUI>(old.gameObject); ui.journal = old.journal; ui.legacy = old; ui.session = session; ui.records = records;
        var canvas = old.transform;
        ui.mainTab = Button("Device Main Tab",canvas,old.journal ? "사건·경영" : "자산·주문",.025f,.012f,.30f,.068f,38f,true);
        Layout((RectTransform)old.deskTab.transform,.35f,.012f,.30f,.068f);
        Layout((RectTransform)old.legacyTab.transform,.675f,.012f,.30f,.068f);
        var oldTab = old.deskTab.GetComponentInChildren<TextMeshProUGUI>(); Style(oldTab,36f); oldTab.alignment = TextAlignmentOptions.Center; oldTab.text = old.journal ? "기록 상세·경영" : "자동 운용";
        var legacyTab = old.legacyTab.GetComponentInChildren<TextMeshProUGUI>(); Style(legacyTab,36f); legacyTab.alignment = TextAlignmentOptions.Center; legacyTab.text = old.journal ? "보유 상세" : "자산·뉴스";
        var root = Rect("Panel :: Device Terminal",canvas,.015f,.095f,.97f,.89f); ui.panel = root.gameObject;
        var bg = Component<Image>(root.gameObject); bg.sprite = background; bg.type = UnityEngine.UI.Image.Type.Sliced; bg.fillCenter = true; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 2f; bg.raycastTarget = false;
        ui.stockName = Text("Selected Company",root,"선택 종목",.025f,.025f,.31f,.06f,46f);
        ui.ticker = Text("Selected Ticker",root,"",.025f,.095f,.31f,.04f,30f,true);
        ui.currentPrice = Text("Current Price",root,"0원",.36f,.02f,.29f,.09f,72f);
        ui.change = Text("Price Change",root,"",.36f,.115f,.29f,.045f,32f);
        var graphRect = Rect("Image :: Price History",root,.69f,.025f,.28f,.09f);
        Component<CanvasRenderer>(graphRect.gameObject);
        ui.graph = Component<DevicePriceGraph>(graphRect.gameObject); ui.graph.raycastTarget = false; ui.graph.color = Accent;
        ui.graphRange = Text("Price Reference",root,"",.69f,.125f,.28f,.05f,27f,true);
        Image("Header Divider",root,.025f,.185f,.95f,.0018f,new Color32(52,67,75,255));
        ui.feedback = Text("Device Feedback",root,"",.025f,.948f,.95f,.045f,29f,true);
        if (old.journal) { Journal(ui,root); }
        else { Orders(ui,root); }
        old.deskPanel.SetActive(false); old.legacyPanel.SetActive(false);
        return ui;
    }
    private static void Orders(DeviceTerminalUI ui, RectTransform root)
    {
        Text("Position Heading",root,"내 포지션",.025f,.21f,.43f,.055f,42f);
        ui.position = Text("Position Values",root,"",.025f,.275f,.43f,.31f,34f); ui.position.richText = true;
        ui.cash = Text("Available Assets",root,"",.025f,.60f,.43f,.10f,34f);
        ui.plan = Text("Plan Budget",root,"",.025f,.725f,.43f,.10f,32f);
        var track = Image("Budget Track",root,.025f,.835f,.43f,.006f,new Color32(52,67,75,255));
        ui.progress = Image("Budget Fill",track.transform,0,0,1,1,Accent); ui.progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); ui.progress.type = UnityEngine.UI.Image.Type.Filled; ui.progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        ui.budget = Button("Manual Budget",root,"예산 비율",.025f,.865f,.13f,.065f,30f);
        ui.prepare = Button("New Manual Plan",root,"새 계획",.168f,.865f,.13f,.065f,30f);
        ui.cancel = Button("Cancel Manual Order",root,"미체결 취소",.311f,.865f,.145f,.065f,30f);
        Image("Order Divider",root,.482f,.21f,.001f,.72f,new Color32(52,67,75,255));
        var area = Rect("Area :: Order Composer",root,.51f,.21f,.465f,.72f);
        var draft = Rect("Panel :: Order Draft",area,0,0,1,1); ui.draftPanel = draft.gameObject;
        Text("Order Heading",draft,"주문 설정",0,0,.45f,.08f,38f);
        ui.status = Text("Order State",draft,"미제출",.46f,0,.54f,.065f,29f,true); ui.status.alignment = TextAlignmentOptions.TopRight;
        ui.buy = Button("Manual Buy",draft,"매수",0,.09f,.49f,.09f,36f);
        ui.sell = Button("Manual Sell",draft,"매도",.51f,.09f,.49f,.09f,36f);
        Text("Order Type",draft,"주문 유형  지정가",0,.195f,.49f,.07f,30f);
        ui.split = Button("Split Toggle",draft,"분할 주문 OFF",.51f,.19f,.49f,.075f,30f); ui.splitLabel = ui.split.GetComponentInChildren<TextMeshProUGUI>();
        Text("Limit Price Caption",draft,"주문 가격 · 원",0,.28f,.49f,.065f,29f,true);
        Text("Quantity Caption",draft,"총 수량 · 주",.51f,.28f,.49f,.065f,29f,true);
        ui.price = Input("Limit Price",draft,"1",0,.35f,.49f,.09f);
        ui.quantity = Input("Order Quantity",draft,"1",.51f,.35f,.49f,.09f);
        var options = Rect("Panel :: Split Options",draft,0,.45f,1,.09f); ui.splitPanel = options.gameObject;
        Text("Split Count Caption",options,"횟수",0,.20f,.13f,.7f,29f,true);
        ui.parts = Input("Split Count",options,"3",.14f,0,.24f,1,33f);
        Text("Split Interval Caption",options,"간격·초",.51f,.20f,.18f,.7f,29f,true);
        ui.interval = Input("Split Interval",options,"3",.70f,0,.30f,1,33f);
        Text("Order Amount Caption",draft,"예상 주문 금액",0,.565f,.49f,.06f,32f);
        ui.amount = Text("Order Amount",draft,"",.48f,.555f,.52f,.075f,40f); ui.amount.alignment = TextAlignmentOptions.TopRight;
        ui.forecast = Text("Order Forecast",draft,"",0,.645f,1,.20f,29f,true);
        ui.validation = Text("Order Validation",draft,"",0,.85f,1,.05f,25f); ui.validation.color = new Color32(255,156,164,255);
        ui.review = Button("Review Order",draft,"주문 내용 확인",0,.905f,1,.095f,35f,true);
        var review = Rect("Panel :: Order Confirmation",area,0,0,1,1); ui.reviewPanel = review.gameObject;
        ui.confirmation = Text("Order Confirmation",review,"",0,.02f,1,.72f,37f);
        ui.submit = Button("Submit Order",review,"최종 제출",0,.78f,1,.09f,36f,true);
        ui.back = Button("Back To Draft",review,"주문 설정으로",0,.90f,1,.09f,34f);
        review.gameObject.SetActive(false); options.gameObject.SetActive(false);
    }
    private static void Journal(DeviceTerminalUI ui, RectTransform root)
    {
        ui.eventCount = Text("Event Count",root,"최근 사건",.025f,.21f,.34f,.05f,40f);
        Scroll("Events Scroll",root,.025f,.275f,.35f,.34f,out ui.eventContent);
        ui.eventButtons = new Button[40]; ui.eventTitles = new TextMeshProUGUI[40]; ui.eventTags = new TextMeshProUGUI[40];
        for (int i = 0; i < 40; i++)
        {
            var row = Button("Event " + i,ui.eventContent,"",0,0,1,1);
            var rect = (RectTransform)row.transform; rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,1);
            rect.sizeDelta = new Vector2(0,144); rect.anchoredPosition = new Vector2(0,-i*150);
            var label = row.GetComponentInChildren<TextMeshProUGUI>(); Layout(label.rectTransform,.025f,.07f,.95f,.53f); Style(label,31f);
            ui.eventButtons[i] = row; ui.eventTitles[i] = label;
            ui.eventTags[i] = Text("Event Classification",rect,"",.025f,.66f,.95f,.26f,27f,true);
            row.gameObject.SetActive(false);
        }
        Image("Event Divider",root,.395f,.21f,.001f,.41f,new Color32(52,67,75,255));
        Scroll("Event Detail Scroll",root,.42f,.21f,.555f,.32f,out var content);
        ui.eventDetail = Text("Event Detail",content,"",0,0,1,1,32f); ui.eventDetail.richText = true; ui.eventDetail.overflowMode = TextOverflowModes.Overflow;
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        var layout = Component<VerticalLayoutGroup>(content.gameObject); layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        Component<ContentSizeFitter>(content.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ui.eventOrder = Button("Event To Order",root,"이 종목 주문 화면으로",.42f,.55f,.555f,.065f,32f);
        Image("Fill Divider",root,.025f,.638f,.95f,.0018f,new Color32(52,67,75,255));
        Text("Fill Heading",root,"내 주문 · 실제 체결 기록",.025f,.655f,.95f,.045f,34f);
        ui.fills = Text("Player Fill History",root,"",.025f,.707f,.95f,.125f,29f);
        Image("Corporate Divider",root,.025f,.84f,.95f,.0018f,new Color32(52,67,75,255));
        ui.company = Text("Ownership Progress",root,"",.025f,.855f,.61f,.09f,27f,true);
        ui.growth = Button("Growth Permissions",root,"성장·권한",.69f,.865f,.285f,.065f,32f);
        var growth = Rect("Panel :: Growth Details",root,.025f,.20f,.95f,.735f); ui.growthPanel = growth.gameObject;
        var backdrop = Component<Image>(growth.gameObject); backdrop.color = Dark; backdrop.raycastTarget = true;
        Text("Growth Heading",growth,"시장 안에서의 나의 위치",.025f,.03f,.95f,.095f,43f);
        ui.growthText = Text("Growth Conditions",growth,"",.025f,.15f,.95f,.64f,35f);
        ui.openRooms = Button("Open Leading Rooms",growth,"리딩방 운영 시작",.025f,.85f,.60f,.11f,36f,true);
        ui.closeGrowth = Button("Close Growth Details",growth,"사건으로",.65f,.85f,.325f,.11f,34f);
        growth.gameObject.SetActive(false);
    }
    private static void Phone(PhoneCallController phone)
    {
        var root = (RectTransform)phone.title.transform.parent;
        var ui = Component<PhoneScreenUI>(root.gameObject); phone.screenUI = ui;
        Component<Image>(root.gameObject).color = Dark;
        ui.state = Text("Phone Status",root,"수신 중",.07f,.055f,.86f,.035f,42f,true); ui.state.alignment = TextAlignmentOptions.Center;
        Layout(phone.title.rectTransform,.07f,.12f,.86f,.10f); Style(phone.title,61f); phone.title.alignment = TextAlignmentOptions.Center;
        var incoming = Rect("Panel :: Incoming Call",root,.07f,.245f,.86f,.49f); ui.incoming = incoming.gameObject;
        ui.identity = Text("Caller Identity",incoming,"신원 미확인",0,0,1,.07f,39f,true); ui.identity.color = new Color32(230,185,99,255); ui.identity.alignment = TextAlignmentOptions.Center;
        ui.informationButton = Button("Caller Information",incoming,"발신 정보",.12f,.12f,.76f,.095f,39f);
        var info = Rect("Panel :: Caller Information",incoming,0,.24f,1,.22f); ui.information = info.gameObject;
        Text("Caller Information Detail",info,"동일 발신자 여부는 미확인입니다.\n발언은 확인된 공시와 구분해 기록합니다.",0,0,1,1,37f,true); info.gameObject.SetActive(false);
        ui.countdown = Text("Call Countdown",incoming,"",0,.53f,1,.09f,55f); ui.countdown.alignment = TextAlignmentOptions.Center;
        phone.answer.transform.SetParent(incoming,false); phone.decline.transform.SetParent(incoming,false);
        Layout((RectTransform)phone.answer.transform,0,.79f,.47f,.15f); Layout((RectTransform)phone.decline.transform,.53f,.79f,.47f,.15f);
        var answerText = phone.answer.GetComponentInChildren<TextMeshProUGUI>(); Style(answerText,40f); answerText.text = "받기\n내용 확인"; answerText.alignment = TextAlignmentOptions.Center;
        var declineText = phone.decline.GetComponentInChildren<TextMeshProUGUI>(); Style(declineText,40f); declineText.text = "거절\n이번 수신 종료"; declineText.alignment = TextAlignmentOptions.Center;
        phone.answer.image.color = new Color32(17,126,139,255); phone.decline.image.color = Field;
        var result = Scroll("Call Record Scroll",root,.07f,.26f,.86f,.44f,out var content); ui.result = result.gameObject;
        phone.body.transform.SetParent(content,false); Layout(phone.body.rectTransform,0,0,1,1); Style(phone.body,43f,true); phone.body.overflowMode = TextOverflowModes.Overflow;
        var layout = Component<VerticalLayoutGroup>(content.gameObject); layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        Component<ContentSizeFitter>(content.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Layout((RectTransform)phone.close.transform,.07f,.76f,.86f,.055f);
        var closeText = phone.close.GetComponentInChildren<TextMeshProUGUI>(); Style(closeText,40f); closeText.text = "화면 닫기"; closeText.alignment = TextAlignmentOptions.Center;
        ui.closeHint = Text("Close Phone Hint",root,"",.07f,.83f,.86f,.055f,32f,true); ui.closeHint.alignment = TextAlignmentOptions.Center;
        Text("Call History Heading",root,"최근 연락",.07f,.903f,.86f,.035f,34f);
        Scroll("Call History Scroll",root,.07f,.944f,.86f,.045f,out var history);
        ui.callHistory = Text("Call History",history,"기록 없음",0,0,1,1,28f,true); ui.callHistory.overflowMode = TextOverflowModes.Overflow;
        var historyLayout = Component<VerticalLayoutGroup>(history.gameObject); historyLayout.childControlHeight = true; historyLayout.childForceExpandHeight = false; historyLayout.childControlWidth = true; historyLayout.childForceExpandWidth = true;
        Component<ContentSizeFitter>(history.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var previousHistory = root.Find("Text :: Call History");
        if (previousHistory != null)
        { Object.DestroyImmediate(previousHistory.gameObject); }
    }
}
