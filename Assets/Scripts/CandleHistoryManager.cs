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
        // 지난 며칠의 분봉을 먼저 만든다. 1분·5분·15분·1시간·1일 봉은 이 분봉을 묶어서 그린다.
        var series = BuildSeedMinutes(stock.currentPrice, stock.issuedShares, StableSeed(seed, stock.symbol));
        if (!minuteHistory.TryGetValue(stock, out var minutes))
        { minutes = new List<CandleData>(series.Count + 512); minuteHistory.Add(stock, minutes); }
        if (minutes.Count == 0)
        {
            // 그보다 앞선 날들은 하루에 한 봉씩만 둔다(1일 봉 보기에서만 보인다).
            minutes.AddRange(BuildSeedDays((decimal)series[0].open, stock.issuedShares, StableSeed(seed ^ 0x5D17, stock.symbol)));
            minutes.AddRange(series);
        }

        // 기본 보기의 지난 봉 60개는 같은 기록의 마지막 하루(480분)를 여덟 개씩 묶은 것이다(어느 보기로 봐도 같은 흐름이 이어진다).
        int from = series.Count - SeedBaseCandles * SeedBaseSpan;
        for (int i = 0; i < SeedBaseCandles; i++)
        {
            var merged = series[from + i * SeedBaseSpan];
            for (int k = 1; k < SeedBaseSpan; k++)
            {
                var next = series[from + i * SeedBaseSpan + k];
                merged.high = Math.Max(merged.high, next.high); merged.low = Math.Min(merged.low, next.low);
                merged.close = next.close; merged.exactClose = next.exactClose;
                merged.volume += next.volume; merged.nonExchangeVolume += next.nonExchangeVolume;
            }
            // 기본 보기의 봉은 시각 없이 차례로만 놓인다(예전과 같다).
            merged.marketDay = 0; merged.marketMinute = 0;
            merged.completedTime = Time.timeAsDouble - (SeedBaseCandles - i) * Math.Max(0.1f, candleDuration);
            list.Add(merged);
        }
        // 옆 패널의 52주 최고·최저도 이 지난 시세를 따른다.
        StockStatistics.Instance?.IncludeHistory(stock);
    }

    /// <summary>미리 그려 두는 지난 시세: 거래일 수, 장이 열리는 분(09:00), 하루의 분 수(17:00까지).</summary>
    public const int SeedDays = 5, SeedOpenMinute = 540, SeedMinutesPerDay = 480;
    /// <summary>분봉보다 앞선 날들을 일봉으로만 만들어 두는 날 수. 1일 봉 보기가 휑하지 않게 한다.</summary>
    public const int SeedDailyDays = 55;
    /// <summary>일봉으로만 만든 지난 날의 표시(completedTime). 분·시간 단위 보기에서는 이 봉을 빼고 그린다.</summary>
    public const double DailyOnly = -1d;
    /// <summary>
    /// 분봉 기록보다 앞선 SeedDailyDays일을 하루 한 봉씩 만든다. 마지막 날의 종가에서 밤사이 갭을 거쳐 분봉 첫날의 시가(nextOpen)로 이어진다.
    /// 변동이 몰려 다니고(조용한 주와 요동치는 주), 가끔 며칠씩 한쪽으로 간다. 값은 호가 단위에 맞춘다.
    /// </summary>
    public static List<CandleData> BuildSeedDays(decimal nextOpen, decimal issuedShares, int seed)
    {
        var random = new System.Random(seed);
        double Gauss()
        {
            double u = 1d - random.NextDouble(), v = random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(u)) * Math.Cos(2d * Math.PI * v);
        }
        int total = SeedDailyDays;
        var opens = new double[total]; var closes = new double[total]; var highs = new double[total]; var lows = new double[total]; var activity = new double[total];
        double level = 0d, swing = 1d, trend = 0d; int run = 0;
        for (int i = 0; i < total; i++)
        {
            swing = Math.Min(2.6, Math.Max(0.45, Math.Exp(Math.Log(swing) * 0.85 + Gauss() * 0.28)));
            if (run <= 0 && random.NextDouble() < 0.10) { run = 2 + random.Next(5); trend = (random.NextDouble() < 0.5 ? -1d : 1d) * (0.008 + random.NextDouble() * 0.014); }
            double sigma = 0.021 * swing;
            if (i > 0) { level += Gauss() * 0.006; }
            opens[i] = level;
            double move = Gauss() * sigma + (run > 0 ? trend : 0d) - level * 0.02;
            run--; level += move; closes[i] = level;
            highs[i] = Math.Max(opens[i], closes[i]) + Math.Abs(Gauss()) * sigma * 0.55;
            lows[i] = Math.Min(opens[i], closes[i]) - Math.Abs(Gauss()) * sigma * 0.55;
            activity[i] = swing * (0.7 + random.NextDouble() * 0.6) * (1d + Math.Abs(move) / 0.021);
        }
        // 마지막 날 종가 뒤의 밤사이 갭까지 더한 자리가 분봉 첫날의 시가다.
        double end = level + Gauss() * 0.006;
        var result = new List<CandleData>(total);
        decimal Price(double value) => Math.Max(1m, PriceTickPolicy.RoundToTick(nextOpen * (decimal)Math.Exp(value - end)));
        for (int i = 0; i < total; i++)
        {
            decimal open = Price(opens[i]), close = Price(closes[i]);
            decimal high = Math.Max(Price(highs[i]), Math.Max(open, close)), low = Math.Min(Price(lows[i]), Math.Min(open, close));
            decimal volume = decimal.Floor(Math.Min(issuedShares * 0.011m * (decimal)activity[i], issuedShares * 0.2m));
            result.Add(new CandleData
            {
                open = (float)open, high = (float)high, low = (float)low, close = (float)close, exactClose = close, volume = (float)volume, nonExchangeVolume = volume,
                historical = true, completedTime = DailyOnly, marketDay = i - total - (SeedDays - 1), marketMinute = SeedOpenMinute,
            });
        }
        return result;
    }
    /// <summary>기본 보기에 놓는 지난 봉의 수와, 한 봉에 묶는 분봉의 수.</summary>
    public const int SeedBaseCandles = 60, SeedBaseSpan = 8;
    /// <summary>
    /// 새 게임의 지난 시세를 분봉으로 만든다. 마지막 봉의 종가가 지금 값(lastPrice)과 같다. 날짜는 게임 첫날의 앞날들(0일, -1일, …)이다.
    /// 예전에는 봉마다 ±0.1~1%를 동전 던지기로 정해 오르내림이 한결같았다. 이제는 실제 시세처럼 굴곡을 준다:
    ///  - 변동이 몰려 다닌다(조용한 구간과 요동치는 구간이 번갈아 온다). 장 시작과 마감 무렵이 더 거칠다.
    ///  - 가끔 한쪽으로 달리는 구간(수십 분)과 갑자기 튀는 봉이 있다. 날마다 분위기와 방향이 다르고, 밤사이에 갭이 뜬다.
    /// 값은 모두 호가 단위에 맞춘다(시가·고가·저가·종가). 거래량은 변동이 큰 봉일수록 많다.
    /// 화면에 보여 주기만 하는 기록이다. 계좌·호가·체결은 건드리지 않는다.
    /// </summary>
    public static List<CandleData> BuildSeedMinutes(decimal lastPrice, decimal issuedShares, int seed)
    {
        // 분봉 하나의 평소 변동. 첫 측정(0.0016)에서는 하루 폭이 15~40%나 돼서 절반 아래로 낮췄다: 하루 폭 6~15%쯤이 되게.
        const double Base = 0.0007;
        var random = new System.Random(seed);
        double Gauss()
        {
            double u = 1d - random.NextDouble(), v = random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(u)) * Math.Cos(2d * Math.PI * v);
        }
        int total = SeedDays * SeedMinutesPerDay;
        var opens = new double[total]; var closes = new double[total]; var highs = new double[total]; var lows = new double[total]; var activity = new double[total];
        double level = 0d, swing = 1d, runDrift = 0d; int run = 0;
        for (int day = 0; day < SeedDays; day++)
        {
            // 밤사이의 갭, 그날의 방향과 분위기.
            if (day > 0) { level += Gauss() * 0.007; }
            double dayDrift = Gauss() * 0.012 / SeedMinutesPerDay, mood = 0.65 + random.NextDouble() * 0.9;
            for (int minute = 0; minute < SeedMinutesPerDay; minute++)
            {
                int i = day * SeedMinutesPerDay + minute;
                double edge = 2d * minute / (SeedMinutesPerDay - 1) - 1d, shape = 1d + 1.2 * edge * edge;
                // 변동은 몰려 다닌다: 평소 수준(1)으로 천천히 돌아오면서 제멋대로 부풀고 가라앉는다.
                swing = Math.Min(3.5, Math.Max(0.35, Math.Exp(Math.Log(swing) * 0.96 + Gauss() * 0.14)));
                if (run <= 0 && random.NextDouble() < 0.012)
                { run = 8 + random.Next(30); runDrift = (random.NextDouble() < 0.5 ? -1d : 1d) * Base * (0.6 + random.NextDouble() * 1.2); swing = Math.Min(3.5, swing * 1.6); }
                double jump = 0d;
                if (random.NextDouble() < 0.004) { jump = Gauss() * Base * 6d; swing = Math.Min(3.5, swing * 2d); }
                double sigma = Base * shape * swing * mood;
                double move = Gauss() * sigma + dayDrift + (run > 0 ? runDrift : 0d) + jump - level * 0.0006;
                run--;
                opens[i] = level; level += move; closes[i] = level;
                highs[i] = Math.Max(opens[i], closes[i]) + Math.Abs(Gauss()) * sigma * 0.7;
                lows[i] = Math.Min(opens[i], closes[i]) - Math.Abs(Gauss()) * sigma * 0.7;
                activity[i] = shape * swing * mood * (0.6 + random.NextDouble() * 0.8) * (1d + Math.Abs(move) / Base * 0.9);
            }
        }

        // 마지막 종가가 지금 값이 되게 통째로 옮기고, 호가 단위에 맞춘다.
        var result = new List<CandleData>(total);
        double end = level; decimal previous = 0m;
        decimal Price(double value) => Math.Max(1m, PriceTickPolicy.RoundToTick(lastPrice * (decimal)Math.Exp(value - end)));
        for (int i = 0; i < total; i++)
        {
            int minute = i % SeedMinutesPerDay;
            // 하루 안에서는 앞 봉의 종가가 다음 봉의 시가다. 날이 바뀌면 갭이 뜰 수 있다.
            decimal open = minute == 0 || previous <= 0m ? Price(opens[i]) : previous;
            decimal close = i == total - 1 ? lastPrice : Price(closes[i]);
            decimal high = Math.Max(Price(highs[i]), Math.Max(open, close)), low = Math.Min(Price(lows[i]), Math.Min(open, close));
            decimal volume = decimal.Floor(Math.Min(issuedShares * 0.0000333m * (decimal)activity[i], issuedShares * 0.002m));
            result.Add(new CandleData
            {
                open = (float)open, high = (float)high, low = (float)low, close = (float)close, exactClose = close, volume = (float)volume, nonExchangeVolume = volume,
                historical = true, marketDay = i / SeedMinutesPerDay - (SeedDays - 1), marketMinute = SeedOpenMinute + minute,
            });
            previous = close;
        }
        return result;
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
