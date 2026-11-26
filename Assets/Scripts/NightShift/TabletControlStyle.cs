using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Flat controls shared by the two tablets; decorative icons come from the supplied kit.</summary>
public static class TabletControlStyle
{
    private static Sprite primary, secondary, input;
    private static TMP_FontAsset readingFont;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { primary = secondary = input = null; readingFont = null; }
    public static Sprite Primary => primary ??= Load("ActionPrimary-v1",new Rect(0,104,2172,524));
    public static Sprite Secondary => secondary ??= Load("ActionSecondary-v1",new Rect(0,104,2172,524));
    public static Sprite InputSurface => input ??= Load("InputField-v1",new Rect(0,84,2172,556));
    private static Sprite Load(string name, Rect sourceRect)
    {
        var source = Resources.Load<Sprite>("UI/TabletControls/" + name);
        if (source == null) { return null; }
        // Keep the generated PNG intact. A cached sprite rectangle removes transparent export padding.
        float sx = source.texture.width / 2172f, sy = source.texture.height / 724f;
        var rect = new Rect(sourceRect.x*sx,sourceRect.y*sy,sourceRect.width*sx,sourceRect.height*sy);
        var sprite = Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(80*sx,80*sy,80*sx,80*sy));
        sprite.name = name + " Sliced"; sprite.hideFlags = HideFlags.DontSave; return sprite;
    }
    private static void Surface(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) { return; }
        if (image.sprite != sprite) { image.sprite = sprite; }
        image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 4; image.color = Color.white;
    }
    // ── 조작 요소의 모양 규칙 ──
    // 모양만 보고 무엇을 하는 칸인지 알 수 있게 다섯 가지로만 쓴다.
    //   주 동작(Primary)  : 강조색으로 꽉 채우고 굵은 글자. 화면마다 하나.
    //   보조 단추(Button) : 바탕보다 한 단 밝은 채움, 가운데 글자. 누르는 것.
    //   입력 칸(Input)    : 바탕보다 어두운 우물 + 가는 테두리. 눌러서 쓰면 테두리가 강조색이 된다. 쓰는 곳.
    //   목록 줄(Row)      : 채움이 없다. 커서를 올리면 밝아지고, 고른 줄은 왼쪽에 강조색 막대. 고르는 것.
    //   위험 동작(Danger) : 보조 단추 모양에 붉은 글자(나가기·취소·멈춤).
    /// <summary>보조 단추의 채움. 패널(#121E26)과 입력 칸(어두운 우물)보다 밝다.</summary>
    public static readonly Color Raised = new Color32(36,54,64,255);
    /// <summary>입력 칸의 우물. 패널보다 어둡다.</summary>
    public static readonly Color Well = new Color32(7,12,16,255);
    public static readonly Color WellEdge = new Color32(62,82,94,255);
    public static readonly Color DangerInk = new Color32(255,128,128,255);
    /// <summary>주 동작의 채움. 강조색(#2EE8CF)을 그대로 칠하면 어두운 방의 블룸에 번져 그 위의 어두운 글자가 회색으로 뭉개진다. 한 단 낮춘 청록에 흰 굵은 글자를 쓴다.</summary>
    public static readonly Color ActionFill = new Color32(22,150,136,255);
    private static Sprite frame;
    private static Sprite FrameSprite
    {
        get
        {
            if (frame != null) { return frame; }
            var texture = new Texture2D(8,8,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[64];
            for (int y = 0; y < 8; y++) { for (int x = 0; x < 8; x++) { bool edge = x < 2 || x > 5 || y < 2 || y > 5; pixels[y*8+x] = edge ? new Color32(255,255,255,255) : new Color32(255,255,255,0); } }
            texture.SetPixels32(pixels); texture.Apply(false,true);
            frame = Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(2,2,2,2));
            frame.name = "Control Frame"; frame.hideFlags = HideFlags.DontSave; return frame;
        }
    }
    /// <summary>칸 둘레의 가는 테두리(없으면 만든다). 글자보다 뒤, 채움보다 앞에 그린다.</summary>
    private static Image Edge(Component owner, float thickness = 1.5f)
    {
        var found = owner.transform.Find("Image :: Edge");
        Image image;
        if (found != null) { image = found.GetComponent<Image>(); }
        else
        {
            var rect = new GameObject("Image :: Edge",typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(owner.transform,false); rect.gameObject.layer = owner.gameObject.layer; rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
        }
        image.sprite = FrameSprite; image.type = Image.Type.Sliced; image.fillCenter = false;
        image.pixelsPerUnitMultiplier = 2f / Mathf.Max(.25f,thickness);
        return image;
    }
    /// <summary>입력 칸 둘레에 테두리를 두르고, 쓰는 중·커서 위·평소에 맞춰 테두리 색을 바꾸게 한다. 채움은 건드리지 않는다.</summary>
    public static void Frame(TMP_InputField field)
    {
        if (field == null) { return; }
        var edge = Edge(field); edge.color = WellEdge; edge.gameObject.SetActive(true);
        if (!field.TryGetComponent<InputFocusEdge>(out var focus)) { focus = field.gameObject.AddComponent<InputFocusEdge>(); }
        focus.Bind(field,edge);
    }
    private static void HideEdge(Component owner)
    {
        var found = owner == null ? null : owner.transform.Find("Image :: Edge");
        if (found != null) { found.gameObject.SetActive(false); }
    }
    private static void Tint(Selectable control, float normalAlpha)
    {
        control.transition = Selectable.Transition.ColorTint;
        var colors = control.colors; colors.normalColor = new Color(1,1,1,normalAlpha); colors.highlightedColor = new Color(1.18f,1.18f,1.18f,1);
        colors.pressedColor = new Color(.72f,.78f,.82f,1); colors.selectedColor = new Color(1,1,1,normalAlpha);
        colors.disabledColor = new Color(.78f,.80f,.82f,.55f*Mathf.Max(normalAlpha,.001f)); colors.fadeDuration = .08f; colors.colorMultiplier = 1;
        control.colors = colors;
    }
    private static TMP_Text Caption(Button button) => button == null ? null : button.GetComponentInChildren<TMP_Text>(true);

    public static void Emphasize(Button button, bool active)
    {
        if (button == null) { return; }
        HideEdge(button);
        // 누를 수 없는 주 동작은 강조색을 빼서 한눈에 꺼져 있음을 알린다.
        bool live = button.interactable;
        TabletKitStyle.Flat(button.image,active ? (live ? ActionFill : TabletKitStyle.Panel) : Raised);
        var text=Caption(button);
        if (text!=null)
        {
            text.color=!live ? TabletKitStyle.Muted : active ? OnFill(ActionFill) : TabletKitStyle.Ink;
            text.fontStyle = active && live ? FontStyles.Bold : FontStyles.Normal;
        }
    }
    /// <summary>주 동작 단추를 그 색으로 칠한다(매수 빨강·매도 파랑 등). 글자는 굵고 어둡게: 밝은 채움 위에서 가장 또렷하다.</summary>
    /// <summary>채움 위에 올릴 글자색: 밝은 채움(청록)에는 거의 검정, 어두운 채움(빨강·파랑)에는 흰색. 어두운 방의 조명 아래에서도 또렷한 쪽을 고른다.</summary>
    public static Color OnFill(Color fill) => fill.r * .2126f + fill.g * .7152f + fill.b * .0722f > .55f ? new Color32(8,10,12,255) : new Color32(255,255,255,255);
    public static void MainAction(Button button, Color fill)
    {
        if (button == null) { return; }
        HideEdge(button);
        bool live = button.interactable;
        TabletKitStyle.Flat(button.image,live ? fill : TabletKitStyle.Panel);
        var text = Caption(button);
        if (text != null) { text.color = live ? OnFill(fill) : TabletKitStyle.Muted; text.fontStyle = live ? FontStyles.Bold : FontStyles.Normal; }
    }
    /// <summary>위험 동작: 보조 단추 모양에 붉은 글자. 나가기·전체 취소·멈춤처럼 되돌리기 어려운 것.</summary>
    public static void Danger(Button button)
    {
        if (button == null) { return; }
        // 자주(갱신마다) 불러도 가볍게: 모양은 보조 단추(Emphasize)로 두고 글자색만 붉게 한다.
        Emphasize(button,false);
        var text = Caption(button);
        if (text != null) { text.color = button.interactable ? DangerInk : TabletKitStyle.Muted; }
    }
    /// <summary>
    /// 목록 줄: 평소에는 채움이 없고 커서를 올리면 밝아진다. 고른 줄은 은은한 채움에 왼쪽 강조색 막대와 강조색 글자.
    /// 고르는 것이지 누르는 단추가 아니므로 단추 모양을 쓰지 않는다.
    /// </summary>
    public static void Row(Button button, bool chosen)
    {
        if (button == null) { return; }
        HideEdge(button);
        TabletKitStyle.Flat(button.image,TabletKitStyle.Hover);
        Tint(button,chosen ? 1f : 0f);
        var marker = button.transform.Find("Image :: Row Marker");
        if (marker == null && chosen)
        {
            var image = TabletKitStyle.Surface("Row Marker",button.transform,0,.12f,.012f,.76f,TabletKitStyle.Accent);
            marker = image.transform; marker.SetAsFirstSibling();
        }
        if (marker != null) { marker.gameObject.SetActive(chosen); }
        var text = Caption(button);
        if (text != null) { text.color = !button.interactable ? TabletKitStyle.Muted : chosen ? TabletKitStyle.Accent : TabletKitStyle.Ink; text.fontStyle = FontStyles.Normal; }
    }
    public static void Button(Button button, bool primaryAction = false)
    {
        if (button == null) { return; }
        Tint(button,1f);
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        { Font(label); label.fontStyle = FontStyles.Normal; label.raycastTarget = false; }
        Emphasize(button,primaryAction);
    }
    public static void Input(TMP_InputField field)
    {
        if (field == null) { return; }
        // 입력 칸은 바탕보다 어두운 우물에 가는 테두리다(단추는 밝게 솟은 채움). 쓰는 중에는 테두리가 강조색이 된다.
        TabletKitStyle.Flat(field.targetGraphic as Image,Well); field.transition = Selectable.Transition.ColorTint;
        var colors = field.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.6f,1.6f,1.6f,1);
        colors.selectedColor = new Color(1.3f,1.3f,1.3f,1); colors.pressedColor = colors.selectedColor;
        colors.disabledColor = new Color(.65f,.65f,.65f,.45f); colors.fadeDuration = .12f; colors.colorMultiplier = 1; field.colors = colors;
        Frame(field);
        field.customCaretColor = true; field.caretColor = new Color32(111,219,232,255); field.caretWidth = 2;
        Font(field.textComponent); if (readingFont != null) { field.fontAsset = readingFont; }
        field.textComponent.fontStyle = FontStyles.Normal;
        if (field.placeholder is TMP_Text hint) { Font(hint); hint.color = new Color32(138,158,171,255); }
    }
    private static void Font(TMP_Text text)
    {
        if (text == null) { return; }
        readingFont ??= Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (readingFont == null) { return; }
        text.font = readingFont; text.fontSharedMaterial = readingFont.material;
    }
    public static void Apply(TabletAccountUI tablet)
    {
        var tabs = new HashSet<Button>(tablet.tabs);
        foreach (var terminal in new[] { tablet.orders,tablet.events })
        {
            tabs.Add(terminal.mainTab); tabs.Add(terminal.legacy.deskTab); tabs.Add(terminal.legacy.legacyTab);
            if (terminal.eventButtons != null) { foreach (var row in terminal.eventButtons) { tabs.Add(row); } }
        }
        foreach (var label in tablet.contentRoot.GetComponentsInChildren<TMP_Text>(true))
        { if (label.GetComponentInParent<TradingOrderView>(true) == null) { Font(label); } }
        foreach (var button in tablet.contentRoot.GetComponentsInChildren<Button>(true))
        {
            if (tabs.Contains(button) || button.GetComponent<InvestmentPositionRow>() != null || button.GetComponentInParent<NewsDeskUI>(true) != null || button.GetComponentInParent<TradingOrderView>(true) != null) { continue; }
            Button(button);
        }
        foreach (var field in tablet.contentRoot.GetComponentsInChildren<TMP_InputField>(true))
        { if (field.GetComponentInParent<TradingOrderView>(true) == null) { Input(field); } }
        foreach (var button in new[] { tablet.orderButton,tablet.transferSubmit,tablet.orders.review,tablet.orders.submit,tablet.events.openRooms })
        { if (button.GetComponentInParent<TradingOrderView>(true) == null) { Button(button,true); } }
    }
}

/// <summary>입력 칸의 테두리를 쓰는 중에는 강조색, 커서를 올리면 밝게, 평소에는 차분하게 칠한다.</summary>
public sealed class InputFocusEdge : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private TMP_InputField field; private Image edge; private bool over; private int state = -1;
    public void Bind(TMP_InputField input, Image frame) { field = input; edge = frame; state = -1; }
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data) { over = true; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) { over = false; }
    void OnDisable() { over = false; state = -1; }
    void LateUpdate()
    {
        if (field == null || edge == null) { return; }
        int now = field.isFocused ? 2 : over && field.interactable ? 1 : 0;
        if (now == state) { return; }
        state = now;
        edge.color = now == 2 ? TabletKitStyle.Accent : now == 1 ? new Color32(120,146,160,255) : TabletControlStyle.WellEdge;
    }
}
