using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public partial class WhaleBot
{
    public bool PreDisposal { get; private set; }
    public long DisposalEventId { get; private set; }
    public decimal DisposalStartingShares { get; private set; }
    public decimal DisposalSold => disposalFilledShares;
    public double DisposalNewsAt { get; private set; }

    [SerializeField]
    private double disposalRemainingSeconds, disposalSoldShares, disposalReservedShares;
    private float nextDisposal;
    private bool disposalSubmitting;
    private decimal disposalFilledShares;
    public bool IsDisposalSubmission => disposalSubmitting;

    private readonly List<Order> ownOrders = new(64);
    private readonly List<(decimal price, decimal volume)> bestBids = new(1);
    private static readonly ProfilerMarker DisposalMarker = new("DonDon.OwnerDisposal");
    private EconomySettings OwnerSettings => StockManager.Instance.economy;

    public void BindCompany(Stock stock, string ownerId)
    {
        OwnedCompany = stock;
        OwnerId = ownerId;
        autoDiscoverFollowers = false;
        lockTargetStock = true;
        targetStock = stock;
        chanceToTryRandomCancel = 0f;
    }

    public bool GetOwnFuture(out IReadOnlyList<ReservedCompanyNews> result)
    {
        result = null;
        return ExchangeBot.Instance != null && ExchangeBot.Instance.TryGetOwnFuture(this, out result);
    }

    public void BeginPreDisposal(ReservedCompanyNews item)
    {
        if (!IsCompanyOwner || item == null || !item.IsDelisting || PreDisposal || OwnedCompany == null || OwnedCompany.IsDelisted)
        { return; }

        PreDisposal = true;
        DisposalEventId = item.EventId;
        DisposalNewsAt = item.PublishAt;
        DisposalStartingShares = GetOwnedVolume(OwnedCompany);
        InvalidateStockIntents(OwnedCompany);
        OwnedCompany.orderBook.CopyOwnerOrders(this, ownOrders);
        foreach (var order in ownOrders)
        {
            if (order.isBuy)
            {
                OwnedCompany.orderBook.CancelOrder(order, OwnedCompany);
            }
        }

        decimal cap = decimal.Floor(DisposalStartingShares * (decimal)Mathf.Clamp01(OwnerSettings.disposalOutstandingRatio));
        decimal outstanding = OwnSellOutstanding();
        for (int i = ownOrders.Count - 1; i >= 0 && outstanding > cap; i--)
        {
            var order = ownOrders[i];
            if (!order.isBuy && OwnedCompany.orderBook.IsActive(order))
            {
                outstanding -= order.volume;
                OwnedCompany.orderBook.CancelOrder(order, OwnedCompany);
            }
        }

        foreach (var order in ownOrders)
        {
            if (!order.isBuy && OwnedCompany.orderBook.IsActive(order))
            {
                order.purpose = OrderPurpose.OwnerPreDisposal;
            }
        }

        nextDisposal = Time.time + InitialAccountId % 10 * 0.1f;
    }

    private decimal OwnSellOutstanding()
    {
        decimal total = 0m;
        OwnedCompany.orderBook.CopyOwnerOrders(this, ownOrders);
        foreach (var order in ownOrders)
        {
            if (!order.isBuy)
            {
                total += order.volume;
            }
        }

        return total;
    }

    protected override bool CanBuyStock(Stock stock) => base.CanBuyStock(stock) && !(PreDisposal && stock == OwnedCompany);
    protected override bool CanSellStock(Stock stock) => base.CanSellStock(stock) && (!(PreDisposal && stock == OwnedCompany) || disposalSubmitting);
    protected override void OnSellFilled(Stock stock, decimal filledVolume, decimal totalCost, decimal avgFillPrice, decimal avgCostBeforeSell)
    {
        if (PreDisposal && stock == OwnedCompany)
        {
            disposalFilledShares += filledVolume;
        }

        base.OnSellFilled(stock, filledVolume, totalCost, avgFillPrice, avgCostBeforeSell);
    }

    public override void OnSellOrderFilled(Stock stock, decimal volume, decimal revenue, decimal price)
    {
        if (PreDisposal && stock == OwnedCompany)
        {
            disposalFilledShares += volume;
        }

        base.OnSellOrderFilled(stock, volume, revenue, price);
    }

    private void UpdatePreDisposal()
    {
        disposalRemainingSeconds = Math.Max(0, DisposalNewsAt - (ExchangeBot.Instance == null ? 0 : ExchangeBot.Instance.NewsClock));
        disposalSoldShares = (double)DisposalSold;
        if (OwnedCompany == null || OwnedCompany.IsDelisting || OwnedCompany.IsDelisted || !MarketDayController.CanTrade || !OwnedCompany.AcceptsOrders || Time.time < nextDisposal)
        { return; }

        nextDisposal = Time.time + Mathf.Max(0.1f, OwnerSettings.disposalInterval);
        using (DisposalMarker.Auto())
        {
            decimal outstanding = OwnSellOutstanding();
            decimal cap = decimal.Floor(DisposalStartingShares * (decimal)Mathf.Clamp01(OwnerSettings.disposalOutstandingRatio));
            decimal chunk = decimal.Floor(DisposalStartingShares * (decimal)Mathf.Clamp01(OwnerSettings.disposalChunkRatio));
            decimal price = PriceTickPolicy.CeilToTick(OwnedCompany.currentPrice * (decimal)Mathf.Max(0.001f, OwnerSettings.disposalLimitRatio));
            decimal quantity = Math.Min(chunk, Math.Min(GetHolding(OwnedCompany), Math.Max(0m, cap - outstanding)));
            for (int i = 0; i < ownOrders.Count; i++)
            {
                var order = ownOrders[i];
                if (order.isBuy || Time.time - order.createdAt < OwnerSettings.staleSellSeconds)
                { continue; }

                decimal remaining = order.volume;
                OwnedCompany.orderBook.CancelOrder(order, OwnedCompany);
                quantity = Math.Min(chunk, Math.Min(remaining, Math.Max(0m, cap - outstanding + remaining)));
                OwnedCompany.orderBook.GetTopBids(bestBids, 1);
                if (bestBids.Count > 0)
                {
                    price = PriceTickPolicy.FloorToTick(bestBids[0].price);
                }

                break;
            }

            quantity = Math.Min(quantity, GetHolding(OwnedCompany));
            if (quantity > 0m && price > 0m)
            {
                disposalSubmitting = true;
                try
                {
                    SellLimit(price, quantity, 0, OwnedCompany);
                }
                finally
                {
                    disposalSubmitting = false;
                }
            }

            disposalReservedShares = (double)OwnSellOutstanding();
        }
    }

    private void InvestOwnerAccount(Stock stock)
    {
        if (!IsCompanyOwner || stock == null || !stock.AcceptsOrders || PreDisposal && stock == OwnedCompany)
        { return; }

        decimal reference = GetReferencePrice(stock);
        if (reference <= 0m || stock.currentPrice <= 0m)
        { return; }

        if (stock.currentPrice <= reference && UnityEngine.Random.value < 0.5f)
        {
            decimal price = GetSpreadSafePrice(stock, true) ?? PriceTickPolicy.FloorToTick(reference * 0.99m);
            if (price > 0m)
            {
                DelayedBuyLimit(price, decimal.Floor(Math.Max(0m, cash - ReservedCash) * buyCashRatio / price), 0, stock);
            }
        }
        else if (GetHolding(stock) > 0m)
        {
            decimal price = GetEffectiveSellPrice(stock, PriceTickPolicy.CeilToTick(reference * 1.01m));
            DelayedSellLimit(price, decimal.Floor(GetHolding(stock) * sellHoldingRatio), 0, stock);
        }
    }
}
