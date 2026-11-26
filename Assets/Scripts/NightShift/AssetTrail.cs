using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 내 총자산의 발자취. 자산 태블릿의 "오늘의 자산 곡선"과 기록 탭의 날짜별 곡선이 이것을 그린다.
///   - 오늘: 장이 열려 있는 동안 게임 시간 5분마다 한 점(09:00 시가부터). 하루가 바뀌면 비운다.
///   - 날마다: 장이 닫힐 때의 총자산 한 점. 최근 120일까지.
/// 총자산 = 현금 + 보유 주식 평가금 − 낼 세금(자산 태블릿의 총자산과 같은 계산).
/// 세이브에 들어간다(SessionStore의 RootNames). 기록만 하고 시장이나 계좌에는 손대지 않는다.
/// </summary>
public sealed class AssetTrail : MonoBehaviour
{
    [Serializable] public sealed class Point { public int at; public decimal value; }
    public static AssetTrail Instance { get; private set; }
    public const int StepMinutes = 5, KeepDays = 120;

    private readonly List<Point> today = new(110);
    private readonly List<Point> days = new(KeepDays);
    private int todayDay = -1, lastMinute = -1, closedDay = -1;
    private readonly List<Stock> owned = new(16);

    public IReadOnlyList<Point> Today => today;
    public IReadOnlyList<Point> Days => days;
    /// <summary>오늘 첫 점(시가)의 총자산. 아직 없으면 0.</summary>
    public decimal Opening => today.Count > 0 ? today[0].value : 0m;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    void Update()
    {
        var day = MarketDayController.Instance; var accounts = PlayerAccounts.Instance;
        if (day == null || !day.Ready || accounts == null || !accounts.Ready || accounts.primary == null || StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (todayDay != day.Day) { todayDay = day.Day; today.Clear(); lastMinute = -1; }
        int minute = day.ClockMinute;
        if (day.Phase == MarketPhase.Trading)
        {
            int slot = minute / StepMinutes * StepMinutes;
            if (slot != lastMinute) { lastMinute = slot; Add(today, slot, Total(accounts.primary), 200); }
        }
        else if (day.Phase == MarketPhase.PostClose && closedDay != day.Day && today.Count > 0)
        {
            closedDay = day.Day;
            Add(today, minute, Total(accounts.primary), 200);
            Add(days, day.Day, today[today.Count - 1].value, KeepDays);
        }
    }

    private static void Add(List<Point> list, int at, decimal value, int keep)
    {
        if (list.Count > 0 && list[list.Count - 1].at == at) { list[list.Count - 1].value = value; return; }
        list.Add(new Point { at = at, value = value });
        if (list.Count > keep) { list.RemoveAt(0); }
    }

    /// <summary>계좌의 총자산(자산 태블릿과 같은 계산). 값이 아직 없는 종목이 있으면 그 종목은 빼고 센다.</summary>
    public decimal Total(Trader account)
    {
        if (account == null) { return 0m; }
        account.CopyOwnedStocks(owned);
        decimal stocks = 0m;
        foreach (var stock in owned) { if (stock != null && stock.currentPrice > 0m) { stocks += account.GetOwnedVolume(stock) * stock.currentPrice; } }
        return account.cash + stocks - PlayerAccounts.TaxDebt(account);
    }
}
