using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작전 탭 오른쪽의 도식. 고른 작전이 "지금 이 종목의 주문들에 무엇을 하는지"를 실제 값으로 그린다.
///   불붙이기: 팔겠다고 걸린 주문(매도 호가)을 아래부터 쌓고, 내 돈이 어디까지 사들이는지 칠한다 → 값이 몇 % 오르는가.
///   내려찍기: 사겠다고 걸린 주문(매수 호가)을 위부터 쌓고, 내 주식이 어디까지 받아 치는지 칠한다 → 값이 몇 % 내려가는가.
///   넘기기:   지금 값 위 네 계단에 내가 걸 매도 주문과, 그중 팔린 만큼.
///   허수 매수벽: 매수 호가 사이에 들어갈 내 벽과 다른 사람들 주문의 크기 비교.
///   종가 관리: 오늘 하루 시간 막대 위의 마감 직전 구간과 예산.
///   거래량 부풀리기: 남은 시간과 지금까지 부풀린 거래대금.
/// 계산은 PlayerTactics·ManipulationTools가 실제로 하는 것과 같은 규칙을 따른다(값이 맞아야 그림을 믿는다).
/// 그림 아래 한 문장(Sentence)은 금융 용어 없이 무슨 일이 일어나는지 적는다.
/// </summary>
public sealed class TacticDiagram : MonoBehaviour
{
    private const int Rows = 7;
    private static readonly Color Faint = new Color32(46, 64, 76, 255), Muted = new Color32(146, 161, 172, 255), Ink = new Color32(230, 235, 239, 255);
    private static readonly Color Amber = new Color32(242, 188, 100, 255);

    private RectTransform area;
    private TMP_FontAsset font;
    private float fontScale = 1f;
    private readonly Image[] bars = new Image[Rows], fills = new Image[Rows];
    private readonly TextMeshProUGUI[] prices = new TextMeshProUGUI[Rows], volumes = new TextMeshProUGUI[Rows];
    private Image bracket, bracketTop, bracketBottom, limitLine, nowLine, track, trackFill, window, nowPin, budgetTrack, budgetFill;
    private TextMeshProUGUI bracketLabel, limitLabel, nowLabel, caption, trackStart, trackEnd, budgetLabel, bigValue;
    private readonly List<(decimal price, decimal volume)> book = new(16);

    /// <summary>그림 아래에 적을 한 문장(그 언어로).</summary>
    public string Sentence { get; private set; } = "";
    /// <summary>이 작전을 지금 쓸 수 있는가(살 주문·팔 주식·돈이 있는가). 아니면 Sentence가 그 까닭이다.</summary>
    public bool Possible { get; private set; }

    public static TacticDiagram Create(RectTransform parent, TMP_FontAsset font, float fontScale)
    {
        var view = parent.gameObject.AddComponent<TacticDiagram>();
        view.area = parent; view.font = font; view.fontScale = fontScale;
        view.Build();
        return view;
    }

    private static string L(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;
    private static string Money(decimal value) => value.ToString("N0") + L("돈", " Ð", " Ð");
    private static string Shares(decimal value) => value.ToString("N0") + L("주", " sh", "株");
    private static string Pct(decimal value) => (value >= 0m ? "+" : "−") + Math.Abs(value * 100m).ToString(Math.Abs(value) < .1m ? "0.0" : "0") + "%";

    // ── 만들기 ──
    private void Build()
    {
        for (int i = 0; i < Rows; i++)
        {
            prices[i] = Text("Price " + i, 20, TextAlignmentOptions.MidlineRight);
            bars[i] = Box("Bar " + i, Faint);
            fills[i] = Box("Fill " + i, Color.white);
            volumes[i] = Text("Volume " + i, 18, TextAlignmentOptions.MidlineLeft);
        }
        bracket = Box("Bracket", Color.white); bracketTop = Box("Bracket Top", Color.white); bracketBottom = Box("Bracket Bottom", Color.white);
        bracketLabel = Text("Bracket Label", 18, TextAlignmentOptions.MidlineRight);
        limitLine = Box("Limit", Color.white); limitLabel = Text("Limit Label", 17, TextAlignmentOptions.MidlineRight);
        nowLine = Box("Now", Ink); nowLabel = Text("Now Label", 18, TextAlignmentOptions.MidlineRight);
        track = Box("Track", Faint); trackFill = Box("Track Fill", Amber); window = Box("Window", new Color(1f, .74f, .39f, .35f)); nowPin = Box("Now Pin", Ink);
        trackStart = Text("Track Start", 17, TextAlignmentOptions.MidlineLeft); trackEnd = Text("Track End", 17, TextAlignmentOptions.MidlineRight);
        budgetTrack = Box("Budget Track", Faint); budgetFill = Box("Budget Fill", Amber);
        budgetLabel = Text("Budget Label", 19, TextAlignmentOptions.MidlineLeft);
        bigValue = Text("Big Value", 40, TextAlignmentOptions.MidlineLeft); TradingOrderView.Numeric(bigValue);
        caption = Text("Caption", 18, TextAlignmentOptions.MidlineLeft); caption.color = Muted;
    }
    private Image Box(string name, Color color)
    {
        var rect = new GameObject("Image :: Diagram " + name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(area, false); rect.gameObject.layer = area.gameObject.layer;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        rect.gameObject.SetActive(false); return image;
    }
    private TextMeshProUGUI Text(string name, float size, TextAlignmentOptions align)
    {
        var rect = new GameObject("Text :: Diagram " + name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(area, false); rect.gameObject.layer = area.gameObject.layer;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { label.font = font; }
        label.fontSize = size * fontScale; label.enableAutoSizing = false; label.color = Ink; label.raycastTarget = false; label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.fontStyle = FontStyles.Normal;
        rect.gameObject.SetActive(false); return label;
    }
    /// <summary>도식 안의 자리(가로·세로 모두 0~1, 세로는 위에서부터).</summary>
    private static void At(Graphic graphic, float x0, float y0, float x1, float y1)
    {
        var rect = graphic.rectTransform;
        rect.anchorMin = new Vector2(x0, 1f - y1); rect.anchorMax = new Vector2(x1, 1f - y0); rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (!graphic.gameObject.activeSelf) { graphic.gameObject.SetActive(true); }
    }
    private static void Off(Graphic graphic) { if (graphic.gameObject.activeSelf) { graphic.gameObject.SetActive(false); } }
    private static void Say(TextMeshProUGUI label, string value) { if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); } }
    private void HideAll() { foreach (Transform child in area) { if (child.GetComponent<Graphic>() != null && child.name.Contains(":: Diagram ")) { child.gameObject.SetActive(false); } } }

    // ── 그리기 ──
    /// <summary>지금 값으로 그 작전을 그린다. run은 이 종목에서 돌고 있는 실행(없으면 null), fraction은 고른 비중(0~1).</summary>
    public void Show(PlayerTactics.Tool tool, Stock stock, Trader account, PlayerTactics.Run run, float fraction)
    {
        HideAll(); Possible = false; Sentence = "";
        if (stock == null || account == null || stock.orderBook == null) { return; }
        switch (tool)
        {
            case PlayerTactics.Tool.Ignite: Ignite(stock, account, run, fraction); break;
            case PlayerTactics.Tool.Slam: Slam(stock, account, fraction); break;
            case PlayerTactics.Tool.Unload: Unload(stock, account, run, fraction); break;
            case PlayerTactics.Tool.Wall: Wall(stock, account, run, fraction); break;
            case PlayerTactics.Tool.Close: Close(stock, account, run, fraction); break;
            default: Wash(stock, account, run); break;
        }
    }

    private static decimal Free(Trader account) => Math.Max(0m, account.cash - account.ReservedCash);

    /// <summary>
    /// 불붙이기를 지금 쓰면 마지막으로 사들일 값(last)과 쓸 돈(spent). 도식과 같은 계산이며, 검증 도구가 실제 결과와 견준다.
    /// 살 수 없으면 false.
    /// </summary>
    public static bool ForecastIgnite(Stock stock, Trader account, float fraction, out decimal last, out decimal spent)
    {
        last = 0m; spent = 0m;
        if (stock == null || account == null || stock.orderBook == null || !stock.orderBook.TryBest(out _, out decimal ask)) { return false; }
        decimal limit = PriceTickPolicy.CeilToTick(ask * (1m + PlayerTactics.IgniteReachFor(fraction)));
        decimal left = decimal.Floor(Math.Min(Free(account) * (decimal)Mathf.Clamp01(fraction), Free(account)) / limit);
        var asks = new List<(decimal price, decimal volume)>(DepthLevels); stock.orderBook.GetTopAsks(asks, DepthLevels);
        foreach (var level in asks)
        {
            if (level.price > limit || left <= 0m) { break; }
            decimal take = Math.Min(left, level.volume); left -= take; spent += take * level.price; last = level.price;
        }
        return last > 0m;
    }
    private static float RowTop(int row) => .06f + row * .118f;
    private const float RowHeight = .09f, BarLeft = .22f, BarRight = .80f;

    /// <summary>한 줄: 가격 · 막대(전체 잔량) · 그중 칠한 몫 · 수량 글자.</summary>
    private void Row(int row, decimal price, decimal volume, decimal taken, decimal largest, Color tone, Color priceColor, string note = null)
    {
        float top = RowTop(row), bottom = top + RowHeight;
        Say(prices[row], price.ToString("N0")); prices[row].color = priceColor; At(prices[row], 0f, top, .19f, bottom);
        float width = largest > 0m ? Mathf.Clamp((float)(volume / largest), .015f, 1f) : .015f;
        float right = BarLeft + (BarRight - BarLeft) * width;
        bars[row].color = new Color(tone.r, tone.g, tone.b, .22f); At(bars[row], BarLeft, top + .015f, right, bottom - .015f);
        if (taken > 0m)
        {
            float part = volume > 0m ? Mathf.Clamp01((float)(taken / volume)) : 1f;
            fills[row].color = tone; At(fills[row], BarLeft, top + .015f, BarLeft + (right - BarLeft) * part, bottom - .015f);
        }
        Say(volumes[row], note ?? Shares(volume)); volumes[row].color = taken > 0m ? Ink : Muted; At(volumes[row], right + .012f, top, 1f, bottom);
    }
    /// <summary>호가 한 줄(또는 이웃한 여러 값을 묶은 한 줄). merged가 1보다 크면 price는 묶음에서 지금 값과 가장 먼 값이다.</summary>
    private struct Level { public decimal price, volume, taken; public bool mine; public int merged; }
    private const int DepthLevels = 40;
    private readonly List<Level> levels = new(DepthLevels);
    /// <summary>
    /// 줄이 max보다 많으면 이웃한 값끼리 고르게 묶어 max줄로 만든다(수량과 칠한 몫은 더한다). keep 번째 줄(내 벽)은 묶지 않고 그대로 둔다.
    /// 잘라 내지 않고 묶는 까닭: 내 돈이 닿는 곳, 내 벽이 있는 곳이 언제나 그림 안에 있어야 한다.
    /// </summary>
    private static List<Level> Fit(List<Level> source, int max, int keep)
    {
        var result = new List<Level>(max);
        if (source.Count <= max) { foreach (var item in source) { var copy = item; copy.merged = 1; result.Add(copy); } return result; }
        int before = keep < 0 ? source.Count : keep, after = keep < 0 ? 0 : source.Count - keep - 1;
        int slots = keep < 0 ? max : max - 1;
        int slotsBefore = after == 0 ? slots : Mathf.Clamp(Mathf.RoundToInt(slots * before / (float)(before + after)), before > 0 ? 1 : 0, slots - (after > 0 ? 1 : 0));
        int slotsAfter = slots - slotsBefore;
        Merge(source, 0, before, slotsBefore, result);
        if (keep >= 0) { var mine = source[keep]; mine.merged = 1; result.Add(mine); }
        Merge(source, keep + 1, after, slotsAfter, result);
        return result;
    }
    private static void Merge(List<Level> source, int start, int count, int slots, List<Level> result)
    {
        if (count <= 0 || slots <= 0) { return; }
        slots = Math.Min(slots, count);
        for (int s = 0; s < slots; s++)
        {
            int from = start + s * count / slots, to = start + (s + 1) * count / slots;
            var group = new Level { merged = to - from };
            for (int i = from; i < to; i++) { group.volume += source[i].volume; group.taken += source[i].taken; group.price = source[i].price; }
            result.Add(group);
        }
    }
    private static decimal Largest(List<Level> rows) { decimal largest = 0m; foreach (var item in rows) { largest = Math.Max(largest, item.volume); } return largest; }
    private void Row(int row, Level level, decimal largest, Color tone, Color priceColor, string note)
    {
        Row(row, level.price, level.volume, level.taken, largest, tone, priceColor, note);
        // 묶은 줄은 값 앞에 "~"를 붙여 여러 값을 합쳤음을 알린다.
        if (level.merged > 1) { Say(prices[row], "~" + level.price.ToString("N0")); }
    }
    private void Bracket(int fromRow, int toRow, Color tone, string label)
    {
        float top = RowTop(fromRow) + .01f, bottom = RowTop(toRow) + RowHeight - .01f;
        bracket.color = bracketTop.color = bracketBottom.color = tone;
        At(bracket, .196f, top, .2f, bottom); At(bracketTop, .196f, top, .212f, top + .008f); At(bracketBottom, .196f, bottom - .008f, .212f, bottom);
        Say(bracketLabel, label); bracketLabel.color = tone;
    }
    private void LineAt(Image line, TextMeshProUGUI label, float y, Color tone, string text, bool dashed)
    {
        line.color = dashed ? new Color(tone.r, tone.g, tone.b, .55f) : tone; At(line, BarLeft, y - .003f, 1f, y + .003f);
        Say(label, text); label.color = tone; At(label, 0f, y - .045f, .19f, y + .045f);
    }

    private void Ignite(Stock stock, Trader account, PlayerTactics.Run run, float fraction)
    {
        var up = TabletKitStyle.Up;
        if (!stock.orderBook.TryBest(out _, out decimal ask)) { Sentence = L("지금 팔겠다는 주문이 없어 살 수 없습니다.", "Nobody is selling right now, so there is nothing to buy.", "今は売り注文がなく、買えません。"); return; }
        decimal reach = PlayerTactics.IgniteReachFor(fraction);
        decimal limit = PriceTickPolicy.CeilToTick(ask * (1m + reach));
        decimal budget = Free(account) * (decimal)Mathf.Clamp01(fraction);
        decimal quantity = decimal.Floor(Math.Min(budget, Free(account)) / limit);
        stock.orderBook.GetTopAsks(book, DepthLevels);
        // 실제 불붙이기와 같은 규칙: limit까지의 매도 주문을 싼 것부터 quantity만큼 사들인다.
        decimal left = quantity, spent = 0m, last = 0m; int reached = -1;
        levels.Clear();
        for (int i = 0; i < book.Count; i++)
        {
            bool within = book[i].price <= limit && left > 0m;
            decimal take = within ? Math.Min(left, book[i].volume) : 0m;
            if (within) { left -= take; spent += take * book[i].price; last = book[i].price; reached = i; }
            levels.Add(new Level { price = book[i].price, volume = book[i].volume, taken = take });
            // 사들일 줄 전부와 그 위 한 줄(넘지 못하는 주문)까지만 그린다.
            if (!within && i > reached && levels.Count >= Math.Max(3, reached + 2)) { break; }
        }
        // 줄이 많으면 이웃한 값끼리 묶는다(잘라 내지 않는다: 내 돈이 닿는 곳이 언제나 다 보이게).
        var rows = Fit(levels, Rows - 1, -1);
        decimal largest = Largest(rows);
        int lastTaken = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            Row(rows.Count - 1 - i, rows[i], largest, up, up, null);
            if (rows[i].taken > 0m) { lastTaken = i; }
        }
        LineAt(nowLine, nowLabel, RowTop(rows.Count) + .02f, Ink, L("지금 ", "now ", "現在 ") + stock.currentPrice.ToString("N0"), false);
        if (lastTaken >= 0) { Bracket(rows.Count - 1 - lastTaken, rows.Count - 1, TabletKitStyle.Accent, ""); }
        decimal lift = last > 0m && stock.currentPrice > 0m ? last / stock.currentPrice - 1m : 0m;
        if (run != null)
        {
            Possible = true;
            Sentence = L($"불을 붙인 뒤 남은 돈으로 그 자리를 받치는 중입니다. 받친 값 아래로 빠지면 사들여 지킵니다.", "Holding the lifted price with the cash left over; it buys any dip below it.", "上げた値を残りの資金で支えています。下がれば買って守ります。");
        }
        else if (quantity < 1m || reached < 0)
        {
            Sentence = quantity < 1m ? L("고른 비중으로는 한 주도 살 수 없습니다. 비중을 올려 보세요.", "This share of your cash can't buy even one share. Raise the size.", "この割合では1株も買えません。割合を上げてください。")
                : L($"{Pct(reach)} 안에 팔겠다는 주문이 없습니다.", $"No sell orders within {Pct(reach)}.", $"{Pct(reach)}以内に売り注文がありません。");
        }
        else
        {
            Possible = true;
            Sentence = L($"{last:N0}돈까지 사들이면 값이 {Pct(lift)} 오릅니다. 약 {Money(spent)}을 쓰고, 20초 동안 남은 돈으로 그 값을 받칩니다.",
                $"Buying up to {last:N0} lifts the price {Pct(lift)}. About {Money(spent)} is spent; the rest holds the price for 20 seconds.",
                $"{last:N0}まで買い上げると値が{Pct(lift)}上がります。約{Money(spent)}を使い、残りで20秒間その値を支えます。");
        }
    }

    private void Slam(Stock stock, Trader account, float fraction)
    {
        var down = TabletKitStyle.Down;
        if (!stock.orderBook.TryBest(out decimal bid, out _)) { Sentence = L("지금 사겠다는 주문이 없어 팔 곳이 없습니다.", "Nobody is buying right now, so there is nowhere to sell.", "今は買い注文がなく、売れません。"); return; }
        decimal limit = Math.Max(1m, PriceTickPolicy.FloorToTick(bid * (1m - PlayerTactics.SlamDepthFor(fraction))));
        decimal shares = decimal.Floor(account.GetHolding(stock) * (decimal)Mathf.Clamp01(fraction));
        stock.orderBook.GetTopBids(book, DepthLevels);
        decimal left = shares, received = 0m, last = 0m; int reached = -1;
        levels.Clear();
        for (int i = 0; i < book.Count; i++)
        {
            bool within = book[i].price >= limit && left > 0m;
            decimal take = within ? Math.Min(left, book[i].volume) : 0m;
            if (within) { left -= take; received += take * book[i].price; last = book[i].price; reached = i; }
            levels.Add(new Level { price = book[i].price, volume = book[i].volume, taken = take });
            if (!within && i > reached && levels.Count >= Math.Max(3, reached + 2)) { break; }
        }
        var rows = Fit(levels, Rows - 1, -1);
        decimal largest = Largest(rows);
        LineAt(nowLine, nowLabel, .045f, Ink, L("지금 ", "now ", "現在 ") + stock.currentPrice.ToString("N0"), false);
        int lastTaken = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            Row(i + 1, rows[i], largest, down, down, null);
            if (rows[i].taken > 0m) { lastTaken = i; }
        }
        if (lastTaken >= 0) { Bracket(1, lastTaken + 1, TabletKitStyle.Accent, ""); }
        decimal drop = last > 0m && stock.currentPrice > 0m ? last / stock.currentPrice - 1m : 0m;
        if (shares < 1m) { Sentence = L("던질 주식이 없습니다. 이 종목을 먼저 가지고 있어야 합니다.", "You have no shares here to dump.", "投げる株がありません。先にこの銘柄を持つ必要があります。"); return; }
        if (reached < 0) { Sentence = L("6% 안에서 사 줄 주문이 없습니다.", "No buyers within 6%.", "6%以内に買い注文がありません。"); return; }
        Possible = true;
        Sentence = L($"{last:N0}돈까지 한꺼번에 팔면 값이 {Pct(drop)} 내려갑니다. 약 {Money(received)}을 받습니다. 싸게 다시 살 때 씁니다.",
            $"Selling down to {last:N0} drops the price {Pct(drop)}. You receive about {Money(received)}. Use it to buy back cheaper.",
            $"{last:N0}まで一気に売ると値が{Pct(drop)}下がります。約{Money(received)}を受け取ります。安く買い戻すときに使います。");
    }

    private static readonly decimal[] UnloadSteps = { .004m, .012m, .022m, .035m };
    private void Unload(Stock stock, Trader account, PlayerTactics.Run run, float fraction)
    {
        var amber = Amber;
        decimal total = run != null ? run.Budget : decimal.Floor(account.GetHolding(stock) * (decimal)Mathf.Clamp01(fraction));
        decimal slice = decimal.Floor(total / UnloadSteps.Length), sold = run != null ? run.Spent : 0m;
        decimal largest = Math.Max(1m, total - slice * (UnloadSteps.Length - 1));
        decimal soldLeft = sold;
        for (int i = 0; i < UnloadSteps.Length; i++)
        {
            decimal price = run != null && run.Ladder.Count > i ? run.Ladder[i] : PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + UnloadSteps[i]));
            decimal quantity = i == UnloadSteps.Length - 1 ? total - slice * (UnloadSteps.Length - 1) : slice;
            decimal filled = Math.Min(soldLeft, quantity); soldLeft -= filled;
            Row(UnloadSteps.Length - 1 - i, price, quantity, filled > 0m ? filled : 0m, largest, amber, TabletKitStyle.Up, Shares(quantity) + "  " + Pct(UnloadSteps[i]));
        }
        LineAt(nowLine, nowLabel, RowTop(UnloadSteps.Length) + .02f, Ink, L("지금 ", "now ", "現在 ") + stock.currentPrice.ToString("N0"), false);
        if (total < 1m) { Sentence = L("넘길 주식이 없습니다. 먼저 이 종목을 사 두거나 불을 붙여 보세요.", "You have no shares here to hand over.", "渡す株がありません。"); return; }
        Possible = true;
        Sentence = run != null
            ? L($"{Shares(total)} 중 {Shares(sold)}을 팔았습니다. 값이 계단에 닿을 때마다 조금씩 팔립니다.", $"Sold {Shares(sold)} of {Shares(total)}. A little sells each time the price reaches a step.", $"{Shares(total)}のうち{Shares(sold)}を売りました。値が段に届くたびに少しずつ売れます。")
            : L($"지금 값 위 네 계단에 {Shares(slice)}씩 팔 주문을 걸어 둡니다. 값이 오르면 따라 사는 사람들에게 조금씩 넘어갑니다. 90초 동안 걸어 둡니다.",
                $"Places {Shares(slice)} on each of four steps above the price. As it climbs, your shares pass to buyers chasing it. Stays up for 90 seconds.",
                $"現在値の上の4段に{Shares(slice)}ずつ売り注文を置きます。値が上がると追いかけて買う人に少しずつ渡ります。90秒間置きます。");
    }

    private void Wall(Stock stock, Trader account, PlayerTactics.Run run, float fraction)
    {
        var down = TabletKitStyle.Down;
        if (!stock.orderBook.TryBest(out decimal bid, out _) && run == null) { Sentence = L("지금 사겠다는 주문이 없어 벽을 세울 자리를 잡지 못했습니다.", "No buy orders to place the wall among.", "買い注文がなく、壁の位置が決まりません。"); return; }
        decimal wallPrice = run != null ? run.Price : PriceTickPolicy.FloorToTick(bid * (1m - ManipulationTools.WallDepth));
        decimal wallShares = run != null ? (run.Price > 0m ? PlayerTactics.Instance.Shown(run) / run.Price : 0m) : wallPrice > 0m ? decimal.Floor(Free(account) * (decimal)Mathf.Clamp01(fraction) / wallPrice) : 0m;
        stock.orderBook.GetTopBids(book, DepthLevels);
        // 벽 위의 매수 호가 전부(묶어서) + 내 벽 + 그 아래 한 줄. 위가 비싼 값. 벽은 따로 한 줄로 둔다.
        levels.Clear(); int wallAt = -1;
        foreach (var level in book)
        {
            if (wallAt < 0 && level.price <= wallPrice)
            {
                // 벽이 이미 걸려 있으면 그 값의 잔량에 내 벽이 들어 있다. 미리 보기면 그 값에 내 벽을 더해 그린다.
                decimal sameVolume = level.price == wallPrice ? (run != null ? level.volume : level.volume + wallShares) : wallShares;
                levels.Add(new Level { price = wallPrice, volume = sameVolume, taken = wallShares, mine = true }); wallAt = levels.Count - 1;
                if (level.price == wallPrice) { continue; }
            }
            if (wallAt >= 0 && levels.Count > wallAt + 1) { break; }
            levels.Add(new Level { price = level.price, volume = level.volume });
        }
        if (wallAt < 0) { levels.Add(new Level { price = wallPrice, volume = wallShares, taken = wallShares, mine = true }); wallAt = levels.Count - 1; }
        var rows = Fit(levels, Rows - 1, wallAt);
        decimal largest = Largest(rows);
        LineAt(nowLine, nowLabel, .045f, Ink, L("지금 ", "now ", "現在 ") + stock.currentPrice.ToString("N0"), false);
        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            Row(i + 1, item, largest, item.mine ? Amber : down, down, item.mine ? Shares(item.taken) + L("  내 벽", "  my wall", "  自分の壁") : null);
        }
        if (wallShares < 1m) { Sentence = L("고른 비중으로는 벽을 세울 수 없습니다. 비중을 올려 보세요.", "Not enough cash at this size for a wall.", "この割合では壁を置けません。"); return; }
        Possible = true;
        Sentence = L($"지금 값보다 2.5% 아래 {wallPrice:N0}돈에 {Shares(wallShares)} 사겠다는 주문을 크게 걸어, 받쳐 주는 큰돈이 있어 보이게 합니다. 값이 그 근처로 오면 저절로 거둡니다(실제로 사지 않으려고).",
            $"Shows a big buy order for {Shares(wallShares)} at {wallPrice:N0}, 2.5% below, so it looks like big money supports the price. It pulls itself when the price comes near, so it never actually buys.",
            $"現在値の2.5%下の{wallPrice:N0}に{Shares(wallShares)}の大きな買い注文を置き、支える大口がいるように見せます。値が近づくと自動で取り消します(実際には買わないため)。");
    }

    private void Close(Stock stock, Trader account, PlayerTactics.Run run, float fraction)
    {
        var day = MarketDayController.Instance;
        bool trading = day != null && day.Phase == MarketPhase.Trading; float length = day == null ? 900f : Mathf.Max(1f, day.tradingSeconds), remaining = trading ? day.RemainingSeconds : length, elapsed = length - remaining;
        float windowStart = Mathf.Clamp01(1f - PlayerTactics.CloseSeconds / length), now = Mathf.Clamp01(elapsed / length);
        At(track, .02f, .30f, .98f, .36f); At(window, .02f + .96f * windowStart, .26f, .98f, .40f);
        trackFill.color = new Color(1f, 1f, 1f, .25f); At(trackFill, .02f, .30f, .02f + .96f * now, .36f);
        At(nowPin, .02f + .96f * now - .002f, .22f, .02f + .96f * now + .002f, .44f);
        Say(trackStart, L("장 시작", "open", "寄り付き")); trackStart.color = Muted; At(trackStart, .02f, .08f, .4f, .2f);
        Say(trackEnd, L("마감 직전 40초 · 여기서 삽니다", "last 40 s · buys here", "引け前40秒 · ここで買う")); trackEnd.color = Amber; At(trackEnd, .4f, .08f, .98f, .2f);
        decimal budget = run != null ? run.Budget : decimal.Floor(Free(account) * (decimal)Mathf.Clamp01(fraction)), spent = run != null ? run.Spent : 0m;
        At(budgetTrack, .02f, .66f, .98f, .74f);
        budgetFill.color = Amber; At(budgetFill, .02f, .66f, .02f + .96f * (budget > 0m ? Mathf.Clamp01((float)(spent / budget)) : 0f), .74f);
        Say(budgetLabel, L($"예산 {Money(budget)} · 쓴 돈 {Money(spent)}", $"budget {Money(budget)} · spent {Money(spent)}", $"予算 {Money(budget)} · 使用 {Money(spent)}")); budgetLabel.color = Ink; At(budgetLabel, .02f, .50f, .98f, .62f);
        if (budget < stock.currentPrice) { Sentence = L("고른 비중으로는 한 주도 살 수 없습니다.", "This size can't buy even one share.", "この割合では1株も買えません。"); return; }
        Possible = true;
        Sentence = L("장이 끝나기 40초 전부터 이 예산으로 사들여 오늘 마지막 값(종가)을 높게 남깁니다. 다음 날 사람들은 이 값을 기준으로 봅니다.",
            "Buys with this budget through the last 40 seconds so today ends at a higher price. Tomorrow people measure from that closing price.",
            "引けの40秒前からこの予算で買い、今日の最後の値(終値)を高く残します。翌日みんながその値を基準に見ます。");
    }

    private void Wash(Stock stock, Trader account, PlayerTactics.Run run)
    {
        float left = run != null ? run.Left : PlayerTactics.WashSeconds, ratio = Mathf.Clamp01(left / PlayerTactics.WashSeconds);
        Say(trackStart, L("남은 시간", "time left", "残り時間")); trackStart.color = Muted; At(trackStart, .02f, .08f, .5f, .2f);
        Say(trackEnd, left.ToString("0") + L("초", " s", "秒")); trackEnd.color = Ink; At(trackEnd, .5f, .08f, .98f, .2f);
        At(track, .02f, .26f, .98f, .32f); trackFill.color = Amber; At(trackFill, .02f, .26f, .02f + .96f * ratio, .32f);
        decimal painted = run != null ? run.Painted : 0m;
        Say(caption, L("지금까지 부풀린 거래대금", "turnover painted so far", "これまで水増しした売買代金")); At(caption, .02f, .46f, .98f, .56f);
        Say(bigValue, Money(painted)); bigValue.color = run != null ? Amber : Muted; At(bigValue, .02f, .58f, .98f, .80f);
        decimal held = account.GetHolding(stock);
        if (held < 1m) { Sentence = L("이 종목을 조금이라도 가지고 있어야 사고팔기를 되풀이할 수 있습니다.", "You need a few shares of this stock to trade back and forth.", "この銘柄を少しでも持っている必要があります。"); return; }
        Possible = true;
        Sentence = L("가진 주식으로 사고팔기를 45초 동안 되풀이해 거래가 활발해 보이게 합니다. 주식 수와 돈은 거의 그대로이고, 거래가 많아 보이면 관심(열기)이 오릅니다.",
            "Trades your own shares back and forth for 45 seconds so the stock looks busy. Your shares and cash barely change; busy stocks draw attention (heat).",
            "持ち株で45秒間売り買いを繰り返し、取引が活発に見えるようにします。株数と現金はほぼそのまま。取引が多く見えると注目(熱気)が上がります。");
    }
}
