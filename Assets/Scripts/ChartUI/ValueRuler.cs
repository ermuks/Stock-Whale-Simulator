using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가운데 모니터 머리의 가치 눈금자. 세 값을 한 줄 위의 점으로 놓는다:
///   기업 실제 가치(회색) · 시장이 믿는 가격(청록, 둘레의 옅은 띠는 사람들 기대의 가운데 절반) · 지금 값(흰색).
/// 이 게임에서 값은 사람들이 믿는 쪽으로 끌려간다. 그 거리를 숫자 셋으로 읽게 하지 않고 눈으로 보게 하는 것이 목적이다.
/// 밑에는 그 거리를 쉬운 말 한 줄로 적는다("지금 값이 사람들 기대보다 5.9% 위").
/// </summary>
public sealed class ValueRuler : MonoBehaviour
{
    private static readonly Color Muted = new Color32(138, 150, 163, 255), Ink = new Color32(236, 240, 243, 255), Axis = new Color32(36, 52, 62, 255), RealInk = new Color32(128, 144, 156, 255);
    private RectTransform root;
    private TMP_FontAsset font;
    private Image axis, band, realPin, crowdPin, nowPin;
    private TextMeshProUGUI realCaption, crowdCaption, nowCaption, realValue, crowdValue, nowValue, sentence;
    private float next;

    public static ValueRuler Create(RectTransform area, TMP_FontAsset font)
    {
        var view = area.gameObject.AddComponent<ValueRuler>(); view.root = area; view.font = font; view.Build(); return view;
    }
    private static string L(string ko, string en, string ja) => GameLocale.Code == "ja" ? ja : GameLocale.Code == "en" ? en : ko;

    private Image Box(string name, Color color)
    {
        var image = new GameObject("Image :: Ruler " + name, typeof(RectTransform)).AddComponent<Image>();
        image.rectTransform.SetParent(root, false); image.gameObject.layer = root.gameObject.layer; image.raycastTarget = false; image.color = color; return image;
    }
    private TextMeshProUGUI Text(string name, float size, Color color)
    {
        var label = new GameObject("Text :: Ruler " + name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(root, false); label.gameObject.layer = root.gameObject.layer;
        if (font != null) { label.font = font; }
        label.fontSize = size; label.enableAutoSizing = false; label.color = color; label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; return label;
    }
    private static void At(Graphic graphic, float x0, float y0, float x1, float y1)
    {
        var rect = graphic.rectTransform; rect.anchorMin = new Vector2(x0, 1f - y1); rect.anchorMax = new Vector2(x1, 1f - y0); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private void Build()
    {
        axis = Box("Axis", Axis); band = Box("Band", new Color(.18f, .91f, .81f, .2f));
        realPin = Box("Real Pin", RealInk); crowdPin = Box("Crowd Pin", TabletKitStyle.Accent); nowPin = Box("Now Pin", Ink);
        realCaption = Text("Real Caption", 22f, Muted); crowdCaption = Text("Crowd Caption", 22f, Muted); nowCaption = Text("Now Caption", 22f, Ink);
        realValue = Text("Real Value", 29f, RealInk); crowdValue = Text("Crowd Value", 29f, TabletKitStyle.Accent); nowValue = Text("Now Value", 29f, Ink);
        TradingOrderView.Numeric(realValue); TradingOrderView.Numeric(crowdValue); TradingOrderView.Numeric(nowValue);
        sentence = Text("Sentence", 22f, Muted); sentence.alignment = TextAlignmentOptions.MidlineLeft;
        At(axis, 0f, .37f, 1f, .40f); At(sentence, 0f, .76f, 1f, .98f);
    }

    void Update()
    {
        if (Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .5f;
        var manager = StockManager.Instance; var stock = manager == null ? null : manager.CurrentStock;
        if (stock == null || stock.currentPrice <= 0m) { return; }
        bool realOpen = PlayerProgression.Has(PlayerProgression.RealValueUnlock), crowdOpen = PlayerProgression.Has(PlayerProgression.CrowdValueUnlock);
        decimal now = stock.currentPrice, crowd = stock.fakeValue, real = stock.realValue;
        bool spread = manager.TryCrowdSpread(stock, out decimal low, out decimal high) && crowdOpen;
        decimal min = now, max = now;
        void Take(decimal v) { if (v > 0m) { min = Math.Min(min, v); max = Math.Max(max, v); } }
        if (crowdOpen) { Take(crowd); }
        if (realOpen) { Take(real); }
        if (spread) { Take(low); Take(high); }
        decimal pad = Math.Max((max - min) * .1m, now * .01m);
        min -= pad; max += pad;
        float X(decimal v) => Mathf.Clamp01((float)((v - min) / Math.Max(1m, max - min)));
        float xNow = X(now), xCrowd = crowdOpen ? X(crowd) : -1f, xReal = realOpen ? X(real) : -1f;
        if (spread) { At(band, X(low), .33f, X(high), .44f); } band.gameObject.SetActive(spread);
        // 글자가 겹치지 않게 세 표지의 글자 자리를 벌린다(점 자체는 실제 자리 그대로).
        float[] spots = { xReal, xCrowd, xNow };
        int[] order = { 0, 1, 2 };
        Array.Sort(order, (a, b) => spots[a].CompareTo(spots[b]));
        float[] labels = new float[3]; float last = -1f;
        foreach (int i in order)
        {
            if (spots[i] < 0f) { labels[i] = -1f; continue; }
            float x = Mathf.Max(spots[i], last + .26f); labels[i] = x; last = x;
        }
        float overflow = last - .88f;
        if (overflow > 0f) { for (int i = 0; i < 3; i++) { if (labels[i] >= 0f) { labels[i] = Mathf.Max(.12f, labels[i] - overflow); } } }
        Pin(realPin, realCaption, realValue, xReal, labels[0], L("기업 실제 가치", "Real value", "企業の実価値"), real);
        Pin(crowdPin, crowdCaption, crowdValue, xCrowd, labels[1], L("시장이 믿는 가격", "Crowd expects", "市場が信じる値"), crowd);
        Pin(nowPin, nowCaption, nowValue, xNow, labels[2], L("지금", "Now", "現在"), now);
        // 쉬운 말 한 줄: 지금 값이 사람들 기대에서 얼마나 떨어져 있는가(값은 그쪽으로 끌려간다).
        string text;
        if (!crowdOpen || crowd <= 0m) { text = ""; }
        else
        {
            decimal gap = now / crowd - 1m; string pct = Math.Abs(gap * 100m).ToString("0.0") + "%";
            text = Math.Abs(gap) < .005m ? L("지금 값이 사람들 기대와 거의 같습니다", "Price sits right at what the crowd expects", "現在値はみんなの期待とほぼ同じです")
                : gap > 0m ? L($"지금 값이 사람들 기대보다 {pct} 위 · 내려올 힘이 있습니다", $"Price is {pct} above what the crowd expects · pull down", $"現在値はみんなの期待より{pct}上 · 下がる力あり")
                : L($"지금 값이 사람들 기대보다 {pct} 아래 · 올라갈 힘이 있습니다", $"Price is {pct} below what the crowd expects · pull up", $"現在値はみんなの期待より{pct}下 · 上がる力あり");
            sentence.color = Math.Abs(gap) < .005m ? Muted : gap > 0m ? MarketColors.Down : MarketColors.Up;
        }
        Say(sentence, text);
    }
    private static void Pin(Image pin, TextMeshProUGUI caption, TextMeshProUGUI value, float x, float labelX, string name, decimal amount)
    {
        bool on = x >= 0f;
        pin.gameObject.SetActive(on); caption.gameObject.SetActive(on); value.gameObject.SetActive(on);
        if (!on) { return; }
        At(pin, x - .004f, .24f, x + .004f, .52f);
        At(caption, labelX - .2f, .0f, labelX + .2f, .2f);
        At(value, labelX - .2f, .5f, labelX + .2f, .72f);
        Say(caption, name); Say(value, PriceTickPolicy.FormatPrice(amount));
    }
    private static void Say(TextMeshProUGUI label, string value) { if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); } }
}
