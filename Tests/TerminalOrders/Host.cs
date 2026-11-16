namespace UnityEngine { public sealed class HideInInspector : System.Attribute { } }
public sealed partial class StockManager { public Trader playerAccount; }
public sealed class PlayerAccounts
{
    public static PlayerAccounts Instance;
    public Trader automated;
    public bool AutomatedUnlocked = true;
    public static decimal ScheduledCash(Trader account) => 0m;
    public static decimal Available(Trader account) => account == null ? 0m : System.Math.Max(0m,account.cash-account.ReservedCash);
}
public static class DeviceMarketJournal
{ public static string FullStamp() => "DAY 1 " + ((int)UnityEngine.Time.time / 60).ToString("00") + ":" + ((int)UnityEngine.Time.time % 60).ToString("00"); }
