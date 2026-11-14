using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시장 심리/센티먼트 관리자.
/// realValue와 currentPrice의 괴리, 전체 시장 방향성을 추적.
/// 봇들이 참조하여 불장/약세장에 맞는 매매를 하도록 유도.
/// </summary>
public class MarketSentiment : MonoBehaviour
{
    public static MarketSentiment Instance { get; private set; }

    public enum Sentiment { StrongBear, Bear, Neutral, Bull, StrongBull }

    [Header("센티먼트 판정 기준")]
    [Tooltip("realValue 대비 currentPrice 괴리 - 강세장 판정 (0.05 = +5%)")]
    public float bullThreshold = 0.03f;
    [Tooltip("realValue 대비 currentPrice 괴리 - 강한 강세장 판정")]
    public float strongBullThreshold = 0.08f;
    [Tooltip("realValue 대비 currentPrice 괴리 - 약세장 판정 (-5%)")]
    public float bearThreshold = -0.03f;
    [Tooltip("realValue 대비 currentPrice 괴리 - 강한 약세장 판정")]
    public float strongBearThreshold = -0.08f;

    [Header("현재 상태 (읽기 전용)")]
    [SerializeField] private Sentiment _overallSentiment = Sentiment.Neutral;
    [SerializeField] private float _averageDeviation;

    private Dictionary<Stock, float> _stockDeviations = new();

    public Sentiment OverallSentiment => _overallSentiment;
    public float AverageDeviation => _averageDeviation;

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
        if (stock != null)
            _stockDeviations.Remove(stock);
    }

    void OnDestroy()
    {
        _stockDeviations.Clear();
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        UpdateSentiment();
    }

    void UpdateSentiment()
    {
        if (StockManager.Instance == null) return;

        float totalDeviation = 0f;
        int count = 0;

        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || stock.realValue <= 0m || stock.currentPrice <= 0m) continue;

            float deviation = (float)((stock.currentPrice - stock.realValue) / stock.realValue);
            _stockDeviations[stock] = deviation;
            totalDeviation += deviation;
            count++;
        }

        if (count > 0)
        {
            _averageDeviation = totalDeviation / count;

            if (_averageDeviation >= strongBullThreshold)
                _overallSentiment = Sentiment.StrongBull;
            else if (_averageDeviation >= bullThreshold)
                _overallSentiment = Sentiment.Bull;
            else if (_averageDeviation <= strongBearThreshold)
                _overallSentiment = Sentiment.StrongBear;
            else if (_averageDeviation <= bearThreshold)
                _overallSentiment = Sentiment.Bear;
            else
                _overallSentiment = Sentiment.Neutral;
        }
    }

    /// <summary>특정 종목의 realValue 대비 currentPrice 괴리율</summary>
    public float GetStockDeviation(Stock stock)
    {
        if (stock == null) return 0f;
        return _stockDeviations.TryGetValue(stock, out float dev) ? dev : 0f;
    }

    /// <summary>특정 종목의 센티먼트</summary>
    public Sentiment GetStockSentiment(Stock stock)
    {
        float dev = GetStockDeviation(stock);

        if (dev >= strongBullThreshold) return Sentiment.StrongBull;
        if (dev >= bullThreshold) return Sentiment.Bull;
        if (dev <= strongBearThreshold) return Sentiment.StrongBear;
        if (dev <= bearThreshold) return Sentiment.Bear;
        return Sentiment.Neutral;
    }

    /// <summary>현재 시장 상황에서 매수 편향 (0~1, 0.5가 중립)</summary>
    public float GetBuyBias()
    {
        switch (_overallSentiment)
        {
            case Sentiment.StrongBull: return 0.75f;
            case Sentiment.Bull: return 0.6f;
            case Sentiment.StrongBear: return 0.25f;
            case Sentiment.Bear: return 0.4f;
            default: return 0.5f;
        }
    }

    /// <summary>특정 종목: realValue가 currentPrice보다 높으면 저평가 → 매수 기회</summary>
    public bool IsUndervalued(Stock stock)
    {
        if (stock == null || stock.realValue <= 0m) return false;
        return stock.currentPrice < stock.realValue * 0.97m;
    }

    /// <summary>특정 종목: realValue가 currentPrice보다 낮으면 고평가 → 매도 기회</summary>
    public bool IsOvervalued(Stock stock)
    {
        if (stock == null || stock.realValue <= 0m) return false;
        return stock.currentPrice > stock.realValue * 1.03m;
    }
}
