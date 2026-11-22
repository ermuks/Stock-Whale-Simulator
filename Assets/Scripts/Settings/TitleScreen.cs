using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임을 켰을 때 가장 먼저 보이는 화면. 왼쪽에 로고와 메뉴, 맨 아래에 흐르는 시세 띠가 있다.
/// "새 게임"을 누르면 같은 자리에서 회사 이름을 정하는 화면으로 바뀐다. 세션의 상태는 건드리지 않고 단추만 주인에게 알린다.
/// </summary>
public sealed class TitleScreen
{
    private static readonly Color Ink = new(.95f, .97f, .98f), Muted = new(.60f, .66f, .71f), Warn = new Color32(242, 188, 100, 255);
    private static Color Accent => TabletKitStyle.Accent;
    private readonly PauseMenuController owner;
    private readonly TMP_FontAsset font;
    public readonly RectTransform Root;
    private readonly RectTransform menu, form;
    private readonly TextMeshProUGUI tagline, version, nameCaption, cashCaption, cashValue, error, startCaption, backCaption;
    private readonly List<(TextMeshProUGUI label, string ko, string en, string ja)> words = new();
    private readonly Button continueButton, start;
    private readonly TMP_InputField nameField;
    private readonly TitleTicker ticker;
    public string CompanyName => nameField.text;

    public TitleScreen(PauseMenuController controller, Transform parent, TMP_FontAsset fontAsset)
    {
        owner = controller; font = fontAsset;
        Root = Area(parent, "Area :: Title Screen"); Stretch(Root);
        // 왼쪽을 어둡게 눌러 로고와 메뉴가 방 배경 위에서도 읽히게 한다.
        // 상자 두 개를 나란히 세우면 이음매가 세로 선으로 보인다. 왼쪽에서 오른쪽으로 서서히 옅어지는 그림 한 장을 깐다.
        var veil = Area(Root, "Image :: Title Veil"); veil.anchorMin = new Vector2(0, 0); veil.anchorMax = new Vector2(.66f, 1); veil.offsetMin = veil.offsetMax = Vector2.zero;
        var fade = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int x = 0; x < 256; x++) { fade.SetPixel(x, 0, new Color(0, 0, 0, .52f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, x / 255f))))); }
        fade.Apply(false, true);
        var veilImage = veil.gameObject.AddComponent<RawImage>(); veilImage.texture = fade; veilImage.raycastTarget = false;

        // 로고: 오르는 캔들 몇 개, 이름 두 줄, 한 줄 소개.
        var mark = Area(Root, "Area :: Logo Mark"); Pin(mark, 120, 150, 260, 84);
        float[] bodies = { 18, 30, 22, 44, 36, 62, 84 }; bool[] rising = { true, true, false, true, false, true, true };
        for (int i = 0; i < bodies.Length; i++)
        {
            var candle = Box(mark, "Image :: Logo Candle " + i, rising[i] ? Accent : new Color32(239, 89, 100, 255));
            candle.anchorMin = candle.anchorMax = candle.pivot = new Vector2(0, 0); candle.anchoredPosition = new Vector2(i * 26, i * 3); candle.sizeDelta = new Vector2(16, bodies[i]);
            var wick = Box(mark, "Image :: Logo Wick " + i, candle.GetComponent<Image>().color);
            wick.anchorMin = wick.anchorMax = wick.pivot = new Vector2(0, 0); wick.anchoredPosition = new Vector2(i * 26 + 7, i * 3 - 8); wick.sizeDelta = new Vector2(2, bodies[i] + 18);
        }
        var name = Label(Root, "Title Name", "STOCK WHALE", 96, FontStyles.Bold); Pin(name.rectTransform, 116, 244, 900, 104); name.characterSpacing = 2;
        var sub = Label(Root, "Title Sub", "SIMULATOR", 38, FontStyles.Normal); Pin(sub.rectTransform, 122, 346, 900, 48); sub.characterSpacing = 46; sub.color = Accent;
        tagline = Label(Root, "Title Tagline", "", 22, FontStyles.Normal); Pin(tagline.rectTransform, 122, 404, 700, 34); tagline.color = Muted;
        words.Add((tagline, "가격을 움직여라. 시장을 삼켜라.", "Move the price. Own the market.", "値を動かせ。市場を呑み込め。"));

        // 메뉴: 상자 없는 큰 글자. 올려 두면 왼쪽에 밝은 막대가 서고 글자가 밀린다.
        menu = Area(Root, "Area :: Title Menu"); Pin(menu, 120, 480, 620, 360);
        continueButton = Item(menu, 0, "이어서 하기", "Continue", "続きから", owner.LoadSession, true);
        Item(menu, 1, "새 게임", "New game", "はじめから", owner.NewSession);
        Item(menu, 2, "설정", "Settings", "設定", () => owner.Settings());
        Item(menu, 3, "게임 종료", "Quit", "ゲーム終了", owner.Quit);

        // 새 게임: 회사 이름 하나만 묻는다. 시작 자금은 크게 보여 준다.
        form = Area(Root, "Area :: New Game"); Pin(form, 120, 470, 720, 380);
        nameCaption = Label(form, "Company Caption", "", 22, FontStyles.Normal); Pin(nameCaption.rectTransform, 0, 0, 600, 30); nameCaption.color = Muted;
        words.Add((nameCaption, "회사 이름", "Company name", "会社の名前"));
        nameField = Field(form, 0, 38, 600, 76);
        cashCaption = Label(form, "Cash Caption", "", 22, FontStyles.Normal); Pin(cashCaption.rectTransform, 0, 136, 300, 30); cashCaption.color = Muted;
        words.Add((cashCaption, "시작 자금", "Starting cash", "開始資金"));
        cashValue = Label(form, "Cash Value", "", 46, FontStyles.Bold); Pin(cashValue.rectTransform, 0, 166, 600, 60); cashValue.color = Accent; TradingOrderView.Numeric(cashValue);
        error = Label(form, "Name Error", "", 21, FontStyles.Normal); Pin(error.rectTransform, 0, 228, 600, 30); error.color = Warn;
        start = Solid(form, "Button :: Start Game", 0, 272, 340, 76, owner.StartNewGame, out startCaption);
        words.Add((startCaption, "시작하기", "Start", "はじめる"));
        var back = Solid(form, "Button :: Back To Title", 360, 272, 200, 76, owner.BackToTitle, out backCaption); back.image.color = new Color(1, 1, 1, .10f); backCaption.color = Ink;
        words.Add((backCaption, "뒤로", "Back", "もどる"));
        nameField.onValueChanged.AddListener(_ => Validate());
        nameField.onSubmit.AddListener(_ => { if (start.interactable) { owner.StartNewGame(); } });

        version = Label(Root, "Version", "v" + Application.version, 18, FontStyles.Normal); version.color = new Color(1, 1, 1, .35f);
        version.rectTransform.anchorMin = version.rectTransform.anchorMax = version.rectTransform.pivot = new Vector2(1, 0); version.rectTransform.anchoredPosition = new Vector2(-28, 62); version.rectTransform.sizeDelta = new Vector2(300, 26);
        version.alignment = TextAlignmentOptions.MidlineRight;

        // 맨 아래의 시세 띠.
        var strip = Box(Root, "Image :: Ticker Strip", new Color(0, 0, 0, .62f)); strip.anchorMin = new Vector2(0, 0); strip.anchorMax = new Vector2(1, 0); strip.pivot = new Vector2(.5f, 0); strip.sizeDelta = new Vector2(0, 46);
        strip.gameObject.AddComponent<RectMask2D>();
        var tape = Label(strip, "Ticker", "", 22, FontStyles.Normal); tape.richText = true; tape.textWrappingMode = TextWrappingModes.NoWrap; tape.overflowMode = TextOverflowModes.Overflow;
        tape.rectTransform.anchorMin = tape.rectTransform.anchorMax = tape.rectTransform.pivot = new Vector2(0, .5f); tape.rectTransform.sizeDelta = new Vector2(100, 46); TradingOrderView.Numeric(tape);
        ticker = strip.gameObject.AddComponent<TitleTicker>(); ticker.Tape = tape;
        Root.gameObject.SetActive(false);
    }

    public void Hide() { Root.gameObject.SetActive(false); }

    /// <summary>타이틀 메뉴. 저장된 세션이 없으면 "이어서 하기"는 아예 보이지 않는다.</summary>
    public void ShowMenu()
    {
        Root.gameObject.SetActive(true); menu.gameObject.SetActive(true); form.gameObject.SetActive(false);
        bool saved = SessionStore.Exists; continueButton.gameObject.SetActive(saved);
        // 보이는 항목만 위에서부터 채운다.
        int row = 0;
        foreach (Transform child in menu) { if (child.gameObject.activeSelf) { ((RectTransform)child).anchoredPosition = new Vector2(0, -row * 74); row++; } }
        Refresh();
    }

    /// <summary>새 게임의 이름 정하기.</summary>
    public void ShowNewGame(string suggested, decimal cash)
    {
        Root.gameObject.SetActive(true); menu.gameObject.SetActive(false); form.gameObject.SetActive(true);
        nameField.SetTextWithoutNotify(suggested ?? "");
        GameLocalizedText.Set(cashValue, DeviceTerminalUI.Money(cash), true);
        Validate(); Refresh();
        EventSystem.current?.SetSelectedGameObject(nameField.gameObject); nameField.ActivateInputField();
    }

    public void ShowError(string text) { GameLocalizedText.Set(error, text ?? "", true); }

    private void Validate()
    {
        bool valid = DailyIncomeController.ValidName(nameField.text);
        start.interactable = valid; TabletCall.Set(start, valid);
        startCaption.color = valid ? TabletKitStyle.Background : Muted;
        ShowError(valid ? "" : Pick("2~20자, 앞뒤 공백 없이", "2 to 20 characters, no spaces at the ends", "2〜20文字、前後に空白なし"));
    }

    public void Refresh()
    {
        if (!Root.gameObject.activeSelf) { return; }
        foreach (var word in words) { GameLocalizedText.Set(word.label, Pick(word.ko, word.en, word.ja), true); }
    }

    private static string Pick(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;

    private Button Item(Transform parent, int index, string ko, string en, string ja, Action action, bool primary = false)
    {
        var rect = Box(parent, "Button :: Title " + en, new Color(1, 1, 1, 0)); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(0, -index * 74); rect.sizeDelta = new Vector2(620, 66); rect.GetComponent<Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.transition = Selectable.Transition.None;
        var bar = Box(rect, "Image :: Title Item Bar", Accent); bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0, .5f); bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(6, 42);
        var label = Label(rect, "Title Item", "", primary ? 44 : 38, primary ? FontStyles.Bold : FontStyles.Normal);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(4, 0); label.rectTransform.offsetMax = Vector2.zero;
        words.Add((label, ko, en, ja));
        var hover = rect.gameObject.AddComponent<TitleMenuItem>(); hover.Label = label; hover.Bar = bar.GetComponent<Image>(); hover.Rest = primary ? Ink : new Color(.80f, .84f, .87f); hover.Lit = Accent;
        button.onClick.AddListener(() => action());
        return button;
    }

    private Button Solid(Transform parent, string name, float x, float y, float w, float h, Action action, out TextMeshProUGUI caption)
    {
        var rect = Box(parent, name, Accent); Pin(rect, x, y, w, h); rect.GetComponent<Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1); colors.pressedColor = new Color(.75f, .78f, .8f, 1); colors.disabledColor = new Color(.6f, .6f, .6f, .35f); colors.fadeDuration = .08f; button.colors = colors;
        caption = Label(rect, name + " Label", "", 30, FontStyles.Bold); caption.alignment = TextAlignmentOptions.Center; caption.color = TabletKitStyle.Background;
        caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one; caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
        button.onClick.AddListener(() => action());
        return button;
    }

    private TMP_InputField Field(Transform parent, float x, float y, float w, float h)
    {
        var frame = Box(parent, "Input :: New Company Name", new Color(1, 1, 1, .10f)); Pin(frame, x, y, w, h); frame.GetComponent<Image>().raycastTarget = true;
        var line = Box(frame, "Image :: Input Underline", Accent); line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0); line.pivot = new Vector2(.5f, 0); line.sizeDelta = new Vector2(0, 3);
        var viewport = Area(frame, "Area :: Name Viewport"); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(20, 0); viewport.offsetMax = new Vector2(-20, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label(viewport, "Name Text", "", 36, FontStyles.Bold); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.richText = false;
        var field = frame.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = frame.GetComponent<Image>(); field.textViewport = viewport; field.textComponent = text;
        field.characterLimit = 20; field.lineType = TMP_InputField.LineType.SingleLine; field.richText = false; field.fontAsset = font; field.pointSize = 36;
        field.customCaretColor = true; field.caretColor = Accent; field.caretWidth = 3; field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, .35f);
        field.transition = Selectable.Transition.None;
        return field;
    }

    private TextMeshProUGUI Label(Transform parent, string name, string text, float size, FontStyles style)
    {
        var rect = Area(parent, "Text :: " + name); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = size; label.enableAutoSizing = false; label.fontStyle = style; label.color = Ink;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        GameLocalizedText.Set(label, text, true); return label;
    }

    private static RectTransform Box(Transform parent, string name, Color color)
    { var rect = Area(parent, name); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rect; }
    private static RectTransform Area(Transform parent, string name)
    { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
    /// <summary>화면 왼쪽 위에서 잰 자리(기준 해상도 1600×900).</summary>
    private static void Pin(RectTransform rect, float x, float y, float w, float h)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}

/// <summary>타이틀 메뉴의 한 줄. 마우스를 올리면 막대가 서고 글자가 오른쪽으로 밀린다. 게임 시간이 멈춰 있어도 움직인다.</summary>
public sealed class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI Label;
    public Image Bar;
    public Color Rest, Lit;
    private bool over;
    private float amount;

    void OnEnable() { over = false; amount = 0f; Apply(); }
    public void OnPointerEnter(PointerEventData data) { over = true; }
    public void OnPointerExit(PointerEventData data) { over = false; }

    void Update()
    {
        float target = over ? 1f : 0f;
        if (Mathf.Approximately(amount, target)) { return; }
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 9f); Apply();
    }

    private void Apply()
    {
        if (Label == null || Bar == null) { return; }
        Label.color = Color.Lerp(Rest, Lit, amount);
        Label.rectTransform.offsetMin = new Vector2(4 + 26 * amount, 0);
        var color = Bar.color; color.a = amount; Bar.color = color;
    }
}

/// <summary>타이틀 화면 아래를 흐르는 시세 띠. 지금 세계의 종목과 등락을 읽기만 한다.</summary>
public sealed class TitleTicker : MonoBehaviour
{
    public TextMeshProUGUI Tape;
    private float next, offset, width;
    private const float Speed = 62f;

    void OnEnable() { next = 0f; offset = 0f; }

    void Update()
    {
        if (Tape == null) { return; }
        if (Time.unscaledTime >= next) { next = Time.unscaledTime + 3f; Rebuild(); }
        if (width <= 0f) { return; }
        offset += Time.unscaledDeltaTime * Speed;
        if (offset > width * .5f) { offset -= width * .5f; }
        Tape.rectTransform.anchoredPosition = new Vector2(-offset, 0);
    }

    private void Rebuild()
    {
        var manager = StockManager.Instance; var stats = StockStatistics.Instance;
        if (manager == null || manager.stocks == null || manager.stocks.Count == 0) { Tape.text = ""; width = 0f; return; }
        string up = "#" + ColorUtility.ToHtmlStringRGB(MarketColors.Up), down = "#" + ColorUtility.ToHtmlStringRGB(MarketColors.Down);
        var line = new StringBuilder();
        foreach (var stock in manager.stocks)
        {
            if (stock == null || !stock.IsListed) { continue; }
            line.Append("<color=#9AA7B2>").Append(stock.symbol).Append("</color>  ").Append(PriceTickPolicy.FormatPrice(stock.currentPrice));
            if (stats != null && stats.TryGetDailyChange(stock, out decimal change) && change != 0m)
            { line.Append("  <color=").Append(change > 0 ? up : down).Append('>').Append(change > 0 ? "+" : "-").Append(Math.Abs(change).ToString("0.0")).Append("%</color>"); }
            line.Append("          ");
        }
        // 같은 줄을 두 번 이어 붙여 끊김 없이 돈다.
        string once = line.ToString(); Tape.text = once + once;
        width = Tape.GetPreferredValues(Tape.text).x; Tape.rectTransform.sizeDelta = new Vector2(width, 46);
    }
}
