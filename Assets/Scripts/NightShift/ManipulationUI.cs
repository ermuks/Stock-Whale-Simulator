using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 두 번째 태블릿의 "작전" 탭. 스프레드·매집과 같은 자리에 겹쳐 두고 위쪽 탭으로 오간다.
// 거래량 부풀리기, 허수 매수벽, 종가 관리를 켜고 끄고, 불붙이기와 내려찍기를 한 번씩 쓴다. 실제 동작은 PlayerTactics가 한다.
// 켜 두는 도구는 여러 종목에서 한꺼번에 돌릴 수 있다. 화면은 언제나 지금 고른 종목(StockManager.CurrentStock)을 따라가고,
// 단추 하나는 그 종목에서 도구를 켜거나 끈다. 다른 종목에서 돌고 있는 것은 왼쪽 아래 "진행 중" 목록에서 보고, 누르면 그리로 간다.
public sealed class ManipulationUI : MonoBehaviour
{
    private static ManipulationUI instance;
    private const float W = 1600f, H = 880f;
    private const int RunRows = 4;
    private static readonly Color Ink = new Color32(230, 235, 239, 255), Muted = new Color32(146, 161, 172, 255), Ground = new Color32(13, 23, 29, 255);
    /// <summary>이 화면의 글자. 한국어·영어·일본어를 각각 적는다.</summary>
    private static readonly Dictionary<string, (string en, string ja)> Words = new()
    {
        ["작전"] = ("Tactics", "仕掛け"), ["작전 도구"] = ("Tactics", "仕掛けの道具"), ["돈"] = ("Ð", "Ð"),
        ["거래량 부풀리기"] = ("Paint the tape", "出来高の水増し"), ["허수 매수벽"] = ("Fake bid wall", "見せ板の買い壁"), ["종가 관리"] = ("Mark the close", "終値の吊り上げ"),
        // 카드의 설명은 "무엇을 위해 쓰는가"를 먼저 적는다. 작동 방식은 그 뒤에 짧게 붙인다.
        ["조용한 종목에 눈길을 끕니다. 45초 동안 저절로 거래가 활발해 보이게 합니다."] = ("Draw eyes to a quiet stock. For 45 seconds it looks busy by itself.", "静かな銘柄に注目を集めます。45秒間、自動で取引が活発に見えます。"),
        ["받쳐 주는 큰돈이 있어 보이게 합니다. 값이 다가오면 저절로 거둡니다."] = ("Make it look like big money supports the price. It pulls itself when the price comes near.", "支える大口がいるように見せます。値が近づくと自動で取り消します。"),
        ["불붙이기"] = ("Ignite", "点火"), ["내려찍기"] = ("Slam", "叩き落とし"), ["실행"] = ("Run", "実行"),
        ["지금 바로 값을 끌어올립니다. 고른 만큼의 현금으로 한 번에 삽니다(최대 +5%)."] = ("Lift the price right now. Buys in one shot with the cash you picked (up to +5%).", "今すぐ値を上げます。選んだ分の現金で一気に買います(最大+5%)。"),
        ["지금 바로 값을 끌어내립니다. 고른 만큼의 주식을 한 번에 팝니다(최대 -6%)."] = ("Drop the price right now. Sells the share of your stock you picked in one shot (down to -6%).", "今すぐ値を下げます。選んだ分の株を一気に売ります(最大-6%)。"),
        ["오늘 종가를 높게 끝냅니다. 예약해 두면 마감 40초 전부터 저절로 삽니다."] = ("Finish today at a higher close. Arm it and it buys by itself through the last 40 seconds.", "今日の終値を高くします。予約しておくと引けの40秒前から自動で買います。"),
        ["쓸 돈 · +5%까지"] = ("Cash to use · up to +5%", "使う現金 · +5%まで"), ["쓸 돈 · +10%까지"] = ("Cash to use · up to +10%", "使う現金 · +10%まで"), ["쓸 돈 · +20%까지"] = ("Cash to use · up to +20%", "使う現金 · +20%まで"),
        ["팔 수량 · -6%까지"] = ("Shares to sell · down to -6%", "売る株数 · -6%まで"), ["팔 수량 · -10%까지"] = ("Shares to sell · down to -10%", "売る株数 · -10%まで"), ["팔 수량 · -18%까지"] = ("Shares to sell · down to -18%", "売る株数 · -18%まで"),
        ["넘기기"] = ("Unload", "売り上がり"), ["오르는 값에 내 물량을 조금씩 넘깁니다."] = ("Hand your shares to buyers as the price climbs.", "上がる値に合わせて持ち株を少しずつ渡します。"),
        ["넘긴 수량"] = ("Handed over", "渡した株数"), ["넘길 수량"] = ("Shares to hand over", "渡す株数"), ["이 종목에서 넘긴 수량"] = ("Handed over here", "この銘柄で渡した株数"),
        ["넘기기 시작"] = ("Unloading started", "売り上がり開始"), ["넘기기 중지"] = ("Unloading stopped", "売り上がり停止"), ["넘기기가 끝났습니다"] = ("Unloading finished", "売り上がり終了"),
        ["한 번에 쓸 비중"] =("Size per use", "1回に使う割合"), ["실행 중"] = ("Running", "実行中"), ["쓸 돈"] = ("Cash to use", "使う現金"), ["팔 수량"] = ("Shares to sell", "売る株数"),
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
        // 종목 띠(보유·손익)와 진행 중 목록.
        ["보유 {0}주 · 평단 {1}"] = ("Holding {0} sh · avg {1}", "保有 {0}株 · 平均 {1}"), ["보유 없음"] = ("No shares held", "保有なし"),
        ["열기 {0} / 5"] = ("Heat {0} / 5", "熱気 {0} / 5"), ["열기 {0} · 불나방 몰림"] = ("Heat {0} · crowd rushing in", "熱気 {0} · イナゴ殺到"),
        ["평가손익"] = ("Unrealized", "評価損益"), ["이 종목으로 번 돈"] = ("Made here", "この銘柄の実現益"),
        ["진행 중"] = ("Active now", "進行中"), ["{0}종목"] = ("on {0}", "{0}銘柄"),
        ["이 종목에서 산 수량"] = ("Bought here so far", "この銘柄で買った株数"), ["이 종목에서 판 금액"] = ("Sold here so far", "この銘柄で売った金額"),
        ["이 종목에서 부풀린 금액"] = ("Painted here so far", "この銘柄で水増しした額"),
        // 금융 용어를 모르는 사람도 읽히게: 평단 → 평균 산 값, 평가손익 → 지금 팔면.
        ["보유 {0}주 · 평균단가 {1}"] = ("Holding {0} sh · avg price {1}", "保有 {0}株 · 平均単価 {1}"),
        ["열기가 5가 되면 급등을 쫓는 사람들(불나방)이 몰려옵니다"] = ("At heat 5, rally chasers (moths) pile in", "熱気が5になると急騰を追う人(イナゴ)が集まります"),
        ["급등을 쫓는 사람들(불나방)이 몰려오는 중입니다"] = ("Rally chasers (moths) are piling in", "急騰を追う人(イナゴ)が集まっています"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;

    private TMP_FontAsset font;
    private float scale, next;
    private RectTransform panel;
    private Button tab, act, previous, following;
    // 한 번에 쓸 비중은 슬라이더로 1%씩 고른다(예전에는 10·25·50% 단추 세 개였다).
    private Slider sizeSlider;
    private TextMeshProUGUI sizeShare;
    private readonly List<(TextMeshProUGUI label, string korean)> fixedLabels = new();
    private readonly Button[] tools = new Button[6];
    private readonly TextMeshProUGUI[] toolStates = new TextMeshProUGUI[6], toolNames = new TextMeshProUGUI[6], metricCaptions = new TextMeshProUGUI[2], metricValues = new TextMeshProUGUI[2], metricUnits = new TextMeshProUGUI[2];
    private readonly Button[] runRows = new Button[RunRows], runStops = new Button[RunRows];
    private readonly TextMeshProUGUI[] runLabels = new TextMeshProUGUI[RunRows];
    private readonly PlayerTactics.Run[] rowRuns = new PlayerTactics.Run[RunRows];
    private TacticDiagram diagram;
    private RectTransform logo;
    private Image toolSymbol;
    private readonly Image[] toolIcons = new Image[6], heatCells = new Image[5], runIcons = new Image[RunRows], runBars = new Image[RunRows];
    private static readonly Color Amber = new Color32(242, 188, 100, 255), HeatOff = new Color32(30, 45, 54, 255);
    private Stock logoStock;
    private int selected;
    private TextMeshProUGUI stockName, price, holding, heat, heatHint, paper, earned, runHeading, runMore, toolTitle, toolLock, purpose, sentence, sizeLabel, actLabel, notice;
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
    private static readonly PlayerTactics.Tool[] Order = { PlayerTactics.Tool.Ignite, PlayerTactics.Tool.Unload, PlayerTactics.Tool.Slam, PlayerTactics.Tool.Close, PlayerTactics.Tool.Wash, PlayerTactics.Tool.Wall };
    private static readonly string[] Names = { "불붙이기", "넘기기", "내려찍기", "종가 관리", "거래량 부풀리기", "허수 매수벽" };
    private static readonly string[] Purposes = { "지금 바로 값을 끌어올립니다.", "오르는 값에 내 물량을 조금씩 넘깁니다.", "지금 바로 값을 끌어내립니다.", "오늘 종가를 높게 끝냅니다.", "조용한 종목에 눈길을 끕니다.", "받쳐 주는 큰돈이 있어 보이게 합니다." };
    private static readonly MiniPreview.Kind[] Pictures = { MiniPreview.Kind.Ignite, MiniPreview.Kind.Ignite, MiniPreview.Kind.Slam, MiniPreview.Kind.Close, MiniPreview.Kind.Wash, MiniPreview.Kind.Wall };
    private static int IndexOf(PlayerTactics.Tool tool) => Mathf.Max(0, System.Array.IndexOf(Order, tool));

    /// <summary>
    /// 왼쪽: 종목과 그 종목의 보유·손익 두 줄, 도구 다섯 가지, 그 아래 지금 돌고 있는 작전 목록(없으면 아무것도 없다).
    /// 오른쪽: 고른 도구의 프리뷰(쓰면 차트가 어떻게 되는지)와 한 줄 설명, 그리고 단추 하나.
    /// 예전에는 다섯 도구의 설명과 단추, 아홉 줄의 현황이 한 화면에 모두 깔려 있었다.
    /// </summary>
    private void Build()
    {
        panel = new GameObject("Area :: Tactics", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.gameObject.layer = gameObject.layer; panel.SetParent(transform, false);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
        var ground = panel.GetComponent<Image>(); ground.color = Ground; ground.raycastTarget = true;

        Label("Title", "작전 도구", 32, 8, 280, 56, 40);
        // 종목 머리: ‹ 로고 이름 › 값. 로고는 업종 도형 + 회사마다 다른 분할로 조립한다(UiPictograms.Logo).
        // 종목 머리: 큰 로고 · 이름 · 값, 이름 밑에 보유. ‹ › 로 종목을 넘긴다.
        logo = Area("Stock Logo", 32, 80, 84, 84);
        stockName = Text("Stock", 132, 80, 300, 46, 32);
        previous = Action("Previous Stock", "‹", 440, 84, 40, 40, () => Select(-1), false, false);
        following = Action("Next Stock", "›", 486, 84, 40, 40, () => Select(1), false, false);
        price = Text("Current Price", 448, 128, 116, 40, 28); TradingOrderView.Numeric(price); price.alignment = TextAlignmentOptions.MidlineRight;
        Label("Current Price Unit", "돈", 570, 128, 40, 40, 22).color = Muted;

        // 종목 바로 아래 조용한 두 줄: 얼마나 들고 있는지, 이 종목으로 얼마를 벌었는지.
        holding = Text("Holding", 132, 126, 320, 32, 21); holding.color = Muted;
        paper = Text("Paper Profit", 32, 176, 280, 32, 23); paper.color = Muted;
        earned = Text("Earned Here", 312, 176, 294, 32, 23); earned.color = Muted; earned.alignment = TextAlignmentOptions.MidlineRight;
        // 이 종목의 열기(사람들의 관심). 다섯 칸 게이지로 보여 주고, 다섯 칸이 차면 무슨 일이 생기는지 바로 밑에 적는다.
        heat = Text("Heat", 32, 216, 220, 34, 23);
        for (int i = 0; i < heatCells.Length; i++)
        {
            var cell = new GameObject("Image :: Heat Cell " + i, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
            TradingOrderView.Place((RectTransform)cell.transform, panel, 260 + i * 70, 228, 64, 12, W, H);
            heatCells[i] = cell.GetComponent<Image>(); heatCells[i].raycastTarget = false; heatCells[i].color = HeatOff;
        }
        heatHint = Label("Heat Hint", "열기가 5가 되면 급등을 쫓는 사람들(불나방)이 몰려옵니다", 32, 248, 574, 28, 19); heatHint.color = Muted;

        // 도구 여섯 가지: 상징 그림 · 이름 · 이 종목/다른 종목에서 돌고 있는지.
        for (int i = 0; i < Order.Length; i++)
        {
            // 작전 고르기: 2열 3줄의 타일. 타일마다 그 작전의 엠블럼(시세에 무엇을 하는지 그린 도식)과 이름, 아래에 상태.
            int index = i; float x = 32 + (i % 2) * 292, y = 290 + (i / 2) * 108;
            tools[i] = Action("Tool " + i, Names[i], x, y, 282, 100, () => { selected = index; next = 0f; }, false, true, 25);
            var name = tools[i].GetComponentInChildren<TextMeshProUGUI>(true);
            toolNames[i] = name; name.rectTransform.anchorMin = new Vector2(.06f, .56f); name.rectTransform.anchorMax = new Vector2(.96f, .94f); name.alignment = TextAlignmentOptions.MidlineLeft;
            toolIcons[i] = UiPictograms.Icon(tools[i].transform, "Tool Emblem " + i, UiPictograms.Emblem(Order[i]), Muted, .04f, .40f, .66f, .56f);
            toolStates[i] = Text("Tool State " + i, x + 282 * .5f, y + 56, 282 * .46f, 40, 19); toolStates[i].alignment = TextAlignmentOptions.MidlineRight;
        }

        // 진행 중: 모든 종목에서 돌고 있는 작전을 최근 것부터 네 줄까지. 줄을 누르면 그 종목과 도구로 가고, ×는 그것만 멈춘다.
        runHeading = Label("Runs Title", "진행 중", 32, 618, 300, 30, 21); runHeading.color = Muted;
        runMore = Text("Runs More", 406, 618, 200, 30, 21); runMore.color = Muted; runMore.alignment = TextAlignmentOptions.MidlineRight;
        for (int i = 0; i < RunRows; i++)
        {
            int index = i; float y = 652 + i * 50;
            runRows[i] = Action("Run " + i, "", 32, y, 516, 44, () => Jump(index), false, false, 22);
            runLabels[i] = runRows[i].GetComponentInChildren<TextMeshProUGUI>(true);
            runLabels[i].rectTransform.anchorMin = new Vector2(.09f, .28f); runLabels[i].rectTransform.anchorMax = new Vector2(.98f, 1f); runLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
            runIcons[i] = UiPictograms.Icon(runRows[i].transform, "Run Symbol " + i, null, Amber, .015f, .16f, .06f, .68f);
            // 진행 막대: 남은 시간(부풀리기), 쓴 예산(종가 관리), 넘긴 수량(넘기기). 벽은 꽉 찬 막대(걸려 있음).
            var track = new GameObject("Image :: Run Track " + i, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
            var trackRect = (RectTransform)track.transform; trackRect.SetParent(runRows[i].transform, false);
            trackRect.anchorMin = new Vector2(.09f, .1f); trackRect.anchorMax = new Vector2(.98f, .18f); trackRect.offsetMin = trackRect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = HeatOff; track.GetComponent<Image>().raycastTarget = false;
            var bar = new GameObject("Image :: Run Progress " + i, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
            var barRect = (RectTransform)bar.transform; barRect.SetParent(trackRect, false);
            barRect.anchorMin = Vector2.zero; barRect.anchorMax = Vector2.one; barRect.offsetMin = barRect.offsetMax = Vector2.zero;
            runBars[i] = bar.GetComponent<Image>(); runBars[i].color = Amber; runBars[i].raycastTarget = false;
            runStops[i] = Action("Run Stop " + i, "×", 556, y, 50, 44, () => StopRun(index), false, false, 24);
            // 진행 중 줄은 눌러서 그 종목으로 가는 목록 줄이고, ×는 작전을 멈추는 위험 동작이다.
            TabletControlStyle.Row(runRows[i], false); TabletControlStyle.Danger(runStops[i]);
            runRows[i].gameObject.SetActive(false); runStops[i].gameObject.SetActive(false);
        }
        runHeading.gameObject.SetActive(false);

        var divider = new GameObject("Image :: Tactics Divider", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        divider.GetComponent<Image>().color = TabletKitStyle.Line; divider.GetComponent<Image>().raycastTarget = false;
        TradingOrderView.Place((RectTransform)divider.transform, panel, 632, 88, 1, 760, W, H);

        // 오른쪽: 고른 도구.
        // 오른쪽 머리: 상징 그림 + 이름 + 한 줄 용도.
        // 오른쪽 머리: 고른 작전의 엠블럼을 크게(시세에 무엇을 하는지), 그 옆에 이름과 한 줄 용도.
        toolSymbol = UiPictograms.Icon(panel, "Tool Title Emblem", UiPictograms.Emblem(Order[0]), Amber, 660f / W, 74f / H, 230f / W, 115f / H);
        toolTitle = Text("Tool Title", 910, 86, 640, 48, 38);
        purpose = Text("Tool Purpose", 910, 136, 658, 34, 22); purpose.color = Muted;
        toolLock = Text("Tool Lock", 1060, 40, 508, 40, 22); toolLock.color = TabletKitStyle.Accent; toolLock.alignment = TextAlignmentOptions.MidlineRight;
        var frame = new GameObject("Image :: Tactic Diagram", typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
        frame.GetComponent<Image>().color = new Color32(8, 15, 20, 255); frame.GetComponent<Image>().raycastTarget = false;
        TradingOrderView.Place((RectTransform)frame.transform, panel, 660, 200, 908, 256, W, H);
        var inner = new GameObject("Area :: Tactic Diagram Inner", typeof(RectTransform)) { layer = gameObject.layer };
        var innerRect = (RectTransform)inner.transform; innerRect.SetParent(frame.transform, false);
        innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one; innerRect.offsetMin = new Vector2(20f, 12f) * scale; innerRect.offsetMax = new Vector2(-20f, -12f) * scale;
        diagram = TacticDiagram.Create(innerRect, font, scale);
        // 그림 밑의 한 문장: 이걸 누르면 무슨 일이 생기는지(금융 용어 없이, 실제 값으로).
        sentence = Text("Tool Sentence", 660, 464, 908, 60, 21); sentence.textWrappingMode = TextWrappingModes.Normal; sentence.alignment = TextAlignmentOptions.TopLeft;
        sizeLabel = Label("Cash Label", "한 번에 쓸 비중", 660, 528, 236, 44, 23); sizeLabel.color = Muted;
        sizeShare = Text("Size Share", 904, 528, 96, 44, 26); sizeShare.color = TabletKitStyle.Accent; TradingOrderView.Numeric(sizeShare); sizeShare.alignment = TextAlignmentOptions.MidlineLeft;
        sizeSlider = ShareSlider("Size Share", 1010, 528, 558, 44, 1, 100, Mathf.RoundToInt(fraction * 100f), value => { fraction = value / 100f; next = 0f; });
        for (int i = 0; i < 2; i++)
        {
            float y = 590 + i * 46;
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
    /// <summary>고른 도구의 단추 하나: 한 번 쓰는 도구는 실행하고, 켜 두는 도구는 지금 보는 종목에서 켜거나 끈다.</summary>
    /// <summary>알림 문장이 어느 작전의 것인지(문장 속 낱말로). 모르면 null.</summary>
    private static PlayerTactics.Tool? NoticeTool(string korean)
    {
        if (string.IsNullOrEmpty(korean)) { return null; }
        if (korean.Contains("매수벽")) { return PlayerTactics.Tool.Wall; }
        if (korean.Contains("종가")) { return PlayerTactics.Tool.Close; }
        if (korean.Contains("부풀리기")) { return PlayerTactics.Tool.Wash; }
        if (korean.Contains("넘기기") || korean.Contains("넘길")) { return PlayerTactics.Tool.Unload; }
        if (korean.Contains("내려찍") || korean.Contains("던질")) { return PlayerTactics.Tool.Slam; }
        if (korean.Contains("불을")) { return PlayerTactics.Tool.Ignite; }
        return null;
    }
    private void Fire()
    {
        var tactics = PlayerTactics.Instance; var stock = Current;
        if (tactics == null || stock == null) { return; }
        var tool = Order[selected];
        switch (tool)
        {
            case PlayerTactics.Tool.Ignite: tactics.Ignite(stock, fraction); break;
            case PlayerTactics.Tool.Slam: tactics.Slam(stock, fraction); break;
            case PlayerTactics.Tool.Unload: if (tactics.Running(tool, stock)) { tactics.StopUnload(stock); } else { tactics.StartUnload(stock, fraction); } break;
            case PlayerTactics.Tool.Close: if (tactics.Running(tool, stock)) { tactics.CancelClose(stock); } else { tactics.ArmClose(stock, fraction); } break;
            case PlayerTactics.Tool.Wash: if (tactics.Running(tool, stock)) { tactics.StopWash(stock); } else { tactics.StartWash(stock); } break;
            default: if (tactics.Running(tool, stock)) { tactics.PullWall(stock); } else { tactics.SetWall(stock, fraction); } break;
        }
        next = 0f;
    }
    /// <summary>진행 중 목록의 줄을 누르면 그 종목과 그 도구로 간다.</summary>
    private void Jump(int row)
    {
        var run = rowRuns[row]; var manager = StockManager.Instance;
        if (run == null || manager == null) { return; }
        int index = manager.stocks.IndexOf(run.Stock);
        if (index >= 0) { manager.SelectStock(index); }
        selected = IndexOf(run.Tool); next = 0f;
    }
    /// <summary>진행 중 목록의 ×: 그 작전 하나만 멈춘다.</summary>
    private void StopRun(int row)
    {
        var tactics = PlayerTactics.Instance;
        if (tactics != null && rowRuns[row] != null) { tactics.Stop(rowRuns[row]); }
        next = 0f;
    }
    private void Metric(int row, string caption, string value, string unit)
    { Lit(metricCaptions[row], T(caption)); Lit(metricValues[row], value); Lit(metricUnits[row], T(unit)); }
    /// <summary>금액을 단위와 함께 적는다("1,200돈", "1,200 DON").</summary>
    private static string Money(decimal value) => value.ToString("N0") + (GameLocale.Code == "ko" ? "돈" : " " + T("돈"));
    /// <summary>번 돈은 오르는 색, 잃은 돈은 내리는 색, 0은 흐린 색.</summary>
    private static Color Tone(decimal value) => value > 0m ? TabletKitStyle.Up : value < 0m ? TabletKitStyle.Down : Muted;
    /// <summary>진행 중 목록에 적는 숫자 하나: 부풀린 거래대금, 벽을 건 값, 종가 관리에 쓴(아직 안 썼으면 쓸) 돈.</summary>
    private static string RunNumber(PlayerTactics.Run run) =>
        run.Tool == PlayerTactics.Tool.Wash ? Money(run.Painted) : run.Tool == PlayerTactics.Tool.Unload ? run.Spent.ToString("N0") + " / " + run.Budget.ToString("N0") : run.Tool == PlayerTactics.Tool.Wall ? Money(run.Price) : Money(run.Spent > 0m ? run.Spent : run.Budget);

    /// <summary>진행 중 줄의 막대(0~1): 부풀리기는 남은 시간, 넘기기는 판 몫, 종가 관리는 쓴 예산, 벽과 받치기는 걸려 있는 동안 꽉 참.</summary>
    private static float RunProgress(PlayerTactics.Run run) => run.Tool switch
    {
        PlayerTactics.Tool.Wash => Mathf.Clamp01(run.Left / PlayerTactics.WashSeconds),
        PlayerTactics.Tool.Unload => run.Budget > 0m ? Mathf.Clamp01((float)(run.Spent / run.Budget)) : 0f,
        PlayerTactics.Tool.Close => run.Budget > 0m ? Mathf.Max(.04f, Mathf.Clamp01((float)(run.Spent / run.Budget))) : 0f,
        PlayerTactics.Tool.Ignite => Mathf.Clamp01((run.Until - SessionClock.Time) / PlayerTactics.IgniteHoldSeconds),
        _ => 1f
    };
    private RectTransform Area(string name, float x, float y, float w, float h)
    {
        var rect = new GameObject("Area :: " + name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = gameObject.layer; TradingOrderView.Place(rect, panel, x, y, w, h, W, H); return rect;
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
        if (logoStock != stock) { logoStock = stock; UiPictograms.Logo(logo, stock, Ground); }

        // 이 종목의 보유와 손익.
        var book = tactics.Book(stock);
        decimal owned = account.GetOwnedVolume(stock), average = account.GetOwnedAverage(stock);
        decimal floating = owned > 0m ? decimal.Round(owned * (stock.currentPrice - average)) : 0m, made = decimal.Round(book.Realized);
        Lit(holding, owned > 0m ? string.Format(T("보유 {0}주 · 평균단가 {1}"), owned.ToString("N0"), average.ToString("N0")) : T("보유 없음"));
        Lit(paper, T("평가손익") + " " + InvestmentDashboard.Signed(floating)); paper.color = Tone(floating);
        Lit(earned, T("이 종목으로 번 돈") + " " + InvestmentDashboard.Signed(made)); earned.color = Tone(made);
        float warmth = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
        bool rush = warmth >= MothPool.RallyHeat;
        Lit(heat, string.Format(T("열기 {0} / 5"), warmth.ToString("0.0"))); heat.color = rush ? Amber : Ink;
        for (int i = 0; i < heatCells.Length; i++)
        {
            // 다 찬 칸은 호박색, 차는 중인 칸은 그만큼 옅게.
            float part = Mathf.Clamp01(warmth - i);
            heatCells[i].color = part <= 0f ? HeatOff : Color.Lerp(HeatOff, Amber, .35f + .65f * part);
        }
        Lit(heatHint, T(rush ? "급등을 쫓는 사람들(불나방)이 몰려오는 중입니다" : "열기가 5가 되면 급등을 쫓는 사람들(불나방)이 몰려옵니다")); heatHint.color = rush ? Amber : Muted;

        for (int i = 0; i < Order.Length; i++)
        {
            bool open = PlayerTactics.Unlocked(Order[i]), here = open && tactics.Running(Order[i], stock);
            int elsewhere = tactics.Count(Order[i]) - (here ? 1 : 0);
            TabletControlStyle.Row(tools[i], i == selected);
            toolNames[i].color = i == selected ? TabletKitStyle.Accent : !open ? Muted : Ink;
            // 상징 그림: 고른 것은 청록, 이 종목에서 돌고 있으면 호박색, 잠긴 것은 흐리게.
            toolIcons[i].color = i == selected ? TabletKitStyle.Accent : here ? Amber : !open ? HeatOff : (Color)new Color32(176, 190, 202, 255);
            // 이 종목에서 돌고 있으면 "실행 중", 다른 종목에서도 돌고 있으면 그 수를 작게 붙인다.
            string state = !open ? T("잠김") : here ? T("실행 중") + (elsewhere > 0 ? " +" + elsewhere : "") : elsewhere > 0 ? string.Format(T("{0}종목"), elsewhere) : "";
            Lit(toolStates[i], state); toolStates[i].color = here ? TabletKitStyle.Accent : Muted;
        }

        // 진행 중 목록: 최근에 시작한 것이 위. 없으면 제목도 숨긴다.
        var runs = tactics.Runs; int total = runs.Count;
        if (runHeading.gameObject.activeSelf != total > 0) { runHeading.gameObject.SetActive(total > 0); }
        Lit(runMore, total > RunRows ? "+" + (total - RunRows) : "");
        for (int i = 0; i < RunRows; i++)
        {
            var item = i < total ? runs[total - 1 - i] : null;
            rowRuns[i] = item;
            bool on = item != null;
            if (runRows[i].gameObject.activeSelf != on) { runRows[i].gameObject.SetActive(on); runStops[i].gameObject.SetActive(on); }
            if (!on) { continue; }
            Lit(runLabels[i], GameLocale.Text(item.Stock.stockName) + " · " + T(Names[IndexOf(item.Tool)]) + " · " + RunNumber(item));
            // 지금 보고 있는 종목·도구의 줄만 밝게.
            runLabels[i].color = item.Stock == stock && item.Tool == Order[selected] ? Ink : Muted;
            runIcons[i].sprite = UiPictograms.OpIcon(item.Tool);
            var bar = runBars[i].rectTransform; bar.anchorMax = new Vector2(RunProgress(item), 1f);
        }

        var tool = Order[selected]; var run = tactics.Find(tool, stock);
        bool usable = PlayerTactics.Unlocked(tool), active = run != null;
        Lit(toolTitle, T(Names[selected])); Lit(toolLock, usable ? "" : T(PlayerTactics.LockReason(tool)));
        toolSymbol.sprite = UiPictograms.Emblem(tool); toolSymbol.color = active ? Amber : TabletKitStyle.Accent;
        Lit(purpose, T(Purposes[selected]));
        diagram.Show(tool, stock, account, run, fraction);
        Lit(sentence, diagram.Sentence); sentence.color = diagram.Possible ? Ink : (Color)Amber;
        // 거래량 부풀리기는 비중을 고르지 않는다(알아서 조금씩 주고받는다).
        bool sized = tool != PlayerTactics.Tool.Wash && !active;
        if (sizeLabel.gameObject.activeSelf != sized) { sizeLabel.gameObject.SetActive(sized); sizeSlider.gameObject.SetActive(sized); sizeShare.gameObject.SetActive(sized); }
        Lit(sizeShare, Mathf.RoundToInt(fraction * 100f) + "%");
        decimal free = System.Math.Max(0m, account.cash - account.ReservedCash), held = account.GetHolding(stock), share = (decimal)fraction;
        // 고른 도구에 필요한 숫자 두 줄만 보여 준다. 돌고 있으면 그 실행의 숫자, 한 번 쓰는 도구는 이 종목에서 지금까지 한 일.
        if (tool == PlayerTactics.Tool.Ignite) { Metric(0, "쓸 돈 · +" + (PlayerTactics.IgniteReachFor(fraction) * 100m).ToString("0") + "%까지",decimal.Floor(free * share).ToString("N0"), "돈"); Metric(1, "이 종목에서 산 수량", book.IgniteShares.ToString("N0"), "주"); }
        else if (tool == PlayerTactics.Tool.Slam) { Metric(0, "팔 수량 · -" + (PlayerTactics.SlamDepthFor(fraction) * 100m).ToString("0") + "%까지",decimal.Floor(held * share).ToString("N0"), "주"); Metric(1, "이 종목에서 판 금액", decimal.Round(book.SlamCash).ToString("N0"), "돈"); }
        else if (tool == PlayerTactics.Tool.Unload && active) { Metric(0, "넘긴 수량", run.Spent.ToString("N0") + " / " + run.Budget.ToString("N0"), "주"); Metric(1, "남은 시간", Mathf.Max(0f, run.Until - SessionClock.Time).ToString("0"), "초"); }
        else if (tool == PlayerTactics.Tool.Unload) { Metric(0, "넘길 수량", decimal.Floor(held * share).ToString("N0"), "주"); Metric(1, "이 종목에서 넘긴 수량", book.UnloadShares.ToString("N0"), "주"); }
        else if (tool == PlayerTactics.Tool.Wash && active) { Metric(0, "부풀린 거래대금", run.Painted.ToString("N0"), "돈"); Metric(1, "남은 시간", run.Left.ToString("0"), "초"); }
        else if (tool == PlayerTactics.Tool.Wash) { Metric(0, "보유 수량", held.ToString("N0"), "주"); Metric(1, "이 종목에서 부풀린 금액", book.WashPainted.ToString("N0"), "돈"); }
        else if (tool == PlayerTactics.Tool.Close && active) { Metric(0, "종가 관리 예산", run.Budget.ToString("N0"), "돈"); Metric(1, "종가 관리 사용", run.Spent.ToString("N0"), "돈"); }
        else if (tool == PlayerTactics.Tool.Wall && active) { Metric(0, "벽 가격", run.Price.ToString("N0"), "돈"); Metric(1, "벽에 걸린 돈", tactics.Shown(run).ToString("N0"), "돈"); }
        else { Metric(0, "쓸 돈", decimal.Floor(free * share).ToString("N0"), "돈"); Metric(1, "가용 현금", free.ToString("N0"), "돈"); }
        // 멈추는 단추는 언제나 누를 수 있다. 시작하는 단추는 도식이 "지금 할 수 있다"고 할 때만(못 하는 까닭은 도식 밑 문장에 있다).
        act.interactable = usable && (active || diagram.Possible); TabletControlStyle.Emphasize(act, true);
        string verb = !usable ? "잠김" : tool == PlayerTactics.Tool.Ignite || tool == PlayerTactics.Tool.Slam ? "실행"
            : tool == PlayerTactics.Tool.Close ? (active ? "취소" : "예약") : tool == PlayerTactics.Tool.Unload ? (active ? "중지" : "시작") : tool == PlayerTactics.Tool.Wash ? (active ? "중지" : "시작") : active ? "거두기" : "세우기";
        // 단추에 작전 이름을 함께 적는다("불붙이기 실행"). 무엇을 누르는지 헷갈리지 않게.
        // 영어는 동사가 앞("Run Ignite"), 한국어·일본어는 뒤("불붙이기 실행").
        Lit(actLabel, !usable ? T(verb) : GameLocale.Code == "en" ? T(verb) + " " + T(Names[selected]) : T(Names[selected]) + " " + T(verb));
        // 마지막으로 일어난 일. 다른 종목의 일이면 종목 이름을 앞에 붙인다.
        // 마지막으로 일어난 일. 지금 보는 종목과 작전의 일이면 그대로, 아니면 어느 종목의 어느 작전 일인지 앞에 붙인다
        // (불붙이기를 보고 있는데 다른 종목의 "매수벽을 거뒀습니다"가 이름 없이 떠서 헷갈렸다).
        string raw = tactics.Notice ?? "", note = T(raw);
        if (note.Length > 0)
        {
            var about = tactics.NoticeStock; var noticeTool = NoticeTool(raw);
            bool here = about == null || about == stock;
            bool sameTool = noticeTool == null || noticeTool == tool;
            if (!here || !sameTool)
            {
                string who = about != null ? GameLocale.Text(about.stockName) : "";
                string what = noticeTool != null ? T(Names[IndexOf(noticeTool.Value)]) : "";
                note = (who.Length > 0 ? who : "") + (who.Length > 0 && what.Length > 0 ? " · " : "") + what + (who.Length + what.Length > 0 ? ": " : "") + note;
            }
            notice.color = here && sameTool ? TabletKitStyle.Accent : Muted;
        }
        Lit(notice, note);
    }

    private static void Lit(TextMeshProUGUI label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); }
    }
    /// <summary>비율을 고르는 슬라이더(스프레드·매집 화면과 같은 모양). 값은 정수(%)다.</summary>
    private Slider ShareSlider(string name, float x, float y, float w, float h, int min, int max, int start, System.Action<int> changed)
    {
        var root = new GameObject("Area :: " + name + " Slider", typeof(RectTransform), typeof(Image), typeof(Slider)) { layer = gameObject.layer };
        TradingOrderView.Place((RectTransform)root.transform, panel, x, y, w, h, W, H);
        root.GetComponent<Image>().color = Color.clear;
        RectTransform Part(string part, float bottom, float top, Color tone, bool image)
        {
            var item = image ? new GameObject("Image :: " + name + " " + part, typeof(RectTransform), typeof(Image)) : new GameObject("Area :: " + name + " " + part, typeof(RectTransform));
            item.layer = gameObject.layer; var rect = (RectTransform)item.transform; rect.SetParent(root.transform, false);
            rect.anchorMin = new Vector2(0f, bottom); rect.anchorMax = new Vector2(1f, top); rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (image) { var picture = item.GetComponent<Image>(); picture.color = tone; picture.raycastTarget = false; }
            return rect;
        }
        Part("Track", .42f, .58f, TabletKitStyle.Line, true);
        var fillArea = Part("Fill Area", .42f, .58f, Color.clear, false); var handleArea = Part("Handle Area", .2f, .8f, Color.clear, false);
        var fill = Part("Fill", 0f, 1f, TabletKitStyle.Accent, true); fill.SetParent(fillArea, false); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
        var handle = Part("Handle", 0f, 1f, Ink, true); handle.SetParent(handleArea, false); handle.sizeDelta = new Vector2(18f * scale, 0f); handle.GetComponent<Image>().raycastTarget = true;
        var slider = root.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true; slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.SetValueWithoutNotify(Mathf.Clamp(start, min, max));
        slider.onValueChanged.AddListener(value => changed(Mathf.RoundToInt(value)));
        return slider;
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
