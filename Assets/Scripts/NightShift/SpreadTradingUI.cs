using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>두 번째 태블릿의 기존 Canvas와 패널에 스프레드 제어를 표시한다.</summary>
public sealed class SpreadTradingUI : MonoBehaviour
{
    private TabletAccountUI tablet;
    private TradingOperationController operation;
    private TMP_FontAsset font;
    private float scale, next;
    private TextMeshProUGUI stockName, currentPrice, state, range, bid, ask, cash, shares, buyRemaining, sellRemaining, bought, sold, realized;
    private Button start, cancel, previous, following;
    private TextMeshProUGUI elapsed, updated, tape;
    private Slider slider;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
    private static readonly Color Line = new Color32(44,60,69,255), Accent = new Color32(37,175,192,255);

    public static SpreadTradingUI Install(TabletAccountUI tablet)
    {
        if (!tablet.second || tablet.orders == null || tablet.orders.legacy == null)
        { return null; }
        var legacy = tablet.orders.legacy;
        var root = (RectTransform)legacy.deskPanel.transform;
        var model = (RectTransform)tablet.orders.panel.transform;
        root.anchorMin = model.anchorMin; root.anchorMax = model.anchorMax; root.pivot = model.pivot;
        root.offsetMin = model.offsetMin; root.offsetMax = model.offsetMax;
        foreach (Transform child in root)
        { child.gameObject.SetActive(false); }
        var view = root.gameObject.AddComponent<SpreadTradingUI>();
        legacy.spreadView = view;
        view.tablet = tablet; view.operation = TradingOperationController.Instance;
        if (view.operation != null)
        { view.operation.account = tablet.account; view.operation.spreadSession = tablet.orders.session; }
        view.Build();
        // 같은 자리에 매집 패널을 겹쳐 두고 위쪽 탭으로 오간다.
        AccumulationUI.Install(view, view.operation, view.scale);
        var label = tablet.autoButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { GameLocalizedText.Set(label,"스프레드"); }
        return view;
    }
    private void Build()
    {
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        scale = ((RectTransform)transform).rect.width / 1600f;
        if (scale <= 0f) { scale = 1f; }
        var background = GetComponent<Image>();
        if (background != null)
        { background.sprite = null; background.type = Image.Type.Simple; background.color = new Color32(13,23,29,255); background.raycastTarget = false; }
        Label("Title","스프레드",32,8,512,56,40);
        state = Label("State","",960,8,608,56,28); state.color = TabletKitStyle.Accent;
        stockName = Label("Stock","",88,88,720,64,38);
        previous = Action("Previous Stock","‹",32,88,48,() => Select(-1));
        following = Action("Next Stock","›",832,88,48,() => Select(1));
        currentPrice = Number("Current Price",32,168,800,56,32);
        Label("Current Price Unit","원",848,168,32,56,25).color=Muted;
        Label("Range Label","스프레드 범위",32,264,480,48,29);
        range = Label("Range","",544,264,336,48,30); range.alignment=TextAlignmentOptions.MidlineRight;
        BuildSlider();
        Label("Bid Label","예상 매수가",32,432,400,40,27).color=Muted;
        Label("Ask Label","예상 매도가",480,432,400,40,27).color=Muted;
        bid = Number("Bid",32,480,400,56,40); ask = Number("Ask",480,480,400,56,40);
        cash = Metric("주문 가능 현금",32,584,848,"원");
        shares = Metric("매도 가능 수량",32,640,848,"주");
        elapsed = Label("Elapsed","",32,720,848,40,27);
        start = Action("Start Spread","매매 시작",32,792,408,StartTrading,true);
        cancel = Action("Cancel Spread","매매 취소",472,792,408,CancelTrading);
        var divider=Graphic("Auto Divider",transform,TabletKitStyle.Line);
        TradingOrderView.Place(divider.rectTransform,transform,920,88,1,752,1600,880);
        Label("Pending Title","미체결",960,88,608,56,34);
        buyRemaining=Metric("매수 잔량",960,168,608,"주");
        sellRemaining=Metric("매도 잔량",960,224,608,"주");
        bought=Metric("매수 체결",960,304,608,"주");
        sold=Metric("매도 체결",960,360,608,"주");
        realized=Metric("실현손익",960,416,608,"원");
        Label("Recent Fills","최근 체결",960,504,608,48,34);
        tape=Label("Fill Tape","—",960,568,608,176,27); tape.textWrappingMode=TextWrappingModes.Normal;
        updated=Label("Updated","",960,784,608,56,25); updated.color=Muted;
    }
    private void Select(int delta)
    {
        var manager=StockManager.Instance;
        if (operation.Active || manager==null || manager.stocks.Count==0)
        { return; }
        int index=manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index+delta+manager.stocks.Count)%manager.stocks.Count); next=0;
    }

    private void BuildSlider()
    {
        var root = new GameObject("Area :: Spread Range",typeof(RectTransform),typeof(Image),typeof(Slider));
        var rect = (RectTransform)root.transform;
        TradingOrderView.Place(rect,transform,48,328,816,56,1600,880);
        var hit = root.GetComponent<Image>(); hit.color = Color.clear;
        slider = root.GetComponent<Slider>(); slider.minValue = 1; slider.maxValue = TradingOperationController.MaxSpreadTicks; slider.wholeNumbers = true;
        var track = Graphic("Range Track",root.transform,Line); Stretch(track.rectTransform,new Vector2(0,.42f),new Vector2(1,.58f));
        var fillArea = SliderArea("Fill",root.transform,.42f,.58f);
        var handleArea = SliderArea("Handle",root.transform,.2f,.8f);
        var fill = Graphic("Range Fill",fillArea,Accent); Stretch(fill.rectTransform,Vector2.zero,Vector2.one);
        var handle = Graphic("Range Handle",handleArea,Ink); handle.rectTransform.sizeDelta = new Vector2(18*scale,0);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(operation == null ? 2 : operation.SpreadTicks);
        slider.onValueChanged.AddListener(value => { operation?.SetSpreadTicks(Mathf.RoundToInt(value)); next = 0f; });
        Label("Minimum Range","1 틱",32,392,160,32,24).color = Muted;
        var max = Label("Maximum Range",TradingOperationController.MaxSpreadTicks+" 틱",720,392,160,32,24); max.color = Muted; max.alignment = TextAlignmentOptions.MidlineRight;
    }
    public void FitCanvas()
    {
        var rect=(RectTransform)transform;
        float fitted=Mathf.Min(rect.rect.width/1600f,rect.rect.height/880f);
        foreach (var text in GetComponentsInChildren<TMP_Text>(true)) { text.fontSize*=fitted/scale; }
        scale=fitted;
    }
    private static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private RectTransform SliderArea(string name,Transform parent,float bottom,float top)
    {
        var obj = new GameObject("Area :: Spread " + name,typeof(RectTransform)); obj.transform.SetParent(parent,false); obj.layer = gameObject.layer;
        var rect = (RectTransform)obj.transform; Stretch(rect,new Vector2(0,bottom),new Vector2(1,top)); return rect;
    }
    private Image Graphic(string name,Transform parent,Color color)
    {
        var obj = new GameObject("Image :: " + name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false); obj.layer = gameObject.layer;
        var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private void Rule(float y)
    { var line = Graphic("Spread Divider",transform,Line); TradingOrderView.Place(line.rectTransform,transform,32,y,1536,1,1600,880); }
    private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
    {
        var obj = new GameObject("Text :: " + name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.layer = gameObject.layer;
        var label = obj.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform,transform,x,y,w,h,1600,880);
        label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.color = Ink; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft; GameLocalizedText.Set(label,value); return label;
    }
    private TextMeshProUGUI Number(string name,float x,float y,float w,float h,float size)
    {
        var label = Label(name,"",x,y,w,h,size); TradingOrderView.Numeric(label); label.alignment = TextAlignmentOptions.MidlineRight; return label;
    }
    private TextMeshProUGUI Metric(string caption,float x,float y,float w,string unit)
    {
        Label(caption+" Label",caption,x,y,288,40,27).color = Muted;
        var value = Number(caption+" Value",x+296,y,w-344,40,30);
        Label(caption+" Unit",unit,x+w-32,y,32,40,25).color = Muted; return value;
    }
    private Button Action(string name,string caption,float x,float y,float w,UnityEngine.Events.UnityAction action,bool primary=false)
    {
        var obj = new GameObject("Button :: " + name,typeof(RectTransform),typeof(Image),typeof(Button)); obj.layer = gameObject.layer;
        TradingOrderView.Place((RectTransform)obj.transform,transform,x,y,w,56,1600,880);
        var label = Label(name,caption,x,y,w,56,28); label.transform.SetParent(obj.transform,false);
        Stretch(label.rectTransform,Vector2.zero,Vector2.one); label.alignment = TextAlignmentOptions.Center;
        var button = obj.GetComponent<Button>(); button.targetGraphic = obj.GetComponent<Image>(); TabletControlStyle.Button(button,primary);
        label.font = font; label.fontSize = 28*scale; label.fontStyle = FontStyles.Normal;
        button.onClick.AddListener(action); return button;
    }
    private void StartTrading()
    { operation?.StartSpread(StockManager.Instance == null ? null : StockManager.Instance.CurrentStock); next = 0f; }
    private void CancelTrading()
    { operation?.CancelSpread(); next = 0f; }
    private void OnEnable() { next = 0f; }
    private void Update()
    {
        if (operation == null || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .25f;
        var stock = operation.SpreadRunning ? operation.Target : StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        DeviceTerminalUI.Set(stockName,stock == null ? "—" : stock.stockName);
        DeviceTerminalUI.Set(currentPrice,stock == null ? "—" : TradingOrderView.Number(stock.currentPrice));
        // 매집이 돌고 있거나 방금 끝났으면 그쪽 상태는 매집 화면에서 보여 준다. 여기에는 적지 않는다.
        bool accumulating = operation.AccumulateRunning || operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed && operation.CurrentMode == TradingOperationController.Mode.Review;
        DeviceTerminalUI.Set(state,operation.SpreadRunning ? operation.Feedback : operation.CurrentMode == TradingOperationController.Mode.Standby || accumulating ? "" : operation.Feedback);
        bool prices = TradingOperationController.SpreadPrices(stock == null ? 0m : stock.currentPrice,operation.SpreadTicks,out decimal buyPrice,out decimal sellPrice);
        DeviceTerminalUI.Set(range,"± " + operation.SpreadTicks + " 틱");
        DeviceTerminalUI.Set(bid,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadBid : buyPrice) : "—");
        DeviceTerminalUI.Set(ask,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadAsk : sellPrice) : "—");
        DeviceTerminalUI.Set(cash,TradingOrderView.Number(operation.SpreadAvailableCash));
        DeviceTerminalUI.Set(shares,TradingOrderView.Number(operation.SpreadAvailableShares(stock)));
        DeviceTerminalUI.Set(buyRemaining,TradingOrderView.Number(operation.SpreadBuyRemaining));
        DeviceTerminalUI.Set(sellRemaining,TradingOrderView.Number(operation.SpreadSellRemaining));
        DeviceTerminalUI.Set(bought,TradingOrderView.Number(operation.Bought)); DeviceTerminalUI.Set(sold,TradingOrderView.Number(operation.Sold));
        DeviceTerminalUI.Set(realized,InvestmentDashboard.Signed(operation.SpreadRealized));
        realized.color = operation.SpreadRealized > 0m ? MarketColors.Up : operation.SpreadRealized < 0m ? MarketColors.Down : Ink;
        previous.interactable=following.interactable=!operation.Active;
        DeviceTerminalUI.Set(elapsed,operation.SpreadStartedGameMinute>0 ? "실행 경과 · "+DeviceMarketJournal.Duration(operation.SpreadElapsedGameMinutes) : "");
        DeviceTerminalUI.Set(updated,operation.SpreadUpdatedStamp==null ? "" : "갱신 · "+operation.SpreadUpdatedStamp);
        var buffer=new System.Text.StringBuilder();
        int shown=0;
        foreach (var fill in operation.SpreadFills)
        {
            if (++shown>3)
            { break; }
            buffer.Append(fill.Time).Append("  ").Append(fill.Buy ? "매수" : "매도").AppendLine();
            buffer.Append(TradingOrderView.Number(fill.Price)).Append("원 · ").Append(TradingOrderView.Number(fill.Quantity)).Append("주").AppendLine();
        }
        DeviceTerminalUI.Set(tape,buffer.Length>0 ? buffer.ToString().TrimEnd() : "—");
        start.interactable = !operation.Active && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
        cancel.interactable = operation.SpreadRunning;
        TabletControlStyle.Emphasize(start,!operation.SpreadRunning);
        TabletControlStyle.Emphasize(cancel,operation.SpreadRunning);
    }
}
