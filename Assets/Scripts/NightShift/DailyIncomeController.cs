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
        CompanyName = name; GameLocale.ProtectLiteral(name); return true;
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
            bot.Social.Psychology.NextMembership = SessionClock.Time + 60f;
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
            decimal equity = account.Equity() + (PlayerAccounts.Instance?.automated == null ? 0m : PlayerAccounts.Instance.automated.Equity());
            decimal available = Math.Max(0m,account.cash - account.ReservedCash);
            bool managed=CorporateEconomy.Instance?.PlayerCompany?.business?.growth?.FounderBusiness==true;
            var report = Ledger.CloseDay(day,managed ? 0 : Rules.companyIncome,realized,equity,available,CorporateEconomy.Instance?.PlayerDividends(day) ?? 0);
            if (report == null)
            { yield break; }
            // 외부 사업 수익/회원료 입금은 매매 수익률 및 리딩방 해금용 실현손익으로 계산하지 않는다.
            account.cash += report.Transfer;
            MarketEventFeed.Instance?.Notify("장 마감 정산 " + InvestmentDashboard.Signed(report.Transfer) + "원");
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
        CompanyName = saved.company; GameLocale.ProtectLiteral(saved.company);
        realizedAtOpen = decimal.Parse(saved.realizedAtOpen,CultureInfo.InvariantCulture); return true;
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
}
