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
    private static void Close(CompanyBusiness c,int day,decimal demand=1,decimal exportDemand=1)
    {
        c.growth.Complete(c,day,rules);
        c.BeginClose(day,2000,10000,c.growth.Demand(demand,rules),businessRules,c.growth.Efficiency(rules),c.growth.FixedSaving(rules),c.growth.ExportShare(rules),exportDemand);
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
            // 새 투자 네 가지와 기업 등급.
            CompanyBusiness Grown(CompanyProject type,int level)
            {
                var company=Create(); company.Cash+=100000000;
                for (int i=0;i<level;i++) { company.growth.Start(company,type,1+i*3,rules); company.growth.Complete(company,3+i*3,rules); }
                return company;
            }
            var plain=Create(); var brand=Grown(CompanyProject.Brand,2); Close(plain,20,.5m); Close(brand,20,.5m);
            Check(brand.growth.Brand==2&&brand.Demand>plain.Demand&&brand.Profit>plain.Profit,"brand softens a weak-demand day");
            var plainGood=Create(); var brandGood=Grown(CompanyProject.Brand,2); Close(plainGood,20,1.3m); Close(brandGood,20,1.3m);
            Check(brandGood.Revenue==plainGood.Revenue,"brand does nothing when demand is already above normal");
            var plainFixed=Create(); var automated=Grown(CompanyProject.Automation,3); Close(plainFixed,20); Close(automated,20);
            Check(automated.Revenue==plainFixed.Revenue&&plainFixed.Costs-automated.Costs==decimal.Floor(plainFixed.BaseRevenue*.075m),"automation lowers fixed costs by exactly 2.5 points per level");
            var home=Create(); var abroad=Grown(CompanyProject.Export,2); Close(home,20,.5m,1.4m); Close(abroad,20,.5m,1.4m);
            Check(abroad.ExportRevenue==decimal.Floor(abroad.BaseRevenue*.60m*1.4m)&&abroad.Revenue==home.Revenue+abroad.ExportRevenue&&abroad.Profit>home.Profit,"export adds a second revenue stream with its own demand");
            var common=Create(); var quality=Grown(CompanyProject.Quality,4); Close(common,20,1.4m); Close(quality,20,1.4m);
            Check(quality.growth.Multiple(rules)==32&&quality.growth.Appraisal-quality.growth.Contributions>common.growth.Appraisal-common.growth.Contributions,"quality raises how strongly profit above expectation lifts the appraisal");
            var small=Create(); small.Cash+=100000000;
            Check(small.growth.Rank(rules)==0&&small.growth.NextRankValue(rules)==30000000&&!small.growth.HasSecondSlot(rules),"a new business starts at the lowest rank with one project slot");
            Check(small.growth.Start(small,CompanyProject.Capacity,1,rules)&&!small.growth.Start(small,CompanyProject.Brand,1,rules),"the second slot is closed below the required rank");
            var mid=Create(); mid.Cash+=100000000; mid.growth.Contributions+=100000000; mid.growth.Revalue(rules);
            Check(mid.growth.Rank(rules)==2&&mid.growth.HasSecondSlot(rules),"a 120M business reaches the rank that opens the second slot");
            Check(mid.growth.Start(mid,CompanyProject.Capacity,1,rules)&&mid.growth.Start(mid,CompanyProject.Brand,1,rules)&&!mid.growth.Start(mid,CompanyProject.Export,1,rules),"two different projects run together, never three");
            Check(!mid.growth.Running(CompanyProject.Export)&&mid.growth.Running(CompanyProject.Brand)&&mid.growth.Complete(mid,3,rules)&&mid.growth.Capacity==1&&mid.growth.Brand==1&&mid.growth.CompletedProjects==2,"both slots complete and each raises its own level");
            var twice=Create(); twice.Cash+=100000000; twice.growth.Contributions+=100000000; twice.growth.Revalue(rules);
            Check(twice.growth.Start(twice,CompanyProject.Sales,1,rules)&&!twice.growth.Start(twice,CompanyProject.Sales,1,rules),"the same project cannot occupy both slots");
            // 최종 목표: 상장 + 확장 14건 + 마감 시가총액.
            var finale=Create(); finale.Cash+=1000000000; finale.growth.Contributions+=100000000; finale.growth.Revalue(rules);
            int finaleDay=1, finaleDone=0;
            while (finale.growth.CompletedProjects<rules.endingProjects && finaleDay<200)
            {
                finale.growth.Complete(finale,finaleDay,rules);
                foreach (CompanyProject type in Enum.GetValues(typeof(CompanyProject))) { finale.growth.Start(finale,type,finaleDay,rules); }
                finaleDone=finale.growth.CompletedProjects; finaleDay++;
            }
            Check(finaleDone==rules.endingProjects&&finale.growth.CompletedProjects==rules.endingProjects&&finaleDay-1>=15,"fourteen expansions take at least fourteen days even with both slots always busy (day "+(finaleDay-1)+")");
            finale.MarketCap=rules.endingMarketCap-1; Check(!finale.growth.EndingReached(finale,true,rules),"ending needs the closing market cap");
            finale.MarketCap=rules.endingMarketCap; Check(finale.growth.EndingReached(finale,true,rules)&&!finale.growth.EndingReached(finale,false,rules),"ending needs a listed company");
            var early=Create(); early.MarketCap=rules.endingMarketCap*10; Check(!early.growth.EndingReached(early,true,rules),"a pumped market cap alone does not end the game");
            // 출자는 사업 규모를 키운다.
            var funded=Create(); decimal unfunded=funded.BaseRevenue; funded.Cash+=180000000; funded.growth.Contributions+=180000000; funded.growth.AddCapital(funded,180000000,rules); funded.growth.Revalue(rules);
            Check(funded.ReferenceCapital==200000000&&funded.BaseRevenue==unfunded*10&&funded.growth.Appraisal==200000000,"funding ten times the capital makes a ten times larger business without a free valuation jump");
            // 창업 기업이 얼마나 걸려 목표에 닿는지 재 본다(수요는 평소 그대로, 재투자 정책, 두 칸을 쉬지 않고 돌림).
            foreach (long seed in new long[]{500000000,2000000000,5000000000})
            {
                var run=Create(); run.Policy=CompanyPolicy.Investment; run.Cash+=seed; run.growth.Contributions+=seed; run.growth.AddCapital(run,seed,rules); run.growth.Revalue(rules);
                int reach=0, fourteen=0;
                for (int day=1;day<=120&&reach==0;day++)
                {
                    run.growth.Complete(run,day,rules);
                    for (int pass=0;pass<2;pass++)
                    {
                        CompanyProject best=CompanyProject.Capacity; decimal low=decimal.MaxValue;
                        foreach (CompanyProject type in Enum.GetValues(typeof(CompanyProject)))
                        { if (run.growth.CanStart(run,type,day,rules)&&run.growth.ProjectCost(run,type,rules)<low) { low=run.growth.ProjectCost(run,type,rules); best=type; } }
                        if (low<decimal.MaxValue) { run.growth.Start(run,best,day,rules); }
                    }
                    run.BeginClose(day,2000,10000,run.growth.Demand(1,rules),businessRules,run.growth.Efficiency(rules),run.growth.FixedSaving(rules),run.growth.ExportShare(rules),1);
                    run.FinishClose(run.DividendBudget(businessRules),run.DividendBudget(businessRules),businessRules,(decimal)rules.founderProductivity,(decimal)rules.founderProductivityGain); run.growth.Close(run,rules);
                    if (fourteen==0&&run.growth.CompletedProjects>=rules.endingProjects) { fourteen=day; }
                    if (day%5==0) { Console.WriteLine("  seed "+seed.ToString("N0")+" day "+day+" appraisal "+run.growth.Appraisal.ToString("N0")+" profit "+run.Profit.ToString("N0")+" productivity "+run.Productivity.ToString("0.00")+" projects "+run.growth.CompletedProjects); }
                    if (run.growth.Appraisal>=rules.endingMarketCap&&fourteen>0) { reach=day; }
                }
                Console.WriteLine("PACE seed "+seed.ToString("N0")+" -> fourteen projects day "+fourteen+", appraisal past the goal day "+reach);
            }
            var options=new JsonSerializerOptions{IncludeFields=true};
            var restored=JsonSerializer.Deserialize<CompanyBusiness>(JsonSerializer.Serialize(c,options),options);
            Check(restored.growth.RequestedDay==5&&restored.growth.Appraisal==g.Appraisal&&restored.Cash==c.Cash,"growth and listing state round-trip with ledger");
            Console.WriteLine("PASS "+count+" checks"); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
