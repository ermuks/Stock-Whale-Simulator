using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 종목별 관심도(가중치). 거래가 발생하면 해당 종목 관심도 상승 → 봇이 그 종목으로 쏠림.
/// 이벤트로 가중치 조절 가능. 감쇠로 서서히 기본값으로 복귀.
/// </summary>
public class StockInterestManager : MonoBehaviour
{
    public static StockInterestManager Instance { get; private set; }

    [Header("거래 시 관심도")]
    [Tooltip("거래대금에 곱한 뒤 log(1+x)로 압축하는 계수. 체결 한 건의 증가량도 제한한다.")]
    public float interestPerTurnover = 0.0001f;
    [Tooltip("거래 체결 시 해당 종목에 더해지는 고정 관심도")]
    public float interestPerTrade = 0.5f;
    [Header("가격 변동 시 관심도 (세력 등으로 급등·급락 시 봇이 반응하도록)")]
    [Tooltip("체결가가 직전 대비 1% 변동할 때 더해지는 관심도. 예: 0.5면 100% 상승 시 +50")]
    public float interestPerPriceChangePct = 0.5f;
    [Header("감쇠 (기본값으로 복귀)")]
    [Tooltip("매 decayInterval초마다 관심도 = 관심도 * decayRate + (1 - decayRate) * baseInterest")]
    public float decayRate = 0.90f;
    public float decayInterval = 5f;
    [Tooltip("종목 기본 가중치. 모든 종목이 이 값에서 시작해 거래/이벤트로 증감 후 감쇠로 복귀")]
    public float baseInterest = 20f;
    [Header("관심 집중 제한")]
    [Min(1f)] public float maximumInterestMultiple = 8f;
    [Range(0f, 1f)] public float explorationShare = .25f;
    [Range(.01f, 1f)] public float maximumTradeBoostFraction = .25f;
    [Min(1f)] public float maximumSignalMultiplier = 3f;
    private Dictionary<Stock, float> _interest = new Dictionary<Stock, float>();
    private Dictionary<Stock, decimal> _lastPrice = new Dictionary<Stock, decimal>();
    private readonly List<Stock> _decayStocks = new();
    private float Baseline => Mathf.Max(1f, baseInterest);
    private float _nextDecayTime;
    void Awake()
    {
        Instance = this;
        if (StockNewsAttentionManager.Instance == null)
        {
            gameObject.AddComponent<StockNewsAttentionManager>();
        }
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

        stock.OnTradeExecutedWithStock -= OnStockTraded;
        _interest.Remove(stock);
        _lastPrice.Remove(stock);
    }

    void Start()
    {
        if (StockManager.Instance == null)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            RegisterStock(stock);
        }

        if (StockNewsAttentionManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                StockNewsAttentionManager.Instance.RegisterStock(stock);
            }
        }

        _nextDecayTime = SessionClock.Time + decayInterval;
    }

    void OnDestroy()
    {
        if (StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock != null)
                {
                    stock.OnTradeExecutedWithStock -= OnStockTraded;
                }
            }
        }

        Instance = null;
    }

    void OnStockTraded(Stock stock, decimal price, decimal volume)
    {
        AddInterest(stock, price, volume);
        if (_lastPrice.TryGetValue(stock, out decimal prev) && prev > 0m)
        {
            decimal pctChange = (price - prev) / prev;
            if (pctChange != 0m)
            {
                AddInterestDelta(stock, (float)System.Math.Abs(pctChange) * interestPerPriceChangePct * 100f);
            }
        }

        _lastPrice[stock] = price;
    }

    void Update()
    {
        if (SessionClock.Time >= _nextDecayTime)
        {
            _nextDecayTime = SessionClock.Time + decayInterval;
            Decay();
        }
    }

    /// <summary>체결 시 호출. 거래대금은 로그로 압축하고, 건별 증가량과 누적 관심도에 상한을 둔다.</summary>
    public void AddInterest(Stock stock, decimal price, decimal filledVolume)
    {
        if (stock == null || price <= 0m || filledVolume <= 0m)
        { return; }
        // 선형 거래대금 누적은 한 종목의 체결 → 관심 → 체결을 무제한 증폭시켰다.
        double turnover = (double)price * (double)filledVolume;
        double boost = Mathf.Max(0f, interestPerTrade) + System.Math.Log(1d + turnover * Mathf.Max(0f, interestPerTurnover));
        AddInterestDelta(stock, (float)System.Math.Min(Baseline * maximumTradeBoostFraction, boost));
    }

    /// <summary>이벤트로 특정 종목 관심도 가중치 설정 (배수 또는 절대값)</summary>
    public void SetInterestMultiplier(Stock stock, float multiplier)
    {
        if (stock == null)
        { return; }

        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        { return; }
        _interest[stock] = Bounded(GetWeight(stock) * Mathf.Clamp(multiplier, 0f, maximumInterestMultiple));
    }

    /// <summary>이벤트로 특정 종목 관심도에 더하기</summary>
    public void AddInterestDelta(Stock stock, float delta)
    {
        if (stock == null)
        { return; }

        if (float.IsNaN(delta) || float.IsInfinity(delta))
        { return; }
        _interest[stock] = Bounded(GetWeight(stock) + delta);
    }

    /// <summary>고래 작전 시 해당 종목 관심도를 올려 다른 봇들이 이 종목에서 거래할 확률을 높임. 보너스는 작전 종류별 200/500/800.</summary>
    public void BoostInterestForWhaleOperation(Stock stock, float bonus)
    {
        AddInterestDelta(stock, bonus);
    }

    /// <summary>관심도는 기본값의 0.5~8배 범위. 과거의 폭증한 값도 읽을 때 제한한다.</summary>
    public float GetWeight(Stock stock)
    {
        if (stock == null)
        { return 0f; }

        return Bounded(_interest.TryGetValue(stock, out var w) ? w : Baseline);
    }

    private float Bounded(float weight)
    {
        if (float.IsNaN(weight))
        { return Baseline; }
        return Mathf.Clamp(weight, Baseline * .5f, Baseline * Mathf.Max(1f, maximumInterestMultiple));
    }

    /// <summary>가중치에 따라 랜덤 종목 한 개 선택. 거래대금 기반 관심도 = 가중치.</summary>
    public Stock PickStockByWeight()
    {
        return PickStockByWeightForBot(null);
    }

    /// <summary>기존 관심·뉴스·선호도와 봇의 지연 반영 위험 가중치를 결합한다.</summary>
    public float SelectionWeight(TradeBot bot, Stock stock)
    {
        if (stock == null || !stock.AcceptsOrders)
        { return 0f; }

        var config = StockManager.Instance.economy;
        float interest = GetWeight(stock);
        if (float.IsNaN(interest) || float.IsInfinity(interest))
        {
            interest = baseInterest;
        }

        float attention = StockNewsAttentionManager.Instance == null ? 1f : StockNewsAttentionManager.Instance.GetAttention(stock);
        if (float.IsNaN(attention) || float.IsInfinity(attention))
        {
            attention = 1f;
        }

        float preference = bot == null ? 1f : 1f + Mathf.Max(0f, bot.GetPreference(stock));
        float signalCap = Mathf.Max(1f, Mathf.Min(config.maximumAttentionFactor, maximumSignalMultiplier));
        double signal = interest / Baseline * (double)Mathf.Clamp(attention, 1f, signalCap) * Mathf.Clamp(preference, 1f, signalCap);
        // 최근 사건은 선호하되 뉴스·수익 경험을 곱할 때 집중도가 폭발하지 않도록 압축한다.
        double product = Baseline * System.Math.Sqrt(signal) * RiskWeight(bot, stock);
        return (float)System.Math.Min(System.Math.Max(0f, config.maximumSelectionWeight), product);
    }

    public Stock PickStockByWeightForBot(Trader trader)
    {
        if (StockManager.Instance == null)
        { return null; }

        var bot = trader as TradeBot;
        var list = StockManager.Instance.stocks;
        bool explore = Random.value < Mathf.Clamp01(explorationShare);
        double sum = 0;
        foreach (var stock in list)
        {
            sum += explore ? RiskWeight(bot, stock) : SelectionWeight(bot, stock);
        }

        if (sum <= 0 || double.IsInfinity(sum) || double.IsNaN(sum))
        { return null; }

        double pick = Random.value * sum;
        Stock last = null;
        foreach (var stock in list)
        {
            float weight = explore ? RiskWeight(bot, stock) : SelectionWeight(bot, stock);
            if (weight <= 0f)
            { continue; }

            last = stock;
            pick -= weight;
            if (pick <= 0)
            { return stock; }
        }

        return last;
    }

    private static float RiskWeight(TradeBot bot, Stock stock)
    {
        if (stock == null || !stock.AcceptsOrders)
        { return 0f; }
        float risk = bot == null ? 1f : bot.RiskSelectionMultiplier(stock);
        return float.IsNaN(risk) || float.IsInfinity(risk) ? 0f : Mathf.Max(0f, risk);
    }

    /// <summary>관심도가 낮은(거래량 적은) 종목을 선호해 선택. 고래처럼 덜 주목받는 종목에서 작업할 때 사용.</summary>
    public Stock PickStockByInverseWeight()
    {
        if (StockManager.Instance == null || StockManager.Instance.stocks.Count == 0)
        { return null; }

        var list = StockManager.Instance.stocks;
        const float epsilon = 0.1f;
        float sum = 0f;
        foreach (var s in list)
        {
            if (s == null || !s.AcceptsOrders)
            { continue; }
            float w = GetWeight(s) + epsilon;
            sum += 1f / w;
        }

        if (sum <= 0f)
        { return null; }

        float r = Random.Range(0f, sum);
        Stock last = null;
        foreach (var s in list)
        {
            if (s == null || !s.AcceptsOrders)
            { continue; }
            last = s;
            r -= 1f / (GetWeight(s) + epsilon);
            if (r <= 0f)
            { return s; }
        }

        return last;
    }

    /// <summary>새 종목이 리스트에 추가될 때 호출 (StockManager에서)</summary>
    public void RegisterStock(Stock stock)
    {
        if (stock==null) { return; }
        stock.OnTradeExecutedWithStock -= OnStockTraded; stock.OnTradeExecutedWithStock += OnStockTraded;
        if (_interest.ContainsKey(stock))
        { return; }

        _interest[stock] = Baseline;
        _lastPrice[stock] = stock.currentPrice;
        if (StockNewsAttentionManager.Instance != null)
        {
            StockNewsAttentionManager.Instance.RegisterStock(stock);
        }
    }

    void Decay()
    {
        _decayStocks.Clear(); _decayStocks.AddRange(_interest.Keys);
        foreach (var stock in _decayStocks)
        {
            float retention = Mathf.Clamp01(decayRate);
            _interest[stock] = Bounded(GetWeight(stock) * retention + (1f - retention) * Baseline);
        }
    }
}
