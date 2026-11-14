using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Display 2종(StockList용 / Chart용) 공용. 에임이 이 디스플레이를 가리킬 때
/// 해당 디스플레이의 Canvas 위 커서 UI가 에임 위치를 따라가게 하고,
/// 클릭 시 커서 위치에서 버튼 등 UI 클릭이 되도록 EventSystem에 전달.
/// </summary>
[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(-200)]
public class DisplayAimCursor : MonoBehaviour
{
    [Tooltip("이 디스플레이의 Canvas (커서가 표시될 곳)")]
    public Canvas displayCanvas;
    [Tooltip("에임을 따라갈 커서 UI (displayCanvas 하위 RectTransform)")]
    public RectTransform displayCursor;
    [Tooltip("Raycast 대상. 비어 있으면 'Display' 레이어 사용. 이 디스플레이/모니터에 Display 레이어 할당 필요.")]
    public LayerMask displayLayerMask;

    [Tooltip("에임 클릭 시 커서 위치에서 버튼 클릭 전달. 끄면 EventSystem 부하 없음 (프레임드랍 시 여기서 끄고 원인 확인용).")]
    public bool enableClickForwarding = true;

    private Collider _collider;
    private Camera _mainCam;
    private static readonly List<RaycastResult> s_raycastResults = new List<RaycastResult>();
    private static DisplayAimCursor capture;
    private static int lastCaptureFrame = -1;
    private PointerEventData pointer;
    private GameObject pressed, dragTarget;
    [Min(.1f)] public float dragPixelsPerAxis = 20f;
    public static bool HasPointerCapture => capture != null || lastCaptureFrame == Time.frameCount;

    void Start()
    {
        _mainCam = Camera.main;
        _collider = GetComponent<Collider>();
        if (displayLayerMask == 0)
        { displayLayerMask = LayerMask.GetMask("Display"); }
    }

    void Update()
    {
        if (displayCanvas == null || displayCursor == null || _mainCam == null || _collider == null)
        { EndPointer(false); return; }
        if (DeskViewController.Instance != null || TabletViewController.IsTabletHeld || PhoneCallController.IsRaised)
        { EndPointer(false); displayCursor.gameObject.SetActive(false); return; }
        if (capture == this)
        {
            if (!MouseCameraController.IsCursorLocked || !enableClickForwarding)
            { EndPointer(false); }
            else
            { UpdatePointer(); return; }
        }

        Vector2 aimScreen = MouseCameraController.GetAimScreenPosition();
        Ray r = _mainCam.ScreenPointToRay(aimScreen);
        if (!Physics.Raycast(r, out RaycastHit hit, _mainCam.farClipPlane, displayLayerMask))
        {
            displayCursor.gameObject.SetActive(false);
            return;
        }
        if (hit.collider != _collider)
        {
            displayCursor.gameObject.SetActive(false);
            return;
        }

        displayCursor.gameObject.SetActive(true);
        Camera cam = displayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : displayCanvas.worldCamera ?? _mainCam;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            displayCanvas.transform as RectTransform, _mainCam.WorldToScreenPoint(hit.point), cam, out Vector2 local);
        displayCursor.anchoredPosition = local;

        // Raycast UI only on press/release. Scroll drags retain the original target between frames.
        if (enableClickForwarding && capture == null && MouseCameraController.IsCursorLocked && Input.GetMouseButtonDown(0))
        { BeginPointer(_mainCam.WorldToScreenPoint(hit.point)); }
    }

    private RaycastResult HitUI(PointerEventData data)
    {
        var es = EventSystem.current;
        if (es == null)
        { return default; }
        s_raycastResults.Clear();
        es.RaycastAll(data, s_raycastResults);
        for (int i = 0; i < s_raycastResults.Count; i++)
        {
            var result = s_raycastResults[i];
            if (result.gameObject == null || result.module is not GraphicRaycaster)
            { continue; }
            return result.gameObject.transform.IsChildOf(displayCanvas.transform) ? result : default;
        }
        return default;
    }
    private void BeginPointer(Vector2 position)
    {
        if (EventSystem.current == null)
        { return; }
        pointer ??= new PointerEventData(EventSystem.current);
        pointer.Reset(); pointer.button = PointerEventData.InputButton.Left;
        pointer.position = pointer.pressPosition = position; pointer.delta = Vector2.zero;
        var hit = HitUI(pointer);
        if (hit.gameObject == null)
        { return; }
        pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hit;
        pointer.eligibleForClick = true; pointer.dragging = false; pointer.clickCount = 1;
        pointer.clickTime = Time.unscaledTime;
        pressed = ExecuteEvents.ExecuteHierarchy(hit.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        if (pressed == null) { pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject); }
        dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject);
        pointer.pointerPress = pressed; pointer.rawPointerPress = hit.gameObject; pointer.pointerDrag = dragTarget;
        if (dragTarget != null) { ExecuteEvents.Execute(dragTarget,pointer,ExecuteEvents.initializePotentialDrag); }
        capture = this;
        lastCaptureFrame = Time.frameCount;
    }
    private void UpdatePointer()
    {
        lastCaptureFrame = Time.frameCount;
        Vector2 before = pointer.position;
        Vector2 movement = new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y")) * dragPixelsPerAxis;
        pointer.position = new Vector2(Mathf.Clamp(before.x+movement.x,0,Screen.width),Mathf.Clamp(before.y+movement.y,0,Screen.height));
        pointer.delta = pointer.position-before;
        int threshold = EventSystem.current == null ? 5 : EventSystem.current.pixelDragThreshold;
        if (dragTarget != null && !pointer.dragging && (pointer.position-pointer.pressPosition).sqrMagnitude >= threshold*threshold)
        {
            ExecuteEvents.Execute(dragTarget,pointer,ExecuteEvents.beginDragHandler); pointer.dragging = true; pointer.eligibleForClick = false;
            if (pressed != null) { ExecuteEvents.Execute(pressed,pointer,ExecuteEvents.pointerUpHandler); pressed = null; pointer.pointerPress = null; }
        }
        if (pointer.dragging && pointer.delta.sqrMagnitude > 0f) { ExecuteEvents.Execute(dragTarget,pointer,ExecuteEvents.dragHandler); }
        Camera camera = displayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : displayCanvas.worldCamera ?? _mainCam;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)displayCanvas.transform,pointer.position,camera,out Vector2 local);
        displayCursor.gameObject.SetActive(true); displayCursor.anchoredPosition = local;
        if (Input.GetMouseButtonUp(0)) { EndPointer(true); }
        else if (!Input.GetMouseButton(0)) { EndPointer(false); }
    }
    private void EndPointer(bool click)
    {
        if (capture != this || pointer == null)
        { return; }
        lastCaptureFrame = Time.frameCount;
        if (pressed != null) { ExecuteEvents.Execute(pressed,pointer,ExecuteEvents.pointerUpHandler); }
        if (pointer.dragging && dragTarget != null) { ExecuteEvents.Execute(dragTarget,pointer,ExecuteEvents.endDragHandler); }
        GameObject target = null;
        if (click && pointer.eligibleForClick && pressed != null)
        {
            var hit = HitUI(pointer);
            if (hit.gameObject != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == pressed) { target = pressed; }
        }
        capture = null; pressed = dragTarget = null; pointer.dragging = false;
        if (target != null) { ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler); }
    }
    void OnDisable() { EndPointer(false); }
}
