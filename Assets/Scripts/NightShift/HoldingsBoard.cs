using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자산 태블릿의 보유 종목판. 회계 장부식 표 대신, 종목 하나를 타일 하나로 보인다:
///   큰 회사 로고 · 이름 · 평가금액 · 손익(색과 크기로 먼저 읽힌다) · 밑의 비중 막대(내 자산에서 이 종목이 차지하는 몫).
/// 평가금액이 큰 순서로 놓고, 많으면 마지막 타일에 "외 n종목"을 적는다. 타일을 누르면 그 종목의 주문 화면으로 간다(예전 줄과 같은 동작).
/// 숫자는 그대로 다 있다. 정확한 평균가·수량은 타일 안의 작은 줄과 "선택 종목 상세"에서 본다.
/// </summary>
public sealed class HoldingsBoard : MonoBehaviour
{
    private const int Columns = 3, Rows = 2, Max = Columns * Rows;
    private sealed class Tile
    {
        public Button button; public Image face, weightTrack, weightFill, profitMark;
        public RectTransform logo; public TextMeshProUGUI name, detail, value, profit;
        public Stock stock, logoStock;
    }
    private readonly Tile[] tiles = new Tile[Max];
    private InvestmentDashboard dashboard;
    private RectTransform root;
    private TMP_FontAsset font;
    private float scale;
    private readonly List<Stock> owned = new(16);
    private readonly List<(Stock stock, decimal value)> ranked = new(16);
    private static readonly Color Face = new Color32(17, 28, 35, 255), FaceSelected = new Color32(26, 44, 52, 255), Muted = new Color32(146, 161, 172, 255);

    public static HoldingsBoard Create(RectTransform area, InvestmentDashboard dashboard, TMP_FontAsset font, float scale)
    {
        var view = area.gameObject.AddComponent<HoldingsBoard>(); view.root = area; view.dashboard = dashboard; view.font = font; view.scale = scale; view.Build(); return view;
    }
    private TextMeshProUGUI Text(Transform parent, string name, float x0, float y0, float x1, float y1, float size, Color color, bool right = false)
    {
        var label = new GameObject("Text :: Holding Tile " + name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        var rect = label.rectTransform; rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x0, 1f - y1); rect.anchorMax = new Vector2(x1, 1f - y0); rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (font != null) { label.font = font; }
        label.fontSize = size * scale; label.color = color; label.raycastTarget = false; label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private Image Box(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
    {
        var image = new GameObject("Image :: Holding Tile " + name, typeof(RectTransform)).AddComponent<Image>();
        var rect = image.rectTransform; rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x0, 1f - y1); rect.anchorMax = new Vector2(x1, 1f - y0); rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.color = color; image.raycastTarget = false; return image;
    }
    private void Build()
    {
        float gap = .018f, w = (1f - gap * (Columns - 1)) / Columns, h = (1f - gap * (Rows - 1)) / Rows;
        for (int i = 0; i < Max; i++)
        {
            int column = i % Columns, row = i / Columns;
            float x0 = column * (w + gap), y0 = row * (h + gap);
            var face = Box(root, "Face " + i, x0, y0, x0 + w, y0 + h, Face); face.raycastTarget = true;
            var tile = new Tile { face = face, button = face.gameObject.AddComponent<Button>() };
            tile.button.targetGraphic = face; TabletControlStyle.Row(tile.button, false); face.color = Face;
            var colors = tile.button.colors; colors.normalColor = Color.white; colors.selectedColor = Color.white; tile.button.colors = colors;
            int index = i; tile.button.onClick.AddListener(() => Pick(index));
            var t = face.transform;
            tile.logo = new GameObject("Area :: Holding Tile Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            tile.logo.SetParent(t, false); tile.logo.gameObject.layer = t.gameObject.layer;
            tile.logo.anchorMin = new Vector2(.04f, 1f - .50f); tile.logo.anchorMax = new Vector2(.27f, 1f - .08f); tile.logo.offsetMin = tile.logo.offsetMax = Vector2.zero;
            tile.name = Text(t, "Name", .31f, .07f, .97f, .29f, 29f, TabletKitStyle.Ink);
            tile.detail = Text(t, "Detail", .31f, .29f, .97f, .46f, 21f, Muted);
            tile.value = Text(t, "Value", .05f, .53f, .97f, .73f, 30f, TabletKitStyle.Ink); TradingOrderView.Numeric(tile.value);
            tile.profit = Text(t, "Profit", .05f, .72f, .97f, .88f, 25f, TabletKitStyle.Ink); TradingOrderView.Numeric(tile.profit);
            tile.weightTrack = Box(t, "Weight Track", .05f, .91f, .95f, .945f, new Color32(32, 47, 56, 255));
            tile.weightFill = Box(tile.weightTrack.transform, "Weight Fill", 0f, 0f, 1f, 1f, new Color32(150, 170, 184, 255));
            tile.profitMark = Box(t, "Profit Mark", 0f, 0f, .012f, 1f, Color.clear);
            tiles[i] = tile; face.gameObject.SetActive(false);
        }
    }
    private void Pick(int index)
    {
        var stock = tiles[index].stock;
        if (stock == null || dashboard == null) { return; }
        dashboard.Select(stock);
        dashboard.navigation.ShowPage(stock.IsListed ? TabletAccountUI.Page.Orders : TabletAccountUI.Page.Management);
        if (!stock.IsListed)
        {
            var company = dashboard.navigation.GetComponentInChildren<CompanyDashboardUI>(true);
            company.ShowCompany(stock); company.OpenGrowth();
        }
    }
    private static string L(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;
    private static void Say(TextMeshProUGUI label, string value) { if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); } }

    private int laidRows = -1;
    /// <summary>보유가 셋 이하면 한 줄(타일을 높게), 넷 이상이면 두 줄로 판을 다시 나눈다.</summary>
    private void Layout(int rows)
    {
        if (rows == laidRows) { return; }
        laidRows = rows;
        float gap = .018f, w = (1f - gap * (Columns - 1)) / Columns, h = (1f - gap * (rows - 1)) / rows;
        for (int i = 0; i < Max; i++)
        {
            int column = i % Columns, row = i / Columns;
            var rect = tiles[i].face.rectTransform;
            float x0 = column * (w + gap), y0 = row * (h + gap);
            rect.anchorMin = new Vector2(x0, 1f - Mathf.Min(1f, y0 + h)); rect.anchorMax = new Vector2(x0 + w, 1f - Mathf.Min(1f, y0)); rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
    /// <summary>계좌로 다시 채운다(0.5초마다).</summary>
    public void Refresh(Trader account)
    {
        ranked.Clear();
        if (account != null)
        {
            account.CopyOwnedStocks(owned);
            foreach (var stock in owned) { if (stock != null && account.GetOwnedVolume(stock) > 0m) { ranked.Add((stock, account.GetOwnedVolume(stock) * stock.currentPrice)); } }
        }
        ranked.Sort((a, b) => b.value.CompareTo(a.value));
        decimal whole = Math.Max(0m, PlayerAccounts.Available(account)); foreach (var item in ranked) { whole += item.value; }
        int shown = Math.Min(ranked.Count, Max);
        Layout(shown <= Columns ? 1 : Rows);
        bool overflow = ranked.Count > Max;
        var selected = dashboard != null ? dashboard.Selected : null;
        for (int i = 0; i < Max; i++)
        {
            var tile = tiles[i]; bool on = i < shown;
            if (tile.face.gameObject.activeSelf != on) { tile.face.gameObject.SetActive(on); }
            if (!on) { tile.stock = null; continue; }
            var stock = ranked[i].stock; tile.stock = stock;
            if (overflow && i == Max - 1)
            {
                // 마지막 칸: 나머지 종목을 한 칸에 묶는다.
                int rest = ranked.Count - (Max - 1); decimal restValue = 0m;
                for (int k = Max - 1; k < ranked.Count; k++) { restValue += ranked[k].value; }
                tile.stock = null; tile.logo.gameObject.SetActive(false);
                Say(tile.name, L($"그 밖 {rest}종목", $"{rest} more", $"その他{rest}銘柄")); Say(tile.detail, "");
                Say(tile.value, DeviceTerminalUI.Money(restValue)); Say(tile.profit, ""); tile.profitMark.color = Color.clear;
                tile.weightFill.rectTransform.anchorMax = new Vector2(whole > 0m ? Mathf.Clamp01((float)(restValue / whole)) : 0f, 1f);
                continue;
            }
            tile.logo.gameObject.SetActive(true);
            if (tile.logoStock != stock) { tile.logoStock = stock; UiPictograms.Logo(tile.logo, stock, Face); }
            decimal volume = account.GetOwnedVolume(stock), cost = account.GetOwnedCost(stock), value = ranked[i].value, profit = value - cost;
            float weight = whole > 0m ? (float)(value / whole) : 0f;
            Say(tile.name, GameLocale.Text(stock.stockName));
            Say(tile.detail, volume.ToString("N0") + L("주", " sh", "株") + (stock.IsListed ? "" : L(" · 비상장", " · unlisted", " · 非上場")) + "  ·  " + L("비중 ", "weight ", "比率 ") + (weight * 100f).ToString(weight < .1f ? "0.0" : "0") + "%");
            Say(tile.value, DeviceTerminalUI.Money(value));
            string rate = cost > 0m ? "  (" + InvestmentDashboard.Rate(profit, cost) + ")" : "";
            Say(tile.profit, InvestmentDashboard.Signed(decimal.Round(profit)) + rate);
            var tone = InvestmentDashboard.ProfitColor(profit); tile.profit.color = tone;
            tile.profitMark.color = profit == 0m ? Color.clear : tone;
            tile.weightFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(weight), 1f);
            tile.face.color = stock == selected ? FaceSelected : Face;
        }
    }
}
