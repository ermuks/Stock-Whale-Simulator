using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// 급락 기록 장치. 게임에는 손대지 않고 지켜보기만 한다.
// 어떤 종목이 짧은 구간(Window초) 안에 고점에서 5% 이상 떨어지면, 그 순간의 시장 전체를 한 덩어리로 적는다:
// 다른 종목들도 같이 떨어졌는가, 누가 때려 팔았는가(계좌 종류별), 갈아타기·세력 작전·몰아주기·뉴스가 있었는가.
// 기록은 Logs/CrashWatch.log에 쌓인다(빌드에서는 저장 폴더).
public sealed class CrashWatch : MonoBehaviour
{
    public const float Window = 30f, Trigger = .05f, Notable = .03f, Rest = 45f;
    private struct Sample { public float At; public decimal Price; }
    private struct Deal { public float At; public Stock Stock; public decimal Value; public string Seller, Buyer; public int SellerId; public bool SellerHit; }
    private readonly Dictionary<Stock, Queue<Sample>> prices = new();
    private readonly Dictionary<Stock, float> rested = new();
    private readonly HashSet<Stock> watched = new();
    private readonly Queue<Deal> deals = new();
    private readonly Queue<(float at, string text)> news = new();
    private readonly Queue<(float at, int chases, decimal sold)> chases = new();
    private float next, lastRecord = -999f;
    public static int Records { get; private set; }
    public static string LastRecord { get; private set; } = "";
    private static string LogPath => Application.isEditor ? Path.Combine(Application.dataPath, "..", "Logs", "CrashWatch.log") : Path.Combine(Application.persistentDataPath, "CrashWatch.log");

    private readonly Queue<(float at, string text)> surges = new();
    private void Surge(Stock stock, float ratio) { if (stock != null) { surges.Enqueue((Time.unscaledTime, stock.symbol + " x" + ratio.ToString("0.0"))); while (surges.Count > 40) { surges.Dequeue(); } } }
    void OnEnable() { NewsManager.OnArticlePublished += Article; StockStatistics.OnVolumeSurge += Surge; }
    void OnDisable()
    {
        NewsManager.OnArticlePublished -= Article; StockStatistics.OnVolumeSurge -= Surge;
        foreach (var stock in watched) { if (stock != null) { stock.OnMatchedTrade -= Matched; } }
        watched.Clear();
    }
    private void Article(NewsArticle article)
    {
        if (article == null) { return; }
        news.Enqueue((Time.unscaledTime, (article.symbol ?? "") + " " + article.sentiment.ToString("+0.00;-0.00") + " " + article.title));
        while (news.Count > 12) { news.Dequeue(); }
    }
    private void Matched(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        var from = seller as Trader;
        deals.Enqueue(new Deal { At = Time.unscaledTime, Stock = stock, Value = price * volume, Seller = Kind(seller), Buyer = Kind(buyer), SellerId = from == null ? 0 : from.InitialAccountId, SellerHit = resting != null && resting.isBuy });
    }
    private static string Kind(IOrderOwner owner)
    {
        if (owner == null) { return "?"; }
        if (owner is Trader trader && PlayerAccounts.IsPlayer(trader)) { return "Player"; }
        if (ManipulationTools.IsNominee(owner)) { return "Nominee"; }
        if (owner is WhaleBot whale) { return whale.IsCompanyOwner ? "Whale(owner)" : "Whale"; }
        return owner.GetType().Name;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (now < next) { return; }
        next = now + 1f;
        var manager = StockManager.Instance; var market = MarketDayController.Instance;
        if (manager == null || market == null || !market.Ready) { return; }
        while (deals.Count > 0 && now - deals.Peek().At > Window) { deals.Dequeue(); }
        while (news.Count > 0 && now - news.Peek().at > 180f) { news.Dequeue(); }
        while (surges.Count > 0 && now - surges.Peek().at > 60f) { surges.Dequeue(); }
        chases.Enqueue((now, TradeBot.ChasesTotal, TradeBot.ChaseSoldTotal));
        while (chases.Count > 0 && now - chases.Peek().at > Window) { chases.Dequeue(); }
        Stock fallen = null; decimal worst = 0m;
        foreach (var stock in manager.stocks)
        {
            if (stock == null || stock.currentPrice <= 0m) { continue; }
            if (watched.Add(stock)) { stock.OnMatchedTrade += Matched; }
            if (!prices.TryGetValue(stock, out var line)) { prices[stock] = line = new Queue<Sample>(); }
            line.Enqueue(new Sample { At = now, Price = stock.currentPrice });
            while (line.Count > 0 && now - line.Peek().At > Window) { line.Dequeue(); }
            if (!MarketDayController.CanTrade) { continue; }
            decimal drop = Drop(stock);
            if (drop >= (decimal)Trigger && drop > worst && (!rested.TryGetValue(stock, out float until) || now >= until)) { worst = drop; fallen = stock; }
        }
        if (fallen == null || now - lastRecord < 8f) { return; }
        lastRecord = now; rested[fallen] = now + Rest;
        Write(fallen, worst, now);
    }
    /// <summary>구간 안의 고점에서 지금까지 떨어진 비율(0.07이면 -7%).</summary>
    private decimal Drop(Stock stock)
    {
        if (!prices.TryGetValue(stock, out var line) || line.Count < 2) { return 0m; }
        decimal high = 0m; foreach (var sample in line) { if (sample.Price > high) { high = sample.Price; } }
        return high <= 0m ? 0m : (high - stock.currentPrice) / high;
    }
    private decimal Change(Stock stock)
    {
        if (!prices.TryGetValue(stock, out var line) || line.Count < 2) { return 0m; }
        decimal first = line.Peek().Price;
        return first <= 0m ? 0m : (stock.currentPrice - first) / first;
    }

    private void Write(Stock trigger, decimal drop, float now)
    {
        var manager = StockManager.Instance; var market = MarketDayController.Instance; var text = new StringBuilder();
        int down = 0, total = 0;
        foreach (var stock in manager.stocks) { if (stock == null) { continue; } total++; if (Drop(stock) >= (decimal)Notable) { down++; } }
        text.AppendLine("==== " + GameLocale.Day(market.Day) + " " + (market.ClockMinute / 60).ToString("00") + ":" + (market.ClockMinute % 60).ToString("00") + " · " + trigger.stockName + "(" + trigger.symbol + ") -" + (drop * 100m).ToString("0.0") + "% in " + Window + "s · stocks down 3%+: " + down + "/" + total + (down * 2 >= total ? "  << MARKET-WIDE" : ""));
        // 종목마다: 구간 고점 대비, 구간 처음 대비, 관심(열기), 누가 때려 팔았는지.
        var sellers = new Dictionary<string, decimal>(); var buyers = new Dictionary<string, decimal>(); var who = new HashSet<int>();
        foreach (var stock in manager.stocks)
        {
            if (stock == null) { continue; }
            sellers.Clear(); buyers.Clear(); who.Clear(); decimal hitSell = 0m, hitBuy = 0m;
            foreach (var deal in deals)
            {
                if (deal.Stock != stock) { continue; }
                if (deal.SellerHit) { hitSell += deal.Value; sellers[deal.Seller] = sellers.TryGetValue(deal.Seller, out var s) ? s + deal.Value : deal.Value; who.Add(deal.SellerId); }
                else { hitBuy += deal.Value; buyers[deal.Buyer] = buyers.TryGetValue(deal.Buyer, out var b) ? b + deal.Value : deal.Value; }
            }
            float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
            text.AppendLine("  " + stock.symbol.PadRight(8) + " from high -" + (Drop(stock) * 100m).ToString("0.0").PadLeft(5) + "%  window " + (Change(stock) * 100m).ToString("+0.0;-0.0").PadLeft(6) + "%  price " + stock.currentPrice.ToString("N0").PadLeft(10)
                + "  real " + stock.realValue.ToString("N0") + "  heat " + heat.ToString("0.0") + "  hit-sell " + hitSell.ToString("N0") + " (" + who.Count + " accounts) vs hit-buy " + hitBuy.ToString("N0")
                + (stock.IsTradingHalted ? "  HALTED" : ""));
            if (Drop(stock) >= (decimal)Notable) { text.AppendLine("           sellers: " + Top(sellers) + "   buyers: " + Top(buyers)); }
        }
        // 그 구간에 있었던 일.
        int chased = chases.Count > 0 ? TradeBot.ChasesTotal - chases.Peek().chases : 0; decimal chasedValue = chases.Count > 0 ? TradeBot.ChaseSoldTotal - chases.Peek().sold : 0m;
        text.AppendLine("  chase(rotation) in window: " + chased + " accounts, sold " + chasedValue.ToString("N0") + "  (total " + TradeBot.ChasesTotal + ")");
        var surgeLine = new StringBuilder("  volume surges in last 60s:"); foreach (var item in surges) { surgeLine.Append(" " + item.text + "(-" + (now - item.at).ToString("0") + "s)"); }
        text.AppendLine(surgeLine.ToString());
        var rush = OpeningRush.Instance;
        text.AppendLine("  opening rush stock: " + (rush == null || rush.NpcStock == null ? "none" : rush.NpcStock.symbol + " window " + (Change(rush.NpcStock) * 100m).ToString("+0.0;-0.0") + "%"));
        var line = new StringBuilder("  whale operations:");
        foreach (var trader in Trader.Registry)
        { if (trader is WhaleBot whale && whale.Operating && whale.OwnedCompany != null) { line.Append(" " + whale.OwnedCompany.symbol + ":stage" + whale.OperationStage); } }
        text.AppendLine(line.ToString());
        text.AppendLine("  sentiment " + (MarketSentiment.Instance == null ? "?" : MarketSentiment.Instance.OverallSentiment + " avg deviation " + MarketSentiment.Instance.AverageDeviation.ToString("+0.000;-0.000")) + "  retail exposure scale " + manager.retailExposureScale.ToString("0.000") + "  bots " + market.Bots.Count + "  timeScale " + Time.timeScale + "  frame " + (Time.unscaledDeltaTime * 1000f).ToString("0") + "ms");
        foreach (var item in news) { text.AppendLine("  news -" + (now - item.at).ToString("0") + "s: " + item.text); }
        Records++; LastRecord = text.ToString();
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.AppendAllText(LogPath, LastRecord, Encoding.UTF8); } catch (Exception) { }
        Debug.Log("[급락 기록] " + trigger.stockName + " -" + (drop * 100m).ToString("0.0") + "% · 3% 이상 같이 내린 종목 " + down + "/" + total);
    }
    private static readonly List<KeyValuePair<string, decimal>> sorted = new();
    private static string Top(Dictionary<string, decimal> source)
    {
        if (source.Count == 0) { return "none"; }
        sorted.Clear(); sorted.AddRange(source); sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        decimal sum = 0m; foreach (var item in sorted) { sum += item.Value; }
        var text = new StringBuilder();
        for (int i = 0; i < sorted.Count && i < 4; i++) { text.Append(sorted[i].Key + " " + (sorted[i].Value / Math.Max(1m, sum) * 100m).ToString("0") + "%  "); }
        return text.ToString().TrimEnd();
    }
}
