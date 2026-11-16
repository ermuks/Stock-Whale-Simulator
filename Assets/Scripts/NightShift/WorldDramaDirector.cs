using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>실제 소유주 계좌의 손익, 맞거래 기억, 자발적 청산과 지분 경영. 가격을 연출용으로 덮어쓰지 않는다.</summary>
public sealed class WorldDramaDirector : MonoBehaviour
{
    public static WorldDramaDirector Instance { get; private set; }
    public sealed class Fund
    {
        public TradeBot Account;
        public decimal OpeningEquity, PeakEquity;
        public bool Reducing, Closed;
        public readonly Dictionary<Trader, float> Relationships = new();
        public Stock EncounterStock;
        public Trader Counterparty;
        public decimal EncounterPrice, EncounterVolume;
        public bool Bought;
        public float EvaluateAt, NextNotice;
        // 이성적 처분 계획: 처음 지분, 오늘 판 수량, 그 날짜.
        public decimal OpeningShares, SoldToday;
        public int SoldDay;
        // 비싸게 판 뒤 아직 되사지 않은 자기 회사 주식과 그때 받은 돈. 평균 매도가 = BuybackProceeds / BuybackShares.
        public decimal BuybackShares, BuybackProceeds;
    }
    public readonly List<Fund> Funds = new();
    private readonly Dictionary<Trader, Fund> byAccount = new();
    private readonly List<Stock> subscribed = new();
    private readonly Queue<string> headlines = new();
    private readonly List<(decimal price, decimal volume)> levels = new(1);
    private readonly List<Order> ownOrders = new();
    private readonly List<(decimal price, decimal volume)> depthLevels = new(5);
    private readonly Dictionary<Stock, float> boardCooldown = new();
    private readonly Dictionary<Stock, int> boardActions = new();
    private float nextUpdate;
    public string Journal { get; private set; } = "기관과 고래의 움직임을 기록하는 중입니다.";
    public string BoardResult { get; private set; } = "과반 지분을 확보한 기업에서 경영 결정을 내릴 수 있습니다.";
    public int MemoryCount { get; private set; }
    void Awake() { Instance = this; StockManager.OnStockListed += Subscribe; if (GetComponent<CorporateEconomy>() == null) { gameObject.AddComponent<CorporateEconomy>(); } }
    void Start()
    {
        if (StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks) { Subscribe(stock); }
    }
    private void Subscribe(Stock stock)
    {
        if (stock==null) { return; }
        if (!subscribed.Contains(stock)) { subscribed.Add(stock); }
        stock.OnMatchedTrade -= Matched; stock.OnMatchedTrade += Matched;
    }
    void Update()
    {
        if (!MarketDayController.CanTrade || SessionClock.Time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }
        nextUpdate = SessionClock.Time + 2f;
        if (Funds.Count == 0)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (!bot.IsCompanyOwner || bot.IsExchangeParticipant || bot.Social == null)
                { continue; }
                var fund = new Fund { Account = bot, OpeningEquity = bot.Equity(), PeakEquity = bot.Equity(),
                    OpeningShares = bot.OwnedCompany == null ? 0m : bot.GetOwnedVolume(bot.OwnedCompany) };
                Funds.Add(fund); byAccount[bot] = fund;
            }
        }
        foreach (var fund in Funds)
        {
            if (fund.Account == null || fund.Closed)
            { continue; }
            decimal equity = fund.Account.Equity();
            fund.PeakEquity = Math.Max(fund.PeakEquity, equity);
            if (!fund.Reducing && fund.PeakEquity > 0m && equity < fund.PeakEquity * .45m)
            {
                fund.Reducing = true;
                InvestorSociety.Instance?.Leave(fund.Account, "운용자산 축소");
                Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 고점 대비 운용자산 55% 이상 감소. 자산 축소에 돌입.");
            }
            if (fund.Counterparty != null && SessionClock.Time >= fund.EvaluateAt)
            { EvaluateMemory(fund); }
            if (fund.Reducing) { Liquidate(fund); }
        }
    }
    private void Matched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order order)
    {
        // 대표가 자기 회사 주식을 실제로 판 수량만 하루 처분 한도에 센다.
        if (seller is TradeBot owner && byAccount.TryGetValue(owner, out var selling) && stock == owner.OwnedCompany)
        {
            int today = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
            if (selling.SoldDay != today)
            {
                selling.SoldDay = today;
                selling.SoldToday = 0m;
            }

            selling.SoldToday += volume;
            selling.BuybackShares += volume;
            selling.BuybackProceeds += price * volume;
        }

        // 되산 만큼 되살 목록에서 지운다. 평균 매도가는 그대로 둔다.
        if (buyer is TradeBot rebuyer && byAccount.TryGetValue(rebuyer, out var buying) && stock == rebuyer.OwnedCompany && buying.BuybackShares > 0m)
        {
            decimal covered = Math.Min(volume, buying.BuybackShares);
            buying.BuybackProceeds -= buying.BuybackProceeds * covered / buying.BuybackShares;
            buying.BuybackShares -= covered;
            if (buying.BuybackShares < 1m)
            {
                buying.BuybackShares = 0m;
                buying.BuybackProceeds = 0m;
            }
        }

        if (buyer is Trader b && seller is Trader s)
        {
            Remember(b, s, stock, price, volume, true);
            Remember(s, b, stock, price, volume, false);
        }
    }
    private void Remember(Trader actor, Trader other, Stock stock, decimal price, decimal volume, bool bought)
    {
        if (!byAccount.TryGetValue(actor, out var fund) || (!PlayerAccounts.IsPlayer(other) && !byAccount.ContainsKey(other)) || fund.Counterparty != null)
        { return; }
        fund.Counterparty = other; fund.EncounterStock = stock; fund.EncounterPrice = price;
        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = SessionClock.Time + 20f;
    }
    private void EvaluateMemory(Fund fund)
    {
        decimal change = fund.EncounterStock.currentPrice / Math.Max(1m, fund.EncounterPrice) - 1m;
        float outcome = Mathf.Clamp((float)change * (fund.Bought ? 100f : -100f), -20f, 20f);
        fund.Relationships.TryGetValue(fund.Counterparty, out float score);
        fund.Relationships[fund.Counterparty] = Mathf.Clamp(score + outcome, -100f, 100f);
        MemoryCount++;
        if (Mathf.Abs(outcome) >= 4f && SessionClock.Time > fund.NextNotice)
        {
            string other = PlayerAccounts.IsPlayer(fund.Counterparty) ? "플레이어" : fund.Counterparty is TradeBot bot ? bot.Social.Nickname : "맞은편 계좌";
            MarketDayController.Instance.PublishParticipant(fund.Account, other + (outcome < 0f ? "와 거래한 뒤 흐름이 불리해졌네요. 다음에는 경계하겠습니다." : "와 거래한 뒤 흐름이 나쁘지 않았습니다. 다음 움직임도 지켜보죠."));
            fund.NextNotice = SessionClock.Time + 120f;
        }
        fund.Counterparty = null;
    }
    public bool CanFundBuy(TradeBot bot) => !byAccount.TryGetValue(bot, out var f) || !f.Reducing && !f.Closed;
    /// <summary>계좌마다 고정된 기질(0~1). 감정이 아니며 변하지 않는다. 계좌 번호에서 나오므로 저장하지 않는다.</summary>
    public static decimal Trait(Trader account, string name)
    { return account == null ? .5m : CompanyBusiness.Hash(name + ":" + account.InitialAccountId) % 100 / 100m; }

    /// <summary>
    /// 누군가 자기 회사를 모으고 있다는 경계(0~1). 플레이어의 현재 지분율과, 플레이어와 거래한 뒤 손해 본 기억에서 계산한다.
    /// 플레이어가 지분을 줄이면 함께 내려간다.
    /// </summary>
    public decimal Vigilance(TradeBot bot, Stock stock)
    {
        if (bot == null || stock == null || stock != bot.OwnedCompany || stock.issuedShares <= 0m || !byAccount.TryGetValue(bot, out var fund))
        { return 0m; }

        decimal owned = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(stock) : StockManager.Instance.playerAccount.GetOwnedVolume(stock);
        decimal share = owned / stock.issuedShares;
        decimal notice = .03m + Trait(bot, "NOTICE") * .05m;
        decimal result = share <= notice ? 0m : Math.Min(1m, (share - notice) / .25m);
        float relation = 0f;
        fund.Relationships.TryGetValue(StockManager.Instance.playerAccount, out relation);
        if (PlayerAccounts.Instance != null && fund.Relationships.TryGetValue(PlayerAccounts.Instance.automated, out float other))
        { relation += other; }
        if (relation < 0f && result > 0m)
        { result = Math.Min(1m, result + (decimal)Mathf.Clamp(-relation / 100f, 0f, 1f) * .3m); }
        return result;
    }

    /// <summary>궁지에 몰린 정도(0~1). 운용자산이 고점에서 20% 넘게 줄면 시작해 55%에서 최대가 된다(그 지점에서 기존 청산 로직이 시작된다).</summary>
    public decimal Pressure(Trader account)
    {
        if (account == null || !byAccount.TryGetValue(account, out var fund) || fund.PeakEquity <= 0m)
        { return 0m; }

        decimal drawdown = 1m - fund.Account.Equity() / fund.PeakEquity;
        return Math.Max(0m, Math.Min(1m, (drawdown - .2m) / .35m));
    }

    public Fund FundOf(Trader account)
    { return account != null && byAccount.TryGetValue(account, out var fund) ? fund : null; }

    /// <summary>
    /// 세력·종목 대표의 매매. 개미와 달리 감정으로 움직이지 않는다.
    /// 진짜 가치를 알고, 시장이 받아 줄 수 있는 양과 지금의 흐름을 보고, 자기 지분을 관리한다.
    /// - 광기로 오를 때: 일부를 털어 차익을 챙긴다. 호가를 치지 않고 위에 걸어 두며, 물량을 쏟아 스스로 값을 깨지 않는다.
    /// - 내려오면: 판 가격보다 충분히 싸졌을 때 판 만큼 되사 지분을 되돌린다.
    /// 기준선은 계좌별 기질(인내심·지분 집착·공격성), 플레이어의 매집에 대한 경계, 자금 압박으로 달라진다.
    /// </summary>
    public bool TryFundDecision(TradeBot bot, Stock stock)
    {
        if (!byAccount.TryGetValue(bot, out var fund) || bot is WhaleBot whale && whale.PreDisposal)
        { return false; }
        if (fund.Reducing || fund.Closed)
        { return true; }
        if (stock.currentPrice <= 0m)
        { return true; }

        bool own = stock == bot.OwnedCompany;
        decimal patience = Trait(bot, "PATIENCE"), attachment = Trait(bot, "CONTROL"), aggression = Trait(bot, "AGGRESSION");
        decimal vigilance = Vigilance(bot, stock), pressure = Pressure(bot);

        // 1) 가치 판단과 기준선.
        decimal value = stock.realValue > 0m ? stock.realValue : stock.currentPrice;
        decimal premium = stock.currentPrice / value - 1m;
        decimal sellAbove = .12m + patience * .2m, buyBelow = -.03m - patience * .05m;
        // 경계할수록 더 비싸야 판다. 압박받을수록 덜 비싸도 판다.
        sellAbove += vigilance * .4m;
        sellAbove -= pressure * Math.Max(0m, sellAbove - .02m);

        // 2) 흐름과 시장이 받아 줄 수 있는 양: 최근 1분의 방향, 최근 30초 거래량.
        decimal trend = 0m, recentVolume = 0m;
        var candles = CandleHistoryManager.Instance == null ? null : CandleHistoryManager.Instance.Completed(stock);
        if (candles != null && candles.Count >= 2)
        {
            int span = Math.Min(12, candles.Count);
            decimal first = candles[candles.Count - span].ClosePrice;
            if (first > 0m)
            { trend = stock.currentPrice / first - 1m; }
            int window = Math.Min(6, candles.Count);
            for (int i = candles.Count - window; i < candles.Count; i++)
            { recentVolume += (decimal)candles[i].volume; }
        }
        bool frenzy = trend > .04m, falling = trend < -.04m;
        decimal absorb = Math.Max(10m, recentVolume * (.08m + aggression * .1m));

        if (own && fund.OpeningShares <= 0m)
        { fund.OpeningShares = bot.GetOwnedVolume(stock); }

        decimal holding = bot.GetHolding(stock);
        bool sell = holding >= 1m && premium > sellAbove;
        // 떨어지는 중에는 던지지 않는다. 압박이 크면 그럴 여유가 없다.
        if (sell && falling && pressure < .5m && premium < sellAbove * 2m + .5m)
        { sell = false; }

        // 3) 되사기: 비싸게 판 자기 회사 주식을, 판 가격보다 충분히 싸지고 하락이 진정됐을 때 되산다.
        decimal averageSale = fund.BuybackShares >= 1m ? fund.BuybackProceeds / fund.BuybackShares : 0m;
        decimal discount = (.08m + patience * .07m) * (1m - vigilance * .7m);
        bool rebuy = !sell && own && averageSale > 0m && !frenzy && trend > -.02m && pressure < .3m
            && stock.currentPrice <= averageSale * (1m - discount) && bot.cash - bot.ReservedCash > stock.currentPrice;
        bool buy = !sell && !rebuy && premium < buyBelow && pressure < .6m && bot.cash - bot.ReservedCash > bot.Equity() * .03m;
        if (!sell && !rebuy && !buy)
        { return true; }

        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        foreach (var order in ownOrders)
        { if (SessionClock.Time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        if (ownOrders.Count >= 3)
        { return true; }

        if (sell)
        {
            holding = bot.GetHolding(stock);
            decimal sellable = holding;
            if (own && fund.OpeningShares > 0m)
            {
                // 지분 관리: 끝까지 지키는 몫과 하루 처분량. 경계하면 더 지키고, 압박받으면 덜 지킨다.
                int today = MarketDayController.Instance == null ? 0 : MarketDayController.Instance.Day;
                decimal soldToday = fund.SoldDay == today ? fund.SoldToday : 0m;
                decimal keep = Math.Min(.9m, .4m + attachment * .25m + vigilance * .25m) * (1m - pressure * .6m);
                decimal daily = fund.OpeningShares * (premium > 1m ? .12m : premium > .5m ? .08m : .05m) * (1m - vigilance * .7m) * (1m + pressure);
                sellable = Math.Min(sellable, Math.Min(Math.Max(0m, daily - soldToday), Math.Max(0m, bot.GetOwnedVolume(stock) - fund.OpeningShares * keep)));
            }

            decimal quantity = decimal.Floor(Math.Min(sellable, Math.Min(2500m, absorb)));
            if (quantity < 1m)
            { return true; }

            decimal price;
            if (frenzy)
            {
                // 광기 매수가 붙어 오르는 중: 매수 호가를 치지 않고 위에 걸어 둔다. 사는 쪽이 올라와서 가져가게 한다.
                quantity = decimal.Floor(Math.Max(1m, quantity * .5m));
                price = PriceTickPolicy.CeilToTick(stock.currentPrice * (1m + .01m + Math.Min(.03m, premium * .02m)));
            }
            else if (pressure < .5m && CompanyBusiness.Hash("WORK:" + bot.InitialAccountId + ":" + (int)(SessionClock.Time / 7f)) % 2 == 0)
            {
                // 잠잠할 때의 절반은 매도 호가에 줄을 선다. 사려는 쪽이 가져가면 값을 깎지 않고 판다.
                stock.orderBook.GetTopAsks(levels, 1);
                price = levels.Count > 0 && levels[0].price <= stock.currentPrice * 1.01m ? levels[0].price : PriceTickPolicy.OneTickAbove(stock.currentPrice);
            }
            else
            {
                // 나머지는 매수 호가에 판다. 가까운 다섯 단계 잔량의 절반까지만, 현재가의 1% 아래로는 내려가지 않는다.
                stock.orderBook.GetTopBids(depthLevels, 5);
                if (depthLevels.Count == 0)
                { return true; }
                decimal floor = stock.currentPrice * .99m, depth = 0m;
                price = depthLevels[0].price;
                foreach (var level in depthLevels)
                {
                    if (level.price < floor)
                    { break; }
                    depth += level.volume;
                    price = level.price;
                }
                quantity = decimal.Floor(Math.Min(quantity, Math.Max(1m, depth * .5m)));
            }

            // 평소에는 가치 아래로 팔지 않는다. 압박이 크면 그 선도 지키지 못한다.
            if (quantity >= 1m && (price > value || pressure > .7m))
            { bot.SellLimit(price, quantity, 0, stock); }
            return true;
        }

        if (rebuy)
        {
            // 보통은 매수 호가에 줄을 선다. 많이 싸졌거나 누가 모으고 있어 급하면 매도 호가를 가져간다.
            bool take = vigilance > .5m || stock.currentPrice <= averageSale * (1m - discount - .1m);
            if (take) { stock.orderBook.GetTopAsks(levels, 1); } else { stock.orderBook.GetTopBids(levels, 1); }
            decimal rebuyPrice = levels.Count > 0 ? levels[0].price : PriceTickPolicy.FloorToTick(stock.currentPrice * .995m);
            if (rebuyPrice < 1m || rebuyPrice > averageSale * (1m - discount))
            { return true; }
            decimal spend = Math.Min(Math.Max(0m, bot.cash - bot.ReservedCash) * .05m, fund.BuybackProceeds);
            decimal rebuyQuantity = decimal.Floor(Math.Min(fund.BuybackShares, Math.Min(2500m, Math.Min(absorb, spend / rebuyPrice))));
            if (rebuyQuantity >= 1m)
            { bot.BuyLimit(rebuyPrice, rebuyQuantity, 0, stock); }
            return true;
        }

        // 가치 매수: 가치보다 쌀 때만. 보통은 매수 호가에 줄을 서고, 가치보다 20% 넘게 쌀 때만 매도 호가를 가져간다.
        bool lift = premium < -.2m;
        if (lift) { stock.orderBook.GetTopAsks(levels, 1); } else { stock.orderBook.GetTopBids(levels, 1); }
        decimal buyPrice = levels.Count > 0 ? levels[0].price : PriceTickPolicy.FloorToTick(stock.currentPrice * .995m);
        if (buyPrice < 1m || buyPrice >= value)
        { return true; }
        decimal urgency = Math.Max(1m, Math.Min(4m, 1m + Math.Abs(premium) * 4m));
        decimal budget = Math.Max(0m, bot.cash - bot.ReservedCash) * .005m * urgency * (1m - pressure);
        decimal buyQuantity = decimal.Floor(Math.Min(2500m, Math.Min(absorb, budget / buyPrice)));
        if (buyQuantity >= 1m)
        { bot.BuyLimit(buyPrice, buyQuantity, 0, stock); }
        return true;
    }
    private void Liquidate(Fund fund)
    {
        bool sharesRemain = false;
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CopyOwnerOrders(fund.Account, ownOrders);
            foreach (var order in ownOrders) { if (order.isBuy || SessionClock.Time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
            if (fund.Account.GetOwnedVolume(stock) < 1m)
            { continue; }
            sharesRemain = true;
            stock.orderBook.GetTopBids(levels, 1);
            if (levels.Count > 0 && fund.Account.GetHolding(stock) > 0m)
            { fund.Account.SellLimit(levels[0].price, Math.Min(1000m, fund.Account.GetHolding(stock)), 0, stock); }
            break;
        }
        if (!sharesRemain)
        {
            fund.Closed = true; fund.Account.enabled = false;
            Publish(fund.Account.OwnedCompany, fund.Account.Social.Nickname + ": 자산 정리 완료, 운용 중단. 남은 현금 " + fund.Account.cash.ToString("N0") + "원.");
        }
    }
    public bool Controls(Stock stock)
    {
        return stock != null && (PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(stock) : StockManager.Instance.playerAccount.GetOwnedVolume(stock)) > stock.issuedShares * .5m;
    }
    public bool AcquireShares(Stock source, Stock target, Trader payer = null)
    {
        if (!MarketDayController.CanTrade || target == null || !target.AcceptsOrders)
        { BoardResult = "거래 가능한 종목을 선택하세요."; return false; }
        var player = payer != null ? payer : StockManager.Instance.playerAccount;
        target.orderBook.GetTopAsks(levels, 1);
        if (levels.Count == 0)
        { BoardResult = "매도 호가가 없어 지분 매입을 기다립니다."; return false; }
        decimal price = levels[0].price;
        if (price > target.currentPrice * 1.02m)
        { BoardResult = "매도 호가가 현재가에서 너무 멉니다."; return false; }
        decimal owned = PlayerAccounts.Instance != null ? PlayerAccounts.Instance.Owned(target) : player.GetOwnedVolume(target);
        decimal goal = owned > target.issuedShares * .5m ? target.issuedShares : decimal.Floor(target.issuedShares * .5m) + 1;
        decimal pending = 0;
        foreach (var account in Trader.Registry)
        {
            if (!PlayerAccounts.IsPlayer(account)) { continue; }
            target.orderBook.CopyOwnerOrders(account, ownOrders);
            foreach (var order in ownOrders) { if (order.isBuy) { pending += order.volume; } }
        }
        decimal shares = decimal.Floor(Math.Min(Math.Max(0, goal - owned - pending), PlayerAccounts.Available(player) / price));
        if (shares < 1m)
        { BoardResult = "가용 현금 또는 추가 매입 수량이 없습니다."; return false; }
        player.BuyLimit(price, shares, 0, target);
        BoardResult = $"{target.stockName} {shares:N0}주 지분 매입 주문. 실제 체결분만 의결권에 반영됩니다.";
        return true;
    }
    public bool BoardDecision(Stock stock, int action, Trader payer = null)
    {
        if (action < 0 || action > 2 || !Controls(stock))
        { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
        CompanyPolicy policy = action == 0 ? CompanyPolicy.Reserve : action == 1 ? CompanyPolicy.Investment : CompanyPolicy.Dividend;
        bool changed = CorporateEconomy.Instance != null && CorporateEconomy.Instance.SetPolicy(stock, policy);
        BoardResult = changed ? "경영 정책 저장" : "정산 중";
        return changed;
    }
    private void Publish(Stock stock, string text)
    {
        if (headlines.Count >= 6) { headlines.Dequeue(); }
        headlines.Enqueue(text); Journal = string.Join("\n\n", headlines);
        NewsManager.Instance?.AddStatusNews(stock, text);
        MarketDayController.Instance?.PublishBulletin(text);
        MarketEventFeed.Instance?.Notify(text);
    }
    void OnDestroy()
    {
        StockManager.OnStockListed -= Subscribe;
        foreach (var stock in subscribed) { stock.OnMatchedTrade -= Matched; }
        if (Instance == this) { Instance = null; }
    }
}
