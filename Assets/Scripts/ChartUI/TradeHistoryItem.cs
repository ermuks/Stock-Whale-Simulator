using UnityEngine;
using TMPro;

/// <summary>
/// 최근 거래 기록 1개 항목 UI
/// </summary>
public class TradeHistoryItem : MonoBehaviour
{
    [Tooltip("체결가")]
    public TextMeshProUGUI txtPrice;
    [Tooltip("체결량")]
    public TextMeshProUGUI txtVolume;

    private bool _isBuyInitiated;

    public void Set(decimal price, decimal volume, bool isBuyInitiated)
    {
        _isBuyInitiated = isBuyInitiated;

        if (txtPrice != null)
        {
            txtPrice.text = PriceTickPolicy.FormatPrice(price);
            txtPrice.color = isBuyInitiated ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.3f, 0.3f);
        }

        if (txtVolume != null)
        {
            decimal totalAmount = price * volume;
            txtVolume.text = totalAmount.ToString("N0");
        }
    }
}
