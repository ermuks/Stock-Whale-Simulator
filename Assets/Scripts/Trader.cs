using System.Collections.Generic;
using UnityEngine;

public class Trader : MonoBehaviour, IOrderOwner
{
    public virtual bool IsExchangeParticipant => false;
    public string OwnerId { get; internal set; }
    public Stock OwnedCompany { get; internal set; }
    public bool IsCompanyOwner => !string.IsNullOrEmpty(OwnerId);
    public bool InitialCashAssigned { get; internal set; }
    public int InitialAccountId { get; private set; }

    private static int nextInitialAccountId;
    protected Order LastRestingOrder;
    private decimal _cash = 100000m;
    public decimal cash
    {
        get => _cash;
        set
        {
            ExternalFlow(value - _cash);
            _cash = value;
        }
    }

    public static readonly List<Trader> Registry = new();
    private readonly Dictionary<Stock, (decimal volume, decimal cost)> sellEscrow = new();
    public decimal SubmittedSellCost { get; private set; }
    public decimal ReservedCash => reservedCash;

    public decimal EscrowVolume
    {
        get
        {
            decimal sum = 0m;
            foreach (var entry in sellEscrow.Values)
            {
                sum += entry.volume;
            }

            return sum;
        }
    }

    private decimal baseline, externalNet;
    private bool baselineReady;
    public decimal DailyReturn { get; private set; }
    public int DailySettledDay { get; private set; }

    protected virtual void Awake()
    {
        if (Registry.Count == 0)
        {
            nextInitialAccountId = 0;
        }

        InitialAccountId = ++nextInitialAccountId;
        Registry.Add(this);
    }

    protected virtual void OnDestroy()
    {
        Registry.Remove(this);
    }

    public void ApplyTradeCash(decimal amount)
    {
        _cash += amount;
    }

    public void ExternalFlow(decimal amount)
    {
        if (baselineReady)
        {
            externalNet += amount;
        }
    }

    public decimal Equity()
    {
        decimal result = cash;
        foreach (var pair in holdings)
        {
            result += pair.Value * pair.Key.currentPrice;
        }

        foreach (var pair in sellEscrow)
        {
            result += pair.Value.volume * pair.Key.currentPrice;
        }

        return result;
    }

    public void BeginDailyBaseline()
    {
        baseline = Equity();
        externalNet = 0m;
        baselineReady = true;
    }

    public void SettleDaily()
    {
        DailyReturn = baselineReady ? Equity() - baseline - externalNet : 0m;
        DailySettledDay = MarketDayController.Instance.Day;
        BeginDailyBaseline();
    }

    public decimal GetOwnedVolume(Stock stock)
    { return GetHolding(stock) + (sellEscrow.TryGetValue(stock, out var escrow) ? escrow.volume : 0m); }

    public decimal GetOwnedAverage(Stock stock)
    {
        sellEscrow.TryGetValue(stock, out var escrow);
        decimal qty = GetHolding(stock) + escrow.volume;
        return qty > 0m ? (GetHolding(stock) * GetAvgPrice(stock) + escrow.cost) / qty : 0m;
    }

    // Includes sell escrow, including positions no longer present in the listed-stock menu.
    public decimal GetOwnedCost(Stock stock)
    {
        sellEscrow.TryGetValue(stock, out var escrow);
        return GetHolding(stock) * GetAvgPrice(stock) + escrow.cost;
    }

    public void CopyOwnedStocks(List<Stock> destination)
    {
        destination.Clear();
        foreach (var pair in holdings)
        {
            if (pair.Value > 0m) { destination.Add(pair.Key); }
        }
        foreach (var pair in sellEscrow)
        {
            if (pair.Value.volume > 0m && !destination.Contains(pair.Key)) { destination.Add(pair.Key); }
        }
    }

    private void ChangeEscrow(Stock stock, decimal volume, decimal basis)
    {
        sellEscrow.TryGetValue(stock, out var old);
        decimal qty = old.volume + volume;
        if (qty <= 0m)
        {
            sellEscrow.Remove(stock);
        }
        else
        {
            sellEscrow[stock] = (qty, old.cost + volume * basis);
        }
    }

    private void PositionChanged(Stock stock)
    {
        if (this is TradeBot bot)
        {
            MarketDayController.Instance?.UpdatePositionIndex(bot, stock);
        }
    }

    public void OnSellOrderCancelledAtCost(Stock stock, decimal volume, decimal basis)
    {
        if (stock == null || volume <= 0m)
        { return; }

        ChangeEscrow(stock, -volume, basis);
        AddHolding(stock, volume, basis);
        RecordOrder($"SELL_CANCEL {volume:F0} (basis {basis:F0} returned)");
    }

    public void OnSellOrderFilledAtCost(Stock stock, decimal volume, decimal revenue, decimal price, decimal basis)
    {
        if (stock == null || volume <= 0m)
        { return; }

        ChangeEscrow(stock, -volume, basis);
        OnSellOrderFilled(stock, volume, revenue, price);
        PositionChanged(stock);
        MarketDayController.Instance?.Sold(this, stock, volume, price, basis);
    }

    /// <summary>미체결 매수 주문에 묶어둔 예치금. 새 매수 시 가용 금액 = cash - reservedCash</summary>
    protected decimal reservedCash;
    protected Dictionary<Stock, decimal> holdings = new();
    protected Dictionary<Stock, decimal> avgPrice = new();
    public const int OrderHistoryLimit = 512;
    private readonly Queue<string> orderHistory = new(OrderHistoryLimit);
    protected Stock CurrentStock => StockManager.Instance.CurrentStock;

    /// <summary>호가창에 남아 있던 내 매수 주문이 체결됐을 때 호출. 현금 차감·보유 반영·예치 해제.</summary>
    public virtual void OnOrderFilled(Stock stock, decimal filledVolume, decimal totalCost)
    {
        if (stock == null || filledVolume <= 0m || totalCost <= 0m)
        { return; }

        decimal release = totalCost > reservedCash ? reservedCash : totalCost;
        reservedCash -= release;
        if (reservedCash < 0m)
        {
            reservedCash = 0m;
        }

        ApplyTradeCash(-totalCost);
        AddHolding(stock, filledVolume, totalCost / filledVolume);
        RecordOrder($"BUY(book) {filledVolume:F0} avg@{totalCost / filledVolume:F0} (cost {totalCost:F0})");
    }

    /// <summary>매도 주문 취소 시 호출. 취소된 물량을 보유량에 다시 추가.</summary>
    public virtual void OnSellOrderCancelled(Stock stock, decimal cancelledVolume, decimal price)
    {
        if (stock == null || cancelledVolume <= 0m)
        { return; }

        OnSellOrderCancelledAtCost(stock, cancelledVolume, GetOwnedAverage(stock));
        RecordOrder($"SELL_CANCEL {cancelledVolume:F0} @ {price:F0} (returned)");
    }

    /// <summary>매수 주문 취소 시 호출. 예치금 해제.</summary>
    public virtual void OnBuyOrderCancelled(Stock stock, decimal cancelledVolume, decimal price)
    {
        if (stock == null || cancelledVolume <= 0m || price <= 0m)
        { return; }

        decimal release = cancelledVolume * price;
        if (release > reservedCash)
        {
            release = reservedCash;
        }

        reservedCash -= release;
        if (reservedCash < 0m)
        {
            reservedCash = 0m;
        }

        RecordOrder($"BUY_CANCEL {cancelledVolume:F0} @ {price:F0} (reserved released)");
    }

    public virtual void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        volume = decimal.Floor(volume);
        if (volume <= 0m)
        { return; }

        BuyInternal(ResolveStock(stock), price, volume, subTickSteps: subTickSteps);
    }

    public virtual void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        volume = decimal.Floor(volume);
        if (volume <= 0m)
        { return; }

        SellInternal(ResolveStock(stock), price, volume, subTickSteps: subTickSteps);
    }

    /// <summary>null이면 CurrentStock (플레이어가 보는 종목)</summary>
    protected Stock ResolveStock(Stock stock)
    { return stock != null ? stock : CurrentStock; }

    private void BuyInternal(Stock stock, decimal price, decimal volume, int subTickSteps = 0)
    {
        LastRestingOrder = null;
        if (!MarketDayController.CanTrade || volume <= 0m || stock == null || !stock.AcceptsOrders)
        { return; }

        decimal bookPrice = PriceTickPolicy.FloorToTick(price);
        decimal expectedCost = price * volume;
        decimal available = cash - reservedCash;
        if (available < expectedCost)
        { return; }

        TradeResult result = stock.ExecuteTrade(price, volume, true, addUnfilledToBook: true, subTickSteps: subTickSteps, orderOwner: this, maxMatchAskPrice: null);
        LastRestingOrder = result.restingOrder;
        if (result.filledVolume <= 0m)
        {
            decimal unfilled = volume - result.filledVolume;
            if (unfilled > 0m)
            {
                reservedCash += unfilled * bookPrice;
            }

            RecordOrder($"BUY(LIMIT?) {volume} @ {price} (NO FILL)");
            return;
        }

        ApplyTradeCash(-result.totalCost);
        AddHolding(stock, result.filledVolume, result.averagePrice);
        decimal unfilledToBook = volume - result.filledVolume;
        if (unfilledToBook > 0m)
        {
            reservedCash += unfilledToBook * bookPrice;
        }

        RecordOrder($"BUY {result.filledVolume:F0} avg@{result.averagePrice:F0} (cost {result.totalCost:F0})");
        ExchangeBot.Instance?.OnTradeSettled(stock);
    }

    private void SellInternal(Stock stock, decimal price, decimal volume, int subTickSteps = 0)
    {
        LastRestingOrder = null;
        if (!MarketDayController.CanTrade || volume <= 0m || stock == null || !stock.AcceptsOrders)
        { return; }

        decimal have = GetHolding(stock);
        if (have <= 0m)
        { return; }

        decimal sellVol = (volume <= have) ? volume : have;
        decimal avgCostBeforeSell = GetAvgPrice(stock);
        SubmittedSellCost = avgCostBeforeSell;
        ChangeEscrow(stock, sellVol, avgCostBeforeSell);
        RemoveHolding(stock, sellVol);
        TradeResult result = stock.ExecuteTrade(price, sellVol, false, addUnfilledToBook: true, subTickSteps: subTickSteps, orderOwner: this);
        LastRestingOrder = result.restingOrder;
        ApplyTradeCash(result.totalCost);
        ChangeEscrow(stock, -result.filledVolume, avgCostBeforeSell);
        PositionChanged(stock);
        MarketDayController.Instance?.Sold(this, stock, result.filledVolume, result.averagePrice, avgCostBeforeSell);
        if (result.filledVolume > 0m)
        {
            RecordOrder($"SELL {result.filledVolume:F0} avg@{result.averagePrice:F0} (rev {result.totalCost:F0})");
            OnSellFilled(stock, result.filledVolume, result.totalCost, result.averagePrice, avgCostBeforeSell);
        }
        else
        {
            RecordOrder($"SELL(LIMIT) {sellVol} @ {price} (PENDING)");
        }

        ExchangeBot.Instance?.OnTradeSettled(stock);
    }

    /// <summary>매도 체결 시 호출. 수익률 기반 종목 선호도 등에 사용. (filledVolume, totalCost, avgFillPrice, avgCostBeforeSell)</summary>
    protected virtual void OnSellFilled(Stock stock, decimal filledVolume, decimal totalCost, decimal avgFillPrice, decimal avgCostBeforeSell)
    {
    }

    protected void AddHolding(Stock stock, decimal filledVolume, decimal fillAvgPrice)
    {
        if (!holdings.ContainsKey(stock))
        {
            holdings[stock] = 0m;
            avgPrice[stock] = 0m;
        }

        decimal prevVol = holdings[stock];
        decimal prevAvg = avgPrice[stock];
        decimal newVol = prevVol + filledVolume;
        decimal newAvg = (newVol > 0m) ? ((prevVol * prevAvg) + (filledVolume * fillAvgPrice)) / newVol : 0m;
        holdings[stock] = newVol;
        avgPrice[stock] = newAvg;
        PositionChanged(stock);
    }

    protected void RemoveHolding(Stock stock, decimal filledVolume)
    {
        if (!holdings.ContainsKey(stock))
        { return; }

        holdings[stock] -= filledVolume;
        if (holdings[stock] <= 0m)
        {
            holdings[stock] = 0m;
            avgPrice[stock] = 0m;
        }
    }

    public decimal GetHolding(Stock stock)
    { return holdings.TryGetValue(stock, out var v) ? v : 0m; }

    public decimal GetAvgPrice(Stock stock)
    { return avgPrice.TryGetValue(stock, out var v) ? v : 0m; }

    /// <summary>주식 지급 (외부에서 직접 보유량 증가). 정수로 내림.</summary>
    public void GrantShares(Stock stock, decimal volume, decimal pricePerShare)
    {
        volume = decimal.Floor(volume);
        if (stock == null || volume <= 0m)
        { return; }

        ExternalFlow(volume * stock.currentPrice);
        AddHolding(stock, volume, pricePerShare);
        if (!baselineReady && MarketDayController.Instance != null && MarketDayController.Instance.Ready)
        {
            BeginDailyBaseline();
        }
    }

    private void RecordOrder(string entry)
    {
        if (orderHistory.Count >= OrderHistoryLimit)
        {
            orderHistory.Dequeue();
        }

        orderHistory.Enqueue(entry);
    }

    public List<string> GetOrderHistory() => new List<string>(orderHistory);
    public virtual void OnSellOrderFilled(Stock stock, decimal volume, decimal revenue, decimal price)
    {
        if (stock == null || volume <= 0m || revenue <= 0m)
        { return; }

        ApplyTradeCash(revenue);
        RecordOrder($"SELL(book) {volume:F0} avg@{price:F0} (rev {revenue:F0})");
    }

    /// <summary>강제로 보유량 제거 (이탈 시 미체결 물량 처리용)</summary>
    public void ForceRemoveHolding(Stock stock, decimal volume)
    {
        if (stock == null || volume <= 0m)
        { return; }

        volume = System.Math.Min(volume, GetHolding(stock));
        ExternalFlow(-volume * stock.currentPrice);
        RemoveHolding(stock, volume);
        PositionChanged(stock);
    }
}
