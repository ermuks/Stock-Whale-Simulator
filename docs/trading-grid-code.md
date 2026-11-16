# 거래 그리드 코드 변경 기록

2026-11-15. 작업 직전 백업과 비교한다. 기존 미커밋 작업이나 씬을 HEAD로 되돌리지 않았다.

## 신규 C# 전문

### Assets/Scripts/NightShift/TradingOrderView.cs

```csharp
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
    private Image buyLine, sellLine;
    private Button current;
    public const float ControlHeight = 56f;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
    private static readonly Color Background = new Color32(13,23,29,255), Line = new Color32(44,60,69,255), Selected = new Color32(32,49,57,255);
    private static readonly Color Up = new Color32(239,89,100,255), Down = new Color32(76,150,232,255);
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
        HeaderElement(ui.stockName,32,16,580,52,42,false);
        HeaderElement(ui.ticker,32,72,580,32,26,false); ui.ticker.color = Muted;
        HeaderElement(ui.currentPrice,640,8,400,72,44,true);
        Label("Price Unit",transform,"원",1052,8,40,72,26,1600,880).color = Muted;
        HeaderElement(ui.change,1104,8,464,72,28,true);
        Rule(transform,32,120,1536,1600,880);

        var left = Area("Account And Pending",transform,32,144,736,704,1600,880);
        var right = Area("Trade Grid",transform,832,144,736,704,1600,880);
        available = Metric(left,"주문 가능",0,"원"); reserved = Metric(left,"예약금",40,"원");
        Rule(left,0,92,736,736,704);
        owned = Metric(left,"보유",104,"주"); sellable = Metric(left,"매도 가능",144,"주");
        average = Metric(left,"평균가",184,"원"); profit = Metric(left,"평가손익",224,"원");
        Place((RectTransform)pending.transform,left,0,304,736,400,736,704);
        ui.cancel.transform.SetParent(pending.transform,false);
        Place((RectTransform)ui.cancel.transform,pending.transform,592,0,144,ControlHeight,736,400);
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
        buyLine.color = sellLine.color = Muted;
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
        afterCash = Forecast(draft,"주문 후 현금",516,"원"); afterShares = Forecast(draft,"주문 후 보유",552,"주");
        Place(ui.validation.rectTransform,draft,0,600,736,32,736,704); TextStyle(ui.validation,25,false); ui.validation.color = Up;
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
        Place(ui.confirmation.rectTransform,confirm,0,536,736,72,736,704); TextStyle(ui.confirmation,25,false); ui.confirmation.color=Up;
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
        var value=Label(caption+" Value",parent,"",264,y,416,36,30,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight;
        Label(caption+" Unit",parent,unit,696,y,40,36,25,736,704).color=Muted; return value;
    }
    private TextMeshProUGUI Forecast(Transform parent,string caption,float y,string unit)
    {
        Label(caption+" Label",parent,caption,0,y,280,32,25,736,704).color=Muted;
        var value=Label(caption+" Value",parent,"",288,y,376,32,27,736,704); Numeric(value); value.alignment=TextAlignmentOptions.MidlineRight; value.color=Muted;
        Label(caption+" Unit",parent,unit,696,y,40,32,25,736,704).color=Muted; return value;
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
        var image=field.targetGraphic as Image; if(image!=null) { image.color=new Color(.8f,.86f,.9f,1); }
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
        TextStyle(label,28,false); label.fontStyle=FontStyles.Normal; label.alignment=TextAlignmentOptions.Center; label.text=caption;
    }
    public static void Numeric(TMP_Text label)
    {
        var font=Numerals;
        if(font!=null) { label.font=font; label.fontSharedMaterial=font.material; }
        label.fontStyle=FontStyles.Normal;
        label.fontFeatures.Clear();
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
        Color buyColor=buying?Selected:Background, sellColor=buying?Background:Selected;
        if(ui.buy.image.color!=buyColor) { ui.buy.image.color=buyColor; }
        if(ui.sell.image.color!=sellColor) { ui.sell.image.color=sellColor; }
        if(buyLine.enabled!=buying) { buyLine.enabled=buying; }
        if(sellLine.enabled==buying) { sellLine.enabled=!buying; }
        DeviceTerminalUI.Set(ui.splitLabel,splitting?"분할 ON":"분할 OFF");
        DeviceTerminalUI.Set(ui.amount,cost>0?Number(cost):"—");
        DeviceTerminalUI.Set(reviewCaption,splitting?(buying?"분할 매수 확인":"분할 매도 확인"):(buying?"매수 주문":"매도 주문"));
        TabletControlStyle.Emphasize(ui.review,!splitting);
        DeviceTerminalUI.Set(afterCash,string.IsNullOrEmpty(error)?Number(ui.session.AvailableCash+(buying?-cost:cost)):"—");
        DeviceTerminalUI.Set(afterShares,string.IsNullOrEmpty(error)?Number(ui.session.account.GetOwnedVolume(stock)+(buying?quantity:-quantity)):"—");
        ShowError(error);
    }
    public void Confirm(Stock stock,bool buying,decimal price,decimal quantity,int parts,float seconds)
    {
        string[] values={stock.stockName,Number(price),Number(quantity),Number(price*quantity),parts.ToString(),seconds.ToString("0",CultureInfo.InvariantCulture)};
        for(int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(confirmationValues[i],values[i]); }
        DeviceTerminalUI.Set(ui.confirmation,"");
        DeviceTerminalUI.Set(submitCaption,buying?"매수 주문":"매도 주문");
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
    private static Color SignColor(decimal value) => value>0?Up:value<0?Down:Ink;
    private TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,float pw,float ph)
    {
        var label=new GameObject("Text :: "+name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform,parent,x,y,w,h,pw,ph); TextStyle(label,size,false); label.alignment=TextAlignmentOptions.MidlineLeft; label.text=text; return label;
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
```

### Assets/Editor/TradingNumeralFontSetup.cs

```csharp
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>Dedicated SDF numerals with equal digit advances; existing fonts are never modified.</summary>
public static class TradingNumeralFontSetup
{
    [MenuItem("DonDon/UI/Prepare Trading Numerals")]
    public static void Ensure()
    {
        const string path = "Assets/Resources/UI/TabletControls/Trading Numerals.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
        { return; }
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/News/Pretendard-Regular.otf");
        if (source == null) { throw new FileNotFoundException("Pretendard Regular source missing"); }
        var font = TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic);
        font.name = "Trading Numerals";
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!font.TryAddCharacters("0123456789,+-−.%/—()")) { throw new System.InvalidOperationException("Numeric glyph generation failed"); }
        float advance = 0;
        for (uint digit = '0'; digit <= '9'; digit++) { advance = Mathf.Max(advance,font.characterLookupTable[digit].glyph.metrics.horizontalAdvance); }
        for (uint digit = '0'; digit <= '9'; digit++)
        {
            var glyph = font.characterLookupTable[digit].glyph;
            var metrics = glyph.metrics;
            glyph.metrics = new GlyphMetrics(metrics.width,metrics.height,metrics.horizontalBearingX + (advance - metrics.horizontalAdvance) * .5f,metrics.horizontalBearingY,advance);
        }
        // Static prevents a later dynamic repack from restoring proportional source advances.
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);
        AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
    }
}
```

## 기존 함수 비교

기존 DeviceTerminalUI의 serialized 필드는 유지한다. 불필요한 receipt/draftSource 표시 상태를 제거하고 TradingOrderView 참조를 추가했다. PendingOrderListUI의 행은 고정 열과 별도 숫자 레이블로 교체했다.

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · ConfigureQuickOrder

원본:

```csharp
private void ConfigureQuickOrder()
    {
        reviewLabel = review.GetComponentInChildren<TextMeshProUGUI>();
        budget.gameObject.SetActive(false); progress.transform.parent.gameObject.SetActive(false);
        Set(prepare.GetComponentInChildren<TextMeshProUGUI>(),"입력 초기화");
        Layout((RectTransform)prepare.transform,.30f,.21f,.155f,.05f);
        Set(cancel.GetComponentInChildren<TextMeshProUGUI>(),"전체 취소");
        Layout((RectTransform)cancel.transform,.325f,.535f,.13f,.045f);
        Layout(position.rectTransform,.025f,.275f,.43f,.14f);
        Layout(cash.rectTransform,.025f,.425f,.43f,.10f);
        cash.fontSize = 28f; plan.gameObject.SetActive(false);
        var pendingRoot = new GameObject("Area :: Pending Orders",typeof(RectTransform));
        pendingRoot.transform.SetParent(panel.transform,false); pendingRoot.layer = panel.layer;
        Layout((RectTransform)pendingRoot.transform,.025f,.535f,.43f,.395f);
        pendingOrders = pendingRoot.AddComponent<PendingOrderListUI>();
        pendingOrders.Configure(session,reviewLabel);
        cancel.transform.SetAsLastSibling();
        quickQuantityPanel = new GameObject("Area :: Quick Order Amounts",typeof(RectTransform));
        quickQuantityPanel.transform.SetParent(draftPanel.transform,false);
        quickQuantityPanel.layer = draftPanel.layer;
        Layout((RectTransform)quickQuantityPanel.transform,0,.45f,1,.09f);
        quickButtons = new[] {
            QuickButton("Current Price","현재가",0,.49f), QuickButton("Quarter","25%",.51f,.15f),
            QuickButton("Half","50%",.68f,.15f), QuickButton("Maximum","최대",.85f,.15f)
        };
        quickButtons[0].onClick.AddListener(CurrentPrice);
        quickButtons[1].onClick.AddListener(() => SetQuantity(.25m));
        quickButtons[2].onClick.AddListener(() => SetQuantity(.5m));
        quickButtons[3].onClick.AddListener(() => SetQuantity(1m));
    }
```

수정본:

```csharp
private void ConfigureQuickOrder()
    {
        reviewLabel = review.GetComponentInChildren<TextMeshProUGUI>();
        var pendingRoot = new GameObject("Area :: Pending Orders",typeof(RectTransform));
        pendingRoot.transform.SetParent(panel.transform,false); pendingRoot.layer = panel.layer;
        pendingOrders = pendingRoot.AddComponent<PendingOrderListUI>();
        pendingOrders.Configure(session,reviewLabel,((RectTransform)panel.transform).rect.width / 1600f);
        quickQuantityPanel = new GameObject("Area :: Quick Order Amounts",typeof(RectTransform));
        quickQuantityPanel.transform.SetParent(draftPanel.transform,false);
        quickQuantityPanel.layer = draftPanel.layer;
        quickButtons = new[] {
            QuickButton("Current Price","현재가",0,.49f), QuickButton("Quarter","25%",.51f,.15f),
            QuickButton("Half","50%",.68f,.15f), QuickButton("Three Quarters","75%",.85f,.15f), QuickButton("Maximum","100%",.85f,.15f)
        };
        quickButtons[0].onClick.AddListener(CurrentPrice);
        quickButtons[1].onClick.AddListener(() => SetQuantity(.25m));
        quickButtons[2].onClick.AddListener(() => SetQuantity(.5m));
        quickButtons[3].onClick.AddListener(() => SetQuantity(.75m));
        quickButtons[4].onClick.AddListener(() => SetQuantity(1m));
        tradeView = panel.AddComponent<TradingOrderView>();
        tradeView.Configure(this,pendingRoot,quickQuantityPanel,quickButtons);
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · LoadSignalDraft

원본:

```csharp
public bool LoadSignalDraft(Stock stock, bool buyOrder, decimal limit, string source)
    {
        if (journal || stock == null || !stock.AcceptsOrders || !PriceTickPolicy.IsValidPrice(limit))
        { return false; }
        int index = StockManager.Instance.stocks.IndexOf(stock);
        if (index < 0 || !session.Prepare(stock))
        { return false; }
        StockManager.Instance.SelectStock(index); selected = stock;
        buying = buyOrder; splitting = confirming = receipt = false;
        draftSource = source;
        price.SetTextWithoutNotify(limit.ToString("0",CultureInfo.InvariantCulture)); quantity.SetTextWithoutNotify("1");
        FocusScreen(); next = 0f; return true;
    }
```

수정본:

```csharp
public bool LoadSignalDraft(Stock stock, bool buyOrder, decimal limit, string source)
    {
        if (journal || stock == null || !stock.AcceptsOrders || !PriceTickPolicy.IsValidPrice(limit))
        { return false; }
        int index = StockManager.Instance.stocks.IndexOf(stock);
        if (index < 0 || !session.Prepare(stock))
        { return false; }
        StockManager.Instance.SelectStock(index); selected = stock;
        buying = buyOrder; splitting = confirming = false;
        price.SetTextWithoutNotify(limit.ToString("0",CultureInfo.InvariantCulture)); quantity.SetTextWithoutNotify("1");
        FocusScreen(); next = 0f; return true;
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Prepare

원본:

```csharp
private void Prepare()
    {
        if (session.Prepare(selected))
        {
            receipt = confirming = false; draftSource = "";
            CurrentPrice(); quantity.SetTextWithoutNotify("1");
        }
        next = 0f;
    }
```

수정본:

```csharp
private void Prepare()
    {
        if (session.Prepare(selected))
        {
            confirming = false;
            CurrentPrice(); quantity.SetTextWithoutNotify("1");
        }
        next = 0f;
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Review

원본:

```csharp
private void Review()
    {
        string error = Check(out decimal p, out decimal q, out int n, out float seconds);
        if (!string.IsNullOrEmpty(error))
        { Set(validation, error); return; }
        if (!splitting)
        {
            session.Submit(selected,buying,p,q,1,1f);
            confirming = receipt = false; draftSource = ""; next = 0f;
            Order(); return;
        }
        reviewedStock = selected; reviewedPrice = p; reviewedQuantity = q; reviewedParts = n; reviewedInterval = seconds;
        confirming = true; receipt = false;
        Set(confirmation, selected.stockName + "\n\n" + (buying ? "매수" : "매도") + " · 지정가 " + Money(p) + "\n총 " + q.ToString("N0") + "주 · " + Money(p * q) + "\n" + (n > 1 ? n + "회 분할 · " + seconds + "초 간격" : "한 번에 제출") + "\n\n지정가 조건과 실제 호가에 따라 체결됩니다.");
        next = 0f;
    }
```

수정본:

```csharp
private void Review()
    {
        string error = Check(out decimal p, out decimal q, out int n, out float seconds);
        if (!string.IsNullOrEmpty(error))
        { tradeView.ShowError(error); return; }
        if (!splitting)
        {
            if (!session.Submit(selected,buying,p,q,1,1f))
            { tradeView.ShowError(session.Feedback); return; }
            confirming = false; next = 0f;
            Order(); return;
        }
        reviewedStock = selected; reviewedPrice = p; reviewedQuantity = q; reviewedParts = n; reviewedInterval = seconds;
        confirming = true;
        tradeView.Confirm(selected,buying,p,q,n,seconds);
        next = 0f;
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Submit

원본:

```csharp
private void Submit()
    {
        if (!confirming || reviewedStock != selected)
        { return; }
        if (session.Submit(reviewedStock, buying, reviewedPrice, reviewedQuantity, reviewedParts, reviewedInterval))
        { confirming = false; receipt = true; draftSource = ""; }
        else
        { Set(confirmation, session.Feedback + "\n\n입력값을 다시 확인하세요."); }
        next = 0f;
    }
```

수정본:

```csharp
private void Submit()
    {
        if (!confirming || reviewedStock != selected)
        { return; }
        if (session.Submit(reviewedStock, buying, reviewedPrice, reviewedQuantity, reviewedParts, reviewedInterval))
        { confirming = false; }
        else
        { Set(confirmation, TradingOrderView.ShortError(session.Feedback)); }
        next = 0f;
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Back

원본:

```csharp
private void Back() { confirming = receipt = false; next = 0f; }
```

수정본:

```csharp
private void Back() { confirming = false; next = 0f; }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Update

원본:

```csharp
void Update()
    {
        if (!panel.activeInHierarchy || Time.unscaledTime < next || StockManager.Instance == null || session.account == null)
        { return; }
        next = Time.unscaledTime + .3f;
        Stock stock = StockManager.Instance.CurrentStock;
        if (selected != stock)
        {
            selected = stock;
            if (!journal)
            {
                confirming = false;
                if (selected != null)
                {
                    session.Prepare(selected); receipt = false; draftSource = "";
                    price.SetTextWithoutNotify(PriceTickPolicy.RoundToTick(selected.currentPrice).ToString("0",CultureInfo.InvariantCulture));
                    quantity.SetTextWithoutNotify("1");
                }
            }
        }
        if (selected == null)
        { return; }
        if (!journal && selected != session.Target) { Prepare(); }
        if (journal && newsReader != null)
        {
            if (growthPanel.activeSelf && PlayerProgression.Instance != null) { visualGrowth?.Refresh(PlayerProgression.Instance); }
            return;
        }
        Header();
        if (journal)
        { Journal(); }
        else
        { Order(); }
    }
```

수정본:

```csharp
void Update()
    {
        if (!panel.activeInHierarchy || Time.unscaledTime < next || StockManager.Instance == null || session.account == null)
        { return; }
        next = Time.unscaledTime + .3f;
        Stock stock = StockManager.Instance.CurrentStock;
        if (selected != stock)
        {
            selected = stock;
            if (!journal)
            {
                confirming = false;
                if (selected != null)
                {
                    session.Prepare(selected);
                    price.SetTextWithoutNotify(PriceTickPolicy.RoundToTick(selected.currentPrice).ToString("0",CultureInfo.InvariantCulture));
                    quantity.SetTextWithoutNotify("1");
                }
            }
        }
        if (selected == null)
        { return; }
        if (!journal && selected != session.Target) { Prepare(); }
        if (journal && newsReader != null)
        {
            if (growthPanel.activeSelf && PlayerProgression.Instance != null) { visualGrowth?.Refresh(PlayerProgression.Instance); }
            return;
        }
        Header();
        if (journal)
        { Journal(); }
        else
        { Order(); }
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Header

원본:

```csharp
private void Header()
    {
        Set(stockName, selected.stockName); Set(ticker, selected.symbol + " · 현재 선택 종목");
        Set(currentPrice, Money(selected.currentPrice));
        decimal reference = StockStatistics.Instance == null ? selected.currentPrice : StockStatistics.Instance.GetStats(selected).prevDayClose;
        decimal delta = selected.currentPrice - reference;
        Set(change, (delta >= 0m ? "+" : "") + Money(delta) + "  (" + (reference > 0m ? (delta / reference * 100m).ToString("+0.0;-0.0;0.0") : "0.0") + "%)");
        change.color = delta >= 0m ? Up : Down;
        graph.color = change.color; graph.SetStock(selected);
        Set(graphRange, "최근 40개 캔들 · 전일 " + Money(reference));
    }
```

수정본:

```csharp
private void Header()
    {
        if (!journal)
        {
            decimal previous = StockStatistics.Instance == null ? selected.currentPrice : StockStatistics.Instance.GetStats(selected).prevDayClose;
            tradeView.Header(selected,previous); return;
        }
        Set(stockName, selected.stockName); Set(ticker, selected.symbol + " · 현재 선택 종목");
        Set(currentPrice, Money(selected.currentPrice));
        decimal reference = StockStatistics.Instance == null ? selected.currentPrice : StockStatistics.Instance.GetStats(selected).prevDayClose;
        decimal delta = selected.currentPrice - reference;
        Set(change, (delta >= 0m ? "+" : "") + Money(delta) + "  (" + (reference > 0m ? (delta / reference * 100m).ToString("+0.0;-0.0;0.0") : "0.0") + "%)");
        change.color = delta >= 0m ? Up : Down;
        graph.color = change.color; graph.SetStock(selected);
        Set(graphRange, "최근 40개 캔들 · 전일 " + Money(reference));
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Order

원본:

```csharp
private void Order()
    {
        var account = session.account;
        decimal owned = account.GetOwnedVolume(selected), average = account.GetOwnedAverage(selected), pnl = (selected.currentPrice - average) * owned;
        Set(position, "<size=30><color=#A6B1BA>평균 매수가</color>  " + Money(average) + "\n<color=#A6B1BA>보유</color>  " + owned.ToString("N0") + "주  <size=26>(매도 가능 " + session.AvailableShares(selected).ToString("N0") + "주)</size>\n<color=#A6B1BA>평가손익</color>  <color=" + (pnl >= 0m ? "#EF5964" : "#4C96E8") + ">" + pnl.ToString("+#,##0;-#,##0;0") + "원</color></size>");
        Set(cash, "주문 가능 현금  " + Money(session.AvailableCash) + "\n미체결 예약금  " + Money(account.ReservedCash) + (session.ScheduledBuyCost > 0m ? "\n분할 제출 예약  " + Money(session.ScheduledBuyCost) : ""));
        Set(feedback, !receipt && !string.IsNullOrEmpty(draftSource) ? draftSource + " · 가격과 수량 확인 후 직접 제출하세요." : session.Feedback);
        pendingOrders.Refresh(); cancel.interactable = pendingOrders.Count > 0;
        Set(status, pendingOrders.Count > 0 ? "대기 " + pendingOrders.Count + "건" : session.Status);
        prepare.interactable = true;
        draftPanel.SetActive(!confirming && !receipt);
        reviewPanel.SetActive(confirming || receipt);
        if (receipt)
        {
            Set(confirmation, session.Latest.Status + "\n" + session.Latest.Stock.stockName + " · #" + session.Number.ToString("D4") + "\n\n" + (session.Buy ? "매수 " : "매도 ") + session.Filled.ToString("N0") + " / " + session.Quantity.ToString("N0") + "주 체결\n호가 대기 " + session.PendingVolume.ToString("N0") + "주\n분할 제출 대기 " + session.Unsubmitted.ToString("N0") + "주\n\n새 주문은 주문 설정으로 돌아가 제출할 수 있습니다.");
        }
        submit.gameObject.SetActive(confirming); back.interactable = true;
        if (confirming || receipt)
        { return; }
        splitPanel.SetActive(splitting);
        quickQuantityPanel.SetActive(!splitting);
        foreach (var button in quickButtons) { button.interactable = true; }
        Set(splitLabel, splitting ? "분할 주문  ON" : "분할 주문  OFF");
        TabletControlStyle.Emphasize(buy,buying); TabletControlStyle.Emphasize(sell,!buying);
        TabletControlStyle.Emphasize(split,splitting);
        string error = Check(out decimal p, out decimal q, out int n, out float seconds);
        Set(validation, error); review.interactable = string.IsNullOrEmpty(error);
        decimal cost = p > 0m && q > 0m && p <= decimal.MaxValue / q ? p * q : 0m;
        Set(amount, cost > 0m ? Money(cost) : "—");
        Set(reviewLabel, splitting ? "분할 주문 확인" : (buying ? "매수 " : "매도 ") + (string.IsNullOrEmpty(error) ? q.ToString("N0") + "주 · " + Money(cost) : "주문"));
        TabletControlStyle.Emphasize(review,true);
        if (!string.IsNullOrEmpty(error))
        { Set(forecast, "전량 체결 시 예상\n입력값을 확인하세요."); return; }
        decimal afterCash = session.AvailableCash + (buying ? -1m : 1m) * p * q;
        Set(forecast, "전량 체결 후  " + (owned + (buying ? q : -q)).ToString("N0") + "주\n주문 가능 현금  " + Money(afterCash));
    }
```

수정본:

```csharp
private void Order()
    {
        tradeView.Account(selected);
        pendingOrders.Refresh(); cancel.interactable = pendingOrders.Count > 0;
        draftPanel.SetActive(!confirming);
        reviewPanel.SetActive(confirming);
        submit.gameObject.SetActive(confirming); back.interactable = true;
        if (confirming)
        { return; }
        splitPanel.SetActive(splitting);
        quickQuantityPanel.SetActive(true);
        foreach (var button in quickButtons) { button.interactable = true; }
        string error = Check(out decimal p, out decimal q, out int n, out float seconds);
        review.interactable = string.IsNullOrEmpty(error);
        decimal cost = p > 0m && q > 0m && p <= decimal.MaxValue / q ? p * q : 0m;
        tradeView.Draft(selected,buying,splitting,error,p,q,cost);
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · Configure

원본:

```csharp
public void Configure(DeviceOrderSession orders, TextMeshProUGUI fontTemplate)
    {
        session = orders; template = fontTemplate;
        title = Label("Pending Order Count",transform,31f,0,0,.68f,.115f);
        var viewport = Rect("Area :: Pending Order Scroll",transform,0,.15f,1,.85f);
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = new Color(1,1,1,.008f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Area :: Pending Order Rows",viewport,0,0,1,1);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45f;
        empty = Label("No Pending Orders",viewport,29f,.02f,.03f,.96f,.25f);
        empty.text = "체결 대기 중인 주문이 없습니다.";
        Refresh();
    }
```

수정본:

```csharp
public void Configure(DeviceOrderSession orders,TextMeshProUGUI fontTemplate,float fontScale=1f)
    {
        session=orders; template=fontTemplate; scale=fontScale; reading=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        title=Label("Pending Heading",transform,30,0,0,128,56,736,400); title.text="미체결";
        count=Label("Pending Order Count",transform,30,136,0,72,56,736,400); TradingOrderView.Numeric(count); count.alignment=TextAlignmentOptions.MidlineRight;
        Label("Pending Company Header",transform,24,0,64,270,32,736,400).text="미체결 종목";
        var price=Label("Pending Price Header",transform,24,286,64,170,32,736,400); price.text="가격 · 원"; price.alignment=TextAlignmentOptions.MidlineRight;
        var volume=Label("Pending Quantity Header",transform,24,472,64,144,32,736,400); volume.text="잔량 / 체결"; volume.alignment=TextAlignmentOptions.MidlineRight;
        var viewport=Rect("Area :: Pending Order Scroll",transform,0,104,736,296,736,400);
        var hit=viewport.gameObject.AddComponent<Image>(); hit.color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Area :: Pending Order Rows",viewport,0,0,1,1,1,1);
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45f;
        empty=Label("No Pending Orders",viewport,28,0,0,1,.35f,1,1); empty.text="—"; empty.alignment=TextAlignmentOptions.Center;
        Refresh();
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · Refresh

원본:

```csharp
public void Refresh()
    {
        session.CopyPendingOrders(entries);
        DeviceTerminalUI.Set(title,"체결 대기  " + entries.Count + "건");
        empty.gameObject.SetActive(entries.Count == 0);
        while (rows.Count < entries.Count) { rows.Add(CreateRow(rows.Count)); }
        if (visibleCount != entries.Count)
        {
            visibleCount = entries.Count;
            content.sizeDelta = new Vector2(0,visibleCount * RowHeight);
            for (int i = 0; i < rows.Count; i++) { rows[i].Root.gameObject.SetActive(i < visibleCount); }
        }
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i]; var row = rows[i]; row.Entry = entry;
            DeviceTerminalUI.Set(row.Name,(entry.Buy ? "매수 · " : "매도 · ") + entry.Stock.stockName);
            row.Name.color = entry.Buy ? new Color32(239,89,100,255) : new Color32(76,150,232,255);
            DeviceTerminalUI.Set(row.Detail,DeviceTerminalUI.Money(entry.Price) + " · 대기 " + entry.Remaining.ToString("N0") + "주");
            string state = entry.Request == null ? "자동·기존 주문" : "#" + entry.Request.Number.ToString("D4") + " · 체결 " + entry.Request.Filled.ToString("N0") + "주";
            if (entry.Request != null && entry.Request.Unsubmitted > 0m)
            { state += " · 분할 예약 " + entry.Request.Unsubmitted.ToString("N0") + "주"; }
            DeviceTerminalUI.Set(row.State,state);
        }
    }
```

수정본:

```csharp
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
            DeviceTerminalUI.Set(row.Side,entry.Buy?"매수":"매도"); row.Side.color=entry.Buy?new Color32(239,89,100,255):new Color32(76,150,232,255);
            DeviceTerminalUI.Set(row.Name,entry.Stock.stockName);
            DeviceTerminalUI.Set(row.Symbol,entry.Stock.symbol+(entry.Request!=null && entry.Request.Unsubmitted>0 ? " · 분할" : ""));
            DeviceTerminalUI.Set(row.Price,TradingOrderView.Number(entry.Price));
            DeviceTerminalUI.Set(row.Remaining,TradingOrderView.Number(entry.Remaining));
            DeviceTerminalUI.Set(row.Filled,entry.Request==null?"—":TradingOrderView.Number(entry.Request.Filled));
        }
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · CancelAll

원본:

```csharp
public void CancelAll()
    {
        session.CopyPendingOrders(entries);
        foreach (var entry in entries) { session.CancelPending(entry); }
        Refresh();
    }
```

수정본:

```csharp
public void CancelAll()
    {
        session.CopyPendingOrders(entries);
        foreach(var entry in entries) { session.CancelPending(entry); }
        Refresh();
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · CreateRow

원본:

```csharp
private Row CreateRow(int index)
    {
        var row = new Row { Root = Rect("Panel :: Pending Order " + index,content,0,0,1,1) };
        row.Root.anchorMin = new Vector2(0,1); row.Root.anchorMax = Vector2.one;
        row.Root.pivot = new Vector2(.5f,1); row.Root.sizeDelta = new Vector2(0,RowHeight);
        row.Root.anchoredPosition = new Vector2(0,-index * RowHeight);
        row.Name = Label("Pending Stock",row.Root,29f,.015f,.035f,.78f,.29f);
        row.Detail = Label("Pending Price Quantity",row.Root,27f,.015f,.34f,.78f,.29f);
        row.State = Label("Pending Fill State",row.Root,23f,.015f,.66f,.78f,.25f);
        row.State.color = new Color32(166,177,186,255);
        var divider = Rect("Image :: Pending Divider",row.Root,0,.985f,1,.008f).gameObject.AddComponent<Image>();
        divider.color = new Color32(52,67,75,180); divider.raycastTarget = false;
        var buttonRoot = Rect("Button :: Cancel Pending Order",row.Root,.82f,.17f,.18f,.61f);
        var image = buttonRoot.gameObject.AddComponent<Image>(); image.color = new Color32(31,44,50,255);
        var button = buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label("Cancel Pending Order",buttonRoot,27f,0,0,1,1);
        label.alignment = TextAlignmentOptions.Center; label.text = "취소";
        TabletControlStyle.Button(button);
        button.onClick.AddListener(() => { session.CancelPending(row.Entry); Refresh(); });
        return row;
    }
```

수정본:

```csharp
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
        var label=Label("Cancel Pending Order",buttonRoot,26,0,0,1,1,1,1); label.alignment=TextAlignmentOptions.Center; label.text="취소";
        TabletControlStyle.Button(button); label.fontStyle=FontStyles.Normal;
        button.onClick.AddListener(()=>{session.CancelPending(row.Entry); Refresh();}); return row;
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · Rect

원본:

```csharp
private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.gameObject.layer = gameObject.layer;
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
```

수정본:

```csharp
private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h,float pw,float ph)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); TradingOrderView.Place(rect,parent,x,y,w,h,pw,ph); return rect;
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · Label

원본:

```csharp
private TextMeshProUGUI Label(string name, Transform parent, float size, float x, float y, float width, float height)
    {
        var label = Instantiate(template,parent); label.name = "Text :: " + name;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.fontSize = size; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        label.color = new Color32(230,235,239,255); label.text = "";
        return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(string name,Transform parent,float size,float x,float y,float w,float h,float pw,float ph)
    {
        var label=Instantiate(template,parent); label.name="Text :: "+name;
        if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
        TradingOrderView.Place(label.rectTransform,parent,x,y,w,h,pw,ph);
        label.fontSize=size*scale; label.enableAutoSizing=false; label.fontStyle=FontStyles.Normal; label.margin=Vector4.zero;
        label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false; label.color=new Color32(230,235,239,255); label.text=""; return label;
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · Number

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private TextMeshProUGUI Number(string name,Transform parent,float x,float y,float w,float h)
    {
        var label=Label(name,parent,26,x,y,w,h,736,88); TradingOrderView.Numeric(label); label.alignment=TextAlignmentOptions.MidlineRight; return label;
    }
```

### Assets/Scripts/NightShift/TabletControlStyle.cs · Apply

원본:

```csharp
public static void Apply(TabletAccountUI tablet)
    {
        var tabs = new HashSet<Button>(tablet.tabs);
        foreach (var terminal in new[] { tablet.orders,tablet.events })
        {
            tabs.Add(terminal.mainTab); tabs.Add(terminal.legacy.deskTab); tabs.Add(terminal.legacy.legacyTab);
            if (terminal.eventButtons != null) { foreach (var row in terminal.eventButtons) { tabs.Add(row); } }
        }
        foreach (var label in tablet.contentRoot.GetComponentsInChildren<TMP_Text>(true)) { Font(label); }
        foreach (var button in tablet.contentRoot.GetComponentsInChildren<Button>(true))
        {
            if (tabs.Contains(button) || button.GetComponent<InvestmentPositionRow>() != null || button.GetComponentInParent<NewsDeskUI>(true) != null) { continue; }
            Button(button);
        }
        foreach (var field in tablet.contentRoot.GetComponentsInChildren<TMP_InputField>(true)) { Input(field); }
        foreach (var button in new[] { tablet.orderButton,tablet.transferSubmit,tablet.orders.review,tablet.orders.submit,tablet.events.openRooms })
        { Button(button,true); }
    }
```

수정본:

```csharp
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
```

### Assets/Scripts/NightShift/TabletAccountUI.cs · ShowPage

원본:

```csharp
public void ShowPage(Page page)
    {
        if (Locked)
        { return; }
        if (page == Page.Automation && !second)
        { return; }
        if (page == Page.Detail && (dashboard.Selected == null || account.GetOwnedVolume(dashboard.Selected) <= 0m))
        { page = Page.Dashboard; }
        foreach (var screen in screens) { screen.SetActive(false); }
        transferPanel.SetActive(false);
        CurrentPage = page;
        if (page == Page.Dashboard) { dashboard.gameObject.SetActive(true); dashboard.Refresh(); }
        else if (page == Page.Events) { events.ShowLocal(); }
        else if (page == Page.Orders) { orders.ShowLocal(); }
        else if (page == Page.Detail)
        { holdings.selectedOverride = dashboard.Selected; holdings.gameObject.SetActive(true); }
        else if (page == Page.Records) { recordsPanel.SetActive(true); seenVersion = -1; }
        else if (page == Page.Automation) { orders.legacy.ShowDesk(); }
        else if (page == Page.Management) { events.legacy.ShowDesk(); }
        for (int i = 0; i < tabs.Length; i++)
        { tabs[i].image.color = (int)page == i ? new Color32(26,43,49,255) : Color.clear; tabLines[i].enabled = (int)page == i; }
        next = 0f;
    }
```

수정본:

```csharp
public void ShowPage(Page page)
    {
        if (Locked)
        { return; }
        if (page == Page.Automation && !second)
        { return; }
        if (page == Page.Detail && (dashboard.Selected == null || account.GetOwnedVolume(dashboard.Selected) <= 0m))
        { page = Page.Dashboard; }
        foreach (var screen in screens) { screen.SetActive(false); }
        transferPanel.SetActive(false);
        CurrentPage = page;
        clock.gameObject.SetActive(page != Page.Orders);
        if (page == Page.Dashboard) { dashboard.gameObject.SetActive(true); dashboard.Refresh(); }
        else if (page == Page.Events) { events.ShowLocal(); }
        else if (page == Page.Orders) { orders.ShowLocal(); }
        else if (page == Page.Detail)
        { holdings.selectedOverride = dashboard.Selected; holdings.gameObject.SetActive(true); }
        else if (page == Page.Records) { recordsPanel.SetActive(true); seenVersion = -1; }
        else if (page == Page.Automation) { orders.legacy.ShowDesk(); }
        else if (page == Page.Management) { events.legacy.ShowDesk(); }
        for (int i = 0; i < tabs.Length; i++)
        { tabs[i].image.color = (int)page == i ? new Color32(26,43,49,255) : Color.clear; tabLines[i].enabled = (int)page == i; }
        next = 0f;
    }
```

### Assets/Scripts/DeskControlHints.cs · Start

원본:

```csharp
void Start()
    {
        if (views == null)
        { views = DeskViewController.Instance; }
        if (views == null)
        { enabled = false; return; }
        mouseLook = views.GetComponent<MouseCameraController>();
        if (sdfFont == null)
        { sdfFont = Resources.Load<TMP_FontAsset>("Chat/Chat Korean"); }
        if (keycap == null)
        { keycap = Resources.Load<Sprite>("UI/TradingTerminal/Keycap_Background"); }
        hints = Rect("Area :: Shortcut Hints", transform, new Vector2(18f, 16f), new Vector2(600f, 70f));
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = Rect("Area :: Shortcut " + i, hints, new Vector2(i % 3 * 200f, (1 - i / 3) * 36f), new Vector2(194f, 32f));
            var cap = Rect("Image :: Keycap", cells[i], Vector2.zero, new Vector2(70f, 32f));
            var image = cap.gameObject.AddComponent<Image>(); image.sprite = keycap; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 32f; image.fillCenter = true; image.color = new Color(1f, 1f, 1f, .82f); image.raycastTarget = false;
            keys[i] = Text("Text :: Key", cap, Vector2.zero, new Vector2(70f, 32f), 17f, new Color32(230, 235, 239, 245));
            keys[i].alignment = TextAlignmentOptions.Center;
            captions[i] = Text("Text :: Action", cells[i], new Vector2(78f, 0f), new Vector2(116f, 32f), 15f, new Color32(174, 187, 195, 235));
            captions[i].alignment = TextAlignmentOptions.MidlineLeft;
        }
        markerRect = Rect("Image :: Device Interaction Circles", transform, Vector2.zero, Vector2.zero);
        markerRect.anchorMax = Vector2.one; markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
        markers = markerRect.gameObject.AddComponent<DeskInteractionMarkers>();
        hints.gameObject.SetActive(false);
    }
```

수정본:

```csharp
void Start()
    {
        if (views == null)
        { views = DeskViewController.Instance; }
        if (views == null)
        { enabled = false; return; }
        mouseLook = views.GetComponent<MouseCameraController>();
        tabletNavigation = FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include);
        if (sdfFont == null)
        { sdfFont = Resources.Load<TMP_FontAsset>("Chat/Chat Korean"); }
        if (keycap == null)
        { keycap = Resources.Load<Sprite>("UI/TradingTerminal/Keycap_Background"); }
        hints = Rect("Area :: Shortcut Hints", transform, new Vector2(18f, 16f), new Vector2(600f, 70f));
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = Rect("Area :: Shortcut " + i, hints, new Vector2(i % 3 * 200f, (1 - i / 3) * 36f), new Vector2(194f, 32f));
            var cap = Rect("Image :: Keycap", cells[i], Vector2.zero, new Vector2(70f, 32f));
            var image = cap.gameObject.AddComponent<Image>(); image.sprite = keycap; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 32f; image.fillCenter = true; image.color = new Color(1f, 1f, 1f, .82f); image.raycastTarget = false;
            keys[i] = Text("Text :: Key", cap, Vector2.zero, new Vector2(70f, 32f), 17f, new Color32(230, 235, 239, 245));
            keys[i].alignment = TextAlignmentOptions.Center;
            captions[i] = Text("Text :: Action", cells[i], new Vector2(78f, 0f), new Vector2(116f, 32f), 15f, new Color32(174, 187, 195, 235));
            captions[i].alignment = TextAlignmentOptions.MidlineLeft;
        }
        markerRect = Rect("Image :: Device Interaction Circles", transform, Vector2.zero, Vector2.zero);
        markerRect.anchorMax = Vector2.one; markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
        markers = markerRect.gameObject.AddComponent<DeskInteractionMarkers>();
        hints.gameObject.SetActive(false);
    }
```

### Assets/Scripts/DeskControlHints.cs · LateUpdate

원본:

```csharp
void LateUpdate()
    {
        if (views == null || hints == null)
        { return; }
        bool shown = views.Ready && !MouseCameraController.TextEntryActive;
        if (shown != wasVisible)
        { hints.gameObject.SetActive(shown); wasVisible = shown; }
        bool phone = PhoneCallController.IsRaised;
        bool paused = mouseLook != null && mouseLook.IsPaused;
        int next = ((int)views.Target + 1) * 4 + (phone ? 2 : 0) + (paused ? 1 : 0);
        if (shown && next != state)
        { state = next; RefreshLabels(phone, paused); }
        bool desk = shown && !views.IsFocused && !views.IsTransitioning && !phone;
        for (int i = 0; i < 5; i++)
        {
            RectTransform screen = views.ScreenFor((DeskViewTarget)i);
            Vector2 local = Vector2.zero; bool visible = false;
            if (desk && screen != null && screen.gameObject.activeInHierarchy)
            {
                Vector3 center = screen.TransformPoint(screen.rect.center);
                Vector3 point = views.ViewCamera.WorldToScreenPoint(center);
                visible = point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point) && Vector3.Dot(screen.forward, center - views.ViewCamera.transform.position) > 0f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
            }
            markers.SetMarker(i, local, visible, desk && (int)views.Hovered == i);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, MouseCameraController.GetAimScreenPosition(), null, out Vector2 aim);
        markers.SetAim(aim, desk && MouseCameraController.IsCursorLocked && views.Hovered == DeskViewTarget.None);
    }
```

수정본:

```csharp
void LateUpdate()
    {
        if (views == null || hints == null)
        { return; }
        bool shown = views.Ready && !MouseCameraController.TextEntryActive && !ViewingOrderForm();
        if (shown != wasVisible)
        { hints.gameObject.SetActive(shown); wasVisible = shown; }
        bool phone = PhoneCallController.IsRaised;
        bool paused = mouseLook != null && mouseLook.IsPaused;
        int next = ((int)views.Target + 1) * 4 + (phone ? 2 : 0) + (paused ? 1 : 0);
        if (shown && next != state)
        { state = next; RefreshLabels(phone, paused); }
        bool desk = shown && !views.IsFocused && !views.IsTransitioning && !phone;
        for (int i = 0; i < 5; i++)
        {
            RectTransform screen = views.ScreenFor((DeskViewTarget)i);
            Vector2 local = Vector2.zero; bool visible = false;
            if (desk && screen != null && screen.gameObject.activeInHierarchy)
            {
                Vector3 center = screen.TransformPoint(screen.rect.center);
                Vector3 point = views.ViewCamera.WorldToScreenPoint(center);
                visible = point.z > views.ViewCamera.nearClipPlane && views.ViewCamera.pixelRect.Contains(point) && Vector3.Dot(screen.forward, center - views.ViewCamera.transform.position) > 0f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, point, null, out local);
            }
            markers.SetMarker(i, local, visible, desk && (int)views.Hovered == i);
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRect, MouseCameraController.GetAimScreenPosition(), null, out Vector2 aim);
        markers.SetAim(aim, desk && MouseCameraController.IsCursorLocked && views.Hovered == DeskViewTarget.None);
    }
```

### Assets/Scripts/DeskControlHints.cs · ViewingOrderForm

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private bool ViewingOrderForm()
    {
        if (PhoneCallController.IsRaised || views.Target < DeskViewTarget.TabletOne || tabletNavigation == null)
        { return false; }
        bool second = views.Target == DeskViewTarget.TabletTwo;
        foreach (var tablet in tabletNavigation)
        {
            if (tablet != null && tablet.second == second && !tablet.Locked && tablet.CurrentPage == TabletAccountUI.Page.Orders)
            { return true; }
        }
        return false;
    }
```
