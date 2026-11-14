using System;
using UnityEngine;
using Unity.Profiling;

public partial class ExchangeBot
{
    private int gradeCursor;
    private static readonly ProfilerMarker GradeMarker = new("DonDon.RiskGrade");
    public void ResetInitialInventory(Stock stock)
    {
        var state = GetState(stock);
        if (state != null)
        {
            state.InitialInventory = GetOwnedVolume(stock);
        }
    }

    private void InitializeGrade(StockState state)
    {
        state.NextGradeEvaluation = Time.time + Mathf.Max(1f, Settings.gradeInterval) + (states.Count - 1) * 0.1f;
        if (CandleHistoryManager.Instance == null || CandleHistoryManager.Instance.Completed(state.Stock).Count < 20)
        {
            state.Stock.RiskPending = true;
            return;
        }

        state.Stock.RiskPending = false;
        ApplyGrade(state, GradeFor(state.Risk), Time.time);
    }

    public StockRiskGrade GradeFor(decimal indicator)
    {
        if (indicator >= (decimal)Settings.haltEnter)
        { return StockRiskGrade.Halted; }

        if (indicator >= (decimal)Settings.highEnter)
        { return StockRiskGrade.HighRisk; }

        if (indicator >= (decimal)Settings.dangerEnter)
        { return StockRiskGrade.Danger; }

        if (indicator >= (decimal)Settings.warningEnter)
        { return StockRiskGrade.Warning; }

        return StockRiskGrade.Safe;
    }

    private void RecordStableCandle(StockState state)
    {
        if (state.GraceCandles > 0)
        {
            state.GraceCandles--;
        }

        float threshold = state.Stock.RiskGrade == StockRiskGrade.Warning ? Settings.warningExit : state.Stock.RiskGrade == StockRiskGrade.Danger ? Settings.dangerExit : state.Stock.RiskGrade == StockRiskGrade.HighRisk ? Settings.highExit : -1f;
        state.StableGradeCandles = threshold > 0f && state.Risk < (decimal)threshold ? Math.Min(Math.Max(1, Settings.stableCandles), state.StableGradeCandles + 1) : 0;
    }

    public void EvaluateGrade(Stock stock, float now)
    {
        var state = GetState(stock);
        if (state == null || stock.IsDelisting || stock.IsDelisted || stock.IsTradingHalted || now < state.NextGradeEvaluation)
        { return; }

        state.NextGradeEvaluation = now + Mathf.Max(1f, Settings.gradeInterval);
        if (CandleHistoryManager.Instance == null || CandleHistoryManager.Instance.Completed(stock).Count < 20)
        {
            stock.RiskPending = true;
            return;
        }

        stock.RiskPending = false;
        var target = GradeFor(state.Risk);
        if (target == StockRiskGrade.Halted && state.GraceCandles > 0)
        {
            target = StockRiskGrade.HighRisk;
        }

        if (target > stock.RiskGrade)
        {
            ApplyGrade(state, target, now);
        }
        else if (state.StableGradeCandles >= Math.Max(1, Settings.stableCandles) && stock.RiskGrade > StockRiskGrade.Safe)
        {
            ApplyGrade(state, stock.RiskGrade - 1, now);
        }
    }

    private void ApplyGrade(StockState state, StockRiskGrade grade, float now)
    {
        var stock = state.Stock;
        if (stock.RiskGrade == grade)
        {
            state.Risky = grade != StockRiskGrade.Safe;
            return;
        }

        var before = stock.RiskGrade;
        stock.PreviousRiskGrade = before;
        stock.RiskGrade = grade;
        stock.RiskVersion++;
        stock.RiskChangedAt = now;
        state.StableGradeCandles = 0;
        state.Risky = grade != StockRiskGrade.Safe;
        if (grade != StockRiskGrade.Safe)
        {
            CancelDefense(state);
            state.RecoveryStage = 0;
            state.RecoveryNeedsTrade = true;
            if (grade == StockRiskGrade.Halted)
            {
                stock.OrderEpoch++;
                stock.HaltUntil = now + Mathf.Max(0.1f, Settings.haltSeconds);
                CancelInitial(state);
            }
        }
        else
        {
            state.RecoveryStage = 0;
            state.RecoveryNeedsTrade = !state.RecentActualTrade;
        }

        stock.RaiseRiskChanged(before);
        NewsManager.Instance?.AddStatusNews(stock, $"{stock.stockName}: {stock.RiskLabel}");
        MarketDayController.Instance?.RiskChanged(stock);
    }

    private void TickRiskGrades()
    {
        if (!AssetsInitialized || states.Count == 0)
        { return; }

        using (GradeMarker.Auto())
        {
            int count = Math.Min(states.Count, Math.Max(1, Settings.stocksPerFrame));
            for (int i = 0; i < count; i++)
            {
                if (gradeCursor >= states.Count)
                {
                    gradeCursor = 0;
                }

                var state = states[gradeCursor++];
                var stock = state.Stock;
                if (stock.IsDelisting || stock.IsDelisted)
                { continue; }

                if (stock.IsTradingHalted)
                {
                    stock.orderBook.CancelAllBudgeted(stock, Math.Max(1, Settings.cancelOrdersPerFrame));
                    if (Time.time >= stock.HaltUntil && !stock.orderBook.HasOrders)
                    {
                        ApplyGrade(state, StockRiskGrade.HighRisk, Time.time);
                        state.GraceCandles = Math.Max(1, Settings.haltGraceCandles);
                        state.NextGradeEvaluation = Time.time + Mathf.Max(1f, Settings.gradeInterval);
                        state.NeedsOpening = state.NewListing && state.CompletedCount < 60;
                    }
                }
                else
                {
                    EvaluateGrade(stock, Time.time);
                }
            }
        }
    }
}
