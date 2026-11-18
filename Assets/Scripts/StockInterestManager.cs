using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 종목별 관심도(가중치). 봇이 이번에 어느 종목을 볼지 고를 때 쓴다.
/// 평소에는 모든 종목이 기본값이라 거래가 고르게 퍼진다. 어떤 종목이 "제 평소보다" 달아오르면(열기) 그 종목으로 쏠리고, 열기는 서서히 식는다.
/// 열기는 두 가지에서 온다: 그 종목의 최근 거래대금이 제 평소 거래대금의 몇 배인가, 그리고 값이 최근 평균에서 얼마나 벗어났는가.
/// 원으로 센 거래대금 자체는 보지 않는다. 주가가 비싸거나 덩치가 큰 종목이라고 관심이 더 쌓이지 않는다.
/// 뉴스·세력 작전 같은 사건은 따로 더해지고, 감쇠로 기본값에 돌아온다.
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
    /// <summary>종목 하나의 맥박. Window는 이번 구간에 쌓인 거래대금, Fast는 최근 거래대금, Slow는 평소 거래대금, Anchor는 최근 평균 값, Heat는 열기(0이면 평소).</summary>
    private sealed class Pulse { public double Window, Fast, Slow; public decimal Anchor; public float Heat; public int Ticks; }
    private readonly Dictionary<Stock, Pulse> _pulse = new();
    // 처음 한동안(12구간 = 1분)은 평소 거래대금을 재기만 한다. 그 뒤로 최근은 빠르게(0.35), 평소는 느리게(0.03, 약 3분) 따라간다.
    private const int WarmTicks = 12;
    private const double FastFollow = .35, SlowFollow = .03;
    // 거래대금이 평소의 1.6배를 넘는 만큼, 값이 최근 평균에서 1.5% 넘게 벗어난 만큼 달아오른다. 식을 때는 구간마다 6%씩 빠진다(반으로 줄기까지 약 1분).
    private const float SurgeStart = 1.6f, SurgeLimit = 7f, MoveStart = .015f, MovePerUnit = 60f, MoveLimit = 8f, HeatLimit = 12f, Cooling = .94f;
    /// <summary>이 종목의 열기. 0이면 평소이고, 1이면 관심이 평소의 두 배다.</summary>
    public float Heat(Stock stock) => stock != null && _pulse.TryGetValue(stock, out var pulse) ? pulse.Heat : 0f;
    /// <summary>값이 최근 평균에서 얼마나 벗어나 있는가(+면 위). 달아오른 종목이 오르는 중인지 빠지는 중인지 가릴 때 쓴다.</summary>
    public float Drift(Stock stock) => stock != null && _pulse.TryGetValue(stock, out var pulse) && pulse.Anchor > 0m ? (float)(stock.currentPrice / pulse.Anchor) - 1f : 0f;
    /// <summary>이 종목의 최근 거래대금과 평소 거래대금(한 구간에 오간 돈). 확인용.</summary>
    public double RecentTurnover(Stock stock) => stock != null && _pulse.TryGetValue(stock, out var pulse) ? pulse.Fast : 0;
    public double UsualTurnover(Stock stock) => stock != null && _pulse.TryGetValue(stock, out var pulse) ? pulse.Slow : 0;
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
        _pulse.Remove(stock);
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

    /// <summary>
    /// 체결 시 호출. 이번 구간의 거래대금에 더해 둘 뿐이다. 관심도는 구간이 끝날 때 "평소의 몇 배인가"로 정한다.
    /// 예전에는 체결 한 건마다 거래대금(원)에 따라 관심도를 올렸다. 봇의 주문이 커지자 거의 모든 체결이 상한만큼 올려서
    /// 전 종목이 최대치에 붙었고(그래서 거래가 고르게 퍼졌고), 주가가 비싼 종목일수록 더 빨리 찼다.
    /// </summary>
    public void AddInterest(Stock stock, decimal price, decimal filledVolume)
    {
        if (stock == null || price <= 0m || filledVolume <= 0m)
        { return; }
        if (!_pulse.TryGetValue(stock, out var pulse)) { _pulse[stock] = pulse = new Pulse { Anchor = price }; }
        pulse.Window += (double)price * (double)filledVolume;
    }

    /// <summary>이벤트로 특정 종목 관심도 가중치 설정 (배수 또는 절대값)</summary>
    public void SetInterestMultiplier(Stock stock, float multiplier)
    {
        if (stock == null)
        { return; }

        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        { return; }
        _interest[stock] = Bounded(EventWeight(stock) * Mathf.Clamp(multiplier, 0f, maximumInterestMultiple));
    }

    /// <summary>이벤트로 특정 종목 관심도에 더하기</summary>
    public void AddInterestDelta(Stock stock, float delta)
    {
        if (stock == null)
        { return; }

        if (float.IsNaN(delta) || float.IsInfinity(delta))
        { return; }
        _interest[stock] = Bounded(EventWeight(stock) + delta);
    }

    /// <summary>고래 작전 시 해당 종목 관심도를 올려 다른 봇들이 이 종목에서 거래할 확률을 높임. 보너스는 작전 종류별 200/500/800.</summary>
    public void BoostInterestForWhaleOperation(Stock stock, float bonus)
    {
        AddInterestDelta(stock, bonus);
    }

    /// <summary>관심도 = 사건으로 쌓인 값(기본값의 0.5~8배) + 기본값 × 열기(최대 12).</summary>
    public float GetWeight(Stock stock)
    {
        if (stock == null)
        { return 0f; }

        // 열기는 사건 관심도의 상한(8배)과 따로 더한다. 가장 달아오른 종목은 평소 종목의 스무 배쯤 눈에 띈다.
        return EventWeight(stock) + Baseline * Mathf.Clamp(Heat(stock), 0f, HeatLimit);
    }
    /// <summary>사건(뉴스·세력 작전)으로 쌓인 값만. 열기는 빼고 센다.</summary>
    private float EventWeight(Stock stock) => Bounded(_interest.TryGetValue(stock, out var w) ? w : Baseline);

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
        // 관심도(열기 포함)는 그대로 반영한다: 달아오른 종목에는 확실히 쏠린다. 뉴스·수익 경험은 곱할 때 폭발하지 않도록 눌러서 반영한다.
        double taste = (double)Mathf.Clamp(attention, 1f, signalCap) * Mathf.Clamp(preference, 1f, signalCap);
        double product = interest * System.Math.Sqrt(taste) * RiskWeight(bot, stock);
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
            _interest[stock] = Bounded(EventWeight(stock) * retention + (1f - retention) * Baseline);
        }
        Beat();
    }

    /// <summary>한 구간(decayInterval초)이 끝났다. 종목마다 최근 거래대금과 평소 거래대금을 고치고 열기를 다시 정한다. 장이 닫혀 있으면 열기만 식는다.</summary>
    private void Beat()
    {
        bool open = MarketDayController.CanTrade;
        // 평소 거래대금이 거의 없는 종목은 체결 한두 건에도 몇 배로 보인다. 시장 평균의 10%를 바닥으로 둔다.
        double total = 0; int counted = 0;
        foreach (var pulse in _pulse.Values) { if (pulse.Ticks >= WarmTicks) { total += pulse.Slow; counted++; } }
        double floor = counted > 0 ? total / counted * .1 : 0;
        foreach (var pair in _pulse)
        {
            var stock = pair.Key; var pulse = pair.Value;
            if (!open || stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m)
            { pulse.Window = 0; pulse.Heat *= Cooling; continue; }
            double window = pulse.Window; pulse.Window = 0; pulse.Ticks++;
            if (pulse.Ticks <= WarmTicks)
            {
                pulse.Slow += (window - pulse.Slow) / pulse.Ticks; pulse.Fast = pulse.Slow; pulse.Anchor = stock.currentPrice; pulse.Heat = 0f;
                continue;
            }
            pulse.Fast += (window - pulse.Fast) * FastFollow; pulse.Slow += (window - pulse.Slow) * SlowFollow;
            pulse.Anchor += (stock.currentPrice - pulse.Anchor) * .02m;
            double usual = System.Math.Max(pulse.Slow, floor);
            float surge = usual <= 0 ? 0f : Mathf.Clamp((float)(pulse.Fast / usual) - SurgeStart, 0f, SurgeLimit);
            float away = pulse.Anchor <= 0m ? 0f : Mathf.Abs((float)(stock.currentPrice / pulse.Anchor) - 1f);
            float move = Mathf.Clamp((away - MoveStart) * MovePerUnit, 0f, MoveLimit);
            float target = Mathf.Min(HeatLimit, surge + move);
            // 달아오를 때는 빠르게, 식을 때는 천천히.
            pulse.Heat = target > pulse.Heat ? Mathf.Lerp(pulse.Heat, target, .5f) : Mathf.Max(target, pulse.Heat * Cooling);
        }
    }
}
