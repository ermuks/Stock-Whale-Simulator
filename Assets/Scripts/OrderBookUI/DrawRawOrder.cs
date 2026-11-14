using TMPro;
using UnityEngine;

public class DrawRawOrder : MonoBehaviour
{
    public TMP_Text txtPrice;
    public TMP_Text txtVolume;
    public RectTransform volumeGuage;

    public GameObject highlight;

    private decimal currentPrice;

    public void SetData(decimal price, decimal volume, decimal maxVolume, decimal lastPrice)
    {
        SetData(price, volume, maxVolume, lastPrice, false);
    }

    public void SetData(decimal price, decimal displayValue, decimal maxValue, decimal lastPrice, bool isTurnover)
    {
        currentPrice = price;

        if (displayValue <= 0m)
        {
            Clear();
            return;
        }

        txtPrice.text = PriceTickPolicy.FormatPrice(price);
        
        if (isTurnover)
        {
            txtVolume.text = FormatTurnover(displayValue, price);
        }
        else
        {
            txtVolume.text = ((long)displayValue).ToString("#,##0");
        }

        float percent = maxValue <= 0m ? 0f : (float)(displayValue / maxValue);
        volumeGuage.anchorMin = new Vector2(0f, 0f);
        volumeGuage.anchorMax = new Vector2(percent, 1f);

        bool isLastPrice = price == lastPrice;
        highlight.SetActive(isLastPrice);
    }

    /// <summary>거래대금은 원 단위로 표시.</summary>
    static string FormatTurnover(decimal turnover, decimal price)
    {
        return PriceTickPolicy.FormatMoney(turnover);
    }

    public void Clear()
    {
        txtPrice.text = "";
        txtVolume.text = "";
        volumeGuage.anchorMax = new Vector2(0f, 1f);
        highlight.SetActive(false);
    }
}
