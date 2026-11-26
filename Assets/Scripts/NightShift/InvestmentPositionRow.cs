using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A pooled, read-only presentation of one actual ledger position.
/// 숫자 열은 그대로 두고, 그림 셋을 더한다: 왼쪽의 회사 로고, 이름 밑의 비중 막대(내 자산에서 이 종목의 몫),
/// 손익 밑의 좌우 막대(가운데 0에서 이익은 오른쪽 빨강, 손실은 왼쪽 파랑, ±10%에서 끝까지).
/// </summary>
public sealed class InvestmentPositionRow : MonoBehaviour
{
    public TextMeshProUGUI company, quantity, price, average, value, profit, percent;
    public Button button;
    public Image background, selection;
    public Stock Stock { get; private set; }
    private InvestmentDashboard dashboard;
    private RectTransform logo;
    private Image weightTrack, weightFill, profitTrack, profitFill;
    private Stock logoStock;
    public void Initialize(InvestmentDashboard owner)
    { dashboard = owner; button.onClick.AddListener(Select); }

    /// <summary>처음 한 번: 로고 자리를 만들고 이름·수량 글자를 그만큼 오른쪽으로 민다. 막대 둘을 만든다.</summary>
    private void Decorate()
    {
        if (logo != null) { return; }
        var row = (RectTransform)transform;
        logo = new GameObject("Area :: Holding Logo", typeof(RectTransform)).GetComponent<RectTransform>();
        logo.SetParent(row, false); logo.gameObject.layer = gameObject.layer;
        logo.anchorMin = new Vector2(.004f, .22f); logo.anchorMax = new Vector2(.044f, .78f); logo.offsetMin = logo.offsetMax = Vector2.zero;
        foreach (var label in new[] { company, quantity })
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(Mathf.Max(rect.anchorMin.x, .055f), rect.anchorMin.y);
            if (rect.anchorMax.x < rect.anchorMin.x + .05f) { rect.anchorMax = new Vector2(rect.anchorMin.x + .28f, rect.anchorMax.y); }
        }
        weightTrack = Bar("Weight Track", row, .055f, .30f, new Color32(27, 42, 51, 255));
        weightFill = Bar("Weight Fill", weightTrack.rectTransform, 0f, 1f, new Color32(127, 149, 163, 255));
        profitTrack = Bar("Profit Track", row, .80f, .995f, new Color32(27, 42, 51, 255));
        profitFill = Bar("Profit Fill", profitTrack.rectTransform, .5f, .5f, Color.white);
        var zero = Bar("Profit Zero", profitTrack.rectTransform, .497f, .503f, new Color32(70, 90, 102, 255)); zero.rectTransform.anchorMin = new Vector2(.497f, -1f); zero.rectTransform.anchorMax = new Vector2(.503f, 2f);
    }
    private Image Bar(string name, RectTransform parent, float x0, float x1, Color color)
    {
        var image = new GameObject("Image :: Holding " + name, typeof(RectTransform)).AddComponent<Image>();
        var rect = image.rectTransform; rect.SetParent(parent, false); image.gameObject.layer = gameObject.layer; image.raycastTarget = false; image.color = color;
        bool inside = parent != transform;
        rect.anchorMin = new Vector2(x0, inside ? 0f : .03f); rect.anchorMax = new Vector2(x1, inside ? 1f : .09f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }

    public void Bind(Stock stock, Trader account, bool selected, float weight = 0f)
    {
        Decorate();
        Stock = stock;
        decimal qty = account.GetOwnedVolume(stock), basis = account.GetOwnedCost(stock);
        bool priced = stock.currentPrice > 0m;
        decimal valuation = qty * stock.currentPrice, pnl = valuation - basis;
        DeviceTerminalUI.Set(company, stock.stockName);
        DeviceTerminalUI.Set(quantity, qty.ToString("N0") + "주"+(stock.IsListed ? "" : " · 비상장"));
        DeviceTerminalUI.Set(price, priced ? (stock.IsListed ? "" : "평가 ")+DeviceTerminalUI.Money(stock.currentPrice) : "—");
        DeviceTerminalUI.Set(average, "내 평균 " + (basis > 0m && qty > 0m ? DeviceTerminalUI.Money(basis / qty) : "—"));
        DeviceTerminalUI.Set(value, priced ? DeviceTerminalUI.Money(valuation) : "—");
        DeviceTerminalUI.Set(profit, priced ? InvestmentDashboard.Signed(pnl) + "돈" : "—");
        DeviceTerminalUI.Set(percent, priced && basis > 0m ? InvestmentDashboard.Rate(pnl, basis) : "—");
        profit.color = percent.color = priced ? InvestmentDashboard.ProfitColor(pnl) : InvestmentDashboard.Ink;
        background.color = selected ? new Color32(30, 54, 59, 255) : Color.clear;
        selection.enabled = selected;
        if (logoStock != stock) { logoStock = stock; UiPictograms.Logo(logo, stock, TabletKitStyle.Background); }
        weightFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(weight), 1f);
        float rate = priced && basis > 0m ? Mathf.Clamp((float)(pnl / basis) / .10f, -1f, 1f) : 0f;
        profitFill.color = rate >= 0f ? (Color)TabletKitStyle.Up : TabletKitStyle.Down;
        profitFill.rectTransform.anchorMin = new Vector2(rate >= 0f ? .5f : .5f + rate * .5f, 0f);
        profitFill.rectTransform.anchorMax = new Vector2(rate >= 0f ? .5f + rate * .5f : .5f, 1f);
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
