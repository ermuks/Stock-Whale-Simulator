using System;
using System.Collections.Generic;
using UnityEngine;

// 비상장 소기업. 시가총액 10억 원 미만의 작은 회사들로, 거래소에 없어서 호가창으로는 살 수 없다.
// 주인(대표 계좌)이 주식을 모두 들고 있고, 플레이어는 주인에게 직접 값을 치르고 지분을 넘겨받는다(평가액에 웃돈을 얹은 값).
// 돈과 주식은 실제로 옮겨 갈 뿐 지어내지 않는다: 현금은 플레이어 → 주인, 주식은 주인 → 플레이어. 발행 주식 수는 그대로다.
//
// 지분 50%를 넘기면 다른 회사와 똑같이 경영권을 얻는다: 배당, 경영 정책, 사업 확장(성장 단계는 회사마다 따로다), 100%면 출자, 조건이 되면 상장 신청.
// 주인이 경영하는 동안에는 스스로 상장하지 않는다. 상장은 삼킨 사람이 정한다.
[Serializable]
public sealed class SmallCompanyCatalog
{
    [Serializable]
    public sealed class Entry
    {
        public string id, ko, en, ja, sector, sectorEn, sectorJa;
        public long value;
    }
    public int schemaVersion;
    public long shares = 10000;
    public double premium = .15;
    public Entry[] companies;
}

public sealed partial class CorporateEconomy
{
    public const string SmallPrefix = "SMB_";
    /// <summary>소기업으로 치는 시가총액의 위쪽 끝.</summary>
    public const long SmallValueLimit = 1_000_000_000;
    private SmallCompanyCatalog smallCatalog;
    public string SmallResult { get; private set; } = "";

    private static string Local(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;
    private SmallCompanyCatalog SmallCatalog
    {
        get
        {
            if (smallCatalog != null) { return smallCatalog; }
            var file = Resources.Load<TextAsset>("Progression/SmallCompanies");
            smallCatalog = file == null ? new SmallCompanyCatalog() : JsonUtility.FromJson<SmallCompanyCatalog>(file.text);
            smallCatalog.companies ??= new SmallCompanyCatalog.Entry[0];
            return smallCatalog;
        }
    }
    /// <summary>이 종목이 소기업 목록의 회사인가(상장한 뒤에도 그렇다).</summary>
    public static bool IsSmall(Stock stock) => stock != null && stock.symbol != null && stock.symbol.StartsWith(SmallPrefix, StringComparison.Ordinal);
    private SmallCompanyCatalog.Entry SmallEntry(Stock stock)
    {
        if (!IsSmall(stock)) { return null; }
        foreach (var entry in SmallCatalog.companies) { if (entry != null && entry.id == stock.symbol) { return entry; } }
        return null;
    }
    private Stock FindCompany(string id)
    {
        foreach (var stock in Companies()) { if (stock != null && stock.symbol == id) { return stock; } }
        return null;
    }
    /// <summary>업종 이름(지금 언어).</summary>
    public string SmallSector(Stock stock)
    {
        var entry = SmallEntry(stock);
        return entry == null ? "" : Local(entry.sector, entry.sectorEn, entry.sectorJa);
    }

    /// <summary>
    /// 소기업들을 세계에 놓는다. 세계를 처음 만들 때 한 번, 정해진 순서로 한다(저장을 불러올 때 계좌가 같은 자리에 있어야 한다).
    /// 회사마다 대표 계좌를 하나 만들고 발행 주식을 모두 준다. 현금은 주지 않는다.
    /// </summary>
    private void EnsureSmallCompanies()
    {
        var manager = StockManager.Instance; var catalog = SmallCatalog;
        if (manager == null || catalog.shares < 1) { return; }
        foreach (var entry in catalog.companies)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || !entry.id.StartsWith(SmallPrefix, StringComparison.Ordinal) || entry.value < catalog.shares || entry.value >= SmallValueLimit) { continue; }
            if (FindCompany(entry.id) != null || manager.FindStock(entry.id) != null || !manager.PrivateRoom) { continue; }
            var owner = TradeBot.Create<WhaleBot>("CEO_" + entry.id);
            if (owner == null) { break; }
            decimal price = decimal.Floor(entry.value / (decimal)catalog.shares);
            var stock = manager.RegisterPrivateCompany(entry.id, entry.ko, price, catalog.shares);
            stock.SetRealValue(price); stock.SetFakeValue(price);
            stock.Owner = owner; stock.OwnerId = "CEO_" + entry.id; owner.BindCompany(stock, stock.OwnerId);
            owner.cash = 0; owner.InitialCashAssigned = true;
            owner.GrantShares(stock, stock.issuedShares, price);
            // 같은 업종끼리는 경기를 같이 탄다.
            stock.business = CompanyBusiness.Create(stock.realValue, stock.issuedShares, entry.sector, Rules);
            Ensure(stock).LastDay = DailyIncomeController.Instance?.Ledger.SettledThrough ?? 0;
        }
        ApplySmallNames();
    }
    /// <summary>회사 이름을 지금 언어의 것으로 바꾼다. 이름은 번역기를 거치지 않게 지켜 둔다.</summary>
    private void ApplySmallNames()
    {
        if (StockManager.Instance == null) { return; }
        foreach (var entry in SmallCatalog.companies)
        {
            if (entry == null) { continue; }
            GameLocale.ProtectLiteral(entry.ko); GameLocale.ProtectLiteral(entry.en); GameLocale.ProtectLiteral(entry.ja);
            var stock = FindCompany(entry.id);
            if (stock != null) { stock.stockName = Local(entry.ko, entry.en, entry.ja); }
        }
    }

    /// <summary>주인이 부르는 한 주의 값: 평가액에 웃돈을 얹는다. 원 단위로 올림.</summary>
    public decimal SmallAsk(Stock stock) => stock == null ? 0m : decimal.Ceiling(Math.Max(1m, stock.currentPrice) * (1m + (decimal)Math.Max(0, SmallCatalog.premium)));
    /// <summary>주인(플레이어가 아닌 주주 가운데 가장 많이 든 계좌).</summary>
    private Trader SmallSeller(Stock stock)
    {
        if (stock.Owner != null && !PlayerAccounts.IsPlayer(stock.Owner) && stock.Owner.GetHolding(stock) >= 1m) { return stock.Owner; }
        Trader best = null; decimal most = 0m;
        foreach (var holder in Trader.Registry)
        {
            if (holder == null || PlayerAccounts.IsPlayer(holder)) { continue; }
            decimal held = holder.GetHolding(stock);
            if (held > most) { most = held; best = holder; }
        }
        return best;
    }
    /// <summary>지금 한 번에 넘겨받을 수 있는 주식 수와 값. 경영권(50% 초과)까지 먼저, 그 다음에는 남은 전부. 돈이 모자라면 되는 만큼.</summary>
    public bool SmallOffer(Stock stock, Trader payer, out decimal shares, out decimal price)
    {
        shares = 0m; price = SmallAsk(stock);
        if (!IsSmall(stock) || stock.IsListed || stock.IsDelisted || payer == null || PlayerAccounts.Instance == null) { return false; }
        var seller = SmallSeller(stock);
        if (seller == null) { return false; }
        decimal owned = PlayerAccounts.Instance.Owned(stock);
        decimal goal = owned > stock.issuedShares * .5m ? stock.issuedShares : decimal.Floor(stock.issuedShares * .5m) + 1m;
        shares = decimal.Floor(Math.Min(Math.Min(goal - owned, seller.GetHolding(stock)), PlayerAccounts.Available(payer) / price));
        return shares >= 1m;
    }
    /// <summary>주인에게 값을 치르고 지분을 넘겨받는다.</summary>
    public bool BuySmall(Stock stock, Trader payer)
    {
        if (!IsSmall(stock) || stock.IsListed) { SmallResult = Local("비상장 소기업만 직접 인수할 수 있습니다", "Only unlisted small companies can be bought directly", "直接買収できるのは非上場の小企業だけです"); return false; }
        if (MarketDayController.Instance == null || !MarketDayController.Instance.Ready || MarketDayController.Instance.Phase == MarketPhase.Closing || !PlayerAccounts.Instance.CanUse(payer))
        { SmallResult = Local("지금은 거래할 수 없습니다", "Not available right now", "今は取引できません"); return false; }
        if (!SmallOffer(stock, payer, out decimal shares, out decimal price))
        { SmallResult = PlayerAccounts.Instance.Owned(stock) >= stock.issuedShares ? Local("이미 모두 가지고 있습니다", "You already own all of it", "すでに全株を保有しています") : Local("가용 현금이 모자랍니다 · 주당 ", "Not enough cash · per share ", "現金が足りません · 1株 ") + price.ToString("N0"); return false; }
        var seller = SmallSeller(stock); decimal cost = shares * price, mark = shares * stock.currentPrice;
        payer.ApplyTradeCash(-cost); seller.ApplyTradeCash(cost);
        seller.ForceRemoveHolding(stock, shares); payer.GrantShares(stock, shares, price);
        // 주고받은 것은 거래다. 하루 손익에는 웃돈만큼만 잡히게 한다.
        payer.ExternalFlow(-mark); seller.ExternalFlow(mark);
        ladderAt = -10f; ObserveControl(stock);
        decimal ratio = PlayerAccounts.Instance.Owned(stock) / stock.issuedShares * 100m;
        SmallResult = Local(shares.ToString("N0") + "주 인수 · " + cost.ToString("N0") + "원 · 지분 " + ratio.ToString("0.#") + "%",
            "Bought " + shares.ToString("N0") + " shares · " + cost.ToString("N0") + " KRW · stake " + ratio.ToString("0.#") + "%",
            shares.ToString("N0") + "株を取得 · " + cost.ToString("N0") + "ウォン · 持ち株 " + ratio.ToString("0.#") + "%");
        return true;
    }

    /// <summary>상장 대기 목록이 바뀌었다(종목 창이 다시 그린다).</summary>
    public static event Action ListingQueued;
    /// <summary>시가총액 10억 원을 넘겨 다음 개장에 상장하기로 된 소기업인가.</summary>
    public bool AwaitingListing(Stock stock) => IsSmall(stock) && !stock.IsListed && !stock.IsDelisted && stock.business?.growth != null && stock.business.growth.RequestedDay > 0;
    /// <summary>상장 요건을 따로 보지 않는 소기업의 상장: 평가액이 10억 원 이상이면 된다.</summary>
    private bool SmallListingReady(Stock stock) => IsSmall(stock) && stock.business?.growth != null && stock.business.growth.Appraisal >= SmallValueLimit;
    /// <summary>결산 뒤에 부른다. 평가액이 10억 원을 넘긴 소기업은 다음 날 09:00에 상장한다. 그때까지 종목 창에 '상장 대기 중'으로 보인다.</summary>
    private void QueueSmallListing(Stock stock, int day)
    {
        if (!IsSmall(stock) || stock.IsListed || stock.IsDelisted || stock.IsDelisting) { return; }
        var growth = stock.business?.growth;
        if (growth == null || growth.RequestedDay > 0 || growth.Appraisal < SmallValueLimit) { return; }
        growth.RequestedDay = Math.Max(1, day);
        MarketEventFeed.Instance?.Notify(Local(stock.stockName + " · 시가총액 10억 원 돌파 · 내일 09:00 상장", stock.stockName + " · passed 1 billion KRW · lists tomorrow at 09:00", stock.stockName + " · 時価総額10億ウォン突破 · 明日09:00に上場"));
        ListingQueued?.Invoke();
    }
    private readonly List<Stock> myCompanies = new();
    /// <summary>내가 경영하는 회사들: 내가 세운 기업이 맨 앞, 그 뒤로 삼킨 회사들(작은 것부터).</summary>
    public IReadOnlyList<Stock> MyCompanies()
    {
        myCompanies.Clear();
        var own = PlayerCompany; if (own != null) { myCompanies.Add(own); }
        if (WorldDramaDirector.Instance == null || StockManager.Instance == null) { return myCompanies; }
        int first = myCompanies.Count;
        foreach (var stock in Companies())
        { if (stock != null && stock != own && !stock.IsDelisted && !Unfounded(stock) && WorldDramaDirector.Instance.Controls(stock)) { myCompanies.Add(stock); } }
        myCompanies.Sort(first, myCompanies.Count - first, Comparer<Stock>.Create((a, b) => (a.currentPrice * a.issuedShares).CompareTo(b.currentPrice * b.issuedShares)));
        return myCompanies;
    }

    /// <summary>꿈에서 깬다: 삼킨 비상장 소기업의 지분은 원래 주인에게 돌아간다. 주식 수는 그대로다.</summary>
    private void ReleaseSmallFromDream(Trader player)
    {
        if (player == null || StockManager.Instance == null) { return; }
        foreach (var stock in new List<Stock>(StockManager.Instance.UnlistedStocks))
        {
            if (!IsSmall(stock) || stock.Owner == null || PlayerAccounts.IsPlayer(stock.Owner)) { continue; }
            foreach (var account in new List<Trader>(Trader.Registry))
            {
                if (account == null || !PlayerAccounts.IsPlayer(account)) { continue; }
                decimal owned = account.GetHolding(stock);
                if (owned > 0m) { account.ForceRemoveHolding(stock, owned); stock.Owner.GrantShares(stock, owned, stock.currentPrice); }
            }
            ObserveControl(stock);
        }
    }
}
