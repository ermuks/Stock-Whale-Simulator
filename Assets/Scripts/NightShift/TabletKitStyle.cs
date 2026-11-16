using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared flat tablet presentation. Atlas cells are cached once; no domain state lives here.</summary>
public static class TabletKitStyle
{
    [Serializable] private sealed class Tokens { public Colors colors; }
    [Serializable] private sealed class Colors
    { public string background, panel, panelHover, divider, textPrimary, textSecondary, textMuted, accent, profitKR, lossKR, warning, disabled; }
    private static Tokens tokens;
    private static readonly Dictionary<string,Sprite> icons = new();
    private static readonly string[] names = { "home","assets","wallet","holdings","market","buy","sell","pending_order","company","factory","sales","research","auto_trade","robot","news","history","cash","coin_stack","growth","loss","check","close","lock","unlock","timer","calendar","refresh","settings","alert","info","chevron_right","chevron_down","plus","minus","search","price_tag","listing","dividend","reinvest","reserve","play","stop","pause","progress","complete","failed","bell","filter" };
    private static Colors Palette => (tokens ??= JsonUtility.FromJson<Tokens>(Resources.Load<TextAsset>("UI/TabletKit/DesignTokens").text)).colors;
    private static Color Parse(string value) { ColorUtility.TryParseHtmlString(value,out var color); return color; }
    public static Color Background => Parse(Palette.background);
    public static Color Panel => Parse(Palette.panel);
    public static Color Hover => Parse(Palette.panelHover);
    public static Color Line => Parse(Palette.divider);
    public static Color Ink => Parse(Palette.textPrimary);
    public static Color Muted => Parse(Palette.textSecondary);
    public static Color Accent => Parse(Palette.accent);
    public static Color Up => MarketColors.Up;
    public static Color Down => MarketColors.Down;
    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name,out var sprite))
        { return sprite; }
        int index = Array.IndexOf(names,name);
        var texture = Resources.Load<Texture2D>("UI/TabletKit/Icons");
        if (index < 0 || texture == null)
        { return null; }
        float cell = texture.width / 8f;
        sprite = Sprite.Create(texture,new Rect(index%8*cell,(5-index/8)*cell,cell,cell),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        sprite.name=name; sprite.hideFlags=HideFlags.DontSave; icons.Add(name,sprite); return sprite;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { tokens=null; icons.Clear(); }
    public static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=new Vector2(x,1-y-h); rect.anchorMax=new Vector2(x+w,1-y); rect.offsetMin=rect.offsetMax=Vector2.zero; rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity; }
    public static RectTransform Area(string name,Transform parent,float x,float y,float w,float h)
    {
        var rect=new GameObject("Area :: "+name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.gameObject.layer=parent.gameObject.layer; Place(rect,x,y,w,h); return rect;
    }
    public static Image Surface(string name,Transform parent,float x,float y,float w,float h,Color color)
    { var image=Area(name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.name="Image :: "+name; image.color=color; image.raycastTarget=false; return image; }
    public static Image Glyph(string name,Transform parent,float x,float y,float w,float h)
    { var image=Surface(name,parent,x,y,w,h,Ink); image.sprite=Icon(name); image.preserveAspect=true; return image; }
    public static void TextStyle(TMP_Text text,float size,bool numeric=false)
    {
        var font=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (font!=null) { text.font=font; text.fontSharedMaterial=font.material; }
        if (numeric) { TradingOrderView.Numeric(text); }
        text.fontSize=size; text.enableAutoSizing=false; text.fontStyle=FontStyles.Normal; text.margin=Vector4.zero;
        text.textWrappingMode=TextWrappingModes.NoWrap; text.overflowMode=TextOverflowModes.Ellipsis; text.raycastTarget=false; text.color=Ink;
    }
    public static TextMeshProUGUI Label(string name,Transform parent,string value,float x,float y,float w,float h,float size=30,bool right=false)
    {
        var text=Area(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); text.name="Text :: "+name; TextStyle(text,size);
        text.alignment=right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft; DeviceTerminalUI.Set(text,value); return text;
    }
    public static Button Action(string name,Transform parent,string value,float x,float y,float w,float h,Action action,bool primary=false)
    {
        var image=Surface(name,parent,x,y,w,h,Panel); image.name="Button :: "+name; image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var label=Label(name,image.transform,value,.03f,0,.94f,1,30); label.alignment=TextAlignmentOptions.Center;
        TabletControlStyle.Button(button,primary); button.onClick.AddListener(()=>action()); return button;
    }
    public static void Flat(Image image,Color color)
    {
        if (image==null)
        { return; }
        image.sprite=null; image.type=Image.Type.Simple; image.color=color;
    }
}
