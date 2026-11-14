using UnityEngine;

/// <summary>기존 씬 참조 호환. 종목별 독립 공급은 단일 ExchangeBot에 통합됨.</summary>
public class ExchangeStockBot : MonoBehaviour
{
    public Stock stock;
    public ExchangeBot Exchange => ExchangeBot.Instance;

    void Awake()
    {
        enabled = false;
    }
}
