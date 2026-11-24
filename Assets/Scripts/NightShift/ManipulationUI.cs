using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 두 번째 태블릿의 "작전" 탭. 스프레드·매집과 같은 자리에 겹쳐 두고 위쪽 탭으로 오간다.
// 거래량 부풀리기, 허수 매수벽, 종가 관리를 켜고 끄고, 불붙이기와 내려찍기를 한 번씩 쓴다. 실제 동작은 PlayerTactics가 한다.
public sealed class ManipulationUI : MonoBehaviour
{
    private static ManipulationUI instance;
    private const float W = 1600f, H = 880f;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(146, 161, 172, 255), Ground = new Color32(13, 23, 29, 255);
    private static readonly float[] Fractions = { .1f, .25f, .5f };
    /// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly Dictionary<string, (string en, string ja)> Words = new()
    {
        ["작전"] = ("Tactics", "仕掛け"), ["작전 도구"] = ("Tactics", "仕掛けの道具"), ["돈"] = ("DON", "ドン"),
        ["거래량 부풀리기"] = ("Paint the tape", "出来高の水増し"), ["허수 매수벽"] = ("Fake bid wall", "見せ板の買い壁"), ["종가 관리"] = ("Mark the close", "終値の吊り上げ"),
        // 카드의 설명은 "무엇을 위해 쓰는가"를 먼저 적는다. 작동 방식은 그 뒤에 짧게 붙인다.
        ["조용한 종목에 눈길을 끕니다. 45초 동안 저절로 거래가 활발해 보이게 합니다."] = ("Draw eyes to a quiet stock. For 45 seconds it looks busy by itself.", "静かな銘柄に注目を集めます。45秒間、自動で取引が活発に見えます。"),
        ["받쳐 주는 큰돈이 있어 보이게 합니다. 값이 다가오면 저절로 거둡니다."] = ("Make it look like big money supports the price. It pulls itself when the price comes near.", "支える大口がいるように見せます。値が近づくと自動で取り消します。"),
        ["불붙이기"] = ("Ignite", "点火"), ["내려찍기"] = ("Slam", "叩き落とし"), ["실행"] = ("Fire", "実行"),
        ["지금 바로 값을 끌어올립니다. 고른 만큼의 현금으로 한 번에 삽니다(최대 +5%)."] = ("Lift the price right now. Buys in one shot with the cash you picked (up to +5%).", "今すぐ値を上げます。選んだ分の現金で一気に買います(最大+5%)。"),
        ["지금 바로 값을 끌어내립니다. 고른 만큼의 주식을 한 번에 팝니다(최대 -6%)."] = ("Drop the price right now. Sells the share of your stock you picked in one shot (down to -6%).", "今すぐ値を下げます。選んだ分の株を一気に売ります(最大-6%)。"),
        ["오늘 종가를 높게 끝냅니다. 예약해 두면 마감 40초 전부터 저절로 삽니다."] = ("Finish today at a higher close. Arm it and it buys by itself through the last 40 seconds.", "今日の終値を高くします。予約しておくと引けの40秒前から自動で買います。"),
        ["한 번에 쓸 비중"] = ("Size per use", "1回に使う割合"), ["실행 중"] = ("Running", "実行中"), ["쓸 돈"] = ("Cash to use", "使う現金"), ["팔 수량"] = ("Shares to sell", "売る株数"),
        ["지금 바로 값을 끌어올립니다."] = ("Lift the price right now.", "今すぐ値を上げます。"), ["지금 바로 값을 끌어내립니다."] = ("Drop the price right now.", "今すぐ値を下げます。"),
        ["오늘 종가를 높게 끝냅니다."] = ("Finish today at a higher close.", "今日の終値を高くします。"), ["조용한 종목에 눈길을 끕니다."] = ("Draw eyes to a quiet stock.", "静かな銘柄に注目を集めます。"),
        ["받쳐 주는 큰돈이 있어 보이게 합니다."] = ("Make it look like big money supports the price.", "支える大口がいるように見せます。"), ["내 현금(내려찍기는 내 주식)에서 쓸 몫"] = ("Share of your cash (for Slam, of your shares)", "現金(叩き落としは保有株)から使う割合"),
        ["누적 거래대금 1억 돈부터"] = ("From 100M DON turnover", "累計売買代金1億ドンから"), ["내 기업을 세운 뒤부터"] = ("After you found a company", "会社を設立してから"),
        ["불을 붙였습니다"] = ("Ignited", "点火しました"), ["내려찍었습니다"] = ("Slammed", "叩き落としました"),
        ["던질 주식이 없습니다"] = ("No shares to dump", "投げる株がありません"), ["받아 줄 호가가 없습니다"] = ("No bids to hit", "当てる買い板がありません"),
        ["예약하면 장 마감 40초 전부터 사들여 종가를 높게 찍습니다."] =
            ("Once armed, it buys through the last 40 seconds to print a higher close.", "予約すると引けの40秒前から買い上げ、終値を高くします。"),
        ["시작"] = ("Start", "開始"), ["중지"] = ("Stop", "停止"), ["세우기"] = ("Place", "置く"), ["거두기"] = ("Pull", "取り消す"), ["예약"] = ("Arm", "予約"), ["취소"] = ("Cancel", "取消"),
        ["쓸 현금"] = ("Cash to use", "使う現金"), ["현황"] = ("Status", "状況"), ["이 종목의 열기"] = ("Heat of this stock", "この銘柄の熱気"),
        ["부풀린 거래대금"] = ("Painted turnover", "水増しした売買代金"), ["남은 시간"] = ("Time left", "残り時間"), ["벽에 걸린 돈"] = ("Cash shown in wall", "壁に見せている資金"),
        ["벽 가격"] = ("Wall price", "壁の値段"), ["종가 관리 예산"] = ("Close budget", "終値用の予算"), ["종가 관리 사용"] = ("Close spent", "終値で使った額"),
        ["가용 현금"] = ("Free cash", "使える現金"), ["보유 수량"] = ("Shares held", "保有株数"), ["주"] = ("sh", "株"), ["초"] = ("s", "秒"),
        ["잠김"] = ("Locked", "ロック中"), ["누적 거래대금 5억 돈부터"] = ("From 500M DON turnover", "累計売買代金5億ドンから"),
        ["내 리딩방을 연 뒤부터"] = ("After you open your room", "自分のルームを開いてから"), ["내 기업이 상장한 뒤부터"] = ("After your company lists", "自社が上場してから"),
        ["거래 불가"] = ("Cannot trade now", "取引不可"), ["넘길 주식이 없습니다"] = ("No shares to pass", "渡す株がありません"), ["현금 부족"] = ("Not enough cash", "現金不足"),
        ["거래량 부풀리기 시작"] = ("Painting started", "水増し開始"), ["거래량 부풀리기 중지"] = ("Painting stopped", "水増し停止"), ["거래량 부풀리기가 끝났습니다"] = ("Painting finished", "水増し終了"),
        ["매수벽을 세웠습니다"] = ("Wall placed", "買い壁を置きました"), ["매수벽을 거뒀습니다"] = ("Wall pulled", "買い壁を取り消しました"),
        ["매수벽이 체결되거나 사라졌습니다"] = ("The wall filled or is gone", "買い壁が約定したか消えました"), ["값이 다가와 매수벽을 거뒀습니다"] = ("Price came near; wall pulled", "値が近づいたので壁を取り消しました"),
        ["매수벽을 올려 걸었습니다"] = ("Wall moved up", "買い壁を上に置き直しました"), ["종가 관리를 예약했습니다"] = ("Close armed", "終値の吊り上げを予約しました"),
        ["종가 관리를 취소했습니다"] = ("Close cancelled", "予約を取り消しました"), ["종가 관리 중"] = ("Marking the close", "終値を吊り上げ中"), ["종가 관리가 끝났습니다"] = ("Close done", "終値の吊り上げ終了"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    private TMP_FontAsset font;
    private float scale, next;
    private RectTransform panel;
    private Button tab, act, previous, following;
    private readonly Button[] fractions = new Button[3];
    private readonly List<(TextMeshProUGUI label, string korean)> fixedLabels = new();
    private readonly Button[] tools = new Button[5];
    private readonly TextMeshProUGUI[] toolStates = new TextMeshProUGUI[5], toolNames = new TextMeshProUGUI[5], metricCaptions = new TextMeshProUGUI[2], metricValues = new TextMeshProUGUI[2], metricUnits = new TextMeshProUGUI[2];
    private MiniPreview preview;
    private int selected;
    private TextMeshProUGUI stockName, price, toolTitle, toolLock, purpose, sizeLabel, actLabel, notice;
    private float fraction = .25f;
    private int locale = -1;

    public static ManipulationUI Install(GameObject root, float designScale)
    {
        if (root == null) { return null; }
        var existing = root.GetComponent<ManipulationUI>();
        if (existing != null) { return existing; }
        var view = root.AddComponent<ManipulationUI>();
        view.scale = designScale <= 0f ? 1f : designScale; view.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        view.Build(); instance = view;
        return view;
    }
    /// <summary>다른 탭(스프레드·매집)을 고르면 이 화면은 숨는다.</summary>
    public static void Hide()
    {
        if (instance == null || instance.panel == null) { return; }
        instance.panel.gameObject.SetActive(false); TabletControlStyle.Emphasize(instance.tab, false);
    }
    void OnDestroy() { if (instance == this) { instance = null; } }

    // 도구는 열리는 순서대로 놓는다.
    private static readonly PlayerTactics.Tool[] Order = { PlayerTactics.Tool.Ignite, PlayerTactics.Tool.Slam, PlayerTactics.Tool.Close, PlayerTactics.Tool.Wash, PlayerTactics.Tool.Wall };
    private static readonly string[] Names = { "불붙이기", "내려찍기", "종가 관리", "거래량 부풀리기", "허수 매수벽" };
    private static readonly string[] Purposes = { "지금 바로 값을 끌어올립니다.", "지금 바로 값을 끌어내립니다.", "오늘 종가를 높게 끝냅니다.", "조용한 종목에 눈길을 끕니다.", "받쳐 주는 큰돈이 있어 보이게 합니다." };
    private static readonly MiniPreview.Kind[] Pictures = { MiniPreview.Kind.Ignite, MiniPreview.Kind.Slam, MiniPreview.Kind.Close, MiniPreview.Kind.Wash, MiniPreview.Kind.Wall };

    /// <summary>
    /// 왼쪽에서 도구를 고르면 오른쪽에 그 도구의 프리뷰(쓰면 차트가 어떻게 되는지)와 한 줄 설명, 그리고 단추 하나가 나온다.
    /// 예전에는 다섯 도구의 설명과 단추, 아홉 줄의 현황이 한 화면에 모두 깔려 있었다.
    /// </summary>
    private void Build()
    {
        panel = new GameObject("Area :: Tactics", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.gameObject.layer = gameObject.layer; panel.SetParent(transform, false);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
        var ground = panel.GetComponent<Image>(); ground.color = Ground; ground.raycastTarget = true;

        Label("Title", "작전 도구", 32, 8, 280, 56, 40);
        previous = Action("Previous Stock", "‹", 32, 88, 48, 56, () => Select(-1), false, false);
        stockName = Text("Stock", 90, 88, 300, 56, 30);
        following = Action("Next Stock", "›", 398, 88, 48, 56, () => Select(1), false, false);
        price = Text("Current Price", 452, 88, 112, 56, 27); TradingOrderView.Numeric(price); price.alignment = TextAlignmentOptions.MidlineRight;
        Label("Current Price Unit", "돈", 570, 88, 40, 56, 22).color = Muted;

        // 왼쪽: 도구 다섯 가지. 이름만 적고, 잠겼는지 돌고 있는지만 옆에 붙인다.
        for (int i = 0; i < Order.Length; i++)
        {
            int index = i; float y = 168 + i * 136;
            tools[i] = Action("Tool " + i, Names[i], 32, y, 574, 120, () => { selected = index; next = 0f; }, false, true, 31);
            var name = tools[i].GetComponentInChildren<TextMeshProUGUI>(true);
            toolNames[i] = name; name.rectTransform.anchorMin = new Vector2(.06f, 0f); name.rectTransform.anchorMax = new Vector2(.66f, 1f); name.alignment = TextAlignmentOptions.MidlineLeft;
            toolStates[i] = Text("Tool State " + i, 32 + 574 * .64f, y, 574 * .31f, 120, 21); toolStates[i].alignment = TextAlignmentOptions.MidlineRight;
        }
        var divider = new GameObject("Image :: Tactics Divider", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        divider.GetComponent<Image>().color = TabletKitStyle.Line; divider.GetComponent<Image>().raycastTarget = false;
        TradingOrderView.Place((RectTransform)divider.transform, panel, 632, 88, 1, 760, W, H);

        // 오른쪽: 고른 도구.
        toolTitle = Text("Tool Title", 660, 84, 520, 60, 38);
        toolLock = Text("Tool Lock", 1060, 84, 508, 60, 23); toolLock.color = TabletKitStyle.Accent; toolLock.alignment = TextAlignmentOptions.MidlineRight;
        var frame = new GameObject("Image :: Tactic Preview", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        frame.GetComponent<Image>().color = new Color32(8, 15, 20, 255); frame.GetComponent<Image>().raycastTarget = false;
        TradingOrderView.Place((RectTransform)frame.transform, panel, 660, 152, 908, 296, W, H);
        preview = MiniPreview.Create("Tactic Preview Picture", frame.transform, font, 21f * scale);
        purpose = Text("Tool Purpose", 660, 458, 908, 48, 26); purpose.color = Muted;
        sizeLabel = Label("Cash Label", "한 번에 쓸 비중", 660, 520, 236, 48, 24); sizeLabel.color = Muted;
        for (int i = 0; i < fractions.Length; i++)
        {
            int index = i;
            fractions[i] = Action("Cash Fraction " + i, Mathf.RoundToInt(Fractions[i] * 100f) + "%", 904 + i * 224, 520, 216, 48, () => { fraction = Fractions[index]; next = 0f; }, false, false);
        }
        for (int i = 0; i < 2; i++)
        {
            float y = 586 + i * 48;
            metricCaptions[i] = Text("Metric Caption " + i, 660, y, 360, 44, 25); metricCaptions[i].color = Muted;
            metricValues[i] = Text("Metric Value " + i, 1030, y, 470, 44, 29); TradingOrderView.Numeric(metricValues[i]); metricValues[i].alignment = TextAlignmentOptions.MidlineRight;
            metricUnits[i] = Text("Metric Unit " + i, 1510, y, 58, 44, 23); metricUnits[i].color = Muted;
        }
        act = Action("Tool Action", "", 660, 700, 908, 76, Fire, true, false, 32);
        actLabel = act.GetComponentInChildren<TextMeshProUGUI>(true);
        notice = Text("Notice", 660, 788, 908, 60, 22); notice.color = TabletKitStyle.Accent; notice.textWrappingMode = TextWrappingModes.Normal; notice.alignment = TextAlignmentOptions.TopLeft;

        // 탭은 세 화면 위에 함께 떠 있어야 한다. 이미 있는 두 탭을 맨 앞으로 올리고 그 옆에 하나를 더 둔다.
        foreach (string name in new[] { "Button :: Tab Spread", "Button :: Tab Accumulate" }) { var other = transform.Find(name); if (other != null) { other.SetAsLastSibling(); } }
        tab = Action("Tab Tactics", "작전", 710, 12, 180, 48, Show, false, true, 25);
        tab.transform.SetParent(transform, false);
        TradingOrderView.Place((RectTransform)tab.transform, transform, 710, 12, 180, 48, W, H);
        tab.transform.SetAsLastSibling();
        panel.gameObject.SetActive(false);
    }
    private void Show()
    {
        // 매집·스프레드 화면 위를 덮는다. 다른 탭을 누르면 AccumulationUI가 Hide를 부른다.
        panel.gameObject.SetActive(true); panel.SetAsLastSibling();
        foreach (string name in new[] { "Button :: Tab Spread", "Button :: Tab Accumulate" })
        {
            var other = transform.Find(name);
            if (other != null) { other.SetAsLastSibling(); TabletControlStyle.Emphasize(other.GetComponent<Button>(), false); }
        }
        tab.transform.SetAsLastSibling(); TabletControlStyle.Emphasize(tab, true); next = 0f;
    }
    private static Stock Current => StockManager.Instance == null ? null : StockManager.Instance.CurrentStock;
    private void Select(int delta)
    {
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks.Count == 0) { return; }
        int index = manager.stocks.IndexOf(manager.CurrentStock);
        manager.SelectStock((index + delta + manager.stocks.Count) % manager.stocks.Count); next = 0f;
    }
    /// <summary>고른 도구의 단추 하나: 한 번 쓰는 도구는 실행하고, 켜 두는 도구는 켜거나 끈다.</summary>
    private void Fire()
    {
        var tactics = PlayerTactics.Instance; if (tactics == null) { return; }
        switch (Order[selected])
        {
            case PlayerTactics.Tool.Ignite: tactics.Ignite(Current, fraction); break;
            case PlayerTactics.Tool.Slam: tactics.Slam(Current, fraction); break;
            case PlayerTactics.Tool.Close: if (tactics.CloseStock != null) { tactics.CancelClose(); } else { tactics.ArmClose(Current, fraction); } break;
            case PlayerTactics.Tool.Wash: if (tactics.WashStock != null) { tactics.StopWash(); } else { tactics.StartWash(Current); } break;
            default: if (tactics.WallStock != null) { tactics.PullWall(); } else { tactics.SetWall(Current, fraction); } break;
        }
        next = 0f;
    }
    /// <summary>켜 두는 도구가 지금 돌고 있는가.</summary>
    private static bool Running(PlayerTactics tactics, PlayerTactics.Tool tool) =>
        tool == PlayerTactics.Tool.Close ? tactics.CloseStock != null : tool == PlayerTactics.Tool.Wash ? tactics.WashStock != null : tool == PlayerTactics.Tool.Wall && tactics.WallStock != null;
    private void Metric(int row, string caption, string value, string unit)
    { Lit(metricCaptions[row], T(caption)); Lit(metricValues[row], value); Lit(metricUnits[row], T(unit)); }

    void Update()
    {
        if (panel == null || !panel.gameObject.activeInHierarchy || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .4f;
        if (locale != GameLocale.Revision) { locale = GameLocale.Revision; foreach (var item in fixedLabels) { Lit(item.label, T(item.korean)); } }
        var tactics = PlayerTactics.Instance; var stock = Current;
        var account = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : null;
        if (tactics == null || stock == null || account == null) { return; }
        Lit(stockName, GameLocale.Text(stock.stockName)); Lit(price, stock.currentPrice.ToString("N0"));
        for (int i = 0; i < Order.Length; i++)
        {
            bool open = PlayerTactics.Unlocked(Order[i]), running = open && Running(tactics, Order[i]);
            TabletControlStyle.Emphasize(tools[i], i == selected);
            // 고른 줄은 밝은 바탕이 된다. 이름이 묻히지 않게 어두운 글씨로 바꾼다.
            toolNames[i].color = i == selected ? (Color)Ground : !open ? Muted : Ink;
            Lit(toolStates[i], !open ? T("잠김") : running ? T("실행 중") : ""); toolStates[i].color = i == selected ? (Color)Ground : running ? TabletKitStyle.Accent : Muted;
        }
        var tool = Order[selected]; bool usable = PlayerTactics.Unlocked(tool), active = Running(tactics, tool);
        Lit(toolTitle, T(Names[selected])); Lit(toolLock, usable ? "" : T(PlayerTactics.LockReason(tool)));
        preview.Show(Pictures[selected]); Lit(purpose, T(Purposes[selected]));
        // 거래량 부풀리기는 비중을 고르지 않는다(알아서 조금씩 주고받는다).
        bool sized = tool != PlayerTactics.Tool.Wash && !active;
        if (sizeLabel.gameObject.activeSelf != sized) { sizeLabel.gameObject.SetActive(sized); foreach (var button in fractions) { button.gameObject.SetActive(sized); } }
        for (int i = 0; i < fractions.Length; i++) { TabletControlStyle.Emphasize(fractions[i], Mathf.Approximately(fraction, Fractions[i])); }
        decimal free = System.Math.Max(0m, account.cash - account.ReservedCash), held = account.GetHolding(stock), share = (decimal)fraction;
        // 고른 도구에 필요한 숫자 두 줄만 보여 준다.
        if (tool == PlayerTactics.Tool.Slam) { Metric(0, "팔 수량", decimal.Floor(held * share).ToString("N0"), "주"); Metric(1, "보유 수량", held.ToString("N0"), "주"); }
        else if (tool == PlayerTactics.Tool.Wash) { Metric(0, "부풀린 거래대금", tactics.WashPainted.ToString("N0"), "돈"); Metric(1, "남은 시간", tactics.WashLeft.ToString("0"), "초"); }
        else if (tool == PlayerTactics.Tool.Close && active) { Metric(0, "종가 관리 예산", tactics.CloseBudget.ToString("N0"), "돈"); Metric(1, "종가 관리 사용", tactics.CloseSpent.ToString("N0"), "돈"); }
        else if (tool == PlayerTactics.Tool.Wall && active) { Metric(0, "벽 가격", tactics.WallPrice.ToString("N0"), "돈"); Metric(1, "벽에 걸린 돈", tactics.WallShown.ToString("N0"), "돈"); }
        else { Metric(0, "쓸 돈", decimal.Floor(free * share).ToString("N0"), "돈"); Metric(1, "가용 현금", free.ToString("N0"), "돈"); }
        act.interactable = usable;
        string verb = !usable ? "잠김" : tool == PlayerTactics.Tool.Ignite || tool == PlayerTactics.Tool.Slam ? "실행"
            : tool == PlayerTactics.Tool.Close ? (active ? "취소" : "예약") : tool == PlayerTactics.Tool.Wash ? (active ? "중지" : "시작") : active ? "거두기" : "세우기";
        Lit(actLabel, T(verb));
        Lit(notice, T(tactics.Notice));
    }

    private static void Lit(TextMeshProUGUI label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); }
    }
    private TextMeshProUGUI Text(string name, float x, float y, float w, float h, float size)
    {
        var item = new GameObject("Text :: " + name, typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = gameObject.layer };
        var label = item.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform, panel, x, y, w, h, W, H);
        label.font = font; label.fontSize = size * scale; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.color = Ink; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private TextMeshProUGUI Label(string name, string korean, float x, float y, float w, float h, float size)
    {
        var label = Text(name, x, y, w, h, size);
        fixedLabels.Add((label, korean)); Lit(label, T(korean));
        return label;
    }
    private TextMeshProUGUI Metric(string caption, float x, float y, string unit)
    {
        Label(caption + " Label", caption, x, y, 300, 44, 27).color = Muted;
        float unitWidth = unit.Length == 0 ? 0f : 60f;
        var number = Text(caption + " Value", x + 308, y, 608 - 308 - unitWidth - 8, 44, 30); TradingOrderView.Numeric(number); number.alignment = TextAlignmentOptions.MidlineRight;
        if (unit.Length > 0) { Label(caption + " Unit", unit, x + 608 - unitWidth, y, unitWidth, 44, 25).color = Muted; }
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
        button.onClick.AddListener(action);
        return button;
    }
}
