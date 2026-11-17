using TMPro;
using UnityEngine;

// 가운데 모니터 머리줄의 오른쪽 칸: 지금 보는 종목을 내가 얼마나 들고 있는지.
// 들고 있지 않으면 흐린 글자 하나만, 들고 있으면 네 줄(이름은 왼쪽, 값은 오른쪽)을 보여 준다.
public partial class ChartManager
{
    private const int HoldingRows = 4;
    private const float HoldingLeft = .72f, HoldingWidth = .255f;
    private static readonly string[] HoldingNames = { "평균단가", "수익률", "평가손익", "평가금액" };
    private static readonly (string en, string ja)[] HoldingNamesOther =
    { ("Avg. price", "平均単価"), ("Return", "損益率"), ("Unrealized P/L", "評価損益"), ("Market value", "評価額") };
    private static readonly Color32 HoldingInk = new(230, 235, 239, 255), HoldingMuted = new(138, 150, 163, 255);
    private readonly TextMeshProUGUI[] holdingCaptions = new TextMeshProUGUI[HoldingRows], holdingValues = new TextMeshProUGUI[HoldingRows];
    private TextMeshProUGUI holdingEmpty;

    private static string HoldingWord(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>"1,234 원"처럼 숫자와 단위를 띄어 쓴다.</summary>
    private static string HoldingMoney(decimal value) => PriceTickPolicy.FormatMoney(value) + " " + (GameLocale.Code == "ko" ? "원" : GameLocale.Text("원"));

    private void ConfigureHoldingPanel(RectTransform header)
    {
        if (holdingEmpty == null)
        {
            holdingEmpty = HoldingText(header, "Text :: Holding Empty");
            for (int i = 0; i < HoldingRows; i++)
            {
                holdingCaptions[i] = HoldingText(header, "Text :: Holding Caption " + i);
                holdingValues[i] = HoldingText(header, "Text :: Holding Value " + i);
            }
        }
        PlaceHeaderText(holdingEmpty, header, HoldingLeft, .38f, HoldingWidth, .24f, 24f, true, true);
        for (int i = 0; i < HoldingRows; i++)
        {
            float y = .075f + i * .225f;
            PlaceHeaderText(holdingCaptions[i], header, HoldingLeft, y, HoldingWidth * .42f, .20f, 22f, true);
            PlaceHeaderText(holdingValues[i], header, HoldingLeft + HoldingWidth * .36f, y, HoldingWidth * .64f, .20f, 24f, false, true);
        }
        RefreshHolding();
    }
    private TextMeshProUGUI HoldingText(RectTransform header, string name)
    {
        var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(header, false);
        if (textVolume != null) { text.font = textVolume.font; text.fontSharedMaterial = textVolume.fontSharedMaterial; }
        return text;
    }

    /// <summary>두 계좌를 합친 보유량과 평균단가로 네 줄을 채운다.</summary>
    private void RefreshHolding()
    {
        if (holdingEmpty == null) { return; }
        decimal quantity = 0m, cost = 0m;
        var accounts = PlayerAccounts.Instance;
        if (currentStock != null && accounts != null)
        {
            foreach (var account in new[] { accounts.primary, accounts.automated })
            {
                if (account == null) { continue; }
                decimal owned = account.GetOwnedVolume(currentStock);
                quantity += owned; cost += owned * account.GetOwnedAverage(currentStock);
            }
        }
        bool holding = quantity > 0m;
        holdingEmpty.gameObject.SetActive(!holding);
        for (int i = 0; i < HoldingRows; i++) { holdingCaptions[i].gameObject.SetActive(holding); holdingValues[i].gameObject.SetActive(holding); }
        if (!holding)
        {
            GameLocalizedText.Set(holdingEmpty, HoldingWord("주식 미보유", "No shares held", "保有なし"), true);
            return;
        }
        decimal average = cost / quantity, value = quantity * currentStock.currentPrice, profit = value - cost;
        decimal rate = cost > 0m ? profit / cost * 100m : 0m;
        Color tone = profit > 0m ? MarketColors.Up : profit < 0m ? MarketColors.Down : (Color)HoldingInk;
        for (int i = 0; i < HoldingRows; i++)
        { GameLocalizedText.Set(holdingCaptions[i], HoldingWord(HoldingNames[i], HoldingNamesOther[i].en, HoldingNamesOther[i].ja), true); }
        GameLocalizedText.Set(holdingValues[0], HoldingMoney(decimal.Round(average)), true);
        GameLocalizedText.Set(holdingValues[1], (rate > 0m ? "+" : "") + rate.ToString("0.00") + "%", true);
        GameLocalizedText.Set(holdingValues[2], (profit > 0m ? "+" : "") + HoldingMoney(decimal.Round(profit)), true);
        GameLocalizedText.Set(holdingValues[3], HoldingMoney(decimal.Round(value)), true);
        holdingValues[0].color = holdingValues[3].color = HoldingInk;
        holdingValues[1].color = holdingValues[2].color = tone;
    }
}
