using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stable ordering and pooled rows; price refresh never submits orders or changes assets.</summary>
public sealed class InvestmentDashboard : MonoBehaviour
{
    public Trader account;
    public TabletAccountUI navigation;
    public InvestmentPositionRow rowTemplate;
    public RectTransform content;
    public ScrollRect scroll;
    public TextMeshProUGUI valuation, profit, available, total, count, cost, empty, sortLabel;
    public Button sort, detail;
    /// <summary>총자산 가운데 주식이 차지하는 몫을 보여 주는 띠(배치 쪽에서 만들어 넣는다).</summary>
    public UnityEngine.UI.Image allocationFill;
    public float rowHeight = 90f;
    public int SortMode { get; private set; }
    public IReadOnlyList<Stock> Ordered => ordered;
    public Stock Selected => selectedUnlisted ?? (StockManager.Instance == null ? null : StockManager.Instance.CurrentStock);
    private Stock selectedUnlisted, lastGlobal;
    private readonly List<Stock> owned = new(), ordered = new();
    private readonly List<InvestmentPositionRow> pool = new();
    private float next;
    private bool sorted;
    public static readonly Color Ink = new Color32(230,235,239,255);
    public static string Signed(decimal value) => value.ToString("+#,##0;-#,##0;0");
    public static string Rate(decimal pnl, decimal basis) => basis > 0m ? (pnl / basis * 100m).ToString("+0.00;-0.00;0.00") + "%" : "—";
    public static Color ProfitColor(decimal pnl) => pnl > 0m ? MarketColors.Up : pnl < 0m ? MarketColors.Down : Ink;
    void Start()
    { sort.onClick.AddListener(CycleSort); detail.onClick.AddListener(() => navigation.ShowPage(TabletAccountUI.Page.Detail)); }
    void OnEnable()
    {
        next = 0f;
        StockManager.OnStockSelected += FollowSelection;
        var current = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        if (lastGlobal != current) { FollowSelection(current); }
    }
    void OnDisable() { StockManager.OnStockSelected -= FollowSelection; }
    private void FollowSelection(Stock stock) { selectedUnlisted = null; lastGlobal = stock; }
    void Update()
    {
        if (Time.unscaledTime < next)
        { return; }
        next = Time.unscaledTime + .3f; Refresh();
    }
    public void CycleSort()
    { SortMode = (SortMode + 1) % 3; sorted = false; Refresh(); }
    private decimal Return(Stock stock)
    {
        decimal basis = account.GetOwnedCost(stock);
        return basis > 0m && stock.currentPrice > 0m ? account.GetOwnedVolume(stock) * stock.currentPrice / basis - 1m : decimal.MinValue;
    }
    public void Refresh()
    {
        bool ready = account != null && StockManager.Instance != null && StockManager.Instance.SetupConfirmed;
        if (!ready)
        {
            foreach (var row in pool) { row.gameObject.SetActive(false); }
            DeviceTerminalUI.Set(valuation,"—"); DeviceTerminalUI.Set(profit,"—"); DeviceTerminalUI.Set(available,"—"); DeviceTerminalUI.Set(total,"—");
            DeviceTerminalUI.Set(cost,"잔고 확인 중"); DeviceTerminalUI.Set(count,"보유 종목 —");
            empty.gameObject.SetActive(true); DeviceTerminalUI.Set(empty,"잔고를 불러오는 중입니다"); detail.interactable = false;
            return;
        }
        account.CopyOwnedStocks(owned);
        bool membership = owned.Count != ordered.Count;
        foreach (var stock in owned) { if (!ordered.Contains(stock)) { membership = true; } }
        if (membership)
        {
            ordered.RemoveAll(stock => !owned.Contains(stock));
            foreach (var stock in owned) { if (!ordered.Contains(stock)) { ordered.Add(stock); } }
        }
        // Re-sort only for explicit sorting or changed membership, never for price ticks.
        if ((!sorted || membership) && ordered.Count > 0)
        {
            ordered.Sort((a,b) => {
                int result = SortMode == 1 ? Return(b).CompareTo(Return(a)) : SortMode == 2 ? string.Compare(a.stockName,b.stockName,StringComparison.Ordinal) : (account.GetOwnedVolume(b)*b.currentPrice).CompareTo(account.GetOwnedVolume(a)*a.currentPrice);
                return result != 0 ? result : string.Compare(a.symbol,b.symbol,StringComparison.Ordinal);
            });
            sorted = true; membership = true;
        }
        decimal stockValue = 0m, basisTotal = 0m;
        bool priced = true;
        foreach (var stock in owned)
        {
            stockValue += account.GetOwnedVolume(stock) * stock.currentPrice;
            basisTotal += account.GetOwnedCost(stock);
            priced &= stock.currentPrice > 0m;
        }
        decimal pnl = stockValue - basisTotal;
        DeviceTerminalUI.Set(valuation, priced ? DeviceTerminalUI.Money(stockValue) : "—");
        DeviceTerminalUI.Set(profit, priced ? Signed(pnl) + "돈  (" + Rate(pnl,basisTotal) + ")" : "평가가격 확인 중"); profit.color = priced ? ProfitColor(pnl) : Ink;
        DeviceTerminalUI.Set(available, DeviceTerminalUI.Money(PlayerAccounts.Available(account)));
        DeviceTerminalUI.Set(total, priced ? DeviceTerminalUI.Money(account.cash + stockValue - PlayerAccounts.TaxDebt(account)) : "—");
        if (allocationFill != null)
        {
            decimal whole = Math.Max(0m, account.cash) + stockValue;
            allocationFill.rectTransform.anchorMax = new Vector2(whole > 0m ? Mathf.Clamp01((float)(stockValue / whole)) : 0f, 1f);
        }
        DeviceTerminalUI.Set(count,"보유 종목  " + owned.Count);
        DeviceTerminalUI.Set(cost,"총 매수원금  " + DeviceTerminalUI.Money(basisTotal));
        DeviceTerminalUI.Set(sortLabel,SortMode == 0 ? "평가금 높은 순" : SortMode == 1 ? "수익률 높은 순" : "종목명 순");
        empty.gameObject.SetActive(owned.Count == 0); DeviceTerminalUI.Set(empty,"아직 보유한 종목이 없습니다");
        while (pool.Count < ordered.Count)
        {
            var row = Instantiate(rowTemplate,content); row.name = "Button :: Holding " + pool.Count; row.Initialize(this); pool.Add(row);
        }
        if (membership)
        {
            Vector2 position = content.anchoredPosition;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ordered.Count * rowHeight);
            content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y,0f,Mathf.Max(0f,ordered.Count*rowHeight-scroll.viewport.rect.height)));
        }
        for (int i = 0; i < pool.Count; i++)
        {
            bool active = i < ordered.Count;
            if (pool[i].gameObject.activeSelf != active) { pool[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            if (membership)
            {
                var rect = (RectTransform)pool[i].transform;
                rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,1);
                rect.sizeDelta = new Vector2(0,rowHeight); rect.anchoredPosition = new Vector2(0,-i*rowHeight);
            }
            pool[i].Bind(ordered[i],account,Selected == ordered[i]);
        }
        detail.interactable = owned.Contains(Selected);
    }
    public void Select(Stock stock)
    {
        int index = StockManager.Instance.stocks.IndexOf(stock);
        if (index >= 0) { StockManager.Instance.SelectStock(index); }
        else { selectedUnlisted = stock; lastGlobal = StockManager.Instance.CurrentStock; }
        Refresh();
    }
}
