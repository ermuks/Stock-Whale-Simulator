using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 기존 모니터/태블릿의 보기와 입력만 관리한다. 시뮬레이션 시간과 경제 상태는 건드리지 않는다.
///
/// 모니터를 보는 시점(1·2·3)에서는 세 모니터가 모두 조작된다. 그리고 시점을 직접 움직일 수 있다:
///  - 휠: 마우스 커서가 가리키는 곳을 향해 더 확대하고, 원래 시점까지 다시 축소한다.
///  - 오른쪽 버튼을 누른 채 끌기: 잡아서 끄는 방식으로 시점을 옮긴다. 옆으로 끄는 만큼 시선이 모니터를 따라 돌아서,
///    왼쪽(종목 목록)·가운데(차트)·오른쪽(채팅) 어느 모니터를 보든 그 모니터를 정면에 가깝게 본다
///    (모니터마다 놓인 각도가 달라서, 카메라가 옆으로만 미끄러지면 옆 모니터가 비스듬히 보인다). 위아래로는 확대해 둔 만큼 움직인다.
///  - 1·2·3 키는 정해 둔 시점(프리셋)으로 간다. 움직여 놓은 상태에서 같은 키를 누르면 그 프리셋으로 돌아오고, 프리셋에서 한 번 더 누르면 자리로 돌아간다.
/// 휠은 확대에 쓰므로 목록(종목 목록, 채팅)은 휠에 움직이지 않는다. 목록은 끌어서 넘기거나, Ctrl을 누른 채 휠을 돌려 넘긴다.
/// </summary>
[DefaultExecutionOrder(-250)]
[RequireComponent(typeof(Camera))]
public sealed class DeskViewController : MonoBehaviour
{
    public static DeskViewController Instance { get; private set; }
    [Tooltip("왼쪽 종목, 중앙 차트, 오른쪽 채팅 순서")]
    public RectTransform[] monitorScreens = new RectTransform[3];
    public TabletViewController[] tablets = new TabletViewController[2];
    public InvestorJourneyNotes journeyNotes;
    [Range(.7f, .96f)] public float monitorFill = .92f;
    [Range(30f, 45f)] public float focusFieldOfView = 45f;
    [Tooltip("쓰지 않는다(예전: 종목 목록을 볼 때의 카메라 월드 Y 회전). 이제 종목 목록은 정면에서 본다.")]
    public float stocksViewYaw = -20f;
    [Tooltip("쓰지 않는다(예전: 채팅을 볼 때의 카메라 월드 Y 회전). 이제 채팅은 정면에서 본다.")]
    public float chatViewYaw = 5.7f;
    [Range(.1f, .5f)] public float transitionSeconds = .24f;
    [Range(0f, .5f)] public float tabletSwayDegrees = .22f;
    public DeskViewTarget Target { get; private set; } = DeskViewTarget.None;
    public DeskViewTarget Hovered { get; private set; } = DeskViewTarget.None;
    public bool IsFocused => Target != DeskViewTarget.None;
    public bool IsTransitioning => moving;
    public bool Ready => StockManager.Instance == null || StockManager.Instance.SetupConfirmed;
    public Camera ViewCamera => viewCamera;
    public static bool OwnsView => Instance != null && (Instance.IsFocused || Instance.moving || Instance.custom);
    /// <summary>모니터·태블릿이 아닌 곳(서랍 같은 소품)을 들여다보는 중인가.</summary>
    public bool IsCustomView => custom;
    private bool custom;
    /// <summary>모니터를 보는 시점(1 종목, 2 가운데, 3 채팅)인가. 이 시점들에서는 가운데 화면도 함께 보이고 조작된다.</summary>
    private static bool MonitorView(DeskViewTarget target) => target == DeskViewTarget.Stocks || target == DeskViewTarget.Chart || target == DeskViewTarget.Chat;
    public static bool BlockChartPointer => Instance != null && (!(MonitorView(Instance.Target) || Instance.TabletOverMonitor) || Instance.moving || Instance.consumedFrame == Time.frameCount);
    /// <summary>태블릿을 들고 있고, 마우스가 태블릿이 아니라 뒤의 모니터 위에 있는가(그때는 차트도 조작된다).</summary>
    private bool TabletOverMonitor => Target >= DeskViewTarget.TabletOne && FindHovered(Input.mousePosition) <= DeskViewTarget.Chat && FindHovered(Input.mousePosition) != DeskViewTarget.None;
    private float tabletClickAt = -10f; private Vector2 tabletClickFrom, tabletGrabFrom;
    /// <summary>태블릿 시점의 기준 방향(home)에서 화면의 한 점을 지나는 시선의 방향(월드). 흔들림은 넣지 않는다.</summary>
    private Vector3 ViewRay(Vector2 pixel)
    {
        float tanY = Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad), tanX = tanY * viewCamera.aspect;
        var local = new Vector3((pixel.x / Mathf.Max(1f, Screen.width) * 2f - 1f) * tanX, (pixel.y / Mathf.Max(1f, Screen.height) * 2f - 1f) * tanY, 1f);
        return (home.rotation * local).normalized;
    }
    /// <summary>모니터를 확대해 보는 중인가(1·2·3 시점).</summary>
    public static bool MonitorZoomed => Instance != null && MonitorView(Instance.Target);
    /// <summary>차트를 끌어도 되는가. 시점은 오른쪽 버튼으로 옮기므로 왼쪽 끌기는 언제나 차트의 것이다.</summary>
    public static bool ChartDragAllowed => true;
    private static bool ViewKey => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    [Header("모니터 시점에서 직접 움직이기")]
    [Tooltip("휠로 가장 가까이 다가갔을 때의 거리(프리셋 거리의 몇 배인가).")]
    [Range(.15f, .9f)] public float nearestZoom = .3f;
    [Tooltip("휠 한 칸에 거리가 줄어드는 비율.")]
    [Range(.7f, .97f)] public float zoomStep = .88f;
    [Tooltip("오른쪽 버튼으로 끌 때, 화면 너비의 이만큼을 끌면 가운데에서 옆 모니터 정면까지 넘어간다.")]
    [Range(.15f, .8f)] public float turnScreenShare = .35f;
    [Tooltip("끌거나 확대할 때 카메라가 따라오는 빠르기. 클수록 바로 따라오고, 작을수록 느긋하게 따라온다.")]
    [Range(4f, 40f)] public float followSharpness = 14f;
    private Camera viewCamera;
    private MarketWallClock wallClock;
    private MouseCameraController mouseLook;
    private Pose home, from, destination;
    private float homeFov, fromFov, destinationFov, transitionTime, lastAspect;
    private bool homeCursorLocked, hasHome, moving;
    private int consumedFrame = -1;
    private Vector2 sway;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly GraphicRaycaster[][] raycasters = new GraphicRaycaster[5][];
    private readonly bool[][] authoredRaycasts = new bool[5][];
    private bool viewKeyHeld, turning;
    private Vector2 lastMouse;
    private Pose preset; private bool hasPreset;
    // 직접 움직인 시점의 상태. turn: -1 종목 목록 정면, 0 가운데, +1 채팅 정면. local: 그 자리에서 휠로 다가간 만큼(카메라 기준).
    // twist: 프리셋이 정면에서 틀어져 있던 만큼(1번·3번은 가운데 화면도 보이게 비스듬히 본다). 돌리기 시작하면 서서히 정면으로 푼다.
    private readonly Pose[] facing = new Pose[3];
    private readonly float[] facingDepth = new float[3];
    /// <summary>가운데 모니터의 한가운데에서 옆 모니터(0 왼쪽, 2 오른쪽)의 한가운데까지의 거리.</summary>
    private readonly float[] facingSpan = new float[3];
    private readonly Vector3[] facingCenter = new Vector3[3];
    private float turn, twistKeep; private Vector3 local; private Quaternion twist = Quaternion.identity; private bool viewReady;
    private readonly System.Collections.Generic.Dictionary<ScrollRect, float> lockedPanels = new();

    void Awake()
    {
        Instance = this;
        viewCamera = GetComponent<Camera>(); mouseLook = GetComponent<MouseCameraController>();
        for (int i = 0; i < raycasters.Length; i++)
        {
            RectTransform screen = ScreenFor((DeskViewTarget)i);
            raycasters[i] = screen == null ? new GraphicRaycaster[0] : screen.GetComponentsInChildren<GraphicRaycaster>(true);
            authoredRaycasts[i] = new bool[raycasters[i].Length];
            for (int j = 0; j < raycasters[i].Length; j++)
            { authoredRaycasts[i][j] = raycasters[i][j].enabled; }
        }
        SetPointerTarget(DeskViewTarget.None);
    }

    public RectTransform ScreenFor(DeskViewTarget target)
    {
        int index = (int)target;
        if (index >= 0 && index < 3)
        { return monitorScreens != null && index < monitorScreens.Length ? monitorScreens[index] : null; }
        index -= 3;
        return tablets != null && index >= 0 && index < tablets.Length && tablets[index] != null ? tablets[index].screen : null;
    }

    public bool OwnsTablet(TabletViewController tablet)
    { return tablets != null && System.Array.IndexOf(tablets, tablet) >= 0; }

    /// <summary>번호로 태블릿을 확대한다(0이 왼쪽·Q, 1이 오른쪽·E).</summary>
    public void FocusTabletNumber(int index)
    {
        if (tablets != null && index >= 0 && index < tablets.Length && tablets[index] != null) { FocusTablet(tablets[index]); }
    }

    public void FocusTablet(TabletViewController tablet, bool toggle = false)
    {
        int index = tablets == null ? -1 : System.Array.IndexOf(tablets, tablet);
        if (index < 0)
        { return; }
        DeskViewTarget next = (DeskViewTarget)(3 + index);
        if (toggle && Target == next)
        { ExitView(); }
        else
        { Focus(next); }
    }

    void Update()
    {
        if (PauseMenuController.BlocksInput) { Hovered = DeskViewTarget.None; return; }
        if (!Ready)
        { Hovered = DeskViewTarget.None; return; }
        if (MouseCameraController.TextEntryActive || MouseCameraController.TextEntryEscape)
        { Hovered = DeskViewTarget.None; return; }
        // 쪽지를 들고 읽는 동안에는 화면 단축키가 먹지 않는다.
        if (DoorNotes.Holding || RoomShell.Cinematic)
        { Hovered = DeskViewTarget.None; return; }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (PhoneCallController.IsRaised)
            { PhoneCallController.LowerAny(); }
            else if (PhoneCallController.Instance != null)
            { ExitView(true); PhoneCallController.Instance.Raise(); }
            consumedFrame = Time.frameCount;
            return;
        }
        // 화면 단축키: 1 종목 목록, 2 가운데 화면만, 3 채팅까지. 같은 키를 다시 누르면 자리로 돌아온다.
        DeskViewTarget quick = Input.GetKeyDown(KeyCode.Alpha1) ? DeskViewTarget.Stocks : Input.GetKeyDown(KeyCode.Alpha2) ? DeskViewTarget.Chart : Input.GetKeyDown(KeyCode.Alpha3) ? DeskViewTarget.Chat : DeskViewTarget.None;
        if (quick != DeskViewTarget.None)
        {
            // 휴대폰을 들고 있었으면 내려놓고 곧바로 그 화면을 확대한다.
            if (PhoneCallController.IsRaised)
            { PhoneCallController.LowerAny(); }
            // 시점을 옮겨 놓았으면 같은 키는 그 프리셋으로 되돌린다. 프리셋에 있을 때 누르면 자리로 돌아간다.
            if (Target == quick && hasPreset && !moving && ((transform.position - preset.position).sqrMagnitude > .000001f || Quaternion.Angle(transform.rotation, preset.rotation) > .05f))
            { viewReady = false; turning = false; following = false; SetPointerTarget(DeskViewTarget.None); BeginTransition(preset, focusFieldOfView); }
            else if (Target == quick)
            { ExitView(); }
            else
            { Focus(quick); }
            return;
        }
        // 태블릿: Q가 1번, E가 2번.
        bool first = Input.GetKeyDown(KeyCode.Q), second = Input.GetKeyDown(KeyCode.E);
        if (first || second)
        {
            int index = first ? 0 : 1;
            if (tablets != null && index < tablets.Length && tablets[index] != null)
            { FocusTablet(tablets[index], true); }
            return;
        }
        if (PhoneCallController.IsRaised)
        { Hovered = DeskViewTarget.None; return; }
        if (custom)
        {
            Hovered = DeskViewTarget.None;
            if (Input.GetKeyDown(KeyCode.Escape))
            { ExitView(); }
            return;
        }
        if (IsFocused)
        {
            // 확대한 화면에서 나올 때도 상호작용 키(F)를 쓴다. 들어갈 때와 같은 키다(예전의 Space는 없앴다).
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(GameKeyBindings.Interact) && Time.frameCount != consumedFrame)
            { ExitView(); return; }
            if (MonitorView(Target) && !moving) { MoveMonitorView(); }
            return;
        }
        if (moving)
        { return; }
        // 책상 위의 휴대폰을 겨누고 있으면 상호작용 키와 클릭은 휴대폰의 것이다(태블릿 화면과 겹쳐 보여도 휴대폰이 앞에 있다).
        if (DeskPhone.Aiming)
        { Hovered = DeskViewTarget.None; return; }
        Hovered = FindHovered(MouseCameraController.GetAimScreenPosition());
        if (MouseCameraController.AimIsPointer && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        { Hovered = DeskViewTarget.None; return; }
        // 바라보는 화면은 클릭으로도, 상호작용 키(F)로도 확대한다(서랍·침대·문과 같은 키).
        if (Hovered != DeskViewTarget.None && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(GameKeyBindings.Interact)))
        { Focus(Hovered); }
    }

    /// <summary>소품을 들여다보는 자리로 시선을 옮긴다. 돌아올 때는 ExitView를 쓴다.</summary>
    public bool FocusCustom(Pose pose, float fieldOfView)
    {
        if (!Ready || IsFocused || moving || custom)
        { return false; }
        if (!hasHome)
        {
            home = new Pose(transform.position, transform.rotation); homeFov = viewCamera.fieldOfView;
            homeCursorLocked = MouseCameraController.IsCursorLocked; hasHome = true;
        }
        custom = true; Hovered = DeskViewTarget.None; consumedFrame = Time.frameCount; sway = Vector2.zero;
        mouseLook?.SetViewCursor(false);
        SetPointerTarget(DeskViewTarget.None);
        BeginTransition(pose, fieldOfView);
        return true;
    }

    public void Focus(DeskViewTarget next, bool keepSelection = false)
    {
        if (!Ready || next == DeskViewTarget.None || next == Target || ScreenFor(next) == null)
        { return; }
        custom = false;
        if (PhoneCallController.IsRaised)
        { PhoneCallController.LowerAny(); }
        if (!hasHome)
        {
            home = new Pose(transform.position, transform.rotation); homeFov = viewCamera.fieldOfView;
            homeCursorLocked = MouseCameraController.IsCursorLocked; hasHome = true;
        }
        ReleaseHeldTablet();
        Target = next; Hovered = DeskViewTarget.None; consumedFrame = Time.frameCount; sway = Vector2.zero;
        UnlockPanels(); hasPreset = false; viewKeyHeld = turning = viewReady = following = false;
        // 보통은 누르고 있던 것의 포커스를 푼다. 글을 쓰는 중에 넘어올 때(채팅 입력)는 그대로 둔다.
        if (!keepSelection) { EventSystem.current?.SetSelectedGameObject(null); }
        mouseLook?.SetViewCursor(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SetPointerTarget(DeskViewTarget.None);
        lastAspect = viewCamera.aspect;
        if (next >= DeskViewTarget.TabletOne)
        {
            // 모델의 원점이 화면 중심과 달라도 실제 Canvas를 기준으로 정면에 맞춘다.
            transform.SetPositionAndRotation(home.position, home.rotation); viewCamera.fieldOfView = focusFieldOfView;
            tablets[(int)next - 3].HoldForDeskView();
            moving = true; transitionTime = 0f;
        }
        else if (TryMonitorPose(next, out Pose pose))
        { preset = pose; hasPreset = true; BeginTransition(pose, focusFieldOfView); }
        else
        { ExitView(true); }
    }

    private bool TryMonitorPose(DeskViewTarget target, out Pose pose)
    {
        if (!DeskViewFraming.TryPose(ScreenFor(target), viewCamera, monitorFill, corners, out pose, focusFieldOfView))
        { return false; }
        // 종목 목록(1번)과 채팅(3번)은 그 모니터를 정면에서 본다. 예전에는 가운데 화면도 걸쳐 보이게 시선만 비스듬히 돌려 두었는데,
        // 그러면 오른쪽 버튼으로 끌어 옮길 때 각도가 한 방향으로 매끄럽게 이어지지 않는다. 가운데 화면은 끌어서 보면 된다.
        if (target == DeskViewTarget.Chart)
        {
            // Fit the hanging notes as well as the screen; the actual monitor transform stays untouched.
            Vector3 up = (corners[1] - corners[0]).normalized;
            Vector3 forward = pose.rotation * Vector3.forward;
            float extra = journeyNotes != null && journeyNotes.framingBottom != null ? Mathf.Max(0f, Vector3.Dot(corners[0] - journeyNotes.framingBottom.position, up)) : 0f;
            // 모니터 위의 벽시계도 함께 보이게 위쪽으로도 넓힌다.
            float above = 0f;
            if (wallClock == null) { wallClock = FindAnyObjectByType<MarketWallClock>(); }
            var clockParts = wallClock == null ? null : wallClock.GetComponentsInChildren<Renderer>();
            if (clockParts != null && clockParts.Length > 0)
            {
                var box = clockParts[0].bounds; foreach (var part in clockParts) { box.Encapsulate(part.bounds); }
                above = Mathf.Max(0f, Vector3.Dot(box.center - corners[1], up) + Mathf.Abs(Vector3.Dot(box.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)))) * 1.15f);
            }
            float height = Vector3.Distance(corners[0], corners[1]) + extra + above;
            float width = Vector3.Distance(corners[0], corners[3]);
            float distance = DeskViewFraming.Distance(width, height, focusFieldOfView, viewCamera.aspect, monitorFill);
            Vector3 center = (corners[0] + corners[2]) * .5f - up * (extra * .5f) + up * (above * .5f);
            pose.position = center - forward * Mathf.Max(viewCamera.nearClipPlane + .1f, distance);
        }
        return true;
    }

    public void ExitView(bool immediate = false)
    {
        if (!hasHome)
        { return; }
        UnlockPanels(); hasPreset = false; viewKeyHeld = turning = viewReady = following = false;
        ReleaseHeldTablet(); Target = DeskViewTarget.None; Hovered = DeskViewTarget.None; custom = false;
        consumedFrame = Time.frameCount; sway = Vector2.zero; SetPointerTarget(DeskViewTarget.None);
        if (immediate)
        { transform.SetPositionAndRotation(home.position, home.rotation); CompleteReturn(); }
        else
        { BeginTransition(home, homeFov); }
    }

    private void ReleaseHeldTablet()
    {
        if (Target < DeskViewTarget.TabletOne)
        { return; }
        var tablet = tablets[(int)Target - 3];
        if (tablet != null)
        { tablet.ReturnForDeskView(); }
    }

    private void BeginTransition(Pose pose, float fieldOfView)
    {
        from = new Pose(transform.position, transform.rotation); destination = pose;
        fromFov = viewCamera.fieldOfView; destinationFov = fieldOfView;
        transitionTime = 0f; moving = true;
    }

    void LateUpdate()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (Target >= DeskViewTarget.TabletOne)
        {
            var tablet = tablets[(int)Target - 3];
            if (tablet == null || !tablet.IsHeld)
            { ExitView(true); return; }
            if (moving && tablet.IsFramed)
            { moving = false; SetPointerTarget(Target); }
            // 태블릿을 든 채로도 걸어 다닌다(WASD). 오른쪽 버튼을 누른 채 끌면 둘러본다. 글을 쓰는 중에는 움직이지 않는다.
            // 태블릿을 든 채 채팅 입력 칸을 눌러 글을 쓰기 시작하면 곧바로 채팅 화면(3번 시점)으로 넘어간다. 입력 칸의 포커스는 그대로 둔다.
            if (MouseCameraController.TextEntryActive && EventSystem.current != null)
            {
                var chat = ScreenFor(DeskViewTarget.Chat); var picked = EventSystem.current.currentSelectedGameObject;
                if (chat != null && picked != null && picked.transform.IsChildOf(chat)) { Focus(DeskViewTarget.Chat, true); return; }
            }
            if (mouseLook != null && !MouseCameraController.TextEntryActive)
            {
                // 오른쪽 버튼을 짧게 누르면(끌지 않고 곧 떼면) 태블릿을 화면의 반대쪽으로 옮긴다.
                if (Input.GetMouseButtonDown(1)) { tabletClickAt = Time.unscaledTime; tabletClickFrom = Input.mousePosition; }
                if (Input.GetMouseButtonUp(1) && Time.unscaledTime - tabletClickAt < .28f && ((Vector2)Input.mousePosition - tabletClickFrom).sqrMagnitude < 64f)
                { tablet.FlipSide(); moving = true; consumedFrame = Time.frameCount; }
                if (Input.GetMouseButtonDown(1)) { tabletGrabFrom = Input.mousePosition; }
                if (Input.GetMouseButton(1))
                {
                    // 잡아서 끄는 방식이다: 오른쪽 버튼으로 누른 곳이 커서를 그대로 따라온다. 오른쪽으로 끌면 방이 오른쪽으로, 위로 끌면 위로 밀린다.
                    // 감도 설정을 곱하지 않는다. 마우스가 움직인 화면 거리만큼 정확히 돈다(그래야 잡은 점이 커서 밑에 남는다).
                    Vector2 mouse = Input.mousePosition;
                    if ((mouse - tabletGrabFrom).sqrMagnitude > 0f)
                    {
                        Vector3 before = ViewRay(tabletGrabFrom), after = ViewRay(mouse); tabletGrabFrom = mouse;
                        Vector3 angles = home.rotation.eulerAngles; float pitch = angles.x > 180f ? angles.x - 360f : angles.x;
                        float yaw = Mathf.Atan2(before.x, before.z) - Mathf.Atan2(after.x, after.z);
                        float rise = Mathf.Asin(Mathf.Clamp(after.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(before.y, -1f, 1f));
                        pitch = Mathf.Clamp(pitch + rise * Mathf.Rad2Deg, -80f, 80f);
                        home.rotation = Quaternion.Euler(pitch, angles.y + Mathf.DeltaAngle(0f, yaw * Mathf.Rad2Deg), 0f);
                    }
                }
                mouseLook.WalkFacing(home.rotation.eulerAngles.y);
                home.position = transform.position;
            }
            Vector2 input = MouseCameraController.TextEntryActive || Input.GetMouseButton(0) || Input.GetMouseButton(1) ? Vector2.zero : new Vector2(
                Input.mousePosition.x / Mathf.Max(1f, Screen.width) * 2f - 1f,
                Input.mousePosition.y / Mathf.Max(1f, Screen.height) * 2f - 1f);
            input = Vector2.ClampMagnitude(input, 1f);
            // 끄는 동안에는 흔들림을 멈춰 둔다(흔들림이 풀리며 화면이 따로 움직이면 잡은 점이 커서에서 미끄러진다).
            if (!Input.GetMouseButton(1)) { sway = Vector2.Lerp(sway, input * tabletSwayDegrees, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime)); }
            transform.rotation = home.rotation * Quaternion.Euler(-sway.y, sway.x, 0f);
            return;
        }
        if (IsFocused && !Mathf.Approximately(lastAspect, viewCamera.aspect))
        {
            lastAspect = viewCamera.aspect;
            if (TryMonitorPose(Target, out Pose pose))
            { preset = pose; hasPreset = true; viewReady = false; following = false; SetPointerTarget(DeskViewTarget.None); BeginTransition(pose, focusFieldOfView); }
        }
        if (!moving)
        { return; }
        transitionTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(transitionTime / Mathf.Max(.05f, transitionSeconds));
        float eased = t * t * (3f - 2f * t);
        transform.SetPositionAndRotation(Vector3.Lerp(from.position, destination.position, eased), Quaternion.Slerp(from.rotation, destination.rotation, eased));
        viewCamera.fieldOfView = Mathf.Lerp(fromFov, destinationFov, eased);
        if (t < 1f)
        { return; }
        moving = false;
        if (custom)
        { return; }
        if (!IsFocused)
        { CompleteReturn(); }
        else
        { SetPointerTarget(Target); }
    }

    // ── 모니터 시점에서 직접 움직이기 ──
    /// <summary>휠로 다가가고 물러나기, 오른쪽 버튼으로 끌어 옮기기. 모니터 시점에서 매 프레임 부른다.</summary>
    private void MoveMonitorView()
    {
        Vector2 mouse = Input.mousePosition; bool key = ViewKey;
        // 휠은 확대에 쓴다. 목록이 같이 넘어가지 않게 휠만 잠가 두고, Ctrl을 누르는 동안에는 풀어서 목록을 넘기게 한다.
        if (key != viewKeyHeld || lockedPanels.Count == 0 && !key) { viewKeyHeld = key; if (key) { UnlockPanels(); } else { LockPanels(); } }
        if (Input.GetMouseButtonDown(1)) { turning = true; lastMouse = mouse; }
        else if (turning && !Input.GetMouseButton(1)) { turning = false; }
        if (turning && (mouse - lastMouse).sqrMagnitude > 0f)
        {
            if (!viewReady && !ReadyView()) { return; }
            // 잡아서 끄는 방식이다: 왼쪽으로 끌면 화면이 왼쪽으로 밀려 오른쪽(채팅)이 보인다.
            Vector2 dragged = mouse - lastMouse; lastMouse = mouse;
            FacingPose(turn, out float full);
            float depth = Mathf.Max(.05f, full - local.z), perPixel = 2f * depth * Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float reach = local.z * Mathf.Tan(focusFieldOfView * .5f * Mathf.Deg2Rad);
            // 옆으로 끄는 만큼 시선이 모니터를 따라 돈다. 카메라가 옆으로만 미끄러지면 옆 모니터를 비스듬히 보게 되므로,
            // 좌우 움직임은 언제나 "가운데 정면 ↔ 옆 모니터 정면" 사이의 자리(turn)로 바꾼다. 확대했을수록 같은 거리를 끌어도 조금만 돈다.
            Sideways(-dragged.x * perPixel);
            local.y = Mathf.Clamp(local.y - dragged.y * perPixel, -reach, reach);
            ApplyView(); consumedFrame = Time.frameCount;
        }
        float wheel = Input.mouseScrollDelta.y;
        if (!key && Mathf.Abs(wheel) > .01f) { Zoom(wheel, mouse); }
        FollowView();
    }
    /// <summary>
    /// 지금 프리셋에서 출발하는 "직접 움직이기" 상태를 만든다: 세 모니터를 각각 정면에서 보는 자리와, 지금 자리가 그 가운데 어디인지.
    /// 가운데 자리는 2번 프리셋 그대로다(쪽지와 벽시계까지 보이는 자리). 양옆은 그 모니터를 정면에서 꽉 차게 보는 자리다.
    /// </summary>
    private bool ReadyView()
    {
        if (!hasPreset || !MonitorView(Target)) { return false; }
        for (int i = 0; i < 3; i++)
        {
            var target = (DeskViewTarget)i; var screen = ScreenFor(target);
            if (screen == null) { return false; }
            Pose pose;
            if (target == DeskViewTarget.Chart) { if (!TryMonitorPose(target, out pose)) { return false; } }
            else if (!DeskViewFraming.TryPose(screen, viewCamera, monitorFill, corners, out pose, focusFieldOfView)) { return false; }
            screen.GetWorldCorners(corners);
            facingCenter[i] = (corners[0] + corners[2]) * .5f;
            facing[i] = pose; facingDepth[i] = Mathf.Max(.05f, Vector3.Dot(facingCenter[i] - pose.position, pose.rotation * Vector3.forward));
        }
        facingSpan[0] = Vector3.Distance(facingCenter[1], facingCenter[0]); facingSpan[2] = Vector3.Distance(facingCenter[1], facingCenter[2]); facingSpan[1] = Mathf.Max(facingSpan[0], facingSpan[2]);
        turn = Target == DeskViewTarget.Stocks ? -1f : Target == DeskViewTarget.Chat ? 1f : 0f;
        Pose start = FacingPose(turn, out _);
        twist = Quaternion.Inverse(start.rotation) * transform.rotation; twistKeep = 1f;
        local = Quaternion.Inverse(transform.rotation) * (transform.position - start.position);
        aim = new Pose(transform.position, transform.rotation);
        viewReady = true;
        return true;
    }
    /// <summary>turn(-1~+1)에 해당하는 정면 자리와, 거기서 화면까지의 거리.</summary>
    private Pose FacingPose(float amount, out float depth)
    {
        int side = amount < 0f ? 0 : 2; float share = Mathf.Abs(amount);
        depth = Mathf.Lerp(facingDepth[1], facingDepth[side], share);
        return new Pose(Vector3.Lerp(facing[1].position, facing[side].position, share), Quaternion.Slerp(facing[1].rotation, facing[side].rotation, share));
    }
    /// <summary>지금 상태(turn, local)가 가리키는 자리. 카메라는 여기를 향해 부드럽게 따라간다.</summary>
    private Pose aim; private bool following;
    private void ApplyView()
    {
        Pose pose = FacingPose(turn, out _);
        Quaternion rotation = pose.rotation * Quaternion.Slerp(Quaternion.identity, twist, twistKeep);
        aim = new Pose(pose.position + rotation * local, rotation); following = true;
    }
    /// <summary>끌거나 확대한 자리로 카메라를 부드럽게 옮긴다. 위치와 각도가 같은 빠르기로 함께 따라간다.</summary>
    private void FollowView()
    {
        if (!following) { return; }
        float pull = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, aim.position, pull), Quaternion.Slerp(transform.rotation, aim.rotation, pull));
        if ((transform.position - aim.position).sqrMagnitude < .0000001f && Quaternion.Angle(transform.rotation, aim.rotation) < .01f)
        { transform.SetPositionAndRotation(aim.position, aim.rotation); following = false; }
    }
    /// <summary>마우스 커서가 가리키는 곳을 향해 다가가거나 물러난다. 원래 시점보다 멀어지지는 않고, 원래 보이던 범위 밖으로 나가지도 않는다.</summary>
    private void Zoom(float wheel, Vector2 mouse)
    {
        if (!viewReady && !ReadyView()) { return; }
        FacingPose(turn, out float full);
        float depth = full - local.z;
        if (depth <= .01f) { return; }
        // 설정의 "휠 감도"(기본 3)가 확대 속도를 정한다.
        float wanted = Mathf.Clamp(depth * Mathf.Pow(zoomStep, wheel * GameSettings.Current.wheel / 3f), full * nearestZoom, full), factor = wanted / depth;
        if (Mathf.Approximately(factor, 1f)) { return; }
        // 커서가 가리키는 화면 위의 점(카메라 기준). 그 점이 커서 아래에 그대로 있도록 다가간다.
        Vector3 direction = Quaternion.Inverse(transform.rotation) * viewCamera.ScreenPointToRay(mouse).direction;
        // 커서가 가리키는 방향은 지금 화면에서 읽는다. 다가갈 거리는 가려는 자리(local)에서 잰다.
        if (direction.z < .01f) { return; }
        Vector3 aim = local + direction * (depth / direction.z);
        local = aim + (local - aim) * factor;
        local.z = full - wanted;
        // 다가간 만큼만 옆으로 벗어날 수 있다. 끝까지 물러나면 원래 자리다.
        float reach = (full - wanted) * Mathf.Tan(focusFieldOfView * .5f * Mathf.Deg2Rad);
        local.y = Mathf.Clamp(local.y, -reach, reach);
        // 커서 쪽으로 다가가며 생긴 좌우 치우침도 시선을 돌리는 것으로 바꾼다(비스듬히 보지 않게).
        float aside = local.x; local.x = 0f; Sideways(aside);
        ApplyView(); consumedFrame = Time.frameCount;
    }
    /// <summary>
    /// 화면을 옆으로 world만큼 옮기고 싶다(오른쪽이 +). 그만큼 시선을 옆 모니터 쪽으로 돌린다.
    /// 가운데 모니터의 한가운데에서 옆 모니터의 한가운데까지가 turn 1이다. 끝(옆 모니터 정면)에 닿으면 더 가지 않는다.
    /// </summary>
    private void Sideways(float world)
    {
        if (Mathf.Approximately(world, 0f)) { return; }
        // 지금 가운데보다 왼쪽에 있으면 왼쪽 모니터까지의 거리로, 오른쪽이면 오른쪽 모니터까지의 거리로 잰다. 한가운데에서는 가려는 쪽으로.
        int side = turn < 0f || Mathf.Approximately(turn, 0f) && world < 0f ? 0 : 2;
        float before = turn;
        turn = Mathf.Clamp(turn + world / Mathf.Max(.05f, facingSpan[side]), -1f, 1f);
        // 가운데를 넘어가면 남은 만큼은 반대쪽 모니터까지의 거리로 다시 잰다.
        if (before != 0f && Mathf.Sign(turn) != Mathf.Sign(before) && turn != 0f)
        { int other = turn < 0f ? 0 : 2; turn = Mathf.Clamp(turn * facingSpan[side] / Mathf.Max(.05f, facingSpan[other]), -1f, 1f); }
        // 돌아가는 만큼 프리셋의 비틀림을 풀어 정면으로 맞춘다.
        twistKeep = Mathf.Max(0f, twistKeep - Mathf.Abs(turn - before) * 3f);
    }
    /// <summary>
    /// 옆으로 벗어날 수 있는 만큼을 정한다. 옆 모니터 쪽으로 넘어갈수록 바깥쪽 여유를 줄여서,
    /// 끝까지 가면 그 모니터가 화면 한가운데에 오는 데서 멈춘다(그 너머의 벽은 보이지 않는다). 안쪽으로는 확대한 만큼 움직일 수 있다.
    /// </summary>
    private void ClampSideways(float reachX)
    {
        float left = -reachX * (1f + Mathf.Min(0f, turn)), right = reachX * (1f - Mathf.Max(0f, turn));
        local.x = Mathf.Clamp(local.x, left, right);
    }
    /// <summary>세 모니터의 목록(스크롤되는 화면)이 휠에 넘어가지 않게 한다. 끌어서 넘기는 것은 그대로 된다.</summary>
    private void LockPanels()
    {
        for (int i = 0; i < 3; i++)
        {
            var screen = ScreenFor((DeskViewTarget)i);
            if (screen == null) { continue; }
            foreach (var panel in screen.GetComponentsInChildren<ScrollRect>(true))
            {
                if (lockedPanels.ContainsKey(panel)) { continue; }
                lockedPanels[panel] = panel.scrollSensitivity; panel.scrollSensitivity = 0f;
            }
        }
    }
    private void UnlockPanels()
    {
        foreach (var pair in lockedPanels) { if (pair.Key != null) { pair.Key.scrollSensitivity = pair.Value; } }
        lockedPanels.Clear();
    }

    private void CompleteReturn()
    {
        moving = false; hasHome = false; viewCamera.fieldOfView = homeFov;
        bool locked = homeCursorLocked && (mouseLook == null || !mouseLook.IsPaused);
        if (mouseLook != null)
        { mouseLook.SyncViewAngles(); mouseLook.SetViewCursor(locked); }
        else
        { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    }

    private void SetPointerTarget(DeskViewTarget target)
    {
        for (int i = 0; i < raycasters.Length; i++)
        {
            if (raycasters[i] == null)
            { continue; }
            for (int j = 0; j < raycasters[i].Length; j++)
            {
                var raycaster = raycasters[i][j];
                // 모니터를 보는 시점에서는 세 모니터가 모두 조작된다(시점을 옮겨 옆 모니터도 쓸 수 있다).
                // 태블릿을 들고 있을 때도 뒤의 모니터를 눌러 쓸 수 있다(태블릿이 앞에 있어서 겹친 자리는 태블릿이 받는다).
                bool enabled = (i == (int)target || i <= (int)DeskViewTarget.Chat && (MonitorView(target) || target >= DeskViewTarget.TabletOne)) && authoredRaycasts[i][j];
                if (raycaster != null && raycaster.enabled != enabled)
                { raycaster.enabled = enabled; }
            }
        }
    }

    private DeskViewTarget FindHovered(Vector2 point)
    {
        Ray ray = viewCamera.ScreenPointToRay(point);
        float nearest = float.MaxValue;
        DeskViewTarget result = DeskViewTarget.None;
        for (int i = 0; i < 5; i++)
        {
            var screen = ScreenFor((DeskViewTarget)i);
            if (screen == null || !screen.gameObject.activeInHierarchy || Vector3.Dot(ray.direction, screen.forward) <= .01f)
            { continue; }
            Plane plane = new Plane(screen.forward, screen.position);
            if (plane.Raycast(ray, out float distance) && distance < nearest && screen.rect.Contains(screen.InverseTransformPoint(ray.GetPoint(distance))))
            { nearest = distance; result = (DeskViewTarget)i; }
        }
        return result;
    }

    void OnDisable()
    {
        UnlockPanels();
        ExitView(true);
        for (int i = 0; i < raycasters.Length; i++)
        {
            if (raycasters[i] == null)
            { continue; }
            for (int j = 0; j < raycasters[i].Length; j++)
            {
                if (raycasters[i][j] != null)
                { raycasters[i][j].enabled = authoredRaycasts[i][j]; }
            }
        }
        if (Instance == this)
        { Instance = null; }
    }
}
