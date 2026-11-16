# 장 마감 수익·리딩방 이용료 코드 기록

2026-11-15. 이 작업 시작 시 백업과 비교했으며 기존 미커밋 변경은 보존했다.

## 신규 런타임 스크립트 전문

### Assets/Scripts/NightShift/DailyIncomeLedger.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>일별 이용권·예수금·정산의 원장. 엔진/UI/벽시계에 의존하지 않는다.</summary>
public sealed class DailyIncomeLedger
{
    public const long MaximumFee = 1000000000000L;
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
        public int day, payingMembers;
        public long company, fees;
        public string realized, assets;
        public long Deposit => company + fees;
    }
    [Serializable] public sealed class Snapshot
    {
        public int version = 1, day, settled;
        public long fee, nextFee, revision, lastOutcome;
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
    public ClosingReport CloseDay(int day, long company, decimal realized, decimal assets)
    {
        if (day != Day || day <= SettledThrough || company < 0 || company > MaximumFee)
        { return null; }
        var result = new ClosingReport { day = day, company = company, fees = Escrow, payingMembers = PayingMembers,
            realized = Money(realized), assets = Money(assets) };
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
    public Snapshot Capture()
    {
        var saved = new Snapshot { day = Day, settled = SettledThrough, fee = Fee, nextFee = NextFee, revision = Revision, lastOutcome = lastOutcome };
        var list = new List<TicketSnapshot>(tickets.Count);
        foreach (var item in tickets.Values)
        { list.Add(new TicketSnapshot { bot = item.BotId, access = item.Access, fee = item.Fee, unattributed = item.UnattributedFee, gross = Money(item.GrossProfit), cost = Money(item.RealizedCost), assets = Money(item.OpeningAssets) }); }
        saved.tickets = list.ToArray();
        var history = new List<PerformanceSnapshot>(performance.Count);
        foreach (int bot in performanceOrder) { history.Add(new PerformanceSnapshot { bot = bot, net = Money(performance[bot]) }); }
        saved.performance = history.ToArray(); saved.reports = reports.ConvertAll(Copy).ToArray(); return saved;
    }
    private static ClosingReport Copy(ClosingReport item) => new ClosingReport { day = item.day, company = item.company, fees = item.fees, payingMembers = item.payingMembers, realized = item.realized, assets = item.assets };
    public bool CanRestore(Snapshot saved,int worldDay)
    {
        if (saved == null || saved.version != 1 || saved.day < 0 || saved.day > worldDay || saved.settled < 0 || saved.settled > saved.day || saved.settled < SettledThrough ||
            saved.revision < Revision || saved.lastOutcome < 0 || saved.fee < 0 || saved.fee > MaximumFee || saved.nextFee < 0 || saved.nextFee > MaximumFee ||
            saved.tickets == null || saved.tickets.Length > 2000 || saved.performance == null || saved.performance.Length > 2000 || saved.reports == null || saved.reports.Length > 32)
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
            previous = item.day;
        }
        return true;
    }
    public bool Restore(Snapshot saved,int worldDay)
    {
        if (!CanRestore(saved,worldDay))
        { return false; }
        Day = saved.day; SettledThrough = saved.settled; Fee = saved.fee; NextFee = saved.nextFee; Revision = saved.revision; lastOutcome = saved.lastOutcome;
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
```

### Assets/Scripts/NightShift/DailyIncomeController.cs

```csharp
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

/// <summary>기존 장 전환과 계좌를 연결한다. 이용료는 봇 현금에서 예수금으로, 마감 시 기본 계좌로 이전한다.</summary>
public sealed class DailyIncomeController : MonoBehaviour
{
    [Serializable] public sealed class SaveData
    {
        public string worldId, company, realizedAtOpen;
        public DailyIncomeLedger.Snapshot ledger;
    }
    public static DailyIncomeController Instance { get; private set; }
    public DailyIncomeLedger.Rules Rules { get; private set; } = new();
    public DailyIncomeLedger Ledger { get; private set; } = new();
    public string CompanyName { get; private set; } = "내 작은 회사";
    public string Feedback { get; private set; } = "";
    private decimal realizedAtOpen;
    private int settlingDay;
    void Awake()
    {
        Instance = this;
        var file = Resources.Load<TextAsset>("Progression/DailyIncome");
        if (file == null)
        { return; }
        try
        {
            var settings = JsonUtility.FromJson<DailyIncomeLedger.Rules>(file.text);
            if (DailyIncomeLedger.Valid(settings)) { Rules = settings; }
            else { Debug.LogWarning("DailyIncome 설정이 유효하지 않아 기본값을 사용합니다."); }
        }
        catch (ArgumentException) { Debug.LogWarning("DailyIncome 설정을 읽지 못해 기본값을 사용합니다."); }
    }
    public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.Length >= 2 && name.Length <= 20 && name.IndexOfAny(new[] {'<','>','\n','\r','\t'}) < 0;
    public bool NameCompany(string name)
    {
        if (!ValidName(name) || StockManager.Instance != null && StockManager.Instance.SetupConfirmed)
        { return false; }
        CompanyName = name; return true;
    }
    public void BeginDay(int day)
    {
        if (Ledger.BeginDay(day))
        { realizedAtOpen = PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit; }
    }
    public bool SetNextFee(string text)
    {
        if (PlayerProgression.Instance == null || !PlayerProgression.Instance.CanOperateRooms)
        { Feedback = "리딩방 개설 전"; return false; }
        if (!long.TryParse(text.Replace(",",""),NumberStyles.None,CultureInfo.InvariantCulture,out long value) || !Ledger.ScheduleFee(value))
        { Feedback = "0~1조 원 · 정수"; return false; }
        Feedback = "다음 장 " + value.ToString("N0") + "원"; return true;
    }
    public decimal Willingness(TradeBot bot)
    {
        if (bot?.Social == null)
        { return 0m; }
        var mind = bot.Social.Psychology;
        return DailyIncomeLedger.Willingness(Rules,bot.Social.Id,Ledger.Day,PlayerAccounts.Available(bot),Math.Max(0m,bot.Equity()),Ledger.RecentNet(bot.Social.Id),
            mind.FormulaTrust,mind.FormulaBetrayal,mind.Greed,mind.Fear,mind.Credulity);
    }
    public bool Enter(TradeBot bot,int roomId,out string reason)
    {
        reason = "";
        if (roomId != 1)
        { return true; }
        var day = MarketDayController.Instance;
        if (bot?.Social == null || day == null || (day.Phase != MarketPhase.PreOpen && day.Phase != MarketPhase.Trading) || Ledger.Day != day.Day || Ledger.Day <= Ledger.SettledThrough)
        { reason = "다음 장 참여"; return false; }
        if (Ledger.HasAccess(bot.Social.Id))
        { return true; }
        decimal available = PlayerAccounts.Available(bot);
        if (available < Ledger.Fee)
        { reason = "이용료를 낼 현금이 없습니다."; return false; }
        if (Ledger.Fee > Willingness(bot))
        { reason = "하루 " + Ledger.Fee.ToString("N0") + "원은 비쌉니다."; return false; }
        if (!Ledger.Admit(bot.Social.Id,available,Math.Max(0m,bot.Equity()),true,out long charge))
        { reason = "이용권 처리 불가"; return false; }
        bot.ApplyTradeCash(-charge);
        InvestorSociety.Instance?.Trace(bot,"이용권: " + Ledger.Day + "일 · " + charge.ToString("N0") + "원 · 예수금 " + Ledger.Escrow.ToString("N0"));
        return true;
    }
    public void Renew(TradeBot bot)
    {
        if (bot?.Social == null || bot.Social.Psychology.Room != 1)
        { return; }
        if (!Enter(bot,1,out string reason))
        {
            bot.Social.Psychology.NextMembership = Time.time + 60f;
            InvestorSociety.Instance?.Leave(bot,"이용권 갱신 거절: " + reason);
        }
    }
    public InvestorExperience NetExperience(TradeBot bot,InvestorExperience gross)
    {
        if (gross.Room != 1 || gross.CommandId <= 0 || !Ledger.AttributeOutcome(bot.Social.Id,gross.EventId,gross.Cost,gross.Proceeds,out long fee) || fee == 0)
        { return gross; }
        return new InvestorExperience(gross.EventId,gross.CommandId,gross.Room,gross.Symbol,gross.Cost+fee,gross.Proceeds,gross.Time,"체결 · 이용료 포함",gross.AssetBefore);
    }
    public IEnumerator Settle(int day)
    {
        var account = PlayerAccounts.Instance == null ? StockManager.Instance?.playerAccount : PlayerAccounts.Instance.primary;
        var market = MarketDayController.Instance;
        if (account == null || market == null || market.Phase != MarketPhase.Closing || market.Day != day || market.IntegrityFailed || day != Ledger.Day || day <= Ledger.SettledThrough || settlingDay == day)
        { yield break; }
        settlingDay = day;
        try
        {
            int count = 0;
            foreach (var ticket in Ledger.Tickets)
            {
                long fee = Ledger.TakeUnattributedFee(ticket.BotId);
                var bot = MarketDayController.Instance?.FindBot(ticket.BotId);
                if (fee > 0 && bot?.Social != null)
                {
                    bot.Social.Psychology.RecordRoomFee(InvestorSociety.Instance.NextEventId(),fee,ticket.OpeningAssets,Rules);
                    InvestorSociety.Instance.Trace(bot,bot.Social.Psychology.LastCause);
                }
                if (++count % 24 == 0) { yield return null; }
            }
            decimal realized = (PlayerProgression.Instance == null ? 0m : PlayerProgression.Instance.RealizedProfit) - realizedAtOpen;
            decimal deposit = Rules.companyIncome + Ledger.Escrow;
            decimal equity = account.Equity() + deposit + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
            var report = Ledger.CloseDay(day,Rules.companyIncome,realized,equity);
            if (report == null)
            { yield break; }
            // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.
            account.cash += report.Deposit;
            MarketEventFeed.Instance?.Notify("장 마감 · 회사 +" + report.company.ToString("N0") + "원 · 이용료 +" + report.fees.ToString("N0") + "원");
        }
        finally { settlingDay = 0; }
    }
    public SaveData Capture() => new SaveData { worldId = PlayerProgression.Instance?.WorldId, company = CompanyName,
        realizedAtOpen = realizedAtOpen.ToString(CultureInfo.InvariantCulture), ledger = Ledger.Capture() };
    public bool CanRestore(SaveData saved,string worldId)
    {
        return saved != null && saved.worldId == worldId && worldId == PlayerProgression.Instance?.WorldId && ValidName(saved.company) &&
            decimal.TryParse(saved.realizedAtOpen,NumberStyles.Number,CultureInfo.InvariantCulture,out _) &&
            Ledger.CanRestore(saved.ledger,MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day);
    }
    public bool Restore(SaveData saved,string worldId)
    {
        if (!CanRestore(saved,worldId) || !Ledger.Restore(saved.ledger,MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day))
        { return false; }
        CompanyName = saved.company; realizedAtOpen = decimal.Parse(saved.realizedAtOpen,CultureInfo.InvariantCulture); return true;
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 시작 화면, 채팅 Canvas, 1번 태블릿에 필요한 짧은 입력/정산만 추가한다.</summary>
public sealed class DailyIncomeUI : MonoBehaviour
{
    private TextMeshProUGUI currentFee, paidCount, escrow, result, day, company;
    private TextMeshProUGUI[] values;
    private TextMeshProUGUI[] history;
    private TMP_InputField feeInput;
    private GameObject panel;
    private TMP_FontAsset font;
    private float scale, next;
    private bool roomMode;
    private int originalSibling;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(146,161,172,255), Dark = new Color32(13,23,29,255);

    public static TMP_InputField CompanyField(PlayerCompanySetupUI setup)
    {
        if (setup.retailIntroduction == null)
        { return null; }
        var root = setup.retailIntroduction.transform;
        var caption = Instantiate(setup.retailPreview,root); caption.name = "Text :: Private Company Name"; caption.text = "회사 이름";
        Place(caption.rectTransform,root,0,.62f,.51f,.045f); caption.raycastTarget = false;
        var field = Instantiate(setup.companyName,root); field.name = "Input :: Private Company Name";
        Place((RectTransform)field.transform,root,0,.675f,.51f,.075f); field.gameObject.SetActive(true); field.interactable = true;
        field.onValueChanged.RemoveAllListeners(); field.characterLimit = 20; field.contentType = TMP_InputField.ContentType.Standard; field.richText = false;
        field.SetTextWithoutNotify(DailyIncomeController.Instance?.CompanyName ?? "내 작은 회사");
        TabletControlStyle.Input(field); field.textComponent.alignment = TextAlignmentOptions.MidlineLeft; field.textComponent.fontSize = 30f;
        field.onValueChanged.AddListener(_ => setup.Refresh());
        return field;
    }
    public static void InstallRoom(LeadingRoomUI rooms)
    {
        if (rooms.playerControls == null)
        { return; }
        var view = rooms.playerControls.AddComponent<DailyIncomeUI>(); view.roomMode = true; view.Initialize();
        var status = rooms.commandStatus.rectTransform; status.anchorMax = new Vector2(.72f,status.anchorMax.y);
        view.Action(rooms.playerControls.transform,"Room Fee","이용료",.74f,.778f,.23f,.029f,() => view.Open(),false,38);
        view.panel = view.Box("Room Fee",rooms.playerControls.transform,.035f,.59f,.93f,.39f,true).gameObject;
        Transform root = view.panel.transform;
        view.Label(root,"Fee Heading","하루 이용료",.045f,.04f,.6f,.065f,58);
        view.Action(root,"Close Fee","닫기",.79f,.035f,.16f,.07f,view.Close,false,44);
        view.currentFee = view.Metric(root,"오늘",.17f,48);
        view.Label(root,"Next Fee Label","다음 장",.045f,.32f,.25f,.075f,48).color = Muted;
        view.feeInput = Instantiate(rooms.buyPrice,root); view.feeInput.name = "Input :: Daily Room Fee";
        Place((RectTransform)view.feeInput.transform,root,.34f,.32f,.61f,.075f); view.feeInput.gameObject.SetActive(true);
        view.feeInput.onValueChanged.RemoveAllListeners(); view.feeInput.characterLimit = 13; view.feeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        TabletControlStyle.Input(view.feeInput); TradingOrderView.Numeric(view.feeInput.textComponent); view.feeInput.fontAsset = view.feeInput.textComponent.font;
        view.feeInput.textComponent.fontSize = 50*view.scale; view.feeInput.textComponent.alignment = TextAlignmentOptions.MidlineRight;
        view.paidCount = view.Metric(root,"유료 회원",.47f,48,"명");
        view.escrow = view.Metric(root,"정산 대기",.60f,48);
        view.result = view.Label(root,"Fee Result","",.045f,.735f,.91f,.05f,40); view.result.color = Muted;
        view.Action(root,"Apply Fee","다음 장 적용",.045f,.83f,.91f,.08f,view.Apply,true,46);
        view.panel.SetActive(false);
    }
    public static void InstallTablet(TabletAccountUI tablet)
    {
        if (tablet.second)
        { return; }
        var root = tablet.dashboard.transform;
        var view = root.gameObject.AddComponent<DailyIncomeUI>(); view.Initialize();
        view.Action(root,"Income Statement","정산",.21f,.95f,.18f,.049f,view.Open,false,27);
        view.panel = view.Box("Income Statement",root,0,0,1,1,true).gameObject;
        root = view.panel.transform;
        view.company = view.Label(root,"Company","",.035f,.035f,.64f,.06f,36);
        view.day = view.Label(root,"Closing Day","",.55f,.105f,.41f,.045f,25); view.day.alignment = TextAlignmentOptions.MidlineRight; view.day.color = Muted;
        view.Action(root,"Close Statement","닫기",.79f,.035f,.17f,.065f,view.Close,false,27);
        string[] captions = {"회사 수익","리딩방 이용료","당일 실현손익","총자산"}; view.values = new TextMeshProUGUI[captions.Length];
        for (int i = 0; i < captions.Length; i++) { view.values[i] = view.Metric(root,captions[i],.20f+i*.09f,29); }
        view.Label(root,"Statement History","최근 정산",.045f,.62f,.91f,.045f,28);
        view.history = new TextMeshProUGUI[5];
        for (int i = 0; i < view.history.Length; i++)
        { view.history[i] = view.Label(root,"Statement Row " + i,"",.045f,.69f+i*.054f,.91f,.045f,25); view.history[i].color = Muted; }
        view.panel.SetActive(false);
    }
    private void Initialize()
    {
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        scale = ((RectTransform)transform).rect.width / 1600f;
        if (scale <= 0f) { scale = 1f; }
    }
    private void Open()
    {
        if (roomMode && DailyIncomeController.Instance != null)
        { feeInput.SetTextWithoutNotify(DailyIncomeController.Instance.Ledger.NextFee.ToString()); result.text = ""; }
        if (roomMode && !panel.activeSelf) { originalSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
        panel.SetActive(true); panel.transform.SetAsLastSibling(); next = 0f; Refresh();
    }
    private void Close()
    {
        if (panel == null || !panel.activeSelf)
        { return; }
        panel.SetActive(false);
        if (roomMode) { transform.SetSiblingIndex(originalSibling); }
    }
    void OnDisable() { Close(); }
    private void Apply()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        income.SetNextFee(feeInput.text); result.text = income.Feedback; next = 0f;
    }
    void Update()
    {
        if (panel == null || !panel.activeInHierarchy || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f; Refresh();
    }
    private void Refresh()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        var ledger = income.Ledger;
        if (roomMode)
        {
            DeviceTerminalUI.Set(currentFee,ledger.Fee.ToString("N0"));
            DeviceTerminalUI.Set(paidCount,ledger.PayingMembers.ToString("N0"));
            DeviceTerminalUI.Set(escrow,ledger.Escrow.ToString("N0")); return;
        }
        DeviceTerminalUI.Set(company,income.CompanyName);
        var reports = ledger.Reports;
        var last = reports.Count == 0 ? null : reports[reports.Count-1];
        DeviceTerminalUI.Set(day,last == null ? "정산 전" : "DAY " + last.day + " · 장 마감");
        string[] texts = last == null ? new[] {"—","—","—","—"} : new[] {
            last.company.ToString("N0"),last.fees.ToString("N0"),InvestmentDashboard.Signed(decimal.Parse(last.realized,System.Globalization.CultureInfo.InvariantCulture)),decimal.Parse(last.assets,System.Globalization.CultureInfo.InvariantCulture).ToString("N0")};
        for (int i=0;i<values.Length;i++) { DeviceTerminalUI.Set(values[i],texts[i]); }
        for (int i=0;i<history.Length;i++)
        {
            int index = reports.Count-1-i;
            DeviceTerminalUI.Set(history[i],index < 0 ? "" : "DAY " + reports[index].day + "     회사 " + reports[index].company.ToString("N0") + "원     이용료 " + reports[index].fees.ToString("N0") + "원");
        }
    }
    private static void Place(RectTransform rect,Transform parent,float x,float y,float w,float h)
    {
        rect.SetParent(parent,false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
        rect.pivot = new Vector2(.5f,.5f); rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private RectTransform Box(string name,Transform parent,float x,float y,float w,float h,bool background)
    {
        var obj = new GameObject((background ? "Panel :: " : "Area :: ")+name,typeof(RectTransform));
        var rect = (RectTransform)obj.transform; Place(rect,parent,x,y,w,h);
        if (background) { var image = obj.AddComponent<Image>(); image.color = Dark; image.raycastTarget = true; }
        return rect;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false; label.enableAutoSizing = false; label.color = Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.text = text; return label;
    }
    private TextMeshProUGUI Metric(Transform parent,string caption,float y,float size=34,string unit="원")
    {
        Label(parent,caption+" Label",caption,.045f,y,.38f,.065f,size).color = Muted;
        var value = Label(parent,caption+" Value","",.43f,y,.47f,.065f,size+2); TradingOrderView.Numeric(value); value.alignment = TextAlignmentOptions.MidlineRight;
        var suffix = Label(parent,caption+" Unit",unit,.912f,y,.038f,.065f,size); suffix.alignment = TextAlignmentOptions.MidlineRight; suffix.color = Muted;
        return value;
    }
    private Button Action(Transform parent,string name,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool primary,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Button :: " + name;
        var image = rect.gameObject.AddComponent<Image>(); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label(rect,name,caption,.02f,0,.96f,1,size); label.alignment = TextAlignmentOptions.Center;
        TabletControlStyle.Button(button,primary); label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        button.onClick.AddListener(action); return button;
    }
}
```

### Assets/Scripts/NightShift/InvestorPsychology.RoomFee.cs

```csharp
using System;
using System.Globalization;
using UnityEngine;

public sealed partial class InvestorPsychology
{
    private readonly InvestorEventMemory feeEvents = new();
    public void RecordRoomFee(long eventId,long amount,decimal assets,DailyIncomeLedger.Rules rules)
    {
        if (amount <= 0 || !feeEvents.Add(eventId))
        { return; }
        // 실제 매매 성과에서 아직 공제하지 않은 이용료만 평가. 소액 이용료를 투자금 전액 손실로 보지 않는다.
        float ratio = (float)(amount / Math.Max(1m,assets) * 100m);
        float impact = Mathf.Min(rules.idleFeeMaximum,ratio * rules.idleFeeImpact);
        Betrayal += impact; BetrayalMemory = Mathf.Clamp(BetrayalMemory + impact*.55f,0,100);
        Remember(new SocialMemory { eventId = eventId, type = SocialMemoryKind.ROOM_FEE, actorId = -1, targetId = SocialId, salience = 35,
            amount = amount.ToString(CultureInfo.InvariantCulture), at = ChatSocialAudit.GameMinute });
        LastCause = "이용료 " + amount.ToString("N0") + "원 · 실현 성과 없음 · 배신감 +" + impact.ToString("0.00");
        Clamp(); AuditEmotion("room-fee",eventId);
    }
}
```

## 기존 파일: 함수 단위 원본과 수정본

PlayerProgression.Snapshot에는 선택적 income 필드를 추가했다. 기존 버전 1 저장 조각에서 해당 필드가 없어도 읽는다. 
ChatSocialData.SocialMemoryKind의 마지막에 ROOM_FEE를 추가하여 기존 enum 번호를 유지했다. 
PlayerCompanySetupUI.businessName은 런타임 입력 필드이며 기존 상장 기업 이름 필드와 분리했다.

### Assets/Scripts/MarketDay/MarketDayController.cs · AdvancePhase

원본:

```csharp
private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0)
                    {
                        yield return null;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                ClosingPrices[stock] = stock.currentPrice;
            }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            Phase = MarketPhase.PostClose;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
    }
```

수정본:

```csharp
private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0)
                    {
                        yield return null;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                ClosingPrices[stock] = stock.currentPrice;
            }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · RecordOutcome

원본:

```csharp
public void RecordOutcome(TradeBot bot, InvestorExperience experience)
    {
        if (!bot.Social.Psychology.Record(experience, settings))
        { return; }
        Outcomes++; Trace(bot, bot.Social.Psychology.LastCause);
        if (SocialReady) { OfferExistingSpeech(bot, bot.Social.Psychology.Room, Context(bot, experience, bot.Social.Psychology.Room), ChatEvent.Agreement); }
    }
```

수정본:

```csharp
public void RecordOutcome(TradeBot bot, InvestorExperience experience)
    {
        if (DailyIncomeController.Instance != null) { experience = DailyIncomeController.Instance.NetExperience(bot,experience); }
        if (!bot.Social.Psychology.Record(experience, settings))
        { return; }
        Outcomes++; Trace(bot, bot.Social.Psychology.LastCause);
        if (SocialReady) { OfferExistingSpeech(bot, bot.Social.Psychology.Room, Context(bot, experience, bot.Social.Psychology.Room), ChatEvent.Agreement); }
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · Join

원본:

```csharp
public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
    {
        if (bot?.Social == null) { return false; }
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        if (room.PlayerOwned && !playerInvited && !CanDiscoverPlayerRoom(bot, roomId))
        { return false; }
        if (room.PlayerOwned && playerInvited)
        { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

수정본:

```csharp
public bool Join(TradeBot bot, int roomId, bool playerInvited = false)
    {
        if (bot?.Social == null) { return false; }
        var room = GetRoom(roomId); var mind = bot.Social.Psychology;
        if (room != null && (room.Social.banned.Contains(bot.Social.Id) || mind.RejoinAt.TryGetValue(roomId, out double allowedAt) && SocialTime.GameMinute < allowedAt))
        { return false; }
        if (room == null || !IsRoomOpen(roomId) || mind.Room != 0 || room.Members.Count >= settings.roomCapacity || !bot.isActiveAndEnabled || bot.IsExchangeParticipant)
        { return false; }
        if (room.PlayerOwned && !playerInvited && !CanDiscoverPlayerRoom(bot, roomId))
        { return false; }
        if (room.PlayerOwned && DailyIncomeController.Instance != null && !DailyIncomeController.Instance.Enter(bot,roomId,out string feeReason))
        { LastAction = bot.Social.Nickname + ": " + feeReason; Trace(bot,"이용료 거절: " + feeReason); return false; }
        if (room.PlayerOwned && playerInvited)
        { room.Social.firstMemberInvited = true; mind.InvitedPlayerRooms |= 1 << (roomId - 1); }
        mind.Room = roomId; mind.JoinedRoomAt = Time.time; room.Members.Add(bot.Social.Id); Joins++;
        mind.LastWitnessRoom = 0;
        mind.LastRead[roomId] = room.Version; // Pre-join private messages were not witnessed.
        PostRoomNotice(roomId, ChatRoomNotice.Join, bot.Social.Nickname + " 님이 입장했습니다.");
        if (Time.time >= room.NextConversation && random.NextDouble() < settings.roomGreetingProbability)
        { Speak(bot, roomId, InvestorConversation.Greeting(bot), default, ChatEvent.SmallTalk); }
        Trace(bot, roomId + "번 방 참여"); return true;
    }
```

### Assets/Scripts/NightShift/InvestorSociety.cs · Invite

원본:

```csharp
public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned || !IsRoomOpen(roomId))
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
        if (!firstMember && (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool accepted = (firstMember || random.NextDouble() < mind.JoinChance) && Join(bot, roomId, playerInvited: true);
        LastAction = accepted ? bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다." : bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        return accepted;
    }
```

수정본:

```csharp
public bool Invite(int botId, int roomId)
    {
        if (PlayerProgression.Instance != null && !PlayerProgression.Instance.CanOperateRooms)
        { LastAction = PlayerProgression.Instance.LockReason; return false; }
        day ??= MarketDayController.Instance;
        var bot = day == null ? null : day.FindBot(botId);
        if (bot == null || bot.Social == null || !MarketDayController.CanTrade || GetRoom(roomId) == null || !GetRoom(roomId).PlayerOwned || !IsRoomOpen(roomId))
        { LastAction = "참여자와 리딩방을 선택하세요."; return false; }
        var mind = bot.Social.Psychology;
        bool firstMember = !GetRoom(roomId).Social.firstMemberInvited;
        if (!firstMember && (Time.time < nextContact || Time.time - mind.LastContact < settings.repeatContactCooldown))
        { LastAction = "아직 다시 연락할 수 없습니다."; return false; }
        if (mind.Room != 0)
        { LastAction = "이미 " + GetRoom(mind.Room).DisplayName + "에 참여 중입니다."; return false; }
        if (GetRoom(roomId).Members.Count >= settings.roomCapacity)
        { LastAction = "방의 정원이 가득 찼습니다."; return false; }
        if (!bot.isActiveAndEnabled || bot.IsExchangeParticipant || GetRoom(roomId).Social.banned.Contains(botId) ||
            mind.RejoinAt.TryGetValue(roomId, out double returnAt) && SocialTime.GameMinute < returnAt)
        { LastAction = "지금은 이 참여자를 초대할 수 없습니다."; return false; }
        nextContact = Time.time + settings.contactCooldown; mind.LastContact = Time.time;
        mind.RecallBetrayal(); Trace(bot, "회유: " + mind.LastCause);
        bool willing = firstMember || random.NextDouble() < mind.JoinChance;
        LastAction = bot.Social.Nickname + ": 참여 제안을 거절했습니다.";
        bool accepted = willing && Join(bot, roomId, playerInvited: true);
        if (accepted) { LastAction = bot.Social.Nickname + ": 내 리딩방에 참여했습니다. 명령 수락은 별도 판단합니다."; }
        return accepted;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Awake

원본:

```csharp
void Awake()
    {
        Instance = this; WorldId = Guid.NewGuid().ToString("N");
        var data = Resources.Load<TextAsset>("Progression/PlayerProgression");
        if (data != null)
        {
            var candidate = JsonUtility.FromJson<Rules>(data.text);
            if (candidate != null && candidate.schemaVersion == 1 && candidate.leadingTurnover > 0 && candidate.leadingRealizedProfit > 0)
            { rules = candidate; }
        }
    }
```

수정본:

```csharp
void Awake()
    {
        Instance = this; WorldId = Guid.NewGuid().ToString("N");
        if (GetComponent<DailyIncomeController>() == null) { gameObject.AddComponent<DailyIncomeController>(); }
        var data = Resources.Load<TextAsset>("Progression/PlayerProgression");
        if (data != null)
        {
            var candidate = JsonUtility.FromJson<Rules>(data.text);
            if (candidate != null && candidate.schemaVersion == 1 && candidate.leadingTurnover > 0 && candidate.leadingRealizedProfit > 0)
            { rules = candidate; }
        }
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Capture

원본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks });
    }
```

수정본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks,
            income = DailyIncomeController.Instance?.Capture() });
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Restore

원본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

수정본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        if (saved.income != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.CanRestore(saved.income,WorldId)))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        if (saved.income != null) { DailyIncomeController.Instance.Restore(saved.income,WorldId); }
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

### Assets/Scripts/PlayerCompanySetupUI.cs · Start

원본:

```csharp
void Start()
    {
        var manager = StockManager.Instance;
        if (manager == null || !manager.useInitialEconomy || manager.EconomyInitialized)
        {
            gameObject.SetActive(false);
            return;
        }

        previousCursor = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        companyName.richText = false;
        companyName.SetTextWithoutNotify(manager.economy.companies[0].name);
        issuedShares.SetTextWithoutNotify(manager.economy.companies[0].issuedShares.ToString(System.Globalization.CultureInfo.InvariantCulture));
        publicPercentage.SetTextWithoutNotify(manager.economy.defaultPublicPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        companyName.onValueChanged.AddListener(Changed);
        issuedShares.onValueChanged.AddListener(Changed);
        publicPercentage.onValueChanged.AddListener(Changed);
        confirm.onClick.AddListener(Confirm);
        StockManager.OnInitialEconomyReady += OnReady;
        listenersReady = true;
        if (retailIntroduction != null)
        { retailIntroduction.SetActive(manager.economy.retailPlayerStart); }
        if (companySetup != null)
        { companySetup.SetActive(!manager.economy.retailPlayerStart); }
        if (controlsToggle != null)
        { controlsToggle.onClick.AddListener(ToggleControls); controlsToggle.gameObject.SetActive(manager.economy.retailPlayerStart); }
        if (!manager.economy.retailPlayerStart)
        { confirm.GetComponentInChildren<TextMeshProUGUI>().text = "기업 설정하고 시작"; }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
        Refresh();
    }
```

수정본:

```csharp
void Start()
    {
        var manager = StockManager.Instance;
        if (manager == null || !manager.useInitialEconomy || manager.EconomyInitialized)
        {
            gameObject.SetActive(false);
            return;
        }

        previousCursor = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        companyName.richText = false;
        companyName.SetTextWithoutNotify(manager.economy.companies[0].name);
        issuedShares.SetTextWithoutNotify(manager.economy.companies[0].issuedShares.ToString(System.Globalization.CultureInfo.InvariantCulture));
        publicPercentage.SetTextWithoutNotify(manager.economy.defaultPublicPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        companyName.onValueChanged.AddListener(Changed);
        issuedShares.onValueChanged.AddListener(Changed);
        publicPercentage.onValueChanged.AddListener(Changed);
        confirm.onClick.AddListener(Confirm);
        StockManager.OnInitialEconomyReady += OnReady;
        listenersReady = true;
        if (retailIntroduction != null)
        { retailIntroduction.SetActive(manager.economy.retailPlayerStart); }
        if (companySetup != null)
        { companySetup.SetActive(!manager.economy.retailPlayerStart); }
        if (controlsToggle != null)
        { controlsToggle.onClick.AddListener(ToggleControls); controlsToggle.gameObject.SetActive(manager.economy.retailPlayerStart); }
        if (!manager.economy.retailPlayerStart)
        { confirm.GetComponentInChildren<TextMeshProUGUI>().text = "기업 설정하고 시작"; }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
        if (manager.economy.retailPlayerStart) { businessName = DailyIncomeUI.CompanyField(this); }
        Refresh();
    }
```

### Assets/Scripts/PlayerCompanySetupUI.cs · Refresh

원본:

```csharp
public void Refresh()
    {
        var manager = StockManager.Instance;
        bool valid = manager.PreviewPlayer(companyName.text, issuedShares.text, publicPercentage.text, out var data, out var reason);
        confirm.interactable = valid;
        error.text = reason;
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { startBalance.text = manager.economy.retailStartingCash.ToString("N0") + "원"; }
            preview.text = "보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.";
            if (retailPreview != null)
            { retailPreview.text = "첫 가입 · 새벽 관찰실"; }
            return;
        }
        preview.text = data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원";
    }
```

수정본:

```csharp
public void Refresh()
    {
        var manager = StockManager.Instance;
        bool valid = manager.PreviewPlayer(companyName.text, issuedShares.text, publicPercentage.text, out var data, out var reason);
        if (businessName != null && !DailyIncomeController.ValidName(businessName.text))
        { valid = false; reason = "회사 이름 · 공백·태그 없이 2~20자"; }
        confirm.interactable = valid;
        error.text = reason;
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { startBalance.text = manager.economy.retailStartingCash.ToString("N0") + "원"; }
            preview.text = "보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.";
            if (retailPreview != null)
            { retailPreview.text = "장 마감 회사 수익 +" + (DailyIncomeController.Instance?.Rules.companyIncome ?? 250000).ToString("N0") + "원"; }
            return;
        }
        preview.text = data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원";
    }
```

### Assets/Scripts/PlayerCompanySetupUI.cs · Confirm

원본:

```csharp
public void Confirm()
    {
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            error.text = reason;
            return;
        }

        confirm.interactable = false;
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        error.text = "초기 자산 설정 중…";
    }
```

수정본:

```csharp
public void Confirm()
    {
        if (businessName != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.NameCompany(businessName.text)))
        { error.text = "회사 이름을 확인하세요."; return; }
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            error.text = reason;
            return;
        }

        confirm.interactable = false;
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        error.text = "초기 자산 설정 중…";
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Start

원본:

```csharp
void Start()
    {
        ConfigureRoomTabs();
        publicRoom.onClick.AddListener(Public); roomOne.onClick.AddListener(One);
        roomTwo.onClick.AddListener(Two); roomThree.onClick.AddListener(Three);
        invite.onClick.AddListener(Invite); issue.onClick.AddListener(Issue); cancel.onClick.AddListener(Cancel);
        inviteLabel = invite.GetComponentInChildren<TextMeshProUGUI>();
        if (growth != null) { growth.onClick.AddListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.AddListener(ShowNpc); npcMembership.onClick.AddListener(Membership);
            npcBuy.onClick.AddListener(NpcBuy); npcSell.onClick.AddListener(NpcSell);
        }
    }
```

수정본:

```csharp
void Start()
    {
        ConfigureRoomTabs();
        DailyIncomeUI.InstallRoom(this);
        publicRoom.onClick.AddListener(Public); roomOne.onClick.AddListener(One);
        roomTwo.onClick.AddListener(Two); roomThree.onClick.AddListener(Three);
        invite.onClick.AddListener(Invite); issue.onClick.AddListener(Issue); cancel.onClick.AddListener(Cancel);
        inviteLabel = invite.GetComponentInChildren<TextMeshProUGUI>();
        if (growth != null) { growth.onClick.AddListener(ShowGrowth); }
        if (npcRoom != null)
        {
            npcRoom.onClick.AddListener(ShowNpc); npcMembership.onClick.AddListener(Membership);
            npcBuy.onClick.AddListener(NpcBuy); npcSell.onClick.AddListener(NpcSell);
        }
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs · Update

원본:

```csharp
void Update()
    {
        if (Time.unscaledTime < nextUpdate || StockManager.Instance == null || InvestorSociety.Instance == null)
        { return; }
        nextUpdate = Time.unscaledTime + .5f;
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        var stock = StockManager.Instance.CurrentStock;
        if (selected != stock && stock != null && !buyPrice.isFocused && !sellPrice.isFocused)
        {
            selected = stock;
            buyPrice.SetTextWithoutNotify(stock.currentPrice.ToString("0", CultureInfo.InvariantCulture));
            sellPrice.SetTextWithoutNotify(System.Math.Max(PriceTickPolicy.OneTickAbove(stock.currentPrice), PriceTickPolicy.RoundToTick(stock.currentPrice * 1.08m)).ToString("0", CultureInfo.InvariantCulture));
        }
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom);
        bool operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        roomOne.interactable = operate;
        roomTwo.interactable = roomThree.interactable = false;
        bool npcSelected = room != null && !room.PlayerOwned;
        bool ownSelected = room != null && room.PlayerOwned;
        if (playerControls != null)
        { playerControls.SetActive(!npcSelected && (lobbyControls == null || ownSelected && operate)); npcControls.SetActive(npcSelected); }
        if (lobbyControls != null)
        {
            lobbyControls.SetActive(!npcSelected && (!ownSelected || !operate));
            Set(lobbyStatus,operate ? InvestorSociety.Instance.InvitedRoomMask == 0 && Time.unscaledTime >= inviteFeedbackUntil ? "첫 회원 초대 · 닉네임 선택 → 내 리딩방" : InvestorSociety.Instance.LastAction : "내 리딩방 · 개설 전");
        }
        if (npcSelected && npcControls != null)
        {
            NpcDetails(); invite.interactable = false; return;
        }
        string title = room == null ? "공용 라운지 · 메시지를 눌러 참여자 선택" : $"내 리딩방 · 참여 {room.Members.Count}명";
        if (room != null && room.PlayerOwned) { title += $" · 경각심 {room.Social.discipline:0} · 위축 {room.Social.chill:0}"; }
        int waiting = 0, holding = 0, deferred = 0, rejected = 0, completed = 0, canceled = 0;
        if (InvestorSociety.Instance.Orders != null && room != null)
        {
            foreach (var plan in InvestorSociety.Instance.Orders.Plans)
            {
                if (plan.Command != room.Command)
                { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { completed++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { canceled++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { deferred++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { rejected++; }
                else if (plan.Remaining > 0m) { holding++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { waiting++; }
            }
        }
        string active = room?.Command == null || room.Command.Canceled ? "활성 지시 없음" : $"{room.Command.Stock.stockName}  매수 ≤ {room.Command.Buy:N0} / 매도 ≥ {room.Command.Sell:N0}";
        Set(commandStatus, title + "\n" + active + $"\n대기 {waiting} · 보유 {holding} · 보류 {deferred} · 거부 {rejected}\n완료 {completed} · 취소 {canceled}");
        Set(feedback, operate ? InvestorSociety.Instance.LastAction : "관전 가능 · 태블릿 2의 성장·권한에서 리딩방을 개설하세요.\n" + PlayerProgression.Instance.LockReason);
        var invitationRoom = room ?? InvestorSociety.Instance.GetRoom(1);
        bool firstInvite = invitationRoom.PlayerOwned && !invitationRoom.Social.firstMemberInvited;
        if (inviteLabel != null) { Set(inviteLabel, firstInvite ? "첫 회원 초대" : "초대"); }
        if (operate && firstInvite && Time.unscaledTime >= inviteFeedbackUntil)
        {
            var candidate = MarketDayController.Instance?.FindBot(invitationBot);
            Set(feedback, candidate == null ? "공용 라운지에서 닉네임 선택 → 내 리딩방 → 첫 회원 초대" : candidate.Social.Nickname + "님을 직접 초대하세요.\n첫 초대는 확정 가입 · 투자 지시는 각자 판단");
        }
        issue.interactable = operate && room != null && MarketDayController.CanTrade;
        cancel.interactable = room?.Command != null && !room.Command.Canceled;
        invite.interactable = operate && (chat.SelectedBotId > 0 || invitationBot > 0) && MarketDayController.CanTrade;
    }
```

수정본:

```csharp
void Update()
    {
        if (Time.unscaledTime < nextUpdate || StockManager.Instance == null || InvestorSociety.Instance == null)
        { return; }
        nextUpdate = Time.unscaledTime + .5f;
        if (chat.SelectedRoom == 0 && chat.SelectedBotId > 0) { invitationBot = chat.SelectedBotId; }
        var stock = StockManager.Instance.CurrentStock;
        if (selected != stock && stock != null && !buyPrice.isFocused && !sellPrice.isFocused)
        {
            selected = stock;
            buyPrice.SetTextWithoutNotify(stock.currentPrice.ToString("0", CultureInfo.InvariantCulture));
            sellPrice.SetTextWithoutNotify(System.Math.Max(PriceTickPolicy.OneTickAbove(stock.currentPrice), PriceTickPolicy.RoundToTick(stock.currentPrice * 1.08m)).ToString("0", CultureInfo.InvariantCulture));
        }
        var room = InvestorSociety.Instance.GetRoom(chat.SelectedRoom);
        bool operate = PlayerProgression.Instance == null || PlayerProgression.Instance.CanOperateRooms;
        roomOne.interactable = operate;
        roomTwo.interactable = roomThree.interactable = false;
        bool npcSelected = room != null && !room.PlayerOwned;
        bool ownSelected = room != null && room.PlayerOwned;
        if (playerControls != null)
        { playerControls.SetActive(!npcSelected && (lobbyControls == null || ownSelected && operate)); npcControls.SetActive(npcSelected); }
        if (lobbyControls != null)
        {
            lobbyControls.SetActive(!npcSelected && (!ownSelected || !operate));
            Set(lobbyStatus,operate ? InvestorSociety.Instance.InvitedRoomMask == 0 && Time.unscaledTime >= inviteFeedbackUntil ? "첫 회원 초대 · 닉네임 선택 → 내 리딩방" : InvestorSociety.Instance.LastAction : "내 리딩방 · 개설 전");
        }
        if (npcSelected && npcControls != null)
        {
            NpcDetails(); invite.interactable = false; return;
        }
        string title = room == null ? "공용 라운지 · 메시지를 눌러 참여자 선택" : $"내 리딩방 · 참여 {room.Members.Count}명";
        if (room != null && room.PlayerOwned) { title += $" · 경각심 {room.Social.discipline:0} · 위축 {room.Social.chill:0}"; }
        int waiting = 0, holding = 0, deferred = 0, rejected = 0, completed = 0, canceled = 0;
        if (InvestorSociety.Instance.Orders != null && room != null)
        {
            foreach (var plan in InvestorSociety.Instance.Orders.Plans)
            {
                if (plan.Command != room.Command)
                { continue; }
                if (plan.State == LeadingRoomOrders.Stage.Completed) { completed++; }
                else if (plan.State == LeadingRoomOrders.Stage.Canceled) { canceled++; }
                else if (plan.State == LeadingRoomOrders.Stage.Deferred) { deferred++; }
                else if (plan.State == LeadingRoomOrders.Stage.Rejected) { rejected++; }
                else if (plan.Remaining > 0m) { holding++; }
                else if (plan.State == LeadingRoomOrders.Stage.AwaitBuy || plan.State == LeadingRoomOrders.Stage.Buying) { waiting++; }
            }
        }
        string active = room?.Command == null || room.Command.Canceled ? "활성 지시 없음" : $"{room.Command.Stock.stockName}  매수 ≤ {room.Command.Buy:N0} / 매도 ≥ {room.Command.Sell:N0}";
        Set(commandStatus, title + "\n" + active + $"\n대기 {waiting} · 보유 {holding} · 보류 {deferred} · 거부 {rejected}\n완료 {completed} · 취소 {canceled}");
        Set(feedback, operate ? InvestorSociety.Instance.LastAction : "관전 가능 · 태블릿 2의 성장·권한에서 리딩방을 개설하세요.\n" + PlayerProgression.Instance.LockReason);
        var invitationRoom = room ?? InvestorSociety.Instance.GetRoom(1);
        bool firstInvite = invitationRoom.PlayerOwned && !invitationRoom.Social.firstMemberInvited;
        if (inviteLabel != null) { Set(inviteLabel, firstInvite ? "첫 회원 초대" : "초대"); }
        if (operate && firstInvite && Time.unscaledTime >= inviteFeedbackUntil)
        {
            var candidate = MarketDayController.Instance?.FindBot(invitationBot);
            Set(feedback, candidate == null ? "공용 라운지에서 닉네임 선택 → 내 리딩방 → 첫 회원 초대" : candidate.Social.Nickname + "님 초대 · 이용료와 투자 지시는 각자 판단");
        }
        issue.interactable = operate && room != null && MarketDayController.CanTrade;
        cancel.interactable = room?.Command != null && !room.Command.Canceled;
        invite.interactable = operate && (chat.SelectedBotId > 0 || invitationBot > 0) && MarketDayController.CanTrade;
    }
```

### Assets/Scripts/NightShift/TabletAccountUI.cs · Start

원본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        if (second) { SpreadTradingUI.Install(this); }
        ShowPage(second ? Page.Automation : Page.Dashboard);
    }
```

수정본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        if (second) { SpreadTradingUI.Install(this); }
        else { DailyIncomeUI.InstallTablet(this); }
        ShowPage(second ? Page.Automation : Page.Dashboard);
    }
```
