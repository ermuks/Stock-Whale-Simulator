using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>Inspector에서 종목 추가용. 심볼, 이름, 시작가 랜덤 범위(최소~최대).</summary>
[System.Serializable]
public class StockDefinition
{
    [Tooltip("종목 코드 (예: AAPL)")]
    public string symbol = "";
    [Tooltip("표시 이름")]
    public string name = "";
    [Tooltip("시작가 랜덤 범위 - 최소")]
    public float priceMin = 50f;
    [Tooltip("시작가 랜덤 범위 - 최대")]
    public float priceMax = 200f;
    [Tooltip("발행 주식 수. 거래소 창고가 전량 보유. 백억(10^10) 이상 가능. Unity는 decimal 직렬화 미지원이라 long 사용, Stock 생성 시 decimal로 전달.")]
    public long issuedShares = 10_000_000_000L;
    [Tooltip("초기 소유주 계좌. 비우면 별도 소유주를 생성하지 않음")]
    public Trader initialOwner;
    public bool playerOwner;
    [Range(0.4f, 0.9f)]
    public float ownerShareRatio = 0.4f;
}

public partial class StockManager : MonoBehaviour
{
    public static StockManager Instance;
    public static event Action<Stock> OnStockSelected;
    private readonly HashSet<Stock> pendingDelistings = new();
    [Tooltip("Inspector에서 종목 추가. 시작가가 priceMin~priceMax 사이 랜덤으로 설정됩니다.")]
    public List<StockDefinition> stockDefinitions = new List<StockDefinition>();
    [HideInInspector]
    public List<Stock> stocks = new List<Stock>();
    public Stock CurrentStock;
    [Header("FakeValue 수렴")]
    [Tooltip("1초당 realValue 쪽으로 fakeValue가 이 비율만큼 수렴 (0.2 = 20%/초)")]
    [Range(0.01f, 1f)]
    public float fakeValueReversionPerSecond = 0.2f;
    /// <summary>상장폐지 시 (종목). 거래소 봇이 StockManager.Delist 호출 후 발생.</summary>
    public static event Action<Stock> OnStockDelisted;
    public static event Action<Stock> OnStockListed;
    void Awake()
    {
        Instance = this;
        stocks.Clear();
        if (useInitialEconomy)
        {
            economy.IncludeNewDefaults();
            economy.ApplyRetailWealth();
            BuildEconomyStocks();
            return;
        }

        EnsureStockDefinitions();
        foreach (var def in stockDefinitions)
        {
            if (string.IsNullOrWhiteSpace(def.name))
            { continue; }

            float min = Mathf.Min(def.priceMin, def.priceMax);
            float max = Mathf.Max(def.priceMin, def.priceMax);
            decimal raw = (decimal)UnityEngine.Random.Range(min, max);
            decimal startPrice = PriceTickPolicy.RoundToTick(raw);
            if (startPrice <= 0m)
            {
                startPrice = PriceTickPolicy.GetTickSize(raw);
            }

            decimal issued = def.issuedShares > 0L ? (decimal)def.issuedShares : 10_000_000_000m;
            stocks.Add(new Stock(def.symbol, def.name.Trim(), startPrice, issued));
        }

        if (stocks.Count > 0)
        {
            CurrentStock = stocks[0];
        }
    }

    public void EnsureStockDefinitions()
    {
        // 기본 종목 세팅 (스프레드시트 기반).
        // 발행량 컬럼은 '천 주' 단위로 보관되어 있으므로 ×1000 해서 실제 발행량으로 사용.
        if (stockDefinitions.Count == 0)
        {
            stockDefinitions.Add(new StockDefinition { symbol = "NM", name = "노보손해보험", priceMin = 100000f, priceMax = 100000f, issuedShares = 200_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "DSA", name = "덕상가주에이아이", priceMin = 1200000f, priceMax = 1200000f, issuedShares = 200_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "HBF", name = "두덕리식품", priceMin = 350000f, priceMax = 350000f, issuedShares = 1_500_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "MBI", name = "미래바이오", priceMin = 77000f, priceMax = 77000f, issuedShares = 2_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "DDP", name = "대만디스플레이", priceMin = 49250f, priceMax = 49250f, issuedShares = 18_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "KMF", name = "국민금융", priceMin = 48800f, priceMax = 48800f, issuedShares = 19_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "GFD", name = "그린푸드", priceMin = 33000f, priceMax = 33000f, issuedShares = 12_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "CBT", name = "코어바이오", priceMin = 45000f, priceMax = 45000f, issuedShares = 15_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "CED", name = "코어전자", priceMin = 52700f, priceMax = 52700f, issuedShares = 23_000_000L });
            stockDefinitions.Add(new StockDefinition { symbol = "NXT", name = "넥스트테크", priceMin = 31300f, priceMax = 31300f, issuedShares = 30_000_000L });
        }
    }

    void Update()
    {
        HandleStockNavigation();
        if (!SetupConfirmed || stocks == null)
        { return; }

        // fakeValue는 더 이상 realValue로 수렴하지 않는다. 개미들이 각자 믿는 가격의 집계값이다.
        UpdateCrowdValues();
    }

    /// <summary>진행 중 신규 상장. 가상 과거를 만들지 않으며 자산·차트·거래소에 같은 종목 등록.</summary>
    public Stock ListStock(string symbol, string name, decimal startPrice, decimal issuedShares)
    {
        if (useInitialEconomy && FindStock(symbol) != null)
        { return ListOwnerCompany(symbol); }

        if (!SetupConfirmed || startPrice <= 0m || issuedShares < 1m)
        { return null; }

        foreach (var existing in stocks)
        {
            if (existing.symbol == symbol)
            { return null; }
        }

        decimal initialPrice = PriceTickPolicy.RoundToTick(startPrice);
        if (initialPrice <= 0m)
        {
            initialPrice = PriceTickPolicy.GetTickSize(startPrice);
        }

        var stock = new Stock(symbol, name, initialPrice, decimal.Floor(issuedShares))
        {
            IsNewListing = true
        };
        stocks.Add(stock);
        ExchangeWarehouse.Instance?.InitializeStock(stock);
        CandleHistoryManager.Instance?.RegisterStock(stock);
        StockInterestManager.Instance?.RegisterStock(stock);
        StockStatistics.Instance?.RegisterStock(stock);
        ItCompanyManager.Instance?.RegisterStock(stock);
        PsychologicalLevelManager.Instance?.RegisterStock(stock);
        OnStockListed?.Invoke(stock);
        return stock;
    }

    private void HandleStockNavigation()
    {
        if (PauseMenuController.BlocksInput) { return; }
        if (!SetupConfirmed || stocks == null || stocks.Count == 0)
        { return; }
        // 종목 넘기기는 위·아래 화살표다. W와 S는 화면·태블릿 단축키가 됐다.
        int direction = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : Input.GetKeyDown(KeyCode.DownArrow) ? 1 : 0;
        if (direction == 0)
        { return; }
        var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (selected != null && (selected.GetComponentInParent<TMP_InputField>() != null || selected.GetComponentInParent<InputField>() != null))
        { return; }
        SelectRelativeStock(direction);
    }

    public void SelectRelativeStock(int direction)
    {
        if (stocks == null || stocks.Count == 0 || direction == 0)
        { return; }
        if (StockListUIManager.VisibleList != null && StockListUIManager.VisibleList.SelectRelativeVisibleStock(direction))
        { return; }
        int index = stocks.IndexOf(CurrentStock);
        if (index < 0)
        {
            SelectStock(direction < 0 ? stocks.Count - 1 : 0);
            return;
        }
        int next = (int)(((long)index + direction) % stocks.Count);
        if (next < 0)
        {
            next += stocks.Count;
        }
        SelectStock(next);
    }

    public void SelectStock(int index)
    {
        if (stocks == null || index < 0 || index >= stocks.Count || CurrentStock == stocks[index])
        { return; }
        CurrentStock = stocks[index];
        OnStockSelected?.Invoke(CurrentStock);
    }

    /// <summary>상장폐지 처리. 거래소 봇 등 권한 있는 쪽에서 호출. 리스트에서 제거 후 CurrentStock 보정.</summary>
    public void Delist(Stock stock)
    {
        if (stock == null || !stocks.Contains(stock) || !pendingDelistings.Add(stock))
        { return; }

        stock.IsDelisting = true;
        StartCoroutine(CompleteDelisting(stock));
    }

    private System.Collections.IEnumerator CompleteDelisting(Stock stock)
    {
        while (stock.orderBook != null && stock.orderBook.HasOrders)
        {
            int budget = MarketDayController.Instance == null ? 128 : Mathf.Max(1, MarketDayController.Instance.closeOrdersPerFrame);
            stock.orderBook.CancelAllBudgeted(stock, budget);
            yield return null;
        }

        pendingDelistings.Remove(stock);
        if (!stocks.Remove(stock))
        { yield break; }

        if (CurrentStock == stock)
        {
            CurrentStock = stocks.Count > 0 ? stocks[0] : null;
        }

        stock.IsListed = false;
        stock.IsDelisted = true;
        OnStockDelisted?.Invoke(stock);
    }
}
