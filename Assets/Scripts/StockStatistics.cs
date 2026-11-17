using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 종목별 통계 관리: 52주 최고/최저, 전일 종가, 당일 시가/고가/저가, 체결강도, 최근 거래 기록
/// </summary>
public class StockStatistics : MonoBehaviour
{
    public static StockStatistics Instance { get; private set; }

    [Header("설정")]
    [Tooltip("52주 = 게임 내 몇 초? (실제 52주 = 31,449,600초, 게임 시간 압축 적용)")]
    public float week52InSeconds = 3600f;
    [HideInInspector] // 구 씬 직렬화 호환용. 일간 기준은 시장 개장 이벤트로만 변경한다.
    public float dayInSeconds = 60f;
    [Tooltip("최근 거래 기록 보관 개수 (UI 표시용)")]
    public int maxTradeRecords = 20;
    [Header("거래량 급증 감지")]
    [Tooltip("거래량 측정 윈도우(초)")]
    public float volumeWindowSeconds = 10f;
    [Tooltip("이전 윈도우 대비 이 배수 이상이면 급증")]
    public float volumeSurgeMultiplier = 3f;
    [Tooltip("급증 이벤트 발생 간격(초) - 너무 자주 발생 방지")]
    public float surgeCooldown = 30f;
    /// <summary>거래량 급증 시 발생 (종목, 급증 배수)</summary>
    public static event System.Action<Stock, float> OnVolumeSurge;
    private Dictionary<Stock, StockStats> _stats = new Dictionary<Stock, StockStats>();
    private Dictionary<Stock, float> _lastSurgeTime = new Dictionary<Stock, float>();
    private Dictionary<Stock, decimal> _prevWindowVolume = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _currentWindowVolume = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, float> _windowStartTime = new Dictionary<Stock, float>();
    private Dictionary<Stock, decimal> _sessionTurnover = new Dictionary<Stock, decimal>();
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        StockManager.OnStockDelisted += OnStockDelisted;
    }

    void OnDisable()
    {
        StockManager.OnStockDelisted -= OnStockDelisted;
    }

    void OnStockDelisted(Stock stock)
    {
        if (stock == null)
        { return; }

        stock.OnTradeExecutedWithStock -= OnTradeExecuted;
        stock.OnMatchedTrade -= OnMatchedPrice;
        _stats.Remove(stock);
        _lastSurgeTime.Remove(stock);
        _prevWindowVolume.Remove(stock);
        _currentWindowVolume.Remove(stock);
        _windowStartTime.Remove(stock);
        _sessionTurnover.Remove(stock);
        _dayWindows?.Remove(stock);
    }

    void Start()
    {
        if (StockManager.Instance == null)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            RegisterStock(stock);
        }
    }

    void OnDestroy()
    {
        if (StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock != null)
                {
                    stock.OnTradeExecutedWithStock -= OnTradeExecuted;
                    stock.OnMatchedTrade -= OnMatchedPrice;
                }
            }
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetInitialStock(Stock stock)
    {
        OnStockDelisted(stock);
        RegisterStock(stock);
    }

    public void RegisterStock(Stock stock)
    {
        if (stock == null)
        { return; }

        if (!_stats.ContainsKey(stock))
        {
            var stats = new StockStats
            {
                stock = stock,
                week52High = stock.currentPrice,
                week52Low = stock.currentPrice,
                prevDayClose = stock.currentPrice,
                dayOpen = stock.currentPrice,
                dayHigh = stock.currentPrice,
                dayLow = stock.currentPrice,
                dayStartTime = SessionClock.Time,
                week52StartTime = SessionClock.Time,
                recentTrades = new List<TradeRecord>(),
                strengthTrades = new List<TradeRecord>()
            };
            _stats[stock] = stats;
            _lastSurgeTime[stock] = 0f;
            _prevWindowVolume[stock] = 0m;
            _currentWindowVolume[stock] = 0m;
            _windowStartTime[stock] = SessionClock.Time;
        }

        if (!_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] = 0m;
        }

        stock.OnTradeExecutedWithStock -= OnTradeExecuted;
        stock.OnTradeExecutedWithStock += OnTradeExecuted;
        stock.OnMatchedTrade -= OnMatchedPrice;
        stock.OnMatchedTrade += OnMatchedPrice;
    }

    /// <summary>새 장이 열릴 때만 전일 종가를 확정한다. 마감 후에는 그날 등락률을 유지한다.</summary>
    public void BeginTradingDay(IReadOnlyDictionary<Stock, decimal> previousCloses)
    {
        _updateKeys.Clear(); _updateKeys.AddRange(_stats.Keys);
        foreach (var stock in _updateKeys)
        {
            var stats = _stats[stock];
            if (previousCloses != null && previousCloses.TryGetValue(stock, out decimal close) && close > 0m)
            { stats.prevDayClose = close; }
            stats.dayOpen = stats.dayHigh = stats.dayLow = stock.currentPrice;
            stats.dayStartTime = SessionClock.Time;
            _stats[stock] = stats;
        }
    }
    public bool TryGetDailyChange(Stock stock, out decimal percent)
    {
        percent = 0m;
        if (stock == null || !_stats.TryGetValue(stock, out var stats) || stats.prevDayClose <= 0m)
        { return false; }
        percent = (stock.currentPrice / stats.prevDayClose - 1m) * 100m;
        return true;
    }

    /// <summary>미니 캔들용 당일 OHLC. 시가는 개장 때 고정하고 종가는 현재 체결가를 사용한다.</summary>
    public bool TryGetDailyCandle(Stock stock, out CandleData candle)
    {
        candle = default;
        if (stock == null || !_stats.TryGetValue(stock, out var stats) || stats.dayOpen <= 0m || stock.currentPrice <= 0m)
        { return false; }
        candle = new CandleData
        {
            open = (float)stats.dayOpen,
            high = (float)Math.Max(stats.dayHigh, stock.currentPrice),
            low = (float)Math.Min(stats.dayLow, stock.currentPrice),
            close = (float)stock.currentPrice,
            exactClose = stock.currentPrice
        };
        return true;
    }

    private void OnMatchedPrice(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (stock == null || volume <= 0m || price <= 0m || ReferenceEquals(buyer,seller) || !_stats.TryGetValue(stock,out var stats))
        { return; }
        // One submitted order can fill across several price levels before the aggregate trade callback.
        stats.dayHigh = Math.Max(stats.dayHigh,price); stats.dayLow = Math.Min(stats.dayLow,price);
        stats.week52High = Math.Max(stats.week52High,price); stats.week52Low = Math.Min(stats.week52Low,price);
        _stats[stock] = stats;
    }

    void OnTradeExecuted(Stock stock, decimal price, decimal volume)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return; }

        float now = SessionClock.Time;
        if (price > stats.week52High)
        {
            stats.week52High = price;
        }

        if (price < stats.week52Low)
        {
            stats.week52Low = price;
        }

        if (price > stats.dayHigh)
        {
            stats.dayHigh = price;
        }

        if (price < stats.dayLow)
        {
            stats.dayLow = price;
        }

        bool isBuyInitiated = IsBuyInitiated(stock, price);
        var record = new TradeRecord
        {
            price = price,
            volume = volume,
            time = now,
            isBuyInitiated = isBuyInitiated
        };
        stats.recentTrades.Add(record);
        if (stats.recentTrades.Count > maxTradeRecords)
        {
            stats.recentTrades.RemoveAt(0);
        }

        stats.strengthTrades.Add(record);
        _stats[stock] = stats;
        if (_sessionTurnover.ContainsKey(stock))
        {
            _sessionTurnover[stock] += price * volume;
        }
        else
        {
            _sessionTurnover[stock] = price * volume;
        }

        RecordDayWindow(stock, price, volume);
        TrackVolumeAndDetectSurge(stock, volume, now);
    }

    /// <summary>게임 시간으로 최근 24시간(1440분) 동안의 체결. 분 단위로 묶어 두고 오래된 분은 버린다.</summary>
    public sealed class DayWindow
    {
        public readonly Queue<(int minute, decimal volume, decimal turnover)> Minutes = new();
        public int LastMinute = int.MinValue;
        public decimal LastVolume, LastTurnover, Volume, Turnover;
    }
    public const int DayWindowMinutes = 1440;
    private Dictionary<Stock, DayWindow> _dayWindows = new Dictionary<Stock, DayWindow>();
    private static int NowMinute => MarketDayController.Instance == null ? 0 : (int)MarketDayController.Instance.AbsoluteGameMinute;
    private DayWindow WindowFor(Stock stock)
    {
        _dayWindows ??= new Dictionary<Stock, DayWindow>();
        if (!_dayWindows.TryGetValue(stock, out var window) || window == null) { window = new DayWindow(); _dayWindows[stock] = window; }
        return window;
    }
    private static void Roll(DayWindow window, int minute)
    {
        // 지금 쌓는 분이 바뀌면 지난 분을 줄에 넣는다.
        if (window.LastMinute != minute)
        {
            if (window.LastMinute != int.MinValue && (window.LastVolume > 0m || window.LastTurnover > 0m))
            { window.Minutes.Enqueue((window.LastMinute, window.LastVolume, window.LastTurnover)); }
            window.LastMinute = minute; window.LastVolume = 0m; window.LastTurnover = 0m;
        }
        while (window.Minutes.Count > 0 && window.Minutes.Peek().minute <= minute - DayWindowMinutes)
        {
            var old = window.Minutes.Dequeue();
            window.Volume -= old.volume; window.Turnover -= old.turnover;
        }
    }
    private void RecordDayWindow(Stock stock, decimal price, decimal volume)
    {
        var window = WindowFor(stock); Roll(window, NowMinute);
        window.LastVolume += volume; window.LastTurnover += price * volume;
        window.Volume += volume; window.Turnover += price * volume;
    }
    /// <summary>게임 시간 기준 최근 24시간의 거래량과 거래대금(실제 체결 가격×수량의 합).</summary>
    public void GetDayWindow(Stock stock, out decimal volume, out decimal turnover)
    {
        volume = turnover = 0m;
        if (stock == null) { return; }
        var window = WindowFor(stock); Roll(window, NowMinute);
        volume = System.Math.Max(0m, window.Volume); turnover = System.Math.Max(0m, window.Turnover);
    }

    /// <summary>세션 누적 거래대금 (가격×체결량 합계). RandomBot 등 가중치 계산용.</summary>
    public decimal GetSessionTurnover(Stock stock)
    { return stock != null && _sessionTurnover.TryGetValue(stock, out var v) ? v : 0m; }

    void TrackVolumeAndDetectSurge(Stock stock, decimal volume, float now)
    {
        if (!_windowStartTime.TryGetValue(stock, out float windowStart))
        {
            _windowStartTime[stock] = now;
            _currentWindowVolume[stock] = volume;
            return;
        }

        if (now - windowStart >= volumeWindowSeconds)
        {
            decimal prevVol = _prevWindowVolume.TryGetValue(stock, out var pv) ? pv : 0m;
            decimal currVol = _currentWindowVolume.TryGetValue(stock, out var cv) ? cv : 0m;
            if (prevVol > 0m && currVol > prevVol * (decimal)volumeSurgeMultiplier)
            {
                float lastSurge = _lastSurgeTime.TryGetValue(stock, out var ls) ? ls : 0f;
                if (now - lastSurge >= surgeCooldown)
                {
                    float surgeRatio = (float)(currVol / prevVol);
                    _lastSurgeTime[stock] = now;
                    OnVolumeSurge?.Invoke(stock, surgeRatio);
                }
            }

            _prevWindowVolume[stock] = currVol;
            _currentWindowVolume[stock] = 0m;
            _windowStartTime[stock] = now;
        }

        _currentWindowVolume[stock] = (_currentWindowVolume.TryGetValue(stock, out var c) ? c : 0m) + volume;
    }

    bool IsBuyInitiated(Stock stock, decimal price)
    {
        if (stock?.orderBook == null)
        { return true; }

        var asks = stock.orderBook.GetTopAsks(1);
        var bids = stock.orderBook.GetTopBids();
        if (asks.Count > 0 && bids.Count > 0)
        {
            decimal bestAsk = asks[0].price;
            decimal bestBid = bids[0].price;
            decimal mid = (bestAsk + bestBid) / 2m;
            return price >= mid;
        }

        return true;
    }

    private List<Stock> _updateKeys = new List<Stock>();
    private float _lastCleanupTime;
    private const float CLEANUP_INTERVAL = 5f;
    void Update()
    {
        float now = SessionClock.Time;
        _updateKeys.Clear();
        _updateKeys.AddRange(_stats.Keys);
        bool doCleanup = now - _lastCleanupTime >= CLEANUP_INTERVAL;
        if (doCleanup)
        {
            _lastCleanupTime = now;
        }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float cutoffTime = now - candleDuration;
        foreach (var stock in _updateKeys)
        {
            if (!_stats.TryGetValue(stock, out var stats))
            { continue; }

            bool modified = false;
            if (now - stats.week52StartTime >= week52InSeconds)
            {
                stats.week52High = stats.stock.currentPrice;
                stats.week52Low = stats.stock.currentPrice;
                stats.week52StartTime = now;
                modified = true;
            }

            if (doCleanup && stats.strengthTrades.Count > 0)
            {
                int removed = stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
                if (removed > 0)
                {
                    modified = true;
                }
            }

            if (modified)
            {
                _stats[stock] = stats;
            }
        }
    }

    public StockStats GetStats(Stock stock)
    {
        if (stock != null && _stats.TryGetValue(stock, out var stats))
        { return stats; }

        return default;
    }

    /// <summary>
    /// 체결강도 계산: (캔들 기간 내 매수체결량 / 매도체결량) × 100
    /// 100 이상이면 매수 우세, 100 미만이면 매도 우세
    /// CandleHistoryManager의 candleDuration 기준으로 오래된 기록 제거 후 계산
    /// </summary>
    public float GetTradeStrength(Stock stock)
    {
        if (stock == null || !_stats.TryGetValue(stock, out var stats))
        { return 100f; }

        float candleDuration = CandleHistoryManager.Instance != null ? CandleHistoryManager.Instance.candleDuration : 5f;
        float now = SessionClock.Time;
        float cutoffTime = now - candleDuration;
        stats.strengthTrades.RemoveAll(t => t.time < cutoffTime);
        _stats[stock] = stats;
        decimal buyVolume = 0m;
        decimal sellVolume = 0m;
        foreach (var trade in stats.strengthTrades)
        {
            if (trade.isBuyInitiated)
            {
                buyVolume += trade.volume;
            }
            else
            {
                sellVolume += trade.volume;
            }
        }

        if (sellVolume <= 0m)
        { return buyVolume > 0m ? 999f : 100f; }

        return (float)(buyVolume / sellVolume * 100m);
    }

    public IReadOnlyList<TradeRecord> GetRecentTrades(Stock stock)
    {
        if (stock != null && _stats.TryGetValue(stock, out var stats))
        { return stats.recentTrades; }

        return Array.Empty<TradeRecord>();
    }
}

public struct StockStats
{
    public Stock stock;
    public decimal week52High;
    public decimal week52Low;
    public decimal prevDayClose;
    public decimal dayOpen;
    public decimal dayHigh;
    public decimal dayLow;
    public float dayStartTime;
    public float week52StartTime;
    public List<TradeRecord> recentTrades;
    public List<TradeRecord> strengthTrades;
    public StockStats(Stock s)
    {
        stock = s;
        week52High = s?.currentPrice ?? 0m;
        week52Low = s?.currentPrice ?? 0m;
        prevDayClose = s?.currentPrice ?? 0m;
        dayOpen = s?.currentPrice ?? 0m;
        dayHigh = s?.currentPrice ?? 0m;
        dayLow = s?.currentPrice ?? 0m;
        dayStartTime = SessionClock.Time;
        week52StartTime = SessionClock.Time;
        recentTrades = new List<TradeRecord>();
        strengthTrades = new List<TradeRecord>();
    }
}

public struct TradeRecord
{
    public decimal price;
    public decimal volume;
    public float time;
    public bool isBuyInitiated;
}
