using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// "지금 이 종목에서 내가 무언가를 돌리고 있는가"를 한곳에서 답한다: 작전 도구(거래량 부풀리기, 허수 매수벽, 종가 관리, 불붙인 값 받치기, 넘기기)와
/// 자동 매매(스프레드, 매집). 종목 목록의 꼬리표와 가운데 모니터의 표시가 이것을 읽는다. 게임 상태는 바꾸지 않는다.
/// </summary>
public static class PlayerOperations
{
    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private static readonly StringBuilder text = new(64);

    /// <summary>이 종목에서 돌고 있는 것의 수.</summary>
    public static int Count(Stock stock)
    {
        if (stock == null) { return 0; }
        int count = 0;
        var tactics = PlayerTactics.Instance;
        if (tactics != null) { foreach (var run in tactics.Runs) { if (run.Stock == stock) { count++; } } }
        var automation = TradingOperationController.RunOn(stock);
        if (automation != null && automation.Active) { count++; }
        return count;
    }
    public static bool Active(Stock stock) => Count(stock) > 0;

    /// <summary>종목 목록에 붙이는 짧은 꼬리표.</summary>
    public static string Chip => Word("작전", "Ops", "作戦");

    /// <summary>가운데 모니터에 적는 한 줄: "작전 중 · 매집 · 매수벽". 돌고 있는 것이 없으면 빈 글.</summary>
    public static string Line(Stock stock)
    {
        if (!Active(stock)) { return ""; }
        text.Clear(); text.Append(Word("작전 중", "Operating", "作戦中"));
        var automation = TradingOperationController.RunOn(stock);
        if (automation != null && automation.Active)
        {
            text.Append(" · ").Append(automation.AccumulateRunning ? Word("매집", "Accumulate", "買い集め") : automation.SpreadRunning ? Word("스프레드", "Spread", "スプレッド") : Word("자동 매매", "Auto", "自動売買"));
        }
        var tactics = PlayerTactics.Instance;
        if (tactics != null)
        {
            foreach (var run in tactics.Runs)
            {
                if (run.Stock != stock) { continue; }
                text.Append(" · ").Append(run.Tool switch
                {
                    PlayerTactics.Tool.Wash => Word("부풀리기", "Painting", "水増し"),
                    PlayerTactics.Tool.Wall => Word("매수벽", "Bid wall", "買い壁"),
                    PlayerTactics.Tool.Close => Word("종가 관리", "Close", "終値"),
                    PlayerTactics.Tool.Ignite => Word("받치기", "Holding", "下支え"),
                    PlayerTactics.Tool.Unload => Word("넘기기", "Unloading", "売り上がり"),
                    _ => Word("작전", "Ops", "作戦"),
                });
            }
        }
        return text.ToString();
    }
}

/// <summary>
/// 가운데 모니터(차트)의 종목 이름 아래, 종목 코드 옆에 "작전 중 · …" 한 줄을 띄운다. 지금 보는 종목에서 내가 무언가를 돌리고 있을 때만 보인다.
/// 앞의 점이 천천히 밝아졌다 어두워져서 "지금 돌고 있다"는 것을 알린다. 그 밖에는 움직이지 않는다.
/// 차트 화면의 글자 하나(종목 코드)를 본떠 만들기 때문에 크기와 글꼴이 그 화면과 같다. 씬 파일은 그대로다.
/// </summary>
public sealed class OperationBadge : MonoBehaviour
{
    private static readonly Color Amber = new Color32(236, 180, 90, 255);
    private TextMeshProUGUI label, source;
    private ChartManager chart;
    private float next;
    private string shown = null;

    void Update()
    {
        if (label != null && label.gameObject.activeSelf)
        {
            // 점만 숨 쉬듯 밝기가 바뀐다(글자는 그대로다).
            float glow = .55f + .45f * Mathf.Sin(Time.unscaledTime * 2.4f);
            var tint = Amber; tint.a = .55f + .45f * glow; label.color = tint;
        }
        if (Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .4f;
        var manager = StockManager.Instance;
        if (chart == null) { chart = FindAnyObjectByType<ChartManager>(); }
        if (manager == null || chart == null || chart.textSymbol == null) { return; }
        if (label == null)
        {
            source = chart.textSymbol;
            label = Instantiate(source, source.transform.parent); label.name = "Text :: Operation Badge";
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; label.alignment = source.alignment;
            label.gameObject.SetActive(false);
        }
        string line = PlayerOperations.Line(manager.CurrentStock);
        bool on = line.Length > 0;
        if (label.gameObject.activeSelf != on) { label.gameObject.SetActive(on); }
        if (!on) { shown = null; return; }
        // 종목 코드 바로 오른쪽에 놓는다. 코드의 길이가 종목마다 달라서 볼 때마다 맞춘다.
        var from = source.rectTransform; var to = label.rectTransform;
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot; to.sizeDelta = from.sizeDelta;
        to.anchoredPosition = from.anchoredPosition + new Vector2(source.preferredWidth + source.fontSize * .9f, 0f);
        string text = "● " + line;
        if (text != shown) { shown = text; GameLocalizedText.Set(label, text, true); }
    }
}
