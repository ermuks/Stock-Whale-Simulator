using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class TradeBot
{
    [Range(0f, 1f)]
    public float riskPreference = 0.5f;
    private bool riskPreferenceInitialized;
    private struct RiskResponse
    {
        public int Version;
        public StockRiskGrade Applied;
        public float ApplyAt;
    }

    private readonly Dictionary<Stock, RiskResponse> riskResponses = new();
    private readonly Dictionary<Stock, int> intentVersions = new();
    public void InitializeRiskPreference(int seed)
    {
        if (riskPreferenceInitialized)
        { return; }

        riskPreferenceInitialized = true;
        var rng = new System.Random(CandleHistoryManager.StableSeed(seed, "RISK:" + InitialAccountId));
        bool cautious = this is SmartBot || this is HolderBot || this is PanicBot || this is InstitutionBot || this is InternalValueBot;
        bool speculative = this is FomoBot || this is StupidBot || this is ScalperBot || this is WhaleFollowerBot || this is FakeValueBot;
        riskPreference = (float)(cautious ? 0.05 + rng.NextDouble() * 0.27 : speculative ? 0.68 + rng.NextDouble() * 0.32 : 0.34 + rng.NextDouble() * 0.32);
    }

    public StockRiskGrade RespondedGrade(Stock stock)
    {
        if (stock == null)
        { return StockRiskGrade.Halted; }

        riskResponses.TryGetValue(stock, out var response);
        if (response.Version != stock.RiskVersion)
        {
            if (response.Version == 0)
            {
                response.Applied = stock.PreviousRiskGrade;
            }

            int seed = StockManager.Instance == null ? 12345 : StockManager.Instance.economy.seed;
            uint hash = (uint)CandleHistoryManager.StableSeed(seed, InitialAccountId + ":" + stock.symbol + ":" + stock.RiskVersion);
            response.Version = stock.RiskVersion;
            response.ApplyAt = stock.RiskChangedAt + hash / (float)uint.MaxValue * (StockManager.Instance == null ? 30f : Mathf.Max(0f, StockManager.Instance.economy.reactionDelayMax));
        }

        if (Time.time >= response.ApplyAt)
        {
            response.Applied = stock.RiskGrade;
        }

        riskResponses[stock] = response;
        return response.Applied;
    }

    public float RiskSelectionMultiplier(Stock stock)
    {
        if (stock == null || !stock.AcceptsOrders)
        { return 0f; }

        var settings = StockManager.Instance.economy;
        var grade = RespondedGrade(stock);
        if (grade == StockRiskGrade.Halted)
        {
            grade = StockRiskGrade.HighRisk;
        }

        var weights = riskPreference < 0.33f ? settings.cautiousWeights : riskPreference < 0.67f ? settings.neutralWeights : settings.speculativeWeights;
        int index = (int)grade;
        float value = weights != null && index < weights.Length ? weights[index] : 1f;
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0f, 10f);
    }

    protected virtual bool CanBuyStock(Stock stock) => stock != null && stock.AcceptsOrders;
    protected virtual bool CanSellStock(Stock stock) => stock != null && stock.AcceptsOrders;
    private int IntentVersion(Stock stock) => stock == null ? -1 : stock.OrderEpoch + (intentVersions.TryGetValue(stock, out int version) ? version : 0);
    public void InvalidateStockIntents(Stock stock)
    {
        if (stock == null)
        { return; }

        intentVersions[stock] = intentVersions.TryGetValue(stock, out int version) ? version + 1 : 1;
        for (int i = _pendingOrders.Count - 1; i >= 0; i--)
        {
            if (_pendingOrders[i].stock == stock)
            {
                _pendingOrders.RemoveAt(i);
            }
        }
    }
}
