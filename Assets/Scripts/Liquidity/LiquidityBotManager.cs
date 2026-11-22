using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TradeBotManager 없이도 시장 흐름을 만들기 위한 유동 봇 스폰/초기 자산 세팅.
/// - Firefly(모멘텀), Random, InternalValue, FakeValue
/// - 내부 가치/뉴스 시스템은 Stock.realValue 갱신을 그대로 사용 (override하지 않음)
/// </summary>
public class LiquidityBotManager : MonoBehaviour
{
    [Header("생성 개수")]
    public int fireflyCount = 30;
    public int randomCount = 60;
    public int internalValueCount = 60;
    public int fakeValueCount = 60;
    [Header("초기 현금 (돈)")]
    public decimal fireflyCash = 12_000_000m;
    public decimal randomCash = 10_000_000m;
    public decimal internalValueCash = 12_000_000m;
    public decimal fakeValueCash = 12_000_000m;
    [Header("초기 주식 보유 (시장에 매도도 생기게)")]
    [Tooltip("봇 1명당 초기 주식 보유가치(돈). 현재가로 나눠 주식 수로 환산해 창고에서 이전")]
    public decimal initialStockValuePerBot = 8_000_000m;
    [Tooltip("초기 보유를 줄 종목 선택 방식: true면 랜덤, false면 CurrentStock")]
    public bool randomizeInitialStock = true;
    [Header("정리")]
    [Tooltip("봇을 이 Transform 아래로 묶음")]
    public Transform parent;
    [Tooltip("이미 생성된 유동 봇이 있으면 중복 생성 방지")]
    public bool skipIfAlreadyExists = true;
    public float startDelaySeconds = 0.1f;
    IEnumerator Start()
    {
        MarketDayController.Initializers++;
        try
        {
            yield return Setup();
        }
        finally
        {
            MarketDayController.Initializers--;
        }
    }

    IEnumerator Setup()
    {
        if (startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        if (StockManager.Instance == null || StockManager.Instance.stocks == null || StockManager.Instance.stocks.Count == 0)
        { yield break; }

        if (skipIfAlreadyExists)
        {
            if (FindObjectsOfType<LiquidityBotBase>(true).Length > 0)
            { yield break; }
        }

        float t0 = SessionClock.Time;
        while (ExchangeWarehouse.Instance == null && SessionClock.Time - t0 < 3f)
        {
            yield return null;
        }

        SpawnMany<FireflyBot>(fireflyCount, fireflyCash);
        SpawnMany<RandomLiquidityBot>(randomCount, randomCash);
        SpawnMany<InternalValueBot>(internalValueCount, internalValueCash);
        SpawnMany<FakeValueBot>(fakeValueCount, fakeValueCash);
    }

    void SpawnMany<T>(int count, decimal cash)
        where T : LiquidityBotBase
    {
        count = Mathf.Max(0, count);
        for (int i = 0; i < count; i++)
        {
            var bot = TradeBot.Create<T>($"{typeof(T).Name}_{i}", parent);
            if (bot == null)
            { break; }

            bot.cash = cash;
            // 초기 보유(매도도 나오게): 창고에서 1종목만 소량 이전
            if (initialStockValuePerBot > 0m)
            {
                var stock = PickInitialStock();
                if (stock != null && stock.currentPrice > 0m)
                {
                    decimal shares = decimal.Floor(initialStockValuePerBot / stock.currentPrice);
                    if (shares > 0m)
                    {
                        TransferSharesFromWarehouseOrGrant(bot, stock, shares);
                    }
                }
            }
        }
    }

    Stock PickInitialStock()
    {
        var list = StockManager.Instance.stocks;
        if (list == null || list.Count == 0)
        { return null; }

        if (!randomizeInitialStock)
        { return StockManager.Instance.CurrentStock != null ? StockManager.Instance.CurrentStock : list[0]; }

        return list[Random.Range(0, list.Count)];
    }

    void TransferSharesFromWarehouseOrGrant(Trader trader, Stock stock, decimal shares)
    {
        shares = decimal.Floor(shares);
        if (StockManager.Instance != null && StockManager.Instance.useInitialEconomy)
        { return; }

        if (trader == null || stock == null || shares <= 0m)
        { return; }

        if (ExchangeWarehouse.Instance != null)
        {
            decimal have = ExchangeWarehouse.Instance.GetHolding(stock);
            decimal take = have > 0m ? (shares > have ? have : shares) : 0m;
            if (take > 0m)
            {
                ExchangeWarehouse.Instance.TransferTo(trader, stock, take, stock.currentPrice);
                return;
            }
        }

        Debug.LogWarning("초기 배분: 창고 부족으로 배분 생략");
    }
}
