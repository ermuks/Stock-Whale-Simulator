using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pooled, fixed-column pending orders. Cancels only through the existing order session.</summary>
public sealed class PendingOrderListUI : MonoBehaviour
{
    private sealed class Row
    {
        public RectTransform Root;
        public TextMeshProUGUI Side, Name, Symbol, Price, Remaining, Filled;
        public DeviceOrderSession.PendingEntry Entry;
    }
    private DeviceOrderSession session;
    private TextMeshProUGUI template,title,count,empty;
    private TMP_FontAsset reading;
    private RectTransform content;
    private float scale=1f;
    private readonly List<DeviceOrderSession.PendingEntry> entries=new(16);
    private readonly List<Row> rows=new(8);
    private int visibleCount=-1;
    private const float RowHeight=88f;
    public int Count => entries.Count;
    public void SetScale(float value) { scale=value; visibleCount=-1; }
    public void Configure(DeviceOrderSession orders,TextMeshProUGUI fontTemplate,float fontScale=1f)
    {
        session=orders; template=fontTemplate; scale=fontScale; reading=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        title=Label("Pending Heading",transform,30,0,0,128,56,736,400); GameLocalizedText.Set(title,"미체결");
        count=Label("Pending Order Count",transform,30,136,0,72,56,736,400); TradingOrderView.Numeric(count); count.alignment=TextAlignmentOptions.MidlineRight;
        GameLocalizedText.Set(Label("Pending Company Header",transform,24,0,64,270,32,736,400),"미체결 종목");
        var price=Label("Pending Price Header",transform,24,286,64,170,32,736,400); GameLocalizedText.Set(price,"가격 · 원"); price.alignment=TextAlignmentOptions.MidlineRight;
        var volume=Label("Pending Quantity Header",transform,24,472,64,144,32,736,400); GameLocalizedText.Set(volume,"잔량 / 체결"); volume.alignment=TextAlignmentOptions.MidlineRight;
        var viewport=Rect("Area :: Pending Order Scroll",transform,0,104,736,296,736,400);
        var hit=viewport.gameObject.AddComponent<Image>(); hit.color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Area :: Pending Order Rows",viewport,0,0,1,1,1,1);
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45f;
        empty=Label("No Pending Orders",viewport,28,0,0,1,.35f,1,1); GameLocalizedText.Set(empty,"—"); empty.alignment=TextAlignmentOptions.Center;
        Refresh();
    }
    public void Refresh()
    {
        session.CopyPendingOrders(entries); DeviceTerminalUI.Set(count,entries.Count.ToString());
        empty.gameObject.SetActive(entries.Count==0);
        while(rows.Count<entries.Count) { rows.Add(CreateRow(rows.Count)); }
        if(visibleCount!=entries.Count)
        {
            visibleCount=entries.Count; content.sizeDelta=new Vector2(0,visibleCount*RowHeight*scale);
            for(int i=0;i<rows.Count;i++) { rows[i].Root.gameObject.SetActive(i<visibleCount); }
        }
        for(int i=0;i<entries.Count;i++)
        {
            var entry=entries[i]; var row=rows[i]; row.Entry=entry;
            DeviceTerminalUI.Set(row.Side,entry.Buy?"매수":"매도"); row.Side.color=entry.Buy?MarketColors.Up:MarketColors.Down;
            DeviceTerminalUI.Set(row.Name,entry.Stock.stockName);
            DeviceTerminalUI.Set(row.Symbol,entry.Request!=null ? entry.Request.AcceptedAt ?? "—" : entry.Stock.symbol);
            DeviceTerminalUI.Set(row.Price,TradingOrderView.Number(entry.Price));
            DeviceTerminalUI.Set(row.Remaining,TradingOrderView.Number(entry.Remaining));
            DeviceTerminalUI.Set(row.Filled,entry.Request==null?"—":TradingOrderView.Number(entry.Request.Filled));
        }
    }
    public void CancelAll()
    {
        session.CopyPendingOrders(entries);
        foreach(var entry in entries) { session.CancelPending(entry); }
        Refresh();
    }
    private Row CreateRow(int index)
    {
        var row=new Row {Root=Rect("Panel :: Pending Order "+index,content,0,0,1,1,1,1)};
        row.Root.anchorMin=new Vector2(0,1); row.Root.anchorMax=Vector2.one; row.Root.pivot=new Vector2(.5f,1);
        row.Root.sizeDelta=new Vector2(0,RowHeight*scale); row.Root.anchoredPosition=new Vector2(0,-index*RowHeight*scale);
        row.Side=Label("Pending Side",row.Root,24,0,8,56,32,736,88);
        row.Name=Label("Pending Stock",row.Root,26,68,8,202,32,736,88);
        row.Symbol=Label("Pending Symbol",row.Root,23,68,42,202,30,736,88); row.Symbol.color=new Color32(146,161,172,255);
        row.Price=Number("Pending Price",row.Root,286,8,170,32);
        row.Remaining=Number("Pending Remaining",row.Root,472,8,144,32);
        row.Filled=Number("Pending Filled",row.Root,472,42,144,30); row.Filled.fontSize=23*scale; row.Filled.color=row.Symbol.color;
        var divider=Rect("Image :: Pending Divider",row.Root,0,87,736,1,736,88).gameObject.AddComponent<Image>();
        divider.color=new Color32(44,60,69,255); divider.raycastTarget=false;
        var buttonRoot=Rect("Button :: Cancel Pending Order",row.Root,632,12,104,TradingOrderView.ControlHeight,736,88);
        var image=buttonRoot.gameObject.AddComponent<Image>(); var button=buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var label=Label("Cancel Pending Order",buttonRoot,26,0,0,1,1,1,1); label.alignment=TextAlignmentOptions.Center; GameLocalizedText.Set(label,"취소");
        TabletControlStyle.Button(button); label.fontStyle=FontStyles.Normal;
        button.onClick.AddListener(()=>{session.CancelPending(row.Entry); Refresh();}); return row;
    }
    private TextMeshProUGUI Number(string name,Transform parent,float x,float y,float w,float h)
    {
        var label=Label(name,parent,26,x,y,w,h,736,88); TradingOrderView.Numeric(label); label.alignment=TextAlignmentOptions.MidlineRight; return label;
    }
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h,float pw,float ph)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); TradingOrderView.Place(rect,parent,x,y,w,h,pw,ph); return rect;
    }
    private TextMeshProUGUI Label(string name,Transform parent,float size,float x,float y,float w,float h,float pw,float ph)
    {
        var label=Instantiate(template,parent); label.name="Text :: "+name;
        if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
        TradingOrderView.Place(label.rectTransform,parent,x,y,w,h,pw,ph);
        label.fontSize=size*scale; label.enableAutoSizing=false; label.fontStyle=FontStyles.Normal; label.margin=Vector4.zero;
        label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false; label.color=new Color32(230,235,239,255); GameLocalizedText.Set(label,""); return label;
    }
}
