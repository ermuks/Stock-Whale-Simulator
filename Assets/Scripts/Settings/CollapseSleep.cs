using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피로가 끝까지 차면 쓰러져 그날을 통째로 잔다. 죽지는 않는다.
/// 눈이 스르륵 감기고, 그동안 세계는 평소와 똑같은 규칙으로 빠르게 흘러간다(걸어 둔 자동 매매·작전 도구, 세력의 작전도 그대로 돈다).
/// 장 마감 정산이 끝나면 눈을 뜬다. 가격이나 계좌를 직접 고치지 않는다. 시간만 빨리 감는다.
/// </summary>
public sealed class CollapseSleep : MonoBehaviour
{
    /// <summary>잠든 동안 세계가 흐르는 빠르기(평소의 몇 배).</summary>
    public const float FastForward = 16f;
    /// <summary>깨어났을 때 남겨 두는 피로의 상한. 눈을 뜨자마자 다시 쓰러지지 않게 한다.</summary>
    public const double WakeFatigue = 40d;
    public static bool Active { get; private set; }

    private static readonly string[] Korean =
    {
        "긴 꿈을 꾸고 있습니다", "몸이 말을 듣지 않습니다", "알람 소리가 아주 멀리서 들립니다", "눈꺼풀이 천 근입니다", "장이 열렸지만 일어날 수 없습니다",
        "누가 흔들어도 깨지 않을 잠입니다", "호가창이 꿈속에서 흘러갑니다", "밀린 잠이 한꺼번에 몰려왔습니다", "시장은 당신 없이도 돌아갑니다", "아직은 눈을 뜰 수 없습니다"
    };
    private static readonly string[] English =
    {
        "You are deep in a long dream", "Your body will not answer", "An alarm rings somewhere very far away", "Your eyelids weigh a ton", "The market is open, and you cannot get up",
        "Nothing is going to wake you now", "The order book scrolls by in your sleep", "Every hour you skipped has come to collect", "The market goes on without you", "Not yet. You cannot open your eyes yet"
    };
    private static readonly string[] Japanese =
    {
        "長い夢を見ています", "体が言うことを聞きません", "アラームがずっと遠くで鳴っています", "まぶたが鉛のように重い", "場は開いたのに、起き上がれません",
        "誰に揺すられても起きない眠りです", "夢の中を板が流れていきます", "ため込んだ眠気が一度に押し寄せました", "市場はあなた抜きでも回っています", "まだ、目を開けられません"
    };

    // 평소의 밤잠에 뜨는 꿈. 쓰러졌을 때와 달리 가볍고 엉뚱하다.
    private static readonly string[] DreamKorean =
    {
        "꿈에서 상한가를 쳤습니다", "꿈속에서도 호가창을 보고 있습니다", "양 대신 캔들을 세고 있습니다", "꿈에서 전 재산을 풀매수했습니다", "알람을 끄는 꿈을 꾸고 있습니다",
        "꿈에서는 항상 저점에 삽니다", "누군가 평단을 묻습니다. 꿈입니다", "코를 골고 있습니다. 아마도", "꿈속 계좌는 온통 빨갛습니다. 좋은 쪽으로", "5분만 더"
    };
    private static readonly string[] DreamEnglish =
    {
        "In the dream, it hit limit up", "Still watching the order book, even asleep", "Counting candles instead of sheep", "You went all in. It was a dream", "Dreaming of turning off the alarm",
        "In dreams you always buy the bottom", "Someone asks your average price. It is a dream", "You are snoring. Probably", "The dream account is all green", "Five more minutes"
    };
    private static readonly string[] DreamJapanese =
    {
        "夢の中でストップ高になりました", "夢の中でも板を見ています", "羊のかわりにローソク足を数えています", "夢で全財産を買いに入れました", "アラームを止める夢を見ています",
        "夢の中ではいつも底で買えます", "誰かに平均単価を聞かれました。夢です", "いびきをかいています。たぶん", "夢の口座は全部プラスです", "あと5分だけ"
    };

    private PlayerRestController rest;
    private MarketDayController day;
    private RectTransform upper, lower;
    private TextMeshProUGUI line;
    private GameObject root;
    private Coroutine running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Active = false; }

    public static CollapseSleep Install(PlayerRestController controller, MarketDayController market)
    {
        var sleep = controller.gameObject.AddComponent<CollapseSleep>(); sleep.rest = controller; sleep.day = market; sleep.Build(); return sleep;
    }

    private void Build()
    {
        root = new GameObject("Area :: Collapse Sleep", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); root.transform.SetParent(transform, false);
        // 메뉴(30000)보다 위: 눈을 뜨는 동안 정산 화면이 눈꺼풀 사이로 드러난다.
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30500;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        upper = Lid("Image :: Upper Eyelid", true); lower = Lid("Image :: Lower Eyelid", false);
        var text = new GameObject("Text :: Dream Line", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(root.transform, false);
        line = text.GetComponent<TextMeshProUGUI>(); line.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        line.fontSize = 34; line.alignment = TextAlignmentOptions.Center; line.raycastTarget = false; line.textWrappingMode = TextWrappingModes.NoWrap; line.color = new Color(.86f, .88f, .9f, 0f);
        var rect = line.rectTransform; rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(0, 60); rect.anchoredPosition = Vector2.zero;
        root.SetActive(false);
    }

    private RectTransform Lid(string name, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(root.transform, false);
        var image = go.GetComponent<Image>(); image.color = Color.black; image.raycastTarget = true;
        var rect = (RectTransform)go.transform; rect.anchorMin = new Vector2(0, top ? 1 : 0); rect.anchorMax = new Vector2(1, top ? 1 : 0); rect.pivot = new Vector2(.5f, top ? 1 : 0);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.sizeDelta = Vector2.zero;
        return rect;
    }

    /// <summary>눈꺼풀이 덮은 정도. 0은 뜬 눈, 1은 꼭 감은 눈.</summary>
    private void Close(float amount)
    {
        float height = ((RectTransform)root.transform).rect.height * .5f * Mathf.Clamp01(amount) + (amount >= 1f ? 2f : 0f);
        upper.sizeDelta = new Vector2(0, height); lower.sizeDelta = new Vector2(0, height);
    }

    /// <summary>쓰러진다. 이미 쓰러져 있거나 세계가 준비되지 않았으면 아무 일도 없다.</summary>
    public bool Begin()
    {
        if (Active || running != null || day == null || !day.Ready) { return false; }
        Active = true; root.SetActive(true); Close(0f);
        running = StartCoroutine(Run());
        return true;
    }

    /// <summary>
    /// 평소의 밤잠(정산 화면의 취침 단추): 눈이 감기고, 꿈 한두 줄이 지나가고, 아침에 눈을 뜬다.
    /// 붙여 둔 메모는 잠들 때 떨어진다. 눈을 뜬 뒤에 "DAY N"과 아침 브리핑이 이어진다.
    /// </summary>
    public bool BeginNightSleep()
    {
        if (Active || running != null || rest == null || !rest.CanSleep) { return false; }
        Active = true; root.SetActive(true); Close(0f);
        running = StartCoroutine(RunNight());
        return true;
    }

    private IEnumerator RunNight()
    {
        MemoNotes.Instance?.DropAll();
        yield return Blink(0f, .6f, 1.0f);
        yield return Blink(.6f, .38f, .55f);
        yield return Blink(.38f, 1f, 1.3f);
        int shown = Random.Range(0, DreamKorean.Length);
        for (int i = 0; i < 2; i++)
        {
            shown = (shown + 1 + Random.Range(0, DreamKorean.Length - 1)) % DreamKorean.Length;
            StartCoroutine(Whisper(GameLocale.Code == "ja" ? DreamJapanese[shown] : GameLocale.Code == "en" ? DreamEnglish[shown] : DreamKorean[shown]));
            yield return new WaitForSecondsRealtime(3.6f);
        }
        rest.SleepUntilMorning();
        Debug.Log("[휴식] 밤잠: 아침 " + DeviceMarketJournal.Stamp() + " · 피로 " + rest.State.fatigue.ToString("0"));
        var faded = line.color;
        for (float t = 0f; t < .7f; t += Time.unscaledDeltaTime) { faded.a = Mathf.Lerp(faded.a, 0f, t / .7f); line.color = faded; yield return null; }
        faded.a = 0f; line.color = faded;
        yield return Blink(1f, .45f, 1.1f);
        yield return Blink(.45f, .7f, .45f);
        yield return Blink(.7f, 0f, 1.3f);
        root.SetActive(false); Active = false; running = null;
    }

    private IEnumerator Run()
    {
        Debug.Log("[휴식] 피로 한계 — 쓰러져 잠듦: " + DeviceMarketJournal.Stamp() + " 단계 " + day.Phase);
        // 스르륵: 반쯤 감겼다가 한 번 버티고, 끝내 감긴다.
        yield return Blink(0f, .62f, 1.1f);
        yield return Blink(.62f, .34f, .7f);
        yield return Blink(.34f, 1f, 1.5f);

        float volume = AudioListener.volume; AudioListener.volume = volume * .12f;
        float nextLine = 0f; int shown = Random.Range(0, Korean.Length); float began = Time.unscaledTime; double startMinute = day.AbsoluteGameMinute;
        // 밤(마감 뒤)에 쓰러졌으면 그대로 아침까지 잔다. 낮이면 마감 정산이 끝날 때까지 세계를 빨리 흘려보낸다.
        bool night = day.Phase == MarketPhase.PostClose && !day.ReportPending;
        while (night ? Time.unscaledTime - began < 6f : !(day.Phase == MarketPhase.PostClose && day.ReportPending))
        {
            if (!PauseMenuController.IsOpen && Time.timeScale > 0f && !night) { Time.timeScale = FastForward; }
            if (Time.unscaledTime >= nextLine)
            {
                nextLine = Time.unscaledTime + 5.2f; shown = (shown + 1 + Random.Range(0, Korean.Length - 1)) % Korean.Length;
                StartCoroutine(Whisper(GameLocale.Code == "ja" ? Japanese[shown] : GameLocale.Code == "en" ? English[shown] : Korean[shown]));
            }
            yield return null;
        }
        if (Time.timeScale > 1f) { Time.timeScale = 1f; }
        AudioListener.volume = volume;

        // 잔 만큼 피로가 풀린다. 그래도 개운하지는 않다.
        rest.ObserveClock();
        if (night) { rest.SleepUntilMorning(); }
        double hours = System.Math.Max(0d, (day.AbsoluteGameMinute - startMinute) / 60d);
        rest.State.fatigue = System.Math.Min(WakeFatigue, System.Math.Max(0d, rest.State.fatigue - hours * 15d));
        rest.State.sleeplessNights = 0;
        Debug.Log("[휴식] 깨어남: " + DeviceMarketJournal.Stamp() + " · 잔 시간 " + hours.ToString("0.0") + "시간(게임) · 실제 " + (Time.unscaledTime - began).ToString("0") + "초 · 피로 " + rest.State.fatigue.ToString("0"));

        var faded = line.color; faded.a = 0f; line.color = faded;
        yield return new WaitForSecondsRealtime(.6f);
        yield return Blink(1f, .45f, 1.2f);
        yield return Blink(.45f, .7f, .5f);
        yield return Blink(.7f, 0f, 1.4f);
        root.SetActive(false); Active = false; running = null;
    }

    private IEnumerator Blink(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t / seconds);
            Close(Mathf.Lerp(from, to, eased));
            yield return null;
        }
        Close(to);
    }

    private IEnumerator Whisper(string text)
    {
        var color = line.color;
        for (float t = 0f; t < .8f; t += Time.unscaledDeltaTime) { color.a = Mathf.Lerp(color.a, 0f, t / .8f); line.color = color; yield return null; }
        GameLocalizedText.Set(line, text, true);
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { color.a = Mathf.SmoothStep(0f, .82f, t / 1.4f); line.color = color; yield return null; }
    }

    private void OnDisable()
    {
        // 장면이 바뀌거나 세션을 떠나면 빨리 감기를 남기지 않는다.
        if (Active) { Active = false; if (Time.timeScale > 1f) { Time.timeScale = 1f; } }
        running = null;
    }
}
