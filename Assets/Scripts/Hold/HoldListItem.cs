using System.Globalization;
using UnityEngine;
using TMPro;

/// <summary>
/// 보유 목록 행 또는 선택 종목 상세. 실제 계좌의 예치 중인 매도 수량도 보유에 포함한다.
/// </summary>
public class HoldListItem : MonoBehaviour
{
    [Header("표시 항목")]
    public TextMeshProUGUI textSymbol;
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textProfitPct;
    public TextMeshProUGUI textEvaluationProfit;
    public TextMeshProUGUI textAvgPrice;
    public TextMeshProUGUI textQuantity;
    public TextMeshProUGUI textCurrentPrice, textCurrentValue, textAvailability, textEmpty;
    public bool currencySuffix;
    private static readonly Color Neutral = new Color32(166,177,186,255);
    private decimal shownProfit;
    void OnEnable() { MarketColors.Changed += RefreshColors; RefreshColors(); }
    void OnDisable() { MarketColors.Changed -= RefreshColors; }
    private void RefreshColors()
    {
        Color color=MarketColors.ForChange(shownProfit,Neutral);
        if (textProfitPct!=null) { textProfitPct.color=color; }
        if (textEvaluationProfit!=null) { textEvaluationProfit.color=color; }
    }

    static readonly NumberFormatInfo _nfi;

    static HoldListItem()
    {
        _nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        _nfi.NumberGroupSizes = new[] { 3 };
        _nfi.NumberGroupSeparator = ",";
        _nfi.NumberDecimalSeparator = ".";
    }

    /// <summary>수익률(%): (현재가-평단)/평단*100. 평가손익: (현재가-평단)*수량. 평균매가는 돈 단위로 표시. 보유수량 정수.</summary>
    public void Set(string symbol, string name, decimal profitPct, decimal evaluationProfitLoss, decimal avgPrice, decimal quantity)
    {
        shownProfit=evaluationProfitLoss;
        DeviceTerminalUI.Set(textSymbol,symbol ?? ""); DeviceTerminalUI.Set(textName,name ?? "");
        DeviceTerminalUI.Set(textProfitPct,profitPct.ToString("+#,##0.0;-#,##0.0;0.0",_nfi) + "%");
        string unit = currencySuffix ? "돈" : "";
        DeviceTerminalUI.Set(textEvaluationProfit,(evaluationProfitLoss > 0m ? "+" : "") + PriceTickPolicy.FormatMoney(evaluationProfitLoss) + unit);
        DeviceTerminalUI.Set(textAvgPrice,FormatAvgPrice(avgPrice) + unit);
        DeviceTerminalUI.Set(textQuantity,decimal.Truncate(quantity).ToString("N0",_nfi) + (currencySuffix ? "주" : ""));
        Color color = MarketColors.ForChange(evaluationProfitLoss,Neutral);
        if (textProfitPct != null && textProfitPct.color != color) { textProfitPct.color = color; }
        if (textEvaluationProfit != null && textEvaluationProfit.color != color) { textEvaluationProfit.color = color; }
    }

    public void SetFromAccount(Stock stock,Trader account)
    {
        decimal quantity = stock == null || account == null ? 0m : account.GetOwnedVolume(stock);
        decimal average = stock == null || account == null ? 0m : account.GetOwnedAverage(stock);
        decimal price = stock == null ? 0m : stock.currentPrice;
        decimal cost = stock == null || account == null ? 0m : account.GetOwnedCost(stock), value = quantity * price, profit = value-cost;
        Set(stock == null ? "" : stock.symbol,stock == null ? "종목 선택" : stock.stockName,cost > 0m ? profit/cost*100m : 0m,profit,average,quantity);
        DeviceTerminalUI.Set(textCurrentPrice,stock == null ? "—" : DeviceTerminalUI.Money(price));
        DeviceTerminalUI.Set(textCurrentValue,DeviceTerminalUI.Money(value));
        decimal available = stock == null || account == null ? 0m : account.GetHolding(stock);
        decimal pending = System.Math.Max(0m,quantity-available);
        DeviceTerminalUI.Set(textAvailability,"매도 가능 " + available.ToString("N0") + "주" + (pending > 0m ? "  ·  매도 대기 " + pending.ToString("N0") + "주" : ""));
        DeviceTerminalUI.Set(textEmpty,stock == null ? "선택한 종목 없음" : quantity > 0m ? "" : "보유 없음");
        if (quantity <= 0m)
        { DeviceTerminalUI.Set(textAvgPrice,"—"); DeviceTerminalUI.Set(textProfitPct,"—"); }
        else if (cost <= 0m)
        { DeviceTerminalUI.Set(textProfitPct,"—"); }
    }

    /// <summary>평균단가는 계산 정밀도를 유지하고 표시만 1돈 단위로 반올림.</summary>
    static string FormatAvgPrice(decimal price)
    {
        return PriceTickPolicy.FormatMoney(price);
    }
}
