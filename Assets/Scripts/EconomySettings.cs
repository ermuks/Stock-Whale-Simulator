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
    /// <summary>거래소 초기 현금을 잴 때 한 종목의 시가총액을 이만큼까지만 센다. 대형주 몇 개가 거래소 현금을 수십 배로 불리지 않게 한다.</summary>
    public long exchangeCashCapPerStock = 100_000_000_000L;
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
    /// <summary>
    /// 개미의 처음 현금. 이 값들은 씬에 저장돼 있어서 위의 기본값을 고쳐도 반영되지 않는다. 그래서 게임을 시작할 때 여기서 덮어쓴다.
    /// 예전의 쉰 배다: 소액 5천만~5억(70%), 중간 5억~25억(25%), 큰손 25억~100억(5%). 개미 현금 합계가 시가총액(약 6조)의 5분의 1쯤 되게 잡았다.
    /// 개미가 가난하면 세력도 플레이어도 벌 돈이 없다.
    /// </summary>
    public void ApplyRetailWealth()
    {
        smallCashMin = 50_000_000L; smallCashMax = 500_000_000L;
        mediumCashMin = 500_000_000L; mediumCashMax = 2_500_000_000L;
        largeCashMin = 2_500_000_000L; largeCashMax = 10_000_000_000L;
    }
    /// <summary>
    /// 종목 목록은 씬에 저장돼 있어서 코드의 기본 목록에 종목을 더해도 반영되지 않는다.
    /// 저장된 목록에 없는 기본 종목을 뒤에 붙인다. 이미 있는 종목의 값은 건드리지 않는다.
    /// </summary>
    public void IncludeNewDefaults()
    {
        var merged = new System.Collections.Generic.List<OwnerCompanyDefinition>(companies ?? Array.Empty<OwnerCompanyDefinition>());
        foreach (var candidate in Defaults())
        {
            bool known = false;
            foreach (var existing in merged)
            {
                if (existing != null && (existing.stockId == candidate.stockId || existing.ownerId == candidate.ownerId)) { known = true; break; }
            }

            if (!known) { merged.Add(candidate); }
        }

        companies = merged.ToArray();
    }
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
            "NXT",
            "SSE",
            "SQH",
            "TZM",
            "AMV"
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
            "넥스트테크",
            "산삼전자",
            "스퀘어하이테크",
            "테줌마",
            "AMDIVIA"
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
            8000,
            200000,
            1200000,
            250000,
            750000
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
            10000000,
            // 대형주 넷의 시가총액: 2조, 1.5조, 1조, 1.2조. 전 종목이 50억~2조 안에 든다.
            10000000,
            1250000,
            4000000,
            1600000
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
            0.85f,
            // 대형주는 대주주 몫이 거의 전부다. 시장에 도는 물량은 200억·180억·150억·156억 원어치로,
            // 기존 종목에서 가장 큰 유통 물량(150억)보다 조금 큰 정도다.
            // 유통 물량까지 시가총액만큼 키우면 처음에 주식을 나눠 받는 개미의 재산이 수십 배로 불어난다.
            0.99f,
            0.988f,
            0.985f,
            0.987f
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
            4000000000,
            3500000000,
            3500000000,
            3000000000,
            2500000000
        };
        // 처음부터 상장돼 있는 종목. 나머지는 나중에 상장한다.
        var unlisted = new System.Collections.Generic.HashSet<string> { "CBT", "CED", "NXT" };
        var result = new OwnerCompanyDefinition[symbols.Length];
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
                initiallyListed = !unlisted.Contains(symbols[i])
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
