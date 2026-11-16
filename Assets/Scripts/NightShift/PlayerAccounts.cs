using System;
using UnityEngine;

/// <summary>Two finite player ledgers. Unlocking grants access, never cash or shares.</summary>
[DefaultExecutionOrder(-80)]
public sealed class PlayerAccounts : MonoBehaviour
{
    public static PlayerAccounts Instance { get; private set; }
    public Trader primary, automated;
    public DeviceOrderSession primaryOrders, automatedOrders;
    public long unlockAssets = 100000000;
    public bool AutomatedUnlocked { get; private set; }
    public event Action Unlocked;
    private float next;
    public bool Ready => StockManager.Instance != null && StockManager.Instance.SetupConfirmed && StockManager.Instance.EconomyInitialized;
    void Awake()
    {
        Instance = this;
        if (automated != null) { automated.cash = 0m; automated.InitialCashAssigned = true; }
    }
    void Update()
    {
        if (AutomatedUnlocked || Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .5f;
        EvaluateUnlock();
    }
    public void EvaluateUnlock()
    {
        if (!Ready || AutomatedUnlocked || primary == null || primary.Equity() - TaxDebt(primary) <= unlockAssets)
        { return; }
        AutomatedUnlocked = true;
        Unlocked?.Invoke();
        MarketEventFeed.Instance?.Notify("2번 계좌가 해금되었습니다. 계좌 간 현금 이체와 자동 운용을 사용할 수 있습니다.");
    }
    public bool CanUse(Trader account) => Ready && (account == primary || account == automated && AutomatedUnlocked);
    public static bool IsPlayer(IOrderOwner owner)
    {
        if (owner == null)
        { return false; }
        return Instance != null ? ReferenceEquals(owner, Instance.primary) || ReferenceEquals(owner, Instance.automated) : StockManager.Instance != null && ReferenceEquals(owner, StockManager.Instance.playerAccount);
    }
    public decimal Owned(Stock stock) => primary.GetOwnedVolume(stock) + automated.GetOwnedVolume(stock);
    public static decimal TaxDebt(Trader account)
    {
        bool primaryAccount = account != null && (Instance != null ? account == Instance.primary : StockManager.Instance != null && account == StockManager.Instance.playerAccount);
        return primaryAccount ? DailyIncomeController.Instance?.Ledger.UnpaidTax ?? 0m : 0m;
    }
    public static decimal Available(Trader account)
    {
        if (account==null)
        { return 0m; }
        return Math.Max(0m,account.cash-account.ReservedCash-TaxDebt(account)-ScheduledCash(account));
    }
    public static decimal ScheduledCash(Trader account)
    {
        var session=Instance==null ? null : account==Instance.primary ? Instance.primaryOrders : account==Instance.automated ? Instance.automatedOrders : null;
        return session?.ScheduledBuyCost ?? 0m;
    }
    public bool Transfer(Trader from, decimal amount, out string reason)
    {
        if (!AutomatedUnlocked || !CanUse(from))
        { reason = "2번 계좌 해금 후 이체할 수 있습니다."; return false; }
        if (amount < 1m || amount != decimal.Floor(amount) || amount > Available(from))
        { reason = "예약금을 제외한 주문 가능 현금 안에서 입력하세요."; return false; }
        Trader to = from == primary ? automated : primary;
        from.cash -= amount; to.cash += amount;
        reason = amount.ToString("N0") + "원 이체 완료";
        return true;
    }
    void OnDestroy()
    { if (Instance == this) { Instance = null; } }
}
