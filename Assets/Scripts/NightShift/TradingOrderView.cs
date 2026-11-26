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
    private Image buyLine, sellLine, budgetFill, sectorIcon;
    private RectTransform logo;
    private Stock logoStock;
    private Slider ratio;
    private float ratioTouched = -10f;
    private TextMeshProUGUI totalCaption;
    private static string Word(string ko,string en,string ja) => GameLocale.Code=="ja" ? ja : GameLocale.Code=="en" ? en : ko;
    /// <summary>이 화면에서 새로 쓴 쉬운 말은 번역 표를 거치지 않고 언어마다 직접 적는다.</summary>
    private static TextMeshProUGUI Literal(TextMeshProUGUI label,string ko,string en,string ja) { LiteralText.Apply(label,ko,en,ja); return label; }
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
    // 숫자도 화면의 다른 글자와 같은 동적 SDF 폰트를 쓴다. 숫자 20자만 구워 둔 정적 에셋(Trading Numerals)은 그 밖의 글자가 들어오면 깨져서 쓰지 않는다.
    public static TMP_FontAsset Numerals => numerals ??= Resources.Load<TMP_FontAsset>("Chat/Chat Korean");
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
        logo=Area("Stock Logo",transform,32,22,60,60,1600,880);
        HeaderElement(ui.stockName,108,16,520,52,38,false);
        HeaderElement(ui.ticker,140,72,480,32,23,false); ui.ticker.color = Muted;
        sectorIcon=Area("Sector Icon",transform,108,74,26,28,1600,880).gameObject.AddComponent<Image>(); sectorIcon.preserveAspect=true; sectorIcon.raycastTarget=false; sectorIcon.color=Muted;
        HeaderElement(ui.currentPrice,640,8,400,72,44,true);
        Label("Price Unit",transform,"돈",1052,8,40,72,26,1600,880).color = Muted;
        HeaderElement(ui.change,1104,8,464,72,28,true);
        Rule(transform,32,120,1536,1600,880);

        var left = Area("Account And Pending",transform,832,144,736,704,1600,880);
        var right = Area("Trade Grid",transform,32,144,736,704,1600,880);
        // 계좌 쪽은 두 칸뿐이다: 쓸 수 있는 돈, 그리고 이 종목을 얼마나 들고 있고 얼마를 벌고 있는지.
        // 예약금·매도 가능·평균가는 밑의 미체결 목록과 자산 탭에서 보인다.
        var unused = Area("Unused Metrics",left,0,0,736,704,736,704); unused.gameObject.SetActive(false);
        reserved = Metric(unused,"예약금",40,"돈"); sellable = Metric(unused,"매도 가능",144,"주"); average = Metric(unused,"평균가",184,"돈");
        // 위: 호가 사다리(내 값이 어디쯤인지, 이 값이면 바로 되는지). 줄을 누르면 그 값이 가격 칸에 들어간다.
        var ladderArea = Area("Order Ladder",left,0,0,736,320,736,704);
        OrderLadder.Create(ui,ladderArea,reading,scale);
        // 가운데 한 줄: 쓸 수 있는 돈, 그리고 이 종목을 얼마나 들고 있고 지금 팔면 얼마인지.
        Rule(left,0,322,736,736,704);
        Literal(Label("주문 가능 Label",left,"",0,328,170,38,23,736,704),"주문 가능 금액","Buying power","注文可能額").color=Muted;
        available = Label("주문 가능 Value",left,"",170,328,190,38,27,736,704); Numeric(available); available.alignment=TextAlignmentOptions.MidlineRight;
        Literal(Label("보유 Label",left,"",384,328,60,38,23,736,704),"보유","Held","保有").color=Muted;
        owned = Label("보유 Value",left,"",444,328,110,38,27,736,704); Numeric(owned); owned.alignment=TextAlignmentOptions.MidlineRight;
        profit = Label("평가손익 Value",left,"",560,328,176,38,24,736,704); Numeric(profit); profit.alignment=TextAlignmentOptions.MidlineRight;
        Place((RectTransform)pending.transform,left,0,374,736,330,736,704);
        ui.cancel.transform.SetParent(pending.transform,false);
        Place((RectTransform)ui.cancel.transform,pending.transform,592,0,144,ControlHeight,736,488);
        Action(ui.cancel,"전체 취소"); Literal(ui.cancel.GetComponentInChildren<TextMeshProUGUI>(true),"전체 취소","Cancel all","全取消"); TabletControlStyle.Danger(ui.cancel);

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
        var limit = Literal(Label("Limit Order Type",type,"",0,0,200,56,25,464,56),"지정가","Limit price","指値");
        limit.alignment = TextAlignmentOptions.Center;
        Rule(type,0,54,160,464,56).color = Muted;
        current = quickButtons[0];
        Place((RectTransform)current.transform,draft,568,72,168,ControlHeight,736,704); Action(current,"현재가"); Literal(current.GetComponentInChildren<TextMeshProUGUI>(true),"현재가","Market price","現在値");
        InputRow(draft,ui.price,"가격","돈",144);
        InputRow(draft,ui.quantity,"수량","주",216);
        Literal(Label("Quantity Ratio",draft,"",0,288,140,56,24,736,704),"비율","Ratio","割合").color = Muted;
        // 비율: 슬라이더로 끌어 고르고, 밑의 25·50·75·100% 눈금을 누르면 그 비율로 딱 맞춘다(눈금은 예전의 비율 단추 그대로).
        ratio=RatioSlider(draft,144,288,592,26);
        Place((RectTransform)quick.transform,draft,144,318,592,26,736,704);
        for (int i = 1; i < quickButtons.Length; i++)
        {
            Place((RectTransform)quickButtons[i].transform,quick.transform,(i-1)*152,0,136,26,592,26);
            Action(quickButtons[i],i==4 ? "100%" : (i*25)+"%");
            Numeric(quickButtons[i].GetComponentInChildren<TextMeshProUGUI>()); quickButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize=20*scale;
            // 눈금은 상자 없이 글자만(누르면 그 비율). 슬라이더 바로 밑에 붙어 있어 눈금으로 읽힌다.
            TabletControlStyle.Row(quickButtons[i],false); quickButtons[i].GetComponentInChildren<TextMeshProUGUI>().color=Muted;
        }
        Place((RectTransform)ui.splitPanel.transform,draft,0,360,736,ControlHeight,736,704);
        foreach (Transform child in ui.splitPanel.transform) { child.gameObject.SetActive(false); }
        Place((RectTransform)ui.split.transform,draft,0,360,128,ControlHeight,736,704); Action(ui.split,"분할 OFF"); ui.split.GetComponentInChildren<TextMeshProUGUI>(true).fontSize=21*scale;
        SplitInput(ui.parts,"횟수","회",144,296);
        SplitInput(ui.interval,"간격","초",448,600);
        Rule(draft,0,436,736,736,704);
        totalCaption=Label("Order Total",draft,"",0,452,288,56,24,736,704); totalCaption.color = Muted;
        Place(ui.amount.rectTransform,draft,288,452,376,56,736,704); TextStyle(ui.amount,36,true); ui.amount.alignment = TextAlignmentOptions.MidlineRight;
        Label("Order Total Unit",draft,"돈",696,452,40,56,26,736,704).color = Muted;
        // 거래 수수료. 평소에는 매도 대금의 0.05%이고, 거래소의 수수료 0% 이벤트 날에는 0%로 바뀌며 색이 달라진다(Header에서 갱신).
        feeLabel = Label("Fee Label",draft,"",0,554,300,36,23,736,704); feeLabel.color = Muted;
        feeValue = Label("Fee Value",draft,"",300,554,436,36,23,736,704); feeValue.alignment = TextAlignmentOptions.MidlineRight;
        // 주문 후 현금·보유를 글자로 적는 대신, 이 주문이 가진 돈(또는 주식)의 얼마를 쓰는지 막대 하나로 보여 준다.
        var hidden = Area("Unused Forecast",draft,0,0,736,704,736,704); hidden.gameObject.SetActive(false);
        afterCash = Forecast(hidden,"주문 후 현금",516,"돈"); afterShares = Forecast(hidden,"주문 후 보유",552,"주");
        var budget = Area("Order Budget",draft,0,528,736,18,736,704).gameObject.AddComponent<Image>(); budget.name="Image :: Order Budget"; budget.color=Line; budget.raycastTarget=false;
        budgetFill = Area("Order Budget Fill",budget.transform,0,0,736,18,736,18).gameObject.AddComponent<Image>(); budgetFill.name="Image :: Order Budget Fill"; budgetFill.raycastTarget=false;
        Place(ui.validation.rectTransform,draft,0,600,736,32,736,704); TextStyle(ui.validation,25,false); ui.validation.color = Error;
        Place((RectTransform)ui.review.transform,draft,0,648,736,ControlHeight,736,704); Action(ui.review,"매수 주문",true);

        Transform confirm = ui.reviewPanel.transform;
        Label("Review Heading",confirm,"주문 확인",0,0,736,56,32,736,704);
        confirmationValues = new TextMeshProUGUI[6];
        string[] captions = {"종목","가격","수량","금액","횟수","간격"};
        string[] units = {"","돈","주","돈","회","초"};
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
    /// <summary>수량 비율 슬라이더(0~100%). 끌면 살 수 있는(팔 수 있는) 최대 수량의 그만큼이 수량 칸에 들어간다.</summary>
    private Slider RatioSlider(Transform parent,float x,float y,float w,float h)
    {
        var root=Area("Quantity Ratio Slider",parent,x,y,w,h,736,704);
        root.gameObject.AddComponent<Image>().color=Color.clear;
        RectTransform Part(string part,Transform holder,float bottom,float top,Color tone,bool image)
        {
            var item=new GameObject((image ? "Image :: " : "Area :: ")+"Ratio "+part,typeof(RectTransform)); item.layer=gameObject.layer;
            var rect=(RectTransform)item.transform; rect.SetParent(holder,false);
            rect.anchorMin=new Vector2(0f,bottom); rect.anchorMax=new Vector2(1f,top); rect.offsetMin=rect.offsetMax=Vector2.zero;
            if (image) { var picture=item.AddComponent<Image>(); picture.color=tone; picture.raycastTarget=false; }
            return rect;
        }
        Part("Track",root,.4f,.6f,Line,true);
        var fillArea=Part("Fill Area",root,.4f,.6f,Color.clear,false); var handleArea=Part("Handle Area",root,0f,1f,Color.clear,false);
        var fill=Part("Fill",fillArea,0f,1f,TabletKitStyle.Accent,true);
        var handle=Part("Handle",handleArea,0f,1f,Ink,true); handle.sizeDelta=new Vector2(14f*scale,0f); handle.GetComponent<Image>().raycastTarget=true;
        var slider=root.gameObject.AddComponent<Slider>(); slider.minValue=0f; slider.maxValue=100f; slider.wholeNumbers=true;
        slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(value => { ratioTouched=Time.unscaledTime; ui.SetQuantityRatio((decimal)value/100m); });
        return slider;
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
    private TextMeshProUGUI feeLabel, feeValue;
    private static string FeeWord(string korean,string english,string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>주문 화면의 수수료 한 줄: "거래 수수료  0.05% · 매도 시". 이벤트 날에는 "0% · 오늘 이벤트"가 강조색으로 적힌다.</summary>
    private void ShowFee()
    {
        if(feeLabel==null) { return; }
        bool free=FeeHoliday.Active;
        GameLocalizedText.Set(feeLabel,FeeWord("거래 수수료","Trading fee","取引手数料"),true);
        GameLocalizedText.Set(feeValue,FeeHoliday.RateText+" · "+(free ? FeeWord("오늘 이벤트","event today","本日イベント") : FeeWord("매도 시","on sells","売却時")),true);
        feeValue.color=free ? TabletKitStyle.Accent : Muted;
    }
    public void Header(Stock stock,decimal reference)
    {
        ShowFee();
        DeviceTerminalUI.Set(ui.stockName,stock.stockName);
        // 약호 옆에 업종(그림과 이름). 로고는 종목이 바뀔 때만 다시 조립한다.
        var sector=UiPictograms.SectorOf(stock); string tickerLine=stock.symbol+"  ·  "+UiPictograms.SectorName(sector);
        if (!GameLocalizedText.Matches(ui.ticker,tickerLine)) { GameLocalizedText.Set(ui.ticker,tickerLine,true); }
        if (logoStock!=stock) { logoStock=stock; UiPictograms.Logo(logo,stock,Background); sectorIcon.sprite=UiPictograms.SectorIcon(sector); }
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
        Color buyColor=buying?sideColor:TabletControlStyle.Raised, sellColor=buying?TabletControlStyle.Raised:sideColor;
        if(ui.buy.image.color!=buyColor) { ui.buy.image.color=buyColor; }
        if(ui.sell.image.color!=sellColor) { ui.sell.image.color=sellColor; }
        buyCaption.color=buying?TabletControlStyle.OnFill(sideColor):Muted; sellCaption.color=buying?Muted:TabletControlStyle.OnFill(sideColor);
        decimal whole=buying?ui.session.AvailableCash:ui.session.AvailableShares(stock), part=buying?cost:quantity;
        float used=whole>0?(float)(part/whole):part>0?2f:0f;
        budgetFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(used),1f); budgetFill.color=used>1f?Error:sideColor;
        Literal(ui.splitLabel,splitting?"분할 ON":"분할 OFF",splitting?"Split ON":"Split OFF",splitting?"分割 ON":"分割 OFF");
        // 슬라이더는 수량 칸을 따라간다(손으로 끄는 중에는 건드리지 않는다).
        if (ratio!=null && Time.unscaledTime-ratioTouched>.6f) { float shown=Mathf.Clamp01(used)*100f; if (Mathf.Abs(ratio.value-shown)>.5f) { ratio.SetValueWithoutNotify(shown); } }
        if (totalCaption!=null) { string share=used<=0f ? "" : used>1f ? Word(" · 가진 것보다 많음"," · more than you have"," · 保有を超過") : (buying ? Word(" · 가진 돈의 "," · "," · 資金の ") : Word(" · 가진 주식의 "," · "," · 保有株の "))+(used<.0001f ? "<0.01" : (used*100f).ToString(used<.01f ? "0.00" : used<.1f ? "0.0" : "0"))+"%"+(GameLocale.Code=="en" ? (buying ? " of cash" : " of shares") : ""); Literal(totalCaption,"예상 금액"+share,"Order total"+share,"予想金額"+share); }
        DeviceTerminalUI.Set(ui.amount,cost>0?Number(cost):"—");
        DeviceTerminalUI.Set(reviewCaption,splitting?(buying?"분할 매수 확인":"분할 매도 확인"):(buying?"매수 주문":"매도 주문"));
        // 화면의 주 동작이다. 분할 주문도 같은 단추를 누르므로 늘 매수·매도 색의 주 동작 모양이다.
        TabletControlStyle.MainAction(ui.review,sideColor);
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
        TabletControlStyle.MainAction(ui.submit,buying?MarketColors.Up:MarketColors.Down);
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
