using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>예약 뉴스 공개 뒤 해당 종목의 장중 완료 봉만 센다.</summary>
public class DelistingManager : MonoBehaviour
{
    public static DelistingManager Instance { get; private set; }

    public float newsDelaySeconds = 600f;
    public int candlesAfterNewsUntilDelist = 10;
    public float finalLiquidationChunkDelay = 0.3f, finalLiquidationChunkRatio = 0.05f;
    public static event Action<Stock, string> OnDelistingNotifiedToWhale;
    private readonly Dictionary<Stock, int> remaining = new();
    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        CandleHistoryManager.OnStockCandleClosed += OnCandle;
        StockManager.OnStockDelisted += OnDelisted;
    }

    void OnDisable()
    {
        CandleHistoryManager.OnStockCandleClosed -= OnCandle;
        StockManager.OnStockDelisted -= OnDelisted;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnScheduledNewsPublished(Stock stock, long eventId)
    {
        if (stock == null || stock.IsDelisting || stock.IsDelisted || remaining.ContainsKey(stock))
        { return; }

        remaining.Add(stock, StockManager.Instance == null ? Math.Max(1, candlesAfterNewsUntilDelist) : Math.Max(1, StockManager.Instance.economy.delistingCompletedCandles));
    }

    public void ScheduleDelisting(Stock stock, ItCompanyNode node, string title)
    {
        // 호환 진입점: 확정 예약 공개 경로에서만 사용. 별도 지연 공개를 생성하지 않는다.
        if (node != null && node.EventKind == CompanyEventKind.Delisting)
        {
            OnScheduledNewsPublished(stock, 0);
        }
    }

    private void OnCandle(Stock stock, CandleData candle, int count)
    {
        if (!MarketDayController.CanTrade || stock.IsTradingHalted || !remaining.TryGetValue(stock, out int left))
        { return; }

        if (--left > 0)
        {
            remaining[stock] = left;
            return;
        }

        remaining.Remove(stock);
        ExchangeBot.Instance?.LiquidateStock(stock);
        StockManager.Instance?.Delist(stock);
    }

    private void OnDelisted(Stock stock)
    {
        remaining.Remove(stock);
    }

    public int RemainingCandles(Stock stock) => remaining.TryGetValue(stock, out var value) ? value : -1;
}
