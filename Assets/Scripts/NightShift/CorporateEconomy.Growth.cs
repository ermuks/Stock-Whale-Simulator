using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CorporateEconomy
{
    public CompanyGrowth.Rules GrowthRules=new();
    private Stock founderCompany;
    /// <summary>장부에 있는 내 창업 기업. 아직 열지 않았어도(설립 전) 돌려준다.</summary>
    private Stock FounderRaw
    {
        get
        {
            if (founderCompany != null) { return founderCompany; }
            var owned = StockManager.Instance?.playerAccount?.OwnedCompany;
            return owned?.business?.growth?.FounderBusiness == true ? owned : null;
        }
    }
    /// <summary>이 종목이 아직 열지 않은(설립 전) 내 기업인가. 설립 전에는 사업을 하지 않는다: 매출도 비용도 결산도 없다.</summary>
    public bool Unfounded(Stock stock)
    {
        var career = PlayerProgression.Instance;
        return stock != null && career != null && !career.CompanyFounded && !career.Dreamt && stock == FounderRaw;
    }
    /// <summary>내 기업. 세우기 전(그리고 꿈에서 깬 뒤)에는 없다(null).</summary>
    public Stock PlayerCompany
    {
        get
        {
            // 설립 비용을 내기 전에는 회사가 장부에만 있고 열려 있지 않다. 그동안은 내 기업이 없는 것으로 다룬다.
            var career = PlayerProgression.Instance;
            return career != null && !career.CompanyFounded ? null : FounderRaw;
        }
    }
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
        foreach (var stock in new List<Stock>(Companies())) { EnsureRepresentative(stock); ObserveControl(stock); }
        // 비상장 소기업은 다른 계좌가 모두 만들어진 뒤에, 늘 같은 순서로 놓는다.
        EnsureSmallCompanies();
        foreach (var stock in Companies()) { if (IsSmall(stock)) { ObserveControl(stock); } }
    }
    private void EnsureFounder()
    {
        var manager=StockManager.Instance; var player=manager.playerAccount;
        var career=PlayerProgression.Instance;
        // 이미 사업을 해 온 기업이 있으면(이 기능이 생기기 전의 저장) 세운 것으로 친다. 한 번도 열지 않은 새 세계의 기업은 설립 비용을 내야 열린다.
        bool Worked(Stock stock) { var g=stock?.business?.growth; return stock!=null && (stock.IsListed || g!=null && (g.OperatingDays>0 || g.CompletedProjects>0 || g.Contributions>0)); }
        if (founderCompany?.business?.growth?.FounderBusiness==true) { if (Worked(founderCompany)) { career?.MarkCompanyFounded(true); } return; }
        var original=player.OwnedCompany;
        if (original!=null && player.GetOwnedVolume(original)>original.issuedShares*.5m) { founderCompany=original; if (Worked(original)) { career?.MarkCompanyFounded(true); } }
        else
        {
            // 이미 세운 기업이 있는 저장(이 기능이 생기기 전의 저장 포함)이면 그 기업을 그대로 잇는다.
            foreach (var existing in Companies())
            {
                if (existing.business?.growth?.FounderBusiness==true && !existing.IsDelisted && player.GetOwnedVolume(existing)>existing.issuedShares*.5m)
                { founderCompany=existing; existing.Owner=player; existing.OwnerId=player.OwnerId; player.OwnedCompany=existing; career?.MarkCompanyFounded(true); return; }
            }
            // 기업 설립은 해금 요소다. 설립 비용을 내기 전에는(그리고 꿈에서 깬 뒤에는) 내 기업이 없다.
            if (career!=null && !career.CompanyFounded) { return; }
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
        var player=StockManager.Instance?.playerAccount; var stock=FounderRaw;
        ReleaseSmallFromDream(player);
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
    /// <summary>
    /// 내 비상장기업을 세운다. 설립 비용을 내면 기업이 생기고, 회사의 첫 값어치(founderValue)를 뺀 나머지는 그 회사의 자본금으로 들어간다.
    /// 돈이 사라지는 것은 첫 값어치만큼이고(그만큼의 지분을 받는다), 나머지는 회사 안의 현금이 된다.
    /// </summary>
    public bool FoundCompany()
    {
        var career=PlayerProgression.Instance; var manager=StockManager.Instance; var payer=manager==null ? null : manager.playerAccount;
        if (career==null || payer==null || !manager.SetupConfirmed || MarketDayController.Instance==null) { GrowthResult="설립 불가"; return false; }
        if (PlayerCompany!=null) { GrowthResult="이미 기업이 있습니다"; return false; }
        if (career.Dreamt && FounderRaw==null) { /* 꿈에서 깬 뒤에도 돈을 모으면 다시 세울 수 있다. */ }
        if (MarketDayController.Instance.Phase==MarketPhase.Closing) { GrowthResult="마감 정산 중"; return false; }
        decimal cost=career.FoundingCost, seed=Math.Min(cost,GrowthRules.founderValue);
        if (PlayerAccounts.Available(payer)<cost) { GrowthResult="설립 자금 부족"; return false; }
        career.MarkCompanyFounded(true); EnsureFounder();
        var stock=founderCompany;
        if (stock==null) { career.MarkCompanyFounded(false); GrowthResult="설립 불가"; return false; }
        payer.cash-=seed;
        decimal capital=decimal.Floor(cost-seed);
        bool funded=capital<1 || FundCompany(stock,payer,capital);
        ObserveControl(stock);
        GrowthResult=funded ? "기업 설립" : "기업 설립 · 자본금 출자는 나중에";
        return true;
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
        if (accepted) { SteamAchievements.Unlock(SteamAchievements.Growth); }
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
        // 비상장 소기업의 주인은 스스로 상장하지 않는다. 상장은 삼킨 사람이 정한다.
        if (!stock.IsListed && !IsSmall(stock) && growth.Eligible(data,GrowthRules)) { growth.Request(data,day,GrowthRules); }
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
        QueueSmallListing(stock,data.LastDay);
    }
    public IEnumerator ProcessListings(int day)
    {
        var pending=new List<Stock>(StockManager.Instance.UnlistedStocks);
        foreach (var stock in pending)
        {
            var data=Ensure(stock); var growth=data.growth;
            if (growth.RequestedDay<1 || growth.RequestedDay>=day || !(growth.Eligible(data,GrowthRules) || SmallListingReady(stock)) || stock.IsDelisted || stock.IsDelisting) { continue; }
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
