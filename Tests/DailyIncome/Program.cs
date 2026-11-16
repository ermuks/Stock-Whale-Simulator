using System;
using System.Globalization;
using System.Text.Json;

public static class DailyIncomeTests
{
    private static int passed;
    private static void Check(bool success,string message)
    { if (!success) { throw new Exception(message); } passed++; Console.WriteLine("PASS " + message); }
    private static DailyIncomeLedger.Snapshot Clone(DailyIncomeLedger.Snapshot value)
    { var options = new JsonSerializerOptions { IncludeFields = true }; return JsonSerializer.Deserialize<DailyIncomeLedger.Snapshot>(JsonSerializer.Serialize(value,options),options); }
    public static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var rules = new DailyIncomeLedger.Rules();
            Check(DailyIncomeLedger.Valid(rules),"defaults valid");
            rules.cashFraction = double.NaN; Check(!DailyIncomeLedger.Valid(rules),"NaN settings rejected"); rules.cashFraction = .02;
            var ledger = new DailyIncomeLedger();
            Check(!ledger.Admit(1,100000,100000,true,out _)&&!ledger.BeginDay(0),"no admission or income before real day");
            Check(ledger.BeginDay(1)&&ledger.Fee==0,"first day free by default");
            Check(ledger.Admit(1,0,0,true,out long charge)&&charge==0,"free admission needs no cash");
            Check(ledger.ScheduleFee(10000)&&ledger.Fee==0&&ledger.NextFee==10000,"price change only schedules next day");
            Check(!ledger.BeginDay(2),"unsettled day cannot discard escrow");
            Check(ledger.CloseDay(1,250000,123,50000123,50000123).Deposit==250000,"base company income is exact fixed amount");
            Check(ledger.CloseDay(1,250000,123,50000123,50000123)==null,"closing cannot pay twice");
            Check(ledger.BeginDay(2)&&ledger.Fee==10000,"next-day price applied");
            Check(!ledger.Admit(2,9999,1000000,true,out _)&&ledger.Escrow==0,"reserved/insufficient available cash cannot pay");
            Check(!ledger.Admit(2,1000000,1000000,false,out _)&&ledger.Escrow==0,"price rejection has no cash/escrow effect");
            Check(ledger.Admit(2,10000,500000,true,out charge)&&charge==10000&&ledger.Escrow==10000,"ticket enters escrow once");
            Check(ledger.Admit(2,0,500000,false,out charge)&&charge==0,"same-day re-entry uses paid ticket even without remaining cash");
            Check(ledger.ScheduleFee(30000)&&ledger.Fee==10000,"editing price cannot reprice admitted members");
            Check(ledger.AttributeOutcome(2,1,100000,105000,out long fee)&&fee==10000,"real outcome consumes daily fee once");
            Check(105000m-(100000m+fee)==-5000,"positive trade becomes loss after actual fee");
            Check(!ledger.AttributeOutcome(2,1,100000,105000,out _),"duplicate outcome ignored");
            Check(ledger.AttributeOutcome(2,2,100000,120000,out fee)&&fee==0,"later trade does not deduct fee again");
            Check(ledger.TakeUnattributedFee(2)==0,"closing does not assess an already allocated fee again");
            var snap = Clone(ledger.Capture()); var loaded = new DailyIncomeLedger();
            Check(loaded.Restore(snap,2)&&loaded.Escrow==10000&&loaded.HasAccess(2),"snapshot preserves escrow and admission");
            Check(loaded.Admit(2,0,0,true,out charge)&&charge==0,"restoring does not rebill");
            var report = loaded.CloseDay(2,250000,2000,50002000,50002000);
            Check(report.fees==10000&&report.company==250000&&report.realized=="2000","statement separates fee income and trading profit");
            Check(loaded.RecentNet(2)==15000,"renewal performance uses gross outcomes minus one fee");
            Check(!loaded.Restore(snap,2)&&loaded.Escrow==0,"stale snapshot cannot restore already paid escrow");
            var closed = Clone(loaded.Capture()); var restored = new DailyIncomeLedger();
            Check(restored.Restore(closed,2)&&restored.CloseDay(2,250000,0,0)==null,"closed snapshot cannot repay company or fee income");
            Check(!new DailyIncomeLedger().Restore(closed,1),"snapshot from a future game day rejected");
            var corrupt = Clone(closed); corrupt.nextFee=-1; Check(!new DailyIncomeLedger().Restore(corrupt,2),"negative fee snapshot rejected");
            corrupt=Clone(closed); corrupt.tickets[0].unattributed=corrupt.tickets[0].fee+1; Check(!new DailyIncomeLedger().Restore(corrupt,2),"over-allocated fee snapshot rejected");
            corrupt=Clone(closed); corrupt.tickets=new[]{corrupt.tickets[0],corrupt.tickets[0]}; Check(!new DailyIncomeLedger().Restore(corrupt,2),"duplicate ticket snapshot rejected");
            corrupt=Clone(closed); corrupt.reports[0].fees=long.MaxValue; Check(!new DailyIncomeLedger().Restore(corrupt,2),"overflowed fee report rejected");
            Check(!ledger.ScheduleFee(-1)&&!ledger.ScheduleFee(DailyIncomeLedger.MaximumFee+1),"invalid fee input rejected");
            Check(loaded.BeginDay(3)&&loaded.Fee==30000&&!loaded.HasAccess(2),"new day requires renewal at new price");
            Check(loaded.Admit(2,100000,1000000,true,out charge)&&charge==30000,"renewing day charges once");
            Check(loaded.TakeUnattributedFee(2)==30000&&loaded.TakeUnattributedFee(2)==0,"unused fee assessment is idempotent");
            loaded.CloseDay(3,250000,0,0); Check(loaded.RecentNet(2)<0,"paying without realized gains reduces assessed performance");

            decimal Will(float trust,float betrayal,float fear,decimal net=0) => DailyIncomeLedger.Willingness(rules,17,1,10000000,10000000,net,trust,betrayal,70,fear,.6f);
            Check(Will(90,0,0)>Will(10,0,0),"trust increases price tolerance");
            Check(Will(90,90,0)<Will(90,0,0),"betrayal independently lowers price tolerance");
            Check(Will(90,0,90)<Will(90,0,0),"fear lowers price tolerance");
            Check(Will(190,0,0)==Will(100,0,0)&&Will(90,190,0)==Will(90,100,0),"overheated trust/betrayal clipped only for purchase formula");
            Check(Will(90,0,0,10000)>Will(90,0,0,-10000),"net experience changes willingness");
            Check(Will(90,0,0)==Will(90,0,0),"repeated invitation does not reroll willingness");
            Check(DailyIncomeLedger.Willingness(rules,17,1,100,100000000,10000000,100,0,80,0,.8f)<=2,"illiquid rich bot cannot spend reserved assets on fee");
            int cheap=0,medium=0,expensive=0;
            for(int id=1;id<=1000;id++)
            {
                decimal cash=1000000+id*50000;
                decimal value=DailyIncomeLedger.Willingness(rules,id,1,cash,cash,0,id%101,(id*7)%101,50+id%31,id%75,.2f+(id%7)*.1f);
                if(value>=1000) { cheap++; } if(value>=10000) { medium++; } if(value>=100000) { expensive++; }
            }
            Check(cheap>medium&&medium>expensive&&medium>0,"mixed population declines increasingly expensive rooms");
            Console.WriteLine($"Population 1000: fee 1,000={cheap}; fee 10,000={medium}; fee 100,000={expensive}");
            var idle = new DailyIncomeLedger(); long income=0;
            for(int day=1;day<=200;day++)
            { Check(idle.BeginDay(day),"idle day "+day); income+=idle.CloseDay(day,250000,0,50000000+income,50000000+income).Deposit; }
            Check(income==50000000&&idle.Reports.Count==32&&idle.Reports[31].Assets==100000000,"200 idle closes pay fixed 50m, retain bounded history and report exact net assets");
            Check(idle.BeginDay(250)&&idle.CloseDay(250,250000,0,0).Deposit==250000,"skipped/offline days have no catch-up payments");
            var busy=new DailyIncomeLedger(); busy.ScheduleFee(10000); busy.BeginDay(1); decimal wallets=2000m*1000000, companyWallet=50000000;
            for(int id=1;id<=2000;id++) { busy.Admit(id,1000000,1000000,true,out charge); wallets-=charge; }
            Check(wallets+busy.Escrow+companyWallet==2050000000,"cash plus escrow conserved for 2000 paid members");
            companyWallet+=busy.CloseDay(1,250000,0,companyWallet,companyWallet).Transfer;
            Check(wallets+busy.Escrow+companyWallet==2050250000,"only fixed external company income increases total currency");
            busy.BeginDay(2); busy.Admit(2001,1000000,1000000,true,out _); busy.CloseDay(2,250000,0,0);
            var turnover=busy.Capture();
            Check(turnover.performance.Length==2000&&turnover.performance[0].bot==2&&busy.RecentNet(2001)==-10000,"departed bot churn retains at most 2000 fee performance records");
            var churn=new DailyIncomeLedger();
            Check(churn.Restore(Clone(turnover),2)&&churn.RecentNet(2001)==-10000,"bounded performance remains restorable after bot turnover");
            var taxBook=new DailyIncomeLedger(); taxBook.ScheduleFee(30000); taxBook.BeginDay(1); taxBook.Admit(1,1000000,1000000,true,out _);
            var profitDay=taxBook.CloseDay(1,250000,1000000,50000000,1000000);
            Check(profitDay.Tax==500&&profitDay.TaxPaid==500&&profitDay.Relief==0,"0.05 percent tax applies only to realized trading profit");
            Check(profitDay.Transfer==279500&&profitDay.DayResult==1279500&&profitDay.Assets==50279500,"statement separates already-realized profit from settlement cash flow");
            Check(profitDay.Cash==1279500&&taxBook.ReliefDay==0,"profit day leaves one-time loss relief unused");
            taxBook.BeginDay(2); var lossDay=taxBook.CloseDay(2,250000,-1000000,49279500,1000000);
            Check(lossDay.Tax==0&&lossDay.Relief==1000000&&lossDay.Transfer==1250000&&taxBook.ReliefDay==2,"first loss day is fully reimbursed once and exempt from profit tax");
            Check(lossDay.DayResult==250000,"relief is cash income, not additional realized trade profit");
            Check(taxBook.CloseDay(2,250000,-1000000,0,0)==null,"same close cannot duplicate tax or rescue");
            var taxSnapshot=Clone(taxBook.Capture()); var rescued=new DailyIncomeLedger();
            Check(rescued.Restore(taxSnapshot,2)&&rescued.ReliefDay==2,"rescue-use state survives snapshot round trip");
            rescued.BeginDay(3); var secondLoss=rescued.CloseDay(3,250000,-2000000,50000000,500000);
            Check(secondLoss.Relief==0&&secondLoss.Tax==0&&secondLoss.Transfer==250000,"subsequent loss has no second rescue and no profit tax");
            var flat=new DailyIncomeLedger(); flat.BeginDay(1); var flatDay=flat.CloseDay(1,250000,0,0,0);
            Check(flatDay.Tax==0&&flat.ReliefDay==0,"company income alone does not incur profit tax or consume rescue");
            flat.BeginDay(2); Check(flat.CloseDay(2,0,1999,10000,10000).Tax==0,"tax below one won is discarded");
            flat.BeginDay(3); Check(flat.CloseDay(3,0,2000,10000,10000).Tax==1,"exact one-won tax threshold");
            flat.BeginDay(4); Check(flat.CloseDay(4,0,-10.1m,10000,10000).Relief==11,"whole-won rescue covers fractional acquisition-cost loss");
            var debt=new DailyIncomeLedger(); debt.BeginDay(1); var illiquid=debt.CloseDay(1,250000,1000000000,100000000,0);
            Check(illiquid.Tax==500000&&illiquid.TaxPaid==250000&&illiquid.UnpaidTax==250000&&illiquid.Cash==0,"illiquid profit never makes cash negative; unpaid tax is retained");
            Check(illiquid.Assets==99750000&&illiquid.Transfer==0,"unpaid tax remains a liability in net assets");
            var debtSaved=Clone(debt.Capture()); var debtLoaded=new DailyIncomeLedger();
            Check(debtLoaded.Restore(debtSaved,1)&&debtLoaded.UnpaidTax==250000,"unpaid tax survives save/restore without recharging cash");
            debtLoaded.BeginDay(2); var repayment=debtLoaded.CloseDay(2,250000,0,100000000,0);
            Check(repayment.Tax==0&&repayment.PriorTax==250000&&repayment.TaxPaid==250000&&repayment.UnpaidTax==0&&repayment.Transfer==0,"next close repays previous tax without inventing new tax");
            Check(repayment.DayResult==250000&&repayment.Assets==100000000,"debt repayment is not counted as a second tax expense");
            var brokenTax=Clone(taxSnapshot); brokenTax.reports[0].tax="999";
            Check(!new DailyIncomeLedger().Restore(brokenTax,2),"inconsistent tax report is rejected");
            brokenTax=Clone(taxSnapshot); brokenTax.reliefDay=0;
            Check(!new DailyIncomeLedger().Restore(brokenTax,2),"snapshot cannot forget a recorded rescue");
            brokenTax=Clone(debtSaved); brokenTax.unpaidTax="0";
            Check(!new DailyIncomeLedger().Restore(brokenTax,1),"snapshot cannot erase outstanding tax");
            var legacy=Clone(taxSnapshot); legacy.version=1; legacy.reliefDay=0; legacy.unpaidTax=null;
            foreach(var old in legacy.reports) { old.version=1; old.tax=old.relief=old.taxPaid=old.priorTax=old.unpaidTax=old.cash=null; }
            var migrated=new DailyIncomeLedger();
            Check(migrated.Restore(legacy,2)&&migrated.ReliefDay==0&&migrated.UnpaidTax==0,"old version-one income history migrates without retrospective tax");
            Console.WriteLine("Passed="+passed); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
