using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 거래소의 "수수료 0% 이벤트". 가끔 하루 동안 매도 수수료를 받지 않는다.
//  - 언제: 한 주(장날 5일)에 한 번쯤(주마다 65%). 그 주의 어느 날인지는 판의 고정 시드로 정한다. 첫 주는 3일째부터만 열린다.
//    날짜만으로 정해지므로 따로 저장하지 않는다(저장을 불러와도 같은 날이다).
//  - 그날: 아무도 수수료를 내지 않는다(Trader.SellFeeRate가 0). 세력들은 높은 확률로 즉흥 작전을 벌이고(WhaleBot),
//    봇들은 08:30부터 이벤트 얘기로 웅성거린다(InvestorSociety.BeginFeeHolidayTalk).
//  - 플레이어에게: 전날 오후에 예고 문자가, 당일 08:30에 시작 문자가 온다. 그날은 화면 위에 작은 표시가 떠 있고, 주문 화면의 수수료가 0%로 적힌다.
// 시세·계좌는 건드리지 않는다. 수수료율과 알림만 다룬다.
public sealed class FeeHoliday : MonoBehaviour
{
    public static FeeHoliday Instance { get; private set; }
    /// <summary>한 주에 이벤트가 열릴 확률(%).</summary>
    public const int WeekChance = 65;
    /// <summary>예고 문자가 오는 시각(전날, 자정부터 센 분)과 이벤트가 시작되는 시각(당일).</summary>
    public const int NoticeMinute = 14 * 60, StartMinute = 8 * 60 + 30;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>day(장날 번호)가 수수료 0% 이벤트 날인가.</summary>
    public static bool IsEventDay(int day)
    {
        // 점검용: 환경 변수로 날짜를 못 박는다.
        string forced = Environment.GetEnvironmentVariable("DONDON_FEE_HOLIDAY");
        if (!string.IsNullOrEmpty(forced) && int.TryParse(forced, out int fixedDay)) { return day == fixedDay; }
        if (day < 3) { return false; }
        int seed = StockManager.Instance != null && StockManager.Instance.economy != null ? StockManager.Instance.economy.seed : 38173;
        int week = (day - 1) / 5;
        uint mixed = unchecked((uint)CandleHistoryManager.StableSeed(seed, "FEEHOLIDAY:" + week));
        unchecked { mixed ^= mixed >> 16; mixed *= 0x85EBCA6Bu; mixed ^= mixed >> 13; mixed *= 0xC2B2AE35u; mixed ^= mixed >> 16; }
        if (mixed % 100u >= WeekChance) { return false; }
        // 첫 주는 3·4·5일째 가운데 하루, 그다음 주부터는 닷새 가운데 하루.
        int first = week == 0 ? 2 : 0;
        int index = first + (int)(mixed / 100u % (uint)(5 - first));
        return day == week * 5 + index + 1;
    }
    /// <summary>오늘이 이벤트 날인가(수수료가 0인가).</summary>
    public static bool Active => MarketDayController.Instance != null && IsEventDay(MarketDayController.Instance.Day);
    /// <summary>오늘의 이벤트가 시작됐는가(08:30이 지났는가). 화면 표시와 봇들의 대화는 이때부터다.</summary>
    public static bool Started => Instance != null && Instance.startedFor == (MarketDayController.Instance == null ? -1 : MarketDayController.Instance.Day);
    /// <summary>주문 화면에 적는 수수료 글자: 평소 "0.05%", 이벤트 날 "0%".</summary>
    public static string RateText => Active ? "0%" : (Trader.StandardSellFeeRate * 100m).ToString("0.##") + "%";

    private int noticedFor = -1, startedFor = -1;
    private float next;
    private GameObject badgeRoot;
    private Image badgeDot;
    private TextMeshProUGUI badgeText;
    private int locale = -1;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    void Update()
    {
        if (badgeRoot != null && badgeRoot.activeSelf && badgeDot != null)
        {
            // 점 하나가 천천히 밝아졌다 어두워진다. 그 밖에는 움직이지 않는다.
            var tint = badgeDot.color; tint.a = .55f + .45f * Mathf.Sin(Time.unscaledTime * 2.2f); badgeDot.color = tint;
        }
        if (Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .5f;
        var market = MarketDayController.Instance; var views = DeskViewController.Instance;
        if (market == null || !market.Ready || views == null || !views.Ready) { Show(false); return; }
        int day = market.Day; bool open = market.Phase == MarketPhase.PreOpen || market.Phase == MarketPhase.Trading;

        // 전날 오후(또는 그날 장이 끝난 뒤)에 예고 문자가 온다.
        bool afternoon = market.Phase == MarketPhase.Trading ? market.ClockMinute >= NoticeMinute : market.Phase != MarketPhase.PreOpen;
        if (noticedFor != day + 1 && afternoon && IsEventDay(day + 1) && DeskPhone.Instance != null && DeskPhone.Instance.Ready)
        {
            noticedFor = day + 1;
            DeskPhone.Instance.ReceiveNotice(day + 1,
                ("내일 수수료 0% 이벤트", "0% fee event tomorrow", "明日 手数料0%イベント"),
                ("내일 하루 거래 수수료가 0%입니다. 거래량이 늘어날 것입니다. 대비하세요.", "Tomorrow the trading fee is 0% all day. Expect heavier volume. Be ready.", "明日は終日、取引手数料が0%です。出来高が増える見込みです。備えてください。"));
            Debug.Log($"[거래소] 수수료 0% 이벤트 예고 — {day + 1}일");
        }
        // 당일 08:30에 시작한다.
        if (startedFor != day && open && IsEventDay(day) && (market.Phase == MarketPhase.Trading || market.ClockMinute >= StartMinute))
        {
            startedFor = day;
            if (DeskPhone.Instance != null && DeskPhone.Instance.Ready)
            {
                DeskPhone.Instance.ReceiveNotice(day,
                    ("수수료 0% 이벤트 시작", "0% fee event is on", "手数料0%イベント開始"),
                    ("오늘 하루 거래 수수료가 0%입니다. 거래량이 늘어날 것입니다. 대비하세요.", "Today the trading fee is 0% all day. Expect heavier volume. Be ready.", "本日は終日、取引手数料が0%です。出来高が増える見込みです。備えてください。"));
            }
            MarketEventFeed.Instance?.Notify(Word("거래소 · 오늘 하루 거래 수수료 0%", "Exchange · 0% trading fee today", "取引所 · 本日は取引手数料0%"));
            InvestorSociety.Instance?.BeginFeeHolidayTalk(day);
            Debug.Log($"[거래소] 수수료 0% 이벤트 시작 — {day}일 {market.ClockMinute / 60:00}:{market.ClockMinute % 60:00}");
        }
        // 휴대폰을 들고 있는 동안에는 표시가 휴대폰 위쪽을 가리므로 잠깐 숨긴다.
        Show(open && startedFor == day && !PauseMenuController.IsOpen && !RoomShell.Cinematic && !DeskPhone.Raised);
    }

    /// <summary>
    /// 이벤트 날임을 알리는 표시. 화면 위 가운데의 작은 알약 하나다: 청록 점과 한 줄의 글.
    /// 번쩍이거나 흔들리지 않는다. 하루 종일 같은 자리에 조용히 떠 있어서, 눈에는 띄지만 방해하지 않는다.
    /// </summary>
    private void Show(bool visible)
    {
        if (!visible) { if (badgeRoot != null && badgeRoot.activeSelf) { badgeRoot.SetActive(false); } return; }
        if (badgeRoot == null) { BuildBadge(); }
        if (!badgeRoot.activeSelf) { badgeRoot.SetActive(true); locale = -1; }
        if (locale != GameLocale.Revision)
        {
            locale = GameLocale.Revision;
            GameLocalizedText.Set(badgeText, Word("수수료 0% 이벤트 · 오늘 하루", "0% fee event · today only", "手数料0%イベント · 本日限り"), true);
        }
    }
    private void BuildBadge()
    {
        badgeRoot = new GameObject("Area :: Fee Holiday", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        badgeRoot.transform.SetParent(transform, false);
        var canvas = badgeRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
        var scaler = badgeRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var pill = new GameObject("Panel :: Fee Holiday", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); pill.SetParent(badgeRoot.transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 1f); pill.pivot = new Vector2(.5f, 1f); pill.anchoredPosition = new Vector2(0f, -18f); pill.sizeDelta = new Vector2(330f, 42f);
        pill.GetComponent<Image>().color = new Color(.04f, .055f, .07f, .90f); pill.GetComponent<Image>().raycastTarget = false;
        // 아래에 가는 청록 줄 하나.
        var rule = new GameObject("Image :: Fee Holiday Rule", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); rule.SetParent(pill, false);
        rule.anchorMin = new Vector2(0f, 0f); rule.anchorMax = new Vector2(1f, 0f); rule.pivot = new Vector2(.5f, 0f); rule.anchoredPosition = Vector2.zero; rule.sizeDelta = new Vector2(0f, 2f);
        rule.GetComponent<Image>().color = new Color(.24f, .90f, .82f, .85f); rule.GetComponent<Image>().raycastTarget = false;
        var dot = new GameObject("Image :: Fee Holiday Dot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); dot.SetParent(pill, false);
        dot.anchorMin = dot.anchorMax = new Vector2(0f, .5f); dot.pivot = new Vector2(.5f, .5f); dot.anchoredPosition = new Vector2(24f, 1f); dot.sizeDelta = new Vector2(10f, 10f);
        badgeDot = dot.GetComponent<Image>(); badgeDot.color = new Color(.24f, .90f, .82f, 1f); badgeDot.raycastTarget = false;
        var text = new GameObject("Text :: Fee Holiday", typeof(RectTransform)).GetComponent<RectTransform>(); text.SetParent(pill, false);
        text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one; text.offsetMin = new Vector2(42f, 2f); text.offsetMax = new Vector2(-14f, 0f);
        badgeText = text.gameObject.AddComponent<TextMeshProUGUI>();
        badgeText.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        badgeText.fontSize = 20f; badgeText.enableAutoSizing = false; badgeText.color = new Color(.90f, .94f, .95f, 1f); badgeText.alignment = TextAlignmentOptions.MidlineLeft;
        badgeText.textWrappingMode = TextWrappingModes.NoWrap; badgeText.overflowMode = TextOverflowModes.Overflow; badgeText.raycastTarget = false;
        badgeRoot.SetActive(false);
    }
}
