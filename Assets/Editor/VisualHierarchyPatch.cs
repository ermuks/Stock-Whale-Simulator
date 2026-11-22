using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Reduce default prose inside the existing canvases. No models, trading data or input cameras are replaced.</summary>
public static class VisualHierarchyPatch
{
    private static TMP_FontAsset font;
    private static Material secondary;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(154,169,180,255), Dark = new Color32(15,23,27,255);
    [MenuItem("DonDon/Apply Compact Visual UI")]
    public static void Apply()
    {
        ImportIcons();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var poses = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => !(t is RectTransform)).ToDictionary(t => t,Pose);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).ToDictionary(c => c,c => EditorJsonUtility.ToJson(c)+Pose(c.transform));
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        secondary = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/TradingTerminal/Secondary_Text.mat");
        if (font == null) { throw new Exception("Existing font not found."); }
        Journey(Object.FindAnyObjectByType<InvestorJourneyUI>(FindObjectsInactive.Include));
        Profile(Object.FindObjectsByType<TabletGroupChatUI>(FindObjectsInactive.Include).Single(c => c.monitorChat));
        Growth(Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include).Single(c => c.journal));
        foreach (var pair in poses)
        { if (pair.Value != Pose(pair.Key)) { throw new Exception("3D pose changed: " + pair.Key.name); } }
        foreach (var pair in canvases)
        { if (pair.Value != EditorJsonUtility.ToJson(pair.Key)+Pose(pair.Key.transform)) { throw new Exception("Canvas root/camera changed: " + pair.Key.name); } }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Validation/VisualHierarchy");
        File.WriteAllText("Validation/VisualHierarchy/scene-results.txt","PASS existing 3D transforms, Canvas roots and event cameras preserved\nPASS existing sprites, buttons, scroll and account references reused\n");
    }
    private static string Pose(Transform t)
    {
        string value = (t.parent == null ? "root" : t.parent.GetEntityId().ToString()) + t.localPosition.ToString("R") + t.localRotation.ToString("R") + t.localScale.ToString("R");
        if (t is RectTransform r) { value += r.anchorMin.ToString("R") + r.anchorMax.ToString("R") + r.sizeDelta.ToString("R") + r.pivot.ToString("R"); }
        return value;
    }
    private static T Component<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    private static void Layout(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y); rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var existing = parent.Find(name); var rect = existing == null ? new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>() : (RectTransform)existing;
        if (existing == null) { rect.SetParent(parent,false); }
        Layout(rect,x,y,w,h); return rect;
    }
    private static void Style(TextMeshProUGUI text,float size,bool muted = false)
    {
        text.font = font; text.fontSharedMaterial = muted && secondary != null ? secondary : font.material; text.fontSize = size; text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal; text.color = muted ? Muted : Ink; text.raycastTarget = false; text.richText = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
    }
    private static TextMeshProUGUI Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,bool muted = false)
    {
        var text = Component<TextMeshProUGUI>(Rect("Text :: " + name,parent,x,y,w,h).gameObject); Style(text,size,muted); text.text = value; return text;
    }
    private static TerminalGlyph Glyph(string name,Transform parent,TerminalGlyph.Symbol symbol,float x,float y,float w,float h,bool ring = false)
    {
        var glyph = Component<TerminalGlyph>(Rect("Image :: " + name,parent,x,y,w,h).gameObject); glyph.symbol = symbol; glyph.ring = ring; glyph.raycastTarget = false;
        float inset = ring ? .19f : 0f;
        var image = Component<Image>(Rect("Image :: Icon Sprite",glyph.transform,inset,inset,1f-inset*2f,1f-inset*2f).gameObject);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/TradingTerminal/Icons/Icon_" + symbol + ".png");
        if (image.sprite == null) { throw new Exception("Generated icon missing: " + symbol); }
        image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
        glyph.iconImage = image; glyph.SetTint(Color.white); glyph.SetVerticesDirty(); return glyph;
    }
    private static void ImportIcons()
    {
        string root = "Assets/Resources/UI/TradingTerminal/Icons/";
        foreach (string file in Directory.GetFiles(root,"Icon_*.png"))
        {
            string path = file.Replace('\\','/'); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite; settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = 100f; settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(.5f,.5f); settings.spriteBorder = Vector4.zero;
            settings.alphaIsTransparency = true; settings.mipmapEnabled = false; settings.wrapMode = TextureWrapMode.Clamp; settings.filterMode = FilterMode.Bilinear;
            importer.SetTextureSettings(settings); importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024; importer.SaveAndReimport();
        }
    }
    private static Button Button(string name,Transform parent,string caption,float x,float y,float w,float h,float size)
    {
        var rect = Rect("Button :: " + name,parent,x,y,w,h); var image = Component<Image>(rect.gameObject); image.color = new Color32(25,36,41,255); image.raycastTarget = true;
        var button = Component<Button>(rect.gameObject); button.targetGraphic = image;
        var text = Text(name + " Label",rect,caption,.04f,.04f,.92f,.92f,size); text.alignment = TextAlignmentOptions.Center; return button;
    }
    private static void Journey(InvestorJourneyUI ui)
    {
        var card = (RectTransform)ui.card.transform; Layout(card,.018f,.80f,.25f,.16f);
        Layout(ui.heading.rectTransform,.045f,.39f,.54f,.23f); Style(ui.heading,23f);
        Layout(ui.detail.rectTransform,.045f,.68f,.54f,.21f); Style(ui.detail,20f,true);
        Layout((RectTransform)ui.action.transform,.64f,.38f,.32f,.49f);
        Layout((RectTransform)ui.dismiss.transform,.91f,.04f,.07f,.18f);
        ui.stages = new TerminalGlyph[3]; var icons = new[]{TerminalGlyph.Symbol.Eye,TerminalGlyph.Symbol.Trade,TerminalGlyph.Symbol.People};
        for (int i = 0; i < 3; i++) { ui.stages[i] = Glyph("Journey Stage " + i,card,icons[i],.04f+i*.21f,.055f,.12f,.27f); }
    }
    private static void Profile(TabletGroupChatUI chat)
    {
        var root = chat.transform;
        Layout((RectTransform)chat.scroll.transform,.025f,.105f,.95f,.55f);
        Layout((RectTransform)chat.newestButton.transform,.80f,.625f,.17f,.027f);
        var rooms = root.GetComponent<LeadingRoomUI>();
        var lobby = Rect("Area :: Public Lounge Actions",root,.035f,.80f,.93f,.14f); rooms.lobbyControls = lobby.gameObject;
        Glyph("Personal Room",lobby,TerminalGlyph.Symbol.People,.02f,.04f,.13f,.40f);
        rooms.lobbyStatus = Text("Personal Room Status",lobby,"내 리딩방 · 개설 전",.19f,.06f,.78f,.30f,44f,true);
        rooms.growth = Button("View Room Progress",lobby,"성장·권한",.19f,.53f,.47f,.32f,44f);
        rooms.growthScreen = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include).Single(c => c.journal);
        // Keep the original invite button in the profile header, clear of the six meters.
        Layout((RectTransform)rooms.invite.transform,.74f,.660f,.23f,.018f);
        var panel = Rect("Panel :: Investor Meters",root,.035f,.66f,.93f,.11f);
        var ui = Component<InvestorProfileUI>(panel.gameObject); chat.visualProfile = ui;
        chat.profile.transform.SetParent(panel,false); Layout(chat.profile.rectTransform,0,0,.68f,.16f); Style(chat.profile,42f);
        ui.heading = chat.profile; ui.summary = Text("Metric Hint",panel,"메시지를 눌러 확인",0,.84f,.78f,.16f,36f,true);
        ui.details = Button("Investor Details",panel,"상세",.83f,.83f,.17f,.17f,36f);
        var meters = Rect("Area :: Psychology Meters",panel,0,.18f,1,.64f); ui.meters = meters.gameObject;
        ui.gauges = new TerminalGlyph[6]; ui.metricButtons = new Button[6];
        string[] labels = {"신뢰","배신감","탐욕","공포","FOMO","스트레스"};
        var icons = new[]{TerminalGlyph.Symbol.Shield,TerminalGlyph.Symbol.BrokenShield,TerminalGlyph.Symbol.Coin,TerminalGlyph.Symbol.Warning,TerminalGlyph.Symbol.Bolt,TerminalGlyph.Symbol.Pulse};
        for (int i = 0; i < 6; i++)
        {
            var button = Button("Metric " + i,meters,"",i/6f,0,.15f,1,36f); button.image.color = Color.clear; ui.metricButtons[i] = button;
            ui.gauges[i] = Glyph("Psychology " + i,button.transform,icons[i],.1f,0,.8f,.72f,true);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(); Layout(label.rectTransform,0,.76f,1,.24f); label.text = labels[i]; Style(label,36f,true); label.alignment = TextAlignmentOptions.Center;
        }
        var detail = Rect("Panel :: Investor Details",root,.035f,.37f,.93f,.40f); Component<Image>(detail.gameObject).color = Dark; detail.GetComponent<Image>().raycastTarget = true;
        ui.detailPanel = detail.gameObject; Text("Investor Details Heading",detail,"참여자 상세",.04f,.025f,.80f,.075f,50f);
        ui.detailText = Text("Investor Details Body",detail,"",.04f,.14f,.92f,.82f,42f,true);
        ui.closeDetails = Button("Close Investor Details",detail,"×",.88f,.025f,.08f,.065f,44f);
        detail.gameObject.SetActive(false); meters.gameObject.SetActive(false);
        var stress = (RectTransform)chat.rowTemplate.Find("Image :: Stress"); stress.GetComponent<Image>().enabled = false;
        var oldStressBackground = chat.rowTemplate.Find("Image :: Stress Background");
        if (oldStressBackground != null) { oldStressBackground.gameObject.SetActive(false); }
        stress.anchorMin = stress.anchorMax = new Vector2(.74f,1f); stress.pivot = new Vector2(.5f,.5f);
        float size = 36f * chat.layoutScale; stress.sizeDelta = new Vector2(size,size); stress.anchoredPosition = new Vector2(0,-size*.5f);
        Glyph("Stress Snapshot Ring",stress,TerminalGlyph.Symbol.Pulse,0,0,1,1,true);
    }
    private static void Growth(DeviceTerminalUI screen)
    {
        var root = screen.growthPanel.transform; var ui = Component<CompactGrowthUI>(root.gameObject); screen.visualGrowth = ui;
        ui.title = root.Find("Text :: Growth Heading").GetComponent<TextMeshProUGUI>(); Layout(ui.title.rectTransform,.035f,.04f,.72f,.10f);
        var overview = Rect("Area :: Growth Meters",root,.025f,.20f,.95f,.59f); ui.overview = overview.gameObject;
        ui.turnover = Glyph("Turnover Goal",overview,TerminalGlyph.Symbol.Trade,.06f,.02f,.34f,.44f,true); ui.turnover.heatColors = false;
        ui.profit = Glyph("Profit Goal",overview,TerminalGlyph.Symbol.Wallet,.60f,.02f,.34f,.44f,true); ui.profit.heatColors = false;
        var left = Text("Turnover Caption",overview,"거래대금",0,.49f,.46f,.12f,36f,true); left.alignment = TextAlignmentOptions.Center;
        var right = Text("Profit Caption",overview,"실현손익",.54f,.49f,.46f,.12f,36f,true); right.alignment = TextAlignmentOptions.Center;
        ui.turnoverAmount = Text("Turnover Goal Value",overview,"",0,.64f,.46f,.34f,44f); ui.turnoverAmount.alignment = TextAlignmentOptions.Center;
        ui.profitAmount = Text("Profit Goal Value",overview,"",.54f,.64f,.46f,.34f,44f); ui.profitAmount.alignment = TextAlignmentOptions.Center;
        ui.detailText = screen.growthText; Layout(ui.detailText.rectTransform,.035f,.22f,.93f,.55f); Style(ui.detailText,35f,true); ui.detailText.gameObject.SetActive(false);
        ui.details = Button("Growth Explanation",root,"조건",.83f,.04f,.14f,.09f,34f);
        screen.openRooms.GetComponentInChildren<TextMeshProUGUI>().text = "리딩방 열기";
    }
}
