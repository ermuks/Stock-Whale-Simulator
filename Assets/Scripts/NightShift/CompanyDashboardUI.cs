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
    // 정책 단추마다: 고르면 다음 결산에 어떻게 되는지 한 줄, 그리고 지금 권하는 정책의 꼬리표.
    private readonly TextMeshProUGUI[] policyNotes = new TextMeshProUGUI[3], policyTags = new TextMeshProUGUI[3];
    private TextMeshProUGUI advice;
    private static readonly Color Good = new Color32(61, 220, 132, 255);
    private Button acquire, trade, mine, ladderButton;
    private TextMeshProUGUI ladderLabel;
    private AcquisitionLadderUI ladderView;
    /// <summary>인수 사다리를 연다.</summary>
    public void OpenLadder() { if (ladderView == null) { ladderView = AcquisitionLadderUI.Install(this); } ladderView.Show(); }
    private TextMeshProUGUI mineLabel;
    private float foundNoteUntil;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
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
        heading = Label("Company Name", "", .04f,.025f,.44f,.07f,46);
        ladderButton = Action("Acquisition Ladder", "인수 사다리", .49f,.035f,.15f,.07f,OpenLadder);
        ladderLabel = ladderButton.GetComponentInChildren<TextMeshProUGUI>(true);
        symbol = Label("Symbol", "", .04f,.10f,.45f,.045f,25);
        mine = Action("My Company", "내 기업", .65f,.035f,.12f,.07f,MyCompany);
        mineLabel = mine.GetComponentInChildren<TextMeshProUGUI>(true);
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
        {
            int index = i; policies[i] = Action("Policy " + i,names[i],.04f+i*.315f,.732f,.285f,.10f,() => Policy(index));
            Place(policies[i].GetComponentInChildren<TextMeshProUGUI>(true).rectTransform,0,.06f,1,.50f);
            policyNotes[i] = Label("Policy Note " + i,"",0,0,1,1,21); policyNotes[i].transform.SetParent(policies[i].transform,false); Place(policyNotes[i].rectTransform,.03f,.56f,.94f,.36f); policyNotes[i].alignment = TextAlignmentOptions.Center;
            policyTags[i] = Label("Policy Tag " + i,"",0,0,1,1,20); policyTags[i].transform.SetParent(policies[i].transform,false); Place(policyTags[i].rectTransform,.70f,.08f,.27f,.30f); policyTags[i].alignment = TextAlignmentOptions.MidlineRight;
        }
        advice = Label("Policy Advice","",.04f,.836f,.915f,.032f,21); advice.color = Good;
        Action("Company Growth","성장·상장",.04f,.87f,.215f,.075f,OpenGrowth);
        acquire = Action("Acquire Shares","지분 매입",.275f,.87f,.215f,.075f,Acquire);
        Action("Company News","관련 뉴스",.51f,.87f,.215f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        trade=Action("Company Orders","거래",.745f,.87f,.21f,.075f,() => { if (FocusedStock?.IsListed==true) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(FocusedStock)); tablet.ShowPage(TabletAccountUI.Page.Orders); } });
        feedback = Label("Company Feedback","",.04f,.955f,.91f,.035f,23);
    }
    /// <summary>내 기업이 있으면 그 기업을 보여 주고, 없으면 설립한다(설립 비용이 있을 때).</summary>
    private void MyCompany()
    {
        var economy = CorporateEconomy.Instance; var career = PlayerProgression.Instance;
        // 내가 경영하는 회사가 여럿이면(세운 기업, 삼킨 회사들) 누를 때마다 다음 회사로 넘어간다. 성장 단계는 회사마다 따로다.
        var owned = economy == null ? null : economy.MyCompanies();
        if (owned != null && owned.Count > 0)
        {
            int at = -1; for (int i = 0; i < owned.Count; i++) { if (owned[i] == FocusedStock) { at = i; break; } }
            inspected = owned[(at + 1) % owned.Count]; showOwn = true; selected = null; Refresh();
            return;
        }
        if (economy != null && career != null && economy.PlayerCompany == null)
        {
            bool founded = economy.FoundCompany();
            string note = founded ? Word("기업을 세웠습니다", "Company founded", "会社を設立しました")
                : Word("기업 설립에는 ", "Founding a company takes ", "会社の設立には") + career.FoundingCost.ToString("N0") + Word("원이 필요합니다", " KRW", "ウォン必要です");
            GameLocalizedText.Set(feedback, note, true); foundNoteUntil = Time.unscaledTime + 6f;
            if (!founded) { return; }
        }
        inspected=null; showOwn=true; selected=null; Refresh();
    }
    public void Refresh()
    {
        if (mineLabel != null && CorporateEconomy.Instance != null)
        {
            var managed = CorporateEconomy.Instance.MyCompanies(); int at = -1; for (int i = 0; i < managed.Count; i++) { if (managed[i] == FocusedStock) { at = i; break; } }
            GameLocalizedText.Set(mineLabel, managed.Count == 0 ? Word("기업 설립", "Found", "会社設立") : Word("내 기업", "Mine", "自社") + (managed.Count > 1 ? " " + (at < 0 ? managed.Count.ToString() : (at + 1) + "/" + managed.Count) : ""), true);
        }
        if (ladderLabel != null) { GameLocalizedText.Set(ladderLabel, Word("인수 사다리", "Ladder", "買収のはしご"), true); }
        if (growthView!=null && growthView.gameObject.activeSelf) { return; }
        if (ladderView!=null && ladderView.gameObject.activeSelf) { return; }
        var stock = FocusedStock;
        if (stock == null || CorporateEconomy.Instance == null)
        { return; }
        if (selected != stock) { selected = stock; if (Time.unscaledTime >= foundNoteUntil) { DeviceTerminalUI.Set(feedback, ""); } }
        bool small = CorporateEconomy.IsSmall(stock) && !stock.IsListed;
        if (small)
        {
            // 비상장 소기업: 이름은 언어마다 따로 적혀 있고, 주인이 부르는 값을 함께 보여 준다.
            GameLocalizedText.Set(heading, stock.stockName, true);
            GameLocalizedText.Set(symbol, Word("비상장 소기업 · ", "Unlisted small company · ", "非上場の小企業 · ") + CorporateEconomy.Instance.SmallSector(stock) + Word(" · 인수가 주당 ", " · asking ", " · 買収価格 1株 ")
                + CorporateEconomy.Instance.SmallAsk(stock).ToString("N0") + Word("원", " KRW a share", "ウォン")
                + (CorporateEconomy.Instance.AwaitingListing(stock) ? Word(" · 상장 대기 중", " · listing soon", " · 上場待ち") : ""), true);
        }
        else { DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.IsListed ? stock.symbol : "비상장 · "+stock.symbol); }
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            foreach (var label in policyNotes) { GameLocalizedText.Set(label, "", true); } foreach (var label in policyTags) { GameLocalizedText.Set(label, "", true); } GameLocalizedText.Set(advice, "", true);
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
        // 정책마다 다음 결산의 예상과, 지금 권하는 정책.
        var economy = CorporateEconomy.Instance; var best = economy.Recommend(stock, out string why);
        string[] policyNames = { Word("배당", "Dividend", "配当"), Word("재투자", "Reinvest", "再投資"), Word("비축", "Reserves", "内部留保") };
        for (int i = 0; i < policies.Length; i++)
        {
            var outlook = economy.Outlook(stock, (CompanyPolicy)i); bool chosen = (int)data.Policy == i;
            string line = Word("내 배당 +", "My dividend +", "配当 +") + DeviceTerminalUI.Money(outlook.Mine) + " · "
                + (i == 2 ? Word("회사 현금 +", "cash +", "現金 +") + DeviceTerminalUI.Money(Math.Max(0m, outlook.Kept)) : Word("하루 이익 +", "daily profit +", "1日の利益 +") + DeviceTerminalUI.Money(outlook.ProfitGain));
            if (!GameLocalizedText.Matches(policyNotes[i], line)) { GameLocalizedText.Set(policyNotes[i], line, true); }
            policyNotes[i].color = chosen ? TabletKitStyle.Background : new Color32(153,168,179,255);
            string tag = (int)best == i ? Word("추천", "Best", "おすすめ") : "";
            if (!GameLocalizedText.Matches(policyTags[i], tag)) { GameLocalizedText.Set(policyTags[i], tag, true); }
            policyTags[i].color = chosen ? TabletKitStyle.Background : Good;
        }
        string note = Word("추천 · ", "Suggested · ", "おすすめ · ") + policyNames[(int)best] + "  —  " + why;
        if (!GameLocalizedText.Matches(advice, note)) { GameLocalizedText.Set(advice, note, true); }
        acquire.interactable = small ? CorporateEconomy.Instance.SmallOffer(stock, tablet.account, out _, out _) && MarketDayController.Instance.Phase != MarketPhase.Closing
            : MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
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
    {
        // 비상장 소기업은 호가창이 없다. 주인에게 직접 값을 치르고 넘겨받는다.
        if (CorporateEconomy.IsSmall(selected) && !selected.IsListed)
        { CorporateEconomy.Instance.BuySmall(selected, tablet.account); GameLocalizedText.Set(feedback, CorporateEconomy.Instance.SmallResult, true); foundNoteUntil = Time.unscaledTime + 6f; Refresh(); return; }
        WorldDramaDirector.Instance.AcquireShares(null,selected,tablet.account); DeviceTerminalUI.Set(feedback,WorldDramaDirector.Instance.BoardResult); Refresh();
    }
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
