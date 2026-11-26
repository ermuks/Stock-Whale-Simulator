using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자산 태블릿의 보유 지도: 내 자산을 한 줄의 띠로 나눈다. 칸의 너비 = 그 몫의 금액(주식은 평가금, 현금은 쓸 수 있는 돈).
/// 주식 칸의 색 = 수익률(빨강 이익 · 파랑 손실, 진할수록 크다). 비상장 회사와 현금은 무채색. 넓은 칸에는 이름과 금액을 적는다.
/// 숫자를 읽지 않아도 "어디에 돈이 몰려 있고 어디서 벌고 있는지"가 보이게 하는 것이 목적이다. 정확한 값은 밑의 목록에 그대로 있다.
/// </summary>
public sealed class HoldingsMap : MonoBehaviour
{
    private const int MaxCells = 8;
    private RectTransform root;
    private TMP_FontAsset font;
    private float scale;
    private readonly Image[] cells = new Image[MaxCells];
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[MaxCells];
    private readonly List<(string name, decimal value, float tone, bool neutral)> parts = new(12);
    private readonly List<Stock> owned = new(16);

    public static HoldingsMap Create(RectTransform area, TMP_FontAsset font, float scale)
    {
        var view = area.gameObject.AddComponent<HoldingsMap>(); view.root = area; view.font = font; view.scale = scale; view.Build(); return view;
    }
    private void Build()
    {
        for (int i = 0; i < MaxCells; i++)
        {
            var cell = new GameObject("Image :: Holding Cell " + i, typeof(RectTransform)).AddComponent<Image>();
            cell.rectTransform.SetParent(root, false); cell.gameObject.layer = root.gameObject.layer; cell.raycastTarget = false; cells[i] = cell; cell.gameObject.SetActive(false);
            // 한 칸에 한 줄("현금 7.51억"). 칸 높이 안에 들어가는 크기로(넘치면 TextMeshPro가 줄을 통째로 숨긴다).
            names[i] = Text(cell.rectTransform, "Name", 0f, 1f, 19, TabletKitStyle.Ink);
        }
    }
    private TextMeshProUGUI Text(RectTransform parent, string name, float bottom, float top, float size, Color color)
    {
        var label = new GameObject("Text :: Holding " + name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        var rect = label.rectTransform; rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(0f, bottom); rect.anchorMax = new Vector2(1f, top); rect.offsetMin = new Vector2(8f, 0f) * scale; rect.offsetMax = new Vector2(-6f, 0f) * scale;
        if (font != null) { label.font = font; }
        label.fontSize = size * scale; label.color = color; label.raycastTarget = false; label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    /// <summary>계좌로 다시 그린다. 칸이 많으면 작은 종목들은 "그 밖 n종목" 한 칸으로 묶는다.</summary>
    public void Refresh(Trader account)
    {
        parts.Clear();
        if (account == null) { Hide(0); return; }
        account.CopyOwnedStocks(owned);
        foreach (var stock in owned)
        {
            if (stock == null || stock.currentPrice <= 0m) { continue; }
            decimal volume = account.GetOwnedVolume(stock), value = volume * stock.currentPrice, basis = account.GetOwnedCost(stock);
            if (value <= 0m) { continue; }
            float rate = basis > 0m ? (float)((value - basis) / basis) : 0f;
            parts.Add((GameLocale.Text(stock.stockName), value, rate, !stock.IsListed));
        }
        parts.Sort((a, b) => b.value.CompareTo(a.value));
        // 작은 종목은 묶는다: 주식 칸은 많아야 MaxCells-2(현금 한 칸, "그 밖" 한 칸).
        if (parts.Count > MaxCells - 1)
        {
            decimal rest = 0m; int count = 0;
            for (int i = parts.Count - 1; i >= MaxCells - 2; i--) { rest += parts[i].value; count++; parts.RemoveAt(i); }
            string others = GameLocale.Code == "ja" ? $"その他 {count}銘柄" : GameLocale.Code == "en" ? $"{count} others" : $"그 밖 {count}종목";
            parts.Add((others, rest, 0f, true));
        }
        decimal cash = PlayerAccounts.Available(account);
        if (cash > 0m) { parts.Add((GameLocale.Code == "ja" ? "現金" : GameLocale.Code == "en" ? "Cash" : "현금", cash, 0f, true)); }
        decimal whole = 0m; foreach (var part in parts) { whole += part.value; }
        if (whole <= 0m) { Hide(0); return; }
        float x = 0f, gap = .004f;
        for (int i = 0; i < parts.Count && i < MaxCells; i++)
        {
            var part = parts[i];
            float width = (float)(part.value / whole);
            var cell = cells[i]; cell.gameObject.SetActive(true);
            cell.rectTransform.anchorMin = new Vector2(x, 0f); cell.rectTransform.anchorMax = new Vector2(Mathf.Min(1f, x + Mathf.Max(width - gap, .002f)), 1f);
            cell.rectTransform.offsetMin = cell.rectTransform.offsetMax = Vector2.zero;
            cell.color = Tone(part.tone, part.neutral, i == parts.Count - 1 && cash > 0m);
            // 칸이 충분히 넓을 때만 글자를 적는다(좁은 칸에 잘린 글자는 읽히지 않는다).
            bool wide = width > .09f;
            names[i].gameObject.SetActive(wide);
            if (wide) { Say(names[i], part.name + "  " + Short(part.value)); }
            x += width;
        }
        Hide(Math.Min(parts.Count, MaxCells));
    }
    private void Hide(int from) { for (int i = from; i < MaxCells; i++) { if (cells[i].gameObject.activeSelf) { cells[i].gameObject.SetActive(false); } } }
    private static Color Tone(float rate, bool neutral, bool cash)
    {
        if (cash) { return new Color32(22, 34, 42, 255); }
        if (neutral) { return new Color32(40, 56, 66, 255); }
        // 수익률 ±5%에서 가장 진하다.
        float strength = Mathf.Clamp01(Mathf.Abs(rate) / .05f);
        var tone = rate >= 0f ? (Color)TabletKitStyle.Up : TabletKitStyle.Down;
        return Color.Lerp(new Color32(40, 56, 66, 255), tone * .78f, .30f + .55f * strength);
    }
    /// <summary>큰 금액을 짧게(1.32억, 7,512만). 영어·일본어는 그 나라 단위로.</summary>
    public static string Short(decimal value)
    {
        string code = GameLocale.Code;
        if (code == "en")
        {
            if (value >= 1_000_000_000m) { return (value / 1_000_000_000m).ToString("0.##") + "B"; }
            if (value >= 1_000_000m) { return (value / 1_000_000m).ToString("0.#") + "M"; }
            return value >= 1_000m ? (value / 1_000m).ToString("0.#") + "K" : value.ToString("N0");
        }
        string eok = code == "ja" ? "億" : "억", man = code == "ja" ? "万" : "만";
        if (value >= 100_000_000m) { return (value / 100_000_000m).ToString("0.##") + eok; }
        if (value >= 10_000m) { return (value / 10_000m).ToString("N0") + man; }
        return value.ToString("N0");
    }
    private static void Say(TextMeshProUGUI label, string value) { if (!GameLocalizedText.Matches(label, value)) { GameLocalizedText.Set(label, value, true); } }
}
