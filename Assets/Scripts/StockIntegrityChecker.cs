using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주식 무결성 검사기
/// 
/// 발행량 = 거래소창고 + 봇보유량 + 호가창매도물량
/// 
/// 구조:
/// - 거래소 창고: PlaceSellOrder 시 즉시 차감 → 호가창으로 이동
/// - 봇 매도: SellInternal 시 즉시 holdings 차감 → 호가창으로 이동
/// - 매수 체결: 호가창 매도 감소 + 매수자 holdings 증가
/// 
/// 따라서 항상: 발행량 = 창고 + 모든봇holdings + 호가창매도
/// </summary>
public class StockIntegrityChecker : MonoBehaviour
{
    public static StockIntegrityChecker Instance { get; private set; }

    [Header("검사 설정")]
    [Tooltip("검사 간격(초)")]
    public float checkInterval = 60f;
    [Tooltip("허용 오차 (소수점 오차 등)")]
    public decimal tolerance = 1m;
    [Tooltip("콘솔에 상세 로그 출력")]
    public bool verboseLog = false;
    [Tooltip("경고만 출력 (오류 대신)")]
    public bool warnOnly = false;
    private float _lastCheckTime;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (Time.time - _lastCheckTime >= checkInterval)
        {
            _lastCheckTime = Time.time;
            CheckAllStocks();
        }
    }

    [ContextMenu("수동 검사 실행")]
    public void CheckAllStocks()
    {
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        { return; }

        var traders = FindObjectsByType<Trader>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var stock in StockManager.Instance.stocks)
        {
            var result = CheckStock(stock, traders);
        }
    }

    public IntegrityResult CheckStock(Stock stock)
    { return CheckStock(stock, FindObjectsByType<Trader>(FindObjectsInactive.Include, FindObjectsSortMode.None)); }

    private IntegrityResult CheckStock(Stock stock, Trader[] traders)
    {
        var result = new IntegrityResult();
        result.stock = stock;
        result.issued = stock.issuedShares;
        if (ExchangeWarehouse.Instance != null)
        {
            result.warehouseHolding = ExchangeWarehouse.Instance.GetHolding(stock);
        }

        result.botHoldings = GetAllTraderHoldings(stock, traders);
        result.exchangeAskVolume = stock.orderBook.GetTotalAskVolume();
        result.totalTracked = result.warehouseHolding + result.botHoldings + result.exchangeAskVolume;
        result.difference = result.issued - result.totalTracked;
        result.isValid = result.difference >= -tolerance && result.difference <= tolerance;
        return result;
    }

    /// <summary>모든 트레이더(봇 + 플레이어 + 거래소봇)의 보유량 합계. 거래소봇 포함해야 발행량=창고+봇보유+매도호가 일치.</summary>
    decimal GetAllTraderHoldings(Stock stock, Trader[] traders)
    {
        decimal total = 0m;
        foreach (var trader in traders)
        {
            decimal holding = trader.GetHolding(stock);
            if (holding > 0m)
            {
                total += holding;
            }
        }

        return total;
    }

    public struct IntegrityResult
    {
        public Stock stock;
        public decimal issued;
        public decimal warehouseHolding;
        public decimal botHoldings;
        public decimal exchangeAskVolume;
        public decimal totalTracked;
        public decimal difference;
        public bool isValid;
    }
}
