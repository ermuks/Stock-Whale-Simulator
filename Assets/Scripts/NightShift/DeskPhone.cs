using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 책상 위, 두 태블릿 사이에 놓인 휴대폰. 세력이 어느 종목을 몇 시 몇 분에 끌어올릴지 누군가 문자로 알려 준다
// (예전에는 문틈으로 쪽지가 들어왔다. 이제는 걸어가서 줍지 않아도 된다).
//  - 휴대폰을 바라보고 상호작용 키(F)를 누르거나 클릭하면 눈앞으로 들어 올린다. 처음 보이는 것은 문자 목록이다(종목과 작전 시간).
//  - 목록의 한 줄을 클릭하면 내용이 열린다: 종목, 문자 온 시간, 작전 시간, 메시지.
//  - Esc: 내용 → 목록 → 책상에 내려놓기. F는 어디서든 바로 내려놓는다.
//  - 1·2·3, Q·E: 휴대폰을 내려놓으면서 곧바로 그 화면을 확대한다(DeskViewController가 처리한다).
// 시세·봇·계좌에는 손대지 않는다. 세력이 이미 정해 둔 계획의 시각을 보여 줄 뿐이다. 씬 파일은 그대로다(실행 중에 만든다).
public sealed class DeskPhone : MonoBehaviour
{
    public static DeskPhone Instance { get; private set; }
    /// <summary>휴대폰을 들고 있다(들어 올리는 중 포함). 이 동안에는 시선·차트·태블릿 입력이 멈춘다.</summary>
    public static bool Raised => Instance != null && (Instance.state == State.Rising || Instance.state == State.Held);
    /// <summary>책상 위의 휴대폰을 겨누고 있다(상호작용 키가 휴대폰 들기로 쓰인다).</summary>
    public static bool Aiming => Instance != null && Instance.AimedNow();

    public sealed class Message
    {
        public Stock Stock;
        /// <summary>작전 시간: 장날 번호와 자정부터 센 분.</summary>
        public int Day, Minute;
        /// <summary>문자가 온 시간: 달력 날짜와 자정부터 센 분.</summary>
        public int ReceivedDate, ReceivedMinute;
        public bool Read;
    }
    private enum State { Desk, Rising, Held, Lowering }
    /// <summary>간직하는 문자의 수. 넘으면 가장 오래된 것부터 지운다. 목록 한 화면에 다 들어가는 만큼이다.</summary>
    public const int Kept = 7;
    // 화면의 크기와 모델의 자리는 씬의 예전 휴대폰(손에 쥔 모습)에서 맞춰 둔 값을 그대로 옮겼다: 모델의 1단위가 화면의 1000픽셀이다.
    private const float ScreenWidth = 978f, ScreenHeight = 2146f, ScreenScale = .00063286f;
    private static readonly Vector3 ModelPosition = new Vector3(.1763134f, -.0019119f, .0506249f), ModelScale = new Vector3(.6328589f, .6328589f, -.6328589f);
    private const string ModelPath = "Modeling/Iphone seceond version finished";
    private const float MoveSeconds = .22f, HeldDistance = 1.45f, HeldFill = .84f, ReachDistance = 11f, ArrivalSeconds = 7f;
    private static readonly Color Wallpaper = new Color(.028f, .032f, .042f), Card = new Color(.085f, .095f, .12f), CardUnread = new Color(.10f, .145f, .225f), CardHover = new Color(.16f, .19f, .25f);
    private static readonly Color Ink = new Color(.95f, .96f, .98f), Soft = new Color(.58f, .62f, .68f), Faint = new Color(.36f, .39f, .44f), Blue = new Color(.26f, .58f, 1f), Red = new Color(.96f, .26f, .24f), TimeTint = new Color(1f, .74f, .30f);
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    private readonly List<Message> messages = new();
    public IReadOnlyList<Message> Messages => messages;
    /// <summary>지금 내용을 열어 둔 문자(목록을 보고 있으면 null).</summary>
    public Message Open { get; private set; }
    public bool Ready => built;
    /// <summary>휴대폰을 놓을 자리를 끝내 찾지 못했다. 이때는 예전처럼 쪽지가 들어온다.</summary>
    public bool Unavailable { get; private set; }
    public int UnreadCount { get { int count = 0; foreach (var message in messages) { if (!message.Read) { count++; } } return count; } }

    private State state = State.Desk;
    private bool built, aimed, previousLocked;
    private int aimFrame = -1, locale = -1;
    private float clock, arrivedUntil, nextWrite, restScale = 1f, fromScale = 1f;
    private Vector3 restPosition, fromPosition;
    private Quaternion restRotation, fromRotation;
    private Vector2 bodyCenter, bodyHalf = new Vector2(ScreenWidth, ScreenHeight) * (ScreenScale * .5f);
    private Transform rig;
    private Camera viewCamera;
    private MouseCameraController mouseLook;
    private GraphicRaycaster raycaster;
    private TMP_FontAsset font;
    private AudioSource buzzer;
    private GameObject idle, idleBadge, list, listPill, detail, empty, hintRoot;
    private Image idleShade;
    private PhoneIcon whenIcon;
    private float nextIdle;
    private TextMeshProUGUI idleClock, idleCount, idleCaption, listTitle, listCount, emptyLabel, backLabel, sender, noReply, hint;
    private TextMeshProUGUI stockValue, whenValue, receivedValue, body;
    private sealed class Row
    {
        public GameObject Root; public Button Button; public PhoneIcon Dot, Clock; public TextMeshProUGUI Name, Time, Stamp, Mark;
    }
    private readonly Row[] rows = new Row[Kept];

    void Awake() { Instance = this; }
    void OnDestroy()
    {
        if (Instance == this) { Instance = null; }
        if (rig != null) { Destroy(rig.gameObject); }
    }

    // ── 자리 ────────────────────────────────────────────────────────────────
    private static readonly Vector3[] cornersA = new Vector3[4], cornersB = new Vector3[4];
    /// <summary>
    /// 두 태블릿 사이의 빈자리: 화면 한가운데가 놓일 곳, 누운 방향(글자의 위쪽이 플레이어 반대편을 본다), 화면의 세로 길이.
    /// 태블릿이 책상에 놓여 있을 때만 맞는 값이다(들고 있을 때 부르면 안 된다).
    /// </summary>
    public static bool RestSpot(DeskViewController views, out Vector3 center, out Quaternion rotation, out float height)
    {
        center = Vector3.zero; rotation = Quaternion.identity; height = 0f;
        if (views == null) { return false; }
        var first = views.ScreenFor(DeskViewTarget.TabletOne); var second = views.ScreenFor(DeskViewTarget.TabletTwo);
        if (first == null || second == null) { return false; }
        first.GetWorldCorners(cornersA); second.GetWorldCorners(cornersB);
        Vector3 a = (cornersA[0] + cornersA[2]) * .5f, b = (cornersB[0] + cornersB[2]) * .5f, axis = b - a; axis.y = 0f;
        float span = axis.magnitude;
        if (span < .01f) { return false; }
        axis /= span;
        // 태블릿 하나가 이 축을 따라 차지하는 절반 너비.
        float reachA = 0f, reachB = 0f;
        for (int i = 0; i < 4; i++)
        {
            reachA = Mathf.Max(reachA, Mathf.Abs(Vector3.Dot(cornersA[i] - a, axis)));
            reachB = Mathf.Max(reachB, Mathf.Abs(Vector3.Dot(cornersB[i] - b, axis)));
        }
        float gap = span - reachA - reachB;
        // 휴대폰은 태블릿 긴 쪽의 절반 남짓이다. 틈이 좁으면 틈에 맞춰 줄인다.
        float longSide = Mathf.Max(Vector3.Distance(cornersA[0], cornersA[1]), Vector3.Distance(cornersA[0], cornersA[3]));
        height = longSide * .55f;
        float width = height * ScreenWidth / ScreenHeight * 1.2f;
        if (gap > .02f && width > gap * .8f) { height *= gap * .8f / width; }
        center = gap > .02f ? a + axis * (reachA + gap * .5f) : (a + b) * .5f;
        center.y = Mathf.Max(a.y, b.y);
        Vector3 toPlayer = views.transform.position - center; toPlayer.y = 0f;
        Vector3 away = Vector3.Cross(axis, Vector3.up);
        if (Vector3.Dot(away, toPlayer) > 0f) { away = -away; }
        // 화면이 위를 보고 눕는다. 아무렇게나 내려놓은 것처럼 조금 비뚤게.
        rotation = Quaternion.LookRotation(Vector3.down, away) * Quaternion.Euler(0f, 0f, 7f);
        return true;
    }
    /// <summary>원 표시를 띄울 자리(책상 위에 놓여 있을 때만).</summary>
    public bool MarkerPoint(out Vector3 point)
    {
        point = built ? restPosition : Vector3.zero;
        return built && state == State.Desk;
    }

    // ── 문자 받기 ────────────────────────────────────────────────────────────
    /// <summary>day일(장날 번호) minute분(자정부터 센 분)에 끌어올린다는 문자가 지금 온다.</summary>
    public void Receive(Stock stock, int day, int minute)
    {
        if (stock == null) { return; }
        var market = MarketDayController.Instance;
        messages.Add(new Message
        {
            Stock = stock, Day = day, Minute = Mathf.Clamp(minute, 0, 1439),
            ReceivedDate = market == null ? MarketDayController.DateOf(day) : market.CalendarDate, ReceivedMinute = market == null ? 0 : market.ClockMinute,
        });
        while (messages.Count > Kept) { messages.RemoveAt(0); }
        arrivedUntil = Time.unscaledTime + ArrivalSeconds;
        if (buzzer != null) { buzzer.Play(); }
        if (built) { Write(); }
    }

    // ── 들기와 내려놓기 ──────────────────────────────────────────────────────
    private bool AimedNow()
    {
        if (aimFrame == Time.frameCount) { return aimed; }
        aimFrame = Time.frameCount; aimed = false;
        var views = DeskViewController.Instance;
        if (!built || state != State.Desk || views == null || !views.Ready || viewCamera == null) { return false; }
        if (DeskViewController.OwnsView || TabletViewController.IsTabletHeld || DoorNotes.Holding || RoomShell.Cinematic || PauseMenuController.BlocksInput || MouseCameraController.TextEntryActive || Time.timeScale <= 0f) { return false; }
        if (MouseCameraController.AimIsPointer && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { return false; }
        Ray ray = viewCamera.ScreenPointToRay(MouseCameraController.GetAimScreenPosition());
        if (!new Plane(rig.forward, rig.position).Raycast(ray, out float distance) || distance > ReachDistance) { return false; }
        // 휴대폰은 작아서 조금 넉넉하게 맞춘다.
        Vector3 local = rig.InverseTransformPoint(ray.GetPoint(distance));
        aimed = Mathf.Abs(local.x - bodyCenter.x) < bodyHalf.x * 1.6f && Mathf.Abs(local.y - bodyCenter.y) < bodyHalf.y * 1.25f;
        return aimed;
    }
    /// <summary>책상의 휴대폰을 눈앞으로 들어 올린다. 문자 목록부터 보인다.</summary>
    public void Raise()
    {
        if (!built || state != State.Desk) { return; }
        previousLocked = MouseCameraController.IsCursorLocked;
        if (mouseLook != null) { mouseLook.SetViewCursor(false); } else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        EventSystem.current?.SetSelectedGameObject(null);
        state = State.Rising; clock = 0f; Open = null; arrivedUntil = 0f;
        Sfx.Play("NotePick", .55f);
        Write();
    }
    /// <summary>들고 있던 휴대폰을 책상에 내려놓는다.</summary>
    public void Lower()
    {
        if (!Raised) { return; }
        raycaster.enabled = false;
        fromPosition = rig.position; fromRotation = rig.rotation; fromScale = rig.localScale.x;
        // 목록만 보고 내려놓은 문자는 안 읽은 채로 남는다. 내용을 열어야 읽은 것이 된다(OpenMessage).
        state = State.Lowering; clock = 0f; Open = null;
        // 다른 화면으로 바로 넘어갈 때(1·2·3, Q·E)는 그쪽이 커서를 다시 정한다. 여기서는 들기 전의 상태로만 돌려놓는다.
        if (previousLocked && Time.timeScale > 0f)
        {
            if (mouseLook != null) { mouseLook.SetViewCursor(true); } else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        Write();
    }
    /// <summary>목록의 index번째 줄(0이 가장 새 문자)을 연다.</summary>
    public void OpenMessage(int index)
    {
        int at = messages.Count - 1 - index;
        if (state != State.Held || at < 0 || at >= messages.Count) { return; }
        Open = messages[at]; Open.Read = true;
        SteamAchievements.Unlock(SteamAchievements.DoorNote);
        Write();
    }
    /// <summary>내용 화면에서 목록으로 돌아간다.</summary>
    public void ShowList()
    {
        if (Open == null) { return; }
        Open = null; Write();
    }

    void Update()
    {
        var views = DeskViewController.Instance;
        if (views == null || !views.Ready) { return; }
        if (!built)
        {
            if (!Unavailable) { Build(views); }
            if (!built) { return; }
        }
        if (locale != GameLocale.Revision) { locale = GameLocale.Revision; Write(); }
        // 쓰러져 잠들거나 장면이 넘어가면 들고 있던 휴대폰은 내려놓는다.
        if (Raised && (RoomShell.Cinematic || CollapseSleep.Active)) { Lower(); }
        bool free = !PauseMenuController.BlocksInput && !MouseCameraController.TextEntryActive && !RoomShell.Cinematic;
        if (state == State.Desk)
        {
            if (free && AimedNow() && (Input.GetKeyDown(GameKeyBindings.Interact) || Input.GetMouseButtonDown(0))) { Raise(); }
        }
        else if (Raised && free)
        {
            // Esc: 내용 → 목록 → 내려놓기. 상호작용 키는 어디서든 바로 내려놓는다(들 때와 같은 키).
            if (Input.GetKeyDown(KeyCode.Escape)) { if (Open != null) { ShowList(); } else { Lower(); } }
            else if (Input.GetKeyDown(GameKeyBindings.Interact) && clock > 0f) { Lower(); }
        }
        // 작전 시간이 지난 문자는 흐려진다. 들고 있는 동안 가끔 다시 쓴다.
        if (state == State.Held && Time.unscaledTime >= nextWrite) { Write(); }
        // 책상 위: 안 읽은 문자가 있으면 화면의 봉투가 숨 쉬듯 밝아진다.
        if (idle.activeSelf)
        {
            int unread = UnreadCount; bool fresh = Time.unscaledTime < arrivedUntil;
            float beat = .5f + .5f * Mathf.Sin(Time.unscaledTime * (fresh ? 9f : 2.6f));
            // 안 읽은 문자가 있으면 화면이 켜져 있다(숨 쉬듯 밝아진다). 없으면 꺼진 듯 어둡다.
            idleShade.color = new Color(0f, 0f, 0f, unread > 0 ? Mathf.Lerp(.28f, 0f, beat) : .62f);
            if (Time.unscaledTime >= nextIdle)
            {
                nextIdle = Time.unscaledTime + 1f; var market = MarketDayController.Instance;
                GameLocalizedText.Set(idleClock, market == null ? "" : Clock(market.ClockMinute), true);
            }
        }
        bool arrived = state == State.Desk && Time.unscaledTime < arrivedUntil;
        bool show = Raised || state == State.Desk && aimed && aimFrame == Time.frameCount || arrived;
        if (hintRoot.activeSelf != show) { hintRoot.SetActive(show); }
        if (show)
        {
            string key = GameKeyBindings.Interact.ToString();
            string text = Raised ? (Open != null ? "Esc   " + Word("목록으로", "Back to the list", "一覧へ") : "Esc   " + Word("내려놓기", "Put it down", "置く"))
                : aimed && aimFrame == Time.frameCount ? key + "   " + Word("휴대폰 보기", "Check the phone", "スマホを見る")
                : Word("문자가 왔습니다", "New text message", "メッセージが届いた");
            GameLocalizedText.Set(hint, text, true);
        }
    }

    void LateUpdate()
    {
        if (!built || state == State.Desk || viewCamera == null) { return; }
        // 눈앞에 든 자세: 화면이 시야의 대부분을 채우되 양옆으로 모니터가 보인다.
        Transform eye = viewCamera.transform;
        float viewHeight = 2f * HeldDistance * Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
        float heldScale = Mathf.Min(HeldFill * viewHeight / (ScreenHeight * ScreenScale), .9f * viewHeight * viewCamera.aspect / (ScreenWidth * ScreenScale));
        // 화면 아래의 안내 문구와 겹치지 않게 조금 올려 든다.
        Vector3 heldPosition = eye.position + eye.forward * HeldDistance + eye.up * (viewHeight * .035f);
        if (state == State.Held) { Place(heldPosition, eye.rotation, heldScale); return; }
        clock += Time.unscaledDeltaTime / MoveSeconds;
        float t = Mathf.Clamp01(clock), eased = t * t * (3f - 2f * t);
        if (state == State.Rising)
        {
            Place(Vector3.Lerp(restPosition, heldPosition, eased), Quaternion.Slerp(restRotation, eye.rotation, eased), Mathf.Lerp(restScale, heldScale, eased));
            if (t >= 1f) { state = State.Held; raycaster.enabled = true; nextWrite = 0f; }
        }
        else
        {
            Place(Vector3.Lerp(fromPosition, restPosition, eased), Quaternion.Slerp(fromRotation, restRotation, eased), Mathf.Lerp(fromScale, restScale, eased));
            if (t >= 1f) { state = State.Desk; Place(restPosition, restRotation, restScale); Write(); }
        }
    }
    private void Place(Vector3 position, Quaternion rotation, float scale)
    { rig.SetPositionAndRotation(position, rotation); rig.localScale = Vector3.one * scale; }

    // ── 만들기 ──────────────────────────────────────────────────────────────
    private void Build(DeskViewController views)
    {
        // 태블릿을 들고 있거나 화면을 확대한 동안에는 자리를 잴 수 없다.
        if (DeskViewController.OwnsView || TabletViewController.IsTabletHeld) { return; }
        if (!RestSpot(views, out restPosition, out restRotation, out float height))
        {
            // 태블릿이 없는 장면이면 포기한다(그때는 예전처럼 쪽지가 들어온다).
            if (Time.timeSinceLevelLoad > 10f) { Unavailable = true; }
            return;
        }
        built = true; viewCamera = views.ViewCamera; mouseLook = views.GetComponent<MouseCameraController>();
        font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? TMP_Settings.defaultFontAsset;
        rig = new GameObject("Area :: Desk Phone").transform;
        // rig의 원점은 화면 유리의 한가운데다. -z가 보는 사람 쪽, +y가 화면의 위쪽이다.
        float thickness = .09f;
        var source = Resources.Load<GameObject>(ModelPath);
        if (source != null)
        {
            var model = Instantiate(source, rig, false).transform; model.name = "Model :: Phone";
            model.localPosition = ModelPosition; model.localRotation = Quaternion.identity; model.localScale = ModelScale;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) { Destroy(collider); }
            // 모델이 실제로 차지하는 상자를 잰다: 가장 앞면(유리)을 원점에 맞추고, 두께만큼 책상에서 띄운다.
            bool any = false; Vector3 low = Vector3.zero, high = Vector3.zero;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) { continue; }
                Matrix4x4 toRig = rig.worldToLocalMatrix * filter.transform.localToWorldMatrix; Bounds box = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = toRig.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                    if (!any) { low = high = corner; any = true; } else { low = Vector3.Min(low, corner); high = Vector3.Max(high, corner); }
                }
            }
            if (any)
            {
                model.localPosition -= new Vector3(0f, 0f, low.z); thickness = high.z - low.z;
                bodyCenter = new Vector2((low.x + high.x) * .5f, (low.y + high.y) * .5f); bodyHalf = new Vector2(high.x - low.x, high.y - low.y) * .5f;
            }
        }
        else
        {
            // 모델 파일이 없으면 검은 판 하나로 대신한다.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); slab.name = "Model :: Phone"; Destroy(slab.GetComponent<Collider>());
            slab.transform.SetParent(rig, false); slab.transform.localScale = new Vector3(ScreenWidth * ScreenScale * 1.08f, ScreenHeight * ScreenScale * 1.04f, thickness);
            slab.transform.localPosition = new Vector3(0f, 0f, thickness * .5f + .004f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader); material.SetColor("_BaseColor", new Color(.03f, .03f, .035f)); material.color = new Color(.03f, .03f, .035f);
            slab.GetComponent<Renderer>().sharedMaterial = material;
            bodyHalf = new Vector2(slab.transform.localScale.x, slab.transform.localScale.y) * .5f;
        }
        restScale = height / (ScreenHeight * ScreenScale);
        // 책상 윗면에 뒷면이 닿게 놓는다. 책상을 못 찾으면 태블릿 화면 높이에 맞춘다.
        var desk = GameObject.Find("desk_body"); var deskRenderer = desk == null ? null : desk.GetComponent<Renderer>();
        float surface = deskRenderer != null && Mathf.Abs(deskRenderer.bounds.max.y - restPosition.y) < .6f ? deskRenderer.bounds.max.y : restPosition.y - thickness * restScale;
        restPosition.y = surface + thickness * restScale + .004f;
        Place(restPosition, restRotation, restScale);

        // 진동 소리. 책상 위에서 두 번 짧게 떤다.
        buzzer = rig.gameObject.AddComponent<AudioSource>();
        buzzer.playOnAwake = false; buzzer.loop = false; buzzer.dopplerLevel = 0f; buzzer.spatialBlend = .7f; buzzer.rolloffMode = AudioRolloffMode.Linear;
        buzzer.minDistance = 3f; buzzer.maxDistance = 30f; buzzer.volume = .85f; buzzer.clip = Buzz();

        BuildScreen();
        BuildHint();
        Write();
    }

    private static AudioClip Buzz()
    {
        const int rate = 44100; const float burst = .26f, pause = .10f;
        int count = (int)(rate * (burst * 2f + pause)); var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate, local = t < burst ? t : t - burst - pause;
            if (local < 0f) { continue; }
            float envelope = Mathf.Clamp01(local / .012f) * Mathf.Clamp01((burst - local) / .05f);
            float motor = Mathf.Sin(t * 172f * Mathf.PI * 2f) * .55f + Mathf.Sin(t * 344f * Mathf.PI * 2f) * .20f;
            // 책상에 닿아 떨리는 잡음.
            float rattle = (Mathf.PerlinNoise(t * 900f, .3f) - .5f) * (.5f + .5f * Mathf.Sin(t * 43f * Mathf.PI * 2f)) * .5f;
            data[i] = (motor * (.7f + .3f * Mathf.Sin(t * 27f * Mathf.PI * 2f)) + rattle) * envelope * .5f;
        }
        var clip = AudioClip.Create("Phone Buzz", count, 1, rate, false); clip.SetData(data, 0);
        return clip;
    }

    // 화면 위의 자리는 모두 화면에 대한 비율로 준다: 왼쪽 위가 (0, 0).
    private static RectTransform Area(string name, Transform parent, float x, float y, float w, float h)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(x, 1f - y - h); rect.anchorMax = new Vector2(x + w, 1f - y); rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var image = Area(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }
    private static Sprite roundedSprite;
    /// <summary>화면 전체를 덮는 판의 네 모서리를 휴대폰 본체처럼 둥글게 깎는다(네모난 모서리가 본체 밖으로 비어져 나오지 않게).</summary>
    private static Image Round(Image image, float corner)
    {
        if (roundedSprite == null)
        {
            const int size = 64; const float radius = 31f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Phone Screen Corner", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 네모까지의 거리로 가장자리를 부드럽게 한다.
                    float qx = Mathf.Max(0f, Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius)), qy = Mathf.Max(0f, Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius));
                    float alpha = Mathf.Clamp01(.5f - (Mathf.Sqrt(qx * qx + qy * qy) - radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
        // 모서리의 반지름이 화면에서 corner 픽셀이 되게.
        image.sprite = roundedSprite; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 31f / Mathf.Max(1f, corner);
        return image;
    }
    private TextMeshProUGUI Label(string name, Transform parent, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, bool bold = false)
    {
        var text = Area(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = color; text.alignment = align; text.raycastTarget = false; text.richText = false;
        // 긴 종목 이름도 칸 안에 들어가게 글자 크기를 스스로 줄인다.
        text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .5f;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        if (bold) { text.fontStyle = FontStyles.Bold; }
        return text;
    }
    /// <summary>그림 기호. 한가운데의 자리(비율)와 한 변의 길이(픽셀)를 준다.</summary>
    private static PhoneIcon Icon(string name, Transform parent, float centerX, float centerY, float size, PhoneIcon.Kind kind, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(centerX, 1f - centerY); rect.sizeDelta = new Vector2(size, size); rect.anchoredPosition = Vector2.zero;
        var icon = rect.gameObject.AddComponent<PhoneIcon>(); icon.Shape = kind; icon.color = color;
        return icon;
    }
    private static Button Clickable(Image image, Color normal, Color hover)
    {
        image.raycastTarget = true; image.color = Color.white;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors; colors.normalColor = normal; colors.highlightedColor = hover; colors.selectedColor = normal; colors.pressedColor = hover * 1.25f; colors.fadeDuration = .06f;
        button.colors = colors;
        return button;
    }

    private const float ScreenCorner = 104f;
    // 화면은 흔히 쓰는 문자 앱의 짜임을 따른다.
    //  - 책상 위(잠금 화면): 시각, 문자 앱 아이콘, 그 귀퉁이의 빨간 숫자(안 읽은 문자의 수).
    //  - 목록: 큰 제목 아래로 한 줄씩. 왼쪽 파란 점(안 읽음), 동그란 얼굴 자리, 굵은 이름 줄(종목), 그 아래 미리보기 줄(작전 시간), 오른쪽에 받은 시각.
    //  - 내용: 위에 돌아가기와 보낸 사람, 가운데에 받은 시각, 그 아래 왼쪽으로 붙은 말풍선 둘(종목·작전 시간, 그리고 메시지).
    private void BuildScreen()
    {
        var root = new GameObject("Panel :: Phone Messages", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        root.SetParent(rig, false); root.sizeDelta = new Vector2(ScreenWidth, ScreenHeight); root.localScale = Vector3.one * ScreenScale; root.localPosition = new Vector3(0f, 0f, -.004f);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewCamera;
        raycaster = root.GetComponent<GraphicRaycaster>(); raycaster.enabled = false;
        // 화면의 네 모서리는 본체처럼 둥글다. 그 안쪽에만 그린다.
        Round(Panel("Image :: Wallpaper", root, 0f, 0f, 1f, 1f, Wallpaper), ScreenCorner);
        Color rule = new Color(1f, 1f, 1f, .08f);

        // 책상 위. 자리에 앉아서 보면 휴대폰이 작으므로 아이콘과 빨간 숫자는 크게 그린다.
        idle = Area("Area :: Idle", root, 0f, 0f, 1f, 1f).gameObject;
        idleClock = Label("Text :: Idle Clock", idle.transform, .1f, .085f, .8f, .10f, 200f, Ink, TextAlignmentOptions.Center);
        idleBadge = Area("Area :: Idle Badge", idle.transform, 0f, .30f, 1f, .34f).gameObject;
        var tile = Round(Panel("Image :: Idle App Icon", idleBadge.transform, .265f, .12f, .47f, .63f, Blue), 110f);
        Icon("Image :: Idle Envelope", tile.transform, .5f, .5f, 270f, PhoneIcon.Kind.Envelope, Color.white);
        var count = Round(Panel("Image :: Idle Count Badge", idleBadge.transform, .60f, 0f, .27f, .36f, Red), 131f);
        idleCount = Label("Text :: Idle Count", count.transform, 0f, 0f, 1f, 1f, 180f, Color.white, TextAlignmentOptions.Center, true);
        idleCaption = Label("Text :: Idle Caption", idleBadge.transform, .1f, .82f, .8f, .14f, 60f, Soft, TextAlignmentOptions.Center);
        idleShade = Round(Panel("Image :: Idle Shade", idle.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, .6f)), ScreenCorner);

        // 목록.
        list = Area("Area :: Message List", root, 0f, 0f, 1f, 1f).gameObject;
        listTitle = Label("Text :: List Title", list.transform, .07f, .078f, .60f, .058f, 92f, Ink, TextAlignmentOptions.MidlineLeft, true);
        var pill = Round(Panel("Image :: Unread Pill", list.transform, .705f, .089f, .225f, .036f, Blue), 39f);
        listPill = pill.gameObject;
        listCount = Label("Text :: Unread Count", pill.transform, 0f, 0f, 1f, 1f, 38f, Color.white, TextAlignmentOptions.Center, true);
        Panel("Image :: List Rule", list.transform, 0f, .150f, 1f, .0008f, rule);
        for (int i = 0; i < Kept; i++)
        {
            int slot = i;
            var back = Round(Panel("Button :: Message " + i, list.transform, .02f, .158f + i * .094f, .96f, .094f, Wallpaper), 30f);
            var row = new Row { Root = back.gameObject, Button = Clickable(back, Wallpaper, CardHover) };
            row.Button.onClick.AddListener(() => OpenMessage(slot));
            row.Dot = Icon("Image :: Unread", back.transform, .035f, .5f, 24f, PhoneIcon.Kind.Dot, Blue);
            var face = Round(Panel("Image :: Sender Face", back.transform, .075f, .20f, .128f, .60f, Card), 60f);
            Icon("Image :: Sender", face.transform, .5f, .5f, 66f, PhoneIcon.Kind.Person, Soft);
            row.Name = Label("Text :: Stock", back.transform, .235f, .14f, .47f, .38f, 54f, Ink, TextAlignmentOptions.MidlineLeft, true);
            row.Stamp = Label("Text :: Received", back.transform, .70f, .17f, .27f, .30f, 34f, Faint, TextAlignmentOptions.MidlineRight);
            row.Clock = Icon("Image :: Time", back.transform, .256f, .695f, 36f, PhoneIcon.Kind.Clock, TimeTint);
            row.Time = Label("Text :: Time", back.transform, .292f, .52f, .42f, .35f, 44f, TimeTint, TextAlignmentOptions.MidlineLeft);
            row.Mark = Label("Text :: Read Mark", back.transform, .70f, .54f, .27f, .30f, 34f, Faint, TextAlignmentOptions.MidlineRight);
            Panel("Image :: Row Rule", back.transform, .235f, .994f, .765f, .006f, rule);
            rows[i] = row;
        }
        empty = Area("Area :: No Messages", list.transform, 0f, .34f, 1f, .26f).gameObject;
        Icon("Image :: Empty Envelope", empty.transform, .5f, .32f, 160f, PhoneIcon.Kind.Envelope, Faint);
        emptyLabel = Label("Text :: Empty", empty.transform, .08f, .58f, .84f, .18f, 44f, Soft, TextAlignmentOptions.Center);

        // 내용.
        detail = Area("Area :: Message Detail", root, 0f, 0f, 1f, 1f).gameObject;
        var back2 = Round(Panel("Button :: Back To List", detail.transform, .03f, .062f, .30f, .05f, Wallpaper), 36f);
        Clickable(back2, Wallpaper, CardHover).onClick.AddListener(ShowList);
        Icon("Image :: Back", back2.transform, .14f, .5f, 44f, PhoneIcon.Kind.Back, Blue);
        backLabel = Label("Text :: Back", back2.transform, .27f, 0f, .73f, 1f, 44f, Blue, TextAlignmentOptions.MidlineLeft);
        var who = Round(Panel("Image :: Sender Face", detail.transform, .45f, .058f, .10f, .0457f, Card), 49f);
        Icon("Image :: Sender", who.transform, .5f, .5f, 54f, PhoneIcon.Kind.Person, Soft);
        sender = Label("Text :: Sender", detail.transform, .2f, .108f, .6f, .026f, 32f, Soft, TextAlignmentOptions.Center);
        Panel("Image :: Detail Rule", detail.transform, 0f, .142f, 1f, .0008f, rule);
        receivedValue = Label("Text :: Received", detail.transform, .1f, .176f, .8f, .028f, 32f, Faint, TextAlignmentOptions.Center);
        Color bubbleTint = new Color(.15f, .165f, .20f);
        var plan = Round(Panel("Image :: Bubble Plan", detail.transform, .06f, .226f, .60f, .122f, bubbleTint), 50f);
        Icon("Image :: Stock", plan.transform, .105f, .30f, 42f, PhoneIcon.Kind.Trend, Blue);
        stockValue = Label("Text :: Stock", plan.transform, .19f, .12f, .75f, .36f, 48f, Ink, TextAlignmentOptions.MidlineLeft, true);
        whenIcon = Icon("Image :: Time", plan.transform, .105f, .70f, 40f, PhoneIcon.Kind.Clock, TimeTint);
        whenValue = Label("Text :: Time", plan.transform, .19f, .52f, .75f, .36f, 48f, TimeTint, TextAlignmentOptions.MidlineLeft);
        var bubble = Round(Panel("Image :: Bubble Text", detail.transform, .06f, .358f, .66f, .066f, bubbleTint), 50f);
        body = Label("Text :: Message", bubble.transform, .07f, .12f, .86f, .76f, 44f, Ink, TextAlignmentOptions.MidlineLeft);
        body.textWrappingMode = TextWrappingModes.Normal; body.fontSizeMin = 30f;
        noReply = Label("Text :: No Reply", detail.transform, .1f, .875f, .8f, .028f, 30f, Faint, TextAlignmentOptions.Center);
    }
    private static void Paint(Button button, Color normal, Color hover)
    {
        var colors = button.colors;
        if (colors.normalColor == normal) { return; }
        colors.normalColor = normal; colors.selectedColor = normal; colors.highlightedColor = hover; colors.pressedColor = hover * 1.25f;
        button.colors = colors;
    }

    private void BuildHint()
    {
        hintRoot = new GameObject("Area :: Phone Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hintRoot.transform.SetParent(transform, false);
        var canvas = hintRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 28860;
        var scaler = hintRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
        var pill = new GameObject("Panel :: Phone Hint", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); pill.SetParent(hintRoot.transform, false);
        pill.anchorMin = pill.anchorMax = new Vector2(.5f, 0f); pill.pivot = new Vector2(.5f, 0f); pill.anchoredPosition = new Vector2(0f, 46f); pill.sizeDelta = new Vector2(360f, 54f);
        pill.GetComponent<Image>().color = new Color(.04f, .055f, .07f, .92f); pill.GetComponent<Image>().raycastTarget = false;
        hint = Label("Text :: Phone Hint", pill, 0f, 0f, 1f, 1f, 24f, new Color(.90f, .92f, .94f, 1f), TextAlignmentOptions.Center);
        hint.enableAutoSizing = false;
        hintRoot.SetActive(false);
    }

    // ── 화면 쓰기 ────────────────────────────────────────────────────────────
    private static string Clock(int minute) => (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
    /// <summary>작전 시간: 날짜와 시각.</summary>
    public static string PlanTime(Message message) => GameLocale.Day(message.Day) + "  " + Clock(message.Minute);
    /// <summary>작전 시간이 이미 지났는가.</summary>
    public static bool Passed(Message message)
    {
        var market = MarketDayController.Instance;
        return market != null && market.AbsoluteGameMinute > (message.Day - 1) * 1440d + message.Minute + 1d;
    }
    /// <summary>지금 상태(책상/목록/내용)에 맞춰 화면을 다시 쓴다.</summary>
    private void Write()
    {
        nextWrite = Time.unscaledTime + 1f;
        bool resting = state == State.Desk || state == State.Lowering;
        idle.SetActive(resting); list.SetActive(!resting && Open == null); detail.SetActive(!resting && Open != null);
        int unread = UnreadCount;
        if (resting)
        {
            // 책상 위: 안 읽은 문자가 몇 통인지.
            idleBadge.SetActive(unread > 0); nextIdle = 0f;
            if (unread > 0)
            {
                GameLocalizedText.Set(idleCount, unread.ToString(), true);
                GameLocalizedText.Set(idleCaption, Word("안 읽은 메시지", "unread", "未読メッセージ"), true);
            }
            return;
        }
        if (Open == null)
        {
            GameLocalizedText.Set(listTitle, Word("메시지", "Messages", "メッセージ"), true);
            listPill.SetActive(unread > 0);
            if (unread > 0) { GameLocalizedText.Set(listCount, Word("안 읽음 ", "Unread ", "未読 ") + unread, true); }
            empty.SetActive(messages.Count == 0);
            if (messages.Count == 0) { GameLocalizedText.Set(emptyLabel, Word("받은 메시지가 없습니다", "No messages yet", "メッセージはありません"), true); }
            for (int i = 0; i < rows.Length; i++)
            {
                int at = messages.Count - 1 - i; var row = rows[i];
                if (row.Root.activeSelf != (at >= 0)) { row.Root.SetActive(at >= 0); }
                if (at < 0) { continue; }
                var message = messages[at]; bool fresh = !message.Read, passed = Passed(message);
                // 안 읽은 줄: 왼쪽에 파란 점, 굵은 흰 글씨. 읽은 줄: 점이 없고 회색 글씨, 오른쪽에 '읽음'.
                row.Dot.gameObject.SetActive(fresh);
                row.Name.color = fresh ? Ink : Soft; row.Name.fontStyle = fresh ? FontStyles.Bold : FontStyles.Normal;
                // 이미 지난 작전은 시각을 회색으로 쓴다.
                Color time = passed ? Faint : fresh ? TimeTint : new Color(TimeTint.r, TimeTint.g, TimeTint.b, .6f);
                row.Time.color = time; row.Clock.color = time;
                GameLocalizedText.Set(row.Name, GameLocale.Text(message.Stock.stockName), true);
                GameLocalizedText.Set(row.Time, PlanTime(message) + (passed ? Word(" · 지남", " · passed", " · 終了") : ""), true);
                GameLocalizedText.Set(row.Stamp, GameLocale.DateText(message.ReceivedDate) + " " + Clock(message.ReceivedMinute), true);
                GameLocalizedText.Set(row.Mark, fresh ? "" : Word("읽음", "Read", "既読"), true);
            }
            return;
        }
        bool over = Passed(Open);
        GameLocalizedText.Set(backLabel, Word("메시지", "Messages", "メッセージ"), true);
        GameLocalizedText.Set(sender, Word("발신자 표시 없음", "Unknown sender", "非通知"), true);
        GameLocalizedText.Set(receivedValue, GameLocale.DateText(Open.ReceivedDate) + "  " + Clock(Open.ReceivedMinute), true);
        GameLocalizedText.Set(stockValue, GameLocale.Text(Open.Stock.stockName), true);
        GameLocalizedText.Set(whenValue, PlanTime(Open) + (over ? Word(" · 지남", " · passed", " · 終了") : ""), true);
        whenValue.color = over ? Faint : TimeTint; whenIcon.color = over ? Faint : TimeTint;
        GameLocalizedText.Set(body, Word("이 시각에 올린다. 먼저 타.", "They lift it then. Get in first.", "この時刻に上げる。先に乗れ。"), true);
        GameLocalizedText.Set(noReply, Word("답장할 수 없는 번호입니다", "You can't reply to this number", "この番号には返信できません"), true);
    }
}
