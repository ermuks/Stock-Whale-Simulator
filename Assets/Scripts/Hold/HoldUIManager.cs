using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 실제 거래 계좌 보유 자산 UI. 보유금액·보유자산·총보유자산과 종목 리스트 관리.
/// Inspector에서 TMP_Text, Prefab, Parent 할당.
/// </summary>
public class HoldUIManager : MonoBehaviour
{
    public Trader account;
    [Header("고래 봇")]
    [Tooltip("비어 있으면 씬에서 WhaleBot 1개를 찾음")]
    public WhaleBot whale;
    [Header("요약 (TMP_Text 할당)")]
    public TextMeshProUGUI textCash;
    public TextMeshProUGUI textHoldingsValue;
    public TextMeshProUGUI textTotal;
    public TextMeshProUGUI textCashAvailability;
    public HoldListItem selectedPosition;
    [System.NonSerialized] public Stock selectedOverride;
    public GameObject viewRoot;
    public bool currencySuffix;
    public Image activeTabImage;
    [Header("리스트")]
    [Tooltip("한 행 프리팹 (HoldListItem 붙어 있어야 함)")]
    public GameObject listItemPrefab;
    [Tooltip("리스트 아이템이 생성될 부모 Transform")]
    public Transform listParent;
    [Header("갱신")]
    [Tooltip("이 간격(초)마다 갱신. 0이면 매 프레임")]
    public float refreshInterval = 0.3f;
    private float _lastRefresh;
    private readonly List<HoldListItem> _pool = new List<HoldListItem>();
    private readonly List<Stock> _ownedStocks = new();
    private readonly List<(Stock stock, decimal quantity, decimal avgPrice, decimal currentValue, decimal costBasis)> _rows = new();
    void OnEnable()
    {
        StockManager.OnStockSelected += FollowSelection;
        _lastRefresh = float.NegativeInfinity;
        if (activeTabImage != null) { activeTabImage.color = new Color32(23,55,62,255); }
    }
    void OnDisable()
    {
        StockManager.OnStockSelected -= FollowSelection;
        if (activeTabImage != null) { activeTabImage.color = new Color32(23,33,38,255); }
    }
    private void FollowSelection(Stock stock) { selectedOverride = null; }
    void Start()
    {
        if (account == null)
        {
            if (whale != null)
            {
                account = whale;
            }
            else if (StockManager.Instance != null && StockManager.Instance.useInitialEconomy)
            {
                account = StockManager.Instance.playerAccount;
            }
            else
            {
                whale = FindFirstObjectByType<WhaleBot>();
                account = whale;
            }
        }
    }

    void Update()
    {
        if (account == null || viewRoot != null && !viewRoot.activeInHierarchy)
        { return; }

        if (refreshInterval > 0f && Time.unscaledTime - _lastRefresh < refreshInterval)
        { return; }

        _lastRefresh = Time.unscaledTime;
        Refresh();
    }

    void Refresh()
    {
        decimal cash = account.cash;
        decimal holdingsValue = 0m;
        if (selectedPosition != null) { selectedPosition.SetFromAccount(selectedOverride ?? (StockManager.Instance == null ? null : StockManager.Instance.CurrentStock),account); }
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        {
            SetSummaryTexts(cash, 0m);
            ClearList();
            return;
        }

        var rows = _rows;
        rows.Clear();
        account.CopyOwnedStocks(_ownedStocks);
        foreach (var stock in _ownedStocks)
        {
            decimal qty = account.GetOwnedVolume(stock);
            if (qty <= 0m)
            { continue; }

            decimal avg = account.GetOwnedAverage(stock);
            decimal current = stock.currentPrice;
            decimal value = qty * current;
            decimal cost = account.GetOwnedCost(stock);
            holdingsValue += value;
            rows.Add((stock, qty, avg, value, cost));
        }

        SetSummaryTexts(cash, holdingsValue);
        RefreshList(rows);
    }

    void SetSummaryTexts(decimal cash, decimal holdingsValue)
    {
        decimal total = cash + holdingsValue;
        string unit = currencySuffix ? "원" : "";
        DeviceTerminalUI.Set(textCash,cash.ToString("N0") + unit);
        DeviceTerminalUI.Set(textHoldingsValue,holdingsValue.ToString("N0") + unit);
        DeviceTerminalUI.Set(textTotal,total.ToString("N0") + unit);
        DeviceTerminalUI.Set(textCashAvailability,"가용 현금 " + DeviceTerminalUI.Money(System.Math.Max(0m,cash-account.ReservedCash)) + "  ·  매수 예치 " + DeviceTerminalUI.Money(account.ReservedCash));
    }

    void RefreshList(List<(Stock stock, decimal quantity, decimal avgPrice, decimal currentValue, decimal costBasis)> rows)
    {
        if (listParent == null || listItemPrefab == null)
        { return; }

        while (_pool.Count < rows.Count)
        {
            var go = Instantiate(listItemPrefab, listParent);
            var item = go.GetComponent<HoldListItem>();
            if (item != null)
            {
                _pool.Add(item);
            }
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            bool active = i < rows.Count;
            _pool[i].gameObject.SetActive(active);
            if (active)
            {
                var r = rows[i];
                decimal profitPct = r.costBasis > 0m ? (r.currentValue - r.costBasis) / r.costBasis * 100m : 0m;
                decimal evalProfit = r.currentValue - r.costBasis;
                _pool[i].Set(r.stock.symbol, r.stock.stockName, profitPct, evalProfit, r.avgPrice, r.quantity);
            }
        }
    }

    void ClearList()
    {
        foreach (var item in _pool)
        {
            item.gameObject.SetActive(false);
        }
    }
}
