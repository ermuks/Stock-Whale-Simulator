using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>실제 플레이어 체결로 얻는 첫 운영 권한. 시세·봇 심리·계좌를 직접 변경하지 않는다.</summary>
public sealed partial class PlayerProgression : MonoBehaviour
{
    [Serializable]
    public sealed class Rules
    {
        public int schemaVersion = 1;
        // 리딩방을 여는 데 한 번 내는 돈.
        public long leadingOpeningCost = 100000000;
        // 작전 도구 '종가 관리'가 열리는 누적 거래대금.
        public long tacticCloseTurnover = 500000000;
        // 내 비상장기업을 세우는 데 드는 돈. 회사의 첫 값어치를 뺀 나머지는 그 회사의 자본금으로 들어간다.
        public long companyFoundingCost = 300000000;
        // 새벽 관찰실: 여는 데 한 번 내는 돈, 열 수 있는 누적 거래대금, 들어가 있는 동안 하루마다 내는 이용료.
        public long dawnRoomCost = 3000000, dawnRoomTurnover = 30000000, dawnRoomDailyFee = 100000;
    }
    [Serializable]
    public sealed class Snapshot
    {
        public int version = 1;
        public string worldId, turnover, realizedProfit;
        public bool roomsOpened;
        /// <summary>엔딩을 보고 꿈에서 깼다. 예전 저장에는 없어서 false로 읽힌다.</summary>
        public bool dreamt;
        /// <summary>내 기업을 세웠다. 예전 저장에는 없어서 false로 읽히지만, 이미 기업이 있으면 불러올 때 true로 고쳐진다.</summary>
        public bool companyFounded;
        /// <summary>새벽 관찰실을 열었는가, 마지막으로 이용료를 낸 날.</summary>
        public bool dawnUnlocked;
        public int dawnFeeDay;
        public int invitedRoomMask;
        public int journeyMilestones;
        public double newsCooldownRemaining;
        public long newsSavedUtc;
        public DailyIncomeController.SaveData income;
    }
    public static PlayerProgression Instance { get; private set; }
    public Rules rules = new();
    public decimal Turnover { get; private set; }
    public decimal RealizedProfit { get; private set; }
    public bool CanOperateRooms { get; private set; }
    /// <summary>엔딩 뒤 꿈에서 깼다. 리딩방도 내 기업도 처음부터 없던 것이다(내 기업을 다시 만들어 주지 않는다).</summary>
    public bool Dreamt { get; private set; }
    /// <summary>꿈에서 깬다: 리딩방 운영 자격이 사라진다. 다시 열려면 개설 비용을 다시 모아야 한다.</summary>
    public void WakeFromDream() { Dreamt = true; CanOperateRooms = false; CompanyFounded = false; }
    /// <summary>내 비상장기업을 세웠는가. 처음에는 기업이 없고, 설립 비용을 내야 생긴다.</summary>
    public bool CompanyFounded { get; private set; }
    public decimal FoundingCost => rules.companyFoundingCost;
    public void MarkCompanyFounded(bool founded) { CompanyFounded = founded; }
    /// <summary>새벽 관찰실을 열었는가. 열려면 누적 거래대금이 기준을 넘어야 하고 한 번 돈을 낸다.</summary>
    public bool DawnUnlocked { get; private set; }
    public int DawnFeeDay { get; private set; }
    public bool DawnEligible => Turnover >= rules.dawnRoomTurnover;
    /// <summary>돈을 내고 새벽 관찰실을 연다. 조건이 안 되거나 돈이 모자라면 false.</summary>
    public bool UnlockDawn(Trader payer)
    {
        if (DawnUnlocked) { return true; }
        if (payer == null || !DawnEligible || PlayerAccounts.Available(payer) < rules.dawnRoomCost) { return false; }
        payer.cash -= rules.dawnRoomCost; DawnUnlocked = true;
        return true;
    }
    /// <summary>오늘 치 이용료를 낸다. 이미 냈으면 아무 일도 없다. 돈이 모자라면 false(방에서 나가야 한다).</summary>
    public bool PayDawnFee(Trader payer, int day)
    {
        if (DawnFeeDay >= day) { return true; }
        if (payer == null || PlayerAccounts.Available(payer) < rules.dawnRoomDailyFee) { return false; }
        payer.cash -= rules.dawnRoomDailyFee; DawnFeeDay = day;
        return true;
    }
    public int JourneyMilestones { get; private set; }
    public decimal OpeningCost => rules.leadingOpeningCost;
    private static Trader OpeningAccount => PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
    /// <summary>개설 비용으로 쓸 수 있는 돈: 1번 계좌의 주문 가능 현금.</summary>
    public decimal OpeningCash => PlayerAccounts.Available(OpeningAccount);
    public bool Eligible => OpeningCash >= OpeningCost;
    public string WorldId { get; private set; }
    public string LockReason => "리딩방 개설 필요 · 개설 비용 " + OpeningCost.ToString("N0") + "원";
    private readonly HashSet<Stock> watched = new();
    void Awake()
    {
        Instance = this; WorldId = Guid.NewGuid().ToString("N");
        if (GetComponent<DailyIncomeController>() == null) { gameObject.AddComponent<DailyIncomeController>(); }
        var data = Resources.Load<TextAsset>("Progression/PlayerProgression");
        if (data != null)
        {
            var candidate = JsonUtility.FromJson<Rules>(data.text);
            if (candidate != null && candidate.schemaVersion == 1 && candidate.leadingOpeningCost > 0)
            { rules = candidate; }
        }
    }
    void OnEnable()
    { StockManager.OnStockListed += Register; StockManager.OnStockDelisted += Remove; }
    void Start()
    {
        if (StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks)
        { Register(stock); }
    }
    private void Register(Stock stock)
    {
        if (stock==null) { return; }
        watched.Add(stock); stock.OnMatchedTrade -= Trade; stock.OnMatchedTrade += Trade;
    }
    private void Remove(Stock stock)
    {
        if (watched.Remove(stock))
        { stock.OnMatchedTrade -= Trade; }
    }
    private void Trade(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        bool playerBuy = PlayerAccounts.IsPlayer(buyer), playerSell = PlayerAccounts.IsPlayer(seller);
        if (playerBuy == playerSell || resting == null || volume <= 0m)
        { return; }
        Turnover += price * volume;
        if (playerSell && seller is Trader player)
        {
            decimal basis = ReferenceEquals(resting.owner, player) ? resting.costBasis : player.SubmittedSellCost;
            RealizedProfit += (price - basis) * volume;
        }
    }
    public bool OpenLeadingRooms()
    {
        if (CanOperateRooms)
        { return true; }
        var account = OpeningAccount;
        if (account == null || !Eligible)
        { return false; }
        account.cash -= OpeningCost;
        CanOperateRooms = true;
        MarketEventFeed.Instance?.Notify("리딩방 개설 완료 · 공용 라운지 채팅에서 + 를 눌러 회원을 초대하세요.");
        return true;
    }
    public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms, dreamt = Dreamt, companyFounded = CompanyFounded, dawnUnlocked = DawnUnlocked, dawnFeeDay = DawnFeeDay,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, journeyMilestones = JourneyMilestones, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks,
            income = DailyIncomeController.Instance?.Capture() });
    }
    public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount || saved.journeyMilestones < 0 || saved.journeyMilestones > 7 ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        if (saved.income != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.CanRestore(saved.income,WorldId)))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened; Dreamt = saved.dreamt; CompanyFounded = saved.companyFounded; DawnUnlocked = saved.dawnUnlocked; DawnFeeDay = saved.dawnFeeDay;
        JourneyMilestones = saved.journeyMilestones;
        if (saved.income != null) { DailyIncomeController.Instance.Restore(saved.income,WorldId); }
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
    /// <summary>Presentation history only: a completed paper goal never returns after later losses or leaving a room.</summary>
    public void RememberJourneyMilestones(bool joinedNpcRoom)
    {
        if (joinedNpcRoom) { JourneyMilestones |= 1; }
        if (Eligible || CanOperateRooms) { JourneyMilestones |= 2; }
        if (CanOperateRooms) { JourneyMilestones |= 4; }
    }
    void OnDisable()
    {
        StockManager.OnStockListed -= Register; StockManager.OnStockDelisted -= Remove;
        foreach (var stock in watched)
        { stock.OnMatchedTrade -= Trade; }
        watched.Clear();
    }
    void OnDestroy()
    {
        if (Instance == this)
        { Instance = null; }
    }
}
