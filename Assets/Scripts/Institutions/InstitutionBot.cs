using UnityEngine;

public enum InstitutionRole
{
    LeaderA,
    FollowerB,
    ScalperC
}

/// <summary>
/// 기관 봇(종목당 A/B/C). 초기 자산은 InstitutionBootstrapper에서 세팅.
/// 세부 매매 로직은 이후 단계에서 각 역할별로 확장.
/// </summary>
public class InstitutionBot : TradeBot
{
    [Header("기관 설정")]
    public Stock targetStock;
    public InstitutionRole role;

    protected override float GetThinkDelayMin() => 0.5f;
    protected override float GetThinkDelayMax() => 0.5f;

    protected override Stock PickStock()
    {
        return targetStock != null ? targetStock : base.PickStock();
    }

    protected override void ThinkForStock(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m) return;
        if (CandleHistoryManager.Instance == null) return;

        if (!CandleHistoryManager.Instance.TryGetRecentCloseAndVolume(stock, out var pPrev, out var pCurr, out var volPrev, out var volCurr))
            return;
        if (pPrev <= 0m) return;

        decimal dP = (pCurr - pPrev) / pPrev;
        decimal dVol = volCurr / (volPrev <= 0m ? 1m : volPrev);

        decimal vReal = stock.realValue;
        decimal vFake = stock.fakeValue;
        decimal p = stock.currentPrice;
        decimal dReal = vReal > 0m ? (p - vReal) / vReal : 0m;
        decimal dFake = vFake > 0m ? (p - vFake) / vFake : 0m;

        var book = stock.orderBook;
        var asks = book != null ? book.GetTopAsks(1) : null;
        var bids = book != null ? book.GetTopBids() : null;
        decimal bestAsk = asks != null && asks.Count > 0 ? asks[0].price : 0m;
        decimal bestBid = bids != null && bids.Count > 0 ? bids[0].price : 0m;

        switch (role)
        {
            case InstitutionRole.LeaderA:
                // 1) Accumulation: D_real < -0.1 → 공격적 매수(최우선 매도 체결)
                if (dReal < -0.10m && bestAsk > 0m)
                {
                    decimal budget = (cash - reservedCash) * 0.20m;
                    decimal vol = decimal.Floor(budget / bestAsk);
                    vol = ClampMinVolume(vol);
                    if (vol > 0m)
                    {
                        // 분할 진입
                        decimal v1 = decimal.Floor(vol * 0.3m);
                        decimal v2 = decimal.Floor(vol * 0.3m);
                        decimal v3 = vol - v1 - v2;
                        if (v1 > 0m) BuyLimit(bestAsk, v1, 0, stock);
                        if (v2 > 0m) BuyLimit(bestAsk, v2, 0, stock);
                        if (v3 > 0m) BuyLimit(bestAsk, v3, 0, stock);
                    }
                }
                // 3) Distribution: D_real > +0.15 && 볼륨 약화 → 점진 매도(최우선 매수 체결)
                else if (dReal > 0.15m && dVol < 1.0m && bestBid > 0m)
                {
                    decimal have = GetHolding(stock);
                    decimal vol = decimal.Floor(have * 0.15m);
                    vol = ClampMinVolume(vol);
                    if (vol > 0m)
                        SellLimit(bestBid, vol, 0, stock);
                }
                // 4) Defensive Buy: 급락인데 D_real < 0 → 지지 매수벽(근접 매수)
                else if (dP < -0.02m && dReal < 0m)
                {
                    decimal near = PriceTickPolicy.OneTickBelow(p);
                    if (near > 0m)
                    {
                        decimal budget = (cash - reservedCash) * 0.06m;
                        decimal vol = decimal.Floor(budget / near);
                        vol = ClampMinVolume(vol);
                        if (vol > 0m) BuyLimit(near, vol, 0, stock);
                    }
                }
                break;

            case InstitutionRole.FollowerB:
                // 1) Trend Follow: ΔP>0 && ΔVol 증가 → 지연 매수(근접 매수)
                if (dP > 0m && dVol > 1.05m)
                {
                    decimal price = bestAsk > 0m ? bestAsk : PriceTickPolicy.OneTickAbove(p);
                    decimal budget = (cash - reservedCash) * 0.08m;
                    decimal vol = decimal.Floor(budget / price);
                    vol = ClampMinVolume(vol);
                    if (vol > 0m) BuyLimit(price, vol, 0, stock);
                }
                // 2) Exit: ΔP 음전환 또는 볼륨 약화 → 빠른 매도
                else if (dP < 0m || dVol < 0.95m)
                {
                    if (bestBid > 0m)
                    {
                        decimal have = GetHolding(stock);
                        decimal vol = decimal.Floor(have * 0.25m);
                        vol = ClampMinVolume(vol);
                        if (vol > 0m) SellLimit(bestBid, vol, 0, stock);
                    }
                }
                break;

            case InstitutionRole.ScalperC:
                // 변동성 기반 소액 진입/청산 (스프레드/변동만)
                decimal absDp = dP < 0m ? -dP : dP;
                if (absDp > 0.01m)
                {
                    // 양방향 랜덤 소액
                    bool buy = Random.value < 0.5f;
                    if (buy)
                    {
                        decimal price = bestAsk > 0m ? bestAsk : PriceTickPolicy.OneTickAbove(p);
                        decimal budget = (cash - reservedCash) * 0.03m;
                        decimal vol = decimal.Floor(budget / price);
                        if (vol > 2_000m) vol = 2_000m;
                        vol = ClampMinVolume(vol);
                        if (vol > 0m) BuyLimit(price, vol, 0, stock);
                    }
                    else
                    {
                        decimal have = GetHolding(stock);
                        decimal vol = decimal.Floor(have * 0.05m);
                        if (vol > 2_000m) vol = 2_000m;
                        vol = ClampMinVolume(vol);
                        decimal price = bestBid > 0m ? bestBid : PriceTickPolicy.OneTickBelow(p);
                        if (vol > 0m && price > 0m) SellLimit(price, vol, 0, stock);
                    }
                }
                break;
        }
    }
}

