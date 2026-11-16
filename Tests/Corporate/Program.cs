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
