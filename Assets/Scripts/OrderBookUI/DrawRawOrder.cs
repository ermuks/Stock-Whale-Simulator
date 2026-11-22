using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DrawRawOrder : MonoBehaviour
{
    public TMP_Text txtPrice;
    public TMP_Text txtVolume;
    public RectTransform volumeGuage;

    public GameObject highlight;

    private decimal currentPrice;
    private bool buySide;
    private Image depth;
    private const float VolumeFontScale = .85f;
    private bool volumeStyled;
    // 호가창 기본 글꼴(김정철고딕 Bold)은 서체 자체가 굵다. 수량에는 얇은 Pretendard Regular를 쓴다.
    private static TMP_FontAsset thinFont;
    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
    void OnDisable() { MarketColors.Changed -= RefreshColors; }
    public void SetSide(bool buy) { buySide=buy; RefreshColors(); }
    private void RefreshColors()
    {
        // Bid rows remain the lower-price color; asks remain the higher-price color.
        Color color=buySide ? MarketColors.Down : MarketColors.Up;
        if (txtPrice!=null) { txtPrice.color=color; }
        // 수량은 방향 색을 쓰지 않는다. 흰색, 조금 작게, 굵은 글씨 없이 보여 준다.
        if (txtVolume!=null)
        {
            txtVolume.color=Color.white;
            if (!volumeStyled)
            {
                volumeStyled=true;
                if (thinFont==null) { thinFont=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading"); }
                if (thinFont!=null) { txtVolume.font=thinFont; }
                txtVolume.fontStyle&=~FontStyles.Bold;
                txtVolume.fontWeight=FontWeight.Regular;
                if (txtVolume.enableAutoSizing) { txtVolume.fontSizeMax*=VolumeFontScale; }
                else { txtVolume.fontSize*=VolumeFontScale; }
            }
        }
        if (depth==null && volumeGuage!=null) { depth=volumeGuage.GetComponent<Image>(); }
        if (depth!=null) { color.a=.55f; depth.color=color; }
    }

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

        GameLocalizedText.Set(txtPrice,PriceTickPolicy.FormatPrice(price));
        
        if (isTurnover)
        {
            GameLocalizedText.Set(txtVolume,FormatTurnover(displayValue, price));
        }
        else
        {
            GameLocalizedText.Set(txtVolume,((long)displayValue).ToString("#,##0"));
        }

        float percent = maxValue <= 0m ? 0f : (float)(displayValue / maxValue);
        volumeGuage.anchorMin = new Vector2(0f, 0f);
        volumeGuage.anchorMax = new Vector2(percent, 1f);

        bool isLastPrice = price == lastPrice;
        highlight.SetActive(isLastPrice);
    }

    /// <summary>거래대금은 돈 단위로 표시.</summary>
    static string FormatTurnover(decimal turnover, decimal price)
    {
        return PriceTickPolicy.FormatMoney(turnover);
    }

    public void Clear()
    {
        GameLocalizedText.Set(txtPrice,"");
        GameLocalizedText.Set(txtVolume,"");
        volumeGuage.anchorMax = new Vector2(0f, 1f);
        highlight.SetActive(false);
    }
}
