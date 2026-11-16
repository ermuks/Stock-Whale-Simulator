# 두 번째 태블릿 스프레드 코드 기록

2026-11-15. 이 작업 직전 백업과 비교. 기존 미커밋 변경은 보존.

## 신규 스크립트 전문

### Assets/Scripts/NightShift/TradingOperationController.Spread.cs

```csharp
using System;
using UnityEngine;

/// <summary>유한한 가용 자산으로 양방향 지정가를 유지한다. UI 갱신과 주문 실행을 분리한다.</summary>
public sealed partial class TradingOperationController
{
    public const int MaxSpreadTicks = 20;
    public DeviceOrderSession spreadSession;
    [SerializeField, Min(.25f)] private float spreadRefreshSeconds = .5f;
    public int SpreadTicks { get; private set; } = 2;
    public bool SpreadRunning => CurrentMode == Mode.Spread && Active;
    public decimal SpreadBid { get; private set; }
    public decimal SpreadAsk { get; private set; }
    public decimal SpreadRealized { get; private set; }
    public decimal SpreadBuyRemaining => Live(spreadBuy) ? spreadBuy.volume : 0m;
    public decimal SpreadSellRemaining => Live(spreadSell) ? spreadSell.volume : 0m;
    public decimal SpreadAvailableCash => account == null ? 0m : spreadSession != null && spreadSession.account == account ? spreadSession.AvailableCash : PlayerAccounts.Available(account);
    private Order spreadBuy, spreadSell;
    private float nextSpread;

    public decimal SpreadAvailableShares(Stock stock)
    {
        if (account == null || stock == null)
        { return 0m; }
        return spreadSession != null && spreadSession.account == account ? spreadSession.AvailableShares(stock) : account.GetHolding(stock);
    }
    public void SetSpreadTicks(int ticks)
    {
        int next = Mathf.Clamp(ticks, 1, MaxSpreadTicks);
        if (SpreadTicks == next)
        { return; }
        SpreadTicks = next;
        nextSpread = 0f;
    }
    public static bool SpreadPrices(decimal center, int ticks, out decimal bid, out decimal ask)
    {
        bid = ask = 0m;
        if (center < 1m || center > decimal.MaxValue - 100000m)
        { return false; }
        bid = PriceTickPolicy.FloorToTick(center);
        ask = PriceTickPolicy.CeilToTick(center);
        for (int i = 0; i < Mathf.Clamp(ticks, 1, MaxSpreadTicks); i++)
        {
            bid = Math.Max(1m, PriceTickPolicy.OneTickBelow(bid));
            ask = PriceTickPolicy.OneTickAbove(ask);
        }
        return bid >= 1m && ask > bid && PriceTickPolicy.IsValidPrice(bid) && PriceTickPolicy.IsValidPrice(ask);
    }
    public bool StartSpread(Stock stock)
    {
        if (Active)
        { Feedback = "매매 실행 중"; return false; }
        var accounts = PlayerAccounts.Instance;
        if (accounts != null && account == accounts.automated && !accounts.AutomatedUnlocked)
        { Feedback = "계좌 잠김"; return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade)
        { Feedback = "거래 불가"; return false; }
        if (!SpreadPrices(stock.currentPrice, SpreadTicks, out decimal bid, out decimal ask))
        { Feedback = "시세 대기"; return false; }
        if (spreadSession == null && accounts != null)
        { spreadSession = account == accounts.automated ? accounts.automatedOrders : account == accounts.primary ? accounts.primaryOrders : null; }
        Unsubscribe();
        Target = stock;
        EntryPrice = stock.currentPrice;
        InitialInventory = Inventory = account.GetOwnedVolume(stock);
        Budget = SpreadAvailableCash;
        Spent = Proceeds = Bought = Sold = Result = SpreadRealized = 0m;
        FillCount = 0; Elapsed = 0f;
        pending.Clear(); spreadBuy = spreadSell = null;
        SpreadBid = bid; SpreadAsk = ask;
        CurrentMode = Mode.Spread;
        Target.OnMatchedTrade += OnFill;
        nextSpread = 0f;
        RefreshSpread();
        return true;
    }
    public void CancelSpread()
    {
        if (!SpreadRunning)
        { return; }
        Finish("매매 취소");
        spreadBuy = spreadSell = null;
        Feedback = "매매 취소됨";
    }
    private bool Live(Order order) => order != null && Target != null && order.volume > 0m && Target.orderBook.IsActive(order);
    private void Retire(ref Order order)
    {
        if (order == null)
        { return; }
        if (Live(order))
        { Target.orderBook.CancelOrder(order, Target); }
        pending.Remove(order); order = null;
    }
    private void RefreshSpread()
    {
        if (!SpreadRunning || Time.timeScale <= 0f)
        { return; }
        Elapsed += Time.deltaTime;
        if (Time.unscaledTime < nextSpread)
        { return; }
        nextSpread = Time.unscaledTime + Math.Max(.25f, spreadRefreshSeconds);
        if (!MarketDayController.CanTrade || !Target.AcceptsOrders || Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { CancelSpread(); Feedback = "거래 종료"; return; }
        if (!SpreadPrices(Target.currentPrice, SpreadTicks, out decimal bid, out decimal ask))
        { CancelSpread(); Feedback = "시세 대기"; return; }
        SpreadBid = bid; SpreadAsk = ask;
        // 먼저 이전 가격의 양쪽 호가를 회수한다. 새 호가가 자신의 옛 호가와 교차하지 않게 한다.
        if (!Live(spreadBuy) || spreadBuy.price != bid)
        { Retire(ref spreadBuy); }
        if (!Live(spreadSell) || spreadSell.price != ask)
        { Retire(ref spreadSell); }
        decimal buyCash = SpreadAvailableCash + (Live(spreadBuy) ? spreadBuy.price * spreadBuy.volume : 0m);
        decimal sellShares = SpreadAvailableShares(Target) + SpreadSellRemaining;
        MaintainQuote(ref spreadBuy, true, bid, decimal.Floor(buyCash / bid));
        MaintainQuote(ref spreadSell, false, ask, decimal.Floor(sellShares));
        Feedback = SpreadBuyRemaining > 0m && SpreadSellRemaining > 0m ? "양방향 대기" : SpreadBuyRemaining > 0m ? "매수 대기" : SpreadSellRemaining > 0m ? "매도 대기" : "자산 · 호가 대기";
    }
    private void MaintainQuote(ref Order quote, bool buy, decimal price, decimal quantity)
    {
        if (Live(quote) && quote.price == price && quote.volume == quantity)
        { return; }
        Retire(ref quote);
        if (quantity < 1m)
        { return; }
        // 스프레드 주문은 유동성을 제공한다. 반대 호가를 쓸어 먹거나 수동 주문을 자전 취소하지 않는다.
        if (buy) { Target.orderBook.GetTopAsks(levels, 1); }
        else { Target.orderBook.GetTopBids(levels, 1); }
        if (levels.Count > 0 && (buy ? levels[0].price <= price : levels[0].price >= price))
        { return; }
        quantity = decimal.Floor(Math.Min(quantity, buy ? SpreadAvailableCash / price : SpreadAvailableShares(Target)));
        if (quantity < 1m)
        { return; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try
        {
            if (buy) { account.BuyLimit(price, quantity, 0, Target); }
            else { account.SellLimit(price, quantity, 0, Target); }
        }
        finally { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!beforeSubmit.Contains(order))
            { quote = order; pending.Add(order); }
        }
    }
}
```

### Assets/Scripts/NightShift/SpreadTradingUI.cs

```csharp
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
    private Button start, cancel;
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
        var label = tablet.autoButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { label.text = "스프레드"; }
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
        Label("Title","스프레드",32,12,280,56,36);
        state = Label("State","",336,12,584,56,27); state.color = Muted;
        Action("Manual Orders","주문",1056,12,240,() => tablet.ShowPage(TabletAccountUI.Page.Orders));
        Action("News","뉴스",1328,12,240,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        Rule(92);
        stockName = Label("Stock","",32,112,880,64,42);
        currentPrice = Number("Current Price",1056,112,472,64,42);
        Label("Current Price Unit","원",1536,112,32,64,25).color = Muted;
        cash = Metric("주문 가능 현금",32,208,736,"원");
        shares = Metric("매도 가능 수량",832,208,736,"주");
        Rule(272);
        Label("Bid Label","매수 대기",32,296,320,48,28).color = Muted;
        Label("Ask Label","매도 대기",832,296,320,48,28).color = Muted;
        bid = Number("Bid",32,344,688,64,40);
        ask = Number("Ask",832,344,688,64,40);
        Label("Bid Unit","원",736,344,32,64,25).color = Muted;
        Label("Ask Unit","원",1536,344,32,64,25).color = Muted;
        buyRemaining = Metric("대기 수량",32,424,736,"주");
        sellRemaining = Metric("대기 수량",832,424,736,"주");
        Rule(488);
        Label("Range Label","스프레드 범위",32,504,400,48,28);
        range = Label("Range","",1056,504,512,48,30); range.alignment = TextAlignmentOptions.MidlineRight;
        BuildSlider();
        bought = Metric("매수 체결",32,656,736,"주");
        sold = Metric("매도 체결",832,656,736,"주");
        realized = Metric("실현손익",832,712,736,"원");
        start = Action("Start Spread","매매 시작",32,792,736,StartTrading,true);
        cancel = Action("Cancel Spread","매매 취소",832,792,736,CancelTrading);
    }
    private void BuildSlider()
    {
        var root = new GameObject("Area :: Spread Range",typeof(RectTransform),typeof(Image),typeof(Slider));
        var rect = (RectTransform)root.transform;
        TradingOrderView.Place(rect,transform,48,564,1504,56,1600,880);
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
        Label("Minimum Range","1틱",32,616,160,32,24).color = Muted;
        var max = Label("Maximum Range",TradingOperationController.MaxSpreadTicks+"틱",1408,616,160,32,24); max.color = Muted; max.alignment = TextAlignmentOptions.MidlineRight;
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
        label.alignment = TextAlignmentOptions.MidlineLeft; label.text = value; return label;
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
        DeviceTerminalUI.Set(state,operation.SpreadRunning ? operation.Feedback : operation.CurrentMode == TradingOperationController.Mode.Standby ? "" : operation.Feedback);
        bool prices = TradingOperationController.SpreadPrices(stock == null ? 0m : stock.currentPrice,operation.SpreadTicks,out decimal buyPrice,out decimal sellPrice);
        DeviceTerminalUI.Set(range,"± " + operation.SpreadTicks + "틱");
        DeviceTerminalUI.Set(bid,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadBid : buyPrice) : "—");
        DeviceTerminalUI.Set(ask,prices ? TradingOrderView.Number(operation.SpreadRunning ? operation.SpreadAsk : sellPrice) : "—");
        DeviceTerminalUI.Set(cash,TradingOrderView.Number(operation.SpreadAvailableCash));
        DeviceTerminalUI.Set(shares,TradingOrderView.Number(operation.SpreadAvailableShares(stock)));
        DeviceTerminalUI.Set(buyRemaining,TradingOrderView.Number(operation.SpreadBuyRemaining));
        DeviceTerminalUI.Set(sellRemaining,TradingOrderView.Number(operation.SpreadSellRemaining));
        DeviceTerminalUI.Set(bought,TradingOrderView.Number(operation.Bought)); DeviceTerminalUI.Set(sold,TradingOrderView.Number(operation.Sold));
        DeviceTerminalUI.Set(realized,InvestmentDashboard.Signed(operation.SpreadRealized));
        realized.color = operation.SpreadRealized > 0m ? new Color32(239,89,100,255) : operation.SpreadRealized < 0m ? new Color32(76,150,232,255) : Ink;
        start.interactable = !operation.Active && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
        cancel.interactable = operation.SpreadRunning;
    }
}
```

## 기존 파일 함수별 원본/수정본

TradingOperationController는 partial로 바꾸고 기존 enum 뒤에 Spread를 추가해 기존 값의 번호를 유지했다. ModeNames에 스프레드를 추가했다. DeskOperationUI에는 런타임 SpreadTradingUI 참조를 추가했다.

### Assets/Scripts/NightShift/TradingOperationController.cs · Update

원본:

```csharp
void Update()
    {
        if (!Active || Time.timeScale <= 0f)
        { return; }
        if (Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { Finish("대상 종목 거래 종료"); return; }
        if (!MarketDayController.CanTrade)
        {
            if (MarketDayController.Instance != null && MarketDayController.Instance.Phase != MarketPhase.PreOpen)
            { Finish("장 종료"); }
            return;
        }
        if (!Target.AcceptsOrders)
        { SetMode(Mode.Paused); Feedback = "거래중지: 미체결 주문 회수. 재개 후 직접 다시 실행하세요."; return; }
        if (CurrentMode == Mode.Prepared)
        { return; }
        Elapsed += Time.deltaTime;
        if (CurrentMode == Mode.Paused || Time.time < nextOrder)
        { return; }
        nextOrder = Time.time + (CurrentMode == Mode.Drive ? 2f : 3.5f);
        CancelPending();
        SubmitSlice();
    }
```

수정본:

```csharp
void Update()
    {
        if (!Active || Time.timeScale <= 0f)
        { return; }
        if (CurrentMode == Mode.Spread)
        { RefreshSpread(); return; }
        if (Target.IsDelisted || Target.IsDelisting || !Target.IsListed)
        { Finish("대상 종목 거래 종료"); return; }
        if (!MarketDayController.CanTrade)
        {
            if (MarketDayController.Instance != null && MarketDayController.Instance.Phase != MarketPhase.PreOpen)
            { Finish("장 종료"); }
            return;
        }
        if (!Target.AcceptsOrders)
        { SetMode(Mode.Paused); Feedback = "거래중지: 미체결 주문 회수. 재개 후 직접 다시 실행하세요."; return; }
        if (CurrentMode == Mode.Prepared)
        { return; }
        Elapsed += Time.deltaTime;
        if (CurrentMode == Mode.Paused || Time.time < nextOrder)
        { return; }
        nextOrder = Time.time + (CurrentMode == Mode.Drive ? 2f : 3.5f);
        CancelPending();
        SubmitSlice();
    }
```

### Assets/Scripts/NightShift/TradingOperationController.cs · SubmitSlice

원본:

```csharp
public bool SubmitSlice()
    {
        if (!Active || !MarketDayController.CanTrade || !Target.AcceptsOrders || CurrentMode == Mode.Prepared || CurrentMode == Mode.Paused)
        { return false; }
        bool buy = CurrentMode != Mode.Unwind;
        if (buy)
        {
            if (CurrentMode == Mode.Drive)
            { Target.orderBook.GetTopAsks(levels, 2); }
            else
            { Target.orderBook.GetTopBids(levels, 1); }
        }
        else
        { Target.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { Feedback = "해당 호가가 비었습니다. 다음 주문 시점을 기다립니다."; return false; }
        decimal price = levels[levels.Count - 1].price;
        // A bounded game action, never an unlimited market sweep into an empty book.
        if (Math.Abs(price / Target.currentPrice - 1m) > .025m)
        { Feedback = "호가 간격이 큽니다. 현재가 2.5% 이내 호가를 기다립니다."; return false; }
        decimal cashLeft = Math.Max(0m, Math.Min(Budget - Spent - CommittedCash, account.cash - account.ReservedCash));
        decimal desired = Math.Max(20m, Math.Min(CurrentMode == Mode.Drive ? 500m : 200m, decimal.Floor(Budget / EntryPrice / 30m)));
        decimal quantity = decimal.Floor(buy ? Math.Min(desired, cashLeft / price) : Math.Min(desired * 2m, Math.Min(Inventory, account.GetHolding(Target))));
        if (quantity < 1m)
        {
            if (!buy)
            { Finish("작업 물량 회수 완료"); }
            else
            { SetMode(Mode.Paused); Feedback = "추가 매수 예산 소진. 회수 시점을 결정하세요."; }
            return false;
        }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try
        {
            if (buy)
            { account.BuyLimit(price, quantity, 0, Target); }
            else
            { account.SellLimit(price, quantity, 0, Target); }
        }
        finally
        { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!beforeSubmit.Contains(order))
            { pending.Add(order); }
        }
        Feedback = $"{(buy ? "매수" : "매도")} {quantity:N0}주 · {price:N0}원 주문 / 체결은 호가 상황에 따라 달라집니다.";
        return true;
    }
```

수정본:

```csharp
public bool SubmitSlice()
    {
        if (CurrentMode == Mode.Spread)
        { return false; }
        if (!Active || !MarketDayController.CanTrade || !Target.AcceptsOrders || CurrentMode == Mode.Prepared || CurrentMode == Mode.Paused)
        { return false; }
        bool buy = CurrentMode != Mode.Unwind;
        if (buy)
        {
            if (CurrentMode == Mode.Drive)
            { Target.orderBook.GetTopAsks(levels, 2); }
            else
            { Target.orderBook.GetTopBids(levels, 1); }
        }
        else
        { Target.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { Feedback = "해당 호가가 비었습니다. 다음 주문 시점을 기다립니다."; return false; }
        decimal price = levels[levels.Count - 1].price;
        // A bounded game action, never an unlimited market sweep into an empty book.
        if (Math.Abs(price / Target.currentPrice - 1m) > .025m)
        { Feedback = "호가 간격이 큽니다. 현재가 2.5% 이내 호가를 기다립니다."; return false; }
        decimal cashLeft = Math.Max(0m, Math.Min(Budget - Spent - CommittedCash, account.cash - account.ReservedCash));
        decimal desired = Math.Max(20m, Math.Min(CurrentMode == Mode.Drive ? 500m : 200m, decimal.Floor(Budget / EntryPrice / 30m)));
        decimal quantity = decimal.Floor(buy ? Math.Min(desired, cashLeft / price) : Math.Min(desired * 2m, Math.Min(Inventory, account.GetHolding(Target))));
        if (quantity < 1m)
        {
            if (!buy)
            { Finish("작업 물량 회수 완료"); }
            else
            { SetMode(Mode.Paused); Feedback = "추가 매수 예산 소진. 회수 시점을 결정하세요."; }
            return false;
        }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        beforeSubmit.Clear();
        foreach (var order in snapshot)
        { beforeSubmit.Add(order); }
        submitting = true;
        try
        {
            if (buy)
            { account.BuyLimit(price, quantity, 0, Target); }
            else
            { account.SellLimit(price, quantity, 0, Target); }
        }
        finally
        { submitting = false; }
        Target.orderBook.CopyOwnerOrders(account, snapshot);
        foreach (var order in snapshot)
        {
            if (!beforeSubmit.Contains(order))
            { pending.Add(order); }
        }
        Feedback = $"{(buy ? "매수" : "매도")} {quantity:N0}주 · {price:N0}원 주문 / 체결은 호가 상황에 따라 달라집니다.";
        return true;
    }
```

### Assets/Scripts/NightShift/TradingOperationController.cs · OnFill

원본:

```csharp
private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!Active || (!submitting && !pending.Contains(resting)))
        { return; }
        if (buyer == account && seller != account)
        { Bought += volume; Spent += price * volume; Inventory += volume; FillCount++; }
        else if (seller == account && buyer != account)
        { Sold += volume; Proceeds += price * volume; Inventory -= volume; FillCount++; }
    }
```

수정본:

```csharp
private void OnFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!Active || (!submitting && !pending.Contains(resting)))
        { return; }
        if (ReferenceEquals(buyer, account) && !ReferenceEquals(seller, account))
        { Bought += volume; Spent += price * volume; Inventory += volume; FillCount++; }
        else if (ReferenceEquals(seller, account) && !ReferenceEquals(buyer, account))
        {
            Sold += volume; Proceeds += price * volume; Inventory -= volume; FillCount++;
            if (CurrentMode == Mode.Spread)
            { SpreadRealized += (price - resting.costBasis) * volume; }
        }
    }
```

### Assets/Scripts/NightShift/DeskOperationUI.cs · Update

원본:

```csharp
void Update()
    {
        if (operation == null || Time.unscaledTime < nextRefresh)
        { return; }
        nextRefresh = Time.unscaledTime + .5f;
        var manager = StockManager.Instance;
        var selected = manager == null ? null : manager.CurrentStock;
        var target = operation.Active ? operation.Target : selected;
        string observation = MarketRhythmDirector.Instance == null ? "체결 관찰 대기" : MarketRhythmDirector.Instance.Observation(selected);
        Text(monitorStatus, observation + "\n\n" + (operation.Active ? operation.Target.stockName + " · " + operation.ModeName + $"\n체결 {operation.FillCount}건 · 잔여 {operation.Inventory:N0}주" : "주문 없이도 시장은 계속 움직입니다.") + "\n1 기본 계좌 / 2 자동 계좌 / 3 전화");
        if (!deskPanel.activeInHierarchy)
        { return; }
        Text(heading, (journal ? "시장 기록" : "포지션 관리") + "     " + (target == null ? "" : target.stockName));
        Text(stateText, Time.timeScale <= 0f ? "일시정지" : journal ? "관찰 → 비교 → 판단" : operation.ModeName);
        var shownAccount = journal && displayAccount != null ? displayAccount : operation.account;
        if (shownAccount != null)
        { Text(accountText, $"가용 현금 {System.Math.Max(0m, shownAccount.cash - shownAccount.ReservedCash):N0}원    ·    예치 {shownAccount.ReservedCash:N0}원"); }
        if (journal)
        {
            Text(details, observation + "\n\n최근 주문 기록\n" + operation.Debrief);
            Text(history, WorldDramaDirector.Instance == null ? "사건을 관찰하는 중입니다." : WorldDramaDirector.Instance.Journal);
            Text(feedback, WorldDramaDirector.Instance == null ? "" : WorldDramaDirector.Instance.BoardResult);
        }
        else
        {
            decimal shownBudget = operation.Active ? operation.Budget : operation.account == null ? 0m : decimal.Floor(System.Math.Max(0m, operation.account.cash - operation.account.ReservedCash) * (decimal)operation.BudgetFraction);
            Text(budgetText, $"예산 {operation.BudgetFraction * 100f:0}%");
            Text(details, $"주문 예산 {shownBudget:N0}원\n매수 {operation.Spent:N0}원 / 매도 {operation.Proceeds:N0}원\n물량 {operation.Inventory:N0}주 · 미체결 {operation.OutstandingCount}건\n평가 포함 손익 {operation.MarkedResult:+#,##0;-#,##0;0}원");
            Text(history, "종목과 예산을 준비한 뒤\n필요한 주문 방식만 선택하세요.\n\n관망은 이 계획의 미체결만 취소합니다.\n이미 체결된 주식은 계좌에 남습니다.");
            Text(feedback, operation.Feedback);
            prepare.interactable = !operation.Active && MarketDayController.CanTrade;
            budget.interactable = !operation.Active;
            build.interactable = drive.interactable = unwind.interactable = pause.interactable = finish.interactable = operation.Active;
        }
        if (heatFill != null) { heatFill.gameObject.SetActive(false); }
        if (progressFill != null) { progressFill.fillAmount = operation.Budget > 0m ? Mathf.Clamp01((float)(operation.Spent / operation.Budget)) : 0f; }
    }
```

수정본:

```csharp
void Update()
    {
        if (spreadView != null || operation == null || Time.unscaledTime < nextRefresh)
        { return; }
        nextRefresh = Time.unscaledTime + .5f;
        var manager = StockManager.Instance;
        var selected = manager == null ? null : manager.CurrentStock;
        var target = operation.Active ? operation.Target : selected;
        string observation = MarketRhythmDirector.Instance == null ? "체결 관찰 대기" : MarketRhythmDirector.Instance.Observation(selected);
        Text(monitorStatus, observation + "\n\n" + (operation.Active ? operation.Target.stockName + " · " + operation.ModeName + $"\n체결 {operation.FillCount}건 · 잔여 {operation.Inventory:N0}주" : "주문 없이도 시장은 계속 움직입니다.") + "\n1 기본 계좌 / 2 자동 계좌 / 3 전화");
        if (!deskPanel.activeInHierarchy)
        { return; }
        Text(heading, (journal ? "시장 기록" : "포지션 관리") + "     " + (target == null ? "" : target.stockName));
        Text(stateText, Time.timeScale <= 0f ? "일시정지" : journal ? "관찰 → 비교 → 판단" : operation.ModeName);
        var shownAccount = journal && displayAccount != null ? displayAccount : operation.account;
        if (shownAccount != null)
        { Text(accountText, $"가용 현금 {System.Math.Max(0m, shownAccount.cash - shownAccount.ReservedCash):N0}원    ·    예치 {shownAccount.ReservedCash:N0}원"); }
        if (journal)
        {
            Text(details, observation + "\n\n최근 주문 기록\n" + operation.Debrief);
            Text(history, WorldDramaDirector.Instance == null ? "사건을 관찰하는 중입니다." : WorldDramaDirector.Instance.Journal);
            Text(feedback, WorldDramaDirector.Instance == null ? "" : WorldDramaDirector.Instance.BoardResult);
        }
        else
        {
            decimal shownBudget = operation.Active ? operation.Budget : operation.account == null ? 0m : decimal.Floor(System.Math.Max(0m, operation.account.cash - operation.account.ReservedCash) * (decimal)operation.BudgetFraction);
            Text(budgetText, $"예산 {operation.BudgetFraction * 100f:0}%");
            Text(details, $"주문 예산 {shownBudget:N0}원\n매수 {operation.Spent:N0}원 / 매도 {operation.Proceeds:N0}원\n물량 {operation.Inventory:N0}주 · 미체결 {operation.OutstandingCount}건\n평가 포함 손익 {operation.MarkedResult:+#,##0;-#,##0;0}원");
            Text(history, "종목과 예산을 준비한 뒤\n필요한 주문 방식만 선택하세요.\n\n관망은 이 계획의 미체결만 취소합니다.\n이미 체결된 주식은 계좌에 남습니다.");
            Text(feedback, operation.Feedback);
            prepare.interactable = !operation.Active && MarketDayController.CanTrade;
            budget.interactable = !operation.Active;
            build.interactable = drive.interactable = unwind.interactable = pause.interactable = finish.interactable = operation.Active;
        }
        if (heatFill != null) { heatFill.gameObject.SetActive(false); }
        if (progressFill != null) { progressFill.fillAmount = operation.Budget > 0m ? Mathf.Clamp01((float)(operation.Spent / operation.Budget)) : 0f; }
    }
```

### Assets/Scripts/NightShift/TabletAccountUI.cs · Start

원본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        ShowPage(Page.Dashboard);
    }
```

수정본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        if (second) { SpreadTradingUI.Install(this); }
        ShowPage(second ? Page.Automation : Page.Dashboard);
    }
```

### Assets/Scripts/NightShift/TabletAccountUI.cs · Unlock

원본:

```csharp
private void Unlock()
    {
        if (!second)
        { return; }
        lockRoot.SetActive(false); contentRoot.SetActive(true); ShowPage(Page.Dashboard);
    }
```

수정본:

```csharp
private void Unlock()
    {
        if (!second)
        { return; }
        lockRoot.SetActive(false); contentRoot.SetActive(true); ShowPage(Page.Automation);
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
        clock.gameObject.SetActive(page != Page.Orders && page != Page.Automation);
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
