using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 종목별 뉴스 관심도. 1 이상만 허용, 일정 시간마다 1로 수렴.
/// criticalValue 적용: 1 이상이면 곱하기, 1 미만이면 나누기(1/criticalValue)로 관심도 상승.
/// 원래 관심 가중치에 이 수치를 곱해 사용.
/// </summary>
public class StockNewsAttentionManager : MonoBehaviour
{
    public static StockNewsAttentionManager Instance { get; private set; }

    [Header("감쇠 (1로 수렴)")]
    [Tooltip("매 decayInterval초마다 관심도 = 관심도 * decayRate + (1 - decayRate) * 1")]
    [Range(0.9f, 1f)]
    public float decayRate = 0.92f;
    public float decayInterval = 3f;
    [Min(1f)] public float maximumAttention = 3f;

    private Dictionary<Stock, float> _attention = new Dictionary<Stock, float>();
    private readonly List<Stock> _decayStocks = new();
    private float _nextDecayTime;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                RegisterStock(stock);
            }
        }
        _nextDecayTime = Time.time + decayInterval;
        StockManager.OnStockDelisted += OnStockDelisted;
    }

    void OnDestroy()
    {
        StockManager.OnStockDelisted -= OnStockDelisted;
        Instance = null;
    }

    void OnStockDelisted(Stock stock)
    {
        if (stock == null)
        { return; }
        _attention.Remove(stock);
    }

    void Update()
    {
        if (Time.time >= _nextDecayTime)
        {
            _nextDecayTime = Time.time + decayInterval;
            Decay();
        }
    }

    /// <summary>종목 등록. 초기 관심도 1.</summary>
    public void RegisterStock(Stock stock)
    {
        if (stock == null)
        { return; }
        if (!_attention.ContainsKey(stock))
        { _attention[stock] = 1f; }
    }

    /// <summary>뉴스로 관심도 변동. criticalValue >= 1이면 곱하기, &lt; 1이면 1/criticalValue 곱하기 (예: 0.6 → 1.6666). 최소 1.</summary>
    public void ApplyNewsCriticalValue(Stock stock, float criticalValue)
    {
        if (stock == null || criticalValue <= 0f || float.IsNaN(criticalValue) || float.IsInfinity(criticalValue))
        { return; }
        float multiplier = criticalValue >= 1f ? criticalValue : (1f / criticalValue);
        _attention[stock] = Mathf.Clamp(GetAttention(stock) * multiplier, 1f, Mathf.Max(1f, maximumAttention));
    }

    /// <summary>현재 관심도. 미등록 종목은 1. 최소 1.</summary>
    public float GetAttention(Stock stock)
    {
        if (stock == null || !_attention.TryGetValue(stock, out float v) || float.IsNaN(v))
        { return 1f; }
        return Mathf.Clamp(v, 1f, Mathf.Max(1f, maximumAttention));
    }

    void Decay()
    {
        _decayStocks.Clear(); _decayStocks.AddRange(_attention.Keys);
        foreach (var stock in _decayStocks)
        {
            float retention = Mathf.Clamp01(decayRate);
            _attention[stock] = GetAttention(stock) * retention + (1f - retention);
        }
    }
}
