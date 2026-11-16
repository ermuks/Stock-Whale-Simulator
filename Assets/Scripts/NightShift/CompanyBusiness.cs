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
        public long entryIssuedShares = 2000000;
    }
    public CompanyPolicy Policy;
    public decimal ReferenceCapital, BaseRevenue, Cash, Debt, Productivity = 1;
    public decimal ClosePrice, CloseShares, MarketCap, Revenue, Costs, Profit, Dividend, Investment, PlayerDividend;
    public decimal OpeningCash, OpeningDebt, Demand = 1, NewsDemand, CumulativeProfit;
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
    public bool BeginClose(int day, decimal price, decimal shares, decimal demand, Rules rules,decimal efficiency=1m)
    {
        if (day <= LastDay || day < 1 || shares <= 0 || price < 0)
        { return false; }
        LastDay = day; CloseFinished = false; ClosePrice = price; CloseShares = shares; MarketCap = price * shares;
        OpeningCash = Cash; OpeningDebt = Debt; Dividend = Investment = PlayerDividend = 0;
        Demand = Math.Max(.25m, Math.Min(1.65m, demand));
        Revenue = decimal.Floor(BaseRevenue * Demand * Productivity);
        Costs = decimal.Ceiling(BaseRevenue * ((decimal)rules.variableCostRatio * Demand * Math.Max(.1m,Math.Min(1,efficiency)) + (decimal)rules.fixedCostRatio) + Debt * .001m);
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
    public void FinishClose(decimal paid, decimal playerPaid, Rules rules)
    {
        if (LastDay == 0 || CloseFinished) { throw new InvalidOperationException("Company close already completed or not started."); }
        if (paid < 0 || paid > DividendBudget(rules) || playerPaid < 0 || playerPaid > paid) { throw new InvalidOperationException("Invalid corporate distribution."); }
        Dividend = paid; PlayerDividend = playerPaid; Cash -= paid;
        double ratio = Policy == CompanyPolicy.Investment ? rules.growthInvestment : Policy == CompanyPolicy.Dividend ? rules.dividendInvestment : 0;
        decimal capacity = rules.productivityGain<=0 ? 0 : Math.Max(0,(decimal)rules.maximumProductivity-Productivity)*ReferenceCapital/(decimal)rules.productivityGain;
        Investment = Debt > 0 ? 0 : Math.Min(decimal.Floor(capacity),Math.Min(Cash, decimal.Floor(Math.Max(0, Profit) * (decimal)ratio)));
        Cash -= Investment;
        Productivity = Math.Min((decimal)rules.maximumProductivity, Productivity + Investment / ReferenceCapital * (decimal)rules.productivityGain);
        CloseFinished = true;
    }
}
