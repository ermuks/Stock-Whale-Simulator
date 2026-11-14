using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Bind and restyle the existing holdings tab without moving either device or its Canvas.</summary>
public static class HoldingsTabletPatch
{
    private static TMP_FontAsset font;
    private static Material secondary;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(166,177,186,255);
    [MenuItem("DonDon/Repair Holdings Detail")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        var poses = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => !(t is RectTransform)).ToDictionary(t => t,Pose);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).ToDictionary(c => c,c => EditorJsonUtility.ToJson(c)+Pose(c.transform));
        var screen = Object.FindObjectsByType<DeviceTerminalUI>(FindObjectsInactive.Include).Single(x => x.journal);
        var root = (RectTransform)screen.legacy.legacyPanel.transform;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        secondary = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/TradingTerminal/Secondary_Text.mat");
        if (font == null) { throw new Exception("Existing Korean TMP font missing."); }
        Layout(root,.015f,.095f,.97f,.89f);
        var background = Component<Image>(root.gameObject);
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/TradingTerminal/Panel_Background.png");
        background.type = Image.Type.Sliced; background.fillCenter = true; background.pixelsPerUnitMultiplier = 2f; background.color = Color.white; background.raycastTarget = false;
        var manager = Component<HoldUIManager>(root.gameObject);
        manager.account = screen.session.account; manager.whale = null; manager.viewRoot = root.gameObject; manager.currencySuffix = true; manager.refreshInterval = .3f;
        manager.activeTabImage = screen.legacy.legacyTab.image;
        manager.listItemPrefab = null; manager.listParent = null;
        Label(root,"Cash Label","보유 현금",.035f,.035f,.28f,.045f,30,true);
        Label(root,"Holdings Label","주식 평가액",.36f,.035f,.28f,.045f,30,true);
        Label(root,"Total Assets Label","총 자산",.685f,.035f,.28f,.045f,30,true);
        manager.textCash = Label(root,"Cash Value","0원",.035f,.09f,.28f,.075f,44);
        manager.textHoldingsValue = Label(root,"Holdings Value","0원",.36f,.09f,.28f,.075f,44);
        manager.textTotal = Label(root,"Total Assets Value","0원",.685f,.09f,.28f,.075f,44);
        manager.textCashAvailability = Label(root,"Cash Availability","",.035f,.18f,.93f,.04f,29,true);
        var divider = (RectTransform)root.Find("Image :: Stock Holdings Divider");
        Layout(divider,.035f,.235f,.93f,.0015f); var line = divider.GetComponent<Image>(); line.color = new Color32(52,67,75,255); line.raycastTarget = false;
        var item = Component<HoldListItem>(root.gameObject); manager.selectedPosition = item; item.currencySuffix = true;
        item.textName = Label(root,"Selected Stock Name","종목 선택",.035f,.28f,.59f,.075f,58);
        item.textSymbol = Label(root,"Selected Ticker","",.035f,.36f,.59f,.045f,30,true);
        item.textCurrentPrice = Label(root,"Selected Current Price","—",.67f,.28f,.295f,.075f,52);
        item.textCurrentPrice.alignment = TextAlignmentOptions.TopRight;
        Label(root,"Current Price Label","현재가",.67f,.36f,.295f,.045f,30,true).alignment = TextAlignmentOptions.TopRight;
        item.textEmpty = Label(root,"Empty Position","",.035f,.42f,.93f,.045f,32,true);
        Label(root,"Quantity Label","보유 수량",.035f,.50f,.28f,.045f,30,true);
        Label(root,"Average Price Label","평균 매수가",.36f,.50f,.28f,.045f,30,true);
        Label(root,"Position Value Label","평가 금액",.685f,.50f,.28f,.045f,30,true);
        item.textQuantity = Label(root,"Quantity Value","0주",.035f,.555f,.28f,.075f,46);
        item.textAvgPrice = Label(root,"Average Price Value","—",.36f,.555f,.28f,.075f,46);
        item.textCurrentValue = Label(root,"Position Value","0원",.685f,.555f,.28f,.075f,44);
        item.textAvailability = Label(root,"Sell Availability","",.035f,.655f,.93f,.045f,30,true);
        Label(root,"Profit Loss Label","평가손익",.035f,.77f,.60f,.045f,30,true);
        Label(root,"Return Label","수익률",.72f,.77f,.245f,.045f,30,true).alignment = TextAlignmentOptions.TopRight;
        item.textEvaluationProfit = Label(root,"Profit Loss Value","0원",.035f,.83f,.60f,.10f,62);
        item.textProfitPct = Label(root,"Return Value","—",.72f,.83f,.245f,.10f,52);
        item.textProfitPct.alignment = TextAlignmentOptions.TopRight;
        root.gameObject.SetActive(false);
        foreach (var other in Object.FindObjectsByType<HoldUIManager>(FindObjectsInactive.Include))
        {
            if (other == manager || other.textCash == null)
            { continue; }
            // Existing first-tablet list keeps its pool; skip refreshes while that tab is hidden.
            other.viewRoot = other.textCash.transform.parent.gameObject; other.refreshInterval = Mathf.Max(.3f,other.refreshInterval);
        }
        foreach (var pair in poses)
        { if (pair.Value != Pose(pair.Key)) { throw new Exception("3D pose changed: " + pair.Key.name); } }
        foreach (var pair in canvases)
        { if (pair.Value != EditorJsonUtility.ToJson(pair.Key)+Pose(pair.Key.transform)) { throw new Exception("Canvas changed: " + pair.Key.name); } }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Validation/HoldingsTablet");
        File.WriteAllText("Validation/HoldingsTablet/patch-results.txt","PASS existing holdings tab bound to player account and current selection\nPASS 3D poses and Canvas roots/cameras preserved\nPASS legacy first-tablet holdings pool retained\n");
    }
    private static string Pose(Transform t)
    {
        string value = (t.parent == null ? "root" : t.parent.GetEntityId().ToString())+t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R");
        if (t is RectTransform r) { value += r.anchorMin.ToString("R")+r.anchorMax.ToString("R")+r.sizeDelta.ToString("R")+r.pivot.ToString("R"); }
        return value;
    }
    private static T Component<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    private static void Layout(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y); rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        var position = rect.anchoredPosition3D; position.z = 0f; rect.anchoredPosition3D = position;
    }
    private static TextMeshProUGUI Label(Transform parent,string name,string value,float x,float y,float w,float h,float size,bool muted = false)
    {
        var child = parent.Find("Text :: "+name);
        var rect = child == null ? new GameObject("Text :: "+name,typeof(RectTransform)).GetComponent<RectTransform>() : (RectTransform)child;
        if (child == null) { rect.SetParent(parent,false); }
        Layout(rect,x,y,w,h); var label = Component<TextMeshProUGUI>(rect.gameObject);
        label.font = font; label.fontSharedMaterial = muted && secondary != null ? secondary : font.material;
        label.fontSize = size; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal; label.color = muted ? Muted : Ink;
        label.margin = Vector4.zero; label.lineSpacing = label.characterSpacing = label.wordSpacing = label.paragraphSpacing = 0f;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.TopLeft; label.richText = false; label.raycastTarget = false; label.text = value;
        return label;
    }
}
