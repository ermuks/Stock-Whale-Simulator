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
    public Stock FindStock(string id) => id != null && stockById.TryGetValue(id, out var stock) ? stock : null;
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
            var stock = new Stock(definition.stockId, definition.name, PriceTickPolicy.RoundToTick(Math.Max(1m, definition.startPrice)), definition.issuedShares);
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
            preview=new PlayerSetupPreview(name,rules.founderShares,0,rules.founderValue/(decimal)rules.founderShares,0,economy.retailStartingCash,
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
            reason = $"거래소 현금 부족: {funding - exchangeCash:N0}원 추가 필요";
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
                decimal transfer = playerAccount.cash - economy.retailStartingCash;
                playerAccount.ApplyTradeCash(-transfer);
                exchange.ApplyTradeCash(transfer);
                // Founder keeps all private shares. Only liquid personal starting cash is normalized.
            }

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

        foreach (var participant in participants)
        {
            decimal quantity = decimal.Floor(pool * participant.weight / weightSum);
            decimal basis = stock.currentPrice * (decimal)(economy.initialBasisMin + rng.NextDouble() * Math.Max(0f, economy.initialBasisMax - economy.initialBasisMin));
            ExchangeBot.Instance.TransferInventory(participant.bot, stock, quantity, basis);
        }

        participants.Clear();
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
        CandleHistoryManager.Instance?.RegisterStock(stock);
        StockInterestManager.Instance?.RegisterStock(stock);
        StockStatistics.Instance?.RegisterStock(stock);
        PsychologicalLevelManager.Instance?.RegisterStock(stock);
        ItCompanyManager.Instance?.RegisterStock(stock);
        OnStockListed?.Invoke(stock);
        return stock;
    }
}
