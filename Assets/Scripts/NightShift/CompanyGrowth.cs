using System;

public enum CompanyProject { Capacity, Sales, Research }

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
    }
    public int Version=1, Capacity, Sales, Research, Project=-1, CompleteDay, LastProjectDay, CompletedProjects;
    public int OperatingDays, ProfitableCloses, RequestedDay, ListedCloseDay, LastDecisionDay, Decisions;
    public bool FounderBusiness, ListingApproved;
    public decimal Capital, Contributions, Retained, Appraisal, StartingRevenue, ExpectedProfit, AverageProfit, PaidProject;
    /// <summary>회사 사건(뉴스 시나리오)이 사업 전망에 준 배율. 0은 기록 이전 저장이며 1로 취급한다.</summary>
    public decimal NewsPremium;
    public string LastDecision="";
    public static bool Valid(Rules r) => r!=null && r.version==1 && r.founderShares>=1 && r.founderValue>=r.founderShares && r.listingValue>0
        && r.maximumLevel>=1 && r.maximumLevel<=20 && r.projectDays>=1 && r.listingProfitableCloses>=1 && r.listingProjects>=1
        && Fraction(r.projectCostRatio) && r.projectCostRatio>0 && Fraction(r.levelCostScale) && Fraction(r.capacityGain)
        && Fraction(r.salesGain) && Fraction(r.researchSaving) && r.researchSaving*r.maximumLevel<.9
        && Fraction(r.publicIssueRatio) && r.publicIssueRatio>0 && Fraction(r.npcCashReserve) && r.earningsMultiple>=0 && r.earningsMultiple<=100;
    private static bool Fraction(double n) => !double.IsNaN(n) && !double.IsInfinity(n) && n>=0 && n<=1;
    public static CompanyGrowth Create(CompanyBusiness company,decimal value,bool founder,decimal expectedMargin=.25m)
    { return new CompanyGrowth { Capital=Math.Max(1,value), Appraisal=Math.Max(1,value), StartingRevenue=company.BaseRevenue,
        ExpectedProfit=company.BaseRevenue*expectedMargin, AverageProfit=company.BaseRevenue*expectedMargin, FounderBusiness=founder }; }
    public int Level(CompanyProject type) => type==CompanyProject.Capacity ? Capacity : type==CompanyProject.Sales ? Sales : Research;
    public decimal ProjectCost(CompanyBusiness company,CompanyProject type,Rules rules)
    { return decimal.Ceiling(company.ReferenceCapital*(decimal)rules.projectCostRatio*(1+Level(type)*(decimal)rules.levelCostScale)); }
    public bool Start(CompanyBusiness company,CompanyProject type,int day,Rules rules)
    {
        if (day<1 || Project>=0 || LastProjectDay==day || !Enum.IsDefined(typeof(CompanyProject),type) || Level(type)>=rules.maximumLevel || company.Debt>0)
        { return false; }
        decimal cost=ProjectCost(company,type,rules);
        if (company.Cash<cost) { return false; }
        company.Cash-=cost; PaidProject=cost; Project=(int)type; CompleteDay=day+rules.projectDays; LastProjectDay=day;
        return true;
    }
    public bool Complete(CompanyBusiness company,int day,Rules rules)
    {
        if (Project<0 || day<CompleteDay) { return false; }
        if (Project==0) { Capacity++; } else if (Project==1) { Sales++; } else { Research++; }
        CompletedProjects++; Project=-1; PaidProject=0;
        company.BaseRevenue=decimal.Floor(StartingRevenue*(1+Capacity*(decimal)rules.capacityGain));
        return true;
    }
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
        Appraisal=decimal.Floor(Math.Max(1,(Capital+Contributions+Retained+(AverageProfit-ExpectedProfit)*(decimal)rules.earningsMultiple)*premium));
    }
    public bool Eligible(CompanyBusiness company,Rules rules) => Appraisal>=rules.listingValue && ProfitableCloses>=rules.listingProfitableCloses
        && CompletedProjects>=rules.listingProjects && company.Debt==0;
    public bool Request(CompanyBusiness company,int day,Rules rules)
    {
        if (RequestedDay>0 || !Eligible(company,rules)) { return false; }
        RequestedDay=day; return true;
    }
}
