using System;
using UnityEngine;

// 세력이 작전 중에 곁들이는 수법들. 작전의 큰 줄기(매집 → 끌어올리기 → 흔들기 → 정리)는 WhaleOperationPlan이 정하고,
// 여기서는 그 단계에 맞춰 눈속임을 얹는다. 모두 자기 계좌의 돈과 주식으로 실제 주문을 낸다.
//
// - 통정매매: 끌어올리기 직전(매집의 마지막 20%)과, 끌어올리는데 아무도 안 따라붙을 때. 차명 계좌와 주고받아 거래량을 부풀려 눈길을 끈다.
// - 허수 매수벽: 끌어올리는 동안 현재가 아래에 큰 매수 주문을 걸어 "받치는 돈"처럼 보이게 한다. 흔들기·정리에 들어가는 순간 거둔다.
// - 종가 관리: 끌어올리는 날의 마감 30초 동안 사들여 종가를 높게 찍는다. 다음 날 등락률과 기준가가 유리해진다.
public partial class WhaleBot
{
    /// <summary>통정매매 한 바퀴에 찍는 거래대금: 그 종목의 평소 5초 거래대금의 이 배수. 작전 예산의 0.2%~2% 사이로 자른다.</summary>
    private const double WashUsualMultiple = 1.5;
    private const decimal WashBudgetMin = .002m, WashBudgetMax = .02m;
    /// <summary>따라붙는 열기가 이보다 낮을 때만 거래량을 부풀린다. 이미 뜨거우면 필요 없다.</summary>
    private const double WashBelowHeat = .3;
    /// <summary>허수 매수벽에 보여 줄 돈: 작전에 쓸 돈을 빼고 남는 현금의 60%, 작전 예산의 절반까지.</summary>
    private const decimal WallIdleShare = .6m, WallBudgetShare = .5m, WallMinimumShare = .05m;
    /// <summary>종가 관리: 마감 이만큼 전부터, 작전 예산의 6%까지.</summary>
    private const float CloseSeconds = 30f; private const decimal CloseBudgetShare = .06m, CloseStepShare = .006m;
    private float washNext, wallNext, closeNext;
    private decimal wallPrice, closeSpent, washPainted;
    private int closeDay = -1, washRounds, wallsPlaced;
    private bool washLogged, wallLogged, closeLogged;
    /// <summary>확인용: 세력 전체가 쓴 수법의 횟수와 금액.</summary>
    public static int WashRoundsTotal, WallsPlacedTotal, WallsPulledTotal, CloseBuysTotal; public static decimal WashPaintedTotal;

    private void TickTactics(Stock stock, float now)
    {
        int stage = operation.stage;
        bool driving = stage == WhaleOperationState.Drive || stage == WhaleOperationState.Repump;
        // 흔들기·정리에 들어가면 받치던 벽부터 거둔다. 벽을 믿고 들어온 사람들 위로 물량이 쏟아진다.
        if (!driving && wallPrice > 0m) { ManipulationTools.PullWall(this, stock, wallPrice); wallPrice = 0m; WallsPulledTotal++; }
        if (!MarketDayController.CanTrade) { return; }

        bool igniting = stage == WhaleOperationState.Build && operation.stageSeconds >= operation.buildSeconds * .8f;
        if ((igniting || driving && OperationHeat < WashBelowHeat) && now >= washNext)
        {
            washNext = now + 1.6f + (float)Roll() * 1.4f;
            decimal usual = StockInterestManager.Instance == null ? 0m : (decimal)StockInterestManager.Instance.UsualTurnover(stock);
            decimal value = Math.Max(operation.budget * WashBudgetMin, Math.Min(operation.budget * WashBudgetMax, usual * (decimal)WashUsualMultiple));
            decimal painted = ManipulationTools.WashRound(this, stock, value);
            if (painted > 0m)
            {
                washRounds++; washPainted += painted; WashRoundsTotal++; WashPaintedTotal += painted;
                if (!washLogged) { washLogged = true; Debug.Log($"[세력] 수법 — 통정매매 시작: {stock.stockName}({stock.symbol}) 한 바퀴 {painted:N0}원, 평소 5초 거래대금 {usual:N0}원"); }
            }
        }

        if (driving && now >= wallNext)
        {
            wallNext = now + 3f;
            if (wallPrice > 0m && (ManipulationTools.WallThreatened(stock, wallPrice) || ManipulationTools.WallStale(stock, wallPrice) || ManipulationTools.WallVolume(this, stock, wallPrice) <= 0m))
            { ManipulationTools.PullWall(this, stock, wallPrice); wallPrice = 0m; WallsPulledTotal++; }
            if (wallPrice <= 0m)
            {
                // 불붙이는 데 쓸 돈(예산의 30%)은 남기고, 나머지 현금을 값 바로 아래에 받치는 돈으로 보여 준다. 개미는 그 벽을 보고 서둘러 산다.
                decimal idle = cash - ReservedCash - operation.budget * .3m;
                decimal show = Math.Min(idle * WallIdleShare, operation.budget * WallBudgetShare);
                // 벽으로 보이지 않을 만큼 작은 돈이면 걸지 않는다(개미들이 벽으로 치는 최소 금액).
                if (show >= operation.budget * WallMinimumShare && show >= ManipulationTools.WallFloor(stock))
                {
                    wallPrice = ManipulationTools.PlaceWall(this, stock, show);
                    if (wallPrice > 0m)
                    {
                        wallsPlaced++; WallsPlacedTotal++;
                        if (!wallLogged) { wallLogged = true; Debug.Log($"[세력] 수법 — 허수 매수벽: {stock.stockName}({stock.symbol}) {wallPrice:N0}원에 {show:N0}원어치"); }
                    }
                }
            }
        }

        var market = MarketDayController.Instance;
        if (driving && market != null && market.Phase == MarketPhase.Trading && market.RemainingSeconds <= CloseSeconds && now >= closeNext)
        {
            if (closeDay != market.Day) { closeDay = market.Day; closeSpent = 0m; closeLogged = false; }
            closeNext = now + 1.5f;
            decimal step = Math.Min(operation.budget * CloseStepShare, operation.budget * CloseBudgetShare - closeSpent);
            step = Math.Min(step, Math.Max(0m, cash - ReservedCash));
            if (step > 0m && stock.orderBook != null && stock.orderBook.TryBest(out _, out decimal ask))
            {
                decimal quantity = decimal.Floor(step / ask);
                if (quantity >= 1m)
                {
                    RawBuyLimit(PriceTickPolicy.CeilToTick(ask * 1.01m), quantity, stock);
                    closeSpent += quantity * ask; CloseBuysTotal++;
                    if (!closeLogged) { closeLogged = true; Debug.Log($"[세력] 수법 — 종가 관리: {stock.stockName}({stock.symbol}) 마감 {market.RemainingSeconds:0}초 전, {ask:N0}원부터 사들임"); }
                }
            }
        }
    }
    /// <summary>작전이 끝나거나 접힐 때 벽을 거두고 수법 상태를 비운다.</summary>
    private void EndTactics(Stock stock)
    {
        if (wallPrice > 0m && stock != null) { ManipulationTools.PullWall(this, stock, wallPrice); WallsPulledTotal++; }
        wallPrice = 0m; washLogged = wallLogged = false; washRounds = wallsPlaced = 0; washPainted = 0m;
    }
}
