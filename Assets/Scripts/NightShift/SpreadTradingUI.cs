using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 두 번째 태블릿의 기존 Canvas와 패널에 스프레드 제어를 표시한다.
/// 화면은 언제나 지금 고른 종목을 따라간다. 스프레드는 여러 종목에서 한꺼번에 돌릴 수 있어서,
/// 돌고 있는 동안에도 종목을 넘길 수 있고 오른쪽 아래 "진행 중" 목록에서 돌고 있는 것을 모두 본다.
/// </summary>
public sealed class SpreadTradingUI : MonoBehaviour
{
    private TabletAccountUI tablet;
    /// <summary>지금 보는 종목을 맡은 자동 매매 장치. 종목마다 다르므로 붙들어 두지 않고 쓸 때마다 다시 읽는다.</summary>
    private static TradingOperationController Operation => TradingOperationController.Instance;
    private static Stock Current => StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
    private TMP_FontAsset font;
    private float scale, next;
    private TextMeshProUGUI stockName, currentPrice, state, range, bid, ask, cash, shares, buyRemaining, sellRemaining, bought, sold, realized, marked, markedCaption;
    private Button start, cancel;
    private TextMeshProUGUI elapsed, updated, tape;
    private Slider slider;
    private OperationRunList runList;
    /// <summary>새로 시작할 스프레드의 범위(틱). 돌고 있는 종목을 볼 때는 그 실행의 범위를 보여 주고, 이 값은 그대로 둔다.</summary>
    private int ticks = 2;
    private int locale = -1;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255);
    private static readonly Color Line = new Color32(44,60,69,255), Accent = new Color32(37,175,192,255);
    private static string Word(string korean,string english,string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

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
        view.tablet = tablet;
        // 종목마다 하나씩 도는 모든 장치가 이 태블릿의 계좌와 주문 창구를 쓴다.
        TradingOperationController.Configure(tablet.account,tablet.orders.session);
        view.Build();
        // 같은 자리에 매집 패널을 겹쳐 두고 위쪽 탭으로 오간다.
        AccumulationUI.Install(view, Operation, view.scale);
        ManipulationUI.Install(view.gameObject, view.scale);
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
        Action("Previous Stock","‹",32,88,48,() => Select(-1));
        Action("Next Stock","›",832,88,48,() => Select(1));
        currentPrice = Number("Current Price",32,168,800,56,32);
        Label("Current Price Unit","돈",848,168,32,56,25).color=Muted;
        Label("Range Label","스프레드 범위",32,264,480,48,29);
        range = Label("Range","",544,264,336,48,30); range.alignment=TextAlignmentOptions.MidlineRight;
        BuildSlider();
        Label("Bid Label","예상 매수가",32,432,400,40,27).color=Muted;
        Label("Ask Label","예상 매도가",480,432,400,40,27).color=Muted;
        bid = Number("Bid",32,480,400,56,40); ask = Number("Ask",480,480,400,56,40);
        cash = Metric("주문 가능 현금",32,584,848,"돈");
        shares = Metric("매도 가능 수량",32,640,848,"주");
        elapsed = Label("Elapsed","",32,720,848,40,27);
        start = Action("Start Spread","매매 시작",32,792,408,StartTrading,true);
        cancel = Action("Cancel Spread","매매 취소",472,792,408,CancelTrading);
        var divider=Graphic("Auto Divider",transform,TabletKitStyle.Line);
        TradingOrderView.Place(divider.rectTransform,transform,920,88,1,752,1600,880);
        // 오른쪽: 지금 보는 종목의 스프레드가 한 일(잔량·체결·번 돈). 그 아래에 모든 종목의 진행 중 목록을 둔다.
        Label("Pending Title","미체결",960,88,608,56,34);
        buyRemaining=Metric("매수 잔량",960,152,608,"주");
        sellRemaining=Metric("매도 잔량",960,198,608,"주");
        bought=Metric("매수 체결",960,244,608,"주");
        sold=Metric("매도 체결",960,290,608,"주");
        realized=Metric("실현손익",960,336,608,"돈");
        // 실현손익은 사고판 차익만이다. 들고 남은 주식의 평가까지 넣은 손익을 한 줄 더 보여 준다.
        markedCaption=Label("Marked Label","",960,382,288,40,27); markedCaption.color=Muted;
        marked=Number("Marked Value",1256,382,264,40,30);
        Label("Marked Unit","돈",1536,382,32,40,25).color=Muted;
        Label("Recent Fills","최근 체결",960,440,608,44,34);
        tape=Label("Fill Tape","—",960,488,608,108,27); tape.textWrappingMode=TextWrappingModes.Normal; tape.alignment=TextAlignmentOptions.TopLeft;
        runList=new OperationRunList(transform,font,scale,960,612,608,Jump);
        updated=Label("Updated","",960,792,608,40,25); updated.color=Muted;
    }
    // 무언가 돌고 있어도 종목은 언제나 넘길 수 있다. 다른 종목에서 또 시작할 수 있어야 한다.
    private void Select(int delta)
    {
        var manager=StockManager.Instance;
        if (manager==null || manager.stocks.Count==0)
        { return; }
        int index=manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index+delta+manager.stocks.Count)%manager.stocks.Count); next=0;
    }
    /// <summary>진행 중 목록의 줄을 누르면 그 종목으로 간다. 매집이면 매집 화면을 연다.</summary>
    private void Jump(TradingOperationController run)
    {
        var manager=StockManager.Instance;
        if (run==null || run.Target==null || manager==null)
        { return; }
        int index=manager.stocks.IndexOf(run.Target);
        if (index>=0) { manager.SelectStock(index); }
        if (run.CurrentMode==TradingOperationController.Mode.Accumulate)
        {
            var other=GetComponent<AccumulationUI>();
            if (other!=null) { other.ShowMode(true); }
        }
        next=0;
    }
    /// <summary>이 장치가 지금 보는 종목에서 스프레드를 돌리고 있는가.</summary>
    private static bool RunningHere(TradingOperationController operation)
    { return operation != null && operation.SpreadRunning && operation.Target == Current; }

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
        var initial = Operation;
        if (initial != null) { ticks = initial.SpreadTicks; }
        slider.SetValueWithoutNotify(ticks);
        slider.onValueChanged.AddListener(value =>
        {
            // 돌고 있는 종목을 보고 있으면 그 실행의 범위를 바꾼다. 아니면 다음에 시작할 범위를 바꾼다.
            int wanted = Mathf.RoundToInt(value); var operation = Operation;
            if (RunningHere(operation)) { operation.SetSpreadTicks(wanted); }
            else { ticks = wanted; if (operation != null && !operation.Active) { operation.SetSpreadTicks(wanted); } }
            next = 0f;
        });
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
    {
        var operation = Operation;
        if (operation != null)
        {
            if (!operation.Active) { operation.SetSpreadTicks(ticks); }
            operation.StartSpread(Current);
        }
        next = 0f;
    }
    // 지금 보는 종목의 스프레드만 멈춘다. 다른 종목의 것은 진행 중 목록의 ×로 멈춘다.
    private void CancelTrading()
    {
        var operation = Operation;
        if (RunningHere(operation)) { operation.CancelSpread(); }
        next = 0f;
    }
    private static void Lit(TextMeshProUGUI label,string text)
    {
        if (label != null && !GameLocalizedText.Matches(label,text)) { GameLocalizedText.Set(label,text,true); }
    }
    private void OnEnable() { next = 0f; }
    private void Update()
    {
        var operation = Operation;
        if (operation == null || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .25f;
        if (locale != GameLocale.Revision)
        { locale = GameLocale.Revision; Lit(markedCaption,Word("평가 포함 손익","P&L incl. holdings","評価込み損益")); }
        // 화면은 언제나 지금 고른 종목을 따라간다. here: 이 장치가 그 종목의 것인가(자리가 모두 찼으면 다른 종목의 장치가 온다).
        var stock = Current;
        bool here = stock != null && operation.Target == stock, running = here && operation.SpreadRunning, busy = operation.Active;
        DeviceTerminalUI.Set(stockName,stock == null ? "—" : stock.stockName);
        DeviceTerminalUI.Set(currentPrice,stock == null ? "—" : TradingOrderView.Number(stock.currentPrice));
        // 매집이 돌고 있거나 방금 끝났으면 그쪽 상태는 매집 화면에서 보여 준다. 여기에는 적지 않는다.
        bool accumulating = here && (operation.AccumulateRunning || operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed && operation.CurrentMode == TradingOperationController.Mode.Review);
        // 이 종목에서 지금 돌고 있는(또는 방금 끝난) 스프레드의 숫자만 보여 준다. 다른 종목이나 매집의 숫자가 섞이지 않게 한다.
        bool mine = here && !accumulating && (running || operation.CurrentMode == TradingOperationController.Mode.Review);
        DeviceTerminalUI.Set(state,running ? operation.Feedback : busy && !here ? Word("자동 매매 자리가 모두 사용 중","All automation slots are in use","自動売買の枠がすべて使用中")
            : !here || operation.CurrentMode == TradingOperationController.Mode.Standby || accumulating ? "" : operation.Feedback);
        // 범위: 돌고 있으면 그 실행의 범위, 아니면 다음에 시작할 범위.
        int shownTicks = running ? operation.SpreadTicks : ticks;
        if (Mathf.RoundToInt(slider.value) != shownTicks) { slider.SetValueWithoutNotify(shownTicks); }
        if (!busy && operation.SpreadTicks != ticks) { operation.SetSpreadTicks(ticks); }
        bool prices = TradingOperationController.SpreadPrices(stock == null ? 0m : stock.currentPrice,shownTicks,out decimal buyPrice,out decimal sellPrice);
        DeviceTerminalUI.Set(range,"± " + shownTicks + " 틱");
        DeviceTerminalUI.Set(bid,prices ? TradingOrderView.Number(running ? operation.SpreadBid : buyPrice) : "—");
        DeviceTerminalUI.Set(ask,prices ? TradingOrderView.Number(running ? operation.SpreadAsk : sellPrice) : "—");
        DeviceTerminalUI.Set(cash,TradingOrderView.Number(operation.SpreadAvailableCash));
        DeviceTerminalUI.Set(shares,TradingOrderView.Number(operation.SpreadAvailableShares(stock)));
        DeviceTerminalUI.Set(buyRemaining,TradingOrderView.Number(running ? operation.SpreadBuyRemaining : 0m));
        DeviceTerminalUI.Set(sellRemaining,TradingOrderView.Number(running ? operation.SpreadSellRemaining : 0m));
        DeviceTerminalUI.Set(bought,TradingOrderView.Number(mine ? operation.Bought : 0m)); DeviceTerminalUI.Set(sold,TradingOrderView.Number(mine ? operation.Sold : 0m));
        decimal gain = mine ? operation.SpreadRealized : 0m, total = mine ? decimal.Round(running ? operation.MarkedResult : operation.Result) : 0m;
        DeviceTerminalUI.Set(realized,InvestmentDashboard.Signed(gain));
        realized.color = gain > 0m ? MarketColors.Up : gain < 0m ? MarketColors.Down : Ink;
        DeviceTerminalUI.Set(marked,InvestmentDashboard.Signed(total));
        marked.color = total > 0m ? MarketColors.Up : total < 0m ? MarketColors.Down : Ink;
        DeviceTerminalUI.Set(elapsed,mine && operation.SpreadStartedGameMinute>0 ? "실행 경과 · "+DeviceMarketJournal.Duration(operation.SpreadElapsedGameMinutes) : "");
        DeviceTerminalUI.Set(updated,!mine || operation.SpreadUpdatedStamp==null ? "" : "갱신 · "+operation.SpreadUpdatedStamp);
        var buffer=new System.Text.StringBuilder();
        int shown=0;
        if (mine)
        {
            // 한 건을 한 줄로 적는다(진행 중 목록에 자리를 내주려고 두 줄에서 한 줄로 줄였다).
            foreach (var fill in operation.SpreadFills)
            {
                if (++shown>3)
                { break; }
                buffer.Append(fill.Time).Append("  ").Append(fill.Buy ? "매수" : "매도").Append("  ");
                buffer.Append(TradingOrderView.Number(fill.Price)).Append("돈 · ").Append(TradingOrderView.Number(fill.Quantity)).Append("주").AppendLine();
            }
        }
        DeviceTerminalUI.Set(tape,buffer.Length>0 ? buffer.ToString().TrimEnd() : "—");
        runList.Refresh();
        // 이 종목에서 아무것도 돌지 않을 때만 새로 시작할 수 있다. 종목 넘기기는 잠그지 않는다.
        start.interactable = !busy && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
        cancel.interactable = running;
        TabletControlStyle.Emphasize(start,!running);
        if (running) { TabletControlStyle.Danger(cancel); } else { TabletControlStyle.Emphasize(cancel,false); }
    }
}
