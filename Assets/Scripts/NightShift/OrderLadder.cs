using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주문 탭의 호가 사다리. 지금 값 위로 "팔겠다는 값" 네 줄, 아래로 "사겠다는 값" 네 줄을 두고,
/// 내가 적은 값이 어디에 들어가는지 표시한다. 줄을 누르면 그 값이 가격 칸에 들어간다.
/// 사다리 밑 한 줄은 금융 용어 없이 이 값이면 어떻게 되는지 적는다("이 값이면 바로 사집니다" / "값이 내려와야 사집니다").
/// 읽기만 한다. 주문을 넣거나 거두지 않는다.
/// </summary>
public sealed class OrderLadder : MonoBehaviour
{
    private const int Side = 4;
    private const float W = 736f, H = 320f, RowH = 28f, Top = 32f;
    private static readonly Color Muted = new Color32(146, 161, 172, 255), Ink = new Color32(230, 235, 239, 255), Faint = new Color32(30, 45, 54, 255);
    private DeviceTerminalUI terminal;
    private RectTransform root;
    private TMP_FontAsset font;
    private float scale;
    private readonly Button[] rows = new Button[Side * 2];
    private readonly Image[] bars = new Image[Side * 2], marks = new Image[Side * 2];
    private readonly TextMeshProUGUI[] prices = new TextMeshProUGUI[Side * 2], volumes = new TextMeshProUGUI[Side * 2];
    private readonly decimal[] values = new decimal[Side * 2];
    private Image nowLine;
    private TextMeshProUGUI nowLabel, verdict, sellHead, buyHead;
    private readonly List<(decimal price, decimal volume)> asks = new(8), bids = new(8);
    private float next;

    public static OrderLadder Create(DeviceTerminalUI terminal, RectTransform area, TMP_FontAsset font, float scale)
    {
        var view = area.gameObject.AddComponent<OrderLadder>();
        view.terminal = terminal; view.root = area; view.font = font; view.scale = scale; view.Build(); return view;
    }
    private static string L(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;

    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h, float pw, float ph)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x / pw, 1f - (y + h) / ph); rect.anchorMax = new Vector2((x + w) / pw, 1f - y / ph); rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private TextMeshProUGUI Text(string name, Transform parent, float x, float y, float w, float h, float size, TextAlignmentOptions align, float pw = W, float ph = H)
    {
        var label = Rect("Text :: Ladder " + name, parent, x, y, w, h, pw, ph).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { label.font = font; }
        label.fontSize = size * scale; label.enableAutoSizing = false; label.color = Ink; label.raycastTarget = false; label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.fontStyle = FontStyles.Normal;
        return label;
    }
    private Image Box(string name, Transform parent, float x, float y, float w, float h, Color color, float pw = W, float ph = H)
    {
        var image = Rect("Image :: Ladder " + name, parent, x, y, w, h, pw, ph).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }

    private void Build()
    {
        sellHead = Text("Sell Head", root, 0, 0, 360, 30, 21, TextAlignmentOptions.MidlineLeft); sellHead.color = Muted;
        buyHead = Text("Buy Head", root, 376, 0, 360, 30, 21, TextAlignmentOptions.MidlineRight); buyHead.color = Muted;
        for (int i = 0; i < Side * 2; i++)
        {
            int index = i;
            float y = Top + (i < Side ? i : i + 1) * RowH;
            var rect = Rect("Button :: Ladder Row " + i, root, 0, y, W, RowH - 2f, W, H);
            var face = rect.gameObject.AddComponent<Image>(); face.color = TabletKitStyle.Hover;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = face;
            button.onClick.AddListener(() => { if (values[index] > 0m) { terminal.SetDraftPrice(values[index]); next = 0f; } });
            rows[i] = button;
            marks[i] = Box("Mine " + i, rect, 0, 0, 5, RowH - 2f, TabletKitStyle.Accent, W, RowH - 2f);
            prices[i] = Text("Price " + i, rect, 16, 0, 150, RowH - 2f, 22, TextAlignmentOptions.MidlineRight, W, RowH - 2f);
            bars[i] = Box("Bar " + i, rect, 186, 7, 10, RowH - 16f, Faint, W, RowH - 2f);
            volumes[i] = Text("Volume " + i, rect, 520, 0, 210, RowH - 2f, 20, TextAlignmentOptions.MidlineRight, W, RowH - 2f);
            TabletControlStyle.Row(button, false);
        }
        float nowY = Top + Side * RowH;
        nowLine = Box("Now Line", root, 186, nowY + RowH / 2f - 1f, 550, 2, Ink);
        nowLabel = Text("Now", root, 0, nowY, 180, RowH, 22, TextAlignmentOptions.MidlineRight);
        verdict = Text("Verdict", root, 0, Top + (Side * 2 + 1) * RowH + 6f, W, 34, 22, TextAlignmentOptions.MidlineLeft);
    }

    void Update()
    {
        if (terminal == null || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f;
        var stock = terminal.SelectedStock;
        Say(sellHead, L("↑ 매도 호가 (누르면 그 가격으로 지정)", "↑ Ask prices (tap to use)", "↑ 売り気配 (押すとその価格に)"));
        Say(buyHead, L("매수 호가 ↓", "Bid prices ↓", "買い気配 ↓"));
        if (stock == null || stock.orderBook == null) { return; }
        stock.orderBook.GetTopAsks(asks, Side); stock.orderBook.GetTopBids(bids, Side);
        decimal largest = 1m;
        foreach (var level in asks) { largest = Math.Max(largest, level.volume); }
        foreach (var level in bids) { largest = Math.Max(largest, level.volume); }
        decimal draft = terminal.DraftPrice;
        // 내 값이 들어갈 줄: 같은 값이 있으면 그 줄, 없으면 가장 가까운 줄.
        int mine = -1; decimal nearest = decimal.MaxValue;
        for (int i = 0; i < Side * 2; i++)
        {
            bool ask = i < Side;
            int at = ask ? Side - 1 - i : i - Side;
            var list = ask ? asks : bids;
            bool has = at < list.Count;
            values[i] = has ? list[at].price : 0m;
            rows[i].gameObject.SetActive(has);
            if (!has) { continue; }
            Say(prices[i], list[at].price.ToString("N0")); prices[i].color = ask ? TabletKitStyle.Up : TabletKitStyle.Down;
            Say(volumes[i], list[at].volume.ToString("N0") + L("주", " sh", "株")); volumes[i].color = Muted;
            float width = Mathf.Clamp((float)(list[at].volume / largest), .02f, 1f);
            var rect = bars[i].rectTransform; rect.anchorMax = new Vector2((186f + 320f * width) / W, rect.anchorMax.y);
            var tone = ask ? TabletKitStyle.Up : TabletKitStyle.Down; bars[i].color = new Color(tone.r, tone.g, tone.b, .38f);
            if (draft > 0m) { decimal gap = Math.Abs(list[at].price - draft); if (gap < nearest) { nearest = gap; mine = i; } }
        }
        for (int i = 0; i < Side * 2; i++) { bool on = i == mine && draft > 0m; if (marks[i].gameObject.activeSelf != on) { marks[i].gameObject.SetActive(on); } TabletControlStyle.Row(rows[i], on); if (on) { prices[i].color = Ink; } }
        Say(nowLabel, L("지금 ", "now ", "現在 ") + stock.currentPrice.ToString("N0"));
        // 이 값이면 어떻게 되는가(쉬운 말로).
        bool hasAsk = asks.Count > 0, hasBid = bids.Count > 0;
        decimal bestAsk = hasAsk ? asks[0].price : 0m, bestBid = hasBid ? bids[0].price : 0m;
        string text; Color color = Muted;
        if (draft <= 0m) { text = L("가격을 입력하거나 호가를 누르세요.", "Type a price or tap a row above.", "価格を入力するか気配を押してください。"); }
        else if (terminal.Buying)
        {
            if (hasAsk && draft >= bestAsk) { text = L("이 가격이면 바로 체결됩니다.", "At this price it fills right away.", "この価格ならすぐ約定します。"); color = TabletKitStyle.Up; }
            else { text = L("매도 호가가 이 가격까지 내려와야 체결됩니다(미체결로 대기).", "Fills only when asks come down to this price (stays open).", "売り気配がこの価格まで下がると約定します(未約定で待機)。"); }
        }
        else
        {
            if (hasBid && draft <= bestBid) { text = L("이 가격이면 바로 체결됩니다.", "At this price it fills right away.", "この価格ならすぐ約定します。"); color = TabletKitStyle.Down; }
            else { text = L("매수 호가가 이 가격까지 올라와야 체결됩니다(미체결로 대기).", "Fills only when bids rise to this price (stays open).", "買い気配がこの価格まで上がると約定します(未約定で待機)。"); }
        }
        Say(verdict, text); verdict.color = color;
    }
    private static void Say(TextMeshProUGUI label, string value) { if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); } }
}
