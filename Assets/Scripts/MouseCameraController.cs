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
            return textEntry != null && textEntry.isFocused;
        }
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
        if (!IsCursorLocked || _paused || DeskViewController.OwnsView || TabletViewController.IsTabletHeld || PhoneCallController.IsRaised || DisplayAimCursor.HasPointerCapture || ChartManager.HasPointerCapture)
        { return; }

        ApplyMouseWheelFov();
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        _currentAngleY += mouseX * sensitivity * GameSettings.Current.sensitivity;
        _currentAngleX -= mouseY * sensitivity * GameSettings.Current.sensitivity;
        _currentAngleX = Mathf.Clamp(_currentAngleX, minAngleX, maxAngleX);
        _currentAngleY = Mathf.Clamp(_currentAngleY, minAngleY, maxAngleY);
        transform.eulerAngles = new Vector3(_currentAngleX, _currentAngleY, 0f);
    }

    private void ApplyMouseWheelFov()
    {
        if (_cam == null || TabletViewController.IsTabletHeld)
        { return; }
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(wheel, 0f))
        {
            wheel = Input.GetAxis("Mouse ScrollWheel") * 10f;
        }
        if (Mathf.Approximately(wheel, 0f))
        { return; }
        _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - wheel * GameSettings.Current.wheel, MinFov, MaxFov);
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
