using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas 종목 리스트 관리. StockManager.stocks 기준으로 아이템 생성/갱신.
/// </summary>
public class StockListUIManager : MonoBehaviour
{
    public static StockListUIManager VisibleList { get; private set; }
    [Header("List")]
    public RectTransform content;
    public GameObject itemPrefab;
    [Header("Refresh")]
    public float refreshInterval = 0.5f;
    private readonly List<StockListItem> _items = new List<StockListItem>();
    private readonly List<StockListItem> _spares = new();
    private ScrollRect scroll;
    private Button volumeSort, changeSort;
    private TextMeshProUGUI volumeLabel, changeLabel;
    private bool volumeAscending, changeAscending;
    void Start()
    {
        if (content == null || itemPrefab == null)
        { return; }

        CreateSortHeader();
        // StockManager Start 이후에 리스트 구성 (한 프레임 뒤)
        StartCoroutine(BuildListNextFrame());
    }

    System.Collections.IEnumerator BuildListNextFrame()
    {
        yield return null;
        BuildList();
        InvokeRepeating(nameof(RefreshAll), refreshInterval, refreshInterval);
    }

    void OnEnable()
    {
        MarketColors.Changed += RefreshAll;
        VisibleList = this;
        StockManager.OnStockDelisted += OnStockDelisted;
        StockManager.OnStockListed += OnStockListed;
    }

    void OnDisable()
    {
        MarketColors.Changed -= RefreshAll;
        if (VisibleList == this) { VisibleList = null; }
        StockManager.OnStockDelisted -= OnStockDelisted;
        StockManager.OnStockListed -= OnStockListed;
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(RefreshAll));
    }

    void OnStockListed(Stock stock)
    {
        if (content != null && itemPrefab != null)
        {
            if (_items.Count < StockManager.Instance.stocks.Count)
            {
                BuildList();
            }
            else
            {
                RefreshAll();
            }
        }
    }

    void OnStockDelisted(Stock s)
    {
        BuildList();
    }

    public void ReconnectSession() { BuildList(); }

    void BuildList()
    {
        if (StockManager.Instance == null)
        { return; }
        var stocks = StockManager.Instance.stocks;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (stocks.Contains(_items[i].BoundStock))
            { continue; }
            _items[i].gameObject.SetActive(false); _spares.Add(_items[i]); _items.RemoveAt(i);
        }
        foreach (var stock in stocks)
        {
            if (_items.Exists(item => item.BoundStock == stock))
            { continue; }
            StockListItem row;
            if (_spares.Count > 0)
            {
                row = _spares[_spares.Count - 1]; _spares.RemoveAt(_spares.Count - 1);
            }
            else { row = Instantiate(itemPrefab,content,false).GetComponent<StockListItem>(); }
            row.gameObject.SetActive(true); row.Set(stock,null); row.transform.SetAsLastSibling(); _items.Add(row);
        }

        RefreshAll();
    }

    void RefreshAll()
    {
        if (StockManager.Instance == null)
        { return; }

        IReadOnlyList<CandleData> candles = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var stock = _items[i].BoundStock;
            if (stock == null)
            { continue; }
            if (CandleHistoryManager.Instance != null)
            {
                candles = CandleHistoryManager.Instance.GetLastCandlesIncludingForming(stock, 18);
            }
            else
            {
                candles = null;
            }

            _items[i].Set(stock, candles);
        }
    }

    public void SortVolume()
    { volumeAscending = !volumeAscending; ApplySort(1,volumeAscending); }
    public void SortChange()
    { changeAscending = !changeAscending; ApplySort(2,changeAscending); }
    public bool SelectRelativeVisibleStock(int direction)
    {
        var market = StockManager.Instance;
        if (market == null || _items.Count == 0 || direction == 0)
        { return false; }
        int current = _items.FindIndex(item => item.BoundStock == market.CurrentStock);
        int next = current < 0 ? direction < 0 ? _items.Count - 1 : 0 : (int)(((long)current + direction) % _items.Count);
        if (next < 0) { next += _items.Count; }
        int index = market.stocks.IndexOf(_items[next].BoundStock);
        if (index < 0)
        { return false; }
        market.SelectStock(index);
        Reveal(_items[next]);
        return true;
    }
    private void Reveal(StockListItem item)
    {
        if (scroll == null || scroll.viewport == null)
        { return; }
        var rect = (RectTransform)item.transform;
        float top = -rect.anchoredPosition.y - rect.rect.height * (1f - rect.pivot.y);
        float y = content.anchoredPosition.y, height = scroll.viewport.rect.height;
        if (top < y) { y = top; }
        else if (top + rect.rect.height > y + height) { y = top + rect.rect.height - height; }
        y = Mathf.Clamp(y,0,Mathf.Max(0,content.rect.height-height));
        scroll.StopMovement(); content.anchoredPosition = new Vector2(content.anchoredPosition.x,y);
    }
    private void ApplySort(int metric,bool ascending)
    {
        RefreshAll();
        _items.Sort((a,b) => {
            float left = metric == 1 ? a.VolumeSortValue : a.ChangeSortValue;
            float right = metric == 1 ? b.VolumeSortValue : b.ChangeSortValue;
            int comparison = ascending ? left.CompareTo(right) : right.CompareTo(left);
            return comparison != 0 ? comparison : string.CompareOrdinal(a.BoundStock.symbol,b.BoundStock.symbol);
        });
        if (scroll != null) { scroll.StopMovement(); }
        for (int i = 0; i < _items.Count; i++) { _items[i].transform.SetSiblingIndex(i); }
        // Preserve the displayed direction of both buttons after switching metrics.
        if (metric == 1) { DeviceTerminalUI.Set(volumeLabel,"거래량 " + (ascending ? "↑" : "↓")); }
        else { DeviceTerminalUI.Set(changeLabel,"상승률 " + (ascending ? "↑" : "↓")); }
        if (volumeSort != null) { volumeSort.image.color = metric == 1 ? new Color32(27,61,67,255) : new Color32(23,31,37,255); }
        if (changeSort != null) { changeSort.image.color = metric == 2 ? new Color32(27,61,67,255) : new Color32(23,31,37,255); }
    }
    private void CreateSortHeader()
    {
        scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.viewport == null)
        { return; }
        var source = itemPrefab.GetComponent<StockListItem>().textSymbol;
        var area = (RectTransform)scroll.transform;
        Vector2 upper = area.offsetMax, lower = area.offsetMin;
        var root = new GameObject("Area :: Stock Sort Tabs",typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(area.parent,false);
        root.gameObject.layer = scroll.gameObject.layer;
        root.anchorMin = new Vector2(area.anchorMin.x,area.anchorMax.y); root.anchorMax = area.anchorMax; root.pivot = new Vector2(.5f,1);
        root.offsetMin = new Vector2(lower.x,upper.y-136); root.offsetMax = upper;
        // ScrollRect can drive its viewport back to full size. Reserve space on the scroll area itself.
        area.offsetMax = new Vector2(upper.x,upper.y-152);
        volumeSort = SortButton(root,source,"Volume","거래량 ↑",0,.49f,out volumeLabel);
        changeSort = SortButton(root,source,"Change","상승률 ↑",.51f,.49f,out changeLabel);
        volumeSort.onClick.AddListener(SortVolume); changeSort.onClick.AddListener(SortChange);
    }
    private static Button SortButton(Transform parent,TextMeshProUGUI source,string name,string caption,float x,float width,out TextMeshProUGUI label)
    {
        var rect = new GameObject("Button :: Sort " + name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x,0); rect.anchorMax = new Vector2(x+width,1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(23,31,37,255); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        label = Instantiate(source,rect); label.name = "Text :: Sort " + name; label.gameObject.SetActive(true);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        GameLocalizedText.Set(label,caption); label.fontSize = 44; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal;
        label.color = new Color32(230,235,239,255); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }
}
