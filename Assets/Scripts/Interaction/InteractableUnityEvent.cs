using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interactable의 OnInteract를 UnityEvent로 연결. E 입력 시 인스펙터에서 할당한 동작(모니터 확대 등) 실행.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class InteractableUnityEvent : MonoBehaviour
{
    [Tooltip("같은 오브젝트의 Interactable을 비우면 자동 할당")]
    public Interactable interactable;

    [Tooltip("단축키로 상호작용했을 때 호출. 여기에 모니터 확대, 카메라 이동, Canvas 켜기 등 연결")]
    public UnityEvent onInteract;

    void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (interactable != null)
            interactable.OnInteract += HandleInteract;
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.OnInteract -= HandleInteract;
    }

    void HandleInteract(Interactable _)
    {
        onInteract?.Invoke();
    }
}
