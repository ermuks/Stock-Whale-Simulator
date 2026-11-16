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
    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
    void OnDisable() { MarketColors.Changed -= RefreshColors; }
    private void RefreshColors()
    {
        if (txtPrice!=null) { txtPrice.color=_isBuyInitiated ? MarketColors.Up : MarketColors.Down; }
    }

    public void Set(decimal price, decimal volume, bool isBuyInitiated)
    {
        _isBuyInitiated = isBuyInitiated;

        if (txtPrice != null)
        {
            GameLocalizedText.Set(txtPrice,PriceTickPolicy.FormatPrice(price));
            RefreshColors();
        }

        if (txtVolume != null)
        {
            decimal totalAmount = price * volume;
            GameLocalizedText.Set(txtVolume,totalAmount.ToString("N0"));
        }
    }
}
