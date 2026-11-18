using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Company information and persistent policies in the existing tablet management page.</summary>
public sealed class CompanyDashboardUI : MonoBehaviour
{
    private TabletAccountUI tablet;
    private TMP_FontAsset font;
    private TextMeshProUGUI heading, symbol, ownership, feedback, day;
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[12];
    private readonly TextMeshProUGUI[] captions = new TextMeshProUGUI[12];
    private readonly Button[] policies = new Button[3];
    private Button acquire, trade;
    private Stock selected;
    private Stock inspected;
    private bool showOwn=true;
    private CompanyGrowthUI growthView;
    // 꿈에서 깬 뒤에는 내 기업이 없다. 그때는 지금 보고 있는 종목을 보여 준다.
    public Stock FocusedStock => inspected ?? (showOwn ? CorporateEconomy.Instance?.PlayerCompany ?? StockManager.Instance?.CurrentStock : StockManager.Instance?.CurrentStock);
    public Trader Account => tablet.account;
    private float next;
    public static CompanyDashboardUI Install(TabletAccountUI owner)
    {
        var parent = owner.events.legacy.deskPanel;
        var old = parent.GetComponentInChildren<CompanyDashboardUI>(true);
        if (old != null)
        { return old; }
        foreach (Transform child in parent.transform) { child.gameObject.SetActive(false); }
        var legacy = owner.events.GetComponent<CorporateControlUI>(); if (legacy != null) { legacy.enabled = false; }
        var root = new GameObject("Panel :: Company Dashboard", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent.transform, false); Place((RectTransform)root.transform, 0, 0, 1, 1);
        root.GetComponent<Image>().color = new Color32(14,23,29,255); root.GetComponent<Image>().raycastTarget = false;
        var view = root.AddComponent<CompanyDashboardUI>(); view.tablet = owner;
        view.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading"); view.Build(); return view;
    }
    private void Build()
    {
        heading = Label("Company Name", "", .04f,.025f,.64f,.07f,46);
        symbol = Label("Symbol", "", .04f,.10f,.45f,.045f,25);
        Action("My Company", "내 기업", .65f,.035f,.12f,.07f,() => { inspected=null; showOwn=true; Refresh(); });
        Action("Previous Company", "‹", .78f,.035f,.075f,.07f,() => Select(-1));
        Action("Next Company", "›", .88f,.035f,.075f,.07f,() => Select(1));
        ownership = Label("Ownership", "", .52f,.11f,.435f,.045f,27); ownership.alignment = TextAlignmentOptions.MidlineRight;
        string[] captions = { "발행 주식 수", "마감 종가", "마감 시총", "내 지분", "매출", "운영비", "순이익", "내 배당금", "기업 현금", "미지급 비용", "재투자", "배당 비율" };
        for (int i = 0; i < captions.Length; i++)
        {
            float x = .04f + i % 4 * .235f, y = .21f + i / 4 * .16f;
            var label = Label(captions[i], captions[i], x,y,.215f,.043f,25); label.color = new Color32(153,168,179,255);
            this.captions[i]=label;
            values[i] = Label(captions[i] + " Value", "—", x,y+.052f,.215f,.065f,34);
            values[i].alignment = TextAlignmentOptions.MidlineRight; TradingOrderView.Numeric(values[i]);
            var divider = new GameObject("Image :: Company Divider",typeof(RectTransform),typeof(Image)); divider.transform.SetParent(transform,false);
            Place((RectTransform)divider.transform,x,y+.135f,.215f,.0015f); divider.GetComponent<Image>().color = new Color(1,1,1,.12f); divider.GetComponent<Image>().raycastTarget = false;
        }
        day = Label("Settlement Day", "", .04f,.685f,.91f,.04f,24);
        string[] names = { "배당", "재투자", "비축" };
        for (int i = 0; i < 3; i++)
        { int index = i; policies[i] = Action("Policy " + i,names[i],.04f+i*.315f,.755f,.285f,.075f,() => Policy(index)); }
        Action("Company Growth","성장·상장",.04f,.87f,.215f,.075f,OpenGrowth);
        acquire = Action("Acquire Shares","지분 매입",.275f,.87f,.215f,.075f,Acquire);
        Action("Company News","관련 뉴스",.51f,.87f,.215f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        trade=Action("Company Orders","거래",.745f,.87f,.21f,.075f,() => { if (FocusedStock?.IsListed==true) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(FocusedStock)); tablet.ShowPage(TabletAccountUI.Page.Orders); } });
        feedback = Label("Company Feedback","",.04f,.955f,.91f,.035f,23);
    }
    public void Refresh()
    {
        if (growthView!=null && growthView.gameObject.activeSelf) { return; }
        var stock = FocusedStock;
        if (stock == null || CorporateEconomy.Instance == null)
        { return; }
        if (selected != stock) { selected = stock; DeviceTerminalUI.Set(feedback, ""); }
        DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.IsListed ? stock.symbol : "비상장 · "+stock.symbol);
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            acquire.interactable=trade.interactable=false; DeviceTerminalUI.Set(ownership,"—"); DeviceTerminalUI.Set(day,"—"); return;
        }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal ratio = stock.issuedShares > 0 ? owned / stock.issuedShares * 100 : 0;
        DeviceTerminalUI.Set(ownership,ratio >= 100 ? "완전 소유" : ratio > 50 ? "경영권 확보" : "경영권 50% 초과");
        bool closed = data.CloseFinished, quoted = data.growth?.ListedCloseDay>0;
        DeviceTerminalUI.Set(captions[1],stock.IsListed ? "마감 종가" : "상장 상태");
        DeviceTerminalUI.Set(captions[2],stock.IsListed ? "마감 시총" : "기업 평가액");
        string Money(decimal n) => n.ToString("N0") + "원";
        var rules = CorporateEconomy.Instance.Rules;
        double payout = data.Policy == CompanyPolicy.Dividend ? rules.dividendPayout : data.Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        string[] row = { stock.issuedShares.ToString("N0")+"주", quoted ? Money(data.ClosePrice) : "—", quoted ? Money(data.MarketCap) : "—", ratio.ToString("0.00####")+"%",
            closed ? Money(data.Revenue) : "—", closed ? Money(data.Costs) : "—", closed ? Money(data.Profit) : "—", closed ? Money(data.PlayerDividend) : "—",
            Money(data.Cash), Money(data.Debt), closed ? Money(data.Investment) : "—", payout.ToString("0%") };
        if (!stock.IsListed) { row[1]="비상장"; row[2]=data.growth==null ? "—" : Money(data.growth.Appraisal); }
        for (int i = 0; i < values.Length; i++) { DeviceTerminalUI.Set(values[i],row[i]); }
        values[6].color = InvestmentDashboard.ProfitColor(data.Profit);
        DeviceTerminalUI.Set(day,closed ? GameLocale.Day(data.LastDay) + " · 17:00 결산" : "첫 기업 결산 전");
        for (int i = 0; i < policies.Length; i++)
        { policies[i].interactable = ratio > 50 && MarketDayController.Instance.Phase != MarketPhase.Closing; TabletControlStyle.Emphasize(policies[i],(int)data.Policy == i); }
        acquire.interactable = MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
        trade.interactable=stock.AcceptsOrders && MarketDayController.CanTrade;
    }
    void OnEnable() { next = 0; }
    void Update() { if (Time.unscaledTime < next) { return; } next = Time.unscaledTime + .5f; Refresh(); }
    private void Select(int delta)
    {
        var list=new System.Collections.Generic.List<Stock>(CorporateEconomy.Instance.Companies()); if (list.Count==0) { return; }
        inspected=list[(list.IndexOf(FocusedStock)+delta+list.Count)%list.Count]; showOwn=false;
        if (inspected.IsListed) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(inspected)); } next=0;
    }
    public void ShowCompany(Stock stock) { inspected=stock; showOwn=false; Refresh(); }
    public void OpenGrowth()
    { if (growthView==null) { growthView=CompanyGrowthUI.Install(this); } growthView.Show(FocusedStock); }
    private void Policy(int index)
    { bool accepted = CorporateEconomy.Instance.SetPolicy(selected,(CompanyPolicy)index); DeviceTerminalUI.Set(feedback,accepted ? "경영 정책 저장" : "경영권 필요"); Refresh(); }
    private void Acquire()
    { WorldDramaDirector.Instance.AcquireShares(null,selected,tablet.account); DeviceTerminalUI.Set(feedback,WorldDramaDirector.Instance.BoardResult); Refresh(); }
    private Button Action(string name,string caption,float x,float y,float w,float h,Action action)
    {
        var go = new GameObject("Button :: " + name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        var text = Label(name+" Label",caption,0,0,1,1,29); text.transform.SetParent(go.transform,false); Place(text.rectTransform,0,0,1,1); text.alignment=TextAlignmentOptions.Center;
        TabletControlStyle.Button(button); button.onClick.AddListener(()=>action()); return button;
    }
    private TextMeshProUGUI Label(string name,string text,float x,float y,float w,float h,float size)
    {
        var go = new GameObject("Text :: " + name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(transform,false); Place((RectTransform)go.transform,x,y,w,h);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size; label.enableAutoSizing=false;
        label.color = new Color32(230,235,239,255); label.alignment=TextAlignmentOptions.MidlineLeft; label.textWrappingMode=TextWrappingModes.NoWrap;
        label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false; DeviceTerminalUI.Set(label,text); return label;
    }
    private static void Place(RectTransform rect,float x,float y,float width,float height)
    { rect.anchorMin=new Vector2(x,1-y-height); rect.anchorMax=new Vector2(x+width,1-y); rect.offsetMin=rect.offsetMax=Vector2.zero; }
}
