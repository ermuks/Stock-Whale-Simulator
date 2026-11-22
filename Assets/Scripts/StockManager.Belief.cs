using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유통 시총 비중과 "시장이 믿는 가격" 집계.
/// fakeValue는 누구도 직접 정하지 않는다. 활동 중인 개미들이 각자 믿는 가격의 평균을 읽기 전용으로 보여 준다.
/// </summary>
public partial class StockManager
{
    private readonly Dictionary<Stock, decimal> floatValues = new();
    private decimal floatTotal;
    private float nextFloatRefresh, nextBeliefRefresh;
    private int beliefCursor;

    /// <summary>전체 유통 시총에서 이 종목이 차지하는 비중(0~1). 소유주·거래소 보유분은 유통량에서 뺀다.</summary>
    public decimal FloatShare(Stock stock)
    {
        if (stock == null || floatTotal <= 0m || !floatValues.TryGetValue(stock, out decimal value))
        { return 0m; }

        return value / floatTotal;
    }

    public decimal FloatValue(Stock stock)
    { return stock != null && floatValues.TryGetValue(stock, out decimal value) ? value : 0m; }

    private void RefreshFloat()
    {
        floatValues.Clear();
        floatTotal = 0m;
        foreach (var stock in stocks)
        {
            if (stock == null || stock.currentPrice <= 0m)
            { continue; }

            decimal locked = stock.Owner != null && !PlayerAccounts.IsPlayer(stock.Owner) ? stock.Owner.GetOwnedVolume(stock) : 0m;
            if (ExchangeBot.Instance != null)
            {
                locked += ExchangeBot.Instance.GetOwnedVolume(stock);
            }

            if (ExchangeWarehouse.Instance != null)
            {
                locked += ExchangeWarehouse.Instance.GetHolding(stock);
            }

            decimal value = Math.Max(0m, stock.issuedShares - locked) * stock.currentPrice;
            floatValues[stock] = value;
            floatTotal += value;
        }
    }

    /// <summary>한 번에 한 종목만 집계해 프레임 부담을 나눈다.</summary>
    private void RefreshCrowdBelief()
    {
        if (stocks.Count == 0 || MarketDayController.Instance == null)
        { return; }

        if (beliefCursor >= stocks.Count)
        {
            beliefCursor = 0;
        }

        var stock = stocks[beliefCursor++];
        if (stock == null || stock.currentPrice <= 0m)
        { return; }

        double sum = 0;
        int count = 0;
        var bots = MarketDayController.Instance.Bots;
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            if (bot == null || bot.Social == null || bot.KnowsRealValue || !bot.isActiveAndEnabled)
            { continue; }

            if (bot.Social.Psychology.Companies.TryBelief(stock.symbol, out double belief))
            {
                sum += belief;
                count++;
            }
        }

        decimal crowd = count >= 5 ? (decimal)(sum / count) : stock.currentPrice;
        using (TellMarker.Auto()) { stock.SetFakeValue(Math.Max(1m, PriceTickPolicy.RoundToTick(crowd))); }
    }

    private static readonly Unity.Profiling.ProfilerMarker FloatMarker = new("DonDon.Crowd.Float"), BeliefMarker = new("DonDon.Crowd.Belief"), TellMarker = new("DonDon.Crowd.Tell");
    private void UpdateCrowdValues()
    {
        if (Time.unscaledTime >= nextFloatRefresh)
        {
            nextFloatRefresh = Time.unscaledTime + 2f;
            using (FloatMarker.Auto()) { RefreshFloat(); }
        }

        if (Time.unscaledTime >= nextBeliefRefresh && MarketDayController.CanTrade)
        {
            nextBeliefRefresh = Time.unscaledTime + 0.5f;
            using (BeliefMarker.Auto()) { RefreshCrowdBelief(); }
        }
    }
}
