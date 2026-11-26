using System.Globalization;
using UnityEngine;

/// <summary>
/// 새 게임을 시작한다(회사 이름을 정하고, 첫 자산을 나눠 주고, 시장을 연다).
/// 예전에는 장면에 놓인 시작 창(DON DON)이 이 일을 맡았다. 그 창은 없어졌고, 이름은 타이틀의 새 게임 화면이 받는다.
/// 점검 도구도 창의 단추를 누르는 대신 여기를 부른다.
/// </summary>
public static class NewGame
{
    /// <summary>예전의 기본 이름. 저장된 이름이 이것이면 아직 이름을 정하지 않은 것으로 본다.</summary>
    private const string LegacyCompany = "내 작은 회사";
    /// <summary>
    /// 새 게임 화면에 처음 적혀 있는 회사 이름. 내 회사는 작전을 벌이는 데 쓰는 범죄법인이다. 겉으로는 더없이 멀쩡한 이름을 단다.
    /// </summary>
    public static string DefaultCompany => GameLocale.Code == "ja" ? "合法投資(株)" : GameLocale.Code == "en" ? "Totally Legal Inc." : "합법투자(주)";

    /// <summary>지금 새 게임을 시작할 수 있는가(아직 시작하지 않았고, 시작하는 중도 아니다).</summary>
    public static bool CanBegin
    {
        get
        {
            var manager = StockManager.Instance;
            if (manager == null || manager.economy == null || !manager.useInitialEconomy || manager.EconomyInitialized || manager.SetupInProgress) { return false; }
            return !manager.economy.retailPlayerStart || DailyIncomeController.Instance != null;
        }
    }

    /// <summary>새 게임 화면에 처음 적어 둘 회사 이름.</summary>
    public static string SuggestedCompany => DailyIncomeController.Instance != null && DailyIncomeController.ValidName(DailyIncomeController.Instance.CompanyName) && DailyIncomeController.Instance.CompanyName != LegacyCompany ? DailyIncomeController.Instance.CompanyName : DefaultCompany;

    public static bool Begin(string company = null) => Begin(company, out _);

    /// <summary>회사 이름을 정하고 시작한다. 이름이 맞지 않거나 시작할 수 없으면 false와 까닭을 돌려준다.</summary>
    public static bool Begin(string company, out string reason)
    {
        reason = "";
        var manager = StockManager.Instance;
        if (!CanBegin) { reason = "이미 확정한 초기 설정입니다."; return false; }
        var first = manager.economy.companies[0];
        if (manager.economy.retailPlayerStart)
        {
            string name = string.IsNullOrEmpty(company) ? SuggestedCompany : company;
            if (!DailyIncomeController.ValidName(name)) { reason = "회사 이름 · 공백·태그 없이 2~20자"; return false; }
            if (!manager.PreviewPlayer(first.name, Number(first.issuedShares), Number(manager.economy.defaultPublicPercentage), out _, out reason)) { return false; }
            if (!DailyIncomeController.Instance.NameCompany(name)) { reason = "회사 이름을 확인하세요."; return false; }
        }
        if (!manager.ConfirmPlayer(first.name, Number(first.issuedShares), Number(manager.economy.defaultPublicPercentage), out reason)) { return false; }
        if (!manager.economy.retailPlayerStart) { GameLocale.ProtectLiteral(first.name); }
        // 내 회사는 처음부터 세워져 있다. 세계가 준비되는 대로 설립 비용을 치르고 연다.
        PlayerProgression.FoundOnStart = true;
        NpcLeadingRoom.Instance?.RequestJoin();
        StockManager.OnInitialEconomyReady -= Ready; StockManager.OnInitialEconomyReady += Ready;
        return true;
    }

    private static string Number(System.IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

    // 세계가 준비되면 걸어 다니는 시점으로 넘긴다(예전 시작 창이 닫히며 하던 일).
    private static void Ready()
    {
        StockManager.OnInitialEconomyReady -= Ready;
        var mouse = Object.FindAnyObjectByType<MouseCameraController>();
        if (mouse != null) { mouse.CompleteInitialSetup(); }
    }
}
