using UnityEngine;

/// <summary>기존 씬 참조 호환. realValue 수렴 주문은 ExchangeBot의 완료 봉 방어로 통합됨.</summary>
public class ExchangeStabilizerBot : MonoBehaviour
{
    public ExchangeBot Exchange => ExchangeBot.Instance;

    void Awake()
    {
        enabled = false;
    }
}
