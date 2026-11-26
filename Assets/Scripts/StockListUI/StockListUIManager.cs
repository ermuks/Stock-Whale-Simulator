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
        FitOperationPanel();
        // StockManager Start 이후에 리스트 구성 (한 프레임 뒤)
        StartCoroutine(BuildListNextFrame());
    }

    /// <summary>목록 아래의 "체결 흐름" 패널을 없애고 목록을 그 자리까지 늘린다.</summary>
    private void FitOperationPanel()
    {
        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll == null || !(scroll.transform is RectTransform view) || !(view.parent is RectTransform screen)) { return; }
        var panel = screen.Find("Panel :: Operation Monitor") as RectTransform;
        if (panel == null || !panel.gameObject.activeSelf) { return; }
        // 체결 흐름 패널은 쓰지 않는다. 숨기고 그 자리까지 목록을 늘린다.
        float freed = (panel.anchorMax.y - panel.anchorMin.y) * screen.rect.height;
        panel.gameObject.SetActive(false);
        view.offsetMin = new Vector2(view.offsetMin.x, view.offsetMin.y - freed);
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
        CorporateEconomy.ListingQueued += BuildList;
    }

    void OnDisable()
    {
        MarketColors.Changed -= RefreshAll;
        if (VisibleList == this) { VisibleList = null; }
        StockManager.OnStockDelisted -= OnStockDelisted;
        StockManager.OnStockListed -= OnStockListed;
        CorporateEconomy.ListingQueued -= BuildList;
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(RefreshAll));
    }

    void OnStockListed(Stock stock)
    {
        if (content != null && itemPrefab != null)
        {
            BuildList();
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
        // 상장 종목에 더해, 다음 개장에 상장하기로 된 종목을 '상장 대기 중'으로 함께 보여 준다.
        var stocks = new List<Stock>(StockManager.Instance.stocks);
        if (CorporateEconomy.Instance != null) { foreach (var waiting in StockManager.Instance.UnlistedStocks) { if (CorporateEconomy.Instance.AwaitingListing(waiting)) { stocks.Add(waiting); } } }
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
            row.gameObject.SetActive(true); row.Set(stock,null);
            // 상장 대기 중인 종목은 눈에 띄게 맨 위에 둔다.
            if (stock.IsListed) { row.transform.SetAsLastSibling(); } else { row.transform.SetAsFirstSibling(); }
            _items.Add(row);
        }

        RefreshAll();
    }

    void RefreshAll()
    {
        if (StockManager.Instance == null)
        { return; }
        // 언어가 바뀌면 거래대금 단추의 글자도 따라 바꾼다.
        if (volumeLabel != null && turnoverLabelLocale != GameLocale.Revision) { SetTurnoverLabel(turnoverAscending); }

        // 행은 통계(StockStatistics)에서 값을 읽는다. 예전에는 종목마다 봉 목록을 새로 만들어 넘겼지만 쓰이지 않았고, 0.5초마다 쓰레기만 만들었다.
        for (int i = 0; i < _items.Count; i++)
        {
            var stock = _items[i].BoundStock;
            if (stock == null)
            { continue; }
            _items[i].Set(stock, null);
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
    /// <summary>
    /// 지금 종목 다음부터 목록을 위에서 아래로 훑어 내가 가진 종목 하나를 고른다. 끝에 닿으면 맨 위로 돌아간다.
    /// 가진 종목이 없으면 아무것도 하지 않는다(false).
    /// </summary>
    public bool SelectNextHeldStock()
    {
        var market = StockManager.Instance; var account = PlayerAccounts.Instance != null && PlayerAccounts.Instance.Ready ? PlayerAccounts.Instance.primary : null;
        if (market == null || account == null || _items.Count == 0)
        { return false; }
        int current = _items.FindIndex(item => item.BoundStock == market.CurrentStock);
        for (int step = 1; step <= _items.Count; step++)
        {
            int at = ((current < 0 ? -1 : current) + step) % _items.Count;
            var stock = _items[at].BoundStock;
            if (stock == null || stock == market.CurrentStock && step < _items.Count || account.GetOwnedVolume(stock) <= 0m)
            { continue; }
            int index = market.stocks.IndexOf(stock);
            if (index < 0)
            { continue; }
            market.SelectStock(index);
            Reveal(_items[at]);
            return true;
        }
        return false;
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
            // 첫 단추는 거래대금 순이다. 주가가 제각각이라 주식 수(거래량)로는 어디에 돈이 몰리는지 알 수 없다.
            float left = metric == 1 ? a.TurnoverSortValue : a.ChangeSortValue;
            float right = metric == 1 ? b.TurnoverSortValue : b.ChangeSortValue;
            int comparison = ascending ? left.CompareTo(right) : right.CompareTo(left);
            return comparison != 0 ? comparison : string.CompareOrdinal(a.BoundStock.symbol,b.BoundStock.symbol);
        });
        if (scroll != null) { scroll.StopMovement(); }
        for (int i = 0; i < _items.Count; i++) { _items[i].transform.SetSiblingIndex(i); }
        // Preserve the displayed direction of both buttons after switching metrics.
        if (metric == 1) { SetTurnoverLabel(ascending); }
        else { DeviceTerminalUI.Set(changeLabel,"상승률 " + (ascending ? "↑" : "↓")); }
        if (volumeSort != null) { volumeSort.image.color = metric == 1 ? new Color32(27,61,67,255) : new Color32(23,31,37,255); }
        if (changeSort != null) { changeSort.image.color = metric == 2 ? new Color32(27,61,67,255) : new Color32(23,31,37,255); }
    }
    private bool turnoverAscending = true;
    private int turnoverLabelLocale = -1;
    /// <summary>거래대금 단추의 글자. 언어마다 따로 적는다.</summary>
    private void SetTurnoverLabel(bool ascending)
    {
        turnoverAscending = ascending; turnoverLabelLocale = GameLocale.Revision;
        string word = GameLocale.Code == "ja" ? "売買代金" : GameLocale.Code == "en" ? "Turnover" : "거래대금";
        GameLocalizedText.Set(volumeLabel, word + " " + (ascending ? "↑" : "↓"), true);
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
        // 목록은 모니터 바탕 면보다 조금 앞에 떠 있다. 정렬 단추도 같은 깊이에 둔다(바탕 면과 같은 평면에 두면 비스듬히 볼 때 글자가 깜빡이며 잘린다).
        var place = root.anchoredPosition3D; place.z = area.anchoredPosition3D.z; root.anchoredPosition3D = place;
        // ScrollRect can drive its viewport back to full size. Reserve space on the scroll area itself.
        area.offsetMax = new Vector2(upper.x,upper.y-152);
        // 왼쪽이 상승률, 오른쪽이 거래대금.
        changeSort = SortButton(root,source,"Change","상승률 ↑",0,.49f,out changeLabel);
        volumeSort = SortButton(root,source,"Turnover","",.51f,.49f,out volumeLabel); SetTurnoverLabel(true);
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
