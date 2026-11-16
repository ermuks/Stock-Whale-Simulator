using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A pooled, read-only presentation of one actual ledger position.</summary>
public sealed class InvestmentPositionRow : MonoBehaviour
{
    public TextMeshProUGUI company, quantity, price, average, value, profit, percent;
    public Button button;
    public Image background, selection;
    public Stock Stock { get; private set; }
    private InvestmentDashboard dashboard;
    public void Initialize(InvestmentDashboard owner)
    { dashboard = owner; button.onClick.AddListener(Select); }
    public void Bind(Stock stock, Trader account, bool selected)
    {
        Stock = stock;
        decimal qty = account.GetOwnedVolume(stock), basis = account.GetOwnedCost(stock);
        bool priced = stock.currentPrice > 0m;
        decimal valuation = qty * stock.currentPrice, pnl = valuation - basis;
        DeviceTerminalUI.Set(company, stock.stockName);
        DeviceTerminalUI.Set(quantity, qty.ToString("N0") + "주"+(stock.IsListed ? "" : " · 비상장"));
        DeviceTerminalUI.Set(price, priced ? (stock.IsListed ? "" : "평가 ")+DeviceTerminalUI.Money(stock.currentPrice) : "—");
        DeviceTerminalUI.Set(average, "내 평균 " + (basis > 0m && qty > 0m ? DeviceTerminalUI.Money(basis / qty) : "—"));
        DeviceTerminalUI.Set(value, priced ? DeviceTerminalUI.Money(valuation) : "—");
        DeviceTerminalUI.Set(profit, priced ? InvestmentDashboard.Signed(pnl) + "원" : "—");
        DeviceTerminalUI.Set(percent, priced && basis > 0m ? InvestmentDashboard.Rate(pnl, basis) : "—");
        profit.color = percent.color = priced ? InvestmentDashboard.ProfitColor(pnl) : InvestmentDashboard.Ink;
        background.color = selected ? new Color32(30, 54, 59, 255) : Color.clear;
        selection.enabled = selected;
    }
    private void Select()
    {
        dashboard.Select(Stock);
        dashboard.navigation.ShowPage(Stock.IsListed ? TabletAccountUI.Page.Orders : TabletAccountUI.Page.Management);
        if (!Stock.IsListed)
        {
            var company=dashboard.navigation.GetComponentInChildren<CompanyDashboardUI>(true);
            company.ShowCompany(Stock); company.OpenGrowth();
        }
    }
}
