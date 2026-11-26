using TMPro;
using UnityEngine;

// 가운데 모니터 머리줄의 오른쪽 칸: 지금 보는 종목을 내가 얼마나 들고 있는지.
// 들고 있지 않으면 흐린 글자 하나만, 들고 있으면 네 줄(이름은 왼쪽, 값은 오른쪽)을 보여 준다.
public partial class ChartManager
{
    private const int HoldingRows = 4;
    private const float HoldingLeft = .70f, HoldingWidth = .28f;
    private static readonly string[] HoldingNames = { "평균단가", "수익률", "평가손익", "평가금액" };
    private static readonly (string en, string ja)[] HoldingNamesOther =
    { ("Avg. price", "平均単価"), ("Return", "損益率"), ("Unrealized P/L", "評価損益"), ("Market value", "評価額") };
    private static readonly Color32 HoldingInk = new(230, 235, 239, 255), HoldingMuted = new(138, 150, 163, 255);
    private readonly TextMeshProUGUI[] holdingCaptions = new TextMeshProUGUI[HoldingRows], holdingValues = new TextMeshProUGUI[HoldingRows];
    private TextMeshProUGUI holdingEmpty;

    private static string HoldingWord(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    /// <summary>"1,234 돈"처럼 숫자와 단위를 띄어 쓴다.</summary>
    private static string HoldingMoney(decimal value) => PriceTickPolicy.FormatMoney(value) + " " + (GameLocale.Code == "ko" ? "돈" : GameLocale.Text("돈"));

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
        // 세 줄. 위: 평가손익(가장 크게)과 수익률. 가운데: 평균단가 · 평가금액. 아래: 거래량 · 거래대금(이 종목 전체).
        float L = HoldingLeft, W = HoldingWidth;
        PlaceHeaderText(holdingEmpty, header, L, .10f, W, .30f, 26f, true);
        PlaceHeaderText(holdingCaptions[2], header, L, .02f, W * .55f, .17f, 20f, true);
        PlaceHeaderText(holdingValues[2], header, L, .17f, W * .64f, .30f, 34f);
        PlaceHeaderText(holdingCaptions[1], header, L + W * .6f, .02f, W * .4f, .17f, 20f, true, true);
        PlaceHeaderText(holdingValues[1], header, L + W * .6f, .17f, W * .4f, .30f, 30f, false, true);
        PlaceHeaderText(holdingCaptions[0], header, L, .52f, W * .2f, .2f, 20f, true);
        PlaceHeaderText(holdingValues[0], header, L + W * .18f, .52f, W * .3f, .2f, 23f);
        PlaceHeaderText(holdingCaptions[3], header, L + W * .5f, .52f, W * .2f, .2f, 20f, true);
        PlaceHeaderText(holdingValues[3], header, L + W * .66f, .52f, W * .34f, .2f, 23f, false, true);
        PlaceHeaderCaption(header, "Volume Caption", L, .77f, W * .2f);
        PlaceHeaderText(textVolume, header, L + W * .18f, .77f, W * .3f, .2f, 23f);
        PlaceHeaderCaption(header, "Turnover Caption", L + W * .5f, .77f, W * .2f);
        PlaceHeaderText(textTurnover, header, L + W * .66f, .77f, W * .34f, .2f, 23f, false, true);
        foreach (string name in new[] { "Volume Caption", "Turnover Caption" })
        {
            var child = header.Find("Text :: " + name);
            if (child != null) { var label = child.GetComponent<TextMeshProUGUI>(); label.alignment = TextAlignmentOptions.MidlineLeft; label.fontSize = 20f; }
        }
        // 구역 사이의 얇은 세로선 두 개(상자 대신 선으로 나눈다).
        foreach (float x in new[] { .365f, L - .012f })
        {
            string name = "Image :: Header Divider " + x.ToString("0.000");
            if (header.Find(name) != null) { continue; }
            var line = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
            line.rectTransform.SetParent(header, false); line.raycastTarget = false; line.color = new Color32(40, 54, 64, 255);
            line.rectTransform.anchorMin = new Vector2(x, .1f); line.rectTransform.anchorMax = new Vector2(x + .0012f, .9f); line.rectTransform.offsetMin = line.rectTransform.offsetMax = Vector2.zero;
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
