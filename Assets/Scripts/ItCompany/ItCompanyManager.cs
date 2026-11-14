using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>회사 데이터 로딩 및 예약 공개. 미래 큐와 시계는 거래소 한 곳에 있다.</summary>
public class ItCompanyManager : MonoBehaviour
{
    public static ItCompanyManager Instance { get; private set; }

    public bool applyRealValueOnNodeChange = true;
    public float startNodeDelayMin = 3f, startNodeDelayMax = 10f;
    public static event Action<Stock, int, ItCompanyNode> OnStockNodeChanged;
    private ItCompanyDataRoot data;
    private readonly Dictionary<int, ItCompanyNode> nodes = new();
    private readonly List<ItCompanyNode> fakeNewsNodes = new();
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        string path = Path.Combine(Application.dataPath, "itCompany.js");
        if (!File.Exists(path))
        { return; }

        try
        {
            data = ItCompanyLoader.ParseFromJsContent(File.ReadAllText(path));
        }
        catch (IOException)
        { return; }

        if (data?.nodes == null)
        { return; }

        foreach (var node in data.nodes)
        {
            nodes[node.id] = node;
            if (node.EventKind != CompanyEventKind.Delisting && !string.IsNullOrEmpty(node.description))
            {
                fakeNewsNodes.Add(node);
            }
        }
    }

    void Start()
    {
        if (StockManager.Instance == null)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            RegisterStock(stock);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public ItCompanyNode FindNode(int id) => nodes.TryGetValue(id, out var node) ? node : null;
    public bool IsLoaded => nodes.Count > 0;

    public void RegisterStock(Stock stock)
    {
        if (stock == null || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        { return; }

        ExchangeBot.Instance?.RegisterCompanySchedule(stock, FindNode(data == null ? 1000 : data.startNodeId));
    }

    internal void PublishReserved(Stock stock, ReservedCompanyNews item, decimal baseValue)
    {
        if (stock == null || stock.IsDelisting || stock.IsDelisted)
        { return; }

        int before = ExchangeBot.Instance == null ? 0 : ExchangeBot.Instance.PublishedCompanyNode(stock);
        if (applyRealValueOnNodeChange)
        {
            stock.SetRealValue(Math.Max(0.00000001m, baseValue * (decimal)item.CriticalValue));
        }

        if (NewsManager.Instance != null && NewsManager.Instance.addNewsOnNodeChange)
        {
            NewsManager.Instance.AddNews(stock, item.CriticalValue, item.Node.description ?? "", item.EventId);
        }

        if (item.IsDelisting)
        {
            DelistingManager.Instance?.OnScheduledNewsPublished(stock, item.EventId);
        }

        OnStockNodeChanged?.Invoke(stock, before, item.Node);
    }

    public int GetCurrentNodeId(Stock stock) => ExchangeBot.Instance == null ? 0 : ExchangeBot.Instance.PublishedCompanyNode(stock);
    public ItCompanyNode GetCurrentNode(Stock stock) => FindNode(GetCurrentNodeId(stock));
    public string GetRandomNodeDescriptionForFakeNews(bool isPump)
    {
        if (fakeNewsNodes.Count == 0)
        { return null; }

        return fakeNewsNodes[UnityEngine.Random.Range(0, fakeNewsNodes.Count)].description;
    }
}
