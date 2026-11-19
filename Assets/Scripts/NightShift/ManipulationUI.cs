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
        ["작전"] = ("Tactics", "仕掛け"), ["작전 도구"] = ("Tactics", "仕掛けの道具"), ["원"] = ("KRW", "ウォン"),
        ["거래량 부풀리기"] = ("Paint the tape", "出来高の水増し"), ["허수 매수벽"] = ("Fake bid wall", "見せ板の買い壁"), ["종가 관리"] = ("Mark the close", "終値の吊り上げ"),
        ["차명 계좌와 주고받아 거래량만 늘립니다. 45초."] = ("Trade with a nominee account to inflate volume. 45 seconds.", "名義口座と売り買いして出来高だけ増やします。45秒。"),
        ["현재가 2.5% 아래에 큰 매수 주문을 겁니다. 못 거두면 체결됩니다."] = ("Rest a big bid 2.5% below the market. If not pulled in time, it fills.", "現在値の2.5%下に大きな買い注文を置きます。取り消しが遅れると約定します。"),
        ["불붙이기"] = ("Ignite", "点火"), ["내려찍기"] = ("Slam", "叩き落とし"), ["실행"] = ("Fire", "実行"),
        ["매도 호가를 한 번에 5%까지 쓸어 올립니다. 남은 주문은 바로 거둡니다."] = ("Sweep the asks up to 5% in one shot. Leftover is pulled at once.", "売り板を一気に5%まで買い上げます。残りの注文はすぐ取り消します。"),
        ["보유 주식을 매수 호가에 한 번에 던져 6%까지 내립니다. 남은 주문은 바로 거둡니다."] = ("Dump your shares into the bids in one shot, down to 6%. Leftover is pulled at once.", "保有株を買い板へ一気に投げ、6%まで下げます。残りの注文はすぐ取り消します。"),
        ["불을 붙였습니다"] = ("Ignited", "点火しました"), ["내려찍었습니다"] = ("Slammed", "叩き落としました"),
        ["던질 주식이 없습니다"] = ("No shares to dump", "投げる株がありません"), ["받아 줄 호가가 없습니다"] = ("No bids to hit", "当てる買い板がありません"),
        ["예약하면 장 마감 40초 전부터 사들여 종가를 높게 찍습니다."] =
            ("Once armed, it buys through the last 40 seconds to print a higher close.", "予約すると引けの40秒前から買い上げ、終値を高くします。"),
        ["시작"] = ("Start", "開始"), ["중지"] = ("Stop", "停止"), ["세우기"] = ("Place", "置く"), ["거두기"] = ("Pull", "取り消す"), ["예약"] = ("Arm", "予約"), ["취소"] = ("Cancel", "取消"),
        ["쓸 현금"] = ("Cash to use", "使う現金"), ["현황"] = ("Status", "状況"), ["이 종목의 열기"] = ("Heat of this stock", "この銘柄の熱気"),
        ["부풀린 거래대금"] = ("Painted turnover", "水増しした売買代金"), ["남은 시간"] = ("Time left", "残り時間"), ["벽에 걸린 돈"] = ("Cash shown in wall", "壁に見せている資金"),
        ["벽 가격"] = ("Wall price", "壁の値段"), ["종가 관리 예산"] = ("Close budget", "終値用の予算"), ["종가 관리 사용"] = ("Close spent", "終値で使った額"),
        ["가용 현금"] = ("Free cash", "使える現金"), ["보유 수량"] = ("Shares held", "保有株数"), ["주"] = ("sh", "株"), ["초"] = ("s", "秒"),
        ["잠김"] = ("Locked", "ロック中"), ["누적 거래대금 5억 원부터"] = ("From 500M KRW turnover", "累計売買代金5億ウォンから"),
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
    private Button tab, wash, wall, close, ignite, slam, previous, following;
    private readonly Button[] fractions = new Button[3];
    private readonly List<(TextMeshProUGUI label, string korean)> fixedLabels = new();
    private TextMeshProUGUI washLock, wallLock, closeLock, igniteLock, slamLock, igniteLabel, slamLabel;
    private TextMeshProUGUI stockName, price, washLabel, wallLabel, closeLabel, heat, painted, washLeft, wallCash, wallPrice, closeBudget, closeSpent, free, held, notice;
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

    private void Build()
    {
        panel = new GameObject("Area :: Tactics", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.gameObject.layer = gameObject.layer; panel.SetParent(transform, false);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
        var ground = panel.GetComponent<Image>(); ground.color = Ground; ground.raycastTarget = true;

        Label("Title", "작전 도구", 32, 8, 280, 56, 40);
        previous = Action("Previous Stock", "‹", 32, 88, 48, 56, () => Select(-1), false, false);
        stockName = Text("Stock", 88, 88, 720, 64, 38);
        following = Action("Next Stock", "›", 832, 88, 48, 56, () => Select(1), false, false);
        price = Text("Current Price", 32, 160, 800, 48, 32); TradingOrderView.Numeric(price); price.alignment = TextAlignmentOptions.MidlineRight;
        Label("Current Price Unit", "원", 848, 160, 60, 48, 25).color = Muted;

        Label("Cash Label", "쓸 현금", 32, 222, 200, 48, 27).color = Muted;
        for (int i = 0; i < fractions.Length; i++)
        {
            int index = i;
            fractions[i] = Action("Cash Fraction " + i, Mathf.RoundToInt(Fractions[i] * 100f) + "%", 250 + i * 214, 222, 204, 48, () => { fraction = Fractions[index]; next = 0f; }, false, false);
        }
        // 다섯 가지를 한 화면에 둔다. 설명은 한 줄로 줄였다.
        Card(0, 290, "불붙이기", "매도 호가를 한 번에 5%까지 쓸어 올립니다. 남은 주문은 바로 거둡니다.", out ignite, out igniteLabel, () => { PlayerTactics.Instance?.Ignite(Current, fraction); next = 0f; });
        igniteLock = LockLine(0, 290);
        Card(1, 402, "내려찍기", "보유 주식을 매수 호가에 한 번에 던져 6%까지 내립니다. 남은 주문은 바로 거둡니다.", out slam, out slamLabel, () => { PlayerTactics.Instance?.Slam(Current, fraction); next = 0f; });
        slamLock = LockLine(1, 402);
        Card(2, 514, "거래량 부풀리기", "차명 계좌와 주고받아 거래량만 늘립니다. 45초.", out wash, out washLabel, ToggleWash);
        washLock = LockLine(2, 514);
        Card(3, 626, "허수 매수벽", "현재가 2.5% 아래에 큰 매수 주문을 겁니다. 못 거두면 체결됩니다.", out wall, out wallLabel, ToggleWall);
        wallLock = LockLine(3, 626);
        Card(4, 738, "종가 관리", "예약하면 장 마감 40초 전부터 사들여 종가를 높게 찍습니다.", out close, out closeLabel, ToggleClose);
        closeLock = LockLine(4, 738);

        var divider = new GameObject("Image :: Tactics Divider", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        divider.GetComponent<Image>().color = TabletKitStyle.Line; divider.GetComponent<Image>().raycastTarget = false;
        TradingOrderView.Place((RectTransform)divider.transform, panel, 920, 88, 1, 752, W, H);
        Label("Status Title", "현황", 960, 88, 608, 56, 34);
        heat = Metric("이 종목의 열기", 960, 160, "");
        free = Metric("가용 현금", 960, 212, "원");
        held = Metric("보유 수량", 960, 264, "주");
        painted = Metric("부풀린 거래대금", 960, 340, "원");
        washLeft = Metric("남은 시간", 960, 392, "초");
        wallPrice = Metric("벽 가격", 960, 468, "원");
        wallCash = Metric("벽에 걸린 돈", 960, 520, "원");
        closeBudget = Metric("종가 관리 예산", 960, 596, "원");
        closeSpent = Metric("종가 관리 사용", 960, 648, "원");
        notice = Text("Notice", 960, 730, 608, 110, 26); notice.color = TabletKitStyle.Accent; notice.textWrappingMode = TextWrappingModes.Normal; notice.alignment = TextAlignmentOptions.TopLeft;

        // 탭은 세 화면 위에 함께 떠 있어야 한다. 이미 있는 두 탭을 맨 앞으로 올리고 그 옆에 하나를 더 둔다.
        foreach (string name in new[] { "Button :: Tab Spread", "Button :: Tab Accumulate" }) { var other = transform.Find(name); if (other != null) { other.SetAsLastSibling(); } }
        tab = Action("Tab Tactics", "작전", 710, 12, 180, 48, Show, false, true, 25);
        tab.transform.SetParent(transform, false);
        TradingOrderView.Place((RectTransform)tab.transform, transform, 710, 12, 180, 48, W, H);
        tab.transform.SetAsLastSibling();
        panel.gameObject.SetActive(false);
    }
    private void Card(int index, float y, string title, string description, out Button button, out TextMeshProUGUI buttonLabel, UnityEngine.Events.UnityAction action)
    {
        Label("Card Title " + index, title, 32, y, 560, 48, 31);
        var text = Label("Card Text " + index, description, 32, y + 50, 848, 56, 22);
        text.color = Muted; text.textWrappingMode = TextWrappingModes.Normal; text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Truncate;
        button = Action("Card Button " + index, "", 620, y, 260, 48, action, true, false);
        buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>카드 제목 옆에 여는 조건을 적는 줄.</summary>
    private TextMeshProUGUI LockLine(int index, float y)
    {
        var line = Text("Card Lock " + index, 300, y, 310, 48, 22); line.color = TabletKitStyle.Accent; line.alignment = TextAlignmentOptions.MidlineRight;
        return line;
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
    private void ToggleWash()
    {
        var tactics = PlayerTactics.Instance; if (tactics == null) { return; }
        if (tactics.WashStock != null) { tactics.StopWash(); } else { tactics.StartWash(Current); }
        next = 0f;
    }
    private void ToggleWall()
    {
        var tactics = PlayerTactics.Instance; if (tactics == null) { return; }
        if (tactics.WallStock != null) { tactics.PullWall(); } else { tactics.SetWall(Current, fraction); }
        next = 0f;
    }
    private void ToggleClose()
    {
        var tactics = PlayerTactics.Instance; if (tactics == null) { return; }
        if (tactics.CloseStock != null) { tactics.CancelClose(); } else { tactics.ArmClose(Current, fraction); }
        next = 0f;
    }

    void Update()
    {
        if (panel == null || !panel.gameObject.activeInHierarchy || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .4f;
        if (locale != GameLocale.Revision) { locale = GameLocale.Revision; foreach (var item in fixedLabels) { Lit(item.label, T(item.korean)); } }
        var tactics = PlayerTactics.Instance; var stock = Current;
        var account = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.primary : null;
        if (tactics == null || stock == null || account == null) { return; }
        Lit(stockName, GameLocale.Text(stock.stockName)); Lit(price, stock.currentPrice.ToString("N0"));
        for (int i = 0; i < fractions.Length; i++) { TabletControlStyle.Emphasize(fractions[i], Mathf.Approximately(fraction, Fractions[i])); }
        // 잠긴 도구는 단추가 눌리지 않고, 설명 자리에 여는 조건이 뜬다.
        bool canWash = PlayerTactics.Unlocked(PlayerTactics.Tool.Wash), canWall = PlayerTactics.Unlocked(PlayerTactics.Tool.Wall), canClose = PlayerTactics.Unlocked(PlayerTactics.Tool.Close);
        wash.interactable = canWash; wall.interactable = canWall; close.interactable = canClose;
        bool canIgnite = PlayerTactics.Unlocked(PlayerTactics.Tool.Ignite), canSlam = PlayerTactics.Unlocked(PlayerTactics.Tool.Slam);
        ignite.interactable = canIgnite; slam.interactable = canSlam;
        Lit(igniteLabel, T(canIgnite ? "실행" : "잠김")); Lit(slamLabel, T(canSlam ? "실행" : "잠김"));
        Lit(igniteLock, canIgnite ? "" : T(PlayerTactics.LockReason(PlayerTactics.Tool.Ignite))); Lit(slamLock, canSlam ? "" : T(PlayerTactics.LockReason(PlayerTactics.Tool.Slam)));
        Lit(washLabel, T(!canWash ? "잠김" : tactics.WashStock != null ? "중지" : "시작")); Lit(wallLabel, T(!canWall ? "잠김" : tactics.WallStock != null ? "거두기" : "세우기")); Lit(closeLabel, T(!canClose ? "잠김" : tactics.CloseStock != null ? "취소" : "예약"));
        Lit(washLock, canWash ? "" : T(PlayerTactics.LockReason(PlayerTactics.Tool.Wash))); Lit(wallLock, canWall ? "" : T(PlayerTactics.LockReason(PlayerTactics.Tool.Wall))); Lit(closeLock, canClose ? "" : T(PlayerTactics.LockReason(PlayerTactics.Tool.Close)));
        float warmth = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
        Lit(heat, warmth.ToString("0.0"));
        Lit(free, System.Math.Max(0m, account.cash - account.ReservedCash).ToString("N0")); Lit(held, account.GetHolding(stock).ToString("N0"));
        Lit(painted, tactics.WashPainted.ToString("N0")); Lit(washLeft, tactics.WashLeft.ToString("0"));
        Lit(wallPrice, tactics.WallPrice.ToString("N0")); Lit(wallCash, tactics.WallShown.ToString("N0"));
        Lit(closeBudget, tactics.CloseBudget.ToString("N0")); Lit(closeSpent, tactics.CloseSpent.ToString("N0"));
        string running = (tactics.WashStock != null ? GameLocale.Text(tactics.WashStock.stockName) + " · " + T("거래량 부풀리기") + "\n" : "")
            + (tactics.WallStock != null ? GameLocale.Text(tactics.WallStock.stockName) + " · " + T("허수 매수벽") + "\n" : "")
            + (tactics.CloseStock != null ? GameLocale.Text(tactics.CloseStock.stockName) + " · " + T("종가 관리") + "\n" : "");
        Lit(notice, running + T(tactics.Notice));
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
