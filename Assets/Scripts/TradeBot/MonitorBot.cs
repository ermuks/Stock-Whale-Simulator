using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모니터링 봇. 초기 20만돈, 종목당 5천 주.
/// 목표 매수가/매도가를 정해두고, 현재가가 근접하면 주문 실행.
/// 떨어지면 매수, 올라가면 매도, 급등하면 FOMO 매수, 수익률 마이너스면 물타기.
/// </summary>
public class MonitorBot : TradeBot
{
    [Header("모니터링 (간격 길수록 오더북 접근·프레임 부하 감소)")]
    [Tooltip("체크 간격 최소~최대(초). 풀/작전 봇이 많을 때는 4~12 권장")]
    public float checkIntervalMin = 4f;
    public float checkIntervalMax = 12f;
    [Header("변동 기준 (%)")]
    public decimal dropThresholdPct = 2m;
    public decimal riseThresholdPct = 2m;
    public decimal spikeThresholdPct = 5m;
    [Header("거래 규모 (비율)")]
    public decimal normalBuyCashRatio = 0.1m;
    public decimal normalSellHoldingRatio = 0.1m;
    public decimal fomoCashRatio = 0.4m;
    public decimal avgDownCashRatio = 0.2m;
    [Header("MonitorBot 목표가 설정")]
    [Tooltip("하락 매수 목표가: 기준가 대비 할인율 (0.02 = -2%)")]
    public float dropBuyTargetDiscount = 0.02f;
    [Tooltip("상승 매도 목표가: 기준가 대비 프리미엄 (0.02 = +2%)")]
    public float riseSellTargetPremium = 0.02f;
    [Tooltip("급등 FOMO 매수 목표가: 기준가 대비 프리미엄 (0.05 = +5%)")]
    public float spikeBuyTargetPremium = 0.05f;
    private Dictionary<Stock, decimal> _lastPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, float> _nextCheckTime = new Dictionary<Stock, float>();
    private Dictionary<Stock, decimal> _targetBuyPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _targetSellPrice = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _targetBuyVolume = new Dictionary<Stock, decimal>();
    private Dictionary<Stock, decimal> _targetSellVolume = new Dictionary<Stock, decimal>();
    public override void SuspendSession()
    {
        _targetBuyPrice.Clear();
        _targetSellPrice.Clear();
        _targetBuyVolume.Clear();
        _targetSellVolume.Clear();
        base.SuspendSession();
    }

    protected override void OnStockDelistedCleanup(Stock stock)
    {
        if (stock == null)
        { return; }

        _lastPrice.Remove(stock);
        _nextCheckTime.Remove(stock);
        _targetBuyPrice.Remove(stock);
        _targetSellPrice.Remove(stock);
        _targetBuyVolume.Remove(stock);
        _targetSellVolume.Remove(stock);
    }

    private bool _isInPool;
    private Stock _operationStock;
    private bool _isExitingOperation;
    public static System.Action<MonitorBot> OnReturnedToPool;
    [Tooltip("ThinkLoop 대기 구간. 모니터봇이 많을 때 5~15초로 두면 오더북 접근 빈도·프레임 부하 감소")]
    public float thinkDelayMin = 10f;
    public float thinkDelayMax = 25f;
    protected override float GetThinkDelayMin() => thinkDelayMin;
    protected override float GetThinkDelayMax() => thinkDelayMax;
    protected override Stock PickStock()
    {
        if (_isInPool)
        { return null; }

        if (_operationStock != null)
        { return _operationStock; }

        return base.PickStock();
    }

    public void SetAsPooled()
    {
        if (_operationStock != null)
        {
            _operationStock.orderBook.CancelAllOrdersByOwner(this, _operationStock);
            decimal remaining = GetHolding(_operationStock);
            if (remaining > 0m)
            {
                if (ExchangeWarehouse.Instance != null)
                {
                    ExchangeWarehouse.Instance.ReceiveShares(_operationStock, remaining);
                }

                ForceRemoveHolding(_operationStock, remaining);
            }
        }

        _isInPool = true;
        _operationStock = null;
        _isExitingOperation = false;
    }

    public void JoinOperation(Stock stock)
    {
        if (stock == null)
        { return; }

        _isInPool = false;
        _isExitingOperation = false;
        _operationStock = stock;
        InitializeForOperation(stock);
    }

    private void InitializeForOperation(Stock stock)
    {
        cash = Random.value < 0.5f ? 1_000_000m : 2_000_000m;
        if (ExchangeWarehouse.Instance != null)
        {
            ExchangeWarehouse.Instance.TransferTo(this, stock, System.Math.Min(5000m, ExchangeWarehouse.Instance.GetHolding(stock)), stock.currentPrice);
        }

        _lastPrice[stock] = stock.currentPrice;
        _nextCheckTime[stock] = SessionClock.Time + Random.Range(checkIntervalMin, checkIntervalMax);
    }

    public void ExitOperation()
    {
        if (_operationStock == null)
        { return; }

        _isExitingOperation = true;
        TrySellAllAndMaybeReturnToPool();
    }

    private void TrySellAllAndMaybeReturnToPool()
    {
        if (_operationStock == null)
        { return; }

        decimal hold = GetHolding(_operationStock);
        if (hold > 0m)
        {
            SellLimit(GetAggressiveLimitPrice(_operationStock, false), hold, 0, _operationStock);
        }

        if (GetHolding(_operationStock) <= 0m)
        {
            _operationStock = null;
            _isExitingOperation = false;
            _isInPool = true;
            OnReturnedToPool?.Invoke(this);
        }
    }

    protected override void Start()
    {
        base.Start();
        int tier = Random.Range(0, 10);
        if (tier < 5)
        {
            cash = (decimal)(Random.Range(12, 20) * 1_000_000);
        }
        else if (tier < 8)
        {
            cash = (decimal)(Random.Range(20, 28) * 1_000_000);
        }
        else
        {
            cash = (decimal)(Random.Range(28, 40) * 1_000_000);
        }
    }

    protected override void ThinkForStock(Stock stock)
    {
        if (_isExitingOperation && _operationStock != null && stock == _operationStock)
        {
            TrySellAllAndMaybeReturnToPool();
            return;
        }

        decimal current = stock.currentPrice;
        if (current <= 0m)
        { return; }

        if (_targetBuyPrice.TryGetValue(stock, out decimal targetBuy) && targetBuy > 0m)
        {
            decimal diff = System.Math.Abs(current - targetBuy) / targetBuy;
            if (diff >= (decimal)targetPriceCancelThreshold)
            {
                _targetBuyPrice[stock] = 0m;
                _targetBuyVolume[stock] = 0m;
            }
            else if (diff <= (decimal)targetPriceThreshold)
            {
                decimal vol = _targetBuyVolume.TryGetValue(stock, out decimal v) ? v : 0m;
                if (vol > 0m)
                {
                    DelayedBuyLimit(GetEffectiveBuyPrice(stock, targetBuy), vol, 0, stock);
                    _targetBuyPrice[stock] = 0m;
                    _targetBuyVolume[stock] = 0m;
                    _lastPrice[stock] = current;
                    ScheduleNext(stock);
                    return;
                }
            }
        }

        if (_targetSellPrice.TryGetValue(stock, out decimal targetSell) && targetSell > 0m)
        {
            decimal diff = System.Math.Abs(current - targetSell) / targetSell;
            if (diff >= (decimal)targetPriceCancelThreshold)
            {
                _targetSellPrice[stock] = 0m;
                _targetSellVolume[stock] = 0m;
            }
            else if (diff <= (decimal)targetPriceThreshold)
            {
                decimal vol = _targetSellVolume.TryGetValue(stock, out decimal v) ? v : 0m;
                if (vol > 0m)
                {
                    DelayedSellLimit(GetEffectiveSellPrice(stock, targetSell), vol, 0, stock);
                    _targetSellPrice[stock] = 0m;
                    _targetSellVolume[stock] = 0m;
                    _lastPrice[stock] = current;
                    ScheduleNext(stock);
                    return;
                }
            }
        }

        float now = SessionClock.Time;
        if (!_nextCheckTime.TryGetValue(stock, out float next) || now < next)
        {
            if (!_nextCheckTime.ContainsKey(stock))
            {
                _nextCheckTime[stock] = now + Random.Range(checkIntervalMin, checkIntervalMax);
            }

            return;
        }

        decimal refPrice = GetReferencePrice(stock);
        if (!_lastPrice.TryGetValue(stock, out decimal last))
        {
            _lastPrice[stock] = current;
            ScheduleNext(stock);
            return;
        }

        if (last <= 0m)
        {
            _lastPrice[stock] = current;
            ScheduleNext(stock);
            return;
        }

        decimal changePct = (current - last) / last * 100m;
        if (holdings.ContainsKey(stock))
        {
            decimal avg = avgPrice[stock];
            if (current < avg && cash > 0m)
            {
                decimal vol = ClampMinVolume(GetBuyVolume(avgDownCashRatio, stock, refPrice));
                if (vol > 0m)
                {
                    decimal buyTarget = PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)dropBuyTargetDiscount));
                    _targetBuyPrice[stock] = buyTarget;
                    _targetBuyVolume[stock] = vol;
                }

                _lastPrice[stock] = current;
                ScheduleNext(stock);
                return;
            }
        }

        if (changePct >= spikeThresholdPct && cash > 0m)
        {
            decimal vol = ClampMinVolume(GetBuyVolume(fomoCashRatio, stock, refPrice));
            if (vol > 0m)
            {
                DelayedBuyMarket(vol, stock);
            }

            _lastPrice[stock] = current;
            ScheduleNext(stock);
            return;
        }

        if (changePct >= riseThresholdPct)
        {
            decimal sellVol = ClampMinVolume(GetSellVolume(normalSellHoldingRatio, stock));
            if (sellVol > 0m)
            {
                decimal sellTarget = PriceTickPolicy.CeilToTick(refPrice * (1m + (decimal)riseSellTargetPremium));
                _targetSellPrice[stock] = sellTarget;
                _targetSellVolume[stock] = sellVol;
            }

            _lastPrice[stock] = current;
            ScheduleNext(stock);
            return;
        }

        if (changePct <= -dropThresholdPct)
        {
            decimal buyVol = ClampMinVolume(GetBuyVolume(normalBuyCashRatio, stock, refPrice));
            if (buyVol > 0m)
            {
                decimal buyTarget = PriceTickPolicy.FloorToTick(refPrice * (1m - (decimal)dropBuyTargetDiscount));
                _targetBuyPrice[stock] = buyTarget;
                _targetBuyVolume[stock] = buyVol;
            }

            _lastPrice[stock] = current;
            ScheduleNext(stock);
            return;
        }

        _lastPrice[stock] = current;
        ScheduleNext(stock);
    }

    void ScheduleNext(Stock stock)
    {
        _nextCheckTime[stock] = SessionClock.Time + Random.Range(checkIntervalMin, checkIntervalMax);
    }
}
