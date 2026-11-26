using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 자율 시장 관찰. 가격·감정·주문을 주입하지 않는다. 기존 체결 이벤트만 구독해 5초마다 종목별 한 줄을 집계한다.
/// 유일한 플레이어 행동은 해금 구매 1건(시장 주문이 아니며 봇 자산과 무관)이고 결과는 unlock.txt에 따로 적는다.
/// </summary>
public static class MarketBeliefValidation
{
    private sealed class Tally
    {
        public int fills, buyAggressive, sellAggressive;
        public decimal volume, turnover;
        public int totalFills, whaleOfferFills, whaleHitFills;
        public decimal whaleOfferVolume, whaleHitVolume, whaleBought;
        public decimal totalVolume, totalTurnover, open, high, low;
    }

    private static string folder;
    private static double began;
    private static float readyAt = -1, nextSample;
    private static bool finishing, confirmed, oldOptions, unlockStarted, unlockChecked;
    private static EnterPlayModeOptions oldFlags;
    private static int frames, errors, panicStarts, fomoStarts, observeSeconds = 300;
    private static readonly Dictionary<Stock, Tally> tallies = new();
    private static readonly Dictionary<string, int> buyerKinds = new(), sellerKinds = new();
    private static readonly List<double> beliefs = new(2048);
    private static readonly List<(decimal price, decimal volume)> levels = new(10);
    private static StreamWriter samples, errorLog, unlockLog, memoryLog;
    private static float nextMemory;
    private static int closePhase;
    private static float closeAt, closeStarted;
    private static decimal savedCash, savedPrice, savedIssued;
    private static double savedBelief;
    private static int savedDay, savedBotId;
    private static bool savedUnlock;
    private static StreamWriter closeLog, whaleLog, cardLog, replyLog, chatLog;
    private static bool hadLanguage, languageChanged;
    private static string languageBefore;
    private static readonly Dictionary<long, SocialMessage> spoken = new();
    private static int addressed, addressedWithParent, sameSymbol, addressedNoParent, sourceDoubt, sourceDoubtWithNews, brags, saleTalk;
    private static decimal smallestBrag = decimal.MaxValue;
    private static string smallestBragText = "";
    private static InvestorSociety cardSociety;
    private static System.IO.StreamWriter hoursLive;
    private static int cardHolding, cardGain, cardLoss, cardHoldingLoss, cardShots, spokenMoney;
    private static float cardShotAt = -1f;
    private static bool chatFocused;
    private static string accumulateSymbol;
    private static bool accumulateFunded;
    private static float nextAccumulate;
    private static SocialJournal journal;
    private static decimal unlockCashBefore;
    private static readonly StringBuilder line = new(512);

    public static void Run()
    {
        folder = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        string project = Path.GetFullPath(".") + Path.DirectorySeparatorChar;
        if (string.IsNullOrEmpty(folder) || !Path.GetFullPath(folder).StartsWith(project, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Output must be inside this project."); }

        string seconds = Environment.GetEnvironmentVariable("DONDON_OBSERVE_SECONDS");
        if (int.TryParse(seconds, out int parsed) && parsed >= 30 && parsed <= 3600)
        {
            observeSeconds = parsed;
        }

        Directory.CreateDirectory(folder);
        // 다른 언어 화면을 확인할 때만 언어 설정을 잠시 바꾼다. 끝나면 원래 값으로 되돌린다(기존 검증들과 같은 방식).
        string locale = Environment.GetEnvironmentVariable("DONDON_LOCALE");
        if (locale == "en" || locale == "ja" || locale == "ko")
        {
            hadLanguage = PlayerPrefs.HasKey("DonDon.Language");
            languageBefore = PlayerPrefs.GetString("DonDon.Language");
            File.WriteAllText(Path.Combine(folder, "language-before.txt"), (hadLanguage ? "1" : "0") + Environment.NewLine + languageBefore);
            PlayerPrefs.SetString("DonDon.Language", locale);
            PlayerPrefs.Save();
            languageChanged = true;
        }

        samples = Writer("samples.csv");
        errorLog = Writer("errors.txt");
        unlockLog = Writer("unlock.txt");
        closeLog = Writer("closing.txt");
        cardLog = Writer("cards.txt");
        replyLog = Writer("replies.txt");
        chatLog = Writer("chat.tsv");
        chatLog.WriteLine("at\tid\troom\tsender\ttarget\treplyTo\tthread\tact\tsymbol\ttemplate\ttext");
        whaleLog = Writer("whales.csv");
        whaleLog.WriteLine("t,symbol,price,real,ownerHolding,openingShares,playerShare,vigilance,pressure,buybackShares,averageSale,soldToday,ownerCash");
        accumulateSymbol = Environment.GetEnvironmentVariable("DONDON_PLAYER_ACCUMULATE");
        memoryLog = Writer("memory.csv");
        memoryLog.WriteLine("t,phase,reportPending,managedMB,totalAllocatedMB,totalReservedMB,activeOrders,bots,monoHeapMB,page");
        samples.WriteLine("t,day,minute,symbol,price,real,crowd,fills,volume,turnover,buyAgg,sellAgg,bestBid,bestAsk,bidDepth,askDepth,holders,beliefN,beliefP10,beliefP50,beliefP90,panic,fomo,grade,phase,bidLevels10,bidWide10,askLevels10,askWide10");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled;
        oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += Log;
        EditorApplication.update += Monitor;
        EditorApplication.EnterPlaymode();
    }

    private static StreamWriter Writer(string name)
    { return new StreamWriter(Path.Combine(folder, name), false, new UTF8Encoding(false), 65536); }

    private static void Log(string text, string stack, LogType kind)
    {
        if (kind != LogType.Error && kind != LogType.Exception)
        { return; }

        errors++;
        if (errors <= 40)
        {
            errorLog?.WriteLine(text + "\n" + stack);
            errorLog?.Flush();
        }
    }

    private static void Monitor()
    {
        if (finishing)
        { return; }

        if (EditorApplication.timeSinceStartup - began > observeSeconds + 240)
        {
            Finish("wall-clock budget");
            return;
        }

        if (!EditorApplication.isPlaying)
        { return; }

        if (UnityEngine.Object.FindAnyObjectByType<MarketBeliefProbe>() == null)
        {
            new GameObject("Area :: Temporary Market Belief Observer").AddComponent<MarketBeliefProbe>();
        }
    }

    internal static void Step()
    {
        if (finishing)
        { return; }

        try
        {
            frames++;
            var day = MarketDayController.Instance;
            var manager = StockManager.Instance;
            if (day == null || manager == null)
            { return; }

            if (!confirmed && frames > 3)
            {
                if (NewGame.CanBegin) { NewGame.Begin(); }

                confirmed = manager.SetupConfirmed;
            }

            foreach (var stock in manager.stocks)
            {
                if (!tallies.ContainsKey(stock))
                {
                    tallies.Add(stock, new Tally { open = stock.currentPrice, high = stock.currentPrice, low = stock.currentPrice });
                    stock.OnMatchedTrade += OnTrade;
                }
            }

            if (!ReferenceEquals(journal, ChatSocialAudit.Journal))
            {
                if (journal != null)
                {
                    journal.Happened -= OnEvent;
                }

                journal = ChatSocialAudit.Journal;
                journal.Happened += OnEvent;
            }

            if (day.IntegrityFailed)
            {
                Finish("market integrity failure");
                return;
            }

            if (!day.Ready)
            { return; }

            if (readyAt < 0)
            {
                readyAt = Time.time;
                nextSample = Time.time;
                WriteBots(day, "bots-start.csv");
            }

            float t = Time.time - readyAt;
            // 메모리 추이: 장중 5초, 마감 처리 중에는 0.5초 간격. 읽기만 한다.
            if (Time.unscaledTime >= nextMemory)
            {
                nextMemory = Time.unscaledTime + (day.Phase == MarketPhase.Trading ? 5f : 0.5f);
                int orders = 0;
                foreach (var stock in manager.stocks)
                {
                    orders += stock.orderBook.ActiveOrderCount;
                }

                memoryLog.WriteLine(t.ToString("F1", CultureInfo.InvariantCulture) + "," + day.Phase + "," + day.ReportPending + "," + (GC.GetTotalMemory(false) >> 20) + "," +
                    (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() >> 20) + "," + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() >> 20) + "," + orders + "," + day.Bots.Count + "," + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + "," + (PauseMenuController.Instance == null ? "" : PauseMenuController.Instance.CurrentPage.ToString()));
                memoryLog.Flush();
            }

            CheckUnlock(day, t);
            Accumulate(day, manager, t); InjectDumps(day, manager, t); InjectTactics(day, manager, t); ForceMoths(manager, t);
            WatchCards(t);
            if (Time.time >= nextSample)
            {
                nextSample = Time.time + 5f;
                Sample(day, manager, t);
            }

            if (CheckClosing(day, manager))
            {
                Finish("observation completed");
                return;
            }

            if (t >= observeSeconds)
            {
                Finish("observation completed");
            }
        }
        catch (Exception exception)
        {
            errors++;
            errorLog?.WriteLine(exception.ToString());
            Finish("observer exception");
        }
    }

    private static void OnEvent(SocialEvent record)
    {
        if (record.kind == "PANIC_START")
        {
            panicStarts++;
        }
        else if (record.kind == "FOMO_START")
        {
            fomoStarts++;
        }
    }

    private static void OnTrade(Stock stock, decimal price, decimal quantity, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (!tallies.TryGetValue(stock, out var tally) || resting == null)
        { return; }

        tally.fills++;
        tally.totalFills++;
        tally.volume += quantity;
        tally.totalVolume += quantity;
        tally.turnover += price * quantity;
        tally.totalTurnover += price * quantity;
        tally.high = Math.Max(tally.high, price);
        tally.low = Math.Min(tally.low, price);
        // 대기 주문이 매도였으면 매수자가 치고 들어온 체결이다.
        if (resting.isBuy)
        {
            tally.sellAggressive++;
        }
        else
        {
            tally.buyAggressive++;
        }

        // 세력의 매도가 걸어 둔 호가를 누가 사 간 것인지(offer), 세력이 매수 호가를 친 것인지(hit) 구분한다.
        if (seller is WhaleBot)
        {
            if (resting.isBuy)
            {
                tally.whaleHitFills++;
                tally.whaleHitVolume += quantity;
            }
            else
            {
                tally.whaleOfferFills++;
                tally.whaleOfferVolume += quantity;
            }
        }

        if (buyer is WhaleBot)
        {
            tally.whaleBought += quantity;
        }

        Count(buyerKinds, stock.symbol + ":" + Kind(buyer));
        Count(sellerKinds, stock.symbol + ":" + Kind(seller));
    }

    private static string Kind(IOrderOwner owner)
    { return owner == null ? "none" : owner.GetType().Name; }

    private static void Count(Dictionary<string, int> map, string key)
    {
        map.TryGetValue(key, out int value);
        map[key] = value + 1;
    }

    private static void Sample(MarketDayController day, StockManager manager, float t)
    {
        foreach (var stock in manager.stocks)
        {
            if (!tallies.TryGetValue(stock, out var tally))
            { continue; }

            beliefs.Clear();
            int holders = 0, panic = 0, fomo = 0;
            foreach (var bot in day.Bots)
            {
                if (bot == null || bot.Social == null || bot.KnowsRealValue)
                { continue; }

                if (bot.GetOwnedVolume(stock) > 0m)
                {
                    holders++;
                }

                var mind = bot.Social.Psychology;
                if (mind.Companies.TryBelief(stock.symbol, out double belief))
                {
                    beliefs.Add(belief);
                }

                if (mind.HasPanicFor(stock))
                {
                    panic++;
                }

                if (mind.HasFomoFor(stock))
                {
                    fomo++;
                }
            }

            beliefs.Sort();
            stock.orderBook.GetTopBids(levels, 10);
            decimal bestBid = levels.Count > 0 ? levels[0].price : 0m, bidDepth = 0m;
            foreach (var level in levels)
            {
                bidDepth += level.volume;
            }

            stock.orderBook.GetTopAsks(levels, 10);
            decimal bestAsk = levels.Count > 0 ? levels[0].price : 0m, askDepth = 0m;
            foreach (var level in levels)
            {
                askDepth += level.volume;
            }

            line.Clear();
            line.Append(t.ToString("F1", CultureInfo.InvariantCulture)).Append(',').Append(day.Day).Append(',').Append(day.GameMinute).Append(',').Append(stock.symbol).Append(',')
                .Append(N(stock.currentPrice)).Append(',').Append(N(stock.realValue)).Append(',').Append(N(stock.fakeValue)).Append(',')
                .Append(tally.fills).Append(',').Append(N(tally.volume)).Append(',').Append(N(tally.turnover)).Append(',')
                .Append(tally.buyAggressive).Append(',').Append(tally.sellAggressive).Append(',')
                .Append(N(bestBid)).Append(',').Append(N(bestAsk)).Append(',').Append(N(bidDepth)).Append(',').Append(N(askDepth)).Append(',')
                .Append(holders).Append(',').Append(beliefs.Count).Append(',')
                .Append(P(.1)).Append(',').Append(P(.5)).Append(',').Append(P(.9)).Append(',')
                .Append(panic).Append(',').Append(fomo).Append(',').Append(stock.RiskGrade).Append(',').Append(day.Phase);
            stock.orderBook.GetTopBids(levels, 400);
            int bidLevelsWide = 0, askLevelsWide = 0;
            decimal bidWide = 0m, askWide = 0m;
            foreach (var level in levels)
            {
                if (level.price >= stock.currentPrice * .9m)
                {
                    bidLevelsWide++;
                    bidWide += level.volume;
                }
            }

            stock.orderBook.GetTopAsks(levels, 400);
            foreach (var level in levels)
            {
                if (level.price <= stock.currentPrice * 1.1m)
                {
                    askLevelsWide++;
                    askWide += level.volume;
                }
            }

            line.Append(',').Append(bidLevelsWide).Append(',').Append(bidWide.ToString("0")).Append(',').Append(askLevelsWide).Append(',').Append(askWide.ToString("0"));
            samples.WriteLine(line.ToString());
            tally.fills = tally.buyAggressive = tally.sellAggressive = 0;
            tally.volume = tally.turnover = 0m;
        }

        samples.Flush();
        var drama = WorldDramaDirector.Instance;
        if (drama != null)
        {
            foreach (var stock in manager.stocks)
            {
                var owner = stock.Owner as TradeBot;
                var fund = drama.FundOf(owner);
                if (owner == null || fund == null)
                { continue; }

                decimal playerOwned = PlayerAccounts.Instance == null ? 0m : PlayerAccounts.Instance.Owned(stock);
                whaleLog.WriteLine(t.ToString("F1", CultureInfo.InvariantCulture) + "," + stock.symbol + "," + N(stock.currentPrice) + "," + N(stock.realValue) + "," + N(owner.GetOwnedVolume(stock)) + "," + N(fund.OpeningShares) + "," +
                    (playerOwned / stock.issuedShares).ToString("F4", CultureInfo.InvariantCulture) + "," + drama.Vigilance(owner, stock).ToString("F3", CultureInfo.InvariantCulture) + "," + drama.Pressure(owner).ToString("F3", CultureInfo.InvariantCulture) + "," +
                    N(fund.BuybackShares) + "," + N(fund.BuybackShares >= 1m ? fund.BuybackProceeds / fund.BuybackShares : 0m) + "," + N(fund.SoldToday) + "," + N(owner.cash));
            }

            whaleLog.Flush();
        }
    }

    /// <summary>
    /// 검증용 주입: 플레이어가 한 종목을 실제 매도 호가에서 꾸준히 사 모은다. 세력의 경계 반응을 보기 위한 것이며 자율 관찰이 아니다.
    /// 현금만 넣어 주고 주문은 실제 호가창에 실제 체결로 낸다.
    /// </summary>
    private static void Accumulate(MarketDayController day, StockManager manager, float t)
    {
        if (string.IsNullOrEmpty(accumulateSymbol) || t < 120f || day.Phase != MarketPhase.Trading || Time.time < nextAccumulate)
        { return; }

        nextAccumulate = Time.time + 4f;
        var player = manager.playerAccount;
        if (!accumulateFunded)
        {
            accumulateFunded = true;
            player.cash += 3000000000m;
        }

        foreach (var stock in manager.stocks)
        {
            if (stock.symbol != accumulateSymbol)
            { continue; }

            stock.orderBook.GetTopAsks(levels, 1);
            if (levels.Count > 0 && levels[0].price <= stock.currentPrice * 1.03m)
            {
                player.BuyLimit(levels[0].price, 3000m, 0, stock);
            }
        }
    }

    /// <summary>
    /// 검증용 주입: 아무 소식 없이 한 종목을 내려찍는다(DONDON_INJECT_DUMP="종목:깊이,종목:깊이", 예 "KMF:0.15,HBF:0.10").
    /// 시세가 "시장이 믿는 가격"보다 싸졌을 때 값이 그 가격까지 돌아오는지 재려는 것이다.
    /// 주식은 거래소 보유분에서 넘겨받고(새로 만들지 않는다), 실제 매수 호가에 실제 주문으로 판다. 목표 깊이에 닿거나 60초가 지나면 손을 뗀다.
    /// </summary>
    private sealed class Dump { public Stock stock; public decimal from, depth; public float until; public bool done, armed; public decimal sold; }
    private static readonly List<Dump> dumps = new();
    private static bool dumpsRead; private static float nextDump;
    private static StreamWriter dumpLog;
    private static void InjectDumps(MarketDayController day, StockManager manager, float t)
    {
        if (!dumpsRead)
        {
            dumpsRead = true; dumps.Clear();
            string wanted = Environment.GetEnvironmentVariable("DONDON_INJECT_DUMP");
            if (!string.IsNullOrEmpty(wanted))
            {
                foreach (string part in wanted.Split(','))
                {
                    string[] pair = part.Split(':'); var stock = manager.FindStock(pair[0].Trim());
                    decimal depth = pair.Length > 1 && decimal.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d) ? d : .15m;
                    if (stock != null) { dumps.Add(new Dump { stock = stock, depth = depth }); }
                }
                dumpLog = Writer("dumps.txt");
            }
        }
        if (dumps.Count == 0 || t < 150f || day.Phase != MarketPhase.Trading || Time.time < nextDump) { return; }
        nextDump = Time.time + 2f;
        var player = manager.playerAccount; var exchange = ExchangeBot.Instance;
        foreach (var dump in dumps)
        {
            if (dump.done) { continue; }
            var stock = dump.stock;
            if (!dump.armed)
            {
                dump.armed = true; dump.from = stock.currentPrice; dump.until = Time.time + 60f;
                // 유통 물량의 넉넉한 몫을 넘겨받는다. 거래소가 내줄 수 있는 만큼만 받는다.
                decimal want = decimal.Floor(400_000_000m / Math.Max(1m, stock.currentPrice)); decimal got = 0m;
                for (int i = 0; i < 6 && got <= 0m && want >= 1m; i++) { if (exchange != null && exchange.TransferInventory(player, stock, want, stock.currentPrice)) { got = want; } else { want = decimal.Floor(want / 2m); } }
                dumpLog.AutoFlush = true; dumpLog.WriteLine($"t={t:F0} {stock.symbol} start price={N(stock.currentPrice)} crowd={N(stock.fakeValue)} shares={N(got)} target={N(dump.from * (1m - dump.depth))}");
            }
            decimal held = player.GetHolding(stock);
            if (stock.currentPrice <= dump.from * (1m - dump.depth) || Time.time >= dump.until || held < 1m)
            {
                dump.done = true;
                dumpLog.WriteLine($"t={t:F0} {stock.symbol} stop price={N(stock.currentPrice)} ({Pct(stock.currentPrice / dump.from - 1m)}) crowd={N(stock.fakeValue)} sold={N(dump.sold)} left={N(held)}");
                dumpLog.Flush();
                continue;
            }
            decimal chunk = Math.Min(held, decimal.Floor(40_000_000m / Math.Max(1m, stock.currentPrice)) + 1m);
            dump.sold += ManipulationTools.Slam(player, stock, chunk, .03m, 0m);
        }
    }

    /// <summary>
    /// 검증용 주입: 플레이어의 작전 도구를 정해진 때에 쓴다(DONDON_INJECT_TACTIC="도구:종목:비중:시각,…", 예 "ignite:KMF:0.25:200,slam:KMF:1:420").
    /// 도구는 ignite, slam, wash, wall, close. 화면에 적힌 효과(값이 오른다, 눈길을 끈다 …)가 실제로 나는지, 그리고 그 결과 내 돈이 어떻게 되는지 잰다.
    /// 쓰기 직전과 10초·60초·180초 뒤의 값·시장이 믿는 가격·열기·그 종목에서의 내 손익(판 돈 − 산 돈 + 남은 주식의 평가)을 tactics.txt에 적는다.
    /// </summary>
    private sealed class TacticShot { public string tool; public Stock stock; public float fraction, at; public bool fired; public decimal price0, cash0, held0; public int reported; public bool accepted; }
    private static readonly List<TacticShot> shots = new();
    private static bool shotsRead; private static StreamWriter tacticLog;
    private static readonly float[] TacticMarks = { 10f, 60f, 180f };
    private static void InjectTactics(MarketDayController day, StockManager manager, float t)
    {
        if (!shotsRead)
        {
            shotsRead = true; shots.Clear();
            string wanted = Environment.GetEnvironmentVariable("DONDON_INJECT_TACTIC");
            if (!string.IsNullOrEmpty(wanted))
            {
                foreach (string part in wanted.Split(','))
                {
                    string[] f = part.Split(':'); if (f.Length < 4) { continue; }
                    var stock = manager.FindStock(f[1].Trim());
                    if (stock == null || !float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float fraction) || !float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float at)) { continue; }
                    shots.Add(new TacticShot { tool = f[0].Trim().ToLowerInvariant(), stock = stock, fraction = fraction, at = at });
                }
                tacticLog = Writer("tactics.txt");
            }
        }
        if (shots.Count == 0 || day.Phase != MarketPhase.Trading) { return; }
        var player = manager.playerAccount; var tactics = PlayerTactics.Instance;
        if (player == null || tactics == null) { return; }
        foreach (var shot in shots)
        {
            var stock = shot.stock;
            float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
            if (!shot.fired)
            {
                if (t < shot.at) { continue; }
                shot.fired = true; shot.price0 = stock.currentPrice; shot.cash0 = player.cash; shot.held0 = player.GetOwnedVolume(stock);
                // 작전 탭 도식의 예보(불붙이기: 마지막으로 사들일 값과 쓸 돈)를 실제 결과와 견준다.
                decimal lastForecast = 0m, spentForecast = 0m;
                bool forecast = shot.tool == "ignite" && TacticDiagram.ForecastIgnite(stock, PlayerTactics.Account, shot.fraction, out lastForecast, out spentForecast);
                if (shot.tool == "ignite")
                {
                    decimal cashBefore = PlayerTactics.Account.cash;
                    bool ok = tactics.Ignite(stock, shot.fraction);
                    decimal spentActual = cashBefore - PlayerTactics.Account.cash;
                    tacticLog.WriteLine($"FORECAST ignite {stock.symbol} forecast={(forecast ? N(lastForecast) : "none")} actualPrice={N(stock.currentPrice)} lastTrade={N(stock.orderBook.lastPrice)} spentForecast={N(spentForecast)} cashOut={N(spentActual)} match={(forecast && stock.orderBook.lastPrice == lastForecast)}");
                    shot.accepted = ok;
                    tacticLog.WriteLine($"t={t:F0} {shot.tool} {stock.symbol} x{shot.fraction.ToString(CultureInfo.InvariantCulture)} accepted={shot.accepted} notice='{tactics.Notice}' | before price={N(shot.price0)}");
                    tacticLog.Flush();
                    continue;
                }
                shot.accepted = shot.tool == "ignite" ? tactics.Ignite(stock, shot.fraction) : shot.tool == "slam" ? tactics.Slam(stock, shot.fraction)
                    : shot.tool == "wash" ? tactics.StartWash(stock) : shot.tool == "unload" ? tactics.StartUnload(stock, shot.fraction) : shot.tool == "wall" ? tactics.SetWall(stock, shot.fraction) : shot.tool == "close" && tactics.ArmClose(stock, shot.fraction);
                tacticLog.WriteLine($"t={t:F0} {shot.tool} {stock.symbol} x{shot.fraction.ToString(CultureInfo.InvariantCulture)} accepted={shot.accepted} notice='{tactics.Notice}' | before price={N(shot.price0)} crowd={N(stock.fakeValue)} heat={heat:F1} cash={N(shot.cash0)} held={N(shot.held0)} | right after price={N(stock.currentPrice)} ({Pct(stock.currentPrice / shot.price0 - 1m)}) cash={N(player.cash)} held={N(player.GetOwnedVolume(stock))}");
                tacticLog.Flush();
                continue;
            }
            if (shot.reported >= TacticMarks.Length || t < shot.at + TacticMarks[shot.reported]) { continue; }
            decimal held = player.GetOwnedVolume(stock);
            // 이 종목에서의 손익: 현금이 달라진 만큼 + 주식 수가 달라진 만큼의 지금 값(다른 종목의 작전이 겹치면 현금에는 그것도 섞인다).
            decimal result = player.cash - shot.cash0 + (held - shot.held0) * stock.currentPrice + shot.held0 * (stock.currentPrice - shot.price0);
            tacticLog.WriteLine($"   +{TacticMarks[shot.reported]:F0}s {shot.tool} {stock.symbol} price={N(stock.currentPrice)} ({Pct(stock.currentPrice / shot.price0 - 1m)}) crowd={N(stock.fakeValue)} heat={heat:F1} held={N(held)} cashDelta={N(player.cash - shot.cash0)} resultOnStock={N(result)} book: realized={N(tactics.Book(stock).Realized)} ignite={N(tactics.Book(stock).IgniteCash)} slam={N(tactics.Book(stock).SlamCash)} washPainted={N(tactics.Book(stock).WashPainted)} unloaded={N(tactics.Book(stock).UnloadShares)} runs={tactics.Runs.Count}");
            tacticLog.Flush(); shot.reported++;
        }
    }

    private static string Pct(decimal fraction) => (fraction * 100m).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
    // ── 성능 측정(DONDON_PERF=1): 프레임 제한을 풀고, 장중의 프레임마다 걸린 시간을 모은다. 창 없이 돌리므로 그리는 비용은 거의 들어 있지 않다(스크립트 비용을 비교하는 용도다). ──
    public static bool Perf;
    private static readonly List<float> frameMs = new(200000);
    private static bool mothsRead; private static readonly List<(Stock stock, int wanted, float at, bool done)> forcedMoths = new();
    public static void FrameSample(float seconds)
    {
        var day = MarketDayController.Instance;
        if (Perf && readyAt >= 0 && day != null && day.Phase == MarketPhase.Trading && Time.time - readyAt > 20f) { frameMs.Add(seconds * 1000f); }
    }
    private static string PerfLine(MarketDayController day)
    {
        if (!Perf || frameMs.Count == 0) { return "perf=off"; }
        var sorted = new List<float>(frameMs); sorted.Sort();
        float At(double share) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * share))];
        double sum = 0; int over16 = 0, over33 = 0, over50 = 0;
        foreach (float ms in sorted) { sum += ms; if (ms > 16.7f) { over16++; } if (ms > 33.3f) { over33++; } if (ms > 50f) { over50++; } }
        int traders = Trader.Registry.Count, pool = MothPool.Size;
        return string.Format(CultureInfo.InvariantCulture, "perf frames={0} avgMs={1:F2} p50={2:F2} p95={3:F2} p99={4:F2} maxMs={5:F1} over16.7ms={6} ({7:P2}) over33ms={8} over50ms={9} | bots={10} traders={11} mothPool={12} managedMB={13}",
            sorted.Count, sum / sorted.Count, At(.5), At(.95), At(.99), sorted[sorted.Count - 1], over16, over16 / (double)sorted.Count, over33, over50, day == null ? 0 : day.Bots.Count, traders, pool, GC.GetTotalMemory(false) >> 20);
    }
    /// <summary>검증용: 정해진 때에 불나방을 한 종목에 불러들인다(DONDON_FORCE_MOTHS="종목:인원:시각,…", 인원 0은 풀 전체).</summary>
    private static void ForceMoths(StockManager manager, float t)
    {
        if (!mothsRead)
        {
            mothsRead = true; forcedMoths.Clear();
            string wanted = Environment.GetEnvironmentVariable("DONDON_FORCE_MOTHS");
            if (!string.IsNullOrEmpty(wanted))
            {
                foreach (string part in wanted.Split(','))
                {
                    string[] f = part.Split(':'); if (f.Length < 3) { continue; }
                    var stock = manager.FindStock(f[0].Trim());
                    if (stock != null && int.TryParse(f[1], out int count) && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float at)) { forcedMoths.Add((stock, count <= 0 ? MothPool.Size : count, at, false)); }
                }
            }
        }
        for (int i = 0; i < forcedMoths.Count; i++)
        {
            var item = forcedMoths[i];
            if (item.done || t < item.at || MothPool.Instance == null) { continue; }
            bool ok = MothPool.Instance.Force(item.stock, item.wanted);
            Debug.Log("[검증] 불나방 강제 호출 " + item.stock.symbol + " " + item.wanted + "명 → " + ok);
            forcedMoths[i] = (item.stock, item.wanted, item.at, true);
        }
    }

    private static string N(decimal value)
    { return decimal.Round(value, 1).ToString(CultureInfo.InvariantCulture); }

    private static string P(double fraction)
    {
        if (beliefs.Count == 0)
        { return "0"; }

        return beliefs[Math.Min(beliefs.Count - 1, (int)(beliefs.Count * fraction))].ToString("F1", CultureInfo.InvariantCulture);
    }

    private static void CheckUnlock(MarketDayController day, float t)
    {
        var progression = PlayerProgression.Instance;
        var player = StockManager.Instance.playerAccount;
        if (progression == null || player == null)
        { return; }

        if (!unlockStarted && t >= 5f)
        {
            unlockStarted = true;
            var chart = UnityEngine.Object.FindAnyObjectByType<ChartManager>();
            unlockLog.WriteLine("items=" + progression.UnlockItems.Count);
            unlockLog.WriteLine("before: cash=" + N(player.cash) + " crowdUnlocked=" + PlayerProgression.Has(PlayerProgression.CrowdValueUnlock) +
                " chartCrowdText='" + (chart != null && chart.textFakeValue != null ? chart.textFakeValue.text : "<none>") + "' chartRealText='" + (chart != null && chart.textRealValue != null ? chart.textRealValue.text : "<none>") + "'");
            var real = progression.FindUnlock(PlayerProgression.RealValueUnlock);
            unlockLog.WriteLine("real-before-prerequisite: canStart=" + progression.CanStartUnlock(real, out string blocked) + " reason=" + blocked);
            unlockCashBefore = player.cash;
            bool started = progression.TryStartUnlock(PlayerProgression.CrowdValueUnlock);
            unlockLog.WriteLine("start crowd: accepted=" + started + " result=" + progression.UnlockResult + " cashDelta=" + N(player.cash - unlockCashBefore) +
                " pending=" + progression.PendingUnlock + " remainingMinutes=" + progression.UnlockRemainingMinutes.ToString("F1", CultureInfo.InvariantCulture) + " gameMinute=" + day.GameMinute);
            bool second = progression.TryStartUnlock(PlayerProgression.CrowdValueUnlock);
            unlockLog.WriteLine("second start while pending: accepted=" + second + " result=" + progression.UnlockResult + " cashDelta=" + N(player.cash - unlockCashBefore));
            var ui = UnityEngine.Object.FindAnyObjectByType<UnlockGridUI>(FindObjectsInactive.Include);
            unlockLog.WriteLine("unlock page object present=" + (ui != null));
            unlockLog.Flush();
        }

        if (unlockStarted && !unlockChecked && PlayerProgression.Has(PlayerProgression.CrowdValueUnlock))
        {
            unlockChecked = true;
            var chart = UnityEngine.Object.FindAnyObjectByType<ChartManager>();
            unlockLog.WriteLine("completed at t=" + t.ToString("F1", CultureInfo.InvariantCulture) + " gameMinute=" + day.GameMinute + " cashDelta=" + N(player.cash - unlockCashBefore) +
                " pending='" + progression.PendingUnlock + "'");
            unlockLog.WriteLine("after (text refreshes on next crowd update): chartCrowdText='" + (chart != null && chart.textFakeValue != null ? chart.textFakeValue.text : "<none>") + "'");
            unlockLog.Flush();
        }
    }

    private static void OnCardMessage(SocialMessage message)
    {
        if (message == null)
        { return; }

        // 시간대 잡담(AFTER.…)이 실제로 몇 시에 어느 방에서 나왔는지 적어 둔다(Validation/ChatSituation/hours-live.txt).
        if (message.templateKey != null && message.templateKey.StartsWith("AFTER.", StringComparison.Ordinal))
        {
            if (hoursLive == null)
            {
                string folder = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Validation", "ChatSituation");
                System.IO.Directory.CreateDirectory(folder);
                hoursLive = new System.IO.StreamWriter(System.IO.Path.Combine(folder, "hours-live.txt"), false, new System.Text.UTF8Encoding(false));
            }
            var clock = MarketDayController.Instance;
            int minute = clock == null ? -1 : clock.ClockMinute;
            hoursLive.WriteLine((minute / 60).ToString("00") + ":" + (minute % 60).ToString("00") + " room" + message.roomId + " " + (clock == null ? "" : clock.Phase.ToString()) + " " + message.templateKey + " | " + message.text);
            hoursLive.Flush();
        }

        // 대화 일관성 집계: @상대를 붙인 말이 실제로 그 상대의 말(같은 종목)에 이어지는가.
        spoken[message.id] = message;
        if (message.facts != null && (message.facts.sourceEvent == "ProfitRealized" || message.facts.sourceEvent == "LossRealized") && (message.act == SocialAct.BRAG || message.act == SocialAct.LAMENT))
        {
            saleTalk++;
        }

        // 품질 검토용 전체 대화 기록. 방, 화자, 상대, 답글 대상, 행위, 종목, 고른 문장 키를 함께 남긴다.
        if (chatLog != null)
        {
            chatLog.WriteLine(message.at.ToString("0.0", CultureInfo.InvariantCulture) + "\t" + message.id + "\t" + message.roomId + "\t" + message.senderId + "\t" + message.targetId + "\t" + message.replyToId + "\t" + message.threadId + "\t" + message.act + "\t" + (message.facts == null ? "" : message.facts.symbol) + "\t" + message.templateKey + "\t" + (message.text ?? "").Replace("\n", " "));
        }

        if (message.act == SocialAct.BRAG && message.facts != null && !message.facts.directLead &&
            decimal.TryParse(message.facts.amount, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal bragAmount))
        {
            brags++;
            if (Math.Abs(bragAmount) < smallestBrag)
            {
                smallestBrag = Math.Abs(bragAmount);
                smallestBragText = message.text;
            }
        }

        if (message.targetId > 0)
        {
            addressed++;
            if (message.replyToId != 0 && spoken.TryGetValue(message.replyToId, out var parent))
            {
                addressedWithParent++;
                bool same = parent.facts != null && message.facts != null && parent.facts.symbol == message.facts.symbol;
                if (same)
                {
                    sameSymbol++;
                }

                if (addressedWithParent <= 60)
                {
                    replyLog.WriteLine("[" + parent.act + "] " + parent.text + "\n   -> [" + message.act + (same ? "" : " OTHER-STOCK") + "] " + message.text);
                    replyLog.Flush();
                }
            }
            else
            {
                addressedNoParent++;
                if (addressedNoParent <= 20)
                {
                    replyLog.WriteLine("[no parent] [" + message.act + "] " + message.text);
                }
            }
        }

        if (message.act == SocialAct.DOUBT && message.text != null && (message.text.Contains("출처") || message.text.Contains("소식")))
        {
            sourceDoubt++;
            if (message.facts != null && message.facts.parentNews)
            {
                sourceDoubtWithNews++;
            }
        }

        if (message.act == SocialAct.BRAG || message.act == SocialAct.LAMENT)
        {
            spokenMoney++;
        }

        var card = message.profitCard;
        if (card == null)
        { return; }

        if (card.kind == ChatProfitCardKind.Holding)
        {
            cardHolding++;
            if (card.amount < 0m)
            {
                cardHoldingLoss++;
            }
        }
        else if (card.kind == ChatProfitCardKind.RealizedGain)
        {
            cardGain++;
        }
        else
        {
            cardLoss++;
        }

        if (cardHolding + cardGain + cardLoss <= 40)
        {
            cardLog.WriteLine("room=" + message.roomId + " act=" + message.act + " kind=" + card.kind + " " + card.stockName + " amount=" + N(card.amount) + " pct=" + card.percent.ToString("F1", CultureInfo.InvariantCulture) +
                " avg=" + N(card.averageCost) + " price=" + N(card.price) + " qty=" + N(card.quantity) + " | " + message.text);
            cardLog.Flush();
        }

        // 공용 라운지에 올라온 카드를 몇 장 찍는다.
        if (message.roomId == 0 && cardShots < 4 && cardShotAt < 0f)
        {
            cardShotAt = Time.unscaledTime + 0.8f;
        }
    }

    private static void WatchCards(float t)
    {
        if (cardSociety == null && InvestorSociety.Instance != null)
        {
            cardSociety = InvestorSociety.Instance;
            cardSociety.SocialMessagePublished += OnCardMessage;
        }

        if (!chatFocused && t > 20f && DeskViewController.Instance != null)
        {
            chatFocused = true;
            DeskViewController.Instance.Focus(DeskViewTarget.Chat);
        }

        if (cardShotAt > 0f && Time.unscaledTime >= cardShotAt)
        {
            cardShotAt = -1f;
            cardShots++;
            CaptureMenu("chat-card-" + cardShots);
        }
    }

    private static void CloseCheck(bool success, string message)
    {
        closeLog.WriteLine((success ? "PASS " : "FAIL ") + message);
        closeLog.Flush();
        if (!success)
        {
            errors++;
        }
    }

    private static string MenuTexts()
    {
        var menu = PauseMenuController.Instance;
        if (menu == null || menu.UI == null)
        { return ""; }

        var parts = new List<string>();
        foreach (var text in menu.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (!string.IsNullOrEmpty(text.text))
            {
                parts.Add(text.text.Replace("\n", " "));
            }
        }

        return string.Join(" | ", parts);
    }

    private static void CaptureMenu(string name)
    {
        var camera = Camera.main;
        if (camera == null)
        { return; }

        var changed = new List<(Canvas canvas, Camera camera, float distance)>();
        var target = new RenderTexture(1920, 1080, 24);
        var prior = camera.targetTexture;
        var active = RenderTexture.active;
        var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                { continue; }

                changed.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .1f;
            }

            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + "-1920x1080.png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = prior;
            RenderTexture.active = active;
            foreach (var item in changed)
            {
                item.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                item.canvas.worldCamera = item.camera;
                item.canvas.planeDistance = item.distance;
            }

            target.Release();
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    /// <summary>게임이 스스로 연 마감 화면을 따라가며 확인한다. 건너뛰기 뒤에도 저장이 끝나는지, 저장한 파일로 복원되는지 본다.</summary>
    private static bool CheckClosing(MarketDayController day, StockManager manager)
    {
        var menu = PauseMenuController.Instance;
        if (menu == null)
        { return false; }

        if (closePhase == 0)
        {
            if (menu.CurrentPage != PauseMenuController.Page.Report)
            { return false; }

            closeStarted = Time.unscaledTime;
            string shown = MenuTexts();
            closeLog.WriteLine("settlement page shown: " + shown);
            CloseCheck(shown.Contains("당일 합계") && !shown.Contains("마감 진행"), "settlement page opens directly, without a closing progress page");
            CloseCheck(menu.ClosingSaveRunning, "save job is running behind the settlement page");
            closePhase = 1;
            closeAt = Time.unscaledTime + 0.4f;
            return false;
        }

        if (closePhase == 1)
        {
            if (Time.unscaledTime < closeAt)
            { return false; }

            var indicator = GameObject.Find("Area :: Saving Indicator");
            var label = indicator == null ? null : indicator.GetComponentInChildren<TMPro.TMP_Text>(false);
            closeLog.WriteLine("indicator: active=" + (indicator != null && indicator.activeInHierarchy) + " text='" + (label == null ? "" : label.text) + "' alpha=" + (label == null ? 0 : label.color.a).ToString("F2", CultureInfo.InvariantCulture));
            if (menu.ClosingSaveRunning)
            {
                CloseCheck(indicator != null && indicator.activeInHierarchy && label != null && label.color.a < 0.5f, "a faint corner indicator is visible while saving");
            }

            CloseCheck(!MenuTexts().Contains("저장 중"), "settlement page itself does not mention saving");
            CaptureMenu("report-while-saving");
            closePhase = 2;
            return false;
        }

        if (closePhase == 2)
        {
            if (menu.ClosingSaveRunning)
            { return false; }

            // 화면 문구는 저장이 끝난 다음 프레임에 바뀐다. 잠깐 기다린 뒤 확인한다.
            closeLog.WriteLine("save job finished after " + (Time.unscaledTime - closeStarted).ToString("F1", CultureInfo.InvariantCulture) + "s managedMB=" + (GC.GetTotalMemory(false) >> 20) + " monoHeapMB=" + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20));
            closePhase = 20;
            closeAt = Time.unscaledTime + 0.5f;
            return false;
        }

        if (closePhase == 20)
        {
            if (Time.unscaledTime < closeAt)
            { return false; }

            float took = Time.unscaledTime - closeStarted;
            string texts = MenuTexts();
            closeLog.WriteLine("save finished after " + took.ToString("F1", CultureInfo.InvariantCulture) + "s: " + texts);
            CloseCheck(GameObject.Find("Area :: Saving Indicator") == null, "corner indicator disappears when the save is done");
            CloseCheck(!texts.Contains("저장 실패"), "no failure notice after a successful save");
            CloseCheck(texts.Contains("오늘 시장"), "settlement page shows the one-line market summary");
            bool exists = File.Exists(SessionStore.PathName);
            CloseCheck(exists, "binary checkpoint file exists");
            if (exists)
            {
                byte[] head = new byte[8];
                using (var stream = File.OpenRead(SessionStore.PathName))
                {
                    stream.Read(head, 0, 8);
                    closeLog.WriteLine("file bytes=" + stream.Length + " head=" + BitConverter.ToString(head));
                }

                CloseCheck(head[0] == (byte)'D' && head[1] == (byte)'D' && head[7] == 0, "file starts with the binary header, not JSON text");
            }

            CloseCheck(!File.Exists(SessionStore.PathName + ".tmp"), "no temporary file left behind");
            CaptureMenu("report-saved");
            closeLog.WriteLine("after save: managedMB=" + (GC.GetTotalMemory(false) >> 20) + " monoHeapMB=" + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + " unityReservedMB=" + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() >> 20));
            closePhase = 3;
            closeAt = Time.unscaledTime + 1f;
            return false;
        }

        if (closePhase == 3)
        {
            if (Time.unscaledTime < closeAt)
            { return false; }

            // 저장 직후 같은 프로세스에서 복원까지 하면 메모리 한도를 넘는 일이 있다. 복원 시험은 요청한 실행에서만 한다.
            if (Environment.GetEnvironmentVariable("DONDON_VERIFY_RESTORE") != "1")
            {
                closeLog.WriteLine("restore check skipped (set DONDON_VERIFY_RESTORE=1 to run it)");
                closePhase = 4;
                return true;
            }

            // 저장 직후 값을 기억하고, 세계를 일부러 바꾼 뒤 파일에서 복원해 되돌아오는지 본다(검증용 주입).
            var player = manager.playerAccount;
            var stock = manager.stocks[0];
            savedCash = player.cash;
            savedPrice = stock.currentPrice;
            savedIssued = stock.issuedShares;
            savedDay = day.Day;
            savedUnlock = PlayerProgression.Has(PlayerProgression.CrowdValueUnlock);
            savedBelief = 0;
            foreach (var bot in day.Bots)
            {
                if (bot != null && bot.Social != null && !bot.KnowsRealValue && bot.Social.Psychology.Companies.TryBelief(stock.symbol, out double belief))
                {
                    savedBelief = belief;
                    savedBotId = bot.Social.Id;
                    break;
                }
            }

            string snapshot = SessionStore.Read();
            CloseCheck(snapshot.StartsWith("@dondon-binary:"), "read returns the binary checkpoint");
            player.cash += 12345m;
            stock.currentPrice += 1000m;
            foreach (var bot in day.Bots)
            {
                if (bot != null && bot.Social != null && bot.Social.Id == savedBotId)
                {
                    bot.Social.Psychology.Companies.ShiftBelief(stock.symbol, 0.25, SessionClock.Time);
                }
            }

            long before = GC.GetTotalMemory(false) >> 20;
            SessionStore.Restore(snapshot);
            closeLog.WriteLine("restore managedMB before=" + before + " after=" + (GC.GetTotalMemory(false) >> 20));
            manager = StockManager.Instance;
            day = MarketDayController.Instance;
            stock = manager.stocks[0];
            CloseCheck(manager.playerAccount.cash == savedCash, "player cash restored exactly");
            CloseCheck(stock.currentPrice == savedPrice && stock.issuedShares == savedIssued, "price and issued shares restored");
            CloseCheck(day.Day == savedDay && day.ReportPending, "day and report state restored");
            CloseCheck(PlayerProgression.Has(PlayerProgression.CrowdValueUnlock) == savedUnlock, "unlock state restored");
            double restoredBelief = -1;
            foreach (var bot in day.Bots)
            {
                if (bot != null && bot.Social != null && bot.Social.Id == savedBotId)
                {
                    bot.Social.Psychology.Companies.TryBelief(stock.symbol, out restoredBelief);
                }
            }

            CloseCheck(savedBelief > 0 && Math.Abs(restoredBelief - savedBelief) < 0.0001, "bot belief restored (" + savedBelief.ToString("F2", CultureInfo.InvariantCulture) + ")");
            CloseCheck(!day.IntegrityFailed, "integrity flag clean after restore");
            closePhase = 4;
            return true;
        }

        return false;
    }

    private static void WriteBots(MarketDayController day, string name)
    {
        using var writer = Writer(name);
        writer.WriteLine("id,kind,active,cash,equity,skill,skillCap,lessons,fear,greed,nickname");
        foreach (var bot in day.Bots)
        {
            if (bot == null || bot.Social == null)
            { continue; }

            var mind = bot.Social.Psychology;
            writer.WriteLine(bot.Social.Id + "," + bot.GetType().Name + "," + bot.isActiveAndEnabled + "," + N(bot.cash) + "," + N(bot.Equity()) + "," +
                mind.ChartSkill.ToString("F3", CultureInfo.InvariantCulture) + "," + mind.ChartSkillCap.ToString("F3", CultureInfo.InvariantCulture) + "," + mind.ChartLessons + "," +
                mind.Fear.ToString("F0", CultureInfo.InvariantCulture) + "," + mind.Greed.ToString("F0", CultureInfo.InvariantCulture) + "," + bot.Social.Nickname);
        }
    }

    private static void Finish(string outcome)
    {
        if (finishing)
        { return; }

        finishing = true;
        try
        {
            var day = MarketDayController.Instance;
            if (day != null && day.Ready)
            {
                WriteBots(day, "bots-end.csv");
                if (unlockStarted)
                {
                    var chart = UnityEngine.Object.FindAnyObjectByType<ChartManager>();
                    unlockLog.WriteLine("end: crowdUnlocked=" + PlayerProgression.Has(PlayerProgression.CrowdValueUnlock) + " chartCrowdText='" + (chart != null && chart.textFakeValue != null ? chart.textFakeValue.text : "<none>") +
                        "' chartRealText='" + (chart != null && chart.textRealValue != null ? chart.textRealValue.text : "<none>") + "'");
                }
            }

            using (var summary = Writer("summary.txt"))
            {
                summary.WriteLine("outcome=" + outcome);
                summary.WriteLine(string.IsNullOrEmpty(accumulateSymbol) ? "mode=actual TradeScene; no price/emotion/order injection; one player unlock purchase only" : "mode=INJECTED player accumulation of " + accumulateSymbol + " (cash granted, real orders at real asks)");
                summary.WriteLine("errors=" + errors + " frames=" + frames + " timeScale=" + Time.timeScale + " observedSeconds=" + (readyAt < 0 ? 0 : Time.time - readyAt).ToString("F1", CultureInfo.InvariantCulture));
                summary.WriteLine("wallSeconds=" + (EditorApplication.timeSinceStartup - began).ToString("F1", CultureInfo.InvariantCulture));
                summary.WriteLine("bots=" + (day == null ? 0 : day.Bots.Count) + " integrityFailed=" + (day != null && day.IntegrityFailed));
                summary.WriteLine(PerfLine(day));
                if (InvestorSociety.Instance != null)
                {
                    var chatter = new StringBuilder("chatterboxes=");
                    foreach (var talker in InvestorSociety.Instance.Chatterboxes)
                    {
                        chatter.Append(talker.Social.Id).Append(':').Append(talker.Social.Nickname).Append(":room").Append(talker.Social.Psychology.Room).Append(' ');
                    }

                    summary.WriteLine(chatter.ToString());
                }
                summary.WriteLine("day=" + (day == null ? 0 : day.Day) + " phase=" + (day == null ? "" : day.Phase.ToString()) + " gameMinute=" + (day == null ? 0 : day.GameMinute) + " reportPending=" + (day != null && day.ReportPending));
                summary.WriteLine("panicStarts=" + panicStarts + " fomoStarts=" + fomoStarts + " rhythmOrders=" + (MarketRhythmDirector.Instance == null ? 0 : MarketRhythmDirector.Instance.DirectedOrders));
                foreach (var pair in tallies)
                {
                    var stock = pair.Key;
                    var tally = pair.Value;
                    summary.WriteLine(stock.symbol + " issued=" + N(stock.issuedShares) + " open=" + N(tally.open) + " last=" + N(stock.currentPrice) + " low=" + N(tally.low) + " high=" + N(tally.high) +
                        " real=" + N(stock.realValue) + " crowd=" + N(stock.fakeValue) + " fills=" + tally.totalFills + " volume=" + N(tally.totalVolume) + " turnover=" + N(tally.totalTurnover) +
                        " whaleSoldAtOffer=" + N(tally.whaleOfferVolume) + " whaleSoldAtBid=" + N(tally.whaleHitVolume) + " whaleBought=" + N(tally.whaleBought) +
                        " ownerHolding=" + N(stock.Owner == null ? 0m : stock.Owner.GetOwnedVolume(stock)));
                }

                summary.WriteLine("-- buyers");
                foreach (var pair in buyerKinds)
                {
                    summary.WriteLine(pair.Key + "=" + pair.Value);
                }

                summary.WriteLine("-- sellers");
                foreach (var pair in sellerKinds)
                {
                    summary.WriteLine(pair.Key + "=" + pair.Value);
                }
            }
        }
        catch (Exception exception)
        {
            errors++;
            errorLog?.WriteLine(exception.ToString());
        }

        samples?.Dispose();
        errorLog?.Dispose();
        unlockLog?.Dispose();
        memoryLog?.Dispose();
        closeLog?.Dispose();
        whaleLog?.Dispose();
        if (cardLog != null)
        {
            cardLog.WriteLine("total: holdingGain=" + (cardHolding - cardHoldingLoss) + " holdingLoss=" + cardHoldingLoss + " realizedGain=" + cardGain + " realizedLoss=" + cardLoss + " messagesStatingMoney=" + spokenMoney + " screenshots=" + cardShots);
            cardLog.Dispose();
        }

        chatLog?.Dispose();
        if (replyLog != null)
        {
            replyLog.WriteLine("total: addressed=" + addressed + " withParent=" + addressedWithParent + " sameStock=" + sameSymbol + " noParent=" + addressedNoParent +
                " sourceDoubt=" + sourceDoubt + " sourceDoubtWithNews=" + sourceDoubtWithNews + " brags=" + brags + " saleTalk=" + saleTalk +
                " smallestBrag=" + (smallestBrag == decimal.MaxValue ? "none" : N(smallestBrag)) + " | " + smallestBragText);
            replyLog.Dispose();
        }

        if (cardSociety != null)
        {
            cardSociety.SocialMessagePublished -= OnCardMessage;
        }
        foreach (var pair in tallies)
        {
            pair.Key.OnMatchedTrade -= OnTrade;
        }

        if (journal != null)
        {
            journal.Happened -= OnEvent;
        }

        if (languageChanged)
        {
            if (hadLanguage)
            {
                PlayerPrefs.SetString("DonDon.Language", languageBefore);
            }
            else
            {
                PlayerPrefs.DeleteKey("DonDon.Language");
            }

            PlayerPrefs.Save();
        }

        EditorSettings.enterPlayModeOptionsEnabled = oldOptions;
        EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Monitor;
        Application.logMessageReceived -= Log;
        EditorApplication.Exit(errors == 0 && outcome == "observation completed" ? 0 : 1);
    }
}

[DefaultExecutionOrder(-32000)]
public sealed class MarketBeliefProbe : MonoBehaviour
{
    private void Awake()
    {
        Application.runInBackground = true;
        // 기본은 정상 속도다. DONDON_TIME_SCALE(1~4)을 주면 그만큼 빨리 돌린다: 한 프레임의 게임 시간은 그대로 두고(20fps의 0.05초) 프레임을 더 자주 돈다.
        string wanted = System.Environment.GetEnvironmentVariable("DONDON_TIME_SCALE");
        if (!float.TryParse(wanted, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale)) { scale = 1f; }
        scale = Mathf.Clamp(scale, 1f, 4f);
        // DONDON_PERF=1이면 프레임 제한을 풀고 정상 속도로 돌리면서 프레임 시간을 잰다.
        MarketBeliefValidation.Perf = System.Environment.GetEnvironmentVariable("DONDON_PERF") == "1";
        if (MarketBeliefValidation.Perf) { scale = 1f; }
        Application.targetFrameRate = MarketBeliefValidation.Perf ? -1 : Mathf.RoundToInt(20 * scale);
        QualitySettings.vSyncCount = 0;
        Time.timeScale = scale;
        Time.captureDeltaTime = 0;
    }
    private float scale = 1f;

    private void Update()
    {
        // 게임이 스스로 속도를 1로 되돌리면(메뉴를 닫을 때 등) 다시 맞춘다. 멈춘 상태(0)는 건드리지 않는다.
        if (scale > 1f && Mathf.Approximately(Time.timeScale, 1f)) { Time.timeScale = scale; }
        MarketBeliefValidation.FrameSample(Time.unscaledDeltaTime);
        MarketBeliefValidation.Step();
    }
}
