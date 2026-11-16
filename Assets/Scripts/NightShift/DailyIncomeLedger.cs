using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>일별 이용권·예수금·정산의 원장. 엔진/UI/벽시계에 의존하지 않는다.</summary>
public sealed class DailyIncomeLedger
{
    public const long MaximumFee = 1000000000000L;
    public const decimal TaxRate = .0005m;
    [Serializable] public sealed class Rules
    {
        public int version = 1;
        public long companyIncome = 250000;
        public double cashFraction = .02, equityFraction = .0015, recentProfitFraction = .25;
        public double betrayalPenalty = .85, fearPenalty = .5;
        public long curiosityValue = 1000;
        public float idleFeeImpact = 4f, idleFeeMaximum = 4f;
    }
    public sealed class Ticket
    {
        public int BotId;
        public bool Access;
        public long Fee, UnattributedFee;
        public decimal GrossProfit, RealizedCost, OpeningAssets;
    }
    [Serializable] public sealed class TicketSnapshot
    {
        public int bot;
        public bool access;
        public long fee, unattributed;
        public string gross, cost, assets;
    }
    [Serializable] public sealed class PerformanceSnapshot { public int bot; public string net; }
    [Serializable] public sealed class ClosingReport
    {
        public int version = 1;
        public int day, payingMembers;
        public long company, fees;
        public string realized, assets, dividends;
        public string tax, relief, taxPaid, priorTax, unpaidTax, cash;
        public long Deposit => company + fees;
        public decimal Realized => Amount(realized);
        public decimal Dividends => Amount(dividends);
        public decimal Tax => Amount(tax);
        public decimal Relief => Amount(relief);
        public decimal TaxPaid => Amount(taxPaid);
        public decimal PriorTax => Amount(priorTax);
        public decimal UnpaidTax => Amount(unpaidTax);
        public decimal Transfer => Deposit + Relief - TaxPaid;
        public decimal DayResult => Realized + Deposit + Dividends + Relief - Tax;
        public decimal Assets => Amount(assets);
        public decimal Cash => Amount(cash);
    }
    [Serializable] public sealed class Snapshot
    {
        public int version = 2, day, settled, reliefDay;
        public long fee, nextFee, revision, lastOutcome;
        public string unpaidTax;
        public TicketSnapshot[] tickets;
        public PerformanceSnapshot[] performance;
        public ClosingReport[] reports;
    }
    public int Day { get; private set; }
    public int SettledThrough { get; private set; }
    public long Fee { get; private set; }
    public long NextFee { get; private set; }
    public long Revision { get; private set; }
    public long Escrow { get; private set; }
    public int PayingMembers { get; private set; }
    public int ReliefDay { get; private set; }
    public decimal UnpaidTax { get; private set; }
    public IReadOnlyList<ClosingReport> Reports => reports;
    public IEnumerable<Ticket> Tickets => tickets.Values;
    private readonly Dictionary<int, Ticket> tickets = new();
    private readonly Dictionary<int, decimal> performance = new();
    private readonly Queue<int> performanceOrder = new();
    private readonly List<ClosingReport> reports = new(32);
    private long lastOutcome;
    public Ticket GetTicket(int bot) => tickets.TryGetValue(bot,out var ticket) ? ticket : null;
    public decimal RecentNet(int bot) => performance.TryGetValue(bot,out var net) ? net : 0m;
    public bool HasAccess(int bot) => Day > SettledThrough && GetTicket(bot)?.Access == true;

    public bool ScheduleFee(long amount)
    {
        if (amount < 0 || amount > MaximumFee)
        { return false; }
        if (NextFee != amount) { NextFee = amount; Revision++; }
        return true;
    }
    public bool BeginDay(int day)
    {
        if (day <= Day || day <= 0 || Day > SettledThrough || Escrow != 0)
        { return false; }
        Day = day; Fee = NextFee; tickets.Clear(); PayingMembers = 0; Revision++; return true;
    }
    public bool Admit(int bot, decimal available, decimal assets, bool accepts, out long charge)
    {
        charge = 0;
        if (bot <= 0 || Day <= SettledThrough)
        { return false; }
        if (HasAccess(bot))
        { return true; }
        if (!accepts || available < Fee || assets < 0 || tickets.Count >= 2000 && !tickets.ContainsKey(bot))
        { return false; }
        var ticket = Ensure(bot); ticket.Access = true; ticket.Fee = ticket.UnattributedFee = Fee; ticket.OpeningAssets = assets;
        charge = Fee; Escrow += charge;
        if (charge > 0) { PayingMembers++; }
        Revision++; return true;
    }
    private Ticket Ensure(int bot)
    {
        if (!tickets.TryGetValue(bot,out var ticket))
        { ticket = new Ticket { BotId = bot }; tickets.Add(bot,ticket); }
        return ticket;
    }
    public bool AttributeOutcome(int bot, long eventId, decimal cost, decimal proceeds, out long fee)
    {
        fee = 0;
        if (bot <= 0 || eventId <= lastOutcome || cost <= 0 || Day <= SettledThrough || tickets.Count >= 2000 && !tickets.ContainsKey(bot))
        { return false; }
        var ticket = Ensure(bot); lastOutcome = eventId;
        ticket.GrossProfit += proceeds - cost; ticket.RealizedCost += cost;
        fee = ticket.UnattributedFee; ticket.UnattributedFee = 0; Revision++; return true;
    }
    public long TakeUnattributedFee(int bot)
    {
        var ticket = GetTicket(bot);
        if (ticket == null || Day <= SettledThrough)
        { return 0; }
        long fee = ticket.UnattributedFee;
        if (fee > 0) { ticket.UnattributedFee = 0; Revision++; }
        return fee;
    }
    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets, decimal availableCash = 0m, decimal dividends = 0m)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee || dividends < 0)
        { return null; }
        decimal tax = decimal.Floor(Math.Max(0m,realized) * TaxRate);
        // Whole-won relief covers the entire first realized loss, including fractional acquisition cost.
        decimal relief = realized < 0m && ReliefDay == 0 ? decimal.Ceiling(-realized) : 0m;
        decimal paid = Math.Min(UnpaidTax + tax, Math.Max(0m,decimal.Floor(availableCash + company + Escrow + relief)));
        decimal remaining = UnpaidTax + tax - paid;
        decimal transfer = company + Escrow + relief - paid;
        var result = new ClosingReport { version = 2, day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), dividends = Money(dividends), assets = Money(assets + transfer - remaining), cash = Money(availableCash + transfer),
            tax = Money(tax), relief = Money(relief), priorTax = Money(UnpaidTax), taxPaid = Money(paid), unpaidTax = Money(remaining) };
        if (relief > 0m) { ReliefDay = day; }
        UnpaidTax = remaining;
        foreach (var ticket in tickets.Values)
        {
            decimal net = ticket.GrossProfit - ticket.Fee;
            if (performance.TryGetValue(ticket.BotId,out decimal prior)) { performance[ticket.BotId] = (prior + net) / 2m; }
            else
            {
                if (performanceOrder.Count >= 2000) { performance.Remove(performanceOrder.Dequeue()); }
                performanceOrder.Enqueue(ticket.BotId); performance.Add(ticket.BotId,net);
            }
        }
        reports.Add(result);
        if (reports.Count > 32) { reports.RemoveAt(0); }
        SettledThrough = day; Escrow = 0; Revision++; return result;
    }
    // Same bot/day uses a stable preference: retries do not reroll a purchase decision.
    public static decimal Willingness(Rules rules, int bot, int day, decimal available, decimal equity, decimal recentNet,
        float trust, float betrayal, float greed, float fear, float credulity)
    {
        if (available <= 0 || equity <= 0)
        { return 0m; }
        double t = Unit(trust), b = Unit(betrayal), g = Unit(greed), f = Unit(fear);
        uint hash = unchecked((uint)(bot * 73856093 ^ day * 19349663)); hash ^= hash >> 13;
        decimal variation = .75m + (hash % 1001) / 2000m;
        decimal confidence = (decimal)((.15 + .85*t) * (.5 + .5*g) * (1-rules.betrayalPenalty*b) * (1-rules.fearPenalty*f) * (.6 + Math.Max(0,Math.Min(1,credulity))));
        decimal expected = (rules.curiosityValue + equity * (decimal)rules.equityFraction * confidence + recentNet * (decimal)rules.recentProfitFraction) * variation;
        return Math.Max(0m,decimal.Floor(Math.Min(available * (decimal)rules.cashFraction,expected)));
    }
    private static double Unit(float value) => Math.Max(0,Math.Min(100,float.IsNaN(value) ? 0 : value)) / 100d;
    public static bool Valid(Rules rules) => rules != null && rules.version == 1 && rules.companyIncome >= 0 && rules.companyIncome <= MaximumFee &&
        rules.curiosityValue >= 0 && rules.curiosityValue <= MaximumFee && Range(rules.cashFraction,.001,.1) && Range(rules.equityFraction,0,.05) &&
        Range(rules.recentProfitFraction,0,1) && Range(rules.betrayalPenalty,0,1) && Range(rules.fearPenalty,0,1) && Range(rules.idleFeeImpact,0,10) && Range(rules.idleFeeMaximum,0,10);
    private static bool Range(double value,double min,double max) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static bool Read(string value,out decimal result) => decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out result);
    private static decimal Amount(string value) => Read(value,out decimal number) ? number : 0m;
    private static bool Whole(string value) => Read(value,out decimal number) && number >= 0m && number == decimal.Floor(number);
    public Snapshot Capture()
    {
        var saved = new Snapshot { day = Day, settled = SettledThrough, fee = Fee, nextFee = NextFee, revision = Revision, lastOutcome = lastOutcome,
            reliefDay = ReliefDay, unpaidTax = Money(UnpaidTax) };
        var list = new List<TicketSnapshot>(tickets.Count);
        foreach (var item in tickets.Values)
        { list.Add(new TicketSnapshot { bot = item.BotId, access = item.Access, fee = item.Fee, unattributed = item.UnattributedFee, gross = Money(item.GrossProfit), cost = Money(item.RealizedCost), assets = Money(item.OpeningAssets) }); }
        saved.tickets = list.ToArray();
        var history = new List<PerformanceSnapshot>(performance.Count);
        foreach (int bot in performanceOrder) { history.Add(new PerformanceSnapshot { bot = bot, net = Money(performance[bot]) }); }
        saved.performance = history.ToArray(); saved.reports = reports.ConvertAll(Copy).ToArray(); return saved;
    }
    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { version = item.version, day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets, dividends = item.dividends,
        tax = item.tax, relief = item.relief, taxPaid = item.taxPaid, priorTax = item.priorTax, unpaidTax = item.unpaidTax, cash = item.cash };
    public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
        { return false; }
        if (saved.reliefDay < ReliefDay || saved.reliefDay < 0 || saved.reliefDay > saved.settled || saved.version == 1 && (saved.reliefDay != 0 || UnpaidTax != 0m) || saved.version == 2 && !Whole(saved.unpaidTax))
        { return false; }
        var seen = new HashSet<int>();
        foreach (var item in saved.tickets)
        {
            if (item == null || item.bot <= 0 || !seen.Add(item.bot) || item.fee < 0 || item.fee > MaximumFee || !item.access && item.fee != 0 || item.unattributed < 0 || item.unattributed > item.fee ||
                !Read(item.gross,out _) || !Read(item.cost,out decimal cost) || cost < 0 || !Read(item.assets,out decimal assets) || assets < 0)
            { return false; }
        }
        seen.Clear();
        foreach (var item in saved.performance)
        { if (item == null || item.bot <= 0 || !seen.Add(item.bot) || !Read(item.net,out _)) { return false; } }
        int previous = 0;
        foreach (var item in saved.reports)
        {
            if (item == null || item.day <= previous || item.day > saved.settled || item.company < 0 || item.company > MaximumFee || item.fees < 0 || item.fees > MaximumFee*2000L ||
                item.payingMembers < 0 || item.payingMembers > 2000 || !Read(item.realized,out _) || !Read(item.assets,out _))
            { return false; }
            if (item.version < 1 || item.version > 2)
            { return false; }
            if (item.dividends != null && !Whole(item.dividends)) { return false; }
            if (item.version == 2)
            {
                if (!Whole(item.tax) || !Whole(item.relief) || !Whole(item.taxPaid) || !Whole(item.priorTax) || !Whole(item.unpaidTax) || !Read(item.cash,out decimal cash) || cash < 0m ||
                    item.Tax != decimal.Floor(Math.Max(0m,item.Realized)*TaxRate) || item.TaxPaid > item.PriorTax + item.Tax || item.UnpaidTax != item.PriorTax + item.Tax - item.TaxPaid ||
                    item.Relief > 0m && (saved.reliefDay != item.day || item.Realized >= 0m || item.Relief != decimal.Ceiling(-item.Realized)))
                { return false; }
            }
            previous = item.day;
        }
        if (saved.reports.Length > 0 && saved.reports[saved.reports.Length-1].version == 2 && saved.reports[saved.reports.Length-1].UnpaidTax != Amount(saved.unpaidTax))
        { return false; }
        return true;
    }
    public bool Restore(Snapshot saved,int worldDay)
    {
        if (!CanRestore(saved,worldDay))
        { return false; }
        Day = saved.day; SettledThrough = saved.settled; Fee = saved.fee; NextFee = saved.nextFee; Revision = saved.revision; lastOutcome = saved.lastOutcome;
        ReliefDay = saved.reliefDay; UnpaidTax = saved.version == 1 ? 0m : Amount(saved.unpaidTax);
        tickets.Clear(); performance.Clear(); performanceOrder.Clear(); reports.Clear(); Escrow = 0; PayingMembers = 0;
        foreach (var item in saved.tickets)
        {
            Read(item.gross,out decimal gross); Read(item.cost,out decimal cost); Read(item.assets,out decimal assets);
            tickets.Add(item.bot,new Ticket { BotId = item.bot, Access = item.access, Fee = item.fee, UnattributedFee = item.unattributed, GrossProfit = gross, RealizedCost = cost, OpeningAssets = assets });
            if (Day > SettledThrough) { Escrow += item.fee; }
            if (item.fee > 0) { PayingMembers++; }
        }
        foreach (var item in saved.performance) { Read(item.net,out decimal net); performance.Add(item.bot,net); performanceOrder.Enqueue(item.bot); }
        foreach (var item in saved.reports) { reports.Add(Copy(item)); }
        return true;
    }
}
