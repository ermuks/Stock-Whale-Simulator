using UnityEditor;
using UnityEngine;

// 점검용 메뉴: 플레이 중에 플레이어의 본계좌에 현금을 넣는다.
// DonDon > 플레이어 입금 > 1억 … 100억.
// 넣은 돈은 바깥에서 들어온 돈으로 적힌다(Trader.cash의 ExternalFlow). 매매로 번 돈으로 세지 않는다.
// 플레이 중이 아니거나 계좌가 아직 준비되지 않았으면 메뉴가 흐리게 꺼져 있다.
public static class PlayerDepositMenu
{
    private const string Root = "DonDon/플레이어 입금/";
    private const decimal Unit = 100_000_000m;   // 1억

    private static Trader Player => Application.isPlaying && PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : null;
    private static bool Ready() => Player != null;

    private static void Deposit(int units)
    {
        var player = Player;
        if (player == null) { Debug.LogWarning("[입금] 플레이 중이 아니거나 플레이어 계좌가 아직 없습니다."); return; }
        decimal amount = units * Unit, before = player.cash;
        player.cash += amount;
        Debug.Log($"[입금] 플레이어 본계좌에 {units}억 원 입금 — {before:N0} → {player.cash:N0}");
    }

    [MenuItem(Root + "1억", false, 100)] private static void Deposit1() => Deposit(1);
    [MenuItem(Root + "2억", false, 101)] private static void Deposit2() => Deposit(2);
    [MenuItem(Root + "5억", false, 102)] private static void Deposit5() => Deposit(5);
    [MenuItem(Root + "10억", false, 103)] private static void Deposit10() => Deposit(10);
    [MenuItem(Root + "20억", false, 104)] private static void Deposit20() => Deposit(20);
    [MenuItem(Root + "50억", false, 105)] private static void Deposit50() => Deposit(50);
    [MenuItem(Root + "100억", false, 106)] private static void Deposit100() => Deposit(100);

    [MenuItem(Root + "1억", true)] private static bool Ready1() => Ready();
    [MenuItem(Root + "2억", true)] private static bool Ready2() => Ready();
    [MenuItem(Root + "5억", true)] private static bool Ready5() => Ready();
    [MenuItem(Root + "10억", true)] private static bool Ready10() => Ready();
    [MenuItem(Root + "20억", true)] private static bool Ready20() => Ready();
    [MenuItem(Root + "50억", true)] private static bool Ready50() => Ready();
    [MenuItem(Root + "100억", true)] private static bool Ready100() => Ready();
}
