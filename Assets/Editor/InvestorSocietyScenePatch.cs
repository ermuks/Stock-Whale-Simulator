using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 세 모니터와 두 태블릿 안에 투자자 사회를 연결한다. 화면 루트 위치/회전/카메라는 유지한다.</summary>
public static class InvestorSocietyScenePatch
{
    private static TMP_FontAsset font;
    private static Material secondary;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(138, 150, 163, 255), Dark = new Color32(20, 28, 32, 255);
    [MenuItem("DonDon/Apply Investor Society Patch")]
    public static void Apply()
    {
        NightShiftScenePatch.Apply();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        const string materialPath = "Assets/Resources/UI/TradingTerminal/Secondary_Text.mat";
        secondary = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (secondary == null)
        {
            secondary = new Material(font.material); secondary.name = "Secondary Text";
            secondary.SetFloat("_FaceDilate", -.065f); AssetDatabase.CreateAsset(secondary, materialPath);
        }
        var systems = Object.FindAnyObjectByType<TradingOperationController>().gameObject;
        if (systems.GetComponent<InvestorSociety>() == null) { systems.AddComponent<InvestorSociety>(); }
        if (systems.GetComponent<SocialContactController>() == null) { systems.AddComponent<SocialContactController>(); }
        if (systems.GetComponent<WorldDramaDirector>() == null) { systems.AddComponent<WorldDramaDirector>(); }
        var feed = systems.GetComponent<MarketEventFeed>() ?? systems.AddComponent<MarketEventFeed>();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var right = canvases.Single(c => c.name == "Panel :: Chat Screen");
        if (right.GetComponent<LeadingRoomUI>() == null) { Chat(right); }
        Chart(canvases.Single(c => c.name == "Panel :: Chart Screen"));
        Tablets();
        var stockScreen = canvases.Single(c => c.name == "Panel :: Stock List Screen");
        var monitor = stockScreen.transform.Find("Panel :: Operation Monitor");
        Layout((RectTransform)monitor, .035f, .81f, .93f, .17f);
        monitor.GetComponent<Image>().color = Dark;
        var label = monitor.GetComponentsInChildren<TextMeshProUGUI>().First(); label.text = "체결 흐름"; label.fontSize = 36f;
        var stockUi = Object.FindObjectsByType<DeskOperationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(c => !c.journal);
        stockUi.monitorStatus.fontSize = 32f; stockUi.monitorStatus.fontSharedMaterial = secondary;
        var scroll = stockScreen.GetComponentInChildren<ScrollRect>(true);
        ((RectTransform)scroll.transform).offsetMin = new Vector2(((RectTransform)scroll.transform).offsetMin.x, 640f);
        var hud = canvases.Single(c => c.name == "Panel :: Interaction HUD");
        var hint = hud.transform.Find("Text :: Desk Controls").GetComponent<TextMeshProUGUI>();
        hint.gameObject.SetActive(false); // 기존 HUD의 DeskControlHints가 SDF 키캡 안내를 담당한다.
        if (feed.eventText == null)
        { feed.eventText = Text("Market Event", hud.transform, "", .25f, .055f, .5f, .07f, 21f, true); feed.eventText.alignment = TextAlignmentOptions.Top; }
        if (systems.GetComponent<PhoneCallController>() == null) { Phone(systems, canvases, hud); }
        var phoneScreen = canvases.Single(c => c.name == "Panel :: Phone Screen");
        phoneScreen.gameObject.SetActive(true); phoneScreen.enabled = true;
        var phoneController = systems.GetComponent<PhoneCallController>();
        if (phoneScreen.transform.parent != phoneController.grip)
        {
            var imported = phoneScreen.transform.parent;
            phoneScreen.transform.SetParent(phoneController.grip, true);
            imported.RotateAround(phoneController.grip.position, phoneController.grip.up, 180f);
            phoneScreen.transform.localPosition = new Vector3(0f, 0f, -.12f);
            phoneScreen.transform.localRotation = Quaternion.identity;
        }
        phoneController.grip.localPosition = new Vector3(.05f, -.03f, 1.6f);
        var screenScale = phoneScreen.transform.localScale;
        phoneScreen.transform.localScale = new Vector3(Mathf.Abs(screenScale.x), Mathf.Abs(screenScale.y), Mathf.Abs(screenScale.z));
        var bones = phoneController.grip.GetComponentsInChildren<Transform>(true);
        var thumb = bones.FirstOrDefault(t => t.name == "thumb.01.L");
        var thumbTip = bones.FirstOrDefault(t => t.name == "thumb.03.L_end");
        if (thumb != null && thumbTip != null)
        {
            Vector3 target = phoneController.grip.TransformPoint(new Vector3(.44f, .25f, .1f));
            thumb.rotation = Quaternion.FromToRotation(thumbTip.position - thumb.position, target - thumb.position) * thumb.rotation;
        }
        if (phoneController.grip.Find("Area :: Phone Screen Spill") == null)
        {
            var spill = new GameObject("Area :: Phone Screen Spill").AddComponent<Light>();
            spill.transform.SetParent(phoneController.grip, false); spill.transform.localPosition = new Vector3(0f, .1f, -.4f);
            spill.type = LightType.Point; spill.range = 1.5f; spill.intensity = .04f;
            spill.color = new Color(.45f, .57f, .58f); spill.shadows = LightShadows.None;
        }
        phoneController.grip.Find("Area :: Phone Screen Spill").GetComponent<Light>().intensity = .04f;
        Setup();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Investor society, room controls, phone grip and visual hierarchy connected.");
    }
    public static void ApplyBatch() { Apply(); EditorApplication.Exit(0); }
    private static void Layout(RectTransform rect, float x, float y, float w, float h)
    {
        rect.localScale = Vector3.one; rect.pivot = new Vector2(.5f, .5f);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); Layout(rect, x, y, w, h); return rect;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, string value, float x, float y, float w, float h, float size, bool muted = false)
    {
        var text = Rect("Text :: " + name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.enableAutoSizing = false;
        text.text = value; text.color = muted ? Muted : Ink; text.raycastTarget = false; text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        if (muted) { text.fontSharedMaterial = secondary; }
        return text;
    }
    private static Button Button(string name, Transform parent, string caption, float x, float y, float w, float h, float size)
    {
        var rect = Rect("Button :: " + name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(29, 39, 43, 255);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.6f, .85f, .9f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
        var label = Text(name + " Label", rect, caption, .03f, .08f, .94f, .84f, size);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static TMP_InputField Input(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = Rect("Area :: " + name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/TradingTerminal/Chat_Input.png");
        image.type = Image.Type.Sliced; image.color = Color.white;
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image;
        var viewport = Rect("Area :: Input Viewport", rect, .06f, .1f, .88f, .8f); viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text(name + " Value", viewport, "", 0, 0, 1, 1, 40f); label.alignment = TextAlignmentOptions.MidlineRight;
        input.textViewport = viewport; input.textComponent = label; input.fontAsset = font;
        input.contentType = TMP_InputField.ContentType.IntegerNumber; input.characterLimit = 12; input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }
    private static void Chat(Canvas canvas)
    {
        var chat = canvas.GetComponent<TabletGroupChatUI>() ?? canvas.GetComponentInChildren<TabletGroupChatUI>(true);
        var ui = canvas.gameObject.AddComponent<LeadingRoomUI>(); ui.chat = chat;
        ui.publicRoom = Button("Public Lounge", canvas.transform, "전체", .025f, .055f, .22f, .033f, 40f);
        ui.roomOne = Button("Leading Room One", canvas.transform, "리딩방 1", .27f, .055f, .22f, .033f, 40f);
        ui.roomTwo = Button("Leading Room Two", canvas.transform, "리딩방 2", .51f, .055f, .22f, .033f, 40f);
        ui.roomThree = Button("Leading Room Three", canvas.transform, "리딩방 3", .755f, .055f, .22f, .033f, 40f);
        Layout((RectTransform)chat.scroll.transform, .025f, .105f, .95f, .57f);
        Layout(chat.profile.rectTransform, .035f, .685f, .72f, .075f); chat.profile.fontSize = 30f; chat.profile.fontSharedMaterial = secondary; chat.profile.color = Muted;
        chat.profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요.";
        Layout((RectTransform)chat.newestButton.transform, .79f, .69f, .18f, .028f);
        ui.invite = Button("Invite Selected Investor", canvas.transform, "회유·초대", .77f, .732f, .2f, .032f, 32f);
        ui.commandStatus = Text("Room Command State", canvas.transform, "", .035f, .778f, .93f, .065f, 33f, true);
        Text("Buy Trigger Label", canvas.transform, "매수 이하 (돈)", .035f, .853f, .28f, .02f, 28f, true);
        Text("Sell Trigger Label", canvas.transform, "매도 이상 (돈)", .36f, .853f, .28f, .02f, 28f, true);
        ui.buyPrice = Input("Buy Trigger", canvas.transform, .035f, .877f, .29f, .034f);
        ui.sellPrice = Input("Sell Trigger", canvas.transform, .36f, .877f, .29f, .034f);
        ui.issue = Button("Issue Room Command", canvas.transform, "지시", .69f, .877f, .13f, .034f, 34f);
        ui.cancel = Button("Cancel Room Command", canvas.transform, "취소", .84f, .877f, .13f, .034f, 34f);
        ui.feedback = Text("Room Feedback", canvas.transform, "", .035f, .924f, .93f, .05f, 27f, true);
        foreach (var text in chat.rowTemplate.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.color = text.name.Contains("Message") ? Ink : Muted;
            text.fontSize = text.name.Contains("Message") ? 43f : text.name.Contains("Nickname") ? 34f : 26f;
            text.enableAutoSizing = false; text.fontSharedMaterial = secondary;
        }
        chat.rowTemplate.Find("Image :: Bubble").GetComponent<Image>().color = new Color(.78f, .84f, .87f, .42f);
        chat.rowTemplate.Find("Image :: Avatar").GetComponent<Image>().color = new Color(.65f, .72f, .76f, 1f);
        chat.rowHeight = 194f;
    }
    private static void Place(TextMeshProUGUI text, Transform parent, float x, float y, float w, float h, float size, bool muted = false)
    {
        text.transform.SetParent(parent, false); Layout(text.rectTransform, x, y, w, h);
        text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = muted ? Muted : Ink;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
        if (muted) { text.fontSharedMaterial = secondary; }
    }
    private static void Chart(Canvas canvas)
    {
        var manager = Object.FindAnyObjectByType<ChartManager>();
        var header = (RectTransform)canvas.transform.Find("Panel :: Stock Header");
        Layout(header, 0, 0, 1, .18f);
        Place(manager.textSymbol, header, .025f, .07f, .10f, .23f, 25f, true);
        Place(manager.textName, header, .14f, .04f, .43f, .28f, 36f);
        if (manager.textRiskBadge == null) { manager.textRiskBadge = Text("Risk Badge", header, "", .59f, .08f, .11f, .22f, 23f, true); }
        Place(manager.textLastPrice, header, .025f, .35f, .28f, .57f, 76f);
        Place(manager.textChangePct, header, .32f, .44f, .18f, .35f, 37f);
        Place(manager.textRealValue, header, .54f, .64f, .12f, .25f, 23f, true);
        Place(manager.textFakeValue, header, .69f, .64f, .12f, .25f, 23f, true);
        Place(manager.textVolume, header, .83f, .28f, .145f, .25f, 25f, true);
        Place(manager.textTurnover, header, .80f, .75f, .175f, .25f, 25f, true);
        manager.textVolume.alignment = manager.textTurnover.alignment = TextAlignmentOptions.MidlineRight;
        if (header.Find("Text :: Volume Caption") == null)
        {
            Text("Volume Caption", header, "거래량", .72f, .13f, .1f, .23f, 19f, true);
            Text("Turnover Caption", header, "거래대금", .70f, .38f, .1f, .23f, 19f, true);
            Text("Real Caption", header, "장부 가치", .54f, .46f, .12f, .18f, 18f, true);
            Text("Fake Caption", header, "시장 평가", .69f, .46f, .12f, .18f, 18f, true);
        }
        Layout((RectTransform)header.Find("Text :: Volume Caption"), .83f, .08f, .145f, .18f);
        Layout((RectTransform)header.Find("Text :: Turnover Caption"), .83f, .55f, .145f, .18f);
        var selected = Object.FindAnyObjectByType<ChartCandleSelectData>();
        Layout(selected.panelRoot, .023f, .20f, .61f, .07f);
        var draw = manager.candleRenderer;
        draw.transform.localPosition = new Vector3(-897.5f, -290f, -1f); draw.transform.localScale = new Vector3(50f, 40f, 1f); draw.volumeChartHeight = 3.5f;
        var book = Object.FindAnyObjectByType<OrderBookUIManager>();
        Layout((RectTransform)book.sellParent, .642f, .305f, .215f, .32f);
        Layout((RectTransform)book.buyParent, .642f, .625f, .215f, .32f);
        Layout((RectTransform)book.toggleButton.transform, .642f, .265f, .215f, .036f);
        book.toggleButtonText.fontSize = 24f;
        foreach (var text in book.sellParent.GetComponentsInChildren<TextMeshProUGUI>(true).Concat(book.buyParent.GetComponentsInChildren<TextMeshProUGUI>(true)))
        { text.fontSize = 25f; text.enableAutoSizing = false; text.fontSharedMaterial = secondary; }
        foreach (var text in canvas.transform.Find("Panel :: Market Statistics And Trades").GetComponentsInChildren<TextMeshProUGUI>(true))
        { text.fontSharedMaterial = secondary; if (text.name.Contains("Title")) { text.color = Muted; } }
    }
    private static void Tablets()
    {
        foreach (var ui in Object.FindObjectsByType<DeskOperationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ui.deskTab.GetComponentInChildren<TextMeshProUGUI>().text = ui.journal ? "사건·경영" : "자산·주문";
            foreach (var image in ui.GetComponentsInChildren<Image>(true))
            { if (image.GetComponent<Button>() != null) { image.color = new Color32(29, 39, 43, 255); } }
            foreach (var text in ui.GetComponentsInChildren<TextMeshProUGUI>(true))
            { if (text.fontSize < 36f) { text.fontSharedMaterial = secondary; text.color = Muted; } }
            ui.heading.fontSize = 48f; ui.stateText.fontSize = 32f; ui.stateText.color = Muted;
            ui.details.fontSize = 31f; ui.history.fontSize = 28f; ui.history.color = Muted;
            var label = ui.deskPanel.transform.Find("Text :: Operation Heat Label");
            if (label != null) { label.GetComponent<TextMeshProUGUI>().text = ui.journal ? "" : "주문 예산 사용"; }
            ui.heatFill.transform.parent.gameObject.SetActive(false);
            if (ui.journal)
            {
                ui.progressFill.transform.parent.gameObject.SetActive(false);
                var corporate = ui.GetComponent<CorporateControlUI>() ?? ui.gameObject.AddComponent<CorporateControlUI>();
                if (corporate.management == null)
                {
                    corporate.status = Text("Corporate Control", ui.deskPanel.transform, "", .04f, .85f, .92f, .04f, 25f, true);
                    corporate.management = Button("Management Decision", ui.deskPanel.transform, "경영진", .04f, .91f, .20f, .07f, 29f);
                    corporate.investment = Button("Business Investment", ui.deskPanel.transform, "신사업", .28f, .91f, .20f, .07f, 29f);
                    corporate.dividend = Button("Funded Dividend", ui.deskPanel.transform, "자비 배당", .52f, .91f, .20f, .07f, 29f);
                    corporate.acquisition = Button("Acquire Equity", ui.deskPanel.transform, "지분 매입", .76f, .91f, .20f, .07f, 29f);
                }
            }
            else
            {
                ui.build.GetComponentInChildren<TextMeshProUGUI>().text = "지정가 매수";
                ui.drive.GetComponentInChildren<TextMeshProUGUI>().text = "즉시 분할 매수";
                ui.unwind.GetComponentInChildren<TextMeshProUGUI>().text = "분할 매도";
                ui.pause.GetComponentInChildren<TextMeshProUGUI>().text = "관망";
                ui.finish.GetComponentInChildren<TextMeshProUGUI>().text = "계획 종료";
            }
        }
    }
    private static void Phone(GameObject systems, Canvas[] canvases, Canvas hud)
    {
        var screen = canvases.Single(c => c.name == "Panel :: Phone Screen");
        var hand = screen.transform.parent;
        var grip = new GameObject("Area :: Phone Grip Pivot").transform;
        grip.SetPositionAndRotation(screen.transform.position, screen.transform.rotation);
        hand.SetParent(grip, true); hand.name = "Area :: Imported Hand And Phone"; hand.gameObject.SetActive(true);
        grip.SetParent(Camera.main.transform, true);
        grip.localPosition = new Vector3(.25f, -.05f, 1.35f); grip.localRotation = Quaternion.Euler(0f, -6f, 0f); grip.localScale = Vector3.one * .65f;
        var phone = systems.AddComponent<PhoneCallController>(); phone.grip = grip;
        foreach (var light in hand.GetComponentsInChildren<Light>(true)) { light.enabled = false; }
        foreach (var child in screen.transform.Cast<Transform>()) { child.gameObject.SetActive(false); }
        var panel = Rect("Panel :: Phone Calls", screen.transform, .025f, .025f, .95f, .95f);
        panel.localPosition += new Vector3(0f, 0f, -5f);
        var background = panel.gameObject.AddComponent<Image>(); background.color = new Color32(12, 20, 24, 255); background.raycastTarget = false;
        phone.title = Text("Caller", panel, "연락 기록", .07f, .10f, .86f, .12f, 65f);
        phone.body = Text("Call Content", panel, "", .07f, .27f, .86f, .47f, 48f, true);
        phone.answer = Button("Answer Call", panel, "받기", .06f, .80f, .40f, .07f, 46f);
        phone.decline = Button("Decline Call", panel, "거절", .54f, .80f, .40f, .07f, 46f);
        phone.close = Button("Lower Phone", panel, "내려놓기", .06f, .91f, .88f, .06f, 40f);
        phone.alert = Text("Phone Alert", hud.transform, "", .78f, .73f, .18f, .04f, 22f, true);
        screen.worldCamera = Camera.main;
        if (screen.GetComponent<GraphicRaycaster>() == null) { screen.gameObject.AddComponent<GraphicRaycaster>(); }
        grip.gameObject.SetActive(false);
    }
    private static void Setup()
    {
        var manager = Object.FindAnyObjectByType<StockManager>(); manager.economy.retailPlayerStart = true;
    }
}
