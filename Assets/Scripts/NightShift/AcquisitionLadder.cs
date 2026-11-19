using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인수 사다리. 상장 기업을 시가총액이 작은 순서로 세워 놓고, 지분 50%를 넘겨 경영권을 차지한 회사를 "삼킨" 것으로 센다.
// 작은 회사부터 먹고 그 힘으로 더 큰 회사를 노리는 흐름을 보여 주는 길잡이다. 순서를 강제하지 않는다:
// 돈만 있으면 맨 위의 회사부터 사도 된다(지분 매입과 경영권 판정은 원래 있던 그대로다).
//
// 삼킨 회사는 원래대로 배당과 경영(정책·확장)을 주고, 여기에 더해 내 창업 기업의 성장이 빨라진다(시너지).
// 맨 아래 칸들은 비상장 소기업이다(주인에게 직접 값을 치르고 넘겨받는다). 그 위로 상장 기업이 이어진다.
// 시너지는 삼킨 회사마다 4% + 사다리 칸 × 1%이고(큰 회사일수록 크다) 합쳐서 150%까지다. 내 기업이 재투자로 생산성을 올리는 빠르기에 곱해진다.
// 돈을 지어내지 않는다. 경영권을 잃으면 그 회사의 시너지도 바로 사라진다.
public struct LadderRung
{
    public Stock Stock;
    /// <summary>사다리 칸(0이 가장 작은 회사).</summary>
    public int Rank;
    public decimal MarketCap, Owned, CostToControl;
    public bool Controlled;
    public float Bonus;
}

public sealed partial class CorporateEconomy
{
    public const float LadderBaseBonus = .04f, LadderRankBonus = .01f, LadderBonusLimit = 1.5f;
    private readonly List<LadderRung> ladder = new();
    private float ladderAt = -10f;

    /// <summary>사다리의 칸들(작은 회사부터). 내 창업 기업은 빼고, 상장돼 있고 거래되는 회사만 센다. 1초 동안은 같은 목록을 돌려준다.</summary>
    public IReadOnlyList<LadderRung> Ladder()
    {
        if (Time.unscaledTime - ladderAt < 1f && Time.unscaledTime >= ladderAt) { return ladder; }
        ladderAt = Time.unscaledTime; ladder.Clear();
        var manager = StockManager.Instance; var accounts = PlayerAccounts.Instance; var mine = FounderRaw;
        if (manager == null || WorldDramaDirector.Instance == null) { return ladder; }
        foreach (var stock in Companies())
        {
            // 상장 기업과 비상장 소기업만 사다리에 선다.
            if (stock != null && !stock.IsListed && !IsSmall(stock)) { continue; }
            if (stock == null || stock == mine || stock.IsDelisted || stock.issuedShares <= 0m || stock.currentPrice <= 0m) { continue; }
            decimal owned = accounts != null ? accounts.Owned(stock) : manager.playerAccount.GetOwnedVolume(stock);
            decimal need = Math.Max(0m, decimal.Floor(stock.issuedShares * .5m) + 1m - owned);
            ladder.Add(new LadderRung { Stock = stock, MarketCap = stock.currentPrice * stock.issuedShares, Owned = owned, CostToControl = need * (stock.IsListed ? stock.currentPrice : SmallAsk(stock)), Controlled = WorldDramaDirector.Instance.Controls(stock) });
        }
        ladder.Sort((a, b) => a.MarketCap.CompareTo(b.MarketCap));
        for (int i = 0; i < ladder.Count; i++) { var rung = ladder[i]; rung.Rank = i; rung.Bonus = LadderBaseBonus + LadderRankBonus * i; ladder[i] = rung; }
        return ladder;
    }
    /// <summary>지금 삼킨 회사의 수.</summary>
    public int LadderControlled { get { int count = 0; foreach (var rung in Ladder()) { if (rung.Controlled) { count++; } } return count; } }
    /// <summary>삼킨 회사들이 내 기업의 성장에 보태는 몫(0.34면 +34%).</summary>
    public float LadderSynergy
    {
        get { float total = 0f; foreach (var rung in Ladder()) { if (rung.Controlled) { total += rung.Bonus; } } return Mathf.Min(LadderBonusLimit, total); }
    }
    /// <summary>방금 이 회사의 경영권을 차지했다. 사다리에서 몇 번째 칸인지와 시너지를 알린다.</summary>
    private void AnnounceLadder(Stock stock)
    {
        ladderAt = -10f; int total = 0, taken = 0, rank = -1;
        foreach (var rung in Ladder()) { total++; if (rung.Controlled) { taken++; } if (rung.Stock == stock) { rank = rung.Rank; } }
        if (rank < 0) { return; }
        string name = GameLocale.Text(stock.stockName), bonus = Mathf.RoundToInt(LadderSynergy * 100f).ToString();
        MarketEventFeed.Instance?.Notify(GameLocale.Code == "ja" ? name + "を飲み込みました · 買収のはしご " + taken + "/" + total + " · 自社の成長 +" + bonus + "%"
            : GameLocale.Code == "en" ? "You swallowed " + name + " · ladder " + taken + "/" + total + " · your company grows +" + bonus + "% faster"
            : name + "을(를) 삼켰습니다 · 인수 사다리 " + taken + "/" + total + " · 내 기업 성장 +" + bonus + "%");
    }
}

/// <summary>태블릿 기업 화면 위에 뜨는 인수 사다리. 칸을 누르면 그 회사 화면으로 가서 지분을 살 수 있다.</summary>
public sealed class AcquisitionLadderUI : MonoBehaviour
{
    private const int Rows = 12;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(146, 161, 172, 255), Accent = new Color32(46, 230, 208, 255), Dim = new Color32(96, 110, 120, 255);
    private CompanyDashboardUI owner;
    private TMP_FontAsset font;
    private TextMeshProUGUI title, summary, note;
    private readonly TextMeshProUGUI[] headers = new TextMeshProUGUI[6];
    private readonly TextMeshProUGUI[,] cells = new TextMeshProUGUI[Rows, 6];
    private readonly Button[] rowButtons = new Button[Rows];
    private readonly Image[] rowBacks = new Image[Rows];
    private readonly Stock[] rowStocks = new Stock[Rows];
    private TextMeshProUGUI closeLabel, pageLabel;
    private Button previousPage, nextPage;
    private int page;
    private float next;
    private static readonly float[] Columns = { .04f, .10f, .40f, .58f, .70f, .86f }, Widths = { .05f, .29f, .17f, .11f, .15f, .11f };

    public static AcquisitionLadderUI Install(CompanyDashboardUI parent)
    {
        var holder = new GameObject("Panel :: Acquisition Ladder", typeof(RectTransform), typeof(Image)); holder.layer = parent.gameObject.layer; holder.transform.SetParent(parent.transform, false);
        Place((RectTransform)holder.transform, 0, 0, 1, 1); holder.GetComponent<Image>().color = new Color32(14, 23, 29, 255);
        var view = holder.AddComponent<AcquisitionLadderUI>(); view.owner = parent; view.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        view.Build(); holder.SetActive(false);
        return view;
    }
    public void Show()
    {
        gameObject.SetActive(true); transform.SetAsLastSibling(); next = 0f;
        // 다음 목표가 있는 쪽을 먼저 보여 준다.
        var rungs = CorporateEconomy.Instance == null ? null : CorporateEconomy.Instance.Ladder(); page = 0;
        if (rungs != null) { for (int i = 0; i < rungs.Count; i++) { if (!rungs[i].Controlled) { page = i / Rows; break; } } }
        Refresh();
    }
    private void Turn(int delta) { page += delta; Refresh(); }
    public void Close() { gameObject.SetActive(false); owner.Refresh(); }

    private void Build()
    {
        title = Label("Ladder Title", .04f, .025f, .50f, .07f, 44, Ink);
        summary = Label("Ladder Summary", .04f, .10f, .92f, .05f, 27, Accent);
        previousPage = Action("Ladder Previous", .60f, .035f, .055f, .07f, () => Turn(-1)); nextPage = Action("Ladder Next", .765f, .035f, .055f, .07f, () => Turn(1));
        previousPage.GetComponentInChildren<TextMeshProUGUI>(true).text = "‹"; nextPage.GetComponentInChildren<TextMeshProUGUI>(true).text = "›";
        pageLabel = Label("Ladder Page", .655f, .035f, .11f, .07f, 26, Muted); pageLabel.alignment = TextAlignmentOptions.Center;
        var close = Action("Close Ladder", .84f, .035f, .12f, .07f, Close); closeLabel = close.GetComponentInChildren<TextMeshProUGUI>(true);
        for (int column = 0; column < 6; column++)
        {
            headers[column] = Label("Ladder Header " + column, Columns[column], .17f, Widths[column], .04f, 22, Muted);
            if (column >= 2) { headers[column].alignment = TextAlignmentOptions.MidlineRight; }
        }
        for (int row = 0; row < Rows; row++)
        {
            int index = row; float y = .215f + row * .054f;
            rowButtons[row] = Action("Ladder Row " + row, .03f, y, .94f, .05f, () => Pick(index));
            rowBacks[row] = rowButtons[row].GetComponent<Image>();
            var own = rowButtons[row].GetComponentInChildren<TextMeshProUGUI>(true); if (own != null) { own.gameObject.SetActive(false); }
            for (int column = 0; column < 6; column++)
            {
                cells[row, column] = Label("Ladder Cell " + row + "-" + column, Columns[column], y, Widths[column], .05f, 25, Ink);
                if (column >= 2) { cells[row, column].alignment = TextAlignmentOptions.MidlineRight; }
                if (column >= 2 && column <= 4) { TradingOrderView.Numeric(cells[row, column]); }
            }
        }
        note = Label("Ladder Note", .04f, .885f, .92f, .09f, 23, Muted); note.textWrappingMode = TextWrappingModes.Normal; note.alignment = TextAlignmentOptions.TopLeft;
    }
    private void Pick(int row)
    {
        if (rowStocks[row] == null) { return; }
        gameObject.SetActive(false); owner.ShowCompany(rowStocks[row]);
    }

    void Update() { if (Time.unscaledTime >= next) { next = Time.unscaledTime + .5f; Refresh(); } }
    private void Refresh()
    {
        var economy = CorporateEconomy.Instance;
        if (economy == null) { return; }
        var rungs = economy.Ladder(); int taken = 0; foreach (var rung in rungs) { if (rung.Controlled) { taken++; } }
        decimal cash = owner.Account == null ? 0m : PlayerAccounts.Available(owner.Account);
        Set(title, Word("인수 사다리", "Acquisition ladder", "買収のはしご"));
        Set(closeLabel, Word("닫기", "Close", "閉じる"));
        Set(summary, Word("삼킨 회사 ", "Swallowed ", "飲み込んだ会社 ") + taken + " / " + rungs.Count + "   ·   " + Word("내 기업 성장 +", "your company grows +", "自社の成長 +") + Mathf.RoundToInt(economy.LadderSynergy * 100f) + "%"
            + (economy.PlayerCompany == null ? Word("  (기업을 세우면 적용)", "  (applies once you found a company)", "  (会社を設立すると適用)") : ""));
        string[] names = { "#", Word("회사", "Company", "会社"), Word("시가총액", "Market cap", "時価総額"), Word("내 지분", "My stake", "持ち株"), Word("경영권까지", "To control", "経営権まで"), Word("상태", "Status", "状態") };
        for (int column = 0; column < 6; column++) { Set(headers[column], names[column]); }
        // 다음 목표: 아직 못 삼킨 회사 가운데 가장 작은 것.
        int target = -1; for (int i = 0; i < rungs.Count; i++) { if (!rungs[i].Controlled) { target = i; break; } }
        int pages = Mathf.Max(1, (rungs.Count + Rows - 1) / Rows); page = Mathf.Clamp(page, 0, pages - 1); int offset = page * Rows;
        Set(pageLabel, (page + 1) + " / " + pages); previousPage.interactable = page > 0; nextPage.interactable = page < pages - 1;
        bool paged = pages > 1; if (previousPage.gameObject.activeSelf != paged) { previousPage.gameObject.SetActive(paged); nextPage.gameObject.SetActive(paged); pageLabel.gameObject.SetActive(paged); }
        for (int line = 0; line < Rows; line++)
        {
            int row = line + offset;
            bool used = row < rungs.Count;
            rowButtons[line].gameObject.SetActive(used);
            for (int column = 0; column < 6; column++) { cells[line, column].gameObject.SetActive(used); }
            rowStocks[line] = used ? rungs[row].Stock : null;
            if (!used) { continue; }
            var rung = rungs[row];
            bool affordable = !rung.Controlled && cash >= rung.CostToControl;
            Color tone = rung.Controlled ? Accent : row == target ? Ink : affordable ? Ink : Dim;
            Set(cells[line, 0], (row + 1).ToString());
            Set(cells[line, 1], rung.Stock.IsListed ? GameLocale.Text(rung.Stock.stockName) : rung.Stock.stockName + Word("  · 비상장", "  · unlisted", "  · 非上場"));
            Set(cells[line, 2], DeviceTerminalUI.Money(rung.MarketCap));
            Set(cells[line, 3], (rung.Stock.issuedShares > 0m ? rung.Owned / rung.Stock.issuedShares * 100m : 0m).ToString("0.0") + "%");
            Set(cells[line, 4], rung.Controlled ? "—" : DeviceTerminalUI.Money(rung.CostToControl));
            Set(cells[line, 5], rung.Controlled ? Word("삼킴 +", "Yours +", "完了 +") + Mathf.RoundToInt(rung.Bonus * 100f) + "%"
                : affordable ? Word("인수 가능", "Can buy", "買収可能") : row == target ? Word("다음 목표", "Next", "次の目標") : Word("자금 부족", "Short", "資金不足"));
            for (int column = 0; column < 6; column++) { cells[line, column].color = column == 5 && affordable ? Accent : tone; }
            rowBacks[line].color = row == target ? new Color(.10f, .17f, .20f, 1f) : new Color(.07f, .10f, .125f, 1f);
        }
        Set(note, Word("지분 50%를 넘기면 그 회사를 삼킨 것입니다. 배당과 경영권을 얻고, 내 기업의 성장이 빨라집니다(큰 회사일수록 더). 비상장 소기업은 주인에게 직접 값을 치르고 넘겨받습니다. 작은 회사부터 먹는 것이 쉽지만 순서는 자유입니다. 칸을 누르면 그 회사로 갑니다.",
            "Own more than 50% and the company is yours: dividends, control, and faster growth for your own company (more for bigger ones). Unlisted small companies are bought straight from their owner. Small ones are easier, but any order works. Tap a row to open that company.",
            "持ち株が50%を超えるとその会社はあなたのものです。配当と経営権を得て、自社の成長が速くなります(大きい会社ほど大きく)。非上場の小企業はオーナーから直接買い取ります。小さい会社からが楽ですが順番は自由です。行を押すとその会社を開きます。"));
    }

    private static void Set(TextMeshProUGUI label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); }
    }
    private TextMeshProUGUI Label(string name, float x, float y, float w, float h, float size, Color color)
    {
        var item = new GameObject("Text :: " + name, typeof(RectTransform), typeof(TextMeshProUGUI)); item.layer = gameObject.layer; item.transform.SetParent(transform, false); Place((RectTransform)item.transform, x, y, w, h);
        var label = item.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size; label.enableAutoSizing = false; label.color = color; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        return label;
    }
    private Button Action(string name, float x, float y, float w, float h, Action action)
    {
        var item = new GameObject("Button :: " + name, typeof(RectTransform), typeof(Image), typeof(Button)); item.layer = gameObject.layer; item.transform.SetParent(transform, false); Place((RectTransform)item.transform, x, y, w, h);
        var button = item.GetComponent<Button>(); button.targetGraphic = item.GetComponent<Image>();
        var text = Label(name + " Label", 0, 0, 1, 1, 27, Ink); text.transform.SetParent(item.transform, false); Place(text.rectTransform, 0, 0, 1, 1); text.alignment = TextAlignmentOptions.Center;
        TabletControlStyle.Button(button); text.font = font; text.fontSize = 27;
        button.onClick.AddListener(() => action());
        return button;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    { rect.anchorMin = new Vector2(x, 1 - y - height); rect.anchorMax = new Vector2(x + width, 1 - y); rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
