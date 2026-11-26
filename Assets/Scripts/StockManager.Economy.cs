using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public partial class StockManager
{
    [Header("초기 경제·소유주·뉴스·위험 설정")]
    public bool useInitialEconomy;
    public EconomySettings economy = new();
    public Trader playerAccount;
    public bool EconomyInitialized { get; private set; }
    public bool SetupInProgress { get; private set; }
    public bool SetupConfirmed => !useInitialEconomy || EconomyInitialized;
    public decimal PlayerStartingEquity { get; private set; }

    public readonly List<Stock> UnlistedStocks = new();
    [Serializable]
    public class InitialAssetDebug
    {
        public string stockId, ownerId;
        public bool listed;
        public double issued, ownerShares, ownerCash, exchangeShares, generalShares;
    }

    public List<InitialAssetDebug> initialAssets = new();
    private readonly Dictionary<string, Stock> stockById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Trader> ownersById = new(StringComparer.Ordinal);
    private readonly HashSet<Stock> distributedStocks = new();
    private System.Random initialRandom;
    private readonly List<(TradeBot bot, decimal weight)> participants = new(2000);
    private static readonly ProfilerMarker InitialMarker = new("DonDon.InitialEconomy");
    public static event Action OnInitialEconomyReady;
    /// <summary>이름이 바뀐 회사의 지금 이름. 씬과 예전 저장에는 예전 이름이 남아 있어서, 종목을 만들 때와 저장을 이을 때 이 표를 거친다.</summary>
    public static string CurrentName(string name) => name == "국민" + "금융" ? "굿데이금융" : name == "대만" + "디스플레이" ? "LC디스플레이" : name;
    public Stock FindStock(string id) => id != null && stockById.TryGetValue(id, out var stock) ? stock : null;
    /// <summary>비상장기업을 더 등록할 자리가 있는가.</summary>
    public bool PrivateRoom => stockById.Count<64;
    public Stock RegisterPrivateCompany(string id,string name,decimal price,decimal shares)
    {
        var existing=FindStock(id); if (existing!=null) { return existing; }
        if (string.IsNullOrWhiteSpace(id) || stockById.Count>=64 || shares<1 || price<=0) { throw new InvalidOperationException("Invalid private company."); }
        var stock=new Stock(id,name,price,shares) { IsListed=false };
        stockById.Add(id,stock); UnlistedStocks.Add(stock); return stock;
    }
    public Trader FindOwner(string id) => id != null && ownersById.TryGetValue(id, out var owner) ? owner : null;
    public void ReconnectListedCompanies()
    {
        foreach (var stock in stocks)
        {
            CandleHistoryManager.Instance?.RegisterStock(stock);
            StockInterestManager.Instance?.RegisterStock(stock);
            StockStatistics.Instance?.RegisterStock(stock);
            OnStockListed?.Invoke(stock);
        }
    }
    /// <summary>시작 가격을 정해 둔 값에서 위아래로 이만큼까지 흔든다.</summary>
    public const double StartPriceSpread = 0.12;
    /// <summary>
    /// 종목의 시작 가격. 정해 둔 값(500, 1,000, 10,000…)은 딱 떨어져서 막 시작한 장처럼 보인다.
    /// 한참 거래돼 온 종목처럼 보이게 ±12% 안에서 흔들고 호가 단위에 맞춘다(예: 10,000 → 10,870).
    /// 같은 판에서는 언제나 같은 값이다(게임의 고정 시드와 종목 코드로 정한다). 정해 둔 값 자체는 고치지 않는다.
    /// </summary>
    public decimal MessyStartPrice(OwnerCompanyDefinition definition)
    {
        decimal planned = Math.Max(1m, definition.startPrice);
        uint mixed = unchecked((uint)CandleHistoryManager.StableSeed(economy.seed, "START:" + definition.stockId));
        unchecked { mixed ^= mixed >> 16; mixed *= 0x85EBCA6Bu; mixed ^= mixed >> 13; mixed *= 0xC2B2AE35u; mixed ^= mixed >> 16; }
        double share = mixed / (double)uint.MaxValue * 2d - 1d;
        decimal price = PriceTickPolicy.RoundToTick(planned * (decimal)(1d + share * StartPriceSpread));
        // 어쩌다 다시 딱 떨어지는 값이 나오면(끝자리가 0으로만 이어지면) 호가 몇 칸을 옮긴다.
        decimal round = 1m; while (planned / round >= 100m) { round *= 10m; }
        if (price % round == 0m) { decimal tick = PriceTickPolicy.GetTickSize(price); price = PriceTickPolicy.RoundToTick(price + tick * (3 + mixed % 5)); }
        return Math.Max(1m, price);
    }
    public void BuildEconomyStocks()
    {
        initialRandom = new System.Random(economy.seed);
        var businessRules = CorporateEconomy.LoadRules();
        var growthRules = CorporateEconomy.LoadGrowthRules();
        foreach (var definition in economy.companies)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.stockId) || stockById.ContainsKey(definition.stockId))
            { continue; }

            // New-world entry target only. Restored Stock instances retain their saved issued shares.
            if (definition.stockId == businessRules.entryCompany && businessRules.entryIssuedShares > 0)
            { definition.issuedShares = businessRules.entryIssuedShares; }
            definition.name = CurrentName(definition.name);
            var stock = new Stock(definition.stockId, definition.name, MessyStartPrice(definition), definition.issuedShares);
            stock.OwnerId = definition.ownerId;
            stock.IsListed = definition.initiallyListed;
            if (definition==economy.companies[0] && economy.retailPlayerStart)
            {
                stock.issuedShares=growthRules.founderShares;
                stock.currentPrice=stock.realValue=stock.fakeValue=growthRules.founderValue/(decimal)growthRules.founderShares;
                stock.orderBook=new OrderBook(stock.currentPrice); stock.IsListed=false;
            }
            stockById.Add(stock.symbol, stock);
            if (stock.IsListed)
            {
                stocks.Add(stock);
            }
            else
            {
                UnlistedStocks.Add(stock);
            }
        }

        CurrentStock = stocks.Count > 0 ? stocks[0] : null;
    }

    public void EnsureOwnerAccounts()
    {
        if (!useInitialEconomy || ownersById.Count > 0)
        { return; }

        if (playerAccount == null)
        { return; }

        for (int i = 0; i < economy.companies.Length; i++)
        {
            var definition = economy.companies[i];
            var stock = FindStock(definition.stockId);
            if (stock == null)
            { continue; }

            Trader owner = i == 0 ? playerAccount : TradeBot.Create<WhaleBot>(definition.ownerId);
            if (owner == null)
            {
                throw new InvalidOperationException("대장 생성 전에 봇 한도 예약이 필요합니다.");
            }

            ownersById.Add(definition.ownerId, owner);
            stock.Owner = owner;
            owner.OwnerId = definition.ownerId;
            owner.OwnedCompany = stock;
            if (owner is WhaleBot whale)
            {
                whale.BindCompany(stock, definition.ownerId);
            }
        }
    }

    public readonly struct PlayerSetupPreview
    {
        public readonly string Name;
        public readonly long Shares, PublicShares, OwnShares;
        public readonly decimal Price, MarketCap, Funding, FinalCash, ExchangeCash;
        public PlayerSetupPreview(string name, long shares, long publicShares, decimal price, decimal funding, decimal finalCash, decimal exchangeCash)
        {
            Name = name;
            Shares = shares;
            PublicShares = publicShares;
            OwnShares = shares - publicShares;
            Price = price;
            MarketCap = price * shares;
            Funding = funding;
            FinalCash = finalCash;
            ExchangeCash = exchangeCash;
        }
    }

    public bool PreviewPlayer(string name, string sharesInput, string publicInput, out PlayerSetupPreview preview, out string reason)
    {
        preview = default;
        reason = "";
        if (EconomyInitialized || SetupInProgress)
        {
            reason = "이미 확정한 초기 설정입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length < economy.minimumNameLength || name.Length > economy.maximumNameLength || name.Contains("<") || name.Contains(">") || name.IndexOfAny(new[] { '\n', '\r', '\t' }) >= 0)
        {
            reason = "종목명은 앞뒤 공백·태그 없이 2~20자로 입력하세요.";
            return false;
        }

        foreach (var definition in economy.companies)
        {
            if (definition.stockId != economy.companies[0].stockId && string.Equals(definition.name, name, StringComparison.OrdinalIgnoreCase))
            {
                reason = "이미 사용 중인 종목 이름입니다.";
                return false;
            }
        }

        if (economy.retailPlayerStart)
        {
            var rules=CorporateEconomy.LoadGrowthRules(); decimal cap=0;
            foreach (var item in stocks) { cap+=item.currentPrice*item.issuedShares; }
            preview=new PlayerSetupPreview(name,rules.founderShares,0,rules.founderValue/(decimal)rules.founderShares,0,PlayerStartCash,
                economy.marketCapExchangeCash ? cap*(decimal)economy.exchangeCashRatio : economy.directExchangeCash);
            return true;
        }

        if (!long.TryParse(sharesInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long shares) || shares < economy.minimumShares || shares >= economy.maximumSharesExclusive)
        {
            reason = "발행량은 1,000,000 이상 1,000,000,000 미만 정수입니다.";
            return false;
        }

        if (!int.TryParse(publicInput, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int percentage) || percentage < 10 || percentage > 90)
        {
            reason = "공개 비율은 10~90% 정수입니다.";
            return false;
        }

        decimal price = PriceTickPolicy.FloorToTick(economy.playerTargetMarketCap / (decimal)shares);
        if (price <= 0m)
        {
            reason = "최소 단가보다 작은 설정입니다.";
            return false;
        }

        long publicShares = (long)decimal.Floor(shares * percentage / 100m);
        decimal funding = publicShares * price, marketCap = price * shares;
        foreach (var definition in economy.companies)
        {
            if (definition.initiallyListed && definition != economy.companies[0])
            {
                marketCap += (decimal)definition.startPrice * definition.issuedShares;
            }
        }

        decimal exchangeCash = economy.marketCapExchangeCash ? marketCap * (decimal)Mathf.Clamp01(economy.exchangeCashRatio) : Math.Max(0L, economy.directExchangeCash);
        preview = new PlayerSetupPreview(name, shares, publicShares, price, funding, economy.playerBaseCash + funding, exchangeCash);
        if (exchangeCash < funding)
        {
            reason = $"거래소 현금 부족: {funding - exchangeCash:N0}돈 추가 필요";
            return false;
        }

        return true;
    }

    public bool ConfirmPlayer(string name, string shares, string percentage, out string reason)
    {
        if (economy.retailPlayerStart && DailyIncomeController.Instance!=null) { name=DailyIncomeController.Instance.CompanyName; }
        if (!PreviewPlayer(name, shares, percentage, out var preview, out reason))
        { return false; }

        EnsureOwnerAccounts();
        if (playerAccount == null || ownersById.Count != economy.companies.Length || ExchangeBot.Instance == null || ExchangeWarehouse.Instance == null)
        {
            reason = "초기 계좌 연결이 준비되지 않았습니다.";
            return false;
        }

        SetupInProgress = true;
        StartCoroutine(InitializeEconomy(preview));
        return true;
    }

    private IEnumerator InitializeEconomy(PlayerSetupPreview preview)
    {
        yield return null;
        yield return null;
        while (MarketDayController.Initializers > 0)
        {
            yield return null;
        }

        using (InitialMarker.Auto())
        {
            var playerStock = playerAccount.OwnedCompany;
            playerStock.stockName = preview.Name;
            playerStock.issuedShares = preview.Shares;
            playerStock.currentPrice = playerStock.realValue = playerStock.fakeValue = preview.Price;
            playerStock.orderBook = new OrderBook(preview.Price);
            if (playerStock.IsListed) { StockStatistics.Instance?.ResetInitialStock(playerStock); }
            for (int i = 0; i < economy.companies.Length; i++)
            {
                var definition = economy.companies[i];
                var stock = FindStock(definition.stockId);
                var owner = stock.Owner;
                owner.cash = i == 0 ? economy.playerBaseCash : definition.initialCash;
                owner.InitialCashAssigned = true;
                owner.GrantShares(stock, i == 0 ? stock.issuedShares : decimal.Floor(stock.issuedShares * (decimal)Mathf.Clamp01(definition.ownerRatio)), stock.currentPrice);
                ExchangeWarehouse.Instance.InitializeStock(stock);
            }

            foreach (var trader in Trader.Registry)
            {
                if (trader is TradeBot bot)
                {
                    AssignInitialCash(bot);
                }
            }

            var exchange = ExchangeBot.Instance;
            exchange.useMarketCapCash = economy.marketCapExchangeCash;
            exchange.initialCashMarketCapRatio = economy.exchangeCashRatio;
            exchange.initialCashAmount = economy.directExchangeCash;
            exchange.InitializeAssets();
            exchange.ApplyTradeCash(-preview.Funding);
            playerAccount.ApplyTradeCash(preview.Funding);
            playerAccount.ForceRemoveHolding(playerStock, preview.PublicShares);
            exchange.GrantShares(playerStock, preview.PublicShares, preview.Price);
            foreach (var stock in UnlistedStocks)
            {
                exchange.GrantShares(stock, ExchangeWarehouse.Instance.TakeRemaining(stock), stock.currentPrice);
            }

            foreach (var stock in stocks)
            {
                DistributePublicShares(stock);
                exchange.ResetInitialInventory(stock);
            }

            if (economy.retailPlayerStart)
            {
                decimal transfer = playerAccount.cash - PlayerStartCash - StartFoundingCash;
                playerAccount.ApplyTradeCash(-transfer);
                exchange.ApplyTradeCash(transfer);
                // Founder keeps all private shares. Only liquid personal starting cash is normalized.
            }

            CalibrateRetailExposure();
            EconomyInitialized = true;
            SetupInProgress = false;
            PlayerStartingEquity = playerAccount.Equity();
            initialAssets.Clear();
            foreach (var definition in economy.companies)
            {
                var stock = FindStock(definition.stockId);
                decimal general = 0m;
                foreach (var trader in Trader.Registry)
                {
                    if (!trader.IsCompanyOwner && !trader.IsExchangeParticipant)
                    {
                        general += trader.GetOwnedVolume(stock);
                    }
                }

                initialAssets.Add(new InitialAssetDebug { stockId = stock.symbol, ownerId = stock.OwnerId, listed = stock.IsListed, issued = (double)stock.issuedShares, ownerShares = (double)stock.Owner.GetOwnedVolume(stock), ownerCash = (double)stock.Owner.cash, exchangeShares = (double)exchange.GetOwnedVolume(stock), generalShares = (double)general });
            }

            foreach (var trader in Trader.Registry)
            {
                trader.BeginDailyBaseline();
            }

            foreach (var stock in stocks)
            {
                ItCompanyManager.Instance?.RegisterStock(stock);
                OnStockListed?.Invoke(stock);
            }

            OnInitialEconomyReady?.Invoke();
        }
    }

    /// <summary>
    /// 개미가 자산 중 주식에 두려는 비중에 곱하는 값. 게임을 시작할 때 개미 전체가 실제로 들고 있는 주식 비중에 맞춘다.
    /// 현금이 많아져도 개장하자마자 전 종목을 사들이지 않게 하기 위해서다. 늘어난 돈은 평소에는 쉬고, 믿음이 기울거나 추격할 때 나온다.
    /// 예전 저장에는 이 값이 없어 1로 읽힌다(예전과 같은 동작).
    /// </summary>
    public float retailExposureScale = 1f;
    private void CalibrateRetailExposure()
    {
        decimal held = 0m, equity = 0m;
        foreach (var trader in Trader.Registry)
        {
            if (!(trader is TradeBot bot) || bot.IsExchangeParticipant || bot.IsCompanyOwner) { continue; }
            decimal value = 0m;
            foreach (var stock in stocks) { value += bot.GetOwnedVolume(stock) * stock.currentPrice; }
            held += value; equity += value + Math.Max(0m, bot.cash);
        }
        if (held <= 0m || equity <= 0m) { return; }
        // 성향 평균(0.5)에 여유(1.15)를 곱한 0.575가 기준 비중이다.
        // 주식을 하나도 안 받은 사람도 이제는 살 돈이 있어서, 딱 맞추면 개장 직후 사는 쪽으로 쏠린다. 그만큼(20%) 낮춰 잡는다.
        retailExposureScale = Mathf.Clamp((float)(held / equity) / .575f * .8f, .01f, 1f);
        Debug.Log($"[개미 자금] 현금 {equity - held:N0}돈 · 보유 주식 {held:N0}돈 · 주식 비중 {held / equity * 100m:0.0}% · 목표 비중 배율 {retailExposureScale:0.000}");
    }

    /// <summary>
    /// 개미의 하루 벌이(바깥에서 들어오는 돈: 월급, 용돈, 다른 데서 번 돈). 장이 열릴 때마다 개미 한 명이 이 범위에서 무작위로 받는다(1만 돈 단위).
    /// 평균 50만 돈이라 개미가 천 명이면 하루에 5억 돈쯤이 시장에 새로 들어온다. 처음 개미 현금 합계(약 1.1조)의 0.05%에 못 미쳐서 값을 밀어 올리지는 않고,
    /// 세력과 플레이어에게 돈을 잃은 개미가 빈손으로 남지 않게 한다. 개미가 가난하면 세력도 플레이어도 벌 돈이 없다.
    /// </summary>
    public const long RetailPayMin = 300_000L, RetailPayMax = 700_000L, RetailPayStep = 10_000L;
    /// <summary>개미인가: 거래소 참여자, 기업 소유주, 세력을 뺀 봇.</summary>
    public static bool IsRetail(TradeBot bot) => bot != null && !bot.IsExchangeParticipant && !bot.IsCompanyOwner && !(bot is WhaleBot);
    /// <summary>이 개미가 day일에 받는 돈. 같은 판의 같은 날, 같은 계좌는 언제나 같은 값이다(저장을 불러와도 달라지지 않는다).</summary>
    public long RetailPay(TradeBot bot, int day)
    {
        // 계좌 번호나 날짜가 1씩 다른 씨앗을 그대로 쓰면 첫 난수가 줄지어 나온다(어제 38만, 오늘 40만, 내일 42만…). 씨앗을 한 번 흩뜨려서 쓴다.
        uint mixed = unchecked((uint)CandleHistoryManager.StableSeed(economy.seed, "PAY:" + bot.InitialAccountId + ":" + day));
        unchecked { mixed ^= mixed >> 16; mixed *= 0x85EBCA6Bu; mixed ^= mixed >> 13; mixed *= 0xC2B2AE35u; mixed ^= mixed >> 16; }
        long steps = (RetailPayMax - RetailPayMin) / RetailPayStep;
        return RetailPayMin + mixed % (uint)(steps + 1) * RetailPayStep;
    }
    /// <summary>
    /// 개미 한 명에게 하루 벌이를 넣는다. 넣은 돈을 돌려준다(개미가 아니면 0).
    /// 바깥에서 들어온 돈으로 기록되므로 그날의 매매 수익에는 잡히지 않는다(돈을 받았다고 수익 자랑을 하지 않는다).
    /// 첫날은 주지 않는다: 처음 현금을 막 나눠 받은 날이다.
    /// </summary>
    public decimal PayRetail(TradeBot bot, int day)
    {
        if (!useInitialEconomy || day <= 1 || !IsRetail(bot) || !bot.InitialCashAssigned)
        { return 0m; }
        long pay = RetailPay(bot, day);
        bot.cash += pay;
        return pay;
    }

    public void AssignInitialCash(TradeBot bot)
    {
        if (!useInitialEconomy || bot == null || bot.InitialCashAssigned || bot.IsExchangeParticipant || bot.IsCompanyOwner)
        { return; }

        var rng = new System.Random(CandleHistoryManager.StableSeed(economy.seed, "CASH:" + bot.InitialAccountId));
        double tier = rng.NextDouble();
        long min = tier < economy.smallCashProbability ? economy.smallCashMin : tier < economy.smallCashProbability + economy.mediumCashProbability ? economy.mediumCashMin : economy.largeCashMin;
        long max = tier < economy.smallCashProbability ? economy.smallCashMax : tier < economy.smallCashProbability + economy.mediumCashProbability ? economy.mediumCashMax : economy.largeCashMax;
        bot.cash = min + (decimal)rng.NextDouble() * Math.Max(0L, max - min);
        bot.InitialCashAssigned = true;
        bot.InitializeRiskPreference(economy.seed);
    }

    /// <summary>대형주로 치는 주가. 이보다 비싸면 개미 대부분이 몇 주밖에 못 산다.</summary>
    public const decimal LargeCapPrice = 100_000m;
    private System.Random largeCapRandom;
    /// <summary>
    /// 대형주의 첫 물량이 어떻게 풀리는지: steady(조용), normal(보통), heavy(물량이 많이 풀려 첫날 크게 밀림). 대형주가 아니면 null.
    /// 판마다 실제 난수로 정한다(게임의 고정 시드를 쓰지 않는다). 창 없이 돌리는 검증 실행은 DONDON_LARGECAP_MOOD로 고정하고, 없으면 heavy다.
    /// </summary>
    private string LargeCapMood(Stock stock)
    {
        if (stock == null || stock.currentPrice < LargeCapPrice) { return null; }
        string forced = Environment.GetEnvironmentVariable("DONDON_LARGECAP_MOOD");
        if (!string.IsNullOrEmpty(forced)) { return forced; }
        if (Application.isBatchMode) { return "heavy"; }
        largeCapRandom ??= new System.Random(Guid.NewGuid().GetHashCode());
        double roll = largeCapRandom.NextDouble();
        return roll < .35 ? "steady" : roll < .70 ? "normal" : "heavy";
    }

    public void DistributePublicShares(Stock stock)
    {
        if (!distributedStocks.Add(stock))
        { return; }

        participants.Clear();
        decimal weightSum = 0m;
        var rng = new System.Random(CandleHistoryManager.StableSeed(economy.seed, "ALLOCATION:" + stock.symbol));
        foreach (var trader in Trader.Registry)
        {
            if (!(trader is TradeBot bot) || bot.IsExchangeParticipant || bot.IsCompanyOwner || rng.NextDouble() >= Mathf.Clamp01(economy.participationProbability))
            { continue; }

            decimal weight = (decimal)(economy.allocationWeightMin + rng.NextDouble() * Math.Max(0f, economy.allocationWeightMax - economy.allocationWeightMin));
            if (weight <= 0m)
            { continue; }

            participants.Add((bot, weight));
            weightSum += weight;
        }

        decimal pool = decimal.Floor(ExchangeBot.Instance.GetHolding(stock) * (decimal)Mathf.Clamp01(economy.publicDistributionRatio));
        if (weightSum <= 0m || pool <= 0m)
        { return; }

        // 한 주가 아주 비싼 대형주는 개미가 처음 받은 물량을 감당하지 못해 첫날 크게 밀린다.
        // 그 자체는 놔두되, 판마다 똑같이 무너지지 않도록 처음 풀리는 물량과 받는 사람을 판마다 달리한다.
        string mood = LargeCapMood(stock);
        if (mood == "steady")
        {
            // 조금만 풀리고, 현금이 많은 사람일수록 많이 받는다. 받은 물량을 버틸 수 있어 조용히 시작한다.
            pool = decimal.Floor(pool * .2m); weightSum = 0m;
            for (int i = 0; i < participants.Count; i++)
            {
                var entry = participants[i]; decimal weight = entry.weight * Math.Max(1m, entry.bot.cash) / 10_000_000m;
                participants[i] = (entry.bot, weight); weightSum += weight;
            }
        }
        else if (mood == "normal") { pool = decimal.Floor(pool * .5m); }
        if (mood != null) { Debug.Log("[상장 물량] " + stock.stockName + "(" + stock.symbol + ") " + mood + " · 개미 배분 " + pool.ToString("N0") + "주"); }
        if (weightSum <= 0m || pool <= 0m)
        { return; }

        foreach (var participant in participants)
        {
            decimal quantity = decimal.Floor(pool * participant.weight / weightSum);
            decimal basis = stock.currentPrice * (decimal)(economy.initialBasisMin + rng.NextDouble() * Math.Max(0f, economy.initialBasisMax - economy.initialBasisMin));
            ExchangeBot.Instance.TransferInventory(participant.bot, stock, quantity, basis);
        }

        participants.Clear();
    }

    /// <summary>
    /// 새 게임에서 플레이어가 쥐고 시작하는 현금. 처음부터 작전을 벌일 수 있을 만큼 준다(예전에는 5천만 돈으로 시작해 하나씩 열어 갔다).
    /// 내 회사를 세우는 돈(설립 비용)은 이와 별도로 얹어 주고, 시작하자마자 회사를 세우는 데 쓴다.
    /// </summary>
    public const long PlayerStartCash = 1_000_000_000L;
    private static long StartFoundingCash => PlayerProgression.Instance == null ? 0L : PlayerProgression.Instance.rules.companyFoundingCost;
    /// <summary>개미 한 명이 공모주에 넣는 돈의 위쪽 끝(쓸 수 있는 현금에 대한 비율).</summary>
    private const decimal SubscriptionCashShare = .25m;
    /// <summary>개미 손에 이만큼(거래소 보유분에 대한 비율)도 없으면 아직 물량이 풀리지 않은 종목으로 본다.</summary>
    private const decimal FloatMissingShare = .05m;
    /// <summary>
    /// 공모주 청약. 새로 상장한 종목의 공모 물량은 주관사(거래소)가 전부 떠안는다. 그대로 두면 주식을 가진 개미가 한 명도 없어서
    /// 파는 사람은 거래소의 듬성듬성한 첫 호가뿐이고, 몇 주만 사도 값이 몇 배로 뛰고는 아무도 거래하지 않는 종목이 된다.
    /// 그래서 처음 세계를 만들 때 기존 종목을 개미에게 나눠 주듯이(DistributePublicShares), 공모 물량의 일부를 개미에게 넘긴다.
    /// 다만 거저 주지 않는다: 개미는 공모가로 값을 치르고, 그 돈은 거래소로 간다(돈과 주식이 새로 생기지 않는다).
    ///
    /// 개미 손에 이미 물량이 풀려 있으면 아무 일도 하지 않는다. 그래서 여러 번 불러도 되고, 따로 저장할 것도 없다
    /// (이 기능이 없던 때에 상장해 물량이 묶여 있는 종목은 다음 개장 때 풀린다). 넘긴 주식 수를 돌려준다.
    /// </summary>
    public decimal SubscribePublicShares(Stock stock)
    {
        var exchange = ExchangeBot.Instance;
        if (!useInitialEconomy || !EconomyInitialized || stock == null || exchange == null || !stock.IsListed || stock.IsDelisting || stock.currentPrice < 1m)
        { return 0m; }

        // 공모가: 막 상장했으면 지금 값이 공모가다. 뒤늦게 푸는 경우에는 시장이 믿는 값보다 비싸게 넘기지 않는다.
        decimal price = PriceTickPolicy.FloorToTick(stock.fakeValue >= 1m ? Math.Min(stock.currentPrice, stock.fakeValue) : stock.currentPrice);
        decimal held = exchange.GetHolding(stock), pool = decimal.Floor(held * (decimal)Mathf.Clamp01(economy.publicDistributionRatio));
        if (price < 1m || pool < 1m) { return 0m; }

        participants.Clear();
        decimal weightSum = 0m, retailHeld = 0m;
        var rng = new System.Random(CandleHistoryManager.StableSeed(economy.seed, "SUBSCRIPTION:" + stock.symbol));
        foreach (var trader in Trader.Registry)
        {
            if (!(trader is TradeBot bot) || !IsRetail(bot)) { continue; }
            retailHeld += bot.GetOwnedVolume(stock);
            if (rng.NextDouble() >= Mathf.Clamp01(economy.participationProbability)) { continue; }
            decimal weight = (decimal)(economy.allocationWeightMin + rng.NextDouble() * Math.Max(0f, economy.allocationWeightMax - economy.allocationWeightMin));
            if (weight <= 0m) { continue; }
            participants.Add((bot, weight)); weightSum += weight;
        }
        if (retailHeld > held * FloatMissingShare || weightSum <= 0m) { participants.Clear(); return 0m; }

        decimal given = 0m, paid = 0m; int buyers = 0;
        foreach (var participant in participants)
        {
            // 한 사람 몫이 한두 주뿐일 때 소수점을 버리면 물량의 절반이 사라진다. 소수점만큼의 확률로 한 주를 더 준다.
            decimal share = pool * participant.weight / weightSum, quantity = decimal.Floor(share);
            if (rng.NextDouble() < (double)(share - quantity)) { quantity += 1m; }
            var bot = participant.bot;
            quantity = Math.Min(quantity, decimal.Floor(Math.Max(0m, bot.cash - bot.ReservedCash) * SubscriptionCashShare / price));
            if (quantity < 1m || !exchange.TransferInventory(bot, stock, quantity, price)) { continue; }
            decimal cost = quantity * price, mark = quantity * stock.currentPrice;
            bot.ApplyTradeCash(-cost); exchange.ApplyTradeCash(cost);
            // 주고받은 것은 거래다. 바깥에서 들어온 것으로 적힌 주식 값을 되돌려, 하루 손익에는 공모가와 시세의 차이만 남긴다.
            bot.ExternalFlow(-mark); exchange.ExternalFlow(mark);
            given += quantity; paid += cost; buyers++;
        }
        participants.Clear();
        if (given > 0m)
        {
            exchange.ResetInitialInventory(stock);
            Debug.Log($"[공모주] {stock.stockName}({stock.symbol}) 개미 {buyers:N0}명이 {given:N0}주 청약 · 공모가 {price:N0}돈 · 대금 {paid:N0}돈 · 거래소에 남은 물량 {exchange.GetHolding(stock):N0}주");
        }
        return given;
    }

    [ContextMenu("테스트: 다음 미상장 소유주 종목 상장")]
    public void ListNextOwnerCompany()
    {
        if (UnlistedStocks.Count > 0)
        {
            ListOwnerCompany(UnlistedStocks[0].symbol);
        }
    }

    public Stock ListOwnerCompany(string id)
    {
        var stock = FindStock(id);
        if (!EconomyInitialized || stock == null || stock.IsListed || stock.IsDelisting || stock.business?.growth==null || !stock.business.growth.ListingApproved || !UnlistedStocks.Remove(stock))
        { return null; }

        stock.IsListed = true;
        stock.IsNewListing = true;
        stocks.Add(stock);
        // Public shares were purchased by the underwriter with finite cash; subsequent trades use the book.
        // 주관사가 떠안은 공모 물량의 일부를 개미가 공모가로 청약한다(거래소가 첫 호가를 깔기 전에 한다).
        SubscribePublicShares(stock);
        CandleHistoryManager.Instance?.RegisterStock(stock);
        StockInterestManager.Instance?.RegisterStock(stock);
        StockStatistics.Instance?.RegisterStock(stock);
        PsychologicalLevelManager.Instance?.RegisterStock(stock);
        ItCompanyManager.Instance?.RegisterStock(stock);
        OnStockListed?.Invoke(stock);
        return stock;
    }
}
