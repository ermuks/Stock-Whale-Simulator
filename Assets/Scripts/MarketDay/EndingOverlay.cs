using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 최종 목표를 이뤘을 때의 연출.
// 내 기업이 상장하고, 사업 확장을 정해진 횟수만큼 끝내고, 마감 시가총액이 목표를 넘으면 한 번만 나온다.
// 화면 위에 그리기만 한다. 입력을 막지 않고 게임은 그대로 이어진다.
public sealed class EndingOverlay : MonoBehaviour
{
    public static EndingOverlay Instance { get; private set; }
    private const float AppearSeconds = 1.4f, HoldSeconds = 6f, LeaveSeconds = 2.2f, RecaptureSeconds = .2f;
    private static readonly System.Collections.Generic.Dictionary<string, (string en, string ja)> Words = new()
    {
        ["{0} · 시가총액 {1}"] = ("{0} · Market cap {1}", "{0} · 時価総額 {1}"),
        ["{0} 만에 목표 달성 · 게임은 계속됩니다"] = ("Goal reached on {0} · the game goes on", "{0}で目標達成 · ゲームは続きます"),
    };
    private static string T(string korean) => GameLocale.Code == "ko" || !Words.TryGetValue(korean, out var other) ? korean : GameLocale.Code == "ja" ? other.ja : other.en;
    public GameObject Root { get; private set; }
    private CanvasGroup group;
    private RawImage blur;
    private TextMeshProUGUI title, company, note;
    private MenuBackdrop backdrop;
    private float started = -1f, nextCapture;
    public bool Playing => started >= 0f;

    void Awake()
    {
        Instance = this;
        Root = new GameObject("Area :: Ending", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Root.transform.SetParent(transform, false);
        var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29100;
        var scaler = Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        group = Root.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        blur = Layer("Image :: Blurred Game", 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>(); blur.raycastTarget = false; backdrop = new MenuBackdrop(blur);
        var shade = Layer("Image :: Ending Shade", 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>(); shade.color = new Color(0f, 0f, 0f, .55f); shade.raycastTarget = false;
        title = Text("Text :: Ending Title", .30f, .30f, 150f, true); title.characterSpacing = 14f;
        var line = Layer("Image :: Ending Line", .40f, .525f, .20f, .004f).gameObject.AddComponent<Image>(); line.color = new Color32(46, 230, 208, 255); line.raycastTarget = false;
        company = Text("Text :: Ending Company", .56f, .08f, 44f, false);
        note = Text("Text :: Ending Note", .66f, .06f, 28f, false); note.color = new Color32(146, 161, 172, 255);
        Root.SetActive(false);
    }
    private RectTransform Layer(string name, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(Root.transform, false);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y); rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private TextMeshProUGUI Text(string name, float y, float h, float size, bool bold)
    {
        var text = Layer(name, 0f, y, 1f, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.enableAutoSizing = false; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.color = new Color(.95f, .97f, .98f, 1f);
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        return text;
    }

    /// <summary>지금 바로 엔딩 연출을 시작한다.</summary>
    public void Play(string companyName, decimal marketCap, int day)
    {
        GameLocalizedText.Set(title, "ENDING", true);
        GameLocalizedText.Set(company, string.Format(T("{0} · 시가총액 {1}"), companyName, DeviceTerminalUI.Money(marketCap)), true);
        GameLocalizedText.Set(note, string.Format(T("{0} 만에 목표 달성 · 게임은 계속됩니다"), GameLocale.Day(day)), true);
        Root.SetActive(true); started = Time.unscaledTime; nextCapture = 0f; group.alpha = 0f;
    }

    void Update()
    {
        if (!Playing)
        {
            var market = MarketDayController.Instance; var stock = CorporateEconomy.Instance != null ? CorporateEconomy.Instance.PlayerCompany : null;
            var growth = stock?.business?.growth;
            if (growth == null || growth.EndingDay < 1 || growth.EndingShown || market == null || !market.Ready || market.Phase == MarketPhase.Closing || PauseMenuController.IsOpen) { return; }
            if (DayIntroOverlay.Instance != null && DayIntroOverlay.Instance.Playing) { return; }
            growth.EndingShown = true;
            Play(stock.stockName, stock.business.MarketCap, growth.EndingDay);
            return;
        }
        float time = Time.unscaledTime - started;
        if (time >= AppearSeconds + HoldSeconds + LeaveSeconds) { started = -1f; Root.SetActive(false); return; }
        if (Time.unscaledTime >= nextCapture) { nextCapture = Time.unscaledTime + RecaptureSeconds; Root.SetActive(false); backdrop.Capture(); Root.SetActive(true); }
        float appear = Mathf.Clamp01(time / AppearSeconds), leave = Mathf.Clamp01((time - AppearSeconds - HoldSeconds) / LeaveSeconds);
        group.alpha = Mathf.Min(appear * appear * (3f - 2f * appear), 1f - leave * leave * (3f - 2f * leave));
        float scale = Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - appear, 3f));
        title.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    void OnDestroy()
    {
        backdrop?.Dispose();
        if (Instance == this) { Instance = null; }
    }
}
