using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 태블릿 "기록" 탭. 예전에는 체결과 주문이 한 덩어리 글자로 나열됐다.
// 위에 오늘의 요약 세 칸을 두고, 아래는 열을 맞춘 표로 보여 준다: 체결 내역, 그 아래 주문 내역.
// 기록을 읽기만 한다. 계좌나 주문은 건드리지 않는다.
public sealed class TabletRecordsView : MonoBehaviour
{
    private const float RowHeight = 50f, HeadingHeight = 46f, Gap = 18f;
    // 열의 왼쪽 끝과 너비(표 너비에 대한 비율): 시각, 종목, 구분, 수량, 가격, 마지막 열.
    private static readonly float[] ColumnX = { .015f, .165f, .405f, .49f, .63f, .77f }, ColumnWidth = { .14f, .23f, .075f, .13f, .13f, .215f };
    private static readonly bool[] ColumnRight = { false, false, false, true, true, true };
    private static readonly string[] FillColumns = { "시각", "종목", "구분", "수량", "가격", "실현 손익" }, OrderColumns = { "시각", "종목", "구분", "체결 / 주문", "지정가", "상태" };

    /// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly Dictionary<string, (string en, string ja)> Words = new()
    {
        ["오늘 실현 손익"] = ("Realized today", "本日の実現損益"), ["오늘 체결"] = ("Fills today", "本日の約定"), ["오늘 매도 성적"] = ("Sells today", "本日の売り成績"),
        ["매수 {0} · 매도 {1}"] = ("{0} buys · {1} sells", "買い {0} · 売り {1}"), ["{0}승 · {1}패"] = ("{0} W · {1} L", "{0}勝 · {1}敗"),
        ["체결 내역"] = ("Fills", "約定履歴"), ["주문 내역"] = ("Orders", "注文履歴"), ["{0}건"] = ("{0}", "{0}件"),
        ["아직 체결이 없습니다"] = ("No fills yet", "まだ約定はありません"), ["아직 주문이 없습니다"] = ("No orders yet", "まだ注文はありません"),
        ["시각"] = ("Time", "時刻"), ["종목"] = ("Stock", "銘柄"), ["구분"] = ("Side", "区分"), ["수량"] = ("Qty", "数量"), ["가격"] = ("Price", "価格"),
        ["실현 손익"] = ("Realized", "実現損益"), ["체결 / 주문"] = ("Filled / Qty", "約定 / 注文"), ["지정가"] = ("Limit", "指値"), ["상태"] = ("Status", "状態"),
        ["매수"] = ("Buy", "買い"), ["매도"] = ("Sell", "売り"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    private sealed class Row
    {
        public RectTransform Rect;
        public Image Back;
        public readonly TextMeshProUGUI[] Cells = new TextMeshProUGUI[6];
    }

    private TabletAccountUI owner;
    private RectTransform content;
    private readonly TextMeshProUGUI[] summaryCaptions = new TextMeshProUGUI[3], summaryValues = new TextMeshProUGUI[3];
    private readonly List<Row> rows = new();
    private Row fillHeading, fillColumns, orderHeading, orderColumns, fillEmpty, orderEmpty;
    private int version = -1, locale = -1, day = -1;
    private float next;

    public static TabletRecordsView Install(TabletAccountUI tablet)
    {
        if (tablet == null || tablet.recordsPanel == null || tablet.ledgerContent == null || tablet.ledger == null) { return null; }
        var existing = tablet.recordsPanel.GetComponent<TabletRecordsView>();
        if (existing != null) { return existing; }
        var view = tablet.recordsPanel.AddComponent<TabletRecordsView>();
        view.owner = tablet; view.content = tablet.ledgerContent;
        view.Build();
        return view;
    }

    private void Build()
    {
        var panel = owner.recordsPanel.transform;
        // 한 덩어리로 찍던 글자는 쓰지 않는다.
        owner.ledger.gameObject.SetActive(false);
        // 요약 세 칸: 바로가기 단추 줄 아래.
        for (int i = 0; i < 3; i++)
        {
            var cell = TabletKitStyle.Surface("Record Summary " + i, panel, i * .34f, .095f, .32f, .125f, TabletKitStyle.Panel).transform;
            summaryCaptions[i] = TabletKitStyle.Label("Summary Caption", cell, "", .05f, .10f, .90f, .36f, 24); summaryCaptions[i].color = TabletKitStyle.Muted;
            summaryValues[i] = TabletKitStyle.Label("Summary Value", cell, "", .05f, .46f, .90f, .46f, 34);
        }
        // 표는 요약 아래부터 화면 끝까지.
        Transform table = content;
        while (table.parent != null && table.parent != panel) { table = table.parent; }
        if (table.parent == panel && table != content) { TabletKitStyle.Place((RectTransform)table, 0f, .245f, 1f, .755f); }
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        fillHeading = Line("Fill Heading", true); fillColumns = Line("Fill Columns", false); fillEmpty = Line("Fill Empty", false);
        orderHeading = Line("Order Heading", true); orderColumns = Line("Order Columns", false); orderEmpty = Line("Order Empty", false);
    }

    /// <summary>표의 한 줄. heading이면 구역 제목처럼 크게 쓴다.</summary>
    private Row Line(string name, bool heading)
    {
        var row = new Row { Rect = TabletKitStyle.Area(name, content, 0, 0, 1, 1) };
        row.Rect.anchorMin = new Vector2(0, 1); row.Rect.anchorMax = Vector2.one; row.Rect.pivot = new Vector2(.5f, 1);
        row.Back = row.Rect.gameObject.AddComponent<Image>(); row.Back.color = Color.clear; row.Back.raycastTarget = false;
        for (int i = 0; i < row.Cells.Length; i++)
        {
            var cell = TabletKitStyle.Label("Cell " + i, row.Rect, "", ColumnX[i], 0, ColumnWidth[i], 1, heading ? 30 : 25, ColumnRight[i] && !heading);
            if (ColumnRight[i] && !heading) { TradingOrderView.Numeric(cell); }
            row.Cells[i] = cell;
        }
        if (heading) { TabletKitStyle.Place(row.Cells[0].rectTransform, ColumnX[0], 0, .60f, 1); }
        return row;
    }
    private static float Put(Row row, float y, float height, bool visible)
    {
        row.Rect.gameObject.SetActive(visible);
        if (!visible) { return y; }
        row.Rect.anchoredPosition = new Vector2(0, -y); row.Rect.sizeDelta = new Vector2(0, height);
        return y + height;
    }
    private static void Write(TextMeshProUGUI label, string text, Color color)
    {
        if (!GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); }
        if (label.color != color) { label.color = color; }
    }
    public static int DayOf(string time)
    {
        // "3일 09:02"(지금)와 "DAY 3 09:02"(예전 저장) 둘 다 읽는다: 처음 나오는 숫자가 날짜다.
        int value = 0; bool found = false;
        foreach (char letter in time ?? "")
        {
            if (char.IsDigit(letter)) { value = value * 10 + (letter - '0'); found = true; }
            else if (found) { break; }
        }
        return found ? value : -1;
    }

    void OnEnable() { version = -1; next = 0f; }
    void Update()
    {
        if (owner == null || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .3f;
        int current = owner.events.records.Version + owner.orders.session.Version;
        int today = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.CalendarDate;
        if (current == version && locale == GameLocale.Revision && day == today) { return; }
        version = current; locale = GameLocale.Revision; day = today;
        Refresh(today);
    }

    private void Refresh(int today)
    {
        var fills = owner.events.records.Fills; var orders = owner.orders.session.History;
        // 요약: 오늘 체결만 센다.
        decimal realized = 0m; int buys = 0, sells = 0, wins = 0, losses = 0;
        foreach (var fill in fills)
        {
            if (DayOf(fill.Time) != today) { continue; }
            if (fill.Buy) { buys++; continue; }
            sells++; realized += fill.Realized;
            if (fill.Realized > 0m) { wins++; } else if (fill.Realized < 0m) { losses++; }
        }
        Color ink = TabletKitStyle.Ink, muted = TabletKitStyle.Muted;
        Write(summaryCaptions[0], T("오늘 실현 손익"), muted);
        Write(summaryValues[0], InvestmentDashboard.Signed(realized) + (GameLocale.Code == "ko" ? "돈" : " " + GameLocale.Text("돈")), realized > 0m ? TabletKitStyle.Up : realized < 0m ? TabletKitStyle.Down : ink);
        Write(summaryCaptions[1], T("오늘 체결"), muted);
        Write(summaryValues[1], string.Format(T("매수 {0} · 매도 {1}"), buys, sells), buys + sells > 0 ? ink : muted);
        Write(summaryCaptions[2], T("오늘 매도 성적"), muted);
        Write(summaryValues[2], string.Format(T("{0}승 · {1}패"), wins, losses), wins + losses > 0 ? ink : muted);

        while (rows.Count < fills.Count + orders.Count) { rows.Add(Line("Record Row " + (rows.Count + 1), false)); }
        float y = 0f; int used = 0;
        // 체결 내역.
        Write(fillHeading.Cells[0], T("체결 내역") + "  " + string.Format(T("{0}건"), fills.Count), ink);
        y = Put(fillHeading, y, HeadingHeight, true);
        for (int i = 0; i < 6; i++) { Write(fillColumns.Cells[i], T(FillColumns[i]), muted); }
        y = Put(fillColumns, y, RowHeight * .8f, fills.Count > 0);
        Write(fillEmpty.Cells[0], T("아직 체결이 없습니다"), muted); TabletKitStyle.Place(fillEmpty.Cells[0].rectTransform, ColumnX[0], 0, .9f, 1);
        y = Put(fillEmpty, y, RowHeight, fills.Count == 0);
        foreach (var fill in fills)
        {
            var row = rows[used];
            Write(row.Cells[0], GameLocale.Text(fill.Time ?? ""), muted);
            Write(row.Cells[1], GameLocale.Text(fill.Stock.stockName), ink);
            Write(row.Cells[2], T(fill.Buy ? "매수" : "매도"), fill.Buy ? TabletKitStyle.Up : TabletKitStyle.Down);
            Write(row.Cells[3], fill.Quantity.ToString("N0"), ink);
            Write(row.Cells[4], PriceTickPolicy.FormatPrice(fill.Price), ink);
            Write(row.Cells[5], fill.Buy ? "—" : InvestmentDashboard.Signed(fill.Realized), fill.Buy ? muted : fill.Realized > 0m ? TabletKitStyle.Up : fill.Realized < 0m ? TabletKitStyle.Down : ink);
            row.Back.color = (used & 1) == 0 ? new Color(1f, 1f, 1f, .025f) : Color.clear;
            y = Put(row, y, RowHeight, true); used++;
        }
        // 주문 내역.
        y += Gap;
        Write(orderHeading.Cells[0], T("주문 내역") + "  " + string.Format(T("{0}건"), orders.Count), ink);
        y = Put(orderHeading, y, HeadingHeight, true);
        for (int i = 0; i < 6; i++) { Write(orderColumns.Cells[i], T(OrderColumns[i]), muted); }
        y = Put(orderColumns, y, RowHeight * .8f, orders.Count > 0);
        Write(orderEmpty.Cells[0], T("아직 주문이 없습니다"), muted); TabletKitStyle.Place(orderEmpty.Cells[0].rectTransform, ColumnX[0], 0, .9f, 1);
        y = Put(orderEmpty, y, RowHeight, orders.Count == 0);
        int stripe = 0;
        foreach (var request in orders)
        {
            var row = rows[used];
            Write(row.Cells[0], GameLocale.Text(request.UpdatedAt ?? "—"), muted);
            Write(row.Cells[1], GameLocale.Text(request.Stock.stockName), ink);
            Write(row.Cells[2], T(request.Buy ? "매수" : "매도"), request.Buy ? TabletKitStyle.Up : TabletKitStyle.Down);
            Write(row.Cells[3], request.Filled.ToString("N0") + " / " + request.Quantity.ToString("N0"), ink);
            Write(row.Cells[4], request.Limit > 0m ? PriceTickPolicy.FormatPrice(request.Limit) : "—", ink);
            Write(row.Cells[5], GameLocale.Text(request.Status ?? ""), muted);
            row.Back.color = (stripe++ & 1) == 0 ? new Color(1f, 1f, 1f, .025f) : Color.clear;
            y = Put(row, y, RowHeight, true); used++;
        }
        for (int i = used; i < rows.Count; i++) { rows[i].Rect.gameObject.SetActive(false); }
        content.sizeDelta = new Vector2(content.sizeDelta.x, y + Gap);
    }
}
