/// <summary>호가창에 남은 내 주문이 체결될 때 알림 받기 (예치금 해제·현금 차감·보유 반영)</summary>
public interface IOrderOwner
{
    void OnOrderFilled(Stock stock, decimal filledVolume, decimal totalCost);
    /// <summary>매도 주문 취소 시 호출. 취소된 물량을 보유량에 다시 추가해야 함.</summary>
    void OnSellOrderCancelled(Stock stock, decimal cancelledVolume, decimal price);
    /// <summary>매수 주문 취소 시 호출. 예치금 해제.</summary>
    void OnBuyOrderCancelled(Stock stock, decimal cancelledVolume, decimal price);
}

public enum OrderPurpose
{
    Ordinary,
    ExchangeDefense,
    ExchangeInitialSupply,
    OwnerPreDisposal,
    Rhythm,
    /// <summary>현재가에서 떨어진 가격에 오래 걸어 두는 대기 주문("이 가격이면 산다/판다").</summary>
    Resting
}

public class Order
{
    public OrderPurpose purpose;
    public int purposeSlot;
    public decimal costBasis;
    public decimal price;
    public decimal volume;
    public bool isBuy;
    public long timestamp;
    public float createdAt;
    /// <summary>이 주문을 낸 트레이더. 매수 체결 시 현금 차감·보유 반영·예치 해제용</summary>
    public IOrderOwner owner;
    public Order(decimal price, decimal volume, bool isBuy, long timestamp, IOrderOwner owner = null)
    {
        this.price = price;
        this.volume = volume;
        this.isBuy = isBuy;
        this.timestamp = timestamp;
        createdAt = SessionClock.Time;
        this.owner = owner;
        purpose = owner is ExchangeBot exchange ? exchange.SubmittedPurpose : owner is WhaleBot whale && whale.IsDisposalSubmission ? OrderPurpose.OwnerPreDisposal : MarketRhythmDirector.SubmittingResting ? OrderPurpose.Resting : MarketRhythmDirector.Submitting ? OrderPurpose.Rhythm : OrderPurpose.Ordinary;
        purposeSlot = owner is ExchangeBot bot ? bot.SubmittedSlot : -1;
        costBasis = !isBuy && owner is Trader trader ? trader.SubmittedSellCost : 0m;
    }
}
