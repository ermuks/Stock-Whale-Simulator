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
    public static void Emphasize(Button button, bool active)
    {
        if (button == null) { return; }
        TabletKitStyle.Flat(button.image,active ? TabletKitStyle.Accent : TabletKitStyle.Hover);
        var text=button.GetComponentInChildren<TMP_Text>(true);
        if (text!=null) { text.color=!button.interactable ? TabletKitStyle.Muted : active ? TabletKitStyle.Background : TabletKitStyle.Ink; }
    }
    public static void Button(Button button, bool primaryAction = false)
    {
        if (button == null) { return; }
        Emphasize(button,primaryAction);
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f,1.18f,1.18f,1);
        colors.pressedColor = new Color(.70f,.75f,.78f,1); colors.selectedColor = new Color(1.08f,1.08f,1.08f,1);
        colors.disabledColor = new Color(.78f,.80f,.82f,.80f); colors.fadeDuration = .10f; colors.colorMultiplier = 1;
        button.colors = colors;
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        { Font(label); label.fontStyle = FontStyles.Normal; label.raycastTarget = false; }
    }
    public static void Input(TMP_InputField field)
    {
        if (field == null) { return; }
        TabletKitStyle.Flat(field.targetGraphic as Image,TabletKitStyle.Hover); field.transition = Selectable.Transition.ColorTint;
        var colors = field.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.9f,1.08f,1.12f,1);
        colors.selectedColor = new Color(.65f,1.20f,1.28f,1); colors.pressedColor = colors.selectedColor;
        colors.disabledColor = new Color(.65f,.65f,.65f,.45f); colors.fadeDuration = .12f; colors.colorMultiplier = 1; field.colors = colors;
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
