using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자고 일어난 아침의 브리핑. 자는 동안 나온 기사를 한 장에 모아 호재·악재와 예상 등락을 보여 준다.
/// 기사를 지어내지 않는다. 자는 사이에 실제로 발행된 기사만 모으고, 예상 등락은 그 기사가 기업 가치에 주는 배율 그대로다.
/// 화면 왼쪽 위에 떠 있다가 장이 열리고 조금 지나면 사라진다. 조작을 막지 않는다.
/// </summary>
public sealed class MorningBriefing : MonoBehaviour
{
    public static MorningBriefing Instance { get; private set; }
    /// <summary>한 장에 보여 주는 기사 수. 많이 움직일 것부터 고른다.</summary>
    private const int Rows = 8;
    /// <summary>일어난 뒤 기사가 모이기를 기다리는 시간, 장이 열린 뒤에도 남겨 두는 시간(실제 초).</summary>
    private const float GatherSeconds = 4.2f;

    public sealed class Entry
    {
        public Stock Stock;
        public string Title;
        public float Critical;
        public bool Fabricated;
        /// <summary>예상 등락(%). 기사가 기업 가치에 주는 배율에서 온다.</summary>
        public float Expected => (Critical - 1f) * 100f;
    }

    private readonly List<Entry> entries = new();
    private readonly Dictionary<long, float> criticals = new();
    private bool collecting;
    private float showAt = -1f, hideAt = -1f;
    private GameObject root;
    private CanvasGroup group;
    // 화면 가득 한 장: 가운데 기준선에서 종목마다 막대가 위(호재)나 아래(악재)로 뻗는다. 막대 길이가 예상 등락이다.
    // 막대 밑에는 그 회사의 이름과, 그 등락을 만든 기사 제목 한 줄이 붙는다.
    private const float ColumnWidth = 184f, BarReach = 140f, Baseline = 390f, NameTop = 620f;
    private TextMeshProUGUI ups, downs, more, key, heading, caption, memoKey;
    private Image memoCap;
    private bool memoSaved;
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[Rows], moves = new TextMeshProUGUI[Rows], titles = new TextMeshProUGUI[Rows];
    private readonly RectTransform[] bars = new RectTransform[Rows], columns = new RectTransform[Rows];
    private readonly Image[] fills = new Image[Rows], hollows = new Image[Rows];
    private RectTransform baseline, sheet;
    public IReadOnlyList<Entry> Entries => entries;
    public bool Shown => root != null && root.activeSelf;
    /// <summary>브리핑이 화면을 덮고 있는 동안은 방 안의 조작을 받지 않는다.</summary>
    public static bool Blocking => Instance != null && Instance.Shown && Instance.entries.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }

    void Awake() { Instance = this; Build(); }
    void OnEnable() { NewsManager.OnMarketNews += OnNews; NewsManager.OnArticlePublished += OnArticle; }
    void OnDisable() { NewsManager.OnMarketNews -= OnNews; NewsManager.OnArticlePublished -= OnArticle; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>잠들었다. 지금부터 나오는 기사를 아침 브리핑에 모은다.</summary>
    public void Begin()
    {
        entries.Clear(); criticals.Clear(); collecting = true;
        showAt = Time.unscaledTime + GatherSeconds; hideAt = -1f;
        if (root != null) { root.SetActive(false); }
    }

    private void OnNews(Stock stock, long id, float critical) { if (collecting) { criticals[id] = critical; } }

    private void OnArticle(NewsArticle article)
    {
        if (!collecting || article == null || StockManager.Instance == null) { return; }
        var stock = StockManager.Instance.stocks.Find(item => item != null && item.symbol == article.symbol);
        if (stock == null || !criticals.TryGetValue(article.id, out float critical)) { return; }
        entries.Add(new Entry { Stock = stock, Title = article.title, Critical = critical, Fabricated = article.fabricated });
    }

    private void Build()
    {
        root = new GameObject("Area :: Morning Briefing", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)); root.transform.SetParent(transform, false);
        // "DAY N" 연출(29000) 바로 위. 그 연출이 끝난 뒤에 뜬다.
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29050;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        var shade = Box(root.transform, "Image :: Briefing Shade", new Color(.02f, .03f, .045f, .94f)); shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        // 기준 해상도 1600×900의 판을 화면 가운데에 둔다. 그 안에서 자리를 잡는다.
        sheet = new GameObject("Area :: Briefing Sheet", typeof(RectTransform)).GetComponent<RectTransform>(); sheet.SetParent(root.transform, false);
        sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f); sheet.sizeDelta = new Vector2(1600, 900); sheet.anchoredPosition = Vector2.zero;
        // 머리: 신문 아이콘과, 호재·악재 개수(화살표와 숫자).
        var paper = Box(sheet, "Image :: Briefing Icon", new Color(.94f, .96f, .97f)); Pin(paper, 80, 54, 56, 56); paper.GetComponent<Image>().sprite = TabletKitStyle.Icon("news"); paper.GetComponent<Image>().preserveAspect = true;
        var upIcon = Box(sheet, "Image :: Briefing Up", MarketColors.Up); Pin(upIcon, 1196, 62, 44, 44); upIcon.GetComponent<Image>().sprite = TabletKitStyle.Icon("growth"); upIcon.GetComponent<Image>().preserveAspect = true;
        ups = Text(sheet, "Briefing Ups", 46, FontStyles.Bold); Pin(ups.rectTransform, 1248, 52, 90, 64); TradingOrderView.Numeric(ups);
        var downIcon = Box(sheet, "Image :: Briefing Down", MarketColors.Down); Pin(downIcon, 1346, 62, 44, 44); downIcon.GetComponent<Image>().sprite = TabletKitStyle.Icon("loss"); downIcon.GetComponent<Image>().preserveAspect = true;
        downs = Text(sheet, "Briefing Downs", 46, FontStyles.Bold); Pin(downs.rectTransform, 1398, 52, 90, 64); TradingOrderView.Numeric(downs);
        more = Text(sheet, "Briefing More", 26, FontStyles.Normal); more.color = new Color(.63f, .68f, .73f); Pin(more.rectTransform, 1470, 60, 90, 48);
        // 제목과 부제: 이 그림이 무엇인지 한 줄씩.
        heading = Text(sheet, "Briefing Heading", 42, FontStyles.Bold); Pin(heading.rectTransform, 154, 42, 900, 56);
        caption = Text(sheet, "Briefing Caption", 22, FontStyles.Normal); Pin(caption.rectTransform, 156, 100, 1000, 32); caption.color = new Color(.63f, .68f, .73f);
        baseline = Box(sheet, "Image :: Briefing Baseline", new Color(1, 1, 1, .30f));
        for (int i = 0; i < Rows; i++)
        {
            columns[i] = new GameObject("Area :: Briefing Column " + i, typeof(RectTransform)).GetComponent<RectTransform>(); columns[i].SetParent(sheet, false);
            bars[i] = Box(columns[i], "Image :: Briefing Bar", Color.white); fills[i] = bars[i].GetComponent<Image>();
            // 소문 기사(확인되지 않은 것)는 속이 빈 막대로 그린다.
            var hole = Box(bars[i], "Image :: Briefing Bar Hollow", new Color(.02f, .03f, .045f, 1f)); hole.anchorMin = Vector2.zero; hole.anchorMax = Vector2.one; hole.offsetMin = new Vector2(6, 6); hole.offsetMax = new Vector2(-6, -6); hollows[i] = hole.GetComponent<Image>();
            moves[i] = Text(columns[i], "Briefing Move", 34, FontStyles.Bold); moves[i].alignment = TextAlignmentOptions.Center; TradingOrderView.Numeric(moves[i]); moves[i].overflowMode = TextOverflowModes.Overflow;
            names[i] = Text(columns[i], "Briefing Stock", 24, FontStyles.Bold); names[i].alignment = TextAlignmentOptions.Top; names[i].enableAutoSizing = true; names[i].fontSizeMax = 24; names[i].fontSizeMin = 16;
            titles[i] = Text(columns[i], "Briefing Title", 17, FontStyles.Normal); titles[i].alignment = TextAlignmentOptions.Top; titles[i].color = new Color(.66f, .71f, .76f);
            titles[i].textWrappingMode = TextWrappingModes.Normal; titles[i].overflowMode = TextOverflowModes.Ellipsis;
        }
        // 닫는 키. 글 대신 자판 모양 하나.
        var cap = Box(sheet, "Image :: Briefing Key", new Color(1, 1, 1, .12f)); Pin(cap, 1380, 800, 140, 44);
        key = Text(cap, "Briefing Key", 20, FontStyles.Bold); key.alignment = TextAlignmentOptions.Center; key.rectTransform.anchorMin = Vector2.zero; key.rectTransform.anchorMax = Vector2.one; key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;
        GameLocalizedText.Set(key, "SPACE", true);
        // 메모 키: 포스트잇 두 장(초록·주황) 그림과 E.
        memoCap = Box(sheet, "Image :: Briefing Memo Key", new Color(1, 1, 1, .12f)).GetComponent<Image>(); Pin(memoCap.rectTransform, 1180, 800, 180, 44);
        var green = Box(memoCap.transform, "Image :: Memo Up", new Color32(168, 214, 130, 255)); Pin(green, 14, 9, 26, 26);
        var orange = Box(memoCap.transform, "Image :: Memo Down", new Color32(240, 170, 110, 255)); Pin(orange, 46, 9, 26, 26);
        memoKey = Text(memoCap.transform, "Briefing Memo Key", 20, FontStyles.Bold); Pin(memoKey.rectTransform, 84, 0, 90, 44); memoKey.alignment = TextAlignmentOptions.Center;
        GameLocalizedText.Set(memoKey, "E", true);
        root.SetActive(false);
    }

    private void Present()
    {
        collecting = false;
        // 조용한 밤에는 아무것도 띄우지 않는다.
        if (entries.Count == 0) { Debug.Log("[아침 브리핑] 밤사이 기사 없음"); return; }
        // 많이 움직일 기사부터.
        entries.Sort((a, b) => Mathf.Abs(b.Expected).CompareTo(Mathf.Abs(a.Expected)));
        int up = 0, down = 0; foreach (var entry in entries) { if (entry.Expected >= 0f) { up++; } else { down++; } }
        GameLocalizedText.Set(heading, Word("밤사이 뉴스", "Overnight news", "夜のあいだのニュース"), true);
        GameLocalizedText.Set(caption, Word("자는 동안 나온 기사와, 그 기사가 주가에 줄 것으로 보이는 영향", "Stories that ran while you slept, and the move each one is expected to cause", "寝ている間に出た記事と、それが株価に与えそうな影響"), true);
        GameLocalizedText.Set(ups, up.ToString(), true); ups.color = MarketColors.Up;
        GameLocalizedText.Set(downs, down.ToString(), true); downs.color = MarketColors.Down;
        int shown = Mathf.Min(Rows, entries.Count);
        float most = 1f; for (int i = 0; i < shown; i++) { most = Mathf.Max(most, Mathf.Abs(entries[i].Expected)); }
        float left = (1600f - shown * ColumnWidth) * .5f;
        for (int i = 0; i < Rows; i++)
        {
            columns[i].gameObject.SetActive(i < shown);
            if (i >= shown) { continue; }
            Pin(columns[i], left + i * ColumnWidth, 0, ColumnWidth, 900);
            var entry = entries[i]; bool good = entry.Expected >= 0f; Color color = good ? MarketColors.Up : MarketColors.Down;
            float length = Mathf.Max(10f, BarReach * Mathf.Abs(entry.Expected) / most);
            // 막대: 기준선에서 위로(호재) 또는 아래로(악재). 숫자는 막대 끝에.
            Pin(bars[i], 52, good ? Baseline - length : Baseline + 3, ColumnWidth - 104, length);
            fills[i].color = color; hollows[i].enabled = entry.Fabricated;
            Pin(moves[i].rectTransform, 0, good ? Baseline - length - 54 : Baseline + length + 8, ColumnWidth, 48);
            GameLocalizedText.Set(moves[i], entry.Expected.ToString("+0.0;-0.0;0.0") + "%", true); moves[i].color = color;
            // 회사 이름과, 그 등락을 만든 기사 제목.
            Pin(names[i].rectTransform, 8, NameTop, ColumnWidth - 16, 34); DeviceTerminalUI.Set(names[i], entry.Stock.stockName);
            Pin(titles[i].rectTransform, 12, NameTop + 40, ColumnWidth - 24, 96); DeviceTerminalUI.Set(titles[i], entry.Title);
        }
        Pin(baseline, left - 20, Baseline, shown * ColumnWidth + 40, 3);
        bool extra = entries.Count > shown; more.gameObject.SetActive(extra);
        if (extra) { GameLocalizedText.Set(more, "+" + (entries.Count - shown), true); }
        root.SetActive(true); group.alpha = 0f; hideAt = -1f;
        memoSaved = false; memoCap.color = new Color(1, 1, 1, .12f); GameLocalizedText.Set(memoKey, "E", true); memoKey.color = new Color(.94f, .96f, .97f);
        var report = new System.Text.StringBuilder("[아침 브리핑] 밤사이 기사 " + entries.Count + "건 (호재 " + up + " · 악재 " + down + ")");
        foreach (var entry in entries) { report.Append(" | ").Append(entry.Stock.symbol).Append(' ').Append(entry.Expected.ToString("+0.0;-0.0")).Append("% ").Append(entry.Fabricated ? "(소문) " : "").Append(entry.Title); }
        Debug.Log(report.ToString());
    }

    // 눈을 뜨고 "DAY N"이 지나간 뒤에 브리핑이 뜬다.
    private static bool IntroDone()
    {
        var intro = DayIntroOverlay.Instance; var day = MarketDayController.Instance;
        return intro == null || day == null || intro.ShownDay == day.Day && !intro.Playing;
    }
    void Update()
    {
        if (showAt >= 0f && Time.unscaledTime >= showAt && !PauseMenuController.IsOpen && !CollapseSleep.Active && IntroDone()) { showAt = -1f; Present(); }
        if (root == null || !root.activeSelf) { return; }
        var day = MarketDayController.Instance;
        // 스페이스·엔터·마우스 클릭으로 닫는다. 장이 열리면 저절로 닫힌다(거래를 가리지 않게).
        bool pressed = group.alpha > .6f && !PauseMenuController.IsOpen && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0));
        // E: 오를 것·내릴 것을 포스트잇 두 장에 적어 모니터 아래에 붙인다. 한 번만.
        if (!memoSaved && hideAt < 0f && group.alpha > .6f && !PauseMenuController.IsOpen && Input.GetKeyDown(KeyCode.E) && MemoNotes.Instance != null && MemoNotes.Instance.Save(entries))
        { memoSaved = true; memoCap.color = TabletKitStyle.Accent; GameLocalizedText.Set(memoKey, "OK", true); memoKey.color = TabletKitStyle.Background; }
        if (hideAt < 0f && (pressed || day != null && day.Phase == MarketPhase.Trading)) { hideAt = Time.unscaledTime; }
        bool leaving = hideAt >= 0f && Time.unscaledTime >= hideAt;
        group.alpha = Mathf.MoveTowards(group.alpha, leaving || PauseMenuController.IsOpen ? 0f : 1f, Time.unscaledDeltaTime * 2.6f);
        if (leaving && group.alpha <= 0f) { root.SetActive(false); hideAt = -1f; }
    }

    private TextMeshProUGUI Text(Transform parent, string name, float size, FontStyles style)
    {
        var rect = new GameObject("Text :: " + name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        label.fontSize = size; label.fontStyle = style; label.enableAutoSizing = false; label.color = new Color(.94f, .96f, .97f); label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false; return label;
    }
    private static RectTransform Box(Transform parent, string name, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
    }
    private static void Pin(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
}
