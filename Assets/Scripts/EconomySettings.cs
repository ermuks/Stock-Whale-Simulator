using System;
using UnityEngine;

[Serializable]
public class EconomySettings
{
    public int seed = 38173;
    public bool retailPlayerStart = true;
    public long retailStartingCash = 50_000_000L;
    public long playerTargetMarketCap = 10_000_000_000L, playerBaseCash = 500_000_000L;
    public float initialPriceUnit = 1f;
    public long minimumShares = 1_000_000L, maximumSharesExclusive = 1_000_000_000L;
    public int minimumNameLength = 2, maximumNameLength = 20;
    [Range(10, 90)]
    public int defaultPublicPercentage = 60;
    public bool marketCapExchangeCash = true;
    public float exchangeCashRatio = 0.2f;
    public long directExchangeCash = 59_600_000_000L;
    [Range(0f, 1f)]
    public float publicDistributionRatio = 0.5f, participationProbability = 0.4f;
    public float allocationWeightMin = 0.5f, allocationWeightMax = 1.5f, initialBasisMin = 0.8f, initialBasisMax = 1.2f;
    public float smallCashProbability = 0.7f, mediumCashProbability = 0.25f;
    public long smallCashMin = 1_000_000L, smallCashMax = 10_000_000L;
    public long mediumCashMin = 10_000_000L, mediumCashMax = 50_000_000L;
    public long largeCashMin = 50_000_000L, largeCashMax = 200_000_000L;
    public int futureCount = 10, futureLimit = 128, futureWorkPerFrame = 8;
    public float futureHorizonSeconds = 600f, minimumStaySeconds = 1f, firstNewsMin = 3f, firstNewsMax = 10f;
    public int delistingCompletedCandles = 10;
    public float ownerNoticeSeconds = 600f, disposalInterval = 10f, staleSellSeconds = 30f;
    public float disposalChunkRatio = 0.02f, disposalOutstandingRatio = 0.1f, disposalLimitRatio = 1f;
    public float gradeInterval = 60f, warningEnter = 0.3f, dangerEnter = 0.45f, highEnter = 0.6f, haltEnter = 0.8f;
    public float warningExit = 0.2f, dangerExit = 0.35f, highExit = 0.5f, haltSeconds = 60f;
    public int stableCandles = 5, haltGraceCandles = 5, stocksPerFrame = 1, cancelOrdersPerFrame = 128;
    public float reactionDelayMax = 30f, maximumSelectionWeight = 1_000_000f, maximumAttentionFactor = 100f;
    public float[] cautiousWeights =
    {
        1f,
        0.9f,
        0.6f,
        0.3f
    };
    public float[] neutralWeights =
    {
        1f,
        1.1f,
        1.2f,
        1.3f
    };
    public float[] speculativeWeights =
    {
        1f,
        1.3f,
        1.8f,
        2.5f
    };
    public Color safeColor = Color.green, warningColor = Color.yellow, dangerColor = new(1f, 0.5f, 0f), highColor = Color.red, haltedColor = Color.gray;
    public OwnerCompanyDefinition[] companies = Defaults();
    public static OwnerCompanyDefinition[] Defaults()
    {
        string[] symbols =
        {
            "PLAYER",
            "NM",
            "DSA",
            "HBF",
            "MBI",
            "DDP",
            "KMF",
            "GFD",
            "CBT",
            "CED",
            "NXT"
        };
        string[] names =
        {
            "플레이어 기업",
            "노보손해보험",
            "덕상가주에이아이",
            "두덕리식품",
            "미래바이오",
            "대만디스플레이",
            "국민금융",
            "그린푸드",
            "코어바이오",
            "코어전자",
            "넥스트테크"
        };
        long[] prices =
        {
            1000,
            500,
            1000,
            2000,
            3000,
            5000,
            7500,
            10000,
            1500,
            4000,
            8000
        };
        long[] shares =
        {
            10000000,
            10000000,
            8000000,
            10000000,
            10000000,
            10000000,
            10000000,
            10000000,
            10000000,
            10000000,
            10000000
        };
        float[] ratios =
        {
            0.4f,
            0.4f,
            0.5f,
            0.6f,
            0.65f,
            0.75f,
            0.8f,
            0.9f,
            0.55f,
            0.7f,
            0.85f
        };
        long[] cash =
        {
            500000000,
            200000000,
            400000000,
            1000000000,
            1500000000,
            2500000000,
            3500000000,
            5000000000,
            700000000,
            2000000000,
            4000000000
        };
        var result = new OwnerCompanyDefinition[11];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new OwnerCompanyDefinition
            {
                stockId = symbols[i],
                ownerId = i == 0 ? "PLAYER_OWNER" : "OWNER_" + (char)('A' + i - 1),
                name = names[i],
                startPrice = prices[i],
                issuedShares = shares[i],
                ownerRatio = ratios[i],
                initialCash = cash[i],
                initiallyListed = i < 8
            };
        }

        return result;
    }
}

[Serializable]
public class OwnerCompanyDefinition
{
    public string stockId, ownerId, name;
    public long startPrice, issuedShares, initialCash;
    public float ownerRatio;
    public bool initiallyListed;
}
