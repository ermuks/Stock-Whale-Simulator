using UnityEngine;

/// <summary>
/// 마우스 드래그로 카메라 회전. X 각도 -90~90, Y 각도 -50~50 제한.
/// 메인 카메라 또는 카메라 부모 오브젝트에 붙여 사용하세요.
/// </summary>
public class MouseCameraController : MonoBehaviour
{
    [Header("감도")]
    [Tooltip("마우스 이동에 따른 회전 감도")]
    public float sensitivity = 2f;
    [Header("마우스 휠 FOV")]
    [Min(0.1f)]
    [Tooltip("휠 한 칸당 FOV 변화량. 휠 입력을 반전하여 적용")]
    public float wheelFovSensitivity = 3f;
    private const float MinFov = 10f, MaxFov = 45f;
    [Header("각도 제한 (도)")]
    [Tooltip("X축 회전 (위/아래) 최소·최대 각도")]
    public float minAngleX = -90f;
    public float maxAngleX = 90f;
    [Tooltip("Y축 회전 (좌/우) 최소·최대 각도")]
    public float minAngleY = -50f;
    public float maxAngleY = 50f;
    // 현재 누적 각도 (오일러 랩 문제 방지용)
    private float _currentAngleX;
    private float _currentAngleY;
    [Header("커서/일시정지")]
    [Tooltip("시작 시 커서를 가운데로 고정하고 숨김")]
    public bool lockCursorOnStart = true;
    [Tooltip("일시정지(커서 토글) 키. useSavedPauseKey가 true면 GameKeyBindings.Pause 사용")]
    public KeyCode pauseKey = KeyCode.P;
    [Tooltip("true면 설정에 저장된 일시정지 키 사용 (GameKeyBindings.Pause)")]
    public bool useSavedPauseKey = false;
    [Tooltip("커서를 풀었을 때(TimeScale=0)로 일시정지할지 여부")]
    public bool pauseWhenCursorUnlocked = true;
    private bool _cursorLocked;
    private bool _paused;
    public bool IsPaused => _paused;
    /// <summary>커서 잠금 여부. 에임(화면 중앙) 사용 시 true.</summary>
    public static bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;
    private static GameObject lastSelected;
    private static TMPro.TMP_InputField textEntry;
    public static bool TextEntryActive
    {
        get
        {
            var selected = UnityEngine.EventSystems.EventSystem.current == null ? null : UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (lastSelected != selected)
            { lastSelected = selected; textEntry = selected == null ? null : selected.GetComponentInParent<TMPro.TMP_InputField>(); }
            bool active = textEntry != null && textEntry.isFocused;
            if (active) { textEntryFrame = Time.frameCount; }
            return active;
        }
    }
    private static int textEntryFrame = -10;
    /// <summary>지금 글을 쓰고 있는 입력 칸(없으면 null).</summary>
    public static TMPro.TMP_InputField ActiveTextEntry => TextEntryActive ? textEntry : null;
    /// <summary>
    /// 글을 쓰는 중에 Esc를 눌렀는가. 이때의 Esc는 입력을 끝내는 데만 쓰이고, 메뉴를 열거나 화면에서 빠져나오지 않는다.
    /// 입력 칸은 같은 프레임에 스스로 Esc를 받아 꺼질 수 있으므로, 바로 앞 프레임까지 쓰고 있었으면 쓰는 중으로 본다.
    /// </summary>
    public static bool TextEntryEscape => Input.GetKeyDown(KeyCode.Escape) && (TextEntryActive || Time.frameCount - textEntryFrame <= 1);
    /// <summary>글쓰기를 끝낸다(쓰던 글은 그대로 둔다).</summary>
    public static void EndTextEntry()
    {
        if (textEntry != null && textEntry.isFocused) { textEntry.DeactivateInputField(); }
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null) { events.SetSelectedGameObject(null); }
    }

    /// <summary>에임/마우스 스크린 좌표. 잠금 시 화면 중앙, 아니면 마우스 위치.</summary>
    public static Vector2 GetAimScreenPosition()
    { return IsCursorLocked ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : (Vector2)Input.mousePosition; }

    private Camera _cam;
    void Start()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            _cam = GetComponentInChildren<Camera>();
        }

        if (_cam != null)
        {
            _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView, MinFov, MaxFov);
        }
        SyncViewAngles();
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    void Update()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (TextEntryActive)
        { return; }
        KeyCode effectivePause = useSavedPauseKey ? GameKeyBindings.Pause : pauseKey;
        if (effectivePause == KeyCode.Space || effectivePause == KeyCode.Tab)
        { effectivePause = KeyCode.P; }
        if (Input.GetKeyDown(effectivePause))
        {
            TogglePause();
        }
        if (!IsCursorLocked || _paused || DeskViewController.OwnsView || TabletViewController.IsTabletHeld || PhoneCallController.IsRaised || DisplayAimCursor.HasPointerCapture || ChartManager.HasPointerCapture || DoorNotes.Holding || RoomShell.Cinematic)
        { return; }

        // 걸어 다니는 동안에는 휠로 확대하지 않는다. 확대는 모니터를 보는 시점에서 Ctrl + 휠로 한다(DeskViewController).
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        _currentAngleY += mouseX * sensitivity * GameSettings.Current.sensitivity;
        _currentAngleX -= mouseY * sensitivity * GameSettings.Current.sensitivity;
        // 위아래는 80도까지만 젖혀지고, 좌우는 제한 없이 몇 바퀴든 돈다.
        _currentAngleX = Mathf.Clamp(_currentAngleX, Mathf.Max(minAngleX, -80f), Mathf.Min(maxAngleX, 80f));
        _currentAngleY = Mathf.Repeat(_currentAngleY + 180f, 360f) - 180f;
        transform.eulerAngles = new Vector3(_currentAngleX, _currentAngleY, 0f);
        Walk();
    }

    /// <summary>걸어 다닐 수 있는 네모(x, z의 최소·최대). RoomShell이 방 크기를 재서 정한다.</summary>
    public static Vector2 WalkMin, WalkMax;
    public static bool WalkEnabled;
    private float strideLeft = 1f;
    /// <summary>걸어 들어갈 수 없는 자리들(x, z). 침대가 여기에 들어 있다.</summary>
    public static readonly System.Collections.Generic.List<Rect> WalkBlocks = new();
    private static bool Blocked(float x, float z)
    {
        foreach (var block in WalkBlocks) { if (x > block.xMin && x < block.xMax && z > block.yMin && z < block.yMax) { return true; } }
        return false;
    }
    [Tooltip("걷는 빠르기(초당 거리). Shift를 누르면 조금 빨라진다.")]
    public float walkSpeed = 4.2f;
    /// <summary>WASD로 바닥을 걷는다. 눈높이는 그대로이고, 벽과 책상 앞에서 멈춘다.</summary>
    private void Walk() { WalkFacing(_currentAngleY); }
    /// <summary>yaw(도) 쪽을 앞으로 삼아 WASD로 걷는다. 태블릿을 든 채 걸을 때는 DeskViewController가 부른다.</summary>
    public void WalkFacing(float facing)
    {
        if (!WalkEnabled) { return; }
        float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f), side = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        if (forward == 0f && side == 0f) { return; }
        float yaw = facing * Mathf.Deg2Rad;
        Vector3 ahead = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), right = new Vector3(ahead.z, 0f, -ahead.x);
        Vector3 step = Vector3.ClampMagnitude(ahead * forward + right * side, 1f) * walkSpeed * (Input.GetKey(KeyCode.LeftShift) ? 1.6f : 1f) * Time.unscaledDeltaTime;
        // 가로와 세로를 따로 옮긴다. 막힌 쪽만 멈추고 벽을 따라 미끄러진다.
        Vector3 position = transform.position;
        float x = Mathf.Clamp(position.x + step.x, WalkMin.x, WalkMax.x);
        if (!Blocked(x, position.z)) { position.x = x; }
        float z = Mathf.Clamp(position.z + step.z, WalkMin.y, WalkMax.y);
        if (!Blocked(position.x, z)) { position.z = z; }
        // 실제로 움직인 거리만큼 발소리가 난다(벽에 막혀 제자리면 나지 않는다).
        strideLeft -= Vector3.Distance(transform.position, position);
        if (strideLeft <= 0f) { strideLeft = Input.GetKey(KeyCode.LeftShift) ? 2.6f : 2.1f; Sfx.Play("Footstep", .5f, null, .08f); }
        transform.position = position;
    }

    public void CompleteInitialSetup()
    {
        SetCursorLocked(lockCursorOnStart);
    }

    public void TogglePause()
    {
        _paused = !_paused;
        SetViewCursor(!_paused && !DeskViewController.OwnsView && !TabletViewController.IsTabletHeld && !PhoneCallController.IsRaised);
        Time.timeScale = _paused ? 0f : 1f;
    }

    public void SyncViewAngles()
    {
        Vector3 euler = transform.eulerAngles;
        _currentAngleX = NormalizeAngle(euler.x); _currentAngleY = NormalizeAngle(euler.y);
    }

    /// <summary>장치 보기의 커서 해제는 시장의 시간을 멈추지 않는다.</summary>
    public void SetViewCursor(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void SetCursorLocked(bool locked)
    {
        _cursorLocked = locked;
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked; // 커서를 화면 중앙에 고정
            Cursor.visible = false;
            Time.timeScale = _paused ? 0f : 1f;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (pauseWhenCursorUnlocked)
            {
                Time.timeScale = 0f;
            }
        }
    }

    /// <summary>
    /// 0~360 범위를 -180~180으로 변환
    /// </summary>
    static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    void OnDisable()
    {
        // 스크립트 비활성화 시 커서 복구
        if (_cursorLocked)
        {
            SetCursorLocked(false);
        }
    }
}
