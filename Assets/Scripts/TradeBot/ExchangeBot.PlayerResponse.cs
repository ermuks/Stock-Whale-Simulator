using System;

public partial class ExchangeBot
{
    /// <summary>관찰 봇의 응답도 기존 거래소 예산·재고·위험 제한과 세 방어 슬롯을 사용한다.</summary>
    public Order TryPlayerResponse(Stock stock, bool buy, decimal price, decimal quantity)
    {
        var state = GetState(stock);
        if (!AssetsInitialized || state == null || !MarketDayController.CanTrade || !stock.AcceptsOrders || stock.currentPrice <= 0m || state.Risky || state.NewListing && state.CompletedCount < 60 || submittingState != null)
        { return null; }
        quantity = Math.Min(50m, decimal.Floor(quantity));
        if (quantity < 1m || !PriceTickPolicy.IsValidPrice(price) || Math.Abs(price / stock.currentPrice - 1m) > .01m)
        { return null; }
        const int slot = 2;
        var orders = buy ? state.Bids : state.Asks;
        if (stock.orderBook.IsActive(orders[slot]))
        { stock.orderBook.CancelOrder(orders[slot], stock); }
        LastRestingOrder = null; SubmittedPurpose = OrderPurpose.ExchangeDefense; SubmittedSlot = slot; submittingState = state;
        try
        {
            if (buy)
            { BuyLimit(price, quantity, 0, stock); }
            else
            { SellLimit(price, quantity, 0, stock); }
            orders[slot] = LastRestingOrder;
            return stock.orderBook.IsActive(LastRestingOrder) ? LastRestingOrder : null;
        }
        finally
        { submittingState = null; SubmittedSlot = -1; }
    }
}
