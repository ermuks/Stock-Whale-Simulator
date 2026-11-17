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
        public long entryIssuedShares = 10000000;
    }
    public CompanyPolicy Policy;
    public decimal ReferenceCapital, BaseRevenue, Cash, Debt, Productivity = 1;
    public decimal ClosePrice, CloseShares, MarketCap, Revenue, Costs, Profit, Dividend, Investment, PlayerDividend;
    public decimal OpeningCash, OpeningDebt, Demand = 1, NewsDemand, CumulativeProfit;
    /// <summary>마지막 결산의 해외 매출(매출에 포함돼 있다).</summary>
    public decimal ExportRevenue;
    public int LastDay, LastAcquisitionDay;
    public bool ControlInitialized, PlayerControls, AcquisitionAnnounced;
    public bool CloseFinished;
    public string Sector;
    public CompanyGrowth growth;

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
    /// <summary>해외 시장의 수요. 국내와 주기가 어긋나 있어서, 국내가 나쁠 때 버팀목이 되기도 한다.</summary>
    public decimal ExportDemandFor(int day, string symbol, Rules rules)
    {
        double phase = Hash("export:" + (Sector ?? "market")) % 628 / 100d;
        double cycle = Math.Sin(day * .23 + phase) * .38;
        double variation = (Hash(symbol + ":EX:" + day + ":" + rules.seed) % 1001 / 1000d - .5) * .24;
        return Math.Max(.25m, Math.Min(1.65m, 1m + (decimal)(cycle + variation)));
    }
    /// <summary>
    /// fixedSaving은 고정비 비율에서 빼는 값(자동화), exportShare는 기본 매출 대비 해외 매출의 크기, exportDemand는 그날의 해외 수요다.
    /// 해외 매출에도 변동비는 똑같이 들고, 고정비는 더 들지 않는다.
    /// </summary>
    public bool BeginClose(int day, decimal price, decimal shares, decimal demand, Rules rules,decimal efficiency=1m,decimal fixedSaving=0m,decimal exportShare=0m,decimal exportDemand=1m)
    {
        if (day <= LastDay || day < 1 || shares <= 0 || price < 0)
        { return false; }
        LastDay = day; CloseFinished = false; ClosePrice = price; CloseShares = shares; MarketCap = price * shares;
        OpeningCash = Cash; OpeningDebt = Debt; Dividend = Investment = PlayerDividend = 0;
        Demand = Math.Max(.25m, Math.Min(1.65m, demand));
        decimal saving = Math.Max(.1m,Math.Min(1,efficiency)), abroad = Math.Max(0m, exportShare) * Math.Max(.25m, Math.Min(1.65m, exportDemand));
        ExportRevenue = decimal.Floor(BaseRevenue * abroad * Productivity);
        Revenue = decimal.Floor(BaseRevenue * Demand * Productivity) + ExportRevenue;
        decimal fixedRatio = Math.Max(0m, (decimal)rules.fixedCostRatio - Math.Max(0m, fixedSaving));
        Costs = decimal.Ceiling(BaseRevenue * ((decimal)rules.variableCostRatio * (Demand + abroad) * saving + fixedRatio) + Debt * .001m);
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
    /// <summary>productivityLimit·productivityGain이 0보다 크면 재투자로 오르는 생산성의 한도와 오르는 빠르기를 그 값으로 바꾼다(창업 기업).</summary>
    public void FinishClose(decimal paid, decimal playerPaid, Rules rules, decimal productivityLimit = 0m, decimal productivityGain = 0m)
    {
        if (LastDay == 0 || CloseFinished) { throw new InvalidOperationException("Company close already completed or not started."); }
        if (paid < 0 || paid > DividendBudget(rules) || playerPaid < 0 || playerPaid > paid) { throw new InvalidOperationException("Invalid corporate distribution."); }
        Dividend = paid; PlayerDividend = playerPaid; Cash -= paid;
        double ratio = Policy == CompanyPolicy.Investment ? rules.growthInvestment : Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        decimal limit = productivityLimit > 0m ? productivityLimit : (decimal)rules.maximumProductivity;
        decimal gain = productivityGain > 0m ? productivityGain : (decimal)rules.productivityGain;
        decimal capacity = gain<=0 ? 0 : Math.Max(0,limit-Productivity)*ReferenceCapital/gain;
        Investment = Debt > 0 ? 0 : Math.Min(decimal.Floor(capacity),Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio)));
        Cash -= Investment;
        Productivity = Math.Min(limit, Productivity + Investment / ReferenceCapital * gain);
        CloseFinished = true;
    }
}
