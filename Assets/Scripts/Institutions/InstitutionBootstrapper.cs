using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스프레드시트(이미지) 기반으로 종목별 기관 A/B/C 초기 현금·보유를 세팅.
/// - 주식 보유는 '주식가치'를 현재가로 나눠 정수 주식 수로 환산해 창고에서 이전.
/// - 창고 보유가 부족하면 가능한 만큼만 이전.
/// </summary>
public class InstitutionBootstrapper : MonoBehaviour
{
    [Tooltip("이미 세팅된 상태면 중복 생성 방지")]
    public bool skipIfAlreadyExists = true;
    [Tooltip("창고 초기화/거래소 봇 초기 배치 이후에 기관을 세팅하려면 약간 늦게 시작")]
    public float startDelaySeconds = 0.1f;
    [Tooltip("세팅된 기관 봇을 이 Transform 아래로 묶음 (비우면 루트)")]
    public Transform parent;
    [Header("프리셋 (이미지 기반)")]
    public List<InstitutionStockPreset> presets = new List<InstitutionStockPreset>();
    [Header("마켓메이커 (거래 발생용)")]
    [Tooltip("종목당 마켓메이커를 생성할지")]
    public bool spawnMarketMakers = true;
    [Tooltip("마켓메이커 1개당 초기 현금(돈)")]
    public decimal marketMakerCash = 2_000_000_000m;
    [Tooltip("마켓메이커 1개당 초기 주식 보유가치(돈). 현재가로 나눠 주식수로 환산")]
    public decimal marketMakerStockValue = 2_000_000_000m;
    [Serializable]
    public class InstitutionStockPreset
    {
        public string symbol;
        public InstitutionSidePreset leaderA = new InstitutionSidePreset();
        public InstitutionSidePreset followerB = new InstitutionSidePreset();
        public InstitutionSidePreset scalperC = new InstitutionSidePreset();
    }

    [Serializable]
    public class InstitutionSidePreset
    {
        public decimal cash;
        public decimal stockValue; // 현재가 기준 보유가치(돈)
    }

    void Reset()
    {
        if (presets != null && presets.Count > 0)
        { return; }

        presets = BuildDefaultPresetsFromImage();
    }

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

        if (skipIfAlreadyExists && FindObjectsOfType<InstitutionBot>(true).Length > 0)
        { yield break; }

        // 창고 준비 대기 (없어도 세팅은 하되, 창고가 있으면 반드시 TransferTo로 이동)
        float t0 = SessionClock.Time;
        while (ExchangeWarehouse.Instance == null && SessionClock.Time - t0 < 3f)
        {
            yield return null;
        }

        var stockBySymbol = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in StockManager.Instance.stocks)
        {
            if (s != null && !string.IsNullOrWhiteSpace(s.symbol))
            {
                stockBySymbol[s.symbol] = s;
            }
        }

        if (presets == null || presets.Count == 0)
        {
            presets = BuildDefaultPresetsFromImage();
        }

        foreach (var p in presets)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.symbol))
            { continue; }

            if (!stockBySymbol.TryGetValue(p.symbol.Trim(), out var stock) || stock == null)
            { continue; }

            if (spawnMarketMakers)
            {
                SpawnMarketMaker(stock);
            }

            SpawnAndSeed(stock, p.leaderA, InstitutionRole.LeaderA);
            SpawnAndSeed(stock, p.followerB, InstitutionRole.FollowerB);
            SpawnAndSeed(stock, p.scalperC, InstitutionRole.ScalperC);
        }
    }

    void SpawnMarketMaker(Stock stock)
    {
        var mm = TradeBot.Create<MarketMakerBot>($"MarketMaker_{stock.symbol}", parent);
        if (mm == null)
        { return; }

        mm.targetStock = stock;
        mm.cash = marketMakerCash;
        if (marketMakerStockValue > 0m && stock.currentPrice > 0m)
        {
            decimal shares = decimal.Floor(marketMakerStockValue / stock.currentPrice);
            if (shares > 0m)
            {
                TransferSharesFromWarehouseOrGrant(mm, stock, shares);
            }
        }
    }

    void SpawnAndSeed(Stock stock, InstitutionSidePreset preset, InstitutionRole role)
    {
        if (stock == null || preset == null)
        { return; }

        var bot = TradeBot.Create<InstitutionBot>($"Institution_{role}_{stock.symbol}", parent);
        if (bot == null)
        { return; }

        bot.targetStock = stock;
        bot.role = role;
        bot.cash = preset.cash;
        if (preset.stockValue > 0m && stock.currentPrice > 0m)
        {
            decimal shares = decimal.Floor(preset.stockValue / stock.currentPrice);
            if (shares > 0m)
            {
                TransferSharesFromWarehouseOrGrant(bot, stock, shares);
            }
        }
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

        // 발행량을 유지하기 위해 창고 외 강제 지급을 하지 않는다.
        Debug.LogWarning("초기 배분: 창고 부족으로 배분 생략");
    }

    static List<InstitutionStockPreset> BuildDefaultPresetsFromImage()
    {
        return new List<InstitutionStockPreset>
        {
            new InstitutionStockPreset
            {
                symbol = "NM",
                leaderA = new InstitutionSidePreset
                {
                    cash = 1_500_000_000m,
                    stockValue = 2_780_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 900_000_000m,
                    stockValue = 1_600_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 600_000_000m,
                    stockValue = 1_080_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "DSA",
                leaderA = new InstitutionSidePreset
                {
                    cash = 2_500_000_000m,
                    stockValue = 4_400_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 1_300_000_000m,
                    stockValue = 2_400_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 700_000_000m,
                    stockValue = 1_900_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "HBF",
                leaderA = new InstitutionSidePreset
                {
                    cash = 5_000_000_000m,
                    stockValue = 8_250_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 2_200_000_000m,
                    stockValue = 5_000_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 2_000_000_000m,
                    stockValue = 3_200_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "MBI",
                leaderA = new InstitutionSidePreset
                {
                    cash = 8_500_000_000m,
                    stockValue = 18_500_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 6_200_000_000m,
                    stockValue = 10_000_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 4_200_000_000m,
                    stockValue = 6_600_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "DDP",
                leaderA = new InstitutionSidePreset
                {
                    cash = 18_000_000_000m,
                    stockValue = 31_250_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 8_200_000_000m,
                    stockValue = 15_700_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 7_200_000_000m,
                    stockValue = 3_000_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "KMF",
                leaderA = new InstitutionSidePreset
                {
                    cash = 19_200_000_000m,
                    stockValue = 29_600_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 11_520_000_000m,
                    stockValue = 17_380_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 7_680_000_000m,
                    stockValue = 4_520_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "GFD",
                leaderA = new InstitutionSidePreset
                {
                    cash = 12_000_000_000m,
                    stockValue = 21_000_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 6_000_000_000m,
                    stockValue = 7_000_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 8_000_000_000m,
                    stockValue = 14_000_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "CBT",
                leaderA = new InstitutionSidePreset
                {
                    cash = 15_000_000_000m,
                    stockValue = 30_000_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 15_800_000_000m,
                    stockValue = 24_400_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 12_200_000_000m,
                    stockValue = 20_000_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "CED",
                leaderA = new InstitutionSidePreset
                {
                    cash = 18_720_000_000m,
                    stockValue = 34_000_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 3_213_000_000m,
                    stockValue = 13_000_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 11_490_000_000m,
                    stockValue = 45_900_000_000m
                },
            },
            new InstitutionStockPreset
            {
                symbol = "NXT",
                leaderA = new InstitutionSidePreset
                {
                    cash = 3_200_000_000m,
                    stockValue = 9_600_000_000m
                },
                followerB = new InstitutionSidePreset
                {
                    cash = 21_580_000_000m,
                    stockValue = 58_000_000_000m
                },
                scalperC = new InstitutionSidePreset
                {
                    cash = 11_320_000_000m,
                    stockValue = 40_000_000_000m
                },
            },
        };
    }
}
