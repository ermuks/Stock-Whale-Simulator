using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public enum MarketPhase
{
    PreOpen,
    Trading,
    Closing,
    PostClose
}

/// <summary>한 게임 시계와 결산, 기존 봇의 사회 상태를 관리. 판단/발언 간격은 현실 초.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class MarketDayController : MonoBehaviour
{
    public static MarketDayController Instance { get; private set; }
    public static bool CanTrade => Time.timeScale > 0f && (StockManager.Instance == null || StockManager.Instance.SetupConfirmed) && (Instance == null || Instance.Phase == MarketPhase.Trading);
    public static int Session => Instance == null ? 0 : Instance.SessionId;

    public static int Initializers;
    [Header("하루 (현실 초)")]
    [Min(0f)]
    public float preOpenSeconds = 0f;
    [Min(0.1f)]
    public float tradingSeconds = 900f, postCloseSeconds = 180f;
    public int seed = 12345;
    [Header("처리 예산 / 명시적 상한")]
    [Range(1, 2000)]
    public int maxBots = 2000;
    [Min(1)]
    public int botsPerFrame = 32, candidatesPerFrame = 8, closeOrdersPerFrame = 128;
    [Range(16, 1024)]
    public int messageLimit = 256;
    public const int EventLimit = 256;
    public const int PositionMemoryLimit = 32;
    [Header("채팅 (현실 초)")]
    [Min(0.1f)]
    public float publishMin = 3f, publishMax = 8f, botCooldown = 30f;
    [Range(0f, 1f)]
    public float speechChance = 0.45f;
    [Header("채팅 종목 집중 — 실제 체결 기준")]
    [Range(0f,1f)] public float marketConversationChance = .85f, topicExplorationShare = .18f;
    [Min(.25f)] public float topicRefreshSeconds = 2f;
    public MarketConversationFocus ConversationFocus { get; } = new();
    [Min(1f)]
    public float eventLifetime = 45f, recentTradeLifetime = 180f, sameEventCooldown = 60f, sentenceCooldown = 120f;
    [Header("스트레스 — 테스트 기본값, 게임 거래 시간에만 적분")]
    [Min(0f)]
    public float lossPerGameHour = 1f, severityPerGameHour = 20f;
    [Min(0f)]
    public float recoveryDrop = 8f, breakEvenDrop = 6f, realizedScale = 20f;
    [Range(0.001f, 1f)]
    public float surgeRatio = 0.05f, recoveryRatio = 0.05f, afterSellRiseRatio = 0.03f;
    [Min(0.1f)]
    public float stressSampleSeconds = 1f, realizedAggregationSeconds = 5f, stressDropThreshold = 5f;
    public int Day { get; private set; } = 1;
    public MarketPhase Phase { get; private set; } = MarketPhase.PreOpen;
    public int SessionId { get; private set; }
    public float RemainingSeconds { get; private set; }
    public double TradingElapsed { get; private set; }
    public bool Ready { get; private set; }
    public bool IntegrityFailed { get; private set; }
    public bool InitialDistributionComplete { get; private set; }
    public bool ReportPending { get; private set; }
    public bool CheckpointReady => Ready && Phase == MarketPhase.PostClose && ReportPending && elapsed == 0f && phaseWork == null;

    public readonly Dictionary<Stock, decimal> ClosingPrices = new();
    public readonly List<TradeBot> Bots = new(2000);
    private readonly Dictionary<int, TradeBot> byId = new();
    private readonly Dictionary<Stock, List<TradeBot>> holders = new();
    private readonly Dictionary<Stock, Dictionary<TradeBot, int>> holderIndices = new();
    private readonly Dictionary<Stock, List<TradeBot>> recentSellers = new();
    private readonly Dictionary<Stock, decimal> eventPrices = new();
    private readonly Dictionary<string, float> recentSentences = new();
    private readonly Queue<(string text, float time)> sentenceExpiry = new();
    private readonly List<MarketEvent> events = new(EventLimit);
    private readonly Dictionary<(ChatEvent kind, Stock stock, TradeBot bot), int> eventIndices = new(EventLimit);
    private readonly Queue<(ChatEvent kind, Stock stock, TradeBot bot, long ticket)> eventOrder = new(EventLimit * 2);
    private long eventTicket;
    private readonly Dictionary<Stock,int> conversationEventCounts = new();
    private int generalConversationEvents;
    private BotChatMessage[] messages;
    private int messageStart, messageCount, stressCursor, nextId;
    private float elapsed, nextPublish, nextConversation, lastBotMessageTime;
    private BotChatMessage replySource;
    private long lastRepliedSequence;
    private long sequence;
    private Coroutine phaseWork;
    private System.Random random;
    private readonly List<Stock> subscribed = new();
    private readonly List<Stock> closingStocks = new();
    private readonly List<Trader> closingTraders = new(2001);
    private static readonly ProfilerMarker StressMarker = new("DonDon.Stress"), ChatMarker = new("DonDon.Chat"), CloseMarker = new("DonDon.Close");
    private struct MarketEvent
    {
        public ChatEvent Kind;
        public Stock Stock;
        public TradeBot Bot;
        public float Time;
        public int Cursor, Candidates;
        public decimal ReferencePrice;
        public long Ticket;
    }

    public int MessageCount => messageCount;
    public long MessageVersion => sequence;
    public int PendingEvents => events.Count;

    // Overnight minutes deliberately extend past 1440, keeping social/event time monotonic.
    public int GameMinute => (int)ExactGameMinute;
    public int ClockMinute => GameMinute % 1440;
    public int CalendarDay => Day + GameMinute / 1440;
    public double AbsoluteGameMinute => (Day - 1) * 1440d + ExactGameMinute;
    public double ExactGameMinute
    {
        get
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, Duration));
            // 첫날은 08:30에 시작해 30분 동안 둘러본다. 둘째 날부터는 일어난 08:00부터다.
            if (Phase == MarketPhase.PreOpen)
            { return Day == 1 ? 8 * 60 + 30 + 30d * progress : 8 * 60 + 60d * progress; }

            if (Phase == MarketPhase.Trading)
            { return 9 * 60 + 480d * progress; }

            return Phase == MarketPhase.Closing ? 1020d : 1020d + 900d * progress;
        }
    }

    private float Duration => Phase == MarketPhase.PreOpen ? (Day == 1 ? Mathf.Max(preOpenSeconds, tradingSeconds / 16f) : tradingSeconds / 8f) :
        Phase == MarketPhase.Trading ? tradingSeconds : tradingSeconds * 15f / 8f;

    public void AcknowledgeReport() { ReportPending = false; }
    public void PrepareSessionRestore()
    {
        if (phaseWork!=null) { StopCoroutine(phaseWork); phaseWork=null; }
        foreach (var bot in Bots) { if (bot!=null) { bot.SuspendSession(); } }
    }
    public void StartMorningAfterSleep()
    {
        if (Phase != MarketPhase.PostClose || phaseWork != null) { return; }
        ReportPending = false; Day++; Phase = MarketPhase.PreOpen; elapsed = 0f; RemainingSeconds = Duration;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (GetComponent<PlayerRestController>() == null) { gameObject.AddComponent<PlayerRestController>(); }
        if (GetComponent<DayIntroOverlay>() == null) { gameObject.AddComponent<DayIntroOverlay>(); }
        if (GetComponent<EndingOverlay>() == null) { gameObject.AddComponent<EndingOverlay>(); }
        if (GetComponent<OpeningRush>() == null) { gameObject.AddComponent<OpeningRush>(); }
        if (GetComponent<SleepPrompt>() == null) { gameObject.AddComponent<SleepPrompt>(); }
        if (GetComponent<ScreenGlow>() == null) { gameObject.AddComponent<ScreenGlow>(); }
        if (GetComponent<DeskSurface>() == null) { gameObject.AddComponent<DeskSurface>(); }
        if (GetComponent<DrawerProp>() == null) { gameObject.AddComponent<DrawerProp>(); }
        if (GetComponent<DeskClutter>() == null) { gameObject.AddComponent<DeskClutter>(); }
        ChatSocialDebugUnity.ApplySeed(this);
        random = new System.Random(seed);
        ConversationFocus.ExplorationShare = topicExplorationShare; ConversationFocus.RefreshSeconds = topicRefreshSeconds;
        ConversationFocus.GameMinutes = () => (Day - 1) * 1440d + GameMinute;
        UnityEngine.Random.InitState(seed);
        messages = new BotChatMessage[Mathf.Clamp(messageLimit, 16, 1024)];
        RemainingSeconds = Duration;
        var asset = Resources.Load<TextAsset>("Chat/BotChatTemplates.ko");
        Catalog = new BotChatTemplateCatalog(asset == null ? "" : asset.text, Debug.LogWarning);
        StockManager.OnStockDelisted += OnDelisted;
        StockManager.OnStockListed += OnListed;
    }

    public BotChatTemplateCatalog Catalog { get; private set; }

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        while (Initializers > 0 || StockManager.Instance != null && !StockManager.Instance.SetupConfirmed)
        {
            yield return null;
        }

        if (StockManager.Instance == null || ExchangeWarehouse.Instance == null)
        {
            Debug.LogError("하루 초기화: StockManager/ExchangeWarehouse 필요");
            yield break;
        }

        ExchangeBot.Instance?.InitializeAssets();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (!subscribed.Contains(stock))
            {
                stock.OnTradeExecutedWithStock += OnMarketTrade;
                stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
                subscribed.Add(stock);
            }

            eventPrices[stock] = stock.currentPrice;
            if (!holders.ContainsKey(stock))
            {
                holders.Add(stock, new List<TradeBot>());
            }

            if (!recentSellers.ContainsKey(stock))
            {
                recentSellers.Add(stock, new List<TradeBot>(64));
            }
        }

        InitialDistributionComplete = true;
        for (int i = 0; i < Trader.Registry.Count; i++)
        {
            var trader = Trader.Registry[i];
            trader.BeginDailyBaseline();
            if (trader is TradeBot bot)
            {
                foreach (var stock in StockManager.Instance.stocks)
                {
                    UpdatePositionIndex(bot, stock);
                }
            }

            if (i % Mathf.Max(1, botsPerFrame) == 0)
            {
                yield return null;
            }
        }

        CorporateEconomy.Instance?.InitializeCompanyWorld();
        Ready = true;
        nextPublish = SessionClock.Time + publishMin;
    }

    public bool Register(TradeBot bot)
    {
        if (Bots.Count >= Mathf.Clamp(maxBots, 1, 2000))
        { return false; }

        bot.Social = new BotSocialState(++nextId, seed, bot.GetType().Name);
        bot.Social.LastTradingSeconds = TradingElapsed;
        Bots.Add(bot);
        byId.Add(bot.Social.Id, bot);
        return true;
    }

    public void Unregister(TradeBot bot)
    {
        Bots.Remove(bot);
        if (bot.Social != null)
        {
            byId.Remove(bot.Social.Id);
        }

        foreach (var stock in holders.Keys)
        {
            RemoveHolder(bot, stock);
        }

        foreach (var list in recentSellers.Values)
        {
            list.Remove(bot);
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].Bot == bot)
            {
                RemoveEvent(i);
            }
        }

        PruneEventOrder(bot, null);
    }

    private void PruneEventOrder(TradeBot bot, Stock stock)
    {
        int count = eventOrder.Count;
        for (int i = 0; i < count; i++)
        {
            var item = eventOrder.Dequeue();
            if ((!ReferenceEquals(bot, null) && ReferenceEquals(item.bot, bot)) || (stock != null && ReferenceEquals(item.stock, stock)))
            { continue; }

            eventOrder.Enqueue(item);
        }
    }

    public TradeBot FindBot(int id)
    { return byId.TryGetValue(id, out var bot) ? bot : null; }

    public decimal InitialCost(Stock stock, decimal supplied)
    {
        if (InitialDistributionComplete)
        { return supplied; }

        return stock.currentPrice * (decimal)(0.75 + random.NextDouble() * 0.5);
    }

    public void UpdatePositionIndex(TradeBot bot, Stock stock)
    {
        if (bot == null || bot.Social == null || stock == null)
        { return; }

        if (!holders.TryGetValue(stock, out var list))
        {
            list = new List<TradeBot>();
            holders.Add(stock, list);
        }

        if (!holderIndices.TryGetValue(stock, out var indices))
        {
            indices = new Dictionary<TradeBot, int>();
            holderIndices.Add(stock, indices);
        }

        bool held = bot.GetOwnedVolume(stock) > 0m;
        if (held && !indices.ContainsKey(bot))
        {
            indices.Add(bot, list.Count);
            list.Add(bot);
            var memory = Memory(bot, stock);
            if (memory != null)
            {
                memory.AcquiredTradingSeconds = TradingElapsed;
            }
        }

        if (!held)
        {
            RemoveHolder(bot, stock);
            if (bot.Social.Positions.TryGetValue(stock, out var memory))
            {
                memory.Observed = false;
                memory.CrossTime = -10000f;
                memory.WasLoss = false;
                memory.LossSeconds = 0;
            }
        }
    }

    private void RemoveHolder(TradeBot bot, Stock stock)
    {
        if (!holderIndices.TryGetValue(stock, out var indices) || !indices.TryGetValue(bot, out int index))
        { return; }

        var list = holders[stock];
        int last = list.Count - 1;
        var tail = list[last];
        list[index] = tail;
        indices[tail] = index;
        list.RemoveAt(last);
        indices.Remove(bot);
    }

    private BotSocialState.PositionMemory Memory(TradeBot bot, Stock stock)
    {
        var memories = bot.Social.Positions;
        if (memories.TryGetValue(stock, out var memory))
        { return memory; }

        if (memories.Count >= PositionMemoryLimit)
        { return null; }

        memory = new BotSocialState.PositionMemory();
        memories.Add(stock, memory);
        return memory;
    }

    public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
    {
        if (!(trader is TradeBot bot) || bot.Social == null || volume <= 0m || basis <= 0m)
        { return; }

        var memory = Memory(bot, stock);
        if (memory == null)
        { return; }

        if (SessionClock.Time - memory.SellTime > recentTradeLifetime)
        {
            memory.SellVolume = 0m;
            memory.SellProfit = 0m;
        }

        decimal total = memory.SellVolume + volume;
        memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
        memory.SellVolume = total;
        memory.SellTime = SessionClock.Time;
        decimal profit = (price - basis) * volume;
        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), SessionClock.Time);
        memory.SellProfit += profit;
        memory.PendingProfit += profit;
        bot.Social.Psychology.LearnFromTrade((double)((price / basis - 1m) * 100m));
        memory.PendingBasis += basis * volume;
        if (!recentSellers.TryGetValue(stock, out var list))
        {
            list = new List<TradeBot>(64);
            recentSellers.Add(stock, list);
        }

        if (!list.Contains(bot))
        {
            if (list.Count >= 64)
            {
                list.RemoveAt(0);
            }

            list.Add(bot);
        }

        Enqueue(profit > 0m ? ChatEvent.ProfitRealized : ChatEvent.LossRealized, stock, bot);
    }

    public void Consider(TradeBot bot, Stock stock)
    {
        if (stock != null && bot.cash - bot.ReservedCash >= stock.currentPrice)
        {
            Enqueue(ChatEvent.BuyConsideration, stock, bot);
        }
    }

    void Update()
    {
        if (!Ready || IntegrityFailed || ReportPending || Time.timeScale <= 0f)
        { return; }

        float dt = Time.deltaTime;
        if (Phase == MarketPhase.Closing)
        {
            RemainingSeconds = 0f;
        }
        else
        {
            float step = Mathf.Min(dt, Mathf.Max(0f, Duration - elapsed));
            elapsed += step;
            if (Phase == MarketPhase.Trading)
            {
                TradingElapsed += step * (28800.0 / Math.Max(0.1, tradingSeconds));
            }

            RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
            if (RemainingSeconds <= 0f && phaseWork == null)
            {
                phaseWork = StartCoroutine(AdvancePhase());
            }
        }

        using (StressMarker.Auto())
        {
            int count = Mathf.Min(Mathf.Max(1, botsPerFrame), Bots.Count);
            for (int i = 0; i < count && Bots.Count > 0; i++)
            {
                if (stressCursor >= Bots.Count)
                {
                    stressCursor = 0;
                }

                UpdateStress(Bots[stressCursor++]);
            }
        }

        using (ChatMarker.Auto())
        {
            ProcessChat();
        }
    }

    private IEnumerator AdvancePhase()
    {
        if (Phase == MarketPhase.PreOpen)
        {
            if (CorporateEconomy.Instance!=null) { yield return CorporateEconomy.Instance.ProcessListings(Day); }
            DailyIncomeController.Instance?.BeginDay(Day);
            for (int i = 0; i < Bots.Count; i++)
            {
                DailyIncomeController.Instance?.Renew(Bots[i]);
                if (i % Mathf.Max(1,botsPerFrame) == 0) { yield return null; }
            }
            if (StockStatistics.Instance != null) { StockStatistics.Instance.BeginTradingDay(ClosingPrices); }
            Phase = MarketPhase.Trading;
            SessionId++;
            elapsed = 0f;
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot.isActiveAndEnabled)
                {
                    bot.ResumeSession();
                }

                if (bot is ExchangeBot exchange)
                {
                    exchange.DoInitialPlacement();
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            Enqueue(ChatEvent.MarketOpen, null, null);
        }
        else if (Phase == MarketPhase.Trading)
        {
            Phase = MarketPhase.Closing;
            SessionId++;
            elapsed = 0f;
            RemainingSeconds = postCloseSeconds;
            closingTraders.Clear();
            closingTraders.AddRange(Trader.Registry);
            closingStocks.Clear();
            closingStocks.AddRange(StockManager.Instance.stocks);
            closingStocks.AddRange(StockManager.Instance.UnlistedStocks);
            for (int i = 0; i < closingTraders.Count; i++)
            {
                using (CloseMarker.Auto())
                {
                    if (closingTraders[i] is TradeBot bot)
                    {
                        bot.SuspendSession();
                        UpdateStress(bot, true);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            foreach (var stock in closingStocks)
            {
                while (stock.orderBook.HasOrders)
                {
                    using (CloseMarker.Auto())
                    {
                        stock.orderBook.CancelAllBudgeted(stock, Mathf.Max(1, closeOrdersPerFrame));
                    }

                    yield return null;
                }
            }

            ClosingPrices.Clear();
            float ledgerFrameStart=Time.realtimeSinceStartup;
            foreach (var stock in closingStocks)
            {
                if (!StockManager.Instance.stocks.Contains(stock) && !StockManager.Instance.UnlistedStocks.Contains(stock))
                { continue; }

                decimal total = ExchangeWarehouse.Instance.GetHolding(stock);
                for (int i = 0; i < closingTraders.Count; i++)
                {
                    using (CloseMarker.Auto())
                    {
                        if (closingTraders[i] != null)
                        {
                            total += closingTraders[i].GetHolding(stock);
                        }
                    }

                    if (i % Mathf.Max(1, botsPerFrame) == 0 && Time.realtimeSinceStartup-ledgerFrameStart>=.002f)
                    {
                        yield return null;
                        ledgerFrameStart=Time.realtimeSinceStartup;
                    }
                }

                if (total != stock.issuedShares)
                {
                    IntegrityFailed = true;
                    Debug.LogError("마감 정합성 실패: " + stock.symbol + " 차이 " + (stock.issuedShares - total));
                    phaseWork = null;
                    yield break;
                }

                if (stock.IsListed) { ClosingPrices[stock] = stock.currentPrice; }
            }

            foreach (var trader in closingTraders)
            {
                if (trader != null && (trader.ReservedCash != 0m || trader.EscrowVolume != 0m))
                {
                    IntegrityFailed = true; Debug.LogError("마감 예약 자산 잔존: " + trader.name); phaseWork = null; yield break;
                }
            }
            if (CorporateEconomy.Instance != null) { yield return CorporateEconomy.Instance.Settle(Day); }

            for (int i = 0; i < closingTraders.Count; i++)
            {
                var trader = closingTraders[i];
                if (trader != null)
                {
                    if (trader.ReservedCash != 0m || trader.EscrowVolume != 0m)
                    {
                        IntegrityFailed = true;
                        Debug.LogError("마감 예약 자산 잔존: " + trader.name);
                        phaseWork = null;
                        yield break;
                    }

                    using (CloseMarker.Auto())
                    {
                        trader.SettleDaily();
                    }

                    if (trader is TradeBot bot && trader.DailyReturn != 0m)
                    {
                        Enqueue(trader.DailyReturn > 0m ? ChatEvent.DailyProfit : ChatEvent.DailyLoss, null, bot);
                    }
                }

                if (i % Mathf.Max(1, botsPerFrame) == 0)
                {
                    yield return null;
                }
            }

            closingTraders.Clear();
            if (DailyIncomeController.Instance != null) { yield return DailyIncomeController.Instance.Settle(Day); }
            Phase = MarketPhase.PostClose;
            elapsed = 0f; ReportPending = true;
            Enqueue(ChatEvent.MarketClose, null, null);
        }
        else
        {
            PlayerRestController.Instance?.NightPassedAwake();
            Day++;
            Phase = MarketPhase.PreOpen;
            elapsed = 0f;
        }

        RemainingSeconds = Mathf.Max(0f, Duration - elapsed);
        // StartCoroutine can complete synchronously; always yield before clearing its handle.
        yield return null;
        phaseWork = null;
        if (ReportPending)
        {
            PlayerRestController.Instance?.ObserveClock();
            PauseMenuController.Instance?.ReceiveClosingReport();
        }
    }

    public void UpdateStress(TradeBot bot, bool force = false)
    {
        if (bot == null || bot.Social == null)
        { return; }

        var state = bot.Social;
        double seconds = TradingElapsed - state.LastTradingSeconds;
        if (!force && SessionClock.Time - state.LastSampleTime < stressSampleSeconds)
        { return; }

        state.LastSampleTime = SessionClock.Time;
        state.LastTradingSeconds = TradingElapsed;
        float before = state.Stress;
        Stock reliefStock = null;
        foreach (var stock in StockManager.Instance.stocks)
        {
            decimal qty = bot.GetOwnedVolume(stock);
            BotSocialState.PositionMemory memory;
            if (qty > 0m)
            {
                memory = Memory(bot, stock);
            }
            else
            {
                state.Positions.TryGetValue(stock, out memory);
            }

            if (memory == null)
            { continue; }

            double heldSeconds = Math.Min(seconds, Math.Max(0.0, TradingElapsed - memory.AcquiredTradingSeconds));
            float positionStressBefore = state.Stress;
            decimal cost = bot.GetOwnedAverage(stock);
            decimal rate = qty > 0m && cost > 0m ? (stock.currentPrice - cost) / cost : 0m;
            if (qty > 0m && cost > 0m)
            {
                if (rate < 0m)
                {
                    memory.LossSeconds += heldSeconds;
                    state.Stress += (float)(heldSeconds / 3600.0) * (lossPerGameHour + (float)(-rate) * severityPerGameHour) * state.Sensitivity;
                }

                if (memory.Observed)
                {
                    if (memory.WasLoss && rate >= 0m)
                    {
                        state.Stress -= breakEvenDrop * state.Recovery;
                        memory.CrossTime = SessionClock.Time;
                        Enqueue(ChatEvent.BreakEvenRecovered, stock, bot);
                    }

                    if ((memory.LastPrice > 0m && stock.currentPrice >= memory.LastPrice * (1m + (decimal)surgeRatio)) || (memory.LastReturn < 0m && rate - memory.LastReturn >= (decimal)recoveryRatio))
                    {
                        state.Stress -= recoveryDrop * state.Recovery;
                    }
                }

                memory.WasLoss = rate < 0m;
                memory.Observed = true;
                memory.LastReturn = rate;
                memory.LastPrice = stock.currentPrice;
                if (!memory.WasLoss)
                {
                    memory.LossSeconds = 0;
                }

                if (rate != 0m)
                {
                    Enqueue(rate < 0m ? ChatEvent.HoldingLoss : ChatEvent.HoldingProfit, stock, bot);
                }
            }
            else
            {
                memory.Observed = false;
                memory.WasLoss = false;
                memory.LossSeconds = 0;
            }

            if (memory.PendingBasis > 0m && SessionClock.Time - memory.LastRealizedResponse >= realizedAggregationSeconds)
            {
                float reaction = Mathf.Clamp((float)(memory.PendingProfit / memory.PendingBasis) * realizedScale, -10f, 10f);
                state.Stress -= reaction * (reaction > 0f ? state.Recovery : state.Sensitivity);
                memory.PendingProfit = 0m;
                memory.PendingBasis = 0m;
                memory.LastRealizedResponse = SessionClock.Time;
            }

            if (state.Stress < positionStressBefore)
            {
                reliefStock = stock;
            }
        }

        state.Stress = Mathf.Clamp(state.Stress, 0f, 100f);
        if (before - state.Stress >= stressDropThreshold)
        {
            state.LastDropTime = SessionClock.Time;
            state.LastDropAmount = before - state.Stress;
            state.LastDropStock = reliefStock;
            Enqueue(ChatEvent.StressRelief, reliefStock, bot);
        }
    }

    public void RiskChanged(Stock stock)
    {
        if (stock != null && Ready)
        {
            AddMessage(new BotChatMessage(++sequence, null, Day, GameMinute, stock.stockName + ": " + stock.RiskLabel, "risk_status", ChatEvent.RiskChanged));
            InvestorSociety.Instance?.NotifyMarketRisk(stock);
        }
    }

    private void OnMarketTrade(Stock stock, decimal price, decimal volume)
    {
        decimal previous = eventPrices.TryGetValue(stock, out var p) ? p : price;
        if (previous > 0m && Math.Abs(price - previous) / previous >= (decimal)surgeRatio)
        {
            Enqueue(price > previous ? ChatEvent.PriceRise : ChatEvent.PriceFall, stock, null, previous);
            eventPrices[stock] = price;
        }

        if (recentSellers.ContainsKey(stock))
        {
            Enqueue(ChatEvent.PriceRiseAfterSell, stock, null);
        }
    }

    private void ObserveConversationTrade(Stock stock,decimal price,decimal volume,IOrderOwner buyer,IOrderOwner seller,Order resting)
    {
        if (!ReferenceEquals(buyer,seller)) { ConversationFocus.Record(stock,price,volume,SessionClock.Time); }
        InvestorSociety.Instance?.ObserveSocialFill(stock, price, volume, buyer, seller, resting);
    }
    private int PickConversationEvent(float now)
    {
        conversationEventCounts.Clear(); generalConversationEvents = 0;
        foreach (var ev in events)
        {
            if (ev.Stock == null) { generalConversationEvents++; }
            else
            {
                conversationEventCounts.TryGetValue(ev.Stock,out int count); conversationEventCounts[ev.Stock] = count+1;
            }
        }
        double total = 0d;
        foreach (var ev in events) { total += EventAttention(ev,now); }
        double roll = random.NextDouble()*total;
        for (int i = 0; i < events.Count; i++)
        {
            roll -= EventAttention(events[i],now);
            if (roll <= 0d)
            { return i; }
        }
        return events.Count-1;
    }
    private double EventAttention(MarketEvent ev,float now)
    {
        double recency = .3d+.7d*Math.Max(0d,1d-(now-ev.Time)/Math.Max(1f,eventLifetime));
        if (ev.Stock == null)
        { return recency*(ev.Kind == ChatEvent.SmallTalk ? .15d : .5d)/Math.Max(1,generalConversationEvents); }
        return recency*ConversationFocus.EventWeight(ev.Stock,conversationEventCounts[ev.Stock],now);
    }

    private void RemoveEvent(int index)
    {
        var removed = events[index];
        eventIndices.Remove((removed.Kind, removed.Stock, removed.Bot));
        int last = events.Count - 1;
        if (index != last)
        {
            var moved = events[last];
            events[index] = moved;
            eventIndices[(moved.Kind, moved.Stock, moved.Bot)] = index;
        }

        events.RemoveAt(last);
    }

    public void Enqueue(ChatEvent kind, Stock stock, TradeBot bot, decimal referencePrice = 0m)
    {
        if (bot != null && bot.Social == null)
        { return; }

        var key = (kind, stock, bot);
        if (eventIndices.ContainsKey(key))
        { return; }

        if (events.Count >= EventLimit || eventOrder.Count >= EventLimit * 2)
        {
            RemoveOldestEvent();
        }

        long ticket = ++eventTicket;
        eventIndices.Add(key, events.Count);
        eventOrder.Enqueue((kind, stock, bot, ticket));
        events.Add(new MarketEvent { Kind = kind, Stock = stock, Bot = bot, Time = SessionClock.Time, ReferencePrice = referencePrice, Ticket = ticket });
    }

    private void RemoveOldestEvent()
    {
        while (eventOrder.Count > 0)
        {
            var old = eventOrder.Dequeue();
            if (eventIndices.TryGetValue((old.kind, old.stock, old.bot), out int index) && events[index].Ticket == old.ticket)
            {
                RemoveEvent(index);
                return;
            }
        }
    }

    private void ProcessChat()
    {
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        {
            int budget = Math.Min(candidatesPerFrame, events.Count);
            for (int i = 0; i < budget; i++)
            {
                int index = PickConversationEvent(SessionClock.Time); var pending = events[index];
                bool current = SessionClock.Time - pending.Time <= eventLifetime;
                current &= pending.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
                current &= pending.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
                current &= pending.Kind != ChatEvent.PriceRise || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice >= pending.ReferencePrice * (1m + (decimal)surgeRatio);
                current &= pending.Kind != ChatEvent.PriceFall || pending.Stock != null && pending.ReferencePrice > 0m && pending.Stock.currentPrice <= pending.ReferencePrice * (1m - (decimal)surgeRatio);
                if (current) { InvestorSociety.Instance.OfferMarketEvent(pending.Kind, pending.Stock, pending.Bot); }
                RemoveEvent(index);
            }
            return;
        }
        float now = SessionClock.Time;
        while (sentenceExpiry.Count > 0 && now - sentenceExpiry.Peek().time > sentenceCooldown)
        {
            var expired = sentenceExpiry.Dequeue();
            if (recentSentences.TryGetValue(expired.text, out float time) && time == expired.time)
            {
                recentSentences.Remove(expired.text);
            }
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (now - events[i].Time > eventLifetime)
            {
                RemoveEvent(i);
            }
        }

        if (Bots.Count > 0 && now >= nextConversation && (Phase == MarketPhase.Trading || Phase == MarketPhase.PostClose))
        {
            bool activeMarket = Phase == MarketPhase.Trading && ConversationFocus.HasActivity(now);
            nextConversation = now + (activeMarket ? 8f : 15f) + (float)random.NextDouble() * (activeMarket ? 10f : 20f);
            Stock topic = activeMarket && random.NextDouble() < marketConversationChance ? ConversationFocus.Choose(random.NextDouble(),now) : null;
            bool reply = messageCount > 0 && now - lastBotMessageTime < 20f;
            var latest = messageCount > 0 ? GetMessage(messageCount - 1) : default;
            reply &= latest.BotId > 0 && latest.Event != ChatEvent.Agreement && latest.Sequence > lastRepliedSequence;
            reply &= random.NextDouble() < 0.5;
            if (reply)
            {
                replySource = latest;
            }
            Enqueue(topic != null ? ChatEvent.SmallTalk : reply ? ChatEvent.Agreement : ChatEvent.SmallTalk, topic, null);
        }
        if (now < nextPublish || events.Count == 0 || Bots.Count == 0)
        { return; }

        for (int step = 0; step < Mathf.Max(1, candidatesPerFrame) && events.Count > 0; step++)
        {
            int index = PickConversationEvent(now);
            var ev = events[index];
            bool currentEvent = ev.Kind != ChatEvent.MarketOpen || Phase == MarketPhase.Trading;
            currentEvent &= ev.Kind != ChatEvent.Agreement || replySource.Sequence > lastRepliedSequence && now - ev.Time < 20f;
            currentEvent &= ev.Kind != ChatEvent.MarketClose || Phase == MarketPhase.PostClose;
            currentEvent &= ev.Kind != ChatEvent.PriceRise || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice >= ev.ReferencePrice * (1m + (decimal)surgeRatio);
            currentEvent &= ev.Kind != ChatEvent.PriceFall || ev.Stock != null && ev.ReferencePrice > 0m && ev.Stock.currentPrice <= ev.ReferencePrice * (1m - (decimal)surgeRatio);
            if (!currentEvent)
            {
                RemoveEvent(index);
                continue;
            }

            TradeBot candidate = ev.Bot;
            List<TradeBot> pool = Bots;
            if (ev.Stock != null && candidate == null && ev.Kind != ChatEvent.SmallTalk)
            {
                var map = ev.Kind == ChatEvent.PriceRiseAfterSell ? recentSellers : holders;
                if (!map.TryGetValue(ev.Stock, out pool) || pool.Count == 0)
                {
                    RemoveEvent(index);
                    continue;
                }
            }

            if (candidate == null && pool.Count > 0)
            {
                candidate = pool[(random.Next(pool.Count) + ev.Cursor) % pool.Count];
            }

            ev.Cursor++;
            ev.Candidates++;
            events[index] = ev;
            if (ev.Candidates >= 64 || ev.Bot != null)
            {
                RemoveEvent(index);
            }

            if (candidate == null || !candidate.isActiveAndEnabled || InvestorSociety.Instance != null && !candidate.Social.Psychology.ActiveAt(now) || now - candidate.Social.LastSpeech < botCooldown || now - candidate.Social.LastEvents[(int)ev.Kind] < sameEventCooldown || random.NextDouble() > speechChance)
            { continue; }

            if (ev.Kind == ChatEvent.Agreement && candidate.Social.Id == replySource.BotId)
            { continue; }
            var templates = Catalog.ForEvent(ev.Kind);
            float sum = 0f;
            for (int t = 0; t < templates.Count; t++)
            {
                if (Eligible(candidate, ev.Stock, templates[t]))
                {
                    sum += templates[t].weight;
                }
            }

            if (sum <= 0f)
            { continue; }

            float roll = (float)random.NextDouble() * sum;
            BotChatTemplateCatalog.Entry chosen = null;
            for (int t = 0; t < templates.Count; t++)
            {
                if (!Eligible(candidate, ev.Stock, templates[t]))
                { continue; }

                roll -= templates[t].weight;
                if (roll <= 0f)
                {
                    chosen = templates[t];
                    break;
                }
            }

            if (chosen == null)
            { continue; }

            string text = chosen.text.Replace("{stockName}", ev.Stock == null ? "" : ev.Stock.stockName);
            InvestorSociety.Instance?.ReadBeforeSpeech(candidate);
            if (now - candidate.Social.LastSpeech < botCooldown)
            { continue; }
            text = ev.Kind == ChatEvent.SmallTalk && ev.Stock != null ? InvestorConversation.MarketRemark(candidate,ev.Stock,random.Next()) : BotMind.Compose(candidate, ev.Kind, ev.Stock, text);
            if (ev.Kind == ChatEvent.Agreement)
            {
                text = replySource.Nickname + "님, " + text;
            }
            if (recentSentences.ContainsKey(text))
            { continue; }

            var state = candidate.Social;
            state.LastSpeech = now;
            state.LastEvents[(int)ev.Kind] = now;
            if (state.RecentSpeech.Count >= 16)
            {
                state.RecentSpeech.Dequeue();
            }

            state.RecentSpeech.Enqueue((chosen.id, now));
            if (sentenceExpiry.Count >= messages.Length)
            {
                var oldest = sentenceExpiry.Dequeue();
                recentSentences.Remove(oldest.text);
            }

            sentenceExpiry.Enqueue((text, now));
            recentSentences[text] = now;
            if (ev.Kind == ChatEvent.Agreement)
            {
                lastRepliedSequence = replySource.Sequence;
            }
            AddMessage(new BotChatMessage(++sequence, candidate, Day, GameMinute, text, chosen.id, ev.Kind));
            nextPublish = now + (float)(publishMin + random.NextDouble() * Math.Max(0, publishMax - publishMin));
            return;
        }
    }

    public void PublishParticipant(TradeBot bot, string text, InvestorMessageContext context = default)
    {
        if (bot == null || bot.Social == null || string.IsNullOrWhiteSpace(text))
        { return; }
        if (InvestorSociety.Instance != null && InvestorSociety.Instance.SocialReady)
        { InvestorSociety.Instance.OfferExistingSpeech(bot, 0, context, ChatEvent.Agreement); return; }
        AddMessage(new BotChatMessage(++sequence, bot, Day, GameMinute, text, "contact", ChatEvent.Agreement, context));
        bot.Social.LastSpeech = SessionClock.Time;
    }
    public void PublishSocialParticipant(TradeBot bot, SocialMessage message, InvestorMessageContext context)
    {
        AddMessage(new BotChatMessage(++sequence, bot, Day, GameMinute, message.text, message.templateKey, ChatEvent.Agreement, context, message));
    }
    public void PublishBulletin(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        { return; }
        AddMessage(new BotChatMessage(++sequence, null, Day, GameMinute, text, "bulletin", ChatEvent.RiskChanged));
    }
    /// <summary>플레이어가 공개방에 직접 올리는 말.</summary>
    public long PublishPlayerLine(string text, string templateId)
    {
        if (string.IsNullOrWhiteSpace(text))
        { return 0; }
        var message = new BotChatMessage(++sequence, null, Day, GameMinute, text, templateId, ChatEvent.Agreement, fromPlayer: true);
        AddMessage(message);
        return message.Social.id;
    }
    public void PublishRoomNotice(string text, ChatRoomNotice notice, long eventId)
    {
        if (string.IsNullOrWhiteSpace(text) || notice == ChatRoomNotice.None)
        { return; }
        AddMessage(new BotChatMessage(++sequence, null, Day, GameMinute, text, "SYSTEM.ROOM", ChatEvent.Agreement,
            new InvestorMessageContext(eventId, 0, 0, 1, 0, false), notice: notice));
    }
    public bool Eligible(TradeBot bot, Stock stock, BotChatTemplateCatalog.Entry entry)
    {
        var state = bot.Social;
        if (state == null || state.Stress < entry.minStress || state.Stress > entry.maxStress || entry.toneKind != state.Tone || (entry.personalityKind != BotPersonality.Any && entry.personalityKind != state.Personality))
        { return false; }

        bool held = stock != null && bot.GetOwnedVolume(stock) > 0m;
        if ((entry.holdingKind == HoldingCondition.Holding && !held) || (entry.holdingKind == HoldingCondition.NotHolding && held) || (entry.text.Contains("{stockName}") && stock == null))
        { return false; }

        float now = SessionClock.Time;
        foreach (var speech in state.RecentSpeech)
        {
            if (speech.id == entry.id && now - speech.time < sentenceCooldown)
            { return false; }
        }

        BotSocialState.PositionMemory memory = null;
        if (stock != null)
        {
            state.Positions.TryGetValue(stock, out memory);
        }

        decimal cost = held ? bot.GetOwnedAverage(stock) : 0m;
        decimal profit = held ? stock.currentPrice - cost : 0m;
        foreach (var requirement in entry.facts)
        {
            bool recent = memory != null && now - memory.SellTime <= recentTradeLifetime;
            bool valid = requirement switch
            {
                ChatRequirement.UnrealizedProfitPositive => held && cost > 0m && profit > 0m,
                ChatRequirement.UnrealizedProfitNegative => held && cost > 0m && profit < 0m,
                ChatRequirement.CrossedUnrealizedLossToNonNegative => held && profit >= 0m && memory != null && now - memory.CrossTime <= eventLifetime,
                ChatRequirement.RecentProfitableSell => recent && memory.SellProfit > 0m,
                ChatRequirement.RecentLosingSell => recent && memory.SellProfit < 0m,
                ChatRequirement.RecentSellThenPriceRise => recent && memory.SellPrice > 0m && stock.currentPrice >= memory.SellPrice * (1m + (decimal)afterSellRiseRatio),
                ChatRequirement.DailyReturnPositive => bot.DailySettledDay == Day && bot.DailyReturn > 0m,
                ChatRequirement.DailyReturnNegative => bot.DailySettledDay == Day && bot.DailyReturn < 0m,
                ChatRequirement.RecentStressDrop => stock == state.LastDropStock && now - state.LastDropTime <= eventLifetime && state.LastDropAmount >= stressDropThreshold,
                _ => false
            };
            if (!valid)
            { return false; }
        }

        return true;
    }

    private void AddMessage(BotChatMessage message)
    {
        if (message.BotId > 0)
        {
            lastBotMessageTime = SessionClock.Time;
        }
        if (messageCount == messages.Length)
        {
            messageStart = (messageStart + 1) % messages.Length;
            messageCount--;
        }

        messages[(messageStart + messageCount++) % messages.Length] = message;
    }

    public BotChatMessage GetMessage(int index)
    { return messages[(messageStart + index) % messages.Length]; }

    void OnListed(Stock stock)
    {
        stock.OnTradeExecutedWithStock -= OnMarketTrade;
        stock.OnTradeExecutedWithStock += OnMarketTrade;
        stock.OnMatchedTrade -= ObserveConversationTrade;
        stock.OnMatchedTrade += ObserveConversationTrade; ConversationFocus.Register(stock);
        if (!subscribed.Contains(stock)) { subscribed.Add(stock); }
        if (!eventPrices.ContainsKey(stock)) { eventPrices[stock] = stock.currentPrice; }
        if (!holders.ContainsKey(stock))
        {
            holders[stock] = new List<TradeBot>();
        }

        if (!recentSellers.ContainsKey(stock))
        {
            recentSellers[stock] = new List<TradeBot>(64);
        }
    }

    void OnDelisted(Stock stock)
    {
        PruneEventOrder(null, stock);
        stock.OnTradeExecutedWithStock -= OnMarketTrade;
        stock.OnMatchedTrade -= ObserveConversationTrade; ConversationFocus.Remove(stock);
        subscribed.Remove(stock);
        holders.Remove(stock);
        holderIndices.Remove(stock);
        recentSellers.Remove(stock);
        eventPrices.Remove(stock);
        ClosingPrices.Remove(stock);
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].Stock == stock)
            {
                RemoveEvent(i);
            }
        }
    }

    void OnDestroy()
    {
        StockManager.OnStockDelisted -= OnDelisted;
        StockManager.OnStockListed -= OnListed;
        foreach (var stock in subscribed)
        {
            stock.OnTradeExecutedWithStock -= OnMarketTrade;
            stock.OnMatchedTrade -= ObserveConversationTrade;
        }

        subscribed.Clear();
        events.Clear();
        eventIndices.Clear();
        eventOrder.Clear();
        holders.Clear();
        holderIndices.Clear();
        recentSellers.Clear();
        byId.Clear();
        Bots.Clear();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
