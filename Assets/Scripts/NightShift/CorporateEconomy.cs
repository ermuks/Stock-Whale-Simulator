using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One company scan per second; settlement yields between companies and shareholder batches.</summary>
public sealed partial class CorporateEconomy : MonoBehaviour
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
    void Awake() { Instance = this; Rules = LoadRules(); GrowthRules=LoadGrowthRules(); }
    void OnEnable() { NewsManager.OnArticlePublished += News; GameLocale.Changed += ApplySmallNames; }
    void OnDisable() { NewsManager.OnArticlePublished -= News; GameLocale.Changed -= ApplySmallNames; }
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
        if (stock.business.growth==null)
        {
            stock.business.growth=CompanyGrowth.Create(stock.business,stock.realValue*stock.issuedShares,false,1-(decimal)Rules.variableCostRatio-(decimal)Rules.fixedCostRatio);
            stock.business.growth.ListedCloseDay=stock.IsListed ? stock.business.LastDay : 0;
        }
        return stock.business;
    }
    void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed || MarketDayController.Instance == null || !MarketDayController.Instance.Ready || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + 1;
        foreach (var stock in Companies()) { EnsureRepresentative(stock); ObserveControl(stock); }
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
            // 삼킨 회사가 늘었다. 인수 사다리의 진행과 시너지를 알린다.
            AnnounceLadder(stock);
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
        if (!CanManage(stock) || !Enum.IsDefined(typeof(CompanyPolicy),policy))
        { return false; }
        Ensure(stock).Policy = policy; return true;
    }
    public void ReconnectSession()
    {
        // 이름이 바뀐 회사: 예전 저장에 남은 이름을 새 이름으로 옮긴다.
        foreach (var renamed in Companies()) { renamed.stockName = StockManager.CurrentName(renamed.stockName); }
        foreach (var stock in Companies())
        {
            if (stock.business != null)
            { Ensure(stock); continue; }
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
        EnsureFounder(); ApplySmallNames();
    }
    public decimal PlayerDividends(int day)
    {
        decimal total = 0;
        foreach (var stock in Companies())
        { if (stock.business?.LastDay == day) { total += stock.business.PlayerDividend; } }
        return total;
    }
    public IEnumerator Settle(int day)
    {
        foreach (var stock in Companies())
        {
            ObserveControl(stock); var data = Ensure(stock);
            // 아직 열지 않은(설립 전) 내 기업은 사업을 하지 않는다.
            if (stock.IsDelisted || data.LastDay >= day || Unfounded(stock))
            { continue; }
            PrepareBusinessDay(stock,day);
            payees.Clear(); payments.Clear(); decimal shares = 0;
            foreach (var owner in Trader.Registry)
            {
                if (owner==null) { continue; }
                decimal owned=owner.GetOwnedVolume(stock);
                if (owned>0) { payees.Add(owner); shares+=owned; }
            }
            // Closing already validates the share ledger; do not mint missing-holder dividends.
            if (shares + ExchangeWarehouse.Instance.GetHolding(stock) != stock.issuedShares) { throw new InvalidOperationException("Corporate shareholder ledger mismatch: " + stock.symbol); }
            decimal demand=data.growth.Demand(data.DemandFor(day,stock.symbol,Rules),GrowthRules);
            decimal efficiency=data.growth.Efficiency(GrowthRules);
            if (!data.BeginClose(day, stock.currentPrice, stock.issuedShares, demand, Rules,efficiency,data.growth.FixedSaving(GrowthRules),
                data.growth.ExportShare(GrowthRules),data.ExportDemandFor(day,stock.symbol,Rules)))
            { continue; }
            decimal budget = data.DividendBudget(Rules), paid = 0, playerPaid = 0;
            foreach (var owner in payees)
            {
                decimal payment = CompanyBusiness.SharePayment(budget, owner.GetOwnedVolume(stock), stock.issuedShares);
                payments.Add(payment); paid += payment;
                if (PlayerAccounts.IsPlayer(owner)) { playerPaid += payment; }
            }
            data.FinishClose(paid, playerPaid, Rules, data.growth.FounderBusiness ? (decimal)GrowthRules.founderProductivity : 0m, data.growth.FounderBusiness ? (decimal)GrowthRules.founderProductivityGain * (1m + (decimal)LadderSynergy) : 0m);
            float payoutFrameStart=Time.realtimeSinceStartup;
            for (int i = 0; i < payees.Count; i++)
            {
                if (payments[i] > 0) { payees[i].ApplyTradeCash(payments[i]); }
                if (i % 64 == 63 && Time.realtimeSinceStartup-payoutFrameStart>=.002f)
                { yield return null; payoutFrameStart=Time.realtimeSinceStartup; }
            }
            // Retained earnings change business value; market quotes still come only from matching.
            FinishBusinessDay(stock);
            yield return null;
        }
        payees.Clear(); payments.Clear();
    }
}
