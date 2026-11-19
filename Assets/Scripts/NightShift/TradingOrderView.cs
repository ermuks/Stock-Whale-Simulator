using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fixed trading grid inside the existing tablet. Reads state; never submits or cancels orders.</summary>
public sealed class TradingOrderView : MonoBehaviour
{
    private DeviceTerminalUI ui;
    private TMP_FontAsset reading;
    private float scale;
    private TextMeshProUGUI available, reserved, owned, sellable, average, profit, afterCash, afterShares;
    private TextMeshProUGUI[] confirmationValues;
    private TextMeshProUGUI reviewCaption, submitCaption;
    private Image buyLine, sellLine, budgetFill;
    private TextMeshProUGUI buyCaption, sellCaption;
    private void Tile(Transform parent,float y)
    {
        var tile=Area("Account Tile",parent,0,y,736,88,736,704).gameObject.AddComponent<Image>(); tile.name="Image :: Account Tile"; tile.color=TabletKitStyle.Panel; tile.raycastTarget=false;
    }
    private Button current;
    public const float ControlHeight = 56f;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
    private static readonly Color Background = new Color32(13,23,29,255), Line = new Color32(44,60,69,255), Selected = new Color32(32,49,57,255);
    private static readonly Color Error = new Color32(239,89,100,255);
    private static TMP_FontAsset numerals;
    public static TMP_FontAsset Numerals => numerals ??= Resources.Load<TMP_FontAsset>("UI/TabletControls/Trading Numerals");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFont() { numerals = null; }

    public void Configure(DeviceTerminalUI terminal, GameObject pending, GameObject quick, Button[] quickButtons)
    {
        ui = terminal; reading = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        reviewCaption=ui.review.GetComponentInChildren<TextMeshProUGUI>(true); submitCaption=ui.submit.GetComponentInChildren<TextMeshProUGUI>(true);
        scale = ((RectTransform)transform).rect.width / 1600f;
        if (scale <= 0f) { scale = 1f; }
        foreach (Transform child in transform) { child.gameObject.SetActive(false); }
        var background = GetComponent<Image>(); background.sprite = null; background.type = Image.Type.Simple; background.color = Background;
        HeaderElement(ui.stockName,32,16,580,52,38,false);
        HeaderElement(ui.ticker,32,72,580,32,23,false); ui.ticker.color = Muted;
        HeaderElement(ui.currentPrice,640,8,400,72,44,true);
        Label("Price Unit",transform,"원",1052,8,40,72,26,1600,880).color = Muted;
        HeaderElement(ui.change,1104,8,464,72,28,true);
        Rule(transform,32,120,1536,1600,880);

        var left = Area("Account And Pending",transform,832,144,736,704,1600,880);
        var right = Area("Trade Grid",transform,32,144,736,704,1600,880);
        // 계좌 쪽은 두 칸뿐이다: 쓸 수 있는 돈, 그리고 이 종목을 얼마나 들고 있고 얼마를 벌고 있는지.
        // 예약금·매도 가능·평균가는 밑의 미체결 목록과 자산 탭에서 보인다.
        var unused = Area("Unused Metrics",left,0,0,736,704,736,704); unused.gameObject.SetActive(false);
        reserved = Metric(unused,"예약금",40,"원"); sellable = Metric(unused,"매도 가능",144,"주"); average = Metric(unused,"평균가",184,"원");
        Tile(left,0);
        Label("주문 가능 Label",left,"주문 가능",20,0,240,88,27,736,704).color=Muted;
        available = Label("주문 가능 Value",left,"",264,0,416,88,38,736,704); Numeric(available); available.alignment=TextAlignmentOptions.MidlineRight;
        Label("주문 가능 Unit",left,"원",692,0,40,88,25,736,704).color=Muted;
        Tile(left,100);
        Label("보유 Label",left,"보유",20,100,96,88,27,736,704).color=Muted;
        owned = Label("보유 Value",left,"",116,100,200,88,38,736,704); Numeric(owned); owned.alignment=TextAlignmentOptions.MidlineRight;
        Label("보유 Unit",left,"주",324,100,40,88,25,736,704).color=Muted;
        profit = Label("평가손익 Value",left,"",380,100,300,88,32,736,704); Numeric(profit); profit.alignment=TextAlignmentOptions.MidlineRight;
        Label("평가손익 Unit",left,"원",692,100,40,88,25,736,704).color=Muted;
        Place((RectTransform)pending.transform,left,0,216,736,488,736,704);
        ui.cancel.transform.SetParent(pending.transform,false);
        Place((RectTransform)ui.cancel.transform,pending.transform,592,0,144,ControlHeight,736,488);
        Action(ui.cancel,"전체 취소");

        Place((RectTransform)ui.draftPanel.transform,right,0,0,736,704,736,704);
        Place((RectTransform)ui.reviewPanel.transform,right,0,0,736,704,736,704);
        foreach (Transform child in ui.draftPanel.transform) { child.gameObject.SetActive(false); }
        foreach (Transform child in ui.reviewPanel.transform) { child.gameObject.SetActive(false); }
        Transform draft = ui.draftPanel.transform;
        var side = Area("Order Side",draft,0,0,736,ControlHeight,736,704);
        Place((RectTransform)ui.buy.transform,side,0,0,368,ControlHeight,736,ControlHeight);
        Place((RectTransform)ui.sell.transform,side,368,0,368,ControlHeight,736,ControlHeight);
        Segment(ui.buy,"매수"); Segment(ui.sell,"매도");
        buyLine = Rule(ui.buy.transform,0,54,368,368,56); sellLine = Rule(ui.sell.transform,0,54,368,368,56);
        buyLine.color = sellLine.color = Muted; buyLine.gameObject.SetActive(false); sellLine.gameObject.SetActive(false);
        buyCaption = ui.buy.GetComponentInChildren<TextMeshProUGUI>(true); sellCaption = ui.sell.GetComponentInChildren<TextMeshProUGUI>(true);
        var type = Area("Order Type Tabs",draft,0,72,464,ControlHeight,736,704);
        var limit = Label("Limit Order Type",type,"지정가",0,0,160,56,28,464,56);
        limit.alignment = TextAlignmentOptions.Center;
        Rule(type,0,54,160,464,56).color = Muted;
        current = quickButtons[0];
        Place((RectTransform)current.transform,draft,568,72,168,ControlHeight,736,704); Action(current,"현재가");
        InputRow(draft,ui.price,"가격","원",144);
        InputRow(draft,ui.quantity,"수량","주",216);
        Label("Quantity Ratio",draft,"비율",0,288,120,56,28,736,704).color = Muted;
        Place((RectTransform)quick.transform,draft,144,288,592,ControlHeight,736,704);
        for (int i = 1; i < quickButtons.Length; i++)
        {
            Place((RectTransform)quickButtons[i].transform,quick.transform,(i-1)*152,0,136,ControlHeight,592,56);
            Action(quickButtons[i],i==4 ? "100%" : (i*25)+"%");
            Numeric(quickButtons[i].GetComponentInChildren<TextMeshProUGUI>());
        }
        Place((RectTransform)ui.splitPanel.transform,draft,0,360,736,ControlHeight,736,704);
        foreach (Transform child in ui.splitPanel.transform) { child.gameObject.SetActive(false); }
        Place((RectTransform)ui.split.transform,draft,0,360,128,ControlHeight,736,704); Action(ui.split,"분할 OFF");
        SplitInput(ui.parts,"횟수","회",144,296);
        SplitInput(ui.interval,"간격","초",448,600);
        Rule(draft,0,436,736,736,704);
        Label("Order Total",draft,"예상 금액",0,452,280,56,28,736,704).color = Muted;
        Place(ui.amount.rectTransform,draft,288,452,376,56,736,704); TextStyle(ui.amount,36,true); ui.amount.alignment = TextAlignmentOptions.MidlineRight;
        Label("Order Total Unit",draft,"원",696,452,40,56,26,736,704).color = Muted;
        // 주문 후 현금·보유를 글자로 적는 대신, 이 주문이 가진 돈(또는 주식)의 얼마를 쓰는지 막대 하나로 보여 준다.
        var hidden = Area("Unused Forecast",draft,0,0,736,704,736,704); hidden.gameObject.SetActive(false);
        afterCash = Forecast(hidden,"주문 후 현금",516,"원"); afterShares = Forecast(hidden,"주문 후 보유",552,"주");
        var budget = Area("Order Budget",draft,0,528,736,18,736,704).gameObject.AddComponent<Image>(); budget.name="Image :: Order Budget"; budget.color=Line; budget.raycastTarget=false;
        budgetFill = Area("Order Budget Fill",budget.transform,0,0,736,18,736,18).gameObject.AddComponent<Image>(); budgetFill.name="Image :: Order Budget Fill"; budgetFill.raycastTarget=false;
        Place(ui.validation.rectTransform,draft,0,600,736,32,736,704); TextStyle(ui.validation,25,false); ui.validation.color = Error;
        Place((RectTransform)ui.review.transform,draft,0,648,736,ControlHeight,736,704); Action(ui.review,"매수 주문",true);

        Transform confirm = ui.reviewPanel.transform;
        Label("Review Heading",confirm,"주문 확인",0,0,736,56,32,736,704);
        confirmationValues = new TextMeshProUGUI[6];
        string[] captions = {"종목","가격","수량","금액","횟수","간격"};
        string[] units = {"","원","주","원","회","초"};
        for (int i=0;i<captions.Length;i++)
        {
            float y=80+i*72;
            Label("Review " + captions[i],confirm,captions[i],0,y,136,56,28,736,704).color=Muted;
            var value=Label("Review Value " + i,confirm,"",152,y,512,56,30,736,704);
            if(i>0) { Numeric(value); } value.alignment=TextAlignmentOptions.MidlineRight; confirmationValues[i]=value;
            Label("Review Unit " + i,confirm,units[i],696,y,40,56,26,736,704).color=Muted;
        }
        Place(ui.confirmation.rectTransform,confirm,0,536,736,72,736,704); TextStyle(ui.confirmation,25,false); ui.confirmation.color=Error;
        Place((RectTransform)ui.back.transform,confirm,0,648,360,ControlHeight,736,704); Action(ui.back,"수정");
        Place((RectTransform)ui.submit.transform,confirm,376,648,360,ControlHeight,736,704); Action(ui.submit,"주문",true);
        ui.reviewPanel.SetActive(false);
    }
    private void SplitInput(TMP_InputField field,string caption,string unit,float labelX,float fieldX)
    {
        Transform root=ui.splitPanel.transform;
        Label("Split " + caption,root,caption,labelX,0,72,56,26,736,56).color=Muted;
        Place((RectTransform)field.transform,root,fieldX-72,0,104,ControlHeight,736,56); InputStyle(field);
        Label("Split " + unit,root,unit,fieldX+40,0,40,56,25,736,56).color=Muted;
    }
    private void HeaderElement(TextMeshProUGUI label,float x,float y,float w,float h,float size,bool numeric)
    { Place(label.rectTransform,transform,x,y,w,h,1600,880); TextStyle(label,size,numeric); label.alignment=numeric?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft; }
    private TextMeshProUGUI Metric(Transform parent,string caption,float y,string unit)
    {
        Label(caption+" Label",parent,caption,0,y,248,36,27,736,704).color=Muted;
        var value=Label(caption+" Value",parent,"",264,y,416,36,26,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight;
        Label(caption+" Unit",parent,unit,696,y,40,36,25,736,704).color=Muted; return value;
    }
    private TextMeshProUGUI Forecast(Transform parent,string caption,float y,string unit)
    {
        Label(caption+" Label",parent,caption,0,y,280,32,23,736,704).color=Muted;
        var value=Label(caption+" Value",parent,"",288,y,376,32,23,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight; value.color=Muted;
        Label(caption+" Unit",parent,unit,696,y,40,32,23,736,704).color=Muted; return value;
    }
    private void InputRow(Transform parent,TMP_InputField field,string caption,string unit,float y)
    {
        Label(caption+" Input Label",parent,caption,0,y,120,56,28,736,704).color=Muted;
        Place((RectTransform)field.transform,parent,144,y,536,ControlHeight,736,704); InputStyle(field);
        Label(caption+" Input Unit",parent,unit,696,y,40,56,26,736,704).color=Muted;
    }
    private void InputStyle(TMP_InputField field)
    {
        TabletControlStyle.Input(field); Numeric(field.textComponent); field.fontAsset=field.textComponent.font;
        field.textComponent.fontSize=30*scale; field.textComponent.enableAutoSizing=false;
        field.textComponent.alignment=TextAlignmentOptions.MidlineRight; field.textComponent.margin=Vector4.zero;
        field.textComponent.fontStyle=FontStyles.Normal;
        field.textComponent.textWrappingMode=TextWrappingModes.NoWrap; field.richText=false;
        var viewport=field.textViewport; viewport.anchorMin=Vector2.zero; viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(16*scale,0); viewport.offsetMax=new Vector2(-16*scale,0);
        var text=field.textComponent.rectTransform; text.anchorMin=Vector2.zero; text.anchorMax=Vector2.one; text.offsetMin=text.offsetMax=Vector2.zero;
        var image=field.targetGraphic as Image; if(image!=null) { image.color=TabletKitStyle.Hover; }
    }
    private void Segment(Button button,string caption)
    {
        Action(button,caption); button.image.sprite=null; button.image.type=Image.Type.Simple;
        var colors=button.colors; colors.selectedColor=colors.normalColor=Color.white; button.colors=colors;
    }
    private void Action(Button button,string caption,bool primary=false)
    {
        TabletControlStyle.Button(button,primary);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(true);
        TextStyle(label,28,false); label.fontStyle=FontStyles.Normal; label.alignment=TextAlignmentOptions.Center; GameLocalizedText.Set(label,caption);
    }
    public static void Numeric(TMP_Text label)
    {
        var font=Numerals;
        if(font!=null) { label.font=font; label.fontSharedMaterial=font.material; }
        label.fontStyle=FontStyles.Normal;
        label.fontFeatures.Clear();
    }
    public void FitCanvas()
    {
        var rect=(RectTransform)transform;
        float fitted=Mathf.Min(rect.rect.width/1600f,rect.rect.height/880f);
        float ratio=fitted/scale;
        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) { text.fontSize*=ratio; }
        foreach (var input in GetComponentsInChildren<TMP_InputField>(true)) { input.pointSize=input.textComponent.fontSize; }
        GetComponentInChildren<PendingOrderListUI>(true)?.SetScale(fitted);
        scale=fitted;
    }
    private void TextStyle(TextMeshProUGUI label,float size,bool numeric)
    {
        if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
        if(numeric) { Numeric(label); }
        label.fontSize=size*scale; label.fontStyle=FontStyles.Normal; label.enableAutoSizing=false;
        label.margin=Vector4.zero; label.textWrappingMode=TextWrappingModes.NoWrap;
        label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; label.color=Ink;
    }
    public void Header(Stock stock,decimal reference)
    {
        DeviceTerminalUI.Set(ui.stockName,stock.stockName); DeviceTerminalUI.Set(ui.ticker,stock.symbol);
        DeviceTerminalUI.Set(ui.currentPrice,Number(stock.currentPrice));
        if(reference<=0)
        { DeviceTerminalUI.Set(ui.change,"—"); ui.change.color=Muted; return; }
        decimal delta=stock.currentPrice-reference;
        string rate=(delta/reference*100).ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture)+"%";
        DeviceTerminalUI.Set(ui.change,Signed(delta)+"  ("+rate+")"); ui.change.color=SignColor(delta);
    }
    public void Account(Stock stock)
    {
        var session=ui.session; var account=session.account;
        decimal volume=account.GetOwnedVolume(stock), basis=account.GetOwnedAverage(stock), pnl=(stock.currentPrice-basis)*volume;
        DeviceTerminalUI.Set(available,Number(session.AvailableCash)); DeviceTerminalUI.Set(reserved,Number(account.ReservedCash+session.ScheduledBuyCost));
        DeviceTerminalUI.Set(owned,Number(volume)); DeviceTerminalUI.Set(sellable,Number(session.AvailableShares(stock)));
        DeviceTerminalUI.Set(average,volume>0 ? Number(basis) : "—"); DeviceTerminalUI.Set(profit,Signed(pnl)); profit.color=SignColor(pnl);
    }
    public void Draft(Stock stock,bool buying,bool splitting,string error,decimal price,decimal quantity,decimal cost)
    {
        // 사는 쪽·파는 쪽은 시세 색 그대로 칠한다. 주문 단추도 같은 색이라 지금 무엇을 누르는지 색으로 안다.
        Color sideColor=buying?MarketColors.Up:MarketColors.Down;
        Color buyColor=buying?sideColor:(Color)TabletKitStyle.Panel, sellColor=buying?(Color)TabletKitStyle.Panel:sideColor;
        if(ui.buy.image.color!=buyColor) { ui.buy.image.color=buyColor; }
        if(ui.sell.image.color!=sellColor) { ui.sell.image.color=sellColor; }
        buyCaption.color=buying?TabletKitStyle.Background:Muted; sellCaption.color=buying?Muted:TabletKitStyle.Background;
        decimal whole=buying?ui.session.AvailableCash:ui.session.AvailableShares(stock), part=buying?cost:quantity;
        float used=whole>0?(float)(part/whole):part>0?2f:0f;
        budgetFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(used),1f); budgetFill.color=used>1f?Error:sideColor;
        DeviceTerminalUI.Set(ui.splitLabel,splitting?"분할 ON":"분할 OFF");
        DeviceTerminalUI.Set(ui.amount,cost>0?Number(cost):"—");
        DeviceTerminalUI.Set(reviewCaption,splitting?(buying?"분할 매수 확인":"분할 매도 확인"):(buying?"매수 주문":"매도 주문"));
        TabletControlStyle.Emphasize(ui.review,!splitting);
        if(!splitting) { ui.review.image.color=sideColor; reviewCaption.color=TabletKitStyle.Background; }
        DeviceTerminalUI.Set(afterCash,string.IsNullOrEmpty(error)?Number(ui.session.AvailableCash+(buying?-cost:cost)):"—");
        DeviceTerminalUI.Set(afterShares,string.IsNullOrEmpty(error)?Number(ui.session.account.GetOwnedVolume(stock)+(buying?quantity:-quantity)):"—");
        ShowError(error);
    }
    public void Confirm(Stock stock,bool buying,decimal price,decimal quantity,int parts,float seconds)
    {
        string[] values={stock.stockName,Number(price),Number(quantity),Number(price*quantity),parts.ToString(),(seconds*DeviceMarketJournal.GameSecondsPerRealSecond).ToString("0",CultureInfo.InvariantCulture)};
        for(int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(confirmationValues[i],values[i]); }
        DeviceTerminalUI.Set(ui.confirmation,"");
        DeviceTerminalUI.Set(submitCaption,buying?"매수 주문":"매도 주문");
        ui.submit.image.color=buying?MarketColors.Up:MarketColors.Down; submitCaption.color=TabletKitStyle.Background;
    }
    public void ShowError(string error) { DeviceTerminalUI.Set(ui.validation,ShortError(error)); }
    public static string ShortError(string error)
    {
        if(string.IsNullOrEmpty(error))
        { return ""; }
        if(error.Contains("잠겨"))
        { return "계좌 잠김"; }
        if(error.Contains("현금"))
        { return "현금 부족"; }
        if(error.Contains("매도 가능"))
        { return "수량 부족"; }
        if(error.Contains("호가"))
        { return "호가 단위 오류"; }
        if(error.Contains("정수"))
        { return "수량 범위 오류"; }
        if(error.Contains("분할"))
        { return "분할 범위 오류"; }
        if(error.Contains("거래할 수"))
        { return "거래 불가"; }
        if(error.Contains("금액"))
        { return "금액 범위 오류"; }
        return "주문 불가";
    }
    public static string Number(decimal value) => value.ToString("N0",CultureInfo.InvariantCulture);
    private static string Signed(decimal value) => value.ToString("+#,##0;−#,##0;0",CultureInfo.InvariantCulture);
    private static Color SignColor(decimal value) => MarketColors.ForChange(value,Ink);
    private TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,float pw,float ph)
    {
        var label=new GameObject("Text :: "+name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform,parent,x,y,w,h,pw,ph); TextStyle(label,size,false); label.alignment=TextAlignmentOptions.MidlineLeft; GameLocalizedText.Set(label,text); return label;
    }
    private static RectTransform Area(string name,Transform parent,float x,float y,float w,float h,float pw,float ph)
    {
        var rect=new GameObject("Area :: "+name,typeof(RectTransform)).GetComponent<RectTransform>();
        Place(rect,parent,x,y,w,h,pw,ph); return rect;
    }
    private static Image Rule(Transform parent,float x,float y,float w,float pw,float ph)
    {
        var image=Area("Divider",parent,x,y,w,1,pw,ph).gameObject.AddComponent<Image>();
        image.name="Image :: Divider"; image.color=Line; image.raycastTarget=false; return image;
    }
    public static void Place(RectTransform rect,Transform parent,float x,float y,float w,float h,float pw,float ph)
    {
        rect.SetParent(parent,false); rect.gameObject.layer=parent.gameObject.layer; rect.gameObject.SetActive(true);
        rect.anchorMin=new Vector2(x/pw,1-(y+h)/ph); rect.anchorMax=new Vector2((x+w)/pw,1-y/ph);
        rect.pivot=new Vector2(.5f,.5f); rect.offsetMin=rect.offsetMax=Vector2.zero; rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
    }
}
