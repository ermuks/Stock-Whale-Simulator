using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 심리적 저항선 관리자: 라운드 넘버(10, 100, 1000, 10000원 등)에서 
/// 매도벽이 형성되고, 돌파 직전에 주문이 철회되는 현상을 시뮬레이션.
/// </summary>
public class PsychologicalLevelManager : MonoBehaviour
{
    public static PsychologicalLevelManager Instance { get; private set; }

    [Header("저항선 감지")]
    [Tooltip("저항선 근접 판정 비율 (0.02 = 2% 이내)")]
    public float approachThreshold = 0.02f;
    [Tooltip("돌파 직전 판정 비율 (0.005 = 0.5% 이내)")]
    public float breakthroughThreshold = 0.005f;

    [Header("벽 형성")]
    [Tooltip("저항선에 벽 형성 확률")]
    [Range(0f, 1f)]
    public float wallFormChance = 0.7f;
    [Tooltip("벽 물량 배율 (평소 매도량의 N배)")]
    public float wallVolumeMultiplier = 3f;

    [Header("벽 철회")]
    [Tooltip("돌파 직전 벽 철회 확률")]
    [Range(0f, 1f)]
    public float wallWithdrawChance = 0.6f;
    [Tooltip("철회 시 남기는 물량 비율")]
    [Range(0f, 0.5f)]
    public float withdrawRemainRatio = 0.1f;

    /// <summary>종목별 저항선 정보</summary>
    class ResistanceState
    {
        public decimal currentResistance;
        public decimal previousPrice;
        public float wallFormedTime;
        public bool isWallActive;
        public float lastCheckTime;
        public System.Action<decimal, decimal> tradeHandler;
    }

    private Dictionary<Stock, ResistanceState> _states = new();

    /// <summary>저항선 돌파 직전 이벤트 (봇들이 매도 주문 취소)</summary>
    public static event Action<Stock, decimal> OnBreakthroughImminent;
    /// <summary>저항선에 벽 형성 이벤트 (봇들이 매도벽 추가)</summary>
    public static event Action<Stock, decimal> OnWallForming;
    /// <summary>저항선 돌파 완료 이벤트</summary>
    public static event Action<Stock, decimal> OnBreakthroughComplete;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        foreach (var kvp in _states)
        {
            if (kvp.Key != null && kvp.Value.tradeHandler != null)
                kvp.Key.OnTradeExecuted -= kvp.Value.tradeHandler;
        }
        _states.Clear();

        if (Instance == this)
            Instance = null;
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
        if (stock == null) return;
        if (_states.TryGetValue(stock, out var state) && state.tradeHandler != null)
            stock.OnTradeExecuted -= state.tradeHandler;
        _states.Remove(stock);
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
    }

    public void RegisterStock(Stock stock)
    {
        if (stock == null || _states.ContainsKey(stock)) return;

        var state = new ResistanceState
        {
            currentResistance = GetNextResistance(stock.currentPrice),
            previousPrice = stock.currentPrice,
            isWallActive = false,
            lastCheckTime = Time.time
        };

        state.tradeHandler = (price, vol) => CheckPriceMovement(stock, price);
        stock.OnTradeExecuted += state.tradeHandler;

        _states[stock] = state;
    }

    void CheckPriceMovement(Stock stock, decimal newPrice)
    {
        if (!_states.TryGetValue(stock, out var state)) return;

        if (Time.time - state.lastCheckTime < 0.5f) return;
        state.lastCheckTime = Time.time;

        decimal resistance = state.currentResistance;
        if (resistance <= 0m) return;

        decimal distanceRatio = (resistance - newPrice) / resistance;
        bool isApproaching = state.previousPrice < resistance && newPrice < resistance;
        bool isMovingUp = newPrice > state.previousPrice;

        if (isApproaching && isMovingUp)
        {
            if (distanceRatio > 0m && distanceRatio <= (decimal)approachThreshold && !state.isWallActive)
            {
                if (UnityEngine.Random.value < wallFormChance)
                {
                    state.isWallActive = true;
                    state.wallFormedTime = Time.time;
                    OnWallForming?.Invoke(stock, resistance);
                }
            }

            if (distanceRatio > 0m && distanceRatio <= (decimal)breakthroughThreshold && state.isWallActive)
            {
                if (UnityEngine.Random.value < wallWithdrawChance)
                {
                    OnBreakthroughImminent?.Invoke(stock, resistance);
                }
            }
        }

        if (newPrice >= resistance)
        {
            OnBreakthroughComplete?.Invoke(stock, resistance);
            state.isWallActive = false;
            state.currentResistance = GetNextResistance(newPrice);
        }
        else if (newPrice < resistance * 0.95m && state.isWallActive)
        {
            state.isWallActive = false;
        }

        state.previousPrice = newPrice;
    }

    /// <summary>다음 심리적 저항선 계산 (라운드 넘버)</summary>
    public static decimal GetNextResistance(decimal price)
    {
        if (price <= 0m) return 10m;

        decimal[] roundUnits = { 10m, 50m, 100m, 500m, 1000m, 5000m, 10000m, 50000m, 100000m };

        foreach (var unit in roundUnits)
        {
            decimal nextRound = Math.Ceiling(price / unit) * unit;
            if (nextRound > price * 1.005m)
                return nextRound;
        }

        decimal bigUnit = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)price)));
        return Math.Ceiling(price / bigUnit) * bigUnit;
    }

    /// <summary>이전 심리적 저항선 (지지선)</summary>
    public static decimal GetPreviousSupport(decimal price)
    {
        if (price <= 0m) return 0m;

        decimal[] roundUnits = { 100000m, 50000m, 10000m, 5000m, 1000m, 500m, 100m, 50m, 10m };

        foreach (var unit in roundUnits)
        {
            decimal prevRound = Math.Floor(price / unit) * unit;
            if (prevRound < price * 0.995m && prevRound > 0m)
                return prevRound;
        }

        return Math.Floor(price / 10m) * 10m;
    }

    /// <summary>가격이 저항선 근처인지 확인</summary>
    public bool IsNearResistance(Stock stock, out decimal resistance)
    {
        resistance = 0m;
        if (!_states.TryGetValue(stock, out var state)) return false;

        resistance = state.currentResistance;
        if (resistance <= 0m) return false;

        decimal distanceRatio = (resistance - stock.currentPrice) / resistance;
        return distanceRatio > 0m && distanceRatio <= (decimal)approachThreshold;
    }
}
