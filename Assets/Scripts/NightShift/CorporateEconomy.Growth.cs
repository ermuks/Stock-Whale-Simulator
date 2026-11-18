using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CorporateEconomy
{
    public CompanyGrowth.Rules GrowthRules=new();
    private Stock founderCompany;
    public Stock PlayerCompany => founderCompany ?? StockManager.Instance?.playerAccount?.OwnedCompany;
    public string GrowthResult { get; private set; }="";
    public static CompanyGrowth.Rules LoadGrowthRules()
    {
        var file=Resources.Load<TextAsset>("Progression/CompanyGrowth");
        var rules=file==null ? new CompanyGrowth.Rules() : JsonUtility.FromJson<CompanyGrowth.Rules>(file.text);
        if (!CompanyGrowth.Valid(rules)) { throw new InvalidOperationException("Invalid CompanyGrowth configuration."); }
        return rules;
    }
    public IEnumerable<Stock> Companies()
    {
        foreach (var stock in StockManager.Instance.stocks) { yield return stock; }
        foreach (var stock in StockManager.Instance.UnlistedStocks) { yield return stock; }
    }
    public void InitializeCompanyWorld()
    {
        EnsureFounder();
        foreach (var stock in Companies()) { EnsureRepresentative(stock); ObserveControl(stock); }
    }
    private void EnsureFounder()
    {
        var manager=StockManager.Instance; var player=manager.playerAccount;
        // 꿈에서 깬 뒤에는 내 기업이 없다. 다시 만들어 주지 않는다.
        if (PlayerProgression.Instance!=null && PlayerProgression.Instance.Dreamt) { return; }
        if (founderCompany?.business?.growth?.FounderBusiness==true) { return; }
        var original=player.OwnedCompany;
        if (original!=null && player.GetOwnedVolume(original)>original.issuedShares*.5m) { founderCompany=original; }
        else
        {
            // Old retail saves had sold PLAYER to the exchange. Preserve those public shares and create the previously off-ledger private business.
            founderCompany=manager.RegisterPrivateCompany("PLAYER_BIZ",DailyIncomeController.Instance?.CompanyName ?? "내 기업",
                GrowthRules.founderValue/(decimal)GrowthRules.founderShares,GrowthRules.founderShares);
            founderCompany.Owner=player; founderCompany.OwnerId=player.OwnerId; player.OwnedCompany=founderCompany;
            if (player.GetOwnedVolume(founderCompany)==0) { player.GrantShares(founderCompany,founderCompany.issuedShares,founderCompany.currentPrice); }
            Ensure(founderCompany).LastDay=DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
        }
        Ensure(founderCompany).growth.FounderBusiness=true;
    }
    /// <summary>
    /// 꿈에서 깬다: 내 기업은 처음부터 남의 회사였다. 걸어 둔 주문을 거두고, 내 지분을 새 대표(세력 계좌)에게 모두 넘기고, 경영권을 뗀다.
    /// 회사와 다른 주주들의 주식은 그대로 남는다(발행 주식 수 장부가 맞아야 한다). 넘긴 뒤 내 기업은 없다.
    /// </summary>
    public void ReleaseFounderFromDream()
    {
        var player=StockManager.Instance?.playerAccount; var stock=PlayerCompany;
        founderCompany=null;
        if (player==null || stock==null) { return; }
        if (stock.business?.growth!=null) { stock.business.growth.FounderBusiness=false; }
        if (stock.Owner==player) { stock.Owner=null; stock.OwnerId=null; }
        if (player.OwnedCompany==stock) { player.OwnedCompany=null; }
        if (stock.IsDelisted) { return; }
        EnsureRepresentative(stock);
        stock.orderBook?.CancelAllOrdersByOwner(player,stock);
        decimal owned=player.GetHolding(stock);
        if (owned>0 && stock.Owner!=null && stock.Owner!=player) { player.ForceRemoveHolding(stock,owned); stock.Owner.GrantShares(stock,owned,stock.currentPrice); }
        ObserveControl(stock);
    }
    private void EnsureRepresentative(Stock stock)
    {
        if (stock.Owner!=null || stock.IsDelisted) { return; }
        var bot=TradeBot.Create<WhaleBot>("CEO_"+stock.symbol);
        if (bot==null) { return; }
        stock.Owner=bot; stock.OwnerId="CEO_"+stock.symbol; bot.BindCompany(stock,stock.OwnerId);
        bot.cash=0; bot.InitialCashAssigned=true;
    }
    public bool StartProject(Stock stock,CompanyProject type)
    {
        if (!CanManage(stock)) { GrowthResult="경영권 필요"; return false; }
        bool accepted=Ensure(stock).growth.Start(stock.business,type,MarketDayController.Instance.Day,GrowthRules);
        GrowthResult=accepted ? "투자 착수" : "진행 중·자금·단계 확인"; return accepted;
    }
    public bool CanManage(Stock stock) => stock!=null && !stock.IsDelisted && !stock.IsDelisting && StockManager.Instance.SetupConfirmed
        && WorldDramaDirector.Instance.Controls(stock) && MarketDayController.Instance.Phase!=MarketPhase.Closing;
    public bool FundCompany(Stock stock,Trader payer,decimal amount)
    {
        if (!CanManage(stock) || stock.IsListed || !PlayerAccounts.Instance.CanUse(payer) || payer.GetHolding(stock)!=stock.issuedShares)
        { GrowthResult="비상장 완전 소유 기업만"; return false; }
        if (amount<1 || amount!=decimal.Floor(amount) || PlayerAccounts.Available(payer)<amount)
        { GrowthResult="가용 현금 부족"; return false; }
        if (!payer.ContributePrivateCapital(stock,amount)) { GrowthResult="가용 현금 부족"; return false; }
        var data=Ensure(stock); data.Cash+=amount; data.growth.Contributions+=amount; data.growth.AddCapital(data,amount,GrowthRules);
        data.growth.Revalue(GrowthRules); ApplyValuation(stock); GrowthResult="출자 완료"; return true;
    }
    public bool RequestListing(Stock stock)
    {
        if (!CanManage(stock) || stock.IsListed) { GrowthResult="상장 신청 불가"; return false; }
        bool requested=Ensure(stock).growth.Request(stock.business,MarketDayController.Instance.Day,GrowthRules);
        GrowthResult=requested ? "다음 개장 심사" : "상장 요건 확인"; return requested;
    }
    private void ManageRepresentative(Stock stock,int day)
    {
        var data=Ensure(stock); var growth=data.growth;
        if (growth.LastDecisionDay>=day || stock.IsDelisted || WorldDramaDirector.Instance.Controls(stock)) { return; }
        growth.LastDecisionDay=day; growth.Decisions++;
        // 대표는 감정이 아니라 기질(성장 지향)과 사정(회사 현금·부채, 자기 계좌의 자금 압박)으로 경영한다.
        decimal growthBias=WorldDramaDirector.Trait(stock.Owner,"GROWTH"), pressure=WorldDramaDirector.Instance.Pressure(stock.Owner);
        decimal reserve=data.BaseRevenue*(decimal)GrowthRules.npcCashReserve;
        data.Policy=data.Debt>0 || data.Cash<reserve ? CompanyPolicy.Reserve : pressure>.3m ? CompanyPolicy.Dividend : growthBias>.45m ? CompanyPolicy.Investment : CompanyPolicy.Dividend;
        int preferred=(int)(CompanyBusiness.Hash(stock.symbol+":"+day)%3);
        if (pressure>.3m) { preferred=2; } else if (growthBias>.75m) { preferred=0; }
        for (int i=0;i<3 && growth.Project<0;i++)
        {
            var type=(CompanyProject)((preferred+i)%3);
            if (data.Cash-growth.ProjectCost(data,type,GrowthRules)<reserve) { continue; }
            if (growth.Start(data,type,day,GrowthRules)) { growth.LastDecision=ProjectName(type); break; }
        }
        if (!stock.IsListed && growth.Eligible(data,GrowthRules)) { growth.Request(data,day,GrowthRules); }
    }
    public static string ProjectName(CompanyProject type) => type switch
    {
        CompanyProject.Capacity => "설비 확장", CompanyProject.Sales => "영업 확대", CompanyProject.Research => "기술 연구", CompanyProject.Brand => "브랜드 구축",
        CompanyProject.Automation => "공정 자동화", CompanyProject.Export => "해외 진출", _ => "품질 관리"
    };
    /// <summary>기업 등급의 이름. 평가액이 커질수록 올라간다.</summary>
    public static string RankName(int rank) => rank<=0 ? "동네 가게" : rank==1 ? "소기업" : rank==2 ? "중소기업" : rank==3 ? "중견기업" : "대기업";
    /// <summary>회사 사건이 진짜 가치를 바꾼다. 마감 재평가에도 같은 배율이 유지되어 값이 튀지 않는다.</summary>
    public void ApplyCompanyEvent(Stock stock,decimal factor)
    {
        if (stock==null || stock.IsDelisted || factor<=0) { return; }
        var growth=Ensure(stock).growth;
        growth.NewsPremium=Math.Max(.2m,Math.Min(5m,factor));
        growth.Revalue(GrowthRules); ApplyValuation(stock);
    }
    private void ApplyValuation(Stock stock)
    {
        var growth=stock.business.growth;
        decimal value=growth.Appraisal/Math.Max(1,stock.issuedShares);
        stock.SetRealValue(Math.Max(1,value));
        if (!stock.IsListed) { stock.currentPrice=value; stock.SetFakeValue(value); }
    }
    private void PrepareBusinessDay(Stock stock,int day)
    {
        var data=Ensure(stock); var growth=data.growth;
        ManageRepresentative(stock,day);
        if (growth.Complete(data,day,GrowthRules))
        {
            growth.LastDecision="확장 완료";
            if (stock.IsListed) { PublishCompanyEvent(stock,"사업 확장 완료",.35,"투자가 완료되어 다음 실적에 반영됩니다."); }
        }
    }
    private void FinishBusinessDay(Stock stock)
    {
        var data=stock.business; data.growth.Close(data,GrowthRules);
        if (stock.IsListed) { data.growth.ListedCloseDay=data.LastDay; }
        // 내 기업이 최종 목표를 처음 이룬 날을 적어 둔다. 연출은 EndingOverlay가 맡는다.
        if (stock==PlayerCompany && data.growth.EndingDay==0 && data.growth.EndingReached(data,stock.IsListed,GrowthRules)) { data.growth.EndingDay=Math.Max(1,data.LastDay); }
        else { data.MarketCap=0; }
        ApplyValuation(stock);
    }
    public IEnumerator ProcessListings(int day)
    {
        var pending=new List<Stock>(StockManager.Instance.UnlistedStocks);
        foreach (var stock in pending)
        {
            var data=Ensure(stock); var growth=data.growth;
            if (growth.RequestedDay<1 || growth.RequestedDay>=day || !growth.Eligible(data,GrowthRules) || stock.IsDelisted || stock.IsDelisting) { continue; }
            decimal price=PriceTickPolicy.FloorToTick(Math.Max(1,growth.Appraisal/stock.issuedShares));
            decimal issue=Math.Max(1,decimal.Floor(stock.issuedShares*(decimal)GrowthRules.publicIssueRatio)), funding=issue*price;
            var underwriter=ExchangeBot.Instance;
            if (underwriter==null || underwriter.cash-underwriter.ReservedCash<funding) { continue; }
            // Primary issue: cash moves from the actual underwriter to the company; founder shares are not sold or duplicated.
            if (!underwriter.SubscribeShares(stock,issue,price)) { continue; }
            data.Cash+=funding; growth.Contributions+=funding; growth.AddCapital(data,funding,GrowthRules); stock.issuedShares+=issue;
            growth.Revalue(GrowthRules); stock.currentPrice=price; stock.orderBook=new OrderBook(price);
            growth.ListingApproved=true; stock.SetRealValue(growth.Appraisal/stock.issuedShares); stock.SetFakeValue(stock.realValue);
            if (StockManager.Instance.ListOwnerCompany(stock.symbol)==null) { throw new InvalidOperationException("Approved IPO failed: "+stock.symbol); }
            PublishCompanyEvent(stock,"신규 상장",.75,"상장 심사를 통과했습니다. 공모 자금은 기업에 유입되며 실제 호가에서 거래가 시작됩니다.");
            yield return null;
        }
    }
    private void PublishCompanyEvent(Stock stock,string headline,double sentiment,string body)
    {
        string title=stock.stockName+" · "+headline;
        var article=new NewsArticle { symbol=stock.symbol,company=stock.stockName,title=title,summary=body,body=body,
            category="corporate",categoryLabel="기업 공시",source="기업 공시",verification="공식 확인",time=DeviceMarketJournal.Stamp(),
            publishedAt=SessionClock.Time,sentiment=sentiment,credibility=.98 };
        NewsManager.Instance?.Publish(stock,1+(float)sentiment*.2f,title,authored:article);
    }
}
