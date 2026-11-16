using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>기존 차트 기록. 과거 생성은 자산/체결 이벤트와 분리하고 완료 봉만 통계에 포함.</summary>
public class CandleHistoryManager : MonoBehaviour
{
    public static CandleHistoryManager Instance { get; private set; }

    public static event Action OnCandleClosed;
    public static event Action<Stock, CandleData, int> OnStockCandleClosed;
    [Min(0.1f)]
    public float candleDuration = 5f;
    private float timer;
    private bool wasTrading;
    private readonly Dictionary<Stock, List<CandleData>> candles = new();
    private readonly Dictionary<Stock, List<CandleData>> chartHistory = new();
    private readonly Dictionary<Stock, List<CandleData>> minuteHistory = new();
    public const int ChartHistoryCapacity = 8192;
    public IReadOnlyList<CandleData> ChartHistory(Stock stock) => chartHistory.TryGetValue(stock, out var archive) ? archive : Completed(stock);
    public IReadOnlyList<CandleData> MinuteHistory(Stock stock) => minuteHistory.TryGetValue(stock, out var minutes) ? minutes : Empty;
    private readonly Dictionary<Stock, Forming> forming = new();
    private readonly Dictionary<Stock, int> actualCompleted = new();
    private static readonly CandleData[] Empty = Array.Empty<CandleData>();
    private static readonly ProfilerMarker Marker = new("DonDon.CandleClose");
    private struct Forming
    {
        public decimal Open, High, Low, Volume, Organic;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        { return; }

        Instance = this;
    }

    void Start()
    {
        if (StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                RegisterStock(stock);
                SeedHistory(stock);
            }
        }

        StockManager.OnStockDelisted += OnStockDelisted;
    }

    void OnDestroy()
    {
        StockManager.OnStockDelisted -= OnStockDelisted;
        foreach (var stock in candles.Keys)
        {
            stock.OnMatchedTrade -= OnMatched;
            stock.OnRiskChanged -= OnRiskChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnStockDelisted(Stock stock)
    {
        stock.OnMatchedTrade -= OnMatched;
        stock.OnRiskChanged -= OnRiskChanged;
        candles.Remove(stock);
        chartHistory.Remove(stock);
        minuteHistory.Remove(stock);
        forming.Remove(stock);
        actualCompleted.Remove(stock);
    }

    public void RegisterStock(Stock stock)
    {
        if (stock==null) { return; }
        stock.OnMatchedTrade -= OnMatched; stock.OnMatchedTrade += OnMatched;
        stock.OnRiskChanged -= OnRiskChanged; stock.OnRiskChanged += OnRiskChanged;
        if (candles.ContainsKey(stock))
        { return; }

        candles[stock] = new List<CandleData>(101);
        actualCompleted[stock] = 0;
        ResetForming(stock);
    }

    private void OnRiskChanged(Stock stock, StockRiskGrade before, StockRiskGrade after)
    {
        if (before == StockRiskGrade.Halted || after == StockRiskGrade.Halted)
        {
            ResetForming(stock);
        }
    }

    private void ResetForming(Stock stock)
    {
        forming[stock] = new Forming
        {
            Open = stock.currentPrice,
            High = stock.currentPrice,
            Low = stock.currentPrice
        };
    }

    /// <summary>새 게임 기존 종목 전용. 신규 상장 RegisterStock에서는 호출하지 않음.</summary>
    public void SeedHistory(Stock stock)
    {
        if (StockManager.Instance != null && StockManager.Instance.useInitialEconomy && !StockManager.Instance.SetupInProgress && !StockManager.Instance.EconomyInitialized)
        { return; }

        if (stock == null || stock.currentPrice <= 0m || stock.IsNewListing)
        { return; }

        RegisterStock(stock);
        var list = candles[stock];
        if (list.Count != 0 || actualCompleted[stock] != 0)
        { return; }

        int seed = ExchangeBot.Instance == null ? 24680 : ExchangeBot.Instance.historySeed;
        var prices = new System.Random(StableSeed(seed, stock.symbol));
        var volumes = new System.Random(StableSeed(seed ^ 0x315421, stock.symbol));
        var closes = new decimal[61];
        closes[60] = stock.currentPrice;
        for (int i = 59; i >= 0; i--)
        {
            decimal change = (decimal)(0.001 + prices.NextDouble() * 0.009) * (prices.NextDouble() < 0.5 ? -1m : 1m);
            closes[i] = closes[i + 1] / (1m + change);
        }

        for (int i = 0; i < 60; i++)
        {
            decimal open = closes[i], close = closes[i + 1];
            decimal high = Math.Max(open, close) * (1m + (decimal)(0.001 + prices.NextDouble() * 0.004));
            decimal low = Math.Min(open, close) * (1m - (decimal)(0.001 + prices.NextDouble() * 0.004));
            decimal spread = (high - low) / open;
            decimal volume = stock.issuedShares * 0.0001m * (1m + spread / 0.01m) * (decimal)(0.8 + volumes.NextDouble() * 0.4);
            volume = decimal.Floor(Math.Min(volume, stock.issuedShares * 0.005m));
            list.Add(new CandleData { open = (float)open, high = (float)high, low = (float)low, close = (float)close, exactClose = close, volume = (float)volume, nonExchangeVolume = volume, historical = true, completedTime = Time.timeAsDouble - (60 - i) * Math.Max(0.1f, candleDuration) });
        }
    }

    public static int StableSeed(int seed, string symbol)
    {
        unchecked
        {
            int hash = seed;
            foreach (char ch in symbol ?? "")
            {
                hash = hash * 31 + ch;
            }

            return hash;
        }
    }

    void OnMatched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        var clock = MarketDayController.Instance;
        if (clock != null && MarketDayController.CanTrade)
        {
            if (!minuteHistory.TryGetValue(stock, out var minutes))
            { minutes = new List<CandleData>(); minuteHistory.Add(stock, minutes); }
            ChartHistorySeries.RecordTrade(minutes, clock.Day, clock.GameMinute, price, volume,
                !(buyer is ExchangeBot) && !(seller is ExchangeBot), ChartHistoryCapacity);
        }
        if (!forming.TryGetValue(stock, out var f))
        { return; }

        f.High = Math.Max(f.High, price);
        f.Low = Math.Min(f.Low, price);
        f.Volume += volume;
        if (!(buyer is ExchangeBot) && !(seller is ExchangeBot))
        {
            f.Organic += volume;
        }

        forming[stock] = f;
    }

    void Update()
    {
        if (!MarketDayController.CanTrade)
        {
            timer = 0f;
            wasTrading = false;
            return;
        }

        if (!wasTrading)
        {
            foreach (var stock in candles.Keys)
            {
                ResetForming(stock);
            }

            wasTrading = true;
        }

        timer += Time.deltaTime;
        if (timer < Mathf.Max(0.1f, candleDuration))
        { return; }

        timer = 0f;
        using (Marker.Auto())
        {
            foreach (var stock in candles.Keys)
            {
                if (stock.IsDelisting || stock.IsTradingHalted || !stock.IsListed)
                { continue; }

                var f = forming[stock];
                CompleteCandle(stock, new CandleData { open = (float)f.Open, high = (float)f.High, low = (float)f.Low, close = (float)stock.currentPrice, exactClose = stock.currentPrice, volume = (float)f.Volume, nonExchangeVolume = f.Organic, completedTime = Time.timeAsDouble });
                ResetForming(stock);
            }

            OnCandleClosed?.Invoke();
        }
    }

    public void CompleteCandle(Stock stock, CandleData candle)
    {
        if (!MarketDayController.CanTrade || stock == null || !stock.AcceptsOrders || !candles.TryGetValue(stock, out var list))
        { return; }

        candle.historical = false;
        candle.marketDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
        candle.marketMinute = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.GameMinute;
        if (!chartHistory.TryGetValue(stock, out var archive))
        { archive = new List<CandleData>(list); chartHistory.Add(stock, archive); }
        archive.Add(candle);
        if (archive.Count > ChartHistoryCapacity)
        { archive.RemoveAt(0); }
        list.Add(candle);
        if (list.Count > 100)
        {
            list.RemoveAt(0);
        }

        actualCompleted[stock]++;
        OnStockCandleClosed?.Invoke(stock, candle, actualCompleted[stock]);
    }

    public int ActualCompleted(Stock stock) => actualCompleted.TryGetValue(stock, out var count) ? count : 0;
    public IReadOnlyList<CandleData> Completed(Stock stock) => candles.TryGetValue(stock, out var list) ? list : Empty;
    public bool TryGetBand(Stock stock, out decimal middle, out decimal deviation, out decimal organicAverage)
    {
        middle = 0m;
        deviation = 0m;
        organicAverage = 0m;
        if (!candles.TryGetValue(stock, out var list) || list.Count < 20)
        { return false; }

        for (int i = list.Count - 20; i < list.Count; i++)
        {
            middle += list[i].ClosePrice;
        }

        middle /= 20m;
        decimal variance = 0m;
        for (int i = list.Count - 20; i < list.Count; i++)
        {
            decimal gap = list[i].ClosePrice - middle;
            variance += gap * gap;
        }

        deviation = (decimal)Math.Sqrt((double)(variance / 20m));
        for (int i = list.Count - 5; i < list.Count; i++)
        {
            organicAverage += list[i].nonExchangeVolume;
        }

        organicAverage /= 5m;
        return middle > 0m;
    }

    public IReadOnlyList<CandleData> GetLastCandles(Stock stock, int count = 10)
    {
        if (stock == null || !candles.TryGetValue(stock, out var list) || list.Count == 0 || count <= 0)
        { return Empty; }

        int from = Mathf.Max(0, list.Count - count);
        var result = new List<CandleData>(list.Count - from);
        for (int i = from; i < list.Count; i++)
        {
            result.Add(list[i]);
        }

        return result;
    }

    public CandleData GetFormingCandle(Stock stock)
    {
        if (stock == null || !forming.TryGetValue(stock, out var f))
        {
            float price = stock == null ? 0f : (float)stock.currentPrice;
            return new CandleData
            {
                open = price,
                high = price,
                low = price,
                close = price,
                marketDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day,
                marketMinute = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.GameMinute
            };
        }

        return new CandleData
        {
            open = (float)f.Open,
            high = (float)f.High,
            low = (float)f.Low,
            close = (float)stock.currentPrice,
            exactClose = stock.currentPrice,
            volume = (float)f.Volume,
            nonExchangeVolume = f.Organic,
            marketDay = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day,
            marketMinute = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.GameMinute
        };
    }

    public IReadOnlyList<CandleData> GetLastCandlesIncludingForming(Stock stock, int count = 10)
    {
        var result = new List<CandleData>(Mathf.Max(1, count));
        if (stock == null)
        { return result; }

        foreach (var candle in GetLastCandles(stock, Mathf.Max(0, count - 1)))
        {
            result.Add(candle);
        }

        result.Add(GetFormingCandle(stock));
        return result;
    }

    public bool TryGetRecentCloseAndVolume(Stock stock, out decimal prevClose, out decimal currClose, out decimal prevVol, out decimal currVol)
    {
        prevClose = 0m;
        currClose = 0m;
        prevVol = 1m;
        currVol = 0m;
        if (stock == null)
        { return false; }

        currClose = stock.currentPrice;
        if (forming.TryGetValue(stock, out var f))
        {
            currVol = f.Volume;
        }

        if (candles.TryGetValue(stock, out var list) && list.Count > 0)
        {
            var candle = list[list.Count - 1];
            prevClose = candle.ClosePrice;
            prevVol = candle.volume > 0f ? (decimal)candle.volume : 1m;
        }
        else
        {
            prevClose = currClose;
        }

        return prevClose > 0m && currClose > 0m;
    }
}
