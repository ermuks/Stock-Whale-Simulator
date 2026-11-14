using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>초기 배분 원장. 배분 뒤 잔여 재고는 단일 ExchangeBot 계좌로 이전하고 이 원장의 잔고는 0.</summary>
public class ExchangeWarehouse : MonoBehaviour, IOrderOwner
{
    public static ExchangeWarehouse Instance { get; private set; }

    private readonly Dictionary<Stock, decimal> holdings = new();
    private readonly HashSet<Stock> initialized = new();
    private ExchangeBot account;
    void Awake()
    {
        if (Instance != null && Instance != this)
        { return; }

        Instance = this;
    }

    void Start()
    {
        if (StockManager.Instance != null && !StockManager.Instance.useInitialEconomy)
        {
            InitializeFromStocks(StockManager.Instance.stocks);
        }
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
        holdings.Remove(stock);
        initialized.Remove(stock);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Bind(ExchangeBot owner)
    {
        if (account == null)
        {
            account = owner;
        }
    }

    public void SetHolding(Stock stock, decimal amount)
    {
        if (stock == null || account != null)
        { return; }

        holdings[stock] = Math.Max(0m, decimal.Floor(amount));
    }

    /// <summary>이 원장의 비예약 보유만 반환. Bind 이후는 0이며 Trader 검사와 중복 집계하지 않음.</summary>
    public decimal GetHolding(Stock stock) => stock != null && holdings.TryGetValue(stock, out var volume) ? volume : 0m;
    public decimal GetAvailableInventory(Stock stock) => account != null ? account.GetHolding(stock) : GetHolding(stock);
    public bool TransferTo(Trader trader, Stock stock, decimal amount, decimal pricePerShare)
    {
        amount = decimal.Floor(amount);
        if (trader == null || stock == null || stock.IsDelisting || amount <= 0m)
        { return false; }

        if (account != null)
        { return account.TransferInventory(trader, stock, amount, pricePerShare); }

        decimal have = GetHolding(stock);
        if (have < amount)
        { return false; }

        holdings[stock] = have - amount;
        decimal basis = StockManager.Instance != null && StockManager.Instance.useInitialEconomy ? pricePerShare : MarketDayController.Instance == null ? pricePerShare : MarketDayController.Instance.InitialCost(stock, pricePerShare);
        trader.GrantShares(stock, amount, basis);
        return true;
    }

    public void InitializeFromStocks(IReadOnlyList<Stock> stocks)
    {
        if (stocks == null)
        { return; }

        foreach (var stock in stocks)
        {
            InitializeStock(stock);
        }
    }

    public void InitializeStock(Stock stock)
    {
        if (stock == null || !initialized.Add(stock))
        { return; }

        if (account != null)
        {
            account.AcceptNewInventory(stock, stock.issuedShares);
            return;
        }

        if (!holdings.ContainsKey(stock))
        {
            decimal distributed = stock.orderBook.GetTotalAskVolume();
            foreach (var trader in Trader.Registry)
            {
                if (trader != null)
                {
                    distributed += trader.GetHolding(stock);
                }
            }

            holdings[stock] = Math.Max(0m, stock.issuedShares - distributed);
            if (distributed > stock.issuedShares)
            {
                Debug.LogError("초기 발행량 초과: " + stock.symbol);
            }
        }

        if (StockManager.Instance == null)
        { return; }

        if (StockManager.Instance.useInitialEconomy)
        { return; }

        foreach (var definition in StockManager.Instance.stockDefinitions)
        {
            if (definition.symbol != stock.symbol || definition.initialOwner == null || definition.initialOwner.IsExchangeParticipant)
            { continue; }

            float ratio = definition.playerOwner ? 0.4f : Mathf.Clamp(definition.ownerShareRatio, 0.4f, 0.9f);
            decimal desired = decimal.Floor(stock.issuedShares * (decimal)ratio);
            decimal missing = Math.Max(0m, desired - definition.initialOwner.GetOwnedVolume(stock));
            TransferTo(definition.initialOwner, stock, Math.Min(missing, GetHolding(stock)), stock.currentPrice);
            break;
        }
    }

    public decimal TakeRemaining(Stock stock)
    {
        decimal volume = GetHolding(stock);
        holdings[stock] = 0m;
        return volume;
    }

    /// <summary>기존 직접 공급 진입점은 중단. 모든 공급은 ExchangeBot의 목적/보호 한도 경로 사용.</summary>
    public TradeResult PlaceSellOrder(Stock stock, decimal price, decimal volume)
    { return new TradeResult(); }

    public void OnOrderFilled(Stock stock, decimal filledVolume, decimal totalCost)
    {
    }

    public void OnSellOrderCancelled(Stock stock, decimal cancelledVolume, decimal price)
    {
        ReceiveShares(stock, cancelledVolume);
    }

    public void OnBuyOrderCancelled(Stock stock, decimal cancelledVolume, decimal price)
    {
    }

    public void LiquidateStock(Stock stock)
    {
        account?.LiquidateStock(stock);
    }

    public void ReceiveShares(Stock stock, decimal volume)
    {
        volume = decimal.Floor(volume);
        if (stock == null || volume <= 0m)
        { return; }

        if (account != null)
        {
            account.AcceptNewInventory(stock, volume);
        }
        else
        {
            holdings[stock] = GetHolding(stock) + volume;
        }
    }
}
