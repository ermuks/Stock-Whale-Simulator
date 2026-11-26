using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 두 번째 태블릿 "자동 매매"의 매집 패널.
// 왼쪽에서 가격대와 쓸 현금을 정해 시작하고, 오른쪽에서 얼마나 모았는지 본다.
// 스프레드 화면 위에 겹쳐 놓고 위쪽 탭으로 오간다. 주문은 TradingOperationController가 낸다.
public sealed class AccumulationUI : MonoBehaviour
{
    private const float W = 1600f, H = 880f;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(146, 161, 172, 255), Line = new Color32(44, 60, 69, 255),
        Ground = new Color32(13, 23, 29, 255), Field = new Color32(25, 36, 43, 255);
    /// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly Dictionary<string, (string en, string ja)> Words = new()
    {
        ["스프레드"] = ("Spread", "スプレッド"), ["매집"] = ("Accumulate", "買い集め"), ["자동 매집"] = ("Auto accumulate", "自動買い集め"),
        ["매집 가격대"] = ("Price band", "買い集め価格帯"), ["돈"] = ("DON", "ドン"), ["주"] = ("sh", "株"),
        ["현재가 −3% ~ +1%"] = ("−3% to +1%", "現在値−3%〜+1%"), ["현재가 −5% ~ +1%"] = ("−5% to +1%", "現在値−5%〜+1%"), ["현재가 −10% ~ −2%"] = ("−10% to −2%", "現在値−10%〜−2%"),
        ["사용할 현금"] = ("Cash to use", "使う現金"), ["매집 예산"] = ("Budget", "買い集め予算"),
        ["30초 안에 4% 넘게 떨어지면 매수 대기를 바로 거두고, 평균 단가보다 쌀 때만 조금씩 삽니다."] =
            ("If the price falls more than 4% within 30 seconds, resting bids are pulled at once and only small buys below your average cost continue.",
             "30秒以内に4%超下落すると買い待機を即座に取り消し、平均単価より安い時だけ少しずつ買います。"),
        ["매집 시작"] = ("Start", "買い集め開始"), ["매집 중지"] = ("Stop", "買い集め停止"),
        ["매집 현황"] = ("Position built", "買い集め状況"), ["매집 수량"] = ("Shares bought", "買い集め数量"), ["평균 단가"] = ("Average cost", "平均単価"),
        ["매집 금액"] = ("Cash spent", "買い集め金額"), ["평가 금액"] = ("Market value", "評価額"), ["평가 손익"] = ("Unrealized P&L", "評価損益"),
        ["남은 예산"] = ("Budget left", "残り予算"), ["매수 대기"] = ("Resting bid", "買い待機"), ["예산 사용 {0}%"] = ("{0}% of budget used", "予算の{0}%を使用"),
        ["최근 체결"] = ("Recent fills", "直近の約定"), ["실행 경과 · {0}"] = ("Running · {0}", "実行経過 · {0}"),
        ["가격대 안 · 매집 중"] = ("In band · accumulating", "価格帯内 · 買い集め中"), ["가격대 위 · 대기"] = ("Above band · waiting", "価格帯より上 · 待機"),
        ["가격대 아래 · 대기"] = ("Below band · waiting", "価格帯より下 · 待機"), ["폭락 감지 · 소량 물타기"] = ("Crash · small averaging down", "急落検知 · 少量ナンピン"),
        ["예산 소진"] = ("Budget used up", "予算を使い切りました"), ["장 대기"] = ("Waiting for the open", "寄り付き待ち"), ["매집 중지됨"] = ("Stopped", "停止しました"),
        ["가격대를 확인하세요"] = ("Check the price band", "価格帯を確認してください"), ["현금 부족"] = ("Not enough cash", "現金不足"),
        ["스프레드 실행 중"] = ("Spread is running", "スプレッド実行中"), ["거래 불가"] = ("Cannot trade now", "取引不可"), ["계좌 잠김"] = ("Account locked", "口座ロック中"),
        ["매매 실행 중"] = ("Another job is running", "別の売買を実行中"), ["거래 종료"] = ("Trading ended", "取引終了"),
        ["자동 매매 자리가 모두 사용 중"] = ("All automation slots are in use", "自動売買の枠がすべて使用中"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    /// <summary>
    /// 지금 보는 종목을 맡은 자동 매매 장치. 종목마다 장치가 다르므로 붙들어 두지 않고 쓸 때마다 다시 읽는다.
    /// 예전에는 장치가 하나라 매집이 도는 동안 종목을 못 바꾸게 잠갔다. 이제 종목을 자유롭게 넘기며 여러 종목에서 돌릴 수 있다.
    /// </summary>
    private static TradingOperationController Operation => TradingOperationController.Instance;
    private OperationRunList runList;
    private TMP_FontAsset font;
    private float scale, next;
    private RectTransform panel;
    private Button spreadTab, accumulateTab, start, stop;
    private readonly Button[] presets = new Button[3];
    // 쓸 현금의 비율은 슬라이더로 1%씩 고른다(예전에는 25·50·75·100% 단추 네 개였다).
    private Slider cashSlider;
    private TextMeshProUGUI cashShare;
    private static readonly string[] PresetNames = { "현재가 −3% ~ +1%", "현재가 −5% ~ +1%", "현재가 −10% ~ −2%" };
    private static readonly (decimal low, decimal high)[] PresetBands = { (.97m, 1.01m), (.95m, 1.01m), (.90m, .98m) };
    private TMP_InputField lowInput, highInput;
    private TextMeshProUGUI title, state, stockName, price, budget, hint, statusHeading, shares, average, spent, value, profit, left, resting, used, tape, elapsed;
    private readonly List<(TextMeshProUGUI label, string korean)> fixedLabels = new();
    private RectTransform usedFill;
    private float fraction = .5f;
    private Stock shown;
    private bool showing;
    private int locale = -1;
    private string notice = "";
    private float noticeUntil;

    public static AccumulationUI Install(SpreadTradingUI spread, TradingOperationController controller, float designScale)
    {
        // controller는 예전 부르는 자리와 맞추려고 남겨 둔다. 화면은 종목마다 맞는 장치를 그때그때 읽는다.
        if (spread == null || controller == null) { return null; }
        var existing = spread.GetComponent<AccumulationUI>();
        if (existing != null) { return existing; }
        var view = spread.gameObject.AddComponent<AccumulationUI>();
        view.scale = designScale <= 0f ? 1f : designScale;
        view.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        view.Build();
        return view;
    }

    private void Build()
    {
        var root = transform;
        panel = new GameObject("Area :: Accumulation", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.gameObject.layer = gameObject.layer; panel.SetParent(root, false);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
        var ground = panel.GetComponent<Image>(); ground.color = Ground; ground.raycastTarget = true;

        title = Label("Title", "자동 매집", 32, 8, 280, 56, 40, true);
        state = Text("State", 960, 8, 608, 56, 28); state.color = TabletKitStyle.Accent; state.alignment = TextAlignmentOptions.MidlineRight;
        Action("Previous Stock", "‹", 32, 88, 48, 56, () => Select(-1), false, false);
        stockName = Text("Stock", 88, 88, 720, 64, 38);
        Action("Next Stock", "›", 832, 88, 48, 56, () => Select(1), false, false);
        price = Number("Current Price", 32, 168, 800, 56, 32); Label("Current Price Unit", "돈", 848, 168, 60, 56, 25).color = Muted;

        Label("Band Label", "매집 가격대", 32, 244, 480, 44, 29);
        lowInput = Input("Band Low", 32, 296, 360, 56); Label("Low Unit", "돈", 400, 296, 60, 56, 25).color = Muted;
        Text("Band Dash", 440, 296, 40, 56, 30).text = "~";
        highInput = Input("Band High", 480, 296, 360, 56); Label("High Unit", "돈", 848, 296, 60, 56, 25).color = Muted;
        for (int i = 0; i < presets.Length; i++)
        {
            int index = i;
            presets[i] = Action("Band Preset " + i, PresetNames[i], 32 + i * 286, 368, 276, 48, () => Preset(index), false, true, 23);
        }
        Label("Cash Label", "사용할 현금", 32, 440, 480, 44, 29);
        cashShare = Number("Cash Share", 600, 440, 280, 44, 29); cashShare.color = TabletKitStyle.Accent;
        cashSlider = ShareSlider("Cash Share", 32, 488, 848, 56, 1, 100, Mathf.RoundToInt(fraction * 100f), value => { fraction = value / 100f; next = 0f; });
        budget = Metric("매집 예산", 32, 564, 848, "돈");
        hint = Label("Crash Hint", "30초 안에 4% 넘게 떨어지면 매수 대기를 바로 거두고, 평균 단가보다 쌀 때만 조금씩 삽니다.", 32, 624, 848, 96, 23);
        hint.color = Muted; hint.textWrappingMode = TextWrappingModes.Normal; hint.alignment = TextAlignmentOptions.TopLeft;
        elapsed = Text("Elapsed", 32, 736, 848, 40, 25); elapsed.color = Muted;
        start = Action("Start Accumulation", "매집 시작", 32, 792, 408, 56, StartAccumulation, true, true);
        stop = Action("Stop Accumulation", "매집 중지", 472, 792, 408, 56, StopAccumulation, false, true);

        // 오른쪽: 지금 보는 종목에서 모은 것(수량·평단·쓴 돈·지금 값·손익). 그 아래에 모든 종목의 진행 중 목록을 둔다.
        var divider = Graphic("Accumulation Divider", panel, TabletKitStyle.Line);
        TradingOrderView.Place(divider.rectTransform, panel, 920, 88, 1, 752, W, H);
        statusHeading = Label("Status Title", "매집 현황", 960, 88, 608, 56, 34);
        shares = Metric("매집 수량", 960, 152, 608, "주");
        average = Metric("평균 단가", 960, 198, 608, "돈");
        spent = Metric("매집 금액", 960, 244, 608, "돈");
        value = Metric("평가 금액", 960, 290, 608, "돈");
        profit = Metric("평가 손익", 960, 336, 608, "돈");
        left = Metric("남은 예산", 960, 382, 608, "돈");
        resting = Metric("매수 대기", 960, 428, 608, "");
        used = Text("Budget Used", 960, 478, 608, 32, 24); used.color = Muted;
        var track = Graphic("Budget Track", panel, Line); TradingOrderView.Place(track.rectTransform, panel, 960, 514, 608, 10, W, H);
        usedFill = Graphic("Budget Fill", track.transform, TabletKitStyle.Accent).rectTransform;
        usedFill.anchorMin = Vector2.zero; usedFill.anchorMax = new Vector2(0, 1); usedFill.offsetMin = usedFill.offsetMax = Vector2.zero;
        Label("Recent Fills", "최근 체결", 960, 532, 608, 44, 34);
        tape = Text("Fill Tape", 960, 580, 608, 72, 25); tape.textWrappingMode = TextWrappingModes.Normal; tape.alignment = TextAlignmentOptions.TopLeft;
        runList = new OperationRunList(panel, font, scale, 960, 664, 608, Jump);

        // 탭은 두 화면 위에 함께 떠 있다.
        spreadTab = Tab("Tab Spread", "스프레드", 330, () => Show(false));
        accumulateTab = Tab("Tab Accumulate", "매집", 520, () => Show(true));
        Show(false);
    }

    private void Show(bool accumulate)
    {
        showing = accumulate; panel.gameObject.SetActive(accumulate); ManipulationUI.Hide();
        TabletControlStyle.Emphasize(spreadTab, !accumulate); TabletControlStyle.Emphasize(accumulateTab, accumulate);
        next = 0f;
    }
    private Button Tab(string name, string caption, float x, UnityEngine.Events.UnityAction action)
    {
        var button = Action(name, caption, x, 12, 180, 48, action, false, true, 25);
        button.transform.SetParent(transform, false);
        TradingOrderView.Place((RectTransform)button.transform, transform, x, 12, 180, 48, W, H);
        return button;
    }

    /// <summary>스프레드 화면의 진행 중 목록에서 매집 줄을 눌렀을 때 이 화면으로 넘어온다(반대도 같다).</summary>
    public void ShowMode(bool accumulate) { Show(accumulate); }
    /// <summary>진행 중 목록의 줄을 누르면 그 종목으로 가고, 그 방식의 화면을 연다.</summary>
    private void Jump(TradingOperationController run)
    {
        var manager = StockManager.Instance;
        if (run == null || run.Target == null || manager == null) { return; }
        int index = manager.stocks.IndexOf(run.Target);
        if (index >= 0) { manager.SelectStock(index); }
        Show(run.CurrentMode == TradingOperationController.Mode.Accumulate);
    }
    // 무언가 돌고 있어도 종목은 언제나 넘길 수 있다. 다른 종목에서 또 시작할 수 있어야 한다.
    private void Select(int delta)
    {
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks.Count == 0) { return; }
        int index = manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index + delta + manager.stocks.Count) % manager.stocks.Count); next = 0f;
    }
    private void Preset(int index)
    {
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        var operation = Operation;
        if (stock == null || operation == null || operation.Active) { return; }
        SetBand(stock.currentPrice * PresetBands[index].low, stock.currentPrice * PresetBands[index].high);
    }
    private void SetBand(decimal low, decimal high)
    {
        low = System.Math.Max(1m, PriceTickPolicy.FloorToTick(low)); high = System.Math.Max(low, PriceTickPolicy.FloorToTick(high));
        lowInput.SetTextWithoutNotify(low.ToString("0", CultureInfo.InvariantCulture)); highInput.SetTextWithoutNotify(high.ToString("0", CultureInfo.InvariantCulture));
    }
    private static bool Read(TMP_InputField field, out decimal amount)
        => decimal.TryParse((field.text ?? "").Replace(",", "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount >= 1m;

    private void StartAccumulation()
    {
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        var operation = Operation;
        if (operation == null) { return; }
        if (!Read(lowInput, out decimal low) || !Read(highInput, out decimal high) || high < low)
        { notice = "가격대를 확인하세요"; noticeUntil = Time.unscaledTime + 4f; next = 0f; return; }
        if (!operation.StartAccumulate(stock, low, high, fraction))
        { notice = operation.Feedback; noticeUntil = Time.unscaledTime + 4f; }
        next = 0f;
    }
    // 지금 보는 종목의 매집만 멈춘다. 다른 종목의 것은 진행 중 목록의 ×로 멈춘다.
    private void StopAccumulation()
    {
        var operation = Operation;
        if (operation != null) { operation.CancelAccumulate(); }
        next = 0f;
    }

    private void OnEnable() { next = 0f; }
    private void Update()
    {
        var operation = Operation;
        if (operation == null || panel == null || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f;
        if (locale != GameLocale.Revision)
        {
            locale = GameLocale.Revision;
            foreach (var entry in fixedLabels) { Lit(entry.label, T(entry.korean)); }
        }
        if (!showing) { return; }
        // 화면은 언제나 지금 고른 종목을 따라간다. here: 이 장치가 그 종목의 것인가(자리가 모두 찼으면 다른 종목의 장치가 온다).
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        bool here = stock != null && operation.Target == stock, running = here && operation.AccumulateRunning, busy = operation.Active;
        if (stock != shown && !running && stock != null && !lowInput.isFocused && !highInput.isFocused)
        { shown = stock; SetBand(stock.currentPrice * .95m, stock.currentPrice * 1.01m); }
        // 돌아가는 동안에는 실제로 쓰고 있는 가격대를 보여 준다.
        if (running) { shown = stock; SetBand(operation.AccumulateLow, operation.AccumulateHigh); }
        Lit(stockName, stock == null ? "—" : GameLocale.Text(stock.stockName));
        Lit(price, stock == null ? "—" : TradingOrderView.Number(stock.currentPrice));
        string status = Time.unscaledTime < noticeUntil ? notice : running ? operation.Feedback : here && operation.SpreadRunning ? "스프레드 실행 중" :
            busy && !here ? "자동 매매 자리가 모두 사용 중" :
            here && operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed && operation.Bought > 0m ? "매집 중지됨" : "";
        Lit(state, T(status));
        state.color = operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Crash && running ? MarketColors.Down : TabletKitStyle.Accent;

        decimal planned = running ? operation.Budget : decimal.Floor(operation.SpreadAvailableCash * (decimal)fraction);
        Lit(budget, TradingOrderView.Number(planned));
        // 이 종목에서 지금 돌고 있는(또는 방금 끝난) 매집의 결과. 스프레드를 돌리는 중에는 그쪽 숫자가 섞이지 않게 비운다.
        bool mine = running || here && operation.CurrentMode == TradingOperationController.Mode.Review && operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed;
        decimal bought = mine ? operation.Bought : 0m, cost = mine ? operation.Spent : 0m;
        decimal worth = stock == null ? 0m : bought * stock.currentPrice, gain = worth - cost;
        Lit(shares, TradingOrderView.Number(bought));
        Lit(average, bought > 0m ? TradingOrderView.Number(decimal.Round(cost / bought)) : "—");
        Lit(spent, TradingOrderView.Number(decimal.Round(cost)));
        Lit(value, TradingOrderView.Number(decimal.Round(worth)));
        Lit(profit, bought > 0m ? InvestmentDashboard.Signed(decimal.Round(gain)) + "  (" + (cost > 0m ? (gain / cost * 100m).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) : "0.0") + "%)" : "—");
        profit.color = bought <= 0m || gain == 0m ? Ink : gain > 0m ? MarketColors.Up : MarketColors.Down;
        Lit(left, running ? TradingOrderView.Number(decimal.Round(operation.AccumulateBudgetLeft)) : "—");
        Lit(resting, running && operation.AccumulateRestingQuantity > 0m ? TradingOrderView.Number(operation.AccumulateRestingPrice) + " × " + TradingOrderView.Number(operation.AccumulateRestingQuantity) : "—");
        float ratio = running && operation.Budget > 0m ? Mathf.Clamp01((float)(operation.Spent / operation.Budget)) : 0f;
        usedFill.anchorMax = new Vector2(ratio, 1f);
        Lit(used, string.Format(T("예산 사용 {0}%"), Mathf.RoundToInt(ratio * 100f)));
        Lit(elapsed, running ? string.Format(T("실행 경과 · {0}"), DeviceMarketJournal.Duration(DeviceMarketJournal.GameNow - operation.AccumulateStartedGameMinute)) : "");

        var buffer = new System.Text.StringBuilder(); int lines = 0;
        if (mine)
        {
            foreach (var fill in operation.AccumulateFills)
            {
                if (++lines > 2) { break; }
                buffer.Append(GameLocale.Text(fill.Time ?? "")).Append("   ").Append(TradingOrderView.Number(fill.Price)).Append(" × ").Append(TradingOrderView.Number(fill.Quantity)).AppendLine();
            }
        }
        Lit(tape, buffer.Length > 0 ? buffer.ToString().TrimEnd() : "—");

        runList.Refresh();

        // 이 종목에서 아무것도 돌지 않을 때만 새로 시작할 수 있다. 종목 넘기기는 잠그지 않는다.
        bool idle = !busy;
        lowInput.interactable = highInput.interactable = idle;
        foreach (var button in presets) { button.interactable = idle && stock != null; }
        cashSlider.interactable = idle; Lit(cashShare, Mathf.RoundToInt(fraction * 100f) + "%");
        start.interactable = idle && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
        stop.interactable = running;
        TabletControlStyle.Emphasize(start, !running); if (running) { TabletControlStyle.Danger(stop); } else { TabletControlStyle.Emphasize(stop, false); }
    }

    private static void Lit(TextMeshProUGUI label, string text)
    {
        text ??= "";
        if (!GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); }
    }
    /// <summary>비율을 고르는 슬라이더(스프레드 화면의 범위 슬라이더와 같은 모양). 값은 정수(%)다.</summary>
    private Slider ShareSlider(string name, float x, float y, float w, float h, int min, int max, int start, System.Action<int> changed)
    {
        var root = new GameObject("Area :: " + name + " Slider", typeof(RectTransform), typeof(Image), typeof(Slider)) { layer = gameObject.layer };
        TradingOrderView.Place((RectTransform)root.transform, panel, x, y, w, h, W, H);
        root.GetComponent<Image>().color = Color.clear;
        RectTransform Area(string part, float bottom, float top)
        {
            var rect = new GameObject("Area :: " + name + " " + part, typeof(RectTransform)) { layer = gameObject.layer }.GetComponent<RectTransform>();
            rect.SetParent(root.transform, false); rect.anchorMin = new Vector2(0f, bottom); rect.anchorMax = new Vector2(1f, top); rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        var track = Graphic(name + " Track", root.transform, Line).rectTransform; track.anchorMin = new Vector2(0f, .42f); track.anchorMax = new Vector2(1f, .58f); track.offsetMin = track.offsetMax = Vector2.zero;
        var fill = Graphic(name + " Fill", Area("Fill", .42f, .58f), TabletKitStyle.Accent).rectTransform; fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
        var handle = Graphic(name + " Handle", Area("Handle", .2f, .8f), Ink); handle.rectTransform.sizeDelta = new Vector2(18f * scale, 0f);
        var slider = root.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true; slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.SetValueWithoutNotify(Mathf.Clamp(start, min, max));
        slider.onValueChanged.AddListener(value => changed(Mathf.RoundToInt(value)));
        return slider;
    }
    private Image Graphic(string name, Transform parent, Color color)
    {
        var item = new GameObject("Image :: " + name, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private TextMeshProUGUI Text(string name, float x, float y, float w, float h, float size)
    {
        var item = new GameObject("Text :: " + name, typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = gameObject.layer };
        var label = item.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform, panel, x, y, w, h, W, H);
        label.font = font; label.fontSize = size * scale; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.color = Ink; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    /// <summary>바뀌지 않는 글자. 언어가 바뀌면 다시 적는다.</summary>
    private TextMeshProUGUI Label(string name, string korean, float x, float y, float w, float h, float size, bool heading = false)
    {
        var label = Text(name, x, y, w, h, size);
        fixedLabels.Add((label, korean)); Lit(label, T(korean));
        return label;
    }
    private TextMeshProUGUI Number(string name, float x, float y, float w, float h, float size)
    {
        var label = Text(name, x, y, w, h, size); TradingOrderView.Numeric(label); label.alignment = TextAlignmentOptions.MidlineRight; return label;
    }
    private TextMeshProUGUI Metric(string caption, float x, float y, float w, string unit)
    {
        Label(caption + " Label", caption, x, y, 288, 44, 27).color = Muted;
        float unitWidth = unit.Length == 0 ? 0f : 60f;
        var number = Number(caption + " Value", x + 296, y, w - 296 - unitWidth - 8, 44, 30);
        if (unit.Length > 0) { Label(caption + " Unit", unit, x + w - unitWidth, y, unitWidth, 44, 25).color = Muted; }
        return number;
    }
    private Button Action(string name, string caption, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, bool primary, bool translated, float size = 28)
    {
        var item = new GameObject("Button :: " + name, typeof(RectTransform), typeof(Image), typeof(Button)) { layer = gameObject.layer };
        TradingOrderView.Place((RectTransform)item.transform, panel, x, y, w, h, W, H);
        var label = translated ? Label(name, caption, x, y, w, h, size) : Text(name, x, y, w, h, size);
        if (!translated) { Lit(label, caption); }
        label.transform.SetParent(item.transform, false);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        var button = item.GetComponent<Button>(); button.targetGraphic = item.GetComponent<Image>(); TabletControlStyle.Button(button, primary);
        label.font = font; label.fontSize = size * scale; label.fontStyle = FontStyles.Normal;
        button.onClick.AddListener(action); return button;
    }
    private TMP_InputField Input(string name, float x, float y, float w, float h)
    {
        var item = new GameObject("Input :: " + name, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        var rect = (RectTransform)item.transform; TradingOrderView.Place(rect, panel, x, y, w, h, W, H);
        var image = item.GetComponent<Image>(); image.color = Field;
        var field = item.AddComponent<TMP_InputField>(); field.targetGraphic = image;
        var text = Text(name + " Text", 0, 0, 10, 10, 32); text.transform.SetParent(rect, false);
        text.rectTransform.anchorMin = new Vector2(.04f, .08f); text.rectTransform.anchorMax = new Vector2(.96f, .92f); text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.MidlineRight; text.raycastTarget = true; TradingOrderView.Numeric(text);
        field.textViewport = rect; field.textComponent = text; field.characterLimit = 12; field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.lineType = TMP_InputField.LineType.SingleLine; TabletControlStyle.Input(field);
        return field;
    }
}

/// <summary>
/// 자동 매매(스프레드·매집)의 "진행 중" 목록. 스프레드 화면과 매집 화면이 같은 것을 하나씩 둔다.
/// 모든 종목에서 돌고 있는 것을 최근에 시작한 순서로 세 줄까지 보여 준다: 종목 · 방식 · 번 돈.
/// 줄을 누르면 그 종목으로 가고(화면이 정한 동작), ×는 그것만 멈춘다. 아무것도 돌지 않으면 제목까지 숨는다.
/// 숫자는 TradingOperationController가 실제 체결로 센 값이다. 여기서 만들지 않는다.
/// </summary>
internal sealed class OperationRunList
{
    private const int Rows = 3;
    private const float W = 1600f, H = 880f;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(146, 161, 172, 255);
    private static readonly System.Comparison<TradingOperationController> Newest = (a, b) => b.StartedAt.CompareTo(a.StartedAt);
    private readonly TextMeshProUGUI heading, more;
    private readonly Button[] rows = new Button[Rows], stops = new Button[Rows];
    private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[Rows], profits = new TextMeshProUGUI[Rows];
    private readonly TradingOperationController[] shown = new TradingOperationController[Rows];
    private readonly List<TradingOperationController> buffer = new(TradingOperationController.MaxRuns);
    private readonly System.Action<TradingOperationController> jump;

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>parent 안의 (x, y)에서 너비 w로 그린다(1600×880 기준). 높이는 제목 32 + 줄 44 × 3이다.</summary>
    public OperationRunList(Transform parent, TMP_FontAsset font, float scale, float x, float y, float w, System.Action<TradingOperationController> jump)
    {
        this.jump = jump;
        heading = Label("Runs Title", parent, font, 22 * scale, x, y, w - 120, 32); heading.color = Muted;
        more = Label("Runs More", parent, font, 22 * scale, x + w - 120, y, 120, 32); more.color = Muted; more.alignment = TextAlignmentOptions.MidlineRight;
        for (int i = 0; i < Rows; i++)
        {
            int index = i; float top = y + 36 + i * 44;
            rows[i] = Press("Run " + i, parent, font, 24 * scale, x, top, w - 60, 40, () => Jump(index), out labels[i]);
            labels[i].rectTransform.anchorMin = new Vector2(.03f, 0f); labels[i].rectTransform.anchorMax = new Vector2(.60f, 1f); labels[i].alignment = TextAlignmentOptions.MidlineLeft;
            // 번 돈은 줄 오른쪽에 숫자 글꼴로 적는다. 단추의 자식으로 두면 단추 꾸밈이 글꼴을 바꾸므로 따로 얹는다.
            profits[i] = Label("Run Profit " + i, parent, font, 24 * scale, x + (w - 60) * .60f, top, (w - 60) * .37f, 40);
            TradingOrderView.Numeric(profits[i]); profits[i].alignment = TextAlignmentOptions.MidlineRight;
            stops[i] = Press("Run Stop " + i, parent, font, 24 * scale, x + w - 52, top, 52, 40, () => Stop(index), out var cross);
            Lit(cross, "×");
        }
        Show(0);
    }

    private void Jump(int row) { if (shown[row] != null && jump != null) { jump(shown[row]); } }
    private void Stop(int row)
    {
        // 목록을 그린 뒤 그 장치가 다른 일을 맡았을 수 있다. 아직 돌고 있을 때만 멈춘다.
        if (shown[row] != null && shown[row].Active) { shown[row].Stop(); }
        Refresh();
    }
    private void Show(int count)
    {
        if (heading.gameObject.activeSelf != count > 0) { heading.gameObject.SetActive(count > 0); more.gameObject.SetActive(count > 0); }
        for (int i = 0; i < Rows; i++)
        {
            bool on = i < count;
            if (rows[i].gameObject.activeSelf == on) { continue; }
            rows[i].gameObject.SetActive(on); stops[i].gameObject.SetActive(on); profits[i].gameObject.SetActive(on);
        }
    }

    /// <summary>지금 돌고 있는 것들로 다시 그린다. 화면이 갱신될 때마다 부른다.</summary>
    public void Refresh()
    {
        buffer.Clear();
        foreach (var run in TradingOperationController.ActiveRuns) { buffer.Add(run); }
        buffer.Sort(Newest);
        int total = buffer.Count, visible = Mathf.Min(total, Rows);
        Show(visible);
        for (int i = 0; i < Rows; i++) { shown[i] = i < visible ? buffer[i] : null; }
        if (visible == 0) { return; }
        Lit(heading, Word("진행 중", "Active now", "進行中"));
        Lit(more, total > Rows ? "+" + (total - Rows) : "");
        var current = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        for (int i = 0; i < visible; i++)
        {
            var run = buffer[i];
            string mode = run.CurrentMode == TradingOperationController.Mode.Spread ? Word("스프레드", "Spread", "スプレッド")
                : run.CurrentMode == TradingOperationController.Mode.Accumulate ? Word("매집", "Accumulate", "買い集め") : GameLocale.Text(run.ModeName);
            Lit(labels[i], GameLocale.Text(run.Target.stockName) + " · " + mode);
            // 지금 보고 있는 종목의 줄만 밝게.
            labels[i].color = run.Target == current ? Ink : Muted;
            decimal gain = decimal.Round(run.RunProfit);
            Lit(profits[i], InvestmentDashboard.Signed(gain));
            profits[i].color = gain > 0m ? MarketColors.Up : gain < 0m ? MarketColors.Down : Muted;
        }
    }

    private static void Lit(TextMeshProUGUI label, string text)
    {
        text ??= "";
        if (!GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); }
    }
    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, float x, float y, float w, float h)
    {
        var item = new GameObject("Text :: " + name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var label = item.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform, parent, x, y, w, h, W, H);
        if (font != null) { label.font = font; }
        label.fontSize = size; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.color = Ink; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private static Button Press(string name, Transform parent, TMP_FontAsset font, float size, float x, float y, float w, float h, UnityEngine.Events.UnityAction action, out TextMeshProUGUI label)
    {
        var item = new GameObject("Button :: " + name, typeof(RectTransform), typeof(Image), typeof(Button));
        TradingOrderView.Place((RectTransform)item.transform, parent, x, y, w, h, W, H);
        label = Label(name, item.transform, font, size, 0, 0, W, H);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        var button = item.GetComponent<Button>(); button.targetGraphic = item.GetComponent<Image>(); TabletControlStyle.Button(button);
        if (font != null) { label.font = font; }
        label.fontSize = size; label.fontStyle = FontStyles.Normal;
        button.onClick.AddListener(action);
        return button;
    }
}
