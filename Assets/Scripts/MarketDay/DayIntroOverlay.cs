using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 새 날이 시작될 때의 연출.
// 뒤에 흐림이 서서히 깔리면서 큰 "DAY N" 글자가 부드럽게 나타나 줄어들고, 흐림이 풀리면서 글자도 사라진다.
// 화면 위에 그리기만 한다. 입력을 막지 않고 게임 시간도 멈추지 않는다.
public sealed class DayIntroOverlay : MonoBehaviour
{
    public static DayIntroOverlay Instance { get; private set; }
    /// <summary>나타나는 시간, 머무는 시간, 사라지는 시간(초). 실제 시간 기준이다.</summary>
    private const float AppearSeconds = 1.0f, HoldSeconds = 1.3f, LeaveSeconds = 1.5f, RecaptureSeconds = .2f;
    private const float StartScale = 2.6f, RestScale = 1.12f, EndScale = .8f, StartSpacing = 60f, RestSpacing = 10f;
    public GameObject Root { get; private set; }
    private CanvasGroup blurGroup, textGroup;
    private RawImage blur;
    private Image shade;
    private TextMeshProUGUI title;
    private MenuBackdrop backdrop;
    private int shownDay = -1;
    private float started = -1f, nextCapture;
    public bool Playing => started >= 0f;

    void Awake()
    {
        Instance = this;
        Root = new GameObject("Area :: Day Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Root.transform.SetParent(transform, false);
        var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29000;
        var scaler = Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;

        var blurArea = Layer("Area :: Day Intro Blur"); blurGroup = blurArea.gameObject.AddComponent<CanvasGroup>(); blurGroup.blocksRaycasts = false; blurGroup.interactable = false;
        blur = Layer("Image :: Blurred Game", blurArea).gameObject.AddComponent<RawImage>(); blur.raycastTarget = false; backdrop = new MenuBackdrop(blur);
        shade = Layer("Image :: Day Intro Shade", blurArea).gameObject.AddComponent<Image>(); shade.color = new Color(0f, 0f, 0f, .38f); shade.raycastTarget = false;

        var textArea = Layer("Area :: Day Intro Title"); textGroup = textArea.gameObject.AddComponent<CanvasGroup>(); textGroup.blocksRaycasts = false; textGroup.interactable = false;
        title = Layer("Text :: Day Intro", textArea).gameObject.AddComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        title.font = font; title.fontSize = 170f; title.enableAutoSizing = false; title.fontStyle = FontStyles.Bold; title.color = new Color(.95f, .97f, .98f, 1f);
        title.alignment = TextAlignmentOptions.Center; title.textWrappingMode = TextWrappingModes.NoWrap; title.overflowMode = TextOverflowModes.Overflow; title.raycastTarget = false;
        Root.SetActive(false);
    }
    private RectTransform Layer(string name, Transform parent = null)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent == null ? Root.transform : parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    /// <summary>지금 바로 "DAY N" 연출을 시작한다.</summary>
    public void Play(int day)
    {
        shownDay = day;
        GameLocalizedText.Set(title, "DAY " + day, true);
        Root.SetActive(true); started = Time.unscaledTime; nextCapture = 0f;
        Apply(0f);
    }

    void Update()
    {
        var market = MarketDayController.Instance;
        if (!Playing)
        {
            // 날짜가 바뀌면(첫날 포함) 시작한다. 메뉴가 떠 있는 동안은 닫힐 때까지 기다린다.
            if (market == null || !market.Ready || (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed) || PauseMenuController.IsOpen || market.ReportPending) { return; }
            // 첫날은 08:30부터 둘러보다가 장이 열리는 09:00에 "DAY 1"이 뜬다. 둘째 날부터는 일어난 08:00에 뜬다.
            if (market.Day == 1 && market.Phase == MarketPhase.PreOpen) { return; }
            if (market.Day != shownDay) { Play(market.Day); }
            return;
        }
        float time = Time.unscaledTime - started;
        if (time >= AppearSeconds + HoldSeconds + LeaveSeconds) { started = -1f; Root.SetActive(false); return; }
        // 뒤의 게임은 계속 움직인다. 흐린 그림을 짧은 간격으로 다시 떠서 멈춘 화면처럼 보이지 않게 한다.
        if (Time.unscaledTime >= nextCapture) { nextCapture = Time.unscaledTime + RecaptureSeconds; Root.SetActive(false); backdrop.Capture(); Root.SetActive(true); }
        Apply(time);
    }

    private void Apply(float time)
    {
        float appear = Mathf.Clamp01(time / AppearSeconds), leave = Mathf.Clamp01((time - AppearSeconds - HoldSeconds) / LeaveSeconds);
        float hold = Mathf.Clamp01((time - AppearSeconds) / HoldSeconds);
        // 나타날 때는 빠르게 시작해 부드럽게 멈추고, 사라질 때는 천천히 시작한다.
        float eased = 1f - Mathf.Pow(1f - appear, 3f), fade = leave * leave * (3f - 2f * leave);
        blurGroup.alpha = Mathf.Min(appear * appear * (3f - 2f * appear), 1f - fade);
        textGroup.alpha = Mathf.Min(Mathf.Clamp01(appear * 1.4f), 1f - fade);
        float scale = Mathf.Lerp(StartScale, RestScale, eased);
        scale = Mathf.Lerp(scale, 1f, hold * .9f);
        scale = Mathf.Lerp(scale, EndScale, fade);
        title.rectTransform.localScale = new Vector3(scale, scale, 1f);
        title.characterSpacing = Mathf.Lerp(Mathf.Lerp(StartSpacing, RestSpacing, eased), 0f, fade);
    }

    void OnDestroy()
    {
        backdrop?.Dispose();
        if (Instance == this) { Instance = null; }
    }
}
