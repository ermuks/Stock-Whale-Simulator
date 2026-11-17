using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장이 끝난 뒤(잠을 미뤘을 때)의 잠자기.
// 화면 아래 가운데에 "Z 길게 눌러 잠자기"가 떠 있고, Z를 누르고 있으면 눈꺼풀이 위아래에서 감긴다. 다 감기면 아침이다.
// 중간에 손을 떼면 눈이 다시 떠진다. 일시정지 메뉴의 잠자기 단추는 그대로 남아 있다.
public sealed class SleepPrompt : MonoBehaviour
{
    public static SleepPrompt Instance { get; private set; }
    public const KeyCode Key = KeyCode.Z;
    private const float HoldSeconds = 1.6f, OpenSeconds = 1.2f;
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    public GameObject Root { get; private set; }
    private RectTransform upper, lower, fill;
    private CanvasGroup hint;
    private TextMeshProUGUI caption, status;
    private float held, opening;
    /// <summary>눈이 감긴 정도(0~1).</summary>
    public float Closed { get; private set; }

    void Awake()
    {
        Instance = this;
        Root = new GameObject("Area :: Sleep Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Root.transform.SetParent(transform, false);
        var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28900;
        var scaler = Root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;

        // 눈꺼풀: 위아래에서 내려오고 올라오는 검은 판 두 장.
        upper = Panel("Image :: Upper Eyelid", Root.transform, Color.black); lower = Panel("Image :: Lower Eyelid", Root.transform, Color.black);

        // 안내: 화면 아래 가운데의 어두운 알약 모양.
        var pill = Panel("Panel :: Sleep Hint", Root.transform, new Color(.04f, .055f, .07f, .96f));
        pill.anchorMin = new Vector2(.5f, 0f); pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f);
        pill.anchoredPosition = new Vector2(0f, 46f); pill.sizeDelta = new Vector2(430f, 62f);
        hint = pill.gameObject.AddComponent<CanvasGroup>(); hint.blocksRaycasts = false; hint.interactable = false;
        var keycap = Panel("Image :: Keycap", pill, new Color(.90f, .92f, .94f, .95f));
        keycap.anchorMin = keycap.anchorMax = new Vector2(0f, .5f); keycap.pivot = new Vector2(0f, .5f); keycap.anchoredPosition = new Vector2(16f, 0f); keycap.sizeDelta = new Vector2(40f, 40f);
        var key = Label("Text :: Key", keycap, font, 24f, new Color(.06f, .08f, .10f, 1f), TextAlignmentOptions.Center);
        key.text = Key.ToString(); key.fontStyle = FontStyles.Bold;
        caption = Label("Text :: Sleep Caption", pill, font, 24f, new Color(.90f, .92f, .94f, 1f), TextAlignmentOptions.MidlineLeft);
        caption.rectTransform.offsetMin = new Vector2(70f, 0f); caption.rectTransform.offsetMax = new Vector2(-150f, 0f);
        status = Label("Text :: Sleep Status", pill, font, 20f, new Color(.57f, .63f, .67f, 1f), TextAlignmentOptions.MidlineRight);
        status.rectTransform.offsetMin = new Vector2(250f, 0f); status.rectTransform.offsetMax = new Vector2(-18f, 0f);
        // 누르고 있는 동안 차오르는 줄.
        fill = Panel("Image :: Hold Progress", pill, new Color32(46, 230, 208, 255));
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 0f); fill.offsetMin = Vector2.zero; fill.offsetMax = new Vector2(0f, 4f);
        Root.SetActive(false);
    }
    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = align; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
        return text;
    }

    void Update()
    {
        var rest = PlayerRestController.Instance; var market = MarketDayController.Instance;
        bool can = rest != null && market != null && rest.CanSleep && !market.ReportPending && !PauseMenuController.IsOpen && Time.timeScale > 0f;
        // 아침에 눈을 뜨는 동안에는 안내 없이 눈꺼풀만 보인다.
        if (opening > 0f)
        {
            opening = Mathf.Max(0f, opening - Time.unscaledDeltaTime);
            Show(false); Lids(opening / OpenSeconds);
            if (opening <= 0f) { Root.SetActive(false); }
            return;
        }
        if (!can) { held = 0f; if (Root.activeSelf) { Root.SetActive(false); } return; }
        bool pressing = Input.GetKey(Key) && !MouseCameraController.TextEntryActive;
        held = pressing ? held + Time.unscaledDeltaTime : Mathf.Max(0f, held - Time.unscaledDeltaTime * 2.5f);
        float progress = Mathf.Clamp01(held / HoldSeconds);
        if (!Root.activeSelf) { Root.SetActive(true); }
        Show(true); Lids(progress * progress * (3f - 2f * progress));
        fill.anchorMax = new Vector2(progress, 0f);
        GameLocalizedText.Set(caption, Word("길게 눌러 잠자기", "Hold to sleep", "長押しで眠る"), true);
        GameLocalizedText.Set(status, Word("피로", "Fatigue", "疲労") + " " + rest.State.fatigue.ToString("0") + "%", true);
        if (progress < 1f) { return; }
        held = 0f;
        if (rest.SleepUntilMorning()) { opening = OpenSeconds; }
    }
    private void Show(bool hinting) { hint.alpha = hinting ? 1f : 0f; }
    private void Lids(float closed)
    {
        Closed = closed;
        // 다 감기면 두 판이 화면 가운데에서 만난다(조금 겹치게 해서 틈이 없게 한다).
        upper.anchorMin = new Vector2(0f, 1f - closed * .52f); upper.anchorMax = Vector2.one;
        lower.anchorMin = Vector2.zero; lower.anchorMax = new Vector2(1f, closed * .52f);
    }

    void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
    }
}
