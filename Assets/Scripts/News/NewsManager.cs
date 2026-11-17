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
    public static event System.Action<NewsArticle> OnArticlePublished;
    public readonly List<NewsArticle> Articles = new(40);
    public int ArticleVersion { get; private set; }
    public NewsWritingCatalog Writing { get; private set; }
    public NewsArticle FindArticle(long id) => Articles.Find(article => article.id == id);
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
        var writing = Resources.Load<TextAsset>("News/news_writing");
        if (writing == null) { throw new System.InvalidOperationException("Missing News/news_writing"); }
        Writing = JsonUtility.FromJson<NewsWritingCatalog>(writing.text);
        LoadTitleVariants();
        if (GetComponent<CompanyNewsAudience>() == null) { gameObject.AddComponent<CompanyNewsAudience>(); }
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
    { Publish(stock, criticalValue, title, newsId); }

    [System.Serializable] private sealed class TitleVariants { public string source; public string[] variants; }
    [System.Serializable] private sealed class TitleCatalog { public int schemaVersion; public TitleVariants[] titles; }
    private readonly Dictionary<string, string[]> titleVariants = new();
    private void LoadTitleVariants()
    {
        var asset = Resources.Load<TextAsset>("News/news_titles");
        if (asset == null) { return; }
        var catalog = JsonUtility.FromJson<TitleCatalog>(asset.text);
        if (catalog == null || catalog.titles == null) { return; }
        foreach (var item in catalog.titles)
        {
            if (item != null && !string.IsNullOrEmpty(item.source) && item.variants != null && item.variants.Length > 0) { titleVariants[item.source] = item.variants; }
        }
    }
    /// <summary>
    /// 같은 소식이라도 종목과 기사마다 제목 문장이 달라지게 한다. 여러 회사가 같은 사건 흐름을 밟기 때문에 제목이 똑같이 반복됐다.
    /// 종목 기호와 기사 번호로 고르므로 난수를 쓰지 않고, 같은 기사는 언제나 같은 제목이 된다.
    /// </summary>
    private string VariedTitle(Stock stock, string title, long newsId)
    {
        if (stock == null || string.IsNullOrEmpty(title) || !titleVariants.TryGetValue(title, out var variants)) { return title; }
        int pick = (int)(NewsWriting.Hash(stock.symbol + ":" + newsId + ":" + title) % (uint)(variants.Length + 1));
        return pick == 0 ? title : variants[pick - 1];
    }

    public NewsArticle Publish(Stock stock, float criticalValue, string title, long newsId = 0, bool fabricated = false,
        string category = null, int variant = 0, long storyId = 0, bool correction = false, NewsArticle authored = null)
    {
        if (float.IsNaN(criticalValue) || float.IsInfinity(criticalValue) || criticalValue <= 0 || string.IsNullOrWhiteSpace(title))
        { return null; }
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
            { return null; }

            publishedIds[stock] = publishedIds.TryGetValue(stock, out long previousId) ? System.Math.Max(previousId, newsId) : newsId;
            OnMarketNews?.Invoke(stock, newsId, criticalValue);
        }

        if (authored == null && !fabricated && !correction) { title = VariedTitle(stock, title, newsId); }
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
        NewsArticle article = null;
        if (stock != null)
        {
            article = authored ?? NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
                DeviceMarketJournal.FullStamp(), SessionClock.Time, fabricated, category, variant, storyId, correction);
            if (authored != null) { article.id = newsId; article.storyId = newsId; }
            Articles.Insert(0, article);
            if (Articles.Count > 40) { Articles.RemoveAt(40); }
            ArticleVersion++;
        }
        OnNewsPublished?.Invoke(stock, newsId, title ?? "");
        if (article != null) { OnArticlePublished?.Invoke(article); }
        return article;
    }

    public NewsArticle PublishAcquisition(Stock stock, decimal ownership)
    {
        string title = stock.stockName + " 경영권 인수 확정";
        var article = new NewsArticle { symbol = stock.symbol, company = stock.stockName, title = title,
            summary = "플레이어가 과반 지분을 확보했습니다. 새 경영 전략과 주주환원 확대에 대한 기대가 커지고 있습니다.",
            body = "주주 원장에서 플레이어의 지분 " + ownership.ToString("0.00####") + "%가 확인됐습니다. 실제 체결이 완료된 지분을 기준으로 경영권이 이전됐습니다.\n\n" +
                "새 지배주주는 배당·재투자·현금 비축 정책을 결정할 수 있습니다. 시장에서는 자본 배분 개선과 성장 투자 가능성을 주요 호재로 평가하고 있습니다.\n\n" +
                "기업의 일일 배당은 장 마감 실적과 보유 현금에 따라 결정됩니다. 인수 자체가 이익이나 주가 상승을 보장하지는 않습니다.",
            category = "corporate", categoryLabel = "기업 인수", source = "기업 공시", verification = "지분 확인",
            time = DeviceMarketJournal.FullStamp(), publishedAt = SessionClock.Time, sentiment = 1, credibility = .98 };
        return Publish(stock, 1.30f, title, authored: article);
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
