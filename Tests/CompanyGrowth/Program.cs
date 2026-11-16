using System;
using System.Globalization;
using System.Text.Json;

public static class CompanyGrowthTests
{
    private static int count;
    private static readonly CompanyBusiness.Rules businessRules=new();
    private static readonly CompanyGrowth.Rules rules=new();
    private static void Check(bool result,string message)
    { if (!result) { throw new Exception(message); } count++; Console.WriteLine("PASS "+message); }
    private static CompanyBusiness Create()
    {
        var company=CompanyBusiness.Create(2000,10000,"test",businessRules);
        company.growth=CompanyGrowth.Create(company,20000000,true); return company;
    }
    private static void Close(CompanyBusiness c,int day,decimal demand=1)
    {
        c.growth.Complete(c,day,rules);
        c.BeginClose(day,2000,10000,demand+c.growth.Sales*(decimal)rules.salesGain,businessRules,1-c.growth.Research*(decimal)rules.researchSaving);
        c.FinishClose(c.DividendBudget(businessRules),c.DividendBudget(businessRules),businessRules); c.growth.Close(c,rules);
    }
    public static int Main()
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try
        {
            Check(CompanyGrowth.Valid(rules),"default growth settings valid");
            var invalid=new CompanyGrowth.Rules{researchSaving=double.NaN}; Check(!CompanyGrowth.Valid(invalid),"nonfinite settings rejected");
            var c=Create(); var g=c.growth;
            Check(c.Cash==1600000&&g.Appraisal==20000000&&!g.Eligible(c,rules),"founder business begins with finite treasury and no IPO entitlement");
            Check(!g.Start(c,(CompanyProject)10,1,rules)&&!g.Start(c,CompanyProject.Capacity,0,rules),"invalid project and day rejected");
            decimal cash=c.Cash;
            Check(g.Start(c,CompanyProject.Capacity,1,rules)&&c.Cash==cash-1000000,"capacity project costs exactly one million company cash");
            Check(!g.Start(c,CompanyProject.Sales,1,rules)&&!g.Complete(c,2,rules),"one project at a time with actual two-day wait");
            Check(g.Appraisal==20000000&&c.BaseRevenue==1600000,"clicking investment cannot immediately raise valuation or revenue");
            Check(g.Complete(c,3,rules)&&c.BaseRevenue==2000000&&g.Capacity==1&&g.CompletedProjects==1,"completion raises production capacity by configured amount");
            Check(!g.Complete(c,3,rules)&&g.Capacity==1,"project completion is idempotent");
            Check(!g.Start(c,CompanyProject.Sales,3,rules),"insufficient treasury cannot start another project");
            c.Cash+=40000000; g.Contributions+=40000000; g.Revalue(rules);
            Check(g.Appraisal==60000000&&!g.Eligible(c,rules),"capital injection alone cannot qualify a business for IPO");
            Check(g.Start(c,CompanyProject.Sales,3,rules),"next funded project accepted");
            Close(c,3); Close(c,4); Close(c,5);
            Check(g.ProfitableCloses==3&&g.CompletedProjects==2&&g.Sales==1,"three genuine profits and two completed projects tracked");
            Check(g.Appraisal>60000000&&c.Revenue>c.BaseRevenue,"earnings and sales expansion increase appraised business value");
            Check(g.Eligible(c,rules)&&g.Request(c,5,rules)&&!g.Request(c,5,rules),"eligible listing request accepted once");
            c.Debt=1; Check(!g.Eligible(c,rules),"unpaid debt blocks listing"); c.Debt=0;
            var research=Create(); var baseline=Create(); research.Cash+=10000000;
            research.growth.Start(research,CompanyProject.Research,1,rules); Close(research,3); Close(baseline,3);
            Check(research.Costs<baseline.Costs&&research.Profit>baseline.Profit,"research reduces real operating costs");
            var loss=Create(); Close(loss,1,.25m);
            Check(loss.Profit<0&&loss.growth.ProfitableCloses==0&&loss.growth.Appraisal<20000000,"loss reduces appraisal and never counts as profitable close");
            var max=Create(); max.Policy=CompanyPolicy.Investment; max.Productivity=(decimal)businessRules.maximumProductivity;
            max.BeginClose(1,2000,10000,1,businessRules); decimal before=max.Cash; decimal dividend=max.DividendBudget(businessRules); max.FinishClose(dividend,dividend,businessRules);
            Check(max.Investment==0&&max.Cash==before-dividend,"maxed productive capital no longer consumes ineffective reinvestment cash");
            max.growth.Capacity=rules.maximumLevel; max.Cash=100000000;
            Check(!max.growth.Start(max,CompanyProject.Capacity,1,rules),"project level cap enforced");
            var options=new JsonSerializerOptions{IncludeFields=true};
            var restored=JsonSerializer.Deserialize<CompanyBusiness>(JsonSerializer.Serialize(c,options),options);
            Check(restored.growth.RequestedDay==5&&restored.growth.Appraisal==g.Appraisal&&restored.Cash==c.Cash,"growth and listing state round-trip with ledger");
            Console.WriteLine("PASS "+count+" checks"); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
