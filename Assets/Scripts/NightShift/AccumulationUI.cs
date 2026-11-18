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
        ["매집 가격대"] = ("Price band", "買い集め価格帯"), ["원"] = ("KRW", "ウォン"), ["주"] = ("sh", "株"),
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
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    private TradingOperationController operation;
    private TMP_FontAsset font;
    private float scale, next;
    private RectTransform panel;
    private Button spreadTab, accumulateTab, start, stop, previous, following;
    private readonly Button[] presets = new Button[3], fractions = new Button[4];
    private static readonly float[] FractionValues = { .25f, .5f, .75f, 1f };
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
        if (spread == null || controller == null) { return null; }
        var existing = spread.GetComponent<AccumulationUI>();
        if (existing != null) { return existing; }
        var view = spread.gameObject.AddComponent<AccumulationUI>();
        view.operation = controller; view.scale = designScale <= 0f ? 1f : designScale;
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
        previous = Action("Previous Stock", "‹", 32, 88, 48, 56, () => Select(-1), false, false);
        stockName = Text("Stock", 88, 88, 720, 64, 38);
        following = Action("Next Stock", "›", 832, 88, 48, 56, () => Select(1), false, false);
        price = Number("Current Price", 32, 168, 800, 56, 32); Label("Current Price Unit", "원", 848, 168, 60, 56, 25).color = Muted;

        Label("Band Label", "매집 가격대", 32, 244, 480, 44, 29);
        lowInput = Input("Band Low", 32, 296, 360, 56); Label("Low Unit", "원", 400, 296, 60, 56, 25).color = Muted;
        Text("Band Dash", 440, 296, 40, 56, 30).text = "~";
        highInput = Input("Band High", 480, 296, 360, 56); Label("High Unit", "원", 848, 296, 60, 56, 25).color = Muted;
        for (int i = 0; i < presets.Length; i++)
        {
            int index = i;
            presets[i] = Action("Band Preset " + i, PresetNames[i], 32 + i * 286, 368, 276, 48, () => Preset(index), false, true, 23);
        }
        Label("Cash Label", "사용할 현금", 32, 440, 480, 44, 29);
        for (int i = 0; i < fractions.Length; i++)
        {
            int index = i;
            fractions[i] = Action("Cash Fraction " + i, Mathf.RoundToInt(FractionValues[i] * 100f) + "%", 32 + i * 214, 492, 204, 52, () => { fraction = FractionValues[index]; next = 0f; }, false, false);
        }
        budget = Metric("매집 예산", 32, 564, 848, "원");
        hint = Label("Crash Hint", "30초 안에 4% 넘게 떨어지면 매수 대기를 바로 거두고, 평균 단가보다 쌀 때만 조금씩 삽니다.", 32, 624, 848, 96, 23);
        hint.color = Muted; hint.textWrappingMode = TextWrappingModes.Normal; hint.alignment = TextAlignmentOptions.TopLeft;
        start = Action("Start Accumulation", "매집 시작", 32, 792, 408, 56, StartAccumulation, true, true);
        stop = Action("Stop Accumulation", "매집 중지", 472, 792, 408, 56, StopAccumulation, false, true);

        var divider = Graphic("Accumulation Divider", panel, TabletKitStyle.Line);
        TradingOrderView.Place(divider.rectTransform, panel, 920, 88, 1, 752, W, H);
        statusHeading = Label("Status Title", "매집 현황", 960, 88, 608, 56, 34);
        shares = Metric("매집 수량", 960, 160, 608, "주");
        average = Metric("평균 단가", 960, 212, 608, "원");
        spent = Metric("매집 금액", 960, 264, 608, "원");
        value = Metric("평가 금액", 960, 316, 608, "원");
        profit = Metric("평가 손익", 960, 368, 608, "원");
        left = Metric("남은 예산", 960, 420, 608, "원");
        resting = Metric("매수 대기", 960, 472, 608, "");
        used = Text("Budget Used", 960, 532, 608, 36, 24); used.color = Muted;
        var track = Graphic("Budget Track", panel, Line); TradingOrderView.Place(track.rectTransform, panel, 960, 574, 608, 10, W, H);
        usedFill = Graphic("Budget Fill", track.transform, TabletKitStyle.Accent).rectTransform;
        usedFill.anchorMin = Vector2.zero; usedFill.anchorMax = new Vector2(0, 1); usedFill.offsetMin = usedFill.offsetMax = Vector2.zero;
        Label("Recent Fills", "최근 체결", 960, 612, 608, 48, 34);
        tape = Text("Fill Tape", 960, 668, 608, 112, 25); tape.textWrappingMode = TextWrappingModes.Normal; tape.alignment = TextAlignmentOptions.TopLeft;
        elapsed = Text("Elapsed", 960, 792, 608, 56, 25); elapsed.color = Muted;

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

    private void Select(int delta)
    {
        var manager = StockManager.Instance;
        if (operation.Active || manager == null || manager.stocks.Count == 0) { return; }
        int index = manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index + delta + manager.stocks.Count) % manager.stocks.Count); next = 0f;
    }
    private void Preset(int index)
    {
        var stock = StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        if (stock == null || operation.Active) { return; }
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
        if (!Read(lowInput, out decimal low) || !Read(highInput, out decimal high) || high < low)
        { notice = "가격대를 확인하세요"; noticeUntil = Time.unscaledTime + 4f; next = 0f; return; }
        if (!operation.StartAccumulate(stock, low, high, fraction))
        { notice = operation.Feedback; noticeUntil = Time.unscaledTime + 4f; }
        next = 0f;
    }
    private void StopAccumulation() { operation.CancelAccumulate(); next = 0f; }

    private void OnEnable() { next = 0f; }
    private void Update()
    {
        if (operation == null || panel == null || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .25f;
        if (locale != GameLocale.Revision)
        {
            locale = GameLocale.Revision;
            foreach (var entry in fixedLabels) { Lit(entry.label, T(entry.korean)); }
        }
        if (!showing) { return; }
        bool running = operation.AccumulateRunning;
        var stock = running ? operation.Target : StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
        if (stock != shown && !running && stock != null && !lowInput.isFocused && !highInput.isFocused)
        { shown = stock; SetBand(stock.currentPrice * .95m, stock.currentPrice * 1.01m); }
        // 돌아가는 동안에는 실제로 쓰고 있는 가격대를 보여 준다.
        if (running) { shown = stock; SetBand(operation.AccumulateLow, operation.AccumulateHigh); }
        Lit(stockName, stock == null ? "—" : GameLocale.Text(stock.stockName));
        Lit(price, stock == null ? "—" : TradingOrderView.Number(stock.currentPrice));
        string status = Time.unscaledTime < noticeUntil ? notice : running ? operation.Feedback : operation.SpreadRunning ? "스프레드 실행 중" :
            operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed && operation.Bought > 0m ? "매집 중지됨" : "";
        Lit(state, T(status));
        state.color = operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Crash && running ? MarketColors.Down : TabletKitStyle.Accent;

        decimal planned = running ? operation.Budget : decimal.Floor(operation.SpreadAvailableCash * (decimal)fraction);
        Lit(budget, TradingOrderView.Number(planned));
        // 지금(또는 방금 끝난) 매집의 결과. 스프레드를 돌리는 중에는 그쪽 숫자가 섞이지 않게 비운다.
        bool mine = running || operation.CurrentMode == TradingOperationController.Mode.Review && operation.AccumulateStatus == TradingOperationController.AccumulatePhase.Closed;
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
                if (++lines > 3) { break; }
                buffer.Append(GameLocale.Text(fill.Time ?? "")).Append("   ").Append(TradingOrderView.Number(fill.Price)).Append(" × ").Append(TradingOrderView.Number(fill.Quantity)).AppendLine();
            }
        }
        Lit(tape, buffer.Length > 0 ? buffer.ToString().TrimEnd() : "—");

        bool idle = !operation.Active;
        previous.interactable = following.interactable = idle;
        lowInput.interactable = highInput.interactable = idle;
        foreach (var button in presets) { button.interactable = idle && stock != null; }
        for (int i = 0; i < fractions.Length; i++) { fractions[i].interactable = idle; TabletControlStyle.Emphasize(fractions[i], Mathf.Approximately(FractionValues[i], fraction)); }
        start.interactable = idle && stock != null && stock.AcceptsOrders && MarketDayController.CanTrade && Time.timeScale > 0f;
        stop.interactable = running;
        TabletControlStyle.Emphasize(start, !running); TabletControlStyle.Emphasize(stop, running);
    }

    private static void Lit(TextMeshProUGUI label, string text)
    {
        text ??= "";
        if (!GameLocalizedText.Matches(label, text)) { GameLocalizedText.Set(label, text, true); }
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
