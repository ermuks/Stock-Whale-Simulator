using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 봇 매니저: 시장 규모 기반 정밀 계산 (v5 - 밸런스 정합)
///
/// === 시장 규모 (StockManager 기본 10종목) ===
/// 종목당 발행 1천만주, 평균 시가 ~5,000원 → 종목당 시총 ~50B, 전체 ~500B
/// 거래소 창고: 전량 보유 후 고래 20% 이전 → 거래소 80% (≈400B 상당)
/// 거래소 초기 매도: 10~15% → 약 50~75B 상당 호가창 공급
/// 고래 보유: 20% ≈ 100B 상당
///
/// === 현금 유동성 ===
/// 일반 봇 초기 현금 합계: ~14.4B (GetBotInitialCash 기준)
/// 거래소 봇(ExchangeBot) 초기 현금: 25B (급락 방어·매수 지원)
/// → 총 유동 현금 ~39.4B (시총 500B 대비 ~8%)
///
/// === 초기 봇 수 (910명 + 고래 1 + 거래소 1) ===
/// FomoBot:    60명 × 24M = 1.44B  | HolderBot:  50명 × 40M = 2.00B  | StupidBot:  100명 × 12M = 1.20B
/// RandomBot:  400명 × 16M = 6.40B | ScalperBot: 90명 × 8M = 0.72B  | BookBot:    60명 × 12M = 0.72B
/// SpreadFiller: 30명 × 10M = 0.30B | SmartBot: 60명 × 16M = 0.96B | PanicBot: 40명 × 16M = 0.64B
/// MonitorBot: 20명 × 20M = 0.40B
/// ------------------------------------
/// 일반 봇 합계: 910명, ~14.38B
/// 팔로워 풀: 260명 × 평균 8M = 2.08B (고래 펌프 시 투입)
/// 목표 인구: 910명 (최소 600, 최대 1250)
/// </summary>
public class TradeBotManager : MonoBehaviour
{
    [Header("=== 시장가/공격적 매수 (매도벽 돌파) ===")]
    [Tooltip("FOMO: 상승 추격. 60명 × 24M = 1.44B")]
    public int fomoBotCount = 60;
    [Tooltip("Holder: 장기 보유. 50명 × 40M = 2.00B")]
    public int holderBotCount = 50;
    [Tooltip("Stupid: 충동 매수. 100명 × 12M = 1.20B")]
    public int stupidBotCount = 100;
    [Header("=== 유동성 제공 ===")]
    [Tooltip("Random: 다양한 주문. 400명 × 16M = 6.40B")]
    public int randomBotCount = 400;
    [Tooltip("Scalper: 초단타. 90명 × 8M = 0.72B")]
    public int scalperBotCount = 90;
    [Tooltip("Book: 호가 채움. 60명 × 12M = 0.72B")]
    public int bookBotCount = 60;
    [Tooltip("SpreadFiller: 스프레드. 30명 × 10M = 0.30B")]
    public int spreadFillerBotCount = 30;
    [Header("=== 전략적 매매 ===")]
    [Tooltip("Smart: 평단 기반. 60명 × 16M = 0.96B")]
    public int smartBotCount = 120;
    [Tooltip("Panic: 패닉셀. 40명 × 16M = 0.64B")]
    public int panicBotCount = 60;
    [Tooltip("Monitor: 급등락 대응. 20명 × 20M = 0.40B")]
    public int monitorBotCount = 20;
    [Header("=== 세력 ===")]
    public int whaleBotCount = 1;
    [Header("=== 고래 추종자 풀 ===")]
    [Tooltip("추종자 총 수. 260명 × 평균 8M = 2.08B")]
    public int followerPoolSize = 260;
    [Tooltip("1회 펌프 즉시 투입")]
    public int followersPerOperation = 100;
    [Tooltip("늦게 합류 (2차 파동)")]
    public int lateFollowerCount = 80;
    [Tooltip("늦게 합류 대기(초) 최소")]
    public float lateJoinDelayMin = 3f;
    [Tooltip("늦게 합류 대기(초) 최대")]
    public float lateJoinDelayMax = 12f;
    [Tooltip("고래 작전 관심도 보너스")]
    public float whaleOperationInterestBoost = 500f;
    [Header("거래소·고래 초기화")]
    [Tooltip("고래에게 종목당 지급할 비율 (발행 주식의 20% = 0.2). 거래소 창고에서 이전")]
    [Range(0.05f, 0.5f)]
    public float whaleInitialShareRatio = 0.2f;
    [Header("=== 인구 유동 (유입/이탈) ===")]
    [Tooltip("유입/이탈 체크 간격(초)")]
    public float populationCheckInterval = 15f;
    [Tooltip("1회 체크당 신규 유입 봇 수 (최소~최대)")]
    public int minNewBotsPerCheck = 1;
    public int maxNewBotsPerCheck = 4;
    [Tooltip("1회 체크당 이탈 봇 수 (최대)")]
    public int maxLeavingBotsPerCheck = 4;
    [Tooltip("봇 이탈 확률 (손실 중일 때)")]
    [Range(0f, 0.3f)]
    public float leaveChanceOnLoss = 0.08f;
    [Tooltip("봇 이탈 확률 (수익 중일 때 - 목돈 필요)")]
    [Range(0f, 0.1f)]
    public float leaveChanceOnProfit = 0.03f;
    [Tooltip("봇 이탈 확률 (보통 상태)")]
    [Range(0f, 0.05f)]
    public float leaveChanceNormal = 0.01f;
    [Tooltip("이탈한 봇 복귀 확률")]
    [Range(0f, 0.3f)]
    public float returnChance = 0.15f;
    [Tooltip("목표 봇 수 (이 수치 근처로 수렴)")]
    public int targetActiveBots = 990;
    [Tooltip("최소 봇 수 (이 이하로 줄지 않음)")]
    public int minActiveBots = 600;
    [Tooltip("최대 봇 수 (이 이상 늘지 않음)")]
    public int maxActiveBots = 1250;
    private List<TradeBot> bots = new();
    private List<TradeBot> _inactiveBots = new List<TradeBot>();
    private List<WhaleFollowerBot> _followerPool = new List<WhaleFollowerBot>();
    private List<WhaleFollowerBot> _activeFollowers = new List<WhaleFollowerBot>();
    private float _lastPopulationCheck;
    void Start()
    {
        bool configuredEconomy = StockManager.Instance != null && StockManager.Instance.useInitialEconomy;
        if (configuredEconomy)
        {
            StockManager.Instance.EnsureOwnerAccounts();
        }

        SpawnBots<FomoBot>(fomoBotCount);
        SpawnBots<HolderBot>(holderBotCount);
        SpawnBots<StupidBot>(stupidBotCount);
        SpawnBots<RandomBot>(randomBotCount);
        SpawnBots<ScalperBot>(scalperBotCount);
        SpawnBots<BookBot>(bookBotCount);
        SpawnBots<SpreadFillerBot>(spreadFillerBotCount);
        SpawnBots<SmartBot>(smartBotCount);
        SpawnBots<PanicBot>(panicBotCount);
        SpawnBots<MonitorBot>(monitorBotCount);
        if (!configuredEconomy)
        {
            SpawnBots<WhaleBot>(whaleBotCount);
        }

        // 씬의 단일 계좌를 먼저 재사용. 초기 현금은 ExchangeBot.InitializeAssets 한 곳에서 지급.
        SpawnBots<ExchangeBot>(1);
        // 거래소 창고: 전 종목 발행 수 보유 (씬에 ExchangeWarehouse 오브젝트 필요)
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        {
            Debug.LogError("[TradeBotManager] StockManager.Instance 또는 stocks가 없습니다. 스크립트 실행 순서를 확인하세요. (StockManager.Awake가 먼저 실행되어야 함)");
        }
        else if (ExchangeWarehouse.Instance == null)
        {
            Debug.LogWarning("[TradeBotManager] ExchangeWarehouse.Instance가 없습니다. 씬에 ExchangeWarehouse 컴포넌트를 가진 오브젝트를 추가하세요.");
        }
        else
        {
            if (!configuredEconomy)
            {
                ExchangeWarehouse.Instance.InitializeFromStocks(StockManager.Instance.stocks);
            }
        }

        // 고래만 주식 보유. 반드시 발행 주식의 20%(whaleInitialShareRatio)를 거래소 창고에서 이전
        if (!configuredEconomy && StockManager.Instance != null && StockManager.Instance.stocks != null)
        {
            decimal whaleRatio = (decimal)Mathf.Clamp01(whaleInitialShareRatio);
            if (whaleRatio <= 0m)
            {
                whaleRatio = 0.2m; // 기본 20% 고정
            }

            foreach (var bot in bots)
            {
                if (!(bot is WhaleBot))
                { continue; }

                foreach (var stock in StockManager.Instance.stocks)
                {
                    decimal whaleAmount = (stock.issuedShares * whaleRatio);
                    if (whaleAmount <= 0m)
                    { continue; }

                    if (ExchangeWarehouse.Instance != null)
                    {
                        ExchangeWarehouse.Instance.TransferTo(bot, stock, whaleAmount, stock.currentPrice);
                    }
                    else
                    {
                        Debug.LogWarning("고래 배분: 창고 없음");
                    }
                }

                break;
            }

            // 랜덤봇 초기 주식 배분: 전체 랜덤봇 중 40%에게만 발행량의 10%를 나눠 줌 (초기 매수세 완화)
            DistributeInitialSharesToRandomBots();
            // 거래소 봇 초기 매도: 반드시 같은 프레임에 실행. 한 프레임 늦으면 다른 봇이 시드 매도를 소진해 매도 호가가 비는 원인이 됨.
            ExchangeBot exchangeBot = null;
            foreach (var b in bots)
            {
                if (b is ExchangeBot ex)
                {
                    exchangeBot = ex;
                    break;
                }
            }

            if (exchangeBot != null)
            {
                exchangeBot.DoInitialPlacement();
            }
        }

        BuildFollowerPool();
        WhaleBot.OnWhaleOperationStarted += OnWhaleOperationStarted;
        WhaleBot.OnWhalePumpStarted += OnWhalePumpStarted;
        WhaleBot.OnWhaleOperationEnded += OnWhaleOperationEnded;
        WhaleFollowerBot.OnReturnedToPool += OnFollowerReturnedToPool;
        _lastPopulationCheck = SessionClock.Time;
        StartCoroutine(PopulationFlowLoop());
        StartCoroutine(OrderCleanupLoop());
    }

    void OnDestroy()
    {
        WhaleBot.OnWhaleOperationStarted -= OnWhaleOperationStarted;
        WhaleBot.OnWhalePumpStarted -= OnWhalePumpStarted;
        WhaleBot.OnWhaleOperationEnded -= OnWhaleOperationEnded;
        WhaleFollowerBot.OnReturnedToPool -= OnFollowerReturnedToPool;
    }

    IEnumerator PopulationFlowLoop()
    {
        yield return new WaitForSeconds(populationCheckInterval);
        while (true)
        {
            if (!MarketDayController.CanTrade)
            {
                yield return new WaitForSeconds(populationCheckInterval);
                continue;
            }

            ProcessBotLeaving();
            yield return null;
            if (MarketDayController.CanTrade)
            {
                ProcessBotReturning();
            }

            yield return null;
            if (MarketDayController.CanTrade)
            {
                ProcessNewBotEntry();
            }

            yield return null;
            if (MarketDayController.CanTrade)
            {
                ProcessCashInjection();
            }

            LogBotStatus();
            yield return new WaitForSeconds(populationCheckInterval);
        }
    }

    /// <summary>전체 호가창 정리 (5분마다 1회). 개별 봇이 아닌 중앙에서 실행.</summary>
    IEnumerator OrderCleanupLoop()
    {
        yield return new WaitForSeconds(300f);
        while (true)
        {
            if (StockManager.Instance != null)
            {
                var stocks = new List<Stock>(StockManager.Instance.stocks);
                foreach (var stock in stocks)
                {
                    if (stock != null && stock.currentPrice > 0m)
                    {
                        stock.orderBook.CleanupDistantOrders(stock.currentPrice, stock);
                    }

                    yield return null;
                }
            }

            yield return new WaitForSeconds(300f);
        }
    }

    void CleanupDistantOrders()
    {
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        { return; }

        int totalCancelled = 0;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || stock.currentPrice <= 0m)
            { continue; }

            totalCancelled += stock.orderBook.CleanupDistantOrders(stock.currentPrice, stock);
        }
    }

    void LogBotStatus()
    {
        if (bots.Count == 0)
        { return; }

        decimal totalCash = 0m;
        decimal totalHoldings = 0m;
        int lowCashCount = 0;
        int zeroCashCount = 0;
        int hasHoldingsCount = 0;
        foreach (var bot in bots)
        {
            if (bot is WhaleBot || bot is ExchangeBot)
            { continue; }

            totalCash += bot.cash;
            decimal minCash = GetBotInitialCash(bot) * 0.2m;
            if (bot.cash < minCash)
            {
                lowCashCount++;
            }

            if (bot.cash <= 0m)
            {
                zeroCashCount++;
            }

            if (StockManager.Instance != null && StockManager.Instance.stocks != null)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    decimal h = bot.GetHolding(stock);
                    if (h > 0m)
                    {
                        totalHoldings += h * stock.currentPrice;
                        hasHoldingsCount++;
                        break;
                    }
                }
            }
        }

        int normalBotCount = bots.Count - 2;
        decimal avgCash = normalBotCount > 0 ? totalCash / normalBotCount : 0m;
    }

    void ProcessCashInjection()
    {
        // Configured investors keep finite accounts. The legacy demo refill must not create recurring market demand.
        if (StockManager.Instance != null && StockManager.Instance.useInitialEconomy)
        { return; }
        int injectedCount = 0;
        foreach (var bot in bots)
        {
            if (bot is WhaleBot || bot is ExchangeBot)
            { continue; }

            decimal initialCash = GetBotInitialCash(bot);
            decimal minCash = initialCash * 0.2m;
            if (bot.cash < minCash)
            {
                decimal injection = initialCash * (decimal)Random.Range(0.5f, 1.0f);
                bot.cash += injection;
                injectedCount++;
            }
            else if (Random.value < 0.05f)
            {
                decimal bonus = initialCash * (decimal)Random.Range(0.1f, 0.3f);
                bot.cash += bonus;
            }
        }
    }

    private readonly List<TradeBot> _populationCandidates = new();
    void ProcessBotLeaving()
    {
        if (bots.Count <= minActiveBots)
        { return; }

        float leaveMultiplier = 1f;
        if (bots.Count > targetActiveBots)
        {
            leaveMultiplier = 1f + (bots.Count - targetActiveBots) / 500f;
        }
        else if (bots.Count < targetActiveBots)
        {
            leaveMultiplier = 0.5f;
        }

        int leftCount = 0;
        int maxLeave = Mathf.RoundToInt(maxLeavingBotsPerCheck * leaveMultiplier);
        var candidates = _populationCandidates;
        candidates.Clear();
        candidates.AddRange(bots);
        Shuffle(candidates);
        foreach (var bot in candidates)
        {
            if (leftCount >= maxLeave)
            { break; }

            if (bots.Count <= minActiveBots)
            { break; }

            if (bot is WhaleBot || bot is ExchangeBot)
            { continue; }

            decimal equity = GetBotEquity(bot);
            decimal initialCash = GetBotInitialCash(bot);
            bool isLosing = equity < initialCash * 0.85m;
            bool isProfiting = equity > initialCash * 1.3m;
            float leaveChance = isLosing ? leaveChanceOnLoss : (isProfiting ? leaveChanceOnProfit : leaveChanceNormal);
            leaveChance *= leaveMultiplier;
            if (Random.value < leaveChance)
            {
                BotLeave(bot);
                leftCount++;
            }
        }
    }

    void ProcessBotReturning()
    {
        if (_inactiveBots.Count == 0)
        { return; }

        if (bots.Count >= maxActiveBots)
        { return; }

        float returnMultiplier = 1f;
        if (bots.Count < targetActiveBots)
        {
            returnMultiplier = 1f + (targetActiveBots - bots.Count) / 300f;
        }
        else if (bots.Count > targetActiveBots)
        {
            returnMultiplier = 0.3f;
        }

        int returnCount = 0;
        var candidates = _populationCandidates;
        candidates.Clear();
        candidates.AddRange(_inactiveBots);
        Shuffle(candidates);
        foreach (var bot in candidates)
        {
            if (bots.Count >= maxActiveBots)
            { break; }

            float actualReturnChance = returnChance * returnMultiplier;
            if (Random.value < actualReturnChance)
            {
                BotReturn(bot);
                returnCount++;
            }
        }
    }

    void ProcessNewBotEntry()
    {
        if (bots.Count >= maxActiveBots)
        { return; }

        float entryMultiplier = 1f;
        if (bots.Count < targetActiveBots)
        {
            entryMultiplier = 1f + (targetActiveBots - bots.Count) / 400f;
        }
        else if (bots.Count > targetActiveBots)
        {
            entryMultiplier = 0.3f;
        }

        int baseNew = Random.Range(minNewBotsPerCheck, maxNewBotsPerCheck + 1);
        int newCount = Mathf.RoundToInt(baseNew * entryMultiplier);
        newCount = Mathf.Clamp(newCount, 0, maxActiveBots - bots.Count);
        for (int i = 0; i < newCount; i++)
        {
            SpawnRandomNewBot();
        }
    }

    void BotLeave(TradeBot bot)
    {
        bot.PrepareForDeparture();
        if (StockManager.Instance != null && StockManager.Instance.useInitialEconomy)
        {
            // Going offline cancels intentions, not ownership. A later return keeps the same portfolio and history.
            foreach (var stock in StockManager.Instance.stocks)
            { stock.orderBook.CancelAllOrdersByOwner(bot, stock); }
        }
        else { SellAllHoldings(bot); }
        bots.Remove(bot);
        _inactiveBots.Add(bot);
        bot.gameObject.SetActive(false);
    }

    void BotReturn(TradeBot bot)
    {
        _inactiveBots.Remove(bot);
        bots.Add(bot);
        bot.ResetStockPreferences();
        if (StockManager.Instance == null || !StockManager.Instance.useInitialEconomy)
        { ResetBotCash(bot); }
        bot.gameObject.SetActive(true);
    }

    void SpawnRandomNewBot()
    {
        if (_inactiveBots.Count > 0)
        {
            BotReturn(_inactiveBots[_inactiveBots.Count - 1]);
            return;
        }

        int roll = Random.Range(0, 100);
        TradeBot newBot;
        if (roll < 25)
        {
            newBot = CreateBot<StupidBot>();
        }
        else if (roll < 45)
        {
            newBot = CreateBot<FomoBot>();
        }
        else if (roll < 60)
        {
            newBot = CreateBot<RandomBot>();
        }
        else if (roll < 72)
        {
            newBot = CreateBot<PanicBot>();
        }
        else if (roll < 82)
        {
            newBot = CreateBot<ScalperBot>();
        }
        else if (roll < 90)
        {
            newBot = CreateBot<HolderBot>();
        }
        else if (roll < 95)
        {
            newBot = CreateBot<SmartBot>();
        }
        else
        {
            newBot = CreateBot<BookBot>();
        }

        if (newBot != null)
        {
            bots.Add(newBot);
        }
    }

    T CreateBot<T>()
        where T : TradeBot
    { return TradeBot.Create<T>(typeof(T).Name + "_New"); }

    void SellAllHoldings(TradeBot bot)
    {
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CancelAllOrdersByOwner(bot, stock);
            decimal holding = bot.GetHolding(stock);
            if (holding > 0m)
            {
                var bids = stock.orderBook.GetTopBids();
                decimal sellPrice = (bids.Count > 0 && bids[0].price > 0m) ? bids[0].price : PriceTickPolicy.FloorToTick(stock.currentPrice - PriceTickPolicy.GetTickSize(stock.currentPrice));
                if (sellPrice > 0m)
                {
                    bot.SellLimit(sellPrice, holding, 0, stock);
                }

                // Return any unfilled liquidation shares before transferring to warehouse.
                stock.orderBook.CancelAllOrdersByOwner(bot, stock);
                decimal remaining = bot.GetHolding(stock);
                if (remaining > 0m)
                {
                    if (ExchangeWarehouse.Instance != null)
                    {
                        ExchangeWarehouse.Instance.ReceiveShares(stock, remaining);
                    }

                    bot.ForceRemoveHolding(stock, remaining);
                }
            }
        }
    }

    void ResetBotCash(TradeBot bot)
    {
        int tier = Random.Range(0, 10);
        decimal newCash;
        if (tier < 5)
        {
            newCash = (decimal)(Random.Range(6, 16) * 1_000_000);
        }
        else if (tier < 8)
        {
            newCash = (decimal)(Random.Range(16, 30) * 1_000_000);
        }
        else
        {
            newCash = (decimal)(Random.Range(30, 60) * 1_000_000);
        }

        bot.cash = newCash;
    }

    decimal GetBotEquity(TradeBot bot)
    {
        // Resting sell orders remain the investor's assets and must not be treated as a loss.
        return bot.Equity();
    }

    /// <summary>봇 종류별 초기 현금. 합계 ~14.38B(910명). 밸런스: Design/BalanceNotes.md 참고.</summary>
    decimal GetBotInitialCash(TradeBot bot)
    {
        if (bot is FomoBot)
        { return 24_000_000m; }

        if (bot is HolderBot)
        { return 40_000_000m; }

        if (bot is StupidBot)
        { return 12_000_000m; }

        if (bot is RandomBot)
        { return 16_000_000m; }

        if (bot is ScalperBot)
        { return 8_000_000m; }

        if (bot is PanicBot)
        { return 16_000_000m; }

        if (bot is SmartBot)
        { return 16_000_000m; }

        if (bot is BookBot)
        { return 12_000_000m; }

        if (bot is SpreadFillerBot)
        { return 10_000_000m; }

        if (bot is MonitorBot)
        { return 20_000_000m; }

        return 10_000_000m;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>전체 랜덤봇 중 40% 확률로 선정된 봇에게만 종목당 발행량의 0.2%~0.5% 배분. 총량은 약 10% 수준. 초기 매수세 완화용.</summary>
    void DistributeInitialSharesToRandomBots()
    {
        if (StockManager.Instance == null || StockManager.Instance.stocks == null)
        { return; }

        if (ExchangeWarehouse.Instance == null)
        { return; }

        var randomBots = new List<TradeBot>();
        foreach (var b in bots)
        {
            if (b is RandomBot)
            {
                randomBots.Add(b);
            }
        }

        if (randomBots.Count == 0)
        { return; }

        foreach (var stock in StockManager.Instance.stocks)
        {
            decimal available = ExchangeWarehouse.Instance.GetHolding(stock);
            if (available <= 0m)
            { continue; }

            decimal capTotal = decimal.Floor(stock.issuedShares * 0.10m); // 종목당 최대 10%까지만 배분
            if (capTotal <= 0m)
            { continue; }

            decimal totalGiven = 0m;
            foreach (var bot in randomBots)
            {
                if (totalGiven >= capTotal)
                { break; }

                if (Random.value > 0.4f)
                {
                    continue; // 40% 확률로만 지급
                }

                float pct = Random.Range(0.002f, 0.005f); // 발행량의 0.2%~0.5%
                decimal amount = decimal.Floor(stock.issuedShares * (decimal)pct);
                if (amount <= 0m)
                { continue; }

                decimal have = ExchangeWarehouse.Instance.GetHolding(stock);
                if (have < amount)
                {
                    amount = decimal.Floor(have);
                }

                decimal remaining = capTotal - totalGiven;
                if (amount > remaining)
                {
                    amount = remaining;
                }

                if (amount <= 0m)
                { continue; }

                ExchangeWarehouse.Instance.TransferTo(bot, stock, amount, stock.currentPrice);
                totalGiven += amount;
            }
        }
    }

    void BuildFollowerPool()
    {
        for (int i = 0; i < followerPoolSize; i++)
        {
            var bot = TradeBot.Create<WhaleFollowerBot>($"WhaleFollowerBot_Pool_{i}");
            if (bot == null)
            { break; }

            bot.SetAsPooled();
            _followerPool.Add(bot);
        }
    }

    void OnWhaleOperationStarted(WhaleBot whale, Stock stock, float interestBonus)
    {
        if (stock == null || StockInterestManager.Instance == null)
        { return; }

        StockInterestManager.Instance.BoostInterestForWhaleOperation(stock, interestBonus);
    }

    void OnWhalePumpStarted(WhaleBot whale, Stock stock, decimal targetPrice)
    {
        if (stock == null)
        { return; }

        int want = Mathf.Max(0, followersPerOperation);
        int take = Mathf.Min(want, _followerPool.Count);
        for (int i = 0; i < take; i++)
        {
            var bot = _followerPool[0];
            _followerPool.RemoveAt(0);
            if (!bot.TryAssignCommander(whale))
            {
                _followerPool.Add(bot);
                continue;
            }

            if (!whale.followers.Contains(bot))
            {
                whale.followers.Add(bot);
            }

            bot.JoinPump(stock, targetPrice);
            _activeFollowers.Add(bot);
        }

        Debug.Log($"[세력] 펌프 투입 — {stock.stockName}({stock.symbol}) 팔로워 {take}명 목표가 {targetPrice:N0}, 2차 합류 예정 {lateFollowerCount}명");
        if (lateFollowerCount > 0)
        {
            StartCoroutine(LateJoinFollowers(whale, stock, targetPrice));
        }
    }

    IEnumerator LateJoinFollowers(WhaleBot whale, Stock stock, decimal targetPrice)
    {
        float delay = Mathf.Clamp(Random.Range(lateJoinDelayMin, lateJoinDelayMax), 0.1f, 300f);
        yield return new WaitForSeconds(delay);
        if (stock == null || !stock.AcceptsOrders || whale == null || whale.PreDisposal || !MarketDayController.CanTrade)
        { yield break; }

        int take = Mathf.Min(Mathf.Max(0, lateFollowerCount), _followerPool.Count);
        for (int i = 0; i < take; i++)
        {
            var bot = _followerPool[0];
            _followerPool.RemoveAt(0);
            if (!bot.TryAssignCommander(whale))
            {
                _followerPool.Add(bot);
                continue;
            }

            if (!whale.followers.Contains(bot))
            {
                whale.followers.Add(bot);
            }

            bot.JoinPump(stock, targetPrice);
            _activeFollowers.Add(bot);
            if (stock.currentPrice >= targetPrice)
            {
                bot.CommandSell();
            }
        }
    }

    void OnWhaleOperationEnded(WhaleBot whale, Stock stock)
    {
        int returned = 0;
        foreach (var bot in new List<WhaleFollowerBot>(_activeFollowers))
        {
            if (bot != null && bot.Commander == whale && bot.operationStock == stock)
            {
                bot.ExitAndReturnToPool();
                returned++;
            }
        }

        if (stock != null && returned > 0)
        {
            Debug.Log($"[세력] 작전 종료 — {stock.stockName}({stock.symbol}) 팔로워 {returned}명 풀 반환");
        }
    }

    void OnFollowerReturnedToPool(WhaleFollowerBot bot)
    {
        if (bot == null)
        { return; }

        _activeFollowers.Remove(bot);
        if (!_followerPool.Contains(bot))
        {
            _followerPool.Add(bot);
        }
    }

    void SpawnBots<T>(int count)
        where T : TradeBot
    {
        for (int i = 0; i < count; i++)
        {
            T bot = TradeBot.Create<T>(typeof(T).Name);
            if (bot == null)
            { break; }

            bots.Add(bot);
        }
    }
}
