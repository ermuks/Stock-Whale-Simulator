using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>기존 장치/월드 Canvas에 심야 작업 탭과 공간 재질을 연결. 같은 씬에 중복 설치하지 않는다.</summary>
public static class NightShiftScenePatch
{
    private static TMP_FontAsset font;
    private static readonly Color Ink = new Color32(225, 231, 232, 255), Muted = new Color32(143, 160, 164, 255);
    [MenuItem("DonDon/Apply Night Shift Patch")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        if (Object.FindAnyObjectByType<TradingOperationController>(FindObjectsInactive.Include) != null)
        {
            AddOperationMonitor();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Night Shift connections refreshed.");
            return;
        }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        var systems = new GameObject("Area :: Night Shift Systems");
        var operation = systems.AddComponent<TradingOperationController>();
        operation.account = Object.FindAnyObjectByType<StockManager>().playerAccount;
        systems.AddComponent<MarketRhythmDirector>();
        var ambience = systems.AddComponent<WorkroomAmbience>();
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var left = canvases.Single(c => c.name == "Panel :: Tablet Portfolio Screen");
        var right = canvases.Single(c => c.name == "Panel :: Stock Holdings Screen");
        BuildTablet(left, false);
        BuildTablet(right, true);
        var existing = Object.FindAnyObjectByType<TabletViewController>();
        var second = new GameObject("Area :: Journal Tablet Interaction").AddComponent<TabletViewController>();
        second.tabletObject = GameObject.Find("Order Tablet (1)").transform;
        second.viewCamera = existing.viewCamera != null ? existing.viewCamera : Camera.main;
        second.tabletLayerMask = existing.tabletLayerMask;
        second.holdLocalPosition = existing.holdLocalPosition;
        second.holdLocalEuler = existing.holdLocalEuler;
        second.moveSpeed = existing.moveSpeed;
        existing.focusKey = KeyCode.Alpha1;
        second.focusKey = KeyCode.Alpha2;
        foreach (var collider in second.tabletObject.GetComponentsInChildren<Collider>(true))
        { collider.gameObject.layer = LayerMask.NameToLayer("Tablet"); }
        foreach (var controller in Object.FindObjectsByType<MouseCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { controller.pauseWhenCursorUnlocked = false; }
        LightRoom(ambience);
        var day = Object.FindAnyObjectByType<MarketDayController>();
        if (day != null)
        {
            day.preOpenSeconds = 0f;
            day.postCloseSeconds = 20f;
        }
        var hud = canvases.Single(c => c.name == "Panel :: Interaction HUD");
        var hint = Label("Desk Controls", hud.transform, "", .04f, .94f, .92f, .045f, 23f);
        hint.gameObject.SetActive(false);
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = Muted;
        AddOperationMonitor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Night Shift applied to existing desk, three monitors and two tablets.");
    }
    public static void ApplyBatch()
    {
        Apply();
        EditorApplication.Exit(0);
    }
    private static void AddOperationMonitor()
    {
        Object.FindAnyObjectByType<MarketRhythmDirector>().participation = .72f;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        var screen = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(c => c.name == "Panel :: Stock List Screen");
        var ui = Object.FindObjectsByType<DeskOperationUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(c => !c.journal);
        if (screen.transform.Find("Panel :: Operation Monitor") != null)
        { return; }
        var scroll = screen.GetComponentInChildren<ScrollRect>(true);
        var scrollRect = (RectTransform)scroll.transform;
        scrollRect.offsetMin = new Vector2(scrollRect.offsetMin.x, 1000f);
        var panel = Rect("Panel :: Operation Monitor", screen.transform, .04f, .71f, .92f, .27f);
        panel.localPosition += new Vector3(0f, 0f, -6f);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color32(17, 27, 29, 255);
        background.raycastTarget = false;
        Label("Operation Monitor Heading", panel, "작업 현황", .04f, .035f, .92f, .09f, 54f);
        ui.monitorStatus = Label("Operation Monitor Status", panel, "", .04f, .17f, .92f, .79f, 42f);
        ui.monitorStatus.color = Muted;
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x, 1f - y - height);
        rect.anchorMax = new Vector2(x + width, 1f - y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, string text, float x, float y, float width, float height, float size)
    {
        var label = Rect("Text :: " + name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.color = Ink;
        label.text = text;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.richText = false;
        return label;
    }
    private static Button Button(string name, Transform parent, string caption, float x, float y, float width, Color color)
    {
        var rect = Rect("Button :: " + name, parent, x, y, width, .09f);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var palette = button.colors;
        palette.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        palette.disabledColor = new Color(.4f, .4f, .4f, .7f);
        button.colors = palette;
        var text = Label(name + " Label", rect, caption, .03f, .05f, .94f, .9f, 34f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static Image Meter(string name, Transform parent, float y, Color color)
    {
        var track = Rect("Image :: " + name + " Track", parent, .04f, y, .92f, .012f).gameObject.AddComponent<Image>();
        track.color = new Color(.12f, .16f, .17f, 1f);
        track.raycastTarget = false;
        var fill = Rect("Image :: " + name + " Fill", track.transform, 0, 0, 1, 1).gameObject.AddComponent<Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.color = color;
        fill.raycastTarget = false;
        return fill;
    }
    private static void BuildTablet(Canvas canvas, bool journal)
    {
        var originals = canvas.transform.Cast<Transform>().ToArray();
        var legacy = Rect("Panel :: Existing " + (journal ? "Holdings" : "Portfolio And News"), canvas.transform, 0, .1f, 1, .9f);
        // Keep legacy absolute coordinates and event references; hide its tab only when the new tab is selected.
        legacy.anchorMin = Vector2.zero;
        legacy.anchorMax = Vector2.one;
        legacy.offsetMin = legacy.offsetMax = Vector2.zero;
        foreach (var child in originals)
        { child.SetParent(legacy, false); }
        legacy.localScale = Vector3.one * .88f;
        legacy.anchoredPosition = new Vector2(0f, -((RectTransform)canvas.transform).rect.height * .055f);
        var ui = canvas.gameObject.AddComponent<DeskOperationUI>();
        ui.journal = journal;
        ui.legacyPanel = legacy.gameObject;
        var panel = Rect("Panel :: " + (journal ? "Operation Journal" : "Operation Controls"), canvas.transform, 0, .11f, 1, .89f);
        panel.localPosition += new Vector3(0f, 0f, -1f);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color32(16, 24, 27, 255);
        bg.raycastTarget = false;
        ui.deskPanel = panel.gameObject;
        Color teal = new Color32(34, 77, 80, 255), dark = new Color32(38, 47, 51, 255);
        ui.deskTab = Button("Operation Tab", canvas.transform, journal ? "작업 기록" : "작업 실행", .02f, .01f, .46f, teal);
        ui.legacyTab = Button("Legacy Tab", canvas.transform, journal ? "보유 상세" : "자산 · 뉴스 · 대화", .52f, .01f, .46f, dark);
        ui.heading = Label("Operation Heading", panel, "", .04f, .025f, .92f, .07f, 44f);
        ui.accountText = Label("Operation Cash", panel, "", .04f, .105f, .92f, .06f, 30f);
        ui.accountText.color = Muted;
        ui.stateText = Label("Operation Mode", panel, "", .04f, .175f, .92f, .065f, 38f);
        ui.details = Label("Operation Details", panel, "", .04f, .265f, journal ? .47f : .58f, journal ? .43f : .24f, 32f);
        ui.history = Label("Operation History", panel, "", journal ? .54f : .65f, .265f, journal ? .42f : .31f, journal ? .43f : .26f, 29f);
        ui.history.color = Muted;
        ui.feedback = Label("Operation Feedback", panel, "", .04f, .735f, .92f, .105f, 30f);
        Label("Operation Heat Label", panel, "투입 과열도   /   높으면 강도 조절", .04f, .85f, .92f, .04f, 24f).color = Muted;
        ui.heatFill = Meter("Operation Heat", panel, .90f, new Color32(190, 156, 91, 255));
        ui.progressFill = Meter("Operation Time", panel, .94f, new Color32(67, 143, 150, 255));
        if (journal)
        { return; }
        ui.budget = Button("Cycle Budget", panel, "예산 5%", .04f, .52f, .19f, dark);
        ui.budgetText = ui.budget.GetComponentInChildren<TextMeshProUGUI>();
        ui.prepare = Button("Prepare Operation", panel, "종목 준비", .27f, .52f, .19f, teal);
        ui.pause = Button("Pause Operation", panel, "일시정지", .50f, .52f, .19f, dark);
        ui.finish = Button("Finish Operation", panel, "작업 종료", .73f, .52f, .23f, dark);
        ui.build = Button("Build Position", panel, "분할 매집", .04f, .63f, .28f, teal);
        ui.drive = Button("Drive Position", panel, "매수 강도 ↑", .36f, .63f, .28f, new Color32(92, 53, 53, 255));
        ui.unwind = Button("Unwind Position", panel, "분할 회수", .68f, .63f, .28f, new Color32(45, 65, 86, 255));
    }
    private static Material Material(string name, Color color, float smoothness)
    {
        const string folder = "Assets/Resources/UI/TradingTerminal/";
        string path = folder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Concrete_Wall_Albedo.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Concrete_Wall_Normal.png"));
        material.SetFloat("_BumpScale", .75f);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        return material;
    }
    private static void LightRoom(WorkroomAmbience ambience)
    {
        var wall = Material("NightShift_DampConcrete", new Color(.32f, .38f, .34f), .25f);
        var floor = Material("NightShift_WornFloor", new Color(.16f, .20f, .19f), .38f);
        var desk = Material("NightShift_Desk", new Color(.14f, .16f, .15f), .18f);
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var size = renderer.bounds.size;
            if (size.magnitude > 15f && renderer.name.StartsWith("Plane"))
            {
                bool horizontal = size.y < .1f;
                renderer.sharedMaterial = horizontal ? floor : wall;
                var position = renderer.transform.position;
                if (position.z > 9f) { position.z = 2.4f; renderer.name = "Area :: Damp Back Wall"; }
                if (position.x < -9f) { position.x = -4.7f; renderer.name = "Area :: Damp Left Wall"; }
                if (position.x > 9f) { position.x = 4.7f; renderer.name = "Area :: Damp Right Wall"; }
                if (position.y > 9f) { position.y = 8.2f; renderer.name = "Area :: Low Ceiling"; }
                renderer.transform.position = position;
            }
            else if (renderer.name == "desk_body")
            { renderer.sharedMaterial = desk; }
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.055f, .068f, .061f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.025f, .038f, .034f);
        RenderSettings.fogDensity = .026f;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            light.shadows = LightShadows.None;
            if (light.name.Contains("Monitor"))
            { light.color = new Color(.38f, .56f, .53f); light.intensity = 1.5f; light.range = 5f; }
            else
            { light.color = new Color(.67f, .73f, .51f); light.intensity = 1.8f; light.range = 12f; ambience.practical = light; }
        }
        var warm = new GameObject("Image :: Desk Practical Light").AddComponent<Light>();
        warm.type = LightType.Point;
        warm.transform.position = new Vector3(-3.4f, 5f, -2.5f);
        warm.color = new Color(.74f, .48f, .23f);
        warm.intensity = 2.3f;
        warm.range = 6f;
        warm.shadows = LightShadows.Soft;
        var pipeMaterial = Material("NightShift_OxidizedPipe", new Color(.16f, .18f, .14f), .2f);
        Pipe("Rear Conduit", new Vector3(0f, 7.8f, 2.15f), new Vector3(.10f, 4.6f, .10f), Quaternion.Euler(0f, 0f, 90f), pipeMaterial);
        Pipe("Left Drain", new Vector3(-4.45f, 5.8f, 2.15f), new Vector3(.13f, 2f, .13f), Quaternion.identity, pipeMaterial);
        Camera.main.backgroundColor = new Color(.015f, .022f, .02f);
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        var urp = Camera.main.GetUniversalAdditionalCameraData();
        urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
    }
    private static void Pipe(string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = "Area :: " + name;
        pipe.transform.SetPositionAndRotation(position, rotation);
        pipe.transform.localScale = scale;
        pipe.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(pipe.GetComponent<Collider>());
    }
}
