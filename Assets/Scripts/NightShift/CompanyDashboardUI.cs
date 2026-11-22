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
    // 화면의 뼈대: 맨 위에 "내 지분" 게이지와 요약 타일 셋, 가운데에 결산 정책 카드 셋, 맨 아래에 할 일 단추.
    // 숫자는 타일마다 하나만 크게 두고, 딸린 숫자는 그 밑에 작게 붙인다.
    private Image stakeFill;
    private readonly TextMeshProUGUI[] subs = new TextMeshProUGUI[3], policyTitles = new TextMeshProUGUI[3];
    private readonly Image[] splitParts = new Image[3];
    private readonly Image[][] policyBorders = new Image[3][];
    private Button growthButton;
    private static readonly Color Warn = new Color32(242, 188, 100, 255), Soft = new Color32(153, 168, 179, 255), Grey = new Color32(110, 129, 142, 255);
    private static readonly string[] PolicyIcons = { "dividend", "reinvest", "reserve" };
    private void Build()
    {
        heading = Label("Company Name", "", .04f,.02f,.44f,.07f,46);
        symbol = Label("Symbol", "", .04f,.092f,.62f,.04f,23); symbol.color = Soft;
        ladderButton = Action("Acquisition Ladder", "인수 사다리", .49f,.03f,.15f,.065f,OpenLadder);
        ladderLabel = ladderButton.GetComponentInChildren<TextMeshProUGUI>(true);
        mine = Action("My Company", "내 기업", .65f,.03f,.12f,.065f,MyCompany);
        mineLabel = mine.GetComponentInChildren<TextMeshProUGUI>(true);
        Action("Previous Company", "‹", .78f,.03f,.08f,.065f,() => Select(-1));
        Action("Next Company", "›", .875f,.03f,.08f,.065f,() => Select(1));

        // 바탕 칸들을 먼저 깔아 글자가 그 위에 오게 한다.
        TabletKitStyle.Surface("Stake Card", transform, .04f,.155f,.285f,.235f, TabletKitStyle.Panel);
        for (int i = 0; i < 3; i++) { TabletKitStyle.Surface("Summary Tile " + i, transform, .345f + i * .205f,.155f,.19f + (i == 2 ? .01f : 0f),.235f, TabletKitStyle.Panel); }

        string[] names = { "발행 주식 수", "마감 종가", "마감 시총", "내 지분", "매출", "운영비", "순이익", "내 배당금", "기업 현금", "미지급 비용", "재투자", "배당 비율" };
        for (int i = 0; i < names.Length; i++)
        {
            captions[i] = Label(names[i], names[i], 0,0,.1f,.04f,23); captions[i].color = Soft;
            values[i] = Label(names[i] + " Value", "—", 0,0,.1f,.04f,34); TradingOrderView.Numeric(values[i]);
            captions[i].gameObject.SetActive(false); values[i].gameObject.SetActive(false);
        }
        void Show(int index, float x, float y, float w, float captionHeight, float valueY, float valueHeight, float size)
        {
            captions[index].gameObject.SetActive(true); values[index].gameObject.SetActive(true);
            Place(captions[index].rectTransform, x, y, w, captionHeight); Place(values[index].rectTransform, x, valueY, w, valueHeight);
            values[index].alignment = TextAlignmentOptions.MidlineLeft; values[index].enableAutoSizing = true; values[index].fontSizeMax = size; values[index].fontSizeMin = 20;
        }
        // 내 지분: 가장 큰 숫자와 게이지. 가운데 금(50%)을 넘겨야 경영권이다.
        Show(3, .055f,.165f,.14f,.04f,.205f,.105f,66);
        ownership = Label("Ownership", "", .185f,.165f,.127f,.04f,22); ownership.alignment = TextAlignmentOptions.MidlineRight;
        var track = TabletKitStyle.Surface("Stake Track", transform, .055f,.332f,.255f,.024f, TabletKitStyle.Line);
        stakeFill = TabletKitStyle.Surface("Stake Fill", track.transform, 0,0,1,1, TabletKitStyle.Accent);
        TabletKitStyle.Surface("Stake Half Mark", transform, .055f + .255f * .5f,.322f,.0022f,.044f, TabletKitStyle.Ink);
        int[] tiles = { 2, 8, 6 };
        for (int i = 0; i < 3; i++)
        {
            float x = .36f + i * .205f, w = .16f + (i == 2 ? .01f : 0f);
            Show(tiles[i], x,.165f,w,.04f,.21f,.075f,38);
            subs[i] = Label("Summary Note " + i, "", x,.295f,w,.085f,21); subs[i].color = Soft; subs[i].textWrappingMode = TextWrappingModes.Normal; subs[i].alignment = TextAlignmentOptions.TopLeft;
        }

        // 결산 정책: 이익을 어떻게 나눌지. 위의 띠가 지금 정책의 나눔(배당·재투자·비축)이다.
        day = Label("Settlement Day", "", .04f,.415f,.40f,.04f,23); day.color = Soft;
        for (int i = 0; i < 2; i++)
        {
            int index = i == 0 ? 7 : 10; float x = .50f + i * .235f;
            captions[index].gameObject.SetActive(true); values[index].gameObject.SetActive(true);
            Place(captions[index].rectTransform, x,.415f,.09f,.04f); captions[index].alignment = TextAlignmentOptions.MidlineRight; captions[index].fontSize = 21;
            Place(values[index].rectTransform, x + .095f,.415f,.125f,.04f); values[index].alignment = TextAlignmentOptions.MidlineRight; values[index].fontSize = 25;
        }
        var split = TabletKitStyle.Surface("Policy Split", transform, .04f,.467f,.915f,.016f, TabletKitStyle.Line);
        Color[] splitColors = { TabletKitStyle.Accent, Warn, Grey };
        for (int i = 0; i < 3; i++) { splitParts[i] = TabletKitStyle.Surface("Policy Split " + i, split.transform, 0,0,1,1, splitColors[i]); }
        string[] policyNames = { "배당", "재투자", "비축" };
        for (int i = 0; i < 3; i++)
        {
            int index = i; policies[i] = Action("Policy " + i,policyNames[i],.04f+i*.315f,.50f,.285f,.215f,() => Policy(index));
            policyTitles[i] = policies[i].GetComponentInChildren<TextMeshProUGUI>(true);
            Place(policyTitles[i].rectTransform,.20f,.09f,.50f,.30f); policyTitles[i].alignment = TextAlignmentOptions.MidlineLeft; policyTitles[i].fontSize = 34;
            var icon = TabletKitStyle.Glyph(PolicyIcons[i], policies[i].transform, .055f,.11f,.11f,.26f); icon.color = splitColors[i];
            policyNotes[i] = Label("Policy Note " + i,"",0,0,1,1,36); policyNotes[i].richText = true; TradingOrderView.Numeric(policyNotes[i]); policyNotes[i].transform.SetParent(policies[i].transform,false); Place(policyNotes[i].rectTransform,.06f,.46f,.90f,.46f);
            policyNotes[i].alignment = TextAlignmentOptions.TopLeft; policyNotes[i].lineSpacing = 6;
            policyTags[i] = TabletCall.Chip("Policy Tag " + i, policies[i].transform, .73f,.11f,.22f,.20f, Good, 21);
            policyBorders[i] = TabletCall.Border(policies[i].transform, TabletKitStyle.Accent, .009f, .022f);
        }
        advice = Label("Policy Advice","",.04f,.722f,.915f,.036f,22); advice.color = Good;

        // 할 일 단추. 지금 해야 하는 것 하나만 밝게 숨 쉰다.
        growthButton = Action("Company Growth","성장·상장",.04f,.785f,.215f,.095f,OpenGrowth); Icon(growthButton, "growth");
        acquire = Action("Acquire Shares","지분 매입",.275f,.785f,.215f,.095f,Acquire); Icon(acquire, "plus");
        Icon(Action("Company News","관련 뉴스",.51f,.785f,.215f,.095f,() => tablet.ShowPage(TabletAccountUI.Page.Events)), "news");
        trade=Action("Company Orders","거래",.745f,.785f,.21f,.095f,() => { if (FocusedStock?.IsListed==true) { StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(FocusedStock)); tablet.ShowPage(TabletAccountUI.Page.Orders); } }); Icon(trade, "buy");
        feedback = Label("Company Feedback","",.04f,.90f,.91f,.045f,24); feedback.color = Warn;
    }
    private static void Icon(Button button, string name)
    {
        var icon = TabletKitStyle.Glyph(name, button.transform, .07f,.28f,.12f,.44f); icon.raycastTarget = false;
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true); if (label != null) { Place(label.rectTransform,.18f,0,.80f,1); }
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
                : Word("기업 설립에는 ", "Founding a company takes ", "会社の設立には") + career.FoundingCost.ToString("N0") + Word("돈이 필요합니다", " DON", "ドン必要です");
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
                + CorporateEconomy.Instance.SmallAsk(stock).ToString("N0") + Word("돈", " DON a share", "ドン")
                + (CorporateEconomy.Instance.AwaitingListing(stock) ? Word(" · 상장 대기 중", " · listing soon", " · 上場待ち") : ""), true);
        }
        else { DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.IsListed ? stock.symbol : "비상장 · "+stock.symbol); }
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            foreach (var label in policyNotes) { GameLocalizedText.Set(label, "", true); } foreach (var label in policyTags) { label.transform.parent.gameObject.SetActive(false); } GameLocalizedText.Set(advice, "", true);
            foreach (var label in subs) { GameLocalizedText.Set(label, "", true); } foreach (var edges in policyBorders) { TabletCall.Show(edges, false, TabletKitStyle.Accent); }
            stakeFill.rectTransform.anchorMax = new Vector2(0, 1);
            acquire.interactable=trade.interactable=false; TabletCall.Set(acquire, false); TabletCall.Set(growthButton, false); DeviceTerminalUI.Set(ownership,"—"); DeviceTerminalUI.Set(day,"—"); return;
        }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal ratio = stock.issuedShares > 0 ? owned / stock.issuedShares * 100 : 0;
        DeviceTerminalUI.Set(ownership,ratio >= 100 ? "완전 소유" : ratio > 50 ? "경영권 확보" : "경영권 50% 초과");
        bool controls = ratio > 50; ownership.color = controls ? TabletKitStyle.Accent : Warn;
        stakeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)ratio / 100f), 1); stakeFill.color = controls ? TabletKitStyle.Accent : Warn;
        bool closed = data.CloseFinished, quoted = data.growth?.ListedCloseDay>0;
        DeviceTerminalUI.Set(captions[1],stock.IsListed ? "마감 종가" : "상장 상태");
        DeviceTerminalUI.Set(captions[2],stock.IsListed ? "마감 시총" : "기업 평가액");
        string Money(decimal n) => n.ToString("N0") + "돈";
        var rules = CorporateEconomy.Instance.Rules;
        double payout = data.Policy == CompanyPolicy.Dividend ? rules.dividendPayout : data.Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        string[] row = { stock.issuedShares.ToString("N0")+"주", quoted ? Money(data.ClosePrice) : "—", quoted ? Money(data.MarketCap) : "—", ratio.ToString("0.00####")+"%",
            closed ? Money(data.Revenue) : "—", closed ? Money(data.Costs) : "—", closed ? Money(data.Profit) : "—", closed ? Money(data.PlayerDividend) : "—",
            Money(data.Cash), Money(data.Debt), closed ? Money(data.Investment) : "—", payout.ToString("0%") };
        if (!stock.IsListed) { row[1]="비상장"; row[2]=data.growth==null ? "—" : Money(data.growth.Appraisal); }
        for (int i = 0; i < values.Length; i++) { DeviceTerminalUI.Set(values[i],row[i]); }
        values[6].color = InvestmentDashboard.ProfitColor(data.Profit);
        // 타일 밑의 딸린 숫자.
        string shares = stock.issuedShares.ToString("N0") + Word("주", " sh.", "株");
        string noteA = shares + " · " + (!stock.IsListed ? Word("비상장", "Private", "非上場") : Word("종가 ", "close ", "終値 ") + (quoted ? DeviceTerminalUI.Money(data.ClosePrice) : "—"));
        string noteB = Word("미지급 ", "Unpaid ", "未払い ") + DeviceTerminalUI.Money(data.Debt);
        string noteC = closed ? Word("매출 ", "Revenue ", "売上 ") + DeviceTerminalUI.Money(data.Revenue) + "\n" + Word("비용 ", "Costs ", "費用 ") + DeviceTerminalUI.Money(data.Costs) : Word("첫 결산 전", "Before first results", "初回決算前");
        string[] notes = { "", data.Debt > 0 ? noteB : "", closed ? noteC : "" };
        // 첫 결산 전에는 지난 결산 줄(배당금·재투자)을 아예 비운다.
        foreach (var item in new Component[] { day, captions[7], values[7], captions[10], values[10] }) { if (item.gameObject.activeSelf != closed) { item.gameObject.SetActive(closed); } }
        for (int i = 0; i < 3; i++) { if (!GameLocalizedText.Matches(subs[i], notes[i])) { GameLocalizedText.Set(subs[i], notes[i], true); } }
        subs[1].color = data.Debt > 0 ? Warn : Soft;
        // 정책 띠: 이익 가운데 배당·재투자·비축으로 가는 몫.
        double reinvestShare = data.Policy == CompanyPolicy.Investment ? rules.growthInvestment : data.Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        float cutA = Mathf.Clamp01((float)payout), cutB = Mathf.Clamp01((float)(payout + reinvestShare));
        splitParts[0].rectTransform.anchorMin = new Vector2(0, 0); splitParts[0].rectTransform.anchorMax = new Vector2(cutA, 1);
        splitParts[1].rectTransform.anchorMin = new Vector2(cutA, 0); splitParts[1].rectTransform.anchorMax = new Vector2(cutB, 1);
        splitParts[2].rectTransform.anchorMin = new Vector2(cutB, 0); splitParts[2].rectTransform.anchorMax = new Vector2(1, 1);
        DeviceTerminalUI.Set(day,closed ? GameLocale.Day(data.LastDay) + " · 17:00 결산" : "첫 기업 결산 전");
        for (int i = 0; i < policies.Length; i++)
        { policies[i].interactable = ratio > 50 && MarketDayController.Instance.Phase != MarketPhase.Closing; }
        // 정책마다 다음 결산의 예상과, 지금 권하는 정책.
        var economy = CorporateEconomy.Instance; var best = economy.Recommend(stock, out string why);
        string[] policyNames = { Word("배당", "Dividend", "配当"), Word("재투자", "Reinvest", "再投資"), Word("비축", "Reserves", "内部留保") };
        for (int i = 0; i < policies.Length; i++)
        {
            var outlook = economy.Outlook(stock, (CompanyPolicy)i); bool chosen = (int)data.Policy == i;
            string caption = i == 0 ? Word("내 배당", "My dividend", "配当") : i == 1 ? Word("하루 이익", "Daily profit", "1日の利益") : Word("회사 현금", "Company cash", "会社の現金");
            decimal figure = i == 0 ? outlook.Mine : i == 1 ? outlook.ProfitGain : Math.Max(0m, outlook.Kept);
            string line = "<size=58%><color=#99A8B3>" + caption + "</color></size>\n+" + DeviceTerminalUI.Money(figure);
            if (!GameLocalizedText.Matches(policyNotes[i], line)) { GameLocalizedText.Set(policyNotes[i], line, true); }
            // 고른 정책은 밝은 테두리, 권하는 정책은 초록 테두리와 꼬리표. 바탕은 채우지 않아 숫자가 그대로 읽힌다.
            bool suggested = (int)best == i;
            TabletKitStyle.Flat(policies[i].image, chosen ? TabletKitStyle.Hover : TabletKitStyle.Panel);
            policyTitles[i].color = chosen ? TabletKitStyle.Accent : policies[i].interactable ? TabletKitStyle.Ink : Soft;
            policyNotes[i].color = chosen || suggested ? TabletKitStyle.Ink : Soft;
            TabletCall.Show(policyBorders[i], chosen || suggested, chosen ? TabletKitStyle.Accent : Good);
            string tag = chosen ? Word("적용 중", "Active", "適用中") : suggested ? Word("추천", "Best", "おすすめ") : "";
            if (policyTags[i].transform.parent.gameObject.activeSelf != (tag.Length > 0)) { policyTags[i].transform.parent.gameObject.SetActive(tag.Length > 0); }
            if (!GameLocalizedText.Matches(policyTags[i], tag)) { GameLocalizedText.Set(policyTags[i], tag, true); }
            policyTags[i].transform.parent.GetComponent<Image>().color = chosen ? TabletKitStyle.Accent : Good;
        }
        if (advice.gameObject.activeSelf) { advice.gameObject.SetActive(false); }
        acquire.interactable = small ? CorporateEconomy.Instance.SmallOffer(stock, tablet.account, out _, out _) && MarketDayController.Instance.Phase != MarketPhase.Closing
            : MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
        trade.interactable=stock.AcceptsOrders && MarketDayController.CanTrade;
        // 지금 할 일: 경영권이 없으면 지분 매입, 있으면 회사를 키우는 쪽.
        TabletCall.Set(acquire, !controls);
        TabletCall.Set(growthButton, controls && economy.CanManage(stock));
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
