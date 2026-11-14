using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가짜 뉴스 발행 관리. FakeValueAndNews.md 설계에 따름.
/// - 세력이 fake value를 조작한 뒤 N초가 지나야 가짜 뉴스 발행 가능.
/// - itCompany 노드 description을 뉴스 헤드라인으로 사용.
/// </summary>
public class FakeNewsManager : MonoBehaviour
{
    public static FakeNewsManager Instance { get; private set; }

    [Header("타이밍")]
    [Tooltip("조작 후 이 시간(초)이 지나야 가짜 뉴스 발행 가능")]
    public float minSecondsAfterManipulation = 30f;

    [Header("정책")]
    [Tooltip("같은 조작에 대해 뉴스 1회만 발행 여부")]
    public bool oneNewsPerManipulation = true;

    /// <summary>종목별: (조작 시각, 이미 뉴스 발행했는지)</summary>
    private Dictionary<Stock, (float time, bool published)> _manipulations = new Dictionary<Stock, (float, bool)>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        StockManager.OnStockDelisted += OnStockDelisted;
    }

    void OnDisable()
    {
        StockManager.OnStockDelisted -= OnStockDelisted;
    }

    void OnStockDelisted(Stock stock)
    {
        if (stock != null)
            _manipulations.Remove(stock);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 세력이 fake value를 조작했을 때 호출. 타임스탬프 기록.
    /// WhaleBot 등에서 SetFakeValue 직후 호출.
    /// </summary>
    public void RecordManipulation(Stock stock)
    {
        if (stock == null) return;
        float now = Time.time;
        _manipulations[stock] = (now, false);
    }

    /// <summary>
    /// 가짜 뉴스 발행 시도. 현재 시각 - 조작 시각 >= N초 이고 아직 미발행이면 발행.
    /// </summary>
    /// <param name="stock">종목</param>
    /// <param name="isPump">펌프(상승) 뉴스면 true, 덤프(하락)면 false. criticalValue 선택에 사용</param>
    /// <returns>발행 성공 여부</returns>
    public bool TryPublishFakeNews(Stock stock, bool isPump)
    {
        if (stock == null) return false;
        if (NewsManager.Instance == null) return false;
        if (!_manipulations.TryGetValue(stock, out var rec)) return false;

        float elapsed = Time.time - rec.time;
        if (elapsed < minSecondsAfterManipulation) return false;
        if (oneNewsPerManipulation && rec.published) return false;

        string title = GetFakeNewsTitle(stock, isPump);
        if (string.IsNullOrEmpty(title)) return false;

        float criticalValue = isPump ? 1.08f : 0.92f; // 호재/악재 색상용
        NewsManager.Instance.AddNews(stock, criticalValue, title);

        _manipulations[stock] = (rec.time, true);
        Debug.Log($"[세력] 가짜 뉴스 발행 — {stock.stockName}({stock.symbol}) {(isPump ? "호재" : "악재")} | {title}");
        return true;
    }

    /// <summary>해당 종목이 N초 경과 후 뉴스 발행 가능한지</summary>
    public bool CanPublish(Stock stock)
    {
        if (stock == null || !_manipulations.TryGetValue(stock, out var rec))
            return false;
        if (oneNewsPerManipulation && rec.published)
            return false;
        return (Time.time - rec.time) >= minSecondsAfterManipulation;
    }

    string GetFakeNewsTitle(Stock stock, bool isPump)
    {
        if (ItCompanyManager.Instance == null || !ItCompanyManager.Instance.IsLoaded)
            return null;

        return ItCompanyManager.Instance.GetRandomNodeDescriptionForFakeNews(isPump);
    }
}
