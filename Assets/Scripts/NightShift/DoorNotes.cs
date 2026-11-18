using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 문틈으로 들어오는 쪽지. 세력이 어느 종목을 몇 시 몇 분에 끌어올릴지 누군가 미리 알려 준다.
// 쪽지는 문 아래로 미끄러져 들어와 바닥에 놓인다. 가서 보고 F를 누르면 눈앞으로 들어 올려 읽고,
// 다시 F나 Space를 누르면 가운데 모니터 위쪽에 붙는다. 모니터에는 여섯 장까지 붙고, 넘으면 가장 오래된 것이 떨어져 나간다.
// 시세·봇·계좌에는 손대지 않는다. 세력이 이미 정해 둔 계획의 시각을 보여 줄 뿐이다.
public sealed class DoorNotes : MonoBehaviour
{
    public static DoorNotes Instance { get; private set; }
    public static bool Holding => Instance != null && Instance.held != null;
    /// <summary>바닥의 쪽지를 겨누고 있다(F가 쪽지 줍기로 쓰인다).</summary>
    public static bool Aiming => Instance != null && Instance.aimed != null;
    public const KeyCode PickKey = KeyCode.F;
    private const int StuckLimit = 6;
    /// <summary>노크가 울리고 이만큼 뒤에 쪽지가 들어오기 시작한다.</summary>
    private const float KnockLeadSeconds = 1.1f;
    private const float SlideSeconds = 1.4f, FlySeconds = .55f, ReachDistance = 10f, Side = .46f;
    private static readonly Color Dim = new(.40f, .35f, .13f), Bright = new(.96f, .87f, .42f);
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    public sealed class Note
    {
        public Transform Root;
        public Image Paper;
        public TextMeshProUGUI Name, Time, Line;
        public Stock Stock;
        public int Day, Minute, Slot = -1;
        /// <summary>0 미끄러져 들어오는 중, 1 바닥, 2 손에 듦, 3 모니터로 날아가는 중, 4 모니터에 붙음.</summary>
        public int State;
        public float Clock;
        /// <summary>종이가 들어오는 소리를 이미 냈는가.</summary>
        public bool Slid;
        public Vector3 From, To; public Quaternion FromTurn, ToTurn;
    }
    private readonly List<Note> notes = new();
    private readonly List<(float at, Stock stock, int day, int minute)> waiting = new();
    private Note held, aimed;
    private TMP_FontAsset font;
    private GameObject hintRoot;
    private TextMeshProUGUI hint;
    private float arrivedUntil;
    private int locale = -1;
    public IReadOnlyList<Note> Notes => notes;

    void Awake()
    {
        Instance = this;
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        hintRoot = new GameObject("Area :: Door Note Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintRoot.transform.SetParent(transform, false);
        var canvas = hintRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28850;
        var scaler = hintRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var pill = new GameObject("Panel :: Door Note Hint", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); pill.SetParent(hintRoot.transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f); pill.anchoredPosition = new Vector2(0f, 112f); pill.sizeDelta = new Vector2(470f, 54f);
        pill.GetComponent<Image>().color = new Color(.04f, .055f, .07f, .92f); pill.GetComponent<Image>().raycastTarget = false;
        hint = Label("Text :: Door Note Hint", pill, 24f, new Color(.90f, .92f, .94f, 1f), TextAlignmentOptions.Center);
        hintRoot.SetActive(false);
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private TextMeshProUGUI Label(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = align; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        return text;
    }

    /// <summary>장중 초로 seconds 뒤에 끌어올리기가 시작된다는 것을 쪽지로 알린다. 장이 닫혀 있는 시간은 세지 않는다.</summary>
    public void AnnounceAfterTrading(Stock stock, float seconds)
    {
        var market = MarketDayController.Instance;
        if (market == null || stock == null) { return; }
        float length = Mathf.Max(1f, market.tradingSeconds); int day = market.Day; float into;
        if (market.Phase == MarketPhase.Trading)
        {
            float passed = length - market.RemainingSeconds;
            if (passed + seconds <= length) { into = passed + seconds; }
            else { seconds -= length - passed; day++; while (seconds > length) { seconds -= length; day++; } into = seconds; }
        }
        else
        {
            // 장이 열리기 전이면 오늘, 끝난 뒤면 내일 장에서 센다.
            if (market.Phase != MarketPhase.PreOpen) { day++; }
            while (seconds > length) { seconds -= length; day++; }
            into = seconds;
        }
        AnnounceAt(stock, day, 540 + Mathf.RoundToInt(480f * into / length));
    }
    /// <summary>day일 minute분(자정부터 센 분)에 끌어올린다는 쪽지를 조금 뒤에 들여보낸다.</summary>
    public void AnnounceAt(Stock stock, int day, int minute)
    {
        if (stock == null) { return; }
        waiting.Add((UnityEngine.Time.time + Random.Range(6f, 22f), stock, day, Mathf.Clamp(minute, 0, 1439)));
    }
    /// <summary>기다리는 쪽지를 지금 바로 들여보낸다(확인용).</summary>
    public void DeliverNow()
    {
        for (int i = 0; i < waiting.Count; i++) { var item = waiting[i]; waiting[i] = (0f, item.stock, item.day, item.minute); }
    }

    void Update()
    {
        var shell = RoomShell.Instance; var views = DeskViewController.Instance;
        if (shell == null || !shell.Built || views == null || !views.Ready) { return; }
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            if (UnityEngine.Time.time < waiting[i].at) { continue; }
            Deliver(waiting[i].stock, waiting[i].day, waiting[i].minute, shell); waiting.RemoveAt(i);
        }
        if (locale != GameLocale.Revision) { locale = GameLocale.Revision; foreach (var note in notes) { Write(note); } }
        var camera = views.ViewCamera;
        foreach (var note in notes)
        {
            if (note.State == 0 || note.State == 3)
            {
                note.Clock += UnityEngine.Time.unscaledDeltaTime / (note.State == 0 ? SlideSeconds : FlySeconds);
                // 노크가 먼저 울리고, 잠깐 뒤에 종이가 문틈으로 밀려 들어온다. 종이 소리는 움직이기 시작하는 순간에 난다.
                if (note.State == 0 && !note.Slid && note.Clock >= 0f) { note.Slid = true; Sfx.Play("NoteSlide", .85f, note.From + Vector3.up * .5f); }
                float t = Mathf.Clamp01(note.Clock), eased = note.State == 0 ? 1f - (1f - t) * (1f - t) : t * t * (3f - 2f * t);
                Vector3 position = Vector3.Lerp(note.From, note.To, eased);
                // 모니터로 갈 때는 살짝 떠올랐다가 내려앉는다.
                if (note.State == 3) { position += Vector3.up * Mathf.Sin(eased * Mathf.PI) * .35f; }
                note.Root.SetPositionAndRotation(position, Quaternion.Slerp(note.FromTurn, note.ToTurn, eased));
                if (t >= 1f)
                {
                    if (note.State == 0) { note.State = 1; }
                    else { note.State = 4; note.Root.SetParent(views.ScreenFor(DeskViewTarget.Chart), true); Sfx.Play("NoteStick", .8f, note.Root.position); }
                }
            }
        }
        bool free = !PauseMenuController.BlocksInput && !MouseCameraController.TextEntryActive && UnityEngine.Time.timeScale > 0f && !RoomShell.Cinematic;
        aimed = null;
        if (held != null)
        {
            // 들고 있는 동안: 눈앞에 붙어 다닌다. F·Space·Esc로 내려놓으면 모니터로 날아가 붙는다.
            held.Root.SetPositionAndRotation(camera.transform.position + camera.transform.forward * .92f - camera.transform.up * .02f, camera.transform.rotation);
            if (free && (Input.GetKeyDown(PickKey) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))) { PutDown(); }
        }
        else if (free && !DeskViewController.OwnsView)
        {
            aimed = Aim(camera.ScreenPointToRay(MouseCameraController.GetAimScreenPosition()));
            if (aimed != null && Input.GetKeyDown(PickKey)) { PickUp(aimed); }
        }
        bool arrived = UnityEngine.Time.unscaledTime < arrivedUntil && held == null && aimed == null;
        bool show = held != null || aimed != null || arrived;
        if (hintRoot.activeSelf != show) { hintRoot.SetActive(show); }
        if (show)
        {
            string text = held != null ? PickKey + " / Space   " + Word("모니터에 붙이기", "Stick it on the monitor", "モニターに貼る")
                : aimed != null ? PickKey + "   " + Word("쪽지 줍기", "Pick up the note", "メモを拾う")
                : Word("문틈으로 쪽지가 들어왔습니다", "A note slid under the door", "ドアの下からメモが差し込まれた");
            GameLocalizedText.Set(hint, text, true);
        }
    }

    private void Deliver(Stock stock, int day, int minute, RoomShell shell)
    {
        var note = new Note { Stock = stock, Day = day, Minute = minute };
        var root = new GameObject("Panel :: Door Note", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
        root.SetParent(transform, false); root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        root.sizeDelta = new Vector2(300f, 300f); root.localScale = Vector3.one * (Side / 300f);
        note.Root = root;
        var paper = new GameObject("Image :: Paper", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); paper.SetParent(root, false);
        paper.anchorMin = Vector2.zero; paper.anchorMax = Vector2.one; paper.offsetMin = paper.offsetMax = Vector2.zero;
        note.Paper = paper.GetComponent<Image>(); note.Paper.raycastTarget = false; note.Paper.color = Dim;
        Color ink = new Color(.10f, .09f, .06f);
        note.Name = Label("Text :: Stock", Area(root, .06f, .10f, .88f, .26f), 44f, ink, TextAlignmentOptions.Center); note.Name.fontStyle = FontStyles.Bold;
        note.Name.enableAutoSizing = true; note.Name.fontSizeMax = 44f; note.Name.fontSizeMin = 20f;
        note.Time = Label("Text :: Time", Area(root, .06f, .38f, .88f, .28f), 56f, new Color(.42f, .07f, .05f), TextAlignmentOptions.Center); note.Time.fontStyle = FontStyles.Bold;
        note.Time.enableAutoSizing = true; note.Time.fontSizeMax = 56f; note.Time.fontSizeMin = 24f;
        note.Line = Label("Text :: Line", Area(root, .06f, .70f, .88f, .22f), 24f, ink, TextAlignmentOptions.Center);
        note.Line.enableAutoSizing = true; note.Line.fontSizeMax = 24f; note.Line.fontSizeMin = 12f;
        Write(note);
        // 문 바깥에서 시작해 틈으로 미끄러져 들어와, 조금 비뚤게 멈춘다.
        float side = Random.Range(-.9f, .9f), turn = Random.Range(-35f, 35f);
        note.From = shell.DoorGap + new Vector3(side, .02f, -.7f); note.To = shell.DoorGap + new Vector3(side * 1.15f, .02f, Random.Range(.85f, 1.5f));
        note.FromTurn = Quaternion.Euler(90f, 180f + turn * .3f, 0f); note.ToTurn = Quaternion.Euler(90f, 180f + turn, 0f);
        root.SetPositionAndRotation(note.From, note.FromTurn);
        // 미끄러지기 전에 노크만큼 기다린다(Clock이 0이 될 때까지 문 밖에 머문다).
        note.Clock = -KnockLeadSeconds / SlideSeconds;
        notes.Add(note); arrivedUntil = UnityEngine.Time.unscaledTime + 7f + KnockLeadSeconds;
        Sfx.Play("NoteKnock", .9f, shell.DoorCenter);
    }
    private static RectTransform Area(RectTransform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject("Area :: Line", typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y); rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static void Write(Note note)
    {
        GameLocalizedText.Set(note.Name, GameLocale.Text(note.Stock.stockName), true);
        GameLocalizedText.Set(note.Time, GameLocale.Day(note.Day) + "  " + (note.Minute / 60).ToString("00") + ":" + (note.Minute % 60).ToString("00"), true);
        GameLocalizedText.Set(note.Line, Word("이 시각에 올린다. 먼저 타.", "They lift it then. Get in first.", "この時刻に上げる。先に乗れ。"), true);
    }

    /// <summary>이 시선이 겨누고 있는 바닥의 쪽지(없으면 null). 쪽지는 작아서 조금 넉넉한 공으로 맞춘다.</summary>
    public Note Aim(Ray ray)
    {
        Note found = null; float nearest = float.MaxValue;
        foreach (var note in notes)
        {
            if (note.State != 1) { continue; }
            Vector3 to = note.Root.position - ray.origin; float along = Vector3.Dot(to, ray.direction);
            if (along <= 0f || along > ReachDistance) { continue; }
            float miss = (to - ray.direction * along).magnitude;
            // 눈높이가 5.5라서 바닥의 쪽지는 바로 위에서 내려다봐도 5.5만큼 떨어져 있다. 멀수록 조금 더 넉넉하게 맞춘다.
            if (miss < Side * .9f + along * .045f && along < nearest) { nearest = along; found = note; }
        }
        return found;
    }
    /// <summary>바닥의 쪽지를 눈앞으로 들어 올린다.</summary>
    public void PickUp(Note note)
    {
        if (note == null || note.State != 1 || held != null) { return; }
        held = note; note.State = 2; note.Paper.color = Bright; note.Root.SetParent(transform, true);
        Sfx.Play("NotePick", .8f);
    }
    /// <summary>들고 있던 쪽지를 가운데 모니터 위쪽에 붙인다.</summary>
    public void PutDown()
    {
        if (held == null) { return; }
        var note = held; held = null;
        var screen = DeskViewController.Instance.ScreenFor(DeskViewTarget.Chart);
        if (screen == null) { note.State = 1; return; }
        // 빈자리 가운데 가장 왼쪽. 다 찼으면 가장 오래 붙어 있던 것을 떼어 낸다.
        var used = new bool[StuckLimit]; Note oldest = null;
        foreach (var other in notes) { if (other.Slot >= 0) { used[other.Slot] = true; if (oldest == null) { oldest = other; } } }
        int slot = System.Array.IndexOf(used, false);
        if (slot < 0 && oldest != null) { slot = oldest.Slot; notes.Remove(oldest); Destroy(oldest.Root.gameObject); }
        note.Slot = Mathf.Max(0, slot);
        var corners = new Vector3[4]; screen.GetWorldCorners(corners);
        Vector3 right = (corners[2] - corners[1]).normalized, up = (corners[1] - corners[0]).normalized;
        float span = Vector3.Distance(corners[1], corners[2]), size = span / (StuckLimit + .8f);
        note.From = note.Root.position; note.FromTurn = note.Root.rotation;
        // 화면 위쪽 테두리에 아랫부분만 걸치게 붙인다. 화면 글자는 가리지 않는다.
        note.To = corners[1] + right * (size * (.9f + note.Slot * 1.0f)) + up * (size * .42f) - screen.forward * .03f;
        note.ToTurn = screen.rotation * Quaternion.Euler(0f, 0f, (note.Slot * 37 % 9) - 4f);
        note.Root.localScale = Vector3.one * (size * .92f / 300f);
        note.Paper.color = Dim * 1.35f; note.State = 3; note.Clock = 0f;
    }
}
