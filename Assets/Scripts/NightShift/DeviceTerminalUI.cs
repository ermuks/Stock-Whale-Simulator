using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 두 World Space Canvas의 새 기본 화면. 기존 탭과 이벤트는 그대로 유지한다.</summary>
[DefaultExecutionOrder(150)]
public sealed class DeviceTerminalUI : MonoBehaviour
{
    public bool journal;
    public TabletAccountUI navigation;
    public DeskOperationUI legacy;
    public DeviceOrderSession session;
    public DeviceMarketJournal records;
    public DeviceTerminalUI orderScreen;
    public TabletViewController orderTablet, thisTablet;
    public TabletViewController pairedTablet;
    public GameObject panel, draftPanel, reviewPanel, splitPanel;
    public Button mainTab, buy, sell, split, review, submit, back, cancel, prepare, budget, eventOrder;
    public TMP_InputField price, quantity, parts, interval;
    public TextMeshProUGUI stockName, ticker, currentPrice, change, graphRange, status, feedback;
    public TextMeshProUGUI position, cash, plan, forecast, validation, confirmation, splitLabel, amount;
    public TextMeshProUGUI eventDetail, fills, company, eventCount;
    public Button[] eventButtons;
    public TextMeshProUGUI[] eventTitles, eventTags;
    public RectTransform eventContent;
    public DevicePriceGraph graph;
    public Image progress;
    public GameObject growthPanel;
    public Button growth, closeGrowth, openRooms;
    public TextMeshProUGUI growthText;
    public CompactGrowthUI visualGrowth;
    [NonSerialized] public NewsDeskUI newsReader;
    [NonSerialized] public TradingOrderView tradeView;
    private Stock selected, reviewedStock;
    private bool buying = true, splitting, confirming;
    private decimal reviewedPrice, reviewedQuantity;
    private int reviewedParts;
    private float reviewedInterval, next;
    private int seenVersion = -1;
    private DeviceMarketJournal.Entry selectedEvent;
    private System.Collections.IEnumerator focusing;
    private GameObject quickQuantityPanel;
    private TextMeshProUGUI reviewLabel;
    private Button[] quickButtons;
    private LeadingRoomUI leadingRooms;
    private PendingOrderListUI pendingOrders;
    private readonly StringBuilder text = new(1024);
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(166, 177, 186, 255), Active = new Color32(23, 55, 62, 255), Neutral = new Color32(23, 33, 38, 255);
    public static void Set(TextMeshProUGUI target, string value)
    {
        if (target != null && !GameLocalizedText.Matches(target,value))
        { GameLocalizedText.Set(target,value); }
    }
    public static string Money(decimal value) => PriceTickPolicy.FormatMoney(value) + (GameLocale.Code=="ko" ? "돈" : " "+GameLocale.Text("돈"));
    void Start()
    {
        mainTab.onClick.AddListener(Show);
        legacy.deskTab.onClick.AddListener(Hide);
        legacy.legacyTab.onClick.AddListener(Hide);
        if (journal)
        {
            visualGrowth?.Configure(openRooms,closeGrowth);
            for (int i = 0; i < eventButtons.Length; i++)
            {
                int row = i;
                eventButtons[i].onClick.AddListener(() => SelectEvent(row));
            }
            eventOrder.onClick.AddListener(OpenOrder);
            growth.onClick.AddListener(ShowGrowth); closeGrowth.onClick.AddListener(HideGrowth); openRooms.onClick.AddListener(OpenRooms);
        }
        else
        {
            ConfigureQuickOrder();
            if (float.TryParse(interval.text,NumberStyles.Float,CultureInfo.InvariantCulture,out var realInterval))
            { interval.SetTextWithoutNotify((realInterval*DeviceMarketJournal.GameSecondsPerRealSecond).ToString("0",CultureInfo.InvariantCulture)); }
            buy.onClick.AddListener(Buy); sell.onClick.AddListener(Sell);
            split.onClick.AddListener(Split); review.onClick.AddListener(Review);
            submit.onClick.AddListener(Submit); back.onClick.AddListener(Back);
            cancel.onClick.AddListener(Cancel); prepare.onClick.AddListener(Prepare);
            price.onValueChanged.AddListener(Edited); quantity.onValueChanged.AddListener(Edited);
            parts.onValueChanged.AddListener(Edited); interval.onValueChanged.AddListener(Edited);
        }
        if (navigation == null) { Show(); }
    }
    public void Show()
    {
        if (navigation != null) { navigation.OpenExisting(this); }
        else { ShowLocal(); }
    }
    public void ShowLocal()
    {
        legacy.deskPanel.SetActive(false); legacy.legacyPanel.SetActive(false);
        panel.SetActive(true); next = 0f;
        mainTab.image.color = Active;
    }
    public void FocusScreen()
    {
        Show();
        if (focusing == null && !PhoneCallController.IsRaised)
        { focusing = FocusExistingTablet(); StartCoroutine(focusing); }
    }
    private System.Collections.IEnumerator FocusExistingTablet()
    {
        if (pairedTablet != null)
        {
            pairedTablet.ReturnTablet();
            while (pairedTablet.IsReturning)
            { yield return null; }
        }
        thisTablet.FocusTablet(); focusing = null;
    }
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
    public void FocusGrowth()
    { FocusScreen(); ShowGrowth(); }
    private void Hide()
    { panel.SetActive(false); mainTab.image.color = Neutral; }
    private void ShowGrowth()
    {
        visualGrowth?.Configure(openRooms,closeGrowth);
        growthPanel.transform.SetAsLastSibling(); growthPanel.SetActive(true);
        if (PlayerProgression.Instance != null) { visualGrowth?.Refresh(PlayerProgression.Instance); }
        next = 0f;
    }
    private void HideGrowth() { growthPanel.SetActive(false); }
    private void OpenRooms()
    {
        var career = PlayerProgression.Instance;
        if (career == null)
        { visualGrowth?.ShowFeedback("성장 기록을 불러오는 중입니다."); return; }
        if (!career.CanOperateRooms && !career.Eligible)
        {
            if (orderScreen == null)
            { visualGrowth?.ShowFeedback("주문 화면 연결을 확인해 주세요."); return; }
            HideGrowth(); orderScreen.FocusScreen(); return;
        }
        if (!career.CanOperateRooms)
        {
            if (career.OpenLeadingRooms()) { visualGrowth?.Refresh(career); }
            else { visualGrowth?.ShowFeedback("개설 조건이 바뀌었습니다. 현재 성과를 확인하세요."); }
            next = 0f; return;
        }
        if (leadingRooms == null) { leadingRooms = FindAnyObjectByType<LeadingRoomUI>(); }
        if (leadingRooms == null || leadingRooms.chat == null)
        { visualGrowth?.ShowFeedback("라운지를 준비하는 중입니다. 잠시 후 다시 눌러 주세요."); return; }
        leadingRooms.chat.SelectRoom(1); HideGrowth(); thisTablet.ReturnTablet();
        MarketEventFeed.Instance?.Notify("내 리딩방을 열었습니다. 공용 라운지 채팅에서 + 를 눌러 회원을 초대하세요.");
    }
    private void Buy() { buying = true; Edited(""); }
    private void Sell() { buying = false; Edited(""); }
    private void Split() { splitting = !splitting; Edited(""); }
    private void Edited(string unused) { next = 0f; }
    private void Prepare()
    {
        if (session.Prepare(selected))
        {
            confirming = false;
            CurrentPrice(); quantity.SetTextWithoutNotify("1");
        }
        next = 0f;
    }
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
        if (navigation!=null && navigation.GetComponent<TabletKitLayout>()!=null) { tradeView.FitCanvas(); }
    }
    private Button QuickButton(string name,string caption,float x,float width)
    {
        var rect = new GameObject("Button :: Order " + name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(quickQuantityPanel.transform,false); Layout(rect,x,0,width,1);
        rect.gameObject.layer = quickQuantityPanel.layer;
        var image = rect.gameObject.AddComponent<Image>(); image.color = Neutral; image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Instantiate(reviewLabel,rect); label.name = "Text :: Order " + name;
        Layout(label.rectTransform,0,0,1,1); GameLocalizedText.Set(label,caption); label.fontSize = 29;
        label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.color = Ink;
        return button;
    }
    private static void Layout(RectTransform rect,float x,float y,float width,float height)
    {
        rect.anchorMin = new Vector2(x,1-y-height); rect.anchorMax = new Vector2(x+width,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    private void CurrentPrice()
    {
        if (selected == null)
        { return; }
        price.SetTextWithoutNotify(PriceTickPolicy.RoundToTick(selected.currentPrice).ToString("0",CultureInfo.InvariantCulture));
        next = 0f;
    }
    private void SetQuantity(decimal fraction)
    {
        if (selected == null)
        { return; }
        decimal limit = Number(price);
        decimal maximum = buying ? limit >= 1m ? decimal.Floor(session.AvailableCash / limit) : 0m : session.AvailableShares(selected);
        decimal value = Math.Min(1000000m,decimal.Floor(Math.Max(0m,maximum) * fraction));
        if (maximum >= 1m) { value = Math.Max(1m,value); }
        quantity.SetTextWithoutNotify(value.ToString("0",CultureInfo.InvariantCulture)); next = 0f;
    }
    private static decimal Number(TMP_InputField field)
    {
        return decimal.TryParse(field.text, NumberStyles.None, CultureInfo.InvariantCulture, out decimal value) ? value : 0m;
    }
    private string Check(out decimal p, out decimal q, out int n, out float seconds)
    {
        p = Number(price); q = Number(quantity);
        decimal rawParts = Number(parts), rawInterval = Number(interval);
        n = splitting && rawParts <= 10m ? (int)rawParts : splitting ? 0 : 1;
        double realSeconds=(double)rawInterval/DeviceMarketJournal.GameSecondsPerRealSecond;
        seconds = splitting && realSeconds <= 60d ? (float)realSeconds : splitting ? 0f : 3f;
        return session.Validate(selected, buying, p, q, n, seconds);
    }
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
    private void Back() { confirming = false; next = 0f; }
    private void Cancel() { pendingOrders.CancelAll(); next = 0f; }
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
        change.color = MarketColors.ForChange(delta,Ink);
        graph.color = change.color; graph.SetStock(selected);
        Set(graphRange, "최근 40개 캔들 · 전일 " + Money(reference));
    }
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
    private void SelectEvent(int index)
    {
        if (index >= records.Events.Count)
        { return; }
        selectedEvent = records.Events[index]; seenVersion = -1; next = 0f;
    }
    private void Journal()
    {
        if (growthPanel.activeSelf && PlayerProgression.Instance != null)
        {
            var career = PlayerProgression.Instance;
            if (visualGrowth != null) { visualGrowth.Refresh(career); }
            else { Set(growthText,"개설 비용 " + Money(decimal.Floor(career.OpeningCash)) + " / " + Money(career.OpeningCost)); }
            openRooms.interactable = true;
        }
        if (selectedEvent == null && records.Events.Count > 0)
        { selectedEvent = records.Events[0]; }
        if (seenVersion != records.Version)
        {
            seenVersion = records.Version;
            Set(eventCount, "최근 사건 " + records.Events.Count);
            eventContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, records.Events.Count * 150f);
            for (int i = 0; i < eventButtons.Length; i++)
            {
                bool present = i < records.Events.Count; eventButtons[i].gameObject.SetActive(present);
                if (!present)
                { continue; }
                var entry = records.Events[i];
                Set(eventTitles[i], entry.Time + "  " + entry.Title);
                string tag = entry.Urgent ? "시장 급변" : entry.Stock != null && session.account.GetOwnedVolume(entry.Stock) > 0m ? "내 포지션 관련" : entry.Source;
                Set(eventTags[i], tag); eventTags[i].color = entry.Urgent ? new Color32(230, 185, 99, 255) : Muted;
                eventButtons[i].image.color = entry == selectedEvent ? Active : Color.clear;
            }
            text.Clear();
            int count = Math.Min(records.Fills.Count, 4);
            for (int i = 0; i < count; i++)
            {
                var fill = records.Fills[i];
                text.Append(fill.Time).Append("  ").Append(fill.Stock.symbol).Append("  ").Append(fill.Buy ? "매수 " : "매도 ").Append(fill.Quantity.ToString("N0")).Append("주  ").Append(Money(fill.Price)).AppendLine();
            }
            Set(fills, count > 0 ? text.ToString() : "실제 체결이 발생하면 시간·종목·수량·가격을 기록합니다.");
        }
        Detail();
        decimal shares = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(selected) : session.account.GetOwnedVolume(selected);
        decimal goal = shares < 100m ? 100m : shares < 1000m ? 1000m : shares < 10000m ? 10000m : selected.issuedShares / 2m + 1m;
        Set(company, "보유 " + shares.ToString("N0") + "주 / 다음 목표 " + goal.ToString("N0") + "주\n실제 지분 " + (selected.issuedShares > 0m ? shares / selected.issuedShares * 100m : 0m).ToString("0.####") + "% · 경영권은 50% 초과");
        Set(feedback, WorldDramaDirector.Instance == null ? "사건과 체결을 비교하며 관찰하세요." : WorldDramaDirector.Instance.BoardResult);
    }
    private void Detail()
    {
        var entry = selectedEvent;
        eventOrder.interactable = entry != null && entry.Stock != null && entry.Stock.AcceptsOrders;
        if (entry == null)
        { Set(eventDetail, "시장을 관찰하는 중입니다.\n뉴스와 체결 변화가 발생하면 이곳에 기록됩니다."); return; }
        text.Clear(); text.Append("<b>").Append(entry.Title.Replace("<", "‹")).Append("</b>\n").Append(entry.Source).Append(" · ").Append(entry.Time).Append("\n").Append(entry.Certainty).Append("\n\n");
        if (entry.Stock != null)
        {
            text.Append("발생 직전 60초 · ").Append(entry.Volume.ToString("N0")).Append("주\n매수 주도 비중 ").Append(entry.Volume > 0m ? (entry.BuyVolume / entry.Volume * 100m).ToString("0.0") + "%" : "집계 없음");
            text.Append("\n<color=#A6B1BA>매도 호가를 소진한 체결량 / 전체 체결량\n상승 확률을 뜻하지 않습니다.</color>\n\n");
        }
        text.Append("연결된 라운지 발언\n");
        int found = 0;
        var day = MarketDayController.Instance;
        if (day != null && entry.Id > 0)
        {
            for (int i = day.MessageCount - 1; i >= Math.Max(0, day.MessageCount - 64) && found < 3; i--)
            {
                var message = day.GetMessage(i);
                if (message.Context.EventId != entry.Id)
                { continue; }
                text.Append(message.Nickname.Replace("<", "‹")).Append(": ").Append(message.Text.Replace("<", "‹")).AppendLine(); found++;
            }
        }
        if (found == 0)
        { text.Append("이 사건에 연결된 발언이 아직 없습니다."); }
        Set(eventDetail, text.ToString());
    }
    private void OpenOrder()
    {
        if (selectedEvent == null || selectedEvent.Stock == null)
        { return; }
        int index = StockManager.Instance.stocks.IndexOf(selectedEvent.Stock);
        if (index < 0)
        { return; }
        StockManager.Instance.SelectStock(index); orderScreen.Show();
        if (navigation != null)
        { return; }
        // 기존 들기/내려놓기 경로를 그대로 사용해 원래 책상 위치로 복귀한다.
        StartCoroutine(SwitchTablet());
    }
    private System.Collections.IEnumerator SwitchTablet()
    {
        thisTablet.ReturnTablet();
        while (thisTablet.IsReturning)
        { yield return null; }
        orderTablet.FocusTablet();
    }
    void OnDestroy()
    {
        if (mainTab != null) { mainTab.onClick.RemoveListener(Show); }
        if (legacy != null) { legacy.deskTab.onClick.RemoveListener(Hide); legacy.legacyTab.onClick.RemoveListener(Hide); }
    }
}
