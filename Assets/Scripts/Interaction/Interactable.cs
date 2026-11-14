using UnityEngine;
using System;

/// <summary>
/// 상호작용 구역. BoxCollider + Layer "Interaction"인 오브젝트에 붙인다.
/// 마우스 호버 시 indicatorToShow 활성화, 벗어나면 비활성화.
/// E(또는 설정 키) 입력 시 OnInteract 구독자에게 알림 (Observer 패턴).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Interactable : MonoBehaviour
{
    [Tooltip("호버 시 활성화할 Canvas 쪽 GameObject (상호작용 표시)")]
    public GameObject indicatorToShow;
    [Tooltip("표시용 문자열 (예: 상호작용 안내 텍스트)")]
    public string interactionText = "";

    /// <summary>E 키로 상호작용 시 발생. Observer 구독용.</summary>
    public event Action<Interactable> OnInteract;

    /// <summary>상호작용 실행. InteractionManager가 E 입력 시 호출.</summary>
    public void InvokeInteract()
    {
        OnInteract?.Invoke(this);
    }

    void OnValidate()
    {
        if (indicatorToShow != null)
            indicatorToShow.SetActive(false);
    }

    void Awake()
    {
        if (indicatorToShow != null)
            indicatorToShow.SetActive(false);
    }
}
