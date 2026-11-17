using System;

/// <summary>
/// 기업이 돈을 들여 하는 투자.
/// Capacity 설비 확장(매출), Sales 영업 확대(수요), Research 기술 연구(변동비),
/// Brand 브랜드 구축(수요가 나쁠 때 덜 꺾임), Automation 공정 자동화(고정비), Export 해외 진출(주기가 다른 두 번째 매출),
/// Quality 품질 관리(이익이 평가액에 반영되는 배수).
/// </summary>
public enum CompanyProject { Capacity, Sales, Research, Brand, Automation, Export, Quality }

/// <summary>Paid projects, earnings-backed private valuation, and public-listing eligibility.</summary>
[Serializable]
public sealed class CompanyGrowth
{
    [Serializable] public sealed class Rules
    {
        public int version=1, maximumLevel=5, projectDays=2, listingProfitableCloses=3, listingProjects=2;
        public long founderShares=10000, founderValue=20000000, listingValue=50000000;
        public double projectCostRatio=.05, levelCostScale=.35, capacityGain=.25, salesGain=.05, researchSaving=.04;
        public double earningsMultiple=20, publicIssueRatio=.25, npcCashReserve=.25;
        /// <summary>브랜드 한 단계마다, 수요가 평소(1.0)보다 낮을 때 모자란 폭이 이만큼 줄어든다.</summary>
        public double brandCushion=.12;
        /// <summary>자동화 한 단계마다 고정비 비율이 이만큼(매출 대비) 내려간다.</summary>
        public double automationSaving=.025;
        /// <summary>해외 진출 한 단계마다, 기본 매출의 이만큼에 해당하는 두 번째 매출이 생긴다. 수요 주기는 국내와 따로 돈다.</summary>
        public double exportShare=.30;
        /// <summary>품질 관리 한 단계마다 평가 배수(earningsMultiple)에 이만큼 더한다.</summary>
        public double qualityMultiple=3;
        /// <summary>기업 등급이 올라가는 평가액. 등급 0은 이 값들보다 작을 때다.</summary>
        public long[] rankValues={30000000,100000000,500000000,3000000000};
        /// <summary>이 등급부터 투자를 두 건 동시에 진행할 수 있다.</summary>
        public int secondSlotRank=2;
        /// <summary>
        /// 최종 목표(엔딩): 상장한 뒤, 사업 확장을 이만큼 끝내고, 마감 시가총액이 endingMarketCap 이상이면 달성이다.
        /// 확장 한 건이 projectDays일 걸리고 동시에 두 건까지라서, 14건이면 아무리 빨라도 14일(실제 약 4시간)이 든다.
        /// </summary>
        public int endingProjects=14;
        public long endingMarketCap=1000000000000;
        /// <summary>창업 기업(플레이어가 세운 회사)이 재투자로 올릴 수 있는 생산성의 한도. 다른 기업은 CompanyBusiness.Rules의 값을 쓴다.</summary>
        public double founderProductivity=1000;
        /// <summary>창업 기업은 재투자한 돈이 생산성으로 바뀌는 빠르기가 다르다. 이익이 클수록 더 빨리 커져서 성장이 갈수록 가팔라진다.</summary>
        public double founderProductivityGain=4.5;
    }
    public int Version=1, Capacity, Sales, Research, Project=-1, CompleteDay, LastProjectDay, CompletedProjects;
    public int Brand, Automation, Export, Quality;
    /// <summary>두 번째 투자 칸. 0이면 비어 있고, 진행 중이면 (투자 종류 + 1)이다. 예전 저장에는 이 값이 없어 0으로 읽힌다.</summary>
    public int SecondProject, SecondCompleteDay, SecondStartDay;
    public int OperatingDays, ProfitableCloses, RequestedDay, ListedCloseDay, LastDecisionDay, Decisions;
    public bool FounderBusiness, ListingApproved;
    /// <summary>최종 목표를 이룬 날(0이면 아직)과, 그 연출을 이미 보여 줬는지.</summary>
    public int EndingDay;
    public bool EndingShown;
    public decimal Capital, Contributions, Retained, Appraisal, StartingRevenue, ExpectedProfit, AverageProfit, PaidProject, SecondPaid;
    /// <summary>회사 사건(뉴스 시나리오)이 사업 전망에 준 배율. 0은 기록 이전 저장이며 1로 취급한다.</summary>
    public decimal NewsPremium;
    public string LastDecision="";
    public static bool Valid(Rules r) => r!=null && r.version==1 && r.founderShares>=1 && r.founderValue>=r.founderShares && r.listingValue>0
        && r.maximumLevel>=1 && r.maximumLevel<=20 && r.projectDays>=1 && r.listingProfitableCloses>=1 && r.listingProjects>=1
        && Fraction(r.projectCostRatio) && r.projectCostRatio>0 && Fraction(r.levelCostScale) && Fraction(r.capacityGain)
        && Fraction(r.salesGain) && Fraction(r.researchSaving) && r.researchSaving*r.maximumLevel<.9
        && Fraction(r.publicIssueRatio) && r.publicIssueRatio>0 && Fraction(r.npcCashReserve) && r.earningsMultiple>=0 && r.earningsMultiple<=100
        && Fraction(r.brandCushion) && r.brandCushion*r.maximumLevel<=1 && Fraction(r.automationSaving) && Fraction(r.exportShare)
        && !double.IsNaN(r.qualityMultiple) && r.qualityMultiple>=0 && r.qualityMultiple<=10 && r.rankValues!=null && r.secondSlotRank>=0
        && r.endingProjects>=1 && r.endingMarketCap>0 && r.founderProductivity>=1 && r.founderProductivity<=1000 && r.founderProductivityGain>0 && r.founderProductivityGain<=10;
    private static bool Fraction(double n) => !double.IsNaN(n) && !double.IsInfinity(n) && n>=0 && n<=1;
    public static CompanyGrowth Create(CompanyBusiness company,decimal value,bool founder,decimal expectedMargin=.25m)
    { return new CompanyGrowth { Capital=Math.Max(1,value), Appraisal=Math.Max(1,value), StartingRevenue=company.BaseRevenue,
        ExpectedProfit=company.BaseRevenue*expectedMargin, AverageProfit=company.BaseRevenue*expectedMargin, FounderBusiness=founder }; }
    public int Level(CompanyProject type) => type switch
    {
        CompanyProject.Capacity => Capacity, CompanyProject.Sales => Sales, CompanyProject.Research => Research, CompanyProject.Brand => Brand,
        CompanyProject.Automation => Automation, CompanyProject.Export => Export, CompanyProject.Quality => Quality, _ => 0
    };
    /// <summary>기업 등급(0부터). 평가액이 넘은 기준의 개수다.</summary>
    public int Rank(Rules rules)
    {
        int rank=0;
        foreach (long value in rules.rankValues) { if (Appraisal>=value) { rank++; } }
        return rank;
    }
    /// <summary>다음 등급의 기준 평가액. 최고 등급이면 0.</summary>
    public long NextRankValue(Rules rules)
    {
        foreach (long value in rules.rankValues) { if (Appraisal<value) { return value; } }
        return 0;
    }
    public bool HasSecondSlot(Rules rules) => Rank(rules)>=rules.secondSlotRank;
    /// <summary>지금 이 종류가 진행 중인가(어느 칸이든).</summary>
    public bool Running(CompanyProject type) => Project==(int)type || SecondProject==(int)type+1;
    public decimal ProjectCost(CompanyBusiness company,CompanyProject type,Rules rules)
    { return decimal.Ceiling(company.ReferenceCapital*(decimal)rules.projectCostRatio*(1+Level(type)*(decimal)rules.levelCostScale)); }
    /// <summary>지금 이 투자를 시작할 수 있는가. 돈은 따지지 않는다.</summary>
    public bool CanStart(CompanyBusiness company,CompanyProject type,int day,Rules rules)
    {
        if (day<1 || !Enum.IsDefined(typeof(CompanyProject),type) || Level(type)>=rules.maximumLevel || company.Debt>0 || Running(type)) { return false; }
        // 첫 칸은 하루에 한 번만 시작할 수 있다. 등급이 오르면 둘째 칸이 열려 다른 종류를 함께 진행한다.
        return Project<0 && LastProjectDay!=day || HasSecondSlot(rules) && SecondProject==0 && SecondStartDay!=day;
    }
    public bool Start(CompanyBusiness company,CompanyProject type,int day,Rules rules)
    {
        if (!CanStart(company,type,day,rules)) { return false; }
        decimal cost=ProjectCost(company,type,rules);
        if (company.Cash<cost) { return false; }
        company.Cash-=cost;
        if (Project<0 && LastProjectDay!=day) { PaidProject=cost; Project=(int)type; CompleteDay=day+rules.projectDays; LastProjectDay=day; }
        else { SecondPaid=cost; SecondProject=(int)type+1; SecondCompleteDay=day+rules.projectDays; SecondStartDay=day; }
        return true;
    }
    public bool Complete(CompanyBusiness company,int day,Rules rules)
    {
        bool done=false;
        if (Project>=0 && day>=CompleteDay) { Finish((CompanyProject)Project,company,rules); Project=-1; PaidProject=0; done=true; }
        if (SecondProject>0 && day>=SecondCompleteDay) { Finish((CompanyProject)(SecondProject-1),company,rules); SecondProject=0; SecondPaid=0; done=true; }
        return done;
    }
    private void Finish(CompanyProject type,CompanyBusiness company,Rules rules)
    {
        switch (type)
        {
            case CompanyProject.Capacity: Capacity++; break;
            case CompanyProject.Sales: Sales++; break;
            case CompanyProject.Research: Research++; break;
            case CompanyProject.Brand: Brand++; break;
            case CompanyProject.Automation: Automation++; break;
            case CompanyProject.Export: Export++; break;
            default: Quality++; break;
        }
        CompletedProjects++;
        company.BaseRevenue=decimal.Floor(StartingRevenue*(1+Capacity*(decimal)rules.capacityGain));
    }
    /// <summary>그날의 시장 수요에 브랜드와 영업의 효과를 입힌다. 브랜드는 수요가 평소보다 낮을 때만 모자란 폭을 줄인다.</summary>
    public decimal Demand(decimal market,Rules rules)
    {
        if (market<1m) { market=1m-(1m-market)*(1m-Math.Min(1m,Brand*(decimal)rules.brandCushion)); }
        return market+Sales*(decimal)rules.salesGain;
    }
    /// <summary>변동비에 곱하는 값(기술 연구).</summary>
    public decimal Efficiency(Rules rules) => 1-Research*(decimal)rules.researchSaving;
    /// <summary>고정비 비율에서 빼는 값(공정 자동화).</summary>
    public decimal FixedSaving(Rules rules) => Automation*(decimal)rules.automationSaving;
    /// <summary>기본 매출 대비 해외 매출의 크기(해외 진출).</summary>
    public decimal ExportShare(Rules rules) => Export*(decimal)rules.exportShare;
    /// <summary>이익이 평가액에 반영되는 배수(품질 관리로 오른다).</summary>
    public decimal Multiple(Rules rules) => (decimal)rules.earningsMultiple+Quality*(decimal)rules.qualityMultiple;
    public void Close(CompanyBusiness company,Rules rules)
    {
        OperatingDays++; if (company.Profit>0) { ProfitableCloses++; }
        Retained+=company.Profit-company.Dividend;
        AverageProfit=OperatingDays==1 ? company.Profit : AverageProfit*.75m+company.Profit*.25m;
        Revalue(rules);
    }
    public void Revalue(Rules rules)
    {
        decimal premium=NewsPremium>0 ? Math.Max(.2m,Math.Min(5m,NewsPremium)) : 1m;
        Appraisal=decimal.Floor(Math.Max(1,(Capital+Contributions+Retained+(AverageProfit-ExpectedProfit)*Multiple(rules))*premium));
    }
    public bool Eligible(CompanyBusiness company,Rules rules) => Appraisal>=rules.listingValue && ProfitableCloses>=rules.listingProfitableCloses
        && CompletedProjects>=rules.listingProjects && company.Debt==0;
    /// <summary>
    /// 회사에 새 돈이 들어와 사업 규모가 커진다(출자·공모). 기준 자본이 늘어난 비율만큼 기본 매출과 기대 이익도 함께 커진다.
    /// 현금과 평가액(Contributions)은 부르는 쪽에서 따로 더한다.
    /// </summary>
    public void AddCapital(CompanyBusiness company,decimal amount,Rules rules)
    {
        if (amount<=0 || company.ReferenceCapital<=0) { return; }
        decimal ratio=(company.ReferenceCapital+amount)/company.ReferenceCapital;
        company.ReferenceCapital+=amount; StartingRevenue=decimal.Floor(StartingRevenue*ratio);
        ExpectedProfit*=ratio; AverageProfit*=ratio;
        company.BaseRevenue=decimal.Floor(StartingRevenue*(1+Capacity*(decimal)rules.capacityGain));
    }
    /// <summary>최종 목표를 이뤘는가. 시가총액은 마감 때 적힌 값(종가 × 발행 주식)으로 본다.</summary>
    public bool EndingReached(CompanyBusiness company,bool listed,Rules rules) => listed && CompletedProjects>=rules.endingProjects && company.MarketCap>=rules.endingMarketCap;
    public bool Request(CompanyBusiness company,int day,Rules rules)
    {
        if (RequestedDay>0 || !Eligible(company,rules)) { return false; }
        RequestedDay=day; return true;
    }
}
