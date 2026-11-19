using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기존 모니터/태블릿의 보기와 입력만 관리한다. 시뮬레이션 시간과 경제 상태는 건드리지 않는다.</summary>
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
    [Tooltip("종목 목록을 볼 때의 카메라 월드 Y 회전. 중앙 모니터도 함께 보이게 한다.")]
    public float stocksViewYaw = -20f;
    [Tooltip("채팅을 볼 때의 카메라 월드 Y 회전.")]
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
    public static bool BlockChartPointer => Instance != null && (!MonitorView(Instance.Target) || Instance.moving || Instance.consumedFrame == Time.frameCount);
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
        if (MouseCameraController.TextEntryActive)
        { Hovered = DeskViewTarget.None; return; }
        // 쪽지를 들고 읽는 동안에는 화면 단축키가 먹지 않는다(Space는 쪽지를 내려놓는 데 쓴다).
        if (DoorNotes.Holding || RoomShell.Cinematic)
        { Hovered = DeskViewTarget.None; return; }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (PhoneCallController.IsRaised)
            { PhoneCallController.Instance.Lower(); }
            else if (PhoneCallController.Instance != null)
            { ExitView(true); PhoneCallController.Instance.Raise(); }
            consumedFrame = Time.frameCount;
            return;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (PhoneCallController.IsRaised)
            { PhoneCallController.Instance.Lower(); }
            else if (IsFocused || moving || custom)
            { ExitView(); }
            else
            { Focus(DeskViewTarget.Chart); }
            return;
        }
        // 화면 단축키: 1 종목 목록, 2 가운데 화면만, 3 채팅까지. 같은 키를 다시 누르면 자리로 돌아온다.
        DeskViewTarget quick = Input.GetKeyDown(KeyCode.Alpha1) ? DeskViewTarget.Stocks : Input.GetKeyDown(KeyCode.Alpha2) ? DeskViewTarget.Chart : Input.GetKeyDown(KeyCode.Alpha3) ? DeskViewTarget.Chat : DeskViewTarget.None;
        if (quick != DeskViewTarget.None)
        {
            if (PhoneCallController.IsRaised)
            { PhoneCallController.Instance.Lower(); }
            if (Target == quick)
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
            if (Input.GetKeyDown(KeyCode.Escape))
            { ExitView(); return; }
            return;
        }
        if (moving)
        { return; }
        Hovered = FindHovered(MouseCameraController.GetAimScreenPosition());
        if (!MouseCameraController.IsCursorLocked && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        { Hovered = DeskViewTarget.None; return; }
        if (Hovered != DeskViewTarget.None && Input.GetMouseButtonDown(0))
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

    public void Focus(DeskViewTarget next)
    {
        if (!Ready || next == DeskViewTarget.None || next == Target || ScreenFor(next) == null)
        { return; }
        custom = false;
        if (PhoneCallController.IsRaised)
        { PhoneCallController.Instance.Lower(); }
        if (!hasHome)
        {
            home = new Pose(transform.position, transform.rotation); homeFov = viewCamera.fieldOfView;
            homeCursorLocked = MouseCameraController.IsCursorLocked; hasHome = true;
        }
        ReleaseHeldTablet();
        Target = next; Hovered = DeskViewTarget.None; consumedFrame = Time.frameCount; sway = Vector2.zero;
        EventSystem.current?.SetSelectedGameObject(null);
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
        { BeginTransition(pose, focusFieldOfView); }
        else
        { ExitView(true); }
    }

    private bool TryMonitorPose(DeskViewTarget target, out Pose pose)
    {
        if (!DeskViewFraming.TryPose(ScreenFor(target), viewCamera, monitorFill, corners, out pose, focusFieldOfView))
        { return false; }
        if (target == DeskViewTarget.Stocks || target == DeskViewTarget.Chat)
        {
            Vector3 angles = pose.rotation.eulerAngles;
            angles.y = target == DeskViewTarget.Stocks ? stocksViewYaw : chatViewYaw;
            pose.rotation = Quaternion.Euler(angles);
        }
        else if (target == DeskViewTarget.Chart)
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
            Vector2 input = MouseCameraController.TextEntryActive || Input.GetMouseButton(0) ? Vector2.zero : new Vector2(
                Input.mousePosition.x / Mathf.Max(1f, Screen.width) * 2f - 1f,
                Input.mousePosition.y / Mathf.Max(1f, Screen.height) * 2f - 1f);
            input = Vector2.ClampMagnitude(input, 1f);
            sway = Vector2.Lerp(sway, input * tabletSwayDegrees, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));
            transform.rotation = home.rotation * Quaternion.Euler(-sway.y, sway.x, 0f);
            return;
        }
        if (IsFocused && !Mathf.Approximately(lastAspect, viewCamera.aspect))
        {
            lastAspect = viewCamera.aspect;
            if (TryMonitorPose(Target, out Pose pose))
            { SetPointerTarget(DeskViewTarget.None); BeginTransition(pose, focusFieldOfView); }
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
                // 종목 목록·채팅 시점에서도 가운데(차트) 화면이 보이므로 그 화면의 입력도 함께 켠다.
                bool enabled = (i == (int)target || i == (int)DeskViewTarget.Chart && MonitorView(target)) && authoredRaycasts[i][j];
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
