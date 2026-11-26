using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 플레이어의 계좌. 계좌는 하나다(primary).
///
/// 예전에는 계좌가 둘이었다: 1번(내 투자)과, 총자산 1억 돈을 넘기면 열리는 2번(자동 운용). 스프레드·매집은 2번 계좌의 돈으로만 돌았고
/// 두 계좌 사이에 돈을 옮겨야 했다. 이제 모든 주문과 작전이 한 계좌에서 나간다. 옮길 것도, 열 것도 없다.
///
/// 장면에는 두 번째 계좌(automated)와 그 주문 창구(automatedOrders)가 아직 놓여 있고 여러 화면이 그것을 가리킨다.
/// 시작할 때 그 연결을 모두 본계좌로 돌려 놓는다(Rebind). 두 번째 계좌는 빈 채로 남아 아무 데도 쓰이지 않는다.
/// 예전 저장을 불러와 두 번째 계좌에 돈이나 주식이 들어 있으면 본계좌로 옮긴다(Sweep).
/// </summary>
[DefaultExecutionOrder(-80)]
public sealed class PlayerAccounts : MonoBehaviour
{
    public static PlayerAccounts Instance { get; private set; }
    public Trader primary, automated;
    public DeviceOrderSession primaryOrders, automatedOrders;
    public long unlockAssets = 100000000;
    /// <summary>예전의 "2번 계좌가 열렸는가". 이제 잠긴 것이 없으므로 언제나 참이다(이 값을 보던 곳들이 그대로 돌아가게 남겨 둔다).</summary>
    public bool AutomatedUnlocked => true;
#pragma warning disable CS0067 // 예전 화면들이 구독한다. 이제는 울리지 않는다.
    public event Action Unlocked;
#pragma warning restore CS0067
    private float next;
    public bool Ready => StockManager.Instance != null && StockManager.Instance.SetupConfirmed && StockManager.Instance.EconomyInitialized;
    void Awake()
    {
        Instance = this;
        if (automated != null) { automated.cash = 0m; automated.InitialCashAssigned = true; }
        Rebind();
    }
    /// <summary>두 번째 계좌나 그 주문 창구를 가리키던 화면·기능을 모두 본계좌로 돌린다. 장면 파일은 건드리지 않는다.</summary>
    private void Rebind()
    {
        if (primary == null || automated == null || primary == automated) { return; }
        int moved = 0;
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour == this || behaviour is Trader) { continue; }
            foreach (var field in behaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(Trader) && ReferenceEquals(field.GetValue(behaviour), automated)) { field.SetValue(behaviour, primary); moved++; }
                else if (field.FieldType == typeof(DeviceOrderSession) && automatedOrders != null && primaryOrders != null && behaviour != automatedOrders && ReferenceEquals(field.GetValue(behaviour), automatedOrders))
                { field.SetValue(behaviour, primaryOrders); moved++; }
            }
        }
        Debug.Log($"[계좌] 계좌를 하나로 합쳤습니다 — 본계좌로 돌린 연결 {moved}곳");
    }
    void Update()
    {
        if (Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + 1f;
        // 저장을 불러오면 화면들의 연결이 저장된 대로(예전 저장이면 두 번째 계좌로) 되돌아갈 수 있다. 그때는 다시 돌려 놓는다.
        var operation = TradingOperationController.Instance;
        if (operation != null && automated != null && operation.account == automated) { Rebind(); }
        Sweep();
    }
    /// <summary>예전에는 총자산을 보고 2번 계좌를 열었다. 이제 할 일이 없다(점검 도구가 부르는 자리만 남긴다).</summary>
    public void EvaluateUnlock() { }
    /// <summary>예전 저장의 두 번째 계좌에 남은 돈과 주식을 본계좌로 옮긴다. 비어 있으면 아무 일도 없다.</summary>
    private void Sweep()
    {
        if (!Ready || primary == null || automated == null || primary == automated) { return; }
        var manager = StockManager.Instance; decimal cash = automated.cash - automated.ReservedCash, shares = 0m;
        // 걸려 있는 주문이 있으면 먼저 거둔다(묶인 돈과 주식이 풀려야 옮길 수 있다).
        foreach (var stock in manager.stocks) { if (automated.GetOwnedVolume(stock) != automated.GetHolding(stock) || automated.ReservedCash > 0m) { stock.orderBook?.CancelAllOrdersByOwner(automated, stock); } }
        cash = automated.cash - automated.ReservedCash;
        if (cash > 0m) { automated.ApplyTradeCash(-cash); primary.ApplyTradeCash(cash); }
        void Move(Stock stock)
        {
            decimal held = stock == null ? 0m : automated.GetHolding(stock);
            if (held <= 0m) { return; }
            decimal basis = automated.GetAvgPrice(stock);
            automated.ForceRemoveHolding(stock, held); primary.GrantShares(stock, held, basis > 0m ? basis : stock.currentPrice);
            shares += held;
        }
        foreach (var stock in manager.stocks) { Move(stock); }
        foreach (var stock in manager.UnlistedStocks) { Move(stock); }
        if (cash > 0m || shares > 0m) { Debug.Log($"[계좌] 예전 2번 계좌를 본계좌로 옮겼습니다 — 현금 {Math.Max(0m, cash):N0}돈 · 주식 {shares:N0}주"); }
    }
    public bool CanUse(Trader account) => Ready && (account == primary || account == automated);
    public static bool IsPlayer(IOrderOwner owner)
    {
        if (owner == null)
        { return false; }
        return Instance != null ? ReferenceEquals(owner, Instance.primary) || ReferenceEquals(owner, Instance.automated) : StockManager.Instance != null && ReferenceEquals(owner, StockManager.Instance.playerAccount);
    }
    public decimal Owned(Stock stock) => primary.GetOwnedVolume(stock) + (automated == null || automated == primary ? 0m : automated.GetOwnedVolume(stock));
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
    /// <summary>계좌가 하나라 옮길 곳이 없다. 예전 화면이 부르던 자리만 남겨 둔다.</summary>
    public bool Transfer(Trader from, decimal amount, out string reason)
    { reason = "계좌가 하나로 합쳐져 이체가 필요 없습니다."; return false; }
    void OnDestroy()
    { if (Instance == this) { Instance = null; } }
}
