using UnityEngine;
using TMPro;

/// <summary>
/// Interaction 레이어에 있는 Interactable을 레이로 감지.
/// 호버 시 해당 Interactable의 indicatorToShow 활성/비활성, E 키 입력 시 Observer 실행.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Tooltip("레이캐스트에 사용할 카메라. 비면 Camera.main")]
    public Camera rayCamera;
    [Tooltip("레이어 이름. 해당 레이어의 BoxCollider만 감지")]
    public string interactionLayerName = "Interaction";
    [Header("상호작용 안내 UI")]
    [Tooltip("상호작용 안내 UI 루트. 비우면 텍스트만 갱신")]
    public GameObject hintRoot;
    [Tooltip("단축키 표시 텍스트 (예: [E])")]
    public TMP_Text keyText;
    [Tooltip("인터랙션 문구 텍스트 (예: 차트 보기)")]
    public TMP_Text interactionText;

    private Interactable _current;
    private int _layerMask;
    private Camera _cam;

    void Awake()
    {
        Instance = this;
        _layerMask = LayerMask.GetMask(interactionLayerName);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        _cam = rayCamera != null ? rayCamera : Camera.main;
        HideHint();
    }

    void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, float.MaxValue, _layerMask);

        if (hit && hitInfo.collider != null)
        {
            var next = hitInfo.collider.GetComponent<Interactable>();
            if (next != null)
            {
                if (_current != next)
                {
                    if (_current != null && _current.indicatorToShow != null)
                        _current.indicatorToShow.SetActive(false);
                    _current = next;
                    if (_current.indicatorToShow != null)
                        _current.indicatorToShow.SetActive(true);
                    ShowHint(_current);
                }

                if (Input.GetKeyDown(GameKeyBindings.Interact))
                    _current.InvokeInteract();
                return;
            }
        }

        if (_current != null && _current.indicatorToShow != null)
            _current.indicatorToShow.SetActive(false);
        _current = null;
        HideHint();
    }

    void ShowHint(Interactable interactable)
    {
        if (hintRoot != null)
            hintRoot.SetActive(true);

        if (keyText != null)
            keyText.text = $"[{GameKeyBindings.Interact}]";

        if (interactionText != null)
            interactionText.text = interactable != null ? interactable.interactionText : "";
    }

    void HideHint()
    {
        if (hintRoot != null)
            hintRoot.SetActive(false);

        if (keyText != null)
            keyText.text = "";

        if (interactionText != null)
            interactionText.text = "";
    }
}
