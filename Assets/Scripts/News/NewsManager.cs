using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뉴스 제목 목록 UI. 최대 20개 캐싱, 새 뉴스 시 해당 종목 관심도 보너스 적용.
/// itCompany 노드 전환 시 자동으로 뉴스 추가 (설정으로 끌 수 있음).
/// </summary>
public class NewsManager : MonoBehaviour
{
    public static event System.Action<Stock, long, float> OnMarketNews;
    public static event System.Action<Stock, long, string> OnNewsPublished;
    private long nextNewsId;
    private readonly Dictionary<Stock, HashSet<long>> reservedIds = new();
    public long ReserveEventId(Stock stock)
    {
        if (!reservedIds.TryGetValue(stock, out var ids))
        {
            ids = new HashSet<long>();
            reservedIds.Add(stock, ids);
        }

        long id = ++nextNewsId;
        ids.Add(id);
        return id;
    }

    public void AddStatusNews(Stock stock, string title)
    {
        AddNews(null, 1f, title);
    }

    private readonly Dictionary<Stock, long> publishedIds = new();
    public static NewsManager Instance { get; private set; }

    [Header("뉴스 목록 UI")]
    [Tooltip("ScrollView의 Content. 여기에 뉴스 행이 추가됨")]
    public Transform content;
    [Tooltip("뉴스 한 행 프리팹 (NewsListItem 붙어 있어야 함)")]
    public GameObject itemPrefab;
    [Header("itCompany 연동")]
    [Tooltip("itCompany 노드 전환 시 해당 노드 description을 뉴스로 자동 추가할지")]
    public bool addNewsOnNodeChange = true;
    const int MaxNewsCount = 20;
    private struct NewsEntry
    {
        public string symbol;
        public float criticalValue;
        public string title;
    }

    private readonly List<NewsEntry> _news = new List<NewsEntry>(MaxNewsCount + 1);
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        StockManager.OnStockDelisted += OnDelisted;
        // FakeNewsManager가 없으면 이 오브젝트에 추가 (FakeValueAndNews.md 설계용)
        if (FakeNewsManager.Instance == null)
        {
            gameObject.AddComponent<FakeNewsManager>();
        }
    }

    void Start()
    {
        if (DelistingManager.Instance == null)
        {
            gameObject.AddComponent<DelistingManager>();
        }

        if (FakeNewsManager.Instance == null)
        {
            gameObject.AddComponent<FakeNewsManager>();
        }
    }

    void OnDestroy()
    {
        StockManager.OnStockDelisted -= OnDelisted;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnDelisted(Stock stock)
    {
        publishedIds.Remove(stock);
        reservedIds.Remove(stock);
    }

    /// <summary>
    /// 뉴스 추가. 목록에 넣고(최대 20개), 해당 종목 관심도에 Mathf.Abs(criticalValue - 1) * 1000 반영.
    /// </summary>
    public void AddNews(Stock stock, float criticalValue, string title, long newsId = 0)
    {
        if (newsId <= 0)
        {
            newsId = ++nextNewsId;
        }
        else
        {
            nextNewsId = System.Math.Max(nextNewsId, newsId);
        }

        if (stock != null)
        {
            if (!(reservedIds.TryGetValue(stock, out var reservations) && reservations.Remove(newsId)) && publishedIds.TryGetValue(stock, out long lastId) && newsId <= lastId)
            { return; }

            publishedIds[stock] = publishedIds.TryGetValue(stock, out long previousId) ? System.Math.Max(previousId, newsId) : newsId;
            OnMarketNews?.Invoke(stock, newsId, criticalValue);
        }

        string symbol = stock != null ? stock.symbol : "";
        _news.Insert(0, new NewsEntry { symbol = symbol, criticalValue = criticalValue, title = title ?? "" });
        while (_news.Count > MaxNewsCount)
        {
            _news.RemoveAt(_news.Count - 1);
        }

        if (stock != null)
        {
            if (StockInterestManager.Instance != null)
            {
                float interestBonus = Mathf.Abs(criticalValue - 1f) * 1000f;
                StockInterestManager.Instance.AddInterestDelta(stock, interestBonus);
            }

            if (StockNewsAttentionManager.Instance != null)
            {
                StockNewsAttentionManager.Instance.ApplyNewsCriticalValue(stock, criticalValue);
            }
        }

        RefreshUI();
        OnNewsPublished?.Invoke(stock, newsId, title ?? "");
    }

    void RefreshUI()
    {
        if (content == null || itemPrefab == null)
        { return; }

        for (int i = content.childCount; i < _news.Count; i++)
        {
            var go = Instantiate(itemPrefab, content);
            go.SetActive(true);
        }

        int count = _news.Count;
        for (int i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            bool show = i < count;
            child.gameObject.SetActive(show);
            if (show)
            {
                var entry = _news[i];
                var item = child.GetComponent<NewsListItem>();
                if (item != null)
                {
                    item.Set(entry.symbol, entry.criticalValue, entry.title);
                }
            }
        }
    }
}
