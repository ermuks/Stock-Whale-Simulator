using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 태블릿(Tablet 레이어) 클릭 시 카메라 자식으로 가져와 지정 위치에 고정, ESC로 원위치 복귀.
/// 고정 중에는 할당한 Scroll Rect에 마우스 휠 입력을 넣어 스크롤 가능.
/// </summary>
public class TabletViewController : MonoBehaviour
{
    /// <summary>태블릿이 카메라에 고정된 상태인지. 카메라 감도 조절 등에서 참조.</summary>
    public static bool IsTabletHeld { get; private set; }
    [Tooltip("가져올 태블릿 오브젝트")]
    public Transform tabletObject;
    [Tooltip("클릭 감지에 쓸 카메라. 비어 있으면 Camera.main")]
    public Camera viewCamera;
    [Tooltip("기존 태블릿 화면 Canvas. 모델 원점 대신 이 영역을 정면에 맞춘다.")]
    public RectTransform screen;
    [Range(.65f, .94f)] public float screenFill = .86f;
    // 들고 볼 때의 자세. 화면을 정면으로 가득 채우지 않고, 아래쪽에 비스듬히 들어 모니터가 위와 옆으로 보이게 한다.
    [Tooltip("들었을 때 태블릿 화면이 시야에서 차지하는 비율(screenFill보다 크게는 안 된다)")]
    [Range(.45f, .94f)] public float heldFill = .68f;
    [Tooltip("위쪽이 뒤로 넘어가는 각도(도). 손에 들고 내려다보는 느낌")]
    [Range(0f, 40f)] public float heldPitch = 15f;
    [Tooltip("안쪽으로 트는 각도(도). 책상 왼쪽 태블릿은 왼쪽에, 오른쪽 태블릿은 오른쪽에 들린다")]
    [Range(0f, 30f)] public float heldYaw = 9f;
    [Tooltip("아래로 내리는 정도(1이면 아래 끝이 시야 아래 끝에 닿는다)")]
    [Range(0f, 1f)] public float heldDrop = .9f;
    [Tooltip("옆으로 치우치는 정도(1이면 옆 끝이 시야 옆 끝에 닿는다)")]
    [Range(0f, 1f)] public float heldAside = .7f;
    private float _side = 1f, _lastSize;
    private string SideKey => "DonDon.TabletSide." + (tabletObject != null ? tabletObject.name : name);
    /// <summary>들고 있는 태블릿을 화면의 반대쪽으로 옮긴다(왼쪽 ↔ 오른쪽). 고른 쪽은 기억해 둔다.</summary>
    public void FlipSide()
    {
        if (!_isHeld || tabletObject == null || viewCamera == null) { return; }
        _side = -_side;
        PlayerPrefs.SetInt(SideKey, PlayerPrefs.GetInt(SideKey, 0) == 1 ? 0 : 1);
        _holdStartPosition = tabletObject.localPosition; _holdStartRotation = tabletObject.localRotation; _holdStartScale = tabletObject.localScale;
        RefreshFramedPose();
        // 옮겨 가는 모습을 잠깐 보여 준다(장면에서 즉시 이동으로 해 두었어도 여기서는 미끄러진다).
        _moveT = 0f; _flipping = true;
    }
    private bool _flipping;
    [Tooltip("Raycast 대상. 비어 있으면 'Tablet' 레이어")]
    public LayerMask tabletLayerMask;
    public KeyCode focusKey = KeyCode.Alpha1;

    [Header("카메라에 고정될 위치 (로컬)")]
    [Tooltip("태블릿이 카메라 자식이 됐을 때의 로컬 위치")]
    public Vector3 holdLocalPosition = new Vector3(0f, 0f, 1.5f);
    [Tooltip("태블릿이 카메라 자식이 됐을 때의 로컬 회전 (도)")]
    public Vector3 holdLocalEuler = Vector3.zero;

    [Header("이동 (선택)")]
    [Tooltip("0이면 즉시 이동. 양수면 초당 이 속도로 보간")]
    public float moveSpeed = 0f;

    [Header("태블릿 안 UI 스크롤 (고정 중에만)")]
    [Tooltip("마우스 휠로 스크롤할 Scroll Rect. 태블릿 내부 Scroll View에 붙어 있는 것 할당")]
    public ScrollRect scrollRect;
    [Tooltip("휠 스크롤 감도. 휠 1눌림당 verticalNormalizedPosition 변화량")]
    public float scrollSensitivity = 0.2f;
    [Tooltip("켜면 태블릿 든 동안 커서 표시·잠금 해제 → 휠/클릭이 UI에 정상 전달됨. 커서 숨김 상태에서는 휠이 안 들어올 수 있음")]
    public bool unlockCursorWhileHoldingTablet = true;

    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Transform _originalParent;
    private Vector3 _originalScale, _heldScale;
    private bool _isHeld;
    private float _moveT;
    private bool _wasCursorLockedBeforeHold;
    private Coroutine _returnRoutine;
    private bool _managedByDesk;
    private Vector3 _targetPosition, _targetScale, _holdStartPosition, _holdStartScale, _screenCenter;
    private Quaternion _targetRotation, _holdStartRotation, _screenRotation;
    private float _screenWidth, _screenHeight, _lastAspect, _lastFov;
    private readonly Vector3[] _screenCorners = new Vector3[4];
    public bool IsHeld => _isHeld;
    public bool IsReturning => _returnRoutine != null;
    public bool IsFramed => _isHeld && !_flipping && (moveSpeed <= 0f || _moveT >= 1f);
    public void FocusTablet()
    {
        if (DeskViewController.Instance != null && DeskViewController.Instance.OwnsTablet(this))
        { DeskViewController.Instance.FocusTablet(this); return; }
        if (!_isHeld && _returnRoutine == null && !IsTabletHeld && !PhoneCallController.IsRaised)
        { BringToView(); }
    }
    public void ReturnTablet()
    {
        if (_managedByDesk && DeskViewController.Instance != null)
        { DeskViewController.Instance.ExitView(); return; }
        if (_isHeld)
        { ReleaseTablet(); }
    }
    public void ToggleTablet()
    {
        if (DeskViewController.Instance != null && DeskViewController.Instance.OwnsTablet(this))
        { DeskViewController.Instance.FocusTablet(this, true); return; }
        if (MouseCameraController.TextEntryActive || PhoneCallController.IsRaised)
        { return; }
        if (_isHeld)
        { ReleaseTablet(); }
        else
        { FocusTablet(); }
    }

    void Start()
    {
        if (viewCamera == null)
        { viewCamera = Camera.main; }
        if (tabletLayerMask == 0)
        { tabletLayerMask = LayerMask.GetMask("Tablet"); }
        if (screen == null && tabletObject != null)
        {
            Canvas canvas = tabletObject.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            { screen = canvas.transform as RectTransform; }
        }
    }

    void Update()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (tabletObject == null || viewCamera == null)
        { return; }
        if (StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance != null && DeskViewController.Instance.OwnsTablet(this))
        {
            if (_isHeld)
            { UpdateHoldPosition(); }
            return;
        }
        if (MouseCameraController.TextEntryActive || MouseCameraController.TextEntryEscape || PhoneCallController.IsRaised || IsTabletHeld && !_isHeld)
        { return; }

        if (Input.GetKeyDown(focusKey))
        { ToggleTablet(); return; }
        if (_isHeld)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            { ReleaseTablet(); }
            else
            {
                UpdateHoldPosition();
                // 커서 풀었을 땐 EventSystem이 휠 전달하므로, 커서 숨긴 상태에서만 수동 적용
                if (!unlockCursorWhileHoldingTablet)
                { ApplyMouseWheelToScroll(); }
            }
            return;
        }

        if (_returnRoutine == null && Input.GetMouseButtonDown(0))
        {
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, viewCamera.farClipPlane, tabletLayerMask))
            {
                Transform hitRoot = hit.collider.transform;
                while (hitRoot != null && hitRoot != tabletObject)
                { hitRoot = hitRoot.parent; }
                if (hitRoot == tabletObject)
                { BringToView(); }
            }
        }
    }

    public void HoldForDeskView()
    {
        if (_isHeld || tabletObject == null || viewCamera == null)
        { return; }
        if (_returnRoutine != null)
        { StopCoroutine(_returnRoutine); _returnRoutine = null; RestoreOriginalPose(); }
        BringToView(true);
    }

    public void ReturnForDeskView()
    {
        if (!_isHeld && _returnRoutine == null)
        { return; }
        if (_returnRoutine != null)
        { StopCoroutine(_returnRoutine); _returnRoutine = null; }
        _isHeld = false; IsTabletHeld = false; _managedByDesk = false;
        RestoreOriginalPose();
    }

    void BringToView(bool managedByDesk = false)
    {
        _managedByDesk = managedByDesk;
        _originalPosition = tabletObject.position;
        _originalRotation = tabletObject.rotation;
        _originalParent = tabletObject.parent;
        _originalScale = tabletObject.localScale;
        tabletObject.SetParent(viewCamera.transform, true);
        _heldScale = tabletObject.localScale;
        // 책상에서 어느 쪽에 놓여 있었는지: 그쪽으로 치우쳐 든다. 짧게 오른쪽 클릭해서 반대쪽으로 바꿔 두었으면 그쪽으로 든다(다음에도 기억한다).
        _side = (tabletObject.localPosition.x < 0f ? -1f : 1f) * (PlayerPrefs.GetInt(SideKey, 0) == 1 ? -1f : 1f);
        CacheScreenGeometry();
        RefreshFramedPose();
        _holdStartPosition = tabletObject.localPosition; _holdStartRotation = tabletObject.localRotation; _holdStartScale = tabletObject.localScale;
        _isHeld = true;
        IsTabletHeld = true;
        _moveT = 0f;

        if (unlockCursorWhileHoldingTablet && !_managedByDesk)
        {
            _wasCursorLockedBeforeHold = MouseCameraController.IsCursorLocked;
            if (_wasCursorLockedBeforeHold)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if (moveSpeed <= 0f)
        {
            tabletObject.localPosition = _targetPosition;
            tabletObject.localRotation = _targetRotation;
            tabletObject.localScale = _targetScale;
        }
    }

    private float Framing => Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad) / Mathf.Tan(22.5f * Mathf.Deg2Rad);
    private Vector3 FramedPosition() => new Vector3(holdLocalPosition.x * Framing, holdLocalPosition.y * Framing, holdLocalPosition.z);
    private void CacheScreenGeometry()
    {
        if (screen == null)
        { return; }
        screen.GetWorldCorners(_screenCorners);
        _screenCenter = tabletObject.InverseTransformPoint((_screenCorners[0] + _screenCorners[2]) * .5f);
        _screenWidth = Vector3.Distance(_screenCorners[0], _screenCorners[3]);
        _screenHeight = Vector3.Distance(_screenCorners[0], _screenCorners[1]);
        _screenRotation = Quaternion.Inverse(tabletObject.rotation) * screen.rotation;
    }

    private void RefreshFramedPose()
    {
        _lastAspect = viewCamera.aspect; _lastFov = viewCamera.fieldOfView;
        if (screen == null || _screenWidth <= 0f || _screenHeight <= 0f)
        {
            _targetPosition = FramedPosition(); _targetRotation = Quaternion.Euler(holdLocalEuler); _targetScale = _heldScale * Framing;
            return;
        }
        float depth = Mathf.Max(viewCamera.nearClipPlane + .3f, holdLocalPosition.z);
        // 크기는 설정(UI > 태블릿 크기)에서 정한다. 장면에 적힌 heldFill은 설정을 읽을 수 없을 때만 쓴다.
        _lastSize = GameSettings.Current != null && GameSettings.Current.tabletView > 0f ? GameSettings.Current.tabletView : heldFill;
        float fill = Mathf.Min(screenFill, _lastSize);
        float fit = depth / DeskViewFraming.Distance(_screenWidth, _screenHeight, _lastFov, _lastAspect, fill);
        _targetScale = _heldScale * fit;
        // 위쪽을 뒤로 넘기고(+X), 놓여 있던 쪽에서 안쪽을 보도록 살짝 튼다.
        _targetRotation = Quaternion.Euler(heldPitch, _side * heldYaw, 0f) * Quaternion.Inverse(_screenRotation);
        // 시야의 절반 크기(이 깊이에서)와 태블릿 화면의 절반 크기로, 아래와 옆에 남는 틈을 구해 그만큼 옮긴다.
        float halfHeight = depth * Mathf.Tan(_lastFov * .5f * Mathf.Deg2Rad), halfWidth = halfHeight * _lastAspect;
        float down = Mathf.Max(0f, halfHeight - _screenHeight * fit * .5f) * heldDrop, aside = Mathf.Max(0f, halfWidth - _screenWidth * fit * .5f) * heldAside;
        _targetPosition = new Vector3(_side * aside, -down, depth) - _targetRotation * Vector3.Scale(_screenCenter, _targetScale);
    }

    void ApplyMouseWheelToScroll()
    {
        if (scrollRect == null)
        { return; }
        // 커서 잠금 시 mouseScrollDelta가 0일 수 있으므로 레거시 축도 사용
        float delta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(delta, 0f))
        { delta = Input.GetAxis("Mouse ScrollWheel"); }
        if (Mathf.Approximately(delta, 0f))
        { return; }
        float next = scrollRect.verticalNormalizedPosition + delta * scrollSensitivity;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(next);
    }

    void UpdateHoldPosition()
    {
        bool resized = !Mathf.Approximately(_lastAspect, viewCamera.aspect) || !Mathf.Approximately(_lastFov, viewCamera.fieldOfView)
            || GameSettings.Current != null && GameSettings.Current.tabletView > 0f && !Mathf.Approximately(_lastSize, GameSettings.Current.tabletView);
        if (!resized && IsFramed)
        { return; }
        if (resized)
        { RefreshFramedPose(); }
        float speed = _flipping ? Mathf.Max(moveSpeed, 5f) : moveSpeed;
        _moveT = speed <= 0f ? 1f : Mathf.Min(1f, _moveT + Time.unscaledDeltaTime * speed);
        if (_moveT >= 1f) { _flipping = false; }
        float t = _moveT * _moveT * (3f - 2f * _moveT);
        tabletObject.localPosition = Vector3.Lerp(_holdStartPosition, _targetPosition, t);
        tabletObject.localRotation = Quaternion.Slerp(_holdStartRotation, _targetRotation, t);
        tabletObject.localScale = Vector3.Lerp(_holdStartScale, _targetScale, t);
    }

    void ReleaseTablet()
    {
        _isHeld = false;
        IsTabletHeld = false;

        if (unlockCursorWhileHoldingTablet && _wasCursorLockedBeforeHold)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        tabletObject.SetParent(_originalParent, true);
        if (moveSpeed <= 0f)
        {
            tabletObject.position = _originalPosition;
            tabletObject.rotation = _originalRotation;
            tabletObject.localScale = _originalScale;
        }
        else
        { _returnRoutine = StartCoroutine(MoveBackToOriginal()); }
    }

    System.Collections.IEnumerator MoveBackToOriginal()
    {
        float t = 0f;
        Vector3 startPos = tabletObject.position;
        Quaternion startRot = tabletObject.rotation;
        Vector3 startScale = tabletObject.localScale;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * moveSpeed;
            float s = Mathf.Clamp01(t);
            tabletObject.position = Vector3.Lerp(startPos, _originalPosition, s);
            tabletObject.rotation = Quaternion.Slerp(startRot, _originalRotation, s);
            tabletObject.localScale = Vector3.Lerp(startScale, _originalScale, s);
            yield return null;
        }
        tabletObject.position = _originalPosition;
        tabletObject.rotation = _originalRotation;
        tabletObject.localScale = _originalScale;
        _returnRoutine = null;
    }

    private void RestoreOriginalPose()
    {
        if (tabletObject == null)
        { return; }
        tabletObject.SetParent(_originalParent, true);
        tabletObject.SetPositionAndRotation(_originalPosition, _originalRotation);
        tabletObject.localScale = _originalScale;
    }

    void OnDisable()
    {
        if (!_isHeld && _returnRoutine == null)
        { return; }

        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }

        if (_isHeld && !_managedByDesk && unlockCursorWhileHoldingTablet && _wasCursorLockedBeforeHold)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        _isHeld = false;
        IsTabletHeld = false;
        _managedByDesk = false;
        RestoreOriginalPose();
    }
}
