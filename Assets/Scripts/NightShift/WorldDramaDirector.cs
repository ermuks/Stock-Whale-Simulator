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
    }
    public readonly List<Fund> Funds = new();
    private readonly Dictionary<Trader, Fund> byAccount = new();
    private readonly List<Stock> subscribed = new();
    private readonly Queue<string> headlines = new();
    private readonly List<(decimal price, decimal volume)> levels = new(1);
    private readonly List<Order> ownOrders = new();
    private readonly Dictionary<Stock, float> boardCooldown = new();
    private readonly Dictionary<Stock, int> boardActions = new();
    private float nextUpdate;
    public string Journal { get; private set; } = "기관과 고래의 움직임을 기록하는 중입니다.";
    public string BoardResult { get; private set; } = "과반 지분을 확보한 기업에서 경영 결정을 내릴 수 있습니다.";
    public int MemoryCount { get; private set; }
    void Awake() { Instance = this; StockManager.OnStockListed += Subscribe; }
    void Start()
    {
        if (StockManager.Instance == null)
        { return; }
        foreach (var stock in StockManager.Instance.stocks) { Subscribe(stock); }
    }
    private void Subscribe(Stock stock)
    {
        if (subscribed.Contains(stock))
        { return; }
        subscribed.Add(stock); stock.OnMatchedTrade += Matched;
    }
    void Update()
    {
        if (!MarketDayController.CanTrade || Time.time < nextUpdate || MarketDayController.Instance == null || !MarketDayController.Instance.Ready)
        { return; }
        nextUpdate = Time.time + 2f;
        if (Funds.Count == 0)
        {
            foreach (var bot in MarketDayController.Instance.Bots)
            {
                if (!bot.IsCompanyOwner || bot.IsExchangeParticipant || bot.Social == null)
                { continue; }
                var fund = new Fund { Account = bot, OpeningEquity = bot.Equity(), PeakEquity = bot.Equity() };
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
            if (fund.Counterparty != null && Time.time >= fund.EvaluateAt)
            { EvaluateMemory(fund); }
            if (fund.Reducing) { Liquidate(fund); }
        }
    }
    private void Matched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order order)
    {
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
        fund.EncounterVolume = volume; fund.Bought = bought; fund.EvaluateAt = Time.time + 20f;
    }
    private void EvaluateMemory(Fund fund)
    {
        decimal change = fund.EncounterStock.currentPrice / Math.Max(1m, fund.EncounterPrice) - 1m;
        float outcome = Mathf.Clamp((float)change * (fund.Bought ? 100f : -100f), -20f, 20f);
        fund.Relationships.TryGetValue(fund.Counterparty, out float score);
        fund.Relationships[fund.Counterparty] = Mathf.Clamp(score + outcome, -100f, 100f);
        MemoryCount++;
        if (Mathf.Abs(outcome) >= 4f && Time.time > fund.NextNotice)
        {
            string other = PlayerAccounts.IsPlayer(fund.Counterparty) ? "플레이어" : fund.Counterparty is TradeBot bot ? bot.Social.Nickname : "맞은편 계좌";
            MarketDayController.Instance.PublishParticipant(fund.Account, other + (outcome < 0f ? "와 거래한 뒤 흐름이 불리해졌네요. 다음에는 경계하겠습니다." : "와 거래한 뒤 흐름이 나쁘지 않았습니다. 다음 움직임도 지켜보죠."));
            fund.NextNotice = Time.time + 120f;
        }
        fund.Counterparty = null;
    }
    public bool CanFundBuy(TradeBot bot) => !byAccount.TryGetValue(bot, out var f) || !f.Reducing && !f.Closed;
    public bool TryFundDecision(TradeBot bot, Stock stock)
    {
        if (!byAccount.TryGetValue(bot, out var fund) || bot is WhaleBot whale && whale.PreDisposal)
        { return false; }
        if (fund.Reducing || fund.Closed)
        { return true; }
        float caution = 0f;
        float relation = 0f;
        fund.Relationships.TryGetValue(StockManager.Instance.playerAccount, out relation);
        if (PlayerAccounts.Instance != null && fund.Relationships.TryGetValue(PlayerAccounts.Instance.automated, out float otherRelation))
        { relation += otherRelation; }
        if (relation != 0f)
        { caution = Mathf.Clamp(-relation / 100f, 0f, 1f); }
        decimal basis = bot.GetOwnedAverage(stock);
        bool sell = bot.GetHolding(stock) > 0m && (basis > 0m && stock.currentPrice > basis * (decimal)(1.04f + bot.Social.Greed * .1f) || bot.Social.Stress > 65f || caution > .35f);
        bool buy = !sell && bot.cash - bot.ReservedCash > bot.Equity() * .03m && stock.currentPrice < stock.fakeValue * (decimal)(1.03f + bot.Social.Greed * .08f);
        if (!sell && !buy)
        { return true; }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        foreach (var order in ownOrders)
        { if (Time.time - order.createdAt > 30f) { stock.orderBook.CancelOrder(order, stock); } }
        stock.orderBook.CopyOwnerOrders(bot, ownOrders);
        if (ownOrders.Count >= 3)
        { return true; }
        if (buy) { stock.orderBook.GetTopAsks(levels, 1); } else { stock.orderBook.GetTopBids(levels, 1); }
        if (levels.Count == 0)
        { return true; }
        decimal price = levels[0].price;
        decimal quantity = decimal.Floor(Math.Min(1000m, buy ? Math.Max(0m, bot.cash - bot.ReservedCash) * .005m / price : bot.GetHolding(stock) * .01m));
        if (quantity >= 1m)
        { if (buy) { bot.BuyLimit(price, quantity, 0, stock); } else { bot.SellLimit(price, quantity, 0, stock); } }
        return true;
    }
    private void Liquidate(Fund fund)
    {
        bool sharesRemain = false;
        foreach (var stock in StockManager.Instance.stocks)
        {
            stock.orderBook.CopyOwnerOrders(fund.Account, ownOrders);
            foreach (var order in ownOrders) { if (order.isBuy || Time.time - order.createdAt > 20f) { stock.orderBook.CancelOrder(order, stock); } }
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
        if (!MarketDayController.CanTrade || !Controls(source) || target == null || target == source || !target.AcceptsOrders)
        { BoardResult = "경영권을 가진 회사와 다른 매입 대상 종목을 선택하세요."; return false; }
        var player = payer != null ? payer : StockManager.Instance.playerAccount;
        target.orderBook.GetTopAsks(levels, 1);
        if (levels.Count == 0)
        { BoardResult = "매도 호가가 없어 지분 매입을 기다립니다."; return false; }
        decimal price = levels[0].price;
        if (price > target.currentPrice * 1.02m)
        { BoardResult = "매도 호가가 현재가에서 너무 멉니다."; return false; }
        decimal shares = decimal.Floor(Math.Min(target.issuedShares * .005m, Math.Max(0m, player.cash - player.ReservedCash) * .1m / price));
        if (shares < 1m)
        { BoardResult = "지분 매입에 쓸 가용 현금이 부족합니다."; return false; }
        player.BuyLimit(price, shares, 0, target);
        BoardResult = $"{target.stockName} {shares:N0}주 지분 매입 주문. 실제 체결분만 의결권에 반영됩니다.";
        return true;
    }
    public bool BoardDecision(Stock stock, int action, Trader payer = null)
    {
        if (!MarketDayController.CanTrade || !Controls(stock))
        { BoardResult = "실제 보유 지분 50% 초과가 필요합니다."; return false; }
        if (boardCooldown.TryGetValue(stock, out float next) && next > Time.time)
        { BoardResult = $"다음 이사회까지 {Mathf.CeilToInt(next - Time.time)}초"; return false; }
        var player = payer != null ? payer : StockManager.Instance.playerAccount;
        decimal cost = action == 2 ? stock.issuedShares : Math.Max(10000m, decimal.Floor(player.cash * .02m));
        if (player.cash - player.ReservedCash < cost)
        { BoardResult = "가용 현금이 부족합니다."; return false; }
        // The action payment is transferred to an existing counterparty, never destroyed or minted.
        if (action == 2)
        {
            decimal payout = 0m;
            foreach (var holder in Trader.Registry) { payout += holder.GetOwnedVolume(stock); }
            if (payout > cost)
            { BoardResult = "주주 원장을 확인할 수 없습니다."; return false; }
            player.ApplyTradeCash(-payout);
            foreach (var holder in Trader.Registry) { holder.ApplyTradeCash(holder.GetOwnedVolume(stock)); }
            cost = payout;
        }
        else { player.ApplyTradeCash(-cost); ExchangeBot.Instance.ApplyTradeCash(cost); }
        boardActions.TryGetValue(stock, out int revision);
        boardActions[stock] = ++revision; boardCooldown[stock] = Time.time + 120f;
        float quality = Mathf.PerlinNoise(revision * .7f, stock.symbol.Length * .31f);
        decimal effect = action == 0 ? .98m + (decimal)quality * .06m : action == 1 ? .95m + (decimal)quality * .12m : 1m;
        stock.SetRealValue(Math.Max(1m, stock.realValue * effect));
        string decision = action == 0 ? "경영진 교체" : action == 1 ? "신사업 투자" : "대주주 자비 특별배당 (주당 1원)";
        BoardResult = decision + " 완료. 시장 평가는 참여자들의 판단에 달려 있습니다.";
        Publish(stock, stock.stockName + ": 플레이어 주도 " + decision + ". 집행 비용 " + cost.ToString("N0") + "원.");
        return true;
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
