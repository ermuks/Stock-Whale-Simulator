using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 뉴스 목록 한 행. 종목 이름, 호재/악재(Image 색상 + 텍스트), 뉴스 제목 표시.
/// </summary>
public class NewsListItem : MonoBehaviour
{
    [Header("표시 항목")]
    [Tooltip("종목 심볼 표시 (예: ALPH, PSLA)")]
    public TextMeshProUGUI textSymbol;
    public TextMeshProUGUI textTitle;

    [Header("호재/악재 색상 (Image)")]
    [Tooltip("호재일 때 표시할 Image. 비어 있으면 색상만 바꿀 Image 없음")]
    public Image imageGoodBad;
    [Tooltip("호재 색상 (criticalValue >= 1)")]
    public Color goodColor = new Color(0.2f, 0.75f, 0.3f);
    [Tooltip("악재 색상 (criticalValue < 1)")]
    public Color badColor = new Color(0.9f, 0.25f, 0.2f);

    /// <summary>criticalValue: 1 초과면 호재, 1 미만이면 악재. 정도는 |criticalValue - 1| * 100 %. Image는 호재/악재에 따라 색상 적용.</summary>
    public void Set(string symbol, float criticalValue, string title)
    {
        if (textSymbol != null) textSymbol.text = symbol ?? "";
        if (textTitle != null) textTitle.text = title ?? "";

        float pct = (criticalValue - 1f) * 100f;

        if (imageGoodBad != null)
            imageGoodBad.color = criticalValue >= 1f ? goodColor : badColor;
    }
}
