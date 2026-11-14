using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Migrates existing tablet subpanels, never replaces their Canvas or 3D model.</summary>
public static class AccountDashboardPatch
{
    private static TMP_FontAsset font;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(166,177,186,255), Dark = new Color32(17,25,30,255), Line = new Color32(58,73,82,255), Accent = new Color32(37,175,192,255);
    private const string Output = "Validation/AccountDashboard/";
    [MenuItem("DonDon/Apply Account Dashboard")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        if (Object.FindAnyObjectByType<TabletAccountUI>(FindObjectsInactive.Include) != null)
        { return; }
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => !(t is RectTransform)).ToArray();
        var poses = transforms.Select(Pose).ToArray();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        var canvasState = canvases.Select(c => EditorJsonUtility.ToJson(c) + Pose(c.transform)).ToArray();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        var icon = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/UI/TradingTerminal/Icons/Icon_Lock.png");
        icon.textureType = TextureImporterType.Sprite; icon.spriteImportMode = SpriteImportMode.Single; icon.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings(); icon.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = 9; settings.spritePivot = new Vector2(.5f,.5f); icon.SetTextureSettings(settings);
        icon.alphaIsTransparency = true; icon.mipmapEnabled = false; icon.wrapMode = TextureWrapMode.Clamp; icon.filterMode = FilterMode.Bilinear;
        icon.textureCompression = TextureImporterCompression.Uncompressed; icon.maxTextureSize = 1024; icon.SaveAndReimport();
        var existing = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include);
        var first = existing.Single(x => !x.journal); var second = existing.Single(x => x.journal);
        var operation = Object.FindAnyObjectByType<TradingOperationController>();
        var accounts = operation.gameObject.AddComponent<PlayerAccounts>(); accounts.primary = StockManagerInScene().playerAccount;
        var secondaryRoot = new GameObject("Area :: Automated Account",typeof(RectTransform)); secondaryRoot.transform.SetParent(operation.transform,false);
        accounts.automated = secondaryRoot.AddComponent<Trader>();
        accounts.primaryOrders = first.session; accounts.primaryOrders.account = accounts.primary;
        accounts.automatedOrders = secondaryRoot.AddComponent<DeviceOrderSession>(); accounts.automatedOrders.account = accounts.automated;
        var firstRecords = first.records; firstRecords.account = accounts.primary;
        var secondRecords = secondaryRoot.AddComponent<DeviceMarketJournal>(); secondRecords.account = accounts.automated;
        operation.account = accounts.automated;
        var firstContent = Wrap(first.transform); var secondContent = Wrap(second.transform);
        var firstEvents = CloneScreen(second,firstContent); var secondOrders = CloneScreen(first,secondContent);
        Configure(first,firstEvents,firstContent,accounts,false,firstRecords,first.thisTablet,second.thisTablet);
        Configure(secondOrders,second,secondContent,accounts,true,secondRecords,second.thisTablet,first.thisTablet);
        foreach (var ui in Object.FindObjectsByType<InvestorJourneyUI>(FindObjectsInactive.Include)) { ui.orders = first; ui.journal = firstEvents; }
        foreach (var ui in Object.FindObjectsByType<LeadingRoomUI>(FindObjectsInactive.Include)) { ui.orderScreen = first; ui.growthScreen = firstEvents; }
        for (int i = 0; i < transforms.Length; i++)
        { if (Pose(transforms[i]) != poses[i]) { throw new Exception("3D pose changed: " + transforms[i].name); } }
        for (int i = 0; i < canvases.Length; i++)
        { if (EditorJsonUtility.ToJson(canvases[i]) + Pose(canvases[i].transform) != canvasState[i]) { throw new Exception("Canvas changed: " + canvases[i].name); } }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"scene-preservation.txt","PASS " + transforms.Length + " existing 3D transforms and " + canvases.Length + " Canvas configurations/poses unchanged\n");
    }
    private static StockManager StockManagerInScene() => Object.FindAnyObjectByType<StockManager>();
    private static RectTransform Wrap(Transform canvas)
    {
        var old = canvas.Cast<Transform>().Where(t => t is RectTransform).ToArray();
        var root = Rect("Area :: Account Content",canvas,0,0,1,1);
        foreach (var child in old) { child.SetParent(root,false); }
        return root;
    }
    private static DeviceTerminalUI CloneScreen(DeviceTerminalUI source,Transform parent)
    {
        var host = Rect("Area :: " + (source.journal ? "Account Events" : "Account Orders"),parent,0,0,1,1);
        var old = host.gameObject.AddComponent<DeskOperationUI>(); var ui = host.gameObject.AddComponent<DeviceTerminalUI>();
        var map = new Dictionary<Object,Object> { [source] = ui, [source.legacy] = old, [source.gameObject] = host.gameObject, [source.transform] = host };
        var roots = new[]{source.panel,source.mainTab.gameObject,source.legacy.deskPanel,source.legacy.legacyPanel,source.legacy.deskTab.gameObject,source.legacy.legacyTab.gameObject};
        var clones = new List<GameObject>();
        foreach (var root in roots)
        {
            var copy = Object.Instantiate(root,host); copy.name = root.name; clones.Add(copy);
            var from = root.GetComponentsInChildren<Transform>(true); var to = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < from.Length; i++)
            {
                map[from[i].gameObject] = to[i].gameObject;
                var a = from[i].GetComponents<Component>(); var b = to[i].GetComponents<Component>();
                for (int j = 0; j < a.Length; j++) { if (a[j] != null) { map[a[j]] = b[j]; } }
            }
        }
        EditorUtility.CopySerialized(source,ui); EditorUtility.CopySerialized(source.legacy,old);
        Remap(ui,map); Remap(old,map); old.monitorStatus = null;
        var corporate = source.GetComponent<CorporateControlUI>();
        if (corporate != null)
        { var copy = host.gameObject.AddComponent<CorporateControlUI>(); EditorUtility.CopySerialized(corporate,copy); Remap(copy,map); }
        foreach (var clone in clones)
        { foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true)) { if (component != null) { Remap(component,map); } } }
        return ui;
    }
    private static void Remap(Object target,Dictionary<Object,Object> map)
    {
        var serialized = new SerializedObject(target); var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && map.TryGetValue(property.objectReferenceValue,out var replacement))
            { property.objectReferenceValue = replacement; }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Configure(DeviceTerminalUI orders,DeviceTerminalUI events,RectTransform content,PlayerAccounts accounts,bool second,DeviceMarketJournal records,TabletViewController tablet,TabletViewController other)
    {
        var nav = content.parent.gameObject.AddComponent<TabletAccountUI>(); nav.accounts = accounts; nav.second = second;
        nav.account = second ? accounts.automated : accounts.primary; nav.orders = orders; nav.events = events; nav.contentRoot = content.gameObject;
        foreach (var ui in new[]{orders,events})
        {
            ui.navigation = nav; ui.session = second ? accounts.automatedOrders : accounts.primaryOrders; ui.records = records;
            ui.thisTablet = tablet; ui.pairedTablet = other; ui.orderTablet = tablet; ui.orderScreen = orders;
            ui.mainTab.gameObject.SetActive(false); ui.legacy.deskTab.gameObject.SetActive(false); ui.legacy.legacyTab.gameObject.SetActive(false);
            ui.legacy.displayAccount = nav.account; ui.legacy.managedNavigation = true;
            var corporate = ui.GetComponent<CorporateControlUI>(); if (corporate != null) { corporate.account = nav.account; }
        }
        nav.holdings = events.legacy.legacyPanel.GetComponent<HoldUIManager>(); nav.holdings.account = nav.account; nav.holdings.activeTabImage = null;
        var background = Image("Account Background",content,0,0,1,1,Dark); background.name = "Panel :: Account Input Surface"; background.raycastTarget = true; background.transform.SetAsFirstSibling();
        nav.tabs = new Button[4]; nav.tabLines = new Image[4]; string[] captions = {"대시보드","사건·경영","거래 기록","보유 상세"};
        for (int i = 0; i < 4; i++)
        {
            var button = Button("Account Tab " + i,content,captions[i],.015f+i*.18f,.012f,.175f,.065f,36);
            nav.tabs[i] = button; button.image.color = Color.clear;
            nav.tabLines[i] = Image("Active Tab",button.transform,0,.94f,1,.06f,Accent);
        }
        nav.clock = Text("Market Clock",content,"DAY —",.745f,.018f,.235f,.05f,32,true,true);
        Image("Navigation Divider",content,.015f,.087f,.97f,.0015f,Line);
        BuildDashboard(nav,content);
        var recordPanel = Rect("Panel :: Account Trade Records",content,.02f,.11f,.96f,.86f); nav.recordsPanel = recordPanel.gameObject;
        Text("Record Heading",recordPanel,"내 계좌 · 실제 체결 기록",.02f,.015f,.96f,.08f,46);
        Scroll("Trade Records",recordPanel,.02f,.14f,.96f,.84f,out nav.ledgerContent);
        nav.ledger = Text("Account Executions",nav.ledgerContent,"",0,0,1,1,34); nav.ledger.textWrappingMode = TextWrappingModes.Normal; nav.ledger.overflowMode = TextOverflowModes.Overflow;
        nav.managementButton = Button("Company Management",events.panel.transform,"경영·기록",.025f,.865f,.25f,.065f,30);
        // Existing company ownership stays visible; move its narrow caption rather than covering it.
        Layout(events.company.rectTransform,.30f,.85f,.36f,.09f); events.company.fontSize = 25;
        BuildTransfer(nav,content);
        nav.screens = new[]{nav.dashboard.gameObject,recordPanel.gameObject,orders.panel,events.panel,orders.legacy.deskPanel,orders.legacy.legacyPanel,events.legacy.deskPanel,events.legacy.legacyPanel};
        foreach (var screen in nav.screens) { screen.SetActive(false); }
        nav.dashboard.gameObject.SetActive(true);
        var locked = Rect("Panel :: Locked Account",content.parent,0,0,1,1); nav.lockRoot = locked.gameObject;
        var lockBackground = locked.gameObject.AddComponent<Image>(); lockBackground.color = Dark; lockBackground.raycastTarget = true;
        var lockImage = Image("Account Lock",locked,.435f,.395f,.13f,.21f,Color.white);
        lockImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/TradingTerminal/Icons/Icon_Lock.png"); lockImage.preserveAspect = true;
        locked.gameObject.SetActive(second); content.gameObject.SetActive(!second);
        tablet.scrollRect = nav.dashboard.scroll;
    }
    private static void BuildDashboard(TabletAccountUI nav,Transform parent)
    {
        var root = Rect("Panel :: Investment Dashboard",parent,.02f,.105f,.96f,.88f);
        var ui = root.gameObject.AddComponent<InvestmentDashboard>(); nav.dashboard = ui; ui.account = nav.account; ui.navigation = nav;
        nav.accountTitle = Text("Account Title",root,"내 투자",.015f,0,.5f,.045f,31,true);
        Text("Valuation Caption",root,"주식 평가금",.015f,.062f,.25f,.075f,43);
        ui.valuation = Text("Portfolio Valuation",root,"—",.265f,.045f,.34f,.10f,65,false,true);
        ui.profit = Text("Unrealized Profit",root,"—",.015f,.156f,.60f,.065f,43);
        Image("Summary Divider",root,.63f,.045f,.001f,.18f,Line);
        Text("Available Cash Caption",root,"주문 가능 현금",.657f,.06f,.20f,.05f,30,true);
        ui.available = Text("Available Cash",root,"—",.81f,.055f,.175f,.058f,35,false,true);
        Text("Total Assets Caption",root,"총자산",.657f,.144f,.15f,.05f,30,true);
        ui.total = Text("Total Assets",root,"—",.79f,.139f,.195f,.058f,35,false,true);
        Image("Summary Line",root,.005f,.253f,.99f,.0015f,Line);
        ui.count = Text("Holding Count",root,"보유 종목 0",.015f,.279f,.5f,.055f,43);
        ui.sort = Button("Sort Holdings",root,"평가금 높은 순",.72f,.27f,.265f,.064f,31); ui.sortLabel = ui.sort.GetComponentInChildren<TextMeshProUGUI>();
        Text("Company Column",root,"종목 · 보유 수량",.015f,.36f,.35f,.045f,31,true);
        Text("Price Column",root,"현재가 · 내 평균",.36f,.36f,.21f,.045f,31,true,true);
        Text("Valuation Column",root,"평가금",.58f,.36f,.19f,.045f,31,true,true);
        Text("Profit Column",root,"평가손익 · 수익률",.78f,.36f,.205f,.045f,31,true,true);
        Image("List Header Line",root,.005f,.408f,.99f,.0015f,Line);
        ui.scroll = Scroll("Holdings",root,.005f,.419f,.99f,.43f,out ui.content);
        ui.rowTemplate = BuildRow(ui.content); ui.rowTemplate.gameObject.SetActive(false);
        ui.empty = Text("No Holdings",root,"아직 보유한 종목이 없습니다",.015f,.53f,.97f,.14f,38,true); ui.empty.alignment = TextAlignmentOptions.Center;
        Image("Footer Divider",root,.005f,.865f,.99f,.0015f,Line);
        ui.cost = Text("Total Cost",root,"총 매수원금 —",.015f,.89f,.43f,.055f,29,true);
        nav.transferButton = Button("Transfer Cash",root,"계좌 이체",.015f,.95f,.18f,.049f,27);
        nav.autoButton = Button("Auto Operation",root,"자동 운용",.21f,.95f,.18f,.049f,27); nav.autoButton.gameObject.SetActive(nav.second);
        nav.orderButton = Button("Account Order",root,"매수·매도",.495f,.893f,.19f,.080f,34);
        ui.detail = Button("Selected Position",root,"선택 종목 상세  ›",.705f,.893f,.28f,.080f,34);
    }
    private static InvestmentPositionRow BuildRow(Transform parent)
    {
        var root = Rect("Button :: Position Template",parent,0,0,1,1); var ui = root.gameObject.AddComponent<InvestmentPositionRow>();
        ui.background = root.gameObject.AddComponent<Image>(); ui.background.color = Color.clear; ui.background.raycastTarget = true;
        ui.button = root.gameObject.AddComponent<Button>(); ui.button.targetGraphic = ui.background; ui.button.transition = Selectable.Transition.None;
        ui.selection = Image("Selection",root,0,.03f,.004f,.94f,Accent);
        ui.company = Text("Company",root,"",.015f,.025f,.345f,.52f,35);
        ui.quantity = Text("Owned Quantity",root,"",.015f,.58f,.345f,.38f,25,true);
        ui.price = Text("Market Price",root,"",.36f,.025f,.21f,.52f,35,false,true);
        ui.average = Text("Acquisition Average",root,"",.36f,.58f,.21f,.38f,25,true,true);
        ui.value = Text("Position Valuation",root,"",.58f,.21f,.19f,.58f,35,false,true);
        ui.profit = Text("Position Profit",root,"",.78f,.025f,.205f,.52f,35,false,true);
        ui.percent = Text("Position Return",root,"",.78f,.58f,.205f,.38f,25,false,true);
        Image("Row Divider",root,.008f,.99f,.985f,.008f,Line);
        return ui;
    }
    public static void FinalizeInputSurface()
    {
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        foreach (var nav in Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include))
        {
            var surface = nav.contentRoot.transform.Find("Image :: Account Background") ?? nav.contentRoot.transform.Find("Panel :: Account Input Surface");
            surface.name = "Panel :: Account Input Surface"; surface.GetComponent<Image>().raycastTarget = true;
            nav.dashboard.sortLabel.text = "평가금 높은 순";
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
    private static void BuildTransfer(TabletAccountUI nav,Transform parent)
    {
        var root = Rect("Panel :: Account Transfer",parent,.18f,.26f,.64f,.49f); nav.transferPanel = root.gameObject;
        var image = root.gameObject.AddComponent<Image>(); image.color = new Color32(24,35,42,255); image.raycastTarget = true;
        nav.transferTitle = Text("Transfer Direction",root,"",.06f,.07f,.88f,.12f,42);
        Text("Transfer Amount Caption",root,"이체 금액 · 원",.06f,.26f,.88f,.09f,32,true);
        var inputRoot = Rect("Area :: Transfer Amount",root,.06f,.38f,.88f,.16f);
        var bg = inputRoot.gameObject.AddComponent<Image>(); bg.color = Dark;
        var input = inputRoot.gameObject.AddComponent<TMP_InputField>(); nav.transferAmount = input; input.targetGraphic = bg;
        var viewport = Rect("Area :: Input Viewport",inputRoot,.03f,.04f,.94f,.92f); viewport.gameObject.AddComponent<RectMask2D>();
        input.textViewport = viewport; input.textComponent = Text("Transfer Input",viewport,"",0,0,1,1,40,false,true); input.fontAsset = font;
        input.contentType = TMP_InputField.ContentType.IntegerNumber; input.characterLimit = 15;
        nav.transferStatus = Text("Transfer Result",root,"",.06f,.58f,.88f,.15f,30,true);
        nav.transferSubmit = Button("Confirm Transfer",root,"이체",.06f,.79f,.43f,.14f,36);
        nav.transferCancel = Button("Close Transfer",root,"닫기",.51f,.79f,.43f,.14f,36);
        root.gameObject.SetActive(false);
    }
    private static string Pose(Transform value) => (value.parent == null ? "root" : value.parent.GetEntityId().ToString()) + value.localPosition.ToString("R") + value.localRotation.ToString("R") + value.localScale.ToString("R") + (value is RectTransform r ? r.anchorMin.ToString("R")+r.anchorMax.ToString("R")+r.sizeDelta.ToString("R")+r.pivot.ToString("R") : "");
    private static void Layout(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y); rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity; rect.anchoredPosition3D = new Vector3(rect.anchoredPosition.x,rect.anchoredPosition.y,0); }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    { var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); Layout(rect,x,y,w,h); return rect; }
    private static TextMeshProUGUI Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,bool muted=false,bool right=false)
    {
        var text = Rect("Text :: "+name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size; text.fontStyle = FontStyles.Normal; text.enableAutoSizing = false;
        text.text = value; text.color = muted ? Muted : Ink; text.raycastTarget = false; text.richText = false; text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        return text;
    }
    private static Image Image(string name,Transform parent,float x,float y,float w,float h,Color color)
    { var image = Rect("Image :: "+name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    private static Button Button(string name,Transform parent,string caption,float x,float y,float w,float h,float size)
    {
        var image = Image(name,parent,x,y,w,h,new Color32(29,42,49,255)); image.name = "Button :: "+name; image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text(name+" Label",image.transform,caption,.03f,.03f,.94f,.94f,size); label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static ScrollRect Scroll(string name,Transform parent,float x,float y,float w,float h,out RectTransform content)
    {
        var root = Rect("Area :: "+name,parent,x,y,w,h); var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(0,0,0,.001f);
        var viewport = Rect("Area :: Viewport",root,0,0,1,1); viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Area :: Content",viewport,0,0,1,1); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 80; return scroll;
    }
}
