using System;
using UnityEngine;

// 플레이어가 쓰는 작전 도구 세 가지. 세력 봇이 쓰는 것과 같은 수법이고 같은 도구(ManipulationTools)를 쓴다.
// 내 1번 계좌의 실제 현금과 주식으로 실제 주문을 낸다. 가격·잔고·거래량을 지어내지 않는다.
//  - 거래량 부풀리기(통정매매): 차명 계좌와 같은 값에 주고받는다. 45초 동안 돈다. 그 종목 주식을 갖고 있어야 한다.
//  - 허수 매수벽: 현재가 2.5% 아래에 큰 매수 주문을 걸어 둔다. 값이 오르면 따라 올려 걸고, 값이 다가오면 거둔다. 못 거두면 진짜로 체결된다.
//  - 종가 관리: 예약해 두면 장 마감 40초 전부터 정한 돈만큼 사들인다.
//  - 불붙이기: 매도 호가를 한 번에 5%까지 쓸어 올린다. 남은 주문은 바로 거둔다. 그 뒤를 남들이 올리게 만드는 수법이다.
//  - 내려찍기: 가진 주식을 매수 호가에 한 번에 던져 6%까지 내린다. 남은 주문은 바로 거둔다(매도벽으로 남지 않는다).
public sealed class PlayerTactics : MonoBehaviour
{
    public static PlayerTactics Instance { get; private set; }
    public const float WashSeconds = 45f, CloseSeconds = 40f;
    public Stock WashStock { get; private set; }
    public Stock WallStock { get; private set; }
    public Stock CloseStock { get; private set; }
    public decimal WashPainted { get; private set; }
    public int WashRounds { get; private set; }
    public decimal WallPrice { get; private set; }
    public decimal WallShown => WallStock == null ? 0m : ManipulationTools.WallVolume(Account, WallStock, WallPrice) * WallPrice;
    public decimal CloseBudget { get; private set; }
    public decimal CloseSpent { get; private set; }
    public float WashLeft => WashStock == null ? 0f : Mathf.Max(0f, washUntil - SessionClock.Time);
    /// <summary>마지막으로 일어난 일(화면에 그대로 띄우는 한국어 열쇠. 화면이 언어에 맞게 바꾼다).</summary>
    public string Notice { get; private set; } = "";
    private float washUntil, washNext, wallNext, closeNext, wallFraction;
    private int closeDay = -1;
    private static Trader Account => PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
    private static decimal Free(Trader account) => account == null ? 0m : Math.Max(0m, account.cash - account.ReservedCash);

    public enum Tool { Close, Wash, Wall, Ignite, Slam }
    /// <summary>불붙이기가 쓸어 올리는 높이와 내려찍기가 받아 치는 깊이.</summary>
    public const decimal IgniteReach = .05m, SlamDepth = .06m;
    /// <summary>
    /// 작전 도구는 차례로 열린다. 종가 관리: 누적 거래대금이 기준(5억)을 넘으면. 거래량 부풀리기: 내 리딩방을 연 뒤. 허수 매수벽: 내 기업이 상장한 뒤.
    /// 진행 정보를 읽을 수 없을 때(검증용 장면 등)는 열려 있는 것으로 본다.
    /// </summary>
    public static bool Unlocked(Tool tool)
    {
        var career = PlayerProgression.Instance;
        switch (tool)
        {
            case Tool.Close: return career == null || career.Turnover >= career.rules.tacticCloseTurnover;
            // 불붙이기는 거래량 부풀리기와, 내려찍기는 허수 매수벽과 함께 열린다.
            case Tool.Wash: case Tool.Ignite: return career == null || career.CanOperateRooms;
            default:
                var company = CorporateEconomy.Instance != null ? CorporateEconomy.Instance.PlayerCompany : null;
                return CorporateEconomy.Instance == null || company != null && company.IsListed && company.business?.growth?.FounderBusiness == true;
        }
    }
    /// <summary>잠겨 있을 때 보여 줄 조건(한국어 열쇠).</summary>
    public static string LockReason(Tool tool) => tool == Tool.Close ? "누적 거래대금 5억 돈부터" : tool == Tool.Wash || tool == Tool.Ignite ? "내 리딩방을 연 뒤부터" : "내 기업이 상장한 뒤부터";

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    public bool StartWash(Stock stock)
    {
        var account = Account;
        if (!Unlocked(Tool.Wash)) { Notice = LockReason(Tool.Wash); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Notice = "거래 불가"; return false; }
        if (account.GetHolding(stock) < 1m) { Notice = "넘길 주식이 없습니다"; return false; }
        WashStock = stock; WashPainted = 0m; WashRounds = 0; washUntil = SessionClock.Time + WashSeconds; washNext = 0f; Notice = "거래량 부풀리기 시작";
        return true;
    }
    public void StopWash() { if (WashStock != null) { WashStock = null; Notice = "거래량 부풀리기 중지"; } }

    public bool SetWall(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Wall)) { Notice = LockReason(Tool.Wall); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Notice = "거래 불가"; return false; }
        PullWall(false);
        decimal price = ManipulationTools.PlaceWall(account, stock, Free(account) * (decimal)Mathf.Clamp01(fraction));
        if (price <= 0m) { Notice = "현금 부족"; return false; }
        WallStock = stock; WallPrice = price; wallFraction = fraction; wallNext = SessionClock.Time + 2f; Notice = "매수벽을 세웠습니다";
        return true;
    }
    public void PullWall(bool say = true)
    {
        if (WallStock == null) { return; }
        ManipulationTools.PullWall(Account, WallStock, WallPrice); WallStock = null; WallPrice = 0m;
        if (say) { Notice = "매수벽을 거뒀습니다"; }
    }

    /// <summary>불붙이기: 쓸 현금의 fraction만큼으로 매도 호가를 한 번에 쓸어 올린다.</summary>
    public bool Ignite(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Ignite)) { Notice = LockReason(Tool.Ignite); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Notice = "거래 불가"; return false; }
        decimal bought = ManipulationTools.Ignite(account, stock, Free(account) * (decimal)Mathf.Clamp01(fraction), IgniteReach);
        Notice = bought >= 1m ? "불을 붙였습니다" : "현금 부족";
        return bought >= 1m;
    }
    /// <summary>내려찍기: 가진 주식의 fraction만큼을 매수 호가에 한 번에 던진다.</summary>
    public bool Slam(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Slam)) { Notice = LockReason(Tool.Slam); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders || !MarketDayController.CanTrade) { Notice = "거래 불가"; return false; }
        decimal shares = decimal.Floor(account.GetHolding(stock) * (decimal)Mathf.Clamp01(fraction));
        if (shares < 1m) { Notice = "던질 주식이 없습니다"; return false; }
        decimal sold = ManipulationTools.Slam(account, stock, shares, SlamDepth, 0m);
        Notice = sold >= 1m ? "내려찍었습니다" : "받아 줄 호가가 없습니다";
        return sold >= 1m;
    }

    public bool ArmClose(Stock stock, float fraction)
    {
        var account = Account;
        if (!Unlocked(Tool.Close)) { Notice = LockReason(Tool.Close); return false; }
        if (account == null || stock == null || !stock.AcceptsOrders) { Notice = "거래 불가"; return false; }
        decimal budget = decimal.Floor(Free(account) * (decimal)Mathf.Clamp01(fraction));
        if (budget < stock.currentPrice) { Notice = "현금 부족"; return false; }
        CloseStock = stock; CloseBudget = budget; CloseSpent = 0m; closeDay = MarketDayController.Instance != null ? MarketDayController.Instance.Day : -1; Notice = "종가 관리를 예약했습니다";
        return true;
    }
    public void CancelClose() { if (CloseStock != null) { CloseStock = null; Notice = "종가 관리를 취소했습니다"; } }

    void Update()
    {
        var account = Account; var market = MarketDayController.Instance;
        if (account == null || market == null) { return; }
        float now = SessionClock.Time;
        if (WashStock != null)
        {
            if (now >= washUntil || !WashStock.AcceptsOrders || account.GetHolding(WashStock) < 1m) { WashStock = null; Notice = "거래량 부풀리기가 끝났습니다"; }
            else if (MarketDayController.CanTrade && now >= washNext)
            {
                washNext = now + 1.8f;
                decimal usual = StockInterestManager.Instance == null ? 0m : (decimal)StockInterestManager.Instance.UsualTurnover(WashStock);
                decimal equity = account.Equity();
                decimal painted = ManipulationTools.WashRound(account, WashStock, Math.Max(equity * .002m, Math.Min(equity * .03m, usual * 1.5m)));
                if (painted > 0m) { WashPainted += painted; WashRounds++; }
            }
        }
        if (WallStock != null && now >= wallNext)
        {
            wallNext = now + 2f;
            if (!WallStock.AcceptsOrders || !MarketDayController.CanTrade) { PullWall(false); Notice = "매수벽을 거뒀습니다"; }
            else if (ManipulationTools.WallVolume(account, WallStock, WallPrice) <= 0m) { WallStock = null; WallPrice = 0m; Notice = "매수벽이 체결되거나 사라졌습니다"; }
            else if (ManipulationTools.WallThreatened(WallStock, WallPrice)) { PullWall(false); Notice = "값이 다가와 매수벽을 거뒀습니다"; }
            else if (ManipulationTools.WallStale(WallStock, WallPrice))
            {
                // 값이 올라 벽이 멀어졌다. 거두고 더 높은 값에 다시 건다.
                var stock = WallStock; ManipulationTools.PullWall(account, stock, WallPrice);
                decimal price = ManipulationTools.PlaceWall(account, stock, Free(account) * (decimal)Mathf.Clamp01(wallFraction));
                if (price > 0m) { WallPrice = price; Notice = "매수벽을 올려 걸었습니다"; } else { WallStock = null; WallPrice = 0m; Notice = "현금 부족"; }
            }
        }
        if (CloseStock != null)
        {
            if (closeDay != market.Day && market.Phase != MarketPhase.Trading) { closeDay = market.Day; }
            bool closing = market.Phase == MarketPhase.Trading && market.RemainingSeconds <= CloseSeconds;
            if (closing && CloseStock.AcceptsOrders && now >= closeNext && CloseStock.orderBook != null && CloseStock.orderBook.TryBest(out _, out decimal ask))
            {
                closeNext = now + 1.5f;
                // 남은 시간에 고르게 나눠 산다. 마지막 몇 초에 몰아서 사면 호가만 비우고 끝난다.
                float stepsLeft = Mathf.Max(1f, market.RemainingSeconds / 1.5f);
                decimal step = Math.Min(Free(account), (CloseBudget - CloseSpent) / (decimal)stepsLeft * 1.6m);
                decimal quantity = decimal.Floor(step / ask);
                if (quantity >= 1m) { account.BuyLimit(PriceTickPolicy.CeilToTick(ask * 1.01m), quantity, 0, CloseStock); CloseSpent += quantity * ask; Notice = "종가 관리 중"; }
            }
            if (market.Phase != MarketPhase.Trading && CloseSpent > 0m) { CloseStock = null; Notice = "종가 관리가 끝났습니다"; }
        }
    }
}
