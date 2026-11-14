using System;
using Unity.Profiling;

public enum StockRiskGrade
{
    Safe,
    Warning,
    Danger,
    HighRisk,
    Halted
}

[Serializable]
public class Stock
{
    private static readonly ProfilerMarker TradeMarker = new("DonDon.Trade");
    public bool IsDelisting { get; internal set; }
    public bool IsNewListing { get; internal set; }
    public bool IsListed { get; internal set; } = true;
    public bool IsDelisted { get; internal set; }
    public string OwnerId { get; internal set; }
    public Trader Owner { get; internal set; }
    public StockRiskGrade RiskGrade { get; internal set; }
    public StockRiskGrade PreviousRiskGrade { get; internal set; }
    public bool RiskPending { get; internal set; } = true;
    public bool IsTradingHalted => RiskGrade == StockRiskGrade.Halted;
    public bool AcceptsOrders => IsListed && !IsDelisted && !IsDelisting && !IsTradingHalted && (StockManager.Instance == null || StockManager.Instance.SetupConfirmed);
    public int OrderEpoch { get; internal set; }
    public int RiskVersion { get; internal set; }
    public float RiskChangedAt { get; internal set; }
    public float HaltUntil { get; internal set; }

    public event Action<Stock, StockRiskGrade, StockRiskGrade> OnRiskChanged;
    internal void RaiseRiskChanged(StockRiskGrade before)
    {
        OnRiskChanged?.Invoke(this, before, RiskGrade);
    }

    public string RiskLabel => RiskPending ? "판정 대기" : RiskGrade == StockRiskGrade.Safe ? "안전" : RiskGrade == StockRiskGrade.Warning ? "경고" : RiskGrade == StockRiskGrade.Danger ? "위험" : RiskGrade == StockRiskGrade.HighRisk ? "고위험" : "거래중지";
    public UnityEngine.Color RiskColor => StockManager.Instance == null ? UnityEngine.Color.gray : RiskGrade == StockRiskGrade.Safe ? StockManager.Instance.economy.safeColor : RiskGrade == StockRiskGrade.Warning ? StockManager.Instance.economy.warningColor : RiskGrade == StockRiskGrade.Danger ? StockManager.Instance.economy.dangerColor : RiskGrade == StockRiskGrade.HighRisk ? StockManager.Instance.economy.highColor : StockManager.Instance.economy.haltedColor;
    public string RiskDisplay => "<color=#" + UnityEngine.ColorUtility.ToHtmlStringRGB(RiskPending ? UnityEngine.Color.gray : RiskColor) + ">" + RiskLabel + (IsTradingHalted ? " " + UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(0f, HaltUntil - UnityEngine.Time.time)) + "초" : "") + "</color>";

    public string symbol;
    public string stockName;
    public decimal currentPrice;
    /// <summary>발행 주식 수. 거래소 창고가 초기에 전량 보유.</summary>
    public decimal issuedShares;
    /// <summary>진짜가치. itCompany/펀더멘털 등에서 갱신. UI는 OnRealOrFakeValueChanged 구독.</summary>
    public decimal realValue;
    /// <summary>가짜가치. 세력 조작·itCompany 등에서 갱신. UI는 OnRealOrFakeValueChanged 구독.</summary>
    public decimal fakeValue;
    public OrderBook orderBook;
    public event Action<decimal> OnPriceChanged;
    public event Action<decimal, decimal> OnTradeExecuted;
    /// <summary>체결 시 (종목, 체결가, 체결량). 관심도 등 외부에서 종목 구분용</summary>
    public event Action<Stock, decimal, decimal> OnTradeExecutedWithStock;
    /// <summary>실제 개별 체결. 거래소 포함 여부와 주문별 초기 공급 판매량에 사용.</summary>
    public event Action<Stock, decimal, decimal, IOrderOwner, IOrderOwner, Order> OnMatchedTrade;
    internal void Matched(decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        OnMatchedTrade?.Invoke(this, price, volume, buyer, seller, resting);
    }

    /// <summary>진짜가치/가짜가치가 변경될 때 (itCompany·세력 조작 등). 차트 UI 갱신용.</summary>
    public event Action OnRealOrFakeValueChanged;
    public Stock(string symbol, string name, decimal startPrice, decimal issuedShares = 10_000_000_000m)
    {
        startPrice = PriceTickPolicy.RoundToTick(Math.Max(1m, startPrice));
        this.symbol = symbol ?? "";
        stockName = name;
        currentPrice = startPrice;
        this.issuedShares = issuedShares > 0m ? issuedShares : 10_000_000_000m;
        realValue = startPrice;
        fakeValue = startPrice;
        orderBook = new OrderBook(startPrice);
    }

    /// <summary>레거시: 심볼 없이 이름과 가격만</summary>
    public Stock(string name, decimal startPrice) : this("", name, startPrice)
    {
    }

    /// <param name = "addUnfilledToBook">false면 시장가: 미체결 수량을 호가창에 넣지 않음</param>
    /// <param name = "subTickSteps">호환 인자. 모든 주문에 한국 주식 호가 단위 적용</param>
    /// <param name = "orderOwner">매수 미체결 분 호가 등록·체결 시 알림 (예치금 해제용)</param>
    /// <param name = "maxMatchAskPrice">매수 시 이 가격 초과 매도는 체결 안 함. 시장가 매수로 고가 매도가 소진되는 것 방지.</param>
    public TradeResult ExecuteTrade(decimal price, decimal volume, bool isBuy, bool addUnfilledToBook = true, int subTickSteps = 0, IOrderOwner orderOwner = null, decimal? maxMatchAskPrice = null)
    {
        using (TradeMarker.Auto())
        {
            if (!MarketDayController.CanTrade || !AcceptsOrders)
            { return new TradeResult(); }

            TradeResult result = orderBook.PlaceOrder(price, volume, isBuy, addUnfilledToBook, subTickSteps, orderOwner, this, maxMatchAskPrice);
            if (result.filledVolume > 0m)
            {
                currentPrice = orderBook.lastPrice;
                OnTradeExecuted?.Invoke(currentPrice, result.filledVolume);
                OnTradeExecutedWithStock?.Invoke(this, currentPrice, result.filledVolume);
                OnPriceChanged?.Invoke(currentPrice);
            }

            return result;
        }
    }

    /// <summary>매도 주문 취소: 현재가 대비 rangeMinPct~rangeMaxPct 밖이면 무조건 취소, 안이면 거리 비율로 취소 확률.</summary>
    public int CancelSellOrdersByOwner(IOrderOwner owner, decimal currentPrice, decimal rangeMinPct = -0.20m, decimal rangeMaxPct = 0.80m)
    { return orderBook?.CancelSellOrdersByOwner(owner, currentPrice, rangeMinPct, rangeMaxPct, this) ?? 0; }

    /// <summary>소유자의 매도 주문 중 가격이 maxPrice 미만인 것만 전부 취소. RealPrice 미만 벽 제거용.</summary>
    public int CancelSellOrdersByOwnerBelowPrice(IOrderOwner owner, decimal maxPrice)
    { return orderBook?.CancelSellOrdersByOwnerBelowPrice(owner, maxPrice, this) ?? 0; }

    /// <summary>대기 주문 랜덤 취소: 소유자의 매수/매도 주문을 각각 cancelChance 확률로 취소.</summary>
    public int CancelRandomOrdersByOwner(IOrderOwner owner, float cancelChance)
    { return orderBook?.CancelRandomOrdersByOwner(owner, this, cancelChance) ?? 0; }

    /// <summary>진짜가치 설정 후 OnRealOrFakeValueChanged 호출. itCompany/시스템에서 호출.</summary>
    public void SetRealValue(decimal value)
    {
        if (realValue == value)
        { return; }

        realValue = value;
        OnRealOrFakeValueChanged?.Invoke();
    }

    /// <summary>가짜가치 설정 후 OnRealOrFakeValueChanged 호출. 세력 조작/itCompany에서 호출.</summary>
    public void SetFakeValue(decimal value)
    {
        if (fakeValue == value)
        { return; }

        fakeValue = value;
        OnRealOrFakeValueChanged?.Invoke();
    }
}

public class TradeResult
{
    public Order restingOrder;
    public decimal filledVolume;
    public decimal totalCost;
    public decimal averagePrice;
}
