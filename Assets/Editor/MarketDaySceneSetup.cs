using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 TradeScene의 태블릿에 한 번 연결. 재실행 시 중복 추가하지 않는다.</summary>
public static partial class MarketDaySceneSetup
{
    [MenuItem("DonDon/Connect Unified Exchange")]
    public static void ConnectExchange()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var exchange = Object.FindFirstObjectByType<ExchangeBot>(FindObjectsInactive.Include);
        if (exchange == null)
        {
            exchange = new GameObject("Unified Exchange").AddComponent<ExchangeBot>();
        }

        foreach (var legacy in Object.FindObjectsByType<ExchangeStockBot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            legacy.enabled = false;
        }

        foreach (var legacy in Object.FindObjectsByType<ExchangeStabilizerBot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            legacy.enabled = false;
        }

        var candles = Object.FindFirstObjectByType<CandleHistoryManager>();
        if (candles == null)
        {
            candles = new GameObject("Candle History").AddComponent<CandleHistoryManager>();
        }

        if (Object.FindFirstObjectByType<ExchangeWarehouse>() == null)
        {
            new GameObject("Exchange Warehouse").AddComponent<ExchangeWarehouse>();
        }

        var stocks = Object.FindFirstObjectByType<StockManager>();
        if (stocks != null)
        {
            stocks.EnsureStockDefinitions();
            EditorUtility.SetDirty(stocks);
        }

        EditorUtility.SetDirty(exchange);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Unified Exchange connected; existing strategy/session/tablet settings preserved.");
    }

    private static TMP_FontAsset font;
    [MenuItem("DonDon/Connect Market Day and Tablet Chat")]
    public static void Connect()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        if (Object.FindFirstObjectByType<MarketDayController>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("Market Day already connected");
            return;
        }

        var tablet = Object.FindFirstObjectByType<TabletViewController>();
        if (tablet == null || tablet.scrollRect == null)
        {
            throw new System.InvalidOperationException("기존 태블릿 연결 필요");
        }

        new GameObject("Market Day").AddComponent<MarketDayController>();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/FontBold.ttf");
            font = TMP_FontAsset.CreateFontAsset(source, 40, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
            font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font, "Assets/Resources/Chat/Chat Korean.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
            {
                AssetDatabase.AddObjectToAsset(texture, font);
            }

            font.TryAddCharacters("익명그룹채팅새메시지현재스트레스위험기간추격민감도회복일개장전거래중마감정리후정보최신보기0123456789:./·");
        }

        var canvas = tablet.scrollRect.GetComponentInParent<Canvas>();
        var ui = canvas.gameObject.AddComponent<TabletGroupChatUI>();
        ui.tablet = tablet;
        ui.informationPanel = tablet.scrollRect.gameObject;
        RectTransform infoRect = tablet.scrollRect.GetComponent<RectTransform>();
        infoRect.offsetMin = new Vector2(0f, 80f);
        infoRect.offsetMax = new Vector2(0f, -270f);
        var root = canvas.transform;
        ui.informationTab = ButtonAt("Information Tab", root, "정보", new Vector2(20f, -195f), new Vector2(180f, 60f));
        ui.chatTab = ButtonAt("Chat Tab", root, "익명 채팅", new Vector2(215f, -195f), new Vector2(220f, 60f));
        ui.unread = TextAt("Unread", root, "익명 그룹", new Vector2(455f, -195f), new Vector2(500f, 60f), 30f);
        ui.status = TextAt("Day Status", root, "1일 · 개장 전", new Vector2(20f, -1090f), new Vector2(1800f, 60f), 32f);
        var panel = Rect("Anonymous Group Chat", root);
        Stretch(panel, new Vector2(0f, 80f), new Vector2(0f, -270f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 1f);
        ui.chatPanel = panel.gameObject;
        var scrollRect = Rect("Messages", panel);
        Stretch(scrollRect, new Vector2(12f, 150f), new Vector2(-12f, -12f));
        ui.scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        ui.scroll.horizontal = false;
        ui.scroll.vertical = true;
        ui.scroll.scrollSensitivity = 45f;
        var viewport = Rect("Viewport", scrollRect);
        Stretch(viewport, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 1f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        ui.scroll.viewport = viewport;
        ui.scroll.content = content;
        ui.rowHeight = 110f;
        ui.rowPoolLimit = 16;
        var row = Rect("Message Row Template", content);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = Vector2.one;
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, 110f);
        var rowImage = row.gameObject.AddComponent<Image>();
        rowImage.color = new Color(0.11f, 0.14f, 0.19f, 1f);
        row.gameObject.AddComponent<Button>().targetGraphic = rowImage;
        var label = TextAt("Message", row, "", new Vector2(20f, -5f), new Vector2(1850f, 90f), 29f);
        label.textWrappingMode = TextWrappingModes.Normal;
        var gauge = Rect("Stress", row);
        gauge.anchorMin = new Vector2(0f, 0f);
        gauge.anchorMax = new Vector2(1f, 0f);
        gauge.pivot = Vector2.zero;
        gauge.sizeDelta = new Vector2(-40f, 5f);
        gauge.anchoredPosition = new Vector2(20f, 3f);
        var image = gauge.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        ui.rowTemplate = row;
        row.gameObject.SetActive(false);
        ui.profile = TextAt("Profile", panel, "메시지를 눌러 현재 프로필 보기", new Vector2(20f, -685f), new Vector2(1750f, 130f), 26f);
        var profileRect = ui.profile.rectTransform;
        profileRect.anchorMin = Vector2.zero;
        profileRect.anchorMax = new Vector2(1f, 0f);
        profileRect.pivot = Vector2.zero;
        profileRect.anchoredPosition = new Vector2(20f, 12f);
        profileRect.sizeDelta = new Vector2(-220f, 130f);
        ui.newestButton = ButtonAt("Newest", panel, "최신 보기", new Vector2(1650f, -740f), new Vector2(220f, 60f));
        var newestRect = ui.newestButton.GetComponent<RectTransform>();
        newestRect.anchorMin = new Vector2(1f, 0f);
        newestRect.anchorMax = new Vector2(1f, 0f);
        newestRect.pivot = new Vector2(1f, 0f);
        newestRect.anchoredPosition = new Vector2(-15f, 20f);
        panel.gameObject.SetActive(false);
        var hud = new GameObject("Market Clock HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        hud.GetComponent<Canvas>().sortingOrder = 20;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ui.hudStatus = TextAt("Clock", hud.transform, "1일 · 개장 전", new Vector2(20f, -20f), new Vector2(800f, 45f), 24f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Market Day connected: existing tablet Canvas, tabs, 16-row maximum pool, Korean dynamic font, HUD");
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private static TextMeshProUGUI TextAt(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.text = text;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static Button ButtonAt(string name, Transform parent, string text, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.23f, 0.32f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = TextAt("Label", rect, text, Vector2.zero, size, 28f);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
