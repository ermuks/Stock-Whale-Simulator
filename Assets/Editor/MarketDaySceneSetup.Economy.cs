using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class MarketDaySceneSetup
{
    [MenuItem("DonDon/Connect Initial Economy and Owner Bots")]
    public static void ConnectEconomy()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var manager = Object.FindFirstObjectByType<StockManager>();
        if (manager == null)
        {
            throw new System.InvalidOperationException("StockManager is required.");
        }

        if (!manager.useInitialEconomy)
        {
            manager.economy = new EconomySettings();
            manager.useInitialEconomy = true;
        }

        if (manager.playerAccount == null)
        {
            foreach (var trader in Object.FindObjectsByType<Trader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (trader.GetType() == typeof(Trader))
                {
                    manager.playerAccount = trader;
                    break;
                }
            }

            if (manager.playerAccount == null)
            {
                manager.playerAccount = new GameObject("Player Account").AddComponent<Trader>();
            }
        }

        var ui = Object.FindFirstObjectByType<PlayerCompanySetupUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            CreatePlayerSetup();
        }

        ui = Object.FindFirstObjectByType<PlayerCompanySetupUI>(FindObjectsInactive.Include);
        ui.mouseController = Object.FindFirstObjectByType<MouseCameraController>();
        EditorUtility.SetDirty(ui);
        foreach (var holdings in Object.FindObjectsByType<HoldUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (holdings.account == null)
            {
                holdings.account = holdings.whale != null ? holdings.whale : manager.playerAccount;
                EditorUtility.SetDirty(holdings);
            }
        }

        var candles = Object.FindFirstObjectByType<CandleHistoryManager>();
        if (candles != null)
        {
            candles.candleDuration = 5f;
            EditorUtility.SetDirty(candles);
        }

        EditorUtility.SetDirty(manager);
        ConnectRiskFontFallbacks();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Initial economy connected: one player account, mandatory setup, 8 listed / 3 unlisted definitions, existing unified exchange.");
    }

    private static void ConnectRiskFontFallbacks()
    {
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        if (fallback == null)
        { return; }

        fallback.TryAddCharacters("안전경고위험거래중지판정대기플레이어기업초발행량공개비율현금조달본인확설시작단가총자산최종보유수량");
        EditorUtility.SetDirty(fallback);
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            AddRiskFallback(text.font, fallback);
        }

        var stockListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/StockListItem.prefab");
        if (stockListPrefab != null)
        {
            foreach (var text in stockListPrefab.GetComponentsInChildren<TMP_Text>(true))
            {
                AddRiskFallback(text.font, fallback);
            }
        }
    }

    private static void AddRiskFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
    {
        if (asset == null || asset == fallback || !AssetDatabase.GetAssetPath(asset).StartsWith("Assets/") || asset.fallbackFontAssetTable.Contains(fallback))
        { return; }

        asset.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(asset);
    }

    private static void CreatePlayerSetup()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        var go = new GameObject("Player Company Setup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var backdrop = Rect("Backdrop", go.transform);
        Stretch(backdrop, Vector2.zero, Vector2.zero);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.96f);
        var panel = Rect("Setup Panel", go.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(1400f, 950f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 1f);
        var ui = go.AddComponent<PlayerCompanySetupUI>();
        TextAt("Title", panel, "내 종목 만들기", new Vector2(70f, -40f), new Vector2(1250f, 70f), 38f);
        TextAt("Explanation", panel, "확정 전에는 거래와 회사 뉴스가 시작되지 않습니다. 공개분은 거래소가 인수합니다.", new Vector2(70f, -120f), new Vector2(1260f, 60f), 24f);
        ui.companyName = SetupInput(panel, "종목 이름", 210f, false);
        ui.issuedShares = SetupInput(panel, "총 발행량", 305f, true);
        ui.publicPercentage = SetupInput(panel, "시장 공개 비율 (%)", 400f, true);
        ui.preview = TextAt("Preview", panel, "", new Vector2(70f, -510f), new Vector2(1260f, 230f), 25f);
        ui.preview.richText = false;
        ui.error = TextAt("Validation", panel, "", new Vector2(70f, -760f), new Vector2(1260f, 60f), 23f);
        ui.error.color = new Color(1f, 0.65f, 0.45f);
        ui.confirm = ButtonAt("Confirm", panel, "확정하고 시작", new Vector2(900f, -850f), new Vector2(430f, 65f));
    }

    private static TMP_InputField SetupInput(Transform panel, string title, float y, bool integer)
    {
        TextAt(title + " Label", panel, title, new Vector2(70f, -y), new Vector2(350f, 65f), 25f);
        var root = Rect(title, panel);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(440f, -y);
        root.sizeDelta = new Vector2(890f, 65f);
        var image = root.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.3f, 1f);
        var viewport = Rect("Viewport", root);
        Stretch(viewport, new Vector2(15f, 8f), new Vector2(-15f, -8f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = TextAt("Text", viewport, "", Vector2.zero, new Vector2(860f, 50f), 27f);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.targetGraphic = image;
        input.richText = false;
        input.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }
}
