using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrderBookUIManager : MonoBehaviour
{
    public Transform buyParent;
    public Transform sellParent;
    [Header("볼륨 표시 토글")]
    [Tooltip("거래량/거래대금 전환 토글 버튼")]
    public Button toggleButton;
    [Tooltip("토글 버튼 텍스트")]
    public TMPro.TMP_Text toggleButtonText;
    public enum VolumeDisplayMode
    {
        Volume,
        Turnover
    }

    private VolumeDisplayMode _displayMode = VolumeDisplayMode.Volume;
    /// <summary>현재 표시 모드 (Volume: 거래량, Turnover: 거래대금)</summary>
    public VolumeDisplayMode DisplayMode => _displayMode;

    private List<DrawRawOrder> buyRows = new();
    private List<DrawRawOrder> sellRows = new();
    private Stock currentStock;
    private bool _dirty = true;
    private long _drawnVersion = -1;
    private Canvas[] _canvases;
    private CanvasGroup[] _canvasGroups;
    private readonly List<(decimal price, decimal volume)> _bids = new(10);
    private readonly List<(decimal price, decimal volume)> _asks = new(10);
    void OnEnable()
    {
        StockManager.OnStockSelected += SetStock;
        _dirty = true;
    }

    void OnDisable()
    {
        StockManager.OnStockSelected -= SetStock;
    }

    void LateUpdate()
    {
        if (currentStock != null && (_dirty || _drawnVersion != currentStock.orderBook.Version))
        {
            Refresh(0m, 0m);
        }
    }

    void MarkDirty(decimal price, decimal volume)
    {
        _dirty = true;
    }

    void Start()
    {
        if (buyParent != null && sellParent != null)
        {
            _canvases = buyParent.GetComponentsInParent<Canvas>(true);
            _canvasGroups = buyParent.GetComponentsInParent<CanvasGroup>(true);
            InitializeRows();
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleDisplayMode);
        }

        UpdateToggleButtonText();
        if (StockManager.Instance == null)
        { return; }

        SetStock(StockManager.Instance.CurrentStock);
    }

    /// <summary>거래량 ↔ 거래대금 토글</summary>
    public void ToggleDisplayMode()
    {
        _displayMode = _displayMode == VolumeDisplayMode.Volume ? VolumeDisplayMode.Turnover : VolumeDisplayMode.Volume;
        UpdateToggleButtonText();
        Refresh(0m, 0m);
    }

    void UpdateToggleButtonText()
    {
        if (toggleButtonText != null)
        {
            // 글자가 단추보다 커서 위아래로 삐져나왔다. 단추 높이에 맞는 크기로 고정한다.
            toggleButtonText.enableAutoSizing = false; toggleButtonText.fontSize = 22f;
            toggleButtonText.alignment = TMPro.TextAlignmentOptions.Center; toggleButtonText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            GameLocalizedText.Set(toggleButtonText,_displayMode == VolumeDisplayMode.Volume ? "거래량 보기" : "거래대금 보기");
        }
    }

    void InitializeRows()
    {
        foreach (Transform child in buyParent)
        {
            var row=child.GetComponent<DrawRawOrder>(); row.SetSide(true); buyRows.Add(row);
        }

        foreach (Transform child in sellParent)
        {
            var row=child.GetComponent<DrawRawOrder>(); row.SetSide(false); sellRows.Add(row);
        }
    }

    public void SetStock(Stock stock)
    {
        if (currentStock != null)
        {
            currentStock.OnTradeExecuted -= MarkDirty;
        }

        currentStock = stock;
        if (currentStock == null)
        {
            foreach (var row in buyRows)
            {
                row.Clear();
            }

            foreach (var row in sellRows)
            {
                row.Clear();
            }

            return;
        }

        currentStock.OnTradeExecuted += MarkDirty;
        Refresh(0m, 0m);
    }

    void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleDisplayMode);
        }

        if (currentStock != null)
        {
            currentStock.OnTradeExecuted -= MarkDirty;
        }
    }

    private bool IsScreenVisible()
    {
        if (_canvases != null)
        {
            foreach (var canvas in _canvases)
            {
                if (canvas != null && !canvas.isActiveAndEnabled)
                { return false; }
            }
        }

        if (_canvasGroups != null)
        {
            foreach (var group in _canvasGroups)
            {
                if (group == null || !group.isActiveAndEnabled)
                { continue; }

                if (group.alpha <= 0f)
                { return false; }

                if (group.ignoreParentGroups)
                { break; }
            }
        }

        return true;
    }

    void Refresh(decimal price, decimal volume)
    {
        _dirty = true;
        if (!isActiveAndEnabled || currentStock == null || buyParent == null || sellParent == null || !buyParent.gameObject.activeInHierarchy || !sellParent.gameObject.activeInHierarchy || !IsScreenVisible())
        { return; }

        _dirty = false;
        _drawnVersion = currentStock.orderBook.Version;
        currentStock.orderBook.GetTopBids(_bids);
        var bids = _bids;
        currentStock.orderBook.GetTopAsks(_asks);
        var asks = _asks;
        decimal lastPrice = currentStock.orderBook.lastPrice;
        bool showTurnover = _displayMode == VolumeDisplayMode.Turnover;
        decimal maxValue = GetMaxDisplayValue(bids, asks, showTurnover);
        DrawSide(buyRows, bids, maxValue, lastPrice, showTurnover);
        DrawSide(sellRows, asks, maxValue, lastPrice, showTurnover);
    }

    void DrawSide(List<DrawRawOrder> rows, List<(decimal price, decimal volume)> data, decimal maxValue, decimal lastPrice, bool showTurnover)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (i < data.Count)
            {
                decimal displayValue = showTurnover ? data[i].price * data[i].volume : data[i].volume;
                rows[i].SetData(data[i].price, displayValue, maxValue, lastPrice, showTurnover);
            }
            else
            {
                rows[i].Clear();
            }
        }
    }

    decimal GetMaxDisplayValue(List<(decimal price, decimal volume)> bids, List<(decimal price, decimal volume)> asks, bool showTurnover)
    {
        decimal max = 0m;
        foreach (var b in bids)
        {
            decimal val = showTurnover ? b.price * b.volume : b.volume;
            if (val > max)
            {
                max = val;
            }
        }

        foreach (var a in asks)
        {
            decimal val = showTurnover ? a.price * a.volume : a.volume;
            if (val > max)
            {
                max = val;
            }
        }

        return max;
    }
}
