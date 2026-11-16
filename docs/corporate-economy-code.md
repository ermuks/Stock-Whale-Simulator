# 기업 결산·인수 변경 코드

신규 스크립트 전문과 작업 시작 시점 대비 변경 내용입니다. 기존의 다른 미커밋 작업을 비교 기준에 섞지 않았습니다.

## 신규: Assets/Scripts/NightShift/CompanyBusiness.cs

```csharp
using System;

public enum CompanyPolicy { Dividend, Investment, Reserve }

/// <summary>Business cash is separate from shareholder accounts. All settlements are whole won.</summary>
[Serializable]
public sealed class CompanyBusiness
{
    [Serializable] public sealed class Rules
    {
        public int version = 1, seed = 38173;
        public double dailyRevenueRate = .08, variableCostRatio = .55, fixedCostRatio = .20, initialCashRatio = .08;
        public double dividendPayout = .60, investmentPayout = .15, reservePayout = .15;
        public double dividendInvestment = .10, growthInvestment = .60, productivityGain = .5, maximumProductivity = 2;
        public string entryCompany = "NM";
        public long entryIssuedShares = 800000;
    }
    public CompanyPolicy Policy;
    public decimal ReferenceCapital, BaseRevenue, Cash, Debt, Productivity = 1;
    public decimal ClosePrice, CloseShares, MarketCap, Revenue, Costs, Profit, Dividend, Investment, PlayerDividend;
    public decimal OpeningCash, OpeningDebt, Demand = 1, NewsDemand, CumulativeProfit;
    public int LastDay, LastAcquisitionDay;
    public bool ControlInitialized, PlayerControls, AcquisitionAnnounced;
    public bool CloseFinished;
    public string Sector;

    public static bool Valid(Rules rules)
    {
        if (rules == null || rules.version != 1 || rules.entryIssuedShares < 1)
        { return false; }
        double[] fractions = { rules.dailyRevenueRate, rules.variableCostRatio, rules.fixedCostRatio, rules.initialCashRatio,
            rules.dividendPayout, rules.investmentPayout, rules.reservePayout, rules.dividendInvestment, rules.growthInvestment, rules.productivityGain };
        foreach (double value in fractions) { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1) { return false; } }
        return !double.IsNaN(rules.maximumProductivity) && rules.maximumProductivity >= 1 && rules.maximumProductivity <= 10
            && rules.dividendPayout + rules.dividendInvestment <= 1 && rules.investmentPayout + rules.growthInvestment <= 1;
    }

    public static CompanyBusiness Create(decimal price, decimal shares, string sector, Rules rules)
    {
        decimal capital = Math.Max(1, price * shares);
        return new CompanyBusiness { ReferenceCapital = capital, BaseRevenue = decimal.Floor(capital * (decimal)rules.dailyRevenueRate),
            Cash = decimal.Floor(capital * (decimal)rules.initialCashRatio), Sector = sector ?? "market", Policy = CompanyPolicy.Dividend };
    }
    public static uint Hash(string text)
    { uint value = 2166136261; foreach (char c in text) { value = unchecked((value ^ c) * 16777619); } return value; }
    public decimal DemandFor(int day, string symbol, Rules rules)
    {
        double phase = Hash(Sector ?? "market") % 628 / 100d;
        double cycle = Math.Sin(day * .31 + phase) * .42;
        double variation = (Hash(symbol + ":" + day + ":" + rules.seed) % 1001 / 1000d - .5) * .20;
        return Math.Max(.25m, Math.Min(1.65m, 1m + (decimal)(cycle + variation) + NewsDemand));
    }
    public bool BeginClose(int day, decimal price, decimal shares, decimal demand, Rules rules)
    {
        if (day <= LastDay || day < 1 || shares <= 0 || price < 0)
        { return false; }
        LastDay = day; CloseFinished = false; ClosePrice = price; CloseShares = shares; MarketCap = price * shares;
        OpeningCash = Cash; OpeningDebt = Debt; Dividend = Investment = PlayerDividend = 0;
        Demand = Math.Max(.25m, Math.Min(1.65m, demand));
        Revenue = decimal.Floor(BaseRevenue * Demand * Productivity);
        Costs = decimal.Ceiling(BaseRevenue * ((decimal)rules.variableCostRatio * Demand + (decimal)rules.fixedCostRatio) + Debt * .001m);
        Profit = Revenue - Costs; CumulativeProfit += Profit;
        decimal net = Cash + Profit - Debt;
        Cash = Math.Max(0, net); Debt = Math.Max(0, -net); NewsDemand *= .6m;
        return true;
    }
    public decimal DividendBudget(Rules rules)
    {
        double ratio = Policy == CompanyPolicy.Dividend ? rules.dividendPayout : Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        return Debt > 0 ? 0 : Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio));
    }
    public static decimal SharePayment(decimal budget, decimal shares, decimal issued)
    { return issued <= 0 ? 0 : decimal.Floor(Math.Max(0, budget) * Math.Max(0, Math.Min(shares, issued)) / issued); }
    public void FinishClose(decimal paid, decimal playerPaid, Rules rules)
    {
        if (LastDay == 0 || CloseFinished) { throw new InvalidOperationException("Company close already completed or not started."); }
        if (paid < 0 || paid > DividendBudget(rules) || playerPaid < 0 || playerPaid > paid) { throw new InvalidOperationException("Invalid corporate distribution."); }
        Dividend = paid; PlayerDividend = playerPaid; Cash -= paid;
        double ratio = Policy == CompanyPolicy.Investment ? rules.growthInvestment : Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        Investment = Debt > 0 ? 0 : Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio));
        Cash -= Investment;
        Productivity = Math.Min((decimal)rules.maximumProductivity, Productivity + Investment / ReferenceCapital * (decimal)rules.productivityGain);
        CloseFinished = true;
    }
}

```

## 신규: Assets/Scripts/NightShift/CorporateEconomy.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One company scan per second; settlement yields between companies and shareholder batches.</summary>
public sealed class CorporateEconomy : MonoBehaviour
{
    public static CorporateEconomy Instance { get; private set; }
    public static CompanyBusiness.Rules LoadRules()
    {
        var file = Resources.Load<TextAsset>("Progression/CompanyBusiness");
        var rules = file == null ? new CompanyBusiness.Rules() : JsonUtility.FromJson<CompanyBusiness.Rules>(file.text);
        if (!CompanyBusiness.Valid(rules)) { throw new InvalidOperationException("Invalid CompanyBusiness configuration."); }
        return rules;
    }
    public CompanyBusiness.Rules Rules = new();
    private float next;
    private readonly List<Trader> payees = new();
    private readonly List<decimal> payments = new();
    void Awake() { Instance = this; Rules = LoadRules(); }
    void OnEnable() { NewsManager.OnArticlePublished += News; }
    void OnDisable() { NewsManager.OnArticlePublished -= News; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
    public CompanyBusiness Ensure(Stock stock)
    {
        if (stock.business == null)
        {
            string sector = stock.symbol;
            var writing = NewsManager.Instance?.Writing;
            if (writing?.companies != null)
            { foreach (var profile in writing.companies) { if (profile.symbol == stock.symbol) { sector = profile.sector; break; } } }
            stock.business = CompanyBusiness.Create(stock.realValue, stock.issuedShares, sector, Rules);
        }
        return stock.business;
    }
    void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + 1;
        foreach (var stock in StockManager.Instance.stocks) { ObserveControl(stock); }
    }
    public void ObserveControl(Stock stock)
    {
        var data = Ensure(stock);
        bool controls = WorldDramaDirector.Instance.Controls(stock);
        if (!data.ControlInitialized) { data.ControlInitialized = true; data.PlayerControls = controls; return; }
        if (controls && !data.PlayerControls && !data.AcquisitionAnnounced)
        {
            data.AcquisitionAnnounced = true; data.LastAcquisitionDay = MarketDayController.Instance.Day;
            decimal owned = PlayerAccounts.Instance.Owned(stock);
            NewsManager.Instance?.PublishAcquisition(stock, owned / stock.issuedShares * 100m);
        }
        data.PlayerControls = controls;
    }
    private void News(NewsArticle article)
    {
        if (article.fabricated || article.correction || article.category == "corporate" || article.credibility < .8)
        { return; }
        var stocks = StockManager.Instance?.stocks;
        if (stocks == null)
        { return; }
        foreach (var stock in stocks)
        {
            if (stock.symbol != article.symbol)
            { continue; }
            var data = Ensure(stock); data.NewsDemand = Math.Max(-.35m, Math.Min(.35m, data.NewsDemand + (decimal)Math.Max(-1, Math.Min(1, article.sentiment)) * .04m)); break;
        }
    }
    public bool SetPolicy(Stock stock, CompanyPolicy policy)
    {
        if (stock == null || !stock.IsListed || stock.IsDelisted || !Enum.IsDefined(typeof(CompanyPolicy),policy) || !WorldDramaDirector.Instance.Controls(stock) || MarketDayController.Instance.Phase == MarketPhase.Closing)
        { return false; }
        Ensure(stock).Policy = policy; return true;
    }
    public void ReconnectSession()
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock.business != null)
            { continue; }
            // Pre-feature saves keep their actual issued shares and closing quote. Never back-pay old days.
            var data = Ensure(stock); var market = MarketDayController.Instance;
            if (market.ClosingPrices.TryGetValue(stock,out decimal price))
            {
                data.LastDay = DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
                data.ClosePrice = price; data.CloseShares = stock.issuedShares; data.MarketCap = price * stock.issuedShares;
            }
            data.ControlInitialized = true; data.PlayerControls = WorldDramaDirector.Instance.Controls(stock);
            data.AcquisitionAnnounced = data.PlayerControls;
        }
    }
    public decimal PlayerDividends(int day)
    {
        decimal total = 0;
        foreach (var stock in StockManager.Instance.stocks)
        { if (stock.business?.LastDay == day) { total += stock.business.PlayerDividend; } }
        return total;
    }
    public IEnumerator Settle(int day)
    {
        foreach (var stock in StockManager.Instance.stocks)
        {
            ObserveControl(stock); var data = Ensure(stock);
            if (stock.IsDelisted || data.LastDay >= day)
            { continue; }
            payees.Clear(); payments.Clear(); decimal shares = 0;
            foreach (var owner in Trader.Registry)
            { if (owner != null) { payees.Add(owner); shares += owner.GetOwnedVolume(stock); } }
            // Closing already validates the share ledger; do not mint missing-holder dividends.
            if (shares + ExchangeWarehouse.Instance.GetHolding(stock) != stock.issuedShares) { throw new InvalidOperationException("Corporate shareholder ledger mismatch: " + stock.symbol); }
            if (!data.BeginClose(day, stock.currentPrice, stock.issuedShares, data.DemandFor(day, stock.symbol, Rules), Rules))
            { continue; }
            decimal budget = data.DividendBudget(Rules), paid = 0, playerPaid = 0;
            foreach (var owner in payees)
            {
                decimal payment = CompanyBusiness.SharePayment(budget, owner.GetOwnedVolume(stock), stock.issuedShares);
                payments.Add(payment); paid += payment;
                if (PlayerAccounts.IsPlayer(owner)) { playerPaid += payment; }
            }
            data.FinishClose(paid, playerPaid, Rules);
            for (int i = 0; i < payees.Count; i++)
            {
                if (payments[i] > 0) { payees[i].ApplyTradeCash(payments[i]); }
                if (i % 64 == 63) { yield return null; }
            }
            // Retained earnings change business value; market quotes still come only from matching.
            stock.SetRealValue(Math.Max(1, stock.realValue + (data.Profit - paid) / stock.issuedShares));
            yield return null;
        }
        payees.Clear(); payments.Clear();
    }
}

```

## 신규: Assets/Scripts/NightShift/CompanyDashboardUI.cs

```csharp
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
    private readonly Button[] policies = new Button[3];
    private Button acquire;
    private Stock selected;
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
        Action("Previous Company", "‹", .78f,.035f,.075f,.07f,() => Select(-1));
        Action("Next Company", "›", .88f,.035f,.075f,.07f,() => Select(1));
        ownership = Label("Ownership", "", .52f,.11f,.435f,.045f,27); ownership.alignment = TextAlignmentOptions.MidlineRight;
        string[] captions = { "발행 주식 수", "마감 종가", "마감 시총", "내 지분", "매출", "운영비", "순이익", "내 배당금", "기업 현금", "미지급 비용", "재투자", "배당 비율" };
        for (int i = 0; i < captions.Length; i++)
        {
            float x = .04f + i % 4 * .235f, y = .21f + i / 4 * .16f;
            var label = Label(captions[i], captions[i], x,y,.215f,.043f,25); label.color = new Color32(153,168,179,255);
            values[i] = Label(captions[i] + " Value", "—", x,y+.052f,.215f,.065f,34);
            values[i].alignment = TextAlignmentOptions.MidlineRight; TradingOrderView.Numeric(values[i]);
            var divider = new GameObject("Image :: Company Divider",typeof(RectTransform),typeof(Image)); divider.transform.SetParent(transform,false);
            Place((RectTransform)divider.transform,x,y+.135f,.215f,.0015f); divider.GetComponent<Image>().color = new Color(1,1,1,.12f); divider.GetComponent<Image>().raycastTarget = false;
        }
        day = Label("Settlement Day", "", .04f,.685f,.91f,.04f,24);
        string[] names = { "배당", "재투자", "비축" };
        for (int i = 0; i < 3; i++)
        { int index = i; policies[i] = Action("Policy " + i,names[i],.04f+i*.315f,.755f,.285f,.075f,() => Policy(index)); }
        acquire = Action("Acquire Shares","지분 매입",.04f,.87f,.285f,.075f,Acquire);
        Action("Company News","관련 뉴스",.355f,.87f,.285f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Events));
        Action("Company Orders","거래",.67f,.87f,.285f,.075f,() => tablet.ShowPage(TabletAccountUI.Page.Orders));
        feedback = Label("Company Feedback","",.04f,.955f,.91f,.035f,23);
    }
    public void Refresh()
    {
        var stock = StockManager.Instance?.CurrentStock;
        if (stock == null || CorporateEconomy.Instance == null)
        { return; }
        if (selected != stock) { selected = stock; DeviceTerminalUI.Set(feedback, ""); }
        DeviceTerminalUI.Set(heading,stock.stockName); DeviceTerminalUI.Set(symbol,stock.symbol);
        var data = stock.business;
        if (data == null)
        {
            foreach (var value in values) { DeviceTerminalUI.Set(value,"—"); }
            foreach (var policy in policies) { policy.interactable=false; }
            acquire.interactable=false; DeviceTerminalUI.Set(ownership,"—"); DeviceTerminalUI.Set(day,"—"); return;
        }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal ratio = stock.issuedShares > 0 ? owned / stock.issuedShares * 100 : 0;
        DeviceTerminalUI.Set(ownership,ratio >= 100 ? "완전 소유" : ratio > 50 ? "경영권 확보" : "경영권 50% 초과");
        bool closed = data.CloseFinished, quoted = data.LastDay > 0;
        string Money(decimal n) => n.ToString("N0") + "원";
        var rules = CorporateEconomy.Instance.Rules;
        double payout = data.Policy == CompanyPolicy.Dividend ? rules.dividendPayout : data.Policy == CompanyPolicy.Investment ? rules.investmentPayout : rules.reservePayout;
        string[] row = { stock.issuedShares.ToString("N0")+"주", quoted ? Money(data.ClosePrice) : "—", quoted ? Money(data.MarketCap) : "—", ratio.ToString("0.00####")+"%",
            closed ? Money(data.Revenue) : "—", closed ? Money(data.Costs) : "—", closed ? Money(data.Profit) : "—", closed ? Money(data.PlayerDividend) : "—",
            Money(data.Cash), Money(data.Debt), closed ? Money(data.Investment) : "—", payout.ToString("0%") };
        for (int i = 0; i < values.Length; i++) { DeviceTerminalUI.Set(values[i],row[i]); }
        values[6].color = InvestmentDashboard.ProfitColor(data.Profit);
        DeviceTerminalUI.Set(day,closed ? "DAY " + data.LastDay + " · 17:00 결산" : "첫 기업 결산 전");
        for (int i = 0; i < policies.Length; i++)
        { policies[i].interactable = ratio > 50 && MarketDayController.Instance.Phase != MarketPhase.Closing; TabletControlStyle.Emphasize(policies[i],(int)data.Policy == i); }
        acquire.interactable = MarketDayController.CanTrade && stock.AcceptsOrders && ratio < 100 && PlayerAccounts.Available(tablet.account) >= stock.currentPrice;
    }
    void OnEnable() { next = 0; }
    void Update() { if (Time.unscaledTime < next) { return; } next = Time.unscaledTime + .5f; Refresh(); }
    private void Select(int delta)
    { var m = StockManager.Instance; if (m == null || m.stocks.Count == 0) { return; } m.SelectStock((m.stocks.IndexOf(m.CurrentStock)+delta+m.stocks.Count)%m.stocks.Count); next=0; }
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

```

## 신규: Assets/Editor/CorporateValidation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit bounded integration test. Fixtures and save files are isolated from the player session.</summary>
public static class CorporateValidation
{
    private static string output, language;
    private static bool hadLanguage, oldOptions, spawned, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, checks, errors, locale;
    private static float due;
    private static decimal closingCash, companyCash, closingCap;
    private static long acquisitionId;
    private static Stock stock;
    private static TabletAccountUI tablet;
    private static readonly string[] languages = { "ko", "en", "ja" };
    public static void Run()
    {
        output=Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Use the bounded Settings runner."); }
        Directory.CreateDirectory(output);
        hadLanguage=PlayerPrefs.HasKey("DonDon.Language"); language=PlayerPrefs.GetString("DonDon.Language");
        File.WriteAllText(Path.Combine(output,"language-before.txt"),(hadLanguage ? "1\n" : "0\n")+language);
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save(); DonDonLocalizationSetup.Import();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions=EditorSettings.enterPlayModeOptionsEnabled; oldFlags=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        began=EditorApplication.timeSinceStartup; EditorApplication.update+=Monitor; Application.logMessageReceived+=Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished)
        { return; }
        if (EditorApplication.timeSinceStartup-began>260) { Finish("Time budget exceeded"); return; }
        if (EditorApplication.isPlaying && !spawned) { spawned=true; new GameObject("Test :: Corporate Probe").AddComponent<CorporateProbe>(); }
    }
    private static void Log(string text,string stack,LogType type)
    { if (type==LogType.Error || type==LogType.Exception) { errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),text+"\n"+stack+"\n"); } }
    private static void Check(bool success,string message)
    { File.AppendAllText(Path.Combine(output,"checks.txt"),(success ? "PASS " : "FAIL ")+message+"\n"); if (!success) { throw new Exception(message); } checks++; }
    private static CompanyDashboardUI View => tablet.GetComponentsInChildren<CompanyDashboardUI>(true).Single();
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime<due)
        { return; }
        try
        {
            var day=MarketDayController.Instance;
            var menu=PauseMenuController.Instance;
            if (phase==0)
            {
                var setup=Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup?.businessName==null)
                { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase=1; due=Time.unscaledTime+2; return;
            }
            if (phase==1)
            {
                if (day==null || !day.Ready || day.Phase!=MarketPhase.Trading)
                { return; }
                Time.timeScale=1;
                stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock));
                tablet=Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(t=>!t.second);
                foreach (var bot in day.Bots) { bot.SuspendSession(); }
                while (stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                Check(stock.business!=null&&stock.business.ControlInitialized,"opening ownership baseline exists before first player order");
                Check(stock.issuedShares==800000,"fresh-game entry target has 800,000 issued shares");
                Check(!CorporateEconomy.Instance.SetPolicy(stock,CompanyPolicy.Investment),"no management rights without majority");
                var player=PlayerAccounts.Instance.primary; player.cash=stock.currentPrice*stock.issuedShares;
                int count=NewsManager.Instance.Articles.Count(a=>a.category=="corporate");
                player.BuyLimit(1,10,0,stock); CorporateEconomy.Instance.ObserveControl(stock);
                Check(!WorldDramaDirector.Instance.Controls(stock)&&NewsManager.Instance.Articles.Count(a=>a.category=="corporate")==count,"pending orders grant neither control nor acquisition news");
                while (stock.orderBook.HasOrders) { stock.orderBook.CancelAllBudgeted(stock,1000); }
                // Test counterparties post their actual shares via the existing matching path; no share minting.
                var sell=typeof(Trader).GetMethod("SellInternal",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach (var account in Trader.Registry.ToArray())
                { if (!PlayerAccounts.IsPlayer(account) && account.GetHolding(stock)>0) { sell.Invoke(account,new object[]{stock,stock.currentPrice,account.GetHolding(stock),0}); } }
                Check(WorldDramaDirector.Instance.AcquireShares(null,stock,player),"acquisition places a finite, cash-backed order in the existing book");
                Check(PlayerAccounts.Instance.Owned(stock)==400001&&WorldDramaDirector.Instance.Controls(stock),"only actual matched 400,001 shares establish majority");
                decimal price=stock.currentPrice; CorporateEconomy.Instance.ObserveControl(stock);
                var news=NewsManager.Instance.Articles.First(a=>a.category=="corporate");
                acquisitionId=news.id;
                Check(news.symbol=="NM"&&news.sentiment>0&&news.credibility>=.98&&!news.fabricated&&news.body.Length>100,"confirmed acquisition becomes a substantial bullish article");
                Check(stock.currentPrice==price,"acquisition headline never directly sets market price");
                CorporateEconomy.Instance.ObserveControl(stock);
                Check(NewsManager.Instance.Articles.Count(a=>a.category=="corporate")==count+1,"control polling cannot spam duplicate acquisition news");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"issued shares conserved through takeover fills");
                tablet.ShowPage(TabletAccountUI.Page.Management); View.Refresh();
                View.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Policy 1").onClick.Invoke();
                Check(stock.business.Policy==CompanyPolicy.Investment,"existing tablet policy button sets persistent investment policy");
                View.GetComponentsInChildren<Button>().Single(b=>b.name=="Button :: Policy 0").onClick.Invoke();
                DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                phase=10; due=Time.unscaledTime+8; return;
            }
            if (phase==10)
            {
                int readers=day.Bots.Count(b=>b.Social!=null && b.Social.Psychology.Companies.Read(stock.symbol).seen.Contains(acquisitionId));
                int bullish=day.Bots.Count(b=>b.Social!=null && b.Social.Psychology.Companies.Read(stock.symbol).seen.Contains(acquisitionId) && b.Social.Psychology.Companies.TradeScale(stock.symbol,true,SessionClock.Time)>1);
                Check(readers>0&&bullish>0,"existing news audience reads takeover and raises buy propensity: "+readers+" readers, "+bullish+" bullish");
                Time.timeScale=0; phase=2; due=Time.unscaledTime+.4f; return;
            }
            if (phase==2)
            {
                View.Refresh(); Audit(View.gameObject); Capture("before-first-close",1600,900);
                Check(View.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="—"),"unsettled figures distinguished from zero");
                closingCash=PlayerAccounts.Instance.primary.cash+PlayerAccounts.Instance.automated.cash;
                typeof(MarketDayController).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(day,day.tradingSeconds);
                Time.timeScale=1; phase=3; return;
            }
            if (phase==3)
            {
                if (menu.CurrentPage!=PauseMenuController.Page.Report)
                { return; }
                var data=stock.business; var report=DailyIncomeController.Instance.Ledger.Reports.Last();
                Check(data.LastDay==day.Day&&data.CloseFinished&&data.MarketCap==data.ClosePrice*data.CloseShares,"actual close settles business and snapshots capitalization");
                Check(data.PlayerDividend>0&&report.Dividends==CorporateEconomy.Instance.PlayerDividends(day.Day),"closing report includes shareholder dividends from all accounts");
                Check(PlayerAccounts.Instance.primary.cash+PlayerAccounts.Instance.automated.cash==closingCash+report.Dividends+report.Transfer,"player receives dividends exactly once, separately from base income");
                Check(SessionStore.Exists,"corporate close included in atomic end-of-day checkpoint");
                companyCash=data.Cash; closingCap=data.MarketCap; decimal playerCash=PlayerAccounts.Instance.primary.cash;
                string saved=SessionStore.Read(); data.Cash=1; data.Policy=CompanyPolicy.Reserve; data.MarketCap=1;
                SessionStore.Restore(saved); saved=null; stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                Check(stock.business.Cash==companyCash&&stock.business.MarketCap==closingCap&&stock.business.Policy==CompanyPolicy.Dividend,"real session restore retains treasury, cap and policy");
                var repeat=CorporateEconomy.Instance.Settle(day.Day); while (repeat.MoveNext()) { }
                Check(PlayerAccounts.Instance.primary.cash==playerCash&&stock.business.Cash==companyCash,"restored close cannot pay a second dividend");
                Check(StockIntegrityChecker.Instance.CheckStock(stock).isValid,"restored shareholder register conserved");
                menu.UI.Report(true); Capture("closing-report",1920,1080); Audit(menu.UI.Root);
                menu.UI.ReportCompanies(); Capture("company-statements",1600,900); Audit(menu.UI.Root);
                menu.ContinueAfterReport(); Time.timeScale=0;
                tablet.ShowPage(TabletAccountUI.Page.Management); DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                phase=4; due=Time.unscaledTime+.6f; return;
            }
            if (phase==4)
            {
                View.Refresh(); Canvas.ForceUpdateCanvases(); Audit(View.gameObject);
                Capture("company-"+GameLocale.Code,1600,900); Capture("company-"+GameLocale.Code,1920,1080);
                Check(GameLocale.MissingSources.Count==0,"all string table entries present: "+GameLocale.Code);
                string article=GameLocale.Text(NewsManager.Instance.Articles.First(a=>a.category=="corporate").body);
                File.WriteAllText(Path.Combine(output,"acquisition-"+GameLocale.Code+".txt"),article);
                if (++locale<languages.Length) { GameLocale.SetLanguage(languages[locale]); due=Time.unscaledTime+.5f; return; }
                decimal closing=stock.business.MarketCap; stock.currentPrice+=100;
                View.Refresh(); Check(stock.business.MarketCap==closing,"live price change leaves displayed last-close cap unchanged");
                phase=5; due=Time.unscaledTime+.2f; return;
            }
            if (phase==5)
            {
                GC.Collect(); LegacyFixture(); GC.Collect(); phase=6; due=Time.unscaledTime+.2f; return;
            }
            if (phase==6)
            {
                SessionStore.Restore(File.ReadAllText(Path.Combine(output,"legacy-session.json")));
                stock=StockManager.Instance.stocks.Single(s=>s.symbol=="NM");
                Check(stock.issuedShares==800000&&stock.business.CloseShares==800000&&stock.business.MarketCap==closingCap,"pre-business save keeps saved issuance and last closing cap");
                Check(!stock.business.CloseFinished&&stock.business.LastDay==day.Day,"legacy financials marked unavailable without back-paying prior days");
                Check(stock.business.Cash==decimal.Floor(stock.realValue*stock.issuedShares*(decimal)CorporateEconomy.Instance.Rules.initialCashRatio),"legacy business initialized from restored values, not fresh-scene values");
                decimal cash=PlayerAccounts.Instance.primary.cash;
                var again=CorporateEconomy.Instance.Settle(day.Day); while (again.MoveNext()) { }
                Check(cash==PlayerAccounts.Instance.primary.cash,"legacy closing checkpoint cannot gain retroactive dividends");
                Check(errors==0,"no runtime errors"); Finish(null);
            }
        }
        catch(Exception ex) { Finish(ex.ToString()); }
    }
    private static void LegacyFixture()
    {
        var envelope=JObject.Parse(SessionStore.Read()); var data=JObject.Parse((string)envelope["payload"]);
        foreach (JObject node in (JArray)data["nodes"])
        {
            if (node["fields"] is not JObject fields)
            { continue; }
            fields.Remove("Stock|business"); fields.Remove("DailyIncomeLedger+ClosingReport|dividends");
        }
        string payload=data.ToString(Formatting.None); envelope["payload"]=payload;
        using (var hash=SHA256.Create()) { envelope["sha256"]=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))); }
        File.WriteAllText(Path.Combine(output,"legacy-session.json"),envelope.ToString(Formatting.None));
    }
    private static void Audit(GameObject root)
    {
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>())
        { label.ForceMeshUpdate(); if (label.isTextTruncated || label.isTextOverflowing) { throw new Exception("Clipped: "+label.name+" / "+label.text+" / "+label.rectTransform.rect); } }
        Check(true,"active labels fit: "+root.name+" / "+GameLocale.Code);
    }
    private static void Capture(string name,int width,int height)
    {
        var camera=Camera.main; var overlays=new List<(Canvas canvas,Camera camera,float distance)>();
        var target=new RenderTexture(width,height,24); var prior=camera.targetTexture; var active=RenderTexture.active; Texture2D pixels=null;
        try
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode!=RenderMode.ScreenSpaceOverlay)
                { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+.1f;
            }
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            pixels=new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=prior; RenderTexture.active=active; if (pixels!=null) { Object.Destroy(pixels); } target.Release(); Object.Destroy(target);
            foreach (var item in overlays) { item.canvas.renderMode=RenderMode.ScreenSpaceOverlay; item.canvas.worldCamera=item.camera; item.canvas.planeDistance=item.distance; }
        }
    }
    private static void Finish(string failure)
    {
        if (finished) { return; } finished=true;
        File.WriteAllText(Path.Combine(output,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",language); } else { PlayerPrefs.DeleteKey("DonDon.Language"); } PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled=oldOptions; EditorSettings.enterPlayModeOptions=oldFlags;
        EditorApplication.update-=Monitor; Application.logMessageReceived-=Log; EditorApplication.Exit(failure==null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class CorporateProbe : MonoBehaviour
{
    void Awake() { Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=20; }
    void LateUpdate() { CorporateValidation.Step(); }
}

```

## 신규: Tests/Corporate/Program.cs

```csharp
using System;
using System.Text.Json;
using System.Globalization;

public static class CorporateTests
{
    private static int count;
    private static void Check(bool result,string message)
    { if (!result) { throw new Exception(message); } count++; Console.WriteLine("PASS " + message); }
    public static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var rules = new CompanyBusiness.Rules();
            Check(CompanyBusiness.Valid(rules),"production rules valid");
            rules.dividendPayout=double.NaN; Check(!CompanyBusiness.Valid(rules),"non-finite ratios rejected"); rules.dividendPayout=.60;
            var company=CompanyBusiness.Create(500,800000,"insurance",rules);
            Check(company.BeginClose(1,520,800000,1,rules)&&company.MarketCap==416000000,"closing cap equals actual close times issued shares");
            Check(company.Revenue==32000000&&company.Profit==8000000&&company.DividendBudget(rules)==4800000,"revenue, operating cost and distributable profit separated");
            decimal owned=400001, player=CompanyBusiness.SharePayment(company.DividendBudget(rules),owned,800000);
            decimal others=CompanyBusiness.SharePayment(company.DividendBudget(rules),800000-owned,800000);
            company.FinishClose(player+others,player,rules);
            Check(player==2400006&&company.Cash==34400000,"majority receives only its share; company retains working cash");
            Check(!company.BeginClose(1,999,800000,1,rules)&&company.MarketCap==416000000,"intraday prices and duplicate settlement cannot rewrite closing cap");
            bool duplicate=false; try { company.FinishClose(0,0,rules); } catch (InvalidOperationException) { duplicate=true; }
            Check(duplicate,"second distribution rejected");
            var grow=CompanyBusiness.Create(500,800000,"insurance",rules); grow.Policy=CompanyPolicy.Investment;
            grow.BeginClose(1,520,800000,1,rules); grow.FinishClose(grow.DividendBudget(rules),0,rules);
            Check(grow.Dividend==1200000&&grow.Investment==4800000&&grow.Productivity>company.Productivity,"growth policy trades current payout for productive capital");
            var bad=CompanyBusiness.Create(500,800000,"insurance",rules); bad.Cash=0;
            bad.BeginClose(1,600,800000,.25m,rules); bad.FinishClose(0,0,rules);
            Check(bad.Profit<0&&bad.Dividend==0&&bad.Debt>0&&bad.Cash==0,"rising quote cannot fund a loss-making company dividend");
            Check(CompanyBusiness.SharePayment(101,1,3)*3==99,"whole-won payout leaves rounding remainder in treasury");
            Check(CompanyBusiness.SharePayment(101,3,3)==101,"full ownership receives entire declared distribution");
            var options=new JsonSerializerOptions { IncludeFields=true };
            var restored=JsonSerializer.Deserialize<CompanyBusiness>(JsonSerializer.Serialize(grow,options),options);
            Check(restored.Policy==grow.Policy&&restored.Cash==grow.Cash&&restored.MarketCap==grow.MarketCap&&!restored.BeginClose(1,600,800000,1,rules),"serialized policy, treasury, cap and settlement day survive restore");
            decimal total=0;
            for (int day=2;day<=120;day++)
            {
                decimal cash=company.Cash, debt=company.Debt;
                if (!company.BeginClose(day,520+day,800000,company.DemandFor(day,"NM",rules),rules)) { throw new Exception("Missing daily close"); }
                decimal budget=company.DividendBudget(rules); player=CompanyBusiness.SharePayment(budget,400001,800000); others=CompanyBusiness.SharePayment(budget,399999,800000);
                company.FinishClose(player+others,player,rules); total+=player;
                if (company.Cash-company.Debt!=cash-debt+company.Profit-company.Dividend-company.Investment || company.Dividend>Math.Max(0,company.Profit))
                { throw new Exception("Daily conservation failed: "+day); }
            }
            Check(company.LastDay==120&&total>0,"120 daily settlements conserve cash and never distribute beyond earned profit");
            var ledger=new DailyIncomeLedger(); ledger.BeginDay(1);
            var report=ledger.CloseDay(1,250000,10000,50000000,50000000,2400006);
            Check(report.Dividends==2400006&&report.Tax==5&&report.Transfer==249995&&report.DayResult==2660001,"report includes dividend once; tax still only realized trading profit");
            var copy=new DailyIncomeLedger(); Check(copy.Restore(ledger.Capture(),1)&&copy.Reports[0].Dividends==2400006,"income save retains exact dividend");
            var legacy=ledger.Capture(); legacy.reports[0].dividends=null;
            Check(new DailyIncomeLedger().Restore(legacy,1),"pre-dividend report snapshots remain readable");
            Console.WriteLine("Checks: "+count); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

```

## 수정: Assets/Scripts/NightShift/CorporateControlUI.cs

```diff
--- original
+++ modified
@@ -26,8 +26,8 @@
         var current = StockManager.Instance.CurrentStock;
         if (WorldDramaDirector.Instance.Controls(current)) { controlled = current; }
         bool can = WorldDramaDirector.Instance.Controls(controlled);
-        management.interactable = investment.interactable = dividend.interactable = can && MarketDayController.CanTrade;
-        acquisition.interactable = can && current != controlled && MarketDayController.CanTrade;
+        management.interactable = investment.interactable = dividend.interactable = can && MarketDayController.Instance.Phase != MarketPhase.Closing;
+        acquisition.interactable = current != null && MarketDayController.CanTrade;
         string text = can ? controlled.stockName + " 경영권 확보 · 매입 대상 " + current.stockName : "경영권: 실제 지분 50% 초과 필요";
         if (!GameLocalizedText.Matches(status,text)) { GameLocalizedText.Set(status,text); }
     }
```

## 수정: Assets/Scripts/NightShift/DailyIncomeController.cs

```diff
--- original
+++ modified
@@ -121,7 +121,7 @@
             decimal realized = (PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit) - realizedAtOpen;
             decimal equity = account.Equity() + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
             decimal available = Math.Max(0m,account.cash - account.ReservedCash);
-            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity,available);
+            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity,available,CorporateEconomy.Instance?.PlayerDividends(day) ?? 0);
             if (report == null)
             { yield break; }
             // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.
```

## 수정: Assets/Scripts/NightShift/DailyIncomeLedger.cs

```diff
--- original
+++ modified
@@ -36,17 +36,18 @@
         public int version = 1;
         public int day, payingMembers;
         public long company, fees;
-        public string realized, assets;
+        public string realized, assets, dividends;
         public string tax, relief, taxPaid, priorTax, unpaidTax, cash;
         public long Deposit => company + fees;
         public decimal Realized => Amount(realized);
+        public decimal Dividends => Amount(dividends);
         public decimal Tax => Amount(tax);
         public decimal Relief => Amount(relief);
         public decimal TaxPaid => Amount(taxPaid);
         public decimal PriorTax => Amount(priorTax);
         public decimal UnpaidTax => Amount(unpaidTax);
         public decimal Transfer => Deposit + Relief - TaxPaid;
-        public decimal DayResult => Realized + Deposit + Relief - Tax;
+        public decimal DayResult => Realized + Deposit + Dividends + Relief - Tax;
         public decimal Assets => Amount(assets);
         public decimal Cash => Amount(cash);
     }
@@ -130,9 +131,9 @@
         if (fee > 0) { ticket.UnattributedFee = 0; Revision++; }
         return fee;
     }
-    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m)
-    {
-        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee)
+    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m, decimal dividends = 0m)
+    {
+        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee || dividends < 0)
         { return null; }
         decimal tax = decimal.Floor(Math.Max(0m,realized) * TaxRate);
         // Whole-won relief covers the entire first realized loss, including fractional acquisition cost.
@@ -141,7 +142,7 @@
         decimal remaining = UnpaidTax + tax - paid;
         decimal transfer = company + Escrow + relief - paid;
         var result = new ClosingReport { version = 2, day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
-            realized = Money(realized), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
+            realized = Money(realized), dividends = Money(dividends), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
             tax = Money(tax), relief = Money(relief), priorTax = Money(UnpaidTax), taxPaid = Money(paid), unpaidTax = Money(remaining) };
         if (relief > 0m) { ReliefDay = day; }
         UnpaidTax = remaining;
@@ -193,7 +194,7 @@
         foreach (int bot in performanceOrder) { history.Add(new PerformanceSnapshot { bot = bot, net = Money(performance[bot]) }); }
         saved.performance = history.ToArray(); saved.reports = reports.ConvertAll(Copy).ToArray(); return saved;
     }
-    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets,
+    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets, dividends = item.dividends,
         tax = item.tax, relief = item.relief, taxPaid = item.taxPaid, priorTax = item.priorTax, unpaidTax = item.unpaidTax, cash = item.cash };
     public bool CanRestore(Snapshot saved,int worldDay)
     {
@@ -221,6 +222,7 @@
             { return false; }
             if (item.version < 1 || item.version > 2)
             { return false; }
+            if (item.dividends != null && !Whole(item.dividends)) { return false; }
             if (item.version == 2)
             {
                 if (!Whole(item.tax) || !Whole(item.relief) || !Whole(item.taxPaid) || !Whole(item.priorTax) || !Whole(item.unpaidTax) || !Read(item.cash,out decimal cash) || cash < 0m ||
```

### 함수 단위 원본 / 수정본

#### CloseDay

원본

```csharp
    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee)
        { return null; }
        decimal tax = decimal.Floor(Math.Max(0m,realized) * TaxRate);
        // Whole-won relief covers the entire first realized loss, including fractional acquisition cost.
        decimal relief = realized < 0m && ReliefDay == 0 ? decimal.Ceiling(-realized) : 0m;
        decimal paid = Math.Min(UnpaidTax + tax, Math.Max(0m,decimal.Floor(availableCash + company + Escrow + relief)));
        decimal remaining = UnpaidTax + tax - paid;
        decimal transfer = company + Escrow + relief - paid;
        var result = new ClosingReport { version = 2, day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
            tax = Money(tax), relief = Money(relief), priorTax = Money(UnpaidTax), taxPaid = Money(paid), unpaidTax = Money(remaining) };
        if (relief > 0m) { ReliefDay = day; }
        UnpaidTax = remaining;
        foreach (var ticket in tickets.Values)
        {
            decimal net = ticket.GrossProfit - ticket.Fee;
            if (performance.TryGetValue(ticket.BotId,out decimal prior)) { performance[ticket.BotId] = (prior + net) / 2m; }
            else
            {
                if (performanceOrder.Count >= 2000) { performance.Remove(performanceOrder.Dequeue()); }
                performanceOrder.Enqueue(ticket.BotId); performance.Add(ticket.BotId,net);
            }
        }
        reports.Add(result);
        if (reports.Count > 32) { reports.RemoveAt(0); }
        SettledThrough = day; Escrow = 0; Revision++; return result;
    }
```

수정본

```csharp
    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m, decimal dividends = 0m)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee || dividends < 0)
        { return null; }
        decimal tax = decimal.Floor(Math.Max(0m,realized) * TaxRate);
        // Whole-won relief covers the entire first realized loss, including fractional acquisition cost.
        decimal relief = realized < 0m && ReliefDay == 0 ? decimal.Ceiling(-realized) : 0m;
        decimal paid = Math.Min(UnpaidTax + tax, Math.Max(0m,decimal.Floor(availableCash + company + Escrow + relief)));
        decimal remaining = UnpaidTax + tax - paid;
        decimal transfer = company + Escrow + relief - paid;
        var result = new ClosingReport { version = 2, day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), dividends = Money(dividends), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
            tax = Money(tax), relief = Money(relief), priorTax = Money(UnpaidTax), taxPaid = Money(paid), unpaidTax = Money(remaining) };
        if (relief > 0m) { ReliefDay = day; }
        UnpaidTax = remaining;
        foreach (var ticket in tickets.Values)
        {
            decimal net = ticket.GrossProfit - ticket.Fee;
            if (performance.TryGetValue(ticket.BotId,out decimal prior)) { performance[ticket.BotId] = (prior + net) / 2m; }
            else
            {
                if (performanceOrder.Count >= 2000) { performance.Remove(performanceOrder.Dequeue()); }
                performanceOrder.Enqueue(ticket.BotId); performance.Add(ticket.BotId,net);
            }
        }
        reports.Add(result);
        if (reports.Count > 32) { reports.RemoveAt(0); }
        SettledThrough = day; Escrow = 0; Revision++; return result;
    }
```

#### Copy

원본

```csharp
    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets,
        tax = item.tax, relief = item.relief, taxPaid = item.taxPaid, priorTax = item.priorTax, unpaidTax = item.unpaidTax, cash = item.cash };
```

수정본

```csharp
    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets, dividends = item.dividends,
        tax = item.tax, relief = item.relief, taxPaid = item.taxPaid, priorTax = item.priorTax, unpaidTax = item.unpaidTax, cash = item.cash };
```

#### CanRestore

원본

```csharp
    public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
        { return false; }
        if (saved.reliefDay < ReliefDay || saved.reliefDay < 0 || saved.reliefDay > saved.settled || saved.version == 1 && (saved.reliefDay != 0 || UnpaidTax != 0m) || saved.version == 2 && !Whole(saved.unpaidTax))
        { return false; }
        var seen = new HashSet<int>();
        foreach (var item in saved.tickets)
        {
            if (item == null || item.bot <= 0 || !seen.Add(item.bot) || item.fee < 0 || item.fee > MaximumFee || !item.access && item.fee != 0 || item.unattributed < 0 || item.unattributed > item.fee ||
                !Read(item.gross,out _) || !Read(item.cost,out decimal cost) || cost < 0 || !Read(item.assets,out decimal assets) || assets < 0)
            { return false; }
        }
        seen.Clear();
        foreach (var item in saved.performance)
        { if (item == null || item.bot <= 0 || !seen.Add(item.bot) || !Read(item.net,out _)) { return false; } }
        int previous = 0;
        foreach (var item in saved.reports)
        {
            if (item == null || item.day <= previous || item.day > saved.settled || item.company < 0 || item.company > MaximumFee || item.fees < 0 || item.fees > MaximumFee*2000L ||
                item.payingMembers < 0 || item.payingMembers > 2000 || !Read(item.realized,out _) || !Read(item.assets,out _))
            { return false; }
            if (item.version < 1 || item.version > 2)
            { return false; }
            if (item.version == 2)
            {
                if (!Whole(item.tax) || !Whole(item.relief) || !Whole(item.taxPaid) || !Whole(item.priorTax) || !Whole(item.unpaidTax) || !Read(item.cash,out decimal cash) || cash < 0m ||
                    item.Tax != decimal.Floor(Math.Max(0m,item.Realized)*TaxRate) || item.TaxPaid > item.PriorTax + item.Tax || item.UnpaidTax != item.PriorTax + item.Tax - item.TaxPaid ||
                    item.Relief > 0m && (saved.reliefDay != item.day || item.Realized >= 0m || item.Relief != decimal.Ceiling(-item.Realized)))
                { return false; }
            }
            previous = item.day;
        }
        if (saved.reports.Length > 0 && saved.reports[saved.reports.Length-1].version == 2 && saved.reports[saved.reports.Length-1].UnpaidTax != Amount(saved.unpaidTax))
        { return false; }
        return true;
    }
```

수정본

```csharp
    public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
        { return false; }
        if (saved.reliefDay < ReliefDay || saved.reliefDay < 0 || saved.reliefDay > saved.settled || saved.version == 1 && (saved.reliefDay != 0 || UnpaidTax != 0m) || saved.version == 2 && !Whole(saved.unpaidTax))
        { return false; }
        var seen = new HashSet<int>();
        foreach (var item in saved.tickets)
        {
            if (item == null || item.bot <= 0 || !seen.Add(item.bot) || item.fee < 0 || item.fee > MaximumFee || !item.access && item.fee != 0 || item.unattributed < 0 || item.unattributed > item.fee ||
                !Read(item.gross,out _) || !Read(item.cost,out decimal cost) || cost < 0 || !Read(item.assets,out decimal assets) || assets < 0)
            { return false; }
        }
        seen.Clear();
        foreach (var item in saved.performance)
        { if (item == null || item.bot <= 0 || !seen.Add(item.bot) || !Read(item.net,out _)) { return false; } }
        int previous = 0;
        foreach (var item in saved.reports)
        {
            if (item == null || item.day <= previous || item.day > saved.settled || item.company < 0 || item.company > MaximumFee || item.fees < 0 || item.fees > MaximumFee*2000L ||
                item.payingMembers < 0 || item.payingMembers > 2000 || !Read(item.realized,out _) || !Read(item.assets,out _))
            { return false; }
            if (item.version < 1 || item.version > 2)
            { return false; }
            if (item.dividends != null && !Whole(item.dividends)) { return false; }
            if (item.version == 2)
            {
                if (!Whole(item.tax) || !Whole(item.relief) || !Whole(item.taxPaid) || !Whole(item.priorTax) || !Whole(item.unpaidTax) || !Read(item.cash,out decimal cash) || cash < 0m ||
                    item.Tax != decimal.Floor(Math.Max(0m,item.Realized)*TaxRate) || item.TaxPaid > item.PriorTax + item.Tax || item.UnpaidTax != item.PriorTax + item.Tax - item.TaxPaid ||
                    item.Relief > 0m && (saved.reliefDay != item.day || item.Realized >= 0m || item.Relief != decimal.Ceiling(-item.Realized)))
                { return false; }
            }
            previous = item.day;
        }
        if (saved.reports.Length > 0 && saved.reports[saved.reports.Length-1].version == 2 && saved.reports[saved.reports.Length-1].UnpaidTax != Amount(saved.unpaidTax))
        { return false; }
        return true;
    }
```

## 수정: Assets/Scripts/NightShift/DailyIncomeUI.cs

```diff
--- original
+++ modified
@@ -9,7 +9,7 @@
     private TextMeshProUGUI currentFee, paidCount, escrow, result, day, company;
     private TextMeshProUGUI[] values;
     private TextMeshProUGUI[] history;
-    private TextMeshProUGUI transfer, closingAssets, dayResult, closingCash, reliefState;
+    private TextMeshProUGUI transfer, closingAssets, dayResult, closingCash, reliefState, dividends;
     private TMP_InputField feeInput;
     private GameObject panel;
     private TMP_FontAsset font;
@@ -82,7 +82,9 @@
             view.Label(root,captions[i]+" Label",captions[i],x,y,.165f,.06f,26).color = Muted;
             view.values[i] = view.Number(root,captions[i]+" Value",x+.17f,y,.245f,.06f,29);
         }
-        view.reliefState = view.Label(root,"Relief State","",.045f,.665f,.905f,.035f,21); view.reliefState.color = Muted;
+        view.reliefState = view.Label(root,"Relief State","",.045f,.665f,.45f,.035f,21); view.reliefState.color = Muted;
+        view.Label(root,"Dividend Label","기업 배당",.535f,.665f,.165f,.035f,21).color = Muted;
+        view.dividends = view.Number(root,"Company Dividend",.705f,.665f,.245f,.035f,24);
         view.Rule(root,.717f);
         view.Label(root,"Day Result Label","당일 순손익",.045f,.746f,.165f,.06f,27).color = Muted;
         view.dayResult = view.Number(root,"Day Result",.215f,.746f,.245f,.06f,30);
@@ -150,6 +152,7 @@
         DeviceTerminalUI.Set(transfer,last == null ? "—" : InvestmentDashboard.Signed(last.Transfer));
         DeviceTerminalUI.Set(closingAssets,last == null ? "—" : last.Assets.ToString("N0"));
         DeviceTerminalUI.Set(dayResult,last == null ? "—" : InvestmentDashboard.Signed(last.DayResult));
+        DeviceTerminalUI.Set(dividends,last == null ? "—" : last.Dividends.ToString("N0"));
         DeviceTerminalUI.Set(closingCash,last == null || last.version < 2 ? "—" : last.Cash.ToString("N0"));
         DeviceTerminalUI.Set(reliefState,ledger.ReliefDay == 0 ? "손실 구제 1회 남음" : "손실 구제 사용 · DAY " + ledger.ReliefDay);
         if (last != null)
```

## 수정: Assets/Scripts/MarketDay/MarketDayController.cs

```diff
--- original
+++ modified
@@ -220,6 +220,7 @@
             }
         }
 
+        foreach (var stock in StockManager.Instance.stocks) { CorporateEconomy.Instance?.ObserveControl(stock); }
         Ready = true;
         nextPublish = SessionClock.Time + publishMin;
     }
@@ -564,6 +565,15 @@
 
                 ClosingPrices[stock] = stock.currentPrice;
             }
+
+            foreach (var trader in closingTraders)
+            {
+                if (trader != null && (trader.ReservedCash != 0m || trader.EscrowVolume != 0m))
+                {
+                    IntegrityFailed = true; Debug.LogError("마감 예약 자산 잔존: " + trader.name); phaseWork = null; yield break;
+                }
+            }
+            if (CorporateEconomy.Instance != null) { yield return CorporateEconomy.Instance.Settle(Day); }
 
             for (int i = 0; i < closingTraders.Count; i++)
             {
```

## 수정: Assets/Scripts/News/NewsManager.cs

```diff
--- original
+++ modified
@@ -110,7 +110,7 @@
     { Publish(stock, criticalValue, title, newsId); }
 
     public NewsArticle Publish(Stock stock, float criticalValue, string title, long newsId = 0, bool fabricated = false,
-        string category = null, int variant = 0, long storyId = 0, bool correction = false)
+        string category = null, int variant = 0, long storyId = 0, bool correction = false, NewsArticle authored = null)
     {
         if (float.IsNaN(criticalValue) || float.IsInfinity(criticalValue) || criticalValue <= 0 || string.IsNullOrWhiteSpace(title))
         { return null; }
@@ -157,8 +157,9 @@
         NewsArticle article = null;
         if (stock != null)
         {
-            article = NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
+            article = authored ?? NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
                 DeviceMarketJournal.Stamp(), SessionClock.Time, fabricated, category, variant, storyId, correction);
+            if (authored != null) { article.id = newsId; article.storyId = newsId; }
             Articles.Insert(0, article);
             if (Articles.Count > 40) { Articles.RemoveAt(40); }
             ArticleVersion++;
@@ -168,6 +169,18 @@
         return article;
     }
 
+    public NewsArticle PublishAcquisition(Stock stock, decimal ownership)
+    {
+        string title = stock.stockName + " 경영권 인수 확정";
+        var article = new NewsArticle { symbol = stock.symbol, company = stock.stockName, title = title,
+            summary = "플레이어가 과반 지분을 확보했습니다. 새 경영 전략과 주주환원 확대에 대한 기대가 커지고 있습니다.",
+            body = "주주 원장에서 플레이어의 지분 " + ownership.ToString("0.00####") + "%가 확인됐습니다. 실제 체결이 완료된 지분을 기준으로 경영권이 이전됐습니다.\n\n" +
+                "새 지배주주는 배당·재투자·현금 비축 정책을 결정할 수 있습니다. 시장에서는 자본 배분 개선과 성장 투자 가능성을 주요 호재로 평가하고 있습니다.\n\n" +
+                "기업의 일일 배당은 장 마감 실적과 보유 현금에 따라 결정됩니다. 인수 자체가 이익이나 주가 상승을 보장하지는 않습니다.",
+            category = "corporate", categoryLabel = "기업 인수", source = "기업 공시", verification = "지분 확인",
+            time = DeviceMarketJournal.Stamp(), publishedAt = SessionClock.Time, sentiment = 1, credibility = .98 };
+        return Publish(stock, 1.30f, title, authored: article);
+    }
     void RefreshUI()
     {
         if (content == null || itemPrefab == null)
```

## 수정: Assets/Scripts/Settings/SessionStore.cs

```diff
--- original
+++ modified
@@ -25,7 +25,7 @@
     private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
     private static readonly HashSet<string> RootNames = new(StringComparer.Ordinal)
     {
-        "StockManager","StockStatistics","CandleHistoryManager","ExchangeWarehouse","MarketDayController","InvestorSociety",
+        "StockManager","StockStatistics","CandleHistoryManager","ExchangeWarehouse","MarketDayController","InvestorSociety","CorporateEconomy",
         "PlayerProgression","PlayerAccounts","DailyIncomeController","FakeNewsManager","NewsManager","ItCompanyManager",
         "StockNewsAttentionManager","StockInterestManager","MarketSentiment","DelistingManager","PsychologicalLevelManager",
         "LiquidityBotManager","TradeBotManager","InstitutionBootstrapper","WorldDramaDirector","MarketRhythmDirector",
@@ -129,6 +129,7 @@
         typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
         InvestorSociety.Instance?.ReconnectSession();
         var day=MarketDayController.Instance;
+        CorporateEconomy.Instance?.ReconnectSession();
         day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
         foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
         foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
@@ -300,6 +301,7 @@
             if(filled.Contains(index) || !filling.Add(index)) { return; }
             if(saved[index]["fields"] is JObject values)
             {
+                if (restored[index] is Stock stock && !values.ContainsKey("Stock|business")) { stock.business=null; }
                 foreach(var field in Fields(restored[index].GetType()))
                 { if(values.TryGetValue(FieldKey(field),out var token)) { field.SetValue(restored[index],Read(token,field.FieldType)); } }
             }
```

## 수정: Assets/Scripts/Stock.cs

```diff
--- original
+++ modified
@@ -45,6 +45,7 @@
     public decimal currentPrice;
     /// <summary>발행 주식 수. 거래소 창고가 초기에 전량 보유.</summary>
     public decimal issuedShares;
+    public CompanyBusiness business;
     /// <summary>진짜가치. itCompany/펀더멘털 등에서 갱신. UI는 OnRealOrFakeValueChanged 구독.</summary>
     public decimal realValue;
     /// <summary>가짜가치. 세력 조작·itCompany 등에서 갱신. UI는 OnRealOrFakeValueChanged 구독.</summary>
```

## 수정: Assets/Scripts/StockManager.Economy.cs

```diff
--- original
+++ modified
@@ -37,11 +37,15 @@
     public void BuildEconomyStocks()
     {
         initialRandom = new System.Random(economy.seed);
+        var businessRules = CorporateEconomy.LoadRules();
         foreach (var definition in economy.companies)
         {
             if (definition == null || string.IsNullOrWhiteSpace(definition.stockId) || stockById.ContainsKey(definition.stockId))
             { continue; }
 
+            // New-world entry target only. Restored Stock instances retain their saved issued shares.
+            if (definition.stockId == businessRules.entryCompany && businessRules.entryIssuedShares > 0)
+            { definition.issuedShares = businessRules.entryIssuedShares; }
             var stock = new Stock(definition.stockId, definition.name, PriceTickPolicy.RoundToTick(Math.Max(1m, definition.startPrice)), definition.issuedShares);
             stock.OwnerId = definition.ownerId;
             stock.IsListed = definition.initiallyListed;
```

## 수정: Assets/Scripts/Settings/SystemMenuUI.cs

```diff
--- original
+++ modified
@@ -18,10 +18,13 @@
     private readonly Sprite controlSprite;
     private readonly TextMeshProUGUI title, rest, reportDate, reportTotal, checkpointStatus, dialogText;
     private readonly TextMeshProUGUI[] reportValues;
+    private readonly List<TextMeshProUGUI[]> companyRows = new();
+    private readonly RectTransform companyContent;
     private readonly Button close, sleep, reportSleep, retrySave, confirm, cancel, continueSession;
     private readonly Button[] tabs;
     private readonly string[] categoryNames = { "그래픽","오디오","게임","UI" };
     private string category = "게임";
+    private bool reportSaved;
     private static readonly Color Ink = new(.92f,.94f,.96f), Muted = new(.63f,.67f,.71f);
     public SystemMenuUI(PauseMenuController controller)
     {
@@ -58,17 +61,26 @@
         cancel=Button(dialog,"Button :: Cancel Action","취소",615,350,290,52,owner.Back);
         var report=NewPage("report"); reportDate=Label(report,"",0,72,400,32,22);
         reportTotal=Label(report,"",0,120,560,80,48,FontStyles.Bold); Label(report,"당일 합계",0,202,520,30,21);
-        string[] rows={ "당일 실현손익","기본 수입","이용료 수입","손실 보전","당일 세금","납부 세금","미납 세금","총자산" };
+        string[] rows={ "당일 실현손익","기본 수입","이용료 수입","기업 배당","손실 보전","당일 세금","납부 세금","미납 세금","총자산" };
         reportValues=new TextMeshProUGUI[rows.Length];
         for (int i=0;i<rows.Length;i++)
         {
-            float x=i/4*640, y=270+i%4*56; Label(report,rows[i],x,y,240,44,22);
-            reportValues[i]=Label(report,"",x+235,y,325,44,25); reportValues[i].alignment=TextAlignmentOptions.MidlineRight; Line(report,x,y+49,560);
+            float x=i/5*640, y=250+i%5*50; Label(report,rows[i],x,y,240,40,22);
+            reportValues[i]=Label(report,"",x+235,y,325,40,25); reportValues[i].alignment=TextAlignmentOptions.MidlineRight; Line(report,x,y+44,560);
         }
         checkpointStatus=Label(report,"",0,520,1200,38,21);
         retrySave=Button(report,"Button :: Retry Save","다시 저장",0,586,300,52,owner.ReceiveClosingReport);
+        Button(report,"Button :: Company Statements","기업 결산",320,586,230,52,ReportCompanies);
         Button(report,"Button :: Prepare Night","준비 계속",575,586,300,52,owner.ContinueAfterReport);
         reportSleep=Button(report,"Button :: Report Sleep","08:00까지 취침",900,586,300,52,owner.Sleep,true);
+        var business=NewPage("companies");
+        Label(business,"기업",0,92,350,36,23); Label(business,"순이익",390,92,250,36,23).alignment=TextAlignmentOptions.MidlineRight;
+        Label(business,"내 배당금",650,92,250,36,23).alignment=TextAlignmentOptions.MidlineRight; Label(business,"기업 현금",910,92,290,36,23).alignment=TextAlignmentOptions.MidlineRight;
+        var viewport=Box(business,"Area :: Company Statements",Color.clear); Place(viewport,0,140,1200,410); viewport.GetComponent<Image>().raycastTarget=true;
+        viewport.gameObject.AddComponent<RectMask2D>(); var scroll=viewport.gameObject.AddComponent<ScrollRect>();
+        companyContent=Area(viewport,"Area :: Company Statement Rows",0,0,1200,410); companyContent.anchorMin=new Vector2(0,1); companyContent.anchorMax=new Vector2(1,1); companyContent.pivot=new Vector2(0,1); companyContent.sizeDelta=new Vector2(0,410);
+        scroll.viewport=viewport; scroll.content=companyContent; scroll.horizontal=false; scroll.vertical=true; scroll.scrollSensitivity=30;
+        Button(business,"Button :: Back To Closing","정산으로",900,586,300,52,()=>Report(reportSaved));
         var death=NewPage("death"); var deathText=Label(death,"4일 연속 밤샘",250,156,700,80,25); deathText.alignment=TextAlignmentOptions.Center;
         Button(death,"Button :: Restart Checkpoint","마지막 마감으로",390,310,420,52,()=> { if (SessionStore.Exists) { owner.LoadSession(); } else { owner.NewSession(); } },true);
         Root.SetActive(false);
@@ -119,18 +131,38 @@
     }
     public void Report(bool saved)
     {
+        reportSaved=saved;
         Show("report","장 마감 정산"); var reports=DailyIncomeController.Instance?.Ledger.Reports;
         if (reports==null || reports.Count==0) { return; }
         var report=reports[reports.Count-1]; GameLocalizedText.Set(reportDate,"DAY "+report.day+"  17:00",true);
         GameLocalizedText.Set(reportTotal,report.DayResult.ToString("+#,##0;-#,##0;0")+"원");
         reportTotal.color=report.DayResult>0 ? new Color(.94f,.36f,.4f) : report.DayResult<0 ? new Color(.3f,.59f,.91f) : Ink;
-        decimal[] values={report.Realized,report.company,report.fees,report.Relief,report.Tax,report.TaxPaid,report.UnpaidTax,report.Assets};
+        decimal[] values={report.Realized,report.company,report.fees,report.Dividends,report.Relief,report.Tax,report.TaxPaid,report.UnpaidTax,report.Assets};
         for (int i=0;i<values.Length;i++) { GameLocalizedText.Set(reportValues[i],values[i].ToString("N0")+"원"); }
         GameLocalizedText.Set(checkpointStatus,saved ? "마감 저장 완료" : "저장 실패 · 이전 마감 유지");
         retrySave.gameObject.SetActive(!saved);
         reportSleep.interactable=PlayerRestController.Instance!=null && PlayerRestController.Instance.CanSleep;
     }
     public void Death() { Show("death","과로로 사망했습니다"); }
+    public void ReportCompanies()
+    {
+        Show("companies","기업 결산"); int i=0;
+        foreach (var stock in StockManager.Instance.stocks)
+        {
+            var data=stock.business; if (data==null || !data.CloseFinished) { continue; }
+            if (i==companyRows.Count)
+            {
+                float y=i*64;
+                companyRows.Add(new[] { Label(companyContent,"",0,y,350,56,24), Label(companyContent,"",390,y,250,56,24), Label(companyContent,"",650,y,250,56,24), Label(companyContent,"",910,y,290,56,24) });
+                for (int col=1;col<4;col++) { companyRows[i][col].alignment=TextAlignmentOptions.MidlineRight; }
+            }
+            string[] row={stock.stockName,data.Profit.ToString("N0")+"원",data.PlayerDividend.ToString("N0")+"원",data.Cash.ToString("N0")+"원"};
+            for (int col=0;col<4;col++) { companyRows[i][col].gameObject.SetActive(true); GameLocalizedText.Set(companyRows[i][col],row[col]); }
+            i++;
+        }
+        for (int unused=i;unused<companyRows.Count;unused++) { foreach (var label in companyRows[unused]) { label.gameObject.SetActive(false); } }
+        companyContent.sizeDelta=new Vector2(0,Mathf.Max(410,i*64)); companyContent.anchoredPosition=Vector2.zero;
+    }
     public void Confirm(string heading,string message,Action accept,string caption)
     {
         Show("dialog",heading); GameLocalizedText.Set(dialogText,message); GameLocalizedText.Set(confirm.GetComponentInChildren<TextMeshProUGUI>(),caption);
```

## 수정: Assets/Scripts/NightShift/TabletAccountUI.cs

```diff
--- original
+++ modified
@@ -46,6 +46,7 @@
         TabletControlStyle.Apply(this);
         if (second) { SpreadTradingUI.Install(this); }
         else { DailyIncomeUI.InstallTablet(this); }
+        CompanyDashboardUI.Install(this);
         ShowPage(second ? Page.Automation : Page.Dashboard);
     }
     private void Unlock()
```

## 수정: Assets/Scripts/NightShift/WorldDramaDirector.cs

```diff
--- original
+++ modified
@@ -30,7 +30,7 @@
     public string Journal { get; private set; } = "기관과 고래의 움직임을 기록하는 중입니다.";
     public string BoardResult { get; private set; } = "과반 지분을 확보한 기업에서 경영 결정을 내릴 수 있습니다.";
     public int MemoryCount { get; private set; }
-    void Awake() { Instance = this; StockManager.OnStockListed += Subscribe; }
+    void Awake() { Instance = this; StockManager.OnStockListed += Subscribe; if (GetComponent<CorporateEconomy>() == null) { gameObject.AddComponent<CorporateEconomy>(); } }
     void Start()
     {
         if (StockManager.Instance == null)
@@ -166,8 +166,8 @@
     }
     public bool AcquireShares(Stock source, Stock target, Trader payer = null)
     {
-        if (!MarketDayController.CanTrade || !Controls(source) || target == null || target == source || !target.AcceptsOrders)
-        { BoardResult = "경영권을 가진 회사와 다른 매입 대상 종목을 선택하세요."; return false; }
+        if (!MarketDayController.CanTrade || target == null || !target.AcceptsOrders)
+        { BoardResult = "거래 가능한 종목을 선택하세요."; return false; }
         var player = payer != null ? payer : StockManager.Instance.playerAccount;
         target.orderBook.GetTopAsks(levels, 1);
         if (levels.Count == 0)
@@ -175,44 +175,30 @@
         decimal price = levels[0].price;
         if (price > target.currentPrice * 1.02m)
         { BoardResult = "매도 호가가 현재가에서 너무 멉니다."; return false; }
-        decimal shares = decimal.Floor(Math.Min(target.issuedShares * .005m, Math.Max(0m, player.cash - player.ReservedCash) * .1m / price));
+        decimal owned = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(target) : player.GetOwnedVolume(target);
+        decimal goal = owned > target.issuedShares * .5m ? target.issuedShares : decimal.Floor(target.issuedShares * .5m) + 1;
+        decimal pending = 0;
+        foreach (var account in Trader.Registry)
+        {
+            if (!PlayerAccounts.IsPlayer(account)) { continue; }
+            target.orderBook.CopyOwnerOrders(account, ownOrders);
+            foreach (var order in ownOrders) { if (order.isBuy) { pending += order.volume; } }
+        }
+        decimal shares = decimal.Floor(Math.Min(Math.Max(0, goal - owned - pending), PlayerAccounts.Available(player) / price));
         if (shares < 1m)
-        { BoardResult = "지분 매입에 쓸 가용 현금이 부족합니다."; return false; }
+        { BoardResult = "가용 현금 또는 추가 매입 수량이 없습니다."; return false; }
         player.BuyLimit(price, shares, 0, target);
         BoardResult = $"{target.stockName} {shares:N0}주 지분 매입 주문. 실제 체결분만 의결권에 반영됩니다.";
         return true;
     }
     public bool BoardDecision(Stock stock, int action, Trader payer = null)
     {
-        if (!MarketDayController.CanTrade || !Controls(stock))
+        if (action < 0 || action > 2 || !Controls(stock))
         { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
-        if (boardCooldown.TryGetValue(stock, out float next) && next > SessionClock.Time)
-        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - SessionClock.Time)}초"; return false; }
-        var player = payer != null ? payer : StockManager.Instance.playerAccount;
-        decimal cost = action == 2 ? stock.issuedShares : Math.Max(10000m, decimal.Floor(player.cash * .02m));
-        if (player.cash - player.ReservedCash < cost)
-        { BoardResult = "가용 현금이 부족합니다."; return false; }
-        // The action payment is transferred to an existing counterparty, never destroyed or minted.
-        if (action == 2)
-        {
-            decimal payout = 0m;
-            foreach (var holder in Trader.Registry) { payout += holder.GetOwnedVolume(stock); }
-            if (payout > cost)
-            { BoardResult = "주주 원장을 확인할 수 없습니다."; return false; }
-            player.ApplyTradeCash(-payout);
-            foreach (var holder in Trader.Registry) { holder.ApplyTradeCash(holder.GetOwnedVolume(stock)); }
-            cost = payout;
-        }
-        else { player.ApplyTradeCash(-cost); ExchangeBot.Instance.ApplyTradeCash(cost); }
-        boardActions.TryGetValue(stock, out int revision);
-        boardActions[stock] = ++revision; boardCooldown[stock] = SessionClock.Time + 120f;
-        float quality = Mathf.PerlinNoise(revision * .7f, stock.symbol.Length * .31f);
-        decimal effect = action == 0 ? .98m + (decimal)quality * .06m : action == 1 ? .95m + (decimal)quality * .12m : 1m;
-        stock.SetRealValue(Math.Max(1m, stock.realValue * effect));
-        string decision = action == 0 ? "경영진 교체" : action == 1 ? "신사업 투자" : "대주주 자비 특별배당 (주당 1원)";
-        BoardResult = decision + " 완료. 시장 평가는 참여자들의 판단에 달려 있습니다.";
-        Publish(stock, stock.stockName + ": 플레이어 주도 " + decision + ". 집행 비용 " + cost.ToString("N0") + "원.");
-        return true;
+        CompanyPolicy policy = action == 0 ? CompanyPolicy.Reserve : action == 1 ? CompanyPolicy.Investment : CompanyPolicy.Dividend;
+        bool changed = CorporateEconomy.Instance != null && CorporateEconomy.Instance.SetPolicy(stock, policy);
+        BoardResult = changed ? "경영 정책 저장" : "정산 중";
+        return changed;
     }
     private void Publish(Stock stock, string text)
     {
```
