using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 차트에서 클릭한 캔들 데이터 보관. 종목이 바뀌면 초기화되어 데이터가 사라짐.
/// SetSelection 호출 시(캔들 값이 바뀔 때마다 ChartManager가 재호출) TMP 텍스트가 이벤트성으로 Refresh됨.
/// 선택 캔들 정보는 전용 영역에 고정하여 다른 패널과 겹치지 않음.
/// </summary>
public class ChartCandleSelectData : MonoBehaviour
{
    [Tooltip("선택된 캔들 데이터. 없으면 null")]
    public CandleData? SelectedCandle;
    [Tooltip("선택된 캔들의 전역 인덱스 (전체 리스트 기준). 캔들 밀림 시 십자선 위치 재계산에 사용")]
    public int? SelectedGlobalIndex;

    [Header("패널 위치 (텍스트들이 들어있는 부모)")]
    [Tooltip("시가/종가 등 텍스트가 들어있는 부모 RectTransform. 전용 캔들 정보 영역에 고정됨")]
    public RectTransform panelRoot;

    [Header("선택 캔들 표시 (TMP) — SetSelection 시마다 갱신")]
    public TextMeshProUGUI txtOpen;
    public TextMeshProUGUI txtClose;
    public TextMeshProUGUI txtHigh;
    public TextMeshProUGUI txtLow;
    public TextMeshProUGUI txtOpenPer;
    public TextMeshProUGUI txtClosePer;
    public TextMeshProUGUI txtHighPer;
    public TextMeshProUGUI txtLowPer;
    public TextMeshProUGUI txtVolume;
    public TextMeshProUGUI txtTurnOver;

    /// <summary>선택 여부</summary>
    public bool HasSelection => SelectedCandle.HasValue && SelectedGlobalIndex.HasValue;

    /// <summary>전캔들 종가 (색상: 올랐으면 녹색, 내렸으면 적색). null이면 기준 없음.</summary>
    float? _previousClose;

    /// <summary>선택이 바뀌었을 때 (설정/해제 시). (캔들, 전역인덱스) 또는 (null, null)</summary>
    public event Action<CandleData?, int?> OnSelectionChanged;

    private static Color Up => MarketColors.Up;
    private static Color Down => MarketColors.Down;
    private bool compacted;
    void Awake() { Compact(); }

    /// <summary>
    /// 캔들 정보는 제목·값·등락률 세 줄이었고, 셋째 줄이 차트 위의 시간 버튼 줄까지 내려와 겹쳤다.
    /// 시가·고가·저가·종가는 제목과 값을 한 줄에 두고(제목 왼쪽, 값 오른쪽) 등락률을 둘째 줄로 올려 두 줄로 줄인다.
    /// </summary>
    private void Compact()
    {
        if (compacted || panelRoot == null)
        { return; }

        compacted = true;
        const float lift = 12f, secondRow = -24f;
        panelRoot.anchoredPosition += new Vector2(0f, lift);
        foreach (var value in new[] { txtOpen, txtHigh, txtLow, txtClose })
        {
            if (value != null)
            { value.rectTransform.anchoredPosition = new Vector2(value.rectTransform.anchoredPosition.x, 0f); }
        }

        foreach (var change in new[] { txtOpenPer, txtHighPer, txtLowPer, txtClosePer })
        {
            if (change != null)
            { change.rectTransform.anchoredPosition = new Vector2(change.rectTransform.anchoredPosition.x, secondRow); }
        }
    }

    void OnEnable() { MarketColors.Changed += RefreshTexts; RefreshTexts(); }
    void OnDisable() { MarketColors.Changed -= RefreshTexts; }

    /// <summary>해당 캔들로 선택 설정. previousClose: 전캔들 종가(전캔들 대비 색상용). 캔들 값이 바뀔 때마다 ChartManager가 다시 호출하면 TMP가 이벤트성 Refresh됨.</summary>
    public void SetSelection(CandleData candle, int globalIndex, float? previousClose = null)
    {
        SelectedCandle = candle;
        SelectedGlobalIndex = globalIndex;
        _previousClose = previousClose;
        RefreshTexts();
        OnSelectionChanged?.Invoke(candle, globalIndex);
    }

    /// <summary>선택 해제. 종목이 바뀌면 ChartManager가 호출.</summary>
    public void Clear()
    {
        SelectedCandle = null;
        SelectedGlobalIndex = null;
        RefreshTexts();
        OnSelectionChanged?.Invoke(null, null);
    }

    /// <summary>선택 정보는 고정된 전용 영역을 사용하여 헤더·통계·차트와 겹치지 않게 한다.</summary>
    public void SetPanelSide(bool candleIsOnLeftOfChartCenter)
    {
        // Scene-authored RectTransform is retained on either side of the chart.
    }

    void RefreshTexts()
    {
        // 고른 봉이 없으면 제목만 빈 채로 남는다. 그때는 줄 전체를 숨긴다.
        if (panelRoot != null && panelRoot.gameObject.activeSelf != SelectedCandle.HasValue) { panelRoot.gameObject.SetActive(SelectedCandle.HasValue); }
        if (!SelectedCandle.HasValue)
        {
            SetTxt(txtOpen, "");
            SetTxt(txtClose, "");
            SetTxt(txtHigh, "");
            SetTxt(txtLow, "");
            SetTxt(txtOpenPer, "");
            SetTxt(txtClosePer, "");
            SetTxt(txtHighPer, "");
            SetTxt(txtLowPer, "");
            SetTxt(txtVolume, "");
            SetTxt(txtTurnOver, "");
            return;
        }
        var c = SelectedCandle.Value;
        float open = c.open;
        if (open <= 0f) open = 1f;
        float prev = _previousClose ?? open;
        if (prev <= 0f) prev = 1f;
        float closePct = (c.close - prev) / prev * 100f;
        float highPct = (c.high - prev) / prev * 100f;
        float lowPct = (c.low - prev) / prev * 100f;
        float openPct = (c.open - prev) / prev * 100f;
        float avgPrice = (c.high + c.low + c.close) / 3f;
        float turnover = c.volume * avgPrice;

        SetTxt(txtOpen, PriceTickPolicy.FormatPrice((decimal)c.open), c.open, prev);
        SetTxt(txtClose, PriceTickPolicy.FormatPrice((decimal)c.close), c.close, prev);
        SetTxt(txtHigh, PriceTickPolicy.FormatPrice((decimal)c.high), c.high, prev);
        SetTxt(txtLow, PriceTickPolicy.FormatPrice((decimal)c.low), c.low, prev);
        SetTxt(txtOpenPer, _previousClose.HasValue ? FormatPct(openPct) : "0%", c.open, prev);
        SetTxt(txtClosePer, FormatPct(closePct), c.close, prev);
        SetTxt(txtHighPer, FormatPct(highPct), c.high, prev);
        SetTxt(txtLowPer, FormatPct(lowPct), c.low, prev);
        SetTxt(txtVolume, ((decimal)c.volume).ToString("N0"));
        SetTxt(txtTurnOver, ((decimal)turnover).ToString("N0"));
    }

    static void SetTxt(TextMeshProUGUI t, string s, float? value, float? previousClose)
    {
        if (t == null) return;
        GameLocalizedText.Set(t,s);
        if (value.HasValue && previousClose.HasValue)
            t.color = value.Value > previousClose.Value ? Up : (value.Value < previousClose.Value ? Down : Color.white);
    }

    static void SetTxt(TextMeshProUGUI t, string s)
    {
        if (t != null) GameLocalizedText.Set(t,s);
    }

    static string FormatPct(float pct)
    {
        string sign = pct >= 0f ? "+" : "";
        return sign + pct.ToString("N2") + "%";
    }
}
